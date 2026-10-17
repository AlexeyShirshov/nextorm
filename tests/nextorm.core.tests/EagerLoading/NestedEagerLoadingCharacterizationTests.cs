using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T212-D1 characterization (reconnaissance only, no production behavior change). Pins the CURRENT tree's
/// observable handling of a nested (level 2+) <c>LoadWith</c> declared on a child query, in both eager
/// modes, and the parent eager-load shape / plan-key identity. Each test prints an <c>OBS</c> line so the
/// runner log is the raw observation record; the assertions encode the observed values.
/// <para>
/// D3/D4 update: the split path (D3) and the expressible single-query path (D4) now orchestrate nested
/// descendants, so the observations that recorded the pre-D3/D4 silent drop were converted to positive
/// assertions. The raw D1 outputs remain in <c>artifacts/pdca/T212/rv1/A/E01.log</c>.
/// </para>
/// </summary>
[Trait("Feature", "T212-D1")]
public sealed class NestedEagerLoadingCharacterizationTests
{
    public sealed class RootEntity
    {
        public int Id { get; set; }
        public ICollection<MiddleEntity> Middles { get; set; } = new List<MiddleEntity>();
    }

    public sealed class MiddleEntity
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public ICollection<LeafEntity> Leaves { get; set; } = new List<LeafEntity>();
    }

    public sealed class LeafEntity
    {
        public int Id { get; set; }
        public int MiddleId { get; set; }
    }

    private static InMemoryDataContext CreateContext()
    {
        var context = new InMemoryDataContext();
        context.From<RootEntity>().WithData(new[]
        {
            new RootEntity { Id = 1 },
            new RootEntity { Id = 2 },
        });
        context.From<MiddleEntity>().WithData(new[]
        {
            new MiddleEntity { Id = 10, RootId = 1 },
            new MiddleEntity { Id = 11, RootId = 1 },
            new MiddleEntity { Id = 12, RootId = 2 },
        });
        context.From<LeafEntity>().WithData(new[]
        {
            new LeafEntity { Id = 100, MiddleId = 10 },
            new LeafEntity { Id = 101, MiddleId = 10 },
            new LeafEntity { Id = 110, MiddleId = 11 },
            new LeafEntity { Id = 120, MiddleId = 12 },
        });
        return context;
    }

    private static void Observe(string label, InMemoryDataContext context, IReadOnlyList<RootEntity> roots, int nestedFactoryCalls)
    {
        var middles = roots.SelectMany(r => r.Middles).Select(m => m.Id).ToArray();
        var leaves = roots.SelectMany(r => r.Middles).SelectMany(m => m.Leaves).Select(l => l.Id).ToArray();
        Console.WriteLine(
            $"OBS {label}: middles=[{string.Join(",", middles)}] " +
            $"leaves=[{string.Join(",", leaves)}] nestedChildFactoryCalls={nestedFactoryCalls}");
    }

    [Fact]
    public void Split_Depth2_NestedLoadWith_LoadsEveryLevel()
    {
        using var context = CreateContext();
        var nestedFactoryCalls = 0;

        var roots = context.From<RootEntity>()
            .LoadWith(
                r => r.Middles,
                c => c.From<MiddleEntity>().LoadWith(
                    m => m.Leaves,
                    l =>
                    {
                        nestedFactoryCalls++;
                        return l.From<LeafEntity>();
                    },
                    m => m.Id,
                    l => l.MiddleId),
                r => r.Id,
                m => m.RootId)
            .OrderBy(r => r.Id)
            .ToList();

        Observe("split-depth2", context, roots, nestedFactoryCalls);

        roots.SelectMany(r => r.Middles).Select(m => m.Id).Should().Equal(new[] { 10, 11, 12 }, "the level-1 split child load works");
        roots.SelectMany(r => r.Middles).SelectMany(m => m.Leaves).Select(l => l.Id).Should().Equal(
            new[] { 100, 101, 110, 120 },
            "D3: the nested level-2 LoadWith is orchestrated by the split path");
        nestedFactoryCalls.Should().Be(1, "the nested child-query factory is resolved once and reused by execution");
    }

    [Fact]
    public async Task Split_Depth2_NestedLoadWith_Async_LoadsEveryLevel()
    {
        using var context = CreateContext();
        var nestedFactoryCalls = 0;

        var roots = await context.From<RootEntity>()
            .LoadWith(
                r => r.Middles,
                c => c.From<MiddleEntity>().LoadWith(
                    m => m.Leaves,
                    l =>
                    {
                        nestedFactoryCalls++;
                        return l.From<LeafEntity>();
                    },
                    m => m.Id,
                    l => l.MiddleId),
                r => r.Id,
                m => m.RootId)
            .OrderBy(r => r.Id)
            .ToListAsync();

        Observe("split-depth2-async", context, roots, nestedFactoryCalls);

        roots.SelectMany(r => r.Middles).SelectMany(m => m.Leaves).Select(l => l.Id).Should().Equal(
            new[] { 100, 101, 110, 120 },
            "D3: the async split terminal orchestrates the nested level-2 LoadWith");
        nestedFactoryCalls.Should().Be(1, "the nested child-query factory is resolved once and reused by execution");
    }

    [Fact]
    public void SingleQuery_Depth2_NestedLoadWith_LoadsEveryLevel()
    {
        using var context = CreateContext();
        var nestedFactoryCalls = 0;

        var roots = context.From<RootEntity>()
            .LoadWith(
                r => r.Middles,
                c => c.From<MiddleEntity>().LoadWith(
                    m => m.Leaves,
                    l =>
                    {
                        nestedFactoryCalls++;
                        return l.From<LeafEntity>();
                    },
                    m => m.Id,
                    l => l.MiddleId),
                r => r.Id,
                m => m.RootId,
                EagerLoadMode.SingleQuery)
            .OrderBy(r => r.Id)
            .ToList();

        Observe("single-query-depth2", context, roots, nestedFactoryCalls);

        roots.SelectMany(r => r.Middles).Select(m => m.Id).Should().Equal(new[] { 10, 11, 12 }, "the level-1 single-query child load works");
        roots.SelectMany(r => r.Middles).SelectMany(m => m.Leaves).Select(l => l.Id).Should().Equal(
            new[] { 100, 101, 110, 120 },
            "D4: the expressible nested level-2 LoadWith is assembled from the one result set");
        nestedFactoryCalls.Should().Be(1, "the nested child-query factory is resolved once and reused");
    }

    [Fact]
    public void Parent_EagerLoadShape_IgnoresTheNestedGraph()
    {
        using var context = CreateContext();

        var plain = context.From<RootEntity>()
            .LoadWith(r => r.Middles, c => c.From<MiddleEntity>(), r => r.Id, m => m.RootId)
            .ToCommand();
        var nested = context.From<RootEntity>()
            .LoadWith(
                r => r.Middles,
                c => c.From<MiddleEntity>().LoadWith(m => m.Leaves, l => l.From<LeafEntity>(), m => m.Id, l => l.MiddleId),
                r => r.Id,
                m => m.RootId)
            .ToCommand();

        Console.WriteLine(
            $"OBS parent-shape: plain={plain.EagerLoadShape.StructuralHash} nested={nested.EagerLoadShape.StructuralHash} " +
            $"equal={nested.EagerLoadShape.Equals(plain.EagerLoadShape)}");

        nested.EagerLoadShape.Equals(plain.EagerLoadShape).Should().BeTrue(
            "OBSERVED: the nested level-2 graph is invisible to the parent eager-load shape / plan key");
        nested.EagerLoadShape.StructuralHash.Should().Be(plain.EagerLoadShape.StructuralHash);
    }
}
