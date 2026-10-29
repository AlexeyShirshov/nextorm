using FluentAssertions;
using NextORM.Core;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace NextORM.SqlServer.Tests;

/// <summary>SQL generation for the C# string semantics surface on SQL Server.</summary>
public class StringSemanticsSqlGenerationTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void DateFormat_ShouldUseFormat()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = x.Datetime!.Value.ToString("yyyy-MM-dd") }))
            .Should().Contain("format(dt, 'yyyy-MM-dd')");
    }

    [Fact]
    public void NumberFormat_ShouldUseFormat()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = string.Format("{0:N2}", x.Id) }))
            .Should().Contain("format(id, 'N2')");
    }

    [Fact]
    public void Collate_ShouldRenderCollate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = SqlFunctions.Sql.collate(x.String, "Latin1_General_100_BIN2") }))
            .Should().Contain("somestring collate Latin1_General_100_BIN2");
    }

    [Fact]
    public void CompareOrdinal_ShouldUseBinaryCollation()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.CompareOrdinal(x.String, "a") > 0).Select(x => new { x.Id }))
            .Should().Contain("somestring collate Latin1_General_100_BIN2 < N'a'");
    }

    [Fact]
    public void ContainsOrdinal_ShouldUseBinaryCollation()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Contains("a", StringComparison.Ordinal)).Select(x => new { x.Id }))
            .Should().Contain("somestring collate Latin1_General_100_BIN2 like N'%a%'");
    }

    [Fact]
    public void RegexIsMatch_ShouldUseRegexpLikeWithCaseSensitiveFlag()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a")).Select(x => new { x.Id }))
            .Should().Contain("regexp_like(somestring, '^a', 'c')");
    }

    [Fact]
    public void RegexIsMatchIgnoreCase_ShouldUseRegexpLikeWithIgnoreCaseFlag()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a", RegexOptions.IgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("regexp_like(somestring, '^a', 'i')");
    }

    [Fact]
    public void RegexReplace_ShouldUseRegexpReplace()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "[0-9]+", "#") }))
            .Should().Contain("regexp_replace(somestring, '[0-9]+', '#', 1, 0, 'c')");
    }

    [Fact]
    public void RegexReplaceIgnoreCase_ShouldUseIgnoreCaseFlag()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "a", "#", RegexOptions.IgnoreCase) }))
            .Should().Contain("regexp_replace(somestring, 'a', '#', 1, 0, 'i')");
    }

    [Theory]
    [InlineData(nameof(string.ToUpper), "upper(")]
    [InlineData(nameof(string.ToLower), "lower(")]
    public void CultureCase_NaturalInvariant_ShouldRenderUpperAndLower(string methodName, string function)
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(NaturalCultureCase(methodName)));

        sql.Should().Contain(function);
        sql.Should().Contain("somestring");
    }

    [Theory]
    [InlineData(nameof(string.ToUpper), "upper(")]
    [InlineData(nameof(string.ToLower), "lower(")]
    public void CultureCase_ConstantInvariant_ShouldRenderUpperAndLower(string methodName, string function)
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(ConstantCultureCase(methodName)));

        sql.Should().Contain(function);
        sql.Should().Contain("somestring");
    }

    [Theory]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.CapturedInvariant)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.CapturedInvariant)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.CapturedEnglish)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.CapturedEnglish)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.CapturedTurkish)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.CapturedTurkish)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.CapturedNull)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.CapturedNull)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.NewEnglish)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.NewEnglish)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.NewTurkish)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.NewTurkish)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.ConstantEnglishInstance)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.ConstantEnglishInstance)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.ConstantCachedEnglish)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.ConstantCachedEnglish)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.ConstantDistinctInvariant)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.ConstantDistinctInvariant)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.CurrentCulture)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.CurrentCulture)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.CurrentUICulture)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.CurrentUICulture)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.GetCultureRussian)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.GetCultureRussian)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.GetCultureInvariant)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.GetCultureInvariant)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.LiteralNull)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.LiteralNull)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.DefaultNull)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.DefaultNull)]
    [InlineData(nameof(string.ToUpper), CultureCaseArgument.LookalikeProperty)]
    [InlineData(nameof(string.ToLower), CultureCaseArgument.LookalikeProperty)]
    public void CultureCase_UnsupportedArguments_ShouldRejectWithExactMessage(string methodName, CultureCaseArgument argument)
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var message = $"string.{methodName}(CultureInfo) is only supported with CultureInfo.InvariantCulture; other cultures have no portable SQL form.";

        var act = () => SqlOf(ctx, e.Select(UnsupportedCultureCase(methodName, argument)));

        act.Should().Throw<NotSupportedException>().WithMessage(message);
    }

    [Theory]
    [InlineData(nameof(string.ToUpper), "upper(")]
    [InlineData(nameof(string.ToLower), "lower(")]
    public void CultureCase_NullReceiver_ShouldRenderUpperAndLower(string methodName, string function)
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(NullReceiverCultureCase(methodName)));

        sql.Should().Contain(function);
        sql.Should().Contain("null");
    }

    /// <summary>
    /// The natural C# spelling: <c>x.String.ToUpper(CultureInfo.InvariantCulture)</c> binds a static
    /// property <see cref="MemberExpression"/>, not a <see cref="ConstantExpression"/>.
    /// </summary>
    private static Expression<Func<IComplexEntity, string>> NaturalCultureCase(string methodName)
        => UpperOrLower(
            methodName,
            x => x.String!.ToUpper(CultureInfo.InvariantCulture),
            x => x.String!.ToLower(CultureInfo.InvariantCulture));

    /// <summary>
    /// The programmatic constant shape accepted since before the defect: an explicit
    /// <see cref="Expression.Constant"/> holding the invariant culture instance.
    /// </summary>
    private static Expression<Func<IComplexEntity, string>> ConstantCultureCase(string methodName)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var text = Expression.Property(parameter, nameof(IComplexEntity.String));
        var method = typeof(string).GetMethod(methodName, [typeof(CultureInfo)])!;
        var body = Expression.Call(text, method, Expression.Constant(CultureInfo.InvariantCulture));
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }

    private static Expression<Func<IComplexEntity, string>> UnsupportedCultureCase(string methodName, CultureCaseArgument argument)
        => argument switch
        {
            CultureCaseArgument.CapturedInvariant => CapturedCultureCase(methodName, CultureInfo.InvariantCulture),
            CultureCaseArgument.CapturedEnglish => CapturedCultureCase(methodName, CultureInfo.GetCultureInfo("en-US")),
            CultureCaseArgument.CapturedTurkish => CapturedCultureCase(methodName, CultureInfo.GetCultureInfo("tr-TR")),
            CultureCaseArgument.CapturedNull => CapturedCultureCase(methodName, null),
            CultureCaseArgument.NewEnglish => NewCultureCase(methodName, "en-US"),
            CultureCaseArgument.NewTurkish => NewCultureCase(methodName, "tr-TR"),
            CultureCaseArgument.ConstantEnglishInstance => ConstantArgumentCultureCase(methodName, new CultureInfo("en-US")),
            CultureCaseArgument.ConstantCachedEnglish => ConstantArgumentCultureCase(methodName, CultureInfo.GetCultureInfo("en-US")),
            // Value-equal to the invariant culture but a distinct instance: the contract is reference identity.
            CultureCaseArgument.ConstantDistinctInvariant => ConstantArgumentCultureCase(methodName, new CultureInfo("")),
            CultureCaseArgument.CurrentCulture => AmbientCultureCase(methodName, uiCulture: false),
            CultureCaseArgument.CurrentUICulture => AmbientCultureCase(methodName, uiCulture: true),
            CultureCaseArgument.GetCultureRussian => MethodCultureCase(methodName, "ru-RU"),
            CultureCaseArgument.GetCultureInvariant => MethodCultureCase(methodName, ""),
            CultureCaseArgument.LiteralNull => UpperOrLower(
                methodName,
                x => x.String!.ToUpper(null!),
                x => x.String!.ToLower(null!)),
            CultureCaseArgument.DefaultNull => UpperOrLower(
                methodName,
                x => x.String!.ToUpper(default(CultureInfo)!),
                x => x.String!.ToLower(default(CultureInfo)!)),
            CultureCaseArgument.LookalikeProperty => LookalikeCultureCase(methodName),
            _ => throw new ArgumentOutOfRangeException(nameof(argument)),
        };

    private static Expression<Func<IComplexEntity, string>> UpperOrLower(
        string methodName,
        Expression<Func<IComplexEntity, string>> upper,
        Expression<Func<IComplexEntity, string>> lower)
        => methodName == nameof(string.ToUpper) ? upper : lower;

    private static Expression<Func<IComplexEntity, string>> CapturedCultureCase(string methodName, CultureInfo? culture)
    {
        var captured = culture;
        return UpperOrLower(
            methodName,
            x => x.String!.ToUpper(captured!),
            x => x.String!.ToLower(captured!));
    }

    private static Expression<Func<IComplexEntity, string>> NewCultureCase(string methodName, string cultureName)
        => UpperOrLower(
            methodName,
            x => x.String!.ToUpper(new CultureInfo(cultureName)),
            x => x.String!.ToLower(new CultureInfo(cultureName)));

    private static Expression<Func<IComplexEntity, string>> MethodCultureCase(string methodName, string cultureName)
        => UpperOrLower(
            methodName,
            x => x.String!.ToUpper(CultureInfo.GetCultureInfo(cultureName)),
            x => x.String!.ToLower(CultureInfo.GetCultureInfo(cultureName)));

    private static Expression<Func<IComplexEntity, string>> AmbientCultureCase(string methodName, bool uiCulture)
        => uiCulture
            ? UpperOrLower(
                methodName,
                x => x.String!.ToUpper(CultureInfo.CurrentUICulture),
                x => x.String!.ToLower(CultureInfo.CurrentUICulture))
            : UpperOrLower(
                methodName,
                x => x.String!.ToUpper(CultureInfo.CurrentCulture),
                x => x.String!.ToLower(CultureInfo.CurrentCulture));

    private static Expression<Func<IComplexEntity, string>> ConstantArgumentCultureCase(string methodName, CultureInfo culture)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var text = Expression.Property(parameter, nameof(IComplexEntity.String));
        var method = typeof(string).GetMethod(methodName, [typeof(CultureInfo)])!;
        var body = Expression.Call(text, method, Expression.Constant(culture));
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }

    /// <summary>
    /// A lookalike static <c>InvariantCulture</c> property on a type that is not <see cref="CultureInfo"/>.
    /// Its getter throws, so the exact <see cref="NotSupportedException"/> proves the translator never
    /// evaluated the argument.
    /// </summary>
    private static Expression<Func<IComplexEntity, string>> LookalikeCultureCase(string methodName)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var text = Expression.Property(parameter, nameof(IComplexEntity.String));
        var method = typeof(string).GetMethod(methodName, [typeof(CultureInfo)])!;
        var lookalike = Expression.Property(null, typeof(CultureLookalike).GetProperty(nameof(CultureLookalike.InvariantCulture))!);
        var body = Expression.Call(text, method, lookalike);
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }

    private static Expression<Func<IComplexEntity, string>> NullReceiverCultureCase(string methodName)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var method = typeof(string).GetMethod(methodName, [typeof(CultureInfo)])!;
        var body = Expression.Call(
            Expression.Constant(null, typeof(string)),
            method,
            Expression.Constant(CultureInfo.InvariantCulture));
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }

    public enum CultureCaseArgument
    {
        CapturedInvariant,
        CapturedEnglish,
        CapturedTurkish,
        CapturedNull,
        NewEnglish,
        NewTurkish,
        ConstantEnglishInstance,
        ConstantCachedEnglish,
        ConstantDistinctInvariant,
        CurrentCulture,
        CurrentUICulture,
        GetCultureRussian,
        GetCultureInvariant,
        LiteralNull,
        DefaultNull,
        LookalikeProperty,
    }

    private static class CultureLookalike
    {
        public static CultureInfo InvariantCulture =>
            throw new InvalidOperationException("getter must not be evaluated");
    }
}
