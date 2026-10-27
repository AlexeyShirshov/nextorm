using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

// =====================================================================================================
// T305 (#305) D6: the provider-only live cells of the 23-row bulk-insert matrix (§5). Each class owns
// its own fixtures, creates them on the live server and drops them again, and every assertion reads the
// rows back from the database: a driver that silently ignores an option cannot pass.
//
// Cross-provider contracts (default-path control, identity arm, portable limits, timeout, progress,
// ServerName and local-temporary refusals, regular target) live in CommonTestSuite.BulkInsert.cs.
// =====================================================================================================

/// <summary>
/// SQL Server cells: rows 6-9 (constraint checking, table lock, nulls, triggers), rows 19-20 (internal
/// transaction) and rows 13-14/16-17 (destination and local temporary target).
/// </summary>
[Collection("SqlServer")]
public sealed class T305SqlServerBulkInsertTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    [Fact]
    public void KeepIdentityFalse_ShouldGenerateTheKeyInsteadOfWritingTheExplicitOne()
    {
        var marker = T305Sql.Marker("keep_identity_false");
        var rows = Enumerable.Range(0, 3).Select(i => new InsertEntity { Id = 0, Name = marker, Age = i }).ToList();

        try
        {
            Context.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity(false)).Values(rows).BulkInsert().Should().Be(3);

            var read = Context.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Id }).ToList();
            read.Should().HaveCount(3);
            read.Should().OnlyContain(row => row.Id > 0, "the generated identity value is used, not the explicit zero");
        }
        finally
        {
            Context.CreateDeleteBuilder<IInsertEntity>().Where(x => x.Name == marker).Delete();
        }
    }

    [Fact]
    public void UseInternalTransactionTrue_ShouldCommitTheDriverBatch()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table dbo.t305_bulk_dest (id int not null primary key, name nvarchar(100) null)");

        var written = Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                .SqlServer(new SqlServerBulkInsertOptions { UseInternalTransaction = true }))
            .Values(T305Sql.DestRows(1, 5))
            .BulkInsert();

        written.Should().Be(5);
        Context.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(5);
    }

    [Fact]
    public void UseInternalTransactionFalse_ShouldWriteWithoutAnInternalTransaction()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table dbo.t305_bulk_dest (id int not null primary key, name nvarchar(100) null)");

        var written = Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                .SqlServer(new SqlServerBulkInsertOptions { UseInternalTransaction = false }))
            .Values(T305Sql.DestRows(11, 5))
            .BulkInsert();

        written.Should().Be(5);
        Context.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(5);
    }

    [Fact]
    public void UseInternalTransactionTrue_WithTheContextTransaction_ShouldBeRefusedBeforeTheSource()
    {
        using var transaction = ((ITransactionManager)Context).BeginTransaction();
        var source = T305Sql.Counting(T305Sql.DestRows(21, 2));

        var act = () => Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                .SqlServer(new SqlServerBulkInsertOptions { UseInternalTransaction = true }))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*UseInternalTransaction=true*external transaction*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "the synchronous terminal buffers the whole source for validation before it renders any SQL, so the "
            + "observable contract here is 'refused before the write, source read exactly once': no driver batch is "
            + "started and the source is not replayed");
        transaction.Rollback();
    }

    [Fact]
    public void CheckConstraintsTrue_ShouldRejectTheViolatingRow()
    {
        T305Sql.Recreate(
            Context,
            "t305_bulk_check",
            "create table dbo.t305_bulk_check (id int not null primary key, age int not null constraint ck_t305_bulk_check_age check (age >= 0))");

        var act = () => Context.CreateBulkInsertBuilder<T305BulkCheck>(o => o.CheckConstraints(true))
            .Values([new T305BulkCheck { Id = 1, Age = -1 }])
            .BulkInsert();

        act.Should().Throw<DbException>();
        Context.From<T305BulkCheck>().Select(x => new { x.Id }).ToList().Should().BeEmpty();
    }

    [Fact]
    public void CheckConstraintsFalse_ShouldTurnTheCheckOffForTheWrite()
    {
        T305Sql.Recreate(
            Context,
            "t305_bulk_check",
            "create table dbo.t305_bulk_check (id int not null primary key, age int not null constraint ck_t305_bulk_check_age check (age >= 0))");

        var written = Context.CreateBulkInsertBuilder<T305BulkCheck>(o => o.CheckConstraints(false))
            .Values([new T305BulkCheck { Id = 1, Age = -1 }])
            .BulkInsert();

        written.Should().Be(1);
        Context.From<T305BulkCheck>().Select(x => new { x.Age }).ToList().Should().ContainSingle().Which.Age.Should().Be(-1);
    }

    [Fact]
    public void TableLockTrueAndFalse_ShouldBothWriteTheRows()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table dbo.t305_bulk_dest (id int not null primary key, name nvarchar(100) null)");

        var locked = Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.TableLock(true))
            .Values(T305Sql.DestRows(31, 2))
            .BulkInsert();
        var unlocked = Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.TableLock(false))
            .Values(T305Sql.DestRows(41, 2))
            .BulkInsert();

        locked.Should().Be(2);
        unlocked.Should().Be(2);
        Context.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(4);
    }

    [Fact]
    public void FireTriggersTrue_ShouldFireTheInsertTriggerAndFalseShouldNot()
    {
        T305Sql.Recreate(Context, "t305_bulk_audit", "create table dbo.t305_bulk_audit (id int not null)");
        T305Sql.Recreate(Context, "t305_bulk_trigger", "create table dbo.t305_bulk_trigger (id int not null primary key, age int null)");
        T305Sql.Execute(Context, "if object_id('dbo.t305_bulk_trigger_ai','TR') is not null drop trigger dbo.t305_bulk_trigger_ai");
        T305Sql.Execute(
            Context,
            "create trigger dbo.t305_bulk_trigger_ai on dbo.t305_bulk_trigger after insert as insert into dbo.t305_bulk_audit (id) select id from inserted");

        try
        {
            Context.CreateBulkInsertBuilder<T305BulkTrigger>(o => o.FireTriggers(true))
                .Values([new T305BulkTrigger { Id = 1, Age = 1 }])
                .BulkInsert()
                .Should().Be(1);
            T305Sql.Scalar<int>(Context, "select count(*) as v from dbo.t305_bulk_audit").Should().Be(1);

            Context.CreateBulkInsertBuilder<T305BulkTrigger>(o => o.FireTriggers(false))
                .Values([new T305BulkTrigger { Id = 2, Age = 2 }])
                .BulkInsert()
                .Should().Be(1);
            T305Sql.Scalar<int>(Context, "select count(*) as v from dbo.t305_bulk_audit").Should().Be(1,
                "an explicit false is honoured by absence: no trigger fires");
        }
        finally
        {
            T305Sql.Execute(Context, "if object_id('dbo.t305_bulk_trigger_ai','TR') is not null drop trigger dbo.t305_bulk_trigger_ai");
        }
    }

    [Fact]
    public void KeepNullsTrue_ShouldWriteNullAndFalseShouldApplyTheColumnDefault()
    {
        T305Sql.Recreate(
            Context,
            "t305_bulk_defaulted",
            "create table dbo.t305_bulk_defaulted (id int not null primary key, note nvarchar(100) null, label nvarchar(100) null default 'dflt')");

        Context.CreateBulkInsertBuilder<T305BulkDefaulted>(o => o.KeepNulls(true))
            .Values([new T305BulkDefaulted { Id = 1, Note = null, Label = null }])
            .BulkInsert()
            .Should().Be(1);
        Context.From<T305BulkDefaulted>().Where(x => x.Id == 1).Select(x => new { x.Label }).ToList()
            .Should().ContainSingle().Which.Label.Should().BeNull("an explicit true keeps the caller's NULL");

        Context.CreateBulkInsertBuilder<T305BulkDefaulted>(o => o.KeepNulls(false))
            .Values([new T305BulkDefaulted { Id = 2, Note = null, Label = null }])
            .BulkInsert()
            .Should().Be(1);
        Context.From<T305BulkDefaulted>().Where(x => x.Id == 2).Select(x => new { x.Label }).ToList()
            .Should().ContainSingle().Which.Label.Should().Be("dflt", "the driver default applies when the flag is off");
    }

    [Fact]
    public void ModePortable_KeepNullsFalse_ShouldAcceptTheDefaultEquivalentShapeAndRefuseAColumnDefault()
    {
        // Row 8 "P true/E": the portable INSERT ... VALUES statement always sends the supplied value, so
        // KeepNulls=false is honoured only where writing NULL is indistinguishable from the DEFAULT.
        T305Sql.Recreate(
            Context,
            "t305_bulk_all_nullable",
            "create table dbo.t305_bulk_all_nullable (id int null, note nvarchar(100) null)");
        T305Sql.Recreate(
            Context,
            "t305_bulk_defaulted_all_nullable",
            "create table dbo.t305_bulk_defaulted_all_nullable (id int null, label nvarchar(100) null default 'dflt')");

        Context.CreateBulkInsertBuilder<T305NullableRow>(o => o.Mode(BulkInsertMode.Portable).KeepNulls(false))
            .Values([new T305NullableRow { Id = 1, Note = null }])
            .BulkInsert()
            .Should().Be(1);
        Context.From<T305NullableRow>().Where(x => x.Id == 1).Select(x => new { x.Note }).ToList()
            .Should().ContainSingle().Which.Note.Should().BeNull("NULL is indistinguishable from the absent DEFAULT");

        var source = T305Sql.Counting([new T305DefaultedNullableRow { Id = 2, Label = null }]);
        var refused = () => Context.CreateBulkInsertBuilder<T305DefaultedNullableRow>(o => o.Mode(BulkInsertMode.Portable).KeepNulls(false))
            .Values(source)
            .BulkInsert();
        refused.Should().Throw<NotSupportedException>().WithMessage("*KeepNulls=false*label*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "the portable statement also always sends the supplied value, so a non-NULL SQL default would be "
            + "silently skipped: the request is refused before the write");
    }

    [Fact]
    public void Destination_CurrentDatabaseSchemaAndEqualAliases_ShouldWrite()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table dbo.t305_bulk_dest (id int not null primary key, name nvarchar(100) null)");
        var database = T305Sql.Scalar<string>(Context, "select db_name() as v");

        var qualified = Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                .Table("t305_bulk_dest")
                .Database(database)
                .Schema("dbo"))
            .Values(T305Sql.DestRows(51, 2))
            .BulkInsert();
        var aliases = Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                .Table("dbo", "t305_bulk_dest")
                .Schema("dbo"))
            .Values(T305Sql.DestRows(61, 2))
            .BulkInsert();

        qualified.Should().Be(2);
        aliases.Should().Be(2);
        Context.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(4);
    }

    [Fact]
    public void LocalTemporary_ShouldWriteIntoTheSameSessionTempTable()
    {
        var name = "#t305_temp_" + Guid.NewGuid().ToString("N");
        T305Sql.Execute(Context, $"create table {name} (id int not null, name nvarchar(100) null)");

        try
        {
            var written = Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                    .TargetKind(BulkInsertTargetKind.LocalTemporary)
                    .Table(name))
                .Values(T305Sql.DestRows(71, 3))
                .BulkInsert();

            written.Should().Be(3);
            _sut.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().HaveCount(3);
        }
        finally
        {
            T305Sql.Execute(Context, $"drop table if exists {name}");
        }
    }
}

