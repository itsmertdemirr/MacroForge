namespace MacroForge.Core.Language;

/// <summary>
/// All lexical token categories recognised by the MacroForge script language (.mf files).
/// </summary>
public enum TokenType
{
    Identifier,
    Number,
    String,
    Dot,
    Minus,
    LBrace,
    RBrace,
    LParen,
    RParen,
    Comma,
    NewLine,
    EndOfFile
}
