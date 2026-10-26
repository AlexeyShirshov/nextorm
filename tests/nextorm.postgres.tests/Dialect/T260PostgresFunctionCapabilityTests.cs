using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Postgres.Tests;

/// <summary>
/// T260 (#260): PostgreSQL function availability. <c>array_reverse</c> does not exist in any current
/// PostgreSQL release and is refused on every configured version; <c>array_sort</c> and <c>uuidv7</c>
/// exist from PostgreSQL 18, so they render only for an explicit 18+ version and are refused below
/// that and when the version is unset. The refusal is a plain <see cref="NotSupportedException"/> raised
/// at preparation (rendering), never a driver-level <c>42883</c> and never a semantic substitution.
/// </summary>
public class T260PostgresFunctionCapabilityTests
{
    private const string ArrayReverseRefusal =
        "The PostgreSQL function 'array_reverse' is not supported by this provider.";

    private const string ArraySortRefusal =
        "The PostgreSQL function 'array_sort' is not supported by this provider.";

    private const string UuidV7Refusal =
        "The uuidv7 function is not supported by this provider.";

    private static IDataContext Context<TMarker>(Version? serverVersion)
        => PostgresTestContext.CreateVersioned<TMarker>(serverVersion);

    private static string PreparedSql<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    private static void AssertRefused<T>(IDataContext ctx, QueryCommand<T> command, string message)
    {
        var prepare = () => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);
        prepare.Should().Throw<NotSupportedException>().WithMessage(message);
    }

    // V01/V02/V03 — array_reverse refuses on every PostgreSQL version, configured or unset.
    [Fact]
    public void ArrayReverse_Pg17_ShouldRefuse()
    {
        using var ctx = Context<Pg17Marker>(new Version(17, 0));
        var values = new[] { 1L, 2L, 3L };

        AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(values) }), ArrayReverseRefusal);
    }

    [Fact]
    public void ArrayReverse_Pg18_ShouldRefuse()
    {
        using var ctx = Context<Pg18Marker>(new Version(18, 0));
        var values = new[] { 1L, 2L, 3L };

        AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(values) }), ArrayReverseRefusal);
    }

    [Fact]
    public void ArrayReverse_Unset_ShouldRefuse()
    {
        using var ctx = Context<UnsetMarker>(null);
        var values = new[] { 1L, 2L, 3L };

        AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(values) }), ArrayReverseRefusal);
    }

    // V04/V05/V06 — array_sort: refused below 18 and unset; native rendering only for explicit 18+.
    [Fact]
    public void ArraySort_Pg17_ShouldRefuse()
    {
        using var ctx = Context<Pg17Marker>(new Version(17, 0));
        var values = new[] { 3L, 1L, 2L };

        AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(values) }), ArraySortRefusal);
    }

    [Fact]
    public void ArraySort_Pg17999_ShouldRefuseBeforeTheBoundary()
    {
        using var ctx = Context<Pg17999Marker>(new Version(17, 999));
        var values = new[] { 3L, 1L, 2L };

        AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(values) }), ArraySortRefusal);
    }

    [Fact]
    public void ArraySort_Pg18_ShouldRenderNative()
    {
        using var ctx = Context<Pg18Marker>(new Version(18, 0));
        var entity = ctx.From<IComplexEntity>();

        var sql = PreparedSql(ctx, entity.Select(x => new { V = SqlFunctions.Postgres.array_sort(SqlFunctions.Parameter<long[]>(9)) }));

        sql.Should().Contain("array_sort(@norm_p9)");
    }

    [Fact]
    public void ArraySort_Unset_ShouldRefuse()
    {
        using var ctx = Context<UnsetMarker>(null);
        var values = new[] { 3L, 1L, 2L };

        AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(values) }), ArraySortRefusal);
    }

    // V07/V08/V09 — uuidv7: refused below 18 and unset; native rendering only for explicit 18+.
    [Fact]
    public void UuidV7_Pg17_ShouldRefuse()
    {
        using var ctx = Context<Pg17Marker>(new Version(17, 0));

        AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Sql.uuidv7() }), UuidV7Refusal);
    }

    [Fact]
    public void UuidV7_Pg17999_ShouldRefuseBeforeTheBoundary()
    {
        using var ctx = Context<Pg17999Marker>(new Version(17, 999));

        AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Sql.uuidv7() }), UuidV7Refusal);
    }

    [Fact]
    public void UuidV7_Pg18_ShouldRenderNative()
    {
        using var ctx = Context<Pg18Marker>(new Version(18, 0));

        var sql = PreparedSql(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Sql.uuidv7() }));

        sql.Should().Contain("uuidv7()");
    }

    [Fact]
    public void UuidV7_Unset_ShouldRefuse()
    {
        using var ctx = Context<UnsetMarker>(null);

        AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Sql.uuidv7() }), UuidV7Refusal);
    }

    // V17 — the capability and the direct renderer agree, so no entry point bypasses the decision.
    [Fact]
    public void CapabilityAndDirectRenderer_ShouldAgree()
    {
        var pg17 = new PostgresDialect(new Version(17, 0));
        pg17.SupportsArrayFunction("array_reverse").Should().BeFalse();
        pg17.SupportsArrayFunction("array_sort").Should().BeFalse();
        pg17.UuidGenerators!.Supports("uuidv7").Should().BeFalse();

        var pg18 = new PostgresDialect(new Version(18, 0));
        pg18.SupportsArrayFunction("array_reverse").Should().BeFalse();
        pg18.SupportsArrayFunction("array_sort").Should().BeTrue();
        pg18.SupportsArrayFunction("array_length").Should().BeTrue("only the two guarded names are gated");
        pg18.UuidGenerators!.Supports("uuidv7").Should().BeTrue();
        pg18.UuidGenerators!.Render("uuidv7").Should().Be("uuidv7()");

        var unset = PostgresDialect.Instance;
        unset.SupportsArrayFunction("array_sort").Should().BeFalse();
        unset.UuidGenerators!.Supports("uuidv7").Should().BeFalse();
        unset.UuidGenerators!.Supports("gen_random_uuid").Should().BeTrue();
        unset.UuidGenerators!.Render("gen_random_uuid").Should().Be("gen_random_uuid()");
    }

    private sealed class Pg17Marker
    {
    }

    private sealed class Pg17999Marker
    {
    }

    private sealed class Pg18Marker
    {
    }

    private sealed class UnsetMarker
    {
    }
}
