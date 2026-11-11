using FluentAssertions;
using NextORM.Core;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation for the C# string semantics surface: the format subset, ordinal
/// <see cref="StringComparison"/>, collation and the fail-fast contract. No database is touched.
/// </summary>
public class StringSemanticsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static void AssertUnsupported<T>(IDataContext ctx, QueryCommand<T> cmd, string messageFragment)
    {
        var act = () => SqlOf(ctx, cmd);
        act.Should().Throw<NotSupportedException>().WithMessage($"*{messageFragment}*");
    }

    [Fact]
    public void ToString_DateFormat_ShouldRenderToChar()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = x.Datetime!.Value.ToString("yyyy-MM-dd") }))
            .Should().Be("select to_char(dt, 'YYYY-MM-DD') as \"F\"\n from complex_entity");
    }

    [Fact]
    public void Interpolation_DateFormat_ShouldRenderToChar()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = $"{x.Datetime:yyyy-MM-dd}" }))
            .Should().Be("select to_char(dt, 'YYYY-MM-DD') as \"F\"\n from complex_entity");
    }

    [Fact]
    public void StringFormat_LiteralAndFixedNumber_ShouldConcat()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = string.Format("Id={0:F2}", x.Id) }))
            .Should().Be("select (E'Id='||to_char(id, 'FM9999999999999990.' || repeat('0', 2))) as \"F\"\n from complex_entity");
    }

    [Fact]
    public void Collate_ShouldRenderCollateClause()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = SqlFunctions.Sql.collate(x.String, "C") }))
            .Should().Be("select somestring collate \"C\" as \"F\"\n from complex_entity");
    }

    [Fact]
    public void CompareOrdinalInWhere_ShouldRenderSignedCase()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.CompareOrdinal(x.String, "a") > 0).Select(x => new { x.Id }))
            .Should().Be("select id\n from complex_entity\n where (case when somestring collate \"C\" < E'a' collate \"C\" then -1 when somestring collate \"C\" > E'a' collate \"C\" then 1 else 0 end > 0)");
    }

    [Fact]
    public void ContainsOrdinal_ShouldRenderBinaryLike()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Contains("a", StringComparison.Ordinal)).Select(x => new { x.Id }))
            .Should().Be("select id\n from complex_entity\n where somestring collate \"C\" like E'%a%'");
    }

    [Fact]
    public void CompareWithoutComparison_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Where(x => string.Compare(x.String, "a") > 0).Select(x => new { x.Id }), "culture-sensitive");
    }

    [Fact]
    public void ToUpperWithCulture_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { F = x.String!.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) }), "InvariantCulture");
    }

    [Fact]
    public void UnsupportedNumberSpecifier_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { F = string.Format("{0:N2}", x.Id) }), "not supported by this provider");
    }

    [Fact]
    public void EqualsOrdinalIgnoreCase_ShouldCaseFoldAndUseBinary()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Equals("a", StringComparison.OrdinalIgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("lower(somestring) collate \"C\" = lower(E'a') collate \"C\"");
    }

    [Fact]
    public void StartsWithOrdinalIgnoreCase_ShouldCaseFoldBothSides()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.StartsWith("a", StringComparison.OrdinalIgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("lower(somestring) collate \"C\" like lower(E'a%')");
    }

    [Fact]
    public void TrimChar_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { F = x.String!.Trim('x') }), "whitespace only");
    }

    [Fact]
    public void FormatWithCurrentCultureProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(
            ctx,
            e.Select(x => new { F = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0}", x.Id) }),
            "InvariantCulture");
    }

    [Fact]
    public void ToUpperInvariant_ShouldMapToUpper()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = x.String!.ToUpperInvariant() }))
            .Should().Be("select upper(somestring) as \"F\"\n from complex_entity");
    }

    [Fact]
    public void IndexOfWithCount_ShouldThrow()
    {
        // IndexOf(string, int startIndex, int count) has no primitive here; the count must not be
        // mistaken for the start index.
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Where(x => x.String!.IndexOf("a", 1, 2) > 0).Select(x => new { x.Id }), "IndexOf");
    }

    [Fact]
    public void RegexIsMatch_ShouldUseMatchOperator()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a")).Select(x => new { x.Id }))
            .Should().Contain("somestring ~ '^a'");
    }

    [Fact]
    public void RegexIsMatchIgnoreCase_ShouldUseCaseInsensitiveOperator()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a", RegexOptions.IgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("somestring ~* '^a'");
    }

    [Fact]
    public void RegexReplace_ShouldUseRegexpReplace()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "[0-9]+", "#") }))
            .Should().Contain("regexp_replace(somestring, '[0-9]+', '#', 'g')");
    }

    [Fact]
    public void RegexReplaceIgnoreCase_ShouldUseGlobalCaseInsensitiveFlags()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "a", "#", RegexOptions.IgnoreCase) }))
            .Should().Contain("regexp_replace(somestring, 'a', '#', 'gi')");
    }

    [Fact]
    public void RegexInstance_ShouldUseReceiverPattern()
    {
        var regex = new Regex("^a", RegexOptions.IgnoreCase);
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => regex.IsMatch(x.String!)).Select(x => new { x.Id }))
            .Should().Contain("somestring ~* '^a'");
    }

    [Fact]
    public void RegexNewInstance_ShouldUseConstructorPattern()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => new Regex("^a").IsMatch(x.String!)).Select(x => new { x.Id }))
            .Should().Contain("somestring ~ '^a'");
    }

    [Fact]
    public void RegexInstanceReplace_ShouldUseReceiverPatternAndOptions()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = new Regex("^a", RegexOptions.IgnoreCase).Replace(x.String!, "#") }))
            .Should().Contain("regexp_replace(somestring, '^a', '#', 'gi')");
    }

    [Fact]
    public void RegexConstantInput_ShouldRenderInline()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.IsMatch("abc", "b"), x.Id }))
            .Should().Contain("'abc' ~ 'b'");
    }

    [Fact]
    public void RegexUnsupportedMethod_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(
            ctx,
            e.Where(x => Regex.Escape(x.String!) == "a").Select(x => new { x.Id }),
            "only Regex.IsMatch and Regex.Replace");
    }

    [Fact]
    public void RegexNonConstantPattern_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var pattern = "^a";

        AssertUnsupported(ctx, e.Where(x => Regex.IsMatch(x.String!, pattern)).Select(x => new { x.Id }), "compile-time constant");
    }

    [Fact]
    public void RegexUnsupportedOptions_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(
            ctx,
            e.Where(x => Regex.IsMatch(x.String!, "^a", RegexOptions.Multiline)).Select(x => new { x.Id }),
            "RegexOptions");
    }

    [Theory]
    [InlineData(nameof(string.ToUpper), "upper(")]
    [InlineData(nameof(string.ToLower), "lower(")]
    public void CultureCase_NaturalInvariant_ShouldRenderUpperAndLower(string methodName, string function)
    {
        using var ctx = PostgresTestContext.Create();
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
        using var ctx = PostgresTestContext.Create();
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
        using var ctx = PostgresTestContext.Create();
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
        using var ctx = PostgresTestContext.Create();
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
