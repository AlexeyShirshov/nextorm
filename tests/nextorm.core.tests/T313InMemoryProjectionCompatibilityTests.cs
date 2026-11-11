using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// T313 (#313) in-memory compatibility control. The fix lives in
/// <c>QueryCommand.QueryPreparer.PrepareColumns</c>, which the in-memory provider also routes through,
/// so the plan makes in-memory a compatibility control: behaviour that worked in this lane must keep
/// working, and a change requires a replan instead of being accepted here.
/// <para>
/// Measured on this lane (see .pdca/T313/rv1/logs/C-pre.log without the D2 guards and C.log with
/// them): a bare enum scalar and a bare <see cref="DayOfWeek"/> scalar already failed before the fix
/// (the same <c>QueryPreparationException: Cannot get ctor from …</c>); they now fail with the frozen
/// E/M messages, which is the fix reaching the same route. The nullable-enum scalar and a
/// <c>.Offset</c> access inside a projection used to succeed and are now refused — those two are the
/// STOP condition and stay asserted against the pre-fix behaviour until the plan decides.
/// </para>
/// </summary>
public class T313InMemoryProjectionCompatibilityTests
{
    private const string EnumScalarMessage =
        "Enum scalar projections without a value converter are not supported in a server-side query.";

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<T313MemEntity>().WithData(
        [
            new T313MemEntity
            {
                Id = 1,
                State = T313MemState.Active,
                MaybeState = T313MemState.Closed,
                Created = new DateTimeOffset(2023, 5, 17, 13, 45, 30, TimeSpan.FromHours(2)),
            },
            new T313MemEntity
            {
                Id = 2,
                State = T313MemState.Unknown,
                MaybeState = null,
                Created = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
        ]);
        return ctx;
    }

    /// <summary>
    /// Lane behaviour, unchanged: the in-memory provider never reaches the relational row mapper, so a
    /// bare enum scalar keeps its pre-existing preparation failure (a CLR constructor error, not E).
    /// The r2 gate keeps the new refusal out of this path entirely.
    /// </summary>
    [Fact]
    public void BareEnumScalar_ShouldKeepTheLanePreparationFailure()
    {
        using var ctx = CreateContext();

        var act = () => { _ = ctx.From<T313MemEntity>().OrderBy(x => x.Id).Select(x => x.State).ToList(); };

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*Cannot get ctor from*")
            .Which.Message.Should().NotContain(EnumScalarMessage);
    }

    /// <summary>
    /// Lane behaviour, unchanged: a bare <see cref="DayOfWeek"/> scalar keeps the same pre-existing
    /// preparation failure instead of the relational guard message.
    /// </summary>
    [Fact]
    public void DateTimeOffsetDayOfWeek_ShouldKeepTheLanePreparationFailure()
    {
        using var ctx = CreateContext();

        var act = () => { _ = ctx.From<T313MemEntity>().OrderBy(x => x.Id).Select(x => x.Created.DayOfWeek).ToList(); };

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*Cannot get ctor from System.DayOfWeek*")
            .Which.Message.Should().NotContain("DateTimeOffset.DayOfWeek is not supported");
    }

    /// <summary>
    /// STOP CONDITION: the nullable-enum scalar projected fine in this lane before the D2 guards and is
    /// refused now. The assertion keeps the pre-fix contract so the change cannot pass unnoticed.
    /// </summary>
    [Fact]
    public void NullableEnumScalar_ShouldStillProjectInMemory()
    {
        using var ctx = CreateContext();

        var states = ctx.From<T313MemEntity>().OrderBy(x => x.Id).Select(x => x.MaybeState).ToList();

        states.Should().Equal(T313MemState.Closed, null);
    }

    /// <summary>
    /// STOP CONDITION: a DateTimeOffset member with no translation inside a projection was evaluated by
    /// the in-memory provider before the D2 guards and is refused now. Kept as the pre-fix contract.
    /// </summary>
    [Fact]
    public void UnsupportedDateTimeOffsetMember_ShouldStillEvaluateInMemory()
    {
        using var ctx = CreateContext();

        var offsets = ctx.From<T313MemEntity>().OrderBy(x => x.Id).Select(x => x.Created.Offset.TotalMinutes).ToList();

        offsets.Should().Equal(120d, 0d);
    }

    /// <summary>Unchanged: the projection shapes keep their in-memory materialization.</summary>
    [Fact]
    public void Shapes_ShouldStillMaterializeInMemory()
    {
        using var ctx = CreateContext();

        var anonymous = ctx.From<T313MemEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.State }).ToList();
        anonymous.Should().HaveCount(2);
        anonymous[0].State.Should().Be(T313MemState.Active);

        var dto = ctx.From<T313MemEntity>().OrderBy(x => x.Id).Select(x => new T313MemDto { Id = x.Id, State = x.State }).ToList();
        dto.Should().HaveCount(2);
        dto[1].State.Should().Be(T313MemState.Unknown);

        var entities = ctx.From<T313MemEntity>().OrderBy(x => x.Id).ToList();
        entities.Should().HaveCount(2);
        entities[0].State.Should().Be(T313MemState.Active);

        var identity = ctx.From<T313MemEntity>().OrderBy(x => x.Id).Select(x => x).ToList();
        identity.Should().HaveCount(2);
    }
}

/// <summary>An enum member of the in-memory compatibility fixture (no converter, no SQL).</summary>
public enum T313MemState
{
    Unknown = 0,
    Active = 3,
    Closed = 9,
}

/// <summary>The in-memory compatibility entity: enum, nullable enum and DateTimeOffset members.</summary>
public sealed class T313MemEntity
{
    public int Id { get; set; }

    public T313MemState State { get; set; }

    public T313MemState? MaybeState { get; set; }

    public DateTimeOffset Created { get; set; }
}

/// <summary>A DTO carrying an enum member for the in-memory projection control.</summary>
public sealed class T313MemDto
{
    public int Id { get; set; }

    public T313MemState State { get; set; }
}
