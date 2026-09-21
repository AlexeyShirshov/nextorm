using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// Verifies the Microsoft SQL Server specific SQL dialect. These tests never open a database
/// connection, so they run on every build/CI. Behavioural tests against a real server live in
/// NextORM.Integration.Tests.
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
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Distinct().Select(x => new { x.Id })).Should().Be("select distinct id from simple_entity");
    }

    [Fact]
    public void QuotedIdentifiers_ShouldUseBrackets()
    {
        using var ctx = SqlServerTestContext.CreateQuoted();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select [id] from [simple_entity]");
    }

    [Fact]
    public void QuotedIdentifiers_CommandOverride_ShouldDisable()
    {
        using var ctx = SqlServerTestContext.CreateQuoted();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id }).WithQuotedIdentifiers(false)).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void IifChoose_ShouldUseSqlServerFunctions()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            I = SqlFunctions.Sql.iif(x.Id > 0L, "yes", "no"),
            C = SqlFunctions.SqlServer.choose(1, "a", "b", "c")
        }));

        sql.Should().Contain("iif(");
        sql.Should().Contain("'yes', 'no')");
        sql.Should().Contain("choose(1, 'a', 'b', 'c')");
    }

    [Fact]
    public void NthValue_ShouldThrowBecauseSqlServerLacksIt()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.Sql.nth_value(x.Id, 2).Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*nth_value*");
    }

    [Fact]
    public void NamedWindow_ShouldThrowBecauseSqlServerHasNoWindowClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var b = e.Window("w", orderBy: [e.Asc(x => x.Id)]);
        var act = () => SqlOf(ctx, b.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over("w")
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Named windows*");
    }

    [Fact]
    public void WindowFrameGroups_ShouldThrowBecauseSqlServerLacksGroups()
    {
        using var ctx = SqlServerTestContext.Create();
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
    public void WindowFrameExclusion_ShouldThrowBecauseSqlServerLacksExclude()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            v = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.RowsUnboundedPrecedingToCurrentRow.WithExclusion(WindowFrameExclusion.CurrentRow))
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*EXCLUDE*");
    }

    [Fact]
    public void SessionInfoFunctions_ShouldUseSqlServerNames()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            CU = SqlFunctions.Sql.current_user(),
            SU = SqlFunctions.Sql.session_user(),
            CS = SqlFunctions.Sql.current_schema(),
            CD = SqlFunctions.Sql.current_database(),
            Ver = SqlFunctions.Sql.version()
        }));

        sql.Should().Contain("current_user");
        sql.Should().Contain("session_user");
        sql.Should().Contain("schema_name()");
        sql.Should().Contain("db_name()");
        sql.Should().Contain("@@version");
    }

    [Fact]
    public void UuidGenerators_ShouldUseSqlServerNames()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { U = SqlFunctions.Sql.gen_random_uuid() }));

        sql.Should().Contain("newid()");
    }

    [Fact]
    public void Uuidv7_ShouldThrowBecauseSqlServerLacksIt()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.uuidv7() }));

        act.Should().Throw<NotSupportedException>().WithMessage("*uuidv7*");
    }

    [Fact]
    public void SelectDistinctWithLimit_ShouldEmitDistinctBeforeTop()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        // SQL Server requires the DISTINCT keyword before TOP; "select top(5) distinct" is invalid.
        var sql = SqlOf(ctx, e.Distinct().Limit(5).Select(x => x.Id));

        sql.Should().StartWith("select distinct top(5) ");
        sql.Should().Contain("from simple_entity");
    }

    [Fact]
    public void SelectDistinctWithUnionAll_ShouldKeepDistinctInLeftBranch()
    {
        using var ctx = SqlServerTestContext.Create();
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
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Union(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n union \nselect id from simple_entity");
    }

    [Fact]
    public void UnionAll_ShouldEmitUnionAll()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).UnionAll(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n union all \nselect id from simple_entity");
    }

    [Fact]
    public void Intersect_ShouldEmitIntersect()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Intersect(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n intersect \nselect id from simple_entity");
    }

    [Fact]
    public void Except_ShouldEmitExcept()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Except(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n except \nselect id from simple_entity");
    }

    [Fact]
    public void IntersectAll_ShouldThrowBecauseSqlServerHasNoIntersectAll()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => x.Id).IntersectAll(e.Select(x => x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("*IntersectAll*");
    }

    [Fact]
    public void ExceptAll_ShouldThrowBecauseSqlServerHasNoExceptAll()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => x.Id).ExceptAll(e.Select(x => x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("*ExceptAll*");
    }

    [Fact]
    public void SelectBasic_ShouldProducePlainSelect()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void Parameter_ShouldUseAtPrefix()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == SqlFunctions.Parameter<int>(0)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = @norm_p0");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void Limit_ShouldUseTop()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

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
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Page(5, 10).Select(x => x.Id));

        // OFFSET/FETCH requires an ORDER BY, so the provider injects an empty sort first.
        sql.Should().Contain("order by (select null as anyorder)");
        sql.Should().EndWith("offset 10 rows\nfetch next 5 rows only");
    }

    [Fact]
    public void Paging_WithOffsetOnly_ShouldNotEmitFetch()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Offset(10).Select(x => x.Id));

        sql.Should().EndWith("offset 10 rows");
        sql.Should().NotContain("fetch next");
    }

    [Fact]
    public void Paging_WithOrderBy_ShouldNotInjectEmptySorting()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Offset(10).OrderBy(x => x.Id).Select(x => x.Id));

        sql.Should().Contain("order by id");
        sql.Should().NotContain("anyorder");
    }

    [Fact]
    public void OrderByWithoutPaging_ShouldNotInjectEmptySorting()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Select(x => x.Id));

        // The empty sort exists only to make OFFSET/FETCH legal; a plain select must stay clean.
        sql.Should().NotContain("order by");
    }

    [Fact]
    public void BooleanLiteral_ShouldUseOne()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.Boolean == true).Select(x => x.Boolean)).Should().Contain("= 1");
    }

    [Fact]
    public void BooleanValueInWhere_ShouldBeComparedWithOne()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // A bare bit column is a value, not a predicate, so T-SQL needs the comparison with 1.
        SqlOf(ctx, e.Where(x => x.Boolean!.Value).Select(x => new { x.Id }))
            .Should().Contain("where (b) = 1");
    }

    [Fact]
    public void BooleanValueAsLogicalOperand_ShouldBeComparedWithOne()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.Boolean!.Value && x.Id > 1L).Select(x => new { x.Id }))
            .Should().Contain("((b) = 1 and (id > 1))");
    }

    [Fact]
    public void StringConcat_ShouldUsePlusOperator()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String + "/" + x.String }));

        sql.Should().Contain("+");
        sql.Should().NotContain("||");
        sql.Should().Contain("as [V]");
    }

    [Fact]
    public void Coalesce_ShouldUseIsNullFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String ?? "" })).Should().Contain("isnull(");
    }

    [Fact]
    public void Count_ShouldUseCountStar()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.count())).Should().Contain("count(*)");
    }

    [Fact]
    public void CountBig_ShouldUseCountBigFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.count_big())).Should().Contain("count_big(*)");
    }

    [Fact]
    public void Aggregate_ShouldKeepProviderSpecificName()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // SQL Server natively has stdev/var; no remapping is needed.
        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.stdev((double)x.Id))).Should().Contain("stdev(");
    }

    [Fact]
    public void ComputedColumn_ShouldBeAliasedWithBrackets()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { x.Id, Calc = x.Id + 1 }));

        sql.Should().Contain("as [Calc]");
        sql.Should().NotContain("as '");
    }

    [Fact]
    public void NestedCalculatedColumn_ShouldReferenceInnerAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var nested = e.Select(x => new { x.Id, Calc = x.String + x.String });
        var sql = SqlOf(ctx, ctx.From(nested).Select(t => new { t.Id, t.Calc }));

        sql.Should().Contain("as [Calc]");
        sql.Should().Contain(") as [t1]");
    }

    [Fact]
    public void Subquery_ShouldAlwaysHaveAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

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
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.Join(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain("as [t1]");
        sql.Should().Contain("as [t2]");
    }

    [Fact]
    public void LeftJoin_ShouldEmitLeftJoinWithOn()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" left join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void RightJoin_ShouldEmitRightJoinWithOn()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.RightJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" right join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void FullJoin_ShouldEmitFullJoinWithOn()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.FullJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" full join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void CrossJoin_ShouldEmitCrossJoinWithoutOn()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.CrossJoin(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" cross join complex_entity");
        sql.Should().NotContain(" on ");
    }

    [Fact]
    public void CrossApply_ShouldEmitCrossApplyWithoutOn()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.CrossApply(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" cross apply complex_entity as [t2]");
        sql.Should().NotContain(" on ");
    }

    [Fact]
    public void OuterApply_ShouldEmitOuterApplyWithoutOn()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.OuterApply(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" outer apply complex_entity as [t2]");
        sql.Should().NotContain(" on ");
    }

    [Fact]
    public void CrossApply_ToSubquery_ShouldEmitDerivedTable()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var subQuery = ctx.From<IComplexEntity>().Where(c => c.Id > 1).Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, simple.CrossApply(subQuery).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain("cross apply (select id, somestring as [String] from complex_entity");
        sql.Should().Contain(") as [t2]");
    }

    [Fact]
    public void CrossApply_ToCorrelatedSubquery_ShouldReferenceOuterAlias()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .CrossApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id).Select(c => new { c.Id, c.String }))
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Be("select t1.id, t3.[String] from simple_entity as [t1] cross apply (select t2.id, t2.somestring as [String] from complex_entity as [t2]\n"
            + " where t2.id = cast(t1.id as bigint)) as [t3]");
    }

    [Fact]
    public void OuterApply_ToCorrelatedSubquery_ShouldReferenceOuterAlias()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .OuterApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id).Select(c => new { c.Id, c.String }))
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Be("select t1.id, t3.[String] from simple_entity as [t1] outer apply (select t2.id, t2.somestring as [String] from complex_entity as [t2]\n"
            + " where t2.id = cast(t1.id as bigint)) as [t3]");
    }

    [Fact]
    public void CrossApply_ToCorrelatedBuilderSource_ShouldProjectTheEntity()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .CrossApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id))
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain("cross apply (select t2.id, t2.nullableint as [Int], t2.somestring as [String]");
        sql.Should().Contain("where t2.id = cast(t1.id as bigint)) as [t3]");
    }

    [Fact]
    public void CrossApply_ToCorrelatedSubqueryWithCapturedValue_ShouldParameterizeTheValue()
    {
        using var ctx = SqlServerTestContext.Create();
        var min = 0L;

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .CrossApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id && c.Id > min).Select(c => new { c.Id }))
            .Select(p => new { OuterId = p.Item1.Id, InnerId = p.Item2.Id }));

        sql.Should().Contain("t2.id = cast(t1.id as bigint)");
        sql.Should().Contain("t2.id > @min");
    }

    [Fact]
    public void CrossApply_OnJoinedProjection_ShouldThrowClearNotSupported()
    {
        using var ctx = SqlServerTestContext.Create();
        var joined = ctx.From<ISimpleEntity>().Join(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

        var act = () => joined
            .CrossApply(p => ctx.From<IComplexEntity>().Where(c => c.Id == p.Item1.Id).Select(c => new { c.Id }))
            .Select(q => new { q.Item1.Item1.Id });

        act.Should().Throw<NotSupportedException>().WithMessage("*join projection*");
    }

    [Fact]
    public void OuterApply_ToCorrelatedBuilderSource_ShouldProjectTheEntity()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .OuterApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id))
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain("outer apply (select t2.id, t2.nullableint as [Int], t2.somestring as [String]");
        sql.Should().Contain("where t2.id = cast(t1.id as bigint)) as [t3]");
    }

    [Fact]
    public void QueryHint_ShouldEmitOptionClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { x.Id }).Hint("recompile"));

        sql.Should().EndWith(" option (recompile)");
    }

    [Fact]
    public void ForJson_ShouldEmitClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id }).ForJson())
            .Should().EndWith("for json path");

        SqlOf(ctx, e.Select(x => new { x.Id }).ForJson(ForJsonMode.Auto, "root", includeNullValues: true))
            .Should().EndWith("for json auto, root('root'), include_null_values");
    }

    [Fact]
    public void ForJson_WithHint_ShouldPlaceOptionAfterJson()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id }).ForJson().Hint("recompile"))
            .Should().EndWith("for json path option (recompile)");
    }

    [Fact]
    public void ForXml_ShouldEmitClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id }).ForXml())
            .Should().EndWith("for xml path");

        SqlOf(ctx, e.Select(x => new { x.Id }).ForXml(ForXmlMode.Raw, "row", "root", elements: true))
            .Should().EndWith("for xml raw('row'), root('root'), elements");
    }

    [Fact]
    public void ForXmlAndForJson_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { x.Id }).ForJson().ForXml());

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot be combined*");
    }

    [Fact]
    public void QueryHint_WithRecursiveCte_ShouldMergeIntoOneOptionClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var anchor = e.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.UnionAll(step);

        var sql = SqlOf(ctx, ctx.WithRecursive("nums", body, 100).From("nums").Select(t => new CteNumberRow { n = t["n"].AsInt }).Hint("recompile"));

        sql.Should().EndWith("option (maxrecursion 100, recompile)");
        sql.Should().NotContain("option (maxrecursion 100) option");
    }

    [Fact]
    public void QueryHint_ShouldNotReuseThePlanOfAnUnhintedCommand()
    {
        using var ctx = SqlServerTestContext.Create();

        var plain = ctx.From<ISimpleEntity>().Select(x => x.Id);
        var hinted = plain.Hint("recompile");

        var plainSql = ((DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(plain, false, true, CancellationToken.None)).DbCommand.CommandText;
        var hintedSql = ((DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(hinted, false, true, CancellationToken.None)).DbCommand.CommandText;

        plainSql.Should().NotContain("option (recompile)");
        hintedSql.Should().Contain("option (recompile)");
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
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.Id > 1 ? "big" : "small" }));

        sql.Should().Contain("case when (id > 1) then 'big' else 'small' end");
        sql.Should().Contain("as [V]");
    }

    [Fact]
    public void NestedConditional_ShouldEmitNestedCaseWhen()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.Boolean == true ? (x.Int == null ? "a" : "b") : "c" }));

        sql.Should().Contain("case when b = 1 then case when nullableint is null then 'a' else 'b' end else 'c' end");
    }

    [Fact]
    public void Conditional_InWhere_ShouldEmitCaseWhen()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => (x.Int == null ? 0 : x.Int) == 1).Select(x => new { x.Id }));

        sql.Should().Contain("case when nullableint is null then 0 else nullableint end");
    }

    [Fact]
    public void ConditionalBoolean_ShouldCastToBit()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // SQL Server has no boolean type, so a boolean-valued CASE is materialised as a bit scalar.
        var sql = SqlOf(ctx, e.Select(x => new { V = x.Id > 1 ? true : false }));

        sql.Should().Contain("cast(case when (id > 1) then 1 else 0 end as bit)");
    }

    [Fact]
    public void ConditionalBoolean_InWhere_ShouldCompareWithOne()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // A bit scalar is not a predicate, so a condition context adds "= 1".
        var sql = SqlOf(ctx, e.Where(x => x.Id > 1 ? true : false).Select(x => x.Id));

        sql.Should().Contain("cast(case when (id > 1) then 1 else 0 end as bit) = 1");
    }

    [Fact]
    public void Switch_ShouldEmitSearchedCase()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(SwitchOfId("other", (1L, "one"), (2L, "two"))));

        sql.Should().Contain("case when id = 1 then 'one' when id = 2 then 'two' else 'other' end");
    }

    [Fact]
    public void StringToUpper_ShouldUseUpperFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.ToUpper() })).Should().Contain("upper(somestring)");
    }

    [Fact]
    public void SqlFunction_OverColumn_ShouldEmitMappedFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Udf.ToUpper(x.String!) }))
            .Should().Be("select upper(somestring) as [V] from complex_entity");
    }

    [Fact]
    public void SqlFunction_WithCapturedArgument_ShouldEmitParameter()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
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
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Udf.WithSchema(x.Id) }))
            .Should().Be("select dbo.my_fn(id) as [V] from complex_entity");
    }

    [Fact]
    public void SqlFunction_FormatDate_ShouldEmitFormatFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { M = Udf.FormatDate(x.Datetime, "yyyy-MM") }))
            .Should().Be("select format(dt, 'yyyy-MM') as [M] from complex_entity");
    }

    private static class Udf
    {
        [SqlFunction("upper")]
        public static string ToUpper(string value) => throw new NotSupportedException();

        [SqlFunction("substr")]
        public static string Slice(string value, int start) => throw new NotSupportedException();

        [SqlFunction("my_fn", Schema = "dbo")]
        public static long WithSchema(long value) => throw new NotSupportedException();

        [SqlFunction("format")]
        public static string FormatDate(DateTime? value, string format) => throw new NotSupportedException();
    }

    [Fact]
    public void Contains_ShouldUseLikeWithWildcards()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Contains("df")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%df%'");
    }

    [Fact]
    public void Contains_WhenProjected_ShouldMaterialiseAsBit()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // T-SQL has no boolean scalar, so a projected LIKE has to become a bit through a CASE.
        SqlOf(ctx, e.Select(x => new { V = x.String!.Contains("df") }))
            .Should().Contain("cast(case when somestring like '%df%' then 1 else 0 end as bit)");
    }

    [Fact]
    public void FullTextPredicates_ShouldEmitFunctions()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.Sql.contains(x.String, "foo")).Select(x => new { x.Id }))
            .Should().Contain("where contains(somestring, 'foo')");

        SqlOf(ctx, e.Where(x => SqlFunctions.Sql.freetext(x.String, "foo")).Select(x => new { x.Id }))
            .Should().Contain("where freetext(somestring, 'foo')");
    }

    [Fact]
    public void IsJson_ShouldEmitPredicateAndBitValue()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // ISJSON returns int, so a WHERE context compares it with 1 ...
        SqlOf(ctx, e.Where(x => SqlFunctions.SqlServer.isjson(x.String)).Select(x => new { x.Id }))
            .Should().Contain("where (isjson(somestring)) = 1");

        // ... and a projection materialises it as a bit.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.isjson(x.String) }))
            .Should().Contain("cast(isjson(somestring) as bit) as [V]");
    }

    [Fact]
    public void FullTextPredicate_WhenProjected_ShouldMaterialiseAsBit()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // T-SQL has no boolean scalar, so a projected CONTAINS has to become a bit through a CASE.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.contains(x.String, "foo") }))
            .Should().Contain("cast(case when contains(somestring, 'foo') then 1 else 0 end as bit)");
    }

    [Fact]
    public void Substring_ShouldUseOneBasedOffsetWithLengthFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Substring(1) }))
            .Should().Contain("substring(somestring, 1 + 1, len(somestring) - (1))");
    }

    [Fact]
    public void StringLength_ShouldUseLenFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Length })).Should().Contain("len(somestring)");
    }

    [Fact]
    public void Trim_ShouldUseTrimFunctions()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Trim() })).Should().Contain("trim(somestring)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.TrimStart() })).Should().Contain("ltrim(somestring)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.TrimEnd() })).Should().Contain("rtrim(somestring)");
    }

    [Fact]
    public void Replace_ShouldUseReplaceFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Replace("a", "b") }))
            .Should().Contain("replace(somestring, 'a', 'b')");
    }

    [Fact]
    public void StringIsNullOrEmpty_ShouldEmitNullCheck()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.IsNullOrEmpty(x.String)).Select(x => new { x.Id }))
            .Should().Contain("(somestring is null or somestring = '')");
    }

    [Fact]
    public void Like_WithEscapeChar_ShouldEmitEscapeClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.Sql.like(x.String, "%a!%", "!")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%a!%' escape '!'");
    }

    [Fact]
    public void MathAbs_ShouldUseAbsFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Math.Abs(x.Id - 5) })).Should().Contain("abs((id - 5))");
    }

    [Fact]
    public void MathRound_ShouldSupplyDefaultLength()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // T-SQL's round() requires the length argument.
        SqlOf(ctx, e.Select(x => new { V = Math.Round(x.Id / 2.0 + 0.2) }))
            .Should().Contain("round(((cast(id as float) / 2) + 0.2), 0)");
    }

    [Fact]
    public void MathTruncate_ShouldUseThreeArgumentRound()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // T-SQL has no trunc; round(number, 0, 1) truncates.
        SqlOf(ctx, e.Select(x => new { V = Math.Truncate(x.Id + 0.0) }))
            .Should().Contain("round((cast(id as float) + 0), 0, 1)");
    }

    [Fact]
    public void MathLog_ShouldUseNaturalLogarithm()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Math.Log(x.Id + 1.0) })).Should().Contain("log(");
    }

    [Fact]
    public void DateTimeNow_ShouldUseGetDateFunctions()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { N = DateTime.Now })).Should().Contain("getdate()");
        SqlOf(ctx, e.Select(x => new { N = DateTime.UtcNow })).Should().Contain("getutcdate()");
    }

    [Fact]
    public void DateTimePart_ShouldUseDatepartFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { Y = x.Datetime!.Value.Year })).Should().Contain("datepart(year, dt)");
        SqlOf(ctx, e.Select(x => new { D = x.Datetime!.Value.Day })).Should().Contain("datepart(day, dt)");
        SqlOf(ctx, e.Select(x => new { DOY = x.Datetime!.Value.DayOfYear })).Should().Contain("datepart(dayofyear, dt)");
    }

    [Fact]
    public void Extract_ShouldUseSqlServerDatePartForms()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { Q = SqlFunctions.Sql.extract("quarter", x.Datetime) }))
            .Should().Contain("datepart(quarter, dt)");
        SqlOf(ctx, e.Select(x => new { W = SqlFunctions.Sql.extract("week", x.Datetime) }))
            .Should().Contain("datepart(isowk, dt)");
        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.extract("dow", x.Datetime) }))
            .Should().Contain("(datepart(weekday, dt) + @@datefirst - 1) % 7");
        SqlOf(ctx, e.Select(x => new { I = SqlFunctions.Sql.extract("isodow", x.Datetime) }))
            .Should().Contain("(datepart(weekday, dt) + @@datefirst - 2) % 7) + 1");
        SqlOf(ctx, e.Select(x => new { E = SqlFunctions.Sql.date_part("epoch", x.Datetime) }))
            .Should().Contain("cast(datediff_big(millisecond, '19700101', dt) as float) / 1000.0");
    }

    [Fact]
    public void SetSeed_ShouldThrowBecauseSqlServerHasNoStandaloneSeed()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { S = SqlFunctions.Postgres.setseed(0.5) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*setseed*");
    }

    [Fact]
    public void CryptoHash_ShouldThrowBecauseOnlyPostgresHasIt()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { H = SqlFunctions.Postgres.digest("abc", "sha256") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*digest/sha256*");
    }

    [Fact]
    public void PostgresTableFunctions_ShouldThrowBecauseOnlyPostgresHasThem()
    {
        using var ctx = SqlServerTestContext.Create();
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
    public void InValues_ShouldRenderInPredicateWithParameters()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new long[] { 1, 2, 3 };

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0, @p1, @p2)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2");
    }

    [Fact]
    public void InValues_InlineParams_ShouldBecomeParameters()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, 1L, 2L)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0, @p1)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void InValues_Empty_ShouldRenderAlwaysFalse()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = Array.Empty<long>();

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("1 = 0");
        command.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
    }

    [Fact]
    public void InValues_SingleElement_ShouldRenderInPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new long[] { 2 };

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in (@p0)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0");
    }

    [Fact]
    public void InValues_WithNull_ShouldAddNullBranch()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new int?[] { 1, null };

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Int, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("(nullableint in (@p0) or nullableint is null)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0");
    }

    [Fact]
    public void InValues_AllNull_ShouldRenderIsNull()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new int?[] { null };

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Int, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("nullableint is null");
        Normalize(command.DbCommand.CommandText).Should().NotContain(" in (");
        command.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
    }

    [Fact]
    public void Contains_CapturedList_ShouldRenderInPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
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
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
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
        var e = ctx.From<IComplexEntity>();

        // A bit column is a value, not a predicate, so it has to be compared with 1 before NOT.
        SqlOf(ctx, e.Where(x => !x.Boolean!.Value).Select(x => new { x.Id }))
            .Should().Contain("where not ((b) = 1)");
    }

    [Fact]
    public void LogicalNot_WhenProjected_ShouldMaterialiseAsBit()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // T-SQL has no boolean scalar, so a projected NOT has to become a bit through a CASE, and
        // the bit column has to be compared with 1 before it can be used as the CASE test.
        SqlOf(ctx, e.Select(x => !x.Boolean!.Value))
            .Should().Contain("cast(case when not ((b) = 1) then 1 else 0 end as bit)");
    }

    [Fact]
    public void Negate_ShouldParenthesiseOperand()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => -x.Id)).Should().Contain("-(id)");
    }

    [Fact]
    public void OnesComplement_ShouldEmitTilde()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => ~x.Id)).Should().Contain("~(id)");
    }

    [Fact]
    public void LogicalNot_OfAnd_ShouldNegateWholePredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => !(x.Boolean!.Value && x.Id > 1L)).Select(x => new { x.Id }))
            .Should().Contain("not (((b) = 1 and (id > 1)))");
    }

    [Fact]
    public void LogicalNot_OfOr_ShouldNegateWholePredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

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
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

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
        var e = ctx.From<ISimpleEntity>();

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
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over(partitionBy: () => x.Int, orderBy: () => x.Id)
        }));

        sql.Should().Be("select id, row_number() over (partition by nullableint order by id) as [rn] from complex_entity");
    }

    [Fact]
    public void RankAndDenseRank_ShouldEmitOverWithOrder()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            r = SqlFunctions.Sql.rank().Over(SqlFunctions.Sql.asc(() => x.Id)),
            dr = SqlFunctions.Sql.dense_rank().Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Be("select id, rank() over (order by id) as [r], dense_rank() over (order by id) as [dr] from complex_entity");
    }

    [Fact]
    public void PercentRankCumeDist_ShouldEmitOverWithOrder()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            pr = SqlFunctions.Sql.percent_rank().Over(SqlFunctions.Sql.asc(() => x.Id)),
            cd = SqlFunctions.Sql.cume_dist().Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Be("select id, percent_rank() over (order by id) as [pr], cume_dist() over (order by id) as [cd] from complex_entity");
    }

    [Fact]
    public void WindowOrderByDescending_ShouldEmitDesc()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            r = SqlFunctions.Sql.row_number().Over(
                partitionBy: new Expression<Func<object?>>[] { () => x.Int },
                orderBy: new[] { SqlFunctions.Sql.desc(() => x.Id) })
        }));

        sql.Should().Be("select id, row_number() over (partition by nullableint order by id desc) as [r] from complex_entity");
    }

    [Fact]
    public void LagAndLead_ShouldEmitOffsetAndDefault()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            prev = SqlFunctions.Sql.lag(x.Id, 1, 0L).Over(SqlFunctions.Sql.asc(() => x.Id)),
            next = SqlFunctions.Sql.lead(x.Int, 2, 0).Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Be("select id, lag(id, 1, 0) over (order by id) as [prev], lead(nullableint, 2, 0) over (order by id) as [next] from complex_entity");
    }

    [Fact]
    public void WindowAggregate_ShouldEmitOverPartition()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            total = SqlFunctions.Sql.sum_over(x.Id).Over(partitionBy: () => x.Int),
            n = SqlFunctions.Sql.count_over().Over(partitionBy: () => x.Int)
        }));

        sql.Should().Be("select id, sum(id) over (partition by nullableint) as [total], count(*) over (partition by nullableint) as [n] from complex_entity");
    }

    [Fact]
    public void WindowFrame_ShouldEmitRowsBetween()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            running = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.RowsUnboundedPrecedingToCurrentRow),
            sliding = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.Rows(1, 1))
        }));

        sql.Should().Be("select id, sum(id) over (order by id rows between unbounded preceding and current row) as [running], sum(id) over (order by id rows between 1 preceding and 1 following) as [sliding] from complex_entity");
    }

    [Fact]
    public void NtileAndFirstLastValue_ShouldEmitOver()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            bucket = SqlFunctions.Sql.ntile(2).Over(SqlFunctions.Sql.asc(() => x.Id)),
            first = SqlFunctions.Sql.first_value(x.Int).Over(partitionBy: () => x.Int, orderBy: () => x.Id),
            last = SqlFunctions.Sql.last_value(x.Int).Over(partitionBy: () => x.Int, orderBy: () => x.Id)
        }));

        sql.Should().Be("select id, ntile(2) over (order by id) as [bucket], first_value(nullableint) over (partition by nullableint order by id) as [first], last_value(nullableint) over (partition by nullableint order by id) as [last] from complex_entity");
    }

    [Fact]
    public void WindowFrame_FullBoundaries_ShouldEmitRowsAndRange()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            full = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.desc(() => x.Id),
                WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.UnboundedFollowing)),
            range = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.RangeUnboundedPrecedingToCurrentRow)
        }));

        sql.Should().Contain("sum(id) over (order by id desc rows between unbounded preceding and unbounded following)");
        sql.Should().Contain("sum(id) over (order by id range between unbounded preceding and current row)");
    }

    [Fact]
    public void WindowFunction_WithoutOver_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { x.Id, rn = SqlFunctions.Sql.row_number() }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Over*");
    }

    public interface ITvfRow
    {
        [Column("id")]
        long Id { get; set; }
        [Column("value")]
        string? Value { get; set; }
    }

    public interface IOpenJsonTypedRow
    {
        [Column("name")]
        string? Name { get; set; }
        [Column("age")]
        int Age { get; set; }
    }

    private static class Tvf
    {
        [SqlTableFunction("all_rows")]
        public static IQueryable<ITvfRow> AllRows() => throw new NotSupportedException();

        [SqlTableFunction("rows_by_id")]
        public static IQueryable<ITvfRow> ById(long id) => throw new NotSupportedException();

        [SqlTableFunction("rows_between", Schema = "app")]
        public static IQueryable<ITvfRow> Between(long lo, long hi) => throw new NotSupportedException();

        [SqlTableFunction("openjson", WithClause = "name nvarchar(50) '$.name', age int '$.age'")]
        public static IQueryable<IOpenJsonTypedRow> OpenJsonTyped(string json) => throw new NotSupportedException();
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
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, ctx
            .FromTableFunction(() => Tvf.AllRows())
            .Join(complex, (r, c) => r.Id == c.Id)
            .Select(p => new { p.Item1.Value, p.Item2.String }));

        sql.Should().Be("select t1.value, t2.somestring as [String] from all_rows() as [t1] join complex_entity as [t2] on t1.id = t2.id");
    }

    [Fact]
    public void TableFunction_AsJoinedSource_ShouldAliasBothSources()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, simple
            .Join(ctx.FromTableFunction(() => Tvf.AllRows()), (s, r) => r.Id == s.Id)
            .Select(p => new { p.Item1.Id, p.Item2.Value }));

        sql.Should().Be("select t1.id, t2.value from simple_entity as [t1] join all_rows() as [t2] on t2.id = cast(t1.id as bigint)");
    }

    [Fact]
    public void StringSplit_TableFunction_ShouldEmitFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var csv = "a,b,c";
        var separator = ",";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.SqlServer.string_split(csv, separator))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText).Should().Be("select value from string_split(@csv, @separator) as [t1]");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("csv", "separator");
    }

    [Fact]
    public void OpenJson_TableFunction_ShouldEmitFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var json = "{\"a\":1}";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.SqlServer.openjson(json))
            .Select(r => new { r.Key, r.Value, r.Type }));

        Normalize(command.DbCommand.CommandText).Should()
            .Be("select [key] as [Key], value, type from openjson(@json) as [t1]");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("json");
    }

    [Fact]
    public void OpenJsonWith_ShouldAppendWithClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var json = "{\"name\":\"a\",\"age\":1}";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => Tvf.OpenJsonTyped(json))
            .Select(r => new { r.Name, r.Age }));

        Normalize(command.DbCommand.CommandText).Should()
            .Contain("from openjson(@json) with (name nvarchar(50) '$.name', age int '$.age') as [t1]");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("json");
    }

    [Fact]
    public void TableHint_ShouldEmitWithClause()
    {
        using var ctx = SqlServerTestContext.Create();

        SqlOf(ctx, ctx.From<IComplexEntity>().WithTableHint("nolock").Select(x => new { x.Id }))
            .Should().Be("select id from complex_entity with (nolock)");

        SqlOf(ctx, ctx.From<IComplexEntity>().WithTableHint("nolock", "index(ix_id)").Select(x => new { x.Id }))
            .Should().Be("select id from complex_entity with (nolock, index(ix_id))");
    }

    [Fact]
    public void TableHint_WithJoin_ShouldPlaceHintBeforeAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.From<ISimpleEntity>().WithTableHint("nolock");
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.Join(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id }));

        sql.Should().Contain("from simple_entity with (nolock) as [t1] join complex_entity as [t2]");
    }

    [Fact]
    public void NullIf_ShouldEmitNullIf()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // nullif is ANSI and therefore not capability-gated.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.nullif(x.Id, 0L) }))
            .Should().Contain("nullif(id, 0)");
    }

    [Fact]
    public void GreatestLeast_ShouldEmitFunctions()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            G = SqlFunctions.Sql.greatest(x.Id, x.Id),
            L = SqlFunctions.Sql.least(x.Id, x.Id)
        }));

        sql.Should().Contain("greatest(id, id) as [G]");
        sql.Should().Contain("least(id, id) as [L]");
    }

    [Fact]
    public void StringAgg_ShouldEmitAggregate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.string_agg(x.String, ",")))
            .Should().Contain("string_agg(somestring, ',')");
    }

    [Fact]
    public void TextJsonFunctions_ShouldEmitFunctions()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Id = SqlFunctions.SqlServer.json_value(x.String, "$.id"),
            Name = SqlFunctions.SqlServer.json_query(x.String, "$.name"),
            Updated = SqlFunctions.SqlServer.json_modify(x.String, "$.id", "1")
        }));

        sql.Should().Contain("json_value(somestring, '$.id') as [Id]");
        sql.Should().Contain("json_query(somestring, '$.name') as [Name]");
        sql.Should().Contain("json_modify(somestring, '$.id', '1') as [Updated]");
    }

    [Fact]
    public void ArrayAgg_ShouldThrowBecauseSqlServerHasNoArrayType()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e
            .GroupBy(x => new { x.Int })
            .Having(x => SqlFunctions.Postgres.array_agg(x.Id) != null)
            .Select(x => new { x.Int }));

        act.Should().Throw<NotSupportedException>().WithMessage("*string_agg/array_agg*");
    }

    [Fact]
    public void AnyValueAggregate_ShouldThrowBecauseSqlServerVersionLacksIt()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.any_agg(x.String) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*ANY_VALUE*");
    }

    [Fact]
    public void PercentileWindow_ShouldEmitWithinGroupOver()
    {
        using var ctx = SqlServerTestContext.Create();
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
    public void DateTrunc_ShouldEmitDatetruncFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { M = SqlFunctions.Sql.date_trunc("month", x.Datetime) }))
            .Should().Contain("datetrunc(month, dt) as [M]");

        // ANSI plural parts are folded onto the singular T-SQL spellings.
        SqlOf(ctx, e.Select(x => new { M = SqlFunctions.Sql.date_trunc("milliseconds", x.Datetime) }))
            .Should().Contain("datetrunc(millisecond, dt)");
    }

    [Fact]
    public void DateTruncWithUnsupportedField_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.date_trunc("century", x.Datetime) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*century*");
    }

    [Fact]
    public void GroupByRollup_ShouldEmitRollup()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .GroupByRollup(x => new { x.Int, x.Boolean })
            .Select(x => new { x.Int, x.Boolean }))
            .Should().Contain("group by rollup (nullableint, b)");
    }

    [Fact]
    public void GroupByCube_ShouldEmitCube()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .GroupByCube(x => new { x.Int, x.Boolean })
            .Select(x => new { x.Int, x.Boolean }))
            .Should().Contain("group by cube (nullableint, b)");
    }

    [Fact]
    public void GroupByGroupingSets_ShouldEmitGroupingSets()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .GroupByGroupingSets(x => new { x.Int, x.Boolean },
                new[] { 0, 1 },
                new[] { 0 },
                Array.Empty<int>())
            .Select(x => new { x.Int, x.Boolean }))
            .Should().Contain("group by grouping sets ((nullableint, b), (nullableint), ())");
    }

    [Fact]
    public void DateAdd_ShouldEmitDateaddFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("day", 1, x.Datetime) }))
            .Should().Contain("dateadd(day, 1, dt) as [D]");

        // ANSI plural parts are folded onto the singular T-SQL spellings.
        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("milliseconds", 5, x.Datetime) }))
            .Should().Contain("dateadd(millisecond, 5, dt)");
    }

    [Fact]
    public void DateAdd_WithDecadePart_ShouldScaleToYears()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // T-SQL dateadd has no decade part, so the amount is scaled on a year add.
        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("decade", 2, x.Datetime) }))
            .Should().Contain("dateadd(year, (2) * 10, dt)");
    }

    [Fact]
    public void EndOfMonth_ShouldEmitEomonth()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { E = SqlFunctions.Sql.end_of_month(x.Datetime) }))
            .Should().Contain("eomonth(dt) as [E]");
    }

    [Fact]
    public void DateDiff_ShouldEmitDatediffFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_diff("day", x.Datetime, x.Datetime) }))
            .Should().Contain("datediff(day, dt, dt) as [D]");

        // ANSI plural parts are folded onto the singular T-SQL spellings.
        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_diff("milliseconds", x.Datetime, x.Datetime) }))
            .Should().Contain("datediff(millisecond, dt, dt)");
    }

    [Fact]
    public void DateFromParts_ShouldEmitDatefromparts()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_from_parts(2023, 1, 31) }))
            .Should().Contain("datefromparts(2023, 1, 31) as [D]");
    }

    [Fact]
    public void DateTimeAddMethods_ShouldEmitDateaddFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = x.Datetime!.Value.AddDays(7) }))
            .Should().Contain("dateadd(day, 7, dt) as [D]");

        SqlOf(ctx, e.Select(x => new { M = x.Datetime!.Value.AddMonths(2) }))
            .Should().Contain("dateadd(month, 2, dt) as [M]");

        // AddMilliseconds maps to the singular T-SQL millisecond part.
        SqlOf(ctx, e.Select(x => new { S = x.Datetime!.Value.AddMilliseconds(500) }))
            .Should().Contain("dateadd(millisecond, 500, dt) as [S]");
    }

    [Fact]
    public void FilteredAggregate_ShouldThrowBecauseSqlServerHasNoFilterClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.Sql.count(() => x.Id > 1L)));

        act.Should().Throw<NotSupportedException>().WithMessage("*FILTER*");
    }

    [Fact]
    public void IndexOfLastIndexOf_ShouldUseCharIndex()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.IndexOf("b") }))
            .Should().Contain("case when (charindex('b', somestring)) = 0 then -1 else (charindex('b', somestring)) - 1 end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.IndexOf("b", 1) }))
            .Should().Contain("case when (charindex('b', somestring, 1 + 1)) = 0 then -1 else (charindex('b', somestring, 1 + 1)) - 1 end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.LastIndexOf("b") }))
            .Should().Contain("case when (charindex(reverse('b'), reverse(somestring))) = 0 then -1 else len(somestring) - (charindex(reverse('b'), reverse(somestring))) - len('b') + 1 end");
    }

    [Fact]
    public void PadLeftRight_ShouldUseReplicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.PadLeft(5, '0') }))
            .Should().Contain("case when len(somestring) >= (5) then somestring else replicate('0', (5) - len(somestring))+somestring end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.PadRight(5) }))
            .Should().Contain("case when len(somestring) >= (5) then somestring else somestring+replicate(' ', (5) - len(somestring)) end");
    }

    [Fact]
    public void RemoveInsertAndNewString_ShouldUseStuffAndReplicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Remove(2) }))
            .Should().Contain("substring(somestring, 1, 2)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.Remove(2, 1) }))
            .Should().Contain("stuff(somestring, 2 + 1, 1, '')");
        SqlOf(ctx, e.Select(x => new { V = x.String!.Insert(2, "x") }))
            .Should().Contain("stuff(somestring, 2 + 1, 0, 'x')");
        SqlOf(ctx, e.Select(x => new { V = new string('*', 4) }))
            .Should().Contain("replicate('*', 4)");
    }

    [Fact]
    public void CorrelatedScalarInSelect_ShouldReferenceOuterAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            sid = inner.Where(s => s.Id == it.Id).Select(s => s.Id).First()
        }));

        sql.Should().Contain("(select top(1)");
        sql.Should().Contain("= t1.id");
        sql.Should().Contain("from complex_entity as [t1]");
    }

    /// <summary>
    /// The outer reference may target a join-projection item (<c>p.Item1.Id</c>); the projection item
    /// then supplies the alias for the inner predicate instead of a plain entity parameter.
    /// </summary>
    [Fact]
    public void CorrelatedScalarOnJoinProjection_ShouldReferenceOuterAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var outer = ctx.From<IComplexEntity>().Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id);
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(p => new
        {
            p.Item1.Id,
            sid = inner.Where(s => s.Id == p.Item1.Id).Select(s => s.Id).First()
        }));

        sql.Should().Contain("(select top(1)");
        sql.Should().Contain("= t1.id");
        sql.Should().Contain("from complex_entity as [t1]");
        sql.Should().Contain("join simple_entity as [t2]");
    }

    /// <summary>
    /// An aggregate terminal inside a correlated subquery is rewritten to the equivalent aggregate
    /// projection instead of being rejected.
    /// </summary>
    [Fact]
    public void CorrelatedAggregateTerminalInSelect_ShouldRenderAggregateSubquery()
    {
        using var ctx = SqlServerTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            c = inner.Where(s => s.Id == it.Id).Count()
        }));

        sql.Should().Contain("count(*) from simple_entity");
        sql.Should().Contain("= t1.id");
    }

    [Fact]
    public void TableSample_ShouldEmitPercent()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.TableSample(10).Select(x => x.Id))
            .Should().EndWith("tablesample (10 percent)");
    }

    [Fact]
    public void TableSample_WithSeed_ShouldEmitRepeatable()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.TableSample(10, TableSampleMethod.System, 3).Select(x => x.Id))
            .Should().EndWith("tablesample (10 percent) repeatable (3)");
    }

    [Fact]
    public void TableSample_Bernoulli_ShouldThrowBecauseSqlServerHasNoBernoulli()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.TableSample(10, TableSampleMethod.Bernoulli).Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*BERNOULLI*");
    }

    [Fact]
    public void WithTies_ShouldUseTopWithTies()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Limit(5).WithTies().OrderBy(x => x.Id).Select(x => x.Id))
            .Should().Contain("top(5) with ties");
    }

    [Fact]
    public void WithTies_WithOffset_ShouldUseFetchWithTies()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Page(5, 3).WithTies().OrderBy(x => x.Id).Select(x => x.Id))
            .Should().Contain("fetch next 5 rows with ties");
    }

    [Fact]
    public void WithTies_WithoutLimit_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.WithTies().Select(x => x.Id));

        act.Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void DistinctOn_ShouldThrowBecauseSqlServerHasNoDistinctOn()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.DistinctOn(x => x.Id).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*DISTINCT ON*");
    }

    [Fact]
    public void ForUpdate_ShouldEmitUpdlockTableHint()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForUpdate().Select(x => x.Id))
            .Should().Be("select id from simple_entity with (updlock)");
    }

    [Fact]
    public void ForShare_ShouldEmitHoldlockTableHint()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForShare().Select(x => x.Id))
            .Should().Be("select id from simple_entity with (holdlock)");
    }

    [Fact]
    public void ForUpdate_WithTableHint_ShouldCombineHints()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithTableHint("rowlock").ForUpdate().Select(x => x.Id))
            .Should().Be("select id from simple_entity with (rowlock, updlock)");
    }

    [Fact]
    public void ForSystemTime_AsOf_ShouldRenderAsOf()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForSystemTime(TemporalClause.AsOf(new DateTime(2020, 1, 1, 12, 0, 0))).Select(x => x.Id))
            .Should().EndWith("for system_time as of '2020-01-01 12:00:00'");
    }

    [Fact]
    public void ForSystemTime_Between_ShouldRenderBetweenAnd()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForSystemTime(TemporalClause.Between(new DateTime(2020, 1, 1), new DateTime(2020, 2, 1))).Select(x => x.Id))
            .Should().EndWith("for system_time between '2020-01-01 00:00:00' and '2020-02-01 00:00:00'");
    }

    [Fact]
    public void ForSystemTime_FromTo_ShouldRenderFromTo()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForSystemTime(TemporalClause.FromTo(new DateTime(2020, 1, 1), new DateTime(2020, 2, 1))).Select(x => x.Id))
            .Should().EndWith("for system_time from '2020-01-01 00:00:00' to '2020-02-01 00:00:00'");
    }

    [Fact]
    public void ForSystemTime_ContainedIn_ShouldRenderContainedIn()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForSystemTime(TemporalClause.ContainedIn(new DateTime(2020, 1, 1), new DateTime(2020, 2, 1))).Select(x => x.Id))
            .Should().EndWith("for system_time contained in ('2020-01-01 00:00:00', '2020-02-01 00:00:00')");
    }

    [Fact]
    public void ForSystemTime_All_ShouldRenderAll()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForSystemTime(TemporalClause.All()).Select(x => x.Id))
            .Should().EndWith("for system_time all");
    }

    [Fact]
    public void ForSystemTime_InvalidRange_ShouldThrow()
    {
        var act = () => TemporalClause.Between(new DateTime(2020, 2, 1), new DateTime(2020, 1, 1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ForSystemTime_WithJoin_ShouldRenderBeforeAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();
        var c = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.ForSystemTime(TemporalClause.All())
            .Join(c, (a, b) => a.Id == b.Id)
            .Select(p => new { p.Item1.Id }));

        sql.Should().Contain("from simple_entity for system_time all as ");
    }

    [Fact]
    public void XmlValue_ShouldEmitPostfixMethod()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.SqlServer.xml_value<int>(x.String, "(/root/item)[1]", "int")
        }));

        sql.Should().Contain("somestring.value('(/root/item)[1]', 'int') as [V]");
    }

    [Fact]
    public void XmlQuery_ShouldEmitPostfixMethod()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.xml_query(x.String, "/root/item") }))
            .Should().Contain("somestring.query('/root/item') as [V]");
    }

    [Fact]
    public void XmlExist_ShouldMaterialisePredicateAndBit()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // exist() returns bit (an int-like value in T-SQL), so a WHERE context compares it with 1 ...
        SqlOf(ctx, e.Where(x => SqlFunctions.SqlServer.xml_exist(x.String, "/root"))
            .Select(x => new { x.Id }))
            .Should().Contain("where (somestring.exist('/root')) = 1");

        // ... and a projection keeps the bit value.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.xml_exist(x.String, "/root") }))
            .Should().Contain("somestring.exist('/root') as [V]");
    }

    [Fact]
    public void XmlMethods_ShouldThrowWhenArgumentsAreNotConstants()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var xpath = "/root";

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.xml_query(x.String, xpath) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*constant*");
    }

    private static string SqlOfCached<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void Pivot_ShouldEmitPivotClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();

        SqlOf(ctx, e
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                PivotValue.Create("1"), PivotValue.Create("2"))
            .Select(t => new
            {
                Category = t.GetString("category"),
                Q1 = t.GetNullableDecimal("[1]"),
                Q2 = t.GetNullableDecimal("[2]")
            }))
            .Should().Be("select category, [1], [2] from sales pivot (sum(margin) for quarter in ([1], [2])) as [t1]");
    }

    [Fact]
    public void Pivot_ShouldRenderEveryAggregateAndKeepValueWithoutAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();

        foreach (var aggregate in new[] { PivotAggregate.Count, PivotAggregate.Avg, PivotAggregate.Min, PivotAggregate.Max })
        {
            var name = aggregate.ToString().ToLowerInvariant();
            SqlOf(ctx, e.Pivot(aggregate, s => s.Margin, s => s.Category, PivotValue.Create("North"))
                .Select(t => new { V = t.GetNullableDecimal("North") }))
                .Should().Contain($"pivot ({name}(margin) for category in ([North]))");
        }
    }

    [Fact]
    public void Pivot_ShouldReuseCachedPlan()
    {
        using var ctx = SqlServerTestContext.Create();

        var e1 = ctx.From<ISalesEntity>();
        var first = SqlOfCached(ctx, e1
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"))
            .Select(t => new { Category = t.GetString("category"), Q1 = t.GetNullableDecimal("[1]") }));

        var e2 = ctx.From<ISalesEntity>();
        var second = SqlOfCached(ctx, e2
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"))
            .Select(t => new { Category = t.GetString("category"), Q1 = t.GetNullableDecimal("[1]") }));

        second.Should().Be(first);
        second.Should().Contain("pivot (sum(margin) for quarter in ([1]))");
    }

    [Fact]
    public void Pivot_ShouldReuseCachedPlan_DerivedSource()
    {
        using var ctx = SqlServerTestContext.Create();

        var derived1 = ctx.From<ISalesEntity>()
            .Where(s => s.Margin > 0)
            .Select(s => new { s.Category, s.Quarter, s.Margin });
        var first = SqlOfCached(ctx, ctx.From(derived1)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.Quarter, PivotValue.Create("1"))
            .Select(t => new { Category = t.GetString("category"), Q1 = t.GetNullableDecimal("[1]") }));

        var derived2 = ctx.From<ISalesEntity>()
            .Where(s => s.Margin > 0)
            .Select(s => new { s.Category, s.Quarter, s.Margin });
        var second = SqlOfCached(ctx, ctx.From(derived2)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.Quarter, PivotValue.Create("1"))
            .Select(t => new { Category = t.GetString("category"), Q1 = t.GetNullableDecimal("[1]") }));

        second.Should().Be(first);
        second.Should().Contain("from (select category, quarter, margin from sales");
        second.Should().Contain("pivot (sum(margin) for quarter in ([1]))");
    }

    [Fact]
    public void Unpivot_ShouldEmitUnpivotClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IQuarterlyEntity>();

        SqlOf(ctx, e
            .Unpivot("val", "qtr", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(t => new
            {
                Category = t.GetString("category"),
                Qtr = t.GetString("qtr"),
                Val = t.GetNullableDecimal("val")
            }))
            .Should().Be("select category, qtr, val from quarterly unpivot ([val] for [qtr] in ([q1], [q2])) as [t1]");
    }

    [Fact]
    public void Pivot_ShouldAcceptDerivedSource()
    {
        using var ctx = SqlServerTestContext.Create();
        var derived = ctx.From<ISalesEntity>()
            .Where(s => s.Margin > 0)
            .Select(s => new { s.Category, s.Quarter, s.Margin });

        SqlOf(ctx, ctx.From(derived)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.Quarter, PivotValue.Create("1"), PivotValue.Create("2"))
            .Select(t => new { Category = t.GetString("category"), Q1 = t.GetNullableDecimal("[1]") }))
            .Should().Be("select category, [1] from (select category, quarter, margin from sales\n where (margin > cast(0 as decimal(38, 10)))) as [t1] pivot (sum(margin) for quarter in ([1], [2])) as [t2]");
    }

    [Fact]
    public void Pivot_ShouldAcceptDerivedComputedSource()
    {
        using var ctx = SqlServerTestContext.Create();
        var derived = ctx.From<ISalesEntity>()
            .Where(s => s.Margin > 0)
            .Select(s => new
            {
                CategoryName = s.Category,
                QuarterNum = "Q" + s.Quarter,
                Margin = s.Margin * 2
            });

        SqlOf(ctx, ctx.From(derived)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.QuarterNum, PivotValue.Create("Q1"))
            .Select(t => new { Category = t.GetString("CategoryName"), Q1 = t.GetNullableDecimal("[Q1]") }))
            .Should().Be("select CategoryName, [Q1] from (select category as [CategoryName], ('Q'+quarter) as [QuarterNum], (margin * cast(2 as decimal(38, 10))) as [Margin] from sales\n where (margin > cast(0 as decimal(38, 10)))) as [t1] pivot (sum([Margin]) for [QuarterNum] in ([Q1])) as [t2]");
    }

    [Fact]
    public void Unpivot_ShouldAcceptDerivedSource()
    {
        using var ctx = SqlServerTestContext.Create();
        var derived = ctx.From<IQuarterlyEntity>()
            .Select(s => new { s.Category, s.Q1, s.Q2 });

        SqlOf(ctx, ctx.From(derived)
            .Unpivot("val", "qtr", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(t => new { Category = t.GetString("category"), Val = t.GetNullableDecimal("val") }))
            .Should().Be("select category, val from (select category, q1, q2 from quarterly) as [t1] unpivot ([val] for [qtr] in ([q1], [q2])) as [t2]");
    }

    [Fact]
    public void Pivot_ShouldThrowWhenSourceIsFiltered()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();

        var act = () => e.Where(s => s.Quarter > 0)
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"));

        act.Should().Throw<NotSupportedException>().WithMessage("*plain table*");
    }

    [Fact]
    public void Pivot_ShouldThrowWhenDerivedSourceHasModifiers()
    {
        using var ctx = SqlServerTestContext.Create();
        var derived = ctx.From<ISalesEntity>().Select(s => new { s.Category, s.Quarter, s.Margin });

        var ordered = () => ctx.From(derived).OrderBy(x => x.Quarter)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.Quarter, PivotValue.Create("1"));
        ordered.Should().Throw<NotSupportedException>().WithMessage("*derived query*");

        var filtered = () => ctx.From(derived).Where(x => x.Quarter > 0)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.Quarter, PivotValue.Create("1"));
        filtered.Should().Throw<NotSupportedException>().WithMessage("*derived query*");
    }

    [Fact]
    public void Pivot_ShouldThrowWhenSourceHasModifiers()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();

        var limited = () => e.Limit(1).Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"));
        limited.Should().Throw<NotSupportedException>().WithMessage("*plain table*");

        var sampled = () => e.TableSample(10).Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"));
        sampled.Should().Throw<NotSupportedException>().WithMessage("*plain table*");

        var ordered = () => e.OrderBy(x => x.Id).Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"));
        ordered.Should().Throw<NotSupportedException>().WithMessage("*plain table*");

        var hinted = () => e.WithTableHint("nolock").Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"));
        hinted.Should().Throw<NotSupportedException>().WithMessage("*plain table*");
    }

    [Fact]
    public void Pivot_ShouldThrowWithoutValues()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();

        var act = () => e.Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter);

        act.Should().Throw<ArgumentException>().WithMessage("*at least one value*");
    }

    [Fact]
    public void Unpivot_ShouldThrowWithoutColumns()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IQuarterlyEntity>();

        var act = () => e.Unpivot("val", "qtr");

        act.Should().Throw<ArgumentException>().WithMessage("*at least one column*");
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

    [Fact]
    public void DerivedSourceThenJoin_ShouldRenderDerivedTable()
    {
        using var ctx = SqlServerTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Where(c => c.Id > 0)
            .Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id, p.Item1.String }));

        sql.Should().Be("select t1.id, t2.id as [SId], t1.[String] from (select id, somestring as [String] from complex_entity\n where (id > 0)) as [t1] join simple_entity as [t2] on t1.id = cast(t2.id as bigint)");
    }

    [Fact]
    public void DerivedSourceWithJoinThenJoin_ShouldResolveTheOuterAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var derived = ctx.From<ISimpleEntity>()
            .Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
            .Select(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<IComplexEntity>(), (d, c2) => d.OrderId == c2.Id)
            .Select(p => new { p.Item1.OrderId, p.Item1.CustomerName, Third = p.Item2.Id }));

        sql.Should().Be("select t3.[OrderId], t3.[CustomerName], t4.id as [Third] from (select t1.id as [OrderId], t2.somestring as [CustomerName] from simple_entity as [t1] join complex_entity as [t2] on cast(t1.id as bigint) = t2.id) as [t3] join complex_entity as [t4] on cast(t3.[OrderId] as bigint) = t4.id");
    }

    [Fact]
    public void DerivedSourceWhereThenJoin_ShouldPushTheFilterOntoTheProjection()
    {
        using var ctx = SqlServerTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Where(d => d.Id > 5)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id }));

        sql.Should().Be("select t1.id, t2.id as [SId] from (select id, somestring as [String] from complex_entity) as [t1] join simple_entity as [t2] on t1.id = cast(t2.id as bigint)\n where (t1.id > 5)");
    }

    [Fact]
    public void DerivedSourceDistinctThenJoin_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var derived = ctx.From<IComplexEntity>().Select(c => new { c.Id });

        var act = () => SqlOf(ctx, ctx.From(derived)
            .Distinct()
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*only a Where clause*");
    }

    [Fact]
    public void DerivedSourceOrderByThenJoin_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var derived = ctx.From<IComplexEntity>().Select(c => new { c.Id });

        var act = () => SqlOf(ctx, ctx.From(derived)
            .OrderBy(d => d.Id)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*only a Where clause*");
    }
}
