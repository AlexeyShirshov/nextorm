using System.Data.Common;
using ClickHouse.Driver.ADO;
using ClickHouse.Driver.ADO.Parameters;
using NextORM.Core;

namespace NextORM.ClickHouse;

/// <summary>
/// Data context for ClickHouse. Wraps the <c>ClickHouse.Driver</c> ADO.NET driver for connections
/// and parameters and renders SQL through <c>ClickHouseDialect</c>.
/// </summary>
public class ClickHouseDataContext : DataContext
{
    /// <summary>
    /// Creates a ClickHouse context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>ClickHouseConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public ClickHouseDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    /// <summary>
    /// Creates a ClickHouse context over a caller-supplied connection, which the context does not
    /// dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public ClickHouseDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    /// <summary>Creates a new <c>ClickHouseConnection</c> for <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">The ClickHouse connection string.</param>
    /// <returns>A new, unopened ClickHouse connection.</returns>
    protected override DbConnection CreateDbConnection(string? connectionString)
        => new ClickHouseConnection(connectionString);

    /// <inheritdoc/>
    public override ISqlDialect Dialect => ClickHouseDialect.Instance;

    /// <summary>
    /// Creates a <c>ClickHouseDbParameter</c>. The driver rewrites the ADO-style <c>@name</c>
    /// placeholder to <c>{name:Type}</c> and infers the ClickHouse type from the CLR value, so the
    /// stored name omits the <c>@</c> prefix.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new ClickHouse parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
    {
        // The driver rewrites the ADO-style @name placeholder to {name:Type} and infers the
        // ClickHouse type from the CLR value; the stored name is the one without the @ prefix.
        return new ClickHouseDbParameter
        {
            ParameterName = name.TrimStart('@'),
            Value = value
        };
    }
}
