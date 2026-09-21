using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// The <c>DISTINCT ON (expr, ...)</c> modifier (PostgreSQL) attached to a query through
/// <c>EntityBuilder.DistinctOn</c>. The expression may be a single column or an anonymous type to key
/// on several columns.
/// </summary>
internal sealed record DistinctOnClause(LambdaExpression Expression);
