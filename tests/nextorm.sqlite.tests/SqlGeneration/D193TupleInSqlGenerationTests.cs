using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// D193.3 — SQLite rendering of the tuple value-list membership path (#193). SQLite overrides the row
/// value list with its <c>VALUES</c> grammar: <c>(a, b) IN (VALUES ($p0, $p1), ...)</c>.
/// </summary>
public class D193TupleInSqlGenerationTests
{
    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Prepare(ctx, cmd).DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void TupleContains_CapturedList_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)> { (1, 10), (2, 20) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int>(x.Id, x.Code))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code) in (VALUES ($p0, $p1), ($p2, $p3))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2", "p3");
    }

    [Fact]
    public void TupleIn_AtInForm_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<Tuple<long, int>> { Tuple.Create(1L, 10), Tuple.Create(2L, 20) };

        var command = Prepare(ctx, e
            .Where(x => SqlFunctions.Sql.@in(Tuple.Create(x.Id, x.Code), tuples))
            .Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code) in (VALUES ($p0, $p1), ($p2, $p3))");
    }

    [Fact]
    public void TupleContains_Empty_ShouldRenderAlwaysFalse()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)>();

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int>(x.Id, x.Code))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n").Should().Contain("1 = 0");
        command.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
    }

    [Fact]
    public void TupleContains_NullableComponent_ShouldGuardAndUseNullArm()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var tuples = new List<(long, int?)> { (2, 1), (1, null) };

        var sql = SqlOf(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int?>(x.Id, x.Int))).Select(x => new { x.Id }));

        sql.Should().Contain("nullableint is not null and (id, nullableint) in (VALUES ($p0, $p1))");
        sql.Should().Contain("(id is not null and id = $p2 and nullableint is null)");
    }

    [Fact]
    public void TupleContains_ReferenceTypeNullableComponent_ShouldGuardAndUseNullArm()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var tuples = new List<(long, string?)> { (2, "b"), (1, null) };

        var sql = SqlOf(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, string?>(x.Id, x.String))).Select(x => new { x.Id }));

        sql.Should().Contain("somestring is not null and (id, somestring) in (VALUES ($p0, $p1))");
        sql.Should().Contain("(id is not null and id = $p2 and somestring is null)");
    }

    [Fact]
    public void TupleContains_SequentialDifferentShapes_ShouldRebindWithoutDisablingCache()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var nonNullRows = new List<(long, int?)> { (1, 10), (2, 20) };
        var nullRow = new List<(long, int?)> { (3, null) };

        var first = e.Where(x => nonNullRows.Contains(new ValueTuple<long, int?>(x.Id, x.Int))).Select(x => new { x.Id });
        var firstSql = SqlOf(ctx, first);
        firstSql.Should().Contain("nullableint is not null and (id, nullableint) in (VALUES ($p0, $p1), ($p2, $p3))");

        var second = e.Where(x => nullRow.Contains(new ValueTuple<long, int?>(x.Id, x.Int))).Select(x => new { x.Id });
        var secondSql = SqlOf(ctx, second);
        secondSql.Should().Contain("(id is not null and id = $p0 and nullableint is null)");

        first.Cache.Should().BeTrue("the tuple IN/Contains path must not set the sticky Cache=false flag");
        second.Cache.Should().BeTrue("a second shape on the same context must not disable the plan cache");
    }

    [Fact]
    public void TupleContains_ConvertedComponent_ShouldBindProviderValue()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInConverterEntity>();
        var tuples = new List<(long, TupleInStatus)> { (1, TupleInStatus.Active) };

        var command = Prepare(ctx, e
            .Where(x => tuples.Contains(new ValueTuple<long, TupleInStatus>(x.Id, x.Status)))
            .Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, status) in (VALUES ($p0, $p1))");
        command.DbCommandParams.Cast<DbParameter>().Should().HaveCount(2);
        command.DbCommandParams[1].Value.Should().Be("Active", "the tuple component converter must bind the provider representation");
    }

    [Fact]
    public void TupleContains_NullableValueTupleElement_ShouldThrowNotSupported()
    {
        // W2: a captured List<(long,int?)?> routes into the tuple translator (Nullable is a tuple
        // family), which must reject the nullable element with the pinned message before any LHS/shape
        // validation. The explicit cast keeps the LHS a parameterised tuple construction.
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var tuples = new List<(long, int?)?>();

        var act = () => Prepare(ctx, e
            .Where(x => tuples.Contains(((long, int?)?)new ValueTuple<long, int?>(x.Id, x.Int)))
            .Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*nullable tuple*");
    }

    [Fact]
    public void TupleIn_TupleTypedLhs_ShouldThrowNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleEntity>();
        var tuples = new List<Tuple<int, string>> { Tuple.Create(1, "a") };

        var act = () => Prepare(ctx, e.Where(x => tuples.Contains(x.Pair)).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*left-hand side*");
    }

    [Fact]
    public void TupleContains_NullReferenceTupleEntry_ShouldThrowNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<Tuple<long, int>> { Tuple.Create(1L, 10), null! };

        var act = () => Prepare(ctx, e.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Code))).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*null tuple entry*");
    }

    [Fact]
    public void TupleContains_ValueTupleArityOne_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<ValueTuple<long>> { new(1L), new(2L) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long>(x.Id))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id) in (VALUES ($p0), ($p1))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void TupleContains_ValueTupleAritySeven_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int, int, int, int, int, int)> { (1L, 10, 1, 2, 3, 4, 5) };

        var command = Prepare(ctx, e
            .Where(x => tuples.Contains(new ValueTuple<long, int, int, int, int, int, int>(x.Id, x.Code, 1, 2, 3, 4, 5)))
            .Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code, 1, 2, 3, 4, 5) in (VALUES ($p0, $p1, $p2, $p3, $p4, $p5, $p6))");
        command.DbCommandParams.Cast<DbParameter>()
            .Should().HaveCount(7);
    }

    [Fact]
    public void TupleContains_ReferenceTupleArityThree_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<Tuple<long, int, long>> { Tuple.Create(1L, 10, 3L) };

        var command = Prepare(ctx, e
            .Where(x => tuples.Contains(Tuple.Create(x.Id, x.Code, 3L)))
            .Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code, 3) in (VALUES ($p0, $p1, $p2))");
    }

    [Fact]
    public void TupleContains_ValueTupleArityThree_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int, int)> { (1L, 10, 9), (2L, 20, 9) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int, int>(x.Id, x.Code, 9))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code, 9) in (VALUES ($p0, $p1, $p2), ($p3, $p4, $p5))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2", "p3", "p4", "p5");
    }

    [Fact]
    public void TupleContains_ValueTupleArityFour_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int, int, int)> { (1L, 10, 9, 8), (2L, 20, 9, 8) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int, int, int>(x.Id, x.Code, 9, 8))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code, 9, 8) in (VALUES ($p0, $p1, $p2, $p3), ($p4, $p5, $p6, $p7))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2", "p3", "p4", "p5", "p6", "p7");
    }

    [Fact]
    public void TupleContains_ValueTupleArityFive_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int, int, int, int)> { (1L, 10, 9, 8, 7), (2L, 20, 9, 8, 7) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int, int, int, int>(x.Id, x.Code, 9, 8, 7))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code, 9, 8, 7) in (VALUES ($p0, $p1, $p2, $p3, $p4), ($p5, $p6, $p7, $p8, $p9))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2", "p3", "p4", "p5", "p6", "p7", "p8", "p9");
    }

    [Fact]
    public void TupleContains_ValueTupleAritySix_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int, int, int, int, int)> { (1L, 10, 9, 8, 7, 6), (2L, 20, 9, 8, 7, 6) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int, int, int, int, int>(x.Id, x.Code, 9, 8, 7, 6))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code, 9, 8, 7, 6) in (VALUES ($p0, $p1, $p2, $p3, $p4, $p5), ($p6, $p7, $p8, $p9, $p10, $p11))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2", "p3", "p4", "p5", "p6", "p7", "p8", "p9", "p10", "p11");
    }

    [Fact]
    public void TupleContains_ReferenceTupleArityOne_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<Tuple<long>> { Tuple.Create(1L), Tuple.Create(2L) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(Tuple.Create(x.Id))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id) in (VALUES ($p0), ($p1))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void TupleContains_ReferenceTupleArityFour_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<Tuple<long, int, int, int>> { Tuple.Create(1L, 10, 9, 8), Tuple.Create(2L, 20, 9, 8) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Code, 9, 8))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code, 9, 8) in (VALUES ($p0, $p1, $p2, $p3), ($p4, $p5, $p6, $p7))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2", "p3", "p4", "p5", "p6", "p7");
    }

    [Fact]
    public void TupleContains_ReferenceTupleArityFive_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<Tuple<long, int, int, int, int>> { Tuple.Create(1L, 10, 9, 8, 7), Tuple.Create(2L, 20, 9, 8, 7) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Code, 9, 8, 7))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code, 9, 8, 7) in (VALUES ($p0, $p1, $p2, $p3, $p4), ($p5, $p6, $p7, $p8, $p9))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2", "p3", "p4", "p5", "p6", "p7", "p8", "p9");
    }

    [Fact]
    public void TupleContains_ReferenceTupleAritySix_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<Tuple<long, int, int, int, int, int>> { Tuple.Create(1L, 10, 9, 8, 7, 6), Tuple.Create(2L, 20, 9, 8, 7, 6) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Code, 9, 8, 7, 6))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code, 9, 8, 7, 6) in (VALUES ($p0, $p1, $p2, $p3, $p4, $p5), ($p6, $p7, $p8, $p9, $p10, $p11))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2", "p3", "p4", "p5", "p6", "p7", "p8", "p9", "p10", "p11");
    }

    [Fact]
    public void TupleContains_ReferenceTupleAritySeven_ShouldRenderValuesInList()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<Tuple<long, int, int, int, int, int, int>> { Tuple.Create(1L, 10, 9, 8, 7, 6, 5), Tuple.Create(2L, 20, 9, 8, 7, 6, 5) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Code, 9, 8, 7, 6, 5))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code, 9, 8, 7, 6, 5) in (VALUES ($p0, $p1, $p2, $p3, $p4, $p5, $p6), ($p7, $p8, $p9, $p10, $p11, $p12, $p13))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2", "p3", "p4", "p5", "p6", "p7", "p8", "p9", "p10", "p11", "p12", "p13");
    }

    [Fact]
    public void TupleContains_DefaultValueTupleEntry_ShouldRenderOrdinaryRow()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)> { default };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int>(x.Id, x.Code))).Select(x => new { x.Id }));

        // A `default` entry is an ordinary (0, 0) row: the non-null cells bind as parameters, not rejected.
        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code) in (VALUES ($p0, $p1))");
        command.DbCommandParams[0].Value.Should().Be(0L);
        command.DbCommandParams[1].Value.Should().Be(0);
    }

    [Fact]
    public void TupleContains_NullCollection_ShouldThrowArgumentNullException()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        List<(long, int)>? tuples = null;

        var act = () => Prepare(ctx, e
            .Where(x => tuples!.Contains(new ValueTuple<long, int>(x.Id, x.Code)))
            .Select(x => new { x.Id }));

        act.Should().Throw<ArgumentNullException>();
    }
}

public enum TupleInStatus
{
    Unknown,
    Active,
    Closed,
}

public sealed class TupleInStatusToStringConverter : ValueConverter<TupleInStatus, string>
{
    public override string? ConvertToProvider(TupleInStatus model) => model.ToString();

    public override TupleInStatus ConvertFromProvider(string? provider) => Enum.Parse<TupleInStatus>(provider!);
}

[SqlTable("tuple_in_converter_entity")]
public interface ITupleInConverterEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("status")]
    [ValueConverter(typeof(TupleInStatusToStringConverter))]
    TupleInStatus Status { get; set; }
}

[SqlTable("tuple_in_entity")]
public interface ITupleInMembershipEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("code")]
    int Code { get; set; }
}
