using System.Globalization;
using System.Text;

namespace LabelSharpDesigner.Expressions.Tokenizing;

public static class Lexer
{
    public static IReadOnlyList<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < source.Length)
        {
            var c = source[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            var start = i;

            if (char.IsDigit(c))
            {
                while (i < source.Length && (char.IsDigit(source[i]) || source[i] == '.'))
                {
                    i++;
                }

                var text = source.Substring(start, i - start);
                var value = double.Parse(text, CultureInfo.InvariantCulture);
                tokens.Add(new Token(TokenType.Number, text, start) { NumberValue = value });
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                {
                    i++;
                }

                var text = source.Substring(start, i - start);
                tokens.Add(text switch
                {
                    "true" => new Token(TokenType.True, text, start),
                    "false" => new Token(TokenType.False, text, start),
                    "null" => new Token(TokenType.Null, text, start),
                    _ => new Token(TokenType.Identifier, text, start),
                });
                continue;
            }

            if (c is '"' or '\'')
            {
                i++;
                var sb = new StringBuilder();
                while (i < source.Length && source[i] != c)
                {
                    if (source[i] == '\\' && i + 1 < source.Length)
                    {
                        i++;
                        sb.Append(source[i] switch
                        {
                            'n' => '\n',
                            't' => '\t',
                            var other => other,
                        });
                    }
                    else
                    {
                        sb.Append(source[i]);
                    }

                    i++;
                }

                if (i >= source.Length)
                {
                    throw new ExpressionSyntaxException("Unterminated string literal.", start);
                }

                i++;
                tokens.Add(new Token(TokenType.String, sb.ToString(), start) { StringValue = sb.ToString() });
                continue;
            }

            (TokenType Type, int Length) match = c switch
            {
                '.' => (TokenType.Dot, 1),
                ',' => (TokenType.Comma, 1),
                '(' => (TokenType.LParen, 1),
                ')' => (TokenType.RParen, 1),
                '+' => (TokenType.Plus, 1),
                '-' => (TokenType.Minus, 1),
                '*' => (TokenType.Star, 1),
                '/' => (TokenType.Slash, 1),
                '%' => (TokenType.Percent, 1),
                '?' => (TokenType.Question, 1),
                ':' => (TokenType.Colon, 1),
                '=' when Peek(source, i, '=') => (TokenType.EqualEqual, 2),
                '!' when Peek(source, i, '=') => (TokenType.NotEqual, 2),
                '!' => (TokenType.Bang, 1),
                '<' when Peek(source, i, '=') => (TokenType.LessEqual, 2),
                '<' => (TokenType.Less, 1),
                '>' when Peek(source, i, '=') => (TokenType.GreaterEqual, 2),
                '>' => (TokenType.Greater, 1),
                '&' when Peek(source, i, '&') => (TokenType.AndAnd, 2),
                '|' when Peek(source, i, '|') => (TokenType.OrOr, 2),
                _ => throw new ExpressionSyntaxException($"Unexpected character '{c}'.", i),
            };

            tokens.Add(new Token(match.Type, source.Substring(start, match.Length), start));
            i += match.Length;
        }

        tokens.Add(new Token(TokenType.EndOfInput, string.Empty, i));
        return tokens;
    }

    private static bool Peek(string source, int i, char next) => i + 1 < source.Length && source[i + 1] == next;
}
