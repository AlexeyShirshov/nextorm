using System.Data;

namespace NextORM.Core;

/// <summary>
/// Describes a single parameter of a raw command or stored procedure: its name, value and the ADO.NET
/// parameter options (<see cref="ParameterDirection"/>, <see cref="DbType"/>, <see cref="Size"/>,
/// <see cref="TypeName"/>). The same shape serves raw parameterised SQL (<c>ExecuteRaw</c>) and stored
/// procedures (<c>ExecuteProcedure</c>).
/// </summary>
/// <param name="Name">The parameter name, without the provider's prefix (for example the SQL Server <c>@</c>).</param>
/// <param name="Value">The value bound to the parameter, or <see langword="null"/> for a SQL <c>NULL</c>.</param>
/// <param name="Direction">The parameter direction. Defaults to <see cref="ParameterDirection.Input"/>.</param>
/// <param name="DbType">An explicit provider-independent type, or <see langword="null"/> to let the provider infer it from <paramref name="Value"/>.</param>
/// <param name="Size">The parameter size in bytes/characters, or <see langword="null"/> for the provider default.</param>
/// <param name="TypeName">The provider type name of a structured (table-valued) parameter, or <see langword="null"/> for an ordinary parameter.</param>
public readonly record struct ProcedureParameter(
    string Name,
    object? Value,
    ParameterDirection Direction = ParameterDirection.Input,
    DbType? DbType = null,
    int? Size = null,
    string? TypeName = null)
{
    /// <summary>
    /// Creates an input table-valued parameter from a sequence of rows, for a provider that emulates a
    /// table parameter (PostgreSQL and ClickHouse typed arrays; MySQL/MariaDB and SQLite JSON). SQL
    /// Server has no emulation — it binds a native user-defined table type — so it requires the
    /// <c>typeName</c> argument and must use the overload that takes it. The provider decides how the
    /// rows are bound; see each provider's documentation.
    /// </summary>
    /// <typeparam name="T">The CLR type of each row.</typeparam>
    /// <param name="name">The parameter name, without the provider's prefix.</param>
    /// <param name="rows">The rows to bind. An empty sequence binds an empty set.</param>
    /// <returns>An input table-valued parameter descriptor.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is <see langword="null"/>.</exception>
    public static ProcedureParameter Table<T>(string name, IEnumerable<T> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rows);

        return new ProcedureParameter(name, new TableValuedParameter<T>(rows));
    }

    /// <summary>
    /// Creates an input table-valued parameter for a provider with a named table type (SQL Server
    /// user-defined table type); the other providers reject a type name because they emulate the table
    /// parameter with a typed array or a JSON document and have no named type to bind.
    /// </summary>
    /// <typeparam name="T">The CLR type of each row.</typeparam>
    /// <param name="name">The parameter name, without the provider's prefix.</param>
    /// <param name="typeName">The user-defined table type name (SQL Server only).</param>
    /// <param name="rows">The rows to bind. An empty sequence binds an empty set.</param>
    /// <returns>An input table-valued parameter descriptor.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="typeName"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is <see langword="null"/>.</exception>
    public static ProcedureParameter Table<T>(string name, string typeName, IEnumerable<T> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(rows);

        return new ProcedureParameter(name, new TableValuedParameter<T>(rows), TypeName: typeName);
    }
}

/// <summary>
/// A snapshot of one output or input/output parameter after a raw command has run. The live
/// <c>DbParameter</c> is never exposed: the value is copied when the owning <see cref="ProcedureResult"/>
/// reads its outputs.
/// </summary>
/// <param name="Name">The parameter name as reported by the provider.</param>
/// <param name="Value">The value returned by the provider, or <see langword="null"/> for <c>DBNull</c>.</param>
/// <param name="Direction">The parameter direction (<see cref="ParameterDirection.Output"/> or <see cref="ParameterDirection.InputOutput"/>).</param>
public readonly record struct ProcedureOutputParameter(string Name, object? Value, ParameterDirection Direction);
