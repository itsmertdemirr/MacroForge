using System.Text;

namespace MacroForge.Core.Recording;

internal enum RecordedEventKind { KeyDown, KeyUp, MouseDown, MouseUp, MouseScroll }

/// <summary>One physically-observed input event captured while recording, in raw (pre-collapse) form.</summary>
internal sealed record RecordedEvent(
    long TimestampMs,
    RecordedEventKind Kind,
    int? VkCode = null,
    int X = 0,
    int Y = 0,
    string? Button = null,
    int ScrollDelta = 0);

/// <summary>
/// Turns a raw list of <see cref="RecordedEvent"/>s into MacroForge script source text.
/// Deliberately has no dependency on Win32 hooks or timers, so it can be unit tested with
/// hand-built event lists instead of requiring a live recording session.
/// </summary>
internal static class RecordedScriptBuilder
{
    // How far the cursor has to move between button-down and button-up, in pixels,
    // before a click is treated as a drag (recorded as mouse.down / mouse.move / mouse.up)
    // instead of a single mouse.click at one point.
    internal const int DragThresholdPixels = 4;

    public static string Build(IReadOnlyList<RecordedEvent> events)
    {
        var lines = new List<(long TimestampMs, string[] Lines)>();
        var consumed = new bool[events.Count];

        for (int i = 0; i < events.Count; i++)
        {
            if (consumed[i])
                continue;

            var evt = events[i];

            switch (evt.Kind)
            {
                case RecordedEventKind.KeyDown:
                {
                    // Collapse a down immediately followed by its matching up (with nothing
                    // else in between) into a single, readable key.press.
                    int upIndex = FindImmediateMatchingKeyUp(events, i, evt.VkCode!.Value);
                    string keyName = VkCodeToKeyName(evt.VkCode.Value);

                    if (upIndex >= 0)
                    {
                        consumed[upIndex] = true;
                        lines.Add((evt.TimestampMs, new[] { $"key.press \"{keyName}\"" }));
                    }
                    else
                    {
                        // Key is held across other events (e.g. a modifier held during a click) —
                        // emit key.down now; the matching key.up will be emitted when we reach it.
                        lines.Add((evt.TimestampMs, new[] { $"key.down \"{keyName}\"" }));
                    }
                    break;
                }

                case RecordedEventKind.KeyUp:
                {
                    string keyName = VkCodeToKeyName(evt.VkCode!.Value);
                    lines.Add((evt.TimestampMs, new[] { $"key.up \"{keyName}\"" }));
                    break;
                }

                case RecordedEventKind.MouseDown:
                {
                    int upIndex = FindMatchingMouseUp(events, i, evt.Button!);
                    if (upIndex >= 0)
                    {
                        var up = events[upIndex];
                        bool moved = Math.Abs(up.X - evt.X) > DragThresholdPixels || Math.Abs(up.Y - evt.Y) > DragThresholdPixels;

                        if (!moved)
                        {
                            consumed[upIndex] = true;
                            lines.Add((evt.TimestampMs, new[]
                            {
                                $"mouse.move {evt.X} {evt.Y}",
                                $"mouse.click {evt.Button}"
                            }));
                            break;
                        }
                    }

                    // Either no matching up was found (button still down when recording stopped)
                    // or the cursor moved enough that this is a drag — emit down now, up (with a
                    // move to its position) when we reach that event below.
                    lines.Add((evt.TimestampMs, new[]
                    {
                        $"mouse.move {evt.X} {evt.Y}",
                        $"mouse.down {evt.Button}"
                    }));
                    break;
                }

                case RecordedEventKind.MouseUp:
                {
                    lines.Add((evt.TimestampMs, new[]
                    {
                        $"mouse.move {evt.X} {evt.Y}",
                        $"mouse.up {evt.Button}"
                    }));
                    break;
                }

                case RecordedEventKind.MouseScroll:
                {
                    lines.Add((evt.TimestampMs, new[] { $"mouse.scroll {evt.ScrollDelta}" }));
                    break;
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("# Recorded with MacroForge");
        long lastTime = 0;

        foreach (var (timestamp, statementLines) in lines)
        {
            long gap = timestamp - lastTime;
            if (gap > 5)
                sb.AppendLine($"wait {gap}");

            foreach (var line in statementLines)
                sb.AppendLine(line);

            lastTime = timestamp;
        }

        return sb.ToString();
    }

    /// <summary>Finds the index of a KeyUp for <paramref name="vk"/> that immediately follows the
    /// KeyDown at <paramref name="downIndex"/> with no other events in between. Returns -1 if the
    /// key was held across other events, or never released before recording stopped.</summary>
    private static int FindImmediateMatchingKeyUp(IReadOnlyList<RecordedEvent> events, int downIndex, int vk)
    {
        int nextIndex = downIndex + 1;
        if (nextIndex >= events.Count)
            return -1;

        var next = events[nextIndex];
        return next.Kind == RecordedEventKind.KeyUp && next.VkCode == vk ? nextIndex : -1;
    }

    /// <summary>Finds the next MouseUp for the same button after <paramref name="downIndex"/>.
    /// Returns -1 if the button was never released before recording stopped.</summary>
    private static int FindMatchingMouseUp(IReadOnlyList<RecordedEvent> events, int downIndex, string button)
    {
        for (int i = downIndex + 1; i < events.Count; i++)
        {
            if (events[i].Kind == RecordedEventKind.MouseUp && events[i].Button == button)
                return i;
        }
        return -1;
    }

    internal static string VkCodeToKeyName(int vk)
    {
        // Printable ASCII letters/digits map directly; everything else falls back to a hex vk code
        // that key.press cannot yet resolve by name — recognisable special keys are listed explicitly.
        if (vk is >= 0x30 and <= 0x5A)
            return ((char)vk).ToString();

        if (vk is >= 0x70 and <= 0x7B) // F1-F12
            return $"F{vk - 0x70 + 1}";

        if (vk is >= 0x60 and <= 0x69) // NUMPAD0-9
            return $"NUMPAD{vk - 0x60}";

        return vk switch
        {
            0x0D => "ENTER",
            0x09 => "TAB",
            0x1B => "ESC",
            0x20 => "SPACE",
            0x08 => "BACKSPACE",
            0x2E => "DELETE",
            0x2D => "INSERT",
            0x24 => "HOME",
            0x23 => "END",
            0x21 => "PAGEUP",
            0x22 => "PAGEDOWN",
            0x25 => "LEFT",
            0x26 => "UP",
            0x27 => "RIGHT",
            0x28 => "DOWN",
            0x14 => "CAPSLOCK",
            0x90 => "NUMLOCK",
            0x91 => "SCROLLLOCK",
            0x2C => "PRINTSCREEN",
            0x13 => "PAUSE",
            // Generic modifiers (rarely delivered by WH_KEYBOARD_LL, which usually reports
            // the left/right-specific codes below, but kept as a safe fallback).
            0x10 => "SHIFT",
            0x11 => "CTRL",
            0x12 => "ALT",
            // Left/right specific modifier codes — what WH_KEYBOARD_LL actually reports.
            0xA0 => "LSHIFT",
            0xA1 => "RSHIFT",
            0xA2 => "LCTRL",
            0xA3 => "RCTRL",
            0xA4 => "LALT",
            0xA5 => "RALT",
            0x5B => "LWIN",
            0x5C => "RWIN",
            0x5D => "MENU",
            // Numpad operators
            0x6A => "MULTIPLY",
            0x6B => "ADD",
            0x6D => "SUBTRACT",
            0x6E => "DECIMAL",
            0x6F => "DIVIDE",
            // Common US-layout punctuation
            0xBD => "OEM_MINUS",
            0xBB => "OEM_PLUS",
            0xBC => "OEM_COMMA",
            0xBE => "OEM_PERIOD",
            0xBA => "OEM_1",
            0xBF => "OEM_2",
            0xC0 => "OEM_3",
            0xDB => "OEM_4",
            0xDC => "OEM_5",
            0xDD => "OEM_6",
            0xDE => "OEM_7",
            _ => $"VK_{vk:X2}"
        };
    }
}
