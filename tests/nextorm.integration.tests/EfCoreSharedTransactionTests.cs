using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// The headline interop scenario: a nextorm query runs on the very connection and transaction EF Core
/// owns, sees EF's uncommitted rows, and is unaffected when EF rolls the transaction back. Unlike the
/// provider-agnostic suite this is SQLite-only, so it needs no container.
/// </summary>
public sealed class EfCoreSharedTransactionTests
{
    [Fact]
    public async Task NextormQuery_InsideEfTransaction_ShouldSeeUncommittedRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm.efcore.{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<EfInsertContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        try
        {
            using var db = new EfInsertContext(options);
            db.Database.EnsureCreated();

            await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

            var marker = "eftx_" + Guid.NewGuid().ToString("N");
            db.InsertEntities.Add(new EfInsertRow { Name = marker, Age = 5 });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            using var next = new SqliteDataContext(db.Database.GetDbConnection(), new DataContextBuilder());
            ((ITransactionManager)next).UseTransaction(efTransaction.GetDbTransaction());

            // nextorm runs on EF's connection inside EF's uncommitted transaction.
            next.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().Equal(5);

            await efTransaction.RollbackAsync(TestContext.Current.CancellationToken);

            // The rollback is EF's; nextorm merely observed the same transaction and drops it lazily.
            next.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private sealed class EfInsertContext(DbContextOptions<EfInsertContext> options) : DbContext(options)
    {
        public DbSet<EfInsertRow> InsertEntities => Set<EfInsertRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EfInsertRow>(entity =>
            {
                entity.ToTable("insert_entity");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(x => x.Name).HasColumnName("name");
                entity.Property(x => x.Age).HasColumnName("age");
            });
        }
    }

    private sealed class EfInsertRow
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
    }
}
