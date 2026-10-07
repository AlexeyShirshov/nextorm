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
    public void Root_alias_emits_a_dim1_projection_and_a_withalias_extension()
    {
        var harness = RunDiagnosticCase("var q = orders.WithAlias(Alias.Root);");

        harness.Run.Diagnostics.Should().BeEmpty();
        harness.Run.GeneratedTrees.Should().HaveCount(1);
        var generated = harness.Run.GeneratedTrees[0].ToString();
        generated.Should().Contain("class AliasProjection_A1_Root<T1> : global::NextORM.Core.Projection<T1>");
        generated.Should().Contain("AliasJoin_A1_Root<T1>");
        generated.Should().Contain("WithAlias<T>");
        generated.Should().Contain("AliasRoot<");
        // The root alias names slot 1; Item1 is inherited from Projection<T1> and the generated alias
        // member is expression-only (slot 1), so the generated text carries the slot attribute + member.
        generated.Should().Contain("[global::NextORM.Core.JoinSlot(1)]");
        generated.Should().Contain("public T1 Root => throw new global::System.NotSupportedException();");
    }

    [Fact]
    public void WithAlias_after_a_join_reports_NORMGEN008()
    {
        var harness = RunDiagnosticCase(
            "var q = orders.Join<Person>(people, (a, b) => true, Alias.Buyer).WithAlias(Alias.Root);");

        AssertSingleDiagnostic(harness, "NORMGEN008", "Alias.Root");
    }

    [Fact]
    public void Repeated_WithAlias_reports_NORMGEN008_on_the_second_root_alias()
    {
        var harness = RunDiagnosticCase(
            "var q = orders.WithAlias(Alias.First).WithAlias(Alias.Second);");

        AssertSingleDiagnostic(harness, "NORMGEN008", "Alias.Second");
    }

    [Fact]
    public void Root_alias_colliding_with_a_retained_ItemN_reports_NORMGEN002()
    {
        var harness = RunDiagnosticCase("var q = orders.WithAlias(Alias.Item1);");

        AssertSingleDiagnostic(harness, "NORMGEN002", "Alias.Item1");
    }

    [Fact]
    public void Root_alias_duplicating_a_join_alias_reports_NORMGEN001()
    {
        var harness = RunDiagnosticCase(
            "var q = orders.WithAlias(Alias.X).Join<Person>(people, (a, b) => true, Alias.X);");

        AssertSingleDiagnostic(harness, "NORMGEN001", "Alias.X");
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
    public void Unrelated_user_type_named_Cte_is_not_recognized_as_a_Nextorm_CTE()
    {
        // R159-10 negative: a user type that merely shares the simple name 'Cte' must not be treated as a
        // NextORM CTE descriptor. With symbol-identity recognition no alias surface is generated for it
        // (the pre-fix simple-name match emitted an overload taking NextORM.Core.Cte<TJoin>).
        const string source =
            "using NextORM.Core;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public sealed class Cte<T> { }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(EntityBuilder<Order> orders, Cte<Person> cte)\n" +
            "    {\n" +
            "        _ = orders.Join(cte, (a, b) => true, Alias.Buyer);\n" +
            "    }\n" +
            "}\n";

        var compilation = CSharpCompilation.Create(
            "AliasHarness",
            [CSharpSyntaxTree.ParseText(source, path: "Harness.cs", cancellationToken: TestContext.Current.CancellationToken)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create([CreateGenerator()]);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, TestContext.Current.CancellationToken);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Explicit_generic_joined_type_is_independent_of_the_source_CTE_kind()
    {
        // C6 (D159 r1/n2 probe, promoted to a permanent test): an explicit generic <Person> whose source
        // actually carries Other (EntityBuilder<Other> or Cte<Other>) still selects the emitted step-1
        // overload by the SOURCE expression's CTE kind, not by the explicit type argument. The explicit
        // joined type is not part of a single-step emitted signature (the method is generic over TJoin),
        // so the mismatch stays a call-site CS1503 and never becomes a generator crash or a
        // silently-merged method. Closes C6 with its observed outcome.
        const string source =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public sealed class Other { public int Id { get; set; } }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(EntityBuilder<Order> orders, EntityBuilder<Other> others, Cte<Other> otherCte)\n" +
            "    {\n" +
            "        _ = orders.Join<Person>(others, (a, b) => true, Alias.X);\n" +
            "        _ = orders.Join<Person>(otherCte, (a, b) => true, Alias.Y);\n" +
            "    }\n" +
            "}\n";

        var compilation = CSharpCompilation.Create(
            "AliasHarness",
            [CSharpSyntaxTree.ParseText(source, path: "Harness.cs", cancellationToken: TestContext.Current.CancellationToken)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create([CreateGenerator()]);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, TestContext.Current.CancellationToken);
        var run = driver.GetRunResult();

        // The generator itself is clean: no NORMGEN diagnostic, one source, both step-1 overload forms.
        run.Diagnostics.Should().BeEmpty();
        run.GeneratedTrees.Should().HaveCount(1);
        var generated = run.GeneratedTrees[0].ToString();
        generated.Should().Contain("AliasJoin_P1_A2_X").And.Contain("AliasJoin_P1_A2_Y");
        generated.Should().Contain("global::NextORM.Core.EntityBuilder<TJoin> _,");
        generated.Should().Contain("global::NextORM.Core.Cte<TJoin> cte,");

        // The explicit <Person> vs actual <Other> mismatch remains a normal compile error at the call site.
        output.GetDiagnostics(TestContext.Current.CancellationToken).Should().Contain(diagnostic => diagnostic.Id == "CS1503");
    }

    [Fact]
    public void Valid_snippet_emits_one_source_without_diagnostics()
    {
        var harness = RunDiagnosticCase(
            "var q = orders.Join<Person>(people, (a, b) => true, Alias.Buyer);");

        harness.Run.Diagnostics.Should().BeEmpty();
        harness.Run.GeneratedTrees.Should().HaveCount(1);
        harness.Run.GeneratedTrees[0].ToString().Should().Contain("class AliasProjection_P1_A2_Buyer");
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

    [Fact]
    public void Digit_ending_alias_at_a_later_slot_keeps_its_positional_slot()
    {
        // R160-01 / D:160-03 harness negative for the F1 gap (E160-13 M4): 'Buyer2' is a name whose
        // trailing digit is 2, but here it occupies slot 3 (a positional join takes slot 2), so the
        // digit and the slot differ. The emitted slot attribute and the property's generic index must
        // be the positional slot 3; a generator that derived the slot from the trailing digit would
        // emit JoinSlot(2) on a T2 property instead.
        var harness = RunDiagnosticCase(
            "var q = orders" +
            ".Join<Person>(people, (a, b) => true)" +
            ".Join<Person>(people, (a, b) => true, Alias.Buyer2);");

        harness.Run.Diagnostics.Should().BeEmpty();
        harness.Run.GeneratedTrees.Should().HaveCount(1);
        var generated = harness.Run.GeneratedTrees[0].ToString();

        generated.Should().Contain("class AliasProjection_P1_P2_A3_Buyer2");
        generated.Should().MatchRegex(@"\[global::NextORM\.Core\.JoinSlot\(3\)\]\s+public T3 Buyer2 =>");
        generated.Should().NotMatchRegex(@"\[global::NextORM\.Core\.JoinSlot\(2\)\]\s+public T2 Buyer2 =>");
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
