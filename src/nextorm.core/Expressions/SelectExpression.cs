using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Static readonly reflection-metadata fields (GetInt32MI, ...) are intentionally PascalCase as immutable lookup tables; IDE1006 is a suggestion and is not enforced by the build.")]
public sealed class SelectExpression //: IEquatable<SelectExpression>
{
    private readonly Type _realType;
    //private readonly bool _nullable;
    public readonly bool Nullable;
    public int Index { get; set; }
    public string? PropertyName { get; set; }
    public Expression? Expression { get; set; }
    public Type PropertyType { get; set; }
    internal PropertyInfo? PropertyInfo { get; set; }

    /// <summary>
    /// True when a SQL NULL in this column means "no row" (the projection came from a
    /// <c>*OrDefault</c> scalar terminal) and the column is a non-nullable value type, so the reader
    /// must substitute <c>default</c> instead of throwing. Set while the projection is prepared;
    /// read by the provider's column mapper.
    /// </summary>
    public bool DefaultOnNull { get; internal set; }
    // public List<QueryCommand>? ReferencedQueries { get; set; }
    //private readonly IDictionary<ExpressionKey, Delegate> _expCache;
    // private readonly IQueryRegistry _queryProvider;

    public SelectExpression(Type propertyType)
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
        // _queryProvider = queryProvider;
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
    private readonly static MethodInfo GetValueMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    // internal int XxHash32;
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
        else if (_realType == typeof(byte[]))
        {
            // Binary columns (bytea/varbinary/blob) have no typed reader getter; read the value
            // through GetValue and let the caller cast it to byte[].
            return GetValueMI;
        }
        else if (_realType == typeof(string[]))
        {
            // PostgreSQL text[] columns (for example regexp_matches) have no typed reader getter;
            // read the value through GetValue and let the caller cast it to string[].
            return GetValueMI;
        }
        else
            throw new NotSupportedException($"Property '{PropertyName}' with index ({Index}) has type {_realType} which is not supported");
    }
    // public override int GetHashCode()
    // {
    //     unchecked
    //     {
    //         var hash = new XxHash32();

    //         hash.Add(Index);

    //         hash.Add(PropertyType);

    //         hash.Add(PropertyName);

    //         hash.Add(Expression, _queryProvider.GetPreciseExpressionEqualityComparer());

    //         // if (ReferencedQueries is not null)
    //         //     foreach (var cmd in ReferencedQueries)
    //         //         hash.Add(cmd);

    //         return hash.ToHashCode();
    //     }
    // }
    // public override bool Equals(object? obj)
    // {
    //     // if (obj is null) return false;
    //     // return Equals((SelectExpression)obj);
    //     return Equals(obj as SelectExpression);
    // }
    // public bool Equals(SelectExpression? exp)
    // {
    //     if (exp is null) return false;

    //     if (Index != exp.Index) return false;

    //     if (PropertyType != exp.PropertyType) return false;

    //     if (PropertyName != exp.PropertyName) return false;

    //     if (!_queryProvider.GetPreciseExpressionEqualityComparer().Equals(Expression, exp.Expression))
    //         return false;

    //     // if (ReferencedQueries is null && exp.ReferencedQueries is not null) return false;
    //     // if (ReferencedQueries is not null && exp.ReferencedQueries is null) return false;

    //     // if (ReferencedQueries is not null && exp.ReferencedQueries is not null)
    //     // {
    //     //     if (ReferencedQueries.Count != exp.ReferencedQueries.Count) return false;

    //     //     for (int i = 0; i < ReferencedQueries.Count; i++)
    //     //     {
    //     //         if (!ReferencedQueries[i].Equals(exp.ReferencedQueries[i])) return false;
    //     //     }
    //     // }

    //     return true;
    // }
}
