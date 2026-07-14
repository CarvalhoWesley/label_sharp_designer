namespace LabelSharpDesigner.Legacy.Bridge;

/// <summary>The satellite process's exit code contract: 0 = saved, 1 = cancelled (closed without
/// saving, or a read-only session), 2 = error (e.g. the file couldn't be opened).</summary>
public enum LaunchOutcome
{
    Saved = 0,
    Cancelled = 1,
    Error = 2,
}
