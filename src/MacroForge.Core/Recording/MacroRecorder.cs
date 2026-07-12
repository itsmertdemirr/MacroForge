using MacroForge.Core.Native;

namespace MacroForge.Core.Recording;

/// <summary>
/// Records the current user's own keyboard and mouse input on this machine, using Win32
/// low-level hooks (WH_KEYBOARD_LL / WH_MOUSE_LL), and turns it into an editable
/// MacroForge (.mf) script via <see cref="RecordedScriptBuilder"/>. Recording is entirely
/// local, explicit, and started/stopped by the user from the app UI — nothing here runs
/// hidden or captures another session.
///
/// Must be constructed and used from a thread with a running Win32 message loop
/// (e.g. the WinForms UI thread), because low-level hooks require one.
/// </summary>
public sealed class MacroRecorder : IDisposable
{
    private readonly List<RecordedEvent> _events = new();
    private readonly System.Diagnostics.Stopwatch _clock = new();

    // Tracks which virtual-key codes are currently physically held, so that Windows'
    // auto-repeat (which re-fires WM_KEYDOWN many times while a key is held) doesn't
    // flood the recording with duplicate key-down events.
    private readonly HashSet<uint> _physicallyDownKeys = new();

    private IntPtr _keyboardHookHandle;
    private IntPtr _mouseHookHandle;
    private NativeMethods.LowLevelProc? _keyboardProc;
    private NativeMethods.LowLevelProc? _mouseProc;

    public bool IsRecording { get; private set; }

    public void Start()
    {
        if (IsRecording)
            return;

        _events.Clear();
        _physicallyDownKeys.Clear();
        _clock.Restart();

        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;

        IntPtr module = NativeMethods.GetModuleHandle(null!);
        _keyboardHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, module, 0);
        _mouseHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, module, 0);

        if (_keyboardHookHandle == IntPtr.Zero || _mouseHookHandle == IntPtr.Zero)
        {
            Stop();
            throw new InvalidOperationException("Failed to install input hooks. Recording requires a UI-thread message loop.");
        }

        IsRecording = true;
    }

    /// <summary>Stops recording and returns the captured session as MacroForge script source text.</summary>
    public string Stop()
    {
        if (_keyboardHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }

        if (_mouseHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }

        IsRecording = false;
        _clock.Stop();

        return RecordedScriptBuilder.Build(_events);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();

            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                // Windows re-sends WM_KEYDOWN repeatedly while a key is held (auto-repeat).
                // Only record the first transition from "up" to "down".
                if (_physicallyDownKeys.Add(data.vkCode))
                    _events.Add(new RecordedEvent(_clock.ElapsedMilliseconds, RecordedEventKind.KeyDown, VkCode: (int)data.vkCode));
            }
            else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
            {
                _physicallyDownKeys.Remove(data.vkCode);
                _events.Add(new RecordedEvent(_clock.ElapsedMilliseconds, RecordedEventKind.KeyUp, VkCode: (int)data.vkCode));
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();

            switch (msg)
            {
                case NativeMethods.WM_LBUTTONDOWN:
                    _events.Add(new RecordedEvent(_clock.ElapsedMilliseconds, RecordedEventKind.MouseDown, X: data.pt.X, Y: data.pt.Y, Button: "left"));
                    break;
                case NativeMethods.WM_LBUTTONUP:
                    _events.Add(new RecordedEvent(_clock.ElapsedMilliseconds, RecordedEventKind.MouseUp, X: data.pt.X, Y: data.pt.Y, Button: "left"));
                    break;
                case NativeMethods.WM_RBUTTONDOWN:
                    _events.Add(new RecordedEvent(_clock.ElapsedMilliseconds, RecordedEventKind.MouseDown, X: data.pt.X, Y: data.pt.Y, Button: "right"));
                    break;
                case NativeMethods.WM_RBUTTONUP:
                    _events.Add(new RecordedEvent(_clock.ElapsedMilliseconds, RecordedEventKind.MouseUp, X: data.pt.X, Y: data.pt.Y, Button: "right"));
                    break;
                case NativeMethods.WM_MBUTTONDOWN:
                    _events.Add(new RecordedEvent(_clock.ElapsedMilliseconds, RecordedEventKind.MouseDown, X: data.pt.X, Y: data.pt.Y, Button: "middle"));
                    break;
                case NativeMethods.WM_MBUTTONUP:
                    _events.Add(new RecordedEvent(_clock.ElapsedMilliseconds, RecordedEventKind.MouseUp, X: data.pt.X, Y: data.pt.Y, Button: "middle"));
                    break;
                case NativeMethods.WM_MOUSEWHEEL:
                    // High word of mouseData is a signed wheel delta in multiples of WHEEL_DELTA (120).
                    int delta = unchecked((short)((data.mouseData >> 16) & 0xFFFF));
                    _events.Add(new RecordedEvent(_clock.ElapsedMilliseconds, RecordedEventKind.MouseScroll, X: data.pt.X, Y: data.pt.Y, ScrollDelta: delta));
                    break;
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (IsRecording)
            Stop();
    }
}
