using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using nextorm.core;

namespace nextorm.sqlserver.tests;

/// <summary>
/// Verifies the Microsoft SQL Server specific SQL dialect. These tests never open a database
/// connection, so they run on every build/CI. Behavioural tests against a real server live in
/// nextorm.integration.tests.
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
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        SqlOf(ctx, e.Distinct().Select(x => new { x.Id })).Should().Be("select distinct id from simple_entity");
    }

    [Fact]
    public void SelectDistinctWithLimit_ShouldEmitDistinctBeforeTop()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        // SQL Server requires the DISTINCT keyword before TOP; "select top(5) distinct" is invalid.
        var sql = SqlOf(ctx, e.Distinct().Limit(5).Select(x => x.Id));

        sql.Should().StartWith("select distinct top(5) ");
        sql.Should().Contain("from simple_entity");
    }

    [Fact]
    public void SelectDistinctWithUnionAll_ShouldKeepDistinctInLeftBranch()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var cmd = e.Select(x => x.Id).Distinct().UnionAll(e.Select(x => x.Id));
        var sql = SqlOf(ctx, cmd);

        sql.Should().Contain("select distinct id from simple_entity");
        sql.Should().Contain(" union all ");
        sql.Should().Contain("select id from simple_entity");
    }

    [Fact]
    public void Union_ShouldEmitUnion()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Union(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n union \nselect id from simple_entity");
    }

    [Fact]
    public void UnionAll_ShouldEmitUnionAll()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).UnionAll(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n union all \nselect id from simple_entity");
    }

    [Fact]
    public void Intersect_ShouldEmitIntersect()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Intersect(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n intersect \nselect id from simple_entity");
    }

    [Fact]
    public void Except_ShouldEmitExcept()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Except(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n except \nselect id from simple_entity");
    }

    [Fact]
    public void IntersectAll_ShouldThrowBecauseSqlServerHasNoIntersectAll()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => x.Id).IntersectAll(e.Select(x => x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("*IntersectAll*");
    }

    [Fact]
    public void ExceptAll_ShouldThrowBecauseSqlServerHasNoExceptAll()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => x.Id).ExceptAll(e.Select(x => x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("*ExceptAll*");
    }

    [Fact]
    public void SelectBasic_ShouldProducePlainSelect()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void Parameter_ShouldUseAtPrefix()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == NORM.Param<int>(0)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = @norm_p0");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void Limit_ShouldUseTop()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Limit(5).Select(x => x.Id));

        // SQL Server can only apply TOP when there is no offset.
        sql.Should().StartWith("select top(5) ");
        sql.Should().NotContain("offset");
        sql.Should().NotContain("fetch");
    }

    [Fact]
    public void Paging_WithLimitAndOffset_ShouldUseOffsetFetch()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Page(5, 10).Select(x => x.Id));

        // OFFSET/FETCH requires an ORDER BY, so the provider injects an empty sort first.
        sql.Should().Contain("order by (select null as anyorder)");
        sql.Should().EndWith("offset 10 rows\nfetch next 5 rows only");
    }

    [Fact]
    public void Paging_WithOffsetOnly_ShouldNotEmitFetch()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Offset(10).Select(x => x.Id));

        sql.Should().EndWith("offset 10 rows");
        sql.Should().NotContain("fetch next");
    }

    [Fact]
    public void Paging_WithOrderBy_ShouldNotInjectEmptySorting()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Offset(10).OrderBy(x => x.Id).Select(x => x.Id));

        sql.Should().Contain("order by id");
        sql.Should().NotContain("anyorder");
    }

    [Fact]
    public void OrderByWithoutPaging_ShouldNotInjectEmptySorting()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Select(x => x.Id));

        // The empty sort exists only to make OFFSET/FETCH legal; a plain select must stay clean.
        sql.Should().NotContain("order by");
    }

    [Fact]
    public void BooleanLiteral_ShouldUseOne()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.Boolean == true).Select(x => x.Boolean)).Should().Contain("= 1");
    }

    [Fact]
    public void BooleanValueInWhere_ShouldBeComparedWithOne()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        // A bare bit column is a value, not a predicate, so T-SQL needs the comparison with 1.
        SqlOf(ctx, e.Where(x => x.Boolean!.Value).Select(x => new { x.Id }))
            .Should().Contain("where (b) = 1");
    }

    [Fact]
    public void BooleanValueAsLogicalOperand_ShouldBeComparedWithOne()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.Boolean!.Value && x.Id > 1L).Select(x => new { x.Id }))
            .Should().Contain("((b) = 1 and (id > 1))");
    }

    [Fact]
    public void StringConcat_ShouldUsePlusOperator()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String + "/" + x.String }));

        sql.Should().Contain("+");
        sql.Should().NotContain("||");
        sql.Should().Contain("as [V]");
    }

    [Fact]
    public void Coalesce_ShouldUseIsNullFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String ?? "" })).Should().Contain("isnull(");
    }

    [Fact]
    public void Count_ShouldUseCountStar()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.count())).Should().Contain("count(*)");
    }

    [Fact]
    public void CountBig_ShouldUseCountBigFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.count_big())).Should().Contain("count_big(*)");
    }

    [Fact]
    public void Aggregate_ShouldKeepProviderSpecificName()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        // SQL Server natively has stdev/var; no remapping is needed.
        SqlOf(ctx, e.Select(x => NORM.SQL.stdev((double)x.Id))).Should().Contain("stdev(");
    }

    [Fact]
    public void ComputedColumn_ShouldBeAliasedWithBrackets()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { x.Id, Calc = x.Id + 1 }));

        sql.Should().Contain("as [Calc]");
        sql.Should().NotContain("as '");
    }

    [Fact]
    public void NestedCalculatedColumn_ShouldReferenceInnerAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var nested = e.Select(x => new { x.Id, Calc = x.String + x.String });
        var sql = SqlOf(ctx, ctx.From(nested).Select(t => new { t.Id, t.Calc }));

        sql.Should().Contain("as [Calc]");
        sql.Should().Contain(") as [t1]");
    }

    [Fact]
    public void Subquery_ShouldAlwaysHaveAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var nested = e.Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.From(nested).Select(t => new { t.Id }));

        // A SQL Server derived table must be aliased; single quoted aliases are not valid here.
        sql.Should().Contain(") as [t1]");
        sql.Should().NotContain("as '");
    }

    [Fact]
    public void Join_ShouldQuoteTableAliases()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.Create<ISimpleEntity>();
        var complex = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, simple.Join(complex, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain("as [t1]");
        sql.Should().Contain("as [t2]");
    }

    [Fact]
    public void LeftJoin_ShouldEmitLeftJoinWithOn()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.Create<ISimpleEntity>();
        var complex = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" left join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void RightJoin_ShouldEmitRightJoinWithOn()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.Create<ISimpleEntity>();
        var complex = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, simple.RightJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" right join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void FullJoin_ShouldEmitFullJoinWithOn()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.Create<ISimpleEntity>();
        var complex = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, simple.FullJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" full join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void CrossJoin_ShouldEmitCrossJoinWithoutOn()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.Create<ISimpleEntity>();
        var complex = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, simple.CrossJoin(complex).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" cross join complex_entity");
        sql.Should().NotContain(" on ");
    }

    [Fact]
    public void AliasEscape_ShouldUseBrackets()
    {
        using var ctx = SqlServerTestContext.CreateSqlServer();

        ctx.Dialect.Escape("Some Alias").Should().Be("[Some Alias]");
    }

    [Fact]
    public void SubqueryAlias_ShouldBeRequired()
    {
        using var ctx = SqlServerTestContext.CreateSqlServer();

        ctx.Dialect.RequireSubqueryAlias.Should().BeTrue();
    }

    [Fact]
    public void Conditional_ShouldEmitCaseWhen()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.Id > 1 ? "big" : "small" }));

        sql.Should().Contain("case when (id > 1) then 'big' else 'small' end");
        sql.Should().Contain("as [V]");
    }

    [Fact]
    public void NestedConditional_ShouldEmitNestedCaseWhen()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.Boolean == true ? (x.Int == null ? "a" : "b") : "c" }));

        sql.Should().Contain("case when b = 1 then case when nullableint is null then 'a' else 'b' end else 'c' end");
    }

    [Fact]
    public void Conditional_InWhere_ShouldEmitCaseWhen()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => (x.Int == null ? 0 : x.Int) == 1).Select(x => new { x.Id }));

        sql.Should().Contain("case when nullableint is null then 0 else nullableint end");
    }

    [Fact]
    public void ConditionalBoolean_ShouldCastToBit()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        // SQL Server has no boolean type, so a boolean-valued CASE is materialised as a bit scalar.
        var sql = SqlOf(ctx, e.Select(x => new { V = x.Id > 1 ? true : false }));

        sql.Should().Contain("cast(case when (id > 1) then 1 else 0 end as bit)");
    }

    [Fact]
    public void ConditionalBoolean_InWhere_ShouldCompareWithOne()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        // A bit scalar is not a predicate, so a condition context adds "= 1".
        var sql = SqlOf(ctx, e.Where(x => x.Id > 1 ? true : false).Select(x => x.Id));

        sql.Should().Contain("cast(case when (id > 1) then 1 else 0 end as bit) = 1");
    }

    [Fact]
    public void Switch_ShouldEmitSearchedCase()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(SwitchOfId("other", (1L, "one"), (2L, "two"))));

        sql.Should().Contain("case when id = 1 then 'one' when id = 2 then 'two' else 'other' end");
    }

    [Fact]
    public void StringToUpper_ShouldUseUpperFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.ToUpper() })).Should().Contain("upper(somestring)");
    }

    [Fact]
    public void SqlFunction_OverColumn_ShouldEmitMappedFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Udf.ToUpper(x.String!) }))
            .Should().Be("select upper(somestring) as [V] from complex_entity");
    }

    [Fact]
    public void SqlFunction_WithCapturedArgument_ShouldEmitParameter()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();
        var start = 1;

        var command = Prepare(ctx, e.Select(x => new { V = Udf.Slice(x.String!, start) }));

        Normalize(command.DbCommand.CommandText).Should().Be("select substr(somestring, @start) as [V] from complex_entity");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("start");
    }

    [Fact]
    public void SqlFunction_WithSchema_ShouldQualifyName()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Udf.WithSchema(x.Id) }))
            .Should().Be("select dbo.my_fn(id) as [V] from complex_entity");
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
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Contains("df")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%df%'");
    }

    [Fact]
    public void Contains_WhenProjected_ShouldMaterialiseAsBit()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        // T-SQL has no boolean scalar, so a projected LIKE has to become a bit through a CASE.
        SqlOf(ctx, e.Select(x => new { V = x.String!.Contains("df") }))
            .Should().Contain("cast(case when somestring like '%df%' then 1 else 0 end as bit)");
    }

    [Fact]
    public void Substring_ShouldUseOneBasedOffsetWithLengthFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Substring(1) }))
            .Should().Contain("substring(somestring, 1 + 1, len(somestring) - (1))");
    }

    [Fact]
    public void StringLength_ShouldUseLenFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Length })).Should().Contain("len(somestring)");
    }

    [Fact]
    public void Trim_ShouldUseTrimFunctions()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Trim() })).Should().Contain("trim(somestring)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.TrimStart() })).Should().Contain("ltrim(somestring)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.TrimEnd() })).Should().Contain("rtrim(somestring)");
    }

    [Fact]
    public void Replace_ShouldUseReplaceFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Replace("a", "b") }))
            .Should().Contain("replace(somestring, 'a', 'b')");
    }

    [Fact]
    public void StringIsNullOrEmpty_ShouldEmitNullCheck()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.IsNullOrEmpty(x.String)).Select(x => new { x.Id }))
            .Should().Contain("(somestring is null or somestring = '')");
    }

    [Fact]
    public void Like_WithEscapeChar_ShouldEmitEscapeClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Where(x => NORM.SQL.like(x.String, "%a!%", "!")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%a!%' escape '!'");
    }

    [Fact]
    public void MathAbs_ShouldUseAbsFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Math.Abs(x.Id - 5) })).Should().Contain("abs((id - 5))");
    }

    [Fact]
    public void MathRound_ShouldSupplyDefaultLength()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        // T-SQL's round() requires the length argument.
        SqlOf(ctx, e.Select(x => new { V = Math.Round(x.Id / 2.0 + 0.2) }))
            .Should().Contain("round(((cast(id as float) / 2) + 0.2), 0)");
    }

    [Fact]
    public void MathTruncate_ShouldUseThreeArgumentRound()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        // T-SQL has no trunc; round(number, 0, 1) truncates.
        SqlOf(ctx, e.Select(x => new { V = Math.Truncate(x.Id + 0.0) }))
            .Should().Contain("round((cast(id as float) + 0), 0, 1)");
    }

    [Fact]
    public void MathLog_ShouldUseNaturalLogarithm()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Math.Log(x.Id + 1.0) })).Should().Contain("log(");
    }

    [Fact]
    public void DateTimeNow_ShouldUseGetDateFunctions()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { N = DateTime.Now })).Should().Contain("getdate()");
        SqlOf(ctx, e.Select(x => new { N = DateTime.UtcNow })).Should().Contain("getutcdate()");
    }

    [Fact]
    public void DateTimePart_ShouldUseDatepartFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { Y = x.Datetime!.Value.Year })).Should().Contain("datepart(year, dt)");
        SqlOf(ctx, e.Select(x => new { D = x.Datetime!.Value.Day })).Should().Contain("datepart(day, dt)");
    }

    [Fact]
    public void InValues_ShouldRenderInPredicateWithParameters()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();
        var values = new long[] { 1, 2, 3 };

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0, @p1, @p2)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2");
    }

    [Fact]
    public void InValues_InlineParams_ShouldBecomeParameters()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Id, 1L, 2L)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0, @p1)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void InValues_Empty_ShouldRenderAlwaysFalse()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();
        var values = Array.Empty<long>();

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("1 = 0");
        command.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
    }

    [Fact]
    public void InValues_SingleElement_ShouldRenderInPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();
        var values = new long[] { 2 };

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0");
    }

    [Fact]
    public void InValues_WithNull_ShouldAddNullBranch()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();
        var values = new int?[] { 1, null };

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Int, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("(nullableint in (@p0) or nullableint is null)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0");
    }

    [Fact]
    public void InValues_AllNull_ShouldRenderIsNull()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();
        var values = new int?[] { null };

        var command = Prepare(ctx, e.Where(x => NORM.SQL.@in(x.Int, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("nullableint is null");
        Normalize(command.DbCommand.CommandText).Should().NotContain(" in (");
        command.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
    }

    [Fact]
    public void Contains_CapturedList_ShouldRenderInPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();
        var values = new List<long> { 1, 2 };

        var command = Prepare(ctx, e.Where(x => values.Contains(x.Id)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0, @p1)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void Contains_CapturedArray_ShouldRenderInPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();
        var values = new long[] { 1, 2 };

        var command = Prepare(ctx, e.Where(x => values.Contains(x.Id)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0, @p1)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void LogicalNot_InWhere_ShouldEmitNotPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        // A bit column is a value, not a predicate, so it has to be compared with 1 before NOT.
        SqlOf(ctx, e.Where(x => !x.Boolean!.Value).Select(x => new { x.Id }))
            .Should().Contain("where not ((b) = 1)");
    }

    [Fact]
    public void LogicalNot_WhenProjected_ShouldMaterialiseAsBit()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        // T-SQL has no boolean scalar, so a projected NOT has to become a bit through a CASE, and
        // the bit column has to be compared with 1 before it can be used as the CASE test.
        SqlOf(ctx, e.Select(x => !x.Boolean!.Value))
            .Should().Contain("cast(case when not ((b) = 1) then 1 else 0 end as bit)");
    }

    [Fact]
    public void Negate_ShouldParenthesiseOperand()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => -x.Id)).Should().Contain("-(id)");
    }

    [Fact]
    public void OnesComplement_ShouldEmitTilde()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => ~x.Id)).Should().Contain("~(id)");
    }

    [Fact]
    public void LogicalNot_OfAnd_ShouldNegateWholePredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Where(x => !(x.Boolean!.Value && x.Id > 1L)).Select(x => new { x.Id }))
            .Should().Contain("not (((b) = 1 and (id > 1)))");
    }

    [Fact]
    public void LogicalNot_OfOr_ShouldNegateWholePredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Where(x => !(x.Boolean!.Value || x.Id > 1L)).Select(x => new { x.Id }))
            .Should().Contain("not (((b) = 1 or (id > 1)))");
    }

    public sealed class CteNumberRow
    {
        public int n { get; set; }
    }

    [Fact]
    public void Cte_NonRecursive_ShouldEmitWithClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var cte = e.Where(x => x.Id > 1).Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.With("recent", cte).From("recent").Select(t => new { id = t["id"].AsInt }));

        sql.Should().StartWith("with recent as (select id from complex_entity");
        sql.Should().Contain("where (id > 1))");
        sql.Should().EndWith("select id from recent");
    }

    [Fact]
    public void Cte_TwoChained_ShouldEmitBothWithClauses()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var first = e.Where(x => x.Id > 1).Select(x => new { x.Id });
        var second = ctx.From("first").Select(t => new { id = t["id"].AsInt });
        var sql = SqlOf(ctx, ctx.With("first", first).With("second", second).From("second").Select(t => new { id = t["id"].AsInt }));

        sql.Should().StartWith("with first as (select id from complex_entity");
        sql.Should().Contain("), second as (select id from first)");
        sql.Should().EndWith("select id from second");
    }

    [Fact]
    public void Cte_Recursive_ShouldOmitRecursiveKeywordButSupportMaxRecursion()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var anchor = e.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.UnionAll(step);

        var sql = SqlOf(ctx, ctx.WithRecursive("nums", body, 100).From("nums").Select(t => new CteNumberRow { n = t["n"].AsInt }));

        // T-SQL declares a recursive CTE with `with` alone (no RECURSIVE keyword).
        sql.Should().StartWith("with nums as (");
        sql.Should().NotContain("with recursive");
        sql.Should().Contain(" union all ");
        sql.Should().EndWith("option (maxrecursion 100)");
    }

    [Fact]
    public void RowNumber_ShouldEmitOverWithPartitionAndOrder()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = NORM.SQL.row_number().Over(partitionBy: () => x.Int, orderBy: () => x.Id)
        }));

        sql.Should().Be("select id, row_number() over (partition by nullableint order by id) as [rn] from complex_entity");
    }

    [Fact]
    public void RankAndDenseRank_ShouldEmitOverWithOrder()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            r = NORM.SQL.rank().Over(NORM.SQL.asc(() => x.Id)),
            dr = NORM.SQL.dense_rank().Over(NORM.SQL.asc(() => x.Id))
        }));

        sql.Should().Be("select id, rank() over (order by id) as [r], dense_rank() over (order by id) as [dr] from complex_entity");
    }

    [Fact]
    public void WindowOrderByDescending_ShouldEmitDesc()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            r = NORM.SQL.row_number().Over(
                partitionBy: new Expression<Func<object?>>[] { () => x.Int },
                orderBy: new[] { NORM.SQL.desc(() => x.Id) })
        }));

        sql.Should().Be("select id, row_number() over (partition by nullableint order by id desc) as [r] from complex_entity");
    }

    [Fact]
    public void LagAndLead_ShouldEmitOffsetAndDefault()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            prev = NORM.SQL.lag(x.Id, 1, 0L).Over(NORM.SQL.asc(() => x.Id)),
            next = NORM.SQL.lead(x.Int, 2, 0).Over(NORM.SQL.asc(() => x.Id))
        }));

        sql.Should().Be("select id, lag(id, 1, 0) over (order by id) as [prev], lead(nullableint, 2, 0) over (order by id) as [next] from complex_entity");
    }

    [Fact]
    public void WindowAggregate_ShouldEmitOverPartition()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            total = NORM.SQL.sum_over(x.Id).Over(partitionBy: () => x.Int),
            n = NORM.SQL.count_over().Over(partitionBy: () => x.Int)
        }));

        sql.Should().Be("select id, sum(id) over (partition by nullableint) as [total], count(*) over (partition by nullableint) as [n] from complex_entity");
    }

    [Fact]
    public void WindowFrame_ShouldEmitRowsBetween()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

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

        sql.Should().Be("select id, sum(id) over (order by id rows between unbounded preceding and current row) as [running], sum(id) over (order by id rows between 1 preceding and 1 following) as [sliding] from complex_entity");
    }

    [Fact]
    public void NtileAndFirstLastValue_ShouldEmitOver()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            bucket = NORM.SQL.ntile(2).Over(NORM.SQL.asc(() => x.Id)),
            first = NORM.SQL.first_value(x.Int).Over(partitionBy: () => x.Int, orderBy: () => x.Id),
            last = NORM.SQL.last_value(x.Int).Over(partitionBy: () => x.Int, orderBy: () => x.Id)
        }));

        sql.Should().Be("select id, ntile(2) over (order by id) as [bucket], first_value(nullableint) over (partition by nullableint order by id) as [first], last_value(nullableint) over (partition by nullableint order by id) as [last] from complex_entity");
    }

    [Fact]
    public void WindowFrame_FullBoundaries_ShouldEmitRowsAndRange()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

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
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

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
    public void TableFunction_NoArgs_ShouldEmitCallWithAlias()
    {
        using var ctx = SqlServerTestContext.Create();

        SqlOf(ctx, ctx.FromTableFunction(() => Tvf.AllRows()).Select(r => new { r.Id }))
            .Should().Be("select id from all_rows() as [t1]");
    }

    [Fact]
    public void TableFunction_WithArgument_ShouldEmitParameter()
    {
        using var ctx = SqlServerTestContext.Create();
        var id = 5L;

        var command = Prepare(ctx, ctx.FromTableFunction(() => Tvf.ById(id)).Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText).Should().Be("select id from rows_by_id(@id) as [t1]");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("id");
    }

    [Fact]
    public void TableFunction_TwoArgumentsAndSchema_ShouldEmitQualifiedCall()
    {
        using var ctx = SqlServerTestContext.Create();
        var lo = 1L;
        var hi = 3L;

        var command = Prepare(ctx, ctx.FromTableFunction(() => Tvf.Between(lo, hi)).Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText).Should().Be("select id from app.rows_between(@lo, @hi) as [t1]");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("lo", "hi");
    }

    [Fact]
    public void TableFunction_JoinedToTable_ShouldAliasBothSources()
    {
        using var ctx = SqlServerTestContext.Create();
        var complex = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, ctx
            .FromTableFunction(() => Tvf.AllRows())
            .Join(complex, (r, c) => r.Id == c.Id)
            .Select(p => new { p.t1.Value, p.t2.String }));

        sql.Should().Be("select t1.value, t2.somestring as [String] from all_rows() as [t1] join complex_entity as [t2] on t1.id = t2.id");
    }

    [Fact]
    public void TableFunction_AsJoinedSource_ShouldAliasBothSources()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.Create<ISimpleEntity>();

        var sql = SqlOf(ctx, simple
            .Join(ctx.FromTableFunction(() => Tvf.AllRows()), (s, r) => r.Id == s.Id)
            .Select(p => new { p.t1.Id, p.t2.Value }));

        sql.Should().Be("select t1.id, t2.value from simple_entity as [t1] join all_rows() as [t2] on t2.id = cast(t1.id as bigint)");
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
