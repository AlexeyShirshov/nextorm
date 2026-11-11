using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// Publication and merge regressions for the EF Core query-filter bridge: an import layered onto an
/// existing core mapping preserves the whole core mapping, publication is failure-atomic and
/// coordinated with the other writers of <see cref="DataContextCache.Metadata"/>, and a mapped
/// property's CLR type is part of the mapping identity.
/// </summary>
public sealed class EfQueryFilterPublicationTests : EfCoreMetadataCleanup
{
    [Fact]
    public void ImportPreservesCoreRelationships()
    {
        using (var context = new InMemoryDataContext())
        {
            context.From<RelParent>(cfg =>
            {
                cfg.Table("rel_parent");
                cfg.HasOne<RelChild, int>(parent => parent.ChildId, child => child.Id);
            });
        }

        var core = DataContextCache.Metadata[typeof(RelParent)];
        core.Relationships.Should().NotBeEmpty("the core mapping declares a relationship before the EF import");

        NextOrmModelMapper.Register(BuildRelModel());

        DataContextCache.Metadata[typeof(RelParent)].Relationships
            .Should().HaveCount(core.Relationships.Count,
                "the EF filter import is layered onto the existing mapping and must preserve its relationships");
    }

    [Fact]
    public void ImportPreservesDynamicColumns()
    {
        using (var context = new InMemoryDataContext())
        {
            context.From<DynRow>(cfg =>
            {
                cfg.Table("dyn_row");
                cfg.Property(row => row.Id).Key();
                cfg.Property(row => row.TenantId);
                cfg.Property(row => row.Extra).DynamicColumnsStore();
            });
        }

        DataContextCache.Metadata[typeof(DynRow)].DynamicColumnsStore
            .Should().NotBeNull("the core mapping declares a dynamic-columns store before the EF import");

        NextOrmModelMapper.Register(BuildDynModel());

        DataContextCache.Metadata[typeof(DynRow)].DynamicColumnsStore
            .Should().NotBeNull("the EF filter import must preserve the core dynamic-columns store");
    }

    [Fact]
    public void ImportPreservesIsTableNameAuto()
    {
        using (var context = new InMemoryDataContext())
            context.From<AutoNamedRow>();

        var core = DataContextCache.Metadata[typeof(AutoNamedRow)];
        core.IsTableNameAuto.Should().BeTrue("the core mapping has no declared table name");

        NextOrmModelMapper.Register(BuildAutoNamedModel(core.TableName!));

        DataContextCache.Metadata[typeof(AutoNamedRow)].IsTableNameAuto
            .Should().BeTrue("the EF filter import must preserve the core auto-name flag");
    }

    [Fact]
    public void PropertyClrTypeChangeConflicts()
    {
        // The mapped property's CLR type is part of the mapping identity: a mapping for the same
        // table/column set whose property is bound to a different CLR type must conflict instead of
        // being silently accepted (the EF-only path cannot produce it, but the public metadata cache
        // accepts an external IEntityMetadata, so the comparison must carry the type).
        var foreignProperty = typeof(ForeignRow).GetProperty(nameof(ForeignRow.Id))!;
        DataContextCache.Metadata[typeof(ClrTypeRow)] = new ForeignMetadata
        {
            TableName = "clr_type_row",
            Properties =
            [
                new ForeignProperty { PropertyInfo = foreignProperty, ColumnName = "Id", IsKey = true },
            ],
        };

        var act = () => NextOrmModelMapper.Register(BuildClrTypeModel());

        act.Should().Throw<InvalidOperationException>().WithMessage("*different table/column layout*");
    }

    [Fact]
    public void FailedImportPublishesNoEntities()
    {
        using (var context = new InMemoryDataContext())
        {
            context.From<AtomicExisting>(cfg =>
                cfg.Table("atomic_existing").HasQueryFilter("tenant", (row, _) => row.TenantId > 0));
        }

        var prior = DataContextCache.Metadata[typeof(AtomicExisting)];

        var act = () => NextOrmModelMapper.Register(BuildAtomicModel());

        act.Should().Throw<InvalidOperationException>().WithMessage("*already registered outside*");
        DataContextCache.Metadata[typeof(AtomicExisting)].Should().BeSameAs(prior, "prior metadata must be preserved");
        DataContextCache.Metadata.Should().NotContainKey(typeof(AtomicFresh),
            "the entry already published from a failed batch must be rolled back, never partially published");
    }

