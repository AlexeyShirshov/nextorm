namespace NextORM.Core;

/// <summary>
/// Declares how a <see cref="System.TimeSpan"/> property is stored. On a provider with a native
/// duration type (PostgreSQL <c>interval</c>, MySQL/MariaDB <c>TIME</c>) the unit is ignored and the
/// value is stored natively; on a provider without one (SQL Server, SQLite, ClickHouse) the value is
/// stored in an integer column in <see cref="Unit"/>, defaulting to <see cref="DurationUnit.Ticks"/>.
/// </summary>
/// <remarks>
/// The attribute is read by <see cref="IPropertyMetadata"/> (<see cref="IPropertyMetadata.DurationUnit"/>)
/// and by the dialect's <c>MakeDurationType</c> hook. The concrete member is
/// <see cref="System.TimeSpan"/>; the equivalent fluent mapping is
/// <see cref="EntityPropertyBuilder{T}.Duration(DurationUnit, int)"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DurationAttribute : Attribute
{
    /// <summary>
    /// Creates an attribute that stores the value in <see cref="DurationUnit.Ticks"/>.
    /// </summary>
    public DurationAttribute()
        : this(DurationUnit.Ticks)
    {
    }

    /// <summary>
    /// Creates an attribute that stores the value in <paramref name="unit"/>.
    /// </summary>
    /// <param name="unit">The unit of the stored integer value on providers without a native duration type.</param>
    public DurationAttribute(DurationUnit unit)
    {
        Unit = unit;
    }

    /// <summary>
    /// The unit of the stored integer value on providers without a native duration type.
    /// </summary>
    public DurationUnit Unit { get; set; }

    /// <summary>
    /// The precision of the native duration type (fractional seconds), for example
    /// <c>TIME(3)</c> or <c>interval(3)</c>. Zero means the provider default.
    /// </summary>
    public int Precision { get; set; }
}
