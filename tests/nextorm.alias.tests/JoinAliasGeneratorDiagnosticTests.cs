using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NextORM.AliasTests;

/// <summary>
/// Hand-rolled harness for the join-alias generator diagnostics: feeds small snippets through a real
/// <see cref="CSharpGeneratorDriver"/> and asserts the exact diagnostic ids, severity and spans, plus
/// determinism and incrementality. The generator type lives in a build-only analyzer reference, so it
/// is loaded by reflection (the generator's assembly is copied next to the tests for that purpose).
/// </summary>
public class JoinAliasGeneratorDiagnosticTests
{
    private const string DiagnosticIdPrefix = "NORMGEN";

    private static readonly Type GeneratorType = Assembly
        .LoadFrom(Path.Combine(AppContext.BaseDirectory, "nextorm.core.sourcegenerator.dll"))
        .GetType("NextORM.Core.SourceGenerator.JoinAliasGenerator", throwOnError: true)!;

    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    private sealed record Harness(string Source, GeneratorDriverRunResult Run, Compilation Output);

    [Fact]
    public void Duplicate_alias_reports_NORMGEN001_at_the_second_alias()
    {
        var harness = RunDiagnosticCase(
            "var q = orders" +
            ".Join<Person>(people, (a, b) => true, Alias.Buyer)" +
            ".Join<Person>(people, (a, b) => true, Alias.Buyer);");

        AssertSingleDiagnostic(harness, "NORMGEN001", "Alias.Buyer");
    }

    [Fact]
    public void Alias_colliding_with_a_retained_ItemN_reports_NORMGEN002()
    {
        var harness = RunDiagnosticCase(
            "var q = orders.Join<Person>(people, (a, b) => true, Alias.Item2);");

        AssertSingleDiagnostic(harness, "NORMGEN002", "Alias.Item2");
    }

    [Fact]
    public void Invalid_alias_identifier_reports_NORMGEN003()
    {
        var harness = RunDiagnosticCase(
            "var q = orders.Join<Person>(people, (a, b) => true, Alias.Buyer_Name);");

        AssertSingleDiagnostic(harness, "NORMGEN003", "Alias.Buyer_Name");
    }

    [Fact]
    public void Ninth_slot_reports_NORMGEN004_at_the_eighth_alias()
    {
        var harness = RunDiagnosticCase(
            "var q = orders" +
            ".Join<Person>(people, (a, b) => true, Alias.A1)" +
            ".Join<Person>(people, (a, b) => true, Alias.A2)" +
            ".Join<Person>(people, (a, b) => true, Alias.A3)" +
            ".Join<Person>(people, (a, b) => true, Alias.A4)" +
            ".Join<Person>(people, (a, b) => true, Alias.A5)" +
            ".Join<Person>(people, (a, b) => true, Alias.A6)" +
            ".Join<Person>(people, (a, b) => true, Alias.A7)" +
            ".Join<Person>(people, (a, b) => true, Alias.A8);");

        var diagnostic = AssertSingleDiagnostic(harness, "NORMGEN004", "Alias.A8");
        diagnostic.GetMessage().Should().Contain("9");
    }

    [Fact]
    public void Non_approved_alias_argument_reports_NORMGEN005()
    {
        var harness = RunDiagnosticCase(
            "var q = orders.Join<Person>(people, (a, b) => true, Alias.Buyer.Approver);");

        AssertSingleDiagnostic(harness, "NORMGEN005", "Alias.Buyer.Approver");
    }

