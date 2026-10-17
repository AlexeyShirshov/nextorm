using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;

namespace NextORM.Benchmark;

/// <summary>
/// #148-B R2.7 investigation: the r2.3 in-memory navigation surface — whole-reference projection
/// (<c>n.Parent</c>, resolved through the metadata binding, never the CLR graph) and the multi-hop
/// reference chain (<c>n.Parent.Parent.Name</c>, walked hop-by-hop with absence at every hop). Each arm
/// executes for real (the constructor fails loudly on an empty result). Deliberately carries no
/// <c>acceptance</c> category so it can never perturb the 7-case acceptance gate.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class InMemoryBenchmarkImplicitNavigation
{
    private const int Iterations = 100;
    private const int NodeCount = 2000;

    private readonly InMemoryDataContext _ctx;

    public InMemoryBenchmarkImplicitNavigation()
    {
        _ctx = new InMemoryDataContext();
        _ctx.From<BenchNavNode>(b => b.HasOne(n => n.Parent, n => n.ParentId));

        var rows = new List<BenchNavNode>(NodeCount);
        for (var i = 1; i <= NodeCount; i++)
            rows.Add(new BenchNavNode { Id = i, ParentId = i == 1 ? 0 : i - 1, Name = "n" + i });
        _ctx.From<BenchNavNode>().WithData(rows);

        if (Chain_Implicit_Warm() <= 0 || Whole_Implicit_Warm() <= 0)
            throw new InvalidOperationException("An implicit-navigation benchmark arm returned no rows.");
    }

    // Warm (execution): plan cache absorbs the rewrite; the chain walker / whole-ref evaluator run.
    [Benchmark]
    public int Chain_Implicit_Warm()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
            foreach (var row in _ctx.From<BenchNavNode>()
                .Select(n => new { n.Id, GrandParent = n.Parent!.Parent!.Name })
                .ToList())
                sum += row.GrandParent is null ? 0 : 1;
        return sum;
    }

    [Benchmark]
    public int Whole_Implicit_Warm()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
            foreach (var row in _ctx.From<BenchNavNode>()
                .Select(n => new { n.Id, Parent = n.Parent })
                .ToList())
                sum += row.Parent?.Id ?? 0;
        return sum;
    }

    // Cold: SQL/plan regeneration every iteration (storeInCache:false) so the navigation lowering runs.
    [Benchmark]
    public int Chain_Implicit_Cold()
    {
        var built = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = _ctx.From<BenchNavNode>().Select(n => new { n.Id, GrandParent = n.Parent!.Parent!.Name });
            _ = _ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
            built++;
        }
        return built;
    }

    [Benchmark]
    public int Whole_Implicit_Cold()
    {
        var built = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = _ctx.From<BenchNavNode>().Select(n => new { n.Id, Parent = n.Parent });
            _ = _ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
            built++;
        }
        return built;
    }
}

[SqlTable("bench_nav_node")]
public sealed class BenchNavNode
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public BenchNavNode? Parent { get; set; }
}
