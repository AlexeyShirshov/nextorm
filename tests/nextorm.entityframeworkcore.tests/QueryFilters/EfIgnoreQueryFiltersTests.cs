using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// Translation of EF Core's <c>IgnoreQueryFilters</c> overloads into the nextorm query-local filter
/// scope: the parameterless form disables every filter including anonymous ones, the named form
/// disables only the listed keys (anonymous preserved), null/empty/unknown/duplicate/empty-string keys
/// behave per EF, chained calls union, and the scope is local to the converted query.
/// </summary>
public sealed class EfIgnoreQueryFiltersTests : EfCoreMetadataCleanup
{
    [Fact]
    public void Parameterless_ShouldDisableNamedFilter()
    {
        using var fixture = new Fixture();

        var sql = Sql(fixture.Db, fixture.Db.Documents.IgnoreQueryFilters().ToNextOrm(fixture.Db));

        sql.Should().NotContain("where");
    }

    [Fact]
    public void Parameterless_ShouldDisableAnonymousFilter()
    {
        using var fixture = new Fixture();

        var sql = Sql(fixture.Db, fixture.Db.AuditRows.IgnoreQueryFilters().ToNextOrm(fixture.Db));

        sql.Should().NotContain("where");
    }

    [Fact]
    public void NamedKeys_ShouldDisableListedNamedFilter()
    {
        using var fixture = new Fixture();

        var sql = Sql(fixture.Db, fixture.Db.Documents.IgnoreQueryFilters(new[] { "tenant" }).ToNextOrm(fixture.Db));

        sql.Should().NotContain("where");
    }

    [Fact]
    public void NamedKeys_ShouldPreserveAnonymousFilter()
    {
        using var fixture = new Fixture();

        var sql = Sql(fixture.Db, fixture.Db.AuditRows.IgnoreQueryFilters(new[] { "tenant" }).ToNextOrm(fixture.Db));

        sql.Should().Contain("where");
    }

    [Fact]
    public void UnknownKey_ShouldMatchNothing()
    {
        using var fixture = new Fixture();

        var sql = Sql(fixture.Db, fixture.Db.Documents.IgnoreQueryFilters(new[] { "other" }).ToNextOrm(fixture.Db));

        sql.Should().Contain("where");
    }

    [Fact]
    public void EmptyKeys_ShouldBeNoOp()
    {
        using var fixture = new Fixture();

        var empty = Sql(fixture.Db, fixture.Db.Documents.IgnoreQueryFilters(Array.Empty<string>()).ToNextOrm(fixture.Db));

        empty.Should().Contain("where");
    }

    [Fact]
    public void EmptyStringKey_ShouldNotSelectAnonymous()
    {
        using var fixture = new Fixture();

        var sql = Sql(fixture.Db, fixture.Db.AuditRows.IgnoreQueryFilters(new[] { "", "  " }).ToNextOrm(fixture.Db));

        sql.Should().Contain("where");
    }

    [Fact]
    public void DuplicateKeys_ShouldBeAccepted()
    {
        using var fixture = new Fixture();

        var sql = Sql(fixture.Db, fixture.Db.Documents.IgnoreQueryFilters(new[] { "tenant", "tenant" }).ToNextOrm(fixture.Db));

        sql.Should().NotContain("where");
    }

    [Fact]
    public void ChainedNamedKeys_ShouldUnion()
    {
        using var fixture = new Fixture();

        var sql = Sql(
            fixture.Db,
            fixture.Db.Documents.IgnoreQueryFilters(new[] { "tenant" }).IgnoreQueryFilters(new[] { "other" }).ToNextOrm(fixture.Db));

        sql.Should().NotContain("where");
    }

    [Fact]
    public void ParameterlessDominates_RegardlessOfOrder()
    {
        using var fixture = new Fixture();

        var afterNamed = Sql(
            fixture.Db,
            fixture.Db.Documents.IgnoreQueryFilters(new[] { "other" }).IgnoreQueryFilters().ToNextOrm(fixture.Db));
        var beforeNamed = Sql(
            fixture.Db,
            fixture.Db.Documents.IgnoreQueryFilters().IgnoreQueryFilters(new[] { "other" }).ToNextOrm(fixture.Db));

        afterNamed.Should().NotContain("where");
        beforeNamed.Should().NotContain("where");
    }

    [Fact]
    public void Ignore_ShouldStayQueryLocal()
    {
        using var fixture = new Fixture();

        var ignored = Sql(fixture.Db, fixture.Db.Documents.IgnoreQueryFilters().ToNextOrm(fixture.Db));
        ignored.Should().NotContain("where");

        // A neighbor direct From<T>() on the same bridge context keeps the filter.
        using var neighbor = fixture.Db.CreateNextOrmContext();
        var neighborSql = NextOrmSql.Of(neighbor, neighbor.From<IgnoredDocument>().ToCommand());
        neighborSql.Should().Contain("where");
    }

    [Fact]
    public void Scope_ShouldAffectRenderedPlanIdentity()
    {
        using var fixture = new Fixture();

        var filtered = Sql(fixture.Db, fixture.Db.Documents.ToNextOrm(fixture.Db));
        var ignored = Sql(fixture.Db, fixture.Db.Documents.IgnoreQueryFilters().ToNextOrm(fixture.Db));

        filtered.Should().Contain("where");
        ignored.Should().NotContain("where");
        ignored.Should().NotBe(filtered);
    }

    [Fact]
    public void Scope_ShouldFlowIntoAnySubquery_AndStayLocal()
    {
        using var fixture = new Fixture();
        fixture.Db.Documents.Add(new IgnoredDocument { Id = 1, TenantId = 2 });
        fixture.Db.SaveChanges();

        fixture.Db.Documents.ToNextOrm(fixture.Db).Any().Should().BeFalse();
        fixture.Db.Documents.IgnoreQueryFilters().ToNextOrm(fixture.Db).Any().Should().BeTrue();
        fixture.Db.Documents.ToNextOrm(fixture.Db).Any().Should().BeFalse();

        // The shared Any command is replaced per scope on one context and does not leak to the next call.
        using var context = fixture.Db.CreateNextOrmContext();
        context.From<IgnoredDocument>().Any().Should().BeFalse();
        context.From<IgnoredDocument>().IgnoreFilters().Any().Should().BeTrue();
    }

    private static string Sql<T>(IgnoreFilterContext db, EntityBuilder<T> builder) where T : class
    {
        using var context = db.CreateNextOrmContext();
        return NextOrmSql.Of(context, builder.ToCommand());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        public Fixture()
        {
            _connection.Open();
            Db = new IgnoreFilterContext(_connection, tenantId: 1);
            Db.Database.EnsureCreated();
        }

        public IgnoreFilterContext Db { get; }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class IgnoredDocument
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class IgnoredAuditRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class IgnoreFilterContext(DbConnection connection, int tenantId) : DbContext
    {
        public int TenantId { get; set; } = tenantId;

        public DbSet<IgnoredDocument> Documents => Set<IgnoredDocument>();

        public DbSet<IgnoredAuditRow> AuditRows => Set<IgnoredAuditRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IgnoredDocument>(entity =>
            {
                entity.ToTable("ignore_document");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id");
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });

            modelBuilder.Entity<IgnoredAuditRow>(entity =>
            {
                entity.ToTable("ignore_audit_row");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id");
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.HasQueryFilter(row => row.TenantId == TenantId);
            });
        }
    }
}
