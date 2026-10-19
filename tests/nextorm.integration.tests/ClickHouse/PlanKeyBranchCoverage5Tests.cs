using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse plan-key branch coverage (cycle 5, cluster C1/D1) executed against a live Testcontainers
/// server. Every fact builds two DISTINCT commands over class-owned <c>ch5_marker</c> rows, prepares
/// both in place (<c>PrepareCommand(dontCalculateHash: false)</c>) so the plan key reads the resolved
/// state, asserts the two key instances are distinct, compares them with the public
/// <see cref="QueryCommand.GetQueryPlanEqualityComparer"/> and executes both sides live.
///
/// <para>
/// Targeted components of <c>QueryPlanEqualityComparer.EqualsCore</c>
/// (<c>src/nextorm.core/Query/QueryPlanEqualityComparer.cs:66-232</c>): <c>Settings</c> through
/// <c>KeyValueListsEqual</c> :286 (equal list :288, key :294, value :295), <c>LimitBy</c> :78-87,
/// <c>ArrayJoinKind</c> :133 and the <c>SampleRatio</c>/<c>SampleOffset</c> pair :125.
/// </para>
///
/// <para>
/// Overlap avoided: <c>ClickHousePreparationDifferentialBranchCoverage4Tests</c> drives the RAW-SHAPE
/// descriptor (<c>RawShape.Matches</c>) for the same forms; this class drives the plan key and the plan
/// comparer. The live forms themselves are already proven by the cycle-4 neighbours
/// (<c>ClickHouseIntegrationTests.ArrayJoinClause_*</c>, <c>LimitBy_ShouldKeepOneRowPerGroup</c>,
/// <c>ClickHouseJoinWholeEntityIntegrationTests</c>).
/// </para>
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHousePlanKeyBranchCoverage5Tests : ProviderTestSuite
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
        Execute("drop table if exists ch5_marker");
        Execute("create table ch5_marker (id Int32, num Int32, grp Nullable(Int32), label Nullable(String), "
                + "nums Array(Int32), other Array(Int32)) engine = Memory");
        Execute("insert into ch5_marker (id, num, grp, label, nums, other) values "
                + "(1, 10, 1, 'one', [1, 2, 3], [9]), "
                + "(2, 20, 1, 'two', [4], [8, 7]), "
                + "(3, 30, 2, 'three', [], [])");
    }

    private void DropMarker() => Execute("drop table if exists ch5_marker");

    private void CreateSample()
    {
        Execute("drop table if exists ch5_sample");
        Execute("create table ch5_sample (id Int32, sid UInt32, grp Nullable(Int32)) "
                + "engine = MergeTree order by (sid, id) sample by sid");
        Execute("insert into ch5_sample (id, sid, grp) values (1, 1, 1), (2, 2, 1), (3, 3, 2)");
    }

    private void DropSample() => Execute("drop table if exists ch5_sample");

    /// <summary>Resolves the prepared state (hashes, prepared condition, resolved overrides) without the plan store.</summary>
    private static void Resolve(QueryCommand command)
        => command.PrepareCommand(dontCalculateHash: false, CancellationToken.None);

    private List<TResult> Run<TResult>(QueryCommand<TResult> command)
        => Context.ToList(((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None));

    private void AssertSamePlan<TResult>(
        QueryCommand<TResult> first,
        QueryCommand<TResult> second,
        IEnumerable<TResult> expectedRows,
        string because)
    {
        Resolve(first);
        Resolve(second);

        first.GetOrCreatePlanKey(null).Should().NotBeSameAs(second.GetOrCreatePlanKey(null),
            "each freshly built command owns its own plan-key instance");
        first.Should().NotBeSameAs(second, "the comparison below is structural, not a reference match");

        first.GetQueryPlanEqualityComparer().Equals(first, second).Should().BeTrue(because);

        Run(first).Should().Equal(expectedRows, because);
        Run(second).Should().Equal(expectedRows, "an equal plan reads the same live rows");
    }

    private void AssertDifferentPlans<TResult>(
        QueryCommand<TResult> first,
        QueryCommand<TResult> second,
        IEnumerable<TResult> firstRows,
        IEnumerable<TResult> secondRows,
        string because)
    {
        Resolve(first);
        Resolve(second);

        first.GetOrCreatePlanKey(null).Should().NotBeSameAs(second.GetOrCreatePlanKey(null),
            "each freshly built command owns its own plan-key instance");
        first.Should().NotBeSameAs(second, "the comparison below is structural, not a reference match");

        first.GetQueryPlanEqualityComparer().Equals(first, second).Should().BeFalse(because);

        Run(first).Should().Equal(firstRows, because);
        Run(second).Should().Equal(secondRows, because);
    }

    /// <summary>The SETTINGS key/value list: same list TRUE, changed value FALSE, rows unchanged.</summary>
    [Fact]
    public void Settings_SameAndChangedValue_ShouldKeySeparately()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> Setting(string value) => Context.From<Ch5Marker>()
                .Settings(("join_use_nulls", value))
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            AssertSamePlan(
                Setting("1"),
                Setting("1"),
                new[] { 1, 2, 3 },
                "an identical settings list is the same plan key");

            AssertDifferentPlans(
                Setting("1"),
                Setting("0"),
                new[] { 1, 2, 3 },
                new[] { 1, 2, 3 },
                "a changed settings VALUE is a different plan key and never changes the rows");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The LIMIT BY limit/offset pair: same clause TRUE, changed limit or offset FALSE, rows follow.</summary>
    [Fact]
    public void LimitBy_LimitAndOffset_ShouldKeySeparately()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> Limited(int limit, int offset) => Context.From<Ch5Marker>()
                .LimitBy(limit, offset, x => x.Grp)
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            AssertSamePlan(
                Limited(1, 0),
                Limited(1, 0),
                new[] { 1, 3 },
                "two structurally identical LIMIT BY clauses are the same plan key");

            AssertDifferentPlans(
                Limited(1, 0),
                Limited(2, 0),
                new[] { 1, 3 },
                new[] { 1, 2, 3 },
                "a different LIMIT BY limit is a different plan key");

            AssertDifferentPlans(
                Limited(1, 0),
                Limited(1, 1),
                new[] { 1, 3 },
                new[] { 2 },
                "a different LIMIT BY offset is a different plan key");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The ARRAY JOIN kind pair: INNER vs LEFT is a different key and keeps the empty-array row.</summary>
    [Fact]
    public void ArrayJoin_KindPair_ShouldKeySeparately()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> Inner() => Context.From<Ch5Marker>()
                .ArrayJoin(x => x.Nums)
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            AssertSamePlan(
                Inner(),
                Inner(),
                new[] { 1, 1, 1, 2 },
                "two identical ARRAY JOIN commands are the same plan key");

            QueryCommand<int> Left() => Context.From<Ch5Marker>()
                .LeftArrayJoin(x => x.Nums)
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            AssertDifferentPlans(
                Inner(),
                Left(),
                new[] { 1, 1, 1, 2 },
                new[] { 1, 1, 1, 2, 3 },
                "a different array-join kind is a different plan key");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The SAMPLE ratio pair: same ratio TRUE, a different ratio FALSE; 1.0 keeps every row.</summary>
    [Fact]
    public void Sample_RatioPair_ShouldKeySeparately()
    {
        CreateSample();
        try
        {
            QueryCommand<int> Full() => Context.From<Ch5Sample>(o => o.Sample(1.0))
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            AssertSamePlan(
                Full(),
                Full(),
                new[] { 1, 2, 3 },
                "two identical SAMPLE ratios are the same plan key");

            QueryCommand<int> Half() => Context.From<Ch5Sample>(o => o.Sample(0.5))
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            var full = Full();
            var half = Half();
            Resolve(full);
            Resolve(half);

            full.GetOrCreatePlanKey(null).Should().NotBeSameAs(half.GetOrCreatePlanKey(null),
                "each freshly built command owns its own plan-key instance");
            full.GetQueryPlanEqualityComparer().Equals(full, half).Should()
                .BeFalse("a different SAMPLE ratio is a different plan key");

            Run(full).Should().Equal(new[] { 1, 2, 3 }, "a ratio of 1.0 keeps every row");
            Run(half).Should().BeSubsetOf(new[] { 1, 2, 3 }, "a half sample can only return rows of the table");
        }
        finally
        {
            DropSample();
        }
    }

    /// <summary>
    /// Distinguishing arm: the second half of the <c>SampleRatio != y.SampleRatio || x.SampleOffset !=
    /// y.SampleOffset</c> pair :125 - a SAMPLE ratio together with an OFFSET - and the present/absent
    /// form against the same query without any SAMPLE clause. The offset side needs a sub-unit ratio:
    /// ClickHouse rejects an OFFSET together with a full sample (Code 69, "Sampling offset is incorrect
    /// because no sampling", kept in <c>rv1/results/da2-clickhouse-plan-run1.log</c>), so the offset
    /// side asserts live subset membership while the full-ratio side still keeps every row.
    /// </summary>
    [Fact]
    public void Sample_OffsetAndPresentAbsent_ShouldKeySeparately()
    {
        CreateSample();
        try
        {
            QueryCommand<int> Control() => Context.From<Ch5Sample>()
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            QueryCommand<int> NoOffset() => Context.From<Ch5Sample>(o => o.Sample(1.0))
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            QueryCommand<int> WithOffset() => Context.From<Ch5Sample>(o => o.Sample(0.5, 0.1))
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            AssertSamePlan(
                NoOffset(),
                NoOffset(),
                new[] { 1, 2, 3 },
                "two identical SAMPLE ratios with the default offset are the same plan key");

            var noOffset = NoOffset();
            var withOffset = WithOffset();
            Resolve(noOffset);
            Resolve(withOffset);

            noOffset.GetOrCreatePlanKey(null).Should().NotBeSameAs(withOffset.GetOrCreatePlanKey(null),
                "each freshly built command owns its own plan-key instance");
            noOffset.GetQueryPlanEqualityComparer().Equals(noOffset, withOffset).Should()
                .BeFalse("a different SAMPLE offset is a different plan key");

            Run(noOffset).Should().Equal(new[] { 1, 2, 3 }, "a ratio of 1.0 keeps every row");
            Run(withOffset).Should().BeSubsetOf(new[] { 1, 2, 3 },
                "an offset sample can only return rows of the table");

            AssertDifferentPlans(
                NoOffset(),
                Control(),
                new[] { 1, 2, 3 },
                new[] { 1, 2, 3 },
                "a SAMPLE clause present on one side only is a different plan key");
        }
        finally
        {
            DropSample();
        }
    }

    [SqlTable("ch5_sample")]
    public sealed class Ch5Sample
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("sid")]
        public uint Sid { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }
    }

    [SqlTable("ch5_marker")]
    public sealed class Ch5Marker
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
