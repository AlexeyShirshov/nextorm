using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextORM.Integration.Tests;

/// <summary>
/// T217 (#217) DateTimeOffset fixture entity. The table (<c>t217_dto</c>) is created by each
/// provider-specific seeder with that provider's native DateTimeOffset column type; the value column
/// always stores the deterministic UTC instant the shared assertions compare against.
/// </summary>
[SqlTable("t217_dto")]
public interface IT217DtoEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("value")]
    DateTimeOffset Value { get; set; }

    [Column("maybe")]
    DateTimeOffset? Maybe { get; set; }
}
