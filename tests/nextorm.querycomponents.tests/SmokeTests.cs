using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Core;
using NextORM.QueryComponents;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.QueryComponents.Tests;

public class SmokeTests
{
    private sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public int rn { get; set; }
        public int rank { get; set; }
        public int __nextorm_ext_rn { get; set; }
    }

    [Fact]
    public void Core_DoesNot_GrantInternalsVisibleTo_QueryComponents()
    {
        var granted = typeof(IDataContext).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .ToArray();

        granted.Should().NotContain(name => name == "nextorm.querycomponents" || name == "nextorm.querycomponents.tests");
    }

    [Fact]
    public void ExternalMin_GeneratesRankWindow_AndRestoresProjection()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        var cmd = e.ExternalSelectWhereMin(x => x.Score).Select(x => new { x.Id, x.Name });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt!);

        sql.Should().Contain("row_number()");
        sql.Should().Contain("__nextorm_ext_rn");
        sql.Should().Contain("= 1");
        sql.Should().NotContain("rn = 1");
    }

    [Fact]
    public void BuildQuery_RunsOnce_OnFirstPrepare_AndZeroOnWarmHit()
    {
        ExternalSelectWhereComponent<Entity>.ResetBuildCount();
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        var cmd = e.ExternalSelectWhereMin(x => x.Score).ToCommand();
        _ = Prepare(ctx, cmd);
        var first = ExternalSelectWhereComponent<Entity>.BuildCount;
        _ = Prepare(ctx, cmd);
        var second = ExternalSelectWhereComponent<Entity>.BuildCount;

        first.Should().Be(1);
        second.Should().Be(1);
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");
}

