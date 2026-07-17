using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LabelSharpDesignerCore.Core.Elements;

namespace LabelSharpDesignerCore.Serialization.Converters;

public sealed class LabelElementJsonConverter : JsonConverter<LabelElement>
{
    private const string DiscriminatorPropertyName = "$type";

    private static readonly IReadOnlyDictionary<string, Type> DiscriminatorToType = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["text"] = typeof(TextElement),
        ["barcode"] = typeof(BarcodeElement),
        ["qrCode"] = typeof(QrCodeElement),
        ["image"] = typeof(ImageElement),
        ["rectangle"] = typeof(RectangleElement),
        ["ellipse"] = typeof(EllipseElement),
        ["circle"] = typeof(CircleElement),
        ["line"] = typeof(LineElement),
        ["variable"] = typeof(VariableElement),
        ["date"] = typeof(DateElement),
        ["time"] = typeof(TimeElement),
        ["table"] = typeof(TableElement),
        ["group"] = typeof(GroupElement),
    };

    private static readonly IReadOnlyDictionary<Type, string> TypeToDiscriminator =
        DiscriminatorToType.ToDictionary(pair => pair.Value, pair => pair.Key);

    public override LabelElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (!root.TryGetProperty(DiscriminatorPropertyName, out var discriminatorElement))
        {
            throw new JsonException($"Missing '{DiscriminatorPropertyName}' discriminator on label element.");
        }

        var discriminator = discriminatorElement.GetString();
        if (discriminator is null || !DiscriminatorToType.TryGetValue(discriminator, out var concreteType))
        {
            throw new JsonException($"Unknown label element discriminator '{discriminator}'.");
        }

        var rawText = root.GetRawText();
        return (LabelElement)(JsonSerializer.Deserialize(rawText, concreteType, options)
            ?? throw new JsonException($"Failed to deserialize label element of type '{discriminator}'."));
    }

    public override void Write(Utf8JsonWriter writer, LabelElement value, JsonSerializerOptions options)
    {
        var runtimeType = value.GetType();
        if (!TypeToDiscriminator.TryGetValue(runtimeType, out var discriminator))
        {
            throw new JsonException($"No discriminator registered for label element type '{runtimeType.Name}'.");
        }

        var node = JsonSerializer.SerializeToNode(value, runtimeType, options)!.AsObject();
        node[DiscriminatorPropertyName] = JsonValue.Create(discriminator);
        node.WriteTo(writer, options);
    }
}
