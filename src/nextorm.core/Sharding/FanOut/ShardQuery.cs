using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Runs the same query separately for each bucket and returns one result vector per bucket.
/// </summary>
/// <remarks>
/// A shard (server) is a dynamic deployment detail and never appears here. Buckets are the stable unit:
/// the result is an array whose cardinality is the configured bucket count, and each element holds the
/// rows (or the scalar) produced for that bucket. Buckets sharing a shard run sequentially on that
/// shard's connection; different shards run in parallel, up to the configured limit. The library never
/// merges, orders or aggregates across buckets — that is the caller's decision.
/// </remarks>
/// <typeparam name="TEntity">The mapped entity selected from every bucket.</typeparam>
public sealed class ShardQuery<TEntity>
{
    private readonly EntityBuilder<TEntity> _source;
    private readonly IReadOnlyList<int> _buckets;
    private readonly IReadOnlyDictionary<int, string> _bucketOwners;
    private readonly int? _parallelism;

    internal ShardQuery(
        EntityBuilder<TEntity> source,
        IReadOnlyList<int> buckets,
        IReadOnlyDictionary<int, string> bucketOwners,
        int? parallelism)
    {
        _source = source;
        _buckets = buckets;
        _bucketOwners = bucketOwners;
        _parallelism = parallelism;
    }

    /// <summary>The buckets this fan-out runs against, in ascending order.</summary>
    public IReadOnlyList<int> Buckets => _buckets;

    /// <summary>Restricts every per-bucket command with the same predicate.</summary>
    /// <param name="predicate">The condition each row must satisfy.</param>
    /// <returns>A fan-out query with the predicate applied per bucket.</returns>
    public ShardQuery<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
        => new(_source.Where(predicate), _buckets, _bucketOwners, _parallelism);

    /// <summary>Projects every per-bucket command with the same selector.</summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="selector">The projection applied on every bucket.</param>
    /// <returns>A fan-out query producing one projected vector per bucket.</returns>
    public ShardQuery<TEntity, TResult> Select<TResult>(Expression<Func<TEntity, TResult>> selector)
        => new(_source, selector, _buckets, _bucketOwners, _parallelism);

    /// <summary>Orders each bucket's vector.</summary>
    /// <param name="keySelector">The ordering applied per bucket.</param>
    /// <returns>A fan-out query that orders every bucket independently.</returns>
    public ShardQuery<TEntity> OrderBy(Expression<Func<TEntity, object?>> keySelector)
        => new(_source.OrderBy(keySelector), _buckets, _bucketOwners, _parallelism);

    /// <summary>Removes duplicates inside each bucket's vector.</summary>
    /// <returns>A fan-out query with distinct applied per bucket.</returns>
    public ShardQuery<TEntity> Distinct()
        => new(_source.Distinct(), _buckets, _bucketOwners, _parallelism);

    /// <summary>Runs the query once per bucket and returns one vector per bucket.</summary>
    /// <returns>
    /// One element per bucket, in <see cref="Buckets"/> order; each element holds that bucket's rows.
    /// </returns>
    public List<TEntity>[] ToList() => RunEach(static query => query.ToList());

    /// <summary>Runs the query once per bucket and returns one vector per bucket.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>
    /// One element per bucket, in <see cref="Buckets"/> order; each element holds that bucket's rows.
    /// </returns>
    public async Task<List<TEntity>[]> ToListAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<TEntity>[_buckets.Count];
        await RunEachAsync(
            static (query, token) => query.ToListAsync(token),
            (position, rows) => results[position] = rows,
            cancellationToken).ConfigureAwait(false);

        return results;
    }

    /// <summary>Runs the query once per bucket and returns one vector per bucket.</summary>
    /// <returns>
    /// One element per bucket, in <see cref="Buckets"/> order; each holds that bucket's rows or is empty.
    /// </returns>
    public TEntity[][] ToArray() => [.. ToList().Select(static rows => rows.ToArray())];

    /// <summary>Runs the query once per bucket and returns one vector per bucket.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>
    /// One element per bucket, in <see cref="Buckets"/> order; each holds that bucket's rows or is empty.
    /// </returns>
    public async Task<TEntity[][]> ToArrayAsync(CancellationToken cancellationToken = default)
        => [.. (await ToListAsync(cancellationToken).ConfigureAwait(false)).Select(static rows => rows.ToArray())];

    /// <summary>Streams the result one bucket at a time, sequentially.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>One asynchronous sequence of per-bucket vectors, in bucket order.</returns>
    public async IAsyncEnumerable<TEntity[]> AsAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var position = 0; position < _buckets.Count; position++)
        {
            var rows = await ShardQueryExecution.Scoped(_source, _buckets[position])
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            yield return [.. rows];
        }
    }

    /// <summary>Returns one row count per bucket.</summary>
    /// <returns>
    /// One value per bucket, in <see cref="Buckets"/> order. The caller decides how to combine them.
    /// </returns>
    public int[] Count() => RunEach(static query => query.Count());

    /// <summary>Returns one boolean per bucket: whether that bucket has any matching row.</summary>
    /// <returns>
    /// One value per bucket, in <see cref="Buckets"/> order. The caller decides how to combine them.
    /// </returns>
    public bool[] Any() => RunEach(static query => query.Any());

    /// <summary>Returns the first row of every bucket.</summary>
    /// <returns>One row per bucket, in <see cref="Buckets"/> order.</returns>
    public TEntity[] First() => RunEach(static query => query.First());

    /// <summary>Returns the single row of every bucket, if the bucket has exactly one.</summary>
    /// <returns>One row per bucket, in <see cref="Buckets"/> order.</returns>
    public TEntity[] Single() => RunEach(static query => query.Single());

    private TResult[] RunEach<TResult>(Func<EntityBuilder<TEntity>, TResult> run)
        => ShardQueryExecution.RunEach(_source, _buckets, _bucketOwners, _parallelism, run);

    private Task RunEachAsync<TResult>(
        Func<EntityBuilder<TEntity>, CancellationToken, Task<TResult>> run,
        Action<int, TResult> store,
        CancellationToken cancellationToken)
        => ShardQueryExecution.RunEachAsync(
            _source,
            _buckets,
            _bucketOwners,
            _parallelism,
            run,
            store,
            cancellationToken);
}

