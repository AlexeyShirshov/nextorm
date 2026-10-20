using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NextORM.Core.Tests;

/// <summary>
/// House rule for test code (AGENTS.md, "Tests"): assertions go through FluentAssertions.
/// <c>Xunit.Assert</c> is reserved for the skip primitives (<c>Skip</c>, <c>SkipUnless</c>,
/// <c>SkipWhen</c>), which have no FluentAssertions equivalent.
/// <para>
/// The scanner parses the sources instead of grepping them, so a mention of <c>Assert.Equal</c> in a
/// comment, a doc-comment or a string literal is not a violation, while an aliased or
/// fully-qualified call is. Both halves of the rule are enforced: the <c>Assert.*</c> usage itself
/// and the FluentAssertions reference in every xunit test project.
/// </para>
/// </summary>
public sealed class AssertionStyleTests
{
    private static readonly HashSet<string> SkipPrimitives = new(StringComparer.Ordinal)
    {
        "Skip",
        "SkipUnless",
        "SkipWhen",
    };

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    [Fact]
    public void TestSources_ShouldNotUseXunitAssert_BeyondTheSkipPrimitives()
    {
        var offenders = new List<string>();

        foreach (var file in EnumerateTestSources(Path.Combine(RepoRoot(), "tests")))
        {
            var text = File.ReadAllText(file);
            // Cheap pre-filter before parsing: every banned form contains either "Assert." (a call
            // or a qualified name) or "Xunit.Assert" (a using directive).
            if (!text.Contains("Assert.", StringComparison.Ordinal)
                && !text.Contains("Xunit.Assert", StringComparison.Ordinal))
                continue;

            var tree = CSharpSyntaxTree.ParseText(
                text,
                ParseOptions,
                file,
                cancellationToken: TestContext.Current.CancellationToken);
            var root = tree.GetRoot(TestContext.Current.CancellationToken);

            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case UsingDirectiveSyntax directive when IsXunitAssertImport(directive):
                        offenders.Add($"{Location(file, directive)}: '{directive}' imports Xunit.Assert directly");
                        break;

                    case MemberAccessExpressionSyntax call
                        when IsXunitAssertReceiver(call.Expression)
                             && !SkipPrimitives.Contains(call.Name.Identifier.Text):
                        offenders.Add(
                            $"{Location(file, call)}: Assert.{call.Name.Identifier.Text} - use a Should() assertion");
                        break;
                }
            }
        }

        offenders.Should().BeEmpty(
            "tests assert through FluentAssertions; Xunit.Assert is allowed only for Skip/SkipUnless/SkipWhen."
            + Environment.NewLine
            + "Offenders:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void TestProjects_ShouldReferenceFluentAssertions()
    {
        var testsRoot = Path.Combine(RepoRoot(), "tests");
        var offenders = new List<string>();

        foreach (var projectDirectory in Directory.EnumerateDirectories(testsRoot))
        {
            foreach (var project in Directory.EnumerateFiles(projectDirectory, "*.csproj"))
            {
                var content = File.ReadAllText(project);
                if (!content.Contains("<PackageReference Include=\"xunit.v3\"", StringComparison.Ordinal))
                    continue;

                if (!content.Contains("<PackageReference Include=\"FluentAssertions\"", StringComparison.Ordinal))
                    offenders.Add(Path.GetRelativePath(RepoRoot(), project));
            }
        }

        offenders.Should().BeEmpty(
            "every xunit test project must reference the FluentAssertions package (version pinned centrally):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    private static bool IsXunitAssertReceiver(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax { Identifier.Text: "Assert" }
        || expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Assert" };

    private static bool IsXunitAssertImport(UsingDirectiveSyntax directive)
    {
        var name = directive.Name?.ToString();
        if (name is not ("Xunit.Assert" or "global::Xunit.Assert"))
            return false;

        return directive.StaticKeyword.RawKind != 0
            || directive.Alias?.Name.Identifier.Text == "Assert";
    }

    private static string Location(string file, SyntaxNode node)
    {
        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        return $"{Path.GetRelativePath(RepoRoot(), file)}:{line}";
    }

    /// <summary>
    /// Walks the test tree without descending into <c>bin</c>/<c>obj</c>: a recursive
    /// <see cref="SearchOption.AllDirectories"/> scan would read every build artifact under the
    /// tree (hundreds of megabytes) before any filter runs.
    /// </summary>
    private static IEnumerable<string> EnumerateTestSources(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
            yield return file;

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(child);
            if (name is "bin" or "obj")
                continue;

            foreach (var file in EnumerateTestSources(child))
                yield return file;
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "nextorm.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
