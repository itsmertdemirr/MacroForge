using MacroForge.Core.Language;

namespace MacroForge.Core.Native;

/// <summary>
/// Simulates keyboard and mouse input on behalf of the current, interactively logged-in user
/// via the Win32 SendInput API. Every simulated event is synthetic input the OS attributes
/// to this process — nothing here hides its origin or targets another user's session.
/// </summary>
public sealed class InputSimulator : IInputSimulator
{
    public void MoveMouseTo(int x, int y)
    {
        NativeMethods.SetCursorPos(x, y);
    }

    public void MouseDown(MouseButton button) => SendMouseEvent(button, down: true);

    public void MouseUp(MouseButton button) => SendMouseEvent(button, down: false);

    public void Click(MouseButton button)
    {
        MouseDown(button);
        MouseUp(button);
    }

    private static void SendMouseEvent(MouseButton button, bool down)
    {
        uint flag = button switch
        {
            MouseButton.Left => down ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_LEFTUP,
            MouseButton.Right => down ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_RIGHTUP,
            MouseButton.Middle => down ? NativeMethods.MOUSEEVENTF_MIDDLEDOWN : NativeMethods.MOUSEEVENTF_MIDDLEUP,
            _ => throw new ArgumentOutOfRangeException(nameof(button))
        };

        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT { dwFlags = flag }
            }
        };

        NativeMethods.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }

    /// <summary>Presses and releases a single key, identified by its virtual-key name (e.g. "A", "ENTER", "TAB").</summary>
    public void PressKey(string keyName)
    {
        ushort vk = VirtualKeyMap.Resolve(keyName);
        SendKeyEvent(vk, down: true);
        SendKeyEvent(vk, down: false);
    }

    /// <summary>Presses (but does not release) a key, identified by its virtual-key name. Use with <see cref="ReleaseKey"/> to build modifier combos, e.g. hold CTRL then press C.</summary>
    public void HoldKey(string keyName) => SendKeyEvent(VirtualKeyMap.Resolve(keyName), down: true);

    /// <summary>Releases a previously held key, identified by its virtual-key name.</summary>
    public void ReleaseKey(string keyName) => SendKeyEvent(VirtualKeyMap.Resolve(keyName), down: false);

    /// <summary>Scrolls the mouse wheel. Positive amounts scroll up/forward, negative scroll down/backward. One "notch" is 120 units (WHEEL_DELTA).</summary>
    public void Scroll(int amount)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_WHEEL, mouseData = unchecked((uint)amount) }
            }
        };

        NativeMethods.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }

    /// <summary>Types literal text, one Unicode character at a time.</summary>
    public void TypeText(string text)
    {
        foreach (char c in text)
        {
            SendUnicodeChar(c, down: true);
            SendUnicodeChar(c, down: false);
        }
    }

    private static void SendKeyEvent(ushort vk, bool down)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = down ? 0u : NativeMethods.KEYEVENTF_KEYUP
                }
            }
        };

        NativeMethods.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void SendUnicodeChar(char c, bool down)
    {
        const uint KEYEVENTF_UNICODE = 0x0004;

        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE | (down ? 0u : NativeMethods.KEYEVENTF_KEYUP)
                }
            }
        };

        NativeMethods.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
