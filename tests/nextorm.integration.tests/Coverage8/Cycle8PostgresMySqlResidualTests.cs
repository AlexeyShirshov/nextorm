using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// PostgreSQL and MySQL residual dialect coverage (cycle 8, unit D6) executed against live
/// Testcontainers servers. The MySQL facts own the <c>c8m_*</c> tables created and seeded in the
/// constructor and read the shared suite context; every PostgreSQL fact creates its own context over
/// the <c>c8pg_marker</c> table, because one harness instance owns one provider. No fact substitutes
/// one provider's shape for the other: each asserts the exact values its own engine returns.
///
/// <para>
/// Targeted arms in <c>src/nextorm.postgres/PostgresDialect.cs</c>: <c>RenderQueryHints</c>
/// (:242-269 — the top-level <c>select</c> insertion plus the quoted-section/single-quote scan a CTE
/// prefix forces), <c>MakeMathFunction</c> (the one-argument <c>ln</c> fold and the floating-point
/// two-argument <c>round</c> cast, :499-516), <c>MakeDateAdd</c>'s <c>DateTimeOffset</c> branch and
/// its plain delegate (:413-416), <c>MakeDateDiffBig</c> (:422-423) and <c>MakeFullText</c>'s
/// <c>contains</c>/<c>freetext</c> pair (:431-434).
/// </para>
///
/// <para>
/// Targeted arms in <c>src/nextorm.mysql/MySqlDialect.cs</c>: <c>MakeDefaultValues</c> (:38, reached
/// through an all-generated entity) and <c>MakeLastInsertId</c> (:31, the <c>LAST_INSERT_ID</c>
/// read-back), the full <c>MakeDateAdd</c> field table incl. the decade/century/millennium year
/// scaling (:247-268), the <c>MakeDateAdd</c> <c>DateTimeOffset</c> scaling and its no-factor
/// fallback (:287-304), <c>MakeDateDiff</c>'s factor-1 and cast-to-signed arms (:309-326),
/// <c>MakeEndOfMonth</c> (:330), <c>MakeDateFromParts</c> (:333-334), <c>MakeTextJsonFunction</c>'s
/// <c>json_value</c>/<c>json_query</c>/<c>json_modify</c> arms (:217-222) and the reachable numeric
/// <c>MakeTypeName</c> arms (:386-393) through projection casts.
/// </para>
///
/// <para>
/// Recorded, never asserted: the <c>MakeDateAdd</c>/<c>MakeDateDiff</c> <c>_ =&gt; throw</c> arms
/// (:262, :321) are unreachable through the public surface because the translator validates the field
/// against <c>SupportsDateAddField</c>/<c>SupportsDateDiffField</c> first and rejects an unknown field
/// with <c>"'fortnight' is not a supported date_* field for this provider."</c> (the rejection itself
/// is asserted). The <c>uint</c> (and the non-numeric <c>string</c>/<c>bool</c>/<c>DateTime</c>/
/// <c>DateTimeOffset</c>/<c>TimeSpan</c>) <c>MakeTypeName</c> arms have no public conversion call
/// site: <c>TypeFacts.TryGetNumericConversion</c> only accepts a numeric CLR target and
/// <c>uint</c> is not in that set. The source-level <c>IndexHints</c> fallback (:531) is not needed
/// because every primary arm is reachable. No skip is declared and both containers are required.
/// </para>
/// </summary>
[Collection("MySql")]
public sealed class Cycle8PostgresMySqlResidualTests : ProviderTestSuite
{
    private static readonly DateTime PgTs = new(2023, 5, 17, 13, 45, 30);

    private static readonly DateTimeOffset PgDto = new DateTimeOffset(2023, 5, 17, 13, 45, 30, TimeSpan.Zero);

    private static readonly DateTime MyDt = new(2023, 1, 31, 10, 30, 15);

    private static readonly DateTime MyDto = new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(1_234_560);

