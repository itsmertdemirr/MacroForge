namespace MacroForge.Core.Native;

/// <summary>Maps human-readable key names used in .mf scripts to Win32 virtual-key codes.</summary>
internal static class VirtualKeyMap
{
    private static readonly Dictionary<string, ushort> Map = BuildMap();

    public static ushort Resolve(string keyName)
    {
        var normalized = keyName.Trim().ToUpperInvariant();

        if (Map.TryGetValue(normalized, out var vk))
            return vk;

        if (normalized.Length == 1 && char.IsLetterOrDigit(normalized[0]))
            return (ushort)normalized[0];

        // Fallback format produced by the recorder for keys without a friendly name,
        // e.g. "VK_A4" (left Alt). Accepting this means recorded scripts always play back,
        // even for keys VirtualKeyMap doesn't have a nice name for yet.
        if (normalized.StartsWith("VK_", StringComparison.Ordinal)
            && ushort.TryParse(normalized.AsSpan(3), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var hexVk))
        {
            return hexVk;
        }

        throw new ArgumentException($"Unknown key name '{keyName}'", nameof(keyName));
    }

    private static Dictionary<string, ushort> BuildMap()
    {
        var map = new Dictionary<string, ushort>
        {
            ["ENTER"] = 0x0D,
            ["RETURN"] = 0x0D,
            ["TAB"] = 0x09,
            ["ESC"] = 0x1B,
            ["ESCAPE"] = 0x1B,
            ["SPACE"] = 0x20,
            ["BACKSPACE"] = 0x08,
            ["DELETE"] = 0x2E,
            ["HOME"] = 0x24,
            ["END"] = 0x23,
            ["PAGEUP"] = 0x21,
            ["PAGEDOWN"] = 0x22,
            ["LEFT"] = 0x25,
            ["UP"] = 0x26,
            ["RIGHT"] = 0x27,
            ["DOWN"] = 0x28,
            ["SHIFT"] = 0x10,
            ["CTRL"] = 0x11,
            ["CONTROL"] = 0x11,
            ["ALT"] = 0x12,
            ["WIN"] = 0x5B,
            ["CAPSLOCK"] = 0x14,
            ["PRINTSCREEN"] = 0x2C,
            ["INSERT"] = 0x2D,
            ["NUMLOCK"] = 0x90,
            ["SCROLLLOCK"] = 0x91,
            ["PAUSE"] = 0x13,
            ["MENU"] = 0x5D,

            // Left/right variants, as reported by the recorder for many modifier keys.
            ["LSHIFT"] = 0xA0,
            ["RSHIFT"] = 0xA1,
            ["LCTRL"] = 0xA2,
            ["LCONTROL"] = 0xA2,
            ["RCTRL"] = 0xA3,
            ["RCONTROL"] = 0xA3,
            ["LALT"] = 0xA4,
            ["RALT"] = 0xA5,
            ["LWIN"] = 0x5B,
            ["RWIN"] = 0x5C,

            // Numpad
            ["NUMPAD0"] = 0x60,
            ["NUMPAD1"] = 0x61,
            ["NUMPAD2"] = 0x62,
            ["NUMPAD3"] = 0x63,
            ["NUMPAD4"] = 0x64,
            ["NUMPAD5"] = 0x65,
            ["NUMPAD6"] = 0x66,
            ["NUMPAD7"] = 0x67,
            ["NUMPAD8"] = 0x68,
            ["NUMPAD9"] = 0x69,
            ["MULTIPLY"] = 0x6A,
            ["ADD"] = 0x6B,
            ["SUBTRACT"] = 0x6D,
            ["DECIMAL"] = 0x6E,
            ["DIVIDE"] = 0x6F,

            // Common OEM punctuation keys (US layout).
            ["OEM_MINUS"] = 0xBD,
            ["OEM_PLUS"] = 0xBB,
            ["OEM_COMMA"] = 0xBC,
            ["OEM_PERIOD"] = 0xBE,
            ["OEM_1"] = 0xBA,
            ["OEM_2"] = 0xBF,
            ["OEM_3"] = 0xC0,
            ["OEM_4"] = 0xDB,
            ["OEM_5"] = 0xDC,
            ["OEM_6"] = 0xDD,
            ["OEM_7"] = 0xDE,
        };

        for (int i = 1; i <= 12; i++)
            map[$"F{i}"] = (ushort)(0x70 + i - 1);

        return map;
    }
}
