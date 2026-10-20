using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NextORM.Core.Tests;

/// <summary>
/// House rules for the integration project (AGENTS.md, "Tests"): a test in
/// <c>tests/nextorm.integration.tests</c> must exercise a real database management system, and the
/// in-memory provider is never one. The in-memory provider, type/DI/exception contracts and
/// SQL-assembly-only assertions over a placeholder connection are unit tests and belong to the
/// matching unit project (<c>tests/nextorm.core.tests</c> or <c>tests/nextorm.&lt;provider&gt;.tests</c>).
/// <para>
/// Core-level tests are allowed in the integration project against a <em>single</em> provider (SQLite
/// is the natural choice: in-process, no container), so an explicit SQLite context counts as
/// database evidence alongside the full harness (<see cref="NextORM.Integration.Tests.ProviderTestSuite"/>),
/// a container, an ADO.NET connection, EF Core, or a connection-open call.
/// </para>
/// <para>
/// The scanner parses the sources instead of grepping them, so a mention of <c>InMemoryDataContext</c>
/// in a doc-comment or a string literal is not a violation.
/// </para>
/// </summary>
public sealed class IntegrationSuiteScopingTests
{
    private const string IntegrationProjectDirectory = "nextorm.integration.tests";

    private static readonly HashSet<string> DatabaseEvidenceNames = new(StringComparer.Ordinal)
    {
        "ITestProvider",
        "TestDataRepository",
        "EnsureConnectionOpen",
        "GetConnection",
        "DbConnection",
        "DbContext",
        "DbContextOptionsBuilder",
        "NpgsqlConnection",
        "SqlConnection",
        "MySqlConnection",
        "MariaDbConnection",
        "ClickHouseConnection",
        "SqliteConnection",
        // Core-level tests are allowed in the integration project on a single provider; SQLite is the
        // in-process one, so an explicit SQLite context is database evidence by itself.
        "SqliteDataContext",
        "SqliteDataContextOptionsBuilderExtensions",
    };

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    [Fact]
    public void IntegrationTests_ShouldNotUseTheInMemoryProvider()
    {
        var integrationDirectory = Path.Combine(RepoRoot(), "tests", IntegrationProjectDirectory);
        var offenders = new List<string>();

        foreach (var file in EnumerateSources(integrationDirectory))
        {
            var text = File.ReadAllText(file);
            // Cheap pre-filter before parsing: the only banned construct names this type.
            if (!text.Contains("InMemoryDataContext", StringComparison.Ordinal))
                continue;

            var root = Parse(file, text);

            foreach (var usage in root.DescendantNodes())
            {
                // IdentifierName covers `new InMemoryDataContext()`, `using var ctx = new ...` and
                // `: InMemoryDataContext`; comments and string literals are trivia and never appear here.
                if (usage is not IdentifierNameSyntax { Identifier.Text: "InMemoryDataContext" })
                    continue;

                offenders.Add(
                    $"{Location(file, usage)}: InMemoryDataContext is not a database - "
                    + "move this test to tests/nextorm.core.tests");
            }
        }

        offenders.Should().BeEmpty(
            "an integration test must run against a real database management system; "
            + "the in-memory provider belongs to the core unit suite."
            + Environment.NewLine
            + "Offenders:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void IntegrationTestClasses_ShouldBeBackedByARealDatabase()
    {
        var integrationDirectory = Path.Combine(RepoRoot(), "tests", IntegrationProjectDirectory);
        var parsed = EnumerateSources(integrationDirectory)
            .Select(file => (File: file, Root: Parse(file, File.ReadAllText(file))))
            .ToList();

        // Partial classes (CommonTestSuite and its per-area files) declare their base list once, so
        // merge every declaration before resolving the ancestry of a suite.
        var baseTypes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (_, root) in parsed)
        {
            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (!baseTypes.TryGetValue(type.Identifier.Text, out var bases))
                    baseTypes[type.Identifier.Text] = bases = [];

                bases.AddRange(type.BaseList?.Types.Select(t => SimpleName(t.Type)) ?? []);
            }
        }

        var offenders = new List<string>();

        foreach (var (file, root) in parsed)
        {
            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (!HasTestFact(type))
                    continue;

                if (IsBackedByRealDatabase(type, baseTypes))
                    continue;

                offenders.Add(
                    $"{Location(file, type)}: {type.Identifier.Text} has no database evidence - "
                    + "a unit test belongs to nextorm.core.tests / nextorm.<provider>.tests, "
                    + "or it must run against a single provider (e.g. SQLite)");
            }
        }

        offenders.Should().BeEmpty(
            "every test class in the integration project must either derive from ProviderTestSuite or "
            + "touch a real database (harness/container, ADO.NET connection, EF Core DbContext)."
            + Environment.NewLine
            + "Offenders:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    private static bool IsBackedByRealDatabase(
        TypeDeclarationSyntax type,
        Dictionary<string, List<string>> baseTypes)
    {
        if (DerivesFromProviderSuite(type.Identifier.Text, baseTypes))
            return true;

        foreach (var name in type.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            var text = name.Identifier.Text;
            if (DatabaseEvidenceNames.Contains(text)
                || text.EndsWith("TestProvider", StringComparison.Ordinal)
                || text.EndsWith("Container", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DerivesFromProviderSuite(string typeName, Dictionary<string, List<string>> baseTypes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(typeName);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current == "ProviderTestSuite")
                return true;

            if (!seen.Add(current) || !baseTypes.TryGetValue(current, out var bases))
                continue;

            foreach (var @base in bases)
                pending.Push(@base);
        }

        return false;
    }

    private static bool HasTestFact(TypeDeclarationSyntax type) =>
        type.Members
            .OfType<MemberDeclarationSyntax>()
            .SelectMany(member => member.AttributeLists)
            .SelectMany(list => list.Attributes)
            .Select(attribute => SimpleName(attribute.Name))
            .Any(name => name is "Fact" or "Theory" or "FactAttribute" or "TheoryAttribute");

    private static string SimpleName(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic.Identifier.Text,
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => SimpleName(alias.Name),
        _ => type.ToString(),
    };

    private static string SimpleName(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => SimpleName(alias.Name),
        _ => name.ToString(),
    };

    private static SyntaxNode Parse(string file, string text) =>
        CSharpSyntaxTree.ParseText(
            text,
            ParseOptions,
            file,
            cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken);

    private static string Location(string file, SyntaxNode node)
    {
        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        return $"{Path.GetRelativePath(RepoRoot(), file)}:{line}";
    }

    /// <summary>
    /// Walks the integration tree without descending into <c>bin</c>/<c>obj</c>: a recursive
    /// <see cref="SearchOption.AllDirectories"/> scan would read every build artifact under the tree
    /// (hundreds of megabytes) before any filter runs.
    /// </summary>
    private static IEnumerable<string> EnumerateSources(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
            yield return file;

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(child);
            if (name is "bin" or "obj")
                continue;

            foreach (var file in EnumerateSources(child))
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
