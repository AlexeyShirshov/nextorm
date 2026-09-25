using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Core.Tests;

[SqlTable("attributed_entity")]
public interface IAttributedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    [Column("total")]
    int Total { get; set; }
}

public sealed class AttributedEntity : IAttributedEntity
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int Total { get; set; }
}

public sealed class ConventionalEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class ConventionallyNamedEntity
{
    public long ConventionallyNamedEntityId { get; set; }
    public string? Name { get; set; }
}

public sealed class FluentKeyEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class GetOnlyMemberEntity
{
    public int Id { get; set; }
    public int Computed { get; } = 5;
}

public sealed class NoKeyEntity
{
    public string? Name { get; set; }
}

public sealed class AttributedDurationEntity
{
    public int Id { get; set; }

    [Duration(DurationUnit.Seconds, Precision = 3)]
    public TimeSpan Span { get; set; }

    public TimeSpan Untuned { get; set; }
}

public sealed class FluentDurationEntity
{
    public int Id { get; set; }
    public TimeSpan Span { get; set; }
}

/// <summary>
/// Unit tests for the DML metadata flags (<see cref="IPropertyMetadata.IsKey"/>/<c>IsIdentity</c>/
/// <c>IsComputed</c>), their attribute/fluent/convention sources, and the in-memory rejection of
/// mutations. No database is involved.
/// </summary>
public class InsertMetadataTests
{
    [Fact]
    public void Attributes_ShouldSetKeyIdentityAndComputed()
    {
        var metadata = new EntityMetadataBuilder<IAttributedEntity>().Build();

        var id = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(IAttributedEntity.Id));
        var name = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(IAttributedEntity.Name));
        var total = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(IAttributedEntity.Total));

        id.IsKey.Should().BeTrue();
        id.IsIdentity.Should().BeTrue();
        id.IsComputed.Should().BeFalse();

        name.IsKey.Should().BeFalse();
        name.IsIdentity.Should().BeFalse();

        total.IsComputed.Should().BeTrue();
        total.IsIdentity.Should().BeFalse();
    }

    [Fact]
    public void Attributes_OnImplementedInterface_ShouldApplyToClassMetadata()
    {
        var metadata = new EntityMetadataBuilder<AttributedEntity>().Build();

        var id = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(AttributedEntity.Id));

        id.IsKey.Should().BeTrue();
        id.IsIdentity.Should().BeTrue();
        id.IsComputed.Should().BeFalse();
        id.ColumnName.Should().Be("id");
        id.IsColumnNameAuto.Should().BeFalse();
    }

    [Fact]
    public void KeyConvention_ShouldInferIdProperty()
    {
        var metadata = new EntityMetadataBuilder<ConventionalEntity>().Build();

        metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(ConventionalEntity.Id)).IsKey.Should().BeTrue();
        metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(ConventionalEntity.Name)).IsKey.Should().BeFalse();
    }

    [Fact]
    public void KeyConvention_ShouldInferTypeNameIdProperty()
    {
        var metadata = new EntityMetadataBuilder<ConventionallyNamedEntity>().Build();

        metadata.Properties.Single(p => p.PropertyInfo.Name == "ConventionallyNamedEntityId").IsKey.Should().BeTrue();
        metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(ConventionallyNamedEntity.Name)).IsKey.Should().BeFalse();
    }

    [Fact]
    public void FluentMapping_ShouldSetKeyIdentityAndComputed()
    {
        var builder = new EntityMetadataBuilder<FluentKeyEntity>();
        _ = builder.Property(x => x.Id).Key().Identity().Computed().HasColumnName("id");
        var metadata = builder.Build();

        var id = metadata.Properties.Single();
        id.IsKey.Should().BeTrue();
        id.IsIdentity.Should().BeTrue();
        id.IsComputed.Should().BeTrue();
        id.ColumnName.Should().Be("id");
    }

    [Fact]
    public void DurationAttribute_ShouldSetUnitAndPrecision()
    {
        var metadata = new EntityMetadataBuilder<AttributedDurationEntity>().Build();

        var span = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(AttributedDurationEntity.Span));
        var untuned = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(AttributedDurationEntity.Untuned));

        span.DurationUnit.Should().Be(DurationUnit.Seconds);
        span.DurationPrecision.Should().Be(3);

        // A TimeSpan property without the attribute keeps a null unit: the provider then decides
        // (native type, or ticks for an integer-stored duration).
        untuned.DurationUnit.Should().BeNull();
        untuned.DurationPrecision.Should().Be(0);
    }

    [Fact]
    public void FluentDuration_ShouldSetUnitAndPrecision()
    {
        var builder = new EntityMetadataBuilder<FluentDurationEntity>();
        _ = builder.Property(x => x.Span).Duration(DurationUnit.Milliseconds, 2).HasColumnName("span");
        var metadata = builder.Build();

        var span = metadata.Properties.Single();
        span.DurationUnit.Should().Be(DurationUnit.Milliseconds);
        span.DurationPrecision.Should().Be(2);
    }

    [Fact]
    public void InMemoryContext_ShouldRejectInsert()    {
        using var ctx = new InMemoryDataContext();

        var builder = ctx.InsertInto<ConventionalEntity>().Value(x => x.Name, "a");

        var toSql = () => builder.ToSql();
        var insert = () => ctx.InsertInto<ConventionalEntity>().Value(x => x.Name, "a").Insert();

        toSql.Should().Throw<NotSupportedException>();
        insert.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InMemoryContext_ShouldRejectInsertFromQuery()
    {
        using var ctx = new InMemoryDataContext();

        var builder = ctx.InsertInto<ConventionalEntity>()
            .Values(ctx.From<ConventionalEntity>(), x => new { x.Name });

        var toSql = () => builder.ToSql();
        var insert = () => ctx.InsertInto<ConventionalEntity>()
            .Values(ctx.From<ConventionalEntity>(), x => new { x.Name })
            .Insert();

        toSql.Should().Throw<NotSupportedException>();
        insert.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InMemoryContext_ShouldRejectDataModifyingCte()
    {
        using var ctx = new InMemoryDataContext();

        var insert = ctx.InsertInto<ConventionalEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => new { x.Id });

        var act = () => ctx.With("ins", insert);

        act.Should().Throw<NotSupportedException>().WithMessage("*PostgreSQL*");
    }

    [Fact]
    public void ReturningIdentity_OnNonIdentityColumn_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<ConventionalEntity>()
            .Value(x => x.Name, "a")
            .ReturningIdentity(x => x.Id);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReturningIdentity_Function_OnInMemory_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        var builder = ctx.InsertInto<ConventionalEntity>().Value(x => x.Name, "a").ReturningIdentity<long>();

        var act = () => builder.Single();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ReturningKey_OnEntityWithoutKey_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<NoKeyEntity>()
            .Value(x => x.Name, "a")
            .ReturningKey<int>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReturningKey_OnMismatchedType_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<ConventionalEntity>()
            .Value(x => x.Name, "a")
            .ReturningKey<string>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InMemoryContext_ShouldRejectReturning()
    {
        using var ctx = new InMemoryDataContext();
        var builder = ctx.InsertInto<ConventionalEntity>().Value(x => x.Name, "a").Returning();

        var toSql = () => builder.ToSql();
        var insert = () => builder.Single();

        toSql.Should().Throw<NotSupportedException>();
        insert.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Returning_OnUnmappedMember_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<GetOnlyMemberEntity>()
            .Value(x => x.Id, 1)
            .Returning(x => x.Computed);

        act.Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void Returning_WholeEntity_OnInterface_ShouldParseForSqlGeneration()
    {
        using var ctx = new InMemoryDataContext();

        var builder = ctx.InsertInto<IAttributedEntity>()
            .Value(x => x.Name, "a")
            .Returning();

        builder.Should().NotBeNull();
    }
}
