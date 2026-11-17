using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.Caching.Parameters;

/// <summary>
/// Metadata-only recognition of value-list membership (<c>in</c>/<c>Contains</c>). The native projection
/// route uses it to skip a shape capture it can never publish, so it must match exactly the translator's
/// membership shapes: a collection receiver (inline or captured, scalar or tuple) and the
/// <c>@in</c>/<c>global_in</c> value list. A scalar type carrying an unrelated
/// <see cref="SqlFunctionAttribute"/> <c>Contains</c> method is a function call, not a membership.
/// </summary>
public sealed class InValuesRecognitionTests
{
    private sealed class TokenRow
    {
        public long Id { get; set; }

        public Token Token { get; set; } = null!;
    }

    // A mapped scalar value (for example an opaque search token) whose Contains is a SQL function, not a
    // collection membership: the translator emits the receiver as the function's first argument.
    private sealed class Token
    {
        [SqlFunction("token_contains")]
        public bool Contains(long value) => throw new NotSupportedException();
    }

    [Fact]
    public void ScalarFunctionReceiverContains_ShouldNotBeRecognizedAsAValueList()
    {
        Expression<Func<TokenRow, bool>> predicate = x => x.Token.Contains(x.Id);
        var call = (MethodCallExpression)((LambdaExpression)predicate).Body;

        // The translator's own recognizer is the authority: it requires an IEnumerable receiver whose
        // element type matches the argument, so this call is never a value list.
        InValues.TryGetArguments(call, out _, out _, out _, out _).Should().BeFalse(
            "a [SqlFunction] instance Contains is a scalar function call, not a collection membership");

        InValues.ContainsAnyInValues(predicate).Should().BeFalse(
            "the classification must agree with the translator instead of treating any receiver as a collection");
        InValues.ContainsUnkeyedInValues(predicate).Should().BeFalse();
    }

    [Fact]
    public void CollectionContains_ShouldStayRecognized()
    {
        var ids = new long[] { 1, 3 };
        Expression<Func<TokenRow, bool>> captured = x => ids.Contains(x.Id);

        InValues.TryGetArguments((MethodCallExpression)((LambdaExpression)captured).Body, out _, out _, out _, out _)
            .Should().BeTrue("an array receiver is an IEnumerable with a matching element type");
        InValues.ContainsAnyInValues(captured).Should().BeTrue();
        InValues.ContainsUnkeyedInValues(captured).Should().BeTrue("a captured collection follows its contents");

        Expression<Func<TokenRow, bool>> inline = x => new long[] { 1, 3 }.Contains(x.Id);
        InValues.ContainsAnyInValues(inline).Should().BeTrue();
        InValues.ContainsUnkeyedInValues(inline).Should().BeFalse(
            "an inline scalar list is keyed by the expression graph and stays publishable");
    }

    [Fact]
    public void ValueListFunction_ShouldStayRecognized()
    {
        var ids = new long[] { 1, 3 };
        Expression<Func<TokenRow, bool>> captured = x => SqlFunctions.Sql.@in(x.Id, ids);

        InValues.ContainsAnyInValues(captured).Should().BeTrue();
        InValues.ContainsUnkeyedInValues(captured).Should().BeTrue();

        Expression<Func<TokenRow, bool>> inline = x => SqlFunctions.Sql.@in(x.Id, new long[] { 1, 3 });
        InValues.ContainsAnyInValues(inline).Should().BeTrue();
        InValues.ContainsUnkeyedInValues(inline).Should().BeFalse();
    }
}
