namespace LabelSharpDesigner.Legacy.Bridge;

/// <summary>
/// What the legacy ASP.NET Framework app asks the satellite <c>LabelSharpDesigner.App</c> process to
/// do: open the editor over a specific <c>.label</c> file, optionally read-only. Encoded/decoded as
/// the command line <c>--edit &lt;path&gt; [--readonly]</c>, so <see cref="ToCommandLineArguments"/>
/// (legacy side, building the process to launch) and <see cref="TryParse"/> (satellite side, decoding
/// its own <c>Main(string[] args)</c>) must stay symmetric.
/// </summary>
public sealed record LaunchRequest
{
    public const string EditFlag = "--edit";
    public const string ReadOnlyFlag = "--readonly";

    public required string FilePath { get; init; }
    public bool ReadOnly { get; init; }

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

        return string.Join(" ", parts);
    }

    /// <summary>Decodes <c>--edit &lt;path&gt; [--readonly]</c> from an already-split argv (i.e. what
    /// the satellite process's own <c>Main(string[] args)</c> receives — no unescaping needed here,
    /// the runtime already did it).</summary>
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
        for (var i = 2; i < args.Length; i++)
        {
            if (string.Equals(args[i], ReadOnlyFlag, StringComparison.Ordinal))
            {
                readOnly = true;
            }
        }

        request = new LaunchRequest { FilePath = filePath, ReadOnly = readOnly };
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
