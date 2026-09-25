using System.Linq.Expressions;
using FluentAssertions;
using static System.Linq.Expressions.Expression;

namespace NextORM.Core.Tests;

/// <summary>
/// Structural-comparison coverage for the plan key beyond the "happy path" already covered by
/// <see cref="ExpressionPlanEqualityComparerNodeTests"/>: the mismatch branches of the private
/// <c>ExpressionComparer</c> (a different child kind/type, null conversion, member-binding lists of
/// different shape, switch/catch/element-init counts) must all report "not equal" instead of throwing
/// or - worse - reporting equal. Also covers <see cref="QueryPlan"/> identity and the
/// <c>LinqSource</c> FROM branch, which is only produced by the in-memory SelectMany/GroupJoin path.
/// </summary>
public class PlanKeyStructureTests
{
    private static ExpressionPlanEqualityComparer Comparer() => new(new QueryProvider());

    // A pair of structurally different trees must never be reported equal, whatever the node kind.
    private static void NotEqual(Expression left, Expression right)
        => Comparer().Equals(left, right).Should().BeFalse();

    [Fact]
    public void NestedChildren_DifferentKindOrType_ShouldNotBeEqual()
    {
        // Different child node kind (Constant vs Parameter) reaches the nested node-type guard.
        NotEqual(Add(Constant(1), Constant(2)), Add(Parameter(typeof(int), "p"), Constant(2)));

        // Same child node kind, different CLR type reaches the nested type guard.
        NotEqual(Add(Constant(1), Constant(2)), Add(Constant(1L), Constant(2L)));

        // One side has a conversion lambda and the other does not: Compare(null conversion, lambda).
        var noConversion = Coalesce(Constant(null, typeof(string)), Constant("x"));
        var withConversion = Coalesce(
            Constant(null, typeof(string)),
            Constant("x"),
            Lambda<Func<string, string>>(Parameter(typeof(string), "s"), Parameter(typeof(string), "s")));
        NotEqual(noConversion, withConversion);
    }

    [Fact]
    public void Lambda_ParameterScope_ShouldNotLeakAcrossComparisons()
    {
        var comparer = Comparer();

        // Compare a lambda that introduces a parameter, then a mismatching pair that fails inside the
        // scope, then a matching pair again: the parameter scope must have been unwound in between.
        var p1 = Parameter(typeof(int), "x");
        var lambda1 = Lambda<Func<int, int>>(Add(p1, Constant(1)), p1);
        var p2 = Parameter(typeof(int), "x");
        var lambda2 = Lambda<Func<int, int>>(Add(p2, Constant(2)), p2);

        comparer.Equals(lambda1, lambda1).Should().BeTrue();
        comparer.Equals(lambda1, lambda2).Should().BeFalse();
        comparer.Equals(lambda1, lambda1).Should().BeTrue();
    }

    [Fact]
    public void New_WithAndWithoutMemberList_ShouldNotBeEqual()
    {
        var ctor = typeof(KeyValuePair<int, int>).GetConstructor([typeof(int), typeof(int)])!;
        var members = new[]
        {
            typeof(KeyValuePair<int, int>).GetProperty(nameof(KeyValuePair<int, int>.Key))!,
            typeof(KeyValuePair<int, int>).GetProperty(nameof(KeyValuePair<int, int>.Value))!,
        };

        var withoutMembers = New(ctor, Constant(1), Constant(2));
        var withMembers = New(ctor, new Expression[] { Constant(1), Constant(2) }, members);

        NotEqual(withoutMembers, withMembers);
    }

    private sealed class TwoProps
    {
        public int A { get; set; }
        public int B { get; set; }
    }

    [Fact]
    public void MemberInit_DifferentMembers_ShouldNotBeEqual()
    {
        var a = typeof(TwoProps).GetProperty(nameof(TwoProps.A))!;
        var b = typeof(TwoProps).GetProperty(nameof(TwoProps.B))!;

        var bindA = MemberInit(New(typeof(TwoProps)), Bind(a, Constant(1)));
        var bindB = MemberInit(New(typeof(TwoProps)), Bind(b, Constant(1)));

        NotEqual(bindA, bindB);

        // Different binding count.
        var single = MemberInit(New(typeof(TwoProps)), Bind(a, Constant(1)));
        var both = MemberInit(
            New(typeof(TwoProps)),
            Bind(a, Constant(1)),
            Bind(b, Constant(2)));
        NotEqual(single, both);
    }

    [Fact]
    public void MemberInit_SameBindings_ShouldBeEqualAndShareTheList()
    {
        // Passing the same binding array to two MemberInit calls makes the comparer take the
        // reference-equal fast path for the binding list instead of the element walk.
        var a = typeof(TwoProps).GetProperty(nameof(TwoProps.A))!;
        var bindings = new MemberBinding[] { Bind(a, Constant(1)) };

        var left = MemberInit(New(typeof(TwoProps)), bindings);
        var right = MemberInit(New(typeof(TwoProps)), bindings);

        Comparer().Equals(left, right).Should().BeTrue();
    }

    [Fact]
    public void ListInit_DifferentElementCounts_ShouldNotBeEqual()
    {
        var add = typeof(List<int>).GetMethod(nameof(List<int>.Add))!;

        var one = ListInit(New(typeof(List<int>)), ElementInit(add, Constant(1)));
        var two = ListInit(
            New(typeof(List<int>)),
            ElementInit(add, Constant(1)),
            ElementInit(add, Constant(2)));

        NotEqual(one, two);

        // Same initializer list instance: reference-equal fast path.
        var initializers = new[] { ElementInit(add, Constant(1)) };
        var left = ListInit(New(typeof(List<int>)), initializers);
        var right = ListInit(New(typeof(List<int>)), initializers);
        Comparer().Equals(left, right).Should().BeTrue();
    }

