using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T313 (#313): a bare scalar projection whose CLR result type is an enum must not fall through to
/// row materialization. The DateTimeOffset fail-closed guard and the enum refusal run in preparation,
/// before the column-shape classification, so the diagnostics are the frozen ones and every supported
/// projection shape keeps its existing behaviour. No database connection is opened: every negative
/// case fails during preparation and every positive case only renders SQL.
/// </summary>
public class T313ScalarProjectionPreparationTests
{
    // Frozen M literals, copied byte-for-byte from src/nextorm.core/Visitors/MemberTranslator.cs
    // (:211-215) and pinned in .pdca/T313/rv1/frozen-messages.json. The test owns its own copy so a
    // production wording change cannot silently follow along.
    private const string DayOfWeekMessage =
        "DateTimeOffset.DayOfWeek is not supported in a server-side query: it has no SQL translation " +
        "and evaluating it on the client would capture the client clock or time zone. " +
        "Translate it explicitly or project a supported date part instead.";

    private const string OffsetMessage =
        "DateTimeOffset.Offset is not supported in a server-side query: it has no SQL translation " +
        "and evaluating it on the client would capture the client clock or time zone. " +
        "Translate it explicitly or project a supported date part instead.";

    private const string TicksMessage =
        "DateTimeOffset.Ticks is not supported in a server-side query: it has no SQL translation " +
        "and evaluating it on the client would capture the client clock or time zone. " +
        "Translate it explicitly or project a supported date part instead.";

    // Frozen enum refusal message E (plan rv1): no such message exists in the lane before the fix.
    private const string EnumScalarMessage =
        "Enum scalar projections without a value converter are not supported in a server-side query.";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    /// <summary>
    /// Runs one requested terminal. <c>prepare</c> is the command terminal (the same preparation
    /// <c>EntityBuilder.ToCommand()</c> feeds, since <c>QueryCommand&lt;TResult&gt;</c> has no
    /// <c>ToCommand()</c> member); <c>ToList</c> and <c>First</c> are execution terminals.
    /// </summary>
    private static void RunTerminal<T>(IDataContext ctx, QueryCommand<T> command, string terminal)
    {
        switch (terminal)
        {
            case "prepare":
                _ = ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);
                break;
            case "ToList":
                _ = command.ToList();
                break;
            case "First":
                _ = command.First();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(terminal), terminal, "unknown terminal");
        }
    }

    /// <summary>
    /// AC01/AC02: a bare <see cref="DayOfWeek"/> scalar projection (a <see cref="DateTimeOffset"/> member
    /// with no server translation) must report the frozen guard message, not a CLR constructor failure,
    /// through both terminals. The mapped-column and captured-value sources are both checked because the
    /// defect is in the classification of the body, not in the receiver shape.
    /// </summary>
    [Theory]
    [InlineData("mapped", "prepare")]
    [InlineData("mapped", "ToList")]
    [InlineData("mapped", "First")]
    [InlineData("captured", "prepare")]
    [InlineData("captured", "ToList")]
    [InlineData("captured", "First")]
    public void D1a_BareDayOfWeek_UsesFrozenGuard(string source, string terminal)
    {
        using var ctx = SqliteTestContext.Create();
        var value = new DateTimeOffset(2023, 5, 17, 13, 45, 30, TimeSpan.FromHours(2));
        var builder = ctx.From<T313Entity>().Where(x => x.Id == 1);
        var command = source == "mapped"
            ? builder.Select(x => x.Created.DayOfWeek)
            : builder.Select(x => value.DayOfWeek);

        var act = () => RunTerminal(ctx, command, terminal);

        act.Should().Throw<NotSupportedException>().WithMessage(DayOfWeekMessage);
    }

    /// <summary>
    /// AC02: every projection form that already reached the guard keeps its exact message — anonymous
    /// projection, DTO, bare scalar (Offset/Ticks), predicate and First — and the supported date parts
    /// stay supported.
    /// </summary>
    [Fact]
    public void DateTimeOffsetControls_KeepFrozenMessages()
    {
        using var ctx = SqliteTestContext.Create();
        var builder = ctx.From<T313Entity>().Where(x => x.Id == 1);

        FluentActions.Invoking(() => SqlOf(ctx, builder.Select(x => new { D = x.Created.DayOfWeek })))
            .Should().Throw<NotSupportedException>().WithMessage(DayOfWeekMessage);

        FluentActions.Invoking(() => SqlOf(ctx, builder.Select(x => new T313DayDto { Day = x.Created.DayOfWeek })))
            .Should().Throw<NotSupportedException>().WithMessage(DayOfWeekMessage);

        FluentActions.Invoking(() => SqlOf(ctx, builder.Select(x => x.Created.Offset)))
            .Should().Throw<NotSupportedException>().WithMessage(OffsetMessage);

        FluentActions.Invoking(() => SqlOf(ctx, builder.Select(x => x.Created.Ticks)))
            .Should().Throw<NotSupportedException>().WithMessage(TicksMessage);

        FluentActions.Invoking(() => SqlOf(ctx, ctx.From<T313Entity>()
                .Where(x => x.Id == 1 && x.Created.DayOfWeek == DayOfWeek.Wednesday)
                .Select(x => x.Id)))
            .Should().Throw<NotSupportedException>().WithMessage(DayOfWeekMessage);


        // The captured value and the First terminal are covered by D1a (they share the same bare-scalar
        // defect); every assertion above is a form that already reported the guard before T313 and must
        // keep its exact wording.

        // Supported parts remain supported, for the mapped column and through the nullable wrapper.
        var partsSql = SqlOf(ctx, builder.Select(x => new
        {
            x.Created.Year,
            x.Created.Month,
            x.Created.Day,
            x.Created.Hour,
            x.Created.Minute,
            x.Created.Second,
            x.Created.DayOfYear,
        }));
        partsSql.Should().Contain("cast(strftime('%Y', created) as integer)")
            .And.Contain("cast(strftime('%j', created) as integer)");

        SqlOf(ctx, builder.Select(x => new { V = x.MaybeCreated!.Value.Year }))
            .Should().Contain("cast(strftime('%Y', maybe_created) as integer)");
    }

    /// <summary>AC03: a bare mapped enum scalar without a converter is refused with E.</summary>
    [Fact]
    public void BareEnum_UsesFrozenRefusal()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<T313Entity>().Select(x => x.State);

        var act = () => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage(EnumScalarMessage);
    }

    /// <summary>AC03: the nullable wrapper must not bypass the refusal.</summary>
    [Fact]
    public void NullableEnum_UsesFrozenRefusal()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<T313Entity>().Select(x => x.MaybeState);

        var act = () => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage(EnumScalarMessage);
    }

    /// <summary>AC04: a converter-mapped enum scalar keeps preparing (the converter boundary wins).</summary>
    [Fact]
    public void ConvertedEnum_RemainsSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var builder = ctx.From<T313Entity>().Where(x => x.Id == 1);

        SqlOf(ctx, builder.Select(x => x.ConvertedState)).Should().Contain("converted_state");

        SqlOf(ctx, builder.Select(x => new { x.ConvertedState })).Should().Contain("converted_state");
    }

    /// <summary>
    /// AC05: the refusal is scalar-only. Whole entity, DTO, tuple and identity projections keep their
    /// preparation, including their enum-valued members (through the converter that makes them a
    /// readable column type).
    /// </summary>
    [Fact]
    public void ProjectionShapes_RemainSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var builder = ctx.From<T313ConvertedEntity>().Where(x => x.Id == 1);

        SqlOf(ctx, builder.ToCommand()).Should().Contain("converted_state").And.Contain("created");

        // An identity Select(x => x) over an entity builder is a pre-existing, untested shape: in this
        // lane it prepares an empty select list ("select\n from t313_converted_entity ..."), before and
        // after T313 (the identity projection users actually get is From<T>().ToCommand(), asserted
        // above). The control here is that the new guards do not refuse it.
        FluentActions.Invoking(() => SqlOf(ctx, builder.Select(x => x))).Should().NotThrow();

        SqlOf(ctx, builder.Select(x => new T313ConvertedDto { Id = x.Id, Converted = x.ConvertedState }))
            .Should().Contain("converted_state");

        // A tuple projection is refused by the dialect's row-constructor rule before any enum check
        // (TupleSqlTranslator.RenderConstructor), unchanged by T313; see the boundary test below.
    }

    /// <summary>
    /// AC05 negative half: a bare enum <em>column</em> inside a whole-entity, DTO or tuple projection is
    /// not a scalar projection, so the new prepared refusal must not re-label it. Those shapes keep
    /// their pre-existing reader boundary (the column type the mapper cannot read, raised by
    /// <c>SelectExpression.GetDataRecordMethod</c>), unchanged by this fix and out of its footprint.
    /// </summary>
    [Fact]
    public void BareEnumEntityShapes_KeepPreExistingMapperBoundary()
    {
        using var ctx = SqliteTestContext.Create();
        var builder = ctx.From<T313Entity>().Where(x => x.Id == 1);

        var entityAct = () => SqlOf(ctx, builder.ToCommand());
        entityAct.Should().Throw<NotSupportedException>().WithMessage("*not supported*")
            .Which.Message.Should().NotBe(EnumScalarMessage);

        var dtoAct = () => SqlOf(ctx, builder.Select(x => new T313Dto { Id = x.Id, State = x.State }));
        dtoAct.Should().Throw<NotSupportedException>().WithMessage("*not supported*")
            .Which.Message.Should().NotBe(EnumScalarMessage);

        var tupleAct = () => SqlOf(ctx, builder.Select(x => Tuple.Create(x.Id, x.State)));
        tupleAct.Should().Throw<NotSupportedException>().WithMessage("*not supported*")
            .Which.Message.Should().NotBe(EnumScalarMessage);

        // The tuple refusal is shape-level, not enum-level: the converted enum is refused identically.
        var convertedTupleAct = () => SqlOf(ctx, ctx.From<T313ConvertedEntity>()
            .Select(x => Tuple.Create(x.Id, x.ConvertedState)));
        convertedTupleAct.Should().Throw<NotSupportedException>().WithMessage("*row constructor*")
            .Which.Message.Should().NotBe(EnumScalarMessage);
    }

    /// <summary>
    /// AC01/AC03: the refusal belongs to the preparation boundary, so the command terminal and both
    /// execution terminals report the identical frozen message.
    /// </summary>
    [Fact]
    public void CommandForms_UseSameBoundary()
    {
        using var ctx = SqliteTestContext.Create();

        var prepareAct = () => ctx.GetPreparedQueryCommand(
            ctx.From<T313Entity>().Select(x => x.State), false, false, CancellationToken.None);
        prepareAct.Should().Throw<NotSupportedException>().WithMessage(EnumScalarMessage);

        var listAct = () => { _ = ctx.From<T313Entity>().Select(x => x.State).ToList(); };
        listAct.Should().Throw<NotSupportedException>().WithMessage(EnumScalarMessage);

        var firstAct = () => { _ = ctx.From<T313Entity>().Select(x => x.State).First(); };
        firstAct.Should().Throw<NotSupportedException>().WithMessage(EnumScalarMessage);
    }

    /// <summary>
    /// AC06: the generated <c>Min</c>/<c>Max</c> bodies over a bare enum column are enum-typed scalar
    /// projections too, so they are refused with E rather than failing in the row mapper.
    /// </summary>
    [Fact]
    public void EnumAggregates_UseFrozenRefusal()
    {
        using var ctx = SqliteTestContext.Create();

        var minAct = () => { _ = ctx.From<T313Entity>().Min(x => x.State); };
        minAct.Should().Throw<NotSupportedException>().WithMessage(EnumScalarMessage);

        var maxAct = () => { _ = ctx.From<T313Entity>().Max(x => x.State); };
        maxAct.Should().Throw<NotSupportedException>().WithMessage(EnumScalarMessage);
    }
}

