using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL generation and plan-cache behaviour for global query filters (#67) on SQLite: the filter
/// predicate reaches the <c>WHERE</c>, the per-context tenant value is bound as a parameter (never
/// inlined) and two contexts with different values share one cached plan. The SQL-only test never
/// opens a database; the two-context test uses a temp file database.
/// </summary>
public class QueryFilterSqlGenerationTests
{
    private const string TenantKey = "tenant";

    [SqlTable("filter_tenant_entity")]
    public sealed class FilterTenantEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("filter_tenant_sql_entity")]
    public sealed class FilterTenantSqlEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("filter_join_left_entity")]
    public sealed class FilterJoinLeftEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("filter_join_right_entity")]
    public sealed class FilterJoinRightEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    private const string JoinTenantKey = "tenant_join";

    private static Expression<Func<FilterTenantEntity, IDataContext, bool>> TenantFilter()
        => (e, ctx) => e.TenantId == (int)ctx.Properties[TenantKey];

    private static Expression<Func<FilterJoinRightEntity, IDataContext, bool>> JoinTenantFilter()
        => (e, ctx) => e.TenantId == (int)ctx.Properties[JoinTenantKey];

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    [Fact]
    public void FilteredEntity_Sql_ShouldContainPredicateAndBoundParameter()
    {
        // A dedicated entity type and property key keep this SQL-shape test independent of the
        // two-context execution test: the context read folds through the process-wide compiled-value
        // cache, whose key does not include the context instance (see the execution test below).
        using var ctx = SqliteTestContext.Create();
        ctx.Properties["tenant_sqlgen"] = 7;
        ctx.From<FilterTenantSqlEntity>(b => b.HasQueryFilter((e, c) => e.TenantId == (int)c.Properties["tenant_sqlgen"]));

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            ctx.From<FilterTenantSqlEntity>().Select(x => x.Id), false, false, CancellationToken.None);

        var sql = Normalize(prepared.DbCommand.CommandText);
        sql.Should().Contain("tenant_id = $");
        sql.Should().NotContain("= 7");

        var parameters = prepared.DbCommand.Parameters.Cast<DbParameter>().ToArray();
        parameters.Should().ContainSingle();
        parameters[0].Value.Should().Be(7);
    }

    [Fact]
    public void TwoContexts_DifferentTenant_ShouldEachReturnOwnRows_OnASharedPlan()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-queryfilter-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table filter_tenant_entity (id integer primary key, tenant_id integer not null);" +
                "insert into filter_tenant_entity (id, tenant_id) values (10, 1), (11, 1), (20, 2);";
            cmd.ExecuteNonQuery();
        }

        try
        {
            using var ctx1 = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
            using var ctx2 = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
            ctx1.EnsureConnectionOpen();
            ctx2.EnsureConnectionOpen();

            ctx1.Properties[TenantKey] = 1;
            ctx2.Properties[TenantKey] = 2;
            ctx1.From<FilterTenantEntity>(b => b.HasQueryFilter(TenantFilter()));

            var rows1 = ctx1.From<FilterTenantEntity>().Select(x => x.Id).ToList();
            var rows2 = ctx2.From<FilterTenantEntity>().Select(x => x.Id).ToList();

            rows1.Should().BeEquivalentTo([10, 11], "the first context must see only tenant 1");
            rows2.Should().BeEquivalentTo([20], "the second context must see only tenant 2");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RePreparedJoin_ShouldNotDoubleInjectFilter()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.Properties[JoinTenantKey] = 1;
        ctx.From<FilterJoinRightEntity>(b => b.HasQueryFilter(JoinTenantFilter()));

        var cmd = ctx.From<FilterJoinLeftEntity>()
            .Join(ctx.From<FilterJoinRightEntity>(), (l, r) => l.Id == r.Id)
            .Select(p => p.Item2.TenantId);

        var first = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        CountOccurrences(Normalize(first.DbCommand.CommandText), "tenant_id = ").Should().Be(1);

        cmd.ResetPreparation();

        var second = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        CountOccurrences(Normalize(second.DbCommand.CommandText), "tenant_id = ").Should().Be(1);
    }

    [Fact]
    public void DerivedJoin_ShouldApplyFilterOnceInSubquery_NotInOnCondition()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.Properties[JoinTenantKey] = 1;
        ctx.From<FilterJoinRightEntity>(b => b.HasQueryFilter(JoinTenantFilter()));

        var right = ctx.From<FilterJoinRightEntity>().ToCommand();
        var cmd = ctx.From<FilterJoinLeftEntity>()
            .Join(right, (l, r) => l.Id == r.Id)
            .Select(p => p.Item2.TenantId);

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        CountOccurrences(Normalize(prepared.DbCommand.CommandText), "tenant_id = ").Should().Be(1);
    }

    [Fact]
    public void CachedPlanClone_ShouldNotShareJoinInstance()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.Properties[JoinTenantKey] = 1;
        ctx.From<FilterJoinRightEntity>(b => b.HasQueryFilter(JoinTenantFilter()));

        var cmd = ctx.From<FilterJoinLeftEntity>()
            .Join(ctx.From<FilterJoinRightEntity>(), (l, r) => l.Id == r.Id)
            .Select(p => p.Item2.TenantId);

        cmd.PrepareCommand(false, TestContext.Current.CancellationToken);

        var clone = cmd.CloneForCache();

        clone.Joins.Should().NotBeSameAs(cmd.Joins);
        clone.Joins![0].Should().NotBeSameAs(cmd.Joins![0]);
    }
}
