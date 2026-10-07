using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Postgres;
using Xunit;

namespace NextORM.PublicExtensibility.Tests;

/// <summary>
/// External-consumer proof for the supported extension boundary of <see cref="PostgresDialect"/> and
/// <see cref="ClickHouseDialect"/>. This assembly has no <c>InternalsVisibleTo</c> grant from
/// <c>nextorm.core</c>, <c>nextorm.postgres</c> or <c>nextorm.clickhouse</c>: it subclasses both
/// dialects through their public constructors, implements the public <see cref="IExtremeRowRenderer"/>,
/// installs it through the subclass and forces the portable strategy with <see langword="null"/>.
/// </summary>
public class ExternalDialectExtensibilityTests
{
    private const string PostgresConnectionString =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    private const string ClickHouseConnectionString =
        "Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    // ---- no friend access: metadata guard --------------------------------------------------

    [Fact]
    public void ReferencedProductionAssemblies_ShouldNotGrantInternalsVisibleToThisConsumer()
    {
        var consumerName = typeof(ExternalDialectExtensibilityTests).Assembly.GetName().Name;
        consumerName.Should().Be("nextorm.publicextensibility.tests");

        var assemblies = new[]
        {
            typeof(IDataContext).Assembly,
            typeof(PostgresDataContext).Assembly,
            typeof(ClickHouseDataContext).Assembly,
        };

        foreach (var assembly in assemblies)
        {
            var granted = assembly
                .GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(a => a.AssemblyName)
                .ToArray();

            granted.Should().NotContain(name =>
                name == consumerName
                || name.StartsWith(consumerName + ",", StringComparison.Ordinal));
        }
    }

    // ---- public constructors and unchanged defaults ----------------------------------------

    [Fact]
    public void PostgresDialectSubclass_ShouldUseThePublicParameterlessConstructor()
    {
        new CustomPostgresDialect().ExtremeRowRenderer.Should().BeNull();

        // The shipped default is unchanged: the built-in dialect still opts into the native renderer.
        new PostgresDialect().ExtremeRowRenderer.Should().NotBeNull();
    }

    [Fact]
    public void PostgresDialectSubclass_ShouldUseThePublicVersionConstructorIncludingNull()
    {
        new CustomPostgresDialect(serverVersion: null, renderer: null).ExtremeRowRenderer.Should().BeNull();
        new CustomPostgresDialect(new Version(15, 0), renderer: null).ExtremeRowRenderer.Should().BeNull();

        new PostgresDialect(null).ExtremeRowRenderer.Should().NotBeNull();
        new PostgresDialect(new Version(15, 0)).ExtremeRowRenderer.Should().NotBeNull();
    }

    [Fact]
    public void ClickHouseDialectSubclass_ShouldUseThePublicParameterlessConstructor()
    {
        new CustomClickHouseDialect().ExtremeRowRenderer.Should().BeNull();
        new ClickHouseDialect().ExtremeRowRenderer.Should().NotBeNull();
    }

    // ---- custom renderer installed through the subclass: accept path -----------------------

    [Fact]
    public void PostgresCustomRenderer_AcceptPath_ShouldBeInvoked()
    {
        var renderer = new RecordingExtremeRowRenderer(canRender: true);
        using var ctx = new CustomPostgresDataContext(PostgresConnectionString, new DataContextBuilder(), renderer);
        var e = ctx.From<PublicExtensionEntity>();

        var sql = SqlOf(ctx, e.SelectWhereMax(x => x.Score).ToCommand());

        renderer.CanRenderCalls.Should().BeGreaterThan(0);
        renderer.RenderCalls.Should().Be(1);
        sql.Should().Contain("custom-extreme-renderer");
        sql.Should().NotContain("row_number()");
    }

    [Fact]
    public void ClickHouseCustomRenderer_AcceptPath_ShouldBeInvoked()
    {
        var renderer = new RecordingExtremeRowRenderer(canRender: true);
        using var ctx = new CustomClickHouseDataContext(ClickHouseConnectionString, new DataContextBuilder(), renderer);
        var e = ctx.From<PublicExtensionEntity>();

        var sql = SqlOf(ctx, e.SelectWhereMax(x => x.Score).ToCommand());

        renderer.CanRenderCalls.Should().BeGreaterThan(0);
        renderer.RenderCalls.Should().Be(1);
        sql.Should().Contain("custom-extreme-renderer");
        sql.Should().NotContain("row_number()");
    }

    // ---- custom renderer: decline path (CanRender => false) ---------------------------------

    [Fact]
    public void PostgresCustomRenderer_DeclinePath_ShouldProducePortableSqlWithoutRender()
    {
        var renderer = new RecordingExtremeRowRenderer(canRender: false);
        using var ctx = new CustomPostgresDataContext(PostgresConnectionString, new DataContextBuilder(), renderer);
        var e = ctx.From<PublicExtensionEntity>();

        var sql = SqlOf(ctx, e.SelectWhereMax(x => x.Score).ToCommand());

        renderer.CanRenderCalls.Should().BeGreaterThan(0);
        renderer.RenderCalls.Should().Be(0);
        sql.Should().Contain("row_number()");
        sql.Should().NotContain("custom-extreme-renderer");
    }

