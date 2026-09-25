using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Pins the off-context contract of <see cref="DataContextBuilder.AddInterceptor(IQueryInterceptor)">DataContextBuilder.AddInterceptor</see>:
/// the argument guard and the fluent chaining. Behaviour against a real provider is covered by the
/// SQLite suite, since the in-memory context exposes no command lifecycle.
/// </summary>
public class InterceptorBuilderTests
{
    private sealed class NoopQueryInterceptor : IQueryInterceptor
    {
    }

    private sealed class NoopConnectionInterceptor : IConnectionInterceptor
    {
    }

    [Fact]
    public void AddInterceptor_WithNullQueryInterceptor_Throws()
    {
        var builder = new DataContextBuilder();

        var act = () => builder.AddInterceptor((IQueryInterceptor)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddInterceptor_WithNullConnectionInterceptor_Throws()
    {
        var builder = new DataContextBuilder();

        var act = () => builder.AddInterceptor((IConnectionInterceptor)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddInterceptor_ReturnsTheSameBuilder()
    {
        var builder = new DataContextBuilder();

        builder.AddInterceptor(new NoopQueryInterceptor()).Should().BeSameAs(builder);
        builder.AddInterceptor(new NoopConnectionInterceptor()).Should().BeSameAs(builder);
    }
}
