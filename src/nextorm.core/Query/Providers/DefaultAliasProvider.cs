using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;

namespace NextORM.Core;

    /// <summary>
    /// Default <see cref="IAliasProvider"/> that hands out deterministic aliases (<c>t1</c>, <c>t2</c>, ...)
    /// in the order sources are registered.
    /// </summary>
    public class DefaultAliasProvider : IAliasProvider
{
    /// <summary>Returns the alias of the <paramref name="oneBasedIndex"/>-th source ("t1", "t2", ...).</summary>
    internal static string GetAliasName(int oneBasedIndex) => _aliasNames.Get(oneBasedIndex);

    // Alias names are deterministic ("t1", "t2", ...); caching them removes a fresh string (and a
    // boxed-index concat) from every alias lookup, of which a joined query does several per build.
    private static readonly ParamNameCache _aliasNames = new("t");

#if NET8_0_OR_GREATER
    private ValueList<(string alias, object sourceProvider)> _valueList;
#else
    private readonly List<(string alias, object sourceProvider)> _valueList = [];
#endif
    /// <inheritdoc/>
    public string? FindAlias(int idx)
    {
#if DEBUG
        Debug.Assert(idx <= _valueList.Count);
#endif
        return _aliasNames.Get(idx + 1);
    }
    /// <inheritdoc/>
    public string GetNextAlias(FromExpression from)
    {
        var idx = _valueList.Count;
        idx++;
        var alias = _aliasNames.Get(idx);
        _valueList.Add((alias, from));
        return alias;
    }

    /// <inheritdoc/>
    public string GetNextAlias(QueryCommand queryCommand)
    {
        var idx = _valueList.Count;
        idx++;
        var alias = _aliasNames.Get(idx);
        _valueList.Add((alias, queryCommand));
        return alias;
    }
}