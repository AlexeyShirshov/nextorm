using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// A captured collection indexed by a query column (<c>dict[column]</c>) renders a portable
/// <c>CASE WHEN</c>; a constant key folds to a parameter. No database connection is opened.
/// </summary>
public class DictionaryLookupSqlGenerationTests
{
    [Fact]
    public void Dictionary_IndexedByColumn_ShouldEmitCase()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var lookup = new Dictionary<int, DateTime> { [1] = new DateTime(2024, 1, 1), [2] = new DateTime(2024, 2, 1) };

        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(x => lookup[x.Int!.Value] < x.Datetime).Select(x => x.Id), false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("case when nullableint = @p0 then @p1 when nullableint = @p2 then @p3 end");
    }

    [Fact]
    public void Dictionary_IndexedByConstant_ShouldFoldToParameter()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var lookup = new Dictionary<int, DateTime> { [2] = new DateTime(2024, 2, 1) };

        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(x => lookup[2] < x.Datetime).Select(x => x.Id), false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Should().NotContain("case");
        prepared.DbCommandParams[0].Value.Should().Be(new DateTime(2024, 2, 1));
    }
}
