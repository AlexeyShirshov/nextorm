using OneOf;

namespace nextorm.core;

public class FromExpression
{
     public FromExpression(string table)
     {
          Table = table;
     }
     public FromExpression(QueryCommand subQuery)
     {
          Table = subQuery;
     }
     public OneOf<string, QueryCommand> Table { get; set; }
     public string? TableAlias { get; set; }
     public override int GetHashCode()
     {
          unchecked
          {
               var hash = new HashCode();

               hash.Add(TableAlias);

               hash.Add(Table.GetHashCode());

               return hash.ToHashCode();
          }
     }
     public override bool Equals(object? obj)
     {
          return Equals(obj as FromExpression);
     }
     public bool Equals(FromExpression? obj)
     {
          if (obj is null) return false;

          return TableAlias == obj.TableAlias && Table.Equals(obj.Table);
     }
}