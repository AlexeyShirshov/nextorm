using System.Globalization;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public interface IParamProvider
{
    string GetParamName();
}
public class DefaultParamProvider : IParamProvider
{
    // p0..pN: grown on demand instead of string.Format("p{0}", i), which boxes the index and
    // goes through the composite-formatting path on every generated parameter name.
    private static string[] _paramNames = ["p0", "p1", "p2", "p3", "p4"];
    private int _paramIdx;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetParamName()
    {
        var index = _paramIdx++;
        var names = _paramNames;
        if ((uint)index < (uint)names.Length)
            return names[index];

        return GrowParamNames(index);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GrowParamNames(int index)
    {
        // Racy growth is fine: every thread writes an equivalent array.
        var names = _paramNames;
        if ((uint)index < (uint)names.Length)
            return names[index];

        var grown = new string[index + 1];
        Array.Copy(names, grown, names.Length);
        for (var i = names.Length; i < grown.Length; i++)
            grown[i] = string.Concat("p", i.ToString(CultureInfo.InvariantCulture));
        _paramNames = grown;
        return grown[index];
    }
}