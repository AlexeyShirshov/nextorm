using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Serializes every T300 fact that observes or mutates the process-wide
/// <see cref="DataContextCache.SelectListCache"/>; a concurrent test from another collection could
/// otherwise warm the type-keyed entry and change which preparation path runs.
/// </summary>
[CollectionDefinition("T300 scalar derived source", DisableParallelization = true)]
public sealed class T300ScalarDerivedSourceCollection;

/// <summary>
/// T300 (#300) against the seeded SQLite database. The defect is cache-history dependent: a scalar
/// read populates <c>SelectListCache[typeof(int)]</c> with a list built for object materialisation,
/// and the following <c>From(left.UnionAll(right)).ToList()</c> reuses it without <c>OneColumn</c>,
/// so preparation throws <c>QueryPreparationException: Cannot get ctor from System.Int32</c>. The
/// same read with a cold cache succeeds today. Both states must return the duplicate-preserving rows
/// after the fix, and the cold-cache control is green on both sides of it.
/// </summary>
[Collection("T300 scalar derived source")]
public sealed class ScalarDerivedSourceIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private QueryCommand<int> Left() => Context.From<ISimpleEntity>().Where(x => x.Id <= 2).Select(x => x.Id);

    private QueryCommand<int> Right() => Context.From<ISimpleEntity>().Where(x => x.Id >= 2 && x.Id <= 3).Select(x => x.Id);

    private static void ClearScalarCache() => DataContextCache.SelectListCache.Remove(typeof(int));

    /// <summary>
    /// AC1 warm-cache red: the documented working scalar read runs first and, before the fix, leaves an
    /// object-materialisation list under <c>typeof(int)</c>; the bare set-operation read then has to
    /// return the duplicate-preserving rows instead of throwing the constructor lookup. After the fix
    /// the same sequence must return <c>[1,2,2,3]</c> (the branch does not populate the cache).
    /// </summary>
    [Fact]
    public void BareUnionAllIntMaterializes()
    {
        ClearScalarCache();

        var left = Left();
        var right = Right();

        Context.From(left).ToList().Should().Equal(1, 2);

        Context.From(left.UnionAll(right)).ToList().Should().Equal(1, 2, 2, 3);
    }

    /// <summary>
    /// AC1 cold-cache control: with no type-keyed entry the issue's shape already succeeds on the
    /// unfixed tree; the fix must keep it green. This is never a required red.
    /// </summary>
    [Fact]
    public void ColdCacheControl_ReturnsRows()
    {
        ClearScalarCache();

        Context.From(Left().UnionAll(Right())).ToList().Should().Equal(1, 2, 2, 3);
    }

    /// <summary>
    /// AC3 on SQLite: all four supported operators render a bare scalar derived source and return the
    /// pinned duplicate/order semantics from left = {1,2} and right = {2,3}.
    /// </summary>
    [Fact]
    public void SupportedOperatorsMaterialize()
    {
        AssertRows(left => left.Union(Right()), 1, 2, 3);
        AssertRows(left => left.UnionAll(Right()), 1, 2, 2, 3);
        AssertRows(left => left.Intersect(Right()), 2);
        AssertRows(left => left.Except(Right()), 1);
    }

    private void AssertRows(Func<QueryCommand<int>, QueryCommand<int>> operation, params int[] expected)
    {
        ClearScalarCache();
        Context.From(operation(Left())).ToList().Should().BeEquivalentTo(expected);
    }

    /// <summary>
    /// AC2: a cold read and a read after a scalar read agree; the derived list must not be inserted
    /// into the type-keyed cache to achieve that.
    /// </summary>
    [Fact]
    public void WarmAndColdReadsAgree()
    {
        ClearScalarCache();
        var cold = Context.From(Left().UnionAll(Right())).ToList();

        ClearScalarCache();
        var left = Left();
        var right = Right();
        Context.From(left).ToList().Should().Equal(1, 2);
        var warm = Context.From(left.UnionAll(right)).ToList();

        warm.Should().Equal(cold);
    }

    /// <summary>
    /// AC2: the single-column derived branch runs before the cache read, so a pre-seeded object list
    /// under <c>typeof(int)</c> is neither used nor replaced.
    /// </summary>
    [Fact]
    public void PreSeededCacheIsNotReadOrWritten()
    {
        var poison = new[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" },
        };
        DataContextCache.SelectListCache[typeof(int)] = poison;

        try
        {
            Context.From(Left().UnionAll(Right())).ToList().Should().Equal(1, 2, 2, 3);
            DataContextCache.SelectListCache[typeof(int)].Should().BeSameAs(
                poison, "the derived branch must not read or replace the type-keyed select-list entry");
        }
        finally
        {
            ClearScalarCache();
        }
    }

    /// <summary>AC2: repeated and reversed preparation produce the same duplicate-preserving rows.</summary>
    [Fact]
    public void RepeatedAndReversedReadsAgree()
    {
        ClearScalarCache();
        var first = Context.From(Left().UnionAll(Right())).ToList();
        var repeated = Context.From(Left().UnionAll(Right())).ToList();
        var reversed = Context.From(Right().UnionAll(Left())).ToList();

        first.Should().Equal(1, 2, 2, 3);
        repeated.Should().Equal(first);
        reversed.Should().BeEquivalentTo(first);
    }

    /// <summary>
    /// AC4: nullable scalars keep values and duplicates; a reference scalar keeps both duplicate values
    /// and the null row. Duplicates are counted explicitly, not just compared as a set.
    /// </summary>
    [Fact]
    public void ScalarVariantsPreserveValuesAndDuplicates()
    {
        ClearScalarCache();
        var nullableLeft = Context.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => (int?)c.Id);
        var nullableRight = Context.From<IComplexEntity>().Where(c => c.Id >= 2 && c.Id <= 3).Select(c => (int?)c.Id);
        Context.From(nullableLeft.UnionAll(nullableRight)).ToList().Should().Equal(1, 2, 2, 3);

        ClearScalarCache();
        var stringLeft = Context.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => c.String);
        var stringRight = Context.From<IComplexEntity>().Where(c => c.Id >= 2 && c.Id <= 3).Select(c => c.String);
        var strings = Context.From(stringLeft.UnionAll(stringRight)).ToList();

        strings.Should().BeEquivalentTo(new string?[] { "dadfasd", "xxx", "xxx", null });
        strings.Count(x => x == "xxx").Should().Be(2);
        strings.Count(x => x is null).Should().Be(1);
    }

    /// <summary>AC4: the existing async and array terminals materialise the same rows.</summary>
    [Fact]
    public async Task AsyncAndArrayTerminalsMaterialize()
    {
        ClearScalarCache();
        var array = Context.From(Left().UnionAll(Right())).ToArray();
        array.Should().Equal(1, 2, 2, 3);

        ClearScalarCache();
        var list = await Context.From(Left().UnionAll(Right())).ToListAsync();
        list.Should().Equal(1, 2, 2, 3);
    }

    /// <summary>AC4: a direct object projection over the entity source keeps its existing shape.</summary>
    [Fact]
    public void DirectObjectProjectionRemainsValid()
    {
        var rows = Context.From<IComplexEntity>().Where(c => c.Id <= 2).OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Int }).ToList();

        rows.Should().HaveCount(2);
        rows[0].Id.Should().Be(1);
        rows[1].Id.Should().Be(2);
    }

    /// <summary>AC4: a scalar CTE read keeps materialising its single column.</summary>
    [Fact]
    public void ScalarCteRemainsValid()
    {
        ClearScalarCache();
        Context.From(Left().AsCte("t300_scalar_cte")).ToList().Should().Equal(1, 2);
    }

    /// <summary>AC4: the count-only terminal keeps working (the non-materialising control).</summary>
    [Fact]
    public void CountRemainsValid()
    {
        Context.From(Left().UnionAll(Right())).Count().Should().Be(4);
    }

    /// <summary>AC4: <c>From(EntityBuilder&lt;int&gt;)</c> materialises its scalar rows.</summary>
    [Fact]
    public void EntityBuilderScalarRemainsValid()
    {
        ClearScalarCache();
        var builder = Context.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => c.Int ?? 0);
        Context.From(builder).ToList().Should().HaveCount(2);
    }

    /// <summary>
    /// AC4 negative: a multi-column derived source is not a scalar shape and keeps the existing
    /// actionable refusal instead of being routed through the new single-column branch.
    /// </summary>
    [Fact]
    public void MultiColumnDerivedSourceStaysRefused()
    {
        ClearScalarCache();
        var inner = Context.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => new { c.Id, c.Int });

        var act = () => Context.From(inner).ToList();

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*derived source without an explicit outer Select*");
    }

    /// <summary>
    /// AC4/E08: an unaliased computed scalar is named by the T312 boundary and materialises, or — if
    /// the final prepared name is unusable — fails with the actionable scalar-derived message. The
    /// constructor lookup is never an acceptable outcome.
    /// </summary>
    [Fact]
    public void ComputedScalarIsAliasedOrRefusedActionably()
    {
        ClearScalarCache();
        var inner = Context.From<ISimpleEntity>().Where(x => x.Id <= 2).Select(x => x.Id + 10);

        List<int>? rows = null;
        Exception? failure = null;
        try
        {
            rows = Context.From(inner).ToList();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is null)
        {
            rows.Should().Equal(11, 12);
        }
        else
        {
            failure.Should().BeOfType<QueryPreparationException>()
                .Which.Message.Should().Contain("scalar derived source")
                .And.NotContain("Cannot get ctor");
        }
    }
}

