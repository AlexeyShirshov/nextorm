namespace NextORM.Core;

/// <summary>
/// Materialization-time identity of a query execution: the provider and the resolved context
/// configuration that change the rendered SQL or the materialized result. It is folded into the
/// canonical key on top of the raw description, never read back from a prepared plan.
/// </summary>
/// <remarks>
/// The dimensions mirror what <see cref="QueryPlanEqualityComparer"/> compares on a prepared command
/// (quoting, naming convention, keyword case, command timeout) plus the provider that renders the
/// dialect and the generations that partition cache entries when the context's filters or the
/// converter/mapping configuration change.
/// </remarks>
internal readonly struct RawQueryContext : IEquatable<RawQueryContext>
{
    public RawQueryContext(
        Type providerType,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        KeywordCase keywordCase,
        int? commandTimeout,
        int filterGeneration = 0,
        int configurationGeneration = 0)
    {
        ProviderType = providerType;
        QuoteIdentifiers = quoteIdentifiers;
        NamingConvention = namingConvention;
        KeywordCase = keywordCase;
        CommandTimeout = commandTimeout;
        FilterGeneration = filterGeneration;
        ConfigurationGeneration = configurationGeneration;
    }

    /// <summary>The provider assembly type that renders the dialect.</summary>
    public Type ProviderType { get; init; }

    /// <summary>Whether identifiers are quoted.</summary>
    public bool QuoteIdentifiers { get; init; }

    /// <summary>The resolved naming convention, compared by reference exactly as the plan comparer does.</summary>
    public INamingConvention? NamingConvention { get; init; }

    /// <summary>The resolved SQL keyword case.</summary>
    public KeywordCase KeywordCase { get; init; }

    /// <summary>The resolved command timeout, or <c>null</c> when the provider default applies.</summary>
    public int? CommandTimeout { get; init; }

    /// <summary>The context's global-filter generation; a change partitions the key.</summary>
    public int FilterGeneration { get; init; }

    /// <summary>The context's converter/mapping configuration generation; a change partitions the key.</summary>
    public int ConfigurationGeneration { get; init; }

    /// <inheritdoc />
    public bool Equals(RawQueryContext other) =>
        ProviderType == other.ProviderType
        && QuoteIdentifiers == other.QuoteIdentifiers
        && ReferenceEquals(NamingConvention, other.NamingConvention)
        && KeywordCase == other.KeywordCase
        && CommandTimeout == other.CommandTimeout
        && FilterGeneration == other.FilterGeneration
        && ConfigurationGeneration == other.ConfigurationGeneration;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RawQueryContext other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ProviderType);
        hash.Add(QuoteIdentifiers);
        hash.Add(NamingConvention is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(NamingConvention));
        hash.Add((int)KeywordCase);
        hash.Add(CommandTimeout);
        hash.Add(FilterGeneration);
        hash.Add(ConfigurationGeneration);
        return hash.ToHashCode();
    }
}
