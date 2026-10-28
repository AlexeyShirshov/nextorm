using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Globalization;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>The T348 U1 fixture row: one scalar source and three product groups.</summary>
[SqlTable("t348_u1")]
public interface IT348U1Row
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("grp")]
    int? Grp { get; set; }

    [Column("value")]
    int? Value { get; set; }

    [Column("label")]
    string? Label { get; set; }
}

/// <summary>Concrete row for <see cref="IT348U1Row"/>.</summary>
public sealed class T348U1Row : IT348U1Row
{
    public int Id { get; set; }
    public int? Grp { get; set; }
    public int? Value { get; set; }
    public string? Label { get; set; }
}

/// <summary>A context declared for SQL Server 2025 (major 17).</summary>
internal sealed class T348V17LiveContext(string connectionString)
    : SqlServerDataContext(new Version(17, 0), connectionString);

/// <summary>A context declared for SQL Server 2022 (major 16).</summary>
internal sealed class T348V16LiveContext(string connectionString)
    : SqlServerDataContext(new Version(16, 0), connectionString);

/// <summary>
/// T348 (#282) U1 live evidence on the real 2025 engine: the four exemplars execute with their values,
/// NULL behaviour and CLR types; the regex TVF runs on a dedicated database whose compatibility level
/// is asserted to be 170; and a declared-16 context refuses every 17-only name before SQL while the
/// 2022 sibling <c>left_shift</c> still executes.
/// </summary>
[Collection("SqlServer")]
[Trait("Category", "T348")]
public sealed class T348SqlServer2025FunctionsTests : IDisposable
{
    private const string FixtureTable = "t348_u1";
    private const string RegexDatabase = "t348_regex170";
    private const string RegexLowDatabase = "t348_regex160";
    private const string FuzzyDatabase = "t348_fuzzy";
    private const string FuzzyNoPreviewDatabase = "t348_fuzzy_nopreview";
    private const string FuzzyCollation = "Latin1_General_100_CI_AS";

    private const string FixtureDdl =
        "if object_id('t348_u1') is not null drop table t348_u1; "
        + "create table t348_u1 (id int not null primary key, grp int null, value int null, label nvarchar(50) null); "
        + "insert into t348_u1 (id, grp, value, label) values "
        + "(1, 1, 2, N'a1b2c3'), (2, 1, 3, N'abc'), (3, 1, 4, null), "
        + "(4, 2, 2, N'x'), (5, 2, null, null), (6, 2, 3, N'y'), (7, 3, null, null)";

    private readonly string _connectionString;
    private readonly int _major;

    public T348SqlServer2025FunctionsTests()
    {
        if (!SqlServerContainer.IsAvailable)
        {
            if (ProviderSkipPolicy.IsOptedOut("sqlserver"))
                Assert.Skip($"Provider 'sqlserver' is explicitly opted out via {ProviderSkipPolicy.EnvironmentVariable}.");

            SqlServerContainer.IsAvailable.Should().BeTrue(
                "provider 'sqlserver' is unavailable ({0}); the live test fails closed rather than skipping silently",
                SqlServerContainer.Failure);
        }

        _connectionString = SqlServerContainer.ConnectionString;
        _major = ReadMajor(_connectionString);
        Execute(_connectionString, FixtureDdl);
    }

    public void Dispose()
    {
        if (_connectionString is null)
            return;

        Execute(_connectionString, $"if object_id('{FixtureTable}') is not null drop table {FixtureTable}");
    }

    [Fact]
    public void ActualEngineMajor_ShouldBeSeventeen()
    {
        _major.Should().Be(17, "the 2025 arm must run on the 2025 image, not on 2022");
        Report($"T348LIVE-2025 major={_major} outcome=running_on_2025");
    }

