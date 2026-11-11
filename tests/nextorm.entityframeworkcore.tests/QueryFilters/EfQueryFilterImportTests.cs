using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// <see cref="NextOrmModelMapper.Register"/> imports EF Core 10 query filters (named/keyed and
/// anonymous) into nextorm's metadata: the translated predicate is a two-parameter
/// <c>(entity, IDataContext)</c> lambda that reads the live owner through
/// <see cref="EfCoreFilterBinding.GetOwner"/>. Unsupported EF filter shapes are refused while the
/// bridge is created, and an identical re-registration is idempotent.
/// </summary>
public sealed class EfQueryFilterImportTests : EfCoreMetadataCleanup
{
    private const string Table = "filter_doc";

    private static int StaticTenantId = 7;

    [Fact]
    public void Register_ShouldImportNamedAndAnonymousFilters()
    {
        using var db = new FilterContext(tenantId: 7);

        NextOrmModelMapper.Register(db.Model);

        var document = DataContextCache.Metadata[typeof(Document)];
        var named = document.Filters.Should().ContainSingle().Subject;
        named.Key.Should().Be("tenant");
        named.Lambda.Should().NotBeNull();
        named.Func.Should().BeNull();
        named.Lambda!.ToString().Should().Contain("GetOwner");

        var audit = DataContextCache.Metadata[typeof(AuditRow)];
        var anonymous = audit.Filters.Should().ContainSingle().Subject;
        anonymous.Key.Should().Be(QueryFilters.AnonymousKey);
        anonymous.Lambda.Should().NotBeNull();
        anonymous.Func.Should().BeNull();
    }

    [Fact]
    public void Register_ShouldImportLiteralScalarFilter()
    {
        var model = BuildModel(entity => entity.HasQueryFilter(row => row.Value >= 100));

        NextOrmModelMapper.Register(model);

        var metadata = DataContextCache.Metadata[typeof(Document)];
        var filter = metadata.Filters.Should().ContainSingle().Subject;
        filter.Key.Should().Be(QueryFilters.AnonymousKey);
        filter.Lambda.Should().NotBeNull();
    }

    [Fact]
    public void Register_ShouldThrow_WhenFilterCapturesClosureLocal()
    {
        var floor = 100;
        var model = BuildModel(entity => entity.HasQueryFilter(row => row.Value >= floor));

        var act = () => NextOrmModelMapper.Register(model);

        act.Should().Throw<NotSupportedException>().WithMessage("*closure/local*");
    }

    [Fact]
    public void Register_ShouldThrow_WhenFilterCapturesStaticMember()
    {
        var model = BuildModel(entity => entity.HasQueryFilter(row => row.TenantId == StaticTenantId));

        var act = () => NextOrmModelMapper.Register(model);

        act.Should().Throw<NotSupportedException>().WithMessage("*static-member*");
    }

    [Fact]
    public void Register_ShouldThrow_WhenFilterUsesEfProperty()
    {
        var model = BuildModel(entity => entity.HasQueryFilter(row => EF.Property<int>(row, "tenant_id") == 1));

        var act = () => NextOrmModelMapper.Register(model);

        act.Should().Throw<NotSupportedException>().WithMessage("*EF.Property*");
    }

    [Fact]
    public void Register_ShouldThrow_WhenFilterUsesEfFunctions()
    {
        var model = BuildModel(entity => entity.HasQueryFilter(row => EF.Functions.Like("a", "a")));

        var act = () => NextOrmModelMapper.Register(model);

        act.Should().Throw<NotSupportedException>().WithMessage("*EF.Functions*");
    }

    [Fact]
    public void Register_ShouldThrow_WhenFilterUsesNavigation()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<NavRow>(entity =>
        {
            entity.ToTable("nav_row");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.ParentId).HasColumnName("parent_id");
            entity.HasOne<NavRow>().WithMany().HasForeignKey(row => row.ParentId);
            entity.HasQueryFilter(row => row.Parent != null);
        });

        var act = () => NextOrmModelMapper.Register(modelBuilder.FinalizeModel());

        act.Should().Throw<NotSupportedException>().WithMessage("*navigation*");
    }

    [Fact]
    public void Register_ShouldBeIdempotent_WhenSameModelRegisteredTwice()
    {
        NextOrmModelMapper.Register(BuildModel(entity => entity.HasQueryFilter(row => row.Value >= 100)));

        var act = () => NextOrmModelMapper.Register(BuildModel(entity => entity.HasQueryFilter(row => row.Value >= 100)));

        act.Should().NotThrow();
        DataContextCache.Metadata[typeof(Document)].Filters.Should().ContainSingle();
    }

    [Fact]
    public void Register_ShouldThrow_WhenBridgeFiltersChanged()
    {
        NextOrmModelMapper.Register(BuildModel(entity => entity.HasQueryFilter(row => row.Value >= 100)));

        var act = () => NextOrmModelMapper.Register(BuildModel(entity => entity.HasQueryFilter(row => row.Value >= 200)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*different*");
    }

    [Fact]
    public void Register_ShouldThrow_WhenDifferentContextTypeUsesSameEntityAndKey()
    {
        using var first = new FirstContext(tenantId: 1);
        using var second = new SecondContext(tenantId: 2);

        NextOrmModelMapper.Register(first.Model);

        var act = () => NextOrmModelMapper.Register(second.Model);

        act.Should().Throw<InvalidOperationException>().WithMessage("*different*");
    }

    private static IModel BuildModel(Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Document>> configure)
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable(Table);
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Value).HasColumnName("value");
            entity.Property(row => row.IsDeleted).HasColumnName("is_deleted");
            configure(entity);
        });

        return modelBuilder.FinalizeModel();
    }

    private sealed class Document
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int Value { get; set; }
        public bool IsDeleted { get; set; }
    }

    private sealed class FilterContext(int tenantId) : DbContext
    {
        public int TenantId { get; set; } = tenantId;

        public DbSet<Document> Documents => Set<Document>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>(entity =>
            {
                ConfigureDocument(entity);
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });

            // EF Core 10 refuses to apply an anonymous and a named filter to the same entity, so the
            // anonymous import is exercised on a second entity of the same context.
            modelBuilder.Entity<AuditRow>(entity =>
            {
                entity.ToTable("audit_row");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id");
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.HasQueryFilter(row => row.TenantId == TenantId);
            });
        }
    }

    private sealed class AuditRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class NavRow
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public NavRow? Parent { get; set; }
    }

    private sealed class FirstContext(int tenantId) : DbContext
    {
        public int TenantId { get; set; } = tenantId;

        public DbSet<Document> Documents => Set<Document>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>(entity =>
            {
                ConfigureDocument(entity);
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });
        }
    }

    private sealed class SecondContext(int tenantId) : DbContext
    {
        public int TenantId { get; set; } = tenantId;

        public DbSet<Document> Documents => Set<Document>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>(entity =>
            {
                ConfigureDocument(entity);
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });
        }
    }

    private static void ConfigureDocument(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Document> entity)
    {
        entity.ToTable(Table);
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasColumnName("id");
        entity.Property(row => row.TenantId).HasColumnName("tenant_id");
        entity.Property(row => row.Value).HasColumnName("value");
        entity.Property(row => row.IsDeleted).HasColumnName("is_deleted");
    }
}
