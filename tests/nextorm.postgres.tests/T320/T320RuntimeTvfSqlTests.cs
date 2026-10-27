using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// #320 (T320) PostgreSQL SQL generation for the runtime TVF source under a correlated APPLY. These
/// tests never open a database connection: the connection string is a placeholder and only the
/// rendered statement and its bound parameters are inspected.
/// </summary>
internal static class T320PgTvf
{
    public static TvfEntityBuilder<ISimpleEntity> Echo(long id) => new("t320_echo");

    public static TvfEntityBuilder<ISimpleEntity> EchoNullable(long? id) => new("t320_echo_nullable");

    public static TvfEntityBuilder<ISimpleEntity> EchoPair(long id, int extra) => new("t320_echo_pair");

    public static TvfEntityBuilder<ISimpleEntity> JoinSource(long id) => new("t320_join_probe");

    public static TvfEntityBuilder<ISimpleEntity> LeadingResultSchema(long id) => new(
        "t320_leading",
        null,
        null,
        null,
        Array.Empty<int>(),
        TableFunctionSchema.LeadingArgument);
}

/// <summary>Row shape of the <c>jsonb_to_record</c> alias-column-list cell (MX-PG R).</summary>
internal interface IT320PgRecordRow
{
    [Column("id")]
    int Id { get; set; }

    [Column("label")]
    string? Label { get; set; }
}

