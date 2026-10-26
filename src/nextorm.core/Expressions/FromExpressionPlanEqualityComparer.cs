using System.Diagnostics.CodeAnalysis;
namespace NextORM.Core;

/// <summary>
/// Compares and hashes query sources for plan-cache purposes: table names by value, and subqueries,
/// table functions, pivots and xml row-sets structurally.
/// </summary>
public sealed class FromExpressionPlanEqualityComparer : IEqualityComparer<FromExpression?>
{
    //private readonly IDictionary<ExpressionKey, Delegate> _cache;
    // private readonly IQueryRegistry _queryProvider;
    private readonly Lazy<QueryPlanEqualityComparer> _equalityComparer;
    // Table-function calls are compared/hashed structurally, with the same closure-aware comparer the
    // WHERE/SELECT expressions use, so two equivalent calls built from different closure instances
    // still share a cached plan while different arguments produce different plans.
    private readonly ExpressionPlanEqualityComparer _expComparer;

    //private readonly ILogger? _logger;
    //private readonly ExpressionPlanEqualityComparer _expComparer;
    //private QueryPlanEqualityComparer? _cmdComparer;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    /// <summary>Initializes a comparer.</summary>
    /// <param name="queryProvider">The registry that supplies the nested plan comparers.</param>
    public FromExpressionPlanEqualityComparer(IQueryRegistry queryProvider)
    //        : this(cache, queryProvider, null)
    {
        //_cache = cache ?? new ExpressionCache<Delegate>();
        _equalityComparer = new Lazy<QueryPlanEqualityComparer>(queryProvider.GetQueryPlanEqualityComparer);
        _expComparer = queryProvider.GetExpressionPlanEqualityComparer();
    }
    // public FromExpressionPlanEqualityComparer(IDictionary<ExpressionKey, Delegate>? cache, IQueryRegistry queryProvider, ILogger? logger)
    // {
    //     _cache = cache ?? new ExpressionCache<Delegate>();
    //     _queryProvider = queryProvider;
    //     _logger = logger;
    //     //_expComparer = new ExpressionPlanEqualityComparer(cache);        
    // }
    // private FromExpressionPlanEqualityComparer() { }
    // public static FromExpressionPlanEqualityComparer Instance => new();
    /// <summary>Determines whether two query sources are equal for plan-cache purposes.</summary>
    /// <param name="x">The first source.</param>
    /// <param name="y">The second source.</param>
    /// <returns><c>true</c> when both sources resolve to the same underlying source.</returns>
    public bool Equals(FromExpression? x, FromExpression? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        // A per-query source override changes the rendered SQL without changing the table name, so it
        // has to be part of the key before the table-name short-circuit below would consider the two
        // sources equal and share a plan.
        if (!string.Equals(x.TableNameOverride, y.TableNameOverride, StringComparison.Ordinal)) return false;
        if (!string.Equals(x.SchemaOverride, y.SchemaOverride, StringComparison.Ordinal)) return false;
        if (!string.Equals(x.DatabaseOverride, y.DatabaseOverride, StringComparison.Ordinal)) return false;
        if (!string.Equals(x.ServerOverride, y.ServerOverride, StringComparison.Ordinal)) return false;
        if (!string.Equals(x.TableExpressionOverride, y.TableExpressionOverride, StringComparison.Ordinal)) return false;

        // The entity binding (its entity type, effective metadata and declared column shape) changes which
        // filters are injected, so it must be part of the key before the table-name short-circuit below
        // would treat two bindings of the same source as equal and share a plan.
        if (!BindingEquals(x.SourceBinding, y.SourceBinding)) return false;

        // A raw fragment has no structural identity here (its SQL and parameters are opaque to the
        // comparer): two raw sources are the same source only when they are the same instance. Without
        // this branch the fallback below compared the two null subqueries and treated every raw source as
        // equal, letting unrelated FromSql queries share a cached plan.
        if (x.RawSqlSource is not null || y.RawSqlSource is not null)
            return ReferenceEquals(x.RawSqlSource, y.RawSqlSource);

        // if (x.TableAlias != y.TableAlias) return false;
        if (!string.Equals(x.SubQueryHint, y.SubQueryHint, StringComparison.Ordinal)) return false;

        // A source-level row lock changes the rendered SQL (FOR UPDATE OF <alias> or a targeted hint)
        // without changing the table name, so it must be part of the key before the table-name
        // short-circuit below would treat two differently-locked sources as equal.
        if (x.SourceLock != y.SourceLock) return false;

        // A named table and a table-less source (type-only, subquery, function, ...) are different
        // source kinds. Without this guard the null-subquery fallthrough below treated a named table
        // and a type-only APPLY source as equal, so the two joins shared one cached plan.
        if (string.IsNullOrEmpty(x.Table) != string.IsNullOrEmpty(y.Table)) return false;

        if (!string.IsNullOrEmpty(x.Table) && x.Table == y.Table) return true;

        // A type-only source is identified by its CLR source type: the applied join renders from it,
        // so two different types must not fall through to the equal-null-subquery comparison. Equal
        // type-only sources keep comparing equal (and keep equal hashes).
        if (string.IsNullOrEmpty(x.Table) && string.IsNullOrEmpty(y.Table)
            && (x.SourceType is not null || y.SourceType is not null)
            && x.SourceType != y.SourceType)
            return false;

        // A SelectMany/GroupJoin source is a computed node whose selectors are delegates over the
        // outer row; compare by identity so an unrelated node never shares a cached plan. (The
        // compiled selectors have their own closure-aware cache in the in-memory provider.)
        if (x.LinqSource is not null || y.LinqSource is not null)
            return ReferenceEquals(x.LinqSource, y.LinqSource);

        if (x.TableFunction is not null || y.TableFunction is not null)
        {
            if (x.TableFunction is null || y.TableFunction is null) return false;

            return TableFunctionEquals(x.TableFunction, y.TableFunction);
        }

        if (x.Pivot is not null || y.Pivot is not null)
            return PivotEquals(x.Pivot, y.Pivot);

        if (x.XmlNodes is not null || y.XmlNodes is not null)
        {
            if (x.XmlNodes is null || y.XmlNodes is null) return false;
            return x.XmlNodes.XPath == y.XmlNodes.XPath
                && _expComparer.Equals(x.XmlNodes.Operand, y.XmlNodes.Operand);
        }

        return _equalityComparer.Value.Equals(x.SubQuery, y.SubQuery);
    }

