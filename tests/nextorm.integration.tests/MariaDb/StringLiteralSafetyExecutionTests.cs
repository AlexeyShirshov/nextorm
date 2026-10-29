using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// #327 D8 (AC03/AC-R3/AC-R4, §8 MySQL-family live matrix): the MariaDB live string-literal corpus.
/// MariaDB does not derive <see cref="CommonTestSuite"/> (no MariaDB test provider is enrolled), so the
/// class owns its fixture and context. It runs the full corpus through inline, captured parameter,
/// INSERT/readback and UPDATE/readback in all four <c>sql_mode</c> configurations crossed with the
/// driver's <c>NoBackslashEscapes</c> option absent/false/true (12 cells), plus LIKE against a native
/// typed-parameter reference, and verifies the product never issues a session-mode <c>SET</c>.
/// <para>
/// Fail-closed: an unavailable container fails the test through the constructor assertion (the plan's
/// uncertainty register requires the targeted MariaDB fixture to fail rather than skip). A failure of
/// this class therefore never reads as a green MariaDB cell.
/// </para>
/// </summary>
public sealed class MariaDbStringLiteralSafetyExecutionTests : IDisposable
{
    private const string ProbeTable = "sls_maria_probe";

    private static long _probeId = DateTime.UtcNow.Ticks;
    private static int _probeMarker = Random.Shared.Next(1_000_000, 2_000_000);

    public MariaDbStringLiteralSafetyExecutionTests()
    {
        MariaDbContainer.IsAvailable.Should().BeTrue(
            MariaDbContainer.Failure
            ?? "MariaDB is not available; the targeted fixture is fail-closed and must not skip.");
    }

    public void Dispose()
    {
        // Each fact owns its context with `using`; nothing is retained across tests.
    }

