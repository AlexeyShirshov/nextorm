using System.Data.Common;
using NextORM.Core;

namespace NextORM.AliasTests;

/// <summary>Records the SQL text of every command the context executes, in order.</summary>
internal sealed class SqlRecordingInterceptor : IQueryInterceptor
{
    public List<string> Statements { get; } = [];

    public void CommandExecuting(CommandEventData eventData, DbCommand command)
        => Statements.Add(eventData.Sql ?? command.CommandText);
}
