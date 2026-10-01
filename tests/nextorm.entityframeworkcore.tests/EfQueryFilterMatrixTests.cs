using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// The EF Core query-filter bridge matrix beyond the core import smoke tests: named/anonymous/none in
/// one model, literals, subquery and whitespace-key fail-fast, collisions with a core-registered named
/// filter and anonymous additivity, all-or-none publication on a staged failure, empty-filter and
/// cache-clear re-registration, and live direct/nested owner reads on SQLite.
/// </summary>
/// <remarks>
/// EF Core 10 collapses two <c>HasQueryFilter</c> calls with the same key on one entity to the last
/// declaration (verified with a probe), so a duplicate key *within one EF model* cannot reach the
/// bridge's duplicate-key check; the named/named collision is exercised through the core-vs-EF merge
/// path instead. Likewise a wrong-arity lambda and a non-assignable context constant are not reachable
/// through EF's public builder (the first is rejected by EF, the second is compiled as a closure field
/// and therefore hits the closure/local rejection first); those translator branches are documented
/// gaps, not test gaps.
/// </remarks>
public sealed class EfQueryFilterMatrixTests : EfCoreMetadataCleanup
{
    [Fact]
    public void Register_ShouldImportNamedAnonymousAndUnfilteredEntities()
    {
        NextOrmModelMapper.Register(MatrixModel());

        var named = DataContextCache.Metadata[typeof(MatrixDoc)].Filters.Should().ContainSingle().Subject;
        named.Key.Should().Be("tenant");
        named.Lambda.Should().NotBeNull();

        var anonymous = DataContextCache.Metadata[typeof(MatrixAudit)].Filters.Should().ContainSingle().Subject;
        anonymous.Key.Should().Be(QueryFilters.AnonymousKey);

        DataContextCache.Metadata[typeof(MatrixPlain)].Filters.Should().BeEmpty();
    }

    [Fact]
    public void Register_ShouldImportLiteralScalarFilter()
    {
        var builder = new ModelBuilder();
        ConfigureMatrixDoc(builder, "matrix_literal", entity => entity.HasQueryFilter(row => row.Value >= 100 && row.IsDeleted == false));

        NextOrmModelMapper.Register(builder.FinalizeModel());

        var filter = DataContextCache.Metadata[typeof(MatrixDoc)].Filters.Should().ContainSingle().Subject;
        filter.Key.Should().Be(QueryFilters.AnonymousKey);
        filter.Lambda.Should().NotBeNull();
    }

    [Fact]
    public void Register_ShouldThrow_WhenFilterUsesSubquery()
    {
        using var db = new SubqueryContext();

        var act = () => NextOrmModelMapper.Register(db.Model);

        act.Should().Throw<NotSupportedException>().WithMessage("*subquery*");
    }

    [Fact]
    public void Register_ShouldThrow_WhenNamedKeyIsWhitespace()
    {
        var builder = new ModelBuilder();
        ConfigureMatrixDoc(builder, "matrix_whitespace", entity => entity.HasQueryFilter("   ", row => row.TenantId == 1));

        var act = () => NextOrmModelMapper.Register(builder.FinalizeModel());

        act.Should().Throw<NotSupportedException>().WithMessage("*no key*");
    }

    [Fact]
    public void Register_ShouldThrow_WhenEfNamedFilterCollidesWithCoreNamedFilter()
    {
        using (var context = new InMemoryDataContext())
        {
            // Publish the core mapping first, so the bridge takes the merge-with-existing path.
            context.From<CollisionRow>(configuration => configuration.HasQueryFilter("tenant", CollisionRow.TenantFilter()));
        }

        var builder = new ModelBuilder();
        builder.Entity<CollisionRow>(entity =>
        {
            ConfigureCollisionRow(entity);
            entity.HasQueryFilter("tenant", row => row.TenantId == 1);
        });

        var act = () => NextOrmModelMapper.Register(builder.FinalizeModel());

        act.Should().Throw<InvalidOperationException>().WithMessage("*already registered outside*");
    }

