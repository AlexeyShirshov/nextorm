using System.Linq.Expressions;

namespace NextORM.Core;

public static partial class RepositoryExtensions
{
    extension<TEntity>(IRepository<TEntity> repository)
    {
        /// <summary>Computes the average of the selected values, or null when no value contributes.</summary>
        public double? Avg(Expression<Func<TEntity, byte>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, ignoring nulls.</summary>
        public double? Avg(Expression<Func<TEntity, byte?>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, or null when no value contributes.</summary>
        public double? Avg(Expression<Func<TEntity, sbyte>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, ignoring nulls.</summary>
        public double? Avg(Expression<Func<TEntity, sbyte?>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, or null when no value contributes.</summary>
        public double? Avg(Expression<Func<TEntity, short>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, ignoring nulls.</summary>
        public double? Avg(Expression<Func<TEntity, short?>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, or null when no value contributes.</summary>
        public double? Avg(Expression<Func<TEntity, ushort>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, ignoring nulls.</summary>
        public double? Avg(Expression<Func<TEntity, ushort?>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, or null when no value contributes.</summary>
        public double? Avg(Expression<Func<TEntity, int>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, ignoring nulls.</summary>
        public double? Avg(Expression<Func<TEntity, int?>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, or null when no value contributes.</summary>
        public double? Avg(Expression<Func<TEntity, uint>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, ignoring nulls.</summary>
        public double? Avg(Expression<Func<TEntity, uint?>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, or null when no value contributes.</summary>
        public double? Avg(Expression<Func<TEntity, long>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, ignoring nulls.</summary>
        public double? Avg(Expression<Func<TEntity, long?>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, or null when no value contributes.</summary>
        public double? Avg(Expression<Func<TEntity, ulong>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, ignoring nulls.</summary>
        public double? Avg(Expression<Func<TEntity, ulong?>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, or null when no value contributes.</summary>
        public float? Avg(Expression<Func<TEntity, float>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, ignoring nulls.</summary>
        public float? Avg(Expression<Func<TEntity, float?>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, or null when no value contributes.</summary>
        public double? Avg(Expression<Func<TEntity, double>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, ignoring nulls.</summary>
        public double? Avg(Expression<Func<TEntity, double?>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, or null when no value contributes.</summary>
        public decimal? Avg(Expression<Func<TEntity, decimal>> exp) => repository.Query().Avg(exp);

        /// <summary>Computes the average of the selected values, ignoring nulls.</summary>
        public decimal? Avg(Expression<Func<TEntity, decimal?>> exp) => repository.Query().Avg(exp);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, byte>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, byte?>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, sbyte>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, sbyte?>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, short>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, short?>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, ushort>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, ushort?>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, int>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, int?>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, uint>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, uint?>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, long>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, long?>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, ulong>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, ulong?>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<float?> AvgAsync(Expression<Func<TEntity, float>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<float?> AvgAsync(Expression<Func<TEntity, float?>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, double>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, double?>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<decimal?> AvgAsync(Expression<Func<TEntity, decimal>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<decimal?> AvgAsync(Expression<Func<TEntity, decimal?>> exp, CancellationToken cancellationToken) =>
            repository.Query().AvgAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, byte>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, byte?>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, sbyte>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, sbyte?>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, short>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, short?>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, ushort>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, ushort?>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, int>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, int?>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, uint>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, uint?>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, long>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, long?>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, ulong>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, ulong?>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<float?> AvgAsync(Expression<Func<TEntity, float>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<float?> AvgAsync(Expression<Func<TEntity, float?>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, double>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<double?> AvgAsync(Expression<Func<TEntity, double?>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, or null when no value contributes.</summary>
        public Task<decimal?> AvgAsync(Expression<Func<TEntity, decimal>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Asynchronously computes the average of the selected values, ignoring nulls.</summary>
        public Task<decimal?> AvgAsync(Expression<Func<TEntity, decimal?>> exp) => repository.Query().AvgAsync(exp);

        /// <summary>Computes the sample standard deviation, or null for fewer than two values.</summary>
        public TResult? Stdev<TResult>(Expression<Func<TEntity, TResult>> exp) where TResult : struct =>
            repository.Query().Stdev(exp);

        /// <summary>Computes the sample standard deviation, ignoring nulls.</summary>
        public TResult? Stdev<TResult>(Expression<Func<TEntity, TResult?>> exp) where TResult : struct =>
            repository.Query().Stdev(exp);

        /// <summary>Asynchronously computes the sample standard deviation, or null for fewer than two values.</summary>
        public Task<TResult?> StdevAsync<TResult>(Expression<Func<TEntity, TResult>> exp) where TResult : struct =>
            repository.Query().StdevAsync(exp);

        /// <summary>Asynchronously computes the sample standard deviation, ignoring nulls.</summary>
        public Task<TResult?> StdevAsync<TResult>(Expression<Func<TEntity, TResult?>> exp) where TResult : struct =>
            repository.Query().StdevAsync(exp);

        /// <summary>Asynchronously computes the sample standard deviation, or null for fewer than two values.</summary>
        public Task<TResult?> StdevAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken) where TResult : struct =>
            repository.Query().StdevAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the sample standard deviation, ignoring nulls.</summary>
        public Task<TResult?> StdevAsync<TResult>(Expression<Func<TEntity, TResult?>> exp, CancellationToken cancellationToken) where TResult : struct =>
            repository.Query().StdevAsync(exp, cancellationToken);

        /// <summary>Computes the population standard deviation, or null when no value contributes.</summary>
        public TResult? Stdevp<TResult>(Expression<Func<TEntity, TResult>> exp) where TResult : struct =>
            repository.Query().Stdevp(exp);

        /// <summary>Computes the population standard deviation, ignoring nulls.</summary>
        public TResult? Stdevp<TResult>(Expression<Func<TEntity, TResult?>> exp) where TResult : struct =>
            repository.Query().Stdevp(exp);

        /// <summary>Asynchronously computes the population standard deviation, or null when no value contributes.</summary>
        public Task<TResult?> StdevpAsync<TResult>(Expression<Func<TEntity, TResult>> exp) where TResult : struct =>
            repository.Query().StdevpAsync(exp);

        /// <summary>Asynchronously computes the population standard deviation, ignoring nulls.</summary>
        public Task<TResult?> StdevpAsync<TResult>(Expression<Func<TEntity, TResult?>> exp) where TResult : struct =>
            repository.Query().StdevpAsync(exp);

        /// <summary>Asynchronously computes the population standard deviation, or null when no value contributes.</summary>
        public Task<TResult?> StdevpAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken) where TResult : struct =>
            repository.Query().StdevpAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the population standard deviation, ignoring nulls.</summary>
        public Task<TResult?> StdevpAsync<TResult>(Expression<Func<TEntity, TResult?>> exp, CancellationToken cancellationToken) where TResult : struct =>
            repository.Query().StdevpAsync(exp, cancellationToken);

        /// <summary>Computes the sample variance, or null for fewer than two values.</summary>
        public TResult? Var<TResult>(Expression<Func<TEntity, TResult>> exp) where TResult : struct =>
            repository.Query().Var(exp);

        /// <summary>Computes the sample variance, ignoring nulls.</summary>
        public TResult? Var<TResult>(Expression<Func<TEntity, TResult?>> exp) where TResult : struct =>
            repository.Query().Var(exp);

        /// <summary>Asynchronously computes the sample variance, or null for fewer than two values.</summary>
        public Task<TResult?> VarAsync<TResult>(Expression<Func<TEntity, TResult>> exp) where TResult : struct =>
            repository.Query().VarAsync(exp);

        /// <summary>Asynchronously computes the sample variance, ignoring nulls.</summary>
        public Task<TResult?> VarAsync<TResult>(Expression<Func<TEntity, TResult?>> exp) where TResult : struct =>
            repository.Query().VarAsync(exp);

        /// <summary>Asynchronously computes the sample variance, or null for fewer than two values.</summary>
        public Task<TResult?> VarAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken) where TResult : struct =>
            repository.Query().VarAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the sample variance, ignoring nulls.</summary>
        public Task<TResult?> VarAsync<TResult>(Expression<Func<TEntity, TResult?>> exp, CancellationToken cancellationToken) where TResult : struct =>
            repository.Query().VarAsync(exp, cancellationToken);

        /// <summary>Computes the population variance, or null when no value contributes.</summary>
        public TResult? Varp<TResult>(Expression<Func<TEntity, TResult>> exp) where TResult : struct =>
            repository.Query().Varp(exp);

        /// <summary>Computes the population variance, ignoring nulls.</summary>
        public TResult? Varp<TResult>(Expression<Func<TEntity, TResult?>> exp) where TResult : struct =>
            repository.Query().Varp(exp);

        /// <summary>Asynchronously computes the population variance, or null when no value contributes.</summary>
        public Task<TResult?> VarpAsync<TResult>(Expression<Func<TEntity, TResult>> exp) where TResult : struct =>
            repository.Query().VarpAsync(exp);

        /// <summary>Asynchronously computes the population variance, ignoring nulls.</summary>
        public Task<TResult?> VarpAsync<TResult>(Expression<Func<TEntity, TResult?>> exp) where TResult : struct =>
            repository.Query().VarpAsync(exp);

        /// <summary>Asynchronously computes the population variance, or null when no value contributes.</summary>
        public Task<TResult?> VarpAsync<TResult>(Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken) where TResult : struct =>
            repository.Query().VarpAsync(exp, cancellationToken);

        /// <summary>Asynchronously computes the population variance, ignoring nulls.</summary>
        public Task<TResult?> VarpAsync<TResult>(Expression<Func<TEntity, TResult?>> exp, CancellationToken cancellationToken) where TResult : struct =>
            repository.Query().VarpAsync(exp, cancellationToken);
    }
}
