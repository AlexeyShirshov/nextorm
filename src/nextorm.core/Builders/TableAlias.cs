using System.Reflection;

namespace NextORM.Core;
/// <summary>
/// Column accessors for a table addressed by name instead of by a mapped entity type (the
/// <c>From("table")</c> mode).
/// </summary>
public class TableAlias
{
    /// <summary>Reads <paramref name="columnName"/> as a 32-bit integer in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public int GetInt32(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a 64-bit integer in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public long GetInt64(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a 16-bit integer in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public short GetInt16(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a string in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public string GetString(string columnName) => string.Empty;
    /// <summary>Reads <paramref name="columnName"/> as a single-precision number in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public float GetSingle(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a double-precision number in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public double GetDouble(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a date/time value in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public DateTime GetDateTime(string columnName) => System.DateTime.MinValue;
    /// <summary>Reads <paramref name="columnName"/> as a decimal value in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public decimal GetDecimal(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as an 8-bit unsigned integer in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public byte GetByte(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a Boolean in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public bool GetBoolean(string columnName) => false;
    /// <summary>Reads <paramref name="columnName"/> as a GUID in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public Guid GetGuid(string columnName) => System.Guid.Empty;
    /// <summary>Reads <paramref name="columnName"/> as a byte array in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public byte[] GetBytes(string columnName) => [];
    /// <summary>Reads <paramref name="columnName"/> as a nullable 32-bit integer (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public int? GetNullableInt32(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a nullable 64-bit integer (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public long? GetNullableInt64(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a nullable 16-bit integer (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public short? GetNullableInt16(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a nullable string (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public string? GetNullableString(string columnName) => string.Empty;
    /// <summary>Reads <paramref name="columnName"/> as a nullable single-precision number (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public float? GetNullableSingle(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a nullable double-precision number (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public double? GetNullableDouble(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a nullable date/time value (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public DateTime? GetNullableDateTime(string columnName) => System.DateTime.MinValue;
    /// <summary>Reads <paramref name="columnName"/> as a nullable decimal value (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public decimal? GetNullableDecimal(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a nullable 8-bit unsigned integer (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public byte? GetNullableByte(string columnName) => 0;
    /// <summary>Reads <paramref name="columnName"/> as a nullable Boolean (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public bool? GetNullableBoolean(string columnName) => false;
    /// <summary>Reads <paramref name="columnName"/> as a nullable GUID (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public Guid? GetNullableGuid(string columnName) => System.Guid.Empty;
    /// <summary>Reads <paramref name="columnName"/> as a nullable byte array (<c>null</c> for a NULL value).</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public byte[]? GetNullableBytes(string columnName) => null;
    /// <summary>Reads <paramref name="columnName"/> as an untyped object in a query expression.</summary>
    /// <param name="columnName">Name of the column to read.</param>
    public object GetColumn(string columnName) => string.Empty;

    /// <summary>
    /// Gets the strongly typed accessor for <paramref name="columnName"/>, or sets a value on the
    /// column. Like the other members it is only meaningful inside a query expression.
    /// </summary>
    /// <param name="columnName">Name of the column to read or write.</param>
    public TableColumn this[string columnName]
    {
        get => default!;
        set => _ = value;
    }
}

/// <summary>
/// A typed value read from a named column of an untyped table source.
/// </summary>
public class TableColumn
{
    /// <summary>The column value read as a 32-bit integer.</summary>
    public int AsInt { get; }
    /// <summary>The column value read as a string.</summary>
    public string AsString { get; } = null!;
    /// <summary>The column value read as a nullable string (<c>null</c> for a NULL value).</summary>
    public string? AsNullableString { get; }
    /// <summary>The column value read as a byte array.</summary>
    public byte[] AsBytes { get; } = null!;
    /// <summary>The column value read as a nullable byte array (<c>null</c> for a NULL value).</summary>
    public byte[]? AsNullableBytes { get; }
}
