# MacroForge for VS Code

Syntax highlighting, snippets, comment toggling, and bracket matching for MacroForge `.mf`
macro scripts.

## Features

- Syntax highlighting for `wait`, `repeat`, `mouse.*`, `key.*` statements, strings, numbers
  (including negative coordinates), and `#` comments.
- Snippets: type `repeat`, `mouse.move`, `mouse.drag`, `key.combo`, etc. and press Tab.
- `#` line-comment toggling (Ctrl+/), bracket auto-closing, and `repeat { ... }` code folding.

## Try it

Open any file in [`../../examples`](../../examples) after installing.

## Local installation (not yet published to the Marketplace)

```bash
cd editors/vscode-macroforge
npm install -g @vscode/vsce   # only needed once
vsce package                  # produces macroforge-0.1.0.vsix
code --install-extension macroforge-0.1.0.vsix
```

Or, for development without packaging: open this folder in VS Code and press F5 to launch an
Extension Development Host with it loaded.

## Structure

- `package.json` — extension manifest, registers the `macroforge` language for `.mf` files
- `language-configuration.json` — comments, brackets, folding
- `syntaxes/macroforge.tmLanguage.json` — TextMate grammar for highlighting
- `snippets/macroforge.code-snippets.json` — code snippets
- `icons/` — light/dark file icons for `.mf` in the file explorer (used if you also enable this
  extension's icon theme contribution point in a future version — currently just supplied for
  the language icon association)
