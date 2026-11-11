using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// T300 (#300): a bare read of a single-column set-operation command must prepare a scalar
/// materialiser. The defect is cache-history dependent: preparing a scalar derived source first
/// populates the process-wide <c>SelectListCache[typeof(int)]</c> with an object-materialisation
/// list, and the following set-operation preparation reused it without <c>OneColumn</c> and threw
/// <c>QueryPreparationException: Cannot get ctor from System.Int32</c>. These are preparation and
/// SQL-generation assertions over a placeholder connection: no database is opened.
/// </summary>
public class T300ScalarDerivedSourceTests
{
    private static readonly bool SupportsIntersectExceptAll = false;

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static QueryCommand<int> Left(IDataContext ctx)
        => ctx.From<ISimpleEntity>().Where(x => x.Id <= 2).Select(x => x.Id);

    private static QueryCommand<int> Right(IDataContext ctx)
        => ctx.From<ISimpleEntity>().Where(x => x.Id >= 2 && x.Id <= 3).Select(x => x.Id);

    private static void ClearScalarCache() => DataContextCache.SelectListCache.Remove(typeof(int));

    /// <summary>
    /// AC1/AC2 unit arm: a cold scalar derived preparation first (it populated the type cache on the
    /// unfixed tree), then the set-operation preparation. Before the fix the second call reuses the
    /// cached object list and throws the constructor lookup; after the fix both prepare and no
    /// derived-specific list is left in the type-keyed cache.
    /// </summary>
    [Fact]
    public void WarmScalarDerivedPreparation_ThenUnionAll_Prepares()
    {
        ClearScalarCache();
        using var ctx = SqlServerTestContext.Create();

        var scalarSql = SqlOf(ctx, ctx.From(Left(ctx)).ToCommand());
        scalarSql.Should().Contain("select", "the direct scalar derived read keeps preparing");

        var unionSql = SqlOf(ctx, ctx.From(Left(ctx).UnionAll(Right(ctx))).ToCommand());
        unionSql.Should().Contain("union all", "the bare set-operation read prepares its own SQL");

        DataContextCache.SelectListCache.ContainsKey(typeof(int))
            .Should().BeFalse("the single-column derived branch must not write the type-keyed cache");
    }

    /// <summary>
    /// AC2 unit arm: a pre-seeded object-materialisation list under <c>typeof(int)</c> is not read and
    /// not replaced — the single-column derived branch runs first.
    /// </summary>
    [Fact]
    public void PreSeededObjectListIsIgnoredAndPreserved()
    {
        var poison = new[] { new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" } };
        DataContextCache.SelectListCache[typeof(int)] = poison;

        try
        {
            using var ctx = SqlServerTestContext.Create();
            var sql = SqlOf(ctx, ctx.From(Left(ctx).UnionAll(Right(ctx))).ToCommand());

            sql.Should().Contain("union all");
            DataContextCache.SelectListCache[typeof(int)].Should().BeSameAs(poison);
        }
        finally
        {
            ClearScalarCache();
        }
    }

    /// <summary>AC3 unit arm: every supported operator prepares a bare scalar derived source.</summary>
    [Fact]
    public void SupportedOperators_Prepare()
    {
        using var ctx = SqlServerTestContext.Create();

        ClearScalarCache();
        SqlOf(ctx, ctx.From(Left(ctx).Union(Right(ctx))).ToCommand()).Should().Contain("union");
        ClearScalarCache();
        SqlOf(ctx, ctx.From(Left(ctx).UnionAll(Right(ctx))).ToCommand()).Should().Contain("union all");
        ClearScalarCache();
        SqlOf(ctx, ctx.From(Left(ctx).Intersect(Right(ctx))).ToCommand()).Should().Contain("intersect");
        ClearScalarCache();
        SqlOf(ctx, ctx.From(Left(ctx).Except(Right(ctx))).ToCommand()).Should().Contain("except");
    }

    /// <summary>
    /// AC3 capability arm: the ALL variants stay guarded exactly where the dialect does not support
    /// them, and keep preparing where it does.
    /// </summary>
    [Fact]
    public void AllVariants_FollowProviderCapability()
    {
        using var ctx = SqlServerTestContext.Create();
        ClearScalarCache();

        var intersectAll = () => SqlOf(ctx, ctx.From(Left(ctx).IntersectAll(Right(ctx))).ToCommand());
        var exceptAll = () => SqlOf(ctx, ctx.From(Left(ctx).ExceptAll(Right(ctx))).ToCommand());

        if (SupportsIntersectExceptAll)
        {
            intersectAll.Should().NotThrow();
            exceptAll.Should().NotThrow();
        }
        else
        {
            intersectAll.Should().Throw<NotSupportedException>();
            exceptAll.Should().Throw<NotSupportedException>();
        }
    }

    /// <summary>
    /// AC4/E05 unit arm: a nullable int and a reference string (whose seeded rows include null)
    /// prepare as bare scalar derived sources on this provider, and neither preparation leaves a
    /// derived list in the type-keyed cache.
    /// </summary>
    [Fact]
    public void ScalarVariants_Prepare()
    {
        using var ctx = SqlServerTestContext.Create();

        ClearScalarCache();
        var nullableLeft = ctx.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => (int?)c.Id);
        var nullableRight = ctx.From<IComplexEntity>().Where(c => c.Id >= 2 && c.Id <= 3).Select(c => (int?)c.Id);
        SqlOf(ctx, ctx.From(nullableLeft.UnionAll(nullableRight)).ToCommand())
            .Should().Contain("union all", "a nullable scalar derived source prepares as a scalar");
        DataContextCache.SelectListCache.ContainsKey(typeof(int?))
            .Should().BeFalse("the nullable scalar branch must not write the type-keyed cache");

        ClearScalarCache();
        var stringLeft = ctx.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => c.String);
        var stringRight = ctx.From<IComplexEntity>().Where(c => c.Id >= 2 && c.Id <= 3).Select(c => c.String);
        SqlOf(ctx, ctx.From(stringLeft.UnionAll(stringRight)).ToCommand())
            .Should().Contain("union all", "a reference scalar derived source prepares as a scalar");
        DataContextCache.SelectListCache.ContainsKey(typeof(string))
            .Should().BeFalse("the reference scalar branch must not write the type-keyed cache");
    }

    /// <summary>
    /// AC4 negative: a multi-column derived source is not a scalar shape and keeps the existing
    /// actionable refusal; the new branch must not capture it.
    /// </summary>
    [Fact]
    public void MultiColumnDerivedSource_StaysRefused()
    {
        using var ctx = SqlServerTestContext.Create();
        ClearScalarCache();
        var inner = ctx.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => new { c.Id, c.Int });

        var act = () => SqlOf(ctx, ctx.From(inner).ToCommand());

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*derived source without an explicit outer Select*");
    }

    /// <summary>AC4 control: a direct mapped scalar read keeps preparing without the derived branch.</summary>
    [Fact]
    public void DirectScalarAndMappedReads_RemainValid()
    {
        using var ctx = SqlServerTestContext.Create();
        ClearScalarCache();

        SqlOf(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id <= 2).Select(x => x.Id))
            .Should().Contain("select");
        SqlOf(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id <= 2).Select(x => new { x.Id }))
            .Should().Contain("select");
    }
}
