using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Builder-level validation of the bulk-insert terminal. The in-memory context is read-only, so any
/// terminal that needs the database throws; no database is involved.
/// </summary>
public class BulkInsertBuilderTests
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
