namespace NextORM.Core;

/// <summary>
/// Process-wide registry of compile-time row materializers. A provider registered here is consulted
/// once per result shape, before the expression tree is built; when it returns a delegate, that
/// delegate materializes every row of the shape and no tree is compiled or interpreted.
/// </summary>
/// <remarks>
/// Registration is global because a generated materializer is a pure function of the row shape: it
/// carries no per-context state, and two contexts that materialize the same type must get the same
/// delegate. Registering the same provider instance twice is a no-op. Shapes the built-in path handles
/// with provider-specific accessors — value converters, streaming LOBs, dynamic-column stores, range
/// pairs, widened counts, non-native durations, time-of-day columns and unsupported CLR types — are
/// never offered to a provider; they always use the expression path.
/// </remarks>
public static class RowMaterializerRegistry
{
    private static readonly object Gate = new();
    private static IRowMaterializerProvider[] _providers = Array.Empty<IRowMaterializerProvider>();

    /// <summary>Registers a provider. Registering the same instance again does nothing.</summary>
    /// <param name="provider">The provider that supplies compile-time materializers.</param>
    public static void Register(IRowMaterializerProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (Gate)
        {
            foreach (var existing in _providers)
            {
                if (ReferenceEquals(existing, provider))
                    return;
            }

            var next = new IRowMaterializerProvider[_providers.Length + 1];
            Array.Copy(_providers, next, _providers.Length);
            next[^1] = provider;
            Volatile.Write(ref _providers, next);
        }
    }

    /// <summary>Removes every registered provider. Intended for tests.</summary>
    internal static void Reset()
    {
        lock (Gate)
        {
            Volatile.Write(ref _providers, Array.Empty<IRowMaterializerProvider>());
        }
    }

    /// <summary>
    /// Asks the registered providers for a materializer of <paramref name="resultType"/>, when the
    /// projection is one a compile-time materializer can reproduce faithfully.
    /// </summary>
    /// <param name="resultType">The type each row is materialized into.</param>
    /// <param name="selectList">The projected columns, in result-set order.</param>
    /// <param name="materializer">The provider's delegate, when one was returned.</param>
    /// <returns>Whether a provider returned a materializer.</returns>
    internal static bool TryCreate(Type resultType, SelectExpression[] selectList, out Delegate? materializer)
    {
        materializer = null;
        var providers = Volatile.Read(ref _providers);
        if (providers.Length == 0 || !TryDescribe(selectList, out var columns))
            return false;

        foreach (var provider in providers)
        {
            materializer = provider.TryCreate(resultType, columns);
            if (materializer is not null)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Describes the projection for a provider, or reports that the shape is not reproducible by one.
    /// A shape is eligible only when every column reads through a plain typed accessor: the columns
    /// with provider-specific handling keep the expression path so a generated materializer can never
    /// silently diverge in NULL, converter or accessor semantics.
    /// </summary>
    private static bool TryDescribe(SelectExpression[] selectList, out RowMaterializerColumn[] columns)
    {
        columns = new RowMaterializerColumn[selectList.Length];
        for (var i = 0; i < selectList.Length; i++)
        {
            var column = selectList[i];
            if (column.ProjectionItem is not null
                || column.IsDynamicColumnsStore
                || column.RangeColumnRole != RangeColumnRole.None
                || column.IsLobStreaming
                || column.IsWideCountNarrowed
                || column.Converter is not null
                || column.DurationUnit is not null
                || column.DefaultOnNull
                || !IsPlainlyReadable(column.PropertyType))
            {
                columns = Array.Empty<RowMaterializerColumn>();
                return false;
            }

            columns[i] = new RowMaterializerColumn(column.Index, column.PropertyName, column.PropertyType, column.Nullable);
        }

        return true;
    }

    /// <summary>
    /// Whether the type is read by one of the plain <see cref="System.Data.IDataRecord"/> getters the
    /// built-in accessor table selects (<c>SelectExpression.ResolveSimpleAccessor</c>). Types with a
    /// pinned <c>GetFieldValue&lt;T&gt;</c> wrapper, arrays, tuples, dictionaries and time-of-day are
    /// excluded on purpose: their accessor is provider-sensitive.
    /// </summary>
    private static bool IsPlainlyReadable(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(int)
            || underlying == typeof(long)
            || underlying == typeof(short)
            || underlying == typeof(byte)
            || underlying == typeof(bool)
            || underlying == typeof(float)
            || underlying == typeof(double)
            || underlying == typeof(decimal)
            || underlying == typeof(string)
            || underlying == typeof(Guid)
            || underlying == typeof(DateTime);
    }
}
