using System.Globalization;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Indexed cache of parameter names of the form <c>{prefix}{index}</c>. Avoids
/// <c>string.Format("{0}", ...)</c>, which boxes the index and takes the composite-formatting
/// path on every call. <see cref="Get"/> is inlined so the common (<c>index &lt; length</c>) case
/// costs a bounds check and an array read.
/// </summary>
internal sealed class ParamNameCache(string prefix)
{
    private readonly string _prefix = prefix;
    private string[] _names = [prefix + "0", prefix + "1", prefix + "2", prefix + "3", prefix + "4"];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Get(int index)
    {
        var names = _names;
        if ((uint)index < (uint)names.Length) return names[index];

        return Grow(index);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string Grow(int index)
    {
        var names = _names;
        if ((uint)index < (uint)names.Length) return names[index];

        // Racy growth is fine: every thread writes an equivalent array, so the worst case is
        // duplicated work rather than a torn read.
        var grown = new string[index + 1];
        Array.Copy(names, grown, names.Length);
        for (var i = names.Length; i < grown.Length; i++)
            grown[i] = string.Concat(_prefix, i.ToString(CultureInfo.InvariantCulture));
        _names = grown;
        return grown[index];
    }
}
