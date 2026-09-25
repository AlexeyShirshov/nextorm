using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [SqlTable("range_probe")]
    public interface IRangeProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [RangeColumns("during_lower", "during_upper")]
        Range<int> During { get; set; }
    }

    public sealed class RangeProbe : IRangeProbe
    {
        public long Id { get; set; }
        public Range<int> During { get; set; }
    }

    /// <summary>
    /// Round-trips a <see cref="Range{T}"/> stored as a pair of scalar columns
    /// (<see cref="RangeColumnsAttribute"/>) and filters on it on every provider. The table is created
    /// from the dialect's result-column type so the test stays provider-agnostic, and is dropped again
    /// at the end. Exercises writes, the read-back materializer and the translated predicates.
    /// </summary>
    [Fact]
    public void RangeColumns_ShouldRoundTripAndFilter() => RangeColumnsRoundTrip(_sut.DataProvider);

    /// <summary>
    /// The round-trip body, shared with provider integration classes that do not inherit
    /// <see cref="CommonTestSuite"/> (ClickHouse runs its own provider-specific suite).
    /// </summary>
    internal static void RangeColumnsRoundTrip(IDataContext ctx)
    {
        var dialect = ((DataContext)ctx).Dialect;
        var isClickHouse = dialect.GetType().Name.Contains("ClickHouse", StringComparison.Ordinal);
        // Every provider's plain `int` column is nullable; only ClickHouse's Int32 is not.
        var boundType = isClickHouse ? "Nullable(Int32)" : "int";
        var engine = isClickHouse ? " engine = Memory" : string.Empty;

        ExecuteRangeColumns(ctx, "drop table if exists range_probe");
        ExecuteRangeColumns(ctx, $"create table range_probe (id bigint, during_lower {boundType}, during_upper {boundType}){engine}");

        try
        {
            ctx.InsertInto<IRangeProbe>()
                .Values(new[]
                {
                    new RangeProbe { Id = 1, During = new Range<int>(1, 10) },
                    new RangeProbe { Id = 2, During = new Range<int>(20, 30) },
                })
                .Insert();

            var roundTrip = ctx.From<RangeProbe>().Where(x => x.Id == 1).ToList().Single().During;
            roundTrip.Lower.Should().Be(1);
            roundTrip.Upper.Should().Be(10);
            // Inclusivity belongs to the [lower, upper) mapping, not to the stored bounds.
            roundTrip.LowerInclusive.Should().BeTrue();
            roundTrip.UpperInclusive.Should().BeFalse();

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.overlaps(x.During, new Range<int>(5, 15)))
                .Select(x => x.Id).ToList().Should().Equal(1L);

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_contains(x.During, 5))
                .Select(x => x.Id).ToList().Should().Equal(1L);

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_strictly_left_of(x.During, new Range<int>(15, 25)))
                .Select(x => x.Id).ToList().Should().Equal(1L);

            // An unbounded bound is stored as SQL NULL and reads back as infinite.
            ctx.InsertInto<IRangeProbe>()
                .Values(new RangeProbe
                {
                    Id = 3,
                    During = new Range<int>(0, 0, lowerInclusive: true, upperInclusive: false, lowerInfinite: false, upperInfinite: true)
                })
                .Insert();

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_contains(x.During, 1000))
                .Select(x => x.Id).ToList().Should().Equal(3L);
        }
        finally
        {
            ExecuteRangeColumns(ctx, "drop table if exists range_probe");
        }
    }

    private static void ExecuteRangeColumns(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
