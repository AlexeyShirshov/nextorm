using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D13 (F13 URL/IP): the ClickHouse-only URL and IP address members. The tests pin every
/// rendered arm (including the vendor's case-sensitive <c>URLHierarchy</c>/<c>URLPathHierarchy</c>
/// spellings), the exact CLR shapes (text stays <see cref="string"/>, <c>UInt16</c> ports become
/// <see cref="ushort"/>, <c>UInt32</c> addresses become <see cref="uint"/>, the native
/// <c>IPv4</c>/<c>IPv6</c> types map to <see cref="IPAddress"/>, a CIDR range is a two-component
/// <see cref="Tuple{T1,T2}"/> of addresses, array results are <see cref="string"/> arrays and booleans
/// are <see cref="bool"/>), the explicit-hex representation of the 16-byte binary IPv6 form, the
/// already-covered <c>url_hash</c> drift, and the fail-closed provider guard. They never open a
/// connection.
/// </summary>
public class T286bF13UrlIpFunctionSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

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

        [Column("addr")]
        string Address { get; set; }

        [Column("addr6")]
        string Address6 { get; set; }
    }

    /// <summary>The forty-seven newly shipped catalog names (the forty-eighth member, <c>urlHash</c>, is already covered by F10).</summary>
    private static readonly string[] DeliveredNames =
    [
        "domain", "protocol", "path", "query_string", "fragment", "port",
        "cut_url_parameter", "top_level_domain", "encode_url_component", "decode_url_component",
        "encode_url_form_component", "decode_url_form_component", "domain_without_www",
        "first_significant_subdomain", "first_significant_subdomain_custom", "path_full",
        "query_string_and_fragment", "cut_www", "cut_query_string", "cut_fragment",
        "cut_query_string_and_fragment", "cut_to_first_significant_subdomain",
        "cut_to_first_significant_subdomain_with_www", "cut_to_first_significant_subdomain_custom",
        "cut_to_first_significant_subdomain_custom_with_www", "url_hierarchy", "url_path_hierarchy",
        "extract_url_parameter", "extract_url_parameter_names",
        "ipv4_num_to_string", "ipv4_num_to_string_class_c", "ipv4_string_to_num",
        "ipv4_string_to_num_or_default", "ipv4_string_to_num_or_null",
        "ipv6_num_to_string", "ipv6_string_to_num", "ipv6_string_to_num_or_default",
        "ipv6_string_to_num_or_null", "cut_ipv6", "is_ipv4_string", "is_ipv6_string",
        "is_ip_address_in_range", "to_ipv4", "to_ipv6", "ipv4_to_ipv6",
        "ipv4_cidr_to_range", "ipv6_cidr_to_range"
    ];

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryDeliveredMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bUrlIpEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.domain(x.Text) }))
                .Should().Contain("domain(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.protocol(x.Text) }))
                .Should().Contain("protocol(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.path(x.Text) }))
                .Should().Contain("path(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.query_string(x.Text) }))
                .Should().Contain("queryString(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.fragment(x.Text) }))
                .Should().Contain("fragment(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.port(x.Text) }))
                .Should().Contain("toInt32(port(text)) as `V`",
                    "UInt16 is widened exactly with toInt32 because the row materializer has no UInt16 reader");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.port(x.Text, 443) }))
                .Should().Contain("toInt32(port(text, 443)) as `V`",
                    "the vendor's second argument is the default port for an absent/unparsable port");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.cut_url_parameter(x.Text, "a") }))
                .Should().Contain("cutURLParameter(text, 'a') as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.top_level_domain(x.Text) }))
                .Should().Contain("topLevelDomain(text) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.encode_url_component(x.Text) }))
                .Should().Contain("encodeURLComponent(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.decode_url_component(x.Text) }))
                .Should().Contain("decodeURLComponent(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.encode_url_form_component(x.Text) }))
                .Should().Contain("encodeURLFormComponent(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.decode_url_form_component(x.Text) }))
                .Should().Contain("decodeURLFormComponent(text) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.domain_without_www(x.Text) }))
                .Should().Contain("domainWithoutWWW(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.first_significant_subdomain(x.Text) }))
                .Should().Contain("firstSignificantSubdomain(text) as `V`");
            SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.first_significant_subdomain_custom(x.Text, "public_suffix_list")
            })).Should().Contain("firstSignificantSubdomainCustom(text, 'public_suffix_list') as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.path_full(x.Text) }))
                .Should().Contain("pathFull(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.query_string_and_fragment(x.Text) }))
                .Should().Contain("queryStringAndFragment(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.cut_www(x.Text) }))
                .Should().Contain("cutWWW(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.cut_query_string(x.Text) }))
                .Should().Contain("cutQueryString(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.cut_fragment(x.Text) }))
                .Should().Contain("cutFragment(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.cut_query_string_and_fragment(x.Text) }))
                .Should().Contain("cutQueryStringAndFragment(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain(x.Text) }))
                .Should().Contain("cutToFirstSignificantSubdomain(text) as `V`");
            SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain_with_www(x.Text)
            })).Should().Contain("cutToFirstSignificantSubdomainWithWWW(text) as `V`");
            SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain_custom(x.Text, "public_suffix_list")
            })).Should().Contain("cutToFirstSignificantSubdomainCustom(text, 'public_suffix_list') as `V`");
            SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain_custom_with_www(
                    x.Text, "public_suffix_list")
            })).Should().Contain("cutToFirstSignificantSubdomainCustomWithWWW(text, 'public_suffix_list') as `V`");

            // The vendor names are case-sensitive: the hierarchy functions are URLHierarchy and
            // URLPathHierarchy, not the catalogue's lower-camel spelling.
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.url_hierarchy(x.TextNonNull) }))
                .Should().Contain("URLHierarchy(text_nn) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.url_path_hierarchy(x.TextNonNull) }))
                .Should().Contain("URLPathHierarchy(text_nn) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.extract_url_parameter(x.Text, "a") }))
                .Should().Contain("extractURLParameter(text, 'a') as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.extract_url_parameter_names(x.TextNonNull) }))
                .Should().Contain("extractURLParameterNames(text_nn) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv4_num_to_string(x.U32) }))
                .Should().Contain("IPv4NumToString(u32) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv4_num_to_string_class_c(x.U32) }))
                .Should().Contain("IPv4NumToStringClassC(u32) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv4_string_to_num(x.Text) }))
                .Should().Contain("IPv4StringToNum(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv4_string_to_num_or_default(x.Text) }))
                .Should().Contain("IPv4StringToNumOrDefault(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv4_string_to_num_or_null(x.Text) }))
                .Should().Contain("IPv4StringToNumOrNull(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv6_num_to_string(x.Text) }))
                .Should().Contain("IPv6NumToString(toFixedString(unhex(text), 16)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv6_string_to_num(x.Text) }))
                .Should().Contain("hex(IPv6StringToNum(text)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv6_string_to_num_or_default(x.Text) }))
                .Should().Contain("hex(IPv6StringToNumOrDefault(text)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv6_string_to_num_or_null(x.Text) }))
                .Should().Contain("hex(IPv6StringToNumOrNull(text)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.cut_ipv6(x.Text, 6, 1) }))
                .Should().Contain("cutIPv6(toFixedString(unhex(text), 16), 6, 1) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.is_ipv4_string(x.Text) }))
                .Should().Contain("isIPv4String(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.is_ipv6_string(x.Text) }))
                .Should().Contain("isIPv6String(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.is_ip_address_in_range(x.Text, "1.2.3.0/24") }))
                .Should().Contain("isIPAddressInRange(text, '1.2.3.0/24') as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_ipv4(x.Text) }))
                .Should().Contain("toString(toIPv4(text)) as `V`",
                    "the native IPv4 value is surfaced as its canonical text form");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_ipv4(x.U32) }))
                .Should().Contain("toString(toIPv4(u32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_ipv6(x.Text) }))
                .Should().Contain("toString(toIPv6(text)) as `V`",
                    "the native IPv6 value is surfaced as its canonical text form");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv4_to_ipv6(x.Address) }))
                .Should().Contain("toString(IPv4ToIPv6(toIPv4(addr))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv4_cidr_to_range(x.Address, 24) }))
                .Should().Contain(
                    "tuple(toString(tupleElement(IPv4CIDRToRange(toIPv4(addr), 24), 1)), " +
                    "toString(tupleElement(IPv4CIDRToRange(toIPv4(addr), 24), 2))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ipv6_cidr_to_range(x.Address6, 48) }))
                .Should().Contain(
                    "tuple(toString(tupleElement(IPv6CIDRToRange(toIPv6(addr6), 48), 1)), " +
                    "toString(tupleElement(IPv6CIDRToRange(toIPv6(addr6), 48), 2))) as `V`");
        }
    }

    [Fact]
    public void Ipv6BinaryValues_ShouldBeWrappedInHexOrUnpackedFromHex()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bUrlIpEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            ToNum = SqlFunctions.ClickHouse.ipv6_string_to_num(x.Text),
            ToNumDefault = SqlFunctions.ClickHouse.ipv6_string_to_num_or_default(x.Text),
            ToNumNull = SqlFunctions.ClickHouse.ipv6_string_to_num_or_null(x.Text),
            ToText = SqlFunctions.ClickHouse.ipv6_num_to_string(x.Text),
            Cut = SqlFunctions.ClickHouse.cut_ipv6(x.Text, 6, 1)
        }));

        using (new AssertionScope())
        {
            // FixedString(16) is surfaced as 32 hex digits, never as the driver's raw byte-valued
            // string: every binary result is hex(...) and every binary argument is
            // toFixedString(unhex(...), 16).
            sql.Should().Contain("hex(IPv6StringToNum(text)) as `ToNum`");
            sql.Should().Contain("hex(IPv6StringToNumOrDefault(text)) as `ToNumDefault`");
            sql.Should().Contain("hex(IPv6StringToNumOrNull(text)) as `ToNumNull`");
            sql.Should().Contain("IPv6NumToString(toFixedString(unhex(text), 16)) as `ToText`");
            sql.Should().Contain("cutIPv6(toFixedString(unhex(text), 16), 6, 1) as `Cut`");
            sql.Should().NotContain("IPv6StringToNum(text) as `ToNum`",
                "the raw FixedString bytes must never be surfaced");
        }
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        using (new AssertionScope())
        {
            ReturnType("domain", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("protocol", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("path", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("query_string", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("fragment", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("port", [typeof(string)]).Should().Be(typeof(int?),
                "port returns UInt16 widened exactly with toInt32; a NULL input stays NULL");
            ReturnType("port", [typeof(string), typeof(ushort)]).Should().Be(typeof(int?));
            ReturnType("cut_url_parameter", [typeof(string), typeof(string)]).Should().Be(typeof(string));
            ReturnType("top_level_domain", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("encode_url_component", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("decode_url_component", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("encode_url_form_component", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("decode_url_form_component", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("domain_without_www", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("first_significant_subdomain", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("first_significant_subdomain_custom", [typeof(string), typeof(string)])
                .Should().Be(typeof(string));
            ReturnType("path_full", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("query_string_and_fragment", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("cut_www", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("cut_query_string", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("cut_fragment", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("cut_query_string_and_fragment", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("cut_to_first_significant_subdomain", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("cut_to_first_significant_subdomain_with_www", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("cut_to_first_significant_subdomain_custom", [typeof(string), typeof(string)])
                .Should().Be(typeof(string));
            ReturnType("cut_to_first_significant_subdomain_custom_with_www", [typeof(string), typeof(string)])
                .Should().Be(typeof(string));

            ReturnType("url_hierarchy", [typeof(string)]).Should().Be(typeof(string[]),
                "URLHierarchy returns Array(String) and the argument cannot be Nullable");
            ReturnType("url_path_hierarchy", [typeof(string)]).Should().Be(typeof(string[]));
            ReturnType("extract_url_parameter", [typeof(string), typeof(string)]).Should().Be(typeof(string));
            ReturnType("extract_url_parameter_names", [typeof(string)]).Should().Be(typeof(string[]));

            ReturnType("ipv4_num_to_string", [typeof(uint?)]).Should().Be(typeof(string));
            ReturnType("ipv4_num_to_string_class_c", [typeof(uint?)]).Should().Be(typeof(string));
            ReturnType("ipv4_string_to_num", [typeof(string)]).Should().Be(typeof(uint?),
                "IPv4StringToNum returns UInt32 and a NULL input stays NULL");
            ReturnType("ipv4_string_to_num_or_default", [typeof(string)]).Should().Be(typeof(uint?));
            ReturnType("ipv4_string_to_num_or_null", [typeof(string)]).Should().Be(typeof(uint?));
            ReturnType("ipv6_num_to_string", [typeof(string)]).Should().Be(typeof(string),
                "the binary IPv6 value is the explicit 32-hex-digit string");
            ReturnType("ipv6_string_to_num", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("ipv6_string_to_num_or_default", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("ipv6_string_to_num_or_null", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("cut_ipv6", [typeof(string), typeof(byte), typeof(byte)]).Should().Be(typeof(string));

            ReturnType("is_ipv4_string", [typeof(string)]).Should().Be(typeof(bool?),
                "UInt8 truth values are surfaced as bool and a NULL input stays NULL");
            ReturnType("is_ipv6_string", [typeof(string)]).Should().Be(typeof(bool?));
            ReturnType("is_ip_address_in_range", [typeof(string), typeof(string)]).Should().Be(typeof(bool),
                "isIPAddressInRange does not propagate NULL, so its result is non-nullable");
            ReturnType("to_ipv4", [typeof(string)]).Should().Be(typeof(string),
                "the row materializer has no address type, so the canonical IPv4 text is the CLR shape");
            ReturnType("to_ipv4", [typeof(uint)]).Should().Be(typeof(string));
            ReturnType("to_ipv4", [typeof(uint?)]).Should().Be(typeof(string));
            ReturnType("to_ipv6", [typeof(string)]).Should().Be(typeof(string),
                "the canonical IPv6 text is the CLR shape, while the binary storage form is hex");
            ReturnType("ipv4_to_ipv6", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("ipv4_cidr_to_range", [typeof(string), typeof(byte)])
                .Should().Be(typeof(Tuple<string, string>), "IPv4CIDRToRange returns Tuple(IPv4, IPv4) as canonical text");
            ReturnType("ipv6_cidr_to_range", [typeof(string), typeof(byte)])
                .Should().Be(typeof(Tuple<string, string>), "IPv6CIDRToRange returns Tuple(IPv6, IPv6) as canonical text");
        }

        static Type ReturnType(string name, Type[] arguments)
            => typeof(ClickHouseFunctions)
                .GetMethod(name, BindingFlags.Public | BindingFlags.Instance, arguments)!
                .ReturnType;
    }

    [Fact]
    public void AlreadyCoveredUrlHash_ShouldKeepTheVendorCasingAndBothArities()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bUrlIpEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            One = SqlFunctions.ClickHouse.url_hash(x.Text),
            Two = SqlFunctions.ClickHouse.url_hash(x.Text, 1)
        }));

        using (new AssertionScope())
        {
            // F13-M0373 (urlHash) is the F10 drift: delivered there, so no second member is added;
            // the vendor spelling is URLHash and both documented arities exist.
            sql.Should().Contain("URLHash(text) as `One`");
            sql.Should().Contain("URLHash(text, 1) as `Two`");
            typeof(ClickHouseFunctions).GetMethods()
                .Count(m => m.Name == "url_hash")
                .Should().Be(2, "the one- and two-argument forms are the F10 delivery");
        }
    }

    [Fact]
    public void CapturedValues_ShouldBindAsParameters()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bUrlIpEntity>();
        var address = "1.2.3.4";
        var text = "https://example.com/a?b=1";
        var ipv4Text = "1.2.3.4";
        var hex = "20010DB8000000000000000000000001";

        var command = Prepare(ctx, e.Select(x => new
        {
            Domain = SqlFunctions.ClickHouse.domain(text),
            Address = SqlFunctions.ClickHouse.to_ipv4(ipv4Text),
            Range = SqlFunctions.ClickHouse.ipv4_cidr_to_range(address, 24),
            Cut = SqlFunctions.ClickHouse.cut_ipv6(hex, 6, 1),
            Port = SqlFunctions.ClickHouse.port(text, 443)
        }));

        var sql = Normalize(command.DbCommand.CommandText);
        using (new AssertionScope())
        {
            sql.Should().Contain("domain(@text) as `Domain`");
            sql.Should().Contain("toString(toIPv4(@ipv4Text)) as `Address`");
            sql.Should().Contain(
                "tuple(toString(tupleElement(IPv4CIDRToRange(toIPv4(@address), 24), 1)), " +
                "toString(tupleElement(IPv4CIDRToRange(toIPv4(@address), 24), 2))) as `Range`");
            sql.Should().Contain("cutIPv6(toFixedString(unhex(@hex), 16), 6, 1) as `Cut`");
            sql.Should().Contain("toInt32(port(@text, 443)) as `Port`");
            command.DbCommandParams.Cast<System.Data.Common.DbParameter>()
                .Select(p => p.Value)
                .Should().Contain(address);
        }
    }

    [Fact]
    public void Projection_ShouldRenderEveryDeliveredMemberInOneSelect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bUrlIpEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Domain = SqlFunctions.ClickHouse.domain(x.Text),
            Protocol = SqlFunctions.ClickHouse.protocol(x.Text),
            Path = SqlFunctions.ClickHouse.path(x.Text),
            Query = SqlFunctions.ClickHouse.query_string(x.Text),
            Fragment = SqlFunctions.ClickHouse.fragment(x.Text),
            Port = SqlFunctions.ClickHouse.port(x.Text, 443),
            CutParameter = SqlFunctions.ClickHouse.cut_url_parameter(x.Text, "a"),
            TopLevel = SqlFunctions.ClickHouse.top_level_domain(x.Text),
            Encoded = SqlFunctions.ClickHouse.encode_url_component(x.Text),
            Decoded = SqlFunctions.ClickHouse.decode_url_component(x.Text),
            FormEncoded = SqlFunctions.ClickHouse.encode_url_form_component(x.Text),
            FormDecoded = SqlFunctions.ClickHouse.decode_url_form_component(x.Text),
            WithoutWww = SqlFunctions.ClickHouse.domain_without_www(x.Text),
            Significant = SqlFunctions.ClickHouse.first_significant_subdomain(x.Text),
            SignificantCustom = SqlFunctions.ClickHouse.first_significant_subdomain_custom(x.Text, "l"),
            PathFull = SqlFunctions.ClickHouse.path_full(x.Text),
            QueryAndFragment = SqlFunctions.ClickHouse.query_string_and_fragment(x.Text),
            CutWww = SqlFunctions.ClickHouse.cut_www(x.Text),
            CutQuery = SqlFunctions.ClickHouse.cut_query_string(x.Text),
            CutFragment = SqlFunctions.ClickHouse.cut_fragment(x.Text),
            CutQueryAndFragment = SqlFunctions.ClickHouse.cut_query_string_and_fragment(x.Text),
            CutSignificant = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain(x.Text),
            CutSignificantWww = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain_with_www(x.Text),
            CutSignificantCustom = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain_custom(x.Text, "l"),
            CutSignificantCustomWww =
                SqlFunctions.ClickHouse.cut_to_first_significant_subdomain_custom_with_www(x.Text, "l"),
            Hierarchy = SqlFunctions.ClickHouse.url_hierarchy(x.TextNonNull),
            PathHierarchy = SqlFunctions.ClickHouse.url_path_hierarchy(x.TextNonNull),
            Parameter = SqlFunctions.ClickHouse.extract_url_parameter(x.Text, "a"),
            ParameterNames = SqlFunctions.ClickHouse.extract_url_parameter_names(x.TextNonNull),
            Ipv4Text = SqlFunctions.ClickHouse.ipv4_num_to_string(x.U32),
            Ipv4ClassC = SqlFunctions.ClickHouse.ipv4_num_to_string_class_c(x.U32),
            Ipv4Num = SqlFunctions.ClickHouse.ipv4_string_to_num(x.Text),
            Ipv4Default = SqlFunctions.ClickHouse.ipv4_string_to_num_or_default(x.Text),
            Ipv4Null = SqlFunctions.ClickHouse.ipv4_string_to_num_or_null(x.Text),
            Ipv6Text = SqlFunctions.ClickHouse.ipv6_num_to_string(x.Text),
            Ipv6Hex = SqlFunctions.ClickHouse.ipv6_string_to_num(x.Text),
            Ipv6Default = SqlFunctions.ClickHouse.ipv6_string_to_num_or_default(x.Text),
            Ipv6Null = SqlFunctions.ClickHouse.ipv6_string_to_num_or_null(x.Text),
            CutIpv6 = SqlFunctions.ClickHouse.cut_ipv6(x.Text, 6, 1),
            IsIpv4 = SqlFunctions.ClickHouse.is_ipv4_string(x.Text),
            IsIpv6 = SqlFunctions.ClickHouse.is_ipv6_string(x.Text),
            InRange = SqlFunctions.ClickHouse.is_ip_address_in_range(x.Text, "1.2.3.0/24"),
            ToIpv4 = SqlFunctions.ClickHouse.to_ipv4(x.Text),
            ToIpv6 = SqlFunctions.ClickHouse.to_ipv6(x.Text),
            Mapped = SqlFunctions.ClickHouse.ipv4_to_ipv6(x.Address),
            Cidr4 = SqlFunctions.ClickHouse.ipv4_cidr_to_range(x.Address, 24),
            Cidr6 = SqlFunctions.ClickHouse.ipv6_cidr_to_range(x.Address6, 48)
        }));

        using (new AssertionScope())
        {
            foreach (var alias in new[]
                     {
                         "Domain", "Protocol", "Path", "Query", "Fragment", "Port", "CutParameter",
                         "TopLevel", "Encoded", "Decoded", "FormEncoded", "FormDecoded", "WithoutWww",
                         "Significant", "SignificantCustom", "PathFull", "QueryAndFragment", "CutWww",
                         "CutQuery", "CutFragment", "CutQueryAndFragment", "CutSignificant",
                         "CutSignificantWww", "CutSignificantCustom", "CutSignificantCustomWww",
                         "Hierarchy", "PathHierarchy", "Parameter", "ParameterNames", "Ipv4Text",
                         "Ipv4ClassC", "Ipv4Num", "Ipv4Default", "Ipv4Null", "Ipv6Text", "Ipv6Hex",
                         "Ipv6Default", "Ipv6Null", "CutIpv6", "IsIpv4", "IsIpv6", "InRange",
                         "ToIpv4", "ToIpv6", "Mapped", "Cidr4", "Cidr6"
                     })
                sql.Should().Contain($"as `{alias}`");
        }
    }

    [Fact]
    public void PostgresAndSqlServer_ShouldRejectEveryDeliveredMember()
    {
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var name in DeliveredNames)
            {
                AssertRejected(postgres, name);
                AssertRejected(sqlServer, name);
            }
        }

        static void AssertRejected(IDataContext ctx, string name)
        {
            Func<string> query = name switch
            {
                "domain" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.domain(x.Text) })),
                "protocol" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.protocol(x.Text) })),
                "path" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.path(x.Text) })),
                "query_string" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.query_string(x.Text) })),
                "fragment" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.fragment(x.Text) })),
                "port" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.port(x.Text) })),
                "cut_url_parameter" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.cut_url_parameter(x.Text, "a") })),
                "top_level_domain" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.top_level_domain(x.Text) })),
                "encode_url_component" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.encode_url_component(x.Text) })),
                "decode_url_component" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.decode_url_component(x.Text) })),
                "encode_url_form_component" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.encode_url_form_component(x.Text) })),
                "decode_url_form_component" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.decode_url_form_component(x.Text) })),
                "domain_without_www" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.domain_without_www(x.Text) })),
                "first_significant_subdomain" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.first_significant_subdomain(x.Text) })),
                "first_significant_subdomain_custom" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.first_significant_subdomain_custom(x.Text, "l") })),
                "path_full" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.path_full(x.Text) })),
                "query_string_and_fragment" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.query_string_and_fragment(x.Text) })),
                "cut_www" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.cut_www(x.Text) })),
                "cut_query_string" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.cut_query_string(x.Text) })),
                "cut_fragment" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.cut_fragment(x.Text) })),
                "cut_query_string_and_fragment" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.cut_query_string_and_fragment(x.Text) })),
                "cut_to_first_significant_subdomain" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain(x.Text) })),
                "cut_to_first_significant_subdomain_with_www" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain_with_www(x.Text) })),
                "cut_to_first_significant_subdomain_custom" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain_custom(x.Text, "l") })),
                "cut_to_first_significant_subdomain_custom_with_www" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.cut_to_first_significant_subdomain_custom_with_www(x.Text, "l") })),
                "url_hierarchy" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.url_hierarchy(x.TextNonNull) })),
                "url_path_hierarchy" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.url_path_hierarchy(x.TextNonNull) })),
                "extract_url_parameter" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.extract_url_parameter(x.Text, "a") })),
                "extract_url_parameter_names" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.extract_url_parameter_names(x.TextNonNull) })),
                "ipv4_num_to_string" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv4_num_to_string(x.U32) })),
                "ipv4_num_to_string_class_c" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv4_num_to_string_class_c(x.U32) })),
                "ipv4_string_to_num" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv4_string_to_num(x.Text) })),
                "ipv4_string_to_num_or_default" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv4_string_to_num_or_default(x.Text) })),
                "ipv4_string_to_num_or_null" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv4_string_to_num_or_null(x.Text) })),
                "ipv6_num_to_string" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv6_num_to_string(x.Text) })),
                "ipv6_string_to_num" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv6_string_to_num(x.Text) })),
                "ipv6_string_to_num_or_default" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv6_string_to_num_or_default(x.Text) })),
                "ipv6_string_to_num_or_null" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv6_string_to_num_or_null(x.Text) })),
                "cut_ipv6" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.cut_ipv6(x.Text, 6, 1) })),
                "is_ipv4_string" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.is_ipv4_string(x.Text) })),
                "is_ipv6_string" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.is_ipv6_string(x.Text) })),
                "is_ip_address_in_range" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.is_ip_address_in_range(x.Text, "1.2.3.0/24") })),
                "to_ipv4" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.to_ipv4(x.Text) })),
                "to_ipv6" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.to_ipv6(x.Text) })),
                "ipv4_to_ipv6" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv4_to_ipv6(x.Address) })),
                "ipv4_cidr_to_range" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv4_cidr_to_range(x.Address, 24) })),
                "ipv6_cidr_to_range" => () => SqlOf(ctx, ctx.From<IT286bUrlIpEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ipv6_cidr_to_range(x.Address6, 48) })),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>(
                "'{0}' is ClickHouse-only and must be refused by every other provider", name)
                .WithMessage("*not supported by this provider*");
        }
    }
}
