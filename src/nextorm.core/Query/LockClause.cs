namespace NextORM.Core;

/// <summary>
/// The row-locking clause attached to a query through <c>EntityBuilder.ForUpdate</c>/<c>EntityBuilder.ForShare</c>.
/// It renders as a trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> clause, or as a table hint on the primary
/// source when the dialect sets <c>ILockRenderer.UsesTableHints</c> (SQL Server).
/// </summary>
internal sealed record LockClause(LockMode Mode, LockWaitMode Wait = LockWaitMode.Wait);
