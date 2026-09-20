using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// F3 (LSP): a prepared command belongs to the context that produced it. Passing it to a context of
/// the other storage backend must fail fast with a clear <see cref="ArgumentException"/> instead of
/// silently producing wrong results. These are the regression tests described by
/// <c>docs/specs/design/solid-review.md</c> (A.10).
/// </summary>
public class BackendMixingTests
{
    [Fact]
    public void InMemoryCommand_OnSqlContext_ShouldThrowArgumentException()
    {
        using var inMemory = new InMemoryDataContext();
        var command = inMemory.From<ISimpleEntity>().Select(x => x.Id);
        var prepared = inMemory.GetPreparedQueryCommand(command, false, true, CancellationToken.None);

        using var sqlite = SqliteTestContext.CreateSqlite();

        var act = () => sqlite.ToList(prepared, ReadOnlySpan<object?>.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("preparedQueryCommand");
    }

    [Fact]
    public void SqlCommand_OnInMemoryContext_ShouldThrowArgumentException()
    {
        using var sqlite = SqliteTestContext.CreateSqlite();
        var command = sqlite.From<ISimpleEntity>().Select(x => x.Id);
        var prepared = sqlite.GetPreparedQueryCommand(command, false, true, CancellationToken.None);

        using var inMemory = new InMemoryDataContext();

        var act = () => inMemory.ToList(prepared, ReadOnlySpan<object?>.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("preparedQueryCommand");
    }
}
