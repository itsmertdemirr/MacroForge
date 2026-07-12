<p align="center"><img src="assets/banner.png" alt="MacroForge" width="640"></p>

# MacroForge

[![CI](https://github.com/example/macroforge/actions/workflows/ci.yml/badge.svg)](https://github.com/example/macroforge/actions/workflows/ci.yml)
&nbsp;[![Coverage: MacroForge.Core ≥ 80%](https://img.shields.io/badge/coverage-%E2%89%A580%25-brightgreen)](docs/TECHNICAL.md#3-testing-strategy)
&nbsp;![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)

> Update the `example/macroforge` badge URL above once this repo has a real GitHub remote —
> it's a placeholder so the badge markup is ready to go.

A small Windows macro recorder/player with its own tiny scripting language, written in C# / .NET 9 / WinForms.

Record your own mouse and keyboard actions, get back an editable `.mf` script, tweak it, and play it back — with pause, stop, and a speed slider.

> MacroForge only records and replays input on your own machine, started explicitly by you from the app window. There's no hidden capture, no remote control, and no attempt to disguise synthetic input from the OS.

## Features

- 🎥 **Recorder** — captures your real mouse clicks, drags, key presses/combos, and scroll wheel via Win32 low-level hooks and turns them into a script
- 📝 **Script language** — a tiny, readable DSL (see below) you can hand-write or edit after recording
- ▶️ **Player** — async execution engine with pause/resume, stop, and adjustable playback speed
- 🆘 **Panic hotkey** — Ctrl+Alt+Q stops playback from anywhere, even if the macro moved focus away
- 💾 Save/load scripts as plain-text `.mf` files
- ✅ Unit-tested lexer, parser, interpreter, and recorder logic (≥80% line coverage on `MacroForge.Core`, enforced in CI)
- 🧩 [VS Code extension](editors/vscode-macroforge) for `.mf` syntax highlighting and snippets

## Documentation

- [`docs/TECHNICAL.md`](docs/TECHNICAL.md) — full language reference and Core library API
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — component/sequence diagrams and design rationale
- [`docs/BENCHMARKS.md`](docs/BENCHMARKS.md) — performance methodology and how to reproduce results
- [`docs/ROADMAP.md`](docs/ROADMAP.md) — what's planned, and explicitly what isn't
- [`examples/`](examples) — runnable example scripts, from "hello world" to drag-and-drop and modifier combos

## The MacroForge script language

```
# comments start with a hash
wait 500                      # wait N milliseconds

mouse.move 400 300            # move the cursor to x, y
mouse.click left               # left | right | middle
mouse.down left
mouse.up left

mouse.scroll 120               # scroll the wheel; positive = up, negative = down

key.press "ENTER"             # press and release one key
key.down "CTRL"               # hold a key down (e.g. to build a combo)
key.press "C"                 #   ...press another key while it's held...
key.up "CTRL"                 #   ...then release it — this is Ctrl+C
key.type "Hello world!"       # type literal text

repeat 3 {                    # repeat a block N times
    key.press "TAB"
    wait 200
}

mouse.move -10 -10            # negative coordinates are supported (e.g. relative to a
                               #   window near the screen edge)
```

Special key names: `ENTER`, `TAB`, `ESC`, `SPACE`, `BACKSPACE`, `DELETE`, `HOME`, `END`, `PAGEUP`, `PAGEDOWN`, `LEFT`, `UP`, `RIGHT`, `DOWN`, `SHIFT`, `CTRL`, `ALT`, `WIN`, `CAPSLOCK`, `F1`–`F12`, or any single letter/digit.

If a script is stopped (Stop button or the panic hotkey below) while a `key.down` or
`mouse.down` hasn't been matched with its `key.up` / `mouse.up` yet, MacroForge releases it
automatically so playback never leaves a key or mouse button stuck "pressed".

## Panic hotkey

While a macro is running, **Ctrl+Alt+Q** stops it immediately, from anywhere — even if the
macro itself has moved keyboard focus or the mouse cursor away from the MacroForge window.
This is registered as a system-wide hotkey for as long as the app is open.

An example script is in [`examples/hello.mf`](examples/hello.mf).

## Project structure

```
MacroForge.sln
src/
  MacroForge.Core/        # language (lexer/parser/AST), interpreter, Win32 interop, recorder
    Language/
    Native/
    Recording/
  MacroForge.App/         # WinForms UI
tests/
  MacroForge.Core.Tests/  # xUnit tests for lexer, parser, interpreter, recorder
benchmarks/
  MacroForge.Benchmarks/  # BenchmarkDotNet project — see docs/BENCHMARKS.md
docs/                     # technical reference, architecture diagrams, roadmap
examples/                 # runnable .mf example scripts
editors/
  vscode-macroforge/      # VS Code syntax highlighting + snippets extension
```

## Building & running

Requires the .NET 9 SDK and Windows (WinForms + the input APIs used here are Windows-only).

```bash
dotnet build MacroForge.sln
dotnet run --project src/MacroForge.App
dotnet test tests/MacroForge.Core.Tests
```

With coverage (see [`docs/TECHNICAL.md`](docs/TECHNICAL.md#3-testing-strategy)):

```bash
dotnet test tests/MacroForge.Core.Tests --settings tests/coverlet.runsettings --results-directory ./coverage
```

Benchmarks (see [`docs/BENCHMARKS.md`](docs/BENCHMARKS.md)):

```bash
dotnet run --project benchmarks/MacroForge.Benchmarks -c Release
```

## How it works

- **Lexer/Parser** (`MacroForge.Core/Language`) turn `.mf` source into a small AST (`MacroScript` → `Statement` nodes).
- **Interpreter** (`MacroForge.Core/Interpreter.cs`) walks the AST asynchronously, honoring cancellation, pause/resume, held-input cleanup, and a speed multiplier on `wait`, driving an injectable `IInputSimulator`.
- **InputSimulator** (`MacroForge.Core/Native`) sends real input via `SendInput`/`SetCursorPos`.
- **MacroRecorder** (`MacroForge.Core/Recording`) installs `WH_KEYBOARD_LL` / `WH_MOUSE_LL` hooks while recording; **RecordedScriptBuilder** (pure, no Win32 dependency) turns the captured, timestamped events into `.mf` source text.
- **GlobalHotkey** (`MacroForge.Core/Native`) registers the Ctrl+Alt+Q panic hotkey via `RegisterHotKey`.

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for diagrams of all of the above.

## Changelog

- **v1.3** — Project-maturity pass, no behavior changes to the app itself:
  - **Testability refactor:** `Interpreter` now depends on an `IInputSimulator` abstraction
    (defaults to the real `InputSimulator`) instead of constructing it directly, and the
    recorder's script-building logic was extracted into a pure, dependency-free
    `RecordedScriptBuilder`. This is what makes real (not superficial) unit test coverage
    possible without a live Windows desktop session.
  - **Tests:** expanded from a handful of smoke tests to a full suite covering the lexer,
    parser, interpreter (dispatch, pause/resume, cancellation, held-key/button cleanup, speed
    scaling), and the recorder's collapse/drag/combo logic — enforced at ≥80% line coverage
    on `MacroForge.Core` in CI.
  - **CI:** added `.github/workflows/ci.yml` — builds and tests on `windows-latest`, enforces
    the coverage threshold, and runs the benchmark suite on pushes to `main`.
  - **Docs:** added `docs/TECHNICAL.md` (language + API reference), `docs/ARCHITECTURE.md`
    (component/sequence diagrams), `docs/BENCHMARKS.md` (methodology), and `docs/ROADMAP.md`.
  - **Benchmarks:** added a BenchmarkDotNet project (`benchmarks/MacroForge.Benchmarks`)
    covering lexer/parser throughput, interpreter dispatch overhead, and recorder script-build
    time; CI publishes results as an artifact rather than hard-coding numbers here (see
    `docs/BENCHMARKS.md` for why).
  - **Editor support:** added a VS Code extension (`editors/vscode-macroforge`) with syntax
    highlighting, snippets, and folding for `.mf` files.
  - **Examples:** expanded `examples/` from one script to eight, covering autoclicking, form
    filling, modifier combos, drag-and-drop, scrolling, nested repeats, and Alt+Tab cycling,
    with an index in `examples/README.md`.

- **v1.2** —
  - **Fixed:** negative numbers (e.g. `mouse.move -10 20`) always threw a syntax error — the
    lexer never produced a token for `-`, so the parser's "optional unary minus" check could
    never match. `-` is now a proper token.
  - **Fixed:** the Win32 message constant for `WM_MBUTTONUP` was wrong (it was set to the actual
    value of `WM_MOUSEWHEEL`, `0x020A`). Corrected, and `WM_MOUSEWHEEL` is now defined and used
    for its own purpose: recording scroll-wheel input.
  - **Fixed:** the recorder logged every physical mouse click as a single `mouse.click`, which
    silently dropped drags (button down, move, button up at a different point) and modifier
    combos (e.g. holding Ctrl while pressing C recorded as two independent `key.press` lines that
    don't overlap in time when replayed). The recorder now tracks real key-down/up and
    mouse-down/up pairs, collapsing them back into `mouse.click` / `key.press` only when nothing
    happened in between, and also filters out Windows' key auto-repeat so holding a key doesn't
    flood the script.
  - **Added:** `key.down` / `key.up` for modifier combos, `mouse.scroll` for the wheel.
  - **Added:** a global panic hotkey (**Ctrl+Alt+Q**) that stops a running macro from anywhere,
    even if it has moved focus away from the MacroForge window.
  - **Added:** if a script is stopped while a key or mouse button is held down mid-combo, it's
    now released automatically instead of staying stuck "pressed".

- **v1.1** — Fixed a crash (`Unknown key name 'VK_A4'`) where certain recorded keys (Alt, Ctrl,
  numpad, punctuation, etc.) couldn't be played back. `VirtualKeyMap` now recognises many more
  named keys and safely falls back to raw `VK_xx` codes instead of throwing. Playback errors and
  any other unhandled exception now show a message box instead of crashing to the .NET JIT-debugger
  dialog. Added an app icon/logo (`assets/logo.svg`, built into `app.ico`).

## Limitations / ideas for contributions

- No variables, conditionals, loops-until-condition, or image/pixel recognition yet — intentionally kept small
- The recorder captures discrete clicks, drags, key combos, and scroll, but not a continuous
  mouse-move trail between clicks (so free-hand mouse movement isn't reproduced, only clicks/drags)
- No installer/packaging — run via `dotnet run` or publish yourself

Pull requests welcome. MIT licensed — see [LICENSE](LICENSE).
