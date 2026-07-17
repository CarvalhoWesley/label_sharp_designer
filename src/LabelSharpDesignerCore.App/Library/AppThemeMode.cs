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

    public static SystemColorMode ToSystemColorMode(this AppThemeMode mode) => mode switch
    {
        AppThemeMode.Light => SystemColorMode.Classic,
        AppThemeMode.Dark => SystemColorMode.Dark,
        _ => SystemColorMode.System,
    };

    public static string Label(this AppThemeMode mode) => mode switch
    {
        AppThemeMode.Light => "Tema: Claro",
        AppThemeMode.Dark => "Tema: Escuro",
        _ => "Tema: Sistema",
    };
}
