using System.Data;

namespace NextORM.Core;

/// <summary>
/// Value-free metadata describing one parameter of a <see cref="RawReaderDescriptor"/>. It carries
/// the provider type, size, precision and scale needed to mint a fresh <see cref="System.Data.Common.DbParameter"/> on
/// every execution, but never a value, a captured closure or a mutable parameter object.
/// </summary>
internal readonly struct RawParameterSpec
{
    public string Name { get; init; }

    public int Position { get; init; }

    /// <summary>CLR provider type token, when known; <see cref="DbType"/> is the authoritative discriminator.</summary>
    public Type ProviderType { get; init; }

    public DbType DbType { get; init; }

    public int Size { get; init; }

    public int Precision { get; init; }

    public int Scale { get; init; }

    public ParameterDirection Direction { get; init; }
}
