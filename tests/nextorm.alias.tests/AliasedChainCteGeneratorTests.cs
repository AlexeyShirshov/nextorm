using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NextORM.AliasTests;

/// <summary>Regression coverage for aliases emitted when an aliased-chain CTE is a joined source.</summary>
public sealed class AliasedChainCteGeneratorTests
{
    private static readonly Type GeneratorType = Assembly
        .LoadFrom(Path.Combine(AppContext.BaseDirectory, "nextorm.core.sourcegenerator.dll"))
        .GetType("NextORM.Core.SourceGenerator.JoinAliasGenerator", throwOnError: true)!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Joined_aliased_chain_cte_emits_outer_alias_z(bool selectGroup)
    {
        var producer = selectGroup
            ? ".SelectGroup(x => new SupplierDelayRow { Id = x.PurchaseOrder.Id }).AsCte(\"weekly\");"
            : ".Select(x => new SupplierDelayRow { Id = x.PurchaseOrder.Id }).AsCte(\"weekly\");";
        var source = Header +
            "public sealed class PurchaseOrder { public int Id { get; set; } }\n" +
            "public sealed class Vendor { public int Id { get; set; } }\n" +
            "public sealed class SupplierDelayRow { public int Id { get; set; } }\n" +
            "public sealed class ProductionImpact { public int ProductId { get; set; } }\n" +
            "public static class Cases { public static void Run(IDataContext ctx, EntityBuilder<PurchaseOrder> orders, EntityBuilder<Vendor> vendors, EntityBuilder<ProductionImpact> impacts) {\n" +
            "var weekly = ctx.From<Order>().WithAlias(Alias.OrderDetail)\n" +
            ".Join<PurchaseOrder>(orders, (d, p) => d.OrderDetail.Id == p.Id, Alias.PurchaseOrder)\n" +
            ".Join<Vendor>(vendors, (p, v) => p.PurchaseOrder.Id == v.Id, Alias.Vendor)" + producer + "\n" +
            "_ = ctx.From(impacts).WithAlias(Alias.ProductionImpact)\n" +
            ".Join(weekly, (pi, sd) => pi.ProductionImpact.ProductId == sd.Id, Alias.Z); } }";

        var (run, output) = RunGenerator(source);
        run.Diagnostics.Should().BeEmpty();
        run.GeneratedTrees.Should().ContainSingle();
        var generated = run.GeneratedTrees.Single().ToString();
        generated.Should().Contain("Alias.ZMarker");
        generated.Should().Contain("AliasJoin_A1_ProductionImpact_A2_Z");
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the joined-CTE call must be represented by the emitted Alias.Z surface");
    }

    private const string Header =
        "using NextORM.Core;\n" +
        "using NextORM.Generated.AliasHarness;\n" +
        "namespace Harness;\n" +
        "public sealed class Order { public int Id { get; set; } }\n" +
        "public sealed class Person { public int Id { get; set; } }\n";

    private static (GeneratorDriverRunResult Run, Compilation Output) RunGenerator(string source)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
            foreach (var path in tpa.Split(Path.PathSeparator)) paths.Add(path);
        paths.Add(Path.Combine(AppContext.BaseDirectory, "nextorm.core.dll"));
        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "Microsoft.Extensions.*.dll")) paths.Add(file);
        ImmutableArray<MetadataReference> references = paths
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();
        var compilation = CSharpCompilation.Create("AliasHarness", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([
            ((IIncrementalGenerator)Activator.CreateInstance(GeneratorType, nonPublic: true)!).AsSourceGenerator()]);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, TestContext.Current.CancellationToken);
        return (driver.GetRunResult(), output);
    }
}
