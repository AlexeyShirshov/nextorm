using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

/// <summary>
/// Plan-equality differential branch coverage (cycle 4, cluster D2) executed against a live SQLite
/// database. Every fact compares <b>two commands that have both been prepared through the cached path</b>
/// (the prepared state is what <c>QueryPlanEqualityComparer.EqualsCore</c> reads), then executes the same
/// shapes and asserts the live rows, so a wrong cache reuse would surface as a wrong row set. The marker
/// rows belong to this class (<c>pd4_marker</c>/<c>pd4_line</c>, dropped, created and seeded from this
/// class's own DDL in the constructor) - no global seed-table writes, no global counts and no
/// <see cref="InMemoryDataContext"/>.
///
/// <para>
/// Targeted arms (each fact names a DIFFERENT one): <c>QueryPlanEqualityComparer.EqualsCore</c>
/// (<c>src/nextorm.core/Query/QueryPlanEqualityComparer.cs:66</c>) reached field by field on the equal
/// path; <c>StringListsEqual</c> :234 (null arm :237, length :238, element :240-242) through the plan key's
/// index-hint list (SQLite renders <c>INDEXED BY</c>/<c>NOT INDEXED</c>; its table-level hint lists are
/// rejected at preparation); <c>WindowsEqual</c> :248 with the six-way FRAME cluster :274-280 (unit, start
/// kind, start offset, end kind, end offset, exclusion); <c>GroupingSetsEqual</c> :315 (set count :319,
/// arity :325, member index :327-329); <c>GetHashCodeCore</c> :390 and
/// <c>ExpressionPlanEqualityComparer.GetHashCode</c> :72 / <c>Equals</c> :97 with the nested
/// <c>Visitor</c> :742. <c>KeyValueListsEqual</c> :286 (<c>Settings</c>) and <c>ExpressionListsEqual</c>
/// :301 (<c>ArrayJoinExpressions</c>) have no public writer on SQLite - the only writers are the ClickHouse
/// <c>.Settings</c>/<c>.ArrayJoin</c> extensions - so they are recorded as deferred in
/// <c>rv1/inputs/deferred.json</c>, never fabricated here.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>PlanEqualityBranchCoverage2Tests</c> owns the
/// expression comparer's constant value/type, constant-vs-closure, member identity, static member, unary
/// convert-vs-member, negation, null-vs-non-null closure, nested binaries, method calls, conditionals,
/// default, type-binary, member-init, anonymous/array, list-init/nested-lambda, and the literal-vs-closure
/// and convert-vs-member plan keys. <c>PlanKeyBranchCoverage3Tests</c> owns the projection nodes, the
/// constant/captured/captured-member/convert plan keys and the tag/paging/distinct/eager-shape axes. This
/// class instead drives the PLAN-KEY list comparers (string lists, grouping sets, window frames), the
/// instance-member (captured-object) arm, a null-valued closure against a null constant, a char/string
/// pair, a hand-built predicate with FRESH parameter instances, and one/two-level correlated nesting.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class PlanEqualityDifferentialBranchCoverage4Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists pd4_line;
        drop table if exists pd4_marker;
        create table pd4_marker
        (
            id integer primary key,
            name text not null,
            code text not null,
            qty int not null,
            grp int not null,
            tag int not null,
            score int null
        );
        create table pd4_line
        (
            id integer primary key,
            marker_id int not null,
            qty int not null,
            label text not null
        );
        insert into pd4_marker (id, name, code, qty, grp, tag, score) values
            (1, 'alpha', 'a', 1, 1, 10, 5),
            (2, 'beta',  'b', 2, 1, 20, 5),
            (3, 'gamma', 'a', 3, 2, 10, 9),
            (4, 'delta', 'b', 4, 2, 20, null);
        insert into pd4_line (id, marker_id, qty, label) values
            (10, 1, 1, 'x'),
            (11, 1, 3, 'y'),
            (12, 2, 5, 'z'),
            (13, 3, 7, 'w');
        create index pd4_grp_idx on pd4_marker (grp);
        create index pd4_tag_idx on pd4_marker (tag);
        """;

    private readonly EntityBuilder<Pd4Line> _lines;

    public PlanEqualityDifferentialBranchCoverage4Tests()
    {
        ExecuteRaw(Ddl);
        _lines = Context.From<Pd4Line>();
    }

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

    private List<TResult> Rows<TResult>(IPreparedQueryCommand<TResult> prepared) => Context.ToList(prepared);

    private List<TResult> Run<TResult>(QueryCommand<TResult> command) => Rows(Prepare(command));

    /// <summary>Drops every cached plan (and the raw candidate index) on this context.</summary>
    private void ResetPlanCache() => Context.PurgeQueryCache();

    /// <summary>
    /// Prepares the command in place (hashes included) without rendering SQL, so a provider-specific
    /// clause that this dialect rejects at render time still reaches the provider-independent plan key.
    /// </summary>
    private static void Resolve(QueryCommand command) => command.PrepareCommand(dontCalculateHash: false, CancellationToken.None);

    /// <summary>A captured object whose member is read inside a predicate expression.</summary>
    private sealed class FloorProbe
    {
        public int Floor { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // 1. Constant vs captured local vs captured-object member (instance member of the SAME closure)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the plan key's <c>MemberExpression</c> comparison for a captured-object member
    /// (a member of a member of the closure constant), which the neighbours never compare - they use a
    /// direct closure field and a STATIC member. The equal arm reuses one plan and re-binds the current
    /// value; the changed value must change the live rows while keeping the same plan.
    /// </summary>
    [Fact]
    public void MemberArms_ConstantCapturedLocalAndCapturedObjectMember_ShouldKeyAndRebind()
    {
        ResetPlanCache();

        var floor = 2;
        var probe = new FloorProbe { Floor = 2 };

        Expression<Func<Pd4Marker, bool>> literal = x => x.Id > 2;
        Expression<Func<Pd4Marker, bool>> literalAgain = x => x.Id > 2;
        Expression<Func<Pd4Marker, bool>> captured = x => x.Id > floor;
        Expression<Func<Pd4Marker, bool>> capturedAgain = x => x.Id > floor;
        Expression<Func<Pd4Marker, bool>> member = x => x.Id > probe.Floor;
        Expression<Func<Pd4Marker, bool>> memberAgain = x => x.Id > probe.Floor;

        var expressionComparer = Context.From<Pd4Marker>().Select(x => x.Id).GetExpressionPlanEqualityComparer();

        expressionComparer.Equals(literal, literalAgain).Should().BeTrue("two identical literal predicates are equal");
        expressionComparer.Equals(captured, capturedAgain).Should()
            .BeTrue("the same closure field at the same site is equal (CompareMember constant-receiver path)");
        expressionComparer.Equals(member, memberAgain).Should()
            .BeTrue("a captured-object member compares by member name/type on the same closure constant");
        expressionComparer.Equals(literal, captured).Should().BeFalse("a constant node and a closure field differ");
        expressionComparer.Equals(captured, member).Should()
            .BeFalse("a direct closure field and a member of a captured object are different member names");
        expressionComparer.GetHashCode(captured).Should().Be(expressionComparer.GetHashCode(capturedAgain));
        expressionComparer.GetHashCode(literal).Should().NotBe(expressionComparer.GetHashCode(captured));

        QueryCommand<int> ByLiteral() => Context.From<Pd4Marker>().Where(literal).OrderBy(x => x.Id).Select(x => x.Id);
        QueryCommand<int> ByCaptured() => Context.From<Pd4Marker>().Where(captured).OrderBy(x => x.Id).Select(x => x.Id);
        QueryCommand<int> ByMember() => Context.From<Pd4Marker>().Where(member).OrderBy(x => x.Id).Select(x => x.Id);

        Run(ByLiteral()).Should().Equal(3, 4);

        var capturedFirst = Prepare(ByCaptured());
        Rows(capturedFirst).Should().Equal(3, 4);
        Prepare(ByCaptured()).Should().BeSameAs(capturedFirst, "one closure site keeps one live plan");

        var memberFirst = Prepare(ByMember());
        Rows(memberFirst).Should().Equal(3, 4);
        Prepare(ByMember()).Should().BeSameAs(memberFirst, "the captured-object member keeps one plan");

        probe.Floor = 0;
        var rebound = Prepare(ByMember());
        rebound.Should().BeSameAs(memberFirst, "the member node is compared by name, not by its current value");
        Rows(rebound).Should().Equal(new[] { 1, 2, 3, 4 }, "the reused plan re-binds the current member value");
        probe.Floor = 2;

        var literalCommand = ByLiteral();
        var capturedCommand = ByCaptured();
        Prepare(literalCommand);
        Prepare(capturedCommand);
        var planComparer = literalCommand.GetQueryPlanEqualityComparer();
        planComparer.Equals(literalCommand, capturedCommand).Should()
            .BeFalse("a literal condition and a parameterised condition are different plans");
        planComparer.GetHashCode(literalCommand).Should().NotBe(planComparer.GetHashCode(capturedCommand));
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Null constant vs a NULL-VALUED closure member
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a null constant against a closure member whose evaluated value is also null -
    /// the neighbour compares a null constant with a NON-null closure. The two shapes read the same rows
    /// but stay different expression trees.
    /// </summary>
    [Fact]
    public void NullArms_NullConstantAndNullValuedClosure_ShouldDifferAsNodesAndReadTheSameRows()
    {
        ResetPlanCache();

        int? absentScore = null;
        int? liveScore = 5;

        Expression<Func<Pd4Marker, bool>> isNull = x => x.Score == null;
        Expression<Func<Pd4Marker, bool>> isNullAgain = x => x.Score == null;
        Expression<Func<Pd4Marker, bool>> isNullCaptured = x => x.Score == absentScore;
        Expression<Func<Pd4Marker, bool>> isNullCapturedAgain = x => x.Score == absentScore;
        Expression<Func<Pd4Marker, bool>> isScore = x => x.Score == liveScore;

        var comparer = Context.From<Pd4Marker>().Select(x => x.Id).GetExpressionPlanEqualityComparer();

        comparer.Equals(isNull, isNullAgain).Should().BeTrue("two identical null tests are equal");
        comparer.Equals(isNullCaptured, isNullCapturedAgain).Should().BeTrue("the same closure site is equal");
        comparer.Equals(isNull, isNullCaptured).Should()
            .BeFalse("a null constant and a null-valued closure member are different node kinds");
        comparer.Equals(isNull, isScore).Should().BeFalse("a null test and a value test differ");
        comparer.GetHashCode(isNull).Should().Be(comparer.GetHashCode(isNullAgain));
        comparer.GetHashCode(isNull).Should().NotBe(comparer.GetHashCode(isNullCaptured));

        Run(Context.From<Pd4Marker>().Where(isNull).OrderBy(x => x.Id).Select(x => x.Id)).Should().Equal(4);
        Run(Context.From<Pd4Marker>().Where(isNullCaptured).OrderBy(x => x.Id).Select(x => x.Id)).Should()
            .Equal(new[] { 4 }, "a NULL-valued captured operand is compared with the approved total form, so it reads " +
                      "the same rows as the literal IS NULL test while the expression trees stay distinct");
        Run(Context.From<Pd4Marker>().Where(isScore).OrderBy(x => x.Id).Select(x => x.Id)).Should().Equal(1, 2);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Char constant vs string constant, and unary/binary/method conversion shapes
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a char constant (materialised under a runtime <c>char.ToString()</c> call) is
    /// compared with a string constant of the same text - the neighbour only compares a unary convert with
    /// a member. The live counterpart of the same predicate family is the string constant, which executes;
    /// the char form is recorded as compared-not-rendered (no SQL rendering is claimed for it).
    /// </summary>
    [Fact]
    public void TextAndConversionArms_CharStringAndUnaryBinaryCall_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        var letter = 'a';

        Expression<Func<Pd4Marker, bool>> charShape = x => x.Code == letter.ToString();
        Expression<Func<Pd4Marker, bool>> charShapeAgain = x => x.Code == letter.ToString();
        Expression<Func<Pd4Marker, bool>> stringShape = x => x.Code == "a";
        Expression<Func<Pd4Marker, bool>> stringShapeAgain = x => x.Code == "a";

        var comparer = Context.From<Pd4Marker>().Select(x => x.Id).GetExpressionPlanEqualityComparer();

        comparer.Equals(charShape, charShapeAgain).Should().BeTrue("the same char conversion site is equal");
        comparer.Equals(stringShape, stringShapeAgain).Should().BeTrue("two identical string constants are equal");
        comparer.Equals(charShape, stringShape).Should()
            .BeFalse("a char conversion node and a string constant are different shapes of the same text");
        comparer.GetHashCode(charShape).Should().Be(comparer.GetHashCode(charShapeAgain));
        comparer.GetHashCode(charShape).Should().NotBe(comparer.GetHashCode(stringShape));

        Expression<Func<Pd4Marker, bool>> unary = x => (long)x.Id > 1L;
        Expression<Func<Pd4Marker, bool>> unaryAgain = x => (long)x.Id > 1L;
        Expression<Func<Pd4Marker, bool>> binary = x => x.Id + 0 > 1;
        Expression<Func<Pd4Marker, bool>> call = x => Convert.ToInt64(x.Id) > 1L;
        Expression<Func<Pd4Marker, bool>> callAgain = x => Convert.ToInt64(x.Id) > 1L;

        comparer.Equals(unary, unaryAgain).Should().BeTrue("two identical convert nodes are equal");
        comparer.Equals(call, callAgain).Should().BeTrue("two identical Convert.ToInt64 calls are equal");
        comparer.Equals(unary, binary).Should().BeFalse("a unary convert and a binary addition are different nodes");
        comparer.Equals(unary, call).Should().BeFalse("a convert node and a method call are different nodes");
        comparer.GetHashCode(unary).Should().Be(comparer.GetHashCode(unaryAgain));

        Run(Context.From<Pd4Marker>().Where(stringShape).OrderBy(x => x.Id).Select(x => x.Id)).Should().Equal(1, 3);
        Run(Context.From<Pd4Marker>().Where(unary).OrderBy(x => x.Id).Select(x => x.Id)).Should().Equal(2, 3, 4);
        Run(Context.From<Pd4Marker>().Where(binary).OrderBy(x => x.Id).Select(x => x.Id)).Should()
            .Equal(new[] { 2, 3, 4 }, "the binary counterpart of the unary conversion reads the same live rows");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Plan-key string lists (index hints): null, length and element arms
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>QueryPlanEqualityComparer.StringListsEqual</c> on the plan key over the
    /// index-hint list (the neighbours only drive the raw-shape helper, and the table/tables-in-scope hint
    /// lists are rejected by SQLite at preparation). The null arm compares a plain source with a hinted
    /// one, the length arm compares one hinted name with <c>NOT INDEXED</c> (an empty list), and the element
    /// arm swaps the index name; every shape executes live.
    /// </summary>
    [Fact]
    public void StringListArms_PlanKeyIndexHints_ShouldCompareNullLengthAndElement()
    {
        ResetPlanCache();

        QueryCommand<int> Hinted(IndexHintKind? kind, params string[] indexes)
        {
            var builder = kind is null
                ? Context.From<Pd4Marker>()
                : Context.From<Pd4Marker>(o => o.WithIndex(kind.Value, indexes));
            return builder.Where(x => x.Id <= 3).OrderBy(x => x.Id).Select(x => x.Id);
        }

        var hinted = Hinted(IndexHintKind.Use, "pd4_grp_idx");
        var hintedAgain = Hinted(IndexHintKind.Use, "pd4_grp_idx");
        var otherElement = Hinted(IndexHintKind.Use, "pd4_tag_idx");
        var ignored = Hinted(IndexHintKind.Ignore);
        var plain = Hinted(null);

        Rows(Prepare(hinted)).Should().Equal(1, 2, 3);

        var planComparer = hinted.GetQueryPlanEqualityComparer();

        var hintedAgainPrepared = Prepare(hintedAgain);
        planComparer.Equals(hinted, hintedAgain).Should().BeTrue("an identical index-hint list is the same plan");
        planComparer.GetHashCode(hinted).Should().Be(planComparer.GetHashCode(hintedAgain));
        hintedAgainPrepared.Should().BeSameAs(Prepare(hinted), "the equal hint list reuses one live plan");

        planComparer.Equals(hinted, otherElement).Should().BeFalse("a different index-hint element is a different plan");
        planComparer.Equals(hinted, ignored).Should().BeFalse("a hint list of a different length is a different plan");
        planComparer.Equals(hinted, plain).Should().BeFalse("a hint list present on one side only is a different plan");
        planComparer.GetHashCode(hinted).Should().NotBe(planComparer.GetHashCode(ignored));

        Rows(Prepare(otherElement)).Should().Equal(new[] { 1, 2, 3 }, "the changed index name still reads the same rows");
        Rows(Prepare(ignored)).Should().Equal(new[] { 1, 2, 3 }, "NOT INDEXED reads the same rows");
        Rows(Prepare(plain)).Should().Equal(new[] { 1, 2, 3 }, "the un-hinted counterpart of the same filter is live");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Plan-key grouping sets: set count, arity and member index
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>QueryPlanEqualityComparer.GroupingSetsEqual</c> on the plan key. SQLite
    /// renders the ANSI grouping-sets clause, so both commands prepare; the live counterpart of the same
    /// grouping family is the plain <c>GroupBy</c> (recorded, not fabricated: the grouping-set rows are not
    /// asserted on this dialect).
    /// </summary>
    [Fact]
    public void GroupingSetArms_PlanKeySetList_ShouldCompareCountArityAndIndex()
    {
        ResetPlanCache();

        QueryCommand<int> Counts(params int[][] sets) => Context.From<Pd4Marker>()
            .GroupByGroupingSets(x => new { x.Grp, x.Tag }, sets)
            .Select(g => SqlFunctions.Sql.count());

        var oneSet = Counts([0]);
        var oneSetAgain = Counts([0]);
        var otherIndex = Counts([1]);
        var bothIndices = Counts([0, 1]);
        var twoSets = Counts([0], [1]);

        Prepare(oneSet);
        Prepare(oneSetAgain);

        var planComparer = oneSet.GetQueryPlanEqualityComparer();

        planComparer.Equals(oneSet, oneSetAgain).Should().BeTrue("an identical grouping-set list is the same plan");
        planComparer.GetHashCode(oneSet).Should().Be(planComparer.GetHashCode(oneSetAgain));
        Prepare(oneSetAgain).Should().BeSameAs(Prepare(oneSet), "the equal set list reuses one plan");

        planComparer.Equals(oneSet, otherIndex).Should().BeFalse("a different set member index is a different plan");
        planComparer.Equals(oneSet, bothIndices).Should().BeFalse("a set of a different arity is a different plan");
        planComparer.Equals(oneSet, twoSets).Should().BeFalse("a different number of sets is a different plan");
        planComparer.GetHashCode(oneSet).Should().NotBe(planComparer.GetHashCode(twoSets));

        var grouped = Run(Context.From<Pd4Marker>()
            .GroupBy(x => x.Grp)
            .Select(g => new { g.Grp, Count = SqlFunctions.Sql.count() }));
        grouped.Should().HaveCount(2);
        grouped.Single(r => r.Grp == 1).Count.Should().Be(2);
        grouped.Single(r => r.Grp == 2).Count.Should().Be(2);
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Plan-key window frames: the six-axis frame cluster
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the plan-key <c>WindowsEqual</c> FRAME cluster (unit, start kind, start
    /// offset, end kind, end offset, exclusion) - the neighbour covers the same arms on the raw-shape side
    /// only, and the statement window frame cluster of this comparer is 0-hit in the pinned baseline.
    /// </summary>
    [Fact]
    public void WindowFrameArms_PlanKeyFrameAxes_ShouldCompareAndReadRowNumbers()
    {
        ResetPlanCache();

        QueryCommand<int> Framed(WindowFrame frame)
        {
            var builder = Context.From<Pd4Marker>();
            return builder
                .Window("pd4_w", partitionBy: [x => x.Grp], orderBy: [builder.Asc(x => x.Id)], frame: frame)
                .OrderBy(x => x.Id)
                .Select(x => SqlFunctions.Sql.row_number().Over("pd4_w"));
        }

        var unbounded = Framed(WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow));
        var unboundedAgain = Framed(WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow));
        var otherUnit = Framed(WindowFrame.Groups(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow));
        var otherStartKind = Framed(WindowFrame.Rows(WindowFrameBound.Preceding(1), WindowFrameBound.CurrentRow));
        var otherStartOffset = Framed(WindowFrame.Rows(WindowFrameBound.Preceding(2), WindowFrameBound.CurrentRow));
        var otherEndKind = Framed(WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.Following(1)));
        var otherEndOffset = Framed(WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.Following(2)));
        var excluded = Framed(WindowFrame
            .Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)
            .WithExclusion(WindowFrameExclusion.NoOthers));

        Rows(Prepare(unbounded)).Should().Equal(new[] { 1, 2, 1, 2 }, "a row number per Grp partition");

        var planComparer = unbounded.GetQueryPlanEqualityComparer();

        Prepare(unboundedAgain);
        planComparer.Equals(unbounded, unboundedAgain).Should().BeTrue("an identical frame is the same plan");
        planComparer.GetHashCode(unbounded).Should().Be(planComparer.GetHashCode(unboundedAgain));

        planComparer.Equals(unbounded, otherUnit).Should().BeFalse("a different frame UNIT is a different plan");
        planComparer.Equals(unbounded, otherStartKind).Should().BeFalse("a different frame START KIND is a different plan");
        planComparer.Equals(unbounded, otherStartOffset).Should().BeFalse("a different frame START OFFSET is a different plan");
        planComparer.Equals(unbounded, otherEndKind).Should().BeFalse("a different frame END KIND is a different plan");
        planComparer.Equals(unbounded, otherEndOffset).Should().BeFalse("a different frame END OFFSET is a different plan");
        planComparer.Equals(unbounded, excluded).Should().BeFalse("a frame EXCLUSION is part of the plan key");

        Rows(Prepare(otherStartOffset)).Should().Equal(new[] { 1, 2, 1, 2 }, "the changed frame still reads live rows");
    }

    // ---------------------------------------------------------------------------------------------
    // 7. One- and two-level correlated nesting and a correlated Count projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: one-level versus two-level correlated nesting through the expression comparer
    /// (the neighbour compares only a single nested <c>List.Any</c> lambda) plus a correlated
    /// <c>Count</c> projection. Live rows are asserted for every shape.
    /// </summary>
    [Fact]
    public void NestedLambdaArms_OneAndTwoLevelCorrelation_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        Expression<Func<Pd4Marker, bool>> oneLevel =
            x => SqlFunctions.Sql.exists(_lines.Where(l => l.MarkerId == x.Id && l.Qty > 2));
        Expression<Func<Pd4Marker, bool>> oneLevelAgain =
            x => SqlFunctions.Sql.exists(_lines.Where(l => l.MarkerId == x.Id && l.Qty > 2));
        Expression<Func<Pd4Marker, bool>> twoLevels =
            x => SqlFunctions.Sql.exists(_lines.Where(l => l.MarkerId == x.Id
                && SqlFunctions.Sql.exists(_lines.Where(l2 => l2.MarkerId == l.MarkerId && l2.Qty > l.Qty))));

        var comparer = Context.From<Pd4Marker>().Select(x => x.Id).GetExpressionPlanEqualityComparer();

        comparer.Equals(oneLevel, oneLevelAgain).Should().BeTrue("two identical one-level nestings are equal");
        comparer.Equals(oneLevel, twoLevels).Should().BeFalse("a two-level nesting is a deeper tree");
        comparer.GetHashCode(oneLevel).Should().Be(comparer.GetHashCode(oneLevelAgain));
        comparer.GetHashCode(oneLevel).Should().NotBe(comparer.GetHashCode(twoLevels));

        Run(Context.From<Pd4Marker>().Where(oneLevel).OrderBy(x => x.Id).Select(x => x.Id)).Should().Equal(1, 2, 3);
        Run(Context.From<Pd4Marker>().Where(twoLevels).OrderBy(x => x.Id).Select(x => x.Id)).Should()
            .Equal(new[] { 1 }, "only marker 1 has a line with a higher-qty sibling line");

        var counts = Run(Context.From<Pd4Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Count = _lines.Where(l => l.MarkerId == x.Id).Count() }));
        counts.Select(r => r.Count).Should().Equal(new[] { 2, 1, 1, 0 }, "a correlated count per marker row");

        var oneLevelCommand = Context.From<Pd4Marker>().Where(oneLevel).Select(x => x.Id);
        var twoLevelsCommand = Context.From<Pd4Marker>().Where(twoLevels).Select(x => x.Id);
        Prepare(oneLevelCommand);
        Prepare(twoLevelsCommand);
        oneLevelCommand.GetQueryPlanEqualityComparer().Equals(oneLevelCommand, twoLevelsCommand).Should()
            .BeFalse("the nesting depth is part of the plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Fresh parameter instances across a re-executed command on one context
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a predicate hand-built with FRESH <c>ParameterExpression</c>/constant nodes on
    /// every call (the neighbour compares two lambdas of the same closure site). Structural equality maps
    /// the fresh parameters, so the plan is reused; a changed literal is a different plan. The same
    /// command re-prepared on the same context keeps one holder.
    /// </summary>
    [Fact]
    public void ParameterIdentityArms_FreshNodesAndReexecution_ShouldReuseAndSeparate()
    {
        ResetPlanCache();

        static Expression<Func<Pd4Marker, bool>> BuildPredicate(int floor)
        {
            var parameter = Expression.Parameter(typeof(Pd4Marker), "x");
            var body = Expression.GreaterThan(
                Expression.Property(parameter, nameof(Pd4Marker.Id)),
                Expression.Constant(floor));
            return Expression.Lambda<Func<Pd4Marker, bool>>(body, parameter);
        }

        var comparer = Context.From<Pd4Marker>().Select(x => x.Id).GetExpressionPlanEqualityComparer();
        var fresh = BuildPredicate(1);
        var freshAgain = BuildPredicate(1);
        var changed = BuildPredicate(2);

        comparer.Equals(fresh, freshAgain).Should()
            .BeTrue("fresh parameter instances are mapped by the parameter scope, not by identity");
        comparer.Equals(fresh, changed).Should().BeFalse("a changed literal is a different expression");
        comparer.GetHashCode(fresh).Should().Be(comparer.GetHashCode(freshAgain));

        QueryCommand<int> By(int floor) => Context.From<Pd4Marker>()
            .Where(BuildPredicate(floor))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var first = By(1);
        var firstPrepared = Prepare(first);
        Rows(firstPrepared).Should().Equal(2, 3, 4);
        Prepare(first).Should().BeSameAs(firstPrepared, "re-preparing the same command on the context keeps one plan");

        var second = By(1);
        Prepare(second).Should().BeSameAs(firstPrepared, "a freshly built identical predicate reuses the plan");

        var changedPrepared = Prepare(By(2));
        changedPrepared.Should().NotBeSameAs(firstPrepared, "a changed literal is a different plan");
        Rows(changedPrepared).Should().Equal(3, 4);
    }

    // ---------------------------------------------------------------------------------------------
    // 9. GetHashCode equality/inequality paired with Equals, and the null arms of both comparers
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a shape matrix that pairs <c>Equals</c> with <c>GetHashCode</c> on both
    /// comparers, including the null arm of each hash entry point (the neighbours only hash one or two
    /// shapes). Every pair is executed live where the shape renders.
    /// </summary>
    [Fact]
    public void HashArms_EqualityAndNullEntries_ShouldStayConsistent()
    {
        ResetPlanCache();

        Expression<Func<Pd4Marker, bool>> byQty = x => x.Qty >= 2;
        Expression<Func<Pd4Marker, bool>> byQtyAgain = x => x.Qty >= 2;
        Expression<Func<Pd4Marker, bool>> byQtyChanged = x => x.Qty >= 3;

        var command = Context.From<Pd4Marker>().Where(byQty).Select(x => x.Id);
        var expressionComparer = command.GetExpressionPlanEqualityComparer();

        expressionComparer.Equals(byQty, byQtyAgain).Should().BeTrue();
        expressionComparer.GetHashCode(byQty).Should().Be(expressionComparer.GetHashCode(byQtyAgain));
        expressionComparer.Equals(byQty, byQtyChanged).Should().BeFalse();
        expressionComparer.GetHashCode(byQty).Should().NotBe(expressionComparer.GetHashCode(byQtyChanged));
        expressionComparer.GetHashCode(null!).Should().Be(0, "the null expression hashes to zero");

        Run(command).Should().Equal(2, 3, 4);

        var same = Context.From<Pd4Marker>().Where(x => x.Qty >= 2).Select(x => x.Id);
        var other = Context.From<Pd4Marker>().Where(x => x.Qty >= 3).Select(x => x.Id);
        Prepare(command);
        Prepare(same);
        Prepare(other);

        var planComparer = command.GetQueryPlanEqualityComparer();
        planComparer.Equals(command, same).Should().BeTrue("identical predicates are one plan");
        planComparer.GetHashCode(command).Should().Be(planComparer.GetHashCode(same));
        planComparer.Equals(command, other).Should().BeFalse("a changed predicate is a different plan");
        planComparer.GetHashCode(command).Should().NotBe(planComparer.GetHashCode(other));
        planComparer.GetHashCode(null).Should().Be(0, "the null command hashes to zero");

        Rows(Prepare(same)).Should().Equal(2, 3, 4);
        Rows(Prepare(other)).Should().Equal(3, 4);
    }

    // ---------------------------------------------------------------------------------------------
    // 10. Plan-key clause fields: grouping totals, LIMIT BY, DISTINCT ON and the extreme-row clause
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Arms B (clause fields): <c>EqualsCore</c> <c>GroupByWithTotals</c> :76, <c>LimitBy</c> null
    /// presence :82 and expression :86, <c>DistinctOn</c> null presence :93 and <c>ExtremeRow</c> null
    /// presence :102. Every pair is built on this ONE context and prepared in place; the plain grouping
    /// shape behind the fact still executes live. The provider-specific clauses are not rendered by the
    /// SQLite dialect (the comparer is provider independent), so only their plan keys are asserted.
    /// </summary>
    [Fact]
    public void ClauseFields_GroupTotalsLimitByDistinctOnAndExtremeRow_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<int> PlainIds() => Context.From<Pd4Marker>().OrderBy(x => x.Id).Select(x => x.Id);
        QueryCommand<int> PlainCounts() => Context.From<Pd4Marker>()
            .GroupBy(x => new { x.Grp })
            .Select(g => SqlFunctions.Sql.count());

        var plainIds = PlainIds();
        var plainCounts = PlainCounts();
        var totals = Context.From<Pd4Marker>()
            .GroupBy(x => new { x.Grp })
            .WithTotals()
            .Select(g => SqlFunctions.Sql.count());

        Resolve(plainCounts);
        Resolve(totals);

        var comparer = plainCounts.GetQueryPlanEqualityComparer();
        comparer.Equals(plainCounts, totals).Should().BeFalse("GROUP BY ... WITH TOTALS is part of the plan key");
        comparer.GetHashCode(plainCounts).Should().NotBe(comparer.GetHashCode(totals));
        Rows(Prepare(plainCounts)).Should().Equal(new[] { 2, 2 }, "the plain grouping shape executes live");

        var limitByGroup = Context.From<Pd4Marker>().LimitBy(2, x => x.Grp).OrderBy(x => x.Id).Select(x => x.Id);
        var limitByTag = Context.From<Pd4Marker>().LimitBy(2, x => x.Tag).OrderBy(x => x.Id).Select(x => x.Id);

        Resolve(plainIds);
        Resolve(limitByGroup);
        Resolve(limitByTag);

        comparer.Equals(plainIds, limitByGroup).Should().BeFalse(
            "a LIMIT BY clause present on one side only is a different plan key");
        comparer.Equals(limitByGroup, limitByTag).Should().BeFalse(
            "the same LIMIT BY bounds with a different expression are a different plan key");
        comparer.GetHashCode(limitByGroup).Should().NotBe(comparer.GetHashCode(limitByTag));
        Rows(Prepare(plainIds)).Should().Equal(new[] { 1, 2, 3, 4 });

        var distinctOn = Context.From<Pd4Marker>().DistinctOn(x => x.Grp).OrderBy(x => x.Id).Select(x => x.Id);
        Resolve(distinctOn);

        comparer.Equals(plainIds, distinctOn).Should().BeFalse(
            "a DISTINCT ON clause present on one side only is a different plan key");

        var extremeRow = Context.From<Pd4Marker>().SelectWhereMin<int?, long>(x => x.Score, x => x.Id);
        Resolve(extremeRow);

        comparer.Equals(plainIds, extremeRow).Should().BeFalse(
            "an ExtremeRow (SelectWhereMin) clause present on one side only is a different plan key");
        comparer.GetHashCode(plainIds).Should().NotBe(comparer.GetHashCode(extremeRow));
    }

    // ---------------------------------------------------------------------------------------------
    // 11. Plan-key source modifiers and flags: TABLESAMPLE, FOR SYSTEM TIME, FINAL, DocumentMode
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Arms B (clause fields): <c>EqualsCore</c> <c>TableSample</c> :117, <c>Temporal</c> :119,
    /// <c>Final</c> :123 and <c>DocumentMode</c> :159. Equal clauses must compare TRUE and hash equally;
    /// a clause present on one side only must compare FALSE. All shapes are prepared in place on this
    /// context and the plain baseline executes live.
    /// </summary>
    [Fact]
    public void SourceModifiersAndDocumentFlag_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<int> Plain() => Context.From<Pd4Marker>().OrderBy(x => x.Id).Select(x => x.Id);

        var plain = Plain();
        var sampleTen = Context.From<Pd4Marker>(o => o.TableSample(10)).OrderBy(x => x.Id).Select(x => x.Id);
        var sampleTenAgain = Context.From<Pd4Marker>(o => o.TableSample(10)).OrderBy(x => x.Id).Select(x => x.Id);
        var sampleTwenty = Context.From<Pd4Marker>(o => o.TableSample(20)).OrderBy(x => x.Id).Select(x => x.Id);
        var temporal = Context.From<Pd4Marker>()
            .ForSystemTime(TemporalClause.AsOf(new DateTime(2020, 1, 1)))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        var temporalAgain = Context.From<Pd4Marker>()
            .ForSystemTime(TemporalClause.AsOf(new DateTime(2020, 1, 1)))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        var final = Context.From<Pd4Marker>().Final().OrderBy(x => x.Id).Select(x => x.Id);
        var finalAgain = Context.From<Pd4Marker>().Final().OrderBy(x => x.Id).Select(x => x.Id);
        var document = Plain();
        document.DocumentMode = true;

        Resolve(plain);
        Resolve(sampleTen);
        Resolve(sampleTenAgain);
        Resolve(sampleTwenty);
        Resolve(temporal);
        Resolve(temporalAgain);
        Resolve(final);
        Resolve(finalAgain);
        Resolve(document);

        var comparer = plain.GetQueryPlanEqualityComparer();

        comparer.Equals(plain, sampleTen).Should().BeFalse(
            "a TABLESAMPLE clause present on one side only is a different plan key");
        comparer.Equals(sampleTen, sampleTenAgain).Should().BeTrue("an identical TABLESAMPLE clause is one plan");
        comparer.GetHashCode(sampleTen).Should().Be(comparer.GetHashCode(sampleTenAgain));
        comparer.Equals(sampleTen, sampleTwenty).Should().BeFalse(
            "a different TABLESAMPLE percentage is a different plan key");

        comparer.Equals(plain, temporal).Should().BeFalse(
            "a FOR SYSTEM TIME clause present on one side only is a different plan key");
        comparer.Equals(temporal, temporalAgain).Should().BeTrue("an identical temporal clause is one plan");

        comparer.Equals(plain, final).Should().BeFalse(
            "the ClickHouse FINAL modifier present on one side only is a different plan key");
        comparer.Equals(final, finalAgain).Should().BeTrue("the same FINAL modifier is one plan");
        comparer.GetHashCode(final).Should().Be(comparer.GetHashCode(finalAgain));

        comparer.Equals(plain, document).Should().BeFalse(
            "the document terminal flag is part of the plan key even when the SQL is identical");
        comparer.GetHashCode(plain).Should().NotBe(comparer.GetHashCode(document));

        Rows(Prepare(Plain())).Should().Equal(new[] { 1, 2, 3, 4 },
            "the plain baseline behind the flag facts executes live");
    }

    // ---------------------------------------------------------------------------------------------
    // 12. Plan-key per-parent paging and first/default terminal flags
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Arms B (clause fields): <c>EqualsCore</c> <c>PerParentPaging</c> :166,
    /// <c>PerParentPagingEmbedded</c> :170 and <c>DefaultOnEmpty</c> :175. The per-parent child commands
    /// carry a top-N page and a shared partition selector, so only the paging/embedding flags differ;
    /// each pair is prepared in place on this context and the plain child executes live.
    /// </summary>
    [Fact]
    public void PerParentPagingAndDefaultOnEmpty_ShouldKeySeparately()
    {
        ResetPlanCache();

        Expression<Func<Pd4Line, int>> partition = l => l.MarkerId;

        QueryCommand<int> Child(bool perParent, bool embedded)
        {
            var command = Context.From<Pd4Line>().OrderBy(x => x.Id).Limit(1).Select(x => x.Id);
            if (!perParent)
                return command;

            command.PerParentPaging = true;
            command.PerParentPartition = partition;
            command.PerParentPagingEmbedded = embedded;
            return command;
        }

        var plainChild = Child(perParent: false, embedded: false);
        var splitChild = Child(perParent: true, embedded: false);
        var embeddedChild = Child(perParent: true, embedded: true);

        Resolve(plainChild);
        Resolve(splitChild);
        Resolve(embeddedChild);

        var comparer = plainChild.GetQueryPlanEqualityComparer();

        comparer.Equals(plainChild, splitChild).Should().BeFalse(
            "a per-parent paging child present on one side only is a different plan key");
        comparer.Equals(splitChild, embeddedChild).Should().BeFalse(
            "an embedded ranked child and a split child render different SQL and are different plan keys");

        var defaultOnEmpty = Context.From<Pd4Line>().OrderBy(x => x.Id).Select(x => x.Id);
        var strict = Context.From<Pd4Line>().OrderBy(x => x.Id).Select(x => x.Id);
        defaultOnEmpty.DefaultOnEmpty = true;
        Resolve(defaultOnEmpty);
        Resolve(strict);

        comparer.Equals(strict, defaultOnEmpty).Should().BeFalse(
            "FirstOrDefault and First share SQL and paging, so the DefaultOnEmpty flag must key separately");
        comparer.GetHashCode(strict).Should().NotBe(comparer.GetHashCode(defaultOnEmpty));

        Rows(Prepare(strict)).Should().Equal(new[] { 10, 11, 12, 13 },
            "the strict scalar baseline executes live");
    }

    // ---------------------------------------------------------------------------------------------
    // 13. Plan-key grouping sets: a declaration list present on one side only
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Arm B (list helper): <c>GroupingSetsEqual</c> :318 (a set list present on one side only). Both
    /// commands carry the same <c>GroupingType</c>, so the comparison reaches the list helper; the
    /// no-set declaration is prepared in place (SQLite rejects the rendered form) while the plain
    /// grouping baseline executes live.
    /// </summary>
    [Fact]
    public void GroupingSetNullPresence_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<int> WithoutSets() => Context.From<Pd4Marker>()
            .GroupByGroupingSets(x => new { x.Grp, x.Tag })
            .Select(g => SqlFunctions.Sql.count());
        QueryCommand<int> WithSets() => Context.From<Pd4Marker>()
            .GroupByGroupingSets(x => new { x.Grp, x.Tag }, [0])
            .Select(g => SqlFunctions.Sql.count());

        var withoutSets = WithoutSets();
        var withSets = WithSets();
        Resolve(withoutSets);
        Resolve(withSets);

        var comparer = withoutSets.GetQueryPlanEqualityComparer();
        comparer.Equals(withoutSets, withSets).Should().BeFalse(
            "a grouping-set list present on one side only is a different plan key");
        comparer.GetHashCode(withoutSets).Should().NotBe(comparer.GetHashCode(withSets));

        Rows(Prepare(Context.From<Pd4Marker>()
                .GroupBy(x => new { x.Grp })
                .Select(g => SqlFunctions.Sql.count()))).Should()
            .Equal(new[] { 2, 2 }, "the plain grouping baseline executes live");
    }

    /// <summary>Marker row of the class-owned <c>pd4_marker</c> table.</summary>
    [SqlTable("pd4_marker")]
    public sealed class Pd4Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>The text column used by the char/string and string-predicate arms.</summary>
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("qty")]
        public int Qty { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("tag")]
        public int Tag { get; set; }

        /// <summary>The nullable column used by the null-arm fact.</summary>
        [Column("score")]
        public int? Score { get; set; }
    }

    /// <summary>Marker row of the class-owned <c>pd4_line</c> table.</summary>
    [SqlTable("pd4_line")]
    public sealed class Pd4Line
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("marker_id")]
        public int MarkerId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }
}
