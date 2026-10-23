using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQLite specific surface, second sweep: the date-unit switches (add/diff parts), the extract
/// parts, the number/date format renderers, the JSON1 library and the SQLite-only scalar helpers.
/// Every call runs against a real file-backed database.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteExtendedFunctionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private static readonly DateTime Value = new(2023, 5, 17, 13, 45, 30);

    [Fact]
    public void DateAddUnits_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Year = SqlFunctions.Sql.date_add("year", 1, Value),
                Quarter = SqlFunctions.Sql.date_add("quarter", 1, Value),
                Month = SqlFunctions.Sql.date_add("month", 1, Value),
                Week = SqlFunctions.Sql.date_add("week", 1, Value),
                Day = SqlFunctions.Sql.date_add("day", 1, Value),
                Hour = SqlFunctions.Sql.date_add("hour", 1, Value),
                Minute = SqlFunctions.Sql.date_add("minute", 1, Value),
                Second = SqlFunctions.Sql.date_add("second", 30, Value),
                Milli = SqlFunctions.Sql.date_add("milliseconds", 500, Value)
            })
            .First();

        r.Year.Should().Be(Value.AddYears(1));
        r.Quarter.Should().Be(Value.AddMonths(3));
        r.Month.Should().Be(Value.AddMonths(1));
        r.Week.Should().Be(Value.AddDays(7));
        r.Day.Should().Be(Value.AddDays(1));
        r.Hour.Should().Be(Value.AddHours(1));
        r.Minute.Should().Be(Value.AddMinutes(1));
        r.Second.Should().Be(Value.AddSeconds(30));
        r.Milli.Should().Be(Value.AddMilliseconds(500));
    }

    [Fact]
    public void DateDiffUnits_ShouldExecute()
    {
        var later = Value.AddDays(40);

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Year = SqlFunctions.Sql.date_diff("year", Value, later),
                Quarter = SqlFunctions.Sql.date_diff("quarter", Value, later),
                Month = SqlFunctions.Sql.date_diff("month", Value, later),
                Week = SqlFunctions.Sql.date_diff("week", Value, later),
                Day = SqlFunctions.Sql.date_diff("day", Value, later),
                Hour = SqlFunctions.Sql.date_diff("hour", Value, later),
                Minute = SqlFunctions.Sql.date_diff("minute", Value, later),
                Second = SqlFunctions.Sql.date_diff("second", Value, later),
                Big = SqlFunctions.Sql.date_diff_big("second", Value, later)
            })
            .First();

        r.Year.Should().Be(0);
        r.Quarter.Should().Be(0);
        r.Month.Should().Be(1);
        r.Week.Should().Be(5);
        r.Day.Should().Be(40);
        r.Hour.Should().Be(960);
        r.Minute.Should().Be(57_600);
        r.Second.Should().Be(3_456_000);
        r.Big.Should().Be(3_456_000);
    }

    [Fact]
    public void DateParts_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Quarter = SqlFunctions.Sql.extract("quarter", Value),
                Dow = SqlFunctions.Sql.extract("dow", Value),
                IsoDow = SqlFunctions.Sql.extract("isodow", Value),
                Doy = SqlFunctions.Sql.extract("doy", Value),
                Week = SqlFunctions.Sql.extract("week", Value),
                Epoch = SqlFunctions.Sql.date_part("epoch", Value),
                Epoch2 = SqlFunctions.Sql.date_part("epoch", Value)
            })
            .First();

        r.Quarter.Should().Be(2);
        r.Dow.Should().Be(3);
        r.IsoDow.Should().Be(3);
        r.Doy.Should().Be(137);
        r.Week.Should().Be(20);
        r.Epoch.Should().BeApproximately(1684331130, 0.001);
        r.Epoch2.Should().BeApproximately(1684331130, 0.001);
    }

    [Fact]
    public void NumberAndDateFormats_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Fixed = 1234.5.ToString("F2"),
                Padded = 12.ToString("D4"),
                Hex = 255.ToString("X"),
                Date = Value.ToString("yyyy-MM-dd"),
                Clock = Value.ToString("HH:mm:ss"),
                Year = Value.ToString("yyyy")
            })
            .First();

        r.Fixed.Should().Be("1234.50");
        r.Padded.Should().Be("0012");
        r.Hex.Should().Be("ff");
        r.Date.Should().Be("2023-05-17");
        r.Year.Should().Be("2023");
        r.Clock.Should().Be("13:45:30");
    }

    [Fact]
    public void JsonFunctions_ShouldExecute()
    {
        const string Doc = "{\"a\":1,\"b\":[1,2]}";

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Extract = SqlFunctions.Sqlite.json_extract<int>(Doc, "$.a"),
                Get = SqlFunctions.Sqlite.json_get(Doc, "$.a"),
                GetText = SqlFunctions.Sqlite.json_get_text(Doc, "$.a"),
                Array = SqlFunctions.Sqlite.json_array(1, 2, 3),
                Insert = SqlFunctions.Sqlite.json_insert(Doc, "$.c", 3),
                Replace = SqlFunctions.Sqlite.json_replace(Doc, "$.a", 9),
                Set = SqlFunctions.Sqlite.json_set(Doc, "$.a", 2),
                Object = SqlFunctions.Sqlite.json_object("a", 1),
                Patch = SqlFunctions.Sqlite.json_patch(Doc, "{\"c\":3}"),
                Pretty = SqlFunctions.Sqlite.json_pretty(Doc),
                Quote = SqlFunctions.Sqlite.json_quote("abc"),
                Remove = SqlFunctions.Sqlite.json_remove(Doc, "$.a"),
                Type = SqlFunctions.Sqlite.json_type(Doc),
                TypePath = SqlFunctions.Sqlite.json_type(Doc, "$.b"),
                Valid = SqlFunctions.Sqlite.json_valid(Doc),
                ValidFlags = SqlFunctions.Sqlite.json_valid(Doc, 1),
                ArrayInsert = SqlFunctions.Sqlite.json_array_insert("[1,2]", "$[0]", 0)
            })
            .First();

        r.Extract.Should().Be(1);
        r.Get.Should().NotBeNull();
        r.GetText.Should().Be("1");
        r.Array.Should().Be("[1,2,3]");
        r.Insert.Should().Contain("\"c\":3");
        r.Replace.Should().Contain("\"a\":9");
        r.Set.Should().Contain("\"a\":2");
        r.Object.Should().Contain("\"a\":1");
        r.Patch.Should().Contain("\"c\":3");
        r.Pretty.Should().Contain("\n");
        r.Quote.Should().Be("\"abc\"");
        r.Remove.Should().NotContain("\"a\"");
        r.Type.Should().Be("object");
        r.TypePath.Should().Be("array");
        r.Valid.Should().BeTrue();
        r.ValidFlags.Should().BeTrue();
        r.ArrayInsert.Should().Contain("0");
    }

    [Fact]
    public void SqliteScalarHelpers_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Printf = SqlFunctions.Sqlite.printf("%s-%d", "a", 1),
                Format = SqlFunctions.Sqlite.format("%s-%d", "a", 1),
                Hex = SqlFunctions.Sqlite.hex("abc"),
                Unhex = SqlFunctions.Sqlite.unhex("616263"),
                UnhexIgnored = SqlFunctions.Sqlite.unhex("616263", "x"),
                Random = SqlFunctions.Sqlite.random(),
                Quote = SqlFunctions.Sqlite.quote("abc"),
                TypeOf = SqlFunctions.Sqlite.@typeof(1),
                Glob = SqlFunctions.Sqlite.glob("a*", "abc"),
                Unicode = SqlFunctions.Sqlite.unicode("A"),
                Char = SqlFunctions.Sqlite.@char(65, 66),
                Octets = SqlFunctions.Sqlite.octet_length("abc"),
                IfNull = SqlFunctions.Sqlite.ifnull<string>(null, "fallback"),
                If = SqlFunctions.Sqlite.@if(true, "yes", "no"),
                Diff = SqlFunctions.Sqlite.timediff(Value.AddHours(1), Value),
                Unix = SqlFunctions.Sqlite.unixepoch(Value),
                Julian = SqlFunctions.Sqlite.julianday(Value),
                Mod = SqlFunctions.Sqlite.mod(7, 4),
                Log2 = SqlFunctions.Sqlite.log2(8),
                Log10 = SqlFunctions.Sqlite.log10(1000),
                Acosh = SqlFunctions.Sqlite.acosh(1),
                Asinh = SqlFunctions.Sqlite.asinh(0),
                Atanh = SqlFunctions.Sqlite.atanh(0),
                Cosh = SqlFunctions.Sqlite.cosh(0),
                Sinh = SqlFunctions.Sqlite.sinh(0),
                Tanh = SqlFunctions.Sqlite.tanh(0),
                Atan2 = SqlFunctions.Sqlite.atan2(1, 1)
            })
            .First();

        r.Printf.Should().Be("a-1");
        r.Format.Should().Be("a-1");
        r.Hex.Should().Be("616263");
        r.Unhex.Should().Equal(97, 98, 99);
        r.UnhexIgnored.Should().Equal(97, 98, 99);
        r.Random.Should().NotBe(0);
        r.Quote.Should().Be("'abc'");
        r.TypeOf.Should().Be("integer");
        r.Glob.Should().BeTrue();
        r.Unicode.Should().Be(65);
        r.Char.Should().Be("AB");
        r.Octets.Should().Be(3);
        r.IfNull.Should().Be("fallback");
        r.If.Should().Be("yes");
        r.Diff.Should().StartWith("+0000-00-00 01:00:00");
        r.Unix.Should().Be(1684331130);
        r.Julian.Should().BeApproximately(2460082.0733, 0.001);
        r.Mod.Should().Be(3);
        r.Log2.Should().Be(3);
        r.Log10.Should().Be(3);
        r.Acosh.Should().Be(0);
        r.Asinh.Should().Be(0);
        r.Atanh.Should().Be(0);
        r.Cosh.Should().Be(1);
        r.Sinh.Should().Be(0);
        r.Tanh.Should().Be(0);
        r.Atan2.Should().BeApproximately(Math.Atan2(1, 1), 1e-9);
    }

    /// <summary>
    /// The folded/scaled <c>MakeDateAdd</c> units not already swept above: <c>decade</c>/<c>century</c>/
    /// <c>millennium</c> fold onto a scaled <c>years</c> modifier and <c>microseconds</c> takes the
    /// sub-second <c>strftime</c> form, so the fraction survives.
    /// </summary>
    [Fact]
    public void DateAddFoldedUnits_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Decade = SqlFunctions.Sql.date_add("decade", 1, Value),
                Century = SqlFunctions.Sql.date_add("century", 1, Value),
                Millennium = SqlFunctions.Sql.date_add("millennium", 1, Value),
                Micros = SqlFunctions.Sql.date_add("microseconds", 500000, Value)
            })
            .First();

        r.Decade.Should().Be(Value.AddYears(10));
        r.Century.Should().Be(Value.AddYears(100));
        r.Millennium.Should().Be(Value.AddYears(1000));
        r.Micros.Should().BeCloseTo(Value.AddMilliseconds(500), TimeSpan.FromMilliseconds(1));
    }

    /// <summary>
    /// The dialect-local unsupported-unit guards: a field outside the renderer switch is rejected with
    /// the documented message for both <c>date_add</c> and <c>date_diff</c>, while the live control query
    /// still answers.
    /// </summary>
    [Fact]
    public void DateUnitGuards_ShouldRejectUnknownFields()
    {
        var dialect = ((DataContext)_sut.DataProvider).Dialect;

        FluentActions.Invoking(() => dialect.MakeDateAdd("fortnight", "1", "d"))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*SQLite date_add does not support the 'fortnight' field*");
        FluentActions.Invoking(() => dialect.MakeDateDiff("fortnight", "a", "b"))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*SQLite date_diff does not support the 'fortnight' field*");

        _sut.SimpleEntity.Where(x => x.Id == 1).Select(x => x.Id).First().Should().Be(1);
    }

    /// <summary>
    /// <c>MakeNow</c>: SQLite has no <c>now()</c>, so both <see cref="DateTime.Now"/> and
    /// <see cref="DateTime.UtcNow"/> render <c>datetime('now')</c> (UTC) and answer inside a tolerance
    /// window of the client clock, monotonically.
    /// </summary>
    [Fact]
    public void Now_ShouldUseTheDatabaseClock()
    {
        var before = DateTime.UtcNow;

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { Utc = DateTime.UtcNow, Local = DateTime.Now })
            .First();

        var after = DateTime.UtcNow;

        r.Utc.Should().BeOnOrAfter(before.AddMinutes(-1));
        r.Utc.Should().BeOnOrBefore(after.AddMinutes(1));
        r.Local.Should().BeOnOrAfter(before.AddMinutes(-1));
        r.Local.Should().BeOnOrBefore(after.AddMinutes(1));
    }

    /// <summary>
    /// <c>MakeQualifiedTableName</c> guards: a linked-server qualifier and the database+schema pair are
    /// both rejected because SQLite has one qualifier level, while a single attached-database qualifier
    /// renders and executes.
    /// </summary>
    [Fact]
    public void TableQualifierGuards_ShouldRejectLinkedServerAndDoubleQualifier()
    {
        FluentActions.Invoking(() => _sut.SimpleEntity
                .WithServer("linked")
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*linked-server table qualifier is not supported*");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .WithDatabase("main")
                .WithSchema("temp")
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*database and the schema as the same qualifier*");

        var ids = _sut.SimpleEntity
            .WithSchema("main")
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().NotBeEmpty();
    }

    /// <summary>
    /// <c>MakeDateTimeOffsetNow</c>: SQLite stores a <see cref="DateTimeOffset"/> as UTC text, so both
    /// <see cref="DateTimeOffset.Now"/> and <see cref="DateTimeOffset.UtcNow"/> are rendered server-side
    /// and materialize as an offset-zero instant inside a tolerance window of the client clock.
    /// </summary>
    [Fact]
    public void DateTimeOffsetNow_ShouldUseTheDatabaseClock()
    {
        var before = DateTimeOffset.UtcNow;

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { Now = DateTimeOffset.Now, UtcNow = DateTimeOffset.UtcNow })
            .First();

        var after = DateTimeOffset.UtcNow;

        r.UtcNow.Offset.Should().Be(TimeSpan.Zero);
        r.UtcNow.Should().BeOnOrAfter(before.AddMinutes(-1));
        r.UtcNow.Should().BeOnOrBefore(after.AddMinutes(1));
        r.Now.Offset.Should().Be(TimeSpan.Zero);
        r.Now.Should().BeOnOrAfter(before.AddMinutes(-1));
        r.Now.Should().BeOnOrBefore(after.AddMinutes(1));
    }

    /// <summary>
    /// The <see cref="DateTimeOffset"/> date-part matrix goes through the DTO-aware
    /// <c>MakeDatePart</c> overload; a captured offset-zero value keeps every part stable.
    /// </summary>
    [Fact]
    public void DateTimeOffsetParts_ShouldExecute()
    {
        var value = new DateTimeOffset(2023, 5, 17, 13, 45, 30, TimeSpan.Zero);

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                value.Year,
                value.Month,
                value.Day,
                value.Hour,
                value.Minute,
                value.Second,
                value.DayOfYear
            })
            .First();

        r.Year.Should().Be(2023);
        r.Month.Should().Be(5);
        r.Day.Should().Be(17);
        r.Hour.Should().Be(13);
        r.Minute.Should().Be(45);
        r.Second.Should().Be(30);
        r.DayOfYear.Should().Be(137);
    }

    /// <summary>
    /// A <see cref="DateTimeOffset"/> member with no server translation must fail closed instead of
    /// capturing the client clock or time zone.
    /// </summary>
    [Fact]
    public void DateTimeOffsetUnsupportedMembers_ShouldBeRejected()
    {
        var value = new DateTimeOffset(2023, 5, 17, 13, 45, 30, TimeSpan.FromHours(2));

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => value.Offset)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*DateTimeOffset.Offset is not supported in a server-side query*");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => value.UtcDateTime)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*DateTimeOffset.UtcDateTime is not supported in a server-side query*");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => value.LocalDateTime)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*DateTimeOffset.LocalDateTime is not supported in a server-side query*");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => value.Date)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*DateTimeOffset.Date is not supported in a server-side query*");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => value.Ticks)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*DateTimeOffset.Ticks is not supported in a server-side query*");
    }

    /// <summary>
    /// <c>new string(char, n)</c> is rendered as the dialect's repetition: a constant character with a
    /// constant count and with a column-derived count both execute.
    /// </summary>
    [Fact]
    public void NewStringRepetition_ShouldExecute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Constant = new string('x', 3),
                ColumnCount = new string('a', x.String!.Length)
            })
            .First();

        r.Constant.Should().Be("xxx");
        r.ColumnCount.Should().Be("aaa");
    }

    /// <summary>SQL has no char type, so the repeated character has to be a compile-time constant.</summary>
    [Fact]
    public void NewStringNonConstantCharacter_ShouldBeRejected()
    {
        var character = 'x';

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => new string(character, 3))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The new string(char, n) character must be a constant.");
    }

    /// <summary>
    /// The untyped table accessors: a method call on a <see cref="TableColumn"/> value, a call to a
    /// non-accessor on a <see cref="TableAlias"/>, and an accessor whose column name is not a constant
    /// all have no SQL shape and are rejected.
    /// </summary>
    [Fact]
    public void RawTableAccessorGuards_ShouldRejectCallsAndNonConstantAccessors()
    {
        FluentActions.Invoking(() => _sut.From("simple_entity")
                .Select(t => t["id"].ToString())
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("Calls on TableColumn are not supported.");

        FluentActions.Invoking(() => _sut.From("simple_entity")
                .Select(t => t.ToString())
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("ToString");

        var column = "id";

        FluentActions.Invoking(() => _sut.From("simple_entity")
                .Where(t => t.GetInt32(column) > 0)
                .Select(t => t.GetInt32(column))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("GetInt32");
    }

    /// <summary>
    /// SQLite does not enforce scalar-subquery cardinality and cannot attach a count guard to a
    /// non-numeric projection, so <c>Single()</c> over a string scalar is refused at translation.
    /// </summary>
    [Fact]
    public void ScalarSubquerySingleOnNonNumericProjection_ShouldBeRejected()
    {
        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(s => s.Id == 1)
                .Select(s => _sut.ComplexEntity.Where(c => c.Id == s.Id).Select(c => c.String).Single())
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*cannot enforce Single/SingleOrDefault in a scalar subquery*");
    }

    /// <summary>
    /// A property stored as a pair of range columns has no single-column value, so projecting it
    /// outside a range role is refused instead of rendering one bound.
    /// </summary>
    [Fact]
    public void RangeColumnPairProjectedAsScalar_ShouldBeRejected()
    {
        var ctx = (DataContext)_sut.DataProvider;

        FluentActions.Invoking(() => ctx.From<CommonTestSuite.IRangeProbe>()
                .Where(x => x.During.Equals(new Range<int>(1, 10)))
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*is stored as a pair of columns and cannot be used as a single value*");
    }

    /// <summary>
    /// The array surface is PostgreSQL (the <c>any</c>/<c>all</c> quantifiers and the array functions)
    /// or ClickHouse (the native array functions, <c>arrayJoin</c> and the higher-order lambda forms).
    /// SQLite rejects each family with its documented message instead of rendering invalid SQL.
    /// </summary>
    [Fact]
    public void UnsupportedArrayFunctions_ShouldBeRejected()
    {
        var values = new[] { 1, 2 };

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => SqlFunctions.Postgres.cardinality(values))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Arrays are not supported by this provider*");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => SqlFunctions.ClickHouse.length(values))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*The array functions require a provider with a native array type*");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => SqlFunctions.ClickHouse.array_join(values))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*arrayJoin requires a provider that supports it*");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => SqlFunctions.ClickHouse.array_map(v => v + 1, values))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*higher-order array functions*");
    }

    /// <summary>
    /// <c>string.Format</c> translation (<c>StringFormatTranslator.TryTranslateFormat</c> :18-38 and
    /// <c>Emit</c> :64-109) executed against SQLite: the provider-free and <c>null</c>-provider overloads,
    /// the whole-format literal arms (one piece, empty, escaped braces), the fixed-argument and
    /// params-array argument lists, a reordered/repeated placeholder and both alignment directions.
    /// </summary>
    [Fact]
    public void StringFormat_ShouldExecutePortableShapes()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Fixed = string.Format("{0}-{1}", x.Id, 7),
                NullProvider = string.Format((IFormatProvider?)null, "{0}", x.Id),
                Single = string.Format("{0}", x.Id),
                Literal = string.Format("plain", new object[0]),
                Empty = string.Format("", new object[0]),
                Braces = string.Format("{{x}}", new object[0]),
                Concat = string.Format("a{0}b", x.Id),
                Reordered = string.Format("{1}/{0}/{1}", x.Id, 9),
                ParamsArray = string.Format("{0}{1}{2}", new object[] { x.Id, 1, 2 }),
                Right = string.Format("{0,6}", "abc"),
                Left = string.Format("{0,-6}", "abc")
            })
            .First();

        r.Fixed.Should().Be("1-7");
        r.NullProvider.Should().Be("1");
        r.Single.Should().Be("1");
        r.Literal.Should().Be("plain");
        r.Empty.Should().BeEmpty();
        r.Braces.Should().Be("{x}");
        r.Concat.Should().Be("a1b");
        r.Reordered.Should().Be("9/1/9");
        r.ParamsArray.Should().Be("112");
        r.Right.Should().Be("   abc");
        r.Left.Should().Be("abc   ");
    }

    /// <summary>
    /// The numeric and date/time specifiers of <c>StringFormatTranslator.RenderValue</c> (:123-152) through
    /// the SQLite <c>printf</c>/<c>strftime</c> renderers: every supported letter with and without a
    /// precision, the CLR default precisions and an aligned formatted value.
    /// </summary>
    [Fact]
    public void StringFormat_NumericAndDateSpecifiers_ShouldUseTheNativeRenderers()
    {
        var moment = new DateTime(2023, 5, 17, 13, 45, 30);

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                DefaultFraction = string.Format("{0:F}", 1234.5),
                Fixed = string.Format("{0:F2}", 1234.5),
                DefaultDigits = string.Format("{0:D}", 12),
                Padded = string.Format("{0:D4}", 12),
                DefaultHex = string.Format("{0:X}", 255),
                PaddedHex = string.Format("{0:X4}", 255),
                Date = string.Format("{0:yyyy-MM-dd}", moment),
                Clock = string.Format("{0:HH:mm:ss}", moment),
                Aligned = string.Format("{0,8:F2}", 12.5)
            })
            .First();

        r.DefaultFraction.Should().Be("1234.50");
        r.Fixed.Should().Be("1234.50");
        r.DefaultDigits.Should().Be("12");
        r.Padded.Should().Be("0012");
        r.DefaultHex.Should().Be("ff");
        r.PaddedHex.Should().Be("00ff");
        r.Date.Should().Be("2023-05-17");
        r.Clock.Should().Be("13:45:30");
        r.Aligned.Should().Be("   12.50");
    }

    /// <summary>
    /// The parameter-only pass of the format translation (<c>Emit</c> :68-79 and
    /// <c>TryTranslateToString</c> :53-57): a captured bound and a formatted <c>ToString</c> in the same
    /// projection run twice on one context, so the cached plan re-walks the values in parameter mode and
    /// still reads each run's own bound.
    /// </summary>
    [Fact]
    public void StringFormat_ParameterPass_ShouldWalkTheValuesAndRefreshTheBound()
    {
        var limit = 2;

        List<string> Read() => _sut.SimpleEntity
            .Where(x => x.Id <= limit)
            .OrderBy(x => x.Id)
            .Select(x => new { Value = string.Format("{1}:{0}", x.Id, x.Id * 10), Digits = x.Id.ToString("D4") })
            .ToList()
            .Select(r => r.Value + "|" + r.Digits)
            .ToList();

        Read().Should().Equal("10:1|0001", "20:2|0002");

        limit = 1;

        Read().Should().Equal(new[] { "10:1|0001" }, "the cached plan re-walks the format values in parameter mode");
    }

    /// <summary>
    /// The documented rejections of <c>StringFormatTranslator</c>: a non-constant format string (:32 and
    /// :50), an out-of-range argument (:113), the unsupported numeric letters (:167) and precisions
    /// (:179), a provider without the numeric format (:135), an unsupported date token (:145), a format
    /// specifier on a non-numeric/non-date value (:151) and a culture-sensitive provider (:196).
    /// </summary>
    [Fact]
    public void StringFormat_UnsupportedShapes_ShouldBeRejected()
    {
        var format = "{0}";
        var moment = new DateTime(2023, 5, 17, 13, 45, 30);

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => string.Format(format, x.Id))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("string.Format requires a constant format string.");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => x.Id.ToString(format))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("Int32.ToString requires a constant format string.");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => string.Format("{1}", x.Id))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("string.Format references argument 1, which is out of range.");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => string.Format("{0:E2}", x.Id))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The numeric format specifier 'E2' is not supported; only N, F, D and X are portable.");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => string.Format("{0:N2}", 1234.5))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The numeric format specifier 'N' is not supported by this provider.");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => string.Format("{0:F2x}", 1.5))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The numeric format specifier 'F2x' is not supported.");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => string.Format("{0:MMM}", moment))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*The date/time format string 'MMM' is not supported by this provider*");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => string.Format("{0:F2}", x.String))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("A format specifier is not supported for type String.");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => string.Format(CultureInfo.CurrentCulture, "{0}", x.Id))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*string.Format is only supported with CultureInfo.InvariantCulture*");
    }

    /// <summary>
    /// The null-aware presence projection of <c>PredicateTranslator.VisitBinary</c> (:354-378) from both
    /// operand orders and for both <c>==</c>/<c>!=</c>, the bool/value coalesce arms (:384-401) and a
    /// conditional whose test is a bare boolean value (the value path of <c>RenderPredicate</c> :126-142).
    /// The shape carries a captured bound and runs twice, so the parameter-only coalesce arm (:385) is
    /// walked as well and each run reads its own bound.
    /// </summary>
    [Fact]
    public void PredicatePresenceCoalesceAndConditional_ShouldMaterializeValues()
    {
        var min = 1;

        List<string> Read() => _sut.ComplexEntity
            .Where(x => x.Id >= min)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                IntMissing = x.Int == null,
                IntPresent = x.Int != null,
                BoolMissing = null == x.Boolean,
                BoolPresent = null != x.Boolean,
                Value = x.Int ?? -1,
                Flag = x.Boolean ?? false,
                BareTest = (x.Boolean ?? false) ? 1 : 0
            })
            .ToList()
            .Select(r => $"{r.IntMissing},{r.IntPresent},{r.BoolMissing},{r.BoolPresent},{r.Value},{r.Flag},{r.BareTest}")
            .ToList();

        Read().Should().Equal(
            "True,False,False,True,-1,True,1",
            "False,True,False,True,1,False,0",
            "False,True,False,True,1,False,0");

        min = 2;

        Read().Should().Equal(
            new[] { "False,True,False,True,1,False,0", "False,True,False,True,1,False,0" },
            "the cached plan re-walks the presence and coalesce operands in parameter mode");
    }

    // -------------------------------------------------------------------------------------------------
    // D10-R2-03 / U3: the public JSON streaming (JsonRowWriterFactory) and CSV (CsvRowBuffer) routes
    // driven by real SQLite reads. JSON is asserted as parsed content or exact document text, CSV as the
    // exact UTF-8 fields it writes; no buffer internals are inspected.

    private static string WriteJsonText<TResult>(QueryCommand<TResult> command, JsonStreamOptions? options = null)
    {
        using var buffer = new MemoryStream();
        if (options is null)
            command.WriteJson(buffer);
        else
            command.WriteJson(buffer, options);

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static async Task<string> WriteJsonTextAsync<TResult>(QueryCommand<TResult> command, JsonStreamOptions? options = null)
    {
        using var buffer = new MemoryStream();
        if (options is null)
            await command.WriteJsonAsync(buffer, TestContext.Current.CancellationToken);
        else
            await command.WriteJsonAsync(buffer, options, TestContext.Current.CancellationToken);

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string WriteCsvText<TResult>(QueryCommand<TResult> command, CsvStreamOptions options, params object?[] parameters)
    {
        using var buffer = new MemoryStream();
        command.WriteCsv(buffer, options, TestContext.Current.CancellationToken, parameters);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// The flat phase-1 path: a <c>*OrDefault</c> correlated scalar in an object member is a "no row"
    /// value and is substituted with <c>default(T)</c> (not JSON null), while a nullable member with no
    /// value stays JSON null. The scalar root form of the same substitution is checked as well.
    /// </summary>
    [Fact]
    public void Json_FlatDefaultOnNullAndNullableMember_ShouldWriteDefaultAndNull()
    {
        var command = _sut.SimpleEntity
            .Where(x => x.Id >= 4 && x.Id <= 5)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Missing = _sut.ComplexEntity.Where(c => c.Id == x.Id).Select(c => c.Id).FirstOrDefault(),
                Child = _sut.ComplexEntity.Where(c => c.Id == x.Id).Select(c => c.Int).FirstOrDefault()
            });

        WriteJsonText(command).Should().Be(
            "[{\"Id\":4,\"Missing\":0,\"Child\":null},{\"Id\":5,\"Missing\":0,\"Child\":null}]");

        var scalar = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => _sut.ComplexEntity.Where(c => c.Id == x.Id + 100).Select(c => c.Id).FirstOrDefault());

        WriteJsonText(scalar).Should().Be("[0]");
    }

    /// <summary>
    /// The recursive shape path: a nested anonymous construction with a nullable member and a
    /// <c>*OrDefault</c> correlated scalar member, read through the captured shape's per-leaf ordinals.
    /// </summary>
    [Fact]
    public void Json_NestedShape_ShouldWriteDefaultOnNullLeaf()
    {
        var command = _sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Child = new
                {
                    x.String,
                    Missing = _sut.ComplexEntity.Where(c => c.Id == x.Id + 100).Select(c => c.Id).FirstOrDefault()
                }
            });

        WriteJsonText(command).Should().Be(
            "[{\"Id\":1,\"Child\":{\"String\":\"dadfasd\",\"Missing\":0}}," +
            "{\"Id\":2,\"Child\":{\"String\":\"xxx\",\"Missing\":0}}," +
            "{\"Id\":3,\"Child\":{\"String\":null,\"Missing\":0}}]");
    }

    /// <summary>
    /// A conditional nested object: the unmatched arm is JSON null (or omitted under <c>IgnoreNull</c>)
    /// and a null leaf member is omitted only when <c>IgnoreNull</c> is set.
    /// </summary>
    [Fact]
    public void Json_ConditionalChild_ShouldHonourIgnoreNull()
    {
        var command = _sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.Id == 1 ? null : new { x.String } });

        WriteJsonText(command).Should().Be(
            "[{\"Id\":1,\"Child\":null},{\"Id\":2,\"Child\":{\"String\":\"xxx\"}},{\"Id\":3,\"Child\":{\"String\":null}}]");

        WriteJsonText(command, new JsonStreamOptions { IgnoreNull = true }).Should().Be(
            "[{\"Id\":1},{\"Id\":2,\"Child\":{\"String\":\"xxx\"}},{\"Id\":3,\"Child\":{}}]");
    }

    /// <summary>
    /// The typed reader path of a nested shape: numeric CLR targets converted from the provider field
    /// type, the unsigned enum underlying types, a stock string-enum converter, a DateTime and a
    /// <c>byte[]</c> leaf (all read from the open <see cref="System.Data.IDataRecord"/>), and the
    /// asynchronous terminal producing the same bytes as the synchronous one.
    /// </summary>
    [Fact]
    public async Task Json_TypedNestedMembers_ShouldReadFromTheDataReader()
    {
        var command = _sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Typed = new
                {
                    I = x.Int ?? 0,
                    L = x.Id,
                    B = x.TinyInt,
                    S = (short)x.Id,
                    F = x.Real,
                    D = x.Double,
                    M = x.Numeric,
                    RawDouble = SqlFunctions.Column<double>(x, "id"),
                    RawLong = SqlFunctions.Column<long>(x, "r"),
                    U = (UIntState)x.Id,
                    Ul = (ULongState)x.Id,
                    Sb = (SByteState)x.Id,
                    Us = (UShortState)x.Id,
                    Named = (NamedState)x.Id,
                    Dt = x.Datetime,
                    Bin = SqlFunctions.Sqlite.unhex("0102")
                }
            });

        var text = WriteJsonText(command);

        using var document = JsonDocument.Parse(text);
        var rows = document.RootElement;

        rows.GetArrayLength().Should().Be(3);
        rows[0].GetProperty("Id").GetInt64().Should().Be(1);

        var first = rows[0].GetProperty("Typed");
        first.GetProperty("I").GetInt32().Should().Be(0);
        first.GetProperty("L").GetInt64().Should().Be(1);
        first.GetProperty("B").GetInt32().Should().Be(2);
        first.GetProperty("S").GetInt32().Should().Be(1);
        first.GetProperty("F").GetSingle().Should().Be(4f);
        first.GetProperty("D").GetDouble().Should().Be(5d);
        first.GetProperty("M").GetDecimal().Should().Be(6m);
        first.GetProperty("RawDouble").GetDouble().Should().Be(1d);
        first.GetProperty("RawLong").GetInt64().Should().Be(4L);
        first.GetProperty("U").GetUInt32().Should().Be(1u);
        first.GetProperty("Ul").GetUInt64().Should().Be(1ul);
        first.GetProperty("Sb").GetSByte().Should().Be((sbyte)1);
        first.GetProperty("Us").GetUInt16().Should().Be((ushort)1);
        first.GetProperty("Named").GetString().Should().Be("One");
        first.GetProperty("Dt").GetDateTime().Should().Be(new DateTime(2023, 1, 1, 10, 0, 0));
        first.GetProperty("Bin").GetString().Should().Be("AQI=");

        var third = rows[2].GetProperty("Typed");
        third.GetProperty("I").GetInt32().Should().Be(1);
        third.GetProperty("F").ValueKind.Should().Be(JsonValueKind.Null);
        third.GetProperty("RawLong").ValueKind.Should().Be(JsonValueKind.Null);
        third.GetProperty("Named").GetString().Should().Be("Three");

        (await WriteJsonTextAsync(command)).Should().Be(text);
    }

    /// <summary>
    /// The reader-binding guard: a numeric projection whose provider field type is not numeric is
    /// rejected after the reader opens and before any output byte is written, with the stable message.
    /// </summary>
    [Fact]
    public void Json_UnsupportedReaderFieldType_ShouldRejectBeforeOutput()
    {
        var objectShape = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { Bad = SqlFunctions.Column<int>(x, "somestring") });

        using var objectBuffer = new MemoryStream();
        FluentActions.Invoking(() => objectShape.WriteJson(objectBuffer))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*[reader-binding]*declared Int32*provider field type 'System.String' which is not supported*");
        objectBuffer.Length.Should().Be(0);

        var scalarShape = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Column<int>(x, "somestring"));

        using var scalarBuffer = new MemoryStream();
        FluentActions.Invoking(() => scalarShape.WriteJson(scalarBuffer))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*[reader-binding]*provider field type 'System.String' which is not supported*");
        scalarBuffer.Length.Should().Be(0);
    }

    /// <summary>
    /// A naming policy that returns null for a nested member is rejected while the recursive writer is
    /// compiled, before the destination is touched.
    /// </summary>
    [Fact]
    public void Json_NullReturningNamingPolicy_ShouldRejectBeforeOutput()
    {
        var command = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { x.Id, Child = new { x.String } });

        using var buffer = new MemoryStream();
        FluentActions.Invoking(() => command.WriteJson(buffer, new JsonStreamOptions { PropertyNamingPolicy = new NullForStringPolicy() }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*The property naming policy returned null for member 'String'*");
        buffer.Length.Should().Be(0);
    }

    private sealed class NullForStringPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) => name == "String" ? null! : name;
    }

    /// <summary>
    /// The NULL-marker collision contract on both the string and the formatted field paths, including
    /// the Excel guard apostrophe that would itself complete the marker, plus the empty/RFC 4180 arm of
    /// the quoted string writer.
    /// </summary>
    [Fact]
    public void Csv_NullMarkerCollision_ShouldQuoteBothFieldKinds()
    {
        var command = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                x.Id,
                Text = SqlFunctions.Parameter<string>(0),
                Number = SqlFunctions.Parameter<int>(1),
                Edge = SqlFunctions.Parameter<string>(2)
            });

        // The marker "1" collides with the numeric texts and with the literal text: every colliding
        // field is quoted so a reader can never mistake a value for NULL. The Edge value starts with a
        // double quote, so the writer has to open, double and close it.
        WriteCsvText(command, new CsvStreamOptions { IncludeHeader = false, NullMarker = "1" }, "1", 1, "\"lead")
            .Should().Be("\"1\",\"1\",\"1\",\"\"\"lead\"\r\n");

        // The Excel guard apostrophe can itself complete the marker: the guarded numeric field must be
        // quoted instead of emitted as a bare (and thus NULL-looking) value.
        var guarded = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { Negative = x.Id - 100L });

        WriteCsvText(guarded, new CsvStreamOptions { IncludeHeader = false, NullMarker = "'-99", ExcelMode = true })
            .Should().Be("\"'-99\"\r\n");

        // Same collision on the string path.
        var text = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { Text = SqlFunctions.Parameter<string>(0) });

        WriteCsvText(text, new CsvStreamOptions { IncludeHeader = false, NullMarker = "'=x", ExcelMode = true }, "=x")
            .Should().Be("\"'=x\"\r\n");
    }

    /// <summary>
    /// The Excel formula guard on a formatted field and on string fields, with and without RFC 4180
    /// quoting, next to an empty string field (an empty field is not the NULL marker).
    /// </summary>
    [Fact]
    public void Csv_ExcelGuard_ShouldPrefixFormulaStarts()
    {
        var command = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Negative = x.Id - 100L,
                Text = SqlFunctions.Parameter<string>(0),
                Formula = SqlFunctions.Parameter<string>(1),
                EmptyText = SqlFunctions.Parameter<string>(2),
                Flag = x.Boolean
            });

        WriteCsvText(command, new CsvStreamOptions { IncludeHeader = false, ExcelMode = true }, "=a,b", "+cmd", "")
            .Should().Be("'-99,\"'=a,b\",'+cmd,,true\r\n");
    }

    /// <summary>
    /// The per-value transform runs before escaping on the string and <c>byte[]</c> paths, while an
    /// empty byte array stays an empty field (never the NULL marker) and never invokes the transform.
    /// </summary>
    [Fact]
    public void Csv_ValueTransformAndEmptyBytes_ShouldKeepFieldPolicy()
    {
        var command = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Text = SqlFunctions.Parameter<string>(0),
                Blob = SqlFunctions.Parameter<byte[]>(1),
                Empty = SqlFunctions.Parameter<byte[]>(2)
            });

        var options = new CsvStreamOptions
        {
            IncludeHeader = false,
            ValueTransform = value => value switch
            {
                byte[] bytes => Convert.ToBase64String(bytes) + ";bytes",
                string text => text + ";text",
                _ => null
            }
        };

        WriteCsvText(command, options, "abc", new byte[] { 1, 2 }, Array.Empty<byte>())
            .Should().Be("abc;text,AQI=;bytes,\r\n");
    }

    /// <summary>
    /// A row whose Base64 payload exceeds the row buffer's initial capacity: the buffer grows in place
    /// and every following row still carries its own complete value.
    /// </summary>
    [Fact]
    public void Csv_RowLargerThanTheInitialBuffer_ShouldGrowAndKeepValues()
    {
        var hex = new string('a', 7000);
        var expectedPayload = Convert.ToBase64String(Enumerable.Repeat((byte)0xAA, 3500).ToArray());

        var command = _sut.SimpleEntity.Where(x => x.Id <= 2).OrderBy(x => x.Id)
            .Select(x => new { x.Id, Payload = SqlFunctions.Sqlite.unhex(SqlFunctions.Parameter<string>(0)) });

        WriteCsvText(command, new CsvStreamOptions { IncludeHeader = false }, hex)
            .Should().Be($"1,{expectedPayload}\r\n2,{expectedPayload}\r\n");
    }

    /// <summary>
    /// The FTS5 maintenance builder's <c>integrity-check</c> rank-value arms (<c>false</c> = 0,
    /// <c>true</c> = 1) and the merge-family control forms execute against a real FTS5 virtual table.
    /// </summary>
    [Fact]
    public void Fts5Maintenance_RankValueArmsAndMergeFamily_ShouldExecute()
    {
        using var _ = _sut.DataProvider.ExecuteRaw("drop table if exists sx5_fts");
        using var __ = _sut.DataProvider.ExecuteRaw("create virtual table sx5_fts using fts5(title, body)");
        using var ___ = _sut.DataProvider.ExecuteRaw(
            "insert into sx5_fts(title, body) values ('hello', 'world'), ('other', 'text')");

        var merge = _sut.DataProvider.CreateSqliteFts5CommandBuilder("sx5_fts").Merge(4).Execute();
        var autoMerge = _sut.DataProvider.CreateSqliteFts5CommandBuilder("sx5_fts").AutoMerge(2).Execute();
        var crisisMerge = _sut.DataProvider.CreateSqliteFts5CommandBuilder("sx5_fts").CrisisMerge(1).Execute();
        var internalOnly = _sut.DataProvider.CreateSqliteFts5CommandBuilder("sx5_fts").IntegrityCheck(false).Execute();
        var external = _sut.DataProvider.CreateSqliteFts5CommandBuilder("sx5_fts").IntegrityCheck(true).Execute();

        merge.Should().BeGreaterThanOrEqualTo(0);
        autoMerge.Should().BeGreaterThanOrEqualTo(0);
        crisisMerge.Should().BeGreaterThanOrEqualTo(0);
        internalOnly.Should().BeGreaterThanOrEqualTo(0);
        external.Should().BeGreaterThanOrEqualTo(0);

        var ids = _sut.DataProvider.From<Sx5Fts>().Select(x => x.RowId).ToList();
        ids.Should().HaveCount(2, "maintenance never removes indexed rows");
    }

    /// <summary>
    /// The SQLite <c>total()</c> aggregate (#285) keeps its native semantics: integers, fractions,
    /// negatives and duplicates add up, nulls are ignored, an all-null group and an empty set both return a
    /// single <c>0.0</c> instead of null, a large integer is summed in floating point instead of overflowing
    /// like <c>sum</c>, numeric text is coerced by SQLite itself and a parameterised argument binds.
    /// </summary>
    [Fact]
    [Trait("Issue", "285")]
    public void TotalAggregate_ShouldUseNativeSemantics()
    {
        _sut.DataProvider.FromTableFunction(() => SqlFunctions.Sqlite.json_each("[2, 2, -1, 0]"))
            .Select(x => SqlFunctions.Sqlite.total(x.Value)).First()
            .Should().Be(3.0);

        _sut.DataProvider.FromTableFunction(() => SqlFunctions.Sqlite.json_each("[0.5, 0.25]"))
            .Select(x => SqlFunctions.Sqlite.total(x.Value)).First()
            .Should().Be(0.75);

        _sut.DataProvider.FromTableFunction(() => SqlFunctions.Sqlite.json_each("[1, null, 3]"))
            .Select(x => SqlFunctions.Sqlite.total(x.Value)).First()
            .Should().Be(4.0, "null values are ignored, not treated as zero-sum rows");

        _sut.DataProvider.FromTableFunction(() => SqlFunctions.Sqlite.json_each("[null, null]"))
            .Select(x => SqlFunctions.Sqlite.total(x.Value)).First()
            .Should().Be(0.0, "an all-null group yields 0.0, never null");

        _sut.DataProvider.FromTableFunction(() => SqlFunctions.Sqlite.json_each("[]"))
            .Select(x => SqlFunctions.Sqlite.total(x.Value)).First()
            .Should().Be(0.0, "an empty set still returns one aggregate row");

        _sut.DataProvider.FromTableFunction(() => SqlFunctions.Sqlite.json_each("[\"1\", \"2\", \"3.5\", \"x\"]"))
            .Select(x => SqlFunctions.Sqlite.total(x.Value)).First()
            .Should().Be(6.5, "numeric text is coerced natively and non-numeric text contributes 0.0");

        _sut.DataProvider.FromTableFunction(() => SqlFunctions.Sqlite.json_each("[9223372036854775807, 9223372036854775807]"))
            .Select(x => SqlFunctions.Sqlite.total(x.Value)).First()
            .Should().BeGreaterThan(long.MaxValue, "total sums in floating point instead of overflowing like sum");

        var offset = 0.5;
        _sut.SimpleEntity
            .Select(x => SqlFunctions.Sqlite.total(x.Id + offset)).First()
            .Should().Be(60.0, "a parameterised argument keeps the binding and still aggregates natively");
    }

    [SqlTable("sx5_fts")]
    private sealed class Sx5Fts
    {
        [Key]
        [Column("rowid")]
        public long RowId { get; set; }

        [Column("title")]
        public string? Title { get; set; }
    }

    public enum UIntState : uint
    {
        One = 1,
        Two = 2,
        Three = 3
    }

    public enum ULongState : ulong
    {
        One = 1,
        Two = 2,
        Three = 3
    }

    public enum SByteState : sbyte
    {
        One = 1,
        Two = 2,
        Three = 3
    }

    public enum UShortState : ushort
    {
        One = 1,
        Two = 2,
        Three = 3
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NamedState
    {
        One = 1,
        Two = 2,
        Three = 3
    }
}
