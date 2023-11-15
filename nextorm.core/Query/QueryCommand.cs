//#define PLAN_CACHE
#define INITALGO_1

using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace nextorm.core;

public class QueryCommand : IPayloadManager, ISourceProvider//, IParamProvider
{
    protected readonly IPayloadManager _payloadMgr;
    private readonly List<JoinExpression> _joins;
    protected List<SelectExpression>? _selectList;
    private int _columnsHash;
    private int _joinHash;
    protected FromExpression? _from;
    protected IDataProvider _dataProvider;
    protected readonly LambdaExpression _exp;
    protected readonly Expression? _condition;
    protected bool _isPrepared;
    protected int? _hash;
    // #if PLAN_CACHE
    //     internal int? _hashPlan;
    // #endif
    protected Type? _srcType;
    private bool _dontCache;
#if DEBUG
    private int? _conditionHash;
    public int? ConditionHash => _conditionHash;
#endif
#if PLAN_CACHE
    //    internal int? PlanHash;
    internal int ColumnsPlanHash;
    internal int JoinPlanHash;
#endif
    //protected ArrayList _params = new();
    public QueryCommand(IDataProvider dataProvider, LambdaExpression exp, Expression? condition)
        : this(dataProvider, exp, condition, new FastPayloadManager(new Dictionary<Type, object?>()), new())
    {
    }
    protected QueryCommand(IDataProvider dataProvider, LambdaExpression exp, Expression? condition, IPayloadManager payloadMgr, List<JoinExpression> joins)
    {
        _dataProvider = dataProvider;
        _exp = exp;
        _condition = condition;
        _payloadMgr = payloadMgr;
        _joins = joins;
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
    public List<JoinExpression> Joins => _joins;
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
#if PLAN_CACHE
        var joinPlanHash = 7;
#endif
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
#if PLAN_CACHE
                        joinPlanHash = joinPlanHash * 13 + JoinExpressionPlanEqualityComparer.Instance.GetHashCode(join);
#endif
                    }
            }
        }


        var selectList = _selectList;
        var columnsHash = 7;
#if PLAN_CACHE
        var columnsPlanHash = 7;
#endif
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
#if PLAN_CACHE
                            columnsPlanHash = columnsPlanHash * 13 + SelectExpressionPlanEqualityComparer.Instance.GetHashCode(selExp);
#endif
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
#if PLAN_CACHE
                                columnsPlanHash = columnsPlanHash * 13 + SelectExpressionPlanEqualityComparer.Instance.GetHashCode(selExp);
#endif
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
#if PLAN_CACHE
                                            columnsPlanHash = columnsPlanHash * 13 + SelectExpressionPlanEqualityComparer.Instance.GetHashCode(selExp);
#endif
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
        _srcType = srcType;
        _from = from;
        _joinHash = joinHash;

#if PLAN_CACHE
        ColumnsPlanHash = columnsPlanHash;
        JoinPlanHash = joinPlanHash;
#endif
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
            {
                var comp = new PreciseExpressionEqualityComparer((_dataProvider as SqlDataProvider)?.ExpressionsCache, (_dataProvider as SqlDataProvider)?.Logger);
                var condHash = comp.GetHashCode(_condition);
                hash.Add(condHash);
#if DEBUG
                _conditionHash = condHash;
#endif
            }

            // foreach (var param in _params)
            // {
            //     hash.Add(param);
            // }

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

        if (!new PreciseExpressionEqualityComparer((_dataProvider as SqlDataProvider)?.ExpressionsCache, (_dataProvider as SqlDataProvider)?.Logger).Equals(_condition, cmd._condition)) return false;

        // if (_params is null && cmd._params is not null) return false;
        // if (_params is not null && cmd._params is null) return false;

        // if (_params is not null && cmd._params is not null)
        // {
        //     if (_params.Count != cmd._params.Count) return false;

        //     for (int i = 0; i < _params.Count; i++)
        //     {
        //         if (!Equals(_params[i], cmd._params[i])) return false;
        //     }
        // }

        if (!Equals(_from, cmd._from)) return false;

        if (_srcType != cmd._srcType) return false;

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
        {
            if (_from?.Table.IsT1 ?? false)
                return _from!.Table.AsT1;

            return null;
        }

        var idx = int.Parse(aliasRaw[1..]);
        return Joins[idx - 2].Query;
    }

    //public object? GetParam(int paramIdx) => _params.Count > paramIdx ? _params[paramIdx] : null;
    // protected virtual void CopyTo(QueryCommand dst)
    // {
    //     dst._selectList = _selectList;
    //     dst._columnsHash = _columnsHash;
    //     dst._joinHash = _joinHash;
    //     dst._from = _from;
    //     dst._isPrepared = _isPrepared;
    //     dst._srcType = _srcType;
    //     dst._dontCache = _dontCache;
    //     dst.Logger = Logger;
    // }
}

