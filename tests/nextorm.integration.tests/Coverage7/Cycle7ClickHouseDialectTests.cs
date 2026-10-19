using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse dialect branch coverage (cycle 7, cluster D2) executed against a live Testcontainers
/// server. Every fact owns the <c>ch7_*</c> objects created in the constructor and asserts the exact
/// values the server returns.
///
/// <para>
/// Targeted arms in <c>src/nextorm.clickhouse/ClickHouseDialect.cs</c>: <c>MakeDictionaryFunction</c>
/// (all six <c>dict*</c> spellings through <see cref="ClickHouseFunctions"/>), <c>MakeQualifiedTableName</c>
/// (database-only, schema-only, database+schema rejection, bare-name path), <c>MakeTrim</c>
/// (trimBoth/trimLeft/trimRight), <c>MakeOrdinal</c> (case-sensitive and case-insensitive),
/// <c>MakeRegexMatch</c>/<c>MakeRegexReplace</c> (with and without the inline <c>(?i)</c> flag) and
/// <c>MakeNow</c> (<c>now('UTC')</c>/<c>now()</c>). The fallback arms covered in the same class are
/// <c>MakeResultColumnType</c>/<c>MakeTypeName</c> (a <c>values()</c> row shape spanning every type
/// mapping plus a nullable property) and <c>WrapTableFunction</c> (<c>numbers_mt</c> and the seeded
/// <c>generateRandom</c> form).
/// </para>
///
/// <para>
/// Overlap avoided: <c>ClickHouse/ClickHouseDialectBranchCoverage3Tests</c> owns the aggregate/date-part
/// matrix, <c>ClickHouse/ClickHouseFunctionGapTests</c> and <c>ClickHouse/ClickHouseExtendedFunctionTests</c>
/// own the scalar-function and <c>now()</c>-as-a-function forms, and
/// <c>Coverage6/ClickHouseRenderingBranchCoverage6Tests</c> owns PREWHERE/FINAL/ARRAY JOIN/LIMIT BY/
/// SETTINGS/window rendering. This class adds the dictionary surface, table qualification, string
/// trim/ordinal/regex translation, <c>DateTime.UtcNow</c>/<c>DateTime.Now</c> translation and the
/// typed <c>values()</c>/table-function wrapper arms.
/// </para>
/// </summary>
[Collection("ClickHouse")]
public sealed class Cycle7ClickHouseDialectTests : ProviderTestSuite
{
    private const string TypeTuples =
        "(1, 2, 3, 4, 5, 1.5, 2.5, 3.5, 100, '2023-01-01 00:00:00', 'x', NULL)";

    public Cycle7ClickHouseDialectTests()
    {
        // The ClickHouse driver rejects multi-statement commands, so each DDL/DML statement runs on
        // its own connection command (same shape as ClickHouseRenderingBranchCoverage6Tests).
        foreach (var statement in Statements())
            Execute(statement);
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private IReadOnlyList<string> Statements()
    {
        var statements = new List<string>
        {
            "drop dictionary if exists ch7_dict",
            "drop database if exists ch7_aux",
            "drop table if exists ch7_marker",
            "drop table if exists ch7_dict_src",
            "drop table if exists ch7_qualified",
            "create database ch7_aux",
            """
            create table ch7_marker
            (
                id Int32,
                label String,
                padded String,
                grp Int32
            )
            engine = MergeTree order by id
            """,
            """
            insert into ch7_marker (id, label, padded, grp) values
                (1, 'alpha', '  alpha  ', 1),
                (2, 'beta', ' beta ', 1),
                (3, 'gamma', 'gamma', 2)
            """,
            """
            create table ch7_dict_src
            (
                id UInt64,
                parent UInt64,
                name String
            )
            engine = MergeTree order by id
            """,
            """
            insert into ch7_dict_src (id, parent, name) values
                (1, 0, 'root'),
                (2, 1, 'child'),
                (3, 2, 'leaf'),
                (4, 0, 'other')
            """,
            """
            create table ch7_qualified
            (
                id Int32,
                label String
            )
            engine = MergeTree order by id
            """,
            "insert into ch7_qualified (id, label) values (1, 'default')",
            """
            create table ch7_aux.ch7_qualified
            (
                id Int32,
                label String
            )
            engine = MergeTree order by id
            """,
            "insert into ch7_aux.ch7_qualified (id, label) values (1, 'aux')",
            DictionaryDdl(),
        };

        return statements;
    }

    /// <summary>
    /// The dictionary is a real server object read through a local <c>CLICKHOUSE</c> source. The source
    /// credentials are taken from the suite connection string (the container's <c>default</c> user is
    /// disabled, so the source cannot rely on the driver defaults); the native endpoint is the server's
    /// own <c>localhost:9000</c>, resolved inside the server that hosts the dictionary.
    /// </summary>
    private static string DictionaryDdl()
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = ClickHouseContainer.ConnectionString };
        var user = Setting(builder, "Username", "User ID", "User");
        var password = Setting(builder, "Password", "Pwd");

