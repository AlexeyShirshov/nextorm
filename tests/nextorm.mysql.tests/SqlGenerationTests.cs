using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// Verifies the MySQL specific SQL dialect. These tests never open a database connection, so they
/// run on every build/CI.
/// </summary>
public class SqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void IndexHint_UseForceIgnore_ShouldRenderMySqlForms()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithIndex("idx_id").Select(x => new { x.Id }))
            .Should().Be("select id from simple_entity use index (idx_id)");

        SqlOf(ctx, e.WithIndex(IndexHintKind.Force, "idx_id", "idx_other").Select(x => new { x.Id }))
            .Should().Be("select id from simple_entity force index (idx_id, idx_other)");

        SqlOf(ctx, e.WithIndex(IndexHintKind.Ignore, "idx_id").Select(x => new { x.Id }))
            .Should().Be("select id from simple_entity ignore index (idx_id)");
    }

    [Fact]
    public void IndexHint_WhitespaceNames_ShouldBeIgnored()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithIndex("  ").Select(x => new { x.Id }))
            .Should().Be("select id from simple_entity");
    }

    [Fact]
    public void IndexHint_WithoutIndex_ShouldThrowOnMySql()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.WithoutIndex().Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*at least one index*");
    }

    [Fact]
    public void Pivot_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e
            .Pivot(PivotAggregate.Count, s => s.Id, s => s.Id, PivotValue.Create("1"))
            .Select(t => new { V = t.GetNullableInt32("1") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*PIVOT*");
    }

    [Fact]
    public void SelectBasic_ShouldProducePlainSelect()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void QuotedIdentifiers_ShouldUseBackticks()
    {
        using var ctx = MySqlTestContext.CreateQuoted();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select `id` from `simple_entity`");
    }

    [Fact]
    public void StringConcat_ShouldUseConcatFunction()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String + "/" + x.String }));

        sql.Should().Contain("concat(");
        sql.Should().NotContain("||");
    }

    [Fact]
    public void AnyValueAggregate_ShouldUseAnyValueFunction()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.any_agg(x.String) }));

        sql.Should().Contain("ANY_VALUE(somestring)");
    }

    [Fact]
    public void PercentileWindow_ShouldThrowBecauseMySqlHasNoPercentile()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.percentile_cont(0.5, x.Id).Over() }));

        act.Should().Throw<NotSupportedException>().WithMessage("*percentile*");
    }

    [Fact]
    public void TextJsonFunctions_ShouldUseJsonExtractFamily()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Id = SqlFunctions.SqlServer.json_value(x.String, "$.id"),
            Name = SqlFunctions.SqlServer.json_query(x.String, "$.name"),
            Updated = SqlFunctions.SqlServer.json_modify(x.String, "$.id", "1")
        }));

        sql.Should().Contain("json_unquote(json_extract(somestring, '$.id'))");
        sql.Should().Contain("json_extract(somestring, '$.name')");
        sql.Should().Contain("json_set(somestring, '$.id', '1')");
    }

    [Fact]
    public void SessionInfoFunctions_ShouldUseMySqlNames()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            CU = SqlFunctions.Sql.current_user(),
            SU = SqlFunctions.Sql.session_user(),
            CS = SqlFunctions.Sql.current_schema(),
            CD = SqlFunctions.Sql.current_database(),
            Ver = SqlFunctions.Sql.version()
        }));

        sql.Should().Contain("current_user()");
        sql.Should().Contain("session_user()");
        sql.Should().Contain("schema()");
        sql.Should().Contain("database()");
        sql.Should().Contain("version()");
    }

    [Fact]
    public void UuidGenerators_ShouldThrowBecauseMySqlLacksThem()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // MySQL only has UUID() (v1); it has no v4/v7 generator, so both are rejected.
        Action v4 = () => SqlOf(ctx, e.Select(x => new { U = SqlFunctions.Sql.gen_random_uuid() }));
        Action v7 = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.uuidv7() }));

        v4.Should().Throw<NotSupportedException>().WithMessage("*UUID generator*");
        v7.Should().Throw<NotSupportedException>().WithMessage("*UUID generator*");
    }

    [Fact]
    public void IsJson_ShouldUseJsonValidPredicate()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.SqlServer.isjson(x.String)).Select(x => new { x.Id }))
            .Should().Contain("where (json_valid(somestring)) = 1");
    }

    [Fact]
    public void DateTimePart_ShouldUseDayofyear()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { DOY = x.Datetime!.Value.DayOfYear }))
            .Should().Contain("dayofyear(dt)");
    }

    [Fact]
    public void Extract_ShouldUseMySqlDatePartForms()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { Q = SqlFunctions.Sql.extract("quarter", x.Datetime) }))
            .Should().Contain("quarter(dt)");
        SqlOf(ctx, e.Select(x => new { W = SqlFunctions.Sql.extract("week", x.Datetime) }))
            .Should().Contain("weekofyear(dt)");
        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.extract("dow", x.Datetime) }))
            .Should().Contain("(dayofweek(dt) - 1)");
        SqlOf(ctx, e.Select(x => new { I = SqlFunctions.Sql.extract("isodow", x.Datetime) }))
            .Should().Contain("(weekday(dt) + 1)");
        SqlOf(ctx, e.Select(x => new { E = SqlFunctions.Sql.date_part("epoch", x.Datetime) }))
            .Should().Contain("cast(unix_timestamp(dt) as double)");
    }

    [Fact]
    public void SetSeed_ShouldThrowBecauseMySqlHasNoStandaloneSeed()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { S = SqlFunctions.Postgres.setseed(0.5) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*setseed*");
    }

    [Fact]
    public void CryptoHash_ShouldThrowBecauseOnlyPostgresHasIt()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { H = SqlFunctions.Postgres.digest("abc", "sha256") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*digest/sha256*");
    }

    [Fact]
    public void PostgresTableFunctions_ShouldThrowBecauseOnlyPostgresHasThem()
    {
        using var ctx = MySqlTestContext.Create();
        var json = """{"a":1}""";
        var source = "a,b";
        var pattern = ",";

        var arrayElements = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Postgres.jsonb_array_elements(json))
            .Select(r => new { r.Value }));
        arrayElements.Should().Throw<NotSupportedException>().WithMessage("*jsonb_array_elements*");

        var each = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Postgres.jsonb_each(json))
            .Select(r => new { r.Key }));
        each.Should().Throw<NotSupportedException>().WithMessage("*jsonb_each*");

        var split = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Postgres.regexp_split_to_table(source, pattern))
            .Select(r => new { r.Value }));
        split.Should().Throw<NotSupportedException>().WithMessage("*regexp_split_to_table*");
    }

    [Fact]
    public void PercentRankCumeDist_ShouldEmitOverWithOrder()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            pr = SqlFunctions.Sql.percent_rank().Over(SqlFunctions.Sql.asc(() => x.Id)),
            cd = SqlFunctions.Sql.cume_dist().Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Contain("percent_rank() over (order by id)");
        sql.Should().Contain("cume_dist() over (order by id)");
    }

    [Fact]
    public void Iif_ShouldUseIfFunction()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.iif(x.Id > 0L, "yes", "no") }));

        sql.Should().Contain("if(");
        sql.Should().Contain("'yes', 'no')");
    }

    [Fact]
    public void NthValue_ShouldEmitOverWithOrder()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            V = SqlFunctions.Sql.nth_value(x.Id, 2).Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Contain("nth_value(id, 2) over (order by id)");
    }

    [Fact]
    public void NamedWindow_ShouldEmitWindowClauseAndOverReference()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var b = e.Window("w", partitionBy: [x => x.Int], orderBy: [e.Asc(x => x.Id)]);
        var sql = SqlOf(ctx, b.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over("w")
        }));

        sql.Should().Contain("row_number() over w");
        sql.Should().Contain("window w as (partition by nullableint order by id)");
    }

    [Fact]
    public void WindowFrameGroups_ShouldThrowBecauseMySqlRejectsGroups()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            v = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.Groups(1, 1))
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*GROUPS*");
    }

    [Fact]
    public void WindowFrameExclusion_ShouldThrowBecauseMySqlRejectsExclude()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            v = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.RowsUnboundedPrecedingToCurrentRow.WithExclusion(WindowFrameExclusion.Ties))
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*EXCLUDE*");
    }

    [Fact]
    public void Parameter_ShouldUseAtPrefix()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == SqlFunctions.Parameter<int>(0)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = @norm_p0");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void Paging_WithLimitAndOffset_ShouldUseLimitOffset()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Page(5, 10).Select(x => x.Id)).Should().EndWith("limit 5 offset 10");
    }

    [Fact]
    public void CrossApply_OnPlainTable_ShouldEmitCrossJoinWithoutLateral()
    {
        using var ctx = MySqlTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.CrossApply(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" cross join complex_entity as `t2`");
        sql.Should().NotContain("lateral");
        sql.Should().NotContain(" on true");
    }

    [Fact]
    public void OuterApply_OnPlainTable_ShouldEmitLeftJoinOnTrueWithoutLateral()
    {
        using var ctx = MySqlTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.OuterApply(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" left join complex_entity as `t2` on true");
        sql.Should().NotContain("lateral");
    }

    [Fact]
    public void CrossApply_ToCorrelatedSubquery_ShouldEmitCrossJoinLateral()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .CrossApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id).Select(c => new { c.Id, c.String }))
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Be("select t1.id, t3.`String` from simple_entity as `t1` cross join lateral (select t2.id, t2.somestring as `String` from complex_entity as `t2`\n"
            + " where t2.id = cast(t1.id as signed)) as `t3`");
    }

    [Fact]
    public void OuterApply_ToCorrelatedSubquery_ShouldEmitLeftJoinLateralOnTrue()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .OuterApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id).Select(c => new { c.Id, c.String }))
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Be("select t1.id, t3.`String` from simple_entity as `t1` left join lateral (select t2.id, t2.somestring as `String` from complex_entity as `t2`\n"
            + " where t2.id = cast(t1.id as signed)) as `t3` on true");
    }

    [Fact]
    public void QueryHint_ShouldEmitInlineOptimizerHintComment()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { x.Id }).Hint("NO_RANGE_OPTIMIZATION(t1)"));

        sql.Should().Be("select /*+ NO_RANGE_OPTIMIZATION(t1) */ id from simple_entity");
    }

    [Fact]
    public void QueryHint_WithCte_ShouldPlaceHintAfterTheTopLevelSelect()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cte = e.Where(x => x.Id > 1).Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.With("recent", cte).From("recent").Select(t => new { id = t["id"].AsInt })
            .Hint("MAX_EXECUTION_TIME(1000)"));

        sql.Should().StartWith("with recent as (select id from complex_entity");
        sql.Should().Contain(") select /*+ MAX_EXECUTION_TIME(1000) */ id");
        sql.Should().Contain("from recent");
    }

    [Fact]
    public void GroupByRollup_ShouldUseWithRollup()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .GroupByRollup(x => new { x.Int, x.Boolean })
            .Select(x => new { x.Int, x.Boolean }))
            .Should().Contain("group by nullableint, b with rollup");
    }

    [Fact]
    public void KeywordCase_Upper_ShouldUppercaseDialectClauses()
    {
        using var ctx = MySqlTestContext.CreateUppercase();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .GroupByRollup(x => new { x.Int, x.Boolean })
            .Select(x => new { x.Int, x.Boolean }))
            .Should().Contain("GROUP BY nullableint, b WITH ROLLUP");

        SqlOf(ctx, e.ForUpdate().Select(x => x.Int)).Should().EndWith("FOR UPDATE");
        SqlOf(ctx, e.ForShare().Select(x => x.Int)).Should().EndWith("LOCK IN SHARE MODE");
        SqlOf(ctx, e.ForUpdate(LockWaitMode.SkipLocked).Select(x => x.Int)).Should().EndWith("FOR UPDATE SKIP LOCKED");
        SqlOf(ctx, e.ForShare(LockWaitMode.NoWait).Select(x => x.Int)).Should().EndWith("FOR SHARE NOWAIT");

        SqlOf(ctx, e.WithIndex(IndexHintKind.Force, "idx_int").Select(x => x.Int))
            .Should().Contain("FORCE INDEX (idx_int)");
    }

    [Fact]
    public void GroupByCube_ShouldThrowBecauseMySqlHasNoCube()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e
            .GroupByCube(x => new { x.Int })
            .Select(x => new { x.Int }));

        act.Should().Throw<NotSupportedException>().WithMessage("*CUBE*");
    }

    [Fact]
    public void GroupByGroupingSets_ShouldThrowBecauseMySqlHasNoGroupingSets()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e
            .GroupByGroupingSets(x => new { x.Int }, new[] { 0 })
            .Select(x => new { x.Int }));

        act.Should().Throw<NotSupportedException>().WithMessage("*GROUPING SETS*");
    }

    [Fact]
    public void StringAgg_ShouldUseGroupConcat()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.string_agg(x.String, ",")))
            .Should().Contain("group_concat(somestring separator ',')");
    }

    [Fact]
    public void FullTextPredicates_ShouldUseMatchAgainst()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.Sql.contains(x.String, "foo")).Select(x => new { x.Id }))
            .Should().Contain("where (match(somestring) against('foo' in boolean mode) > 0)");

        SqlOf(ctx, e.Where(x => SqlFunctions.Sql.freetext(x.String, "foo")).Select(x => new { x.Id }))
            .Should().Contain("where (match(somestring) against('foo') > 0)");
    }

    [Fact]
    public void DateArithmetic_ShouldUseDateAddTimestampDiffAndLastDay()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.date_add("day", 1, x.Datetime) }))
            .Should().Contain("date_add(dt, interval 1 day)");

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.date_diff("day", x.Datetime, x.Datetime) }))
            .Should().Contain("timestampdiff(day, dt, dt)");

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.end_of_month(x.Datetime) }))
            .Should().Contain("last_day(dt)");
    }

    [Fact]
    public void IndexOfLastIndexOf_ShouldUseInstrAndLocate()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.IndexOf("b") }))
            .Should().Contain("case when (instr(somestring, 'b')) = 0 then -1 else (instr(somestring, 'b')) - 1 end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.IndexOf("b", 1) }))
            .Should().Contain("case when (locate('b', somestring, 1 + 1)) = 0 then -1 else (locate('b', somestring, 1 + 1)) - 1 end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.LastIndexOf("b") }))
            .Should().Contain("case when (instr(reverse(somestring), reverse('b'))) = 0 then -1 else char_length(somestring) - (instr(reverse(somestring), reverse('b'))) - (char_length('b')) + 1 end");
    }

    [Fact]
    public void PadLeftRight_ShouldUseRepeat()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.PadLeft(5, '0') }))
            .Should().Contain("case when char_length(somestring) >= (5) then somestring else concat(repeat('0', (5) - char_length(somestring)), somestring) end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.PadRight(5) }))
            .Should().Contain("case when char_length(somestring) >= (5) then somestring else concat(somestring, repeat(' ', (5) - char_length(somestring))) end");
    }

    [Fact]
    public void RemoveInsertAndNewString_ShouldUseInsertAndRepeat()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Remove(2) }))
            .Should().Contain("substring(somestring, 1, 2)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.Remove(2, 1) }))
            .Should().Contain("insert(somestring, 2 + 1, 1, '')");
        SqlOf(ctx, e.Select(x => new { V = x.String!.Insert(2, "x") }))
            .Should().Contain("insert(somestring, 2 + 1, 0, 'x')");
        SqlOf(ctx, e.Select(x => new { V = new string('*', 4) }))
            .Should().Contain("repeat('*', 4)");
    }

    [Fact]
    public void ForUpdate_ShouldUseForUpdate()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForUpdate().Select(x => x.Id)).Should().EndWith("for update");
    }

    [Fact]
    public void ForShare_ShouldUseLockInShareMode()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForShare().Select(x => x.Id)).Should().EndWith("lock in share mode");
    }

    [Fact]
    public void ForUpdate_SkipLocked_ShouldEmitSkipLocked()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForUpdate(LockWaitMode.SkipLocked).Select(x => x.Id)).Should().EndWith("for update skip locked");
    }

    [Fact]
    public void ForShare_NoWait_ShouldEmitForShareNowait()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForShare(LockWaitMode.NoWait).Select(x => x.Id)).Should().EndWith("for share nowait");
    }

    [Fact]
    public void DistinctOn_ShouldThrowBecauseMySqlHasNoDistinctOn()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.DistinctOn(x => x.Id).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*DISTINCT ON*");
    }

    [Fact]
    public void TableSample_ShouldThrowBecauseMySqlHasNoTableSample()
    {
        using var ctx = MySqlTestContext.Create();
        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>(o => o.TableSample(10)).Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*TABLESAMPLE*");
    }

    [Fact]
    public void WithTies_ShouldThrowBecauseMySqlHasNoWithTies()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Limit(5).WithTies().Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*WITH TIES*");
    }

    [Fact]
    public void XmlMethods_ShouldThrowBecauseOnlySqlServerHasThem()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.xml_query(x.String, "/root") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*XML data-type methods*");
    }

    [Fact]
    public void XmlNodes_ShouldThrowBecauseOnlySqlServerHasThem()
    {
        using var ctx = MySqlTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.String, "/root/item"))
            .Select(p => new { p.Item2.Value }));

        act.Should().Throw<NotSupportedException>().WithMessage("*xml.nodes*");
    }

    [Fact]
    public void DerivedSourceThenJoin_ShouldRenderDerivedTable()
    {
        using var ctx = MySqlTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Where(c => c.Id > 0)
            .Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id, p.Item1.String }));

        sql.Should().Be("select t1.id, t2.id as `SId`, t1.`String` from (select id, somestring as `String` from complex_entity\n where (id > 0)) as `t1` join simple_entity as `t2` on t1.id = cast(t2.id as signed)");
    }

    [Fact]
    public void DerivedSourceWithJoinThenJoin_ShouldResolveTheOuterAlias()
    {
        using var ctx = MySqlTestContext.Create();
        var derived = ctx.From<ISimpleEntity>()
            .Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
            .Select(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<IComplexEntity>(), (d, c2) => d.OrderId == c2.Id)
            .Select(p => new { p.Item1.OrderId, p.Item1.CustomerName, Third = p.Item2.Id }));

        sql.Should().Be("select t3.`OrderId`, t3.`CustomerName`, t4.id as `Third` from (select t1.id as `OrderId`, t2.somestring as `CustomerName` from simple_entity as `t1` join complex_entity as `t2` on cast(t1.id as signed) = t2.id) as `t3` join complex_entity as `t4` on cast(t3.`OrderId` as signed) = t4.id");
    }

    [Fact]
    public void DerivedSourceWhereThenJoin_ShouldPushTheFilterOntoTheProjection()
    {
        using var ctx = MySqlTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Where(d => d.Id > 5)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id }));

        sql.Should().Be("select t1.id, t2.id as `SId` from (select id, somestring as `String` from complex_entity) as `t1` join simple_entity as `t2` on t1.id = cast(t2.id as signed)\n where (t1.id > 5)");
    }

    public interface IJsonTableRow
    {
        [Column("id")]
        int Id { get; set; }
        [Column("name")]
        string? Name { get; set; }
    }

    private static class JsonTableTvf
    {
        [SqlTableFunction("json_table", CallClause = ", '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')")]
        public static IQueryable<IJsonTableRow> JsonTable(string doc) => throw new NotSupportedException();
    }

    [Fact]
    public void TableFunction_CallClause_ShouldEmitJsonTableColumns()
    {
        using var ctx = MySqlTestContext.Create();
        var doc = "[]";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => JsonTableTvf.JsonTable(doc))
            .Select(r => new { r.Id, r.Name }));

        Normalize(command.DbCommand.CommandText).Should()
            .Be("select id, name from json_table(@doc, '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')) as `t1`");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("doc");
    }

    [Fact]
    public void FromSql_ShouldRenderDerivedTableWithNamedParameters()
    {
        using var ctx = MySqlTestContext.Create();
        var min = 1;

        var command = Prepare(ctx, ctx
            .FromSql("select id from complex_entity where id > @min", new { min })
            .Select(t => new { Id = t["id"].AsInt }));

        var sql = Normalize(command.DbCommand.CommandText);
        sql.Should().Contain("from (select id from complex_entity where id > @min) as `t1`");
        sql.Should().Contain("t1.id");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName).Should().Equal("min");
    }

    [Fact]
    public void FromSql_AsJoinedSource_ShouldRenderDerivedTableAndResolveColumns()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<ISimpleEntity>()
            .Join(ctx.FromSql("select id from complex_entity"), (s, r) => s.Id == r["id"].AsInt)
            .Select(p => new { p.Item1.Id, R = p.Item2["id"].AsInt }));

        sql.Should().Contain("join (select id from complex_entity) as `t2`");
        sql.Should().Contain("on t1.id = t2.id");
    }

    [Fact]
    public void DerivedSource_JoinOnFilteredPrimary_ShouldReferenceExposedColumnName()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Where(c => c.Int > 0)
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Int == (int?)s.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id }));

        sql.Should().Contain("nullableint as `Int`");
        sql.Should().Contain("on t1.`Int` = ");
        sql.Should().NotContain("t1.nullableint");
    }

    [Fact]
    public void ProjectedCommand_OrderByDescendingAndPage_ShouldResolveProjectionExpression()
    {
        using var ctx = MySqlTestContext.Create();

        var grouped = ctx.From<IComplexEntity>()
            .GroupBy(x => x.Int)
            .Select(x => new { x.Int, Cnt = SqlFunctions.Sql.count() });

        var sql = SqlOf(ctx, grouped.OrderByDescending(x => x.Cnt).Page(20, 1));

        sql.Should().Contain("group by nullableint");
        sql.Should().Contain("order by count(*) desc");
        sql.Should().Contain("limit 20 offset 1");
    }

}
