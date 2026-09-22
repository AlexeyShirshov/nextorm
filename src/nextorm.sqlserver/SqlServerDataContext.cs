using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Data.SqlClient;
using NextORM.Core;

namespace NextORM.SqlServer;

/// <summary>
/// Data context for Microsoft SQL Server. Wraps the <c>Microsoft.Data.SqlClient</c> driver for
/// connections and parameters and renders SQL through <c>SqlServerDialect</c>.
/// </summary>
public class SqlServerDataContext : DataContext
{
    private static readonly MethodInfo GetValueMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    private static readonly MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private static readonly MethodInfo ChangeTypeMethod = typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!;

    /// <summary>
    /// Creates a SQL Server context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>SqlConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public SqlServerDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    /// <summary>
    /// Creates a SQL Server context over a caller-supplied connection, which the context does not
    /// dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public SqlServerDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    /// <summary>Creates a new <c>SqlConnection</c> for <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>A new, unopened SQL Server connection.</returns>
    protected override DbConnection CreateDbConnection(string? connectionString)
        => new SqlConnection(connectionString);

    /// <inheritdoc/>
    public override ISqlDialect Dialect => SqlServerDialect.Instance;

    /// <summary>
    /// Creates a <c>SqlParameter</c>, mapping a <see langword="null"/> value to <c>DBNull.Value</c>
    /// because SqlClient otherwise omits the parameter entirely.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new SQL Server parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
    {
        // A null value would leave SqlParameter unset, and SqlClient then sends no value at all
        // ("expects the parameter ... which was not supplied"), so nulls must be passed as DBNull.
        return new SqlParameter(name, value ?? DBNull.Value);
    }
    /// <summary>
    /// Maps projected numeric columns by reading the raw value and converting it to the projected CLR
    /// type, because SqlClient typed getters throw when the field type does not match.
    /// </summary>
    /// <param name="column">The projected column being read.</param>
    /// <param name="param">The data-reader expression the accessor is built from.</param>
    /// <returns>An expression that reads and converts the column value.</returns>
    public override Expression MapColumnExpression(SelectExpression column, Expression param)
    {
        var type = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;

        if (!IsNumeric(type))
            return base.MapColumnExpression(column, param);

        // SqlClient typed getters are strict: reading an int column through GetInt64 throws, and a
        // computed numeric expression can be wider than the projected CLR type. Read the value and
        // convert it instead of relying on the reader getter to widen the type.
        var index = Expression.Constant(column.Index);
        var value = Expression.Convert(
            Expression.Call(ChangeTypeMethod, Expression.Call(param, GetValueMethod, index), Expression.Constant(type)),
            column.PropertyType);

        if (column.Nullable)
        {
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Constant(null, column.PropertyType),
                value);
        }

        if (column.DefaultOnNull)
        {
            // A non-nullable *OrDefault scalar: SQL NULL means no row, so return default(T) rather than
            // letting ChangeType(DBNull) throw.
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Default(column.PropertyType),
                value);
        }

        return value;
    }
    private static bool IsNumeric(Type type) => type == typeof(byte) || type == typeof(short)
        || type == typeof(int) || type == typeof(long) || type == typeof(float)
        || type == typeof(double) || type == typeof(decimal);
}
