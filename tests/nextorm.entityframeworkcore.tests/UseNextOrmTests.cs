using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// <c>UseNextOrm</c> stores the nextorm configuration in the EF options; <c>GetNextOrmContext</c> then
/// applies it, keeps working when no configuration was stored, and always borrows the context's
/// connection and current transaction.
/// </summary>
public sealed class UseNextOrmTests : EfCoreMetadataCleanup
{
    [Fact]
    public void UseNextOrm_ShouldApplyStoredConfigure()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var options = new DbContextOptionsBuilder<BridgeContext>()
            .UseSqlite(connection)
            .UseNextOrm(builder => builder.UseUppercaseKeywords())
            .Options;
        using var db = new BridgeContext(options);

        using var ctx = db.GetNextOrmContext();
        var sql = NextOrmSql.Of(ctx, ctx.From<BridgeRow>().Select(x => new { x.Id }));

        sql.Should().Be("SELECT bridge_id AS 'Id' FROM bridge_entity");
    }

    [Fact]
    public void GetNextOrmContext_WithoutUseNextOrm_ShouldUseProviderDefaults()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var options = new DbContextOptionsBuilder<BridgeContext>()
            .UseSqlite(connection)
            .Options;
        using var db = new BridgeContext(options);

        using var ctx = db.GetNextOrmContext();
        var sql = NextOrmSql.Of(ctx, ctx.From<BridgeRow>().Select(x => new { x.Id }));

        sql.Should().Be("select bridge_id as 'Id' from bridge_entity");
    }

    [Fact]
    public async Task UseNextOrm_ShouldRunRealQueryOnEfConnectionAndTransaction()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<BridgeContext>()
            .UseSqlite(connection)
            .UseNextOrm()
            .Options;
        using var db = new BridgeContext(options);
        db.Database.EnsureCreated();

        await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var marker = "bridge_" + Guid.NewGuid().ToString("N");
        db.Rows.Add(new BridgeRow { Name = marker });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using (var first = db.GetNextOrmContext())
        using (var second = db.GetNextOrmContext())
        {
            // Both contexts run on EF's connection inside EF's uncommitted transaction; a context that
            // opened its own connection would see neither the row nor the transaction.
            first.From<BridgeRow>().Where(x => x.Name == marker).Select(x => x.Id).ToList().Should().HaveCount(1);
            second.From<BridgeRow>().Where(x => x.Name == marker).Select(x => x.Id).ToList().Should().HaveCount(1);
        }

        await efTransaction.RollbackAsync(TestContext.Current.CancellationToken);

        using (var afterRollback = db.GetNextOrmContext())
        {
            afterRollback.From<BridgeRow>().Where(x => x.Name == marker).Select(x => x.Id).ToList().Should().BeEmpty();
        }
    }

    private sealed class BridgeContext(DbContextOptions<BridgeContext> options) : DbContext(options)
    {
        public DbSet<BridgeRow> Rows => Set<BridgeRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BridgeRow>(entity =>
            {
                entity.ToTable("bridge_entity");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("bridge_id").ValueGeneratedOnAdd();
                entity.Property(x => x.Name).HasColumnName("bridge_name");
            });
        }
    }

    private sealed class BridgeRow
    {
        public long Id { get; set; }

        public string? Name { get; set; }
    }
}
