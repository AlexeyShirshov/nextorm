using System.Collections;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class QueryCommand : IPayloadManager
{
    private IPayloadManager _payloadMgr;
    protected List<SelectExpression>? _selectList;
    protected FromExpression? _from;
    protected IDataProvider _dataProvider;
    protected readonly LambdaExpression _exp;
    protected readonly Expression? _condition;
    protected bool _isPrepared;
    //private bool _hasCtor;
    protected Type? _srcType;
    public ILogger? Logger { get; set; }
    public QueryCommand(IDataProvider dataProvider, LambdaExpression exp, Expression? condition)
    {
        _dataProvider = dataProvider;
        _exp = exp;
        _condition = condition;
    }
    public FromExpression? From { get => _from; set => _from = value; }
    public List<SelectExpression>? SelectList => _selectList;
    public Type? EntityType => _srcType;
    public IDataProvider DataProvider
    {
        get => _dataProvider;
        set
        {
            ResetPreparation();
            _dataProvider = value;
        }
    }
    public bool IsPrepared => _isPrepared;
    public Expression? Condition => _condition;
    //public bool Cacheable => _cache;
    public virtual void ResetPreparation()
    {
        _isPrepared = false;
        _selectList = null;
        _srcType = null;
        _from = null;

        _dataProvider.ResetPreparation(this);
    }
    public virtual void PrepareCommand(CancellationToken cancellationToken)
    {
        if (_from is not null && _from.Table.IsT1 && !_from.Table.AsT1.IsPrepared)
            _from.Table.AsT1.PrepareCommand(cancellationToken);

        var srcType = _srcType;
        if (srcType is null)
        {
            if (_exp is null)
                throw new PrepareException("Lambda expression for anonymous type must exists");

            srcType = _exp.Parameters[0].Type;
        }

        var selectList = _selectList;
        var cache = true;

        if (selectList is null)
        {
            if (_exp is null)
                throw new PrepareException("Lambda expression for anonymous type must exists");

            selectList = new List<SelectExpression>();

            if (_exp.Body is NewExpression ctor)
            {
                cache = !ctor.Type.IsAnonymous();

                for (var idx = 0; idx < ctor.Arguments.Count; idx++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var arg = ctor.Arguments[idx];
                    var ctorParam = ctor.Constructor!.GetParameters()[idx];

                    selectList.Add(new SelectExpression(ctorParam.ParameterType) { Index = idx, PropertyName = ctorParam.Name!, Expression = arg });
                }

                if (selectList.Count == 0)
                    throw new PrepareException("Select must return new anonymous type with at least one property");

            }
            else if (_dataProvider.NeedMapping)
            {
                var props = srcType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.CanWrite).ToArray();
                for (int idx = 0; idx < props.Length; idx++)
                {
                    var prop = props[idx];
                    if (prop is null) continue;
                    var colAttr = prop.GetCustomAttribute<ColumnAttribute>(true);
                    if (colAttr is not null)
                    {
                        Expression<Func<TableAlias, object>> exp = tbl => tbl.Column(colAttr.Name!);
                        selectList.Add(new SelectExpression(prop.PropertyType) { Index = idx, PropertyName = prop.Name, Expression = exp, PropertyInfo = prop });
                    }
                    else
                    {
                        foreach (var interf in srcType.GetInterfaces())
                        {
                            var intMap = srcType.GetInterfaceMap(interf);

                            var implIdx = Array.IndexOf(intMap.TargetMethods, prop!.GetMethod);
                            if (implIdx >= 0)
                            {
                                var intMethod = intMap.InterfaceMethods[implIdx];

                                var intProp = interf.GetProperties().FirstOrDefault(prop => prop.GetMethod == intMethod);
                                colAttr = intProp?.GetCustomAttribute<ColumnAttribute>(true);
                                if (colAttr is not null)
                                {
                                    Expression<Func<TableAlias, object>> exp = tbl => tbl.Column(colAttr.Name!);
                                    selectList.Add(new SelectExpression(prop!.PropertyType) { Index = idx, PropertyName = prop.Name, Expression = exp, PropertyInfo = prop });
                                }
                            }
                        }
                    }
                }

                if (selectList.Count == 0)
                    throw new PrepareException("Select must return new anonymous type");
            }
        }

        FromExpression? from = _from ?? _dataProvider.GetFrom(srcType);

        _isPrepared = true;
        _selectList = selectList;
        _srcType = srcType;
        _from = from;

        _payloadMgr = new FastPayloadManager(cache ? new Dictionary<Type,object?>() : null);
    }

    public bool RemovePayload<T>() where T : class, IPayload
    {
        return _payloadMgr.RemovePayload<T>();
    }

    public bool TryGetPayload<T>(out T? payload) where T : class, IPayload
    {
        return _payloadMgr.TryGetPayload<T>(out payload);
    }

    public bool TryGetNotNullPayload<T>(out T? payload) where T : class, IPayload
    {
        return _payloadMgr.TryGetNotNullPayload<T>(out payload);
    }

    public T GetNotNullOrAddPayload<T>(Func<T> factory) where T : class, IPayload
    {
        return _payloadMgr.GetNotNullOrAddPayload<T>(factory);
    }

    public T? GetOrAddPayload<T>(Func<T?> factory) where T : class, IPayload
    {
        return _payloadMgr.GetOrAddPayload<T>(factory);
    }

    public T? AddOrUpdatePayload<T>(Func<T?> factory) where T : class, IPayload
    {
        return _payloadMgr.AddOrUpdatePayload<T>(factory);
    }
}

