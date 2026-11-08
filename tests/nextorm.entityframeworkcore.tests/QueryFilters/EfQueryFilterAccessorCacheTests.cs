using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// Round-4 ownership regressions for the host-free owner-getter accessor cache: the cache key must not
/// root a live executing context (the D3 failure mode keyed on the context's query command), while
/// structurally identical accessors from independent contexts still share one compiled delegate.
/// </summary>
public sealed class EfQueryFilterAccessorCacheTests : EfCoreMetadataCleanup
{
    [Fact]
    public void AccessorCache_DoesNotRetainLiveContext()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var owner = new AccessorContext(connection, tenantId: 1);
        using var bridge = owner.CreateNextOrmContext();

        // Populate the process-wide cache with the GetOwner-shaped accessor the bridge imports.
        NextOrmSql.Of(bridge, bridge.From<AccessorRow>().Select(row => row.Id));

        // The cache shape is the ownership contract: a canonical string key and an accessor that takes
        // the executing context as an argument (rather than a delegate closed over one).
        var property = typeof(DataContextCache).GetProperty(
            "QueryFilterContextAccessors",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        property.PropertyType.Should().Be<IDictionary<string, Func<IDataContext, object>>>();

        Func<IDataContext, object>? accessor = null;
        foreach (var entry in (System.Collections.IEnumerable)property.GetValue(null)!)
        {
            accessor = (Func<IDataContext, object>)entry.GetType().GetProperty("Value")!.GetValue(entry)!;
            ReferencesHost(((Delegate)accessor).Target).Should().BeFalse(
                "the accessor cache stores host-free delegates, not closures over the executing context");
        }

        accessor.Should().NotBeNull("the GetOwner-shaped accessor must actually be cached");

        // A plain, unbridged context that uses the cached accessor must not be rooted by the cache; the
        // accessor fails closed on the unbound context, and only the accessor cache stays alive.
        var weak = UseAccessorAndRelease(accessor!);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        AccessorCount().Should().BeGreaterThan(0, "the accessor cache itself stays alive across the collection");
        weak.IsAlive.Should().BeFalse(
            "the process-wide accessor cache must hold a host-free key and value, not root the context that used the accessor");
    }

    [Fact]
    public void AccessorCache_KeyIsNotInterned_AndRemovedOnClear()
    {
        DataContextCache.Clear();

        // Build a structurally unique accessor shape at runtime so its canonical text cannot collide
        // with any other shape this process caches. The lambda is host-free by construction: it takes
        // only the executing context and references no owner instance.
        var unique = "nextorm-accessor-" + Guid.NewGuid().ToString("N");
        var contextParameter = Expression.Parameter(typeof(IDataContext), "context");
        var accessorLambda = Expression.Lambda<Func<IDataContext, object>>(
            Expression.Constant(unique, typeof(object)),
            contextParameter);

        var getOrCompile = typeof(DataContextCache).Assembly
            .GetType("NextORM.Core.QueryFilterContextAccessor")!
            .GetMethod("GetOrCompile", BindingFlags.Static | BindingFlags.NonPublic)!;
        var compiled = (Func<IDataContext, object>)getOrCompile.Invoke(null, [accessorLambda])!;

        var entry = AccessorEntries().Single(e => ReferenceEquals(e.Value, compiled));
        var key = entry.Key;
        key.Should().Contain(unique, "the stored key is the canonical text of the unique accessor shape");

        string.IsInterned(key).Should().BeNull(
            "the accessor cache must not pin the accessor text in the process-wide intern pool");

        DataContextCache.Clear();

        AccessorKeys().Should().NotContain(key, "DataContextCache.Clear() must release the cached entry");
    }

    [Fact]
    public void AccessorCache_SharedAcrossIndependentContexts()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var ownerOne = new AccessorContext(connection, tenantId: 1);
        using var ownerTwo = new AccessorContext(connection, tenantId: 2);

        using var nextOne = ownerOne.CreateNextOrmContext();
        using var nextTwo = ownerTwo.CreateNextOrmContext();

        NextOrmSql.Of(nextOne, nextOne.From<AccessorRow>().Select(row => row.Id));
        var afterFirst = AccessorCount();
        afterFirst.Should().BeGreaterThan(0);

        NextOrmSql.Of(nextTwo, nextTwo.From<AccessorRow>().Select(row => row.Id));
        AccessorCount().Should().Be(afterFirst,
            "structurally identical accessors from independent contexts share one compiled delegate");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference UseAccessorAndRelease(Func<IDataContext, object> accessor)
    {
        var context = new InMemoryDataContext();

        // The unbridged context fails closed when the accessor reads its missing owner; the accessor
        // simply takes the context as an argument, so the cache must not close over it.
        var act = () => accessor(context);
        act.Should().Throw<InvalidOperationException>();

        var weak = new WeakReference(context);
        context = null!;
        return weak;
    }

    private static int AccessorCount() => AccessorEntries().Count();

    private static IEnumerable<string> AccessorKeys() => AccessorEntries().Select(entry => entry.Key);

    private static IEnumerable<(string Key, Func<IDataContext, object> Value)> AccessorEntries()
    {
        var property = typeof(DataContextCache).GetProperty(
            "QueryFilterContextAccessors",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (property?.GetValue(null) is not System.Collections.IEnumerable accessors)
            yield break;

        foreach (var entry in accessors)
        {
            var type = entry.GetType();
            yield return (
                (string)type.GetProperty("Key")!.GetValue(entry)!,
                (Func<IDataContext, object>)type.GetProperty("Value")!.GetValue(entry)!);
        }
    }

    private static bool ReferencesHost(object? candidate, int depth = 3)
    {
        switch (candidate)
        {
            case null:
            case string:
            case Type:
            case MemberInfo:
                return false;
            case IDataContext:
            case DbConnection:
            case DbContext:
                return true;
        }

        if (depth < 0)
            return false;

        if (candidate is System.Collections.IEnumerable items)
        {
            foreach (var item in items)
            {
                if (ReferencesHost(item, depth - 1))
                    return true;
            }
        }

        foreach (var field in candidate.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (ReferencesHost(field.GetValue(candidate), depth - 1))
                return true;
        }

        return false;
    }

    private sealed class AccessorRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class AccessorContext(DbConnection connection, int tenantId) : DbContext
    {
        public int TenantId { get; set; } = tenantId;

        public DbSet<AccessorRow> Rows => Set<AccessorRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccessorRow>(entity =>
            {
                entity.ToTable("accessor_row");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });
        }
    }
}
