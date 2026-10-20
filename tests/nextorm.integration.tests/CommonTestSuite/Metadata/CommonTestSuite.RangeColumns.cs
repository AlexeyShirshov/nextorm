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
    /// The same stored pair read through the opposite bound-inclusivity mapping
    /// (<c>(lower, upper]</c>), which is what the <c>lower_inc</c>/<c>upper_inc</c> translation
    /// branches on: inclusivity belongs to the mapping, not to the stored values.
    /// </summary>
    [SqlTable("range_probe")]
    public interface IRangeProbeExclusive
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [RangeColumns("during_lower", "during_upper", LowerInclusive = false, UpperInclusive = true)]
        Range<int> During { get; set; }
    }

    public sealed class RangeProbeExclusive : IRangeProbeExclusive
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
            ctx.CreateInsertBuilder<IRangeProbe>()
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
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L);

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_contains(x.During, 5))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L);

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_strictly_left_of(x.During, new Range<int>(15, 25)))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L);

            // An unbounded bound is stored as SQL NULL and reads back as infinite.
            ctx.CreateInsertBuilder<IRangeProbe>()
                .Values(new RangeProbe
                {
                    Id = 3,
                    During = new Range<int>(0, 0, lowerInclusive: true, upperInclusive: false, lowerInfinite: false, upperInfinite: true)
                })
                .Insert();

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_contains(x.During, 1000))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(3L);

            // A row with no lower bound, added after the predicates that predate it so their
            // expectations stay untouched.
            ctx.CreateInsertBuilder<IRangeProbe>()
                .Values(new RangeProbe
                {
                    Id = 4,
                    During = new Range<int>(0, 5, lowerInclusive: true, upperInclusive: false, lowerInfinite: true, upperInfinite: false)
                })
                .Insert();

            // Inspection functions over the stored pair. A pair is never empty; infinity is a NULL
            // bound; the *_inc forms fold the mapping's inclusivity, not the stored value.
            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.isempty(x.During))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().BeEmpty();

            ctx.From<RangeProbe>()
                .Where(x => x.Id == 2)
                .Select(x => SqlFunctions.Postgres.lower(x.During)).ToList()
                .Should().Equal(20);

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.lower(x.During) == 20)
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(2L);

            ctx.From<RangeProbe>()
                .Where(x => x.Id == 2)
                .Select(x => SqlFunctions.Postgres.upper(x.During)).ToList()
                .Should().Equal(30);

            // Row 3 has no upper bound, so upper() reads back as SQL NULL.
            ctx.From<RangeProbe>()
                .Where(x => x.Id == 3)
                .Select(x => SqlFunctions.Postgres.upper(x.During)).ToList()
                .Should().Equal(new int?[] { null });

            // The default [lower, upper) mapping: a present lower bound is inclusive, an upper
            // bound is never inclusive.
            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.lower_inc(x.During))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L, 3L);

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.upper_inc(x.During))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().BeEmpty();

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.lower_inf(x.During))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(4L);

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.upper_inf(x.During))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(3L);

            // The opposite (lower, upper] mapping over the same stored pair: the lower bound is
            // never inclusive and a present upper bound is.
            ctx.From<RangeProbeExclusive>()
                .Where(x => SqlFunctions.Postgres.lower_inc(x.During))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().BeEmpty();

            ctx.From<RangeProbeExclusive>()
                .Where(x => SqlFunctions.Postgres.upper_inc(x.During))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L, 4L);

            // A range-typed second argument reaches the range-to-range switch, unlike the scalar
            // overload above; an unbounded upper side (row 3) contains every range starting above 0.
            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_contains(x.During, new Range<int>(2, 9)))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 3L);

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_contained_by(x.During, new Range<int>(0, 100)))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L);

            // [10, 20) touches row 1 at its upper end and row 2 at its lower end.
            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_adjacent(x.During, new Range<int>(10, 20)))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L);

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_strictly_right_of(x.During, new Range<int>(10, 15)))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(2L);

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_not_extend_right_of(x.During, new Range<int>(0, 100)))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L, 4L);

            // An unbounded upper side of the argument accepts every row, row 3 included.
            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_not_extend_right_of(
                    x.During, new Range<int>(0, 0, lowerInclusive: true, upperInclusive: false, lowerInfinite: false, upperInfinite: true)))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L, 3L, 4L);

            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_not_extend_left_of(x.During, new Range<int>(-100, 100)))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L, 3L);

            // An unbounded lower side of the argument accepts the row with no lower bound too.
            ctx.From<RangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_not_extend_left_of(
                    x.During, new Range<int>(0, 0, lowerInclusive: true, upperInclusive: false, lowerInfinite: true, upperInfinite: false)))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L, 3L, 4L);
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
