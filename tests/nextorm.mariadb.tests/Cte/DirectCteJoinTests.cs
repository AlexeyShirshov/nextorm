using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// #159 direct <c>Cte&lt;T&gt;</c> join SQL generation on MariaDB: a descriptor passed straight to
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

    // Runs the whole prepare pipeline (build + SQL compilation) so the declared outcome covers the
    // stage the exception is promised at, instead of reading the expectation from the reference call.
    private static (string? Sql, object?[]? Parameters, Exception? Error) TryPrepare<T>(IDataContext ctx, Func<QueryCommand<T>> build)
    {
        try
        {
            var (sql, parameters) = Prepare(ctx, build());
            return (sql, parameters, null);
        }
        catch (Exception ex)
        {
            return (null, null, ex);
        }
    }

    // The outcome is declared per case: a null marker means MariaDB supports the operator and both
    // forms must prepare and render the same SQL. A non-null marker is the exact message MariaDB
    // documents for the refusal and is asserted independently on both forms.
    [Theory]
    [InlineData("Join", null)]
    [InlineData("LeftJoin", null)]
    [InlineData("RightJoin", null)]
    [InlineData("FullJoin", "The Full join is not supported by this SQL dialect")]
    [InlineData("CrossJoin", null)]
    [InlineData("CrossApply", "The CrossApply join is not supported by this SQL dialect")]
    [InlineData("OuterApply", "The OuterApply join is not supported by this SQL dialect")]
    public void Direct_typed_cte_join_matches_the_converted_form(string operation, string? expectedRefusal)
    {
        using var ctx = MariaDbTestContext.Create();

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

        var direct = TryPrepare(ctx, Direct);
        var converted = TryPrepare(ctx, Converted);

        if (expectedRefusal is null)
        {
            direct.Error.Should().BeNull("MariaDB declares {0} supported, so the direct form must prepare", operation);
            converted.Error.Should().BeNull("MariaDB declares {0} supported, so the converted form must prepare", operation);
            direct.Sql.Should().Be(converted.Sql, "the direct CTE form must render the declared SQL of the converted form");
            direct.Parameters.Should().Equal(converted.Parameters, "both forms must bind the same parameters");
            direct.Sql.Should().Contain("with l as (").And.Contain("r as (", "the rendered SQL must keep both declared CTEs");
            return;
        }

        direct.Error.Should().BeOfType<NotSupportedException>("MariaDB declares {0} a capability refusal", operation);
        converted.Error.Should().BeOfType<NotSupportedException>("the converted form must hit the same declared refusal");
        direct.Error!.Message.Should().Contain(expectedRefusal, "the direct form must carry the declared capability marker");
        converted.Error!.Message.Should().Contain(expectedRefusal, "the converted form must carry the declared capability marker");
    }
}