    private const string PostgresDdl = """
        drop table if exists c8pg_marker;
        create table c8pg_marker
        (
            id int not null primary key,
            name text not null,
            grp int not null,
            num int not null,
            ratio double precision not null,
            ts timestamp not null,
            dto timestamptz not null,
            body text not null
        );
        insert into c8pg_marker (id, name, grp, num, ratio, ts, dto, body) values
            (1, 'alpha', 1, 10, 1.2345, '2023-05-17 13:45:30', '2023-05-17 13:45:30+00', 'the quick brown fox'),
            (2, 'beta', 1, 20, 2.5, '2023-05-17 13:45:30', '2023-05-17 13:45:30+00', 'lazy dogs sleep'),
            (3, 'gamma', 2, 30, -0.25, '2023-05-17 13:45:30', '2023-05-17 13:45:30+00', 'quicksilver is a metal'),
            (4, 'delta', 2, 40, 4.0, '2023-05-17 13:45:30', '2023-05-17 13:45:30+00', 'quick quick quick');
        """;

    public Cycle8PostgresMySqlResidualTests()
    {
        foreach (var statement in MySqlStatements())
            Execute(Context, statement);

        using var postgres = CreatePostgresContext();
        Execute(postgres, PostgresDdl);
    }

    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private static IDataContext CreatePostgresContext()
    {
        var provider = PostgresTestProvider.Instance;
        provider.IsAvailable.Should().BeTrue(
            $"provider '{provider.Name}' is unavailable ({provider.SkipReason}); the D6 PostgreSQL arms need a live PostgreSQL 17");

        return provider.CreateContext();
    }

    private static IEnumerable<string> MySqlStatements()
    {
        yield return "drop table if exists c8m_defaults";
        yield return "drop table if exists c8m_identity";
        yield return "drop table if exists c8m_row";
        yield return "drop table if exists c8m_json";
        yield return "create table c8m_defaults (id int not null auto_increment primary key, label varchar(50) not null default 'dflt-label', qty int not null default 7)";
        yield return "create table c8m_identity (id int not null auto_increment primary key, name varchar(50) not null)";
        yield return "create table c8m_row (id int not null primary key, dt datetime not null, dto datetime(6) not null, num int not null, big bigint not null)";
        yield return "create table c8m_json (id int not null primary key, payload varchar(200) not null)";
        yield return "insert into c8m_row (id, dt, dto, num, big) values (1, '2023-01-31 10:30:15', '2023-05-17 13:45:30.123456', 10, 10)";
        yield return "insert into c8m_json (id, payload) values (1, '{\"a\": 1, \"b\": {\"c\": \"x\"}, \"arr\": [1, 2]}'), (2, '{\"a\": 5}')";
    }

