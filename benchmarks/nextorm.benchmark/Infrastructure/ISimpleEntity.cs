using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.Benchmark;

[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    //[Key]
    [Column("id")]
    int Id { get; set; }
}

[Table("simple_entity")]
public sealed class SimpleEntity : ISimpleEntity
{
    public int Id { get; set; }
}