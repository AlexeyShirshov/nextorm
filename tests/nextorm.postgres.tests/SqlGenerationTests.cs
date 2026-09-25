using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;
using NpgsqlTypes;

namespace NextORM.Postgres.Tests;

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
    public void IndexHint_ShouldThrowBecauseNotSupported()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.WithIndex("idx_id").Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Index hints*");
    }

    [Fact]
    public void KeywordCase_Upper_ShouldUppercaseSkeletonAliasesAndPaging()
    {
        using var ctx = PostgresTestContext.CreateUppercase();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Limit(3).Offset(2).Select(x => new { X = x.Id }));

        sql.Should().Be("SELECT id AS \"X\" FROM simple_entity\nLIMIT 3 OFFSET 2");
    }

    [Fact]
    public void Pivot_ShouldThrowBecauseNotSupported()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e
            .Pivot(PivotAggregate.Count, s => s.Id, s => s.Id, PivotValue.Create("1"))
            .Select(t => new { V = t.GetNullableInt32("1") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*PIVOT*");
    }

    [Fact]
    public void NamingConvention_WithQuotedIdentifiers_ShouldQuoteTranslatedNamesWithTheProviderDelimiter()
    {
        using var ctx = PostgresTestContext.CreateSnakeCaseQuoted();

        SqlOf(ctx, ctx.From<BareEntity>().Select(x => new { x.Id, x.Name }))
            .Should().Be("select \"id\", \"name\" from \"bare_entity\"");
    }

    private static string SqlOfCached<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void SelectDistinct_ShouldEmitDistinct()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Distinct().Select(x => new { x.Id })).Should().Be("select distinct id from simple_entity");
    }

    [Fact]
    public void QuotedIdentifiers_ShouldUseDoubleQuotes()
    {
        using var ctx = PostgresTestContext.CreateQuoted();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select \"id\" from \"simple_entity\"");
    }

    [Fact]
    public void QuotedIdentifiers_CommandOverride_ShouldDisable()
    {
        using var ctx = PostgresTestContext.CreateQuoted();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id }).WithQuotedIdentifiers(false)).Should().Be("select id from simple_entity");
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
    public void ColumnByName_ShouldRenderColumnIdentifierAndRenameAlias()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .Select(x => new { Region = SqlFunctions.Column<int>(x, "region_id") }));

        sql.Should().Be("select region_id as \"Region\" from simple_entity");
    }

    [Fact]
    public void Parameter_ShouldUseAtPrefix()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == SqlFunctions.Parameter<int>(0)).Select(x => new { x.Id }));

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

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.count())).Should().Contain("count(*)");
    }

    [Fact]
    public void CountBig_ShouldUseCountStar()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.count_big())).Should().Contain("count(*)");
    }

    [Fact]
    public void GroupBy_ScalarKey_ShouldMatchAnonymousKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var anonymous = SqlOf(ctx, e
            .GroupBy(x => new { x.Int })
            .Select(x => new { x.Int, count = SqlFunctions.Sql.count() }));

        var scalar = SqlOf(ctx, e
            .GroupBy(x => x.Int)
            .Select(x => new { x.Int, count = SqlFunctions.Sql.count() }));

        scalar.Should().Contain("group by nullableint");
        scalar.Should().Be(anonymous);
    }

    [Fact]
    public void Stdev_ShouldMapToStddev()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.Sql.stdev((double)x.Id)));

        sql.Should().Contain("stddev(");
        sql.Should().NotContain("stdev(");
    }

    [Fact]
    public void Variance_ShouldMapToVarPop()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.Sql.varp((double)x.Id)));

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

        var sql = SqlOf(ctx, simple.Join(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain("as \"t1\"");
        sql.Should().Contain("as \"t2\"");
    }

    [Fact]
    public void LeftJoin_ShouldEmitLeftJoinWithOn()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" left join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void RightJoin_ShouldEmitRightJoinWithOn()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.RightJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" right join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void FullJoin_ShouldEmitFullJoinWithOn()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.FullJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" full join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void JoinStrictness_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id)
            .WithStrictness(JoinStrictness.Any)
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        act.Should().Throw<NotSupportedException>().WithMessage("*join modifier is not supported*");
    }

    [Fact]
    public void GlobalJoin_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id)
            .Global()
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        act.Should().Throw<NotSupportedException>().WithMessage("*GLOBAL join modifier is not supported*");
    }

    [Fact]
    public void SemiAntiPasteJoin_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var semi = () => SqlOf(ctx, simple.SemiJoin(complex, (s, c) => s.Id == c.Id).Select(s => new { s.Id }));
        semi.Should().Throw<NotSupportedException>().WithMessage("*Semi join is not supported*");

        var anti = () => SqlOf(ctx, simple.AntiJoin(complex, (s, c) => s.Id == c.Id).Select(s => new { s.Id }));
        anti.Should().Throw<NotSupportedException>().WithMessage("*Anti join is not supported*");

        var paste = () => SqlOf(ctx, simple.PasteJoin(complex).Select(p => new { p.Item1.Id, p.Item2.String }));
        paste.Should().Throw<NotSupportedException>().WithMessage("*PASTE join is not supported*");
    }

    [Fact]
    public void CrossJoin_ShouldEmitCrossJoinWithoutOn()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.CrossJoin(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" cross join complex_entity");
        sql.Should().NotContain(" on ");
    }

    [Fact]
    public void CrossApply_OnPlainTable_ShouldEmitCrossJoinWithoutLateral()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.CrossApply(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" cross join complex_entity as \"t2\"");
        sql.Should().NotContain("lateral");
        sql.Should().NotContain(" on true");
    }

    [Fact]
    public void OuterApply_OnPlainTable_ShouldEmitLeftJoinOnTrueWithoutLateral()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.OuterApply(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" left join complex_entity as \"t2\" on true");
        sql.Should().NotContain("lateral");
    }

    [Fact]
    public void CrossApply_ToSubquery_ShouldEmitDerivedTable()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var subQuery = ctx.From<IComplexEntity>().Where(c => c.Id > 1).Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, simple.CrossApply(subQuery).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain("cross join lateral (select id, somestring as \"String\" from complex_entity");
        sql.Should().Contain(") as \"t2\"");
    }

    [Fact]
    public void CrossApply_ToCorrelatedSubquery_ShouldEmitCrossJoinLateral()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .CrossApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id).Select(c => new { c.Id, c.String }))
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Be("select t1.id, t3.\"String\" from simple_entity as \"t1\" cross join lateral (select t2.id, t2.somestring as \"String\" from complex_entity as \"t2\"\n"
            + " where t2.id = cast(t1.id as bigint)) as \"t3\"");
    }

    [Fact]
    public void OuterApply_ToCorrelatedSubquery_ShouldEmitLeftJoinLateralOnTrue()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .OuterApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id).Select(c => new { c.Id, c.String }))
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Be("select t1.id, t3.\"String\" from simple_entity as \"t1\" left join lateral (select t2.id, t2.somestring as \"String\" from complex_entity as \"t2\"\n"
            + " where t2.id = cast(t1.id as bigint)) as \"t3\" on true");
    }

    [Fact]
    public void CrossApply_ToCorrelatedBuilderSource_ShouldReferenceProjectedAlias()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .CrossApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id))
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        // The derived table exposes the entity's columns under their projected names, so the
        // outer reference must use the alias ("String"), not the physical column (somestring).
        sql.Should().Contain("select t1.id, t3.\"String\" from");
        sql.Should().Contain("cross join lateral (select t2.id, t2.nullableint as \"Int\", t2.somestring as \"String\"");
        sql.Should().NotContain("t3.somestring");
    }

    [Fact]
    public void QueryHint_ShouldEmitInlineOptimizerHintComment()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { x.Id }).Hint("recompile"));

        sql.Should().Be("select /*+ recompile */ id from simple_entity");
    }

    [Fact]
    public void QueryHint_WithCte_ShouldPlaceHintAfterTheTopLevelSelect()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cte = e.Where(x => x.Id > 1).Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.With("recent", cte).From("recent").Select(t => new { id = t["id"].AsInt })
            .Hint("SeqScan(recent)"));

        sql.Should().StartWith("with recent as (select id from complex_entity");
        sql.Should().Contain(") select /*+ SeqScan(recent) */ id");
        sql.Should().EndWith("from recent");
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

        SqlOf(ctx, e.Where(x => SqlFunctions.Sql.like(x.String, "%a%")).Select(x => new { x.Id }))
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
    public void MathRoundWithDigits_ShouldCastDoublePrecisionToNumeric()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // PostgreSQL has no round(double precision, integer), so the first argument is cast.
        SqlOf(ctx, e.Select(x => new { V = Math.Round(x.Id / 2.0 + 0.2, 2) }))
            .Should().Contain("round((((cast(id as double precision) / 2) + 0.2))::numeric, 2)");
    }

    [Fact]
    public void MathRoundWithDigits_ShouldNotCastNumeric()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Math.Round(1.25m, 1) }))
            .Should().Contain("round(1.25, 1)").And.NotContain("::numeric");
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
        SqlOf(ctx, e.Select(x => new { DOY = x.Datetime!.Value.DayOfYear })).Should().Contain("extract(doy from dt)");
    }

    [Fact]
    public void Extract_ShouldUsePostgresDatePartForms()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { Q = SqlFunctions.Sql.extract("quarter", x.Datetime) }))
            .Should().Contain("extract(quarter from dt)");
        SqlOf(ctx, e.Select(x => new { W = SqlFunctions.Sql.extract("week", x.Datetime) }))
            .Should().Contain("extract(week from dt)");
        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.extract("dow", x.Datetime) }))
            .Should().Contain("extract(dow from dt)");
        SqlOf(ctx, e.Select(x => new { I = SqlFunctions.Sql.extract("isodow", x.Datetime) }))
            .Should().Contain("extract(isodow from dt)");
        SqlOf(ctx, e.Select(x => new { E = SqlFunctions.Sql.date_part("epoch", x.Datetime) }))
            .Should().Contain("cast(extract(epoch from dt) as double precision)");
    }

    [Fact]
    public void DatePart_UnsupportedSurface_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var extractEpoch = () => SqlOf(ctx, e.Select(x => new { E = SqlFunctions.Sql.extract("epoch", x.Datetime) }));
        extractEpoch.Should().Throw<NotSupportedException>();

        var datePartYear = () => SqlOf(ctx, e.Select(x => new { Y = SqlFunctions.Sql.date_part("year", x.Datetime) }));
        datePartYear.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InValues_ShouldRenderInPredicateWithParameters()
    {
        using var ctx = PostgresTestContext.Create();
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
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, 1L, 2L)).Select(x => new { x.Id }));

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

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("1 = 0");
        command.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
    }

    [Fact]
    public void InValues_SingleElement_ShouldRenderInPredicate()
    {
        using var ctx = PostgresTestContext.Create();
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
        using var ctx = PostgresTestContext.Create();
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
        using var ctx = PostgresTestContext.Create();
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

        var command = Prepare(ctx, e.Where(x => x.Id == SqlFunctions.Postgres.any(SqlFunctions.Parameter<long[]>(0))).Select(x => new { x.Id }));

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

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Postgres.any(x.Id, values)).Select(x => new { x.Id }));

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

        var command = Prepare(ctx, e.Where(x => x.Id == SqlFunctions.Postgres.all(SqlFunctions.Parameter<long[]>(0))).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = all(@norm_p0)");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void InlineArray_ShouldBindAsSingleParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == SqlFunctions.Postgres.any(new long[] { 1, 2, 3 })).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = any(@p0)");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle();
    }

    [Fact]
    public void ArrayFunction_Cardinality_ShouldEmitFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { N = SqlFunctions.Postgres.cardinality(SqlFunctions.Parameter<long[]>(0)) }))
            .Should().Contain("cardinality(@norm_p0)");
    }

    [Fact]
    public void ArrayFunction_ArrayLengthAndPosition_ShouldEmitFunctions()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e
            .Where(x => SqlFunctions.Postgres.array_length(SqlFunctions.Parameter<long[]>(0), 1) == 3)
            .Where(x => SqlFunctions.Postgres.array_position(SqlFunctions.Parameter<long[]>(1), x.Id) == 1)
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

        SqlOf(ctx, e.Select(x => new { S = SqlFunctions.Postgres.array_to_string(SqlFunctions.Parameter<long[]>(0), ",") }))
            .Should().Contain("array_to_string(@norm_p0, ',')");
    }

    [Fact]
    public void ArrayOperator_ContainsAndOverlaps_ShouldEmitOperators()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e
            .Where(x => SqlFunctions.Postgres.array_contains(SqlFunctions.Parameter<long[]>(0), SqlFunctions.Parameter<long[]>(1)))
            .Where(x => SqlFunctions.Postgres.array_overlaps(SqlFunctions.Parameter<long[]>(2), SqlFunctions.Parameter<long[]>(3)))
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

        SqlOf(ctx, e.Select(x => SqlFunctions.Postgres.json_agg(x.String))).Should().Contain("json_agg(somestring)");
        SqlOf(ctx, e.Select(x => SqlFunctions.Postgres.jsonb_agg(x.String))).Should().Contain("jsonb_agg(somestring)");
    }

    [Fact]
    public void JsonObjectAgg_ShouldEmitAggregate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.Postgres.jsonb_object_agg(x.Id, x.String)))
            .Should().Contain("jsonb_object_agg(id, somestring)");
    }

    [Fact]
    public void JsonBuildObject_ShouldEmitFunctionWithKeyValueArguments()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.json_build_object("id", x.Id, "s", x.String) }))
            .Should().Contain("json_build_object('id', id, 's', somestring) as \"V\"");
    }

    [Fact]
    public void ToJsonb_ShouldEmitConversionFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_jsonb(x.Id) })).Should().Contain("to_jsonb(id)");
    }

    [Fact]
    public void JsonGet_WithParameterAndKey_ShouldEmitAccessOperator()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e
            .Where(x => SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<JsonDocument>(0), "name") == "x")
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
            .Where(x => SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<JsonDocument>(0), 0) == "x")
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
            .Where(x => SqlFunctions.Postgres.json_get_path(SqlFunctions.Parameter<JsonDocument>(0), path) == "x")
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
            .Where(x => SqlFunctions.Postgres.json_contains(SqlFunctions.Parameter<JsonDocument>(0), SqlFunctions.Parameter<JsonDocument>(1)))
            .Where(x => SqlFunctions.Postgres.json_exists(SqlFunctions.Parameter<JsonDocument>(2), "key"))
            .Where(x => SqlFunctions.Postgres.json_exists_any(SqlFunctions.Parameter<JsonDocument>(3), new[] { "a", "b" }))
            .Where(x => SqlFunctions.Postgres.json_exists_all(SqlFunctions.Parameter<JsonDocument>(4), new[] { "a", "b" }))
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
            .Where(x => SqlFunctions.Postgres.json_exists(SqlFunctions.Postgres.json_cast(SqlFunctions.Parameter<string>(0)), "key"))
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
            L = SqlFunctions.Postgres.json_array_length(SqlFunctions.Parameter<JsonDocument>(0)),
            T = SqlFunctions.Postgres.json_typeof(SqlFunctions.Parameter<JsonDocument>(1))
        }));

        sql.Should().Contain("json_array_length(@norm_p0)");
        sql.Should().Contain("json_typeof(@norm_p1)");
    }

    [Fact]
    public void NullIf_ShouldEmitNullIf()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.nullif(x.Id, 0L) }))
            .Should().Contain("nullif(id, 0) as \"V\"");
    }

    [Fact]
    public void GreatestLeast_ShouldEmitFunctions()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            G = SqlFunctions.Sql.greatest(x.Id, x.Id),
            L = SqlFunctions.Sql.least(x.Id, x.Id)
        }));

        sql.Should().Contain("greatest(id, id) as \"G\"");
        sql.Should().Contain("least(id, id) as \"L\"");
    }

    [Fact]
    public void DateTrunc_ShouldEmitFunction()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { M = SqlFunctions.Sql.date_trunc("month", x.Datetime) }))
            .Should().Contain("date_trunc('month', dt) as \"M\"");
    }

    [Fact]
    public void DateArithmetic_ShouldEmitIntervalExpressions()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("day", 1, x.Datetime) }))
            .Should().Contain("dt + (1 * interval '1 day') as \"D\"");

        // Units PostgreSQL's interval literal has no part for are folded (decade -> 10 years).
        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("decade", 2, x.Datetime) }))
            .Should().Contain("dt + (2 * interval '10 years')");

        SqlOf(ctx, e.Select(x => new { E = SqlFunctions.Sql.end_of_month(x.Datetime) }))
            .Should().Contain("(date_trunc('month', dt) + interval '1 month - 1 day') as \"E\"");
    }

    [Fact]
    public void DateDiff_ShouldEmitDatePartDifference()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_diff("day", x.Datetime, x.Datetime) }))
            .Should().Contain("(cast(dt as date) - cast(dt as date)) as \"D\"");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_diff("month", x.Datetime, x.Datetime) }))
            .Should().Contain("(extract(year from dt) * 12 + extract(month from dt)) - (extract(year from dt) * 12 + extract(month from dt))");
    }

    [Fact]
    public void DateFromParts_ShouldEmitMakeDate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_from_parts(2023, 1, 31) }))
            .Should().Contain("make_date(2023, 1, 31) as \"D\"");
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

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.string_agg(x.String, ",")))
            .Should().Contain("string_agg(somestring, ',')");
        // array_agg produces an array column; the row reader materialises it as T[].
        SqlOf(ctx, e.Select(x => SqlFunctions.Postgres.array_agg(x.Id)))
            .Should().Contain("array_agg(id)");
    }

    [Fact]
    public void GroupArray_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.group_array(x.Id) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*groupArray/groupUniqArray*");
    }

    [Fact]
    public void HigherOrderArrayFunction_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_map(t => t, x.Tags)));

        act.Should().Throw<NotSupportedException>().WithMessage("*higher-order array functions*");
    }

    [Fact]
    public void ArrayRelationPredicates_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.has_substr(x.Tags, new[] { "a" })));

        act.Should().Throw<NotSupportedException>().WithMessage("*array functions*");
    }

    [Fact]
    public void TupleCreate_ShouldRenderRowConstructor()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => Tuple.Create(x.Id, x.String)))
            .Should().Contain("ROW(id, somestring)");
    }

    [Fact]
    public void TupleElementAccess_OnServerTuple_ShouldRenderRowField()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ITupleEntity>();

        SqlOf(ctx, e.Select(x => x.Pair.Item1))
            .Should().Contain("(pair).f1");
    }

    [Fact]
    public void TupleElementAccess_OnInlineConstructor_ShouldFoldToArgument()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => Tuple.Create(x.Id, x.String).Item2));

        sql.Should().Contain("somestring");
        sql.Should().NotContain("ROW(");
    }

    [Fact]
    public void TupleEquality_ShouldRenderRowComparison()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => Tuple.Create(x.Id, x.String) == Tuple.Create(1L, "a"))
            .Select(x => new { x.Id }));

        sql.Should().Contain("ROW(id, somestring) = ROW(1, 'a')");
    }

    [Fact]
    public void FilteredAggregates_ShouldEmitFilterClause()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C = SqlFunctions.Sql.count(() => x.Id > 1L),
            S = SqlFunctions.Sql.sum(x.Id, () => x.Boolean!.Value),
            M = SqlFunctions.Sql.max(x.Int, () => x.Id > 0L)
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

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.string_agg(x.String, ",", () => x.Id > 0L)))
            .Should().Contain("string_agg(somestring, ',') filter (where (id > 0))");
    }

    [Fact]
    public void TableFunction_GenerateSeries_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();
        var start = 1L;
        var stop = 3L;

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.generate_series(start, stop))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select generate_series as \"Value\" from (select generate_series from generate_series(@start, @stop)) as \"t1\"");
    }

    [Fact]
    public void TableFunction_Unnest_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select unnest as \"Value\" from (select unnest from unnest(@norm_p0)) as \"t1\"");
    }

    [Fact]
    public void TableFunction_RegexpMatches_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();
        var source = "a1b2";
        var pattern = "\\d";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.regexp_matches(source, pattern))
            .Select(r => new { r.Matches }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select regexp_matches as \"Matches\" from (select regexp_matches from regexp_matches(@source, @pattern)) as \"t1\"");
    }

    [Fact]
    public void TableFunction_RegexpSplitToTable_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();
        var source = "a,b";
        var pattern = ",";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.regexp_split_to_table(source, pattern))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select regexp_split_to_table as \"Value\" from (select regexp_split_to_table from regexp_split_to_table(@source, @pattern)) as \"t1\"");
    }

    [Fact]
    public void TableFunction_JsonbArrayElements_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_array_elements(SqlFunctions.Parameter<JsonDocument>(0)))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select value from jsonb_array_elements(@norm_p0) as \"t1\"");
    }

    [Fact]
    public void TableFunction_JsonbArrayElementsText_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_array_elements_text(SqlFunctions.Parameter<JsonDocument>(0)))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select value from jsonb_array_elements_text(@norm_p0) as \"t1\"");
    }

    [Fact]
    public void TableFunction_JsonbEach_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_each(SqlFunctions.Parameter<JsonDocument>(0)))
            .Select(r => new { r.Key, r.Value }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select key, value from jsonb_each(@norm_p0) as \"t1\"");
    }

    [Fact]
    public void TableFunction_JsonbEachText_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_each_text(SqlFunctions.Parameter<JsonDocument>(0)))
            .Select(r => new { r.Key, r.Value }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select key, value from jsonb_each_text(@norm_p0) as \"t1\"");
    }

    [Fact]
    public void TableFunction_JsonbToRecord_ShouldRenderAliasColumnList()
    {
        using var ctx = PostgresTestContext.Create();

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_to_record<IDynamicRecordRow>(SqlFunctions.Parameter<JsonDocument>(0)))
            .Select(r => new { r.A, r.B }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select a, b from jsonb_to_record(@norm_p0) as \"t1\"(a integer, b text)");
    }

    [Fact]
    public void TableFunction_JsonbToRecordset_ShouldRenderAliasColumnList()
    {
        using var ctx = PostgresTestContext.Create();

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_to_recordset<IDynamicRecordRow>(SqlFunctions.Parameter<JsonDocument>(0)))
            .Select(r => new { r.A, r.B }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select a, b from jsonb_to_recordset(@norm_p0) as \"t1\"(a integer, b text)");
    }

    [Fact]
    public void TableFunction_WithUnsupportedLeadingArgument_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => SqlOf(ctx, ctx
            .FromTableFunction(() => MismatchedTvf.Values("a integer", "(1)"))
            .Select(r => new { r.A }));

        act.Should().Throw<NotSupportedException>().WithMessage("*LeadingArgument*");
    }

    [Fact]
    public void ClickHouseValuesFunction_ShouldThrowBecauseOnlyClickHouseHasIt()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => SqlOf(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.values<IDynamicRecordRow>("(1, 'x')"))
            .Select(r => new { r.A }));

        act.Should().Throw<NotSupportedException>().WithMessage("*values*");
    }

    [Fact]
    public void TableFunction_JsonbObjectKeys_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_object_keys(SqlFunctions.Parameter<JsonDocument>(0)))
            .Select(r => new { r.Key }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select jsonb_object_keys as \"Key\" from (select jsonb_object_keys from jsonb_object_keys(@norm_p0)) as \"t1\"");
    }

    [Fact]
    public void TableFunction_JsonbPathQuery_ShouldCastPathAndEmitCall()
    {
        using var ctx = PostgresTestContext.Create();
        var path = "$.a";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_path_query(
                SqlFunctions.Parameter<JsonDocument>(0),
                SqlFunctions.Postgres.jsonpath(path)))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select jsonb_path_query as \"Value\" from (select jsonb_path_query from jsonb_path_query(@norm_p0, cast(@path as jsonpath))) as \"t1\"");
    }

    [Fact]
    public void TableFunction_TsStat_ShouldEmitCall()
    {
        using var ctx = PostgresTestContext.Create();
        var query = "select to_tsvector('a b')";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Postgres.ts_stat(query))
            .Select(r => new { r.Word, r.Ndoc, r.Nentry }));

        Normalize(command.DbCommand.CommandText)
            .Should().Be("select word, ndoc, nentry from ts_stat(@query) as \"t1\"");
    }

    [Fact]
    public void BuiltInTableFunction_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var csv = "a,b";

        var act = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.SqlServer.string_split(csv, ","))
            .Select(r => new { r.Value }));

        act.Should().Throw<NotSupportedException>().WithMessage("*string_split*");
    }

    [Fact]
    public void BuiltInTableFunction_Containstable_ShouldThrowOnPostgres()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => SqlOf(ctx, ctx
            .FromTableFunction(() => SqlFunctions.SqlServer.containstable<int>("docs", "body", "cat"))
            .Select(r => new { r.Key }));

        act.Should().Throw<NotSupportedException>().WithMessage("*containstable*");
    }

    [Theory]
    [InlineData("url")]
    [InlineData("s3")]
    [InlineData("file")]
    [InlineData("remote")]
    [InlineData("remoteSecure")]
    [InlineData("cluster")]
    [InlineData("clusterAllReplicas")]
    public void BuiltInTableFunction_ClickHouseServerTableFunctions_ShouldThrowOnPostgres(string name)
    {
        using var ctx = PostgresTestContext.Create();
        var location = "http://127.0.0.1/data.csv";
        var format = "CSV";
        var structure = "id UInt64, name String";
        var addresses = "127.0.0.1:9000";
        var cluster = "my_cluster";
        var database = "default";
        var table = "hits";

        var source = name switch
        {
            "url" => ctx.FromTableFunction(() => SqlFunctions.ClickHouse.url<ISimpleEntity>(location, format, structure)),
            "s3" => ctx.FromTableFunction(() => SqlFunctions.ClickHouse.s3<ISimpleEntity>(location, format, structure)),
            "file" => ctx.FromTableFunction(() => SqlFunctions.ClickHouse.file<ISimpleEntity>(location, format, structure)),
            "remote" => ctx.FromTableFunction(() => SqlFunctions.ClickHouse.remote<ISimpleEntity>(addresses, database, table)),
            "remoteSecure" => ctx.FromTableFunction(() => SqlFunctions.ClickHouse.remote_secure<ISimpleEntity>(addresses, database, table)),
            "cluster" => ctx.FromTableFunction(() => SqlFunctions.ClickHouse.cluster<ISimpleEntity>(cluster, database, table)),
            "clusterAllReplicas" => ctx.FromTableFunction(() => SqlFunctions.ClickHouse.cluster_all_replicas<ISimpleEntity>(cluster, database, table)),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown ClickHouse server table function.")
        };

        var act = () => SqlOf(ctx, source.Select(r => new { r.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*");
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
    public void Cte_JoinedToAnotherCte_ShouldQualifyAliasColumns()
    {
        using var ctx = PostgresTestContext.Create();

        var left = ctx.From<IComplexEntity>().Select(x => new { x.Id });
        var right = ctx.From<ISimpleEntity>().Select(x => new { x.Id });

        var sql = SqlOf(ctx, ctx.With("l", left).With("r", right)
            .From("l")
            .Join(ctx.From("r"), (l, r) => l.GetInt64("id") == r.GetInt64("id"))
            .Select(p => new { Id = p.Item1.GetInt64("id"), Other = p.Item2.GetInt64("id") }));

        sql.Should().Be("with l as (select id from complex_entity), r as (select id from simple_entity) select t1.id, t2.id from l as \"t1\" join r as \"t2\" on t1.id = t2.id");
    }

    [Fact]
    public void Cte_Source_ShouldSupportGroupByHavingOrderByLimit()
    {
        using var ctx = PostgresTestContext.Create();
        var cte = ctx.From<IComplexEntity>().Select(x => new { x.Id, somestring = x.String });

        var sql = SqlOf(ctx, ctx.With("recent", cte)
            .From("recent")
            .Where(t => t.GetInt64("id") > 0)
            .GroupBy(t => new { somestring = t.GetString("somestring") })
            .Having(t => SqlFunctions.Sql.count() > 1)
            .OrderBy(t => t.GetString("somestring"))
            .Limit(5)
            .Select(t => new { somestring = t.GetString("somestring"), count = SqlFunctions.Sql.count() }));

        sql.Should().StartWith("with recent as (select id, somestring from complex_entity) select somestring, count(*) as \"count\" from recent");
        sql.Should().Contain("group by somestring");
        sql.Should().Contain("having (count(*) > 1)");
        sql.Should().Contain("order by somestring");
        sql.Should().EndWith("limit 5");
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
    public void Cte_Recursive_WithDistinctUnion_ShouldEmitUnionNotUnionAll()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var anchor = e.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.Union(step);

        var sql = SqlOf(ctx, ctx.WithRecursive("nums", body).From("nums").Select(t => new CteNumberRow { n = t["n"].AsInt * 2 }));

        // PostgreSQL uses the ANSI `with recursive` form and has no maxrecursion option.
        sql.Should().StartWith("with recursive nums as (");
        sql.Should().Contain(" union ");
        sql.Should().NotContain("union all");
        sql.Should().Contain("(n * 2)");
        sql.Should().NotContain("from (select");
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
            rn = SqlFunctions.Sql.row_number().Over(partitionBy: () => x.Int, orderBy: () => x.Id)
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
            r = SqlFunctions.Sql.rank().Over(SqlFunctions.Sql.asc(() => x.Id)),
            dr = SqlFunctions.Sql.dense_rank().Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Be("select id, rank() over (order by id) as \"r\", dense_rank() over (order by id) as \"dr\" from complex_entity");
    }

    [Fact]
    public void PercentRankCumeDist_ShouldEmitOverWithOrder()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            pr = SqlFunctions.Sql.percent_rank().Over(SqlFunctions.Sql.asc(() => x.Id)),
            cd = SqlFunctions.Sql.cume_dist().Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Be("select id, percent_rank() over (order by id) as \"pr\", cume_dist() over (order by id) as \"cd\" from complex_entity");
    }

    [Fact]
    public void UniqAggregates_ShouldThrowBecausePostgresHasNoUniq()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.uniq_exact(x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("*uniq*");
    }

    [Fact]
    public void QuantileAggregates_ShouldThrowBecausePostgresHasNoQuantile()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.quantile(0.5, x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("*quantile*");
    }

    [Fact]
    public void QuantilesAggregates_ShouldThrowBecausePostgresHasNoQuantile()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.quantiles(new[] { 0.25, 0.5 }, x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("*quantile*");
    }

    [Fact]
    public void TopKAggregates_ShouldThrowBecausePostgresHasNoTopK()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.top_k(3, x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("*topK*");
    }

    [Fact]
    public void AnyAggregates_ShouldThrowBecausePostgresHasNoAnyAggregate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.Sql.any_agg(x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("*any*");
    }

    [Fact]
    public void JsonExtract_ShouldThrowBecausePostgresHasNoJsonExtract()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        Action[] acts =
        [
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_string(x.String, "s") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_int(x.String, "n") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_float(x.String, "f") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_bool(x.String, "b") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_raw(x.String, "o") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_has(x.String, "s") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_length(x.String, "a") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_type(x.String, "s") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.visit_param_extract_string(x.String, "s") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.visit_param_extract_int(x.String, "n") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.visit_param_extract_float(x.String, "f") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.visit_param_extract_bool(x.String, "b") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.visit_param_extract_raw(x.String, "o") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_keys(x.String) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_array_raw(x.String, "a") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_keys_and_values<int>(x.String) }))
        ];

        foreach (var act in acts)
            act.Should().Throw<NotSupportedException>().WithMessage("*JSONExtract*");
    }

    [Fact]
    public void NativeJsonFunctions_ShouldThrowBecausePostgresHasNoNativeJson()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        Action[] acts =
        [
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_all_paths(x.String) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_all_paths_with_types(x.String) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_json_string(x.String) }))
        ];

        foreach (var act in acts)
            act.Should().Throw<NotSupportedException>().WithMessage("*native-JSON*");
    }

    [Fact]
    public void DictFunctions_ShouldThrowBecausePostgresHasNoDictionaries()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        Action[] acts =
        [
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dict_get<string, long>("d", "a", x.Id) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dict_get_or_default<string, long>("d", "a", x.Id, "n/a") })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dict_has<long>("d", x.Id) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dict_get_hierarchy<long>("d", x.Id) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dict_get_children<long>("d", x.Id) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dict_is_in<long>("d", x.Id, 3L) }))
        ];

        foreach (var act in acts)
            act.Should().Throw<NotSupportedException>().WithMessage("*dictionary*");
    }

    [Fact]
    public void DateConversionFunctions_ShouldThrowBecausePostgresHasNoClickHouseDateSurface()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        Action[] acts =
        [
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_date(x.String) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_date_time(x.String) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_date32(x.String) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_year(x.Datetime) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_day_of_week(x.Datetime) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_start_of_month(x.Datetime) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_monday(x.Datetime) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_yyyymm(x.Datetime) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_yyyymmdd(x.Datetime) })),
            () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_unix_timestamp(x.Datetime) }))
        ];

        foreach (var act in acts)
            act.Should().Throw<NotSupportedException>().WithMessage("*not supported by this provider*");
    }

    [Fact]
    public void Split_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = x.String!.Split(',') }));

        act.Should().Throw<NotSupportedException>().WithMessage("*string.Split*");
    }

    [Fact]
    public void SessionInfoFunctions_ShouldUsePostgresNames()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            T = SqlFunctions.Postgres.pg_typeof(x.Id),
            CU = SqlFunctions.Sql.current_user(),
            SU = SqlFunctions.Sql.session_user(),
            CS = SqlFunctions.Sql.current_schema(),
            CD = SqlFunctions.Sql.current_database(),
            Ver = SqlFunctions.Sql.version()
        }));

        sql.Should().Contain("cast(pg_typeof(id) as text)");
        sql.Should().Contain("current_user");
        sql.Should().Contain("session_user");
        sql.Should().Contain("current_schema");
        sql.Should().Contain("current_database()");
        sql.Should().Contain("version()");
    }

    [Fact]
    public void UuidGenerators_ShouldUsePostgresNames()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            U = SqlFunctions.Sql.gen_random_uuid(),
            V7 = SqlFunctions.Sql.uuidv7()
        }));

        sql.Should().Contain("gen_random_uuid()");
        sql.Should().Contain("uuidv7()");
    }

    [Fact]
    public void GroupByWithTotals_ShouldThrowBecausePostgresHasNoTotals()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.GroupBy(x => new { x.Int }).WithTotals().Select(x => new { x.Int }));

        act.Should().Throw<NotSupportedException>().WithMessage("*WITH TOTALS*");
    }

    [Fact]
    public void BuiltInTableFunction_Numbers_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var count = 3L;

        var act = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.ClickHouse.numbers(count))
            .Select(r => new { r.Value }));

        act.Should().Throw<NotSupportedException>().WithMessage("*numbers*");
    }

    [Fact]
    public void BuiltInTableFunction_Zeros_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var count = 3L;

        var act = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.ClickHouse.zeros(count))
            .Select(r => new { r.Value }));

        act.Should().Throw<NotSupportedException>().WithMessage("*zeros*");
    }

    [Fact]
    public void BuiltInTableFunction_GenerateRandom_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.ClickHouse.generate_random())
            .Select(r => new { r.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*generateRandom*");
    }

    [Fact]
    public void GlobalIn_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.global_in(
                x.Id,
                ctx.From<ISimpleEntity>().Where(y => y.Id > 1).Select(y => y.Id)))
            .Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*GLOBAL IN*");
    }

    [Fact]
    public void GlobalIn_Values_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();
        var values = new List<int> { 1, 2 };

        var act = () => SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.global_in(x.Id, values))
            .Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*GLOBAL IN*");
    }

    [Fact]
    public void JsonPath_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e
            .Select(x => new { V = SqlFunctions.ClickHouse.json_value(x.String, "$.a") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*JSONPath*");
    }

    [Fact]
    public void QueryModifiers_ShouldThrowBecausePostgresHasNone()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        ((Action)(() => SqlOf(ctx, e.Final().Select(x => new { x.Id }))))
            .Should().Throw<NotSupportedException>().WithMessage("*FINAL*");

        ((Action)(() => SqlOf(ctx, ctx.From<IComplexEntity>(o => o.Sample(0.1)).Select(x => new { x.Id }))))
            .Should().Throw<NotSupportedException>().WithMessage("*SAMPLE*");

        ((Action)(() => SqlOf(ctx, e.PreWhere(x => x.Id > 0L).Select(x => new { x.Id }))))
            .Should().Throw<NotSupportedException>().WithMessage("*PREWHERE*");

        ((Action)(() => SqlOf(ctx, e.Settings(("max_threads", "2")).Select(x => new { x.Id }))))
            .Should().Throw<NotSupportedException>().WithMessage("*SETTINGS*");
    }

    [Fact]
    public void LimitBy_ShouldThrowBecausePostgresHasNoLimitBy()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.LimitBy(2, x => x.Int).Select(x => new { x.Int }));

        act.Should().Throw<NotSupportedException>().WithMessage("*LIMIT BY*");
    }

    [Fact]
    public void Iif_ShouldEmitCaseExpression()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.iif(x.Id > 0L, "yes", "no") }));

        sql.Should().Contain("case when");
        sql.Should().Contain("then 'yes' else 'no' end");
    }

    [Fact]
    public void NthValue_ShouldEmitOverWithOrder()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            V = SqlFunctions.Sql.nth_value(x.Id, 2).Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Contain("nth_value(id, 2) over (order by id)");
    }

    [Fact]
    public void Choose_ShouldThrowBecausePostgresHasNoChoose()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { C = SqlFunctions.SqlServer.choose(1, "a", "b") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*choose*");
    }

    [Fact]
    public void WindowOrderByDescending_ShouldEmitDesc()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            r = SqlFunctions.Sql.row_number().Over(
                partitionBy: new Expression<Func<object?>>[] { () => x.Int },
                orderBy: new[] { SqlFunctions.Sql.desc(() => x.Id) })
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
            prev = SqlFunctions.Sql.lag(x.Id, 1, 0L).Over(SqlFunctions.Sql.asc(() => x.Id)),
            next = SqlFunctions.Sql.lead(x.Int, 2, 0).Over(SqlFunctions.Sql.asc(() => x.Id))
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
            total = SqlFunctions.Sql.sum_over(x.Id).Over(partitionBy: () => x.Int),
            n = SqlFunctions.Sql.count_over().Over(partitionBy: () => x.Int)
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
            running = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.RowsUnboundedPrecedingToCurrentRow),
            sliding = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.Rows(1, 1))
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
            bucket = SqlFunctions.Sql.ntile(2).Over(SqlFunctions.Sql.asc(() => x.Id)),
            first = SqlFunctions.Sql.first_value(x.Int).Over(partitionBy: () => x.Int, orderBy: () => x.Id),
            last = SqlFunctions.Sql.last_value(x.Int).Over(partitionBy: () => x.Int, orderBy: () => x.Id)
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
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { x.Id, rn = SqlFunctions.Sql.row_number() }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Over*");
    }

    [Fact]
    public void WindowFrameGroups_ShouldEmitGroupsUnit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            v = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.Groups(1, 1))
        }));

        sql.Should().Contain("sum(id) over (order by id groups between 1 preceding and 1 following) as \"v\"");
    }

    [Fact]
    public void WindowFrameExclusion_ShouldEmitEveryVariant()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            cur = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.RowsUnboundedPrecedingToCurrentRow.WithExclusion(WindowFrameExclusion.CurrentRow)),
            grp = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.RowsUnboundedPrecedingToCurrentRow.WithExclusion(WindowFrameExclusion.Group)),
            tie = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.RowsUnboundedPrecedingToCurrentRow.WithExclusion(WindowFrameExclusion.Ties)),
            noo = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.RowsUnboundedPrecedingToCurrentRow.WithExclusion(WindowFrameExclusion.NoOthers))
        }));

        sql.Should().Contain("rows between unbounded preceding and current row exclude current row");
        sql.Should().Contain("rows between unbounded preceding and current row exclude group");
        sql.Should().Contain("rows between unbounded preceding and current row exclude ties");
        sql.Should().Contain("rows between unbounded preceding and current row exclude no others");
    }

    [Fact]
    public void NamedWindow_ShouldEmitWindowClauseAndOverReference()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var b = e.Window("w", partitionBy: [x => x.Int], orderBy: [e.Asc(x => x.Id)]);
        var sql = SqlOf(ctx, b.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over("w"),
            total = SqlFunctions.Sql.sum_over(x.Id).Over("w")
        }));

        sql.Should().Contain("row_number() over w as \"rn\"");
        sql.Should().Contain("sum(id) over w as \"total\"");
        sql.Should().Contain("window w as (partition by nullableint order by id)");
    }

    [Fact]
    public void NamedWindow_WithGroupsFrameAndExclusion_ShouldEmitInClause()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var b = e.Window(
            "w",
            orderBy: [e.Asc(x => x.Id)],
            frame: WindowFrame.Groups(1, 1).WithExclusion(WindowFrameExclusion.Ties));
        var sql = SqlOf(ctx, b.Select(x => new
        {
            x.Id,
            total = SqlFunctions.Sql.sum_over(x.Id).Over("w")
        }));

        sql.Should().Contain("window w as (order by id groups between 1 preceding and 1 following exclude ties)");
    }

    [Fact]
    public void NamedWindow_TwoWindows_ShouldEmitCommaSeparatedClause()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var b = e
            .Window("w1", orderBy: [e.Asc(x => x.Id)])
            .Window("w2", partitionBy: [x => x.Int]);
        var sql = SqlOf(ctx, b.Select(x => new
        {
            x.Id,
            r1 = SqlFunctions.Sql.rank().Over("w1"),
            r2 = SqlFunctions.Sql.count_over().Over("w2")
        }));

        sql.Should().Contain("window w1 as (order by id), w2 as (partition by nullableint)");
    }

    [Fact]
    public void NamedWindow_UnknownName_ShouldStillEmitReference()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over("notdeclared")
        }));

        sql.Should().Contain("row_number() over notdeclared");
    }

    [Fact]
    public void NamedWindow_InvalidReferenceName_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over("bad name")
        }));

        act.Should().Throw<ArgumentException>().WithMessage("*identifier*");
    }

    [Fact]
    public void NamedWindow_ShouldReuseCachedPlanWithoutLosingTheClause()
    {
        using var ctx = PostgresTestContext.Create();

        var e1 = ctx.From<IComplexEntity>();
        var first = SqlOfCached(ctx, e1.Window("w", orderBy: [e1.Asc(x => x.Id)]).Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over("w")
        }));

        // A fresh, structurally identical command must hit the cached plan. In Debug the plan's
        // GetCacheVersion re-checks the clone's plan hash, which includes WindowsPlanHash.
        var e2 = ctx.From<IComplexEntity>();
        var second = SqlOfCached(ctx, e2.Window("w", orderBy: [e2.Asc(x => x.Id)]).Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over("w")
        }));

        second.Should().Be(first);
        second.Should().Contain("window w as (order by id)");
    }

    [Fact]
    public void WindowFrameGroups_ExclusionOnPlainFrame_ShouldRenderExclusion()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            v = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.Range(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)
                    .WithExclusion(WindowFrameExclusion.Group))
        }));

        sql.Should().Contain("range between unbounded preceding and current row exclude group");
    }

    public interface ITvfRow
    {
        [Column("id")]
        long Id { get; set; }
        [Column("value")]
        string? Value { get; set; }
    }

    public interface IDynamicRecordRow
    {
        [Column("a")]
        int A { get; set; }
        [Column("b")]
        string? B { get; set; }
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

    private static class MismatchedTvf
    {
        [SqlTableFunction("user_values", ResultSchema = TableFunctionSchema.LeadingArgument)]
        public static IQueryable<IDynamicRecordRow> Values(string structure, string tuples) => throw new NotSupportedException();
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
            .Select(p => new { p.Item1.Value, p.Item2.String }));

        sql.Should().Be("select t1.value, t2.somestring as \"String\" from all_rows() as \"t1\" join complex_entity as \"t2\" on t1.id = t2.id");
    }

    [Fact]
    public void TableFunction_AsJoinedSource_ShouldAliasBothSources()
    {
        using var ctx = PostgresTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, simple
            .Join(ctx.FromTableFunction(() => Tvf.AllRows()), (s, r) => r.Id == s.Id)
            .Select(p => new { p.Item1.Id, p.Item2.Value }));

        sql.Should().Be("select t1.id, t2.value from simple_entity as \"t1\" join all_rows() as \"t2\" on t2.id = cast(t1.id as bigint)");
    }

    [Fact]
    public void BooleanAggregates_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.bool_and(x.Boolean),
            B = SqlFunctions.Postgres.bool_or(x.Boolean),
            C = SqlFunctions.Postgres.every(x.Boolean)
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
            A = SqlFunctions.Postgres.bit_and(x.Id),
            B = SqlFunctions.Postgres.bit_or(x.Id),
            C = SqlFunctions.Postgres.bit_xor(x.Id)
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
            A = SqlFunctions.Sql.corr(x.Id, x.Id),
            B = SqlFunctions.Sql.covar_pop(x.Id, x.Id),
            C = SqlFunctions.Sql.covar_samp(x.Id, x.Id),
            D = SqlFunctions.Postgres.regr_slope(x.Id, x.Id),
            E = SqlFunctions.Postgres.regr_intercept(x.Id, x.Id),
            F = SqlFunctions.Postgres.regr_r2(x.Id, x.Id),
            G = SqlFunctions.Postgres.regr_count(x.Id, x.Id),
            H = SqlFunctions.Postgres.regr_avgx(x.Id, x.Id),
            I = SqlFunctions.Postgres.regr_avgy(x.Id, x.Id)
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
            A = SqlFunctions.Postgres.percentile_cont(0.5, () => x.Id),
            B = SqlFunctions.Postgres.percentile_disc(0.5, () => x.Id),
            C = SqlFunctions.Postgres.mode(() => x.String)
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
            A = SqlFunctions.Postgres.asin(x.Int),
            B = SqlFunctions.Postgres.atan2(x.Int, x.Int),
            C = SqlFunctions.Postgres.cbrt(x.Int),
            D = SqlFunctions.Postgres.sinh(x.Int),
            E = SqlFunctions.Postgres.degrees(x.Int),
            F = SqlFunctions.Postgres.pi(),
            G = SqlFunctions.Postgres.random(),
            I = SqlFunctions.Postgres.gcd(x.Id, 2L),
            J = SqlFunctions.Postgres.lcm(x.Id, 2L),
            K = SqlFunctions.Postgres.factorial(x.Id),
            L = SqlFunctions.Postgres.width_bucket(x.Int, 0.0, 10.0, 5)
        }));

        sql.Should().Contain("asin(");
        sql.Should().Contain("atan2(");
        sql.Should().Contain("cbrt(");
        sql.Should().Contain("sinh(");
        sql.Should().Contain("degrees(");
        sql.Should().Contain("pi()");
        sql.Should().Contain("random()");
        sql.Should().Contain("gcd(id, 2)");
        sql.Should().Contain("lcm(id, 2)");
        sql.Should().Contain("factorial(id)");
        sql.Should().Contain("width_bucket(");
    }

    [Fact]
    public void SetSeed_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { S = SqlFunctions.Postgres.setseed(0.5) }))
            .Should().Contain("setseed(0.5)");
    }

    [Fact]
    public void CryptoHash_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.digest("abc", "sha256"),
            B = SqlFunctions.Postgres.digest(SqlFunctions.Parameter<byte[]>(0), "sha1"),
            C = SqlFunctions.Postgres.sha256(SqlFunctions.Parameter<byte[]>(1))
        }));

        sql.Should().Contain("digest('abc', 'sha256')");
        sql.Should().Contain("digest(@norm_p0, 'sha1')");
        sql.Should().Contain("sha256(@norm_p1)");
    }

    [Fact]
    public void ExtendedLogFunction_ShouldEmitTwoArgumentLog()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.log(2.0, x.Int) }))
            .Should().Contain("log(2, cast(nullableint as double precision))");
    }

    [Fact]
    public void ExtendedStringFunctions_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.split_part(x.String, ",", 1),
            B = SqlFunctions.Postgres.strpos(x.String, "a"),
            C = SqlFunctions.Postgres.left(x.String, 2),
            D = SqlFunctions.Postgres.right(x.String, 2),
            E = SqlFunctions.Postgres.lpad(x.String, 5, "0"),
            F = SqlFunctions.Postgres.rpad(x.String, 5, "0"),
            G = SqlFunctions.Postgres.repeat(x.String, 3),
            H = SqlFunctions.Postgres.reverse(x.String),
            I = SqlFunctions.Postgres.initcap(x.String),
            J = SqlFunctions.Postgres.translate(x.String, "a", "b"),
            K = SqlFunctions.Postgres.overlay(x.String, "XX", 2, 2),
            L = SqlFunctions.Postgres.concat_ws(",", x.String, x.Id),
            M = SqlFunctions.Postgres.format("%s", x.String),
            N = SqlFunctions.Postgres.md5(x.String)
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
            .Where(x => SqlFunctions.Postgres.regexp_split_to_array(x.String, ",") != null)
            .Select(x => new
            {
                A = SqlFunctions.Postgres.regexp_replace(x.String, "a", "b"),
                B = SqlFunctions.Postgres.regexp_like(x.String, "^a"),
                D = SqlFunctions.Postgres.regexp_count(x.String, "a"),
                E = SqlFunctions.Postgres.regexp_instr(x.String, "a")
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
            .Where(x => SqlFunctions.Postgres.justify_days(SqlFunctions.Parameter<TimeSpan>(0)) != null)
            .Where(x => SqlFunctions.Postgres.current_time() != null)
            .Where(x => SqlFunctions.Postgres.localtime() != null)
            .Select(x => new
            {
                D = SqlFunctions.Postgres.to_char(x.Datetime, "YYYY"),
                E = SqlFunctions.Postgres.to_date("2020-01-01", "YYYY-MM-DD"),
                F = SqlFunctions.Postgres.to_number("1", "999"),
                G = SqlFunctions.Postgres.to_timestamp(0.0),
                H = SqlFunctions.Postgres.timezone("UTC", x.Datetime),
                J = SqlFunctions.Postgres.current_date(),
                M = SqlFunctions.Postgres.localtimestamp()
            }));

        sql.Should().Contain("justify_days(@norm_p0)");
        sql.Should().Contain("to_char(dt, 'YYYY')");
        sql.Should().Contain("to_date('2020-01-01', 'YYYY-MM-DD')");
        sql.Should().Contain("to_number('1', '999')");
        sql.Should().Contain("to_timestamp(0)");
        sql.Should().Contain("timezone('UTC', dt)");
        sql.Should().Contain("current_date");
        sql.Should().Contain("current_time");
        sql.Should().Contain("localtime");
        sql.Should().Contain("localtimestamp");
    }

    [Fact]
    public void MakeIntervalAndJustify_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.Postgres.make_interval(0, 1, 2, 3, 4, 5.0) != null)
            .Where(x => SqlFunctions.Postgres.justify_hours(SqlFunctions.Parameter<TimeSpan>(1)) != null)
            .Select(x => new { x.Id }));

        sql.Should().Contain("make_interval(0, 1, 0, 2, 3, 4, 5)");
        sql.Should().Contain("justify_hours(@norm_p1)");
    }

    [Fact]
    public void Sha224_384_512_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.sha224(SqlFunctions.Parameter<byte[]>(0)),
            B = SqlFunctions.Postgres.sha384(SqlFunctions.Parameter<byte[]>(1)),
            C = SqlFunctions.Postgres.sha512(SqlFunctions.Parameter<byte[]>(2))
        }));

        sql.Should().Contain("sha224(@norm_p0)");
        sql.Should().Contain("sha384(@norm_p1)");
        sql.Should().Contain("sha512(@norm_p2)");
    }

    [Fact]
    public void RegexpSubstr_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.regexp_substr(x.String, "\\d+"),
            B = SqlFunctions.Postgres.regexp_substr(x.String, "\\d+", "i")
        }));

        sql.Should().Contain("regexp_substr(somestring, '\\d+')");
        sql.Should().Contain("regexp_substr(somestring, '\\d+', 'i')");
    }

    [Fact]
    public void MakeTimeAndTimestamp_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            T = SqlFunctions.Postgres.make_time(12, 30, 15.0),
            S = SqlFunctions.Postgres.make_timestamp(2020, 1, 2, 3, 4, 5.0)
        }));

        sql.Should().Contain("make_time(12, 30, 15)");
        sql.Should().Contain("make_timestamp(2020, 1, 2, 3, 4, 5)");
    }

    [Fact]
    public void AgeAndDateBin_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.age(x.Datetime, x.Datetime),
            B = SqlFunctions.Postgres.date_bin("1 hour", x.Datetime, x.Datetime)
        }));

        sql.Should().Contain("age(dt, dt)");
        sql.Should().Contain("date_bin(cast('1 hour' as interval), dt, dt)");
    }

    [Fact]
    public void CurrentSettingAndSetConfig_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.current_setting("app.name"),
            B = SqlFunctions.Postgres.current_setting("app.name", true),
            C = SqlFunctions.Postgres.set_config("app.name", "1", false)
        }));

        sql.Should().Contain("current_setting('app.name')");
        sql.Should().Contain("current_setting('app.name', true)");
        sql.Should().Contain("set_config('app.name', '1', false)");
    }

    [Fact]
    public void SequenceFunctions_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.nextval("my_seq"),
            B = SqlFunctions.Postgres.setval("my_seq", 5L),
            C = SqlFunctions.Postgres.currval("my_seq"),
            D = SqlFunctions.Postgres.lastval()
        }));

        sql.Should().Contain("nextval(cast('my_seq' as regclass))");
        sql.Should().Contain("setval(cast('my_seq' as regclass), 5)");
        sql.Should().Contain("currval(cast('my_seq' as regclass))");
        sql.Should().Contain("lastval()");
    }

    [Fact]
    public void SqlJsonArrayConstructors_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.json_array(1, true, "x"),
            B = SqlFunctions.Postgres.jsonb_array(1, 2)
        }));

        sql.Should().Contain("json_array(1, true, 'x')");
        sql.Should().Contain("json_array(1, 2 returning jsonb)");
    }

    [Fact]
    public void SqlJsonQueryFunctions_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.json_value(SqlFunctions.Parameter<JsonDocument>(0), "$.a"),
            B = SqlFunctions.Postgres.json_query(SqlFunctions.Parameter<JsonDocument>(1), "$.b"),
            C = SqlFunctions.Postgres.json_exists(SqlFunctions.Parameter<JsonDocument>(2), "$.c", true)
        }));

        sql.Should().Contain("json_value(@norm_p0, cast('$.a' as jsonpath))");
        sql.Should().Contain("json_query(@norm_p1, cast('$.b' as jsonpath))");
        sql.Should().Contain("json_exists(@norm_p2, cast('$.c' as jsonpath))");
    }

    [Fact]
    public void NumNullsFunctions_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.num_nulls(x.Int, x.String),
            B = SqlFunctions.Postgres.num_nonnulls(x.Int)
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
            .Where(x => SqlFunctions.Postgres.array_append(SqlFunctions.Parameter<long[]>(0), x.Id) != null)
            .Where(x => SqlFunctions.Postgres.array_prepend(x.Id, SqlFunctions.Parameter<long[]>(1)) != null)
            .Where(x => SqlFunctions.Postgres.array_cat(SqlFunctions.Parameter<long[]>(2), SqlFunctions.Parameter<long[]>(3)) != null)
            .Where(x => SqlFunctions.Postgres.array_remove(SqlFunctions.Parameter<long[]>(4), x.Id) != null)
            .Where(x => SqlFunctions.Postgres.array_replace(SqlFunctions.Parameter<long[]>(5), x.Id, x.Id) != null)
            .Where(x => SqlFunctions.Postgres.array_fill(x.Id, 2, 3) != null)
            .Where(x => SqlFunctions.Postgres.array_positions(SqlFunctions.Parameter<long[]>(7), x.Id) != null)
            .Where(x => SqlFunctions.Postgres.array_reverse(SqlFunctions.Parameter<long[]>(8)) != null)
            .Where(x => SqlFunctions.Postgres.array_sort(SqlFunctions.Parameter<long[]>(9)) != null)
            .Where(x => SqlFunctions.Postgres.string_to_array(x.String, ",") != null)
            .Select(x => new
            {
                G = SqlFunctions.Postgres.array_dims(SqlFunctions.Parameter<long[]>(6))
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
    public void ArrayShuffleSample_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.Postgres.array_shuffle(SqlFunctions.Parameter<long[]>(0)) != null)
            .Where(x => SqlFunctions.Postgres.array_sample(SqlFunctions.Parameter<long[]>(1), 3) != null)
            .Select(x => new { x.Id }));

        sql.Should().Contain("array_shuffle(@norm_p0)");
        sql.Should().Contain("array_sample(@norm_p1, 3)");
    }

    [Fact]
    public void StringJoinAndSplit_ShouldUseArrayToStringAndStringToArray()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = string.Join(",", x.String!.Split(',')) }))
            .Should().Contain("array_to_string(string_to_array(somestring, ','), ',')");

        // A Split result is an array operand for the other array functions too.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.array_position(x.String!.Split(','), "a") }))
            .Should().Contain("array_position(string_to_array(somestring, ','), 'a')");
    }

    [Fact]
    public void ArrayOperators_ShouldEmit()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.Postgres.array_contained_by(SqlFunctions.Parameter<long[]>(0), SqlFunctions.Parameter<long[]>(1)))
            .Where(x => SqlFunctions.Postgres.array_concat(SqlFunctions.Parameter<long[]>(2), SqlFunctions.Parameter<long[]>(3)) != null)
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
            A = SqlFunctions.Postgres.jsonb_set(SqlFunctions.Parameter<JsonDocument>(0), new[] { "a" }, 1),
            B = SqlFunctions.Postgres.jsonb_insert(SqlFunctions.Parameter<JsonDocument>(1), new[] { "a" }, 1, true),
            C = SqlFunctions.Postgres.jsonb_strip_nulls(SqlFunctions.Parameter<JsonDocument>(2)),
            D = SqlFunctions.Postgres.jsonb_pretty(SqlFunctions.Parameter<JsonDocument>(3)),
            E = SqlFunctions.Postgres.jsonb_delete(SqlFunctions.Parameter<JsonDocument>(4), "a"),
            F = SqlFunctions.Postgres.jsonb_delete(SqlFunctions.Parameter<JsonDocument>(5), 0),
            G = SqlFunctions.Postgres.row_to_json(SqlFunctions.Parameter<object>(6)),
            H = SqlFunctions.Postgres.array_to_json(SqlFunctions.Parameter<long[]>(7)),
            I = SqlFunctions.Postgres.json_concat(SqlFunctions.Parameter<JsonDocument>(8), SqlFunctions.Parameter<JsonDocument>(9))
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
            A = SqlFunctions.Postgres.jsonb_path_exists(SqlFunctions.Parameter<JsonDocument>(0), "$.a"),
            B = SqlFunctions.Postgres.jsonb_path_match(SqlFunctions.Parameter<JsonDocument>(1), "$.a == 1"),
            C = SqlFunctions.Postgres.jsonb_path_query_first(SqlFunctions.Parameter<JsonDocument>(2), "$.a"),
            D = SqlFunctions.Postgres.jsonb_path_query_array(SqlFunctions.Parameter<JsonDocument>(3), "$.a")
        }));

        sql.Should().Contain("jsonb_path_exists(@norm_p0, cast('$.a' as jsonpath))");
        sql.Should().Contain("jsonb_path_match(@norm_p1, cast('$.a == 1' as jsonpath))");
        sql.Should().Contain("jsonb_path_query_first(@norm_p2, cast('$.a' as jsonpath))");
        sql.Should().Contain("jsonb_path_query_array(@norm_p3, cast('$.a' as jsonpath))");
    }

    [Fact]
    public void FullTextPredicates_ShouldUseTsvectorMatch()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.Sql.contains(x.String, "foo")).Select(x => new { x.Id }))
            .Should().Contain("where to_tsvector(somestring) @@ plainto_tsquery('foo')");

        SqlOf(ctx, e.Where(x => SqlFunctions.Sql.freetext(x.String, "foo")).Select(x => new { x.Id }))
            .Should().Contain("where to_tsvector(somestring) @@ websearch_to_tsquery('foo')");
    }

    [Fact]
    public void IndexOfLastIndexOf_ShouldUseStrpos()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.IndexOf("b") }))
            .Should().Contain("case when (strpos(somestring, 'b')) = 0 then -1 else (strpos(somestring, 'b')) - 1 end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.IndexOf("b", 1) }))
            .Should().Contain("strpos(substring(somestring, 1 + 1, length(somestring) - (1)), 'b')");
        SqlOf(ctx, e.Select(x => new { V = x.String!.LastIndexOf("b") }))
            .Should().Contain("case when (strpos(reverse(somestring), reverse('b'))) = 0 then -1 else length(somestring) - (strpos(reverse(somestring), reverse('b'))) - (length('b')) + 1 end");
    }

    [Fact]
    public void PadLeftRight_ShouldUseRepeat()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.PadLeft(5, '0') }))
            .Should().Contain("case when length(somestring) >= (5) then somestring else repeat('0', (5) - length(somestring))||somestring end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.PadRight(5) }))
            .Should().Contain("case when length(somestring) >= (5) then somestring else somestring||repeat(' ', (5) - length(somestring)) end");
    }

    [Fact]
    public void RemoveInsertAndNewString_ShouldUseOverlayAndRepeat()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Remove(2) }))
            .Should().Contain("substring(somestring, 0 + 1, 2)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.Remove(2, 1) }))
            .Should().Contain("overlay(somestring placing '' from 2 + 1 for 1)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.Insert(2, "x") }))
            .Should().Contain("overlay(somestring placing 'x' from 2 + 1 for 0)");
        SqlOf(ctx, e.Select(x => new { V = new string('*', 4) }))
            .Should().Contain("repeat('*', 4)");
    }

    [Fact]
    public void CorrelatedScalarInSelect_ShouldReferenceOuterAlias()
    {
        using var ctx = PostgresTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            sid = inner.Where(s => s.Id == it.Id).Select(s => s.Id).First()
        }));

        sql.Should().Contain("= t1.id");
        sql.Should().Contain("from complex_entity as \"t1\"");
    }

    /// <summary>
    /// An aggregate terminal inside a correlated subquery is rewritten to the equivalent aggregate
    /// projection instead of being rejected.
    /// </summary>
    [Fact]
    public void CorrelatedAggregateTerminalInSelect_ShouldRenderAggregateSubquery()
    {
        using var ctx = PostgresTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            c = inner.Where(s => s.Id == it.Id).Count()
        }));

        sql.Should().Contain("(select count(*) from simple_entity");
        sql.Should().Contain("= t1.id");
    }

    [Fact]
    public void CorrelatedStatisticalAggregateTerminalInSelect_ShouldRenderAggregateSubquery()
    {
        using var ctx = PostgresTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            sd = inner.Where(s => s.Id == it.Id).Stdev(s => s.Id),
            vr = inner.Where(s => s.Id == it.Id).Var(s => s.Id)
        }));

        sql.Should().Contain("stddev(");
        sql.Should().Contain("variance(");
    }

    [Fact]
    public void ClickHouseArrayFunctions_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var arrayFunction = () => SqlOf(ctx, e.Select(x => new { N = SqlFunctions.ClickHouse.length(x.Tags) }));
        arrayFunction.Should().Throw<NotSupportedException>().WithMessage("*array functions*");

        var arrayJoin = () => SqlOf(ctx, e.Select(x => new { Tag = SqlFunctions.ClickHouse.array_join(x.Tags) }));
        arrayJoin.Should().Throw<NotSupportedException>().WithMessage("*arrayJoin*");
    }

    [Fact]
    public void ClickHouseArrayScalarFunctions_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var range = () => SqlOf(ctx, e.Select(x => new { N = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.range(1, 5)) }));
        range.Should().Throw<NotSupportedException>().WithMessage("*array functions*");

        var slice = () => SqlOf(ctx, e.Select(x => new { S = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_slice(x.Tags, 1), ",") }));
        slice.Should().Throw<NotSupportedException>().WithMessage("*array functions*");

        var push = () => SqlOf(ctx, e.Select(x => new { S = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_push_back(x.Tags, "z"), ",") }));
        push.Should().Throw<NotSupportedException>().WithMessage("*array functions*");
    }

    [Fact]
    public void ClickHouseSequenceAggregates_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var funnel = () => SqlOf(ctx, e.Select(x => new { F = SqlFunctions.ClickHouse.window_funnel(10, x.Id, x.Id >= 1, x.Id >= 5) }));
        funnel.Should().Throw<NotSupportedException>().WithMessage("*windowFunnel*");

        var match = () => SqlOf(ctx, e.Select(x => new { M = SqlFunctions.ClickHouse.sequence_match("(?1)(?2)", x.Id, x.Id >= 1, x.Id >= 5) }));
        match.Should().Throw<NotSupportedException>().WithMessage("*windowFunnel*");

        var retention = () => SqlOf(ctx, e.Select(x => new { M = SqlFunctions.ClickHouse.retention(x.Id >= 1, x.Id >= 5) }));
        retention.Should().Throw<NotSupportedException>().WithMessage("*windowFunnel*");
    }

    [Fact]
    public void ClickHouseMultiIf_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new
        {
            B = SqlFunctions.ClickHouse.multi_if(
                SqlFunctions.ClickHouse.when(x.Id == 1, "one"),
                SqlFunctions.ClickHouse.otherwise("many"))
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*multiIf*");
    }

    [Fact]
    public void ClickHouseInFrameWindowFunctions_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var lag = () => SqlOf(ctx, e.Select(x => new
        {
            P = SqlFunctions.ClickHouse.lag_in_frame(x.Id, 1).Over(SqlFunctions.Sql.asc(() => x.Id))
        }));
        lag.Should().Throw<NotSupportedException>().WithMessage("*lagInFrame*");

        var lead = () => SqlOf(ctx, e.Select(x => new
        {
            N = SqlFunctions.ClickHouse.lead_in_frame(x.Id, 1).Over(SqlFunctions.Sql.asc(() => x.Id))
        }));
        lead.Should().Throw<NotSupportedException>().WithMessage("*leadInFrame*");
    }

    [Fact]
    public void ArrayJoinClause_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => SqlOf(ctx, e.ArrayJoin(x => x.Tags).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*ARRAY JOIN*");
    }

    [Fact]
    public void ArrayJoinElement_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => SqlOf(ctx, e.ArrayJoinElement(x => x.Tags).Select(p => new { p.Item1.Id, Tag = p.Element }));

        act.Should().Throw<NotSupportedException>().WithMessage("*ARRAY JOIN*");
    }

    [Fact]
    public void DistinctOn_ShouldEmitDistinctOnPrefix()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.DistinctOn(x => x.Id).Select(x => new { x.Id }))
            .Should().Be("select distinct on (id) id from simple_entity");
    }

    [Fact]
    public void DistinctOn_WithMultipleKeys_ShouldEmitAllColumns()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.DistinctOn(x => new { x.Int, x.String }).Select(x => new { x.Id }))
            .Should().StartWith("select distinct on (nullableint, somestring) id from complex_entity");
    }

    [Fact]
    public void DistinctOn_WithOrderBy_ShouldKeepBoth()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.DistinctOn(x => x.Id).OrderBy(x => x.Id).Select(x => new { x.Id }));

        sql.Should().StartWith("select distinct on (id) id from simple_entity");
        sql.Should().Contain("order by");
    }

    [Fact]
    public void DistinctOn_CombinedWithDistinct_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => e.Distinct().DistinctOn(x => x.Id);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TableSample_ShouldEmitSystemSample()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>(o => o.TableSample(10));

        SqlOf(ctx, e.Select(x => x.Id))
            .Should().Be("select id from simple_entity tablesample system (10)");
    }

    [Fact]
    public void TableSample_WithSeed_ShouldEmitRepeatable()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>(o => o.TableSample(10, TableSampleMethod.System, 42));

        SqlOf(ctx, e.Select(x => x.Id))
            .Should().EndWith("tablesample system (10) repeatable (42)");
    }

    [Fact]
    public void TableSample_Bernoulli_ShouldEmitBernoulliMethod()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>(o => o.TableSample(5, TableSampleMethod.Bernoulli));

        SqlOf(ctx, e.Select(x => x.Id))
            .Should().EndWith("tablesample bernoulli (5)");
    }

    [Fact]
    public void TableSample_WithInvalidPercent_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var act = () => ctx.From<ISimpleEntity>(o => o.TableSample(0));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WithTies_ShouldUseFetchFirstWithTies()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Limit(5).WithTies().OrderBy(x => x.Id).Select(x => x.Id))
            .Should().EndWith("fetch first 5 rows with ties");
    }

    [Fact]
    public void WithTies_WithOffset_ShouldEmitOffsetBeforeFetch()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Page(5, 3).WithTies().OrderBy(x => x.Id).Select(x => x.Id))
            .Should().EndWith("offset 3 fetch first 5 rows with ties");
    }

    [Fact]
    public void WithTies_WithoutLimit_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.WithTies().Select(x => x.Id));

        act.Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void ForUpdate_ShouldEmitForUpdate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForUpdate().Select(x => x.Id)).Should().EndWith("for update");
    }

    [Fact]
    public void ForShare_ShouldEmitForShare()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForShare().Select(x => x.Id)).Should().EndWith("for share");
    }

    [Fact]
    public void ForUpdate_NoWait_ShouldEmitNowait()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForUpdate(LockWaitMode.NoWait).Select(x => x.Id)).Should().EndWith("for update nowait");
    }

    [Fact]
    public void ForShare_SkipLocked_ShouldEmitSkipLocked()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForShare(LockWaitMode.SkipLocked).Select(x => x.Id)).Should().EndWith("for share skip locked");
    }

    [Fact]
    public void KeywordCase_Upper_ShouldUppercaseDialectClauses()
    {
        using var ctx = PostgresTestContext.CreateUppercase();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.DistinctOn(x => x.Int).Select(x => new { x.Int }))
            .Should().Contain("SELECT DISTINCT ON (nullableint)");

        SqlOf(ctx, ctx.From<IComplexEntity>(o => o.TableSample(10, TableSampleMethod.System, 3)).Select(x => x.Int))
            .Should().Contain(" TABLESAMPLE system (10) REPEATABLE (3)");

        SqlOf(ctx, e.ForUpdate().Select(x => x.Int)).Should().EndWith("FOR UPDATE");
        SqlOf(ctx, e.ForShare().Select(x => x.Int)).Should().EndWith("FOR SHARE");
        SqlOf(ctx, e.ForUpdate(LockWaitMode.SkipLocked).Select(x => x.Int)).Should().EndWith("FOR UPDATE SKIP LOCKED");
    }

    [Fact]
    public void ForUpdate_WithPaging_ShouldFollowPaging()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Limit(5).ForUpdate().Select(x => x.Id))
            .Should().EndWith("limit 5\n for update");
    }

    [Fact]
    public void TextSearchRank_ShouldRenderTsRankOverTsvectorQuery()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Rank = SqlFunctions.Postgres.ts_rank(
                SqlFunctions.Postgres.to_tsvector(x.String!),
                SqlFunctions.Postgres.to_tsquery("cat"))
        }));

        sql.Should().Contain("ts_rank(to_tsvector(somestring), to_tsquery('cat'))");
    }

    [Fact]
    public void TextSearchRankCd_ShouldRenderTsRankCd()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Rank = SqlFunctions.Postgres.ts_rank_cd(
                SqlFunctions.Postgres.to_tsvector(x.String!),
                SqlFunctions.Postgres.to_tsquery("cat"))
        }));

        sql.Should().Contain("ts_rank_cd(to_tsvector(somestring), to_tsquery('cat'))");
    }

    [Fact]
    public void TextSearchMatch_ShouldRenderAtAtOperator()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.Postgres.ts_match(
                SqlFunctions.Postgres.to_tsvector(x.String!),
                SqlFunctions.Postgres.websearch_to_tsquery("cat & dog")))
            .Select(x => new { x.Id }));

        sql.Should().Contain("(to_tsvector(somestring) @@ websearch_to_tsquery('cat & dog'))");
    }

    [Fact]
    public void TextSearchQueryVariants_ShouldUseTheirNativeNames()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            P = SqlFunctions.Postgres.plainto_tsquery("a b"),
            H = SqlFunctions.Postgres.phraseto_tsquery("a b"),
            T = SqlFunctions.Postgres.to_tsquery("a & b")
        }));

        sql.Should().Contain("plainto_tsquery('a b')");
        sql.Should().Contain("phraseto_tsquery('a b')");
        sql.Should().Contain("to_tsquery('a & b')");
    }

    [Fact]
    public void TextSearchHeadline_ShouldRenderTsHeadline()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Head = SqlFunctions.Postgres.ts_headline(x.String!, SqlFunctions.Postgres.to_tsquery("cat"))
        }));

        sql.Should().Contain("ts_headline(somestring, to_tsquery('cat'))");
    }

    [Fact]
    public void XmlMethods_ShouldThrowBecauseOnlySqlServerHasThem()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.xml_query(x.String, "/root") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*XML data-type methods*");
    }

    [Fact]
    public void XmlNodes_ShouldThrowBecauseOnlySqlServerHasThem()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.String, "/root/item"))
            .Select(p => new { p.Item2.Value }));

        act.Should().Throw<NotSupportedException>().WithMessage("*xml.nodes*");
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
        using var ctx = PostgresTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Where(c => c.Id > 0)
            .Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id, p.Item1.String }));

        sql.Should().Be("select t1.id, t2.id as \"SId\", t1.\"String\" from (select id, somestring as \"String\" from complex_entity\n where (id > 0)) as \"t1\" join simple_entity as \"t2\" on t1.id = cast(t2.id as bigint)");
    }

    [Fact]
    public void DerivedSourceWithJoinThenJoin_ShouldResolveTheOuterAlias()
    {
        using var ctx = PostgresTestContext.Create();
        var derived = ctx.From<ISimpleEntity>()
            .Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
            .Select(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<IComplexEntity>(), (d, c2) => d.OrderId == c2.Id)
            .Select(p => new { p.Item1.OrderId, p.Item1.CustomerName, Third = p.Item2.Id }));

        sql.Should().Be("select t3.\"OrderId\", t3.\"CustomerName\", t4.id as \"Third\" from (select t1.id as \"OrderId\", t2.somestring as \"CustomerName\" from simple_entity as \"t1\" join complex_entity as \"t2\" on cast(t1.id as bigint) = t2.id) as \"t3\" join complex_entity as \"t4\" on cast(t3.\"OrderId\" as bigint) = t4.id");
    }

    [Fact]
    public void DerivedSource_JoinOnFilteredPrimary_ShouldReferenceExposedColumnName()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Where(c => c.Int > 0)
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Int == (int?)s.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id }));

        sql.Should().Contain("nullableint as \"Int\"");
        sql.Should().Contain("on t1.\"Int\" = ");
        sql.Should().NotContain("t1.nullableint");
    }

    [Fact]
    public void DerivedSourceAndPhysicalSourceOfSameType_ShouldResolveTheirOwnColumns()
    {
        using var ctx = PostgresTestContext.Create();

        var derived = ctx.From<IComplexEntity>().Where(c => c.Id > 1).ToCommand();

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<IComplexEntity>(), (d, c) => d.Int == c.Int)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id }));

        sql.Should().Contain("nullableint as \"Int\"");
        sql.Should().Contain("on t1.\"Int\" = t2.nullableint");
    }

    [Fact]
    public void ProjectedCommand_OrderByDescendingAndPage_ShouldResolveProjectionExpression()
    {
        using var ctx = PostgresTestContext.Create();

        var grouped = ctx.From<IComplexEntity>()
            .GroupBy(x => x.Int)
            .Select(x => new { x.Int, Cnt = SqlFunctions.Sql.count() });

        var sql = SqlOf(ctx, grouped.OrderByDescending(x => x.Cnt).Page(20, 1));

        sql.Should().Contain("group by nullableint");
        sql.Should().Contain("order by count(*) desc");
        sql.Should().Contain("limit 20 offset 1");
    }

    [Fact]
    public void ProjectedCommand_OrderByAndLimit_ShouldResolveSourceColumn()
    {
        using var ctx = PostgresTestContext.Create();

        var projected = ctx.From<IComplexEntity>().Select(x => new { x.Id, Name = x.String });

        var sql = SqlOf(ctx, projected.OrderBy(x => x.Name).Limit(5).Offset(2));

        sql.Should().Contain("order by somestring");
        sql.Should().Contain("limit 5 offset 2");
    }

    [Fact]
    public void DerivedSourceWhereThenJoin_ShouldPushTheFilterOntoTheProjection()
    {
        using var ctx = PostgresTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Where(d => d.Id > 5)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id }));

        sql.Should().Be("select t1.id, t2.id as \"SId\" from (select id, somestring as \"String\" from complex_entity) as \"t1\" join simple_entity as \"t2\" on t1.id = cast(t2.id as bigint)\n where (t1.id > 5)");
    }

    [Fact]
    public void DerivedSourceOverDerivedWithWindow_ShouldResolvePassThroughColumns()
    {
        using var ctx = PostgresTestContext.Create();

        // The middle derived query projects pass-through columns of another derived query plus a
        // window function. The outer query must resolve the pass-through columns against the source
        // the middle query was built from, not lose them to the source-scope boundary.
        var revenue = ctx.From<IComplexEntity>()
            .GroupBy(x => new { DepartureCity = x.String, Route = x.String + " -> " + x.String })
            .Select(x => new
            {
                DepartureCity = x.String,
                Route = x.String + " -> " + x.String,
                Revenue = SqlFunctions.Sql.sum(x.Id)
            });

        var ranked = ctx.From(revenue)
            .Select(r => new
            {
                r.DepartureCity,
                r.Route,
                r.Revenue,
                Place = SqlFunctions.Sql.rank().Over(
                    partitionBy: new Expression<Func<object?>>[] { () => r.DepartureCity },
                    orderBy: new[] { SqlFunctions.Sql.desc(() => r.Revenue) })
            });

        var sql = SqlOf(ctx, ctx.From(ranked)
            .Where(r => r.Place <= 3)
            .OrderBy(r => r.DepartureCity)
            .Select(r => new { r.DepartureCity, r.Place, r.Route, r.Revenue }));

        sql.Should().NotContain("select ,");
        sql.Should().Contain("select \"DepartureCity\", \"Place\", \"Route\", \"Revenue\"");
        sql.Should().Contain("order by t2.\"DepartureCity\"");
    }

    [Fact]
    public void FromSql_ShouldRenderDerivedTableWithNamedParameters()
    {
        using var ctx = PostgresTestContext.Create();
        var min = 1;

        var command = Prepare(ctx, ctx
            .FromSql("select id from complex_entity where id > @min", new { min })
            .Select(t => new { Id = t["id"].AsInt }));

        var sql = Normalize(command.DbCommand.CommandText);
        sql.Should().Be("select t1.id from (select id from complex_entity where id > @min) as \"t1\"");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName).Should().Equal("min");
    }

    [Fact]
    public void FromSql_AsJoinedSource_ShouldRenderDerivedTableAndResolveColumns()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<ISimpleEntity>()
            .Join(ctx.FromSql("select id from complex_entity"), (s, r) => s.Id == r["id"].AsInt)
            .Select(p => new { p.Item1.Id, R = p.Item2["id"].AsInt }));

        sql.Should().Contain("join (select id from complex_entity) as \"t2\"");
        sql.Should().Contain("on t1.id = t2.id");
    }

    [Fact]
    public void FromSql_CollidingParameterName_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var min = 1;

        var act = () => Prepare(ctx, ctx
            .FromSql("select id from complex_entity where id > @min", new { min })
            .Where(t => t["id"].AsInt > min)
            .Select(t => new { Id = t["id"].AsInt }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*two parameters named 'min'*");
    }

    [Fact]
    public void RangeOverlaps_ColumnAndCapturedRange_ShouldEmitAndOperator()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IRangeEntity>();
        var window = new Range<int>(1, 10);

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Postgres.overlaps(x.During, window)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("(during && @");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.Value.Should().BeOfType<NpgsqlRange<int>>();
    }

    [Fact]
    public void RangeContains_ValueAndRangeAndContainedBy_ShouldEmitContainmentOperators()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IRangeEntity>();
        var other = new Range<int>(5, 20);

        var command = Prepare(ctx, e
            .Where(x => SqlFunctions.Postgres.range_contains(x.During, 3))
            .Where(x => SqlFunctions.Postgres.range_contains(x.During, other))
            .Where(x => SqlFunctions.Postgres.range_contained_by(x.During, other))
            .Select(x => new { x.Id }));

        var sql = Normalize(command.DbCommand.CommandText);
        sql.Should().Contain("(during @> 3)");
        sql.Should().Contain("(during @> @");
        sql.Should().Contain("(during <@ @");
    }

    [Fact]
    public void RangeOperators_ShouldEmitNativeOperators()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IRangeEntity>();
        var a = new Range<int>(1, 10);
        var b = new Range<int>(5, 20);

        var command = Prepare(ctx, e.Select(x => new
        {
            U = SqlFunctions.Postgres.range_union(a, b),
            I = SqlFunctions.Postgres.range_intersection(a, b),
            D = SqlFunctions.Postgres.range_difference(a, b),
            Adj = SqlFunctions.Postgres.range_adjacent(a, b),
            L = SqlFunctions.Postgres.range_strictly_left_of(a, b),
            R = SqlFunctions.Postgres.range_strictly_right_of(a, b),
            Nr = SqlFunctions.Postgres.range_not_extend_right_of(a, b),
            Nl = SqlFunctions.Postgres.range_not_extend_left_of(a, b)
        }));

        var sql = Normalize(command.DbCommand.CommandText);
        sql.Should().Contain("+ @");
        sql.Should().Contain("* @");
        sql.Should().Contain(" - @");
        sql.Should().Contain("-|- @");
        sql.Should().Contain("<< @");
        sql.Should().Contain(">> @");
        sql.Should().Contain("&< @");
        sql.Should().Contain("&> @");
    }

    [Fact]
    public void RangeConstructors_ShouldEmitNativeCalls()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IRangeEntity>();

        SqlOf(ctx, e.Select(x => new { R = SqlFunctions.Postgres.int4range(1, 10) }))
            .Should().Contain("int4range(1, 10)");
        SqlOf(ctx, e.Select(x => new { R = SqlFunctions.Postgres.int4range(1, 10, "[]") }))
            .Should().Contain("int4range(1, 10, '[]')");
        SqlOf(ctx, e.Select(x => new { R = SqlFunctions.Postgres.int4range(null, null) }))
            .Should().Contain("int4range(null, null)");
        SqlOf(ctx, e.Select(x => new { R = SqlFunctions.Postgres.empty_range<int>() }))
            .Should().Contain("'empty'::int4range");
    }

    [Fact]
    public void RangeFunctions_ShouldEmitNativeCalls()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IRangeEntity>();
        var window = new Range<int>(1, 10);

        var command = Prepare(ctx, e.Select(x => new
        {
            E = SqlFunctions.Postgres.isempty(x.During),
            L = SqlFunctions.Postgres.lower(window),
            U = SqlFunctions.Postgres.upper(window),
            Li = SqlFunctions.Postgres.lower_inc(window),
            Ui = SqlFunctions.Postgres.upper_inc(window),
            Lf = SqlFunctions.Postgres.lower_inf(window),
            Uf = SqlFunctions.Postgres.upper_inf(window)
        }));

        var sql = Normalize(command.DbCommand.CommandText);
        sql.Should().Contain("isempty(during)");
        sql.Should().Contain("lower(@");
        sql.Should().Contain("upper(@");
        sql.Should().Contain("lower_inc(@");
        sql.Should().Contain("upper_inc(@");
        sql.Should().Contain("lower_inf(@");
        sql.Should().Contain("upper_inf(@");
    }
}
