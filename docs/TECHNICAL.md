# Technical Documentation

Reference documentation for the MacroForge script language and the public surface of
`MacroForge.Core`. For the "how it fits together" view, see [ARCHITECTURE.md](ARCHITECTURE.md).

## 1. Language reference (.mf files)

### Grammar (EBNF-ish)

```
script       := statement*
statement    := wait_stmt | repeat_stmt | mouse_stmt | key_stmt
wait_stmt    := "wait" INT
repeat_stmt  := "repeat" INT "{" statement* "}"
mouse_stmt   := "mouse" "." mouse_action
mouse_action := "move" INT INT
              | "click" button
              | "down" button
              | "up" button
              | "scroll" INT
key_stmt     := "key" "." key_action
key_action   := "press" STRING
              | "down" STRING
              | "up" STRING
              | "type" STRING
button       := "left" | "right" | "middle"
INT          := ["-"] DIGIT+ ["." DIGIT+]   ; parsed as double, truncated toward zero
STRING       := '"' (any char | escape)* '"'
```

Notes:
- Whitespace (space/tab/CR) is insignificant except as a token separator. **Newlines are
  significant** — they terminate a statement. There is no statement separator character; each
  statement must be on its own line (blank lines and `# comments` are allowed between them).
- `#` starts a line comment, valid anywhere outside a string.
- String escapes: `\"`, `\\`, `\n`, `\t`; any other `\x` is passed through as `x` unchanged.
- Numbers accept an optional leading `-` and an optional decimal part; the value is parsed as
  `double` then truncated to `int` (`(int)double.Parse(...)`), so `wait 250.9` runs for 250ms,
  not 251ms — truncation, not rounding.
- `repeat` with a count ≤ 0 parses successfully and simply executes its body zero times (no
  error, no infinite loop).

### Statement reference

| Statement | Effect | Notes |
|---|---|---|
| `wait <ms>` | Delays for `ms * SpeedMultiplier` milliseconds | Clamped to ≥ 0 even if `ms` is negative |
| `mouse.move <x> <y>` | Moves the cursor to absolute screen coordinates | Negative values are valid (e.g. multi-monitor setups with a monitor to the left of/above the primary) |
| `mouse.click <button>` | Presses and releases a mouse button at the current cursor position | |
| `mouse.down <button>` / `mouse.up <button>` | Presses/releases a mouse button without the other half | Use for drags: `mouse.move`, `mouse.down`, `mouse.move`, `mouse.up` |
| `mouse.scroll <amount>` | Scrolls the wheel; positive = up, negative = down | One notch is conventionally 120 (`WHEEL_DELTA`) |
| `key.press "NAME"` | Presses and releases one key | See key names below |
| `key.down "NAME"` / `key.up "NAME"` | Holds/releases one key without the other half | Use for combos: `key.down "CTRL"`, `key.press "C"`, `key.up "CTRL"` |
| `key.type "text"` | Types literal Unicode text, one character at a time | Not layout-dependent (uses `KEYEVENTF_UNICODE`, not virtual-key codes) — unlike `key.press`, this works for any Unicode character regardless of keyboard layout |
| `repeat <n> { ... }` | Executes its body `n` times, in order; may nest | |

### Key names

Case-insensitive. Single letters/digits (`"A"`–`"Z"`, `"0"`–`"9"`) map directly. Named keys:
`ENTER`, `TAB`, `ESC`, `SPACE`, `BACKSPACE`, `DELETE`, `INSERT`, `HOME`, `END`, `PAGEUP`,
`PAGEDOWN`, `LEFT`, `UP`, `RIGHT`, `DOWN`, `CAPSLOCK`, `NUMLOCK`, `SCROLLLOCK`, `PRINTSCREEN`,
`PAUSE`, `SHIFT`/`LSHIFT`/`RSHIFT`, `CTRL`/`LCTRL`/`RCTRL`, `ALT`/`LALT`/`RALT`, `LWIN`, `RWIN`,
`MENU`, `F1`–`F24`, `NUMPAD0`–`NUMPAD9`, numpad operators (`MULTIPLY`, `ADD`, `SUBTRACT`,
`DECIMAL`, `DIVIDE`), and US-layout OEM punctuation (`OEM_MINUS`, `OEM_PLUS`, `OEM_COMMA`,
`OEM_PERIOD`, `OEM_1`…`OEM_7`). Anything not in this list can still be referenced by its raw
virtual-key code as `VK_xx` (hex, e.g. `VK_BE`) — this is also what the recorder falls back to
for keys it doesn't have a friendly name for. See `VirtualKeyMap.cs` for the authoritative list.

### Example: everything in one script

