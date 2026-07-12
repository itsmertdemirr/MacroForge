using MacroForge.Core;
using MacroForge.Core.Language;
using MacroForge.Core.Native;
using MacroForge.Core.Recording;

namespace MacroForge.App;

/// <summary>
/// Main window: a script editor plus record / run / pause / stop controls.
/// Built in code (no .Designer.cs split) to keep the sample small and readable.
/// </summary>
public sealed class MainForm : Form
{
    private readonly TextBox _editor;
    private readonly Label _statusLabel;
    private readonly Button _recordButton;
    private readonly Button _runButton;
    private readonly Button _pauseButton;
    private readonly Button _stopButton;
    private readonly Button _saveButton;
    private readonly Button _loadButton;
    private readonly NumericUpDown _speedInput;

    private readonly MacroRecorder _recorder = new();
    private readonly Interpreter _interpreter = new();
    private CancellationTokenSource? _runCts;

    public MainForm()
    {
        Text = "MacroForge";
        Width = 820;
        Height = 560;
        StartPosition = FormStartPosition.CenterScreen;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(iconPath))
            Icon = new Icon(iconPath);

        _editor = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 11f),
            AcceptsTab = true,
            Text = "# Write a macro, or press Record to capture one.\r\n"
                 + "wait 500\r\n"
                 + "mouse.move 400 300\r\n"
                 + "mouse.click left\r\n"
                 + "key.type \"Hello from MacroForge!\"\r\n"
                 + "repeat 3 {\r\n"
                 + "    key.press \"TAB\"\r\n"
                 + "    wait 200\r\n"
                 + "}\r\n"
        };

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6)
        };

        _recordButton = MakeButton("● Record", OnRecordClicked);
        _runButton = MakeButton("▶ Run", OnRunClicked);
        _pauseButton = MakeButton("‖ Pause", OnPauseClicked);
        _stopButton = MakeButton("■ Stop", OnStopClicked);
        _saveButton = MakeButton("Save .mf", OnSaveClicked);
        _loadButton = MakeButton("Open .mf", OnLoadClicked);

        _speedInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 500,
            Value = 100,
            Width = 60,
            Margin = new Padding(10, 8, 0, 0)
        };
        var speedLabel = new Label { Text = "Speed %", AutoSize = true, Margin = new Padding(4, 12, 0, 0) };

        toolbar.Controls.AddRange(new Control[]
        {
            _recordButton, _runButton, _pauseButton, _stopButton, _saveButton, _loadButton, speedLabel, _speedInput
        });

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            Text = "Ready. Ctrl+Alt+Q stops a running macro from anywhere.",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0)
        };

        Controls.Add(_editor);
        Controls.Add(toolbar);
        Controls.Add(_statusLabel);

        _interpreter.StatementStarting += statement => BeginInvoke(() =>
            SetStatus($"Running: {statement.GetType().Name} (line {statement.Line})"));

        UpdateButtonStates(isRunning: false, isRecording: false);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Ctrl+Alt+Q works as a panic button even if a running macro has moved focus/mouse
        // away from this window, since global hotkeys are delivered regardless of focus.
        if (!GlobalHotkey.RegisterPanicHotkey(Handle))
            SetStatus("Ready. (Could not register the Ctrl+Alt+Q panic hotkey — another app may already be using it.)");
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        GlobalHotkey.UnregisterPanicHotkey(Handle);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == GlobalHotkey.HotkeyMessageId && m.WParam.ToInt32() == GlobalHotkey.PanicHotkeyId)
        {
            PanicStop();
            return;
        }

        base.WndProc(ref m);
    }

    /// <summary>Immediately cancels any running macro and unsticks any paused state, regardless of which window currently has focus.</summary>
    private void PanicStop()
    {
        if (_runCts is null)
            return;

        _runCts.Cancel();
        if (_interpreter.IsPaused)
            _interpreter.Resume();

        SetStatus("Stopped via panic hotkey (Ctrl+Alt+Q).");
    }

    private static Button MakeButton(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(4, 4, 4, 4) };
        button.Click += handler;
        return button;
    }

    private void OnRecordClicked(object? sender, EventArgs e)
    {
        if (!_recorder.IsRecording)
        {
            try
            {
                _recorder.Start();
                UpdateButtonStates(isRunning: false, isRecording: true);
                SetStatus("Recording... perform the actions you want captured, then press Record again to stop.");
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, ex.Message, "Could not start recording", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            var script = _recorder.Stop();
            _editor.Text = script;
            UpdateButtonStates(isRunning: false, isRecording: false);
            SetStatus("Recording stopped. Script generated below — edit freely before running.");
        }
    }

    private async void OnRunClicked(object? sender, EventArgs e)
    {
        MacroScript script;
        try
        {
            script = Parser.Parse(_editor.Text);
        }
        catch (MacroSyntaxException ex)
        {
            MessageBox.Show(this, ex.Message, "Script error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _interpreter.SpeedMultiplier = 100.0 / (double)_speedInput.Value;
        _runCts = new CancellationTokenSource();
        UpdateButtonStates(isRunning: true, isRecording: false);
        SetStatus("Running macro...");

        try
        {
            await _interpreter.RunAsync(script, _runCts.Token);
            SetStatus("Finished.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Stopped.");
        }
        catch (Exception ex)
        {
            // A single bad statement (e.g. an unrecognised key name) should not crash the app —
            // report it and let the user fix the script.
            SetStatus("Stopped — error during playback.");
            MessageBox.Show(this, ex.Message, "Playback error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UpdateButtonStates(isRunning: false, isRecording: false);
        }
    }

    private void OnPauseClicked(object? sender, EventArgs e)
    {
        if (_runCts is null)
            return;

        if (_interpreter.IsPaused)
        {
            _interpreter.Resume();
            _pauseButton.Text = "‖ Pause";
            SetStatus("Resumed.");
        }
        else
        {
            _interpreter.Pause();
            _pauseButton.Text = "▶ Resume";
            SetStatus("Paused.");
        }
    }

    private void OnStopClicked(object? sender, EventArgs e)
    {
        _runCts?.Cancel();
        if (_interpreter.IsPaused)
            _interpreter.Resume(); // release the wait gate so cancellation can propagate
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog { Filter = "MacroForge script (*.mf)|*.mf", FileName = "macro.mf" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName, _editor.Text);
            SetStatus($"Saved to {dialog.FileName}");
        }
    }

    private void OnLoadClicked(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "MacroForge script (*.mf)|*.mf" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _editor.Text = File.ReadAllText(dialog.FileName);
            SetStatus($"Loaded {dialog.FileName}");
        }
    }

    private void UpdateButtonStates(bool isRunning, bool isRecording)
    {
        _recordButton.Enabled = !isRunning;
        _recordButton.Text = isRecording ? "■ Stop Recording" : "● Record";
        _runButton.Enabled = !isRunning && !isRecording;
        _pauseButton.Enabled = isRunning;
        _stopButton.Enabled = isRunning;
        _saveButton.Enabled = !isRecording;
        _loadButton.Enabled = !isRunning && !isRecording;
        if (!isRunning)
            _pauseButton.Text = "‖ Pause";
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => _statusLabel.Text = text);
            return;
        }
        _statusLabel.Text = text;
    }
}
