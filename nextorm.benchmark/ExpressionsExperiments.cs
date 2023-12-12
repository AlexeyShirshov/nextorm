using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace nextorm.benchmark;

[MemoryDiagnoser]
public class ExpressionsExperiments
{
    const int Iterations = 10;
    private readonly Delegate _del;
    private readonly Func<int, int> _lambda;

    public ExpressionsExperiments()
    {
        var testMI = GetType().GetMethod(nameof(Test), BindingFlags.Public | BindingFlags.Instance)!;
        var @this = Expression.Constant(this);
        var p = Expression.Parameter(typeof(int));
        var call = Expression.Call(@this, testMI, p);
        _del = Expression.Lambda(call, p).Compile();

        _lambda = Expression.Lambda<Func<int, int>>(call, p).Compile();
    }
    public int Test(int i) => i + 10;

    [Benchmark]
    public void DirectCall()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var r = _lambda(i);
        }
    }
    [Benchmark]
    public void DynamicCall()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var r = (int)_del.DynamicInvoke(i)!;
        }
    }
}