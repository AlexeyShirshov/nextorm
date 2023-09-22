using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using nextorm.core;

namespace nextorm.sqlite.tests;

[SqlTable("complex_entity")]
public interface IComplexEntity
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    [Column("id")]  
    long Id {get;set;}
    [Column("nullableInt")]  
    int? Int {get;set;}
    [MaxLength(100)]
    [Column("someString")]      
    string? String {get;set;}
    [Column("tinyInt")]  
    byte TinyInt {get;set;}
    [Column("small")]  
    short? SmallInt {get;set;}
    [Column("r")]  
    float? Real {get;set;}
    [Column("d")]  
    double? Double {get;set;}
    [Column("m")]  
    decimal? Numeric {get;set;}
    [Column("dt")]  
    DateTime? Datetime {get;set;}
    [Column("onlyDate")]  
    DateTime Date {get;set;}
    [Column("b")]  
    bool? Boolean {get;set;}
    [Required]
    [Column("requiredString")]  
    string RequiredString {get;set;}
}