using LabelSharpDesignerCore.App.Library;
using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Legacy.Bridge;
using LabelSharpDesignerCore.Serialization;

namespace LabelSharpDesignerCore.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (LaunchRequest.TryParse(args, out var request))
        {
            Environment.ExitCode = (int)RunEditMode(request!);
            return;
        }

        RunLibraryMode();
    }

    /// <summary>
    /// The satellite entry point the legacy ASP.NET Framework app launches via
    /// <see cref="LegacyLauncher"/>: opens the editor directly over one file — bypassing the library
    /// entirely, since this file isn't necessarily managed by <see cref="LibraryRepository"/> — and
    /// resolves to the process exit code contract documented on <see cref="LaunchOutcome"/>.
    /// </summary>
    private static LaunchOutcome RunEditMode(LaunchRequest request)
    {
        InitializeApplication();

        LabelDocument document;
        try
        {
            document = LabelDocumentCodec.Load(File.ReadAllText(request.FilePath));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Falha ao abrir \"{request.FilePath}\":\n{ex.Message}",
                "LabelSharpDesignerCore",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return LaunchOutcome.Error;
        }

        var saved = false;
        Action<LabelDocument>? onSave = request.ReadOnly
            ? null
            : doc =>
            {
                File.WriteAllText(request.FilePath, LabelDocumentCodec.Save(doc));
                saved = true;
            };

        var allowedElementKinds = ParseAllowedElementKinds(request.AllowedElementKinds);

        using var editor = new EditorForm(document, onSave, allowedElementKinds, request.ShowLayersPanel);
        Application.Run(editor);

        return saved ? LaunchOutcome.Saved : LaunchOutcome.Cancelled;
    }

    /// <summary>Maps <see cref="LaunchRequest.AllowedElementKinds"/>'s plain names (the only contract
    /// <c>Legacy.Bridge</c> can share with a legacy caller, since it can't reference this assembly's
    /// own <see cref="NewElementKind"/>) back to real enum values. A name the caller sends that this
    /// version of the app doesn't recognize is silently dropped — never worth failing the whole editor
    /// launch over one stale/misspelled entry.</summary>
    private static IReadOnlyCollection<NewElementKind>? ParseAllowedElementKinds(IReadOnlyList<string>? names)
    {
        if (names is not { Count: > 0 })
        {
            return null;
        }

        var kinds = names
            .Select(name => Enum.TryParse<NewElementKind>(name, ignoreCase: true, out var kind) ? kind : (NewElementKind?)null)
            .Where(kind => kind is not null)
            .Select(kind => kind!.Value)
            .ToArray();

        return kinds.Length > 0 ? kinds : null;
    }

    private static void RunLibraryMode()
    {
        var repository = LibraryRepository.Open();
        var themeMode = AppThemeMode.System;
        InitializeApplication();
        ApplyThemeMode(themeMode);

        // A theme switch can't be repainted live onto controls already on screen (verified: the title
        // bar and owner-drawn controls stay stuck in their original mode), so switching themes means
        // closing the library and opening a brand new one under the new SystemColorMode rather than
        // restyling the existing form in place.
        while (true)
        {
            var library = new LibraryForm(repository, themeMode);
            Application.Run(library);

            if (library.RequestedThemeMode is not { } requested)
            {
                break;
            }

            themeMode = requested;
            ApplyThemeMode(themeMode);
        }
    }

    /// <summary>WinForms bootstrap — visual styles, text rendering, high-DPI mode. .NET 9 gets this
    /// for free from the SDK-generated <c>ApplicationConfiguration.Initialize()</c>; net48 has no such
    /// generator, so it's done by hand with the pre-.NET-6 API.</summary>
    private static void InitializeApplication()
    {
#if NET9_0_OR_GREATER
        ApplicationConfiguration.Initialize();
#else
        // High-DPI mode on net48 comes from app.config's <System.Windows.Forms.ApplicationConfigurationSection>
        // (DpiAwareness=PerMonitorV2) instead of the SDK-generated call net9 gets — see App.config.
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
#endif
    }

    /// <summary>System dark-mode theming (<c>Application.SetColorMode</c>) is a .NET 9+ WinForms API
    /// with no net48 equivalent — net48 always renders in the OS's classic light theme regardless of
    /// <paramref name="mode"/>.</summary>
    private static void ApplyThemeMode(AppThemeMode mode)
    {
#if NET9_0_OR_GREATER
        Application.SetColorMode(mode.ToSystemColorMode());
#endif
    }
}
