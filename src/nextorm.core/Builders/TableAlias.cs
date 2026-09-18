using System.Reflection;

namespace nextorm.core;
/// <summary>
/// Column accessors for a table addressed by name instead of by a mapped entity type (the
/// <c>From("table")</c> mode).
/// </summary>
/// <remarks>
/// The members are named after CLR types (<c>Int</c>, <c>Long</c>, <c>Boolean</c>) rather than verbs,
/// mix C# aliases with CLR names (<c>Int</c> vs <c>Boolean</c>), and each takes an unnamed
/// <c>string _</c> parameter so named arguments are impossible. Recommended: verb-style accessors
/// such as <c>GetInt32</c> with a meaningful parameter name.
/// See <c>API-NAMING-REVIEW.md</c> finding P0-9.
/// </remarks>
public class TableAlias
{
    public int Int(string _) => 0;
    public long Long(string _) => 0;
    public short Short(string _) => 0;
    public string String(string _) => string.Empty;
    public float Float(string _) => 0;
    public double Double(string _) => 0;
    public DateTime DateTime(string _) => System.DateTime.MinValue;
    public decimal Decimal(string _) => 0;
    public byte Byte(string _) => 0;
    public bool Boolean(string _) => false;
    public Guid Guid(string _) => System.Guid.Empty;
    public byte[] Bytes(string _) => [];
    public int? NullableInt(string _) => 0;
    public long? NullableLong(string _) => 0;
    public short? NullableShort(string _) => 0;
    public string? NullableString(string _) => string.Empty;
    public float? NullableFloat(string _) => 0;
    public double? NullableDouble(string _) => 0;
    public DateTime? NullableDateTime(string _) => System.DateTime.MinValue;
    public decimal? NullableDecimal(string _) => 0;
    public byte? NullableByte(string _) => 0;
    public bool? NullableBoolean(string _) => false;
    public Guid? NullableGuid(string _) => System.Guid.Empty;
    public byte[]? NullableBytes(string _) => null;
    public object Column(string _) => string.Empty;

    public TableColumn this[string __]
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