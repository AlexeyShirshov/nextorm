using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NextORM.Core.Tests;

/// <summary>
/// T342 (#342) guard for the process-wide <see cref="DataContextCache"/> isolation contract.
/// <para>
/// The assembly fixture (<c>Support/ProcessWideCacheIsolationFixture</c>) is the universal backstop: it
/// restores the sliding-expiration window captured at assembly start and clears the five process-wide
/// caches after <b>every</b> test. This guard keeps that contract from rotting silently, because a
/// misplaced clear, a forgotten window restore or a cache asserter outside the serialized collection
/// reintroduces the order-dependent failure of <c>TimedDictionaryEvictionTests</c> without failing
/// anywhere else.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Every check is a pure function over <c>(relativePath, sourceText)</c> pairs. The facts feed them the
/// real project sources; the control fact feeds them synthetic violators, so each rule is proven to fire
/// on input it must reject instead of waiting for a real offender to appear.
/// </para>
/// <para>
/// Sources are parsed, never grepped: a cache write mentioned in a comment, a doc-comment or a string
/// literal is not a violation, while an aliased or qualified call is. Findings are reported as
/// <c>relativePath:line: code: reason</c>, one diagnostic per broken element, and the diagnostic codes are
/// stable so the control fact can name the exact rule it expects.
/// </para>
/// </remarks>
public sealed class ProcessWideCacheIsolationTests
{
    private const string ProjectDirectory = "tests/nextorm.core.tests";

    private const string FixtureRelativePath = ProjectDirectory + "/Support/ProcessWideCacheIsolationFixture.cs";

    private const string FixtureTypeName = "ProcessWideCacheIsolationFixture";

    private const string ControlsCollectionName = "Query cache controls";

    private const string CacheReceiver = "DataContextCache";

    private const string WindowMember = "CacheSlidingExpiration";

    // Stable diagnostic codes: the control fact asserts on these, the facts print them.
    private const string RegistrationMissing = "T342-REG-MISSING";
    private const string RegistrationDuplicate = "T342-REG-DUPLICATE";
    private const string RegistrationTypeMissing = "T342-REG-TYPE-MISSING";
    private const string FixtureMissing = "T342-FIXTURE-MISSING";
    private const string FixtureStartMissing = "T342-FIXTURE-START-MISSING";
    private const string FixtureFinishMissing = "T342-FIXTURE-FINISH-MISSING";
    private const string FixtureStartClears = "T342-FIXTURE-START-CLEARS";
    private const string FixtureFinishNoClear = "T342-FIXTURE-FINISH-NO-CLEAR";
    private const string FixtureFinishNoRestore = "T342-FIXTURE-FINISH-NO-RESTORE";
    private const string SerializationMissing = "T342-SERIALIZATION-MISSING";
    private const string WindowNoRestore = "T342-WINDOW-NO-RESTORE";
    private const string ReaderNoIsolation = "T342-READER-NO-ISOLATION";

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    /// <summary>The five process-wide caches the fixture owns.</summary>
    private static readonly HashSet<string> CacheNames = new(StringComparer.Ordinal)
    {
        "Metadata",
        "TvpMetadata",
        "SelectListCache",
        "ExpressionsCache",
        "InValuesCache",
    };

    /// <summary>Members that read a cache without writing it.</summary>
    private static readonly HashSet<string> CacheReaderMembers = new(StringComparer.Ordinal)
    {
        "ContainsKey",
        "TryGetValue",
    };

    /// <summary>
    /// E12 (T342) registration: the assembly fixture is declared exactly once, and the type it names
    /// exists in the project. Zero registrations means no universal teardown runs at all; two means the
    /// runner would create two fixtures with two captured windows.
    /// </summary>
    [Fact]
    public void T342_Registration_ShouldExistExactlyOnce()
    {
        var diagnostics = CheckRegistration(ProjectSources());

        Fail(
            diagnostics,
            $"the assembly fixture must be registered exactly once under {ProjectDirectory} "
            + $"([assembly: AssemblyFixture(typeof({FixtureTypeName}))]) and its type must exist");
    }

