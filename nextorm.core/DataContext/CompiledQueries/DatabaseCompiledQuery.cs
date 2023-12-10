#define PARAM_CONDITION
using System.Data;
using System.Data.Common;

namespace nextorm.core;

public class DbCompiledQuery<TResult> : CompiledQuery<TResult, IDataRecord>
{
    public readonly CommandBehavior Behavior = CommandBehavior.SingleResult;
    public DbCompiledQuery(DbCommand dbCommand, Func<Func<IDataRecord, TResult>?> getMap)
        : base(getMap)
    {
        DbCommand = dbCommand;
    }
    public DbCompiledQuery(DbCommand dbCommand, Func<IDataRecord, TResult>? mapDelegate, bool singleRow)
        : base(mapDelegate)
    {
        DbCommand = dbCommand;
        if (singleRow)
            Behavior = CommandBehavior.SingleResult | CommandBehavior.SingleRow;
    }
    public DbCommand DbCommand;
    public ResultSetEnumerator<TResult>? Enumerator;
    public int LastRowCount;
}
