namespace MacroForge.Core.Language;

/// <summary>
/// A single lexical unit produced by the <see cref="Lexer"/>.
/// </summary>
public readonly record struct Token(TokenType Type, string Text, int Line)
{
    public override string ToString() => $"{Type}('{Text}') @line {Line}";
}
