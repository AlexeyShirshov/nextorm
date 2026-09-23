using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// Verifies the MariaDB specific SQL dialect. These tests never open a database connection, so they
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
    public void KeywordCase_Upper_ShouldUppercaseDialectClauses()
    {
        using var ctx = MariaDbTestContext.CreateUppercase();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .GroupByRollup(x => new { x.Int, x.Boolean })
            .Select(x => new { x.Int, x.Boolean }))
            .Should().Contain("GROUP BY nullableint, b WITH ROLLUP");

        SqlOf(ctx, e.ForUpdate().Select(x => x.Int)).Should().EndWith("FOR UPDATE");
    }

    [Fact]
    public void Pivot_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e
            .Pivot(PivotAggregate.Count, s => s.Id, s => s.Id, PivotValue.Create("1"))
            .Select(t => new { V = t.GetNullableInt32("1") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*PIVOT*");
    }

    [Fact]
    public void SelectBasic_ShouldProducePlainSelect()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void QuotedIdentifiers_ShouldUseBackticks()
    {
        using var ctx = MariaDbTestContext.CreateQuoted();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select `id` from `simple_entity`");
    }

    [Fact]
    public void StringConcat_ShouldUseConcatFunction()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String + "/" + x.String }));

        sql.Should().Contain("concat(");
        sql.Should().NotContain("||");
    }

    [Fact]
    public void UuidGenerators_ShouldUseMariaDbNames()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            U = SqlFunctions.Sql.gen_random_uuid(),
            V7 = SqlFunctions.Sql.uuidv7()
        }));

        sql.Should().Contain("uuid_v4()");
        sql.Should().Contain("uuid_v7()");
    }

    [Fact]
    public void PercentileWindow_ShouldEmitWithinGroupOver()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C = SqlFunctions.Sql.percentile_cont(0.5, x.Id).Over(),
            D = SqlFunctions.Sql.percentile_disc(0.5, x.Id).Over()
        }));

        sql.Should().Contain("percentile_cont(0.5) within group (order by id) over ()");
        sql.Should().Contain("percentile_disc(0.5) within group (order by id) over ()");
    }

    [Fact]
    public void Iif_ShouldUseIfFunction()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.iif(x.Id > 0L, "yes", "no") }));

        sql.Should().Contain("if(");
        sql.Should().Contain("'yes', 'no')");
    }

    [Fact]
    public void Extract_ShouldUseMariaDbDatePartForms()
    {
        using var ctx = MariaDbTestContext.Create();
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
    public void SetSeed_ShouldThrowBecauseMariaDbHasNoStandaloneSeed()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { S = SqlFunctions.Postgres.setseed(0.5) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*setseed*");
    }

    [Fact]
    public void CryptoHash_ShouldThrowBecauseOnlyPostgresHasIt()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { H = SqlFunctions.Postgres.digest("abc", "sha256") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*digest/sha256*");
    }

    [Fact]
    public void PostgresTableFunctions_ShouldThrowBecauseOnlyPostgresHasThem()
    {
        using var ctx = MariaDbTestContext.Create();
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
    public void NthValue_ShouldEmitOverWithOrder()
    {
        using var ctx = MariaDbTestContext.Create();
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
        using var ctx = MariaDbTestContext.Create();
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
    public void WindowFrameGroups_ShouldThrowBecauseMariaDbRejectsGroups()
    {
        using var ctx = MariaDbTestContext.Create();
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
    public void WindowFrameExclusion_ShouldThrowBecauseMariaDbRejectsExclude()
    {
        using var ctx = MariaDbTestContext.Create();
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
    public void AnyValueAggregate_ShouldThrowBecauseMariaDbLacksAnyValue()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { A = SqlFunctions.Sql.any_agg(x.Id) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*ANY_VALUE/any*");
    }

    [Fact]
    public void IntersectAll_ShouldEmitIntersectAll()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).IntersectAll(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n intersect all \nselect id from simple_entity");
    }

    [Fact]
    public void ExceptAll_ShouldEmitExceptAll()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).ExceptAll(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n except all \nselect id from simple_entity");
    }

    [Fact]
    public void StringFunctionExtensions_ShouldUseMariaDbForms()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.IndexOf("b") }))
            .Should().Contain("case when (instr(somestring, 'b')) = 0 then -1 else (instr(somestring, 'b')) - 1 end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.LastIndexOf("b") }))
            .Should().Contain("instr(reverse(somestring), reverse('b'))");
        SqlOf(ctx, e.Select(x => new { V = x.String!.PadLeft(5, '0') }))
            .Should().Contain("case when char_length(somestring) >= (5) then somestring else concat(repeat('0', (5) - char_length(somestring)), somestring) end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.Insert(2, "x") }))
            .Should().Contain("insert(somestring, 2 + 1, 0, 'x')");
        SqlOf(ctx, e.Select(x => new { V = new string('*', 4) }))
            .Should().Contain("repeat('*', 4)");
    }

    [Fact]
    public void ForSystemTime_AsOf_ShouldRenderAsOf()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForSystemTime(TemporalClause.AsOf(new DateTime(2020, 1, 1, 12, 0, 0))).Select(x => x.Id))
            .Should().EndWith("for system_time as of '2020-01-01 12:00:00'");
    }

    [Fact]
    public void ForSystemTime_ContainedIn_ShouldThrowBecauseMariaDbHasNoContainedIn()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.ForSystemTime(TemporalClause.ContainedIn(new DateTime(2020, 1, 1), new DateTime(2020, 2, 1))).Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR SYSTEM_TIME*");
    }

    [Fact]
    public void XmlMethods_ShouldThrowBecauseOnlySqlServerHasThem()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.xml_query(x.String, "/root") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*XML data-type methods*");
    }

    [Fact]
    public void XmlNodes_ShouldThrowBecauseOnlySqlServerHasThem()
    {
        using var ctx = MariaDbTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.String, "/root/item"))
            .Select(p => new { p.Item2.Value }));

        act.Should().Throw<NotSupportedException>().WithMessage("*xml.nodes*");
    }

    [Fact]
    public void DerivedSourceThenJoin_ShouldRenderDerivedTable()
    {
        using var ctx = MariaDbTestContext.Create();
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
        using var ctx = MariaDbTestContext.Create();
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
        using var ctx = MariaDbTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Where(d => d.Id > 5)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id }));

        sql.Should().Be("select t1.id, t2.id as `SId` from (select id, somestring as `String` from complex_entity) as `t1` join simple_entity as `t2` on t1.id = cast(t2.id as signed)\n where (t1.id > 5)");
    }
}
