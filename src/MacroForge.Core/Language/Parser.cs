namespace MacroForge.Core.Language;

/// <summary>
/// Recursive-descent parser that turns a token stream into a <see cref="MacroScript"/> AST.
///
/// Supported statement forms:
///   wait &lt;number&gt;
///   mouse.move &lt;number&gt; &lt;number&gt;
///   mouse.click left|right|middle
///   mouse.down left|right|middle
///   mouse.up left|right|middle
///   mouse.scroll &lt;number&gt;
///   key.press "KEY"
///   key.down "KEY"
///   key.up "KEY"
///   key.type "text to type"
///   repeat &lt;number&gt; { ...statements... }
/// </summary>
public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    public static MacroScript Parse(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        return new Parser(tokens).ParseScript();
    }

    public MacroScript ParseScript()
    {
        var statements = ParseStatementList(topLevel: true);
        return new MacroScript { Statements = statements };
    }

    private List<Statement> ParseStatementList(bool topLevel)
    {
        var statements = new List<Statement>();

        while (true)
        {
            SkipNewLines();

            if (Check(TokenType.EndOfFile))
            {
                if (!topLevel)
                    throw new MacroSyntaxException("Unexpected end of file, expected '}'", Peek().Line);
                break;
            }

            if (!topLevel && Check(TokenType.RBrace))
                break;

            statements.Add(ParseStatement());
            SkipNewLines();
        }

        return statements;
    }

    private Statement ParseStatement()
    {
        var name = Expect(TokenType.Identifier, "Expected a statement keyword").Text;
        int line = Previous().Line;

        switch (name)
        {
            case "wait":
                return new WaitStatement { Milliseconds = ExpectInt(), Line = line };

            case "repeat":
                return ParseRepeat(line);

            case "mouse":
                return ParseMouseStatement(line);

            case "key":
                return ParseKeyStatement(line);

            default:
                throw new MacroSyntaxException($"Unknown statement '{name}'", line);
        }
    }

    private Statement ParseRepeat(int line)
    {
        int count = ExpectInt();
        Expect(TokenType.LBrace, "Expected '{' after repeat count");
        var body = ParseStatementList(topLevel: false);
        Expect(TokenType.RBrace, "Expected '}' to close repeat block");
        return new RepeatStatement { Count = count, Body = body, Line = line };
    }

    private Statement ParseMouseStatement(int line)
    {
        Expect(TokenType.Dot, "Expected '.' after 'mouse'");
        var action = Expect(TokenType.Identifier, "Expected mouse action").Text;

        switch (action)
        {
            case "move":
                int x = ExpectInt();
                int y = ExpectInt();
                return new MouseMoveStatement { X = x, Y = y, Line = line };
            case "click":
                return new MouseClickStatement { Button = ExpectButton(), Line = line };
            case "down":
                return new MouseDownStatement { Button = ExpectButton(), Line = line };
            case "up":
                return new MouseUpStatement { Button = ExpectButton(), Line = line };
            case "scroll":
                return new MouseScrollStatement { Amount = ExpectInt(), Line = line };
            default:
                throw new MacroSyntaxException($"Unknown mouse action '{action}'", line);
        }
    }

    private Statement ParseKeyStatement(int line)
    {
        Expect(TokenType.Dot, "Expected '.' after 'key'");
        var action = Expect(TokenType.Identifier, "Expected key action").Text;

        switch (action)
        {
            case "press":
                return new KeyPressStatement { Key = ExpectString(), Line = line };
            case "down":
                return new KeyDownStatement { Key = ExpectString(), Line = line };
            case "up":
                return new KeyUpStatement { Key = ExpectString(), Line = line };
            case "type":
                return new KeyTypeStatement { Text = ExpectString(), Line = line };
            default:
                throw new MacroSyntaxException($"Unknown key action '{action}'", line);
        }
    }

    private MouseButton ExpectButton()
    {
        var token = Expect(TokenType.Identifier, "Expected mouse button (left, right, middle)");
        return token.Text switch
        {
            "left" => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => throw new MacroSyntaxException($"Unknown mouse button '{token.Text}'", token.Line)
        };
    }

    private int ExpectInt()
    {
        // Optional unary minus, e.g. "mouse.move -10 20"
        bool negative = false;
        if (Check(TokenType.Minus))
        {
            negative = true;
            Advance();
        }

        var token = Expect(TokenType.Number, "Expected a number");
        int value = (int)double.Parse(token.Text, System.Globalization.CultureInfo.InvariantCulture);
        return negative ? -value : value;
    }

    private string ExpectString()
    {
        return Expect(TokenType.String, "Expected a string literal").Text;
    }

    private void SkipNewLines()
    {
        while (Check(TokenType.NewLine))
            Advance();
    }

    private bool Check(TokenType type) => Peek().Type == type;

    private Token Peek() => _tokens[_pos];

    private Token Previous() => _tokens[_pos - 1];

    private Token Advance()
    {
        var t = _tokens[_pos];
        if (_pos < _tokens.Count - 1)
            _pos++;
        return t;
    }

    private Token Expect(TokenType type, string message)
    {
        if (Check(type))
            return Advance();

        throw new MacroSyntaxException($"{message}, got {Peek().Type} '{Peek().Text}'", Peek().Line);
    }
}