/// <summary>An enum stored as its underlying integer: no value converter is configured.</summary>
public enum T313State
{
    Unknown = 0,
    Active = 3,
    Closed = 9,
}

[SqlTable("t313_entity")]
public sealed class T313Entity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("state")]
    public T313State State { get; set; }

    [Column("maybe_state")]
    public T313State? MaybeState { get; set; }

    [Column("converted_state")]
    [ValueConverter(typeof(EnumToStringConverter<T313State>))]
    public T313State ConvertedState { get; set; }

    [Column("created")]
    public DateTimeOffset Created { get; set; }

    [Column("maybe_created")]
    public DateTimeOffset? MaybeCreated { get; set; }
}

/// <summary>A DTO projection carrying a <see cref="DayOfWeek"/> member (the guard must reach it).</summary>
public sealed class T313DayDto
{
    public DayOfWeek Day { get; set; }
}

/// <summary>A DTO projection carrying bare and converted enum members (both must stay supported).</summary>
public sealed class T313Dto
{
    public int Id { get; set; }

    public T313State State { get; set; }
}

/// <summary>
/// The converted-enum fixture: every enum-valued member is readable through its value converter, so
/// whole-entity, DTO, tuple and identity shapes have a supported column type (T313 shape controls).
/// </summary>
[SqlTable("t313_converted_entity")]
public sealed class T313ConvertedEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("converted_state")]
    [ValueConverter(typeof(EnumToStringConverter<T313State>))]
    public T313State ConvertedState { get; set; }

    [Column("created")]
    public DateTimeOffset Created { get; set; }
}

/// <summary>A DTO projection carrying a converted enum member.</summary>
public sealed class T313ConvertedDto
{
    public int Id { get; set; }

    public T313State Converted { get; set; }
}
