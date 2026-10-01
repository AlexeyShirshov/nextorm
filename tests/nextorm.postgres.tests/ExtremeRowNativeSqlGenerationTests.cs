using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL-generation asserts for PostgreSQL's native <c>SelectWhereMax</c>/<c>SelectWhereMin</c> strategy:
/// the <c>DISTINCT ON</c> grouped form and the <c>ORDER BY ... LIMIT 1</c> global form. The portable
/// window-rank lowering (and every ineligible key) is pinned separately in
/// <see cref="SqlGenerationTests"/>.
/// </summary>
public class ExtremeRowNativeSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    private static string Dequoted(string sql) => sql
        .Replace("\r\n", " ")
        .Replace('\n', ' ')
        .Replace("\"", string.Empty)
        .Replace("[", string.Empty)
        .Replace("]", string.Empty)
        .Replace("`", string.Empty);

    private static string OuterSelectList(string sql)
    {
        const string marker = "select ";
        var start = sql.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return sql;
        start += marker.Length;
        var end = sql.IndexOf(" from ", start, StringComparison.Ordinal);
        return end < 0 ? sql[start..] : sql[start..end];
    }

    // --- global: ORDER BY <key> LIMIT 1 ------------------------------------------------------

    [Fact]
    public void SelectWhereMax_GlobalWholeRow_ShouldRenderNativeLimitOne()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Int).ToCommand()));

        norm.Should().Contain("order by nullableint desc limit 1");
        norm.Should().Contain("nullableint is not null");
        norm.Should().NotContain("row_number()");
        norm.Should().NotContain("rank()");
        OuterSelectList(norm).Should().NotContain("__nextorm_rn").And.NotContain("*");
    }

    [Fact]
    public void SelectWhereMin_GlobalWholeRow_ShouldRenderNativeLimitOneAscending()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.Int).ToCommand()));

        norm.Should().Contain("order by nullableint limit 1");
        norm.Should().NotContain("nullableint desc");
        norm.Should().NotContain("row_number()");
        norm.Should().Contain("nullableint is not null");
    }

    [Fact]
    public void SelectWhereMax_GlobalProjection_ShouldRenderNativeLimitOne()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Int, x => new { x.Id, x.String })));

        norm.Should().Contain("order by nullableint desc limit 1");
        norm.Should().NotContain("row_number()");
        OuterSelectList(norm).Should().Contain("id").And.Contain("somestring").And.NotContain("__nextorm_rn");
    }

    [Fact]
    public void SelectWhereMin_GlobalProjection_ShouldRenderNativeLimitOneAscending()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.Int, x => new { x.Id, x.String })));

        norm.Should().Contain("order by nullableint limit 1");
        norm.Should().NotContain("nullableint desc");
        norm.Should().NotContain("row_number()");
        OuterSelectList(norm).Should().NotContain("__nextorm_rn");
    }

    // --- grouped: DISTINCT ON (<group>) ORDER BY <group>, <key> ------------------------------

    [Fact]
    public void SelectWhereMax_GroupedWholeRow_ShouldRenderDistinctOnWithDescendingKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Int, ExtremeRowTies.One, x => new { x.Id }).ToCommand()));

        norm.Should().Contain("distinct on (id)");
        norm.Should().Contain("order by id, nullableint desc");
        norm.Should().Contain("nullableint is not null");
        norm.Should().NotContain("row_number()");
        norm.Should().NotContain("rank()");
        OuterSelectList(norm).Should().NotContain("*").And.NotContain("__nextorm_rn");
    }

    [Fact]
    public void SelectWhereMin_GroupedWholeRow_ShouldRenderDistinctOnWithAscendingKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.Int, ExtremeRowTies.One, x => new { x.Id }).ToCommand()));

        norm.Should().Contain("distinct on (id)");
        norm.Should().Contain("order by id, nullableint");
        norm.Should().NotContain("nullableint desc");
        norm.Should().NotContain("row_number()");
        OuterSelectList(norm).Should().NotContain("*").And.NotContain("__nextorm_rn");
    }

    [Fact]
    public void SelectWhereMax_GroupedProjection_ShouldRenderDistinctOn()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Int, x => new { x.Id, x.String }, ExtremeRowTies.One, x => new { x.Id })));

        norm.Should().Contain("distinct on (id)");
        norm.Should().Contain("order by id, nullableint desc");
        norm.Should().NotContain("row_number()");
        OuterSelectList(norm).Should().Contain("id").And.Contain("somestring").And.NotContain("__nextorm_rn");
    }

    [Fact]
    public void SelectWhereMin_GroupedProjection_ShouldRenderDistinctOnAscending()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.Int, x => new { x.Id, x.String }, ExtremeRowTies.One, x => new { x.Id })));

        norm.Should().Contain("distinct on (id)");
        norm.Should().Contain("order by id, nullableint");
        norm.Should().NotContain("nullableint desc");
        norm.Should().NotContain("row_number()");
        OuterSelectList(norm).Should().NotContain("__nextorm_rn");
    }

    // --- composite keys: lexicographic order per component -----------------------------------

    [Fact]
    public void SelectWhereMax_CompositeKey_ShouldOrderEveryComponentDescending()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => new { x.Int, x.Id }, x => new { x.Id })));

        norm.Should().Contain("order by nullableint desc, id desc limit 1");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMin_CompositeKey_ShouldOrderEveryComponentAscending()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => new { x.Int, x.Id }, x => new { x.Id })));

        norm.Should().Contain("order by nullableint, id limit 1");
        norm.Should().NotContain("desc");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_CompositeGroupKey_ShouldDistinctOnEveryComponent()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Int, x => new { x.Id }, ExtremeRowTies.One, x => new { x.Id, x.Int })));

        norm.Should().Contain("distinct on (id, nullableint)");
        norm.Should().Contain("order by id, nullableint, nullableint desc");
        norm.Should().NotContain("row_number()");
    }

    // --- aliases / mapped columns / quoting --------------------------------------------------

    [Fact]
    public void SelectWhereMax_Grouped_ShouldUseMappedPhysicalColumnNames()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Int, ExtremeRowTies.One, x => new { x.Id }).ToCommand()));

        // The selectors are the CLR properties Int/Id; the native clauses must carry the mapped
        // physical names nullableint/id.
        norm.Should().Contain("distinct on (id)");
        norm.Should().Contain("order by id, nullableint desc");
    }

    [Fact]
    public void SelectWhereMax_Grouped_QuotedIdentifiers_ShouldQuoteNativeAliases()
    {
        using var ctx = PostgresTestContext.CreateQuoted();
        var e = ctx.From<ExtremeNativeEntity>();

        var sql = SqlOf(ctx, e.SelectWhereMax(x => x.Int, ExtremeRowTies.One, x => new { x.Id }).ToCommand());

        sql.Should().Contain("distinct on (\"id\")");
        sql.Should().Contain("order by \"id\", \"nullableint\" desc");
    }

    // --- outer output ordering stays outer ---------------------------------------------------

    [Fact]
    public void SelectWhereMax_WithOutputOrderBy_ShouldKeepWinnerOrderingInnerAndOutputOrderingOuter()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.OrderBy(x => x.Id).SelectWhereMax(x => x.Int, x => new { x.Id })));

        var innerOrder = norm.IndexOf("order by nullableint desc limit 1", StringComparison.Ordinal);
        var derivedTableEnd = norm.IndexOf(") as t1", StringComparison.Ordinal);
        var outerOrder = norm.LastIndexOf("order by", StringComparison.Ordinal);

        innerOrder.Should().BeGreaterThanOrEqualTo(0);
        derivedTableEnd.Should().BeGreaterThan(innerOrder);
        outerOrder.Should().BeGreaterThan(derivedTableEnd);
        norm.Should().Contain("limit 1) as t1 order by");
        // The user's output ordering is not woven into the tie-winner ordering.
        norm.Should().NotContain("order by nullableint desc, id");
    }

    // --- fallback: All and ineligible key types keep the portable window path ----------------

    [Fact]
    public void SelectWhereMax_AllTies_ShouldKeepPortableWindowLowering()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Int, x => new { x.Id }, ExtremeRowTies.All)));

        norm.Should().Contain("rank() over (order by nullableint desc)");
        norm.Should().Contain("= 1");
        norm.Should().NotContain("limit 1");
        norm.Should().NotContain("distinct on");
    }

    [Fact]
    public void SelectWhereMax_AllTiesGrouped_ShouldKeepPortableWindowLowering()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Int, ExtremeRowTies.All, x => x.Id).Select(x => new { x.Id })));

        norm.Should().Contain("rank() over (partition by id order by nullableint desc)");
        norm.Should().Contain("= 1");
        norm.Should().NotContain("limit 1");
        norm.Should().NotContain("distinct on");
    }

    [Fact]
    public void SelectWhereMax_StringKey_ShouldKeepPortableWindowLowering()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.String, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by somestring desc)");
        norm.Should().NotContain("limit 1");
        norm.Should().NotContain("distinct on");
    }

    [Fact]
    public void SelectWhereMax_DateTimeKey_ShouldKeepPortableWindowLowering()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Datetime, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by dt desc)");
        norm.Should().NotContain("limit 1");
    }

    [Fact]
    public void SelectWhereMax_ExpressionKey_ShouldKeepPortableWindowLowering()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // A computed expression is not a direct mapped column, so it is not natively eligible even
        // though the result is integral.
        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Int + 1, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by");
        norm.Should().NotContain("limit 1");
    }

    [Fact]
    public void SelectWhereMax_FloatKey_ShouldKeepPortableWindowLowering()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IFloatEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.D, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by d desc)");
        norm.Should().NotContain("limit 1");
        norm.Should().NotContain("distinct on");
    }

    [Fact]
    public void SelectWhereMax_CompositeFloatingKey_ShouldKeepPortableWindowLowering()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IFloatEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => new { x.D, x.Id }, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by d desc, id desc)");
        norm.Should().NotContain("limit 1");
    }

    // --- unsupported modifiers still reject before dispatch ----------------------------------

    [Fact]
    public void SelectWhereMax_WithPaging_ShouldRejectBeforeNativeDispatch()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Limit(1).SelectWhereMax(x => x.Int).Select(x => new { x.Id }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*paging*");
    }

    [Fact]
    public void SelectWhereMax_WithDistinctOn_ShouldRejectBeforeNativeDispatch()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.DistinctOn(x => x.Id).SelectWhereMax(x => x.Int).Select(x => new { x.Id }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*DISTINCT ON*");
    }

    [Fact]
    public void SelectWhereMax_WithJoin_ShouldRejectBeforeNativeDispatch()
    {
        using var ctx = PostgresTestContext.Create();
        var joined = ctx.From<ISimpleEntity>().Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Int);

        var act = () => SqlOf(ctx, joined.SelectWhereMax(p => p.Item2.Int, p => new { p.Item1.Id }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*joins*");
    }

    // --- fail-closed: a renderer failure is an error, never a late portable fallback -------------

    [Fact]
    public void SelectWhereMax_WhenRendererCanRenderButThrows_ShouldPropagateAndNotFallBack()
    {
        using var ctx = new ThrowingPostgresDataContext(
            "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm",
            new DataContextBuilder());
        var e = ctx.From<ExtremeNativeEntity>();

        // CanRender accepted the command, so the native path is taken; its renderer throwing must
        // surface the exception rather than silently retrying the portable window lowering.
        var act = () => SqlOf(ctx, e.SelectWhereMax(x => x.Int).ToCommand());

        act.Should().Throw<InvalidOperationException>().WithMessage("native renderer boom");
    }

    // --- temp-table source: no mapped payload, so the native renderer must decline, not throw ---------

    [Fact]
    public void SelectWhereMax_TempTableSource_ShouldFallBackToPortableLowering()
    {
        using var ctx = PostgresTestContext.Create();
        var temp = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();

        // The lazy temp-table read addresses the generated table by name (TableAlias), so there is no
        // mapped payload metadata the native DISTINCT ON / LIMIT renderer could describe. The pre-existing
        // policy for such a source is the portable window lowering; the native capability must decline
        // rather than fail the whole batch with "requires a mapped entity source".
        var command = ctx.From(temp).SelectWhereMax(t => t.GetInt32("id"), t => new { Id = t.GetInt32("id") });

        var sql = command.ToBatchSql();

        sql.Should().Contain("drop table if exists " + temp.Name);
        sql.Should().Contain("create temporary table " + temp.Name + " as select id from simple_entity");
        sql.Should().Contain("row_number() over (order by id desc)");
        sql.Should().NotContain("limit 1");
        sql.Should().NotContain("distinct on");
    }

    [Fact]
    public void SelectWhereMax_TempTableSource_WithPaging_ShouldStillRejectBeforeDispatch()
    {
        using var ctx = PostgresTestContext.Create();
        var temp = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();

        // The compatibility guard runs before the native/portable decision, so a forbidden modifier is
        // still rejected for a temp-table source and is never legalized by the native inner LIMIT.
        var command = ctx.From(temp).Limit(1)
            .SelectWhereMax(t => t.GetInt32("id"), t => new { Id = t.GetInt32("id") });

        var act = () => command.ToBatchSql();

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*paging*");
    }

    // --- alias collision: a mapped key column named like the internal derived-table alias -----

    [Fact]
    public void SelectWhereMax_Grouped_KeyColumnNamedLikeDerivedAlias_ShouldPickCollisionFreeAlias()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ExtremeAliasEntity>();

        // The extreme key's physical name is "__nextorm_extreme", the renderer's base derived-table
        // alias. Reusing it verbatim would make the ORDER BY on that column ambiguous against the table
        // alias, so the renderer must extend the alias until it no longer collides with any source
        // column.
        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Extreme, ExtremeRowTies.One, x => new { x.G }).ToCommand()));

        norm.Should().Contain(") __nextorm_extreme_ order by");
        norm.Should().NotContain(") __nextorm_extreme order by");
        norm.Should().Contain("order by g, __nextorm_extreme desc");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_Global_KeyColumnNamedLikeDerivedAlias_ShouldStillReferenceTheKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ExtremeAliasEntity>();

        // The global form renders no derived table alias, so a key column named like the alias base is
        // referenced unchanged.
        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Extreme).ToCommand()));

        norm.Should().Contain("order by __nextorm_extreme desc limit 1");
        norm.Should().NotContain("row_number()");
    }

    // --- parameterization: the native predicate keeps the bound parameter --------------------

    [Fact]
    public void SelectWhereMax_WithParameterizedWhere_ShouldKeepTheBoundParameterInTheNativePredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        // A runtime placeholder in the source condition must survive the native limit-1 dispatch as a
        // bound parameter: the native source rebuild must not inline it while re-rendering the WHERE.
        var command = Prepare(ctx, e
            .Where(x => x.Int == SqlFunctions.Parameter<int>(0))
            .SelectWhereMax(x => x.Int).ToCommand());

        var sql = Dequoted(Normalize(command.DbCommand.CommandText));

        sql.Should().Contain("limit 1");
        sql.Should().NotContain("row_number()");
        sql.Should().Contain("nullableint = @norm_p0");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }
}

