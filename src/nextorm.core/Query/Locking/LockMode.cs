namespace NextORM.Core;

/// <summary>Row-locking strength for a trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> clause.</summary>
public enum LockMode
{
    /// <summary>Exclusive row lock (<c>FOR UPDATE</c>).</summary>
    Update,
    /// <summary>Shared row lock (<c>FOR SHARE</c>, rendered as <c>LOCK IN SHARE MODE</c> on MySQL/MariaDB).</summary>
    Share
}
