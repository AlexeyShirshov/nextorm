using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D13 (F13 URL/IP): live execution of the ClickHouse-only URL and IP address members against
/// the pinned <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the vendor's non-RFC URL parser
/// (empty input, no scheme, port, userinfo, IDN, trailing dot and malformed values), the array- and
/// tuple-valued result shapes, the explicit-hex representation of the binary <c>FixedString(16)</c>
/// IPv6 form, the canonical-text representation of the native <c>IPv4</c>/<c>IPv6</c> values (the row
/// materializer has no address type), the <c>UInt16</c> port widened to <see cref="int"/>, the
/// <c>OrDefault</c>/<c>OrNull</c> distinction, the CIDR clamping boundaries, the NULL matrix, and the
/// deterministic <c>TLD_LIST_NOT_FOUND</c> fact of the custom-list members.
/// </summary>
[Collection("ClickHouse")]
public sealed class T286bF13UrlIpFunctionTests : ProviderTestSuite
{
    private const string SampleUrl = "http://example.com/path?query=value&param=123#fragment";
    private const string Ipv6Hex = "20010DB8000000000000000000000001";

    [SqlTable("t286b_f13_entity")]
    internal interface IT286bUrlIpEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("text")]
        string? Text { get; set; }

        [Column("text_nn")]
        string TextNonNull { get; set; }

        [Column("u32")]
        uint U32 { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static T Scalar<T>(string expression)
    {
        using var context = ClickHouseTestProvider.Instance.CreateContext();
        using var result = context.ExecuteRaw($"select {expression} as v");
        return result.Read<T>().Single();
    }

    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t286b_f13_entity",
                     "create table t286b_f13_entity (id Int32, text Nullable(String), "
                     + "text_nn String, u32 UInt32) engine = Memory",
                     "insert into t286b_f13_entity (id, text, text_nn, u32) values "
                     + "(1, 'https://user:pw@www.example.com:8080/a/b?x=1&y=2#frag', "
                     + "'https://www.example.co.uk/a/b?b=1&b=2#c', 16909060), "
                     + "(2, NULL, '', 0), "
                     + "(3, 'not a url', 'example.com', 4294967295), "
                     + "(4, 'https://пример.рф/путь', 'http://example.com./x', 3232235776)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void DomainAndProtocol_ShouldParseTheVendorNonRfcGrammar()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Domain = SqlFunctions.ClickHouse.domain(x.Text),
                Protocol = SqlFunctions.ClickHouse.protocol(x.Text),
                SchemeLess = SqlFunctions.ClickHouse.domain("example.com:8080"),
                Idn = SqlFunctions.ClickHouse.domain("https://пример.рф/путь"),
                Uppercase = SqlFunctions.ClickHouse.protocol("HTTPS://example.com"),
                UserInfo = SqlFunctions.ClickHouse.domain("https://user:pw@example.com:8080/x"),
                UserNameOnly = SqlFunctions.ClickHouse.domain("https://user@example.com/x"),
                TrailingDot = SqlFunctions.ClickHouse.domain("http://example.com./x"),
                Ipv6Host = SqlFunctions.ClickHouse.domain("http://[::1]:8080/x"),
                Empty = SqlFunctions.ClickHouse.domain(""),
                Malformed = SqlFunctions.ClickHouse.domain("not a url")
            })
            .First();

        using (new AssertionScope())
        {
            r.Domain.Should().BeEmpty("a userinfo URL is not accepted by the non-RFC parser");
            r.Protocol.Should().Be("https");
            r.SchemeLess.Should().Be("example.com", "the scheme is optional for domain");
            r.Idn.Should().Be("пример.рф", "IDN text is parsed as UTF-8");
            r.Uppercase.Should().Be("HTTPS", "protocol preserves the case of the scheme");
            r.UserInfo.Should().BeEmpty("userinfo defeats the non-RFC host parser");
            r.UserNameOnly.Should().Be("example.com", "a lone user name is accepted");
            r.TrailingDot.Should().BeEmpty("a trailing-dot host is not accepted");
            r.Ipv6Host.Should().BeEmpty("a bracketed IPv6 host is not accepted");
            r.Empty.Should().BeEmpty();
            r.Malformed.Should().BeEmpty();

            Scalar<bool>("domain(CAST(NULL AS Nullable(String))) is NULL").Should().BeTrue(
                "a NULL input stays NULL");
        }
    }

    [Fact]
    public void PathAndQueryParts_ShouldSplitTheUrl()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Path = SqlFunctions.ClickHouse.path(x.Text),
                PathFull = SqlFunctions.ClickHouse.path_full(x.Text),
                Query = SqlFunctions.ClickHouse.query_string(x.Text),
                QueryAndFragment = SqlFunctions.ClickHouse.query_string_and_fragment(x.Text),
                Fragment = SqlFunctions.ClickHouse.fragment(x.Text),
                NoPath = SqlFunctions.ClickHouse.path("https://example.com"),
                SlashOnly = SqlFunctions.ClickHouse.path("https://example.com/"),
                QueryOnly = SqlFunctions.ClickHouse.query_string("?x=1"),
                EmptyQuery = SqlFunctions.ClickHouse.query_string("https://example.com/a?#f"),
                DoubleHash = SqlFunctions.ClickHouse.fragment("https://example.com/a#b#c")
            })
            .First();

        using (new AssertionScope())
        {
            r.Path.Should().Be("/a/b");
            r.PathFull.Should().Be("/a/b?x=1&y=2#frag");
            r.Query.Should().Be("x=1&y=2");
            r.QueryAndFragment.Should().Be("x=1&y=2#frag");
            r.Fragment.Should().Be("frag");
            r.NoPath.Should().BeEmpty();
            r.SlashOnly.Should().Be("/");
            r.QueryOnly.Should().Be("x=1", "the leading ? is stripped");
            r.EmptyQuery.Should().BeEmpty();
            r.DoubleHash.Should().Be("b#c", "a later # stays part of the fragment");
        }
    }

    [Fact]
    public void Port_ShouldReturnThePortOrDefault()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Explicit = SqlFunctions.ClickHouse.port("https://example.com:8080/x"),
                Default = SqlFunctions.ClickHouse.port("https://example.com/x"),
                WithDefault = SqlFunctions.ClickHouse.port("https://example.com/x", 443),
                UserInfo = SqlFunctions.ClickHouse.port("https://user:pw@example.com:8080/x"),
                UserInfoDefault = SqlFunctions.ClickHouse.port("https://user:pw@example.com:8080/x", 443),
                OutOfRange = SqlFunctions.ClickHouse.port("example.com:99999"),
                Padded = SqlFunctions.ClickHouse.port("https://example.com:00080/x")
            })
            .First();

        using (new AssertionScope())
        {
            r.Explicit.Should().Be(8080);
            r.Default.Should().Be(0);
            r.WithDefault.Should().Be(443, "the optional default is used when no port is present");
            r.UserInfo.Should().Be(0, "the non-RFC parser rejects a userinfo URL");
            r.UserInfoDefault.Should().Be(443);
            r.OutOfRange.Should().Be(0);
            r.Padded.Should().Be(80);

            Scalar<bool>("port(CAST(NULL AS Nullable(String))) is NULL").Should().BeTrue(
                "a NULL URL stays NULL, never the default");
            Scalar<bool>("port(CAST(NULL AS Nullable(String)), 443) is NULL").Should().BeTrue();
        }
    }

    [Fact]
    public void Cutters_ShouldRemoveTheDocumentedParts()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var url = SampleUrl;

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                CutWww = SqlFunctions.ClickHouse.cut_www("https://www.example.com/x"),
                BareWww = SqlFunctions.ClickHouse.cut_www("www.example.com"),
                CutQuery = SqlFunctions.ClickHouse.cut_query_string(url),
                CutFragment = SqlFunctions.ClickHouse.cut_fragment(url),
                CutBoth = SqlFunctions.ClickHouse.cut_query_string_and_fragment(url),
                CutParameter = SqlFunctions.ClickHouse.cut_url_parameter(url, "query"),
                CutAbsent = SqlFunctions.ClickHouse.cut_url_parameter(url, "absent"),
                CutRepeated = SqlFunctions.ClickHouse.cut_url_parameter(
                    "https://x/?a=1&a=2", "a"),
                CutFlag = SqlFunctions.ClickHouse.cut_url_parameter(
                    "http://bigmir.net/?a&c=d", "a")
            })
            .First();

        using (new AssertionScope())
        {
            r.CutWww.Should().Be("https://example.com/x");
            r.BareWww.Should().Be("www.example.com", "cutWWW keeps a scheme-less value unchanged");
            r.CutQuery.Should().Be("http://example.com/path#fragment");
            r.CutFragment.Should().Be("http://example.com/path?query=value&param=123");
            r.CutBoth.Should().Be("http://example.com/path");
            r.CutParameter.Should().Be("http://example.com/path?param=123#fragment");
            r.CutAbsent.Should().Be(SampleUrl, "an unmatched name leaves the URL unchanged");
            r.CutRepeated.Should().Be("https://x/?a=2", "only the first occurrence is removed");
            r.CutFlag.Should().Be("http://bigmir.net/?a&c=d",
                "a value-less flag parameter is not removed");
        }
    }

    [Fact]
    public void EncodingComponents_ShouldRoundTrip()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var text = "a b&c/d?e=f";

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Encoded = SqlFunctions.ClickHouse.encode_url_component(text),
                Decoded = SqlFunctions.ClickHouse.decode_url_component(
                    SqlFunctions.ClickHouse.encode_url_component(text)),
                FormEncoded = SqlFunctions.ClickHouse.encode_url_form_component(text),
                FormDecoded = SqlFunctions.ClickHouse.decode_url_form_component(
                    SqlFunctions.ClickHouse.encode_url_form_component(text)),
                PlusIsLiteral = SqlFunctions.ClickHouse.decode_url_component("a+b")
            })
            .First();

        using (new AssertionScope())
        {
            r.Encoded.Should().Be("a%20b%26c%2Fd%3Fe%3Df", "component encoding uses %20 for a space");
            r.Decoded.Should().Be(text);
            r.FormEncoded.Should().Be("a+b%26c%2Fd%3Fe%3Df", "form encoding uses + for a space");
            r.FormDecoded.Should().Be(text);
            r.PlusIsLiteral.Should().Be("a+b", "decodeURLComponent does not treat + as a space");
        }
    }

    [Fact]
    public void SignificantSubdomainMembers_ShouldUseTheBuiltInList()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Significant = SqlFunctions.ClickHouse.first_significant_subdomain(
                    "https://news.clickhouse.com.tr/"),
                WithoutWww = SqlFunctions.ClickHouse.domain_without_www(
                    "https://www.example.com/x"),
                Cut = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain(
                    "https://www.example.co.uk/x"),
                CutWithWww = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain_with_www(
                    "https://www.example.co.uk/x"),
                CutBareTld = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain("com")
            })
            .First();

        using (new AssertionScope())
        {
            r.Significant.Should().Be("clickhouse");
            r.WithoutWww.Should().Be("example.com");
            r.Cut.Should().Be("example.co.uk");
            r.CutWithWww.Should().Be("example.co.uk",
                "on 25.8.33.6 the WithWWW variant does not retain the www prefix");
            r.CutBareTld.Should().BeEmpty("a bare top-level domain cannot be cut");
        }
    }

    [Fact]
    public void CustomTldMembers_ShouldRequireAConfiguredList()
    {
        // The vendor requires a TLD list configured in <top_level_domains_lists>; the pinned stock
        // server has none, so the three custom-list members deterministically raise
        // TLD_LIST_NOT_FOUND. The function names and their arities are verified by that server error.
        using (new AssertionScope())
        {
            FluentActions.Invoking(() => Scalar<string>(
                    "cutToFirstSignificantSubdomainCustom('https://www.example.co.uk/x', 'uk')"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*TLD_LIST_NOT_FOUND*");
            FluentActions.Invoking(() => Scalar<string>(
                    "cutToFirstSignificantSubdomainCustomWithWWW('https://www.example.co.uk/x', 'uk')"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*TLD_LIST_NOT_FOUND*");
            FluentActions.Invoking(() => Scalar<string>(
                    "firstSignificantSubdomainCustom('https://www.example.co.uk/x', 'uk')"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*TLD_LIST_NOT_FOUND*");

            Scalar<bool>(
                    "cutToFirstSignificantSubdomainCustom(CAST(NULL AS Nullable(String)), 'uk') is NULL")
                .Should().BeTrue("a NULL URL stays NULL without resolving the TLD list");
            Scalar<bool>(
                    "cutToFirstSignificantSubdomainCustomWithWWW(CAST(NULL AS Nullable(String)), 'uk') is NULL")
                .Should().BeTrue();
        }
    }

    [Fact]
    public void NonNullableShapes_ShouldRejectNullableArguments()
    {
        // The array- and tuple-valued members cannot sit inside a Nullable type: the server rejects a
        // Nullable argument with ILLEGAL_TYPE_OF_ARGUMENT, which is why their CLR parameters are
        // non-nullable. This is the Null/default variant of those members.
        using (new AssertionScope())
        {
            FluentActions.Invoking(() => Scalar<string[]>(
                    "extractURLParameterNames(CAST(NULL AS Nullable(String)))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*ILLEGAL_TYPE_OF_ARGUMENT*");
            FluentActions.Invoking(() => Scalar<string[]>(
                    "URLHierarchy(CAST(NULL AS Nullable(String)))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*ILLEGAL_TYPE_OF_ARGUMENT*");
            FluentActions.Invoking(() => Scalar<string[]>(
                    "URLPathHierarchy(CAST(NULL AS Nullable(String)))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*ILLEGAL_TYPE_OF_ARGUMENT*");
            FluentActions.Invoking(() => Scalar<string>(
                    "toString(tupleElement(IPv4CIDRToRange(CAST(NULL AS Nullable(IPv4)), 24), 1))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*ILLEGAL_TYPE_OF_ARGUMENT*");
            FluentActions.Invoking(() => Scalar<string>(
                    "toString(tupleElement(IPv6CIDRToRange(CAST(NULL AS Nullable(IPv6)), 48), 1))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*ILLEGAL_TYPE_OF_ARGUMENT*");
        }
    }

    [Fact]
    public void Hierarchies_ShouldReturnTheArrayShapes()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Hierarchy = SqlFunctions.ClickHouse.url_hierarchy("https://x/a/b?c=1"),
                PathHierarchy = SqlFunctions.ClickHouse.url_path_hierarchy("https://x/a/b?c=1"),
                FromColumn = SqlFunctions.ClickHouse.url_hierarchy(x.TextNonNull)
            })
            .First();

        using (new AssertionScope())
        {
            r.Hierarchy.Should().Equal(
                "https://x/", "https://x/a/", "https://x/a/b?", "https://x/a/b?c=1");
            r.PathHierarchy.Should().Equal("/a/", "/a/b?", "/a/b?c=1");
            r.FromColumn[0].Should().Be("https://www.example.co.uk/");
        }
    }

    [Fact]
    public void ParameterMembers_ShouldExtractNamesAndValues()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Present = SqlFunctions.ClickHouse.extract_url_parameter(
                    "https://x/?a=1&b=2", "b"),
                Absent = SqlFunctions.ClickHouse.extract_url_parameter(
                    "https://example.com/?a=1", "b"),
                First = SqlFunctions.ClickHouse.extract_url_parameter(
                    "https://x/?a=1&a=2", "a"),
                Encoded = SqlFunctions.ClickHouse.extract_url_parameter(
                    "https://x/?a=hello%20world", "a"),
                NoQuestionMark = SqlFunctions.ClickHouse.extract_url_parameter(
                    "a=1&b=2", "a"),
                CaseSensitive = SqlFunctions.ClickHouse.extract_url_parameter(
                    "https://x/?A=1", "a"),
                Names = SqlFunctions.ClickHouse.extract_url_parameter_names(
                    "https://x/?a=1&b=2#f"),
                RepeatedNames = SqlFunctions.ClickHouse.extract_url_parameter_names(
                    "https://x/?a=1&a=2"),
                FlagName = SqlFunctions.ClickHouse.extract_url_parameter_names(
                    "https://x/?a=&b"),
                FromColumn = SqlFunctions.ClickHouse.extract_url_parameter(
                    x.TextNonNull, "b")
            })
            .First();

        using (new AssertionScope())
        {
            r.Present.Should().Be("2");
            r.Absent.Should().BeEmpty();
            r.First.Should().Be("1", "the first occurrence wins");
            r.Encoded.Should().Be("hello%20world", "values are not decoded");
            r.NoQuestionMark.Should().BeEmpty("the vendor requires the ? separator");
            r.CaseSensitive.Should().BeEmpty();
            r.Names.Should().Equal("a", "b");
            r.RepeatedNames.Should().Equal("a", "a");
            r.FlagName.Should().Equal(["a"],
                "a value-less second parameter contributes no name after an a= entry");
            r.FromColumn.Should().Be("1");
        }
    }

    [Fact]
    public void Ipv4TextMembers_ShouldFormatNumbers()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Text = SqlFunctions.ClickHouse.ipv4_num_to_string(x.U32),
                ClassC = SqlFunctions.ClickHouse.ipv4_num_to_string_class_c(x.U32),
                Zero = SqlFunctions.ClickHouse.ipv4_num_to_string(0u),
                Max = SqlFunctions.ClickHouse.ipv4_num_to_string(4294967295u),
                MaxClassC = SqlFunctions.ClickHouse.ipv4_num_to_string_class_c(4294967295u)
            })
            .First();

        using (new AssertionScope())
        {
            r.Text.Should().Be("1.2.3.4");
            r.ClassC.Should().Be("1.2.3.xxx");
            r.Zero.Should().Be("0.0.0.0");
            r.Max.Should().Be("255.255.255.255");
            r.MaxClassC.Should().Be("255.255.255.xxx");

            Scalar<bool>("IPv4NumToString(CAST(NULL AS Nullable(UInt32))) is NULL").Should().BeTrue();
        }
    }

    [Fact]
    public void Ipv4ParsingMembers_ShouldParseAndDistinguishDefaultFromNull()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Parsed = SqlFunctions.ClickHouse.ipv4_string_to_num("1.2.3.4"),
                Defaulted = SqlFunctions.ClickHouse.ipv4_string_to_num_or_default("nope"),
                DefaultedValid = SqlFunctions.ClickHouse.ipv4_string_to_num_or_default("1.2.3.4"),
                OrNull = SqlFunctions.ClickHouse.ipv4_string_to_num_or_null("nope"),
                OrNullValid = SqlFunctions.ClickHouse.ipv4_string_to_num_or_null("1.2.3.4")
            })
            .First();

        using (new AssertionScope())
        {
            r.Parsed.Should().Be(16909060u);
            r.Defaulted.Should().Be(0u);
            r.DefaultedValid.Should().Be(16909060u);
            r.OrNull.Should().BeNull();
            r.OrNullValid.Should().Be(16909060u);

            FluentActions.Invoking(() => Scalar<uint>("IPv4StringToNum('1.2.3.256')"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*CANNOT_PARSE_IPV4*");
            FluentActions.Invoking(() => Scalar<uint>("IPv4StringToNum('01.2.3.4')"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*CANNOT_PARSE_IPV4*");
            Scalar<bool>("IPv4StringToNumOrDefault(CAST(NULL AS Nullable(String))) is NULL")
                .Should().BeTrue("OrDefault keeps a NULL input NULL instead of returning 0");
            Scalar<bool>("IPv4StringToNumOrNull(CAST(NULL AS Nullable(String))) is NULL")
                .Should().BeTrue();
        }
    }

    [Fact]
    public void Ipv6BinaryMembers_ShouldRoundTripHex()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Hex = SqlFunctions.ClickHouse.ipv6_string_to_num("2001:db8::1"),
                RoundTrip = SqlFunctions.ClickHouse.ipv6_num_to_string(Ipv6Hex),
                Mapped = SqlFunctions.ClickHouse.ipv6_string_to_num("::ffff:1.2.3.4"),
                MappedText = SqlFunctions.ClickHouse.ipv6_num_to_string("00000000000000000000FFFF01020304"),
                Defaulted = SqlFunctions.ClickHouse.ipv6_string_to_num_or_default("nope"),
                OrNull = SqlFunctions.ClickHouse.ipv6_string_to_num_or_null("nope")
            })
            .First();

        using (new AssertionScope())
        {
            r.Hex.Should().Be(Ipv6Hex, "the 16-byte form is 32 upper-case hex digits");
            r.RoundTrip.Should().Be("2001:db8::1", "the hex -> binary -> text pair round-trips");
            r.Mapped.Should().Be("00000000000000000000FFFF01020304");
            r.MappedText.Should().Be("::ffff:1.2.3.4");
            r.Defaulted.Should().Be(new string('0', 32), "malformed input is sixteen zero bytes");
            r.OrNull.Should().BeNull();

            FluentActions.Invoking(() => Scalar<string>("IPv6StringToNum('nope')"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*CANNOT_PARSE_IPV6*");
            Scalar<bool>("hex(IPv6StringToNumOrDefault(CAST(NULL AS Nullable(String)))) is NULL")
                .Should().BeTrue();
        }
    }

    [Fact]
    public void CutIpv6_ShouldTruncateTheBinaryForm()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Truncated = SqlFunctions.ClickHouse.cut_ipv6(Ipv6Hex, 2, 2),
                Unchanged = SqlFunctions.ClickHouse.cut_ipv6(Ipv6Hex, 0, 0),
                Mapped = SqlFunctions.ClickHouse.cut_ipv6("00000000000000000000FFFF01020304", 6, 0)
            })
            .First();

        using (new AssertionScope())
        {
            r.Truncated.Should().Be("2001:db8::");
            r.Unchanged.Should().Be("2001:db8::1");
            r.Mapped.Should().Be("::ffff:1.2.3.4");
            Scalar<bool>("cutIPv6(toFixedString(unhex(CAST(NULL AS Nullable(String))), 16), 6, 1) is NULL")
                .Should().BeTrue();
        }
    }

    [Fact]
    public void PredicateMembers_ShouldReturnBooleanTruth()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                IsIpv4 = SqlFunctions.ClickHouse.is_ipv4_string("1.2.3.4"),
                IsIpv4Short = SqlFunctions.ClickHouse.is_ipv4_string("1.2.3"),
                IsIpv4V6 = SqlFunctions.ClickHouse.is_ipv4_string("::1"),
                IsIpv6 = SqlFunctions.ClickHouse.is_ipv6_string("::1"),
                IsIpv6V4 = SqlFunctions.ClickHouse.is_ipv6_string("1.2.3.4"),
                InRange = SqlFunctions.ClickHouse.is_ip_address_in_range("1.2.3.4", "1.2.3.0/24"),
                OutOfRange = SqlFunctions.ClickHouse.is_ip_address_in_range("1.2.4.4", "1.2.3.0/24"),
                V6InRange = SqlFunctions.ClickHouse.is_ip_address_in_range("::1", "::/0")
            })
            .First();

        using (new AssertionScope())
        {
            r.IsIpv4.Should().BeTrue();
            r.IsIpv4Short.Should().BeFalse();
            r.IsIpv4V6.Should().BeFalse();
            r.IsIpv6.Should().BeTrue();
            r.IsIpv6V4.Should().BeFalse();
            r.InRange.Should().BeTrue();
            r.OutOfRange.Should().BeFalse();
            r.V6InRange.Should().BeTrue();

            Scalar<bool>("isIPv4String(CAST(NULL AS Nullable(String))) is NULL")
                .Should().BeTrue("a NULL is not false for the predicate");
            Scalar<bool>("isIPAddressInRange(CAST(NULL AS Nullable(String)), '1.2.3.0/24') = 0")
                .Should().BeTrue("isIPAddressInRange does not propagate NULL; it yields false");

            FluentActions.Invoking(() => Scalar<byte>(
                    "isIPAddressInRange('nope', '1.2.3.0/24')"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*CANNOT_PARSE_TEXT*");
        }
    }

    [Fact]
    public void AddressMembers_ShouldRenderCanonicalText()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                FromText = SqlFunctions.ClickHouse.to_ipv4("1.2.3.4"),
                FromNumber = SqlFunctions.ClickHouse.to_ipv4(x.U32),
                FromNull = SqlFunctions.ClickHouse.to_ipv4(x.Text),
                V6 = SqlFunctions.ClickHouse.to_ipv6("::1"),
                V6Mapped = SqlFunctions.ClickHouse.to_ipv6("1.2.3.4"),
                Mapped = SqlFunctions.ClickHouse.ipv4_to_ipv6("1.2.3.4"),
                MappedZero = SqlFunctions.ClickHouse.ipv4_to_ipv6("0.0.0.0")
            })
            .First();

        using (new AssertionScope())
        {
            r.FromText.Should().Be("1.2.3.4");
            r.FromNumber.Should().Be("1.2.3.4", "the UInt32 constructor renders the same canonical text");
            r.FromNull.Should().BeNull();
            r.V6.Should().Be("::1");
            r.V6Mapped.Should().Be("::ffff:1.2.3.4");
            r.Mapped.Should().Be("::ffff:1.2.3.4");
            r.MappedZero.Should().Be("::ffff:0.0.0.0");

            FluentActions.Invoking(() => Scalar<string>("toString(toIPv4('nope'))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*CANNOT_PARSE_IPV4*");
            FluentActions.Invoking(() => Scalar<string>("toString(toIPv6('nope'))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*CANNOT_PARSE_IPV6*");
        }
    }

    [Fact]
    public void CidrMembers_ShouldReturnTheRangeTuple()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V4Slash24 = SqlFunctions.ClickHouse.ipv4_cidr_to_range("1.2.3.4", 24),
                V4Slash0 = SqlFunctions.ClickHouse.ipv4_cidr_to_range("1.2.3.4", 0),
                V4Slash32 = SqlFunctions.ClickHouse.ipv4_cidr_to_range("1.2.3.4", 32),
                V4Clamped = SqlFunctions.ClickHouse.ipv4_cidr_to_range("1.2.3.4", 33),
                V6Slash48 = SqlFunctions.ClickHouse.ipv6_cidr_to_range("2001:db8::1", 48),
                V6Slash0 = SqlFunctions.ClickHouse.ipv6_cidr_to_range("2001:db8::1", 0),
                V6Slash128 = SqlFunctions.ClickHouse.ipv6_cidr_to_range("2001:db8::1", 128),
                V6Clamped = SqlFunctions.ClickHouse.ipv6_cidr_to_range("2001:db8::1", 129)
            })
            .First();

        using (new AssertionScope())
        {
            r.V4Slash24.Should().Be(Tuple.Create("1.2.3.0", "1.2.3.255"));
            r.V4Slash0.Should().Be(Tuple.Create("0.0.0.0", "255.255.255.255"));
            r.V4Slash32.Should().Be(Tuple.Create("1.2.3.4", "1.2.3.4"));
            r.V4Clamped.Should().Be(Tuple.Create("1.2.3.4", "1.2.3.4"),
                "a prefix above 32 is clamped to a single address");
            r.V6Slash48.Should().Be(Tuple.Create("2001:db8::", "2001:db8:0:ffff:ffff:ffff:ffff:ffff"));
            r.V6Slash0.Should().Be(Tuple.Create("::", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff"));
            r.V6Slash128.Should().Be(Tuple.Create("2001:db8::1", "2001:db8::1"));
            r.V6Clamped.Should().Be(Tuple.Create("2001:db8::1", "2001:db8::1"));
        }
    }

    [Fact]
    public void NullMatrix_ShouldPropagateNullableTextArguments()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Domain = SqlFunctions.ClickHouse.domain(x.Text),
                Protocol = SqlFunctions.ClickHouse.protocol(x.Text),
                Path = SqlFunctions.ClickHouse.path(x.Text),
                Query = SqlFunctions.ClickHouse.query_string(x.Text),
                Fragment = SqlFunctions.ClickHouse.fragment(x.Text),
                Port = SqlFunctions.ClickHouse.port(x.Text),
                CutParameter = SqlFunctions.ClickHouse.cut_url_parameter(x.Text, "a"),
                CutWww = SqlFunctions.ClickHouse.cut_www(x.Text),
                CutQuery = SqlFunctions.ClickHouse.cut_query_string(x.Text),
                CutFragment = SqlFunctions.ClickHouse.cut_fragment(x.Text),
                CutBoth = SqlFunctions.ClickHouse.cut_query_string_and_fragment(x.Text),
                Significant = SqlFunctions.ClickHouse.first_significant_subdomain(x.Text),
                WithoutWww = SqlFunctions.ClickHouse.domain_without_www(x.Text),
                PathFull = SqlFunctions.ClickHouse.path_full(x.Text),
                QueryAndFragment = SqlFunctions.ClickHouse.query_string_and_fragment(x.Text),
                CutSignificant = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain(x.Text),
                Encoded = SqlFunctions.ClickHouse.encode_url_component(x.Text),
                Decoded = SqlFunctions.ClickHouse.decode_url_component(x.Text),
                FormEncoded = SqlFunctions.ClickHouse.encode_url_form_component(x.Text),
                FormDecoded = SqlFunctions.ClickHouse.decode_url_form_component(x.Text),
                Parameter = SqlFunctions.ClickHouse.extract_url_parameter(x.Text, "a"),
                Ipv4 = SqlFunctions.ClickHouse.ipv4_string_to_num(x.Text),
                Ipv4OrNull = SqlFunctions.ClickHouse.ipv4_string_to_num_or_null(x.Text),
                Ipv6 = SqlFunctions.ClickHouse.ipv6_string_to_num(x.Text),
                Ipv6OrNull = SqlFunctions.ClickHouse.ipv6_string_to_num_or_null(x.Text),
                CutIpv6 = SqlFunctions.ClickHouse.cut_ipv6(x.Text, 6, 1),
                IsIpv4 = SqlFunctions.ClickHouse.is_ipv4_string(x.Text),
                IsIpv6 = SqlFunctions.ClickHouse.is_ipv6_string(x.Text),
                ToIpv4 = SqlFunctions.ClickHouse.to_ipv4(x.Text),
                ToIpv6 = SqlFunctions.ClickHouse.to_ipv6(x.Text),
                Mapped = SqlFunctions.ClickHouse.ipv4_to_ipv6(x.Text)
            })
            .First();

        using (new AssertionScope())
        {
            r.Domain.Should().BeNull();
            r.Protocol.Should().BeNull();
            r.Path.Should().BeNull();
            r.Query.Should().BeNull();
            r.Fragment.Should().BeNull();
            r.Port.Should().BeNull();
            r.CutParameter.Should().BeNull();
            r.CutWww.Should().BeNull();
            r.CutQuery.Should().BeNull();
            r.CutFragment.Should().BeNull();
            r.CutBoth.Should().BeNull();
            r.Significant.Should().BeNull();
            r.WithoutWww.Should().BeNull();
            r.PathFull.Should().BeNull();
            r.QueryAndFragment.Should().BeNull();
            r.CutSignificant.Should().BeNull();
            r.Encoded.Should().BeNull();
            r.Decoded.Should().BeNull();
            r.FormEncoded.Should().BeNull();
            r.FormDecoded.Should().BeNull();
            r.Parameter.Should().BeNull();
            r.Ipv4.Should().BeNull();
            r.Ipv4OrNull.Should().BeNull();
            r.Ipv6.Should().BeNull();
            r.Ipv6OrNull.Should().BeNull();
            r.CutIpv6.Should().BeNull();
            r.IsIpv4.Should().BeNull();
            r.IsIpv6.Should().BeNull();
            r.ToIpv4.Should().BeNull();
            r.ToIpv6.Should().BeNull();
            r.Mapped.Should().BeNull();
        }
    }

    [Fact]
    public void Projection_ShouldMaterialiseEveryDeliveredMember()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var url = "https://x/a/b?c=1&d=2#e";

        var r = ctx.From<IT286bUrlIpEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Domain = SqlFunctions.ClickHouse.domain("https://www.example.com/x"),
                Protocol = SqlFunctions.ClickHouse.protocol(url),
                Path = SqlFunctions.ClickHouse.path(url),
                Query = SqlFunctions.ClickHouse.query_string(url),
                Fragment = SqlFunctions.ClickHouse.fragment(url),
                Port = SqlFunctions.ClickHouse.port("https://example.com:8080/x"),
                CutParameter = SqlFunctions.ClickHouse.cut_url_parameter(url, "c"),
                TopLevel = SqlFunctions.ClickHouse.top_level_domain("https://www.example.com/x"),
                Encoded = SqlFunctions.ClickHouse.encode_url_component("a b"),
                Decoded = SqlFunctions.ClickHouse.decode_url_component("a%20b"),
                FormEncoded = SqlFunctions.ClickHouse.encode_url_form_component("a b"),
                FormDecoded = SqlFunctions.ClickHouse.decode_url_form_component("a+b"),
                WithoutWww = SqlFunctions.ClickHouse.domain_without_www("https://www.example.com/x"),
                Significant = SqlFunctions.ClickHouse.first_significant_subdomain(
                    "https://www.example.com/x"),
                PathFull = SqlFunctions.ClickHouse.path_full(url),
                QueryAndFragment = SqlFunctions.ClickHouse.query_string_and_fragment(url),
                CutWww = SqlFunctions.ClickHouse.cut_www("https://www.example.com/x"),
                CutQuery = SqlFunctions.ClickHouse.cut_query_string(url),
                CutFragment = SqlFunctions.ClickHouse.cut_fragment(url),
                CutQueryAndFragment = SqlFunctions.ClickHouse.cut_query_string_and_fragment(url),
                CutSignificant = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain(
                    "https://www.example.co.uk/x"),
                CutSignificantWww = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain_with_www(
                    "https://www.example.co.uk/x"),
                Hierarchy = SqlFunctions.ClickHouse.url_hierarchy("https://x/a/b?c=1"),
                PathHierarchy = SqlFunctions.ClickHouse.url_path_hierarchy("https://x/a/b?c=1"),
                Parameter = SqlFunctions.ClickHouse.extract_url_parameter("https://x/?a=1", "a"),
                ParameterNames = SqlFunctions.ClickHouse.extract_url_parameter_names("https://x/?a=1&b=2"),
                Ipv4Text = SqlFunctions.ClickHouse.ipv4_num_to_string(16909060u),
                Ipv4ClassC = SqlFunctions.ClickHouse.ipv4_num_to_string_class_c(16909060u),
                Ipv4Num = SqlFunctions.ClickHouse.ipv4_string_to_num("1.2.3.4"),
                Ipv4Default = SqlFunctions.ClickHouse.ipv4_string_to_num_or_default("nope"),
                Ipv4Null = SqlFunctions.ClickHouse.ipv4_string_to_num_or_null("nope"),
                Ipv6Text = SqlFunctions.ClickHouse.ipv6_num_to_string(Ipv6Hex),
                Ipv6Hex = SqlFunctions.ClickHouse.ipv6_string_to_num("2001:db8::1"),
                Ipv6Default = SqlFunctions.ClickHouse.ipv6_string_to_num_or_default("nope"),
                Ipv6Null = SqlFunctions.ClickHouse.ipv6_string_to_num_or_null("nope"),
                CutIpv6 = SqlFunctions.ClickHouse.cut_ipv6(Ipv6Hex, 2, 2),
                IsIpv4 = SqlFunctions.ClickHouse.is_ipv4_string("1.2.3.4"),
                IsIpv6 = SqlFunctions.ClickHouse.is_ipv6_string("::1"),
                InRange = SqlFunctions.ClickHouse.is_ip_address_in_range("1.2.3.4", "1.2.3.0/24"),
                ToIpv4 = SqlFunctions.ClickHouse.to_ipv4("1.2.3.4"),
                ToIpv6 = SqlFunctions.ClickHouse.to_ipv6("::1"),
                Mapped = SqlFunctions.ClickHouse.ipv4_to_ipv6("1.2.3.4"),
                Cidr4 = SqlFunctions.ClickHouse.ipv4_cidr_to_range("1.2.3.4", 24),
                Cidr6 = SqlFunctions.ClickHouse.ipv6_cidr_to_range("2001:db8::1", 48)
            })
            .First();

        using (new AssertionScope())
        {
            r.Domain.Should().Be("www.example.com");
            r.Protocol.Should().Be("https");
            r.Path.Should().Be("/a/b");
            r.Query.Should().Be("c=1&d=2");
            r.Fragment.Should().Be("e");
            r.Port.Should().Be(8080);
            r.CutParameter.Should().Be("https://x/a/b?d=2#e");
            r.TopLevel.Should().Be("com");
            r.Encoded.Should().Be("a%20b");
            r.Decoded.Should().Be("a b");
            r.FormEncoded.Should().Be("a+b");
            r.FormDecoded.Should().Be("a b");
            r.WithoutWww.Should().Be("example.com");
            r.Significant.Should().Be("example");
            r.PathFull.Should().Be("/a/b?c=1&d=2#e");
            r.QueryAndFragment.Should().Be("c=1&d=2#e");
            r.CutWww.Should().Be("https://example.com/x");
            r.CutQuery.Should().Be("https://x/a/b#e");
            r.CutFragment.Should().Be("https://x/a/b?c=1&d=2");
            r.CutQueryAndFragment.Should().Be("https://x/a/b");
            r.CutSignificant.Should().Be("example.co.uk");
            r.CutSignificantWww.Should().Be("example.co.uk");
            r.Hierarchy.Should().HaveCount(4);
            r.PathHierarchy.Should().HaveCount(3);
            r.Parameter.Should().Be("1");
            r.ParameterNames.Should().Equal("a", "b");
            r.Ipv4Text.Should().Be("1.2.3.4");
            r.Ipv4ClassC.Should().Be("1.2.3.xxx");
            r.Ipv4Num.Should().Be(16909060u);
            r.Ipv4Default.Should().Be(0u);
            r.Ipv4Null.Should().BeNull();
            r.Ipv6Text.Should().Be("2001:db8::1");
            r.Ipv6Hex.Should().Be(Ipv6Hex);
            r.Ipv6Default.Should().Be(new string('0', 32));
            r.Ipv6Null.Should().BeNull();
            r.CutIpv6.Should().Be("2001:db8::");
            r.IsIpv4.Should().BeTrue();
            r.IsIpv6.Should().BeTrue();
            r.InRange.Should().BeTrue();
            r.ToIpv4.Should().Be("1.2.3.4");
            r.ToIpv6.Should().Be("::1");
            r.Mapped.Should().Be("::ffff:1.2.3.4");
            r.Cidr4.Should().Be(Tuple.Create("1.2.3.0", "1.2.3.255"));
            r.Cidr6.Should().Be(Tuple.Create("2001:db8::", "2001:db8:0:ffff:ffff:ffff:ffff:ffff"));
        }
    }
}
