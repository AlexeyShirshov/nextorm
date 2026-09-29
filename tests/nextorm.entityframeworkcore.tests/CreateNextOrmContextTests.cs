using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// <c>CreateNextOrmContext</c> selects the provider from the EF Core context and then lets the caller
/// override nextorm defaults; omitting the callback keeps those defaults.
/// </summary>
public sealed class CreateNextOrmContextTests : EfCoreMetadataCleanup
{
    [Fact]
    public void CreateNextOrmContext_ShouldHonorExplicitConfigure()
    {
        using var db = new ConfigureContext();

        using var ctx = db.CreateNextOrmContext(builder => builder.UseUppercaseKeywords());
        var sql = NextOrmSql.Of(ctx, ctx.From<ConfigureRow>().Select(x => new { x.Id }));

        sql.Should().Be("SELECT configured_id AS 'Id' FROM configured_table");
    }

    [Fact]
    public void CreateNextOrmContext_ShouldApplyProviderDefaults_WhenConfigureIsNull()
    {
        using var db = new ConfigureContext();

        using var ctx = db.CreateNextOrmContext(null);
        var sql = NextOrmSql.Of(ctx, ctx.From<ConfigureRow>().Select(x => new { x.Id }));

        sql.Should().Be("select configured_id as 'Id' from configured_table");
    }

    private sealed class ConfigureContext : DbContext
    {
        public DbSet<ConfigureRow> Rows => Set<ConfigureRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConfigureRow>(entity =>
            {
                entity.ToTable("configured_table");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("configured_id");
            });
        }
    }

    private sealed class ConfigureRow
    {
        public long Id { get; set; }
    }
}
