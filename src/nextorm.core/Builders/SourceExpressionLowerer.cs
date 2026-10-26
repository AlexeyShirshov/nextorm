using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace NextORM.Core;

/// <summary>
/// Lowers an expression source (<c>() =&gt; Tvf.Factory(args)</c>) into the table-function source node
/// shared by <c>From</c> and the Join/APPLY forms. This is internal machinery: it recognises a direct
/// call to a runtime factory that returns <see cref="TvfEntityBuilder{T}"/>, optionally followed by
/// builder operations composed on the descriptor (<c>() =&gt; Tvf.Factory(args).Where(...)</c>), and
/// rejects every other shape explicitly instead of guessing.
/// </summary>
internal static class SourceExpressionLowerer
{
    private const string NotADirectFactoryCallMessage =
        "The source expression must be a direct call to a runtime table-valued-function factory, optionally followed by builder operations such as Where or OrderBy, for example ctx.From(() => Db.MyTvf(42).Where(row => row.Id > 0)). "
        + "A captured descriptor loses the original argument provenance and cannot be lowered.";

    /// <summary>
    /// Lowers <paramref name="source"/> into a context-bound builder over the table-function source: the
    /// metadata is materialised from a metadata-only projection of the factory call (every SQL argument
    /// replaced by a typed default), the returned source node keeps the <b>original</b> call expression and
    /// its arguments untouched, and the builder operations composed on the descriptor are re-applied in
    /// their original order with their original lambda expressions.
    /// </summary>
    /// <typeparam name="T">The mapped row type of the source, i.e. the result type of the composed chain.</typeparam>
    /// <param name="context">The context the lowered source is bound to.</param>
    /// <param name="source">The expression source supplied to the context.</param>
    /// <param name="parameterName">The parameter name reported by <see cref="ArgumentNullException"/>.</param>
    /// <returns>The builder over the lowered source, carrying the composed operations.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// The body is not a direct factory call or a supported builder-operation chain over one, the factory
    /// does not return <see cref="TvfEntityBuilder{T}"/> for the factory row type, the factory returned no
    /// descriptor, or a declared verbatim index does not address an existing argument.
    /// </exception>
    /// <exception cref="ArgumentException">A declared verbatim argument index is out of range.</exception>
    internal static EntityBuilder<T> LowerTvfSource<T>(
        IDataContext context,
        Expression<Func<EntityBuilder<T>>> source,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source, parameterName);

        // Walk the composition spine from the outermost call inward. Every step is a builder operation
        // over the previous builder; the innermost receiver must be the originating factory call. The
        // outermost Where/OrderBy/... is therefore never mistaken for the factory.
        var operations = new List<MethodCallExpression>();
        var current = StripConvert(source.Body);
        while (current is MethodCallExpression { Object: not null } operation && IsSupportedBuilderOperation(operation))
        {
            operations.Add(operation);
            current = StripConvert(operation.Object!);
        }

        if (current is not MethodCallExpression call)
            throw new NotSupportedException(NotADirectFactoryCallMessage);

        var rowType = GetFactoryRowType(call);
        if (rowType is null && call.Object is not null)
        {
            // A call over a builder that is not a supported composition step (a pre-computed lambda, a
            // command-producing projection, a nested source): reject it by name instead of treating the
            // outermost operation as a factory.
            throw new NotSupportedException(
                $"The builder operation '{call.Method.DeclaringType?.Name}.{call.Method.Name}' is not supported as part of a table-valued-function source chain: only builder-returning operations with preserved lambda or constant arguments are re-applied over the originating runtime factory call.");
        }

        if (rowType is null)
        {
            throw new NotSupportedException(
                $"The method '{call.Method.DeclaringType?.Name}.{call.Method.Name}' does not return TvfEntityBuilder<T>, so it is not a runtime table-valued-function factory.");
        }

        if (operations.Count == 0 && rowType != typeof(T))
        {
            throw new NotSupportedException(
                $"The factory '{call.Method.DeclaringType?.Name}.{call.Method.Name}' returns TvfEntityBuilder<{rowType.Name}>, which does not match the source row type {typeof(T).Name}.");
        }

