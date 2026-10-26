using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Builder-level validation of the bulk-insert terminal. The in-memory context is read-only, so any
/// terminal that needs the database throws; no database is involved.
/// </summary>
public partial class BulkInsertBuilderTests
{
    [Fact]
    public void NoValues_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>().BulkInsert();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Values*");
    }

    [Fact]
    public void ValuesTwice_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>().Values([new BulkEntity()]).Values([new BulkEntity()]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InMemory_BulkInsert_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>().Values([new BulkEntity { Id = 1, Name = "a" }]).BulkInsert();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task InMemory_BulkInsertAsync_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>().Values([new BulkEntity { Id = 1, Name = "a" }]).BulkInsertAsync();

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void InMemory_ToSql_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>().Values([new BulkEntity { Id = 1, Name = "a" }]).ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SyncTerminal_WithAsyncSource_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>().Values(EmptyAsync()).BulkInsert();

        act.Should().Throw<InvalidOperationException>().WithMessage("*async*");
    }

    [Fact]
    public void NonPositiveBatchSize_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.MaxBatchSize(0));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void InvalidOptions_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(new BulkInsertOptions { MaxBatchSize = 0 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TableOverride_EmptyTable_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Table(string.Empty));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TableOverride_SchemaWithoutTable_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(new BulkInsertOptions { TableSchema = "staging" });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TableOverride_TwoArg_EmptySchema_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Table(string.Empty, "bulk_target"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InMemory_ReturningKey_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>().Values([new BulkEntity { Id = 1, Name = "a" }]).ReturningKey<int>().ToList();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void OptionsBuilder_BulkCopyFlags_ShouldBuild()
    {
        var options = new BulkInsertOptionsBuilder()
            .CheckConstraints()
            .TableLock()
            .KeepNulls()
            .FireTriggers()
            .Build();

        options.CheckConstraints.Should().BeTrue();
        options.TableLock.Should().BeTrue();
        options.KeepNulls.Should().BeTrue();
        options.FireTriggers.Should().BeTrue();
    }

    [Fact]
    public void OptionsBuilder_BulkCopyFlags_ShouldDefaultToNull()
    {
        var options = new BulkInsertOptionsBuilder().Build();

        options.CheckConstraints.Should().BeNull();
        options.TableLock.Should().BeNull();
        options.KeepNulls.Should().BeNull();
        options.FireTriggers.Should().BeNull();
    }

    [Fact]
    public void OptionsBuilder_BulkCopyFlags_CanBeDisabledExplicitly()
    {
        var options = new BulkInsertOptionsBuilder().TableLock(false).Build();

        options.TableLock.Should().BeFalse();
    }

    [Fact]
    public void BulkCopyFlags_None_ShouldBeEmpty()
    {
        BulkCopyFlags.None.IsAny.Should().BeFalse();
        new BulkCopyFlags(CheckConstraints: true, TableLock: false, KeepNulls: false, FireTriggers: false).IsAny.Should().BeTrue();
    }

    [Fact]
    public void Options_Defaults_ShouldBeAutoRegularAndUnset()
    {
        var options = new BulkInsertOptions();

        options.Mode.Should().Be(BulkInsertMode.Auto);
        options.TargetKind.Should().Be(BulkInsertTargetKind.Regular);
        options.SqlServer.Should().BeNull();
        options.ClickHouse.Should().BeNull();
        options.Timeout.Should().BeNull();
        options.SchemaName.Should().BeNull();
        options.DatabaseName.Should().BeNull();
        options.ServerName.Should().BeNull();
    }

    [Fact]
    public void OptionsBuilder_ShouldPropagateModeTargetKindDestinationGroupsAndTimeout()
    {
        var sqlServer = new SqlServerBulkInsertOptions { UseInternalTransaction = false };
        var clickHouse = new ClickHouseBulkInsertOptions { MaxDegreeOfParallelism = 2, UseSession = true };

        var options = new BulkInsertOptionsBuilder()
            .Mode(BulkInsertMode.Portable)
            .TargetKind(BulkInsertTargetKind.LocalTemporary)
            .Schema("staging")
            .Database("app")
            .Server("primary")
            .SqlServer(sqlServer)
            .ClickHouse(clickHouse)
            .Timeout(TimeSpan.FromSeconds(5))
            .Build();

        options.Mode.Should().Be(BulkInsertMode.Portable);
        options.TargetKind.Should().Be(BulkInsertTargetKind.LocalTemporary);
        options.SchemaName.Should().Be("staging");
        options.DatabaseName.Should().Be("app");
        options.ServerName.Should().Be("primary");
        options.SqlServer.Should().BeSameAs(sqlServer);
        options.ClickHouse.Should().BeSameAs(clickHouse);
        options.Timeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void KeepIdentity_ExplicitFalse_ShouldBeDistinguishableFromAbsent()
    {
        var absent = new BulkInsertOptions();
        var explicitFalse = new BulkInsertOptions { KeepIdentity = false };

        absent.KeepIdentitySpecified.Should().BeFalse();
        explicitFalse.KeepIdentitySpecified.Should().BeTrue();
        explicitFalse.KeepIdentity.Should().BeFalse();
    }

    [Fact]
    public void IgnoreDuplicates_ExplicitFalse_ShouldBeDistinguishableFromAbsent()
    {
        var absent = new BulkInsertOptions();
        var explicitFalse = new BulkInsertOptions { IgnoreDuplicates = false };

        absent.IgnoreDuplicatesSpecified.Should().BeFalse();
        explicitFalse.IgnoreDuplicatesSpecified.Should().BeTrue();
        explicitFalse.IgnoreDuplicates.Should().BeFalse();
    }

    [Fact]
    public void NullableFlags_ExplicitFalse_ShouldBeDistinguishableFromAbsent()
    {
        var absent = new BulkInsertOptions();
        var explicitFalse = new BulkInsertOptions
        {
            CheckConstraints = false,
            TableLock = false,
            KeepNulls = false,
            FireTriggers = false,
        };

        absent.CheckConstraintsSpecified.Should().BeFalse();
        absent.TableLockSpecified.Should().BeFalse();
        absent.KeepNullsSpecified.Should().BeFalse();
        absent.FireTriggersSpecified.Should().BeFalse();

        explicitFalse.CheckConstraintsSpecified.Should().BeTrue();
        explicitFalse.TableLockSpecified.Should().BeTrue();
        explicitFalse.KeepNullsSpecified.Should().BeTrue();
        explicitFalse.FireTriggersSpecified.Should().BeTrue();
        explicitFalse.CheckConstraints.Should().BeFalse();
    }

    [Fact]
    public void OptionsBuilder_UnsetFlags_ShouldCarryNoPresence()
    {
        var options = new BulkInsertOptionsBuilder().Build();

        options.KeepIdentitySpecified.Should().BeFalse();
        options.IgnoreDuplicatesSpecified.Should().BeFalse();
        options.CheckConstraintsSpecified.Should().BeFalse();
        options.TableLockSpecified.Should().BeFalse();
        options.KeepNullsSpecified.Should().BeFalse();
        options.FireTriggersSpecified.Should().BeFalse();
    }

    [Fact]
    public void OptionsBuilder_ExplicitFalseFlags_ShouldCarryPresence()
    {
        var options = new BulkInsertOptionsBuilder()
            .KeepIdentity(false)
            .IgnoreDuplicates(false)
            .CheckConstraints(false)
            .TableLock(false)
            .KeepNulls(false)
            .FireTriggers(false)
            .Build();

        options.KeepIdentity.Should().BeFalse();
        options.KeepIdentitySpecified.Should().BeTrue();
        options.IgnoreDuplicatesSpecified.Should().BeTrue();
        options.CheckConstraintsSpecified.Should().BeTrue();
        options.TableLockSpecified.Should().BeTrue();
        options.KeepNullsSpecified.Should().BeTrue();
        options.FireTriggersSpecified.Should().BeTrue();
    }

    [Fact]
    public void EmptyTypedGroups_ShouldCreateNoRequest()
    {
        var options = new BulkInsertOptionsBuilder()
            .SqlServer(new SqlServerBulkInsertOptions())
            .ClickHouse(new ClickHouseBulkInsertOptions())
            .Build();

        options.SqlServer!.RequestsAny.Should().BeFalse();
        options.ClickHouse!.RequestsAny.Should().BeFalse();

        var act = () => options.ValidateForProvider(BulkInsertProvider.Postgres);

        act.Should().NotThrow();
    }

    [Fact]
    public void ForeignSqlServerGroup_ExplicitFalse_ShouldThrow()
    {
        var options = new BulkInsertOptions
        {
            SqlServer = new SqlServerBulkInsertOptions { UseInternalTransaction = false },
        };

        var act = () => options.ValidateForProvider(BulkInsertProvider.Postgres);

        act.Should().Throw<NotSupportedException>().WithMessage("*SqlServer*");
    }

    [Fact]
    public void OwnSqlServerGroup_ShouldBeAccepted()
    {
        var options = new BulkInsertOptions
        {
            SqlServer = new SqlServerBulkInsertOptions { UseInternalTransaction = false },
        };

        var act = () => options.ValidateForProvider(BulkInsertProvider.SqlServer);

        act.Should().NotThrow();
    }

    [Fact]
    public void ForeignClickHouseGroup_ExplicitFalse_ShouldThrow()
    {
        var options = new BulkInsertOptions
        {
            ClickHouse = new ClickHouseBulkInsertOptions { UseSession = false },
        };

        var act = () => options.ValidateForProvider(BulkInsertProvider.Postgres);

        act.Should().Throw<NotSupportedException>().WithMessage("*ClickHouse*");
    }

    [Fact]
    public void Timeout_TimeSpanOverload_ShouldDriveTheValue()
    {
        var options = new BulkInsertOptionsBuilder().Timeout(TimeSpan.FromMilliseconds(2500)).Build();

        options.Timeout.Should().Be(TimeSpan.FromMilliseconds(2500));
    }

    [Fact]
    public void Timeout_IntOverload_ShouldBeRemoved()
    {
        var intOverloads = typeof(BulkInsertOptionsBuilder)
            .GetMethods()
            .Where(m => m.Name == "Timeout"
                && m.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(int));

        intOverloads.Should().BeEmpty();
    }

    [Fact]
    public void Options_NonPositiveTimeout_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var direct = () => ctx.CreateBulkInsertBuilder<BulkEntity>(new BulkInsertOptions { Timeout = TimeSpan.Zero });
        var built = () => ctx.CreateBulkInsertBuilder<BulkEntity>((BulkInsertOptionsBuilder o) => o.Timeout(TimeSpan.FromSeconds(-1)));

        direct.Should().Throw<ArgumentOutOfRangeException>();
        built.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Options_NonPositiveMaxBatchSizeParametersOrSqlLength_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var maxBatchSize = () => ctx.CreateBulkInsertBuilder<BulkEntity>(new BulkInsertOptions { MaxBatchSize = 0 });
        var maxParameters = () => ctx.CreateBulkInsertBuilder<BulkEntity>(new BulkInsertOptions { MaxParameters = 0 });
        var maxSqlLength = () => ctx.CreateBulkInsertBuilder<BulkEntity>(new BulkInsertOptions { MaxSqlLength = -1 });
        var builderMaxBatchSize = () => new BulkInsertOptionsBuilder().MaxBatchSize(0);

        maxBatchSize.Should().Throw<ArgumentOutOfRangeException>();
        maxParameters.Should().Throw<ArgumentOutOfRangeException>();
        maxSqlLength.Should().Throw<ArgumentOutOfRangeException>();
        builderMaxBatchSize.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Options_UndefinedModeOrTargetKind_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var mode = () => ctx.CreateBulkInsertBuilder<BulkEntity>(new BulkInsertOptions { Mode = (BulkInsertMode)42 });
        var targetKind = () => ctx.CreateBulkInsertBuilder<BulkEntity>(new BulkInsertOptions { TargetKind = (BulkInsertTargetKind)42 });
        var builderMode = () => ctx.CreateBulkInsertBuilder<BulkEntity>((BulkInsertOptionsBuilder o) => o.Mode((BulkInsertMode)42));
        var builderTargetKind = () => ctx.CreateBulkInsertBuilder<BulkEntity>((BulkInsertOptionsBuilder o) => o.TargetKind((BulkInsertTargetKind)42));

        mode.Should().Throw<ArgumentException>();
        targetKind.Should().Throw<ArgumentException>();
        builderMode.Should().Throw<ArgumentException>();
        builderTargetKind.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Options_EmptyDestinationPart_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var schema = () => ctx.CreateBulkInsertBuilder<BulkEntity>(new BulkInsertOptions { TableName = "bulk_target", SchemaName = " " });
        var database = () => ctx.CreateBulkInsertBuilder<BulkEntity>(new BulkInsertOptions { TableName = "bulk_target", DatabaseName = string.Empty });
        var server = () => ctx.CreateBulkInsertBuilder<BulkEntity>(new BulkInsertOptions { TableName = "bulk_target", ServerName = "\t" });

        schema.Should().Throw<ArgumentException>();
        database.Should().Throw<ArgumentException>();
        server.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Destination_ShouldQuoteEachQualificationPartIndependently()
    {
        var destination = BulkInsertDestination.From(new BulkInsertOptions { DatabaseName = "app", SchemaName = "staging" });

        destination.Render(NextORM.SqlServer.SqlServerDialect.Instance, quoteIdentifiers: true, "orders")
            .Should().Be("[app].[staging].[orders]");
        destination.Render(NextORM.SqlServer.SqlServerDialect.Instance, quoteIdentifiers: false, "orders")
            .Should().Be("app.staging.orders");
    }

    [Fact]
    public void Destination_WithoutOptions_ShouldLeaveTheTableReferenceUnchanged()
    {
        var destination = BulkInsertDestination.From(new BulkInsertOptions());

        destination.IsEmpty.Should().BeTrue();
        destination.Render(NextORM.SqlServer.SqlServerDialect.Instance, quoteIdentifiers: true, "orders")
            .Should().Be("[orders]");
    }

    [Fact]
    public void Destination_SchemaOnly_ShouldResolveAgainstTheMappedTableName()
    {
        var independent = BulkInsertDestination.From(new BulkInsertOptions { SchemaName = "staging" });
        var legacyAlias = BulkInsertDestination.From(new BulkInsertOptions { TableSchema = "staging" });

        independent.Table.Should().BeNull();
        independent.Render(NextORM.Sqlite.SqliteDialect.Instance, quoteIdentifiers: false, "orders")
            .Should().Be("staging.orders");
        legacyAlias.Render(NextORM.Sqlite.SqliteDialect.Instance, quoteIdentifiers: false, "orders")
            .Should().Be("staging.orders");
    }

    [Fact]
    public void Destination_EqualSchemaAliases_ShouldCollapseToOnePart()
    {
        var destination = BulkInsertDestination.From(new BulkInsertOptions { TableSchema = "staging", SchemaName = "staging" });

        destination.Schema.Should().Be("staging");
        destination.Render(NextORM.Sqlite.SqliteDialect.Instance, quoteIdentifiers: false, "orders")
            .Should().Be("staging.orders");
    }

    [Fact]
    public void Destination_ConflictingSchemaAliases_ShouldThrowBeforeInput()
    {
        var act = () => BulkInsertDestination.From(new BulkInsertOptions { TableSchema = "staging", SchemaName = "archive" });

        act.Should().Throw<ArgumentException>().WithMessage("*TableSchema*SchemaName*");
    }

    [Fact]
    public void Destination_ServerName_ShouldBeRefused()
    {
        var act = () => BulkInsertDestination.From(new BulkInsertOptions { ServerName = "primary" });

        act.Should().Throw<NotSupportedException>().WithMessage("*ServerName*");
    }

    [Fact]
    public void Destination_UnsupportedDialectQualification_ShouldThrow()
    {
        var destination = BulkInsertDestination.From(new BulkInsertOptions { DatabaseName = "other" });

        var act = () => destination.Render(NextORM.Postgres.PostgresDialect.Instance, quoteIdentifiers: true, "orders");

        act.Should().Throw<NotSupportedException>().WithMessage("*DatabaseName*");
    }

    // ---------------------------------------------------------------- D1 mode dispatch

    [Fact]
    public void ModeAuto_OnNativeCapableContext_ShouldPreferTheNativePath()
    {
        using var ctx = new NativeCapableContext();

        var count = ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Mode(BulkInsertMode.Auto))
            .Values([new BulkEntity { Id = 1, Name = "a" }, new BulkEntity { Id = 2, Name = "b" }])
            .BulkInsert();

        count.Should().Be(2);
        ctx.NativeCalls.Should().Be(1);
    }

    [Fact]
    public void ModeNative_OnNativeCapableContext_ShouldWriteThroughTheNativePath()
    {
        using var ctx = new NativeCapableContext();

        var count = ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Mode(BulkInsertMode.Native))
            .Values([new BulkEntity { Id = 1, Name = "a" }])
            .BulkInsert();

        count.Should().Be(1);
        ctx.NativeCalls.Should().Be(1);
    }

    [Fact]
    public void ModePortable_OnNativeCapableContext_ShouldNotCallTheNativePath()
    {
        using var ctx = new NativeCapableContext();
        var builder = ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Mode(BulkInsertMode.Portable))
            .Values([new BulkEntity { Id = 1, Name = "a" }]);

        // The portable path is the parameterised INSERT loop: the fake context has no connection, so the
        // write stops at the connection, which proves the native hook was never entered.
        var act = () => builder.BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*t305-fake-connection*");
        ctx.NativeCalls.Should().Be(0);
    }

    [Fact]
    public void ModeNative_OnPortableOnlyContext_ShouldThrowBeforeReadingTheSource()
    {
        using var ctx = new PortableOnlyContext();
        var source = new CountingSource(new BulkEntity { Id = 1, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Mode(BulkInsertMode.Native))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*Mode.Native*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public async Task ModeNative_OnPortableOnlyContextAsync_ShouldThrowBeforeReadingTheSource()
    {
        using var ctx = new PortableOnlyContext();
        var source = new CountingSource(new BulkEntity { Id = 1, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Mode(BulkInsertMode.Native))
            .Values(source)
            .BulkInsertAsync();

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*Mode.Native*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void ModeNative_WithIgnoreDuplicates_ShouldThrowNamingthePortableOnlyOption()
    {
        using var ctx = new NativeCapableContext();
        var source = new CountingSource(new BulkEntity { Id = 1, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Mode(BulkInsertMode.Native).IgnoreDuplicates())
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*IgnoreDuplicates*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void ReturningKey_WithModeNative_ShouldThrowBeforeReadingTheSource()
    {
        using var ctx = new InMemoryDataContext();
        var source = new CountingSource(new BulkEntity { Id = 1, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Mode(BulkInsertMode.Native))
            .Values(source)
            .ReturningKey<int>()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Mode.Native*");
        source.Enumerated.Should().Be(0);
    }

    // ---------------------------------------------------------------- D1 provider-scoped groups

    [Fact]
    public void ForeignSqlServerGroup_ShouldBeRejectedBeforeReadingTheSource()
    {
        using var ctx = new InMemoryDataContext();
        var source = new CountingSource(new BulkEntity { Id = 1, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(
                o => o.SqlServer(new SqlServerBulkInsertOptions { UseInternalTransaction = false }))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*SqlServer*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void ForeignClickHouseGroup_ShouldBeRejectedBeforeReadingTheSource()
    {
        using var ctx = new InMemoryDataContext();
        var source = new CountingSource(new BulkEntity { Id = 1, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(
                o => o.ClickHouse(new ClickHouseBulkInsertOptions { UseSession = false }))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*ClickHouse*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void EmptyTypedGroups_OnUnknownProvider_ShouldRequestNothing()
    {
        var options = new BulkInsertOptions
        {
            SqlServer = new SqlServerBulkInsertOptions(),
            ClickHouse = new ClickHouseBulkInsertOptions(),
        };

        var act = () => options.ValidateForProvider(provider: null);

        act.Should().NotThrow();
    }

    // ---------------------------------------------------------------- D1 target kind

    [Fact]
    public void TargetKind_ShouldDefaultToRegular()
    {
        new BulkInsertOptions().TargetKind.Should().Be(BulkInsertTargetKind.Regular);
    }

    [Fact]
    public void TargetKind_LocalTemporary_ShouldBeCarriedOnTheCommand()
    {
        using var ctx = new InMemoryDataContext();

        var command = ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.TargetKind(BulkInsertTargetKind.LocalTemporary))
            .Values(Array.Empty<BulkEntity>())
            .BuildCommand([], null);

        command.TargetKind.Should().Be(BulkInsertTargetKind.LocalTemporary);
    }

    [Fact]
    public void LocalTemporary_WithDatabaseName_ShouldBeRejectedBeforeReadingTheSource()
    {
        using var ctx = new InMemoryDataContext();
        var source = new CountingSource(new BulkEntity { Id = 1, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .Database("other"))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*DatabaseName*");
        source.Enumerated.Should().Be(0);
    }

    [Theory]
    [InlineData((int)BulkInsertProvider.SqlServer)]
    [InlineData((int)BulkInsertProvider.Postgres)]
    [InlineData((int)BulkInsertProvider.MySql)]
    [InlineData((int)BulkInsertProvider.MariaDb)]
    [InlineData((int)BulkInsertProvider.ClickHouse)]
    [InlineData((int)BulkInsertProvider.Sqlite)]
    public void LocalTemporary_OnASameConnectionProvider_ShouldBeAccepted(int provider)
    {
        var options = new BulkInsertOptions { TargetKind = BulkInsertTargetKind.LocalTemporary };

        var act = () => options.ValidateTargetKind((BulkInsertProvider)provider);

        act.Should().NotThrow();
    }

    [Fact]
    public void LocalTemporary_WithSqlServerSchema_ShouldThrow()
    {
        var options = new BulkInsertOptions
        {
            TargetKind = BulkInsertTargetKind.LocalTemporary,
            SchemaName = "staging",
        };

        var act = () => options.ValidateTargetKind(BulkInsertProvider.SqlServer);

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*SchemaName*");
    }

    [Fact]
    public void LocalTemporary_WithSessionlessClickHouse_ShouldThrow()
    {
        var options = new BulkInsertOptions
        {
            TargetKind = BulkInsertTargetKind.LocalTemporary,
            ClickHouse = new ClickHouseBulkInsertOptions { UseSession = false },
        };

        var act = () => options.ValidateTargetKind(BulkInsertProvider.ClickHouse);

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*UseSession*");
    }

    [Fact]
    public void LocalTemporary_WithParallelClickHouse_ShouldThrow()
    {
        var options = new BulkInsertOptions
        {
            TargetKind = BulkInsertTargetKind.LocalTemporary,
            ClickHouse = new ClickHouseBulkInsertOptions { MaxDegreeOfParallelism = 4 },
        };

        var act = () => options.ValidateTargetKind(BulkInsertProvider.ClickHouse);

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*MaxDegreeOfParallelism*");
    }

    // ---------------------------------------------------------------- D1 bulk-copy flag matrix

    [Theory]
    [InlineData((int)BulkInsertProvider.Postgres)]
    [InlineData((int)BulkInsertProvider.MySql)]
    [InlineData((int)BulkInsertProvider.MariaDb)]
    [InlineData((int)BulkInsertProvider.ClickHouse)]
    [InlineData((int)BulkInsertProvider.Sqlite)]
    public void TableLockTrue_OffSqlServer_ShouldThrowNamingTheOption(int provider)
    {
        var act = () => new BulkInsertOptions { TableLock = true }.ValidateBulkCopyFlags((BulkInsertProvider)provider, useNative: true);

        act.Should().Throw<NotSupportedException>().WithMessage("*TableLock=true*");
    }

    [Fact]
    public void TableLockTrue_OnSqlServerNative_ShouldBeAccepted()
    {
        var act = () => new BulkInsertOptions { TableLock = true }.ValidateBulkCopyFlags(BulkInsertProvider.SqlServer, useNative: true);

        act.Should().NotThrow();
    }

    [Fact]
    public void TableLockTrue_OnSqlServerPortable_ShouldThrow()
    {
        var act = () => new BulkInsertOptions { TableLock = true }.ValidateBulkCopyFlags(BulkInsertProvider.SqlServer, useNative: false);

        act.Should().Throw<NotSupportedException>().WithMessage("*TableLock=true*");
    }

    [Theory]
    [InlineData((int)BulkInsertProvider.SqlServer, true)]
    [InlineData((int)BulkInsertProvider.SqlServer, false)]
    [InlineData((int)BulkInsertProvider.Postgres, true)]
    [InlineData((int)BulkInsertProvider.Postgres, false)]
    [InlineData((int)BulkInsertProvider.MySql, true)]
    [InlineData((int)BulkInsertProvider.MariaDb, true)]
    [InlineData((int)BulkInsertProvider.ClickHouse, true)]
    [InlineData((int)BulkInsertProvider.Sqlite, true)]
    public void TableLockFalse_ShouldBeHonouredByAbsence(int provider, bool useNative)
    {
        var act = () => new BulkInsertOptions { TableLock = false }.ValidateBulkCopyFlags((BulkInsertProvider)provider, useNative);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData((int)BulkInsertProvider.Postgres)]
    [InlineData((int)BulkInsertProvider.MySql)]
    [InlineData((int)BulkInsertProvider.MariaDb)]
    [InlineData((int)BulkInsertProvider.ClickHouse)]
    [InlineData((int)BulkInsertProvider.Sqlite)]
    public void FireTriggersTrue_OffSqlServer_ShouldThrowNamingTheOption(int provider)
    {
        var act = () => new BulkInsertOptions { FireTriggers = true }.ValidateBulkCopyFlags((BulkInsertProvider)provider, useNative: true);

        act.Should().Throw<NotSupportedException>().WithMessage("*FireTriggers=true*");
    }

    [Fact]
    public void FireTriggersFalse_ShouldBeHonouredByAbsence()
    {
        var act = () => new BulkInsertOptions { FireTriggers = false }.ValidateBulkCopyFlags(BulkInsertProvider.Postgres, useNative: true);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData((int)BulkInsertProvider.SqlServer, true)]
    [InlineData((int)BulkInsertProvider.SqlServer, false)]
    [InlineData((int)BulkInsertProvider.Postgres, true)]
    [InlineData((int)BulkInsertProvider.MySql, true)]
    [InlineData((int)BulkInsertProvider.MariaDb, true)]
    [InlineData((int)BulkInsertProvider.ClickHouse, true)]
    [InlineData((int)BulkInsertProvider.Sqlite, true)]
    public void CheckConstraintsTrue_ShouldBeHonouredEverywhere(int provider, bool useNative)
    {
        var act = () => new BulkInsertOptions { CheckConstraints = true }.ValidateBulkCopyFlags((BulkInsertProvider)provider, useNative);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData((int)BulkInsertProvider.Postgres)]
    [InlineData((int)BulkInsertProvider.MySql)]
    [InlineData((int)BulkInsertProvider.ClickHouse)]
    public void CheckConstraintsFalse_WhereThePathAlwaysChecks_ShouldThrow(int provider)
    {
        var act = () => new BulkInsertOptions { CheckConstraints = false }.ValidateBulkCopyFlags((BulkInsertProvider)provider, useNative: true);

        act.Should().Throw<NotSupportedException>().WithMessage("*CheckConstraints=false*");
    }

    [Fact]
    public void CheckConstraintsFalse_OnSqlServerPortable_ShouldThrow()
    {
        var act = () => new BulkInsertOptions { CheckConstraints = false }.ValidateBulkCopyFlags(BulkInsertProvider.SqlServer, useNative: false);

        act.Should().Throw<NotSupportedException>().WithMessage("*CheckConstraints=false*");
    }

    [Theory]
    [InlineData((int)BulkInsertProvider.MariaDb)]
    [InlineData((int)BulkInsertProvider.Sqlite)]
    public void CheckConstraintsFalse_WithScopedSessionControls_ShouldBeAccepted(int provider)
    {
        var act = () => new BulkInsertOptions { CheckConstraints = false }.ValidateBulkCopyFlags((BulkInsertProvider)provider, useNative: false);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData((int)BulkInsertProvider.SqlServer)]
    [InlineData((int)BulkInsertProvider.Postgres)]
    [InlineData((int)BulkInsertProvider.MySql)]
    [InlineData((int)BulkInsertProvider.MariaDb)]
    [InlineData((int)BulkInsertProvider.ClickHouse)]
    [InlineData((int)BulkInsertProvider.Sqlite)]
    public void KeepNullsTrueAndFalse_ShouldBeAcceptedAtD1(int provider)
    {
        var keepNulls = () => new BulkInsertOptions { KeepNulls = true }.ValidateBulkCopyFlags((BulkInsertProvider)provider, useNative: true);
        var defaultEquivalent = () => new BulkInsertOptions { KeepNulls = false }.ValidateBulkCopyFlags((BulkInsertProvider)provider, useNative: false);

        keepNulls.Should().NotThrow();
        defaultEquivalent.Should().NotThrow();
    }

    [Fact]
    public void RejectedFlagCombination_ShouldNameEveryOption()
    {
        var options = new BulkInsertOptions
        {
            TableLock = true,
            FireTriggers = true,
            CheckConstraints = false,
        };

        var act = () => options.ValidateBulkCopyFlags(BulkInsertProvider.Postgres, useNative: true);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*TableLock=true*FireTriggers=true*CheckConstraints=false*");
    }

    [Fact]
    public void BulkCopyFlags_CheckConstraintsOrKeepNullsTrue_ShouldNotThrow()
    {
        var checkConstraints = () => new BulkCopyFlags(CheckConstraints: true, TableLock: false, KeepNulls: false, FireTriggers: false)
            .ThrowIfRequested("the portable INSERT ... VALUES path");
        var keepNulls = () => new BulkCopyFlags(CheckConstraints: false, TableLock: false, KeepNulls: true, FireTriggers: false)
            .ThrowIfRequested("the portable INSERT ... VALUES path");

        checkConstraints.Should().NotThrow();
        keepNulls.Should().NotThrow();
    }

    [Fact]
    public void BulkCopyFlags_TableLockAndFireTriggersTrue_ShouldNameBothOptions()
    {
        var flags = new BulkCopyFlags(CheckConstraints: false, TableLock: true, KeepNulls: false, FireTriggers: true);

        var act = () => flags.ThrowIfRequested("the PostgreSQL COPY path");

        act.Should().Throw<NotSupportedException>().WithMessage("*TableLock=true*FireTriggers=true*");
    }

    // ---------------------------------------------------------------- D1 native-only options on the portable path

    [Fact]
    public void SqlServerInternalTransactionTrue_OnPortablePath_ShouldThrow()
    {
        var options = new BulkInsertOptions { SqlServer = new SqlServerBulkInsertOptions { UseInternalTransaction = true } };

        var act = () => options.ValidateNativeOnlyOptions(useNative: false);

        act.Should().Throw<NotSupportedException>().WithMessage("*UseInternalTransaction=true*");
    }

    [Fact]
    public void SqlServerInternalTransactionFalse_OnPortablePath_ShouldBeHonouredByAbsence()
    {
        var options = new BulkInsertOptions { SqlServer = new SqlServerBulkInsertOptions { UseInternalTransaction = false } };

        var act = () => options.ValidateNativeOnlyOptions(useNative: false);

        act.Should().NotThrow();
    }

    [Fact]
    public void ClickHouseGroup_OnPortablePath_ShouldThrow()
    {
        var options = new BulkInsertOptions { ClickHouse = new ClickHouseBulkInsertOptions { MaxDegreeOfParallelism = 1 } };

        var act = () => options.ValidateNativeOnlyOptions(useNative: false);

        act.Should().Throw<NotSupportedException>().WithMessage("*ClickHouseBulkInsertOptions*");
    }

    // ---------------------------------------------------------------- D4b native compatibility + portable provider scope

    [Fact]
    public void ModeAuto_KeepIdentity_WhenNativeSupportsIt_ShouldPreferTheNativePath()
    {
        using var ctx = new NativeCapableContext(supportsKeepIdentity: true);

        var count = ctx.CreateBulkInsertBuilder<BulkIdentityEntity>(o => o.KeepIdentity())
            .Values([new BulkIdentityEntity { Id = 7, Name = "a" }])
            .BulkInsert();

        // Row 3 of the provider matrix: explicit identity values stay on the native path when the provider
        // declares it can honour them, instead of being forced onto the portable statement.
        count.Should().Be(1);
        ctx.NativeCalls.Should().Be(1);
        ctx.PortableScopeEntries.Should().Be(0);
    }

    [Fact]
    public void ModeAuto_KeepIdentity_WhenNativeCannotHonourIt_ShouldFallBackToPortable()
    {
        using var ctx = new NativeCapableContext();
        var source = new BulkIdentityCountingSource(new BulkIdentityEntity { Id = 7, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkIdentityEntity>(o => o.KeepIdentity())
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*t305-fake-connection*");
        ctx.NativeCalls.Should().Be(0);
        source.Enumerated.Should().Be(1);
    }

    [Fact]
    public void ModeNative_KeepIdentity_WhenNativeCannotHonourIt_ShouldThrowByNameBeforeReading()
    {
        using var ctx = new NativeCapableContext();
        var source = new BulkIdentityCountingSource(new BulkIdentityEntity { Id = 7, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkIdentityEntity>(o => o.Mode(BulkInsertMode.Native).KeepIdentity())
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*KeepIdentity=true*");
        ctx.NativeCalls.Should().Be(0);
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void ModeAuto_IgnoreDuplicates_WhenNativeSupportsIt_ShouldPreferTheNativePath()
    {
        using var ctx = new NativeCapableContext(supportsIgnoreDuplicates: true);

        var count = ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.IgnoreDuplicates())
            .Values([new BulkEntity { Id = 1, Name = "a" }])
            .BulkInsert();

        count.Should().Be(1);
        ctx.NativeCalls.Should().Be(1);
    }

    [Fact]
    public void ModeAuto_IgnoreDuplicates_WhenNativeCannotHonourIt_ShouldUseThePortablePath()
    {
        using var ctx = new NativeCapableContext();

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.IgnoreDuplicates())
            .Values([new BulkEntity { Id = 1, Name = "a" }])
            .BulkInsert();

        // The portable path is selected, and this dialect has neither ignore form, so the request is
        // refused by name there instead of silently failing the whole write.
        act.Should().Throw<NotSupportedException>().WithMessage("*IgnoreDuplicates=true*");
        ctx.NativeCalls.Should().Be(0);
    }

    [Fact]
    public void ModeNative_IgnoreDuplicates_WhenNativeCannotHonourIt_ShouldThrowByNameBeforeReading()
    {
        using var ctx = new NativeCapableContext();
        var source = new CountingSource(new BulkEntity { Id = 1, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Mode(BulkInsertMode.Native).IgnoreDuplicates())
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*IgnoreDuplicates=true*");
        source.Enumerated.Should().Be(0);
    }

    [Fact]
    public void ModePortable_ShouldStillRunTheProviderScope()
    {
        using var ctx = new NativeCapableContext(supportsKeepIdentity: true, supportsIgnoreDuplicates: true);
        var source = new CountingSource(new BulkEntity { Id = 1, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Mode(BulkInsertMode.Portable))
            .Values(source)
            .BulkInsert();

        // Mode.Portable never bypasses the provider-scoped contract: the scope is entered (and the source
        // is read on this path) before the portable write reaches the fake connection.
        act.Should().Throw<NotSupportedException>().WithMessage("*t305-fake-connection*");
        ctx.NativeCalls.Should().Be(0);
        ctx.PortableScopeEntries.Should().Be(1);
        ctx.PortableScopeDisposals.Should().Be(1);
        source.Enumerated.Should().Be(1);
    }

    [Fact]
    public async Task PortableProviderScope_ShouldBeEnteredBeforeTheSourceAndRestoredOnError()
    {
        using var ctx = new NativeCapableContext();
        var source = new CountingAsyncSource(new BulkEntity { Id = 1, Name = "a" });
        ctx.OnPortableScopeEntered = () => source.Enumerated;

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Mode(BulkInsertMode.Portable))
            .Values(source)
            .BulkInsertAsync();

        // The streaming source is not pulled until the scope is active, and the scope is restored on the
        // error path, exactly like the native path does.
        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*t305-fake-connection*");
        ctx.EnumeratedAtScopeEntry.Should().Be(0);
        ctx.PortableScopeEntries.Should().Be(1);
        ctx.PortableScopeDisposals.Should().Be(1);
    }

    // ---------------------------------------------------------------- T305 D6 matrix presence cells (rows 1/6-9)

    [Fact]
    public void NullableFlags_TrueFalseAndAbsent_ShouldBeThreeDistinctStates()
    {
        var absent = new BulkInsertOptions();
        var explicitTrue = new BulkInsertOptions { CheckConstraints = true, TableLock = true, KeepNulls = true, FireTriggers = true };
        var explicitFalse = new BulkInsertOptions { CheckConstraints = false, TableLock = false, KeepNulls = false, FireTriggers = false };

        absent.CheckConstraintsSpecified.Should().BeFalse();
        explicitTrue.CheckConstraints.Should().BeTrue();
        explicitFalse.CheckConstraints.Should().BeFalse();

        explicitTrue.CheckConstraintsSpecified.Should().BeTrue();
        explicitFalse.CheckConstraintsSpecified.Should().BeTrue();
        explicitTrue.TableLockSpecified.Should().BeTrue();
        explicitTrue.KeepNullsSpecified.Should().BeTrue();
        explicitTrue.FireTriggersSpecified.Should().BeTrue();
    }

    [Fact]
    public void KeepIdentityAndIgnoreDuplicates_TrueFalseAndAbsent_ShouldBeThreeDistinctStates()
    {
        var absent = new BulkInsertOptions();
        var explicitTrue = new BulkInsertOptions { KeepIdentity = true, IgnoreDuplicates = true };
        var explicitFalse = new BulkInsertOptions { KeepIdentity = false, IgnoreDuplicates = false };

        absent.KeepIdentitySpecified.Should().BeFalse();
        absent.IgnoreDuplicatesSpecified.Should().BeFalse();

        explicitTrue.KeepIdentitySpecified.Should().BeTrue();
        explicitTrue.KeepIdentity.Should().BeTrue();
        explicitTrue.IgnoreDuplicatesSpecified.Should().BeTrue();
        explicitTrue.IgnoreDuplicates.Should().BeTrue();

        explicitFalse.KeepIdentitySpecified.Should().BeTrue();
        explicitFalse.KeepIdentity.Should().BeFalse();
        explicitFalse.IgnoreDuplicatesSpecified.Should().BeTrue();
        explicitFalse.IgnoreDuplicates.Should().BeFalse();
    }

    [Fact]
    public void ModeAuto_OnAPortableOnlyContext_ShouldUseThePortablePath()
    {
        using var ctx = new PortableOnlyContext();
        var source = new CountingSource(new BulkEntity { Id = 1, Name = "a" });

        var act = () => ctx.CreateBulkInsertBuilder<BulkEntity>(o => o.Mode(BulkInsertMode.Auto))
            .Values(source)
            .BulkInsert();

        // Auto has no native path to prefer here, so the write goes through the portable INSERT loop and
        // reaches the fake connection with the row already pulled.
        act.Should().Throw<NotSupportedException>().WithMessage("*t305-fake-connection*");
        source.Enumerated.Should().Be(1);
    }

    [Fact]
    public void ModeAndTargetKind_ShouldExposeExactlyTheDeclaredValues()
    {
        Enum.GetValues<BulkInsertMode>().Should().Equal(BulkInsertMode.Auto, BulkInsertMode.Native, BulkInsertMode.Portable);
        Enum.GetValues<BulkInsertTargetKind>().Should().Equal(BulkInsertTargetKind.Regular, BulkInsertTargetKind.LocalTemporary);
    }

    private sealed class NativeCapableDialect : SqlDialectBase
    {
        internal static readonly NativeCapableDialect Instance = new();

        public override bool SupportsBulkCopy => true;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class PortableOnlyDialect : SqlDialectBase
    {
        internal static readonly PortableOnlyDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class NativeCapableContext : DataContext, IBulkInsertNativeOptions, IBulkInsertPortableScope
    {
        private readonly bool _supportsKeepIdentity;
        private readonly bool _supportsIgnoreDuplicates;

        public NativeCapableContext(bool supportsKeepIdentity = false, bool supportsIgnoreDuplicates = false)
            : base(new DataContextBuilder())
        {
            _supportsKeepIdentity = supportsKeepIdentity;
            _supportsIgnoreDuplicates = supportsIgnoreDuplicates;
        }

        public int NativeCalls { get; private set; }

        public int PortableScopeEntries { get; private set; }

        public int PortableScopeDisposals { get; private set; }

        public int EnumeratedAtScopeEntry { get; private set; }

        public Func<int>? OnPortableScopeEntered { get; set; }

        bool IBulkInsertNativeOptions.SupportsNativeKeepIdentity => _supportsKeepIdentity;

        bool IBulkInsertNativeOptions.SupportsNativeIgnoreDuplicates => _supportsIgnoreDuplicates;

        IDisposable IBulkInsertPortableScope.BeginPortableBulkInsertScope(BulkInsertCommand command)
        {
            PortableScopeEntries++;
            EnumeratedAtScopeEntry = OnPortableScopeEntered?.Invoke() ?? 0;
            return new RecordingScope(this);
        }

        private sealed class RecordingScope(NativeCapableContext owner) : IDisposable
        {
            public void Dispose() => owner.PortableScopeDisposals++;
        }

        public override ISqlDialect Dialect => NativeCapableDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException("t305-fake-param");

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException("t305-fake-connection");

        protected override int BulkInsertRows(
            string tableName,
            IReadOnlyList<string> columnNames,
            IReadOnlyList<IPropertyMetadata> columns,
            IEnumerable<object?[]> rows,
            int? commandTimeoutSeconds,
            int? maxBatchSize,
            Action<int>? progress,
            int notifyEvery,
            BulkCopyFlags bulkCopy)
        {
            NativeCalls++;
            return rows.Count();
        }

        protected override Task<int> BulkInsertRowsAsync(
            string tableName,
            IReadOnlyList<string> columnNames,
            IReadOnlyList<IPropertyMetadata> columns,
            IAsyncEnumerable<object?[]> rows,
            int? commandTimeoutSeconds,
            int? maxBatchSize,
            Action<int>? progress,
            int notifyEvery,
            BulkCopyFlags bulkCopy,
            CancellationToken cancellationToken)
        {
            NativeCalls++;
            return CountAsync(rows, cancellationToken);
        }

        private static async Task<int> CountAsync(IAsyncEnumerable<object?[]> rows, CancellationToken cancellationToken)
        {
            var count = 0;
            await foreach (var _ in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
                count++;

            return count;
        }
    }

    private sealed class PortableOnlyContext : DataContext
    {
        public PortableOnlyContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => PortableOnlyDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException("t305-fake-param");

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException("t305-fake-connection");
    }

    private sealed class CountingAsyncSource(params BulkEntity[] rows) : IAsyncEnumerable<BulkEntity>
    {
        public int Enumerated { get; private set; }

        public async IAsyncEnumerator<BulkEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var row in rows)
            {
                Enumerated++;
                yield return row;
                await Task.Yield();
            }
        }
    }

    private sealed class BulkIdentityCountingSource(params BulkIdentityEntity[] rows) : IEnumerable<BulkIdentityEntity>
    {
        public int Enumerated { get; private set; }

        public IEnumerator<BulkIdentityEntity> GetEnumerator()
        {
            foreach (var row in rows)
            {
                Enumerated++;
                yield return row;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CountingSource(params BulkEntity[] rows) : IEnumerable<BulkEntity>
    {
        public int Enumerated { get; private set; }

        public IEnumerator<BulkEntity> GetEnumerator()
        {
            foreach (var row in rows)
            {
                Enumerated++;
                yield return row;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static async IAsyncEnumerable<BulkEntity> EmptyAsync()
    {
        await Task.CompletedTask;
        yield break;
    }
}

public interface IBulkEntity
{
    int Id { get; set; }
    string Name { get; set; }
}

public class BulkEntity : IBulkEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>An entity with an identity key, so <c>KeepIdentity</c> is actually requested.</summary>
[SqlTable("bulk_identity_entity")]
public interface IBulkIdentityEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string Name { get; set; }
}

/// <summary>The concrete entity for <see cref="IBulkIdentityEntity"/>.</summary>
public sealed class BulkIdentityEntity : IBulkIdentityEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
