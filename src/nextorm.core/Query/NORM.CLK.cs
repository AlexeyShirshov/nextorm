using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// ClickHouse-only SQL surface: the <c>argMin</c>/<c>argMax</c> aggregates and the <c>-If</c>
/// aggregate combinator. Exposed through <see cref="NORM.CLK_SQL"/>; every member is gated by a
/// capability flag and rejected by providers that do not opt in.
/// </summary>
public static partial class NORM
{
    /// <summary>ClickHouse-only functions; see <see cref="NORM.CLK_SQL"/>.</summary>
    public class CLK : NORM_SQL
    {
        /// <summary>
        /// <c>argMin(value, by)</c>: the <paramref name="value"/> of the row where <paramref name="by"/>
        /// is the smallest. Requires a provider that supports it (see
        /// <see cref="ISqlDialect.SupportsArgMinMax"/>; ClickHouse).
        /// </summary>
        public TValue? arg_min<TValue, TBy>(TValue? value, TBy? by) => default!;

        /// <summary><c>argMax(value, by)</c>: the <paramref name="value"/> of the row where <paramref name="by"/> is the largest.</summary>
        public TValue? arg_max<TValue, TBy>(TValue? value, TBy? by) => default!;

        /// <summary>
        /// <c>countIf(predicate)</c>: the number of rows for which the predicate is true. Requires a
        /// provider that renders the <c>-If</c> combinator (see
        /// <see cref="ISqlDialect.SupportsIfAggregates"/>; ClickHouse).
        /// </summary>
        public int count_if(Expression<Func<bool>> filter) => default!;

        /// <summary><c>sumIf(value, predicate)</c>: the sum of <paramref name="value"/> over the rows for which the predicate is true.</summary>
        public T? sum_if<T>(T? value, Expression<Func<bool>> filter) => default!;

        /// <summary><c>avgIf(value, predicate)</c>: the average of <paramref name="value"/> over the rows for which the predicate is true.</summary>
        public T? avg_if<T>(T? value, Expression<Func<bool>> filter) => default!;

        /// <summary><c>minIf(value, predicate)</c>: the minimum of <paramref name="value"/> over the rows for which the predicate is true.</summary>
        public T? min_if<T>(T? value, Expression<Func<bool>> filter) => default!;

        /// <summary><c>maxIf(value, predicate)</c>: the maximum of <paramref name="value"/> over the rows for which the predicate is true.</summary>
        public T? max_if<T>(T? value, Expression<Func<bool>> filter) => default!;
    }
}
