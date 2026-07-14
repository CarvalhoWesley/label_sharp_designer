using LabelSharpDesigner.Expressions.Evaluation;

namespace LabelSharpDesigner.Expressions.Tests;

public class TemplateResolverTests
{
    private readonly TemplateResolver _resolver = new();

    private static EvaluationContext SampleContext() => new(new Dictionary<string, object?>
    {
        ["produto"] = new Dictionary<string, object?> { ["nome"] = "Parafuso" },
    });

    [Fact]
    public void Resolve_ReplacesPlaceholdersAndKeepsLiteralText()
    {
        var result = _resolver.Resolve("Produto: {{produto.nome}} - fim", SampleContext());

        Assert.Equal("Produto: Parafuso - fim", result);
    }

    [Fact]
    public void Resolve_TemplateWithoutPlaceholders_IsUnchanged()
    {
        Assert.Equal("texto fixo", _resolver.Resolve("texto fixo", SampleContext()));
    }

    [Fact]
    public void Resolve_MultiplePlaceholders_AreAllReplaced()
    {
        var result = _resolver.Resolve("{{produto.nome}} x {{produto.nome}}", SampleContext());

        Assert.Equal("Parafuso x Parafuso", result);
    }
}
