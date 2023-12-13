using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using BenchmarkDotNet.Jobs;
using nextorm.core;
using DbContext = nextorm.core.DbContext;

namespace nextorm.benchmark;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
public class BenchmarkQueryCommand
{
    private readonly TestDataRepository _ctx;
    private readonly DbContext _provider;
    private readonly QueryCommand _cmd;
    // private readonly SqlDataProvider.QueryPlan _plan;
    // private readonly SqlDataProvider.QueryPlan _plan2;
    private readonly ExpressionEqualityComparer _eq;
    private readonly ExpressionPlanEqualityComparer _eqPlan;

    public BenchmarkQueryCommand()
    {
        var builder = new DbContextBuilder();
        builder.UseSqlite(Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db"));
        _ctx = new TestDataRepository(builder.CreateDbContext());

        _provider = (DbContext)_ctx.DbContext;

        var p = 10;
        _cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
        _cmd.PrepareCommand(CancellationToken.None);

        // _plan = new SqlDataProvider.QueryPlan(_cmd);
        // _plan2 = new SqlDataProvider.QueryPlan(_cmd);

        _eq = ExpressionEqualityComparer.Instance;
        _eqPlan = new ExpressionPlanEqualityComparer(_provider.ExpressionsCache, null);
    }
    // [Benchmark(Baseline = true)]
    // public void WhereEqualityComparer()
    // {
    //     _eq.GetHashCode(_cmd.Condition);
    //     _eq.Equals(_cmd.Condition, _cmd.Condition);
    // }
    // [Benchmark()]
    // public void WherePlanEqualityComparer()
    // {
    //     _eqPlan.GetHashCode(_cmd.Condition);
    //     _eqPlan.Equals(_cmd.Condition, _cmd.Condition);
    // }
    // [Benchmark()]
    // public void SelectEqualityComparer()
    // {
    //     _eq.GetHashCode(_cmd.SelectExpression);
    //     _eq.Equals(_cmd.SelectExpression, _cmd.SelectExpression);
    // }
    // [Benchmark()]
    // public void SelectPlanEqualityComparer()
    // {
    //     _eqPlan.GetHashCode(_cmd.SelectExpression);
    //     _eqPlan.Equals(_cmd.SelectExpression, _cmd.SelectExpression);
    // }
    [Benchmark()]
    public void StandardEqualityComparer()
    {
        _cmd.GetHashCode();
        _cmd.Equals(_cmd);
    }
    // [Benchmark()]
    // public void PlanEqualityComparer()
    // {
    //     _plan.GetHashCode();
    //     _plan.Equals(_plan2);
    // }
}
