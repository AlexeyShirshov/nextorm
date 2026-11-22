using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NextORM.Core;

namespace NextORM.AliasTests;

public sealed class InterfaceEntityGeneratorTests
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(IDataContext).Assembly.Location).Distinct(StringComparer.Ordinal)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();

    private static readonly Type Generator = Assembly
        .LoadFrom(Path.Combine(AppContext.BaseDirectory, "nextorm.core.sourcegenerator.dll"))
        .GetType("NextORM.Core.SourceGenerator.InterfaceEntityGenerator", throwOnError: true)!;

    [Fact]
    public void GenericHelperDiscoversExternalAndFriendInterfacesOnlyOnce()
    {
        using var referenceAssembly = new MemoryStream();
        var external = Build("D34.Models", """
            using System.Runtime.CompilerServices;
            [assembly: InternalsVisibleTo("D34.Friend")]
            namespace Models;
            public interface IPublic { int Id { get; } }
            internal interface IFriend { string Name { get; init; } }
            internal interface IPrivate { int Id { get; } }
            """);
        external.Emit(referenceAssembly, cancellationToken: TestContext.Current.CancellationToken).Success.Should().BeTrue();
        var reference = MetadataReference.CreateFromImage(referenceAssembly.ToArray());
        const string code = """
            using Models;
            using NextORM.Core;
            public static class Client
            {
                private static void Load<T>() { }
                public static void Read()
                {
                    Load<Projection<IPublic,IFriend>>();
                    Load<IPublic>(); Load<IPublic>(); Load<IFriend>();
                }
            }
            """;
        var compilation = Build("D34.Friend", code).AddReferences(reference);

        var first = Run(compilation, out var output);
        var second = Run(compilation, out _);
        first.Diagnostics.Should().BeEmpty();
        var generated = first.GeneratedTrees.Should().ContainSingle().Which.ToString();
        generated.Should().Be(second.GeneratedTrees.Single().ToString())
            .And.Contain("Register<global::Models.IPublic>").And.Contain("Register<global::Models.IFriend>")
            .And.NotContain("IPrivate");
        generated.Split("Register<global::Models.IPublic>", StringSplitOptions.None).Should().HaveCount(2);
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var outsideFriend = Run(Build("D34.Other", code).AddReferences(reference), out _);
        string.Concat(outsideFriend.GeneratedTrees.Select(tree => tree.ToString())).Should().NotContain("Models.IFriend");
    }

    [Theory]
    [InlineData("public interface IEntity { int Id { get; } void Run(); }", "member 'Run' is not a public instance property")]
    [InlineData("public interface IEntity : IBase { new string Id { get; } } public interface IBase { int Id { get; } }", "property 'Id' is declared with conflicting types")]
    [InlineData("public interface IEntity { System.IDisposable Nested { get; } }", "property 'Nested' must have an accessible, non-interface storage type")]
    public void UnsupportedEntityDiagnosticNamesTheShapeButIgnoresUnrelatedGenericUses(string model, string reason)
    {
        var compilation = Build("D34.Diagnostics", $$"""
            using NextORM.Core;
            {{model}}
            public interface IUnrelated { void DoWork(); }
            public static class Client
            {
                private static void Load<T>() { }
                public static void Read(IDataContext context)
                {
                    Load<IUnrelated>();
                    _ = context.From<IEntity>().ToList();
                }
            }
            """);

        var run = Run(compilation, out _);
        var diagnostic = run.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("NORMGEN010");
        diagnostic.GetMessage().Should().Contain(reason);
        diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan).Should().Be("IEntity");
    }

    private static CSharpCompilation Build(string name, string source) =>
        CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static GeneratorDriverRunResult Run(CSharpCompilation compilation, out Compilation output)
    {
        var generator = (IIncrementalGenerator)Activator.CreateInstance(Generator, nonPublic: true)!;
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());
        return driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out _).GetRunResult();
    }
}
