namespace LabelSharpDesignerCore.LegacySampleApp.Labels;

/// <summary>Resolves the path to the published <c>LabelSharpDesignerCore.App.exe</c> satellite that
/// <c>LegacyLauncher</c> needs (INTEGRATION.md §3.1/§3.4). A real deployment configures this once
/// (<see cref="EditorLauncherSettingsStore"/>, via the Settings screen) and never needs auto-detection
/// again; <see cref="TryAutoDetect"/> exists purely as a dev-time convenience so this sample works
/// out of the box when run from inside this repository, next to an already-built
/// <c>LabelSharpDesignerCore.App</c>.</summary>
internal static class EditorLauncherLocator
{
    private const string ExeFileName = "LabelSharpDesignerCore.App.exe";

    /// <summary>The persisted path if it's configured and still points at a real file; otherwise a
    /// dev-time auto-detected one; otherwise <see langword="null"/> (caller must ask the user).</summary>
    public static string? Resolve()
    {
        var configured = EditorLauncherSettingsStore.Load().AppExecutablePath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        return TryAutoDetect();
    }

    /// <summary>Walks up from this app's own build output looking for a sibling
    /// <c>src\LabelSharpDesignerCore.App\bin\{Release,Debug}\net9.0-windows*\LabelSharpDesignerCore.App.exe</c>
    /// — only ever finds anything when running from inside this monorepo's own checkout.</summary>
    public static string? TryAutoDetect()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (var i = 0; i < 8 && directory is not null; i++, directory = directory.Parent)
        {
            var binRoot = Path.Combine(directory.FullName, "src", "LabelSharpDesignerCore.App", "bin");
            if (!Directory.Exists(binRoot))
            {
                continue;
            }

            foreach (var configurationName in new[] { "Release", "Debug" })
            {
                var configurationPath = Path.Combine(binRoot, configurationName);
                if (!Directory.Exists(configurationPath))
                {
                    continue;
                }

                var exePath = Directory.EnumerateDirectories(configurationPath, "net9.0-windows*")
                    .Select(tfmDirectory => Path.Combine(tfmDirectory, ExeFileName))
                    .FirstOrDefault(File.Exists);

                if (exePath is not null)
                {
                    return exePath;
                }
            }
        }

        return null;
    }
}