public class QueryCommand<TResult> : QueryCommand, IAsyncEnumerable<TResult>
{
    private Func<object, TResult>? _mapCache;
    //private readonly IPayloadManager _payloadMap = new FastPayloadManager(new Dictionary<Type, object?>());
    public QueryCommand(IDataProvider dataProvider, LambdaExpression exp, Expression? condition) : base(dataProvider, exp, condition)
    {
    }
    public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        return DataProvider.CreateEnumerator(this, cancellationToken);
    }
    // public Expression MapColumn(SelectExpression column, ParameterExpression param, Type _)
    // {
    //     return Expression.PropertyOrField(param, column.PropertyName!);
    // }
    public TResult Map(object dataRecord)
    {
        if (!IsPrepared)
            throw new InvalidOperationException("Command not prepared");

        var resultType = typeof(TResult);

        var recordType = dataRecord.GetType();

        if (resultType == recordType)
            return (TResult)dataRecord;

        // var factory = () =>
        // {
        //     var ctorInfo = resultType.GetConstructors().OrderByDescending(it => it.GetParameters().Length).FirstOrDefault() ?? throw new PrepareException($"Cannot get ctor from {resultType}");

        //     var param = Expression.Parameter(typeof(object));
        //     //var @params = SelectList.Select(column => Expression.Parameter(column.PropertyType!)).ToArray();

        //     if (ctorInfo.GetParameters().Length == SelectList!.Count)
        //     {
        //         var newParams = SelectList!.Select(column => _dataProvider.MapColumn(column, Expression.Convert(param,recordType), recordType)).ToArray();

        //         var ctor = Expression.New(ctorInfo, newParams);

        //         var lambda = Expression.Lambda<Func<object,TResult>>(ctor, param);

        //         if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Init expression for {type}: {exp}", resultType, lambda);

        //         return lambda.Compile();
        //     }
        //     else
        //     {
        //         var bindings = SelectList!.Select(column =>
        //         {
        //             var propInfo = column.PropertyInfo ?? resultType.GetProperty(column.PropertyName!)!;
        //             return Expression.Bind(propInfo, _dataProvider.MapColumn(column, Expression.Convert(param,recordType), recordType));
        //         }).ToArray();

        //         var ctor = Expression.New(ctorInfo);

        //         var memberInit = Expression.MemberInit(ctor, bindings);

        //         var lambda = Expression.Lambda<Func<object,TResult>>(memberInit, param);

        //         if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Init expression for {type}: {exp}", resultType, lambda);

        //         return lambda.Compile();
        //     }
        // };

        if (_mapCache is null)
        {
            _mapCache=(object _)=>Activator.CreateInstance<TResult>();
            //_mapCache=factory();
        }

        return _mapCache(dataRecord);
    }
    //record MapPayload(Delegate Delegate) : IPayload;

}