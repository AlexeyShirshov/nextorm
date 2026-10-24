using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D19 (F19 boundary): the catalog's §4 out-of-scope external-engine table functions stay
/// outside the CLR surface and outside the dialect's table-function contract, while the §1 delivered
/// table functions keep rendering. The frozen §3 inventory contains no table-function member, so the
/// §4 boundary excludes nothing that §3 requires. No connection is opened.
/// </summary>
public class T286bF19TableFunctionBoundarySqlGenerationTests
{
    /// <summary>The §4 ClickHouse row of <c>docs/specs/roadmap/sql-function-coverage-gap.md</c>.</summary>
    private static readonly string[] ExternalEngineTableFunctions =
    [
        "mysql", "postgresql", "sqlite", "jdbc", "odbc", "mongodb", "redis",
        "hdfs", "s3Cluster", "azureBlobStorage", "deltaLake", "iceberg", "hudi", "paimon",
        "bigquery", "gcs", "ytsaurus", "arrowFlight", "prometheusQuery",
        "input", "view", "format", "null", "executable", "primes", "merge", "dictionary",
        "eval", "loop", "fuzzJSON", "fuzzQuery",
    ];

    /// <summary>The §1 table functions: the only names <see cref="ClickHouseDialect"/> accepts.</summary>
    private static readonly string[] DeliveredTableFunctions =
    [
        "numbers", "numbers_mt", "zeros", "zeros_mt", "generateRandom", "values",
        "url", "s3", "file", "remote", "remoteSecure", "cluster", "clusterAllReplicas",
    ];

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void ExternalEngineTableFunctions_ShouldNotBeSupportedByTheDialect()
    {
        using (new AssertionScope())
        {
            foreach (var name in ExternalEngineTableFunctions)
                ClickHouseDialect.Instance.SupportsTableFunction(name)
                    .Should().BeFalse("'{0}' is the §4 external-engine boundary and is not modelled", name);
        }
    }

    [Fact]
    public void ExternalEngineTableFunctions_ShouldHaveNoBuiltInTableFunctionMember()
    {
        var builtIn = typeof(ClickHouseFunctions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(method => method.GetCustomAttribute<SqlTableFunctionAttribute>()?.Name)
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct()
            .ToArray();

        builtIn.Should().BeEquivalentTo(DeliveredTableFunctions, "the §1 table-function row is the whole built-in surface");

        using (new AssertionScope())
        {
            foreach (var name in ExternalEngineTableFunctions)
                builtIn.Should().NotContain(name, "'{0}' is the §4 external-engine boundary", name);
        }
    }

    [Fact]
    public void DeliveredTableFunctions_ShouldStillRenderFromAFromSource()
    {
        using var ctx = ClickHouseTestContext.Create();
        var count = 3L;

        SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.ClickHouse.numbers(count))
                .Select(r => new { r.Value }))
            .Should().Contain("from numbers(@count)");

        SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.ClickHouse.zeros(count))
                .Select(r => new { r.Value }))
            .Should().Contain("from zeros(@count)");
    }
}