    [Fact]
    public void FailedImportRollsBackNewlyAddedEntity()
    {
        // The first staged entity is brand new, so publication writes it before the second staged entity
        // collides with a core mapping registered outside the bridge and fails. Publication is all-or-none:
        // the already-written brand-new entity must be removed again and the prior mapping preserved.
        using (var context = new InMemoryDataContext())
        {
            context.From<RollbackOmegaConflict>(cfg =>
                cfg.Table("rollback_omega_conflict").HasQueryFilter("tenant", (row, _) => row.TenantId > 0));
        }

        var prior = DataContextCache.Metadata[typeof(RollbackOmegaConflict)];

        var act = () => NextOrmModelMapper.Register(BuildRollbackModel());

        act.Should().Throw<InvalidOperationException>().WithMessage("*already registered outside*");
        DataContextCache.Metadata.Should().NotContainKey(typeof(RollbackAlphaFresh),
            "the entity already published before the later conflict must be rolled back, never partially published");
        DataContextCache.Metadata[typeof(RollbackOmegaConflict)].Should().BeSameAs(prior,
            "the pre-existing core mapping must be preserved");
    }

    [Fact]
    public void ConcurrentCoreAndEfRegistrationDoesNotLoseMapping()
    {
        using var context = new InMemoryDataContext();
        using var insideCoreBuild = new ManualResetEventSlim(false);
        using var letCoreBuildFinish = new ManualResetEventSlim(false);

        var coreThread = new Thread(() =>
        {
            context.From<ConcurrentRow>(cfg =>
            {
                insideCoreBuild.Set();
                letCoreBuildFinish.Wait(TestContext.Current.CancellationToken);
                cfg.Table("concurrent_row");
                cfg.HasQueryFilter("core", (row, _) => row.TenantId > 0);
            });
        });

        coreThread.Start();
        insideCoreBuild.Wait(TestContext.Current.CancellationToken);

        // The bridge publishes while the core writer is paused inside its configuration callback:
        // the core writer must revalidate against the current entry and not clobber the bridge mapping.
        NextOrmModelMapper.Register(BuildConcurrentModel());

        letCoreBuildFinish.Set();
        coreThread.Join();

        var metadata = DataContextCache.Metadata[typeof(ConcurrentRow)];
        metadata.TableName.Should().Be("concurrent_row");
        metadata.Filters.Should().ContainSingle().Which.Key.Should().Be("tenant",
            "the racing core auto-build must not overwrite the published bridge mapping");
    }

    [Fact]
    public void MergeRechecksCurrentCoreMapping()
    {
        using (var context = new InMemoryDataContext())
        {
            context.From<MergeRow>(cfg =>
                cfg.Table("merge_row").HasQueryFilter("first", (row, _) => row.TenantId > 0));
        }

        NextOrmModelMapper.Register(BuildMergeModel());

        // A core writer replaces the entry with a new mapping (as a rebuild after Clear would).
        var replacement = new EntityMetadataBuilder<MergeRow>();
        replacement.Table("merge_row").HasQueryFilter("second", (row, _) => row.TenantId > 1);
        DataContextCache.Metadata[typeof(MergeRow)] = replacement.Build();

        NextOrmModelMapper.Register(BuildMergeModel());

        DataContextCache.Metadata[typeof(MergeRow)].Filters.Select(filter => filter.Key)
            .Should().BeEquivalentTo(["tenant", "second"],
                "the bridge re-reads the current core entry and re-merges instead of reusing a stale merged entry");
    }

    [Fact]
    public void RepeatImport_PreservesExplicitMapping()
    {
        using (var context = new InMemoryDataContext())
        {
            context.From<RepeatFullRow>(cfg =>
            {
                cfg.Table("explicit_repeat_table");
                cfg.HasOne<RelChild, int>(row => row.ChildId, child => child.Id);
                cfg.HasQueryFilter("core", (row, _) => row.TenantId > 0);
            });
        }

        var core = DataContextCache.Metadata[typeof(RepeatFullRow)];
        core.Relationships.Should().NotBeEmpty("the explicit core mapping declares a relationship");
        core.DynamicColumnsStore.Should().NotBeNull("the explicit core mapping declares a dynamic-columns store");

        NextOrmModelMapper.Register(BuildRepeatFullModel());

        var merged = DataContextCache.Metadata[typeof(RepeatFullRow)];
        merged.TableName.Should().Be("explicit_repeat_table",
            "the explicit core mapping is authoritative for the table and survives the EF import");
        merged.Relationships.Should().HaveCount(core.Relationships.Count,
            "the EF import layers its filters on and must preserve the explicit core mapping's relationships");
        merged.DynamicColumnsStore.Should().NotBeNull(
            "the EF import must preserve the explicit core mapping's dynamic-columns store");
        merged.Filters.Select(filter => filter.Key).Should().BeEquivalentTo(["core", "tenant"]);
    }