/// <summary>
/// PostgreSQL cells: rows 3/5/6/7/8/9 and rows 13/14/16/17. The <c>KeepNulls=false</c> SQL-default half
/// is certified here against the live schema (D3 deferred it to D6).
/// </summary>
[Collection("Postgres")]
public sealed class T305PostgresBulkInsertTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    [Fact]
    public void KeepIdentityFalse_ShouldGenerateTheKeyInsteadOfWritingTheExplicitOne()
    {
        var marker = T305Sql.Marker("pg_keep_identity_false");
        var rows = Enumerable.Range(0, 3).Select(i => new InsertEntity { Id = 0, Name = marker, Age = i }).ToList();

        try
        {
            Context.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity(false)).Values(rows).BulkInsert().Should().Be(3);

            Context.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Id }).ToList()
                .Should().HaveCount(3).And.OnlyContain(row => row.Id > 0);
        }
        finally
        {
            Context.CreateDeleteBuilder<IInsertEntity>().Where(x => x.Name == marker).Delete();
        }
    }

    [Fact]
    public void KeepNullsFalse_OnTheDefaultEquivalentShape_ShouldPreserveNull()
    {
        // The default-equivalent shape (row 8, "E"): every written column carries a NULL input and has a
        // NULL/absent SQL default, so writing NULL is indistinguishable from omitting the column.
        T305Sql.Recreate(Context, "t305_bulk_all_nullable", "create table t305_bulk_all_nullable (id int null, note varchar(100) null)");

        var written = Context.CreateBulkInsertBuilder<T305NullableRow>(o => o.KeepNulls(false))
            .Values([new T305NullableRow { Id = 1, Note = null }])
            .BulkInsert();

        written.Should().Be(1);
        Context.From<T305NullableRow>().Where(x => x.Id == 1).Select(x => new { x.Note }).ToList()
            .Should().ContainSingle().Which.Note.Should().BeNull();
    }

    [Fact]
    public void KeepNullsFalse_WithAColumnDefault_ShouldBeRefusedBeforeTheSource()
    {
        // A non-NULL SQL default is not default-equivalent: COPY would write the caller's NULL instead of
        // the default, so the option would be accepted and inert. It is refused by name before the source.
        T305Sql.Recreate(
            Context,
            "t305_bulk_defaulted_all_nullable",
            "create table t305_bulk_defaulted_all_nullable (id int null, label varchar(100) null default 'dflt')");
        var source = T305Sql.Counting([new T305DefaultedNullableRow { Id = 1, Label = null }]);

        var act = () => Context.CreateBulkInsertBuilder<T305DefaultedNullableRow>(o => o.KeepNulls(false))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*KeepNulls=false*label*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "COPY cannot apply a per-row DEFAULT, so the request is refused before the write: the synchronous "
            + "terminal buffered the source for validation and no batch was sent");
    }

    [Fact]
    public void KeepNullsFalse_OnThePortablePath_ShouldApplyTheSameShapeGuard()
    {
        // Row 8 "P" half: the parameterised INSERT ... VALUES path always sends the supplied value too, so
        // the default-equivalent guard has to hold there exactly as it does on binary COPY.
        T305Sql.Recreate(Context, "t305_bulk_all_nullable", "create table t305_bulk_all_nullable (id int null, note varchar(100) null)");
        T305Sql.Recreate(
            Context,
            "t305_bulk_defaulted_all_nullable",
            "create table t305_bulk_defaulted_all_nullable (id int null, label varchar(100) null default 'dflt')");

        Context.CreateBulkInsertBuilder<T305NullableRow>(o => o.Mode(BulkInsertMode.Portable).KeepNulls(false))
            .Values([new T305NullableRow { Id = 1, Note = null }])
            .BulkInsert()
            .Should().Be(1);

        var source = T305Sql.Counting([new T305DefaultedNullableRow { Id = 2, Label = null }]);
        var refused = () => Context.CreateBulkInsertBuilder<T305DefaultedNullableRow>(o => o.Mode(BulkInsertMode.Portable).KeepNulls(false))
            .Values(source)
            .BulkInsert();
        refused.Should().Throw<NotSupportedException>().WithMessage("*KeepNulls=false*label*");
        Context.From<T305NullableRow>().Where(x => x.Id == 1).Select(x => new { x.Note }).ToList()
            .Should().ContainSingle().Which.Note.Should().BeNull();
    }

    [Fact]
    public void CheckConstraintsTrue_ShouldRejectTheViolatingRowAndFalseShouldBeRefused()
    {
        T305Sql.Recreate(
            Context,
            "t305_bulk_check",
            "create table t305_bulk_check (id int not null primary key, age int not null constraint ck_t305_bulk_check_age check (age >= 0))");

        var violating = () => Context.CreateBulkInsertBuilder<T305BulkCheck>(o => o.CheckConstraints(true))
            .Values([new T305BulkCheck { Id = 1, Age = -1 }])
            .BulkInsert();
        violating.Should().Throw<DbException>();

        var source = T305Sql.Counting([new T305BulkCheck { Id = 2, Age = -1 }]);
        var refused = () => Context.CreateBulkInsertBuilder<T305BulkCheck>(o => o.CheckConstraints(false))
            .Values(source)
            .BulkInsert();
        refused.Should().Throw<NotSupportedException>().WithMessage("*CheckConstraints=false*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void TableLockAndFireTriggersTrue_ShouldBeRefusedAndFalseShouldBeHonouredByAbsence()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table t305_bulk_dest (id int not null primary key, name varchar(100) null)");

        var locked = () => Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.TableLock(true))
            .Values(T305Sql.DestRows(1, 1))
            .BulkInsert();
        var firing = () => Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.FireTriggers(true))
            .Values(T305Sql.DestRows(2, 1))
            .BulkInsert();

        locked.Should().Throw<NotSupportedException>().WithMessage("*TableLock=true*");
        firing.Should().Throw<NotSupportedException>().WithMessage("*FireTriggers=true*");

        Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.TableLock(false).FireTriggers(false))
            .Values(T305Sql.DestRows(3, 2))
            .BulkInsert()
            .Should().Be(2);
        Context.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(2);
    }

    [Fact]
    public void Destination_SchemaAndCurrentDatabase_ShouldWriteAndAnotherDatabaseShouldBeRefused()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table t305_bulk_dest (id int not null primary key, name varchar(100) null)");
        var database = T305Sql.Scalar<string>(Context, "select current_database() as v");

        Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.Table("t305_bulk_dest").Schema("public").Database(database))
            .Values(T305Sql.DestRows(11, 2))
            .BulkInsert()
            .Should().Be(2);

        var source = T305Sql.Counting(T305Sql.DestRows(21, 1));
        var other = () => Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.Table("t305_bulk_dest").Database("another_database"))
            .Values(source)
            .BulkInsert();
        other.Should().Throw<NotSupportedException>().WithMessage("*DatabaseName*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "the synchronous terminal buffers the whole source for validation before it renders any SQL, so the "
            + "observable contract is 'refused before the write, source read exactly once': COPY never starts");
    }

    [Fact]
    public void LocalTemporary_ShouldWriteIntoTheSameSessionTempTable()
    {
        var name = "t305_temp_" + Guid.NewGuid().ToString("N");
        T305Sql.Execute(Context, $"create temporary table {name} (id int not null, name varchar(100) null) on commit preserve rows");

        try
        {
            var written = Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                    .TargetKind(BulkInsertTargetKind.LocalTemporary)
                    .Table(name))
                .Values(T305Sql.DestRows(31, 3))
                .BulkInsert();

            written.Should().Be(3);
            _sut.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().HaveCount(3);
        }
        finally
        {
            T305Sql.Execute(Context, $"drop table if exists {name}");
        }
    }

    [Fact]
    public void IgnoreDuplicatesFalse_ShouldFailOnTheConflictingKey()
    {
        var first = Random.Shared.Next(1_000_000, 2_000_000_000);
        int[] ids = [first];
        var marker = T305Sql.Marker("pg_ignore_false");

        try
        {
            Context.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity())
                .Values([new DeleteEntity { Id = first, Name = marker, Age = 1 }])
                .BulkInsert()
                .Should().Be(1);

            var act = () => Context.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity(false).IgnoreDuplicates(false))
                .Values([new DeleteEntity { Id = first, Name = marker, Age = 2 }])
                .BulkInsert();
            act.Should().Throw<DbException>("an explicit false does not skip the conflicting row");

            Context.From<IDeleteEntity>().Where(x => ids.Contains(x.Id)).Select(x => new { x.Age }).ToList()
                .Should().ContainSingle().Which.Age.Should().Be(1);
        }
        finally
        {
            Context.CreateDeleteBuilder<IDeleteEntity>().Where(x => ids.Contains(x.Id)).Delete();
        }
    }
}

