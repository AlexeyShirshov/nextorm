using System.Data.Common;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

/// <summary>
/// #327 D8 (AC01–AC09/AC11, §8 live matrix): the shared-suite half of the live string-literal
/// acceptance. Every value is compared ordinally and byte for byte against the exact UTF-8 payload —
/// SQL <c>length</c>/<c>char_length</c> is never the oracle — across the inline-literal, captured
/// parameter, INSERT/readback and UPDATE/readback forms, LIKE with a native typed-parameter
/// reference, null/NUL separation, the 4096-character value, value alternation on a warm command,
/// recreated contexts and the shared-cache invariant.
/// <para>
/// Provider-specific behaviour is a branch inside these facts (not a provider-only test): PostgreSQL
/// rejects U+0000 on both paths and no value-bearing command may execute; SQLite stores NUL exactly
/// while its native LIKE truncates at the first NUL; SQL Server transports Unicode/NUL/long values
/// exactly; MySQL runs only the portable corpus here (the four-mode × <c>NoBackslashEscapes</c> matrix
/// lives in <c>MySqlStringLiteralSafetyExecutionTests</c>, and MariaDB in
/// <c>MariaDbStringLiteralSafetyExecutionTests</c>).
/// </para>
/// <para>
/// The mutation/readback and LIKE fixtures own the task table <c>sls_probe</c> instead of writing the
/// shared <c>complex_entity</c> seed: other classes assert that table's exact contents/count while the
/// suite runs classes in parallel, so a shared-fixture write would be a cross-test race. The table is
/// created with each provider's Unicode text type (<c>text</c>, <c>nvarchar(max)</c>,
/// <c>varchar … utf8mb4</c>, <c>text</c>), which is also what makes the SQL Server Unicode/NUL/long
/// cells exact.
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    private const string ProbeTable = "sls_probe";

    private static long _probeId = DateTime.UtcNow.Ticks;
    private static int _probeMarker = Random.Shared.Next(1_000_000, 10_000_000);

    private static long NextProbeId() => Interlocked.Increment(ref _probeId);

    private static int NextProbeMarker() => Interlocked.Increment(ref _probeMarker);

    private EntityBuilder<IStringLiteralProbe> Probe => _sut.DataProvider.From<IStringLiteralProbe>();

    // =============================================================================================
    // Inline literal and captured parameter forms
    // =============================================================================================

    [Fact]
    [Trait("Issue", "327")]
    public void StringLiteralSafety_InlineProjection_ShouldRoundTripEveryCase()
    {
        foreach (var literal in StringLiteralSafetyCases.Strings)
        {
            if (Provider.Name == "postgres" && literal.ContainsNul)
            {
                var exception = FluentActions.Invoking(() => InlineText(literal.Value))
                    .Should().Throw<NotSupportedException>(
                        $"PostgreSQL cannot represent U+0000 inline (case '{literal.Id}')").Which;

                exception.Message.Should().Contain("PostgreSQL").And.Contain("U+0000");
                exception.Message.Should().NotContain(literal.Value,
                    "the rejection never echoes the supplied value");
                continue;
            }

            AssertExact(InlineText(literal.Value), literal.Value, $"inline literal case '{literal.Id}'");
        }

        foreach (var single in StringLiteralSafetyCases.Chars)
        {
            if (Provider.Name == "postgres" && single.ContainsNul)
            {
                FluentActions.Invoking(() => InlineChar(single.Value))
                    .Should().Throw<NotSupportedException>(
                        $"PostgreSQL rejects default(char)/U+0000 inline (case '{single.Id}')")
                    .WithMessage("*PostgreSQL*U+0000*");
                continue;
            }

            var actual = InlineChar(single.Value);
            actual.Should().Be(single.Value, $"inline char case '{single.Id}'");
            Encoding.UTF8.GetBytes(actual.ToString()).Should()
                .Equal(Encoding.UTF8.GetBytes(single.Value.ToString()), $"inline char bytes case '{single.Id}'");
        }
    }

    [Fact]
    [Trait("Issue", "327")]
    public void StringLiteralSafety_ParameterProjection_ShouldRoundTripEveryCase()
    {
        foreach (var literal in StringLiteralSafetyCases.Strings)
        {
            if (Provider.Name == "postgres" && literal.ContainsNul)
            {
                var exception = FluentActions.Invoking(() => CapturedText(literal.Value))
                    .Should().Throw<NotSupportedException>(
                        $"PostgreSQL rejects U+0000 on the final parameter value (case '{literal.Id}')").Which;

                exception.Message.Should().Contain("PostgreSQL").And.Contain("U+0000");
                exception.Message.Should().NotContain(literal.Value);
                continue;
            }

            AssertExact(CapturedText(literal.Value), literal.Value, $"captured parameter case '{literal.Id}'");
        }

        // The char value is a CLR parameter (char is not rendered as an inline SQL literal; see
        // D8a evidence): it must transport exactly, and default(char) is U+0000 on every provider.
        foreach (var single in StringLiteralSafetyCases.Chars)
        {
            if (Provider.Name == "postgres" && single.ContainsNul)
            {
                FluentActions.Invoking(() => CapturedChar(single.Value))
                    .Should().Throw<NotSupportedException>(
                        $"PostgreSQL rejects default(char)/U+0000 in a parameter (case '{single.Id}')")
                    .WithMessage("*PostgreSQL*U+0000*");
                continue;
            }

            var actual = CapturedChar(single.Value);
            actual.Should().Be(single.Value, $"captured char case '{single.Id}'");
            Encoding.UTF8.GetBytes(actual.ToString()).Should()
                .Equal(Encoding.UTF8.GetBytes(single.Value.ToString()), $"captured char bytes case '{single.Id}'");
        }

        if (Provider.Name == "postgres")
            AssertPostgresNulExecutionSeam();
    }

    [Fact]
    [Trait("Issue", "327")]
    public async Task StringLiteralSafety_ParameterProjectionAsync_ShouldRoundTripHostileSubset()
    {
        string[] hostile =
        [
            "", "plain", "it's; --", "\\'", "x'); SELECT 1; --", "\u0001", "\u001F", "Привет", "漢字", "😀",
        ];

        foreach (var value in hostile)
            AssertExact(await CapturedTextAsync(value), value, "async captured parameter form");
    }

    // =============================================================================================
    // INSERT/readback and UPDATE/readback through the ORM mutation path
    // =============================================================================================

    [Fact]
    [Trait("Issue", "327")]
    public void StringLiteralSafety_InsertAndUpdate_ShouldRoundTripEveryCase()
    {
        RecreateProbeTable();

        foreach (var literal in StringLiteralSafetyCases.Strings)
        {
            var insertId = NextProbeId();
            var insertMarker = NextProbeMarker();

            if (Provider.Name == "postgres" && literal.ContainsNul)
            {
                FluentActions.Invoking(() => InsertProbe(insertId, insertMarker, literal.Value))
                    .Should().Throw<NotSupportedException>(
                        $"the PostgreSQL guard refuses a NUL-bearing INSERT (case '{literal.Id}')")
                    .WithMessage("*PostgreSQL*U+0000*");

                InsertProbe(insertId, insertMarker, "seed");
                FluentActions.Invoking(() => UpdateProbe(insertMarker, literal.Value))
                    .Should().Throw<NotSupportedException>(
                        $"the PostgreSQL guard refuses a NUL-bearing UPDATE (case '{literal.Id}')")
                    .WithMessage("*PostgreSQL*U+0000*");
                continue;
            }

            InsertProbe(insertId, insertMarker, literal.Value);
            AssertExact(ReadProbe(insertMarker), literal.Value, $"INSERT/readback case '{literal.Id}'");

            var updateMarker = NextProbeMarker();
            InsertProbe(NextProbeId(), updateMarker, "before-" + literal.Id);
            UpdateProbe(updateMarker, literal.Value);
            AssertExact(ReadProbe(updateMarker), literal.Value, $"UPDATE/readback case '{literal.Id}'");
        }

        if (Provider.Name == "sqlserver")
            AssertSqlServerUnicodeNulAndLongStorage();
    }

    // =============================================================================================
    // LIKE: ORM result vs a native typed-parameter reference, for inline and captured patterns
    // =============================================================================================

    [Fact]
    [Trait("Issue", "327")]
    public void StringLiteralSafety_Like_ShouldMatchTheNativeReference()
    {
        RecreateProbeTable();
        var marker = NextProbeMarker();
        var idsByValue = SeedLikeRows(marker);

        foreach (var pattern in StringLiteralSafetyLike.Patterns)
        {
            var expected = pattern.Matches.Select(value => idsByValue[value]).OrderBy(id => id).ToList();

            var inline = LikeInline(marker, pattern);
            var captured = LikeCaptured(marker, pattern);
            var reference = NativeLikeIds(marker, pattern);

            reference.Should().Equal(expected,
                $"the native typed-parameter reference must have the documented LIKE semantics for '{pattern.Id}'");
            inline.Should().Equal(reference,
                $"the inline pattern '{pattern.Id}' must match the native engine");
            captured.Should().Equal(reference,
                $"the captured pattern '{pattern.Id}' must match the native engine");
        }

        if (Provider.Name == "sqlite")
            AssertSqliteNulStorageAndNativeLikeTruncation(marker);
    }

    // =============================================================================================
    // NULL / DBNull / default(char) separation
    // =============================================================================================

    [Fact]
    [Trait("Issue", "327")]
    public void StringLiteralSafety_NullAndDefaultChar_ShouldStayDistinctFromNul()
    {
        // A bare null scalar projection is refused by the First(null) contract, so the null arm is read
        // through an anonymous row: the value column must be SQL NULL, never the text 'null'.
        _sut.SimpleEntity.Limit(1).Select(e => new { V = (string?)null }).First().V
            .Should().BeNull("an inline null string is SQL NULL");
        string? nullProbe = null;
        _sut.SimpleEntity.Limit(1).Select(e => new { V = nullProbe }).First().V
            .Should().BeNull("a captured null string is SQL NULL, not the text 'null'");

        RecreateProbeTable();
        var marker = NextProbeMarker();
        InsertProbe(NextProbeId(), marker, null);
        ReadProbe(marker).Should().BeNull("a null mutation value stays SQL NULL");

        var dbNull = ReadDbNullScalar();
        dbNull.IsNull.Should().BeTrue("DBNull at the provider boundary is SQL NULL, never U+0000");

        if (Provider.Name == "postgres")
        {
            FluentActions.Invoking(() => CapturedChar(default))
                .Should().Throw<NotSupportedException>("default(char) is U+0000, which PostgreSQL rejects")
                .WithMessage("*PostgreSQL*U+0000*");
        }
        else
        {
            CapturedChar(default).Should().Be('\0', "default(char) transports U+0000 exactly, never SQL NULL");
            CapturedChar('\u0001').Should().Be('\u0001');
            CapturedChar('\u001f').Should().Be('\u001f');
        }
    }

    // =============================================================================================
    // Long Unicode/control value (constant/parameter projection, never the 100-character column)
    // =============================================================================================

    [Fact]
    [Trait("Issue", "327")]
    public void StringLiteralSafety_LongValues_ShouldRoundTripExactly()
    {
        AssertExact(InlineText(StringLiteralSafetyCases.LongUnicode),
            StringLiteralSafetyCases.LongUnicode, "4096-character Unicode inline literal");
        AssertExact(CapturedText(StringLiteralSafetyCases.LongUnicode),
            StringLiteralSafetyCases.LongUnicode, "4096-character Unicode captured parameter");
        AssertExact(InlineText(StringLiteralSafetyCases.LongControl),
            StringLiteralSafetyCases.LongControl, "4096-character control-containing inline literal");
        AssertExact(CapturedText(StringLiteralSafetyCases.LongControl),
            StringLiteralSafetyCases.LongControl, "4096-character control-containing captured parameter");

        StringLiteralSafetyCases.LongUnicode.Length.Should().Be(4096);
    }

    // =============================================================================================
    // Cache/template/lifecycle: alternation, warm-command NUL, recreated contexts, no sticky Cache
    // =============================================================================================

    [Fact]
    [Trait("Issue", "327")]
    public void StringLiteralSafety_ValueAlternation_ShouldNotReuseAStaleLiteral()
    {
        RecreateProbeTable();
        var marker = NextProbeMarker();

        var safe = "safe-value";
        var hostile = "it's; --";
        var nul = "a\0b";

        var safeId = NextProbeId();
        var hostileId = NextProbeId();
        var nulId = NextProbeId();

        InsertProbe(safeId, marker, safe);
        InsertProbe(hostileId, marker, hostile);
        if (Provider.Name != "postgres")
            InsertProbe(nulId, marker, nul);
        else
            InsertProbe(nulId, marker, safe);

        string? probe = safe;

        bool Matches(long id) => Probe.Where(x => x.Id == id && x.Svalue == probe).Any();

        // safe -> hostile -> safe on the same prepared command.
        Matches(safeId).Should().BeTrue("the first execution binds the safe value");
        probe = hostile;
        Matches(hostileId).Should().BeTrue("the second execution must bind the new hostile value, not a stale literal");
        probe = safe;
        Matches(safeId).Should().BeTrue("the third execution must bind the safe value again");

        // safe -> NUL -> safe: PostgreSQL rejects the NUL execution and the following safe one must work.
        probe = nul;
        if (Provider.Name == "postgres")
        {
            FluentActions.Invoking(() => Matches(nulId))
                .Should().Throw<NotSupportedException>("the warm cached command still validates the refreshed value")
                .WithMessage("*PostgreSQL*U+0000*");
        }
        else
        {
            Matches(nulId).Should().BeTrue("the warm cached command transports U+0000 exactly");
        }

        probe = safe;
        Matches(safeId).Should().BeTrue("a safe execution after the NUL attempt must still bind correctly");

        // A second NUL execution on the already-warm command follows the same rule.
        probe = nul;
        if (Provider.Name == "postgres")
        {
            FluentActions.Invoking(() => Matches(nulId))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*PostgreSQL*U+0000*");
        }
        else
        {
            Matches(nulId).Should().BeTrue("the second NUL execution on a warm command is exact");
        }
    }

    [Fact]
    [Trait("Issue", "327")]
    public void StringLiteralSafety_RecreatedContext_ShouldTransportTheSameValues()
    {
        // Two independently created contexts over the same connection string: each opens/reuses its own
        // connection and runs the parameter form against the corpus. The provider must be seeded before a
        // context that does not go through the shared repository is used.
        Provider.EnsureSeeded();

        using (var first = Provider.CreateContext())
            RunReconnectProbe(first);

        using (var second = Provider.CreateContext())
            RunReconnectProbe(second);
    }

    [Fact]
    [Trait("Issue", "327")]
    public void StringLiteralSafety_SharedCommandCacheFlag_ShouldStayEnabled()
    {
        var context = (DataContext)_sut.DataProvider;
        _sut.SimpleEntity.Where(x => x.Id > 0).Any();

        context.AnyCommand.Should().NotBeNull("the terminal created the shared Any command");
        var shared = context.AnyCommand!.Value;
        shared.Cache.Should().BeTrue("the shared command starts cacheable");
        var cacheEnabled = context.QueryCacheEnabled;

        string? probe = "dadfasd";
        bool AnyComplex() => _sut.ComplexEntity.Where(x => x.String == probe).Any();

        AnyComplex().Should().BeTrue();
        probe = "it's; --";
        AnyComplex().Should().BeFalse();
        probe = "dadfasd";
        AnyComplex().Should().BeTrue();
        probe = "a\0b";
        if (Provider.Name == "postgres")
        {
            FluentActions.Invoking(AnyComplex)
                .Should().Throw<NotSupportedException>()
                .WithMessage("*PostgreSQL*U+0000*");
        }
        else
        {
            AnyComplex().Should().BeFalse("no seeded row carries a NUL value");
        }

        shared.Cache.Should().BeTrue(
            "no execution may mutate the sticky Cache flag of the shared terminal command");
        context.QueryCacheEnabled.Should().Be(cacheEnabled, "the context-wide cache setting stays unchanged");
    }

    // =============================================================================================
    // Provider branches
    // =============================================================================================

    /// <summary>
    /// PostgreSQL U+0000 must be rejected before any value-bearing command executes, on the inline
    /// translation path and on the captured-parameter execution path. An interceptor observes the
    /// command lifecycle; the guard fires earlier, so zero commands execute.
    /// </summary>
    private void AssertPostgresNulExecutionSeam()
    {
        const string secret = "secret\0payload";

        var inlineCounter = new ExecutingCounter();
        using (var context = new PostgresDataContext(
                   PostgresContainer.ConnectionString, new DataContextBuilder().AddInterceptor(inlineCounter)))
        {
            var parameter = Expression.Parameter(typeof(ISimpleEntity), "e");
            var projection = Expression.Lambda<Func<ISimpleEntity, string>>(
                Expression.Constant(secret, typeof(string)), parameter);

            var exception = FluentActions.Invoking(() =>
                    context.From<ISimpleEntity>().Limit(1).Select(projection).First())
                .Should().Throw<NotSupportedException>("the inline translation guard rejects U+0000").Which;

            exception.Message.Should().Contain("PostgreSQL").And.Contain("U+0000");
            exception.Message.Should().NotContain("secret").And.NotContain("payload");
            inlineCounter.Executing.Should().Be(0, "no value-bearing command may execute");
        }

        var capturedCounter = new ExecutingCounter();
        using (var context = new PostgresDataContext(
                   PostgresContainer.ConnectionString, new DataContextBuilder().AddInterceptor(capturedCounter)))
        {
            var captured = secret;

            var exception = FluentActions.Invoking(() =>
                    context.From<ISimpleEntity>().Limit(1).Select(e => captured).First())
                .Should().Throw<NotSupportedException>("the final-value execution guard rejects U+0000").Which;

            exception.Message.Should().Contain("PostgreSQL").And.Contain("U+0000");
            exception.Message.Should().NotContain("secret").And.NotContain("payload");
            capturedCounter.Executing.Should().Be(0, "the guard runs before the driver sees the value");
        }
    }

    /// <summary>SQL Server must transport Unicode, NUL and the long value exactly (nvarchar(max) fixture).</summary>
    private void AssertSqlServerUnicodeNulAndLongStorage()
    {
        foreach (var value in new[] { "Привет", "漢字", "😀", "a\0b", StringLiteralSafetyCases.LongUnicode })
        {
            var marker = NextProbeMarker();
            InsertProbe(NextProbeId(), marker, value);
            AssertExact(ReadProbe(marker), value, "SQL Server Unicode/NUL/long storage");
        }
    }

    /// <summary>
    /// SQLite stores U+0000 exactly but its native LIKE is C-string based and stops at the first NUL;
    /// that truncation is measured and asserted as the native limitation (it is not emulated away).
    /// </summary>
    private void AssertSqliteNulStorageAndNativeLikeTruncation(int marker)
    {
        const string value = "a\0b";
        var id = NextProbeId();
        var nulMarker = NextProbeMarker();
        InsertProbe(id, nulMarker, value);

        AssertExact(ReadProbe(nulMarker), value, "SQLite stores the NUL-bearing value byte for byte");

        Probe
            .Where(x => x.Marker == nulMarker && SqlFunctions.Sql.like(x.Svalue, "a"))
            .Select(x => x.Id)
            .ToList()
            .Should().Equal([id],
                "SQLite's native LIKE truncates at the first NUL: the stored value is seen as its prefix 'a'");
    }

    // =============================================================================================
    // Fixtures and helpers
    // =============================================================================================

    /// <summary>Creates the task-owned probe table with the provider's Unicode text type, and seeds one row.</summary>
    private void RecreateProbeTable()
    {
        switch (Provider.Name)
        {
            case "postgres":
                ExecuteProbeSql($"drop table if exists {ProbeTable}");
                ExecuteProbeSql($"create table {ProbeTable} (id bigint primary key, marker integer not null, svalue text)");
                break;
            case "sqlserver":
                ExecuteProbeSql($"drop table if exists {ProbeTable}");
                ExecuteProbeSql($"create table {ProbeTable} (id bigint not null primary key, marker int not null, svalue nvarchar(max))");
                break;
            case "mysql":
                ExecuteProbeSql($"drop table if exists {ProbeTable}");
                ExecuteProbeSql($"create table {ProbeTable} (id bigint not null primary key, marker int not null, svalue longtext) character set utf8mb4");
                break;
            default:
                ExecuteProbeSql($"drop table if exists {ProbeTable}");
                ExecuteProbeSql($"create table {ProbeTable} (id integer primary key, marker integer not null, svalue text)");
                break;
        }

        ExecuteProbeSql($"insert into {ProbeTable} (id, marker, svalue) values (1, 0, 'seed-row')");
    }

    private void ExecuteProbeSql(string sql)
    {
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void InsertProbe(long id, int marker, string? value)
        => _sut.DataProvider.CreateInsertBuilder<IStringLiteralProbe>()
            .Value(x => x.Id, id)
            .Value(x => x.Marker, marker)
            .Value(x => x.Svalue, value)
            .Insert();

    private void UpdateProbe(int marker, string? value)
        => _sut.DataProvider.CreateUpdateBuilder<IStringLiteralProbe>()
            .Set(x => x.Svalue, value)
            .Where(x => x.Marker == marker)
            .Update();

    private string? ReadProbe(int marker)
        => Probe.Where(x => x.Marker == marker).Select(x => x.Svalue).Single();

    private Dictionary<string, long> SeedLikeRows(int marker)
    {
        var ids = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (id, value) in StringLiteralSafetyLike.LikeRows)
        {
            var rowId = NextProbeId();
            ids.Add(value, rowId);
            InsertProbe(rowId, marker, value);
        }

        return ids;
    }

    private List<long> LikeInline(int marker, LikePatternCase pattern)
        => Probe
            .Where(StringLiteralSafetyLike.InlinePatternPredicate<IStringLiteralProbe>(
                nameof(IStringLiteralProbe.Marker),
                nameof(IStringLiteralProbe.Svalue),
                marker,
                pattern.Pattern,
                pattern.ExplicitEscape))
            .Select(x => x.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

    private List<long> LikeCaptured(int marker, LikePatternCase pattern)
    {
        var captured = pattern.Pattern;

        return pattern.ExplicitEscape
            ? Probe
                .Where(x => x.Marker == marker && SqlFunctions.Sql.like(x.Svalue, captured, "\\"))
                .Select(x => x.Id)
                .ToList()
                .OrderBy(id => id)
                .ToList()
            : Probe
                .Where(x => x.Marker == marker && SqlFunctions.Sql.like(x.Svalue, captured))
                .Select(x => x.Id)
                .ToList()
                .OrderBy(id => id)
                .ToList();
    }

    /// <summary>
    /// Runs the same LIKE through raw ADO.NET with a typed string parameter and the provider's native
    /// ESCAPE spelling (MySQL's escape operand is the mode-independent hex token). This is the oracle,
    /// not a cast of the ORM SQL.
    /// </summary>
    private List<long> NativeLikeIds(int marker, LikePatternCase pattern)
    {
        var escape = pattern.ExplicitEscape
            ? Provider.Name switch
            {
                "postgres" => "E'\\\\'",
                "mysql" => "_utf8mb4 X'5c'",
                _ => "'\\'",
            }
            : string.Empty;
        var escapeClause = escape.Length == 0 ? string.Empty : " escape " + escape;

        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(
            $"select id from {ProbeTable} where marker = @m and svalue like @p{escapeClause}");

        var markerParameter = command.CreateParameter();
        markerParameter.ParameterName = "@m";
        markerParameter.Value = marker;
        command.Parameters.Add(markerParameter);

        var patternParameter = command.CreateParameter();
        patternParameter.ParameterName = "@p";
        patternParameter.Value = pattern.Pattern;
        command.Parameters.Add(patternParameter);

        var ids = new List<long>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            ids.Add(Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture));

        ids.Sort();
        return ids;
    }

    private void RunReconnectProbe(IDataContext context)
    {
        foreach (var literal in StringLiteralSafetyCases.Strings)
        {
            if (Provider.Name == "postgres" && literal.ContainsNul)
            {
                FluentActions.Invoking(() => CapturedTextOn(context, literal.Value))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("*PostgreSQL*U+0000*");
                continue;
            }

            AssertExact(CapturedTextOn(context, literal.Value), literal.Value,
                $"recreated-context parameter case '{literal.Id}'");
        }
    }

    private static string? CapturedTextOn(IDataContext context, string? value)
    {
        var captured = value;
        return context.From<ISimpleEntity>().Limit(1).Select(e => captured).First();
    }

    private string? InlineText(string? value)
    {
        var parameter = Expression.Parameter(typeof(ISimpleEntity), "e");
        var projection = Expression.Lambda<Func<ISimpleEntity, string?>>(
            Expression.Constant(value, typeof(string)), parameter);

        return _sut.SimpleEntity.Limit(1).Select(projection).First();
    }

    private string? CapturedText(string? value)
    {
        var captured = value;
        return _sut.SimpleEntity.Limit(1).Select(e => captured).First();
    }

    private async Task<string?> CapturedTextAsync(string? value)
    {
        var captured = value;
        return await _sut.SimpleEntity.Limit(1)
            .Select(e => captured)
            .FirstAsync(TestContext.Current.CancellationToken);
    }

    private char InlineChar(char value)
    {
        var parameter = Expression.Parameter(typeof(ISimpleEntity), "e");
        var projection = Expression.Lambda<Func<ISimpleEntity, char>>(
            Expression.Constant(value, typeof(char)), parameter);

        return _sut.SimpleEntity.Limit(1).Select(projection).First();
    }

    private char CapturedChar(char value)
    {
        var captured = value;
        return _sut.SimpleEntity.Limit(1).Select(e => captured).First();
    }

    private (bool IsNull, object? Value) ReadDbNullScalar()
    {
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand("select @v");

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@v";
        parameter.Value = DBNull.Value;
        command.Parameters.Add(parameter);

        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();
        return (reader.IsDBNull(0), reader.IsDBNull(0) ? null : reader.GetValue(0));
    }

    private static void AssertExact(string? actual, string expected, string because)
    {
        actual.Should().Be(expected, because);
        Encoding.UTF8.GetBytes(actual!).Should().Equal(Encoding.UTF8.GetBytes(expected), because);
    }

    /// <summary>Counts database executions so a pre-execution refusal can be proven.</summary>
    private sealed class ExecutingCounter : IQueryInterceptor
    {
        private int _executing;

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _executing);

        public int Executing => Volatile.Read(ref _executing);
    }
}
