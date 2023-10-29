using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using nextorm.core;

namespace nextorm.core.benchmark;

[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id {get;set;}
}

[Table("simple_entity")]
public class SimpleEntity : ISimpleEntity
{
    public int Id { get; set; }
}