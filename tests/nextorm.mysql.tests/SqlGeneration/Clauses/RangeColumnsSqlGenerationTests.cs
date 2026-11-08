using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// A <see cref="Range{T}"/> stored as a pair of scalar columns (<see cref="RangeColumnsAttribute"/>)
/// renders its predicates as comparisons between the two bounds on a provider without a native range
/// type, with SQL <c>NULL</c> treated as an unbounded side. No database connection is opened.
/// </summary>
public class RangeColumnsSqlGenerationTests
{
    [SqlTable("range_sql")]
    public interface IRangeSqlEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [RangeColumns("rl", "ru")]
        Range<int> During { get; set; }
    }

    [Fact]
    public void Overlaps_OverPair_ShouldRenderBoundComparisons()
    {
        using var ctx = MySqlTestContext.Create();
        var query = ctx.From<IRangeSqlEntity>()
            .Where(x => SqlFunctions.Postgres.overlaps(x.During, new Range<int>(5, 15)))
            .Select(x => x.Id);

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(query, false, false, CancellationToken.None);

        var sql = prepared.DbCommand.CommandText.Replace("\r\n", "\n");
        sql.Should().Contain("rl").And.Contain("ru");
        sql.Should().Contain("is null");
        sql.Should().NotContain("overlaps");
        prepared.DbCommandParams[0].Value.Should().Be(5);
        prepared.DbCommandParams[1].Value.Should().Be(15);
    }

    [Fact]
    public void ContainedBy_OverPair_ShouldRenderBoundComparisons()
    {
        using var ctx = MySqlTestContext.Create();
        var query = ctx.From<IRangeSqlEntity>()
            .Where(x => SqlFunctions.Postgres.range_contained_by(x.During, new Range<int>(0, 100)))
            .Select(x => x.Id);

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(query, false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Replace("\r\n", "\n").Should()
            .Contain("rl").And.Contain("ru").And.NotContain("range_contained_by");
    }
}