    [Fact]
    public void Non_normalizable_assembly_name_reports_NORMGEN006()
    {
        const string body = "var q = orders.Join<Person>(people, (a, b) => true, Alias.Buyer);";
        var compilation = CSharpCompilation.Create(
            " ",
            [CSharpSyntaxTree.ParseText(Source(body), path: "Harness.cs", cancellationToken: TestContext.Current.CancellationToken)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create([CreateGenerator()]);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();
        result.Diagnostics.Should().ContainSingle(d => d.Id == "NORMGEN006");
        result.Diagnostics.Single(d => d.Id == "NORMGEN006").Severity.Should().Be(DiagnosticSeverity.Error);
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Valid_snippet_emits_one_source_without_diagnostics()
    {
        var harness = RunDiagnosticCase(
            "var q = orders.Join<Person>(people, (a, b) => true, Alias.Buyer);");

        harness.Run.Diagnostics.Should().BeEmpty();
        harness.Run.GeneratedTrees.Should().HaveCount(1);
        harness.Run.GeneratedTrees[0].ToString().Should().Contain("class AliasProjection_Buyer");
    }

    [Fact]
    public void Valid_snippet_output_is_deterministic()
    {
        const string body = "var q = orders.Join<Person>(people, (a, b) => true, Alias.Buyer);";

        var first = RunDiagnosticCase(body);
        // Same semantics, different whitespace/line endings: generated names must not depend on layout.
        var second = RunDiagnosticCase(body.Replace("\n", "\r\n").Replace(" ", "  "));

        first.Run.GeneratedTrees[0].ToString().Should().Be(second.Run.GeneratedTrees[0].ToString());
    }

    [Fact]
    public void Unrelated_edit_reuses_the_cached_generator_output()
    {
        var generator = CreateGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        var compilation = CreateCompilation(Source("var q = orders.Join<Person>(people, (a, b) => true, Alias.Buyer);"));
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var first = driver.GetRunResult();

        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace Unrelated { public sealed class Extra { } }", cancellationToken: TestContext.Current.CancellationToken));
        driver = driver.RunGenerators(edited, TestContext.Current.CancellationToken);
        var second = driver.GetRunResult();

        second.GeneratedTrees.Single().ToString().Should().Be(first.GeneratedTrees.Single().ToString());

        // With step tracking on, the unrelated edit must leave the source-output step cached: every
        // output it produced is reused (IncrementalStepRunReason.Cached), i.e. the pipeline did not
        // re-run emission because of an edit that cannot affect the alias schema.
        var sourceOutputSteps = second.Results.Single().TrackedSteps["SourceOutput"];
        sourceOutputSteps.Should().ContainSingle();
        var outputs = sourceOutputSteps[0].Outputs;
        outputs.Should().NotBeEmpty();
        outputs.Should().OnlyContain(output => output.Reason == IncrementalStepRunReason.Cached);
    }

    // ---------------------------------------------------------------------------------------------
    // Harness plumbing.
    // ---------------------------------------------------------------------------------------------

    private static Harness RunDiagnosticCase(string body)
    {
        var source = Source(body);
        var compilation = CreateCompilation(source);

        GeneratorDriver driver = CSharpGeneratorDriver.Create([CreateGenerator()]);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, TestContext.Current.CancellationToken);

        return new Harness(source, driver.GetRunResult(), output);
    }

    private static string Source(string body)
        => "using NextORM.Core;\n" +
           "namespace Harness;\n" +
           "public sealed class Order { public int Id { get; set; } }\n" +
           "public sealed class Person { public int Id { get; set; } }\n" +
           "public static class Cases\n" +
           "{\n" +
           "    public static void Run(EntityBuilder<Order> orders, EntityBuilder<Person> people)\n" +
           "    {\n" +
           "        " + body + "\n" +
           "    }\n" +
           "}\n";

    private static Compilation CreateCompilation(string source)
        => CSharpCompilation.Create(
            "AliasHarness",
            [CSharpSyntaxTree.ParseText(source, path: "Harness.cs", cancellationToken: TestContext.Current.CancellationToken)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static ISourceGenerator CreateGenerator()
        => ((IIncrementalGenerator)Activator.CreateInstance(GeneratorType, nonPublic: true)!).AsSourceGenerator();

    private static Diagnostic AssertSingleDiagnostic(Harness harness, string id, string expectedSpanText)
    {
        harness.Run.Diagnostics.Should().ContainSingle(d => d.Id == id);
        var diagnostic = harness.Run.Diagnostics.Single(d => d.Id == id);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);

        var span = diagnostic.Location.SourceSpan;
        harness.Source.Substring(span.Start, span.Length).Should().Be(expectedSpanText);
        return diagnostic;
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
        {
            foreach (var path in tpa.Split(Path.PathSeparator)) paths.Add(path);
        }

        var baseDirectory = AppContext.BaseDirectory;
        paths.Add(Path.Combine(baseDirectory, "nextorm.core.dll"));
        paths.Add(Path.Combine(baseDirectory, "OneOf.dll"));
        foreach (var file in Directory.EnumerateFiles(baseDirectory, "Microsoft.Extensions.*.dll")) paths.Add(file);

        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (var path in paths.Where(File.Exists)) references.Add(MetadataReference.CreateFromFile(path));
        return references.ToImmutable();
    }
}
