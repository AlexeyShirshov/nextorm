using System.Data;
using System.Reflection;
namespace NextORM.Core;

public static class QueryCommandExtensions
{
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, CancellationToken cancellationToken = default)
        => queryCommand.PrepareFromSql(sql, null, PrepareFromSqlMode.None, cancellationToken);
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, CancellationToken cancellationToken = default)
        => queryCommand.PrepareFromSql(sql, @params, PrepareFromSqlMode.None, cancellationToken);
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, PrepareFromSqlMode mode, CancellationToken cancellationToken = default)
        => queryCommand.PrepareFromSql(sql, null, mode, cancellationToken);
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, PrepareFromSqlMode mode, CancellationToken cancellationToken = default)
        => queryCommand.DataContext!.GetPreparedQueryCommand(WithSql(queryCommand, sql, @params), !mode.HasFlag(PrepareFromSqlMode.Streaming), mode.HasFlag(PrepareFromSqlMode.StoreInCache), cancellationToken);
    public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql) => WithSql(queryCommand, sql, null);
    public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params)
    {
        queryCommand.CustomData = new RawSqlOverride
        {
            ManualSql = sql,
            MakeParams = () =>
            {
                List<Parameter> ps = [];
                if (@params is not null)
                {
                    var t = @params.GetType();
                    var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                    for (var (i, cnt) = (0, props.Length); i < cnt; i++)
                    {
                        var prop = props[i];
                        ps.Add(new Parameter(prop.Name, prop.GetValue(@params)));
                    }
                }
                return ps;
            }
        };
        return queryCommand;
    }
}