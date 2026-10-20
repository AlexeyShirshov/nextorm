using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

/// <summary>
/// PostgreSQL plan-key branch coverage (cycle 5, cluster C1/D1) executed against a live Testcontainers
/// server. Every fact builds two DISTINCT commands over class-owned <c>pg5_marker</c>/<c>pg5_line</c>
/// rows, prepares both in place (<c>PrepareCommand(dontCalculateHash: false)</c>) so the plan key reads
/// the resolved state, asserts the two key instances are distinct, compares them with the public
/// <see cref="QueryCommand.GetQueryPlanEqualityComparer"/> and executes both sides live.
///
/// <para>
/// Targeted components of <c>QueryPlanEqualityComparer.EqualsCore</c>
/// (<c>src/nextorm.core/Query/QueryPlanEqualityComparer.cs:66-232</c>): <c>RowLock</c> :121 (the
/// <c>LockMode</c>/<c>LockWaitMode</c> pair) on a plain statement and on a joined derived statement,
/// and <c>DistinctOn</c> :89-96 through the expression comparer.
/// </para>
///
/// <para>
/// Recorded, never fabricated: the row lock applies to the statement's primary source, so the
/// "joined statement" arm locks the OUTER derived read of a joined subquery (the plain
/// <c>JoinedEntityBuilder</c> exposes no lock method) - the rendered statement still contains the
/// join and the lock, and both sides are executed.
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class PostgresPlanKeyBranchCoverage5Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

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
        Execute("drop table if exists pg5_line");
        Execute("drop table if exists pg5_marker");
        Execute("create table pg5_marker (id int not null primary key, name text not null, grp int not null, tag int not null, num int null)");
        Execute("create table pg5_line (id int not null primary key, marker_id int not null, qty int not null)");
        Execute("insert into pg5_marker (id, name, grp, tag, num) values "
                + "(1, 'alpha', 1, 10, 1), (2, 'beta', 1, 20, 1), (3, 'gamma', 2, 10, 2), (4, 'delta', 2, 20, null)");
        Execute("insert into pg5_line (id, marker_id, qty) values (10, 1, 1), (11, 1, 3), (12, 2, 5), (13, 3, 7)");
    }

    private void DropMarker()
    {
        Execute("drop table if exists pg5_line");
        Execute("drop table if exists pg5_marker");
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

    /// <summary>The row-lock mode pair: identical FOR UPDATE TRUE, FOR UPDATE vs FOR SHARE FALSE, rows unchanged.</summary>
    [Fact]
    public void RowLock_ForUpdateAndForShare_ShouldKeySeparately()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> Locked(bool update) => update
                ? Context.From<Pg5Marker>().ForUpdate().OrderBy(x => x.Id).Select(x => x.Id)
                : Context.From<Pg5Marker>().ForShare().OrderBy(x => x.Id).Select(x => x.Id);

            AssertSamePlan(
                Locked(true),
                Locked(true),
                new[] { 1, 2, 3, 4 },
                "two identical FOR UPDATE statements are the same plan key");

            AssertDifferentPlans(
                Locked(true),
                Locked(false),
                new[] { 1, 2, 3, 4 },
                new[] { 1, 2, 3, 4 },
                "a different row-lock mode (FOR UPDATE vs FOR SHARE) is a different plan key");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>A lock over a statement whose body joins two tables: locked vs unlocked is a different key, rows unchanged.</summary>
    [Fact]
    public void RowLock_OnJoinedStatement_ShouldKeySeparately()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> Joined(bool locked)
            {
                var joined = Context.From<Pg5Marker>()
                    .Join(Context.From<Pg5Line>(), (m, l) => m.Id == l.MarkerId)
                    .Select(p => new { Marker = p.Item1.Id, Line = p.Item2.Id });

                var outer = locked
                    ? Context.From(joined).ForUpdate()
                    : Context.From(joined);

                return outer.OrderBy(x => x.Marker).OrderBy(x => x.Line).Select(x => x.Marker);
            }

            AssertSamePlan(
                Joined(true),
                Joined(true),
                new[] { 1, 1, 2, 3 },
                "two identical locked joined statements are the same plan key");

            AssertDifferentPlans(
                Joined(true),
                Joined(false),
                new[] { 1, 1, 2, 3 },
                new[] { 1, 1, 2, 3 },
                "a row lock present on one side only is a different plan key over the joined statement");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The DISTINCT ON expression pair: identical TRUE, a different expression FALSE, live rows follow.</summary>
    [Fact]
    public void DistinctOn_SameAndDifferentExpression_ShouldKeySeparately()
    {
        CreateMarker();
        try
        {
            QueryCommand<int> ByGroup() => Context.From<Pg5Marker>()
                .DistinctOn(x => x.Grp)
                .OrderBy(x => x.Grp)
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            AssertSamePlan(
                ByGroup(),
                ByGroup(),
                new[] { 1, 3 },
                "two identical DISTINCT ON expressions are the same plan key");

            var byTag = Context.From<Pg5Marker>()
                .DistinctOn(x => x.Tag)
                .OrderBy(x => x.Tag)
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            AssertDifferentPlans(
                ByGroup(),
                byTag,
                new[] { 1, 3 },
                new[] { 1, 2 },
                "a different DISTINCT ON expression is a different plan key");
        }
        finally
        {
            DropMarker();
        }
    }

    [SqlTable("pg5_marker")]
    public sealed class Pg5Marker
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

    [SqlTable("pg5_line")]
    public sealed class Pg5Line
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
