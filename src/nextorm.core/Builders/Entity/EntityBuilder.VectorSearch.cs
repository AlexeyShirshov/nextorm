using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <content>Provider-independent nearest-neighbor query entry point.</content>
public partial class EntityBuilder<TEntity>
{
    private static readonly MethodInfo VectorDistanceMethod =
        typeof(SqlFunctions).GetMethod(nameof(SqlFunctions.VectorDistance))!;
    private static readonly MethodInfo ColumnMethod =
        typeof(SqlFunctions).GetMethod(nameof(SqlFunctions.Column))!;

    /// <summary>
    /// Searches this entity query for its <paramref name="topK"/> nearest rows, ordered by the
    /// selected vector-distance metric.
    /// </summary>
    /// <typeparam name="TVector">The vector property type.</typeparam>
    /// <param name="vectorSelector">Selects the vector property on each entity.</param>
    /// <param name="queryVector">The vector to search near.</param>
    /// <param name="metric">The distance metric. Lower distances are nearer.</param>
    /// <param name="topK">The positive maximum number of neighbors to return.</param>
    /// <param name="mode">The requested exact or approximate search mode.</param>
    /// <returns>A query of entities paired with their computed distance.</returns>
    /// <remarks>
    /// The source query's filtering and source modifiers are preserved, while its prior ordering and
    /// paging are replaced by distance ascending and <paramref name="topK"/>. PostgreSQL emits the same
    /// distance-operator query for <see cref="VectorSearchMode.Exact"/> and
    /// <see cref="VectorSearchMode.Approximate"/>; its planner may use HNSW or IVFFlat transparently.
    /// SQL Server emits <paramref name="topK"/> as a literal and uses the context's explicitly selected
    /// <c>VECTOR_SEARCH</c> grammar for approximate search.
    /// The mode does not rewrite the query into a different metric or result semantics. Other providers
    /// reject vector search during preparation unless their dialect advertises support.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="vectorSelector"/> or <paramref name="queryVector"/> is null.</exception>
    /// <exception cref="ArgumentException">The query vector has no dimensions, or an enum value is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="topK"/> is not positive.</exception>
    public QueryCommand<VectorSearchResult<TEntity>> VectorSearch<TVector>(
        Expression<Func<TEntity, TVector>> vectorSelector,
        TVector queryVector,
        VectorDistanceMetric metric,
        int topK,
        VectorSearchMode mode = VectorSearchMode.Exact)
        where TVector : IVectorValue
    {
        if (DataProvider is InMemoryDataContext)
            throw new NotSupportedException(
                "Vector distance and nearest-neighbor search are not supported by the InMemoryDialect provider.");

        ArgumentNullException.ThrowIfNull(vectorSelector);
        ArgumentNullException.ThrowIfNull(queryVector);
        if (queryVector.Dimensions <= 0)
            throw new ArgumentException("The query vector must have at least one dimension.", nameof(queryVector));
        if (topK <= 0 && mode == VectorSearchMode.Approximate)
            throw new InvalidOperationException("The approximate vector-search top-k value must be positive.");
        if (topK <= 0)
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "The nearest-neighbor top-k value must be positive.");
        if (metric is not (VectorDistanceMetric.Cosine or VectorDistanceMetric.Euclidean or VectorDistanceMetric.DotProduct))
            throw new ArgumentException("The vector distance metric is not supported by the unified vector-search API.", nameof(metric));
        if (mode is not (VectorSearchMode.Exact or VectorSearchMode.Approximate))
            throw new ArgumentException("The vector search mode must be Exact or Approximate.", nameof(mode));

        var dialect = (DataProvider as DataContext)?.Dialect;
        var sqlServerApproximate = mode == VectorSearchMode.Approximate
            && dialect?.SqlServerFunctions is not null;

        if (sqlServerApproximate)
        {
            if (_sorting is not null)
                throw new InvalidOperationException("SQL Server approximate VECTOR_SEARCH owns ORDER BY distance ascending; a user OrderBy cannot be combined with it.");
            if (Paging.Limit > 0 || Paging.Offset > 0)
                throw new InvalidOperationException("SQL Server approximate VECTOR_SEARCH owns TOP (@k); existing paging cannot be combined with it.");
            if (_query is not null || _joins is { Count: > 0 } || _group is not null || _having is not null || IsDistinct)
                throw new NotSupportedException("SQL Server approximate VECTOR_SEARCH requires a direct entity-table source without joins, grouping, DISTINCT, or a derived query.");
        }

        Expression<Func<TVector>> queryVectorParameter = () => queryVector;
        var vectorDistance = Expression.Call(
            VectorDistanceMethod.MakeGenericMethod(typeof(TVector)),
            vectorSelector.Body,
            queryVectorParameter.Body,
            Expression.Constant(metric));
        var parameter = vectorSelector.Parameters[0];
        var distance = sqlServerApproximate
            ? Expression.Call(
                ColumnMethod.MakeGenericMethod(typeof(double?)),
                Expression.Convert(parameter, typeof(object)),
                Expression.Constant("distance"))
            : vectorDistance;
        var orderSelector = Expression.Lambda<Func<TEntity, object?>>(
            Expression.Convert(distance, typeof(object)), parameter);

