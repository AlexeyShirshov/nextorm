using System.Linq.Expressions;
using FluentAssertions;

namespace NextORM.Core.Tests;

public class ExpressionPlanEqualityComparerTests
{
    private readonly ExpressionPlanEqualityComparer _sut;

    public ExpressionPlanEqualityComparerTests()
    {
        _sut = new ExpressionPlanEqualityComparer(new QueryProvider());
    }

    [Fact]
    public void GetHashCode_ShouldBeEquals()
    {
        var (k1, k2) = (10, 20);
        // Given
        Expression exp1 = (int i) => i + k1 + k2;
        var h1 = _sut.GetHashCode(exp1);

        (k1, k2) = (30, 40);
        Expression exp2 = (int i) => i + k1 + k2;

        h1.Should().Be(_sut.GetHashCode(exp2));
    }

    [Fact]
    public void GetHashCode_ShouldBeThreadSafe()
    {
        var sut = new ExpressionPlanEqualityComparer(new QueryProvider());
        var param = Expression.Parameter(typeof(int), "i");
        var expressions = Enumerable.Range(0, 8)
            .Select(i => (Expression)Expression.Lambda<Func<int, int>>(
                Expression.Add(param, Expression.Constant(i)), param))
            .ToArray();

        var expected = expressions.Select(sut.GetHashCode).ToArray();
        var mismatches = 0;

        Parallel.For(0, 50_000, i =>
        {
            var idx = i % expressions.Length;
            if (sut.GetHashCode(expressions[idx]) != expected[idx])
                Interlocked.Increment(ref mismatches);
        });

        // With a single visitor accumulating one XxHash32, concurrent calls interleaved their
        // writes and returned hashes that didn't match the single-threaded result. One visitor
        // per (thread, comparer) makes the result deterministic.
        mismatches.Should().Be(0);
    }

    /// <summary>
    /// <see cref="ExpressionPlanEqualityComparer.Equals(Expression?, Expression?)"/> allocates one
    /// parameter scope per call, so a shared comparer must let threads compare nested lambdas (which
    /// spill past the inline capacity of 4) concurrently with mixed true/false outcomes. Only the
    /// hash path was covered before, and the hash visitor is thread-static by design; this pins the
    /// structural-comparison path under contention.
    /// </summary>
    [Fact]
    public void Equals_ShouldBeThreadSafe_WithNestedLambdas()
    {
        var sut = new ExpressionPlanEqualityComparer(new QueryProvider());
        var equalLeft = Nested(1);
        var equalRight = Nested(1);
        var differentLeft = Nested(1);
        var differentRight = Nested(2);

        sut.Equals(equalLeft, equalRight).Should().BeTrue();
        sut.Equals(differentLeft, differentRight).Should().BeFalse();

        const int workers = 4;
        using var barrier = new Barrier(workers);
        var mismatches = 0;

        var threads = Enumerable.Range(0, workers)
            .Select(_ => new Thread(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 5_000; i++)
                {
                    if (!sut.Equals(equalLeft, equalRight))
                    {
                        Interlocked.Increment(ref mismatches);
                    }

                    if (sut.Equals(differentLeft, differentRight))
                    {
                        Interlocked.Increment(ref mismatches);
                    }
                }
            }))
            .ToArray();

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        mismatches.Should().Be(0);
    }

    private static Expression Nested(int seed)
    {
        var o0 = Expression.Parameter(typeof(int), "o0");
        var o1 = Expression.Parameter(typeof(int), "o1");
        var o2 = Expression.Parameter(typeof(int), "o2");
        var i0 = Expression.Parameter(typeof(int), "i0");
        var i1 = Expression.Parameter(typeof(int), "i1");
        var i2 = Expression.Parameter(typeof(int), "i2");

        var inner = Expression.Lambda<Func<int, int, int, int>>(
            Expression.Add(Expression.Add(i0, i1), Expression.Add(i2, Expression.Constant(seed))),
            i0, i1, i2);

        return Expression.Lambda<Func<int, int, int, int>>(
            Expression.Add(Expression.Invoke(inner, o0, o1, o2), o0), o0, o1, o2);
    }
}
