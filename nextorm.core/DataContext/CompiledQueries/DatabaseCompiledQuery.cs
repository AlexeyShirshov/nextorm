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
        CommandText = dbCommand.CommandText;
        //DbCommandParams = dbCommand.Parameters;
    }
    public DbCompiledQuery(DbCommand dbCommand, Func<IDataRecord, TResult>? mapDelegate, bool singleRow)
        : base(mapDelegate)
    {
        DbCommand = dbCommand;
        CommandText = dbCommand.CommandText;
        //DbCommandParams = dbCommand.Parameters;
        if (singleRow)
            Behavior = CommandBehavior.SingleRow;
    }
    public DbCommand DbCommand;
    private readonly string CommandText;

    //public DbParameterCollection DbCommandParams;
    public ResultSetEnumerator<TResult>? Enumerator;
    public int LastRowCount;
    public int[] ParamMap;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "<Pending>")]
    public DbCommand GetDbCommand(object[]? @params, DbContext dataContext, DbConnection conn)
    {
        var cmd = DbCommand;
        var parameters = cmd.Parameters;

        if (@params is not null)
        {
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
                    if (parameters[idx].Value != @params[i])
                    {
                        // if (cmd == DbCommand)
                        // {
                        // cmd = conn.CreateCommand();
                        // cmd.CommandText = CommandText;
                        // foreach (var p in parameters) cmd.Parameters.Add(p);
                        // parameters = cmd.Parameters;
                        //}
                        parameters[idx].Value = @params[i];
                    }
                }
                else
                {
                    // if (cmd == DbCommand)
                    // {
                    //     cmd = conn.CreateCommand();
                    //     cmd.CommandText = CommandText;
                    //     foreach (var p in parameters) cmd.Parameters.Add(p);
                    //     parameters = cmd.Parameters;
                    // }

                    ParamMap[i] = parameters.Count;
                    parameters.Add(dataContext.CreateParam(paramName!, @params[i]));
                }
            }
        }

        if (cmd.Connection != conn)
            cmd.Connection = conn;

        return cmd;
    }
}
