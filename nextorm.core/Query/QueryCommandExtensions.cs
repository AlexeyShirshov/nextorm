using System.Data;
using System.Diagnostics;
namespace nextorm.core;

public static class QueryCommandExtensions
{
    public static DbCompiledQuery<TResult>? GetCompiledQuery<TResult>(this QueryCommand<TResult> queryCommand)
    {
        return queryCommand.GetCompiledQuery<IDataRecord>() as DbCompiledQuery<TResult>;
    }
}