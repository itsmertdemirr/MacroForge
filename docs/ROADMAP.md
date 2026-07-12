# Roadmap

A rough, non-binding priority order for where MacroForge could go next. "Now" items are the
kind of thing a first contribution could tackle; "Later" items are bigger and would want a
design discussion first (open an issue).

## Now

- [ ] **Variables** — `set $x 10`, then `mouse.move $x 20`. Biggest ergonomics win for scripts
      that repeat similar coordinates/text with small variations.
- [ ] **`repeat` upper bound / warning** — nothing currently stops `repeat 2000000000 { ... }`
      from being written; the loop itself is cheap to *start* but would run effectively forever.
      Worth a soft warning in the editor (not a hard parse error, since a very large repeat count
      combined with `wait 0` might be a legitimate stress-test use case).
- [ ] **`.mf` syntax highlighting outside the app** — the VS Code extension in `editors/vscode-macroforge`
      covers VS Code; a Notepad++ UDL / Sublime syntax file would help other editors.
- [ ] **`RecordedScriptBuilder` O(n²) worst case** — see [BENCHMARKS.md](BENCHMARKS.md#interpreting-results).
      Track open down-events in a dictionary while scanning instead of re-scanning forward for
      each one. Low risk, isolated change, already has unit test coverage to verify against.

## Next

- [ ] **Conditionals** — `if key.down "SHIFT" { ... }` or pixel-color checks (`if pixel 10 10 == "#FF0000"`).
      Needs a design decision on what "condition" even means for a linear input-replay tool —
      probably scoped to keyboard-modifier-state and pixel color, not general expressions.
- [ ] **Relative mouse moves** — `mouse.move.by <dx> <dy>` alongside the existing absolute
      `mouse.move`, for scripts that should still work if a window isn't in exactly the same
      screen position as when recorded.
- [ ] **Multi-monitor DPI awareness** — `mouse.move` currently uses raw screen coordinates via
      `SetCursorPos`; verify/document behaviour under mixed-DPI monitor setups.
- [ ] **Script library / snippets panel** in the app UI, backed by the richer `examples/` set
      added in this pass.
- [ ] **Undo/redo in the script editor** (currently a plain multiline `TextBox`).

## Later (needs design discussion first)

- [ ] **Image/pixel recognition** (`wait_for_pixel`, `wait_for_image`) — the natural next step
      for scripts that need to react to on-screen state rather than blindly replaying timed
      input. Meaningful scope increase (screen capture, template matching); should land as an
      opt-in module, not a Core dependency, to keep the interpreter's footprint small.
- [ ] **A real bytecode/compiled path** — not needed today (see
      [ARCHITECTURE.md](ARCHITECTURE.md#why-the-language-interpreter-walks-the-ast-directly)); would
      become worth it if conditionals/expressions/variables make scripts CPU-bound rather than
      I/O-bound.
- [ ] **Cross-platform input backends** (X11/Wayland on Linux, Quartz on macOS) behind the
      existing `IInputSimulator` seam — the interface is already OS-agnostic on paper; only
      `InputSimulator`'s implementation and `MacroRecorder`'s hooks are Windows-specific.
- [ ] **Installer/packaging** (MSIX or a simple `dotnet publish` zip release on tag push).
- [ ] **Script signing / macro marketplace** — explicitly *not* planned without a strong
      trust/verification story; MacroForge's whole design philosophy (see README) is transparent,
      local, user-initiated automation, and a marketplace of shareable scripts changes that trust
      model in ways that need real thought first.

## Explicitly not planned

- **Hidden/background recording** (capturing input the user didn't knowingly start recording,
  or recording another user's session) — against the project's core design principle, not a
  roadmap gap.
- **Anti-detection features** for bypassing anti-cheat/anti-bot systems — out of scope; this is
  a general local-automation tool, not an evasion tool.
