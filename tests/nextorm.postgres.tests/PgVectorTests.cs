using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextORM.Postgres.Tests;

public sealed class PgVectorTests
{
    [SqlTable("pgvector_sql_marker")]
    public interface IVectorMarker
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("value")]
        PgVector Value { get; set; }
    }

    private static DbPreparedQueryCommand<T> Prepared<T>(IDataContext ctx, QueryCommand<T> command)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    [Fact]
    public void DistanceMarkers_ShouldRenderOperatorsInSelectWhereAndKnnOrder()
    {
        using var ctx = PostgresTestContext.Create();
        var target = PgVector.Parse("[1,2,3]");

        var selects = new[]
        {
            Prepared(ctx, ctx.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l2_distance(x.Value, target))),
            Prepared(ctx, ctx.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.cosine_distance(x.Value, target))),
            Prepared(ctx, ctx.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.negative_inner_product(x.Value, target))),
            Prepared(ctx, ctx.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l1_distance(x.Value, target)))
        };
        var operators = new[] { "<->", "<=>", "<#>", "<+>" };
        foreach (var (prepared, sqlOperator) in selects.Zip(operators))
        {
            Normalize(prepared.DbCommand.CommandText).Should().Be($"select (value {sqlOperator} @target)\n from pgvector_sql_marker");
            AssertTargetVectorIsBound(prepared.DbCommand);
        }

        var predicates = new[]
        {
            Prepared(ctx, ctx.From<IVectorMarker>().Where(x => SqlFunctions.Postgres.l2_distance(x.Value, target) < 0.25).Select(x => x.Id)),
            Prepared(ctx, ctx.From<IVectorMarker>().Where(x => SqlFunctions.Postgres.cosine_distance(x.Value, target) < 0.25).Select(x => x.Id)),
            Prepared(ctx, ctx.From<IVectorMarker>().Where(x => SqlFunctions.Postgres.negative_inner_product(x.Value, target) < 0.25).Select(x => x.Id)),
            Prepared(ctx, ctx.From<IVectorMarker>().Where(x => SqlFunctions.Postgres.l1_distance(x.Value, target) < 0.25).Select(x => x.Id))
        };
        foreach (var (prepared, sqlOperator) in predicates.Zip(operators))
        {
            Normalize(prepared.DbCommand.CommandText).Should().Be($"select id\n from pgvector_sql_marker\n where ((value {sqlOperator} @target) < 0.25)");
            AssertTargetVectorIsBound(prepared.DbCommand);
        }

        var nearest = new[]
        {
            Prepared(ctx, ctx.From<IVectorMarker>().OrderBy(x => SqlFunctions.Postgres.l2_distance(x.Value, target)).Limit(5).Select(x => x.Id)),
            Prepared(ctx, ctx.From<IVectorMarker>().OrderBy(x => SqlFunctions.Postgres.cosine_distance(x.Value, target)).Limit(5).Select(x => x.Id)),
            Prepared(ctx, ctx.From<IVectorMarker>().OrderBy(x => SqlFunctions.Postgres.negative_inner_product(x.Value, target)).Limit(5).Select(x => x.Id)),
            Prepared(ctx, ctx.From<IVectorMarker>().OrderBy(x => SqlFunctions.Postgres.l1_distance(x.Value, target)).Limit(5).Select(x => x.Id))
        };
        foreach (var (prepared, sqlOperator) in nearest.Zip(operators))
        {
            Normalize(prepared.DbCommand.CommandText).Should().Be($"select id\n from pgvector_sql_marker\n order by (value {sqlOperator} @target)\nlimit 5");
            AssertTargetVectorIsBound(prepared.DbCommand);
        }
    }

    [Fact]
    public void VectorScalarMarkers_ShouldRenderPgvectorFunctions()
    {
        using var ctx = PostgresTestContext.Create();
        var sql = Normalize(Prepared(ctx, ctx.From<IVectorMarker>().Select(x => new
        {
            Dimensions = SqlFunctions.Postgres.vector_dims(x.Value),
            Norm = SqlFunctions.Postgres.vector_norm(x.Value),
            Normalized = SqlFunctions.Postgres.l2_normalize(x.Value)
        })).DbCommand.CommandText);

        sql.Should().Contain("vector_dims(value)").And.Contain("vector_norm(value)")
            .And.Contain("CAST(l2_normalize(value) AS text)");
    }

    private static void AssertTargetVectorIsBound(System.Data.Common.DbCommand command)
    {
        command.Parameters.Count.Should().Be(1);
        command.Parameters[0].ParameterName.Should().Be("target");
        command.Parameters[0].Value.Should().Be("[1,2,3]");
        command.CommandText.Should().NotContain("[1,2,3]");
    }

    [Fact]
    public void ParseAndFormat_UsesInvariantRoundTripRepresentation()
    {
        var vector = PgVector.Parse("[1.25,-2,3.4028235E+38]");

        vector.Dimensions.Should().Be(3);
        vector[0].Should().Be(1.25f);
        vector.ToString().Should().Be("[1.25,-2,3.4028235E+38]");
        PgVector.Parse(vector.ToString()).Should().Be(vector);
    }

    [Fact]
    public void Equality_UsesCoordinatesAndHashCode()
    {
        var first = PgVector.Parse("[1,2.5]");
        var equal = new PgVector(new[] { 1f, 2.5f });
        var different = PgVector.Parse("[1,2]");

        first.Should().Be(equal);
        first.GetHashCode().Should().Be(equal.GetHashCode());
        first.Should().NotBe(different);
        PgVector.Parse("[]").Should().Be(default(PgVector));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void Parse_RejectsNonFiniteCoordinates(string coordinate)
    {
        FluentActions.Invoking(() => PgVector.Parse($"[{coordinate}]"))
            .Should().Throw<FormatException>();
    }

    [Fact]
    public void Constructor_RejectsNonFiniteCoordinates()
    {
        FluentActions.Invoking(() => new PgVector(new[] { 1f, float.NaN }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new PgVector(new[] { float.PositiveInfinity }))
            .Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("1,2")]
    [InlineData("[1,]")]
    [InlineData("[not-a-number]")]
    public void Parse_RejectsMalformedValues(string text)
    {
        FluentActions.Invoking(() => PgVector.Parse(text))
            .Should().Throw<FormatException>();
    }
}
