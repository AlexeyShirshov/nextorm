using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteMappingVariantLiveTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void ScopedQuery_ReadsAlternateAttachedSchemaAndTable()
    {
        using var context = (DataContext)SqliteTestProvider.Instance.CreateContext(Builder());
        context.EnsureConnectionOpen();
        using (var setup = context.CreateCommand("ATTACH DATABASE ':memory:' AS scope_live; CREATE TABLE scope_live.scope_rows (live_id INTEGER PRIMARY KEY, live_name TEXT NOT NULL); INSERT INTO scope_live.scope_rows VALUES (41, 'scoped'); CREATE TABLE scope_rows (id INTEGER PRIMARY KEY, name TEXT NOT NULL); INSERT INTO scope_rows VALUES (99, 'default');"))
            setup.ExecuteNonQuery();

        var rows = context.From<ScopedRow>().WithVariant(Scope).OrderBy(row => row.Id).Select(row => row.Id).ToList();

        rows.Should().Equal(41);
    }

    [Fact]
    public void ScopedQuery_UsesEachProvidersColumnEvenWhenProvidersCompareEqual()
    {
        const string sql = """
            CREATE TABLE shared_scope_rows (first_id INTEGER NOT NULL, second_id INTEGER NOT NULL);
            INSERT INTO shared_scope_rows VALUES (41, 99);
            """;
        using var firstConnection = Seed(sql);
        using var secondConnection = Seed(sql);
        var firstProvider = new EqualColumnMetadataProvider("first_id");
        var secondProvider = new EqualColumnMetadataProvider("second_id");
        firstProvider.Equals(secondProvider).Should().BeTrue();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().UseEntityMetadataProvider(firstProvider));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().UseEntityMetadataProvider(secondProvider));

        Read(first).Should().Equal(41);
        Read(second).Should().Equal(99);
        Read(first).Should().Equal(41);

        static List<int> Read(DataContext context)
            => context.From<DefaultProviderRow>().WithVariant(Scope).OrderBy(row => row.Id).Select(row => row.Id).ToList();
    }

    [Fact]
    public void ScopedQuery_DoesNotKeepItsMetadataProviderAlive()
    {
        using var connection = Seed("CREATE TABLE shared_scope_rows (first_id INTEGER NOT NULL); INSERT INTO shared_scope_rows VALUES (41);");
        var provider = ExecuteAndGetProviderReference(connection);
        for (var i = 0; i < 5 && provider.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        provider.IsAlive.Should().BeFalse("the query plan and column-name caches should not root a caller's mapping service");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ExecuteAndGetProviderReference(SqliteConnection connection)
    {
        var provider = new EqualColumnMetadataProvider("first_id");
        using var context = new SqliteDataContext(connection, new DataContextBuilder().UseEntityMetadataProvider(provider));
        context.From<DefaultProviderRow>().WithVariant(Scope).OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(41);
        return new WeakReference(provider);
    }

    [Fact]
    public void DefaultVariant_UsesTheTableFromEachContextsMetadataProvider()
    {
        const string sql = """
            CREATE TABLE first_rows (Id INTEGER NOT NULL);
            INSERT INTO first_rows VALUES (41);
            CREATE TABLE second_rows (Id INTEGER NOT NULL);
            INSERT INTO second_rows VALUES (99);
            """;
        using var firstConnection = Seed(sql);
        using var secondConnection = Seed(sql);
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder()
            .UseEntityMetadataProvider(new LiveMetadataProvider("first_rows", "main")));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder()
            .UseEntityMetadataProvider(new LiveMetadataProvider("second_rows", "main")));
        first.From<DefaultProviderRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(41);
        second.From<DefaultProviderRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(99);
        first.From<DefaultProviderRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(41);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultVariant_UsesEachProvidersSchemaForTheSameTable(bool ordered)
    {
        const string sql = """
            ATTACH DATABASE ':memory:' AS first_schema;
            ATTACH DATABASE ':memory:' AS second_schema;
            CREATE TABLE first_schema.shared_rows (Id INTEGER NOT NULL);
            CREATE TABLE second_schema.shared_rows (Id INTEGER NOT NULL);
            INSERT INTO first_schema.shared_rows VALUES (41);
            INSERT INTO second_schema.shared_rows VALUES (99);
            """;
        using var firstConnection = Seed(sql);
        using var secondConnection = Seed(sql);
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder()
            .UseEntityMetadataProvider(new LiveMetadataProvider("shared_rows", "first_schema")));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder()
            .UseEntityMetadataProvider(new LiveMetadataProvider("shared_rows", "second_schema")));
        Read(first).Should().Equal(41);
        Read(second).Should().Equal(99);
        Read(first).Should().Equal(41);

        List<int> Read(DataContext context)
        {
            var query = context.From<DefaultProviderRow>();
            return ordered
                ? query.OrderBy(row => row.Id).Select(row => row.Id).ToList()
                : query.Select(row => row.Id).ToList();
        }
    }

    private static SqliteConnection Seed(string sql)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
        return connection;
    }

    [Fact]
    public void ScopedInsertUpdateDelete_OnlyMutateAttachedTarget()
    {
        using var context = (DataContext)SqliteTestProvider.Instance.CreateContext(DmlBuilder());
        context.EnsureConnectionOpen();
        using (var setup = context.CreateCommand("ATTACH DATABASE ':memory:' AS scope_live_dml; CREATE TABLE scope_live_dml.scope_dml_rows (live_id INTEGER PRIMARY KEY, live_name TEXT NOT NULL); INSERT INTO scope_live_dml.scope_dml_rows VALUES (1, 'scoped-keep'), (2, 'scoped-delete'); CREATE TABLE scope_dml_rows (id INTEGER PRIMARY KEY, name TEXT NOT NULL); INSERT INTO scope_dml_rows VALUES (1, 'default-sentinel');"))
            setup.ExecuteNonQuery();

        context.CreateInsertBuilder<ScopedDmlRow>()
            .Value(row => row.Id, 3)
            .Value(row => row.Name, "scoped-insert")
            .Insert().Should().Be(1);
        context.CreateUpdateBuilder<ScopedDmlRow>()
            .Set(row => row.Name, "scoped-updated")
            .Where(row => row.Id == 1)
            .Update().Should().Be(1);
        context.CreateDeleteBuilder<ScopedDmlRow>()
            .Where(row => row.Id == 2)
            .Delete().Should().Be(1);
        context.From<ScopedDmlRow>().WithVariant(new MappingVariant("livedml"))
            .OrderBy(row => row.Id).Select(row => row.Name).ToList()
            .Should().Equal("scoped-updated", "scoped-insert");
        using var defaultRead = context.CreateCommand("SELECT name FROM main.scope_dml_rows WHERE id = 1");
        defaultRead.ExecuteScalar().Should().Be("default-sentinel");
    }

    [Fact]
    public void ScopedTruncate_IsRejectedBySqlite()
    {
        using var context = (DataContext)SqliteTestProvider.Instance.CreateContext(DmlBuilder());

        var act = () => context.CreateTruncateBuilder<ScopedDmlRow>().Execute();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support TRUNCATE*");
    }

    private static DataContextBuilder DmlBuilder() => new DataContextBuilder()
        .UseEntityMetadataProvider(new LiveDmlMetadataProvider())
        .UseVariantResolver(new LiveDmlVariantResolver());

    private static DataContextBuilder Builder() => new DataContextBuilder()
        .UseEntityMetadataProvider(new LiveMetadataProvider())
        .UseVariantResolver(new LiveVariantResolver());

    private static readonly MappingVariant Scope = new("live");

    private sealed class ScopedRow { public int Id { get; set; } public string Name { get; set; } = ""; }
    private sealed class DefaultProviderRow { public int Id { get; set; } }

    private sealed class LiveVariantResolver : IEntityVariantResolver
    {
        public MappingVariant Resolve(Type entityType) => Scope;
        public MappingVariant Resolve(Type entityType, object entity) => Scope;
        public MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Scope;
    }

    private sealed class LiveMetadataProvider(string tableName = "scope_rows", string schema = "scope_live") : IEntityMetadataProvider
    {
        private readonly DefaultEntityMetadataProvider _inner = new();
        public IEntityMetadata GetMetadata(Type entityType, in MappingVariant variant)
        {
            var metadata = _inner.GetMetadata(entityType, MappingVariant.Default);
            return new Metadata(tableName, schema,
                metadata.Properties.Select(p => (IPropertyMetadata)new Property(p.PropertyInfo, "live_" + p.PropertyInfo.Name.ToLowerInvariant())).ToArray());
        }
        public string GetColumnName(PropertyInfo property, in MappingVariant variant, INamingConvention? convention) => "live_" + property.Name.ToLowerInvariant();
    }

    private sealed class EqualColumnMetadataProvider(string columnName) : IEntityMetadataProvider
    {
        public IEntityMetadata GetMetadata(Type entityType, in MappingVariant variant)
            => new Metadata("shared_scope_rows", "main", [new Property(entityType.GetProperty(nameof(DefaultProviderRow.Id))!, columnName)]);

        public string GetColumnName(PropertyInfo property, in MappingVariant variant, INamingConvention? convention) => columnName;

        public override bool Equals(object? obj) => obj is EqualColumnMetadataProvider;
        public override int GetHashCode() => typeof(EqualColumnMetadataProvider).GetHashCode();
    }

    private sealed class Metadata(string tableName, string schema, IReadOnlyList<IPropertyMetadata> properties) : IEntityMetadata
    {
        public IReadOnlyList<IPropertyMetadata> Properties { get; } = properties;
        public string? TableName => tableName;
        public string? Schema => schema;
    }

    private sealed class Property(PropertyInfo property, string columnName) : IPropertyMetadata
    {
        public PropertyInfo PropertyInfo { get; } = property;
        public string ColumnName { get; } = columnName;
    }
}

public sealed class PostgresMappingVariantLiveTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void ScopedQuery_ReadsAlternateSchemaAndTable()
    {
        using var context = (DataContext)PostgresTestProvider.Instance.CreateContext(Builder());
        context.EnsureConnectionOpen();
        using (var setup = context.CreateCommand("CREATE SCHEMA IF NOT EXISTS scope_live; CREATE TABLE IF NOT EXISTS scope_live.scope_rows (live_id INTEGER PRIMARY KEY, live_name TEXT NOT NULL); TRUNCATE TABLE scope_live.scope_rows; INSERT INTO scope_live.scope_rows VALUES (41, 'scoped'); CREATE TABLE IF NOT EXISTS public.scope_rows (id INTEGER PRIMARY KEY, name TEXT NOT NULL); TRUNCATE TABLE public.scope_rows; INSERT INTO public.scope_rows VALUES (99, 'default');"))
            setup.ExecuteNonQuery();

        var rows = context.From<ScopedRow>().WithVariant(Scope).OrderBy(row => row.Id).Select(row => row.Id).ToList();

        rows.Should().Equal(41);
    }

    [Fact]
    public void ScopedInsertUpdateDelete_OnlyMutateAlternateSchemaTarget()
    {
        using var context = (DataContext)PostgresTestProvider.Instance.CreateContext(DmlBuilder());
        context.EnsureConnectionOpen();
        using (var setup = context.CreateCommand("CREATE SCHEMA IF NOT EXISTS scope_live_dml; CREATE TABLE IF NOT EXISTS scope_live_dml.scope_dml_rows (live_id INTEGER PRIMARY KEY, live_name TEXT NOT NULL); TRUNCATE TABLE scope_live_dml.scope_dml_rows; INSERT INTO scope_live_dml.scope_dml_rows VALUES (1, 'scoped-keep'), (2, 'scoped-delete'); CREATE TABLE IF NOT EXISTS public.scope_dml_rows (id INTEGER PRIMARY KEY, name TEXT NOT NULL); TRUNCATE TABLE public.scope_dml_rows; INSERT INTO public.scope_dml_rows VALUES (1, 'default-sentinel');"))
            setup.ExecuteNonQuery();

        context.CreateInsertBuilder<ScopedDmlRow>()
            .Value(row => row.Id, 3)
            .Value(row => row.Name, "scoped-insert")
            .Insert().Should().Be(1);
        context.CreateUpdateBuilder<ScopedDmlRow>()
            .Set(row => row.Name, "scoped-updated")
            .Where(row => row.Id == 1)
            .Update().Should().Be(1);
        context.CreateDeleteBuilder<ScopedDmlRow>()
            .Where(row => row.Id == 2)
            .Delete().Should().Be(1);
        context.CreateMergeBuilder<ScopedDmlRow>()
            .Using(new ScopedDmlRow { Id = 1, Name = "scoped-merged" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge().Should().Be(1);
        context.CreateMergeBuilder<ScopedDmlRow>()
            .Using(new ScopedDmlRow { Id = 4, Name = "scoped-merge-insert" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge().Should().Be(1);

        context.From<ScopedDmlRow>().WithVariant(new MappingVariant("livedml"))
            .OrderBy(row => row.Id).Select(row => row.Name).ToList()
            .Should().Equal("scoped-merged", "scoped-insert", "scoped-merge-insert");
        using var defaultRead = context.CreateCommand("SELECT name FROM public.scope_dml_rows WHERE id = 1");
        defaultRead.ExecuteScalar().Should().Be("default-sentinel");
    }

    [Fact]
    public void ScopedMergeBatch_OnlyMutatesAlternateSchemaTarget()
    {
        using var context = (DataContext)PostgresTestProvider.Instance.CreateContext(DmlBuilder());
        context.EnsureConnectionOpen();
        using (var setup = context.CreateCommand("CREATE SCHEMA IF NOT EXISTS scope_live_dml; CREATE TABLE IF NOT EXISTS scope_live_dml.scope_dml_rows (live_id INTEGER PRIMARY KEY, live_name TEXT NOT NULL); TRUNCATE TABLE scope_live_dml.scope_dml_rows; INSERT INTO scope_live_dml.scope_dml_rows VALUES (1, 'scoped-keep'); CREATE TABLE IF NOT EXISTS public.scope_dml_rows (id INTEGER PRIMARY KEY, name TEXT NOT NULL); TRUNCATE TABLE public.scope_dml_rows; INSERT INTO public.scope_dml_rows VALUES (1, 'default-sentinel');"))
            setup.ExecuteNonQuery();

        context.CreateMergeBuilder<ScopedDmlRow>()
            .Using(new[] { new ScopedDmlRow { Id = 1, Name = "batch-merged" }, new ScopedDmlRow { Id = 2, Name = "batch-inserted" } })
            .WithVariant(new MappingVariant("livedml"))
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge().Should().Be(2);

        context.From<ScopedDmlRow>().WithVariant(new MappingVariant("livedml"))
            .OrderBy(row => row.Id).Select(row => row.Name).ToList()
            .Should().Equal("batch-merged", "batch-inserted");
        using var defaultRead = context.CreateCommand("SELECT name FROM public.scope_dml_rows WHERE id = 1");
        defaultRead.ExecuteScalar().Should().Be("default-sentinel");
    }

    [Fact]
    public void ScopedTruncate_OnlyTruncatesAlternateSchemaTarget()
    {
        using var context = (DataContext)PostgresTestProvider.Instance.CreateContext(DmlBuilder());
        context.EnsureConnectionOpen();
        using (var setup = context.CreateCommand("CREATE SCHEMA IF NOT EXISTS scope_live_dml; CREATE TABLE IF NOT EXISTS scope_live_dml.scope_dml_rows (live_id INTEGER PRIMARY KEY, live_name TEXT NOT NULL); TRUNCATE TABLE scope_live_dml.scope_dml_rows; INSERT INTO scope_live_dml.scope_dml_rows VALUES (1, 'scoped'); CREATE TABLE IF NOT EXISTS public.scope_dml_rows (id INTEGER PRIMARY KEY, name TEXT NOT NULL); TRUNCATE TABLE public.scope_dml_rows; INSERT INTO public.scope_dml_rows VALUES (1, 'default-sentinel');"))
            setup.ExecuteNonQuery();

        context.CreateTruncateBuilder<ScopedDmlRow>().Execute();

        context.From<ScopedDmlRow>().WithVariant(new MappingVariant("livedml")).Count().Should().Be(0);
        using var defaultRead = context.CreateCommand("SELECT name FROM public.scope_dml_rows WHERE id = 1");
        defaultRead.ExecuteScalar().Should().Be("default-sentinel");
    }

    private static DataContextBuilder DmlBuilder() => new DataContextBuilder()
        .UseEntityMetadataProvider(new LiveDmlMetadataProvider())
        .UseVariantResolver(new LiveDmlVariantResolver());

    private static DataContextBuilder Builder() => new DataContextBuilder()
        .UseEntityMetadataProvider(new LiveMetadataProvider())
        .UseVariantResolver(new LiveVariantResolver());

    private static readonly MappingVariant Scope = new("live");
    private sealed class ScopedRow { public int Id { get; set; } public string Name { get; set; } = ""; }
    private sealed class LiveVariantResolver : IEntityVariantResolver
    {
        public MappingVariant Resolve(Type entityType) => Scope;
        public MappingVariant Resolve(Type entityType, object entity) => Scope;
        public MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Scope;
    }
    private sealed class LiveMetadataProvider : IEntityMetadataProvider
    {
        private readonly DefaultEntityMetadataProvider _inner = new();
        public IEntityMetadata GetMetadata(Type entityType, in MappingVariant variant)
        {
            var metadata = _inner.GetMetadata(entityType, MappingVariant.Default);
            return new Metadata(metadata.Properties.Select(p => (IPropertyMetadata)new Property(p.PropertyInfo, "live_" + p.PropertyInfo.Name.ToLowerInvariant())).ToArray());
        }
        public string GetColumnName(PropertyInfo property, in MappingVariant variant, INamingConvention? convention) => "live_" + property.Name.ToLowerInvariant();
    }
    private sealed class Metadata(IReadOnlyList<IPropertyMetadata> properties) : IEntityMetadata
    {
        public IReadOnlyList<IPropertyMetadata> Properties { get; } = properties;
        public string? TableName => "scope_rows";
        public string? Schema => "scope_live";
    }
    private sealed class Property(PropertyInfo property, string columnName) : IPropertyMetadata
    {
        public PropertyInfo PropertyInfo { get; } = property;
        public string ColumnName { get; } = columnName;
    }
}

internal sealed class ScopedDmlRow
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

internal sealed class LiveDmlVariantResolver : IEntityVariantResolver
{
    private static readonly MappingVariant Scope = new("livedml");
    public MappingVariant Resolve(Type entityType) => Scope;
    public MappingVariant Resolve(Type entityType, object entity) => Scope;
    public MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Scope;
}

internal sealed class LiveDmlMetadataProvider : IEntityMetadataProvider
{
    private readonly DefaultEntityMetadataProvider _inner = new();

    public IEntityMetadata GetMetadata(Type entityType, in MappingVariant variant)
    {
        var metadata = _inner.GetMetadata(entityType, MappingVariant.Default);
        var properties = metadata.Properties.Select(property =>
            (IPropertyMetadata)new LiveDmlProperty(property.PropertyInfo,
                "live_" + property.PropertyInfo.Name.ToLowerInvariant())).ToArray();
        return new LiveDmlMetadata(properties);
    }

    public string GetColumnName(PropertyInfo property, in MappingVariant variant, INamingConvention? convention)
        => "live_" + property.Name.ToLowerInvariant();

    private sealed class LiveDmlMetadata(IReadOnlyList<IPropertyMetadata> properties) : IEntityMetadata
    {
        public IReadOnlyList<IPropertyMetadata> Properties { get; } = properties;
        public string? TableName => "scope_dml_rows";
        public string? Schema => "scope_live_dml";
    }

    private sealed class LiveDmlProperty(PropertyInfo property, string columnName) : IPropertyMetadata
    {
        public PropertyInfo PropertyInfo { get; } = property;
        public string ColumnName { get; } = columnName;
    }
}
