using System.Globalization;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// Шаг 2, unit C-2: массовый перебор имён T-SQL-поверхности SQL Server — <c>SqlServerScalarFunctions</c>
/// (<c>SqlFunctions.Sql.*</c>), <c>SqlServerSpecificFunctions</c> (<c>SqlFunctions.SqlServer.*</c>) и
/// транслятора <c>SqlServerScalarFunctionTranslator</c>. Каждая строка — реальный запрос к живому
/// контейнеру SQL Server; ожидание — vendor-написание того же вызова, исполненное на том же соединении
/// (или явное значение для кросс-провайдерных форм, которые T-SQL не выражает напрямую).
/// </summary>
[Collection("SqlServer")]
public sealed class SqlServerStep2UnitC2Tests : ProviderTestSuite
{
    /// <summary>A 129-character sequence component: one past the <c>sysname</c> limit, a constant.</summary>
    private const string OverlongIdentifier =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    /// <summary>Every delivered row of the unversioned sweep, one test case each.</summary>
    public static TheoryData<string> ScalarNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var @case in ScalarCases)
                data.Add(@case.Id);
            return data;
        }
    }

    /// <summary>Every delivered row of the versioned (major 17) sweep, one test case each.</summary>
    public static TheoryData<string> VersionedNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var @case in VersionedCases)
                data.Add(@case.Id);
            return data;
        }
    }

    /// <summary>Every row whose native outcome depends on engine prerequisites, one test case each.</summary>
    public static TheoryData<string> EnvironmentNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var @case in EnvironmentCases)
                data.Add(@case.Id);
            return data;
        }
    }

    /// <summary>Every row of the version-bound sweep whose outcome the engine decides, one test case each.</summary>
    public static TheoryData<string> VersionedEnvironmentNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var @case in VersionedEnvironmentCases)
                data.Add(@case.Id);
            return data;
        }
    }

    /// <summary>
    /// One name/arity of the unversioned surface: the rendered call must return what the server returns
    /// for the vendor spelling, and (when pinned) materialise the declared surface type.
    /// </summary>
    [Theory]
    [MemberData(nameof(ScalarNames))]
    public void ScalarSurface_ShouldMatchTheServerEvaluation(string name)
    {
        var @case = ScalarCases.Single(c => c.Id == name);

        var expected = @case.Sql is { } sql ? Execute(() => Vendor(Context, sql)) : @case.Expected;
        var actual = Execute(() => @case.Run(Context));

        Same(expected, actual).Should().BeTrue(
            $"the rendered {name} call must return what the server returns for {@case.Sql ?? "<explicit expected>"}: "
            + $"server='{Format(expected)}', rendered='{Format(actual)}'");

        if (@case.ExpectedType is { } type && expected is not null)
            actual.Should().BeOfType(type, $"the {name} surface member declares that CLR return type");
    }

    /// <summary>
    /// One name/arity of the version-bound surface (major 17): rendered through a declared-17 context and
    /// compared with the vendor spelling on the same connection.
    /// </summary>
    [Theory]
    [MemberData(nameof(VersionedNames))]
    public void VersionedSurface_ShouldMatchTheServerEvaluation(string name)
    {
        _ = Context; // seed the shared database before the versioned context opens it
        using var context = new SqlServerStep2UnitCVersionedContext(SqlServerContainer.ConnectionString);
        var @case = VersionedCases.Single(c => c.Id == name);

        var expected = @case.Sql is { } sql ? Execute(() => Vendor(context, sql)) : @case.Expected;
        var actual = Execute(() => @case.Run(context));

        Same(expected, actual).Should().BeTrue(
            $"the rendered {name} call must return what the server returns for {@case.Sql ?? "<explicit expected>"}: "
            + $"server='{Format(expected)}', rendered='{Format(actual)}'");

        if (@case.ExpectedType is { } type && expected is not null)
            actual.Should().BeOfType(type, $"the {name} surface member declares that CLR return type");
    }

    /// <summary>
    /// Rows whose native outcome depends on engine prerequisites the fixture does not control (a fuzzy
    /// family database prerequisite, an object a bare <c>select</c> cannot produce, a clock). The rendered
    /// call and the vendor spelling must agree: the same value, or both refused by the engine.
    /// </summary>
    [Theory]
    [MemberData(nameof(EnvironmentNames))]
    public void EnvironmentDependentSurface_ShouldAgreeWithTheServer(string name)
    {
        var @case = EnvironmentCases.Single(c => c.Id == name);

        object? expected = null;
        var vendorFailure = Record.Exception(() => expected = Execute(() => Vendor(Context, @case.Sql!)));
        object? actual = null;
        var renderedFailure = Record.Exception(() => actual = Execute(() => @case.Run(Context)));

        if (vendorFailure is not null)
        {
            // The vendor spelling is executed on the same connection, so a refusal here is the engine's
            // own answer. The rendered call must be refused for the same concrete reason (S2-F03): the
            // SQL error number of the two statements must match, not merely the exception type.
            if (vendorFailure is not SqlException vendorError)
            {
                renderedFailure.Should().BeOfType(vendorFailure.GetType(),
                    $"the vendor spelling '{@case.Sql}' failed with {vendorFailure.GetType().Name}, "
                    + $"so the rendered {name} call must fail the same way");
                return;
            }

            var renderedError = renderedFailure.Should().BeOfType<SqlException>(
                $"the vendor spelling '{@case.Sql}' is refused by the engine with Msg {vendorError.Number}, "
                + $"so the rendered {name} call is refused by the engine too").Subject;
            renderedError.Number.Should().Be(vendorError.Number,
                $"the rendered {name} call must hit the same engine refusal as the vendor spelling "
                + $"'{@case.Sql}' (vendor: Msg {vendorError.Number}: {vendorError.Message})");
            return;
        }

        renderedFailure.Should().BeNull($"the rendered {name} call must execute: {renderedFailure?.Message}");
        Same(expected, actual).Should().BeTrue(
            $"the rendered {name} call must agree with the server: "
            + $"server='{Format(expected)}', rendered='{Format(actual)}'");
    }

    /// <summary>
    /// The version-bound rows whose native outcome is decided by the engine: rendered through a
    /// declared-17 context and compared with the vendor spelling — value or the same refusal.
    /// </summary>
    [Theory]
    [MemberData(nameof(VersionedEnvironmentNames))]
    public void VersionedEnvironmentSurface_ShouldAgreeWithTheServer(string name)
    {
        _ = Context; // seed the shared database before the versioned context opens it
        using var context = new SqlServerStep2UnitCVersionedContext(SqlServerContainer.ConnectionString);
        var @case = VersionedEnvironmentCases.Single(c => c.Id == name);

        object? expected = null;
        var vendorFailure = Record.Exception(() => expected = Execute(() => Vendor(context, @case.Sql!)));
        object? actual = null;
        var renderedFailure = Record.Exception(() => actual = Execute(() => @case.Run(context)));

        if (vendorFailure is not null)
        {
            // The vendor spelling is executed on the same connection, so a refusal here is the engine's
            // own answer. The rendered call must be refused for the same concrete reason (S2-F03): the
            // SQL error number of the two statements must match, not merely the exception type.
            if (vendorFailure is not SqlException vendorError)
            {
                renderedFailure.Should().BeOfType(vendorFailure.GetType(),
                    $"the vendor spelling '{@case.Sql}' failed with {vendorFailure.GetType().Name}, "
                    + $"so the rendered {name} call must fail the same way");
                return;
            }

            var renderedError = renderedFailure.Should().BeOfType<SqlException>(
                $"the vendor spelling '{@case.Sql}' is refused by the engine with Msg {vendorError.Number}, "
                + $"so the rendered {name} call is refused by the engine too").Subject;
            renderedError.Number.Should().Be(vendorError.Number,
                $"the rendered {name} call must hit the same engine refusal as the vendor spelling "
                + $"'{@case.Sql}' (vendor: Msg {vendorError.Number}: {vendorError.Message})");
            return;
        }

        renderedFailure.Should().BeNull($"the rendered {name} call must execute: {renderedFailure?.Message}");
        Same(expected, actual).Should().BeTrue(
            $"the rendered {name} call must agree with the server: "
            + $"server='{Format(expected)}', rendered='{Format(actual)}'");
    }

    /// <summary>
    /// The clock and the pseudo-random generators are not repeatable: only the declared surface type and
    /// the documented range are checkable, while the call still executes on the server.
    /// </summary>
    [Fact]
    public void NonDeterministicScalars_ShouldReturnTheDeclaredSurface()
    {
        var now = P(Context, _ => SqlFunctions.SqlServer.sysdatetime());
        var nowOffset = P(Context, _ => SqlFunctions.SqlServer.sysdatetimeoffset());
        var utc = P(Context, _ => SqlFunctions.SqlServer.sysutcdatetime());

        now.Should().BeAfter(new DateTime(2020, 1, 1));
        utc.Should().BeAfter(new DateTime(2020, 1, 1));
        nowOffset.Should().NotBeNull();
        ((object)nowOffset!.Value).Should().BeOfType<DateTimeOffset>();

        P(Context, _ => SqlFunctions.SqlServer.rand()).Should().BeInRange(0d, 0.9999999999999999d);
        P(Context, _ => SqlFunctions.SqlServer.rand(42)).Should().Be(
            P(Context, _ => SqlFunctions.SqlServer.rand(42)), "the seeded form is reproducible");
        P(Context, _ => SqlFunctions.SqlServer.sysutcdatetime()).Should().BeAfter(new DateTime(2020, 1, 1));

        // The transaction-scoped identities differ between the vendor statement and the prepared query:
        // only their domain is checkable.
        P(Context, _ => SqlFunctions.SqlServer.current_transaction_id()).Should().BePositive();
        P(Context, _ => SqlFunctions.SqlServer.rowcount_big()).Should().BeGreaterThanOrEqualTo(0);
        P(Context, _ => SqlFunctions.SqlServer.xact_state()).Should().BeGreaterThanOrEqualTo(0,
            "the observed state is 0 without a transaction and 1 inside the prepared command");
    }

    /// <summary>
    /// The binary family keeps its byte shape: <c>compress</c> over the text and the binary overloads
    /// round-trips through <c>decompress</c>, and every blob stays non-null for a non-null input.
    /// </summary>
    [Fact]
    public void BinaryFamily_ShouldRoundTrip()
    {
        var compressedText = P(Context, _ => SqlFunctions.SqlServer.compress("abc"));
        var roundTrip = P(Context, _ => SqlFunctions.SqlServer.decompress(SqlFunctions.SqlServer.compress("abc")));

        compressedText.Should().NotBeNull();
        roundTrip.Should().Equal(System.Text.Encoding.Unicode.GetBytes("abc"),
            "compress over a text operand stores the UTF-16 bytes of the nvarchar value");
        P(Context, _ => SqlFunctions.SqlServer.decompress(null)).Should().BeNull(
            "an invalid input makes the server return NULL");
    }

    /// <summary>
    /// The sequence name is grammar, never a value: an empty, NUL-containing, over-long, over-component or
    /// non-constant name is refused before any SQL is emitted.
    /// </summary>
    [Theory]
    [InlineData("empty")]
    [InlineData("nul")]
    [InlineData("empty-component")]
    [InlineData("too-many-components")]
    [InlineData("over-long")]
    [InlineData("non-constant")]
    public void SequenceName_ShouldBeRefusedBeforeSql(string shape)
    {
        var name = "seq_name";
        object? Run() => shape switch
        {
            "empty" => P<long?>(Context, _ => SqlFunctions.SqlServer.next_value_for(string.Empty)),
            "nul" => P<long?>(Context, _ => SqlFunctions.SqlServer.next_value_for("a\0b")),
            "empty-component" => P<long?>(Context, _ => SqlFunctions.SqlServer.nextval("a..b")),
            "too-many-components" => P<long?>(Context, _ => SqlFunctions.SqlServer.nextval("a.b.c")),
            "over-long" => P<long?>(Context, _ => SqlFunctions.SqlServer.next_value_for(OverlongIdentifier)),
            _ => P<long?>(Context, _ => SqlFunctions.SqlServer.next_value_for(name))
        };

        FluentActions.Invoking(Run).Should().Throw<NotSupportedException>().WithMessage("*sequence name*");
    }

    /// <summary>
    /// The variadic and formatting guards of the translator: an empty <c>checksum</c> list and a
    /// <c>formatmessage</c> with more than twenty formatting arguments are refused before SQL.
    /// </summary>
    [Fact]
    public void VariadicGuards_ShouldBeRefusedBeforeSql()
    {
        FluentActions.Invoking(() => P<int?>(Context, _ => SqlFunctions.SqlServer.checksum()))
            .Should().Throw<NotSupportedException>().WithMessage("The checksum function requires at least one argument.");

        FluentActions.Invoking(() => P<string?>(Context, _ => SqlFunctions.SqlServer.formatmessage(
                "%s", 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21)))
            .Should().Throw<NotSupportedException>().WithMessage("*at most 20 formatting arguments*");
    }

    /// <summary>
    /// The <c>*FROMPARTS</c> precision is a compile-time integral constant of any width: every narrow
    /// operand type is decoded and normalised to a plain literal, and the value is still computed by the
    /// server.
    /// </summary>
    [Theory]
    [InlineData("byte")]
    [InlineData("sbyte")]
    [InlineData("short")]
    [InlineData("ushort")]
    [InlineData("int")]
    public void PrecisionOperandWidths_ShouldBeNormalised(string width)
    {
        var expected = new TimeSpan(10, 20, 30);
        var actual = width switch
        {
            "byte" => P(Context, _ => SqlFunctions.SqlServer.timefromparts(10, 20, 30, 0, (byte)3)),
            "sbyte" => P(Context, _ => SqlFunctions.SqlServer.timefromparts(10, 20, 30, 0, (sbyte)3)),
            "short" => P(Context, _ => SqlFunctions.SqlServer.timefromparts(10, 20, 30, 0, (short)3)),
            "ushort" => P(Context, _ => SqlFunctions.SqlServer.timefromparts(10, 20, 30, 0, (ushort)3)),
            _ => P(Context, _ => SqlFunctions.SqlServer.timefromparts(10, 20, 30, 0, 3))
        };

        actual.Should().Be(expected, "the normalised precision keeps the fractional part of the value");
    }

    /// <summary>
    /// The precision must be a constant in 0..7: an out-of-range constant and a captured (parameter)
    /// value are both refused before SQL.
    /// </summary>
    [Fact]
    public void PrecisionGuards_ShouldBeRefusedBeforeSql()
    {
        FluentActions.Invoking(() => P(Context, _ =>
                SqlFunctions.SqlServer.datetime2fromparts(2023, 1, 2, 3, 4, 5, 0, 9)))
            .Should().Throw<NotSupportedException>().WithMessage("*between 0 and 7*");

        var precision = 3;
        FluentActions.Invoking(() => P(Context, _ =>
                SqlFunctions.SqlServer.timefromparts(10, 20, 30, 0, precision)))
            .Should().Throw<NotSupportedException>().WithMessage("*constant integer between 0 and 7*");
    }

    /// <summary>
    /// The closed conversion map: every shipped target type and the optional style overload convert the
    /// value the server converts, including an omitted and a NULL style.
    /// </summary>
    [Fact]
    public void TryConversionTargets_ShouldCompute()
    {
        P(Context, _ => SqlFunctions.SqlServer.try_cast<long>("42")).Should().Be(42L);
        P(Context, _ => SqlFunctions.SqlServer.try_cast<decimal>("42.5")).Should().Be(42.5m);
        P(Context, _ => SqlFunctions.SqlServer.try_cast<DateTime>("2023-01-01")).Should()
            .Be(new DateTime(2023, 1, 1));
        P(Context, _ => SqlFunctions.SqlServer.try_cast("not-a-number")).Should()
            .Be((string?)Vendor(Context, "select try_cast('not-a-number' as nvarchar(4000))"));
        P(Context, _ => SqlFunctions.SqlServer.try_convert<double>("1.5")).Should().Be(1.5d);
        P(Context, _ => SqlFunctions.SqlServer.try_convert<int>("1", 1)).Should().Be(1);
        P(Context, _ => SqlFunctions.SqlServer.try_convert<int>("1", null)).Should().BeNull(
            "a supplied NULL style is a typed NULL and yields SQL NULL, not the default style");
    }

    /// <summary>
    /// A query whose predicate carries a SQL parameter extracts that parameter in a separate pass; every
    /// family of the translator must survive that pass and still compute the projected value.
    /// </summary>
    [Fact]
    public void ParameterisedQuery_ShouldComputeEveryFamily()
    {
        var id = 1L;
        var r = _sut.ComplexEntity
            .Where(x => x.Id == id)
            .Select(x => new
            {
                Position = SqlFunctions.SqlServer.patindex("%b%", "abc"),
                Converted = SqlFunctions.SqlServer.try_convert<int>("42"),
                Checksum = SqlFunctions.SqlServer.checksum(1, 2, 3),
                Message = SqlFunctions.SqlServer.formatmessage("%s %s", "a", "b")
            })
            .First();

        r.Position.Should().Be(2);
        r.Converted.Should().Be(42);
        r.Checksum.Should().NotBe(0);
        r.Message.Should().Be("a b");
    }

    /// <summary>Evaluates one scalar projection over the seeded probe row.</summary>
    private static T? P<T>(IDataContext context, System.Linq.Expressions.Expression<Func<IComplexEntity, T?>> select) =>
        context.From<IComplexEntity>().Where(x => x.Id == 1).Select(select).ToList()[0];

    /// <summary>
    /// Runs one database action, retrying once on Msg 208. The reused container is restarted between runs
    /// and the seed batch runs concurrently with the first test: a table that is missing for a moment is
    /// re-read instead of being reported as absent.
    /// </summary>
    private static object? Execute(Func<object?> action)
    {
        try
        {
            return action();
        }
        catch (SqlException error) when (error.Number == 208)
        {
            Thread.Sleep(750);
            return action();
        }
    }

    /// <summary>Executes the vendor spelling directly on the same connection.</summary>
    private static object? Vendor(IDataContext context, string sql)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

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
        is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal or bool;

    private static string Format(object? value) => value switch
    {
        null => "<null>",
        byte[] bytes => "0x" + Convert.ToHexString(bytes),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "<null>"
    };

    /// <summary>
    /// The unversioned surface, one row per name/arity. <see cref="ScalarCase.Sql"/> is the vendor spelling
    /// executed on the same connection; <see cref="ScalarCase.Expected"/> pins a value the vendor spelling
    /// cannot express (the T-SQL emulation of a cross-provider function).
    /// </summary>
    private static readonly ScalarCase[] ScalarCases =
    [
        // ---- SqlServerScalarFunctions: cross-provider names and their arities ----
        new("sql.left", c => P(c, _ => SqlFunctions.Sql.left("abcde", 2)), "ab", ExpectedType: typeof(string)),
        new("sql.right", c => P(c, _ => SqlFunctions.Sql.right("abcde", 2)), "de", ExpectedType: typeof(string)),
        new("sql.lpad2", c => P(c, _ => SqlFunctions.Sql.lpad("abc", 5)), "  abc", ExpectedType: typeof(string)),
        new("sql.lpad3", c => P(c, _ => SqlFunctions.Sql.lpad("abc", 5, "x")), "xxabc", ExpectedType: typeof(string)),
        new("sql.rpad2", c => P(c, _ => SqlFunctions.Sql.rpad("abc", 5)), "abc  ", ExpectedType: typeof(string)),
        new("sql.rpad3", c => P(c, _ => SqlFunctions.Sql.rpad("abc", 5, "x")), "abcxx", ExpectedType: typeof(string)),
        new("sql.rpad-truncating", c => P(c, _ => SqlFunctions.Sql.rpad("abcdef", 3, "x")), "abc",
            ExpectedType: typeof(string)),
        new("sql.repeat", c => P(c, _ => SqlFunctions.Sql.repeat("ab", 3)), "ababab", ExpectedType: typeof(string)),
        new("sql.reverse", c => P(c, _ => SqlFunctions.Sql.reverse("abc")), "cba", ExpectedType: typeof(string)),
        new("sql.space", c => P(c, _ => SqlFunctions.Sql.space(2)), "  ", ExpectedType: typeof(string)),
        new("sql.concat-ws-many", c => P(c, _ => SqlFunctions.Sql.concat_ws("-", "a", "b", "c")), "a-b-c",
            ExpectedType: typeof(string)),
        new("sql.translate", c => P(c, _ => SqlFunctions.Sql.translate("abc", "abc", "xyz")), "xyz",
            ExpectedType: typeof(string)),
        new("sql.ascii", c => P(c, _ => SqlFunctions.Sql.ascii("A")), 65, ExpectedType: typeof(int)),
        new("sql.char", c => P(c, _ => SqlFunctions.Sql.@char(65)), "A", ExpectedType: typeof(string)),
        new("sql.bit-length", c => P(c, _ => SqlFunctions.Sql.bit_length("abc")), 48, ExpectedType: typeof(int)),
        new("sql.octet-length", c => P(c, _ => SqlFunctions.Sql.octet_length("abc")), 6, ExpectedType: typeof(int)),
        new("sql.cot", c => P(c, _ => SqlFunctions.Sql.cot(2.0)), Sql: "select cot(2.0)"),
        new("sql.degrees", c => P(c, _ => SqlFunctions.Sql.degrees(1.0)), Sql: "select degrees(cast(1.0 as float))"),
        new("sql.radians", c => P(c, _ => SqlFunctions.Sql.radians(180.0)),
            Sql: "select radians(cast(180.0 as float))"),
        new("sql.pi", c => P(c, _ => SqlFunctions.Sql.pi()), Sql: "select pi()"),

        // ---- SqlServerSpecificFunctions: string and trigonometry ----
        new("patindex", c => P(c, _ => SqlFunctions.SqlServer.patindex("%b%", "abc")),
            Sql: "select patindex('%b%','abc')", ExpectedType: typeof(int)),
        new("quotename-1", c => P(c, _ => SqlFunctions.SqlServer.quotename("abc")),
            Sql: "select quotename('abc')", ExpectedType: typeof(string)),
        new("quotename-2", c => P(c, _ => SqlFunctions.SqlServer.quotename("abc", "'")),
            Sql: "select quotename('abc','''')", ExpectedType: typeof(string)),
        new("quotename-bracket", c => P(c, _ => SqlFunctions.SqlServer.quotename("a]b", "[")),
            Sql: "select quotename('a]b','[')", ExpectedType: typeof(string)),
        new("quotename-overlong", c => P(c, _ => SqlFunctions.SqlServer.quotename(OverlongIdentifier)),
            Sql: "select quotename(replicate('a',129))"),
        new("soundex", c => P(c, _ => SqlFunctions.SqlServer.soundex("Robert")),
            Sql: "select soundex('Robert')", ExpectedType: typeof(string)),
        new("difference", c => P(c, _ => SqlFunctions.SqlServer.difference("Robert", "Rupert")),
            Sql: "select difference('Robert','Rupert')", ExpectedType: typeof(int)),
        new("string-escape", c => P(c, _ => SqlFunctions.SqlServer.string_escape("a\"b", "json")),
            Sql: "select string_escape('a\"b','json')", ExpectedType: typeof(string)),
        new("unicode", c => P(c, _ => SqlFunctions.SqlServer.unicode("A")),
            Sql: "select unicode('A')", ExpectedType: typeof(int)),
        new("nchar", c => P(c, _ => SqlFunctions.SqlServer.nchar(65)),
            Sql: "select nchar(65)", ExpectedType: typeof(string)),
        new("format-2", c => P(c, _ => SqlFunctions.SqlServer.format(42, "D6")),
            Sql: "select format(42,'D6')", ExpectedType: typeof(string)),
        new("format-3", c => P(c, _ => SqlFunctions.SqlServer.format(42, "D6", "en-US")),
            Sql: "select format(42,'D6','en-US')", ExpectedType: typeof(string)),
        new("acos", c => P(c, _ => SqlFunctions.SqlServer.acos(0.5)), Sql: "select acos(cast(0.5 as float))"),
        new("asin", c => P(c, _ => SqlFunctions.SqlServer.asin(0.5)), Sql: "select asin(cast(0.5 as float))"),
        new("atan", c => P(c, _ => SqlFunctions.SqlServer.atan(0.5)), Sql: "select atan(cast(0.5 as float))"),
        new("atn2", c => P(c, _ => SqlFunctions.SqlServer.atn2(1.0, 2.0)),
            Sql: "select atn2(cast(1.0 as float),cast(2.0 as float))"),
        new("square", c => P(c, _ => SqlFunctions.SqlServer.square(4.0)), Sql: "select square(cast(4.0 as float))"),

        // ---- date/time ----
        new("datename", c => P(c, x => SqlFunctions.SqlServer.datename("year", x.Datetime!.Value)),
            Sql: "select datename(year, cast('2023-01-01T10:00:00' as datetime2))", ExpectedType: typeof(string)),
        new("date-bucket-3", c => P(c, x => SqlFunctions.SqlServer.date_bucket("day", 1, x.Datetime!.Value)),
            Sql: "select date_bucket(day, 1, cast('2023-01-01T10:00:00' as datetime2))",
            ExpectedType: typeof(DateTime)),
        new("date-bucket-4",
            c => P(c, x => SqlFunctions.SqlServer.date_bucket(
                "day", 1, x.Datetime!.Value, x.Datetime!.Value)),
            Sql: "select date_bucket(day, 1, cast('2023-01-01T10:00:00' as datetime2), "
                + "cast('2023-01-01T10:00:00' as datetime2))"),
        new("switchoffset-string",
            c => P(c, _ => SqlFunctions.SqlServer.switchoffset(
                SqlFunctions.SqlServer.todatetimeoffset(new DateTime(2023, 5, 17, 12, 0, 0), 120), "-08:00")),
            Sql: "select switchoffset(todatetimeoffset(cast('2023-05-17T12:00:00' as datetime),120),'-08:00')"),
        new("switchoffset-minutes",
            c => P(c, _ => SqlFunctions.SqlServer.switchoffset(
                SqlFunctions.SqlServer.todatetimeoffset(new DateTime(2023, 5, 17, 12, 0, 0), 120), 60)),
            Sql: "select switchoffset(todatetimeoffset(cast('2023-05-17T12:00:00' as datetime),120),60)"),
        new("todatetimeoffset-string",
            c => P(c, _ => SqlFunctions.SqlServer.todatetimeoffset(new DateTime(2023, 5, 17, 12, 0, 0), "+02:00")),
            Sql: "select todatetimeoffset(cast('2023-05-17T12:00:00' as datetime),'+02:00')"),
        new("todatetimeoffset-minutes",
            c => P(c, _ => SqlFunctions.SqlServer.todatetimeoffset(new DateTime(2023, 5, 17, 12, 0, 0), 120)),
            Sql: "select todatetimeoffset(cast('2023-05-17T12:00:00' as datetime),120)"),
        new("timefromparts", c => P(c, _ => SqlFunctions.SqlServer.timefromparts(10, 20, 30, 0, 3)),
            Sql: "select timefromparts(10,20,30,0,3)", ExpectedType: typeof(TimeSpan)),
        new("smalldatetimefromparts",
            c => P(c, _ => SqlFunctions.SqlServer.smalldatetimefromparts(2023, 1, 2, 3, 4)),
            Sql: "select smalldatetimefromparts(2023,1,2,3,4)", ExpectedType: typeof(DateTime)),
        new("datetimefromparts",
            c => P(c, _ => SqlFunctions.SqlServer.datetimefromparts(2023, 1, 2, 3, 4, 5, 0)),
            Sql: "select datetimefromparts(2023,1,2,3,4,5,0)", ExpectedType: typeof(DateTime)),
        new("datetime2fromparts",
            c => P(c, _ => SqlFunctions.SqlServer.datetime2fromparts(2023, 1, 2, 3, 4, 5, 0, 3)),
            Sql: "select datetime2fromparts(2023,1,2,3,4,5,0,3)", ExpectedType: typeof(DateTime)),
        new("datetimeoffsetfromparts",
            c => P(c, _ => SqlFunctions.SqlServer.datetimeoffsetfromparts(2023, 1, 2, 3, 4, 5, 0, 2, 0, 3)),
            Sql: "select datetimeoffsetfromparts(2023,1,2,3,4,5,0,2,0,3)"),

        // ---- binary / checksum / hash ----
        new("hashbytes", c => P(c, _ => SqlFunctions.SqlServer.hashbytes("MD5", System.Text.Encoding.UTF8.GetBytes("abc"))),
            Sql: "select hashbytes('MD5', cast('abc' as varbinary(max)))", ExpectedType: typeof(byte[])),
        new("checksum-1", c => P(c, _ => SqlFunctions.SqlServer.checksum(1)),
            Sql: "select checksum(1)", ExpectedType: typeof(int)),
        new("checksum-3", c => P(c, _ => SqlFunctions.SqlServer.checksum(1, 2, 3)),
            Sql: "select checksum(1,2,3)", ExpectedType: typeof(int)),
        new("binary-checksum", c => P(c, _ => SqlFunctions.SqlServer.binary_checksum(1, 2, 3)),
            Sql: "select binary_checksum(1,2,3)", ExpectedType: typeof(int)),
        new("min-active-rowversion", c => P(c, _ => SqlFunctions.SqlServer.min_active_rowversion()),
            Sql: "select min_active_rowversion()", ExpectedType: typeof(byte[])),

        // ---- other scalars ----
        new("stuff", c => P(c, _ => SqlFunctions.SqlServer.stuff("abcdef", 2, 2, "XX")),
            Sql: "select stuff('abcdef',2,2,'XX')", ExpectedType: typeof(string)),
        new("str-1", c => P(c, _ => SqlFunctions.SqlServer.str(42.5)),
            Sql: "select str(cast(42.5 as float))", ExpectedType: typeof(string)),
        new("str-2", c => P(c, _ => SqlFunctions.SqlServer.str(42.5, 10)),
            Sql: "select str(cast(42.5 as float),10)", ExpectedType: typeof(string)),
        new("str-3", c => P(c, _ => SqlFunctions.SqlServer.str(42.5, 10, 2)),
            Sql: "select str(cast(42.5 as float),10,2)", ExpectedType: typeof(string)),
        new("isdate-valid", c => P(c, _ => SqlFunctions.SqlServer.isdate("2023-01-01")),
            Sql: "select isdate('2023-01-01')", ExpectedType: typeof(int)),
        new("isdate-invalid", c => P(c, _ => SqlFunctions.SqlServer.isdate("x")),
            Sql: "select isdate('x')", ExpectedType: typeof(int)),
        new("isnumeric", c => P(c, _ => SqlFunctions.SqlServer.isnumeric("42")),
            Sql: "select isnumeric('42')", ExpectedType: typeof(int)),
        new("parsename", c => P(c, _ => SqlFunctions.SqlServer.parsename("a.b.c", 1)),
            Sql: "select parsename('a.b.c',1)", ExpectedType: typeof(string)),
        new("parse", c => P(c, _ => SqlFunctions.SqlServer.parse("42.5")),
            Sql: "select parse('42.5' as decimal(38,18) using N'en-US')", ExpectedType: typeof(decimal)),
        new("try-parse", c => P(c, _ => SqlFunctions.SqlServer.try_parse("x")),
            Sql: "select try_parse('x' as decimal(38,18) using N'en-US')"),
        new("publishingservername", c => P(c, _ => SqlFunctions.SqlServer.publishingservername()),
            Sql: "select publishingservername()"),
        new("scope-identity", c => P(c, _ => SqlFunctions.SqlServer.scope_identity()),
            Sql: "select scope_identity()"),

        // ---- metadata A: object/column/index ----
        new("col-length", c => P(c, _ => SqlFunctions.SqlServer.col_length("complex_entity", "id")),
            Sql: "select col_length('complex_entity','id')", ExpectedType: typeof(int)),
        new("col-name", c => P(c, _ => SqlFunctions.SqlServer.col_name(SqlFunctions.SqlServer.object_id("complex_entity"), 1)),
            Sql: "select col_name(object_id('complex_entity'),1)", ExpectedType: typeof(string)),
        new("ident-incr", c => P(c, _ => SqlFunctions.SqlServer.ident_incr("complex_entity")),
            Sql: "select ident_incr('complex_entity')", ExpectedType: typeof(decimal)),
        new("ident-seed", c => P(c, _ => SqlFunctions.SqlServer.ident_seed("complex_entity")),
            Sql: "select ident_seed('complex_entity')", ExpectedType: typeof(decimal)),
        new("index-col", c => P(c, _ => SqlFunctions.SqlServer.index_col("complex_entity", 1, 1)),
            Sql: "select index_col('complex_entity',1,1)", ExpectedType: typeof(string)),
        new("object-definition",
            c => P(c, _ => SqlFunctions.SqlServer.object_definition(SqlFunctions.SqlServer.object_id("complex_entity"))),
            Sql: "select object_definition(object_id('complex_entity'))", ExpectedType: typeof(string)),
        new("object-id-1", c => P(c, _ => SqlFunctions.SqlServer.object_id("complex_entity")),
            Sql: "select object_id('complex_entity')", ExpectedType: typeof(int)),
        new("object-id-2", c => P(c, _ => SqlFunctions.SqlServer.object_id("complex_entity", "U")),
            Sql: "select object_id('complex_entity','U')", ExpectedType: typeof(int)),
        new("object-name-1", c => P(c, _ => SqlFunctions.SqlServer.object_name(SqlFunctions.SqlServer.object_id("complex_entity"))),
            Sql: "select object_name(object_id('complex_entity'))", ExpectedType: typeof(string)),
        new("object-name-2",
            c => P(c, _ => SqlFunctions.SqlServer.object_name(SqlFunctions.SqlServer.object_id("complex_entity"), 1)),
            Sql: "select object_name(object_id('complex_entity'),1)", ExpectedType: typeof(string)),
        new("object-schema-name-1",
            c => P(c, _ => SqlFunctions.SqlServer.object_schema_name(SqlFunctions.SqlServer.object_id("complex_entity"))),
            Sql: "select object_schema_name(object_id('complex_entity'))", ExpectedType: typeof(string)),
        new("object-schema-name-2",
            c => P(c, _ => SqlFunctions.SqlServer.object_schema_name(SqlFunctions.SqlServer.object_id("complex_entity"), 1)),
            Sql: "select object_schema_name(object_id('complex_entity'),1)", ExpectedType: typeof(string)),

        // ---- metadata B/C/D: database, schema, type, file, server ----
        new("db-id-0", c => P(c, _ => SqlFunctions.SqlServer.db_id()),
            Sql: "select db_id()", ExpectedType: typeof(int)),
        new("db-id-1", c => P(c, _ => SqlFunctions.SqlServer.db_id("tempdb")),
            Sql: "select db_id('tempdb')", ExpectedType: typeof(int)),
        new("db-name-0", c => P(c, _ => SqlFunctions.SqlServer.db_name()),
            Sql: "select db_name()", ExpectedType: typeof(string)),
        new("db-name-1", c => P(c, _ => SqlFunctions.SqlServer.db_name(2)),
            Sql: "select db_name(2)", ExpectedType: typeof(string)),
        new("original-db-name", c => P(c, _ => SqlFunctions.SqlServer.original_db_name()),
            Sql: "select original_db_name()", ExpectedType: typeof(string)),
        new("schema-id-0", c => P(c, _ => SqlFunctions.SqlServer.schema_id()),
            Sql: "select schema_id()", ExpectedType: typeof(int)),
        new("schema-id-1", c => P(c, _ => SqlFunctions.SqlServer.schema_id("dbo")),
            Sql: "select schema_id('dbo')", ExpectedType: typeof(int)),
        new("schema-name-0", c => P(c, _ => SqlFunctions.SqlServer.schema_name()),
            Sql: "select schema_name()", ExpectedType: typeof(string)),
        new("schema-name-1", c => P(c, _ => SqlFunctions.SqlServer.schema_name(1)),
            Sql: "select schema_name(1)", ExpectedType: typeof(string)),
        new("type-id", c => P(c, _ => SqlFunctions.SqlServer.type_id("int")),
            Sql: "select type_id('int')", ExpectedType: typeof(int)),
        new("type-name", c => P(c, _ => SqlFunctions.SqlServer.type_name(56)),
            Sql: "select type_name(56)", ExpectedType: typeof(string)),
        new("filegroup-id", c => P(c, _ => SqlFunctions.SqlServer.filegroup_id("PRIMARY")),
            Sql: "select filegroup_id('PRIMARY')", ExpectedType: typeof(int)),
        new("filegroup-name", c => P(c, _ => SqlFunctions.SqlServer.filegroup_name(1)),
            Sql: "select filegroup_name(1)", ExpectedType: typeof(string)),
        new("file-id", c => P(c, _ => SqlFunctions.SqlServer.file_id(SqlFunctions.SqlServer.file_name(1))),
            Sql: "select file_id(file_name(1))", ExpectedType: typeof(int)),
        new("file-idex", c => P(c, _ => SqlFunctions.SqlServer.file_idex(SqlFunctions.SqlServer.file_name(1))),
            Sql: "select file_idex(file_name(1))", ExpectedType: typeof(int)),
        new("file-name", c => P(c, _ => SqlFunctions.SqlServer.file_name(1)),
            Sql: "select file_name(1)", ExpectedType: typeof(string)),
        new("current-request-id", c => P(c, _ => SqlFunctions.SqlServer.current_request_id()),
            Sql: "select current_request_id()"),
        new("current-timezone", c => P(c, _ => SqlFunctions.SqlServer.current_timezone()),
            Sql: "select current_timezone()", ExpectedType: typeof(string)),
        new("formatmessage", c => P(c, _ => SqlFunctions.SqlServer.formatmessage("%s %s", "a", "b")),
            Sql: "select formatmessage('%s %s','a','b')", ExpectedType: typeof(string)),
        new("getansinull-0", c => P(c, _ => SqlFunctions.SqlServer.getansinull()),
            Sql: "select getansinull()", ExpectedType: typeof(int)),
        new("getansinull-1", c => P(c, _ => SqlFunctions.SqlServer.getansinull("tempdb")),
            Sql: "select getansinull('tempdb')", ExpectedType: typeof(int)),
        new("ident-current", c => P(c, _ => SqlFunctions.SqlServer.ident_current("complex_entity")),
            Sql: "select ident_current('complex_entity')", ExpectedType: typeof(decimal)),

        // ---- SQL/JSON constructors and predicates ----
        new("json-array-0", c => P(c, _ => SqlFunctions.SqlServer.json_array()),
            Sql: "select json_array()", ExpectedType: typeof(string)),
        new("json-array-1", c => P(c, _ => SqlFunctions.SqlServer.json_array(1)),
            Sql: "select json_array(1)", ExpectedType: typeof(string)),
        new("json-array-many", c => P(c, _ => SqlFunctions.SqlServer.json_array("a", 1, "b")),
            Sql: "select json_array('a',1,'b')", ExpectedType: typeof(string)),
        new("json-object-1", c => P(c, _ => SqlFunctions.SqlServer.json_object("k", 1)),
            Sql: "select json_object('k': 1)", ExpectedType: typeof(string)),
        new("json-object-2", c => P(c, _ => SqlFunctions.SqlServer.json_object("a", 1, "b", 2)),
            Sql: "select json_object('a': 1, 'b': 2)", ExpectedType: typeof(string)),
        new("json-path-exists", c => P(c, _ => SqlFunctions.SqlServer.json_path_exists("{\"k\":1}", "$.k")),
            Sql: "select json_path_exists('{\"k\":1}','$.k')", ExpectedType: typeof(bool))
    ];

    /// <summary>
    /// The version-bound (major 17) surface: the 2025 regex scalars and the 2022 bit family with every
    /// shipped operand type and the typed-NULL guard.
    /// </summary>
    private static readonly ScalarCase[] VersionedCases =
    [
        new("regexp-count", c => P(c, _ => SqlFunctions.SqlServer.regexp_count("abcabc", "a")),
            Sql: "select regexp_count('abcabc','a')", ExpectedType: typeof(int)),
        new("regexp-count-no-match", c => P(c, _ => SqlFunctions.SqlServer.regexp_count("abc", "[0-9]")),
            Sql: "select regexp_count('abc','[0-9]')", ExpectedType: typeof(int)),
        new("regexp-count-null", c => P(c, _ => SqlFunctions.SqlServer.regexp_count(null, "a")),
            Sql: "select regexp_count(cast(null as varchar(10)),'a')"),
        new("regexp-substr-2", c => P(c, _ => SqlFunctions.SqlServer.regexp_substr("abcabc", "b")),
            Sql: "select regexp_substr('abcabc','b')", ExpectedType: typeof(string)),
        new("regexp-substr-3", c => P(c, _ => SqlFunctions.SqlServer.regexp_substr("abcabc", "b", 3)),
            Sql: "select regexp_substr('abcabc','b',3)", ExpectedType: typeof(string)),
        new("regexp-substr-4", c => P(c, _ => SqlFunctions.SqlServer.regexp_substr("abcabc", "b", 1, 2)),
            Sql: "select regexp_substr('abcabc','b',1,2)", ExpectedType: typeof(string)),
        new("regexp-substr-5",
            c => P(c, _ => SqlFunctions.SqlServer.regexp_substr("aAbB", "a", 1, 1, "i")),
            Sql: "select regexp_substr('aAbB','a',1,1,'i')", ExpectedType: typeof(string)),
        new("regexp-instr-2", c => P(c, _ => SqlFunctions.SqlServer.regexp_instr("abcabc", "b")),
            Sql: "select regexp_instr('abcabc','b')", ExpectedType: typeof(int)),
        new("regexp-instr-3", c => P(c, _ => SqlFunctions.SqlServer.regexp_instr("abcabc", "b", 3)),
            Sql: "select regexp_instr('abcabc','b',3)", ExpectedType: typeof(int)),
        new("regexp-instr-4", c => P(c, _ => SqlFunctions.SqlServer.regexp_instr("abcabc", "b", 1, 2)),
            Sql: "select regexp_instr('abcabc','b',1,2)", ExpectedType: typeof(int)),
        new("regexp-instr-5",
            c => P(c, _ => SqlFunctions.SqlServer.regexp_instr("abcabc", "b", 1, 1, 1)),
            Sql: "select regexp_instr('abcabc','b',1,1,1)", ExpectedType: typeof(int)),
        new("regexp-instr-6",
            c => P(c, _ => SqlFunctions.SqlServer.regexp_instr("aAbB", "a", 1, 1, 0, "i")),
            Sql: "select regexp_instr('aAbB','a',1,1,0,'i')", ExpectedType: typeof(int)),
        new("bit-left-shift-byte", c => P(c, _ => SqlFunctions.SqlServer.left_shift((byte)1, 1)),
            Sql: "select left_shift(cast(1 as tinyint), cast(1 as bigint))", ExpectedType: typeof(byte)),
        new("bit-left-shift-short", c => P(c, _ => SqlFunctions.SqlServer.left_shift((short)8, 1)),
            Sql: "select left_shift(cast(8 as smallint), cast(1 as bigint))", ExpectedType: typeof(short)),
        new("bit-left-shift-int", c => P(c, x => SqlFunctions.SqlServer.left_shift((int?)x.SmallInt!.Value, 1)),
            Sql: "select left_shift(cast(3 as int), cast(1 as bigint))", ExpectedType: typeof(int)),
        new("bit-left-shift-long", c => P(c, _ => SqlFunctions.SqlServer.left_shift((long)8, 1)),
            Sql: "select left_shift(cast(8 as bigint), cast(1 as bigint))", ExpectedType: typeof(long)),
        new("bit-left-shift-null", c => P(c, _ => SqlFunctions.SqlServer.left_shift((int?)null, 1)),
            Sql: "select cast(null as int)"),
        new("bit-right-shift-short", c => P(c, _ => SqlFunctions.SqlServer.right_shift((short)8, 1)),
            Sql: "select right_shift(cast(8 as smallint), cast(1 as bigint))", ExpectedType: typeof(short)),
        new("bit-right-shift-long", c => P(c, _ => SqlFunctions.SqlServer.right_shift((long)8, 1)),
            Sql: "select right_shift(cast(8 as bigint), cast(1 as bigint))", ExpectedType: typeof(long)),
        new("bit-count-byte", c => P(c, _ => SqlFunctions.SqlServer.bit_count((byte)7)),
            Sql: "select bit_count(cast(7 as tinyint))", ExpectedType: typeof(long)),
        new("bit-count-short", c => P(c, _ => SqlFunctions.SqlServer.bit_count((short)7)),
            Sql: "select bit_count(cast(7 as smallint))", ExpectedType: typeof(long)),
        new("bit-count-long", c => P(c, _ => SqlFunctions.SqlServer.bit_count((long)7)),
            Sql: "select bit_count(cast(7 as bigint))", ExpectedType: typeof(long)),
        new("bit-count-null", c => P(c, _ => SqlFunctions.SqlServer.bit_count((long?)null)),
            Sql: "select cast(null as bigint)"),
        new("get-bit-byte", c => P(c, _ => SqlFunctions.SqlServer.get_bit((byte)5, 0)),
            Sql: "select get_bit(cast(5 as tinyint), cast(0 as int))", ExpectedType: typeof(bool)),
        new("get-bit-short", c => P(c, _ => SqlFunctions.SqlServer.get_bit((short)5, 2)),
            Sql: "select get_bit(cast(5 as smallint), cast(2 as int))", ExpectedType: typeof(bool)),
        new("get-bit-long", c => P(c, _ => SqlFunctions.SqlServer.get_bit((long)5, 1)),
            Sql: "select get_bit(cast(5 as bigint), cast(1 as int))", ExpectedType: typeof(bool)),
        new("get-bit-null", c => P(c, _ => SqlFunctions.SqlServer.get_bit((int?)null, 1)),
            Sql: "select cast(null as bit)"),
        new("set-bit-byte", c => P(c, _ => SqlFunctions.SqlServer.set_bit((byte)5, 1)),
            Sql: "select set_bit(cast(5 as tinyint), cast(1 as int))", ExpectedType: typeof(byte)),
        new("set-bit-short", c => P(c, _ => SqlFunctions.SqlServer.set_bit((short)5, 1, false)),
            Sql: "select set_bit(cast(5 as smallint), cast(1 as int), cast(0 as bit))",
            ExpectedType: typeof(short)),
        new("set-bit-int", c => P(c, x => SqlFunctions.SqlServer.set_bit((int?)x.SmallInt!.Value, 1, true)),
            Sql: "select set_bit(cast(3 as int), cast(1 as int), cast(1 as bit))", ExpectedType: typeof(int)),
        new("set-bit-long", c => P(c, _ => SqlFunctions.SqlServer.set_bit((long)5, 1)),
            Sql: "select set_bit(cast(5 as bigint), cast(1 as int))", ExpectedType: typeof(long)),
        new("set-bit-null-value", c => P(c, _ => SqlFunctions.SqlServer.set_bit(5, 1, null)),
            Sql: "select cast(null as int)")
    ];

    /// <summary>
    /// The version-bound fuzzy family: the engine decides whether the call executes (compatibility level
    /// 170, <c>PREVIEW_FEATURES</c> and a Windows collation are environment prerequisites); the rendered
    /// call must agree with the vendor spelling — value or the same refusal.
    /// </summary>
    private static readonly ScalarCase[] VersionedEnvironmentCases =
    [
        new("edit-distance", c => P(c, _ => SqlFunctions.SqlServer.edit_distance("kitten", "sitting")),
            Sql: "select edit_distance('kitten','sitting')"),
        new("edit-distance-bounded", c => P(c, _ => SqlFunctions.SqlServer.edit_distance("kitten", "sitting", 1)),
            Sql: "select edit_distance('kitten','sitting',1)"),
        new("edit-distance-similarity",
            c => P(c, _ => SqlFunctions.SqlServer.edit_distance_similarity("kitten", "sitting")),
            Sql: "select edit_distance_similarity('kitten','sitting')"),
        new("jaro-winkler-distance",
            c => P(c, _ => SqlFunctions.SqlServer.jaro_winkler_distance("martha", "marhta")),
            Sql: "select jaro_winkler_distance('martha','marhta')"),
        new("jaro-winkler-similarity",
            c => P(c, _ => SqlFunctions.SqlServer.jaro_winkler_similarity("martha", "marhta")),
            Sql: "select jaro_winkler_similarity('martha','marhta')")
    ];

    /// <summary>
    /// Rows whose native outcome is decided by the engine (prerequisites, an object a bare select cannot
    /// produce); the rendered call must agree with the vendor spelling — value or refusal.
    /// </summary>
    private static readonly ScalarCase[] EnvironmentCases =
    [
        new("newsequentialid", c => P(c, _ => SqlFunctions.SqlServer.newsequentialid()),
            Sql: "select newsequentialid()"),
        new("json-contains", c => P(c, _ => SqlFunctions.SqlServer.json_contains("{\"a\":1}", "1", "$.a")),
            Sql: "select json_contains('{\"a\":1}','1','$.a')")
    ];

    /// <summary>
    /// One name/arity of the surface: the production projection and either the vendor spelling
    /// (<see cref="Sql"/>) executed on the same connection or an explicit <see cref="Expected"/> value.
    /// </summary>
    /// <param name="Id">The row id, i.e. the surface name and arity.</param>
    /// <param name="Run">The same call through the production renderer.</param>
    /// <param name="Expected">A pinned expected value, used when T-SQL has no direct vendor spelling.</param>
    /// <param name="Sql">The vendor spelling executed directly on the same connection.</param>
    /// <param name="ExpectedType">The declared surface type, or <see langword="null"/> when not pinned.</param>
    private sealed record ScalarCase(
        string Id,
        Func<IDataContext, object?> Run,
        object? Expected = null,
        string? Sql = null,
        Type? ExpectedType = null);
}
