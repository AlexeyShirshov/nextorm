namespace NextORM.Core;

/// <summary>
/// Immutable, detached metadata describing the SQL text and parameter shape of a cached
/// <c>ToDataReader</c> preparation. It never retains a value, a <see cref="System.Data.Common.DbParameter"/>, a
/// <see cref="QueryCommand"/>, a context, a connection, a reader or a <c>QueryPlan</c>: per-call
/// execution mints its own command, binds current values and owns its own reader through
/// <c>CommandReaderOwner</c>. Descriptors are cache-owned metadata, not resources, so eviction or
/// <c>Clear</c> never disposes an open reader.
/// </summary>
internal sealed class RawReaderDescriptor
{
    public string SqlText { get; }

    public RawParameterSpec[] Parameters { get; }

    public RawReaderFlavor Flavor { get; }

    public long Generation { get; }

    public RawReaderDescriptor(string sqlText, RawParameterSpec[] parameters, RawReaderFlavor flavor, long generation)
    {
        SqlText = sqlText;
        Parameters = parameters;
        Flavor = flavor;
        Generation = generation;
    }
}
