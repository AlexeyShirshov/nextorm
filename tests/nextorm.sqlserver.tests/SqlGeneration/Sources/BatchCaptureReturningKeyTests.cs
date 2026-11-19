using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests.SqlGeneration;

/// <summary>
/// The batch capture path with a returning terminal that carries <em>no</em> selector:
/// <c>ReturningKey&lt;TKey&gt;()</c> makes the read shape reconstruct the single key member access at
/// build time, which is a different branch from the selector-based capture covered elsewhere. The test
/// builds and prepares the batch (no connection is opened) and pins the rendered statement, so the branch
/// cannot regress silently.
/// </summary>
public sealed class BatchCaptureReturningKeyTests
{
    [Fact]
    public void ReturningKey_WithOutputCapture_ShouldRenderTheKeyColumnIntoTheTableVariable()
    {
        using var ctx = SqlServerTestContext.Create();

        var mutation = ctx.CreateInsertBuilder<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningKey<long>();

        var batch = ctx.CreateBatchBuilder()
            .OutputIntoTableVariable(mutation, "@t", "id bigint");

        var read = batch.FromOutput<long>("@t")
            .Select(r => r);

        var sql = batch.Query(read).ToSql().ToLowerInvariant();

        sql.Should().Contain("declare @t table (id bigint)");
        sql.Should().Contain("output inserted.id into @t (id)");
        sql.Should().Contain("from @t");
    }
}