    /// <summary>
    /// E12 (T342) lifecycle: the fixture performs <b>no</b> cleanup when a test starts (a leaked entry must
    /// stay observable to the test that inherits it) and both restores the assembly window and clears the
    /// caches when a test finishes.
    /// </summary>
    [Fact]
    public void T342_Fixture_ShouldCleanOnFinishOnly()
    {
        var source = ProjectSources().FirstOrDefault(file => file.RelativePath == FixtureRelativePath);
        if (source is null)
        {
            Fail([new GuardDiagnostic("<project>", 0, FixtureMissing, $"{FixtureRelativePath} does not exist")], string.Empty);
            return;
        }

        Fail(
            CheckFixtureLifecycle(source),
            $"{FixtureRelativePath} must clean on test finish only: no start-of-test clear, "
            + "a finish callback that calls DataContextCache.Clear() and restores DataContextCache.CacheSlidingExpiration");
    }

    /// <summary>
    /// E12 (T342) serialization: the assembly stays serialized. The isolation contract is only sound while
    /// no two tests touch the process-wide caches concurrently.
    /// </summary>
    [Fact]
    public void T342_Serialization_ShouldRemainEnabled()
    {
        Fail(
            CheckSerialization(ProjectSources()),
            "the assembly must stay serialized: [assembly: Parallelization(Mode = ParallelMode.None)]");
    }

    /// <summary>
    /// E12 (T342) window ownership: a file that assigns <see cref="DataContextCache.CacheSlidingExpiration"/>
    /// must assign it back inside a <c>finally</c> (or a <c>Dispose</c>). Only the fixture is exempt - it is
    /// the universal backstop and captures the window once at assembly start.
    /// </summary>
    [Fact]
    public void T342_WindowMutators_ShouldRestoreInFinally()
    {
        var diagnostics = ProjectSources().SelectMany(CheckWindowMutators).ToArray();

        Fail(
            diagnostics,
            $"a test that assigns {CacheReceiver}.{WindowMember} must restore it inside a finally (or Dispose); "
            + "the assembly fixture is the only exemption");
    }

    /// <summary>
    /// E12 (T342) asserter isolation: a file that reads one of the five process-wide caches (or ages them
    /// through <c>ExpireTimedEntriesForTesting</c>) either joins the <c>Query cache controls</c> collection
    /// or clears the caches in a <c>finally</c>. Every offender is listed, never allowlisted.
    /// </summary>
    [Fact]
    public void T342_CacheReaders_ShouldBeIsolated()
    {
        var diagnostics = ProjectSources().SelectMany(CheckCacheReaders).ToArray();

        Fail(
            diagnostics,
            $"a file that reads the process-wide caches must carry [Collection(\"{ControlsCollectionName}\")] "
            + $"or clear {CacheReceiver} in a finally");
    }