    [Fact]
    public void Register_ShouldMerge_WhenEfAndCoreAnonymousFilters()
    {
        using (var context = new InMemoryDataContext())
        {
            context.From<AnonymousCoreRow>(configuration => configuration.HasQueryFilter(row => row.TenantId > 0));
        }

        var builder = new ModelBuilder();
        builder.Entity<AnonymousCoreRow>(entity =>
        {
            ConfigureAnonymousCoreRow(entity);
            entity.HasQueryFilter(row => row.TenantId > 1);
        });

        NextOrmModelMapper.Register(builder.FinalizeModel());

        var filters = DataContextCache.Metadata[typeof(AnonymousCoreRow)].Filters;
        filters.Should().HaveCount(2, "anonymous filters from the core mapping and the EF model are AND-ed, not replaced");
        filters.Should().OnlyContain(filter => filter.Key == QueryFilters.AnonymousKey);
    }

    [Fact]
    public void Register_ShouldAllow_SameNamedKeyOnDifferentEntities()
    {
        var builder = new ModelBuilder();
        builder.Entity<DistinctNamedA>(entity =>
        {
            ConfigureDistinctNamedA(entity);
            entity.HasQueryFilter("tenant", row => row.TenantId == 1);
        });
        builder.Entity<DistinctNamedB>(entity =>
        {
            ConfigureDistinctNamedB(entity);
            entity.HasQueryFilter("tenant", row => row.TenantId == 2);
        });

        NextOrmModelMapper.Register(builder.FinalizeModel());

        DataContextCache.Metadata[typeof(DistinctNamedA)].Filters.Should().ContainSingle().Which.Key.Should().Be("tenant");
        DataContextCache.Metadata[typeof(DistinctNamedB)].Filters.Should().ContainSingle().Which.Key.Should().Be("tenant");
    }

    // The mapper rejects any mapped inheritance hierarchy (TPH/TPT/TPC) before it looks at filters:
    // a root filter carried by a base type whose derived type is mapped must fail with the documented
    // inheritance error and publish nothing, rather than mapping a discriminator-less table.
    [Fact]
    public void InheritedUnsupportedFilterRejected()
    {
        var builder = new ModelBuilder();
        builder.Entity<MatrixFilteredBase>(entity =>
        {
            entity.ToTable("matrix_filtered_base");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.HasQueryFilter("tenant", row => row.Id > 0);
        });
        builder.Entity<MatrixFilteredDerived>().HasBaseType<MatrixFilteredBase>();

        var act = () => NextOrmModelMapper.Register(builder.FinalizeModel());

        act.Should().Throw<NotSupportedException>().WithMessage("*inheritance*");
        DataContextCache.Metadata.Should().NotContainKey(typeof(MatrixFilteredBase));
        DataContextCache.Metadata.Should().NotContainKey(typeof(MatrixFilteredDerived));
    }

    // EF Core 10 applies same-key HasQueryFilter declarations on one entity as last-writer-wins: the
    // earlier declaration is overwritten inside the model builder before any nextorm import runs. The
    // importer therefore sees (and publishes) only the surviving, last declaration.
    [Fact]
    public void DuplicateEfNamedKeyImportsOnlyEfSurvivor()
    {
        var builder = new ModelBuilder();
        ConfigureMatrixDoc(builder, "matrix_duplicate_named", entity =>
        {
            entity.HasQueryFilter("tenant", row => row.TenantId == 1);
            entity.HasQueryFilter("tenant", row => row.TenantId == 2);
        });

        NextOrmModelMapper.Register(builder.FinalizeModel());

        var filter = DataContextCache.Metadata[typeof(MatrixDoc)].Filters
            .Should().ContainSingle("EF Core 10 collapses repeated same-key declarations before import").Subject;
        filter.Key.Should().Be("tenant");
        filter.Lambda!.ToString().Should().Contain("2", "the last same-key declaration is the surviving one");
    }

