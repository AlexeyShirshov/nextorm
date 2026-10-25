using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// A nullable integer/string entity used by the T287 NULL-placement oracles. Values are seeded
/// deliberately unsorted (<c>NULL, 2, NULL, -1, 1</c>) so the secondary id key has to disambiguate ties.
/// </summary>
public class T287NullProbe
{
    public int Id { get; set; }
    public int? Key { get; set; }
    public string? Text { get; set; }
}

/// <summary>
/// T287 (issue #287): portable explicit NULL placement in query ordering, database-free slice.
/// <para>
/// These are the in-memory result oracles against the frozen expected sequences
/// <c>NULL,NULL,-1,1,2</c> / <c>-1,1,2,NULL,NULL</c> / <c>NULL,NULL,2,1,-1</c> / <c>2,1,-1,NULL,NULL</c>,
/// plus the placement-aware plan identity, the <c>ForLast</c> reversal and the pre-execution guards.
/// SQL text for the dialects is owned by the provider SQL-generation suites.
/// </para>
/// </summary>
public class T287NullOrderingTests
{
    private static readonly int?[] AscFirstKeys = [null, null, -1, 1, 2];
    private static readonly int?[] AscLastKeys = [-1, 1, 2, null, null];
    private static readonly int?[] DescFirstKeys = [null, null, 2, 1, -1];
    private static readonly int?[] DescLastKeys = [2, 1, -1, null, null];

    // ---- result oracle: the four direction/placement cells ---------------------------------

