using System.Text.Json;
using System.Text.Json.Serialization;
using LabelSharpDesigner.Serialization.Converters;

namespace LabelSharpDesigner.Serialization;

public static class JsonOptionsFactory
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new LabelElementJsonConverter());

        return options;
    }

    public static readonly JsonSerializerOptions Default = Create();
}