    /// <summary>
    /// E12 (T342) control: every guard rule must fire on a synthetic violator and stay silent on a correct
    /// pair. A rule that cannot be shown to detect anything is not a guard.
    /// </summary>
    [Fact]
    public void T342_GuardControls_ShouldDetectViolations()
    {
        var failures = new List<string>();

        foreach (var (name, expectedCode, check) in GuardControls())
        {
            var codes = check().Select(diagnostic => diagnostic.Code).ToArray();
            var detected = expectedCode is null ? codes.Length == 0 : Array.IndexOf(codes, expectedCode) >= 0;
            if (!detected)
                failures.Add($"{name}: expected {expectedCode ?? "no diagnostic"} but got [{string.Join(", ", codes)}]");
        }

        failures.Should().BeEmpty(
            "every synthetic violation must be reported and a correct pair must stay silent:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, failures));
    }

    // -- checks (pure over relativePath + sourceText) -------------------------

    private static IReadOnlyList<GuardDiagnostic> CheckRegistration(IReadOnlyList<SourceFile> sources)
    {
        var diagnostics = new List<GuardDiagnostic>();
        var registrations = new List<GuardDiagnostic>();
        var fixtureTypeDeclared = false;

        foreach (var source in sources)
        {
            var root = Parse(source);

            fixtureTypeDeclared |= root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Any(type => type.Identifier.Text == FixtureTypeName);

            foreach (var attribute in AssemblyAttributes(root, "AssemblyFixture"))
            {
                if (attribute.ArgumentList is not { } arguments)
                    continue;

                if (arguments.Arguments.Any(argument =>
                        argument.Expression.ToString().Contains(FixtureTypeName, StringComparison.Ordinal)))
                {
                    registrations.Add(new GuardDiagnostic(
                        source.RelativePath,
                        Line(attribute),
                        RegistrationDuplicate,
                        $"duplicate [assembly: AssemblyFixture(typeof({FixtureTypeName}))]"));
                }
            }
        }

        if (registrations.Count == 0)
        {
            diagnostics.Add(new GuardDiagnostic(
                "<project>",
                0,
                RegistrationMissing,
                $"no [assembly: AssemblyFixture(typeof({FixtureTypeName}))] found under {ProjectDirectory}"));
        }
        else if (registrations.Count > 1)
        {
            diagnostics.AddRange(registrations.Skip(1));
        }

        if (!fixtureTypeDeclared)
        {
            diagnostics.Add(new GuardDiagnostic(
                "<project>",
                0,
                RegistrationTypeMissing,
                $"type {FixtureTypeName} is not declared under {ProjectDirectory}"));
        }

        return diagnostics;
    }

    private static IReadOnlyList<GuardDiagnostic> CheckFixtureLifecycle(SourceFile source)
    {
        var root = Parse(source);
        var startCallbacks = CallbackMembers(root, "OnTestStarting", "Start");
        var finishCallbacks = CallbackMembers(root, "OnTestFinished", "Finish");
        var diagnostics = new List<GuardDiagnostic>();

        if (startCallbacks.Count == 0)
        {
            diagnostics.Add(new GuardDiagnostic(
                source.RelativePath, 1, FixtureStartMissing,
                "no test-start callback (OnTestStarting or Start) found"));
        }
        else
        {
            var clearing = startCallbacks.FirstOrDefault(ClearsProcessWideCaches);
            if (clearing is not null)
            {
                diagnostics.Add(new GuardDiagnostic(
                    source.RelativePath, Line(clearing), FixtureStartClears,
                    $"the test-start callback clears {CacheReceiver}; a leaked entry must stay observable "
                    + "to the test that inherits it, so cleanup belongs to the finish callback only"));
            }
        }

        if (finishCallbacks.Count == 0)
        {
            diagnostics.Add(new GuardDiagnostic(
                source.RelativePath, 1, FixtureFinishMissing,
                "no test-finish callback (OnTestFinished or Finish) found"));
            return diagnostics;
        }

        if (!finishCallbacks.Any(ClearsProcessWideCaches))
        {
            diagnostics.Add(new GuardDiagnostic(
                source.RelativePath, Line(finishCallbacks[0]), FixtureFinishNoClear,
                $"the test-finish callback never calls {CacheReceiver}.Clear()"));
        }

        if (!finishCallbacks.Any(RestoresWindow))
        {
            diagnostics.Add(new GuardDiagnostic(
                source.RelativePath, Line(finishCallbacks[0]), FixtureFinishNoRestore,
                $"the test-finish callback never restores {CacheReceiver}.{WindowMember}"));
        }

        return diagnostics;
    }

    private static IReadOnlyList<GuardDiagnostic> CheckSerialization(IReadOnlyList<SourceFile> sources)
    {
        foreach (var source in sources)
        {
            foreach (var attribute in AssemblyAttributes(Parse(source), "Parallelization"))
            {
                var mode = attribute.ArgumentList?.Arguments
                    .FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.Text == "Mode");
                if (mode is not null
                    && mode.Expression.ToString().EndsWith("ParallelMode.None", StringComparison.Ordinal))
                {
                    return [];
                }
            }
        }

        return
        [
            new GuardDiagnostic(
                "<project>", 0, SerializationMissing,
                "the assembly is no longer serialized: [assembly: Parallelization(Mode = ParallelMode.None)]"),
        ];
    }

    private static IReadOnlyList<GuardDiagnostic> CheckWindowMutators(SourceFile source)
    {
        // The fixture is the universal backstop: it captures the assembly window in its constructor and
        // restores it on every finish, so it is the single file exempt from the per-file restore rule.
        if (source.RelativePath == FixtureRelativePath)
            return [];

        var root = Parse(source);
        var mutations = root.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(IsWindowAssignment)
            .ToArray();

        if (mutations.Length == 0)
            return [];

        if (mutations.Any(IsRestoreSite))
            return [];

        return
        [
            new GuardDiagnostic(
                source.RelativePath, Line(mutations[0]), WindowNoRestore,
                $"assigns {CacheReceiver}.{WindowMember} but never restores it inside a finally "
                + "(or a Dispose); the window leaks into the following tests"),
        ];
    }

