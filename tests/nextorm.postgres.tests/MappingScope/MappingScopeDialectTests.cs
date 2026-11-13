using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.SqlServer;
using NextORM.Sqlite;

namespace NextORM.Postgres.Tests.MappingScope;

public sealed class MappingScopeDialectTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("clickhouse")]
    public void ScopedQuery_RendersScopeSpecificNamesAndParameterPrefix(string provider)
    {
        var builder = new DataContextBuilder().UseEntityMetadataProvider(new ScopedMetadataProvider());
        using var scopedContext = CreateContext(provider, builder);
        var minimum = 3;
        var query = scopedContext.From<ScopedRow>().WithScope(new global::NextORM.Core.MappingScope("tenant"))
            .Where(row => row.Id > minimum)
            .Select(row => row.Id);
        var prepared = (DbPreparedQueryCommand<int>)scopedContext.GetPreparedQueryCommand(
            query, false, true, CancellationToken.None);
        var sql = prepared.DbCommand.CommandText;

        sql.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id");
        prepared.DbCommand.Parameters.Cast<System.Data.Common.DbParameter>()
            .Should().ContainSingle(parameter => parameter.ParameterName.Contains("tenant_", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("clickhouse")]
    public void ScopedUpdate_RendersScopeSpecificNamesAndParameterPrefix(string provider)
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseScopeResolver(new TenantScopeResolver());
        using var scopedContext = CreateContext(provider, builder);
        var minimum = 3;
        var sql = scopedContext.CreateUpdateBuilder<ScopedRow>()
            .Set(row => row.Id, 42)
            .Where(row => row.Id > minimum)
            .ToSql();

        sql.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id").And.Contain("tenant_");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("clickhouse")]
    public void ScopedInsertAndDelete_RenderScopeSpecificTargetColumnsAndPrefixes(string provider)
    {
        var resolver = new MutableScopeResolver();
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseScopeResolver(resolver);
        using var context = CreateContext(provider, builder);

        resolver.Scope = global::NextORM.Core.MappingScope.Default;
        var defaultInsert = context.CreateInsertBuilder<ScopedRow>().Value(row => row.Id, 1).ToSql();
        var defaultDelete = context.CreateDeleteBuilder<ScopedRow>().Where(row => row.Id > 2).ToSql();
        resolver.Scope = new global::NextORM.Core.MappingScope("alpha");
        var alphaInsert = context.CreateInsertBuilder<ScopedRow>().Value(row => row.Id, 11).ToSql();
        var minimum = 2;
        var alphaDelete = context.CreateDeleteBuilder<ScopedRow>().Where(row => row.Id > minimum).ToSql();
        resolver.Scope = new global::NextORM.Core.MappingScope("beta");
        var betaInsert = context.CreateInsertBuilder<ScopedRow>().Value(row => row.Id, 22).ToSql();
        var betaDelete = context.CreateDeleteBuilder<ScopedRow>().Where(row => row.Id > minimum).ToSql();

        defaultInsert.Should().Contain("ScopedRow").And.Contain("Id").And.NotContain("_schema").And.NotContain("_id").And.NotContain("_p");
        defaultDelete.Should().Contain("ScopedRow").And.Contain("Id").And.NotContain("_schema").And.NotContain("_id").And.NotContain("_p");
        alphaInsert.Should().Contain("alpha_schema").And.Contain("alpha_rows").And.Contain("alpha_id").And.Contain("alpha_");
        alphaDelete.Should().Contain("alpha_schema").And.Contain("alpha_rows").And.Contain("alpha_id").And.Contain("alpha_");
        betaInsert.Should().Contain("beta_schema").And.Contain("beta_rows").And.Contain("beta_id").And.Contain("beta_");
        betaDelete.Should().Contain("beta_schema").And.Contain("beta_rows").And.Contain("beta_id").And.Contain("beta_");
        alphaInsert.Should().NotBe(betaInsert);
        alphaDelete.Should().NotBe(betaDelete);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void ScopedInsertAndDeleteReturning_RenderScopeSpecificReturnedColumns(string provider)
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseScopeResolver(new TenantScopeResolver());
        using var context = CreateContext(provider, builder);
        var minimum = 4;

        var insertSql = context.CreateInsertBuilder<ScopedRow>()
            .Value(row => row.Id, 19)
            .Returning(row => row.Id)
            .ToSql();
        var deleteSql = context.CreateDeleteBuilder<ScopedRow>()
            .Where(row => row.Id > minimum)
            .Returning(row => row.Id)
            .ToSql();

        insertSql.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id").And.Contain("tenant_");
        deleteSql.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id").And.Contain("tenant_");

        if (provider == "sqlserver")
        {
            var insertCapture = context.CreateInsertBuilder<ScopedRow>()
                .Value(row => row.Id, 19)
                .Returning(row => row.Id)
                .OutputIntoTableVariable("@inserted", "[tenant_id] int")
                .ToSql();
            var deleteCapture = context.CreateDeleteBuilder<ScopedRow>()
                .Where(row => row.Id > minimum)
                .Returning(row => row.Id)
                .OutputIntoTableVariable("@deleted", "[tenant_id] int")
                .ToSql();

            insertCapture.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id");
            deleteCapture.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id");
        }
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void ScopedUpdateReturning_RendersMappedTargetAndReturnedColumns(string provider)
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseScopeResolver(new TenantScopeResolver());
        using var context = CreateContext(provider, builder);
        var minimum = 4;

        var sql = context.CreateUpdateBuilder<ScopedRow>()
            .Set(row => row.Id, 19)
            .Where(row => row.Id > minimum)
            .Returning(row => row.Id)
            .ToSql();

        sql.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id").And.Contain("tenant_");

        if (provider == "sqlserver")
        {
            var captured = context.CreateUpdateBuilder<ScopedRow>()
                .Set(row => row.Id, 20)
                .Where(row => row.Id > minimum)
                .Returning(row => row.Id)
                .OutputIntoTableVariable("@updated", "[tenant_id] int")
                .ToSql();
            captured.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id");
        }
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void ScopedDefaultValuesInsert_RendersMappedTargetAndReturningColumns(string provider)
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseScopeResolver(new TenantScopeResolver());
        using var context = CreateContext(provider, builder);

        var sql = context.CreateInsertBuilder<ScopedDefaultRow>()
            .Returning(row => row.Id)
            .ToSql();

        sql.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("clickhouse")]
    public void ScopedInsertSelect_RendersMappedTargetAndSource(string provider)
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseScopeResolver(new TenantScopeResolver());
        using var context = CreateContext(provider, builder);
        var minimum = 2;
        var source = context.From<ScopedRow>().Where(row => row.Id > minimum);

        var sql = context.CreateInsertBuilder<ScopedRow>()
            .Values(source, row => new { row.Id })
            .ToSql();

        sql.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id").And.Contain("tenant_");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    public void ScopedMerge_EntityAndBatchSourcesRenderResolvedTargetAndColumns(string provider)
    {
        var builder = new DataContextBuilder().UseEntityMetadataProvider(new ScopedMetadataProvider());
        using var context = CreateContext(provider, builder);
        var scope = new global::NextORM.Core.MappingScope("tenant");
        var entity = context.CreateMergeBuilder<ScopedRow>()
            .Using(new ScopedRow { Id = 1, Label = "one" })
            .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert()
            .WithScope(scope).ToSql();
        var batch = context.CreateMergeBuilder<ScopedRow>()
            .Using(new[] { new ScopedRow { Id = 2, Label = "two" } })
            .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert()
            .WithScope(scope).ToSql();

        entity.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id").And.Contain("tenant_label").And.Contain("tenant_");
        batch.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id").And.Contain("tenant_label").And.Contain("tenant_");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    public void ScopedMerge_CollectionSourceRendersScopedNamesAndParameterPrefix(string provider)
    {
        var builder = new DataContextBuilder().UseEntityMetadataProvider(new ScopedMetadataProvider());
        using var context = CreateContext(provider, builder);
        var scope = new global::NextORM.Core.MappingScope("batch");
        IEnumerable<ScopedRow> source = new[]
        {
            new ScopedRow { Id = 3, Label = "three" },
            new ScopedRow { Id = 4, Label = "four" }
        };

        var sql = context.CreateMergeBuilder<ScopedRow>()
            .Using(source)
            .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert()
            .WithScope(scope)
            .ToSql();

        sql.Should().Contain("batch_schema").And.Contain("batch_rows").And.Contain("batch_id")
            .And.Contain("batch_label").And.MatchRegex("[$@:]batch_");
    }

    [Fact]
    public void ScopedMerge_QuerySourceRendersResolvedTargetAndSource()
    {
        var builder = new DataContextBuilder().UseEntityMetadataProvider(new ScopedMetadataProvider());
        using var context = CreateContext("sqlserver", builder);
        var scope = new global::NextORM.Core.MappingScope("tenant");
        var source = context.From<ScopedRow>().WithScope(scope).Select(row => row);

        var sql = context.CreateMergeBuilder<ScopedRow>()
            .Using(source)
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .WithScope(scope)
            .ToSql();

        sql.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id").And.Contain("tenant_label");
    }

    [Fact]
    public void ScopedTruncate_RendersResolvedTableAndSchema()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseScopeResolver(new TenantScopeResolver());
        using var context = CreateContext("postgres", builder);

        var sql = context.CreateTruncateBuilder<ScopedRow>().ToSql();

        sql.Should().Contain("tenant_schema").And.Contain("tenant_rows");
    }

    [Fact]
    public void ScopedBatchDml_RendersEachMutationWithItsResolvedScope()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseScopeResolver(new TenantScopeResolver());
        using var context = CreateContext("sqlserver", builder);
        var query = context.From<ScopedRow>().Select(row => row.Id);
        var sql = context.CreateBatchBuilder()
            .Insert(context.CreateInsertBuilder<ScopedRow>().Value(row => row.Id, 17).Value(row => row.Label, "batch"))
            .Update(context.CreateUpdateBuilder<ScopedRow>().Set(row => row.Label, "updated").Where(row => row.Id == 17))
            .Delete(context.CreateDeleteBuilder<ScopedRow>().Where(row => row.Id == 17))
            .AddQuery(query)
            .ToSql();

        sql.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id").And.Contain("tenant_label").And.Contain("tenant_");
    }

    [Fact]
    public void ScopedBatchUpdateOutputCapture_RendersScopeSpecificColumns()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseScopeResolver(new TenantScopeResolver());
        using var context = CreateContext("sqlserver", builder);
        var batch = context.CreateBatchBuilder();
        var update = context.CreateUpdateBuilder<ScopedRow>()
            .Set(row => row.Label, "updated")
            .Where(row => row.Id == 17)
            .Returning(row => row.Id);
        batch.OutputIntoTableVariable(update, "@updated", "[tenant_id] int");
        var resultQuery = batch.FromOutput<int>("@updated").Select(id => id);

        var sql = batch.Query(resultQuery).ToSql();

        sql.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id").And.Contain("@updated");
    }

    [Fact]
    public void ScopedUpdateCte_RendersMappedMutationTargetAndReturnedColumn()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseScopeResolver(new TenantScopeResolver());
        using var context = CreateContext("postgres", builder);
        var minimum = 10;
        var mutation = context.CreateUpdateBuilder<ScopedRow>()
            .Set(row => row.Label, "cte")
            .Where(row => row.Id > minimum)
            .Returning(row => row.Id);

        var query = context.With("changed", mutation)
            .From("changed")
            .Select(row => row);
        var prepared = (DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(query, false, true, CancellationToken.None);
        var sql = prepared.DbCommand.CommandText;

        sql.Should().Contain("tenant_schema").And.Contain("tenant_rows").And.Contain("tenant_id").And.Contain("tenant_");
    }

    [Fact]
    public async Task ScopedEntityKeyUpdateAndDelete_ExecuteAgainstResolvedMapping()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseScopeResolver(new TenantScopeResolver());
        using var context = (SqliteDataContext)CreateContext("sqlite", builder);
        await context.ExecuteRawAsync("ATTACH DATABASE ':memory:' AS tenant_schema", TestContext.Current.CancellationToken);
        await context.ExecuteRawAsync("CREATE TABLE tenant_schema.tenant_rows (tenant_id INTEGER PRIMARY KEY, tenant_label TEXT NOT NULL)", TestContext.Current.CancellationToken);

        context.CreateInsertBuilder<ScopedRow>()
            .Value(row => row.Id, 9)
            .Value(row => row.Label, "before")
            .Insert();

        var updated = context.Update(new ScopedRow { Id = 9, Label = "after" });
        var deleted = context.Delete(new ScopedRow { Id = 9, Label = "after" });

        updated.Should().Be(1);
        deleted.Should().Be(1);
    }

    private static IDataContext CreateContext(string provider, DataContextBuilder? builder = null)
    {
        builder ??= new DataContextBuilder();
        return provider switch
        {
            "sqlite" => new SqliteDataContext("Data Source=:memory:", builder),
            "postgres" => new PostgresDataContext("Host=localhost;Database=test;Username=test;Password=test", builder),
            "sqlserver" => new SqlServerDataContext("Server=localhost;Database=test;User Id=test;Password=test;TrustServerCertificate=True", builder),
            "mysql" => new MySqlDataContext("Server=localhost;Database=test;User=test;Password=test", builder),
            "clickhouse" => new ClickHouseDataContext("Host=localhost;Database=test;Username=test;Password=test", builder),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
    }

    private sealed class ScopedRow
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    private sealed class ScopedDefaultRow
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
    }

    private sealed class ScopedMetadataProvider : IEntityMetadataProvider
    {
        private readonly DefaultEntityMetadataProvider _inner = new();

        public IEntityMetadata GetMetadata(Type entityType, in global::NextORM.Core.MappingScope scope)
        {
            var metadata = _inner.GetMetadata(entityType, global::NextORM.Core.MappingScope.Default);
            var suffix = scope.Key ?? "default";
            return new ScopedMetadata($"{suffix}_rows", $"{suffix}_schema",
                metadata.Properties.Select(property => (IPropertyMetadata)new ScopedProperty(property.PropertyInfo,
                    $"{suffix}_{(property.PropertyInfo.Name == nameof(ScopedRow.Id) ? "id" : "label")}")).ToArray());
        }

        public string GetColumnName(PropertyInfo property, in global::NextORM.Core.MappingScope scope, INamingConvention? convention)
            => $"{scope.Key ?? "default"}_{(property.Name == nameof(ScopedRow.Id) ? "id" : "label")}";
    }

    private sealed class TenantScopeResolver : IEntityScopeResolver
    {
        public global::NextORM.Core.MappingScope Resolve(Type entityType) => new("tenant");

        public global::NextORM.Core.MappingScope Resolve(Type entityType, object entity) => new("tenant");

        public global::NextORM.Core.MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues) => new("tenant");
    }

    private sealed class MutableScopeResolver : IEntityScopeResolver
    {
        public global::NextORM.Core.MappingScope Scope { get; set; } = global::NextORM.Core.MappingScope.Default;
        public global::NextORM.Core.MappingScope Resolve(Type entityType) => Scope;
        public global::NextORM.Core.MappingScope Resolve(Type entityType, object entity) => Scope;
        public global::NextORM.Core.MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Scope;
    }

    private sealed class ScopedMetadata(string tableName, string schema, IReadOnlyList<IPropertyMetadata> properties) : IEntityMetadata
    {
        public IReadOnlyList<IPropertyMetadata> Properties { get; } = properties;
        public string? TableName { get; } = tableName;
        public string? Schema { get; } = schema;
    }

    private sealed class ScopedProperty(PropertyInfo property, string column) : IPropertyMetadata
    {
        public PropertyInfo PropertyInfo { get; } = property;
        public string ColumnName { get; } = column;
    }
}
