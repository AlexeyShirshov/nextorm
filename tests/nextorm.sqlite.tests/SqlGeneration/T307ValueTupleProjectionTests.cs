using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T307 (#307): a top-level <c>ValueTuple.Create</c> projection is an unsupported Select shape.
/// Before the fix it fell through the projection classification, produced an empty select list and
/// leaked the raw materializer <see cref="ArgumentException"/> ("Incorrect number of arguments for
/// constructor"). Preparation now refuses it with the same <see cref="NotSupportedException"/> the
/// explicit <c>new ValueTuple&lt;...&gt;(...)</c> constructor already produces, before dialect
/// dispatch. No database connection is opened: every negative case fails during preparation and
/// every accepted control only renders SQL.
/// </summary>
public class T307ValueTupleProjectionTests
{
    private const string PinnedTemplate =
        "The ValueTuple projection '{0}' at Select position 0 is not supported; project an explicit type or use Tuple.Create.";

    private const string TupleCreateSqliteMessage =
        "A row constructor in this position is not supported by provider 'SqliteDialect'; " +
        "it is supported only as a direct comparison operand in WHERE/HAVING/JOIN ON.";

    private const string ItemAccessSqliteMessage =
        "Positional tuple element access (.ItemN) on a server-side tuple is not supported by provider 'SqliteDialect'.";

    private static string Pinned(Type type) => string.Format(PinnedTemplate, type);

    private static void Prepare<T>(IDataContext ctx, QueryCommand<T> command)
        => _ = ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    /// <summary>
    /// AC02 red/green regression: the exact two-column factory projection from the defect report.
    /// The execution terminal is used on purpose: the old failure surfaced from the row
    /// materializer, and the accepted fix must stop it during preparation instead.
    /// </summary>
    [Fact]
    public void FactoryArityTwo_ThrowsPinnedDiagnostic()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<IComplexEntity>().OrderBy(x => x.Id)
            .Select(x => ValueTuple.Create(x.Id, x.String));

        var act = () => command.ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage(Pinned(typeof(ValueTuple<long, string>)))
            .Which.Message.Should().NotContain("Incorrect number of arguments");
    }

    /// <summary>
    /// AC03: every factory arity, including the 8-element <c>Rest</c> form, is refused during
    /// preparation with the frozen type-based diagnostic. The factory method itself is 1..8
    /// generic parameters; the value/reference mixtures and nullable components are deliberate.
    /// </summary>
    [Theory]
    [InlineData(1, typeof(ValueTuple<long>))]
    [InlineData(2, typeof(ValueTuple<long, string>))]
    [InlineData(3, typeof(ValueTuple<long, string, int?>))]
    [InlineData(4, typeof(ValueTuple<long, string, int?, bool?>))]
    [InlineData(5, typeof(ValueTuple<long, string, int?, bool?, DateTime?>))]
    [InlineData(6, typeof(ValueTuple<long, string, int?, bool?, DateTime?, long>))]
    [InlineData(7, typeof(ValueTuple<long, string, int?, bool?, DateTime?, long, string>))]
    [InlineData(8, typeof(ValueTuple<long, string, int?, bool?, DateTime?, long, string, ValueTuple<int?>>))]
    public void FactoryAritiesOneThroughEight_ThrowPinnedDiagnostic(int arity, Type expectedType)
    {
        using var ctx = SqliteTestContext.Create();
        var entity = ctx.From<IComplexEntity>();

        Action act = arity switch
        {
            1 => () => Prepare(ctx, entity.Select(x => ValueTuple.Create(x.Id))),
            2 => () => Prepare(ctx, entity.Select(x => ValueTuple.Create(x.Id, x.String))),
            3 => () => Prepare(ctx, entity.Select(x => ValueTuple.Create(x.Id, x.String, x.Int))),
            4 => () => Prepare(ctx, entity.Select(x => ValueTuple.Create(x.Id, x.String, x.Int, x.Boolean))),
            5 => () => Prepare(ctx, entity.Select(x => ValueTuple.Create(x.Id, x.String, x.Int, x.Boolean, x.Datetime))),
            6 => () => Prepare(ctx, entity.Select(x => ValueTuple.Create(x.Id, x.String, x.Int, x.Boolean, x.Datetime, x.Id))),
            7 => () => Prepare(ctx, entity.Select(x => ValueTuple.Create(x.Id, x.String, x.Int, x.Boolean, x.Datetime, x.Id, x.String))),
            8 => () => Prepare(ctx, entity.Select(x => ValueTuple.Create(x.Id, x.String, x.Int, x.Boolean, x.Datetime, x.Id, x.String, x.Int))),
            _ => throw new ArgumentOutOfRangeException(nameof(arity), arity, "arity must be 1..8"),
        };

        act.Should().Throw<NotSupportedException>().WithMessage(Pinned(expectedType));
    }

    /// <summary>
    /// AC03 control: the explicit constructor keeps its existing frozen refusal, byte for byte.
    /// </summary>
    [Fact]
    public void ExplicitConstructor_PreservesPinnedDiagnostic()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<IComplexEntity>()
            .Select(x => new ValueTuple<long, string>(x.Id, x.String!));

        FluentActions.Invoking(() => Prepare(ctx, command))
            .Should().Throw<NotSupportedException>()
            .WithMessage(Pinned(typeof(ValueTuple<long, string>)));
    }

    /// <summary>
    /// AC03 control: <c>Tuple.Create</c> in the Select list keeps the provider's existing
    /// row-constructor refusal; it is not a ValueTuple projection and must not adopt its message.
    /// </summary>
    [Fact]
    public void TupleFactory_PreservesSqliteDiagnostic()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<IComplexEntity>()
            .Select(x => Tuple.Create(x.Id, x.String));

        FluentActions.Invoking(() => Prepare(ctx, command))
            .Should().Throw<NotSupportedException>()
            .WithMessage(TupleCreateSqliteMessage);
    }

    /// <summary>
    /// AC03 control: ordinary anonymous, DTO and whole-entity projections still prepare.
    /// </summary>
    [Fact]
    public void AnonymousDtoAndEntity_Prepare()
    {
        using var ctx = SqliteTestContext.Create();
        var entity = ctx.From<IComplexEntity>();

        SqlOf(ctx, entity.Select(x => new { x.Id, x.String }))
            .Should().Contain("id").And.Contain("somestring");
        SqlOf(ctx, entity.Select(x => new T307Dto { Id = x.Id, Name = x.String }))
            .Should().Contain("id").And.Contain("Name");
        SqlOf(ctx, entity.Select(x => new T307ComplexEntity { Id = x.Id, String = x.String, Int = x.Int }))
            .Should().Contain("somestring").And.Contain("nullableint");
    }

    /// <summary>
    /// AC03 controls and the deferred nested shape: a non-top-level <c>ValueTuple.Create</c> keeps
    /// its current behaviour. The inline element access and the nested anonymous member keep their
    /// existing provider diagnostics (the top-level factory guard must not scan descendants), while
    /// an ordinary predicate still prepares.
    /// </summary>
    [Fact]
    public void InlineItemAccessAndPredicate_PreservePreparation()
    {
        using var ctx = SqliteTestContext.Create();
        var entity = ctx.From<IComplexEntity>();

        FluentActions.Invoking(() => Prepare(ctx,
                entity.Where(x => ValueTuple.Create(x.Id, x.String).Item1 > 1).Select(x => x.Id)))
            .Should().Throw<NotSupportedException>()
            .WithMessage(ItemAccessSqliteMessage);

        FluentActions.Invoking(() => Prepare(ctx,
                entity.Select(x => new { Pair = ValueTuple.Create(x.Id, x.String) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Property 'Pair'*is not supported*")
            .Which.Message.Should().NotContain("The ValueTuple projection");

        SqlOf(ctx, entity.Where(x => x.Id > 1).Select(x => x.Id))
            .Should().Contain("id");
    }
}

/// <summary>DTO control for the top-level factory refusal.</summary>
public sealed class T307Dto
{
    public long Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>Concrete whole-entity control (a mapped interface cannot be materialised as a whole).</summary>
[SqlTable("complex_entity")]
public sealed class T307ComplexEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("nullableint")]
    public int? Int { get; set; }

    [Column("somestring")]
    public string? String { get; set; }

    [Column("b")]
    public bool? Boolean { get; set; }

    [Column("dt")]
    public DateTime? Datetime { get; set; }
}
