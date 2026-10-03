using System.Collections;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #148-B R2.4: the checked Int32 narrowing of a navigation <c>Count()</c>/property <c>Count</c> is
/// execution-visible through every consumer. The four consumers (scalar projection, predicate,
/// boolean projection, arithmetic) are exercised over a controlled stub scalar source that feeds
/// <see cref="int.MaxValue"/> + 1 through the real lowering/materialiser/compiled-expression path, so
/// each one must surface a top-level <see cref="OverflowException"/> instead of wrapping, truncating
/// or reading a wrong boolean. <c>LongCount()</c> stays a true 64-bit count.
/// </summary>
[Collection("Query cache controls")]
public class ImplicitNavigationR24Tests
{
    public ImplicitNavigationR24Tests() => DataContextCache.Clear();

    private const long Wide = (long)int.MaxValue + 1;

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<R24Parent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<R24Child>();
        ctx.From<R24Parent>().WithData([new R24Parent { Id = 1 }, new R24Parent { Id = 2 }]);
        ctx.From<R24Child>().WithData([new R24Child { Id = 10, ParentId = 1 }, new R24Child { Id = 11, ParentId = 1 }]);
        return ctx;
    }

    /// <summary>
    /// Replaces the cached correlated plan of the long-typed count subcommand so its scalar source
    /// yields <see cref="Wide"/>. This is a stub scalar source, not a direct call to the narrowing
    /// helper: preparation, correlation rewrite, projection compilation and the four consumers all run
    /// through the ordinary in-memory pipeline.
    /// </summary>
    private static void StubWideCount<T>(InMemoryDataContext ctx, QueryCommand<T> command)
    {
        ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        var field = typeof(InMemoryDataContext).GetField("_correlatedPlans", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var plans = (IDictionary)field.GetValue(ctx)!;
        var stubbed = 0;

        foreach (DictionaryEntry entry in plans)
        {
            var plan = entry.Value!;
            var command0 = (QueryCommand)plan.GetType().GetProperty("Command")!.GetValue(plan)!;
            var resultType = command0.GetType().IsGenericType ? command0.GetType().GetGenericArguments()[0] : null;
            if (resultType != typeof(long))
                continue;

            var enumerate = plan.GetType().GetField("<Enumerate>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
            enumerate.SetValue(plan, (Func<object?[], IEnumerator>)(_ => new object?[] { Wide }.GetEnumerator()));
            stubbed++;
        }

        stubbed.Should().BeGreaterThan(0, "the navigation count must have registered a long-typed correlated plan");
    }

    // ---- normal values: both Count forms through all four consumers ------------------------------

    [Fact]
    public void Count_dot_and_property_should_compose_in_scalar_predicate_bool_and_arithmetic()
    {
        using var ctx = CreateContext();

        var scalar = ctx.From<R24Parent>().OrderBy(p => p.Id).Select(p => p.Children.Count()).ToList();
        scalar.Should().Equal(2, 0);
        var scalarProp = ctx.From<R24Parent>().OrderBy(p => p.Id).Select(p => p.Children.Count).ToList();
        scalarProp.Should().Equal(2, 0);

        ctx.From<R24Parent>().Where(p => p.Children.Count() > 1).Select(p => p.Id).ToList().Should().Equal(1);
        ctx.From<R24Parent>().Where(p => p.Children.Count > 1).Select(p => p.Id).ToList().Should().Equal(1);

        var boolean = ctx.From<R24Parent>().OrderBy(p => p.Id).Select(p => new { B = p.Children.Count() == 2 }).ToList();
        boolean.Select(r => r.B).Should().Equal(true, false);
        var booleanProp = ctx.From<R24Parent>().OrderBy(p => p.Id).Select(p => new { B = p.Children.Count == 2 }).ToList();
        booleanProp.Select(r => r.B).Should().Equal(true, false);

        var arithmetic = ctx.From<R24Parent>().OrderBy(p => p.Id).Select(p => new { V = p.Children.Count() + 1 }).ToList();
        arithmetic.Select(r => r.V).Should().Equal(3, 1);
        var arithmeticProp = ctx.From<R24Parent>().OrderBy(p => p.Id).Select(p => new { V = p.Children.Count + 1 }).ToList();
        arithmeticProp.Select(r => r.V).Should().Equal(3, 1);
    }

    // ---- wide value: top-level OverflowException per consumer ------------------------------------

    [Fact]
    public void Wide_Count_scalar_projection_should_throw_overflow()
    {
        using var ctx = CreateContext();
        var count = ctx.From<R24Parent>().Select(p => p.Children.Count());
        StubWideCount(ctx, count);
        var act = () => count.ToList();
        act.Should().Throw<OverflowException>();

        var countProp = ctx.From<R24Parent>().Select(p => p.Children.Count);
        StubWideCount(ctx, countProp);
        var actProp = () => countProp.ToList();
        actProp.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Wide_Count_predicate_should_throw_overflow()
    {
        using var ctx = CreateContext();
        var count = ctx.From<R24Parent>().Where(p => p.Children.Count() > 1).ToCommand();
        StubWideCount(ctx, count);
        var act = () => count.ToList();
        act.Should().Throw<OverflowException>();

        var countProp = ctx.From<R24Parent>().Where(p => p.Children.Count > 1).ToCommand();
        StubWideCount(ctx, countProp);
        var actProp = () => countProp.ToList();
        actProp.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Wide_Count_boolean_projection_should_throw_overflow()
    {
        using var ctx = CreateContext();
        var count = ctx.From<R24Parent>().Select(p => new { B = p.Children.Count() == 2 });
        StubWideCount(ctx, count);
        var act = () => count.ToList();
        act.Should().Throw<OverflowException>();

        var countProp = ctx.From<R24Parent>().Select(p => new { B = p.Children.Count == 2 });
        StubWideCount(ctx, countProp);
        var actProp = () => countProp.ToList();
        actProp.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Wide_Count_arithmetic_should_throw_overflow()
    {
        using var ctx = CreateContext();
        var count = ctx.From<R24Parent>().Select(p => new { V = p.Children.Count() + 1 });
        StubWideCount(ctx, count);
        var act = () => count.ToList();
        act.Should().Throw<OverflowException>();

        var countProp = ctx.From<R24Parent>().Select(p => new { V = p.Children.Count + 1 });
        StubWideCount(ctx, countProp);
        var actProp = () => countProp.ToList();
        actProp.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Wide_LongCount_should_not_overflow_and_stay_64_bit()
    {
        using var ctx = CreateContext();
        var longCount = ctx.From<R24Parent>().Where(p => p.Id == 1).Select(p => p.Children.LongCount());
        StubWideCount(ctx, longCount);

        var rows = longCount.ToList();

        rows.Should().Equal(Wide);
    }
}

[SqlTable("r24_parent")]
public sealed class R24Parent
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    public ICollection<R24Child> Children { get; set; } = new List<R24Child>();
}

[SqlTable("r24_child")]
public sealed class R24Child
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_id")]
    public int ParentId { get; set; }
}