/// <summary>
/// MySQL cells: row 2 (native LOCAL prerequisites), rows 3/5 (identity zero mode, ignore duplicates) and
/// rows 6/8/13/14/16/17.
/// </summary>
[Collection("MySql")]
public sealed class T305MySqlBulkInsertTests : ProviderTestSuite
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    [Fact]
    public void NativeMode_WithoutTheClientLocalPrerequisite_ShouldRefuseByNameAndAutoShouldWrite()
    {
        var marker = T305Sql.Marker("mysql_native");
        var source = T305Sql.Counting(T305Sql.InsertRows(marker, 3));

        var native = () => Context.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native))
            .Values(source)
            .BulkInsert();

        native.Should().Throw<NotSupportedException>().WithMessage("*AllowLoadLocalInfile*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "the synchronous terminal buffers the whole source for validation before it renders any SQL, so the "
            + "observable contract is 'refused before the write, source read exactly once': no LOAD is attempted");

        Context.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Auto))
            .Values(T305Sql.InsertRows(marker, 3))
            .BulkInsert()
            .Should().Be(3, "Auto falls back to the fully compatible portable path");
        Context.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList().Should().HaveCount(3);
    }

    [Fact]
    public void NativeMode_WithTheClientPrerequisiteAndADisabledServer_ShouldRefuseNamingLocalInfile()
    {
        // The client flag is enabled for this context only; the server value is read, never changed, and
        // the arm asserts whichever of the two documented outcomes the live server selects.
        var connectionString = MySqlContainer.ConnectionString + ";AllowLoadLocalInfile=true";
        using var ctx = new NextORM.MySql.MySqlDataContext(connectionString, new DataContextBuilder());
        var serverAllows = T305Sql.Scalar<int>(Context, "select cast(@@GLOBAL.local_infile as signed) as v") == 1;
        var marker = T305Sql.Marker("mysql_native_server");

        if (serverAllows)
        {
            ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native))
                .Values(T305Sql.InsertRows(marker, 2))
                .BulkInsert()
                .Should().Be(2, "both native prerequisites are present, so the native LOAD runs");
            ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList().Should().HaveCount(2);
        }
        else
        {
            var source = T305Sql.Counting(T305Sql.InsertRows(marker, 2));
            var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native))
                .Values(source)
                .BulkInsert();
            act.Should().Throw<NotSupportedException>().WithMessage("*local_infile*");
            source.Enumerated.Should().Be(
                source.RowCount,
                "the read-only server preflight refuses before the write; the synchronous terminal buffered the source "
                + "for whole-set validation first, and the source is never replayed");
        }
    }

    [Fact]
    public void KeepIdentityTrue_ShouldWriteTheZeroIdentityValueUnderTheSessionMode()
    {
        var marker = T305Sql.Marker("mysql_zero_identity");

        try
        {
            Context.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity())
                .Values([new InsertEntity { Id = 0, Name = marker, Age = 7 }])
                .BulkInsert()
                .Should().Be(1);

            Context.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Id }).ToList()
                .Should().ContainSingle().Which.Id.Should().Be(0, "NO_AUTO_VALUE_ON_ZERO keeps an explicit zero identity");
        }
        finally
        {
            Context.CreateDeleteBuilder<IInsertEntity>().Where(x => x.Name == marker).Delete();
        }
    }

    [Fact]
    public void IgnoreDuplicatesTrue_ShouldSkipAndFalseShouldFailOnTheConflictingKey()
    {
        var id = Random.Shared.Next(1_500_000_000, 1_900_000_000);
        int[] ids = [id];
        var marker = T305Sql.Marker("mysql_ignore");

        try
        {
            Context.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity())
                .Values([new DeleteEntity { Id = id, Name = marker, Age = 1 }])
                .BulkInsert()
                .Should().Be(1);

            Context.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity().IgnoreDuplicates(true))
                .Values([new DeleteEntity { Id = id, Name = marker, Age = 2 }])
                .BulkInsert()
                .Should().Be(0, "the conflicting row is skipped");
            Context.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Age }).ToList()
                .Should().ContainSingle().Which.Age.Should().Be(1);

            var explicitFalse = () => Context.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity(false).IgnoreDuplicates(false))
                .Values([new DeleteEntity { Id = id, Name = marker, Age = 3 }])
                .BulkInsert();
            explicitFalse.Should().Throw<DbException>();
        }
        finally
        {
            Context.CreateDeleteBuilder<IDeleteEntity>().Where(x => ids.Contains(x.Id)).Delete();
        }
    }

    [Fact]
    public void CheckConstraintsTrue_ShouldRejectTheViolatingRowAndFalseShouldBeRefused()
    {
        T305Sql.Recreate(
            Context,
            "t305_bulk_check",
            "create table t305_bulk_check (id int not null primary key, age int not null, constraint ck_t305_bulk_check_age check (age >= 0))");

        var violating = () => Context.CreateBulkInsertBuilder<T305BulkCheck>(o => o.CheckConstraints(true))
            .Values([new T305BulkCheck { Id = 1, Age = -1 }])
            .BulkInsert();
        violating.Should().Throw<DbException>();

        var source = T305Sql.Counting([new T305BulkCheck { Id = 2, Age = -1 }]);
        var refused = () => Context.CreateBulkInsertBuilder<T305BulkCheck>(o => o.CheckConstraints(false))
            .Values(source)
            .BulkInsert();
        refused.Should().Throw<NotSupportedException>().WithMessage("*CheckConstraints=false*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void KeepNullsFalse_ShouldAcceptTheDefaultEquivalentShapeAndRefuseAColumnDefault()
    {
        // The default-equivalent shape (row 8, "E"): every written column carries a NULL input and has a
        // NULL/absent SQL default, so writing NULL is indistinguishable from omitting the column.
        T305Sql.Recreate(Context, "t305_bulk_all_nullable", "create table t305_bulk_all_nullable (id int null, note varchar(100) null)");
        T305Sql.Recreate(
            Context,
            "t305_bulk_defaulted_all_nullable",
            "create table t305_bulk_defaulted_all_nullable (id int null, label varchar(100) null default 'dflt')");

        Context.CreateBulkInsertBuilder<T305NullableRow>(o => o.KeepNulls(false))
            .Values([new T305NullableRow { Id = 1, Note = null }])
            .BulkInsert()
            .Should().Be(1);
        Context.From<T305NullableRow>().Where(x => x.Id == 1).Select(x => new { x.Note }).ToList()
            .Should().ContainSingle().Which.Note.Should().BeNull();

        var source = T305Sql.Counting([new T305DefaultedNullableRow { Id = 2, Label = null }]);
        var refused = () => Context.CreateBulkInsertBuilder<T305DefaultedNullableRow>(o => o.KeepNulls(false))
            .Values(source)
            .BulkInsert();
        refused.Should().Throw<NotSupportedException>().WithMessage("*KeepNulls=false*label*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "the write always sends the supplied value, so a non-NULL SQL default would be silently skipped: the "
            + "request is refused before the write (the synchronous terminal buffered the source for validation)");
    }

    [Fact]
    public void Destination_CurrentDatabaseAndSchema_ShouldWrite()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table t305_bulk_dest (id int not null primary key, name varchar(100) null)");
        var database = T305Sql.Scalar<string>(Context, "select database() as v");

        Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.Table("t305_bulk_dest").Database(database))
            .Values(T305Sql.DestRows(1, 2))
            .BulkInsert()
            .Should().Be(2);
        Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.Table("t305_bulk_dest").Schema(database))
            .Values(T305Sql.DestRows(11, 2))
            .BulkInsert()
            .Should().Be(2);
        Context.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(4);
    }

    [Fact]
    public void LocalTemporary_ShouldWriteIntoTheSameSessionTempTable()
    {
        var name = "t305_temp_" + Guid.NewGuid().ToString("N");
        T305Sql.Execute(Context, $"create temporary table {name} (id int not null, name varchar(100) null)");

        try
        {
            Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                    .TargetKind(BulkInsertTargetKind.LocalTemporary)
                    .Table(name))
                .Values(T305Sql.DestRows(21, 3))
                .BulkInsert()
                .Should().Be(3);

            _sut.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().HaveCount(3);
        }
        finally
        {
            T305Sql.Execute(Context, $"drop temporary table if exists {name}");
        }
    }

    // ---------------------------------------------------------------- r3: rows 3/8/10 on the portable and bounded paths

    [Fact]
    public void ModePortable_KeepIdentityTrue_ShouldWriteTheZeroIdentityValueUnderTheScopedMode()
    {
        // Row 3 "P": the portable INSERT ... VALUES path runs the same session-scoped zero-mode as the native
        // path, and restores `sql_mode` once the write is finished.
        var marker = T305Sql.Marker("mysql_portable_zero");

        try
        {
            Context.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Portable).KeepIdentity())
                .Values([new InsertEntity { Id = 0, Name = marker, Age = 7 }])
                .BulkInsert()
                .Should().Be(1);

            Context.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Id }).ToList()
                .Should().ContainSingle().Which.Id.Should().Be(0, "NO_AUTO_VALUE_ON_ZERO keeps an explicit zero identity on the portable path");

            T305Sql.Scalar<string>(Context, "select @@session.sql_mode")
                .Should().NotContain("NO_AUTO_VALUE_ON_ZERO", "the scoped mode is restored after the write");

            // Negative arm: without the request no zero-mode is applied, so the identity is generated.
            var generatedMarker = T305Sql.Marker("mysql_portable_generated");
            try
            {
                Context.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Portable).KeepIdentity(false))
                    .Values([new InsertEntity { Id = 0, Name = generatedMarker, Age = 8 }])
                    .BulkInsert()
                    .Should().Be(1);

                Context.From<IInsertEntity>().Where(x => x.Name == generatedMarker).Select(x => new { x.Id }).ToList()
                    .Should().ContainSingle().Which.Id.Should().BePositive("the zero-mode is scoped to a KeepIdentity request only");
            }
            finally
            {
                Context.CreateDeleteBuilder<IInsertEntity>().Where(x => x.Name == generatedMarker).Delete();
            }
        }
        finally
        {
            Context.CreateDeleteBuilder<IInsertEntity>().Where(x => x.Name == marker).Delete();
        }
    }

    [Fact]
    public void ModePortable_KeepNullsFalse_ShouldAcceptTheDefaultEquivalentShapeAndRefuseAColumnDefault()
    {
        // Row 8 "P": the portable scope applies the same equivalence guard as the native load.
        T305Sql.Recreate(Context, "t305_bulk_all_nullable", "create table t305_bulk_all_nullable (id int null, note varchar(100) null)");
        T305Sql.Recreate(
            Context,
            "t305_bulk_defaulted_all_nullable",
            "create table t305_bulk_defaulted_all_nullable (id int null, label varchar(100) null default 'dflt')");

        Context.CreateBulkInsertBuilder<T305NullableRow>(o => o.Mode(BulkInsertMode.Portable).KeepNulls(false))
            .Values([new T305NullableRow { Id = 1, Note = null }])
            .BulkInsert()
            .Should().Be(1);
        Context.From<T305NullableRow>().Where(x => x.Id == 1).Select(x => new { x.Note }).ToList()
            .Should().ContainSingle().Which.Note.Should().BeNull();

        var source = T305Sql.Counting([new T305DefaultedNullableRow { Id = 2, Label = null }]);
        var refused = () => Context.CreateBulkInsertBuilder<T305DefaultedNullableRow>(o => o.Mode(BulkInsertMode.Portable).KeepNulls(false))
            .Values(source)
            .BulkInsert();
        refused.Should().Throw<NotSupportedException>().WithMessage("*KeepNulls=false*label*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "the portable statement also always sends the supplied value, so a non-NULL SQL default would be "
            + "silently skipped: the request is refused before the write");
    }

    [Fact]
    public async Task NativeBoundedCalls_ShouldSplitTheSourceAtMaxBatchSize()
    {
        // Row 10 "N": the native MySqlBulkCopy path really issues bounded calls. The source throws on the
        // fourth row, so the first bounded call (MaxBatchSize=3) must already be committed: a whole-source
        // buffer would leave the table empty.
        var serverAllows = T305Sql.Scalar<int>(Context, "select cast(@@GLOBAL.local_infile as signed) as v") == 1;
        var enabledHere = false;
        if (!serverAllows)
        {
            // The container's server value is part of the fixture, restored below; nextorm itself never
            // changes a client or server setting (A08).
            T305Sql.Execute(Context, "set global local_infile = 1");
            enabledHere = true;
        }

        var marker = T305Sql.Marker("mysql_bounded");
        try
        {
            var connectionString = MySqlContainer.ConnectionString + ";AllowLoadLocalInfile=true";
            using var ctx = new NextORM.MySql.MySqlDataContext(connectionString, new DataContextBuilder());

            var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native).MaxBatchSize(3))
                .Values(FailingAfter(marker, 3))
                .BulkInsertAsync(TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*t305 bounded source failure*");

            Context.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList()
                .Should().HaveCount(3, "the first bounded native call wrote exactly MaxBatchSize=3 rows before the source failed");
        }
        finally
        {
            Context.CreateDeleteBuilder<IInsertEntity>().Where(x => x.Name == marker).Delete();
            if (enabledHere)
                T305Sql.Execute(Context, "set global local_infile = 0");
        }
    }

    private static async IAsyncEnumerable<InsertEntity> FailingAfter(string marker, int okRows)
    {
        for (var i = 0; i < okRows; i++)
        {
            await Task.Yield();
            yield return new InsertEntity { Name = marker, Age = i };
        }

        throw new InvalidOperationException("t305 bounded source failure");
    }
}

