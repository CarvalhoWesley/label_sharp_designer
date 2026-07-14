using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.Core.Tests;

public class ArgbColorTests
{
    [Theory]
    [InlineData("#FF0000", 255, 255, 0, 0)]
    [InlineData("00FF00", 255, 0, 255, 0)]
    [InlineData("#800000FF", 128, 0, 0, 255)]
    public void FromHex_ParsesRgbAndArgbForms(string hex, byte a, byte r, byte g, byte b)
    {
        var color = ArgbColor.FromHex(hex);

        Assert.Equal(a, color.A);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Fact]
    public void ToHex_RoundTripsThroughFromHex()
    {
        var original = new ArgbColor(200, 10, 20, 30);

        var roundTripped = ArgbColor.FromHex(original.ToHex());

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void FromHex_RejectsInvalidLength()
    {
        Assert.Throws<FormatException>(() => ArgbColor.FromHex("#ABC"));
    }
}
