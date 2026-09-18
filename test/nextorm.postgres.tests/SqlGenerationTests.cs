using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using nextorm.core;

namespace nextorm.postgres.tests;

/// <summary>
/// Verifies the PostgreSQL specific SQL dialect. These tests never open a database connection,
/// so they run on every build/CI.
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
    public void SelectDistinct_ShouldEmitDistinct()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Distinct().Select(x => new { x.Id })).Should().Be("select distinct id from simple_entity");
    }

    [Fact]
    public void SelectDistinctWithUnionAll_ShouldKeepDistinctInLeftBranch()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var cmd = e.Select(x => x.Id).Distinct().UnionAll(e.Select(x => x.Id));
        var sql = SqlOf(ctx, cmd);

        sql.Should().Contain("select distinct id from simple_entity");
        sql.Should().Contain(" union all ");
        sql.Should().Contain("select id from simple_entity");
    }

    [Fact]
    public void Union_ShouldEmitUnion()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Union(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n union \nselect id from simple_entity");
    }

    [Fact]
    public void UnionAll_ShouldEmitUnionAll()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).UnionAll(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n union all \nselect id from simple_entity");
    }

    [Fact]
    public void Intersect_ShouldEmitIntersect()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Intersect(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n intersect \nselect id from simple_entity");
    }

    [Fact]
    public void Except_ShouldEmitExcept()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Except(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n except \nselect id from simple_entity");
    }

    [Fact]
    public void IntersectAll_ShouldEmitIntersectAll()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).IntersectAll(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n intersect all \nselect id from simple_entity");
    }

    [Fact]
    public void ExceptAll_ShouldEmitExceptAll()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).ExceptAll(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n except all \nselect id from simple_entity");
    }

    [Fact]
    public void SelectBasic_ShouldProducePlainSelect()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void Parameter_ShouldUseAtPrefix()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == NORM.Param<int>(0)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = @norm_p0");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void Paging_WithLimitAndOffset_ShouldUseLimitOffset()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Page(5, 10).Select(x => x.Id)).Should().EndWith("limit 5 offset 10");
    }

    [Fact]
    public void Paging_WithOffsetOnly_ShouldNotEmitLimit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Offset(10).Select(x => x.Id));

        sql.Should().EndWith("offset 10");
        sql.Should().NotContain("limit");
    }

    [Fact]
    public void BooleanLiteral_ShouldUseTrueKeyword()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.Boolean == true).Select(x => x.Boolean)).Should().Contain("= true");
    }

    [Fact]
    public void StringConcat_ShouldUseDoublePipe()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String + "/" + x.String }));

        sql.Should().Contain("||");
        sql.Should().Contain("as \"V\"");
    }

    [Fact]
    public void Coalesce_ShouldUseCoalesceFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String ?? "" })).Should().Contain("coalesce(");
    }

    [Fact]
    public void Count_ShouldUseCountStar()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.count())).Should().Contain("count(*)");
    }

    [Fact]
    public void CountBig_ShouldUseCountStar()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.count_big())).Should().Contain("count(*)");
    }

    [Fact]
    public void Stdev_ShouldMapToStddev()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => NORM.SQL.stdev((double)x.Id)));

        sql.Should().Contain("stddev(");
        sql.Should().NotContain("stdev(");
    }

    [Fact]
    public void Variance_ShouldMapToVarPop()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => NORM.SQL.varp((double)x.Id)));

        sql.Should().Contain("var_pop(");
        sql.Should().NotContain("varp(");
    }

    [Fact]
    public void ComputedColumn_ShouldBeAliasedWithDoubleQuotes()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { x.Id, Calc = x.Id + 1 }));

        sql.Should().Contain("as \"Calc\"");
        sql.Should().NotContain("as '");
    }

    [Fact]
    public void NestedCalculatedColumn_ShouldReferenceInnerAlias()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var nested = e.Select(x => new { x.Id, Calc = x.String + x.String });
        var sql = SqlOf(ctx, ctx.From(nested).Select(t => new { t.Id, t.Calc }));

        sql.Should().Contain("as \"Calc\"");
        sql.Should().Contain(") as \"t1\"");
    }

    [Fact]
    public void Subquery_ShouldAlwaysHaveAlias()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var nested = e.Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.From(nested).Select(t => new { t.Id }));

        sql.Should().Contain(") as \"t1\"");
    }

    [Fact]
    public void Join_ShouldQuoteTableAliases()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.Join(complex, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain("as \"t1\"");
        sql.Should().Contain("as \"t2\"");
    }

    [Fact]
    public void LeftJoin_ShouldEmitLeftJoinWithOn()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" left join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void RightJoin_ShouldEmitRightJoinWithOn()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.RightJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" right join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void FullJoin_ShouldEmitFullJoinWithOn()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.FullJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" full join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void CrossJoin_ShouldEmitCrossJoinWithoutOn()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.CrossJoin(complex).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" cross join complex_entity");
        sql.Should().NotContain(" on ");
    }

    [Fact]
    public void CrossApply_ShouldEmitCrossJoinLateral()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.CrossApply(complex).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" cross join lateral complex_entity as \"t2\"");
        sql.Should().NotContain(" on true");
    }

    [Fact]
    public void OuterApply_ShouldEmitLeftJoinLateralOnTrue()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.OuterApply(complex).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" left join lateral complex_entity as \"t2\" on true");
    }

    [Fact]
    public void CrossApply_ToSubquery_ShouldEmitDerivedTable()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var subQuery = ctx.From<IComplexEntity>().Where(c => c.Id > 1).Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, simple.CrossApply(subQuery).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain("cross join lateral (select id, somestring as \"String\" from complex_entity");
        sql.Should().Contain(") as \"t2\"");
    }

    [Fact]
    public void QueryHint_ShouldThrowBecausePostgresHasNoQueryHints()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { x.Id }).Hint("recompile"));

        act.Should().Throw<NotSupportedException>().WithMessage("*Query hints*");
    }

    [Fact]
    public void Escape_ShouldUseDoubleQuotes()
    {
        using var ctx = PostgresTestContext.CreatePostgres();

        ctx.Dialect.Escape("Some Alias").Should().Be("\"Some Alias\"");
    }

    [Fact]
    public void Conditional_ShouldEmitCaseWhen()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.Id > 1 ? "big" : "small" }));

        sql.Should().Contain("case when (id > 1) then 'big' else 'small' end");
        sql.Should().Contain("as \"V\"");
    }

    [Fact]
    public void NestedConditional_ShouldEmitNestedCaseWhen()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.Boolean == true ? (x.Int == null ? "a" : "b") : "c" }));

        sql.Should().Contain("case when b = true then case when nullableint is null then 'a' else 'b' end else 'c' end");
    }

    [Fact]
    public void Conditional_InWhere_ShouldEmitCaseWhen()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => (x.Int == null ? 0 : x.Int) == 1).Select(x => new { x.Id }));

        sql.Should().Contain("case when nullableint is null then 0 else nullableint end");
    }

    [Fact]
    public void ConditionalBoolean_ShouldEmitBooleanLiterals()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // PostgreSQL has a boolean type usable as a value and as a predicate, so the CASE stays ANSI.
        var sql = SqlOf(ctx, e.Select(x => new { V = x.Id > 1 ? true : false }));

        sql.Should().Contain("case when (id > 1) then true else false end");
    }

    [Fact]
    public void Switch_ShouldEmitSearchedCase()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(SwitchOfId("other", (1L, "one"), (2L, "two"))));

        sql.Should().Contain("case when id = 1 then 'one' when id = 2 then 'two' else 'other' end");
    }

    [Fact]
    public void StringToUpper_ShouldUseUpperFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.ToUpper() })).Should().Contain("upper(somestring)");
    }

    [Fact]
    public void SqlFunction_OverColumn_ShouldEmitMappedFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Udf.ToUpper(x.String!) }))
            .Should().Be("select upper(somestring) as \"V\" from complex_entity");
    }

    [Fact]
    public void SqlFunction_WithCapturedArgument_ShouldEmitParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var start = 1;

        var command = Prepare(ctx, e.Select(x => new { V = Udf.Slice(x.String!, start) }));

        Normalize(command.DbCommand.CommandText).Should().Be("select substr(somestring, @start) as \"V\" from complex_entity");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("start");
    }

    [Fact]
    public void SqlFunction_WithSchema_ShouldQualifyName()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Udf.WithSchema(x.Id) }))
            .Should().Be("select dbo.my_fn(id) as \"V\" from complex_entity");
    }

    private static class Udf
    {
        [SqlFunction("upper")]
        public static string ToUpper(string value) => throw new NotSupportedException();

        [SqlFunction("substr")]
        public static string Slice(string value, int start) => throw new NotSupportedException();

        [SqlFunction("my_fn", Schema = "dbo")]
        public static long WithSchema(long value) => throw new NotSupportedException();
    }

    [Fact]
    public void Contains_ShouldUseLikeWithWildcards()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Contains("df")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%df%'");
    }

    [Fact]
    public void Contains_WithWildcard_ShouldEscapeAndEmitEscapeClause()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Contains("a%b_c")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%a\\%b\\_c%' escape '\\'");
    }

    [Fact]
    public void Substring_ShouldUseOneBasedOffset()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Substring(1, 2) }))
            .Should().Contain("substring(somestring, 1 + 1, 2)");
    }

    [Fact]
    public void StringLength_ShouldUseLengthFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Length })).Should().Contain("length(somestring)");
    }

    [Fact]
    public void Trim_ShouldUseTrimFunctions()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Trim() })).Should().Contain("trim(somestring)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.TrimStart() })).Should().Contain("ltrim(somestring)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.TrimEnd() })).Should().Contain("rtrim(somestring)");
    }

    [Fact]
    public void Replace_ShouldUseReplaceFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Replace("a", "b") }))
            .Should().Contain("replace(somestring, 'a', 'b')");
    }

    [Fact]
    public void StringIsNullOrEmpty_ShouldEmitNullCheck()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.IsNullOrEmpty(x.String)).Select(x => new { x.Id }))
            .Should().Contain("(somestring is null or somestring = '')");
    }

    [Fact]
    public void Like_ShouldEmitLikePredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => NORM.SQL.like(x.String, "%a%")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%a%'");
    }

    [Fact]
    public void MathAbs_ShouldUseAbsFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Math.Abs(x.Id - 5) })).Should().Contain("abs((id - 5))");
    }

    [Fact]
    public void MathRound_ShouldUseRoundFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Math.Round(x.Id / 2.0 + 0.2) }))
            .Should().Contain("round(((cast(id as double precision) / 2) + 0.2))");
    }

    [Fact]
    public void MathLog_ShouldUseNaturalLogarithm()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // PostgreSQL's log() is base 10, so Math.Log must map to ln().
        SqlOf(ctx, e.Select(x => new { V = Math.Log(x.Id + 1.0) })).Should().Contain("ln(");
    }

    [Fact]
    public void MathTruncate_ShouldUseTruncFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Math.Truncate(x.Id + 0.0) })).Should().Contain("trunc(");
    }

    [Fact]
    public void DateTimeNow_ShouldUseNowFunctions()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { N = DateTime.Now })).Should().Contain("now()");
        SqlOf(ctx, e.Select(x => new { N = DateTime.UtcNow })).Should().Contain("now() at time zone 'utc'");
    }

    [Fact]
    public void DateTimePart_ShouldUseExtractFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { Y = x.Datetime!.Value.Year })).Should().Contain("extract(year from dt)");
        SqlOf(ctx, e.Select(x => new { D = x.Datetime!.Value.Day })).Should().Contain("extract(day from dt)");
    }

    [Fact]
    public void InValues_ShouldRenderInPredicateWithParameters()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new long[] { 1, 2, 3 };

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0, @p1, @p2)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2");
    }

    [Fact]
    public void InValues_InlineParams_ShouldBecomeParameters()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Id, 1L, 2L)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0, @p1)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void InValues_Empty_ShouldRenderAlwaysFalse()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = Array.Empty<long>();

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("1 = 0");
        command.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
    }

    [Fact]
    public void InValues_SingleElement_ShouldRenderInPredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new long[] { 2 };

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0");
    }

    [Fact]
    public void InValues_WithNull_ShouldAddNullBranch()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new int?[] { 1, null };

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Int, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("(nullableint in (@p0) or nullableint is null)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0");
    }

    [Fact]
    public void InValues_AllNull_ShouldRenderIsNull()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new int?[] { null };

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Int, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("nullableint is null");
        Normalize(command.DbCommand.CommandText).Should().NotContain(" in (");
        command.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
    }

    [Fact]
    public void Contains_CapturedList_ShouldRenderInPredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new List<long> { 1, 2 };

        var command = Prepare(ctx, e.Where(x => values.Contains(x.Id)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0, @p1)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void Contains_CapturedArray_ShouldRenderInPredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new long[] { 1, 2 };

        var command = Prepare(ctx, e.Where(x => values.Contains(x.Id)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0, @p1)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void AnyArray_WithRuntimeParam_ShouldRenderAnyPredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == NORM.SQL.any(NORM.Param<long[]>(0))).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = any(@norm_p0)");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void AnyArray_PredicateFormWithCapturedArray_ShouldBindWholeArrayAsSingleParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new long[] { 1, 2, 3 };

        var command = Prepare(ctx, e.Where(x => NORM.SQL.any(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = any(@p0)");
        Normalize(command.DbCommand.CommandText).Should().NotContain(" in (");

        var parameter = command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle().Subject;
        parameter.Value.Should().BeSameAs(values);
    }

    [Fact]
    public void AllArray_WithRuntimeParam_ShouldRenderAllPredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == NORM.SQL.all(NORM.Param<long[]>(0))).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = all(@norm_p0)");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void InlineArray_ShouldBindAsSingleParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == NORM.SQL.any(new long[] { 1, 2, 3 })).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = any(@p0)");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle();
    }

    [Fact]
    public void ArrayFunction_Cardinality_ShouldEmitFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { N = NORM.SQL.cardinality(NORM.Param<long[]>(0)) }))
            .Should().Contain("cardinality(@norm_p0)");
    }

    [Fact]
    public void ArrayFunction_ArrayLengthAndPosition_ShouldEmitFunctions()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e
            .Where(x => NORM.SQL.array_length(NORM.Param<long[]>(0), 1) == 3)
            .Where(x => NORM.SQL.array_position(NORM.Param<long[]>(1), x.Id) == 1)
            .Select(x => new { x.Id }));

        var sql = Normalize(command.DbCommand.CommandText);
        sql.Should().Contain("array_length(@norm_p0, 1)");
        sql.Should().Contain("array_position(@norm_p1, id)");
    }

    [Fact]
    public void ArrayFunction_ArrayToString_ShouldEmitFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { S = NORM.SQL.array_to_string(NORM.Param<long[]>(0), ",") }))
            .Should().Contain("array_to_string(@norm_p0, ',')");
    }

    [Fact]
    public void ArrayOperator_ContainsAndOverlaps_ShouldEmitOperators()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e
            .Where(x => NORM.SQL.array_contains(NORM.Param<long[]>(0), NORM.Param<long[]>(1)))
            .Where(x => NORM.SQL.array_overlaps(NORM.Param<long[]>(2), NORM.Param<long[]>(3)))
            .Select(x => new { x.Id }));

        var sql = Normalize(command.DbCommand.CommandText);
        sql.Should().Contain("(@norm_p0 @> @norm_p1)");
        sql.Should().Contain("(@norm_p2 && @norm_p3)");
    }

    [Fact]
    public void JsonAgg_ShouldEmitJsonAggregate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.json_agg(x.String))).Should().Contain("json_agg(somestring)");
        SqlOf(ctx, e.Select(x => NORM.SQL.jsonb_agg(x.String))).Should().Contain("jsonb_agg(somestring)");
    }

    [Fact]
    public void JsonObjectAgg_ShouldEmitAggregate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.jsonb_object_agg(x.Id, x.String)))
            .Should().Contain("jsonb_object_agg(id, somestring)");
    }

    [Fact]
    public void JsonBuildObject_ShouldEmitFunctionWithKeyValueArguments()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = NORM.SQL.json_build_object("id", x.Id, "s", x.String) }))
            .Should().Contain("json_build_object('id', id, 's', somestring) as \"V\"");
    }

    [Fact]
    public void ToJsonb_ShouldEmitConversionFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = NORM.SQL.to_jsonb(x.Id) })).Should().Contain("to_jsonb(id)");
    }

    [Fact]
    public void JsonGet_WithParameterAndKey_ShouldEmitAccessOperator()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e
            .Where(x => NORM.SQL.json_get(NORM.Param<JsonDocument>(0), "name") == "x")
            .Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("@norm_p0 -> 'name'");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void JsonGetText_WithIntIndex_ShouldEmitTextAccessOperator()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e
            .Where(x => NORM.SQL.json_get_text(NORM.Param<JsonDocument>(0), 0) == "x")
            .Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("@norm_p0 ->> 0");
    }

    [Fact]
    public void JsonGetPath_WithCapturedArray_ShouldBindPathAsArrayParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var path = new[] { "a", "b" };

        var command = Prepare(ctx, e
            .Where(x => NORM.SQL.json_get_path(NORM.Param<JsonDocument>(0), path) == "x")
            .Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("@norm_p0 #> @p0");
        command.DbCommandParams.Cast<DbParameter>().Should().Contain(p => p.ParameterName == "p0");
    }

    [Fact]
    public void JsonPredicates_ShouldEmitContainmentAndExistenceOperators()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e
            .Where(x => NORM.SQL.json_contains(NORM.Param<JsonDocument>(0), NORM.Param<JsonDocument>(1)))
            .Where(x => NORM.SQL.json_exists(NORM.Param<JsonDocument>(2), "key"))
            .Where(x => NORM.SQL.json_exists_any(NORM.Param<JsonDocument>(3), new[] { "a", "b" }))
            .Where(x => NORM.SQL.json_exists_all(NORM.Param<JsonDocument>(4), new[] { "a", "b" }))
            .Select(x => new { x.Id }));

        var sql = Normalize(command.DbCommand.CommandText);
        sql.Should().Contain("@norm_p0 @> @norm_p1");
        sql.Should().Contain("@norm_p2 ? 'key'");
        sql.Should().Contain("@norm_p3 ?| @p0");
        sql.Should().Contain("@norm_p4 ?& @p1");
    }

    [Fact]
    public void JsonCast_ShouldEmitJsonbCast()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e
            .Where(x => NORM.SQL.json_exists(NORM.SQL.json_cast(NORM.Param<string>(0)), "key"))
            .Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("cast(@norm_p0 as jsonb) ? 'key'");
    }

    [Fact]
    public void JsonIntrospection_ShouldEmitFunctions()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            L = NORM.SQL.json_array_length(NORM.Param<JsonDocument>(0)),
            T = NORM.SQL.json_typeof(NORM.Param<JsonDocument>(1))
        }));

        sql.Should().Contain("json_array_length(@norm_p0)");
        sql.Should().Contain("json_typeof(@norm_p1)");
    }

    [Fact]
    public void NullIf_ShouldEmitNullIf()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = NORM.SQL.nullif(x.Id, 0L) }))
            .Should().Contain("nullif(id, 0) as \"V\"");
    }

    [Fact]
    public void GreatestLeast_ShouldEmitFunctions()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            G = NORM.SQL.greatest(x.Id, x.Id),
            L = NORM.SQL.least(x.Id, x.Id)
        }));

        sql.Should().Contain("greatest(id, id) as \"G\"");
        sql.Should().Contain("least(id, id) as \"L\"");
    }

    [Fact]
    public void DateTrunc_ShouldEmitFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { M = NORM.SQL.date_trunc("month", x.Datetime) }))
            .Should().Contain("date_trunc('month', dt) as \"M\"");
    }

    [Fact]
    public void DateArithmetic_ShouldEmitIntervalExpressions()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = NORM.SQL.date_add("day", 1, x.Datetime) }))
            .Should().Contain("dt + (1 * interval '1 day') as \"D\"");

        // Units PostgreSQL's interval literal has no part for are folded (decade -> 10 years).
        SqlOf(ctx, e.Select(x => new { D = NORM.SQL.date_add("decade", 2, x.Datetime) }))
            .Should().Contain("dt + (2 * interval '10 years')");

        SqlOf(ctx, e.Select(x => new { E = NORM.SQL.end_of_month(x.Datetime) }))
            .Should().Contain("(date_trunc('month', dt) + interval '1 month - 1 day') as \"E\"");
    }

    [Fact]
    public void DateTimeAddMethods_ShouldEmitIntervalExpressions()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = x.Datetime!.Value.AddDays(7) }))
            .Should().Contain("dt + (7 * interval '1 day') as \"D\"");

        SqlOf(ctx, e.Select(x => new { M = x.Datetime!.Value.AddMonths(2) }))
            .Should().Contain("dt + (2 * interval '1 month') as \"M\"");

        SqlOf(ctx, e.Select(x => new { S = x.Datetime!.Value.AddSeconds(30) }))
            .Should().Contain("dt + (30 * interval '1 second') as \"S\"");
    }

    [Fact]
    public void StringAndArrayAgg_ShouldEmitAggregates()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.string_agg(x.String, ",")))
            .Should().Contain("string_agg(somestring, ',')");
        // array_agg produces an array column, which the row reader cannot materialise yet; assert the
        // generated SQL through a predicate instead of a projection.
        SqlOf(ctx, e
            .GroupBy(x => new { x.Int })
            .Having(x => NORM.SQL.array_agg(x.Id) != null)
            .Select(x => new { x.Int }))
            .Should().Contain("array_agg(id)");
    }

    [Fact]
    public void FilteredAggregates_ShouldEmitFilterClause()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C = NORM.SQL.count(() => x.Id > 1L),
            S = NORM.SQL.sum(x.Id, () => x.Boolean!.Value),
            M = NORM.SQL.max(x.Int, () => x.Id > 0L)
        }));

        sql.Should().Contain("count(*) filter (where (id > 1))");
        sql.Should().Contain("sum(id) filter (where b)");
        sql.Should().Contain("max(nullableint) filter (where (id > 0))");
    }

    [Fact]
    public void StringAgg_WithFilter_ShouldEmitFilterClause()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.string_agg(x.String, ",", () => x.Id > 0L)))
            .Should().Contain("string_agg(somestring, ',') filter (where (id > 0))");
    }

    [Fact]
    public void TableFunction_GenerateSeries_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();
        var start = 1L;
        var stop = 3L;

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => NORM.SQL.generate_series(start, stop))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select generate_series as \"Value\" from generate_series(@start, @stop) as \"t1\"");
    }

    [Fact]
    public void TableFunction_Unnest_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => NORM.SQL.unnest(NORM.Param<long[]>(0)))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select unnest as \"Value\" from unnest(@norm_p0) as \"t1\"");
    }

    [Fact]
    public void LogicalNot_InWhere_ShouldEmitNotPredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => !x.Boolean!.Value).Select(x => new { x.Id }))
            .Should().Contain("where not (b)");
    }

    [Fact]
    public void LogicalNot_WhenProjected_ShouldEmitNotScalar()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // PostgreSQL has a boolean type valid as a scalar, so no cast/CASE is added around the negation.
        var sql = SqlOf(ctx, e.Select(x => !x.Boolean!.Value));

        sql.Should().Contain("not (b)");
        sql.Should().NotContain("cast(");
    }

    [Fact]
    public void Negate_ShouldParenthesiseOperand()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => -x.Id)).Should().Contain("-(id)");
    }

    [Fact]
    public void OnesComplement_ShouldEmitTilde()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => ~x.Id)).Should().Contain("~(id)");
    }

    [Fact]
    public void LogicalNot_OfAnd_ShouldNegateWholePredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => !(x.Boolean!.Value && x.Id > 1L)).Select(x => new { x.Id }))
            .Should().Contain("not ((b and (id > 1)))");
    }

    [Fact]
    public void LogicalNot_OfOr_ShouldNegateWholePredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => !(x.Boolean!.Value || x.Id > 1L)).Select(x => new { x.Id }))
            .Should().Contain("not ((b or (id > 1)))");
    }

    public sealed class CteNumberRow
    {
        public int n { get; set; }
    }

    [Fact]
    public void Cte_NonRecursive_ShouldEmitWithClause()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cte = e.Where(x => x.Id > 1).Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.With("recent", cte).From("recent").Select(t => new { id = t["id"].AsInt }));

        sql.Should().StartWith("with recent as (select id from complex_entity");
        sql.Should().Contain("where (id > 1))");
        sql.Should().EndWith("select id from recent");
    }

    [Fact]
    public void Cte_TwoChained_ShouldEmitBothWithClauses()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var first = e.Where(x => x.Id > 1).Select(x => new { x.Id });
        var second = ctx.From("first").Select(t => new { id = t["id"].AsInt });
        var sql = SqlOf(ctx, ctx.With("first", first).With("second", second).From("second").Select(t => new { id = t["id"].AsInt }));

        sql.Should().StartWith("with first as (select id from complex_entity");
        sql.Should().Contain("), second as (select id from first)");
        sql.Should().EndWith("select id from second");
    }

    [Fact]
    public void Cte_Recursive_ShouldEmitWithRecursiveKeyword()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var anchor = e.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.UnionAll(step);

        var sql = SqlOf(ctx, ctx.WithRecursive("nums", body).From("nums").Select(t => new CteNumberRow { n = t["n"].AsInt }));

        // PostgreSQL uses the ANSI `with recursive` form and has no maxrecursion option.
        sql.Should().StartWith("with recursive nums as (");
        sql.Should().Contain(" union all ");
        sql.Should().Contain("select n from nums");
        sql.Should().NotContain("maxrecursion");
    }

    [Fact]
    public void RowNumber_ShouldEmitOverWithPartitionAndOrder()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = NORM.SQL.row_number().Over(partitionBy: () => x.Int, orderBy: () => x.Id)
        }));

        sql.Should().Be("select id, row_number() over (partition by nullableint order by id) as \"rn\" from complex_entity");
    }

    [Fact]
    public void RankAndDenseRank_ShouldEmitOverWithOrder()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            r = NORM.SQL.rank().Over(NORM.SQL.asc(() => x.Id)),
            dr = NORM.SQL.dense_rank().Over(NORM.SQL.asc(() => x.Id))
        }));

        sql.Should().Be("select id, rank() over (order by id) as \"r\", dense_rank() over (order by id) as \"dr\" from complex_entity");
    }

    [Fact]
    public void WindowOrderByDescending_ShouldEmitDesc()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            r = NORM.SQL.row_number().Over(
                partitionBy: new Expression<Func<object?>>[] { () => x.Int },
                orderBy: new[] { NORM.SQL.desc(() => x.Id) })
        }));

        sql.Should().Be("select id, row_number() over (partition by nullableint order by id desc) as \"r\" from complex_entity");
    }

    [Fact]
    public void LagAndLead_ShouldEmitOffsetAndDefault()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            prev = NORM.SQL.lag(x.Id, 1, 0L).Over(NORM.SQL.asc(() => x.Id)),
            next = NORM.SQL.lead(x.Int, 2, 0).Over(NORM.SQL.asc(() => x.Id))
        }));

        sql.Should().Be("select id, lag(id, 1, 0) over (order by id) as \"prev\", lead(nullableint, 2, 0) over (order by id) as \"next\" from complex_entity");
    }

    [Fact]
    public void WindowAggregate_ShouldEmitOverPartition()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            total = NORM.SQL.sum_over(x.Id).Over(partitionBy: () => x.Int),
            n = NORM.SQL.count_over().Over(partitionBy: () => x.Int)
        }));

        sql.Should().Be("select id, sum(id) over (partition by nullableint) as \"total\", count(*) over (partition by nullableint) as \"n\" from complex_entity");
    }

    [Fact]
    public void WindowFrame_ShouldEmitRowsBetween()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            running = NORM.SQL.sum_over(x.Id).Over(
                NORM.SQL.asc(() => x.Id),
                NORM.WindowFrame.RowsUnboundedPrecedingToCurrentRow),
            sliding = NORM.SQL.sum_over(x.Id).Over(
                NORM.SQL.asc(() => x.Id),
                NORM.WindowFrame.Rows(1, 1))
        }));

        sql.Should().Be("select id, sum(id) over (order by id rows between unbounded preceding and current row) as \"running\", sum(id) over (order by id rows between 1 preceding and 1 following) as \"sliding\" from complex_entity");
    }

    [Fact]
    public void NtileAndFirstLastValue_ShouldEmitOver()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            bucket = NORM.SQL.ntile(2).Over(NORM.SQL.asc(() => x.Id)),
            first = NORM.SQL.first_value(x.Int).Over(partitionBy: () => x.Int, orderBy: () => x.Id),
            last = NORM.SQL.last_value(x.Int).Over(partitionBy: () => x.Int, orderBy: () => x.Id)
        }));

        sql.Should().Be("select id, ntile(2) over (order by id) as \"bucket\", first_value(nullableint) over (partition by nullableint order by id) as \"first\", last_value(nullableint) over (partition by nullableint order by id) as \"last\" from complex_entity");
    }

    [Fact]
    public void WindowFrame_FullBoundaries_ShouldEmitRowsAndRange()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            full = NORM.SQL.sum_over(x.Id).Over(
                NORM.SQL.desc(() => x.Id),
                NORM.WindowFrame.Rows(NORM.WindowFrameBound.UnboundedPreceding, NORM.WindowFrameBound.UnboundedFollowing)),
            range = NORM.SQL.sum_over(x.Id).Over(
                NORM.SQL.asc(() => x.Id),
                NORM.WindowFrame.RangeUnboundedPrecedingToCurrentRow)
        }));

        sql.Should().Contain("sum(id) over (order by id desc rows between unbounded preceding and unbounded following)");
        sql.Should().Contain("sum(id) over (order by id range between unbounded preceding and current row)");
    }

    [Fact]
    public void WindowFunction_WithoutOver_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { x.Id, rn = NORM.SQL.row_number() }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Over*");
    }

    public interface ITvfRow
    {
        [Column("id")]
        long Id { get; set; }
        [Column("value")]
        string? Value { get; set; }
    }

    private static class Tvf
    {
        [SqlTableFunction("all_rows")]
        public static IQueryable<ITvfRow> AllRows() => throw new NotSupportedException();

        [SqlTableFunction("rows_by_id")]
        public static IQueryable<ITvfRow> ById(long id) => throw new NotSupportedException();

        [SqlTableFunction("rows_between", Schema = "app")]
        public static IQueryable<ITvfRow> Between(long lo, long hi) => throw new NotSupportedException();
    }

    [Fact]
    public void TableFunction_NoArgs_ShouldEmitCallWithRequiredAlias()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.FromTableFunction(() => Tvf.AllRows()).Select(r => new { r.Id }))
            .Should().Be("select id from all_rows() as \"t1\"");
    }

    [Fact]
    public void TableFunction_WithArgument_ShouldEmitParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var id = 5L;

        var command = Prepare(ctx, ctx.FromTableFunction(() => Tvf.ById(id)).Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText).Should().Be("select id from rows_by_id(@id) as \"t1\"");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("id");
    }

    [Fact]
    public void TableFunction_TwoArgumentsAndSchema_ShouldEmitQualifiedCall()
    {
        using var ctx = PostgresTestContext.Create();
        var lo = 1L;
        var hi = 3L;

        var command = Prepare(ctx, ctx.FromTableFunction(() => Tvf.Between(lo, hi)).Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText).Should().Be("select id from app.rows_between(@lo, @hi) as \"t1\"");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("lo", "hi");
    }

    [Fact]
    public void TableFunction_JoinedToTable_ShouldAliasBothSources()
    {
        using var ctx = PostgresTestContext.Create();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, ctx
            .FromTableFunction(() => Tvf.AllRows())
            .Join(complex, (r, c) => r.Id == c.Id)
            .Select(p => new { p.t1.Value, p.t2.String }));

        sql.Should().Be("select t1.value, t2.somestring as \"String\" from all_rows() as \"t1\" join complex_entity as \"t2\" on t1.id = t2.id");
    }

    [Fact]
    public void TableFunction_AsJoinedSource_ShouldAliasBothSources()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, simple
            .Join(ctx.FromTableFunction(() => Tvf.AllRows()), (s, r) => r.Id == s.Id)
            .Select(p => new { p.t1.Id, p.t2.Value }));

        sql.Should().Be("select t1.id, t2.value from simple_entity as \"t1\" join all_rows() as \"t2\" on t2.id = cast(t1.id as bigint)");
    }

    [Fact]
    public void BooleanAggregates_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = NORM.SQL.bool_and(x.Boolean),
            B = NORM.SQL.bool_or(x.Boolean),
            C = NORM.SQL.every(x.Boolean)
        }));

        sql.Should().Contain("bool_and(b)");
        sql.Should().Contain("bool_or(b)");
        sql.Should().Contain("every(b)");
    }

    [Fact]
    public void BitAggregates_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = NORM.SQL.bit_and(x.Id),
            B = NORM.SQL.bit_or(x.Id),
            C = NORM.SQL.bit_xor(x.Id)
        }));

        sql.Should().Contain("bit_and(id)");
        sql.Should().Contain("bit_or(id)");
        sql.Should().Contain("bit_xor(id)");
    }

    [Fact]
    public void StatisticalAggregates_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = NORM.SQL.corr(x.Id, x.Id),
            B = NORM.SQL.covar_pop(x.Id, x.Id),
            C = NORM.SQL.covar_samp(x.Id, x.Id),
            D = NORM.SQL.regr_slope(x.Id, x.Id),
            E = NORM.SQL.regr_intercept(x.Id, x.Id),
            F = NORM.SQL.regr_r2(x.Id, x.Id),
            G = NORM.SQL.regr_count(x.Id, x.Id),
            H = NORM.SQL.regr_avgx(x.Id, x.Id),
            I = NORM.SQL.regr_avgy(x.Id, x.Id)
        }));

        sql.Should().Contain("corr(id, id)");
        sql.Should().Contain("covar_pop(id, id)");
        sql.Should().Contain("covar_samp(id, id)");
        sql.Should().Contain("regr_slope(id, id)");
        sql.Should().Contain("regr_intercept(id, id)");
        sql.Should().Contain("regr_r2(id, id)");
        sql.Should().Contain("regr_count(id, id)");
        sql.Should().Contain("regr_avgx(id, id)");
        sql.Should().Contain("regr_avgy(id, id)");
    }

    [Fact]
    public void OrderedSetAggregates_ShouldEmitWithinGroup()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = NORM.SQL.percentile_cont(0.5, () => x.Id),
            B = NORM.SQL.percentile_disc(0.5, () => x.Id),
            C = NORM.SQL.mode(() => x.String)
        }));

        sql.Should().Contain("percentile_cont(0.5) within group (order by id)");
        sql.Should().Contain("percentile_disc(0.5) within group (order by id)");
        sql.Should().Contain("mode() within group (order by somestring)");
    }

    [Fact]
    public void ExtendedMathFunctions_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = NORM.SQL.asin(x.Int),
            B = NORM.SQL.atan2(x.Int, x.Int),
            C = NORM.SQL.cbrt(x.Int),
            D = NORM.SQL.sinh(x.Int),
            E = NORM.SQL.degrees(x.Int),
            F = NORM.SQL.pi(),
            G = NORM.SQL.random(),
            H = NORM.SQL.mod(x.Id, 2L),
            I = NORM.SQL.gcd(x.Id, 2L),
            J = NORM.SQL.lcm(x.Id, 2L),
            K = NORM.SQL.factorial(x.Id),
            L = NORM.SQL.width_bucket(x.Int, 0.0, 10.0, 5)
        }));

        sql.Should().Contain("asin(");
        sql.Should().Contain("atan2(");
        sql.Should().Contain("cbrt(");
        sql.Should().Contain("sinh(");
        sql.Should().Contain("degrees(");
        sql.Should().Contain("pi()");
        sql.Should().Contain("random()");
        sql.Should().Contain("mod(id, 2)");
        sql.Should().Contain("gcd(id, 2)");
        sql.Should().Contain("lcm(id, 2)");
        sql.Should().Contain("factorial(id)");
        sql.Should().Contain("width_bucket(");
    }

    [Fact]
    public void ExtendedLogFunction_ShouldEmitTwoArgumentLog()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = NORM.SQL.log(2.0, x.Int) }))
            .Should().Contain("log(2, cast(nullableint as double precision))");
    }

    [Fact]
    public void ExtendedStringFunctions_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = NORM.SQL.split_part(x.String, ",", 1),
            B = NORM.SQL.strpos(x.String, "a"),
            C = NORM.SQL.left(x.String, 2),
            D = NORM.SQL.right(x.String, 2),
            E = NORM.SQL.lpad(x.String, 5, "0"),
            F = NORM.SQL.rpad(x.String, 5, "0"),
            G = NORM.SQL.repeat(x.String, 3),
            H = NORM.SQL.reverse(x.String),
            I = NORM.SQL.initcap(x.String),
            J = NORM.SQL.translate(x.String, "a", "b"),
            K = NORM.SQL.overlay(x.String, "XX", 2, 2),
            L = NORM.SQL.concat_ws(",", x.String, x.Id),
            M = NORM.SQL.format("%s", x.String),
            N = NORM.SQL.md5(x.String)
        }));

        sql.Should().Contain("split_part(somestring, ',', 1)");
        sql.Should().Contain("strpos(somestring, 'a')");
        sql.Should().Contain("left(somestring, 2)");
        sql.Should().Contain("right(somestring, 2)");
        sql.Should().Contain("lpad(somestring, 5, '0')");
        sql.Should().Contain("rpad(somestring, 5, '0')");
        sql.Should().Contain("repeat(somestring, 3)");
        sql.Should().Contain("reverse(somestring)");
        sql.Should().Contain("initcap(somestring)");
        sql.Should().Contain("translate(somestring, 'a', 'b')");
        sql.Should().Contain("overlay(somestring, 'XX', 2, 2)");
        sql.Should().Contain("concat_ws(',', somestring, id)");
        sql.Should().Contain("format('%s', somestring)");
        sql.Should().Contain("md5(somestring)");
    }

    [Fact]
    public void RegexFunctions_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => NORM.SQL.regexp_split_to_array(x.String, ",") != null)
            .Select(x => new
            {
                A = NORM.SQL.regexp_replace(x.String, "a", "b"),
                B = NORM.SQL.regexp_like(x.String, "^a"),
                D = NORM.SQL.regexp_count(x.String, "a"),
                E = NORM.SQL.regexp_instr(x.String, "a")
            }));

        sql.Should().Contain("regexp_replace(somestring, 'a', 'b')");
        sql.Should().Contain("regexp_like(somestring, '^a')");
        sql.Should().Contain("regexp_split_to_array(somestring, ',')");
        sql.Should().Contain("regexp_count(somestring, 'a')");
        sql.Should().Contain("regexp_instr(somestring, 'a')");
    }

    [Fact]
    public void ExtendedDateFunctions_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => NORM.SQL.age(x.Datetime, x.Datetime) != null)
            .Where(x => NORM.SQL.justify_days(NORM.Param<TimeSpan>(0)) != null)
            .Where(x => NORM.SQL.current_time() != null)
            .Where(x => NORM.SQL.localtime() != null)
            .Select(x => new
            {
                B = NORM.SQL.make_date(2020, 1, 1),
                D = NORM.SQL.to_char(x.Datetime, "YYYY"),
                E = NORM.SQL.to_date("2020-01-01", "YYYY-MM-DD"),
                F = NORM.SQL.to_number("1", "999"),
                G = NORM.SQL.to_timestamp(0.0),
                H = NORM.SQL.timezone("UTC", x.Datetime),
                I = NORM.SQL.extract("quarter", x.Datetime),
                J = NORM.SQL.current_date(),
                M = NORM.SQL.localtimestamp()
            }));

        sql.Should().Contain("age(dt, dt)");
        sql.Should().Contain("make_date(2020, 1, 1)");
        sql.Should().Contain("justify_days(@norm_p0)");
        sql.Should().Contain("to_char(dt, 'YYYY')");
        sql.Should().Contain("to_date('2020-01-01', 'YYYY-MM-DD')");
        sql.Should().Contain("to_number('1', '999')");
        sql.Should().Contain("to_timestamp(0)");
        sql.Should().Contain("timezone('UTC', dt)");
        sql.Should().Contain("extract(quarter from dt)");
        sql.Should().Contain("current_date");
        sql.Should().Contain("current_time");
        sql.Should().Contain("localtime");
        sql.Should().Contain("localtimestamp");
    }

    [Fact]
    public void MakeIntervalAndDateBin_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => NORM.SQL.make_interval(0, 1, 2, 3, 4, 5.0) != null)
            .Where(x => NORM.SQL.justify_hours(NORM.Param<TimeSpan>(1)) != null)
            .Select(x => new
            {
                B = NORM.SQL.date_bin(NORM.Param<TimeSpan>(0), x.Datetime, x.Datetime)
            }));

        sql.Should().Contain("make_interval(0, 1, 2, 3, 4, 5)");
        sql.Should().Contain("date_bin(@norm_p0, dt, dt)");
        sql.Should().Contain("justify_hours(@norm_p1)");
    }

    [Fact]
    public void NumNullsFunctions_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = NORM.SQL.num_nulls(x.Int, x.String),
            B = NORM.SQL.num_nonnulls(x.Int)
        }));

        sql.Should().Contain("num_nulls(nullableint, somestring)");
        sql.Should().Contain("num_nonnulls(nullableint)");
    }

    [Fact]
    public void ArrayFunctions_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => NORM.SQL.array_append(NORM.Param<long[]>(0), x.Id) != null)
            .Where(x => NORM.SQL.array_prepend(x.Id, NORM.Param<long[]>(1)) != null)
            .Where(x => NORM.SQL.array_cat(NORM.Param<long[]>(2), NORM.Param<long[]>(3)) != null)
            .Where(x => NORM.SQL.array_remove(NORM.Param<long[]>(4), x.Id) != null)
            .Where(x => NORM.SQL.array_replace(NORM.Param<long[]>(5), x.Id, x.Id) != null)
            .Where(x => NORM.SQL.array_fill(x.Id, 2, 3) != null)
            .Where(x => NORM.SQL.array_positions(NORM.Param<long[]>(7), x.Id) != null)
            .Where(x => NORM.SQL.array_reverse(NORM.Param<long[]>(8)) != null)
            .Where(x => NORM.SQL.array_sort(NORM.Param<long[]>(9)) != null)
            .Where(x => NORM.SQL.string_to_array(x.String, ",") != null)
            .Select(x => new
            {
                G = NORM.SQL.array_dims(NORM.Param<long[]>(6))
            }));

        sql.Should().Contain("array_append(@norm_p0, id)");
        sql.Should().Contain("array_prepend(id, @norm_p1)");
        sql.Should().Contain("array_cat(@norm_p2, @norm_p3)");
        sql.Should().Contain("array_remove(@norm_p4, id)");
        sql.Should().Contain("array_replace(@norm_p5, id, id)");
        sql.Should().Contain("array_fill(id, @");
        sql.Should().Contain("array_dims(@norm_p6)");
        sql.Should().Contain("array_positions(@norm_p7, id)");
        sql.Should().Contain("array_reverse(@norm_p8)");
        sql.Should().Contain("array_sort(@norm_p9)");
        sql.Should().Contain("string_to_array(somestring, ',')");
    }

    [Fact]
    public void ArrayOperators_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => NORM.SQL.array_contained_by(NORM.Param<long[]>(0), NORM.Param<long[]>(1)))
            .Where(x => NORM.SQL.array_concat(NORM.Param<long[]>(2), NORM.Param<long[]>(3)) != null)
            .Select(x => new { x.Id }));

        sql.Should().Contain("(@norm_p0 <@ @norm_p1)");
        sql.Should().Contain("(@norm_p2 || @norm_p3)");
    }

    [Fact]
    public void JsonFunctions_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = NORM.SQL.jsonb_set(NORM.Param<JsonDocument>(0), new[] { "a" }, 1),
            B = NORM.SQL.jsonb_insert(NORM.Param<JsonDocument>(1), new[] { "a" }, 1, true),
            C = NORM.SQL.jsonb_strip_nulls(NORM.Param<JsonDocument>(2)),
            D = NORM.SQL.jsonb_pretty(NORM.Param<JsonDocument>(3)),
            E = NORM.SQL.jsonb_delete(NORM.Param<JsonDocument>(4), "a"),
            F = NORM.SQL.jsonb_delete(NORM.Param<JsonDocument>(5), 0),
            G = NORM.SQL.row_to_json(NORM.Param<object>(6)),
            H = NORM.SQL.array_to_json(NORM.Param<long[]>(7)),
            I = NORM.SQL.json_concat(NORM.Param<JsonDocument>(8), NORM.Param<JsonDocument>(9))
        }));

        sql.Should().Contain("jsonb_set(@norm_p0, @");
        sql.Should().Contain("jsonb_insert(@norm_p1, @");
        sql.Should().Contain("jsonb_strip_nulls(@norm_p2)");
        sql.Should().Contain("jsonb_pretty(@norm_p3)");
        sql.Should().Contain("(@norm_p4 - 'a')");
        sql.Should().Contain("(@norm_p5 - 0)");
        sql.Should().Contain("row_to_json(@norm_p6)");
        sql.Should().Contain("array_to_json(@norm_p7)");
        sql.Should().Contain("(@norm_p8 || @norm_p9)");
    }

    [Fact]
    public void JsonPathFunctions_ShouldCastPathToJsonpath()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = NORM.SQL.jsonb_path_exists(NORM.Param<JsonDocument>(0), "$.a"),
            B = NORM.SQL.jsonb_path_match(NORM.Param<JsonDocument>(1), "$.a == 1"),
            C = NORM.SQL.jsonb_path_query_first(NORM.Param<JsonDocument>(2), "$.a"),
            D = NORM.SQL.jsonb_path_query_array(NORM.Param<JsonDocument>(3), "$.a")
        }));

        sql.Should().Contain("jsonb_path_exists(@norm_p0, cast('$.a' as jsonpath))");
        sql.Should().Contain("jsonb_path_match(@norm_p1, cast('$.a == 1' as jsonpath))");
        sql.Should().Contain("jsonb_path_query_first(@norm_p2, cast('$.a' as jsonpath))");
        sql.Should().Contain("jsonb_path_query_array(@norm_p3, cast('$.a' as jsonpath))");
    }

    private static Expression<Func<IComplexEntity, string>> SwitchOfId(string @default, params (long Test, string Result)[] cases)
    {
        var p = Expression.Parameter(typeof(IComplexEntity), "x");
        var switchCases = cases
            .Select(c => Expression.SwitchCase(Expression.Constant(c.Result), Expression.Constant(c.Test)))
            .ToArray();
        var body = Expression.Switch(Expression.Property(p, nameof(IComplexEntity.Id)), Expression.Constant(@default), switchCases);

        return Expression.Lambda<Func<IComplexEntity, string>>(body, p);
    }
}
