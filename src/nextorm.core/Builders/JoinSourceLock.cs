namespace NextORM.Core;

/// <summary>
/// Carries a per-join source-level lock onto the joined source. Every join surface funnels through
/// <see cref="Apply"/>, so <c>JoinOptions.ForUpdate</c>/<c>JoinOptions.ForShare</c> is
/// never silently dropped: preparation rejects a lock whose source is not a physical table
/// (APPLY/derived/CTE) and SQL assembly rejects a dialect without source locks.
/// </summary>
internal static class JoinSourceLock
{
    public static FromExpression Apply(FromExpression source, JoinOptions options)
        => options.SourceLock is null ? source : source.WithSourceLock(options.SourceLock);
}