/// <summary>
/// SQLite cells: row 1 (portable only), rows 5/6 (ignore and constraint scope), rows 13-17. SQLite has no
/// toggle for CHECK constraints; <c>CheckConstraints=false</c> is expressed through the scoped
/// <c>PRAGMA foreign_keys</c> outside an active transaction and refused inside one.
/// </summary>
[Collection("Sqlite")]
public sealed class T305SqliteBulkInsertTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    [Fact]
    public void KeepIdentityFalse_ShouldGenerateTheKeyInsteadOfWritingTheExplicitOne()
    {
        var marker = T305Sql.Marker("sqlite_keep_identity_false");
        var rows = Enumerable.Range(0, 3).Select(i => new InsertEntity { Id = 0, Name = marker, Age = i }).ToList();

        try
        {
            Context.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity(false)).Values(rows).BulkInsert().Should().Be(3);

            Context.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Id }).ToList()
                .Should().HaveCount(3).And.OnlyContain(row => row.Id > 0);
        }
        finally
        {
            Context.CreateDeleteBuilder<IInsertEntity>().Where(x => x.Name == marker).Delete();
        }
    }

    [Fact]
    public void IgnoreDuplicatesTrue_ShouldSkipAndFalseShouldFailOnTheConflictingKey()
    {
        var id = Random.Shared.Next(1_500_000_000, 1_900_000_000);
        int[] ids = [id];
        var marker = T305Sql.Marker("sqlite_ignore");

        try
        {
            Context.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity())
                .Values([new DeleteEntity { Id = id, Name = marker, Age = 1 }])
                .BulkInsert()
                .Should().Be(1);

            Context.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity().IgnoreDuplicates(true))
                .Values([new DeleteEntity { Id = id, Name = marker, Age = 2 }])
                .BulkInsert()
                .Should().Be(0);
            Context.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Age }).ToList()
                .Should().ContainSingle().Which.Age.Should().Be(1);

            var explicitFalse = () => Context.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity(false).IgnoreDuplicates(false))
                .Values([new DeleteEntity { Id = id, Name = marker, Age = 3 }])
                .BulkInsert();
            explicitFalse.Should().Throw<DbException>();
        }
        finally
        {
            Context.CreateDeleteBuilder<IDeleteEntity>().Where(x => ids.Contains(x.Id)).Delete();
        }
    }

    [Fact]
    public void CheckConstraintsFalse_OutsideATransaction_ShouldApplyTheScopedPragmaAndRestoreIt()
    {
        T305Sql.Recreate(Context, "t305_bulk_fk_parent", "create table t305_bulk_fk_parent (id integer not null primary key)");
        T305Sql.Recreate(
            Context,
            "t305_bulk_fk",
            "create table t305_bulk_fk (id integer not null primary key, parent_id integer not null references t305_bulk_fk_parent(id))");
        var before = T305Sql.Scalar<int>(Context, "pragma foreign_keys");

        var written = Context.CreateBulkInsertBuilder<T305BulkForeignKey>(o => o.CheckConstraints(false))
            .Values([new T305BulkForeignKey { Id = 1, ParentId = 999 }])
            .BulkInsert();

        written.Should().Be(1, "the scoped pragma turns foreign-key enforcement off for the write");
        T305Sql.Scalar<int>(Context, "pragma foreign_keys").Should().Be(before, "the session control is restored in finally");

        var enforced = () => Context.CreateBulkInsertBuilder<T305BulkForeignKey>()
            .Values([new T305BulkForeignKey { Id = 2, ParentId = 999 }])
            .BulkInsert();
        enforced.Should().Throw<DbException>("enforcement is back on after the scoped write");
    }

    [Fact]
    public void CheckConstraintsFalse_InsideAnActiveTransaction_ShouldBeRefusedBeforeTheSource()
    {
        using var transaction = ((ITransactionManager)Context).BeginTransaction();
        var source = T305Sql.Counting([new T305BulkCheck { Id = 1, Age = -1 }]);

        var act = () => Context.CreateBulkInsertBuilder<T305BulkCheck>(o => o.CheckConstraints(false))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*CheckConstraints=false*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "the synchronous terminal buffers the whole source for validation before it renders any SQL, so the "
            + "observable contract here is 'refused before the write, source read exactly once': the provider never "
            + "received a statement and the source is not replayed");
        transaction.Rollback();
    }

    [Fact]
    public void KeepNullsFalse_ShouldAcceptTheDefaultEquivalentShapeAndRefuseAColumnDefault()
    {
        // Row 8 "P/E": SQLite has only the parameterised path, and it always sends the supplied value, so
        // KeepNulls=false is honoured only for a default-equivalent shape and refused otherwise.
        T305Sql.Recreate(Context, "t305_bulk_all_nullable", "create table t305_bulk_all_nullable (id integer null, note text null)");
        T305Sql.Recreate(
            Context,
            "t305_bulk_defaulted_all_nullable",
            "create table t305_bulk_defaulted_all_nullable (id integer null, label text null default 'dflt')");

        Context.CreateBulkInsertBuilder<T305NullableRow>(o => o.KeepNulls(false))
            .Values([new T305NullableRow { Id = 1, Note = null }])
            .BulkInsert()
            .Should().Be(1);
        Context.From<T305NullableRow>().Where(x => x.Id == 1).Select(x => new { x.Note }).ToList()
            .Should().ContainSingle().Which.Note.Should().BeNull();

        var source = T305Sql.Counting([new T305DefaultedNullableRow { Id = 2, Label = null }]);
        var refused = () => Context.CreateBulkInsertBuilder<T305DefaultedNullableRow>(o => o.KeepNulls(false))
            .Values(source)
            .BulkInsert();
        refused.Should().Throw<NotSupportedException>().WithMessage("*KeepNulls=false*label*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "the request is refused before the write; the synchronous terminal buffered the source for validation first");
    }

    [Fact]
    public void CheckConstraintsTrue_ShouldRejectTheViolatingRow()
    {
        T305Sql.Recreate(
            Context,
            "t305_bulk_check",
            "create table t305_bulk_check (id integer not null primary key, age integer not null check (age >= 0))");

        var act = () => Context.CreateBulkInsertBuilder<T305BulkCheck>(o => o.CheckConstraints(true))
            .Values([new T305BulkCheck { Id = 1, Age = -1 }])
            .BulkInsert();

        act.Should().Throw<DbException>();
    }

    [Fact]
    public void Destination_MainDatabaseAndEqualAliases_ShouldWrite()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table t305_bulk_dest (id integer not null primary key, name text null)");

        Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.Table("t305_bulk_dest").Database("main"))
            .Values(T305Sql.DestRows(1, 2))
            .BulkInsert()
            .Should().Be(2);
        Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.Table("main", "t305_bulk_dest").Schema("main"))
            .Values(T305Sql.DestRows(11, 2))
            .BulkInsert()
            .Should().Be(2);
        Context.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(4);
    }

    [Fact]
    public void LocalTemporary_ShouldWriteIntoTheSameConnectionTempTable()
    {
        var name = "t305_temp_" + Guid.NewGuid().ToString("N");
        T305Sql.Execute(Context, $"create temp table {name} (id integer not null, name text null)");

        try
        {
            Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                    .TargetKind(BulkInsertTargetKind.LocalTemporary)
                    .Table(name))
                .Values(T305Sql.DestRows(21, 3))
                .BulkInsert()
                .Should().Be(3);

            _sut.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().HaveCount(3);
        }
        finally
        {
            T305Sql.Execute(Context, $"drop table if exists {name}");
        }
    }
}

