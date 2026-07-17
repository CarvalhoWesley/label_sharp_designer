using System.Windows.Forms;

namespace LabelSharpDesignerCore.App.Library;

/// <summary>Not persisted across launches — matches the original Flutter app, which always resets to
/// <see cref="System"/> on restart rather than remembering the user's last choice.</summary>
public enum AppThemeMode
{
    System,
    Light,
    Dark,
}

internal static class AppThemeModeExtensions
{
    public static AppThemeMode Next(this AppThemeMode mode) => mode switch
    {
        AppThemeMode.System => AppThemeMode.Light,
        AppThemeMode.Light => AppThemeMode.Dark,
        _ => AppThemeMode.System,
    };

#if NET9_0_OR_GREATER
    // SystemColorMode/Application.SetColorMode (dark-mode theming) is a .NET 9+ WinForms API with no
    // net48 equivalent — the net48 leg never calls this, so it doesn't need the conversion at all.
    public static SystemColorMode ToSystemColorMode(this AppThemeMode mode) => mode switch
    {
        AppThemeMode.Light => SystemColorMode.Classic,
        AppThemeMode.Dark => SystemColorMode.Dark,
        _ => SystemColorMode.System,
    };
#endif

    public static string Label(this AppThemeMode mode) => mode switch
    {
        AppThemeMode.Light => "Tema: Claro",
        AppThemeMode.Dark => "Tema: Escuro",
        _ => "Tema: Sistema",
    };
}