public class QueryCommand<TResult> : QueryCommand, IAsyncEnumerable<TResult>
{
    //private readonly IPayloadManager _payloadMap = new FastPayloadManager(new Dictionary<Type, object?>());
    public QueryCommand(IDataProvider dataProvider, LambdaExpression exp, Expression? condition) : base(dataProvider, exp, condition)
    {
    }
    protected QueryCommand(IDataProvider dataProvider, LambdaExpression exp, Expression? condition, IPayloadManager payloadMgr, List<JoinExpression> joins)
    : base(dataProvider, exp, condition, payloadMgr, joins)
    {

    }
    //internal CompiledQuery<TResult>? Compiled => CacheEntry?.CompiledQuery as CompiledQuery<TResult>;
    public CacheEntry? CacheEntry { get; set; }
    public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        return DataProvider.CreateAsyncEnumerator(this, null, cancellationToken);
    }
    // public Expression MapColumn(SelectExpression column, ParameterExpression param, Type _)
    // {
    //     return Expression.PropertyOrField(param, column.PropertyName!);
    // }
    // #if INITALGO_1
    //     public Func<Func<object, TResult>> GetMap(Type resultType, Type recordType)
    // #else
    //     public Func<Func<object, object[]?, TResult>> GetMap(Type resultType, Type recordType)
    // #endif
    //     {
    //         if (!IsPrepared)
    //             throw new InvalidOperationException("Command not prepared");

    //         // var key = new ExpressionKey(_exp);
    //         // if (!(_dataProvider as SqlDataProvider).MapCache.TryGetValue(key, out var del))
    //         // {
    //         //     if (Logger?.IsEnabled(LogLevel.Information) ?? false) Logger.LogInformation("Map delegate cache miss for: {exp}", _exp);

    //         var del = () =>
    //         {
    //             var ctorInfo = resultType.GetConstructors().OrderByDescending(it => it.GetParameters().Length).FirstOrDefault() ?? throw new PrepareException($"Cannot get ctor from {resultType}");

    //             var param = Expression.Parameter(typeof(object));
    // #if !INITALGO_1
    //             var valuesParam = Expression.Parameter(typeof(object[]));
    //             var getValuesMethod = recordType.GetMethod(nameof(IDataRecord.GetValues))!;
    //             var assignValuesVariable = Expression.Call(Expression.Convert(param, recordType), getValuesMethod, valuesParam);
    // #endif
    //             //return Expression.Lambda<Func<object, TResult>>(Expression.New(ctorInfo), param).Compile();
    //             //var @params = SelectList.Select(column => Expression.Parameter(column.PropertyType!)).ToArray();

    //             if (ctorInfo.GetParameters().Length == SelectList!.Count)
    //             {
    // #if INITALGO_1
    //                 var newParams = SelectList!.Select(column => _dataProvider.MapColumn(column, Expression.Convert(param, recordType), recordType)).ToArray();
    // #else
    //                 var newParams = SelectList!.Select(column => Expression.Convert(Expression.ArrayIndex(valuesParam, Expression.Constant(column.Index)), column.PropertyType)).ToArray();
    // #endif
    //                 var ctor = Expression.New(ctorInfo, newParams);

    // #if INITALGO_1
    //                 var lambda = Expression.Lambda<Func<object, TResult>>(ctor, param);
    //                 if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Get instance of {type} as: {exp}", resultType, lambda);
    // #else
    //                 var body = Expression.Block(typeof(TResult), new Expression[] { assignValuesVariable, ctor });
    //                 var lambda = Expression.Lambda<Func<object, object[]?, TResult>>(body, param, valuesParam);
    //                 if (Logger?.IsEnabled(LogLevel.Debug) ?? false)
    //                 {
    //                     var sb = new StringBuilder();
    //                     sb.AppendLine();
    //                     sb.Append(assignValuesVariable.ToString()).AppendLine(";");
    //                     sb.Append(ctor.ToString()).AppendLine(";");
    //                     var dumpExp = lambda.ToString().Replace("...", sb.ToString());
    //                     Logger.LogDebug("Get instance of {type} as: {exp}", resultType, dumpExp);
    //                 }
    // #endif
    //                 return lambda.Compile();
    //             }
    //             else
    //             {
    // #if INITALGO_1
    //                 var bindings = SelectList!.Select(column =>
    //                 {
    //                     var propInfo = column.PropertyInfo ?? resultType.GetProperty(column.PropertyName!)!;
    //                     return Expression.Bind(propInfo, _dataProvider.MapColumn(column, Expression.Convert(param, recordType), recordType));
    //                 }).ToArray();

    //                 var ctor = Expression.New(ctorInfo);

    //                 var memberInit = Expression.MemberInit(ctor, bindings);

    //                 var body = memberInit;
    //                 var lambda = Expression.Lambda<Func<object, TResult>>(body, param);

    //                 if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Get instance of {type} as: {exp}", resultType, lambda);
    // #else
    //                 //var fieldCountProp = recordType.GetProperty(nameof(IDataRecord.FieldCount))!;

    //                 //var valuesVariable = Expression.Variable(typeof(object[]));
    //                 //var initValuesVariable = Expression.Assign(valuesVariable, Expression.NewArrayBounds(typeof(object), Expression.Property(Expression.Convert(param, recordType), fieldCountProp)));

    //                 var bindings = SelectList!.Select(column =>
    //                 {
    //                     var propInfo = column.PropertyInfo ?? resultType.GetProperty(column.PropertyName!)!;
    //                     return Expression.Bind(propInfo, Expression.Convert(Expression.ArrayIndex(valuesParam, Expression.Constant(column.Index)), column.PropertyType));
    //                     //return Expression.Bind(propInfo, _dataProvider.MapColumn(column, Expression.Convert(param, recordType), recordType));
    //                 }).ToArray();

    //                 var ctor = Expression.New(ctorInfo);

    //                 var memberInit = Expression.MemberInit(ctor, bindings);

    //                 var body = Expression.Block(typeof(TResult), new Expression[] { assignValuesVariable, memberInit });
    //                 var lambda = Expression.Lambda<Func<object, object[]?, TResult>>(body, param, valuesParam);

    //                 if (Logger?.IsEnabled(LogLevel.Debug) ?? false)
    //                 {
    //                     var sb = new StringBuilder();
    //                     sb.AppendLine();
    //                     //sb.Append(initValuesVariable.ToString()).AppendLine(";");
    //                     sb.Append(assignValuesVariable.ToString()).AppendLine(";");
    //                     sb.Append(memberInit.ToString()).AppendLine(";");
    //                     var dumpExp = lambda.ToString().Replace("...", sb.ToString());
    //                     Logger.LogDebug("Get instance of {type} as: {exp}", resultType, dumpExp);
    //                 }
    // #endif
    //                 return lambda.Compile();
    //             }
    //         };

    //         //         (_dataProvider as SqlDataProvider).MapCache[key] = del;
    //         //     }

    // #if INITALGO_1
    //         return (Func<Func<object, TResult>>)del;
    // #else
    //         return (Func<Func<object, object[]?, TResult>>)del;
    // #endif
    //     }
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
    public IAsyncEnumerable<TResult> Pipeline(params object[] @params) => Pipeline(CancellationToken.None, @params);
    public async IAsyncEnumerable<TResult> Pipeline(CancellationToken cancellationToken, params object[] @params)
    {
        var bus = Channel.CreateUnbounded<TResult>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true
        });

        var t = Task.Run(async () =>
        {
            try
            {
                foreach (var item in await Exec(cancellationToken, @params))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await bus.Writer.WriteAsync(item, cancellationToken);
                }
            }
            finally
            {
                bus.Writer.Complete();
            }
        }, cancellationToken);

        // return bus.Reader.ReadAllAsync(cancellationToken);
        while (await bus.Reader.WaitToReadAsync(cancellationToken))
            yield return await bus.Reader.ReadAsync(cancellationToken);

        await t;
    }
    public IAsyncEnumerable<TResult> ExecAsync(params object[] @params) => ExecAsync(CancellationToken.None, @params);
    public async IAsyncEnumerable<TResult> ExecAsync([EnumeratorCancellation] CancellationToken cancellationToken, params object[] @params)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        await using var ee = DataProvider.CreateAsyncEnumerator(this, @params, cancellationToken);
        while (await ee.MoveNextAsync())
            yield return ee.Current;
    }
    public Task<IEnumerable<TResult>> Exec(params object[] @params) => Exec(CancellationToken.None, @params);
    public async Task<IEnumerable<TResult>> Exec(CancellationToken cancellationToken, params object[] @params)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        // return Array.Empty<TResult>();
        var enumerator = await DataProvider.CreateEnumerator(this, @params, cancellationToken);

        if (enumerator is IEnumerable<TResult> ee)
            return ee;

        return new InternalEnumerable(enumerator);
    }
    public Task<IEnumerable<TResult>> ToListAsync(params object[] @params) => ToListAsync(CancellationToken.None, @params);
    public async Task<IEnumerable<TResult>> ToListAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        return await DataProvider.ToListAsync(this, @params, cancellationToken);
    }
    public QueryCommand<TResult> Compile(bool forToListCalls, CancellationToken cancellationToken = default)
    {
        if (!IsPrepared)
            PrepareCommand(cancellationToken);

        _dataProvider.Compile(this, forToListCalls, cancellationToken);

        return this;
    }
    // public QueryCommand<TResult> WithParams(params object[] @params)
    // {
    //     QueryCommand<TResult> cmd;

    //     if (CacheEntry?.CompiledQuery is IReplaceParam rp)
    //     {
    //         rp.ReplaceParams(@params, _dataProvider);
    //         cmd = this;
    //     }
    //     else
    //     {
    //         cmd = new QueryCommand<TResult>(_dataProvider, _exp, _condition, _payloadMgr, Joins);
    //         CopyTo(cmd);
    //         cmd._params.AddRange(@params);
    //     }

    //     return cmd;
    // }
    // protected override void CopyTo(QueryCommand dst)
    // {
    //     CopyTo(dst as QueryCommand<TResult>);
    // }
    // protected void CopyTo(QueryCommand<TResult>? dst)
    // {
    //     if (dst is not null)
    //     {
    //         dst.CacheEntry = CacheEntry;
    //     }
    // }
    class InternalEnumerable : IEnumerable<TResult>
    {
        private readonly IEnumerator<TResult> _enumerator;
        public InternalEnumerable(IEnumerator<TResult> enumerator)
        {
            _enumerator = enumerator;
        }
        IEnumerator<TResult> IEnumerable<TResult>.GetEnumerator() => _enumerator;
        IEnumerator IEnumerable.GetEnumerator() => _enumerator;
    }
}