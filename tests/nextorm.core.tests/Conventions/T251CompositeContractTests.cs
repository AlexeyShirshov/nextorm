using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Public contract for the composite relationship surface (#251): the ten A–J declaration overloads
/// with their documented parameter types, the ordered <see cref="IRelationshipMetadata"/> key lists,
/// the unchanged six legacy fluent signatures, and the presence of the corresponding EN/RU guide
/// sections. Structural checks over reflection and documented phrases only — no behaviour tests and
/// no snapshot of a whole file.
/// </summary>
public sealed class T251CompositeContractTests
{
    private const string RelationshipsEn = "docs/relationships/relationships.md";
    private const string RelationshipsRu = "docs/ru/relationships/relationships.md";
    private const string ImplicitEn = "docs/relationships/implicit-navigation.md";
    private const string ImplicitRu = "docs/ru/relationships/implicit-navigation.md";
    private const string ComparisonsEn = "docs/comparisons/index.md";
    private const string ComparisonsRu = "docs/ru/comparisons/index.md";

    private static readonly Type OpenBuilder = typeof(EntityMetadataBuilder<>);

    private static readonly string[] SharedDocumentedTokens =
    [
        "EntityMetadataBuilder<T> HasMany<TChild>(",
        "EntityMetadataBuilder<T> HasOne<TChild>(",
        "EntityMetadataBuilder<T> HasOneToOne<TChild, TKey>(",
        "EntityMetadataBuilder<T> HasManyThrough<TChild, TJunction>(",
        "Expression<Func<T, object?>> principalKey",
        "Expression<Func<TJunction, object?>> junctionChildForeignKey",
        "ForeignKeys",
        "PrincipalKeys",
        "ArgumentNullException",
        "NotSupportedException",
    ];

    public sealed record Signature(string Name, string[] GenericParameters, string[] Parameters);

    [Fact]
    public void TenDeclarationOverloads_ShouldMatchTheDocumentedSignatures()
    {
        var overloads = new[]
        {
            // A — 1:N, navigation + explicit foreign key + explicit principal key.
            new Signature(
                "HasMany",
                ["TChild"],
                ["Expression<Func<T,ICollection<TChild>>>", "Expression<Func<TChild,object>>", "Expression<Func<T,object>>"]),
            // B — 1:N, no navigation, explicit foreign key + explicit principal key.
            new Signature(
                "HasMany",
                ["TChild"],
                ["Expression<Func<TChild,object>>", "Expression<Func<T,object>>"]),
            // C — N:1, navigation + explicit foreign key + explicit principal key.
            new Signature(
                "HasOne",
                ["TChild"],
                ["Expression<Func<T,TChild>>", "Expression<Func<T,object>>", "Expression<Func<TChild,object>>"]),
            // D — N:1, no navigation, explicit foreign key + explicit principal key.
            new Signature(
                "HasOne",
                ["TChild"],
                ["Expression<Func<T,object>>", "Expression<Func<TChild,object>>"]),
            // E — 1:1, navigation + explicit parent key + explicit child foreign key.
            new Signature(
                "HasOneToOne",
                ["TChild"],
                ["Expression<Func<T,TChild>>", "Expression<Func<T,object>>", "Expression<Func<TChild,object>>"]),
            // F — M:N, navigation + all four explicit key lists.
            new Signature(
                "HasManyThrough",
                ["TChild", "TJunction"],
                [
                    "Expression<Func<T,IEnumerable<TChild>>>",
                    "Expression<Func<T,object>>",
                    "Expression<Func<TJunction,object>>",
                    "Expression<Func<TChild,object>>",
                    "Expression<Func<TJunction,object>>",
                ]),
            // G — 1:N, no navigation; principal = the full key of T.
            new Signature(
                "HasMany",
                ["TChild"],
                ["Expression<Func<TChild,object>>"]),
            // H — N:1, no navigation; principal = the full key of TChild.
            new Signature(
                "HasOne",
                ["TChild"],
                ["Expression<Func<T,object>>"]),
            // I — 1:1 with the child foreign key only; parent key = the full key of T.
            new Signature(
                "HasOneToOne",
                ["TChild", "TKey"],
                ["Expression<Func<T,TChild>>", "Expression<Func<TChild,TKey>>"]),
            // J — M:N with the two junction foreign keys only; both entity keys = full keys.
            new Signature(
                "HasManyThrough",
                ["TChild", "TJunction"],
                [
                    "Expression<Func<T,IEnumerable<TChild>>>",
                    "Expression<Func<TJunction,object>>",
                    "Expression<Func<TJunction,object>>",
                ]),
        };

        foreach (var signature in overloads)
            Describe(Resolve(signature).ReturnType).Should().Be("EntityMetadataBuilder<T>",
                "every declaration overload returns EntityMetadataBuilder<T>");
    }

    [Fact]
    public void LegacySignatures_ShouldRemainUnchanged()
    {
        var legacy = new[]
        {
            new Signature(
                "HasMany",
                ["TChild", "TKey"],
                ["Expression<Func<T,ICollection<TChild>>>", "Expression<Func<TChild,TKey>>"]),
            new Signature(
                "HasMany",
                ["TChild", "TKey"],
                ["Expression<Func<TChild,TKey>>", "Expression<Func<T,TKey>>"]),
            new Signature(
                "HasOne",
                ["TChild", "TKey"],
                ["Expression<Func<T,TChild>>", "Expression<Func<T,TKey>>"]),
            new Signature(
                "HasOne",
                ["TChild", "TKey"],
                ["Expression<Func<T,TKey>>", "Expression<Func<TChild,TKey>>"]),
            new Signature(
                "HasOneToOne",
                ["TChild", "TKey"],
                ["Expression<Func<T,TChild>>", "Expression<Func<T,TKey>>", "Expression<Func<TChild,TKey>>"]),
            new Signature(
                "HasManyThrough",
                ["TChild", "TJunction", "TParentKey", "TChildKey"],
                [
                    "Expression<Func<T,IEnumerable<TChild>>>",
                    "Expression<Func<T,TParentKey>>",
                    "Expression<Func<TJunction,TParentKey>>",
                    "Expression<Func<TChild,TChildKey>>",
                    "Expression<Func<TJunction,TChildKey>>",
                ]),
        };

        foreach (var signature in legacy)
            Describe(Resolve(signature).ReturnType).Should().Be("EntityMetadataBuilder<T>",
                "the six legacy fluent signatures keep their shape and return type");
    }

    [Fact]
    public void RelationshipMetadataKeyLists_ShouldBeOrderedPropertyMetadataLists()
    {
        typeof(IRelationshipMetadata).GetProperty(nameof(IRelationshipMetadata.ForeignKey))!
            .PropertyType.Should().Be(typeof(IReadOnlyList<IPropertyMetadata>));

        typeof(IRelationshipMetadata).GetProperty(nameof(IRelationshipMetadata.PrincipalKey))!
            .PropertyType.Should().Be(typeof(IReadOnlyList<IPropertyMetadata>));
    }

    [Fact]
    public void RelationshipsGuides_ShouldDocumentTheNewSurfaceInBothLanguages()
    {
        foreach (var path in new[] { RelationshipsEn, RelationshipsRu })
        {
            var text = Read(path);
            foreach (var token in SharedDocumentedTokens)
                text.Should().Contain(token, $"{path} documents the composite declaration surface");
        }

        Read(RelationshipsEn).Should()
            .Contain("The **order is the contract**")
            .And.Contain("no first-key fallback")
            .And.Contain("Ordered key selectors")
            .And.Contain("Validation timing");

        Read(RelationshipsRu).Should()
            .Contain("Порядок — это контракт")
            .And.Contain("Fallback на первый ключ отсутствует")
            .And.Contain("Упорядоченные селекторы ключей")
            .And.Contain("Момент валидации");
    }

    [Fact]
    public void ImplicitNavigationGuides_ShouldDescribeFullComponentCorrelation()
    {
        Read(ImplicitEn).Should()
            .Contain("correlate on **every** declared component")
            .And.Contain("no `null == null`")
            .And.NotContain("rejects a composite key");

        Read(ImplicitRu).Should()
            .Contain("коррелируют по **каждому** объявленному компоненту")
            .And.Contain("`null == null`")
            .And.NotContain("отклоняет составной ключ");
    }

    [Fact]
    public void ComparisonGuides_ShouldNotClaimCompositeRejection()
    {
        Read(ComparisonsEn).Should()
            .Contain("composite foreign, principal and junction")
            .And.NotContain("still rejects composite keys");

        Read(ComparisonsRu).Should()
            .Contain("составные внешние")
            .And.NotContain("всё ещё отклоняет составные");
    }

    [Fact]
    public void EagerLoadingPages_ShouldNotDeclareTheNewOverloads()
    {
        // The A–J surface belongs to the relationships guides; the eager-loading pages are owned by
        // the separate LoadWith work and must not acquire these declarations.
        var eagerLoadingPages = new[]
        {
            "docs/relationships/eager-loading.md",
            "docs/ru/relationships/eager-loading.md",
            "docs/relationships/eager-loading-per-parent-paging.md",
            "docs/ru/relationships/eager-loading-per-parent-paging.md",
        };

        foreach (var path in eagerLoadingPages)
        {
            var text = Read(path);
            text.Should().NotContain("HasManyThrough<TChild, TJunction>");
            text.Should().NotContain("EntityMetadataBuilder<T> HasOneToOne<TChild, TKey>(");
            text.Should().NotContain("PrincipalKeys");
            text.Should().NotContain("A–J");
        }
    }

    [Fact]
    public void PublicGuides_ShouldNotLinkToInternalSpecs()
    {
        var editedPages = new[]
        {
            RelationshipsEn,
            RelationshipsRu,
            ImplicitEn,
            ImplicitRu,
            ComparisonsEn,
            ComparisonsRu,
        };

        foreach (var path in editedPages)
        {
            var text = Read(path);
            text.Should().NotContain("docs/specs");
            text.Should().NotContain("../specs");
        }
    }

    private static MethodInfo Resolve(Signature signature)
    {
        var candidates = OpenBuilder
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == signature.Name && method.IsGenericMethodDefinition)
            .Where(method => method.GetGenericArguments().Select(argument => argument.Name)
                .SequenceEqual(signature.GenericParameters, StringComparer.Ordinal))
            .Where(method => method.GetParameters().Select(parameter => Describe(parameter.ParameterType))
                .SequenceEqual(signature.Parameters, StringComparer.Ordinal))
            .ToList();

        candidates.Should().ContainSingle(
            $"exactly one '{signature.Name}<{string.Join(", ", signature.GenericParameters)}>' overload matches the documented signature");
        return candidates[0];
    }

    /// <summary>
    /// Renders a reflected type in the same compact form the documented signatures use, so a match is
    /// a semantic parameter-shape assertion rather than a source-text snapshot.
    /// </summary>
    private static string Describe(Type type)
    {
        if (type.IsGenericParameter)
            return type.Name;

        if (type.IsArray)
            return Describe(type.GetElementType()!) + "[]";

        if (type == typeof(object))
            return "object";

        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0)
            name = name[..tick];

        return $"{name}<{string.Join(",", type.GetGenericArguments().Select(Describe))}>";
    }

    private static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nextorm.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
