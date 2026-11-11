using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite;

/// <summary>
/// Data context for SQLite. Wraps the <c>Microsoft.Data.Sqlite</c> driver for connections and
/// parameters, registers the SQLite function set on every connection and renders SQL through
/// <c>SqliteDialect</c>.
/// </summary>
public class SqliteDataContext : DataContext, IBulkInsertPortableScope
{
    /// <summary>
    /// Creates a SQLite context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>SqliteConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public SqliteDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }
    /// <summary>
    /// Creates a SQLite context over a caller-supplied connection, which the context does not dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public SqliteDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }
    /// <summary>Creates a new <c>SqliteConnection</c> for <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>A new, unopened SQLite connection.</returns>
    protected override DbConnection CreateDbConnection(string? connectionString)
        => new SqliteConnection(connectionString);

    /// <summary>
    /// Registers the custom SQLite functions on <paramref name="connection"/> so they are available to
    /// generated SQL.
    /// </summary>
    /// <param name="connection">The connection the context has just started using.</param>
    protected override void OnConnectionCreated(DbConnection connection)
    {
        // Registered for supplied connections too, matching the previous behaviour.
        if (connection is SqliteConnection sqliteConnection)
            SQLiteFunctions.Register(sqliteConnection);
    }

    /// <inheritdoc/>
    public override ISqlDialect Dialect => SqliteDialect.Instance;

    /// <summary>
    /// SQLite has no time-of-day type: a duration is an <c>INTEGER</c> tick count and a formatted value is
    /// <c>TEXT</c>. Reading either into a <see cref="TimeOnly"/> would reinterpret an integer or parse a
    /// string, so the projection is refused and a value converter must opt in explicitly.
    /// </summary>
    protected override string? TimeOnlyProjectionRefusal =>
        "TimeOnly projection refused for SQLite: there is no native time-of-day type; use an explicit value converter.";

    /// <summary>
    /// Applies the SQLite form of <c>BulkInsertOptions.CheckConstraints=false</c> for one bulk write.
    /// SQLite has no switch for CHECK constraints; the scoped control is <c>PRAGMA foreign_keys</c>,
    /// which the engine ignores once a transaction is open, so the request is refused by name there
    /// instead of writing with the constraints still enforced.
    /// </summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <returns>A scope that restores the previous pragma value when disposed.</returns>
    /// <exception cref="NotSupportedException">The pragma cannot be scoped: an active transaction is open, or the current value cannot be read.</exception>
    IDisposable IBulkInsertPortableScope.BeginPortableBulkInsertScope(BulkInsertCommand command)
    {
        EnsureKeepNullsIsDefaultEquivalent(command);

        if (command.Options.CheckConstraints is not false)
            return NoBulkInsertScope.Instance;

        // PRAGMA foreign_keys is a no-op inside a transaction, so the flag cannot be honoured there.
        if (((ITransactionManager)this).CurrentTransaction is not null)
            throw new NotSupportedException(
                "BulkInsertOptions.CheckConstraints=false cannot be honoured on SQLite inside an active transaction: "
                + "SQLite ignores PRAGMA foreign_keys once the transaction has started, so the write would still enforce "
                + "its CHECK and FOREIGN KEY constraints. Run the bulk insert outside the transaction, or leave "
                + "CheckConstraints unset.");

        var previous = ReadForeignKeyPragma();
        if (previous == 0)
            return NoBulkInsertScope.Instance;

        ExecutePragma("pragma foreign_keys = 0");
        return new ForeignKeyScope(this, previous);
    }

    // SQLite writes the supplied value, so a NULL is never replaced by the column DEFAULT: KeepNulls=false
    // is honoured only for a default-equivalent shape (every written column nullable, its SQL default
    // NULL/absent). Anything else would be accepted and inert, so it is refused by name instead.
    private void EnsureKeepNullsIsDefaultEquivalent(BulkInsertCommand command)
    {
        if (command.Options.KeepNulls is not false)
            return;

        var nullability = new NullabilityInfoContext();
        var nonNullable = new List<string>(command.Columns.Count);
        foreach (var column in command.Columns)
        {
            if (nullability.Create(column.PropertyInfo).WriteState != NullabilityState.Nullable)
                nonNullable.Add(column.ColumnName);
        }

        if (nonNullable.Count > 0)
        {
            throw new NotSupportedException(
                "BulkInsertOptions.KeepNulls=false cannot be honoured by the SQLite bulk path: the write always sends the supplied value, so a column DEFAULT applies only when the column is omitted. "
                + "The request is accepted only for a default-equivalent shape, where every written column is nullable with a NULL/absent SQL default; "
                + $"the non-nullable input column(s) {string.Join(", ", nonNullable.Select(name => $"'{name}'"))} break that shape. "
                + "Use KeepNulls=true, or write the DEFAULT explicitly.");
        }

        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in command.Columns)
            written.Add(SqlMutationBuilder.ResolveColumnName(column, NamingConvention));

