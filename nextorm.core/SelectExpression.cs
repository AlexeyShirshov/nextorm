using System.Linq.Expressions;
using OneOf;

namespace nextorm.core;

public class SelectExpression
{
    public int Index {get;set;}
    public string? PropertyName {get;set;}
    public OneOf<ScalarSqlCommand,Expression> Expression {get;set;}
    public Type? PropertyType { get; set; }
}
