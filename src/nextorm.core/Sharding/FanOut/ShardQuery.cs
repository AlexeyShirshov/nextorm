using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// A cross-shard query that runs one command per selected shard and merges the rows on the client.
/// </summary>
/// <remarks>
/// Fan-out is deliberately limited to per-shard filtering, projection and materialization. Global
/// ordering, paging, distinct and aggregation cannot be answered correctly by concatenating shard
/// results, so the corresponding members throw <see cref="NotSupportedException"/> instead of returning
/// a silently wrong answer.
/// </remarks>
/// <typeparam name="TEntity">The mapped entity selected from every shard.</typeparam>
public sealed class ShardQuery<TEntity>
{
    private readonly EntityBuilder<TEntity> _source;
    private readonly IReadOnlyList<string> _shardIds;

    internal ShardQuery(EntityBuilder<TEntity> source, IReadOnlyList<string> shardIds)
    {
        _source = source;
        _shardIds = shardIds;
    }

    /// <summary>Restricts every per-shard command with the same predicate.</summary>
    /// <param name="predicate">The condition each row must satisfy.</param>
    /// <returns>A fan-out query with the predicate applied per shard.</returns>
    public ShardQuery<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
        => new(_source.Where(predicate), _shardIds);

    /// <summary>Projects every per-shard command with the same selector.</summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="selector">The projection applied on every shard.</param>
    /// <returns>A fan-out query producing <typeparamref name="TResult"/> rows.</returns>
    public ShardQuery<TEntity, TResult> Select<TResult>(Expression<Func<TEntity, TResult>> selector)
        => new(_source, selector, _shardIds);

    /// <summary>Runs one command per shard and concatenates the rows.</summary>
    /// <returns>Every row from every selected shard, in shard order.</returns>
    public List<TEntity> ToList()
    {
        var rows = new List<TEntity>();
        foreach (var shardId in _shardIds)
            rows.AddRange(_source.WithScope(new MappingScope(shardId)).ToCommand().ToList());

        return rows;
    }

    /// <summary>Runs one command per shard and returns the concatenated rows.</summary>
    /// <returns>Every row from every selected shard, in shard order.</returns>
    public TEntity[] ToArray() => [.. ToList()];

    /// <summary>Asynchronously runs one command per shard and concatenates the rows.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>Every row from every selected shard, in shard order.</returns>
    public async Task<List<TEntity>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<TEntity>();
        foreach (var shardId in _shardIds)
        {
            var shardRows = await _source.WithScope(new MappingScope(shardId)).ToCommand()
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            rows.AddRange(shardRows);
        }

        return rows;
    }

    /// <summary>Asynchronously runs one command per shard and returns the concatenated rows.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>Every row from every selected shard, in shard order.</returns>
    public async Task<TEntity[]> ToArrayAsync(CancellationToken cancellationToken = default)
        => [.. await ToListAsync(cancellationToken).ConfigureAwait(false)];

    /// <summary>Global cross-shard ordering is not supported.</summary>
    /// <typeparam name="TKey">The ordering key type.</typeparam>
    /// <param name="keySelector">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: the ordering would not be global.</exception>
    public ShardQuery<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        => throw GlobalOperationNotSupported("ordering");

    /// <summary>Global cross-shard ordering is not supported.</summary>
    /// <typeparam name="TKey">The ordering key type.</typeparam>
    /// <param name="keySelector">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: the ordering would not be global.</exception>
    public ShardQuery<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        => throw GlobalOperationNotSupported("ordering");

    /// <summary>Global cross-shard paging is not supported.</summary>
    /// <param name="count">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: the page would not be global.</exception>
    public ShardQuery<TEntity> Take(int count) => throw GlobalOperationNotSupported("paging");

    /// <summary>Global cross-shard paging is not supported.</summary>
    /// <param name="count">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: the page would not be global.</exception>
    public ShardQuery<TEntity> Skip(int count) => throw GlobalOperationNotSupported("paging");

    /// <summary>Global cross-shard distinct is not supported.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: per-shard distinct does not deduplicate across shards.</exception>
    public ShardQuery<TEntity> Distinct() => throw GlobalOperationNotSupported("distinct");

    /// <summary>Global cross-shard counting is not supported.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: per-shard counts are not a global count.</exception>
    public int Count() => throw GlobalOperationNotSupported("counting");

    /// <summary>Global cross-shard aggregation is not supported.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: per-shard aggregates are not global aggregates.</exception>
    public bool Any() => throw GlobalOperationNotSupported("aggregation");

    /// <summary>Global cross-shard aggregation is not supported.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: per-shard aggregates are not global aggregates.</exception>
    public TEntity First() => throw GlobalOperationNotSupported("aggregation");

    /// <summary>Global cross-shard aggregation is not supported.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: per-shard aggregates are not global aggregates.</exception>
    public TEntity Single() => throw GlobalOperationNotSupported("aggregation");

    /// <summary>Global cross-shard secondary ordering is not supported.</summary>
    /// <typeparam name="TKey">The ordering key type.</typeparam>
    /// <param name="keySelector">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: the ordering would not be global.</exception>
    public ShardQuery<TEntity> ThenBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        => throw GlobalOperationNotSupported("ordering");

    /// <summary>Global cross-shard secondary ordering is not supported.</summary>
    /// <typeparam name="TKey">The ordering key type.</typeparam>
    /// <param name="keySelector">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: the ordering would not be global.</exception>
    public ShardQuery<TEntity> ThenByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        => throw GlobalOperationNotSupported("ordering");

    /// <summary>Global cross-shard predicate evaluation is not supported.</summary>
    /// <param name="predicate">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: per-shard results are not a global answer.</exception>
    public bool All(Expression<Func<TEntity, bool>> predicate) => throw GlobalOperationNotSupported("aggregation");

    /// <summary>Global cross-shard aggregation is not supported.</summary>
    /// <typeparam name="TValue">The aggregate value type.</typeparam>
    /// <param name="selector">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: per-shard aggregates are not global aggregates.</exception>
    public TValue Sum<TValue>(Expression<Func<TEntity, TValue>> selector) => throw GlobalOperationNotSupported("aggregation");

    /// <summary>Global cross-shard aggregation is not supported.</summary>
    /// <typeparam name="TValue">The aggregate value type.</typeparam>
    /// <param name="selector">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: per-shard aggregates are not global aggregates.</exception>
    public TValue Min<TValue>(Expression<Func<TEntity, TValue>> selector) => throw GlobalOperationNotSupported("aggregation");

    /// <summary>Global cross-shard aggregation is not supported.</summary>
    /// <typeparam name="TValue">The aggregate value type.</typeparam>
    /// <param name="selector">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: per-shard aggregates are not global aggregates.</exception>
    public TValue Max<TValue>(Expression<Func<TEntity, TValue>> selector) => throw GlobalOperationNotSupported("aggregation");

    /// <summary>Global cross-shard aggregation is not supported.</summary>
    /// <typeparam name="TValue">The aggregate value type.</typeparam>
    /// <param name="selector">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown: per-shard aggregates are not global aggregates.</exception>
    public TValue Average<TValue>(Expression<Func<TEntity, TValue>> selector) => throw GlobalOperationNotSupported("aggregation");

    /// <summary>Streams every selected shard's rows in shard order.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>One merged asynchronous sequence, shard by shard.</returns>
    public async IAsyncEnumerable<TEntity> AsAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var shardId in _shardIds)
        {
            var stream = _source.WithScope(new MappingScope(shardId)).ToCommand().ToAsyncEnumerable(cancellationToken);
            await foreach (var row in stream.ConfigureAwait(false))
                yield return row;
        }
    }

    private static NotSupportedException GlobalOperationNotSupported(string operation)
        => new($"A cross-shard {operation} operation cannot be expressed by concatenating per-shard results. " +
               "Run it against a single shard (WithShardKey/WithScope) or merge the materialized rows on the client.");
}

