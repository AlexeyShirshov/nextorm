using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T241 (issue #241) regressions for uniform cancellation handling while sorting/grouping preparation
/// builds its keys — the third slice of the #207 pattern (<c>ec42c960</c>) applied to the remaining
/// <c>IsCancellationRequested</c> early exits in <see cref="QueryCommand.QueryPreparer"/>.
/// <para>
/// The pre-cancelled cases are the RED arm against the original source: before the fix each targeted
/// path returned a partial result (or left the keys unprepared) instead of surfacing
/// <see cref="OperationCanceledException"/>. The active/None cases are the frozen positive oracles:
/// an active cancellable token and <see cref="CancellationToken.None"/> must keep the complete key
/// columns, the applicable plan hashes and the query results.
/// </para>
/// </summary>
public class SortingGroupingCancellationTests
{
    private static readonly CancellationToken PreCancelled = new(canceled: true);

    private static readonly MethodInfo BuildKeyColumnsMethod = typeof(QueryCommand.QueryPreparer)
        .GetMethod("BuildKeyColumns", BindingFlags.NonPublic | BindingFlags.Static)!;

    public SortingGroupingCancellationTests()
    {
        DataContextCache.Clear();
    }

    /// <summary>
    /// The private key-column build reached by both GROUP BY and the LIMIT BY/DISTINCT ON/ExtremeRow/
    /// per-parent callers. A pre-cancelled token must throw instead of returning an array whose slots
    /// are still <see langword="null"/>.
    /// </summary>
    [Fact]
    public void T241_PreCancelled_BuildKeyColumns_ThrowsOperationCanceled()
    {
        Action act = () => BuildKeyColumns(PairKeyLambda(), PreCancelled);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled key build must surface cancellation instead of returning null-filled key columns");
    }

    /// <summary>
    /// The public preparation path for a sorting-only, projection-less entity query must surface the
    /// pre-cancelled token instead of returning a command whose sort keys were never prepared.
    /// </summary>
    [Fact]
    public void T241_PreCancelled_Sorting_ThrowsOperationCanceled()
    {
        using var ctx = new InMemoryDataContext();
        var command = ctx.From<SimpleEntity>().OrderBy(x => x.Id).ToCommand();

        Action act = () => command.PrepareCommand(PreCancelled);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must surface cancellation instead of skipping every sorting key");
    }

    /// <summary>
    /// The public grouping path must reach the key-column build and surface the pre-cancelled token
    /// instead of returning a partial grouping list.
    /// </summary>
    [Fact]
    public void T241_PreCancelled_Grouping_ThrowsOperationCanceled()
    {
        using var ctx = new InMemoryDataContext();
        var command = ctx.From<SimpleEntity>().GroupBy(e => new { e.Id, Mod = e.Id % 2 }).ToCommand();

        Action act = () => command.PrepareCommand(PreCancelled);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must surface cancellation instead of returning a partial grouping list");
    }

    /// <summary>
    /// Frozen oracle: an active cancellable token and <see cref="CancellationToken.None"/> both return
    /// the complete key columns in constructor-parameter order, with no unfilled slots.
    /// </summary>
    [Fact]
    public void T241_KeyColumns_ActiveAndNone_ReturnCompleteKeys()
    {
        using var cts = new CancellationTokenSource();

        var active = BuildKeyColumns(PairKeyLambda(), cts.Token);
        var none = BuildKeyColumns(PairKeyLambda(), CancellationToken.None);

        AssertCompleteKeys(active);
        AssertCompleteKeys(none);
    }

    /// <summary>
    /// Frozen oracle: neither an active token nor <see cref="CancellationToken.None"/> changes the
    /// prepared sorting/grouping keys, the applicable plan hashes or the in-memory results.
    /// </summary>
    [Fact]
    public void T241_ActiveAndNone_InMemorySortingGrouping_KeepResults()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<SimpleEntity>().WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 },
        });

        using var cts = new CancellationTokenSource();

        var noneSort = ctx.From<SimpleEntity>().OrderBy(e => e.Id).ToCommand();
        noneSort.PrepareCommand(CancellationToken.None);
        var activeSort = ctx.From<SimpleEntity>().OrderBy(e => e.Id).ToCommand();
        activeSort.PrepareCommand(cts.Token);

        noneSort.IsPrepared.Should().BeTrue();
        activeSort.IsPrepared.Should().BeTrue();
        noneSort.SortingPlanHash.Should().NotBe(0);
        activeSort.SortingPlanHash.Should().Be(noneSort.SortingPlanHash);

        var noneGroup = ctx.From<SimpleEntity>().GroupBy(e => new { e.Id, Mod = e.Id % 2 }).ToCommand();
        noneGroup.PrepareCommand(CancellationToken.None);
        var activeGroup = ctx.From<SimpleEntity>().GroupBy(e => new { e.Id, Mod = e.Id % 2 }).ToCommand();
        activeGroup.PrepareCommand(cts.Token);

        noneGroup.GroupingPlanHash.Should().NotBe(0);
        activeGroup.GroupingPlanHash.Should().Be(noneGroup.GroupingPlanHash);
        activeGroup.GroupingList.Should().NotBeNull();
        activeGroup.GroupingList!.Select(c => c.PropertyName).Should().Equal("Id", "Mod");
        activeGroup.GroupingList!.Select(c => c.Index).Should().Equal(0, 1);

        // Frozen fixture: seeded ids 1..3, ascending; the grouped projection yields one row per id.
        ctx.From<SimpleEntity>().OrderBy(e => e.Id).Select(e => e.Id).ToList().Should().Equal(1, 2, 3);

        var grouped = ctx.From<SimpleEntity>()
            .GroupBy(e => new { e.Id, Mod = e.Id % 2 })
            .Select(e => new { e.Id, Mod = e.Id % 2, Count = SqlFunctions.Sql.count() })
            .ToList();

        grouped.Should().HaveCount(3);
        grouped.Select(g => g.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
        grouped.Should().OnlyContain(g => g.Count == 1);
    }

    private static void AssertCompleteKeys(SelectExpression[] columns)
    {
        columns.Should().HaveCount(2);
        // PropertyName is the key constructor parameter name (the independent oracle is the fixture's
        // own constructor signature), so the expected names are lowercase.
        columns.Select(c => c.PropertyName).Should().Equal("id", "name");
        columns.Select(c => c.Index).Should().Equal(0, 1);
        columns.Select(c => c.PropertyType).Should().Equal(typeof(int), typeof(string));
        columns.Should().OnlyContain(c => c.Expression != null);
    }

    private static SelectExpression[] BuildKeyColumns(LambdaExpression expression, CancellationToken cancellationToken)
    {
        try
        {
            return (SelectExpression[])BuildKeyColumnsMethod.Invoke(null, [expression, "GROUP BY", cancellationToken])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            // Reflection wraps the production exception; unwrap it so the caller observes the real
            // OperationCanceledException (and its original stack) rather than the reflection wrapper.
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static LambdaExpression PairKeyLambda()
    {
        var parameter = Expression.Parameter(typeof(T241KeyEntity), "e");
        var constructor = typeof(T241KeyPair).GetConstructor([typeof(int), typeof(string)])!;
        var body = Expression.New(
            constructor,
            Expression.Property(parameter, nameof(T241KeyEntity.Id)),
            Expression.Property(parameter, nameof(T241KeyEntity.Name)));

        return Expression.Lambda(body, parameter);
    }
}

/// <summary>The two-member key shape used by the T241 key-column oracles.</summary>
public sealed class T241KeyEntity
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>A two-argument constructor projection standing in for a composite GROUP BY key.</summary>
public sealed class T241KeyPair
{
    public T241KeyPair(int id, string? name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }

    public string? Name { get; }
}
