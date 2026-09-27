using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory provider has no SQL source to rewrite, so every per-query source override is rejected
/// eagerly with <see cref="NotSupportedException"/> rather than being silently ignored.
/// </summary>
public class SourceOverrideTests
{
    [Fact]
    public void WithTableName_ShouldThrowOnInMemory()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        var act = () => e.WithTableName("other");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void WithSchema_ShouldThrowOnInMemory()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        var act = () => e.WithSchema("main");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void WithDatabase_ShouldThrowOnInMemory()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        var act = () => e.WithDatabase("shop");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void WithServer_ShouldThrowOnInMemory()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        var act = () => e.WithServer("srv");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void WithTableExpression_ShouldThrowOnInMemory()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        var act = () => e.WithTableExpression("select 1");

        act.Should().Throw<NotSupportedException>();
    }
}
