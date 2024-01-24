using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

    // [Benchmark]
    // public void DirectCall()
    // {
    //     for (int i = 0; i < Iterations; i++)
    //     {
    //         var r = _lambda(i);
    //     }
    // }
    // [Benchmark]
    // public void DynamicCall()
    // {
    //     for (int i = 0; i < Iterations; i++)
    //     {
    //         var r = (int)_del.DynamicInvoke(i)!;
    //     }
    // }
    // [Benchmark]
    // public void Explicit()
    // {
    //     if (IsClassAndNull3(0) != 0)
    //         throw new ApplicationException();

    //     if (IsClassAndNull3(1) == 0)
    //         throw new ApplicationException();

    //     if (IsClassAndNull3("sss") == 0)
    //         throw new ApplicationException();

    //     string? s = null;
    //     if (IsClassAndNull3(s) != 0)
    //         throw new ApplicationException();
    // }
    // // [Benchmark]
    // // public void WithoutBoxing()
    // // {
    // //     _ = IsClassAndNull1(1);
    // //     _ = IsClassAndNull1("sss");
    // // }
    // [Benchmark]
    // public void Boxing()
    // {
    //     if (IsClassAndNull2(0) != 0)
    //         throw new ApplicationException();

    //     if (IsClassAndNull2(1) == 0)
    //         throw new ApplicationException();

    //     if (IsClassAndNull2("sss") == 0)
    //         throw new ApplicationException();

    //     string? s = null;
    //     if (IsClassAndNull2(s) != 0)
    //         throw new ApplicationException();
    // }
    // [Benchmark]
    // public void PatternMathing()
    // {
    //     if (IsClassAndNull4(0) != 0)
    //         throw new ApplicationException();

    //     if (IsClassAndNull4(1) == 0)
    //         throw new ApplicationException();

    //     if (IsClassAndNull4("sss") == 0)
    //         throw new ApplicationException();

    //     string? s = null;
    //     if (IsClassAndNull4(s) != 0)
    //         throw new ApplicationException();
    // }
    // private static int IsClassAndNull4<T>(T value) => value is null ? 0 : value.GetHashCode();
    // private static int IsClassAndNull3(int v) => v.GetHashCode();
    // private static int IsClassAndNull3<T>(T value)
    // {
    //     return value?.GetHashCode() ?? 0;
    // }
    // // private static int IsClassAndNull1<T>(T _) => typeof(T).IsClass && default(T) == null;
    // private static int IsClassAndNull2<T>(T value)
    // {
    //     return value?.GetHashCode() ?? 0;
    // }
    [Benchmark]
    public void SystemHashCode()
    {
        var hc = new HashCode();
        for (int i = 0; i < 100; i++)
        {
            hc.Add(i);
            hc.Add(i.ToString());
            hc.Add(Guid.NewGuid());
        }
    }
    [Benchmark]
    public void CustomHashCode()
    {
        var hc = new core.HashCode();
        for (int i = 0; i < 100; i++)
        {
            hc.Add(i);
            hc.Add(i.ToString());
            hc.Add(Guid.NewGuid());
        }
    }
}
