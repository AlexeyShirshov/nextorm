using NextORM.Core;

namespace NextORM.Core;

/// <summary>
/// Fluent terminal for a <c>TRUNCATE TABLE</c>, started with
/// <see cref="DataContextExtensions.CreateTruncateBuilder{TEntity}(IDataContext, MappingVariant)"/>. Renders the provider's native form and
/// executes it on the context; providers without <c>TRUNCATE</c> (SQLite) reject it with
/// <see cref="NotSupportedException"/>.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type whose table is truncated.</typeparam>
public sealed class TruncateBuilder<TEntity>
{
    private readonly IDataContext _dataContext;
    private readonly IEntityMetadata _metadata;
    private readonly MappingVariant _mappingVariant;

    internal TruncateBuilder(IDataContext dataContext, IEntityMetadata metadata, MappingVariant mappingVariant = default)
    {
        _dataContext = dataContext;
        _metadata = metadata;
        _mappingVariant = mappingVariant;
    }

    /// <summary>The context the truncate executes on.</summary>
    internal IDataContext DataContext => _dataContext;

    /// <summary>Renders the parameterised SQL this builder would execute, without executing it.</summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context is not database-backed, or the provider has no <c>TRUNCATE</c>.</exception>
    public string ToSql()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_dataContext.GetType().Name} cannot render SQL: it is not a database-backed context.");
    }

    /// <summary>Executes the truncate.</summary>
    /// <returns>The driver's non-query result, which may not indicate how many rows were removed.</returns>
    /// <exception cref="NotSupportedException">The context is read-only, or the provider has no <c>TRUNCATE</c>.</exception>
    public int Execute()
    {
        var command = BuildCommand();

        if (_dataContext is IMutationExecutor executor)
            return executor.Execute(command);

        throw Unsupported();
    }

    /// <summary>Asynchronously executes the truncate.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the driver's non-query result, which may not indicate how many rows were removed.</returns>
    /// <exception cref="NotSupportedException">The context is read-only, or the provider has no <c>TRUNCATE</c>.</exception>
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var command = BuildCommand();

        if (_dataContext is IMutationExecutor executor)
            return executor.Execute(command, cancellationToken);

        throw Unsupported();
    }

    /// <summary>Builds the truncate command for use as a side-effecting step of a batch.</summary>
    /// <returns>The truncate command.</returns>
    internal MutationCommand BuildBatchCommand() => BuildCommand();

    private TruncateCommand BuildCommand()
    {
        EnsureShardScopeResolved();
        return new(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, _mappingVariant);
    }

    /// <summary>
    /// Rejects a sharded truncate that carries no resolved shard scope: truncating the default table of a
    /// sharded entity is never a silent fallback.
    /// </summary>
    private void EnsureShardScopeResolved()
    {
        if (_mappingVariant != MappingVariant.Default)
            return;

        if (_dataContext is not DataContext dataContext || dataContext.EntityVariantResolver is not IShardRouter router)
            return;

        if (!router.IsSharded(typeof(TEntity)))
            return;

        throw new InvalidOperationException(
            $"Entity '{typeof(TEntity)}' is sharded. Pass a shard mapping variant to CreateTruncateBuilder(...).");
    }

    private NotSupportedException Unsupported()
        => new(
            $"{_dataContext.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB).");
}
