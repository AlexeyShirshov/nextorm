using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Verifies the SQLite specific SQL dialect. These tests never open a database connection
/// (the in-memory connection string is never used), so they run on every build/CI.
/// Behavioural tests against a real database live in NextORM.Integration.Tests.
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
    public void QueryTag_ShouldRenderBlockCommentAfterSelect()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithTag("app.list").Select(x => new { x.Id }))
            .Should().Be("select /* app.list */ id from simple_entity");
    }

    [Fact]
    public void QueryTag_ShouldNeutraliseCommentDelimitersAndNewlines()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id }).WithTag("a*/b/*c\r\nd"))
            .Should().Be("select /* a* /b/ *c d */ id from simple_entity");
    }

    [Fact]
    public void Pivot_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e
            .Pivot(PivotAggregate.Count, s => s.Id, s => s.Id, PivotValue.Create("1"))
            .Select(t => new { V = t.GetNullableInt32("1") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*PIVOT*");
    }

    [Fact]
    public void JoinHint_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>()
            .Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
            .WithJoinHint("hash")
            .Select(p => new { p.Item1.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Join hints*");
    }

    [Fact]
    public void SubQueryHint_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<ISimpleEntity>().Select(x => new { x.Id });

        var act = () => SqlOf(ctx, ctx.From(inner).WithSubQueryHint("SeqScan(t1)").Select(t => new { t.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Subquery hints*");
    }

    [Fact]
    public void TablesInScopeHint_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>().WithTablesInScopeHint("nolock").Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Tables-in-scope hints*");
    }

    [Fact]
    public void SelectDistinct_ShouldEmitDistinct()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Distinct().Select(x => new { x.Id })).Should().Be("select distinct id from simple_entity");
    }

    [Fact]
    public void QuotedIdentifiers_ContextFlag_ShouldQuoteTableAndColumn()
    {
        using var ctx = SqliteTestContext.CreateQuoted();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select \"id\" from \"simple_entity\"");
    }

    [Fact]
    public void QuotedIdentifiers_CommandOverride_ShouldEnable()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id }).WithQuotedIdentifiers()).Should().Be("select \"id\" from \"simple_entity\"");
    }

    [Fact]
    public void QuotedIdentifiers_CommandOverride_ShouldDisable()
    {
        using var ctx = SqliteTestContext.CreateQuoted();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id }).WithQuotedIdentifiers(false)).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void QuotedIdentifiers_BuilderOverride_ShouldApplyToWhereAndAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithQuotedIdentifiers().Where(x => x.Id > 1).Select(x => new { x.Id }))
            .Should().Be("select \"id\" from \"simple_entity\"\n where (\"id\" > 1)");
    }

    [Fact]
    public void QuotedIdentifiers_KeywordPhysicalNames_ShouldNotNeedManualQuoting()
    {
        using var ctx = SqliteTestContext.CreateQuoted();
        var e = ctx.From<IKeywordEntity>();

        SqlOf(ctx, e.Select(x => new { x.Value })).Should().Be("select \"select\" as 'Value' from \"order\"");
    }

    [Fact]
    public void KeywordCase_CommandOverride_ShouldPreserveCteSource()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();
        var cte = e.Where(x => x.Id > 1).Select(x => new { x.Id });

        var cmd = ctx.With("recent", cte).From("recent").Select(t => new { id = t["id"].AsInt });
        var sql = SqlOf(ctx, cmd.WithKeywordCase());

        sql.Should().StartWith("WITH ");
        sql.Should().Contain("FROM recent");
    }

    [Fact]
    public void IndexHint_WithIndex_ShouldEmitIndexedBy()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithIndex("idx_id").Select(x => new { x.Id }))
            .Should().Be("select id from simple_entity indexed by idx_id");
    }

    [Fact]
    public void IndexHint_WithoutIndex_ShouldEmitNotIndexed()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithoutIndex().Select(x => new { x.Id }))
            .Should().Be("select id from simple_entity not indexed");
    }

    [Fact]
    public void IndexHint_ShouldRespectKeywordCase()
    {
        using var ctx = SqliteTestContext.CreateUppercase();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithIndex("idx_id").Select(x => new { x.Id }))
            .Should().Contain("INDEXED BY idx_id");
    }

    [Fact]
    public void KeywordCase_Upper_ShouldUppercaseCoreKeywords()
    {
        using var ctx = SqliteTestContext.CreateUppercase();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Where(x => x.Id > 1).Select(x => new { x.Id }))
            .Should().Be("SELECT id FROM simple_entity\n WHERE (id > 1)");
    }

    [Fact]
    public void KeywordCase_Upper_ShouldUppercaseLogicalAndConditional()
    {
        using var ctx = SqliteTestContext.CreateUppercase();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id > 1 && x.Id < 5)
            .Select(x => x.Id == 1 ? 10 : 20));

        sql.Should().Contain("WHERE ((id > 1) AND (id < 5))");
        sql.Should().Contain("CASE WHEN");
        sql.Should().Contain(" THEN ");
        sql.Should().Contain(" ELSE ");
        sql.Should().Contain(" END");
    }

    [Fact]
    public void KeywordCase_Upper_ShouldNotTouchIdentifiersLiteralsOrFunctions()
    {
        using var ctx = SqliteTestContext.CreateUppercaseQuoted();
        var e = ctx.From<IKeywordEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Value == 1).Select(x => new { x.Value }));

        sql.Should().Contain("\"select\"");
        sql.Should().Contain("\"order\"");
        sql.Should().NotContain("\"SELECT\"");
        sql.Should().NotContain("\"ORDER\"");
    }

    [Fact]
    public void KeywordCase_Upper_ShouldNotRewriteStringLiteral()
    {
        using var ctx = SqliteTestContext.CreateUppercase();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "select from where").Select(x => new { x.Id }));

        sql.Should().Contain("'select from where'");
    }

    [Fact]
    public void KeywordCase_Lower_IsDefaultAndCommandOverride_ShouldWin()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var lower = SqlOf(ctx, e.Select(x => new { x.Id }));
        lower.Should().StartWith("select ");

        var upper = SqlOf(ctx, e.WithKeywordCase().Select(x => new { x.Id }));
        upper.Should().StartWith("SELECT ");
        upper.Should().NotBe(lower);
    }

    [Fact]
    public void KeywordCase_Upper_ShouldUppercaseAliasesAndPaging()
    {
        using var ctx = SqliteTestContext.CreateUppercase();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Limit(3).Offset(2).Select(x => new { X = x.Id }));

        sql.Should().Contain("SELECT id AS 'X'");
        sql.Should().Contain("LIMIT 3");
        sql.Should().Contain("OFFSET 2");
    }

    [Fact]
    public void QuotedIdentifiers_Join_ShouldQuoteTablesAndColumns()
    {
        using var ctx = SqliteTestContext.CreateQuoted();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain("from \"simple_entity\"");
        sql.Should().Contain("join \"complex_entity\"");
        sql.Should().Contain("t1.\"id\"");
    }

    [Fact]
    public void QuotedIdentifiers_Cte_ShouldQuoteDeclarationAndReference()
    {
        using var ctx = SqliteTestContext.CreateQuoted();
        var e = ctx.From<IComplexEntity>();

        var cte = e.Where(x => x.Id > 1).Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.With("recent", cte).From("recent").Select(t => new { id = t["id"].AsInt }));

        sql.Should().StartWith("with \"recent\" as (select \"id\" from \"complex_entity\"");
        sql.Should().EndWith("select \"id\" from \"recent\"");
    }

    [Fact]
    public void QuotedIdentifiers_DerivedTable_ShouldQuoteAliasedColumns()
    {
        using var ctx = SqliteTestContext.CreateQuoted();
        var e = ctx.From<ISimpleEntity>();

        var inner = e.Where(x => x.Id > 1).Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.From(inner).Select(t => new { t.Id }));

        sql.Should().Contain("from (select \"id\" from \"simple_entity\"");
    }

    [Fact]
    public void QuotedIdentifiers_SchemaQualifiedTable_ShouldQuoteEachPart()
    {
        using var ctx = SqliteTestContext.CreateQuoted();

        SqlOf(ctx, ctx.From("main.simple_entity").Select(t => new { id = t["id"].AsInt }))
            .Should().Be("select \"id\" from \"main\".\"simple_entity\"");
    }

    [Fact]
    public void UnattributedPoco_ShouldAutoMapByName()
    {
        using var ctx = SqliteTestContext.Create();

        SqlOf(ctx, ctx.From<BareEntity>().Select(x => new { x.Id, x.Name }))
            .Should().Be("select Id, Name from BareEntity");
    }

    [Fact]
    public void UnattributedInterface_ShouldKeepTypeNameAndProject()
    {
        using var ctx = SqliteTestContext.Create();

        SqlOf(ctx, ctx.From<IBareEntity>().Where(x => x.Id > 1).Select(x => x.Name))
            .Should().Be("select Name from IBareEntity\n where (Id > 1)");
    }

    [Fact]
    public void UnattributedPoco_GetOnlyProperty_ShouldNotBeMapped()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.From<BareEntity>();

        var metadata = DataContextCache.Metadata[typeof(BareEntity)];
        metadata.TableName.Should().Be("BareEntity");
        metadata.Properties.Select(p => p.ColumnName).Should().BeEquivalentTo(["Id", "Name", "Stock"]);
    }

    [Fact]
    public void UnattributedPoco_PrivateSetter_ShouldBeMapped()
    {
        using var ctx = SqliteTestContext.Create();

        SqlOf(ctx, ctx.From<BareEntity>().Select(x => new { x.Stock }))
            .Should().Be("select Stock from BareEntity");
    }

    [Fact]
    public void NamingConvention_ShouldSnakeCaseAutoNames()
    {
        using var ctx = SqliteTestContext.CreateSnakeCase();

        SqlOf(ctx, ctx.From<BareEntity>().Select(x => new { x.Id, x.Name }))
            .Should().Be("select id, name from bare_entity");
    }

    [Fact]
    public void NamingConvention_Interface_ShouldDropLeadingIPrefix()
    {
        using var ctx = SqliteTestContext.CreateSnakeCase();

        SqlOf(ctx, ctx.From<IBareEntity>().Where(x => x.Id > 1).Select(x => x.Name))
            .Should().Be("select name from bare_entity\n where (id > 1)");
    }

    [Fact]
    public void NamingConvention_ShouldNotTranslateExplicitNames()
    {
        using var ctx = SqliteTestContext.CreateSnakeCase();

        SqlOf(ctx, ctx.From<ExplicitlyMappedEntity>().Select(x => new { x.Value, x.FirstName }))
            .Should().Be("select ExplicitColumn as 'Value', first_name as 'FirstName' from ExplicitTable");
    }

    [Fact]
    public void NamingConvention_QueryOverride_ShouldApply()
    {
        using var ctx = SqliteTestContext.Create();

        SqlOf(ctx, ctx.From<BareEntity>().WithNamingConvention(SnakeCaseNamingConvention.Instance).Select(x => new { x.Id, x.Name }))
            .Should().Be("select id, name from bare_entity");
    }

    [Fact]
    public void NamingConvention_WithQuotedIdentifiers_ShouldQuoteTranslatedNames()
    {
        using var ctx = SqliteTestContext.CreateSnakeCaseQuoted();

        SqlOf(ctx, ctx.From<BareEntity>().Select(x => new { x.Id, x.Name }))
            .Should().Be("select \"id\", \"name\" from \"bare_entity\"");
    }

    [Fact]
    public void NamingConvention_PlanCache_ShouldNotLeakBetweenContextDefaults()
    {
        using var plain = SqliteTestContext.Create();
        plain.PurgeQueryCache();
        var plainSql = Normalize(((DbPreparedQueryCommand<int>)plain.GetPreparedQueryCommand(
            plain.From<BareEntity>().Select(x => x.Id), false, true, CancellationToken.None)).DbCommand.CommandText);
        plainSql.Should().Be("select Id from BareEntity");

        using var snake = SqliteTestContext.CreateSnakeCase();
        var snakeSql = Normalize(((DbPreparedQueryCommand<int>)snake.GetPreparedQueryCommand(
            snake.From<BareEntity>().Select(x => x.Id), false, true, CancellationToken.None)).DbCommand.CommandText);
        snakeSql.Should().Be("select id from bare_entity");
    }

    [Fact]
    public void NamingConvention_ShouldApplyToWhereAndGroupBy()
    {
        using var ctx = SqliteTestContext.CreateSnakeCase();

        var sql = SqlOf(ctx, ctx.From<BareEntity>()
            .Where(x => x.Stock > 1)
            .GroupBy(x => x.Name)
            .Select(x => new { x.Name, count = SqlFunctions.Sql.count() }));

        sql.Should().Contain("from bare_entity");
        sql.Should().Contain("where (stock > 1)");
        sql.Should().Contain("group by name");
    }

    [Fact]
    public void NamingConvention_ShouldApplyToJoinedTablesButNotExplicitOnes()
    {
        using var ctx = SqliteTestContext.CreateSnakeCase();

        var sql = SqlOf(ctx, ctx.From<BareEntity>()
            .Join(ctx.From<ExplicitlyMappedEntity>(), (a, b) => a.Id == b.Value)
            .Select(p => new { p.Item1.Id, p.Item2.Value }));

        sql.Should().Contain("from bare_entity");
        sql.Should().Contain("join ExplicitTable");
    }

    [Fact]
    public void QuotedIdentifiers_PlanCache_ShouldNotLeakBetweenContextDefaults()
    {
        using var plain = SqliteTestContext.Create();
        plain.PurgeQueryCache();
        var plainSql = Normalize(((DbPreparedQueryCommand<int>)plain.GetPreparedQueryCommand(
            plain.From<ISimpleEntity>().Select(x => x.Id), false, true, CancellationToken.None)).DbCommand.CommandText);
        plainSql.Should().Be("select id from simple_entity");

        using var quoted = SqliteTestContext.CreateQuoted();
        var quotedSql = Normalize(((DbPreparedQueryCommand<int>)quoted.GetPreparedQueryCommand(
            quoted.From<ISimpleEntity>().Select(x => x.Id), false, true, CancellationToken.None)).DbCommand.CommandText);
        quotedSql.Should().Be("select \"id\" from \"simple_entity\"");
    }

    [Fact]
    public void SelectDistinctWithUnionAll_ShouldKeepDistinctInLeftBranch()
    {
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Union(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n union \nselect id from simple_entity");
    }

    [Fact]
    public void UnionAll_ShouldEmitUnionAll()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).UnionAll(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n union all \nselect id from simple_entity");
    }

    [Fact]
    public void Intersect_ShouldEmitIntersect()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Intersect(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n intersect \nselect id from simple_entity");
    }

    [Fact]
    public void Except_ShouldEmitExcept()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => x.Id).Except(e.Select(x => x.Id)))
            .Should().Be("select id from simple_entity\n except \nselect id from simple_entity");
    }

    [Fact]
    public void IntersectAll_ShouldThrowBecauseSqliteHasNoIntersectAll()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => x.Id).IntersectAll(e.Select(x => x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("*IntersectAll*");
    }

    [Fact]
    public void ExceptAll_ShouldThrowBecauseSqliteHasNoExceptAll()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => x.Id).ExceptAll(e.Select(x => x.Id)));

        act.Should().Throw<NotSupportedException>().WithMessage("*ExceptAll*");
    }

    [Fact]
    public void SelectBasic_ShouldProducePlainSelect()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void ColumnByName_ShouldRenderColumnIdentifierAndRenameAlias()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .Select(x => new { Region = SqlFunctions.Column<int>(x, "region_id") }));

        sql.Should().Be("select region_id as 'Region' from simple_entity");
    }

    [Fact]
    public void ColumnByName_ScalarProjection_ShouldNotAlias()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>().Select(x => SqlFunctions.Column<int>(x, "id")));

        sql.Should().Be("select id from simple_entity");
    }

    [Fact]
    public void Parameter_ShouldUseDollarPrefix()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == SqlFunctions.Parameter<int>(0)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = $norm_p0");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void Paging_WithLimitAndOffset_ShouldUseLimitOffset()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Page(5, 10).Select(x => x.Id)).Should().EndWith("limit 5 offset 10");
    }

    [Fact]
    public void Paging_WithOffsetOnly_ShouldEmitLimitMinusOne()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        // SQLite has no OFFSET without LIMIT, so the provider emits "limit -1".
        SqlOf(ctx, e.Offset(10).Select(x => x.Id)).Should().EndWith("limit -1 offset 10");
    }

    [Fact]
    public void BooleanLiteral_ShouldUseOne()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.Boolean == true).Select(x => x.Boolean)).Should().Contain("b = 1");
    }

    [Fact]
    public void StringConcat_ShouldUseDoublePipe()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String + "/" + x.String }));

        sql.Should().Contain("||");
        sql.Should().Contain("as 'V'");
    }

    [Fact]
    public void Coalesce_ShouldUseIfNullFunction()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String ?? "" }));

        sql.Should().Contain("ifnull(");
        sql.Should().NotContain("coalesce(");
    }

    [Fact]
    public void Count_ShouldUseCountStar()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.count())).Should().Contain("count(*)");
    }

    [Fact]
    public void CountBig_ShouldUseCountStar()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // SQLite's count() already yields a 64-bit integer, so the big request needs no special syntax.
        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.count_big())).Should().Contain("count(*)");
    }

    [Fact]
    public void GroupBy_ScalarKey_ShouldMatchAnonymousKey()
    {
        using var ctx = SqliteTestContext.Create();
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
    public void Aggregate_ShouldKeepProviderSpecificName()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.Sql.stdev((double)x.Id)));

        // SQLite relies on its own registered functions, so no name remapping happens.
        sql.Should().Contain("stdev(");
        sql.Should().NotContain("stddev(");
    }

    [Fact]
    public void ComputedColumn_ShouldBeAliasedWithSingleQuotes()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { x.Id, Calc = x.Id + 1 }));

        sql.Should().Contain("as 'Calc'");
        sql.Should().NotContain("as \"");
    }

    [Fact]
    public void RenamedColumn_ShouldBeAliased()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // The underlying column is "somestring"; the projection calls it "String", so the SQL must
        // expose it under the projected name for outer queries to find it.
        var sql = SqlOf(ctx, e.Select(x => new { x.Id, Renamed = x.String }));

        sql.Should().Contain("as 'Renamed'");
    }

    [Fact]
    public void NestedCalculatedColumn_ShouldReferenceInnerAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var nested = e.Select(x => new { x.Id, Calc = x.String + x.String });
        var sql = SqlOf(ctx, ctx.From(nested).Select(t => new { t.Id, t.Calc }));

        sql.Should().Contain("as 'Calc'");
        sql.Should().Contain("Calc");
    }

    [Fact]
    public void Subquery_ShouldAlwaysAliasForStableAliasIdentity()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var nested = e.Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.From(nested).Select(t => new { t.Id }));

        // SQLite does not require a derived table alias, but nextorm always emits one so every
        // reference to the derived source resolves through the same alias identity.
        sql.Should().Be("select id from (select id from complex_entity) as 't1'");
    }

    [Fact]
    public void Join_ShouldQuoteTableAliases()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.Join(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain("as 't1'");
        sql.Should().Contain("as 't2'");
    }

    [Fact]
    public void LeftJoin_ShouldEmitLeftJoinWithOn()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" left join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void RightJoin_ShouldEmitRightJoinWithOn()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.RightJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" right join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void FullJoin_ShouldEmitFullJoinWithOn()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.FullJoin(complex, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" full join complex_entity");
        sql.Should().Contain(" on ");
    }

    [Fact]
    public void CrossJoin_ShouldEmitCrossJoinWithoutOn()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.CrossJoin(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" cross join complex_entity");
        sql.Should().NotContain(" on ");
    }

    [Fact]
    public void CrossApply_ShouldThrowBecauseSqliteHasNoLateral()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple.CrossApply(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        act.Should().Throw<NotSupportedException>().WithMessage("*CrossApply*");
    }

    [Fact]
    public void OuterApply_ShouldThrowBecauseSqliteHasNoLateral()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple.OuterApply(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        act.Should().Throw<NotSupportedException>().WithMessage("*OuterApply*");
    }

    [Fact]
    public void CrossApply_ToCorrelatedSubquery_ShouldThrowBecauseSqliteHasNoLateral()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>()
            .CrossApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id).Select(c => new { c.Id, c.String }))
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        act.Should().Throw<NotSupportedException>().WithMessage("*CrossApply*");
    }

    [Fact]
    public void QueryHint_ShouldThrowBecauseSqliteHasNoQueryHints()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { x.Id }).Hint("recompile"));

        act.Should().Throw<NotSupportedException>().WithMessage("*Query hints*");
    }

    [Fact]
    public void SubqueryAliasIsNotRequired()
    {
        using var ctx = SqliteTestContext.CreateSqlite();

        ctx.Dialect.RequireSubqueryAlias.Should().BeFalse();
    }

    [Fact]
    public void Escape_ShouldUseSingleQuotes()
    {
        using var ctx = SqliteTestContext.CreateSqlite();

        ctx.Dialect.Escape("Some Alias").Should().Be("'Some Alias'");
    }

    [Fact]
    public void Join4Tables_ShouldEmitFourAliasesAndThreeJoins()
    {
        using var ctx = SqliteTestContext.Create();
        var simple1 = ctx.From<ISimpleEntity>();
        var complex1 = ctx.From<IComplexEntity>();
        var simple2 = ctx.From<ISimpleEntity>();
        var complex2 = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple1
            .Join(complex1, (s, c) => s.Id == c.Id)
            .Join(simple2, (p, s) => p.Item2.Id == s.Id)
            .Join(complex2, (p, c) => p.Item3.Id == c.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id }));

        sql.Should().Contain("as 't1'");
        sql.Should().Contain("as 't2'");
        sql.Should().Contain("as 't3'");
        sql.Should().Contain("as 't4'");
        // Each chained join condition must reference the accumulated projection via the right alias.
        // simple_entity.Id is int while complex_entity.Id is long, hence the widening cast.
        sql.Should().Contain("on cast(t1.id as bigint) = t2.id");
        sql.Should().Contain("on t2.id = cast(t3.id as bigint)");
        sql.Should().Contain("on cast(t3.id as bigint) = t4.id");
        // Three joins => four "join" segments when split on the keyword.
        sql.Split("join").Should().HaveCount(4);
    }

    /// <summary>
    /// Guards the cached projection-alias resolution: a projection whose repeated entity types appear
    /// in a different pattern than <see cref="Join4Tables_ShouldEmitFourAliasesAndThreeJoins"/> must
    /// still map each <c>tN</c> to the right table, and repeated builds must stay stable.
    /// </summary>
    [Fact]
    public void Join4Tables_RepeatedTypesInDifferentPattern_ResolvePerShapeAndStayStable()
    {
        using var ctx = SqliteTestContext.Create();
        var simple1 = ctx.From<ISimpleEntity>();
        var simple2 = ctx.From<ISimpleEntity>();
        var complex1 = ctx.From<IComplexEntity>();
        var complex2 = ctx.From<IComplexEntity>();

        string Build() => SqlOf(ctx, simple1
            .Join(simple2, (a, b) => a.Id == b.Id)
            .Join(complex1, (p, c) => p.Item2.Id == c.Id)
            .Join(complex2, (p, c) => p.Item3.Id == c.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id }));

        var first = Build();
        var second = Build();

        second.Should().Be(first);
        second.Should().Contain("on t1.id = t2.id");
        second.Should().Contain("on cast(t2.id as bigint) = t3.id");
        second.Should().Contain("on t3.id = t4.id");
    }

    [Fact]
    public void Join8Tables_ShouldEmitEightAliasesAndSevenJoins()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = new[]
        {
            ctx.From<ISimpleEntity>(),
            ctx.From<ISimpleEntity>(),
            ctx.From<ISimpleEntity>(),
            ctx.From<ISimpleEntity>(),
        };
        var complex = new[]
        {
            ctx.From<IComplexEntity>(),
            ctx.From<IComplexEntity>(),
            ctx.From<IComplexEntity>(),
            ctx.From<IComplexEntity>(),
        };

        var sql = SqlOf(ctx, simple[0]
            .Join(complex[0], (s, c) => s.Id == c.Id)
            .Join(simple[1], (p, s) => p.Item2.Id == s.Id)
            .Join(complex[1], (p, c) => p.Item3.Id == c.Id)
            .Join(simple[2], (p, s) => p.Item4.Id == s.Id)
            .Join(complex[2], (p, c) => p.Item5.Id == c.Id)
            .Join(simple[3], (p, s) => p.Item6.Id == s.Id)
            .Join(complex[3], (p, c) => p.Item7.Id == c.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id, E = p.Item5.Id, F = p.Item6.Id, G = p.Item7.Id, H = p.Item8.Id }));

        for (var i = 1; i <= 8; i++)
            sql.Should().Contain($"as 't{i}'");

        // Each chained join condition must reference the accumulated projection via the right alias.
        // The chain alternates simple_entity (int) and complex_entity (long): the int side is cast.
        sql.Should().Contain("on cast(t1.id as bigint) = t2.id");
        sql.Should().Contain("on t2.id = cast(t3.id as bigint)");
        sql.Should().Contain("on cast(t3.id as bigint) = t4.id");
        sql.Should().Contain("on t4.id = cast(t5.id as bigint)");
        sql.Should().Contain("on cast(t5.id as bigint) = t6.id");
        sql.Should().Contain("on t6.id = cast(t7.id as bigint)");
        sql.Should().Contain("on cast(t7.id as bigint) = t8.id");

        // Seven joins => eight "join" segments when split on the keyword.
        sql.Split("join").Should().HaveCount(8);
    }

    [Fact]
    public void Join5Tables_ShouldEmitFiveAliasesAndFourJoins()
    {
        using var ctx = SqliteTestContext.Create();
        var e = Enumerable.Range(0, 5).Select(_ => ctx.From<ISimpleEntity>()).ToArray();

        var sql = SqlOf(ctx, e[0]
            .Join(e[1], (a, b) => a.Id == b.Id)
            .Join(e[2], (p, c) => p.Item2.Id == c.Id)
            .Join(e[3], (p, c) => p.Item3.Id == c.Id)
            .Join(e[4], (p, c) => p.Item4.Id == c.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id, E = p.Item5.Id }));

        for (var i = 1; i <= 5; i++)
            sql.Should().Contain($"as 't{i}'");

        // Four joins => five "join" segments when split on the keyword.
        sql.Split("join").Should().HaveCount(5);
    }

    [Fact]
    public void Join6Tables_ShouldEmitSixAliasesAndFiveJoins()
    {
        using var ctx = SqliteTestContext.Create();
        var e = Enumerable.Range(0, 6).Select(_ => ctx.From<ISimpleEntity>()).ToArray();

        var sql = SqlOf(ctx, e[0]
            .Join(e[1], (a, b) => a.Id == b.Id)
            .Join(e[2], (p, c) => p.Item2.Id == c.Id)
            .Join(e[3], (p, c) => p.Item3.Id == c.Id)
            .Join(e[4], (p, c) => p.Item4.Id == c.Id)
            .Join(e[5], (p, c) => p.Item5.Id == c.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id, E = p.Item5.Id, F = p.Item6.Id }));

        for (var i = 1; i <= 6; i++)
            sql.Should().Contain($"as 't{i}'");

        sql.Split("join").Should().HaveCount(6);
    }

    [Fact]
    public void Join7Tables_ShouldEmitSevenAliasesAndSixJoins()
    {
        using var ctx = SqliteTestContext.Create();
        var e = Enumerable.Range(0, 7).Select(_ => ctx.From<ISimpleEntity>()).ToArray();

        var sql = SqlOf(ctx, e[0]
            .Join(e[1], (a, b) => a.Id == b.Id)
            .Join(e[2], (p, c) => p.Item2.Id == c.Id)
            .Join(e[3], (p, c) => p.Item3.Id == c.Id)
            .Join(e[4], (p, c) => p.Item4.Id == c.Id)
            .Join(e[5], (p, c) => p.Item5.Id == c.Id)
            .Join(e[6], (p, c) => p.Item6.Id == c.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id, E = p.Item5.Id, F = p.Item6.Id, G = p.Item7.Id }));

        for (var i = 1; i <= 7; i++)
            sql.Should().Contain($"as 't{i}'");

        sql.Split("join").Should().HaveCount(7);
    }

    [Fact]
    public void LeftJoin8Tables_ShouldEmitLeftJoinAtEveryArity()
    {
        using var ctx = SqliteTestContext.Create();
        var e = Enumerable.Range(0, 8).Select(_ => ctx.From<ISimpleEntity>()).ToArray();

        var sql = SqlOf(ctx, e[0]
            .LeftJoin(e[1], (a, b) => a.Id == b.Id)
            .LeftJoin(e[2], (p, c) => p.Item2.Id == c.Id)
            .LeftJoin(e[3], (p, c) => p.Item3.Id == c.Id)
            .LeftJoin(e[4], (p, c) => p.Item4.Id == c.Id)
            .LeftJoin(e[5], (p, c) => p.Item5.Id == c.Id)
            .LeftJoin(e[6], (p, c) => p.Item6.Id == c.Id)
            .LeftJoin(e[7], (p, c) => p.Item7.Id == c.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id, E = p.Item5.Id, F = p.Item6.Id, G = p.Item7.Id, H = p.Item8.Id }));

        for (var i = 1; i <= 8; i++)
            sql.Should().Contain($"as 't{i}'");

        sql.Split("left join").Should().HaveCount(8);
    }

    [Fact]
    public void RightJoin8Tables_ShouldEmitRightJoinAtEveryArity()
    {
        using var ctx = SqliteTestContext.Create();
        var e = Enumerable.Range(0, 8).Select(_ => ctx.From<ISimpleEntity>()).ToArray();

        var sql = SqlOf(ctx, e[0]
            .RightJoin(e[1], (a, b) => a.Id == b.Id)
            .RightJoin(e[2], (p, c) => p.Item2.Id == c.Id)
            .RightJoin(e[3], (p, c) => p.Item3.Id == c.Id)
            .RightJoin(e[4], (p, c) => p.Item4.Id == c.Id)
            .RightJoin(e[5], (p, c) => p.Item5.Id == c.Id)
            .RightJoin(e[6], (p, c) => p.Item6.Id == c.Id)
            .RightJoin(e[7], (p, c) => p.Item7.Id == c.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id, E = p.Item5.Id, F = p.Item6.Id, G = p.Item7.Id, H = p.Item8.Id }));

        for (var i = 1; i <= 8; i++)
            sql.Should().Contain($"as 't{i}'");

        sql.Split("right join").Should().HaveCount(8);
    }

    [Fact]
    public void FullJoin8Tables_ShouldEmitFullJoinAtEveryArity()
    {
        using var ctx = SqliteTestContext.Create();
        var e = Enumerable.Range(0, 8).Select(_ => ctx.From<ISimpleEntity>()).ToArray();

        var sql = SqlOf(ctx, e[0]
            .FullJoin(e[1], (a, b) => a.Id == b.Id)
            .FullJoin(e[2], (p, c) => p.Item2.Id == c.Id)
            .FullJoin(e[3], (p, c) => p.Item3.Id == c.Id)
            .FullJoin(e[4], (p, c) => p.Item4.Id == c.Id)
            .FullJoin(e[5], (p, c) => p.Item5.Id == c.Id)
            .FullJoin(e[6], (p, c) => p.Item6.Id == c.Id)
            .FullJoin(e[7], (p, c) => p.Item7.Id == c.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id, E = p.Item5.Id, F = p.Item6.Id, G = p.Item7.Id, H = p.Item8.Id }));

        for (var i = 1; i <= 8; i++)
            sql.Should().Contain($"as 't{i}'");

        sql.Split("full join").Should().HaveCount(8);
    }

    [Fact]
    public void CrossJoin8Tables_ShouldEmitCrossJoinAtEveryArity()
    {
        using var ctx = SqliteTestContext.Create();
        var e = Enumerable.Range(0, 8).Select(_ => ctx.From<ISimpleEntity>()).ToArray();

        var sql = SqlOf(ctx, e[0]
            .CrossJoin(e[1])
            .CrossJoin(e[2])
            .CrossJoin(e[3])
            .CrossJoin(e[4])
            .CrossJoin(e[5])
            .CrossJoin(e[6])
            .CrossJoin(e[7])
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id, E = p.Item5.Id, F = p.Item6.Id, G = p.Item7.Id, H = p.Item8.Id }));

        for (var i = 1; i <= 8; i++)
            sql.Should().Contain($"as 't{i}'");

        sql.Split("cross join").Should().HaveCount(8);
        sql.Should().NotContain(" on ");
    }

    [Fact]
    public void Conditional_ShouldEmitCaseWhen()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.Id > 1 ? "big" : "small" }));

        sql.Should().Contain("case when (id > 1) then 'big' else 'small' end");
        sql.Should().Contain("as 'V'");
    }

    [Fact]
    public void NestedConditional_ShouldEmitNestedCaseWhen()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.Boolean == true ? (x.Int == null ? "a" : "b") : "c" }));

        sql.Should().Contain("case when b = 1 then case when nullableint is null then 'a' else 'b' end else 'c' end");
    }

    [Fact]
    public void Conditional_InWhere_ShouldEmitCaseWhen()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => (x.Int == null ? 0 : x.Int) == 1).Select(x => new { x.Id }));

        sql.Should().Contain("case when nullableint is null then 0 else nullableint end");
    }

    [Fact]
    public void ConditionalBoolean_ShouldEmitBooleanLiterals()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // SQLite has a boolean type usable as a value and as a predicate, so the CASE stays ANSI.
        var sql = SqlOf(ctx, e.Select(x => new { V = x.Id > 1 ? true : false }));

        sql.Should().Contain("case when (id > 1) then 1 else 0 end");
        sql.Should().NotContain("cast(");
    }

    [Fact]
    public void Conditional_WithCapturedValue_ShouldEmitParameter()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var threshold = 1L;

        var command = Prepare(ctx, e.Select(x => new { V = x.Id > threshold ? "big" : "small" }));

        // The parameter-extraction pass has to walk the CASE test even though it emits no SQL.
        Normalize(command.DbCommand.CommandText).Should().Contain("$threshold");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("threshold");
    }

    [Fact]
    public void Switch_ShouldEmitSearchedCase()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(SwitchOfId("other", (1L, "one"), (2L, "two"))));

        sql.Should().Contain("case when id = 1 then 'one' when id = 2 then 'two' else 'other' end");
    }

    [Fact]
    public void Switch_WithComparisonMethod_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var comparison = typeof(string).GetMethod(nameof(string.Equals), [typeof(string), typeof(string)])!;
        var p = Expression.Parameter(typeof(IComplexEntity), "x");
        var body = Expression.Switch(
            Expression.Property(p, nameof(IComplexEntity.String)),
            Expression.Constant("other"),
            comparison,
            Expression.SwitchCase(Expression.Constant("a"), Expression.Constant("x")));
        var exp = Expression.Lambda<Func<IComplexEntity, string>>(body, p);

        var act = () => SqlOf(ctx, e.Select(exp));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void StringToUpper_ShouldUseUpperFunction()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.ToUpper() })).Should().Contain("upper(somestring)");
    }

    [Fact]
    public void StringToLower_ShouldUseLowerFunction()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.ToLower() })).Should().Contain("lower(somestring)");
    }

    [Fact]
    public void SqlFunction_OverColumn_ShouldEmitMappedFunction()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Udf.ToUpper(x.String!) }))
            .Should().Be("select upper(somestring) as 'V' from complex_entity");
    }

    [Fact]
    public void SqlFunction_WithCapturedArgument_ShouldEmitParameter()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var start = 1;

        var command = Prepare(ctx, e.Select(x => new { V = Udf.Slice(x.String!, start) }));

        Normalize(command.DbCommand.CommandText).Should().Be("select substr(somestring, $start) as 'V' from complex_entity");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("start");
    }

    [Fact]
    public void SqlFunction_WithSchema_ShouldQualifyName()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Udf.WithSchema(x.Id) }))
            .Should().Be("select dbo.my_fn(id) as 'V' from complex_entity");
    }

    [Fact]
    public void SqlFunction_OnDeclaringType_ShouldUseMethodName()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = ImplicitUdf.Lower(x.String!) }))
            .Should().Be("select Lower(somestring) as 'V' from complex_entity");
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

    [SqlFunction]
    private static class ImplicitUdf
    {
        public static string Lower(string value) => throw new NotSupportedException();
    }

    [Fact]
    public void Contains_ShouldUseLikeWithWildcards()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Contains("df")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%df%'");
    }

    [Fact]
    public void Contains_WithCapturedValue_ShouldConcatParameterIntoPattern()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var needle = "df";

        // A runtime value cannot be escaped at translation time, so the wildcards are concatenated
        // around the parameter. The parameter-extraction pass must still collect it.
        var command = Prepare(ctx, e.Where(x => x.String!.Contains(needle)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("somestring like '%'||$needle||'%'");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("needle");
    }

    [Fact]
    public void StartsWith_ShouldUseLikePrefixPattern()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.StartsWith("xx")).Select(x => new { x.Id }))
            .Should().Contain("somestring like 'xx%'");
    }

    [Fact]
    public void EndsWith_ShouldUseLikeSuffixPattern()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.EndsWith("sd")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%sd'");
    }

    [Fact]
    public void Contains_WithWildcard_ShouldEscapeAndEmitEscapeClause()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // %, _ and the escape character itself are escaped so the literal is matched verbatim.
        SqlOf(ctx, e.Where(x => x.String!.Contains("a%b_c")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%a\\%b\\_c%' escape '\\'");
    }

    [Fact]
    public void Substring_ShouldUseOneBasedOffset()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Substring(1, 2) }))
            .Should().Contain("substring(somestring, 1 + 1, 2)");
    }

    [Fact]
    public void Substring_WithoutLength_ShouldDeriveRemainingLength()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Substring(1) }))
            .Should().Contain("substring(somestring, 1 + 1, length(somestring) - (1))");
    }

    [Fact]
    public void StringLength_ShouldUseLengthFunction()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Length })).Should().Contain("length(somestring)");
    }

    [Fact]
    public void Trim_ShouldUseTrimFunctions()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Trim() })).Should().Contain("trim(somestring)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.TrimStart() })).Should().Contain("ltrim(somestring)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.TrimEnd() })).Should().Contain("rtrim(somestring)");
    }

    [Fact]
    public void Replace_ShouldUseReplaceFunction()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Replace("a", "b") }))
            .Should().Contain("replace(somestring, 'a', 'b')");
    }

    [Fact]
    public void IndexOf_ShouldUseInstr()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.IndexOf("b") }))
            .Should().Contain("case when (instr(somestring, 'b')) = 0 then -1 else (instr(somestring, 'b')) - 1 end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.IndexOf("b", 1) }))
            .Should().Contain("instr(substring(somestring, 1 + 1, length(somestring) - (1)), 'b')");
    }

    [Fact]
    public void LastIndexOf_ShouldNotBeSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = x.String!.LastIndexOf("b") })))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void StringJoinAndSplit_ShouldNotBeSupportedWithoutArrays()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = string.Join(",", x.String!.Split(',')) })))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void PadLeftRight_ShouldUseZeroblobRepeat()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.PadLeft(5) }))
            .Should().Contain("case when length(somestring) >= (5) then somestring else replace(hex(zeroblob((5) - length(somestring))), '00', ' ')||somestring end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.PadRight(5, '0') }))
            .Should().Contain("case when length(somestring) >= (5) then somestring else somestring||replace(hex(zeroblob((5) - length(somestring))), '00', '0') end");
    }

    [Fact]
    public void RemoveAndInsert_ShouldSpliceWithSubstring()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Remove(2) }))
            .Should().Contain("substring(somestring, 0 + 1, 2)");
        SqlOf(ctx, e.Select(x => new { V = x.String!.Remove(2, 1) }))
            .Should().Contain("substring(somestring, 0 + 1, 2)||''||substring(somestring, (2) + (1) + 1, length(somestring) - ((2) + (1)))");
        SqlOf(ctx, e.Select(x => new { V = x.String!.Insert(2, "x") }))
            .Should().Contain("substring(somestring, 0 + 1, 2)||'x'||substring(somestring, 2 + 1, length(somestring) - (2))");
    }

    [Fact]
    public void NewString_ShouldUseZeroblobRepeat()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = new string('*', 4) }))
            .Should().Contain("replace(hex(zeroblob(4)), '00', '*')");
    }

    [Fact]
    public void StringIsNullOrEmpty_ShouldEmitNullCheck()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.IsNullOrEmpty(x.String)).Select(x => new { x.Id }))
            .Should().Contain("(somestring is null or somestring = '')");
    }

    [Fact]
    public void Like_ShouldEmitLikePredicate()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.Sql.like(x.String, "%a%")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%a%'");
    }

    [Fact]
    public void Like_WithEscapeChar_ShouldEmitEscapeClause()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.Sql.like(x.String, "%a!%", "!")).Select(x => new { x.Id }))
            .Should().Contain("somestring like '%a!%' escape '!'");
    }

    [Fact]
    public void MathAbs_ShouldUseAbsFunction()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Math.Abs(x.Id - 5) })).Should().Contain("abs((id - 5))");
    }

    [Fact]
    public void MathRound_ShouldUseRoundFunction()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Math.Round(x.Id / 2.0 + 0.2) }))
            .Should().Contain("round(((cast(id as double precision) / 2) + 0.2))");
    }

    [Fact]
    public void MathTruncate_ShouldUseTruncFunction()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = Math.Truncate(x.Id + 0.0) })).Should().Contain("trunc(");
    }

    [Fact]
    public void MathLog_ShouldUseNaturalLogarithm()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // SQLite's log() is base 10, so Math.Log must map to ln().
        SqlOf(ctx, e.Select(x => new { V = Math.Log(x.Id + 1.0) })).Should().Contain("ln(");
    }

    [Fact]
    public void DateTimeNow_ShouldUseDatetimeNow()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { N = DateTime.Now })).Should().Contain("datetime('now')");
        SqlOf(ctx, e.Select(x => new { N = DateTime.UtcNow })).Should().Contain("datetime('now')");
    }

    [Fact]
    public void DateTimeConstructorLiteral_ShouldBindAsParameterNotConcatenate()
    {
        // new DateTime(2014, 3, 20) is a compile-time constant; it must be folded into a parameter, not
        // rendered by visiting the constructor arguments (which concatenated to "2014320").
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Datetime == new DateTime(2014, 3, 20)).Select(x => new { x.Id }));

        sql.Should().Contain(" where dt = ");
        sql.Should().NotContain("2014320");
    }

    [Fact]
    public void DateTimeYearMonthDay_ShouldUseStrftime()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { Y = x.Datetime!.Value.Year }))
            .Should().Contain("cast(strftime('%Y', dt) as integer)");
        SqlOf(ctx, e.Select(x => new { M = x.Datetime!.Value.Month }))
            .Should().Contain("cast(strftime('%m', dt) as integer)");
        SqlOf(ctx, e.Select(x => new { D = x.Datetime!.Value.Day }))
            .Should().Contain("cast(strftime('%d', dt) as integer)");
        SqlOf(ctx, e.Select(x => new { DOY = x.Datetime!.Value.DayOfYear }))
            .Should().Contain("cast(strftime('%j', dt) as integer)");
    }

    [Fact]
    public void Extract_ShouldUseSqliteDatePartForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { Q = SqlFunctions.Sql.extract("quarter", x.Datetime) }))
            .Should().Contain("cast((cast(strftime('%m', dt) as integer) + 2) / 3 as integer)");
        SqlOf(ctx, e.Select(x => new { W = SqlFunctions.Sql.extract("week", x.Datetime) }))
            .Should().Contain("cast((cast(strftime('%j', date(dt, '-3 days', 'weekday 4')) as integer) + 6) / 7 as integer)");
        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.extract("dow", x.Datetime) }))
            .Should().Contain("cast(strftime('%w', dt) as integer)");
        SqlOf(ctx, e.Select(x => new { I = SqlFunctions.Sql.extract("isodow", x.Datetime) }))
            .Should().Contain("(cast(strftime('%w', dt) as integer) + 6) % 7 + 1");
        SqlOf(ctx, e.Select(x => new { E = SqlFunctions.Sql.date_part("epoch", x.Datetime) }))
            .Should().Contain("((julianday(dt) - 2440587.5) * 86400.0)");
    }

    [Fact]
    public void SetSeed_ShouldThrowBecauseSqliteHasNoSeed()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { S = SqlFunctions.Postgres.setseed(0.5) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*setseed*");
    }

    [Fact]
    public void CryptoHash_ShouldThrowBecauseOnlyPostgresHasIt()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { H = SqlFunctions.Postgres.digest("abc", "sha256") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*digest/sha256*");

        var sha512 = () => SqlOf(ctx, e.Select(x => new { H = SqlFunctions.Postgres.sha512(SqlFunctions.Parameter<byte[]>(0)) }));
        sha512.Should().Throw<NotSupportedException>().WithMessage("*digest/sha256*");
    }

    [Fact]
    public void PostgresFunctionGaps_ShouldThrowOnSqlite()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var regexp = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.regexp_substr(x.String, "a") }));
        regexp.Should().Throw<NotSupportedException>().WithMessage("*extended scalar*");

        var makeTime = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.make_time(1, 2, 3.0) }));
        makeTime.Should().Throw<NotSupportedException>().WithMessage("*extended scalar*");

        var age = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.age(x.Datetime, x.Datetime) }));
        age.Should().Throw<NotSupportedException>().WithMessage("*extended scalar*");

        var dateBin = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.date_bin("1 hour", x.Datetime, x.Datetime) }));
        dateBin.Should().Throw<NotSupportedException>().WithMessage("*extended scalar*");

        var setting = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.current_setting("app.x") }));
        setting.Should().Throw<NotSupportedException>().WithMessage("*extended scalar*");

        var nextval = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.nextval("s") }));
        nextval.Should().Throw<NotSupportedException>().WithMessage("*extended scalar*");

        var jsonArray = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.json_array(1, 2) }));
        jsonArray.Should().Throw<NotSupportedException>().WithMessage("*JSON*");

        var jsonValue = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.json_value(SqlFunctions.Parameter<JsonDocument>(0), "$.a") }));
        jsonValue.Should().Throw<NotSupportedException>().WithMessage("*JSON*");
    }

    [Fact]
    public void PostgresTableFunctions_ShouldThrowBecauseOnlyPostgresHasThem()
    {
        using var ctx = SqliteTestContext.Create();
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

        var record = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Postgres.jsonb_to_record<int>(json))
            .Select(r => new { V = r }));
        record.Should().Throw<NotSupportedException>().WithMessage("*jsonb_to_record*");

        var values = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.ClickHouse.values<int>("(1)"))
            .Select(r => new { V = r }));
        values.Should().Throw<NotSupportedException>().WithMessage("*values*");
    }

    [Fact]
    public void ArrayShuffle_ShouldThrowBecausePostgresArraySurfaceIsGated()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e
            .Where(x => SqlFunctions.Postgres.array_shuffle(SqlFunctions.Parameter<long[]>(0)) != null)
            .Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Arrays are not supported*");
    }

    [Fact]
    public void IsNullOrWhiteSpace_ShouldThrowClearException()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => string.IsNullOrWhiteSpace(x.String)).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*IsNullOrWhiteSpace*");
    }

    [Fact]
    public void MathLogWithBase_ShouldThrowClearException()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // The two-argument Math.Log has a provider-specific argument order, so it is left unsupported.
        var act = () => SqlOf(ctx, e.Select(x => new { V = Math.Log(x.Id + 1.0, 10) }));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InValues_ShouldRenderInPredicateWithParameters()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new long[] { 1, 2, 3 };

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in ($p0, $p1, $p2)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2");
    }

    [Fact]
    public void InValues_InlineParams_ShouldBecomeParameters()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, 1L, 2L)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in ($p0, $p1)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void InValues_Empty_ShouldRenderAlwaysFalse()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = Array.Empty<long>();

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("1 = 0");
        command.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
    }

    [Fact]
    public void InValues_SingleElement_ShouldRenderInPredicate()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new long[] { 2 };

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in ($p0)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0");
    }

    [Fact]
    public void InValues_WithNull_ShouldAddNullBranch()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new int?[] { 1, null };

        var command = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Int, values)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("(nullableint in ($p0) or nullableint is null)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0");
    }

    [Fact]
    public void InValues_AllNull_ShouldRenderIsNull()
    {
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new List<long> { 1, 2 };

        var command = Prepare(ctx, e.Where(x => values.Contains(x.Id)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in ($p0, $p1)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void Contains_CapturedArray_ShouldRenderInPredicate()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var values = new long[] { 1, 2 };

        var command = Prepare(ctx, e.Where(x => values.Contains(x.Id)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id in ($p0, $p1)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1");
    }

    [Fact]
    public void LogicalNot_InWhere_ShouldEmitNotPredicate()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => !x.Boolean!.Value).Select(x => new { x.Id }))
            .Should().Contain("where not (b)");
    }

    [Fact]
    public void LogicalNot_WhenProjected_ShouldEmitNotScalar()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // SQLite has a boolean type valid as a scalar, so no cast/CASE is added around the negation.
        var sql = SqlOf(ctx, e.Select(x => !x.Boolean!.Value));

        sql.Should().Contain("not (b)");
        sql.Should().NotContain("cast(");
    }

    [Fact]
    public void LogicalNot_WithCapturedValue_ShouldEmitParameter()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var flag = true;

        var command = Prepare(ctx, e.Where(x => !flag).Select(x => new { x.Id }));

        // The parameter-extraction pass has to walk the operand even though it emits no SQL.
        Normalize(command.DbCommand.CommandText).Should().Contain("not ($flag)");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("flag");
    }

    [Fact]
    public void Negate_ShouldParenthesiseOperand()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => -x.Id)).Should().Contain("-(id)");
    }

    [Fact]
    public void OnesComplement_ShouldEmitTilde()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => ~x.Id)).Should().Contain("~(id)");
    }

    [Fact]
    public void LogicalNot_OfAnd_ShouldNegateWholePredicate()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => !(x.Boolean!.Value && x.Id > 1L)).Select(x => new { x.Id }))
            .Should().Contain("not ((b and (id > 1)))");
    }

    [Fact]
    public void LogicalNot_OfOr_ShouldNegateWholePredicate()
    {
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cte = e.Where(x => x.Id > 1).Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.With("recent", cte).From("recent").Select(t => new { id = t["id"].AsInt }));

        sql.Should().StartWith("with recent as (select id from complex_entity");
        sql.Should().Contain("where (id > 1))");
        sql.Should().EndWith("select id from recent");
    }

    [Fact]
    public void Cte_FromDefinition_ShouldUseDefinitionName()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var query = e.Where(x => x.Id > 1).Select(x => new { x.Id });
        var cte = ctx.With("recent", query);
        var sql = SqlOf(ctx, cte.From(cte.Ctes[0]).Select(t => new { id = t["id"].AsInt }));

        sql.Should().StartWith("with recent as (select id from complex_entity");
        sql.Should().EndWith("select id from recent");
    }

    [Fact]
    public void Cte_TwoChained_ShouldEmitBothWithClauses()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var first = e.Where(x => x.Id > 1).Select(x => new { x.Id });
        var second = ctx.From("first").Select(t => new { id = t["id"].AsInt });
        var sql = SqlOf(ctx, ctx.With("first", first).With("second", second).From("second").Select(t => new { id = t["id"].AsInt }));

        sql.Should().StartWith("with first as (select id from complex_entity");
        sql.Should().Contain("), second as (select id from first)");
        sql.Should().EndWith("select id from second");
    }

    [Fact]
    public void Cte_Source_ShouldSupportGroupByHavingOrderByLimit()
    {
        using var ctx = SqliteTestContext.Create();
        var cte = ctx.From<IComplexEntity>().Select(x => new { x.Id, somestring = x.String });

        var sql = SqlOf(ctx, ctx.With("recent", cte)
            .From("recent")
            .Where(t => t.GetInt64("id") > 0)
            .GroupBy(t => new { somestring = t.GetString("somestring") })
            .Having(t => SqlFunctions.Sql.count() > 1)
            .OrderBy(t => t.GetString("somestring"))
            .Limit(5)
            .Select(t => new { somestring = t.GetString("somestring"), count = SqlFunctions.Sql.count() }));

        sql.Should().StartWith("with recent as (select id, somestring from complex_entity) select somestring, count(*) as 'count' from recent");
        sql.Should().Contain("group by somestring");
        sql.Should().Contain("having (count(*) > 1)");
        sql.Should().Contain("order by somestring");
        sql.Should().EndWith("limit 5");
    }

    [Fact]
    public void Cte_Body_CanGroupOverEarlierCte()
    {
        using var ctx = SqliteTestContext.Create();

        var first = ctx.From<IComplexEntity>().Select(x => new { x.Id, somestring = x.String });
        var second = ctx.From("first")
            .GroupBy(t => new { somestring = t.GetString("somestring") })
            .Select(t => new { somestring = t.GetString("somestring"), count = SqlFunctions.Sql.count() });

        var sql = SqlOf(ctx, ctx.With("first", first).With("second", second)
            .From("second")
            .OrderByDescending(t => t.GetInt32("count"))
            .Select(t => new { somestring = t.GetString("somestring"), count = t.GetInt32("count") }));

        sql.Should().Contain(", second as (select somestring, count(*) as 'count' from first");
        sql.Should().Contain("group by somestring)");
        sql.Should().Contain("select somestring, count from second");
        sql.Should().Contain("order by count desc");
    }

    [Fact]
    public void Cte_JoinedToAnotherCte_ShouldQualifyAliasColumns()
    {
        using var ctx = SqliteTestContext.Create();

        var left = ctx.From<IComplexEntity>().Select(x => new { x.Id });
        var right = ctx.From<ISimpleEntity>().Select(x => new { x.Id });

        var sql = SqlOf(ctx, ctx.With("l", left).With("r", right)
            .From("l")
            .Join(ctx.From("r"), (l, r) => l.GetInt64("id") == r.GetInt64("id"))
            .Select(p => new { Id = p.Item1.GetInt64("id"), Other = p.Item2.GetInt64("id") }));

        sql.Should().Be("with l as (select id from complex_entity), r as (select id from simple_entity) select t1.id, t2.id from l as 't1' join r as 't2' on t1.id = t2.id");
    }

    [Fact]
    public void Cte_WithCapturedParam_ShouldExtractParam()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var threshold = 1L;

        var command = Prepare(ctx, ctx
            .With("recent", e.Where(x => x.Id > threshold).Select(x => new { x.Id }))
            .From("recent")
            .Select(t => new { id = t["id"].AsInt }));

        Normalize(command.DbCommand.CommandText).Should().Contain("$threshold");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("threshold");
    }

    [Fact]
    public void Cte_Recursive_ShouldEmitWithRecursiveKeyword()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var anchor = e.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.UnionAll(step);

        var sql = SqlOf(ctx, ctx.WithRecursive("nums", body).From("nums").Select(t => new CteNumberRow { n = t["n"].AsInt }));

        sql.Should().StartWith("with recursive nums as (");
        sql.Should().Contain(" union all ");
        sql.Should().Contain("select n from nums");
        sql.Should().NotContain("maxrecursion");
    }

    [Fact]
    public void Cte_Recursive_WithDistinctUnion_ShouldEmitUnionNotUnionAll()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var anchor = e.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.Union(step);

        var sql = SqlOf(ctx, ctx.WithRecursive("nums", body).From("nums").Select(t => new CteNumberRow { n = t["n"].AsInt * 2 }));

        sql.Should().StartWith("with recursive nums as (");
        sql.Should().Contain(" union ");
        sql.Should().NotContain("union all");
        sql.Should().Contain("(n * 2)");
        sql.Should().NotContain("from (select");
    }

    [Fact]
    public void RowNumber_ShouldEmitOverWithPartitionAndOrder()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over(partitionBy: () => x.Int, orderBy: () => x.Id)
        }));

        sql.Should().Be("select id, row_number() over (partition by nullableint order by id) as 'rn' from complex_entity");
    }

    [Fact]
    public void RankAndDenseRank_ShouldEmitOverWithOrder()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            r = SqlFunctions.Sql.rank().Over(SqlFunctions.Sql.asc(() => x.Id)),
            dr = SqlFunctions.Sql.dense_rank().Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Be("select id, rank() over (order by id) as 'r', dense_rank() over (order by id) as 'dr' from complex_entity");
    }

    [Fact]
    public void PercentRankCumeDist_ShouldEmitOverWithOrder()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            pr = SqlFunctions.Sql.percent_rank().Over(SqlFunctions.Sql.asc(() => x.Id)),
            cd = SqlFunctions.Sql.cume_dist().Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Be("select id, percent_rank() over (order by id) as 'pr', cume_dist() over (order by id) as 'cd' from complex_entity");
    }

    [Fact]
    public void Iif_ShouldUseIifFunction()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.iif(x.Id > 0L, "yes", "no") }));

        sql.Should().Contain("iif(");
        sql.Should().Contain("'yes', 'no')");
    }

    [Fact]
    public void NthValue_ShouldEmitOverWithOrder()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            V = SqlFunctions.Sql.nth_value(x.Id, 2).Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Contain("nth_value(id, 2) over (order by id)");
    }

    [Fact]
    public void WindowOrderByDescending_ShouldEmitDesc()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            r = SqlFunctions.Sql.row_number().Over(
                partitionBy: new Expression<Func<object?>>[] { () => x.Int },
                orderBy: new[] { SqlFunctions.Sql.desc(() => x.Id) })
        }));

        sql.Should().Be("select id, row_number() over (partition by nullableint order by id desc) as 'r' from complex_entity");
    }

    [Fact]
    public void LagAndLead_ShouldEmitOffsetAndDefault()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            prev = SqlFunctions.Sql.lag(x.Id, 1, 0L).Over(SqlFunctions.Sql.asc(() => x.Id)),
            next = SqlFunctions.Sql.lead(x.Int, 2, 0).Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Be("select id, lag(id, 1, 0) over (order by id) as 'prev', lead(nullableint, 2, 0) over (order by id) as 'next' from complex_entity");
    }

    [Fact]
    public void WindowAggregate_ShouldEmitOverPartition()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            total = SqlFunctions.Sql.sum_over(x.Id).Over(partitionBy: () => x.Int),
            n = SqlFunctions.Sql.count_over().Over(partitionBy: () => x.Int)
        }));

        sql.Should().Be("select id, sum(id) over (partition by nullableint) as 'total', count(*) over (partition by nullableint) as 'n' from complex_entity");
    }

    [Fact]
    public void WindowFrame_ShouldEmitRowsBetween()
    {
        using var ctx = SqliteTestContext.Create();
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

        sql.Should().Be("select id, sum(id) over (order by id rows between unbounded preceding and current row) as 'running', sum(id) over (order by id rows between 1 preceding and 1 following) as 'sliding' from complex_entity");
    }

    [Fact]
    public void NtileAndFirstLastValue_ShouldEmitOver()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            bucket = SqlFunctions.Sql.ntile(2).Over(SqlFunctions.Sql.asc(() => x.Id)),
            first = SqlFunctions.Sql.first_value(x.Int).Over(partitionBy: () => x.Int, orderBy: () => x.Id),
            last = SqlFunctions.Sql.last_value(x.Int).Over(partitionBy: () => x.Int, orderBy: () => x.Id)
        }));

        sql.Should().Be("select id, ntile(2) over (order by id) as 'bucket', first_value(nullableint) over (partition by nullableint order by id) as 'first', last_value(nullableint) over (partition by nullableint order by id) as 'last' from complex_entity");
    }

    [Fact]
    public void WindowMultiplePartitionsAndRangeFrame_ShouldEmitAllParts()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            v = SqlFunctions.Sql.sum_over(x.Id).Over(
                partitionBy: new Expression<Func<object?>>[] { () => x.Int, () => x.Boolean },
                orderBy: new[] { SqlFunctions.Sql.asc(() => x.Int), SqlFunctions.Sql.desc(() => x.Id) },
                frame: WindowFrame.Range(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
        }));

        sql.Should().Be("select id, sum(id) over (partition by nullableint, b order by nullableint, id desc range between unbounded preceding and current row) as 'v' from complex_entity");
    }

    [Fact]
    public void WindowFrame_FullBoundaries_ShouldEmitRowsAndRange()
    {
        using var ctx = SqliteTestContext.Create();
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

        sql.Should().Contain("sum(id) over (order by id desc rows between unbounded preceding and unbounded following) as 'full'");
        sql.Should().Contain("sum(id) over (order by id range between unbounded preceding and current row) as 'range'");
    }

    [Fact]
    public void WindowFunction_WithoutOver_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { x.Id, rn = SqlFunctions.Sql.row_number() }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Over*");
    }

    [Fact]
    public void WindowFrameGroupsAndExclusion_ShouldEmit()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            v = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.Groups(1, 1).WithExclusion(WindowFrameExclusion.CurrentRow))
        }));

        sql.Should().Contain("sum(id) over (order by id groups between 1 preceding and 1 following exclude current row) as 'v'");
    }

    [Fact]
    public void NamedWindow_ShouldEmitWindowClauseAndOverReference()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var b = e.Window("w", partitionBy: [x => x.Int], orderBy: [e.Asc(x => x.Id)]);
        var sql = SqlOf(ctx, b.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over("w")
        }));

        sql.Should().Contain("row_number() over w as 'rn'");
        sql.Should().Contain("window w as (partition by nullableint order by id)");
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
    public void TableFunction_NoArgs_ShouldEmitCall()
    {
        using var ctx = SqliteTestContext.Create();

        SqlOf(ctx, ctx.FromTableFunction(() => Tvf.AllRows()).Select(r => new { r.Id }))
            .Should().Be("select id from all_rows()");
    }

    [Fact]
    public void BuiltInTableFunction_UnsupportedByProvider_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var start = 1L;
        var stop = 5L;

        var act = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Postgres.generate_series(start, stop))
            .Select(r => new { r.Value }));

        act.Should().Throw<NotSupportedException>().WithMessage("*generate_series*");
    }

    [Fact]
    public void TableFunction_WithArgument_ShouldEmitParameter()
    {
        using var ctx = SqliteTestContext.Create();
        var id = 5L;

        var command = Prepare(ctx, ctx.FromTableFunction(() => Tvf.ById(id)).Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText).Should().Be("select id from rows_by_id($id)");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("id");
    }

    [Fact]
    public void TableFunction_TwoArgumentsAndSchema_ShouldEmitQualifiedCall()
    {
        using var ctx = SqliteTestContext.Create();
        var lo = 1L;
        var hi = 3L;

        var command = Prepare(ctx, ctx.FromTableFunction(() => Tvf.Between(lo, hi)).Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText).Should().Be("select id from app.rows_between($lo, $hi)");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("lo", "hi");
    }

    [Fact]
    public void TableFunction_JoinedToTable_ShouldAliasBothSources()
    {
        using var ctx = SqliteTestContext.Create();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, ctx
            .FromTableFunction(() => Tvf.AllRows())
            .Join(complex, (r, c) => r.Id == c.Id)
            .Select(p => new { p.Item1.Value, p.Item2.String }));

        sql.Should().Be("select t1.value, t2.somestring as 'String' from all_rows() as 't1' join complex_entity as 't2' on t1.id = t2.id");
    }

    [Fact]
    public void TableFunction_AsJoinedSource_ShouldAliasBothSources()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, simple
            .Join(ctx.FromTableFunction(() => Tvf.AllRows()), (s, r) => r.Id == s.Id)
            .Select(p => new { p.Item1.Id, p.Item2.Value }));

        sql.Should().Be("select t1.id, t2.value from simple_entity as 't1' join all_rows() as 't2' on t2.id = cast(t1.id as bigint)");
    }

    [Fact]
    public void ArrayAny_ShouldThrowBecauseSqliteHasNoArrays()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.Id == SqlFunctions.Postgres.any(SqlFunctions.Parameter<long[]>(0))).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Arrays*");
    }

    [Fact]
    public void JsonAgg_ShouldThrowBecauseSqliteHasNoJsonbSurface()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.Postgres.json_agg(x.String)));

        act.Should().Throw<NotSupportedException>().WithMessage("*JSON*");
    }

    [Fact]
    public void NullIf_ShouldEmitNullIf()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // nullif is ANSI and therefore not capability-gated.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.nullif(x.Id, 0L) }))
            .Should().Contain("nullif(id, 0)");
    }

    [Fact]
    public void FilteredAggregate_ShouldEmitFilterClause()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // SQLite 3.30+ accepts the FILTER (WHERE ...) aggregate clause.
        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.count(() => x.Id > 1L)))
            .Should().Contain("count(*) filter (where (id > 1))");
    }

    [Fact]
    public void GreatestLeast_ShouldEmitMaxMin()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Hi = SqlFunctions.Sql.greatest(x.Id, x.Id),
            Lo = SqlFunctions.Sql.least(x.Id, x.Id)
        }));

        sql.Should().Contain("max(id, id)");
        sql.Should().Contain("min(id, id)");
    }

    [Fact]
    public void Greatest_WithSingleArgument_ShouldEmitArgument()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.greatest(x.Id) }))
            .Should().Contain("(id)");
    }

    [Fact]
    public void DateTrunc_ShouldThrowBecauseSqliteHasNoDateTrunc()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.date_trunc("month", x.Datetime) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*date_trunc*");
    }

    [Fact]
    public void DateAdd_ShouldUseDatetimeModifier()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.date_add("day", 1, x.Datetime) }))
            .Should().Contain("datetime(dt, (1) || ' days')");
    }

    [Fact]
    public void DateTimeAddMethod_ShouldUseDatetimeModifier()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.Datetime!.Value.AddDays(1) }))
            .Should().Contain("datetime(dt, (1) || ' days')");
    }

    [Fact]
    public void DateDiffAndEndOfMonth_ShouldUseSqliteFunctions()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.date_diff("day", x.Datetime, x.Datetime) }))
            .Should().Contain("/ 86400");

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.end_of_month(x.Datetime) }))
            .Should().Contain("date(dt, 'start of month', '+1 month', '-1 day')");
    }

    [Fact]
    public void StringAgg_ShouldUseGroupConcat()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.string_agg(x.String, ",")))
            .Should().Contain("group_concat(somestring, ',')");
    }

    [Fact]
    public void TextJsonFunctions_ShouldThrowBecauseSqliteLacksThem()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.SqlServer.json_value(x.String, "$.id")));

        act.Should().Throw<NotSupportedException>().WithMessage("*text JSON*");
    }

    [Fact]
    public void FullTextPredicates_ShouldThrowBecauseSqliteLacksThem()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => SqlFunctions.Sql.contains(x.String, "foo")).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*full-text*");
    }

    [Fact]
    public void TableHint_ShouldThrowBecauseSqliteHasNoTableHints()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>().WithTableHint("nolock").Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Table hints*");
    }

    [Fact]
    public void ForUpdate_ShouldThrowBecauseSqliteHasNoRowLocking()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.ForUpdate().Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR UPDATE*");
    }

    [Fact]
    public void ForUpdate_SkipLocked_ShouldThrowBecauseSqliteHasNoRowLocking()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.ForUpdate(LockWaitMode.SkipLocked).Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR UPDATE*");
    }

    [Fact]
    public void WithForJson_ShouldThrowBecauseSqliteHasNoForJson()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { x.Id }).WithForJson());

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR JSON*");
    }

    [Fact]
    public void WithForXml_ShouldThrowBecauseSqliteHasNoForXml()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { x.Id }).WithForXml());

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR XML*");
    }

    [Fact]
    public void BooleanAggregates_ShouldThrowBecauseSqliteLacksThem()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.bool_and(x.Id) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*bool_and/bool_or/every*");
    }

    [Fact]
    public void StatisticalAggregates_ShouldThrowBecauseSqliteLacksThem()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.corr(x.Id, x.Id) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*statistical*");
    }

    [Fact]
    public void OrderedSetAggregates_ShouldThrowBecauseSqliteLacksThem()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.percentile_cont(0.5, () => x.Id) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*ordered-set*");
    }

    [Fact]
    public void AnyValueAggregate_ShouldThrowBecauseSqliteLacksIt()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.any_agg(x.String) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*ANY_VALUE*");
    }

    [Fact]
    public void ExtendedScalarFunctions_ShouldThrowBecauseSqliteLacksThem()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.asin(x.Id) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*extended scalar*");
    }

    [Fact]
    public void SessionInfoFunctions_ShouldThrowBecauseSqliteLacksThem()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.current_user() }));

        act.Should().Throw<NotSupportedException>().WithMessage("*current_user*");
    }

    [Fact]
    public void Version_ShouldUseSqliteVersion()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.version() }));

        sql.Should().Contain("sqlite_version()");
    }

    [Fact]
    public void UuidGenerators_ShouldThrowBecauseSqliteLacksThem()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { U = SqlFunctions.Sql.gen_random_uuid() }));

        act.Should().Throw<NotSupportedException>().WithMessage("*UUID generator*");
    }

    [Fact]
    public void BinaryColumn_ShouldEmitBytesAccessor()
    {
        using var ctx = SqliteTestContext.Create();

        SqlOf(ctx, ctx.From("binary_entity").Select(t => new { Payload = t.GetBytes("data") }))
            .Should().Be("select data from binary_entity");
    }

    [Fact]
    public void BinaryColumn_ShouldEmitAsBytesAccessor()
    {
        using var ctx = SqliteTestContext.Create();

        SqlOf(ctx, ctx.From("binary_entity").Select(t => new { Payload = t["data"].AsBytes }))
            .Should().Be("select data from binary_entity");
    }

    [Fact]
    public void SelectMany_ShouldThrowNotSupportedOnSqlProvider()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => e.SelectMany(x => new[] { x.Id });

        act.Should().Throw<NotSupportedException>().WithMessage("*SelectMany*");
    }

    [Fact]
    public void GroupJoin_ShouldThrowNotSupportedOnSqlProvider()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var act = () => e.GroupJoin(inner, o => o.Id, i => i.Id, (o, g) => new { o.Id, Count = g.Count() });

        act.Should().Throw<NotSupportedException>().WithMessage("*GroupJoin*");
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
    public void DistinctOn_ShouldThrowBecauseSqliteHasNoDistinctOn()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.DistinctOn(x => x.Id).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*DISTINCT ON*");
    }

    [Fact]
    public void TableSample_ShouldThrowBecauseSqliteHasNoTableSample()
    {
        using var ctx = SqliteTestContext.Create();
        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>(o => o.TableSample(10)).Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*TABLESAMPLE*");
    }

    [Fact]
    public void TextSearch_ShouldThrowBecauseSqliteHasNoTsvector()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { Q = SqlFunctions.Postgres.to_tsquery("a") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*text-search*");
    }

    [Fact]
    public void ForSystemTime_ShouldThrowBecauseSqliteHasNoTemporalTables()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.ForSystemTime(TemporalClause.All()).Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR SYSTEM_TIME*");
    }

    [Fact]
    public void XmlMethods_ShouldThrowBecauseOnlySqlServerHasThem()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.xml_query(x.String, "/root") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*XML data-type methods*");
    }

    [Fact]
    public void XmlNodes_ShouldThrowBecauseSqliteHasNoApply()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.String, "/root/item"))
            .Select(p => new { p.Item2.Value }));

        act.Should().Throw<NotSupportedException>().WithMessage("*CrossApply*");
    }

    [Fact]
    public void DerivedSourceThenJoin_ShouldRenderDerivedTable()
    {
        using var ctx = SqliteTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Where(c => c.Id > 0)
            .Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id, p.Item1.String }));

        sql.Should().Be("select t1.id, t2.id as 'SId', t1.String from (select id, somestring as 'String' from complex_entity\n where (id > 0)) as 't1' join simple_entity as 't2' on t1.id = cast(t2.id as bigint)");
    }

    [Fact]
    public void DerivedSourceWithJoinThenJoin_ShouldResolveTheOuterAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var derived = ctx.From<ISimpleEntity>()
            .Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
            .Select(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<IComplexEntity>(), (d, c2) => d.OrderId == c2.Id)
            .Select(p => new { p.Item1.OrderId, p.Item1.CustomerName, Third = p.Item2.Id }));

        sql.Should().Be("select t3.OrderId, t3.CustomerName, t4.id as 'Third' from (select t1.id as 'OrderId', t2.somestring as 'CustomerName' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id) as 't3' join complex_entity as 't4' on cast(t3.OrderId as bigint) = t4.id");
    }

    [Fact]
    public void DerivedSource_JoinOnFilteredPrimary_ShouldReferenceExposedColumnName()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Where(c => c.Int > 0)
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Int == (int?)s.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id }));

        // The derived table exposes the renamed column as 'Int', so the ON must use that name.
        sql.Should().Contain("nullableint as 'Int'");
        sql.Should().Contain("on t1.Int = ");
        sql.Should().NotContain("t1.nullableint");
    }

    [Fact]
    public void DerivedSourceAndPhysicalSourceOfSameType_ShouldResolveTheirOwnColumns()
    {
        using var ctx = SqliteTestContext.Create();

        var derived = ctx.From<IComplexEntity>().Where(c => c.Id > 1).ToCommand();

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<IComplexEntity>(), (d, c) => d.Int == c.Int)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id }));

        sql.Should().Contain("nullableint as 'Int'");
        sql.Should().Contain("on t1.Int = t2.nullableint");
    }

    [Fact]
    public void ProjectedCommand_OrderByDescending_ShouldResolveProjectionExpression()
    {
        using var ctx = SqliteTestContext.Create();

        var grouped = ctx.From<IComplexEntity>()
            .GroupBy(x => x.Int)
            .Select(x => new { x.Int, Cnt = SqlFunctions.Sql.count() });

        var sql = SqlOf(ctx, grouped.OrderByDescending(x => x.Cnt));

        sql.Should().Contain("group by nullableint");
        sql.Should().Contain("order by count(*) desc");
    }

    [Fact]
    public void ProjectedCommand_PageAndOrder_ShouldEmitProviderPaging()
    {
        using var ctx = SqliteTestContext.Create();

        var grouped = ctx.From<IComplexEntity>()
            .GroupBy(x => x.Int)
            .Select(x => new { x.Int, Cnt = SqlFunctions.Sql.count() });

        var sql = SqlOf(ctx, grouped.OrderByDescending(x => x.Cnt).Page(20, 1));

        sql.Should().Contain("order by count(*) desc");
        sql.Should().Contain("limit 20 offset 1");
    }

    [Fact]
    public void ProjectedCommand_OrderByAndLimit_ShouldResolveSourceColumn()
    {
        using var ctx = SqliteTestContext.Create();

        var projected = ctx.From<IComplexEntity>().Select(x => new { x.Id, Name = x.String });

        var sql = SqlOf(ctx, projected.OrderBy(x => x.Name).Limit(5).Offset(2));

        sql.Should().Contain("order by somestring");
        sql.Should().Contain("limit 5 offset 2");
    }

    [Fact]
    public void DerivedSourceWhereThenJoin_ShouldPushTheFilterOntoTheProjection()
    {
        using var ctx = SqliteTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Where(d => d.Id > 5)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id }));

        sql.Should().Be("select t1.id, t2.id as 'SId' from (select id, somestring as 'String' from complex_entity) as 't1' join simple_entity as 't2' on t1.id = cast(t2.id as bigint)\n where (t1.id > 5)");
    }

    [Fact]
    public void FromSql_ShouldRenderDerivedTableWithNamedParameters()
    {
        using var ctx = SqliteTestContext.Create();
        var min = 1;

        var sql = SqlOf(ctx, ctx
            .FromSql("select id from complex_entity where id > $min", new { min })
            .Select(t => new { Id = t["id"].AsInt }));

        sql.Should().Contain("from (select id from complex_entity where id > $min)");
        sql.Should().Contain("select id from");
    }

    [Fact]
    public void FromSql_AsJoinedSource_ShouldRenderDerivedTableAndResolveColumns()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<ISimpleEntity>()
            .Join(ctx.FromSql("select id from complex_entity"), (s, r) => s.Id == r["id"].AsInt)
            .Select(p => new { p.Item1.Id, R = p.Item2["id"].AsInt }));

        sql.Should().Contain("join (select id from complex_entity) as 't2'");
        sql.Should().Contain("on t1.id = t2.id");
    }

}
