using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// The version gate of the native <c>arrayExcept</c> (issue #252). A declared ClickHouse 25.9+ server
/// renders the native call; a declared older version refuses with the required and the declared
/// version before any SQL is emitted; an undeclared dialect keeps the historical message byte for
/// byte. The declaration is per concrete context type, because the plan cache is keyed by it.
/// </summary>
public class ClickHouseArrayExceptVersionGateTests
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm";

    private static readonly Version Version259 = new(25, 9);
    private static readonly Version Version258 = new(25, 8);

    private const string DeclaredOldMessage =
        "The array_except function requires ClickHouse version 25.9 or later; declared server version: 25.8.";

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void Declared25_9_ShouldRenderTheNativeFunction()
    {
        using var ctx = new Declared259Context();
        var e = ctx.From<IArrayEntity>();

        var two = SqlOf(ctx, e.Select(x => new
        {
            Difference = SqlFunctions.ClickHouse.array_except(x.Nums, x.Nums)
        }));
        two.Should().Contain("arrayExcept(nums, nums)");

        ctx.Dialect.ScalarFunctions!.Supports("array_except").Should().BeTrue(
            "25.9 is the release that introduced arrayExcept");
        ctx.Dialect.ScalarFunctions!.GetUnsupportedReason("array_except").Should().BeNull(
            "a supported name has no refusal reason");
        ((ClickHouseDialect)ctx.Dialect).SupportsClickHouse25_9Functions.Should().BeTrue(
            "the dialect capability flag must agree with the scalar table");
    }

    // #252: the native arrayExcept takes exactly two arrays. A declared 25.9+ context must fail closed
    // before any SQL for every other arity the public params T[][] signature can express — the same
    // "never emit SQL the server rejects" rule the version gate exists for.
    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    [InlineData(0)]
    public void Declared25_9_ShouldRefuseUnsupportedAritiesBeforeAnySql(int arity)
    {
        using var ctx = new Declared259Context();
        var e = ctx.From<IArrayEntity>();
        var message =
            $"The array_except function is not supported by this provider with {arity} array argument(s): " +
            "ClickHouse's arrayExcept takes exactly two arrays.";

        Action act = arity switch
        {
            3 => () => SqlOf(ctx, e.Select(x => new
            {
                Difference = SqlFunctions.ClickHouse.array_except(x.Nums, x.Nums, x.Nums)
            })),
            1 => () => SqlOf(ctx, e.Select(x => new
            {
                Difference = SqlFunctions.ClickHouse.array_except(x.Nums)
            })),
            0 => () => SqlOf(ctx, e.Select(x => new
            {
                Difference = SqlFunctions.ClickHouse.array_except<long>()
            })),
            _ => throw new ArgumentOutOfRangeException(nameof(arity), arity, "not part of the arity matrix")
        };

        act.Should().Throw<NotSupportedException>().WithMessage(message);
    }

    // AC8: nullability of the elements does not change the capability or the spelling. A declared 25.9
    // context renders the same native call for an Array(Nullable(Int64)) / Array(Nullable(String))
    // projection; the live nullable semantics are pinned in the integration arm.
    [Fact]
    public void Declared25_9_ShouldRenderNullableElementArraysWithTheSameSpelling()
    {
        using var ctx = new Declared259Context();
        var e = ctx.From<IT351NullableArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Longs = SqlFunctions.ClickHouse.array_except(x.Nums, x.Nums),
            Strings = SqlFunctions.ClickHouse.array_except(x.Tags, x.Tags)
        }));

        sql.Should().Contain("arrayExcept(nnums, nnums)");
        sql.Should().Contain("arrayExcept(ntags, ntags)");
    }

    [Fact]
    public void Declared25_9_DirectRender_ShouldRefuseUnsupportedAritiesToo()
    {
        var scalars = new ClickHouseDialect(Version259).ScalarFunctions!;

        scalars.Render("array_except", ["a", "b"]).Should().Be("arrayExcept(a, b)");
        FluentActions.Invoking(() => scalars.Render("array_except", ["a", "b", "c"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage(
                "The array_except function is not supported by ClickHouse with 3 array argument(s): " +
                "ClickHouse's arrayExcept takes exactly two arrays.");
        FluentActions.Invoking(() => scalars.Render("array_except", []))
            .Should().Throw<NotSupportedException>()
            .WithMessage(
                "The array_except function is not supported by ClickHouse with 0 array argument(s): " +
                "ClickHouse's arrayExcept takes exactly two arrays.");
    }

    [Fact]
    public void Declared25_8_ShouldRefuseWithTheRequiredAndDeclaredVersion()
    {
        using var ctx = new Declared258Context();
        var e = ctx.From<IArrayEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
        {
            Difference = SqlFunctions.ClickHouse.array_except(x.Nums, x.Nums)
        }))).Should().Throw<NotSupportedException>().WithMessage(DeclaredOldMessage);

        var scalars = ctx.Dialect.ScalarFunctions!;
        scalars.Supports("array_except").Should().BeFalse();
        scalars.GetUnsupportedReason("array_except").Should().Be(DeclaredOldMessage);
        FluentActions.Invoking(() => scalars.Render("array_except", ["a", "b"]))
            .Should().Throw<NotSupportedException>().WithMessage(DeclaredOldMessage);
    }

    [Fact]
    public void Undeclared_ShouldKeepTodaysMessages()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
        {
            Difference = SqlFunctions.ClickHouse.array_except(x.Nums, x.Nums)
        }))).Should().Throw<NotSupportedException>()
            .WithMessage("The array_except function is not supported by this provider.");

        var scalars = ClickHouseDialect.Instance.ScalarFunctions!;
        scalars.Supports("array_except").Should().BeFalse(
            "an unspecified version must not silently enable a 25.9 function");
        ClickHouseDialect.Instance.SupportsClickHouse25_9Functions.Should().BeFalse(
            "the shared dialect stays on the pinned baseline");
        scalars.GetUnsupportedReason("array_except").Should().BeNull(
            "the undeclared refusal keeps the generic message");
        FluentActions.Invoking(() => scalars.Render("array_except", ["a", "b"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The array_except function is not supported by ClickHouse.");
    }

    [Fact]
    public void OneContextType_ShouldOwnOneDeclaration()
    {
        using var first = new GuardedContext(Version259);
        using var repeated = new GuardedContext(Version259);

        FluentActions.Invoking(() => new GuardedContext(Version258))
            .Should().Throw<InvalidOperationException>()
            .WithMessage(
                "*already bound to server version 25.9*25.8*derive a distinct DataContext subclass for each version*");
    }

    /// <summary>A nullable-element array shape: <c>Array(Nullable(Int64))</c> / <c>Array(Nullable(String))</c>.</summary>
    [SqlTable("t351_nullable_array")]
    internal interface IT351NullableArrayEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("nnums")]
        long?[] Nums { get; set; }

        [Column("ntags")]
        string?[] Tags { get; set; }
    }

    /// <summary>Distinct declaration per concrete context type: the guard is keyed by the type.</summary>
    private sealed class Declared259Context : ClickHouseDataContext
    {
        public Declared259Context()
            : base(Version259, PlaceholderConnectionString, new DataContextBuilder())
        {
        }
    }

    private sealed class Declared258Context : ClickHouseDataContext
    {
        public Declared258Context()
            : base(Version258, PlaceholderConnectionString, new DataContextBuilder())
        {
        }
    }

    /// <summary>The single type the declaration-conflict test drives with two different versions.</summary>
    private sealed class GuardedContext : ClickHouseDataContext
    {
        public GuardedContext(Version version)
            : base(version, PlaceholderConnectionString, new DataContextBuilder())
        {
        }
    }
}