/// <summary>
/// AC3 provider parity: the same bare scalar derived set-operation read runs live on every provider
/// that has an integration harness. MariaDB has no integration provider in rc4 and is deferred with
/// the recorded trigger "MariaDB harness availability"; its preparation/SQL parity is covered by the
/// provider unit project.
/// </summary>
[Collection("T300 scalar derived source")]
public sealed class T300ScalarDerivedSourceLiveMatrix
{
    private static readonly Dictionary<string, ITestProvider> Providers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sqlite"] = SqliteTestProvider.Instance,
        ["postgres"] = PostgresTestProvider.Instance,
        ["sqlserver"] = SqlServerTestProvider.Instance,
        ["mysql"] = MySqlTestProvider.Instance,
        ["clickhouse"] = ClickHouseTestProvider.Instance,
    };

    public static TheoryData<string> LiveProviders => new() { "sqlite", "postgres", "sqlserver", "mysql", "clickhouse" };

    /// <summary>Only PostgreSQL and ClickHouse render <c>INTERSECT ALL</c>/<c>EXCEPT ALL</c>.</summary>
    public static TheoryData<string> AllVariantProviders => new() { "postgres", "clickhouse" };

    [Theory]
    [MemberData(nameof(LiveProviders))]
    public void SupportedOperatorsMaterialize_Live(string providerName)
    {
        var provider = Providers[providerName];
        SkipUnlessAvailable(provider);
        provider.EnsureSeeded();
        using var context = provider.CreateContext();

        DataContextCache.SelectListCache.Remove(typeof(int));
        if (string.Equals(providerName, "clickhouse", StringComparison.OrdinalIgnoreCase))
        {
            // ClickHouse rejects a bare `UNION` without ALL/DISTINCT (EXPECTED_ALL_OR_DISTINCT); the
            // dialect's distinct rendering is a pre-existing provider gap outside T300. The branch
            // itself is still exercised on ClickHouse by UnionAll/Intersect/Except below.
            var union = () => context.From(Left(context).Union(Right(context))).OrderBy(x => x).ToList();
            union.Should().Throw<global::ClickHouse.Driver.ClickHouseServerException>()
                .WithMessage("*ALL or DISTINCT*");
        }
        else
        {
            AssertRows(context, left => left.Union(Right(context)), 1, 2, 3);
        }
        DataContextCache.SelectListCache.Remove(typeof(int));
        AssertRows(context, left => left.UnionAll(Right(context)), 1, 2, 2, 3);
        DataContextCache.SelectListCache.Remove(typeof(int));
        AssertRows(context, left => left.Intersect(Right(context)), 2);
        DataContextCache.SelectListCache.Remove(typeof(int));
        AssertRows(context, left => left.Except(Right(context)), 1);
    }

    /// <summary>
    /// AC3: the ALL variants of INTERSECT/EXCEPT materialise live on the providers that support them
    /// (PostgreSQL, ClickHouse); left = {1,2} and right = {2,3} give IntersectAll {2} and ExceptAll {1}.
    /// SQLite/SQL Server/MySQL keep the existing capability refusal (unit arm
    /// <c>AllVariants_FollowProviderCapability</c>), so they are not asserted here.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllVariantProviders))]
    public void AllVariantsMaterialize_Live(string providerName)
    {
        var provider = Providers[providerName];
        SkipUnlessAvailable(provider);
        provider.EnsureSeeded();
        using var context = provider.CreateContext();

        DataContextCache.SelectListCache.Remove(typeof(int));
        AssertRows(context, left => left.IntersectAll(Right(context)), 2);
        DataContextCache.SelectListCache.Remove(typeof(int));
        AssertRows(context, left => left.ExceptAll(Right(context)), 1);
    }

    /// <summary>
    /// AC4/E05: a nullable int scalar — a converted projection, so the derived boundary aliases the
    /// column — keeps values and duplicates live on every provider, not only on SQLite.
    /// </summary>
    [Theory]
    [MemberData(nameof(LiveProviders))]
    public void NullableScalarMaterialize_Live(string providerName)
    {
        var provider = Providers[providerName];
        SkipUnlessAvailable(provider);
        provider.EnsureSeeded();
        using var context = provider.CreateContext();

        DataContextCache.SelectListCache.Remove(typeof(int?));
        context.From(NullableLeft(context).UnionAll(NullableRight(context))).ToList()
            .Should().Equal(1, 2, 2, 3);
    }

    /// <summary>
    /// AC4/E05: a reference string — the seeded complex_entity rows carry 'dadfasd' / 'xxx' / null —
    /// keeps values, duplicates and the null row live on every provider, not only on SQLite.
    /// </summary>
    [Theory]
    [MemberData(nameof(LiveProviders))]
    public void ReferenceScalarMaterialize_Live(string providerName)
    {
        var provider = Providers[providerName];
        SkipUnlessAvailable(provider);
        provider.EnsureSeeded();
        using var context = provider.CreateContext();

        DataContextCache.SelectListCache.Remove(typeof(string));
        var strings = context.From(StringLeft(context).UnionAll(StringRight(context))).ToList();

        strings.Should().BeEquivalentTo(new string?[] { "dadfasd", "xxx", "xxx", null });
        strings.Count(x => x == "xxx").Should().Be(2);
        strings.Count(x => x is null).Should().Be(1);
    }

    /// <summary>
    /// AC4: the existing <c>ToArray</c> and async list terminals return the same rows live. The
    /// assertion compares the sorted projections because a set operation carries no ordering
    /// guarantee — ClickHouse runs <c>UNION ALL</c> branches in parallel and returned
    /// <c>{2,3,1,2}</c> on one run of the identical tree — while duplicates stay significant, so a
    /// dropped or merged duplicate still fails.
    /// </summary>
    [Theory]
    [MemberData(nameof(LiveProviders))]
    public async Task AsyncAndArrayTerminalsMaterialize_Live(string providerName)
    {
        var provider = Providers[providerName];
        SkipUnlessAvailable(provider);
        provider.EnsureSeeded();
        using var context = provider.CreateContext();

        DataContextCache.SelectListCache.Remove(typeof(int));
        context.From(Left(context).UnionAll(Right(context))).ToArray().OrderBy(x => x).Should().Equal(1, 2, 2, 3);

        DataContextCache.SelectListCache.Remove(typeof(int));
        var list = await context.From(Left(context).UnionAll(Right(context))).ToListAsync();

        list.OrderBy(x => x).Should().Equal(1, 2, 2, 3);
    }

    private static QueryCommand<int> Left(IDataContext context)
        => context.From<ISimpleEntity>().Where(x => x.Id <= 2).Select(x => x.Id);

    private static QueryCommand<int?> NullableLeft(IDataContext context)
        => context.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => (int?)c.Id);

    private static QueryCommand<int?> NullableRight(IDataContext context)
        => context.From<IComplexEntity>().Where(c => c.Id >= 2 && c.Id <= 3).Select(c => (int?)c.Id);

    private static QueryCommand<string?> StringLeft(IDataContext context)
        => context.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => c.String);

    private static QueryCommand<string?> StringRight(IDataContext context)
        => context.From<IComplexEntity>().Where(c => c.Id >= 2 && c.Id <= 3).Select(c => c.String);

    private static QueryCommand<int> Right(IDataContext context)
        => context.From<ISimpleEntity>().Where(x => x.Id >= 2 && x.Id <= 3).Select(x => x.Id);

    private static void AssertRows(
        IDataContext context,
        Func<QueryCommand<int>, QueryCommand<int>> operation,
        params int[] expected)
        => context.From(operation(Left(context))).ToList().Should().BeEquivalentTo(expected);

    private static void SkipUnlessAvailable(ITestProvider provider)
    {
        if (provider.IsAvailable)
            return;

        if (ProviderSkipPolicy.IsOptedOut(provider.Name))
            Assert.Skip($"Provider '{provider.Name}' is explicitly opted out via {ProviderSkipPolicy.EnvironmentVariable}.");

        provider.IsAvailable.Should().BeTrue(
            $"provider '{provider.Name}' is unavailable ({provider.SkipReason}); a skipped supported provider is not parity evidence");
    }
}
