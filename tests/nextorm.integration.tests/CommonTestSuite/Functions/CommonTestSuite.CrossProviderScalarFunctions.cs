using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes the cross-provider scalar functions every SQL provider can express
/// (<c>left</c>/<c>right</c>, <c>concat_ws</c>, <c>ascii</c>/<c>char</c>, <c>mod</c>, <c>log10</c>,
/// <c>power</c>) against the seeded <c>ComplexEntity</c>. The functions that only some providers have
/// (<c>lpad</c>/<c>rpad</c>, <c>repeat</c>, <c>reverse</c>, <c>space</c>, <c>translate</c>) are covered
/// per provider by the SQL-generation tests and, for PostgreSQL, by <see cref="PostgresFunctionsTests"/>.
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void CrossProviderScalarFunctions_ShouldCompute()
    {
        // A captured variable is bound as a string parameter, so SQL Server's non-Unicode inline
        // literal handling does not mangle the multi-byte value.
        var cyrillicLeft = "Привет";
        var cyrillicRight = "Привет";

        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                L = SqlFunctions.Sql.left("abcdef", 3),
                R = SqlFunctions.Sql.right("abcdef", 3),
                Lru = SqlFunctions.Sql.left(cyrillicLeft, 3),
                Rru = SqlFunctions.Sql.right(cyrillicRight, 3),
                Cw = SqlFunctions.Sql.concat_ws("-", "a", "b", "c"),
                Asc = SqlFunctions.Sql.ascii("A"),
                Ch = SqlFunctions.Sql.@char(65)
            })
            .First();

        r.L.Should().Be("abc");
        r.R.Should().Be("def");
        r.Lru.Should().Be("При");
        r.Rru.Should().Be("вет");
        r.Cw.Should().Be("a-b-c");
        r.Asc.Should().Be(65);
        r.Ch.Should().Be("A");
    }

    [Fact]
    public void CrossProviderScalarFunctions_ConcatWsNull_ShouldSkipNulls()
    {
        // On every provider except ClickHouse, concat_ws skips NULL arguments (ClickHouse's
        // concatWithSeparator returns NULL instead; pinned in ClickHouseScalarFunctionsTests).
        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => SqlFunctions.Sql.concat_ws("-", "a", null, "b"))
            .First();

        r.Should().Be("a-b");
    }

    [Fact]
    public void CrossProviderScalarFunctions_NumericAndLength_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                Bl = SqlFunctions.Sql.bit_length("abc"),
                Ol = SqlFunctions.Sql.octet_length("abc"),
                Deg = SqlFunctions.Sql.degrees(Math.PI),
                Rad = SqlFunctions.Sql.radians(180.0),
                Pi = SqlFunctions.Sql.pi(),
                Acos = Math.Acos(1.0),
                Atan2 = Math.Atan2(1.0, 1.0)
            })
            .First();

        // SQL Server renders the inline literal as N'abc' (SqlStringLiteralPolicy.cs:219-267), so
        // datalength(N'abc') counts six UTF-16 bytes: bit_length doubles that to 48 and octet_length
        // keeps 6 (SqlServerDialect.cs:1011-1012). Every other provider measures the three ASCII bytes.
        if (Provider.Name == "sqlserver")
        {
            r.Bl.Should().Be(48);
            r.Ol.Should().Be(6);
        }
        else
        {
            r.Bl.Should().Be(24);
            r.Ol.Should().Be(3);
        }
        ((double?)r.Deg).GetValueOrDefault().Should().BeApproximately(180.0, 1e-6);
        ((double?)r.Rad).GetValueOrDefault().Should().BeApproximately(Math.PI, 1e-6);
        ((double?)r.Pi).GetValueOrDefault().Should().BeApproximately(Math.PI, 1e-6);
        ((double?)r.Acos).GetValueOrDefault().Should().BeApproximately(0.0, 1e-6);
        ((double?)r.Atan2).GetValueOrDefault().Should().BeApproximately(Math.PI / 4, 1e-6);
    }

    [Fact]
    [Trait("Issue", "281")]
    public void CrossProviderScalarFunctions_NumericScale_ShouldMatchProviderSupport()
    {
        var scale = () => _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new { V = SqlFunctions.Postgres.scale(12.3400m) })
            .First();
        var minScale = () => _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new { V = SqlFunctions.Postgres.min_scale(12.3400m) })
            .First();
        var trimScale = () => _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new { V = SqlFunctions.Postgres.trim_scale(12.3400m) })
            .First();

        if (Provider.Name == "postgres")
        {
            scale().V.Should().Be(4);
            minScale().V.Should().Be(2);
            trimScale().V.Should().Be(12.34m);
            return;
        }

        scale.Should().Throw<NotSupportedException>().WithMessage("*scale*not supported*");
        minScale.Should().Throw<NotSupportedException>().WithMessage("*min_scale*not supported*");
        trimScale.Should().Throw<NotSupportedException>().WithMessage("*trim_scale*not supported*");
    }

    /// <summary>
    /// The residual string and numeric names of #281 are enrolled in the shared scalar path, so each one
    /// runs on every provider: PostgreSQL executes and maps it, every other provider refuses it during
    /// preparation with a message naming the function. The refused members (<c>casefold</c>,
    /// <c>gamma</c>/<c>tgamma</c>/<c>lgamma</c>, one-argument <c>to_ascii</c>) carry their technical
    /// reason on every provider, because the reason is a property of the target server, not of a
    /// provider.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void CrossProviderScalarFunctions_PostgresResidualFamilies_ShouldMatchProviderSupport()
    {
        var delivered = new (string Name, Func<object?> Run, Type Expected)[]
        {
            ("btrim", () => Row(e => new { V = SqlFunctions.Postgres.btrim("  ab  ") }).V, typeof(string)),
            ("ltrim", () => Row(e => new { V = SqlFunctions.Postgres.ltrim("xxab", "x") }).V, typeof(string)),
            ("rtrim", () => Row(e => new { V = SqlFunctions.Postgres.rtrim("abxx", "x") }).V, typeof(string)),
            ("starts_with", () => Row(e => new { V = SqlFunctions.Postgres.starts_with("dadfasd", "dad") }).V, typeof(bool)),
            ("parse_ident", () => Row(e => new { V = SqlFunctions.Postgres.parse_ident("a.b", false) }).V, typeof(string[])),
            ("quote_ident", () => Row(e => new { V = SqlFunctions.Postgres.quote_ident("a b") }).V, typeof(string)),
            ("quote_literal", () => Row(e => new { V = SqlFunctions.Postgres.quote_literal("a") }).V, typeof(string)),
            ("quote_nullable", () => Row(e => new { V = SqlFunctions.Postgres.quote_nullable("a") }).V, typeof(string)),
            ("to_ascii", () => Row(e => new { V = SqlFunctions.Postgres.to_ascii("abc", "LATIN1") }).V, typeof(string)),
            ("to_hex", () => Row(e => new { V = SqlFunctions.Postgres.to_hex(-1) }).V, typeof(string)),
            ("to_oct", () => Row(e => new { V = SqlFunctions.Postgres.to_oct(8) }).V, typeof(string)),
            ("to_bin", () => Row(e => new { V = SqlFunctions.Postgres.to_bin(5) }).V, typeof(string)),
            ("convert_from", () => Row(e => new
            {
                V = SqlFunctions.Postgres.convert_from(SqlFunctions.Postgres.convert_to("abc", "UTF8"), "UTF8")
            }).V, typeof(string)),
            ("convert_to", () => Row(e => new { V = SqlFunctions.Postgres.convert_to("abc", "UTF8") }).V, typeof(byte[])),
            ("encode", () => Row(e => new
            {
                V = SqlFunctions.Postgres.encode(SqlFunctions.Postgres.convert_to("abc", "UTF8"), "hex")
            }).V, typeof(string)),
            ("decode", () => Row(e => new { V = SqlFunctions.Postgres.decode("616263", "hex") }).V, typeof(byte[])),
            ("normalize", () => Row(e => new { V = SqlFunctions.Postgres.normalize("abc", "NFC") }).V, typeof(string)),
            ("unistr", () => Row(e => new { V = SqlFunctions.Postgres.unistr("a\\0441") }).V, typeof(string)),
            ("regexp_match", () => Row(e => new { V = SqlFunctions.Postgres.regexp_match("dadfasd", "(d)(a)") }).V, typeof(string[])),
            ("div", () => Row(e => new { V = SqlFunctions.Postgres.div(7m, 2m) }).V, typeof(decimal)),
            ("erf", () => Row(e => new { V = SqlFunctions.Postgres.erf(0.0) }).V, typeof(double)),
            ("erfc", () => Row(e => new { V = SqlFunctions.Postgres.erfc(0.0) }).V, typeof(double)),
            ("random_normal", () => Row(e => new { V = SqlFunctions.Postgres.random_normal(0.0, 0.0) }).V, typeof(double)),
            ("acosd", () => Row(e => new { V = SqlFunctions.Postgres.acosd(1.0) }).V, typeof(double)),
            ("asind", () => Row(e => new { V = SqlFunctions.Postgres.asind(1.0) }).V, typeof(double)),
            ("atand", () => Row(e => new { V = SqlFunctions.Postgres.atand(1.0) }).V, typeof(double)),
            ("atan2d", () => Row(e => new { V = SqlFunctions.Postgres.atan2d(1.0, 1.0) }).V, typeof(double)),
            ("cosd", () => Row(e => new { V = SqlFunctions.Postgres.cosd(60.0) }).V, typeof(double)),
            ("cotd", () => Row(e => new { V = SqlFunctions.Postgres.cotd(45.0) }).V, typeof(double)),
            ("sind", () => Row(e => new { V = SqlFunctions.Postgres.sind(30.0) }).V, typeof(double)),
            ("tand", () => Row(e => new { V = SqlFunctions.Postgres.tand(45.0) }).V, typeof(double))
        };

        var refused = new (string Name, Func<object?> Run)[]
        {
            ("casefold", () => Row(e => new { V = SqlFunctions.Postgres.casefold("A") }).V),
            ("gamma", () => Row(e => new { V = SqlFunctions.Postgres.gamma(1.5) }).V),
            ("tgamma", () => Row(e => new { V = SqlFunctions.Postgres.tgamma(1.5) }).V),
            ("lgamma", () => Row(e => new { V = SqlFunctions.Postgres.lgamma(1.5) }).V),
            ("to_ascii", () => Row(e => new { V = SqlFunctions.Postgres.to_ascii("abc") }))
        };

        if (Provider.Name == "postgres")
        {
            foreach (var (name, run, expected) in delivered)
            {
                var value = run();
                value.Should().NotBeNull($"'{name}' is delivered on PostgreSQL");
                value.Should().BeOfType(expected, $"'{name}' maps to {expected.Name}");
            }

            foreach (var (name, run) in refused)
            {
                var act = () => { _ = run(); };
                act.Should().Throw<NotSupportedException>($"'{name}' is a refused member");
            }

            return;
        }

        foreach (var (name, run, _) in delivered)
        {
            var act = () => { _ = run(); };
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
        }

        foreach (var (name, run) in refused)
        {
            var act = () => { _ = run(); };
            act.Should().Throw<NotSupportedException>().WithMessage("*refused*", $"'{name}' carries its reason on every provider");
        }
    }

    /// <summary>
    /// <c>string_to_table</c> is the one set-returning name of the D2a family: PostgreSQL returns one row
    /// per fragment, every other provider refuses the table function by name.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void CrossProviderScalarFunctions_StringToTable_ShouldMatchProviderSupport()
    {
        var run = () => _sut.DataProvider
            .From(() => SqlFunctions.Postgres.string_to_table("a,b,c", ","))
            .Select(row => row.Value)
            .ToList();

        if (Provider.Name == "postgres")
        {
            run().Should().Equal("a", "b", "c");
            return;
        }

        var act = () => { _ = run(); };
        act.Should().Throw<NotSupportedException>().WithMessage("*string_to_table*not supported*");
    }

    private T Row<T>(System.Linq.Expressions.Expression<Func<IComplexEntity, T>> projection)
        => _sut.ComplexEntity.Where(e => e.Id == 1).Select(projection).First();
}
