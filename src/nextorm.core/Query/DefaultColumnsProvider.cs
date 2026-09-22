
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Default <see cref="IColumnsProvider"/> that tracks the column sources of a query in registration
/// order and resolves lambda parameters to alias indexes within the current scope.
/// </summary>
public class DefaultColumnsProvider : IColumnsProvider
{
#if NET8_0_OR_GREATER
    private ValueList<(Type, QueryCommand?, bool, bool)> _list;
    private ValueList3<ReadOnlyCollection<ParameterExpression>> _scope;
    private ValueList<int> _sourceScopes;

#else
    private readonly List<(Type, QueryCommand?, bool, bool)> _list = [];
    private readonly List<ReadOnlyCollection<ParameterExpression>> _scope = [];
    private readonly List<int> _sourceScopes = [];
#endif
    /// <summary>The index of the first entry owned by the command currently being rendered.</summary>
    private int SourceScopeStart => _sourceScopes.Count > 0 ? _sourceScopes.Peek() : 0;

    /// <inheritdoc/>
    public void PushSourceScope() => _sourceScopes.Add(_list.Count);

    /// <inheritdoc/>
    public void PopSourceScope()
    {
        // A nested command's entries keep their index (the alias provider numbers sources by the
        // global assignment order), but must stop satisfying the enclosing command's lookups: the
        // enclosing scope's boundary was recorded before the nested command appended them, so
        // without this a same-typed outer source would resolve to the inner alias ("t2" for an
        // out-of-scope derived source).
        var start = _sourceScopes.Peek();
        for (var (i, cnt) = (start, _list.Count); i < cnt; i++)
        {
            var item = _list[i];
            if (!item.Item4)
                _list[i] = (item.Item1, item.Item2, item.Item3, true);
        }
        _sourceScopes.Pop();
    }

    /// <inheritdoc/>
    public void Add(Type entityType, bool fromProjection)
    {
        _list.Add((entityType, null, fromProjection, false));
    }

    /// <inheritdoc/>
    public void Add(QueryCommand queryCommand, bool fromProjection)
    {
        _list.Add((queryCommand.ResultType!, queryCommand, fromProjection, false));
    }

    /// <inheritdoc/>
    public int? FindAlias(ParameterExpression param, bool fromProjection)
        => FindAlias(param, fromProjection, includeOuterScopes: false, includeNestedSources: false);

    /// <inheritdoc/>
    public int? FindAlias(ParameterExpression param, bool fromProjection, bool includeOuterScopes)
        => FindAlias(param, fromProjection, includeOuterScopes, includeNestedSources: false);

    /// <inheritdoc/>
    public int? FindAlias(ParameterExpression param, bool fromProjection, bool includeOuterScopes, bool includeNestedSources)
    {
        if (includeNestedSources)
        {
            var nested = FindAliasInScope(param, fromProjection, includeOuterScopes, onlyNested: true);
            if (nested.HasValue)
                return nested;
        }

        return FindAliasInScope(param, fromProjection, includeOuterScopes, onlyNested: false);
    }

    private int? FindAliasInScope(ParameterExpression param, bool fromProjection, bool includeOuterScopes, bool onlyNested)
    {
        var entityType = param.Type;
        var foundIdx = -1;
        ReadOnlyCollection<ParameterExpression>? paramColl = null;
        var paramIdx = -1;
        var start = includeOuterScopes || onlyNested ? 0 : SourceScopeStart;

        for (var (i, cnt) = (start, _list.Count); i < cnt; i++)
        {
            var item = _list[i];
            if (item.Item4 != onlyNested) continue;
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

    /// <inheritdoc/>
    public (int, QueryCommand?) FindQueryCommand(Type entityType)
        => FindQueryCommand(entityType, includeNestedSources: false);

    /// <inheritdoc/>
    public (int, QueryCommand?) FindQueryCommand(Type entityType, bool includeNestedSources)
    {
        if (includeNestedSources)
        {
            var nested = FindQueryCommandInScope(entityType, onlyNested: true);
            if (nested.Item2 is not null)
                return nested;
        }

        return FindQueryCommandInScope(entityType, onlyNested: false);
    }

    private (int, QueryCommand?) FindQueryCommandInScope(Type entityType, bool onlyNested)
    {
        // Prefer an in-scope source, but fall back to the most recently added out-of-scope one.
        // Rendering a derived query's pass-through column re-visits the expression against the source
        // that derived query was built from (for example the middle query over the inner derived
        // query's columns), and that source's scope has already been popped by the time the enclosing
        // query renders its projection. The entry is inactive for the enclosing command's own alias
        // lookups, yet it is the only command that can resolve the nested member.
        (int Index, QueryCommand? Command) fallback = default;

        for (var (i, cnt) = (0, _list.Count); i < cnt; i++)
        {
            var item = _list[i];
            if (item.Item1 != entityType)
                continue;

            if (!item.Item4)
                return (i, item.Item2);

            fallback = (i, item.Item2);
        }

        return fallback;
    }

    /// <inheritdoc/>
    public void PopScope()
    {
        _scope.Pop();
    }

    /// <inheritdoc/>
    public void PushScope(ReadOnlyCollection<ParameterExpression> parameters)
    {
        _scope.Add(parameters);
    }

    /// <inheritdoc/>
    public int? FindAlias(Type entityType, int? paramIdx, bool fromProjection)
        => FindAlias(entityType, paramIdx, fromProjection, includeNestedSources: false);

    /// <inheritdoc/>
    public int? FindAlias(Type entityType, int? paramIdx, bool fromProjection, bool includeNestedSources)
    {
        if (includeNestedSources)
        {
            var nested = FindAliasInScope(entityType, paramIdx, fromProjection, onlyNested: true);
            if (nested.HasValue)
                return nested;
        }

        return FindAliasInScope(entityType, paramIdx, fromProjection, onlyNested: false);
    }

    private int? FindAliasInScope(Type entityType, int? paramIdx, bool fromProjection, bool onlyNested)
    {
        var foundIdx = -1;
        var start = onlyNested ? 0 : SourceScopeStart;
        for (var (i, cnt) = (start, _list.Count); i < cnt; i++)
        {
            var item = _list[i];
            if (item.Item4 != onlyNested) continue;
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

    /// <inheritdoc/>
    public bool HasAliases => _list.Count > 0;
}