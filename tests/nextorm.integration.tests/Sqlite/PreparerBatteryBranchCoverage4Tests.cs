using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// QueryPreparer "battery" branch coverage (cycle 4, cluster D1) executed against a live SQLite
/// database. Each fact is a deliberate battery: several heterogeneous clause and expression forms in
/// one command, so a single preparation walks a wide slice of
/// <c>src/nextorm.core/Query/QueryCommand.QueryPreparer.cs</c> <c>RawShape.Matches</c> and the
/// reachable preparer helpers, and the live rows are asserted against the class-owned <c>pb4_*</c>
/// marker tables (dropped, created and seeded by this class's own DDL in the constructor - no global
/// seed-table writes, no global seed-table counts and no <c>InMemoryDataContext</c>).
///
/// <para>
/// Targeted arms (each fact names a DIFFERENT one): <c>GroupingSetsEqual</c> 3867-3881 (arity 3870,
/// element count 3873, element value 3876) through the public <c>GroupByGroupingSets</c>;
/// <c>Paging.HasWithTies</c> 3716; <c>WindowsEqual</c> 3803-3834 (window count 3806, name 3811,
/// order-direction 3820, order-expression 3821) and the frame arms 3824-3830 (start kind, start
/// offset, end kind, exclusion); <c>CtesEqual</c> 3766-3789 (list-length arm 3769);
/// <c>StringListsEqual</c> 3842-3852 (length 3845, element 3848) through the public
/// <c>WithTablesInScopeHint</c>; <c>ExtremeRow</c> 3727 through <c>SelectWhereMax/Min</c>;
/// <c>AttachPrimarySourceLock</c> 316-350 (combined-lock arm 324, non-physical-source arm 330) and
/// <c>IsLockablePhysicalSource</c> 357-366; <c>IsUnrelatedCastOfProjectionItem</c> 1792-1819;
/// the six expression slots 3746-3748 and the join/sorting/union walk 3751-3759 through the wide
/// battery command.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>RawShapeBranchCoverage2Tests</c> owns the
/// self/null short circuits, the entity/projection/column-list arms, the paging/distinct/sorting/
/// join/tag DIFFERENCE arms, the untyped CTE name/body arms and the root raw-SQL/derived-source
/// ineligibility; <c>PreparationBranchCoverage3Tests</c> owns the two-key sorting walk, the grouping
/// TYPE arm, the two-declaration CTE name pairing, the union type/nested body, the DISTINCT flag,
/// the tag and tables-in-scope EQUALITY arms, the partition-difference and frame-TYPE window arms and
/// the naming-convention arm. The arms above are disjoint from all of them.
/// </para>
///
/// <para>
/// Recorded as compared-but-not-rendered (never fabricated): a tables-in-scope hint list is settable
/// through the public builder but SQLite renders no table hint (<c>SqliteDialect.SupportsTableHints</c>
/// stays false), so its <c>RawShape</c> arms are driven by the descriptor comparison while the live
/// counterpart of the same shape runs without the hint; <c>Paging.HasWithTies</c> is set by the
/// public <c>WithTies()</c> but SQLite renders no <c>WITH TIES</c>, so the flag is compared in the
/// descriptor and the live counterpart is the same page without ties. GROUP BY GROUPING SETS is
/// likewise compared in the descriptor; the live counterpart of the same grouping family is the plain
/// <c>GroupBy</c> (recorded rather than fabricated: <c>RawShapeBranchCoverage2Tests</c> measured that
/// SQLite does not render the cube family).
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class PreparerBatteryBranchCoverage4Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists pb4_line;
        drop table if exists pb4_main;
        create table pb4_main
        (
            id integer primary key,
            name text not null,
            qty int not null,
            grp int not null,
            tag int not null,
            score int null
        );
        create table pb4_line
        (
            id integer primary key,
            main_id int not null,
            qty int not null,
            grp int not null,
            label text not null
        );
        insert into pb4_main (id, name, qty, grp, tag, score) values
            (1, 'alpha', 2, 1, 10, 5),
            (2, 'beta',  3, 1, 20, 5),
            (3, 'gamma', 4, 2, 10, 9),
            (4, 'delta', 4, 2, 20, null);
        insert into pb4_line (id, main_id, qty, grp, label) values
            (10, 1, 1, 1, 'x'),
            (11, 1, 3, 1, 'y'),
            (12, 2, 5, 2, 'z'),
            (13, 3, 7, 3, 'w');
        """;

    public PreparerBatteryBranchCoverage4Tests() => ExecuteRaw(Ddl);

    private IDataContext Context => _sut.DataProvider;

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Prepares through the cached path (<c>storeInCache: true</c>), returning the plan holder.</summary>
    private IPreparedQueryCommand<TResult> Prepare<TResult>(QueryCommand<TResult> command)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);

    /// <summary>Executes an already prepared command against the live database.</summary>
    private List<TResult> Rows<TResult>(IPreparedQueryCommand<TResult> prepared) => Context.ToList(prepared);

    /// <summary>Prepares and executes a command in one step.</summary>
    private List<TResult> Run<TResult>(QueryCommand<TResult> command) => Rows(Prepare(command));

    /// <summary>Drops every cached plan (and the raw candidate index) on this context.</summary>
    private void ResetPlanCache() => Context.PurgeQueryCache();

    /// <summary>Captures the value-free raw descriptor of a fresh command, or <see langword="null"/>.</summary>
    private static QueryCommand.RawShape? Shape<TResult>(QueryCommand<TResult> command)
        => QueryCommand.CaptureRawShape(command);

    // ---------------------------------------------------------------------------------------------
    // 1. Grouping-set list: same grouping type, different set list (arity / element count / value)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GroupingSetsBattery_SetListArms_ShouldCompareSetListsAndReadTheGroupingFamily()
    {
        ResetPlanCache();

        QueryCommand<int> Counts(int[][] sets) => Context.From<Pb4Main>()
            .GroupByGroupingSets(x => new { x.Grp, x.Tag }, sets)
            .Select(g => SqlFunctions.Sql.count());

        var oneSet = Counts([[0]]);
        var oneSetAgain = Counts([[0]]);
        var otherIndex = Counts([[1]]);
        var bothIndices = Counts([[0, 1]]);
        var twoSets = Counts([[0], [1]]);

        Shape(oneSet).Should().NotBeNull("a grouping-set list over a plain table is a describable graph");

        Shape(oneSetAgain)!.Matches(Shape(oneSet)!, oneSetAgain).Should()
            .BeTrue("an identical grouping-set list is the same descriptor");
        Shape(otherIndex)!.Matches(Shape(oneSet)!, otherIndex).Should()
            .BeFalse("a different set member index is a different descriptor");
        Shape(bothIndices)!.Matches(Shape(oneSet)!, bothIndices).Should()
            .BeFalse("a set of a different arity is a different descriptor");
        Shape(twoSets)!.Matches(Shape(oneSet)!, twoSets).Should()
            .BeFalse("a different number of sets is a different descriptor");

        // The live counterpart of the same grouping family (SQLite does not render the cube family).
        var grouped = Run(Context.From<Pb4Main>()
            .GroupBy(x => x.Grp)
            .Select(g => new { g.Grp, Count = SqlFunctions.Sql.count() }));
        grouped.Should().HaveCount(2);
        grouped.Single(r => r.Grp == 1).Count.Should().Be(2);
        grouped.Single(r => r.Grp == 2).Count.Should().Be(2);
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Paging: the WITH TIES flag is a descriptor dimension of the same page
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void PagingBattery_WithTiesFlag_ShouldCompareTheFlagAndReadThePlainPage()
    {
        ResetPlanCache();

        var page = Context.From<Pb4Main>().OrderBy(x => x.Id).Page(2, 1).Select(x => x.Id);
        var pageAgain = Context.From<Pb4Main>().OrderBy(x => x.Id).Page(2, 1).Select(x => x.Id);
        var tied = Context.From<Pb4Main>().OrderBy(x => x.Id).Page(2, 1).WithTies().Select(x => x.Id);

        Shape(pageAgain)!.Matches(Shape(page)!, pageAgain).Should()
            .BeTrue("an identical page request is the same descriptor");
        Shape(tied)!.Matches(Shape(page)!, tied).Should()
            .BeFalse("the WITH TIES flag is part of the page descriptor");

        Run(page).Should().Equal(new[] { 2, 3 }, "skip one row and take the next two of the same ordering");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Named windows: list count, window name, order direction and order expression
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void WindowsBattery_NameCountAndOrderArms_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        var ascBuilder = Context.From<Pb4Main>();
        var asc = ascBuilder
            .Window("pb4_w", partitionBy: [x => x.Grp], orderBy: [ascBuilder.Asc(x => x.Id)])
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, RowNumber = SqlFunctions.Sql.row_number().Over("pb4_w") });

        var ascAgainBuilder = Context.From<Pb4Main>();
        var ascAgain = ascAgainBuilder
            .Window("pb4_w", partitionBy: [x => x.Grp], orderBy: [ascAgainBuilder.Asc(x => x.Id)])
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, RowNumber = SqlFunctions.Sql.row_number().Over("pb4_w") });

        var renamedBuilder = Context.From<Pb4Main>();
        var renamed = renamedBuilder
            .Window("pb4_renamed", partitionBy: [x => x.Grp], orderBy: [renamedBuilder.Asc(x => x.Id)])
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, RowNumber = SqlFunctions.Sql.row_number().Over("pb4_renamed") });

        var descendingBuilder = Context.From<Pb4Main>();
        var descending = descendingBuilder
            .Window("pb4_w", partitionBy: [x => x.Grp], orderBy: [descendingBuilder.Desc(x => x.Id)])
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, RowNumber = SqlFunctions.Sql.row_number().Over("pb4_w") });

        var otherExpressionBuilder = Context.From<Pb4Main>();
        var otherExpression = otherExpressionBuilder
            .Window("pb4_w", partitionBy: [x => x.Grp], orderBy: [otherExpressionBuilder.Asc(x => x.Tag)])
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, RowNumber = SqlFunctions.Sql.row_number().Over("pb4_w") });

        var twoWindowsBuilder = Context.From<Pb4Main>();
        var twoWindows = twoWindowsBuilder
            .Window("pb4_w", partitionBy: [x => x.Grp], orderBy: [twoWindowsBuilder.Asc(x => x.Id)])
            .Window("pb4_w2", orderBy: [twoWindowsBuilder.Desc(x => x.Id)])
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, RowNumber = SqlFunctions.Sql.row_number().Over("pb4_w") });

        Shape(asc).Should().NotBeNull("a named window over a plain table is a describable graph");

        Shape(ascAgain)!.Matches(Shape(asc)!, ascAgain).Should()
            .BeTrue("an identical window list is the same descriptor");
        Shape(renamed)!.Matches(Shape(asc)!, renamed).Should()
            .BeFalse("a different window NAME is a different descriptor");
        Shape(descending)!.Matches(Shape(asc)!, descending).Should()
            .BeFalse("a different ORDER BY direction inside the window is a different descriptor");
        Shape(otherExpression)!.Matches(Shape(asc)!, otherExpression).Should()
            .BeFalse("a different ORDER BY expression inside the window is a different descriptor");
        Shape(twoWindows)!.Matches(Shape(asc)!, twoWindows).Should()
            .BeFalse("a different number of named windows is a different descriptor");

        var rows = Run(asc);
        rows.Select(r => r.Id).Should().Equal(1, 2, 3, 4);
        rows.Select(r => r.RowNumber).Should().Equal(new[] { 1, 2, 1, 2 }, "a row number per Grp partition");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Window frame: start kind, start offset, end kind and exclusion arms
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void WindowsBattery_FrameBoundAndExclusionArms_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        QueryCommand<int> Framed(WindowFrame frame)
        {
            var builder = Context.From<Pb4Main>();
            return builder
                .Window("pb4_f", partitionBy: [x => x.Grp], orderBy: [builder.Asc(x => x.Id)], frame: frame)
                .OrderBy(x => x.Id)
                .Select(x => SqlFunctions.Sql.row_number().Over("pb4_f"));
        }

        var unbounded = Framed(WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow));
        var unboundedAgain = Framed(WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow));
        var otherStartKind = Framed(WindowFrame.Rows(WindowFrameBound.Preceding(1), WindowFrameBound.CurrentRow));
        var otherStartOffset = Framed(WindowFrame.Rows(WindowFrameBound.Preceding(2), WindowFrameBound.CurrentRow));
        var otherEndKind = Framed(WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.Following(1)));
        var excluded = Framed(WindowFrame
            .Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)
            .WithExclusion(WindowFrameExclusion.NoOthers));

        Shape(unbounded).Should().NotBeNull("a window frame over a plain table is a describable graph");

        Shape(unboundedAgain)!.Matches(Shape(unbounded)!, unboundedAgain).Should()
            .BeTrue("an identical frame is the same descriptor");
        Shape(otherStartKind)!.Matches(Shape(unbounded)!, otherStartKind).Should()
            .BeFalse("a different frame START KIND is a different descriptor");
        Shape(otherStartOffset)!.Matches(Shape(unbounded)!, otherStartOffset).Should()
            .BeFalse("a different frame START OFFSET is a different descriptor");
        Shape(otherEndKind)!.Matches(Shape(unbounded)!, otherEndKind).Should()
            .BeFalse("a different frame END KIND is a different descriptor");
        Shape(excluded)!.Matches(Shape(unbounded)!, excluded).Should()
            .BeFalse("a frame EXCLUSION is part of the descriptor");

        Run(unbounded).Should().Equal(new[] { 1, 2, 1, 2 }, "a row number per Grp partition");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. CTE declaration list: the list-length arm with the same projection
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void CteBattery_DeclarationListLength_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        QueryCommand<int> OneCte()
        {
            var body = Context.From<Pb4Main>().Where(m => m.Grp == 1).Select(m => new { m.Id });
            return Context.With("pb4_c1", body)
                .From("pb4_c1")
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        QueryCommand<int> TwoCtes()
        {
            var bodyA = Context.From<Pb4Main>().Where(m => m.Grp == 1).Select(m => new { m.Id });
            var bodyB = Context.From<Pb4Main>().Where(m => m.Grp == 2).Select(m => new { m.Id });
            return Context.With("pb4_c1", bodyA)
                .With("pb4_c2", bodyB)
                .From("pb4_c1")
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        var one = OneCte();
        var oneAgain = OneCte();
        var two = TwoCtes();

        Shape(one).Should().NotBeNull("a single plain-table CTE declaration is a describable graph");

        Shape(oneAgain)!.Matches(Shape(one)!, oneAgain).Should()
            .BeTrue("an identical single-declaration list is the same descriptor");
        Shape(two)!.Matches(Shape(one)!, two).Should()
            .BeFalse("a declaration list of a different length is a different descriptor");

        Run(one).Should().Equal(1, 2);
        Run(two).Should().Equal(1, 2);
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Hints and string lists: list-length and element arms of the tables-in-scope hint list
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ScopeHintBattery_ListLengthAndElementArms_ShouldCompareAndReadThePlainShape()
    {
        ResetPlanCache();

        var plain = Context.From<Pb4Main>().Where(x => x.Id <= 3).OrderBy(x => x.Id).Select(x => x.Id);
        var hinted = Context.From<Pb4Main>()
            .WithTablesInScopeHint("pb4_main")
            .Where(x => x.Id <= 3)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        var hintedAgain = Context.From<Pb4Main>()
            .WithTablesInScopeHint("pb4_main")
            .Where(x => x.Id <= 3)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        var otherElement = Context.From<Pb4Main>()
            .WithTablesInScopeHint("pb4_line")
            .Where(x => x.Id <= 3)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        var longerList = Context.From<Pb4Main>()
            .WithTablesInScopeHint("pb4_main", "pb4_line")
            .Where(x => x.Id <= 3)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        Shape(hinted).Should().NotBeNull("a hint list is a describable graph even when SQLite renders no hint");

        Shape(hintedAgain)!.Matches(Shape(hinted)!, hintedAgain).Should()
            .BeTrue("an identical hint list is the same descriptor");
        Shape(otherElement)!.Matches(Shape(hinted)!, otherElement).Should()
            .BeFalse("a different hint element is a different descriptor");
        Shape(longerList)!.Matches(Shape(hinted)!, longerList).Should()
            .BeFalse("a hint list of a different length is a different descriptor");
        Shape(plain)!.Matches(Shape(hinted)!, plain).Should()
            .BeFalse("a hint list present on one side only is a different descriptor");

        Run(plain).Should().Equal(1, 2, 3);
    }

    // ---------------------------------------------------------------------------------------------
    // 7. Extreme-row clause: the ties mode and the max/min direction are descriptor dimensions
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ExtremeRowBattery_TiesAndDirectionArms_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        QueryCommand<Pb4Main> MaxAll() => Context.From<Pb4Main>()
            .SelectWhereMax(x => x.Score!, ExtremeRowTies.All, x => x.Grp);
        QueryCommand<Pb4Main> MaxOne() => Context.From<Pb4Main>()
            .SelectWhereMax(x => x.Score!, ExtremeRowTies.One, x => x.Grp);
        QueryCommand<Pb4Main> MinAll() => Context.From<Pb4Main>()
            .SelectWhereMin(x => x.Score!, ExtremeRowTies.All, x => x.Grp);

        var maxAll = MaxAll();
        var maxOne = MaxOne();
        var minAll = MinAll();

        Shape(maxAll).Should().NotBeNull("an extreme-row descriptor over a plain table is describable");

        Shape(maxOne)!.Matches(Shape(maxAll)!, maxOne).Should()
            .BeFalse("the ties mode is part of the extreme-row descriptor");
        Shape(minAll)!.Matches(Shape(maxAll)!, minAll).Should()
            .BeFalse("a max clause compared with a min clause differs in the extreme-row descriptor");

        // Live: per group the maximal score is 5 (group 1: two tied rows) and 9 (group 2: one row).
        var rows = Run(maxAll);
        rows.Select(r => r.Id).OrderBy(x => x).Should().Equal(1, 2, 3);
        rows.Should().OnlyContain(r => r.Grp == 1 || r.Grp == 2);
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Source lock attachment: the prepare-time rejection arms on a dialect without source locks
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SourceLockBattery_CombinedAndNonPhysicalArms_ShouldRejectWhileThePlainReadRuns()
    {
        ResetPlanCache();

        Run(Context.From<Pb4Main>().OrderBy(x => x.Id).Select(x => x.Id))
            .Should().Equal(new[] { 1, 2, 3, 4 }, "the plain read is the live counterpart of the lock facts");

        // A whole-query lock combined with a source-level lock is ambiguous and rejected at preparation.
        FluentActions.Invoking(() => Context.From<Pb4Main>(o => o.ForUpdate())
                .ForUpdate()
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be combined with a source-level lock*");

        // A source-level lock on a derived subquery source is rejected at preparation.
        var derived = Context.From<Pb4Main>().Where(x => x.Id > 1).Select(x => new { x.Id });
        FluentActions.Invoking(() => Context.From(derived, o => o.ForUpdate()).Select(x => x.Id).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*can only target a physical table source*");

        // A source-level lock on a raw-SQL source is rejected at preparation.
        var raw = Context.FromSql("select id from pb4_main where id <= 2").Select(t => t["id"].AsInt);
        FluentActions.Invoking(() => Context.From(raw, o => o.ForUpdate()).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*can only target a physical table source*");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. Unrelated cast of a joined projection item fails closed while the column projection runs
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ProjectionCastBattery_UnrelatedCast_ShouldRejectWhileTheColumnProjectionRuns()
    {
        ResetPlanCache();

        // Live counterpart: the explicit column projection over the same join.
        var rows = Run(Context.From<Pb4Main>()
            .Join(Context.From<Pb4Line>(), (m, l) => m.Id == l.MainId)
            .Select(p => new { MainId = p.Item1.Id, LineId = p.Item2.Id }));
        rows.Should().HaveCount(4);
        rows.Select(r => r.LineId).OrderBy(x => x).Should().Equal(10, 11, 12, 13);

        // A whole-item projection cast to an unrelated result type must fail closed.
        FluentActions.Invoking(() => Context.From<Pb4Main>()
                .Join(Context.From<Pb4Line>(), (m, l) => m.Id == l.MainId)
                .Select(p => (object)p.Item1)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*cast to an unrelated result type*");
    }

    // ---------------------------------------------------------------------------------------------
    // 10. Cross join: the joined-source walk and the left-condition arm
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void CrossJoinBattery_LeftConditionArm_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        QueryCommand<int> Cross(int maxId) => Context.From<Pb4Main>()
            .CrossJoin(Context.From<Pb4Line>())
            .Where(p => p.Item1.Id <= maxId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => p.Item1.Id);

        var two = Cross(2);
        var twoAgain = Cross(2);
        var three = Cross(3);

        // Recorded (never fabricated): a cross-join graph is not capture-eligible, so only the live part
        // of this fact drives the preparer (same disposition as the root derived/raw-SQL sources).
        Shape(two).Should().BeNull("a cross-join graph is not a capturable raw shape");

        Run(two).Should().HaveCount(8, "two main rows times four line rows");
        Run(three).Should().HaveCount(12, "three main rows times four line rows");
        Run(twoAgain).Should().HaveCount(8, "the identical cross join is a live query, not a rejected one");
    }

    // ---------------------------------------------------------------------------------------------
    // 11. Wide battery + A -> A -> B -> B -> A differential on ONE context
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void BatteryDifferential_SameContextRoundTrip_ShouldReuseAndRePrepareLivePlans()
    {
        ResetPlanCache();

        var minQty = 2;

        QueryCommand<int> Battery(string tag) => Context.From<Pb4Main>()
            .Where(x => x.Grp <= 2
                        && x.Qty + 1 > minQty
                        && x.Name != "nope"
                        && x.Name.Contains("a")
                        && SqlFunctions.Sql.@in(x.Tag, 10, 20)
                        && !(x.Score == null)
                        && Context.From<Pb4Line>().Where(l => l.MainId == x.Id).Any())
            .OrderBy(x => x.Tag)
            .OrderByDescending(x => x.Qty)
            .Select(x => x.Id)
            .WithTag(tag);

        var a1 = Battery("pb4_tag");
        Rows(Prepare(a1)).Should().Equal(3, 1, 2);

        var a2 = Battery("pb4_tag");
        Rows(Prepare(a2)).Should().Equal(new[] { 3, 1, 2 }, "an identical battery shape reuses the live plan");

        var b1 = Battery("pb4_tag_other");
        Rows(Prepare(b1)).Should().Equal(new[] { 3, 1, 2 }, "the changed tag text still reads the same live rows");

        var b2 = Battery("pb4_tag_other");
        Rows(Prepare(b2)).Should().Equal(new[] { 3, 1, 2 }, "the changed-tag shape reuses its own live plan");

        var a3 = Battery("pb4_tag");
        Rows(Prepare(a3)).Should().Equal(new[] { 3, 1, 2 }, "the original shape is correct again after the miss");
    }

    /// <summary>Marker row of the class-owned <c>pb4_main</c> table.</summary>
    [SqlTable("pb4_main")]
    public sealed class Pb4Main
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("qty")]
        public int Qty { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("tag")]
        public int Tag { get; set; }

        [Column("score")]
        public int? Score { get; set; }
    }

    /// <summary>Marker row of the class-owned <c>pb4_line</c> table.</summary>
    [SqlTable("pb4_line")]
    public sealed class Pb4Line
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("main_id")]
        public int MainId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }
}
