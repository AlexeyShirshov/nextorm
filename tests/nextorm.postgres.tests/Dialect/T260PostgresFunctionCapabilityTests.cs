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

        // "Plain" is exactly the framework exception, never a provider- or driver-specific subclass.
        prepare.Should().ThrowExactly<NotSupportedException>().WithMessage(message);
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

    // V12/V13/V14 — the per-name gate runs before the operand is translated, so the argument shape
    // cannot change the answer: an inline literal array, a null reference, nullable elements, an empty
    // array, a NULL element, duplicates, a single element and value-/reference-element arrays are all
    // refused on 17.0, on unset and (for array_reverse) on explicit 18.0. On 18.0 array_sort keeps the
    // native function and binds the whole array as one parameter; that arm asserts SQL text only and
    // executes nothing.
    [Fact]
    public void UnavailableArrayFunction_AllArgumentForms_ShouldRefuse()
    {
        // Declared non-nullable so the call binds the non-nullable overload; the runtime value is null.
        long[] nullReference = null!;
        long?[] nullableElements = new long?[] { 1L, null, 3L };
        long[] empty = Array.Empty<long>();
        long?[] nullElement = new long?[] { 1L, null };
        long[] duplicates = new long[] { 2L, 2L, 1L };
        long[] single = new long[] { 7L };
        long[] valueElements = new long[] { 1L, 2L, 3L };
        int[] intElements = new int[] { 1, 2, 3 };
        string[] referenceElements = new string[] { "b", "a" };

        // The gate answers by function name, so every argument shape gets the same answer; only the
        // enabled 18.0 array_sort arm asks for rendered SQL instead of a refusal (V12/V13).
        void AssertEveryShapeRefused(IDataContext ctx, bool sortEnabled = false)
        {
            void AssertSortRefused<T>(QueryCommand<T> command)
            {
                if (!sortEnabled)
                    AssertRefused(ctx, command, ArraySortRefusal);
            }

            // inline literal array (V12)
            AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(new[] { 1L, 2L, 3L }) }), ArrayReverseRefusal);
            AssertSortRefused(ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(new[] { 3L, 1L, 2L }) }));

            // null reference: refused, never a null short-circuit or an unchanged array (V12)
            AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(nullReference) }), ArrayReverseRefusal);
            AssertSortRefused(ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(nullReference) }));

            // nullable-element array (V12)
            AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(nullableElements) }), ArrayReverseRefusal);
            AssertSortRefused(ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(nullableElements) }));

            // empty array (V13)
            AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(empty) }), ArrayReverseRefusal);
            AssertSortRefused(ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(empty) }));

            // array containing a NULL element (V13)
            AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(nullElement) }), ArrayReverseRefusal);
            AssertSortRefused(ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(nullElement) }));

            // duplicate elements (V13)
            AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(duplicates) }), ArrayReverseRefusal);
            AssertSortRefused(ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(duplicates) }));

            // single element (V13)
            AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(single) }), ArrayReverseRefusal);
            AssertSortRefused(ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(single) }));

            // value-element arrays: long[] and int[] (V14)
            AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(valueElements) }), ArrayReverseRefusal);
            AssertSortRefused(ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(valueElements) }));
            AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(intElements) }), ArrayReverseRefusal);
            AssertSortRefused(ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(intElements) }));

            // reference-element array (V14)
            AssertRefused(ctx, ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(referenceElements) }), ArrayReverseRefusal);
            AssertSortRefused(ctx.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(referenceElements) }));
        }

        using (var pg17 = Context<Pg17Marker>(new Version(17, 0)))
            AssertEveryShapeRefused(pg17);

        using (var unset = Context<UnsetMarker>(null))
            AssertEveryShapeRefused(unset);

        using (var pg18 = Context<Pg18Marker>(new Version(18, 0)))
        {
            // array_reverse refuses on every version, including the explicit 18.0 arm (V02); array_sort
            // is enabled here, so only the reverse shapes are asserted as refusals.
            AssertEveryShapeRefused(pg18, sortEnabled: true);

            // array_sort is enabled here: each shape keeps the native function and one array parameter.
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(new[] { 3L, 1L, 2L }) })).Should().MatchRegex(@"array_sort\(@\w+\)");
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(nullReference) })).Should().MatchRegex(@"array_sort\(@\w+\)");
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(nullableElements) })).Should().MatchRegex(@"array_sort\(@\w+\)");
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(empty) })).Should().MatchRegex(@"array_sort\(@\w+\)");
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(nullElement) })).Should().MatchRegex(@"array_sort\(@\w+\)");
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(duplicates) })).Should().MatchRegex(@"array_sort\(@\w+\)");
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(single) })).Should().MatchRegex(@"array_sort\(@\w+\)");
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(valueElements) })).Should().MatchRegex(@"array_sort\(@\w+\)");
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(intElements) })).Should().MatchRegex(@"array_sort\(@\w+\)");
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(referenceElements) })).Should().MatchRegex(@"array_sort\(@\w+\)");
        }
    }

    // V15 — the PostgreSQL façade declares no separate UUID surface of its own: PostgresFunctions
    // inherits CommonFunctions.uuidv7, so `SqlFunctions.Postgres.uuidv7()` and `SqlFunctions.Sql.uuidv7()`
    // are the same single route, reached through that inherited member. No reflection is used; both
    // expressions below are compiled routes and must answer identically.
    [Fact]
    public void UuidV7_FacadeRoutes_ShouldRespectCapability()
    {
        using (var unset = Context<UnsetMarker>(null))
        {
            AssertRefused(unset, unset.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Sql.uuidv7() }), UuidV7Refusal);
            AssertRefused(unset, unset.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.uuidv7() }), UuidV7Refusal);
        }

        using (var pg17 = Context<Pg17Marker>(new Version(17, 0)))
        {
            AssertRefused(pg17, pg17.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Sql.uuidv7() }), UuidV7Refusal);
            AssertRefused(pg17, pg17.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.uuidv7() }), UuidV7Refusal);
        }

        using (var pg18 = Context<Pg18Marker>(new Version(18, 0)))
        {
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Sql.uuidv7() })).Should().Contain("uuidv7()");
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.uuidv7() })).Should().Contain("uuidv7()");
        }
    }

    // V26 — a nested/multidimensional operand follows the same gate: refused on the disabled routes,
    // rendered unchanged on the enabled native route. The multidimensional semantics stay deferred
    // (V25/V26); this case asserts the observed translator behaviour and executes nothing.
    [Fact]
    public void NestedArrayOperand_ShouldRefuseOrRenderWithoutCrash()
    {
        long[][] nested = new long[][] { new long[] { 1L, 2L }, new long[] { 3L } };

        using (var pg17 = Context<Pg17Marker>(new Version(17, 0)))
        {
            AssertRefused(pg17, pg17.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(nested) }), ArrayReverseRefusal);
            AssertRefused(pg17, pg17.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(nested) }), ArraySortRefusal);
        }

        using (var unset = Context<UnsetMarker>(null))
        {
            AssertRefused(unset, unset.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(nested) }), ArrayReverseRefusal);
            AssertRefused(unset, unset.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(nested) }), ArraySortRefusal);
        }

        using (var pg18 = Context<Pg18Marker>(new Version(18, 0)))
        {
            AssertRefused(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_reverse(nested) }), ArrayReverseRefusal);
            PreparedSql(pg18, pg18.From<IComplexEntity>().Select(x => new { V = SqlFunctions.Postgres.array_sort(nested) })).Should().MatchRegex(@"array_sort\(@\w+\)");
        }
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
