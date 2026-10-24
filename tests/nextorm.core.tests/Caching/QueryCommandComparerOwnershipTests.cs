using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// DEF-T327-05 regression: the six lazily-created plan comparers are bound to the command that owns
/// them — each resolves <see cref="QueryCommand.ReferencedQueries"/> through the registry it was
/// constructed with — so <c>QueryCommand.CopyTo</c> must never share them between command instances.
/// Before the fix a cloned command adopted the source command's comparers; after the recipe fast path
/// was discarded (apply + discard keeps the copied comparers while <c>ResetPreparation</c> nulls the
/// destination's referenced queries) the destination hashed a sort expression through the foreign
/// registry and <c>ExpressionPlanEqualityComparer.VisitIndex</c> threw
/// <see cref="NullReferenceException"/>.
/// </summary>
public class QueryCommandComparerOwnershipTests
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>Comparer names in <see cref="CreateComparers"/> order, for assertion messages.</summary>
    private static readonly string[] ComparerNames =
    [
        "QueryPlanEqualityComparer",
        "FromExpressionPlanEqualityComparer",
        "ExpressionPlanEqualityComparer",
        "SelectExpressionPlanEqualityComparer",
        "JoinExpressionPlanEqualityComparer",
        "SortingExpressionPlanEqualityComparer",
    ];

    /// <summary>
    /// The live copy path (<see cref="QueryCommand.Clone"/>, i.e. <c>CopyTo(copyAll: true)</c>) is the
    /// one the recipe fast path uses through <c>ApplyPreparationFrom</c>. A destination must create its
    /// own lazily-loaded comparers instead of adopting the source's.
    /// </summary>
    [Fact]
    public void Clone_Should_Give_The_Copy_Its_Own_Plan_Comparers()
    {
        using var ctx = new InMemoryDataContext();
        var source = CreateCommand(ctx);
        var sourceComparers = CreateComparers(source);

        var destination = source.Clone();
        var destinationComparers = CreateComparers(destination);

        AssertOwnership(sourceComparers, destinationComparers, destination, "a live clone");
    }

    /// <summary>
    /// The plan-cache copy path (<see cref="QueryCommand.CloneForCache"/>, i.e. <c>CopyTo(copyAll: false)</c>)
    /// must obey the same ownership invariant.
    /// </summary>
    [Fact]
    public void CloneForCache_Should_Give_The_Copy_Its_Own_Plan_Comparers()
    {
        using var ctx = new InMemoryDataContext();
        var source = CreateCommand(ctx);
        var sourceComparers = CreateComparers(source);

        var destination = source.CloneForCache();
        var destinationComparers = CreateComparers(destination);

        AssertOwnership(sourceComparers, destinationComparers, destination, "a cache clone");
    }

    /// <summary>
    /// The exact recipe sequence: a destination whose comparers are already initialised adopts the
    /// stored command's prepared state and then discards it. The destination must keep its own
    /// comparers, not the stored command's.
    /// </summary>
    [Fact]
    public void Discarded_Preparation_Adoption_Should_Keep_The_Destination_Plan_Comparers()
    {
        using var ctx = new InMemoryDataContext();
        var stored = CreateCommand(ctx);
        var storedComparers = CreateComparers(stored);

        var staged = CreateCommand(ctx);
        var ownComparers = CreateComparers(staged);
        var raw = staged.CaptureRawGraph();

        staged.ApplyPreparationFrom(stored);
        staged.DiscardStagedPreparation(raw);

        var keptComparers = CreateComparers(staged);

        for (var idx = 0; idx < ComparerNames.Length; idx++)
        {
            keptComparers[idx].Should().BeSameAs(
                ownComparers[idx],
                $"{ComparerNames[idx]} belongs to the staged command and must survive an adopted-then-discarded preparation");
            keptComparers[idx].Should().NotBeSameAs(
                storedComparers[idx],
                $"{ComparerNames[idx]} must not be adopted from the stored cache-side command");
        }

        // Adoption also swaps preparation-derived state in and out; the comparers must still resolve
        // ReferencedQueries through the staged command itself.
        for (var idx = 0; idx < ComparerNames.Length; idx++)
        {
            var provider = TryGetProvider(keptComparers[idx]);
            if (provider is not null)
                provider.Should().BeSameAs(staged, $"{ComparerNames[idx]} must resolve ReferencedQueries through the staged command");
        }
    }

    /// <summary>
    /// Owning a comparer must not change plan identity: the same sort key hashes identically through
    /// the source's and the destination's sorting comparer.
    /// </summary>
    [Fact]
    public void Clone_Should_Preserve_The_Structural_Hash_Of_The_Same_Sort_Key()
    {
        using var ctx = new InMemoryDataContext();
        var source = CreateCommand(ctx);
        _ = source.GetSortingExpressionPlanEqualityComparer();

        var destination = source.Clone();

        var sorting = new Sorting(Expression.Constant(1))
        {
            PreparedExpression = Expression.Add(Expression.Constant(1), Expression.Constant(2)),
        };

        var sourceHash = source.GetSortingExpressionPlanEqualityComparer().GetHashCode(sorting);
        var destinationHash = destination.GetSortingExpressionPlanEqualityComparer().GetHashCode(sorting);

        destinationHash.Should().Be(sourceHash, "the ownership fix must not change the plan identity of the same expression");
    }

    // The command shape only matters in that it builds a real prepared-capable command; the
    // comparers are lazy and exist independently of preparation.
    private static QueryCommand CreateCommand(InMemoryDataContext ctx)
        => ctx.From<SimpleEntity>().Where(x => x.Id > 0).OrderBy(x => x.Id).Select(x => x.Id);

    private static object[] CreateComparers(QueryCommand command) =>
    [
        command.GetQueryPlanEqualityComparer(),
        command.GetFromExpressionPlanEqualityComparer(),
        command.GetExpressionPlanEqualityComparer(),
        command.GetSelectExpressionPlanEqualityComparer(),
        command.GetJoinExpressionPlanEqualityComparer(),
        command.GetSortingExpressionPlanEqualityComparer(),
    ];

    private static void AssertOwnership(object[] sourceComparers, object[] destinationComparers, QueryCommand destination, string what)
    {
        for (var idx = 0; idx < ComparerNames.Length; idx++)
        {
            destinationComparers[idx].Should().NotBeSameAs(
                sourceComparers[idx],
                $"{ComparerNames[idx]} on {what} must be created by the copy, not shared with the source command");

            var provider = TryGetProvider(destinationComparers[idx]);
            if (provider is not null)
                provider.Should().BeSameAs(
                    destination,
                    $"{ComparerNames[idx]} must resolve ReferencedQueries through the destination command");
        }
    }

    // QueryPlanEqualityComparer and FromExpressionPlanEqualityComparer no longer hold the registry
    // directly; the other four keep it in a private field. The field, when present, must be the
    // destination command.
    private static object? TryGetProvider(object comparer)
        => comparer.GetType().GetField("_queryProvider", InstanceFields)?.GetValue(comparer);
}
