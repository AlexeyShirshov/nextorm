using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// Renders the SQL of a nextorm query command without touching a database, the same way the SQLite
/// SQL-generation tests do.
/// </summary>
internal static class NextOrmSql
{
    public static string Of<TResult>(IDataContext context, QueryCommand<TResult> command)
    {
        var prepared = (DbPreparedQueryCommand<TResult>)
            context.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        return prepared.DbCommand.CommandText.Replace("\r\n", "\n");
    }
}
