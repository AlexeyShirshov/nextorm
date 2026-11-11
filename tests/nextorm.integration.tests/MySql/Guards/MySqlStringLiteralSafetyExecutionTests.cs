using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using NextORM.MySql;

namespace NextORM.Integration.Tests;

/// <summary>
/// #327 D8 (AC03/AC-R2, §8 MySQL live matrix): MySQL's own live corpus, independent of the shared
/// <see cref="CommonTestSuite"/> class, so the four <c>sql_mode</c> configurations crossed with the
/// driver's <c>NoBackslashEscapes</c> option absent/false/true (12 cells) are all exercised. Every
/// cell runs the corpus through inline, captured parameter, INSERT/readback and UPDATE/readback, LIKE
/// against a native typed-parameter reference, and verifies the product never issues a session-mode
/// <c>SET</c>. The class is fail-closed when the container is unavailable.
/// </summary>
public sealed class MySqlStringLiteralSafetyExecutionTests : IDisposable
{
    private const string ProbeTable = "sls_mysql_probe";

    private static long _probeId = DateTime.UtcNow.Ticks;
    private static int _probeMarker = Random.Shared.Next(2_000_000, 3_000_000);

    public MySqlStringLiteralSafetyExecutionTests()
    {
        MySqlContainer.IsAvailable.Should().BeTrue(
            MySqlContainer.Failure
            ?? "MySQL is not available; the targeted fixture is fail-closed and must not skip.");
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
    public void MySql_LiteralSafety_ShouldRoundTripEveryCaseInEveryConfiguration(string sqlMode, string noBackslashEscapes)
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
            ctx.CreateInsertBuilder<IMySqlStringLiteralProbe>()
                .Value(x => x.Id, insertId)
                .Value(x => x.Marker, insertMarker)
                .Value(x => x.Svalue, literal.Value)
                .Insert();
            AssertExact(Read(ctx, insertMarker), literal.Value, $"{config} INSERT/readback case '{literal.Id}'");

            var updateMarker = NextProbeMarker();
            ctx.CreateInsertBuilder<IMySqlStringLiteralProbe>()
                .Value(x => x.Id, NextProbeId())
                .Value(x => x.Marker, updateMarker)
                .Value(x => x.Svalue, "before-" + literal.Id)
                .Insert();
            ctx.CreateUpdateBuilder<IMySqlStringLiteralProbe>()
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

    private static IEnumerable<string> HostileSubset()
    {
        string[] ids =
        [
            "empty", "plain", "apostrophe", "backslash-embedded", "backslash-apostrophe",
            "trailing-backslash", "injection", "u0001", "u001f", "nul-a-b", "emoji", "mixed-hostile",
        ];

        return ids.Select(id => StringLiteralSafetyCases.Strings.Single(c => c.Id == id).Value);
    }

    private static MySqlDataContext CreateContext(string noBackslashEscapes)
    {
        var suffix = noBackslashEscapes switch
        {
            "absent" => string.Empty,
            "false" => ";NoBackslashEscapes=False",
            "true" => ";NoBackslashEscapes=True",
            _ => throw new ArgumentOutOfRangeException(nameof(noBackslashEscapes)),
        };

        return new MySqlDataContext(MySqlContainer.ConnectionString + suffix, new DataContextBuilder());
    }

    private static string ConfigureSessionMode(MySqlDataContext context, string sqlMode)
    {
        Execute(context, $"set session sql_mode = '{sqlMode}'");
        return ReadSessionMode(context);
    }

    private static string ReadSessionMode(MySqlDataContext context)
    {
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand("select @@SESSION.sql_mode");
        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();
        return Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture)!.Trim();
    }

    private static void Execute(MySqlDataContext context, string sql)
    {
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private static void RecreateProbeTable(MySqlDataContext context)
    {
        Execute(context, $"drop table if exists {ProbeTable}");
        Execute(context,
            $"create table {ProbeTable} (id bigint not null primary key, marker int not null, svalue longtext) character set utf8mb4");
        Execute(context, $"insert into {ProbeTable} (id, marker, svalue) values (1, 0, 'seed-row')");
    }

    private static string? Inline(MySqlDataContext context, string? value)
    {
        var parameter = Expression.Parameter(typeof(IMySqlStringLiteralProbe), "e");
        var projection = Expression.Lambda<Func<IMySqlStringLiteralProbe, string?>>(
            Expression.Constant(value, typeof(string)), parameter);

        return context.From<IMySqlStringLiteralProbe>().Limit(1).Select(projection).First();
    }

    private static string? Captured(MySqlDataContext context, string? value)
    {
        var captured = value;
        return context.From<IMySqlStringLiteralProbe>().Limit(1).Select(e => captured).First();
    }

    private static char InlineChar(MySqlDataContext context, char value)
    {
        var parameter = Expression.Parameter(typeof(IMySqlStringLiteralProbe), "e");
        var projection = Expression.Lambda<Func<IMySqlStringLiteralProbe, char>>(
            Expression.Constant(value, typeof(char)), parameter);

        return context.From<IMySqlStringLiteralProbe>().Limit(1).Select(projection).First();
    }

    private static char CapturedChar(MySqlDataContext context, char value)
    {
        var captured = value;
        return context.From<IMySqlStringLiteralProbe>().Limit(1).Select(e => captured).First();
    }

    private static string? Read(MySqlDataContext context, int marker)
        => context.From<IMySqlStringLiteralProbe>()
            .Where(x => x.Marker == marker)
            .Select(x => x.Svalue)
            .Single();

    private static Dictionary<string, long> SeedLikeRows(MySqlDataContext context, int marker)
    {
        var ids = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (_, value) in StringLiteralSafetyLike.LikeRows)
        {
            var rowId = NextProbeId();
            ids.Add(value, rowId);
            context.CreateInsertBuilder<IMySqlStringLiteralProbe>()
                .Value(x => x.Id, rowId)
                .Value(x => x.Marker, marker)
                .Value(x => x.Svalue, value)
                .Insert();
        }

        return ids;
    }

    private static List<long> LikeInline(MySqlDataContext context, int marker, LikePatternCase pattern)
        => context.From<IMySqlStringLiteralProbe>()
            .Where(StringLiteralSafetyLike.InlinePatternPredicate<IMySqlStringLiteralProbe>(
                nameof(IMySqlStringLiteralProbe.Marker), nameof(IMySqlStringLiteralProbe.Svalue), marker, pattern.Pattern, pattern.ExplicitEscape))
            .Select(x => x.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

    private static List<long> LikeCaptured(MySqlDataContext context, int marker, LikePatternCase pattern)
    {
        var captured = pattern.Pattern;

        return pattern.ExplicitEscape
            ? context.From<IMySqlStringLiteralProbe>()
                .Where(x => x.Marker == marker && SqlFunctions.Sql.like(x.Svalue, captured, "\\"))
                .Select(x => x.Id)
                .ToList()
                .OrderBy(id => id)
                .ToList()
            : context.From<IMySqlStringLiteralProbe>()
                .Where(x => x.Marker == marker && SqlFunctions.Sql.like(x.Svalue, captured))
                .Select(x => x.Id)
                .ToList()
                .OrderBy(id => id)
                .ToList();
    }

    private static List<long> NativeLikeIds(MySqlDataContext context, int marker, LikePatternCase pattern)
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
