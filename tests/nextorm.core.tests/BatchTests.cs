using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory context is read-only and does not implement the batch executor role, so every batch
/// entry point rejects it with <see cref="NotSupportedException"/> instead of silently degrading to
/// separate statements. No database is involved.
/// </summary>
public class BatchTests
{
    [Fact]
    public void InMemory_Batch_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.Batch();

        act.Should().Throw<NotSupportedException>();
    }

}
