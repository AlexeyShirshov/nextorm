using System.Data;
using System.Data.Common;
using MySqlConnector;
using NextORM.Core;

namespace NextORM.MySql;

/// <summary>
/// Data context for MySQL. Wraps the <c>MySqlConnector</c> driver for connections and parameters and
/// renders SQL through <c>MySqlDialect</c>.
/// </summary>
public class MySqlDataContext : DataContext
{
    /// <summary>
    /// Creates a MySQL context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>MySqlConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public MySqlDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    /// <summary>
    /// Creates a MySQL context over a caller-supplied connection, which the context does not dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public MySqlDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    /// <summary>Creates a new <c>MySqlConnection</c> for <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">The MySQL connection string.</param>
    /// <returns>A new, unopened MySQL connection.</returns>
    protected override DbConnection CreateDbConnection(string? connectionString)
        => new MySqlConnection(connectionString);

    /// <inheritdoc/>
    public override ISqlDialect Dialect => MySqlDialect.Instance;

    /// <summary>
    /// Creates a <c>MySqlConnector</c> parameter, mapping a <see langword="null"/> value to
    /// <c>DBNull.Value</c> because <c>MySqlConnector</c> rejects an unset parameter value. This is the
    /// public 2-arg factory; the execution paths use the command-aware overloads below so a command
    /// from the active connection's driver mints the parameter.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new MySQL parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
        => new MySqlParameter(name, value ?? DBNull.Value);

    /// <summary>
    /// Mints the parameter through the executing command so it belongs to the connection's actual
    /// driver: <c>MySql.Data</c> (the driver of Oracle's EF Core provider) rejects a
    /// <c>MySqlConnector.MySqlParameter</c> in its parameter collection, and vice versa. No throwaway
    /// command is created: the active command owns the parameter and the execution layer adds it.
    /// </summary>
    /// <param name="command">The executing command the parameter will be added to.</param>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new MySQL parameter of the active connection's driver.</returns>
    protected override DbParameter CreateParam(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    /// <summary>
    /// Creates a <c>MySqlParameter</c> for a descriptor. A <see cref="TableParameterValue"/> is emulated
    /// as a JSON text parameter (<c>JSON_TABLE(@p, '$[*]' COLUMNS(...))</c> on MySQL 8.0+/MariaDB
    /// 10.6+). A <see cref="ProcedureParameter.TypeName"/> is SQL Server only and is rejected.
    /// </summary>
    /// <remarks>
    /// Kept for compatibility with callers that hold no executing command; the raw/procedure execution
    /// paths use the command-aware overload below, which mints the parameter through the command so it
    /// belongs to the connection's actual driver. This command-unaware overload falls back to the 2-arg
    /// <see cref="CreateParam(string, object?)"/> and is therefore not safe when the connection is
    /// driven by <c>MySql.Data</c>.
    /// </remarks>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new MySQL parameter configured from <paramref name="parameter"/>.</returns>
    /// <exception cref="ArgumentException"><see cref="ProcedureParameter.Name"/> is blank, <see cref="ProcedureParameter.TypeName"/> is set (SQL Server only), or a table parameter is not <see cref="ParameterDirection.Input"/>.</exception>
    protected override DbParameter CreateProcedureParameter(ProcedureParameter parameter)
        => CreateProcedureParameterCore(null, parameter);

    /// <summary>
    /// Command-aware variant used by the raw/procedure execution paths: mints the parameter through
    /// <paramref name="command"/> so it belongs to the connection's actual driver (<c>MySql.Data</c> vs
    /// <c>MySqlConnector</c>), which is required when Oracle's EF Core provider owns the connection.
    /// </summary>
    /// <param name="command">The executing command the parameter will be added to.</param>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new MySQL parameter of the active connection's driver.</returns>
    /// <exception cref="ArgumentException"><see cref="ProcedureParameter.Name"/> is blank, <see cref="ProcedureParameter.TypeName"/> is set (SQL Server only), or a table parameter is not <see cref="ParameterDirection.Input"/>.</exception>
    protected override DbParameter CreateProcedureParameter(DbCommand command, ProcedureParameter parameter)
        => CreateProcedureParameterCore(command, parameter);

    /// <summary>
    /// Single implementation behind both procedure-parameter overloads: validates the descriptor, then
    /// mints the provider parameter either through <paramref name="command"/> (raw/procedure execution;
    /// driver identity) or through the command-unaware 2-arg factory when no command is available. The
    /// name check is the first statement so a blank name fails before any provider work.
    /// </summary>
    /// <param name="command">The executing command, or <see langword="null"/> for the compatibility path.</param>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new MySQL parameter configured from <paramref name="parameter"/>.</returns>
    private DbParameter CreateProcedureParameterCore(DbCommand? command, ProcedureParameter parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter.Name);

        if (parameter.Value is TableParameterValue tableValue)
        {
            RejectTypeName(parameter);
            RejectNonInputTable(parameter);
            return command is null
                ? CreateParam(parameter.Name, tableValue.WriteJson(this))
                : CreateParam(command, parameter.Name, tableValue.WriteJson(this));
        }

        RejectTypeName(parameter);

        var dbParameter = command is null
            ? CreateParam(parameter.Name, parameter.Value)
            : CreateParam(command, parameter.Name, parameter.Value);

        return ApplyProcedureParameterMetadata(dbParameter, parameter);
    }

    private static void RejectTypeName(ProcedureParameter parameter)
    {
        if (parameter.TypeName is not null)
            throw new ArgumentException(
                $"TypeName is SQL Server only, but '{parameter.Name}' has TypeName = '{parameter.TypeName}'. "
                + "On MySQL/MariaDB pass the rows to ProcedureParameter.Table(name, rows) and read them with JSON_TABLE.",
                nameof(parameter));
    }

    private static void RejectNonInputTable(ProcedureParameter parameter)
    {
        if (parameter.Direction != ParameterDirection.Input)
            throw new ArgumentException(
                $"A table-valued parameter is input-only, but '{parameter.Name}' has Direction = {parameter.Direction}.",
                nameof(parameter));
    }
}