    private static IReadOnlyList<GuardDiagnostic> CheckCacheReaders(SourceFile source)
    {
        var root = Parse(source);
        var reads = CacheReadSites(root).ToArray();

        if (reads.Length == 0)
            return [];

        if (HasControlsCollection(root) || ClearsInFinally(root))
            return [];

        return
        [
            new GuardDiagnostic(
                source.RelativePath, Line(reads[0]), ReaderNoIsolation,
                $"reads the process-wide caches but neither joins [Collection(\"{ControlsCollectionName}\")] "
                + $"nor clears {CacheReceiver} in a finally"),
        ];
    }

    // -- controls: synthetic sources with an expected outcome -----------------

    private static IEnumerable<(string Name, string? ExpectedCode, Func<IReadOnlyList<GuardDiagnostic>> Check)> GuardControls()
    {
        yield return (
            "missing assembly-fixture registration",
            RegistrationMissing,
            () => CheckRegistration([Fixture(FixtureRelativePath, CleanFixtureSource)]));

        yield return (
            "finish callback without Clear()",
            FixtureFinishNoClear,
            () => CheckFixtureLifecycle(Fixture(FixtureRelativePath, FixtureWithoutClearSource)));

        yield return (
            "clear in the start callback",
            FixtureStartClears,
            () => CheckFixtureLifecycle(Fixture(FixtureRelativePath, FixtureClearingOnStartSource)));

        yield return (
            "window mutation without a finally restore",
            WindowNoRestore,
            () => CheckWindowMutators(Fixture("tests/nextorm.core.tests/Example/WindowTestSource.cs", WindowWithoutRestoreSource)));

        yield return (
            "cache reader outside the collection",
            ReaderNoIsolation,
            () => CheckCacheReaders(Fixture("tests/nextorm.core.tests/Example/ReaderTestSource.cs", ReaderWithoutIsolationSource)));

        yield return (
            "correct registration, fixture and isolated reader",
            null,
            () =>
            {
                var sources = CorrectSources();
                var fixture = sources.Single(source => source.RelativePath == FixtureRelativePath);

                return CheckRegistration(sources)
                    .Concat(CheckFixtureLifecycle(fixture))
                    .Concat(CheckSerialization(sources))
                    .Concat(sources.SelectMany(CheckWindowMutators))
                    .Concat(sources.SelectMany(CheckCacheReaders))
                    .ToArray();
            });
    }

    private static IReadOnlyList<SourceFile> CorrectSources() =>
    [
        Fixture("tests/nextorm.core.tests/Usings.cs", RegistrationSource),
        Fixture(FixtureRelativePath, CleanFixtureSource),
        Fixture("tests/nextorm.core.tests/Example/IsolatedReaderTestSource.cs", IsolatedReaderSource),
        Fixture("tests/nextorm.core.tests/Example/WindowTestSource.cs", WindowRestoringSource),
    ];

    private const string RegistrationSource = """
        [assembly: Parallelization(Mode = ParallelMode.None)]
        [assembly: AssemblyFixture(typeof(NextORM.Core.Tests.ProcessWideCacheIsolationFixture))]
        """;

    private const string CleanFixtureSource = """
        public sealed class ProcessWideCacheIsolationFixture
        {
            private readonly System.TimeSpan _window;

            public ProcessWideCacheIsolationFixture() => _window = DataContextCache.CacheSlidingExpiration;

            public void OnTestStarting(IXunitTest test) => Start(test.TestMethod.Method.Name);

            public void OnTestFinished(IXunitTest test) => Finish(test.TestMethod.Method.Name);

            public void Start(string testName) => System.Console.WriteLine(testName);

            public void Finish(string testName)
            {
                DataContextCache.CacheSlidingExpiration = _window;
                DataContextCache.Clear();
            }
        }
        """;

