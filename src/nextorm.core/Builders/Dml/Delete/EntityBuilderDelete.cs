namespace NextORM.Core;

/// <summary>Deletes rows selected by a query over a single mapped table.</summary>
public static class QueryDeleteExtensions
{
    /// <summary>Deletes rows from a mapped table using an explicit predicate, a positive limit, or both.</summary>
    public static int Delete<TEntity>(this EntityBuilder<TEntity> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.CreateQueryDelete().DeleteQuery(query);
    }

    /// <summary>Asynchronously deletes rows from a mapped table using an explicit predicate, a positive limit, or both.</summary>
    public static Task<int> DeleteAsync<TEntity>(this EntityBuilder<TEntity> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return query.CreateQueryDelete().DeleteQueryAsync(query, cancellationToken);
    }
}

public partial class EntityBuilder<TEntity>
{
    internal DeleteBuilder<TEntity> CreateQueryDelete()
    {
        if (_query is not null || _table is not null || _from is not null ||
            _joins is { Count: > 0 } || Ctes is { Count: > 0 } ||
            _sourceEntityType is not null || _tableNameOverride is not null ||
            _schemaOverride is not null || _databaseOverride is not null ||
            _serverOverride is not null || _tableExpression is not null)
        {
            throw new NotSupportedException("Query DELETE requires a single mapped table source.");
        }

        if (_hasExplicitLimit && Paging.Limit == 0)
            throw new InvalidOperationException("Query DELETE does not allow an explicit zero limit.");

        if (_condition is null && Paging.Limit == 0)
            throw new InvalidOperationException("Query DELETE requires an explicit Where predicate or a positive limit.");

        if (Paging.Offset > 0 || Paging.HasWithTies || _group is not null || _having is not null ||
            _componentState is not null || _extremeRow is not null || _extras?.SubQueryHint is not null ||
            IsDistinct || DistinctOnClause is not null || ArrayJoins is { Count: > 0 } ||
            LoadSpecs is { Count: > 0 } || JoinIntos is { Count: > 0 } ||
            LimitByClause is not null || GroupByWithTotals || Windows is { Count: > 0 } ||
            RowLockClause is not null || SourceLockClause is not null ||
            TableSampleClause is not null || TemporalClause is not null || IsFinal ||
            SampleRatio is not null || SampleOffset != 0 || PreWhereCondition is not null ||
            SettingsList is { Count: > 0 } || TableHints is { Count: > 0 } ||
            IndexHints is { Count: > 0 } || TablesInScopeHints is { Count: > 0 } ||
            CommandTimeout is not null || Tag is not null || NamingConvention is not null ||
            QuoteIdentifiers is not null || KeywordCase is not null)
        {
            throw new NotSupportedException("Query DELETE does not support these query options.");
        }

        if (Paging.Limit == 0 && _sorting is { Count: > 0 })
            throw new InvalidOperationException("DELETE ordering requires a positive limit.");

        return RequireContext.CreateDeleteBuilder<TEntity>();
    }
}