    [Fact]
    public void Switch_DifferentCases_ShouldNotBeEqual()
    {
        // Different case count.
        var one = Switch(Constant(1), Constant(0), SwitchCase(Constant(3), Constant(1)));
        var two = Switch(
            Constant(1),
            Constant(0),
            SwitchCase(Constant(3), Constant(1)),
            SwitchCase(Constant(4), Constant(2)));
        NotEqual(one, two);

        // Same case count, different test value.
        var three = Switch(Constant(1), Constant(0), SwitchCase(Constant(4), Constant(1)));
        NotEqual(one, three);
    }

    [Fact]
    public void Try_DifferentCatches_ShouldNotBeEqual()
    {
        // Different catch count.
        var one = TryCatch(Empty(), Catch(typeof(InvalidOperationException), Empty()));
        var two = TryCatch(
            Empty(),
            Catch(typeof(InvalidOperationException), Empty()),
            Catch(typeof(ArgumentException), Empty()));
        NotEqual(one, two);

        // Same catch count, different exception type.
        var three = TryCatch(Empty(), Catch(typeof(ArgumentException), Empty()));
        NotEqual(one, three);
    }

    [Fact]
    public void ListInit_DifferentElement_ShouldNotBeEqual()
    {
        var add = typeof(List<int>).GetMethod(nameof(List<int>.Add))!;

        var withOne = ListInit(New(typeof(List<int>)), ElementInit(add, Constant(1)));
        var withTwo = ListInit(New(typeof(List<int>)), ElementInit(add, Constant(2)));

        NotEqual(withOne, withTwo);
    }

    [Fact]
    public void New_DifferentMemberOrder_ShouldNotBeEqual()
    {
        var ctor = typeof(KeyValuePair<int, int>).GetConstructor([typeof(int), typeof(int)])!;
        var key = typeof(KeyValuePair<int, int>).GetProperty(nameof(KeyValuePair<int, int>.Key))!;
        var value = typeof(KeyValuePair<int, int>).GetProperty(nameof(KeyValuePair<int, int>.Value))!;

        var forward = New(ctor, new Expression[] { Constant(1), Constant(2) }, new[] { key, value });
        var reversed = New(ctor, new Expression[] { Constant(1), Constant(2) }, new[] { value, key });

        NotEqual(forward, reversed);
    }

    private sealed class WithList
    {
        public List<int> Items { get; set; } = new();
    }

    [Fact]
    public void MemberInit_DifferentBindingKinds_ShouldNotBeEqual()
    {
        var items = typeof(WithList).GetProperty(nameof(WithList.Items))!;
        var add = typeof(List<int>).GetMethod(nameof(List<int>.Add))!;

        var assignment = MemberInit(New(typeof(WithList)), Bind(items, New(typeof(List<int>))));
        var listBinding = MemberInit(New(typeof(WithList)), ListBind(items, ElementInit(add, Constant(1))));

        NotEqual(assignment, listBinding);
    }

    [Fact]
    public void QueryPlan_ShouldCompareByCommandAndSql()
    {
        using var ctx = new InMemoryDataContext();
        var repo = new InMemoryRepository(ctx);
        var cmd = repo.SimpleEntity.Select(x => x.Id);
        cmd.PrepareCommand(CancellationToken.None);

        var planA = new QueryPlan(cmd, null);
        var planB = new QueryPlan(cmd, null);

        planA!.Equals(planB).Should().BeTrue("two plans for the same prepared command are the same key");
        planA.Equals((QueryPlan?)null).Should().BeFalse();

        // The object overload is part of the plan's equality surface too.
        object? planObj = planA;
        planObj!.Equals(null).Should().BeFalse();
        planObj!.Equals("not a plan").Should().BeFalse();
        planA!.GetHashCode().Should().Be(planA.GetHashCode());

        // The SQL participates in the key: the same command rendered differently is a different plan.
        var renderedA = new QueryPlan(cmd, "select 1");
        var renderedB = new QueryPlan(cmd, "select 2");

        renderedA.Equals(renderedB).Should().BeFalse();
        renderedA.GetHashCode().Should().NotBe(renderedB.GetHashCode());
    }

    [Fact]
    public void QueryPlan_GetCacheVersion_ShouldKeepIdentity()
    {
        using var ctx = new InMemoryDataContext();
        var cmd = ctx.From<SimpleEntity>().Select(x => x.Id);
        cmd.PrepareCommand(CancellationToken.None);

        var plan = new QueryPlan(cmd, null);
        var hash = plan.GetHashCode();
        var original = plan.QueryCommand;

        var cached = plan.GetCacheVersion();

        cached.Should().BeSameAs(plan);
        plan.QueryCommand.Should().NotBeSameAs(original, "the live tree is swapped for a detached clone");
        plan.GetHashCode().Should().Be(hash, "plan identity must survive the cache-version swap");
    }

    [Fact]
    public void LinqSourceFrom_ShouldCompareByReference()
    {
        using var ctx = new InMemoryDataContext();
        var items = new[] { 1, 2 };

        var first = ctx.From<SimpleEntity>().SelectMany(_ => items).Select(x => x);
        var second = ctx.From<SimpleEntity>().SelectMany(_ => items).Select(x => x);
        first.PrepareCommand(CancellationToken.None);
        second.PrepareCommand(CancellationToken.None);

        var fromComparer = first.GetFromExpressionPlanEqualityComparer();
        fromComparer.Equals(first.From, first.From).Should().BeTrue();
        fromComparer.Equals(first.From, second.From).Should().BeFalse(
            "a computed SelectMany source is per-command and must not share a cached plan");
    }
}
