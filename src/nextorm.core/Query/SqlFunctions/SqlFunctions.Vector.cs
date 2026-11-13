namespace NextORM.Core;

public static partial class SqlFunctions
{
    /// <summary>
    /// Computes the distance between two vectors using the selected metric. This method is a marker
    /// intended for query expressions; the provider translates it to its native distance operation.
    /// </summary>
    /// <typeparam name="TVector">The provider-independent or provider-specific vector value type.</typeparam>
    /// <param name="left">The vector column or value being measured.</param>
    /// <param name="right">The query vector.</param>
    /// <param name="metric">The distance metric.</param>
    /// <returns>The distance, or SQL NULL when either operand is NULL.</returns>
    /// <remarks>
    /// PostgreSQL translates the metrics to pgvector's <c>&lt;=&gt;</c> cosine,
    /// <c>&lt;-&gt;</c> Euclidean, or <c>&lt;#&gt;</c> negative-inner-product operator. SQL Server 2025+
    /// translates them to <c>VECTOR_DISTANCE('cosine'|'euclidean'|'dot', ...)</c>; SQL Server's
    /// <c>dot</c> metric is the negative inner product, so smaller distances remain nearer.
    /// Providers without vector-distance support reject the call during query preparation.
    /// </remarks>
    public static double? VectorDistance<TVector>(TVector left, TVector right, VectorDistanceMetric metric)
        where TVector : IVectorValue => default!;
}