        // The factory row type and the composed result type are tracked separately: a type-changing
        // builder operation in the chain (for example As) makes them different, and the metadata and the
        // table-function node always belong to the factory row type.
        try
        {
            return (EntityBuilder<T>)LowerComposedMI
                .MakeGenericMethod(rowType, typeof(T))
                .Invoke(null, [context, call, operations.ToArray()])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw; // unreachable: Throw() always rethrows.
        }
    }

    private static readonly MethodInfo LowerComposedMI = typeof(SourceExpressionLowerer)
        .GetMethod(nameof(LowerComposed), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Binds the table-function source for <typeparamref name="TRow"/> and re-applies the collected
    /// builder operations in their original order. The operations are invoked with the <b>original</b>
    /// lambda expression nodes, never with a compiled delegate, so predicate/projection bodies and their
    /// closures stay expression trees and are not executed here.
    /// </summary>
    private static EntityBuilder<TResult> LowerComposed<TRow, TResult>(
        IDataContext context,
        MethodCallExpression call,
        MethodCallExpression[] operations)
    {
        var descriptor = MaterializeMetadata<TRow>(call);

        ValidateVerbatimArguments(descriptor.VerbatimArguments, call.Arguments.Count);

        var function = TableFunctionExpression.CreateRuntime(
            descriptor.Name,
            descriptor.Schema,
            descriptor.WithClause,
            descriptor.CallClause,
            descriptor.VerbatimArguments.Count == 0 ? null : descriptor.VerbatimArguments,
            call,
            typeof(TRow),
            descriptor.ResultSchema);

        var builder = context.From<TRow>();
        builder.SourceFrom = new FromExpression(function);

        object current = builder;
        for (var i = operations.Length - 1; i >= 0; i--)
            current = ApplyOperation(current, operations[i]);

        return current as EntityBuilder<TResult>
            ?? throw new NotSupportedException(
                $"The composed source expression produced '{current.GetType().Name}', not an EntityBuilder<{typeof(TResult).Name}>; this composition shape is not supported as a table-valued-function source.");
    }

    /// <summary>
    /// Recognises one supported composition step: a public instance builder operation that returns a
    /// builder and whose arguments are preserved expression nodes (lambdas) or constants. Anything else
    /// stays outside the supported spine and is rejected by the caller.
    /// </summary>
    private static bool IsSupportedBuilderOperation(MethodCallExpression operation)
    {
        if (operation.Method.IsStatic || !operation.Method.IsPublic)
            return false;

        if (!IsBuilderType(operation.Method.DeclaringType) || !IsBuilderType(operation.Method.ReturnType))
            return false;

        var parameters = operation.Method.GetParameters();
        if (parameters.Length != operation.Arguments.Count)
            return false;

        for (var i = 0; i < parameters.Length; i++)
        {
            var argument = operation.Arguments[i];
            if (typeof(LambdaExpression).IsAssignableFrom(parameters[i].ParameterType))
            {
                if (argument is not UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression })
                    return false;
            }
            else if (argument is not ConstantExpression)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBuilderType(Type? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current == typeof(EntityBuilder))
                return true;

            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(EntityBuilder<>))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Invokes one composition step on <paramref name="builder"/>. Lambda arguments are taken from the
    /// expression tree as <see cref="LambdaExpression"/> nodes; a constant argument is passed as its
    /// value. No expression body is compiled or evaluated for metadata discovery.
    /// </summary>
    private static object ApplyOperation(object builder, MethodCallExpression operation)
    {
        var parameters = operation.Method.GetParameters();
        var arguments = new object?[parameters.Length];
        for (var i = 0; i < arguments.Length; i++)
            arguments[i] = ConvertArgument(operation, parameters[i], operation.Arguments[i]);

        object? result;
        try
        {
            result = operation.Method.Invoke(builder, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw; // unreachable: Throw() always rethrows.
        }

        return result
            ?? throw new NotSupportedException(
                $"The builder operation '{operation.Method.DeclaringType?.Name}.{operation.Method.Name}' returned no builder, so it cannot be part of a table-valued-function source chain.");
    }

    private static object? ConvertArgument(MethodCallExpression operation, ParameterInfo parameter, Expression argument)
    {
        if (typeof(LambdaExpression).IsAssignableFrom(parameter.ParameterType))
        {
            return argument is UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda }
                ? lambda
                : throw new NotSupportedException(
                    $"The builder operation '{operation.Method.Name}' must be given a lambda expression directly, not a '{argument.NodeType}' node; this shape cannot be lowered without losing the original expression.");
        }

        if (argument is ConstantExpression constant)
            return constant.Value;

        throw new NotSupportedException(
            $"The builder operation '{operation.Method.Name}' has an argument of shape '{argument.NodeType}', which is neither a preserved lambda expression nor a constant; this composition shape is not supported for a table-valued-function source.");
    }

    private static readonly MethodInfo BuildCorrelatedApplyMI = typeof(SourceExpressionLowerer)
        .GetMethod(nameof(BuildCorrelatedApply), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo BuildCorrelatedApplyFromOuterMI = typeof(SourceExpressionLowerer)
        .GetMethod(nameof(BuildCorrelatedApplyFromOuter), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Recognises the correlated <c>CROSS/OUTER APPLY</c> form
    /// (<c>builder.CrossApply(row =&gt; Db.MyTvf(row.Id))</c>) and rewrites it into an APPLY source whose
    /// body builds the same table-function source <b>with the outer row still in the expression</b>.
    /// <para>
    /// The outer parameter is not evaluated here: the expression is lowered again at preparation time,
    /// after the outer member reads have been replaced by outer-reference markers, so a correlated
    /// argument stays a SQL column reference. Metadata materialisation keeps using typed defaults, so
    /// neither the outer value nor any other argument value is ever read.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The mapped row type of the applied source.</typeparam>
    /// <param name="source">The correlated APPLY lambda supplied to the builder.</param>
    /// <param name="context">The context the APPLY source is bound to when it is built.</param>
    /// <param name="applySource">The rewritten APPLY lambda, or <see langword="null"/> when the body is not a TVF factory call.</param>
    /// <returns><see langword="true"/> when <paramref name="source"/> is a TVF factory call.</returns>
    internal static bool TryCreateCorrelatedTvfApply<T>(
        LambdaExpression source,
        IDataContext context,
        [NotNullWhen(true)] out LambdaExpression? applySource)
    {
        applySource = null;

        if (StripConvert(source.Body) is not MethodCallExpression call || GetFactoryRowType(call) != typeof(T))
            return false;

        // Keep the factory call verbatim (the outer parameter included) and defer the lowering to
        // preparation: the marker rewrite has to run first, and it only sees the original tree.
        //
        // The nested factory lambda must be well-formed. An uncorrelated source is a zero-argument
        // Func<EntityBuilder<T>> and carries no parameter list; a correlated source carries exactly one
        // outer parameter and uses the matching one-argument delegate, so the OUTER parameter list is
        // never grafted onto a zero-argument delegate (that mismatch fails at Expression.Lambda).
        Expression factory;
        MethodInfo applyMethod;
        if (source.Parameters.Count == 0)
        {
            factory = Expression.Quote(Expression.Lambda<Func<EntityBuilder<T>>>(call));
            applyMethod = BuildCorrelatedApplyMI.MakeGenericMethod(typeof(T));
        }
        else if (source.Parameters.Count == 1)
        {
            var outer = source.Parameters[0];
            var delegateType = typeof(Func<,>).MakeGenericType(outer.Type, typeof(EntityBuilder<T>));

            // Declare a fresh, unused parameter in the quoted factory lambda instead of reusing the
            // outer parameter instance. Reusing it makes the plan-key comparer reject the whole
            // ApplySource ("a nested lambda reused an outer parameter"), so two identical correlated
            // APPLY sources would never share a cached plan and every execution would add a new plan
            // entry. The body keeps the outer parameter as a free variable: PushOuter matches it by
            // reference at preparation time, and the Quote is only inspected, never compiled, so the
            // free variable is not a scope error.
            var declared = Expression.Parameter(outer.Type, outer.Name);
            factory = Expression.Quote(Expression.Lambda(delegateType, call, declared));
            applyMethod = BuildCorrelatedApplyFromOuterMI.MakeGenericMethod(outer.Type, typeof(T));
        }
        else
        {
            return false;
        }

        var command = Expression.Convert(
            Expression.Call(applyMethod, Expression.Constant(context, typeof(IDataContext)), factory),
            typeof(QueryCommand));

        applySource = Expression.Lambda(command, source.Parameters);
        return true;
    }

    private static QueryCommand BuildCorrelatedApply<T>(IDataContext context, Expression<Func<EntityBuilder<T>>> source)
        => context.From(source).ToCommand();

    private static QueryCommand BuildCorrelatedApplyFromOuter<TEntity, T>(IDataContext context, Expression<Func<TEntity, EntityBuilder<T>>> source)
        // The outer member reads are already rewritten to outer-reference markers by the preparation
        // visitor; the parameter list is dropped here because From(...) carries the call, not the
        // correlation, and the rewritten body no longer references the outer parameter.
        => context.From(Expression.Lambda<Func<EntityBuilder<T>>>(source.Body)).ToCommand();

    private static Type? GetFactoryRowType(MethodCallExpression call)
    {
        var returnType = call.Method.ReturnType;
        return returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(TvfEntityBuilder<>)
            ? returnType.GetGenericArguments()[0]
            : null;
    }

    // The metadata projection: the factory is invoked once with a typed default in every SQL argument
    // position. This never evaluates the original argument expressions (no correlated column becomes a
    // CLR value) and the defaults never reach SQL lowering, which keeps the untouched call.
    private static TvfEntityBuilder<T> MaterializeMetadata<T>(MethodCallExpression call)
    {
        var defaults = new Expression[call.Arguments.Count];
        for (var i = 0; i < defaults.Length; i++)
            defaults[i] = Expression.Default(call.Arguments[i].Type);

        var defaulted = call.Object is null
            ? Expression.Call(call.Method, defaults)
            : Expression.Call(call.Object, call.Method, defaults);

        var factory = Expression.Lambda<Func<TvfEntityBuilder<T>>>(defaulted).Compile();
        var descriptor = factory();

        return descriptor
            ?? throw new NotSupportedException(
                $"The factory '{call.Method.DeclaringType?.Name}.{call.Method.Name}' returned no table-valued-function descriptor.");
    }

    private static void ValidateVerbatimArguments(IReadOnlyList<int> verbatimArguments, int argumentCount)
    {
        for (var i = 0; i < verbatimArguments.Count; i++)
        {
            var index = verbatimArguments[i];
            if (index >= argumentCount)
            {
                throw new ArgumentException(
                    $"The verbatim argument index {index} is out of range: the factory call has {argumentCount} argument(s).");
            }
        }
    }

    private static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            expression = unary.Operand;

        return expression;
    }
}
