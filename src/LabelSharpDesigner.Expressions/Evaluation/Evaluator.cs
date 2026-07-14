using System.Globalization;
using System.Reflection;
using LabelSharpDesigner.Expressions.Ast;

namespace LabelSharpDesigner.Expressions.Evaluation;

public sealed class Evaluator
{
    private readonly FunctionRegistry _functions;

    public Evaluator(FunctionRegistry functions)
    {
        _functions = functions;
    }

    public object? Evaluate(ExpressionNode node, EvaluationContext context) => node switch
    {
        LiteralNode literal => literal.Value,
        IdentifierNode identifier => ResolveIdentifier(identifier, context),
        MemberAccessNode member => ResolveMember(Evaluate(member.Target, context), member.MemberName),
        UnaryNode unary => EvaluateUnary(unary, context),
        BinaryNode binary => EvaluateBinary(binary, context),
        ConditionalNode conditional => ToBool(Evaluate(conditional.Condition, context))
            ? Evaluate(conditional.WhenTrue, context)
            : Evaluate(conditional.WhenFalse, context),
        CallNode call => EvaluateCall(call, context),
        _ => throw new NotSupportedException($"Unsupported expression node '{node.GetType().Name}'."),
    };

    private static object? ResolveIdentifier(IdentifierNode identifier, EvaluationContext context)
        => context.TryGetRoot(identifier.Name, out var value)
            ? value
            : throw new ExpressionEvaluationException($"Unknown variable '{identifier.Name}'.");

    private static object? ResolveMember(object? target, string memberName)
    {
        switch (target)
        {
            case null:
                return null;
            case IReadOnlyDictionary<string, object?> dictionary:
                return dictionary.TryGetValue(memberName, out var value) ? value : null;
            default:
                var type = target.GetType();
                var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property is not null)
                {
                    return property.GetValue(target);
                }

                var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field is not null)
                {
                    return field.GetValue(target);
                }

                throw new ExpressionEvaluationException($"'{type.Name}' has no member named '{memberName}'.");
        }
    }

    private object? EvaluateCall(CallNode call, EvaluationContext context)
    {
        var target = Evaluate(call.Target, context);
        var arguments = call.Arguments.Select(argument => Evaluate(argument, context)).ToArray();

        if (!_functions.TryGet(call.MethodName, out var function))
        {
            throw new ExpressionEvaluationException($"Unknown function '{call.MethodName}'.");
        }

        return function.Invoke(target, arguments);
    }

    private object? EvaluateUnary(UnaryNode unary, EvaluationContext context)
    {
        var operand = Evaluate(unary.Operand, context);
        return unary.Operator switch
        {
            "!" => !ToBool(operand),
            "-" => -ToDouble(operand),
            _ => throw new NotSupportedException($"Unsupported unary operator '{unary.Operator}'."),
        };
    }

    private object? EvaluateBinary(BinaryNode binary, EvaluationContext context)
    {
        if (binary.Operator == "&&")
        {
            return ToBool(Evaluate(binary.Left, context)) && ToBool(Evaluate(binary.Right, context));
        }

        if (binary.Operator == "||")
        {
            return ToBool(Evaluate(binary.Left, context)) || ToBool(Evaluate(binary.Right, context));
        }

        var left = Evaluate(binary.Left, context);
        var right = Evaluate(binary.Right, context);

        if (binary.Operator == "+" && (left is string || right is string))
        {
            return ToDisplayString(left) + ToDisplayString(right);
        }

        return binary.Operator switch
        {
            "+" => ToDouble(left) + ToDouble(right),
            "-" => ToDouble(left) - ToDouble(right),
            "*" => ToDouble(left) * ToDouble(right),
            "/" => ToDouble(left) / ToDouble(right),
            "%" => ToDouble(left) % ToDouble(right),
            "==" => AreEqual(left, right),
            "!=" => !AreEqual(left, right),
            "<" => Compare(left, right) < 0,
            "<=" => Compare(left, right) <= 0,
            ">" => Compare(left, right) > 0,
            ">=" => Compare(left, right) >= 0,
            _ => throw new NotSupportedException($"Unsupported binary operator '{binary.Operator}'."),
        };
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            return ToDouble(left).Equals(ToDouble(right));
        }

        return left.Equals(right);
    }

    private static int Compare(object? left, object? right)
    {
        if (IsNumeric(left) && IsNumeric(right))
        {
            return ToDouble(left).CompareTo(ToDouble(right));
        }

        return string.Compare(ToDisplayString(left), ToDisplayString(right), StringComparison.Ordinal);
    }

    private static bool IsNumeric(object? value) => value is double or int or float or decimal or long;

    private static double ToDouble(object? value) => value switch
    {
        null => throw new ExpressionEvaluationException("Cannot use 'null' in an arithmetic expression."),
        double d => d,
        int i => i,
        float f => f,
        decimal m => (double)m,
        long l => l,
        string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => throw new ExpressionEvaluationException($"Value of type '{value.GetType().Name}' is not numeric."),
    };

    private static bool ToBool(object? value) => value switch
    {
        null => false,
        bool b => b,
        double d => d != 0,
        int i => i != 0,
        string s => s.Length > 0,
        _ => true,
    };

    private static string ToDisplayString(object? value) => value switch
    {
        null => string.Empty,
        double d => d.ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