        return "create dictionary ch7_dict (id UInt64, parent UInt64 DEFAULT 0 HIERARCHICAL, name String DEFAULT '') "
            + "primary key id "
            + $"source(clickhouse(host 'localhost' port 9000 user '{Literal(user)}' password '{Literal(password)}' db 'default' table 'ch7_dict_src')) "
            + "layout(hashed()) lifetime(0)";
    }

    private static string Setting(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value))
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return string.Empty;
    }

    private static string Literal(string value) => value.Replace("'", "''");

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    // ---- V05: dictionary function spellings -------------------------------------------------

    /// <summary>Hit arm: <c>dictGet</c> returns the stored attribute for an existing key.</summary>
    [Fact]
    public void DictGet_ExistingKey_ShouldReturnTheAttribute()
    {
        var row = Context.From<Ch7Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Child = SqlFunctions.ClickHouse.dict_get<string, ulong>("ch7_dict", "name", 2ul),
                Root = SqlFunctions.ClickHouse.dict_get<string, ulong>("ch7_dict", "name", 1ul),
            })
            .Single();

        row.Child.Should().Be("child");
        row.Root.Should().Be("root");
    }

    /// <summary>
    /// Miss/default arm: <c>dictGetOrDefault</c> answers the fallback for an absent key and the stored
    /// value for a present one, so the default is not masking a blanket miss.
    /// </summary>
    [Fact]
    public void DictGetOrDefault_MissingKey_ShouldReturnTheDefault()
    {
        var row = Context.From<Ch7Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Missing = SqlFunctions.ClickHouse.dict_get_or_default<string, ulong>("ch7_dict", "name", 99ul, "n/a"),
                Present = SqlFunctions.ClickHouse.dict_get_or_default<string, ulong>("ch7_dict", "name", 3ul, "n/a"),
            })
            .Single();

        row.Missing.Should().Be("n/a");
        row.Present.Should().Be("leaf");
    }

    /// <summary><c>dictHas</c> reports presence and absence for the same dictionary.</summary>
    [Fact]
    public void DictHas_ShouldReportKeyPresence()
    {
        var row = Context.From<Ch7Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Present = SqlFunctions.ClickHouse.dict_has("ch7_dict", 2ul),
                Missing = SqlFunctions.ClickHouse.dict_has("ch7_dict", 99ul),
            })
            .Single();

        row.Present.Should().BeTrue();
        row.Missing.Should().BeFalse();
    }

    /// <summary><c>dictGetHierarchy</c> returns the key and its parents up to the stored root.</summary>
    [Fact]
    public void DictGetHierarchy_ShouldReturnTheParentChain()
    {
        var chain = Context.From<Ch7Marker>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.dict_get_hierarchy<long>("ch7_dict", 3L))
            .Single();

        chain.Should().Equal(3UL, 2UL, 1UL);
    }

    /// <summary><c>dictGetChildren</c> returns the direct children only; a leaf has none.</summary>
    [Fact]
    public void DictGetChildren_ShouldReturnTheDirectChildren()
    {
        var row = Context.From<Ch7Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                OfRoot = SqlFunctions.ClickHouse.dict_get_children<long>("ch7_dict", 1L),
                OfLeaf = SqlFunctions.ClickHouse.dict_get_children<long>("ch7_dict", 3L),
            })
            .Single();

        row.OfRoot.Should().Equal(2UL);
        row.OfLeaf.Should().BeEmpty();
    }

    /// <summary><c>dictIsIn</c> answers the ancestor relation, both positively and negatively.</summary>
    [Fact]
    public void DictIsIn_ShouldReportAncestry()
    {
        var row = Context.From<Ch7Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Descendant = SqlFunctions.ClickHouse.dict_is_in("ch7_dict", 3ul, 1ul),
                Unrelated = SqlFunctions.ClickHouse.dict_is_in("ch7_dict", 3ul, 4ul),
            })
            .Single();

        row.Descendant.Should().BeTrue();
        row.Unrelated.Should().BeFalse();
    }

    // ---- V06: table qualification and quoted identifiers -------------------------------------

    /// <summary>
    /// Database-only qualifier arm with backtick quoting. The same physical table name exists in the
    /// default database and in <c>ch7_aux</c>; the qualified read sees only the aux row and the plain
    /// read only the default row, so the two objects do not collide.
    /// </summary>
    [Fact]
    public void DatabaseQualifier_ShouldReadTheQualifiedObject()
    {
        var qualified = Context.From<Ch7Qualified>()
            .WithDatabase("ch7_aux")
            .WithQuotedIdentifiers()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Label })
            .ToList();

        var plain = Context.From<Ch7Qualified>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Label })
            .ToList();

        qualified.Should().ContainSingle();
        qualified[0].Label.Should().Be("aux");
        plain.Should().ContainSingle();
        plain[0].Label.Should().Be("default");
    }

    /// <summary>Schema-only qualifier arm: ClickHouse treats it as the same single database qualifier.</summary>
    [Fact]
    public void SchemaQualifier_ShouldResolveToTheSameDatabase()
    {
        var labels = Context.From<Ch7Qualified>()
            .WithSchema("ch7_aux")
            .Select(x => x.Label)
            .ToList();

        labels.Should().Equal("aux");
    }

    /// <summary>The database+schema combination is rejected because ClickHouse has one qualifier level.</summary>
    [Fact]
    public void DatabaseAndSchema_ShouldBeRejectedAsOneQualifier()
    {
        var act = () => Context.From<Ch7Qualified>()
            .WithDatabase("ch7_aux")
            .WithSchema("ch7_aux")
            .Select(x => x.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*same qualifier*");
    }

    /// <summary>
    /// Bare-name arm: a table-name override carries no qualifier, so the qualifier join falls through
    /// to the plain table name; the quoting wrapper still runs over it.
    /// </summary>
    [Fact]
    public void TableNameOnly_ShouldRenderTheBareQuotedName()
    {
        var labels = Context.From<Ch7Qualified>()
            .WithTableName("ch7_qualified")
            .WithQuotedIdentifiers()
            .Select(x => x.Label)
            .ToList();

        labels.Should().Equal("default");
    }

    // ---- V07: trim sides/characters and ordinal comparison -----------------------------------

    /// <summary><c>string.Trim()</c> maps to <c>trimBoth</c> and removes whitespace at both ends.</summary>
    [Fact]
    public void TrimBoth_ShouldRemoveWhitespaceAtBothEnds()
    {
        var values = Context.From<Ch7Marker>()
            .OrderBy(x => x.Id)
            .Select(x => x.Padded.Trim())
            .ToList();

        values.Should().Equal("alpha", "beta", "gamma");
    }

    /// <summary><c>string.TrimStart()</c> maps to <c>trimLeft</c> and keeps the trailing whitespace.</summary>
    [Fact]
    public void TrimStart_ShouldRemoveOnlyLeadingWhitespace()
    {
        var values = Context.From<Ch7Marker>()
            .OrderBy(x => x.Id)
            .Select(x => x.Padded.TrimStart())
            .ToList();

        values.Should().Equal("alpha  ", "beta ", "gamma");
    }

    /// <summary><c>string.TrimEnd()</c> maps to <c>trimRight</c> and keeps the leading whitespace.</summary>
    [Fact]
    public void TrimEnd_ShouldRemoveOnlyTrailingWhitespace()
    {
        var values = Context.From<Ch7Marker>()
            .OrderBy(x => x.Id)
            .Select(x => x.Padded.TrimEnd())
            .ToList();

        values.Should().Equal("  alpha", " beta", "gamma");
    }

    /// <summary>The native <c>trim*</c> functions trim an explicit character set, not only whitespace.</summary>
    [Fact]
    public void TrimCharacters_ShouldHonourTheExplicitCharacterSet()
    {
        var row = Context.From<Ch7Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Both = SqlFunctions.ClickHouse.trim_both(" - alpha - ", " -"),
                Left = SqlFunctions.ClickHouse.trim_left("--alpha--", "-"),
                Right = SqlFunctions.ClickHouse.trim_right("--alpha--", "-"),
            })
            .Single();

        row.Both.Should().Be("alpha");
        row.Left.Should().Be("alpha--");
        row.Right.Should().Be("--alpha");
    }

    /// <summary>
    /// <c>string.Equals</c> is ordinal in C#: the plain overload keeps the value and the
    /// <see cref="StringComparison.OrdinalIgnoreCase"/> overload lower-cases both operands
    /// (<c>MakeOrdinal</c> false/true arms). <c>IndexOf</c> with the same comparison proves the
    /// case-folded operand is still position-correct.
    /// </summary>
    [Fact]
    public void OrdinalComparison_ShouldBeCaseSensitiveAndIgnoreCaseAware()
    {
        var rows = Context.From<Ch7Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                Exact = x.Label.Equals("alpha"),
                IgnoreCase = x.Label.Equals("ALPHA", StringComparison.OrdinalIgnoreCase),
                // Read as a predicate: positionUTF8-based IndexOf materialises as Int64 on the
                // server, so only the comparison (not the raw int projection) is portable.
                Position = x.Label.IndexOf("LP", StringComparison.OrdinalIgnoreCase) > 0,
            })
            .ToList();

        rows.Select(r => r.Exact).Should().Equal(true, false, false);
        rows.Select(r => r.IgnoreCase).Should().Equal(true, false, false);
        rows.Select(r => r.Position).Should().Equal(true, false, false);
    }

    // ---- V08: regex match/replace and now -----------------------------------------------------

    /// <summary><c>Regex.IsMatch</c> without options maps to the case-sensitive <c>match</c>.</summary>
    [Fact]
    public void RegexMatch_CaseSensitive_ShouldMatchExactCase()
    {
        var values = Context.From<Ch7Marker>()
            .OrderBy(x => x.Id)
            .Select(x => Regex.IsMatch(x.Label, "^al"))
            .ToList();

        values.Should().Equal(true, false, false);
    }

    /// <summary><c>Regex.IsMatch</c> with <see cref="RegexOptions.IgnoreCase"/> prefixes <c>(?i)</c>.</summary>
    [Fact]
    public void RegexMatch_IgnoreCase_ShouldMatchEitherCase()
    {
        var values = Context.From<Ch7Marker>()
            .OrderBy(x => x.Id)
            .Select(x => Regex.IsMatch(x.Label, "^AL", RegexOptions.IgnoreCase))
            .ToList();

        values.Should().Equal(true, false, false);
    }

    /// <summary>
    /// <c>Regex.Replace</c> maps to <c>replaceRegexpAll</c>: every occurrence is replaced, not only the
    /// first, so "alpha" and "gamma" keep two replacements each.
    /// </summary>
    [Fact]
    public void RegexReplace_ShouldReplaceEveryOccurrence()
    {
        var values = Context.From<Ch7Marker>()
            .OrderBy(x => x.Id)
            .Select(x => Regex.Replace(x.Label, "a", "X"))
            .ToList();

        values.Should().Equal("XlphX", "betX", "gXmmX");
    }

    /// <summary><c>Regex.Replace</c> with <see cref="RegexOptions.IgnoreCase"/> folds the pattern before replacing.</summary>
    [Fact]
    public void RegexReplace_IgnoreCase_ShouldFoldThePattern()
    {
        var values = Context.From<Ch7Marker>()
            .OrderBy(x => x.Id)
            .Select(x => Regex.Replace(x.Label, "A", "Z", RegexOptions.IgnoreCase))
            .ToList();

        values.Should().Equal("ZlphZ", "betZ", "gZmmZ");
    }

    /// <summary>
    /// <see cref="DateTime.UtcNow"/> is rendered as <c>now('UTC')</c> and answered by the server; the
    /// value is checked against a tolerance window plus the ordering of two successive reads, never
    /// against an exact wall-clock instant.
    /// </summary>
    [Fact]
    public void DateTimeUtcNow_ShouldUseTheUtcServerClock()
    {
        var before = DateTime.UtcNow;
        var first = Context.From<Ch7Marker>()
            .Where(x => x.Id == 1)
            .Select(x => DateTime.UtcNow)
            .Single();
        var after = DateTime.UtcNow;

        first.Should().BeOnOrAfter(before.AddMinutes(-1));
        first.Should().BeOnOrBefore(after.AddMinutes(1));

        var second = Context.From<Ch7Marker>()
            .Where(x => x.Id == 2)
            .Select(x => DateTime.UtcNow)
            .Single();

        second.Should().BeOnOrAfter(first);
    }

    /// <summary>
    /// <see cref="DateTime.Now"/> is rendered as the server-timezone <c>now()</c>; the value is checked
    /// against the same tolerance window and must be monotonic across two reads.
    /// </summary>
    [Fact]
    public void DateTimeNow_ShouldUseTheServerTimezoneClock()
    {
        var before = DateTime.UtcNow;
        var first = Context.From<Ch7Marker>()
            .Where(x => x.Id == 1)
            .Select(x => DateTime.Now)
            .Single();
        var after = DateTime.UtcNow;

        // The server runs in UTC; the driver hands back a wall-clock DateTime whose Kind the
        // comparison deliberately ignores (a Kind-dependent conversion would shift by the client
        // offset). Only the tolerance window and monotonicity are asserted.
        first.Should().BeOnOrAfter(before.AddMinutes(-1));
        first.Should().BeOnOrBefore(after.AddMinutes(1));

        var second = Context.From<Ch7Marker>()
            .Where(x => x.Id == 2)
            .Select(x => DateTime.Now)
            .Single();

        second.Should().BeOnOrAfter(first);
    }

    // ---- fallback: result-column types and table-function wrapping ----------------------------

    /// <summary>
    /// One executed <c>values()</c> shape spans every <c>MakeTypeName</c> arm (UInt8/Int16/Int32/
    /// UInt32/Int64/Float32/Float64/Decimal/Int64-for-TimeSpan/DateTime64/String) plus a nullable
    /// property, so both <c>MakeResultColumnType</c> arms run. The server validates the rendered
    /// structure, and the projected columns prove the row materialized.
    /// </summary>
    [Fact]
    public void ValuesTableFunction_ShouldRenderEveryClrTypeName()
    {
        var rows = Context.FromTableFunction(() => SqlFunctions.ClickHouse.values<Ch7TypeRow>(TypeTuples))
            .Select(r => new { r.B, r.I, r.Txt, r.N })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].B.Should().Be((byte)1);
        rows[0].I.Should().Be(3);
        rows[0].Txt.Should().Be("x");
        rows[0].N.Should().BeNull();
    }

    /// <summary><c>numbers_mt</c> takes the multi-threaded wrapper arm of <c>WrapTableFunction</c>.</summary>
    [Fact]
    public void NumbersMtTableFunction_ShouldReturnTheGeneratedRows()
    {
        var values = Context.FromTableFunction(() => SqlFunctions.ClickHouse.numbers_mt(3))
            .Select(r => r.Value)
            .ToList();

        values.Should().BeEquivalentTo(new[] { 0L, 1L, 2L });
    }

    /// <summary>
    /// The seeded <c>generateRandom</c> form takes the argumented wrapper arm: the fixed structure is
    /// injected before the seed and the <c>id</c> column is narrowed to <c>Int64</c>.
    /// </summary>
    [Fact]
    public void GenerateRandomSeededTableFunction_ShouldReturnTheFixedShape()
    {
        var rows = Context.FromTableFunction(() => SqlFunctions.ClickHouse.generate_random(42L))
            .Page(2, 0)
            .Select(r => new { r.Id, r.Value, r.Name })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Name != null);
    }

    // ---- DML surfaces and dialect guards -----------------------------------------------------

    /// <summary>
    /// The ClickHouse DML heads executed end to end on the live server: a plain insert, the
    /// <c>ALTER TABLE ... UPDATE</c> mutation with its <c>SETTINGS mutations_sync = 1</c> suffix, the
    /// <c>ALTER TABLE ... DELETE</c> mutation with the same suffix, and the native <c>TRUNCATE TABLE</c>.
    /// Each step is read back, so the mutation visibility (not only the rendering) is asserted.
    /// </summary>
    [Fact]
    public void DmlSurfaces_ShouldApplyTheMutationHeads()
    {
        Context.CreateInsertBuilder<Ch7Marker>()
            .Values(new Ch7Marker { Id = 9, Label = "nine", Padded = " nine ", Grp = 9 })
            .Insert();

        Context.From<Ch7Marker>().Where(x => x.Id == 9).Select(x => x.Label).Single().Should().Be("nine");

        Context.CreateUpdateBuilder<Ch7Marker>()
            .Set(x => x.Label, "updated")
            .Where(x => x.Id == 9)
            .Update();

        Context.From<Ch7Marker>().Where(x => x.Id == 9).Select(x => x.Label).Single().Should().Be("updated");

        Context.CreateDeleteBuilder<Ch7Marker>()
            .Where(x => x.Id == 9)
            .Delete();

        Context.From<Ch7Marker>().Where(x => x.Id == 9).Select(x => x.Id).ToList().Should().BeEmpty();

        Context.CreateTruncateBuilder<Ch7Marker>().Execute();

        Context.From<Ch7Marker>().Select(x => x.Id).ToList().Should().BeEmpty();
    }

    /// <summary>
    /// <c>MakeCreateTableAsSelect</c> and the capability flags: the persistent materialisation renders the
    /// required <c>ENGINE = MergeTree ORDER BY tuple()</c> form, accepts <c>IF NOT EXISTS</c> on a repeat,
    /// and the temporary form is rejected because ClickHouse cannot express a temporary <c>AS SELECT</c>.
    /// </summary>
    [Fact]
    public void CreateTableAsSelect_ShouldUseTheEngineFormAndRejectTheTemporaryForm()
    {
        Execute("drop table if exists ch7_ctas");
        try
        {
            Context.From<Ch7Marker>()
                .Select(x => new { x.Id, x.Label })
                .ToTable("ch7_ctas", o => o.IfNotExists(true));

            var ids = Context.From("ch7_ctas").Select(t => t.GetInt32("id")).ToList();
            ids.Should().BeEquivalentTo(new[] { 1, 2, 3 });

            Context.From<Ch7Marker>()
                .Select(x => new { x.Id })
                .ToTable("ch7_ctas", o => o.IfNotExists(true));

            Context.From("ch7_ctas").Select(t => t.GetInt32("id")).ToList().Should().HaveCount(3);

            FluentActions.Invoking(() => Context.From<Ch7Marker>()
                    .Select(x => new { x.Id })
                    .ToTempTable("ch7_ctas_tmp"))
                .Should().Throw<NotSupportedException>();
        }
        finally
        {
            Execute("drop table if exists ch7_ctas");
        }
    }

    /// <summary>
    /// The unsupported date units and the out-of-range join kind/modifier throws, plus the plain string
    /// helpers. A live query then reads the same helpers back from the server.
    /// </summary>
    [Fact]
    public void DialectGuards_ShouldRejectUnsupportedUnitsAndNameTheJoinArms()
    {
        var dialect = (ClickHouseDialect)((DataContext)Context).Dialect;

        FluentActions.Invoking(() => dialect.MakeDateTrunc("century", "v"))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*ClickHouse dateTrunc does not support the 'century' field*");
        FluentActions.Invoking(() => dialect.MakeDateAdd("fortnight", "1", "v"))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*ClickHouse date_add does not support the 'fortnight' field*");
        FluentActions.Invoking(() => dialect.MakeJoinKeyword((JoinType)42, JoinStrictness.Default, false))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*join modifier cannot be applied to a 42 join*");
        FluentActions.Invoking(() => dialect.MakeJoinKeyword(JoinType.Inner, (JoinStrictness)42, false))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*join modifier is not supported*");

        dialect.MakeStringLength("label").Should().Be("lengthUTF8(label)");
        dialect.MakeRepeat("v", "3").Should().Be("repeat(v, 3)");

        var row = Context.From<Ch7Marker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Trunc = SqlFunctions.Sql.date_trunc("hour", new DateTime(2023, 5, 17, 13, 45, 30)),
                Decade = SqlFunctions.Sql.date_add("decade", 1, new DateTime(2023, 5, 17)),
                Position = x.Label.IndexOf("p", 2) > 0
            })
            .Single();

        row.Trunc.Should().Be(new DateTime(2023, 5, 17, 13, 0, 0));
        row.Decade.Should().Be(new DateTime(2033, 5, 17));
        row.Position.Should().BeTrue();
    }

    [SqlTable("ch7_marker")]
    public sealed class Ch7Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;

        [Column("padded")]
        public string Padded { get; set; } = string.Empty;

        [Column("grp")]
        public int Grp { get; set; }
    }

    [SqlTable("ch7_qualified")]
    public sealed class Ch7Qualified
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }

    public sealed class Ch7TypeRow
    {
        [Column("b")]
        public byte B { get; set; }

        [Column("s")]
        public short S { get; set; }

        [Column("i")]
        public int I { get; set; }

        [Column("u")]
        public uint U { get; set; }

        [Column("l")]
        public long L { get; set; }

        [Column("f")]
        public float F { get; set; }

        [Column("d")]
        public double D { get; set; }

        [Column("m")]
        public decimal M { get; set; }

        [Column("ts")]
        public TimeSpan Ts { get; set; }

        [Column("dto")]
        public DateTimeOffset Dto { get; set; }

        [Column("txt")]
        public string Txt { get; set; } = string.Empty;

        [Column("n")]
        public int? N { get; set; }
    }
}
