using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public struct ReadonlyExpressionVisitor
{
    public Func<MethodCallExpression, bool>? MethodCallHandler;
    public readonly void Visit(Expression expression)
    {
        switch (expression)
        {
            case BinaryExpression binaryExpression:
                if (binaryExpression.Conversion is not null)
                    Visit(binaryExpression.Conversion);

                Visit(binaryExpression.Left);
                Visit(binaryExpression.Right);

                break;

            case BlockExpression blockExpression:
                for (var (i, cnt) = (0, blockExpression.Variables.Count); i < cnt; i++)
                {
                    var paramExp = blockExpression.Variables[i];
                    Visit(paramExp);
                }

                for (var (i, cnt) = (0, blockExpression.Expressions.Count); i < cnt; i++)
                {
                    var exp = blockExpression.Expressions[i];
                    Visit(exp);
                }
                break;

            case ConditionalExpression conditionalExpression:
                Visit(conditionalExpression.Test);
                Visit(conditionalExpression.IfTrue);
                Visit(conditionalExpression.IfFalse);
                break;

            case ConstantExpression:
                // Intentionally empty. No additional members
                break;

            case DefaultExpression:
                // Intentionally empty. No additional members
                break;

            case GotoExpression gotoExpression:
                if (gotoExpression.Value is not null)
                    Visit(gotoExpression.Value);

                break;

            case IndexExpression indexExpression:
                if (indexExpression.Object is not null)
                    Visit(indexExpression.Object);

                for (var (i, cnt) = (0, indexExpression.Arguments.Count); i < cnt; i++)
                {
                    var exp = indexExpression.Arguments[i];
                    Visit(exp);
                }

                break;

            case InvocationExpression invocationExpression:
                Visit(invocationExpression.Expression);

                for (var (i, cnt) = (0, invocationExpression.Arguments.Count); i < cnt; i++)
                {
                    var exp = invocationExpression.Arguments[i];
                    Visit(exp);
                }

                break;

            case LabelExpression labelExpression:
                if (labelExpression.DefaultValue is not null)
                    Visit(labelExpression.DefaultValue);

                break;

            case LambdaExpression lambdaExpression:
                Visit(lambdaExpression.Body);

                for (var (i, cnt) = (0, lambdaExpression.Parameters.Count); i < cnt; i++)
                {
                    var paramExp = lambdaExpression.Parameters[i];
                    Visit(paramExp);
                }

                break;

            case ListInitExpression listInitExpression:
                Visit(listInitExpression.NewExpression);

                break;

            case LoopExpression loopExpression:
                Visit(loopExpression.Body);

                break;

            case MemberExpression memberExpression:
                if (memberExpression.Expression is not null)
                    Visit(memberExpression.Expression);

                break;

            case MemberInitExpression memberInitExpression:
                Visit(memberInitExpression.NewExpression);

                break;

            case MethodCallExpression methodCallExpression:
                if (MethodCallHandler is not null && !MethodCallHandler(methodCallExpression))
                    return;

                if (methodCallExpression.Object is not null)
                    Visit(methodCallExpression.Object);

                for (var (i, cnt) = (0, methodCallExpression.Arguments.Count); i < cnt; i++)
                {
                    var exp = methodCallExpression.Arguments[i];
                    Visit(exp);
                }
                break;

            case NewArrayExpression newArrayExpression:
                for (var (i, cnt) = (0, newArrayExpression.Expressions.Count); i < cnt; i++)
                {
                    var exp = newArrayExpression.Expressions[i];
                    Visit(exp);
                }

                break;

            case NewExpression newExpression:
                for (var (i, cnt) = (0, newExpression.Arguments.Count); i < cnt; i++)
                {
                    var exp = newExpression.Arguments[i];
                    Visit(exp);
                }

                break;

            case ParameterExpression parameterExpression:

                break;

            case RuntimeVariablesExpression runtimeVariablesExpression:
                for (var (i, cnt) = (0, runtimeVariablesExpression.Variables.Count); i < cnt; i++)
                {
                    var paramExp = runtimeVariablesExpression.Variables[i];
                    Visit(paramExp);
                }
                break;

            case SwitchExpression switchExpression:
                Visit(switchExpression.SwitchValue);

                if (switchExpression.DefaultBody is not null)
                    Visit(switchExpression.DefaultBody);

                for (var (i, cnt) = (0, switchExpression.Cases.Count); i < cnt; i++)
                {
                    var @case = switchExpression.Cases[i];
                    Visit(@case.Body);

                    for (var (j, cnt2) = (0, @case.TestValues.Count); j < cnt2; j++)
                    {
                        var exp = @case.TestValues[j];
                        Visit(exp);
                    }
                }

                break;

            case TryExpression tryExpression:
                Visit(tryExpression.Body);

                if (tryExpression.Fault is not null)
                    Visit(tryExpression.Fault);

                if (tryExpression.Finally is not null)
                    Visit(tryExpression.Finally);

                if (tryExpression.Handlers != null)
                {
                    for (var (i, cnt) = (0, tryExpression.Handlers.Count); i < cnt; i++)
                    {
                        var handler = tryExpression.Handlers[i];
                        Visit(handler.Body);

                        if (handler.Variable is not null)
                            Visit(handler.Variable);

                        if (handler.Filter is not null)
                            Visit(handler.Filter);
                    }
                }

                break;

            case TypeBinaryExpression typeBinaryExpression:
                Visit(typeBinaryExpression.Expression);
                break;

            case UnaryExpression unaryExpression:
                Visit(unaryExpression.Operand);

                break;

            default:
                if (expression.NodeType == ExpressionType.Extension)
                {
                    Visit(expression);
                    break;
                }

                throw new NotSupportedException(expression.NodeType.ToString());
        }
    }
}

