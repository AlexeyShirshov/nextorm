using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// T243/D243 safeguard for the context-owned scalar artifact store itself (internal API, no private
/// reflection): the bounded FIFO capacity, the normalized key identity that makes a fresh and a cached
/// <c>Sum</c> selector one shape, and the purge/clear pairing that a purged or disposed context relies
/// on. <c>DataContext.DisposeStaff</c> calls the same <c>ClearScalarArtifacts</c> that
/// <see cref="QueryCache.PurgeQueryCache"/> uses here.
/// </summary>
public class T243ScalarArtifactCacheTests
{
    private static ScalarArtifactKey CountKey(Type entityType, Type resultType)
        => new(entityType, resultType, ScalarTerminal.Count, null);

    [Fact]
    public void Find_ShouldReturnTheSameArtifactForAnEqualKey()
    {
        var cache = new ScalarArtifactCache();
        var key = CountKey(typeof(SimpleEntity), typeof(int));

        var created = cache.GetOrAdd(key);
        var again = cache.GetOrAdd(key);

        again.Should().BeSameAs(created);
        cache.Find(key).Should().BeSameAs(created);
        cache.Find(CountKey(typeof(SimpleEntity), typeof(long))).Should().BeNull("the result type is part of the key");
        cache.Find(CountKey(typeof(NullableRow), typeof(int))).Should().BeNull("the entity type is part of the key");
        cache.Find(new ScalarArtifactKey(typeof(SimpleEntity), typeof(int), ScalarTerminal.Sum, null)).Should().BeNull();
    }

    [Fact]
    public void Key_ShouldNormalizeOnTheMemberIdentity_NotOnTheExpressionInstance()
    {
        var id = typeof(SimpleEntity).GetProperty(nameof(SimpleEntity.Id))!;
        var amount = typeof(NullableRow).GetProperty(nameof(NullableRow.Amount))!;
        var left = new ScalarArtifactKey(typeof(SimpleEntity), typeof(int), ScalarTerminal.Sum, id);
        var right = new ScalarArtifactKey(typeof(SimpleEntity), typeof(int), ScalarTerminal.Sum, id);
        var other = new ScalarArtifactKey(typeof(SimpleEntity), typeof(int), ScalarTerminal.Sum, amount);

        left.Should().Be(right);
        left.GetHashCode().Should().Be(right.GetHashCode());
        left.Should().NotBe(other);
    }

    [Fact]
    public void Capacity_ShouldEvictInFifoOrder_AndClearShouldDropEverything()
    {
        var cache = new ScalarArtifactCache();
        var keys = DistinctKeys(ScalarArtifactCache.Capacity + 1);

        for (var i = 0; i < ScalarArtifactCache.Capacity; i++)
            cache.GetOrAdd(keys[i]).Should().NotBeNull();

        cache.GetOrAdd(keys[ScalarArtifactCache.Capacity]);

        cache.Find(keys[0]).Should().BeNull("the oldest entry is evicted first");
        cache.Find(keys[1]).Should().NotBeNull();
        cache.Find(keys[ScalarArtifactCache.Capacity]).Should().NotBeNull();

        cache.Clear();
        for (var i = 0; i <= ScalarArtifactCache.Capacity; i++)
            cache.Find(keys[i]).Should().BeNull();
    }

    [Fact]
    public void PurgeQueryCache_ShouldClearArtifactsAndRunThePurgeCallback()
    {
        var purged = 0;
        var cache = new QueryCache(() => purged++);
        var key = CountKey(typeof(SimpleEntity), typeof(int));
        cache.GetOrAddScalarArtifact(key).Should().NotBeNull();

        cache.PurgeQueryCache();

        purged.Should().Be(1);
        cache.FindScalarArtifact(key).Should().BeNull("a purge drops the artifacts that reference the cleared plans");

        // A later call rebuilds the artifact: the store stays usable and is not left in a broken state.
        cache.GetOrAddScalarArtifact(key).Should().NotBeNull();
        cache.FindScalarArtifact(key).Should().NotBeNull();
    }

    [Fact]
    public void ClearScalarArtifacts_ShouldDropEveryEntry()
    {
        var cache = new QueryCache(static () => { });
        var key = CountKey(typeof(SimpleEntity), typeof(int));
        cache.GetOrAddScalarArtifact(key).Should().NotBeNull();

        cache.ClearScalarArtifacts();

        cache.FindScalarArtifact(key).Should().BeNull();
    }

    /// <summary>
    /// <see cref="ScalarArtifactCache.Capacity"/> + 1 distinct keys: the terminal, the entity type and
    /// the result type are all key parts, so distinct CLR result types are enough to produce distinct
    /// shapes for one entity.
    /// </summary>
    private static ScalarArtifactKey[] DistinctKeys(int count)
    {
        Type[] resultTypes =
        [
            typeof(int), typeof(long), typeof(short), typeof(byte), typeof(sbyte), typeof(uint),
            typeof(ulong), typeof(ushort), typeof(decimal), typeof(double), typeof(float),
            typeof(DateTime), typeof(Guid), typeof(bool), typeof(char), typeof(TimeSpan),
            typeof(string),
        ];

        var keys = new ScalarArtifactKey[count];
        for (var i = 0; i < count; i++)
            keys[i] = CountKey(typeof(SimpleEntity), resultTypes[i]);
        return keys;
    }

    private sealed class NullableRow
    {
        public int? Amount { get; set; }
    }
}
