using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Postgres;
using Npgsql;

namespace NextORM.Integration.Tests;

/// <summary>
/// Exercises the extended pgvector surface against the live PostgreSQL provider: half-precision and
/// sparse vectors, binary (bit) vectors, the element-wise operators and vector arrays.
/// </summary>
public sealed class PgVectorTypeIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private const string CreateTable = """
        drop table if exists pgvector_types_probe;
        create table pgvector_types_probe (
            id integer primary key,
            value vector not null,
            half_value halfvec not null,
            sparse_value sparsevec not null,
            bits bit(5) not null,
            optional_bits bit(5) null,
            values vector[] not null,
            half_values halfvec[] not null
        )
        """;

    [SqlTable("pgvector_types_probe")]
    public interface ITypesProbe
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("value")]
        PgVector Value { get; set; }

        [Column("half_value")]
        PgHalfVector HalfValue { get; set; }

        [Column("sparse_value")]
        PgSparseVector SparseValue { get; set; }

        [Column("bits")]
        PgBit Bits { get; set; }

        [Column("optional_bits")]
        PgBit? OptionalBits { get; set; }

        [Column("values")]
        PgVector[] Values { get; set; }

        [Column("half_values")]
        PgHalfVector[] HalfValues { get; set; }
    }

    public sealed class TypesProbe : ITypesProbe
    {
        public int Id { get; set; }
        public PgVector Value { get; set; }
        public PgHalfVector HalfValue { get; set; }
        public PgSparseVector SparseValue { get; set; }
        public PgBit Bits { get; set; }
        public PgBit? OptionalBits { get; set; }
        public PgVector[] Values { get; set; } = [];
        public PgHalfVector[] HalfValues { get; set; } = [];
    }

    private static void WithTypesTable(Action<NpgsqlConnection> action)
    {
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = CreateTable;
        command.ExecuteNonQuery();

        try
        {
            action(connection);
        }
        finally
        {
            command.CommandText = "drop table if exists pgvector_types_probe";
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void ExtendedTypes_ShouldRoundTripThroughTheOrm()
    {
        var ctx = _sut.DataProvider;
        WithTypesTable(_ =>
        {
            var half = new PgHalfVector(new[] { 1.5f, -2f, 0.25f });
            var sparse = new PgSparseVector(5, new[] { 0, 2 }, new[] { 1f, 2f });
            var bits = PgBit.Parse("10101");
            var values = new[] { PgVector.Parse("[1,2,3]"), PgVector.Parse("[4,5,6]") };
            var halfValues = new[] { new PgHalfVector(new[] { 0.5f, 1f }) };

            ctx.CreateInsertBuilder<ITypesProbe>()
                .Values(new[]
                {
                    new TypesProbe
                    {
                        Id = 1,
                        Value = PgVector.Parse("[1,2,3]"),
                        HalfValue = half,
                        SparseValue = sparse,
                        Bits = bits,
                        OptionalBits = PgBit.Parse("11001"),
                        Values = values,
                        HalfValues = halfValues
                    },
                    new TypesProbe
                    {
                        Id = 2,
                        Value = PgVector.Parse("[1,1,1]"),
                        HalfValue = new PgHalfVector(new[] { 1f, 1f, 1f }),
                        SparseValue = new PgSparseVector(5, Array.Empty<int>(), Array.Empty<float>()),
                        Bits = PgBit.Parse("11111"),
                        OptionalBits = null,
                        Values = Array.Empty<PgVector>(),
                        HalfValues = Array.Empty<PgHalfVector>()
                    }
                })
                .Insert();

            var entity = ctx.From<TypesProbe>().Where(x => x.Id == 1).ToList().Single();
            entity.HalfValue.Should().Be(half);
            entity.SparseValue.Should().Be(sparse);
            entity.Bits.Should().Be(bits);
            entity.OptionalBits.Should().Be(PgBit.Parse("11001"));
            entity.Values.Should().Equal(values);
            entity.HalfValues.Should().Equal(halfValues);

            ctx.From<TypesProbe>().Where(x => x.Id == 2).Select(x => x.OptionalBits).ToList().Single().Should().BeNull();
            ctx.From<TypesProbe>().Where(x => x.Id == 2).Select(x => x.HalfValue).ToList().Single()
                .Should().Be(new PgHalfVector(new[] { 1f, 1f, 1f }));
            ctx.From<TypesProbe>().Where(x => x.Id == 2).Select(x => x.SparseValue).ToList().Single()
                .Dimensions.Should().Be(5);
            ctx.From<TypesProbe>().Where(x => x.Id == 2).Select(x => x.Values).ToList().Single().Should().BeEmpty();
        });
    }

    [Fact]
    public void ExtendedTypes_ShouldSupportDistancesFunctionsAndOperators()
    {
        var ctx = _sut.DataProvider;
        WithTypesTable(_ =>
        {
            var half = new PgHalfVector(new[] { 1.5f, -2f, 0.25f });
            ctx.CreateInsertBuilder<ITypesProbe>()
                .Values(new[]
                {
                    new TypesProbe
                    {
                        Id = 1,
                        Value = PgVector.Parse("[1,2,3]"),
                        HalfValue = half,
                        SparseValue = new PgSparseVector(3, new[] { 0, 2 }, new[] { 1f, 2f }),
                        Bits = PgBit.Parse("10101"),
                        Values = new[] { PgVector.Parse("[1,2,3]") },
                        HalfValues = new[] { new PgHalfVector(new[] { 1f, 2f }) }
                    }
                })
                .Insert();

            var halfTarget = new PgHalfVector(new[] { 1f, 2f, 3f });
            var sparseTarget = new PgSparseVector(3, new[] { 0 }, new[] { 1f });
            var bitTarget = PgBit.Parse("11111");

            var distances = ctx.From<TypesProbe>().Where(x => x.Id == 1).Select(x => new
            {
                HalfL2 = SqlFunctions.Postgres.l2_distance(x.HalfValue, halfTarget),
                HalfCosine = SqlFunctions.Postgres.cosine_distance(x.HalfValue, halfTarget),
                HalfInner = SqlFunctions.Postgres.negative_inner_product(x.HalfValue, halfTarget),
                HalfL1 = SqlFunctions.Postgres.l1_distance(x.HalfValue, halfTarget),
                SparseL2 = SqlFunctions.Postgres.l2_distance(x.SparseValue, sparseTarget),
                SparseCosine = SqlFunctions.Postgres.cosine_distance(x.SparseValue, sparseTarget),
                Hamming = SqlFunctions.Postgres.hamming_distance(x.Bits, bitTarget),
                Jaccard = SqlFunctions.Postgres.jaccard_distance(x.Bits, bitTarget)
            }).First();

            // pgvector accumulates a half-precision distance in half precision, so the server's value is
            // slightly above the exact float result (sqrt(0.0625 + 16 + 7.5625) = 4.86056).
            distances.HalfL2.Should().BeApproximately(4.879805323985784, 0.0001);
            distances.HalfCosine.Should().BeGreaterThan(0);
            distances.HalfInner.Should().BeApproximately(1.75, 0.0001);
            distances.HalfL1.Should().BeApproximately(7.25, 0.0001);
            distances.SparseL2.Should().BeApproximately(2, 0.0001);
            distances.SparseCosine.Should().BeGreaterThan(0);
            distances.Hamming.Should().Be(2);
            distances.Jaccard.Should().BeApproximately(0.4, 0.0001);

            var functions = ctx.From<TypesProbe>().Where(x => x.Id == 1).Select(x => new
            {
                HalfNorm = SqlFunctions.Postgres.l2_norm(x.HalfValue),
                SparseNorm = SqlFunctions.Postgres.l2_norm(x.SparseValue),
                HalfNormalized = SqlFunctions.Postgres.l2_normalize(x.HalfValue),
                SparseNormalized = SqlFunctions.Postgres.l2_normalize(x.SparseValue),
                Subvector = SqlFunctions.Postgres.subvector(x.HalfValue, 2, 2),
                Quantized = SqlFunctions.Postgres.binary_quantize<PgVector, PgBit>(x.Value)
            }).First();

            functions.HalfNorm.Should().BeApproximately(Math.Sqrt(1.5 * 1.5 + 4 + 0.25 * 0.25), 0.0001);
            functions.SparseNorm.Should().BeApproximately(Math.Sqrt(5), 0.0001);
            functions.HalfNormalized.Dimensions.Should().Be(3);
            functions.SparseNormalized.Dimensions.Should().Be(3);
            functions.Subvector.Should().Be(new PgHalfVector(new[] { -2f, 0.25f }));
            functions.Quantized.ToString().Should().Be("111");

            var arithmetic = ctx.From<TypesProbe>().Where(x => x.Id == 1).Select(x => new
            {
                Sum = SqlFunctions.Postgres.vector_add(x.HalfValue, halfTarget),
                Difference = SqlFunctions.Postgres.vector_subtract(x.HalfValue, halfTarget),
                Product = SqlFunctions.Postgres.vector_multiply(x.HalfValue, halfTarget),
                Concatenated = SqlFunctions.Postgres.vector_concat(x.HalfValue, halfTarget)
            }).First();

            arithmetic.Sum.Should().Be(new PgHalfVector(new[] { 2.5f, 0f, 3.25f }));
            arithmetic.Difference.Should().Be(new PgHalfVector(new[] { 0.5f, -4f, -2.75f }));
            arithmetic.Product.Should().Be(new PgHalfVector(new[] { 1.5f, -4f, 0.75f }));
            arithmetic.Concatenated.Dimensions.Should().Be(6);

            ctx.From<TypesProbe>()
                .Where(x => SqlFunctions.Postgres.hamming_distance(x.Bits, bitTarget) == 2)
                .Select(x => x.Id).ToList().Should().Equal(1);
            ctx.From<TypesProbe>()
                .Where(x => SqlFunctions.Postgres.l2_norm(x.HalfValue) > 0)
                .Select(x => x.Id).ToList().Should().Equal(1);
            ctx.From<TypesProbe>()
                .OrderBy(x => SqlFunctions.Postgres.l2_distance(x.HalfValue, halfTarget))
                .Limit(1).Select(x => x.Id).ToList().Should().Equal(1);
        });
    }

    [Fact]
    public void VectorArrays_ShouldProjectAndFilter()
    {
        var ctx = _sut.DataProvider;
        WithTypesTable(_ =>
        {
            var first = new[] { PgVector.Parse("[1,2,3]"), PgVector.Parse("[4,5,6]") };
            ctx.CreateInsertBuilder<ITypesProbe>()
                .Values(new[]
                {
                    new TypesProbe
                    {
                        Id = 1,
                        Value = PgVector.Parse("[1,2]"),
                        HalfValue = new PgHalfVector(new[] { 1f, 2f }),
                        SparseValue = new PgSparseVector(3, new[] { 0 }, new[] { 1f }),
                        Bits = PgBit.Parse("10101"),
                        Values = first,
                        HalfValues = new[] { new PgHalfVector(new[] { 1f, 2f }) }
                    }
                })
                .Insert();

            ctx.From<TypesProbe>().Where(x => x.Id == 1).Select(x => x.Values).ToList().Single().Should().Equal(first);

            var dto = ctx.From<TypesProbe>().Where(x => x.Id == 1)
                .Select(x => new { x.Values, x.HalfValues }).First();
            dto.Values.Should().Equal(first);
            dto.HalfValues.Should().Equal(new[] { new PgHalfVector(new[] { 1f, 2f }) });
        });
    }

    [Fact]
    public void HalfPrecision_ShouldRejectDimensionMismatch()
    {
        var ctx = _sut.DataProvider;
        WithTypesTable(_ =>
        {
            ctx.CreateInsertBuilder<ITypesProbe>()
                .Values(new[]
                {
                    new TypesProbe
                    {
                        Id = 1,
                        Value = PgVector.Parse("[1,2,3]"),
                        HalfValue = new PgHalfVector(new[] { 1f, 2f, 3f }),
                        SparseValue = new PgSparseVector(3, new[] { 0 }, new[] { 1f }),
                        Bits = PgBit.Parse("10101"),
                        Values = Array.Empty<PgVector>(),
                        HalfValues = Array.Empty<PgHalfVector>()
                    }
                })
                .Insert();

            var wrongDimensions = new PgHalfVector(new[] { 1f, 2f });
            var mismatch = FluentActions.Invoking(() => ctx.From<TypesProbe>()
                    .Select(x => SqlFunctions.Postgres.l2_distance(x.HalfValue, wrongDimensions)).First())
                .Should().Throw<PostgresException>().Which;
            mismatch.SqlState.Should().Be("22000");
            mismatch.MessageText.Should().Contain("different halfvec dimensions");
        });
    }
}
