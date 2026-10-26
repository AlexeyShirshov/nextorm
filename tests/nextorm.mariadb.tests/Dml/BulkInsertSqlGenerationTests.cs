using System.Collections;
using FluentAssertions;
using MySqlConnector;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// SQL generation and offline path selection for the bulk-insert terminals on MariaDB. MariaDB has a
/// native <c>MySqlBulkCopy</c> path gated by read-only <c>AllowLoadLocalInfile</c>/<c>local_infile</c>
/// prerequisites and its own scoped constraint controls; the live load is certified by the integration
/// suite. These tests never open a database: the port-1 connection strings fail fast, and the source
/// counter proves which path ran.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    // Port 1 refuses immediately, so the only reachable failure is the connection/prerequisite one.
    private const string LocalInfileDisabledConnectionString =
        "Server=127.0.0.1;Port=1;Database=nextorm;User ID=nextorm;Password=nextorm;Connection Timeout=1";

    private const string LocalInfileEnabledConnectionString =
        LocalInfileDisabledConnectionString + ";AllowLoadLocalInfile=true";

    /// <summary>A source that counts how many rows nextorm pulled, proving the path was chosen before the read.</summary>
    private sealed class CountingSource(IEnumerable<SimpleEntity> items) : IEnumerable<SimpleEntity>
    {
        public int Enumerated { get; private set; }

        public IEnumerator<SimpleEntity> GetEnumerator()
        {
            foreach (var item in items)
            {
                Enumerated++;
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>The async mirror: an async source is not materialised before the path is chosen.</summary>
    private sealed class CountingAsyncSource(IEnumerable<SimpleEntity> items) : IAsyncEnumerable<SimpleEntity>
    {
        public int Enumerated { get; private set; }

        public async IAsyncEnumerator<SimpleEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in items)
            {
                Enumerated++;
                yield return item;
                await Task.Yield();
            }
        }
    }

    /// <summary>The async mirror for the identity-bearing entity.</summary>
    private sealed class CountingNullableAsyncSource(IEnumerable<INullableEntity> items) : IAsyncEnumerable<INullableEntity>
    {
        public int Enumerated { get; private set; }

        public async IAsyncEnumerator<INullableEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in items)
            {
                Enumerated++;
                yield return item;
                await Task.Yield();
            }
        }
    }
    [Fact]
    public void BulkInsert_ShouldRenderExplicitValues()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<ISimpleEntity>().Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into simple_entity (id) values (@p0)");
    }

    [Fact]
    public void BulkInsert_TableOverrideWithSchema_ShouldQualifyTarget()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.Table("staging", "bulk_target")).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into staging.bulk_target (id) values (@p0)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldNotEmitIdentityInsertToggle()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.KeepIdentity()).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().NotContain("identity_insert");
    }

    // ---------------------------------------------------------------- D4 native path selection (A08)

    [Fact]
    public void BulkInsert_Dialect_ShouldAdvertiseTheNativeBulkPath()
    {
        MariaDbDialect.Instance.SupportsBulkCopy.Should().BeTrue();
    }

    [Fact]
    public async Task BulkInsert_ModeNative_WithoutClientLocalInfile_ShouldRefuseByNameBeforeReading()
    {
        using var ctx = new MariaDbDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([new SimpleEntity { Id = 7 }]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.Mode(BulkInsertMode.Native))
            .Values(source)
            .BulkInsertAsync();

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*AllowLoadLocalInfile*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void BulkInsert_ModeAuto_WithoutClientLocalInfile_ShouldFallBackToPortable()
    {
        using var ctx = new MariaDbDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());
        var source = new CountingSource([new SimpleEntity { Id = 7 }]);

        Action act = () => ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.Mode(BulkInsertMode.Auto))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<MySqlException>();
        source.Enumerated.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BulkInsert_ModeNative_WithClientLocalInfile_ShouldEnterTheNativePathBeforeReading()
    {
        using var ctx = new MariaDbDataContext(LocalInfileEnabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([new SimpleEntity { Id = 7 }]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.Mode(BulkInsertMode.Native))
            .Values(source)
            .BulkInsertAsync();

        await act.Should().ThrowAsync<MySqlException>();
        source.Enumerated.Should().Be(0);
    }

    // ---------------------------------------------------------------- D4b native compatibility (rows 3/5/6/8)

    [Fact]
    public async Task BulkInsert_Auto_KeepIdentity_WithClientLocalInfile_ShouldEnterTheNativePathBeforeReading()
    {
        using var ctx = new MariaDbDataContext(LocalInfileEnabledConnectionString, new DataContextBuilder());
        var source = new CountingNullableAsyncSource([new NullableEntity { Id = 7, Name = "a" }]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<INullableEntity>(o => o.KeepIdentity())
            .Values(source)
            .BulkInsertAsync();

        // Row 3: the native load writes the explicit identity column (with the zero-mode guard), so Auto
        // keeps the native path; only the unreachable server stops the call, before any row is read.
        await act.Should().ThrowAsync<MySqlException>();
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsert_Auto_IgnoreDuplicates_WithClientLocalInfile_ShouldEnterTheNativePathBeforeReading()
    {
        using var ctx = new MariaDbDataContext(LocalInfileEnabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([new SimpleEntity { Id = 7 }]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.IgnoreDuplicates())
            .Values(source)
            .BulkInsertAsync();

        // Row 5: ConflictOption.Ignore expresses the request natively.
        await act.Should().ThrowAsync<MySqlException>();
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsert_ModeNative_KeepIdentity_WithoutClientLocalInfile_ShouldRefuseByName()
    {
        using var ctx = new MariaDbDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());
        var source = new CountingNullableAsyncSource([new NullableEntity { Id = 7, Name = "a" }]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<INullableEntity>(o => o.Mode(BulkInsertMode.Native).KeepIdentity())
            .Values(source)
            .BulkInsertAsync();

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*AllowLoadLocalInfile*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsert_Portable_CheckConstraintsFalse_ShouldApplyTheScopedSessionContract()
    {
        using var ctx = new MariaDbDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([new SimpleEntity { Id = 7 }]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.Mode(BulkInsertMode.Portable).CheckConstraints(false))
            .Values(source)
            .BulkInsertAsync();

        // Row 6: Mode.Portable does not bypass the scoped session controls, so the disabling statements run
        // on the session before the first row is read (the unreachable server is what fails here).
        await act.Should().ThrowAsync<MySqlException>();
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsert_Portable_KeepIdentity_ShouldApplyTheIdentitySessionScope()
    {
        using var ctx = new MariaDbDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());
        var source = new CountingNullableAsyncSource([new NullableEntity { Id = 7, Name = "a" }]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<INullableEntity>(o => o.Mode(BulkInsertMode.Portable).KeepIdentity())
            .Values(source)
            .BulkInsertAsync();

        await act.Should().ThrowAsync<MySqlException>();
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsert_Portable_KeepNullsFalse_NonDefaultEquivalentShape_ShouldRefuseByName()
    {
        using var ctx = new MariaDbDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([new SimpleEntity { Id = 7 }]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.Mode(BulkInsertMode.Portable).KeepNulls(false))
            .Values(source)
            .BulkInsertAsync();

        // Row 8: the shape guard runs on the portable path too.
        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*KeepNulls=false*id*");
        source.Enumerated.Should().Be(0);
    }

    // ---------------------------------------------------------------- D4 G4/G5 option semantics

    [Fact]
    public async Task BulkInsert_KeepNullsFalse_NonDefaultEquivalentShape_ShouldRefuseByName()
    {
        using var ctx = MariaDbTestContext.Create();
        var source = new CountingAsyncSource([new SimpleEntity { Id = 7 }]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.KeepNulls(false))
            .Values(source)
            .BulkInsertAsync();

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*KeepNulls=false*id*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void BulkInsert_KeepNullsFalse_DefaultEquivalentShape_ShouldReachTheNativePrerequisite()
    {
        using var ctx = new MariaDbDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());

        Action act = () => ctx.CreateBulkInsertBuilder<INullableEntity>(o => o.Mode(BulkInsertMode.Native).KeepNulls(false))
            .Values([new NullableEntity { Name = "a" }])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*AllowLoadLocalInfile*");
    }

    [Fact]
    public void BulkInsert_CheckConstraintsFalse_ShouldBeAcceptedThroughScopedSessionControls()
    {
        using var ctx = new MariaDbDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());

        Action act = () => ctx.CreateBulkInsertBuilder<INullableEntity>(o => o.Mode(BulkInsertMode.Native).CheckConstraints(false))
            .Values([new NullableEntity { Name = "a" }])
            .BulkInsert();

        // MariaDB accepts the request; the next refusal is the missing read-only client LOCAL
        // prerequisite, not the constraint flag (the scoped session control is certified by the live suite).
        act.Should().Throw<NotSupportedException>().WithMessage("*AllowLoadLocalInfile*");
    }

    [Fact]
    public void BulkInsert_TableLock_ShouldRefuseByName()
    {
        using var ctx = MariaDbTestContext.Create();

        Action act = () => ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.TableLock())
            .Values([new SimpleEntity { Id = 7 }])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*TableLock*");
    }

    [Fact]
    public void BulkInsert_FireTriggers_ShouldRefuseByName()
    {
        using var ctx = MariaDbTestContext.Create();

        Action act = () => ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.FireTriggers())
            .Values([new SimpleEntity { Id = 7 }])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*FireTriggers*");
    }

    [Fact]
    public void BulkInsert_ReturningKey_ShouldBeRefused()
    {
        using var ctx = MariaDbTestContext.Create();

        Action act = () => ctx.CreateBulkInsertBuilder<ISimpleEntity>()
            .Values([new SimpleEntity { Id = 7 }])
            .ReturningKey<int>()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    // ---------------------------------------------------------------- D4 destination semantics (row 14)

    [Fact]
    public void BulkInsert_DatabaseName_ShouldRenderDatabaseQualifiedTarget()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.Database("archive")).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into archive.simple_entity (id) values (@p0)");
    }

    [Fact]
    public void BulkInsert_SchemaName_ShouldUseTheSameDatabaseQualifierAsDatabaseName()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.Schema("archive")).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into archive.simple_entity (id) values (@p0)");
    }

    [Fact]
    public void BulkInsert_ServerName_ShouldBeRefused()
    {
        using var ctx = MariaDbTestContext.Create();

        Action act = () => ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.Server("remote"))
            .Values([new SimpleEntity { Id = 7 }])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*ServerName*");
    }

    [Fact]
    public void BulkInsert_LocalTemporary_ShouldRenderTheTargetWithoutDdl()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .Table("staging_rows"))
            .Values([new SimpleEntity { Id = 7 }])
            .ToSql();

        sql.Should().Be("insert into staging_rows (id) values (@p0)");
    }

    [Fact]
    public void BulkInsert_LocalTemporaryWithDatabase_ShouldBeRefused()
    {
        using var ctx = MariaDbTestContext.Create();

        Action act = () => ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .Table("staging_rows")
                .Database("archive"))
            .Values([new SimpleEntity { Id = 7 }])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*DatabaseName*");
    }
}
