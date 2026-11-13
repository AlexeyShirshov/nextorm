using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteMappingScopeLiveTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void ScopedQuery_ReadsAlternateAttachedSchemaAndTable()
    {
        using var context = (DataContext)SqliteTestProvider.Instance.CreateContext(Builder());
        context.EnsureConnectionOpen();
        using (var setup = context.CreateCommand("ATTACH DATABASE ':memory:' AS scope_live; CREATE TABLE scope_live.scope_rows (live_id INTEGER PRIMARY KEY, live_name TEXT NOT NULL); INSERT INTO scope_live.scope_rows VALUES (41, 'scoped'); CREATE TABLE scope_rows (id INTEGER PRIMARY KEY, name TEXT NOT NULL); INSERT INTO scope_rows VALUES (99, 'default');"))
            setup.ExecuteNonQuery();

        var rows = context.From<ScopedRow>().WithScope(Scope).OrderBy(row => row.Id).Select(row => row.Id).ToList();

        rows.Should().Equal(41);
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
        context.From<ScopedDmlRow>().WithScope(new MappingScope("livedml"))
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
        .UseScopeResolver(new LiveDmlScopeResolver());

    private static DataContextBuilder Builder() => new DataContextBuilder()
        .UseEntityMetadataProvider(new LiveMetadataProvider())
        .UseScopeResolver(new LiveScopeResolver());

    private static readonly MappingScope Scope = new("live");

    private sealed class ScopedRow { public int Id { get; set; } public string Name { get; set; } = ""; }

    private sealed class LiveScopeResolver : IEntityScopeResolver
    {
        public MappingScope Resolve(Type entityType) => Scope;
        public MappingScope Resolve(Type entityType, object entity) => Scope;
        public MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Scope;
    }

    private sealed class LiveMetadataProvider : IEntityMetadataProvider
    {
        private readonly DefaultEntityMetadataProvider _inner = new();
        public IEntityMetadata GetMetadata(Type entityType, in MappingScope scope)
        {
            var metadata = _inner.GetMetadata(entityType, MappingScope.Default);
            return new Metadata(metadata.Properties.Select(p => (IPropertyMetadata)new Property(p.PropertyInfo, "live_" + p.PropertyInfo.Name.ToLowerInvariant())).ToArray());
        }
        public string GetColumnName(PropertyInfo property, in MappingScope scope, INamingConvention? convention) => "live_" + property.Name.ToLowerInvariant();
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

public sealed class PostgresMappingScopeLiveTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void ScopedQuery_ReadsAlternateSchemaAndTable()
    {
        using var context = (DataContext)PostgresTestProvider.Instance.CreateContext(Builder());
        context.EnsureConnectionOpen();
        using (var setup = context.CreateCommand("CREATE SCHEMA IF NOT EXISTS scope_live; CREATE TABLE IF NOT EXISTS scope_live.scope_rows (live_id INTEGER PRIMARY KEY, live_name TEXT NOT NULL); TRUNCATE TABLE scope_live.scope_rows; INSERT INTO scope_live.scope_rows VALUES (41, 'scoped'); CREATE TABLE IF NOT EXISTS public.scope_rows (id INTEGER PRIMARY KEY, name TEXT NOT NULL); TRUNCATE TABLE public.scope_rows; INSERT INTO public.scope_rows VALUES (99, 'default');"))
            setup.ExecuteNonQuery();

        var rows = context.From<ScopedRow>().WithScope(Scope).OrderBy(row => row.Id).Select(row => row.Id).ToList();

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

        context.From<ScopedDmlRow>().WithScope(new MappingScope("livedml"))
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
            .WithScope(new MappingScope("livedml"))
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge().Should().Be(2);

        context.From<ScopedDmlRow>().WithScope(new MappingScope("livedml"))
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

        context.From<ScopedDmlRow>().WithScope(new MappingScope("livedml")).Count().Should().Be(0);
        using var defaultRead = context.CreateCommand("SELECT name FROM public.scope_dml_rows WHERE id = 1");
        defaultRead.ExecuteScalar().Should().Be("default-sentinel");
    }

    private static DataContextBuilder DmlBuilder() => new DataContextBuilder()
        .UseEntityMetadataProvider(new LiveDmlMetadataProvider())
        .UseScopeResolver(new LiveDmlScopeResolver());

    private static DataContextBuilder Builder() => new DataContextBuilder()
        .UseEntityMetadataProvider(new LiveMetadataProvider())
        .UseScopeResolver(new LiveScopeResolver());

    private static readonly MappingScope Scope = new("live");
    private sealed class ScopedRow { public int Id { get; set; } public string Name { get; set; } = ""; }
    private sealed class LiveScopeResolver : IEntityScopeResolver
    {
        public MappingScope Resolve(Type entityType) => Scope;
        public MappingScope Resolve(Type entityType, object entity) => Scope;
        public MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Scope;
    }
    private sealed class LiveMetadataProvider : IEntityMetadataProvider
    {
        private readonly DefaultEntityMetadataProvider _inner = new();
        public IEntityMetadata GetMetadata(Type entityType, in MappingScope scope)
        {
            var metadata = _inner.GetMetadata(entityType, MappingScope.Default);
            return new Metadata(metadata.Properties.Select(p => (IPropertyMetadata)new Property(p.PropertyInfo, "live_" + p.PropertyInfo.Name.ToLowerInvariant())).ToArray());
        }
        public string GetColumnName(PropertyInfo property, in MappingScope scope, INamingConvention? convention) => "live_" + property.Name.ToLowerInvariant();
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

internal sealed class LiveDmlScopeResolver : IEntityScopeResolver
{
    private static readonly MappingScope Scope = new("livedml");
    public MappingScope Resolve(Type entityType) => Scope;
    public MappingScope Resolve(Type entityType, object entity) => Scope;
    public MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Scope;
}

internal sealed class LiveDmlMetadataProvider : IEntityMetadataProvider
{
    private readonly DefaultEntityMetadataProvider _inner = new();

    public IEntityMetadata GetMetadata(Type entityType, in MappingScope scope)
    {
        var metadata = _inner.GetMetadata(entityType, MappingScope.Default);
        var properties = metadata.Properties.Select(property =>
            (IPropertyMetadata)new LiveDmlProperty(property.PropertyInfo,
                "live_" + property.PropertyInfo.Name.ToLowerInvariant())).ToArray();
        return new LiveDmlMetadata(properties);
    }

    public string GetColumnName(PropertyInfo property, in MappingScope scope, INamingConvention? convention)
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
