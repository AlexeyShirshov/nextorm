using System.Linq.Expressions;

namespace nextorm.core;

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

public static class InMemoryCommandBuilderExtensions
{
    public static void WithData<TEntity>(this CommandBuilder<TEntity> builder, IEnumerable<TEntity>? data)
    {
        if (data is not null)
            builder.AddOrUpdatePayload(()=>
            {
                builder.CommandCreatedEvent += OnCommandCreated;
                return new InMemoryDataPayload<TEntity>(data);
            },
            existing=>
            {
                return new InMemoryDataPayload<TEntity>(data);
            });
        else
        {
            builder.RemovePayload<InMemoryDataPayload<TEntity>>();
            builder.CommandCreatedEvent -= OnCommandCreated;
        }
    }
    public static void WithAsyncData<TEntity>(this CommandBuilder<TEntity> builder, IAsyncEnumerable<TEntity>? data)
    {
        if (data is not null)
            builder.AddOrUpdatePayload(()=>
            {
                builder.CommandCreatedEvent += OnCommandCreated;
                return new InMemoryAsyncDataPayload<TEntity>(data);
            },
            existing=>
            {
                return new InMemoryAsyncDataPayload<TEntity>(data);
            });
        else
        {
            builder.RemovePayload<InMemoryAsyncDataPayload<TEntity>>();
            builder.CommandCreatedEvent -= OnCommandCreated;
        }
    }
    private static void OnCommandCreated<TEntity>(CommandBuilder<TEntity> sender, QueryCommand queryCommand)
    {
        if (sender.TryGetPayload<InMemoryDataPayload<TEntity>>(out var payload))
            queryCommand.AddOrUpdatePayload(payload);

        if (sender.TryGetPayload<InMemoryAsyncDataPayload<TEntity>>(out var asyncPayload))
            queryCommand.AddOrUpdatePayload(asyncPayload);
    }
}

public record InMemoryDataPayload<T>(IEnumerable<T>? Data) : IPayload;
public record InMemoryAsyncDataPayload<T>(IAsyncEnumerable<T>? Data) : IPayload;