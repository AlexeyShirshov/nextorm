using Microsoft.Extensions.Logging;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace nextorm.core;

public sealed class PreciseExpressionEqualityComparerDELETE : IEqualityComparer<Expression?>
{
    private readonly IQueryProvider _queryProvider;
    private readonly ILogger? _logger;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public PreciseExpressionEqualityComparerDELETE(IQueryProvider queryProvider)
        : this(queryProvider, null)
    {
    }
    public PreciseExpressionEqualityComparerDELETE(IQueryProvider queryProvider, ILogger? logger)
    {
        _queryProvider = queryProvider;
        _logger = logger;
    }
    //public static PreciseExpressionEqualityComparer Instance { get; } = new();
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
                    hash.Add(indexExpression.Object, this);
                    AddListToHash(indexExpression.Arguments);
                    hash.Add(indexExpression.Indexer);
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
                    if (memberExpression.Expression is not null && memberExpression.Expression.Has<ConstantExpression>(out var ce))
                    {
                        var key = new ExpressionKey(memberExpression, _queryProvider);
                        if (!DataContextCache.ExpressionsCache.TryGetValue(key, out var del))
                        {
                            var p = Expression.Parameter(typeof(object));
                            var replace = new ReplaceConstantVisitor(Expression.Convert(p, ce!.Type));
                            var body = Expression.Convert(replace.Visit(memberExpression), typeof(object));
                            del = Expression.Lambda<Func<object?, object>>(body, p).Compile();
                            //value = 1;
                            DataContextCache.ExpressionsCache[key] = del;

                            if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                            {
                                _logger.LogTrace("Expression cache miss on gethashcode. hascode: {hash}, value: {value}", key.GetHashCode(), ((Func<object?, object>)del)(ce.Value));
                            }
                            else if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Expression cache miss on gethashcode");
                        }

                        AddToHashIfNotNull(((Func<object?, object>)del)(ce!.Value));
                        break;
                    }

                    hash.Add(memberExpression.Expression, this);
                    hash.Add(memberExpression.Member);
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
                    hash.Add(parameterExpression.Type);
                    // 
                    // AddToHashIfNotNull(value);
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
                if (t != null)
                {
                    hash.Add(t);
                }
            }

            void AddExpressionToHashIfNotNull(Expression? t)
            {
                if (t != null)
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
        private Dictionary<ParameterExpression, ParameterExpression>? _parameterScope;
        private readonly PreciseExpressionEqualityComparerDELETE _equalityComparer;
        public ExpressionComparer(PreciseExpressionEqualityComparerDELETE _EqualityComparer)
        {
            _equalityComparer = _EqualityComparer;
        }

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
        {
            if (!Equals(a.Member, b.Member)) return false;

            if (a.Expression is not null && a.Expression.Has<ConstantExpression>(out var ceA)
                && b.Expression is not null && b.Expression.Has<ConstantExpression>(out var ceB))
            {
                var (keyA, keyB) = (new ExpressionKey(a, _equalityComparer._queryProvider), new ExpressionKey(b, _equalityComparer._queryProvider));
                if (DataContextCache.ExpressionsCache.TryGetValue(keyA, out var delA) && DataContextCache.ExpressionsCache.TryGetValue(keyB, out var delB))
                {
                    return Equals(((Func<object?, object>)delA)(ceA!.Value), ((Func<object?, object>)delB)(ceB!.Value));
                }
                else
                {
                    if (_equalityComparer._logger?.IsEnabled(LogLevel.Debug) ?? false) _equalityComparer._logger.LogDebug("Expression cache miss on equals");

                    var pA = Expression.Parameter(typeof(object));
                    var replaceA = new ReplaceConstantVisitor(Expression.Convert(pA, ceA!.Type));
                    var bodyA = Expression.Convert(replaceA.Visit(a), typeof(object));
                    delA = Expression.Lambda<Func<object?, object>>(bodyA, pA).Compile();

                    var pB = Expression.Parameter(typeof(object));
                    var replaceB = new ReplaceConstantVisitor(Expression.Convert(pB, ceB!.Type));
                    var bodyB = Expression.Convert(replaceB.Visit(a), typeof(object));
                    delB = Expression.Lambda<Func<object?, object>>(bodyB, pB).Compile();

                    DataContextCache.ExpressionsCache[keyA] = delA;
                    DataContextCache.ExpressionsCache[keyB] = delB;

                    return Equals(((Func<object?, object>)delA)(ceA.Value), ((Func<object?, object>)delB)(ceB.Value));
                }
            }

            return Compare(a.Expression, b.Expression);
        }
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
        {
            if (_parameterScope != null
               && _parameterScope.TryGetValue(a, out var mapped)
                   ? mapped.Type == b.Type
                   : a.Type == b.Type)
            {
                // var valueA = Expression.Lambda(a).Compile().DynamicInvoke();
                // var valueB = Expression.Lambda(b).Compile().DynamicInvoke();
                // return Equals(valueA, valueB);
                return true;
            }

            return false;
        }

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
}