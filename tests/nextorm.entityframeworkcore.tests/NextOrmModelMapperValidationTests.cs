using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// <see cref="NextOrmModelMapper.Register"/> refuses EF model shapes the read-only MVP cannot represent
/// correctly, instead of silently mapping them wrong: inheritance (TPH/TPT/TPC) and schema-qualified
/// tables. Global query filters are imported, not refused. It also refuses a second, different mapping
/// for a CLR type already in the process-wide metadata cache.
/// </summary>
public sealed class NextOrmModelMapperValidationTests : EfCoreMetadataCleanup
{
    [Fact]
    public void Register_ShouldAccept_WhenEntityDeclaresQueryFilter()
    {
        var builder = new ModelBuilder();
        builder.Entity<FilteredRow>(entity =>
        {
            entity.ToTable("filtered_row");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.HasQueryFilter(x => x.Id > 0);
        });

        var act = () => NextOrmModelMapper.Register(builder.FinalizeModel());

        act.Should().NotThrow();
        var filter = DataContextCache.Metadata[typeof(FilteredRow)].Filters.Should().ContainSingle().Subject;
        filter.Key.Should().Be(QueryFilters.AnonymousKey);
        filter.Lambda.Should().NotBeNull();
    }

    [Fact]
    public void Register_ShouldThrow_WhenModelUsesInheritance()
    {
        var builder = new ModelBuilder();
        builder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("vehicle");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
        });
        builder.Entity<Car>().HasBaseType<Vehicle>();

        var act = () => NextOrmModelMapper.Register(builder.FinalizeModel());

        act.Should().Throw<NotSupportedException>().WithMessage("*inheritance*");
    }

    [Fact]
    public void Register_ShouldThrow_WhenEntityIsSchemaQualified()
    {
        var builder = new ModelBuilder();
        builder.Entity<SchemaRow>(entity =>
        {
            entity.ToTable("schema_table", "reporting");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
        });

        var act = () => NextOrmModelMapper.Register(builder.FinalizeModel());

        act.Should().Throw<NotSupportedException>().WithMessage("*schema*");
    }

    [Fact]
    public void Register_ShouldThrow_WhenSameTypeIsAlreadyMappedDifferently()
    {
        var first = new ModelBuilder();
        first.Entity<ConflictRow>(entity =>
        {
            entity.ToTable("conflict_first");
            entity.Property(x => x.Id).HasColumnName("first_id");
        });
        NextOrmModelMapper.Register(first.FinalizeModel());

        var second = new ModelBuilder();
        second.Entity<ConflictRow>(entity =>
        {
            entity.ToTable("conflict_second");
            entity.Property(x => x.Id).HasColumnName("second_id");
        });

        var act = () => NextOrmModelMapper.Register(second.FinalizeModel());

        act.Should().Throw<InvalidOperationException>().WithMessage("*already mapped*");
    }

    [Fact]
    public void Register_ShouldAllow_IdenticalReRegistration()
    {
        var first = new ModelBuilder();
        first.Entity<RepeatedRow>(entity =>
        {
            entity.ToTable("repeated_table");
            entity.Property(x => x.Id).HasColumnName("repeated_id");
        });
        NextOrmModelMapper.Register(first.FinalizeModel());

        var second = new ModelBuilder();
        second.Entity<RepeatedRow>(entity =>
        {
            entity.ToTable("repeated_table");
            entity.Property(x => x.Id).HasColumnName("repeated_id");
        });

        var act = () => NextOrmModelMapper.Register(second.FinalizeModel());

        act.Should().NotThrow();
    }

    private sealed class FilteredRow
    {
        public long Id { get; set; }
    }

    private class Vehicle
    {
        public long Id { get; set; }
    }

    private sealed class Car : Vehicle
    {
    }

    private sealed class SchemaRow
    {
        public long Id { get; set; }
    }

    private sealed class ConflictRow
    {
        public long Id { get; set; }
    }

    private sealed class RepeatedRow
    {
        public long Id { get; set; }
    }
}
