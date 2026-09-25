using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

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
        [Collation("utf8mb4_bin")]
        string? Name { get; set; }
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void CollatedColumn_InWhere_ShouldRenderCollate()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<ICollatedEntity>();

        SqlOf(ctx, e.Where(x => x.Name == "a").Select(x => new { x.Id }))
            .Should().Contain("name collate utf8mb4_bin = 'a'");
    }

    [Fact]
    public void CollatedColumn_InOrderBy_ShouldRenderCollate()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<ICollatedEntity>();

        SqlOf(ctx, e.OrderBy(x => x.Name).Select(x => new { x.Id }))
            .Should().Contain("order by name collate utf8mb4_bin");
    }
}
