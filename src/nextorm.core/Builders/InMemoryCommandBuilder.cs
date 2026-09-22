namespace NextORM.Core;

// public class InMemoryCommandBuilder<TEntity> : CommandBuilder<TEntity>
// {
//     private IEnumerable<TEntity>? _data;
//     private IAsyncEnumerable<TEntity>? _asyncData;

//     public InMemoryCommandBuilder(IDataProvider dataProvider) : base(dataProvider)
//     {
//     }
//     public void WithData(IEnumerable<TEntity>? data)
//     {
//         _data = data;
//     }
//     public void WithAsyncData(IAsyncEnumerable<TEntity>? data)
//     {
//         _asyncData = data;
//     }
//     protected override void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
//     {
//         if (_data is not null)
//             cmd.AddOrUpdatePayload(()=>new InMemoryDataPayload<TEntity>(_data));

//         if (_asyncData is not null)
//             cmd.AddOrUpdatePayload(()=>new InMemoryAsyncDataPayload<TEntity>(_asyncData));

//         base.OnCommandCreated(cmd);
//     }
// }

/// <summary>
/// Extension methods that register the data an <c>EntityBuilder&lt;TEntity&gt;</c> queries when it runs
/// on the in-memory provider (<c>InMemoryDataContext</c>). Calls on any other provider are no-ops.
/// </summary>
public static class InMemoryCommandBuilderExtensions
{
    /// <summary>
    /// Registers <paramref name="data"/> as the sequence queried for <typeparamref name="TEntity"/> when
    /// <paramref name="builder"/> runs on the in-memory provider. A <c>null</c> value clears the
    /// registration. Ignored by other providers.
    /// </summary>
    /// <typeparam name="TEntity">The entity type the data is registered for.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="data">The source sequence, or <c>null</c> to clear the registration.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static EntityBuilder<TEntity> WithData<TEntity>(this EntityBuilder<TEntity> builder, IEnumerable<TEntity>? data)
    {
        if (builder.DataProvider is InMemoryDataContext inMemoryProvider)
        {
            inMemoryProvider.Data[typeof(TEntity)] = data;
        }

        return builder;
    }
    /// <summary>
    /// Registers <paramref name="data"/> as the asynchronous sequence queried for
    /// <typeparamref name="TEntity"/> when <paramref name="builder"/> runs on the in-memory provider. A
    /// <c>null</c> value clears the registration. Ignored by other providers.
    /// </summary>
    /// <typeparam name="TEntity">The entity type the data is registered for.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="data">The source sequence, or <c>null</c> to clear the registration.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static EntityBuilder<TEntity> WithAsyncData<TEntity>(this EntityBuilder<TEntity> builder, IAsyncEnumerable<TEntity>? data)
    {
        if (builder.DataProvider is InMemoryDataContext inMemoryProvider)
        {
            inMemoryProvider.Data[typeof(TEntity)] = data;
        }

        return builder;
    }
}