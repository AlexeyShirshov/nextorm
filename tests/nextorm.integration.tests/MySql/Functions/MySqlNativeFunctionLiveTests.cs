using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// The MySQL native-function surface of <c>MySqlNativeFunctions</c> executed against a live MySQL
/// Testcontainers server (or the server named by <c>NEXTORM_MYSQL_CONNECTION</c>). Every delivered name
/// of the renderer is called through the production path over the class-owned <c>mfn_probe</c>
/// temporary table, and the value it returns must equal the value the server returns for the same
/// vendor spelling, executed on the same connection in the same session.
///
/// <para>
/// The comparison is the point of the test: the rendered SQL is what ships, so the only meaningful
/// contract is "the rendered call means the same thing as the vendor function". A test that merely
/// executed the renderer arm would prove nothing about the SQL it produced.
/// </para>
///
/// <para>
/// Names whose result is not a function of the arguments (<c>RAND()</c>, <c>RANDOM_BYTES</c>,
/// <c>UUID_SHORT</c>, <c>SLEEP</c> and the session-state readers) are asserted separately by their
/// documented shape or range, because two evaluations legitimately disagree on the value.
/// </para>
///
/// <para>
/// The class follows the MySQL provider convention (<see cref="ProviderTestSuite"/> with the
/// <c>MySql</c> collection), so a missing server fails closed instead of turning the class into skips,
/// and the <c>IntegrationSuiteScopingTests</c> database-evidence markers hold (container type, harness
/// type, live connection).
/// </para>
/// </summary>
[Collection("MySql")]
public sealed class MySqlNativeFunctionLiveTests : ProviderTestSuite
{
    private static readonly byte[] Text = Encoding.UTF8.GetBytes("text");

    private static readonly byte[] Key = Encoding.UTF8.GetBytes("key");

    private static readonly DateTime Day = new(2020, 1, 1);

    private static readonly DateTime DayStart = new(2020, 1, 1, 0, 0, 0);

    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    private IDataContext Context => _sut.DataProvider;

    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    public MySqlNativeFunctionLiveTests()
    {
        Execute("drop temporary table if exists mfn_probe");
        Execute("create temporary table mfn_probe "
                + "(id int not null primary key, x bigint unsigned not null, doc json null, s varchar(20) null default 'one')");
        Execute("insert into mfn_probe (id, x, doc, s) values "
                + "(1, 1, '{\"a\":1}', 'one'), (2, 3, '{\"a\":2}', 'two')");
    }

