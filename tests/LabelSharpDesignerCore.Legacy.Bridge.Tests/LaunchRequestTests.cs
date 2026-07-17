namespace LabelSharpDesignerCore.Legacy.Bridge.Tests;

public class LaunchRequestTests
{
    [Fact]
    public void TryParse_AcceptsEditWithPathOnly()
    {
        var ok = LaunchRequest.TryParse(["--edit", @"C:\labels\a.label"], out var request);

        Assert.True(ok);
        Assert.Equal(@"C:\labels\a.label", request!.FilePath);
        Assert.False(request.ReadOnly);
    }

    [Fact]
    public void TryParse_AcceptsTrailingReadOnlyFlag()
    {
        var ok = LaunchRequest.TryParse(["--edit", @"C:\labels\a.label", "--readonly"], out var request);

        Assert.True(ok);
        Assert.True(request!.ReadOnly);
    }

    [Theory]
    [MemberData(nameof(InvalidArgSets))]
    public void TryParse_RejectsAnythingElse(string[] args)
    {
        var ok = LaunchRequest.TryParse(args, out var request);

        Assert.False(ok);
        Assert.Null(request);
    }

    public static IEnumerable<object[]> InvalidArgSets()
    {
        yield return [Array.Empty<string>()];
        yield return [new[] { "--edit" }];
        yield return [new[] { "--edit", "" }];
        yield return [new[] { "--open", @"C:\labels\a.label" }];
        yield return [new[] { "--readonly", "--edit", @"C:\labels\a.label" }];
    }

    [Fact]
    public void ToCommandLineArguments_LeavesASimplePathUnquoted()
    {
        var request = new LaunchRequest { FilePath = @"C:\labels\a.label" };

        Assert.Equal(@"--edit C:\labels\a.label", request.ToCommandLineArguments());
    }

    [Fact]
    public void ToCommandLineArguments_QuotesAPathContainingSpaces()
    {
        var request = new LaunchRequest { FilePath = @"C:\Program Files\LabelSharpDesignerCore\a.label" };

        Assert.Equal("--edit \"C:\\Program Files\\LabelSharpDesignerCore\\a.label\"", request.ToCommandLineArguments());
    }

