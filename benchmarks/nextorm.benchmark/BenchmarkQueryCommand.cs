using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using BenchmarkDotNet.Jobs;
using System.Linq.Expressions;
using nextorm.core;
using DbContext = nextorm.core.DbContext;

namespace nextorm.benchmark;

[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class BenchmarkQueryCommand
{
    private readonly TestDataRepository _ctx;
    // private readonly SqlDataProvider.QueryPlan _plan;
    // private readonly SqlDataProvider.QueryPlan _plan2;
    private readonly ExpressionPlanEqualityComparer _planComparer;
    private readonly Expression _condition;
    private readonly TestDataRepository _nonCacheCtx;
    // [Params(1, 2, 3, 5)]
    // public int Iterations { get; set; } = 1;
    public QueryCommand<LargeEntity> Command { get; }

    public BenchmarkQueryCommand()
    {
        var filepath = BenchDb.FilePath;

        var builder = new DbContextBuilder
        {
            //  CacheExpressions = false
        };
        builder.UseSqlite(filepath);
        _nonCacheCtx = new TestDataRepository(builder.CreateDbContext());

        builder = new DbContextBuilder
        {
            // CacheExpressions = true
        };
        builder.UseSqlite(filepath);
        _ctx = new TestDataRepository(builder.CreateDbContext());

        Command = _ctx.LargeEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(it => new LargeEntity { Id = it.Id, Str = it.Str, Dt = it.Dt });

        _condition = Command.Condition!;
        _planComparer = new ExpressionPlanEqualityComparer(Command);
    }
    // [Benchmark()]
    // public void CacheExpressions()
    // {
    //     DataContextCache.ExpressionsCache.Clear();
    //     Workload(_ctx);
    // }
    // [Benchmark()]
    // public void DontCacheExpressions()
    // {
    //     Workload(_nonCacheCtx);
    // }
    // void Workload(TestDataRepository repo)
    // {
    //     for (int i = 0; i < Iterations; i++)
    //     {
    //         var cmd = repo.LargeEntity.Where(it => it.Id == i).Select(it => new { it.Id, it.Str, it.Dt });
    //         cmd.ToEnumerable();
    //     }
    // }
    // Comparer is created once (as in production, it is cached per QueryCommand), so this
    // measures pure GetHashCode traversal without construction overhead.
    [Benchmark]
    public int ExpressionPlanEqualityComparer() => _planComparer.GetHashCode(_condition);

}
