namespace NextORM.Core;

/// <summary>How a row lock behaves when the requested row is already locked by another transaction.</summary>
public enum LockWaitMode
{
    /// <summary>Block until the lock can be acquired (the default; no wait-mode suffix is emitted).</summary>
    Wait,
    /// <summary>Fail immediately instead of waiting (<c>NOWAIT</c>; SQL Server <c>NOWAIT</c> table hint).</summary>
    NoWait,
    /// <summary>Skip rows locked by other transactions instead of waiting (<c>SKIP LOCKED</c>; SQL Server <c>READPAST</c>).</summary>
    SkipLocked
}
