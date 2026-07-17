namespace LabelSharpDesignerCore.App.Compat;

/// <summary>Generic clamp — <see cref="Math.Clamp"/> only exists from .NET Core 2.0 on, and this
/// assembly also targets net48. Used unconditionally on both TFMs so callers don't need to care which
/// one is active.</summary>
internal static class MathCompat
{
    public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        => value.CompareTo(min) < 0 ? min : value.CompareTo(max) > 0 ? max : value;
}
