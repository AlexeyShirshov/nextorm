using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using OneOf;

namespace nextorm.core;

public class SelectExpression
{
    public int Index {get;set;}
    public string? PropertyName {get;set;}
    public OneOf<ScalarSqlCommand,Expression> Expression {get;set;}
    public Type? PropertyType { get; set; }

    public MethodInfo GetDataRecordMethod()
    {
        var recordType = typeof(IDataRecord);

        if (PropertyType == typeof(int))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetInt32))!;
        }
        else if (PropertyType == typeof(long))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetInt64))!;
        }
        else
            throw new NotSupportedException(PropertyType?.Name);
    }
}
