using BenchmarkDotNet.Attributes;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace nextorm.benchmark;

/// <summary>
/// Isolates the primitives changed by M5-M8. Their end-to-end effect is below the noise floor of
/// a query benchmark, so each pair measures the same input with the old and the new implementation.
///
/// The "old" arms reproduce exactly what the code did before the fix:
/// M5 <c>Convert.ToBoolean(object)</c>, M6 <c>Enum.HasFlag</c>, M7 culture-sensitive
/// <c>string.StartsWith(string)</c>, M8 <c>string.Format("norm_p{0}", i)</c>.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class MicroOptimizationsBenchmark
{
    private static readonly object s_boxedLongOne = 1L;
    private static readonly Type s_type = typeof(MicroOptimizationsBenchmark);
    private static readonly string s_name = "count_distinct";
    private static readonly string[] s_paramNames =
        ["norm_p0", "norm_p1", "norm_p2", "norm_p3", "norm_p4", "norm_p5", "norm_p6", "norm_p7"];

    // ---- M5: scalar conversion (SQLite returns INTEGER/long for bool) ------------

    [Benchmark(Baseline = true)]
    public bool M5_ConvertChangeType_LongToBool() => Convert.ToBoolean(s_boxedLongOne);

    [Benchmark]
    public bool M5_FastPath_LongToBool() => (long)s_boxedLongOne != 0;

    // ---- M6: TypeAttributes.HasFlag (boxes the enum) vs bitwise ------------------

    [Benchmark]
    public bool M6_HasFlag() => s_type.Attributes.HasFlag(TypeAttributes.NotPublic);

    [Benchmark]
    public bool M6_Bitwise() => (s_type.Attributes & TypeAttributes.NotPublic) != 0;

    // ---- M7: culture-sensitive vs ordinal name checks ----------------------------

    [Benchmark]
    public bool M7_StartsWith_Culture() => s_name.StartsWith("count");

    [Benchmark]
    public bool M7_StartsWith_Ordinal() => s_name.StartsWith("count", StringComparison.Ordinal);

    [Benchmark]
    public bool M7_EndsWith_Culture() => s_name.EndsWith("distinct");

    [Benchmark]
    public bool M7_EndsWith_Ordinal() => s_name.EndsWith("distinct", StringComparison.Ordinal);

    // ---- M8: parameter name construction ----------------------------------------

    [Benchmark]
    public string M8_StringFormat() => string.Format("norm_p{0}", 7);

    [Benchmark]
    public string M8_CachedLookup() => s_paramNames[7];

    // ---- M10: sealed vs unsealed through an interface reference -------------------
    // A sealed implementation lets the JIT devirtualize an otherwise-interface call.

    private static readonly UnsealedCounter s_unsealed = new();
    private static readonly SealedCounter s_sealed = new();

    [Benchmark]
    public int M10_Unsealed_InterfaceCall()
    {
        ICounter c = s_unsealed;
        var sum = 0;
        for (var i = 0; i < 100; i++) sum += c.Next();
        return sum;
    }

    [Benchmark]
    public int M10_Sealed_InterfaceCall()
    {
        ICounter c = s_sealed;
        var sum = 0;
        for (var i = 0; i < 100; i++) sum += c.Next();
        return sum;
    }

    // ---- M11: LINQ Any / Select().ToArray() vs loops (plan/map build) ------------

    private static readonly List<int> s_ints = [1, 2, 3, -4, 5];

    [Benchmark]
    public bool M11_Linq_Any() => s_ints.Any(x => x < 0);

    [Benchmark]
    public bool M11_Loop_Any()
    {
        for (var i = 0; i < s_ints.Count; i++)
            if (s_ints[i] < 0) return true;
        return false;
    }

    [Benchmark]
    public int[] M11_Linq_SelectToArray() => s_ints.Select(x => x * 2).ToArray();

    [Benchmark]
    public int[] M11_Loop_SelectToArray()
    {
        var result = new int[s_ints.Count];
        for (var i = 0; i < s_ints.Count; i++) result[i] = s_ints[i] * 2;
        return result;
    }

    // ---- M3: per-row ReadAsync fast path ------------------------------------------
    // Old shape: `async ValueTask<bool> MoveNextAsync()` awaiting Task<bool>.ReadAsync per row.
    // New shape: stay synchronous when the read task is already completed.

    private const int RowCount = 100;
    private static readonly Task<bool> s_completedRead = Task.FromResult(true);

    private static async ValueTask<bool> ReadRowOld()
    {
        if (await s_completedRead.ConfigureAwait(false)) return true;
        return false;
    }

    private static ValueTask<bool> ReadRowNew()
    {
        var task = s_completedRead;
        if (task.IsCompletedSuccessfully) return new ValueTask<bool>(task.Result);
        return ReadRowSlow(task);
    }

    private static async ValueTask<bool> ReadRowSlow(Task<bool> task) => await task.ConfigureAwait(false);

    [Benchmark]
    public bool M3_Old_AwaitPerRow()
    {
        var last = false;
        for (var i = 0; i < RowCount; i++) last = ReadRowOld().GetAwaiter().GetResult();
        return last;
    }

    [Benchmark]
    public bool M3_New_FastPathPerRow()
    {
        var last = false;
        for (var i = 0; i < RowCount; i++) last = ReadRowNew().GetAwaiter().GetResult();
        return last;
    }

    // ---- M12: [AggressiveInlining] on a forwarder vs on a leaf with a small loop -------
    // A one-line forwarder is already inlined by the JIT, so the attribute should be a no-op
    // there. A leaf that contains a small loop is where it actually changes the decision: the
    // JIT declines loops by default, the attribute overrides that. Both pairs are semantically
    // identical within the pair, so the delta is the attribute and nothing else.

    private static int Leaf(int x) => x + 1;
    private static int ForwarderNoAttribute(int x) => Leaf(x);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ForwarderWithAttribute(int x) => Leaf(x);

    private static int LoopLeafNoAttribute(int x)
    {
        var count = 0;
        for (var i = 0; i < 4; i++)
            if ((x & (1 << i)) != 0) count++;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int LoopLeafWithAttribute(int x)
    {
        var count = 0;
        for (var i = 0; i < 4; i++)
            if ((x & (1 << i)) != 0) count++;
        return count;
    }

    [Benchmark]
    public int M12_Forwarder_NoAttribute()
    {
        var sum = 0;
        for (var i = 0; i < 1000; i++) sum += ForwarderNoAttribute(i);
        return sum;
    }

    [Benchmark]
    public int M12_Forwarder_AggressiveInlining()
    {
        var sum = 0;
        for (var i = 0; i < 1000; i++) sum += ForwarderWithAttribute(i);
        return sum;
    }

    [Benchmark]
    public int M12_LoopLeaf_NoAttribute()
    {
        var sum = 0;
        for (var i = 0; i < 1000; i++) sum += LoopLeafNoAttribute(i);
        return sum;
    }

    [Benchmark]
    public int M12_LoopLeaf_AggressiveInlining()
    {
        var sum = 0;
        for (var i = 0; i < 1000; i++) sum += LoopLeafWithAttribute(i);
        return sum;
    }
}

public interface ICounter
{
    int Next();
}

public class UnsealedCounter : ICounter
{
    public int Next() => 1;
}

public sealed class SealedCounter : ICounter
{
    public int Next() => 1;
}
