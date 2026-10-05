using System.Collections.Immutable;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NextORM.AliasTests;

/// <summary>
/// Compile-contract guard for the explicit DELETE-join starter (#145). The joined <c>Returning</c>
/// overloads were removed and replaced by <c>JoinedEntityBuilder&lt;...&gt;.CreateDeleteJoinBuilder()</c>
/// followed by <c>Returning()</c> / <c>Returning(projection)</c>.
/// <para>
/// These tests compile user-source snippets against the REAL nextorm API (the same metadata-reference
/// set the join-alias generator harness uses: the test-platform assemblies plus <c>nextorm.core.dll</c>
/// and its runtime dependencies), so they exercise the public surface exactly as a consumer would. The
/// positive fixtures must produce zero errors; the negative fixtures must fail specifically because the
/// member <c>Returning</c> no longer exists on <c>JoinedEntityBuilder&lt;...&gt;</c>.
/// </para>
/// </summary>
public class JoinedDeleteReturningApiCompilationTests
{
    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(8)]
    public void CreateDeleteJoinBuilder_Chain_ShouldCompileAtEveryArity(int arity)
    {
        var source = Source(BuildPositiveBody(arity));

        var errors = Errors(source);

        errors.Should().BeEmpty(
            because: "the explicit CreateDeleteJoinBuilder().Returning() chain must compile cleanly:\n"
                + string.Join("\n", errors.Select(d => d.ToString())));
    }

    [Theory]
    [InlineData("Returning()")]
    [InlineData("Returning(p => p)")]
    public void Returning_DirectlyOnJoinedBuilder_ShouldBeAMissingMember(string returning)
    {
        var source = Source(BuildNegativeBody(returning));

        // Guard: the same receiver chain is otherwise valid, so if `Returning` were still an extension
        // on JoinedEntityBuilder<...> the snippet below would compile. The only difference is the
        // removed extension, which is exactly what makes the original snippet error. (The removed
        // overloads can no longer be invoked in-tree, so this control is the closest reproducible proof
        // that the failure is a pure API-removal failure and not a broken fixture/reference setup.)
        var control = source.Replace(".Returning", ".CreateDeleteJoinBuilder().Returning");
        var controlErrors = Errors(control);
        controlErrors.Should().BeEmpty(
            because: "the control snippet with the new starter must compile cleanly:\n"
                + string.Join("\n", controlErrors.Select(d => d.ToString())));

        var errors = Errors(source);

        // No missing-reference / missing-namespace noise: the only problem must be the removed member.
        errors.Should().NotContain(d => d.Id == "CS0234" || d.Id == "CS0246");

        var missingReturning = errors.Where(d =>
            (d.Id == "CS1061" || d.Id == "CS1929")
            && d.GetMessage().Contains("Returning")).ToList();

        missingReturning.Should().ContainSingle(
            because: "exactly one missing-member error must point at `Returning`:\n"
                + string.Join("\n", errors.Select(d => $"{d.Id}: {d}")));

        var diagnostic = missingReturning[0];
        var span = diagnostic.Location.SourceSpan;
        source.Substring(span.Start, span.Length).Should().Be("Returning",
            because: "the error must be reported at the `Returning` identifier");
    }

    // ---------------------------------------------------------------------------------------------
    // Fixture generation.
    // ---------------------------------------------------------------------------------------------

    private static string BuildPositiveBody(int arity)
    {
        var sb = new StringBuilder();
        sb.Append("_ = ").Append(Chain(arity)).Append(".CreateDeleteJoinBuilder().Returning();").Append('\n');
        sb.Append("_ = ").Append(Chain(arity)).Append(".CreateDeleteJoinBuilder().Returning(p => new { p.Item1.Id });");
        return sb.ToString();
    }

    private static string BuildNegativeBody(string returning)
        => $"_ = {Chain(2)}.{returning};";

    private static string Chain(int arity)
    {
        var sb = new StringBuilder("ctx.From<E1>().Join(ctx.From<E2>(), (a, b) => a.Id == b.Id)");
        for (var slot = 3; slot <= arity; slot++)
            sb.Append($".Join(ctx.From<E{slot}>(), (a, b) => a.Item1.Id == b.Id)");
        return sb.ToString();
    }

    private static string Source(string body)
        => "using NextORM.Core;\n"
        + "namespace Harness;\n"
        + "public sealed class E1 { public int Id { get; set; } }\n"
        + "public sealed class E2 { public int Id { get; set; } }\n"
        + "public sealed class E3 { public int Id { get; set; } }\n"
        + "public sealed class E4 { public int Id { get; set; } }\n"
        + "public sealed class E5 { public int Id { get; set; } }\n"
        + "public sealed class E6 { public int Id { get; set; } }\n"
        + "public sealed class E7 { public int Id { get; set; } }\n"
        + "public sealed class E8 { public int Id { get; set; } }\n"
        + "public static class Cases\n"
        + "{\n"
        + "    public static void Run(IDataContext ctx)\n"
        + "    {\n"
        + "        " + body + "\n"
        + "    }\n"
        + "}\n";

    private static ImmutableArray<Diagnostic> Errors(string source)
    {
        var compilation = CSharpCompilation.Create(
            "JoinedDeleteReturningApiHarness",
            [CSharpSyntaxTree.ParseText(source, path: "Harness.cs", cancellationToken: TestContext.Current.CancellationToken)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
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