public class ReadonlyExpressionVisitor2 : ExpressionVisitor
{
    // [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Accept")]
    // extern static Expression Accept(Expression @this, ExpressionVisitor visitor);
    protected override Expression VisitBinary(BinaryExpression binaryExpression)
    {
        if (binaryExpression.Conversion is not null)
            Visit(binaryExpression.Conversion);

        Visit(binaryExpression.Left);
        Visit(binaryExpression.Right);

        return binaryExpression;
    }
    protected override Expression VisitBlock(BlockExpression blockExpression)
    {
        var @params = blockExpression.Variables;
        for (var (i, cnt) = (0, @params.Count); i < cnt; i++)
        {
            var paramExp = @params[i];
            Visit(paramExp);
        }

        var items = blockExpression.Expressions;
        for (var (i, cnt) = (0, items.Count); i < cnt; i++)
        {
            var exp = items[i];
            Visit(exp);
        }

        return blockExpression;
    }
    protected override Expression VisitConditional(ConditionalExpression conditionalExpression)
    {
        Visit(conditionalExpression.Test);
        Visit(conditionalExpression.IfTrue);
        Visit(conditionalExpression.IfFalse);

        return conditionalExpression;
    }
    protected override Expression VisitGoto(GotoExpression gotoExpression)
    {
        if (gotoExpression.Value is not null)
            Visit(gotoExpression.Value);

        return gotoExpression;
    }
    protected override Expression VisitIndex(IndexExpression indexExpression)
    {
        if (indexExpression.Object is not null)
            Visit(indexExpression.Object);

        var args = indexExpression.Arguments;
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            var exp = args[i];
            Visit(exp);
        }

