
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Default <see cref="IColumnsProvider"/> that tracks the column sources of a query in registration
/// order and resolves lambda parameters to alias indexes within the current scope.
/// </summary>
public class DefaultColumnsProvider : IColumnsProvider, IOuterSlotSourceRegistry
{
#if NET8_0_OR_GREATER
    private ValueList<(Type, QueryCommand?, bool, bool, ParameterExpression?)> _list;
    private ValueList3<ReadOnlyCollection<ParameterExpression>> _scope;
    private ValueList<int> _sourceScopes;

#else
    private readonly List<(Type, QueryCommand?, bool, bool, ParameterExpression?)> _list = [];
    private readonly List<ReadOnlyCollection<ParameterExpression>> _scope = [];
    private readonly List<int> _sourceScopes = [];
#endif

    /// <summary>
    /// Source entries bound to an explicit occurrence parameter (#148-B R2.2), keyed by reference
    /// identity. A navigation-injected join registers its right-hand parameter here so
    /// <see cref="FindAlias(ParameterExpression, bool)"/> binds by occurrence identity rather than by
    /// CLR type, which keeps two same-typed joined sources distinct.
    /// </summary>
    private readonly Dictionary<ParameterExpression, int> _paramSources = new(ReferenceEqualityComparer.Instance);
    /// <summary>
    /// Frame-local correlated-APPLY left-row bindings (#324): one entry per source scope, created
    /// lazily, holding <c>(owner parameter occurrence, one-based slot) -&gt; registration index</c>.
    /// Popped together with its source scope, so a frame only addresses the sources it registered.
    /// </summary>
    private readonly List<Dictionary<(ParameterExpression Owner, int Slot), int>?> _outerSlotFrames = [];
    /// <summary>
    /// The projection type of the command each source scope belongs to (#324), parallel to
    /// <see cref="_sourceScopes"/>. It lets a SELECT projection item resolve through the frame-local slot
    /// map only in the frame that actually renders that projection; an enclosing or nested command's
    /// frame can never satisfy this command's projection lookups.
    /// </summary>
    private readonly List<Type?> _frameProjections = [];
    /// <summary>The index of the first entry owned by the command currently being rendered.</summary>
    private int SourceScopeStart => _sourceScopes.Count > 0 ? _sourceScopes.Peek() : 0;

