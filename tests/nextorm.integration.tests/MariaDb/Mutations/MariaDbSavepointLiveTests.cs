using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T289 (issue #289): the live savepoint matrix for MariaDB 11.4. MariaDB has no
/// <see cref="ITestProvider"/>, so the shared <see cref="CommonTestSuite"/> facts never reach it; the
/// same cells are re-pinned here through the established harness route
/// (<see cref="MariaDbContainer.ConnectionString"/> + <see cref="MariaDbDataContext"/>). MariaDB is
/// never run through the MySQL container, and every assertion reads rows back from the database, so a
/// driver that silently ignores a savepoint cannot pass.
/// <para>
/// An unavailable container fails the facts instead of skipping them: a skip is not a pass, and the
/// MariaDB row stays unproven until the harness is reachable.
/// </para>
/// </summary>
public sealed class MariaDbSavepointLiveTests : IDisposable
{
    private readonly MariaDbDataContext _ctx;

    public MariaDbSavepointLiveTests(ITestOutputHelper output)
    {
        MariaDbContainer.IsAvailable.Should().BeTrue(
            "the MariaDB container must be reachable for the T289 savepoint matrix: " +
            $"{MariaDbContainer.Failure ?? "unavailable without a reported reason"}");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());

        using (var version = _ctx.ExecuteRaw("select version() as version"))
        {
            var banner = version.Read<string>().Single();
            banner.Should().Contain("MariaDB",
                "the savepoint matrix must run against the MariaDB container, never through the MySQL container");
            output.WriteLine($"T289 MariaDB server version: {banner}");
        }

        Execute("drop table if exists savepoint_probe");
        Execute(
            "create table savepoint_probe (id int not null auto_increment primary key, " +
            "name varchar(100) null, age int not null) engine=InnoDB");
    }

    public void Dispose() => _ctx.Dispose();

    private ITransactionManager Transactions => _ctx;

    private static string Marker() => "sp_" + Guid.NewGuid().ToString("N")[..16];

    private static string Point(string suffix) => "sp_" + suffix + Guid.NewGuid().ToString("N")[..8];

    private void Execute(string sql)
    {
        using var result = _ctx.ExecuteRaw(sql);
    }

    private void Insert(string? name, int age)
    {
        using var result = name is null
            ? _ctx.ExecuteRaw(
                "insert into savepoint_probe (name, age) values (null, @age)",
                [new ProcedureParameter("age", age)])
            : _ctx.ExecuteRaw(
                "insert into savepoint_probe (name, age) values (@name, @age)",
                [new ProcedureParameter("name", name), new ProcedureParameter("age", age)]);
    }

    private async Task InsertAsync(string? name, int age)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        if (name is null)
        {
            using var result = await _ctx.ExecuteRawAsync(
                "insert into savepoint_probe (name, age) values (null, @age)",
                [new ProcedureParameter("age", age)],
                cancellationToken);
        }
        else
        {
            using var result = await _ctx.ExecuteRawAsync(
                "insert into savepoint_probe (name, age) values (@name, @age)",
                [new ProcedureParameter("name", name), new ProcedureParameter("age", age)],
                cancellationToken);
        }
    }

    private long Count(string name)
    {
        using var result = _ctx.ExecuteRaw(
            "select count(*) as c from savepoint_probe where name = @name",
            [new ProcedureParameter("name", name)]);
        return result.Read<long>().Single();
    }

    private async Task<long> CountAsync(string name)
    {
        using var result = await _ctx.ExecuteRawAsync(
            "select count(*) as c from savepoint_probe where name = @name",
            [new ProcedureParameter("name", name)],
            TestContext.Current.CancellationToken);
        return result.Read<long>().Single();
    }

    private async Task InsertRow(bool asyncForm, string? name, int age)
    {
        if (asyncForm)
            await InsertAsync(name, age);
        else
            Insert(name, age);
    }

    private async Task<long> CountRows(bool asyncForm, string name) =>
        asyncForm ? await CountAsync(name) : Count(name);

    private async Task<DbTransaction> BeginTransaction(bool asyncForm) =>
        asyncForm
            ? await Transactions.BeginTransactionAsync(TestContext.Current.CancellationToken)
            : Transactions.BeginTransaction();

    private async Task CreateSavepoint(bool asyncForm, string name)
    {
        if (asyncForm)
            await Transactions.CreateSavepointAsync(name, TestContext.Current.CancellationToken);
        else
            Transactions.CreateSavepoint(name);
    }

    private async Task RollbackToSavepoint(bool asyncForm, string name)
    {
        if (asyncForm)
            await Transactions.RollbackToSavepointAsync(name, TestContext.Current.CancellationToken);
        else
            Transactions.RollbackToSavepoint(name);
    }

    private async Task ReleaseSavepoint(bool asyncForm, string name)
    {
        if (asyncForm)
            await Transactions.ReleaseSavepointAsync(name, TestContext.Current.CancellationToken);
        else
            Transactions.ReleaseSavepoint(name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_Create_ShouldLeaveTheTransactionUsable(bool asyncForm)
    {
        var keep = Marker();
        var after = Marker();

        using (var tx = await BeginTransaction(asyncForm))
        {
            await InsertRow(asyncForm, keep, 1);
            await CreateSavepoint(asyncForm, Point("create"));
            await InsertRow(asyncForm, after, 2);
            tx.Commit();
        }

        (await CountRows(asyncForm, keep)).Should().Be(1);
        (await CountRows(asyncForm, after)).Should().Be(1,
            "creating a savepoint must neither end nor roll back the transaction");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_RollbackTo_ShouldDiscardOnlyLaterRows(bool asyncForm)
    {
        var keep = Marker();
        var discard = Marker();
        var point = Point("rollback");

        using var tx = await BeginTransaction(asyncForm);
        await InsertRow(asyncForm, keep, 1);
        await CreateSavepoint(asyncForm, point);
        await InsertRow(asyncForm, discard, 2);
        await RollbackToSavepoint(asyncForm, point);

        (await CountRows(asyncForm, keep)).Should().Be(1);
        (await CountRows(asyncForm, discard)).Should().Be(0);
        tx.Rollback();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_ReleaseThenCommit_ShouldPersistBothRows(bool asyncForm)
    {
        var before = Marker();
        var after = Marker();
        var point = Point("release");

        using (var tx = await BeginTransaction(asyncForm))
        {
            await InsertRow(asyncForm, before, 1);
            await CreateSavepoint(asyncForm, point);
            await InsertRow(asyncForm, after, 2);
            await ReleaseSavepoint(asyncForm, point);
            tx.Commit();
        }

        (await CountRows(asyncForm, before)).Should().Be(1);
        (await CountRows(asyncForm, after)).Should().Be(1,
            "releasing a savepoint must not roll back the work done after it");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_Nested_ShouldRollBackToTheInnerAndOuterPoints(bool asyncForm)
    {
        var baseline = Marker();
        var outerRow = Marker();
        var innerRow = Marker();
        var outer = Point("outer");
        var inner = Point("inner");

        using var tx = await BeginTransaction(asyncForm);
        await InsertRow(asyncForm, baseline, 1);
        await CreateSavepoint(asyncForm, outer);
        await InsertRow(asyncForm, outerRow, 2);
        await CreateSavepoint(asyncForm, inner);
        await InsertRow(asyncForm, innerRow, 3);

        await RollbackToSavepoint(asyncForm, inner);

        (await CountRows(asyncForm, baseline)).Should().Be(1);
        (await CountRows(asyncForm, outerRow)).Should().Be(1);
        (await CountRows(asyncForm, innerRow)).Should().Be(0);

        await RollbackToSavepoint(asyncForm, outer);

        (await CountRows(asyncForm, baseline)).Should().Be(1);
        (await CountRows(asyncForm, outerRow)).Should().Be(0);
        tx.Rollback();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_CreateThenRollbackWithNoCommandBetween_ShouldKeepTheTransactionUsable(bool asyncForm)
    {
        var before = Marker();
        var after = Marker();
        var point = Point("nocmd");

        using (var tx = await BeginTransaction(asyncForm))
        {
            await InsertRow(asyncForm, before, 1);
            await CreateSavepoint(asyncForm, point);
            await RollbackToSavepoint(asyncForm, point);
            await InsertRow(asyncForm, after, 2);
            tx.Commit();
        }

        (await CountRows(asyncForm, before)).Should().Be(1);
        (await CountRows(asyncForm, after)).Should().Be(1,
            "an immediate rollback to a fresh savepoint must leave the transaction usable");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_RollbackTo_ShouldPreserveNullsInPreSavepointRows(bool asyncForm)
    {
        var age = Random.Shared.Next(1_000_000_000, int.MaxValue);
        var point = Point("nulls");

        long CountRowsWith(string predicate)
        {
            using var result = _ctx.ExecuteRaw(
                $"select count(*) as c from savepoint_probe where age = @age and {predicate}",
                [new ProcedureParameter("age", age)]);
            return result.Read<long>().Single();
        }

        async Task<long> CountRowsWithAsync(string predicate)
        {
            using var result = await _ctx.ExecuteRawAsync(
                $"select count(*) as c from savepoint_probe where age = @age and {predicate}",
                [new ProcedureParameter("age", age)],
                TestContext.Current.CancellationToken);
            return result.Read<long>().Single();
        }

        using var tx = await BeginTransaction(asyncForm);
        await InsertRow(asyncForm, name: null, age);
        await CreateSavepoint(asyncForm, point);
        await InsertRow(asyncForm, Marker(), age);
        await RollbackToSavepoint(asyncForm, point);

        var nullRows = asyncForm ? await CountRowsWithAsync("name is null") : CountRowsWith("name is null");
        nullRows.Should().Be(1, "the pre-savepoint row keeps its NULL after the rollback");

        var totalRows = asyncForm ? await CountRowsWithAsync("1 = 1") : CountRowsWith("1 = 1");
        totalRows.Should().Be(1, "the row created after the savepoint must be gone");
        tx.Rollback();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_NoActiveTransaction_ShouldThrowInvalidOperation(bool asyncForm)
    {
        var point = Point("noactive");

        var act = async () =>
        {
            if (asyncForm)
                await Transactions.CreateSavepointAsync(point, TestContext.Current.CancellationToken);
            else
                Transactions.CreateSavepoint(point);
        };

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*active transaction*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_InvalidNames_ShouldThrowArgumentException(bool asyncForm)
    {
        using var tx = await BeginTransaction(asyncForm);

        string[] invalid = ["", "bad name", "1sp", "sp-name", "sp;drop", new string('a', 33)];
        foreach (var name in invalid)
        {
            if (asyncForm)
            {
                var asyncAct = async () => await Transactions.CreateSavepointAsync(name, TestContext.Current.CancellationToken);
                await asyncAct.Should().ThrowAsync<ArgumentException>();
            }
            else
            {
                var act = () => Transactions.CreateSavepoint(name);
                act.Should().Throw<ArgumentException>();
            }
        }

        if (asyncForm)
        {
            var nullAsyncAct = async () => await Transactions.CreateSavepointAsync(null!, TestContext.Current.CancellationToken);
            await nullAsyncAct.Should().ThrowAsync<ArgumentNullException>();
        }
        else
        {
            var nullAct = () => Transactions.CreateSavepoint(null!);
            nullAct.Should().Throw<ArgumentNullException>();
        }

        tx.Rollback();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_DuplicateName_ShouldBeDelegatedWithoutATrackingLayer(bool asyncForm)
    {
        var keep = Marker();
        var point = Point("dup");

        using var tx = await BeginTransaction(asyncForm);
        await InsertRow(asyncForm, keep, 1);
        await CreateSavepoint(asyncForm, point);
        await CreateSavepoint(asyncForm, point);
        await InsertRow(asyncForm, Marker(), 2);
        await RollbackToSavepoint(asyncForm, point);

        // MySQL-family SHADOWING: a repeated SAVEPOINT name replaces the older point, so only the row
        // written before both creates is portably guaranteed; the two calls must simply be delegated.
        (await CountRows(asyncForm, keep)).Should().Be(1);
        tx.Rollback();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_EnlistedTransaction_ShouldReceiveSavepointsWithoutChangingOwnership(bool asyncForm)
    {
        var keep = Marker();
        var discard = Marker();
        var point = Point("enlisted");

        ((DataContext)_ctx).EnsureConnectionOpen();
        var connection = ((DataContext)_ctx).GetConnection();
        using var raw = asyncForm
            ? await connection.BeginTransactionAsync(TestContext.Current.CancellationToken)
            : connection.BeginTransaction();

        Transactions.UseTransaction(raw);
        try
        {
            await InsertRow(asyncForm, keep, 1);
            await CreateSavepoint(asyncForm, point);
            await InsertRow(asyncForm, discard, 2);
            await RollbackToSavepoint(asyncForm, point);

            (await CountRows(asyncForm, keep)).Should().Be(1);
            (await CountRows(asyncForm, discard)).Should().Be(0);
            raw.Connection.Should().NotBeNull("the context must never finish a caller-owned transaction");
        }
        finally
        {
            Transactions.UseTransaction(null);
            if (asyncForm)
                await raw.RollbackAsync(TestContext.Current.CancellationToken);
            else
                raw.Rollback();
        }

        Transactions.CurrentTransaction.Should().BeNull();
    }
}
