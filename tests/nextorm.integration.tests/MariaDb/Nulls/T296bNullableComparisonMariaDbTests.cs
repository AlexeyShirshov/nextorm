using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T296b (#296, D4): the live truth tables for captured nullable/string comparisons, mapped
/// <c>HasValue</c>, correlated <c>Exists</c> association, boolean SELECT materialisation and JOIN ON
/// multiplicity, executed against a real MariaDB server. MariaDB has no <see cref="CommonTestSuite"/>
/// subclass (and this task may not invent one), so it re-pins the shared assertion bodies against its
/// own container harness. A skip is never a pass: when the container is unavailable the constructor
/// reports the reason and the provider row stays unproven.
/// </summary>
public sealed class T296bNullableComparisonMariaDbTests : IDisposable
{
    private readonly IDataContext _ctx;
    private readonly ITestOutputHelper _output;

    public T296bNullableComparisonMariaDbTests(ITestOutputHelper output)
    {
        _output = output;
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        CommonTestSuite.SeedT296bFixture(_ctx);
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void T296bCapturedNullableComparison_ShouldMatchTheTruthTable()
        => CommonTestSuite.AssertT296bCapturedComparisonTruthTable(_ctx, "mariadb", Evidence);

    [Fact]
    public void T296bMappedHasValue_ShouldMatchTheTruthTable()
        => CommonTestSuite.AssertT296bMappedHasValueTruthTable(_ctx, "mariadb", Evidence);

    [Fact]
    public void T296bCorrelatedExists_ShouldKeepOuterAssociation()
        => CommonTestSuite.AssertT296bCorrelatedExistsAssociation(_ctx, "mariadb", Evidence);

    [Fact]
    public void T296bBooleanSelect_ShouldMaterialiseNonNullableBool()
        => CommonTestSuite.AssertT296bBooleanSelectMaterialisation(_ctx, "mariadb", Evidence);

    [Fact]
    public void T296bJoinOn_ShouldKeepMultiplicityAndOuterRows()
        => CommonTestSuite.AssertT296bJoinOnTruthTable(_ctx, "mariadb", Evidence);

    private void Log(string message) => _output.WriteLine(message);

    private Action<string> Evidence => CommonTestSuite.T296bEvidenceLogger(Log);
}
