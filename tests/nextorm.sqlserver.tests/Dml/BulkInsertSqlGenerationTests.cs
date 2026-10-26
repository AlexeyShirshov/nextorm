using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// SQL generation for the bulk-insert terminals on SQL Server. These tests never open a database.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    private const string PlaceholderConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    private static InsertEntity Row(string name, int age) => new() { Name = name, Age = age };

    [Fact]
    public void BulkInsert_ShouldRenderMultiRowValues()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values([Row("a", 1), Row("b", 2)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldWrapWithIdentityInsert()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("set identity_insert insert_entity on; insert into insert_entity (id, name, age) values (@p0, @p1, @p2); set identity_insert insert_entity off");
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1)]).ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*skip conflicting rows*");
    }

    [Fact]
    public void BulkInsert_ReturningKey_ShouldEmitOutput()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values([Row("a", 1)]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert into insert_entity (name, age) output inserted.id values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_TableOverride_ShouldTargetOverriddenTable()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Table("bulk_target")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into bulk_target (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_TableOverrideWithSchema_ShouldQualifyTarget()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Table("staging", "bulk_target")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into staging.bulk_target (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_DatabaseAndSchema_ShouldQuoteEachPartSeparately()
    {
        using var ctx = SqlServerTestContext.CreateQuoted();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .Table("bulk_target")
                .Database("app_db")
                .Schema("staging"))
            .Values([Row("a", 1)])
            .ToSql();

        sql.Should().Be("insert into [app_db].[staging].[bulk_target] ([name], [age]) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_ServerName_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Server("remote")).Values([Row("a", 1)]).BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*ServerName*");
    }

    [Fact]
    public void BulkInsert_LocalTemporary_ShouldTargetTheSessionTempTableName()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .Table("#staging"))
            .Values([Row("a", 1)])
            .ToSql();

        sql.Should().Be("insert into #staging (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_LocalTemporary_WithSchema_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .Table("#staging")
                .Schema("staging"))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*SchemaName*");
    }

    [Fact]
    public void BulkInsert_TableOverride_KeepIdentity_ShouldWrapOverriddenTable()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Table("staging", "bulk_target").KeepIdentity()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("set identity_insert staging.bulk_target on; insert into staging.bulk_target (id, name, age) values (@p0, @p1, @p2); set identity_insert staging.bulk_target off");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_OnNonIdentityEntity_ShouldNotWrapWithIdentityInsert()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.KeepIdentity()).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into simple_entity (id) values (@p0)");
    }

    [Fact]
    public void MapBulkCopyOptions_ShouldSetEveryEnabledFlag()
    {
        var options = SqlServerDataContext.MapBulkCopyOptions(checkConstraints: true, tableLock: true, keepNulls: true, fireTriggers: true);

        options.Should().HaveFlag(SqlBulkCopyOptions.CheckConstraints);
        options.Should().HaveFlag(SqlBulkCopyOptions.TableLock);
        options.Should().HaveFlag(SqlBulkCopyOptions.KeepNulls);
        options.Should().HaveFlag(SqlBulkCopyOptions.FireTriggers);
    }

    [Fact]
    public void MapBulkCopyOptions_None_ShouldBeDefault()
    {
        var options = SqlServerDataContext.MapBulkCopyOptions(checkConstraints: false, tableLock: false, keepNulls: false, fireTriggers: false);

        options.Should().Be(SqlBulkCopyOptions.Default);
    }

    [Fact]
    public void BulkInsert_UseInternalTransactionTrue_ShouldMapTheDriverFlag()
    {
        var internalTransaction = SqlServerDataContext.MapBulkCopyOptions(
            checkConstraints: false, tableLock: false, keepNulls: false, fireTriggers: false, useInternalTransaction: true);

        internalTransaction.Should().HaveFlag(SqlBulkCopyOptions.UseInternalTransaction);

        var noInternalTransaction = SqlServerDataContext.MapBulkCopyOptions(
            checkConstraints: false, tableLock: false, keepNulls: false, fireTriggers: false, useInternalTransaction: false);

        noInternalTransaction.Should().NotHaveFlag(SqlBulkCopyOptions.UseInternalTransaction);
    }

    [Fact]
    public void BulkInsert_ExternalTransactionWithInternalTransaction_ShouldThrowNamingBoth()
    {
        using var transaction = new FakeDbTransaction();

        var act = () => SqlServerDataContext.ValidateInternalTransaction(useInternalTransaction: true, transaction);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*UseInternalTransaction=true*external transaction*FakeDbTransaction*");

        var withoutInternal = () => SqlServerDataContext.ValidateInternalTransaction(useInternalTransaction: false, transaction);
        withoutInternal.Should().NotThrow();
    }

    [Fact]
    public void BulkInsert_BatchSizeNull_ShouldStayTheDriverDefaultZero()
    {
        SqlServerDataContext.ResolveBatchSize(null).Should().Be(0);
        SqlServerDataContext.ResolveBatchSize(5).Should().Be(5);

        using var connection = new SqlConnection(PlaceholderConnectionString);
        using var bulk = SqlServerDataContext.CreateBulkCopy(
            connection,
            transaction: null,
            tableName: "[bulk_target]",
            columnNames: ["name"],
            bulkCopyTimeoutSeconds: 30,
            batchSize: SqlServerDataContext.ResolveBatchSize(maxBatchSize: null),
            progress: null,
            notifyEvery: 1,
            BulkCopyFlags.None,
            useInternalTransaction: false);

        bulk.BatchSize.Should().Be(0);
        bulk.BulkCopyTimeout.Should().Be(30);
        bulk.DestinationTableName.Should().Be("[bulk_target]");
    }

    [Fact]
    public void BulkInsert_Timeout_ShouldGiveTheDriverTheRemainingBudget()
    {
        SqlServerDataContext.ResolveBulkCopyTimeout(TimeSpan.FromSeconds(5.2), configuredSeconds: 9).Should().Be(6);
        SqlServerDataContext.ResolveBulkCopyTimeout(TimeSpan.FromMilliseconds(100), configuredSeconds: 9).Should().Be(1);
        SqlServerDataContext.ResolveBulkCopyTimeout(remaining: null, configuredSeconds: 9).Should().Be(9);
        SqlServerDataContext.ResolveBulkCopyTimeout(remaining: null, configuredSeconds: null).Should().Be(30);
    }

    [Fact]
    public void BulkInsert_BulkCopyFlag_ShouldNotChangeRenderedSql()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TableLock()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_Returning_WithBulkCopyFlag_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TableLock())
            .Values([Row("a", 1)])
            .ReturningKey<long>()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*TableLock*SqlBulkCopy*");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_WithBulkCopyFlag_ShouldStayOnTheNativePath()
    {
        using var ctx = SqlServerTestContext.Create();

        // Rows 3/9: the native path honours KeepIdentity (SqlBulkCopyOptions.KeepIdentity) and FireTriggers
        // (SqlBulkCopyOptions.FireTriggers), so the combination is not a portable-only contradiction any
        // more. Validation only, so no server is contacted.
        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native).KeepIdentity().FireTriggers())
            .Values([Row("a", 1)])
            .ValidateTarget();

        act.Should().NotThrow();
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldSetTheKeepIdentityBulkCopyFlag()
    {
        var options = SqlServerDataContext.MapBulkCopyOptions(
            checkConstraints: false,
            tableLock: false,
            keepNulls: false,
            fireTriggers: false,
            keepIdentity: true);

        options.Should().HaveFlag(SqlBulkCopyOptions.KeepIdentity);
    }

    [Fact]
    public void BulkInsert_ModeNative_KeepIdentity_ShouldSelectTheNativePath()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native).KeepIdentity())
            .Values([Row("a", 1)])
            .ValidateTarget();

        act.Should().NotThrow();
    }

    [Fact]
    public void BulkInsert_ModeNative_IgnoreDuplicates_ShouldThrowNamingTheOption()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native).IgnoreDuplicates())
            .Values([Row("a", 1)])
            .ValidateTarget();

        act.Should().Throw<NotSupportedException>().WithMessage("*SQL Server*IgnoreDuplicates=true*");
    }

    [Fact]
    public void BulkInsert_ModeAuto_IgnoreDuplicates_ShouldFallBackToThePortablePath()
    {
        using var ctx = SqlServerTestContext.Create();

        // Row 5: SqlBulkCopy has no conflict-skipping form, so Auto selects the portable statement, which
        // refuses the request by name because this provider has neither ignore form.
        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates())
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*IgnoreDuplicates=true*");
    }

    // ---------------------------------------------------------------- T305 D6 matrix shape cells (rows 1/6/8/13/16)

    [Fact]
    public void BulkInsert_RegularTarget_ShouldRenderTheSameMappedTable()
    {
        using var ctx = SqlServerTestContext.Create();
        InsertEntity[] rows = [Row("a", 1)];

        var explicitRegular = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TargetKind(BulkInsertTargetKind.Regular)).Values(rows).ToSql();

        explicitRegular.Should().Be(ctx.CreateBulkInsertBuilder<IInsertEntity>().Values(rows).ToSql());
    }

    [Fact]
    public void BulkInsert_SchemaOnlyAndEqualAliases_ShouldQualifyTheMappedTableOnce()
    {
        using var ctx = SqlServerTestContext.Create();
        InsertEntity[] rows = [Row("a", 1)];

        var schemaOnly = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Schema("dbo")).Values(rows).ToSql();
        var aliases = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Table("dbo", "insert_entity").Schema("dbo")).Values(rows).ToSql();

        schemaOnly.Should().Be("insert into dbo.insert_entity (name, age) values (@p0, @p1)");
        aliases.Should().Be(schemaOnly);
    }

    [Fact]
    public void BulkInsert_KeepNullsTrueAndFalse_ShouldNotChangeTheRenderedSql()
    {
        using var ctx = SqlServerTestContext.Create();
        InsertEntity[] rows = [Row("a", 1)];
        var plain = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values(rows).ToSql();

        ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepNulls(true)).Values(rows).ToSql().Should().Be(plain);
        ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepNulls(false)).Values(rows).ToSql().Should().Be(plain);
    }

    [Fact]
    public void BulkInsert_CheckConstraintsFalse_OnThePortablePath_ShouldBeRefusedByName()
    {
        using var ctx = SqlServerTestContext.Create();

        // Row 6: only the native SqlBulkCopy flag can turn constraint checking off.
        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Portable).CheckConstraints(false))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*CheckConstraints=false*");
    }

    [Fact]
    public void BulkInsert_ModePortable_KeepIdentity_ShouldKeepTheIdentityInsertWrapper()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Portable).KeepIdentity())
            .Values([Row("a", 1)])
            .ToSql();

        sql.Should().Contain("set identity_insert").And.Contain("insert into insert_entity (id, name, age)");
    }

    [Fact]
    public void BulkInsert_ModeAuto_PlainWrite_ShouldBeACompatibleNativeRequest()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Auto))
            .Values([Row("a", 1)])
            .ValidateTarget();

        act.Should().NotThrow("row 1: Auto prefers the native path for a request with no incompatible option");
    }
}

/// <summary>A caller-owned transaction stand-in, so the internal-transaction conflict is testable offline.</summary>
internal sealed class FakeDbTransaction : DbTransaction
{
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

    protected override DbConnection? DbConnection => null;

    public override void Commit()
    {
    }

    public override void Rollback()
    {
    }
}
