using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using Xunit;

namespace NextORM.Sqlite.Tests.QueryComponents;

/// <summary>
/// The pre-preparation recipe cache identifies a command by its value-free raw shape. A query component
/// contributes its own state to that shape through <see cref="IQueryComponentShape"/>, so two commands
/// that differ only in their component never share one recipe — before that, a <c>One</c> plan was reused
/// for an <c>All</c> query on the same thread, which surfaced as a wrong tie set on a live server. A
/// component that cannot describe itself stays excluded from the recipe path instead of being guessed.
/// </summary>
public sealed class RawRecipeComponentIsolationTests
{
    private sealed class Row
    {
        public int Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void OneRecipeIsNotReusedForAnAllCommand()
    {
        string one;
        using (var first = SqliteTestContext.Create())
        {
            one = Sql(first, ExtremeRowExtensions.SelectWhereMin(first.From<Row>()
, x => x.Score, x => new { x.Id, x.Name }));
        }

        string all;
        using (var second = SqliteTestContext.Create())
        {
            all = Sql(second, ExtremeRowExtensions.SelectWhereMin(second.From<Row>()
, x => x.Score, x => new { x.Id, x.Name }, ExtremeRowTies.All));
        }

        one.Should().ContainEquivalentOf("row_number()");
        all.Should().NotContainEquivalentOf("row_number()");
        all.Should().ContainEquivalentOf("min(");
        all.Should().NotBe(one);
    }

    [Fact]
    public void ComponentRecipeIsNotReusedForAPlainCommandAndViceVersa()
    {
        string plain;
        using (var first = SqliteTestContext.Create())
        {
            plain = Sql(first, (QueryCommand<Row>)first.From<Row>().Where(x => x.Score > 3));
        }

        string component;
        using (var second = SqliteTestContext.Create())
        {
            component = Sql(second, (QueryCommand<Row>)ExtremeRowExtensions.SelectWhereMin(second.From<Row>()
                .Where(x => x.Score > 3)
, x => x.Score));
        }

        plain.Should().NotContainEquivalentOf("row_number()");
        component.Should().ContainEquivalentOf("row_number()");
        component.Should().NotBe(plain);
    }

    [Fact]
    public void DescribedComponentReusesTheRecipeAcrossContexts()
    {
        var component = new DescribedCountingComponent();

        using (var first = SqliteTestContext.Create())
        {
            Sql(first, (QueryCommand<Row>)first.From<Row>().Where(x => x.Id > 0).WithComponent(component));
        }

        using (var second = SqliteTestContext.Create())
        {
            Sql(second, (QueryCommand<Row>)second.From<Row>().Where(x => x.Id > 0).WithComponent(component));
        }

        component.PrepareCount.Should().Be(1,
            "a component that describes its state lets the raw recipe replay the prepared plan");
    }

    [Fact]
    public void UndescribedComponentIsExcludedFromTheRecipe()
    {
        var component = new UndescribedCountingComponent();

        using (var first = SqliteTestContext.Create())
        {
            Sql(first, (QueryCommand<Row>)first.From<Row>().Where(x => x.Id > 0).WithComponent(component));
        }

        using (var second = SqliteTestContext.Create())
        {
            Sql(second, (QueryCommand<Row>)second.From<Row>().Where(x => x.Id > 0).WithComponent(component));
        }

        component.PrepareCount.Should().Be(2,
            "a component without IQueryComponentShape is invisible before preparation, so no plan is reused");
    }

    private static string Sql<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None)).SqlStmt!;

    private abstract class CountingComponentBase : IQueryComponent
    {
        public int PrepareCount { get; private set; }

        public Type Slot => GetType();

        public QueryComponentContribution Contribution => QueryComponentContribution.AdditivePredicate;

        public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
        {
            PrepareCount++;
            return new Descriptor(Slot, GetType());
        }

        public void BuildQuery(IQueryComponentBuildContext context)
        {
        }

        private sealed class Descriptor(Type slot, Type componentType) : IQueryComponentDescriptor
        {
            public Type Slot { get; } = slot;

            public Type ComponentType { get; } = componentType;

            public QueryComponentContribution Contribution => QueryComponentContribution.AdditivePredicate;

            public string Discriminator => "counting";

            public int StructuralHash => 42;

            public bool StructurallyEquals(IQueryComponentDescriptor? other)
                => other is Descriptor && other.Slot == Slot;
        }
    }

    private sealed class UndescribedCountingComponent : CountingComponentBase;

    private sealed class DescribedCountingComponent : CountingComponentBase, IQueryComponentShape
    {
        private static readonly Expression<Func<Row, bool>> Selector = x => x.Id > 0;

        public object? ShapeState => "additive";

        public int ShapeSelectorCount => 1;

        public LambdaExpression GetShapeSelector(int index) => index == 0
            ? Selector
            : throw new ArgumentOutOfRangeException(nameof(index), index, "No selector at this index.");
    }
}
