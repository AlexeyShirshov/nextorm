using System.Data.Common;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using nextorm.core;

namespace nextorm.sqlserver;

public class SqlServerClient(string connectionString, DbContextBuilder optionsBuilder) : DbContext(optionsBuilder)
{
    private readonly string _connectionString = connectionString;
    public override DbConnection CreateConnection()
    {
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            if (LogSensetiveData)
                Logger.LogDebug("Creating connection with {connStr}", _connectionString);
            else
                Logger.LogDebug("Creating connection");
        }

        return new SqlConnection(_connectionString);
    }
    public override DbCommand CreateCommand(string sql)
    {
        return new SqlCommand(sql);
    }
    public override DbParameter CreateParam(string name, object? value)
    {
        return new SqlParameter(name, value);
    }
    public override string MakeParam(string name)
    {
        return $"@{name}";
    }
    protected override void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        sqlBuilder.Append("offset ").Append(paging.Offset).Append(" rows");

        if (paging.Limit > 0)
            sqlBuilder.AppendLine().Append("fetch next ").Append(paging.Limit).Append(" rows only");
    }
    protected override bool RequireSorting(QueryCommand queryCommand)
    {
        return !queryCommand.Paging.IsEmpty;
    }
    protected override string EmptySorting()
    {
        return "(select null as anyorder)";
    }
}
