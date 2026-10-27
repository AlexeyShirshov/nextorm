using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// The residual PostgreSQL string and numeric functions of #281 (<see cref="SqlFunctions.Postgres"/>)
/// have no equivalent on this provider, so its dialect rejects each name during preparation instead of
/// rendering PostgreSQL SQL. The refused members (<c>casefold</c>, <c>gamma</c>/<c>tgamma</c>/<c>lgamma</c>,
/// one-argument <c>to_ascii</c>) keep their technical reason here too, because the reason is a property
/// of the pinned target server, not of a provider.
/// </summary>
public class PostgresFunctionsRejectionTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    [Trait("Issue", "281")]
    public void StringFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("btrim", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.btrim(x.String) }))),
            ("ltrim", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.ltrim(x.String, "xy") }))),
            ("rtrim", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.rtrim(x.String, "xy") }))),
            ("starts_with", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.starts_with(x.String, "d") }))),
            ("parse_ident", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.parse_ident("a.b", false) }))),
            ("quote_ident", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.quote_ident("a b") }))),
            ("quote_literal", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.quote_literal(x.String) }))),
            ("quote_nullable", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.quote_nullable(x.String) }))),
            ("to_ascii", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_ascii(x.String, "LATIN1") }))),
            ("to_hex", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_hex(x.Int) }))),
            ("to_oct", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_oct(x.Int) }))),
            ("to_bin", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_bin(x.Int) }))),
            ("convert_from", () => SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.Postgres.convert_from(SqlFunctions.Postgres.convert_to(x.String, "UTF8"), "UTF8")
            }))),
            ("convert_to", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.convert_to(x.String, "UTF8") }))),
            ("encode", () => SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.Postgres.encode(SqlFunctions.Postgres.convert_to(x.String, "UTF8"), "hex")
            }))),
            ("decode", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.decode(x.String, "hex") }))),
            ("normalize", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.normalize(x.String, "NFC") }))),
            ("unistr", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.unistr(x.String) }))),
            ("regexp_match", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.regexp_match(x.String, "a") })))
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void NumericFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("div", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.div(7m, 2m) }))),
            ("erf", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.erf(0.5) }))),
            ("erfc", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.erfc(0.5) }))),
            ("random_normal", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.random_normal(0.0, 1.0) }))),
            ("acosd", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.acosd(0.5) }))),
            ("asind", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.asind(0.5) }))),
            ("atand", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.atand(0.5) }))),
            ("atan2d", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.atan2d(1.0, 1.0) }))),
            ("cosd", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.cosd(60.0) }))),
            ("cotd", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.cotd(45.0) }))),
            ("sind", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.sind(30.0) }))),
            ("tand", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.tand(45.0) })))
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void RefusedMembers_ShouldCarryTheirReason()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var casefold = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.casefold(x.String) }));
        var gamma = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.gamma(1.5) }));
        var tgamma = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.tgamma(1.5) }));
        var lgamma = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.lgamma(1.5) }));
        var toAsciiOneArg = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_ascii(x.String) }));

        casefold.Should().Throw<NotSupportedException>().WithMessage("*casefold*refused*");
        gamma.Should().Throw<NotSupportedException>().WithMessage("*gamma*refused*");
        tgamma.Should().Throw<NotSupportedException>().WithMessage("*tgamma*refused*");
        lgamma.Should().Throw<NotSupportedException>().WithMessage("*lgamma*refused*");
        toAsciiOneArg.Should().Throw<NotSupportedException>().WithMessage("*to_ascii*refused*0A000*");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void Families_ShouldNotBeAdvertised()
    {
        var scalars = SqliteDialect.Instance.ScalarFunctions;

        foreach (var name in new[]
        {
            "btrim", "ltrim", "rtrim", "starts_with", "parse_ident", "quote_ident", "quote_literal",
            "quote_nullable", "to_ascii", "to_hex", "to_oct", "to_bin", "convert_from", "convert_to",
            "encode", "decode", "normalize", "unistr", "regexp_match",
            "div", "random_normal", "acosd", "asind", "atand", "atan2d", "cosd", "cotd", "sind", "tand"
        })
        {
            scalars.Supports(name).Should().BeFalse($"'{name}' is PostgreSQL-only");
        }
    }
}