[Trait("Category", "T320")]
public class T320RuntimeTvfSqlTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static List<string> ParametersOf<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")];

    [Fact]
    public void CorrelatedRuntimeTvfApply_ShouldRenderLateralWithTheOuterColumnAndAParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var extra = 7;

        var prepared = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .CrossApply(row => T320PgTvf.EchoPair(row.Id, extra))
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            storeInCache: false);

        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain("cross join lateral", "PostgreSQL renders the correlated APPLY as a lateral join");
        sql.Should().Contain("t320_echo_pair", "the runtime factory supplies the applied source name");
        sql.Should().Contain("t1.id", "the correlated argument stays an outer column reference");
        sql.Should().Contain("@extra", "a captured argument is a bound parameter");
        sql.Should().NotContain("t320_echo_pair(7", "the outer column must never be evaluated into a constant");

        ParametersOf(prepared).Should().Equal("extra=7");
    }

    [Fact]
    public void CorrelatedRuntimeTvfApply_CacheHit_ShouldRefreshTheCaptureAndKeepTheOuterColumn()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();
        var extra = 7;

        QueryCommand<int> Build() => ctx.From<ISimpleEntity>()
            .CrossApply(row => T320PgTvf.EchoPair(row.Id, extra))
            .Select(p => p.Item1.Id);

        var first = Prepare(ctx, Build(), storeInCache: true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        ParametersOf(first).Should().Equal("extra=7");
        firstSql.Should().Contain("t1.id");

        extra = 8;
        var second = Prepare(ctx, Build(), storeInCache: true);

        ReferenceEquals(first, second).Should().BeTrue(
            "the same correlated shape must reuse the cached plan so the refresh path is exercised");
        Normalize(second.DbCommand.CommandText).Should().Be(firstSql, "a capture change must not re-render the statement");
        ParametersOf(second).Should().Equal(new[] { "extra=8" }, "the cache hit must bind the new capture");
        Normalize(second.DbCommand.CommandText).Should().Contain(
            "t1.id",
            "the correlated column stays a column reference, never an evaluated value");
    }

    // ---- C/N/X: the argument shapes ---------------------------------------------------------------

    [Fact]
    public void RuntimeTvf_C_CapturedArgument_ShouldBindAParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var id = 42L;

        var prepared = Prepare(ctx, ctx.From(() => T320PgTvf.Echo(id)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);

        sql.Should().Be("select id\n from t320_echo(@id) as \"t1\"", "a captured value is bound, never inlined");
        parameters.Should().Equal(new[] { "id=42" }, "the capture travels as one parameter with the captured value");
        sql.Should().NotContain("42", "a captured value must not be inlined into the statement text");
    }

    [Fact]
    public void RuntimeTvf_N_NullArgument_ShouldRenderNullWithoutAParameter()
    {
        using var ctx = PostgresTestContext.Create();

        var prepared = Prepare(ctx, ctx.From(() => T320PgTvf.EchoNullable(null)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);

        sql.Should().Be(
            "select id\n from t320_echo_nullable(null) as \"t1\"",
            "a null argument stays the SQL NULL literal, never a metadata-evaluation default");
        parameters.Should().BeEmpty("a null literal is not parameterised");
    }

    [Fact]
    public void RuntimeTvf_X_NestedSqlFunctionArgument_ShouldKeepTheNestedCallAndTheParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var captured = 42L;

        var prepared = Prepare(
            ctx,
            ctx.From(() => T320PgTvf.EchoNullable(SqlFunctions.Sql.nullif(captured, 0L))).Select(r => r.Id),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);

        sql.Should().Contain(
            "from t320_echo_nullable(nullif(@captured, 0))",
            "the nested SQL function is the argument, not its evaluated value");
        sql.Should().NotContain("t320_echo_nullable(42)", "the nested expression is never folded into a constant");
        parameters.Should().Equal(new[] { "captured=42" }, "the capture inside the nested call is a bound parameter");
    }

    // ---- J: ordinary lambda Join and a mixed source ---------------------------------------------

    [Fact]
    public void RuntimeTvf_J_OrdinaryJoinAndMixedSource_ShouldRenderTheTvfSource()
    {
        using var ctx = PostgresTestContext.Create();
        var joinId = 3L;

        var joined = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .Join(() => T320PgTvf.JoinSource(joinId), (left, right) => left.Id == right.Id)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var joinedSql = Normalize(joined.DbCommand.CommandText);
        joinedSql.Should().Contain(
            "join t320_join_probe(@joinId) as \"t2\"",
            "an ordinary lambda join renders the TVF as the right source");
        joinedSql.Should().NotContain("lateral", "an ordinary join is not an APPLY");
        ParametersOf(joined).Should().Equal(new[] { "joinId=3" });

        var fromId = 5L;
        var mixed = Prepare(
            ctx,
            ctx.From(() => T320PgTvf.Echo(fromId))
                .Join(ctx.From<ISimpleEntity>(), (tvf, entity) => tvf.Id == entity.Id)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var mixedSql = Normalize(mixed.DbCommand.CommandText);
        mixedSql.Should().Contain("from t320_echo(@fromId) as \"t1\"", "the TVF is the FROM source");
        mixedSql.Should().Contain("join simple_entity as \"t2\"", "the ordinary table composes as the joined source");
        ParametersOf(mixed).Should().Equal(new[] { "fromId=5" });
    }

    // ---- R: result placement ---------------------------------------------------------------------

    [Fact]
    public void RuntimeTvf_R_ResultPlacements_ShouldRenderNoneAndAliasColumnsAndRefuseLeading()
    {
        using var ctx = PostgresTestContext.Create();

        // None: the function supplies its own result shape, so the call is bare.
        var none = Prepare(ctx, ctx.From(() => T320PgTvf.Echo(42)).Select(r => r.Id), false);
        var noneSql = Normalize(none.DbCommand.CommandText);
        noneSql.Should().Be("select id\n from t320_echo(42) as \"t1\"", "None appends no column-definition list");

        // AliasColumnList: PostgreSQL's record functions carry the row-derived schema as the alias
        // column-definition list (jsonb_to_record(...) as "t1"(id integer, label text)).
        var json = "{\"id\":1,\"label\":\"a\"}";
        var record = Prepare(
            ctx,
            ctx.From(() => SqlFunctions.Postgres.jsonb_to_record<IT320PgRecordRow>(json)).Select(r => new { r.Id, r.Label }),
            false);
        var recordSql = Normalize(record.DbCommand.CommandText);
        recordSql.Should().Contain(
            "from jsonb_to_record(@json) as \"t1\"(id integer, label text)",
            "the alias column-definition list is rendered from the row type's mapped properties");
        ParametersOf(record).Should().Equal(new[] { "json={\"id\":1,\"label\":\"a\"}" });

        // LeadingArgument: PostgreSQL has no leading column-definition argument, so the form is a named
        // capability refusal, not silently dropped.
        Action leading = () => Prepare(ctx, ctx.From(() => T320PgTvf.LeadingResultSchema(42)).Select(r => r.Id), false);
        leading.Should().Throw<NotSupportedException>()
            .WithMessage("*'t320_leading'*'LeadingArgument' form*not supported by this provider*");
    }
}
