using System.Runtime.CompilerServices;
using OneOf;

namespace nextorm.core;

public sealed class FromExpression
{
     public FromExpression(string table)
     {
          Table = table;
     }
     public FromExpression(QueryCommand subQuery)
     {
          SubQuery = subQuery;
     }
     //public OneOf<string, QueryCommand> Table { get; }
     internal string? TableAliasDELETE;
     public readonly string? Table;
     public readonly QueryCommand? SubQuery;

     // public override int GetHashCode()
     // {
     //      unchecked
     //      {
     //           var hash = new HashCode();

     //           hash.Add(TableAlias);

     //           hash.Add(Table.GetHashCode());

     //           return hash.ToHashCode();
     //      }
     // }
     // public override bool Equals(object? obj)
     // {
     //      return Equals(obj as FromExpression);
     // }
     // public bool Equals(FromExpression? obj)
     // {
     //      if (obj is null) return false;

     //      return TableAlias == obj.TableAlias && Table.Equals(obj.Table);
     // }
     internal FromExpression? CloneForCache()
     {
          if (!string.IsNullOrEmpty(Table)) return this;

          return new FromExpression(SubQuery!.CloneForCache());// { TableAlias = TableAlias };
     }
}