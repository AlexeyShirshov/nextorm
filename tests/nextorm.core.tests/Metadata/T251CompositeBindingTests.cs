using System.Collections.Immutable;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T251 binding oracle (AC251-01): a reproducible in-memory compilation of the declaration surface plus
/// a semantic-model lookup that pins which overload each representative call binds to, and runtime
/// metadata checks that the legacy shapes still declare the old relationships. The oracle compiles
/// against the product assembly of this snapshot, not a hand-written stand-in.
/// </summary>
public class T251CompositeBindingTests
{
    public T251CompositeBindingTests()
    {
        DataContextCache.Clear();
    }

    public sealed class Blog
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public ICollection<Post> Posts { get; set; } = new List<Post>();
        public Post? Featured { get; set; }
        public List<Tag> Tags { get; set; } = new List<Tag>();
    }

    public sealed class Post
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public int ParentTenantId { get; set; }
        public int ParentId { get; set; }
        public Blog? Parent { get; set; }
    }

    public sealed class Tag
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
    }

    public sealed class BlogTag
    {
        public int BlogTenantId { get; set; }
        public int BlogId { get; set; }
        public int TagTenantId { get; set; }
        public int TagId { get; set; }
    }

    public sealed class PlainParent
    {
        public int Id { get; set; }
        public ICollection<PlainChild> Children { get; set; } = new List<PlainChild>();
    }

    public sealed class PlainChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public PlainParent? Parent { get; set; }
    }

    public sealed class ProfileOwner
    {
        public int Id { get; set; }
        public Profile? Profile { get; set; }
    }

    public sealed class Profile
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
    }

    private const string Harness = """
        using System;
        using System.Collections.Generic;
        using System.Linq.Expressions;
        using NextORM.Core;

        namespace Harness
        {
            public sealed class Blog
            {
                public int TenantId { get; set; }
                public int Id { get; set; }
                public ICollection<Post> Posts { get; set; } = new List<Post>();
                public Post? Featured { get; set; }
                public List<Tag> Tags { get; set; } = new List<Tag>();
            }

            public sealed class Post
            {
                public int TenantId { get; set; }
                public int Id { get; set; }
                public int ParentTenantId { get; set; }
                public int ParentId { get; set; }
                public Blog? Parent { get; set; }
            }

            public sealed class Tag
            {
                public int TenantId { get; set; }
                public int Id { get; set; }
            }

            public sealed class BlogTag
            {
                public int BlogTenantId { get; set; }
                public int BlogId { get; set; }
                public int TagTenantId { get; set; }
                public int TagId { get; set; }
            }

            public sealed class PlainParent
            {
                public int Id { get; set; }
                public ICollection<PlainChild> Children { get; set; } = new List<PlainChild>();
            }

            public sealed class PlainChild
            {
                public int Id { get; set; }
                public int ParentId { get; set; }
                public PlainParent? Parent { get; set; }
            }

            public sealed class ProfileOwner
            {
                public int Id { get; set; }
                public Profile? Profile { get; set; }
            }

            public sealed class Profile
            {
                public int Id { get; set; }
                public int OwnerId { get; set; }
            }

            public static class Cases
            {
                public static void Run()
                {
        %BODY%
                }
            }
        }
        """;

    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    private static (Compilation Compilation, SyntaxTree Tree) Compile(string body)
    {
        var source = Harness.Replace("%BODY%", body, StringComparison.Ordinal);
        var tree = CSharpSyntaxTree.ParseText(source, path: "Harness.cs", cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "T251BindingHarness",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return (compilation, tree);
    }

    private static IMethodSymbol ResolveMethod(string body)
    {
        var (compilation, tree) = Compile(body);
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        errors.Should().BeEmpty("the positive binding oracle must compile without CS0121 ambiguity");

        var model = compilation.GetSemanticModel(tree);
        var invocation = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .First();

        return model.GetSymbolInfo(invocation, TestContext.Current.CancellationToken).Symbol
            .Should().BeAssignableTo<IMethodSymbol>()
            .Subject;
    }

    private static void ShouldBindToLegacy(IMethodSymbol method, int parameterCount, int typeParameterCount)
    {
        method.ContainingType.Name.Should().Be("EntityMetadataBuilder");
        method.OriginalDefinition.Parameters.Should().HaveCount(parameterCount);
        method.OriginalDefinition.TypeParameters.Should().HaveCount(typeParameterCount);
        method.OriginalDefinition.Parameters.Select(p => p.Type.ToString())
            .Should().Contain(t => t.Contains("object", StringComparison.Ordinal) == false,
                "a legacy overload has no object-typed key selector");
    }

    private static readonly (string Body, int Parameters, int TypeParameters)[] LegacyCases =
    [
        ("var b = new EntityMetadataBuilder<PlainParent>(); b.HasMany(p => p.Children, c => c.ParentId);", 2, 2),
        ("var b = new EntityMetadataBuilder<Blog>(); b.HasMany<Post, int>(p => p.ParentId, b => b.TenantId);", 2, 2),
        ("var b = new EntityMetadataBuilder<PlainChild>(); b.HasOne(c => c.Parent, c => c.ParentId);", 2, 2),
        ("var b = new EntityMetadataBuilder<PlainChild>(); b.HasOne<PlainParent, int>(c => c.ParentId, p => p.Id);", 2, 2),
        ("var b = new EntityMetadataBuilder<ProfileOwner>(); b.HasOneToOne<Profile, int>(o => o.Profile, o => o.Id, p => p.OwnerId);", 3, 2),
        ("var b = new EntityMetadataBuilder<Blog>(); b.HasManyThrough<Tag, BlogTag, int, int>(b => b.Tags, b => b.Id, l => l.BlogId, t => t.Id, l => l.TagId);", 5, 4),
        ("var b = new EntityMetadataBuilder<Blog>(); Expression<Func<Post, int>> fk = p => p.ParentId; Expression<Func<Blog, int>> pk = b2 => b2.TenantId; b.HasMany(fk, pk);", 2, 2),
        ("var b = new EntityMetadataBuilder<Blog>(); Expression<Func<Post, object>> fk = p => p.ParentId; Expression<Func<Blog, object>> pk = b2 => b2.TenantId; b.HasMany(fk, pk);", 2, 2),
        ("var b = new EntityMetadataBuilder<Blog>(); Expression<Func<Post, object?>> fk = p => p.ParentId; Expression<Func<Blog, object?>> pk = b2 => b2.TenantId; b.HasMany(fk, pk);", 2, 2),
        ("var b = new EntityMetadataBuilder<Blog>(); b.HasMany(b2 => b2.Posts, p => new { p.ParentTenantId, p.ParentId });", 2, 2),
        ("var b = new EntityMetadataBuilder<Post>(); b.HasOne(p => p.Parent, p => new { p.ParentTenantId, p.ParentId });", 2, 2),
    ];

    [Fact]
    public void LegacyCallShapes_ShouldCompileWithoutAmbiguity_AndBindToTheSixOldSignatures()
    {
        foreach (var (body, parameters, typeParameters) in LegacyCases)
            ShouldBindToLegacy(ResolveMethod(body), parameters, typeParameters);
    }

    [Fact]
    public void CompositeOverloads_ShouldCompileAndBindToTheirOwnSignatures()
    {
        var cases = new (string Body, int Parameters, int TypeParameters)[]
        {
            ("var b = new EntityMetadataBuilder<Blog>(); b.HasMany(b2 => b2.Posts, p => new { p.ParentTenantId, p.ParentId }, b2 => new { b2.TenantId, b2.Id });", 3, 1),
            ("var b = new EntityMetadataBuilder<Blog>(); b.HasMany<Post>(p => new { p.ParentTenantId, p.ParentId }, b2 => new { b2.TenantId, b2.Id });", 2, 1),
            ("var b = new EntityMetadataBuilder<Post>(); b.HasOne(p => p.Parent, p => new { p.ParentTenantId, p.ParentId }, b2 => new { b2.TenantId, b2.Id });", 3, 1),
            ("var b = new EntityMetadataBuilder<Post>(); b.HasOne<Blog>(p => new { p.ParentTenantId, p.ParentId }, b2 => new { b2.TenantId, b2.Id });", 2, 1),
            ("var b = new EntityMetadataBuilder<Blog>(); b.HasOneToOne(b2 => b2.Featured, b2 => new { b2.TenantId, b2.Id }, p => new { p.ParentTenantId, p.ParentId });", 3, 1),
            ("var b = new EntityMetadataBuilder<Blog>(); b.HasManyThrough<Tag, BlogTag>(b2 => b2.Tags, b2 => new { b2.TenantId, b2.Id }, l => new { l.BlogTenantId, l.BlogId }, t => new { t.TenantId, t.Id }, l => new { l.TagTenantId, l.TagId });", 5, 2),
            ("var b = new EntityMetadataBuilder<Blog>(); b.HasMany<Post>(p => new { p.ParentTenantId, p.ParentId });", 1, 1),
            ("var b = new EntityMetadataBuilder<Post>(); b.HasOne<Blog>(p => new { p.ParentTenantId, p.ParentId });", 1, 1),
            ("var b = new EntityMetadataBuilder<Blog>(); b.HasOneToOne(b2 => b2.Featured, p => new { p.ParentTenantId, p.ParentId });", 2, 2),
            ("var b = new EntityMetadataBuilder<Blog>(); b.HasManyThrough<Tag, BlogTag>(b2 => b2.Tags, l => new { l.BlogTenantId, l.BlogId }, l => new { l.TagTenantId, l.TagId });", 3, 2),
        };

        foreach (var (body, parameters, typeParameters) in cases)
        {
            var method = ResolveMethod(body);
            method.OriginalDefinition.Parameters.Should().HaveCount(parameters);
            method.OriginalDefinition.TypeParameters.Should().HaveCount(typeParameters);
        }
    }

    [Fact]
    public void DeliberatelyWrongInference_ShouldBeACompileNegative()
    {
        var (compilation, _) = Compile("var b = new EntityMetadataBuilder<Blog>(); b.HasMany<Post, int>(p => new { p.ParentTenantId, p.ParentId }, b2 => new { b2.TenantId, b2.Id });");

        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        errors.Should().NotBeEmpty("a deliberately wrong generic-argument count must not bind");
        var ids = errors.Select(e => e.Id).ToArray();
        ids.Any(id => id is "CS0305" or "CS0308" or "CS0411" or "CS1503" or "CS7036" or "CS1061" or "CS0029" or "CS1662")
            .Should().BeTrue("the rejection is a binding/inference diagnostic, got: {0}", string.Join(", ", ids));
    }

    [Fact]
    public void LegacyScalarShapes_ShouldProduceTheOldSingleComponentMetadata()
    {
        var oneToMany = new EntityMetadataBuilder<PlainParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build()
            .Relationships[0];
        var manyToOne = new EntityMetadataBuilder<PlainChild>()
            .HasOne(c => c.Parent, c => c.ParentId)
            .Build()
            .Relationships[0];
        var explicitOneToMany = new EntityMetadataBuilder<Blog>()
            .HasMany<Post, int>(p => p.ParentId, b => b.Id)
            .Build()
            .Relationships[0];

        oneToMany.Kind.Should().Be(RelationshipKind.OneToMany);
        oneToMany.ForeignKey.Should().ContainSingle().Which.PropertyInfo.Name.Should().Be(nameof(PlainChild.ParentId));
        oneToMany.PrincipalKey.Should().ContainSingle().Which.PropertyInfo.Name.Should().Be(nameof(PlainParent.Id));

        manyToOne.Kind.Should().Be(RelationshipKind.ManyToOne);
        manyToOne.ForeignKey.Should().ContainSingle().Which.PropertyInfo.Name.Should().Be(nameof(PlainChild.ParentId));
        manyToOne.PrincipalKey.Should().ContainSingle().Which.PropertyInfo.Name.Should().Be(nameof(PlainParent.Id));

        explicitOneToMany.ForeignKey.Should().ContainSingle().Which.PropertyInfo.Name.Should().Be(nameof(Post.ParentId));
        explicitOneToMany.PrincipalKey.Should().ContainSingle().Which.PropertyInfo.Name.Should().Be(nameof(Blog.Id));
    }

    [Fact]
    public void LegacyObjectSelectorBinding_ShouldPreserveTheDeclaredRelationship()
    {
        Expression<Func<Post, object>> objectForeignKey = p => p.ParentId;
        Expression<Func<Blog, object>> objectPrincipalKey = b => b.Id;

        var scalar = new EntityMetadataBuilder<Blog>()
            .HasMany(b => b.Posts, p => p.ParentId)
            .Build()
            .Relationships[0];
        var rebound = new EntityMetadataBuilder<Blog>()
            .HasMany(objectForeignKey, objectPrincipalKey)
            .Build()
            .Relationships[0];

        rebound.Kind.Should().Be(scalar.Kind);
        rebound.ForeignKey.Select(p => p.PropertyInfo.Name).Should().Equal(scalar.ForeignKey.Select(p => p.PropertyInfo.Name));
        rebound.PrincipalKey.Select(p => p.PropertyInfo.Name).Should().Equal(scalar.PrincipalKey.Select(p => p.PropertyInfo.Name));
    }

    [Fact]
    public void CompositeOverloads_ShouldProduceOrderedMetadata()
    {
        DataContextCache.Metadata[typeof(Blog)] = CompositeBlogMetadata();
        DataContextCache.Metadata[typeof(Tag)] = CompositeTagMetadata();

        var a = new EntityMetadataBuilder<Blog>()
            .HasMany(b => b.Posts, p => new { p.ParentTenantId, p.ParentId }, b => new { b.TenantId, b.Id })
            .Build()
            .Relationships[0];
        var bForm = new EntityMetadataBuilder<Blog>()
            .HasMany<Post>(p => new { p.ParentTenantId, p.ParentId }, b => new { b.TenantId, b.Id })
            .Build()
            .Relationships[0];
        var c = new EntityMetadataBuilder<Post>()
            .HasOne(p => p.Parent, p => new { p.ParentTenantId, p.ParentId }, b => new { b.TenantId, b.Id })
            .Build()
            .Relationships[0];
        var d = new EntityMetadataBuilder<Post>()
            .HasOne<Blog>(p => new { p.ParentTenantId, p.ParentId }, b => new { b.TenantId, b.Id })
            .Build()
            .Relationships[0];
        var e = new EntityMetadataBuilder<Blog>()
            .HasOneToOne(b => b.Featured, b => new { b.TenantId, b.Id }, p => new { p.ParentTenantId, p.ParentId })
            .Build()
            .Relationships[0];
        var f = new EntityMetadataBuilder<Blog>()
            .HasManyThrough<Tag, BlogTag>(
                b => b.Tags,
                b => new { b.TenantId, b.Id },
                l => new { l.BlogTenantId, l.BlogId },
                t => new { t.TenantId, t.Id },
                l => new { l.TagTenantId, l.TagId })
            .Build()
            .Relationships[0];
        var g = new EntityMetadataBuilder<Blog>()
            .HasMany<Post>(p => new { p.ParentTenantId, p.ParentId })
            .Build()
            .Relationships[0];
        var h = new EntityMetadataBuilder<Post>()
            .HasOne<Blog>(p => new { p.ParentTenantId, p.ParentId })
            .Build()
            .Relationships[0];
        var i = new EntityMetadataBuilder<Blog>()
            .HasOneToOne(b => b.Featured, p => new { p.ParentTenantId, p.ParentId })
            .Build()
            .Relationships[0];
        var j = new EntityMetadataBuilder<Blog>()
            .HasManyThrough<Tag, BlogTag>(
                b => b.Tags,
                l => new { l.BlogTenantId, l.BlogId },
                l => new { l.TagTenantId, l.TagId })
            .Build()
            .Relationships[0];

        foreach (var relationship in new[] { a, bForm, c, d, g, h })
        {
            relationship.ForeignKey.Select(p => p.PropertyInfo.Name)
                .Should().ContainInOrder(nameof(Post.ParentTenantId), nameof(Post.ParentId));
            relationship.PrincipalKey.Select(p => p.PropertyInfo.Name)
                .Should().ContainInOrder(nameof(Blog.TenantId), nameof(Blog.Id));
        }

        e.Kind.Should().Be(RelationshipKind.OneToOne);
        e.ForeignKey.Select(p => p.PropertyInfo.Name).Should().ContainInOrder(nameof(Post.ParentTenantId), nameof(Post.ParentId));
        e.PrincipalKey.Select(p => p.PropertyInfo.Name).Should().ContainInOrder(nameof(Blog.TenantId), nameof(Blog.Id));

        f.Kind.Should().Be(RelationshipKind.ManyToMany);
        f.Junction!.JunctionParentForeignKey.Select(p => p.PropertyInfo.Name)
            .Should().ContainInOrder(nameof(BlogTag.BlogTenantId), nameof(BlogTag.BlogId));
        f.Junction.JunctionChildForeignKey.Select(p => p.PropertyInfo.Name)
            .Should().ContainInOrder(nameof(BlogTag.TagTenantId), nameof(BlogTag.TagId));

        i.Kind.Should().Be(RelationshipKind.OneToOne);
        i.ForeignKey.Select(p => p.PropertyInfo.Name).Should().ContainInOrder(nameof(Post.ParentTenantId), nameof(Post.ParentId));
        i.PrincipalKey.Select(p => p.PropertyInfo.Name).Should().ContainInOrder(nameof(Blog.TenantId), nameof(Blog.Id));

        j.Kind.Should().Be(RelationshipKind.ManyToMany);
        j.Junction!.ParentKey.Select(p => p.PropertyInfo.Name).Should().ContainInOrder(nameof(Blog.TenantId), nameof(Blog.Id));
        j.Junction.ChildKey.Select(p => p.PropertyInfo.Name).Should().ContainInOrder(nameof(Tag.TenantId), nameof(Tag.Id));
    }

    private static IEntityMetadata CompositeBlogMetadata()
    {
        var builder = new EntityMetadataBuilder<Blog>();
        builder.Property(b => b.TenantId).Key();
        builder.Property(b => b.Id).Key();
        return builder.Build();
    }

    private static IEntityMetadata CompositeTagMetadata()
    {
        var builder = new EntityMetadataBuilder<Tag>();
        builder.Property(t => t.TenantId).Key();
        builder.Property(t => t.Id).Key();
        return builder.Build();
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedPlatformAssemblies)
        {
            foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator))
                paths.Add(path);
        }

        var baseDirectory = AppContext.BaseDirectory;
        paths.Add(Path.Combine(baseDirectory, "nextorm.core.dll"));
        foreach (var file in Directory.EnumerateFiles(baseDirectory, "Microsoft.Extensions.*.dll"))
            paths.Add(file);

        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (var path in paths.Where(File.Exists))
            references.Add(MetadataReference.CreateFromFile(path));

        return references.ToImmutable();
    }
}
