using MacroForge.Core.Recording;
using Xunit;

namespace MacroForge.Core.Tests;

public class RecordedScriptBuilderTests
{
    [Fact]
    public void Collapses_quick_key_down_up_into_a_single_key_press()
    {
        var events = new[]
        {
            new RecordedEvent(0, RecordedEventKind.KeyDown, VkCode: 0x41), // 'A'
            new RecordedEvent(30, RecordedEventKind.KeyUp, VkCode: 0x41),
        };

        var script = RecordedScriptBuilder.Build(events);

        Assert.Contains("key.press \"A\"", script);
        Assert.DoesNotContain("key.down", script);
        Assert.DoesNotContain("key.up", script);
    }

    [Fact]
    public void Keeps_key_down_and_up_separate_when_something_happens_in_between()
    {
        // Holding CTRL while pressing C (a real combo) must not collapse into key.press,
        // or the "held while" relationship is lost on replay.
        var events = new[]
        {
            new RecordedEvent(0, RecordedEventKind.KeyDown, VkCode: 0xA2),   // LCTRL down
            new RecordedEvent(10, RecordedEventKind.KeyDown, VkCode: 0x43), // 'C' down
            new RecordedEvent(20, RecordedEventKind.KeyUp, VkCode: 0x43),   // 'C' up (collapses to key.press)
            new RecordedEvent(30, RecordedEventKind.KeyUp, VkCode: 0xA2),   // LCTRL up
        };

        var script = RecordedScriptBuilder.Build(events);

        Assert.Contains("key.down \"LCTRL\"", script);
        Assert.Contains("key.press \"C\"", script);
        Assert.Contains("key.up \"LCTRL\"", script);
    }

    [Fact]
    public void Collapses_a_stationary_click_into_move_plus_click()
    {
        var events = new[]
        {
            new RecordedEvent(0, RecordedEventKind.MouseDown, X: 100, Y: 200, Button: "left"),
            new RecordedEvent(20, RecordedEventKind.MouseUp, X: 100, Y: 200, Button: "left"),
        };

        var script = RecordedScriptBuilder.Build(events);

        Assert.Contains("mouse.move 100 200", script);
        Assert.Contains("mouse.click left", script);
        Assert.DoesNotContain("mouse.down", script);
        Assert.DoesNotContain("mouse.up", script);
    }

    [Fact]
    public void Records_a_drag_as_move_down_move_up_when_position_changes_beyond_threshold()
    {
        var events = new[]
        {
            new RecordedEvent(0, RecordedEventKind.MouseDown, X: 0, Y: 0, Button: "left"),
            new RecordedEvent(50, RecordedEventKind.MouseUp, X: 200, Y: 150, Button: "left"),
        };

        var script = RecordedScriptBuilder.Build(events);

        Assert.Contains("mouse.move 0 0", script);
        Assert.Contains("mouse.down left", script);
        Assert.Contains("mouse.move 200 150", script);
        Assert.Contains("mouse.up left", script);
        Assert.DoesNotContain("mouse.click", script);
    }

    [Fact]
    public void Small_movement_under_the_drag_threshold_is_still_a_click()
    {
        var events = new[]
        {
            new RecordedEvent(0, RecordedEventKind.MouseDown, X: 500, Y: 500, Button: "left"),
            new RecordedEvent(10, RecordedEventKind.MouseUp, X: 501, Y: 502, Button: "left"), // 1-2px jitter
        };

        var script = RecordedScriptBuilder.Build(events);

        Assert.Contains("mouse.click left", script);
    }

    [Fact]
    public void Records_scroll_events()
    {
        var events = new[]
        {
            new RecordedEvent(0, RecordedEventKind.MouseScroll, X: 10, Y: 10, ScrollDelta: -120),
        };

        var script = RecordedScriptBuilder.Build(events);

        Assert.Contains("mouse.scroll -120", script);
    }

    [Fact]
    public void Emits_wait_statements_proportional_to_time_gaps()
    {
        var events = new[]
        {
            new RecordedEvent(0, RecordedEventKind.KeyDown, VkCode: 0x41),
            new RecordedEvent(5, RecordedEventKind.KeyUp, VkCode: 0x41),
            new RecordedEvent(505, RecordedEventKind.KeyDown, VkCode: 0x42),
            new RecordedEvent(510, RecordedEventKind.KeyUp, VkCode: 0x42),
        };

        var script = RecordedScriptBuilder.Build(events);

        Assert.Contains("wait 500", script);
    }

    [Fact]
    public void Ignores_negligible_gaps_below_the_5ms_threshold()
    {
        var events = new[]
        {
            new RecordedEvent(0, RecordedEventKind.KeyDown, VkCode: 0x41),
            new RecordedEvent(2, RecordedEventKind.KeyUp, VkCode: 0x41),
        };

        var script = RecordedScriptBuilder.Build(events);

        Assert.DoesNotContain("wait", script);
    }

    [Fact]
    public void A_key_never_released_before_recording_stopped_emits_only_key_down()
    {
        var events = new[]
        {
            new RecordedEvent(0, RecordedEventKind.KeyDown, VkCode: 0xA2), // LCTRL, never released
        };

        var script = RecordedScriptBuilder.Build(events);

        Assert.Contains("key.down \"LCTRL\"", script);
        Assert.DoesNotContain("key.up", script);
    }

    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x39, "9")]
    [InlineData(0x0D, "ENTER")]
    [InlineData(0x70, "F1")]
    [InlineData(0x7B, "F12")]
    [InlineData(0x60, "NUMPAD0")]
    [InlineData(0xA2, "LCTRL")]
    [InlineData(0xBD, "OEM_MINUS")]
    public void Maps_known_virtual_key_codes_to_readable_names(int vk, string expectedName)
    {
        Assert.Equal(expectedName, RecordedScriptBuilder.VkCodeToKeyName(vk));
    }

    [Fact]
    public void Falls_back_to_a_hex_VK_name_for_unmapped_codes()
    {
        Assert.Equal("VK_F0", RecordedScriptBuilder.VkCodeToKeyName(0xF0));
    }

    [Fact]
    public void Empty_event_list_produces_only_the_header_comment()
    {
        var script = RecordedScriptBuilder.Build(Array.Empty<RecordedEvent>());
        Assert.Equal("# Recorded with MacroForge" + Environment.NewLine, script);
    }
}
