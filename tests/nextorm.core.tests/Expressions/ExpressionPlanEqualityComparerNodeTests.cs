using FluentAssertions;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace NextORM.Core.Tests;

/// <summary>
/// Coverage for the private <c>ExpressionComparer</c>/<c>Visitor</c> types inside
/// <see cref="ExpressionPlanEqualityComparer"/>: they are only reachable through the public
/// <see cref="ExpressionPlanEqualityComparer.Equals(Expression?, Expression?)"/> and
/// <see cref="ExpressionPlanEqualityComparer.GetHashCode(Expression)"/> methods, so every
/// expression node kind is fed through both of them here.
/// </summary>
/// <remarks>
/// Joins the "Query cache controls" collection so the process-wide cached-path counters
/// (<see cref="ExpressionPlanEqualityComparer.SpillCount"/>/<c>ResetCounters</c>) can be reset and
/// read without another test class spilling concurrently. The collection disables parallelization.
/// </remarks>
[Collection("Query cache controls")]
public class ExpressionPlanEqualityComparerNodeTests
{
    private static ExpressionPlanEqualityComparer CreateComparer() => new(new QueryProvider());

    /// <summary>The two trees must be structurally equal but different instances.</summary>
    private static void AssertEqual(Func<Expression> factory)
    {
        var comparer = CreateComparer();
        var (left, right) = (factory(), factory());

        left.Should().NotBeSameAs(right);

        comparer.Equals(left, right).Should().BeTrue();
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    private static void AssertEqual(Expression left, Expression right)
    {
        var comparer = CreateComparer();

        left.Should().NotBeSameAs(right);

        comparer.Equals(left, right).Should().BeTrue();
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    private static void AssertNotEqual(Expression left, Expression right)
    {
        var comparer = CreateComparer();

        comparer.Equals(left, right).Should().BeFalse();
    }

    [Fact]
    public void Equals_NullAndReferenceCases() => TestNullAndReferenceCases();

    private static void TestNullAndReferenceCases()
    {
        var comparer = CreateComparer();
        var expression = Constant(1);

        comparer.Equals(expression, expression).Should().BeTrue();
        comparer.Equals(null, null).Should().BeTrue();
        comparer.Equals(expression, null).Should().BeFalse();
        comparer.Equals(null, expression).Should().BeFalse();
        comparer.Equals(Constant(1), Constant("a")).Should().BeFalse("node types differ");
        comparer.Equals(Constant(1), Constant(2L)).Should().BeFalse("expression types differ");
        comparer.GetHashCode(null!).Should().Be(0);
    }

    [Fact]
    public void Constant_ShouldBeCompared()
    {
        AssertEqual(() => Constant(42));
        AssertEqual(() => Constant("text"));
        AssertEqual(() => Constant(null, typeof(string)));
        AssertEqual(() => Constant(new byte[] { 1, 2, 3 }));
        AssertNotEqual(Constant(1), Constant(2));
    }

    [Fact]
    public void Parameter_ShouldBeCompared()
    {
        AssertEqual(() => Parameter(typeof(int), "i"));

        // Parameter names are intentionally ignored: only the type participates in the comparison.
        CreateComparer().Equals(Parameter(typeof(int), "i"), Parameter(typeof(int), "j")).Should().BeTrue();

        CreateComparer().GetHashCode(Parameter(typeof(int), "i"))
            .Should().Be(CreateComparer().GetHashCode(Parameter(typeof(int), "j")));
    }

    [Fact]
    public void Binary_ShouldBeCompared()
    {
        AssertEqual(() => Add(Constant(1), Constant(2)));
        AssertEqual(() => Equal(Constant(1), Constant(1)));
        AssertEqual(() => Coalesce(Constant(null, typeof(string)), Constant("x")));
        AssertEqual(() =>
        {
            var p = Parameter(typeof(string), "s");
            return Coalesce(Constant(null, typeof(string)), Constant("x"), Lambda<Func<string, string>>(p, p));
        });
        AssertNotEqual(Add(Constant(1), Constant(2)), Add(Constant(1), Constant(3)));
        AssertNotEqual(Add(Constant(1), Constant(2)), Subtract(Constant(1), Constant(2)));
    }

    [Fact]
    public void Unary_ShouldBeCompared()
    {
        AssertEqual(() => Convert(Constant(1), typeof(long)));
        AssertEqual(() => Not(Constant(true)));
        AssertNotEqual(Convert(Constant(1), typeof(long)), Not(Constant(true)));
    }

    [Fact]
    public void Conditional_ShouldBeCompared()
    {
        AssertEqual(() => Condition(Constant(true), Constant(1), Constant(2)));
        AssertNotEqual(
            Condition(Constant(true), Constant(1), Constant(2)),
            Condition(Constant(false), Constant(1), Constant(2)));
    }

    [Fact]
    public void Block_ShouldBeCompared()
    {
        AssertEqual(() => Block(Constant(1)));
        AssertEqual(() => Block(new[] { Parameter(typeof(int), "v") }, Constant(1)));
        AssertNotEqual(Block(Constant(1)), Block(Constant(2)));
    }

    [Fact]
    public void Default_ShouldBeCompared() => AssertEqual(() => Default(typeof(int)));

    [Fact]
    public void Label_ShouldBeCompared()
    {
        var intTarget = Label(typeof(int), "lbl");
        var voidTarget = Label(typeof(void), "voidLbl");

        AssertEqual(() => Label(intTarget, Constant(1)));
        AssertEqual(() => Label(voidTarget));
    }

    [Fact]
    public void Goto_ShouldBeCompared()
    {
        var target = Label(typeof(int), "lbl");

        AssertEqual(() => Goto(target, Constant(1)));
    }

    [Fact]
    public void Loop_ShouldBeCompared()
    {
        var breakLabel = Label(typeof(void), "break");
        var continueLabel = Label(typeof(void), "continue");

        AssertEqual(() => Loop(Empty(), breakLabel, continueLabel));
    }

    [Fact]
    public void Index_ShouldBeCompared()
    {
        var itemProperty = typeof(List<int>).GetProperty("Item")!;
        Expression Receiver() => Parameter(typeof(List<int>), "l");

        AssertEqual(() => MakeIndex(Receiver(), itemProperty, new Expression[] { Constant(0) }));
        AssertNotEqual(
            MakeIndex(Receiver(), itemProperty, new Expression[] { Constant(0) }),
            MakeIndex(Receiver(), itemProperty, new Expression[] { Constant(1) }));
    }

    [Fact]
    public void Invocation_ShouldBeCompared()
    {
        AssertEqual(() => Invoke(Lambda<Func<int>>(Constant(1))));
    }

    [Fact]
    public void Lambda_ShouldBeCompared()
    {
        AssertEqual(() => Lambda<Func<int>>(Constant(1)));
        AssertEqual(() =>
        {
            var p = Parameter(typeof(int), "x");
            return Lambda<Func<int, int>>(Add(p, Constant(1)), p);
        });
        AssertEqual(() =>
        {
            var (p1, p2) = (Parameter(typeof(int), "a"), Parameter(typeof(int), "b"));
            return Lambda<Func<int, int, int>>(Add(p1, p2), p1, p2);
        });
    }

    [Fact]
    public void Member_ShouldBeCompared()
    {
        var idProperty = typeof(SimpleEntity).GetProperty(nameof(SimpleEntity.Id))!;

        // Constant receiver: compared by member type/name (the expression instance is not compared).
        AssertEqual(() => Property(Constant(new SimpleEntity()), idProperty));
        AssertEqual(() => Property(Constant(new SimpleEntity()), idProperty));

        // Non-constant receiver: compared by member and receiver.
        AssertEqual(() => Property(Parameter(typeof(SimpleEntity), "e"), idProperty));
        AssertNotEqual(
            Property(Parameter(typeof(SimpleEntity), "e"), idProperty),
            Property(Parameter(typeof(string), "s"), typeof(string).GetProperty(nameof(string.Length))!));
    }

    [Fact]
    public void MethodCall_ShouldBeCompared()
    {
        var abs = typeof(Math).GetMethod(nameof(Math.Abs), new[] { typeof(int) })!;
        var toString = typeof(string).GetMethod(nameof(ToString), Type.EmptyTypes)!;

        AssertEqual(() => Call(abs, Constant(-1)));
        AssertEqual(() => Call(Constant("a"), toString));
        AssertNotEqual(Call(abs, Constant(-1)), Call(abs, Constant(-2)));
    }

    [Fact]
    public void New_ShouldBeCompared()
    {
        AssertEqual(() => New(typeof(SimpleEntity)));

        var ctor = typeof(KeyValuePair<int, int>).GetConstructor(new[] { typeof(int), typeof(int) })!;
        var members = new[]
        {
            typeof(KeyValuePair<int, int>).GetProperty(nameof(KeyValuePair<int, int>.Key))!,
            typeof(KeyValuePair<int, int>).GetProperty(nameof(KeyValuePair<int, int>.Value))!,
        };

        AssertEqual(() => New(ctor, new Expression[] { Constant(1), Constant(2) }, members));
    }

    [Fact]
    public void NewArray_ShouldBeCompared()
    {
        AssertEqual(() => NewArrayInit(typeof(int), Constant(1), Constant(2)));
        AssertNotEqual(
            NewArrayInit(typeof(int), Constant(1)),
            NewArrayInit(typeof(int), Constant(1), Constant(2)));
    }

    [Fact]
    public void ListInit_ShouldBeCompared()
    {
        var add = typeof(List<int>).GetMethod(nameof(List<int>.Add))!;

        AssertEqual(() => ListInit(
            New(typeof(List<int>)),
            ElementInit(add, Constant(1))));
    }

    [Fact]
    public void MemberInit_Assignment_ShouldBeCompared()
    {
        var idProperty = typeof(BindingTarget).GetProperty(nameof(BindingTarget.Id))!;

        AssertEqual(() => MemberInit(
            New(typeof(BindingTarget)),
            Bind(idProperty, Constant(1))));
    }

    [Fact]
    public void MemberInit_ListBinding_ShouldBeCompared()
    {
        var itemsProperty = typeof(BindingTarget).GetProperty(nameof(BindingTarget.Items))!;
        var add = typeof(List<int>).GetMethod(nameof(List<int>.Add))!;

        AssertEqual(() => MemberInit(
            New(typeof(BindingTarget)),
            ListBind(itemsProperty, ElementInit(add, Constant(1)))));
    }

    [Fact]
    public void MemberInit_MemberBinding_ShouldBeCompared()
    {
        var nestedProperty = typeof(BindingTarget).GetProperty(nameof(BindingTarget.Nested))!;
        var valueProperty = typeof(NestedTarget).GetProperty(nameof(NestedTarget.Value))!;

        AssertEqual(() => MemberInit(
            New(typeof(BindingTarget)),
            MemberBind(nestedProperty, Bind(valueProperty, Constant(1)))));
    }

    [Fact]
    public void MemberInit_DifferentBindingCount_ShouldNotBeEqual()
    {
        var idProperty = typeof(BindingTarget).GetProperty(nameof(BindingTarget.Id))!;
        var nestedProperty = typeof(BindingTarget).GetProperty(nameof(BindingTarget.Nested))!;

        AssertNotEqual(
            MemberInit(New(typeof(BindingTarget)), Bind(idProperty, Constant(1))),
            MemberInit(
                New(typeof(BindingTarget)),
                Bind(idProperty, Constant(1)),
                Bind(nestedProperty, New(typeof(NestedTarget)))));
    }

    [Fact]
    public void RuntimeVariables_ShouldBeCompared()
    {
        AssertEqual(() => RuntimeVariables(Parameter(typeof(int), "v")));
    }

    [Fact]
    public void Switch_ShouldBeCompared()
    {
        AssertEqual(() => Switch(Constant(1), Constant(2), SwitchCase(Constant(3), Constant(1))));
        AssertEqual(() => Switch(Constant(1), Default(typeof(int))));
    }

    [Fact]
    public void Try_ShouldBeCompared()
    {
        AssertEqual(() => TryCatch(Empty(), Catch(typeof(Exception), Empty())));
        AssertEqual(() => TryCatch(
            Empty(),
            Catch(Parameter(typeof(Exception), "ex"), Empty(), Constant(true))));
        AssertEqual(() => TryFinally(Empty(), Empty()));
        AssertEqual(() => TryFault(Empty(), Empty()));
        AssertEqual(() => TryCatchFinally(Empty(), Empty(), Catch(typeof(Exception), Empty())));
    }

    [Fact]
    public void TypeBinary_ShouldBeCompared()
    {
        AssertEqual(() => TypeIs(Parameter(typeof(object), "o"), typeof(string)));
        AssertEqual(() => TypeEqual(Parameter(typeof(object), "o"), typeof(object)));
    }

    [Fact]
    public void DifferentShape_ShouldNotBeEqual()
    {
        AssertNotEqual(Constant(1), Add(Constant(1), Constant(2)));
        AssertNotEqual(Lambda<Func<int>>(Constant(1)), Lambda<Func<int>>(Constant(2)));
        AssertNotEqual(
            Lambda<Func<int>>(Constant(1)),
            Invoke(Lambda<Func<int>>(Constant(1))));
    }

    [Fact]
    public void GetHashCode_QueryableConstant_ShouldIgnoreValue()
    {
        var comparer = CreateComparer();

        var left = Constant(new List<int> { 1 }.AsQueryable(), typeof(IQueryable<int>));
        var right = Constant(new List<int> { 1, 2, 3 }.AsQueryable(), typeof(IQueryable<int>));

        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
        comparer.Equals(left, right).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_ShouldUseStructuralEqualityForArrays()
    {
        var comparer = CreateComparer();

        comparer.GetHashCode(Constant(new byte[] { 1, 2, 3 }))
            .Should().Be(comparer.GetHashCode(Constant(new byte[] { 1, 2, 3 })));
    }

    [Fact]
    public void GetHashCode_DifferentTrees_ShouldDiffer()
    {
        var comparer = CreateComparer();

        comparer.GetHashCode(Add(Constant(1), Constant(2)))
            .Should().NotBe(comparer.GetHashCode(Add(Constant(1), Constant(3))));
    }

    // A lambda of `arity` int parameters whose body sums them with `seed`. Every parameter of one
    // lambda is registered before its body is compared, so `arity` is the number of live concurrent
    // bindings the scope sees: <=4 stays inline, the 5th spills into a dictionary and stays there.
    private static Expression FlatLambda(int arity, int seed)
    {
        var parameters = new ParameterExpression[arity];
        for (var i = 0; i < arity; i++)
        {
            parameters[i] = Parameter(typeof(int), "p" + i);
        }

        Expression body = Constant(seed);
        for (var i = 0; i < arity; i++)
        {
            body = Add(body, parameters[i]);
        }

        var typeArguments = new Type[arity + 1];
        for (var i = 0; i < arity; i++)
        {
            typeArguments[i] = typeof(int);
        }

        typeArguments[arity] = typeof(int);

        return Lambda(GetFuncType(typeArguments), body, parameters);
    }

    // Outer lambda with three parameters whose body invokes an inner lambda with three more
    // parameters, then reads an outer parameter again: the deepest comparison holds six concurrent
    // bindings (a one-way spill past the inline capacity of four) and must unwind both scopes.
    private static Expression NestedSpillLambda(int seed)
    {
        var o0 = Parameter(typeof(int), "o0");
        var o1 = Parameter(typeof(int), "o1");
        var o2 = Parameter(typeof(int), "o2");
        var i0 = Parameter(typeof(int), "i0");
        var i1 = Parameter(typeof(int), "i1");
        var i2 = Parameter(typeof(int), "i2");

        var inner = Lambda<Func<int, int, int, int>>(
            Add(Add(i0, i1), Add(i2, Constant(seed))), i0, i1, i2);

        return Lambda<Func<int, int, int, int>>(
            Add(Invoke(inner, o0, o1, o2), o0), o0, o1, o2);
    }

    // Two sibling inner lambdas share the name "x" (shadowing the outer "x") but are distinct
    // instances; the second body reads the outer parameter, so it only compares after the first
    // inner scope has unwound.
    private static Expression SiblingShadowingLambda(int seed)
    {
        var outer = Parameter(typeof(int), "x");
        var shadowA = Parameter(typeof(int), "x");
        var shadowB = Parameter(typeof(int), "x");

        var lambdaA = Lambda<Func<int, int>>(Add(shadowA, Constant(seed)), shadowA);
        var lambdaB = Lambda<Func<int, int>>(Add(shadowB, outer), shadowB);

        return Lambda<Func<int, int>>(
            Block(Invoke(lambdaA, outer), Invoke(lambdaB, outer)), outer);
    }

    // The inner lambda reuses the outer lambda's very ParameterExpression instance, so the scope's
    // TryAdd rejects it (duplicate binding) and the comparison reports "not equal" without throwing.
    private static Expression ReusedParameterLambda(int seed)
    {
        var p = Parameter(typeof(int), "x");
        var inner = Lambda<Func<int, int>>(Add(p, Constant(seed)), p);

        return Lambda<Func<int, int>>(Invoke(inner, p), p);
    }

    [Fact]
    public void Lambda_DifferentDelegateTypes_ShouldRejectAtOuterTypeGuard()
    {
        // A 0-parameter and a 1-parameter lambda have different delegate types, so the outer
        // ExpressionPlanEqualityComparer.Equals type guard rejects them before CompareLambda runs;
        // the arity check inside CompareLambda is defensively unreachable from the public factory.
        var comparer = CreateComparer();
        var zero = Lambda<Func<int>>(Constant(1));
        var one = Lambda<Func<int, int>>(Constant(1), Parameter(typeof(int), "p"));

        comparer.Equals(zero, one).Should().BeFalse("different delegate types are rejected by the outer type guard");

        // Distinct param-bearing trees that DO reach the parameter scope: a failed comparison must
        // fully unwind, so the next equivalent pair still compares equal.
        var p1 = Parameter(typeof(int), "x");
        var matching = Lambda<Func<int, int>>(Add(p1, Constant(1)), p1);
        var p2 = Parameter(typeof(int), "x");
        var mismatching = Lambda<Func<int, int>>(Add(p2, Constant(2)), p2);
        var p3 = Parameter(typeof(int), "x");
        var equivalent = Lambda<Func<int, int>>(Add(p3, Constant(1)), p3);

        comparer.Equals(matching, mismatching).Should().BeFalse();
        comparer.Equals(matching, equivalent).Should().BeTrue("a failed scoped comparison must leave no binding behind");
    }

    [Fact]
    public void Lambda_InlineAndSpillBoundaries_ShouldCompareAndUnwind()
    {
        foreach (var arity in new[] { 0, 1, 3, 4, 5, 8 })
        {
            var comparer = CreateComparer();

            comparer.Equals(FlatLambda(arity, 1), FlatLambda(arity, 1))
                .Should().BeTrue("arity {0} must compare structurally", arity);
            comparer.Equals(FlatLambda(arity, 1), FlatLambda(arity, 2))
                .Should().BeFalse("arity {0} must report a body mismatch", arity);
            comparer.Equals(FlatLambda(arity, 7), FlatLambda(arity, 7))
                .Should().BeTrue("arity {0} must unwind its live bindings", arity);
        }
    }

    [Fact]
    public void Lambda_NestedScopes_ShouldSpillAndUnwind()
    {
        var comparer = CreateComparer();

        comparer.Equals(NestedSpillLambda(1), NestedSpillLambda(1)).Should().BeTrue();
        comparer.Equals(NestedSpillLambda(1), NestedSpillLambda(2)).Should().BeFalse();
        comparer.Equals(NestedSpillLambda(3), NestedSpillLambda(3))
            .Should().BeTrue("both nested scopes must unwind after the mismatch");
    }

    [Fact]
    public void Lambda_SiblingScopes_ShouldShadowAndUnwind()
    {
        var comparer = CreateComparer();

        comparer.Equals(SiblingShadowingLambda(1), SiblingShadowingLambda(1)).Should().BeTrue();
        comparer.Equals(SiblingShadowingLambda(1), SiblingShadowingLambda(2)).Should().BeFalse();
        comparer.Equals(SiblingShadowingLambda(5), SiblingShadowingLambda(5))
            .Should().BeTrue("the second sibling must compare after the first scope unwound");
    }

    [Fact]
    public void Lambda_ReusedOuterParameter_ShouldReportNotEqualWithoutLeakingScope()
    {
        var comparer = CreateComparer();

        comparer.Equals(ReusedParameterLambda(1), ReusedParameterLambda(1)).Should().BeFalse();
        comparer.Equals(FlatLambda(2, 1), FlatLambda(2, 1))
            .Should().BeTrue("the duplicate-binding rejection must leave the comparer clean");
    }

    [Fact]
    public void Parameter_NullAndFreeInteractions_ShouldGuard()
    {
        var comparer = CreateComparer();
        var bound = Parameter(typeof(int), "x");

        comparer.Equals(null, bound).Should().BeFalse();
        comparer.Equals(bound, null).Should().BeFalse();
        comparer.Equals(null, null).Should().BeTrue();

        // A free (unbound) parameter has no scope entry, so it falls back to the type guard.
        comparer.Equals(bound, Parameter(typeof(int), "y")).Should().BeTrue();
        comparer.Equals(bound, Parameter(typeof(long), "y")).Should().BeFalse();
    }

    [Fact]
    public void Reentrancy_FailedNestedComparison_ShouldNotLeakScope()
    {
        var comparer = CreateComparer();

        // A matching nested pair, then a mismatching flat pair spanning the spill boundary: storage
        // must not be shared between the two Equals calls, and each failed call must fully unwind.
        comparer.Equals(SiblingShadowingLambda(4), SiblingShadowingLambda(4)).Should().BeTrue();
        comparer.Equals(NestedSpillLambda(1), NestedSpillLambda(2)).Should().BeFalse();
        comparer.Equals(FlatLambda(8, 1), FlatLambda(8, 2)).Should().BeFalse();
        comparer.Equals(FlatLambda(8, 9), FlatLambda(8, 9)).Should().BeTrue();
    }

    /// <summary>
    /// Equal trees must produce equal hashes on both sides of the inline/spill boundary: the hash
    /// visitor is scope-independent, so the one-way 4→5 <c>Dictionary</c> migration in the equality
    /// path must not change the hash relationship.
    /// </summary>
    [Fact]
    public void EqualsAndGetHashCode_ShouldStayCoherentAcrossSpillBoundary()
    {
        var comparer = CreateComparer();

        foreach (var arity in new[] { 0, 1, 3, 4, 5, 8 })
        {
            var left = FlatLambda(arity, 1);
            var right = FlatLambda(arity, 1);

            comparer.Equals(left, right).Should().BeTrue("arity {0} must compare structurally", arity);
            comparer.GetHashCode(left).Should().Be(
                comparer.GetHashCode(right),
                "equal trees must hash equal on both sides of the inline/spill boundary (arity {0})",
                arity);
        }
    }

    /// <summary>
    /// <see cref="ExpressionPlanEqualityComparer.SpillCount"/> counts exactly the 5th-binding
    /// migrations: 0 for every arity up to the inline capacity of 4 (0..4), at least one for 5 and for 8.
    /// </summary>
    [Fact]
    public void SpillCount_ShouldTrackOnlyTheFifthBindingMigration()
    {
        var comparer = CreateComparer();

        ExpressionPlanEqualityComparer.ResetCounters();

        foreach (var arity in new[] { 0, 1, 2, 3, 4 })
        {
            comparer.Equals(FlatLambda(arity, 1), FlatLambda(arity, 1)).Should().BeTrue();
            ExpressionPlanEqualityComparer.SpillCount.Should().Be(0, "arity {0} has at most 4 concurrent bindings and fits inline", arity);
        }

        comparer.Equals(FlatLambda(5, 1), FlatLambda(5, 1)).Should().BeTrue();
        ExpressionPlanEqualityComparer.SpillCount
            .Should().BeGreaterThanOrEqualTo(1, "the 5th concurrent binding spills");

        var afterFive = ExpressionPlanEqualityComparer.SpillCount;
        comparer.Equals(FlatLambda(8, 1), FlatLambda(8, 1)).Should().BeTrue();
        ExpressionPlanEqualityComparer.SpillCount
            .Should().BeGreaterThanOrEqualTo(afterFive + 1, "arity 8 also spills");
    }

    private sealed class BindingTarget
    {
        public int Id { get; set; }

        public List<int> Items { get; } = new();

        public NestedTarget Nested { get; set; } = new();
    }

    private sealed class NestedTarget
    {
        public int Value { get; set; }
    }
}
