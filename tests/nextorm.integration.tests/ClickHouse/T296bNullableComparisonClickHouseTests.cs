using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T296b (#296, D4): the same live truth tables as the shared suite, executed against a real
/// ClickHouse server. ClickHouse has no <see cref="CommonTestSuite"/> subclass (and this task may not
/// invent one), so the shared assertion bodies are re-pinned here with ClickHouse-native DDL
/// (<c>Nullable(Int32)</c>/<c>Nullable(String)</c>). The RIGHT JOIN form is outside the approved live
/// scope and the shared body records it as not applicable rather than claiming it.
/// </summary>
public sealed class T296bNullableComparisonClickHouseTests : IDisposable
{
    private readonly IDataContext _ctx;
    private readonly ITestOutputHelper _output;

    public T296bNullableComparisonClickHouseTests(ITestOutputHelper output)
    {
        _output = output;
        Assert.SkipUnless(ClickHouseContainer.IsAvailable, ClickHouseContainer.Failure ?? "ClickHouse is not available.");

        _ctx = new ClickHouseDataContext(ClickHouseContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void T296bCapturedNullableComparison_ShouldMatchTheTruthTable()
        => CommonTestSuite.AssertT296bCapturedComparisonTruthTable(_ctx, "clickhouse", Evidence);

    [Fact]
    public void T296bMappedHasValue_ShouldMatchTheTruthTable()
        => CommonTestSuite.AssertT296bMappedHasValueTruthTable(_ctx, "clickhouse", Evidence);

    [Fact]
    public void T296bCorrelatedExists_ShouldKeepOuterAssociation()
        => CommonTestSuite.AssertT296bCorrelatedExistsAssociation(_ctx, "clickhouse", Evidence);

    [Fact]
    public void T296bBooleanSelect_ShouldMaterialiseNonNullableBool()
        => CommonTestSuite.AssertT296bBooleanSelectMaterialisation(_ctx, "clickhouse", Evidence);

    [Fact]
    public void T296bJoinOn_ShouldKeepMultiplicityAndOuterRows()
        => CommonTestSuite.AssertT296bJoinOnTruthTable(_ctx, "clickhouse", Evidence);

    private void Seed()
    {
        Execute("drop table if exists t296b_child");
        Execute("drop table if exists t296b_parent");
        Execute("drop table if exists t296b_rows");

        Execute("create table t296b_rows (id Int64, n Nullable(Int32), s Nullable(String)) engine = Memory");
        Execute(
            "insert into t296b_rows (id, n, s) values " +
            "(1, null, null), (2, 10, 'alpha'), (3, 20, 'beta'), (4, 10, 'alpha'), (5, 30, null), (6, null, 'gamma')");

        Execute("create table t296b_parent (id Int64, label Nullable(String)) engine = Memory");
        Execute("insert into t296b_parent (id, label) values (1, 'one'), (2, 'two'), (3, 'three'), (4, 'four')");

        Execute(
            "create table t296b_child (id Int64, parent_id Int64, amount Nullable(Int32), tag Nullable(String)) " +
            "engine = Memory");
        Execute(
            "insert into t296b_child (id, parent_id, amount, tag) values " +
            "(11, 1, null, null), (12, 1, 5, 'x'), (21, 2, 42, 'y'), (22, 2, 42, 'y'), (31, 3, null, null)");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    private void Log(string message) => _output.WriteLine(message);

    private Action<string> Evidence => CommonTestSuite.T296bEvidenceLogger(Log);
}
