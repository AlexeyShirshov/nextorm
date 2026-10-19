using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse source-rendering coverage (cycle 5, cluster C6/D6) executed against a live Testcontainers
/// server. Every fact creates, seeds and drops its own <c>ch5r_marker</c> MergeTree table and asserts
/// the rows and aggregate values the server returns.
///
/// <para>
/// Targeted arms: the <c>FINAL</c> and <c>WITH TOTALS</c> modifiers
/// (<c>ClickHouseEntityBuilderExtensions.Final</c> :15, <c>WithTotals</c> :97), the two public
/// <c>LimitBy</c> overloads (<c>limit, key</c> :77 and <c>limit, offset, key</c> :87) and a
/// multi-key versus single-key <c>SETTINGS</c> list (:23) resolved live.
/// </para>
///
/// <para>
/// Overlap avoided: <c>ClickHousePlanKeyBranchCoverage5Tests</c> compares the plan keys of these forms;
/// <c>ClickHousePreparationDifferentialBranchCoverage4Tests</c> drives the raw descriptor; this class
/// executes the rendering variants and asserts the concrete rows/aggregates. The <c>SAMPLE</c> form on
/// a Memory table stays deferred (server Code 141), so this class uses a MergeTree-family table.
/// </para>
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseRenderingBranchCoverage5Tests : ProviderTestSuite
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
        Execute("drop table if exists ch5r_marker");
        Execute("create table ch5r_marker (id Int32, grp Int32, num Int32, label String) "
                + "engine = ReplacingMergeTree order by id");
        Execute("insert into ch5r_marker (id, grp, num, label) values "
                + "(1, 1, 10, 'a'), (2, 1, 20, 'b'), (3, 2, 30, 'c'), (4, 2, 40, 'd')");
    }

    private void DropMarker() => Execute("drop table if exists ch5r_marker");

    /// <summary>
    /// Distinguishing arm: the <c>FINAL</c> modifier on a MergeTree table (every row still visible)
    /// and the <c>WITH TOTALS</c> modifier over a grouped read (the driver does not surface the extra
    /// totals block, so the two group aggregates are asserted).
    /// </summary>
    [Fact]
    public void FinalAndWithTotals_ShouldExecuteAndReturnTheExpectedGroups()
    {
        CreateMarker();
        try
        {
            var final = Context.From<Ch5rMarker>()
                .Final()
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();

            final.Should().Equal(new[] { 1, 2, 3, 4 }, "FINAL does not change the visible rows of a seeded table");

            var plain = Context.From<Ch5rMarker>()
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();

            plain.Should().Equal(final);

            var totals = Context.From<Ch5rMarker>()
                .GroupBy(x => new { x.Grp })
                .WithTotals()
                .Select(g => new { g.Grp, Count = SqlFunctions.Sql.count() })
                .OrderBy(1, OrderDirection.Asc)
                .ToList();

            totals.Should().HaveCount(2, "the totals block is a separate response block the driver does not surface");
            totals.Select(r => r.Grp).Should().Equal(1, 2);
            totals.Select(r => r.Count).Should().Equal(new[] { 2, 2 });
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: both public <c>LimitBy</c> overloads. The offset overload skips the first
    /// row of every group; the plain overload keeps it.
    /// </summary>
    [Fact]
    public void LimitBy_BothOverloads_ShouldKeepOneRowPerGroupAtTheRequestedOffset()
    {
        CreateMarker();
        try
        {
            var first = Context.From<Ch5rMarker>()
                .OrderBy(x => x.Id)
                .LimitBy(1, x => x.Grp)
                .Select(x => x.Id)
                .ToList();

            first.Should().Equal(new[] { 1, 3 }, "one row per group with no offset");

            var offset = Context.From<Ch5rMarker>()
                .OrderBy(x => x.Id)
                .LimitBy(1, 1, x => x.Grp)
                .Select(x => x.Id)
                .ToList();

            offset.Should().Equal(new[] { 2, 4 }, "the offset overload skips the first row of every group");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: a multi-key <c>SETTINGS</c> list versus a single-key list. Both render and
    /// execute, and the aggregate value is unchanged.
    /// </summary>
    [Fact]
    public void Settings_MultipleAndSingle_ShouldExecuteWithTheSameAggregate()
    {
        CreateMarker();
        try
        {
            var multi = Context.From<Ch5rMarker>()
                .Settings(("max_threads", "1"), ("max_block_size", "1000"))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();

            multi.Should().Equal(new[] { 1, 2, 3, 4 }, "settings are execution hints, not row filters");

            var single = Context.From<Ch5rMarker>()
                .Settings(("max_threads", "1"))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();

            single.Should().Equal(multi);

            var multiCount = Context.From<Ch5rMarker>()
                .Settings(("max_threads", "1"), ("max_block_size", "1000"))
                .Select(x => SqlFunctions.Sql.count())
                .ToList()
                .Single();

            var singleCount = Context.From<Ch5rMarker>()
                .Settings(("max_threads", "1"))
                .Select(x => SqlFunctions.Sql.count())
                .ToList()
                .Single();

            multiCount.Should().Be(4);
            singleCount.Should().Be(multiCount, "a setting never changes the aggregate result");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// The <c>LIMIT BY</c> plan identity: the limit, the offset and the key expression are compared,
    /// and the extreme-row compatibility guard rejects the combination before execution.
    /// </summary>
    [Fact]
    public void LimitBy_PlanComparisonAndExtremeComposition_ShouldDistinguishAndReject()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> First() => Context.From<Ch5rMarker>()
                .OrderBy(x => x.Id)
                .LimitBy(1, x => x.Grp)
                .Select(x => x.Id);
            QueryCommand<int> Offset() => Context.From<Ch5rMarker>()
                .OrderBy(x => x.Id)
                .LimitBy(1, 1, x => x.Grp)
                .Select(x => x.Id);
            QueryCommand<int> OtherKey() => Context.From<Ch5rMarker>()
                .OrderBy(x => x.Id)
                .LimitBy(1, x => x.Num)
                .Select(x => x.Id);

            var first = First();
            var firstAgain = First();
            var offset = Offset();
            var otherKey = OtherKey();

            first.PrepareCommand(dontCalculateHash: false, CancellationToken.None);
            firstAgain.PrepareCommand(dontCalculateHash: false, CancellationToken.None);
            offset.PrepareCommand(dontCalculateHash: false, CancellationToken.None);
            otherKey.PrepareCommand(dontCalculateHash: false, CancellationToken.None);

            var comparer = first.GetQueryPlanEqualityComparer();
            comparer.Equals(first, firstAgain).Should().BeTrue("identical LIMIT BY clauses are the same plan");
            comparer.Equals(first, offset).Should().BeFalse("the offset is part of the plan identity");
            comparer.Equals(first, otherKey).Should().BeFalse("the key expression is part of the plan identity");

            var extreme = () => Context.From<Ch5rMarker>()
                .LimitBy(1, x => x.Grp)
                .SelectWhereMax(x => x.Num)
                .Select(x => x.Id)
                .ToList();
            extreme.Should().Throw<BuildSqlCommandException>()
                .WithMessage("*SelectWhereMax/SelectWhereMin cannot be combined with LIMIT BY*");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// A temporal clause on ClickHouse is rejected by the dialect guard, and the base document
    /// renderers return the documented empty string for a provider without FOR JSON/FOR XML.
    /// </summary>
    [Fact]
    public void UnsupportedTemporalAndDocumentForms_ShouldUseTheBaseGuards()
    {
        CreateMarker();
        try
        {
            var temporal = () => Context.From<Ch5rMarker>()
                .ForSystemTime(TemporalClause.All())
                .Select(x => x.Id)
                .ToList();
            temporal.Should().Throw<NotSupportedException>()
                .WithMessage("*The FOR SYSTEM_TIME clause is not supported by this SQL dialect*");

            var dialect = new ClickHouseDialect();
            dialect.MakeForJson(new ForJsonClause(ForJsonMode.Path), KeywordCase.Lower).Should().BeEmpty();
            dialect.MakeForXml(new ForXmlClause(ForXmlMode.Path), KeywordCase.Lower).Should().BeEmpty();
        }
        finally
        {
            DropMarker();
        }
    }

    [SqlTable("ch5r_marker")]
    public sealed class Ch5rMarker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("num")]
        public int Num { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }
}
