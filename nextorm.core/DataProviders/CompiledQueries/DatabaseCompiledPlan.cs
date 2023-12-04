#define PARAM_CONDITION
using System.Data;

namespace nextorm.core;

public class DatabaseCompiledPlan<TResult> : CompiledQuery<TResult, IDataRecord>
{
    internal readonly string _sql;
    public readonly bool NoParams;
    public SqlCacheEntry? CacheEntry { get; internal set; }

    public DatabaseCompiledPlan(string sql, Func<Func<IDataRecord, TResult>> getMap, bool noParams)
        : base(getMap)
    {
        _sql = sql;
        NoParams = noParams;
    }
    public DatabaseCompiledPlan(string sql, Func<IDataRecord, TResult> map, bool noParams)
        : base(map)
    {
        _sql = sql;
        NoParams = noParams;
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