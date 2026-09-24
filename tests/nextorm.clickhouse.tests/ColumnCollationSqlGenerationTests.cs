using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// ClickHouse has no per-expression <c>COLLATE</c> clause, so a collated column must be rejected
/// instead of silently dropping the declared collation.
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
        [Collation("binary")]
        string? Name { get; set; }
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void CollatedColumn_ShouldThrowBecauseNoCollateClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ICollatedEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { x.Name }));

        act.Should().Throw<NotSupportedException>().WithMessage("*COLLATE*");
    }
}