        var resultType = typeof(VectorSearchResult<TEntity>);
        var resultConstructor = resultType.GetConstructor([typeof(TEntity), typeof(double?)])!;
        var resultExpression = Expression.New(resultConstructor, parameter, distance);
        var resultSelector = Expression.Lambda<Func<TEntity, VectorSearchResult<TEntity>>>(resultExpression, parameter);

        // k-NN has its own primary order and page; an earlier user sort/offset must not change which
        // rows are the nearest top-k. Filters and other source shape remain intact.
        var search = Clone();
        search._sorting = null;
        search.Paging.Limit = topK;
        search.Paging.Offset = 0;
        if (sqlServerApproximate)
        {
            var (tableName, columnName) = ResolveApproximateVectorSource(vectorSelector.Body);
            search._from = new FromExpression(TableFunctionExpression.CreateApproximateVectorSearch(
                tableName,
                columnName,
                (MethodCallExpression)vectorDistance,
                queryVectorParameter.Body,
                GetSqlServerMetric(metric),
                Expression.Constant(topK),
                typeof(TEntity)));
            search._table = null;
            search._tableNameOverride = null;
            search._schemaOverride = null;
            search._databaseOverride = null;
            search._serverOverride = null;
        }

        return search.OrderBy(orderSelector).Select(resultSelector);
    }

    private (string TableName, string ColumnName) ResolveApproximateVectorSource(Expression vectorSelector)
    {
        var dialect = (DataProvider as DataContext)?.Dialect
            ?? throw new NotSupportedException("SQL Server approximate VECTOR_SEARCH requires a provider backed by DataContext.");

        if (TypeFacts.UnwrapConvert(vectorSelector) is not MemberExpression { Member: PropertyInfo property })
            throw new NotSupportedException("SQL Server approximate VECTOR_SEARCH requires a direct mapped vector property.");

        var source = _from;
        if (source is not null
            && (source.Table is null || source.TableFunction is not null || source.SubQuery is not null
                || source.RawSqlSource is not null || source.TableExpressionOverride is not null))
            throw new NotSupportedException("SQL Server approximate VECTOR_SEARCH requires a physical entity-table source.");

        if (!DataContextCache.Metadata.TryGetValue(typeof(TEntity), out var metadata))
            throw new InvalidOperationException($"Entity metadata for '{typeof(TEntity).Name}' is unavailable for SQL Server approximate VECTOR_SEARCH.");

        var tableName = _table ?? source?.Table ?? metadata.TableName ?? typeof(TEntity).Name;
        var tableNameOverride = _tableNameOverride ?? source?.TableNameOverride;
        var schemaOverride = _schemaOverride ?? source?.SchemaOverride;
        var databaseOverride = _databaseOverride ?? source?.DatabaseOverride;
        var serverOverride = _serverOverride ?? source?.ServerOverride;

        if (tableNameOverride is not null)
            tableName = tableNameOverride;
        else if (_table is null && source?.Table is null && metadata.IsTableNameAuto && NamingConvention is { } namingConvention)
            tableName = namingConvention.TableName(tableName, typeof(TEntity).IsInterface);
        else if (source is { IsAutoMapped: true } && NamingConvention is { } sourceNamingConvention)
            tableName = sourceNamingConvention.TableName(tableName, source.SourceIsInterface);

        var isSqlServer = dialect.SqlServerFunctions is not null;
        var hasQualifiedTableName = tableName.Contains('.', StringComparison.Ordinal);
        var effectiveSchema = schemaOverride ?? (isSqlServer && !hasQualifiedTableName ? "dbo" : null);
        if (serverOverride is not null || databaseOverride is not null || effectiveSchema is not null)
            tableName = dialect.MakeQualifiedTableName(serverOverride, databaseOverride, effectiveSchema, tableName);

        var propertyMetadata = metadata.Properties.FirstOrDefault(item => item.PropertyInfo == property)
            ?? throw new NotSupportedException($"The vector property '{property.Name}' is not mapped for SQL Server approximate VECTOR_SEARCH.");
        var columnName = propertyMetadata.ColumnName;
        if (propertyMetadata.IsColumnNameAuto && NamingConvention is { } columnNamingConvention)
            columnName = columnNamingConvention.ColumnName(property.Name);
        columnName ??= property.Name;

        return (tableName, columnName);
    }

    private static string GetSqlServerMetric(VectorDistanceMetric metric)
        => metric switch
        {
            VectorDistanceMetric.Cosine => "cosine",
            VectorDistanceMetric.DotProduct => "dot",
            VectorDistanceMetric.Euclidean => "euclidean",
            _ => throw new NotSupportedException($"The vector distance metric '{metric}' is not supported by SQL Server.")
        };
}
