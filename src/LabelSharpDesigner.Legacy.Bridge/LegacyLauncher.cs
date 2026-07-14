using System.Diagnostics;

namespace LabelSharpDesigner.Legacy.Bridge;

/// <summary>
/// Meant to be referenced directly by the legacy ASP.NET Framework project (netstandard2.0, so it's
/// referenceable from net462/net472) to start the satellite <c>LabelSharpDesigner.App.exe</c> over a
/// specific <c>.label</c> file and block until the user closes the editor.
/// </summary>
public sealed class LegacyLauncher
{
    private readonly string _appExecutablePath;

    /// <param name="appExecutablePath">Full path to <c>LabelSharpDesigner.App.exe</c> — deployment
    /// location is entirely the caller's concern, this class makes no assumption about it.</param>
    public LegacyLauncher(string appExecutablePath)
    {
        _appExecutablePath = appExecutablePath;
    }

    public LaunchResult Launch(LaunchRequest request)
    {
        var startInfo = new ProcessStartInfo(_appExecutablePath, request.ToCommandLineArguments())
        {
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{_appExecutablePath}'.");
        process.WaitForExit();

        return LaunchResult.FromExitCode(process.ExitCode);
    }
}
