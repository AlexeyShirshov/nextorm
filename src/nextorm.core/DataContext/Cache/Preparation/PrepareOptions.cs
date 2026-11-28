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
/// Options for <c>PrepareBuffered</c> and <c>PrepareStreaming</c>.
/// </summary>
public readonly record struct PrepareOptions
{
    /// <summary>
    /// How the prepared command caches and reuses its underlying command; defaults to
    /// <see cref="PreparedCommandCache.Connection"/>.
    /// </summary>
    public PreparedCommandCache Cache { get; init; }

    internal static void Validate(PrepareOptions options)
    {
        if (!Enum.IsDefined(options.Cache))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Cache,
                "The prepared-command cache policy is not a defined PreparedCommandCache value; use PreparedCommandCache.Connection or PreparedCommandCache.None.");
        }
    }
}
