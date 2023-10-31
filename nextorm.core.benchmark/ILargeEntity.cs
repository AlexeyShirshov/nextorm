using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using nextorm.core;

namespace nextorm.core.benchmark;

[SqlTable("large_table")]
public interface ILargeEntity
{
    //[Key]
    [Column("id")]
    int Id { get; set; }
    [Column("someString")]
    string? Str { get; set; }
    [Column("dt")]
    DateTime? Dt { get; set; }
}

[Table("large_table")]
public class LargeEntity : ILargeEntity
{
    public int Id { get; set; }
    [Column("someString")]
    public string? Str { get; set; }
    public DateTime? Dt { get; set; }
}