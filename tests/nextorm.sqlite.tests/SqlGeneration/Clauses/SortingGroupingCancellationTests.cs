using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T241 (issue #241) SQLite SQL-generation regressions for uniform cancellation handling while
/// sorting/grouping preparation builds its keys, plus the frozen positive oracles: an active
/// cancellable token and <see cref="CancellationToken.None"/> must keep the same SQL, the same key
/// column order/count and the applicable plan hashes for both hash flags.
/// <para>
/// The pre-cancelled cases are the RED arm against the original source. Column preparation has its
/// own #207 cancellation coverage (<c>EntitySelectListBuilder.Build</c>), so those tests set
/// <see cref="QueryCommand.IgnoreColumns"/> to keep the assertion on the sorting/grouping
/// preparation site under test instead of the earlier column stage.
/// </para>
/// </summary>
public class SortingGroupingCancellationTests
{
    private const string SortingSql = "select a, b\n from t241_pair\n order by a, b";
    private const string GroupingSql = "select a, b\n from t241_pair\n group by a, b";

    private static readonly CancellationToken PreCancelled = new(canceled: true);

    public SortingGroupingCancellationTests()
    {
        DataContextCache.Clear();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void T241_PreCancelled_Sorting_ThrowsOperationCanceled(bool dontCalculateHash)
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<T241PairEntity>().OrderBy(x => x.A).OrderBy(x => x.B).ToCommand();
        command.IgnoreColumns = true;

        Action act = () => command.PrepareCommand(dontCalculateHash, PreCancelled);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must surface cancellation instead of skipping every sorting key");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void T241_PreCancelled_Grouping_ThrowsOperationCanceled(bool dontCalculateHash)
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<T241PairEntity>().GroupBy(x => new { x.A, x.B }).ToCommand();
        command.IgnoreColumns = true;

        // The public grouping path reaches BuildKeyColumns before its own later grouping-loop guard;
        // the failure is attributed to that call, not to independent coverage of the dominated guard.
        Action act = () => command.PrepareCommand(dontCalculateHash, PreCancelled);

        act.Should().Throw<OperationCanceledException>(
            "the public grouping path must reach the key-column build and surface cancellation");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void T241_ActiveAndNone_SortingGrouping_KeepSqlKeysAndHashes(bool dontCalculateHash)
    {
        using var ctx = SqliteTestContext.Create();
        using var cts = new CancellationTokenSource();

        // Frozen SQL oracle: the projected columns, the source and the rendered key clauses.
        SqlOf(ctx, ctx.From<T241PairEntity>().OrderBy(x => x.A).OrderBy(x => x.B).ToCommand())
            .Should().Be(SortingSql);
        SqlOf(ctx, ctx.From<T241PairEntity>().GroupBy(x => new { x.A, x.B }).ToCommand())
            .Should().Be(GroupingSql);

        var noneSort = ctx.From<T241PairEntity>().OrderBy(x => x.A).OrderBy(x => x.B).ToCommand();
        noneSort.PrepareCommand(dontCalculateHash, CancellationToken.None);
        var activeSort = ctx.From<T241PairEntity>().OrderBy(x => x.A).OrderBy(x => x.B).ToCommand();
        activeSort.PrepareCommand(dontCalculateHash, cts.Token);

        activeSort.IsPrepared.Should().BeTrue();
        activeSort.SortingPlanHash.Should().Be(noneSort.SortingPlanHash);
        if (dontCalculateHash)
        {
            noneSort.SortingPlanHash.Should().Be(0, "a deferred hash is not computed");
        }
        else
        {
            noneSort.SortingPlanHash.Should().NotBe(0, "the sorting plan hash folds in both keys");
        }

        var noneGroup = ctx.From<T241PairEntity>().GroupBy(x => new { x.A, x.B }).ToCommand();
        noneGroup.PrepareCommand(dontCalculateHash, CancellationToken.None);
        var activeGroup = ctx.From<T241PairEntity>().GroupBy(x => new { x.A, x.B }).ToCommand();
        activeGroup.PrepareCommand(dontCalculateHash, cts.Token);

        activeGroup.IsPrepared.Should().BeTrue();
        activeGroup.GroupingPlanHash.Should().Be(noneGroup.GroupingPlanHash);
        if (dontCalculateHash)
        {
            noneGroup.GroupingPlanHash.Should().Be(0, "a deferred hash is not computed");
        }
        else
        {
            noneGroup.GroupingPlanHash.Should().NotBe(0, "the grouping plan hash folds in both keys");
        }

        // Frozen oracle: both grouping keys, in constructor-parameter order, for both token kinds.
        foreach (var grouping in new[] { noneGroup, activeGroup })
        {
            grouping.GroupingList.Should().NotBeNull();
            grouping.GroupingList!.Should().HaveCount(2);
            grouping.GroupingList!.Select(c => c.PropertyName).Should().Equal("A", "B");
            grouping.GroupingList!.Select(c => c.Index).Should().Equal(0, 1);
        }
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");
}

/// <summary>The two-column fixture the T241 SQLite sorting/grouping oracles are pinned to.</summary>
[SqlTable("t241_pair")]
public sealed class T241PairEntity
{
    [Key]
    [Column("a")]
    public int A { get; set; }

    [Column("b")]
    public int B { get; set; }
}
