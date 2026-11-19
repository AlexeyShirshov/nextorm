using System.Collections;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

public class InMemoryEnumeratorProjectionCacheTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Enumerator_DistinctCachesProjectionForRepeatedCurrentAndDuplicateRows(bool useAsyncMove)
    {
        var mapCalls = 0;
        var query = CreateQuery<int, Projection>(value =>
        {
            mapCalls++;
            return new Projection(value);
        });
        var sut = new InMemoryEnumerator<Projection, int>(query, default, distinct: true);
        sut.Init([1, 1, 2], null);

        (await MoveNext(sut, useAsyncMove)).Should().BeTrue();
        mapCalls.Should().Be(1);

        var first = sut.Current;
        sut.Current.Should().BeSameAs(first);
        ((IEnumerator)sut).Current.Should().BeSameAs(first);
        mapCalls.Should().Be(1);

        (await MoveNext(sut, useAsyncMove)).Should().BeTrue();
        mapCalls.Should().Be(3);
        var second = sut.Current;
        second.Value.Should().Be(2);
        second.Should().NotBeSameAs(first);
        ((IEnumerator)sut).Current.Should().BeSameAs(second);
        mapCalls.Should().Be(3);

        (await MoveNext(sut, useAsyncMove)).Should().BeFalse();
        mapCalls.Should().Be(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Enumerator_NonDistinctMappingIsLazyAndCurrentIsCached(bool useAsyncMove)
    {
        var mapCalls = 0;
        var query = CreateQuery<int, Projection>(value =>
        {
            mapCalls++;
            return new Projection(value);
        });
        var sut = new InMemoryEnumerator<Projection, int>(query, default);
        sut.Init([4], null);

        (await MoveNext(sut, useAsyncMove)).Should().BeTrue();
        mapCalls.Should().Be(0);

        var first = sut.Current;
        sut.Current.Should().BeSameAs(first);
        ((IEnumerator)sut).Current.Should().BeSameAs(first);
        mapCalls.Should().Be(1);
    }

    [Fact]
    public void Enumerator_IdentityProjectionKeepsDirectSourcePath()
    {
        var mapCalls = 0;
        var query = CreateQuery<int, int>(value =>
        {
            mapCalls++;
            return value + 1;
        });
        var sut = new InMemoryEnumerator<int, int>(query, default);
        sut.Init([4], null);

        sut.MoveNext().Should().BeTrue();

        sut.Current.Should().Be(4);
        ((IEnumerator)sut).Current.Should().Be(4);
        mapCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Adapter_DistinctCachesProjectionForRepeatedCurrentAndDuplicateRows(bool useAsyncMove)
    {
        var mapCalls = 0;
        var query = CreateQuery<int, Projection>(value =>
        {
            mapCalls++;
            return new Projection(value);
        });
        var inner = new DualEnumerator<int>([1, 1, 2]);
        var sut = new InMemoryEnumeratorAdapter<Projection, int>(query, inner, distinct: true);

        (await MoveNext(sut, useAsyncMove)).Should().BeTrue();
        mapCalls.Should().Be(1);

        var first = sut.Current;
        sut.Current.Should().BeSameAs(first);
        ((IEnumerator)sut).Current.Should().BeSameAs(first);
        mapCalls.Should().Be(1);

        (await MoveNext(sut, useAsyncMove)).Should().BeTrue();
        mapCalls.Should().Be(3);
        var second = sut.Current;
        second.Value.Should().Be(2);
        second.Should().NotBeSameAs(first);
        ((IEnumerator)sut).Current.Should().BeSameAs(second);
        mapCalls.Should().Be(3);

        (await MoveNext(sut, useAsyncMove)).Should().BeFalse();
        mapCalls.Should().Be(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Adapter_NonDistinctMappingIsLazyAndCurrentIsCached(bool useAsyncMove)
    {
        var mapCalls = 0;
        var query = CreateQuery<int, Projection>(value =>
        {
            mapCalls++;
            return new Projection(value);
        });
        var inner = new DualEnumerator<int>([4]);
        var sut = new InMemoryEnumeratorAdapter<Projection, int>(query, inner);

        (await MoveNext(sut, useAsyncMove)).Should().BeTrue();
        mapCalls.Should().Be(0);

        var first = sut.Current;
        sut.Current.Should().BeSameAs(first);
        ((IEnumerator)sut).Current.Should().BeSameAs(first);
        mapCalls.Should().Be(1);
    }

    [Fact]
    public async Task Adapter_SuspendingAsyncOnlySourceRejectsSyncMoveBeforeAdvancingThenDistinctAsyncMoveWorks()
    {
        var sourceMoves = 0;
        var mapCalls = 0;
        var query = CreateQuery<int, Projection>(value =>
        {
            mapCalls++;
            return new Projection(value);
        });

        async IAsyncEnumerable<int> SuspendedSource()
        {
            await Task.Yield();
            sourceMoves++;
            yield return 1;

            await Task.Yield();
            sourceMoves++;
            yield return 1;

            await Task.Yield();
            sourceMoves++;
            yield return 2;
        }

        var inner = SuspendedSource().GetAsyncEnumerator(TestContext.Current.CancellationToken);
        (inner is IEnumerator<int>).Should().BeFalse();
        var sut = new InMemoryEnumeratorAdapter<Projection, int>(query, inner, distinct: true);

        var syncMove = () => sut.MoveNext();

        syncMove.Should().Throw<NotSupportedException>();
        sourceMoves.Should().Be(0);
        mapCalls.Should().Be(0);

        (await sut.MoveNextAsync()).Should().BeTrue();
        sourceMoves.Should().Be(1);
        mapCalls.Should().Be(1);

        var first = sut.Current;
        sut.Current.Should().BeSameAs(first);
        ((IEnumerator)sut).Current.Should().BeSameAs(first);
        mapCalls.Should().Be(1);

        (await sut.MoveNextAsync()).Should().BeTrue();
        sourceMoves.Should().Be(3);
        mapCalls.Should().Be(3);
        var second = sut.Current;
        second.Value.Should().Be(2);
        second.Should().NotBeSameAs(first);
        ((IEnumerator)sut).Current.Should().BeSameAs(second);
        mapCalls.Should().Be(3);

        (await sut.MoveNextAsync()).Should().BeFalse();
        sourceMoves.Should().Be(3);
        mapCalls.Should().Be(3);
        await sut.DisposeAsync();
    }

    [Fact]
    public void Enumerator_InitRebindInvalidatesProjectionAcrossListArrayAndIteratorSources()
    {
        var mapCalls = 0;
        var query = CreateQuery<int, Projection>(value =>
        {
            mapCalls++;
            return new Projection(value);
        });
        var sut = new InMemoryEnumerator<Projection, int>(query, default);

        AssertReboundSource(sut, new List<int> { 1 }, 1, ref mapCalls);
        AssertReboundSource(sut, new[] { 2 }, 2, ref mapCalls);
        AssertReboundSource(sut, Enumerable.Range(3, 1).Where(static _ => true), 3, ref mapCalls);
    }

    [Fact]
    public void Enumerator_EmptyAndEndDoNotInvokeSelector()
    {
        var mapCalls = 0;
        var query = CreateQuery<int, Projection>(value =>
        {
            mapCalls++;
            return new Projection(value);
        });
        var sut = new InMemoryEnumerator<Projection, int>(query, default);
        sut.Init(Array.Empty<int>(), null);

        sut.MoveNext().Should().BeFalse();
        mapCalls.Should().Be(0);

        sut.Init([7], null);
        sut.MoveNext().Should().BeTrue();
        sut.Current.Value.Should().Be(7);
        sut.MoveNext().Should().BeFalse();
        mapCalls.Should().Be(1);
    }

    [Fact]
    public void Enumerator_ResetRemainsNoOpWithoutInvalidatingCurrentProjection()
    {
        var mapCalls = 0;
        var query = CreateQuery<int, Projection>(value =>
        {
            mapCalls++;
            return new Projection(value);
        });
        var sut = new InMemoryEnumerator<Projection, int>(query, default);
        sut.Init([1, 2], null);
        sut.MoveNext().Should().BeTrue();
        var first = sut.Current;

        sut.Reset();

        sut.Current.Should().BeSameAs(first);
        mapCalls.Should().Be(1);
        sut.MoveNext().Should().BeTrue();
        sut.Current.Value.Should().Be(2);
        mapCalls.Should().Be(2);
    }

    [Fact]
    public void Adapter_ResetContinuesToThrowNotSupportedException()
    {
        var query = CreateQuery<int, Projection>(static value => new Projection(value));
        var sut = new InMemoryEnumeratorAdapter<Projection, int>(query, new DualEnumerator<int>([1]));

        var act = () => sut.Reset();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Enumerator_SelectorFailureIsNotCachedAndCanBeRetried()
    {
        var mapCalls = 0;
        var query = CreateQuery<int, Projection>(value =>
        {
            if (++mapCalls == 1)
                throw new InvalidOperationException("projection failed");

            return new Projection(value);
        });
        var sut = new InMemoryEnumerator<Projection, int>(query, default);
        sut.Init([5], null);
        sut.MoveNext().Should().BeTrue();

        FluentActions.Invoking(() => _ = sut.Current).Should().Throw<InvalidOperationException>();

        var projection = sut.Current;
        sut.Current.Should().BeSameAs(projection);
        mapCalls.Should().Be(2);
    }

    [Fact]
    public void Adapter_SelectorFailureIsNotCachedAndCanBeRetried()
    {
        var mapCalls = 0;
        var query = CreateQuery<int, Projection>(value =>
        {
            if (++mapCalls == 1)
                throw new InvalidOperationException("projection failed");

            return new Projection(value);
        });
        var sut = new InMemoryEnumeratorAdapter<Projection, int>(query, new DualEnumerator<int>([5]));
        sut.MoveNext().Should().BeTrue();

        FluentActions.Invoking(() => _ = sut.Current).Should().Throw<InvalidOperationException>();

        var projection = sut.Current;
        sut.Current.Should().BeSameAs(projection);
        mapCalls.Should().Be(2);
    }

    [Fact]
    public async Task Enumerator_CanceledAsyncMoveDoesNotAdvanceOrProject()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var mapCalls = 0;
        var query = CreateQuery<int, Projection>(value =>
        {
            mapCalls++;
            return new Projection(value);
        });
        var sut = new InMemoryEnumerator<Projection, int>(query, cancellation.Token);
        sut.Init([6], null);

        (await sut.MoveNextAsync()).Should().BeFalse();
        mapCalls.Should().Be(0);
    }

    private static InMemoryCompiledQuery<TResult, TEntity> CreateQuery<TEntity, TResult>(
        Func<TEntity, TResult> map) => new(() => map, null);

    private static ValueTask<bool> MoveNext(
        InMemoryEnumerator<Projection, int> enumerator,
        bool useAsyncMove) => useAsyncMove
            ? enumerator.MoveNextAsync()
            : ValueTask.FromResult(enumerator.MoveNext());

    private static ValueTask<bool> MoveNext(
        InMemoryEnumeratorAdapter<Projection, int> enumerator,
        bool useAsyncMove) => useAsyncMove
            ? enumerator.MoveNextAsync()
            : ValueTask.FromResult(enumerator.MoveNext());

    private static void AssertReboundSource(
        InMemoryEnumerator<Projection, int> enumerator,
        IEnumerable<int> source,
        int expected,
        ref int mapCalls)
    {
        enumerator.Init(source, null);
        enumerator.MoveNext().Should().BeTrue();
        var current = enumerator.Current;
        current.Value.Should().Be(expected);
        enumerator.Current.Should().BeSameAs(current);
        mapCalls.Should().Be(expected);
        enumerator.MoveNext().Should().BeFalse();
    }

    private sealed record Projection(int Value);

    private sealed class DualEnumerator<T>(IReadOnlyList<T> values) : IAsyncEnumerator<T>, IEnumerator<T>
    {
        private int _index = -1;

        public T Current => values[_index];

        object? IEnumerator.Current => Current;

        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(MoveNext());

        public bool MoveNext() => ++_index < values.Count;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }

        public void Reset() => throw new NotSupportedException();
    }
}