    /// <summary>Every delivered name of the renderer, one test case each.</summary>
    public static TheoryData<string> DeliveredNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var @case in Cases)
                data.Add(@case.Id);
            return data;
        }
    }

    /// <summary>
    /// One delivered name: the rendered call must return what the server returns for the vendor
    /// spelling, and must materialise the declared surface type.
    /// </summary>
    /// <param name="name">The case id, i.e. the member name.</param>
    [Theory]
    [MemberData(nameof(DeliveredNames))]
    public void DeliveredNames_ShouldMatchTheServerEvaluation(string name)
    {
        var @case = Cases.Single(c => c.Id == name);

        var expected = Vendor(@case.Sql);
        object? actual = null;
        var thrown = Record.Exception(() => actual = @case.Translated(Context));

        thrown.Should().BeNull($"the rendered {name} call must execute on the MySQL server");
        Same(expected, actual).Should().BeTrue(
            $"the rendered {name} call must return what the server returns for '{@case.Sql}': " +
            $"server='{Format(expected)}', rendered='{Format(actual)}'");

        if (@case.ExpectedType is { } type && expected is not null)
            actual.Should().BeOfType(type, $"the {name} surface member declares that CLR return type");
    }

    /// <summary><c>RAND()</c> draws a fresh number per call: only the documented range is checkable.</summary>
    [Fact]
    public void Rand_ShouldDrawFromTheUnitInterval()
    {
        var unseeded = Project<double?>(Context, x => SqlFunctions.MySql.rand());
        var seeded = Project<double?>(Context, x => SqlFunctions.MySql.rand(42));

        unseeded.Should().BeInRange(0d, 0.9999999999999999d);
        seeded.Should().BeInRange(0d, 0.9999999999999999d);
        seeded.Should().Be(Project<double?>(Context, x => SqlFunctions.MySql.rand(42)),
            "the seeded form is reproducible");
    }

    /// <summary><c>RANDOM_BYTES(n)</c> is random: the contract is the requested length on both sides.</summary>
    [Fact]
    public void RandomBytes_ShouldReturnTheRequestedLength()
    {
        var bytes = Project<byte[]?>(Context, x => SqlFunctions.MySql.random_bytes(4));

        bytes.Should().HaveCount(4);
        Vendor("select random_bytes(4)").Should().BeOfType<byte[]>()
            .Which.Should().HaveCount(4, "the vendor call is the reference the rendering must match");
    }

    /// <summary><c>UUID_SHORT()</c> is a monotonically increasing counter, not a repeatable value.</summary>
    [Fact]
    public void UuidShort_ShouldReturnAPositiveCounter()
    {
        var first = Project<ulong?>(Context, x => SqlFunctions.MySql.uuid_short());
        var second = Project<ulong?>(Context, x => SqlFunctions.MySql.uuid_short());

        first.Should().BePositive();
        second.Should().BeGreaterThan(first!.Value, "UUID_SHORT() increments per call");
    }

    /// <summary><c>SLEEP(0)</c> returns 0 and must not block the suite.</summary>
    [Fact]
    public void Sleep_ShouldReturnZeroImmediately()
    {
        Project<long?>(Context, x => SqlFunctions.MySql.sleep(0)).Should().Be(0);
    }

    /// <summary>
    /// The session-state readers report the state of the connection, so the assertion is the documented
    /// value after a known setup rather than a comparison with a second read.
    /// </summary>
    [Fact]
    public void SessionReaders_ShouldReportTheSessionStateOfTheSameConnection()
    {
        Execute("do 1");

        Project<long?>(Context, x => SqlFunctions.MySql.row_count()).Should().Be(0,
            "ROW_COUNT() reports the affected rows of the preceding no-op statement");

        Project<ulong?>(Context, x => SqlFunctions.MySql.last_insert_id(7UL)).Should().Be(7UL,
            "the write form of LAST_INSERT_ID sets and returns its argument");
        Project<ulong?>(Context, x => SqlFunctions.MySql.last_insert_id()).Should().Be(7UL,
            "the reader returns the value the write form pinned");

        Project<long?>(Context, x => SqlFunctions.MySql.found_rows()).Should().Be(1,
            "FOUND_ROWS() reports the row count of the preceding single-row select");
    }

    /// <summary>
    /// <c>CURRENT_ROLE()</c> reports the session's roles; a fresh MySQL 8.4 session answers the literal
    /// <c>NONE</c>, so the rendered member must report exactly what the server reports for the same
    /// statement rather than a value of its own.
    /// </summary>
    [Fact]
    public void CurrentRole_ShouldReportTheServerRoleListOfTheSameSession()
    {
        var server = Vendor("select current_role()")
            .Should().BeOfType<string>("MySQL 8.4 answers a role list, not NULL, without a granted role")
            .Subject;

        server.Should().Be("NONE", "a fresh session has no granted role");

        Project<string?>(Context, x => SqlFunctions.MySql.current_role())
            .Should().Be(server, "the rendered call reads the same session state");
    }

    // ---- translate-time validation: the grammar of these names is refused before any SQL runs ----

    /// <summary><c>JSON_OBJECT</c> reads key/value pairs, so an odd argument count is a shape error.</summary>
    [Fact]
    public void JsonObject_WithOddArgumentCount_ShouldBeRefusedBeforeSql() =>
        FluentActions.Invoking(() => Project<string?>(Context, x => SqlFunctions.MySql.json_object("k")))
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL JSON_OBJECT() requires an even number of key/value arguments.");

    /// <summary><c>JSON_CONTAINS_PATH</c>'s <c>one_or_all</c> is grammar: only the literals one/all are valid.</summary>
    [Fact]
    public void JsonContainsPath_WithUnknownOneOrAll_ShouldBeRefused()
    {
        FluentActions.Invoking(() => Project<long?>(Context, x =>
                SqlFunctions.MySql.json_contains_path("{\"a\":1}", "two", "$.a")))
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL JSON_CONTAINS_PATH() requires one_or_all to be the compile-time literal 'one' or 'all'.");
    }

    /// <summary>Every JSON path is inlined, so a captured path cannot be rendered and is refused.</summary>
    [Fact]
    public void JsonContainsPath_WithNonLiteralPath_ShouldBeRefused()
    {
        var path = "$.a";

        FluentActions.Invoking(() => Project<long?>(Context, x =>
                SqlFunctions.MySql.json_contains_path("{\"a\":1}", "one", path)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL JSON_CONTAINS_PATH() requires every path argument to be a compile-time string literal.");
    }

    /// <summary><c>JSON_SEARCH</c>'s escape is a single character by contract.</summary>
    [Fact]
    public void JsonSearch_WithMultiCharacterEscape_ShouldBeRefused() =>
        FluentActions.Invoking(() => Project<string?>(Context, x =>
                SqlFunctions.MySql.json_search("[\"abc\"]", "one", "abc", "ab")))
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL JSON_SEARCH() requires escape_char to be a compile-time single-character literal.");

    /// <summary>A NULL escape is accepted (no escaping requested) and the call executes live.</summary>
    [Fact]
    public void JsonSearch_WithNullEscape_ShouldMatchTheServerEvaluation()
    {
        const string json = "[\"abc\"]";
        var expected = Vendor($"select json_search('{json}','one','abc',null)");
        var actual = Project<string?>(Context, x =>
            SqlFunctions.MySql.json_search(json, "one", "abc", (string?)null));

        actual.Should().Be(expected?.ToString(), "a NULL escape is a legal grammar argument, so the call must execute");
    }

    /// <summary><c>MEMBER OF</c> compares against an inline JSON array literal, never a parameter.</summary>
    [Fact]
    public void MemberOf_WithNonLiteralArray_ShouldBeRefused()
    {
        var array = "[\"a\",\"b\"]";

        FluentActions.Invoking(() => Project<long?>(Context, x => SqlFunctions.MySql.member_of("a", array)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL MEMBER OF requires argument 1 to be a compile-time JSON-array string literal.");
    }

    /// <summary>A NULL array is refused as well: the operand may not be null (only a non-literal is equally refused).</summary>
    [Fact]
    public void MemberOf_WithNullArray_ShouldBeRefused() =>
        FluentActions.Invoking(() => Project<long?>(Context, x => SqlFunctions.MySql.member_of("a", null!)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL MEMBER OF requires argument 1 to be a compile-time JSON-array string literal.");

    /// <summary><c>GET_FORMAT</c> accepts only the five documented platform names.</summary>
    [Fact]
    public void GetFormat_WithUnknownFormatName_ShouldBeRefused() =>
        FluentActions.Invoking(() => Project<string?>(Context, x =>
                SqlFunctions.MySql.get_format(MySqlFormatKind.Date, "XYZ")))
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL GET_FORMAT() requires a compile-time IANA format kind and format literal.");

    /// <summary>A kind that is not a compile-time constant cannot be turned into the bare keyword.</summary>
    [Fact]
    public void GetFormat_WithNonConstantKind_ShouldBeRefused()
    {
        var kind = MySqlFormatKind.Date;

        FluentActions.Invoking(() => Project<string?>(Context, x => SqlFunctions.MySql.get_format(kind, "EUR")))
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL GET_FORMAT() requires a compile-time IANA format kind and format literal.");
    }

    /// <summary>Every kind reaches its own keyword arm and the server answers the same format string.</summary>
    [Theory]
    [InlineData("TIME")]
    [InlineData("DATETIME")]
    public void GetFormat_ShouldRenderEveryKindKeyword(string keyword)
    {
        Func<IDataContext, object?> projection = keyword switch
        {
            "TIME" => c => Project(c, x => SqlFunctions.MySql.get_format(MySqlFormatKind.Time, "USA")),
            "DATETIME" => c => Project(c, x => SqlFunctions.MySql.get_format(MySqlFormatKind.DateTime, "ISO")),
            _ => throw new ArgumentOutOfRangeException(nameof(keyword), keyword, "Unknown GET_FORMAT kind.")
        };
        var sql = keyword == "TIME"
            ? "select get_format(TIME,'USA')"
            : "select get_format(DATETIME,'ISO')";

        Convert.ToString(projection(Context), CultureInfo.InvariantCulture)
            .Should().Be(Convert.ToString(Vendor(sql), CultureInfo.InvariantCulture),
                $"the {keyword} keyword arm must produce the vendor format string");
    }

    /// <summary><c>NAME_CONST</c>'s label must be a non-null compile-time literal.</summary>
    [Fact]
    public void NameConst_WithNullName_ShouldBeRefused() =>
        FluentActions.Invoking(() => Context.From<MfnProbe>().Where(x => x.Id == 1)
                .Select(x => SqlFunctions.MySql.name_const((string?)null, 1)).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL NAME_CONST() requires a non-null name literal.");

    /// <summary>A captured label is a parameter, and MySQL rejects a parameter in that position.</summary>
    [Fact]
    public void NameConst_WithNonConstantName_ShouldBeRefused()
    {
        var name = "n";

        FluentActions.Invoking(() => Context.From<MfnProbe>().Where(x => x.Id == 1)
                .Select(x => SqlFunctions.MySql.name_const(name, 1)).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL NAME_CONST() requires argument 0 to be a compile-time string literal.");
    }

    /// <summary>The weight_string cast keyword must be a constant the renderer can turn into CHAR/BINARY.</summary>
    [Fact]
    public void WeightString_WithNonConstantCast_ShouldBeRefused()
    {
        var cast = MySqlWeightStringCast.Char;

        FluentActions.Invoking(() => Project<byte[]?>(Context, x =>
                SqlFunctions.MySql.weight_string("abc", cast, 4)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The weight_string function requires a constant cast*");
    }

    /// <summary>The weight_string length is inlined as a token, so it must be a constant.</summary>
    [Fact]
    public void WeightString_WithNonConstantLength_ShouldBeRefused()
    {
        var length = 4;

        FluentActions.Invoking(() => Project<byte[]?>(Context, x =>
                SqlFunctions.MySql.weight_string("abc", MySqlWeightStringCast.Char, length)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The weight_string function requires a constant length.");
    }

    /// <summary>A zero-length weight string has no meaning and is refused.</summary>
    [Fact]
    public void WeightString_WithZeroLength_ShouldBeRefused() =>
        FluentActions.Invoking(() => Project<byte[]?>(Context, x =>
                SqlFunctions.MySql.weight_string("abc", MySqlWeightStringCast.Char, 0)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The weight_string function requires a length of at least 1.");

    /// <summary>The BINARY cast modifier is the other keyword arm and must match the vendor bytes.</summary>
    [Fact]
    public void WeightString_WithBinaryCast_ShouldMatchTheServerEvaluation()
    {
        var expected = Vendor("select weight_string('abc' as binary(4))");
        var actual = Project<byte[]?>(Context, x =>
            SqlFunctions.MySql.weight_string("abc", MySqlWeightStringCast.Binary, 4));

        Same(expected, actual).Should().BeTrue(
            $"weight_string(… as binary(4)) must return the server bytes; server='{Format(expected)}', rendered='{Format(actual)}'");
    }

    /// <summary><c>DEFAULT(col)</c> is grammar over a column identifier, not a scalar function.</summary>
    [Fact]
    public void ColumnDefault_WithConstantArgument_ShouldBeRefused() =>
        FluentActions.Invoking(() => Project<string?>(Context, x => SqlFunctions.MySql.column_default("literal")))
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL DEFAULT() requires a column reference; constants, parameters, casts, and computed expressions are not supported.");

    /// <summary><c>EXPORT_SET</c>'s bit count is a compile-time grammar token bounded to 0..64.</summary>
    [Fact]
    public void ExportSet_WithOutOfRangeBitCount_ShouldBeRefused() =>
        FluentActions.Invoking(() => Project<string?>(Context, x =>
                SqlFunctions.MySql.export_set(5UL, "Y", "N", ",", 65)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The export_set function requires a bit count between 0 and 64 (got 65).");

    /// <summary>The guarded names refuse a call that cannot supply their minimum argument count.</summary>
    [Theory]
    [InlineData("field")]
    [InlineData("make_set")]
    public void NamesWithAMinimumArgumentCount_ShouldRefuseTooFewArguments(string name)
    {
        object? Invoke() => name switch
        {
            "field" => Project<int?>(Context, x => SqlFunctions.MySql.field("a")),
            "make_set" => Project<string?>(Context, x => SqlFunctions.MySql.make_set(1UL)),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown guarded name.")
        };

        FluentActions.Invoking(Invoke)
            .Should().Throw<NotSupportedException>()
            .WithMessage($"The {name} function requires at least one value argument.");
    }

    /// <summary><c>JSON_CONTAINS_PATH</c> carries its own arity message because its tail is the path list.</summary>
    [Fact]
    public void JsonContainsPath_WithTooFewArguments_ShouldBeRefused() =>
        FluentActions.Invoking(() => Project<long?>(Context, x =>
                SqlFunctions.MySql.json_contains_path("{\"a\":1}", "one")))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The json_contains_path function requires at least one path argument.");

    /// <summary>The <c>-&gt;</c> operator needs a column identifier on the left, never a value.</summary>
    [Fact]
    public void JsonArrow_WithConstantLeftOperand_ShouldBeRefused() =>
        FluentActions.Invoking(() => Project<string?>(Context, x =>
                SqlFunctions.MySql.json_arrow("{\"a\":1}", "$.a")))
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL operator '->' requires argument 0 to be a column reference.");

    /// <summary>and a compile-time path literal on the right.</summary>
    [Fact]
    public void JsonArrow_WithNonConstantPath_ShouldBeRefused()
    {
        var path = "$.a";

        FluentActions.Invoking(() => Project<string?>(Context, x =>
                SqlFunctions.MySql.json_arrow(x.Doc, path)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("MySQL operator '->' requires argument 1 to be a compile-time string literal.");
    }

    private object? Vendor(string sql)
    {
        using var command = ((DataContext)Context).CreateCommand(sql);
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>Evaluates one scalar projection over the probe row.</summary>
    private static T? Project<T>(IDataContext ctx, System.Linq.Expressions.Expression<Func<MfnProbe, T?>> projection) =>
        ctx.From<MfnProbe>().Where(x => x.Id == 1).Select(projection).ToList()[0];

    /// <summary>Evaluates one aggregate projection over both probe rows.</summary>
    private static T? Aggregate<T>(IDataContext ctx, System.Linq.Expressions.Expression<Func<MfnProbe, T?>> projection) =>
        ctx.From<MfnProbe>().Select(projection).ToList()[0];

    /// <summary>Value equality: NULL only equals NULL, blobs compare by content, numbers by value.</summary>
    private static bool Same(object? expected, object? actual)
    {
        if (expected is null || actual is null)
            return expected is null && actual is null;

        if (expected is byte[] left && actual is byte[] right)
            return left.AsSpan().SequenceEqual(right);

        if (IsNumber(expected) && IsNumber(actual))
            return Convert.ToDecimal(expected, CultureInfo.InvariantCulture)
                == Convert.ToDecimal(actual, CultureInfo.InvariantCulture);

        if (expected is string expectedText && actual is string actualText)
            return string.Equals(expectedText, actualText, StringComparison.Ordinal);

        return expected.Equals(actual);
    }

    private static bool IsNumber(object value) => value
        is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static string Format(object? value) => value switch
    {
        null => "<null>",
        byte[] bytes => "0x" + Convert.ToHexString(bytes),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "<null>"
    };

    /// <summary>
    /// The delivered names. <see cref="Sql"/> is the vendor spelling executed directly on the
    /// connection; <see cref="Translated"/> is the same call through the production renderer.
    /// </summary>
    private static readonly NativeCase[] Cases =
    [
        // ---- string ----
        new("find_in_set", "select find_in_set('b','a,b')",
            c => Project(c, x => SqlFunctions.MySql.find_in_set("b", "a,b")), typeof(int)),
        new("field", "select field('b','a','b')",
            c => Project(c, x => SqlFunctions.MySql.field("b", "a", "b")), typeof(int)),
        new("elt", "select elt(2,'a','b')",
            c => Project(c, x => SqlFunctions.MySql.elt<string>(2, "a", "b")), typeof(string)),
        new("substring_index", "select substring_index('a,b,c',',',2)",
            c => Project(c, x => SqlFunctions.MySql.substring_index("a,b,c", ",", 2)), typeof(string)),
        new("format", "select format(1234.5,2)",
            c => Project(c, x => SqlFunctions.MySql.format(1234.5m, 2)), typeof(string)),
        new("md5", "select md5('abc')",
            c => Project(c, x => SqlFunctions.MySql.md5("abc")), typeof(string)),
        new("sha1", "select sha1('abc')",
            c => Project(c, x => SqlFunctions.MySql.sha1("abc")), typeof(string)),
        new("sha2", "select sha2('abc',256)",
            c => Project(c, x => SqlFunctions.MySql.sha2("abc", 256)), typeof(string)),
        new("inet_aton", "select inet_aton('1.2.3.4')",
            c => Project(c, x => SqlFunctions.MySql.inet_aton("1.2.3.4")), typeof(long)),
        new("inet_ntoa", "select inet_ntoa(16909060)",
            c => Project(c, x => SqlFunctions.MySql.inet_ntoa(16909060L)), typeof(string)),

        // ---- date/time ----
        new("str_to_date", "select str_to_date('2020-01-01','%Y-%m-%d')",
            c => Project(c, x => SqlFunctions.MySql.str_to_date("2020-01-01", "%Y-%m-%d")), typeof(DateTime)),
        new("date_format", "select date_format('2020-01-01','%Y')",
            c => Project(c, x => SqlFunctions.MySql.date_format(Day, "%Y")), typeof(string)),
        new("from_unixtime", "select from_unixtime(0)",
            c => Project(c, x => SqlFunctions.MySql.from_unixtime(0L)), typeof(DateTime)),
        new("unix_timestamp", "select unix_timestamp('2020-01-01 00:00:00')",
            c => Project(c, x => SqlFunctions.MySql.unix_timestamp(DayStart)), typeof(long)),
        // The vendor statement casts its literal so the server resolves a temporal result type: an
        // untyped string argument makes MySQL return a VARCHAR, which is not what the surface declares.
        new("adddate", "select adddate(cast('2020-01-01' as datetime), 1)",
            c => Project(c, x => SqlFunctions.MySql.adddate(Day, 1)), typeof(DateTime)),
        new("addtime", "select addtime(cast('2020-01-01 00:00:00' as datetime), cast('01:00:00' as time))",
            c => Project(c, x => SqlFunctions.MySql.addtime(DayStart, OneHour)), typeof(DateTime)),
        new("convert_tz", "select convert_tz('2020-01-01 00:00:00','+00:00','+02:00')",
            c => Project(c, x => SqlFunctions.MySql.convert_tz(DayStart, "+00:00", "+02:00")), typeof(DateTime)),
        new("dayname", "select dayname('2020-01-01')",
            c => Project(c, x => SqlFunctions.MySql.dayname(Day)), typeof(string)),
        new("from_days", "select from_days(738000)",
            c => Project(c, x => SqlFunctions.MySql.from_days(738000L)), typeof(DateTime)),
        new("get_format", "select get_format(DATE,'EUR')",
            c => Project(c, x => SqlFunctions.MySql.get_format(MySqlFormatKind.Date, "EUR")), typeof(string)),
        new("makedate", "select makedate(2020,32)",
            c => Project(c, x => SqlFunctions.MySql.makedate(2020, 32)), typeof(DateTime)),
        new("maketime", "select maketime(12,34,56)",
            c => Project(c, x => SqlFunctions.MySql.maketime(12, 34, 56m)), typeof(TimeSpan)),
        new("microsecond", "select microsecond('2020-01-01 00:00:00.123456')",
            c => Project(c, x => SqlFunctions.MySql.microsecond(DayStart.AddTicks(1_234_560))), typeof(long)),
        new("monthname", "select monthname('2020-01-01')",
            c => Project(c, x => SqlFunctions.MySql.monthname(Day)), typeof(string)),
        new("period_add", "select period_add(202401,2)",
            c => Project(c, x => SqlFunctions.MySql.period_add(202401L, 2L)), typeof(long)),
        new("period_diff", "select period_diff(202402,202401)",
            c => Project(c, x => SqlFunctions.MySql.period_diff(202402L, 202401L)), typeof(long)),
        new("sec_to_time", "select sec_to_time(3661)",
            c => Project(c, x => SqlFunctions.MySql.sec_to_time(3661m)), typeof(TimeSpan)),
        new("subdate", "select subdate(cast('2020-01-01' as datetime), 1)",
            c => Project(c, x => SqlFunctions.MySql.subdate(Day, 1)), typeof(DateTime)),
        new("subtime", "select subtime(cast('2020-01-01 00:00:00' as datetime), cast('01:00:00' as time))",
            c => Project(c, x => SqlFunctions.MySql.subtime(DayStart, OneHour)), typeof(DateTime)),
        new("time", "select time('2020-01-01 12:34:56')",
            c => Project(c, x => SqlFunctions.MySql.time(DayStart.AddHours(12).AddMinutes(34).AddSeconds(56))),
            typeof(TimeSpan)),
        new("time_format", "select time_format('12:34:56','%H:%i')",
            c => Project(c, x => SqlFunctions.MySql.time_format(new TimeSpan(12, 34, 56), "%H:%i")),
            typeof(string)),
        new("time_to_sec", "select time_to_sec('12:34:56')",
            c => Project(c, x => SqlFunctions.MySql.time_to_sec(new TimeSpan(12, 34, 56))), typeof(long)),
        new("timediff", "select timediff('2020-01-02 00:00:00','2020-01-01 00:00:00')",
            c => Project(c, x => SqlFunctions.MySql.timediff(DayStart.AddDays(1), DayStart)), typeof(TimeSpan)),
        new("timestamp", "select timestamp('2020-01-01 00:00:00','12:00:00')",
            c => Project(c, x => SqlFunctions.MySql.timestamp(DayStart, TimeSpan.FromHours(12))),
            typeof(DateTime)),
        new("to_days", "select to_days('2020-01-01')",
            c => Project(c, x => SqlFunctions.MySql.to_days(Day)), typeof(long)),
        new("to_seconds", "select to_seconds('2020-01-01 00:00:00')",
            c => Project(c, x => SqlFunctions.MySql.to_seconds(DayStart)), typeof(long)),
        new("yearweek", "select yearweek('2020-01-01',1)",
            c => Project(c, x => SqlFunctions.MySql.yearweek(Day, 1)), typeof(long)),

        // ---- JSON ----
        new("json_set", "select json_set('{\"a\":1}','$.a','2')",
            c => Project(c, x => SqlFunctions.MySql.json_set("{\"a\":1}", "$.a", "2")), typeof(string)),
        new("json_insert", "select json_insert('{\"a\":1}','$.b','2')",
            c => Project(c, x => SqlFunctions.MySql.json_insert("{\"a\":1}", "$.b", "2")), typeof(string)),
        new("json_replace", "select json_replace('{\"a\":1}','$.a','9')",
            c => Project(c, x => SqlFunctions.MySql.json_replace("{\"a\":1}", "$.a", "9")), typeof(string)),
        new("json_remove", "select json_remove('{\"a\":1}','$.a')",
            c => Project(c, x => SqlFunctions.MySql.json_remove("{\"a\":1}", "$.a")), typeof(string)),
        new("json_merge_patch", "select json_merge_patch('{\"a\":1}','{\"a\":2}')",
            c => Project(c, x => SqlFunctions.MySql.json_merge_patch("{\"a\":1}", "{\"a\":2}")), typeof(string)),
        new("json_merge_preserve", "select json_merge_preserve('{\"a\":1}','{\"a\":2}')",
            c => Project(c, x => SqlFunctions.MySql.json_merge_preserve("{\"a\":1}", "{\"a\":2}")),
            typeof(string)),
        new("json_array_append", "select json_array_append('[1]','$','2')",
            c => Project(c, x => SqlFunctions.MySql.json_array_append("[1]", "$", "2")), typeof(string)),
        new("json_array_insert", "select json_array_insert('[1,3]','$[1]','2')",
            c => Project(c, x => SqlFunctions.MySql.json_array_insert("[1,3]", "$[1]", "2")), typeof(string)),
        new("json_depth", "select json_depth('{\"a\":{\"b\":1}}')",
            c => Project(c, x => SqlFunctions.MySql.json_depth("{\"a\":{\"b\":1}}")), typeof(int)),
        new("json_keys", "select json_keys('{\"a\":1,\"b\":2}')",
            c => Project(c, x => SqlFunctions.MySql.json_keys("{\"a\":1,\"b\":2}")), typeof(string)),
        new("json_length", "select json_length('{\"a\":1}')",
            c => Project(c, x => SqlFunctions.MySql.json_length("{\"a\":1}")), typeof(int)),
        new("json_type", "select json_type('{\"a\":1}')",
            c => Project(c, x => SqlFunctions.MySql.json_type("{\"a\":1}")), typeof(string)),
        new("json_storage_free", "select json_storage_free('{\"a\":1}')",
            c => Project(c, x => SqlFunctions.MySql.json_storage_free("{\"a\":1}")), typeof(long)),
        new("json_storage_size", "select json_storage_size('{\"a\":1}')",
            c => Project(c, x => SqlFunctions.MySql.json_storage_size("{\"a\":1}")), typeof(long)),
        new("json_array", "select json_array(1,'a')",
            c => Project(c, x => SqlFunctions.MySql.json_array(1, "a")), typeof(string)),
        new("json_object", "select json_object('k',1)",
            c => Project(c, x => SqlFunctions.MySql.json_object("k", 1)), typeof(string)),
        new("json_contains", "select json_contains('{\"a\":1}','1','$.a')",
            c => Project(c, x => SqlFunctions.MySql.json_contains("{\"a\":1}", "1", "$.a")), typeof(long)),
        new("json_contains_path", "select json_contains_path('{\"a\":1}','one','$.a')",
            c => Project(c, x => SqlFunctions.MySql.json_contains_path("{\"a\":1}", "one", "$.a")), typeof(long)),
        new("json_merge", "select json_merge('{\"a\":1}','{\"b\":2}')",
            c => Project(c, x => SqlFunctions.MySql.json_merge("{\"a\":1}", "{\"b\":2}")), typeof(string)),
        new("json_overlaps", "select json_overlaps('[1,2]','[2,3]')",
            c => Project(c, x => SqlFunctions.MySql.json_overlaps("[1,2]", "[2,3]")), typeof(long)),
        new("json_pretty", "select json_pretty('{\"a\":1}')",
            c => Project(c, x => SqlFunctions.MySql.json_pretty("{\"a\":1}")), typeof(string)),
        new("json_quote", "select json_quote('a\"b')",
            c => Project(c, x => SqlFunctions.MySql.json_quote("a\"b")), typeof(string)),
        new("json_schema_valid", "select json_schema_valid('{\"type\":\"object\"}','{}')",
            c => Project(c, x => SqlFunctions.MySql.json_schema_valid("{\"type\":\"object\"}", "{}")),
            typeof(long)),
        new("json_schema_validation_report", "select json_schema_validation_report('{\"type\":\"object\"}','{}')",
            c => Project(c, x => SqlFunctions.MySql.json_schema_validation_report("{\"type\":\"object\"}", "{}")),
            typeof(string)),
        new("json_search", "select json_search('[\"abc\"]','one','abc')",
            c => Project(c, x => SqlFunctions.MySql.json_search("[\"abc\"]", "one", "abc")), typeof(string)),
        new("json_arrow", "select doc -> '$.a' from mfn_probe where id = 1",
            c => Project(c, x => SqlFunctions.MySql.json_arrow(x.Doc, "$.a")), typeof(string)),
        new("json_arrow_text", "select doc ->> '$.a' from mfn_probe where id = 1",
            c => Project(c, x => SqlFunctions.MySql.json_arrow_text(x.Doc, "$.a")), typeof(string)),
        new("json_arrayagg", "select json_arrayagg(x) from mfn_probe",
            c => Aggregate(c, x => SqlFunctions.MySql.json_arrayagg(x.X)), typeof(string)),
        new("json_objectagg", "select json_objectagg(s,x) from mfn_probe",
            c => Aggregate(c, x => SqlFunctions.MySql.json_objectagg(x.S, x.X)), typeof(string)),
        new("member_of", "select 'a' member of ('[\"a\",\"b\"]')",
            c => Project(c, x => SqlFunctions.MySql.member_of("a", "[\"a\",\"b\"]")), typeof(long)),
        new("uuid_to_bin", "select uuid_to_bin('00000000-0000-0000-0000-000000000000')",
            c => Project(c, x => SqlFunctions.MySql.uuid_to_bin("00000000-0000-0000-0000-000000000000")),
            typeof(byte[])),
        // The binary argument is bound through the vendor's own hex decoder, so the rendered statement
        // carries the same 16 bytes the reference statement builds; a raw byte[] constant is rendered in
        // a shape the server rejects for this function ("Incorrect string value").
        new("bin_to_uuid", "select bin_to_uuid(unhex('00000000000000000000000000000000'))",
            c => Project(c, x => SqlFunctions.MySql.bin_to_uuid(
                SqlFunctions.MySql.unhex("00000000000000000000000000000000"))), typeof(string)),

        // ---- string/binary catalogue ----
        new("bin", "select bin(5)",
            c => Project(c, x => SqlFunctions.MySql.bin(5UL)), typeof(string)),
        new("export_set", "select export_set(5,'Y','N')",
            c => Project(c, x => SqlFunctions.MySql.export_set(5UL, "Y", "N")), typeof(string)),
        new("hex", "select hex('abc')",
            c => Project(c, x => SqlFunctions.MySql.hex("abc")), typeof(string)),
        new("make_set", "select make_set(1,'a','b')",
            c => Project(c, x => SqlFunctions.MySql.make_set(1UL, "a", "b")), typeof(string)),
        new("oct", "select oct(8)",
            c => Project(c, x => SqlFunctions.MySql.oct(8UL)), typeof(string)),
        new("ord", "select ord('A')",
            c => Project(c, x => SqlFunctions.MySql.ord("A")), typeof(long)),
        new("quote", "select quote('a''b')",
            c => Project(c, x => SqlFunctions.MySql.quote("a'b")), typeof(string)),
        new("soundex", "select soundex('hello')",
            c => Project(c, x => SqlFunctions.MySql.soundex("hello")), typeof(string)),
        new("strcmp", "select strcmp('a','b')",
            c => Project(c, x => SqlFunctions.MySql.strcmp("a", "b")), typeof(long)),
        new("to_base64", "select to_base64('abc')",
            c => Project(c, x => SqlFunctions.MySql.to_base64(Encoding.UTF8.GetBytes("abc"))), typeof(string)),
        new("from_base64", "select from_base64('YWJj')",
            c => Project(c, x => SqlFunctions.MySql.from_base64("YWJj")), typeof(byte[])),
        new("unhex", "select unhex('6162')",
            c => Project(c, x => SqlFunctions.MySql.unhex("6162")), typeof(byte[])),
        new("weight_string", "select weight_string('abc')",
            c => Project(c, x => SqlFunctions.MySql.weight_string("abc")), typeof(byte[])),
        new("weight_string_modifiers", "select weight_string('abc' as char(4))",
            c => Project(c, x => SqlFunctions.MySql.weight_string("abc", MySqlWeightStringCast.Char, 4)),
            typeof(byte[])),
        new("conv", "select conv('ff',16,10)",
            c => Project(c, x => SqlFunctions.MySql.conv("ff", 16, 10)), typeof(string)),
        new("crc32", "select crc32('abc')",
            c => Project(c, x => SqlFunctions.MySql.crc32("abc")), typeof(ulong)),
        new("log2", "select log2(8)",
            c => Project(c, x => SqlFunctions.MySql.log2(8d)), typeof(double)),
        new("truncate", "select truncate(1.2345,2)",
            c => Project(c, x => SqlFunctions.MySql.truncate(1.2345m, 2)), typeof(decimal)),
        new("bit_count", "select bit_count(7)",
            c => Project(c, x => SqlFunctions.MySql.bit_count(7UL)), typeof(long)),
        new("bit_and", "select bit_and(x) from mfn_probe",
            c => Aggregate(c, x => SqlFunctions.MySql.bit_and(x.X)), typeof(ulong)),
        new("bit_or", "select bit_or(x) from mfn_probe",
            c => Aggregate(c, x => SqlFunctions.MySql.bit_or(x.X)), typeof(ulong)),
        new("bit_xor", "select bit_xor(x) from mfn_probe",
            c => Aggregate(c, x => SqlFunctions.MySql.bit_xor(x.X)), typeof(ulong)),

        // ---- crypto ----
        new("aes_encrypt", "select aes_encrypt('text','key')",
            c => Project(c, x => SqlFunctions.MySql.aes_encrypt(Text, Key)), typeof(byte[])),
        new("aes_decrypt", "select aes_decrypt(aes_encrypt('text','key'),'key')",
            c => Project(c, x => SqlFunctions.MySql.aes_decrypt(
                SqlFunctions.MySql.aes_encrypt(Text, Key), Key)), typeof(byte[])),
        new("compress", "select compress('abc')",
            c => Project(c, x => SqlFunctions.MySql.compress(Encoding.UTF8.GetBytes("abc"))), typeof(byte[])),
        new("uncompress", "select uncompress(compress('abc'))",
            c => Project(c, x => SqlFunctions.MySql.uncompress(
                SqlFunctions.MySql.compress(Encoding.UTF8.GetBytes("abc")))), typeof(byte[])),
        new("uncompressed_length", "select uncompressed_length(compress('abc'))",
            c => Project(c, x => SqlFunctions.MySql.uncompressed_length(
                SqlFunctions.MySql.compress(Encoding.UTF8.GetBytes("abc")))), typeof(long)),
        new("validate_password_strength", "select validate_password_strength('abc')",
            c => Project(c, x => SqlFunctions.MySql.validate_password_strength("abc")), typeof(long)),
        new("statement_digest", "select statement_digest('SELECT 1')",
            c => Project(c, x => SqlFunctions.MySql.statement_digest("SELECT 1")), typeof(string)),
        new("statement_digest_text", "select statement_digest_text('SELECT 1')",
            c => Project(c, x => SqlFunctions.MySql.statement_digest_text("SELECT 1")), typeof(string)),

        // ---- information ----
        new("charset", "select charset('abc')",
            c => Project(c, x => SqlFunctions.MySql.charset("abc")), typeof(string)),
        new("collation", "select collation('abc')",
            c => Project(c, x => SqlFunctions.MySql.collation("abc")), typeof(string)),
        new("coercibility", "select coercibility('abc')",
            c => Project(c, x => SqlFunctions.MySql.coercibility("abc")), typeof(long)),
        new("benchmark", "select benchmark(1,1)",
            c => Project(c, x => SqlFunctions.MySql.benchmark(1L, 1)), typeof(long)),
        new("icu_version", "select icu_version()",
            c => Project(c, x => SqlFunctions.MySql.icu_version()), typeof(string)),
        new("is_uuid", "select is_uuid('00000000-0000-0000-0000-000000000000')",
            c => Project(c, x => SqlFunctions.MySql.is_uuid("00000000-0000-0000-0000-000000000000")),
            typeof(long)),
        new("name_const", "select name_const('n',1)",
            c => c.From<MfnProbe>().Where(x => x.Id == 1)
                .Select(x => SqlFunctions.MySql.name_const("n", 1)).ToList()[0], typeof(int)),
        new("column_default", "select default(s) from mfn_probe where id = 1",
            c => Project(c, x => SqlFunctions.MySql.column_default(x.S)), typeof(string))
    ];

    /// <summary>A delivered name: vendor SQL, the production projection and the declared surface type.</summary>
    /// <param name="Id">The surface member name.</param>
    /// <param name="Sql">The vendor spelling, executed directly on the same connection.</param>
    /// <param name="Translated">The same call through the production renderer.</param>
    /// <param name="ExpectedType">The declared surface type, or <see langword="null"/> when the value is not pinned.</param>
    internal sealed record NativeCase(
        string Id,
        string Sql,
        Func<IDataContext, object?> Translated,
        Type? ExpectedType);

    /// <summary>The two-row probe table every projection reads from.</summary>
    [SqlTable("mfn_probe")]
    public sealed class MfnProbe
    {
        /// <summary>The probe row identifier.</summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>An unsigned integer column, used by the bit aggregates.</summary>
        [Column("x")]
        public ulong X { get; set; }

        /// <summary>A native JSON column, the required left operand of the <c>-&gt;</c> operators.</summary>
        [Column("doc")]
        public string? Doc { get; set; }

        /// <summary>A text column with a column default, the operand of <c>DEFAULT(col)</c>.</summary>
        [Column("s")]
        public string? S { get; set; }
    }
}
