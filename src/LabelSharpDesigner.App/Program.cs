using LabelSharpDesigner.App.Library;
using LabelSharpDesigner.Core.Document;
using LabelSharpDesigner.Legacy.Bridge;
using LabelSharpDesigner.Serialization;

namespace LabelSharpDesigner.App;

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
        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.System);

        LabelDocument document;
        try
        {
            document = LabelDocumentCodec.Load(File.ReadAllText(request.FilePath));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Falha ao abrir \"{request.FilePath}\":\n{ex.Message}",
                "LabelSharpDesigner",
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

        using var editor = new EditorForm(document, onSave);
        Application.Run(editor);

        return saved ? LaunchOutcome.Saved : LaunchOutcome.Cancelled;
    }

    private static void RunLibraryMode()
    {
        var repository = LibraryRepository.Open();
        var themeMode = AppThemeMode.System;
        ApplicationConfiguration.Initialize();
        Application.SetColorMode(themeMode.ToSystemColorMode());

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
            Application.SetColorMode(themeMode.ToSystemColorMode());
        }
    }
}
