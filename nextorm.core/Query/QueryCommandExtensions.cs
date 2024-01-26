using System.Data;
using System.Reflection;
namespace nextorm.core;

public static class QueryCommandExtensions
{
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, CancellationToken cancellationToken = default) => queryCommand.PrepareFromSql(sql, null, true, cancellationToken);
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, CancellationToken cancellationToken = default) => queryCommand.PrepareFromSql(sql, @params, true, cancellationToken);
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, bool nonStreamUsing, CancellationToken cancellationToken = default) => queryCommand.PrepareFromSql(sql, null, nonStreamUsing, cancellationToken);
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, bool nonStreamUsing, CancellationToken cancellationToken = default) => queryCommand.PrepareFromSql(sql, @params, nonStreamUsing, false, cancellationToken);
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken = default)
        => queryCommand.DataContext.GetPreparedQueryCommand(WithSql(queryCommand, sql, @params), nonStreamUsing, storeInCache, cancellationToken);
    public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql) => WithSql(queryCommand, sql, null);
    public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params)
    {
        queryCommand.CustomData = new DbQueryCommandExtension
        {
            ManualSql = sql,
            MakeParams = () =>
            {
                List<Param> ps = [];
                if (@params is not null)
                {
                    var t = @params.GetType();
                    var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                    for (var (i, cnt) = (0, props.Length); i < cnt; i++)
                    {
                        var prop = props[i];
                        ps.Add(new Param(prop.Name, prop.GetValue(@params)));
                    }
                }
                return ps;
            }
        };
        return queryCommand;
    }
}