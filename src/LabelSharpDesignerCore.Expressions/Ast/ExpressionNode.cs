namespace LabelSharpDesignerCore.Expressions.Ast;

public abstract record ExpressionNode;

public sealed record LiteralNode(object? Value) : ExpressionNode;

public sealed record IdentifierNode(string Name) : ExpressionNode;

public sealed record MemberAccessNode(ExpressionNode Target, string MemberName) : ExpressionNode;

public sealed record CallNode(ExpressionNode Target, string MethodName, IReadOnlyList<ExpressionNode> Arguments) : ExpressionNode;

public sealed record UnaryNode(string Operator, ExpressionNode Operand) : ExpressionNode;

public sealed record BinaryNode(string Operator, ExpressionNode Left, ExpressionNode Right) : ExpressionNode;

public sealed record ConditionalNode(ExpressionNode Condition, ExpressionNode WhenTrue, ExpressionNode WhenFalse) : ExpressionNode;
