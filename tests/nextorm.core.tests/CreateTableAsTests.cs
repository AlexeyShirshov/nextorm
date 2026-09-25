using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Builder-level validation of the <c>CREATE TABLE ... AS SELECT</c> terminals. The in-memory context is
/// query-only (it does not implement the mutation executor role), so materialising a query into a table
/// on it throws; no database is involved.
/// </summary>
public class CreateTableAsTests
{
    [Fact]
    public void InMemory_ToTempTable_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>().Where(x => x.Id > 0).ToTempTable("t");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InMemory_ToTable_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>().Where(x => x.Id > 0).ToTable("t");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InMemory_ToTempTableSql_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>().Where(x => x.Id > 0).ToTempTableSql("t");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InMemory_AsTempTable_From_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var source = ctx.From<ConventionalEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }).AsTempTable();

        var act = () => ctx.From(source);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void EmptyName_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>().ToTempTable(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Builder_ShouldProduceOptions()
    {
        var options = new CreateTableOptionsBuilder()
            .IfNotExists()
            .WithData(false)
            .OnCommit(TempTableOnCommit.Drop)
            .Columns("a", "b")
            .Build();

        options.IfNotExists.Should().BeTrue();
        options.WithData.Should().BeFalse();
        options.OnCommit.Should().Be(TempTableOnCommit.Drop);
        options.Columns.Should().Equal("a", "b");
    }

    [Fact]
    public void Builder_DropExistingAndIfNotExists_ShouldThrow()
    {
        var act = () => new CreateTableOptionsBuilder().DropExisting().IfNotExists().Build();

        act.Should().Throw<ArgumentException>().WithMessage("*mutually exclusive*");
    }

    [Fact]
    public void Builder_ColumnsWithoutNames_ShouldThrow()
    {
        var act = () => new CreateTableOptionsBuilder().Columns();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Builder_ColumnsWithEmptyEntry_ShouldThrow()
    {
        var act = () => new CreateTableOptionsBuilder().Columns("a", "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InMemory_ToTableWithBuilder_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>().ToTable("t", o => o.DropExisting());

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NullConfigure_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>().ToTable("t", (Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder>)null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
