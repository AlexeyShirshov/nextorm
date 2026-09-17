using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace nextorm.core;

public sealed class DbPreparedQueryCommand<TResult> : PreparedQueryCommand<TResult, IDataRecord>, IDbCommandHolder
{
    public readonly CommandBehavior Behavior = 0;
    public readonly DbCommand DbCommand;
    private DbConnection? DbCommandConnection;
    public readonly DbParameterCollection DbCommandParams;
    public ResultSetEnumerator<TResult>? Enumerator;
    public int LastRowCount;
    public int[]? ParamMap;
    public readonly string? SqlStmt;
    public readonly bool NoParams;
    public readonly bool NeedsParamRefresh;
    public DbPreparedQueryCommand(DbCommand dbCommand, Func<IDataRecord, TResult>? mapDelegate, bool singleRow, string? sql, bool noParams, bool needsParamRefresh)
        : base(mapDelegate)
    {
        DbCommand = dbCommand;
        DbCommandConnection = dbCommand.Connection;
        DbCommandParams = dbCommand.Parameters;
        if (singleRow)
            Behavior = CommandBehavior.SingleRow;

        SqlStmt = sql;
        NoParams = noParams;
        NeedsParamRefresh = needsParamRefresh;
    }
    // private readonly string CommandText;
    //public DbParameterCollection DbCommandParams;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "Both branches are reachable: @params may be null at any call site, so the ReadOnlySpan conversion is not dead code; the analyzer cannot model the span conversion.")]
    public DbCommand GetDbCommand(object[]? @params, DbContext dataContext, DbConnection conn)
        => GetDbCommand(@params is null ? ReadOnlySpan<object?>.Empty : @params, dataContext, conn);

    public DbCommand GetDbCommand(ReadOnlySpan<object?> @params, DbContext dataContext, DbConnection conn)
    {
        var cmd = DbCommand;
        var parameters = DbCommandParams;//cmd.Parameters;

        if (!@params.IsEmpty)
        {
            var pLength = @params.Length;
            if (ParamMap is null)
            {
                ParamMap = new int[pLength];
                for (var i = 0; i < pLength; i++) ParamMap[i] = -1;
            }
            Debug.Assert(pLength == ParamMap.Length, "Arrays must be equal size", "{0} and {1} found", pLength, ParamMap.Length);

            for (var i = 0; i < pLength; i++)
            {
                var idx = ParamMap[i];

                string? paramName = null;
                if (idx < 0)
                {
                    paramName = NormParam.GetName(i);
                    // parameters.IndexOf is a provider-side linear scan, but ParamMap caches the
                    // result below, so it runs at most once per parameter per compiled command.
                    idx = parameters.IndexOf(paramName);

                    if (idx >= 0)
                    {
                        ParamMap[i] = idx;
                    }
                }

                if (idx >= 0)
                {
                    // Normalize null to DBNull so providers that reject null parameter values
                    // (Microsoft.Data.Sqlite) work and the equality check stays stable.
                    var newValue = @params[i] ?? DBNull.Value;
                    if (parameters[idx].Value != newValue)
                    {
                        // if (cmd == DbCommand)
                        // {
                        // cmd = conn.CreateCommand();
                        // cmd.CommandText = CommandText;
                        // foreach (var p in parameters) cmd.Parameters.Add(p);
                        // parameters = cmd.Parameters;
                        //}
                        parameters[idx].Value = newValue;
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

        if (DbCommandConnection != conn)
        {
            DbCommandConnection = conn;
            cmd.Connection = conn;
        }

        return cmd;
    }
    public void ResetConnection(DbConnection conn, IDataContext dbContext)
    {
        if (DbCommand?.Connection == conn)
            DbCommand.Connection = null;

        if (Enumerator?.DbContext == dbContext)
            Enumerator.DbContext = null;

        DbCommandConnection = null;
    }
}
