using System.Reflection;

namespace nextorm.core;
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
    public object Column(string _) => string.Empty;

    public TableColumn this[string __]
    {
        get => default!;
        set => _ = value;
    }
}

public class TableColumn
{
    public int AsInt { get; }
    public string AsString { get; }
    public string? AsNullableString { get; }
}