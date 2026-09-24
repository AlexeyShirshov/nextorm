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
    public void EmptyName_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>().ToTempTable(string.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
