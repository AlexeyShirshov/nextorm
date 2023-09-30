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
}