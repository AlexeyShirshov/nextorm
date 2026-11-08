using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// The headline interop scenario through the public entry point: nextorm runs on the very SQLite
/// connection and transaction EF Core owns, sees EF's uncommitted rows, and stops seeing them once EF
/// rolls the transaction back. No container is needed.
/// </summary>
public sealed class EfCoreSharedTransactionTests : EfCoreMetadataCleanup
{
    [Fact]
    public async Task NextormContext_InsideEfTransaction_ShouldSeeUncommittedRows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<InsertContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new InsertContext(options);
        db.Database.EnsureCreated();

        await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var marker = "eftx_" + Guid.NewGuid().ToString("N");
        db.Rows.Add(new InsertRow { Name = marker, Age = 5 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var next = db.CreateNextOrmContext();

        // nextorm runs on EF's connection inside EF's uncommitted transaction.
        next.From<InsertRow>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().Equal(5);

        await efTransaction.RollbackAsync(TestContext.Current.CancellationToken);

        // The rollback is EF's; nextorm merely observed the same transaction and drops it lazily.
        next.From<InsertRow>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().BeEmpty();
    }

    private sealed class InsertContext(DbContextOptions<InsertContext> options) : DbContext(options)
    {
        public DbSet<InsertRow> Rows => Set<InsertRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InsertRow>(entity =>
            {
                entity.ToTable("insert_entity");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(x => x.Name).HasColumnName("name");
                entity.Property(x => x.Age).HasColumnName("age");
            });
        }
    }

    private sealed class InsertRow
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
    }
}
