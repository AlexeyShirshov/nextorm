using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Lowers an expression source (<c>() =&gt; Tvf.Factory(args)</c>) into the table-function source node
/// shared by <c>From</c> and the Join/APPLY forms. This is internal machinery: it recognises a direct
/// call to a runtime factory that returns <see cref="TvfEntityBuilder{T}"/> and rejects every other
/// shape explicitly instead of guessing.
/// </summary>
internal static class SourceExpressionLowerer
{
    /// <summary>
    /// Lowers <paramref name="source"/> into a table-function <see cref="FromExpression"/>: the metadata
    /// is materialised from a metadata-only projection of the factory call (every SQL argument replaced
    /// by a typed default), while the returned node keeps the <b>original</b> call expression and its
    /// arguments untouched.
    /// </summary>
    /// <typeparam name="T">The mapped row type of the source.</typeparam>
    /// <param name="source">The expression source supplied to the context.</param>
    /// <param name="parameterName">The parameter name reported by <see cref="ArgumentNullException"/>.</param>
    /// <returns>The lowered source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// The body is not a direct factory call, the factory does not return <see cref="TvfEntityBuilder{T}"/>
    /// for <typeparamref name="T"/>, the factory returned no descriptor, or a declared verbatim index does
    /// not address an existing argument.
    /// </exception>
    /// <exception cref="ArgumentException">A declared verbatim argument index is out of range.</exception>
    internal static FromExpression LowerTvfSource<T>(Expression<Func<EntityBuilder<T>>> source, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);

        var body = StripConvert(source.Body);
        if (body is not MethodCallExpression call)
        {
            throw new NotSupportedException(
                "The source expression must be a direct call to a runtime table-valued-function factory, for example ctx.From(() => Db.MyTvf(42)). "
                + "A captured descriptor or a composed chain loses the original argument provenance and cannot be lowered.");
        }

        var rowType = GetFactoryRowType(call);
        if (rowType is null)
        {
            throw new NotSupportedException(
                $"The method '{call.Method.DeclaringType?.Name}.{call.Method.Name}' does not return TvfEntityBuilder<T>, so it is not a runtime table-valued-function factory.");
        }

        if (rowType != typeof(T))
        {
            throw new NotSupportedException(
                $"The factory '{call.Method.DeclaringType?.Name}.{call.Method.Name}' returns TvfEntityBuilder<{rowType.Name}>, which does not match the source row type {typeof(T).Name}.");
        }

        var descriptor = MaterializeMetadata<T>(call);

        ValidateVerbatimArguments(descriptor.VerbatimArguments, call.Arguments.Count);

        var function = TableFunctionExpression.CreateRuntime(
            descriptor.Name,
            descriptor.Schema,
            descriptor.WithClause,
            descriptor.CallClause,
            descriptor.VerbatimArguments.Count == 0 ? null : descriptor.VerbatimArguments,
            call,
            typeof(T),
            descriptor.ResultSchema);

        return new FromExpression(function);
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
            factory = Expression.Quote(Expression.Lambda(delegateType, call, outer));
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
