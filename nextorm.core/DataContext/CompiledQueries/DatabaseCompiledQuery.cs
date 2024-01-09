#define PARAM_CONDITION
using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace nextorm.core;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3881:\"IDisposable\" should be implemented correctly", Justification = "<Pending>")]
public sealed class DbCompiledQuery<TResult> : CompiledQuery<TResult, IDataRecord>
{
    public readonly CommandBehavior Behavior = 0;
    public DbCompiledQuery(DbCommand dbCommand, Func<Func<IDataRecord, TResult>?> getMap)
        : base(getMap)
    {
        DbCommand = dbCommand;
        DbCommandParams = dbCommand.Parameters;
    }
    public DbCompiledQuery(DbCommand dbCommand, Func<IDataRecord, TResult>? mapDelegate, bool singleRow)
        : base(mapDelegate)
    {
        DbCommand = dbCommand;
        DbCommandParams = dbCommand.Parameters;
        if (singleRow)
            Behavior = CommandBehavior.SingleRow;
    }
    public DbCommand DbCommand;
    public DbParameterCollection DbCommandParams;
    public ResultSetEnumerator<TResult>? Enumerator;
    public int LastRowCount;
    public int[] ParamMap;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "<Pending>")]
    public void PrepareDbCommand(object[]? @params, DbContext dataContext, DbConnection conn)
    {
        if (@params is not null)
        {
            var parameters = DbCommandParams;
            if (ParamMap is null)
            {
                ParamMap = new int[@params.Length];
                for (var i = 0; i < @params.Length; i++) ParamMap[i] = -1;
            }
            Debug.Assert(@params.Length == ParamMap.Length, "Arrays must be equal size", "{0} and {1} found", @params.Length, ParamMap.Length);

            for (var i = 0; i < @params.Length; i++)
            {
                var idx = ParamMap[i];

                string? paramName = null;
                if (idx < 0)
                {
                    paramName = i < 5 ? DbContext._params[i] : string.Format("norm_p{0}", i);
                    // parameters[0].Value = @params[i];
                    //sqlCommand.Parameters[paramName].Value = @params[i];
                    //var added = false;
                    idx = parameters.IndexOf(paramName);

                    if (idx >= 0)
                    {
                        ParamMap[i] = idx;
                    }
                }

                if (idx >= 0)
                {
                    parameters[idx].Value = @params[i];
                }
                else
                {
                    ParamMap[i] = parameters.Count;
                    parameters.Add(dataContext.CreateParam(paramName!, @params[i]));
                }
            }
        }

        if (DbCommand.Connection != conn)
            DbCommand.Connection = conn;
    }
}
