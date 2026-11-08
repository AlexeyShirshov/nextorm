namespace NextORM.Integration.Tests;

/// <summary>
/// Policy for providers that cannot be started. The suite is fail-closed: an unavailable provider is
/// an error, because a missing container must never turn into a silently green run. The only way to
/// skip a provider is an explicit opt-out through <see cref="EnvironmentVariable"/>.
/// </summary>
internal static class ProviderSkipPolicy
{
    /// <summary>
    /// Comma or semicolon separated provider names (<c>sqlite</c>, <c>postgres</c>, <c>sqlserver</c>,
    /// <c>mysql</c>, <c>clickhouse</c>) that are allowed to be skipped when unavailable, e.g.
    /// <c>NEXTORM_SKIP_PROVIDERS=postgres,sqlserver</c>.
    /// </summary>
    public const string EnvironmentVariable = "NEXTORM_SKIP_PROVIDERS";

    public static bool IsOptedOut(string providerName)
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (var part in value.Split([',', ';'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(part, providerName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