/// <summary>
/// ClickHouse cells outside the settings/session file: rows 5/6/8 (ignore refusal, constraint flag,
/// KeepNulls guard), rows 13-14/16-17 (destination, temporary target).
/// </summary>
[Collection("ClickHouse")]
public sealed class T305ClickHouseBulkInsertTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    [Fact]
    public void IgnoreDuplicatesTrue_ShouldBeRefusedBeforeTheSource()
    {
        var source = T305Sql.Counting(T305Sql.InsertRows(T305Sql.Marker("ch_ignore"), 2));

        var act = () => Context.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates())
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*IgnoreDuplicates=true*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "ClickHouse has no unique constraint and no ignore form: the request is refused before the write, after the "
            + "synchronous terminal buffered the source for whole-set validation");
    }

    [Fact]
    public void CheckConstraintsTrue_ShouldBeAcceptedAndFalseShouldBeRefused()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table t305_bulk_dest (id Int32, name Nullable(String)) engine = MergeTree order by id");

        Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.CheckConstraints(true))
            .Values(T305Sql.DestRows(1, 2))
            .BulkInsert()
            .Should().Be(2);

        var source = T305Sql.Counting(T305Sql.DestRows(11, 1));
        var refused = () => Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.CheckConstraints(false))
            .Values(source)
            .BulkInsert();
        refused.Should().Throw<NotSupportedException>().WithMessage("*CheckConstraints=false*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void KeepNullsFalse_OnTheDefaultEquivalentShape_ShouldBeAccepted()
    {
        T305Sql.Recreate(
            Context,
            "t305_bulk_all_nullable",
            "create table t305_bulk_all_nullable (id Nullable(Int32), note Nullable(String)) engine = MergeTree order by tuple()");

        Context.CreateBulkInsertBuilder<T305NullableRow>(o => o.KeepNulls(false))
            .Values([new T305NullableRow { Id = 1, Note = null }, new T305NullableRow { Id = 2, Note = "x" }])
            .BulkInsert()
            .Should().Be(2);

        var read = Context.From<T305NullableRow>().Select(x => new { x.Id, x.Note }).ToList();
        read.Should().HaveCount(2);
        read.Should().ContainSingle(row => row.Id == 1 && row.Note == null);
    }

    [Fact]
    public void KeepNullsFalse_WithASqlDefaultColumn_ShouldBeRefused()
    {
        // Row 8: writing an explicit NULL is only equivalent to the destination DEFAULT when no DEFAULT
        // exists; the SQL-default half is certified against the live schema here.
        T305Sql.Recreate(
            Context,
            "t305_bulk_defaulted_all_nullable",
            "create table t305_bulk_defaulted_all_nullable (id Nullable(Int32), label Nullable(String) DEFAULT 'dflt') engine = MergeTree order by tuple()");
        var source = T305Sql.Counting([new T305DefaultedNullableRow { Id = 1, Label = null }]);

        var act = () => Context.CreateBulkInsertBuilder<T305DefaultedNullableRow>(o => o.KeepNulls(false))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*KeepNulls=false*label*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "writing an explicit NULL is not equivalent to the destination DEFAULT, so the request is refused before "
            + "the write, after the synchronous terminal buffered the source for validation");
    }

    [Fact]
    public void Destination_DatabaseAndSchema_ShouldWrite()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table t305_bulk_dest (id Int32, name Nullable(String)) engine = MergeTree order by id");
        var database = T305Sql.Scalar<string>(Context, "select currentDatabase() as v");

        Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.Table("t305_bulk_dest").Database(database))
            .Values(T305Sql.DestRows(1, 2))
            .BulkInsert()
            .Should().Be(2);
        Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.Table("t305_bulk_dest").Schema(database))
            .Values(T305Sql.DestRows(11, 2))
            .BulkInsert()
            .Should().Be(2);
        Context.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(4);
    }

    [Fact]
    public void LocalTemporary_OnASessionlessConnection_ShouldBeRefusedBeforeTheSource()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table t305_bulk_dest (id Int32, name Nullable(String)) engine = MergeTree order by id");
        var source = T305Sql.Counting(T305Sql.DestRows(1, 1));

        var act = () => Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o.TargetKind(BulkInsertTargetKind.LocalTemporary))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "a sessionless write cannot see a session-local temporary table: the request is refused before the write, "
            + "after the synchronous terminal buffered the source for validation");
    }

    [Fact]
    public void AliasOrMaterializedColumn_ShouldBeRefusedByNameOnBothPaths()
    {
        // Row 3 "N/P writable columns, else R": ClickHouse computes ALIAS/MATERIALIZED columns, so a mapping
        // that writes one is refused by name before any row is read, instead of failing inside the insert.
        T305Sql.Recreate(
            Context,
            "t305_bulk_alias",
            "create table t305_bulk_alias (id Int32, name Nullable(String), alias_name String ALIAS lower(name)) "
            + "engine = MergeTree order by id");

        var nativeSource = T305Sql.Counting([new T305AliasRow { Id = 1, Name = "a" }]);
        var native = () => Context.CreateBulkInsertBuilder<T305AliasRow>()
            .Values(nativeSource)
            .BulkInsert();
        native.Should().Throw<NotSupportedException>().WithMessage("*'alias_name'*ALIAS*");
        nativeSource.Enumerated.Should().Be(
            nativeSource.RowCount,
            "the refusal names the computed column before the write, after the synchronous terminal buffered the source");

        var portableSource = T305Sql.Counting([new T305AliasRow { Id = 2, Name = "b" }]);
        var portable = () => Context.CreateBulkInsertBuilder<T305AliasRow>(o => o.Mode(BulkInsertMode.Portable))
            .Values(portableSource)
            .BulkInsert();
        portable.Should().Throw<NotSupportedException>().WithMessage("*'alias_name'*ALIAS*");
        portableSource.Enumerated.Should().Be(portableSource.RowCount);

        Context.From<T305AliasRow>().Select(x => new { x.Id }).ToList().Should().BeEmpty("a refused write writes nothing");
    }
}

