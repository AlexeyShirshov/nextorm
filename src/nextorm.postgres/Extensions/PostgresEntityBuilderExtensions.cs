using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.Postgres;

/// <summary>
/// PostgreSQL-only fluent members of <c>EntityBuilder&lt;TEntity&gt;</c>, removed from the common
/// <c>nextorm</c> package and re-exposed here as extension methods. Add
/// <c>using NextORM.Postgres;</c> to keep the fluent chain unchanged on a PostgreSQL provider.
/// The render-time dialect gating (and the thrown exceptions) is unchanged.
/// </summary>
public static class PostgresEntityBuilderExtensions
{
    /// <summary>
    /// Adds a <c>DISTINCT ON (expr, ...)</c> clause (PostgreSQL): keeps the first row of each distinct
    /// key, where <paramref name="exp"/> is a single column or an anonymous type to key on several
    /// columns. PostgreSQL requires the leading <c>ORDER BY</c> expressions to match the key. Cannot be
    /// combined with <c>Distinct</c>. Requires a dialect that supports it (see
    /// <c>ISqlDialect.DistinctOn</c>).
    /// </summary>
    /// <param name="builder">The query builder to extend.</param>
    /// <param name="exp">The key selector: a single column or an anonymous type keying on several columns.</param>
    public static EntityBuilder<TEntity> DistinctOn<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp)
    {
        ArgumentNullException.ThrowIfNull(exp);

        if (builder.IsDistinct)
            throw new InvalidOperationException("DISTINCT ON cannot be combined with DISTINCT.");

        var b = builder.Clone();
        b.DistinctOnClause = new DistinctOnClause(exp);

        return b;
    }
}