    /// <summary>
    /// Compares two table-function sources by their <b>SQL identity</b>, never by the factory method or
    /// the descriptor instance that produced them: name, schema and clause text, the verbatim argument
    /// positions, the declared result placement/shape, the built-in classification and the argument
    /// expressions. A legacy <see cref="SqlTableFunctionAttribute"/> declaration and a runtime
    /// <see cref="TvfEntityBuilder{T}"/> factory with the same SQL identity must share a cached plan
    /// (AC08); anything that changes the rendered statement or its support checks must not.
    /// </summary>
    private bool TableFunctionEquals(TableFunctionExpression x, TableFunctionExpression y)
    {
        if (!string.Equals(x.Name, y.Name, StringComparison.Ordinal)) return false;
        if (!string.Equals(x.Schema, y.Schema, StringComparison.Ordinal)) return false;
        if (!string.Equals(x.WithClause, y.WithClause, StringComparison.Ordinal)) return false;
        if (!string.Equals(x.CallClause, y.CallClause, StringComparison.Ordinal)) return false;

        // The declared result placement is rendered into the SQL, so it is part of identity for every
        // value. The row type only matters when a schema is actually derived from it: a legacy source
        // without a declared schema carries no result type, while the runtime node always knows its row
        // type, and that difference must not split two identical SQL identities.
        if (x.ResultSchema != y.ResultSchema) return false;
        if (x.ResultSchema != TableFunctionSchema.None && x.ResultType != y.ResultType) return false;

        // Built-in functions get the dialect support check and the string-literal rendering policy from
        // this classification (see SqlSourceRenderer.MakeTableFunction), so it is part of identity.
        if (IsBuiltInTableFunction(x) != IsBuiltInTableFunction(y)) return false;

        if (!VerbatimArgumentsEqual(x.VerbatimArguments, y.VerbatimArguments)) return false;

        // Compare the argument expressions themselves, not the whole factory call: the factory method
        // is not part of the SQL identity.
        var xArguments = x.Arguments;
        var yArguments = y.Arguments;
        if (xArguments.Count != yArguments.Count) return false;

        for (var i = 0; i < xArguments.Count; i++)
        {
            if (!_expComparer.Equals(xArguments[i], yArguments[i])) return false;
        }

        return true;
    }

    private static bool IsBuiltInTableFunction(TableFunctionExpression function)
        => typeof(CommonFunctions).IsAssignableFrom(function.Call.Method.DeclaringType);

