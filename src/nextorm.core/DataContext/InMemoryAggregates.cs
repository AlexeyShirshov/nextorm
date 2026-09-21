using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// The runtime type arguments an in-memory aggregate is closed over. Bundled so
/// <see cref="InMemoryAggregates.Compute"/> takes one value instead of three trailing type
/// parameters.
/// </summary>
/// <param name="EntityType">Type of the rows the aggregate folds.</param>
/// <param name="ResultType">Type the aggregate returns to the projection.</param>
/// <param name="ValueType">Type of the selected value the fold operates on.</param>
internal readonly record struct AggregateTypeInfo(Type EntityType, Type ResultType, Type ValueType);

/// <summary>
/// Evaluates the aggregate functions that <c>EntityBuilder</c> wraps in <see cref="CommonFunctions"/>
/// method calls (<c>Sum</c>, <c>Min</c>, <c>Max</c>, <c>Avg</c>, <c>Count</c>, <c>Stdev</c>,
/// <c>Var</c>). SQL providers render those calls to SQL; the in-memory provider computes them here
/// over the attached data instead of mapping each row through a no-op aggregate.
/// </summary>
/// <remarks>
/// The fold is generic in the value type so a column selector does not box every row
/// (<c>Func&lt;TEntity, int&gt;</c> instead of <c>Func&lt;TEntity, object?&gt;</c>), which keeps the
/// in-memory aggregate close to raw LINQ. Callers resolve the closed method through
/// <see cref="Compute(object, string, Delegate?, AggregateTypeInfo)"/>; the compiled selector is
/// cached by the caller.
/// </remarks>
internal static class InMemoryAggregates
{
    private static readonly HashSet<string> AggregateNames = new(StringComparer.Ordinal)
    {
        nameof(CommonFunctions.min),
        nameof(CommonFunctions.max),
        nameof(CommonFunctions.sum),
        nameof(CommonFunctions.avg),
        nameof(CommonFunctions.count),
        nameof(CommonFunctions.stdev),
        nameof(CommonFunctions.stdevp),
        nameof(CommonFunctions.var),
        nameof(CommonFunctions.varp),
        "min_distinct",
        "max_distinct",
        "sum_distinct",
        "avg_distinct",
        "count_distinct",
        "stdev_distinct",
        "stdevp_distinct",
        "var_distinct",
        "varp_distinct",
    };

