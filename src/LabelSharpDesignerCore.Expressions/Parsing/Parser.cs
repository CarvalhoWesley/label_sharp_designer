using LabelSharpDesignerCore.Expressions.Ast;
using LabelSharpDesignerCore.Expressions.Tokenizing;

namespace LabelSharpDesignerCore.Expressions.Parsing;

/// <summary>
/// Recursive-descent parser. Precedence, loosest to tightest:
/// conditional (?:) &gt; || &gt; &amp;&amp; &gt; equality &gt; comparison &gt; additive &gt; multiplicative &gt; unary &gt; postfix (member/call) &gt; primary.
/// </summary>
public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _index;

    public Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public static ExpressionNode Parse(string source) => new Parser(Lexer.Tokenize(source)).ParseProgram();

    public ExpressionNode ParseProgram()
    {
        var expression = ParseExpression();
        Expect(TokenType.EndOfInput, "Unexpected trailing input.");
        return expression;
    }

    public ExpressionNode ParseExpression() => ParseConditional();

    private ExpressionNode ParseConditional()
    {
        var condition = ParseLogicalOr();

        if (Match(TokenType.Question))
        {
            var whenTrue = ParseExpression();
            Expect(TokenType.Colon, "Expected ':' in conditional expression.");
            var whenFalse = ParseConditional();
            return new ConditionalNode(condition, whenTrue, whenFalse);
        }

        return condition;
    }

    private ExpressionNode ParseLogicalOr() => ParseLeftAssociativeBinary(ParseLogicalAnd, TokenType.OrOr);

    private ExpressionNode ParseLogicalAnd() => ParseLeftAssociativeBinary(ParseEquality, TokenType.AndAnd);

    private ExpressionNode ParseEquality() => ParseLeftAssociativeBinary(ParseComparison, TokenType.EqualEqual, TokenType.NotEqual);

    private ExpressionNode ParseComparison() => ParseLeftAssociativeBinary(ParseAdditive, TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual);

    private ExpressionNode ParseAdditive() => ParseLeftAssociativeBinary(ParseMultiplicative, TokenType.Plus, TokenType.Minus);

    private ExpressionNode ParseMultiplicative() => ParseLeftAssociativeBinary(ParseUnary, TokenType.Star, TokenType.Slash, TokenType.Percent);

    private ExpressionNode ParseLeftAssociativeBinary(Func<ExpressionNode> operand, params TokenType[] operators)
    {
        var left = operand();

        while (MatchAny(operators, out var op))
        {
            var right = operand();
            left = new BinaryNode(op!.Text, left, right);
        }

        return left;
    }

    private ExpressionNode ParseUnary()
    {
        if (Match(TokenType.Bang) || Match(TokenType.Minus))
        {
            var op = Previous();
            var operand = ParseUnary();
            return new UnaryNode(op.Text, operand);
        }

        return ParsePostfix();
    }

    private ExpressionNode ParsePostfix()
    {
        var expression = ParsePrimary();

        while (true)
        {
            if (Match(TokenType.Dot))
            {
                var name = Expect(TokenType.Identifier, "Expected member name after '.'.").Text;

                if (Match(TokenType.LParen))
                {
                    var arguments = ParseArguments();
                    expression = new CallNode(expression, name, arguments);
                }
                else
                {
                    expression = new MemberAccessNode(expression, name);
                }

                continue;
            }

            break;
        }

        return expression;
    }

    private IReadOnlyList<ExpressionNode> ParseArguments()
    {
        var arguments = new List<ExpressionNode>();

        if (!Check(TokenType.RParen))
        {
            do
            {
                arguments.Add(ParseExpression());
            }
            while (Match(TokenType.Comma));
        }

        Expect(TokenType.RParen, "Expected ')' after argument list.");
        return arguments;
    }

    private ExpressionNode ParsePrimary()
    {
        if (Match(TokenType.Number))
        {
            return new LiteralNode(Previous().NumberValue);
        }

        if (Match(TokenType.String))
        {
            return new LiteralNode(Previous().StringValue);
        }

        if (Match(TokenType.True))
        {
            return new LiteralNode(true);
        }

        if (Match(TokenType.False))
        {
            return new LiteralNode(false);
        }

        if (Match(TokenType.Null))
        {
            return new LiteralNode(null);
        }

        if (Match(TokenType.Identifier))
        {
            return new IdentifierNode(Previous().Text);
        }

        if (Match(TokenType.LParen))
        {
            var expression = ParseExpression();
            Expect(TokenType.RParen, "Expected ')' after expression.");
            return expression;
        }

        var current = Current();
        throw new ExpressionSyntaxException($"Unexpected token '{current.Text}'.", current.Position);
    }

    private bool Match(TokenType type)
    {
        if (!Check(type))
        {
            return false;
        }

        _index++;
        return true;
    }

    private bool MatchAny(TokenType[] types, out Token? matched)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                matched = Current();
                _index++;
                return true;
            }
        }

        matched = null;
        return false;
    }

    private bool Check(TokenType type) => Current().Type == type;

    private Token Current() => _tokens[_index];

    private Token Previous() => _tokens[_index - 1];

    private Token Expect(TokenType type, string message)
    {
        if (!Check(type))
        {
            throw new ExpressionSyntaxException(message, Current().Position);
        }

        var token = Current();
        _index++;
        return token;
    }
}
