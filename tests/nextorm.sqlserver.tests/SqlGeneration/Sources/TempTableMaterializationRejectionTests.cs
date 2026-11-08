using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// #162 boundary regression for SQL Server: the dialect cannot express a temporary
/// <c>CREATE TABLE ... AS SELECT</c> (<see cref="ISqlDialect.SupportsTemporaryCreateTableAsSelect"/> is
/// <see langword="false"/>), so materialising a lazy temp-table source is rejected with
/// <see cref="NotSupportedException"/> instead of silently rendering a persistent table.
/// </summary>
[Trait("D162", "Boundary")]
public class TempTableMaterializationRejectionTests
{
    [Fact]
    public void Temporary_temp_table_batch_should_throw_not_supported()
    {
        using var ctx = SqlServerTestContext.Create();

        var source = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();

        var act = () => ctx.From(source).Select(t => new { Id = t.GetInt32("id") }).ToBatchSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*temporary table*");
    }
}