    private const string FixtureWithoutClearSource = """
        public sealed class ProcessWideCacheIsolationFixture
        {
            private readonly System.TimeSpan _window;

            public void OnTestStarting(IXunitTest test) => Start(test.TestMethod.Method.Name);

            public void OnTestFinished(IXunitTest test) => Finish(test.TestMethod.Method.Name);

            public void Start(string testName) => System.Console.WriteLine(testName);

            public void Finish(string testName)
            {
                DataContextCache.CacheSlidingExpiration = _window;
                System.Console.WriteLine(testName);
            }
        }
        """;

    private const string FixtureClearingOnStartSource = """
        public sealed class ProcessWideCacheIsolationFixture
        {
            private readonly System.TimeSpan _window;

            public void OnTestStarting(IXunitTest test) => Start(test.TestMethod.Method.Name);

            public void OnTestFinished(IXunitTest test) => Finish(test.TestMethod.Method.Name);

            public void Start(string testName)
            {
                DataContextCache.Clear();
            }

            public void Finish(string testName)
            {
                DataContextCache.CacheSlidingExpiration = _window;
                DataContextCache.Clear();
            }
        }
        """;

    private const string WindowWithoutRestoreSource = """
        public sealed class WindowTests
        {
            [Fact]
            public void Mutates_Without_Restoring()
            {
                DataContextCache.CacheSlidingExpiration = System.TimeSpan.FromMilliseconds(1000);
                System.Console.WriteLine("body");
            }
        }
        """;

    private const string WindowRestoringSource = """
        public sealed class WindowTests
        {
            [Fact]
            public void Mutates_And_Restores()
            {
                var previous = DataContextCache.CacheSlidingExpiration;
                try
                {
                    DataContextCache.CacheSlidingExpiration = System.TimeSpan.FromMilliseconds(1000);
                    System.Console.WriteLine(previous.ToString());
                }
                finally
                {
                    DataContextCache.CacheSlidingExpiration = previous;
                }
            }
        }
        """;

    private const string ReaderWithoutIsolationSource = """
        public sealed class ReaderTests
        {
            [Fact]
            public void Reads_The_Cache()
            {
                DataContextCache.Metadata.ContainsKey(typeof(string)).Should().BeFalse();
            }
        }
        """;

    private const string IsolatedReaderSource = """
        [Collection("Query cache controls")]
        public sealed class ReaderTests
        {
            [Fact]
            public void Reads_The_Cache()
            {
                DataContextCache.Metadata.ContainsKey(typeof(string)).Should().BeFalse();
                DataContextCache.ExpireTimedEntriesForTesting();
            }
        }
        """;

    // -- syntax helpers ------------------------------------------------------