    [Fact]
    public void ClickHouseCustomRenderer_DeclinePath_ShouldProducePortableSqlWithoutRender()
    {
        var renderer = new RecordingExtremeRowRenderer(canRender: false);
        using var ctx = new CustomClickHouseDataContext(ClickHouseConnectionString, new DataContextBuilder(), renderer);
        var e = ctx.From<PublicExtensionEntity>();

        var sql = SqlOf(ctx, e.SelectWhereMax(x => x.Score).ToCommand());

        renderer.CanRenderCalls.Should().BeGreaterThan(0);
        renderer.RenderCalls.Should().Be(0);
        sql.Should().Contain("row_number()");
        sql.Should().NotContain("custom-extreme-renderer");
    }

    // ---- override returning null: portable strategy, via real SQL generation ----------------

    [Fact]
    public void PostgresNullOverride_ShouldProducePortableSql()
    {
        using var ctx = new CustomPostgresDataContext(PostgresConnectionString, new DataContextBuilder(), renderer: null);
        var e = ctx.From<PublicExtensionEntity>();

        var sql = SqlOf(ctx, e.SelectWhereMax(x => x.Score).ToCommand());

        sql.Should().Contain("row_number()");
        sql.Should().NotContain("distinct on");
        sql.Should().NotContain("limit 1");
    }

    [Fact]
    public void ClickHouseNullOverride_ShouldProducePortableSql()
    {
        using var ctx = new CustomClickHouseDataContext(ClickHouseConnectionString, new DataContextBuilder(), renderer: null);
        var e = ctx.From<PublicExtensionEntity>();

        var sql = SqlOf(ctx, e.SelectWhereMax(x => x.Score).ToCommand());

        sql.Should().Contain("row_number()");
        sql.Should().NotContain("argMax");
        sql.Should().NotContain("argMin");
    }
}

/// <summary>A mapped entity for the public query path, materializable from the placeholder SQL.</summary>
[SqlTable("public_extension_entity")]
public class PublicExtensionEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("score")]
    public int? Score { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

/// <summary>A public <see cref="IExtremeRowRenderer"/> implementation that records its callbacks.</summary>
internal sealed class RecordingExtremeRowRenderer : IExtremeRowRenderer
{
    private readonly bool _canRender;

    public RecordingExtremeRowRenderer(bool canRender) => _canRender = canRender;

    public int CanRenderCalls { get; private set; }

    public int RenderCalls { get; private set; }

    public bool CanRender(ExtremeRowDescription description)
    {
        CanRenderCalls++;
        return _canRender;
    }

    public string Render(ExtremeRowRenderRequest request)
    {
        RenderCalls++;
        return request.SourceSql + " /* custom-extreme-renderer */";
    }
}

/// <summary>A PostgreSQL dialect subclass created through the public constructors.</summary>
internal sealed class CustomPostgresDialect : PostgresDialect
{
    private readonly IExtremeRowRenderer? _renderer;

    public CustomPostgresDialect()
    {
    }

    public CustomPostgresDialect(Version? serverVersion, IExtremeRowRenderer? renderer)
        : base(serverVersion) => _renderer = renderer;

    public override IExtremeRowRenderer? ExtremeRowRenderer => _renderer;
}

/// <summary>A ClickHouse dialect subclass created through the public constructor.</summary>
internal sealed class CustomClickHouseDialect : ClickHouseDialect
{
    private readonly IExtremeRowRenderer? _renderer;

    public CustomClickHouseDialect()
    {
    }

    public CustomClickHouseDialect(IExtremeRowRenderer? renderer) => _renderer = renderer;

    public override IExtremeRowRenderer? ExtremeRowRenderer => _renderer;
}

/// <summary>Installs a custom PostgreSQL dialect through the overridable <c>Dialect</c> hook.</summary>
internal sealed class CustomPostgresDataContext : PostgresDataContext
{
    private readonly ISqlDialect _dialect;

    public CustomPostgresDataContext(string connectionString, DataContextBuilder optionsBuilder, IExtremeRowRenderer? renderer)
        : base(connectionString, optionsBuilder) => _dialect = new CustomPostgresDialect(null, renderer);

    public override ISqlDialect Dialect => _dialect;
}

/// <summary>Installs a custom ClickHouse dialect through the overridable <c>Dialect</c> hook.</summary>
internal sealed class CustomClickHouseDataContext : ClickHouseDataContext
{
    private readonly ISqlDialect _dialect;

    public CustomClickHouseDataContext(string connectionString, DataContextBuilder optionsBuilder, IExtremeRowRenderer? renderer)
        : base(connectionString, optionsBuilder) => _dialect = new CustomClickHouseDialect(renderer);

    public override ISqlDialect Dialect => _dialect;
}
