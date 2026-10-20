using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// #159 direct <c>Cte&lt;T&gt;</c> join SQL generation on MySQL: a descriptor passed straight to
/// the seven join operators must render the same SQL and bind the same parameters as converting it
/// with the receiving context first (<c>ctx.From(cte)</c>). Unsupported operator/provider pairs must
/// retain the exact full-form rejection.
/// </summary>
public class DirectCteJoinTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static (string Sql, object?[] Parameters) Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        var parameters = prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).ToArray();
        return (Normalize(prepared.DbCommand.CommandText), parameters);
    }

    [Theory]
    [InlineData("Join")]
    [InlineData("LeftJoin")]
    [InlineData("RightJoin")]
    [InlineData("FullJoin")]
    [InlineData("CrossJoin")]
    [InlineData("CrossApply")]
    [InlineData("OuterApply")]
    public void Direct_typed_cte_join_matches_the_converted_form(string operation)
    {
        using var ctx = MySqlTestContext.Create();

        var minId = 1L;
        var left = ctx.From<IComplexEntity>().Where(c => c.Id > minId).Select(c => new { c.Id }).AsCte("l");
        var right = ctx.From<IComplexEntity>().Select(c => new { c.Id, c.Int }).AsCte("r");

        QueryCommand<long> Direct() => operation switch
        {
            "Join" => ctx.From(left).Join(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "LeftJoin" => ctx.From(left).LeftJoin(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "RightJoin" => ctx.From(left).RightJoin(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "FullJoin" => ctx.From(left).FullJoin(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "CrossJoin" => ctx.From(left).CrossJoin(right).Select(p => p.Item1.Id),
            "CrossApply" => ctx.From(left).CrossApply(right).Select(p => p.Item1.Id),
            "OuterApply" => ctx.From(left).OuterApply(right).Select(p => p.Item1.Id),
            _ => throw new NotSupportedException(),
        };
        QueryCommand<long> Converted() => operation switch
        {
            "Join" => ctx.From(left).Join(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "LeftJoin" => ctx.From(left).LeftJoin(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "RightJoin" => ctx.From(left).RightJoin(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "FullJoin" => ctx.From(left).FullJoin(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "CrossJoin" => ctx.From(left).CrossJoin(ctx.From(right)).Select(p => p.Item1.Id),
            "CrossApply" => ctx.From(left).CrossApply(ctx.From(right)).Select(p => p.Item1.Id),
            "OuterApply" => ctx.From(left).OuterApply(ctx.From(right)).Select(p => p.Item1.Id),
            _ => throw new NotSupportedException(),
        };

        var convertedEx = Record.Exception(() => Normalize(((DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(Converted(), false, false, CancellationToken.None)).DbCommand.CommandText));
        if (convertedEx is null)
        {
            var direct = Prepare(ctx, Direct());
            var converted = Prepare(ctx, Converted());

            direct.Sql.Should().Be(converted.Sql);
            direct.Parameters.Should().Equal(converted.Parameters);
            direct.Sql.Should().Contain("with l as (").And.Contain("r as (");
        }
        else
        {
            var directEx = Record.Exception(() => Prepare(ctx, Direct()));
            directEx.Should().BeOfType(convertedEx.GetType());
            directEx!.Message.Should().Be(convertedEx.Message);
        }
    }
}
