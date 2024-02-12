using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;

namespace nextorm.core;

public class DefaultAliasProvider : IAliasProvider
{
#if NET8_0_OR_GREATER
    private ValueList<(string alias, object sourceProvider)> _valueList;
#else
    private readonly List<(string alias, object sourceProvider)> _valueList = [];
#endif
    public string? FindAlias(int idx)
    {
#if DEBUG
        Debug.Assert(idx <= _valueList.Count);
#endif
        return "t" + (idx + 1);
    }
    public string GetNextAlias(FromExpression from)
    {
        var idx = _valueList.Count;
        idx++;
        var alias = "t" + idx;
        _valueList.Add((alias, from));
        return alias;
    }

    public string GetNextAlias(QueryCommand queryCommand)
    {
        var idx = _valueList.Count;
        idx++;
        var alias = "t" + idx;
        _valueList.Add((alias, queryCommand));
        return alias;
    }
}