using MacroForge.Core.Language;
using MacroForge.Core.Native;

namespace MacroForge.Core;

/// <summary>
/// Executes a parsed <see cref="MacroScript"/> against the live input system.
/// Supports pausing, stopping, and a global speed multiplier for wait times.
/// </summary>
public sealed class Interpreter
{
    private readonly IInputSimulator _input;
    private readonly ManualResetEventSlim _pauseGate = new(initialState: true);

    // Tracks keys/buttons currently held down by key.down / mouse.down so that if the
    // script is stopped or throws partway through, we can release them automatically
    // instead of leaving the physical input stuck "pressed" for the rest of the session.
    private readonly HashSet<string> _heldKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<MouseButton> _heldMouseButtons = new();

    /// <summary>Creates an interpreter that drives the real keyboard/mouse via <see cref="InputSimulator"/>.</summary>
    public Interpreter() : this(new InputSimulator())
    {
    }

    /// <summary>Creates an interpreter against a custom <see cref="IInputSimulator"/> — used by tests to
    /// substitute a fake that records calls instead of touching the real keyboard/mouse.</summary>
    public Interpreter(IInputSimulator input)
    {
        _input = input;
    }

    /// <summary>Multiplies every 'wait' duration. 1.0 = as recorded/written, 0.5 = twice as fast.</summary>
    public double SpeedMultiplier { get; set; } = 1.0;

    /// <summary>Raised before each statement executes; useful for UI highlighting / step debugging.</summary>
    public event Action<Statement>? StatementStarting;

    public bool IsPaused { get; private set; }

    public void Pause()
    {
        IsPaused = true;
        _pauseGate.Reset();
    }

    public void Resume()
    {
        IsPaused = false;
        _pauseGate.Set();
    }

    public async Task RunAsync(MacroScript script, CancellationToken cancellationToken)
    {
        try
        {
            await RunStatementsAsync(script.Statements, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ReleaseAllHeldInput();
        }
    }

    /// <summary>Releases any keys/mouse buttons still held down (e.g. because the script was stopped
    /// mid-way through a key.down/mouse.down without a matching release). Safe to call even if nothing is held.</summary>
    private void ReleaseAllHeldInput()
    {
        foreach (var key in _heldKeys)
            _input.ReleaseKey(key);
        _heldKeys.Clear();

        foreach (var button in _heldMouseButtons)
            _input.MouseUp(button);
        _heldMouseButtons.Clear();
    }

    private async Task RunStatementsAsync(IReadOnlyList<Statement> statements, CancellationToken cancellationToken)
    {
        foreach (var statement in statements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pauseGate.Wait(cancellationToken);

            StatementStarting?.Invoke(statement);

            switch (statement)
            {
                case WaitStatement wait:
                    int delay = (int)Math.Max(0, wait.Milliseconds * SpeedMultiplier);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    break;

                case MouseMoveStatement move:
                    _input.MoveMouseTo(move.X, move.Y);
                    break;

                case MouseClickStatement click:
                    _input.Click(click.Button);
                    break;

                case MouseDownStatement down:
                    _input.MouseDown(down.Button);
                    _heldMouseButtons.Add(down.Button);
                    break;

                case MouseUpStatement up:
                    _input.MouseUp(up.Button);
                    _heldMouseButtons.Remove(up.Button);
                    break;

                case KeyPressStatement key:
                    _input.PressKey(key.Key);
                    break;

                case KeyDownStatement keyDown:
                    _input.HoldKey(keyDown.Key);
                    _heldKeys.Add(keyDown.Key);
                    break;

                case KeyUpStatement keyUp:
                    _input.ReleaseKey(keyUp.Key);
                    _heldKeys.Remove(keyUp.Key);
                    break;

                case KeyTypeStatement type:
                    _input.TypeText(type.Text);
                    break;

                case MouseScrollStatement scroll:
                    _input.Scroll(scroll.Amount);
                    break;

                case RepeatStatement repeat:
                    for (int i = 0; i < repeat.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await RunStatementsAsync(repeat.Body, cancellationToken).ConfigureAwait(false);
                    }
                    break;

                default:
                    throw new NotSupportedException($"Unhandled statement type: {statement.GetType().Name}");
            }
        }
    }
}
