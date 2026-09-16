using Microsoft.EntityFrameworkCore;

namespace nextorm.benchmark;

/// <summary>
/// EF Core context backed by the InMemory provider, used for apples-to-apples comparison with the
/// pure-LINQ and nextorm in-memory benchmarks. <see cref="SimpleEntity.Id"/> is configured with
/// <c>ValueGeneratedNever</c> so that the full 0..N-1 id range (including 0) can be seeded.
/// </summary>
internal sealed class EFInMemoryDataContext : DbContext
{
    public EFInMemoryDataContext(DbContextOptions<EFInMemoryDataContext> options) : base(options)
    {
    }

    public DbSet<SimpleEntity> SimpleEntities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SimpleEntity>().Property(e => e.Id).ValueGeneratedNever();
    }
}

internal static class EfInMemory
{
    /// <summary>
    /// Builds an isolated EF Core InMemory store seeded with <paramref name="rowCount"/> rows with
    /// sequential ids (0..rowCount-1), mirroring the nextorm/pure-LINQ fixtures.
    /// </summary>
    public static EFInMemoryDataContext Create(int rowCount)
    {
        var options = new DbContextOptionsBuilder<EFInMemoryDataContext>()
            .UseInMemoryDatabase($"nextorm-bench-{Guid.NewGuid():N}")
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        var ctx = new EFInMemoryDataContext(options);

        var data = new List<SimpleEntity>(rowCount);
        for (var i = 0; i < rowCount; i++)
            data.Add(new SimpleEntity { Id = i });

        ctx.SimpleEntities.AddRange(data);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        return ctx;
    }
}
