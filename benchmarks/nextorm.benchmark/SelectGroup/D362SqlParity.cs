using System.Data.Common;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// SQL-parity dump for the full D362 benchmark matrix (keyed / composite / global / constant /
/// captured / positional + the GroupBy and extrema controls). It prepares each scenario and prints the
/// rendered statement and bound parameters; the PF1 run script diffes the candidate and baseline output.
/// The same source compiles in both trees: <c>SelectGroup</c> is an instance method at the baseline and
/// a <c>NextORM.Extensions</c> extension on the candidate, both reached through the same call syntax.
/// </summary>
public static class D362SqlParity
{
    private const string DbPath = "/tmp/nextorm.d362.parity.db";

    public static int Run()
    {
        D362ControlFixture.Seed(DbPath);

        using var ctx = new SqliteDataContext($"Data Source='{DbPath}'", new DataContextBuilder());

        int captured = 5;

        Print(ctx, "keyed", ctx.From<D362BenchRow>()
            .SelectGroup(x => new { x.Category, Total = x.Score }));
        Print(ctx, "composite", ctx.From<D362BenchRow>()
            .SelectGroup(x => new { x.Category, x.Score }));
        Print(ctx, "global", ctx.From<D362BenchRow>()
            .SelectGroup(x => new { Total = SqlFunctions.Sql.sum(x.Score) }));
        Print(ctx, "constant", ctx.From<D362BenchRow>()
            .SelectGroup(x => 42));
        // Common syntax for both trees: at the baseline the condition was the builder's Having; the
        // packaged SelectGroupHaving on the candidate is the same AND-composed condition (asserted by
        // the AC3 live oracle), so the parity dump compares the same source form.
        Print(ctx, "captured", ctx.From<D362BenchRow>()
            .Having(x => x.Score > captured)
            .SelectGroup(x => new { x.Category, Total = x.Score }));
        Print(ctx, "positional", ctx.From<D362BenchRow>()
            .Having(x => x.Score > SqlFunctions.Parameter<int>(0))
            .SelectGroup(x => new { x.Category, Total = x.Score }));

        Print(ctx, "control_groupby", ctx.From<D362BenchRow>()
            .GroupBy(x => x.Category)
            .Select(x => new { x.Category, Total = x.Score }));
        Print(ctx, "control_extrema_min", ExtremeRowExtensions.SelectWhereMin(
            ctx.From<D362BenchRow>(), x => x.Score, x => new { x.Id, x.Score }));
        Print(ctx, "control_extrema_max", ExtremeRowExtensions.SelectWhereMax(
            ctx.From<D362BenchRow>(), x => x.Score, x => new { x.Id, x.Score }));

        return 0;
    }

    private static void Print<T>(SqliteDataContext ctx, string scenario, QueryCommand<T> command)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(
            command, false, true, CancellationToken.None);
        var sql = (prepared.SqlStmt ?? string.Empty).Replace("\r\n", "\n");
        var parameters = string.Join(
            ";",
            prepared.DbCommand.Parameters.Cast<DbParameter>()
                .Select(p => p.ParameterName + "=" + (p.Value is DBNull ? "NULL" : p.Value)));

        Console.WriteLine("## scenario=" + scenario);
        Console.WriteLine(sql);
        Console.WriteLine("## params=" + parameters);
    }
}
