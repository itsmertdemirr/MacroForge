using MacroForge.Core.Language;
using Xunit;

namespace MacroForge.Core.Tests;

public class ParserTests
{
    [Fact]
    public void Parses_wait_statement()
    {
        var script = Parser.Parse("wait 250\n");
        var wait = Assert.IsType<WaitStatement>(script.Statements[0]);
        Assert.Equal(250, wait.Milliseconds);
    }

    [Fact]
    public void Parses_mouse_move_and_click()
    {
        var script = Parser.Parse("mouse.move 10 20\nmouse.click right\n");

        var move = Assert.IsType<MouseMoveStatement>(script.Statements[0]);
        Assert.Equal(10, move.X);
        Assert.Equal(20, move.Y);

        var click = Assert.IsType<MouseClickStatement>(script.Statements[1]);
        Assert.Equal(MouseButton.Right, click.Button);
    }

    [Fact]
    public void Parses_key_press_and_type()
    {
        var script = Parser.Parse("key.press \"ENTER\"\nkey.type \"hi there\"\n");

        var press = Assert.IsType<KeyPressStatement>(script.Statements[0]);
        Assert.Equal("ENTER", press.Key);

        var type = Assert.IsType<KeyTypeStatement>(script.Statements[1]);
        Assert.Equal("hi there", type.Text);
    }

    [Fact]
    public void Parses_nested_repeat_block()
    {
        var source = "repeat 3 {\n    key.press \"TAB\"\n    wait 100\n}\n";
        var script = Parser.Parse(source);

        var repeat = Assert.IsType<RepeatStatement>(script.Statements[0]);
        Assert.Equal(3, repeat.Count);
        Assert.Equal(2, repeat.Body.Count);
        Assert.IsType<KeyPressStatement>(repeat.Body[0]);
        Assert.IsType<WaitStatement>(repeat.Body[1]);
    }

    [Fact]
    public void Throws_on_unknown_statement()
    {
        Assert.Throws<MacroSyntaxException>(() => Parser.Parse("teleport 1 2\n"));
    }

    [Fact]
    public void Throws_on_missing_closing_brace()
    {
        Assert.Throws<MacroSyntaxException>(() => Parser.Parse("repeat 2 {\nwait 100\n"));
    }

    [Fact]
    public void Parses_negative_mouse_coordinates()
    {
        // Regression test: this used to throw a MacroSyntaxException for '-' before it
        // ever got a chance to check the (correct) "optional unary minus" logic below.
        var script = Parser.Parse("mouse.move -10 -20\n");

        var move = Assert.IsType<MouseMoveStatement>(script.Statements[0]);
        Assert.Equal(-10, move.X);
        Assert.Equal(-20, move.Y);
    }

    [Fact]
    public void Parses_negative_wait_as_zero_or_negative_without_throwing()
    {
        var script = Parser.Parse("wait -5\n");
        var wait = Assert.IsType<WaitStatement>(script.Statements[0]);
        Assert.Equal(-5, wait.Milliseconds);
    }

    [Fact]
    public void Parses_key_down_and_key_up()
    {
        var script = Parser.Parse("key.down \"CTRL\"\nkey.press \"C\"\nkey.up \"CTRL\"\n");

        var down = Assert.IsType<KeyDownStatement>(script.Statements[0]);
        Assert.Equal("CTRL", down.Key);

        var up = Assert.IsType<KeyUpStatement>(script.Statements[2]);
        Assert.Equal("CTRL", up.Key);
    }

    [Fact]
    public void Parses_mouse_scroll()
    {
        var script = Parser.Parse("mouse.scroll -120\n");
        var scroll = Assert.IsType<MouseScrollStatement>(script.Statements[0]);
        Assert.Equal(-120, scroll.Amount);
    }

    [Fact]
    public void Parses_mouse_down_and_up()
    {
        var script = Parser.Parse("mouse.down middle\nmouse.up middle\n");
        Assert.Equal(MouseButton.Middle, Assert.IsType<MouseDownStatement>(script.Statements[0]).Button);
        Assert.Equal(MouseButton.Middle, Assert.IsType<MouseUpStatement>(script.Statements[1]).Button);
    }

    [Fact]
    public void Throws_on_unknown_mouse_action()
    {
        Assert.Throws<MacroSyntaxException>(() => Parser.Parse("mouse.teleport 1 2\n"));
    }

    [Fact]
    public void Throws_on_unknown_key_action()
    {
        Assert.Throws<MacroSyntaxException>(() => Parser.Parse("key.wiggle \"A\"\n"));
    }

    [Fact]
    public void Throws_on_unknown_mouse_button()
    {
        Assert.Throws<MacroSyntaxException>(() => Parser.Parse("mouse.click sideways\n"));
    }

    [Fact]
    public void Truncates_decimal_numbers_toward_zero()
    {
        var script = Parser.Parse("wait 250.9\n");
        var wait = Assert.IsType<WaitStatement>(script.Statements[0]);
        Assert.Equal(250, wait.Milliseconds);
    }

    [Fact]
    public void Ignores_comments_and_blank_lines_between_statements()
    {
        var script = Parser.Parse("# leading comment\n\nwait 10 # inline comment\n\nwait 20\n");
        Assert.Equal(2, script.Statements.Count);
    }

    [Fact]
    public void Empty_script_parses_to_no_statements()
    {
        var script = Parser.Parse("");
        Assert.Empty(script.Statements);
    }

    [Fact]
    public void Empty_repeat_body_is_allowed()
    {
        var script = Parser.Parse("repeat 3 {\n}\n");
        var repeat = Assert.IsType<RepeatStatement>(script.Statements[0]);
        Assert.Empty(repeat.Body);
    }
}
