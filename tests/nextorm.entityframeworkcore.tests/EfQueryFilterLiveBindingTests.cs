using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// End-to-end proof of the EF Core query-filter bridge on SQLite: an imported context-capturing filter
/// evaluates against the live owning <see cref="DbContext"/>, a warmed (cached) execution re-reads the
/// changed owner value, a direct <c>From&lt;T&gt;</c> on the bridge context is filtered, and the tenant
/// value does not enter the metadata or the SQL plan shape.
/// </summary>
public sealed class EfQueryFilterLiveBindingTests : EfCoreMetadataCleanup
{
    [Fact]
    public void ImportedContextFilter_ShouldEvaluateAgainstLiveOwner_AndRereadPerExecution()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var tenantOne = new LiveFilterContext(connection, tenantId: 1);
        using var tenantTwo = new LiveFilterContext(connection, tenantId: 2);

        tenantOne.Database.EnsureCreated();
        tenantOne.Documents.AddRange(
            new LiveDocument { Id = 1, TenantId = 1, Value = 10 },
            new LiveDocument { Id = 2, TenantId = 2, Value = 20 },
            new LiveDocument { Id = 3, TenantId = 1, Value = 30 });
        tenantOne.SaveChanges();

        using var nextOne = tenantOne.CreateNextOrmContext();
        using var nextTwo = tenantTwo.CreateNextOrmContext();

        // A direct From<T>() on a bridge-created context (not ToNextOrm) is filtered too.
        var idsOne = nextOne.From<LiveDocument>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
        var idsTwo = nextTwo.From<LiveDocument>().OrderBy(row => row.Id).Select(row => row.Id).ToList();

        idsOne.Should().Equal(1, 3);
        idsTwo.Should().Equal(2);

        // Two tenants share one SQL plan shape: the tenant value is not part of the plan or the SQL.
        var sqlOne = NextOrmSql.Of(nextOne, nextOne.From<LiveDocument>().Select(row => row.Id));
        var sqlTwo = NextOrmSql.Of(nextTwo, nextTwo.From<LiveDocument>().Select(row => row.Id));
        sqlOne.Should().Be(sqlTwo);

        // A warmed (cached) execution re-reads the live owner value rather than reusing the value that
        // was bound when the plan was first prepared.
        tenantOne.TenantId = 2;
        var idsOneAgain = nextOne.From<LiveDocument>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
        idsOneAgain.Should().Equal(2);

        // The D3 host-free accessor path is what resolved the owner read (the legacy closure-cache path
        // would have left this internal cache empty); reflects the internal engine detail, not public API.
        AccessorCount().Should().BeGreaterThan(0);
    }

    [Fact]
    public void ImportedContextFilter_ShouldNotEmbedTenantInMetadata()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var tenant = new LiveFilterContext(connection, tenantId: 42);

        NextOrmModelMapper.Register(tenant.Model);
        NextOrmModelMapper.Register(tenant.Model); // idempotent: no owner instance/value in the fingerprint

        var filter = DataContextCache.Metadata[typeof(LiveDocument)].Filters.Should().ContainSingle().Subject;
        filter.Lambda!.ToString().Should().Contain("GetOwner");
    }

    private static int AccessorCount()
    {
        var property = typeof(DataContextCache).GetProperty(
            "QueryFilterContextAccessors",
            BindingFlags.Static | BindingFlags.NonPublic);

        return property?.GetValue(null) is System.Collections.IEnumerable accessors
            ? accessors.Cast<object>().Count()
            : 0;
    }

    private sealed class LiveDocument
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int Value { get; set; }
    }

    private sealed class LiveFilterContext(DbConnection connection, int tenantId) : DbContext
    {
        public int TenantId { get; set; } = tenantId;

        public DbSet<LiveDocument> Documents => Set<LiveDocument>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LiveDocument>(entity =>
            {
                entity.ToTable("live_document");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });
        }
    }
}
