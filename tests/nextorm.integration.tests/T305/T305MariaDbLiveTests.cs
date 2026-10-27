using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T305 (#305) D6: the MariaDB column of the 23-row matrix, run against a real MariaDB server. MariaDB
/// is not an <see cref="ITestProvider"/>, so the shared suite never reaches it; the cells are pinned here
/// through <see cref="MariaDbContainer.ConnectionString"/> and <see cref="MariaDbDataContext"/>.
/// <para>
/// Fail closed: an unavailable container fails the facts instead of skipping them, and the server banner
/// is asserted to contain <c>MariaDB</c> so the MySQL runtime is never presented as MariaDB evidence.
/// </para>
/// </summary>
public sealed class T305MariaDbLiveTests : IDisposable
{
    private readonly MariaDbDataContext _ctx;

    public T305MariaDbLiveTests()
    {
        MariaDbContainer.IsAvailable.Should().BeTrue(
            "the MariaDB server must be reachable for the T305 matrix: "
            + (MariaDbContainer.Failure ?? "unavailable without a reported reason"));

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());

        // MariaDB is not an ITestProvider, so the suite's EnsureSeeded never runs against this server:
        // the two shared fixtures this class writes through are created here, idempotently.
        Execute(
            "create table if not exists insert_entity "
            + "(id bigint not null auto_increment primary key, name varchar(100) null, age int null)");
        Execute(
            "create table if not exists delete_entity "
            + "(id int not null primary key, name varchar(100) null, age int null)");
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void ServerIdentity_ShouldBeMariaDb()
    {
        var banner = Scalar<string>(_ctx, "select version() as v");

        banner.Should().Contain("MariaDB", "the matrix must run against MariaDB, never through MySQL");
    }

    [Fact]
    public void NativeMode_ShouldWriteThroughLocalInfileOrRefuseByTheMissingName()
    {
        var marker = T305Sql.Marker("maria_native");
        var clientOnly = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        try
        {
            var source = T305Sql.Counting(T305Sql.InsertRows(marker, 2));
            var clientMissing = () => clientOnly.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native))
                .Values(source)
                .BulkInsert();
            clientMissing.Should().Throw<NotSupportedException>().WithMessage("*AllowLoadLocalInfile*");
            source.Enumerated.Should().Be(
                source.RowCount,
                "the refusal happens before the write: the synchronous terminal buffered the source for whole-set "
                + "validation first, no LOAD is attempted, and the source is never replayed");
        }
        finally
        {
            clientOnly.Dispose();
        }

        var connectionString = MariaDbContainer.ConnectionString + ";AllowLoadLocalInfile=true";
        using var clientEnabled = new MariaDbDataContext(connectionString, new DataContextBuilder());
        var serverAllows = Scalar<int>(_ctx, "select cast(@@GLOBAL.local_infile as signed) as v") == 1;

        if (serverAllows)
        {
            clientEnabled.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native))
                .Values(T305Sql.InsertRows(marker, 2))
                .BulkInsert()
                .Should().Be(2, "both LOCAL prerequisites are present, so the native LOAD runs once");
        }
        else
        {
            var source = T305Sql.Counting(T305Sql.InsertRows(marker, 2));
            var serverMissing = () => clientEnabled.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native))
                .Values(source)
                .BulkInsert();
            serverMissing.Should().Throw<NotSupportedException>().WithMessage("*local_infile*");
            source.Enumerated.Should().Be(
                source.RowCount,
                "the read-only server preflight refuses before the write; the synchronous terminal buffered the source "
                + "for whole-set validation first, and it is never replayed");
        }

        // Auto never loses rows: it uses the native path only when fully compatible, otherwise portable.
        _ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Auto))
            .Values(T305Sql.InsertRows(marker, 3))
            .BulkInsert()
            .Should().Be(3);
        _ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList()
            .Should().NotBeEmpty();
    }

    [Fact]
    public void KeepIdentityTrue_ShouldWriteTheZeroIdentityValueAndFalseShouldGenerate()
    {
        var zeroMarker = T305Sql.Marker("maria_zero_identity");
        var generatedMarker = T305Sql.Marker("maria_generated_identity");

        try
        {
            _ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity())
                .Values([new InsertEntity { Id = 0, Name = zeroMarker, Age = 1 }])
                .BulkInsert()
                .Should().Be(1);
            _ctx.From<IInsertEntity>().Where(x => x.Name == zeroMarker).Select(x => new { x.Id }).ToList()
                .Should().ContainSingle().Which.Id.Should().Be(0, "NO_AUTO_VALUE_ON_ZERO keeps the explicit zero");

            _ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity(false))
                .Values([new InsertEntity { Id = 0, Name = generatedMarker, Age = 2 }])
                .BulkInsert()
                .Should().Be(1);
            _ctx.From<IInsertEntity>().Where(x => x.Name == generatedMarker).Select(x => new { x.Id }).ToList()
                .Should().ContainSingle().Which.Id.Should().BePositive();
        }
        finally
        {
            _ctx.CreateDeleteBuilder<IInsertEntity>().Where(x => x.Name == zeroMarker || x.Name == generatedMarker).Delete();
        }
    }

    [Fact]
    public void IgnoreDuplicatesTrue_ShouldSkipAndFalseShouldFailOnTheConflictingKey()
    {
        var id = Random.Shared.Next(1_500_000_000, 1_900_000_000);
        int[] ids = [id];
        var marker = T305Sql.Marker("maria_ignore");

        try
        {
            _ctx.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity())
                .Values([new DeleteEntity { Id = id, Name = marker, Age = 1 }])
                .BulkInsert()
                .Should().Be(1);

            _ctx.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity().IgnoreDuplicates(true))
                .Values([new DeleteEntity { Id = id, Name = marker, Age = 2 }])
                .BulkInsert()
                .Should().Be(0);
            _ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Age }).ToList()
                .Should().ContainSingle().Which.Age.Should().Be(1);

            var explicitFalse = () => _ctx.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity(false).IgnoreDuplicates(false))
                .Values([new DeleteEntity { Id = id, Name = marker, Age = 3 }])
                .BulkInsert();
            explicitFalse.Should().Throw<DbException>();
        }
        finally
        {
            _ctx.CreateDeleteBuilder<IDeleteEntity>().Where(x => ids.Contains(x.Id)).Delete();
        }
    }

    [Fact]
    public void ReturningKey_ShouldBeRefusedBeforeVersion13AndReturnTheKeysFrom13On()
    {
        var version = ParseVersion(Scalar<string>(_ctx, "select version() as v"));
        var marker = T305Sql.Marker("maria_returning");

        if (version < new Version(13, 0))
        {
            var source = T305Sql.Counting(T305Sql.InsertRows(marker, 2));
            var act = () => _ctx.CreateBulkInsertBuilder<IInsertEntity>().Values(source).ReturningKey<long>().ToList();
            act.Should().Throw<NotSupportedException>().WithMessage("*returning*");
            source.Enumerated.Should().Be(
                source.RowCount,
                "the refusal happens before the write: the synchronous terminal buffered the source for whole-set "
                + "validation first, and it is never replayed");
            return;
        }

        try
        {
            // MariaDB 13.0+ exposes INSERT/UPDATE ... RETURNING; the keys are the written identity values.
            var keys = _ctx.CreateBulkInsertBuilder<IInsertEntity>().Values(T305Sql.InsertRows(marker, 2)).ReturningKey<long>().ToList();
            keys.Should().HaveCount(2).And.OnlyContain(key => key > 0);
        }
        finally
        {
            _ctx.CreateDeleteBuilder<IInsertEntity>().Where(x => x.Name == marker).Delete();
        }
    }

    [Fact]
    public void CheckConstraintsFalse_ShouldScopeTheSessionControlAndRestoreItEvenOnError()
    {
        T305Sql.Recreate(
            _ctx,
            "t305_bulk_check",
            "create table t305_bulk_check (id int not null primary key, age int not null, constraint ck_t305_bulk_check_age check (age >= 0))");

        var enforced = () => _ctx.CreateBulkInsertBuilder<T305BulkCheck>(o => o.CheckConstraints(true))
            .Values([new T305BulkCheck { Id = 1, Age = -1 }])
            .BulkInsert();
        enforced.Should().Throw<DbException>();

        _ctx.CreateBulkInsertBuilder<T305BulkCheck>(o => o.CheckConstraints(false))
            .Values([new T305BulkCheck { Id = 2, Age = -1 }])
            .BulkInsert()
            .Should().Be(1);
        Scalar<int>(_ctx, "select cast(@@check_constraint_checks as signed) as v").Should().Be(1,
            "the scoped session control is restored after a successful write");

        // The error path restores the control too: a duplicate key fails inside the same scope.
        var failing = () => _ctx.CreateBulkInsertBuilder<T305BulkCheck>(o => o.CheckConstraints(false).IgnoreDuplicates(false))
            .Values([new T305BulkCheck { Id = 2, Age = -1 }])
            .BulkInsert();
        failing.Should().Throw<DbException>();
        Scalar<int>(_ctx, "select cast(@@check_constraint_checks as signed) as v").Should().Be(1,
            "the scoped session control is restored in finally, even when the write fails");
    }

    [Fact]
    public void KeepNullsFalse_ShouldAcceptTheDefaultEquivalentShapeAndRefuseAColumnDefault()
    {
        // The default-equivalent shape (row 8, "E"): every written column carries a NULL input and has a
        // NULL/absent SQL default. The defaulted variant carries a non-NULL default, which the write would
        // silently skip, so it is refused by name.
        T305Sql.Recreate(
            _ctx,
            "t305_bulk_all_nullable",
            "create table t305_bulk_all_nullable (id int null, note varchar(100) null)");
        T305Sql.Recreate(
            _ctx,
            "t305_bulk_defaulted_all_nullable",
            "create table t305_bulk_defaulted_all_nullable (id int null, label varchar(100) null default 'dflt')");

        _ctx.CreateBulkInsertBuilder<T305NullableRow>(o => o.KeepNulls(false))
            .Values([new T305NullableRow { Id = 1, Note = null }])
            .BulkInsert()
            .Should().Be(1);
        _ctx.From<T305NullableRow>().Where(x => x.Id == 1).Select(x => new { x.Note }).ToList()
            .Should().ContainSingle().Which.Note.Should().BeNull("NULL is indistinguishable from the absent DEFAULT");

        var source = T305Sql.Counting([new T305DefaultedNullableRow { Id = 2, Label = null }]);
        var refused = () => _ctx.CreateBulkInsertBuilder<T305DefaultedNullableRow>(o => o.KeepNulls(false))
            .Values(source)
            .BulkInsert();
        refused.Should().Throw<NotSupportedException>().WithMessage("*KeepNulls=false*label*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "the write always sends the supplied value, so a column DEFAULT cannot be applied per row: the request "
            + "is refused before the write, after the synchronous terminal buffered the source for validation");
    }

    [Fact]
    public void TableLockAndFireTriggersTrue_ShouldBeRefusedAndFalseShouldBeHonouredByAbsence()
    {
        T305Sql.Recreate(_ctx, "t305_bulk_dest", "create table t305_bulk_dest (id int not null primary key, name varchar(100) null)");

        var locked = () => _ctx.CreateBulkInsertBuilder<IT305BulkDest>(o => o.TableLock(true))
            .Values(T305Sql.DestRows(1, 1))
            .BulkInsert();
        var firing = () => _ctx.CreateBulkInsertBuilder<IT305BulkDest>(o => o.FireTriggers(true))
            .Values(T305Sql.DestRows(2, 1))
            .BulkInsert();

        locked.Should().Throw<NotSupportedException>().WithMessage("*TableLock=true*");
        firing.Should().Throw<NotSupportedException>().WithMessage("*FireTriggers=true*");

        _ctx.CreateBulkInsertBuilder<IT305BulkDest>(o => o.TableLock(false).FireTriggers(false))
            .Values(T305Sql.DestRows(3, 2))
            .BulkInsert()
            .Should().Be(2);
        _ctx.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(2);
    }

    [Fact]
    public void Destination_CurrentDatabaseAndSchema_ShouldWrite()
    {
        T305Sql.Recreate(_ctx, "t305_bulk_dest", "create table t305_bulk_dest (id int not null primary key, name varchar(100) null)");
        var database = Scalar<string>(_ctx, "select database() as v");

        _ctx.CreateBulkInsertBuilder<IT305BulkDest>(o => o.Table("t305_bulk_dest").Database(database))
            .Values(T305Sql.DestRows(11, 2))
            .BulkInsert()
            .Should().Be(2);
        _ctx.CreateBulkInsertBuilder<IT305BulkDest>(o => o.Table("t305_bulk_dest").Schema(database))
            .Values(T305Sql.DestRows(21, 2))
            .BulkInsert()
            .Should().Be(2);
        _ctx.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(4);
    }

    [Fact]
    public void LocalTemporary_ShouldWriteIntoTheSameSessionTempTable()
    {
        var name = "t305_temp_" + Guid.NewGuid().ToString("N");
        Execute($"create temporary table {name} (id int not null, name varchar(100) null)");

        try
        {
            _ctx.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                    .TargetKind(BulkInsertTargetKind.LocalTemporary)
                    .Table(name))
                .Values(T305Sql.DestRows(31, 3))
                .BulkInsert()
                .Should().Be(3);

            _ctx.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().HaveCount(3);
        }
        finally
        {
            Execute($"drop temporary table if exists {name}");
        }
    }

    [Fact]
    public void ModePortable_KeepIdentityTrue_ShouldWriteTheZeroIdentityValueUnderTheScopedMode()
    {
        // Row 3 "P" on MariaDB: the portable path runs the same session-scoped zero-mode as the native load,
        // and restores `sql_mode` afterwards.
        var zeroMarker = T305Sql.Marker("maria_portable_zero");
        var generatedMarker = T305Sql.Marker("maria_portable_generated");

        try
        {
            _ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Portable).KeepIdentity())
                .Values([new InsertEntity { Id = 0, Name = zeroMarker, Age = 1 }])
                .BulkInsert()
                .Should().Be(1);
            _ctx.From<IInsertEntity>().Where(x => x.Name == zeroMarker).Select(x => new { x.Id }).ToList()
                .Should().ContainSingle().Which.Id.Should().Be(0, "NO_AUTO_VALUE_ON_ZERO keeps the explicit zero");
            Scalar<string>(_ctx, "select @@session.sql_mode")
                .Should().NotContain("NO_AUTO_VALUE_ON_ZERO", "the scoped mode is restored after the write");

            // Negative arm: without the request the zero-mode is not applied, so the identity is generated.
            _ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Portable).KeepIdentity(false))
                .Values([new InsertEntity { Id = 0, Name = generatedMarker, Age = 2 }])
                .BulkInsert()
                .Should().Be(1);
            _ctx.From<IInsertEntity>().Where(x => x.Name == generatedMarker).Select(x => new { x.Id }).ToList()
                .Should().ContainSingle().Which.Id.Should().BePositive("the zero-mode is scoped to a KeepIdentity request only");
        }
        finally
        {
            _ctx.CreateDeleteBuilder<IInsertEntity>().Where(x => x.Name == zeroMarker || x.Name == generatedMarker).Delete();
        }
    }

    [Fact]
    public void BoundedBatches_ShouldWriteEveryRowOfAHundredRowSource()
    {
        // Row 10/23 live arm: the bounded batching never drops a row on MariaDB either.
        var marker = T305Sql.Marker("maria_batches");
        var rows = T305Sql.InsertRows(marker, 100);
        var seen = new List<int>();

        try
        {
            _ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.MaxBatchSize(9).NotifyAfter(9, (n, _) => seen.Add(n)))
                .Values(rows)
                .BulkInsert()
                .Should().Be(100);

            seen.Should().NotBeEmpty();
            seen.Should().BeInAscendingOrder();
            _ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Id }).ToList().Should().HaveCount(100);
        }
        finally
        {
            _ctx.CreateDeleteBuilder<IInsertEntity>().Where(x => x.Name == marker).Delete();
        }
    }

    private void Execute(string sql)
    {
        _ctx.EnsureConnectionOpen();
        using var command = _ctx.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private static T Scalar<T>(MariaDbDataContext context, string sql)
    {
        using var result = context.ExecuteRaw(sql);
        return result.Read<T>().Single();
    }

    private static Version ParseVersion(string banner)
    {
        var parts = banner.Split('-')[0].Split('.');
        var numbers = new int[3];
        for (var i = 0; i < parts.Length && i < numbers.Length; i++)
            _ = int.TryParse(parts[i], out numbers[i]);

        return new Version(numbers[0], numbers[1], numbers[2]);
    }
}
