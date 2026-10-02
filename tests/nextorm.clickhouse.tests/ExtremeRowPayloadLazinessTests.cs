using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// Flow evidence for #155 on ClickHouse: the native dispatch builds one factory-backed
/// <see cref="ExtremeRowDescription"/>, and ClickHouse's renderer reads
/// <see cref="ExtremeRowDescription.Payload"/> exactly once while deciding eligibility. The renderer is a
/// transparent wrapper around the real dialect renderer (reached through <c>base.ExtremeRowRenderer</c>),
/// so the SQL stays byte-identical to the stock context while the production-built description is
/// observed. Materialization is read as <see cref="Lazy{T}.IsValueCreated"/> on that instance and the
/// memoized identity of <see cref="ExtremeRowDescription.Payload"/>; the factory-invocation count itself
/// is pinned by the DTO test in <c>nextorm.core.tests</c>.
/// </summary>
public class ExtremeRowPayloadLazinessTests
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm";

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    private static bool IsPayloadMaterialized(ExtremeRowDescription description)
    {
        var field = typeof(ExtremeRowDescription)
            .GetField("_payload", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var lazy = field.GetValue(description)!;
        var isValueCreated = lazy.GetType().GetProperty("IsValueCreated")!;
        return (bool)isValueCreated.GetValue(lazy)!;
    }

    [Fact]
    public void GlobalNative_ShouldRenderIdenticalSqlAndMaterializePayload()
    {
        using var real = ClickHouseTestContext.Create();
        var realSql = SqlOf(real, real.From<ExtremeNativeEntity>().SelectWhereMax(x => x.K1).ToCommand());

        using var ctx = new CapturingClickHouseDataContext(PlaceholderConnectionString);
        var sql = SqlOf(ctx, ctx.From<ExtremeNativeEntity>().SelectWhereMax(x => x.K1).ToCommand());

        sql.Should().Be(realSql, "the transparent wrapper must not change the native SQL");
        ctx.CapturingDialect.Renderer.CanRenderCalls.Should().Be(1);
        var description = ctx.CapturingDialect.Renderer.Description!;
        IsPayloadMaterialized(description).Should()
            .BeTrue("the ClickHouse renderer reads the payload to decide tuple eligibility");

        var first = description.Payload;
        var second = description.Payload;
        ReferenceEquals(first, second).Should()
            .BeTrue("repeated reads must keep returning the single memoized payload");
    }

    [Fact]
    public void GroupedNative_ShouldRenderIdenticalSqlAndMaterializePayload()
    {
        using var real = ClickHouseTestContext.Create();
        var realSql = SqlOf(
            real,
            real.From<ExtremeNativeEntity>()
                .SelectWhereMax(x => x.K1, ExtremeRowTies.One, x => new { x.Id }).ToCommand());

        using var ctx = new CapturingClickHouseDataContext(PlaceholderConnectionString);
        var sql = SqlOf(
            ctx,
            ctx.From<ExtremeNativeEntity>()
                .SelectWhereMax(x => x.K1, ExtremeRowTies.One, x => new { x.Id }).ToCommand());

        sql.Should().Be(realSql);
        ctx.CapturingDialect.Renderer.CanRenderCalls.Should().Be(1);
        IsPayloadMaterialized(ctx.CapturingDialect.Renderer.Description!).Should().BeTrue();
    }

    [Fact]
    public void IneligibleKey_ShouldDeclineBeforeReadingThePayload()
    {
        // A floating-point key is rejected by AreIntegralDirectColumns(Keys); the && chain short-circuits
        // before AreSupportedPayloadColumns reads the payload, so the eligibility pass stays free of the
        // payload projection on this rejected native candidate.
        using var real = ClickHouseTestContext.Create();
        var realSql = SqlOf(real, real.From<IFloatNativeEntity>().SelectWhereMax(x => x.D, x => new { x.Id }));

        using var ctx = new CapturingClickHouseDataContext(PlaceholderConnectionString);
        var sql = SqlOf(ctx, ctx.From<IFloatNativeEntity>().SelectWhereMax(x => x.D, x => new { x.Id }));

        sql.Should().Be(realSql);
        ctx.CapturingDialect.Renderer.CanRenderCalls.Should().Be(1);
        IsPayloadMaterialized(ctx.CapturingDialect.Renderer.Description!).Should()
            .BeFalse("the payload is read only after the key/group checks accept the candidate");
        sql.Should().Contain("row_number()");
    }

    /// <summary>Transparent wrapper around the stock ClickHouse renderer that captures the description.</summary>
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

    /// <summary>ClickHouse rendering whose renderer is the transparent capturing wrapper.</summary>
    internal sealed class CapturingClickHouseDialect : ClickHouseDialect
    {
        public CapturingClickHouseDialect()
            => Renderer = new CapturingExtremeRowRenderer(base.ExtremeRowRenderer!);

        public CapturingExtremeRowRenderer Renderer { get; }

        public override IExtremeRowRenderer? ExtremeRowRenderer => Renderer;
    }

    /// <summary>A distinct context type so the plan cache cannot alias it with the stock context.</summary>
    internal sealed class CapturingClickHouseDataContext : ClickHouseDataContext
    {
        public CapturingClickHouseDataContext(string connectionString)
            : base(connectionString, new DataContextBuilder())
            => CapturingDialect = new CapturingClickHouseDialect();

        public CapturingClickHouseDialect CapturingDialect { get; }

        public override ISqlDialect Dialect => CapturingDialect;
    }
}
