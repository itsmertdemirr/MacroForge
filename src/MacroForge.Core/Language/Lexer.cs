using System.Text;

namespace MacroForge.Core.Language;

/// <summary>
/// Converts MacroForge script source text into a flat list of <see cref="Token"/>s.
///
/// Grammar notes:
///   - '#' starts a line comment (runs to end of line).
///   - Identifiers: letters, digits, underscore; may not start with a digit.
///   - Numbers: integer or decimal, e.g. 12, 3.5. A leading '-' is handled by the parser, not the lexer.
///   - Strings: double-quoted, supports \" and \\ escapes.
///   - '.' separates namespace-style calls, e.g. mouse.move
///   - '{' '}' delimit blocks (used by repeat).
///   - Newlines are significant: they terminate statements.
/// </summary>
public sealed class Lexer
{
    private readonly string _source;
    private int _pos;
    private int _line = 1;

    public Lexer(string source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (true)
        {
            SkipWhitespaceAndComments(tokens);

            if (IsAtEnd())
            {
                tokens.Add(new Token(TokenType.EndOfFile, string.Empty, _line));
                break;
            }

            char c = Peek();

            if (c == '\n')
            {
                tokens.Add(new Token(TokenType.NewLine, "\\n", _line));
                Advance();
                _line++;
                continue;
            }

            if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            if (c == '"')
            {
                tokens.Add(ReadString());
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                tokens.Add(ReadIdentifier());
                continue;
            }

            switch (c)
            {
                case '.': tokens.Add(Single(TokenType.Dot)); continue;
                case '-': tokens.Add(Single(TokenType.Minus)); continue;
                case '{': tokens.Add(Single(TokenType.LBrace)); continue;
                case '}': tokens.Add(Single(TokenType.RBrace)); continue;
                case '(': tokens.Add(Single(TokenType.LParen)); continue;
                case ')': tokens.Add(Single(TokenType.RParen)); continue;
                case ',': tokens.Add(Single(TokenType.Comma)); continue;
                default:
                    throw new MacroSyntaxException($"Unexpected character '{c}'", _line);
            }
        }

        return tokens;
    }

    private Token Single(TokenType type)
    {
        var t = new Token(type, Peek().ToString(), _line);
        Advance();
        return t;
    }

    private void SkipWhitespaceAndComments(List<Token> tokens)
    {
        while (!IsAtEnd())
        {
            char c = Peek();

            if (c == ' ' || c == '\t' || c == '\r')
            {
                Advance();
                continue;
            }

            if (c == '#')
            {
                while (!IsAtEnd() && Peek() != '\n')
                    Advance();
                continue;
            }

            break;
        }
    }

    private Token ReadNumber()
    {
        int start = _pos;
        int line = _line;
        while (!IsAtEnd() && char.IsDigit(Peek()))
            Advance();

        if (!IsAtEnd() && Peek() == '.' && _pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1]))
        {
            Advance();
            while (!IsAtEnd() && char.IsDigit(Peek()))
                Advance();
        }

        return new Token(TokenType.Number, _source[start.._pos], line);
    }

    private Token ReadString()
    {
        int line = _line;
        Advance(); // consume opening quote
        var sb = new StringBuilder();

        while (!IsAtEnd() && Peek() != '"')
        {
            char c = Peek();
            if (c == '\\' && _pos + 1 < _source.Length)
            {
                char next = _source[_pos + 1];
                switch (next)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    default: sb.Append(next); break;
                }
                Advance();
                Advance();
                continue;
            }

            if (c == '\n')
                _line++;

            sb.Append(c);
            Advance();
        }

        if (IsAtEnd())
            throw new MacroSyntaxException("Unterminated string literal", line);

        Advance(); // consume closing quote
        return new Token(TokenType.String, sb.ToString(), line);
    }

    private Token ReadIdentifier()
    {
        int start = _pos;
        int line = _line;
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
            Advance();

        return new Token(TokenType.Identifier, _source[start.._pos], line);
    }

    private bool IsAtEnd() => _pos >= _source.Length;
    private char Peek() => _source[_pos];
    private void Advance() => _pos++;
}

/// <summary>Thrown when the lexer or parser encounters invalid MacroForge script syntax.</summary>
public sealed class MacroSyntaxException : Exception
{
    public int Line { get; }

    public MacroSyntaxException(string message, int line)
        : base($"{message} (line {line})")
    {
        Line = line;
    }
}
