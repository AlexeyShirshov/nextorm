using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
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

        ctx.CreateInsertBuilder<IReservationEntity>()
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

        ctx.CreateInsertBuilder<IReservationEntity>()
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

        ctx.CreateInsertBuilder<IClosedReservationEntity>()
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

        ctx.CreateInsertBuilder<IReservationEntity>()
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
        ctx.CreateInsertBuilder<IReservationEntity>()
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

        act.Should().Throw<NotSupportedException>()
            .WithMessage(RangeReturningOverPairMessage("range_union"));
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

        var act = () => ctx.CreateInsertBuilder<IReservationEntity>()
            .Values(new ReservationEntity { Id = 1, During = Range<int>.Empty })
            .Insert();

        act.Should().Throw<NotSupportedException>().WithMessage("*empty range*");
    }

    [Fact]
    public void Overlaps_UnboundedColumn_ShouldMatch()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        ctx.CreateInsertBuilder<IReservationEntity>()
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

        Action act = () => ctx.CreateInsertBuilder<IReservationEntity>().Value(x => x.During, new Range<int>(1, 10));

        act.Should().Throw<NotSupportedException>().WithMessage("*During*Values(entity)*");
    }

    [Fact]
    public void RangePair_Mapping_ShouldThrow()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        Action act = () => ctx.CreateInsertBuilder<IReservationEntity>()
            .Values(new[] { new { During = new Range<int>(1, 10) } }, s => new { s.During });

        act.Should().Throw<NotSupportedException>().WithMessage("*During*");
    }

    [Fact]
    public void RangePair_UpdateSelector_ShouldThrow()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        Action act = () => ctx.CreateUpdateBuilder<IReservationEntity>().Set(x => x.During, new Range<int>(1, 10));

        act.Should().Throw<NotSupportedException>().WithMessage("*During*Set(entity)*");
    }

    [Fact]
    public void RangePair_ReturningProjection_ShouldThrow()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        Action act = () => ctx.CreateInsertBuilder<IReservationEntity>()
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

        ctx.CreateInsertBuilder<IClosedReservationEntity>()
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

    // The canonical diagnostic source of the pair-over-range rejection. The text is pinned exactly
    // (punctuation and spacing included) so the prepare-time guard, the anonymous-projection guard and
    // the predicate/operand guard cannot drift apart.
    private const string RangeReturningOverPairSuffix =
        "returns a range and cannot be expressed over a pair of columns. Use a provider with a native range type (PostgreSQL) or a range-returning projection.";

    private static string RangeReturningOverPairMessage(string name)
        => $"The PostgreSQL range function '{name}' {RangeReturningOverPairSuffix}";

    /// <summary>
    /// Bare projection (<c>Select(x =&gt; range_union(x.During, ...))</c>) of a <see cref="Range{T}"/>-returning
    /// PostgreSQL function over a mapped <see cref="RangeColumnsAttribute"/> pair. The body has no
    /// single-column shape, so preparation falls through to the row mapper with an empty select list
    /// unless the preparer rejects it; the pair position is varied so a first-argument-only check fails.
    /// </summary>
    [Theory]
    [InlineData("range_union", true)]
    [InlineData("range_union", false)]
    [InlineData("range_intersection", true)]
    [InlineData("range_intersection", false)]
    [InlineData("range_difference", true)]
    [InlineData("range_difference", false)]
    [InlineData("range_merge", true)]
    [InlineData("range_merge", false)]
    [InlineData("range_intersect_agg", true)]
    public void RangeReturning_BareProjectionOverPair_ShouldThrowNamedDiagnostic(string functionName, bool pairFirst)
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var builder = ctx.From<ReservationEntity>().Where(x => x.Id == 1);

        var command = (functionName, pairFirst) switch
        {
            ("range_union", true) => builder.Select(x => SqlFunctions.Postgres.range_union(x.During, new Range<int>(1, 10))),
            ("range_union", false) => builder.Select(x => SqlFunctions.Postgres.range_union(new Range<int>(1, 10), x.During)),
            ("range_intersection", true) => builder.Select(x => SqlFunctions.Postgres.range_intersection(x.During, new Range<int>(1, 10))),
            ("range_intersection", false) => builder.Select(x => SqlFunctions.Postgres.range_intersection(new Range<int>(1, 10), x.During)),
            ("range_difference", true) => builder.Select(x => SqlFunctions.Postgres.range_difference(x.During, new Range<int>(1, 10))),
            ("range_difference", false) => builder.Select(x => SqlFunctions.Postgres.range_difference(new Range<int>(1, 10), x.During)),
            ("range_merge", true) => builder.Select(x => SqlFunctions.Postgres.range_merge(x.During, new Range<int>(1, 10))),
            ("range_merge", false) => builder.Select(x => SqlFunctions.Postgres.range_merge(new Range<int>(1, 10), x.During)),
            ("range_intersect_agg", true) => builder.Select(x => SqlFunctions.Postgres.range_intersect_agg(x.During)),
            _ => throw new ArgumentOutOfRangeException(nameof(functionName), functionName, null)
        };

        var act = () => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage(RangeReturningOverPairMessage(functionName));
    }

    /// <summary>
    /// Bare projection of the conversion-wrapped forms: an identity <see cref="Expression.Convert"/>
    /// over the whole body and over the pair argument must be unwrapped before classification. Built
    /// through the expression API so the wrappers really are present in the tree (the C# compiler may
    /// elide an identity cast).
    /// </summary>
    [Fact]
    public void RangeReturning_BareProjectionOverPair_ConvertWrappedForms_ShouldThrowNamedDiagnostic()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        Expression<Func<ReservationEntity, Range<int>>> template =
            x => SqlFunctions.Postgres.range_union(x.During, new Range<int>(1, 10));
        var call = (MethodCallExpression)template.Body;
        var builder = ctx.From<ReservationEntity>().Where(x => x.Id == 1);

        var wrappedBody = Expression.Convert(call, typeof(Range<int>));
        var bodyCommand = builder.Select(Expression.Lambda<Func<ReservationEntity, Range<int>>>(wrappedBody, template.Parameters));

        var wrappedArgument = Expression.Convert(call.Arguments[0], typeof(Range<int>));
        var argumentBody = Expression.Call(call.Object, call.Method, wrappedArgument, call.Arguments[1]);
        var argumentCommand = builder.Select(Expression.Lambda<Func<ReservationEntity, Range<int>>>(argumentBody, template.Parameters));

        var bodyAct = () => ctx.GetPreparedQueryCommand(bodyCommand, false, false, CancellationToken.None);
        var argumentAct = () => ctx.GetPreparedQueryCommand(argumentCommand, false, false, CancellationToken.None);

        bodyAct.Should().Throw<NotSupportedException>().WithMessage(RangeReturningOverPairMessage("range_union"));
        argumentAct.Should().Throw<NotSupportedException>().WithMessage(RangeReturningOverPairMessage("range_union"));
    }

    /// <summary>
    /// The array-returning members of the same set. <c>multirange</c> has no single-column shape and is
    /// rejected by the pair renderer during preparation; <c>range_agg</c> is an aggregate rejected
    /// earlier by the aggregate capability guard. Both are deliberate rejections rather than the raw
    /// constructor failure, and neither is claimed as newly supported.
    /// </summary>
    [Fact]
    public void RangeReturning_ArrayProjectionOverPair_ShouldBeRejectedDeliberately()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var builder = ctx.From<ReservationEntity>().Where(x => x.Id == 1);

        var multirange = builder.Select(x => SqlFunctions.Postgres.multirange(x.During));
        var rangeAgg = builder.Select(x => SqlFunctions.Postgres.range_agg(x.During));

        var multirangeAct = () => ctx.GetPreparedQueryCommand(multirange, false, false, CancellationToken.None);
        var rangeAggAct = () => ctx.GetPreparedQueryCommand(rangeAgg, false, false, CancellationToken.None);

        multirangeAct.Should().Throw<NotSupportedException>().WithMessage(RangeReturningOverPairMessage("multirange"));
        rangeAggAct.Should().Throw<NotSupportedException>()
            .WithMessage("The range_agg/range_intersect_agg aggregates are not supported by this provider.");
    }

    /// <summary>
    /// Negative control for the constructor members of the set: their signatures accept only scalar
    /// bounds, so a mapped pair property can never be an argument. The prepare-time guard must not fire
    /// and the pre-existing unsupported projection fallthrough is unchanged (this is not a support
    /// claim, and it does not assert any adjacent defect as fixed).
    /// </summary>
    [Fact]
    public void RangeReturning_BareConstructorProjection_ShouldNotUsePairDiagnostic()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        EntityBuilder<ReservationEntity> Fresh() => ctx.From<ReservationEntity>().Where(x => x.Id == 1);

        Action[] acts =
        [
            () => ctx.GetPreparedQueryCommand(Fresh().Select(x => SqlFunctions.Postgres.empty_range<int>()), false, false, CancellationToken.None),
            () => ctx.GetPreparedQueryCommand(Fresh().Select(x => SqlFunctions.Postgres.int4range(1, 10)), false, false, CancellationToken.None),
            () => ctx.GetPreparedQueryCommand(Fresh().Select(x => SqlFunctions.Postgres.int8range(1L, 10L)), false, false, CancellationToken.None),
            () => ctx.GetPreparedQueryCommand(Fresh().Select(x => SqlFunctions.Postgres.numrange(1m, 10m)), false, false, CancellationToken.None),
            () => ctx.GetPreparedQueryCommand(Fresh().Select(x => SqlFunctions.Postgres.tsrange(new DateTime(2024, 1, 1), new DateTime(2024, 1, 10))), false, false, CancellationToken.None),
            () => ctx.GetPreparedQueryCommand(Fresh().Select(x => SqlFunctions.Postgres.tstzrange(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero))), false, false, CancellationToken.None),
            () => ctx.GetPreparedQueryCommand(Fresh().Select(x => SqlFunctions.Postgres.daterange(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 10))), false, false, CancellationToken.None),
        ];

        foreach (var act in acts)
        {
            act.Should().Throw<ArgumentException>().WithMessage("Incorrect number of arguments for constructor");
        }
    }

    /// <summary>
    /// Negative control: a range-returning call whose arguments are a captured value, a constant, a
    /// mapped non-pair property or no argument at all, and a non-<c>RangeReturning</c> method, must not
    /// be classified as a pair operation. The unchanged fallthrough failure is asserted as such, not
    /// as desired behaviour.
    /// </summary>
    [Fact]
    public void RangeReturning_WithoutPairOperand_ShouldNotUsePairDiagnostic()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        EntityBuilder<ReservationEntity> Fresh() => ctx.From<ReservationEntity>().Where(x => x.Id == 1);
        var captured = new Range<int>(1, 2);

        Action[] acts =
        [
            () => ctx.GetPreparedQueryCommand(Fresh().Select(x => SqlFunctions.Postgres.range_union(captured, new Range<int>(3, 4))), false, false, CancellationToken.None),
            () => ctx.GetPreparedQueryCommand(Fresh().Select(x => SqlFunctions.Postgres.range_union(new Range<int>(1, 2), new Range<int>(3, 4))), false, false, CancellationToken.None),
            () => ctx.GetPreparedQueryCommand(Fresh().Select(x => SqlFunctions.Postgres.int4range(x.Id, x.Id)), false, false, CancellationToken.None),
            () => ctx.GetPreparedQueryCommand(Fresh().Select(x => IdentityRange(x.During)), false, false, CancellationToken.None),
        ];

        foreach (var act in acts)
        {
            act.Should().Throw<ArgumentException>().WithMessage("Incorrect number of arguments for constructor");
        }
    }

    /// <summary>Non-<c>RangeReturning</c> method used by the negative control above.</summary>
    private static Range<int> IdentityRange(Range<int> value) => value;

    /// <summary>
    /// Anonymous projection keeps the existing translator diagnostic, and the capability check text is
    /// untouched (the SQLite dialect supports range pairs, so the capability throw is not reachable
    /// here; the message itself is covered by the source review).
    /// </summary>
    [Fact]
    public void RangeReturning_AnonymousProjectionOverPair_ShouldKeepNamedDiagnostic()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        var command = ctx.From<ReservationEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { U = SqlFunctions.Postgres.range_union(x.During, new Range<int>(1, 10)) });

        var act = () => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage(RangeReturningOverPairMessage("range_union"));
    }
}
