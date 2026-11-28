using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.MappingVariant;

public sealed class MappingVariantIsolationTests
{
    [Fact]
    public void PrefixedParameterProvider_ShouldPrefixNamesFromInnerProvider()
    {
        var provider = new PrefixedParameterProvider(new FixedParameterProvider("@p0"), "tenant_");

        provider.GetParamName().Should().Be("tenant_@p0");
    }

    [Fact]
    public void Merge_EntityAndBatchSources_UseExplicitScopedTargetAndColumns()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new AlphaVariantResolver());
        using var context = new SqlServerDataContext("Server=localhost", builder);
        var scope = new global::NextORM.Core.MappingVariant("beta");

        var entitySql = context.CreateMergeBuilder<ScopedRow>()
            .Using(new ScopedRow { Id = 4, Name = "one" }).OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert().ToSql();
        var batchSql = context.CreateMergeBuilder<ScopedRow>(scope)
            .Using([new ScopedRow { Id = 4, Name = "one" }, new ScopedRow { Id = 5, Name = "two" }])
            .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert().ToSql();
        var querySql = context.CreateMergeBuilder<ScopedRow>(scope)
            .Using(context.From<ScopedRow>(scope))
            .OnKeys().WhenMatched().ThenUpdate().WhenNotMatched().ThenInsert().ToSql();

        entitySql.Should().Contain("alpha_schema.alpha_rows").And.Contain("alpha_id");
        batchSql.Should().Contain("beta_schema.beta_rows").And.Contain("beta_id");
        querySql.Should().Contain("beta_schema.beta_rows").And.Contain("beta_id");
    }

    [Fact]
    public void Merge_EntityBatchAndQuerySources_DoNotReuseSqlAcrossScopes()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new FixedVariantResolver());
        using var context = new SqlServerDataContext("Server=localhost", builder);
        var alpha = new global::NextORM.Core.MappingVariant("alpha");
        var beta = new global::NextORM.Core.MappingVariant("beta");
        var entity = new ScopedRow { Id = 4, Name = "one" };
        var batch = new[] { entity, new ScopedRow { Id = 5, Name = "two" } };

        var alphaEntity = context.CreateMergeBuilder<ScopedRow>(alpha).Using(entity)
            .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert().ToSql();
        var betaEntity = context.CreateMergeBuilder<ScopedRow>(beta).Using(entity)
            .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert().ToSql();
        var alphaBatch = context.CreateMergeBuilder<ScopedRow>(alpha).Using(batch)
            .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert().ToSql();
        var betaBatch = context.CreateMergeBuilder<ScopedRow>(beta).Using(batch)
            .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert().ToSql();
        var alphaQuery = context.CreateMergeBuilder<ScopedRow>(alpha)
            .Using(context.From<ScopedRow>(alpha))
            .OnKeys().WhenMatched().ThenUpdate().WhenNotMatched().ThenInsert().ToSql();
        var betaQuery = context.CreateMergeBuilder<ScopedRow>(beta)
            .Using(context.From<ScopedRow>(beta))
            .OnKeys().WhenMatched().ThenUpdate().WhenNotMatched().ThenInsert().ToSql();

        alphaEntity.Should().Contain("alpha_schema.alpha_rows").And.Contain("alpha_id");
        betaEntity.Should().Contain("beta_schema.beta_rows").And.Contain("beta_id");
        alphaBatch.Should().Contain("alpha_schema.alpha_rows").And.Contain("alpha_id");
        betaBatch.Should().Contain("beta_schema.beta_rows").And.Contain("beta_id");
        alphaQuery.Should().Contain("alpha_schema.alpha_rows").And.Contain("alpha_id");
        betaQuery.Should().Contain("beta_schema.beta_rows").And.Contain("beta_id");
        alphaEntity.Should().NotBe(betaEntity);
        alphaBatch.Should().NotBe(betaBatch);
        alphaQuery.Should().NotBe(betaQuery);
    }

    [Fact]
    public void Truncate_UsesEachResolvedScopeOnTheSameContext()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new AlternatingVariantResolver());
        using var context = new SqlServerDataContext("Server=localhost", builder);

        var alpha = context.CreateTruncateBuilder<ScopedRow>().ToSql();
        var beta = context.CreateTruncateBuilder<ScopedRow>().ToSql();

        alpha.Should().Contain("alpha_schema.alpha_rows");
        beta.Should().Contain("beta_schema.beta_rows");
        alpha.Should().NotBe(beta);
    }

    [Fact]
    public void Truncate_UsesResolvedScopedTableAndSchema()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new AlphaVariantResolver());
        using var context = new SqlServerDataContext("Server=localhost", builder);

        context.CreateTruncateBuilder<ScopedRow>().ToSql()
            .Should().Contain("alpha_schema.alpha_rows");
    }

    [Fact]
    public void InsertAndDelete_UseScopedTargetAndParameterPrefix()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new AlphaVariantResolver());
        using var context = new SqliteDataContext("Data Source=:memory:", builder);

        var insert = context.CreateInsertBuilder<ScopedRow>().Value(row => row.Id, 5);
        var delete = context.CreateDeleteBuilder<ScopedRow>().Where(row => row.Id == 5);

        insert.ToSql().Should().Contain("alpha_schema.alpha_rows").And.Contain("alpha_id").And.Contain("alpha_");
        delete.ToSql().Should().Contain("alpha_schema.alpha_rows").And.Contain("alpha_id").And.Contain("alpha_");
    }

    [Fact]
    public void DmlFactories_ExplicitVariantOverridesContextResolver()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new AlphaVariantResolver());
        using var context = new SqlServerDataContext("Server=localhost", builder);
        var beta = new global::NextORM.Core.MappingVariant("beta");

        context.CreateInsertBuilder<ScopedRow>(beta).Value(row => row.Id, 1).ToSql()
            .Should().Contain("beta_schema.beta_rows").And.Contain("beta_id");
        context.CreateUpdateBuilder<ScopedRow>(beta).Set(row => row.Name, "updated").Where(row => row.Id == 1).ToSql()
            .Should().Contain("beta_schema.beta_rows").And.Contain("beta_name").And.Contain("beta_id");
        context.CreateDeleteBuilder<ScopedRow>(beta).Where(row => row.Id == 1).ToSql()
            .Should().Contain("beta_schema.beta_rows").And.Contain("beta_id");
        context.CreateTruncateBuilder<ScopedRow>(beta).ToSql()
            .Should().Contain("beta_schema.beta_rows");
    }

    [Fact]
    public void BulkInsertOptions_UseVariant_RendersResolvedTargetAndRefusesNativeMode()
    {
        using var context = new SqlServerDataContext("Server=localhost",
            new DataContextBuilder().UseEntityMetadataProvider(new ScopedMetadataProvider()));
        var beta = new global::NextORM.Core.MappingVariant("beta");
        var rows = new[] { new ScopedRow { Id = 1, Name = "bulk" } };

        context.CreateBulkInsertBuilder<ScopedRow>(options => options.UseVariant(beta)).Values(rows).ToSql()
            .Should().Contain("beta_schema.beta_rows").And.Contain("beta_id");
        var native = context.CreateBulkInsertBuilder<ScopedRow>(
            options => options.UseVariant(beta).Mode(BulkInsertMode.Native)).Values(rows);
        FluentActions.Invoking(() => native.BulkInsert()).Should().Throw<NotSupportedException>()
            .WithMessage("*Mode.Native*MappingVariant*");
    }

    [Fact]
    public void FromOptions_UseVariant_OverridesContextResolverForOneQuery()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new AlphaVariantResolver());
        using var context = new SqliteDataContext("Data Source=:memory:", builder);
        var beta = new global::NextORM.Core.MappingVariant("beta");

        var explicitQuery = context.From<ScopedRow>(options: options => options.UseVariant(beta))
            .Select(row => row.Id);
        var implicitQuery = context.From<ScopedRow>().Select(row => row.Id);
        var explicitSql = ((DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            explicitQuery, false, true, CancellationToken.None)).DbCommand.CommandText;
        var implicitSql = ((DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            implicitQuery, false, true, CancellationToken.None)).DbCommand.CommandText;

        explicitSql.Should().Contain("beta_schema.beta_rows").And.Contain("beta_id");
        implicitSql.Should().Contain("alpha_schema.alpha_rows");
    }

    [Fact]
    public void DefaultScope_PreservesLegacySelectAndInsertSqlAndParameterNames()
    {
        using var context = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var minimum = 3;
        var query = context.From<ScopedRow>(global::NextORM.Core.MappingVariant.Default)
            .Where(row => row.Id > minimum).Select(row => row.Id);
        var prepared = BoundQueryInvocation<int>.GetPlan(context.GetPreparedQueryCommand(
            query, false, true, CancellationToken.None))!;

        prepared.DbCommand.CommandText.Should().Be("select Id\n from ScopedRow\n where (Id > $minimum)");
        prepared.DbCommand.Parameters.Cast<System.Data.Common.DbParameter>()
            .Select(parameter => parameter.ParameterName).Should().Equal("minimum");

        var insertSql = context.CreateInsertBuilder<ScopedRow>().Value(row => row.Id, 5).ToSql();
        insertSql.Should().Be("insert into ScopedRow (Id) values ($p0)");
        ParameterNamesIn(insertSql).Should().Equal("p0");
    }

    [Fact]
    public void DefaultScope_IdenticalQueriesReuseCachedPlan_AndNonDefaultScopeDoesNotReuseIt()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new FixedVariantResolver());
        using var context = new SqliteDataContext("Data Source=:memory:", builder);

        var defaultFirst = BoundQueryInvocation<int>.GetPlan(context.GetPreparedQueryCommand(
            BuildScopedQuery(context, global::NextORM.Core.MappingVariant.Default, 3), false, true, CancellationToken.None))!;
        var defaultAgain = BoundQueryInvocation<int>.GetPlan(context.GetPreparedQueryCommand(
            BuildScopedQuery(context, global::NextORM.Core.MappingVariant.Default, 3), false, true, CancellationToken.None));
        var alpha = BoundQueryInvocation<int>.GetPlan(context.GetPreparedQueryCommand(
            BuildScopedQuery(context, new global::NextORM.Core.MappingVariant("alpha"), 3), false, true, CancellationToken.None))!;

        defaultAgain.Should().BeSameAs(defaultFirst, "an identical default-scope query must be served by its cached plan");
        alpha.Should().NotBeSameAs(defaultFirst, "a non-default scope must not reuse the default-scope cache entry");
        alpha.DbCommand.CommandText.Should().NotBe(defaultFirst.DbCommand.CommandText);
    }

    [Fact]
    public void SameContext_DifferentScopes_IsolateFromCachePlanSqlAndParameters()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new FixedVariantResolver());
        using var context = new SqliteDataContext("Data Source=:memory:", builder);

        var alpha = Prepare(context, new global::NextORM.Core.MappingVariant("alpha"), 3);
        var beta = Prepare(context, new global::NextORM.Core.MappingVariant("beta"), 3);
        var alphaAgain = Prepare(context, new global::NextORM.Core.MappingVariant("alpha"), 7);

        alpha.Sql.Should().Contain("alpha_schema").And.Contain("alpha_rows").And.Contain("alpha_id");
        beta.Sql.Should().Contain("beta_schema").And.Contain("beta_rows").And.Contain("beta_id");
        alpha.Sql.Should().NotBe(beta.Sql, "the cached FROM expression and prepared plan must not cross scopes");
        alpha.Parameters.Should().ContainSingle(p => p.ParameterName.Contains("alpha_", StringComparison.Ordinal));
        beta.Parameters.Should().ContainSingle(p => p.ParameterName.Contains("beta_", StringComparison.Ordinal));
        alpha.Parameters[0].ParameterName.Should().NotBe(beta.Parameters[0].ParameterName,
            "scope prefixes prevent parameter collisions");
        alphaAgain.Sql.Should().Be(alpha.Sql, "the same scoped plan remains reusable");
        alphaAgain.Parameters[0].Value.Should().Be(7, "plan reuse refreshes the captured value");
    }

    [Fact]
    public void Join_ReadSource_UsesTheQueryMappingVariant()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new FixedVariantResolver());
        using var context = new SqliteDataContext("Data Source=:memory:", builder);
        var alpha = new global::NextORM.Core.MappingVariant("alpha");
        var query = context.From<ScopedRow>(alpha)
            .Join(context.From<ScopedRow>(), (left, right) => left.Id == right.Id)
            .Select(pair => pair.Item1.Id);
        var sql = ((DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            query, false, true, CancellationToken.None)).DbCommand.CommandText;

        sql.Should().Contain("alpha_schema.alpha_rows").And.Contain("default_schema.default_rows")
            .And.Contain("t1.alpha_id = t2.Id");
    }

    [Fact]
    public void Join_ExplicitRightSource_KeepsItsOwnMappingVariant()
    {
        using var context = new SqliteDataContext("Data Source=:memory:",
            new DataContextBuilder().UseEntityMetadataProvider(new ScopedMetadataProvider()));
        var alpha = new global::NextORM.Core.MappingVariant("alpha");
        var beta = new global::NextORM.Core.MappingVariant("beta");

        var query = context.From<ScopedRow>(alpha)
            .Join(context.From<ScopedRow>(beta), (left, right) => left.Id == right.Id)
            .Select(pair => pair.Item2.Name);
        var sql = ((DbPreparedQueryCommand<string>)context.GetPreparedQueryCommand(
            query, false, true, CancellationToken.None)).DbCommand.CommandText;

        sql.Should().Contain("alpha_schema.alpha_rows").And.Contain("beta_schema.beta_rows")
            .And.Contain("beta_name");
    }

    [Fact]
    public void Subquery_ReadSource_UsesItsMappingVariant()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new FixedVariantResolver());
        using var context = new SqliteDataContext("Data Source=:memory:", builder);
        var alpha = new global::NextORM.Core.MappingVariant("alpha");
        var beta = new global::NextORM.Core.MappingVariant("beta");
        var subquery = context.From<ScopedRow>(beta).Where(row => row.Id > 1);

        var query = context.From<ScopedRow>(alpha)
            .Where(row => subquery.Any()).Select(row => row.Id);
        var sql = ((DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            query, false, true, CancellationToken.None)).DbCommand.CommandText;

        sql.Should().Contain("alpha_schema.alpha_rows").And.Contain("beta_schema.beta_rows")
            .And.Contain("alpha_id");
    }

    [Fact]
    public void Cte_ReadSource_UsesItsMappingVariant()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new FixedVariantResolver());
        using var context = new SqliteDataContext("Data Source=:memory:", builder);
        var alpha = new global::NextORM.Core.MappingVariant("alpha");
        var beta = new global::NextORM.Core.MappingVariant("beta");
        var cte = context.From<ScopedRow>(beta).ToCommand().AsCte("scoped_rows");

        var query = context.From(cte, alpha).Select(row => row.Id);
        var sql = ((DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            query, false, true, CancellationToken.None)).DbCommand.CommandText;

        sql.Should().Contain("beta_schema.beta_rows").And.Contain("beta_id").And.NotContain("alpha_id");
    }

    [Fact]
    public void SameContext_ScopedThenDefaultThenOtherScope_KeepMetadataIsolated()
    {
        var builder = new DataContextBuilder()
            .UseEntityMetadataProvider(new ScopedMetadataProvider())
            .UseVariantResolver(new FixedVariantResolver());
        using var context = new SqliteDataContext("Data Source=:memory:", builder);

        var alpha = Prepare(context, new global::NextORM.Core.MappingVariant("alpha"), 1);
        var defaultQuery = context.From<ScopedRow>().Where(row => row.Id > 1).Select(row => row.Id);
        var defaultPrepared = (DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            defaultQuery, false, true, CancellationToken.None);
        var beta = Prepare(context, new global::NextORM.Core.MappingVariant("beta"), 1);
        var defaultAgain = (DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            defaultQuery, false, true, CancellationToken.None);

        alpha.Sql.Should().Contain("alpha_schema.alpha_rows");
        beta.Sql.Should().Contain("beta_schema.beta_rows");
        defaultPrepared.DbCommand.CommandText.Should().Contain("default_schema.default_rows");
        defaultAgain.DbCommand.CommandText.Should().Be(defaultPrepared.DbCommand.CommandText);
        alpha.Sql.Should().NotBe(defaultPrepared.DbCommand.CommandText);
        beta.Sql.Should().NotBe(defaultPrepared.DbCommand.CommandText);
    }

    [Fact]
    public async Task MetadataProvider_ConcurrentScopes_ReturnIndependentMetadata()
    {
        var provider = new ScopedMetadataProvider();
        var alphaScope = new global::NextORM.Core.MappingVariant("alpha");
        var betaScope = new global::NextORM.Core.MappingVariant("beta");

        var results = await Task.WhenAll(Enumerable.Range(0, 128).Select(i => Task.Run(() =>
        {
            var scope = i % 2 == 0 ? alphaScope : betaScope;
            var metadata = provider.GetMetadata(typeof(ScopedRow), scope);
            return (Scope: scope.Key, metadata.TableName, metadata.Schema,
                Column: provider.GetColumnName(typeof(ScopedRow).GetProperty(nameof(ScopedRow.Id))!, scope, null));
        })));

        foreach (var result in results)
        {
            result.TableName.Should().Be($"{result.Scope}_rows");
            result.Schema.Should().Be($"{result.Scope}_schema");
            result.Column.Should().Be($"{result.Scope}_id");
        }
    }

    private static (string Sql, (string ParameterName, object? Value)[] Parameters) Prepare(
        SqliteDataContext context, global::NextORM.Core.MappingVariant variant, int minimum)
    {
        var command = BuildScopedQuery(context, variant, minimum);
        var invocation = context.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        var plan = BoundQueryInvocation<int>.GetPlan(invocation)!;
        var values = (invocation as BoundQueryInvocation<int>)?.Values;
        var parameters = plan.DbCommandParams.Cast<System.Data.Common.DbParameter>()
            .Select((parameter, ordinal) => (parameter.ParameterName, values is null ? parameter.Value : values[ordinal]))
            .ToArray();
        return (plan.CommandText, parameters);
    }

    private static QueryCommand<int> BuildScopedQuery(
        SqliteDataContext context, global::NextORM.Core.MappingVariant variant, int minimum)
        => context.From<ScopedRow>(variant)
            .Where(row => row.Id > minimum)
            .Select(row => row.Id);

    private static string[] ParameterNamesIn(string sql)
        => sql.Split([' ', ',', '(', ')', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.StartsWith("@", StringComparison.Ordinal)
                || token.StartsWith("$", StringComparison.Ordinal))
            .Select(token => token[1..])
            .ToArray();

    private sealed class ScopedRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class FixedVariantResolver : IEntityVariantResolver
    {
        public global::NextORM.Core.MappingVariant Resolve(Type entityType) => global::NextORM.Core.MappingVariant.Default;
        public global::NextORM.Core.MappingVariant Resolve(Type entityType, object entity) => global::NextORM.Core.MappingVariant.Default;
        public global::NextORM.Core.MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues) => global::NextORM.Core.MappingVariant.Default;
    }

    private sealed class AlphaVariantResolver : IEntityVariantResolver
    {
        private static readonly global::NextORM.Core.MappingVariant Scope = new("alpha");
        public global::NextORM.Core.MappingVariant Resolve(Type entityType) => Scope;
        public global::NextORM.Core.MappingVariant Resolve(Type entityType, object entity) => Scope;
        public global::NextORM.Core.MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Scope;
    }

    private sealed class AlternatingVariantResolver : IEntityVariantResolver
    {
        private int _next;
        public global::NextORM.Core.MappingVariant Resolve(Type entityType)
            => Interlocked.Increment(ref _next) % 2 == 1
                ? new global::NextORM.Core.MappingVariant("alpha")
                : new global::NextORM.Core.MappingVariant("beta");
        public global::NextORM.Core.MappingVariant Resolve(Type entityType, object entity) => Resolve(entityType);
        public global::NextORM.Core.MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Resolve(entityType);
    }

    private sealed class ScopedMetadataProvider : IEntityMetadataProvider
    {
        private readonly DefaultEntityMetadataProvider _inner = new();

        public IEntityMetadata GetMetadata(Type entityType, in global::NextORM.Core.MappingVariant variant)
        {
            var metadata = _inner.GetMetadata(entityType, global::NextORM.Core.MappingVariant.Default);
            var suffix = variant.Key ?? "default";
            return new ScopedMetadata($"{suffix}_rows", $"{suffix}_schema",
                metadata.Properties.Select(p => (IPropertyMetadata)new ScopedProperty(
                    p.PropertyInfo, $"{suffix}_{p.PropertyInfo.Name.ToLowerInvariant()}")).ToArray());
        }

        public string GetColumnName(PropertyInfo property, in global::NextORM.Core.MappingVariant variant, INamingConvention? convention)
            => $"{variant.Key ?? "default"}_{property.Name.ToLowerInvariant()}";
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

    private sealed class FixedParameterProvider(string parameterName) : IParameterProvider
    {
        public string GetParamName() => parameterName;
    }
}
