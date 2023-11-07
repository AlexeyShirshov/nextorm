using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class QueryCommand : IPayloadManager, ISourceProvider
{
    private IPayloadManager _payloadMgr;
    protected List<SelectExpression>? _selectList;
    private int _columnsHash;
    private int _joinHash;
    protected FromExpression? _from;
    protected IDataProvider _dataProvider;
    protected readonly LambdaExpression _exp;
    protected readonly Expression? _condition;
    protected bool _isPrepared;
    private int? _hash;
    internal int? _hashPlan;
    protected Type? _srcType;
    private bool _dontCache;
    internal int? PlanHash;
    internal int ColumnsPlanHash;
    internal int JoinPlanHash;
    public QueryCommand(IDataProvider dataProvider, LambdaExpression exp, Expression? condition)
    {
        _dataProvider = dataProvider;
        _exp = exp;
        _condition = condition;
        _payloadMgr = new FastPayloadManager(new Dictionary<Type, object?>());
    }
    public ILogger? Logger { get; set; }
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
    public bool Cache
    {
        get => !_dontCache;
        set => _dontCache = !value;
    }
    internal QueryCommand? FromQuery => From?.Table.AsT1;
    public virtual void ResetPreparation()
    {
        _isPrepared = false;
        _selectList = null;
        _srcType = null;
        _from = null;
        _hash = null;

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

        FromExpression? from = _from ?? _dataProvider.GetFrom(srcType, this);

        var joinHash = 7;
        var joinPlanHash = 7;
        if (Joins.Any())
        {
            if (_from is not null && string.IsNullOrEmpty(_from.TableAlias)) _from.TableAlias = "t1";

            foreach (var join in Joins)
            {
                if (!(join.Query?.IsPrepared ?? true))
                    join.Query!.PrepareCommand(cancellationToken);

                if (!_dontCache) unchecked
                    {
                        joinHash = joinHash * 13 + join.GetHashCode();
                        joinPlanHash = joinPlanHash * 13 + JoinExpressionPlanEqualityComparer.Instance.GetHashCode(join);
                    }
            }
        }


        var selectList = _selectList;
        var columnsHash = 7;
        var columnsPlanHash = 7;

        if (selectList is null)
        {
            if (_exp is null)
                throw new PrepareException("Lambda expression for anonymous type must exists");

            selectList = new List<SelectExpression>();

            if (_exp.Body is NewExpression ctor)
            {
                //var isTuple = ctor.Type.IsTuple();

                for (var idx = 0; idx < ctor.Arguments.Count; idx++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    SelectExpression selExp;
                    var arg = ctor.Arguments[idx];
                    // if (arg is MemberExpression me)
                    // {
                    //     selExp = new SelectExpression(me.Type)
                    //     {
                    //         Index = idx,
                    //         PropertyName = me.Member.Name!,
                    //         Expression = arg
                    //     };
                    // }
                    // else
                    // {
                    var ctorParam = ctor.Constructor!.GetParameters()[idx];

                    selExp = new SelectExpression(ctorParam.ParameterType)
                    {
                        Index = idx,
                        PropertyName = ctorParam.Name!,
                        Expression = arg
                    };
                    //}
                    selectList.Add(selExp);

                    if (!_dontCache) unchecked
                        {
                            columnsHash = columnsHash * 13 + selExp.GetHashCode();
                            columnsPlanHash = columnsPlanHash * 13 + SelectExpressionPlanEqualityComparer.Instance.GetHashCode(selExp);
                        }
                }

                if (selectList.Count == 0)
                    throw new PrepareException("Select must return new anonymous type with at least one property");

            }
            else if (_dataProvider.NeedMapping)
            {
                var props = srcType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.CanWrite).ToArray();
                for (int idx = 0; idx < props.Length; idx++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var prop = props[idx];
                    if (prop is null) continue;
                    var colAttr = prop.GetCustomAttribute<ColumnAttribute>(true);
                    if (colAttr is not null)
                    {
                        Expression<Func<TableAlias, object>> exp = tbl => tbl.Column(colAttr.Name!);

                        var selExp = new SelectExpression(prop.PropertyType)
                        {
                            Index = idx,
                            PropertyName = prop.Name,
                            Expression = exp,
                            PropertyInfo = prop
                        };

                        selectList.Add(selExp);

                        if (!_dontCache) unchecked
                            {
                                columnsHash = columnsHash * 13 + selExp.GetHashCode();
                                columnsPlanHash = columnsPlanHash * 13 + SelectExpressionPlanEqualityComparer.Instance.GetHashCode(selExp);
                            }
                    }
                    else
                    {
                        foreach (var interf in srcType.GetInterfaces())
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;

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

                                    var selExp = new SelectExpression(prop!.PropertyType)
                                    {
                                        Index = idx,
                                        PropertyName = prop.Name,
                                        Expression = exp,
                                        PropertyInfo = prop
                                    };

                                    selectList.Add(selExp);

                                    if (!_dontCache) unchecked
                                        {
                                            columnsHash = columnsHash * 13 + selExp.GetHashCode();
                                            columnsPlanHash = columnsPlanHash * 13 + SelectExpressionPlanEqualityComparer.Instance.GetHashCode(selExp);
                                        }
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
        _columnsHash = columnsHash;
        ColumnsPlanHash = columnsPlanHash;
        _srcType = srcType;
        _from = from;
        _joinHash = joinHash;
        JoinPlanHash = joinPlanHash;

        //_payloadMgr = new FastPayloadManager(cache ? new Dictionary<Type, object?>() : null);

        // string capitalize(string str)
        // {
        //     return string.Concat(new ReadOnlySpan<char>(Char.ToUpper(str[0])), str.AsSpan()[1..]);
        // }
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

    public T? AddOrUpdatePayload<T>(Func<T?> factory, Func<T?, T?>? update = null) where T : class, IPayload
    {
        return _payloadMgr.AddOrUpdatePayload<T>(factory, update);
    }
    public void AddOrUpdatePayload<T>(T? payload) where T : class, IPayload
    {
        _payloadMgr.AddOrUpdatePayload<T>(payload);
    }
    public override int GetHashCode()
    {
        if (_hash.HasValue)
            return _hash.Value;

        unchecked
        {
            HashCode hash = new();
            if (_from is not null)
                hash.Add(_from);

            if (_srcType is not null)
                hash.Add(_srcType);

            if (_condition is not null)
                hash.Add(_condition, PreciseExpressionEqualityComparer.Instance);

            hash.Add(_columnsHash);

            hash.Add(_joinHash);

            _hash = hash.ToHashCode();

            //Console.WriteLine(_hash);

            return _hash.Value;
        }
    }
    public override bool Equals(object? obj)
    {
        return Equals(obj as QueryCommand);
    }
    public bool Equals(QueryCommand? cmd)
    {
        if (cmd is null) return false;

        if (!Equals(_from, cmd._from)) return false;

        if (_srcType != cmd._srcType) return false;

        if (!PreciseExpressionEqualityComparer.Instance.Equals(_condition, cmd._condition)) return false;

        if (_selectList is null && cmd._selectList is not null) return false;
        if (_selectList is not null && cmd._selectList is null) return false;

        if (_selectList is not null && cmd._selectList is not null)
        {
            if (_selectList.Count != cmd._selectList.Count) return false;

            for (int i = 0; i < _selectList.Count; i++)
            {
                if (!_selectList[i].Equals(cmd._selectList[i])) return false;
            }
        }

        if (Joins is null && cmd.Joins is not null) return false;
        if (Joins is not null && cmd.Joins is null) return false;

        if (Joins is not null && cmd.Joins is not null)
        {
            if (Joins.Count != cmd.Joins.Count) return false;

            for (int i = 0; i < Joins.Count; i++)
            {
                if (!Joins[i].Equals(cmd.Joins[i])) return false;
            }
        }

        return true;
    }
    public QueryCommand? FindSourceFromAlias(string? aliasRaw)
    {
        if (string.IsNullOrEmpty(aliasRaw))
            return _from!.Table.AsT1;

        var idx = int.Parse(aliasRaw[1..]);
        return Joins[idx - 2].Query;
    }
}

public class QueryCommand<TResult> : QueryCommand, IAsyncEnumerable<TResult>
{
    //private readonly IPayloadManager _payloadMap = new FastPayloadManager(new Dictionary<Type, object?>());
    public QueryCommand(IDataProvider dataProvider, LambdaExpression exp, Expression? condition) : base(dataProvider, exp, condition)
    {
    }

    internal CompiledQuery<TResult>? Compiled => CacheEntry?.CompiledQuery as CompiledQuery<TResult>;
    internal CacheEntry? CacheEntry { get; set; }
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
    public Func<Func<object, TResult>> GetMap(Type resultType, Type recordType)
    {
        if (!IsPrepared)
            throw new InvalidOperationException("Command not prepared");

        return () =>
        {
            var ctorInfo = resultType.GetConstructors().OrderByDescending(it => it.GetParameters().Length).FirstOrDefault() ?? throw new PrepareException($"Cannot get ctor from {resultType}");

            var param = Expression.Parameter(typeof(object));
            //var @params = SelectList.Select(column => Expression.Parameter(column.PropertyType!)).ToArray();

            if (ctorInfo.GetParameters().Length == SelectList!.Count)
            {
                var newParams = SelectList!.Select(column => _dataProvider.MapColumn(column, Expression.Convert(param, recordType), recordType)).ToArray();

                var ctor = Expression.New(ctorInfo, newParams);

                var lambda = Expression.Lambda<Func<object, TResult>>(ctor, param);

                if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Init expression for {type}: {exp}", resultType, lambda);

                return lambda.Compile();
            }
            else
            {
                var bindings = SelectList!.Select(column =>
                {
                    var propInfo = column.PropertyInfo ?? resultType.GetProperty(column.PropertyName!)!;
                    return Expression.Bind(propInfo, _dataProvider.MapColumn(column, Expression.Convert(param, recordType), recordType));
                }).ToArray();

                var ctor = Expression.New(ctorInfo);

                var memberInit = Expression.MemberInit(ctor, bindings);

                var lambda = Expression.Lambda<Func<object, TResult>>(memberInit, param);

                if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Init expression for {type}: {exp}", resultType, lambda);

                return lambda.Compile();
            }
        };
    }
    //record MapPayload(Delegate Delegate) : IPayload;
    // public IEnumerable<TResult> Fetch(CancellationToken cancellationToken)
    // {
    //     return Fetch(100, cancellationToken);
    // }
    // public IEnumerable<TResult> Fetch(int capacity, CancellationToken cancellationToken)
    // {
    //     var bus = new BlockingCollection<TResult>(capacity);

    //     Task.Run(async () =>
    //     {
    //         await foreach (var item in this.WithCancellation(cancellationToken))
    //         {
    //             bus.Add(item);
    //         }
    //         bus.CompleteAdding();
    //     }, cancellationToken);

    //     while (!bus.IsCompleted)
    //     {
    //         TResult? r = default;
    //         try { r = bus.Take(cancellationToken); } catch (InvalidOperationException) { continue; }
    //         yield return r;
    //     }
    // }
    // public IAsyncEnumerable<TResult> FetchAsync(CancellationToken cancellationToken)
    // {
    //     return FetchAsync(10, cancellationToken);
    // }
    public IAsyncEnumerable<TResult> Fetch(CancellationToken cancellationToken)
    {
        var bus = Channel.CreateUnbounded<TResult>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true
        });

        var _ = Task.Run(async () =>
        {
            await foreach (var item in this.WithCancellation(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await bus.Writer.WriteAsync(item, cancellationToken);
            }

            bus.Writer.Complete();
        }, cancellationToken);

        return bus.Reader.ReadAllAsync(cancellationToken);
        // while (await bus.Reader.WaitToReadAsync(cancellationToken))
        //     yield return await bus.Reader.ReadAsync(cancellationToken);
    }
}