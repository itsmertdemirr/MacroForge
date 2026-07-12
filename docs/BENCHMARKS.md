# Benchmarks

MacroForge includes a [BenchmarkDotNet](https://benchmarkdotnet.org/) project at
`benchmarks/MacroForge.Benchmarks` that measures the three things that actually matter for a
macro tool's overhead: **parsing** a script, **dispatching** its statements, and **rebuilding**
a script from a recorded session.

> **Why no numbers are hard-coded in this document:** BenchmarkDotNet results depend heavily on
> the exact CPU, OS, and .NET runtime they're measured on, and this repository can't build or run
> on the Linux sandbox this improvement pass was made in (the app is WinForms + Win32 P/Invoke,
> `net9.0-windows`). Rather than invent plausible-looking numbers, CI runs the real benchmark suite
> on every push to `main` (see `.github/workflows/ci.yml`, job `benchmarks`) and publishes the
> results as a build artifact and in the workflow's job summary. That's the actual, trustworthy
> source of truth — check the latest `main` run for current numbers on GitHub-hosted `windows-latest`
> runners.

## Running locally

```powershell
dotnet run --project benchmarks/MacroForge.Benchmarks -c Release
```

Results land in `benchmarks/MacroForge.Benchmarks/BenchmarkDotNet.Artifacts/results/`, including
a GitHub-flavoured Markdown table you can paste directly into an issue or PR description.

## What's measured

| Benchmark class | What it isolates | Why |
|---|---|---|
| `LexerAndParserBenchmarks` | `Lexer.Tokenize` and `Parser.Parse`, at 1 / 20 / 200 repeat-blocks | Scripts are usually short, but a recorded session or a heavily-nested macro can get large; this shows how parse time scales with script size. |
| `InterpreterBenchmarks` | `Interpreter.RunAsync` statement dispatch, with a `NoOpInputSimulator` standing in for real Win32 calls | Real playback is dominated by `wait` durations and the OS's `SendInput` cost, neither of which is interesting to benchmark (they're the *point*, not overhead). This isolates the interpreter's own loop/switch/cancellation-check overhead per statement. |
| `RecordedScriptBuilderBenchmarks` | `RecordedScriptBuilder.Build`, at 50 / 1000 events | This runs once, after recording stops, over the whole session — a long recording session (many minutes of clicking/typing) is the realistic worst case. |

Each benchmark class uses `[MemoryDiagnoser]` so allocations are reported alongside timing,
since GC pressure matters more than raw CPU time for a tool that mostly waits on `Task.Delay`
and OS calls.

## Interpreting results

- **Lexer/Parser**: both are single-pass, O(n) in source length with no backtracking, so timings
  should scale roughly linearly with `RepeatBlocks`. A super-linear trend would indicate an
  accidental O(n²) (e.g. repeated `string` concatenation or `List` re-scans) worth investigating.
- **Interpreter**: dispatch uses a C# `switch` pattern-match over `Statement` subtypes. This is
  O(1) per statement type in practice (the JIT compiles pattern-match switches into efficient
  type-check chains for a handful of cases), so total time should scale linearly with the total
  number of executed statements (`RepeatCount × 4`).
- **RecordedScriptBuilder**: does two backward "find the matching up event" scans per down event
  in the worst case (no matches found), making it O(n²) in adversarial input (e.g. many keys held
  down and never released before recording stops, which is realistically rare — most events pair
  up within a few positions of each other). If profiling ever shows this mattering for very long
  recordings, the fix is to track "currently open" down-events in a `Dictionary<key, index>` while
  scanning instead of re-scanning — noted in the [Roadmap](ROADMAP.md).
