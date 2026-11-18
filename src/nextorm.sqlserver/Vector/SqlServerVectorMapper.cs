using Microsoft.Data.SqlTypes;
using NextORM.Core;

namespace NextORM.SqlServer;

internal static class SqlServerVectorMapper
{
    internal static Vector? Read(object? value) => value switch
    {
        null or DBNull => null,
        string text => Vector.Parse(text),
        SqlVector<float> { IsNull: true } => null,
        SqlVector<float> vector => new Vector(vector.Memory.ToArray()),
        _ => throw new NotSupportedException(
            $"SQL Server returned vector data as '{value.GetType().FullName}', which the vector materializer does not support.")
    };
}
