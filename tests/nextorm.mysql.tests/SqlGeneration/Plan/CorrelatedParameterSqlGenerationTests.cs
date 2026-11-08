using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// SQL generation for two engine behaviours that share <c>PredicateTranslator</c> and
/// <c>CorrelatedQueryExpressionVisitor</c>: a captured local repeated in a <c>WHERE</c> over a join
/// projection is registered once, and a correlated <c>EXISTS</c> can be combined with the logical
/// operators <c>||</c>, <c>&amp;&amp;</c> and <c>!</c>. These tests never open a database connection.
/// </summary>
public class CorrelatedParameterSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void CapturedLocalRepeatedInJoinWhere_ShouldBindOnce()
    {
        using var ctx = MySqlTestContext.Create();
        var v = 5;

        var prepared = Prepare(ctx, ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Where(p => p.Item1.Int == v && p.Item2.Id != v)
            .Select(p => new { p.Item1.Id }));

        var parameters = prepared.DbCommand.Parameters.Cast<DbParameter>().ToArray();
        parameters.Should().ContainSingle();
        parameters[0].ParameterName.Should().Be("v");
        parameters[0].Value.Should().Be(5);
        Normalize(prepared.DbCommand.CommandText).Should().Contain("@v");
    }

    [Fact]
    public void CorrelatedExistsCombinedWithLogicalOperators_ShouldRenderPredicates()
    {
        using var ctx = MySqlTestContext.Create();
        var inner = ctx.From<ISimpleEntity>();

        var orSql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Where(c => SqlFunctions.Sql.exists(inner.Where(s => s.Id == c.Int)) || c.Id > 0)
            .Select(c => new { c.Id }));

        orSql.Should().Contain("exists");
        orSql.Should().Contain(" or ");

        var andSql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Where(c => c.Id > 0 && SqlFunctions.Sql.exists(inner.Where(s => s.Id == c.Int)))
            .Select(c => new { c.Id }));

        andSql.Should().Contain(" and ");
        andSql.Should().Contain("exists");

        var notSql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Where(c => !SqlFunctions.Sql.exists(inner.Where(s => s.Id == c.Int)))
            .Select(c => new { c.Id }));

        notSql.Should().Contain("not (exists");
    }
}