/// <summary>DDL/scalar helpers shared by the T305 provider classes.</summary>
internal static class T305Sql
{
    public static string Marker(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N");

    public static List<InsertEntity> InsertRows(string marker, int count) =>
        Enumerable.Range(0, count).Select(i => new InsertEntity { Name = marker, Age = i }).ToList();

    public static List<T305BulkDest> DestRows(int firstId, int count) =>
        Enumerable.Range(0, count).Select(i => new T305BulkDest { Id = firstId + i, Name = "row" }).ToList();

    public static void Execute(IDataContext context, string sql)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    public static T Scalar<T>(IDataContext context, string sql)
    {
        using var result = ((DataContext)context).ExecuteRaw(sql);
        return result.Read<T>().Single();
    }

    public static void Recreate(IDataContext context, string table, string createSql)
    {
        Execute(context, $"drop table if exists {table}");
        Execute(context, createSql);
    }

    /// <summary>Counts the rows nextorm pulled, proving a refusal happens before the source is read.</summary>
    public static CountingSource<T> Counting<T>(IEnumerable<T> rows) => new(rows);

    public sealed class CountingSource<T>(IEnumerable<T> rows) : IEnumerable<T>
    {
        private readonly IReadOnlyList<T> _rows = rows as IReadOnlyList<T> ?? rows.ToList();

        public int Enumerated { get; private set; }

        /// <summary>The number of rows the source holds, so a test can tell "read once" from "never read".</summary>
        public int RowCount => _rows.Count;

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var row in _rows)
            {
                Enumerated++;
                yield return row;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

[SqlTable("t305_bulk_check")]
public sealed class T305BulkCheck
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("age")]
    public int Age { get; set; }
}

[SqlTable("t305_bulk_trigger")]
public sealed class T305BulkTrigger
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("age")]
    public int? Age { get; set; }
}

[SqlTable("t305_bulk_defaults")]
public sealed class T305BulkDefaults
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

[SqlTable("t305_bulk_defaulted")]
public sealed class T305BulkDefaulted
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

[SqlTable("t305_bulk_dest")]
public interface IT305BulkDest
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string? Name { get; set; }
}

public sealed class T305BulkDest : IT305BulkDest
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

[SqlTable("t305_bulk_fk")]
public sealed class T305BulkForeignKey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}

/// <summary>An entity that maps a ClickHouse-computed (ALIAS/MATERIALIZED) column, which must be refused.</summary>
[SqlTable("t305_bulk_alias")]
public sealed class T305AliasRow
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("alias_name")]
    public string? AliasName { get; set; }
}

[SqlTable("t305_bulk_all_nullable")]
public sealed class T305NullableRow
{
    [Key]
    [Column("id")]
    public int? Id { get; set; }

    [Column("note")]
    public string? Note { get; set; }
}

[SqlTable("t305_bulk_defaulted_all_nullable")]
public sealed class T305DefaultedNullableRow
{
    [Key]
    [Column("id")]
    public int? Id { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}
