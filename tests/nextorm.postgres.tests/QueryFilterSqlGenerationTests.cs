using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation of global query filters on PostgreSQL (#108): the filter reaches the SELECT
/// <c>WHERE</c>, a selective ignore drops only the named predicate, the target filter is ANDed to the
/// UPDATE/DELETE <c>WHERE</c>, an INSERT target is never filtered and an INSERT ... SELECT filters only
/// its source. No database connection is opened.
/// </summary>
public class QueryFilterSqlGenerationTests
{
    private const string TenantKey = "qf_tenant_pg_sqlgen";

    [SqlTable("qf_select_entity")]
    public sealed class QfSelectEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }

    [SqlTable("qf_insert_source")]
    public sealed class QfInsertSourceEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }

    [SqlTable("qf_insert_target")]
    public sealed class QfInsertTargetEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }

    private static void ConfigureSelect(IDataContext ctx)
    {
        ctx.Properties[TenantKey] = 1;
        ctx.From<QfSelectEntity>(b => b
            .HasQueryFilter((e, _) => e.Id > 5)
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));
    }

    private static string SelectSql(IDataContext ctx, EntityBuilder<QfSelectEntity> builder)
    {
        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            builder.Select(x => x.Id), false, false, CancellationToken.None);
        return prepared.DbCommand.CommandText.Replace("\r\n", "\n");
    }

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
    public void Select_Filter_ShouldRenderInWhere()
    {
        using var ctx = PostgresTestContext.Create();
        ConfigureSelect(ctx);

        var sql = SelectSql(ctx, ctx.From<QfSelectEntity>());

        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = @");
    }

    [Fact]
    public void Select_SelectiveIgnoreByKey_ShouldDropOnlyNamedFilter()
    {
        using var ctx = PostgresTestContext.Create();
        ConfigureSelect(ctx);

        var sql = SelectSql(ctx, ctx.From<QfSelectEntity>().IgnoreFilters(["tenant"]));

        sql.Should().Contain("id > 5");
        sql.Should().NotContain("tenant_id = @");
    }

    [Fact]
    public void Update_Where_ShouldAndFilter()
    {
        using var ctx = PostgresTestContext.Create();
        ConfigureSelect(ctx);

        var sql = ctx.Update<QfSelectEntity>()
            .Set(x => x.IsDeleted, true)
            .Where(x => x.Id == 10)
            .ToSql()
            .Replace("\r\n", "\n");

        sql.Should().Contain("id = 10");
        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = @");
    }

    [Fact]
    public void Delete_Where_ShouldAndFilter()
    {
        using var ctx = PostgresTestContext.Create();
        ConfigureSelect(ctx);

        var sql = ctx.DeleteFrom<QfSelectEntity>()
            .Where(x => x.Id == 10)
            .ToSql()
            .Replace("\r\n", "\n");

        sql.Should().Contain("id = 10");
        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = @");
    }

    [Fact]
    public void Insert_Target_ShouldNotBeFiltered()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.Properties[TenantKey] = 1;

        var sql = ctx.InsertInto<QfInsertTargetEntity>(b => b
                .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]))
            .Values(new QfInsertTargetEntity { Id = 1, TenantId = 1, IsDeleted = false })
            .ToSql()
            .Replace("\r\n", "\n");

        sql.Should().StartWith("insert into qf_insert_target");
        sql.Should().NotContain("where");
    }

    [Fact]
    public void InsertSelect_SourceFiltered_TargetNotFiltered()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.Properties[TenantKey] = 1;
        ctx.From<QfInsertSourceEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        var sql = ctx.InsertInto<QfInsertTargetEntity>(b => b
                .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]))
            .Values(
                ctx.From<QfInsertSourceEntity>().Where(x => x.Id > 0),
                s => new { s.Id, s.TenantId, s.IsDeleted })
            .ToSql()
            .Replace("\r\n", "\n");

        sql.Should().Contain("from qf_insert_source");
        CountOccurrences(sql, "tenant_id = @").Should().Be(1, "only the source filter renders");
    }
}
