using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation for a collation declared on a mapped column (<see cref="CollationAttribute"/>).
/// No database is touched.
/// </summary>
public class ColumnCollationSqlGenerationTests
{
    [SqlTable("collated_entity")]
    internal interface ICollatedEntity
    {
        [Key]
        [Column("id")]
        long Id { get; set; }
        [Column("name")]
        [Collation("C")]
        string? Name { get; set; }
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void CollatedColumn_InWhere_ShouldRenderCollate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ICollatedEntity>();

        SqlOf(ctx, e.Where(x => x.Name == "a").Select(x => new { x.Id }))
            .Should().Contain("name collate \"C\" = 'a'");
    }

    [Fact]
    public void CollatedColumn_InOrderBy_ShouldRenderCollate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ICollatedEntity>();

        SqlOf(ctx, e.OrderBy(x => x.Name).Select(x => new { x.Id }))
            .Should().Contain("order by name collate \"C\"");
    }

    [Fact]
    public void CollatedColumn_InProjection_ShouldRenderCollate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ICollatedEntity>();

        SqlOf(ctx, e.Select(x => new { x.Name }))
            .Should().Be("select name collate \"C\" as \"Name\" from collated_entity");
    }

    [Fact]
    public void CollatedColumn_InLike_ShouldRenderCollate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ICollatedEntity>();

        SqlOf(ctx, e.Where(x => x.Name!.Contains("a")).Select(x => new { x.Id }))
            .Should().Contain("name collate \"C\" like '%a%'");
    }

    [Fact]
    public void CollatedColumn_InJoinCondition_ShouldRenderCollate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ICollatedEntity>();

        SqlOf(ctx, e.Join(e, (a, b) => a.Name == b.Name).Select(p => new { p.Item1.Id }))
            .Should().Contain("collate \"C\" = t2.name collate \"C\"");
    }

    [Fact]
    public void CollatedColumn_NestedInOrdinalFunction_ShouldNotCollateTwice()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ICollatedEntity>();

        SqlOf(ctx, e.Where(x => string.Equals(x.Name!.ToUpper(), "A")).Select(x => new { x.Id }))
            .Should().Contain("upper(name) collate \"C\" = 'A' collate \"C\"");
    }

    [Fact]
    public void FluentCollation_ShouldRenderCollate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IFluentCollatedEntity>(b =>
        {
            b.Property(x => x.Id).HasColumnName("id").Key();
            b.Property(x => x.Name!).HasColumnName("name").Collation("C");
        });

        SqlOf(ctx, e.Where(x => x.Name == "a").Select(x => new { x.Id }))
            .Should().Contain("name collate \"C\" = 'a'");
    }

    [Fact]
    public void CollatedColumn_OrdinalEquals_ShouldRenderBinaryCollation()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ICollatedEntity>();

        SqlOf(ctx, e.Where(x => string.Equals(x.Name, "a")).Select(x => new { x.Id }))
            .Should().Contain("name collate \"C\" = 'a' collate \"C\"");
    }

    [SqlTable("fluent_collated_entity")]
    internal interface IFluentCollatedEntity
    {
        [Key]
        [Column("id")]
        long Id { get; set; }
        [Column("name")]
        string? Name { get; set; }
    }
}