    private static readonly MethodInfo ComputeTypedMI =
        typeof(InMemoryAggregates).GetMethod(nameof(ComputeTyped), BindingFlags.Public | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<(Type Entity, Type Result, Type Value), MethodInfo> MethodCache = new();

    public static bool IsAggregate(string name) => AggregateNames.Contains(name);

    /// <summary>Compiles the value selector as <c>Func&lt;TEntity, TValue&gt;</c> (unboxed fold).</summary>
    public static Delegate CompileSelector<TEntity>(Expression body, ParameterExpression parameter, Type valueType)
    {
        var funcType = typeof(Func<,>).MakeGenericType(typeof(TEntity), valueType);
        return Expression.Lambda(funcType, body, parameter).Compile();
    }

    /// <summary>
    /// Reflection entry used by the in-memory provider and the grouped-projection rewrite, which only
    /// know the selector value type at runtime.
    /// </summary>
    public static object? Compute(object data, string name, Delegate? selector, AggregateTypeInfo types)
    {
        var method = MethodCache.GetOrAdd(
            (types.EntityType, types.ResultType, types.ValueType),
            static key => ComputeTypedMI.MakeGenericMethod(key.Entity, key.Result, key.Value));

        try
        {
            return method.Invoke(null, [data, name, selector]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    public static TResult? ComputeTyped<TEntity, TResult, TValue>(IEnumerable<TEntity> data, string name, Func<TEntity, TValue>? selector)
    {
        var distinct = name.EndsWith("_distinct", StringComparison.Ordinal);
        var normalized = distinct ? name[..name.IndexOf('_')] : name;
        var resultType = Nullable.GetUnderlyingType(typeof(TResult)) ?? typeof(TResult);

        if (normalized == nameof(CommonFunctions.count))
        {
            long count = 0;
            if (selector is null)
            {
                foreach (var _ in data) count++;
            }
            else if (distinct)
            {
                var seen = new HashSet<TValue>();
                foreach (var row in data)
                {
                    var value = selector(row);
                    if (value is not null && seen.Add(value)) count++;
                }
            }
            else
            {
                foreach (var row in data)
                    if (selector(row) is not null) count++;
            }

            return (TResult)Convert.ChangeType(count, resultType, CultureInfo.InvariantCulture);
        }

        if (selector is null)
            throw new NotSupportedException($"Aggregate '{name}' requires a value selector in the in-memory provider.");

        if (normalized is nameof(CommonFunctions.min) or nameof(CommonFunctions.max))
        {
            var comparer = Comparer<TValue>.Default;
            HashSet<TValue>? seen = distinct ? [] : null;
            var hasValue = false;
            TValue best = default!;

            foreach (var row in data)
            {
                var value = selector(row);
                if (value is null) continue;
                if (seen is not null && !seen.Add(value)) continue;

                if (!hasValue)
                {
                    best = value;
                    hasValue = true;
                }
                else
                {
                    var cmp = comparer.Compare(value, best);
                    if ((normalized == nameof(CommonFunctions.min) && cmp < 0)
                        || (normalized == nameof(CommonFunctions.max) && cmp > 0))
                        best = value;
                }
            }

            return hasValue ? (TResult)Convert.ChangeType(best!, resultType, CultureInfo.InvariantCulture) : default;
        }

        // Numeric fold over sum and sum-of-squares in a single pass (no per-row materialisation).
        HashSet<TValue>? distinctValues = distinct ? [] : null;
        double sum = 0, sumSquares = 0;
        long count2 = 0;
        foreach (var row in data)
        {
            var value = selector(row);
            if (value is null) continue;
            if (distinctValues is not null && !distinctValues.Add(value)) continue;

            // Convert.ToDouble(object, provider) has no primitive overload, so it would box every row;
            // the compiled converter keeps the fold allocation-free.
            var d = DoubleConverter<TValue>.Convert(value);
            sum += d;
            sumSquares += d * d;
            count2++;
        }

        if (count2 == 0)
        {
            // SQL SUM over an empty set is NULL; the CLR default (0) is the closest mapping and what
            // callers of a non-nullable TResult expect. MIN/MAX/AVG/STDEV/VAR stay default.
            return normalized == nameof(CommonFunctions.sum)
                ? (TResult)Convert.ChangeType(0d, resultType, CultureInfo.InvariantCulture)
                : default;
        }

        var centre = sumSquares - (sum * sum / count2);
        var result = normalized switch
        {
            nameof(CommonFunctions.sum) => sum,
            nameof(CommonFunctions.avg) => sum / count2,
            nameof(CommonFunctions.var) => count2 < 2 ? 0 : centre / (count2 - 1),
            nameof(CommonFunctions.varp) => centre / count2,
            nameof(CommonFunctions.stdev) => count2 < 2 ? 0 : Math.Sqrt(centre / (count2 - 1)),
            nameof(CommonFunctions.stdevp) => Math.Sqrt(centre / count2),
            _ => throw new NotSupportedException($"Aggregate '{name}' is not supported by the in-memory provider."),
        };

        return (TResult)Convert.ChangeType(result, resultType, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Per-closed-type numeric converter compiled once. Unconstrained generic code cannot select the
    /// primitive <c>Convert.ToDouble</c> overloads, so folding through <c>Convert</c> would box each
    /// value; the compiled expression avoids that.
    /// </summary>
    private static class DoubleConverter<T>
    {
        public static readonly Func<T, double> Convert = Build();

        private static Func<T, double> Build()
        {
            var parameter = Expression.Parameter(typeof(T));
            Expression body = Nullable.GetUnderlyingType(typeof(T)) is { } underlying && underlying != typeof(T)
                ? Expression.Convert(Expression.Property(parameter, "Value"), typeof(double))
                : Expression.Convert(parameter, typeof(double));
            return Expression.Lambda<Func<T, double>>(body, parameter).Compile();
        }
    }
}

/// <summary>
/// Single-value enumerator used for in-memory aggregates: the aggregate is already computed, so the
/// sequence yields exactly one row.
/// </summary>
internal sealed class InMemoryScalarEnumerator<T> : IAsyncEnumerator<T>, IEnumerator<T>, IEnumerable<T>
{
    private readonly T _value;
    private bool _moved;

    public InMemoryScalarEnumerator(T value) => _value = value;

    public T Current => _value;
    object? IEnumerator.Current => _value;

    public bool MoveNext()
    {
        if (_moved) return false;
        _moved = true;
        return true;
    }

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(MoveNext());

    public void Reset() => _moved = false;

    public void Dispose() => GC.SuppressFinalize(this);

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public IEnumerator<T> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => this;
}
