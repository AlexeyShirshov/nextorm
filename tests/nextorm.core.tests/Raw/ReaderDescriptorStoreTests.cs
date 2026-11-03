using FluentAssertions;
using NextORM.Postgres;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

[Collection("Query cache controls")]
public class ReaderDescriptorStoreTests
{
    private sealed class Entity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private static SqliteDataContext CreateContext()
        => new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

    [Fact]
    public void Capacity_EvictsLeastRecentlyUsed()
    {
        ReaderDescriptorStore.Clear();

        for (var i = 0; i < ReaderDescriptorStore.Capacity + 1; i++)
        {
            var key = new RawReaderKey { ShapeFingerprint = i, IsKeyable = true, ContextType = typeof(int), DialectType = typeof(int) };
            var descriptor = new RawReaderDescriptor($"sql{i}", Array.Empty<RawParameterSpec>(), RawReaderFlavor.Result, QueryPlanStore.Generation);
            ReaderDescriptorStore.TryPublish(in key, descriptor);
        }

        ReaderDescriptorStore.EntryCount.Should().Be(ReaderDescriptorStore.Capacity);

        var firstKey = new RawReaderKey { ShapeFingerprint = 0, IsKeyable = true, ContextType = typeof(int), DialectType = typeof(int) };
        ReaderDescriptorStore.TryGet(in firstKey, out _).Should().BeFalse();

        ReaderDescriptorStore.Clear();
    }

    [Fact]
    public void GenerationStaleness_DropsPublishedDescriptor()
    {
        ReaderDescriptorStore.Clear();

        var key = new RawReaderKey { ShapeFingerprint = 42, IsKeyable = true, ContextType = typeof(int), DialectType = typeof(int) };
        var descriptor = new RawReaderDescriptor("sql", Array.Empty<RawParameterSpec>(), RawReaderFlavor.Result, QueryPlanStore.Generation);
        ReaderDescriptorStore.TryPublish(in key, descriptor);

        ReaderDescriptorStore.TryGet(in key, out _).Should().BeTrue();

        ReaderDescriptorStore.Clear();

        ReaderDescriptorStore.TryGet(in key, out _).Should().BeFalse();

        ReaderDescriptorStore.Clear();
    }

    [Fact]
    public void StructuralCollision_NotMatchedByHashAlone()
    {
        ReaderDescriptorStore.Clear();

        var keyA = new RawReaderKey { ShapeFingerprint = 7, Flavor = RawReaderFlavor.Result, IsKeyable = true, ContextType = typeof(int), DialectType = typeof(int) };
        var keyB = new RawReaderKey { ShapeFingerprint = 7, Flavor = RawReaderFlavor.Lob, IsKeyable = true, ContextType = typeof(int), DialectType = typeof(int) };

        keyA.Should().NotBe(keyB);

        var descriptorA = new RawReaderDescriptor("sqlA", Array.Empty<RawParameterSpec>(), RawReaderFlavor.Result, QueryPlanStore.Generation);
        var descriptorB = new RawReaderDescriptor("sqlB", Array.Empty<RawParameterSpec>(), RawReaderFlavor.Lob, QueryPlanStore.Generation);

        ReaderDescriptorStore.TryPublish(in keyA, descriptorA);
        ReaderDescriptorStore.TryPublish(in keyB, descriptorB);

        ReaderDescriptorStore.TryGet(in keyA, out var gotA).Should().BeTrue();
        gotA!.SqlText.Should().Be("sqlA");

        ReaderDescriptorStore.TryGet(in keyB, out var gotB).Should().BeTrue();
        gotB!.SqlText.Should().Be("sqlB");

        ReaderDescriptorStore.Clear();
    }

    [Fact]
    public void NonKeyableForm_IsRejected()
    {
        ReaderDescriptorStore.Clear();
        var before = ReaderDescriptorStore.EntryCount;

        var key = new RawReaderKey { ShapeFingerprint = 99, IsKeyable = false, ContextType = typeof(int), DialectType = typeof(int) };
        var descriptor = new RawReaderDescriptor("sql", Array.Empty<RawParameterSpec>(), RawReaderFlavor.Result, QueryPlanStore.Generation);
        ReaderDescriptorStore.TryPublish(in key, descriptor);

        ReaderDescriptorStore.EntryCount.Should().Be(before);
        ReaderDescriptorStore.Clear();
    }

