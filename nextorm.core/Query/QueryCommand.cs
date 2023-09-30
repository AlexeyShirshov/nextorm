using System.Collections;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class QueryCommand
{
    private readonly ArrayList _payload = new();
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
    public List<JoinExpression> Joins { get; } = new();
    public virtual void ResetPreparation()
    {
        _isPrepared = false;
        _selectList = null;
        _srcType = null;
        _from = null;

        _dataProvider.ResetPreparation(this);
    }
    #region Payload
    public bool RemovePayload<T>()
        where T : class, IPayload
    {
        foreach (var item in _payload)
        {
            if (item is T)
            {
                _payload.Remove(item);
                return true;
            }
        }
        return false;
    }
    public bool TryGetPayload<T>(out T? payload)
        where T : class, IPayload
    {
        foreach (var item in _payload)
        {
            if (item is T p)
            {
                payload = p;
                return true;
            }
        }
        payload = default;
        return false;
    }
    public bool TryGetNotNullPayload<T>(out T? payload)
        where T : class, IPayload
    {
        foreach (var item in _payload)
        {
            if (item is T p && p is not null)
            {
                payload = p;
                return true;
            }
        }
        payload = default;
        return false;
    }
    public T GetNotNullOrAddPayload<T>(Func<T> factory)
        where T : class, IPayload
    {
        if (!TryGetNotNullPayload<T>(out var payload))
        {
            ArgumentNullException.ThrowIfNull(factory);

            payload = factory();
            _payload.Add(payload);
        }
        return payload!;
    }
    public T? GetOrAddPayload<T>(Func<T?> factory)
        where T : class, IPayload
    {
        if (!TryGetPayload<T>(out var payload))
        {
            payload = factory is null
                ? default
                : factory();
            _payload.Add(payload);
        }
        return payload;
    }
    public T? AddOrUpdatePayload<T>(Func<T?> factory)
        where T : class, IPayload
    {
        for (int i = 0; i < _payload.Count; i++)
        {
            var item = _payload[i];

            if (item is T)
            {
                var p = factory();
                _payload[i] = p;
                return p;
            }
        }

        var payload = factory();
        _payload.Add(payload);
        return payload;
    }
    public void AddOrUpdatePayload<T>(T? payload)
        where T : class, IPayload
    {
        for (int i = 0; i < _payload.Count; i++)
        {
            var item = _payload[i];

            if (item is T)
            {
                _payload[i] = payload;
                return;
            }
        }

        _payload.Add(payload);
    }
    #endregion

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

        FromExpression? from = _from ?? _dataProvider.GetFrom(srcType, this);

        var selectList = _selectList;

        if (selectList is null)
        {
            if (_exp is null)
                throw new PrepareException("Lambda expression for anonymous type must exists");

            selectList = new List<SelectExpression>();

            if (_exp.Body is NewExpression ctor)
            {
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

        _isPrepared = true;
        _selectList = selectList;
        _srcType = srcType;
        _from = from;
    }
}

public class QueryCommand<TResult> : QueryCommand, IAsyncEnumerable<TResult>
{
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

        var factory = GetOrAddPayload(() =>
        {
            var ctorInfo = resultType.GetConstructors().OrderByDescending(it => it.GetParameters().Length).FirstOrDefault() ?? throw new PrepareException($"Cannot get ctor from {resultType}");

            var param = Expression.Parameter(recordType);
            //var @params = SelectList.Select(column => Expression.Parameter(column.PropertyType!)).ToArray();

            if (ctorInfo.GetParameters().Length == SelectList!.Count)
            {
                var newParams = SelectList!.Select(column => _dataProvider.MapColumn(column, param, recordType)).ToArray();

                var ctor = Expression.New(ctorInfo, newParams);

                var lambda = Expression.Lambda(ctor, param);

                if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Init expression for {type}: {exp}", resultType, lambda);

                return new MapPayload(lambda.Compile());
            }
            else
            {
                var bindings = SelectList!.Select(column => 
                {
                    var propInfo = column.PropertyInfo ?? resultType.GetProperty(column.PropertyName!)!;
                    return Expression.Bind(propInfo, _dataProvider.MapColumn(column, param, recordType));
                }).ToArray();

                var ctor = Expression.New(ctorInfo);

                var memberInit = Expression.MemberInit(ctor, bindings);

                var lambda = Expression.Lambda(memberInit, param);

                if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Init expression for {type}: {exp}", resultType, lambda);

                return new MapPayload(lambda.Compile());
            }
        });

        return (TResult)factory!.Delegate.DynamicInvoke(dataRecord)!;
    }
    record MapPayload(Delegate Delegate) : IPayload;

}