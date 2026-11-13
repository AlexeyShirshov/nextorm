namespace NextORM.Core;

/// <summary>Prefixes names allocated by another parameter provider.</summary>
/// <remarks>
/// The prefix must come from a trusted mapping configuration; parameter identifiers are not SQL values.
/// </remarks>
public sealed class PrefixedParameterProvider : IParameterProvider
{
    private readonly IParameterProvider _inner;
    private readonly string _prefix;

    /// <summary>Creates a provider that adds <paramref name="prefix"/> to each generated name.</summary>
    /// <param name="inner">The provider that allocates the unprefixed names.</param>
    /// <param name="prefix">The trusted prefix to prepend.</param>
    public PrefixedParameterProvider(IParameterProvider inner, string prefix)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(prefix);
        _inner = inner;
        _prefix = prefix;
    }

    /// <inheritdoc />
    public string GetParamName() => string.Concat(_prefix, _inner.GetParamName());
}