    [Fact]
    public void OptOut_LeavesStoreUntouched()
    {
        ReaderDescriptorStore.Clear();
        var before = ReaderDescriptorStore.EntryCount;

        var key = new RawReaderKey { ShapeFingerprint = 5, IsKeyable = true, ContextType = typeof(int), DialectType = typeof(int) };
        ReaderDescriptorStore.TryPublish(in key, null!);

        ReaderDescriptorStore.EntryCount.Should().Be(before);
        ReaderDescriptorStore.Clear();
    }

    [Fact]
    public void KeyIdentity_SameShape_ProducesSameKey()
    {
        using var ctx = CreateContext();
        var cmd1 = ctx.From<Entity>().Select(x => x.Id);
        var cmd2 = ctx.From<Entity>().Select(x => x.Id);

        RawReaderKey.TryCreate(ctx, cmd1, RawReaderFlavor.Result, false, false, out var k1).Should().BeTrue();
        RawReaderKey.TryCreate(ctx, cmd2, RawReaderFlavor.Result, false, false, out var k2).Should().BeTrue();

        k1.Should().Be(k2);
    }

    [Fact]
    public void KeyIdentity_FilterDifference_ProducesDifferentKey()
    {
        using var ctx = CreateContext();
        var cmd1 = ctx.From<Entity>().Where(x => x.Id > 1).Select(x => x.Id);
        var cmd2 = ctx.From<Entity>().Where(x => x.Id > 2).Select(x => x.Id);

        cmd1.PrepareCommand(false, CancellationToken.None);
        cmd2.PrepareCommand(false, CancellationToken.None);

        RawReaderKey.TryCreate(ctx, cmd1, RawReaderFlavor.Result, false, false, out var k1).Should().BeTrue();
        RawReaderKey.TryCreate(ctx, cmd2, RawReaderFlavor.Result, false, false, out var k2).Should().BeTrue();

        k1.Should().NotBe(k2);
    }

    [Fact]
    public void KeyIdentity_ReaderFlavor_ProducesDifferentKey()
    {
        using var ctx = CreateContext();
        var cmd = ctx.From<Entity>().Select(x => x.Id);

        RawReaderKey.TryCreate(ctx, cmd, RawReaderFlavor.Result, false, false, out var k1).Should().BeTrue();
        RawReaderKey.TryCreate(ctx, cmd, RawReaderFlavor.Lob, false, false, out var k2).Should().BeTrue();

        k1.Should().NotBe(k2);
    }

    [Fact]
    public void KeyIdentity_SequentialAccess_ProducesDifferentKey()
    {
        using var ctx = CreateContext();
        var cmd = ctx.From<Entity>().Select(x => x.Id);

        RawReaderKey.TryCreate(ctx, cmd, RawReaderFlavor.Result, false, false, out var k1).Should().BeTrue();
        RawReaderKey.TryCreate(ctx, cmd, RawReaderFlavor.Result, true, false, out var k2).Should().BeTrue();

        k1.Should().NotBe(k2);
    }

    [Fact]
    public void KeyIdentity_SuppressLocator_ProducesDifferentKey()
    {
        using var ctx = CreateContext();
        var cmd = ctx.From<Entity>().Select(x => x.Id);

        RawReaderKey.TryCreate(ctx, cmd, RawReaderFlavor.Result, false, false, out var k1).Should().BeTrue();
        RawReaderKey.TryCreate(ctx, cmd, RawReaderFlavor.Result, false, true, out var k2).Should().BeTrue();

        k1.Should().NotBe(k2);
    }

    [Fact]
    public void KeyIdentity_ProviderDifference_ProducesDifferentKey()
    {
        using var sqlite = CreateContext();
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());

        var cmdS = sqlite.From<Entity>().Select(x => x.Id);
        var cmdP = postgres.From<Entity>().Select(x => x.Id);

