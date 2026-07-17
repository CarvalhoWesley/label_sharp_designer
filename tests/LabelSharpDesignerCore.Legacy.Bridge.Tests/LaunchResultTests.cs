namespace LabelSharpDesignerCore.Legacy.Bridge.Tests;

public class LaunchResultTests
{
    [Theory]
    [InlineData(0, LaunchOutcome.Saved)]
    [InlineData(1, LaunchOutcome.Cancelled)]
    [InlineData(2, LaunchOutcome.Error)]
    public void FromExitCode_MapsTheDocumentedContract(int exitCode, LaunchOutcome expected)
    {
        var result = LaunchResult.FromExitCode(exitCode);

        Assert.Equal(expected, result.Outcome);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    [InlineData(255)]
    public void FromExitCode_TreatsAnyUndocumentedCodeAsError(int exitCode)
    {
        var result = LaunchResult.FromExitCode(exitCode);

        Assert.Equal(LaunchOutcome.Error, result.Outcome);
    }
}