        return indexExpression;
    }
    protected override Expression VisitInvocation(InvocationExpression invocationExpression)
    {
        Visit(invocationExpression.Expression);

        var args = invocationExpression.Arguments;
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            var exp = args[i];
            Visit(exp);
        }

        return invocationExpression;
    }
    protected override Expression VisitLabel(LabelExpression labelExpression)
    {
        if (labelExpression.DefaultValue is not null)
            Visit(labelExpression.DefaultValue);

        return labelExpression;
    }
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        Visit(node.Body);

        var @params = node.Parameters;
        for (var (i, cnt) = (0, @params.Count); i < cnt; i++)
        {
            var paramExp = @params[i];
            Visit(paramExp);
        }

        return node;
    }
    protected override Expression VisitListInit(ListInitExpression listInitExpression)
    {
        Visit(listInitExpression.NewExpression);
        return listInitExpression;
    }
    protected override Expression VisitLoop(LoopExpression loopExpression)
    {
        Visit(loopExpression.Body);
        return loopExpression;
    }
    protected override Expression VisitMember(MemberExpression memberExpression)
    {
        if (memberExpression.Expression is not null)
            Visit(memberExpression.Expression);

        return memberExpression;
    }
    protected override Expression VisitMemberInit(MemberInitExpression memberInitExpression)
    {
        Visit(memberInitExpression.NewExpression);
        return memberInitExpression;
    }
    protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
    {
        if (methodCallExpression.Object is not null)
            Visit(methodCallExpression.Object);

        var args = methodCallExpression.Arguments;
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            var exp = args[i];
            Visit(exp);
        }

        return methodCallExpression;
    }
    protected override Expression VisitNewArray(NewArrayExpression newArrayExpression)
    {
        var items = newArrayExpression.Expressions;
        for (var (i, cnt) = (0, items.Count); i < cnt; i++)
        {
            var exp = items[i];
            Visit(exp);
        }

        return newArrayExpression;
    }
    protected override Expression VisitNew(NewExpression newExpression)
    {
        var args = newExpression.Arguments;
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            var exp = args[i];
            Visit(exp);
        }

        return newExpression;
    }
    protected override Expression VisitRuntimeVariables(RuntimeVariablesExpression runtimeVariablesExpression)
    {
        var vars = runtimeVariablesExpression.Variables;
        for (var (i, cnt) = (0, vars.Count); i < cnt; i++)
        {
            var paramExp = vars[i];
            Visit(paramExp);
        }

        return runtimeVariablesExpression;
    }
    protected override Expression VisitSwitch(SwitchExpression switchExpression)
    {
        Visit(switchExpression.SwitchValue);

        if (switchExpression.DefaultBody is not null)
            Visit(switchExpression.DefaultBody);

        var cases = switchExpression.Cases;
        for (var (i, cnt) = (0, cases.Count); i < cnt; i++)
        {
            var @case = cases[i];
            Visit(@case.Body);

            var values = @case.TestValues;
            for (var (j, cnt2) = (0, values.Count); j < cnt2; j++)
            {
                var exp = values[j];
                Visit(exp);
            }
        }

        return switchExpression;
    }
    protected override Expression VisitTry(TryExpression tryExpression)
    {
        Visit(tryExpression.Body);

        if (tryExpression.Fault is not null)
            Visit(tryExpression.Fault);

        if (tryExpression.Finally is not null)
            Visit(tryExpression.Finally);

        var handlers = tryExpression.Handlers;
        if (handlers is not null)
        {
            for (var (i, cnt) = (0, handlers.Count); i < cnt; i++)
            {
                var handler = handlers[i];
                Visit(handler.Body);

                if (handler.Variable is not null)
                    Visit(handler.Variable);

                if (handler.Filter is not null)
                    Visit(handler.Filter);
            }
        }

        return tryExpression;
    }
    protected override Expression VisitTypeBinary(TypeBinaryExpression typeBinaryExpression)
    {
        Visit(typeBinaryExpression.Expression);
        return typeBinaryExpression;
    }
    protected override Expression VisitUnary(UnaryExpression unaryExpression)
    {
        Visit(unaryExpression.Operand);
        return unaryExpression;
    }
}