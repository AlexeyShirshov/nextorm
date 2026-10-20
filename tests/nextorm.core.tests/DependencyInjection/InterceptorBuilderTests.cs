using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Pins the off-context contract of the <see cref="DataContextBuilder"/> interceptor registration:
/// the argument guards, the fluent chaining, the immediate type validation and the exact-key
/// deduplication. Behaviour against a real provider is covered by the SQLite suite, since the
/// in-memory context exposes no command lifecycle. DI-backed resolution is covered by
/// <see cref="InterceptorDiRegistrationTests"/>.
/// </summary>
[Trait("Category", "T221")]
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
    public void AddInterceptor_WithNullType_Throws()
    {
        var builder = new DataContextBuilder();

        var act = () => builder.AddInterceptor((Type)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("interceptorType");
    }

    [Fact]
    public void AddInterceptor_ReturnsTheSameBuilder()
    {
        var builder = new DataContextBuilder();

        builder.AddInterceptor(new NoopQueryInterceptor()).Should().BeSameAs(builder);
        builder.AddInterceptor(new NoopConnectionInterceptor()).Should().BeSameAs(builder);
        builder.AddInterceptor(typeof(NoopQueryInterceptor)).Should().BeSameAs(builder);
        builder.AddInterceptor<NoopConnectionInterceptor>().Should().BeSameAs(builder);
    }

    [Fact]
    public void AddInterceptor_WithTypeNotAssignableToAnInterceptorInterface_Throws()
    {
        var builder = new DataContextBuilder();

        var act = () => builder.AddInterceptor(typeof(string));

        act.Should().Throw<ArgumentException>()
            .WithParameterName("interceptorType")
            .WithMessage("*must be a closed reference type assignable to IQueryInterceptor or IConnectionInterceptor*");
    }

    [Fact]
    public void AddInterceptor_WithOpenGenericType_Throws()
    {
        var builder = new DataContextBuilder();

        var act = () => builder.AddInterceptor(typeof(List<>));

        act.Should().Throw<ArgumentException>().WithParameterName("interceptorType");
    }

    [Fact]
    public void AddInterceptor_WithValueType_Throws()
    {
        var builder = new DataContextBuilder();

        var act = () => builder.AddInterceptor(typeof(int));

        act.Should().Throw<ArgumentException>().WithParameterName("interceptorType");
    }

    [Fact]
    public void AddInterceptor_InvalidType_LeavesRegistrationStateUnchanged()
    {
        var builder = new DataContextBuilder();
        var retained = new NoopQueryInterceptor();
        builder.AddInterceptor(retained);

        var act = () => builder.AddInterceptor(typeof(int));

        act.Should().Throw<ArgumentException>();
        builder.QueryInterceptors.Should().ContainSingle().Which.Should().BeSameAs(retained);
    }

    [Fact]
    public void AddInterceptor_DuplicateInstanceType_FirstWins()
    {
        var first = new NoopQueryInterceptor();
        var second = new NoopQueryInterceptor();

        var builder = new DataContextBuilder()
            .AddInterceptor(first)
            .AddInterceptor(second);

        builder.QueryInterceptors.Should().ContainSingle().Which.Should().BeSameAs(first);
    }

    [Fact]
    public void AddInterceptor_StandaloneTypeEntry_ThrowsAtCreation()
    {
        var builder = new DataContextBuilder()
            .AddInterceptor<NoopQueryInterceptor>();
        builder.Factory = _ => new InMemoryDataContext();

        var act = () => builder.CreateDataContext();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires a DI-backed DataContextBuilder*");
    }

    [Fact]
    public void AddInterceptor_InterfaceServiceKey_IsAcceptedAtRegistration()
    {
        var builder = new DataContextBuilder();

        var act = () => builder.AddInterceptor(typeof(IQueryInterceptor));

        act.Should().NotThrow();
    }

    // Builder guards that live in the same mutation surface (DataContextBuilder) as the interceptor
    // registrations. They are pinned here so the Stryker run over DI/DataContextBuilder.cs is fully
    // accounted for, not because they are part of the interceptor feature.

    [Fact]
    public void Constructor_WithNullServiceProvider_Throws()
    {
        var act = () => new DataContextBuilder((IServiceProvider)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseLoggerFactory_WithNull_Throws()
    {
        var builder = new DataContextBuilder();

        var act = () => builder.UseLoggerFactory(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseCommandTimeout_WithPositiveValue_IsApplied()
    {
        new DataContextBuilder().UseCommandTimeout(7).CommandTimeout.Should().Be(7);
    }

    [Fact]
    public void UseCommandTimeout_WithNonPositiveValue_UsesProviderDefault()
    {
        new DataContextBuilder().UseCommandTimeout(0).CommandTimeout.Should().BeNull();
        new DataContextBuilder().UseCommandTimeout(-1).CommandTimeout.Should().BeNull();
    }

    [Fact]
    public void UseEagerLoadMode_WithDefaultSentinel_ThrowsWithMessage()
    {
        var builder = new DataContextBuilder();

        var act = () => builder.UseEagerLoadMode(EagerLoadMode.Default);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*inheritance sentinel*");
    }

    [Fact]
    public void UseEagerLoadMode_WithUndefinedValue_ThrowsWithMessage()
    {
        var builder = new DataContextBuilder();

        var act = () => builder.UseEagerLoadMode((EagerLoadMode)999);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*is not a defined EagerLoadMode*");
    }

    [Fact]
    public void CreateDataContext_WithoutFactory_Throws()
    {
        var builder = new DataContextBuilder();

        var act = () => builder.CreateDataContext();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Context is not set*");
    }

    [Fact]
    public void LogSensitiveData_TogglesTheFlag()
    {
        new DataContextBuilder().LogSensitiveData(true).ShouldLogSensitiveData.Should().BeTrue();
    }

    [Fact]
    public void UseUppercaseKeywords_And_UseMultilineBatchSql_ToggleTheFlags()
    {
        var builder = new DataContextBuilder();
        builder.UseUppercaseKeywords().KeywordCase.Should().Be(KeywordCase.Upper);
        builder.UseUppercaseKeywords(false).KeywordCase.Should().Be(KeywordCase.Lower);
        builder.UseMultilineBatchSql().MultilineBatchSql.Should().BeTrue();
    }
}
