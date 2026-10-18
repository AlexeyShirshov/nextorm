using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #275 compatibility guard: SQLite inherits the default <c>MakeMaxRecursion</c> (<c>null</c>), so a
/// recursive CTE hoisted into an INSERT…SELECT or a CTAS body must keep its SQL byte-identical — no
/// <c>option (maxrecursion …)</c> fragment can appear. Baselines captured before the fix
/// (`.pdca/D275/rv1/baseline/sqlite-*.txt`).
/// </summary>
public class T275MaxRecursionHoistTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static Cte<int> Recursive(IDataContext ctx)
        => ctx.From<ISimpleEntity>().Where(s => s.Id == 1).Select(s => s.Id)
            .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1), 7);

    [Fact]
    public void Sqlite_RecursiveMutationSource_SqlUnchanged()
    {
        using var ctx = SqliteTestContext.Create();

        var insertSql = Normalize(ctx.CreateInsertBuilder<ISimpleEntity>()
            .Values(ctx.From(Recursive(ctx)), n => new { Id = n })
            .ToSql());

        insertSql.Should().StartWith("with recursive nums as (").And.NotContain("maxrecursion");
        insertSql.Should().Be("with recursive nums as (select id as 'Id'\n from simple_entity\n where id = 1\n union all \nselect (Id + 1) as 'c0'\n from nums as 't1'\n where (t1.Id < 5)) insert into simple_entity (id) select Id\n from nums as 't1'");
    }

    [Fact]
    public void Sqlite_RecursiveCtas_SqlUnchanged()
    {
        using var ctx = SqliteTestContext.Create();

        var ctasSql = Normalize(ctx.From(Recursive(ctx)).Select(n => new { Id = n }).ToTableSql("t275_archive"));

        ctasSql.Should().StartWith("create table t275_archive as with recursive nums as (").And.NotContain("maxrecursion");
        ctasSql.Should().Be("create table t275_archive as with recursive nums as (select id as 'Id'\n from simple_entity\n where id = 1\n union all \nselect (Id + 1) as 'c0'\n from nums as 't1'\n where (t1.Id < 5)) select Id\n from nums as 't1'");
    }
}
