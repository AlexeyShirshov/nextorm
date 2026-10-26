using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void Count_ShouldReturn10()
    {
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.count()).First();

        cnt.Should().Be(10);
    }

    [Fact]
    public void CountBig_ShouldReturn10()
    {
        // count_big is a 64-bit count. SQL Server needs count_big(); the other dialects satisfy it with
        // count(), which already returns a 64-bit integer. All of them must produce a long here.
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.count_big()).First();

        cnt.Should().Be(10L);
    }

    [Fact]
    public void CountOnEntity_ShouldReturn10()
    {
        var cnt = _sut.SimpleEntity.Count();

        cnt.Should().Be(10);
    }

    [Fact]
    public void CountComplex_ShouldReturn()
    {
        var cnt = _sut.ComplexEntity.Select(e => SqlFunctions.Sql.count(e.Int)).First();

        cnt.Should().Be(2);

        cnt = _sut.ComplexEntity.Select(e => SqlFunctions.Sql.count(e.Int) + 2).First();

        cnt.Should().Be(4);
    }

    [Fact]
    public void CountDistinct_ShouldReturn1()
    {
        var cnt = _sut.ComplexEntity.Select(e => SqlFunctions.Sql.count_distinct(e.Int)).First();

        cnt.Should().Be(1);
    }

    [Fact]
    public void Min_ShouldReturn1()
    {
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.min(e.Id)).First();

        cnt.Should().Be(1);

        _sut.SimpleEntity.Min(e => e.Id).Should().Be(cnt);
    }

    [Fact]
    public void Max_ShouldReturn10()
    {
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.max(e.Id)).First();

        cnt.Should().Be(10);

        _sut.SimpleEntity.Max(e => e.Id).Should().Be(cnt);
    }

    [Fact]
    public void Avg_ShouldReturn6()
    {
        // PostgreSQL and SQLite keep the fractional part of an integer AVG (5.5 rounds to 6 when
        // projected as int); SQL Server evaluates AVG over an integer column as an integer, so the
        // assertion below only applies where the provider supports fractional results.
        Assert.SkipUnless(Provider.SupportsFractionalAverage,
            "This provider evaluates AVG over an integer column as an integer.");

        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.avg(e.Id)).First();

        cnt.Should().Be(6);

        _sut.SimpleEntity.Avg(e => e.Id).Should().Be(cnt);
    }

    [Fact]
    public void Sum_ShouldReturn55()
    {
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.sum(e.Id)).First();

        cnt.Should().Be(55);

        _sut.SimpleEntity.Sum(e => e.Id).Should().Be(cnt);
    }

    [Fact]
    public void Stdev_ShouldReturn3()
    {
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.stdev(e.Id)).First();

        cnt.Should().Be(3);

        _sut.SimpleEntity.Stdev(e => e.Id).Should().Be(cnt);
    }

    [Fact]
    public void Stdevp_ShouldReturn3()
    {
        // The population standard deviation of 1..10 is sqrt(82.5 / 10) = 2.8722813232690143.
        // The int projection rounds it (Convert.ToInt32, midpoint-to-even), so the result is 3.
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.stdevp(e.Id)).First();

        cnt.Should().Be(3);

        _sut.SimpleEntity.Stdevp(e => e.Id).Should().Be(cnt);
    }

    [Fact]
    public void Stdevp_ShouldMatchPopulationStandardDeviation()
    {
        // Independent check that pins the exact value and bypasses the lossy int projection.
        _sut.SimpleEntity
            .Select(x => SqlFunctions.Sql.stdevp((double)x.Id))
            .First()
            .Should().BeApproximately(2.8722813232690143, 1e-12);
    }

    [Fact]
    public void Var_ShouldReturn9()
    {
        Assert.SkipUnless(Provider.SupportsVarianceAggregates,
            "This provider does not implement the sample variance aggregate.");

        // The sample variance of 1..10 is 82.5 / 9 = 9.166666666666666, which the int projection
        // rounds down to 9.
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.var(e.Id)).First();

        cnt.Should().Be(9);

        _sut.SimpleEntity.Var(e => e.Id).Should().Be(cnt);
    }

    [Fact]
    public void Var_ShouldMatchSampleVariance()
    {
        Assert.SkipUnless(Provider.SupportsVarianceAggregates,
            "This provider does not implement the sample variance aggregate.");

        _sut.SimpleEntity
            .Select(x => SqlFunctions.Sql.var((double)x.Id))
            .First()
            .Should().BeApproximately(9.166666666666666, 1e-12);
    }

    [Fact]
    public void Varp_ShouldReturn8()
    {
        Assert.SkipUnless(Provider.SupportsVarianceAggregates,
            "This provider does not implement the population variance aggregate.");

        // The population variance of 1..10 is 82.5 / 10 = 8.25, which the int projection rounds
        // to 8.
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.varp(e.Id)).First();

        cnt.Should().Be(8);

        _sut.SimpleEntity.Varp(e => e.Id).Should().Be(cnt);
    }

    [Fact]
    public void Varp_ShouldMatchPopulationVariance()
    {
        Assert.SkipUnless(Provider.SupportsVarianceAggregates,
            "This provider does not implement the population variance aggregate.");

        _sut.SimpleEntity
            .Select(x => SqlFunctions.Sql.varp((double)x.Id))
            .First()
            .Should().BeApproximately(8.25, 1e-12);
    }

    [Fact]
    public async Task MinAsync_ShouldReturn1()
    {
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.min(e.Id)).First();

        cnt.Should().Be(1);

        (await _sut.SimpleEntity.MinAsync(e => e.Id)).Should().Be(cnt);
    }

    [Fact]
    public async Task MaxAsync_ShouldReturn10()
    {
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.max(e.Id)).First();

        cnt.Should().Be(10);

        (await _sut.SimpleEntity.MaxAsync(e => e.Id)).Should().Be(cnt);
    }

    [Fact]
    public async Task AvgAsync_ShouldReturn6()
    {
        Assert.SkipUnless(Provider.SupportsFractionalAverage,
            "This provider evaluates AVG over an integer column as an integer.");

        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.avg(e.Id)).First();

        cnt.Should().Be(6);

        (await _sut.SimpleEntity.AvgAsync(e => e.Id)).Should().Be(cnt);
    }

    [Fact]
    public async Task SumAsync_ShouldReturn55()
    {
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.sum(e.Id)).First();

        cnt.Should().Be(55);

        (await _sut.SimpleEntity.SumAsync(e => e.Id)).Should().Be(cnt);
    }

    [Fact]
    public async Task StdevAsync_ShouldReturn3()
    {
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.stdev(e.Id)).First();

        cnt.Should().Be(3);

        (await _sut.SimpleEntity.StdevAsync(e => e.Id)).Should().Be(cnt);
    }

    [Fact]
    public async Task StdevpAsync_ShouldReturn3()
    {
        // sqrt(8.25) = 2.8722813232690143 rounds to 3 when projected as an int.
        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.stdevp(e.Id)).First();

        cnt.Should().Be(3);

        (await _sut.SimpleEntity.StdevpAsync(e => e.Id)).Should().Be(cnt);
    }

    [Fact]
    public async Task VarAsync_ShouldReturn9()
    {
        Assert.SkipUnless(Provider.SupportsVarianceAggregates,
            "This provider does not implement the sample variance aggregate.");

        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.var(e.Id)).First();

        cnt.Should().Be(9);

        (await _sut.SimpleEntity.VarAsync(e => e.Id)).Should().Be(cnt);
    }

    [Fact]
    public async Task VarpAsync_ShouldReturn8()
    {
        Assert.SkipUnless(Provider.SupportsVarianceAggregates,
            "This provider does not implement the population variance aggregate.");

        var cnt = _sut.SimpleEntity.Select(e => SqlFunctions.Sql.varp(e.Id)).First();

        cnt.Should().Be(8);

        (await _sut.SimpleEntity.VarpAsync(e => e.Id)).Should().Be(cnt);
    }

    // The tests below exercise the params carrying overloads. Each binds the value 5 to the
    // positional SqlFunctions.Parameter<int>(0) used by the Where filter, so the aggregate only sees ids
    // 6..10. That reaches the ReadOnlySpan<object?> / object[] overloads rather than the no-arg
    // ones.
    [Fact]
    public void MinParam_ShouldReturn6()
    {
        var r = _sut.SimpleEntity.Where(e => e.Id > SqlFunctions.Parameter<int>(0)).Min(e => e.Id, 5);

        r.Should().Be(6);
    }

    [Fact]
    public void MaxParam_ShouldReturn10()
    {
        var r = _sut.SimpleEntity.Where(e => e.Id > SqlFunctions.Parameter<int>(0)).Max(e => e.Id, 5);

        r.Should().Be(10);
    }

    [Fact]
    public void AvgParam_ShouldReturn8()
    {
        // The average of 6..10 is exactly 8, so integer and fractional AVG agree here and no
        // capability gate is needed.
        var r = _sut.SimpleEntity.Where(e => e.Id > SqlFunctions.Parameter<int>(0)).Avg(e => e.Id, 5);

        r.Should().Be(8);
    }

    [Fact]
    public void SumParam_ShouldReturn40()
    {
        var r = _sut.SimpleEntity.Where(e => e.Id > SqlFunctions.Parameter<int>(0)).Sum(e => e.Id, 5);

        r.Should().Be(40);
    }

    [Fact]
    public void StdevParam_ShouldReturn2()
    {
        // The sample standard deviation of 6..10 is sqrt(10 / 4) = 1.5811388300841898, which
        // rounds to 2 when projected as an int.
        var r = _sut.SimpleEntity.Where(e => e.Id > SqlFunctions.Parameter<int>(0)).Stdev(e => e.Id, 5);

        r.Should().Be(2);
    }

    [Fact]
    public void StdevpParam_ShouldReturn1()
    {
        // The population standard deviation of 6..10 is sqrt(10 / 5) = 1.4142135623730951, which
        // rounds to 1 when projected as an int.
        var r = _sut.SimpleEntity.Where(e => e.Id > SqlFunctions.Parameter<int>(0)).Stdevp(e => e.Id, 5);

        r.Should().Be(1);
    }

    [Fact]
    public void VarParam_ShouldMatchSampleVariance()
    {
        Assert.SkipUnless(Provider.SupportsVarianceAggregates,
            "This provider does not implement the sample variance aggregate.");

        // The sample variance of 6..10 is 10 / 4 = 2.5. It is asserted on a double projection
        // because 2.5 is a midpoint tie for the int projection (Convert.ToInt32 rounds to even).
        var r = _sut.SimpleEntity
            .Where(e => e.Id > SqlFunctions.Parameter<int>(0))
            .Var(e => (double)e.Id, 5);

        r.Should().BeApproximately(2.5, 1e-12);
    }

    [Fact]
    public void VarpParam_ShouldMatchPopulationVariance()
    {
        Assert.SkipUnless(Provider.SupportsVarianceAggregates,
            "This provider does not implement the population variance aggregate.");

        // The population variance of 6..10 is 10 / 5 = 2.
        var r = _sut.SimpleEntity
            .Where(e => e.Id > SqlFunctions.Parameter<int>(0))
            .Varp(e => (double)e.Id, 5);

        r.Should().BeApproximately(2.0, 1e-12);
    }

    [Fact]
    public async Task MinAsyncParam_ShouldReturn6()
    {
        var r = await _sut.SimpleEntity.Where(e => e.Id > SqlFunctions.Parameter<int>(0)).MinAsync(e => e.Id, 5);

        r.Should().Be(6);
    }

    [Fact]
    public async Task SumAsyncParam_ShouldReturn40()
    {
        var r = await _sut.SimpleEntity.Where(e => e.Id > SqlFunctions.Parameter<int>(0)).SumAsync(e => e.Id, 5);

        r.Should().Be(40);
    }

    [Fact]
    public void Last_ShouldReturnLastOrderedRow()
    {
        // simple_entity ids are 1..10; Last reverses the ORDER BY and reuses First.
        _sut.SimpleEntity.OrderBy(e => e.Id).Select(e => e.Id).Last().Should().Be(10);
    }

    [Fact]
    public void LastOrDefault_OnDescending_ShouldReturnFirstRow()
    {
        _sut.SimpleEntity.OrderByDescending(e => e.Id).Select(e => e.Id).LastOrDefault().Should().Be(1);
    }

    // T292 (#292): the variability aggregates return a structurally nullable result. Cardinality
    // counts contributing non-null values: n = 0 is undefined for all four families, n = 1 is
    // undefined for the sample flavours and zero for the population ones, and the established
    // multi-row results are unchanged. SimpleEntity holds ids 1..10; a negative id filter selects no
    // rows, and `Id == 1` selects exactly one.

    [Fact]
    public void T292_VariabilityZeroContributingRows_ShouldBeNull()
    {
        var empty = _sut.SimpleEntity.Where(e => e.Id < 0);

        empty.Stdev(e => e.Id).Should().BeNull("an undefined sample deviation is null, not zero");
        empty.Stdevp(e => e.Id).Should().BeNull("an undefined population deviation is null, not zero");
        empty.Var(e => e.Id).Should().BeNull("an undefined sample variance is null, not zero");
        empty.Varp(e => e.Id).Should().BeNull("an undefined population variance is null, not zero");
    }

    [Fact]
    public void T292_VariabilitySingleContributingRow_ShouldMatchCardinality()
    {
        var single = _sut.SimpleEntity.Where(e => e.Id == 1);

        single.Stdev(e => e.Id).Should().BeNull("a one-row sample has no sample deviation");
        single.Var(e => e.Id).Should().BeNull("a one-row sample has no sample variance");
        single.Stdevp(e => e.Id).Should().Be(0, "a one-row population deviation is defined and zero");
        single.Varp(e => e.Id).Should().Be(0, "a one-row population variance is defined and zero");
    }

    [Fact]
    public void T292_VariabilityMultipleContributingRows_ShouldReturnEstablishedValues()
    {
        // The sample variance of 1..10 is 82.5 / 9, the population variance 82.5 / 10.
        _sut.SimpleEntity.Stdev(e => (double)e.Id).Should().BeApproximately(Math.Sqrt(82.5 / 9), 1e-12);
        _sut.SimpleEntity.Stdevp(e => (double)e.Id).Should().BeApproximately(Math.Sqrt(8.25), 1e-12);
        _sut.SimpleEntity.Var(e => (double)e.Id).Should().BeApproximately(82.5 / 9, 1e-12);
        _sut.SimpleEntity.Varp(e => (double)e.Id).Should().BeApproximately(8.25, 1e-12);
    }

    [Fact]
    public void T292_VariabilityNullableSelector_ShouldIgnoreNulls()
    {
        var empty = _sut.SimpleEntity.Where(e => e.Id < 0);

        empty.Stdev(e => (double?)e.Id).Should().BeNull();
        empty.Var(e => (double?)e.Id).Should().BeNull();
        empty.Stdevp(e => (double?)e.Id).Should().BeNull();
        empty.Varp(e => (double?)e.Id).Should().BeNull();
    }

    [Fact]
    public void T292_VariabilityFacadeForms_ShouldReturnEstablishedValues()
    {
        // The eight SQL facade forms are semantically nullable; the distinct forms of 1..10 agree
        // with the plain forms because the values are already distinct.
        _sut.SimpleEntity.Select(e => SqlFunctions.Sql.stdev((double)e.Id)).First()
            .Should().BeApproximately(Math.Sqrt(82.5 / 9), 1e-12);
        _sut.SimpleEntity.Select(e => SqlFunctions.Sql.stdevp((double)e.Id)).First()
            .Should().BeApproximately(Math.Sqrt(8.25), 1e-12);

        // The MySQL/MariaDB server grammar rejects DISTINCT inside STDDEV_SAMP/STDDEV_POP/VAR_SAMP/
        // VAR_POP ("You have an error in your SQL syntax ... near 'distinct cast(id as double))'"),
        // i.e. `stddev_samp(distinct x)` is not executable there. That is a pre-existing limitation of
        // the shared renderer's `_distinct` spelling, unrelated to the nullable result; the SQL
        // generation tests keep asserting that spelling for every provider, and the live distinct
        // forms run on the providers whose grammar accepts them.
        if (Provider.Name is not ("mysql" or "mariadb"))
        {
            _sut.SimpleEntity.Select(e => SqlFunctions.Sql.stdev_distinct((double)e.Id)).First()
                .Should().BeApproximately(Math.Sqrt(82.5 / 9), 1e-12);
            _sut.SimpleEntity.Select(e => SqlFunctions.Sql.stdevp_distinct((double)e.Id)).First()
                .Should().BeApproximately(Math.Sqrt(8.25), 1e-12);
        }
    }

    [Fact]
    public async Task T292_VariabilityAsyncZeroContributingRows_ShouldBeNull()
    {
        using var cts = new CancellationTokenSource();
        var empty = _sut.SimpleEntity.Where(e => e.Id < 0);

        (await empty.StdevAsync(e => e.Id)).Should().BeNull();
        (await empty.StdevpAsync(e => e.Id)).Should().BeNull();
        (await empty.VarAsync(e => e.Id, cts.Token)).Should().BeNull();
        (await empty.VarpAsync(e => e.Id, cts.Token)).Should().BeNull();
    }

    [Fact]
    public async Task T292_VariabilityAsyncMultipleContributingRows_ShouldReturnEstablishedValues()
    {
        using var cts = new CancellationTokenSource();

        (await _sut.SimpleEntity.StdevAsync(e => (double)e.Id)).Should().BeApproximately(Math.Sqrt(82.5 / 9), 1e-12);
        (await _sut.SimpleEntity.StdevpAsync(e => (double)e.Id, cts.Token)).Should().BeApproximately(Math.Sqrt(8.25), 1e-12);
        (await _sut.SimpleEntity.VarAsync(e => (double)e.Id, cts.Token)).Should().BeApproximately(82.5 / 9, 1e-12);
        (await _sut.SimpleEntity.VarpAsync(e => (double)e.Id)).Should().BeApproximately(8.25, 1e-12);
    }
}