    private static void Execute(IDataContext context, string sql)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        return command.ExecuteScalar();
    }

    // ---- PostgreSQL: hints (:242-269) ---------------------------------------------------------

    /// <summary>
    /// <c>RenderQueryHints</c> arms: a top-level statement gets the <c>/*+ ... */</c> comment directly
    /// after <c>select</c>, and a CTE with a quoted name, an underscore before the keyword
    /// (<c>c8pg_select</c>), a nested parenthesised body and the <c>'select from here'</c> literal
    /// forces the quote/bracket scan arms. Both queries return the seeded rows, which proves the hint
    /// stayed a comment; a misplaced insertion would not parse.
    /// </summary>
    [Fact]
    public void PostgresHints_ShouldRenderOnTopLevelSelectAndCte()
    {
        using var context = CreatePostgresContext();

        var ids = context.From<C8pgRow>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Hint("SeqScan(c8pg_marker)")
            .ToList();

        ids.Should().Equal(1, 2, 3, 4);

        var cte = context.From<C8pgRow>()
            .Select(x => new { x.Id, V = SqlFunctions.Sql.left("select from here", 3) })
            .AsCte("c8pg_select");

        var rows = context.From(cte)
            .WithQuotedIdentifiers()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.V })
            .Hint("SeqScan(c8pg_select)")
            .ToList();

        rows.Select(r => r.Id).Should().Equal(1, 2, 3, 4);
        rows.Select(r => r.V).Should().OnlyContain(v => v == "sel");
    }

    // ---- PostgreSQL: math (:499-516) ---------------------------------------------------------

    /// <summary>
    /// <c>MakeMathFunction</c> arms: one-argument <c>Math.Log</c> folds onto <c>ln</c> (PostgreSQL's
    /// <c>log</c> is base 10), two-argument <c>Math.Round</c> on a double casts the first argument to
    /// <c>numeric</c> and <c>Math.Pow</c> takes the base implementation. The values are those of the
    /// stored row, not a constant.
    /// </summary>
    [Fact]
    public void PostgresMath_ShouldFloorTheLogAndCastTheDoubleRound()
    {
        using var context = CreatePostgresContext();

        var row = context.From<C8pgRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Log = Math.Log(x.Ratio),
                Rounded = Math.Round(x.Ratio, 2),
                Powered = Math.Pow(x.Num, 2),
            })
            .Single();

        row.Log.Should().BeApproximately(Math.Log(1.2345), 1e-12, "Math.Log maps to ln on PostgreSQL");
        row.Rounded.Should().Be(1.23, "round((ratio)::numeric, 2) keeps the stored 1.2345 at two places");
        row.Powered.Should().Be(100d, "pow(10, 2)");
    }

    // ---- PostgreSQL: date add/diff (:413-434) ------------------------------------------------

    /// <summary>
    /// Date arms: the plain <c>date_add</c> delegates to the ANSI interval form, a <c>DateTimeOffset</c>
    /// <c>AddDays</c>/<c>AddHours</c> takes the UTC-wall-time branch (:413-416) and
    /// <c>date_diff_big</c> widens the sub-day cast to <c>bigint</c> (:422-423). The DTO results are
    /// compared as UTC instants; a wall-time implementation would shift them by the session zone.
    /// </summary>
    [Fact]
    public void PostgresDateAddAndDiffBig_ShouldReturnTheExpectedInstants()
    {
        using var context = CreatePostgresContext();

        var row = context.From<C8pgRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Day = SqlFunctions.Sql.date_add("day", 1, x.Ts),
                Days = x.Dto.AddDays(1),
                Hours = x.Dto.AddHours(2),
                Millis = SqlFunctions.Sql.date_diff_big(
                    "milliseconds", x.Ts, SqlFunctions.Sql.date_add("milliseconds", 500, x.Ts)),
            })
            .Single();

        row.Day.Should().Be(PgTs.AddDays(1));
        row.Days.UtcDateTime.Should().Be(PgDto.AddDays(1).UtcDateTime);
        row.Hours.UtcDateTime.Should().Be(PgDto.AddHours(2).UtcDateTime);
        row.Millis.Should().Be(500L, "the bigint diff of a 500 ms span");
    }

    // ---- PostgreSQL: full text (:431-434) ----------------------------------------------------

    /// <summary>
    /// <c>MakeFullText</c> arms: <c>contains</c> renders <c>plainto_tsquery</c> and <c>freetext</c>
    /// renders <c>websearch_to_tsquery</c>. The whole-word match must not hit <c>quicksilver</c>, and
    /// an absent term yields no row.
    /// </summary>
    [Fact]
    public void PostgresFullText_ShouldMatchWholeTermsInBothModes()
    {
        using var context = CreatePostgresContext();

        var contains = context.From<C8pgRow>()
            .Where(x => SqlFunctions.Sql.contains(x.Body, "quick"))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        contains.Should().Equal(new[] { 1, 4 }, "plainto_tsquery matches the term 'quick', not 'quicksilver'");

        var misses = context.From<C8pgRow>()
            .Where(x => SqlFunctions.Sql.contains(x.Body, "zebra"))
            .Select(x => x.Id)
            .ToList();

        misses.Should().BeEmpty();

        var freeText = context.From<C8pgRow>()
            .Where(x => SqlFunctions.Sql.freetext(x.Body, "brown"))
            .Select(x => x.Id)
            .ToList();

        freeText.Should().Equal(1);
    }

    // ---- MySQL: default values (:38) and last insert id (:31) --------------------------------

    /// <summary>
    /// <c>MakeDefaultValues</c> arm: an entity whose every mapped column is generated has no writable
    /// column, so <c>Insert()</c> renders the empty column-list form and the server supplies the
    /// documented defaults. The row count and the stored defaults are both asserted.
    /// </summary>
    [Fact]
    public void MySqlDefaultValues_ShouldInsertOneRowOfDefaults()
    {
        var affected = Context.CreateInsertBuilder<C8mDefaultRow>().Insert();

        affected.Should().Be(1);
        Convert.ToInt64(Scalar("select count(*) from c8m_defaults where label = 'dflt-label' and qty = 7"))
            .Should().Be(1L, "the () values () insert wrote one row with the column defaults");
    }

    /// <summary>
    /// <c>MakeLastInsertId</c> arm: a MySQL insert has no <c>RETURNING</c>, so
    /// <c>ReturningIdentity</c> issues the separate <c>select last_insert_id()</c> and the key is read
    /// back from the target row.
    /// </summary>
    [Fact]
    public void MySqlReturningIdentity_ShouldReadBackTheGeneratedKey()
    {
        var id = Context.CreateInsertBuilder<C8mIdentityRow>()
            .Value(x => x.Name, "last-id")
            .ReturningIdentity(x => x.Id)
            .Single();

        id.Should().BeGreaterThan(0, "select last_insert_id() answers the generated identity");

        Context.From<C8mIdentityRow>()
            .Where(x => x.Id == id)
            .Select(x => x.Name)
            .Single()
            .Should().Be("last-id");
    }

    // ---- MySQL: date add/diff/from parts (:247-334) ------------------------------------------

    /// <summary>
    /// <c>MakeDateAdd</c> field table: the decade/century/millennium folds scale the year by
    /// 10/100/1000, the plain fields use their own unit. <c>MakeDateDiff</c>: <c>microseconds</c>
    /// keeps <c>timestampdiff</c> while <c>milliseconds</c> folds onto microseconds and casts the
    /// quotient to <c>signed</c>; whole <c>week</c>/<c>quarter</c>/<c>year</c> differ by the added
    /// amount. <c>MakeEndOfMonth</c> and <c>MakeDateFromParts</c> return the expected dates.
    /// </summary>
    [Fact]
    public void MySqlDateAddDiffAndParts_ShouldReturnTheExpectedValues()
    {
        var row = Context.From<C8mRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Decade = SqlFunctions.Sql.date_add("decade", 1, x.Dt),
                Century = SqlFunctions.Sql.date_add("century", 1, x.Dt),
                Millennium = SqlFunctions.Sql.date_add("millennium", 1, x.Dt),
                Micros = SqlFunctions.Sql.date_diff(
                    "microseconds", x.Dt, SqlFunctions.Sql.date_add("milliseconds", 500, x.Dt)),
                Millis = SqlFunctions.Sql.date_diff(
                    "milliseconds", x.Dt, SqlFunctions.Sql.date_add("milliseconds", 500, x.Dt)),
                Weeks = SqlFunctions.Sql.date_diff("week", x.Dt, SqlFunctions.Sql.date_add("week", 2, x.Dt)),
                Quarters = SqlFunctions.Sql.date_diff("quarter", x.Dt, SqlFunctions.Sql.date_add("quarter", 2, x.Dt)),
                Years = SqlFunctions.Sql.date_diff("year", x.Dt, SqlFunctions.Sql.date_add("year", 2, x.Dt)),
                FromParts = SqlFunctions.Sql.date_from_parts(2024, 2, 29),
                Eom = SqlFunctions.Sql.end_of_month(x.Dt),
            })
            .Single();

        row.Decade.Should().Be(MyDt.AddYears(10));
        row.Century.Should().Be(MyDt.AddYears(100));
        row.Millennium.Should().Be(MyDt.AddYears(1000));
        row.Micros.Should().Be(500_000);
        row.Millis.Should().Be(500, "the microsecond span is divided by 1000 and cast to signed");
        row.Weeks.Should().Be(2);
        row.Quarters.Should().Be(2);
        row.Years.Should().Be(2);
        row.FromParts.Should().Be(new DateTime(2024, 2, 29));
        row.Eom.Should().Be(new DateTime(2023, 1, 31));

        var addField = () => Context.From<C8mRow>()
            .Select(x => SqlFunctions.Sql.date_add("fortnight", 1, x.Dt))
            .ToList();
        addField.Should().Throw<NotSupportedException>().WithMessage("*fortnight*");

        var diffField = () => Context.From<C8mRow>()
            .Select(x => SqlFunctions.Sql.date_diff("fortnight", x.Dt, x.Dt))
            .ToList();
        diffField.Should().Throw<NotSupportedException>().WithMessage("*fortnight*");
    }

    /// <summary>
    /// <c>MakeDateAdd</c>'s <c>DateTimeOffset</c> overload (:287-304): <c>day</c> has no direct
    /// fractional unit, so it is scaled to hours; <c>month</c> is not in the factor table and falls
    /// back to the plain field rendering. MySQL stores a bare wall time, so the wall clock is what is
    /// asserted.
    /// </summary>
    [Fact]
    public void MySqlDateTimeOffsetAdd_ShouldScaleDaysAndFallBackForMonths()
    {
        var row = Context.From<C8mRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Days = x.Dto.AddDays(1),
                Months = x.Dto.AddMonths(1),
            })
            .Single();

        row.Days.DateTime.Should().Be(MyDto.AddDays(1));
        row.Months.DateTime.Should().Be(MyDto.AddMonths(1));
    }

    // ---- MySQL: text JSON (:217-222) ---------------------------------------------------------

    /// <summary>
    /// <c>MakeTextJsonFunction</c> arms: <c>json_value</c> unpacks a scalar through
    /// <c>json_unquote(json_extract(...))</c>, <c>json_query</c> keeps the fragment and
    /// <c>json_modify</c> maps onto <c>json_set</c> (insert-or-replace). The fragment is parsed rather
    /// than compared textually because MySQL owns its canonical spacing.
    /// </summary>
    [Fact]
    public void MySqlTextJson_ShouldReadFragmentAndApplyModification()
    {
        var row = Context.From<C8mJsonRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Scalar = SqlFunctions.SqlServer.json_value(x.Payload, "$.b.c"),
                Fragment = SqlFunctions.SqlServer.json_query(x.Payload, "$.arr"),
                Missing = SqlFunctions.SqlServer.json_value(x.Payload, "$.nope"),
                Modified = SqlFunctions.SqlServer.json_value(
                    SqlFunctions.SqlServer.json_modify(x.Payload, "$.a", "9"), "$.a"),
            })
            .Single();

        row.Scalar.Should().Be("x");
        row.Missing.Should().BeNull("an absent path yields SQL NULL");

        using var fragment = JsonDocument.Parse(row.Fragment!);
        fragment.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        fragment.RootElement.GetArrayLength().Should().Be(2);
        fragment.RootElement[0].GetInt32().Should().Be(1);
        fragment.RootElement[1].GetInt32().Should().Be(2);

        row.Modified.Should().Be("9", "json_set replaced $.a in the copy, leaving the stored row untouched");
    }

    // ---- MySQL: numeric MakeTypeName (:386-393) ----------------------------------------------

    /// <summary>
    /// Numeric <c>MakeTypeName</c> arms through projection casts: <c>byte</c> maps to
    /// <c>unsigned</c>, <c>short</c>/<c>int</c>/<c>long</c> to <c>signed</c>, <c>float</c>/<c>double</c>
    /// to their own names and <c>decimal</c> to <c>decimal</c>. Each cast is served by the server and
    /// materialised back into the CLR type, so the values must round-trip exactly.
    /// </summary>
    [Fact]
    public void MySqlNumericTypeNames_ShouldRoundTripEveryCast()
    {
        var row = Context.From<C8mRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                AsByte = (byte)x.Num,
                AsShort = (short)x.Num,
                AsInt = (int)x.Big,
                AsLong = (long)x.Num,
                AsFloat = (float)x.Num,
                AsDouble = (double)x.Num,
                AsDecimal = (decimal)x.Num,
            })
            .Single();

        row.AsByte.Should().Be((byte)10);
        row.AsShort.Should().Be((short)10);
        row.AsInt.Should().Be(10);
        row.AsLong.Should().Be(10L);
        row.AsFloat.Should().Be(10f);
        row.AsDouble.Should().Be(10d);
        row.AsDecimal.Should().Be(10m);
    }

    [SqlTable("c8pg_marker")]
    public sealed class C8pgRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("grp")]
        public int Grp { get; set; }

        [Column("num")]
        public int Num { get; set; }

        [Column("ratio")]
        public double Ratio { get; set; }

        [Column("ts")]
        public DateTime Ts { get; set; }

        [Column("dto")]
        public DateTimeOffset Dto { get; set; }

        [Column("body")]
        public string Body { get; set; } = string.Empty;
    }

    /// <summary>Only the generated column is mapped, so the entity has no writable column.</summary>
    [SqlTable("c8m_defaults")]
    public sealed class C8mDefaultRow
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
    }

    [SqlTable("c8m_identity")]
    public sealed class C8mIdentityRow
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    [SqlTable("c8m_row")]
    public sealed class C8mRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("dt")]
        public DateTime Dt { get; set; }

        [Column("dto")]
        public DateTimeOffset Dto { get; set; }

        [Column("num")]
        public int Num { get; set; }

        [Column("big")]
        public long Big { get; set; }
    }

    [SqlTable("c8m_json")]
    public sealed class C8mJsonRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("payload")]
        public string Payload { get; set; } = string.Empty;
    }
}
