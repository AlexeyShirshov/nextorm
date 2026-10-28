using System.Globalization;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Tests.Shared.ClickHouseRetry;

namespace NextORM.Integration.Tests;

/// <summary>
/// T332 ClickHouse live coverage. ClickHouse does not derive <see cref="CommonTestSuite"/>, so the narrow
/// behaviour — the canonical <c>CultureInfo.InvariantCulture</c> static property, the pre-existing constant
/// and null-provider arms, and the exact refusal — is verified here against a real ClickHouse server.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseT332StringFormatInvariantProviderTests : ProviderTestSuite
{
    private const string T332RejectionMessage =
        "string.Format is only supported with CultureInfo.InvariantCulture; the current/other cultures have no portable SQL form.";

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public async Task T332StringFormat_StaticPropertyInvariantProvider_ShouldExecute()
    {
        var query = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => string.Format(CultureInfo.InvariantCulture, "{0}-{1}", e.String, e.RequiredString));

        var value = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseT332StringFormatInvariantProviderTests.T332StringFormat_StaticPropertyInvariantProvider_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        value.Should().Be("dadfasd-sdf");
    }

    [Fact]
    public async Task T332StringFormat_ConstantInvariantProvider_ShouldExecute()
    {
        var query = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(T332ConstantProviderCase("{0}-{1}"));

        var value = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseT332StringFormatInvariantProviderTests.T332StringFormat_ConstantInvariantProvider_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        value.Should().Be("dadfasd-sdf");
    }

    [Fact]
    public async Task T332StringFormat_NullProvider_ShouldKeepBehavingLikeTheProviderlessOverload()
    {
        var query = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                LiteralNull = string.Format((IFormatProvider?)null, "{0}-{1}", e.String, e.RequiredString),
                NoProvider = string.Format("{0}-{1}", e.String, e.RequiredString)
            });

        var value = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseT332StringFormatInvariantProviderTests.T332StringFormat_NullProvider_ShouldKeepBehavingLikeTheProviderlessOverload#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        value.LiteralNull.Should().Be(value.NoProvider);
    }

    [Fact]
    public void T332StringFormat_NonCanonicalProvider_ShouldRejectWithExactMessage()
    {
        var act = () => _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => string.Format(CultureInfo.CurrentCulture, "{0}", e.Id))
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage(T332RejectionMessage);
    }

    /// <summary>
    /// The programmatic constant shape accepted since before the defect:
    /// <c>Expression.Constant(CultureInfo.InvariantCulture)</c> rather than a static property access.
    /// </summary>
    private static Expression<Func<IComplexEntity, string>> T332ConstantProviderCase(string format)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "e");
        var formatMethod = typeof(string).GetMethod(
            nameof(string.Format),
            [typeof(IFormatProvider), typeof(string), typeof(object), typeof(object)])!;
        var body = Expression.Call(
            formatMethod,
            Expression.Constant(CultureInfo.InvariantCulture),
            Expression.Constant(format),
            Expression.Convert(Expression.Property(parameter, nameof(IComplexEntity.String)), typeof(object)),
            Expression.Convert(Expression.Property(parameter, nameof(IComplexEntity.RequiredString)), typeof(object)));
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }
}
