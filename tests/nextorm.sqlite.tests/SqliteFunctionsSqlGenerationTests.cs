using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>SQL generation for the SQLite-only surface (<see cref="SqlFunctions.Sqlite"/>) without a database.</summary>
public class SqliteFunctionsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void CoreScalars_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            P = SqlFunctions.Sqlite.printf("%d", 1),
            F = SqlFunctions.Sqlite.format("%d", x.Id),
            H = SqlFunctions.Sqlite.hex(x.String),
            U = SqlFunctions.Sqlite.unhex("41"),
            Q = SqlFunctions.Sqlite.quote(x.String),
            T = SqlFunctions.Sqlite.@typeof(x.String),
            G = SqlFunctions.Sqlite.glob("a*", x.String),
            N = SqlFunctions.Sqlite.unicode(x.String),
            O = SqlFunctions.Sqlite.octet_length(x.String),
            Co = SqlFunctions.Sqlite.@char(65, 66),
            Ifn = SqlFunctions.Sqlite.ifnull(x.Int, 0)
        }));

        sql.Should().Contain("printf('%d', 1)");
        sql.Should().Contain("format('%d', id)");
        sql.Should().Contain("hex(somestring)");
        sql.Should().Contain("unhex('41')");
        sql.Should().Contain("quote(somestring)");
        sql.Should().Contain("typeof(somestring)");
        sql.Should().Contain("glob('a*', somestring)");
        sql.Should().Contain("unicode(somestring)");
        sql.Should().Contain("octet_length(somestring)");
        sql.Should().Contain("char(65, 66)");
        sql.Should().Contain("ifnull(nullableint, 0)");
    }

    [Fact]
    public void NoArgumentCoreScalars_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            R = SqlFunctions.Sqlite.random(),
            Rb = SqlFunctions.Sqlite.randomblob(4),
            I = SqlFunctions.Sqlite.@if(x.Int > 0, "yes", "no"),
            S = SqlFunctions.Sqlite.soundex(x.String)
        }));

        sql.Should().Contain("random()");
        sql.Should().Contain("randomblob(4)");
        sql.Should().Contain("if((nullableint > 0), 'yes', 'no')");
        sql.Should().Contain("soundex(somestring)");
    }

    [Fact]
    public void Math_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Sqlite.acos(0.5),
            Ah = SqlFunctions.Sqlite.acosh(1.5),
            As = SqlFunctions.Sqlite.asin(0.5),
            Ash = SqlFunctions.Sqlite.asinh(0.5),
            At = SqlFunctions.Sqlite.atan(0.5),
            At2 = SqlFunctions.Sqlite.atan2(1.0, 2.0),
            Ath = SqlFunctions.Sqlite.atanh(0.5),
            Csh = SqlFunctions.Sqlite.cosh(0.5),
            L10 = SqlFunctions.Sqlite.log10(100.0),
            L2 = SqlFunctions.Sqlite.log2(8.0),
            M = SqlFunctions.Sqlite.mod(5.0, 2.0),
            Sh = SqlFunctions.Sqlite.sinh(0.5),
            Th = SqlFunctions.Sqlite.tanh(0.5)
        }));

        sql.Should().Contain("acos(0.5)");
        sql.Should().Contain("acosh(1.5)");
        sql.Should().Contain("asin(0.5)");
        sql.Should().Contain("asinh(0.5)");
        sql.Should().Contain("atan(0.5)");
        sql.Should().Contain("atan2(1, 2)");
        sql.Should().Contain("atanh(0.5)");
        sql.Should().Contain("cosh(0.5)");
        sql.Should().Contain("log10(100)");
        sql.Should().Contain("log2(8)");
        sql.Should().Contain("mod(5, 2)");
        sql.Should().Contain("sinh(0.5)");
        sql.Should().Contain("tanh(0.5)");
    }

    [Fact]
    public void Dates_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            T = SqlFunctions.Sqlite.timediff(x.Datetime, x.Datetime),
            U = SqlFunctions.Sqlite.unixepoch(x.Datetime),
            J = SqlFunctions.Sqlite.julianday(x.Datetime)
        }));

        sql.Should().Contain("timediff(dt, dt)");
        sql.Should().Contain("unixepoch(dt)");
        sql.Should().Contain("julianday(dt)");
    }

    [Fact]
    public void JsonScalars_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Ex = SqlFunctions.Sqlite.json_extract<int>(x.String, "$.a"),
            G = SqlFunctions.Sqlite.json_get(x.String, "$.a"),
            Gt = SqlFunctions.Sqlite.json_get_text(x.String, "$.a"),
            J = SqlFunctions.Sqlite.json(x.String),
            Jb = SqlFunctions.Sqlite.jsonb(x.String),
            Ar = SqlFunctions.Sqlite.json_array(1, x.String),
            Ob = SqlFunctions.Sqlite.json_object("a", 1),
            Ins = SqlFunctions.Sqlite.json_insert(x.String, "$.a", 1),
            Rep = SqlFunctions.Sqlite.json_replace(x.String, "$.a", 1),
            Set = SqlFunctions.Sqlite.json_set(x.String, "$.a", 1),
            Ai = SqlFunctions.Sqlite.json_array_insert(x.String, "$.a[0]", 1),
            Pa = SqlFunctions.Sqlite.json_patch(x.String, "{}"),
            Pr = SqlFunctions.Sqlite.json_pretty(x.String),
            Qu = SqlFunctions.Sqlite.json_quote(x.String),
            Rm = SqlFunctions.Sqlite.json_remove(x.String, "$.a"),
            Ty = SqlFunctions.Sqlite.json_type(x.String, "$.a"),
            Va = SqlFunctions.Sqlite.json_valid(x.String)
        }));

        sql.Should().Contain("json_extract(somestring, '$.a')");
        sql.Should().Contain("(somestring -> '$.a')");
        sql.Should().Contain("(somestring ->> '$.a')");
        sql.Should().Contain("json(somestring)");
        sql.Should().Contain("jsonb(somestring)");
        sql.Should().Contain("json_array(1, somestring)");
        sql.Should().Contain("json_object('a', 1)");
        sql.Should().Contain("json_insert(somestring, '$.a', 1)");
        sql.Should().Contain("json_replace(somestring, '$.a', 1)");
        sql.Should().Contain("json_set(somestring, '$.a', 1)");
        sql.Should().Contain("json_array_insert(somestring, '$.a[0]', 1)");
        sql.Should().Contain("json_patch(somestring, '{}')");
        sql.Should().Contain("json_pretty(somestring)");
        sql.Should().Contain("json_quote(somestring)");
        sql.Should().Contain("json_remove(somestring, '$.a')");
        sql.Should().Contain("json_type(somestring, '$.a')");
        sql.Should().Contain("json_valid(somestring)");
    }

    [Fact]
    public void JsonAggregates_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Sqlite.json_group_array(x.String),
            O = SqlFunctions.Sqlite.json_group_object(x.Int, x.String)
        }));

        sql.Should().Contain("json_group_array(somestring)");
        sql.Should().Contain("json_group_object(nullableint, somestring)");
    }

    [Fact]
    public void JsonTableFunctions_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();

        var eachSql = SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Sqlite.json_each("[1,2,3]")).Select(r => r.Value));
        var treeSql = SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Sqlite.json_tree("[1,2,3]")).Select(r => r.Value));

        eachSql.Should().Contain("json_each('[1,2,3]')");
        treeSql.Should().Contain("json_tree('[1,2,3]')");
    }

    // ---------------------------------------------------------------------------------------------
    // FTS3/FTS4/FTS5 query surface (#181): exact native shape, argument order, table/column
    // identity, hidden columns and parameterisation.
    // ---------------------------------------------------------------------------------------------

    private static DbPreparedQueryCommand<T> Prepared<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_Match_ShouldEmitTableAndColumnMatch()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var prepared = Prepared(ctx, e.Select(x => new
        {
            Table = SqlFunctions.Sqlite.Match("docs_fts", "hello"),
            Column = SqlFunctions.Sqlite.Match(x.String, "hello")
        }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        // The constant table token is emitted as a quoted identifier (never parameterised), the mapped
        // column renders through the ordinary column pipeline, and the constant search text is bound.
        sql.Should().Contain("\"docs_fts\" MATCH $p0");
        sql.Should().Contain("somestring MATCH $p1");
        sql.Should().NotContain("'hello'", "the constant search text must be bound, not inlined as a raw literal");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Equal("hello", "hello");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_Bm25_ShouldEmitDefaultAndWeights()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            B = SqlFunctions.Sqlite.FTS5bm25("docs_fts"),
            W = SqlFunctions.Sqlite.FTS5bm25("docs_fts", 1, 2.5)
        }));

        sql.Should().Contain("bm25(\"docs_fts\")");
        sql.Should().Contain("bm25(\"docs_fts\", 1, 2.5)");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_Highlight_ShouldEmitNativeForm()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var prepared = Prepared(ctx, e.Select(x => new
        {
            H = SqlFunctions.Sqlite.Highlight("docs_fts", 0, "[", "]")
        }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain("highlight(\"docs_fts\", 0, $p0, $p1)",
            "the constant markers are bound, not inlined as raw SQL literals");
        sql.Should().NotContain("'['");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Equal("[", "]");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_Snippet_ShouldEmitNativeForm()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            S = SqlFunctions.Sqlite.Snippet("docs_fts", 0, "[", "]", "...", 8)
        }));

        sql.Should().Contain("snippet(\"docs_fts\", 0, $p0, $p1, $p2, 8)",
            "the constant markers are bound, not inlined as raw SQL literals");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_Rank_ShouldEmitHiddenRankColumn()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            R = SqlFunctions.Sqlite.Rank("docs_fts")
        }));

        sql.Should().Contain("\"docs_fts\".rank");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_MatchTable_ShouldEmitNativeFromSource()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<ISimpleEntity>("docs_fts", "hello"))
            .Select(r => r.Id));

        sql.Should().Contain("\"docs_fts\"('hello')");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_MatchTableFromSourceApostropheMatch_ShouldEscapeLiteralNotInject()
    {
        using var ctx = SqliteTestContext.Create();

        // The FROM match value is a constant built-in table-function argument; it must render as an
        // escaped SQL literal ('O''Brien'), never as an injectable 'O'Brien' fragment.
        var escaped = SqlOf(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<ISimpleEntity>("docs_fts", "O'Brien"))
            .Select(r => r.Id));
        escaped.Should().Contain("\"docs_fts\"('O''Brien')");
        escaped.Should().NotContain("'O'Brien'");

        var injected = SqlOf(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<ISimpleEntity>("docs_fts", "x'); drop table sentinel; --"))
            .Select(r => r.Id));
        injected.Should().Contain("\"docs_fts\"('x''); drop table sentinel; --')");
        injected.Should().NotContain("'x'); drop table sentinel; --'");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3_Rank_ShouldEmitRankUdfOverMatchInfo()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            R = SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("docs_fts"))
        }));

        sql.Should().Contain("rank(matchinfo(\"docs_fts\"))");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3_RowId_ShouldEmitHiddenRowIdColumn()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Rid = SqlFunctions.Sqlite.RowId("docs_fts")
        }));

        sql.Should().Contain("\"docs_fts\".rowid");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3_Offsets_ShouldEmitNativeForm()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            O = SqlFunctions.Sqlite.FTS3Offsets("docs_fts")
        }));

        sql.Should().Contain("offsets(\"docs_fts\")");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3_MatchInfo_ShouldEmitDefaultAndExplicitFormat()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var prepared = Prepared(ctx, e.Select(x => new
        {
            Default = SqlFunctions.Sqlite.FTS3MatchInfo("docs_fts"),
            Pcx = SqlFunctions.Sqlite.FTS3MatchInfo("docs_fts", "pcx")
        }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain("matchinfo(\"docs_fts\")");
        sql.Should().Contain("matchinfo(\"docs_fts\", $p0)",
            "the explicit matchinfo format is bound, not inlined as a raw literal");
        sql.Should().NotContain("'pcx'");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Equal("pcx");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3_Snippet_ShouldEmitDefaultAndFullOverloads()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var prepared = Prepared(ctx, e.Select(x => new
        {
            Default = SqlFunctions.Sqlite.FTS3Snippet("docs_fts"),
            Full = SqlFunctions.Sqlite.FTS3Snippet("docs_fts", "[", "]", "...", 0, 8)
        }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain("snippet(\"docs_fts\")");
        sql.Should().Contain("snippet(\"docs_fts\", $p0, $p1, $p2, 0, 8)",
            "the constant markers and ellipsis are bound while the integer indexes stay literals");
        sql.Should().NotContain("'...'");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Equal("[", "]", "...");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_Match_ShouldParameteriseQueryInWhere()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var query = "hello";

        var prepared = Prepared(ctx, e
            .Where(x => SqlFunctions.Sqlite.Match("docs_fts", query))
            .Select(x => new { x.Id }));

        prepared.DbCommand.CommandText.Should().Contain("\"docs_fts\" MATCH");
        prepared.DbCommand.CommandText.Should().NotContain("hello", "a non-constant FTS query must be bound, not inlined");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Contain("hello");
    }

    // ---------------------------------------------------------------------------------------------
    // Input guards (#181, E181-16): the FTS first operand must be a mapped column or a trusted
    // constant token, and a null query must not silently drop the filter.
    // ---------------------------------------------------------------------------------------------

    private sealed class TokenHolder
    {
        public string Value { get; set; } = "q";
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_Match_ShouldRejectNullEmptyComputedAndCapturedFirstOperand()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var holder = new TokenHolder();

        var nullConstant = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Match((string?)null, "q") }));
        var emptyConstant = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Match("", "q") }));
        var computed = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Match(x.Int + 1, "q") }));
        var captured = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Match(holder.Value, "q") }));

        nullConstant.Should().Throw<NotSupportedException>().WithMessage("*mapped column*constant non-empty*");
        emptyConstant.Should().Throw<NotSupportedException>();
        computed.Should().Throw<NotSupportedException>();
        captured.Should().Throw<NotSupportedException>();
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_AuxiliaryAndRank_ShouldRejectNullEmptyAndComputedTableToken()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var holder = new TokenHolder();

        var bm25Null = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS5bm25((string?)null) }));
        var bm25Empty = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS5bm25("") }));
        var highlightComputed = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Highlight(holder.Value, 0, "[", "]") }));
        var rankNull = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Rank((string?)null) }));

        bm25Null.Should().Throw<NotSupportedException>().WithMessage("*FTS5bm25*constant non-empty*");
        bm25Empty.Should().Throw<NotSupportedException>();
        highlightComputed.Should().Throw<NotSupportedException>();
        rankNull.Should().Throw<NotSupportedException>();
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_MatchTable_ShouldRejectNullAndEmptyTableToken()
    {
        using var ctx = SqliteTestContext.Create();

        var nullToken = () => SqlOf(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<ISimpleEntity>((string?)null, "hello"))
            .Select(r => r.Id));
        var emptyToken = () => SqlOf(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<ISimpleEntity>("", "hello"))
            .Select(r => r.Id));

        nullToken.Should().Throw<NotSupportedException>().WithMessage("*verbatim table-function argument*constant string*");
        emptyToken.Should().Throw<NotSupportedException>().WithMessage("*non-empty constant table token*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_Match_ShouldPreserveNullQueryPredicate()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.Sqlite.Match(x.String, (string?)null))
            .Select(x => x.Id));

        // A null FTS query is inherited by the native statement (MATCH null), never silently dropped.
        sql.Should().Contain("MATCH null");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_Match_ShouldKeepTableTokenAndMappedColumnOperandsValid()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var prepared = Prepared(ctx, e.Select(x => new
        {
            Table = SqlFunctions.Sqlite.Match("docs_fts", "hello"),
            Column = SqlFunctions.Sqlite.Match(x.String, "hello")
        }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain("\"docs_fts\" MATCH $p0");
        sql.Should().Contain("somestring MATCH $p1");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Equal("hello", "hello");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_Match_AdversarialToken_ShouldBeOneEscapedIdentifierNotRawSql()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var prepared = Prepared(ctx, e.Select(x => new
        {
            V = SqlFunctions.Sqlite.Match("a\"; DROP TABLE sentinel; --", "q")
        }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain("\"a\"\"; DROP TABLE sentinel; --\" MATCH $p0",
            "the whole token must be one escaped identifier");
        sql.Should().NotContain("DROP TABLE sentinel; -- MATCH", "the raw text must not become SQL");
        sql.Should().NotContain("'q'", "the adversarial query must be bound, not inlined");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Equal("q");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_ApostropheConstants_ShouldBindNotInlineBrokenLiterals()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // An apostrophe-bearing search query must be bound; inlining it as a raw literal would both
        // break the statement and let the value escape the literal.
        var match = Prepared(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Match("docs_fts", "O'Brien") }));
        var matchSql = Normalize(match.DbCommand.CommandText);
        matchSql.Should().Contain("\"docs_fts\" MATCH $p0");
        matchSql.Should().NotContain("'O'Brien'", "the apostrophe-bearing query must be bound, not inlined");
        match.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Equal("O'Brien");

        // The same holds for the marker/format value positions.
        var marker = Prepared(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Highlight("docs_fts", 0, "O'Brien", "]") }));
        var markerSql = Normalize(marker.DbCommand.CommandText);
        markerSql.Should().Contain("highlight(\"docs_fts\", 0, $p0, $p1)");
        markerSql.Should().NotContain("'O'Brien'");
        marker.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Equal("O'Brien", "]");

        var format = Prepared(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS3MatchInfo("docs_fts", "p'cx") }));
        var formatSql = Normalize(format.DbCommand.CommandText);
        formatSql.Should().Contain("matchinfo(\"docs_fts\", $p0)");
        formatSql.Should().NotContain("'p'cx'");
        format.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Equal("p'cx");
    }

    // ---------------------------------------------------------------------------------------------
    // Branch evidence (#181, E181-19): the reachable outcomes of WrapTableFunction and the renderer
    // fallback are asserted directly (not by coverage hits alone).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Issue", "181")]
    public void WrapTableFunction_Fts5_ShouldQuoteTokenRejectMalformedAndPassThroughOthers()
    {
        var dialect = NextORM.Sqlite.SqliteDialect.Instance;

        // The table token arrives as its own structured argument (never reparsed from the call text):
        // it is echoed as one quoted identifier while the query argument passes through unchanged.
        dialect.WrapTableFunction("fts5", "fts5(docs_fts, $p0)", new[] { "docs_fts", "$p0" })
            .Should().Be("\"docs_fts\"($p0)");
        dialect.WrapTableFunction("fts5", "fts5(ignored, ignored)", new[] { "a\"; DROP TABLE sentinel; --", "$p0" })
            .Should().Be("\"a\"\"; DROP TABLE sentinel; --\"($p0)",
                "the whole adversarial token is one escaped identifier, never reparsed as SQL");
        dialect.WrapTableFunction("json_each", "json_each('[1]')", new[] { "'[1]'" })
            .Should().Be("json_each('[1]')", "non-FTS table functions pass through unchanged");

        var tooFew = () => dialect.WrapTableFunction("fts5", "fts5(x)", new[] { "docs_fts" });
        var emptyTable = () => dialect.WrapTableFunction("fts5", "fts5(x)", new[] { "", "$p0" });

        tooFew.Should().Throw<NotSupportedException>().WithMessage("*requires a table token and a query*");
        emptyTable.Should().Throw<NotSupportedException>().WithMessage("*non-empty constant table token*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void SqliteFunctionRenderer_UnsupportedName_ShouldThrowInsteadOfEmittingRawSql()
    {
        var functions = NextORM.Sqlite.SqliteDialect.Instance.SqliteFunctions!;

        functions.Supports("bogus").Should().BeFalse();

        var act = () => functions.Render("bogus", Array.Empty<string>());
        act.Should().Throw<NotSupportedException>().WithMessage("*bogus*not supported by SQLite*");
    }
}
