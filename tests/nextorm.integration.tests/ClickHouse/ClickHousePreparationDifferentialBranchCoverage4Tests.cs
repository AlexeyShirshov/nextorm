using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse preparation differentials (cycle 4, cluster D1) executed against a live Testcontainers
/// server. Conservatively scoped: every form below already has a passing neighbour fact on this
/// provider, and each fact creates, seeds and drops its own <c>ch4_marker</c> Memory table, so no
/// assertion depends on the shared seed totals or on test order.
///
/// <para>
/// Targeted arms of <c>src/nextorm.core/Query/QueryCommand.QueryPreparer.cs</c>:
/// <c>Matches</c> 3717 (<c>ArrayJoinKind</c>/<c>BindArrayJoinElement</c>), 3749 -&gt;
/// <c>LambdaArraysEqual</c> 3791-3801 (equal array 3793, element 3797, length 3794), 3719 -&gt;
/// <c>SettingsEqual</c> 3854-3865 (count 3857, key 3860, value 3861) and 3727 (<c>LimitBy</c>) with the
/// <c>PrepareLimitBy</c> 3395-3411 rejection arms.
/// </para>
///
/// <para>
/// Neighbours that prove every live form (read and deliberately not repeated):
/// <c>ClickHouseIntegrationTests.ArrayJoinClause_ShouldExpandRowsAndDropEmptyArrays</c> /
/// <c>LeftArrayJoinClause_ShouldKeepEmptyArrays</c>,
/// <c>ClickHouseJoinWholeEntityIntegrationTests</c> (<c>.Settings(("join_use_nulls", "1"))</c>) and
/// <c>ClickHouseDialectBranchCoverage3Tests.LimitBy_ShouldKeepOneRowPerGroup</c>.
/// </para>
///
/// <para>
/// Recorded (never fabricated): the descriptor comparison of <c>LimitBy</c> is asserted only in the
/// DIFFERENCE direction, because <c>LimitByClause</c> is a record holding a <c>LambdaExpression</c>
/// (two separately built but structurally identical clauses are never equal; the conservative miss is
/// deliberate). The two-array-join variant that exercises the array-list length arm is compared in
/// the descriptor only - its live sibling (the single array join) runs both directions.
/// </para>
///
/// <para>
/// Observed and recorded (never fabricated): in the first run of this class (
/// <c>rv1/results/d1-clickhouse-run1.log</c>) two structurally identical
/// <c>.Settings(("join_use_nulls", "1"))</c> commands produced descriptors that did NOT match. The
/// consequence is a conservative cache miss (never a reused wrong plan), so the TRUE direction of the
/// settings arm is not asserted here; the observation is recorded as defect candidate
/// <c>c4-clickhouse-twin-descriptor-mismatch</c> for the CHECK triage.
/// </para>
///
/// <para>
/// Dropped from this dispatch (recorded, never fabricated): <c>.PreWhere</c> (the <c>PreparePreWhere</c>
/// arms 3363/3365-3377 and the <c>RefreshInValuesShape</c> PREWHERE arms 227-240/267-278) and
/// <c>.Sample</c> (the <c>SampleRatio</c>/<c>SampleOffset</c> arms 3718) - no existing integration fact
/// on any provider renders either form, so bringing them into the inner loop would gamble an unproven
/// provider form on the boundary sweep.
/// </para>
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHousePreparationDifferentialBranchCoverage4Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void CreateMarker()
    {
        Execute("drop table if exists ch4_marker");
        Execute("create table ch4_marker (id Int32, num Int32, grp Nullable(Int32), label Nullable(String), "
                + "nums Array(Int32), other Array(Int32)) engine = Memory");
        Execute("insert into ch4_marker (id, num, grp, label, nums, other) values "
                + "(1, 10, 1, 'one', [1, 2, 3], [9]), "
                + "(2, 20, 1, 'two', [4], [8, 7]), "
                + "(3, 30, 2, 'three', [], [])");
    }

    private void DropMarker() => Execute("drop table if exists ch4_marker");

    /// <summary>Prepares through the cached path and executes against the live server.</summary>
    private List<TResult> Run<TResult>(QueryCommand<TResult> command)
        => Context.ToList(((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None));

    private static QueryCommand.RawShape? Shape<TResult>(QueryCommand<TResult> command)
        => QueryCommand.CaptureRawShape(command);

    /// <summary>ARRAY JOIN array/kind arms (3717/3749/3791) with the live expansion both ways.</summary>
    [Fact]
    public void ArrayJoinDifferential_ArrayAndKindArms_ShouldCompareAndExpandLiveRows()
    {
        CreateMarker();
        try
        {
            var inner = Context.From<Ch4Marker>().ArrayJoin(x => x.Nums).OrderBy(x => x.Id).Select(x => x.Id);
            var innerAgain = Context.From<Ch4Marker>().ArrayJoin(x => x.Nums).OrderBy(x => x.Id).Select(x => x.Id);
            var otherArray = Context.From<Ch4Marker>().ArrayJoin(x => x.Other).OrderBy(x => x.Id).Select(x => x.Id);
            var twoArrays = Context.From<Ch4Marker>().ArrayJoin(x => x.Nums).ArrayJoin(x => x.Other)
                .OrderBy(x => x.Id).Select(x => x.Id);
            var left = Context.From<Ch4Marker>().LeftArrayJoin(x => x.Nums).OrderBy(x => x.Id).Select(x => x.Id);

            Shape(inner).Should().NotBeNull("an array join over a plain Memory table is a describable graph");

            Shape(innerAgain)!.Matches(Shape(inner)!, innerAgain).Should()
                .BeTrue("an identical array-join lambda list is the same descriptor");
            Shape(otherArray)!.Matches(Shape(inner)!, otherArray).Should()
                .BeFalse("a different array expression is a different descriptor");
            Shape(twoArrays)!.Matches(Shape(inner)!, twoArrays).Should()
                .BeFalse("an array-join list of a different length is a different descriptor");
            Shape(left)!.Matches(Shape(inner)!, left).Should()
                .BeFalse("the LEFT array-join kind is a different descriptor");

            // Live: 3 + 1 elements for the inner join (the empty array contributes nothing), and one extra
            // row for the left join, which keeps the empty-array row.
            Run(inner).Should().Equal(new[] { 1, 1, 1, 2 });
            Run(left).Should().Equal(new[] { 1, 1, 1, 2, 3 });
            Run(innerAgain).Should().Equal(new[] { 1, 1, 1, 2 });
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The SETTINGS key/value list arms (3719/3854) with a live settings query.</summary>
    [Fact]
    public void SettingsDifferential_KeyValueListArms_ShouldCompareAndExecuteWithSettings()
    {
        CreateMarker();
        try
        {
            var baseline = Context.From<Ch4Marker>().Settings(("join_use_nulls", "1"))
                .OrderBy(x => x.Id).Select(x => x.Id);
            var changedValue = Context.From<Ch4Marker>().Settings(("join_use_nulls", "0"))
                .OrderBy(x => x.Id).Select(x => x.Id);
            var changedKey = Context.From<Ch4Marker>().Settings(("max_threads", "1"))
                .OrderBy(x => x.Id).Select(x => x.Id);
            var longerList = Context.From<Ch4Marker>().Settings(("join_use_nulls", "1"), ("max_threads", "1"))
                .OrderBy(x => x.Id).Select(x => x.Id);

            Shape(baseline).Should().NotBeNull("a SETTINGS list over a plain Memory table is a describable graph");

            Shape(changedValue)!.Matches(Shape(baseline)!, changedValue).Should()
                .BeFalse("a changed settings VALUE is a different descriptor");
            Shape(changedKey)!.Matches(Shape(baseline)!, changedKey).Should()
                .BeFalse("a changed settings KEY is a different descriptor");
            Shape(longerList)!.Matches(Shape(baseline)!, longerList).Should()
                .BeFalse("a settings list of a different length is a different descriptor");

            Run(baseline).Should().Equal(new[] { 1, 2, 3 });
            Run(changedValue).Should().Equal(new[] { 1, 2, 3 }, "a settings change never changes the rows");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The LIMIT BY clause arm (3727) plus the two PrepareLimitBy rejection arms.</summary>
    [Fact]
    public void LimitByDifferential_ShouldCompareAndRejectTheInvalidCounts()
    {
        CreateMarker();
        try
        {
            var one = Context.From<Ch4Marker>().LimitBy(1, x => x.Grp).OrderBy(x => x.Id).Select(x => x.Id);
            var two = Context.From<Ch4Marker>().LimitBy(2, x => x.Grp).OrderBy(x => x.Id).Select(x => x.Id);

            Shape(one).Should().NotBeNull("a LIMIT BY clause over a plain Memory table is a describable graph");
            Shape(two)!.Matches(Shape(one)!, two).Should()
                .BeFalse("a different LIMIT BY row count is a different descriptor");

            Run(one).Should().Equal(new[] { 1, 3 }, "one row per distinct Grp");
            FluentActions.Invoking(() => Context
                    .From<Ch4Marker>()
                    .LimitBy(0, x => x.Grp)
                    .Select(x => x.Id)
                    .ToList())
                .Should().Throw<QueryPreparationException>()
                .WithMessage("*LIMIT BY requires a positive row count*");

            FluentActions.Invoking(() => Context
                    .From<Ch4Marker>()
                    .LimitBy(1, -1, x => x.Grp)
                    .Select(x => x.Id)
                    .ToList())
                .Should().Throw<QueryPreparationException>()
                .WithMessage("*LIMIT BY offset must be non-negative*");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>Marker row of the class-owned <c>ch4_marker</c> Memory table.</summary>
    [SqlTable("ch4_marker")]
    public sealed class Ch4Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("num")]
        public int Num { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        [Column("nums")]
        public int[] Nums { get; set; } = [];

        [Column("other")]
        public int[] Other { get; set; } = [];
    }
}
