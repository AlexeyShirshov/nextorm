namespace NextORM.Core;

/// <summary>
/// The trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> row-locking clause attached to a query through
/// <c>EntityBuilder.ForUpdate</c>/<c>EntityBuilder.ForShare</c>.
/// </summary>
internal sealed record LockClause(LockMode Mode);
