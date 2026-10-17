using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// ClickHouse has no stored procedures, so its dialect reports
/// <see cref="ISqlDialect.SupportsStoredProcedures"/> as false and <c>ExecuteProcedure</c> rejects the
/// call before a connection is opened (the placeholder connection string is never used).
/// </summary>
public class StoredProcedureTests
{
    [Fact]
    public void SupportsStoredProcedures_IsFalse()
    {
        ClickHouseTestContext.CreateClickHouse().Dialect.SupportsStoredProcedures.Should().BeFalse();
    }

    [Fact]
    public void ExecuteProcedure_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.ExecuteProcedure("my_proc");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task ExecuteProcedureAsync_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = async () => await ctx.ExecuteProcedureAsync("my_proc");

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
