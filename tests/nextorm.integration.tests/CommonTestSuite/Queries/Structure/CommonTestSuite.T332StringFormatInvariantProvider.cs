using System.Globalization;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T332 live coverage: the canonical <c>CultureInfo.InvariantCulture</c> static property must reach the
/// database exactly like the pre-existing constant-invariant arm, and the constant, null-provider and
/// refusal behaviour established before the change must stay unchanged. Runs against every provider in
/// the shared suite (SQLite, PostgreSQL, SQL Server, MySQL, ClickHouse); MariaDB has its own class.
/// </summary>
public abstract partial class CommonTestSuite
{
    private const string T332RejectionMessage =
        "string.Format is only supported with CultureInfo.InvariantCulture; the current/other cultures have no portable SQL form.";

    [Fact]
    public void T332StringFormat_StaticPropertyInvariantProvider_ShouldExecute()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => string.Format(CultureInfo.InvariantCulture, "{0}-{1}", e.String, e.RequiredString))
            .First();

        value.Should().Be("dadfasd-sdf");
    }

    [Fact]
    public void T332StringFormat_ConstantInvariantProvider_ShouldExecute()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(T332ConstantProviderCase("{0}-{1}"))
            .First();

        value.Should().Be("dadfasd-sdf");
    }

    [Fact]
    public void T332StringFormat_NullProvider_ShouldKeepBehavingLikeTheProviderlessOverload()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                LiteralNull = string.Format((IFormatProvider?)null, "{0}-{1}", e.String, e.RequiredString),
                DefaultNull = string.Format(default(IFormatProvider?), "{0}/{1}", e.String, e.RequiredString),
                NoProvider = string.Format("{0}-{1}", e.String, e.RequiredString)
            })
            .First();

        value.LiteralNull.Should().Be(value.NoProvider);
        value.DefaultNull.Should().Be("dadfasd/sdf");
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
