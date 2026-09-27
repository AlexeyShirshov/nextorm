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
    /// Creates a <c>MySqlParameter</c>, mapping a <see langword="null"/> value to <c>DBNull.Value</c>
    /// because MySqlConnector rejects an unset parameter value.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new MySQL parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
    {
        // MySqlConnector rejects a null parameter value, so unset/null values must be passed as DBNull.
        return new MySqlParameter(name, value ?? DBNull.Value);
    }

    /// <summary>
    /// Creates a <c>MySqlParameter</c> for a descriptor. A <see cref="TableParameterValue"/> is emulated
    /// as a JSON text parameter (<c>JSON_TABLE(@p, '$[*]' COLUMNS(...))</c> on MySQL 8.0+/MariaDB
    /// 10.6+). A <see cref="ProcedureParameter.TypeName"/> is SQL Server only and is rejected.
    /// </summary>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new MySQL parameter configured from <paramref name="parameter"/>.</returns>
    /// <exception cref="ArgumentException"><see cref="ProcedureParameter.TypeName"/> is set (SQL Server only), or a table parameter is not <see cref="ParameterDirection.Input"/>.</exception>
    protected override DbParameter CreateProcedureParameter(ProcedureParameter parameter)
    {
        if (parameter.Value is TableParameterValue tableValue)
        {
            RejectTypeName(parameter);
            RejectNonInputTable(parameter);
            return new MySqlParameter(parameter.Name, tableValue.WriteJson(this));
        }

        RejectTypeName(parameter);
        return base.CreateProcedureParameter(parameter);
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
