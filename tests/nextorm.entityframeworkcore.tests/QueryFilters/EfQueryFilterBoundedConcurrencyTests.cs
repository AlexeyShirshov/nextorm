using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// Bounded-concurrency contract of the EF Core query-filter bridge (accept §1.12). When the bridge
/// metadata is imported once, sequentially, and no thread imports or mutates it afterwards, concurrent
/// read queries over independent bridge contexts stay isolated and correct: the process-wide
/// metadata/accessor/plan caches are only read, so one tenant's rows can never leak into another's
/// result.
/// <para>
/// Concurrent import plus mutation is deliberately <b>not</b> exercised here: §1.12 guarantees that an
/// import is failure-atomic, but it explicitly does not guarantee snapshot atomicity for a concurrent
/// reader, so doing both at once is unsupported. This test documents that published boundary.
/// </para>
/// </summary>
public sealed class EfQueryFilterBoundedConcurrencyTests : EfCoreMetadataCleanup
{
    [Fact]
    public async Task BoundedConcurrency_WithinSection112_Isolated()
    {
        const int rounds = 4;
        var tenants = new[] { 1, 2 };
        var expected = new[]
        {
            new[] { 1, 3, 5 },
            new[] { 2, 4 },
        };

        var path = Path.Combine(Path.GetTempPath(), $"nextorm-efqf-conc-{Guid.NewGuid():N}.db");
        var owners = new BoundedContext[tenants.Length];
        var bridges = new IDataContext[tenants.Length];

        try
        {
            using (var seed = new BoundedContext($"Data Source={path}", tenantId: 1))
            {
                seed.Database.EnsureCreated();
                seed.Rows.AddRange(
                    new BoundedRow { Id = 1, TenantId = 1 },
                    new BoundedRow { Id = 2, TenantId = 2 },
                    new BoundedRow { Id = 3, TenantId = 1 },
                    new BoundedRow { Id = 4, TenantId = 2 },
                    new BoundedRow { Id = 5, TenantId = 1 });
                seed.SaveChanges();
            }

            // Import the bridge metadata once, sequentially, before any reader starts.
            for (var slot = 0; slot < tenants.Length; slot++)
                owners[slot] = new BoundedContext($"Data Source={path}", tenants[slot]);

            using (var warm = owners[0].CreateNextOrmContext())
                warm.From<BoundedRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(1, 3, 5);

            // One bridge context per tenant, all created sequentially (no concurrent publication).
            for (var slot = 0; slot < tenants.Length; slot++)
                bridges[slot] = owners[slot].CreateNextOrmContext();

            // Concurrent readers, each on its own bridge context: no thread mutates the metadata.
            var cancellationToken = TestContext.Current.CancellationToken;
            var results = new int[tenants.Length][];
            var readers = new Task[tenants.Length];
            for (var slot = 0; slot < tenants.Length; slot++)
            {
                var captured = slot;
                readers[captured] = Task.Run(() =>
                {
                    int[]? last = null;
                    for (var round = 0; round < rounds; round++)
                    {
                        last = bridges[captured].From<BoundedRow>()
                            .OrderBy(row => row.Id)
                            .Select(row => row.Id)
                            .ToList()
                            .ToArray();
                    }

                    results[captured] = last!;
                }, cancellationToken);
            }

            await Task.WhenAll(readers);

            for (var slot = 0; slot < tenants.Length; slot++)
                results[slot].Should().Equal(expected[slot],
                    "tenant {0} must never see another tenant's rows under concurrent reads", tenants[slot]);
        }
        finally
        {
            foreach (var bridge in bridges)
                bridge?.Dispose();

            foreach (var owner in owners)
                owner?.Dispose();

            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private sealed class BoundedRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class BoundedContext(string connectionString, int tenantId) : DbContext
    {
        public int TenantId { get; set; } = tenantId;

        public DbSet<BoundedRow> Rows => Set<BoundedRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connectionString);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BoundedRow>(entity =>
            {
                entity.ToTable("bounded_row");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });
        }
    }
}