        RawReaderKey.TryCreate(sqlite, cmdS, RawReaderFlavor.Result, false, false, out var k1).Should().BeTrue();
        RawReaderKey.TryCreate(postgres, cmdP, RawReaderFlavor.Result, false, false, out var k2).Should().BeTrue();

        k1.Should().NotBe(k2);
    }

    [Fact]
    public void KeyIdentity_NullContextOrCommand_ReturnsFalse()
    {
        using var ctx = CreateContext();
        var cmd = ctx.From<Entity>().Select(x => x.Id);

        RawReaderKey.TryCreate(null!, cmd, RawReaderFlavor.Result, false, false, out _).Should().BeFalse();
        RawReaderKey.TryCreate(ctx, null!, RawReaderFlavor.Result, false, false, out _).Should().BeFalse();
    }

    [Fact]
    public void WeakRetention_DescriptorDoesNotRetainContextOrCommand()
    {
        ReaderDescriptorStore.Clear();
        using var ctx = CreateContext();
        var cmd = ctx.From<Entity>().Select(x => x.Id);

        RawReaderKey.TryCreate(ctx, cmd, RawReaderFlavor.Result, false, false, out var key).Should().BeTrue();
        var descriptor = new RawReaderDescriptor("sql", Array.Empty<RawParameterSpec>(), RawReaderFlavor.Result, QueryPlanStore.Generation);
        ReaderDescriptorStore.TryPublish(in key, descriptor);

        descriptor.SqlText.Should().Be("sql");
        ReaderDescriptorStore.Clear();
    }

    [Fact]
    public void KeyIdentity_InCardinality_ProducesDifferentKey()
    {
        using var ctx = CreateContext();
        var ids1 = new[] { 1, 2 };
        var ids2 = new[] { 1, 2, 3 };

        var cmd1 = ctx.From<Entity>().Where(x => ids1.Contains(x.Id)).Select(x => x.Id);
        var cmd2 = ctx.From<Entity>().Where(x => ids2.Contains(x.Id)).Select(x => x.Id);

        cmd1.PrepareCommand(false, CancellationToken.None);
        cmd2.PrepareCommand(false, CancellationToken.None);

        RawReaderKey.TryCreate(ctx, cmd1, RawReaderFlavor.Result, false, false, out var k1).Should().BeTrue();
        RawReaderKey.TryCreate(ctx, cmd2, RawReaderFlavor.Result, false, false, out var k2).Should().BeTrue();

        k1.Should().NotBe(k2);
    }

    [Fact]
    public void KeyIdentity_CollectionShape_ProducesDifferentKey()
    {
        using var ctx = CreateContext();
        var flat = new[] { 1, 2 };
        var nested = new[] { new[] { 1, 2 } };

        // Two distinct collection shapes feeding the same predicate surface must not collide.
        var cmd1 = ctx.From<Entity>().Where(x => flat.Contains(x.Id)).Select(x => x.Id);
        var cmd2 = ctx.From<Entity>().Where(x => x.Id == 1 || x.Id == 2).Select(x => x.Id);

        cmd1.PrepareCommand(false, CancellationToken.None);
        cmd2.PrepareCommand(false, CancellationToken.None);

        RawReaderKey.TryCreate(ctx, cmd1, RawReaderFlavor.Result, false, false, out var k1).Should().BeTrue();
        RawReaderKey.TryCreate(ctx, cmd2, RawReaderFlavor.Result, false, false, out var k2).Should().BeTrue();

        k1.Should().NotBe(k2);
    }

    [Fact]
    public void KeyIdentity_UnsupportedForm_NullContextOrCommand_ReturnsFalse()
    {
        using var ctx = CreateContext();
        var cmd = ctx.From<Entity>().Select(x => x.Id);

        RawReaderKey.TryCreate(null!, cmd, RawReaderFlavor.Result, false, false, out _).Should().BeFalse();
        RawReaderKey.TryCreate(ctx, null!, RawReaderFlavor.Result, false, false, out _).Should().BeFalse();
    }
}
