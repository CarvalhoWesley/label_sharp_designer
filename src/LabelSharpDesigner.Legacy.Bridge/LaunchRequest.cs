namespace LabelSharpDesigner.Legacy.Bridge;

/// <summary>
/// What the legacy ASP.NET Framework app asks the satellite <c>LabelSharpDesigner.App</c> process to
/// do: open the editor over a specific <c>.label</c> file, optionally read-only, optionally restricted
/// to a subset of the "+ Adicionar" element types, optionally without the layers sidebar. Encoded/
/// decoded as the command line
/// <c>--edit &lt;path&gt; [--readonly] [--hide-layers-panel] [--elements &lt;comma-separated names&gt;]</c>,
/// so <see cref="ToCommandLineArguments"/> (legacy side, building the process to launch) and
/// <see cref="TryParse"/> (satellite side, decoding its own <c>Main(string[] args)</c>) must stay
/// symmetric.
/// </summary>
public sealed record LaunchRequest
{
    public const string EditFlag = "--edit";
    public const string ReadOnlyFlag = "--readonly";
    public const string ElementsFlag = "--elements";
    public const string HideLayersPanelFlag = "--hide-layers-panel";

    public required string FilePath { get; init; }
    public bool ReadOnly { get; init; }

    /// <summary>Which element types the editor's "+ Adicionar" menu offers, by
    /// <c>NewElementKind.ToString()</c> name (e.g. <c>"Text"</c>, <c>"Barcode"</c>, <c>"QrCode"</c>) —
    /// this project can't reference <c>LabelSharpDesigner.App</c>'s actual enum (wrong dependency
    /// direction: <c>App</c> already depends on this project), so names are the shared contract
    /// instead. <see langword="null"/> or empty (the default) offers every element type; the satellite
    /// process silently ignores any name it doesn't recognize rather than failing the whole launch.</summary>
    public IReadOnlyList<string>? AllowedElementKinds { get; init; }

    /// <summary>Whether the editor's layers sidebar is offered at all — <see langword="true"/> (the
    /// default, and the wire absence of <see cref="HideLayersPanelFlag"/>) matches today's behavior,
    /// where the end user can still show/hide it themselves; <see langword="false"/> means the
    /// satellite never builds the panel, regardless of that end-user preference.</summary>
    public bool ShowLayersPanel { get; init; } = true;

    /// <summary>Builds the single, properly quoted argument string for
    /// <see cref="System.Diagnostics.ProcessStartInfo.Arguments"/> — <c>ArgumentList</c> isn't
    /// available on netstandard2.0, so this hand-escapes using the same rule the Windows C runtime
    /// (and therefore .NET's own <c>Main(string[] args)</c> splitter) uses to parse argv, guaranteeing
    /// <see cref="TryParse"/> on the receiving end sees the path back exactly as given even if it
    /// contains spaces or quotes.</summary>
    public string ToCommandLineArguments()
    {
        var parts = new List<string> { EditFlag, EscapeArgument(FilePath) };
        if (ReadOnly)
        {
            parts.Add(ReadOnlyFlag);
        }

        if (!ShowLayersPanel)
        {
            parts.Add(HideLayersPanelFlag);
        }

        if (AllowedElementKinds is { Count: > 0 })
        {
            parts.Add(ElementsFlag);
            parts.Add(EscapeArgument(string.Join(",", AllowedElementKinds)));
        }

        return string.Join(" ", parts);
    }

    /// <summary>Decodes <c>--edit &lt;path&gt; [--readonly] [--hide-layers-panel] [--elements &lt;names&gt;]</c>
    /// from an already-split argv (i.e. what the satellite process's own <c>Main(string[] args)</c>
    /// receives — no unescaping needed here, the runtime already did it).</summary>
    public static bool TryParse(string[] args, out LaunchRequest? request)
    {
        request = null;

        if (args.Length < 2 || !string.Equals(args[0], EditFlag, StringComparison.Ordinal))
        {
            return false;
        }

        var filePath = args[1];
        if (string.IsNullOrEmpty(filePath))
        {
            return false;
        }

        var readOnly = false;
        var showLayersPanel = true;
        IReadOnlyList<string>? allowedElementKinds = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (string.Equals(args[i], ReadOnlyFlag, StringComparison.Ordinal))
            {
                readOnly = true;
            }
            else if (string.Equals(args[i], HideLayersPanelFlag, StringComparison.Ordinal))
            {
                showLayersPanel = false;
            }
            else if (string.Equals(args[i], ElementsFlag, StringComparison.Ordinal) && i + 1 < args.Length)
            {
                allowedElementKinds = args[i + 1].Split(',');
                i++;
            }
        }

        request = new LaunchRequest { FilePath = filePath, ReadOnly = readOnly, AllowedElementKinds = allowedElementKinds, ShowLayersPanel = showLayersPanel };
        return true;
    }

    private static string EscapeArgument(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '"']) < 0)
        {
            return arg;
        }

        var builder = new System.Text.StringBuilder();
        builder.Append('"');

        for (var i = 0; i < arg.Length; i++)
        {
            var backslashCount = 0;
            while (i < arg.Length && arg[i] == '\\')
            {
                backslashCount++;
                i++;
            }

            if (i == arg.Length)
            {
                builder.Append('\\', backslashCount * 2);
                break;
            }

            if (arg[i] == '"')
            {
                builder.Append('\\', backslashCount * 2 + 1);
                builder.Append('"');
            }
            else
            {
                builder.Append('\\', backslashCount);
                builder.Append(arg[i]);
            }
        }

        builder.Append('"');
        return builder.ToString();
    }
}
