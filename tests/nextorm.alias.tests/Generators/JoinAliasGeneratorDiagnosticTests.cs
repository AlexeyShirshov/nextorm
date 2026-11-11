using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
    public void Correlated_apply_ninth_slot_reports_NORMGEN004_with_the_exact_message()
    {
        // #324: a correlated APPLY consumes one slot like any other join step, so a full eight-slot alias
        // projection plus one applied alias must be refused with the existing arity diagnostic - never
        // nested, and never widened beyond Projection<T1..T8>.
        var harness = RunDiagnosticCase(
            "var q = orders" +
            ".Join<Person>(people, (a, b) => true, Alias.A1)" +
            ".Join<Person>(people, (a, b) => true, Alias.A2)" +
            ".Join<Person>(people, (a, b) => true, Alias.A3)" +
            ".Join<Person>(people, (a, b) => true, Alias.A4)" +
            ".Join<Person>(people, (a, b) => true, Alias.A5)" +
            ".Join<Person>(people, (a, b) => true, Alias.A6)" +
            ".Join<Person>(people, (a, b) => true, Alias.A7)" +
            ".CrossApply(p => people, Alias.A8);");

        var diagnostic = AssertSingleDiagnostic(harness, "NORMGEN004", "Alias.A8");
        diagnostic.GetMessage().Should().Be(
            "The alias projection would have 9 slots; the maximum supported by Projection<T1..T8> is 8");

        var generated = string.Concat(harness.Run.GeneratedTrees.Select(tree => tree.ToString()));
        generated.Should().NotContain("Projection<T1, T2, T3, T4, T5, T6, T7, T8, T9>");
    }

    [Fact]
    public void Correlated_apply_alias_extension_takes_the_marker_last_and_no_options()
    {
        // #324: both correlated APPLY operators are emitted as marker extensions for an after-join alias
        // receiver. The marker is the last parameter (after the lambda source) and the form carries no
        // Action<JoinOptions>, exactly like every other source-explicit alias extension.
        var harness = RunDiagnosticCase(
            "var q = orders.Join<Person>(people, (a, b) => true, Alias.Buyer)" +
            ".CrossApply(p => people, Alias.Approver)" +
            ".OuterApply(p => people, Alias.Manager);");

        harness.Run.Diagnostics.Should().BeEmpty();
        var generated = NormalizedGeneratedSource(harness.Run);

        foreach (var operation in new[] { "CrossApply", "OuterApply" })
        {
            var signatures = Regex.Matches(
                generated,
                "public static [^\\n]* " + operation + "<[^\\n]*\\n" +
                "\\s+this [^\\n]* self,\\n" +
                "\\s+global::System\\.Linq\\.Expressions\\.Expression<[^\\n]*> source,\\n" +
                "\\s+[^\\n]*Marker marker\\)");

            signatures.Should().HaveCount(
                2, "the generated " + operation + " alias extension must cover both lambda return forms");
        }

        generated.Should().Contain("global::NextORM.Core.EntityBuilder<TJoin>>> source,");
        generated.Should().Contain("global::NextORM.Core.QueryCommand<TJoin>>> source,");
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

    [Fact]
    public void Distinct_root_aliases_emit_one_WithAlias_each_and_duplicates_are_deduped()
    {
        // `JoinAliasGenerator.cs:666-671` collects the distinct root-alias names, so the loop at
        // `:1053` emits exactly one generic `WithAlias<T>` per name. The `continue` guard at `:1057`
        // is unreachable for valid input: each name yields a distinct signature (its own marker type),
        // so no root alias is ever skipped as an already-seen signature.
        var distinct = RunDiagnosticCase(
            "var q1 = orders.WithAlias(Alias.First);" +
            "var q2 = people.WithAlias(Alias.Second);");

        distinct.Run.Diagnostics.Should().BeEmpty();
        distinct.Run.GeneratedTrees.Should().HaveCount(1);
        var generated = distinct.Run.GeneratedTrees[0].ToString();
        CountOccurrences(generated, "WithAlias<T>(").Should().Be(2);
        generated.Should().Contain("AliasJoin_A1_First<T>");
        generated.Should().Contain("AliasJoin_A1_Second<T>");

        // The same root-alias name in two independent chains is deduped to a single method by the
        // `Distinct` at `:669`, not emitted twice (which would be the collision `:1057` guards on).
        var duplicate = RunDiagnosticCase(
            "var q1 = orders.WithAlias(Alias.Root);" +
            "var q2 = people.WithAlias(Alias.Root);");

        duplicate.Run.Diagnostics.Should().BeEmpty();
        duplicate.Run.GeneratedTrees.Should().HaveCount(1);
        var generatedDuplicate = duplicate.Run.GeneratedTrees[0].ToString();
        CountOccurrences(generatedDuplicate, "WithAlias<T>(").Should().Be(1);
        generatedDuplicate.Should().Contain("AliasJoin_A1_Root<T>");
    }

    [Fact]
    public void R03_byte_identical_distinct_chains_collapse_to_one_method()
    {
        // R03 (issue #206): two individually-valid alias chains over the same base `Order`, same
        // `Alias.X`, differing only in the LAST step's joined type (Person vs Other). The last-step
        // joined type is deliberately excluded from the emitted extension identity (it becomes the
        // method type parameter `TJoin`), so both candidates render the SAME signature with a
        // BYTE-IDENTICAL body. The generator must collapse them to one method, with no CS0111 and no
        // NORMGEN007: the same-signature/different-body guard is not spuriously triggered.
        const string source =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public sealed class Other { public int Id { get; set; } }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(EntityBuilder<Order> orders, EntityBuilder<Person> people, EntityBuilder<Other> others)\n" +
            "    {\n" +
            "        _ = orders.Join<Person>(people, (a, b) => true, Alias.X);\n" +
            "        _ = orders.Join<Other>(others, (a, b) => true, Alias.X);\n" +
            "    }\n" +
            "}\n";

        var harness = RunSource(source);
        harness.Run.Diagnostics.Should().BeEmpty();
        harness.Run.GeneratedTrees.Should().HaveCount(1);

        var generated = harness.Run.GeneratedTrees[0].ToString();

        TestContext.Current.AddAttachment("R03-generated.cs", generated);
        TestContext.Current.AddAttachment("R03-diagnostics.txt", string.Join("\n", harness.Run.Diagnostics.Select(d => d.ToString())));
        TestContext.Current.AddAttachment("R03-compiler.txt", string.Join("\n", harness.Output.GetDiagnostics(TestContext.Current.CancellationToken).Select(d => d.ToString())));

        // Two distinct chains (different ChainKey: last JoinedType) collapse to exactly one method.
        CountOccurrences(generated, "Join<TJoin>(").Should().Be(1);
        generated.Should().Contain("AliasJoin_P1_A2_X<global::Harness.Order, TJoin>");

        // No CS0111 (duplicate member) and no other compiler error from the emitted source.
        harness.Output.GetDiagnostics(TestContext.Current.CancellationToken)
            .Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void R03_control_distinct_aliases_process_both_call_sites()
    {
        // R03 non-vacuity control (issue #206): the SAME two alias-join call sites as
        // `R03_byte_identical_distinct_chains_collapse_to_one_method`, but with DISTINCT aliases
        // (Alias.X / Alias.Y) so the two candidates carry distinct emitted signatures. The shared-alias
        // test's `CountOccurrences(..., "Join<TJoin>(") == 1` would also hold vacuously for a generator
        // that processed only one of the two call sites (one method, 0 diagnostics, 0 compiler errors);
        // this control demands exactly TWO generated overloads, so a one-site generator fails here.
        const string source =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public sealed class Other { public int Id { get; set; } }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(EntityBuilder<Order> orders, EntityBuilder<Person> people, EntityBuilder<Other> others)\n" +
            "    {\n" +
            "        _ = orders.Join<Person>(people, (a, b) => true, Alias.X);\n" +
            "        _ = orders.Join<Other>(others, (a, b) => true, Alias.Y);\n" +
            "    }\n" +
            "}\n";

        var control = RunSource(source);
        control.Run.Diagnostics.Should().BeEmpty();
        control.Run.GeneratedTrees.Should().HaveCount(1);

        var generated = control.Run.GeneratedTrees[0].ToString();
        TestContext.Current.AddAttachment("R03-control-generated.cs", generated);
        TestContext.Current.AddAttachment("R03-control-compiler.txt", string.Join("\n", control.Output.GetDiagnostics(TestContext.Current.CancellationToken).Select(d => d.ToString())));

        // Two distinct call sites must yield two distinct overloads and two distinct typed builders.
        CountOccurrences(generated, "Join<TJoin>(").Should().Be(2);
        generated.Should().Contain("AliasJoin_P1_A2_X<global::Harness.Order, TJoin>");
        generated.Should().Contain("AliasJoin_P1_A2_Y<global::Harness.Order, TJoin>");
        generated.Should().Contain("public class AliasJoin_P1_A2_X<T1, T2>");
        generated.Should().Contain("public class AliasJoin_P1_A2_Y<T1, T2>");
        generated.Should().Contain("public class AliasProjection_P1_A2_X<T1, T2>");
        generated.Should().Contain("public class AliasProjection_P1_A2_Y<T1, T2>");
        control.Output.GetDiagnostics(TestContext.Current.CancellationToken)
            .Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void R07_generation_is_order_independent_and_repeatable()
    {
        // R07 (issue #206): byte-identical generated source across (a) reversed declaration order of
        // two supported alias chains and (b) repeated runs on the same source. The pipeline sorts every
        // emitted collection by an ordinal key, so neither file order nor run-to-run state may leak
        // into the generated text.
        const string header =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(EntityBuilder<Order> orders, EntityBuilder<Person> people)\n" +
            "    {\n";
        const string chainA = "        _ = orders.Join<Person>(people, (a, b) => true, Alias.Buyer);\n";
        const string chainB = "        _ = people.Join<Order>(orders, (a, b) => true, Alias.Seller);\n";
        const string footer = "    }\n}\n";

        var forward1 = RunSource(header + chainA + chainB + footer);
        var forward2 = RunSource(header + chainA + chainB + footer);
        var reversed1 = RunSource(header + chainB + chainA + footer);
        var reversed2 = RunSource(header + chainB + chainA + footer);

        forward1.Run.Diagnostics.Should().BeEmpty();
        reversed1.Run.Diagnostics.Should().BeEmpty();

        var forwardText = forward1.Run.GeneratedTrees.Single().ToString();
        var reversedText = reversed1.Run.GeneratedTrees.Single().ToString();

        TestContext.Current.AddAttachment("R07-forward-generated.cs", forwardText);
        TestContext.Current.AddAttachment("R07-reversed-generated.cs", reversedText);

        // Non-vacuity guard: a byte compare of two empty (or declaration-less) payloads would pass even
        // if generation silently dropped both overloads. Before comparing, require the fixture's actual
        // generated builders and generic extension signatures — a regression that stops emitting them
        // must fail here, not slip through as `empty == empty`.
        forwardText.Should().NotBeNullOrWhiteSpace();
        reversedText.Should().NotBeNullOrWhiteSpace();
        forwardText.Should().Contain("public class AliasJoin_P1_A2_Buyer<T1, T2>");
        forwardText.Should().Contain("public class AliasJoin_P1_A2_Seller<T1, T2>");
        forwardText.Should().Contain("public class AliasProjection_P1_A2_Buyer<T1, T2>");
        forwardText.Should().Contain("public class AliasProjection_P1_A2_Seller<T1, T2>");
        forwardText.Should().Contain("AliasJoin_P1_A2_Buyer<global::Harness.Order, TJoin>");
        forwardText.Should().Contain("AliasJoin_P1_A2_Seller<global::Harness.Person, TJoin>");
        forwardText.Should().Contain("Alias.BuyerMarker");
        forwardText.Should().Contain("Alias.SellerMarker");
        CountOccurrences(forwardText, "Join<TJoin>(").Should().Be(2);

        // Repeated runs of the same source are byte-identical.
        forward2.Run.GeneratedTrees.Single().ToString().Should().Be(forwardText);
        reversed2.Run.GeneratedTrees.Single().ToString().Should().Be(reversedText);

        // Declaration-order independence: reversing the two chains changes not a single byte.
        reversedText.Should().Be(forwardText);
    }

    [Fact]
    public void R03_intermediate_cte_alias_join_is_supported_without_false_NORMGEN007()
    {
        // R03 (issue #206, intermediate `IsCte`): a chain whose intermediate step is an alias join
        // from a CLOSED typed CTE descriptor (`Cte<Person>`), followed by another alias join. The
        // intermediate step's source kind belongs only to that step's own extension identity; it is
        // NOT part of the later step's emitted signature. The continuation must still be emitted and
        // the chain must not trigger a false NORMGEN007.
        const string source =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(EntityBuilder<Order> orders, EntityBuilder<Person> people, Cte<Person> cte)\n" +
            "    {\n" +
            "        _ = orders.Join(cte, (a, b) => true, Alias.Buyer).Join<Person>(people, (a, b) => true, Alias.Seller);\n" +
            "    }\n" +
            "}\n";

        var harness = RunSource(source);
        harness.Run.Diagnostics.Should().BeEmpty();
        harness.Run.GeneratedTrees.Should().HaveCount(1);

        var generated = harness.Run.GeneratedTrees[0].ToString();

        TestContext.Current.AddAttachment("R03-cte-generated.cs", generated);
        TestContext.Current.AddAttachment("R03-cte-diagnostics.txt", string.Join("\n", harness.Run.Diagnostics.Select(d => d.ToString())));

        // The closed-CTE intermediate step emits its own Cte<TJoin>-source overload...
        generated.Should().Contain("global::NextORM.Core.Cte<TJoin> cte,");
        // ...and the continuation over the already-aliased receiver is still emitted.
        generated.Should().Contain("AliasJoin_P1_A2_Buyer<global::Harness.Order, global::Harness.Person>");
        generated.Should().Contain("AliasJoin_P1_A2_Buyer_A3_Seller<global::Harness.Order, global::Harness.Person, TJoin>");

        harness.Output.GetDiagnostics(TestContext.Current.CancellationToken)
            .Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Aliased_chain_consumed_by_a_later_query_keeps_the_downstream_aliases()
    {
        // #230: an aliased chain (Buyer) projected into a CTE and consumed by a later query whose chain
        // itself carries aliases (Root/Seller). The aliased chain's builder type is generated and absent
        // on the generator's input pass, so the consuming expression is an error type; the generator must
        // still recover the downstream chain from syntax and emit Root/Seller.
        const string source =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public sealed class MidRow { public int Id { get; set; } }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(IDataContext ctx, EntityBuilder<Person> people)\n" +
            "    {\n" +
            "        var inner = ctx.From<Order>().WithAlias(Alias.Order)\n" +
            "            .Join<Person>(people, (o, p) => o.Order.Id == p.Id, Alias.Customer)\n" +
            "            .Join<Person>(people, (p, per) => p.Customer.Id == per.Id, Alias.Person)\n" +
            "            .Select(p => new { Id = p.Order.Id })\n" +
            "            .AsCte(\"inner\");\n" +
            "        var mid = ctx.From(inner)\n" +
            "            .GroupBy(x => new { x.Id })\n" +
            "            .Select(x => new MidRow { Id = x.Id })\n" +
            "            .AsCte(\"mid\");\n" +
            "        _ = ctx.From(mid).WithAlias(Alias.Metrics)\n" +
            "            .Join<Person>(people, (m, p) => m.Metrics.Id == p.Id, Alias.SalesPerson)\n" +
            "            .Join<Person>(people, (p, per) => p.SalesPerson.Id == per.Id, Alias.Manager);\n" +
            "    }\n" +
            "}\n";

        var harness = RunSource(source);
        harness.Run.Diagnostics.Should().BeEmpty();
        harness.Run.GeneratedTrees.Should().HaveCount(1);

        var generated = harness.Run.GeneratedTrees[0].ToString();
        TestContext.Current.AddAttachment("230-generated.cs", generated);
        TestContext.Current.AddAttachment("230-compiler.txt", string.Join("\n", harness.Output.GetDiagnostics(TestContext.Current.CancellationToken).Select(d => d.ToString())));

        generated.Should().Contain("Alias.OrderMarker");
        generated.Should().Contain("Alias.MetricsMarker");
        generated.Should().Contain("Alias.SalesPersonMarker");
        generated.Should().Contain("Alias.ManagerMarker");
        generated.Should().Contain("AliasJoin_A1_Metrics_A2_SalesPerson<global::Harness.MidRow, TJoin>");
    }

    [Fact]
    public void Alias_over_an_anonymous_chain_type_reports_NORMGEN009()
    {
        // #229: the emitted extension signatures name the chain's concrete projected type, which cannot
        // name an anonymous type. The generator must fail closed with NORMGEN009 and never emit source
        // containing the invalid `global::<anonymous type: …>` token.
        const string source =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(IDataContext ctx, EntityBuilder<Person> people)\n" +
            "    {\n" +
            "        var inner = ctx.From<Order>().Select(o => new { o.Id }).AsCte(\"inner\");\n" +
            "        _ = ctx.From(inner).WithAlias(Alias.Root);\n" +
            "    }\n" +
            "}\n";

        var harness = RunSource(source);

        harness.Run.Diagnostics.Should().ContainSingle(d => d.Id == "NORMGEN009");
        harness.Run.Diagnostics.Single(d => d.Id == "NORMGEN009").Severity.Should().Be(DiagnosticSeverity.Error);
        harness.Run.GeneratedTrees.Should().NotBeEmpty();
        harness.Run.GeneratedTrees.Single().ToString().Should().NotContain("<anonymous type:");
    }

    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Alias_over_an_intermediate_anonymous_join_step_reports_NORMGEN009()
    {
        // E02 (#229 intermediate / non-last step): the first alias step joins an anonymous-typed CTE
        // while the chain continues with a LATER named alias join. The anonymous step still appears in
        // the emitted receiver/return signatures, so the chain must fail closed exactly once with
        // NORMGEN009 and never emit the invalid `global::<anonymous type: …>` token.
        const string source =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(IDataContext ctx, EntityBuilder<Person> people)\n" +
            "    {\n" +
            "        var inner = ctx.From<Order>().Select(o => new { o.Id }).AsCte(\"inner\");\n" +
            "        _ = ctx.From<Order>().Join(inner, (o, p) => true, Alias.A).Join<Person>(people, (p, q) => true, Alias.B);\n" +
            "    }\n" +
            "}\n";

        var harness = RunSource(source);

        TestContext.Current.AddAttachment("E02-generated-trees.txt",
            string.Join("\n----\n", harness.Run.GeneratedTrees.Select(tree => tree.ToString())));

        harness.Run.Diagnostics.Should().ContainSingle(d => d.Id == "NORMGEN009");
        harness.Run.Diagnostics.Single(d => d.Id == "NORMGEN009").Severity.Should().Be(DiagnosticSeverity.Error);
        harness.Run.GeneratedTrees.Should().NotBeEmpty();
        harness.Run.GeneratedTrees.Should().OnlyContain(tree => !tree.ToString().Contains("<anonymous type:"));
    }

    [Fact]
    public void Error_path_chain_with_Where_and_OrderBy_keeps_the_downstream_aliases()
    {
        // E06 (#230 recovering walk): the syntax walk from the error-typed `ctx.From(mid)` root must
        // traverse element-preserving operators beyond GroupBy/Select/AsCte. Here the middle chain
        // projects a NAMED DTO and then applies Where and OrderBy before AsCte, so the walk must step
        // through both operators to reach the Select that establishes the element type; the outer
        // aliased chain still emits every alias.
        const string source =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public sealed class MidRow { public int Id { get; set; } }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(IDataContext ctx, EntityBuilder<Person> people)\n" +
            "    {\n" +
            "        var inner = ctx.From<Order>().WithAlias(Alias.Order)\n" +
            "            .Join<Person>(people, (o, p) => o.Order.Id == p.Id, Alias.Customer)\n" +
            "            .Select(p => new { Id = p.Order.Id })\n" +
            "            .AsCte(\"inner\");\n" +
            "        var mid = ctx.From(inner)\n" +
            "            .Select(x => new MidRow { Id = x.Id })\n" +
            "            .Where(x => x.Id > 0)\n" +
            "            .OrderBy(x => x.Id)\n" +
            "            .AsCte(\"mid\");\n" +
            "        _ = ctx.From(mid).WithAlias(Alias.Metrics)\n" +
            "            .Join<Person>(people, (m, p) => m.Metrics.Id == p.Id, Alias.SalesPerson)\n" +
            "            .Join<Person>(people, (p, per) => p.SalesPerson.Id == per.Id, Alias.Manager);\n" +
            "    }\n" +
            "}\n";

        var harness = RunSource(source);
        harness.Run.Diagnostics.Should().BeEmpty();
        harness.Run.GeneratedTrees.Should().HaveCount(1);

        var generated = harness.Run.GeneratedTrees[0].ToString();
        TestContext.Current.AddAttachment("E06-where-orderby-generated.cs", generated);

        generated.Should().Contain("Alias.OrderMarker");
        generated.Should().Contain("Alias.MetricsMarker");
        generated.Should().Contain("Alias.SalesPersonMarker");
        generated.Should().Contain("Alias.ManagerMarker");
        generated.Should().Contain("AliasJoin_A1_Metrics_A2_SalesPerson<global::Harness.MidRow, TJoin>");
        generated.Should().NotContain("<anonymous type:");
    }

    [Fact]
    public void Valid_named_aliased_chain_acquires_no_NORMGEN009()
    {
        // E08 negative: the fail-closed diagnostic is reserved for anonymous chain types; a valid
        // aliased chain over a NAMED projection type emits its alias surface and no NORMGEN009.
        const string source =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public sealed class NamedRow { public int Id { get; set; } }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(IDataContext ctx, EntityBuilder<Person> people)\n" +
            "    {\n" +
            "        var cte = ctx.From<Order>().Select(o => new NamedRow { Id = o.Id }).AsCte(\"named\");\n" +
            "        _ = ctx.From(cte).WithAlias(Alias.Root).Join<Person>(people, (r, p) => true, Alias.Seller);\n" +
            "    }\n" +
            "}\n";

        var harness = RunSource(source);

        harness.Run.Diagnostics.Should().NotContain(d => d.Id == "NORMGEN009");
        harness.Run.GeneratedTrees.Should().NotBeEmpty();
        harness.Run.GeneratedTrees.Should().OnlyContain(tree => !tree.ToString().Contains("<anonymous type:"));
    }

    // ---------------------------------------------------------------------------------------------
    // E06/E07 recovery coverage: every member of the three frozen allowlists is driven with an
    // error-typed chain. The walk (ResolveQueryElementType) descends one strictly nested receiver per
    // step, so it terminates; per node it is total (query-root operator => element type, preserving
    // operator => recurse, otherwise => null). The invariant per member: the generator does not crash,
    // a valid named chain acquires no NORMGEN009, and the emitted surface (if any) carries the
    // recovered NAMED element type — a wrong alias, an anonymous token or a spurious NORMGEN009 fail.
    // ---------------------------------------------------------------------------------------------

    private static readonly string[] ExpectedQueryRootOperators =
        ["CreateQueryBuilder", "From", "FromSql"];

    private static readonly string[] ExpectedElementPreservingOperators =
    [
        "As", "AsCte", "AsRecursiveCte", "Distinct", "DistinctOn", "ForShare", "ForUpdate", "GroupBy",
        "Having", "Limit", "Offset", "OrderBy", "OrderByDescending", "Prepare", "Skip", "Take",
        "ThenBy", "ThenByDescending", "ToCommand", "Where", "WithKeywordCase"
    ];

    private static readonly string[] ExpectedElementWrappers =
        ["Cte", "EntityBuilder", "MutationCteQuery", "QueryCommand", "TempTableSource"];

    /// <summary>Per-operator source template map: the recovery-walk suffix appended after the named
    /// <c>Select</c> (a trailing <c>.AsCte("mid")</c> turns the chain into the consumed CTE).</summary>
    private static readonly Dictionary<string, string> PreservingOperatorSuffixes = new(StringComparer.Ordinal)
    {
        ["Where"] = ".Where(x => x.Id > 0)",
        ["GroupBy"] = ".GroupBy(x => new { x.Id })",
        ["Having"] = ".Having(x => x.Id > 0)",
        ["OrderBy"] = ".OrderBy(x => x.Id)",
        ["OrderByDescending"] = ".OrderByDescending(x => x.Id)",
        ["ThenBy"] = ".OrderBy(x => x.Id).ThenBy(x => x.Id)",
        ["ThenByDescending"] = ".OrderBy(x => x.Id).ThenByDescending(x => x.Id)",
        ["Limit"] = ".Limit(10)",
        ["Offset"] = ".Offset(5)",
        ["Skip"] = ".Skip(5)",
        ["Take"] = ".Take(10)",
        ["Distinct"] = ".Distinct()",
        ["DistinctOn"] = ".DistinctOn(x => x.Id)",
        ["AsCte"] = ".AsCte(\"inner2\")",
        ["AsRecursiveCte"] = ".AsRecursiveCte(\"inner2\", c => c)",
        ["ToCommand"] = ".ToCommand()",
        ["Prepare"] = ".Prepare()",
        ["ForUpdate"] = ".ForUpdate()",
        ["ForShare"] = ".ForShare()",
        ["WithKeywordCase"] = ".WithKeywordCase()",
        ["As"] = ".As(x => x)",
    };

    public static IEnumerable<object[]> RecoveryWalkMembers()
    {
        // Each member carries its determinate expected outcome (kind, member, expectedEmitted, reason):
        // the alias surface is emitted with the recovered named MidRow element type, or it is absent
        // (fail-closed). The theory asserts the encoded branch for every member, so no case is vacuous.
        foreach (var member in ExpectedQueryRootOperators)
            yield return new object[] { "root", member, true, "query-root operator recovers the Cte<MidRow> argument element type" };
        foreach (var member in ExpectedElementPreservingOperators)
            yield return new object[] { "preserving", member, true, "preserving operator: the walk descends to the named Select projection" };
        foreach (var member in ExpectedElementWrappers)
            yield return new object[] { "wrapper", member, true, "wrapper: UnwrapElement unwraps Wrapper<MidRow> to MidRow" };
    }

    [Fact]
    public void Recovery_allowlists_match_the_expected_members()
    {
        // Set-membership guard (E06 re-open trigger): a member added, renamed or removed in the frozen
        // generator must fail here and force a matching theory case (template or unreachable reason).
        ReadGeneratorStringSet("QueryRootOperators").OrderBy(static m => m, StringComparer.Ordinal)
            .Should().Equal(ExpectedQueryRootOperators);
        ReadGeneratorStringSet("ElementPreservingOperators").OrderBy(static m => m, StringComparer.Ordinal)
            .Should().Equal(ExpectedElementPreservingOperators);
        ReadGeneratorStringSet("ElementWrappers").OrderBy(static m => m, StringComparer.Ordinal)
            .Should().Equal(ExpectedElementWrappers);
    }

    [Theory]
    [MemberData(nameof(RecoveryWalkMembers))]
    public void Error_path_recovery_handles_every_walk_member(string kind, string member, bool expectedEmitted, string reason)
    {
        var source = kind switch
        {
            "root" => BuildRootMemberSource(member),
            "preserving" => BuildPreservingMemberSource(member),
            "wrapper" => BuildWrapperMemberSource(member),
            _ => throw new InvalidOperationException($"unknown allowlist kind '{kind}'"),
        };

        Harness harness = null!;
        Action run = () => harness = RunSource(source);
        run.Should().NotThrow($"recovery for allowlist {kind} member '{member}' must not crash the generator");

        harness.Run.Diagnostics.Should().NotContain(
            d => d.Id == "NORMGEN009",
            $"'{member}' drives a valid named chain and must not acquire NORMGEN009");

        var generated = string.Join("\n", harness.Run.GeneratedTrees.Select(static tree => tree.ToString()));
        TestContext.Current.AddAttachment($"E06-recovery-{kind}-{member}.txt",
            generated + "\n---DIAGNOSTICS---\n" + string.Join("\n", harness.Run.Diagnostics.Select(static d => d.ToString())));

        generated.Should().NotContain("<anonymous type:");

        // The walk is total: each member has a determinate outcome encoded in the MemberData. When
        // expectedEmitted, the surface must be present and carry the recovered named MidRow element
        // type; otherwise it must be absent (fail-closed). Both branches are asserted explicitly, so
        // no member is vacuous. A surface with any other element type is a wrong alias and fails here.
        if (expectedEmitted)
        {
            generated.Should().Contain(
                "Alias.RootMarker",
                $"recovery for {kind} member '{member}' must emit the alias surface ({reason})");
            generated.Should().Contain(
                "AliasJoin_A1_Root_A2_Seller<global::Harness.MidRow, TJoin>",
                $"an emitted surface for {kind} member '{member}' must carry the recovered named element type ({reason})");
        }
        else
        {
            generated.Should().NotContain(
                "Alias.RootMarker",
                $"recovery for {kind} member '{member}' must fail closed with no alias surface ({reason})");
        }
    }

    [Theory]
    [InlineData("Cte")]
    [InlineData("QueryCommand")]
    [InlineData("EntityBuilder")]
    [InlineData("TempTableSource")]
    [InlineData("MutationCteQuery")]
    public void Error_path_recovery_unwraps_a_wrapper_typed_root_argument(string wrapper)
    {
        // E07 UnwrapElement: the recovery walk reads a wrapper-typed argument (`Cte<T>`/`QueryCommand<T>`/
        // `EntityBuilder<T>`/`TempTableSource<T>`/`MutationCteQuery<T>`) and unwraps it to its element
        // type. All five ElementWrappers members share this one UnwrapElement branch; MutationCteQuery is
        // closed here (a parameter of that public type is enough — the source is compiled, not run).
        var harness = RunSource(BuildWrapperMemberSource(wrapper));

        harness.Run.Diagnostics.Should().NotContain(d => d.Id == "NORMGEN009");
        harness.Run.GeneratedTrees.Should().HaveCount(1);
        var generated = harness.Run.GeneratedTrees.Single().ToString();
        generated.Should().NotContain("<anonymous type:");
        generated.Should().Contain("AliasJoin_A1_Root_A2_Seller<global::Harness.MidRow, TJoin>");
    }

    [Fact]
    public void Error_path_recovery_with_a_non_wrapper_parameter_fails_closed()
    {
        // E07 ResolveIdentifierElementType parameter branch (:453-454) IS reachable in the error path:
        // a parameter declared as a plain named type unwraps to null (not an ElementWrappers member),
        // so ResolveQueryElementType returns null, ResolveRootFromSyntax returns null and no alias
        // surface is fabricated (fail-closed, no NORMGEN009).
        var source = ErrorReceiverSource(
            "IDataContext ctx, EntityBuilder<Person> people, MidRow target",
            "unknown.From(target)");

        var harness = RunSource(source);

        harness.Run.Diagnostics.Should().NotContain(d => d.Id == "NORMGEN009");
        var generated = string.Join("\n", harness.Run.GeneratedTrees.Select(static tree => tree.ToString()));
        generated.Should().NotContain("Alias.RootMarker");
        generated.Should().NotContain("Alias.SellerMarker");
    }

    private static HashSet<string> ReadGeneratorStringSet(string fieldName)
    {
        var field = GeneratorType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"generator field '{fieldName}' was not found");
        return (HashSet<string>)field.GetValue(null)!;
    }

    private static string RecoveryHeader()
        => "using NextORM.Core;\n" +
           "using NextORM.Generated.AliasHarness;\n" +
           "namespace Harness;\n" +
           "public sealed class Order { public int Id { get; set; } }\n" +
           "public sealed class Person { public int Id { get; set; } }\n" +
           "public sealed class MidRow { public int Id { get; set; } }\n";

    private static string BuildRootMemberSource(string member)
        => ErrorReceiverSource(
            "IDataContext ctx, EntityBuilder<Person> people, Cte<MidRow> target",
            "unknown." + member + "(target)");

    private static string BuildPreservingMemberSource(string member)
    {
        if (!PreservingOperatorSuffixes.TryGetValue(member, out var suffix))
        {
            throw new InvalidOperationException(
                $"No recovery template for ElementPreservingOperators member '{member}': add one or " +
                "declare it unreachable-by-construction with a reason.");
        }

        return RecoveryHeader() +
           "public static class Cases\n{\n" +
           "    public static void Run(IDataContext ctx, EntityBuilder<Person> people)\n    {\n" +
           "        var inner = ctx.From<Order>().WithAlias(Alias.Order)\n" +
           "            .Join<Person>(people, (o, p) => o.Order.Id == p.Id, Alias.Customer)\n" +
           "            .Select(o => new { Id = o.Order.Id })\n" +
           "            .AsCte(\"inner\");\n" +
           "        var mid = ctx.From(inner).Select(x => new MidRow { Id = x.Id })" + suffix + ".AsCte(\"mid\");\n" +
           "        _ = ctx.From(mid).WithAlias(Alias.Root)\n" +
           "            .Join<Person>(people, (r, p) => r.Root.Id == p.Id, Alias.Seller);\n" +
           "    }\n}\n";
    }

    /// <summary>
    /// Error-typed receiver chain: the root operator is invoked on the undefined <c>unknown</c> receiver,
    /// so the invocation is an error type regardless of overload resolution and the #230 syntactic
    /// recovery (ResolveRootFromSyntax -> ResolveQueryElementType) is actually entered.
    /// </summary>
    private static string ErrorReceiverSource(string parameters, string rootInvocation)
        => RecoveryHeader() +
           "public static class Cases\n{\n" +
           "    public static void Run(" + parameters + ")\n    {\n" +
           "        var inner = ctx.From<Order>().WithAlias(Alias.Order)\n" +
           "            .Join<Person>(people, (o, p) => o.Order.Id == p.Id, Alias.Customer)\n" +
           "            .Select(o => new { Id = o.Order.Id })\n" +
           "            .AsCte(\"inner\");\n" +
           "        var mid = " + rootInvocation + ".AsCte(\"mid\");\n" +
           "        _ = ctx.From(mid).WithAlias(Alias.Root)\n" +
           "            .Join<Person>(people, (r, p) => r.Root.Id == p.Id, Alias.Seller);\n" +
           "    }\n}\n";

    private static string BuildWrapperMemberSource(string wrapper)
        => ErrorReceiverSource(
            "IDataContext ctx, EntityBuilder<Person> people, " + wrapper + "<MidRow> target",
            "unknown.From(target)");

    [Fact]
    public void TypeOnlyAliasExactCallShapeCompiles()
    {
        // T322 (issue #322) D1 executable proof: #222's source-less joins must also accept #160's
        // generated alias marker, i.e. the exact explicit-generic call shape
        // `.Join<TJoin>((l, r) => <condition>, Alias.X)` with no source argument. The requested call
        // stays inside the harness source text; the emitted surface has to make that call bind, so the
        // assertion is a compilation success (missing-surface red before the generator/core work).
        const string source =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public sealed class Person { public int Id { get; set; } }\n" +
            "public static class Cases\n" +
            "{\n" +
            "    public static void Run(EntityBuilder<Order> orders, EntityBuilder<Person> people)\n" +
            "    {\n" +
            "        _ = orders.Join<Person>(people, (a, b) => a.Id == b.Id, Alias.Customer);\n" +
            "        _ = orders.Join<Person>((l, r) => l.Id == r.Id, Alias.Buyer);\n" +
            "    }\n" +
            "}\n";

        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([CreateGenerator()]);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, TestContext.Current.CancellationToken);

        var errors = output.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        errors.Should().BeEmpty(
            "the exact source-less alias call `.Join<Person>((l, r) => ..., Alias.Buyer)` must compile " +
            "against the emitted surface; observed compiler errors: {0}",
            string.Join(" | ", errors.Select(diagnostic => diagnostic.ToString())));

        var run = driver.GetRunResult();
        run.Diagnostics.Should().BeEmpty();
        run.GeneratedTrees.Should().HaveCount(1);
    }

    // #322 D3: the source-less alias surface.
    // ---------------------------------------------------------------------------------------------

    private const string SourceLessHeader =
        "using NextORM.Core;\n" +
        "using NextORM.Generated.AliasHarness;\n" +
        "namespace Harness;\n" +
        "public sealed class Order { public int Id { get; set; } }\n" +
        "public sealed class Person { public int Id { get; set; } }\n";

    /// <summary>Wraps a call-site body in the source-less harness (root <c>orders</c>, source <c>people</c>).</summary>
    private static string SourceLessSource(string body)
        => SourceLessHeader +
           "public static class Cases\n{\n" +
           "    public static void Run(EntityBuilder<Order> orders, EntityBuilder<Person> people)\n    {\n" +
           "        _ = people;\n" +
           body +
           "    }\n}\n";

    [Fact]
    public void Source_less_alias_join_emits_the_marker_with_options_overload()
    {
        // The exact #322 shape must produce a closed-receiver extension with the frozen parameter
        // order `self, condition, marker, Action<JoinOptions> options = null` and no source argument at
        // all: the implicit source is created inside the core JoinAlias seam.
        var (run, output) = RunGenerator(SourceLessSource(
            "        _ = orders.Join<Person>((l, r) => l.Id == r.Id, Alias.Buyer);\n" +
            "        _ = orders.Join<Person>((l, r) => l.Id == r.Id, Alias.Buyer, o => o.WithJoinHint(\"loop\"));\n"));

        ErrorText(output).Should().BeEmpty("the source-less alias call must compile against the emitted surface");
        run.Diagnostics.Should().BeEmpty();

        var generated = NormalizedGeneratedSource(run);
        var expected =
            "        public static NextORM.Generated.AliasHarness.AliasJoin_P1_A2_Buyer<global::Harness.Order, TJoin> Join<TJoin>(\n" +
            "            this global::NextORM.Core.EntityBuilder<global::Harness.Order> self,\n" +
            "            global::System.Linq.Expressions.Expression<global::System.Func<global::Harness.Order, TJoin, bool>> condition,\n" +
            "            NextORM.Generated.AliasHarness.Alias.BuyerMarker marker,\n" +
            "            global::System.Action<global::NextORM.Core.JoinOptions> options = null)\n" +
            "            => self.JoinAlias<NextORM.Generated.AliasHarness.AliasJoin_P1_A2_Buyer<global::Harness.Order, TJoin>, NextORM.Generated.AliasHarness.AliasProjection_P1_A2_Buyer<global::Harness.Order, TJoin>, TJoin>(\n" +
            "                static dc => new NextORM.Generated.AliasHarness.AliasJoin_P1_A2_Buyer<global::Harness.Order, TJoin>(dc),\n" +
            "                condition,\n" +
            "                global::NextORM.Core.JoinType.Inner, options);";

        generated.Should().Contain(expected);
        generated.Should().NotContain("EntityBuilder<TJoin> _", "the source-less form takes no source argument");
    }

    [Fact]
    public void Source_less_alias_joins_emit_every_conditional_operator()
    {
        // O = Join/Left/Right/Full on the root receiver: each operator keeps its JoinType and forwards the
        // options, so the generated surface is the source-less counterpart of the source-explicit one.
        var (run, output) = RunGenerator(SourceLessSource(
            "        _ = orders.Join<Person>((l, r) => true, Alias.Buyer);\n" +
            "        _ = orders.LeftJoin<Person>((l, r) => true, Alias.Buyer);\n" +
            "        _ = orders.RightJoin<Person>((l, r) => true, Alias.Buyer);\n" +
            "        _ = orders.FullJoin<Person>((l, r) => true, Alias.Buyer);\n"));

        ErrorText(output).Should().BeEmpty();
        run.Diagnostics.Should().BeEmpty();

        var generated = NormalizedGeneratedSource(run);
        foreach (var (operation, joinType) in new[]
                 {
                     ("Join", "Inner"), ("LeftJoin", "Left"), ("RightJoin", "Right"), ("FullJoin", "Full")
                 })
        {
            generated.Should().Contain(
                "public static NextORM.Generated.AliasHarness.AliasJoin_P1_A2_Buyer<global::Harness.Order, TJoin> "
                + operation + "<TJoin>(");
            generated.Should().Contain(
                "global::NextORM.Core.JoinType." + joinType + ", options);",
                "the source-less " + operation + " step must delegate with JoinType." + joinType);
        }
    }

    [Fact]
    public void Source_less_alias_joins_close_every_positional_receiver_arity()
    {
        // R = root (EntityBuilder<T>) plus JoinedEntityBuilder arity 2..7, for every conditional operator:
        // the generated receiver type is the closed positional projection of that arity.
        string[] operators = ["Join", "LeftJoin", "RightJoin", "FullJoin"];
        var body = new StringBuilder();
        var expectations = new List<string>();

        for (var arity = 2; arity <= 7; arity++)
        {
            foreach (var operation in operators)
            {
                var alias = "S" + operation + arity;
                var receiver = "orders" + string.Concat(Enumerable.Repeat(".Join<Person>((a, b) => true)", arity - 1));
                body.Append("        _ = ").Append(receiver).Append('.')
                    .Append(operation).Append("<Person>((l, r) => true, Alias.").Append(alias).Append(");\n");

                var orderedArguments = "<global::Harness.Order" +
                    string.Concat(Enumerable.Repeat(", global::Harness.Person", arity - 1)) + ">";
                var receiverSuffix = "P1" + string.Concat(
                    Enumerable.Range(2, arity - 1).Select(slot => "_P" + slot));

                expectations.Add(
                    "public static NextORM.Generated.AliasHarness.AliasJoin_" + receiverSuffix + "_A" + (arity + 1) + "_" + alias + orderedArguments.Replace(">", ", TJoin>", StringComparison.Ordinal) + " " + operation + "<TJoin>(");
                expectations.Add(
                    "            this global::NextORM.Core.JoinedEntityBuilder" + orderedArguments + " self,");
            }
        }

        var (run, output) = RunGenerator(SourceLessSource(body.ToString()));

        ErrorText(output).Should().BeEmpty();
        run.Diagnostics.Should().BeEmpty();

        var generated = NormalizedGeneratedSource(run);
        foreach (var expectation in expectations) generated.Should().Contain(expectation);
    }

    [Fact]
    public void Source_less_alias_joins_compile_for_a_generic_joined_type_argument()
    {
        // "Generic callers": the emitted method keeps exactly one method type parameter, so a consumer
        // generic method can supply its own type argument and the alias surface still binds.
        const string source =
            "using NextORM.Core;\n" +
            "using NextORM.Generated.AliasHarness;\n" +
            "namespace Harness;\n" +
            "public sealed class Order { public int Id { get; set; } }\n" +
            "public static class Cases\n{\n" +
            "    public static void Run<TJoined>(EntityBuilder<Order> orders)\n    {\n" +
            "        _ = orders.Join<TJoined>((l, r) => l.Id != 0, Alias.Generic);\n" +
            "        _ = orders.LeftJoin<TJoined>((l, r) => l.Id != 0, Alias.Generic, o => o.WithJoinHint(\"loop\"));\n" +
            "    }\n}\n";

        var (run, output) = RunGenerator(source);

        ErrorText(output).Should().BeEmpty();
        run.Diagnostics.Should().BeEmpty();
        NormalizedGeneratedSource(run).Should().Contain(
            "public static NextORM.Generated.AliasHarness.AliasJoin_P1_A2_Generic<global::Harness.Order, TJoin> LeftJoin<TJoin>(");
    }

    [Fact]
    public void Source_less_alias_join_reaches_the_seven_to_eight_transition()
    {
        // Seven chained source-less alias joins: receivers run from EntityBuilder<Order> through the
        // generated AliasJoin_* of arity 7, and the return type is the arity-8 builder.
        var body = new StringBuilder("        _ = orders");
        for (var slot = 2; slot <= 8; slot++)
        {
            body.Append(".Join<Person>((l, r) => true, Alias.S").Append(slot - 1).Append(')');
        }

        body.Append(";\n");

        var (run, output) = RunGenerator(SourceLessSource(body.ToString()));

        ErrorText(output).Should().BeEmpty();
        run.Diagnostics.Should().BeEmpty();

        var generated = NormalizedGeneratedSource(run);
        generated.Should().Contain(
            "class AliasJoin_P1_A2_S1_A3_S2_A4_S3_A5_S4_A6_S5_A7_S6_A8_S7<T1, T2, T3, T4, T5, T6, T7, T8>");
        generated.Should().Contain(
            "class AliasProjection_P1_A2_S1_A3_S2_A4_S3_A5_S4_A6_S5_A7_S6_A8_S7<T1, T2, T3, T4, T5, T6, T7, T8>");
        // The arity-7 receiver is the generated alias builder of the six-step prefix, not a positional one.
        generated.Should().Contain(
            "            this NextORM.Generated.AliasHarness.AliasJoin_P1_A2_S1_A3_S2_A4_S3_A5_S4_A6_S5_A7_S6<global::Harness.Order, global::Harness.Person, global::Harness.Person, global::Harness.Person, global::Harness.Person, global::Harness.Person, global::Harness.Person> self,");
    }

    [Fact]
    public void Source_less_alias_join_with_a_malformed_marker_reports_NORMGEN005()
    {
        // Negative control: the guard still rejects, it is not a valid shape turned into a failure. Both
        // the option-less and the options-after-marker forms must report NORMGEN005 on the marker.
        AssertSingleDiagnostic(
            RunDiagnosticCase("var q = orders.Join<Person>((a, b) => true, Alias.Buyer.Approver);"),
            "NORMGEN005",
            "Alias.Buyer.Approver");

        AssertSingleDiagnostic(
            RunDiagnosticCase("var q = orders.Join<Person>((a, b) => true, Alias.Buyer.Approver, o => o.WithJoinHint(\"loop\"));"),
            "NORMGEN005",
            "Alias.Buyer.Approver");
    }

    [Fact]
    public void Source_less_alias_join_ninth_slot_reports_NORMGEN004()
    {
        // Negative control: the slot cap is unchanged by the new form; the ninth slot is rejected at the
        // eighth source-less alias step.
        var body = new StringBuilder("var q = orders");
        for (var slot = 2; slot <= 9; slot++)
        {
            body.Append(".Join<Person>((a, b) => true, Alias.A").Append(slot - 1).Append(')');
        }

        body.Append(';');

        var harness = RunDiagnosticCase(body.ToString());
        var diagnostic = AssertSingleDiagnostic(harness, "NORMGEN004", "Alias.A8");
        diagnostic.GetMessage().Should().Contain("9");
    }

    private static (GeneratorDriverRunResult Run, Compilation Output) RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([CreateGenerator()]);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, TestContext.Current.CancellationToken);
        return (driver.GetRunResult(), output);
    }

    private static string ErrorText(Compilation output)
        => string.Join(
            " | ",
            output.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.ToString()));

    /// <summary>Generated source with line endings normalized, so multi-line expectations are portable.</summary>
    private static string NormalizedGeneratedSource(GeneratorDriverRunResult run)
    {
        run.GeneratedTrees.Should().HaveCount(1);
        return run.GeneratedTrees[0].ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    // Harness plumbing.
    // ---------------------------------------------------------------------------------------------

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static Harness RunDiagnosticCase(string body)
        => RunSource(Source(body));

    private static Harness RunSource(string source)
    {
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
        foreach (var file in Directory.EnumerateFiles(baseDirectory, "Microsoft.Extensions.*.dll")) paths.Add(file);

        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (var path in paths.Where(File.Exists)) references.Add(MetadataReference.CreateFromFile(path));
        return references.ToImmutable();
    }
}
