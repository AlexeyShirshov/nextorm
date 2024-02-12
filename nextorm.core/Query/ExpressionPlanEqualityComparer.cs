using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public class ExpressionPlanEqualityComparerDELETE : IEqualityComparer<Expression?>
{
    private readonly static ConcurrentDictionary<QueryCommandKey, Func<object?, QueryCommand>> _cmdCache = new();
    //private readonly IDictionary<ExpressionKey, Delegate> _cache;
    private readonly IQueryProvider _queryProvider;
    private readonly ILogger? _logger;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public ExpressionPlanEqualityComparerDELETE(IQueryProvider queryProvider)
        : this(queryProvider, null)
    {
    }
    public ExpressionPlanEqualityComparerDELETE(IQueryProvider queryProvider, ILogger? logger)
    {
        //_cache = cache ?? new ExpressionCache<Delegate>();
        _queryProvider = queryProvider;
        _logger = logger;
    }
    /// <inheritdoc />
    public int GetHashCode(Expression obj)
    {
        if (obj == null)
        {
            return 0;
        }

        unchecked
        {
            var hash = new HashCode();
            hash.Add(obj.NodeType);
            hash.Add(obj.Type);

            switch (obj)
            {
                case BinaryExpression binaryExpression:
                    hash.Add(binaryExpression.Left, this);
                    hash.Add(binaryExpression.Right, this);
                    AddExpressionToHashIfNotNull(binaryExpression.Conversion);
                    AddToHashIfNotNull(binaryExpression.Method);

                    break;

                case BlockExpression blockExpression:
                    AddListToHash(blockExpression.Variables);
                    AddListToHash(blockExpression.Expressions);
                    break;

                case ConditionalExpression conditionalExpression:
                    hash.Add(conditionalExpression.Test, this);
                    hash.Add(conditionalExpression.IfTrue, this);
                    hash.Add(conditionalExpression.IfFalse, this);
                    break;

                case ConstantExpression constantExpression:
                    //hash.Add(constantExpression.Type);
                    switch (constantExpression.Value)
                    {
                        case IQueryable:
                        case null:
                            break;

                        case IStructuralEquatable structuralEquatable:
                            hash.Add(structuralEquatable.GetHashCode(StructuralComparisons.StructuralEqualityComparer));
                            break;

                        default:
                            hash.Add(constantExpression.Value);
                            break;
                    }

                    break;

                case DefaultExpression:
                    // Intentionally empty. No additional members
                    break;

                case GotoExpression gotoExpression:
                    hash.Add(gotoExpression.Value, this);
                    hash.Add(gotoExpression.Kind);
                    hash.Add(gotoExpression.Target);
                    break;

                case IndexExpression indexExpression:
                    if (indexExpression.Type.IsAssignableTo(typeof(QueryCommand)) && indexExpression.Arguments is [ConstantExpression cex] && cex.Value is int idx)
                    {
                        var cmd = _queryProvider.ReferencedQueries[idx];
                        hash.Add(cmd, _queryProvider.GetQueryPlanEqualityComparer());
                    }
                    else
                    {
                        hash.Add(indexExpression.Object, this);
                        AddListToHash(indexExpression.Arguments);
                        hash.Add(indexExpression.Indexer);
                    }

                    break;

                case InvocationExpression invocationExpression:
                    hash.Add(invocationExpression.Expression, this);
                    AddListToHash(invocationExpression.Arguments);
                    break;

                case LabelExpression labelExpression:
                    AddExpressionToHashIfNotNull(labelExpression.DefaultValue);
                    hash.Add(labelExpression.Target);
                    break;

                case LambdaExpression lambdaExpression:
                    hash.Add(lambdaExpression.Body, this);
                    AddListToHash(lambdaExpression.Parameters);
                    hash.Add(lambdaExpression.ReturnType);
                    break;

                case ListInitExpression listInitExpression:
                    hash.Add(listInitExpression.NewExpression, this);
                    AddInitializersToHash(listInitExpression.Initializers);
                    break;

                case LoopExpression loopExpression:
                    hash.Add(loopExpression.Body, this);
                    AddToHashIfNotNull(loopExpression.BreakLabel);
                    AddToHashIfNotNull(loopExpression.ContinueLabel);
                    break;

                case MemberExpression memberExpression:
                    if (memberExpression.Expression is ConstantExpression ce)
                    {
                        if (typeof(QueryCommand).IsAssignableFrom(memberExpression.Type))
                        {
                            var key = new QueryCommandKey(ce.Type, memberExpression.Member.Name);
                            if (!_cmdCache.TryGetValue(key, out var del))
                            {
                                var p = Expression.Parameter(typeof(object));
                                var replace = new ReplaceConstantVisitor(Expression.Convert(p, ce!.Type));
                                var body = replace.Visit(memberExpression);
                                del = Expression.Lambda<Func<object?, QueryCommand>>(body, p).Compile();
                                //value = 1;
                                _cmdCache[key] = del;

                                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                                {
                                    _logger.LogTrace("Expression cache miss on gethashcode. hashcode: {hash}, value: {value}", key.GetHashCode(), del(ce.Value));
                                }
                                else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Expression cache miss on gethashcode");
                            }

                            var cmd = del(ce!.Value);

                            hash.Add(cmd, _queryProvider.GetQueryPlanEqualityComparer());

                            // var cmd = Expression.Lambda<Func<QueryCommand>>(memberExpression).Compile()();
                            // hash.Add(cmd, new QueryPlanEqualityComparer(_cache));
                        }
                        else
                        {
                            hash.Add(ce.Type);
                            hash.Add(memberExpression.Type);
                            hash.Add(memberExpression.Member.Name);
                        }
                    }
                    else
                    {
                        hash.Add(memberExpression.Expression, this);
                        hash.Add(memberExpression.Member);
                    }

                    break;

                case MemberInitExpression memberInitExpression:
                    hash.Add(memberInitExpression.NewExpression, this);
                    AddMemberBindingsToHash(memberInitExpression.Bindings);
                    break;

                case MethodCallExpression methodCallExpression:
                    hash.Add(methodCallExpression.Object, this);
                    AddListToHash(methodCallExpression.Arguments);
                    hash.Add(methodCallExpression.Method);
                    break;

                case NewArrayExpression newArrayExpression:
                    AddListToHash(newArrayExpression.Expressions);
                    break;

                case NewExpression newExpression:
                    AddListToHash(newExpression.Arguments);
                    hash.Add(newExpression.Constructor);

                    var members = newExpression.Members;
                    if (members is not null)
                    {
                        for (var (i, cnt) = (0, members.Count); i < cnt; i++)
                        {
                            hash.Add(members[i]);
                        }
                    }

                    break;

                case ParameterExpression parameterExpression:
                    //AddToHashIfNotNull(parameterExpression.Name);
                    hash.Add(parameterExpression.Type);
                    break;

                case RuntimeVariablesExpression runtimeVariablesExpression:
                    AddListToHash(runtimeVariablesExpression.Variables);
                    break;

                case SwitchExpression switchExpression:
                    hash.Add(switchExpression.SwitchValue, this);
                    AddExpressionToHashIfNotNull(switchExpression.DefaultBody);
                    AddToHashIfNotNull(switchExpression.Comparison);
                    var cases = switchExpression.Cases;
                    for (var (i, cnt) = (0, cases.Count); i < cnt; i++)
                    {
                        var @case = cases[i];
                        hash.Add(@case.Body, this);
                        AddListToHash(@case.TestValues);
                    }

                    break;

                case TryExpression tryExpression:
                    hash.Add(tryExpression.Body, this);
                    AddExpressionToHashIfNotNull(tryExpression.Fault);
                    AddExpressionToHashIfNotNull(tryExpression.Finally);
                    var handlers = tryExpression.Handlers;
                    if (handlers is not null)
                    {
                        for (var (i, cnt) = (0, handlers.Count); i < cnt; i++)
                        {
                            var handler = handlers[i];
                            hash.Add(handler.Body, this);
                            AddExpressionToHashIfNotNull(handler.Variable);
                            AddExpressionToHashIfNotNull(handler.Filter);
                            hash.Add(handler.Test);
                        }
                    }

                    break;

                case TypeBinaryExpression typeBinaryExpression:
                    hash.Add(typeBinaryExpression.Expression, this);
                    hash.Add(typeBinaryExpression.TypeOperand);
                    break;

                case UnaryExpression unaryExpression:
                    hash.Add(unaryExpression.Operand, this);
                    AddToHashIfNotNull(unaryExpression.Method);
                    break;

                default:
                    if (obj.NodeType == ExpressionType.Extension)
                    {
                        hash.Add(obj);
                        break;
                    }

                    throw new NotSupportedException(obj.NodeType.ToString());
            }

            return hash.ToHashCode();

            void AddToHashIfNotNull(object? t)
            {
                if (t is not null)
                {
                    hash.Add(t);
                }
            }

            void AddExpressionToHashIfNotNull(Expression? t)
            {
                if (t is not null)
                {
                    hash.Add(t, this);
                }
            }

            void AddListToHash<T>(IReadOnlyList<T> expressions)
                where T : Expression
            {
                for (var (i, cnt) = (0, expressions.Count); i < cnt; i++)
                {
                    hash.Add(expressions[i], this);
                }
            }

            void AddInitializersToHash(IReadOnlyList<ElementInit> initializers)
            {
                for (var (i, cnt) = (0, initializers.Count); i < cnt; i++)
                {
                    AddListToHash(initializers[i].Arguments);
                    hash.Add(initializers[i].AddMethod);
                }
            }

            void AddMemberBindingsToHash(IReadOnlyList<MemberBinding> memberBindings)
            {
                for (var (i, cnt) = (0, memberBindings.Count); i < cnt; i++)
                {
                    var memberBinding = memberBindings[i];

                    hash.Add(memberBinding.Member);
                    hash.Add(memberBinding.BindingType);

                    switch (memberBinding)
                    {
                        case MemberAssignment memberAssignment:
                            hash.Add(memberAssignment.Expression, this);
                            break;

                        case MemberListBinding memberListBinding:
                            AddInitializersToHash(memberListBinding.Initializers);
                            break;

                        case MemberMemberBinding memberMemberBinding:
                            AddMemberBindingsToHash(memberMemberBinding.Bindings);
                            break;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public bool Equals(Expression? left, Expression? right)
    {
        if (left == right)
        {
            return true;
        }

        if (left == null
            || right == null)
        {
            return false;
        }

        if (left.NodeType != right.NodeType)
        {
            return false;
        }

        if (left.Type != right.Type)
        {
            return false;
        }

        return new ExpressionComparer().Compare2(left, right);
    }

    private struct ExpressionComparer
    {
        private Dictionary<ParameterExpression, ParameterExpression> _parameterScope;

        public bool Compare(Expression? left, Expression? right)
        {
            if (left == right)
            {
                return true;
            }

            if (left == null
                || right == null)
            {
                return false;
            }

            if (left.NodeType != right.NodeType)
            {
                return false;
            }

            if (left.Type != right.Type)
            {
                return false;
            }

            return Compare2(left, right);
        }

        internal bool Compare2(Expression? left, Expression? right)
        {
            return left switch
            {
                BinaryExpression leftBinary => CompareBinary(leftBinary, (BinaryExpression)right!),
                BlockExpression leftBlock => CompareBlock(leftBlock, (BlockExpression)right!),
                ConditionalExpression leftConditional => CompareConditional(leftConditional, (ConditionalExpression)right!),
                ConstantExpression leftConstant => CompareConstant(leftConstant, (ConstantExpression)right!),
                DefaultExpression => true, // Intentionally empty. No additional members
                GotoExpression leftGoto => CompareGoto(leftGoto, (GotoExpression)right!),
                IndexExpression leftIndex => CompareIndex(leftIndex, (IndexExpression)right!),
                InvocationExpression leftInvocation => CompareInvocation(leftInvocation, (InvocationExpression)right!),
                LabelExpression leftLabel => CompareLabel(leftLabel, (LabelExpression)right!),
                LambdaExpression leftLambda => CompareLambda(leftLambda, (LambdaExpression)right!),
                ListInitExpression leftListInit => CompareListInit(leftListInit, (ListInitExpression)right!),
                LoopExpression leftLoop => CompareLoop(leftLoop, (LoopExpression)right!),
                MemberExpression leftMember => CompareMember(leftMember, (MemberExpression)right!),
                MemberInitExpression leftMemberInit => CompareMemberInit(leftMemberInit, (MemberInitExpression)right!),
                MethodCallExpression leftMethodCall => CompareMethodCall(leftMethodCall, (MethodCallExpression)right!),
                NewArrayExpression leftNewArray => CompareNewArray(leftNewArray, (NewArrayExpression)right!),
                NewExpression leftNew => CompareNew(leftNew, (NewExpression)right!),
                ParameterExpression leftParameter => CompareParameter(leftParameter, (ParameterExpression)right!),
                RuntimeVariablesExpression leftRuntimeVariables => CompareRuntimeVariables(
                    leftRuntimeVariables, (RuntimeVariablesExpression)right!),
                SwitchExpression leftSwitch => CompareSwitch(leftSwitch, (SwitchExpression)right!),
                TryExpression leftTry => CompareTry(leftTry, (TryExpression)right!),
                TypeBinaryExpression leftTypeBinary => CompareTypeBinary(leftTypeBinary, (TypeBinaryExpression)right!),
                UnaryExpression leftUnary => CompareUnary(leftUnary, (UnaryExpression)right!),

                _ => left.NodeType == ExpressionType.Extension
                    ? left.Equals(right)
                    : throw new InvalidOperationException(left.NodeType.ToString())
            };
        }

        private bool CompareBinary(BinaryExpression a, BinaryExpression b)
            => Equals(a.Method, b.Method)
                && a.IsLifted == b.IsLifted
                && a.IsLiftedToNull == b.IsLiftedToNull
                && Compare(a.Left, b.Left)
                && Compare(a.Right, b.Right)
                && Compare(a.Conversion, b.Conversion);

        private bool CompareBlock(BlockExpression a, BlockExpression b)
            => CompareExpressionList(a.Variables, b.Variables)
                && CompareExpressionList(a.Expressions, b.Expressions);

        private bool CompareConditional(ConditionalExpression a, ConditionalExpression b)
            => Compare(a.Test, b.Test)
                && Compare(a.IfTrue, b.IfTrue)
                && Compare(a.IfFalse, b.IfFalse);

        private static bool CompareConstant(ConstantExpression a, ConstantExpression b)
        {
            var (v1, v2) = (a.Value, b.Value);

            return Equals(v1, v2)
                || (v1 is IStructuralEquatable array1 && array1.Equals(v2, StructuralComparisons.StructuralEqualityComparer));
        }

        private bool CompareGoto(GotoExpression a, GotoExpression b)
            => a.Kind == b.Kind
                && Equals(a.Target, b.Target)
                && Compare(a.Value, b.Value);

        private bool CompareIndex(IndexExpression a, IndexExpression b)
            => Equals(a.Indexer, b.Indexer)
                && Compare(a.Object, b.Object)
                && CompareExpressionList(a.Arguments, b.Arguments);

        private bool CompareInvocation(InvocationExpression a, InvocationExpression b)
            => Compare(a.Expression, b.Expression)
                && CompareExpressionList(a.Arguments, b.Arguments);

        private bool CompareLabel(LabelExpression a, LabelExpression b)
            => Equals(a.Target, b.Target)
                && Compare(a.DefaultValue, b.DefaultValue);

        private bool CompareLambda(LambdaExpression a, LambdaExpression b)
        {
            var n = a.Parameters.Count;

            if (b.Parameters.Count != n)
            {
                return false;
            }

            _parameterScope ??= new Dictionary<ParameterExpression, ParameterExpression>();

            for (var i = 0; i < n; i++)
            {
                var (p1, p2) = (a.Parameters[i], b.Parameters[i]);

                if (p1.Type != p2.Type)
                {
                    for (var j = 0; j < i; j++)
                    {
                        _parameterScope.Remove(a.Parameters[j]);
                    }

                    return false;
                }

                if (!_parameterScope.TryAdd(p1, p2))
                {
                    throw new InvalidOperationException(p1.Name);
                }
            }

            try
            {
                return Compare(a.Body, b.Body);
            }
            finally
            {
                for (var i = 0; i < n; i++)
                {
                    _parameterScope.Remove(a.Parameters[i]);
                }
            }
        }

        private bool CompareListInit(ListInitExpression a, ListInitExpression b)
            => Compare(a.NewExpression, b.NewExpression)
                && CompareElementInitList(a.Initializers, b.Initializers);

        private bool CompareLoop(LoopExpression a, LoopExpression b)
            => Equals(a.BreakLabel, b.BreakLabel)
                && Equals(a.ContinueLabel, b.ContinueLabel)
                && Compare(a.Body, b.Body);

        private bool CompareMember(MemberExpression a, MemberExpression b)
            => a.Expression is ConstantExpression ce1 && b.Expression is ConstantExpression ce2
                ? Equals(a.Member.MemberType, b.Member.MemberType) && ce1.Type == ce2.Type && a.Member.Name == b.Member.Name
                : Equals(a.Member, b.Member) && Compare(a.Expression, b.Expression);

        private bool CompareMemberInit(MemberInitExpression a, MemberInitExpression b)
            => Compare(a.NewExpression, b.NewExpression)
                && CompareMemberBindingList(a.Bindings, b.Bindings);

        private bool CompareMethodCall(MethodCallExpression a, MethodCallExpression b)
            => Equals(a.Method, b.Method)
                && Compare(a.Object, b.Object)
                && CompareExpressionList(a.Arguments, b.Arguments);

        private bool CompareNewArray(NewArrayExpression a, NewArrayExpression b)
            => CompareExpressionList(a.Expressions, b.Expressions);

        private bool CompareNew(NewExpression a, NewExpression b)
            => Equals(a.Constructor, b.Constructor)
                && CompareExpressionList(a.Arguments, b.Arguments)
                && CompareMemberList(a.Members, b.Members);

        private readonly bool CompareParameter(ParameterExpression a, ParameterExpression b)
            => _parameterScope != null
                && _parameterScope.TryGetValue(a, out var mapped)
                    ? mapped.Type == b.Type
                    : a.Type == b.Type;

        private bool CompareRuntimeVariables(RuntimeVariablesExpression a, RuntimeVariablesExpression b)
            => CompareExpressionList(a.Variables, b.Variables);

        private bool CompareSwitch(SwitchExpression a, SwitchExpression b)
            => Equals(a.Comparison, b.Comparison)
                && Compare(a.SwitchValue, b.SwitchValue)
                && Compare(a.DefaultBody, b.DefaultBody)
                && CompareSwitchCaseList(a.Cases, b.Cases);

        private bool CompareTry(TryExpression a, TryExpression b)
            => Compare(a.Body, b.Body)
                && Compare(a.Fault, b.Fault)
                && Compare(a.Finally, b.Finally)
                && CompareCatchBlockList(a.Handlers, b.Handlers);

        private bool CompareTypeBinary(TypeBinaryExpression a, TypeBinaryExpression b)
            => a.TypeOperand == b.TypeOperand
                && Compare(a.Expression, b.Expression);

        private bool CompareUnary(UnaryExpression a, UnaryExpression b)
            => Equals(a.Method, b.Method)
                && a.IsLifted == b.IsLifted
                && a.IsLiftedToNull == b.IsLiftedToNull
                && Compare(a.Operand, b.Operand);

        private bool CompareExpressionList(IReadOnlyList<Expression> a, IReadOnlyList<Expression> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!Compare(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CompareMemberList(IReadOnlyList<MemberInfo>? a, IReadOnlyList<MemberInfo>? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!Equals(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CompareMemberBindingList(IReadOnlyList<MemberBinding> a, IReadOnlyList<MemberBinding> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!CompareBinding(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CompareBinding(MemberBinding a, MemberBinding b)
        {
            if (a == b)
            {
                return true;
            }

            if (a == null
                || b == null)
            {
                return false;
            }

            if (a.BindingType != b.BindingType)
            {
                return false;
            }

            if (!Equals(a.Member, b.Member))
            {
                return false;
            }

#pragma warning disable IDE0066 // Convert switch statement to expression
            switch (a)
#pragma warning restore IDE0066 // Convert switch statement to expression
            {
                case MemberAssignment aMemberAssignment:
                    return Compare(aMemberAssignment.Expression, ((MemberAssignment)b).Expression);

                case MemberListBinding aMemberListBinding:
                    return CompareElementInitList(aMemberListBinding.Initializers, ((MemberListBinding)b).Initializers);

                case MemberMemberBinding aMemberMemberBinding:
                    return CompareMemberBindingList(aMemberMemberBinding.Bindings, ((MemberMemberBinding)b).Bindings);

                default:
                    throw new InvalidOperationException(a.BindingType.ToString());
            }
        }

        private bool CompareElementInitList(IReadOnlyList<ElementInit> a, IReadOnlyList<ElementInit> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!CompareElementInit(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CompareElementInit(ElementInit a, ElementInit b)
            => Equals(a.AddMethod, b.AddMethod)
                && CompareExpressionList(a.Arguments, b.Arguments);

        private bool CompareSwitchCaseList(IReadOnlyList<SwitchCase> a, IReadOnlyList<SwitchCase> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!CompareSwitchCase(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CompareSwitchCase(SwitchCase a, SwitchCase b)
            => Compare(a.Body, b.Body)
                && CompareExpressionList(a.TestValues, b.TestValues);

        private bool CompareCatchBlockList(IReadOnlyList<CatchBlock> a, IReadOnlyList<CatchBlock> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!CompareCatchBlock(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CompareCatchBlock(CatchBlock a, CatchBlock b)
            => Equals(a.Test, b.Test)
                && Compare(a.Body, b.Body)
                && Compare(a.Filter, b.Filter)
                && Compare(a.Variable, b.Variable);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3897:Classes that provide \"Equals(<T>)\" should implement \"IEquatable<T>\"", Justification = "<Pending>")]
    private sealed class QueryCommandKey
    {
        private readonly Type _type;
        private readonly string _name;
        private int? _hash;

        public QueryCommandKey(Type type, string name)
        {
            _type = type;
            _name = name;
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Bug", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "<Pending>")]
        public override int GetHashCode()
        {
            if (_hash.HasValue)
                return _hash.Value;

            unchecked
            {
                HashCode hash = new();

                hash.Add(_type);
                hash.Add(_name);

                _hash = hash.ToHashCode();

                return _hash.Value;
            }
        }
        public override bool Equals(object? obj)
        {
            return Equals(obj as QueryCommandKey);
        }
        public bool Equals(QueryCommandKey? obj)
        {
            if (obj is null) return false;

            return _type == obj._type && _name == obj._name;
        }
    }
}

public class ExpressionPlanEqualityComparer : IEqualityComparer<Expression?>
{
    private readonly static ConcurrentDictionary<QueryCommandKey, Func<object?, QueryCommand>> _cmdCache = new();
    private readonly Visitor _visitor;

    public ExpressionPlanEqualityComparer(IQueryProvider queryProvider)
        : this(queryProvider, null)
    {
    }
    public ExpressionPlanEqualityComparer(IQueryProvider queryProvider, ILogger? logger)
    {
        _visitor = new Visitor(logger, queryProvider);
    }
    public int GetHashCode(Expression obj)
    {
        if (obj == null)
        {
            return 0;
        }

        unchecked
        {
            _visitor._hash = new();
            _visitor.Visit(obj);

            return _visitor._hash.ToHashCode();
        }
    }

    /// <inheritdoc />
    public bool Equals(Expression? left, Expression? right)
    {
        if (left == right)
        {
            return true;
        }

        if (left == null
            || right == null)
        {
            return false;
        }

        if (left.NodeType != right.NodeType)
        {
            return false;
        }

        if (left.Type != right.Type)
        {
            return false;
        }

        return new ExpressionComparer().Compare2(left, right);
    }

    private struct ExpressionComparer
    {
        private Dictionary<ParameterExpression, ParameterExpression> _parameterScope;

        public bool Compare(Expression? left, Expression? right)
        {
            if (left == right)
            {
                return true;
            }

            if (left == null
                || right == null)
            {
                return false;
            }

            if (left.NodeType != right.NodeType)
            {
                return false;
            }

            if (left.Type != right.Type)
            {
                return false;
            }

            return Compare2(left, right);
        }

        internal bool Compare2(Expression? left, Expression? right)
        {
            return left switch
            {
                BinaryExpression leftBinary => CompareBinary(leftBinary, (BinaryExpression)right!),
                BlockExpression leftBlock => CompareBlock(leftBlock, (BlockExpression)right!),
                ConditionalExpression leftConditional => CompareConditional(leftConditional, (ConditionalExpression)right!),
                ConstantExpression leftConstant => CompareConstant(leftConstant, (ConstantExpression)right!),
                DefaultExpression => true, // Intentionally empty. No additional members
                GotoExpression leftGoto => CompareGoto(leftGoto, (GotoExpression)right!),
                IndexExpression leftIndex => CompareIndex(leftIndex, (IndexExpression)right!),
                InvocationExpression leftInvocation => CompareInvocation(leftInvocation, (InvocationExpression)right!),
                LabelExpression leftLabel => CompareLabel(leftLabel, (LabelExpression)right!),
                LambdaExpression leftLambda => CompareLambda(leftLambda, (LambdaExpression)right!),
                ListInitExpression leftListInit => CompareListInit(leftListInit, (ListInitExpression)right!),
                LoopExpression leftLoop => CompareLoop(leftLoop, (LoopExpression)right!),
                MemberExpression leftMember => CompareMember(leftMember, (MemberExpression)right!),
                MemberInitExpression leftMemberInit => CompareMemberInit(leftMemberInit, (MemberInitExpression)right!),
                MethodCallExpression leftMethodCall => CompareMethodCall(leftMethodCall, (MethodCallExpression)right!),
                NewArrayExpression leftNewArray => CompareNewArray(leftNewArray, (NewArrayExpression)right!),
                NewExpression leftNew => CompareNew(leftNew, (NewExpression)right!),
                ParameterExpression leftParameter => CompareParameter(leftParameter, (ParameterExpression)right!),
                RuntimeVariablesExpression leftRuntimeVariables => CompareRuntimeVariables(
                    leftRuntimeVariables, (RuntimeVariablesExpression)right!),
                SwitchExpression leftSwitch => CompareSwitch(leftSwitch, (SwitchExpression)right!),
                TryExpression leftTry => CompareTry(leftTry, (TryExpression)right!),
                TypeBinaryExpression leftTypeBinary => CompareTypeBinary(leftTypeBinary, (TypeBinaryExpression)right!),
                UnaryExpression leftUnary => CompareUnary(leftUnary, (UnaryExpression)right!),

                _ => left.NodeType == ExpressionType.Extension
                    ? left.Equals(right)
                    : throw new InvalidOperationException(left.NodeType.ToString())
            };
        }

        private bool CompareBinary(BinaryExpression a, BinaryExpression b)
            => Equals(a.Method, b.Method)
                && a.IsLifted == b.IsLifted
                && a.IsLiftedToNull == b.IsLiftedToNull
                && Compare(a.Left, b.Left)
                && Compare(a.Right, b.Right)
                && Compare(a.Conversion, b.Conversion);

        private bool CompareBlock(BlockExpression a, BlockExpression b)
            => CompareExpressionList(a.Variables, b.Variables)
                && CompareExpressionList(a.Expressions, b.Expressions);

        private bool CompareConditional(ConditionalExpression a, ConditionalExpression b)
            => Compare(a.Test, b.Test)
                && Compare(a.IfTrue, b.IfTrue)
                && Compare(a.IfFalse, b.IfFalse);

        private static bool CompareConstant(ConstantExpression a, ConstantExpression b)
        {
            var (v1, v2) = (a.Value, b.Value);

            return Equals(v1, v2)
                || (v1 is IStructuralEquatable array1 && array1.Equals(v2, StructuralComparisons.StructuralEqualityComparer));
        }

        private bool CompareGoto(GotoExpression a, GotoExpression b)
            => a.Kind == b.Kind
                && Equals(a.Target, b.Target)
                && Compare(a.Value, b.Value);

        private bool CompareIndex(IndexExpression a, IndexExpression b)
            => Equals(a.Indexer, b.Indexer)
                && Compare(a.Object, b.Object)
                && CompareExpressionList(a.Arguments, b.Arguments);

        private bool CompareInvocation(InvocationExpression a, InvocationExpression b)
            => Compare(a.Expression, b.Expression)
                && CompareExpressionList(a.Arguments, b.Arguments);

        private bool CompareLabel(LabelExpression a, LabelExpression b)
            => Equals(a.Target, b.Target)
                && Compare(a.DefaultValue, b.DefaultValue);

        private bool CompareLambda(LambdaExpression a, LambdaExpression b)
        {
            var n = a.Parameters.Count;

            if (b.Parameters.Count != n)
            {
                return false;
            }

            _parameterScope ??= new Dictionary<ParameterExpression, ParameterExpression>();

            for (var i = 0; i < n; i++)
            {
                var (p1, p2) = (a.Parameters[i], b.Parameters[i]);

                if (p1.Type != p2.Type)
                {
                    for (var j = 0; j < i; j++)
                    {
                        _parameterScope.Remove(a.Parameters[j]);
                    }

                    return false;
                }

                if (!_parameterScope.TryAdd(p1, p2))
                {
                    throw new InvalidOperationException(p1.Name);
                }
            }

            try
            {
                return Compare(a.Body, b.Body);
            }
            finally
            {
                for (var i = 0; i < n; i++)
                {
                    _parameterScope.Remove(a.Parameters[i]);
                }
            }
        }

        private bool CompareListInit(ListInitExpression a, ListInitExpression b)
            => Compare(a.NewExpression, b.NewExpression)
                && CompareElementInitList(a.Initializers, b.Initializers);

        private bool CompareLoop(LoopExpression a, LoopExpression b)
            => Equals(a.BreakLabel, b.BreakLabel)
                && Equals(a.ContinueLabel, b.ContinueLabel)
                && Compare(a.Body, b.Body);

        private bool CompareMember(MemberExpression a, MemberExpression b)
            => a.Expression is ConstantExpression ce1 && b.Expression is ConstantExpression ce2
                ? Equals(a.Member.MemberType, b.Member.MemberType) && ce1.Type == ce2.Type && a.Member.Name == b.Member.Name
                : Equals(a.Member, b.Member) && Compare(a.Expression, b.Expression);

        private bool CompareMemberInit(MemberInitExpression a, MemberInitExpression b)
            => Compare(a.NewExpression, b.NewExpression)
                && CompareMemberBindingList(a.Bindings, b.Bindings);

        private bool CompareMethodCall(MethodCallExpression a, MethodCallExpression b)
            => Equals(a.Method, b.Method)
                && Compare(a.Object, b.Object)
                && CompareExpressionList(a.Arguments, b.Arguments);

        private bool CompareNewArray(NewArrayExpression a, NewArrayExpression b)
            => CompareExpressionList(a.Expressions, b.Expressions);

        private bool CompareNew(NewExpression a, NewExpression b)
            => Equals(a.Constructor, b.Constructor)
                && CompareExpressionList(a.Arguments, b.Arguments)
                && CompareMemberList(a.Members, b.Members);

        private readonly bool CompareParameter(ParameterExpression a, ParameterExpression b)
            => _parameterScope != null
                && _parameterScope.TryGetValue(a, out var mapped)
                    ? mapped.Type == b.Type
                    : a.Type == b.Type;

        private bool CompareRuntimeVariables(RuntimeVariablesExpression a, RuntimeVariablesExpression b)
            => CompareExpressionList(a.Variables, b.Variables);

        private bool CompareSwitch(SwitchExpression a, SwitchExpression b)
            => Equals(a.Comparison, b.Comparison)
                && Compare(a.SwitchValue, b.SwitchValue)
                && Compare(a.DefaultBody, b.DefaultBody)
                && CompareSwitchCaseList(a.Cases, b.Cases);

        private bool CompareTry(TryExpression a, TryExpression b)
            => Compare(a.Body, b.Body)
                && Compare(a.Fault, b.Fault)
                && Compare(a.Finally, b.Finally)
                && CompareCatchBlockList(a.Handlers, b.Handlers);

        private bool CompareTypeBinary(TypeBinaryExpression a, TypeBinaryExpression b)
            => a.TypeOperand == b.TypeOperand
                && Compare(a.Expression, b.Expression);

        private bool CompareUnary(UnaryExpression a, UnaryExpression b)
            => Equals(a.Method, b.Method)
                && a.IsLifted == b.IsLifted
                && a.IsLiftedToNull == b.IsLiftedToNull
                && Compare(a.Operand, b.Operand);

        private bool CompareExpressionList(IReadOnlyList<Expression> a, IReadOnlyList<Expression> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!Compare(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CompareMemberList(IReadOnlyList<MemberInfo>? a, IReadOnlyList<MemberInfo>? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!Equals(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CompareMemberBindingList(IReadOnlyList<MemberBinding> a, IReadOnlyList<MemberBinding> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!CompareBinding(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CompareBinding(MemberBinding a, MemberBinding b)
        {
            if (a == b)
            {
                return true;
            }

            if (a == null
                || b == null)
            {
                return false;
            }

            if (a.BindingType != b.BindingType)
            {
                return false;
            }

            if (!Equals(a.Member, b.Member))
            {
                return false;
            }

#pragma warning disable IDE0066 // Convert switch statement to expression
            switch (a)
#pragma warning restore IDE0066 // Convert switch statement to expression
            {
                case MemberAssignment aMemberAssignment:
                    return Compare(aMemberAssignment.Expression, ((MemberAssignment)b).Expression);

                case MemberListBinding aMemberListBinding:
                    return CompareElementInitList(aMemberListBinding.Initializers, ((MemberListBinding)b).Initializers);

                case MemberMemberBinding aMemberMemberBinding:
                    return CompareMemberBindingList(aMemberMemberBinding.Bindings, ((MemberMemberBinding)b).Bindings);

                default:
                    throw new InvalidOperationException(a.BindingType.ToString());
            }
        }

        private bool CompareElementInitList(IReadOnlyList<ElementInit> a, IReadOnlyList<ElementInit> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!CompareElementInit(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CompareElementInit(ElementInit a, ElementInit b)
            => Equals(a.AddMethod, b.AddMethod)
                && CompareExpressionList(a.Arguments, b.Arguments);

        private bool CompareSwitchCaseList(IReadOnlyList<SwitchCase> a, IReadOnlyList<SwitchCase> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!CompareSwitchCase(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CompareSwitchCase(SwitchCase a, SwitchCase b)
            => Compare(a.Body, b.Body)
                && CompareExpressionList(a.TestValues, b.TestValues);

        private bool CompareCatchBlockList(IReadOnlyList<CatchBlock> a, IReadOnlyList<CatchBlock> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null
                || b == null
                || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!CompareCatchBlock(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CompareCatchBlock(CatchBlock a, CatchBlock b)
            => Equals(a.Test, b.Test)
                && Compare(a.Body, b.Body)
                && Compare(a.Filter, b.Filter)
                && Compare(a.Variable, b.Variable);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3897:Classes that provide \"Equals(<T>)\" should implement \"IEquatable<T>\"", Justification = "<Pending>")]
    private sealed class QueryCommandKey
    {
        private readonly Type _type;
        private readonly string _name;
        private int? _hash;

        public QueryCommandKey(Type type, string name)
        {
            _type = type;
            _name = name;
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Bug", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "<Pending>")]
        public override int GetHashCode()
        {
            if (_hash.HasValue)
                return _hash.Value;

            unchecked
            {
                HashCode hash = new();

                hash.Add(_type);
                hash.Add(_name);

                _hash = hash.ToHashCode();

                return _hash.Value;
            }
        }
        public override bool Equals(object? obj)
        {
            return Equals(obj as QueryCommandKey);
        }
        public bool Equals(QueryCommandKey? obj)
        {
            if (obj is null) return false;

            return _type == obj._type && _name == obj._name;
        }
    }
    private sealed class Visitor(ILogger? logger, IQueryProvider queryProvider) : ExpressionVisitor
    {
        private readonly ILogger? _logger = logger;
        private readonly IQueryProvider _queryProvider = queryProvider;
        internal HashCode _hash;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void VisitBase(Expression? node)
        {
            _hash.Add(node?.NodeType);
            _hash.Add(node?.Type);
        }
        protected override Expression VisitBinary(BinaryExpression node)
        {
            VisitBase(node);
            Visit(node.Left);
            Visit(node.Right);
            AddExpressionToHashIfNotNull(node.Conversion);
            AddToHashIfNotNull(node.Method);
            return node;
        }
        protected override Expression VisitBlock(BlockExpression node)
        {
            VisitBase(node);
            AddListToHash(node.Variables);
            AddListToHash(node.Expressions);
            return node;
        }
        protected override Expression VisitConditional(ConditionalExpression node)
        {
            VisitBase(node);
            Visit(node.Test);
            Visit(node.IfTrue);
            Visit(node.IfFalse);

            return node;
        }
        protected override Expression VisitConstant(ConstantExpression node)
        {
            VisitBase(node);
            switch (node.Value)
            {
                case IQueryable:
                case null:
                    break;

                case IStructuralEquatable structuralEquatable:
                    _hash.Add(structuralEquatable.GetHashCode(StructuralComparisons.StructuralEqualityComparer));
                    break;

                default:
                    _hash.Add(node.Value);
                    break;
            }

            return node;
        }
        protected override Expression VisitGoto(GotoExpression node)
        {
            VisitBase(node);
            Visit(node.Value);
            _hash.Add(node.Kind);
            _hash.Add(node.Target);
            return node;
        }
        protected override Expression VisitIndex(IndexExpression node)
        {
            VisitBase(node);
            if (node.Type.IsAssignableTo(typeof(QueryCommand)) && node.Arguments is [ConstantExpression cex] && cex.Value is int idx)
            {
                var cmd = _queryProvider.ReferencedQueries[idx];
                _hash.Add(cmd, _queryProvider.GetQueryPlanEqualityComparer());
            }
            else
            {
                Visit(node.Object);
                AddListToHash(node.Arguments);
                _hash.Add(node.Indexer);
            }

            return node;
        }
        protected override Expression VisitInvocation(InvocationExpression node)
        {
            VisitBase(node);
            Visit(node.Expression);
            AddListToHash(node.Arguments);
            return node;
        }
        protected override Expression VisitLabel(LabelExpression node)
        {
            VisitBase(node);
            AddExpressionToHashIfNotNull(node.DefaultValue);
            _hash.Add(node.Target);
            return node;
        }
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            VisitBase(node);
            Visit(node.Body);
            AddListToHash(node.Parameters);
            _hash.Add(node.ReturnType);
            return node;
        }
        protected override Expression VisitListInit(ListInitExpression node)
        {
            VisitBase(node);
            Visit(node.NewExpression);
            AddInitializersToHash(node.Initializers);
            return node;
        }
        protected override Expression VisitLoop(LoopExpression node)
        {
            VisitBase(node);
            Visit(node.Body);
            AddToHashIfNotNull(node.BreakLabel);
            AddToHashIfNotNull(node.ContinueLabel);
            return node;
        }
        protected override Expression VisitMember(MemberExpression node)
        {
            VisitBase(node);
            if (node.Expression is ConstantExpression ce)
            {
                if (typeof(QueryCommand).IsAssignableFrom(node.Type))
                {
                    var key = new QueryCommandKey(ce.Type, node.Member.Name);
                    if (!_cmdCache.TryGetValue(key, out var del))
                    {
                        var p = Expression.Parameter(typeof(object));
                        var replace = new ReplaceConstantVisitor(Expression.Convert(p, ce!.Type));
                        var body = replace.Visit(node);
                        del = Expression.Lambda<Func<object?, QueryCommand>>(body, p).Compile();
                        //value = 1;
                        _cmdCache[key] = del;

                        if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                        {
                            _logger.LogTrace("Expression cache miss on gethashcode. hashcode: {hash}, value: {value}", key.GetHashCode(), del(ce.Value));
                        }
                        else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Expression cache miss on gethashcode");
                    }

                    var cmd = del(ce!.Value);

                    _hash.Add(cmd, _queryProvider.GetQueryPlanEqualityComparer());

                    // var cmd = Expression.Lambda<Func<QueryCommand>>(memberExpression).Compile()();
                    // hash.Add(cmd, new QueryPlanEqualityComparer(_cache));
                }
                else
                {
                    _hash.Add(ce.Type);
                    _hash.Add(node.Type);
                    _hash.Add(node.Member.Name);
                }
            }
            else
            {
                Visit(node.Expression);
                _hash.Add(node.Member);
            }
            return node;
        }
        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            VisitBase(node);
            Visit(node.NewExpression);
            AddMemberBindingsToHash(node.Bindings);

            return node;
        }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            VisitBase(node);
            Visit(node.Object);
            AddListToHash(node.Arguments);
            _hash.Add(node.Method);

            return node;
        }
        protected override Expression VisitNewArray(NewArrayExpression node)
        {
            VisitBase(node);
            AddListToHash(node.Expressions);
            return node;
        }
        protected override Expression VisitNew(NewExpression node)
        {
            VisitBase(node);
            AddListToHash(node.Arguments);
            _hash.Add(node.Constructor);

            var members = node.Members;
            if (members is not null)
            {
                for (var (i, cnt) = (0, members.Count); i < cnt; i++)
                {
                    _hash.Add(members[i]);
                }
            }

            return node;
        }
        protected override Expression VisitParameter(ParameterExpression node)
        {
            VisitBase(node);
            _hash.Add(node.Type);
            return node;
        }
        protected override Expression VisitRuntimeVariables(RuntimeVariablesExpression node)
        {
            VisitBase(node);
            AddListToHash(node.Variables);
            return node;
        }
        protected override Expression VisitSwitch(SwitchExpression node)
        {
            VisitBase(node);
            Visit(node.SwitchValue);
            AddExpressionToHashIfNotNull(node.DefaultBody);
            AddToHashIfNotNull(node.Comparison);
            var cases = node.Cases;
            for (var (i, cnt) = (0, cases.Count); i < cnt; i++)
            {
                var @case = cases[i];
                Visit(@case.Body);
                AddListToHash(@case.TestValues);
            }

            return node;
        }
        protected override Expression VisitTry(TryExpression node)
        {
            VisitBase(node);
            Visit(node.Body);
            AddExpressionToHashIfNotNull(node.Fault);
            AddExpressionToHashIfNotNull(node.Finally);
            var handlers = node.Handlers;
            if (handlers is not null)
            {
                for (var (i, cnt) = (0, handlers.Count); i < cnt; i++)
                {
                    var handler = handlers[i];
                    Visit(handler.Body);
                    AddExpressionToHashIfNotNull(handler.Variable);
                    AddExpressionToHashIfNotNull(handler.Filter);
                    _hash.Add(handler.Test);
                }
            }

            return node;
        }
        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        {
            VisitBase(node);
            Visit(node.Expression);
            _hash.Add(node.TypeOperand);

            return node;
        }
        protected override Expression VisitUnary(UnaryExpression node)
        {
            VisitBase(node);
            Visit(node.Operand);
            AddToHashIfNotNull(node.Method);

            return node;
        }
        protected override Expression VisitExtension(Expression node)
        {
            VisitBase(node);
            Visit(node);
            return node;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddToHashIfNotNull(object? t)
        {
            if (t is not null)
            {
                _hash.Add(t);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddExpressionToHashIfNotNull(Expression? t)
        {
            if (t is not null)
            {
                Visit(t);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddListToHash<T>(IReadOnlyList<T> expressions)
                    where T : Expression
        {
            for (var (i, cnt) = (0, expressions.Count); i < cnt; i++)
            {
                Visit(expressions[i]);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddInitializersToHash(IReadOnlyList<ElementInit> initializers)
        {
            for (var (i, cnt) = (0, initializers.Count); i < cnt; i++)
            {
                AddListToHash(initializers[i].Arguments);
                _hash.Add(initializers[i].AddMethod);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddMemberBindingsToHash(IReadOnlyList<MemberBinding> memberBindings)
        {
            for (var (i, cnt) = (0, memberBindings.Count); i < cnt; i++)
            {
                var memberBinding = memberBindings[i];

                _hash.Add(memberBinding.Member);
                _hash.Add(memberBinding.BindingType);

                switch (memberBinding)
                {
                    case MemberAssignment memberAssignment:
                        Visit(memberAssignment.Expression);
                        break;

                    case MemberListBinding memberListBinding:
                        AddInitializersToHash(memberListBinding.Initializers);
                        break;

                    case MemberMemberBinding memberMemberBinding:
                        AddMemberBindingsToHash(memberMemberBinding.Bindings);
                        break;
                }
            }
        }
    }
}

