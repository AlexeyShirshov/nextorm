using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #296: a nullable null literal (<c>(int?)null</c>, and the compiler-produced nullable
/// <c>default</c>) renders <c>is null</c> / <c>is not null</c> for both operators instead of
/// <c>= null</c> / <c>!= null</c>. The fact owns a three-row table — id 1 has a NULL value, ids 2 and
/// 3 do not — so it is independent of the seeded <c>complex_entity</c> fixture, and it runs once per
/// concrete suite: <c>SqliteIntegrationTests</c>, <c>PostgresIntegrationTests</c>,
/// <c>SqlServerIntegrationTests</c> and <c>MySqlIntegrationTests</c>. MariaDB and ClickHouse have no
/// shared-suite subclass; their coverage is the SQL-generation matrix.
/// </summary>
public abstract partial class CommonTestSuite
{
    [SqlTable("t296_nullable_compare")]
    public interface IT296NullableCompare
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("nullableint")]
        int? Nullable { get; set; }
    }

    [Fact]
    public async Task NullableLiteralComparisonsReturnExpectedRows()
    {
        var ctx = _sut.DataProvider;

        ExecuteNullableFixture(ctx, "drop table if exists t296_nullable_compare");
        ExecuteNullableFixture(
            ctx,
            "create table t296_nullable_compare (id bigint not null primary key, nullableint int null)");
        ExecuteNullableFixture(
            ctx,
            "insert into t296_nullable_compare (id, nullableint) values (1, null), (2, 10), (3, 20)");

        try
        {
            // `== (int?)null`: only the NULL row matches, and the captured SQL is a presence test.
            var equalitySql = NullableComparisonSql(ctx, ctx.From<IT296NullableCompare>()
                .Where(x => x.Nullable == (int?)null)
                .Select(x => x.Id));

            equalitySql.ToLowerInvariant().Should().Contain("is null").And.NotContain("is not null");

            var equalityRows = await ctx.From<IT296NullableCompare>()
                .Where(x => x.Nullable == (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();

            LogNullableScenario("nullable == (int?)null", equalitySql, $"[{string.Join(", ", equalityRows)}]");
            equalityRows.Should().Equal(1L);

            // `!= (int?)null`: the complementary presence test.
            var inequalitySql = NullableComparisonSql(ctx, ctx.From<IT296NullableCompare>()
                .Where(x => x.Nullable != (int?)null)
                .Select(x => x.Id));

            inequalitySql.ToLowerInvariant().Should().Contain("is not null");

            var inequalityRows = await ctx.From<IT296NullableCompare>()
                .Where(x => x.Nullable != (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();

            LogNullableScenario("nullable != (int?)null", inequalitySql, $"[{string.Join(", ", inequalityRows)}]");
            inequalityRows.Should().Equal(2L, 3L);

            // A derived operand keeps its arithmetic and agrees with the plain column comparison.
            var derivedEqualitySql = NullableComparisonSql(ctx, ctx.From<IT296NullableCompare>()
                .Where(x => (x.Nullable + 1) == (int?)null)
                .Select(x => x.Id));

            var derivedEqualityRows = await ctx.From<IT296NullableCompare>()
                .Where(x => (x.Nullable + 1) == (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();

            LogNullableScenario("(nullable + 1) == (int?)null", derivedEqualitySql, $"[{string.Join(", ", derivedEqualityRows)}]");
            derivedEqualityRows.Should().Equal(1L);

            var derivedInequalitySql = NullableComparisonSql(ctx, ctx.From<IT296NullableCompare>()
                .Where(x => (x.Nullable + 1) != (int?)null)
                .Select(x => x.Id));

            var derivedInequalityRows = await ctx.From<IT296NullableCompare>()
                .Where(x => (x.Nullable + 1) != (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();

            LogNullableScenario("(nullable + 1) != (int?)null", derivedInequalitySql, $"[{string.Join(", ", derivedInequalityRows)}]");
            derivedInequalityRows.Should().Equal(2L, 3L);

            // A projected comparison goes through the independent predicate path.
            var projectionSql = NullableComparisonSql(ctx, ctx.From<IT296NullableCompare>()
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    IsNull = x.Nullable == (int?)null,
                    IsNotNull = x.Nullable != (int?)null,
                }));

            var projected = await ctx.From<IT296NullableCompare>()
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    IsNull = x.Nullable == (int?)null,
                    IsNotNull = x.Nullable != (int?)null,
                })
                .ToListAsync();

            LogNullableScenario(
                "projection nullable == / != (int?)null",
                projectionSql,
                $"[{string.Join(", ", projected.Select(x => $"{{id={x.Id}, isNull={x.IsNull}, isNotNull={x.IsNotNull}}}"))}]");

            projected.Select(x => x.Id).Should().Equal(1L, 2L, 3L);
            projected.Select(x => x.IsNull).Should().Equal(true, false, false);
            projected.Select(x => x.IsNotNull).Should().Equal(false, true, true);
        }
        finally
        {
            ExecuteNullableFixture(ctx, "drop table if exists t296_nullable_compare");
        }
    }

    /// <summary>Emits one evidence line per scenario: provider, identity, executed SQL and observed rows.</summary>
    private void LogNullableScenario(string scenario, string sql, string rows)
        => TestContext.Current.TestOutputHelper?.WriteLine(
            $"provider={Provider.Name} test=CommonTestSuite.NullableLiteralComparisonsReturnExpectedRows " +
            $"scenario={scenario} sql={sql.Replace('\n', ' ')} rows={rows}");

    /// <summary>Renders the SQL a fresh command is prepared with, without executing it.</summary>
    private static string NullableComparisonSql<TResult>(IDataContext ctx, QueryCommand<TResult> command)
        => ((DbPreparedQueryCommand<TResult>)((DataContext)ctx)
                .GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static void ExecuteNullableFixture(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
