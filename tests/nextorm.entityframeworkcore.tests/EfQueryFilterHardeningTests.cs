using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// P1 hardening regressions for the EF Core query-filter bridge: a missing owner (unbridged context),
/// a null intermediate owner member and a null leaf all fail closed (or bind null) instead of throwing
/// a raw <see cref="NullReferenceException"/> or silently dropping the filter; two owner chains that end
/// in the same leaf keep distinct parameter identities and both values stay live across a cached plan.
/// </summary>
public sealed class EfQueryFilterHardeningTests : EfCoreMetadataCleanup
{
    [Fact]
    public void NullOwnerLeafRetainsPredicate()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var owner = new HardeningContext(connection) { NullableThreshold = null };
        owner.Database.EnsureCreated();
        owner.Rows.Add(new HardeningRow { Id = 1, Value = 5 });
        owner.SaveChanges();

        using var next = owner.CreateNextOrmContext();

        // value > NULL matches nothing; if the filter had been silently dropped the row would surface.
        next.From<HardeningRow>().Select(row => row.Id).ToList().Should().BeEmpty();
    }

    [Fact]
    public void NullOwnerIntermediateFailsClosed()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var owner = new HardeningContext(connection) { Settings = null };
        owner.Database.EnsureCreated();
        owner.NestedRows.Add(new NestedHardeningRow { Id = 1, Value = 1 });
        owner.SaveChanges();

        using var next = owner.CreateNextOrmContext();

        var act = () => next.From<NestedHardeningRow>().Select(row => row.Id).ToList();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*null owner*")
            .Which.Should().NotBeOfType<NullReferenceException>();
    }

    [Fact]
    public void ImportedFilterWithoutBridgeFailsClosed()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var owner = new HardeningContext(connection) { NullableThreshold = 100 };

        NextOrmModelMapper.Register(owner.Model);

        using var plain = new InMemoryDataContext();
        plain.From<HardeningRow>().WithData([new HardeningRow { Id = 1, Value = 5 }]);

        var act = () => plain.From<HardeningRow>().ToList();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*EF Core bridge*")
            .Which.Should().NotBeOfType<NullReferenceException>();
    }

    [Fact]
    public void SameLeafDifferentOwnerPathsBindDistinctValues()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var owner = new HardeningContext(connection)
        {
            TenantId = 1,
            Settings = new HardeningSettings { TenantId = 10 },
        };
        owner.Database.EnsureCreated();
        owner.DirectRows.AddRange(
            new DirectTenantRow { Id = 1, TenantId = 1, Budget = 10 },
            new DirectTenantRow { Id = 2, TenantId = 2, Budget = 10 },
            new DirectTenantRow { Id = 3, TenantId = 1, Budget = 20 },
            new DirectTenantRow { Id = 4, TenantId = 2, Budget = 20 });
        owner.SaveChanges();

        using var next = owner.CreateNextOrmContext();

        // Both owner chains end in TenantId: TenantId (direct) and Settings.TenantId (nested).
        next.From<DirectTenantRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(1);

        // A warmed (cached) execution re-reads both changed values rather than reusing the first binding.
        owner.TenantId = 2;
        owner.Settings.TenantId = 20;
        next.From<DirectTenantRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(4);
    }

    [Fact]
    public void MixedOwnerLeavesAreNotCollapsed()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var owner = new HardeningContext(connection)
        {
            TenantId = 1,
            Settings = new HardeningSettings { TenantId = 10 },
        };
        owner.Database.EnsureCreated();
        owner.DirectRows.AddRange(
            new DirectTenantRow { Id = 1, TenantId = 1, Budget = 10 },
            new DirectTenantRow { Id = 2, TenantId = 1, Budget = 20 },
            new DirectTenantRow { Id = 3, TenantId = 2, Budget = 10 });
        owner.SaveChanges();

        using var next = owner.CreateNextOrmContext();

        // Only Id=1 satisfies both owner leaves; a collapse of the two reads onto one value would either
        // admit a row that matches only one chain or drop the row that matches both.
        next.From<DirectTenantRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(1);
    }

    private sealed class HardeningRow
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    private sealed class NestedHardeningRow
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    private sealed class DirectTenantRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int Budget { get; set; }
    }

    private sealed class HardeningSettings
    {
        public int TenantId { get; set; }
        public int Threshold { get; set; }
    }

    private sealed class HardeningContext(DbConnection connection) : DbContext
    {
        public int TenantId { get; set; }

        public int? NullableThreshold { get; set; }

        public HardeningSettings? Settings { get; set; }

        public DbSet<HardeningRow> Rows => Set<HardeningRow>();

        public DbSet<NestedHardeningRow> NestedRows => Set<NestedHardeningRow>();

        public DbSet<DirectTenantRow> DirectRows => Set<DirectTenantRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HardeningRow>(entity =>
            {
                entity.ToTable("hardening_row");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter("leaf", row => row.Value > NullableThreshold);
            });

            modelBuilder.Entity<NestedHardeningRow>(entity =>
            {
                entity.ToTable("nested_hardening_row");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter("nested", row => row.Value > Settings!.Threshold);
            });

            modelBuilder.Entity<DirectTenantRow>(entity =>
            {
                entity.ToTable("direct_tenant_row");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.Property(row => row.Budget).HasColumnName("budget");
                entity.HasQueryFilter("distinct", row => row.TenantId == TenantId && row.Budget == Settings!.TenantId);
            });
        }
    }
}
