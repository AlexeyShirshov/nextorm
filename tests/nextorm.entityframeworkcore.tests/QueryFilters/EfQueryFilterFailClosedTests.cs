using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// Round-4 fail-closed regressions for the EF Core query-filter bridge: when the imported filter
/// metadata is dropped from the process-wide cache (by <see cref="DataContextCache.Clear"/> or a
/// sliding-eviction miss), a bridge-bound context must refuse to run rather than silently run
/// unfiltered. The expectation state lives outside <see cref="DataContextCache"/> and survives Clear.
/// </summary>
public sealed class EfQueryFilterFailClosedTests : EfCoreMetadataCleanup
{
    [Fact]
    public void MissingExpectedFilters_BothResolveOverloads_Throw()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var owner = new FailClosedContext(connection);
        using var next = owner.CreateNextOrmContext();

        // Drop the imported metadata; the bound context still expects the imported filter.
        DataContextCache.Clear();

        // Strongly-typed resolve path (ResolveMetadata<TEntity> through From<T>).
        var typed = () => next.From<FailClosedRow>();
        typed.Should().Throw<InvalidOperationException>()
            .WithMessage("*unfiltered*")
            .Which.Should().NotBeOfType<NullReferenceException>();

        // Non-generic resolve path (ResolveMetadata(Type) through BindEntity<T>).
        var nonGeneric = () => next.From("fail_closed_row").BindEntity<FailClosedRow>(["id", "tenant_id"]);
        nonGeneric.Should().Throw<InvalidOperationException>()
            .WithMessage("*unfiltered*");
    }

    [Fact]
    public void Clear_DoesNotClearExpectations()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var owner = new FailClosedContext(connection);
        using var next = owner.CreateNextOrmContext();

        ExpectationRegistered(next, typeof(FailClosedRow)).Should().BeTrue(
            "creating a bridge context registers the imported-filter expectation");

        DataContextCache.Clear();

        ExpectationRegistered(next, typeof(FailClosedRow)).Should().BeTrue(
            "the expectation registry lives outside DataContextCache, so Clear must not drop it");

        var act = () => next.From<FailClosedRow>().ToList();
        act.Should().Throw<InvalidOperationException>().WithMessage("*unfiltered*");
    }

    [Fact]
    public void FailedBind_DoesNotRegisterExpectation()
    {
        var before = RegisteredExpectationCount();

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var owner = new FailingContext(connection);

        var act = () => owner.CreateNextOrmContext();
        act.Should().Throw<NotSupportedException>();

        RegisteredExpectationCount().Should().Be(before,
            "a failed Register/Bind must publish nothing, including the fail-closed expectation");
    }

    [Fact]
    public void DmlRender_FailsClosed_WhenExpectedFiltersMissing()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var owner = new FailClosedContext(connection);
        using var next = owner.CreateNextOrmContext();

        // Build the key-upsert commands while the imported metadata is still present; the builders have
        // already captured the EF mapping, so they can be rendered after the metadata is dropped.
        var upsert = next.CreateMergeBuilder<FailClosedRow>()
            .Using(new FailClosedRow { Id = 1, TenantId = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert();

        // An explicit IgnoreFilters() skips the metadata-resolution guard, so this arm reaches the
        // context-wide DML render guard in MakeMerge; either way the context must fail closed.
        var ignoredUpsert = next.CreateMergeBuilder<FailClosedRow>()
            .IgnoreFilters()
            .Using(new FailClosedRow { Id = 2, TenantId = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert();

        // Drop the imported metadata; the bound context still expects the imported filter.
        DataContextCache.Clear();

        // DML rendering must hit the same fail-closed funnel as a SELECT: the documented bridge
        // InvalidOperationException, never a provider SQL error and never unfiltered SQL.
        var render = () => upsert.ToSql();
        render.Should().Throw<InvalidOperationException>().WithMessage("*unfiltered*");

        var ignoredRender = () => ignoredUpsert.ToSql();
        ignoredRender.Should().Throw<InvalidOperationException>().WithMessage("*unfiltered*");
    }

    private static bool ExpectationRegistered(IDataContext context, Type entityType)
    {
        var type = typeof(DataContextCache).Assembly.GetType("NextORM.Core.QueryFilterExpectations");
        var method = type?.GetMethod("HasExpectation", BindingFlags.Static | BindingFlags.NonPublic);
        return method?.Invoke(null, [context, entityType]) is true;
    }

    private static int RegisteredExpectationCount()
    {
        var type = typeof(DataContextCache).Assembly.GetType("NextORM.Core.QueryFilterExpectations");
        var property = type?.GetProperty("RegisteredExpectationCount", BindingFlags.Static | BindingFlags.NonPublic);
        return property?.GetValue(null) is int count ? count : 0;
    }

    private sealed class FailClosedRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class FailClosedContext(DbConnection connection) : DbContext
    {
        public int TenantId { get; set; } = 1;

        public DbSet<FailClosedRow> Rows => Set<FailClosedRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FailClosedRow>(entity =>
            {
                entity.ToTable("fail_closed_row");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });
        }
    }

    private sealed class FailingRow
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    private sealed class FailingContext(DbConnection connection) : DbContext
    {
        public DbSet<FailingRow> Rows => Set<FailingRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var threshold = 5;

            modelBuilder.Entity<FailingRow>(entity =>
            {
                entity.ToTable("failing_row");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.Value).HasColumnName("value");
                // A closure-local capture is rejected at bridge creation, before publication.
                entity.HasQueryFilter("bad", row => row.Value > threshold);
            });
        }
    }
}
