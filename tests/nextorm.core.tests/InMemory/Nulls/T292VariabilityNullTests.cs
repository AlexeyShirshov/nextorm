using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T292 (#292) in-memory arm: the four variability families (<c>Stdev</c>/<c>Stdevp</c>/<c>Var</c>/
/// <c>Varp</c>) must reproduce the SQL cardinality contract in memory — undefined results are
/// <see langword="null"/>, a one-row population result stays zero, and the multi-row formulas are
/// unchanged.
/// </summary>
/// <remarks>
/// <para>
/// Cardinality counts contributing non-null values after DISTINCT: n = 0 is undefined for all four
/// families; the sample flavours are undefined for n = 1; the population flavours are defined at
/// n = 1 and fold to zero. The pre-fix fold returned <c>default</c> for an empty set and <c>0</c> for
/// a one-row sample, so the undefined cases silently produced <c>0.0</c>.
/// </para>
/// <para>
/// The fixture rows 1, 2, 3 pin the established formulas: sample variance 1, population variance 2/3,
/// hence <c>Stdev</c> = 1 and <c>Stdevp</c> = sqrt(2/3). Every fact states the post-fix contract.
/// </para>
/// </remarks>
public class T292VariabilityNullTests
{
    /// <summary>Parent with no child rows.</summary>
    private const int EmptyParent = 29200;

    /// <summary>Parent with exactly one child row (value 4).</summary>
    private const int SingleParent = 29201;

    /// <summary>Parent with three child rows (values 1, 2, 3).</summary>
    private const int TripleParent = 29202;

    /// <summary>Parent with two physical rows whose nullable value is null in both.</summary>
    private const int AllNullParent = 29203;

    /// <summary>Parent with two physical rows, exactly one of them non-null (5).</summary>
    private const int MixedParent = 29204;

    /// <summary>Parent with three physical rows all carrying the value 2.</summary>
    private const int DuplicateParent = 29205;

    private readonly InMemoryRepository _sut;
    private readonly EntityBuilder<T292Child> _children;

    public T292VariabilityNullTests(InMemoryRepository sut)
    {
        _sut = sut;
        _children = sut.DataProvider.From<T292Child>();

        sut.DataProvider.From<T292Parent>().WithData(
        [
            new T292Parent { Id = EmptyParent, Name = "empty" },
            new T292Parent { Id = SingleParent, Name = "single" },
            new T292Parent { Id = TripleParent, Name = "triple" },
            new T292Parent { Id = AllNullParent, Name = "all-null" },
            new T292Parent { Id = MixedParent, Name = "mixed" },
            new T292Parent { Id = DuplicateParent, Name = "duplicate" },
        ]);

        _children.WithData(
        [
            new T292Child { Id = 29210, ParentId = SingleParent, Value = 4, OptionalValue = 4 },
            new T292Child { Id = 29220, ParentId = TripleParent, Value = 1, OptionalValue = 1 },
            new T292Child { Id = 29221, ParentId = TripleParent, Value = 2, OptionalValue = 2 },
            new T292Child { Id = 29222, ParentId = TripleParent, Value = 3, OptionalValue = 3 },
            new T292Child { Id = 29230, ParentId = AllNullParent, Value = 7, OptionalValue = null },
            new T292Child { Id = 29231, ParentId = AllNullParent, Value = 8, OptionalValue = null },
            new T292Child { Id = 29240, ParentId = MixedParent, Value = 8, OptionalValue = null },
            new T292Child { Id = 29241, ParentId = MixedParent, Value = 9, OptionalValue = 5 },
            new T292Child { Id = 29250, ParentId = DuplicateParent, Value = 2, OptionalValue = 2 },
            new T292Child { Id = 29251, ParentId = DuplicateParent, Value = 2, OptionalValue = 2 },
            new T292Child { Id = 29252, ParentId = DuplicateParent, Value = 2, OptionalValue = 2 },
        ]);
    }

    private EntityBuilder<T292Parent> Parents() => _sut.DataProvider.From<T292Parent>();

    private EntityBuilder<T292Child> Children(int parentId) => _children.Where(c => c.ParentId == parentId);

    // ---------------------------------------------------------------------------------------
    // n = 0 contributing rows: undefined for every family.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Stdev_TerminalZeroContributingRows_ShouldBeNull()
    {
        double? value = Children(EmptyParent).Stdev(c => c.Value);

        value.Should().BeNull("an undefined sample deviation must be null, not an invented zero (AC01/AC07)");
    }

    [Fact]
    public void Stdevp_TerminalZeroContributingRows_ShouldBeNull()
    {
        double? value = Children(EmptyParent).Stdevp(c => c.Value);

        value.Should().BeNull("an undefined population deviation must be null, not an invented zero (AC03/AC07)");
    }

    [Fact]
    public void Var_TerminalZeroContributingRows_ShouldBeNull()
    {
        double? value = Children(EmptyParent).Var(c => c.Value);

        value.Should().BeNull("an undefined sample variance must be null, not an invented zero (AC01/AC07)");
    }

    [Fact]
    public void Varp_TerminalZeroContributingRows_ShouldBeNull()
    {
        double? value = Children(EmptyParent).Varp(c => c.Value);

        value.Should().BeNull("an undefined population variance must be null, not an invented zero (AC03/AC07)");
    }

    // ---------------------------------------------------------------------------------------
    // n = 1 contributing row: the sample flavours are undefined, the population ones are zero.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Stdev_TerminalSingleContributingRow_ShouldBeNull()
    {
        double? value = Children(SingleParent).Stdev(c => c.Value);

        value.Should().BeNull("a one-row sample has no sample deviation, and must not report zero (AC07)");
    }

    [Fact]
    public void Var_TerminalSingleContributingRow_ShouldBeNull()
    {
        double? value = Children(SingleParent).Var(c => c.Value);

        value.Should().BeNull("a one-row sample has no sample variance, and must not report zero (AC07)");
    }

    [Fact]
    public void Stdevp_TerminalSingleContributingRow_ShouldBeZero()
    {
        Children(SingleParent).Stdevp(c => c.Value)
            .Should().Be(0.0, "a one-row population deviation is defined and stays zero (AC04)");
    }

    [Fact]
    public void Varp_TerminalSingleContributingRow_ShouldBeZero()
    {
        Children(SingleParent).Varp(c => c.Value)
            .Should().Be(0.0, "a one-row population variance is defined and stays zero (AC04)");
    }

    // ---------------------------------------------------------------------------------------
    // n = 3 contributing rows (values 1, 2, 3): the established numeric results are preserved.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void VariabilityAggregates_TerminalMultipleContributingRows_ShouldReturnEstablishedValues()
    {
        Children(TripleParent).Stdev(c => c.Value)
            .Should().BeApproximately(1.0, 1e-12, "the sample deviation of 1, 2, 3 is 1");
        Children(TripleParent).Stdevp(c => c.Value)
            .Should().BeApproximately(0.816496580927726, 1e-12, "sqrt(2/3)");
        Children(TripleParent).Var(c => c.Value)
            .Should().BeApproximately(1.0, 1e-12, "the sample variance of 1, 2, 3 is 1");
        Children(TripleParent).Varp(c => c.Value)
            .Should().BeApproximately(2.0 / 3.0, 1e-12);
    }

    // ---------------------------------------------------------------------------------------
    // Nullable selectors: null inputs do not contribute to the cardinality (AC06).
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void VariabilityAggregates_TerminalNullableSelectorAllNull_ShouldBeNull()
    {
        Children(AllNullParent).Stdev(c => c.OptionalValue)
            .Should().BeNull("two physical all-null rows contribute no value to a sample deviation");
        Children(AllNullParent).Stdevp(c => c.OptionalValue)
            .Should().BeNull("two physical all-null rows contribute no value to a population deviation");
        Children(AllNullParent).Var(c => c.OptionalValue)
            .Should().BeNull("two physical all-null rows contribute no value to a sample variance");
        Children(AllNullParent).Varp(c => c.OptionalValue)
            .Should().BeNull("two physical all-null rows contribute no value to a population variance");
    }

    [Fact]
    public void Stdev_TerminalNullableSelectorSingleNonNull_ShouldBeNull()
    {
        Children(MixedParent).Stdev(c => c.OptionalValue)
            .Should().BeNull("two physical rows with one non-null value remain an undefined sample (AC06)");
    }

    [Fact]
    public void Var_TerminalNullableSelectorSingleNonNull_ShouldBeNull()
    {
        Children(MixedParent).Var(c => c.OptionalValue)
            .Should().BeNull("two physical rows with one non-null value remain an undefined sample (AC06)");
    }

    [Fact]
    public void Stdevp_TerminalNullableSelectorSingleNonNull_ShouldBeZero()
    {
        Children(MixedParent).Stdevp(c => c.OptionalValue)
            .Should().Be(0.0, "a population deviation over one contributing value is defined and zero (AC06)");
    }

    [Fact]
    public void Varp_TerminalNullableSelectorSingleNonNull_ShouldBeZero()
    {
        Children(MixedParent).Varp(c => c.OptionalValue)
            .Should().Be(0.0, "a population variance over one contributing value is defined and zero (AC06)");
    }

    [Fact]
    public void VariabilityAggregates_TerminalNullableSelectorMultipleNonNull_ShouldReturnEstablishedValues()
    {
        Children(TripleParent).Stdev(c => c.OptionalValue).Should().BeApproximately(1.0, 1e-12);
        Children(TripleParent).Stdevp(c => c.OptionalValue).Should().BeApproximately(0.816496580927726, 1e-12);
        Children(TripleParent).Var(c => c.OptionalValue).Should().BeApproximately(1.0, 1e-12);
        Children(TripleParent).Varp(c => c.OptionalValue).Should().BeApproximately(2.0 / 3.0, 1e-12);
    }

    // ---------------------------------------------------------------------------------------
    // Async twins: the params shape and the CancellationToken shape of every family.
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
    public async Task StdevAsync_TerminalNullableSelectorAllNull_ShouldBeNull()
    {
        using var cts = new CancellationTokenSource();

        (await Children(AllNullParent).StdevAsync(c => c.OptionalValue, cts.Token)).Should().BeNull();
        (await Children(AllNullParent).VarAsync(c => c.OptionalValue, cts.Token)).Should().BeNull();
    }

    [Fact]
    public async Task StdevpAsync_TerminalNullableSelectorSingleNonNull_ShouldBeZero()
    {
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
        Children(DuplicateParent)
            .Select(c => SqlFunctions.Sql.stdev(c.Value))
            .First()
            .Should().Be(0.0, "three equal contributions define a zero sample deviation");
    }

    // ---------------------------------------------------------------------------------------
    // Conversion controls: defined results still convert to the declared type, undefined ones do
    // not reach Convert.ChangeType (AC07).
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Stdev_TerminalDecimalSelectorMultipleContributingRows_ShouldConvert()
    {
        Children(TripleParent).Stdev(c => (decimal)c.Value)
            .Should().BeApproximately(1.0m, 1e-12m, "a defined decimal result keeps the conversion path");
    }

    [Fact]
    public void Var_TerminalDecimalSelectorSingleContributingRow_ShouldBeNull()
    {
        Children(SingleParent).Var(c => (decimal)c.Value)
            .Should().BeNull("the undefined decimal sample result is returned before any conversion");
    }

    [Fact]
    public void Varp_TerminalFloatSelectorSingleContributingRow_ShouldBeZero()
    {
        Children(SingleParent).Varp(c => (float)c.Value)
            .Should().Be(0.0f, "a defined single-row population result converts to float zero");
    }

    // ---------------------------------------------------------------------------------------
    // Correlated projection: the projection member carries the nullable result type.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Stdev_ProjectionZeroContributingRows_ShouldBeNull()
    {
        var rows = Parents()
            .Where(p => p.Id == EmptyParent)
            .Select(p => new { p.Id, V = _children.Where(c => c.ParentId == p.Id).Stdev(c => c.Value) })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("an undefined sample deviation must be null, not an invented zero (AC01/AC07)");
    }

    [Fact]
    public void Stdev_ProjectionSingleContributingRow_ShouldBeNull()
    {
        var rows = Parents()
            .Where(p => p.Id == SingleParent)
            .Select(p => new { p.Id, V = _children.Where(c => c.ParentId == p.Id).Stdev(c => c.Value) })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("a one-row sample has no sample deviation (AC07)");
    }

    [Fact]
    public void Stdevp_ProjectionZeroContributingRows_ShouldBeNull()
    {
        var rows = Parents()
            .Where(p => p.Id == EmptyParent)
            .Select(p => new { p.Id, V = _children.Where(c => c.ParentId == p.Id).Stdevp(c => c.Value) })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("an undefined population deviation must be null (AC07)");
    }

    [Fact]
    public void Var_ProjectionZeroContributingRows_ShouldBeNull()
    {
        var rows = Parents()
            .Where(p => p.Id == EmptyParent)
            .Select(p => new { p.Id, V = _children.Where(c => c.ParentId == p.Id).Var(c => c.Value) })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("an undefined sample variance must be null (AC07)");
    }

    [Fact]
    public void Varp_ProjectionZeroContributingRows_ShouldBeNull()
    {
        var rows = Parents()
            .Where(p => p.Id == EmptyParent)
            .Select(p => new { p.Id, V = _children.Where(c => c.ParentId == p.Id).Varp(c => c.Value) })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("an undefined population variance must be null (AC07)");
    }

    [Fact]
    public void VariabilityAggregates_ProjectionMultipleContributingRows_ShouldReturnEstablishedValues()
    {
        var rows = Parents()
            .Where(p => p.Id == TripleParent)
            .Select(p => new
            {
                p.Id,
                S = _children.Where(c => c.ParentId == p.Id).Stdev(c => c.Value),
                Sp = _children.Where(c => c.ParentId == p.Id).Stdevp(c => c.Value),
                V = _children.Where(c => c.ParentId == p.Id).Var(c => c.Value),
                Vp = _children.Where(c => c.ParentId == p.Id).Varp(c => c.Value),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].S.Should().BeApproximately(1.0, 1e-12);
        rows[0].Sp.Should().BeApproximately(0.816496580927726, 1e-12);
        rows[0].V.Should().BeApproximately(1.0, 1e-12);
        rows[0].Vp.Should().BeApproximately(2.0 / 3.0, 1e-12);
    }

    [Fact]
    public void Stdev_ProjectionNullableSelectorSingleNonNull_ShouldBeNull()
    {
        var rows = Parents()
            .Where(p => p.Id == MixedParent)
            .Select(p => new { p.Id, V = _children.Where(c => c.ParentId == p.Id).Stdev(c => c.OptionalValue) })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].V.Should().BeNull("one non-null value across two physical rows is an undefined sample (AC06)");
    }

    /// <summary>Parent row of the in-memory T292 fixture.</summary>
    public sealed class T292Parent
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Child row of the in-memory T292 fixture.</summary>
    public sealed class T292Child
    {
        public int Id { get; set; }

        public int ParentId { get; set; }

        public double Value { get; set; }

        public double? OptionalValue { get; set; }
    }
}
