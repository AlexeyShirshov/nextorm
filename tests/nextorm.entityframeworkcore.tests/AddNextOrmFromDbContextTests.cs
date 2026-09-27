using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// <c>AddNextOrmFromDbContext</c> registers one scoped nextorm context per scope, resolved from the
/// scope's EF context; disposing the nextorm context leaves the EF connection and EF context usable.
/// </summary>
public sealed class AddNextOrmFromDbContextTests : EfCoreMetadataCleanup
{
    [Fact]
    public async Task AddNextOrmFromDbContext_ShouldResolveWorkingScopedContext()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<DiContext>();
        db.Database.EnsureCreated();
        var marker = "di_" + Guid.NewGuid().ToString("N");
        db.Rows.Add(new DiRow { Name = marker });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ctx = scope.ServiceProvider.GetRequiredService<IDataContext>();
        ctx.From<DiRow>().Where(x => x.Name == marker).Select(x => x.Id).ToList().Should().HaveCount(1);
    }

    [Fact]
    public void AddNextOrmFromDbContext_ShouldResolveOneInstancePerScopeAndSeparateInstancesAcrossScopes()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var provider = BuildProvider(connection);

        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var second = scope.ServiceProvider.GetRequiredService<IDataContext>();

        first.Should().BeSameAs(second);

        using var otherScope = provider.CreateScope();
        var other = otherScope.ServiceProvider.GetRequiredService<IDataContext>();

        other.Should().NotBeSameAs(first);
    }

    [Fact]
    public void AddNextOrmFromDbContext_ShouldApplyConfigureDelegate()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<DiContext>(options => options.UseSqlite(connection));
        services.AddNextOrmFromDbContext<DiContext>(builder => builder.UseUppercaseKeywords());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var ctx = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var sql = NextOrmSql.Of(ctx, ctx.From<DiRow>().Select(x => new { x.Id }));

        sql.Should().Be("SELECT di_id AS 'Id' FROM di_entity");
    }

    [Fact]
    public void AddNextOrmFromDbContext_ShouldComposeStoredAndExplicitConfigure()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<DiContext>(options => options.UseSqlite(connection).UseNextOrm(builder => builder.UseUppercaseKeywords()));
        services.AddNextOrmFromDbContext<DiContext>(builder => builder.UseQuotedIdentifiers(true));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var ctx = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var sql = NextOrmSql.Of(ctx, ctx.From<DiRow>().Select(x => new { x.Id }));

        sql.Should().Be("SELECT \"di_id\" AS 'Id' FROM \"di_entity\"");
    }

    [Fact]
    public async Task DisposingResolvedContext_ShouldLeaveEfConnectionAndContextUsable()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<DiContext>();
        db.Database.EnsureCreated();

        var ctx = scope.ServiceProvider.GetRequiredService<IDataContext>();
        ctx.From<DiRow>().Select(x => x.Id).ToList();
        ctx.Dispose();

        connection.State.Should().Be(ConnectionState.Open);

        var marker = "after_dispose_" + Guid.NewGuid().ToString("N");
        db.Rows.Add(new DiRow { Name = marker });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Rows.Any(x => x.Name == marker).Should().BeTrue();
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddDbContext<DiContext>(options => options.UseSqlite(connection));
        services.AddNextOrmFromDbContext<DiContext>();

        return services.BuildServiceProvider();
    }

    private sealed class DiContext(DbContextOptions<DiContext> options) : DbContext(options)
    {
        public DbSet<DiRow> Rows => Set<DiRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DiRow>(entity =>
            {
                entity.ToTable("di_entity");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("di_id").ValueGeneratedOnAdd();
                entity.Property(x => x.Name).HasColumnName("di_name");
            });
        }
    }

    private sealed class DiRow
    {
        public long Id { get; set; }

        public string? Name { get; set; }
    }
}
