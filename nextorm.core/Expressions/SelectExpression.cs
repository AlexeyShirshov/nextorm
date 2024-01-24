using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace nextorm.core;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
public sealed class SelectExpression : IEquatable<SelectExpression>
{
    private readonly Type _realType;
    //private readonly bool _nullable;
    public readonly bool Nullable;
    public int Index { get; set; }
    public string? PropertyName { get; set; }
    public Expression? Expression { get; set; }
    public Type PropertyType { get; set; }
    internal PropertyInfo? PropertyInfo { get; set; }
    // public List<QueryCommand>? ReferencedQueries { get; set; }
    //private readonly IDictionary<ExpressionKey, Delegate> _expCache;
    private readonly IQueryProvider _queryProvider;

    public SelectExpression(Type propertyType, IQueryProvider queryProvider)
    {
        PropertyType = propertyType;
        var nullable = PropertyType.IsGenericType && PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        Nullable = PropertyType.IsClass || nullable;

        if (nullable)
        {
            _realType = System.Nullable.GetUnderlyingType(PropertyType)!;
        }
        else
        {
            _realType = PropertyType;
        }
        //_expCache = expCache;
        _queryProvider = queryProvider;
    }
    //public readonly bool IsEmpty => _realType is null;
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
    // internal int HashCode;
    internal int PlanHashCode;

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

            hash.Add(Expression, _queryProvider.GetPreciseExpressionEqualityComparer());

            // if (ReferencedQueries is not null)
            //     foreach (var cmd in ReferencedQueries)
            //         hash.Add(cmd);

            return hash.ToHashCode();
        }
    }
    public override bool Equals(object? obj)
    {
        // if (obj is null) return false;
        // return Equals((SelectExpression)obj);
        return Equals(obj as SelectExpression);
    }
    public bool Equals(SelectExpression? exp)
    {
        if (exp is null) return false;

        if (Index != exp.Index) return false;

        if (PropertyType != exp.PropertyType) return false;

        if (PropertyName != exp.PropertyName) return false;

        if (!_queryProvider.GetPreciseExpressionEqualityComparer().Equals(Expression, exp.Expression))
            return false;

        // if (ReferencedQueries is null && exp.ReferencedQueries is not null) return false;
        // if (ReferencedQueries is not null && exp.ReferencedQueries is null) return false;

        // if (ReferencedQueries is not null && exp.ReferencedQueries is not null)
        // {
        //     if (ReferencedQueries.Count != exp.ReferencedQueries.Count) return false;

        //     for (int i = 0; i < ReferencedQueries.Count; i++)
        //     {
        //         if (!ReferencedQueries[i].Equals(exp.ReferencedQueries[i])) return false;
        //     }
        // }

        return true;
    }
}
