using System.Data.Common;
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
/// It also drives the real <see cref="QueryPlanStore"/> through the test-only forced-hash
/// <see cref="QueryPlan"/> seam, so it joins the "Query cache controls" collection that serializes
/// every test touching the process-wide plan store.
/// </summary>
[Collection("Query cache controls")]
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

    /// <summary>
    /// The forced-hash seam must not break the frozen-hash invariant: <c>GetCacheVersion</c> asserts
    /// <c>_hashPlan == ComputeHash(...)</c> (Debug), so a forced plan routed through the normal
    /// <c>GetCacheVersion</c> path must keep its forced hash instead of firing the assert. The hash is
    /// forced through <c>ComputeHash</c> semantics, not around them.
    /// </summary>
    [Fact]
    public void QueryPlan_ForcedHash_ShouldKeepFrozenHashThroughGetCacheVersion()
    {
        using var ctx = new InMemoryDataContext();
        var cmd = ctx.From<SimpleEntity>().Select(x => x.Id);
        cmd.PrepareCommand(CancellationToken.None);

        const int forcedHash = 0x0BAD_F00D;
        var plan = new QueryPlan(cmd, null, forcedHash);
        plan.GetHashCode().Should().Be(forcedHash);

        var original = plan.QueryCommand;
        var cached = plan.GetCacheVersion();

        cached.Should().BeSameAs(plan);
        plan.QueryCommand.Should().NotBeSameAs(original, "the live tree is swapped for a detached clone");
        plan.GetHashCode().Should().Be(forcedHash, "the forced hash is part of ComputeHash and stays frozen");
    }

    /// <summary>
    /// The plan store is a dictionary keyed by the structural hash, so two <em>unequal</em> plans that
    /// share one effective hash must still be stored and found independently. The forced-hash seam
    /// pins a real <see cref="QueryPlanStore.TryGet"/>/<c>Set</c> collision instead of only asserting
    /// the comparer-level invariant: store A, look up B (must miss), store B, then each key resolves
    /// to its own holder and an equivalent key to A still resolves to A.
    /// </summary>
    [Fact]
    public void QueryPlanStore_ForcedHashCollision_ShouldFallBackToStructuralEquality()
    {
        using var ctx = new InMemoryDataContext();
        var repo = new InMemoryRepository(ctx);

        var cmdA = repo.SimpleEntity.Select(x => x.Id);
        // A second, independently issued command from the same builder/config shape: a distinct
        // QueryCommand instance, unlike the production warm-up that reuses the very same command.
        // Reusing cmdA would make QueryPlan.Equals short-circuit on command reference equality and
        // never reach the structural fallback this test exists to pin.
        var cmdA2 = repo.SimpleEntity.Select(x => x.Id);
        var cmdB = repo.SimpleEntity.Where(x => x.Id > 0).Select(x => x.Id);
        cmdA.PrepareCommand(CancellationToken.None);
        cmdA2.PrepareCommand(CancellationToken.None);
        cmdB.PrepareCommand(CancellationToken.None);

        cmdA.Should().NotBeSameAs(cmdA2);
        cmdA.GetQueryPlanEqualityComparer().Equals(cmdA, cmdA2)
            .Should().BeTrue("the second command is the same query shape and must be structurally equal");

        // Drop any plan the prepares above may have memoized on this thread, then drive the store
        // exclusively through the forced-hash plans. Single-threaded: the store is [ThreadStatic],
        // and this class is serialized through the "Query cache controls" collection.
        QueryPlanStore.Clear();

        const int forcedHash = 0x5EED_1234;
        var contextType = typeof(PlanKeyStructureTests);
        var holderA = new StubCommandHolder();
        var holderB = new StubCommandHolder();

        var planA1 = new QueryPlan(cmdA, null, forcedHash);
        var planA2 = new QueryPlan(cmdA2, null, forcedHash); // equivalent key to planA1, distinct command
        var planB = new QueryPlan(cmdB, null, forcedHash);  // structurally unequal, same forced hash

        planA1.GetHashCode().Should().Be(forcedHash);
        planB.GetHashCode().Should().Be(forcedHash);
        planA1.Equals(planA2).Should().BeTrue("same command shape is the same key");
        planA1.Equals(planB).Should().BeFalse("different query shape must stay a different key");

        QueryPlanStore.Set(contextType, planA1, holderA);
        QueryPlanStore.TryGet(contextType, planB, out var collisionHolder, out var collisionPlan)
            .Should().BeFalse("an equal hash must not collapse two unequal keys");
        collisionHolder.Should().BeNull();
        collisionPlan.Should().BeNull();

        QueryPlanStore.Set(contextType, planB, holderB);

        QueryPlanStore.TryGet(contextType, planA1, out var foundA, out var storedA).Should().BeTrue();
        foundA.Should().BeSameAs(holderA);
        storedA.Should().BeSameAs(planA1);

        QueryPlanStore.TryGet(contextType, planA2, out var foundAEquiv, out var storedAEquiv).Should().BeTrue();
        foundAEquiv.Should().BeSameAs(holderA, "an equivalent key must find the first plan");
        storedAEquiv.Should().BeSameAs(planA1);

        QueryPlanStore.TryGet(contextType, planB, out var foundB, out var storedB).Should().BeTrue();
        foundB.Should().BeSameAs(holderB);
        storedB.Should().BeSameAs(planB);
    }

    private sealed class StubCommandHolder : IDbCommandHolder
    {
        public void ResetConnection(DbConnection conn, IDataContext dbContext)
        {
        }
    }

    /// <summary>
    /// Nested lambdas whose inner parameter shadows the outer one by name must compare structurally:
    /// the comparer maps parameters by scope (instance), so the shared name is irrelevant and the
    /// matching pair is equal with equal hashes. This is the shape the B1 parameter-scope allocation
    /// change must not alter.
    /// </summary>
    [Fact]
    public void Lambda_NestedShadowedParameter_ShouldCompareStructurally()
    {
        static Expression Nested(int constant)
        {
            var outer = Parameter(typeof(int), "x");
            var inner = Parameter(typeof(int), "x"); // same name, distinct instance: shadowing
            var innerLambda = Lambda<Func<int, int>>(Add(inner, Constant(constant)), inner);
            return Lambda<Func<int, int>>(Invoke(innerLambda, outer), outer);
        }

        var comparer = Comparer();
        var left = Nested(1);
        var right = Nested(1);

        left.Should().NotBeSameAs(right);
        comparer.Equals(left, right).Should().BeTrue("a shadowed parameter is compared by scope, not by name");
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    /// <summary>
    /// A nested parameter scope entered by a mismatching comparison must be fully unwound before the
    /// next comparison on the same comparer instance, otherwise a later matching nested tree would be
    /// reported unequal (B1 reentrancy/cleanup invariant).
    /// </summary>
    [Fact]
    public void Lambda_NestedScope_ShouldBeUnwoundAfterMismatch()
    {
        static Expression Nested(int constant)
        {
            var outer = Parameter(typeof(int), "x");
            var inner = Parameter(typeof(int), "x");
            var innerLambda = Lambda<Func<int, int>>(Add(inner, Constant(constant)), inner);
            return Lambda<Func<int, int>>(Invoke(innerLambda, outer), outer);
        }

        var comparer = Comparer();

        comparer.Equals(Nested(1), Nested(2)).Should().BeFalse();
        comparer.Equals(Nested(3), Nested(3)).Should()
            .BeTrue("the nested scope entered by the failed comparison must be unwound");
    }

    /// <summary>
    /// The plan lookup is a dictionary keyed by the structural hash, so a hash collision must still be
    /// rejected by the full structural <c>Equals</c>. There is no public seam to force a collision at
    /// the real lookup (the Stage A forced-collision row is a DO-&gt;PLAN item), so the comparer-level
    /// invariant "equal hashes never imply equal keys" is pinned here instead.
    /// </summary>
    [Fact]
    public void EqualHashes_ShouldNotImplyEquality_ForQueryableConstant()
    {
        var comparer = Comparer();
        var left = Constant(new List<int> { 1 }.AsQueryable(), typeof(IQueryable<int>));
        var right = Constant(new List<int> { 1, 2, 3 }.AsQueryable(), typeof(IQueryable<int>));

        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
        comparer.Equals(left, right).Should().BeFalse("equal hashes must never collapse two different keys");
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
