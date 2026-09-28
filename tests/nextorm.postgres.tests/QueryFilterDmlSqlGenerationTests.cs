using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation of global query filters on mutations (#108, DML part) on PostgreSQL: a filter declared
/// for the target entity reaches the multi-table <c>DELETE ... USING</c> (SQLite cannot render it, so the
/// equivalent assertions live in the PostgreSQL suite). No database connection is opened.
/// </summary>
public class QueryFilterDmlSqlGenerationTests
{
    private const string TenantKey = "tenant_delete_sqlgen";

    [SqlTable("filter_delete_target")]
    public sealed class FilterDeleteTargetEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("filter_delete_right")]
    public sealed class FilterDeleteRightEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [Fact]
    public void DeleteJoin_ShouldFilterTargetTable()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.Properties[TenantKey] = 1;
        ctx.From<FilterDeleteTargetEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        var sql = ctx.From<FilterDeleteTargetEntity>()
            .Join(ctx.From<FilterDeleteRightEntity>(), (l, r) => l.Id == r.Id)
            .ToSql();

        sql.Should().Contain("t1.tenant_id = @");
    }
}