    /// <inheritdoc/>
    public void PushSourceScope()
    {
        _sourceScopes.Add(_list.Count);
        _outerSlotFrames.Add(null);
        _frameProjections.Add(null);
    }

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
                _list[i] = (item.Item1, item.Item2, item.Item3, true, item.Item5);
        }
        _sourceScopes.Pop();
        // The frame's binding dictionary dies with the frame: a popped occurrence must not resolve.
        _outerSlotFrames.RemoveAt(_outerSlotFrames.Count - 1);
        _frameProjections.RemoveAt(_frameProjections.Count - 1);
    }

    /// <inheritdoc/>
    public void Add(Type entityType, bool fromProjection)
        => Add(entityType, fromProjection, sourceParameter: null);

    /// <inheritdoc/>
    public void Add(Type entityType, bool fromProjection, ParameterExpression? sourceParameter)
    {
        _list.Add((entityType, null, fromProjection, false, sourceParameter));
        if (sourceParameter is not null)
            _paramSources[sourceParameter] = _list.Count - 1;
    }

    /// <inheritdoc/>
    public void Add(QueryCommand queryCommand, bool fromProjection)
    {
        _list.Add((queryCommand.ResultType!, queryCommand, fromProjection, false, null));
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
        // Occurrence identity first (#148-B R2.2): a source registered with this exact parameter binds
        // by reference, so two same-typed navigation joins never merge. Returning null (rather than
        // falling through) when the entry is not visible keeps a popped or out-of-scope navigation
        // source from silently resolving to a same-typed sibling.
        if (_paramSources.TryGetValue(param, out var direct))
        {
            var directItem = _list[direct];
            if (directItem.Item3 == fromProjection
                && directItem.Item4 == onlyNested
                && (onlyNested || includeOuterScopes || direct >= SourceScopeStart))
                return direct;

            return null;
        }

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

    /// <inheritdoc/>
    public (int Index, QueryCommand? Command)? FindInScopeQueryCommand(Type entityType)
    {
        for (var (i, cnt) = (SourceScopeStart, _list.Count); i < cnt; i++)
        {
            var item = _list[i];
            if (item.Item4 || item.Item1 != entityType)
                continue;

            return (i, item.Item2);
        }

        return null;
    }

    /// <inheritdoc/>
    public (int Index, QueryCommand? Command)? FindInScopeQueryCommand(ParameterExpression param, bool fromProjection)
    {
        var idx = FindAliasInScope(param, fromProjection, includeOuterScopes: false, onlyNested: false);
        return idx.HasValue ? (idx.Value, _list[idx.Value].Item2) : null;
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
    /// <inheritdoc/>
    int? IOuterSlotSourceRegistry.ResolveFrameSourceIndex(int slot)
    {
        if (slot < 1)
            return null;

        var seen = 0;
        for (var (i, cnt) = (SourceScopeStart, _list.Count); i < cnt; i++)
        {
            if (_list[i].Item4)
                continue;

            if (++seen == slot)
                return i;
        }

        return null;
    }

    /// <inheritdoc/>
    void IOuterSlotSourceRegistry.PushOuterSlotSource(ParameterExpression owner, int slot, int sourceIndex)
    {
        if (_outerSlotFrames.Count == 0)
            throw new InvalidOperationException(
                "A correlated APPLY left-row binding can only be registered inside an active source scope.");

        var frame = _outerSlotFrames[^1] ??= new Dictionary<(ParameterExpression Owner, int Slot), int>();
        frame[(owner, slot)] = sourceIndex;
    }

    /// <inheritdoc/>
    int? IOuterSlotSourceRegistry.FindOuterSlotSource(ParameterExpression owner, int slot)
    {
        // Innermost frame first: an inner APPLY level shadows nothing (owner parameters are distinct
        // instances), but a popped frame is gone, so a stale occurrence can never resolve.
        for (var i = _outerSlotFrames.Count - 1; i >= 0; i--)
        {
            if (_outerSlotFrames[i] is { } frame && frame.TryGetValue((owner, slot), out var sourceIndex))
                return sourceIndex;
        }

        return null;
    }

    /// <inheritdoc/>
    void IOuterSlotSourceRegistry.BindFrameProjection(Type? projectionType)
    {
        if (_frameProjections.Count > 0)
            _frameProjections[^1] = projectionType;
    }

    /// <inheritdoc/>
    int? IOuterSlotSourceRegistry.ResolveProjectionSlotSourceIndex(Type projectionType, int slot)
    {
        // Only the frame that renders this projection may answer, and only when it carries a
        // correlated-APPLY binding: a non-zero enclosing scope, an unavailable frame or an unrelated
        // nested command therefore keeps the legacy type/occurrence path instead of borrowing an alias.
        if (_frameProjections.Count == 0
            || !ReferenceEquals(_frameProjections[^1], projectionType)
            || _outerSlotFrames[^1] is null)
            return null;

        if (((IOuterSlotSourceRegistry)this).ResolveFrameSourceIndex(slot) is int sourceIndex)
            return sourceIndex;

        var registered = 0;
        for (var (i, cnt) = (SourceScopeStart, _list.Count); i < cnt; i++)
        {
            if (!_list[i].Item4)
                registered++;
        }

        throw new InvalidOperationException(
            $"The SELECT projection addresses slot {slot} of '{projectionType}', but the active render frame of this command registers only {registered} source(s). A projection item and the APPLY correlation that shares its slot may only address the FROM source plus the joins that precede the correlated APPLY; a forward or out-of-frame slot is refused instead of resolving to another alias.");
    }
}