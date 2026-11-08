using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T292 (#292) SQLite arm: the four variability families (<c>Stdev</c>/<c>Stdevp</c>/<c>Var</c>/
/// <c>Varp</c>) over a real SQLite database, in the correlated projection and in the scalar,
/// asynchronous and SQL-facade forms.
/// </summary>
/// <remarks>
/// <para>
/// Cardinality counts contributing non-null values after DISTINCT. Zero contributing rows are
/// undefined for all four families; the sample flavours are undefined for one contributing value;
/// the population flavours stay defined and fold to zero. Before the fix the projection member was
/// typed <c>double</c>, so a SQL NULL reached the unguarded reader getter and threw, while the scalar
/// terminal silently returned the invented zero.
/// </para>
/// <para>
/// The values 1, 2, 3 of the three-row parent pin the established formulas: sample variance 1,
/// population variance 2/3, so <c>Stdev</c> = 1 and <c>Stdevp</c> = sqrt(2/3). Every fact states the
/// post-fix contract.
/// </para>
/// </remarks>
[Collection("Sqlite")]
public sealed class SqliteT292VariabilityNullTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    /// <summary>Parent with zero child rows: the aggregate sees an empty set.</summary>
    private const int EmptyParent = 29200;

    /// <summary>Parent with exactly one child row (value 4): an undefined sample, a defined population.</summary>
    private const int SingleParent = 29201;

    /// <summary>Parent with three child rows (values 1, 2, 3): the established multi-row result.</summary>
    private const int TripleParent = 29202;

    /// <summary>Parent with two physical rows whose nullable value is null in both.</summary>
    private const int AllNullParent = 29203;

    /// <summary>Parent with two physical rows, exactly one of them non-null (5).</summary>
    private const int MixedParent = 29204;

    /// <summary>Parent with three physical rows all carrying the value 2.</summary>
    private const int DuplicateParent = 29205;

    private const string Ddl = """
        drop table if exists t292_child;
        drop table if exists t292_parent;
        create table t292_parent
        (
            id int primary key,
            name text not null
        );
        create table t292_child
        (
            id int primary key,
            parent_id int not null,
            val real not null,
            opt real null
        );
        insert into t292_parent (id, name) values
            (29200, 'empty'),
            (29201, 'single'),
            (29202, 'triple'),
            (29203, 'all-null'),
            (29204, 'mixed'),
            (29205, 'duplicate');
        insert into t292_child (id, parent_id, val, opt) values
            (29210, 29201, 4, 4),
            (29220, 29202, 1, 1),
            (29221, 29202, 2, 2),
            (29222, 29202, 3, 3),
            (29230, 29203, 7, null),
            (29231, 29203, 8, null),
            (29240, 29204, 8, null),
            (29241, 29204, 9, 5),
            (29250, 29205, 2, 2),
            (29251, 29205, 2, 2),
            (29252, 29205, 2, 2);
        """;

    public SqliteT292VariabilityNullTests()
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(Ddl);
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    private EntityBuilder<T292Child> Children(int parentId)
        => Context.From<T292Child>().Where(c => c.ParentId == parentId);

    // ---------------------------------------------------------------------------------------
    // Correlated projection, n = 0 contributing rows: undefined for every family.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Stdev_ProjectionZeroContributingRows_ShouldBeNull()
    {
        var rows = Context.From<T292Parent>()
            .Where(p => p.Id == EmptyParent)
            .Select(p => new
            {
                p.Id,
                V = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Stdev(c => c.Value),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("a sample standard deviation over zero contributing rows is undefined (#292)");
    }

    [Fact]
    public void Stdevp_ProjectionZeroContributingRows_ShouldBeNull()
    {
        var rows = Context.From<T292Parent>()
            .Where(p => p.Id == EmptyParent)
            .Select(p => new
            {
                p.Id,
                V = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Stdevp(c => c.Value),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("a population standard deviation over zero contributing rows is undefined (#292)");
    }

    [Fact]
    public void Var_ProjectionZeroContributingRows_ShouldBeNull()
    {
        var rows = Context.From<T292Parent>()
            .Where(p => p.Id == EmptyParent)
            .Select(p => new
            {
                p.Id,
                V = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Var(c => c.Value),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("a sample variance over zero contributing rows is undefined (#292)");
    }

    [Fact]
    public void Varp_ProjectionZeroContributingRows_ShouldBeNull()
    {
        var rows = Context.From<T292Parent>()
            .Where(p => p.Id == EmptyParent)
            .Select(p => new
            {
                p.Id,
                V = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Varp(c => c.Value),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("a population variance over zero contributing rows is undefined (#292)");
    }

    // ---------------------------------------------------------------------------------------
    // Correlated projection, n = 1: the sample flavours are undefined, the population ones zero.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Stdev_ProjectionSingleContributingRow_ShouldBeNull()
    {
        var rows = Context.From<T292Parent>()
            .Where(p => p.Id == SingleParent)
            .Select(p => new
            {
                p.Id,
                V = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Stdev(c => c.Value),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("a one-row sample has no sample deviation (#292)");
    }

    [Fact]
    public void Var_ProjectionSingleContributingRow_ShouldBeNull()
    {
        var rows = Context.From<T292Parent>()
            .Where(p => p.Id == SingleParent)
            .Select(p => new
            {
                p.Id,
                V = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Var(c => c.Value),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("a one-row sample has no sample variance (#292)");
    }

    [Fact]
    public void Stdevp_ProjectionSingleContributingRow_ShouldBeZero()
    {
        var rows = Context.From<T292Parent>()
            .Where(p => p.Id == SingleParent)
            .Select(p => new
            {
                p.Id,
                V = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Stdevp(c => c.Value),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().Be(0.0, "a one-row population deviation is defined and stays zero (AC04)");
    }

    [Fact]
    public void Varp_ProjectionSingleContributingRow_ShouldBeZero()
    {
        var rows = Context.From<T292Parent>()
            .Where(p => p.Id == SingleParent)
            .Select(p => new
            {
                p.Id,
                V = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Varp(c => c.Value),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().Be(0.0, "a one-row population variance is defined and stays zero (AC04)");
    }

    // ---------------------------------------------------------------------------------------
    // Correlated projection, n = 3 (values 1, 2, 3): established results, nullable-selector arm.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void VariabilityAggregates_ProjectionMultipleContributingRows_ShouldReturnEstablishedValues()
    {
        var rows = Context.From<T292Parent>()
            .Where(p => p.Id == TripleParent)
            .Select(p => new
            {
                p.Id,
                Stdev = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Stdev(c => c.Value),
                Stdevp = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Stdevp(c => c.Value),
                Var = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Var(c => c.Value),
                Varp = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Varp(c => c.Value),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Stdev.Should().BeApproximately(1.0, 1e-12, "the sample deviation of 1, 2, 3 is 1");
        rows[0].Stdevp.Should().BeApproximately(0.816496580927726, 1e-12, "sqrt(2/3)");
        rows[0].Var.Should().BeApproximately(1.0, 1e-12, "the sample variance of 1, 2, 3 is 1");
        rows[0].Varp.Should().BeApproximately(2.0 / 3.0, 1e-12);
    }

    [Fact]
    public void Stdev_ProjectionNullableSelectorSingleNonNull_ShouldBeNull()
    {
        var rows = Context.From<T292Parent>()
            .Where(p => p.Id == MixedParent)
            .Select(p => new
            {
                p.Id,
                V = Context.From<T292Child>().Where(c => c.ParentId == p.Id).Stdev(c => c.OptionalValue),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("one non-null value across two physical rows is an undefined sample (AC06)");
    }

    // ---------------------------------------------------------------------------------------
    // Scalar terminal form.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Stdev_TerminalZeroContributingRows_ShouldBeNull()
    {
        Children(EmptyParent).Stdev(c => c.Value)
            .Should().BeNull("an undefined sample deviation must be null, never an invented zero (AC03)");
    }

    [Fact]
    public void Stdevp_TerminalZeroContributingRows_ShouldBeNull()
    {
        Children(EmptyParent).Stdevp(c => c.Value)
            .Should().BeNull("an undefined population deviation must be null, never an invented zero (AC03)");
    }

    [Fact]
    public void Var_TerminalZeroContributingRows_ShouldBeNull()
    {
        Children(EmptyParent).Var(c => c.Value)
            .Should().BeNull("an undefined sample variance must be null, never an invented zero (AC03)");
    }

    [Fact]
    public void Varp_TerminalZeroContributingRows_ShouldBeNull()
    {
        Children(EmptyParent).Varp(c => c.Value)
            .Should().BeNull("an undefined population variance must be null, never an invented zero (AC03)");
    }

    [Fact]
    public void VariabilityAggregates_TerminalSingleContributingRow_ShouldMatchCardinality()
    {
        Children(SingleParent).Stdev(c => c.Value)
            .Should().BeNull("a one-row sample has no sample deviation (AC04)");
        Children(SingleParent).Var(c => c.Value)
            .Should().BeNull("a one-row sample has no sample variance (AC04)");
        Children(SingleParent).Stdevp(c => c.Value).Should().Be(0.0);
        Children(SingleParent).Varp(c => c.Value).Should().Be(0.0);
    }

    [Fact]
    public void VariabilityAggregates_TerminalMultipleContributingRows_ShouldReturnEstablishedValues()
    {
        Children(TripleParent).Stdev(c => c.Value).Should().BeApproximately(1.0, 1e-12);
        Children(TripleParent).Stdevp(c => c.Value).Should().BeApproximately(0.816496580927726, 1e-12);
        Children(TripleParent).Var(c => c.Value).Should().BeApproximately(1.0, 1e-12);
        Children(TripleParent).Varp(c => c.Value).Should().BeApproximately(2.0 / 3.0, 1e-12);
    }

    // ---------------------------------------------------------------------------------------
    // Nullable selectors: null inputs do not contribute to the cardinality (AC06).
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void VariabilityAggregates_TerminalNullableSelectorAllNull_ShouldBeNull()
    {
        Children(AllNullParent).Stdev(c => c.OptionalValue).Should().BeNull();
        Children(AllNullParent).Stdevp(c => c.OptionalValue).Should().BeNull();
        Children(AllNullParent).Var(c => c.OptionalValue).Should().BeNull();
        Children(AllNullParent).Varp(c => c.OptionalValue).Should().BeNull();
    }

    [Fact]
    public void VariabilityAggregates_TerminalNullableSelectorSingleNonNull_ShouldMatchCardinality()
    {
        Children(MixedParent).Stdev(c => c.OptionalValue)
            .Should().BeNull("two physical rows with one non-null value remain an undefined sample (AC06)");
        Children(MixedParent).Var(c => c.OptionalValue)
            .Should().BeNull("two physical rows with one non-null value remain an undefined sample (AC06)");
        Children(MixedParent).Stdevp(c => c.OptionalValue).Should().Be(0.0);
        Children(MixedParent).Varp(c => c.OptionalValue).Should().Be(0.0);
    }

    // ---------------------------------------------------------------------------------------
    // Asynchronous twins: the params shape and the CancellationToken shape.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task VariabilityAggregates_TerminalAsyncParamsShapeZeroContributingRows_ShouldBeNull()
    {
        (await Children(EmptyParent).StdevAsync(c => c.Value)).Should().BeNull();
        (await Children(EmptyParent).StdevpAsync(c => c.Value)).Should().BeNull();
        (await Children(EmptyParent).VarAsync(c => c.Value)).Should().BeNull();
        (await Children(EmptyParent).VarpAsync(c => c.Value)).Should().BeNull();
    }

    [Fact]
    public async Task VariabilityAggregates_TerminalAsyncCancellationTokenShapeZeroContributingRows_ShouldBeNull()
    {
        using var cts = new CancellationTokenSource();

        (await Children(EmptyParent).StdevAsync(c => c.Value, cts.Token)).Should().BeNull();
        (await Children(EmptyParent).StdevpAsync(c => c.Value, cts.Token)).Should().BeNull();
        (await Children(EmptyParent).VarAsync(c => c.Value, cts.Token)).Should().BeNull();
        (await Children(EmptyParent).VarpAsync(c => c.Value, cts.Token)).Should().BeNull();
    }

    [Fact]
    public async Task VariabilityAggregates_TerminalAsyncSingleContributingRow_ShouldMatchCardinality()
    {
        using var cts = new CancellationTokenSource();

        (await Children(SingleParent).StdevAsync(c => c.Value, cts.Token)).Should().BeNull();
        (await Children(SingleParent).VarAsync(c => c.Value, cts.Token)).Should().BeNull();
        (await Children(SingleParent).StdevpAsync(c => c.Value, cts.Token)).Should().Be(0.0);
        (await Children(SingleParent).VarpAsync(c => c.Value, cts.Token)).Should().Be(0.0);
    }

    [Fact]
    public async Task VariabilityAggregates_TerminalAsyncMultipleContributingRows_ShouldReturnEstablishedValues()
    {
        (await Children(TripleParent).StdevAsync(c => c.Value)).Should().BeApproximately(1.0, 1e-12);
        (await Children(TripleParent).StdevpAsync(c => c.Value)).Should().BeApproximately(0.816496580927726, 1e-12);
        (await Children(TripleParent).VarAsync(c => c.Value)).Should().BeApproximately(1.0, 1e-12);
        (await Children(TripleParent).VarpAsync(c => c.Value)).Should().BeApproximately(2.0 / 3.0, 1e-12);
    }

    [Fact]
    public async Task VariabilityAggregates_TerminalAsyncNullableSelector_ShouldIgnoreNulls()
    {
        using var cts = new CancellationTokenSource();

        (await Children(AllNullParent).StdevAsync(c => c.OptionalValue, cts.Token)).Should().BeNull();
        (await Children(AllNullParent).VarAsync(c => c.OptionalValue, cts.Token)).Should().BeNull();
        (await Children(MixedParent).StdevpAsync(c => c.OptionalValue)).Should().Be(0.0);
        (await Children(MixedParent).VarpAsync(c => c.OptionalValue)).Should().Be(0.0);
    }

    // ---------------------------------------------------------------------------------------
    // The eight SQL facade forms, including DISTINCT: semantics and duplicate collapsing.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void VariabilityAggregates_FacadeFormsMultipleContributingRows_ShouldReturnEstablishedValues()
    {
        Children(TripleParent).Select(c => SqlFunctions.Sql.stdev(c.Value)).First()
            .Should().BeApproximately(1.0, 1e-12);
        Children(TripleParent).Select(c => SqlFunctions.Sql.stdevp(c.Value)).First()
            .Should().BeApproximately(0.816496580927726, 1e-12);
        Children(TripleParent).Select(c => SqlFunctions.Sql.var(c.Value)).First()
            .Should().BeApproximately(1.0, 1e-12);
        Children(TripleParent).Select(c => SqlFunctions.Sql.varp(c.Value)).First()
            .Should().BeApproximately(2.0 / 3.0, 1e-12);
        Children(TripleParent).Select(c => SqlFunctions.Sql.stdev_distinct(c.Value)).FirstOrDefault()
            .Should().BeApproximately(1.0, 1e-12);
        Children(TripleParent).Select(c => SqlFunctions.Sql.stdevp_distinct(c.Value)).FirstOrDefault()
            .Should().BeApproximately(0.816496580927726, 1e-12);
        Children(TripleParent).Select(c => SqlFunctions.Sql.var_distinct(c.Value)).FirstOrDefault()
            .Should().BeApproximately(1.0, 1e-12);
        Children(TripleParent).Select(c => SqlFunctions.Sql.varp_distinct(c.Value)).FirstOrDefault()
            .Should().BeApproximately(2.0 / 3.0, 1e-12);
    }

    [Fact]
    public void VariabilityAggregates_FacadeDistinctFormsDuplicates_ShouldCollapseToOneContribution()
    {
        Children(DuplicateParent).Select(c => SqlFunctions.Sql.stdev_distinct(c.Value)).FirstOrDefault()
            .Should().BeNull("three duplicate 2s collapse to one distinct contribution, an undefined sample");
        Children(DuplicateParent).Select(c => SqlFunctions.Sql.var_distinct(c.Value)).FirstOrDefault()
            .Should().BeNull("three duplicate 2s collapse to one distinct contribution, an undefined sample");
        Children(DuplicateParent).Select(c => SqlFunctions.Sql.stdevp_distinct(c.Value)).FirstOrDefault()
            .Should().Be(0.0, "a population deviation over one distinct contribution is defined and zero");
        Children(DuplicateParent).Select(c => SqlFunctions.Sql.varp_distinct(c.Value)).FirstOrDefault()
            .Should().Be(0.0, "a population variance over one distinct contribution is defined and zero");
    }

    [Fact]
    public void Stdev_FacadeNonDistinctDuplicates_ShouldBeZero()
    {
        Children(DuplicateParent).Select(c => SqlFunctions.Sql.stdev(c.Value)).First()
            .Should().Be(0.0, "three equal contributions define a zero sample deviation");
    }

    /// <summary>Parent marker row of the class-owned <c>t292_parent</c> table.</summary>
    [SqlTable("t292_parent")]
    public sealed class T292Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Child marker row of the class-owned <c>t292_child</c> table.</summary>
    [SqlTable("t292_child")]
    public sealed class T292Child
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("val")]
        public double Value { get; set; }

        [Column("opt")]
        public double? OptionalValue { get; set; }
    }
}
