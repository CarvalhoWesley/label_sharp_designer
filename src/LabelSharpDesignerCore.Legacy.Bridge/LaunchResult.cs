namespace LabelSharpDesignerCore.Legacy.Bridge;

/// <summary>What came back from a satellite process launch — the legacy app's own read of the exit
/// code contract, so callers never have to hardcode the 0/1/2 mapping themselves.</summary>
public sealed record LaunchResult
{
    public required LaunchOutcome Outcome { get; init; }

    /// <summary>Any exit code outside 0-2 is treated as <see cref="LaunchOutcome.Error"/> — the
    /// satellite process crashing or being killed still has to resolve to a definite outcome.</summary>
    public static LaunchResult FromExitCode(int exitCode) => new()
    {
        Outcome = exitCode switch
        {
            (int)LaunchOutcome.Saved => LaunchOutcome.Saved,
            (int)LaunchOutcome.Cancelled => LaunchOutcome.Cancelled,
            _ => LaunchOutcome.Error,
        },
    };
}