    [Theory]
    [Trait("Issue", "327")]
    [InlineData("", "absent")]
    [InlineData("", "false")]
    [InlineData("", "true")]
    [InlineData("NO_BACKSLASH_ESCAPES", "absent")]
    [InlineData("NO_BACKSLASH_ESCAPES", "false")]
    [InlineData("NO_BACKSLASH_ESCAPES", "true")]
    [InlineData("ANSI_QUOTES", "absent")]
    [InlineData("ANSI_QUOTES", "false")]
    [InlineData("ANSI_QUOTES", "true")]
    [InlineData("NO_BACKSLASH_ESCAPES,ANSI_QUOTES", "absent")]
    [InlineData("NO_BACKSLASH_ESCAPES,ANSI_QUOTES", "false")]
    [InlineData("NO_BACKSLASH_ESCAPES,ANSI_QUOTES", "true")]
    public void MariaDb_LiteralSafety_ShouldRoundTripEveryCaseInEveryConfiguration(string sqlMode, string noBackslashEscapes)
    {
        using var ctx = CreateContext(noBackslashEscapes);
        var appliedMode = ConfigureSessionMode(ctx, sqlMode);
        RecreateProbeTable(ctx);
        var likeMarker = NextProbeMarker();
        var likeIds = SeedLikeRows(ctx, likeMarker);

        var config = $"[sql_mode='{sqlMode}' NoBackslashEscapes={noBackslashEscapes}]";

        foreach (var literal in StringLiteralSafetyCases.Strings)
        {
            AssertExact(Inline(ctx, literal.Value), literal.Value, $"{config} inline literal case '{literal.Id}'");
            AssertExact(Captured(ctx, literal.Value), literal.Value, $"{config} captured parameter case '{literal.Id}'");

            var insertId = NextProbeId();
            var insertMarker = NextProbeMarker();
            ctx.CreateInsertBuilder<IMariaDbStringLiteralProbe>()
                .Value(x => x.Id, insertId)
                .Value(x => x.Marker, insertMarker)
                .Value(x => x.Svalue, literal.Value)
                .Insert();
            AssertExact(Read(ctx, insertMarker), literal.Value, $"{config} INSERT/readback case '{literal.Id}'");

            var updateMarker = NextProbeMarker();
            ctx.CreateInsertBuilder<IMariaDbStringLiteralProbe>()
                .Value(x => x.Id, NextProbeId())
                .Value(x => x.Marker, updateMarker)
                .Value(x => x.Svalue, "before-" + literal.Id)
                .Insert();
            ctx.CreateUpdateBuilder<IMariaDbStringLiteralProbe>()
                .Set(x => x.Svalue, literal.Value)
                .Where(x => x.Marker == updateMarker)
                .Update();
            AssertExact(Read(ctx, updateMarker), literal.Value, $"{config} UPDATE/readback case '{literal.Id}'");
        }

        foreach (var single in StringLiteralSafetyCases.Chars)
        {
            var inline = InlineChar(ctx, single.Value);
            inline.Should().Be(single.Value, $"{config} inline char case '{single.Id}'");
            Encoding.UTF8.GetBytes(inline.ToString()).Should()
                .Equal(Encoding.UTF8.GetBytes(single.Value.ToString()), $"{config} inline char bytes case '{single.Id}'");

            var captured = CapturedChar(ctx, single.Value);
            captured.Should().Be(single.Value, $"{config} captured char case '{single.Id}'");
            Encoding.UTF8.GetBytes(captured.ToString()).Should()
                .Equal(Encoding.UTF8.GetBytes(single.Value.ToString()), $"{config} captured char bytes case '{single.Id}'");
        }

        foreach (var pattern in StringLiteralSafetyLike.Patterns)
        {
            var expected = pattern.Matches.Select(value => likeIds[value]).OrderBy(id => id).ToList();
            var inline = LikeInline(ctx, likeMarker, pattern);
            var captured = LikeCaptured(ctx, likeMarker, pattern);
            var reference = NativeLikeIds(ctx, likeMarker, pattern);

            reference.Should().Equal(expected, $"{config} the native typed-parameter reference for '{pattern.Id}'");
            inline.Should().Equal(reference, $"{config} inline LIKE pattern '{pattern.Id}'");
            captured.Should().Equal(reference, $"{config} captured LIKE pattern '{pattern.Id}'");
        }

        ReadSessionMode(ctx).Should().Be(appliedMode,
            $"{config} the product must not issue a session-mode SET during ORM operations");

        // External mode change after the commands above were prepared/cached on this connection: the
        // next execution must still be exact and must not reset the session mode.
        var alternateMode = sqlMode.Contains("ANSI_QUOTES", StringComparison.Ordinal)
            ? "NO_BACKSLASH_ESCAPES"
            : "ANSI_QUOTES";
        var changedMode = ConfigureSessionMode(ctx, alternateMode);
        foreach (var value in HostileSubset())
        {
            AssertExact(Inline(ctx, value), value, $"{config} post-mode-change inline value");
            AssertExact(Captured(ctx, value), value, $"{config} post-mode-change captured value");
        }

        ReadSessionMode(ctx).Should().Be(changedMode,
            $"{config} an external mode change is observed; the product must not issue its own SET");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void MariaDb_StringAggSeparator_ShouldJoinRowsWithTheLiteralCommaToken()
    {
        using var ctx = CreateContext("absent");
        RecreateProbeTable(ctx);
        var marker = NextProbeMarker();

        foreach (var value in new[] { "alpha", "beta" })
        {
            ctx.CreateInsertBuilder<IMariaDbStringLiteralProbe>()
                .Value(x => x.Id, NextProbeId())
                .Value(x => x.Marker, marker)
                .Value(x => x.Svalue, value)
                .Insert();
        }

        var concatenated = ctx.From<IMariaDbStringLiteralProbe>()
            .Where(x => x.Marker == marker)
            .Select(x => SqlFunctions.Sql.string_agg(x.Svalue, ","))
            .First();

        // GROUP_CONCAT's SEPARATOR grammar accepts only a bare text-literal token, so the product renders
        // the constant separator as ',': neither the charset-introduced hex value spelling nor an
        // introduced quoted literal is accepted there. Row order inside an unordered aggregate is
        // unspecified, so
        // either concatenation of the exact ordinal values is correct; the separator itself must be a
        // single comma and the payload must be byte-exact.
        concatenated.Should().BeOneOf("alpha,beta", "beta,alpha",
            "the SEPARATOR token must be accepted and join both rows");
        concatenated!.Count(c => c == ',').Should().Be(1, "the separator is exactly one comma");
        Encoding.UTF8.GetBytes(concatenated).Should().HaveCount(10,
            "'alpha' + ',' + 'beta' is ten UTF-8 bytes with no loss");
    }

    private static IEnumerable<string> HostileSubset()
    {
        string[] ids =
        [
            "empty", "plain", "apostrophe", "backslash-embedded", "backslash-apostrophe",
            "trailing-backslash", "injection", "u0001", "u001f", "nul-a-b", "emoji", "mixed-hostile",
        ];

        return ids.Select(id => StringLiteralSafetyCases.Strings.Single(c => c.Id == id).Value);
    }

    private static MariaDbDataContext CreateContext(string noBackslashEscapes)
    {
        var suffix = noBackslashEscapes switch
        {
            "absent" => string.Empty,
            "false" => ";NoBackslashEscapes=False",
            "true" => ";NoBackslashEscapes=True",
            _ => throw new ArgumentOutOfRangeException(nameof(noBackslashEscapes)),
        };

        return new MariaDbDataContext(MariaDbContainer.ConnectionString + suffix, new DataContextBuilder());
    }

    private static string ConfigureSessionMode(MariaDbDataContext context, string sqlMode)
    {
        Execute(context, $"set session sql_mode = '{sqlMode}'");
        return ReadSessionMode(context);
    }

    private static string ReadSessionMode(MariaDbDataContext context)
    {
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand("select @@SESSION.sql_mode");
        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();
        return Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture)!.Trim();
    }

