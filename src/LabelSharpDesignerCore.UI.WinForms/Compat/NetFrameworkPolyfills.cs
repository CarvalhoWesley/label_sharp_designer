namespace LabelSharpDesignerCore.UI.WinForms.Compat;

/// <summary>Generic clamp — <see cref="Math.Clamp"/> only exists from .NET Core 2.0 on, and this
/// assembly also targets net48. Used unconditionally on both TFMs so callers don't need to care which
/// one is active.</summary>
internal static class MathCompat
{
    public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        => value.CompareTo(min) < 0 ? min : value.CompareTo(max) > 0 ? max : value;
}

/// <summary>Polyfills <c>KeyValuePair&lt;TKey,TValue&gt;.Deconstruct</c> for net48, which doesn't ship
/// it (added to corelib in .NET Core). An extension method is only picked up by the compiler when no
/// instance member matches, so this is a no-op on TFMs where the BCL already provides it.</summary>
internal static class KeyValuePairCompat
{
    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }
}
