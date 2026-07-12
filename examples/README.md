# Examples

Load any of these in the app (File → Open, or paste into the editor) and adjust coordinates for
your screen/window before running. All use absolute screen coordinates captured at write time —
they're illustrative starting points, not portable across different screen resolutions.

| File | Demonstrates |
|---|---|
| [`hello.mf`](hello.mf) | The basics: `wait`, `mouse.move`, `mouse.click`, `key.type`, `repeat` |
| [`autoclicker.mf`](autoclicker.mf) | A simple timed repeat-click loop |
| [`login-form-fill.mf`](login-form-fill.mf) | Filling a form: click a field, type, tab, type, submit |
| [`copy-paste-combo.mf`](copy-paste-combo.mf) | `key.down`/`key.up` for modifier combos (Ctrl+A, Ctrl+C, Ctrl+V) |
| [`drag-and-drop.mf`](drag-and-drop.mf) | `mouse.down` + intermediate `mouse.move`s + `mouse.up` for a drag |
| [`scroll-reader.mf`](scroll-reader.mf) | `mouse.scroll` in a timed loop, e.g. for slowly reading/scraping a page |
| [`nested-repeat-grid-fill.mf`](nested-repeat-grid-fill.mf) | Nested `repeat` blocks for grid/spreadsheet-style data entry |
| [`alt-tab-cycle.mf`](alt-tab-cycle.mf) | Holding a modifier (`key.down "ALT"`) across several key presses |

See [`docs/TECHNICAL.md`](../docs/TECHNICAL.md) for the full language reference, including every
statement and the complete list of recognised key names.
