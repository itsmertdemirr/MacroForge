namespace MacroForge.Core.Native;

/// <summary>
/// Thin public wrapper around <see cref="NativeMethods.RegisterHotKey"/> / <c>UnregisterHotKey</c>,
/// used to register a system-wide "panic" hotkey that stops a running macro even if the macro
/// itself has moved focus away from the MacroForge window (e.g. by clicking into another app).
/// Without this, a runaway or misbehaving script could make the Stop button unreachable.
/// </summary>
public static class GlobalHotkey
{
    /// <summary>The window message posted to the registered window when the hotkey is pressed.</summary>
    public const int HotkeyMessageId = NativeMethods.WM_HOTKEY;

    /// <summary>Arbitrary, application-unique id used when registering/unregistering the panic hotkey.</summary>
    public const int PanicHotkeyId = 0x4D46; // "MF"

    private const uint VK_Q = 0x51;

    /// <summary>Registers Ctrl+Alt+Q as a global panic hotkey delivered to <paramref name="windowHandle"/> as a WM_HOTKEY message.</summary>
    public static bool RegisterPanicHotkey(IntPtr windowHandle)
    {
        uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
        return NativeMethods.RegisterHotKey(windowHandle, PanicHotkeyId, modifiers, VK_Q);
    }

    /// <summary>Unregisters the panic hotkey. Safe to call even if registration failed or was never attempted.</summary>
    public static void UnregisterPanicHotkey(IntPtr windowHandle)
    {
        NativeMethods.UnregisterHotKey(windowHandle, PanicHotkeyId);
    }
}
