using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// MySQL dialect branch coverage (cycle 7, cluster D3) executed against a live Testcontainers server.
/// Every fact owns the <c>my7_*</c> objects created and seeded in the constructor and asserts the exact
/// rows the server returns.
///
/// <para>
/// Targeted arms in <c>src/nextorm.mysql/MySqlDialect.cs</c>: <c>MakeQualifiedTableName</c>
/// (database-only, schema-only, bare-name, database+schema rejection, linked-server rejection and the
/// quoted special-character name), <c>MakeFullText</c> (<c>contains</c> boolean mode and
/// <c>freetext</c> natural-language mode over a real <c>FULLTEXT</c> index), <c>MakeNow</c>
/// (<c>now()</c> versus <c>utc_timestamp()</c>, separated by a session time-zone offset) and the
/// <c>select</c>-recognizer pair <c>MatchesSelect</c>/<c>IsIdentifierChar</c> reached through the public
/// query-hint surface over CTEs (the only rendered form that puts tokens before the top-level
/// <c>select</c>). The fallback arms covered in the same class are <c>MakeGrouping</c>
/// (<c>with rollup</c>), <c>MakeDurationType</c> (<c>time</c> and <c>time(3)</c>),
/// <c>MakeRegexReplace</c>/<c>MakeRegexMatch</c> (both case arms) and <c>MakeIsJson</c> (predicate and
/// value context).
/// </para>
///
/// <para>
/// Overlap avoided: <c>MySql/MySqlFunctionExecutionTests</c> and <c>MySql/MySqlSpecificTests</c> own the
/// native scalar/date/format matrix and the optimizer-hint execution, <c>MySql/MySqlDialectBranchCoverage3Tests</c>
/// owns paging/join/user, and <c>CommonTestSuite.Duration</c> owns the native duration round trip. This
/// class adds the qualification, full-text, recognizer, grouping, duration-type, regex and JSON arms.
/// </para>
/// </summary>
[Collection("MySql")]
public sealed class Cycle7MySqlDialectTests : ProviderTestSuite
{
    public Cycle7MySqlDialectTests()
    {
        foreach (var statement in Statements())
            Execute(statement);
    }

    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private static IEnumerable<string> Statements()
    {
        yield return "drop table if exists my7_doc";
        yield return "drop table if exists my7_marker";
        yield return "drop table if exists my7_json";
        yield return "drop table if exists my7_qualified";
        yield return "drop table if exists `my7 odd-name`";
        yield return "drop table if exists my7_span";
        yield return "create table my7_marker (id int not null primary key, label varchar(100) not null, grp int null)";
        yield return "create table my7_doc (id int not null primary key, body text not null, fulltext key ft_my7_doc_body (body))";
        yield return "create table my7_json (id int not null primary key, payload varchar(100) not null)";
        yield return "create table my7_qualified (id int not null primary key, label varchar(100) not null)";
        yield return "create table `my7 odd-name` (id int not null primary key, label varchar(100) not null)";
        yield return "insert into my7_marker (id, label, grp) values (1, 'alpha', 1), (2, 'beta', 1), (3, 'gamma', 2), (4, 'delta', 2)";
        yield return "insert into my7_doc (id, body) values (1, 'the quick brown fox'), (2, 'lazy dogs sleep'), (3, 'quicksilver is a metal'), (4, 'quick quick quick')";
        yield return "insert into my7_json (id, payload) values (1, '{\"a\": 1}'), (2, 'not json')";
        yield return "insert into my7_qualified (id, label) values (1, 'default')";
        yield return "insert into `my7 odd-name` (id, label) values (3, 'odd')";
    }

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        return command.ExecuteScalar();
    }

    private string CurrentDatabase() => (string)Scalar("select database()")!;

    // ---- V09: qualified identifiers, the select recognizer and identifier characters ----------

    /// <summary>
    /// Database-qualifier arm: <c>WithDatabase</c> renders <c>database.table</c> and the live read hits a
    /// different database (<c>information_schema</c>) than the connection default. The unknown-database
    /// negative proves the qualifier is really emitted instead of being dropped.
    /// </summary>
    [Fact]
    public void DatabaseQualifier_ShouldReadAnotherDatabase()
    {
        var rows = Context.From<My7Catalog>()
            .WithDatabase("information_schema")
            .Where(x => x.Name == "my7_marker")
            .Select(x => new { x.Name, x.Kind })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Name.Should().Be("my7_marker");
        rows[0].Kind.Should().Be("BASE TABLE");

        FluentActions.Invoking(() => Context.From<My7Catalog>()
                .WithDatabase("my7_no_such_db")
                .Select(x => x.Name)
                .ToList())
            .Should().Throw<MySqlConnector.MySqlException>()
            .WithMessage("*denied*", "an unknown database yields no grant, which still proves the qualifier was emitted");
    }

    /// <summary>
    /// Schema-qualifier arm: MySQL treats the schema as the same single qualifier, so
    /// <c>WithSchema("information_schema")</c> reads the identical catalog rows; an absent schema is
    /// rejected by the server, which proves the qualifier reaches the SQL.
    /// </summary>
    [Fact]
    public void SchemaQualifier_ShouldResolveToTheSameQualifier()
    {
        var names = Context.From<My7Catalog>()
            .WithSchema("information_schema")
            .Where(x => x.Name == "my7_marker")
            .Select(x => x.Name)
            .ToList();

        names.Should().Equal("my7_marker");

        FluentActions.Invoking(() => Context.From<My7Catalog>()
                .WithSchema("my7_no_such_schema")
                .Select(x => x.Name)
                .ToList())
            .Should().Throw<MySqlConnector.MySqlException>()
            .WithMessage("*denied*", "an unknown schema yields no grant, which still proves the qualifier was emitted");
    }

    /// <summary>
    /// Bare-name arm: a table-name override without a qualifier renders the plain name, both plain and
    /// inside the backtick quoting wrapper, and reads the class-owned row.
    /// </summary>
    [Fact]
    public void TableNameOnly_ShouldRenderTheBareName()
    {
        var plain = Context.From<My7Qualified>()
            .WithTableName("my7_qualified")
            .Select(x => x.Label)
            .ToList();

        var quoted = Context.From<My7Qualified>()
            .WithTableName("my7_qualified")
            .WithQuotedIdentifiers()
            .Select(x => x.Label)
            .ToList();

        plain.Should().Equal("default");
        quoted.Should().Equal("default");
    }

    /// <summary>
    /// Rejection arm: MySQL has one qualifier level, so setting the database and the schema together is
    /// rejected before any SQL runs.
    /// </summary>
    [Fact]
    public void DatabaseAndSchema_ShouldBeRejectedAsOneQualifier()
    {
        var database = CurrentDatabase();

        FluentActions.Invoking(() => Context.From<My7Qualified>()
                .WithDatabase(database)
                .WithSchema(database)
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*same qualifier*");
    }

    /// <summary>
    /// Rejection arm: the linked-server guard of <c>MakeQualifiedTableName</c>. The public
    /// <c>WithServer</c> route never reaches it (<c>SqlSourceRenderer</c> rejects the override first), so
    /// the dialect guard is asserted through the public dialect instance; a live read follows to prove
    /// the context is usable.
    /// </summary>
    [Fact]
    public void ServerQualifier_ShouldBeRejectedOnBothRoutes()
    {
        var dialect = ((DataContext)Context).Dialect;

        FluentActions.Invoking(() => dialect.MakeQualifiedTableName("linked", null, null, "my7_qualified"))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*linked-server*");

        FluentActions.Invoking(() => Context.From<My7Qualified>()
                .WithServer("linked")
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*linked-server*");

        Context.From<My7Qualified>().Select(x => x.Label).ToList().Should().Equal("default");
    }

    /// <summary>
    /// Quoted-identifier arm: a table name with a space and a dash only parses when the backtick quoting
    /// wrapper runs; the unquoted form is a server syntax error, so the two arms cannot be confused.
    /// </summary>
    [Fact]
    public void QuotedSpecialCharacterName_ShouldReadTheRowAndFailUnquoted()
    {
        var labels = Context.From<My7Qualified>()
            .WithTableName("my7 odd-name")
            .WithQuotedIdentifiers()
            .Select(x => x.Label)
            .ToList();

        labels.Should().Equal("odd");

        FluentActions.Invoking(() => Context.From<My7Qualified>()
                .WithTableName("my7 odd-name")
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<MySqlConnector.MySqlException>()
            .WithMessage("*SQL syntax*");
    }

    /// <summary>
    /// Recognizer arm: the statement starts with the top-level <c>select</c> keyword (<c>index == 0</c>),
    /// so the hint is inserted directly after it and the query still returns every row.
    /// </summary>
    [Fact]
    public void QueryHint_OnTopLevelSelect_ShouldInsertAfterTheKeyword()
    {
        var ids = Context.From<My7Marker>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Hint("MAX_EXECUTION_TIME(1000)")
            .ToList();

        ids.Should().Equal(1, 2, 3, 4);
    }

    /// <summary>
    /// Recognizer arm: a CTE puts the name and the body before the top-level <c>select</c>. The CTE name
    /// <c>my7_select</c> exercises the identifier-character guard (an underscore before <c>select</c>),
    /// the body exercises the parenthesis depth (the inner <c>select</c> sits at depth 1 and is skipped)
    /// and the inlined literal <c>'select from here'</c> exercises the single-quote scan, which must skip
    /// the keyword inside the literal.
    /// </summary>
    [Fact]
    public void QueryHint_WithCteNamedAfterSelect_ShouldKeepTheCteIntact()
    {
        var cte = Context.From<My7Marker>()
            .Select(x => new { x.Id, V = SqlFunctions.Sql.left("select from here", 3) })
            .AsCte("my7_select");

        var rows = Context.From(cte)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.V })
            .Hint("MAX_EXECUTION_TIME(1000)")
            .ToList();

        rows.Select(r => r.Id).Should().Equal(1, 2, 3, 4);
        rows.Select(r => r.V).Should().OnlyContain(v => v == "sel");
    }

    /// <summary>
    /// Recognizer arm: a CTE whose name starts with <c>select</c> and continues with an identifier
    /// character (<c>selects</c>). The trailing-character guard keeps the hint out of the CTE name; a
    /// misplaced insertion would make the declaration unparsable.
    /// </summary>
    [Fact]
    public void QueryHint_WithSelectPrefixedCteName_ShouldKeepTheNameIntact()
    {
        var cte = Context.From<My7Marker>()
            .Select(x => new { x.Id })
            .AsCte("selects");

        var ids = Context.From(cte)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Hint("MAX_EXECUTION_TIME(1000)")
            .ToList();

        ids.Should().Equal(1, 2, 3, 4);
    }

    /// <summary>
    /// Recognizer arm: with quoting enabled the CTE body renders backticked identifiers, so the hint scan
    /// has to skip a quoted section before it reaches the top-level keyword. The rows still come back.
    /// </summary>
    [Fact]
    public void QueryHint_WithQuotedIdentifiers_ShouldSkipTheQuotedSections()
    {
        var cte = Context.From<My7Marker>()
            .Select(x => new { x.Id, x.Label })
            .AsCte("my7select");

        var rows = Context.From(cte)
            .WithQuotedIdentifiers()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Label })
            .Hint("MAX_EXECUTION_TIME(1000)")
            .ToList();

        rows.Select(r => r.Label).Should().Equal("alpha", "beta", "gamma", "delta");
    }

    // ---- V10: full text, now, grouping, duration type, regex and JSON ------------------------

    /// <summary>
    /// <c>contains</c> arm (boolean mode over a real <c>FULLTEXT</c> index). Two documents carry the word
    /// <c>quick</c>; the document with <c>quicksilver</c> must not match, which rules out a substring
    /// renderer, and an absent word yields no row.
    /// </summary>
    [Fact]
    public void FullTextContains_ShouldMatchWholeWordsOnly()
    {
        var hits = Context.From<My7Doc>()
            .Where(x => SqlFunctions.Sql.contains(x.Body, "quick"))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        hits.Should().Equal(1, 4);

        var misses = Context.From<My7Doc>()
            .Where(x => SqlFunctions.Sql.contains(x.Body, "zebra"))
            .Select(x => x.Id)
            .ToList();

        misses.Should().BeEmpty();
    }

    /// <summary>
    /// <c>freetext</c> arm (natural-language mode): the stored word matches, an absent word does not.
    /// </summary>
    [Fact]
    public void FullTextFreetext_ShouldMatchTheStoredWord()
    {
        var hits = Context.From<My7Doc>()
            .Where(x => SqlFunctions.Sql.freetext(x.Body, "brown"))
            .Select(x => x.Id)
            .ToList();

        hits.Should().Equal(1);

        var misses = Context.From<My7Doc>()
            .Where(x => SqlFunctions.Sql.freetext(x.Body, "zebra"))
            .Select(x => x.Id)
            .ToList();

        misses.Should().BeEmpty();
    }

    /// <summary>
    /// <c>MakeNow</c> arms: <c>DateTime.UtcNow</c> renders <c>utc_timestamp()</c> and <c>DateTime.Now</c>
    /// renders <c>now()</c>. The session time zone is moved to <c>+05:00</c> for the duration of the fact,
    /// so the two arms differ by exactly five hours; a dialect that mapped both to one function fails the
    /// offset assertion. Precision is asserted with a tolerance, never against an exact wall clock.
    /// </summary>
    [Fact]
    public void Now_ShouldUseTheSessionClockForNowAndUtcForUtcNow()
    {
        Execute("set time_zone = '+05:00'");
        try
        {
            var row = Context.From<My7Marker>()
                .Where(x => x.Id == 1)
                .Select(x => new { Local = DateTime.Now, Utc = DateTime.UtcNow })
                .Single();

            var clientUtc = DateTime.UtcNow;

            (row.Utc - clientUtc).Duration().Should().BeLessThan(TimeSpan.FromMinutes(5),
                "utc_timestamp() is the server's UTC clock");
            (row.Local - row.Utc).Duration().Should().BeCloseTo(TimeSpan.FromHours(5), TimeSpan.FromMinutes(5),
                "now() follows the +05:00 session time zone while utc_timestamp() does not");
        }
        finally
        {
            Execute("set time_zone = '+00:00'");
        }
    }

    /// <summary>
    /// <c>MakeGrouping</c> arms: <c>GroupByRollup</c> renders <c>with rollup</c> and adds the grand-total
    /// row (a NULL group with the full count), while the plain <c>GroupBy</c> arm returns only the two
    /// groups.
    /// </summary>
    [Fact]
    public void GroupByRollup_ShouldAddTheGrandTotalRow()
    {
        var rollup = Context.From<My7Marker>()
            .GroupByRollup(x => new { x.Grp })
            .Select(x => new { x.Grp, Count = SqlFunctions.Sql.count() })
            .ToList();

        rollup.Should().HaveCount(3);
        rollup.Single(r => r.Grp is null).Count.Should().Be(4);
        rollup.Where(r => r.Grp is not null).Should().OnlyContain(r => r.Count == 2);

        var plain = Context.From<My7Marker>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { x.Grp, Count = SqlFunctions.Sql.count() })
            .ToList();

        plain.Should().HaveCount(2);
        plain.Should().NotContain(r => r.Grp == null);
    }

    /// <summary>
    /// <c>MakeDurationType</c> arms: <c>precision == 0</c> renders <c>time</c> and a positive precision
    /// renders <c>time(p)</c>. Both columns are created from the dialect output, the server reports the
    /// resulting column types, and a whole-second value round-trips through both.
    /// </summary>
    [Fact]
    public void DurationType_ShouldRenderTimeWithAndWithoutPrecision()
    {
        var dialect = ((DataContext)Context).Dialect;

        dialect.MakeDurationType(null).Should().Be("time");
        dialect.MakeDurationType(null, 3).Should().Be("time(3)");

        Execute("drop table if exists my7_span");
        Execute($"create table my7_span (id int not null primary key, whole {dialect.MakeDurationType(null)}, frac {dialect.MakeDurationType(null, 3)})");
        try
        {
            Execute("insert into my7_span (id, whole, frac) values (1, '00:01:30', '00:01:30.000')");

            Scalar("select column_type from information_schema.columns where table_schema = database() and table_name = 'my7_span' and column_name = 'whole'")
                .Should().Be("time");
            Scalar("select column_type from information_schema.columns where table_schema = database() and table_name = 'my7_span' and column_name = 'frac'")
                .Should().Be("time(3)");

            var row = Context.From<My7Span>().Single();
            row.Whole.Should().Be(TimeSpan.FromSeconds(90));
            row.Frac.Should().Be(TimeSpan.FromSeconds(90));
        }
        finally
        {
            Execute("drop table if exists my7_span");
        }
    }

    /// <summary>
    /// <c>MakeRegexReplace</c>/<c>MakeRegexMatch</c> arms: case-sensitive and case-insensitive matching.
    /// The ignored-case predicate hits the row, the case-sensitive one does not, and the replacement
    /// rewrites both <c>a</c> occurrences.
    /// </summary>
    [Fact]
    public void RegexReplaceAndMatch_ShouldRespectCaseSensitivity()
    {
        var ignoredCase = Context.From<My7Marker>()
            .Where(x => Regex.IsMatch(x.Label, "^AL", RegexOptions.IgnoreCase))
            .Select(x => x.Id)
            .ToList();

        var exactCase = Context.From<My7Marker>()
            .Where(x => Regex.IsMatch(x.Label, "^AL"))
            .Select(x => x.Id)
            .ToList();

        ignoredCase.Should().Equal(new[] { 1 }, "the 'i' match type ignores case and 'alpha' starts with 'al'");
        exactCase.Should().BeEmpty("the 'c' match type is case-sensitive");

        var row = Context.From<My7Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                CaseSensitive = Regex.Replace(x.Label, "a+", "X"),
                IgnoringCase = Regex.Replace(x.Label, "A+", "X", RegexOptions.IgnoreCase),
            })
            .Single();

        row.CaseSensitive.Should().Be("XlphX");
        row.IgnoringCase.Should().Be("XlphX", "the ignored-case arm replaces the lowercase matches too");
    }

    /// <summary>
    /// <c>MakeIsJson</c> arms: the predicate context renders <c>(json_valid(...)) = 1</c> and filters out
    /// the invalid document, while the value context keeps the raw 0/1 result. Both a valid and an invalid
    /// payload are asserted, so a renderer that answers one constant cannot pass.
    /// </summary>
    [Fact]
    public void IsJson_ShouldSeparateValidFromInvalidText()
    {
        var valid = Context.From<My7Json>()
            .Where(x => SqlFunctions.SqlServer.isjson(x.Payload))
            .Select(x => x.Id)
            .ToList();

        valid.Should().Equal(1);

        var flags = Context.From<My7Json>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Valid = SqlFunctions.SqlServer.isjson(x.Payload) })
            .ToList();

        flags.Select(f => f.Valid).Should().Equal(true, false);
    }

    /// <summary>
    /// Combined qualification arm: a database qualifier plus the quoting wrapper. A qualified name that
    /// were quoted as one identifier (<c>`information_schema.tables`</c>) does not exist, so the live read
    /// proves the qualifier is split and each part quoted on its own.
    /// </summary>
    [Fact]
    public void QualifiedAndQuotedIdentifiers_ShouldQuoteEachPart()
    {
        var rows = Context.From<My7Catalog>()
            .WithDatabase("information_schema")
            .WithTableName("tables")
            .WithQuotedIdentifiers()
            .Where(x => x.Name == "my7_marker")
            .Select(x => new { x.Name, x.Kind })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Kind.Should().Be("BASE TABLE");
    }

    /// <summary>The class-owned marker table: text payload plus a nullable grouping column.</summary>
    [SqlTable("my7_marker")]
    public sealed class My7Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;

        [Column("grp")]
        public int? Grp { get; set; }
    }

    /// <summary>The full-text table: the <c>body</c> column carries the <c>FULLTEXT</c> index.</summary>
    [SqlTable("my7_doc")]
    public sealed class My7Doc
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("body")]
        public string Body { get; set; } = string.Empty;
    }

    /// <summary>The text-JSON table: one valid and one invalid payload.</summary>
    [SqlTable("my7_json")]
    public sealed class My7Json
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("payload")]
        public string Payload { get; set; } = string.Empty;
    }

    /// <summary>A single-row table read through the qualification and quoting overrides.</summary>
    [SqlTable("my7_qualified")]
    public sealed class My7Qualified
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>The server catalog, read cross-database through the <c>information_schema</c> qualifier.</summary>
    [SqlTable("tables")]
    public sealed class My7Catalog
    {
        [Column("table_name")]
        public string Name { get; set; } = string.Empty;

        [Column("table_type")]
        public string Kind { get; set; } = string.Empty;
    }

    /// <summary>The duration table: both columns are created from the dialect's type renderer.</summary>
    [SqlTable("my7_span")]
    public sealed class My7Span
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("whole")]
        public TimeSpan Whole { get; set; }

        [Column("frac")]
        public TimeSpan Frac { get; set; }
    }
}
