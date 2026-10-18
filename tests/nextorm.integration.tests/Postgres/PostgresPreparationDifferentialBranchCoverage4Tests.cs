using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

/// <summary>
/// PostgreSQL preparation differentials (cycle 4, cluster D1) executed against a live Testcontainers
/// server. Conservatively scoped: every form below already has a passing neighbour fact on this
/// provider, and each fact creates, seeds and drops its own <c>pg4_marker</c> table, so no assertion
/// depends on the shared seed totals or on test order.
///
/// <para>
/// Neighbours that prove every form (read and deliberately not repeated):
/// <c>PostgresDialectBranchCoverage3Tests.DistinctOn_OrderingVariants_ShouldKeepTheFirstRowPerKey</c>
/// (<c>DistinctOn</c> ordering matrix) and
/// <c>PostgresExtendedFunctionTests.LockWaitModes_ShouldExecute</c> (<c>ForUpdate</c>/<c>ForShare</c>
/// wait modes inside a transaction). This class adds the raw-descriptor differential
/// (<c>RawShape.Matches</c> <c>DistinctOn</c> 3727 and <c>RowLock</c> 3726) over class-owned rows, with
/// the live rows asserted before and after the one-dimension change.
/// </para>
///
/// <para>
/// Recorded (never fabricated): the descriptor comparison is asserted only in the DIFFERENCE direction.
/// <c>DistinctOnClause</c>/<c>LockClause</c> are records whose compared member is a
/// <c>LambdaExpression</c> (or a lock value built per command), so two separately built but
/// structurally identical clauses are never equal - the guard is deliberately conservative (a false
/// positive only forgoes a cache hit, it never reuses a wrong plan). The TRUE direction of those arms
/// therefore needs the identical clause instance and is not publicly reachable.
/// </para>
///
/// <para>
/// Dropped from this dispatch (recorded, never fabricated): <c>TABLESAMPLE</c> and the
/// <c>FOR SYSTEM_TIME</c> temporal form - no existing integration fact renders either - and the
/// source-level lock on a joined/derived source (the dialect capability is enforced at SQL assembly,
/// not in the compared descriptor).
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class PostgresPreparationDifferentialBranchCoverage4Tests : ProviderTestSuite
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
        Execute("drop table if exists pg4_marker");
        Execute("create table pg4_marker (id int not null primary key, num int null, label text null, grp int null)");
        Execute("insert into pg4_marker (id, num, label, grp) values (1, 10, 'one', 1), (2, 10, 'two', 2), (3, 20, 'three', 2), (4, 30, 'four', 1)");
    }

    private void DropMarker() => Execute("drop table if exists pg4_marker");

    private static QueryCommand.RawShape? Shape<TResult>(QueryCommand<TResult> command)
        => QueryCommand.CaptureRawShape(command);

    /// <summary>The DISTINCT ON key expression is a descriptor dimension (RawShape 3727), live rows kept.</summary>
    [Fact]
    public void DistinctOnDifferential_KeyExpression_ShouldCompareAndKeepTheFirstRowPerKey()
    {
        CreateMarker();
        try
        {
            var byNum = Context.From<Pg4Marker>()
                .DistinctOn(x => x.Num)
                .OrderBy(x => x.Num)
                .OrderBy(x => x.Id)
                .Select(x => x.Id);
            var byLabel = Context.From<Pg4Marker>()
                .DistinctOn(x => x.Label)
                .OrderBy(x => x.Label)
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            Shape(byNum).Should().NotBeNull("a DISTINCT ON clause over a plain table is a describable graph");
            Shape(byLabel)!.Matches(Shape(byNum)!, byLabel).Should()
                .BeFalse("a different DISTINCT ON key expression is a different descriptor");

            Context.From<Pg4Marker>()
                .DistinctOn(x => x.Num)
                .OrderBy(x => x.Num)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList().Should().Equal(new[] { 1, 3, 4 }, "one row per distinct Num, the first by Id");

            Context.From<Pg4Marker>()
                .DistinctOn(x => x.Label)
                .OrderBy(x => x.Label)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList().Should().Equal(new[] { 4, 1, 3, 2 }, "one row per distinct Label, the first by Id");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The row-lock clause (mode/wait) is a descriptor dimension (RawShape 3726), live rows kept.</summary>
    [Fact]
    public void RowLockDifferential_WaitModes_ShouldCompareAndExecuteInsideATransaction()
    {
        CreateMarker();
        try
        {
            var forUpdate = Context.From<Pg4Marker>().Where(x => x.Id == 1).ForUpdate(LockWaitMode.NoWait).Select(x => x.Id);
            var forShare = Context.From<Pg4Marker>().Where(x => x.Id == 1).ForShare(LockWaitMode.SkipLocked).Select(x => x.Id);

            Shape(forUpdate).Should().NotBeNull("a row lock over a plain table is a describable graph");
            Shape(forShare)!.Matches(Shape(forUpdate)!, forShare).Should()
                .BeFalse("a different lock mode/wait is a different descriptor");

            using var transaction = ((ITransactionManager)Context).BeginTransaction();

            Context.From<Pg4Marker>().Where(x => x.Id == 1).ForUpdate(LockWaitMode.NoWait).Select(x => x.Id)
                .ToList().Should().Equal(new[] { 1 });

            Context.From<Pg4Marker>().Where(x => x.Id == 2).ForShare().Select(x => x.Id)
                .ToList().Should().Equal(new[] { 2 });

            Context.From<Pg4Marker>().Where(x => x.Id == 1).ForShare(LockWaitMode.SkipLocked).Select(x => x.Id)
                .ToList().Should().Equal(new[] { 1 }, "the first lock shape reads its own row again");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>Marker row of the class-owned <c>pg4_marker</c> table.</summary>
    [SqlTable("pg4_marker")]
    public sealed class Pg4Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("num")]
        public int? Num { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }
    }
}
