using System.Collections;
using System.Data;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// Unit coverage for the SQL Server CSV typed-column hook
/// (<see cref="SqlServerDataContext.MapTypedColumnExpression"/>): a numeric column is read with the
/// typed getter of its actual storage type and converted with the matching static
/// <c>Convert.To&lt;Target&gt;(storage)</c> overload, so the per-row path never calls
/// <c>GetValue</c>/<c>ChangeType</c>. These tests never open a connection.
/// </summary>
public class CsvTypedColumnMappingTests
{
    private static readonly ParameterExpression Record = Expression.Parameter(typeof(IDataRecord), "record");

    public static TheoryData<Type, Type, object, object> NumericCases() => new()
    {
        { typeof(long), typeof(int), 42L, 42 },
        { typeof(decimal), typeof(int), 42m, 42 },
        { typeof(decimal), typeof(long), 42m, 42L },
        { typeof(int), typeof(int), 42, 42 },
        { typeof(long), typeof(int?), 7L, 7 },
        { typeof(decimal), typeof(decimal?), 1.25m, 1.25m },
    };

    [Theory]
    [MemberData(nameof(NumericCases))]
    public void NumericColumn_ReadsStorageTypedAndConverts(Type storage, Type projected, object value, object expected)
    {
        var column = new SelectExpression(projected) { Index = 0, PropertyName = "V" };
        var body = SqlServerTestContext.CreateCsvHook().MapTypedColumnExpression(column, Record, storage);

        var text = body.ToString();
        text.Should().NotContain(nameof(IDataRecord.GetValue)).And.NotContain(nameof(Convert.ChangeType));

        Invoke(body, value, storage).Should().Be(expected);
    }

    [Fact]
    public void NullableNumericColumn_Null_ReturnsNullWithoutReadingTheGetter()
    {
        var column = new SelectExpression(typeof(int?)) { Index = 0, PropertyName = "V" };
        var body = SqlServerTestContext.CreateCsvHook().MapTypedColumnExpression(column, Record, typeof(long));

        Invoke(body, null, typeof(long)).Should().BeNull();
    }

    [Fact]
    public void DefaultOnNullNumericColumn_Null_ReturnsDefault()
    {
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "V" };
        // DefaultOnNull is set internally by the *OrDefault scalar terminals; the property setter is
        // internal to nextorm.core, so drive it from the test via reflection.
        typeof(SelectExpression).GetProperty(nameof(SelectExpression.DefaultOnNull))!.SetValue(column, true);

        var body = SqlServerTestContext.CreateCsvHook().MapTypedColumnExpression(column, Record, typeof(long));

        Invoke(body, null, typeof(long)).Should().Be(0);
    }

    [Fact]
    public void ConverterColumn_IsNotBypassedByNumericWidening()
    {
        var column = new SelectExpression(typeof(long))
        {
            Index = 0,
            PropertyName = "V",
            Converter = new IntToLongConverter(),
        };
        var body = SqlServerTestContext.CreateCsvHook().MapTypedColumnExpression(column, Record, typeof(long));

        body.ToString().Should().Contain(nameof(IPropertyValueConverter.ConvertFromProvider));
        Invoke(body, 42, typeof(int)).Should().Be(42L);
    }

    [Fact]
    public void UnsupportedStorageType_ThrowsNotSupportedBeforeAnyRow()
    {
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "V" };

        var act = () => SqlServerTestContext.CreateCsvHook().MapTypedColumnExpression(column, Record, typeof(bool));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*'V'*")
            .WithMessage("*Boolean*");
    }

    [Fact]
    public void BinaryColumn_OnSequentialReader_AdmitsBoundedChunking()
    {
        // byte[] is not numeric, so the SQL Server typed hook delegates to the base direct
        // GetFieldValue<byte[]> read. On a confirmed sequential reader the CSV plan must switch to the
        // bounded chunked path (Write is null, BinaryOrdinal is set); a buffered reader must keep the
        // whole-array getter compiled.
        var hook = SqlServerTestContext.CreateCsvHook();
        // FIX 2: the no-Expression mapper seam is admitted only on positive stored-column evidence, so the
        // hand-built probe must carry the mapping-derived physical column name of a real entity column.
        var column = new SelectExpression(typeof(byte[])) { Index = 0, PropertyName = "Data", PhysicalColumnName = "data" };
        var schema = new NumericRecord(null, typeof(byte[]));
        var options = new CsvStreamOptions();

        var sequential = CsvStreamWriter.Build(
            [column],
            schema,
            (c, r, storage) => hook.MapTypedColumnExpression(c, r, storage),
            sequentialAccess: true,
            options);
        sequential.Columns[0].BinaryOrdinal.Should().Be(0);
        sequential.Columns[0].Write.Should().BeNull("the chunked path must not compile a whole-field getter");

        var buffered = CsvStreamWriter.Build(
            [column],
            schema,
            (c, r, storage) => hook.MapTypedColumnExpression(c, r, storage),
            sequentialAccess: false,
            options);
        buffered.Columns[0].BinaryOrdinal.Should().BeNull();
        buffered.Columns[0].Write.Should().NotBeNull();
    }

    private static object? Invoke(Expression body, object? value, Type storage)
    {
        var lambda = Expression.Lambda(body, Record).Compile();
        return lambda.DynamicInvoke(new NumericRecord(value, storage));
    }

    /// <summary>An <see cref="IPropertyValueConverter"/> whose provider type differs from the model type,
    /// so the numeric hook must delegate to the converter instead of widening the column itself.</summary>
    private sealed class IntToLongConverter : IPropertyValueConverter
    {
        public Type ProviderType => typeof(int);

        public bool ConvertsNulls => false;

        public object? ConvertToProvider(object? model) => model is long value ? (int)value : model;

        public object? ConvertFromProvider(object? provider) => provider is int value ? (long)value : 0L;
    }

    /// <summary>A one-column <see cref="IDataRecord"/> that serves only the typed getter the hook reads;
    /// <c>GetValue</c> throws so any object-based fallback fails the test.</summary>
    private sealed class NumericRecord(object? value, Type storage) : IDataRecord
    {
        public int FieldCount => 1;

        public object this[int i] => throw new NotSupportedException();

        public object this[string name] => throw new NotSupportedException();

        public bool GetBoolean(int i) => (bool)value!;

        public byte GetByte(int i) => (byte)value!;

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public string GetDataTypeName(int i) => storage.Name;

        public DateTime GetDateTime(int i) => (DateTime)value!;

        public decimal GetDecimal(int i) => (decimal)value!;

        public double GetDouble(int i) => (double)value!;

        public Type GetFieldType(int i) => storage;

        public float GetFloat(int i) => (float)value!;

        public Guid GetGuid(int i) => (Guid)value!;

        public short GetInt16(int i) => (short)value!;

        public int GetInt32(int i) => (int)value!;

        public long GetInt64(int i) => (long)value!;

        public string GetName(int i) => "V";

        public int GetOrdinal(string name) => 0;

        public string GetString(int i) => (string)value!;

        public object GetValue(int i) => throw new NotSupportedException("GetValue must not be called on the CSV path.");

        public int GetValues(object[] values) => throw new NotSupportedException();

        public bool IsDBNull(int i) => value is null or DBNull;

        public T GetFieldValue<T>(int i) => (T)value!;

        public IEnumerator GetEnumerator() => throw new NotSupportedException();
    }
}
