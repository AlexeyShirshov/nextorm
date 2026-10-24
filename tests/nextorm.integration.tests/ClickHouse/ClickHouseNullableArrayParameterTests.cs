using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using ClickHouse.Driver.ADO;
using ClickHouse.Driver.ADO.Parameters;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T335 (#335), revised oracle (rv2): a non-null CLR array with nullable elements bound to ClickHouse
/// must keep its NULL positions. The current ClickHouse parameter construction passes the array to
/// the driver without an explicit ClickHouse type, so the driver infers the element type from the CLR
/// value (<c>Array(Int32)</c> for <c>int?[] { 1, null, 3 }</c>) and the server receives <c>{1,0,3}</c>.
/// </summary>
/// <remarks>
/// <para>
/// The suite is the required product of the T335 plan §7: five element families x two expression forms
/// (an array literal in the expression tree, and a captured local bound as one parameter) x six content
/// shapes. The revised AC02 splits the 60 cells into two observation paths that share the same bound
/// array operand:
/// </para>
/// <list type="bullet">
/// <item>Int32, Double and DateTime (36 cells) return the T276-normalised
/// <c>array_map(v =&gt; v, &lt;array&gt;)</c> result and assert the CLR array equality, length, order,
/// NULL mask and runtime element type.</item>
/// <item>Decimal (12 cells) does not materialise the array; the same operand is observed through
/// server-side scalars only (<c>length</c>, <c>arrayCount(v =&gt; v == null, ...)</c>,
/// <c>arrayFirstIndex</c> and <c>arraySlice</c>), because the driver's decimal materialisation is
/// outside this input-only repair. The substitutions are recorded in
/// <c>.pdca/T335/rv2/artifacts/oracle-notes.md</c>.</item>
/// <item>String (12 cells) keeps the same scalar oracle as the T335 input-path regression, and in
/// addition (T336) projects the same operand through <c>array_map(v =&gt; v, ...)</c> and materialises
/// the actual <c>string[]</c> result, so a lost or mis-positioned NULL is observed on the output path.</item>
/// </list>
/// <para>
/// The two focused facts (<see cref="Inline_Int_Mixed"/>, <see cref="Captured_Int_Mixed"/>) remain the
/// red reproducer. <see cref="NegativeControl_UntypedParameterLosesNulls"/> pins the oracle's
/// semantics on the raw driver binding: an untyped <c>int?[]</c> parameter loses its NULL.
/// </para>
/// </remarks>
[Collection("ClickHouse")]
public sealed class ClickHouseNullableArrayParameterTests : ProviderTestSuite
{
    [SqlTable("t335_nullable_array_param_entity")]
    internal interface IT335NullableArrayParamEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
    }

    private static readonly string[] FamilyNames = ["int", "double", "datetime", "decimal", "string"];

    private static readonly string[] ContentNames =
        ["mixed", "non_null_only", "empty", "all_null_len3", "singleton_null", "singleton_non_null"];

    /// <summary>One row, so the projection is evaluated exactly once per cell.</summary>
    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t335_nullable_array_param_entity",
                     "create table t335_nullable_array_param_entity (id Int32) engine = Memory",
                     "insert into t335_nullable_array_param_entity (id) values (1)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    public static TheoryData<string, string, string> MatrixCases
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var form in new[] { "inline", "captured" })
                foreach (var family in FamilyNames)
                    foreach (var content in ContentNames)
                        data.Add(form, family, content);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(MatrixCases))]
    public void Matrix(string form, string family, string content)
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var query = ctx.From<IT335NullableArrayParamEntity>().Where(x => x.Id == 1);

        var expected = ExpectedValues(family, content);

        if (family is "string" or "decimal")
        {
            // Scalar oracle: the same bound array operand, observed without materialising an array.
            var projection = ScalarOracle(family, content, form == "inline", out var input, out var operand);
            var observation = query.Select(projection).First();

            AssertScalarCell(observation, expected, form, family, content);

            if (family == "string")
            {
                // Output-path oracle (T336): the same operand, projected as an actual array and
                // materialised, so a NULL lost by the declared result type cannot hide behind scalars.
                var arrayProjection = StringArrayProjection(operand);
                var result = query.Select(arrayProjection).First();
                AssertStringArrayCell(result, expected, form, content);
            }

            if (form == "captured")
                AssertInputUnchanged(input, expected, family, content);
            return;
        }

        object? actual;
        if (form == "inline")
        {
            actual = RunInline(query, family, content);
        }
        else
        {
            actual = RunCaptured(query, family, content, out var input);
            AssertInputUnchanged(input, expected, family, content);
        }

        AssertCell(actual, expected, RuntimeArrayType(family), form, family, content);
    }

    [Fact]
    public void Inline_Int_Mixed()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var value = ctx.From<IT335NullableArrayParamEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => v, new int?[] { 1, null, 3 }))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int?[]>("the inline literal is bound as one nullable-element array");
            value.Should().Equal(new int?[] { 1, null, 3 }, "binding must preserve the NULL element");
        }
    }

    [Fact]
    public void Captured_Int_Mixed()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var values = new int?[] { 1, null, 3 };

        var value = ctx.From<IT335NullableArrayParamEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => v, values))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int?[]>("the captured array is bound as one nullable-element parameter");
            value.Should().Equal(new int?[] { 1, null, 3 }, "binding must preserve the NULL element");
            values.Should().Equal(new int?[] { 1, null, 3 }, "binding must not mutate the caller's array");
        }
    }

    /// <summary>
    /// Negative control: a raw <see cref="ClickHouseDbParameter"/> with no <see cref="ClickHouseDbParameter.ClickHouseType"/>
    /// reproduces the defect the matrix oracle detects — the driver infers <c>Array(Int32)</c>, the NULL
    /// at the 1-based index 2 becomes <c>0</c>, and <c>isNull(arrayElement(...))</c> is false.
    /// </summary>
    [Fact]
    public void NegativeControl_UntypedParameterLosesNulls()
    {
        Seed();
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "select toTypeName(@p), toInt64(length(@p)), toInt64(isNull(arrayElement(@p, 2))), toInt64(arrayElement(@p, 2))";
        command.Parameters.Add(new ClickHouseDbParameter
        {
            ParameterName = "p",
            Value = new int?[] { 1, null, 3 }
        });

        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue("the negative-control query returns exactly one row");
        var observedType = reader.GetString(0);
        var observedLength = reader.GetInt64(1);
        var isNullAtTwo = reader.GetInt64(2);
        var valueAtTwo = reader.GetInt64(3);

        using (new AssertionScope("negative control: an untyped int?[] parameter"))
        {
            observedType.Should().Be(
                "Array(Int32)",
                "without an explicit ClickHouseType the driver infers a non-nullable element type from the CLR value");
            observedLength.Should().Be(3, "the array length itself is preserved");
            isNullAtTwo.Should().Be(0,
                "the NULL at index 2 was replaced by the element default — the defect the oracle detects");
            valueAtTwo.Should().Be(0, "the loss replaces the NULL with the element default");
        }
    }

    // ---- §7 controls: shapes without a nullable declared element type --------------------------
    //
    // These controls prove the provider-local repair did not leak into shapes that must keep the
    // driver's inference. They are not matrix cells and they carry no nullable-element requirement.

    [Fact]
    public void Control_ByteArray_Inline()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var value = ctx.From<IT335NullableArrayParamEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => v, new byte[] { 1, 2, 3 }))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<byte[]>("byte[] keeps the driver's Array(UInt8) inference");
            value.Should().Equal(new byte[] { 1, 2, 3 });
        }
    }

    [Fact]
    public void Control_ByteArray_Captured()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var values = new byte[] { 1, 2, 3 };

        var value = ctx.From<IT335NullableArrayParamEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => v, values))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<byte[]>("a captured byte[] is not a nullable-element array");
            value.Should().Equal(new byte[] { 1, 2, 3 });
            values.Should().Equal(new byte[] { 1, 2, 3 }, "binding must not mutate the caller's array");
        }
    }

    [Fact]
    public void Control_NonNullableInt_Captured()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var values = new[] { 1, 2, 3 };

        var value = ctx.From<IT335NullableArrayParamEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => v, values))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int[]>("a non-nullable int[] stays on the driver's Array(Int32) inference");
            value.Should().Equal(new[] { 1, 2, 3 });
            values.Should().Equal(new[] { 1, 2, 3 }, "binding must not mutate the caller's array");
        }
    }

    [Fact]
    public void Control_NonNullableDouble_Captured()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var values = new[] { 1.5, 2.5, 3.5 };

        var value = ctx.From<IT335NullableArrayParamEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => v, values))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<double[]>("a non-nullable double[] stays on the driver's inference");
            value.Should().Equal(new[] { 1.5, 2.5, 3.5 });
            values.Should().Equal(new[] { 1.5, 2.5, 3.5 }, "binding must not mutate the caller's array");
        }
    }

    [Fact]
    public void Control_NonNullableDateTime_Captured()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var values = new[]
        {
            new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
            new DateTime(2021, 6, 7, 8, 9, 10, DateTimeKind.Unspecified)
        };

        var value = ctx.From<IT335NullableArrayParamEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => v, values))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<DateTime[]>("a non-nullable DateTime[] stays on the driver's inference");
            value.Should().Equal(values);
            values.Should().Equal(
                new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
                new DateTime(2021, 6, 7, 8, 9, 10, DateTimeKind.Unspecified));
        }
    }

    [Fact]
    public void Control_NonNullableDecimal_Captured()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var values = new[] { 1.5m, 2.5m, 3.25m };

        // Decimal array *output* materialisation is the recorded DEC-DECIMAL-ARRAY-MATERIALISATION
        // follow-up, not T335 work; the input binding is observed through server-side scalars.
        var observation = ctx.From<IT335NullableArrayParamEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Length = SqlFunctions.ClickHouse.length(values),
                First = SqlFunctions.ClickHouse.array_first_index(v => v == 1.5m, values),
                Last = SqlFunctions.ClickHouse.array_first_index(v => v == 3.25m, values)
            })
            .First();

        using (new AssertionScope())
        {
            observation.Length.Should().Be(3, "the server observes the whole captured array");
            observation.First.Should().Be(1L, "the decimal values keep their order and values");
            observation.Last.Should().Be(3L);
            values.Should().Equal(new[] { 1.5m, 2.5m, 3.25m }, "binding must not mutate the caller's array");
        }
    }

    // ---- scalar oracle for the string/decimal families -----------------------------------------
    //
    // ClickHouseFunctions exposes neither toTypeName nor isNull/arrayElement. The closest supported
    // server-side equivalents are used; the exact substitutions are recorded in
    // .pdca/T335/rv2/artifacts/oracle-notes.md:
    //   toTypeName(arr)             -> the server-observed nullability (a non-zero NULL count is only
    //                                  possible for Array(Nullable(T))) + the empty/all-null bind
    //   isNull(arrayElement(a, i))  -> arrayCount(v => v == null, arraySlice(a, i + 1, 1))
    //   arrayElement(a, i)          -> arrayFirstIndex(v => v == expectedLiteral, a) == i + 1
    //
    // The operand is the same inline literal / captured array the array_map cells bind, but it is not
    // nested under array_map: T276's declared cast to Array(String) cannot carry a NULL element.

    private static readonly PropertyInfo ClickHouseFunctionsProperty =
        typeof(SqlFunctions).GetProperty(nameof(SqlFunctions.ClickHouse))!;

    private static readonly MethodInfo LengthMethod =
        typeof(ClickHouseFunctions).GetMethod(nameof(ClickHouseFunctions.length))!;

    private static readonly MethodInfo ArrayCountMethod =
        typeof(ClickHouseFunctions).GetMethod(nameof(ClickHouseFunctions.array_count))!;

    private static readonly MethodInfo ArrayFirstIndexMethod =
        typeof(ClickHouseFunctions).GetMethod(nameof(ClickHouseFunctions.array_first_index))!;

    private static readonly MethodInfo ArraySliceMethod = typeof(ClickHouseFunctions)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .Single(m => m.Name == nameof(ClickHouseFunctions.array_slice) && m.GetParameters().Length == 3);

    /// <summary>The two-argument <c>array_map&lt;TIn, TOut&gt;</c> overload used by the output oracle.</summary>
    private static readonly MethodInfo ArrayMapMethod = typeof(ClickHouseFunctions)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .Single(m => m.Name == nameof(ClickHouseFunctions.array_map)
                     && m.IsGenericMethodDefinition
                     && m.GetParameters().Length == 2);

    private static readonly PropertyInfo[] NullFlagProperties =
    [
        typeof(T335ScalarObservation).GetProperty(nameof(T335ScalarObservation.NullFlag0))!,
        typeof(T335ScalarObservation).GetProperty(nameof(T335ScalarObservation.NullFlag1))!,
        typeof(T335ScalarObservation).GetProperty(nameof(T335ScalarObservation.NullFlag2))!
    ];

    private static readonly PropertyInfo[] ValueIndexProperties =
    [
        typeof(T335ScalarObservation).GetProperty(nameof(T335ScalarObservation.ValueIndex0))!,
        typeof(T335ScalarObservation).GetProperty(nameof(T335ScalarObservation.ValueIndex1))!,
        typeof(T335ScalarObservation).GetProperty(nameof(T335ScalarObservation.ValueIndex2))!
    ];

    private static readonly PropertyInfo LengthProperty =
        typeof(T335ScalarObservation).GetProperty(nameof(T335ScalarObservation.Length))!;

    private static readonly PropertyInfo NullCountProperty =
        typeof(T335ScalarObservation).GetProperty(nameof(T335ScalarObservation.NullCount))!;

    private static readonly PropertyInfo FirstNullIndexProperty =
        typeof(T335ScalarObservation).GetProperty(nameof(T335ScalarObservation.FirstNullIndex))!;

    /// <summary>Captured-form holder: the array is reached through a closure-style field.</summary>
    private sealed class ArrayHolder<T>
    {
        public ArrayHolder(T[] values) => Values = values;

        public readonly T[] Values;
    }

    private static Expression<Func<IT335NullableArrayParamEntity, T335ScalarObservation>> ScalarOracle(
        string family,
        string content,
        bool inlineForm,
        out Array input,
        out Expression operand)
    {
        var elementType = family == "string" ? typeof(string) : typeof(decimal?);
        var expected = ExpectedValues(family, content);
        input = expected;

        Expression array;
        if (inlineForm)
        {
            array = Expression.NewArrayInit(
                elementType,
                expected.Cast<object?>().Select(value => ElementConstant(elementType, value)));
        }
        else
        {
            var holderType = typeof(ArrayHolder<>).MakeGenericType(elementType);
            var holder = Activator.CreateInstance(holderType, expected)!;
            array = Expression.Field(Expression.Constant(holder, holderType), "Values");
        }

        operand = array;

        var functions = Expression.Property(null, ClickHouseFunctionsProperty);
        var firstNullIndex = ArrayFirstIndex(functions, elementType, array, NullPredicate(elementType));
        var bindings = new List<MemberBinding>
        {
            Expression.Bind(
                LengthProperty,
                Expression.Call(functions, LengthMethod.MakeGenericMethod(elementType), array)),
            Expression.Bind(
                NullCountProperty,
                ArrayCount(functions, elementType, array, NullPredicate(elementType))),
            Expression.Bind(FirstNullIndexProperty, firstNullIndex)
        };

        for (var slot = 0; slot < 3; slot++)
        {
            bindings.Add(Expression.Bind(
                NullFlagProperties[slot],
                ArrayCount(functions, elementType, Slice(functions, elementType, array, slot), NullPredicate(elementType))));

            // A slot without a populated expected value still projects a server-observed Int64 (never a
            // bare literal: ClickHouse types a raw "0" as UInt8 and the row mapper reads a long).
            Expression valueIndex = firstNullIndex;
            if (slot < expected.Length && expected.GetValue(slot) is { } value)
                valueIndex = ArrayFirstIndex(functions, elementType, array, ValuePredicate(elementType, value));
            bindings.Add(Expression.Bind(ValueIndexProperties[slot], valueIndex));
        }

        var parameter = Expression.Parameter(typeof(IT335NullableArrayParamEntity), "x");
        var body = Expression.MemberInit(Expression.New(typeof(T335ScalarObservation)), bindings);
        return Expression.Lambda<Func<IT335NullableArrayParamEntity, T335ScalarObservation>>(body, parameter);
    }

    /// <summary>
    /// The output-path oracle: <c>array_map&lt;string, string&gt;(v =&gt; v, &lt;operand&gt;)</c> over the
    /// very same operand the scalar oracle observes, projected as a declared <c>string[]</c> result.
    /// </summary>
    private static Expression<Func<IT335NullableArrayParamEntity, string[]>> StringArrayProjection(Expression arrayOperand)
    {
        var functions = Expression.Property(null, ClickHouseFunctionsProperty);
        var value = Expression.Parameter(typeof(string), "v");
        var identity = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(typeof(string), typeof(string)), value, value);
        var call = Expression.Call(
            functions,
            ArrayMapMethod.MakeGenericMethod(typeof(string), typeof(string)),
            Expression.Quote(identity),
            arrayOperand);

        var parameter = Expression.Parameter(typeof(IT335NullableArrayParamEntity), "x");
        return Expression.Lambda<Func<IT335NullableArrayParamEntity, string[]>>(call, parameter);
    }

    private static Expression ElementConstant(Type elementType, object? value) =>
        value is null
            ? Expression.Constant(null, elementType)
            : Nullable.GetUnderlyingType(elementType) is null
                ? Expression.Constant(value, elementType)
                : Expression.Convert(Expression.Constant(value), elementType);

    private static LambdaExpression NullPredicate(Type elementType)
    {
        var parameter = Expression.Parameter(elementType, "v");
        return Expression.Lambda(
            typeof(Func<,>).MakeGenericType(elementType, typeof(bool)),
            Expression.Equal(parameter, Expression.Constant(null, elementType)),
            parameter);
    }

    private static LambdaExpression ValuePredicate(Type elementType, object value)
    {
        var parameter = Expression.Parameter(elementType, "v");
        return Expression.Lambda(
            typeof(Func<,>).MakeGenericType(elementType, typeof(bool)),
            Expression.Equal(parameter, ElementConstant(elementType, value)),
            parameter);
    }

    private static Expression ArrayCount(
        Expression functions, Type elementType, Expression array, LambdaExpression predicate) =>
        Expression.Call(
            functions,
            ArrayCountMethod.MakeGenericMethod(elementType),
            Expression.Quote(predicate),
            array);

    private static Expression ArrayFirstIndex(
        Expression functions, Type elementType, Expression array, LambdaExpression predicate) =>
        Expression.Call(
            functions,
            ArrayFirstIndexMethod.MakeGenericMethod(elementType),
            Expression.Quote(predicate),
            array);

    private static Expression Slice(
        Expression functions, Type elementType, Expression array, int slot) =>
        Expression.Call(
            functions,
            ArraySliceMethod.MakeGenericMethod(elementType),
            array,
            Expression.Constant((long)(slot + 1)),
            Expression.Constant(1L));

    // ---- inline form: every arm carries a literal inside the expression tree -------------------

    private static object? RunInline(EntityBuilder<IT335NullableArrayParamEntity> query, string family, string content) =>
        family switch
        {
            "int" => content switch
            {
                "mixed" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new int?[] { 1, null, 3 })).First(),
                "non_null_only" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new int?[] { 1, 2, 3 })).First(),
                "empty" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new int?[] { })).First(),
                "all_null_len3" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new int?[] { null, null, null })).First(),
                "singleton_null" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new int?[] { null })).First(),
                "singleton_non_null" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new int?[] { 7 })).First(),
                _ => throw new ArgumentOutOfRangeException(nameof(content), content, "unknown content")
            },
            "double" => content switch
            {
                "mixed" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new double?[] { 1.5, null, -2.5 })).First(),
                "non_null_only" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new double?[] { 1.5, 2.5, 3.5 })).First(),
                "empty" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new double?[] { })).First(),
                "all_null_len3" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new double?[] { null, null, null })).First(),
                "singleton_null" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new double?[] { null })).First(),
                "singleton_non_null" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new double?[] { 7.5 })).First(),
                _ => throw new ArgumentOutOfRangeException(nameof(content), content, "unknown content")
            },
            "datetime" => content switch
            {
                "mixed" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new DateTime?[]
                {
                    new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified), null,
                    new DateTime(2021, 6, 7, 8, 9, 10, DateTimeKind.Unspecified)
                })).First(),
                "non_null_only" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new DateTime?[]
                {
                    new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
                    new DateTime(2021, 6, 7, 8, 9, 10, DateTimeKind.Unspecified),
                    new DateTime(2022, 12, 31, 23, 59, 58, DateTimeKind.Unspecified)
                })).First(),
                "empty" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new DateTime?[] { })).First(),
                "all_null_len3" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new DateTime?[] { null, null, null })).First(),
                "singleton_null" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new DateTime?[] { null })).First(),
                "singleton_non_null" => query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, new DateTime?[]
                {
                    new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified)
                })).First(),
                _ => throw new ArgumentOutOfRangeException(nameof(content), content, "unknown content")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "unknown family")
        };

    // ---- captured form: a local array referenced from the expression tree ----------------------

    private static object? RunCaptured(
        EntityBuilder<IT335NullableArrayParamEntity> query,
        string family,
        string content,
        out Array input) =>
        family switch
        {
            "int" => RunCapturedInt(query, content, out input),
            "double" => RunCapturedDouble(query, content, out input),
            "datetime" => RunCapturedDateTime(query, content, out input),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "unknown family")
        };

    private static object? RunCapturedInt(
        EntityBuilder<IT335NullableArrayParamEntity> query, string content, out Array input)
    {
        var values = Values<int>(content);
        input = values;
        return query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, values)).First();
    }

    private static object? RunCapturedDouble(
        EntityBuilder<IT335NullableArrayParamEntity> query, string content, out Array input)
    {
        var values = Values<double>(content);
        input = values;
        return query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, values)).First();
    }

    private static object? RunCapturedDateTime(
        EntityBuilder<IT335NullableArrayParamEntity> query, string content, out Array input)
    {
        var values = Values<DateTime>(content);
        input = values;
        return query.Select(x => SqlFunctions.ClickHouse.array_map(v => v, values)).First();
    }

    // ---- content catalogue ---------------------------------------------------------------------

    private static T?[] Values<T>(string content) where T : struct =>
        content switch
        {
            "mixed" => Mixed<T>(),
            "non_null_only" => NonNullOnly<T>(),
            "empty" => [],
            "all_null_len3" => [null, null, null],
            "singleton_null" => [null],
            "singleton_non_null" => [Singleton<T>()],
            _ => throw new ArgumentOutOfRangeException(nameof(content), content, "unknown content")
        };

    private static string?[] Values(string content) =>
        content switch
        {
            "mixed" => ["a", null, "b"],
            "non_null_only" => ["a", "b", "c"],
            "empty" => [],
            "all_null_len3" => [null, null, null],
            "singleton_null" => [null],
            "singleton_non_null" => ["a"],
            _ => throw new ArgumentOutOfRangeException(nameof(content), content, "unknown content")
        };

    private static T?[] Mixed<T>() where T : struct => typeof(T) switch
    {
        _ when typeof(T) == typeof(int) => (T?[])(object)new int?[] { 1, null, 3 },
        _ when typeof(T) == typeof(double) => (T?[])(object)new double?[] { 1.5, null, -2.5 },
        _ when typeof(T) == typeof(DateTime) => (T?[])(object)new DateTime?[]
        {
            new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified), null,
            new DateTime(2021, 6, 7, 8, 9, 10, DateTimeKind.Unspecified)
        },
        _ when typeof(T) == typeof(decimal) => (T?[])(object)new decimal?[] { 1.5m, null, 3.25m },
        _ => throw new ArgumentOutOfRangeException(nameof(T), typeof(T), "unknown element type")
    };

    private static T?[] NonNullOnly<T>() where T : struct => typeof(T) switch
    {
        _ when typeof(T) == typeof(int) => (T?[])(object)new int?[] { 1, 2, 3 },
        _ when typeof(T) == typeof(double) => (T?[])(object)new double?[] { 1.5, 2.5, 3.5 },
        _ when typeof(T) == typeof(DateTime) => (T?[])(object)new DateTime?[]
        {
            new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
            new DateTime(2021, 6, 7, 8, 9, 10, DateTimeKind.Unspecified),
            new DateTime(2022, 12, 31, 23, 59, 58, DateTimeKind.Unspecified)
        },
        _ when typeof(T) == typeof(decimal) => (T?[])(object)new decimal?[] { 1.5m, 2.5m, 3.25m },
        _ => throw new ArgumentOutOfRangeException(nameof(T), typeof(T), "unknown element type")
    };

    private static T? Singleton<T>() where T : struct => typeof(T) switch
    {
        _ when typeof(T) == typeof(int) => (T?)(object)7,
        _ when typeof(T) == typeof(double) => (T?)(object)7.5,
        _ when typeof(T) == typeof(DateTime) => (T?)(object)new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
        _ when typeof(T) == typeof(decimal) => (T?)(object)7.5m,
        _ => throw new ArgumentOutOfRangeException(nameof(T), typeof(T), "unknown element type")
    };

    private static Array ExpectedValues(string family, string content) => family switch
    {
        "int" => Values<int>(content),
        "double" => Values<double>(content),
        "datetime" => Values<DateTime>(content),
        "decimal" => Values<decimal>(content),
        "string" => Values(content),
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "unknown family")
    };

    private static Type RuntimeArrayType(string family) => family switch
    {
        "int" => typeof(int?[]),
        "double" => typeof(double?[]),
        "datetime" => typeof(DateTime?[]),
        "decimal" => typeof(decimal?[]),
        "string" => typeof(string?[]),
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "unknown family")
    };

    // ---- assertions ----------------------------------------------------------------------------

    private static void AssertScalarCell(
        T335ScalarObservation observed, Array expected, string form, string family, string content)
    {
        var label = $"{form} {family} {content}";
        var nullPositions = Enumerable.Range(0, expected.Length)
            .Where(i => expected.GetValue(i) is null)
            .ToArray();

        using (new AssertionScope(label))
        {
            observed.Length.Should().Be(expected.Length, $"{label}: server-observed length");
            observed.NullCount.Should().Be(
                nullPositions.Length, $"{label}: server-observed NULL count");
            observed.FirstNullIndex.Should().Be(
                nullPositions.Length == 0 ? 0L : nullPositions[0] + 1L,
                $"{label}: server-observed one-based position of the first NULL");

            var nullFlags = new[] { observed.NullFlag0, observed.NullFlag1, observed.NullFlag2 };
            var valueIndexes = new[] { observed.ValueIndex0, observed.ValueIndex1, observed.ValueIndex2 };
            for (var slot = 0; slot < nullFlags.Length; slot++)
            {
                var nullHere = slot < expected.Length && expected.GetValue(slot) is null;
                nullFlags[slot].Should().Be(
                    nullHere ? 1L : 0L,
                    $"{label}: isNull(arraySlice(array, {slot + 1}, 1))");
            }

            for (var slot = 0; slot < expected.Length; slot++)
            {
                if (expected.GetValue(slot) is null)
                    continue;
                valueIndexes[slot].Should().Be(
                    slot + 1L,
                    $"{label}: the independently typed expected literal sits at the one-based index {slot + 1}");
            }
        }
    }

    private static void AssertCell(object? actual, Array expected, Type expectedType, string form, string family, string content)
    {
        var label = $"{form} {family} {content}";

        using (new AssertionScope(label))
        {
            actual.Should().BeOfType(expectedType, $"{label}: the projected runtime type is the declared array type");
            var array = (Array)actual!;
            array.Length.Should().Be(expected.Length, $"{label}: length");
            array.GetType().GetElementType().Should().Be(
                expectedType.GetElementType(), $"{label}: runtime element type");

            for (var i = 0; i < expected.Length; i++)
                array.GetValue(i).Should().Be(expected.GetValue(i), $"{label}: element {i} keeps its value/NULL");
        }
    }

    /// <summary>
    /// Output-path assertion (T336): the materialised <c>string[]</c> must keep the expected length and
    /// every element's exact value/NULL at its expected position.
    /// </summary>
    private static void AssertStringArrayCell(string[] actual, Array expected, string form, string content)
    {
        var label = $"{form} string {content}";

        using (new AssertionScope(label))
        {
            actual.Should().BeOfType<string[]>($"{label}: the projected runtime type is the declared string array");
            actual.Should().HaveCount(expected.Length, $"{label}: result length");

            for (var i = 0; i < expected.Length && i < actual.Length; i++)
            {
                var expectedValue = (string?)expected.GetValue(i);
                if (expectedValue is null)
                {
                    actual[i].Should().BeNull($"{label}: result[{i}] must be NULL");
                }
                else
                {
                    actual[i].Should().Be(
                        expectedValue,
                        $"{label}: result[{i}] must be the value \"{expectedValue}\" in its original position");
                }
            }
        }
    }

    private static void AssertInputUnchanged(Array input, Array expected, string family, string content)
    {
        var label = $"captured {family} {content}";

        using (new AssertionScope(label))
        {
            input.Length.Should().Be(expected.Length, $"{label}: the caller's array keeps its length");
            for (var i = 0; i < expected.Length; i++)
                input.GetValue(i).Should().Be(expected.GetValue(i), $"{label}: element {i} is unchanged");
        }
    }

    /// <summary>The nine server-side scalar observations of one string/decimal matrix cell.</summary>
    public sealed class T335ScalarObservation
    {
        public long Length { get; set; }

        public long NullCount { get; set; }

        public long FirstNullIndex { get; set; }

        public long NullFlag0 { get; set; }

        public long NullFlag1 { get; set; }

        public long NullFlag2 { get; set; }

        public long ValueIndex0 { get; set; }

        public long ValueIndex1 { get; set; }

        public long ValueIndex2 { get; set; }
    }
}
