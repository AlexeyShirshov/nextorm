using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// Live MariaDB 11.4 row-locking tests (EV-10/V-27), run against the existing <see cref="MariaDbContainer"/>.
/// MariaDB is not part of the shared provider matrix. The whole-query locks execute MariaDB's native grammar
/// (`for update`, `lock in share mode [skip locked]`); the source-level lock fails closed (no `FOR UPDATE OF`).
/// </summary>
public sealed class MariaDbRowLockingLiveTests : IDisposable
{
    private readonly MariaDbDataContext _reader;

    public MariaDbRowLockingLiveTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");
        _reader = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _reader.Dispose();

    private void Seed()
    {
        Execute("drop table if exists lock_probe");
        Execute("create table lock_probe (id int not null primary key, name varchar(50)) engine=InnoDB");
        Execute("insert into lock_probe (id, name) values (1, 'a'), (2, 'b')");
    }

    private void Execute(string sql)
    {
        ((DataContext)_reader).EnsureConnectionOpen();
        using var cmd = ((DataContext)_reader).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    [Fact]
    [Trait("PDCA", "T216")]
    public void WholeUpdateLock_SkipLocked_ShouldSkipRowHeldByAnotherTransaction()
    {
        using var locker = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        var tx = ((ITransactionManager)locker).BeginTransaction();

        try
        {
            locker.From<ILockProbe>().Where(x => x.Id == 1).ForUpdate().Select(x => x.Id).ToList()
                .Should().Equal(1);

            _reader.From<ILockProbe>().Where(x => x.Id == 1).ForUpdate(LockWaitMode.SkipLocked).Select(x => x.Id).ToList()
                .Should().BeEmpty();
        }
        finally
        {
            tx.Rollback();
        }
    }

    [Fact]
    [Trait("PDCA", "T216")]
    public void WholeShareLock_ShouldExecuteMariaDbNativeGrammar()
    {
        _reader.From<ILockProbe>().Where(x => x.Id == 1).ForShare().Select(x => x.Id).ToList()
            .Should().Equal(1);
    }

    [Fact]
    [Trait("PDCA", "T216")]
    public void SourceLock_ShouldFailClosed()
    {
        var act = () => _reader.From<ILockProbe>(o => o.ForUpdate()).Select(x => x.Id).ToList();

        act.Should().Throw<NotSupportedException>();
    }
}

[SqlTable("lock_probe")]
internal interface ILockProbe
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string? Name { get; set; }
}
