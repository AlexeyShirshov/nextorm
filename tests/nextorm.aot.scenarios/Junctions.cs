using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.AotScenarios;

/// <summary>
/// The many-to-many shapes (#370 Stream B): the split loader, which reads the junction as an ordinary
/// typed entity, and the single-query scalar/composite shapes exercised by the native runner.
/// </summary>
public static partial class AotScenarioCatalog
{
    private const int JunctionBase = 1_500_000_000;
    private const int JunctionParentId = JunctionBase;
    private const int JunctionFirstTagId = JunctionBase + 300;
    private const int JunctionSecondTagId = JunctionBase + 301;
    private const int JunctionFirstLinkId = JunctionBase + 400;
    private const int JunctionSecondLinkId = JunctionBase + 401;

    private static object? ManyToManyLoadWith(IDataContext context)
    {
        RegisterJunctionNavigation(context);

        // Idempotent: drop this shape's rows, seed them, read, drop them again. The ids live in a block
        // no other fixture uses, so the JIT control and the native arm can share the database.
        CleanJunctionRows(context);

        context.CreateInsertBuilder<AotJunctionParent>()
            .Values(new AotJunctionParent { Id = JunctionParentId, Name = "aot-m2m" })
            .Insert();
        context.CreateInsertBuilder<AotJunctionTag>()
            .Values([
                new AotJunctionTag { Id = JunctionFirstTagId, Name = "t1" },
                new AotJunctionTag { Id = JunctionSecondTagId, Name = "t2" },
            ])
            .Insert();
        context.CreateInsertBuilder<AotJunctionLink>()
            .Values([
                new AotJunctionLink { Id = JunctionFirstLinkId, ParentId = JunctionParentId, ChildId = JunctionFirstTagId },
                new AotJunctionLink { Id = JunctionSecondLinkId, ParentId = JunctionParentId, ChildId = JunctionSecondTagId },
            ])
            .Insert();

        var parents = context.From<AotJunctionParent>()
            .Where(parent => parent.Id == JunctionParentId)
            .LoadWith(parent => parent.Tags)
            .ToList();
        var text = string.Join(";", parents.Select(parent =>
            $"{parent.Id}:{parent.Tags.Count}:{string.Join(",", parent.Tags.OrderBy(tag => tag.Id).Select(tag => tag.Name))}"));

        CleanJunctionRows(context);
        return text;
    }

    /// <summary>
    /// Registers the junction navigation. Both arms declare the same mapping, and the registration is
    /// idempotent, so re-declaring it on a reused context is safe.
    /// </summary>
    private static void RegisterJunctionNavigation(IDataContext context)
    {
        context.From<AotJunctionTag>();
        context.From<AotJunctionLink>();
        context.From<AotJunctionParent>(builder => builder
            .HasManyThrough<AotJunctionTag, AotJunctionLink, int, int>(
                parent => parent.Tags, parent => parent.Id, link => link.ParentId, tag => tag.Id, link => link.ChildId));
    }

    private static void CleanJunctionRows(IDataContext context)
    {
        context.CreateDeleteBuilder<AotJunctionLink>()
            .Where(link => link.Id == JunctionFirstLinkId || link.Id == JunctionSecondLinkId)
            .Delete();
        context.CreateDeleteBuilder<AotJunctionTag>()
            .Where(tag => tag.Id == JunctionFirstTagId || tag.Id == JunctionSecondTagId)
            .Delete();
        context.CreateDeleteBuilder<AotJunctionParent>()
            .Where(parent => parent.Id == JunctionParentId)
            .Delete();
    }
}

/// <summary>The many-to-many parent fixture (`eager_parent`); its navigation is declared fluently.</summary>
[SqlTable("eager_parent")]
public sealed class AotJunctionParent
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Parent name.</summary>
    [Column("name")]
    public string? Name { get; set; }

    /// <summary>The tags linked to this parent through the junction.</summary>
    public ICollection<AotJunctionTag> Tags { get; set; } = new List<AotJunctionTag>();
}

/// <summary>The many-to-many child fixture (`eager_tag`).</summary>
[SqlTable("eager_tag")]
public sealed class AotJunctionTag
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Tag name.</summary>
    [Column("name")]
    public string? Name { get; set; }
}

/// <summary>The junction fixture (`eager_link`) that links a parent to a tag.</summary>
[SqlTable("eager_link")]
public sealed class AotJunctionLink
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>The linked parent.</summary>
    [Column("parent_id")]
    public int ParentId { get; set; }

    /// <summary>The linked tag.</summary>
    [Column("child_id")]
    public int ChildId { get; set; }
}

/// <summary>A composite-key parent for NativeAOT many-to-many single-query scenarios.</summary>
[SqlTable("aot_composite_parent")]
public sealed class AotCompositeJunctionParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_key_a")]
    public int ParentKeyA { get; set; }

    [Column("parent_key_b")]
    public int ParentKeyB { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<AotCompositeJunctionTag> Tags { get; set; } = new List<AotCompositeJunctionTag>();
}

/// <summary>A composite-key child for NativeAOT many-to-many single-query scenarios.</summary>
[SqlTable("aot_composite_tag")]
public sealed class AotCompositeJunctionTag
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tag_key_a")]
    public int TagKeyA { get; set; }

    [Column("tag_key_b")]
    public int TagKeyB { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

/// <summary>The composite junction for NativeAOT many-to-many single-query scenarios.</summary>
[SqlTable("aot_composite_link")]
public sealed class AotCompositeJunctionLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_key_a")]
    public int ParentKeyA { get; set; }

    [Column("parent_key_b")]
    public int ParentKeyB { get; set; }

    [Column("tag_key_a")]
    public int TagKeyA { get; set; }

    [Column("tag_key_b")]
    public int TagKeyB { get; set; }
}
