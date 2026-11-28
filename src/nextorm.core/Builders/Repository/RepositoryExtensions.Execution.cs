using System.Linq.Expressions;

namespace NextORM.Core;

public static partial class RepositoryExtensions
{
    extension<TEntity>(IRepository<TEntity> repository)
    {
        /// <summary>Checks whether the query returns any rows.</summary>
        public bool Any() => repository.Query().Any();

        /// <summary>Asynchronously checks whether the query returns any rows.</summary>
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
            repository.Query().AnyAsync(cancellationToken);

        /// <summary>Counts the rows using a 64-bit result.</summary>
        public long LongCount() => repository.Query().LongCount();

        /// <summary>Asynchronously counts the rows using a 64-bit result.</summary>
        public Task<long> LongCountAsync(CancellationToken cancellationToken = default) =>
            repository.Query().LongCountAsync(cancellationToken);

        /// <summary>Reads the first row in provider order and throws if the query is empty.</summary>
        public TEntity First() => repository.Query().First();

        /// <summary>Asynchronously reads the first row in provider order and throws if the query is empty.</summary>
        public Task<TEntity> FirstAsync(CancellationToken cancellationToken = default) =>
            repository.Query().FirstAsync(cancellationToken);

        /// <summary>Reads the first row in provider order, or returns the default value if the query is empty.</summary>
        public TEntity? FirstOrDefault() => repository.Query().FirstOrDefault();

        /// <summary>Asynchronously reads the first row in provider order, or returns the default value if the query is empty.</summary>
        public Task<TEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) =>
            repository.Query().FirstOrDefaultAsync(cancellationToken);

        /// <summary>Reads the only row and throws if the query does not produce exactly one row.</summary>
        public TEntity Single() => repository.Query().Single();

        /// <summary>Asynchronously reads the only row and throws if the query does not produce exactly one row.</summary>
        public Task<TEntity> SingleAsync(CancellationToken cancellationToken = default) =>
            repository.Query().SingleAsync(cancellationToken);

        /// <summary>Reads the only row, returns the default value if empty, or throws if there are multiple rows.</summary>
        public TEntity? SingleOrDefault() => repository.Query().SingleOrDefault();

        /// <summary>Asynchronously reads the only row, returns the default value if empty, or throws if there are multiple rows.</summary>
        public Task<TEntity?> SingleOrDefaultAsync(CancellationToken cancellationToken = default) =>
            repository.Query().SingleOrDefaultAsync(cancellationToken);

        /// <summary>Reads the query results into an array.</summary>
        public TEntity[] ToArray() => repository.Query().ToArray();

        /// <summary>Asynchronously reads the query results into an array.</summary>
        public Task<TEntity[]> ToArrayAsync(CancellationToken cancellationToken = default) =>
            repository.Query().ToArrayAsync(cancellationToken);

        /// <summary>Reads the query results into a hash set.</summary>
        public HashSet<TEntity> ToHashSet() => repository.Query().ToHashSet();

        /// <summary>Reads the query results into a hash set using the given comparer.</summary>
        public HashSet<TEntity> ToHashSet(IEqualityComparer<TEntity>? comparer) =>
            repository.Query().ToHashSet(comparer, ReadOnlySpan<object?>.Empty);

        /// <summary>Asynchronously reads the query results into a hash set.</summary>
        public Task<HashSet<TEntity>> ToHashSetAsync() => repository.Query().ToHashSetAsync();

        /// <summary>Asynchronously reads the query results into a hash set with cancellation support.</summary>
        public Task<HashSet<TEntity>> ToHashSetAsync(CancellationToken cancellationToken) =>
            repository.Query().ToHashSetAsync(cancellationToken);

        /// <summary>Asynchronously reads the query results into a hash set using the given comparer.</summary>
        public Task<HashSet<TEntity>> ToHashSetAsync(
            IEqualityComparer<TEntity>? comparer, CancellationToken cancellationToken = default) =>
            repository.Query().ToHashSetAsync(comparer, cancellationToken);

        /// <summary>Reads the query results into a dictionary using the given key selector.</summary>
        public Dictionary<TKey, TEntity> ToDictionary<TKey>(Func<TEntity, TKey> keySelector)
            where TKey : notnull =>
            repository.Query().ToDictionary(keySelector);

        /// <summary>Reads the query results into a dictionary using the given key selector and comparer.</summary>
        public Dictionary<TKey, TEntity> ToDictionary<TKey>(Func<TEntity, TKey> keySelector, IEqualityComparer<TKey>? comparer)
            where TKey : notnull =>
            repository.Query().ToDictionary(keySelector, comparer, ReadOnlySpan<object?>.Empty);