    // Two independent EF named registrations for the same entity/key are observable when their bodies
    // differ: the fingerprint changes, so the second registration is refused instead of silently winning.
    [Fact]
    public void Register_ShouldThrow_WhenEfNamedFilterChangesAcrossModels()
    {
        NextOrmModelMapper.Register(NamedMatrixModel(variant: 1));

        var act = () => NextOrmModelMapper.Register(NamedMatrixModel(variant: 2));

        act.Should().Throw<InvalidOperationException>().WithMessage("*different*");
        DataContextCache.Metadata[typeof(MatrixDoc)].Filters.Should().ContainSingle().Which.Key.Should().Be("tenant");
    }

    // Repeating the identical complete named registration is idempotent (the owner-free fingerprint
    // ignores the literal value only across the same body; here both models are literally identical).
    [Fact]
    public void Register_ShouldBeIdempotent_WhenNamedModelRegisteredTwice()
    {
        NextOrmModelMapper.Register(NamedMatrixModel(variant: 1));

        var act = () => NextOrmModelMapper.Register(NamedMatrixModel(variant: 1));

        act.Should().NotThrow();
        DataContextCache.Metadata[typeof(MatrixDoc)].Filters.Should().ContainSingle().Which.Key.Should().Be("tenant");
    }

    [Fact]
    public void Register_Failure_ShouldLeavePriorMetadataIntact()
    {
        NextOrmModelMapper.Register(AtomicModel(includeInvalid: false));
        var beforeA = DataContextCache.Metadata[typeof(AtomicA)];

        var act = () => NextOrmModelMapper.Register(AtomicModel(includeInvalid: true));

        act.Should().Throw<NotSupportedException>().WithMessage("*closure/local*");
        DataContextCache.Metadata[typeof(AtomicA)].Should().BeSameAs(beforeA, "the staged batch is published all-or-none");
        DataContextCache.Metadata.Should().ContainKey(typeof(AtomicB));
        DataContextCache.Metadata.Should().NotContainKey(typeof(AtomicC), "the invalid entity must not be partially published");
    }

    // The owner-free fingerprint is order-independent over the AND-ed filter list: two named filters
    // declared in opposite order must collapse onto one fingerprint, so the second registration is the
    // identical one and stays idempotent. Without the sort the two predicates would hash in declaration
    // order and the second registration would be refused as a changed shape.
    [Fact]
    public void FingerprintIsOrderIndependentAcrossMultipleFilters()
    {
        NextOrmModelMapper.Register(MultiFilterModel(zetaFirst: true));

        var metadata = DataContextCache.Metadata[typeof(MultiFilterRow)];
        metadata.Filters.Should().HaveCount(2, "both named filters must be imported");

        var act = () => NextOrmModelMapper.Register(MultiFilterModel(zetaFirst: false));

        act.Should().NotThrow("the fingerprint sorts the filter list, so declaration order is irrelevant");
        DataContextCache.Metadata[typeof(MultiFilterRow)].Should().BeSameAs(metadata,
            "an order-only difference is the identical registration and must stay idempotent");
    }

    [Fact]
    public void Register_ShouldImportEntityWithoutFilters()
    {
        NextOrmModelMapper.Register(MatrixModel());

        DataContextCache.Metadata[typeof(MatrixPlain)].Filters.Should().BeEmpty();
    }

    [Fact]
    public void Register_AfterCacheClear_ShouldSucceedAgain()
    {
        NextOrmModelMapper.Register(MatrixModel());
        DataContextCache.Metadata.Should().ContainKey(typeof(MatrixDoc));

        DataContextCache.Clear();

        var act = () => NextOrmModelMapper.Register(MatrixModel());

        act.Should().NotThrow();
        DataContextCache.Metadata[typeof(MatrixDoc)].Filters.Should().ContainSingle().Which.Key.Should().Be("tenant");
    }