    private static void Execute(MariaDbDataContext context, string sql)
    {
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private static void RecreateProbeTable(MariaDbDataContext context)
    {
        Execute(context, $"drop table if exists {ProbeTable}");
        Execute(context,
            $"create table {ProbeTable} (id bigint not null primary key, marker int not null, svalue longtext) character set utf8mb4");
        Execute(context, $"insert into {ProbeTable} (id, marker, svalue) values (1, 0, 'seed-row')");
    }

    private static string? Inline(MariaDbDataContext context, string? value)
    {
        var parameter = Expression.Parameter(typeof(IMariaDbStringLiteralProbe), "e");
        var projection = Expression.Lambda<Func<IMariaDbStringLiteralProbe, string?>>(
            Expression.Constant(value, typeof(string)), parameter);

        return context.From<IMariaDbStringLiteralProbe>().Limit(1).Select(projection).First();
    }

    private static string? Captured(MariaDbDataContext context, string? value)
    {
        var captured = value;
        return context.From<IMariaDbStringLiteralProbe>().Limit(1).Select(e => captured).First();
    }

    private static char InlineChar(MariaDbDataContext context, char value)
    {
        var parameter = Expression.Parameter(typeof(IMariaDbStringLiteralProbe), "e");
        var projection = Expression.Lambda<Func<IMariaDbStringLiteralProbe, char>>(
            Expression.Constant(value, typeof(char)), parameter);

        return context.From<IMariaDbStringLiteralProbe>().Limit(1).Select(projection).First();
    }

    private static char CapturedChar(MariaDbDataContext context, char value)
    {
        var captured = value;
        return context.From<IMariaDbStringLiteralProbe>().Limit(1).Select(e => captured).First();
    }

    private static string? Read(MariaDbDataContext context, int marker)
        => context.From<IMariaDbStringLiteralProbe>()
            .Where(x => x.Marker == marker)
            .Select(x => x.Svalue)
            .Single();

    private static Dictionary<string, long> SeedLikeRows(MariaDbDataContext context, int marker)
    {
        var ids = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (_, value) in StringLiteralSafetyLike.LikeRows)
        {
            var rowId = NextProbeId();
            ids.Add(value, rowId);
            context.CreateInsertBuilder<IMariaDbStringLiteralProbe>()
                .Value(x => x.Id, rowId)
                .Value(x => x.Marker, marker)
                .Value(x => x.Svalue, value)
                .Insert();
        }

        return ids;
    }

    private static List<long> LikeInline(MariaDbDataContext context, int marker, LikePatternCase pattern)
        => context.From<IMariaDbStringLiteralProbe>()
            .Where(StringLiteralSafetyLike.InlinePatternPredicate<IMariaDbStringLiteralProbe>(
                nameof(IMariaDbStringLiteralProbe.Marker), nameof(IMariaDbStringLiteralProbe.Svalue), marker, pattern.Pattern, pattern.ExplicitEscape))
            .Select(x => x.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

    private static List<long> LikeCaptured(MariaDbDataContext context, int marker, LikePatternCase pattern)
    {
        var captured = pattern.Pattern;

        return pattern.ExplicitEscape
            ? context.From<IMariaDbStringLiteralProbe>()
                .Where(x => x.Marker == marker && SqlFunctions.Sql.like(x.Svalue, captured, "\\"))
                .Select(x => x.Id)
                .ToList()
                .OrderBy(id => id)
                .ToList()
            : context.From<IMariaDbStringLiteralProbe>()
                .Where(x => x.Marker == marker && SqlFunctions.Sql.like(x.Svalue, captured))
                .Select(x => x.Id)
                .ToList()
                .OrderBy(id => id)
                .ToList();
    }

    private static List<long> NativeLikeIds(MariaDbDataContext context, int marker, LikePatternCase pattern)
    {
        // The driver's text escaping is governed by the connection's NoBackslashEscapes option, not by
        // the live session mode, so a text-bound pattern is not a stable oracle across this matrix. The
        // pattern travels as the mode-independent UTF-8 hex literal (the same representation the product
        // emits); the marker stays a typed parameter, and the comparison is still the server's native LIKE.
        var escapeClause = pattern.ExplicitEscape ? " escape _utf8mb4 X'5c'" : string.Empty;
        var patternHex = Convert.ToHexString(Encoding.UTF8.GetBytes(pattern.Pattern)).ToLowerInvariant();

        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(
            $"select id from {ProbeTable} where marker = @m and svalue like _utf8mb4 X'{patternHex}'{escapeClause}");

        var markerParameter = command.CreateParameter();
        markerParameter.ParameterName = "@m";
        markerParameter.Value = marker;
        command.Parameters.Add(markerParameter);

        var ids = new List<long>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));

        ids.Sort();
        return ids;
    }

    private static void AssertExact(string? actual, string expected, string because)
    {
        actual.Should().Be(expected, because);
        Encoding.UTF8.GetBytes(actual!).Should().Equal(Encoding.UTF8.GetBytes(expected), because);
    }

    private static long NextProbeId() => Interlocked.Increment(ref _probeId);

    private static int NextProbeMarker() => Interlocked.Increment(ref _probeMarker);
}