        /// <summary>Asynchronously reads the query results into a dictionary using the given key selector.</summary>
        public Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TKey>(Func<TEntity, TKey> keySelector)
            where TKey : notnull =>
            repository.Query().ToDictionaryAsync(keySelector);

        /// <summary>Asynchronously reads the query results into a dictionary with cancellation support.</summary>
        public Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TKey>(
            Func<TEntity, TKey> keySelector, CancellationToken cancellationToken)
            where TKey : notnull =>
            repository.Query().ToDictionaryAsync(keySelector, cancellationToken);

        /// <summary>Asynchronously reads the query results into a dictionary using the given key selector and comparer.</summary>
        public Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TKey>(Func<TEntity, TKey> keySelector,
            IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken = default)
            where TKey : notnull =>
            repository.Query().ToDictionaryAsync(keySelector, comparer, cancellationToken);

        /// <summary>Returns a synchronous sequence of query results.</summary>
        public IEnumerable<TEntity> ToEnumerable() => repository.Query().ToEnumerable();

        /// <summary>Returns an asynchronous sequence of query results.</summary>
        public IAsyncEnumerable<TEntity> ToAsyncEnumerable(CancellationToken cancellationToken = default) =>
            repository.Query().ToAsyncEnumerable(cancellationToken);

        /// <summary>Prepares the query for repeated execution that materializes results.</summary>
        public IPreparedBufferedQueryCommand<TEntity> PrepareBuffered(PrepareOptions options = default, CancellationToken cancellationToken = default) =>
            repository.Query().PrepareBuffered(options, cancellationToken);

        /// <summary>Prepares the query for repeated execution that streams result rows.</summary>
        public IPreparedStreamingQueryCommand<TEntity> PrepareStreaming(PrepareOptions options = default, CancellationToken cancellationToken = default) =>
            repository.Query().PrepareStreaming(options, cancellationToken);

        /// <summary>Writes the query results to a stream as CSV.</summary>
        public void WriteCsv(Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default) =>
            repository.Query().WriteCsv(destination, options, cancellationToken);

        /// <summary>Asynchronously writes the query results to a stream as CSV.</summary>
        public Task WriteCsvAsync(Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default) =>
            repository.Query().WriteCsvAsync(destination, options, cancellationToken);

        /// <summary>Writes the query results to a stream as JSON.</summary>
        public void WriteJson(Stream destination) => repository.Query().WriteJson(destination);

        /// <summary>Writes the query results to a stream as JSON using the given options.</summary>
        public void WriteJson(Stream destination, JsonStreamOptions options) =>
            repository.Query().WriteJson(destination, options);

        /// <summary>Writes the query results to a stream as JSON with cancellation support.</summary>
        public void WriteJson(Stream stream, JsonStreamOptions options, CancellationToken cancellationToken) =>
            repository.Query().WriteJson(stream, options, cancellationToken);

        /// <summary>Asynchronously writes the query results to a stream as JSON.</summary>
        public Task WriteJsonAsync(Stream destination) => repository.Query().WriteJsonAsync(destination);

        /// <summary>Asynchronously writes the query results to a stream as JSON with cancellation support.</summary>
        public Task WriteJsonAsync(Stream destination, CancellationToken cancellationToken) =>
            repository.Query().WriteJsonAsync(destination, cancellationToken);

        /// <summary>Asynchronously writes the query results to a stream as JSON using the given options.</summary>
        public Task WriteJsonAsync(Stream destination, JsonStreamOptions options, CancellationToken cancellationToken = default) =>
            repository.Query().WriteJsonAsync(destination, options, cancellationToken);

        /// <summary>Computes the minimum value of the selected expression.</summary>
        public TResult? Min<TResult>(Expression<Func<TEntity, TResult>> exp) => repository.Query().Min(exp);

        /// <summary>Asynchronously computes the minimum value of the selected expression.</summary>
        public Task<TResult?> MinAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken = default) =>
            repository.Query().MinAsync(exp, cancellationToken);

        /// <summary>Computes the maximum value of the selected expression.</summary>
        public TResult? Max<TResult>(Expression<Func<TEntity, TResult>> exp) => repository.Query().Max(exp);

        /// <summary>Asynchronously computes the maximum value of the selected expression.</summary>
        public Task<TResult?> MaxAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken = default) =>
            repository.Query().MaxAsync(exp, cancellationToken);

        /// <summary>Computes the sum of the selected expression.</summary>
        public TResult? Sum<TResult>(Expression<Func<TEntity, TResult>> exp) => repository.Query().Sum(exp);

        /// <summary>Asynchronously computes the sum of the selected expression.</summary>
        public Task<TResult?> SumAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken = default) =>
            repository.Query().SumAsync(exp, cancellationToken);
    }
}
