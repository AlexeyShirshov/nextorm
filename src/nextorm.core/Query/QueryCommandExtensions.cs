using System.Data;
using System.Reflection;
namespace NextORM.Core;

/// <summary>
/// Extension methods that run a <see cref="QueryCommand{TResult}"/> against raw SQL instead of the
/// generated query.
/// </summary>
public static class QueryCommandExtensions
{
    /// <summary>Prepares the command to read the given SQL, materializing the result as <typeparamref name="TResult"/>.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to run.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <param name="cancellationToken">A token that cancels preparation.</param>
    /// <returns>The prepared command.</returns>
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, CancellationToken cancellationToken = default)
        => queryCommand.PrepareFromSql(sql, null, PrepareFromSqlMode.None, cancellationToken);
    /// <summary>Prepares the command to read the given SQL with parameters supplied as the public properties of <paramref name="params"/>.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to run.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <param name="params">An object whose public properties become the SQL parameters, or <c>null</c>.</param>
    /// <param name="cancellationToken">A token that cancels preparation.</param>
    /// <returns>The prepared command.</returns>
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, CancellationToken cancellationToken = default)
        => queryCommand.PrepareFromSql(sql, @params, PrepareFromSqlMode.None, cancellationToken);
    /// <summary>Prepares the command to read the given SQL using the given caching/streaming <paramref name="mode"/>.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to run.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <param name="mode">How the prepared command is cached and whether it streams.</param>
    /// <param name="cancellationToken">A token that cancels preparation.</param>
    /// <returns>The prepared command.</returns>
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, PrepareFromSqlMode mode, CancellationToken cancellationToken = default)
        => queryCommand.PrepareFromSql(sql, null, mode, cancellationToken);
    /// <summary>Prepares the command to read the given SQL with parameters and an explicit caching/streaming mode.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to run.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <param name="params">An object whose public properties become the SQL parameters, or <c>null</c>.</param>
    /// <param name="mode">How the prepared command is cached and whether it streams.</param>
    /// <param name="cancellationToken">A token that cancels preparation.</param>
    /// <returns>The prepared command.</returns>
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, PrepareFromSqlMode mode, CancellationToken cancellationToken = default)
        => queryCommand.DataContext!.GetPreparedQueryCommand(WithSql(queryCommand, sql, @params), !mode.HasFlag(PrepareFromSqlMode.Streaming), mode.HasFlag(PrepareFromSqlMode.StoreInCache), cancellationToken);
    /// <summary>Overrides the command's generated SQL with <paramref name="sql"/> and no parameters.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to override.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <returns>The same command, with the raw-SQL override attached.</returns>
    public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql) => WithSql(queryCommand, sql, null);
    /// <summary>
    /// Overrides the command's generated SQL with <paramref name="sql"/>, taking the SQL parameters from
    /// the public properties of <paramref name="params"/>.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to override.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <param name="params">An object whose public properties become the SQL parameters, or <c>null</c>.</param>
    /// <returns>The same command, with the raw-SQL override attached.</returns>
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