    private static bool VerbatimArgumentsEqual(IReadOnlyList<int>? x, IReadOnlyList<int>? y)
    {
        // Null and an empty list both mean "no verbatim arguments".
        var xCount = x?.Count ?? 0;
        var yCount = y?.Count ?? 0;
        if (xCount != yCount) return false;

        for (var i = 0; i < xCount; i++)
        {
            if (x![i] != y![i]) return false;
        }

        return true;
    }

    private int TableFunctionHash(TableFunctionExpression function)
    {
        var hash = new System.HashCode();
        hash.Add(function.Name, StringComparer.Ordinal);
        hash.Add(function.Schema, StringComparer.Ordinal);
        hash.Add(function.WithClause, StringComparer.Ordinal);
        hash.Add(function.CallClause, StringComparer.Ordinal);
        hash.Add((int)function.ResultSchema);
        if (function.ResultSchema != TableFunctionSchema.None)
            hash.Add(function.ResultType is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(function.ResultType));
        hash.Add(IsBuiltInTableFunction(function));

        var verbatimArguments = function.VerbatimArguments;
        hash.Add(verbatimArguments?.Count ?? 0);
        if (verbatimArguments is not null)
        {
            for (var i = 0; i < verbatimArguments.Count; i++)
                hash.Add(verbatimArguments[i]);
        }

        foreach (var argument in function.Arguments)
            hash.Add(_expComparer.GetHashCode(argument));

        return hash.ToHashCode();
    }

    private bool PivotEquals(PivotExpression? x, PivotExpression? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.IsUnpivot != y.IsUnpivot) return false;
        if (!Equals(x.Inner, y.Inner)) return false;

        if (x.IsUnpivot)
            return x.UnpivotValueColumn == y.UnpivotValueColumn
                && x.UnpivotNameColumn == y.UnpivotNameColumn
                && ColumnsEqual(x.Columns, y.Columns);

