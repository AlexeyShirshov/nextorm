using System.Reflection;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Guards the dialect capability contract: once a dialect reports a capability as supported it must
/// supply its own rendering and never fall back to a base stub that throws at runtime. Complements
/// the SQL-shape assertions in the per-provider test projects, which only validate the renderers that
/// already exist.
/// </summary>
public sealed class DialectCapabilityContractTests
{
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

    /// <summary>Capability flag name paired with the base renderer that throws unless overridden.</summary>
    public static TheoryData<string, string> ThrowingRenderers => new()
    {
        { nameof(ISqlDialect.SupportsIif), nameof(ISqlDialect.MakeIif) },
        { nameof(ISqlDialect.SupportsSessionInfoFunctions), nameof(ISqlDialect.MakeSessionInfoFunction) },
        { nameof(ISqlDialect.SupportsUuidGenerators), nameof(ISqlDialect.MakeUuidGenerator) },
        { nameof(ISqlDialect.SupportsLimitBy), nameof(ISqlDialect.MakeLimitBy) },
        { nameof(ISqlDialect.SupportsDateConversionFunctions), nameof(ISqlDialect.MakeDateConversion) },
    };

    [Theory]
    [MemberData(nameof(ThrowingRenderers))]
    public void SupportedCapability_ShouldOverrideItsBaseRenderer(string flagName, string rendererName)
    {
        var dialects = AllDialects().ToList();
        dialects.Should().NotBeEmpty("the provider dialect assemblies must be loaded");

        var flag = typeof(ISqlDialect).GetProperty(flagName)!;

        foreach (var dialect in dialects)
        {
            if (!(bool)flag.GetValue(dialect)!)
                continue;

            var renderer = dialect.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(m => m.Name == rendererName);

            renderer.DeclaringType.Should().NotBe(
                typeof(SqlDialectBase),
                $"{dialect.GetType().Name} reports {flagName} but inherits the throwing base {rendererName}");
        }
    }
}
