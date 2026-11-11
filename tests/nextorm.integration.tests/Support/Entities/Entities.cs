using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}

public class SimpleEntity : ISimpleEntity
{
    public int Id { get; set; }
}

[SqlTable("complex_entity")]
public interface IComplexEntity
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    [Column("id")]
    long Id { get; set; }
    [Column("nullableint")]
    int? Int { get; set; }
    [MaxLength(100)]
    [Column("somestring")]
    string? String { get; set; }
    [Column("tinyval")]
    byte TinyInt { get; set; }
    [Column("small")]
    short? SmallInt { get; set; }
    [Column("r")]
    float? Real { get; set; }
    [Column("d")]
    double? Double { get; set; }
    [Column("m")]
    decimal? Numeric { get; set; }
    [Column("dt")]
    DateTime? Datetime { get; set; }
    [Column("onlydate")]
    DateTime Date { get; set; }
    [Column("b")]
    bool? Boolean { get; set; }
    [Required]
    [Column("requiredstring")]
    string RequiredString { get; set; }
}

/// <summary>
/// T291 (#291) AVG fixture: four deterministic rows whose non-nullable integral columns hold
/// <c>1, 2, 1, 3</c> and whose nullable twins hold <c>NULL, 1, 2, 3</c>. The value sequence is chosen so
/// every frozen AVG scenario is expressible by filtering on <see cref="Id"/>:
/// <c>id &lt;= 2</c> is the ordinary <c>[1,2]</c> pair, <c>id &lt;= 3</c> is the DISTINCT
/// <c>[1,1,2]</c> triple (ordinary control <c>4/3</c>), <c>id = 1</c> is the one-row case and
/// <see cref="Id"/> <c>!= 3</c> is the ascending <c>[1,2,3]</c> window partition.
/// </summary>
[SqlTable("avg_fixture")]
public interface IAvgFixture
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("b")]
    byte B { get; set; }

    [Column("s")]
    short S { get; set; }

    [Column("i")]
    int I { get; set; }

    [Column("l")]
    long L { get; set; }

    [Column("nb")]
    byte? NB { get; set; }

    [Column("ns")]
    short? NS { get; set; }

    [Column("ni")]
    int? NI { get; set; }

    [Column("nl")]
    long? NL { get; set; }
}

[SqlTable("binary_entity")]
public class BinaryEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("data")]
    public byte[]? Data { get; set; }
}

[SqlTable("lob_entity")]
public class LobEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("data")]
    public byte[]? Data { get; set; }
    [Column("body")]
    public string? Body { get; set; }
}

[SqlTable("array_entity")]
public interface IArrayEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("tags")]
    string[] Tags { get; set; }
    [Column("nums")]
    int[] Nums { get; set; }
}

[SqlTable("insert_entity")]
public interface IInsertEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
}

public sealed class InsertEntity : IInsertEntity
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
}

public sealed record InsertSource(string Name, int Age);

[SqlTable("bulk_destination_stub")]
public interface IBulkDestinationEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
}

public sealed class BulkDestinationEntity : IBulkDestinationEntity
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
}

[SqlTable("merge_entity")]
public interface IMergeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
}

public sealed class MergeEntity : IMergeEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
}

[SqlTable("delete_entity")]
public interface IDeleteEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
}

public sealed class DeleteEntity : IDeleteEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
}

/// <summary>
/// Entity whose <see cref="DynamicColumnsAttribute"/> store contributes physical columns to an
/// INSERT/UPDATE/MERGE. The physical table carries <c>alpha</c>, <c>beta</c> and the defaulted
/// <c>seeded</c> column, so a key omitted from the store is distinguishable from a key bound to NULL.
/// </summary>
[SqlTable("dynamic_entity")]
public sealed class DynamicColumnsEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}

[SqlTable("eager_parent")]
public sealed class EagerParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("name")]
    public string? Name { get; set; }
    public ICollection<EagerChild> Children { get; } = new List<EagerChild>();
}

[SqlTable("eager_child")]
public sealed class EagerChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("parent_id")]
    public int ParentId { get; set; }
    [Column("name")]
    public string? Name { get; set; }
    // T212-D5 nested (level 2+) fixture: a second level in the same eager_child table, keyed by the
    // child-level id. Get-only so the level-1 single-query projection ignores it (a settable collection
    // would be mapped as an unsupported column) while split/single nested loaders can still fill it.
    public ICollection<EagerGrandChild> GrandChildren { get; } = new List<EagerGrandChild>();
}

/// <summary>
/// T212-D5 level-two child of <see cref="EagerChild"/>: the same <c>eager_child</c> table, so nested
/// integration coverage needs no new provider DDL. Rows are grandchildren when their
/// <c>parent_id</c> points at a level-one child id.
/// </summary>
[SqlTable("eager_child")]
public sealed class EagerGrandChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    // Deliberately named ChildId (column stays parent_id): the single-query projection aliases this key
    // as ChildId, so a nested join of the same table does not collide with the level-one child's
    // ParentId alias (ClickHouse rejects two SELECT items sharing one alias).
    [Column("parent_id")]
    public int ChildId { get; set; }
    [Column("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Parent whose eager link key is the composite <c>(Tenant, LinkId)</c>, deliberately not the table key
/// (<see cref="RowId"/>). The variant-matrix :236 integration fixture: the seeded keys overlap on one
/// component each, so a partition that used only one component blends two parents.
/// </summary>
[SqlTable("eager_composite_parent")]
public sealed class EagerCompositeParent
{
    [Key]
    [Column("row_id")]
    public int RowId { get; set; }
    [Column("tenant")]
    public int Tenant { get; set; }
    [Column("link_id")]
    public int LinkId { get; set; }
    public ICollection<EagerCompositeChild> Children { get; } = new List<EagerCompositeChild>();
}

/// <summary>Child keyed to its parent by the same composite <c>(Tenant, ParentId)</c> link key.</summary>
[SqlTable("eager_composite_child")]
public sealed class EagerCompositeChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("tenant")]
    public int Tenant { get; set; }
    [Column("parent_id")]
    public int ParentId { get; set; }
    [Column("sort_key")]
    public int SortKey { get; set; }
}
