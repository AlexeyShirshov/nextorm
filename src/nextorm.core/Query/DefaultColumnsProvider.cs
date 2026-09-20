
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace NextORM.Core;

public class DefaultColumnsProvider : IColumnsProvider
{
#if NET8_0_OR_GREATER
    private ValueList<(Type, QueryCommand?, bool)> _list;
    private ValueList3<ReadOnlyCollection<ParameterExpression>> _scope;
    private ValueList<int> _sourceScopes;

#else
    private readonly List<(Type, QueryCommand?, bool)> _list = [];
    private readonly List<ReadOnlyCollection<ParameterExpression>> _scope = [];
    private readonly List<int> _sourceScopes = [];
#endif
    /// <summary>The index of the first entry owned by the command currently being rendered.</summary>
    private int SourceScopeStart => _sourceScopes.Count > 0 ? _sourceScopes.Peek() : 0;

    public void PushSourceScope() => _sourceScopes.Add(_list.Count);

    public void PopSourceScope() => _sourceScopes.Pop();

    public void Add(Type entityType, bool fromProjection)
    {
        _list.Add((entityType, null, fromProjection));
    }

    public void Add(QueryCommand queryCommand, bool fromProjection)
    {
        _list.Add((queryCommand.ResultType!, queryCommand, fromProjection));
    }

    public int? FindAlias(ParameterExpression param, bool fromProjection)
        => FindAlias(param, fromProjection, includeOuterScopes: false);

    public int? FindAlias(ParameterExpression param, bool fromProjection, bool includeOuterScopes)
    {
        var entityType = param.Type;
        var foundIdx = -1;
        ReadOnlyCollection<ParameterExpression>? paramColl = null;
        var paramIdx = -1;
        var start = includeOuterScopes ? 0 : SourceScopeStart;

        for (var (i, cnt) = (start, _list.Count); i < cnt; i++)
        {
            var item = _list[i];
            if (item.Item1 == entityType && item.Item3 == fromProjection)
            {
                if (_scope.Count > 0)
                {
                    if (paramColl is null)
                    {
                        paramColl = _scope.Peek();
                        paramIdx = paramColl.IndexOf(param);
                    }

                    foundIdx++;
                    if (foundIdx != paramIdx)
                        continue;
                }
                return i;
            }
        }

        return null;
    }

    public (int, QueryCommand?) FindQueryCommand(Type entityType)
    {
        for (var (i, cnt) = (0, _list.Count); i < cnt; i++)
        {
            var item = _list[i];
            if (item.Item1 == entityType)
                return (i, item.Item2);
        }

        return (default, default);
    }

    public void PopScope()
    {
        _scope.Pop();
    }

    public void PushScope(ReadOnlyCollection<ParameterExpression> parameters)
    {
        _scope.Add(parameters);
    }

    public int? FindAlias(Type entityType, int? paramIdx, bool fromProjection)
    {
        var foundIdx = -1;
        for (var (i, cnt) = (SourceScopeStart, _list.Count); i < cnt; i++)
        {
            var item = _list[i];
            if (item.Item1 == entityType && item.Item3 == fromProjection)
            {
                if (paramIdx.HasValue)
                {
                    foundIdx++;
                    if (foundIdx != paramIdx)
                        continue;
                }
                return i;
            }
        }

        return null;
    }

    public bool HasAliases => _list.Count > 0;
}