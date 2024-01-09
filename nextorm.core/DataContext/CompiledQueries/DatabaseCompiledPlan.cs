#define PARAM_CONDITION
using System.Data;
using System.Data.Common;

namespace nextorm.core;

public sealed class DbCompiledPlan<TResult> : CompiledQuery<TResult, IDataRecord>, IDbCommandHolder
{
    public readonly string? SqlStmt;
    public readonly bool NoParams;
    public DbCompiledQuery<TResult>? QueryTemplate;
    public DbCompiledPlan(string? sql, Func<Func<IDataRecord, TResult>?> getMap, bool noParams)
        : base(getMap)
    {
        SqlStmt = sql;
        NoParams = noParams;
    }
    public DbCompiledPlan(string? sql, Func<IDataRecord, TResult>? map, bool noParams)
        : base(map)
    {
        SqlStmt = sql;
        NoParams = noParams;
    }
    public void ResetConnection(DbConnection conn)
    {
        if (QueryTemplate?.DbCommand?.Connection == conn)
            QueryTemplate.DbCommand.Connection = null;
    }
    // public DbCommand GetCommand(IEnumerable<Param> @params, SqlDataProvider dataProvider)
    // {
    //     if (DbCommand is null)
    //         DbCommand = dataProvider.CreateCommand(_sql);
    //     else if (!NoParams)
    //         DbCommand.Parameters.Clear();

    //     if (!NoParams)
    //         DbCommand.Parameters.AddRange(@params.Select(it => dataProvider.CreateParam(it.Name, it.Value)).ToArray());

    //     return DbCommand;
    // }
    // public DatabaseCompiledQuery<TResult> CompileQuery(IEnumerable<Param> @params, SqlDataProvider dataProvider, bool singleRow)
    //     => new(GetCommand(@params, dataProvider), MapDelegate, singleRow);
}