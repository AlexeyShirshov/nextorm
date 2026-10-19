using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQL Server plan-key branch coverage (cycle 5, cluster C1/D1) executed against a live Testcontainers
/// server. Every fact builds two DISTINCT commands over class-owned <c>ss5_marker</c>/<c>ss5_line</c>
/// rows, prepares both in place (<c>PrepareCommand(dontCalculateHash: false)</c>) so the plan key reads
/// the resolved state, asserts the two key instances are distinct, compares them with the public
/// <see cref="QueryCommand.GetQueryPlanEqualityComparer"/> and executes both sides live (a hint never
/// changes the rows, so both sides assert the same values).
///
/// <para>
/// Targeted components of <c>QueryPlanEqualityComparer.EqualsCore</c>
/// (<c>src/nextorm.core/Query/QueryPlanEqualityComparer.cs:66-232</c>): <c>TableHints</c> and
/// <c>TablesInScopeHints</c> through <c>StringListsEqual</c> :234 (null :237, count :238, element
/// :242), <c>IndexHints</c>/<c>IndexHintKind</c> :145-147 and the per-join table-hint list inside
/// <c>_joinComparer.Equals</c> :192.
/// </para>
///
/// <para>
/// Overlap avoided: <c>SqlServerPreparationDifferentialBranchCoverage4Tests</c> executes the same
/// table-hint form but never compares a plan key; <c>PlanEqualityDifferentialBranchCoverage4Tests</c>
/// owns the index-hint string list on SQLite only.
/// </para>
/// </summary>
[Collection("SqlServer")]
public sealed class SqlServerPlanKeyBranchCoverage5Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

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
        Execute("drop table if exists ss5_line");
        Execute("drop table if exists ss5_marker");
        Execute("create table ss5_marker (id int not null primary key, name nvarchar(50) not null, grp int not null, tag int not null, num int null)");
        Execute("create table ss5_line (id int not null primary key, marker_id int not null, qty int not null)");
        Execute("insert into ss5_marker (id, name, grp, tag, num) values "
                + "(1, 'alpha', 1, 10, 1), (2, 'beta', 1, 20, 1), (3, 'gamma', 2, 10, 2), (4, 'delta', 2, 20, null)");
        Execute("insert into ss5_line (id, marker_id, qty) values (10, 1, 1), (11, 1, 3), (12, 2, 5), (13, 3, 7)");
        Execute("create index ix_ss5_grp on ss5_marker (grp)");
        Execute("create index ix_ss5_tag on ss5_marker (tag)");
    }

    private void DropMarker()
    {
        Execute("drop table if exists ss5_line");
        Execute("drop table if exists ss5_marker");
    }

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

    /// <summary>The primary-source table hint: identical hint TRUE, a different hint FALSE, rows unchanged.</summary>
    [Fact]
    public void TableHint_SameAndDifferentHint_ShouldKeySeparately()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> Hinted(string hint) => Context.From<Ss5Marker>(o => o.WithTableHint(hint))
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            AssertSamePlan(
                Hinted("nolock"),
                Hinted("nolock"),
                new[] { 1, 2, 3, 4 },
                "an identical table-hint list is the same plan key");

            AssertDifferentPlans(
                Hinted("nolock"),
                Hinted("holdlock"),
                new[] { 1, 2, 3, 4 },
                new[] { 1, 2, 3, 4 },
                "a different table-hint element is a different plan key and never changes the rows");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The tables-in-scope hint list: identical TRUE, a different element and an absent list FALSE.</summary>
    [Fact]
    public void TablesInScopeHint_SameDifferentAndAbsent_ShouldKeySeparately()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> Scoped(string hint) => Context.From<Ss5Marker>()
                .WithTablesInScopeHint(hint)
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            AssertSamePlan(
                Scoped("nolock"),
                Scoped("nolock"),
                new[] { 1, 2, 3, 4 },
                "an identical tables-in-scope hint list is the same plan key");

            AssertDifferentPlans(
                Scoped("nolock"),
                Scoped("holdlock"),
                new[] { 1, 2, 3, 4 },
                new[] { 1, 2, 3, 4 },
                "a different tables-in-scope hint is a different plan key");

            var plain = Context.From<Ss5Marker>().OrderBy(x => x.Id).Select(x => x.Id);

            AssertDifferentPlans(
                Scoped("nolock"),
                plain,
                new[] { 1, 2, 3, 4 },
                new[] { 1, 2, 3, 4 },
                "a tables-in-scope hint list present on one side only is a different plan key");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The per-join hint: identical TRUE, a different join hint FALSE, the joined rows unchanged.</summary>
    [Fact]
    public void JoinHint_SameAndDifferentHint_ShouldKeySeparately()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> Joined(string hint) => Context.From<Ss5Marker>()
                .Join(Context.From<Ss5Line>(), (m, l) => m.Id == l.MarkerId, o => o.WithJoinHint(hint))
                .OrderBy(p => p.Item1.Id)
                .OrderBy(p => p.Item2.Id)
                .Select(p => p.Item1.Id);

            AssertSamePlan(
                Joined("HASH"),
                Joined("HASH"),
                new[] { 1, 1, 2, 3 },
                "an identical join hint is the same plan key");

            AssertDifferentPlans(
                Joined("HASH"),
                Joined("MERGE"),
                new[] { 1, 1, 2, 3 },
                new[] { 1, 1, 2, 3 },
                "a different join hint is a different plan key and never changes the joined rows");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The index-hint pair over two real class-owned indexes: identical TRUE, a different index name FALSE.</summary>
    [Fact]
    public void IndexHint_SameAndDifferentIndex_ShouldKeySeparately()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> Hinted(string index) => Context.From<Ss5Marker>(o => o.WithIndex(index))
                .Where(x => x.Grp == 1)
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            AssertSamePlan(
                Hinted("ix_ss5_grp"),
                Hinted("ix_ss5_grp"),
                new[] { 1, 2 },
                "an identical index-hint list is the same plan key");

            AssertDifferentPlans(
                Hinted("ix_ss5_grp"),
                Hinted("ix_ss5_tag"),
                new[] { 1, 2 },
                new[] { 1, 2 },
                "a different index-hint element is a different plan key and never changes the rows");

            // Recorded in rv1/inputs/deferred.json: the negative pair over a real index uses two real
            // indexes because WithoutIndex() is rejected by this dialect at rendering time
            // (NotSupportedException: SQL Server has no index-ignore hint; only INDEX(...) is supported).
        }
        finally
        {
            DropMarker();
        }
    }

    [SqlTable("ss5_marker")]
    public sealed class Ss5Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("grp")]
        public int Grp { get; set; }

        [Column("tag")]
        public int Tag { get; set; }

        [Column("num")]
        public int? Num { get; set; }
    }

    [SqlTable("ss5_line")]
    public sealed class Ss5Line
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("marker_id")]
        public int MarkerId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }
}
