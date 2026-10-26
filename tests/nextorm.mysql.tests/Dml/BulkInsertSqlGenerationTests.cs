using System.Collections;
using FluentAssertions;
using MySqlConnector;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// SQL generation and offline path selection for the bulk-insert terminals on MySQL. MySQL has a native
/// <c>MySqlBulkCopy</c> path gated by read-only <c>AllowLoadLocalInfile</c>/<c>local_infile</c>
/// prerequisites; the live load itself is certified by the integration suite. These tests never open a
/// database: the port-1 connection strings fail fast, and the source counter proves which path ran.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    // Port 1 refuses immediately, so the only reachable failure is the connection/prerequisite one.
    private const string LocalInfileDisabledConnectionString =
        "Server=127.0.0.1;Port=1;Database=nextorm;User ID=nextorm;Password=nextorm;Connection Timeout=1";

    private const string LocalInfileEnabledConnectionString =
        LocalInfileDisabledConnectionString + ";AllowLoadLocalInfile=true";

    private static InsertEntity Row(string name, int age) => new() { Name = name, Age = age };

    /// <summary>A source that counts how many rows nextorm pulled, proving the path was chosen before the read.</summary>
    private sealed class CountingSource(IEnumerable<InsertEntity> items) : IEnumerable<InsertEntity>
    {
        public int Enumerated { get; private set; }

        public IEnumerator<InsertEntity> GetEnumerator()
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
    private sealed class CountingAsyncSource(IEnumerable<InsertEntity> items) : IAsyncEnumerable<InsertEntity>
    {
        public int Enumerated { get; private set; }

        public async IAsyncEnumerator<InsertEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default)
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
    public void BulkInsert_ShouldRenderMultiRowValues()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values([Row("a", 1), Row("b", 2)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (_utf8mb4 X'61', @p1), (_utf8mb4 X'62', @p3)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldIncludeIdentityColumnWithoutClause()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into insert_entity (id, name, age) values (@p0, _utf8mb4 X'61', @p2)");
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_ShouldUseInsertIgnore()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert ignore into insert_entity (name, age) values (_utf8mb4 X'61', @p1)");
    }

    [Fact]
    public void BulkInsert_ReturningKey_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();

        Action act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>().Values([Row("a", 1)]).ReturningKey<long>().ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void BulkInsert_TableOverrideWithSchema_ShouldQualifyTarget()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Table("staging", "bulk_target")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into staging.bulk_target (name, age) values (_utf8mb4 X'61', @p1)");
    }

    // ---------------------------------------------------------------- D4 native path selection (A08)

    [Fact]
    public void BulkInsert_Dialect_ShouldAdvertiseTheNativeBulkPath()
    {
        MySqlDialect.Instance.SupportsBulkCopy.Should().BeTrue();
    }

    [Fact]
    public async Task BulkInsert_ModeNative_WithoutClientLocalInfile_ShouldRefuseByNameBeforeReading()
    {
        using var ctx = new MySqlDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([Row("a", 1)]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native))
            .Values(source)
            .BulkInsertAsync();

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*AllowLoadLocalInfile*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void BulkInsert_ModeAuto_WithoutClientLocalInfile_ShouldFallBackToPortable()
    {
        using var ctx = new MySqlDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());
        var source = new CountingSource([Row("a", 1)]);

        Action act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Auto))
            .Values(source)
            .BulkInsert();

        // The portable path pulled and projected the row before touching the connection; the native path
        // would have refused or connected before reading it.
        act.Should().Throw<MySqlException>();
        source.Enumerated.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BulkInsert_ModeNative_WithClientLocalInfile_ShouldEnterTheNativePathBeforeReading()
    {
        using var ctx = new MySqlDataContext(LocalInfileEnabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([Row("a", 1)]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native))
            .Values(source)
            .BulkInsertAsync();

        // The client prerequisite is satisfied; the read-only server preflight reaches the unreachable
        // server without a single row being read, which is the native path.
        await act.Should().ThrowAsync<MySqlException>();
        source.Enumerated.Should().Be(0);
    }

    // ---------------------------------------------------------------- D4b native compatibility (rows 3/5)

    [Fact]
    public async Task BulkInsert_Auto_KeepIdentity_WithClientLocalInfile_ShouldEnterTheNativePathBeforeReading()
    {
        using var ctx = new MySqlDataContext(LocalInfileEnabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([Row("a", 1)]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity())
            .Values(source)
            .BulkInsertAsync();

        // Row 3: the native load can write explicit identity columns, so Auto keeps the native path; only
        // the unreachable server stops the call, and not a single row was read.
        await act.Should().ThrowAsync<MySqlException>();
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsert_Auto_IgnoreDuplicates_WithClientLocalInfile_ShouldEnterTheNativePathBeforeReading()
    {
        using var ctx = new MySqlDataContext(LocalInfileEnabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([Row("a", 1)]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates())
            .Values(source)
            .BulkInsertAsync();

        // Row 5: ConflictOption.Ignore expresses the request natively, so Auto does not degrade to portable.
        await act.Should().ThrowAsync<MySqlException>();
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsert_ModeNative_KeepIdentity_WithoutClientLocalInfile_ShouldRefuseByName()
    {
        using var ctx = new MySqlDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([Row("a", 1)]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native).KeepIdentity())
            .Values(source)
            .BulkInsertAsync();

        // The identity request is native-eligible now; the refusal is the read-only LOCAL prerequisite,
        // not the option.
        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*AllowLoadLocalInfile*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsert_Portable_KeepIdentity_ShouldApplyTheIdentitySessionScope()
    {
        using var ctx = new MySqlDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([Row("a", 1)]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Portable).KeepIdentity())
            .Values(source)
            .BulkInsertAsync();

        // Mode.Portable runs the provider-scoped contract too: the identity zero-mode is applied before the
        // first row is read, so the connection (not the row source) is what fails here.
        await act.Should().ThrowAsync<MySqlException>();
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsert_Portable_KeepNullsFalse_NonDefaultEquivalentShape_ShouldRefuseByName()
    {
        using var ctx = new MySqlDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());
        var source = new CountingAsyncSource([Row("a", 1)]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Portable).KeepNulls(false))
            .Values(source)
            .BulkInsertAsync();

        // Row 8: the shape guard belongs to the provider seam, so the portable path refuses it too.
        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*KeepNulls=false*age*");
        source.Enumerated.Should().Be(0);
    }

    // ---------------------------------------------------------------- D4 G4/G5 option semantics

    [Fact]
    public async Task BulkInsert_KeepNullsFalse_NonDefaultEquivalentShape_ShouldRefuseByName()
    {
        using var ctx = MySqlTestContext.Create();
        var source = new CountingAsyncSource([Row("a", 1)]);

        Func<Task> act = async () => await ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepNulls(false))
            .Values(source)
            .BulkInsertAsync();

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*KeepNulls=false*age*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void BulkInsert_KeepNullsFalse_DefaultEquivalentShape_ShouldReachTheNativePrerequisite()
    {
        using var ctx = new MySqlDataContext(LocalInfileDisabledConnectionString, new DataContextBuilder());

        Action act = () => ctx.CreateBulkInsertBuilder<INullableEntity>(o => o.Mode(BulkInsertMode.Native).KeepNulls(false))
            .Values([new NullableEntity { Name = "a" }])
            .BulkInsert();

        // The shape guard accepted the write (every written column is nullable); the next refusal is the
        // client LOCAL prerequisite, not KeepNulls.
        act.Should().Throw<NotSupportedException>().WithMessage("*AllowLoadLocalInfile*");
    }

    [Fact]
    public void BulkInsert_TableLock_ShouldRefuseByName()
    {
        using var ctx = MySqlTestContext.Create();

        Action act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TableLock())
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*TableLock*");
    }

    [Fact]
    public void BulkInsert_FireTriggers_ShouldRefuseByName()
    {
        using var ctx = MySqlTestContext.Create();

        Action act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.FireTriggers())
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*FireTriggers*");
    }

    [Fact]
    public void BulkInsert_CheckConstraintsFalse_ShouldBeRefusedOnMySql()
    {
        using var ctx = MySqlTestContext.Create();

        Action act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.CheckConstraints(false))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*CheckConstraints=false*");
    }

    // ---------------------------------------------------------------- D4 destination semantics (row 14)

    [Fact]
    public void BulkInsert_DatabaseName_ShouldRenderDatabaseQualifiedTarget()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Database("archive")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into archive.insert_entity (name, age) values (_utf8mb4 X'61', @p1)");
    }

    [Fact]
    public void BulkInsert_SchemaName_ShouldUseTheSameDatabaseQualifierAsDatabaseName()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Schema("archive")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into archive.insert_entity (name, age) values (_utf8mb4 X'61', @p1)");
    }

    [Fact]
    public void BulkInsert_ServerName_ShouldBeRefused()
    {
        using var ctx = MySqlTestContext.Create();

        Action act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Server("remote"))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*ServerName*");
    }

    [Fact]
    public void BulkInsert_LocalTemporary_ShouldRenderTheTargetWithoutDdl()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .Table("staging_rows"))
            .Values([Row("a", 1)])
            .ToSql();

        sql.Should().Be("insert into staging_rows (name, age) values (_utf8mb4 X'61', @p1)");
    }

    [Fact]
    public void BulkInsert_LocalTemporaryWithDatabase_ShouldBeRefused()
    {
        using var ctx = MySqlTestContext.Create();

        Action act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .Table("staging_rows")
                .Database("archive"))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*DatabaseName*");
    }
}
