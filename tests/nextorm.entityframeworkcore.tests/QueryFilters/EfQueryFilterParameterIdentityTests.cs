using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// Round-4 parameter-identity regressions for the EF Core query-filter bridge: two distinct
/// owner-getter member chains that collapse to the same source name must be rejected with a diagnostic
/// before any SQL is built/executed, while supported distinct chains keep binding and rebinding their
/// live owner values.
/// </summary>
public sealed class EfQueryFilterParameterIdentityTests : EfCoreMetadataCleanup
{
    [Fact]
    public void CollidingParameterIdentity_ThrowsBeforeSql()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var owner = new CollidingIdentityContext(connection);
        owner.Database.EnsureCreated();
        owner.Rows.Add(new CollidingRow { Id = 1, TenantId = 1, Budget = 1 });
        owner.SaveChanges();

        using var next = owner.CreateNextOrmContext();

        // NextOrmSql.Of prepares the command (builds the SQL) without executing it, so the throw proves
        // the collision is rejected during preparation, before any command runs.
        var build = () => NextOrmSql.Of(next, next.From<CollidingRow>().Select(row => row.Id));

        var exception = build.Should().Throw<InvalidOperationException>().Subject.Single();
        exception.Message.Should().Contain("two distinct owner-getter",
            "the diagnostic identifies the ambiguous owner-getter identity");
        exception.Message.Should().Contain("Get_Id", "the collapsed source name is named in the diagnostic");
        exception.Message.Should().NotContain("Must add values",
            "the collision is rejected before parameter binding, not left to fail at SQL execution");

        // The execution path fails with the same diagnostic, never reaching SQLite's parameter binding.
        var execute = () => next.From<CollidingRow>().Select(row => row.Id).ToList();
        execute.Should().Throw<InvalidOperationException>()
            .WithMessage("*two distinct owner-getter*")
            .Which.Message.Should().NotContain("Must add values");
    }

    [Fact]
    public void DistinctParameterIdentities_RebindCorrectly()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var owner = new DistinctIdentityContext(connection)
        {
            Left = new IdentityHolder { Probe = new IdentityProbe { Id = 1, Other = 10 } },
            Right = new IdentityHolder { Probe = new IdentityProbe { Id = 1, Other = 10 } },
        };
        owner.Database.EnsureCreated();
        owner.Rows.AddRange(
            new DistinctRow { Id = 1, TenantId = 1, Budget = 10 },
            new DistinctRow { Id = 2, TenantId = 2, Budget = 20 },
            new DistinctRow { Id = 3, TenantId = 1, Budget = 20 },
            new DistinctRow { Id = 4, TenantId = 2, Budget = 10 });
        owner.SaveChanges();

        using var next = owner.CreateNextOrmContext();

        // Distinct method-call chains that end in different leaves ("Get_Id" vs "Get_Other") are
        // supported; the previously warmed plan re-reads both owner values on the next execution.
        next.From<DistinctRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(1);

        owner.Left.Probe.Id = 2;
        owner.Right.Probe.Other = 20;
        next.From<DistinctRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(2);
    }

    private sealed class IdentityProbe
    {
        public int Id { get; set; }

        public int Other { get; set; }
    }

    private sealed class IdentityHolder
    {
        public IdentityProbe Probe { get; set; } = new();

        public IdentityProbe Get() => Probe;
    }

    private sealed class CollidingRow
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int Budget { get; set; }
    }

    private sealed class DistinctRow
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int Budget { get; set; }
    }

    private sealed class CollidingIdentityContext(DbConnection connection) : DbContext
    {
        public IdentityHolder Left { get; set; } = new();

        public IdentityHolder Right { get; set; } = new();

        public DbSet<CollidingRow> Rows => Set<CollidingRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CollidingRow>(entity =>
            {
                entity.ToTable("colliding_identity_row");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.Property(row => row.Budget).HasColumnName("budget");
                // Left.Get().Id and Right.Get().Id both collapse to the source name "Get_Id".
                entity.HasQueryFilter("collide", row => row.TenantId == Left.Get().Id && row.Budget == Right.Get().Id);
            });
        }
    }

    private sealed class DistinctIdentityContext(DbConnection connection) : DbContext
    {
        public IdentityHolder Left { get; set; } = new();

        public IdentityHolder Right { get; set; } = new();

        public DbSet<DistinctRow> Rows => Set<DistinctRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DistinctRow>(entity =>
            {
                entity.ToTable("distinct_identity_row");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.Property(row => row.Budget).HasColumnName("budget");
                // "Get_Id" and "Get_Other" stay distinct and bind independently.
                entity.HasQueryFilter("distinct", row => row.TenantId == Left.Get().Id && row.Budget == Right.Get().Other);
            });
        }
    }
}