```
# Fill a login form and submit with Ctrl+Enter
wait 500
mouse.move 640 400
mouse.click left
key.type "user@example.com"
key.press "TAB"
key.type "hunter2"
key.down "CTRL"
key.press "ENTER"
key.up "CTRL"

# Scroll down and drag-select a paragraph
mouse.scroll -300
mouse.move 100 200
mouse.down left
mouse.move 500 260
mouse.up left

repeat 3 {
    key.press "TAB"
    wait 150
}
```

## 2. Core library API

### `Interpreter`

```csharp
public sealed class Interpreter
{
    public Interpreter();                          // drives the real keyboard/mouse
    public Interpreter(IInputSimulator input);      // for tests / custom backends

    public double SpeedMultiplier { get; set; }     // default 1.0
    public bool IsPaused { get; }
    public event Action<Statement>? StatementStarting;

    public void Pause();
    public void Resume();
    public Task RunAsync(MacroScript script, CancellationToken cancellationToken);
}
```

- `RunAsync` throws `OperationCanceledException` if `cancellationToken` is cancelled mid-run.
  Regardless of how the run ends (completes, cancelled, or an unexpected exception), a `finally`
  block releases any key or mouse button left held down by an unmatched `key.down`/`mouse.down` —
  see [ARCHITECTURE.md](ARCHITECTURE.md#safety-relevant-design-decisions).
- `Pause()` blocks the *next* statement boundary, not mid-statement — a `key.type "..."` in
  progress finishes typing before a pause takes effect. This is intentional: pausing mid-keypress
  could leave a key physically held.
- `StatementStarting` fires **before** a statement executes (including before its `_pauseGate.Wait()`
  for the *next* statement — the currently-dispatching one always fires first), which is what
  `MainForm` uses to highlight/report progress.

### `IInputSimulator` / `InputSimulator`

The seam between the interpreter and the OS. `InputSimulator` is the real, `SendInput`-backed
implementation; substitute a fake for testing (see `FakeInputSimulator` in the test project, or
`NoOpInputSimulator` in the benchmarks project).

### `Lexer` / `Parser`

```csharp
var tokens = new Lexer(sourceText).Tokenize();       // List<Token>
var script = new Parser(tokens).ParseScript();       // MacroScript
// or, in one call:
var script = Parser.Parse(sourceText);
```

Both throw `MacroSyntaxException` (carries a 1-based `Line` number) on malformed input. There is
no recovery/multi-error reporting — parsing stops at the first error, matching the tool's use
case (short, hand-written or recorded scripts, edited in the app's built-in editor).

### `MacroRecorder` / `RecordedScriptBuilder`

`MacroRecorder` owns the Win32 low-level hooks and raw event capture; `RecordedScriptBuilder` is
a pure function (`IReadOnlyList<RecordedEvent> → string`) that turns those events into script
text, including:
- Collapsing an immediate down+up (same key/button, nothing in between) into `key.press` /
  `mouse.move` + `mouse.click`.
- Keeping `key.down`/`key.up` (or `mouse.down`/`mouse.up`) separate when something happened in
  between — i.e. a held modifier combo or a drag.
- Filtering Windows' key auto-repeat (`MacroRecorder` does this before events even reach the
  builder, via `_physicallyDownKeys`).
- Emitting `wait <ms>` for gaps over 5ms between recorded actions.

### `GlobalHotkey`

Thin wrapper around `RegisterHotKey`/`UnregisterHotKey` used for the Ctrl+Alt+Q panic hotkey.
`MainForm` calls `RegisterPanicHotkey` in `OnHandleCreated` and `UnregisterPanicHotkey` in
`OnHandleDestroyed`, and intercepts `WM_HOTKEY` in `WndProc`.

## 3. Testing strategy

See [tests/MacroForge.Core.Tests](../tests/MacroForge.Core.Tests). All tests run against pure
.NET logic — no live Windows desktop session, no real `SendInput`/hook calls — via two seams:

1. **`IInputSimulator`** — `Interpreter` tests inject `FakeInputSimulator`, which records calls
   instead of touching the real keyboard/mouse. This lets tests assert exact call sequences
   (`"move 10 20"`, `"click Left"`, ...) and exercise pause/resume/cancel/cleanup behaviour
   deterministically.
2. **`RecordedScriptBuilder`** — a pure function taking hand-built `RecordedEvent` lists, so the
   collapse/drag/combo logic is tested without a live recording session.

`Lexer`/`Parser` need no seam at all — they're pure functions of source text.

Run tests with coverage locally:

```powershell
dotnet test tests/MacroForge.Core.Tests --settings tests/coverlet.runsettings --results-directory ./coverage
```

CI enforces an 80% minimum line-coverage threshold on `MacroForge.Core` (see
`.github/workflows/ci.yml`); `MacroForge.App`'s WinForms glue code is excluded from that
measurement (see the rationale in `tests/coverlet.runsettings`) since UI event-handler wiring
isn't meaningfully unit-testable and is covered by manual verification instead.
