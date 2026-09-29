using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// The bridge is read-only over the borrowed connection and transaction: disposing the nextorm context
/// neither closes EF's connection nor ends EF's transaction, and EF can keep using both. When a query
/// runs on a closed connection, nextorm opens it and leaves it open.
/// </summary>
public sealed class EfCoreConnectionOwnershipTests : EfCoreMetadataCleanup
{
    [Fact]
    public async Task NextormDispose_ShouldLeaveEfConnectionOpenAndTransactionActive()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<OwnershipContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new OwnershipContext(options);
        db.Database.EnsureCreated();

        await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        using (var next = db.CreateNextOrmContext())
        {
            next.From<OwnershipRow>().Select(x => x.Id).ToList();
        }

        connection.State.Should().Be(ConnectionState.Open);
        db.Database.CurrentTransaction.Should().NotBeNull();

        // The connection and transaction are still EF's to use after the nextorm context is gone.
        db.Rows.Add(new OwnershipRow { Name = "still-alive" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using (var next = db.CreateNextOrmContext())
        {
            next.From<OwnershipRow>()
                .Where(x => x.Name == "still-alive")
                .Select(x => x.Id)
                .ToList()
                .Should().HaveCount(1);
        }

        await efTransaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void NextormShouldOpenClosedBorrowedConnectionAndLeaveItOpen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-ef-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<OwnershipContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        try
        {
            using (var seed = new OwnershipContext(options))
                seed.Database.EnsureCreated();

            using var db = new OwnershipContext(options);
            var connection = db.Database.GetDbConnection();
            connection.State.Should().Be(ConnectionState.Closed);

            using (var next = db.CreateNextOrmContext())
            {
                next.From<OwnershipRow>().Select(x => x.Id).ToList();

                connection.State.Should().Be(ConnectionState.Open);
            }

            // A caller-supplied connection is opened on demand but never closed by nextorm.
            connection.State.Should().Be(ConnectionState.Open);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private sealed class OwnershipContext(DbContextOptions<OwnershipContext> options) : DbContext(options)
    {
        public DbSet<OwnershipRow> Rows => Set<OwnershipRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OwnershipRow>(entity =>
            {
                entity.ToTable("ownership_entity");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(x => x.Name).HasColumnName("name");
            });
        }
    }

    private sealed class OwnershipRow
    {
        public long Id { get; set; }

        public string? Name { get; set; }
    }
}
