using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;

namespace NextORM.Benchmark;

/// <summary>
/// D01 (#213) in-memory <c>JoinInto</c> harness: the denormalized assembly paths the 7-case acceptance
/// suite does not exercise. A scalar one-to-many declaration and a scalar many-to-many declaration
/// (junction + occurrence dedup) are measured against a fixed in-memory dataset, so the D02/D03
/// per-row assembly changes have a comparable before/after reading. Deliberately carries no
/// <c>acceptance</c> category, so it can never perturb the acceptance perf gate.
/// </summary>
/// <remarks>
/// Run explicitly with <c>--filter "*JoinIntoBenchmarks*"</c>. Each measured method loops
/// <see cref="InternalIterations"/> times over the same prepared/cached context, so the plan cache is
/// warm; the returned child total keeps the JIT from eliminating the work and proves the join returned
/// a non-degenerate result.
/// </remarks>
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class JoinIntoBenchmarks
{
    private const int ParentCount = 500;
    private const int ChildrenPerParent = 4;
    private const int InternalIterations = 5;

    private readonly InMemoryDataContext _ctx = new();

    public JoinIntoBenchmarks()
    {
        var parents = new List<JoinIntoBenchParent>(ParentCount);
        var children = new List<JoinIntoBenchChild>(ParentCount * ChildrenPerParent);
        var tags = new List<JoinIntoBenchTag>(ChildrenPerParent);
        var links = new List<JoinIntoBenchLink>(ParentCount * ChildrenPerParent);

        for (var p = 1; p <= ParentCount; p++)
        {
            parents.Add(new JoinIntoBenchParent { Id = p });

            for (var c = 0; c < ChildrenPerParent; c++)
            {
                var childId = (p * 10) + c;
                children.Add(new JoinIntoBenchChild { Id = childId, ParentId = p });
                links.Add(new JoinIntoBenchLink { ParentId = p, ChildId = c + 1 });
            }
        }

        for (var c = 1; c <= ChildrenPerParent; c++)
            tags.Add(new JoinIntoBenchTag { Id = c });

        _ctx.From<JoinIntoBenchParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasManyThrough<JoinIntoBenchTag, JoinIntoBenchLink, int, int>(
                    p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.ChildId))
            .WithData(parents);
        _ctx.From<JoinIntoBenchChild>().WithData(children);
        _ctx.From<JoinIntoBenchTag>().WithData(tags);
        _ctx.From<JoinIntoBenchLink>().WithData(links);

        // Fail loudly if the harness ever stops stitching: a degenerate empty result is not a baseline.
        if (JoinInto_OneToMany_ToList() <= 0 || JoinInto_ManyToMany_ToList() <= 0)
            throw new InvalidOperationException("The in-memory JoinInto benchmark returned no children.");
    }

    /// <summary>Scalar one-to-many: parent dedup + child grouping/materialization from denormalized rows.</summary>
    [Benchmark(Baseline = true)]
    public int JoinInto_OneToMany_ToList()
    {
        var total = 0;
        for (var i = 0; i < InternalIterations; i++)
        {
            var parents = _ctx.From<JoinIntoBenchParent>()
                .JoinInto(_ctx.From<JoinIntoBenchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .ToList();
            total += parents.Sum(p => p.Children.Count);
        }

        return total;
    }

    /// <summary>Scalar many-to-many: junction projection + occurrence dedup + full-key grouping.</summary>
    [Benchmark]
    public int JoinInto_ManyToMany_ToList()
    {
        var total = 0;
        for (var i = 0; i < InternalIterations; i++)
        {
            var parents = _ctx.From<JoinIntoBenchParent>()
                .JoinInto(_ctx.From<JoinIntoBenchTag>(), (p, t) => true, p => p.Tags)
                .ToList();
            total += parents.Sum(p => p.Tags.Count);
        }

        return total;
    }
}

public sealed class JoinIntoBenchParent
{
    public int Id { get; set; }
    public ICollection<JoinIntoBenchChild> Children { get; set; } = new List<JoinIntoBenchChild>();
    public ICollection<JoinIntoBenchTag> Tags { get; set; } = new List<JoinIntoBenchTag>();
}

public sealed class JoinIntoBenchChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
}

public sealed class JoinIntoBenchTag
{
    public int Id { get; set; }
}

public sealed class JoinIntoBenchLink
{
    public int ParentId { get; set; }
    public int ChildId { get; set; }
}
