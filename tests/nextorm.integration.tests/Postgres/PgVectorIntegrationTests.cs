using FluentAssertions;
using NextORM.Postgres;
using Npgsql;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextORM.Integration.Tests;

/// <summary>Exercises the pgvector extension against the live PostgreSQL integration provider.</summary>
public sealed class PgVectorIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [SqlTable("pgvector_materialization_probe")]
    public interface IVectorProbe
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("value")]
        PgVector Value { get; set; }

        [Column("optional_value")]
        PgVector? OptionalValue { get; set; }
    }

    public sealed class VectorProbe : IVectorProbe
    {
        public int Id { get; set; }
        public PgVector Value { get; set; }
        public PgVector? OptionalValue { get; set; }
    }

    public sealed class VectorDto
    {
        public PgVector Value { get; set; }
        public PgVector? OptionalValue { get; set; }
    }

    [Fact]
    public void NextOrmVectorMaterialization_ShouldRoundTripEntityAndProjections()
    {
        var ctx = _sut.DataProvider;
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "drop table if exists pgvector_materialization_probe; "
            + "create table pgvector_materialization_probe (id integer primary key, value vector not null, optional_value vector null)";
        command.ExecuteNonQuery();

        try
        {
            var expected = new PgVector([1.25f, -2f, 3.5f]);
            var target = PgVector.Parse("[1,2,3]");
            ctx.CreateInsertBuilder<IVectorProbe>()
                .Values(new[]
                {
                    new VectorProbe { Id = 1, Value = expected, OptionalValue = expected },
                    new VectorProbe { Id = 2, Value = target, OptionalValue = null }
                })
                .Insert();

            var entity = ctx.From<VectorProbe>().Where(x => x.Id == 1).ToList().Single();
            entity.Value.Should().Be(expected);
            entity.OptionalValue.Should().Be(expected);

            var scalar = ctx.From<VectorProbe>().Where(x => x.Id == 1).Select(x => x.Value).ToList().Single();
            scalar.Should().Be(expected);

            var nullable = ctx.From<VectorProbe>().Where(x => x.Id == 1).Select(x => x.OptionalValue).ToList().Single();
            nullable.Should().Be(expected);

            var projected = ctx.From<VectorProbe>().Where(x => x.Id == 1)
                .Select(x => new { x.Value, x.OptionalValue }).First();
            projected.Value.Should().Be(expected);
            projected.OptionalValue.Should().Be(expected);

            var dto = ctx.From<VectorProbe>().Where(x => x.Id == 1)
                .Select(x => new VectorDto { Value = x.Value, OptionalValue = x.OptionalValue }).First();
            dto.Value.Should().Be(expected);
            dto.OptionalValue.Should().Be(expected);

            var distances = ctx.From<VectorProbe>().Where(x => x.Id == 1).Select(x => new
            {
                L2 = SqlFunctions.Postgres.l2_distance(x.Value, target),
                Cosine = SqlFunctions.Postgres.cosine_distance(x.Value, target),
                InnerProduct = SqlFunctions.Postgres.negative_inner_product(x.Value, target),
                L1 = SqlFunctions.Postgres.l1_distance(x.Value, target)
            }).First();
            distances.L2.Should().BeApproximately(Math.Sqrt(0.25 * 0.25 + 16 + 0.5 * 0.5), 0.00001);
            distances.Cosine.Should().BeGreaterThan(0);
            distances.InnerProduct.Should().BeApproximately(-7.75, 0.00001);
            distances.L1.Should().BeApproximately(4.75, 0.00001);

            ctx.From<VectorProbe>()
                .Where(x => SqlFunctions.Postgres.l2_distance(x.Value, target) < 5)
                .Select(x => x.Id).ToList().Should().Contain(1);

            ctx.From<VectorProbe>()
                .OrderBy(x => SqlFunctions.Postgres.l2_distance(x.Value, target))
                .Limit(1).Select(x => x.Id).ToList().Single().Should().Be(2);

            ctx.From<VectorProbe>().Where(x => SqlFunctions.Postgres.cosine_distance(x.Value, target) > 0)
                .Select(x => x.Id).ToList().Should().ContainSingle().Which.Should().Be(1);
            ctx.From<VectorProbe>().OrderBy(x => SqlFunctions.Postgres.cosine_distance(x.Value, target))
                .Limit(1).Select(x => x.Id).ToList().Single().Should().Be(2);
            ctx.From<VectorProbe>().Where(x => SqlFunctions.Postgres.negative_inner_product(x.Value, target) > -10)
                .Select(x => x.Id).ToList().Should().ContainSingle().Which.Should().Be(1);
            ctx.From<VectorProbe>().OrderBy(x => SqlFunctions.Postgres.negative_inner_product(x.Value, target))
                .Limit(1).Select(x => x.Id).ToList().Single().Should().Be(2);
            ctx.From<VectorProbe>().Where(x => SqlFunctions.Postgres.l1_distance(x.Value, target) > 0)
                .Select(x => x.Id).ToList().Should().ContainSingle().Which.Should().Be(1);
            ctx.From<VectorProbe>().OrderBy(x => SqlFunctions.Postgres.l1_distance(x.Value, target))
                .Limit(1).Select(x => x.Id).ToList().Single().Should().Be(2);

            ctx.From<VectorProbe>().Where(x => SqlFunctions.Postgres.vector_dims(x.Value) == 3)
                .Select(x => x.Id).ToList().Should().Contain(1);
            ctx.From<VectorProbe>().Where(x => SqlFunctions.Postgres.vector_norm(x.Value) > 0)
                .Select(x => x.Id).ToList().Should().Contain(1);
            ctx.From<VectorProbe>().Where(x => SqlFunctions.Postgres.vector_dims(SqlFunctions.Postgres.l2_normalize(x.Value)) == 3)
                .Select(x => x.Id).ToList().Should().Contain(1);

            var functions = ctx.From<VectorProbe>().Where(x => x.Id == 1).Select(x => new
            {
                Dimensions = SqlFunctions.Postgres.vector_dims(x.Value),
                Norm = SqlFunctions.Postgres.vector_norm(x.Value),
                Normalized = SqlFunctions.Postgres.l2_normalize(x.Value)
            }).First();
            functions.Dimensions.Should().Be(3);
            functions.Norm.Should().BeApproximately(Math.Sqrt(1.25 * 1.25 + 4 + 3.5 * 3.5), 0.00001);
            functions.Normalized.Dimensions.Should().Be(3);

            var updated = PgVector.Parse("[9.125,-8.5,7.25]");
            ctx.CreateUpdateBuilder<IVectorProbe>().Set(x => x.Value, updated).Where(x => x.Id == 1).Update();
            ctx.From<VectorProbe>().Where(x => x.Id == 1).Select(x => x.Value).ToList().Single().Should().Be(updated);

            ctx.From<VectorProbe>().Where(x => x.Id == 2).Select(x => x.OptionalValue).ToList().Single().Should().BeNull();

            var wrongDimensions = PgVector.Parse("[1,2]");
            var mismatch = FluentActions.Invoking(() => ctx.From<VectorProbe>()
                    .Select(x => SqlFunctions.Postgres.l2_distance(x.Value, wrongDimensions)).First())
                .Should().Throw<PostgresException>().Which;
            mismatch.SqlState.Should().Be("22000");
            mismatch.MessageText.Should().Contain("different vector dimensions");
        }
        finally
        {
            command.CommandText = "drop table if exists pgvector_materialization_probe";
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void VectorCtas_ShouldPreserveNativeVectorColumnType()
    {
        var ctx = _sut.DataProvider;
        const string tableName = "pgvector_ctas_probe";
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"drop table if exists {tableName}; drop table if exists pgvector_materialization_probe; create table pgvector_materialization_probe (id integer primary key, value vector not null, optional_value vector null); insert into pgvector_materialization_probe values (-1, '[1,2,3]'::vector, null)";
        command.ExecuteNonQuery();
        try
        {
            ctx.From<VectorProbe>().Where(x => x.Id == -1)
                .Select(x => x.Value).ToTable(tableName);
            command.CommandText = "select udt_name from information_schema.columns where table_name = @table and column_name = 'value'";
            command.Parameters.AddWithValue("table", tableName);
            command.ExecuteScalar().Should().Be("vector");
            command.Parameters.Clear();
            command.CommandText = $"select value::text from {tableName}";
            command.ExecuteScalar().Should().Be("[1,2,3]");
        }
        finally
        {
            command.Parameters.Clear();
            command.CommandText = $"drop table if exists {tableName}; drop table if exists pgvector_materialization_probe";
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void VectorRoundtrip_ShouldPreserveValuesAndNull()
    {
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select @value::vector::text, @null_value::vector::text";
        command.Parameters.Add(new NpgsqlParameter("value", "[1.25,-2,3.5]") { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Unknown });
        command.Parameters.Add(new NpgsqlParameter("null_value", DBNull.Value) { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Unknown });

        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be("[1.25,-2,3.5]");
        reader.IsDBNull(1).Should().BeTrue();
    }

    [Fact]
    public void VectorSqlMarkers_ShouldExecute()
    {
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            select '[1,2,3]'::vector <-> '[4,5,6]'::vector,
                   '[1,2,3]'::vector <=> '[4,5,6]'::vector,
                   '[1,2,3]'::vector <#> '[4,5,6]'::vector,
                   '[1,2,3]'::vector <+> '[4,5,6]'::vector,
                   vector_dims('[1,2,3]'::vector),
                   vector_norm('[1,2,3]'::vector),
                   l2_normalize('[1,2,3]'::vector)::text
            """;

        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();
        reader.GetDouble(0).Should().BeApproximately(Math.Sqrt(27), 0.00001);
        reader.GetDouble(1).Should().BeGreaterThan(0);
        reader.GetDouble(2).Should().Be(-32);
        reader.GetDouble(3).Should().Be(9);
        reader.GetInt32(4).Should().Be(3);
        reader.GetDouble(5).Should().BeApproximately(Math.Sqrt(14), 0.00001);
        reader.GetString(6).Should().StartWith("[");
    }

    [Fact]
    public void VectorUnknownParameter_ShouldWorkWithDistanceOperator()
    {
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select '[1,2,3]'::vector <-> @value";
        command.Parameters.Add(new NpgsqlParameter("value", "[4,5,6]") { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Unknown });

        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();
        reader.GetDouble(0).Should().BeApproximately(Math.Sqrt(27), 0.00001);
    }
}
