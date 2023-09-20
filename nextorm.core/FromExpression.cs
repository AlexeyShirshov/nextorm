using OneOf;

namespace nextorm.core;

public class FromExpression
{
     public FromExpression(string table)
     {
          Table = table;
     }
     public FromExpression(SqlCommand subQuery)
     {
          Table = subQuery;
     }
     public OneOf<string, SqlCommand> Table { get; set; }
     public string? TableAlias { get; set; }
}