/// <summary>A renderer that accepts every command and then fails while rendering it.</summary>
internal sealed class ThrowingExtremeRowRenderer : IExtremeRowRenderer
{
    public static readonly ThrowingExtremeRowRenderer Instance = new();

    public bool CanRender(ExtremeRowDescription description) => true;

    public string Render(ExtremeRowRenderRequest request)
        => throw new InvalidOperationException("native renderer boom");
}

/// <summary>PostgreSQL rendering with a renderer that always throws after accepting the command.</summary>
internal sealed class ThrowingExtremeRowDialect : PostgresDialect
{
    public static readonly ThrowingExtremeRowDialect ThrowingInstance = new();

    public override IExtremeRowRenderer? ExtremeRowRenderer => ThrowingExtremeRowRenderer.Instance;
}

/// <summary>The distinct concrete context type used to pin the fail-closed behaviour.</summary>
internal sealed class ThrowingPostgresDataContext : PostgresDataContext
{
    public ThrowingPostgresDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, optionsBuilder)
    {
    }

    public override ISqlDialect Dialect => ThrowingExtremeRowDialect.ThrowingInstance;
}

[SqlTable("float_entity")]
public interface IFloatEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("d")]
    double? D { get; set; }
}

/// <summary>A concrete (materializable) entity for the whole-row native forms.</summary>
[SqlTable("extreme_native_entity")]
public class ExtremeNativeEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("nullableint")]
    public int? Int { get; set; }
}

/// <summary>An entity whose mapped extreme-key column is named like the renderer's internal alias.</summary>
[SqlTable("extreme_alias_entity")]
public class ExtremeAliasEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>Physical name deliberately equals the renderer's derived-table alias base.</summary>
    [Column("__nextorm_extreme")]
    public int? Extreme { get; set; }

    [Column("g")]
    public int? G { get; set; }
}
