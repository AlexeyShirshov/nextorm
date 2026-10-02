using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// Flow evidence for #155 on PostgreSQL: the native dispatch builds one eligibility
/// <see cref="ExtremeRowDescription"/> whose payload is factory-backed, and PostgreSQL's renderer decides
/// from <see cref="ExtremeRowDescription.Keys"/>/<see cref="ExtremeRowDescription.Groups"/> only, so the
/// production-built description must reach the SQL pass with its payload never materialized. The SQL is
/// compared byte-for-byte against the stock PostgreSQL context, and the renderer is a transparent wrapper
/// around the real dialect renderer (obtained through <c>base.ExtremeRowRenderer</c>), so behaviour is
/// unchanged while the description is observed through reflection. The factory-invocation count itself is
/// pinned by the DTO test in <c>nextorm.core.tests</c>; here materialization is observed as
/// <see cref="Lazy{T}.IsValueCreated"/> on the very instance production built.
/// </summary>
public class ExtremeRowPayloadLazinessTests
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    /// <summary>Reads the private memo field's <c>IsValueCreated</c> without touching the payload itself.</summary>
    private static bool IsPayloadMaterialized(ExtremeRowDescription description)
    {
        var field = typeof(ExtremeRowDescription)
            .GetField("_payload", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var lazy = field.GetValue(description)!;
        var isValueCreated = lazy.GetType().GetProperty("IsValueCreated")!;
        return (bool)isValueCreated.GetValue(lazy)!;
    }

    [Fact]
    public void GlobalNative_ShouldRenderIdenticalSqlWithoutMaterializingPayload()
    {
        using var real = PostgresTestContext.Create();
        var realSql = SqlOf(real, real.From<ExtremeNativeEntity>().SelectWhereMax(x => x.Int).ToCommand());

        using var ctx = new CapturingPostgresDataContext(PlaceholderConnectionString);
        var sql = SqlOf(ctx, ctx.From<ExtremeNativeEntity>().SelectWhereMax(x => x.Int).ToCommand());

        sql.Should().Be(realSql, "the transparent wrapper must not change the native SQL");
        ctx.CapturingDialect.Renderer.CanRenderCalls.Should().Be(1);
        ctx.CapturingDialect.Renderer.Description.Should().NotBeNull();
        IsPayloadMaterialized(ctx.CapturingDialect.Renderer.Description!).Should()
            .BeFalse("the PostgreSQL renderer reads only Keys/Groups, never Payload");
    }

    [Fact]
    public void GroupedNative_ShouldRenderIdenticalSqlWithoutMaterializingPayload()
    {
        using var real = PostgresTestContext.Create();
        var realSql = SqlOf(
            real,
            real.From<ExtremeNativeEntity>()
                .SelectWhereMax(x => x.Int, ExtremeRowTies.One, x => new { x.Id }).ToCommand());

        using var ctx = new CapturingPostgresDataContext(PlaceholderConnectionString);
        var sql = SqlOf(
            ctx,
            ctx.From<ExtremeNativeEntity>()
                .SelectWhereMax(x => x.Int, ExtremeRowTies.One, x => new { x.Id }).ToCommand());

        sql.Should().Be(realSql);
        ctx.CapturingDialect.Renderer.CanRenderCalls.Should().Be(1);
        IsPayloadMaterialized(ctx.CapturingDialect.Renderer.Description!).Should().BeFalse();
    }

    [Fact]
    public void RejectedNativeCandidate_ShouldLeavePayloadUnmaterializedForThePortablePath()
    {
        // A string key is not natively eligible, so CanRender is called once and declines; the portable
        // lowering then runs. PostgreSQL must not have materialized the payload while deciding.
        using var ctx = new CapturingPostgresDataContext(PlaceholderConnectionString);
        var sql = SqlOf(
            ctx,
            ctx.From<IComplexEntity>().SelectWhereMax(x => x.String, x => new { x.Id }));

        ctx.CapturingDialect.Renderer.CanRenderCalls.Should().Be(1);
        IsPayloadMaterialized(ctx.CapturingDialect.Renderer.Description!).Should().BeFalse();
        sql.Should().Contain("row_number()");
    }

    /// <summary>
    /// A transparent wrapper around the stock PostgreSQL renderer (reached through
    /// <c>base.ExtremeRowRenderer</c>) that captures the production-built description and counts how many
    /// times eligibility was consulted.
    /// </summary>
    internal sealed class CapturingExtremeRowRenderer(IExtremeRowRenderer inner) : IExtremeRowRenderer
    {
        public ExtremeRowDescription? Description { get; private set; }

        public int CanRenderCalls { get; private set; }

        public bool CanRender(ExtremeRowDescription description)
        {
            Description = description;
            CanRenderCalls++;
            return inner.CanRender(description);
        }

        public string Render(ExtremeRowRenderRequest request) => inner.Render(request);
    }

    /// <summary>PostgreSQL rendering whose renderer is the transparent capturing wrapper.</summary>
    internal sealed class CapturingPostgresDialect : PostgresDialect
    {
        public CapturingPostgresDialect()
            => Renderer = new CapturingExtremeRowRenderer(base.ExtremeRowRenderer!);

        public CapturingExtremeRowRenderer Renderer { get; }

        public override IExtremeRowRenderer? ExtremeRowRenderer => Renderer;
    }

    /// <summary>A distinct context type so the plan cache cannot alias it with the stock context.</summary>
    internal sealed class CapturingPostgresDataContext : PostgresDataContext
    {
        public CapturingPostgresDataContext(string connectionString)
            : base(connectionString, new DataContextBuilder())
            => CapturingDialect = new CapturingPostgresDialect();

        public CapturingPostgresDialect CapturingDialect { get; }

        public override ISqlDialect Dialect => CapturingDialect;
    }
}
