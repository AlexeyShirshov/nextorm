using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.RawSources;

/// <summary>
/// Physical-name placeholders in raw SQL: <c>{schema}</c>, <c>{table}</c> and <c>{qualified}</c> are
/// expanded from the metadata of the entity bound to the source and the command's mapping variant while
/// the plan is built, so raw SQL can address a bucket's table without the caller composing the name.
/// </summary>
public sealed class RawSqlPlaceholderTests
{
    [Fact]
    public void TablePlaceholder_ShouldExpandToTheBucketTable()
    {
        using var context = CreateSharded(BucketPlacementMode.TableSuffix);

        var sql = SqlOf(context, "select Id, Name from {table}");

        sql.Should().Contain("PlaceholderRow_7");
        sql.Should().NotContain("{table}");
    }

    [Fact]
    public void SchemaPlaceholder_ShouldExpandToTheBucketSchema()
    {
        using var context = CreateSharded(BucketPlacementMode.SchemaPerBucket);

        var sql = SqlOf(context, "select Id, Name from {schema}.{table}");

        sql.Should().Contain("bucket_7.PlaceholderRow");
        sql.Should().NotContain("{schema}").And.NotContain("{table}");
    }

    [Fact]
    public void QualifiedPlaceholder_ShouldExpandToSchemaAndTable()
    {
        using var context = CreateSharded(BucketPlacementMode.SchemaPerBucket);

        var sql = SqlOf(context, "select Id, Name from {qualified}");

        sql.Should().Contain("bucket_7.PlaceholderRow");
    }

    [Fact]
    public void QualifiedPlaceholder_ShouldOmitAMissingSchema()
    {
        using var context = CreateSharded(BucketPlacementMode.TableSuffix);

        var sql = SqlOf(context, "select Id, Name from {qualified}");

        sql.Should().Contain("PlaceholderRow_7");
        sql.Should().NotContain("..");
    }

    [Fact]
    public void PlaceholderWithoutABinding_ShouldFail()
    {
        using var context = CreateSharded(BucketPlacementMode.TableSuffix);

        FluentActions.Invoking(() => SqlOf(context, "select Id from {table}", bind: false))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*BindEntity*");
    }

    [Fact]
    public void SchemaPlaceholderWithoutAMappedSchema_ShouldFail()
    {
        using var context = CreateSharded(BucketPlacementMode.TableSuffix);

        FluentActions.Invoking(() => SqlOf(context, "select Id from {schema}.{table}"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*declares no schema*");
    }

    [Fact]
    public void UnknownBraces_ShouldBeLeftAlone()
    {
        using var context = CreateSharded(BucketPlacementMode.TableSuffix);

        var sql = SqlOf(context, "select '{notatoken}' as x, Id from {table}");

        sql.Should().Contain("{notatoken}");
        sql.Should().Contain("PlaceholderRow_7");
    }

    [Fact]
    public void MappedSchema_ShouldBecomeTheBucketSchemaPrefix()
    {
        using var context = CreateSharded(BucketPlacementMode.SchemaPerBucket, new SchemaProvider());

        SqlOf(context, "select Id from {qualified}").Should().Contain("sales_7.PlaceholderRow");
    }

    [Fact]
    public void GetPhysicalTableName_ShouldMatchThePlacement()
    {
        using var suffix = CreateSharded(BucketPlacementMode.TableSuffix);
        using var schema = CreateSharded(BucketPlacementMode.SchemaPerBucket);

        suffix.GetPhysicalTableName<PlaceholderRow>().Should().Be(new PhysicalTableName(null, "PlaceholderRow"));
        suffix.GetPhysicalTableName<PlaceholderRow>(Scope(7)).Should().Be(new PhysicalTableName(null, "PlaceholderRow_7"));
        schema.GetPhysicalTableName<PlaceholderRow>(Scope(7))
            .Should().Be(new PhysicalTableName("bucket_7", "PlaceholderRow"));
        schema.GetPhysicalTableName<PlaceholderRow>(Scope(7)).QualifiedName.Should().Be("bucket_7.PlaceholderRow");
    }

    private static string SqlOf(SqliteDataContext context, string sql, bool bind = true)
    {
        if (!bind)
            return Prepare(context, context.FromSql(sql).Select(row => row["Id"].AsInt));

        var bound = context.FromSql(sql, parameters: null, variant: Scope(7)).BindEntity<PlaceholderRow>(["Id", "Name"]);
        return Prepare(context, bound.Select(row => row.Id));
    }

    private static string Prepare(SqliteDataContext context, QueryCommand<int> command)
        => ((DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            command, false, false, CancellationToken.None)).DbCommand.CommandText;

    private static global::NextORM.Core.MappingVariant Scope(int bucket)
        => new(bucket.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static SqliteDataContext CreateSharded(BucketPlacementMode mode, IEntityMetadataProvider? metadata = null)
    {
        var builder = new DataContextBuilder();
        if (metadata is not null)
            builder.UseEntityMetadataProvider(metadata);

        return new SqliteDataContext(builder
            .UseSharding(new ShardTopology(8, mode))
            .UseDataRouting(routing => routing.UseDefaultTopology("main", "Data Source=:memory:")));
    }

    /// <summary>Reports every entity as mapped to the <c>sales</c> schema.</summary>
    private sealed class SchemaProvider : IEntityMetadataProvider
    {
        private readonly DefaultEntityMetadataProvider _inner = new();

        public IEntityMetadata GetMetadata(Type entityType, in global::NextORM.Core.MappingVariant variant)
            => new SchemaMetadata(_inner.GetMetadata(entityType, variant));

        public string GetColumnName(PropertyInfo property, in global::NextORM.Core.MappingVariant variant, INamingConvention? convention)
            => _inner.GetColumnName(property, variant, convention);

        private sealed class SchemaMetadata(IEntityMetadata inner) : IEntityMetadata
        {
            public IReadOnlyList<IPropertyMetadata> Properties => inner.Properties;
            public string? TableName => inner.TableName;
            public string? Schema => "sales";
            public bool IsTableNameAuto => inner.IsTableNameAuto;
            public IPropertyMetadata? DynamicColumnsStore => inner.DynamicColumnsStore;
            public IReadOnlyList<IQueryFilterMetadata> Filters => inner.Filters;
            public IReadOnlyList<IRelationshipMetadata> Relationships => inner.Relationships;
        }
    }

    public sealed class PlaceholderRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
