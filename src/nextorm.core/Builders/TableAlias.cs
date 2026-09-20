using System.Reflection;

namespace NextORM.Core;
/// <summary>
/// Column accessors for a table addressed by name instead of by a mapped entity type (the
/// <c>From("table")</c> mode).
/// </summary>
public class TableAlias
{
    public int GetInt32(string columnName) => 0;
    public long GetInt64(string columnName) => 0;
    public short GetInt16(string columnName) => 0;
    public string GetString(string columnName) => string.Empty;
    public float GetSingle(string columnName) => 0;
    public double GetDouble(string columnName) => 0;
    public DateTime GetDateTime(string columnName) => System.DateTime.MinValue;
    public decimal GetDecimal(string columnName) => 0;
    public byte GetByte(string columnName) => 0;
    public bool GetBoolean(string columnName) => false;
    public Guid GetGuid(string columnName) => System.Guid.Empty;
    public byte[] GetBytes(string columnName) => [];
    public int? GetNullableInt32(string columnName) => 0;
    public long? GetNullableInt64(string columnName) => 0;
    public short? GetNullableInt16(string columnName) => 0;
    public string? GetNullableString(string columnName) => string.Empty;
    public float? GetNullableSingle(string columnName) => 0;
    public double? GetNullableDouble(string columnName) => 0;
    public DateTime? GetNullableDateTime(string columnName) => System.DateTime.MinValue;
    public decimal? GetNullableDecimal(string columnName) => 0;
    public byte? GetNullableByte(string columnName) => 0;
    public bool? GetNullableBoolean(string columnName) => false;
    public Guid? GetNullableGuid(string columnName) => System.Guid.Empty;
    public byte[]? GetNullableBytes(string columnName) => null;
    public object GetColumn(string columnName) => string.Empty;

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
    public int AsInt { get; }
    public string AsString { get; } = null!;
    public string? AsNullableString { get; }
    public byte[] AsBytes { get; } = null!;
    public byte[]? AsNullableBytes { get; }
}
