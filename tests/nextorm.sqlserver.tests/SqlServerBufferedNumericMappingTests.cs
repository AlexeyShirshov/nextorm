using System.Collections;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// Characterization + parity coverage for the SQL Server <b>general buffered</b> numeric mapper
/// (<see cref="SqlServerDataContext.MapColumnExpression"/>). The mapper is compiled before the reader
/// is open, so it resolves the column's storage type at runtime (<see cref="IDataRecord.GetFieldType"/>)
/// and reads it with the matching typed getter, converting with the matching typed
/// <c>Convert.To&lt;Target&gt;(storage)</c> overload. The expected value is the independent
/// <see cref="Convert.ChangeType(object, Type)"/> oracle, pinning the previous semantics. The tests
/// never open a connection.
/// </summary>
public class SqlServerBufferedNumericMappingTests
{
    private static readonly ParameterExpression Record = Expression.Parameter(typeof(IDataRecord), "record");

    // The closed numeric matrix: byte, short, int, long, float, double, decimal.
    private static readonly Type[] NumericTypes =
    [
        typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal),
    ];

    /// <summary>The 49 present-value source-to-destination pairs (7 sources x 7 destinations).</summary>
    public static TheoryData<Type, Type> AllPairs()
    {
        var data = new TheoryData<Type, Type>();
        foreach (var source in NumericTypes)
            foreach (var dest in NumericTypes)
                data.Add(source, dest);
        return data;
    }

    /// <summary>
    /// The 28 null-policy cases (7 sources x {nullable-null, nullable-value, default-null, default-value}),
    /// closing the matrix at 77 cases with <see cref="AllPairs"/>.
    /// </summary>
    public static TheoryData<Type, string> NullPolicyCases()
    {
        var data = new TheoryData<Type, string>();
        foreach (var source in NumericTypes)
            foreach (var shape in new[] { "nullable-null", "nullable-value", "default-null", "default-value" })
                data.Add(source, shape);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void BufferedNumeric_MatchesChangeTypeOracle(Type source, Type dest)
    {
        var value = Sample(source);
        var expected = Convert.ChangeType(value, dest);

        var body = MapBuffered(dest);
        Invoke(body, value, source).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(NullPolicyCases))]
    public void BufferedNumeric_NullPolicy_MatchesOracle(Type source, string shape)
    {
        var value = Sample(source);

        switch (shape)
        {
            case "nullable-null":
            {
                var body = MapBuffered(NullableType(source));
                Invoke(body, null, source).Should().BeNull();
                break;
            }
            case "nullable-value":
            {
                var body = MapBuffered(NullableType(source));
                Invoke(body, value, source).Should().Be(value);
                break;
            }
            case "default-null":
            {
                var body = MapBuffered(source, defaultOnNull: true);
                Invoke(body, null, source).Should().Be(Activator.CreateInstance(source));
                break;
            }
            case "default-value":
            {
                var body = MapBuffered(source, defaultOnNull: true);
                Invoke(body, value, source).Should().Be(value);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown null-policy shape.");
        }
    }

    [Fact]
    public void BufferedNumeric_DoesNotCallGetValueForSupportedStorage()
    {
        var body = MapBuffered(typeof(long));
        // StrictNumericReader.GetValue/this[] throw; a successful read proves the typed dispatch won.
        Invoke(body, 42, typeof(int)).Should().Be(42L);
    }

    [Fact]
    public void BufferedNumeric_DispatchesOnRuntimeStorageType_UnlikeTheCsvHook()
    {
        var body = MapBuffered(typeof(long));

        // One compiled accessor adapts to two different storage types, which a build-time CSV hook cannot.
        Invoke(body, (byte)42, typeof(byte)).Should().Be(42L);
        Invoke(body, (short)42, typeof(short)).Should().Be(42L);
        Invoke(body, 42, typeof(int)).Should().Be(42L);
    }

    // --- Independent oracle characterization: pin the Convert.ChangeType semantics the mapper replaced. ---

    [Fact]
    public void Oracle_DoubleToInt_RoundsHalfToEven()
    {
        Convert.ChangeType(2.5d, typeof(int)).Should().Be(2);
        Convert.ChangeType(3.5d, typeof(int)).Should().Be(4);
    }

    [Fact]
    public void Oracle_DecimalToInt_RoundsHalfToEven()
    {
        Convert.ChangeType(2.5m, typeof(int)).Should().Be(2);
        Convert.ChangeType(3.5m, typeof(int)).Should().Be(4);
    }

    [Theory]
    [InlineData(300L, typeof(byte))]
    [InlineData(-1L, typeof(byte))]
    [InlineData(70000, typeof(short))]
    public void Oracle_IntegerOverflow_ThrowsOverflowException(long value, Type dest)
    {
        var act = () => Convert.ChangeType(value, dest);
        act.Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData(double.NaN, typeof(int))]
    [InlineData(double.PositiveInfinity, typeof(int))]
    [InlineData(double.PositiveInfinity, typeof(decimal))]
    [InlineData(1e30, typeof(decimal))]
    public void Oracle_NaNInfinityOverflow_ThrowsOverflowException(object value, Type dest)
    {
        var act = () => Convert.ChangeType(value, dest);
        act.Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData(2.5d, typeof(int))]
    [InlineData(3.5d, typeof(int))]
    [InlineData(2.5f, typeof(int))]
    [InlineData(2.5d, typeof(byte))]
    [InlineData(300L, typeof(byte))]
    [InlineData(-1L, typeof(byte))]
    [InlineData(70000, typeof(short))]
    [InlineData(double.NaN, typeof(int))]
    [InlineData(double.PositiveInfinity, typeof(int))]
    [InlineData(double.PositiveInfinity, typeof(decimal))]
    [InlineData(1e30, typeof(decimal))]
    [InlineData(float.MaxValue, typeof(long))]
    [InlineData(long.MaxValue, typeof(decimal))]
    public void BufferedNumeric_MatchesOracle_OnRoundingOverflowNaNInfinity(object value, Type dest)
    {
        var source = value.GetType();
        AssertParity(source, value, dest);
    }

    [Fact]
    public void BufferedNumeric_DecimalMaxToDouble_MatchesOracle()
        => AssertParity(typeof(decimal), decimal.MaxValue, typeof(double));

    private static Type NullableType(Type type) => typeof(Nullable<>).MakeGenericType(type);

    private static void AssertParity(Type source, object value, Type dest)
    {
        Exception? oracleException = null;
        object? oracle = null;
        try
        {
            oracle = Convert.ChangeType(value, dest);
        }
        catch (Exception ex)
        {
            oracleException = ex;
        }

        Exception? bufferedException = null;
        object? buffered = null;
        try
        {
            buffered = Invoke(MapBuffered(dest), value, source);
        }
        catch (TargetInvocationException ex)
        {
            bufferedException = ex.InnerException ?? ex;
        }
        catch (Exception ex)
        {
            bufferedException = ex;
        }

        if (oracleException is not null)
        {
            bufferedException.Should().NotBeNull();
            bufferedException!.GetType().Should().Be(oracleException.GetType());
        }
        else
        {
            bufferedException.Should().BeNull();
            buffered.Should().Be(oracle);
        }
    }

    private static Expression MapBuffered(Type dest, bool defaultOnNull = false)
    {
        var column = new SelectExpression(dest) { Index = 0, PropertyName = "V" };
        if (defaultOnNull)
        {
            // DefaultOnNull is set internally by the *OrDefault scalar terminals; the property setter is
            // internal to nextorm.core, so drive it from the test via reflection.
            typeof(SelectExpression).GetProperty(nameof(SelectExpression.DefaultOnNull))!.SetValue(column, true);
        }

        return SqlServerTestContext.CreateSqlServer().MapColumnExpression(column, Record);
    }

    private static object? Invoke(Expression body, object? value, Type storage)
    {
        var lambda = Expression.Lambda(body, Record).Compile();
        return lambda.DynamicInvoke(new StrictNumericReader(value, storage));
    }

    private static object Sample(Type type) => type switch
    {
        _ when type == typeof(byte) => (byte)7,
        _ when type == typeof(short) => (short)7,
        _ when type == typeof(int) => 7,
        _ when type == typeof(long) => 7L,
        _ when type == typeof(float) => 7f,
        _ when type == typeof(double) => 7d,
        _ when type == typeof(decimal) => 7m,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a closed-matrix numeric type."),
    };

    /// <summary>
    /// A one-column reader with a declared storage type. Every typed getter throws when it does not match
    /// the storage type, and <c>GetValue</c>/<c>this[]</c> always throw, so any object-based fallback on
    /// the numeric path fails the test loudly.
    /// </summary>
    private sealed class StrictNumericReader(object? value, Type storage) : DbDataReader
    {
        public override int FieldCount => 1;

        public override bool HasRows => true;

        public override bool IsClosed => false;

        public override int RecordsAffected => -1;

        public override int Depth => 0;

        public override int VisibleFieldCount => 1;

        public override object this[int ordinal] => throw new NotSupportedException();

        public override object this[string name] => throw new NotSupportedException();

        public override object GetValue(int ordinal) => throw new NotSupportedException("GetValue must not be called on the box-free buffered numeric path.");

        public override bool IsDBNull(int ordinal) => value is null or DBNull;

        public override Type GetFieldType(int ordinal) => storage;

        public override string GetDataTypeName(int ordinal) => storage.Name;

        public override string GetName(int ordinal) => "V";

        public override int GetOrdinal(string name) => 0;

        public override bool Read() => throw new NotSupportedException();

        public override bool NextResult() => throw new NotSupportedException();

        public override IEnumerator GetEnumerator() => throw new NotSupportedException();

        public override int GetValues(object[] values) => throw new NotSupportedException();

        public override bool GetBoolean(int ordinal) => Require<bool>(ordinal);

        public override byte GetByte(int ordinal) => Require<byte>(ordinal);

        public override short GetInt16(int ordinal) => Require<short>(ordinal);

        public override int GetInt32(int ordinal) => Require<int>(ordinal);

        public override long GetInt64(int ordinal) => Require<long>(ordinal);

        public override float GetFloat(int ordinal) => Require<float>(ordinal);

        public override double GetDouble(int ordinal) => Require<double>(ordinal);

        public override decimal GetDecimal(int ordinal) => Require<decimal>(ordinal);

        public override string GetString(int ordinal) => Require<string>(ordinal);

        public override Guid GetGuid(int ordinal) => Require<Guid>(ordinal);

        public override DateTime GetDateTime(int ordinal) => Require<DateTime>(ordinal);

        public override char GetChar(int ordinal) => Require<char>(ordinal);

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        private T Require<T>(int ordinal)
        {
            if (ordinal != 0)
                throw new IndexOutOfRangeException();

            if (storage != typeof(T))
                throw new InvalidCastException($"Get{typeof(T).Name} called for storage {storage.Name}.");

            return (T)value!;
        }
    }
}
