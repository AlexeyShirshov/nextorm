using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// A <c>LIMIT n BY expr</c> request (ClickHouse): at most <see cref="Limit"/> rows per distinct value of
/// <see cref="Expression"/>, optionally skipping <see cref="Offset"/> rows per group. Carried from the
/// builder to the command and rendered by the dialect; it is an implementation detail of the
/// <c>EntityBuilder.LimitBy</c> method.
/// </summary>
internal sealed record LimitByClause(LambdaExpression Expression, int Limit, int Offset);
