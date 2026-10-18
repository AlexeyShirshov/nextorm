using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// #275 compatibility guard: MySQL/MariaDB inherit the default <c>MakeMaxRecursion</c> (<c>null</c>), so a
/// recursive CTE hoisted into an INSERT…SELECT or a CTAS body must not gain an
/// <c>option (maxrecursion …)</c> fragment.
/// </summary>
public class T275MaxRecursionHoistTests
{
    private static Cte<int> Recursive(IDataContext ctx)
        => ctx.From<ISimpleEntity>().Where(s => s.Id == 1).Select(s => s.Id)
            .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1), 7);

    [Fact]
    public void MySql_RecursiveMutationSource_SqlUnchanged()
    {
        using var ctx = MySqlTestContext.Create();

        var insertSql = ctx.CreateInsertBuilder<ISimpleEntity>()
            .Values(ctx.From(Recursive(ctx)), n => new { Id = n })
            .ToSql();

        insertSql.Should().StartWith("with recursive nums as (").And.NotContain("maxrecursion");
    }

    [Fact]
    public void MySql_RecursiveCtas_SqlUnchanged()
    {
        using var ctx = MySqlTestContext.Create();

        var ctasSql = ctx.From(Recursive(ctx)).Select(n => new { Id = n }).ToTableSql("t275_archive");

        ctasSql.Should().StartWith("create table t275_archive as with recursive nums as (").And.NotContain("maxrecursion");
    }
}