    [Fact]
    public void T287_AscFirst_ShouldPutNullsBeforeEveryValue()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);

        var rows = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, OrderDirection.Asc, NullsPosition.First)
            .ThenBy(x => x.Id)
            .ToList();

        rows.Select(x => x.Key).Should().Equal(AscFirstKeys);
        rows.Select(x => x.Id).Should().Equal(1, 3, 4, 5, 2);
    }

    [Fact]
    public void T287_AscLast_ShouldPutNullsAfterEveryValue()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);

        var rows = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, OrderDirection.Asc, NullsPosition.Last)
            .ThenBy(x => x.Id)
            .ToList();

        rows.Select(x => x.Key).Should().Equal(AscLastKeys);
        rows.Select(x => x.Id).Should().Equal(4, 5, 2, 1, 3);
    }

    [Fact]
    public void T287_DescFirst_ShouldKeepNullsFirstWhileTheValuesReverse()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);

        var rows = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, OrderDirection.Desc, NullsPosition.First)
            .ThenBy(x => x.Id)
            .ToList();

        rows.Select(x => x.Key).Should().Equal(DescFirstKeys);
        rows.Select(x => x.Id).Should().Equal(1, 3, 2, 5, 4);
    }

    [Fact]
    public void T287_DescLast_ShouldKeepNullsLastWhileTheValuesReverse()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);

        var rows = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, OrderDirection.Desc, NullsPosition.Last)
            .ThenBy(x => x.Id)
            .ToList();

        rows.Select(x => x.Key).Should().Equal(DescLastKeys);
        rows.Select(x => x.Id).Should().Equal(2, 5, 4, 1, 3);
    }

    [Fact]
    public void T287_DescendingShortcut_ShouldHonourPlacement()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);

        var rows = ctx.From<T287NullProbe>()
            .OrderByDescending(x => x.Key, NullsPosition.First)
            .ThenBy(x => x.Id)
            .ToList();

        rows.Select(x => x.Key).Should().Equal(DescFirstKeys);
    }

    // ---- nullable string parity, all-null, no-null, empty, duplicates ----------------------

    [Fact]
    public void T287_NullableString_ShouldMatchTheIntegerParity()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);

        var first = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Text, NullsPosition.First)
            .ThenBy(x => x.Id)
            .ToList();
        var last = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Text, OrderDirection.Desc, NullsPosition.Last)
            .ThenBy(x => x.Id)
            .ToList();

        // Texts mirror the integer keys: NULL, "b", NULL, "a", "c".
        first.Select(x => x.Text).Should().Equal(null, null, "a", "b", "c");
        last.Select(x => x.Text).Should().Equal("c", "b", "a", null, null);
    }

    [Fact]
    public void T287_AllNullKeys_ShouldKeepTheSecondaryOrder()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<T287NullProbe>().WithData(
        [
            new T287NullProbe { Id = 3 },
            new T287NullProbe { Id = 1 },
            new T287NullProbe { Id = 2 },
        ]);

        var rows = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, OrderDirection.Desc, NullsPosition.First)
            .ThenBy(x => x.Id)
            .ToList();

        rows.Select(x => x.Id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void T287_NoNullKeys_ShouldOrderValuesByDirection()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<T287NullProbe>().WithData(
        [
            new T287NullProbe { Id = 1, Key = 2 },
            new T287NullProbe { Id = 2, Key = -1 },
            new T287NullProbe { Id = 3, Key = 1 },
        ]);

        var asc = ctx.From<T287NullProbe>().OrderBy(x => x.Key, NullsPosition.Last).ToList();
        var desc = ctx.From<T287NullProbe>().OrderByDescending(x => x.Key, NullsPosition.First).ToList();

        asc.Select(x => x.Key).Should().Equal(-1, 1, 2);
        desc.Select(x => x.Key).Should().Equal(2, 1, -1);
    }

    [Fact]
    public void T287_EmptySource_ShouldReturnEmpty()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<T287NullProbe>().WithData(Array.Empty<T287NullProbe>());

        var rows = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, NullsPosition.First)
            .ThenBy(x => x.Id)
            .ToList();

        rows.Should().BeEmpty();
    }

    [Fact]
    public void T287_DuplicateKeys_ShouldKeepTheSecondaryOrderWithinTies()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<T287NullProbe>().WithData(
        [
            new T287NullProbe { Id = 1, Key = 1 },
            new T287NullProbe { Id = 2, Key = null },
            new T287NullProbe { Id = 3, Key = 1 },
            new T287NullProbe { Id = 4, Key = null },
        ]);

        var rows = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, NullsPosition.Last)
            .ThenBy(x => x.Id)
            .ToList();

        rows.Select(x => x.Id).Should().Equal(1, 3, 2, 4);
    }

    // ---- secondary keys: mixed directions and mixed placements -----------------------------

    [Fact]
    public void T287_ThenBy_ShouldAppendMixedDirectionAndPlacementKeys()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<T287NullProbe>().WithData(
        [
            new T287NullProbe { Id = 1, Key = null, Text = "x" },
            new T287NullProbe { Id = 2, Key = 1, Text = null },
            new T287NullProbe { Id = 3, Key = null, Text = null },
            new T287NullProbe { Id = 4, Key = 1, Text = "a" },
            new T287NullProbe { Id = 5, Key = 2, Text = "m" },
        ]);

        var rows = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, NullsPosition.First)
            .ThenByDescending(x => x.Text, NullsPosition.Last)
            .ToList();

        // Primary: NULL group first, then the values ascending. Secondary: text descending with its own
        // NULLs last, so the NULL-group tie is "x" then null and the key=1 tie is "a" then null.
        rows.Select(x => x.Id).Should().Equal(1, 3, 4, 2, 5);
    }

    [Fact]
    public void T287_ThenBy_ShouldNeverReplaceThePrimaryKey()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);

        var rows = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, NullsPosition.Last)
            .ThenBy(x => x.Id)
            .ToList();

        rows.Select(x => x.Key).Should().Equal(AscLastKeys);
    }

    // ---- regressions: secondary keys must extend, not replace, the primary ordering ---------

    [Fact]
    public void T287_DefaultSecondaryKey_ShouldExtendThePrimaryOrdering()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<T287NullProbe>().WithData(
        [
            new T287NullProbe { Id = 3, Key = 1 },
            new T287NullProbe { Id = 1, Key = 2 },
            new T287NullProbe { Id = 2, Key = 1 },
        ]);

        var rows = ctx.From<T287NullProbe>().OrderBy(x => x.Key).ThenBy(x => x.Id).ToList();

        // A second key used to re-sort the whole sequence (Enumerable.OrderBy is a fresh stable sort), so
        // the last key silently became primary. The primary key must stay first and the id key only breaks
        // its ties.
        rows.Select(x => x.Id).Should().Equal(2, 3, 1);
        rows.Select(x => x.Key).Should().Equal(1, 1, 2);
    }

    [Fact]
    public void T287_SameContext_TwoPlacements_ShouldNotShareASequence()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);

        var first = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, NullsPosition.First)
            .ThenBy(x => x.Id)
            .ToList();
        var last = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, NullsPosition.Last)
            .ThenBy(x => x.Id)
            .ToList();

        // Same context, same source and same secondary key: only the placement differs, so a shared cache
        // entry (or a placement-blind key) would make the two sequences identical.
        first.Select(x => x.Id).Should().Equal(1, 3, 4, 5, 2);
        last.Select(x => x.Id).Should().Equal(4, 5, 2, 1, 3);
    }

    // ---- Default keeps today's behaviour ----------------------------------------------------

    [Fact]
    public void T287_DefaultPlacement_ShouldKeepTheProviderDefaultOrdering()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);

        var asc = ctx.From<T287NullProbe>().OrderBy(x => x.Key).ToList();
        var desc = ctx.From<T287NullProbe>().OrderByDescending(x => x.Key).ThenBy(x => x.Id).ToList();

        // The in-memory default comparer puts nulls first ascending and last descending.
        asc.Select(x => x.Key).Should().Equal(AscFirstKeys);
        desc.Select(x => x.Key).Should().Equal(DescLastKeys);
    }

    // ---- invalid values fail before execution ----------------------------------------------

    [Fact]
    public void T287_InvalidEnumValues_ShouldBeRejectedByConstructionAndFluentEntryPoints()
    {
        const NullsPosition Invalid = (NullsPosition)42;

        FluentActions.Invoking(() => new Sorting(Expression.Constant(1), Invalid))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("nullsPosition");
        FluentActions.Invoking(() => new Sorting(1, Invalid))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("nullsPosition");

        var sorting = new Sorting(Expression.Constant(1));
        FluentActions.Invoking(() => sorting.NullsPosition = Invalid)
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("nullsPosition");

        using var ctx = new InMemoryDataContext();
        FluentActions.Invoking(() => ctx.From<T287NullProbe>().OrderBy(x => x.Key, Invalid))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("nullsPosition");
        FluentActions.Invoking(() => ctx.From<T287NullProbe>().OrderBy(1, Invalid))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("nullsPosition");
    }

    [Fact]
    public void T287_InvalidPlacementReachingExecution_ShouldThrowBeforeRunning()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);
        var command = ctx.From<T287NullProbe>().OrderBy(x => x.Key).ToCommand();

        ForcePlacement(command, (NullsPosition)42);

        var act = FluentActions.Invoking(() => command.ToList());

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("nullsPosition")
            .Which.Message.Should().Contain("42");
    }

    // ---- plan identity ---------------------------------------------------------------------

    [Fact]
    public void T287_Comparer_ShouldDistinguishEveryPlacement()
    {
        var comparer = new QueryProvider().GetSortingExpressionPlanEqualityComparer();

        var @default = MakeSorting(NullsPosition.Default);
        var first = MakeSorting(NullsPosition.First);
        var last = MakeSorting(NullsPosition.Last);

        comparer.Equals(@default, first).Should().BeFalse();
        comparer.Equals(@default, last).Should().BeFalse();
        comparer.Equals(first, last).Should().BeFalse();

        var hashes = new[] { comparer.GetHashCode(@default), comparer.GetHashCode(first), comparer.GetHashCode(last) };
        hashes.Should().OnlyHaveUniqueItems("placement participates in the plan hash");

        var firstAgain = MakeSorting(NullsPosition.First);
        comparer.Equals(first, firstAgain).Should().BeTrue();
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(firstAgain));
        comparer.ValueEquals(in first, in firstAgain).Should().BeTrue();
        comparer.ValueEquals(in first, in last).Should().BeFalse();
    }

    // ---- ForLast carries and swaps placement -----------------------------------------------

    [Fact]
    public void T287_ForLast_ShouldCarryAndSwapExplicitPlacement()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);
        var command = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, NullsPosition.First)
            .ThenBy(x => x.Id)
            .ToCommand();

        var reversed = InvokeForLast(command);

        reversed.Sorting.Should().HaveCount(2);
        reversed.Sorting![0].Direction.Should().Be(OrderDirection.Desc);
        reversed.Sorting[0].NullsPosition.Should().Be(NullsPosition.Last, "the reversed order must stay logically equivalent");
        reversed.Sorting[1].NullsPosition.Should().Be(NullsPosition.Default);
    }

    [Fact]
    public void T287_ForLast_ShouldKeepDefaultPlacementDefault()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);
        var command = ctx.From<T287NullProbe>().OrderBy(x => x.Key).ToCommand();

        var reversed = InvokeForLast(command);

        reversed.Sorting.Should().ContainSingle();
        reversed.Sorting![0].Direction.Should().Be(OrderDirection.Desc);
        reversed.Sorting[0].NullsPosition.Should().Be(NullsPosition.Default);
    }

    [Fact]
    public void T287_Last_ShouldReturnTheLogicalLastRowUnderExplicitPlacement()
    {
        using var ctx = new InMemoryDataContext();
        SeedProbe(ctx);

        var last = ctx.From<T287NullProbe>()
            .OrderBy(x => x.Key, NullsPosition.First)
            .ThenBy(x => x.Id)
            .Last();

        // Ascending with NULLs first ends at the largest value (2, id 2).
        last.Id.Should().Be(2);
        last.Key.Should().Be(2);
    }

    // ---- in-memory projected and extreme-row paths -----------------------------------------

    [Fact]
    public void T287_ProjectedOrdering_ShouldHonourExplicitPlacement()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<T287NullProbe>().WithData(
        [
            new T287NullProbe { Id = 1, Key = null },
            new T287NullProbe { Id = 2, Key = null },
            new T287NullProbe { Id = 3, Key = 1 },
            new T287NullProbe { Id = 4, Key = 2 },
        ]);

        var first = ctx.From<T287NullProbe>()
            .GroupBy(e => new { e.Key })
            .Select(e => new { e.Key, Count = SqlFunctions.Sql.count() })
            .OrderBy(1, NullsPosition.First)
            .ToList();
        var last = ctx.From<T287NullProbe>()
            .GroupBy(e => new { e.Key })
            .Select(e => new { e.Key, Count = SqlFunctions.Sql.count() })
            .OrderBy(1, NullsPosition.Last)
            .ToList();

        first.Select(x => x.Key).Should().Equal(null, 1, 2);
        first.Select(x => x.Count).Should().Equal(2, 1, 1);
        last.Select(x => x.Key).Should().Equal(1, 2, null);
    }

    [Fact]
    public void T287_ExtremeRowOrdering_ShouldHonourExplicitPlacement()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<T287ExtremeProbe>().WithData(
        [
            new T287ExtremeProbe { Id = 1, Key = null, Score = 9 },
            new T287ExtremeProbe { Id = 2, Key = 2, Score = 9 },
            new T287ExtremeProbe { Id = 3, Key = null, Score = 9 },
            new T287ExtremeProbe { Id = 4, Key = -1, Score = 9 },
            new T287ExtremeProbe { Id = 5, Key = 1, Score = 9 },
        ]);

        var rows = ctx.From<T287ExtremeProbe>()
            .SelectWhereMax(x => x.Score, ExtremeRowTies.All)
            .OrderBy(x => x.Key, NullsPosition.First)
            .ThenBy(x => x.Id)
            .ToList();

        rows.Select(x => x.Key).Should().Equal(AscFirstKeys);
        rows.Select(x => x.Id).Should().Equal(1, 3, 4, 5, 2);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static void SeedProbe(InMemoryDataContext ctx)
    {
        ctx.From<T287NullProbe>().WithData(
        [
            new T287NullProbe { Id = 1, Key = null, Text = null },
            new T287NullProbe { Id = 2, Key = 2, Text = "b" },
            new T287NullProbe { Id = 3, Key = null, Text = null },
            new T287NullProbe { Id = 4, Key = -1, Text = "a" },
            new T287NullProbe { Id = 5, Key = 1, Text = "c" },
        ]);
    }

    private static Sorting MakeSorting(NullsPosition nullsPosition) => new(Expression.Constant(1), nullsPosition)
    {
        Direction = OrderDirection.Asc,
        PreparedExpression = Expression.Constant(1),
    };

    private static QueryCommand<T287NullProbe> InvokeForLast(QueryCommand<T287NullProbe> command)
    {
        var method = typeof(QueryCommand<T287NullProbe>)
            .GetMethod("ForLast", BindingFlags.NonPublic | BindingFlags.Instance)!;

        return (QueryCommand<T287NullProbe>)method.Invoke(command, null)!;
    }

    /// <summary>
    /// Writes an out-of-range placement through the private backing field, bypassing the validating
    /// property setter, to prove rendering/execution re-validates a publicly mutable <see cref="Sorting"/>.
    /// </summary>
    private static void ForcePlacement(QueryCommand<T287NullProbe> command, NullsPosition nullsPosition)
    {
        var field = typeof(Sorting).GetField("_nullsPosition", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var boxed = (object)command.Sorting![0];
        field.SetValue(boxed, nullsPosition);
        command.Sorting[0] = (Sorting)boxed;
    }
}

/// <summary>Extreme-row probe for the T287 in-memory outer-ordering path.</summary>
public class T287ExtremeProbe
{
    public int Id { get; set; }
    public int? Key { get; set; }
    public int? Score { get; set; }
}