    [Fact]
    public void ToCommandLineArguments_DoublesATrailingBackslashBeforeTheClosingQuote()
    {
        // A single trailing backslash right before the closing quote would otherwise escape it —
        // the classic CommandLineToArgvW gotcha — so it must come out doubled.
        var request = new LaunchRequest { FilePath = @"C:\Program Files\" };

        Assert.Equal("--edit \"C:\\Program Files\\\\\"", request.ToCommandLineArguments());
    }

    [Fact]
    public void ToCommandLineArguments_EscapesEmbeddedQuotes()
    {
        var request = new LaunchRequest { FilePath = "C:\\labels\\a\"b.label" };

        Assert.Equal("--edit \"C:\\labels\\a\\\"b.label\"", request.ToCommandLineArguments());
    }

    [Fact]
    public void ToCommandLineArguments_AppendsReadOnlyFlagLast()
    {
        var request = new LaunchRequest { FilePath = @"C:\labels\a.label", ReadOnly = true };

        Assert.Equal(@"--edit C:\labels\a.label --readonly", request.ToCommandLineArguments());
    }

    [Fact]
    public void ToCommandLineArguments_OmitsHideLayersPanelFlagByDefault()
    {
        var request = new LaunchRequest { FilePath = @"C:\labels\a.label" };

        Assert.DoesNotContain("--hide-layers-panel", request.ToCommandLineArguments());
    }

    [Fact]
    public void ToCommandLineArguments_AppendsHideLayersPanelFlagWhenDisabled()
    {
        var request = new LaunchRequest { FilePath = @"C:\labels\a.label", ShowLayersPanel = false };

        Assert.Equal(@"--edit C:\labels\a.label --hide-layers-panel", request.ToCommandLineArguments());
    }

    [Fact]
    public void ToCommandLineArguments_AppendsHideLayersPanelBetweenReadOnlyAndElements()
    {
        var request = new LaunchRequest { FilePath = @"C:\labels\a.label", ReadOnly = true, ShowLayersPanel = false, AllowedElementKinds = ["Text"] };

        Assert.Equal(@"--edit C:\labels\a.label --readonly --hide-layers-panel --elements Text", request.ToCommandLineArguments());
    }

    [Fact]
    public void TryParse_DecodesHideLayersPanelFlag()
    {
        var ok = LaunchRequest.TryParse(["--edit", @"C:\labels\a.label", "--hide-layers-panel"], out var request);

        Assert.True(ok);
        Assert.False(request!.ShowLayersPanel);
    }

    [Fact]
    public void TryParse_DefaultsShowLayersPanelToTrueWhenFlagAbsent()
    {
        var ok = LaunchRequest.TryParse(["--edit", @"C:\labels\a.label"], out var request);

        Assert.True(ok);
        Assert.True(request!.ShowLayersPanel);
    }

    [Fact]
    public void EncodeThenDecode_RoundTripsShowLayersPanel()
    {
        var original = new LaunchRequest { FilePath = @"C:\labels\a.label", ShowLayersPanel = false };

        var ok = LaunchRequest.TryParse(original.ToCommandLineArguments().Split(' '), out var decoded);

        Assert.True(ok);
        Assert.Equal(original.ShowLayersPanel, decoded!.ShowLayersPanel);
    }

    [Fact]
    public void ToCommandLineArguments_OmitsElementsFlagWhenNotSet()
    {
        var request = new LaunchRequest { FilePath = @"C:\labels\a.label" };

        Assert.DoesNotContain("--elements", request.ToCommandLineArguments());
    }

    [Fact]
    public void ToCommandLineArguments_OmitsElementsFlagWhenEmpty()
    {
        var request = new LaunchRequest { FilePath = @"C:\labels\a.label", AllowedElementKinds = [] };

        Assert.DoesNotContain("--elements", request.ToCommandLineArguments());
    }

    [Fact]
    public void ToCommandLineArguments_AppendsElementsAsCommaSeparatedList()
    {
        var request = new LaunchRequest { FilePath = @"C:\labels\a.label", AllowedElementKinds = ["Text", "Barcode", "QrCode"] };

        Assert.Equal(@"--edit C:\labels\a.label --elements Text,Barcode,QrCode", request.ToCommandLineArguments());
    }

    [Fact]
    public void ToCommandLineArguments_AppendsElementsAfterReadOnly()
    {
        var request = new LaunchRequest { FilePath = @"C:\labels\a.label", ReadOnly = true, AllowedElementKinds = ["Text"] };

        Assert.Equal(@"--edit C:\labels\a.label --readonly --elements Text", request.ToCommandLineArguments());
    }

    [Fact]
    public void TryParse_DecodesElementsFlag()
    {
        var ok = LaunchRequest.TryParse(["--edit", @"C:\labels\a.label", "--elements", "Text,Barcode,QrCode"], out var request);

        Assert.True(ok);
        Assert.Equal(new[] { "Text", "Barcode", "QrCode" }, request!.AllowedElementKinds);
    }

    [Fact]
    public void TryParse_LeavesElementsNullWhenFlagAbsent()
    {
        var ok = LaunchRequest.TryParse(["--edit", @"C:\labels\a.label"], out var request);

        Assert.True(ok);
        Assert.Null(request!.AllowedElementKinds);
    }

    [Fact]
    public void EncodeThenDecode_RoundTripsAllowedElementKinds()
    {
        var original = new LaunchRequest { FilePath = @"C:\labels\a.label", AllowedElementKinds = ["Text", "Barcode", "QrCode"] };

        var ok = LaunchRequest.TryParse(original.ToCommandLineArguments().Split(' '), out var decoded);

        Assert.True(ok);
        Assert.Equal(original.AllowedElementKinds, decoded!.AllowedElementKinds);
    }

    [Fact]
    public void EncodeThenDecode_RoundTripsAPathWithSpaces()
    {
        // Simulates what actually happens end to end: ToCommandLineArguments() feeds
        // ProcessStartInfo.Arguments, the OS/CRT re-splits it using the same CommandLineToArgvW rules
        // this class's escaping targets, and the satellite process's own Main(string[] args) — decoded
        // here via TryParse — must see the original path back unchanged.
        var original = new LaunchRequest { FilePath = @"C:\Program Files\LabelSharpDesignerCore\a b.label", ReadOnly = true };

        var argv = SplitCommandLineArguments(original.ToCommandLineArguments());
        var ok = LaunchRequest.TryParse(argv, out var decoded);

        Assert.True(ok);
        Assert.Equal(original.FilePath, decoded!.FilePath);
        Assert.Equal(original.ReadOnly, decoded.ReadOnly);
    }

    /// <summary>Re-implements the Windows CommandLineToArgvW splitting rules (the same ones
    /// <c>Main(string[] args)</c> is handed pre-split by the runtime) so the round-trip test above can
    /// verify <see cref="LaunchRequest.ToCommandLineArguments"/> against the real target grammar without
    /// actually spawning a process.</summary>
    private static string[] SplitCommandLineArguments(string commandLine)
    {
        var args = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < commandLine.Length)
        {
            if (char.IsWhiteSpace(commandLine[i]) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }

                i++;
                continue;
            }

            if (commandLine[i] == '\\')
            {
                var backslashCount = 0;
                while (i < commandLine.Length && commandLine[i] == '\\')
                {
                    backslashCount++;
                    i++;
                }

                if (i < commandLine.Length && commandLine[i] == '"')
                {
                    current.Append('\\', backslashCount / 2);
                    if (backslashCount % 2 == 1)
                    {
                        current.Append('"');
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    i++;
                }
                else
                {
                    current.Append('\\', backslashCount);
                }

                continue;
            }

            if (commandLine[i] == '"')
            {
                inQuotes = !inQuotes;
                i++;
                continue;
            }

            current.Append(commandLine[i]);
            i++;
        }

        if (current.Length > 0)
        {
            args.Add(current.ToString());
        }

        return args.ToArray();
    }
}