    [Fact]
    public void Import_ShouldReadDirectAndNestedLiveOwnerMembers()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var tenantOne = new LiveMatrixContext(connection, tenantId: 1, threshold: 100);
        using var tenantTwo = new LiveMatrixContext(connection, tenantId: 2, threshold: 300);

        tenantOne.Database.EnsureCreated();
        tenantOne.Direct.AddRange(
            new DirectDoc { Id = 1, TenantId = 1, Value = 10 },
            new DirectDoc { Id = 2, TenantId = 2, Value = 20 });
        tenantOne.Nested.AddRange(
            new NestedDoc { Id = 1, Value = 150 },
            new NestedDoc { Id = 2, Value = 250 });
        tenantOne.SaveChanges();

        using var nextOne = tenantOne.CreateNextOrmContext();
        using var nextTwo = tenantTwo.CreateNextOrmContext();

        nextOne.From<DirectDoc>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(1);
        nextTwo.From<DirectDoc>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(2);

        nextOne.From<NestedDoc>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(1, 2);
        nextTwo.From<NestedDoc>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().BeEmpty("the nested threshold is read from the live owner");

        DataContextCache.Metadata[typeof(DirectDoc)].Filters.Single().Lambda!.ToString().Should().Contain("GetOwner");
        DataContextCache.Metadata[typeof(NestedDoc)].Filters.Single().Lambda!.ToString().Should().Contain("GetOwner");
    }

    private static IModel MatrixModel()
    {
        var builder = new ModelBuilder();
        ConfigureMatrixDoc(builder, "matrix_doc", entity => entity.HasQueryFilter("tenant", row => row.TenantId == 1));
        builder.Entity<MatrixAudit>(entity =>
        {
            entity.ToTable("matrix_audit");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.HasQueryFilter(row => row.TenantId == 1);
        });
        builder.Entity<MatrixPlain>(entity =>
        {
            entity.ToTable("matrix_plain");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
        });

        return builder.FinalizeModel();
    }

    // Two literally distinct named bodies over the same table/key, so the second registration's
    // fingerprint differs. The variant is a builder-time branch; the lambda itself captures no local,
    // so the literal stays a supported scalar constant.
    private static IModel NamedMatrixModel(int variant)
    {
        var builder = new ModelBuilder();
        ConfigureMatrixDoc(builder, "matrix_named", entity =>
        {
            if (variant == 1)
                entity.HasQueryFilter("tenant", row => row.TenantId == 1);
            else
                entity.HasQueryFilter("tenant", row => row.TenantId == 2);
        });

        return builder.FinalizeModel();
    }

    private static IModel MultiFilterModel(bool zetaFirst)
    {
        var builder = new ModelBuilder();
        builder.Entity<MultiFilterRow>(entity =>
        {
            entity.ToTable("multi_filter_row");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Value).HasColumnName("value");
            if (zetaFirst)
            {
                entity.HasQueryFilter("zeta", row => row.Value > 0);
                entity.HasQueryFilter("alpha", row => row.TenantId == 1);
            }
            else
            {
                entity.HasQueryFilter("alpha", row => row.TenantId == 1);
                entity.HasQueryFilter("zeta", row => row.Value > 0);
            }
        });

        return builder.FinalizeModel();
    }

    private static void ConfigureMatrixDoc(ModelBuilder builder, string table, Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<MatrixDoc>> configure)
    {
        builder.Entity<MatrixDoc>(entity =>
        {
            entity.ToTable(table);
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Value).HasColumnName("value");
            entity.Property(row => row.IsDeleted).HasColumnName("is_deleted");
            configure(entity);
        });
    }

    private static void ConfigureCollisionRow(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<CollisionRow> entity)
    {
        entity.ToTable("collision_row");
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(row => row.TenantId).HasColumnName("tenant_id");
    }

    private static void ConfigureAnonymousCoreRow(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<AnonymousCoreRow> entity)
    {
        entity.ToTable("anonymous_core_row");
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(row => row.TenantId).HasColumnName("tenant_id");
    }

    private static void ConfigureDistinctNamedA(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DistinctNamedA> entity)
    {
        entity.ToTable("distinct_named_a");
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(row => row.TenantId).HasColumnName("tenant_id");
    }

    private static void ConfigureDistinctNamedB(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DistinctNamedB> entity)
    {
        entity.ToTable("distinct_named_b");
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(row => row.TenantId).HasColumnName("tenant_id");
    }

    private static IModel AtomicModel(bool includeInvalid)
    {
        var builder = new ModelBuilder();
        ConfigureAtomic<AtomicA>(builder, "atomic_a");
        ConfigureAtomic<AtomicB>(builder, "atomic_b");

        if (includeInvalid)
        {
            var floor = 100;
            builder.Entity<AtomicC>(entity =>
            {
                entity.ToTable("atomic_c");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter(row => row.Value >= floor);
            });
        }

        return builder.FinalizeModel();
    }

    private static void ConfigureAtomic<T>(ModelBuilder builder, string table) where T : class
    {
        builder.Entity<T>(entity =>
        {
            entity.ToTable(table);
            entity.Property(nameof(AtomicA.Id)).HasColumnName("id");
            entity.Property(nameof(AtomicA.Value)).HasColumnName("value");
            entity.HasKey(nameof(AtomicA.Id));
            entity.HasQueryFilter(BuildAnonymousFilter<T>());
        });
    }

    private static LambdaExpression BuildAnonymousFilter<T>()
    {
        var parameter = Expression.Parameter(typeof(T), "row");
        var value = Expression.Property(parameter, nameof(AtomicA.Value));
        return Expression.Lambda(Expression.GreaterThan(value, Expression.Constant(0)), parameter);
    }

    private sealed class MatrixDoc
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int Value { get; set; }
        public bool IsDeleted { get; set; }
    }

    private sealed class MatrixAudit
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class MatrixPlain
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class MultiFilterRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int Value { get; set; }
    }

    private class MatrixFilteredBase
    {
        public int Id { get; set; }
    }

    private sealed class MatrixFilteredDerived : MatrixFilteredBase
    {
    }

    [SqlTable("collision_row")]
    private sealed class CollisionRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        public static Expression<Func<CollisionRow, IDataContext, bool>> TenantFilter()
            => (entity, context) => entity.TenantId == 1;
    }

    [SqlTable("anonymous_core_row")]
    private sealed class AnonymousCoreRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    private sealed class DistinctNamedA
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class DistinctNamedB
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class AtomicA
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    private sealed class AtomicB
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    private sealed class AtomicC
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    private sealed class DirectDoc
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int Value { get; set; }
    }

    private sealed class NestedDoc
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    private sealed class TenantSettings
    {
        public int Threshold { get; set; }
    }

    private sealed class LiveMatrixContext(DbConnection connection, int tenantId, int threshold) : DbContext
    {
        public int TenantId { get; set; } = tenantId;

        public TenantSettings CurrentTenant { get; set; } = new() { Threshold = threshold };

        public DbSet<DirectDoc> Direct => Set<DirectDoc>();

        public DbSet<NestedDoc> Nested => Set<NestedDoc>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DirectDoc>(entity =>
            {
                entity.ToTable("matrix_direct");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });

            modelBuilder.Entity<NestedDoc>(entity =>
            {
                entity.ToTable("matrix_nested");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter("nested", row => row.Value > CurrentTenant.Threshold);
            });
        }
    }

    private sealed class SubqueryContext : DbContext
    {
        public DbSet<MatrixDoc> Docs => Set<MatrixDoc>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MatrixDoc>(entity =>
            {
                entity.ToTable("matrix_subquery");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id");
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.HasQueryFilter("sub", row => Docs.Any(other => other.TenantId == row.TenantId));
            });
        }
    }
}
