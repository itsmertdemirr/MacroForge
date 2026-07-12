using BenchmarkDotNet.Attributes;
using MacroForge.Core;
using MacroForge.Core.Language;
using MacroForge.Core.Recording;

namespace MacroForge.Benchmarks;

[MemoryDiagnoser]
public class LexerAndParserBenchmarks
{
    // Representative of a "real" recorded script: a form-fill-and-submit style macro,
    // long enough to be meaningful but typical of what the app actually produces.
    private string _source = "";

    [Params(1, 20, 200)]
    public int RepeatBlocks { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Benchmark script");
        sb.AppendLine("wait 250");
        sb.AppendLine("mouse.move 400 300");
        sb.AppendLine("mouse.click left");
        sb.AppendLine("key.type \"user@example.com\"");
        sb.AppendLine("key.press \"TAB\"");
        sb.AppendLine("key.down \"CTRL\"");
        sb.AppendLine("key.press \"A\"");
        sb.AppendLine("key.up \"CTRL\"");

        for (int i = 0; i < RepeatBlocks; i++)
        {
            sb.AppendLine("repeat 5 {");
            sb.AppendLine("    mouse.move -10 20");
            sb.AppendLine("    mouse.scroll -120");
            sb.AppendLine("    wait 50");
            sb.AppendLine("}");
        }

        _source = sb.ToString();
    }

    [Benchmark(Baseline = true)]
    public List<Token> Lex() => new Lexer(_source).Tokenize();

    [Benchmark]
    public MacroScript LexAndParse() => Parser.Parse(_source);
}

[MemoryDiagnoser]
public class InterpreterBenchmarks
{
    private MacroScript _script = null!;
    private Interpreter _interpreter = null!;

    [Params(1, 20, 200)]
    public int RepeatCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // No 'wait' statements here: we want to measure statement-dispatch overhead, not
        // Task.Delay scheduling. Real-world scripts are dominated by wait time, not CPU.
        var source = $"repeat {RepeatCount} {{\n" +
                      "    mouse.move 10 10\n" +
                      "    mouse.click left\n" +
                      "    key.press \"A\"\n" +
                      "    mouse.scroll 1\n" +
                      "}}\n";

        _script = Parser.Parse(source);
        _interpreter = new Interpreter(new NoOpInputSimulator());
    }

    [Benchmark]
    public async Task RunScript()
    {
        using var cts = new CancellationTokenSource();
        await _interpreter.RunAsync(_script, cts.Token);
    }
}

[MemoryDiagnoser]
public class RecordedScriptBuilderBenchmarks
{
    private List<RecordedEvent> _events = null!;

    [Params(50, 1000)]
    public int EventCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _events = new List<RecordedEvent>(EventCount);
        long t = 0;

        for (int i = 0; i < EventCount; i++)
        {
            // Alternate between simple key presses and stationary clicks — representative
            // of a typical recorded session.
            if (i % 2 == 0)
            {
                _events.Add(new RecordedEvent(t, RecordedEventKind.KeyDown, VkCode: 0x41));
                _events.Add(new RecordedEvent(t + 20, RecordedEventKind.KeyUp, VkCode: 0x41));
            }
            else
            {
                _events.Add(new RecordedEvent(t, RecordedEventKind.MouseDown, X: 100, Y: 100, Button: "left"));
                _events.Add(new RecordedEvent(t + 20, RecordedEventKind.MouseUp, X: 100, Y: 100, Button: "left"));
            }

            t += 100;
        }
    }

    [Benchmark]
    public string Build() => RecordedScriptBuilder.Build(_events);
}
