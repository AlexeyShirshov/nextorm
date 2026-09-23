using NextORM.Core;

namespace NextORM.Core;

/// <summary>
/// Fluent terminal for a <c>TRUNCATE TABLE</c>, started with
/// <see cref="DataContextExtensions.Truncate{TEntity}"/>. Renders the provider's native form and
/// executes it on the context; providers without <c>TRUNCATE</c> (SQLite) reject it with
/// <see cref="NotSupportedException"/>.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type whose table is truncated.</typeparam>
public sealed class TruncateBuilder<TEntity>
{
    private readonly IDataContext _dataContext;
    private readonly IEntityMetadata _metadata;

    internal TruncateBuilder(IDataContext dataContext, IEntityMetadata metadata)
    {
        _dataContext = dataContext;
        _metadata = metadata;
    }

    /// <summary>Renders the parameterised SQL this builder would execute, without executing it.</summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context is not database-backed, or the provider has no <c>TRUNCATE</c>.</exception>
    public string ToSql()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_dataContext.GetType().Name} cannot render SQL: it is not a database-backed context.");
    }

    /// <summary>Executes the truncate and returns the affected-row count where the provider reports one.</summary>
    /// <returns>The affected-row count, or 0 where the provider reports none.</returns>
    /// <exception cref="NotSupportedException">The context is read-only, or the provider has no <c>TRUNCATE</c>.</exception>
    public int Execute()
    {
        var command = BuildCommand();

        if (_dataContext is IMutationExecutor executor)
            return executor.Execute(command);

        throw Unsupported();
    }

    /// <summary>Asynchronously executes the truncate and returns the affected-row count where the provider reports one.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the affected-row count, or 0 where the provider reports none.</returns>
    /// <exception cref="NotSupportedException">The context is read-only, or the provider has no <c>TRUNCATE</c>.</exception>
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var command = BuildCommand();

        if (_dataContext is IMutationExecutor executor)
            return executor.Execute(command, cancellationToken);

        throw Unsupported();
    }

    private TruncateCommand BuildCommand()
        => new(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto);

    private NotSupportedException Unsupported()
        => new(
            $"{_dataContext.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB).");
}
