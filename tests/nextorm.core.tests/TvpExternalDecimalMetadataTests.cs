using System.Data.Common;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Validates the decimal precision/scale declared by an <b>external</b> <see cref="IPropertyMetadata"/>
/// implementation, injected directly through the public <see cref="DataContextCache.Metadata"/> seam so
/// it bypasses <see cref="EntityMetadataBuilder{T}"/>/<see cref="EntityPropertyBuilder{T}"/>. The table
/// parameter binder must reject an out-of-range, partial or non-decimal pair before a provider narrows
/// (SQL Server casts precision/scale to <see cref="byte"/>) or formats (ClickHouse renders
/// <c>Decimal(p, s)</c>) the column, and must accept a decimal pair whose non-decimal model is mapped
/// through a converter.
/// <para>
/// Runs in the "Query cache controls" collection (serialized, parallelization disabled) because it
/// mutates and clears the process-wide <see cref="DataContextCache"/>.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class TvpExternalDecimalMetadataTests
{
    public sealed class ExternalDecimalRow
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ExternalPropertyMetadata : IPropertyMetadata
    {
        public required PropertyInfo PropertyInfo { get; init; }
        public required string ColumnName { get; init; }
        public int? DecimalPrecision { get; init; }
        public int? DecimalScale { get; init; }
        public IPropertyValueConverter? Converter { get; init; }
    }

    private sealed class ExternalEntityMetadata : IEntityMetadata
    {
        public required IReadOnlyList<IPropertyMetadata> Properties { get; init; }
        public string? TableName => "external_decimal_row";
        public bool IsTableNameAuto => false;
    }

    private sealed class TestDialect : SqlDialectBase
    {
        internal static readonly TestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class TestContext : DataContext
    {
        public TestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => TestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    private static IPropertyMetadata Amount(int? precision, int? scale, IPropertyValueConverter? converter = null)
        => new ExternalPropertyMetadata
        {
            PropertyInfo = typeof(ExternalDecimalRow).GetProperty(nameof(ExternalDecimalRow.Amount))!,
            ColumnName = nameof(ExternalDecimalRow.Amount),
            DecimalPrecision = precision,
            DecimalScale = scale,
            Converter = converter,
        };

    private static IPropertyMetadata NameWithConverter(IPropertyValueConverter converter, int precision, int scale)
        => new ExternalPropertyMetadata
        {
            PropertyInfo = typeof(ExternalDecimalRow).GetProperty(nameof(ExternalDecimalRow.Name))!,
            ColumnName = nameof(ExternalDecimalRow.Name),
            DecimalPrecision = precision,
            DecimalScale = scale,
            Converter = converter,
        };

    private static void Register(params IPropertyMetadata[] properties)
        => DataContextCache.Metadata[typeof(ExternalDecimalRow)] = new ExternalEntityMetadata { Properties = properties };

    private static IReadOnlyList<TableParameterColumn> GetColumns()
    {
        using var ctx = new TestContext();
        var carrier = (TableParameterValue)ProcedureParameter
            .Table("p", new[] { new ExternalDecimalRow { Name = "1.5" } }).Value!;
        return carrier.GetColumns(ctx);
    }

    private static void Run(Action assert)
    {
        DataContextCache.Clear();
        try
        {
            assert();
        }
        finally
        {
            DataContextCache.Clear();
        }
    }

    [Fact]
    public void ExternalMetadata_WithValidPrecisionScale_ShouldReachTheColumn()
    {
        Run(() =>
        {
            Register(Amount(12, 4));

            var column = GetColumns().Single();

            column.DecimalPrecision.Should().Be(12);
            column.DecimalScale.Should().Be(4);
        });
    }

    [Fact]
    public void ExternalMetadata_WithConverterToDecimal_ShouldBeAccepted()
    {
        Run(() =>
        {
            // A non-decimal model (string) bound through a converter to decimal owns a decimal column,
            // so the declared pair is valid.
            Register(NameWithConverter(new StringToDecimalConverter(), 12, 4));

            var column = GetColumns().Single();

            column.ClrType.Should().Be(typeof(decimal));
            column.DecimalPrecision.Should().Be(12);
            column.DecimalScale.Should().Be(4);
        });
    }

    [Theory]
    [InlineData(39, 38)]
    [InlineData(0, 0)]
    [InlineData(10, 11)]
    [InlineData(10, -1)]
    public void ExternalMetadata_WithOutOfRangePair_ShouldThrow(int precision, int scale)
    {
        Run(() =>
        {
            Register(Amount(precision, scale));

            Action act = () => GetColumns();

            act.Should().Throw<ArgumentOutOfRangeException>();
        });
    }

    [Fact]
    public void ExternalMetadata_WithPrecisionOnly_ShouldThrowPartialPair()
    {
        Run(() =>
        {
            Register(Amount(12, null));

            Action act = () => GetColumns();

            act.Should().Throw<InvalidOperationException>().WithMessage("*both*precision*scale*");
        });
    }

    [Fact]
    public void ExternalMetadata_WithScaleOnly_ShouldThrowPartialPair()
    {
        Run(() =>
        {
            Register(Amount(null, 4));

            Action act = () => GetColumns();

            act.Should().Throw<InvalidOperationException>().WithMessage("*both*precision*scale*");
        });
    }

    [Fact]
    public void ExternalMetadata_OnNonDecimalBoundType_ShouldThrow()
    {
        Run(() =>
        {
            Register(new ExternalPropertyMetadata
            {
                PropertyInfo = typeof(ExternalDecimalRow).GetProperty(nameof(ExternalDecimalRow.Name))!,
                ColumnName = nameof(ExternalDecimalRow.Name),
                DecimalPrecision = 12,
                DecimalScale = 4,
            });

            Action act = () => GetColumns();

            act.Should().Throw<InvalidOperationException>().WithMessage("*String*");
        });
    }

    [Fact]
    public void ExternalMetadata_DecimalModelWithTextConverter_ShouldThrow()
    {
        Run(() =>
        {
            Register(Amount(12, 4, new DecimalToStringConverter()));

            Action act = () => GetColumns();

            act.Should().Throw<InvalidOperationException>().WithMessage("*String*");
        });
    }
}
