using MacroForge.Core.Language;
using Xunit;

namespace MacroForge.Core.Tests;

public class InterpreterTests
{
    [Fact]
    public async Task Runs_wait_and_repeat_without_throwing()
    {
        var script = Parser.Parse("wait 10\nrepeat 2 {\n    wait 5\n}\n");
        var interpreter = new Interpreter { SpeedMultiplier = 0.1 };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await interpreter.RunAsync(script, cts.Token);
    }

    [Fact]
    public async Task Cancellation_stops_execution()
    {
        var script = Parser.Parse("wait 5000\n");
        var interpreter = new Interpreter { SpeedMultiplier = 1.0 };

        using var cts = new CancellationTokenSource();
        var run = interpreter.RunAsync(script, cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public void Raises_StatementStarting_event_count_matches_top_level_statements()
    {
        var script = Parser.Parse("wait 1\nwait 1\n");
        var interpreter = new Interpreter { SpeedMultiplier = 0.01 };

        int count = 0;
        interpreter.StatementStarting += _ => count++;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        interpreter.RunAsync(script, cts.Token).GetAwaiter().GetResult();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Dispatches_every_statement_kind_to_the_input_simulator()
    {
        var script = Parser.Parse(
            "mouse.move 10 20\n" +
            "mouse.click left\n" +
            "mouse.down right\n" +
            "mouse.up right\n" +
            "mouse.scroll -120\n" +
            "key.press \"A\"\n" +
            "key.down \"CTRL\"\n" +
            "key.up \"CTRL\"\n" +
            "key.type \"hi\"\n");

        var fake = new FakeInputSimulator();
        var interpreter = new Interpreter(fake) { SpeedMultiplier = 0.01 };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await interpreter.RunAsync(script, cts.Token);

        Assert.Equal(new[]
        {
            "move 10 20",
            "click Left",
            "mousedown Right",
            "mouseup Right",
            "scroll -120",
            "press A",
            "keydown CTRL",
            "keyup CTRL",
            "type hi"
        }, fake.Calls);
    }

    [Fact]
    public async Task Negative_mouse_coordinates_reach_the_input_simulator_unmodified()
    {
        var script = Parser.Parse("mouse.move -50 -75\n");
        var fake = new FakeInputSimulator();
        var interpreter = new Interpreter(fake);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await interpreter.RunAsync(script, cts.Token);

        Assert.Equal(new[] { "move -50 -75" }, fake.Calls);
    }

    [Fact]
    public async Task Repeat_executes_body_the_requested_number_of_times_in_order()
    {
        var script = Parser.Parse("repeat 3 {\n    key.press \"X\"\n}\n");
        var fake = new FakeInputSimulator();
        var interpreter = new Interpreter(fake) { SpeedMultiplier = 0.01 };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await interpreter.RunAsync(script, cts.Token);

        Assert.Equal(new[] { "press X", "press X", "press X" }, fake.Calls);
    }

    [Fact]
    public async Task Repeat_zero_or_negative_executes_body_zero_times()
    {
        var script = Parser.Parse("repeat 0 {\n    key.press \"X\"\n}\nrepeat -3 {\n    key.press \"Y\"\n}\n");
        var fake = new FakeInputSimulator();
        var interpreter = new Interpreter(fake);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await interpreter.RunAsync(script, cts.Token);

        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task Nested_repeat_multiplies_correctly()
    {
        var script = Parser.Parse("repeat 2 {\n    repeat 3 {\n        key.press \"Z\"\n    }\n}\n");
        var fake = new FakeInputSimulator();
        var interpreter = new Interpreter(fake) { SpeedMultiplier = 0.01 };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await interpreter.RunAsync(script, cts.Token);

        Assert.Equal(6, fake.Calls.Count(c => c == "press Z"));
    }

    [Fact]
    public void SpeedMultiplier_scales_wait_duration()
    {
        var script = Parser.Parse("wait 1000\n");
        var fake = new FakeInputSimulator();
        var interpreter = new Interpreter(fake) { SpeedMultiplier = 0.05 }; // ~50ms

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        interpreter.RunAsync(script, cts.Token).GetAwaiter().GetResult();
        sw.Stop();

        // Should complete far faster than the unscaled 1000ms would take.
        Assert.True(sw.ElapsedMilliseconds < 800, $"Expected scaled wait to be fast, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Negative_wait_does_not_throw_and_is_treated_as_zero()
    {
        var script = Parser.Parse("wait -100\n");
        var fake = new FakeInputSimulator();
        var interpreter = new Interpreter(fake);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        interpreter.RunAsync(script, cts.Token).GetAwaiter().GetResult();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 500);
    }

    [Fact]
    public async Task Pause_blocks_execution_until_Resume_is_called()
    {
        var script = Parser.Parse("key.press \"A\"\nkey.press \"B\"\n");
        var fake = new FakeInputSimulator();
        var interpreter = new Interpreter(fake);

        interpreter.Pause();
        Assert.True(interpreter.IsPaused);

        using var cts = new CancellationTokenSource();
        var run = interpreter.RunAsync(script, cts.Token);

        // Give the run a moment to (not) proceed while paused.
        await Task.Delay(100);
        Assert.Empty(fake.Calls);

        interpreter.Resume();
        await run;

        Assert.False(interpreter.IsPaused);
        Assert.Equal(new[] { "press A", "press B" }, fake.Calls);
    }

    [Fact]
    public async Task Cancelling_while_a_key_is_held_releases_it_automatically()
    {
        // key.down with no matching key.up before the script is cancelled.
        var script = Parser.Parse("key.down \"CTRL\"\nwait 5000\nkey.up \"CTRL\"\n");
        var fake = new FakeInputSimulator();
        var interpreter = new Interpreter(fake);

        using var cts = new CancellationTokenSource();
        var run = interpreter.RunAsync(script, cts.Token);

        await Task.Delay(50); // let it reach the key.down and start the long wait
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Contains("keydown CTRL", fake.Calls);
        Assert.Contains("keyup CTRL", fake.Calls); // released automatically by cleanup, not by the script
    }

    [Fact]
    public async Task Cancelling_while_a_mouse_button_is_held_releases_it_automatically()
    {
        var script = Parser.Parse("mouse.down left\nwait 5000\nmouse.up left\n");
        var fake = new FakeInputSimulator();
        var interpreter = new Interpreter(fake);

        using var cts = new CancellationTokenSource();
        var run = interpreter.RunAsync(script, cts.Token);

        await Task.Delay(50);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Contains("mousedown Left", fake.Calls);
        Assert.Contains("mouseup Left", fake.Calls);
    }

    [Fact]
    public async Task Held_key_that_is_properly_released_by_the_script_is_not_released_twice()
    {
        var script = Parser.Parse("key.down \"CTRL\"\nkey.up \"CTRL\"\n");
        var fake = new FakeInputSimulator();
        var interpreter = new Interpreter(fake);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await interpreter.RunAsync(script, cts.Token);

        Assert.Equal(1, fake.Calls.Count(c => c == "keyup CTRL"));
    }
}