        var defaulted = new List<string>();
        foreach (var name in ReadColumnsWithSqlDefault(
                     SqlMutationBuilder.ResolveTableName(command.TableName, command.IsTableNameAuto, command.EntityType, NamingConvention)))
        {
            if (written.Contains(name))
                defaulted.Add(name);
        }

        if (defaulted.Count == 0)
            return;

        throw new NotSupportedException(
            "BulkInsertOptions.KeepNulls=false cannot be honoured by the SQLite bulk path: the write always sends the supplied value, so a column DEFAULT applies only when the column is omitted. "
            + $"The written column(s) {string.Join(", ", defaulted.Select(name => $"'{name}'"))} declare a SQL default, so a NULL input would be written as NULL instead of the default. "
            + "Use KeepNulls=true, or write the DEFAULT explicitly.");
    }

    // The written columns whose live SQL default is non-NULL. Internal and virtual so the SQL-generation
    // tests, which own no database, can supply the schema facts instead of opening a connection.
    internal virtual IReadOnlyList<string> ReadColumnsWithSqlDefault(string table)
    {
        EnsureConnectionOpen();

        var defaulted = new List<string>();
        using var select = CreateCommand("select name from pragma_table_info('" + table.Replace("'", "''", StringComparison.Ordinal) + "') where dflt_value is not null");
        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
                defaulted.Add(reader.GetString(0));
        }

        return defaulted;
    }

    private int ReadForeignKeyPragma()
    {
        try
        {
            using var command = CreateCommand("pragma foreign_keys");
            var value = command.ExecuteScalar();
            return value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (SqliteException exception)
        {
            throw new NotSupportedException(
                "BulkInsertOptions.CheckConstraints=false cannot be honoured on SQLite: PRAGMA foreign_keys could not be read "
                + "for this connection, so the scoped constraint control cannot be applied or restored: " + exception.Message,
                exception);
        }
    }

    private void ExecutePragma(string sql)
    {
        EnsureConnectionOpen();
        using var command = CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates a <c>SqliteParameter</c>, mapping a <see langword="null"/> value to
    /// <c>DBNull.Value</c> because Microsoft.Data.Sqlite rejects an unset parameter value.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new SQLite parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
    {
        // Microsoft.Data.Sqlite throws "Value must be set." when a parameter holds null,
        // so unset/null values must be passed as DBNull.
        return new SqliteParameter(name, value ?? DBNull.Value);
    }

    /// <summary>
    /// Creates a <c>SqliteParameter</c> for a descriptor. A <see cref="TableParameterValue"/> is
    /// emulated as a JSON text parameter consumed with <c>json_each(@p)</c> (scalars) or
    /// <c>json_extract(value, '$.col')</c> (entities). A <see cref="ProcedureParameter.TypeName"/> is
    /// SQL Server only and is rejected.
    /// </summary>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new SQLite parameter configured from <paramref name="parameter"/>.</returns>
    /// <exception cref="ArgumentException"><see cref="ProcedureParameter.TypeName"/> is set (SQL Server only), or a table parameter is not <see cref="ParameterDirection.Input"/>.</exception>
    protected internal override DbParameter CreateProcedureParameter(ProcedureParameter parameter)
    {
        if (parameter.Value is TableParameterValue tableValue)
        {
            RejectTypeName(parameter);
            RejectNonInputTable(parameter);
            return new SqliteParameter(parameter.Name, tableValue.WriteJson(this));
        }

        RejectTypeName(parameter);
        return base.CreateProcedureParameter(parameter);
    }

    private static void RejectTypeName(ProcedureParameter parameter)
    {
        if (parameter.TypeName is not null)
            throw new ArgumentException(
                $"TypeName is SQL Server only, but '{parameter.Name}' has TypeName = '{parameter.TypeName}'. "
                + "On SQLite pass the rows to ProcedureParameter.Table(name, rows) and read them with json_each.",
                nameof(parameter));
    }

    private static void RejectNonInputTable(ProcedureParameter parameter)
    {
        if (parameter.Direction != ParameterDirection.Input)
            throw new ArgumentException(
                $"A table-valued parameter is input-only, but '{parameter.Name}' has Direction = {parameter.Direction}.",
                nameof(parameter));
    }

    private sealed class NoBulkInsertScope : IDisposable
    {
        internal static readonly NoBulkInsertScope Instance = new();

        private NoBulkInsertScope()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class ForeignKeyScope(SqliteDataContext context, int previousValue) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            context.ExecutePragma($"pragma foreign_keys = {previousValue.ToString(CultureInfo.InvariantCulture)}");
        }
    }
}