/// <summary>
/// A projected cross-shard query that runs one command per selected shard and merges the rows on the client.
/// </summary>
/// <typeparam name="TEntity">The mapped entity selected from every shard.</typeparam>
/// <typeparam name="TResult">The projected row type.</typeparam>
public sealed class ShardQuery<TEntity, TResult>
{
    private readonly EntityBuilder<TEntity> _source;
    private readonly Expression<Func<TEntity, TResult>> _selector;
    private readonly IReadOnlyList<string> _shardIds;

    internal ShardQuery(EntityBuilder<TEntity> source, Expression<Func<TEntity, TResult>> selector, IReadOnlyList<string> shardIds)
    {
        _source = source;
        _selector = selector;
        _shardIds = shardIds;
    }

    /// <summary>Restricts every per-shard command with the same predicate.</summary>
    /// <param name="predicate">The condition each row must satisfy.</param>
    /// <returns>A projected fan-out query with the predicate applied per shard.</returns>
    public ShardQuery<TEntity, TResult> Where(Expression<Func<TEntity, bool>> predicate)
        => new(_source.Where(predicate), _selector, _shardIds);

    /// <summary>Runs one command per shard and concatenates the projected rows.</summary>
    /// <returns>Every projected row from every selected shard, in shard order.</returns>
    public List<TResult> ToList()
    {
        var rows = new List<TResult>();
        foreach (var shardId in _shardIds)
            rows.AddRange(_source.WithScope(new MappingScope(shardId)).Select(_selector).ToList());

        return rows;
    }

    /// <summary>Runs one command per shard and returns the concatenated projected rows.</summary>
    /// <returns>Every projected row from every selected shard, in shard order.</returns>
    public TResult[] ToArray() => [.. ToList()];

    /// <summary>Asynchronously runs one command per shard and concatenates the projected rows.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>Every projected row from every selected shard, in shard order.</returns>
    public async Task<List<TResult>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<TResult>();
        foreach (var shardId in _shardIds)
        {
            var shardRows = await _source.WithScope(new MappingScope(shardId)).Select(_selector)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            rows.AddRange(shardRows);
        }

        return rows;
    }

    /// <summary>Asynchronously runs one command per shard and returns the concatenated projected rows.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>Every projected row from every selected shard, in shard order.</returns>
    public async Task<TResult[]> ToArrayAsync(CancellationToken cancellationToken = default)
        => [.. await ToListAsync(cancellationToken).ConfigureAwait(false)];

    /// <summary>Streams every selected shard's projected rows in shard order.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>One merged asynchronous sequence, shard by shard.</returns>
    public async IAsyncEnumerable<TResult> AsAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var shardId in _shardIds)
        {
            var stream = _source.WithScope(new MappingScope(shardId)).Select(_selector).ToAsyncEnumerable(cancellationToken);
            await foreach (var row in stream.ConfigureAwait(false))
                yield return row;
        }
    }
}
