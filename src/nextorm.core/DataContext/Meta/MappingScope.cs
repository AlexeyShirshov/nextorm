namespace NextORM.Core;

/// <summary>Identifies a runtime mapping variant. The default value preserves the unscoped mapping.</summary>
public readonly record struct MappingScope(string? Key)
{
    /// <summary>The existing, unmodified mapping.</summary>
    public static readonly MappingScope Default = new(null);

    /// <summary>Prefix for generated parameter names.</summary>
    public string ParameterPrefix => string.IsNullOrEmpty(Key) ? string.Empty : string.Concat(Key, "_");
}
