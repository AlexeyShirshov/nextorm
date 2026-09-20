using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Data.SqlClient;
using NextORM.Core;

namespace NextORM.SqlServer;

public class SqlServerDataContext : DataContext
{
    private static readonly MethodInfo GetValueMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    private static readonly MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private static readonly MethodInfo ChangeTypeMethod = typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!;

    public SqlServerDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    public SqlServerDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    protected override DbConnection CreateDbConnection(string? connectionString)
        => new SqlConnection(connectionString);

    public override ISqlDialect Dialect => SqlServerDialect.Instance;

    public override DbParameter CreateParam(string name, object? value)
    {
        // A null value would leave SqlParameter unset, and SqlClient then sends no value at all
        // ("expects the parameter ... which was not supplied"), so nulls must be passed as DBNull.
        return new SqlParameter(name, value ?? DBNull.Value);
    }
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
