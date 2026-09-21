using System.Reflection;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Guards the dialect capability contract: every class-A capability is expressed as a nullable
/// capability object, where the object's presence <em>is</em> the capability, so a dialect can never
/// report support without supplying its rendering. A non-null object must render every name/method it
/// reports as supported without throwing. Complements the SQL-shape assertions in the per-provider test
/// projects, which only validate the renderers that already exist.
/// </summary>
public sealed class DialectCapabilityContractTests
{
    private static readonly string[] SessionNames =
        ["current_user", "session_user", "current_schema", "current_database", "version"];

    private static readonly string[] UuidNames = ["gen_random_uuid", "uuidv7"];

    private static readonly string[] XmlNames = ["value", "query", "exist"];

    private static PivotExpression CreatePivot()
    {
        var factory = typeof(PivotExpression).GetMethod("ForPivot", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (PivotExpression)factory.Invoke(
            null,
            [new FromExpression(typeof(object)), typeof(object), PivotAggregate.Sum, null!, null!, new[] { PivotValue.Create("1") }])!;
    }

    private static PivotExpression CreateUnpivot()
    {
        var factory = typeof(PivotExpression).GetMethod("ForUnpivot", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (PivotExpression)factory.Invoke(
            null,
            [new FromExpression(typeof(object)), typeof(object), "val", "nm", new[] { UnpivotColumn.Create("c1") }])!;
    }

    private static IEnumerable<ISqlDialect> AllDialects()
    {
        // Provider assemblies are referenced but lazily loaded, so load every NextORM assembly that
        // sits in the test output before reflecting over the dialects it contains.
        var assemblies = Directory.EnumerateFiles(AppContext.BaseDirectory, "nextorm.*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null && !name.EndsWith(".tests", StringComparison.OrdinalIgnoreCase))
            .Select(name => Assembly.Load(name!));

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type is { IsClass: true, IsAbstract: false } && typeof(ISqlDialect).IsAssignableFrom(type))
                    yield return (ISqlDialect)Activator.CreateInstance(type)!;
            }
        }
    }

    /// <summary>
    /// A non-null capability object implies its renderer does not throw: every name/method the object
    /// reports as supported must render.
    /// </summary>
    [Fact]
    public void SupportedCapabilityObjects_ShouldRenderWithoutThrowing()
    {
        var dialects = AllDialects().ToList();
        dialects.Should().NotBeEmpty("the provider dialect assemblies must be loaded");

        foreach (var dialect in dialects)
        {
            if (dialect.Iif is { } iif)
                iif.Render("1 = 1", "1", "0").Should().NotBeNullOrEmpty();

            if (dialect.SessionInfoFunctions is { } session)
            {
                foreach (var name in SessionNames)
                {
                    if (!session.Supports(name))
                        continue;

                    session.Render(name).Should().NotBeNullOrEmpty();
                }
            }

            if (dialect.UuidGenerators is { } uuid)
            {
                foreach (var name in UuidNames)
                {
                    if (!uuid.Supports(name))
                        continue;

                    uuid.Render(name).Should().NotBeNullOrEmpty();
                }
            }

            if (dialect.LimitBy is { } limitBy)
                limitBy.Render(5, 0, ["a", "b"]).Should().NotBeNullOrEmpty();

            if (dialect.XmlFunctions is { } xml)
            {
                foreach (var name in XmlNames)
                {
                    if (!xml.Supports(name))
                        continue;

                    xml.Render(name, "[x]", ["'a'"]).Should().NotBeNullOrEmpty();
                }
            }

            if (dialect.StringSplit is { } split)
                split.Render("','", "[x]").Should().NotBeNullOrEmpty();

            if (dialect.DateConversion is { } dateConversion)
                dateConversion.Render("to_date", ["[x]"]).Should().NotBeNullOrEmpty();

            if (dialect.SequenceAggregates is { } sequence)
                sequence.Render("window_funnel", "10", "[t], [c]").Should().NotBeNullOrEmpty();

            if (dialect.UniqAggregates is { } uniq)
                uniq.Render("uniq", "[x]").Should().NotBeNullOrEmpty();

            if (dialect.QuantileAggregates is { } quantile)
            {
                quantile.Render("quantile", "0.5", "[x]").Should().NotBeNullOrEmpty();
                quantile.RenderMedian("[x]").Should().NotBeNullOrEmpty();
            }

            if (dialect.MultiIf is { } multiIf)
                multiIf.Render(["[c]", "1", "0"], typeof(int)).Should().NotBeNullOrEmpty();

            if (dialect.DistinctOn is { } distinctOn)
                distinctOn.Render(["[x]"]).Should().NotBeNullOrEmpty();

            if (dialect.TableSample is { } tableSample)
            {
                foreach (var method in Enum.GetValues<TableSampleMethod>())
                {
                    if (!tableSample.Supports(method))
                        continue;

                    tableSample.Render(method, 10, null).Should().NotBeNullOrEmpty();
                }
            }

            if (dialect.ArrayJoinClause is { } arrayJoin)
                arrayJoin.Render(ArrayJoinKind.Left, ["[x]"]).Should().NotBeNullOrEmpty();

            if (dialect.Lock is { } lockRenderer)
                lockRenderer.Render(LockMode.Share).Should().NotBeNullOrEmpty();

            if (dialect.Pivot is { } pivotRenderer)
            {
                var pivot = CreatePivot();
                pivotRenderer.RenderPivot(pivot, "[src]", "[agg]", "[for]", "t1").Should().NotBeNullOrEmpty();

                var unpivot = CreateUnpivot();
                pivotRenderer.RenderUnpivot(unpivot, "[src]", "t1").Should().NotBeNullOrEmpty();
            }
        }
    }

    [Fact]
    public void AtLeastOneDialect_ShouldExposeEachClassACapability()
    {
        var dialects = AllDialects().ToList();

        dialects.Should().Contain(d => d.Iif != null);
        dialects.Should().Contain(d => d.SessionInfoFunctions != null);
        dialects.Should().Contain(d => d.UuidGenerators != null);
        dialects.Should().Contain(d => d.LimitBy != null);
        dialects.Should().Contain(d => d.XmlFunctions != null);
        dialects.Should().Contain(d => d.StringSplit != null);
        dialects.Should().Contain(d => d.DateConversion != null);
        dialects.Should().Contain(d => d.SequenceAggregates != null);
        dialects.Should().Contain(d => d.UniqAggregates != null);
        dialects.Should().Contain(d => d.QuantileAggregates != null);
        dialects.Should().Contain(d => d.MultiIf != null);
        dialects.Should().Contain(d => d.DistinctOn != null);
        dialects.Should().Contain(d => d.TableSample != null);
        dialects.Should().Contain(d => d.Pivot != null);
        dialects.Should().Contain(d => d.ArrayJoinClause != null);
        dialects.Should().Contain(d => d.Lock != null);
    }
}
