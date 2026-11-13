using System.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

public sealed class SqlServerVectorSearchTests
{
    private const string ConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    [SqlTable("sqlserver_vector_probe")]
    public sealed class VectorSearchProbe
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("embedding")]
        public Vector Embedding { get; set; } = default!;
    }

    [Fact]
    public void VectorDistance_ShouldRenderSqlServerMetricsAndBindSqlVectorParameters()
    {
        using var context = Create17();
        var target = new Vector([1f, 2f, 3f]);

        AssertDistanceParameter(Prepared(context, context.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.VectorDistance(row.Embedding, target, VectorDistanceMetric.Cosine))), "cosine");
        AssertDistanceParameter(Prepared(context, context.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.VectorDistance(row.Embedding, target, VectorDistanceMetric.DotProduct))), "dot");
        AssertDistanceParameter(Prepared(context, context.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.VectorDistance(row.Embedding, target, VectorDistanceMetric.Euclidean))), "euclidean");

        context.CreateParam("nullVector", null).Value.Should().Be(DBNull.Value);
    }

    [Fact]
    public void VectorDistance_ShouldWorkInSelectWhereOrderByAndExactVectorSearch()
    {
        using var context = Create17();
        var target = new Vector([1f, 2f, 3f]);

        var selectSql = Normalize(Prepared(context, context.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.VectorDistance(row.Embedding, target, VectorDistanceMetric.Cosine)))
            .DbCommand.CommandText);
        var whereSql = Normalize(Prepared(context, context.From<VectorSearchProbe>()
            .Where(row => SqlFunctions.VectorDistance(row.Embedding, target, VectorDistanceMetric.Cosine) < 1d)
            .Select(row => row.Id)).DbCommand.CommandText);
        var orderSql = Normalize(Prepared(context, context.From<VectorSearchProbe>()
            .OrderBy(row => SqlFunctions.VectorDistance(row.Embedding, target, VectorDistanceMetric.Cosine))
            .Select(row => row.Id)).DbCommand.CommandText);
        var exactSearch = Prepared(context, context.From<VectorSearchProbe>()
            .VectorSearch(row => row.Embedding, target, VectorDistanceMetric.Cosine, 3, VectorSearchMode.Exact));
        var exactSql = Normalize(exactSearch.DbCommand.CommandText);

        selectSql.Should().Contain("VECTOR_DISTANCE('cosine', embedding, @target)");
        whereSql.Should().Contain("VECTOR_DISTANCE('cosine', embedding, @target)");
        orderSql.Should().Contain("VECTOR_DISTANCE('cosine', embedding, @target)");
        exactSql.Should().Contain("VECTOR_DISTANCE('cosine', embedding, @queryVector)");
        exactSearch.DbCommandParams.Count.Should().Be(1);
        ((SqlParameter)exactSearch.DbCommandParams[0]).SqlDbType.Should().Be(SqlDbTypeExtensions.Vector);
    }

    [Fact]
    public void ApproximateVectorSearch_ShouldRenderNamedVectorSearchArgumentsAndMaterializeResult()
    {
        using var context = Create17();
        var target = new Vector([1f, 2f, 3f]);
        var query = context.From<VectorSearchProbe>()
            .Where(row => row.Id > 1)
            .VectorSearch(row => row.Embedding, target, VectorDistanceMetric.Cosine, 5, VectorSearchMode.Approximate);
        var prepared = Prepared(context, query);
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Be("select top (@topK) with approximate id, embedding, [distance]\n"
            + " from VECTOR_SEARCH(TABLE = sqlserver_vector_probe as [t], COLUMN = [t].embedding, SIMILAR_TO = @queryVector, METRIC = 'cosine') as [t1]\n"
            + " where (t1.id > 1)\n"
            + " order by t1.[distance]");
        prepared.DbCommandParams.Count.Should().Be(2);
        prepared.DbCommandParams.Cast<SqlParameter>().Select(parameter => parameter.ParameterName).Should().Contain("topK");
        prepared.DbCommandParams.Cast<SqlParameter>().Select(parameter => parameter.ParameterName).Should().Contain("queryVector");
        prepared.DbCommandParams.Cast<SqlParameter>().Single(parameter => parameter.ParameterName.Contains("topK", StringComparison.Ordinal))
            .Value.Should().Be(5);
        var vectorParameter = prepared.DbCommandParams.Cast<SqlParameter>().Single(parameter => parameter.ParameterName.Contains("queryVector", StringComparison.Ordinal));
        vectorParameter.SqlDbType.Should().Be(SqlDbTypeExtensions.Vector);
        vectorParameter.Value.Should().BeOfType<SqlVector<float>>().Which.Memory.ToArray().Should().Equal(1f, 2f, 3f);

        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("embedding", typeof(string));
        table.Columns.Add("distance", typeof(double));
        table.Rows.Add(7, "[1,2,3]", 0.25d);
        using var reader = table.CreateDataReader();
        reader.Read().Should().BeTrue();
        prepared.MapDelegate.Should().NotBeNull();
        var result = prepared.MapDelegate!(reader);
        result.Entity.Id.Should().Be(7);
        result.Entity.Embedding.ToArray().Should().Equal(1f, 2f, 3f);
        result.Distance.Should().Be(0.25d);
    }

    [Fact]
    public void ApproximateVectorSearch_ShouldRefuseNonPositiveTopKAndConflictingOrderingOrPaging()
    {
        using var context = Create17();
        var target = new Vector([1f, 2f, 3f]);

        FluentActions.Invoking(() => context.From<VectorSearchProbe>()
                .VectorSearch(row => row.Embedding, target, VectorDistanceMetric.Cosine, 0, VectorSearchMode.Approximate))
            .Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("The approximate vector-search top-k value must be positive.");

        FluentActions.Invoking(() => context.From<VectorSearchProbe>()
                .VectorSearch(row => row.Embedding, target, VectorDistanceMetric.Cosine, -1, VectorSearchMode.Approximate))
            .Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("The approximate vector-search top-k value must be positive.");

        FluentActions.Invoking(() => Prepared(context, context.From<VectorSearchProbe>()
                .OrderBy(row => row.Id)
                .VectorSearch(row => row.Embedding, target, VectorDistanceMetric.Cosine, 5, VectorSearchMode.Approximate)))
            .Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("SQL Server approximate VECTOR_SEARCH owns ORDER BY distance ascending; a user OrderBy cannot be combined with it.");

        FluentActions.Invoking(() => Prepared(context, context.From<VectorSearchProbe>()
                .Page(10, 2)
                .VectorSearch(row => row.Embedding, target, VectorDistanceMetric.Cosine, 5, VectorSearchMode.Approximate)))
            .Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("SQL Server approximate VECTOR_SEARCH owns TOP (@k); existing paging cannot be combined with it.");
    }

    [Fact]
    public void VectorDistance_ShouldGateUndeclaredAndOlderServerVersionsDuringPreparation()
    {
        using var unversioned = new SqlServerDataContext(ConnectionString, new DataContextBuilder());
        using var sqlServer16 = new SqlServer16Context(ConnectionString);
        using var sqlServer17 = Create17();
        var target = new Vector([1f, 2f, 3f]);

        unversioned.Dialect.SupportsVectorSearch.Should().BeFalse();
        sqlServer16.Dialect.SupportsVectorSearch.Should().BeFalse();
        sqlServer17.Dialect.SupportsVectorSearch.Should().BeTrue();

        var unversionedQuery = unversioned.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.VectorDistance(row.Embedding, target, VectorDistanceMetric.Cosine));
        FluentActions.Invoking(() => Prepared(unversioned, unversionedQuery))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage("The VECTOR_DISTANCE function requires SQL Server major version 17 or later; declared major version: unspecified.");

        var sqlServer16Query = sqlServer16.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.VectorDistance(row.Embedding, target, VectorDistanceMetric.Cosine));
        FluentActions.Invoking(() => Prepared(sqlServer16, sqlServer16Query))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage("The VECTOR_DISTANCE function requires SQL Server major version 17 or later; declared major version: 16.");
    }

    [Fact]
    public void ApproximateVectorSearch_ShouldGateUndeclaredAndOlderServerVersionsDuringPreparation()
    {
        using var unversioned = new SqlServerDataContext(ConnectionString, new DataContextBuilder());
        using var sqlServer16 = new SqlServer16Context(ConnectionString);
        var target = new Vector([1f, 2f, 3f]);
        var unversionedQuery = unversioned.From<VectorSearchProbe>()
            .VectorSearch(row => row.Embedding, target, VectorDistanceMetric.Cosine, 5, VectorSearchMode.Approximate);
        var sqlServer16Query = sqlServer16.From<VectorSearchProbe>()
            .VectorSearch(row => row.Embedding, target, VectorDistanceMetric.Cosine, 5, VectorSearchMode.Approximate);

        FluentActions.Invoking(() => Prepared(unversioned, unversionedQuery))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage("The VECTOR_DISTANCE function requires SQL Server major version 17 or later; declared major version: unspecified.");
        FluentActions.Invoking(() => Prepared(sqlServer16, sqlServer16Query))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage("The VECTOR_DISTANCE function requires SQL Server major version 17 or later; declared major version: 16.");
    }

    [Fact]
    public void VectorDistance_ShouldRefuseUnknownAndPostgreSqlOnlyMetrics()
    {
        using var context = Create17();
        var target = new Vector([1f, 2f, 3f]);
        var invalidMetric = context.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.VectorDistance(row.Embedding, target, (VectorDistanceMetric)99));
        var l1Metric = context.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.Postgres.l1_distance(row.Embedding, target));
        var hammingMetric = context.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.Postgres.hamming_distance(row.Embedding, target));
        var jaccardMetric = context.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.Postgres.jaccard_distance(row.Embedding, target));

        FluentActions.Invoking(() => Prepared(context, invalidMetric))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage("The vector distance metric '99' is not supported by SQL Server.");
        FluentActions.Invoking(() => Prepared(context, l1Metric))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage("pgvector functions require PostgreSQL.");
        FluentActions.Invoking(() => Prepared(context, hammingMetric))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage("pgvector functions require PostgreSQL.");
        FluentActions.Invoking(() => Prepared(context, jaccardMetric))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage("pgvector functions require PostgreSQL.");
    }

    [Fact]
    public void VectorColumnDdl_ShouldRequireUserDeclaredDimension_AndMaterializerShouldParseServerText()
    {
        using var context = Create17();

        FluentActions.Invoking(() => context.Dialect.MakeTypeName(typeof(Vector)))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage("SQL Server cannot generate a vector column for CLR type 'Vector' without a declared dimension. Create the column explicitly as VECTOR(n).");

        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var materialized = MaterializeVector(context, "[1.25E+02,-2.5e-03,3.0E0]");
            materialized.Should().NotBeNull();
            materialized!.ToArray().Should().Equal(125f, -0.0025f, 3f);
            MaterializeVector(context, null).Should().BeNull();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static Vector? MaterializeVector(SqlServerDataContext context, string? text)
    {
        var column = new SelectExpression(typeof(Vector)) { Index = 0 };
        var record = Expression.Parameter(typeof(IDataRecord), "record");
        var mapped = context.MapColumnExpression(column, record);
        var mapper = Expression.Lambda<Func<IDataRecord, Vector?>>(mapped, record).Compile();

        var table = new DataTable();
        table.Columns.Add("embedding", typeof(string));
        var row = table.NewRow();
        row[0] = text ?? (object)DBNull.Value;
        table.Rows.Add(row);
        using var reader = table.CreateDataReader();
        reader.Read().Should().BeTrue();
        return mapper(reader);
    }

    private static void AssertDistanceParameter<T>(DbPreparedQueryCommand<T> prepared, string metric)
    {
        Normalize(prepared.DbCommand.CommandText)
            .Should().Contain($"VECTOR_DISTANCE('{metric}', embedding, @target)");
        prepared.DbCommandParams.Count.Should().Be(1);

        var parameter = (SqlParameter)prepared.DbCommandParams[0];
        parameter.ParameterName.Should().Contain("target");
        parameter.SqlDbType.Should().Be(SqlDbTypeExtensions.Vector);
        var sqlVector = parameter.Value.Should().BeOfType<SqlVector<float>>().Subject;
        sqlVector.Memory.ToArray().Should().Equal(1f, 2f, 3f);
    }

    private static SqlServerDataContext Create17() => new SqlServer17Context(ConnectionString);

    private static DbPreparedQueryCommand<T> Prepared<T>(SqlServerDataContext context, QueryCommand<T> command)
        => (DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private sealed class SqlServer16Context(string connectionString)
        : SqlServerDataContext(new Version(16, 0), connectionString)
    {
    }

    private sealed class SqlServer17Context(string connectionString)
        : SqlServerDataContext(new Version(17, 0), connectionString)
    {
    }
}
