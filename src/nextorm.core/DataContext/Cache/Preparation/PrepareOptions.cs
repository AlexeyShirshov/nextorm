namespace NextORM.Core;

/// <summary>
/// Selects how a prepared query command owns its underlying ADO.NET command across executions.
/// </summary>
public enum PreparedCommandCache
{
    /// <summary>
    /// One command is reused per connection: successive executions on the same connection reuse the same
    /// underlying command, while a different connection gets its own. This is the default.
    /// </summary>
    Connection = 0,
    /// <summary>
    /// A fresh command is created for every execution and disposed when that execution ends; nothing is reused.
    /// </summary>
    None = 1,
}

/// <summary>
/// Options controlling how a prepared query command is built by
/// <c>QueryCommand&lt;TResult&gt;.Prepare</c> and <c>EntityBuilderExtensions.Prepare</c>.
/// <para>
/// The default value (<c>default(PrepareOptions)</c>) prepares a buffered command whose underlying command is
/// reused per connection, which is the behavior of the parameterless preparation.
/// </para>
/// </summary>
public readonly record struct PrepareOptions
{
    /// <summary>
    /// How the prepared command caches and reuses its underlying command; defaults to
    /// <see cref="PreparedCommandCache.Connection"/>.
    /// </summary>
    public PreparedCommandCache Cache { get; init; }

    /// <summary>
    /// When <see langword="true"/>, prepares a streaming command and creates the row enumerator as part of
    /// preparation; when <see langword="false"/> (the default), prepares a buffered command that needs no
    /// enumerator. This is the positive form of the removed <c>nonStreamUsing</c> boolean.
    /// </summary>
    public bool Streaming { get; init; }
}