    private static IReadOnlyList<MethodDeclarationSyntax> CallbackMembers(
        CompilationUnitSyntax root,
        params string[] names) =>
        root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method => Array.IndexOf(names, method.Identifier.Text) >= 0)
            .ToArray();

    private static bool ClearsProcessWideCaches(MethodDeclarationSyntax callback) =>
        callback.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => IsCacheClearCall(invocation));

    private static bool IsCacheClearCall(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Clear" } access
        && IsCacheReceiver(access.Expression);

    private static bool RestoresWindow(MethodDeclarationSyntax callback) =>
        callback.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(IsWindowAssignment);

    private static bool IsWindowAssignment(AssignmentExpressionSyntax assignment) =>
        assignment.Left is MemberAccessExpressionSyntax access
        && IsCacheReceiver(access.Expression)
        && access.Name.Identifier.Text == WindowMember;

    /// <summary>
    /// The assignment puts the window back where a later test finds it: it lives in a <c>finally</c> (the
    /// guaranteed path) or in a <c>Dispose</c> (the disposal protocol).
    /// </summary>
    private static bool IsRestoreSite(AssignmentExpressionSyntax assignment) =>
        assignment.Ancestors().Any(ancestor => ancestor is FinallyClauseSyntax)
        || assignment.Ancestors().OfType<MethodDeclarationSyntax>()
            .Any(method => method.Identifier.Text is "Dispose" or "DisposeAsync");

    private static IEnumerable<SyntaxNode> CacheReadSites(CompilationUnitSyntax root)
    {
        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case MemberAccessExpressionSyntax access
                    when CacheMember(access.Expression) is not null
                         && CacheReaderMembers.Contains(access.Name.Identifier.Text):
                    yield return access;
                    break;

                case ElementAccessExpressionSyntax element
                    when CacheMember(element.Expression) is not null && !IsWriteTarget(element):
                    yield return element;
                    break;

                case InvocationExpressionSyntax invocation
                    when invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "ExpireTimedEntriesForTesting" }:
                    yield return invocation;
                    break;
            }
        }
    }

    /// <summary>Names the cache when <paramref name="expression"/> is <c>DataContextCache.&lt;one of the five&gt;</c>.</summary>
    private static string? CacheMember(ExpressionSyntax expression)
    {
        if (expression is MemberAccessExpressionSyntax access
            && IsCacheReceiver(access.Expression)
            && CacheNames.Contains(access.Name.Identifier.Text))
        {
            return access.Name.Identifier.Text;
        }

        return null;
    }

    private static bool IsCacheReceiver(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax { Identifier.Text: CacheReceiver };

    private static bool IsWriteTarget(ElementAccessExpressionSyntax element) =>
        element.Parent is AssignmentExpressionSyntax assignment
        && assignment.Left.DescendantNodesAndSelf().Contains(element);

    private static bool HasControlsCollection(CompilationUnitSyntax root) =>
        root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Any(type => type.AttributeLists
                .SelectMany(list => list.Attributes)
                .Any(attribute =>
                    attribute.Name.ToString() is "Collection" or "CollectionAttribute"
                    && attribute.ArgumentList is { } arguments
                    && arguments.Arguments.Any(argument =>
                        argument.Expression is LiteralExpressionSyntax literal
                        && literal.Token.ValueText == ControlsCollectionName)));

    private static bool ClearsInFinally(CompilationUnitSyntax root) =>
        root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => IsCacheClearCall(invocation)
                && invocation.Ancestors().Any(ancestor => ancestor is FinallyClauseSyntax));

    private static IEnumerable<AttributeSyntax> AssemblyAttributes(CompilationUnitSyntax root, string name) =>
        root.DescendantNodes()
            .OfType<AttributeListSyntax>()
            .Where(list => list.Target?.Identifier.Text == "assembly")
            .SelectMany(list => list.Attributes)
            .Where(attribute =>
            {
                var attributeName = attribute.Name.ToString();
                return attributeName == name || attributeName == "Xunit." + name;
            });

    // -- plumbing ------------------------------------------------------------

    private static SourceFile Fixture(string relativePath, string text) => new(relativePath, text);

    private static CompilationUnitSyntax Parse(SourceFile source) =>
        CSharpSyntaxTree.ParseText(source.Text, ParseOptions, source.RelativePath)
            .GetCompilationUnitRoot();

    private static int Line(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static void Fail(IReadOnlyList<GuardDiagnostic> diagnostics, string because)
    {
        var offenders = diagnostics.Select(diagnostic => diagnostic.ToString()).ToArray();

        offenders.Should().BeEmpty(
            because
            + Environment.NewLine
            + "Offenders:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    private static IReadOnlyList<SourceFile> ProjectSources()
    {
        var root = RepoRoot();

        return EnumerateSourceFiles(Path.Combine(root, ProjectDirectory))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new SourceFile(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)))
            .ToArray();
    }

    /// <summary>
    /// Walks the project without descending into <c>bin</c>/<c>obj</c>: a recursive
    /// <see cref="SearchOption.AllDirectories"/> scan would read every build artifact under the tree.
    /// </summary>
    private static IEnumerable<string> EnumerateSourceFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
            yield return file;

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(child);
            if (name is "bin" or "obj")
                continue;

            foreach (var file in EnumerateSourceFiles(child))
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

    /// <summary>A source file as the checks see it: a relative path and its text, nothing else.</summary>
    private sealed record SourceFile(string RelativePath, string Text);

    /// <summary>One broken element. <c>Line</c> is 1-based, or 0 when the finding is not tied to a location.</summary>
    private readonly record struct GuardDiagnostic(string RelativePath, int Line, string Code, string Message)
    {
        public override string ToString() =>
            Line > 0
                ? $"{RelativePath}:{Line}: {Code}: {Message}"
                : $"{RelativePath}: {Code}: {Message}";
    }
}
