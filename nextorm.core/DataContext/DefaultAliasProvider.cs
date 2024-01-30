using System.Collections;
using System.Linq.Expressions;

namespace nextorm.core;

public class DefaultAliasProvider : IAliasProvider
{
#if NET8_0_OR_GREATER
    private ValueList<(string alias, object sourceProvider)> _valueArrayList;
#else
    private readonly ArrayList _valueArrayList = [];
#endif
    public string? FindAlias(ISourceProvider sourceProvider)
    {
        for (var (i, cnt) = (0, _valueArrayList.Length); i < cnt; i++)
        {
            var item = _valueArrayList.Get(i);
            if (item.sourceProvider == sourceProvider)
                return item.alias;
        }
        return null;
    }
    public string GetNextAlias(FromExpression from)
    {
        var idx = _valueArrayList.Length;
        idx++;
        var alias = "t" + idx;
        _valueArrayList.Add((alias, from));
        return alias;
    }

    public string GetNextAlias(QueryCommand queryCommand)
    {
        var idx = _valueArrayList.Length;
        idx++;
        var alias = "t" + idx;
        _valueArrayList.Add((alias, queryCommand));
        return alias;
    }
}