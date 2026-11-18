using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Postgres.Tests;

/// <summary>Verifies SQL generation for the pgvector half-precision, sparse, binary and array surface.</summary>
public sealed class PgVectorExtendedSqlTests
{
    [SqlTable("pgvector_extended_probe")]
    public interface IProbe
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

        [Column("values")]
        PgVector[] Values { get; set; }
    }

    private static DbPreparedQueryCommand<T> Prepared<T>(IDataContext ctx, QueryCommand<T> command)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    [Fact]
    public void HalfAndSparseDistances_ShouldRenderOperatorsWithTypedParameters()
    {
        using var ctx = PostgresTestContext.Create();
        var halfTarget = new PgHalfVector(new[] { 1f, 2f, 3f });
        var sparseTarget = new PgSparseVector(3, new[] { 0, 2 }, new[] { 1f, 2f });

        var half = Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.l2_distance(x.HalfValue, halfTarget))).DbCommand.CommandText);
        half.Should().Be("select (half_value <-> cast(@halfTarget as halfvec))\n from pgvector_extended_probe");

        var sparse = Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.cosine_distance(x.SparseValue, sparseTarget))).DbCommand.CommandText);
        sparse.Should().Be("select (sparse_value <=> cast(@sparseTarget as sparsevec))\n from pgvector_extended_probe");
    }

    [Fact]
    public void BitDistances_ShouldRenderHammingAndJaccard()
    {
        using var ctx = PostgresTestContext.Create();
        var target = PgBit.Parse("10101");

        var hamming = Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.hamming_distance(x.Bits, target)));
        Normalize(hamming.DbCommand.CommandText).Should().Be("select (bits <~> @target)\n from pgvector_extended_probe");
        hamming.DbCommand.Parameters[0].Value.Should().Be("10101");
        hamming.DbCommand.CommandText.Should().NotContain("10101");

        var jaccard = Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.jaccard_distance(x.Bits, target))).DbCommand.CommandText);
        jaccard.Should().Be("select (bits <%> @target)\n from pgvector_extended_probe");
    }

    [Fact]
    public void VectorArithmeticMarkers_ShouldRenderElementWiseOperators()
    {
        using var ctx = PostgresTestContext.Create();
        var target = PgVector.Parse("[1,2,3]");

        var operators = new (string Sql, string Operator)[]
        {
            (Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.vector_add(x.Value, target))).DbCommand.CommandText), "+"),
            (Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.vector_subtract(x.Value, target))).DbCommand.CommandText), "-"),
            (Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.vector_multiply(x.Value, target))).DbCommand.CommandText), "*"),
            (Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.vector_concat(x.Value, target))).DbCommand.CommandText), "||")
        };

        foreach (var (sql, sqlOperator) in operators)
            sql.Should().Be($"select CAST((value {sqlOperator} cast(@target as vector)) AS text)\n from pgvector_extended_probe");
    }

    [Fact]
    public void ExtendedVectorFunctions_ShouldRenderSubvectorNormAndQuantize()
    {
        using var ctx = PostgresTestContext.Create();

        var subvector = Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.subvector(x.Value, 2, 3))).DbCommand.CommandText);
        subvector.Should().Be("select CAST(subvector(value, 2, 3) AS text)\n from pgvector_extended_probe");

        var norm = Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.l2_norm(x.HalfValue))).DbCommand.CommandText);
        norm.Should().Be("select l2_norm(half_value)\n from pgvector_extended_probe");

        var normalized = Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.l2_normalize(x.SparseValue))).DbCommand.CommandText);
        normalized.Should().Be("select CAST(l2_normalize(sparse_value) AS text)\n from pgvector_extended_probe");

        var quantized = Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.binary_quantize<PgVector, PgBit>(x.Value))).DbCommand.CommandText);
        quantized.Should().Be("select CAST(binary_quantize(value) AS text)\n from pgvector_extended_probe");
    }

    [Fact]
    public void VectorArrayProjection_ShouldCastToTextArray()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => x.Values)).DbCommand.CommandText);

        sql.Should().Contain("CAST(values AS text[])");
    }

    [Fact]
    public void VectorArrayParameter_ShouldBindPostgresArrayLiteral()
    {
        var ctx = PostgresTestContext.CreatePostgres();
        var values = new[] { PgVector.Parse("[1,2]"), PgVector.Parse("[3,4]") };

        var parameter = ctx.CreateParam("values", values);

        parameter.Value.Should().Be("{\"[1,2]\",\"[3,4]\"}");
    }

    [Fact]
    public void ConstantOnlyVectorExpression_ShouldCastTheParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var constant = PgVector.Parse("[1,2,3]");

        var sql = Normalize(Prepared(ctx, ctx.From<IProbe>().Select(x => SqlFunctions.Postgres.l2_normalize(constant))).DbCommand.CommandText);

        sql.Should().Be("select CAST(l2_normalize(cast(@constant as vector)) AS text)\n from pgvector_extended_probe");
    }

    [Fact]
    public void HalfVectorArrayParameter_ShouldBindPostgresArrayLiteral()
    {
        var ctx = PostgresTestContext.CreatePostgres();
        var values = new[] { new PgHalfVector(new[] { 1f, 2f }) };

        var parameter = ctx.CreateParam("values", values);

        parameter.Value.Should().Be("{\"[1,2]\"}");
    }
}
