using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// The <see cref="TableAlias"/> streaming members are query-expression markers recognised by
/// <see cref="System.Reflection.MethodInfo"/>; they must never silently return a null stream when they
/// are evaluated outside a streaming terminal (for example on a provider that compiles the projection,
/// such as the in-memory one). <c>#101 P1</c> replaced the silent <see cref="Stream.Null"/>/
/// <see cref="TextReader.Null"/> placeholders with a clear <see cref="NotSupportedException"/>.
/// </summary>
public class TableAliasStreamingAccessorTests
{
    [Fact]
    public void GetStream_WhenInvokedOutsideQueryExpression_ShouldThrowNotSupported()
    {
        var alias = new TableAlias();

        var act = () => alias.GetStream("payload");

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*GetStream*query-expression marker*ToStream*");
    }

    [Fact]
    public void GetTextReader_WhenInvokedOutsideQueryExpression_ShouldThrowNotSupported()
    {
        var alias = new TableAlias();

        var act = () => alias.GetTextReader("body");

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*GetTextReader*query-expression marker*ToTextReader*");
    }
}