/// <summary>
/// Runs the projection separately for each bucket and returns one result vector per bucket.
/// </summary>
/// <typeparam name="TEntity">The mapped entity selected from every bucket.</typeparam>
/// <typeparam name="TResult">The projected row type.</typeparam>
public sealed class ShardQuery<TEntity, TResult>
{
    private readonly EntityBuilder<TEntity> _source;
    private readonly Expression<Func<TEntity, TResult>> _selector;
    private readonly IReadOnlyList<int> _buckets;
    private readonly IReadOnlyDictionary<int, string> _bucketOwners;
    private readonly int? _parallelism;

    internal ShardQuery(
        EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TResult>> selector,
        IReadOnlyList<int> buckets,
        IReadOnlyDictionary<int, string> bucketOwners,
        int? parallelism)
    {
        _source = source;
        _selector = selector;
        _buckets = buckets;
        _bucketOwners = bucketOwners;
        _parallelism = parallelism;
    }

    /// <summary>Restricts every per-bucket command with the same predicate.</summary>
    /// <param name="predicate">The condition each row must satisfy.</param>
    /// <returns>A projected fan-out query with the predicate applied per bucket.</returns>
    public ShardQuery<TEntity, TResult> Where(Expression<Func<TEntity, bool>> predicate)
        => new(_source.Where(predicate), _selector, _buckets, _bucketOwners, _parallelism);

    /// <summary>Runs the projection once per bucket and returns one vector per bucket.</summary>
    /// <returns>
    /// One element per bucket, in <see cref="ShardQuery{TEntity}.Buckets"/> order; each holds that
    /// bucket's projected rows.
    /// </returns>
    public List<TResult>[] ToList() => RunEach(query => query.Select(_selector).ToList());

    /// <summary>Runs the projection once per bucket and returns one vector per bucket.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>
    /// One element per bucket, in <see cref="ShardQuery{TEntity}.Buckets"/> order; each holds that
    /// bucket's projected rows.
    /// </returns>
    public async Task<List<TResult>[]> ToListAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<TResult>[_buckets.Count];
        await RunEachAsync(
            (query, token) => query.Select(_selector).ToListAsync(token),
            (position, rows) => results[position] = rows,
            cancellationToken).ConfigureAwait(false);

        return results;
    }

    /// <summary>Runs the projection once per bucket and returns one vector per bucket.</summary>
    /// <returns>
    /// One element per bucket, in <see cref="ShardQuery{TEntity}.Buckets"/> order; each holds that
    /// bucket's projected rows.
    /// </returns>
    public TResult[][] ToArray() => [.. ToList().Select(static rows => rows.ToArray())];

    /// <summary>Runs the projection once per bucket and returns one vector per bucket.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>
    /// One element per bucket, in <see cref="ShardQuery{TEntity}.Buckets"/> order; each holds that
    /// bucket's projected rows.
    /// </returns>
    public async Task<TResult[][]> ToArrayAsync(CancellationToken cancellationToken = default)
        => [.. (await ToListAsync(cancellationToken).ConfigureAwait(false)).Select(static rows => rows.ToArray())];

    /// <summary>Streams the projected result one bucket at a time, sequentially.</summary>
    /// <param name="cancellationToken">Cancels the fan-out.</param>
    /// <returns>One asynchronous sequence of per-bucket vectors, in bucket order.</returns>
    public async IAsyncEnumerable<TResult[]> AsAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var position = 0; position < _buckets.Count; position++)
        {
            var rows = await ShardQueryExecution.Scoped(_source, _buckets[position])
                .Select(_selector).ToListAsync(cancellationToken).ConfigureAwait(false);
            yield return [.. rows];
        }
    }

    /// <summary>Returns one row count per bucket.</summary>
    /// <returns>
    /// One value per bucket, in <see cref="ShardQuery{TEntity}.Buckets"/> order. The caller decides
    /// how to combine them.
    /// </returns>
    public int[] Count() => RunEach(static query => query.Count());

    /// <summary>Returns one boolean per bucket: whether that bucket has any matching row.</summary>
    /// <returns>
    /// One value per bucket, in <see cref="ShardQuery{TEntity}.Buckets"/> order. The caller decides
    /// how to combine them.
    /// </returns>
    public bool[] Any() => RunEach(static query => query.Any());

    private TValue[] RunEach<TValue>(Func<EntityBuilder<TEntity>, TValue> run)
        => ShardQueryExecution.RunEach(_source, _buckets, _bucketOwners, _parallelism, run);

    private Task RunEachAsync<TValue>(
        Func<EntityBuilder<TEntity>, CancellationToken, Task<TValue>> run,
        Action<int, TValue> store,
        CancellationToken cancellationToken)
        => ShardQueryExecution.RunEachAsync(
            _source,
            _buckets,
            _bucketOwners,
            _parallelism,
            run,
            store,
            cancellationToken);
}