    [Fact]
    public void RegexpCountAndLeftShift_ShouldExecuteWithValuesNullsAndClrTypes()
    {
        using var ctx = new T348V17LiveContext(_connectionString);

        var row = ctx.From<IT348U1Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Count = SqlFunctions.SqlServer.regexp_count("a1b2c3", "[0-9]"),
                NoMatch = SqlFunctions.SqlServer.regexp_count("abc", "[0-9]"),
                NullRegex = SqlFunctions.SqlServer.regexp_count(null, "[0-9]"),
                Shift = SqlFunctions.SqlServer.left_shift((int?)1, 2),
                Negative = SqlFunctions.SqlServer.left_shift((int?)-8, 1),
                NullOperand = SqlFunctions.SqlServer.left_shift((int?)null, 1),
                NullAmount = SqlFunctions.SqlServer.left_shift((int?)8, null)
            })
            .Single();

        row.Count.Should().Be(3);
        row.NoMatch.Should().Be(0, "a no-match regexp_count is 0, not NULL");
        row.NullRegex.Should().BeNull("a NULL input gives NULL");
        row.Shift.Should().Be(4);
        row.Negative.Should().Be(-16, "a negative operand shifts arithmetically");
        row.NullOperand.Should().BeNull("the typed-NULL guard keeps the NULL operand");
        row.NullAmount.Should().BeNull("the typed-NULL guard keeps the NULL shift");

        ((object)row.Count).Should().BeOfType<int>();
        ((object)row.Shift).Should().BeOfType<int>();

        Report($"T348LIVE-2025 regexp_count=3/0/null left_shift=4/-16/null/null outcome=ok");
    }

    /// <summary>
    /// U2: the whole bit family executes on the real 2025 engine across all four operand shapes, with the
    /// typed-NULL guard on the operand and on the shift/offset, and each name lands in its CLR type
    /// (<c>byte?</c>/<c>short?</c>/<c>int?</c>/<c>long?</c> for the shifts and <c>SET_BIT</c>,
    /// <c>long?</c> for <c>BIT_COUNT</c>, <c>bool?</c> for <c>GET_BIT</c>).
    /// </summary>
    [Fact]
    public void BitFamily_ShouldExecuteEveryOperandShapeWithValuesNullsAndClrTypes()
    {
        using var ctx = new T348V17LiveContext(_connectionString);

        // Row id = 1 carries value = 2: shift 1 -> 4, right shift 1 -> 1, one set bit, bit 1 set,
        // set_bit(2, 0) -> 3 and clearing bit 0 -> 2.
        var row = ctx.From<IT348U1Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                TinyL = SqlFunctions.SqlServer.left_shift((byte?)x.Value, 1),
                TinyR = SqlFunctions.SqlServer.right_shift((byte?)x.Value, 1),
                TinyC = SqlFunctions.SqlServer.bit_count((byte?)x.Value),
                TinyG = SqlFunctions.SqlServer.get_bit((byte?)x.Value, 1),
                TinyS = SqlFunctions.SqlServer.set_bit((byte?)x.Value, 0),
                TinyS3 = SqlFunctions.SqlServer.set_bit((byte?)x.Value, 0, false),
                TinyNull = SqlFunctions.SqlServer.left_shift((byte?)null, 1),

                SmallL = SqlFunctions.SqlServer.left_shift((short?)x.Value, 1),
                SmallR = SqlFunctions.SqlServer.right_shift((short?)x.Value, 1),
                SmallC = SqlFunctions.SqlServer.bit_count((short?)x.Value),
                SmallG = SqlFunctions.SqlServer.get_bit((short?)x.Value, 1),
                SmallS = SqlFunctions.SqlServer.set_bit((short?)x.Value, 0),
                SmallNull = SqlFunctions.SqlServer.bit_count((short?)null),

                IntL = SqlFunctions.SqlServer.left_shift(x.Value, 1),
                IntR = SqlFunctions.SqlServer.right_shift(x.Value, 1),
                IntC = SqlFunctions.SqlServer.bit_count(x.Value),
                IntG = SqlFunctions.SqlServer.get_bit(x.Value, 1),
                IntS = SqlFunctions.SqlServer.set_bit(x.Value, 0),
                IntNull = SqlFunctions.SqlServer.get_bit((int?)null, 1),
                IntNullShift = SqlFunctions.SqlServer.left_shift(x.Value, null),

                // Id is an int column; the explicit (long?) conversion selects the bigint operand shape
                // (id = 1).
                LongL = SqlFunctions.SqlServer.left_shift((long?)x.Id, 1),
                LongR = SqlFunctions.SqlServer.right_shift((long?)x.Id, 1),
                LongC = SqlFunctions.SqlServer.bit_count((long?)x.Id),
                LongG = SqlFunctions.SqlServer.get_bit((long?)x.Id, 0),
                LongS = SqlFunctions.SqlServer.set_bit((long?)x.Id, 1),
                LongNull = SqlFunctions.SqlServer.right_shift((long?)null, 1),

                Negative = SqlFunctions.SqlServer.right_shift((int?)-8, 1)
            })
            .Single();

        row.TinyL.Should().Be((byte)4);
        row.TinyR.Should().Be((byte)1);
        row.TinyC.Should().Be(1L);
        row.TinyG.Should().BeTrue();
        row.TinyS.Should().Be((byte)3);
        row.TinyS3.Should().Be((byte)2);
        row.TinyNull.Should().BeNull();

        row.SmallL.Should().Be((short)4);
        row.SmallR.Should().Be((short)1);
        row.SmallC.Should().Be(1L);
        row.SmallG.Should().BeTrue();
        row.SmallS.Should().Be((short)3);
        row.SmallNull.Should().BeNull();

        row.IntL.Should().Be(4);
        row.IntR.Should().Be(1);
        row.IntC.Should().Be(1L);
        row.IntG.Should().BeTrue();
        row.IntS.Should().Be(3);
        row.IntNull.Should().BeNull("the typed-NULL operand guard removes Msg 8116");
        row.IntNullShift.Should().BeNull("the typed-NULL shift guard keeps NULL");

        row.LongL.Should().Be(2L);
        row.LongR.Should().Be(0L);
        row.LongC.Should().Be(1L);
        row.LongG.Should().BeTrue();
        row.LongS.Should().Be(3L);
        row.LongNull.Should().BeNull();
        row.Negative.Should().Be(2147483644, "RIGHT_SHIFT is a logical (zero-fill) shift, not an arithmetic one");

        ((object)row.TinyL!).Should().BeOfType<byte>();
        ((object)row.SmallL!).Should().BeOfType<short>();
        ((object)row.IntL!).Should().BeOfType<int>();
        ((object)row.LongL!).Should().BeOfType<long>();
        ((object)row.TinyC!).Should().BeOfType<long>("BIT_COUNT always returns bigint");
        ((object)row.IntG!).Should().BeOfType<bool>("GET_BIT returns bit");

        Report("T348LIVE-2025 bit tiny=4/1/1/true/3/2/null small=4/1/1/true/3/null int=4/1/1/true/3/null/null long=2/0/1/true/3/null right_shift(-8,1)=2147483644 outcome=ok");
    }

    /// <summary>
    /// U2: both conversions execute on the real 2025 engine over the whole 13-entry closed map — a valid
    /// source for every destination and an invalid source that must come back as SQL NULL, not an engine
    /// error — plus the three style forms of <c>TRY_CONVERT</c>.
    /// </summary>
    [Fact]
    public void TryCastAndTryConvert_ShouldExecuteTheWholeClosedMap()
    {
        using var ctx = new T348V17LiveContext(_connectionString);

        var row = ctx.From<IT348U1Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Byte = SqlFunctions.SqlServer.try_cast<byte>("42"),
                ByteBad = SqlFunctions.SqlServer.try_cast<byte>("300"),
                Short = SqlFunctions.SqlServer.try_cast<short>("42"),
                ShortBad = SqlFunctions.SqlServer.try_cast<short>("70000"),
                Int = SqlFunctions.SqlServer.try_cast<int>("42"),
                IntBad = SqlFunctions.SqlServer.try_cast<int>("not-a-number"),
                Long = SqlFunctions.SqlServer.try_cast<long>("42"),
                LongBad = SqlFunctions.SqlServer.try_cast<long>("not-a-number"),
                Single = SqlFunctions.SqlServer.try_cast<float>("1.5"),
                SingleBad = SqlFunctions.SqlServer.try_cast<float>("x"),
                Double = SqlFunctions.SqlServer.try_cast<double>("1.5"),
                DoubleBad = SqlFunctions.SqlServer.try_cast<double>("x"),
                Decimal = SqlFunctions.SqlServer.try_cast<decimal>("1.5"),
                DecimalBad = SqlFunctions.SqlServer.try_cast<decimal>("x"),
                Bool = SqlFunctions.SqlServer.try_cast<bool>("1"),
                BoolBad = SqlFunctions.SqlServer.try_cast<bool>("x"),
                Guid = SqlFunctions.SqlServer.try_cast<Guid>("6F9619FF-8B86-D011-B42D-00C04FC964FF"),
                GuidBad = SqlFunctions.SqlServer.try_cast<Guid>("x"),
                DateTime = SqlFunctions.SqlServer.try_cast<DateTime>("2024-01-02T03:04:05"),
                DateTimeBad = SqlFunctions.SqlServer.try_cast<DateTime>("x"),
                Offset = SqlFunctions.SqlServer.try_cast<DateTimeOffset>("2024-01-02T03:04:05+02:00"),
                OffsetBad = SqlFunctions.SqlServer.try_cast<DateTimeOffset>("x"),
                Time = SqlFunctions.SqlServer.try_cast<TimeSpan>("03:04:05"),
                TimeBad = SqlFunctions.SqlServer.try_cast<TimeSpan>("x"),
                Text = SqlFunctions.SqlServer.try_cast(x.Label),

                Converted = SqlFunctions.SqlServer.try_convert<int>("42"),
                ConvertedBad = SqlFunctions.SqlServer.try_convert<int>("x"),
                Styled = SqlFunctions.SqlServer.try_convert<DateTime>("2024-01-02T03:04:05", 126),
                StyledBad = SqlFunctions.SqlServer.try_convert<DateTime>(x.Label, 126),
                NullStyle = SqlFunctions.SqlServer.try_convert<int>("42", null),
                TextConverted = SqlFunctions.SqlServer.try_convert(x.Label)
            })
            .Single();

        row.Byte.Should().Be((byte)42);
        row.ByteBad.Should().BeNull("300 overflows tinyint");
        row.Short.Should().Be((short)42);
        row.ShortBad.Should().BeNull("70000 overflows smallint");
        row.Int.Should().Be(42);
        row.IntBad.Should().BeNull();
        row.Long.Should().Be(42L);
        row.LongBad.Should().BeNull();
        row.Single.Should().Be(1.5f);
        row.SingleBad.Should().BeNull();
        row.Double.Should().Be(1.5d);
        row.DoubleBad.Should().BeNull();
        row.Decimal.Should().Be(1.5m);
        row.DecimalBad.Should().BeNull();
        row.Bool.Should().BeTrue();
        row.BoolBad.Should().BeNull();
        row.Guid.Should().Be(Guid.Parse("6F9619FF-8B86-D011-B42D-00C04FC964FF"));
        row.GuidBad.Should().BeNull();
        row.DateTime.Should().Be(new DateTime(2024, 1, 2, 3, 4, 5));
        row.DateTimeBad.Should().BeNull();
        row.Offset.Should().Be(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)));
        row.OffsetBad.Should().BeNull();
        row.Time.Should().Be(new TimeSpan(3, 4, 5));
        row.TimeBad.Should().BeNull();
        row.Text.Should().Be("a1b2c3");

        row.Converted.Should().Be(42);
        row.ConvertedBad.Should().BeNull();
        row.Styled.Should().Be(new DateTime(2024, 1, 2, 3, 4, 5));
        row.StyledBad.Should().BeNull("label 1 is 'a1b2c3'");
        row.NullStyle.Should().BeNull(
            "a NULL style makes the native conversion return NULL even for a valid value (probe C4)");
        row.TextConverted.Should().Be("a1b2c3");

        ((object)row.Byte!).Should().BeOfType<byte>();
        ((object)row.Single!).Should().BeOfType<float>();
        ((object)row.Double!).Should().BeOfType<double>();
        ((object)row.Decimal!).Should().BeOfType<decimal>();
        ((object)row.Bool!).Should().BeOfType<bool>();
        ((object)row.Guid!).Should().BeOfType<Guid>();
        ((object)row.DateTime!).Should().BeOfType<DateTime>();
        ((object)row.Offset!).Should().BeOfType<DateTimeOffset>();
        ((object)row.Time!).Should().BeOfType<TimeSpan>();
        ((object)row.Text!).Should().BeOfType<string>();

        Report("T348LIVE-2025 try_cast 13/13 valid + 12/12 invalid=null try_convert 42/null/126/null-style-null outcome=ok");
    }

    [Fact]
    public void Product_ShouldExecuteInAggregateContextAndIgnoreNullRows()
    {
        using var ctx = new T348V17LiveContext(_connectionString);

        var rows = ctx.From<IT348U1Row>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { x.Grp, P = SqlFunctions.SqlServer.product(x.Value) })
            .ToList()
            .OrderBy(r => r.Grp)
            .ToList();

        rows.Should().HaveCount(3, "the fixture has three groups");
        rows[0].P.Should().Be(24, "2 * 3 * 4");
        rows[1].P.Should().Be(6, "the NULL row is ignored: 2 * 3");
        rows[2].P.Should().BeNull("a group with no non-null value has no product");
        ((object)rows[0].P!).Should().BeOfType<int>();

        Report($"T348LIVE-2025 product=24/6/null outcome=ok");
    }

    [Fact]
    public void RegexpSplitToTable_ShouldExecuteOnACompatibility170Database()
    {
        var admin = _connectionString;
        DropDatabase(admin, RegexDatabase);
        Execute(admin, $"create database [{RegexDatabase}]");
        Execute(admin, $"alter database [{RegexDatabase}] set compatibility_level = 170");

        var level = ScalarInt(admin, $"select compatibility_level from sys.databases where name = '{RegexDatabase}'");
        level.Should().Be(170, "the regexp table functions are unusable below compatibility level 170");

        T348V17LiveContext? regexContext = null;
        try
        {
            var databaseConnection = new SqlConnectionStringBuilder(admin) { InitialCatalog = RegexDatabase }.ConnectionString;
            regexContext = new T348V17LiveContext(databaseConnection);

            var rows = regexContext.From(() => SqlFunctions.SqlServer.regexp_split_to_table("a,b,c", ","))
                .Select(r => new { r.Value, r.Ordinal })
                .ToList()
                .OrderBy(r => r.Ordinal)
                .ToList();

            rows.Should().HaveCount(3);
            rows.Select(r => r.Value).Should().Equal("a", "b", "c");
            rows.Select(r => r.Ordinal).Should().Equal(1L, 2L, 3L);
            ((object)rows[0].Ordinal).Should().BeOfType<long>();

            var noMatch = regexContext.From(() => SqlFunctions.SqlServer.regexp_split_to_table("abc", ","))
                .Select(r => new { r.Value, r.Ordinal })
                .ToList();

            noMatch.Should().ContainSingle("a string without a delimiter is one fragment");
            noMatch[0].Value.Should().Be("abc");
            noMatch[0].Ordinal.Should().Be(1L);

            Report($"T348LIVE-2025 regexp_split_to_table compat={level} rows=3 no_match=1/null outcome=ok");
        }
        finally
        {
            regexContext?.Dispose();
            DropDatabase(admin, RegexDatabase);
        }
    }

    /// <summary>
    /// U3: the regex scalars execute on a major-17 engine at <b>both</b> compatibility levels (they are not
    /// compatibility-gated), over every shipped optional argument: start, occurrence, return option and
    /// match-type flags, plus the no-match, NULL-operand and supplied-NULL-argument behaviour observed on
    /// the engine.
    /// </summary>
    [Fact]
    public void RegexScalars_ShouldExecuteAtCompatibility160And170()
    {
        var admin = _connectionString;

        foreach (var (database, compat) in new[] { (RegexLowDatabase, 160), (RegexDatabase, 170) })
        {
            DropDatabase(admin, database);
            Execute(admin, $"create database [{database}]");
            Execute(admin, $"alter database [{database}] set compatibility_level = {compat}");

            var level = ScalarInt(admin, $"select compatibility_level from sys.databases where name = '{database}'");
            level.Should().Be(compat, "the scalar regex functions must run at both 160 and 170");

            var databaseConnection = new SqlConnectionStringBuilder(admin) { InitialCatalog = database }.ConnectionString;
            Execute(databaseConnection, FixtureDdl);

            try
            {
                using var ctx = new T348V17LiveContext(databaseConnection);

                var row = ctx.From<IT348U1Row>()
                    .Where(x => x.Id == 1)
                    .Select(x => new
                    {
                        Substr = SqlFunctions.SqlServer.regexp_substr("a1b2c3", "[0-9]+"),
                        SubstrStart = SqlFunctions.SqlServer.regexp_substr("a1b2c3", "[0-9]+", 4),
                        SubstrOccurrence = SqlFunctions.SqlServer.regexp_substr("a1b2c3", "[0-9]+", 1, 2),
                        SubstrFlag = SqlFunctions.SqlServer.regexp_substr("aAaA", "a", 1, 2, "i"),
                        SubstrNoMatch = SqlFunctions.SqlServer.regexp_substr("abc", "[0-9]+"),
                        SubstrNullInput = SqlFunctions.SqlServer.regexp_substr((string?)null, "[0-9]+"),
                        SubstrNullStart = SqlFunctions.SqlServer.regexp_substr("a1b2c3", "[0-9]+", null),
                        SubstrNullOccurrence = SqlFunctions.SqlServer.regexp_substr("a1b2c3", "[0-9]+", 1, null),
                        SubstrNullFlag = SqlFunctions.SqlServer.regexp_substr("a1b2c3", "[0-9]+", 1, 1, null),

                        Instr = SqlFunctions.SqlServer.regexp_instr("a1b2c3", "[0-9]+"),
                        InstrStart = SqlFunctions.SqlServer.regexp_instr("a1b2c3", "[0-9]+", 4),
                        InstrOccurrence = SqlFunctions.SqlServer.regexp_instr("a1b2c3", "[0-9]+", 1, 2),
                        InstrReturn = SqlFunctions.SqlServer.regexp_instr("a1b2c3", "[0-9]+", 1, 1, 1),
                        InstrFlag = SqlFunctions.SqlServer.regexp_instr("aAaA", "a", 1, 2, 0, "c"),
                        InstrNoMatch = SqlFunctions.SqlServer.regexp_instr("abc", "[0-9]+"),
                        InstrNullInput = SqlFunctions.SqlServer.regexp_instr((string?)null, "[0-9]+"),
                        InstrNullStart = SqlFunctions.SqlServer.regexp_instr("a1b2c3", "[0-9]+", null),
                        InstrNullReturn = SqlFunctions.SqlServer.regexp_instr("a1b2c3", "[0-9]+", 1, 1, null),
                        InstrNullFlag = SqlFunctions.SqlServer.regexp_instr("a1b2c3", "[0-9]+", 1, 1, 0, null)
                    })
                    .Single();

                row.Substr.Should().Be("1");
                row.SubstrStart.Should().Be("2", "start = 4 skips the first match at position 2");
                row.SubstrOccurrence.Should().Be("2", "the second match is returned");
                row.SubstrFlag.Should().Be("A", "the 'i' flag makes the second 'a' match 'A'");
                row.SubstrNoMatch.Should().BeNull("a no-match regexp_substr is NULL");
                row.SubstrNullInput.Should().BeNull();
                row.SubstrNullStart.Should().BeNull("an explicitly NULL start yields NULL");
                row.SubstrNullOccurrence.Should().BeNull("an explicitly NULL occurrence yields NULL");
                row.SubstrNullFlag.Should().BeNull("an explicitly NULL match type yields NULL");

                row.Instr.Should().Be(2);
                row.InstrStart.Should().Be(4);
                row.InstrOccurrence.Should().Be(4);
                row.InstrReturn.Should().Be(3, "return_option = 1 gives the position after the match");
                row.InstrFlag.Should().Be(3, "case-sensitive flags skip the 'A' at position 2");
                row.InstrNoMatch.Should().Be(0, "a no-match regexp_instr is 0, not NULL");
                row.InstrNullInput.Should().BeNull();
                row.InstrNullStart.Should().BeNull();
                row.InstrNullReturn.Should().Be(2, "a supplied NULL return option behaves like 0");
                row.InstrNullFlag.Should().BeNull();

                ((object)row.Substr!).Should().BeOfType<string>();
                ((object)row.Instr!).Should().BeOfType<int>();

                Report($"T348LIVE-2025 regex_scalars compat={level} substr=1/2/2/A/null/null/null/null instr=2/4/4/3/3/0/null/null/2/null outcome=ok");
            }
            finally
            {
                DropDatabase(admin, database);
            }
        }
    }

    /// <summary>
    /// U3: <c>regexp_matches</c> executes on the compatibility-170 database with the whole row shape
    /// (including the full <c>substring_matches</c> JSON text), the flag overload, no-match and NULL-input
    /// zero rows; its compatibility-level incompatibility is captured separately as an environment fact
    /// (Msg 208 at 160), not as an application refusal.
    /// </summary>
    [Fact]
    public void RegexpMatches_ShouldExecuteOnCompatibility170AndFailNativelyAt160()
    {
        var admin = _connectionString;
        DropDatabase(admin, RegexDatabase);
        Execute(admin, $"create database [{RegexDatabase}]");
        Execute(admin, $"alter database [{RegexDatabase}] set compatibility_level = 170");

        var level = ScalarInt(admin, $"select compatibility_level from sys.databases where name = '{RegexDatabase}'");
        level.Should().Be(170, "regexp_matches is unusable below compatibility level 170");

        T348V17LiveContext? regexContext = null;
        try
        {
            var databaseConnection = new SqlConnectionStringBuilder(admin) { InitialCatalog = RegexDatabase }.ConnectionString;
            regexContext = new T348V17LiveContext(databaseConnection);

            var rows = regexContext.From(() => SqlFunctions.SqlServer.regexp_matches("a1b2", "[0-9]"))
                .Select(r => new { r.MatchId, r.StartPosition, r.EndPosition, r.MatchValue, r.SubstringMatches })
                .ToList()
                .OrderBy(r => r.MatchId)
                .ToList();

            rows.Should().HaveCount(2);
            rows[0].MatchId.Should().Be(1L);
            rows[0].StartPosition.Should().Be(2);
            rows[0].EndPosition.Should().Be(2);
            rows[0].MatchValue.Should().Be("1");
            rows[0].SubstringMatches.Should().Be("[{\"value\":\"1\",\"start\":2,\"length\":1}]",
                "the full native JSON capture text is materialized, never cast or truncated");
            rows[1].MatchId.Should().Be(2L);
            rows[1].StartPosition.Should().Be(4);
            rows[1].EndPosition.Should().Be(4);
            rows[1].MatchValue.Should().Be("2");
            rows[1].SubstringMatches.Should().Be("[{\"value\":\"2\",\"start\":4,\"length\":1}]");

            ((object)rows[0].MatchId).Should().BeOfType<long>();
            ((object)rows[0].StartPosition).Should().BeOfType<int>();
            ((object)rows[0].EndPosition).Should().BeOfType<int>();
            ((object)rows[0].MatchValue!).Should().BeOfType<string>();
            ((object)rows[0].SubstringMatches!).Should().BeOfType<string>();

            var flagged = regexContext.From(() => SqlFunctions.SqlServer.regexp_matches("aAbB", "a", "i"))
                .Select(r => new { r.MatchId, r.MatchValue })
                .ToList()
                .OrderBy(r => r.MatchId)
                .ToList();

            flagged.Should().HaveCount(2);
            flagged.Select(r => r.MatchValue).Should().Equal("a", "A");

            regexContext.From(() => SqlFunctions.SqlServer.regexp_matches("abc", "[0-9]"))
                .Select(r => r.MatchValue)
                .ToList()
                .Should().BeEmpty("a no-match regexp_matches returns zero rows");

            regexContext.From(() => SqlFunctions.SqlServer.regexp_matches((string?)null, "a"))
                .Select(r => r.MatchValue)
                .ToList()
                .Should().BeEmpty("a NULL input returns zero rows");

            Report($"T348LIVE-2025 regexp_matches compat={level} rows=2/2/no_match=0/null=0 types=bigint/int/int/varchar/varchar substring_matches=full outcome=ok");
        }
        finally
        {
            regexContext?.Dispose();
            DropDatabase(admin, RegexDatabase);
        }

        // The incompatible environment is a prerequisite fact: at compatibility 160 the same call is
        // Msg 208. This is not an application-capability refusal and is kept out of the declared-16 gate.
        DropDatabase(admin, RegexLowDatabase);
        Execute(admin, $"create database [{RegexLowDatabase}]");
        Execute(admin, $"alter database [{RegexLowDatabase}] set compatibility_level = 160");
        try
        {
            var lowConnection = new SqlConnectionStringBuilder(admin) { InitialCatalog = RegexLowDatabase }.ConnectionString;
            AssertSqlError(lowConnection, "select * from regexp_matches('a1b2', '[0-9]')", 208);
            Report("T348LIVE-2025 regexp_matches compat=160 error=Msg208 outcome=environment_prerequisite");
        }
        finally
        {
            DropDatabase(admin, RegexLowDatabase);
        }
    }

    /// <summary>
    /// U3: the four fuzzy names execute on a dedicated database with the observed runtime prerequisites —
    /// compatibility 170, <c>PREVIEW_FEATURES = ON</c> and a Windows collation
    /// (<c>Latin1_General_100_CI_AS</c> as the database default) — asserting values, NULLs, CLR types and
    /// the bounded <c>edit_distance</c>. A <c>SQL_*</c> collation (Msg 9847) and a missing preview
    /// (Msg 195) are captured as environment facts.
    /// </summary>
    [Fact]
    public void FuzzyFamily_ShouldExecuteWithTheObservedPrerequisites()
    {
        var admin = _connectionString;

        DropDatabase(admin, FuzzyDatabase);
        Execute(admin, $"create database [{FuzzyDatabase}] collate {FuzzyCollation}");
        Execute(admin, $"alter database [{FuzzyDatabase}] set compatibility_level = 170");

        var fuzzyConnection = new SqlConnectionStringBuilder(admin) { InitialCatalog = FuzzyDatabase }.ConnectionString;
        Execute(fuzzyConnection, "alter database scoped configuration set preview_features = on");

        try
        {
            var level = ScalarInt(fuzzyConnection, "select compatibility_level from sys.databases where name = db_name()");
            level.Should().Be(170, "the fuzzy functions require database compatibility level 170");

            var collation = ScalarString(fuzzyConnection, "select collation_name from sys.databases where name = db_name()");
            collation.Should().Be(FuzzyCollation, "a Windows collation is required; SQL_* raises Msg 9847");

            var preview = ScalarInt(fuzzyConnection, "select cast(value as int) from sys.database_scoped_configurations where name = 'PREVIEW_FEATURES'");
            preview.Should().Be(1, "PREVIEW_FEATURES must be explicitly ON");

            var isDefault = ScalarInt(fuzzyConnection, "select is_value_default from sys.database_scoped_configurations where name = 'PREVIEW_FEATURES'");
            isDefault.Should().Be(0, "the fixture enabled preview features explicitly, not by default");

            var build = ScalarString(admin, "select cast(serverproperty('ProductVersion') as nvarchar(128))");
            build.Should().StartWith("17.0.", "the tested engine baseline is the major-17 image");
            _major.Should().Be(17);

            Execute(fuzzyConnection, FixtureDdl);
            using var ctx = new T348V17LiveContext(fuzzyConnection);

            var row = ctx.From<IT348U1Row>()
                .Where(x => x.Id == 1)
                .Select(x => new
                {
                    Distance = SqlFunctions.SqlServer.edit_distance("kitten", "sitting"),
                    BoundedOne = SqlFunctions.SqlServer.edit_distance("kitten", "sitting", 1),
                    BoundedTwo = SqlFunctions.SqlServer.edit_distance("kitten", "sitting", 2),
                    SameBounded = SqlFunctions.SqlServer.edit_distance("same", "same", 0),
                    NullMaximum = SqlFunctions.SqlServer.edit_distance("kitten", "sitting", null),
                    NullOperand = SqlFunctions.SqlServer.edit_distance("kitten", (string?)null),
                    ColumnDistance = SqlFunctions.SqlServer.edit_distance(x.Label, "kitten"),
                    Similarity = SqlFunctions.SqlServer.edit_distance_similarity("kitten", "sitting"),
                    JaroDistance = SqlFunctions.SqlServer.jaro_winkler_distance("martha", "marhta"),
                    JaroSimilarity = SqlFunctions.SqlServer.jaro_winkler_similarity("martha", "marhta")
                })
                .Single();

            row.Distance.Should().Be(3);
            row.BoundedOne.Should().Be(2, "the native cap returns maximum_distance + 1 when the distance exceeds it");
            row.BoundedTwo.Should().Be(3);
            row.SameBounded.Should().Be(0, "equal strings are 0 even with a 0 cap");
            row.NullMaximum.Should().BeNull("a supplied NULL maximum is NULL, not the omitted form");
            row.NullOperand.Should().BeNull();
            row.ColumnDistance.Should().Be(6, "edit_distance('a1b2c3', 'kitten') on the Windows-collated column");
            row.Similarity.Should().Be(57);
            row.JaroDistance.Should().BeApproximately(0.0388888888888862, 1e-12);
            row.JaroSimilarity.Should().Be(96);

            ((object)row.Distance!).Should().BeOfType<int>();
            ((object)row.Similarity!).Should().BeOfType<int>();
            ((object)row.JaroDistance!).Should().BeOfType<double>();
            ((object)row.JaroSimilarity!).Should().BeOfType<int>();

            var nullColumn = ctx.From<IT348U1Row>()
                .Where(x => x.Id == 3)
                .Select(x => SqlFunctions.SqlServer.edit_distance(x.Label, "kitten"))
                .Single();
            nullColumn.Should().BeNull("a NULL column operand yields NULL");

            AssertSqlError(fuzzyConnection,
                "select edit_distance('kitten' collate SQL_Latin1_General_CP1_CI_AS, 'sitting' collate SQL_Latin1_General_CP1_CI_AS) as d",
                9847);

            Report($"T348LIVE-2025 fuzzy db={FuzzyDatabase} collation={collation} compat={level} preview={preview} build={build} "
                + "values=3/2/3/0/null/null/6/57/0.0388888888888862/96 outcome=ok");

            // The preview prerequisite is a separate environment fact on a Windows-collated database.
            DropDatabase(admin, FuzzyNoPreviewDatabase);
            Execute(admin, $"create database [{FuzzyNoPreviewDatabase}] collate {FuzzyCollation}");
            Execute(admin, $"alter database [{FuzzyNoPreviewDatabase}] set compatibility_level = 170");
            try
            {
                var noPreview = new SqlConnectionStringBuilder(admin) { InitialCatalog = FuzzyNoPreviewDatabase }.ConnectionString;
                AssertSqlError(noPreview, "select edit_distance('kitten', 'sitting') as d", 195);
                Report("T348LIVE-2025 fuzzy preview=0 error=Msg195 outcome=environment_prerequisite");
            }
            finally
            {
                DropDatabase(admin, FuzzyNoPreviewDatabase);
            }
        }
        finally
        {
            DropDatabase(admin, FuzzyDatabase);
        }
    }

    [Fact]
    public void DeclaredSixteen_ShouldRefuseThe2025NamesBeforeSql_WhileLeftShiftStillExecutes()
    {
        using var ctx = new T348V16LiveContext(_connectionString);
        var counter = new CommandCounter();
        ctx.AddInterceptor(counter);

        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { C = SqlFunctions.SqlServer.regexp_count(x.Label, "[0-9]") }),
            "The REGEXP_COUNT function requires SQL Server major version 17 or later; declared major version: 16.");

        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .GroupBy(x => new { x.Grp })
                .Select(x => new { x.Grp, P = SqlFunctions.SqlServer.product(x.Value) }),
            "The PRODUCT function requires SQL Server major version 17 or later; declared major version: 16.");

        ShouldRefuse(ctx, ctx.From(() => SqlFunctions.SqlServer.regexp_split_to_table("a,b", ","))
                .Select(r => r.Value),
            "The REGEXP_SPLIT_TO_TABLE function requires SQL Server major version 17 or later; declared major version: 16.");

        // U3: the three regex (two scalars and the TVF) and the four fuzzy names refuse by name too.
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { S = SqlFunctions.SqlServer.regexp_substr(x.Label, "[0-9]") }),
            "The REGEXP_SUBSTR function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { I = SqlFunctions.SqlServer.regexp_instr(x.Label, "[0-9]") }),
            "The REGEXP_INSTR function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From(() => SqlFunctions.SqlServer.regexp_matches("a1", "[0-9]"))
                .Select(r => r.MatchValue),
            "The REGEXP_MATCHES function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { D = SqlFunctions.SqlServer.edit_distance(x.Label, "kitten") }),
            "The EDIT_DISTANCE function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { S = SqlFunctions.SqlServer.edit_distance_similarity(x.Label, "kitten") }),
            "The EDIT_DISTANCE_SIMILARITY function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { D = SqlFunctions.SqlServer.jaro_winkler_distance(x.Label, "martha") }),
            "The JARO_WINKLER_DISTANCE function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { S = SqlFunctions.SqlServer.jaro_winkler_similarity(x.Label, "martha") }),
            "The JARO_WINKLER_SIMILARITY function requires SQL Server major version 17 or later; declared major version: 16.");

        counter.Executions.Should().Be(0, "every refusal is raised before a command is dispatched");

        // Sibling positive control: the 2022 name is permitted on the same declared-16 context.
        var shifted = ctx.From<IT348U1Row>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.SqlServer.left_shift(x.Value, 1))
            .Single();

        shifted.Should().Be(4);
        counter.Executions.Should().BeGreaterThan(0, "the positive control executed a real command");

        Report($"T348LIVE-2025 declared16 refusals=10 executions_before_control=0 left_shift=4 executions_total={counter.Executions} outcome=ok");
    }

    private static void ShouldRefuse<T>(IDataContext ctx, QueryCommand<T> command, string message)
    {
        var act = () => ((DataContext)ctx).GetPreparedQueryCommand(
            command, createEnumerator: false, storeInCache: false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage(message);
    }

    private static int ReadMajor(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select cast(serverproperty('ProductMajorVersion') as int)";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static int ScalarInt(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string ScalarString(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>Asserts the engine's native error number, used for the prerequisite negatives.</summary>
    private static void AssertSqlError(string connectionString, string sql, int number)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var exception = FluentActions.Invoking(() => command.ExecuteScalar())
            .Should().Throw<SqlException>("the engine owns the native error")
            .Which;

        exception.Number.Should().Be(number);
    }

    private static void DropDatabase(string connectionString, string database)
        => Execute(connectionString,
            $"if db_id('{database}') is not null begin alter database [{database}] set single_user with rollback immediate; drop database [{database}]; end");

    private static void Report(string message) => T348Evidence.Write(message);

    /// <summary>Counts the commands that actually reach execution.</summary>
    private sealed class CommandCounter : IQueryInterceptor
    {
        private int _executions;

        public int Executions => Volatile.Read(ref _executions);

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _executions);
    }
}

/// <summary>Writes the T348 live rows to the console and, when set, to the evidence file.</summary>
internal static class T348Evidence
{
    internal const string Variable = "NEXTORM_T348_EVIDENCE";

    public static void Write(string message)
    {
        Console.WriteLine("[T348-sqlserver] " + message);

        var path = Environment.GetEnvironmentVariable(Variable);
        if (!string.IsNullOrEmpty(path))
            File.AppendAllText(path, message + Environment.NewLine);
    }
}
