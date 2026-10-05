using System.ComponentModel.DataAnnotations;
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

    // --- #123: the general MERGE isolates the write target by injecting the active filter into its ON
    // --- search condition, alongside the key predicate. No database connection is opened.

    [SqlTable("qf_dml_merge_target")]
    public sealed class FilterMergeTargetEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    [Fact]
    public void Merge_FullBranches_FilterIsInjectedIntoOn()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.Properties[TenantKey] = 1;
        ctx.From<FilterMergeTargetEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        var sql = ctx.CreateMergeBuilder<FilterMergeTargetEntity>()
            .Using(new FilterMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        var start = sql.IndexOf(" on ", StringComparison.Ordinal);
        var end = sql.IndexOf(" when ", StringComparison.Ordinal);
        var on = start >= 0 && end > start ? sql[start..end] : sql;
        on.Should().Contain("source.id and (target.tenant_id = @", "the key predicate and the filter are joined by AND, not OR");
        on.Should().Contain("target.tenant_id = @");
    }
}
