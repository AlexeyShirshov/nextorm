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
