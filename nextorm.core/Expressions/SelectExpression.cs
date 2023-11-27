using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using OneOf;

namespace nextorm.core;

public class SelectExpression
{
    private readonly Type _realType;
    private readonly bool _nullable;
    public int Index { get; set; }
    public string? PropertyName { get; set; }
    public OneOf<QueryCommand, Expression> Expression { get; set; }
    public Type PropertyType { get; set; }
    public bool Nullable { get; }
    internal PropertyInfo? PropertyInfo { get; set; }
    public List<QueryCommand>? ReferencedQueries { get; set; }

    public SelectExpression(Type propertyType)
    {
        PropertyType = propertyType;
        _nullable = PropertyType.IsGenericType && PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        Nullable = PropertyType.IsClass || _nullable;

        if (_nullable)
        {
            _realType = System.Nullable.GetUnderlyingType(PropertyType)!;
        }
        else
        {
            _realType = PropertyType;
        }
    }
    private readonly static MethodInfo GetInt32MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt32))!;
    private readonly static MethodInfo GetInt64MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt64))!;
    private readonly static MethodInfo GetDateTimeMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDateTime))!;
    private readonly static MethodInfo GetStringMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetString))!;
    private readonly static MethodInfo GetBooleanMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetBoolean))!;
    private readonly static MethodInfo GetDoubleMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDouble))!;
    private readonly static MethodInfo GetDecimalMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDecimal))!;
    private readonly static MethodInfo GetFloatMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetFloat))!;
    private readonly static MethodInfo GetInt16MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt16))!;
    private readonly static MethodInfo GetByteMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetByte))!;
    private readonly static MethodInfo GetGuidMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetGuid))!;
    public MethodInfo GetDataRecordMethod()
    {
        // var recordType = typeof(IDataRecord);

        if (_realType == typeof(int))
        {
            return GetInt32MI;
        }
        else if (_realType == typeof(long))
        {
            return GetInt64MI;
        }
        else if (_realType == typeof(DateTime))
        {
            return GetDateTimeMI;
        }
        else if (_realType == typeof(string))
        {
            return GetStringMI;
        }
        else if (_realType == typeof(bool))
        {
            return GetBooleanMI;
        }
        else if (_realType == typeof(double))
        {
            return GetDoubleMI;
        }
        else if (_realType == typeof(decimal))
        {
            return GetDecimalMI;
        }
        else if (_realType == typeof(float))
        {
            return GetFloatMI;
        }
        else if (_realType == typeof(short))
        {
            return GetInt16MI;
        }
        else if (_realType == typeof(byte))
        {
            return GetByteMI;
        }
        else if (_realType == typeof(Guid))
        {
            return GetGuidMI;
        }
        else
            throw new NotSupportedException($"Property '{PropertyName}' with index ({Index}) has type {_realType} which is not supported");
    }
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = new HashCode();

            hash.Add(Index);

            hash.Add(PropertyType);

            hash.Add(PropertyName);

            Expression.Switch(cmd => hash.Add(cmd), exp => hash.Add(exp, new PreciseExpressionEqualityComparer()));

            if (ReferencedQueries is not null)
                foreach (var cmd in ReferencedQueries)
                    hash.Add(cmd);

            return hash.ToHashCode();
        }
    }
    public override bool Equals(object? obj)
    {
        return Equals(obj as SelectExpression);
    }
    public bool Equals(SelectExpression? exp)
    {
        if (exp is null) return false;

        if (Index != exp.Index) return false;

        if (PropertyType != exp.PropertyType) return false;

        if (PropertyName != exp.PropertyName) return false;

        if (Expression.IsT0 != exp.Expression.IsT0
            || !Expression.Match(cmd => cmd.Equals(exp.Expression.AsT0), e => new PreciseExpressionEqualityComparer().Equals(e, exp.Expression.AsT1)))
            return false;

        if (ReferencedQueries is null && exp.ReferencedQueries is not null) return false;
        if (ReferencedQueries is not null && exp.ReferencedQueries is null) return false;

        if (ReferencedQueries is not null && exp.ReferencedQueries is not null)
        {
            if (ReferencedQueries.Count != exp.ReferencedQueries.Count) return false;

            for (int i = 0; i < ReferencedQueries.Count; i++)
            {
                if (!ReferencedQueries[i].Equals(exp.ReferencedQueries[i])) return false;
            }
        }

        return true;
    }
}
