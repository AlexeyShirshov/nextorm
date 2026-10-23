using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// T332: the canonical <c>CultureInfo.InvariantCulture</c> static property must be accepted by the
/// provider-taking <c>string.Format</c> overload exactly where the constant-invariant arm is already
/// accepted, while the constant, null-provider and refusal behaviour of the translator is preserved.
/// </summary>
public class T332StringFormatInvariantProviderTests
{
    private const string RejectionMessage =
        "string.Format is only supported with CultureInfo.InvariantCulture; the current/other cultures have no portable SQL form.";

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void T332StringFormat_ConstantInvariantProvider_ShouldKeepExistingSql()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var withProvider = SqlOf(ctx, e.Select(ConstantProviderOne("{0:N2}")));
        var withoutProvider = SqlOf(ctx, e.Select(x => string.Format("{0:N2}", x.Id)));

        withProvider.Should().Be(withoutProvider);
        withProvider.Should().Contain("format(id, 2)");
    }

    [Fact]
    public void T332StringFormat_StaticPropertyInvariantProvider_ShouldRenderSameSqlAsTheConstantArm()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var expected = SqlOf(ctx, e.Select(ConstantProviderOne("{0:N2}")));
        var actual = SqlOf(ctx, e.Select(x => string.Format(CultureInfo.InvariantCulture, "{0:N2}", x.Id)));

        actual.Should().Be(expected);
        actual.Should().Contain("format(id, 2)");
    }

    [Fact]
    public void T332StringFormat_ConvertWrappedInvariantProvider_ShouldRenderSameSqlAsTheConstantArm()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var expected = SqlOf(ctx, e.Select(ConstantProviderOne("{0:N2}")));
        var actual = SqlOf(ctx, e.Select(x => string.Format((IFormatProvider)CultureInfo.InvariantCulture, "{0:N2}", x.Id)));

        actual.Should().Be(expected);
    }

    [Fact]
    public void T332StringFormat_StaticPropertyInvariantProvider_FixedAndParamsOverloads_ShouldBeAccepted()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var twoValues = SqlOf(ctx, e.Select(x => string.Format(CultureInfo.InvariantCulture, "{0}/{1}", x.Id, x.Id)));
        twoValues.Should().Be(SqlOf(ctx, e.Select(ConstantProviderTwo("{0}/{1}"))));

        var threeValues = SqlOf(ctx, e.Select(x => string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}", x.Id, x.Id, x.Id)));
        threeValues.Should().Be(SqlOf(ctx, e.Select(ConstantProviderThree("{0}-{1}-{2}"))));

        var paramsArray = SqlOf(ctx, e.Select(x => string.Format(CultureInfo.InvariantCulture, "{0}{1}{2}{3}", new object[] { x.Id, 1, 2, 3 })));
        paramsArray.Should().Be(SqlOf(ctx, e.Select(ConstantProviderParams("{0}{1}{2}{3}"))));
    }

    [Fact]
    public void T332StringFormat_NullProvider_ShouldKeepExistingBehavior()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var literalNull = SqlOf(ctx, e.Select(x => string.Format((IFormatProvider?)null, "{0}", x.Id)));
        var defaultValue = SqlOf(ctx, e.Select(x => string.Format(default(IFormatProvider?), "{0}", x.Id)));
        var withoutProvider = SqlOf(ctx, e.Select(x => string.Format("{0}", x.Id)));

        literalNull.Should().Be(withoutProvider);
        defaultValue.Should().Be(withoutProvider);

        var valueAndReference = SqlOf(ctx, e.Select(x => string.Format((IFormatProvider?)null, "{0}-{1}", x.Id, x.String)));

        valueAndReference.Should().Contain("somestring");
    }

    [Theory]
    [InlineData(T332ProviderArgument.ConstantEnglishInstance)]
    [InlineData(T332ProviderArgument.ConstantDistinctInvariant)]
    [InlineData(T332ProviderArgument.CurrentCulture)]
    [InlineData(T332ProviderArgument.CapturedInvariant)]
    [InlineData(T332ProviderArgument.LookalikeProperty)]
    public void T332StringFormat_NonCanonicalProvider_ShouldRejectWithExactMessage(T332ProviderArgument argument)
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(UnsupportedProviderCase(argument)));

        act.Should().Throw<NotSupportedException>().WithMessage(RejectionMessage);
        T332CultureLookalike.Invocations.Should().Be(0);
    }

    [Fact]
    public void T332StringFormat_UnsupportedFormat_ShouldKeepRefusing()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(ConstantProviderOne("{0:F2}")));

        act.Should().Throw<NotSupportedException>().WithMessage("*not supported*");
    }

    [Fact]
    public void T332StringFormat_NonConstantFormat_ShouldKeepRefusing()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var format = "{0}";

        var act = () => SqlOf(ctx, e.Select(x => string.Format((IFormatProvider?)null, format, x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("string.Format requires a constant format string.");
    }

    [Fact]
    public void T332StringFormat_CompositeFormatOverload_ShouldKeepRefusing()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => string.Format((IFormatProvider?)null, System.Text.CompositeFormat.Parse("{0}"), x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("string.Format requires a constant format string.");
    }

    private static Expression<Func<IComplexEntity, string>> ConstantProviderOne(string format)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var body = Expression.Call(
            FormatMethod(typeof(object)),
            Expression.Constant(CultureInfo.InvariantCulture),
            Expression.Constant(format),
            IdArgument(parameter));
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }

    private static Expression<Func<IComplexEntity, string>> ConstantProviderTwo(string format)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var body = Expression.Call(
            FormatMethod(typeof(object), typeof(object)),
            Expression.Constant(CultureInfo.InvariantCulture),
            Expression.Constant(format),
            IdArgument(parameter),
            IdArgument(parameter));
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }

    private static Expression<Func<IComplexEntity, string>> ConstantProviderThree(string format)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var body = Expression.Call(
            FormatMethod(typeof(object), typeof(object), typeof(object)),
            Expression.Constant(CultureInfo.InvariantCulture),
            Expression.Constant(format),
            IdArgument(parameter),
            IdArgument(parameter),
            IdArgument(parameter));
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }

    private static Expression<Func<IComplexEntity, string>> ConstantProviderParams(string format)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var array = Expression.NewArrayInit(
            typeof(object),
            IdArgument(parameter),
            BoxedInt(1),
            BoxedInt(2),
            BoxedInt(3));
        var body = Expression.Call(
            FormatMethod(typeof(object[])),
            Expression.Constant(CultureInfo.InvariantCulture),
            Expression.Constant(format),
            array);
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }

    private static Expression<Func<IComplexEntity, string>> UnsupportedProviderCase(T332ProviderArgument argument)
        => argument switch
        {
            T332ProviderArgument.ConstantEnglishInstance => ConstantProvider(CultureInfo.GetCultureInfo("en-US")),
            // Value-equal to the invariant culture but a distinct instance: the contract is reference identity.
            T332ProviderArgument.ConstantDistinctInvariant => ConstantProvider(new CultureInfo("")),
            T332ProviderArgument.CurrentCulture => x => string.Format(CultureInfo.CurrentCulture, "{0}", x.Id),
            T332ProviderArgument.CapturedInvariant => CapturedInvariantCase(),
            T332ProviderArgument.LookalikeProperty => LookalikeProviderCase(),
            _ => throw new ArgumentOutOfRangeException(nameof(argument)),
        };

    private static Expression<Func<IComplexEntity, string>> ConstantProvider(CultureInfo provider)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var body = Expression.Call(
            FormatMethod(typeof(object)),
            Expression.Constant(provider),
            Expression.Constant("{0}"),
            IdArgument(parameter));
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }

    /// <summary>A captured (closure field) invariant culture: a member access, not a constant.</summary>
    private static Expression<Func<IComplexEntity, string>> CapturedInvariantCase()
    {
        var captured = CultureInfo.InvariantCulture;
        return x => string.Format(captured, "{0}", x.Id);
    }

    /// <summary>
    /// A lookalike static <c>InvariantCulture</c> property on a type that is not <see cref="CultureInfo"/>.
    /// Its getter throws, so the exact <see cref="NotSupportedException"/> proves the translator never
    /// evaluated the argument.
    /// </summary>
    private static Expression<Func<IComplexEntity, string>> LookalikeProviderCase()
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var lookalike = Expression.Property(null, typeof(T332CultureLookalike).GetProperty(nameof(T332CultureLookalike.InvariantCulture))!);
        var body = Expression.Call(
            FormatMethod(typeof(object)),
            lookalike,
            Expression.Constant("{0}"),
            IdArgument(parameter));
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }

    private static MethodInfo FormatMethod(params Type[] valueTypes)
        => typeof(string).GetMethod(
            nameof(string.Format),
            [typeof(IFormatProvider), typeof(string), .. valueTypes])!;

    private static Expression IdArgument(ParameterExpression parameter)
        => Expression.Convert(Expression.Property(parameter, nameof(IComplexEntity.Id)), typeof(object));

    private static Expression BoxedInt(int value) => Expression.Convert(Expression.Constant(value), typeof(object));

    public enum T332ProviderArgument
    {
        ConstantEnglishInstance,
        ConstantDistinctInvariant,
        CurrentCulture,
        CapturedInvariant,
        LookalikeProperty,
    }

    private static class T332CultureLookalike
    {
        public static int Invocations { get; private set; }

        public static CultureInfo InvariantCulture
        {
            get
            {
                Invocations++;
                throw new InvalidOperationException("getter must not be evaluated");
            }
        }
    }
}