        return x.Aggregate == y.Aggregate
            && _expComparer.Equals(x.AggregateColumn, y.AggregateColumn)
            && _expComparer.Equals(x.ForColumn, y.ForColumn)
            && ValuesEqual(x.Values, y.Values);
    }

    private static bool ColumnsEqual(IReadOnlyList<UnpivotColumn> x, IReadOnlyList<UnpivotColumn> y)
    {
        if (x.Count != y.Count) return false;
        for (var i = 0; i < x.Count; i++)
            if (x[i].Column != y[i].Column) return false;
        return true;
    }

    private static bool ValuesEqual(IReadOnlyList<PivotValue> x, IReadOnlyList<PivotValue> y)
    {
        if (x.Count != y.Count) return false;
        for (var i = 0; i < x.Count; i++)
            if (x[i].Value != y[i].Value) return false;
        return true;
    }

    private int PivotHash(PivotExpression pivot)
    {
        var hash = new System.HashCode();
        hash.Add(pivot.IsUnpivot);
        hash.Add(GetHashCode(pivot.Inner));

        if (pivot.IsUnpivot)
        {
            hash.Add(pivot.UnpivotValueColumn);
            hash.Add(pivot.UnpivotNameColumn);
            foreach (var column in pivot.Columns)
                hash.Add(column.Column);
        }
        else
        {
            hash.Add(pivot.Aggregate);
            hash.Add(pivot.AggregateColumn is null ? 0 : _expComparer.GetHashCode(pivot.AggregateColumn));
            hash.Add(pivot.ForColumn is null ? 0 : _expComparer.GetHashCode(pivot.ForColumn));
            foreach (var value in pivot.Values)
                hash.Add(value.Value);
        }

        return hash.ToHashCode();
    }

    /// <summary>Returns a hash code for a query source consistent with the equality comparison.</summary>
    /// <param name="obj">The source to hash.</param>
    /// <returns>The hash code, or zero when <paramref name="obj"/> is <c>null</c>.</returns>
    public int GetHashCode(FromExpression? obj)
    {
        if (obj is null) return 0;

        var baseHash = GetBaseHash(obj);

        // The source-level lock is not part of the shape branches above, so fold it into the base hash
        // for every source kind. Null and a non-null lock must map to different keys.
        if (obj.SourceLock is { } sourceLock)
        {
            var withLock = new System.HashCode();
            withLock.Add(baseHash);
            withLock.Add(sourceLock);
            baseHash = withLock.ToHashCode();
        }

        if (obj.TableNameOverride is null && obj.SchemaOverride is null && obj.DatabaseOverride is null
            && obj.ServerOverride is null && obj.TableExpressionOverride is null)
            return baseHash;

        var hash = new System.HashCode();
        hash.Add(baseHash);
        hash.Add(obj.TableNameOverride, StringComparer.Ordinal);
        hash.Add(obj.SchemaOverride, StringComparer.Ordinal);
        hash.Add(obj.DatabaseOverride, StringComparer.Ordinal);
        hash.Add(obj.ServerOverride, StringComparer.Ordinal);
        hash.Add(obj.TableExpressionOverride, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    private static bool BindingEquals(FromExpression.EntityBinding? x, FromExpression.EntityBinding? y)
    {
        if (x is null || y is null)
            return x is null && y is null;

        if (x.EntityType != y.EntityType)
            return false;

        // Two different effective mappings of the same entity type produce different filter predicates,
        // so compare the metadata instance (not just the entity type) to keep their plans isolated.
        if (!ReferenceEquals(EffectiveMetadata(x.EntityType), EffectiveMetadata(y.EntityType)))
            return false;

        var xColumns = x.AvailableColumns;
        var yColumns = y.AvailableColumns;
        if (xColumns.Count != yColumns.Count)
            return false;

        for (var i = 0; i < xColumns.Count; i++)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(xColumns[i], yColumns[i]))
                return false;
        }

        return true;
    }

    private static IEntityMetadata? EffectiveMetadata(Type entityType)
        => DataContextCache.Metadata.TryGetValue(entityType, out var metadata) ? metadata : null;

    private static void AddBindingHash(ref System.HashCode hash, FromExpression.EntityBinding? binding)
    {
        if (binding is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(binding.EntityType));
        var metadata = EffectiveMetadata(binding.EntityType);
        hash.Add(metadata is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(metadata));
        hash.Add(binding.AvailableColumns.Count);

        for (var i = 0; i < binding.AvailableColumns.Count; i++)
            hash.Add(binding.AvailableColumns[i], StringComparer.OrdinalIgnoreCase);
    }

    private int GetBaseHash(FromExpression obj)
    {
        // A raw fragment is identified by reference (see Equals) and carries its own binding identity.
        if (obj.RawSqlSource is not null)
        {
            var rawHash = new System.HashCode();
            rawHash.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.RawSqlSource));
            AddBindingHash(ref rawHash, obj.SourceBinding);
            return rawHash.ToHashCode();
        }

        // A named table with an entity binding is identified by its name plus the binding identity: two
        // bindings of the same table must not hash to the same plan key.
        if (obj.SourceBinding is not null)
        {
            var boundHash = new System.HashCode();
            boundHash.Add(obj.Table, StringComparer.Ordinal);
            AddBindingHash(ref boundHash, obj.SourceBinding);
            return boundHash.ToHashCode();
        }

        if (obj.LinqSource is not null)
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.LinqSource);

        if (obj.TableFunction is not null)
            return TableFunctionHash(obj.TableFunction);

        if (obj.Pivot is not null)
            return PivotHash(obj.Pivot);

        if (obj.XmlNodes is not null)
        {
            var nodesHash = new System.HashCode();
            nodesHash.Add(obj.XmlNodes.XPath);
            nodesHash.Add(_expComparer.GetHashCode(obj.XmlNodes.Operand));
            return nodesHash.ToHashCode();
        }

        if (!string.IsNullOrEmpty(obj.Table))
            return StringComparer.Ordinal.GetHashCode(obj.Table);
        else
        {
            var subQueryHash = _equalityComparer.Value.GetHashCode(obj.SubQuery!);

            // A type-only source (the APPLY right-hand shape) must hash its CLR source type: two
            // different types never compare equal, so they must not share the type-only hash bucket.
            if (obj.SourceType is not null)
            {
                var typeOnlyHash = new System.HashCode();
                typeOnlyHash.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.SourceType));
                typeOnlyHash.Add(subQueryHash);
                if (obj.SubQueryHint is not null)
                    typeOnlyHash.Add(obj.SubQueryHint);
                return typeOnlyHash.ToHashCode();
            }

            if (obj.SubQueryHint is null)
                return subQueryHash;

            var hintHash = new System.HashCode();
            hintHash.Add(subQueryHash);
            hintHash.Add(obj.SubQueryHint);
            return hintHash.ToHashCode();
        }
    }
}