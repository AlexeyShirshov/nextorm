using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using OneOf;

namespace nextorm.core;

public class SelectExpression
{
    private readonly Type _realType;
    public int Index {get;set;}
    public string? PropertyName {get;set;}
    public OneOf<QueryCommand,Expression> Expression {get;set;}
    public Type PropertyType { get; set; }

    private readonly bool _nullable;

    public bool Nullable { get; }


    public SelectExpression(Type propertyType)
    {
        PropertyType = propertyType;
        _nullable =PropertyType.IsGenericType && PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        Nullable = PropertyType.IsClass || _nullable;
        
        if (_nullable)
        {
            _realType =  System.Nullable.GetUnderlyingType(PropertyType)!;
        }
        else 
        {
            _realType = PropertyType;
        }
    }

    public MethodInfo GetDataRecordMethod()
    {
        var recordType = typeof(IDataRecord);

        if (_realType == typeof(int))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetInt32))!;
        }
        else if (_realType == typeof(long))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetInt64))!;
        }
        else if (_realType == typeof(DateTime))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetDateTime))!;
        }
        else if (_realType == typeof(string))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetString))!;
        }
        else if (_realType == typeof(bool))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetBoolean))!;
        }
        else if (_realType == typeof(double))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetDouble))!;
        }
        else if (_realType == typeof(decimal))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetDecimal))!;
        }
        else if (_realType == typeof(float))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetFloat))!;
        }
        else if (_realType == typeof(short))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetInt16))!;
        }
        else if (_realType == typeof(byte))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetByte))!;
        }
        else if (_realType == typeof(Guid))
        {
            return recordType.GetMethod(nameof(IDataRecord.GetGuid))!;
        }
        else
            throw new NotSupportedException($"Property '{PropertyName}' with index ({Index}) has type {_realType} which is not supported");
    }
}