    [Fact]
    public void RepeatBind_IsIdempotent()
    {
        using (var context = new InMemoryDataContext())
        {
            context.From<RepeatFullRow>(cfg =>
                cfg.Table("explicit_repeat_table").HasQueryFilter("core", (row, _) => row.TenantId > 0));
        }

        NextOrmModelMapper.Register(BuildRepeatFullModel());
        var merged = DataContextCache.Metadata[typeof(RepeatFullRow)];

        var act = () => NextOrmModelMapper.Register(BuildRepeatFullModel());

        act.Should().NotThrow();
        DataContextCache.Metadata[typeof(RepeatFullRow)].Should().BeSameAs(merged,
            "repeating the identical EF import is a no-op that reuses the published merged metadata and preserves the explicit mapping");
    }

    private static IModel BuildRepeatFullModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<RepeatFullRow>(entity =>
        {
            entity.ToTable("ef_repeat_table");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId);
            entity.Property(row => row.ChildId);
            entity.Ignore(row => row.Extra);
            entity.HasQueryFilter("tenant", row => row.TenantId == 1);
        });

        return builder.FinalizeModel();
    }

    private static IModel BuildRelModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<RelParent>(entity =>
        {
            entity.ToTable("rel_parent");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId);
            entity.Property(row => row.ChildId);
            entity.HasQueryFilter("tenant", row => row.TenantId == 1);
        });

        return builder.FinalizeModel();
    }

    private static IModel BuildDynModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<DynRow>(entity =>
        {
            entity.ToTable("dyn_row");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId);
            entity.Ignore(row => row.Extra);
            entity.HasQueryFilter("tenant", row => row.TenantId == 1);
        });

        return builder.FinalizeModel();
    }

    private static IModel BuildAutoNamedModel(string tableName)
    {
        var builder = new ModelBuilder();
        builder.Entity<AutoNamedRow>(entity =>
        {
            entity.ToTable(tableName);
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId);
            entity.HasQueryFilter("tenant", row => row.TenantId == 1);
        });

        return builder.FinalizeModel();
    }

    private static IModel BuildClrTypeModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<ClrTypeRow>(entity =>
        {
            entity.ToTable("clr_type_row");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.HasQueryFilter("tenant", row => row.Id > 0);
        });

        return builder.FinalizeModel();
    }

    private static IModel BuildAtomicModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<AtomicFresh>(entity =>
        {
            entity.ToTable("atomic_fresh");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId);
        });
        builder.Entity<AtomicExisting>(entity =>
        {
            entity.ToTable("atomic_existing");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId);
            entity.HasQueryFilter("tenant", row => row.TenantId == 1);
        });

        return builder.FinalizeModel();
    }

    private static IModel BuildConcurrentModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<ConcurrentRow>(entity =>
        {
            entity.ToTable("concurrent_row");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId);
            entity.HasQueryFilter("tenant", row => row.TenantId == 1);
        });

        return builder.FinalizeModel();
    }

    private static IModel BuildMergeModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<MergeRow>(entity =>
        {
            entity.ToTable("merge_row");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId);
            entity.HasQueryFilter("tenant", row => row.TenantId == 1);
        });

        return builder.FinalizeModel();
    }

    private static IModel BuildRollbackModel()
    {
        // RollbackAlphaFresh sorts before RollbackOmegaConflict (and is declared first), so the new entity
        // is published first and the conflicting later entity must trigger the rollback of the new entry.
        var builder = new ModelBuilder();
        builder.Entity<RollbackAlphaFresh>(entity =>
        {
            entity.ToTable("rollback_alpha_fresh");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId);
            entity.HasQueryFilter("fresh", row => row.TenantId == 1);
        });
        builder.Entity<RollbackOmegaConflict>(entity =>
        {
            entity.ToTable("rollback_omega_conflict");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId);
            entity.HasQueryFilter("tenant", row => row.TenantId == 1);
        });

        return builder.FinalizeModel();
    }

    private sealed class RelParent
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int ChildId { get; set; }
    }

    private sealed class RelChild
    {
        public int Id { get; set; }
    }

    private sealed class DynRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    private sealed class AutoNamedRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class ClrTypeRow
    {
        public int Id { get; set; }
    }

    private sealed class ForeignRow
    {
        public long Id { get; set; }
    }

    private sealed class AtomicFresh
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class AtomicExisting
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class ConcurrentRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class MergeRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class RepeatFullRow
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int ChildId { get; set; }

        [DynamicColumns]
        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    private sealed class RollbackAlphaFresh
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class RollbackOmegaConflict
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class ForeignProperty : IPropertyMetadata
    {
        public required PropertyInfo PropertyInfo { get; init; }

        public required string ColumnName { get; init; }

        public bool IsKey { get; init; }
    }

    private sealed class ForeignMetadata : IEntityMetadata
    {
        public required IReadOnlyList<IPropertyMetadata> Properties { get; init; }

        public required string? TableName { get; init; }
    }
}
