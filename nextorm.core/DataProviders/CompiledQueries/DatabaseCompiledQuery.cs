#define PARAM_CONDITION
using System.Data;
using System.Data.Common;

namespace nextorm.core;

public class DatabaseCompiledQuery<TResult> : CompiledQuery<TResult, IDataRecord>//, IReplaceParam
{
    public DbCommand DbCommand;
    public readonly System.Data.CommandBehavior Behavior = System.Data.CommandBehavior.SingleResult;
    public DatabaseCompiledQuery(DbCommand dbCommand, Func<Func<IDataRecord, TResult>> getMap)
        : base(getMap)
    {
        DbCommand = dbCommand;
    }
    public DatabaseCompiledQuery(DbCommand dbCommand, Func<IDataRecord, TResult> mapDelegate, bool singleRow)
        : base(mapDelegate)
    {
        DbCommand = dbCommand;
        if (singleRow)
            Behavior = System.Data.CommandBehavior.SingleResult | System.Data.CommandBehavior.SingleRow;
    }

    // public void ReplaceParams(object[] @params, IDataProvider dataProvider)
    // {
    //     //if (dataProvider is SqlDataProvider sqlDataProvider)
    //     for (var i = 0; i < @params.Length; i++)
    //     {
    //         var paramName = string.Format("norm_p{0}", i);
    //         foreach (DbParameter p in DbCommand.Parameters)
    //         {
    //             if (p.ParameterName == paramName)
    //             {
    //                 p.Value = @params[i];
    //                 break;
    //             }
    //         }
    //     }
    // }
}
