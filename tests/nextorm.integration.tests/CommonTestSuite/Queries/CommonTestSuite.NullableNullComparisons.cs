using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #296: a nullable null literal (<c>(int?)null</c>, and the compiler-produced nullable
/// <c>default</c>) renders <c>is null</c> / <c>is not null</c> for both operators instead of
/// <c>= null</c> / <c>!= null</c>. The fact owns a three-row table — id 1 has a NULL value, ids 2 and
/// 3 do not — so it is independent of the seeded <c>complex_entity</c> fixture, and it runs once per
/// concrete suite: <c>SqliteIntegrationTests</c>, <c>PostgresIntegrationTests</c>,
/// <c>SqlServerIntegrationTests</c> and <c>MySqlIntegrationTests</c>. MariaDB and ClickHouse have no
/// shared-suite subclass; their coverage is the SQL-generation matrix.
/// </summary>
public abstract partial class CommonTestSuite
{
    [SqlTable("t296_nullable_compare")]
    public interface IT296NullableCompare
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("nullableint")]
        int? Nullable { get; set; }
    }

    [Fact]
    public async Task NullableLiteralComparisonsReturnExpectedRows()
    {
        var ctx = _sut.DataProvider;

        ExecuteNullableFixture(ctx, "drop table if exists t296_nullable_compare");
        ExecuteNullableFixture(
            ctx,
            "create table t296_nullable_compare (id bigint not null primary key, nullableint int null)");
        ExecuteNullableFixture(
            ctx,
            "insert into t296_nullable_compare (id, nullableint) values (1, null), (2, 10), (3, 20)");

        try
        {
            // `== (int?)null`: only the NULL row matches, and the captured SQL is a presence test.
            var equalitySql = NullableComparisonSql(ctx, ctx.From<IT296NullableCompare>()
                .Where(x => x.Nullable == (int?)null)
                .Select(x => x.Id));

            equalitySql.ToLowerInvariant().Should().Contain("is null").And.NotContain("is not null");

            var equalityRows = await ctx.From<IT296NullableCompare>()
                .Where(x => x.Nullable == (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();

            LogNullableScenario("nullable == (int?)null", equalitySql, $"[{string.Join(", ", equalityRows)}]");
            equalityRows.Should().Equal(1L);

            // `!= (int?)null`: the complementary presence test.
            var inequalitySql = NullableComparisonSql(ctx, ctx.From<IT296NullableCompare>()
                .Where(x => x.Nullable != (int?)null)
                .Select(x => x.Id));

            inequalitySql.ToLowerInvariant().Should().Contain("is not null");

            var inequalityRows = await ctx.From<IT296NullableCompare>()
                .Where(x => x.Nullable != (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();

            LogNullableScenario("nullable != (int?)null", inequalitySql, $"[{string.Join(", ", inequalityRows)}]");
            inequalityRows.Should().Equal(2L, 3L);

            // A derived operand keeps its arithmetic and agrees with the plain column comparison.
            var derivedEqualitySql = NullableComparisonSql(ctx, ctx.From<IT296NullableCompare>()
                .Where(x => (x.Nullable + 1) == (int?)null)
                .Select(x => x.Id));

            var derivedEqualityRows = await ctx.From<IT296NullableCompare>()
                .Where(x => (x.Nullable + 1) == (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();

            LogNullableScenario("(nullable + 1) == (int?)null", derivedEqualitySql, $"[{string.Join(", ", derivedEqualityRows)}]");
            derivedEqualityRows.Should().Equal(1L);

            var derivedInequalitySql = NullableComparisonSql(ctx, ctx.From<IT296NullableCompare>()
                .Where(x => (x.Nullable + 1) != (int?)null)
                .Select(x => x.Id));

            var derivedInequalityRows = await ctx.From<IT296NullableCompare>()
                .Where(x => (x.Nullable + 1) != (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();

            LogNullableScenario("(nullable + 1) != (int?)null", derivedInequalitySql, $"[{string.Join(", ", derivedInequalityRows)}]");
            derivedInequalityRows.Should().Equal(2L, 3L);

            // A projected comparison goes through the independent predicate path.
            var projectionSql = NullableComparisonSql(ctx, ctx.From<IT296NullableCompare>()
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    IsNull = x.Nullable == (int?)null,
                    IsNotNull = x.Nullable != (int?)null,
                }));

            var projected = await ctx.From<IT296NullableCompare>()
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    IsNull = x.Nullable == (int?)null,
                    IsNotNull = x.Nullable != (int?)null,
                })
                .ToListAsync();

            LogNullableScenario(
                "projection nullable == / != (int?)null",
                projectionSql,
                $"[{string.Join(", ", projected.Select(x => $"{{id={x.Id}, isNull={x.IsNull}, isNotNull={x.IsNotNull}}}"))}]");

            projected.Select(x => x.Id).Should().Equal(1L, 2L, 3L);
            projected.Select(x => x.IsNull).Should().Equal(true, false, false);
            projected.Select(x => x.IsNotNull).Should().Equal(false, true, true);
        }
        finally
        {
            ExecuteNullableFixture(ctx, "drop table if exists t296_nullable_compare");
        }
    }

    /// <summary>Emits one evidence line per scenario: provider, identity, executed SQL and observed rows.</summary>
    private void LogNullableScenario(string scenario, string sql, string rows)
        => TestContext.Current.TestOutputHelper?.WriteLine(
            $"provider={Provider.Name} test=CommonTestSuite.NullableLiteralComparisonsReturnExpectedRows " +
            $"scenario={scenario} sql={sql.Replace('\n', ' ')} rows={rows}");

    /// <summary>Renders the SQL a fresh command is prepared with, without executing it.</summary>
    private static string NullableComparisonSql<TResult>(IDataContext ctx, QueryCommand<TResult> command)
        => ((DbPreparedQueryCommand<TResult>)((DataContext)ctx)
                .GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static void ExecuteNullableFixture(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    // =============================================================================================
    // T296b (D4) — live truth tables for the approved total-comparison contract (A1–A15, A23–A29).
    //
    // The assertion bodies accept an ALREADY SEEDED context, so the four CommonTestSuite providers and
    // the provider-specific MariaDB/ClickHouse harnesses run the identical checks without inventing a
    // new CommonTestSuite subclass (forbidden by the plan). Captured values stay captured mutable
    // locals: every check drives the real preparation/parameter-refresh path and never an inlined
    // constant, so the null -> value -> null transitions prove freshness rather than a stale command.
    //
    // Fixture (SeedT296bFixture):
    //   t296b_rows  : id 1(null,null) 2(10,alpha) 3(20,beta) 4(10,alpha) 5(30,null) 6(null,gamma)
    //   t296b_parent: 1 one, 2 two, 3 three, 4 four (four has no child)
    //   t296b_child : 11(p1,null,null) 12(p1,5,x) 21(p2,42,y) 22(p2,42,y) 31(p3,null,null)
    // =============================================================================================

    /// <summary>Creates and seeds the deterministic T296b fixture (shared portable DDL).</summary>
    internal static void SeedT296bFixture(IDataContext ctx)
    {
        DropT296bFixture(ctx);
        ExecuteNullableFixture(ctx, "create table t296b_rows (id bigint not null primary key, n int null, s varchar(64) null)");
        ExecuteNullableFixture(
            ctx,
            "insert into t296b_rows (id, n, s) values " +
            "(1, null, null), (2, 10, 'alpha'), (3, 20, 'beta'), (4, 10, 'alpha'), (5, 30, null), (6, null, 'gamma')");
        ExecuteNullableFixture(ctx, "create table t296b_parent (id bigint not null primary key, label varchar(64) null)");
        ExecuteNullableFixture(
            ctx,
            "insert into t296b_parent (id, label) values (1, 'one'), (2, 'two'), (3, 'three'), (4, 'four')");
        ExecuteNullableFixture(
            ctx,
            "create table t296b_child (id bigint not null primary key, parent_id bigint not null, amount int null, tag varchar(64) null)");
        ExecuteNullableFixture(
            ctx,
            "insert into t296b_child (id, parent_id, amount, tag) values " +
            "(11, 1, null, null), (12, 1, 5, 'x'), (21, 2, 42, 'y'), (22, 2, 42, 'y'), (31, 3, null, null)");
    }

    /// <summary>Drops the T296b fixture tables (children first).</summary>
    internal static void DropT296bFixture(IDataContext ctx)
    {
        ExecuteNullableFixture(ctx, "drop table if exists t296b_child");
        ExecuteNullableFixture(ctx, "drop table if exists t296b_parent");
        ExecuteNullableFixture(ctx, "drop table if exists t296b_rows");
    }

    /// <summary>A1–A5, A9: captured <c>==</c>/<c>!=</c> for <c>int?</c>/<c>string?</c>, both orders.</summary>
    [Fact]
    public void T296b_CapturedNullableComparison_ShouldMatchTheTruthTable()
        => RunT296b(AssertT296bCapturedComparisonTruthTable);

    /// <summary>A10–A12: mapped <c>HasValue</c>/<c>!HasValue</c> standalone and composed.</summary>
    [Fact]
    public void T296b_MappedHasValue_ShouldMatchTheTruthTable()
        => RunT296b(AssertT296bMappedHasValueTruthTable);

    /// <summary>A13–A15: correlated <c>Exists</c> keeps the outer association and refreshes the captured value.</summary>
    [Fact]
    public void T296b_CorrelatedExists_ShouldKeepOuterAssociation()
        => RunT296b(AssertT296bCorrelatedExistsAssociation);

    /// <summary>A23–A26: boolean SELECT projections materialise as <see cref="bool"/>, correlated included.</summary>
    [Fact]
    public void T296b_BooleanSelect_ShouldMaterialiseNonNullableBool()
        => RunT296b(AssertT296bBooleanSelectMaterialisation);

    /// <summary>A27–A29: JOIN ON multiplicity, LEFT JOIN preservation and (where supported) RIGHT JOIN.</summary>
    [Fact]
    public void T296b_JoinOn_ShouldKeepMultiplicityAndOuterRows()
        => RunT296b(AssertT296bJoinOnTruthTable);

    private static readonly object T296bEvidenceGate = new();

    /// <summary>
    /// Wraps the per-body evidence logger: it forwards to the test output and, when
    /// <c>NEXTORM_T296B_EVIDENCE_DUMP</c> names a file, appends the same line there so the raw
    /// provider evidence can be projected into the rv1 artifacts without depending on runner verbosity.
    /// </summary>
    internal static Action<string> T296bEvidenceLogger(Action<string> sink)
        => message =>
        {
            sink(message);
            var dump = Environment.GetEnvironmentVariable("NEXTORM_T296B_EVIDENCE_DUMP");
            if (string.IsNullOrEmpty(dump))
                return;

            lock (T296bEvidenceGate)
                File.AppendAllText(dump, message.Replace("\r", string.Empty).Replace('\n', ' ') + "\n");
        };

    private void RunT296b(Action<IDataContext, string, Action<string>> body)
    {
        var ctx = _sut.DataProvider;
        SeedT296bFixture(ctx);
        try
        {
            body(
                ctx,
                Provider.Name,
                T296bEvidenceLogger(static message => TestContext.Current.TestOutputHelper?.WriteLine(message)));
        }
        finally
        {
            DropT296bFixture(ctx);
        }
    }

    /// <summary>Runs the captured-comparison truth table on an already seeded context.</summary>
    internal static void AssertT296bCapturedComparisonTruthTable(IDataContext ctx, string provider, Action<string> log)
    {
        var rows = ctx.From<IT296bRow>();

        void Check(string scenario, EntityBuilder<IT296bRow> query, params long[] expected)
        {
            var ordered = query.OrderBy(x => x.Id).Select(x => x.Id);
            var sql = NullableComparisonSql(ctx, ordered);
            var ids = ordered.ToList();
            ids.Should().Equal(expected, scenario);
            log($"provider={provider} test=T296b_CapturedNullableComparison row=A1-A5,A9 scenario={scenario} " +
                $"rows=[{string.Join(",", ids)}] sql={CollapseNullableSql(sql)}");
        }

        // Equal value: only the rows carrying it, including the duplicate (2 and 4 both hold 10).
        int? capturedInt = 10;
        Check("int == 10", rows.Where(x => x.N == capturedInt), 2, 4);
        Check("10 == int", rows.Where(x => capturedInt == x.N), 2, 4);
        // Lifted inequality is TOTAL: the NULL rows compare as "not equal" to a non-null value.
        Check("int != 10", rows.Where(x => x.N != capturedInt), 1, 3, 5, 6);
        Check("10 != int", rows.Where(x => capturedInt != x.N), 1, 3, 5, 6);

        // Unequal value: no equality match, every row on inequality.
        capturedInt = 42;
        Check("int == 42 (no match)", rows.Where(x => x.N == capturedInt));
        Check("42 == int (no match)", rows.Where(x => capturedInt == x.N));
        Check("int != 42 (all rows)", rows.Where(x => x.N != capturedInt), 1, 2, 3, 4, 5, 6);
        Check("42 != int (all rows)", rows.Where(x => capturedInt != x.N), 1, 2, 3, 4, 5, 6);

        // Captured null: equality matches only NULL rows, inequality the complementary set.
        capturedInt = null;
        Check("int == null-capture", rows.Where(x => x.N == capturedInt), 1, 6);
        Check("null-capture == int", rows.Where(x => capturedInt == x.N), 1, 6);
        Check("int != null-capture", rows.Where(x => x.N != capturedInt), 2, 3, 4, 5);
        Check("null-capture != int", rows.Where(x => capturedInt != x.N), 2, 3, 4, 5);

        // The same truth table for string? (A9), including the duplicate 'alpha'.
        string? capturedText = "alpha";
        Check("string == alpha", rows.Where(x => x.S == capturedText), 2, 4);
        Check("alpha == string", rows.Where(x => capturedText == x.S), 2, 4);
        Check("string != alpha", rows.Where(x => x.S != capturedText), 1, 3, 5, 6);
        Check("alpha != string", rows.Where(x => capturedText != x.S), 1, 3, 5, 6);

        capturedText = "zeta";
        Check("string == zeta (no match)", rows.Where(x => x.S == capturedText));
        Check("string != zeta (all rows)", rows.Where(x => x.S != capturedText), 1, 2, 3, 4, 5, 6);

        capturedText = null;
        Check("string == null-capture", rows.Where(x => x.S == capturedText), 1, 5);
        Check("string != null-capture", rows.Where(x => x.S != capturedText), 2, 3, 4, 6);
    }

    /// <summary>Runs the mapped-presence truth table on an already seeded context.</summary>
    internal static void AssertT296bMappedHasValueTruthTable(IDataContext ctx, string provider, Action<string> log)
    {
        var rows = ctx.From<IT296bRow>();

        void Check(string scenario, EntityBuilder<IT296bRow> query, params long[] expected)
        {
            var ordered = query.OrderBy(x => x.Id).Select(x => x.Id);
            var sql = NullableComparisonSql(ctx, ordered);
            var ids = ordered.ToList();
            ids.Should().Equal(expected, scenario);
            log($"provider={provider} test=T296b_MappedHasValue row=A10-A12 scenario={scenario} " +
                $"rows=[{string.Join(",", ids)}] sql={CollapseNullableSql(sql)}");
        }

        Check("int.HasValue", rows.Where(x => x.N.HasValue), 2, 3, 4, 5);
        Check("!int.HasValue", rows.Where(x => !x.N.HasValue), 1, 6);

        Check("int.HasValue && int > 15", rows.Where(x => x.N.HasValue && x.N > 15), 3, 5);
        Check("int.HasValue || string == gamma", rows.Where(x => x.N.HasValue || x.S == "gamma"), 2, 3, 4, 5, 6);
        Check("!int.HasValue || int > 15", rows.Where(x => !x.N.HasValue || x.N > 15), 1, 3, 5, 6);
        Check("!int.HasValue && string != null", rows.Where(x => !x.N.HasValue && x.S != null), 6);
        Check("int.HasValue && string != null", rows.Where(x => x.N.HasValue && x.S != null), 2, 3, 4);

        // Literal presence stays the earlier approved semantics and must not be re-routed.
        Check("string == null literal", rows.Where(x => x.S == null), 1, 5);
        Check("string != null literal", rows.Where(x => x.S != null), 2, 3, 4, 6);
    }

    /// <summary>Runs the correlated-Exists outer-association and freshness checks on a seeded context.</summary>
    internal static void AssertT296bCorrelatedExistsAssociation(IDataContext ctx, string provider, Action<string> log)
    {
        var parents = ctx.From<IT296bParent>();
        var children = ctx.From<IT296bChild>();
        int? capturedAmount = null;

        long[] OuterIds(string scenario)
        {
            var query = parents
                .Where(p => SqlFunctions.Sql.exists(
                    children.Where(c => c.ParentId == p.Id && c.Amount == capturedAmount)))
                .OrderBy(p => p.Id)
                .Select(p => p.Id);
            var sql = NullableComparisonSql(ctx, query);
            var ids = query.ToList().ToArray();
            log($"provider={provider} test=T296b_CorrelatedExists row=A13-A15 scenario={scenario} " +
                $"rows=[{string.Join(",", ids)}] sql={CollapseNullableSql(sql)}");
            return ids;
        }

        // Each parent owns DIFFERENT children, so a leaking association would include the wrong parent.
        capturedAmount = 42;
        OuterIds("captured 42").Should().Equal(2L);
        capturedAmount = 5;
        OuterIds("captured 5").Should().Equal(1L);
        capturedAmount = 7;
        OuterIds("captured 7 (no child)").Should().BeEmpty();
        capturedAmount = 99;
        OuterIds("captured 99 (no child)").Should().BeEmpty();

        // A15: the SAME expression shape, the captured value transitions null -> 42 -> null, and each
        // execution must observe the current value (no stale first preparation).
        capturedAmount = null;
        OuterIds("transition null (#1)").Should().Equal(1L, 3L);
        capturedAmount = 42;
        OuterIds("transition 42").Should().Equal(2L);
        capturedAmount = null;
        OuterIds("transition null (#2)").Should().Equal(1L, 3L);

        // Nullable literal and mapped presence forms keep the same association.
        var literal = parents
            .Where(p => SqlFunctions.Sql.exists(
                children.Where(c => c.ParentId == p.Id && c.Amount == (int?)null)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        literal.Should().Equal(1L, 3L);
        log($"provider={provider} test=T296b_CorrelatedExists row=A13-A15 scenario=nullable literal rows=[{string.Join(",", literal)}]");

        var presence = parents
            .Where(p => SqlFunctions.Sql.exists(
                children.Where(c => c.ParentId == p.Id && c.Amount.HasValue)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        presence.Should().Equal(1L, 2L);
        log($"provider={provider} test=T296b_CorrelatedExists row=A12-A13 scenario=child HasValue rows=[{string.Join(",", presence)}]");

        var absence = parents
            .Where(p => SqlFunctions.Sql.exists(
                children.Where(c => c.ParentId == p.Id && !c.Amount.HasValue)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        absence.Should().Equal(1L, 3L);

        string? capturedTag = "x";
        var byTag = parents
            .Where(p => SqlFunctions.Sql.exists(
                children.Where(c => c.ParentId == p.Id && c.Tag == capturedTag)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        byTag.Should().Equal(1L);

        capturedTag = null;
        var byNullTag = parents
            .Where(p => SqlFunctions.Sql.exists(
                children.Where(c => c.ParentId == p.Id && c.Tag == capturedTag)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        byNullTag.Should().Equal(1L, 3L);
    }

    /// <summary>Runs the boolean-SELECT materialisation checks (standalone, composed, correlated).</summary>
    internal static void AssertT296bBooleanSelectMaterialisation(IDataContext ctx, string provider, Action<string> log)
    {
        var rows = ctx.From<IT296bRow>();
        int? capturedInt = 10;
        string? capturedText = "alpha";

        var query = rows.OrderBy(x => x.Id).Select(x => new
        {
            x.Id,
            IntEq = x.N == capturedInt,
            IntNe = x.N != capturedInt,
            StrEq = x.S == capturedText,
            StrNe = x.S != capturedText,
            Has = x.N.HasValue,
            Absent = !x.N.HasValue,
            ComposedOr = x.N.HasValue || x.Id > 4,
            ComposedAnd = x.N.HasValue && x.S != null,
        });

        var sql = NullableComparisonSql(ctx, query);
        log($"provider={provider} test=T296b_BooleanSelect row=A23-A25 stage=sql sql={CollapseNullableSql(sql)}");
        var projected = query.ToList();

        projected.Select(x => x.Id).Should().Equal(1L, 2L, 3L, 4L, 5L, 6L);
        projected.Select(x => x.IntEq).Should().Equal(false, true, false, true, false, false);
        projected.Select(x => x.IntNe).Should().Equal(true, false, true, false, true, true);
        projected.Select(x => x.StrEq).Should().Equal(false, true, false, true, false, false);
        projected.Select(x => x.StrNe).Should().Equal(true, false, true, false, true, true);
        projected.Select(x => x.Has).Should().Equal(false, true, true, true, true, false);
        projected.Select(x => x.Absent).Should().Equal(true, false, false, false, false, true);
        projected.Select(x => x.ComposedOr).Should().Equal(false, true, true, true, true, true);
        projected.Select(x => x.ComposedAnd).Should().Equal(false, true, true, true, false, false);

        // A23–A25: the MATERIALISED value is a non-nullable bool — never bool?, never DBNull.
        projected.Select(x => x.IntEq.GetType()).Should().OnlyContain(t => t == typeof(bool));
        projected.Select(x => x.IntNe.GetType()).Should().OnlyContain(t => t == typeof(bool));
        projected.Select(x => x.StrEq.GetType()).Should().OnlyContain(t => t == typeof(bool));
        projected.Select(x => x.StrNe.GetType()).Should().OnlyContain(t => t == typeof(bool));
        projected.Select(x => x.Has.GetType()).Should().OnlyContain(t => t == typeof(bool));
        projected.Select(x => x.Absent.GetType()).Should().OnlyContain(t => t == typeof(bool));
        projected.Select(x => x.ComposedOr.GetType()).Should().OnlyContain(t => t == typeof(bool));
        projected.Select(x => x.ComposedAnd.GetType()).Should().OnlyContain(t => t == typeof(bool));

        // Storage evidence: the reader's field type for the boolean scalar. PostgreSQL and SQL Server
        // expose a real boolean/bit type, so a projected predicate must not come back as a number there.
        var readerTypes = ReaderFieldTypes(ctx, query);
        log($"provider={provider} test=T296b_BooleanSelect row=A23-A25 storage={string.Join(",", readerTypes.Select(t => t.Name))} " +
            $"sql={CollapseNullableSql(sql)}");
        if (provider is "sqlserver" or "postgres")
            readerTypes[1].Should().Be(typeof(bool), "a boolean SELECT scalar must materialise through a real boolean storage type");

        // A26: the same forms inside a correlated subquery keep the outer association.
        var parents = ctx.From<IT296bParent>();
        var children = ctx.From<IT296bChild>();

        void CheckCorrelated(string scenario, int? value, bool[] expectedMatches)
        {
            capturedInt = value;
            var correlatedQuery = parents.OrderBy(p => p.Id).Select(p => new
            {
                p.Id,
                HasMatch = children.Where(c => c.ParentId == p.Id && c.Amount == capturedInt).Any(),
                HasPresence = children.Where(c => c.ParentId == p.Id && c.Amount.HasValue).Any(),
            });
            var correlatedSql = NullableComparisonSql(ctx, correlatedQuery);
            log($"provider={provider} test=T296b_BooleanSelect row=A26 scenario={scenario} stage=sql " +
                $"sql={CollapseNullableSql(correlatedSql)}");
            var correlated = correlatedQuery.ToList();
            correlated.Select(x => x.Id).Should().Equal(1L, 2L, 3L, 4L);
            correlated.Select(x => x.HasMatch).Should().Equal(expectedMatches);
            correlated.Select(x => x.HasPresence).Should().Equal(true, true, false, false);
            correlated.Select(x => x.HasMatch.GetType()).Should().OnlyContain(t => t == typeof(bool));
            correlated.Select(x => x.HasPresence.GetType()).Should().OnlyContain(t => t == typeof(bool));
            log($"provider={provider} test=T296b_BooleanSelect row=A26 scenario={scenario} " +
                $"matches=[{string.Join(",", correlated.Select(x => x.HasMatch))}] sql={CollapseNullableSql(correlatedSql)}");
        }

        CheckCorrelated("correlated 42", 42, [false, true, false, false]);
        CheckCorrelated("correlated null", null, [true, false, true, false]);
        CheckCorrelated("correlated 42 again", 42, [false, true, false, false]);
    }

    /// <summary>Runs the JOIN ON multiplicity / outer-preservation checks on a seeded context.</summary>
    internal static void AssertT296bJoinOnTruthTable(IDataContext ctx, string provider, Action<string> log)
    {
        var parents = ctx.From<IT296bParent>();
        var children = ctx.From<IT296bChild>();
        int? capturedAmount = 42;
        string? capturedTag = "y";

        static string[] Format(IEnumerable<string> pairs) => pairs.OrderBy(x => x, StringComparer.Ordinal).ToArray();

        // ClickHouse 25.8 mis-applies a bare `IS NULL` ON residual: the residual is pushed to the left
        // join input, where the right-side column is NULL, so every matched row passes. The identical
        // form works with one more conjunct, wrapped as `isNull(col) = 1`, inside OR, or in WHERE.
        // Ground truth of the real container is recorded in rv1/live/clickhouse-join-on-null-residual.txt;
        // the shape is a provider limitation, not something this task may work around by changing the
        // join algorithm or widening the SQL semantics.
        var clickHouseBareNullResidual = provider == "clickhouse";

        void NotApplicable(string scenario, string reason)
            => log($"T296B_JOIN_JSON {JsonSerializer.Serialize(new { provider, scenario, status = "not-applicable", reason })}");

        void Check(string scenario, IEnumerable<string> pairs, params string[] expected)
        {
            var actual = Format(pairs);
            actual.Should().BeEquivalentTo(expected, scenario);
            var unmatched = actual
                .Where(p => p.EndsWith(":null", StringComparison.Ordinal))
                .Select(p => p[..p.IndexOf(':')])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            log($"T296B_JOIN_JSON {JsonSerializer.Serialize(new { provider, scenario, pairs = actual, unmatchedOuter = unmatched })}");
        }

        // ClickHouse's plain LEFT JOIN does not inject join_use_nulls, so an unmatched right side is the
        // provider default (0) rather than SQL NULL. Preservation of every outer row is still asserted
        // exactly; the null-vs-default right side is recorded as a provider behaviour, never as a pass.
        void CheckLeftPreserved(string scenario, IEnumerable<(long Parent, long? Child)> pairs, params long[] expectedParents)
        {
            var rows = pairs.ToList();
            rows.Select(r => r.Parent).OrderBy(x => x).Should().Equal(expectedParents, scenario);
            log($"T296B_JOIN_JSON {JsonSerializer.Serialize(new
            {
                provider,
                scenario,
                pairs = Format(rows.Select(r => $"{r.Parent}:{(r.Child is null ? "null" : r.Child.Value.ToString())}")),
                unmatchedOuter = rows.Where(r => r.Child is null || r.Child == 0).Select(r => r.Parent)
                    .Distinct().OrderBy(x => x).ToArray(),
                childNullSide = "provider-default (join_use_nulls is injected for reference-navigation queries only)",
            })}");
        }

        // A27–A28: the ON predicate keeps the join structure and filters on the joined rows only.
        static IEnumerable<string> InnerPairs(IEnumerable<(long Parent, long Child)> pairs)
            => pairs.Select(p => $"{p.Parent}:{p.Child}");

        Check(
            "inner captured 42",
            InnerPairs(parents.Join(children, (p, c) => p.Id == c.ParentId && c.Amount == capturedAmount)
                .Select(p => new { Parent = p.Item1.Id, Child = p.Item2.Id })
                .ToList()
                .Select(p => (p.Parent, p.Child))),
            "2:21", "2:22");

        Check(
            "inner captured != 42 (total)",
            InnerPairs(parents.Join(children, (p, c) => p.Id == c.ParentId && c.Amount != capturedAmount)
                .Select(p => new { Parent = p.Item1.Id, Child = p.Item2.Id })
                .ToList()
                .Select(p => (p.Parent, p.Child))),
            "1:11", "1:12", "3:31");

        if (clickHouseBareNullResidual)
        {
            NotApplicable(
                "inner nullable literal",
                "ClickHouse 25.8 mis-applies a bare IS NULL ON residual (rv1/live/clickhouse-join-on-null-residual.txt); the approved rendering is unchanged");
        }
        else
        {
            var innerLiteralQuery = parents.Join(children, (p, c) => p.Id == c.ParentId && c.Amount == (int?)null)
                .Select(p => new { Parent = p.Item1.Id, Child = p.Item2.Id });
            log($"provider={provider} test=T296b_JoinOn scenario=inner nullable literal stage=sql " +
                $"sql={CollapseNullableSql(NullableComparisonSql(ctx, innerLiteralQuery))}");
            Check(
                "inner nullable literal",
                InnerPairs(innerLiteralQuery.ToList().Select(p => (p.Parent, p.Child))),
                "1:11", "3:31");
        }

        Check(
            "inner HasValue",
            InnerPairs(parents.Join(children, (p, c) => p.Id == c.ParentId && c.Amount.HasValue)
                .Select(p => new { Parent = p.Item1.Id, Child = p.Item2.Id })
                .ToList()
                .Select(p => (p.Parent, p.Child))),
            "1:12", "2:21", "2:22");

        if (clickHouseBareNullResidual)
        {
            NotApplicable(
                "inner !HasValue",
                "ClickHouse 25.8 mis-applies a bare IS NULL ON residual (rv1/live/clickhouse-join-on-null-residual.txt); the approved rendering is unchanged");
        }
        else
        {
            Check(
                "inner !HasValue",
                InnerPairs(parents.Join(children, (p, c) => p.Id == c.ParentId && !c.Amount.HasValue)
                    .Select(p => new { Parent = p.Item1.Id, Child = p.Item2.Id })
                    .ToList()
                    .Select(p => (p.Parent, p.Child))),
                "1:11", "3:31");
        }

        Check(
            "inner captured tag y",
            InnerPairs(parents.Join(children, (p, c) => p.Id == c.ParentId && c.Tag == capturedTag)
                .Select(p => new { Parent = p.Item1.Id, Child = p.Item2.Id })
                .ToList()
                .Select(p => (p.Parent, p.Child))),
            "2:21", "2:22");

        // A29: LEFT JOIN preserves every outer row; unmatched rows project a null right side, including
        // parent 4 (no child at all) and every parent whose only children fail the ON predicate.
        static IEnumerable<string> OuterPairs(IEnumerable<(long Parent, long? Child)> pairs)
            => pairs.Select(p => $"{p.Parent}:{(p.Child is null ? "null" : p.Child.Value.ToString())}");

        if (clickHouseBareNullResidual)
        {
            NotApplicable(
                "left nullable literal",
                "ClickHouse 25.8 mis-applies a bare IS NULL ON residual (rv1/live/clickhouse-join-on-null-residual.txt)");

            CheckLeftPreserved(
                "left captured 42",
                parents.LeftJoin(children, (p, c) => p.Id == c.ParentId && c.Amount == capturedAmount)
                    .Select(p => new { Parent = p.Item1.Id, Child = (long?)p.Item2.Id })
                    .ToList()
                    .Select(p => (p.Parent, p.Child)),
                1, 2, 2, 3, 4);

            CheckLeftPreserved(
                "left HasValue",
                parents.LeftJoin(children, (p, c) => p.Id == c.ParentId && c.Amount.HasValue)
                    .Select(p => new { Parent = p.Item1.Id, Child = (long?)p.Item2.Id })
                    .ToList()
                    .Select(p => (p.Parent, p.Child)),
                1, 2, 2, 3, 4);
        }
        else
        {
            Check(
                "left nullable literal",
                OuterPairs(parents.LeftJoin(children, (p, c) => p.Id == c.ParentId && c.Amount == (int?)null)
                    .Select(p => new { Parent = p.Item1.Id, Child = (long?)p.Item2.Id })
                    .ToList()
                    .Select(p => (p.Parent, p.Child))),
                "1:11", "2:null", "3:31", "4:null");

            Check(
                "left captured 42",
                OuterPairs(parents.LeftJoin(children, (p, c) => p.Id == c.ParentId && c.Amount == capturedAmount)
                    .Select(p => new { Parent = p.Item1.Id, Child = (long?)p.Item2.Id })
                    .ToList()
                    .Select(p => (p.Parent, p.Child))),
                "1:null", "2:21", "2:22", "3:null", "4:null");

            Check(
                "left HasValue",
                OuterPairs(parents.LeftJoin(children, (p, c) => p.Id == c.ParentId && c.Amount.HasValue)
                    .Select(p => new { Parent = p.Item1.Id, Child = (long?)p.Item2.Id })
                    .ToList()
                    .Select(p => (p.Parent, p.Child))),
                "1:12", "2:21", "2:22", "3:null", "4:null");
        }

        // RIGHT JOIN is executed only where the plan approves it (SQL Server, PostgreSQL); elsewhere the
        // form is out of the approved live scope and is not silently claimed as passing.
        if (provider is "sqlserver" or "postgres")
        {
            Check(
                "right nullable literal",
                children.RightJoin(parents, (c, p) => c.ParentId == p.Id && c.Amount == (int?)null)
                    .Select(p => new { Child = (long?)p.Item1.Id, Parent = p.Item2.Id })
                    .ToList()
                    .Select(p => $"{(p.Child is null ? "null" : p.Child.Value.ToString())}:{p.Parent}"),
                "11:1", "null:2", "31:3", "null:4");
        }
        else
        {
            log($"provider={provider} test=T296b_JoinOn row=A29 right-join=not-applicable (outside the approved live scope)");
        }
    }

    private static string CollapseNullableSql(string sql)
        => string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Executes the prepared projection and returns the reader's CLR field types, in select order.</summary>
    private static Type[] ReaderFieldTypes<TResult>(IDataContext ctx, QueryCommand<TResult> command)
    {
        var context = (DataContext)ctx;
        context.EnsureConnectionOpen();
        var prepared = (DbPreparedQueryCommand<TResult>)context
            .GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, CancellationToken.None);
        using var reader = prepared.DbCommand.ExecuteReader();
        var types = new Type[reader.FieldCount];
        for (var i = 0; i < types.Length; i++)
            types[i] = reader.GetFieldType(i);
        return types;
    }
}

/// <summary>T296b row fixture: nullable integer and string columns with null, duplicate and unequal values.</summary>
[SqlTable("t296b_rows")]
public interface IT296bRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("n")]
    int? N { get; set; }

    [Column("s")]
    string? S { get; set; }
}

/// <summary>T296b parent fixture: parent 4 owns no child, so an outer join has an unmatched row.</summary>
[SqlTable("t296b_parent")]
public interface IT296bParent
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("label")]
    string? Label { get; set; }
}

/// <summary>T296b child fixture: parents own distinct children, with null and duplicated amounts.</summary>
[SqlTable("t296b_child")]
public interface IT296bChild
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("parent_id")]
    long ParentId { get; set; }

    [Column("amount")]
    int? Amount { get; set; }

    [Column("tag")]
    string? Tag { get; set; }
}
