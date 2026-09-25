using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Round-trip tests for a <see cref="Range{T}"/> stored as a pair of scalar columns on a provider
/// without a native range type (SQLite). The lower/upper bounds are written to and read from the two
/// columns declared with <see cref="RangeColumnsAttribute"/>; a SQL <c>NULL</c> bound is unbounded.
/// </summary>
public class RangeColumnsTests
{
    [SqlTable("reservation")]
    public interface IReservationEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [RangeColumns("during_lower", "during_upper")]
        Range<int> During { get; set; }
    }

    public class ReservationEntity : IReservationEntity
    {
        public int Id { get; set; }
        public Range<int> During { get; set; }
    }

    [SqlTable("closed_reservation")]
    public interface IClosedReservationEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [RangeColumns("closed_lower", "closed_upper", LowerInclusive = false, UpperInclusive = true)]
        Range<int> During { get; set; }
    }

    public class ClosedReservationEntity : IClosedReservationEntity
    {
        public int Id { get; set; }
        public Range<int> During { get; set; }
    }

    [SqlTable("record_reservation")]
    public record RecordReservationEntity(int Id, [property: RangeColumns("during_lower", "during_upper")] Range<int> During);

    private static SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var setup = conn.CreateCommand();
        setup.CommandText = "create table reservation (id integer primary key, during_lower integer, during_upper integer);";
        setup.ExecuteNonQuery();
        return conn;
    }

    private static SqliteDataContext ContextFor(SqliteConnection connection)
        => new(connection, new DataContextBuilder());

    private static object? Raw(SqliteConnection connection, string column)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"select {column} from reservation where id = 1";
        var value = cmd.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    [Fact]
    public void RangeColumns_ShouldWriteBothBounds()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        ctx.InsertInto<IReservationEntity>()
            .Values(new ReservationEntity { Id = 1, During = new Range<int>(1, 10) })
            .Insert();

        Raw(conn, "during_lower").Should().Be(1L);
        Raw(conn, "during_upper").Should().Be(10L);
    }

    [Fact]
    public void RangeColumns_ShouldRoundTripBoundsWithDeclaredInclusivity()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        ctx.InsertInto<IReservationEntity>()
            .Values(new ReservationEntity { Id = 1, During = new Range<int>(1, 10, lowerInclusive: true, upperInclusive: true) })
            .Insert();

        var range = ctx.From<ReservationEntity>().ToList().Single().During;

        range.Lower.Should().Be(1);
        range.Upper.Should().Be(10);
        // Inclusivity belongs to the mapping, not to the stored value: the declared [lower, upper)
        // wins over the inclusivity of the written range.
        range.LowerInclusive.Should().BeTrue();
        range.UpperInclusive.Should().BeFalse();
    }

    [Fact]
    public void RangeColumns_ShouldApplyDeclaredInclusivity()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        using (var setup = conn.CreateCommand())
        {
            setup.CommandText = "create table closed_reservation (id integer primary key, closed_lower integer, closed_upper integer);";
            setup.ExecuteNonQuery();
        }

        ctx.InsertInto<IClosedReservationEntity>()
            .Values(new ClosedReservationEntity { Id = 1, During = new Range<int>(1, 10) })
            .Insert();

        var range = ctx.From<ClosedReservationEntity>().ToList().Single().During;

        range.LowerInclusive.Should().BeFalse();
        range.UpperInclusive.Should().BeTrue();
    }

    [Fact]
    public void RangeColumns_ShouldWriteNullForUnboundedSide()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        ctx.InsertInto<IReservationEntity>()
            .Values(new ReservationEntity { Id = 1, During = new Range<int>(0, 5, lowerInclusive: true, upperInclusive: false, lowerInfinite: true, upperInfinite: false) })
            .Insert();

        Raw(conn, "during_lower").Should().BeNull();
        Raw(conn, "during_upper").Should().Be(5L);

        var range = ctx.From<ReservationEntity>().ToList().Single().During;
        range.LowerInfinite.Should().BeTrue();
        range.Upper.Should().Be(5);
    }

    [Fact]
    public void RangeColumns_ShouldReadNullBoundsAsUnbounded()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        using (var raw = conn.CreateCommand())
        {
            raw.CommandText = "insert into reservation (id, during_lower, during_upper) values (1, null, null);";
            raw.ExecuteNonQuery();
        }

        var range = ctx.From<ReservationEntity>().ToList().Single().During;

        range.LowerInfinite.Should().BeTrue();
        range.UpperInfinite.Should().BeTrue();
    }

    [Fact]
    public void RangeColumns_RecordEntity_ShouldMaterializeRangeThroughConstructor()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        using (var setup = conn.CreateCommand())
        {
            setup.CommandText = "create table record_reservation (id integer primary key, during_lower integer, during_upper integer);";
            setup.ExecuteNonQuery();
        }

        using (var raw = conn.CreateCommand())
        {
            raw.CommandText = "insert into record_reservation (id, during_lower, during_upper) values (1, 1, 10);";
            raw.ExecuteNonQuery();
        }

        var entity = ctx.From<RecordReservationEntity>().ToList().Single();

        entity.Id.Should().Be(1);
        entity.During.Lower.Should().Be(1);
        entity.During.Upper.Should().Be(10);
    }

    private static void Seed(SqliteDataContext ctx)
    {
        ctx.InsertInto<IReservationEntity>()
            .Values(new[]
            {
                new ReservationEntity { Id = 1, During = new Range<int>(1, 10) },
                new ReservationEntity { Id = 2, During = new Range<int>(20, 30) },
            })
            .Insert();
    }

    [Fact]
    public void Overlaps_ShouldFilterByInterval()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var ids = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.overlaps(x.During, new Range<int>(5, 15)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1);
    }

    [Fact]
    public void Overlaps_AtTouchingBoundary_ShouldNotMatch()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var ids = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.overlaps(x.During, new Range<int>(10, 20)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void RangeContains_Value_ShouldFilter()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var inside = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.range_contains(x.During, 5))
            .Select(x => x.Id)
            .ToList();
        var boundary = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.range_contains(x.During, 10))
            .Select(x => x.Id)
            .ToList();

        inside.Should().Equal(1);
        boundary.Should().BeEmpty();
    }

    [Fact]
    public void LowerUpper_ShouldProjectBounds()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var bounds = ctx.From<ReservationEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new { L = SqlFunctions.Postgres.lower(x.During), U = SqlFunctions.Postgres.upper(x.During) })
            .Single();

        bounds.L.Should().Be(20);
        bounds.U.Should().Be(30);
    }

    [Fact]
    public void RangeReturning_OverPair_ShouldThrow()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        var query = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.range_not_extend_right_of(
                SqlFunctions.Postgres.range_union(x.During, new Range<int>(1, 2)), x.During))
            .Select(x => x.Id);

        var act = () => ctx.GetPreparedQueryCommand(query, false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage("*returns a range*");
    }

    [Fact]
    public void Adjacent_ShouldMatchTouchingRanges()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var ids = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.range_adjacent(x.During, new Range<int>(10, 20)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1, 2);
    }

    [Fact]
    public void ContainedBy_ShouldMatch()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var ids = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.range_contained_by(x.During, new Range<int>(0, 40)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1, 2);
    }

    [Fact]
    public void IsEmpty_OverPair_ShouldBeFalse()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var ids = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.isempty(x.During))
            .Select(x => x.Id)
            .ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void EmptyRange_Write_ShouldThrow()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        var act = () => ctx.InsertInto<IReservationEntity>()
            .Values(new ReservationEntity { Id = 1, During = Range<int>.Empty })
            .Insert();

        act.Should().Throw<NotSupportedException>().WithMessage("*empty range*");
    }

    [Fact]
    public void Overlaps_UnboundedColumn_ShouldMatch()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        ctx.InsertInto<IReservationEntity>()
            .Values(new ReservationEntity
            {
                Id = 1,
                During = new Range<int>(0, 0, lowerInclusive: true, upperInclusive: false, lowerInfinite: false, upperInfinite: true)
            })
            .Insert();

        var ids = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.overlaps(x.During, new Range<int>(100, 200)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1);
    }

    [Fact]
    public void RangeColumns_ThenHasConversion_ShouldThrow()
    {
        var property = new EntityMetadataBuilder<ReservationEntity>().Property(x => x.During);
        property.RangeColumns("l", "u");

        Action act = () => property.HasConversion<Range<int>, Range<int>>(r => r, r => r);

        act.Should().Throw<InvalidOperationException>().WithMessage("*RangeColumns*");
    }

    [Fact]
    public void HasConversion_ThenRangeColumns_ShouldThrow()
    {
        var property = new EntityMetadataBuilder<ReservationEntity>().Property(x => x.During);
        property.HasConversion<Range<int>, Range<int>>(r => r, r => r);

        Action act = () => property.RangeColumns("l", "u");

        act.Should().Throw<InvalidOperationException>().WithMessage("*RangeColumns*");
    }

    [Fact]
    public void RangeColumns_ThenJsonColumn_ShouldThrow()
    {
        var property = new EntityMetadataBuilder<ReservationEntity>().Property(x => x.During);
        property.RangeColumns("l", "u");

        Action act = () => property.JsonColumn();

        act.Should().Throw<InvalidOperationException>().WithMessage("*RangeColumns*");
    }

    [Fact]
    public void JsonColumn_ThenRangeColumns_ShouldThrow()
    {
        var property = new EntityMetadataBuilder<ReservationEntity>().Property(x => x.During);
        property.JsonColumn();

        Action act = () => property.RangeColumns("l", "u");

        act.Should().Throw<InvalidOperationException>().WithMessage("*RangeColumns*");
    }

    [Fact]
    public void RangePair_ValueSelector_ShouldThrow()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        Action act = () => ctx.InsertInto<IReservationEntity>().Value(x => x.During, new Range<int>(1, 10));

        act.Should().Throw<NotSupportedException>().WithMessage("*During*Values(entity)*");
    }

    [Fact]
    public void RangePair_Mapping_ShouldThrow()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        Action act = () => ctx.InsertInto<IReservationEntity>()
            .Values(new[] { new { During = new Range<int>(1, 10) } }, s => new { s.During });

        act.Should().Throw<NotSupportedException>().WithMessage("*During*");
    }

    [Fact]
    public void RangePair_UpdateSelector_ShouldThrow()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        Action act = () => ctx.Update<IReservationEntity>().Set(x => x.During, new Range<int>(1, 10));

        act.Should().Throw<NotSupportedException>().WithMessage("*During*Set(entity)*");
    }

    [Fact]
    public void RangePair_ReturningProjection_ShouldThrow()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        Action act = () => ctx.InsertInto<IReservationEntity>()
            .Values(new ReservationEntity { Id = 1, During = new Range<int>(1, 10) })
            .Returning(x => x.During);

        act.Should().Throw<NotSupportedException>().WithMessage("*During*");
    }

    [Fact]
    public void StrictlyLeftOf_ShouldFilter()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var ids = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.range_strictly_left_of(x.During, new Range<int>(15, 25)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1);
    }

    [Fact]
    public void StrictlyRightOf_ShouldFilter()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var ids = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.range_strictly_right_of(x.During, new Range<int>(0, 5)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(2);
    }

    [Fact]
    public void NotExtendRightOf_ShouldFilter()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var ids = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.range_not_extend_right_of(x.During, new Range<int>(0, 10)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1);
    }

    [Fact]
    public void NotExtendLeftOf_ShouldFilter()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var ids = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.range_not_extend_left_of(x.During, new Range<int>(15, 25)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(2);
    }

    [Fact]
    public void Contains_Range_ShouldFilter()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var ids = ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.range_contains(x.During, new Range<int>(2, 5)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1);
    }

    [Fact]
    public void Inspection_InclusiveFlags_ShouldReflectMapping()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        var open = ctx.From<ReservationEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { L = SqlFunctions.Postgres.lower_inc(x.During), U = SqlFunctions.Postgres.upper_inc(x.During) })
            .Single();

        // The [lower, upper) mapping wins over the written range's inclusivity.
        open.L.Should().BeTrue();
        open.U.Should().BeFalse();
    }

    [Fact]
    public void Inspection_InfiniteFlags_ShouldReflectNullBounds()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        using (var raw = conn.CreateCommand())
        {
            raw.CommandText = "insert into reservation (id, during_lower, during_upper) values (1, null, 10), (2, 0, null);";
            raw.ExecuteNonQuery();
        }

        var unboundedStart = ctx.From<ReservationEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { Inf = SqlFunctions.Postgres.lower_inf(x.During), Empty = SqlFunctions.Postgres.isempty(x.During) })
            .Single();
        unboundedStart.Inf.Should().BeTrue();
        unboundedStart.Empty.Should().BeFalse();

        var unboundedEnd = ctx.From<ReservationEntity>()
            .Where(x => x.Id == 2)
            .Select(x => SqlFunctions.Postgres.upper_inf(x.During))
            .Single();
        unboundedEnd.Should().BeTrue();
    }

    [Fact]
    public void ClosedInclusiveInspection_ShouldReflectMapping()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        using (var setup = conn.CreateCommand())
        {
            setup.CommandText = "create table closed_reservation (id integer primary key, closed_lower integer, closed_upper integer);";
            setup.ExecuteNonQuery();
        }

        ctx.InsertInto<IClosedReservationEntity>()
            .Values(new ClosedReservationEntity { Id = 1, During = new Range<int>(1, 10) })
            .Insert();

        var flags = ctx.From<ClosedReservationEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { L = SqlFunctions.Postgres.lower_inc(x.During), U = SqlFunctions.Postgres.upper_inc(x.During) })
            .Single();

        flags.L.Should().BeFalse();
        flags.U.Should().BeTrue();
    }

    [Fact]
    public void ReusedQuery_ShouldHitCacheAndReturnSameRows()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Seed(ctx);

        List<int> Run() => ctx.From<ReservationEntity>()
            .Where(x => SqlFunctions.Postgres.overlaps(x.During, new Range<int>(5, 15)))
            .Select(x => x.Id)
            .ToList();

        Run().Should().Equal(1);
        // The second run reuses the cached plan; it must produce the same rows.
        Run().Should().Equal(1);
    }
}
