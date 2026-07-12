using MacroForge.Core.Language;
using Xunit;

namespace MacroForge.Core.Tests;

public class LexerTests
{
    [Fact]
    public void Tokenizes_simple_wait_statement()
    {
        var tokens = new Lexer("wait 500\n").Tokenize();

        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("wait", tokens[0].Text);
        Assert.Equal(TokenType.Number, tokens[1].Type);
        Assert.Equal("500", tokens[1].Text);
        Assert.Equal(TokenType.NewLine, tokens[2].Type);
        Assert.Equal(TokenType.EndOfFile, tokens[^1].Type);
    }

    [Fact]
    public void Skips_comments()
    {
        var tokens = new Lexer("# this is a comment\nwait 10\n").Tokenize();
        Assert.Equal("wait", tokens[0].Text);
    }

    [Fact]
    public void Reads_dotted_identifiers()
    {
        var tokens = new Lexer("mouse.move 1 2").Tokenize();
        Assert.Equal("mouse", tokens[0].Text);
        Assert.Equal(TokenType.Dot, tokens[1].Type);
        Assert.Equal("move", tokens[2].Text);
    }

    [Fact]
    public void Reads_string_literal_with_escapes()
    {
        var tokens = new Lexer("key.type \"say \\\"hi\\\"\"").Tokenize();
        var stringToken = tokens.First(t => t.Type == TokenType.String);
        Assert.Equal("say \"hi\"", stringToken.Text);
    }

    [Fact]
    public void Skips_comments_and_blank_lines()
    {
        var tokens = new Lexer("# a full-line comment\nwait 5 # trailing comment\n").Tokenize();
        var kinds = tokens.Select(t => t.Type).Where(t => t != TokenType.NewLine).ToList();

        Assert.Equal(new[] { TokenType.Identifier, TokenType.Number, TokenType.EndOfFile }, kinds);
    }

    [Fact]
    public void Reads_decimal_numbers()
    {
        var tokens = new Lexer("3.5").Tokenize();
        Assert.Equal(TokenType.Number, tokens[0].Type);
        Assert.Equal("3.5", tokens[0].Text);
    }

    [Fact]
    public void Reads_string_escapes()
    {
        var tokens = new Lexer("\"a\\\"b\\\\c\\nd\"").Tokenize();
        Assert.Equal("a\"b\\c\nd", tokens[0].Text);
    }

    [Fact]
    public void Throws_on_unterminated_string()
    {
        Assert.Throws<MacroSyntaxException>(() => new Lexer("key.press \"oops").Tokenize());
    }

    [Fact]
    public void Tokenizes_minus_sign_as_its_own_token()
    {
        // Regression test: '-' previously had no case in the lexer's switch and fell through
        // to the "unexpected character" branch, so "mouse.move -10 20" always threw even
        // though the parser had logic that assumed a Minus-like token would exist.
        var tokens = new Lexer("-10").Tokenize();

        Assert.Equal(TokenType.Minus, tokens[0].Type);
        Assert.Equal(TokenType.Number, tokens[1].Type);
        Assert.Equal("10", tokens[1].Text);
    }
}
