using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T251 declaration-axis tests: the shared key-selector parser (accepted forms, aliases, boxing and
/// nullable wrappers, and every rejection family) and the <see cref="RelationshipAttribute"/> key arrays
/// (arity, explicit principal, legacy single column, malformed and conflicting declarations, fluent
/// precedence). No database is involved.
/// </summary>
public class T251CompositeDeclarationTests
{
    public T251CompositeDeclarationTests()
    {
        DataContextCache.Clear();
    }

    public sealed class Widget
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int ParentId { get; set; }
        public int? NullableId { get; set; }
        public Guid OwnerId { get; set; }
        public string Code { get; set; } = "";
        public int[] Codes { get; set; } = [];
        public int Field;
        public Widget? Parent { get; set; }
        public ICollection<Widget> Children { get; set; } = new List<Widget>();
        public WidgetHolder Holder { get; set; } = new();
    }

    public sealed class WidgetHolder
    {
        public Widget? Child { get; set; }
    }

    public sealed class WidgetDto
    {
        public WidgetDto()
        {
        }

        public WidgetDto(int id)
        {
            Id = id;
        }

        public int Id { get; set; }
    }

    private static IReadOnlyList<PropertyInfo> Parse(LambdaExpression? selector, string selectorName = "foreignKey")
        => RelationshipKeySelectorParser.Parse(selector, typeof(Widget), selectorName, "Widget.Parent");

    [Fact]
    public void Parser_ShouldAcceptScalarPropertyInSelectorOrder()
    {
        Expression<Func<Widget, int>> selector = w => w.ParentId;

        var components = Parse(selector);

        components.Should().ContainSingle();
        components[0].Name.Should().Be(nameof(Widget.ParentId));
    }

    [Fact]
    public void Parser_ShouldAcceptFlatAnonymousObject_PreservingOrderAndIgnoringAliases()
    {
        Expression<Func<Widget, object>> selector = w => new { Owner = w.ParentId, w.TenantId, w.Id };

        var components = Parse(selector);

        components.Select(p => p.Name).Should().ContainInOrder(
            nameof(Widget.ParentId), nameof(Widget.TenantId), nameof(Widget.Id));
    }

    [Fact]
    public void Parser_ShouldPreserveArityWithoutAnArtificialCap()
    {
        Expression<Func<Widget, object>> selector = w => new
        {
            w.Id,
            w.TenantId,
            w.ParentId,
            w.NullableId,
            w.OwnerId,
            w.Code,
            w.Codes,
            w.Holder,
        };

        var components = Parse(selector);

        components.Should().HaveCount(8);
        components.Select(p => p.Name).Should().ContainInOrder(
            nameof(Widget.Id),
            nameof(Widget.TenantId),
            nameof(Widget.ParentId),
            nameof(Widget.NullableId),
            nameof(Widget.OwnerId),
            nameof(Widget.Code),
            nameof(Widget.Codes),
            nameof(Widget.Holder));
    }

    [Fact]
    public void Parser_ShouldAcceptBoxingAndNullableWrappers()
    {
        Expression<Func<Widget, object>> selector = w => new { w.NullableId, w.Id };

        var components = Parse(selector);

        components.Select(p => p.Name).Should().ContainInOrder(nameof(Widget.NullableId), nameof(Widget.Id));
    }

    [Fact]
    public void Parser_ShouldRejectNullExpressionWithArgumentNullException()
    {
        Action act = () => Parse(null);

        act.Should().Throw<ArgumentNullException>().WithParameterName("foreignKey");
    }

    [Fact]
    public void Parser_ShouldRejectFieldAccess()
    {
        Expression<Func<Widget, object>> selector = w => w.Field;

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>().WithMessage("*Widget.Parent*field access*");
    }

    [Fact]
    public void Parser_ShouldRejectNestedMemberPath_AndNameTheComponentAndCount()
    {
        Expression<Func<Widget, object>> selector = w => new { w.Id, Nested = w.Parent!.ParentId };

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Widget.Parent*component 2 of 2*nested member path*");
    }

    [Fact]
    public void Parser_ShouldRejectComputation()
    {
        Expression<Func<Widget, object>> selector = w => w.Id + 1;

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>().WithMessage("*computation*");
    }

    [Fact]
    public void Parser_ShouldRejectMethodCall()
    {
        Expression<Func<Widget, object>> selector = w => w.Code.Trim();

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>().WithMessage("*method call*");
    }

    [Fact]
    public void Parser_ShouldRejectIndexAccess()
    {
        Expression<Func<Widget, object>> selector = w => w.Codes[0];

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>().WithMessage("*index access*");
    }

    [Fact]
    public void Parser_ShouldRejectArrayInitializer()
    {
        Expression<Func<Widget, object>> selector = w => new[] { w.Id };

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>().WithMessage("*array initializer*");
    }

    [Fact]
    public void Parser_ShouldRejectTupleLiteral()
    {
        var parameter = Expression.Parameter(typeof(Widget), "w");
        var constructor = typeof(ValueTuple<int, int>).GetConstructor([typeof(int), typeof(int)])!;
        var body = Expression.New(constructor, Expression.Property(parameter, nameof(Widget.Id)), Expression.Property(parameter, nameof(Widget.TenantId)));
        var selector = Expression.Lambda(body, parameter);

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>().WithMessage("*tuple literal*");
    }

    [Fact]
    public void Parser_ShouldRejectDtoConstruction()
    {
        Expression<Func<Widget, object>> selector = w => new WidgetDto(w.Id);

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>().WithMessage("*object construction*");
    }

    [Fact]
    public void Parser_ShouldRejectObjectInitializer()
    {
        Expression<Func<Widget, object>> selector = w => new WidgetDto { Id = w.Id };

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>().WithMessage("*object initializer*");
    }

    [Fact]
    public void Parser_ShouldRejectNestedAnonymousObject_AndNameTheComponentAndCount()
    {
        Expression<Func<Widget, object>> selector = w => new { w.Id, Inner = new { w.TenantId } };

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*component 2 of 2*nested anonymous object*");
    }

    [Fact]
    public void Parser_ShouldRejectEmptyAnonymousObject()
    {
        var emptyAnonymousType = EmitEmptyAnonymousType();
        var selector = Expression.Lambda(Expression.New(emptyAnonymousType), Expression.Parameter(typeof(Widget), "w"));

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>().WithMessage("*empty anonymous object*");
    }

    [Fact]
    public void Parser_ShouldRejectDuplicateMember()
    {
        var anonymousType = new { First = 0, Second = 0 }.GetType();
        var constructor = anonymousType.GetConstructors().Single();
        var parameter = Expression.Parameter(typeof(Widget), "w");
        var id = Expression.Property(parameter, nameof(Widget.Id));
        var selector = Expression.Lambda(Expression.New(constructor, id, id), parameter);

        Action act = () => Parse(selector);

        act.Should().Throw<NotSupportedException>().WithMessage("*component 2 of 2*repeats*Id*");
    }

    // ---- attribute matrix -------------------------------------------------

    public sealed class AttrBlog
    {
        public int TenantId { get; set; }
        public int Id { get; set; }

        [Relationship(ForeignKeys = new[] { nameof(AttrPost.TenantId), nameof(AttrPost.ParentId) })]
        public ICollection<AttrPost> Posts { get; set; } = new List<AttrPost>();
    }

    public sealed class AttrPost
    {
        public int TenantId { get; set; }
        public int ParentId { get; set; }
        public int Id { get; set; }

        [Relationship(
            ForeignKeys = new[] { nameof(AttrPost.TenantId), nameof(AttrPost.ParentId) },
            PrincipalKeys = new[] { nameof(AttrBlog.TenantId), nameof(AttrBlog.Id) })]
        public AttrBlog? Parent { get; set; }
    }

    public sealed class LegacyAttrBlog
    {
        public int Id { get; set; }

        [Relationship(ForeignKey = nameof(LegacyAttrPost.ParentId))]
        public ICollection<LegacyAttrPost> Posts { get; set; } = new List<LegacyAttrPost>();
    }

    public sealed class LegacyAttrPost
    {
        public int ParentId { get; set; }
    }

    public sealed class ExplicitPrincipalBlog
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public string Code { get; set; } = "";

        [Relationship(
            ForeignKeys = new[] { nameof(ExplicitPrincipalPost.ParentCode) },
            PrincipalKeys = new[] { nameof(ExplicitPrincipalBlog.Code) })]
        public ICollection<ExplicitPrincipalPost> Posts { get; set; } = new List<ExplicitPrincipalPost>();
    }

    public sealed class ExplicitPrincipalPost
    {
        public string ParentCode { get; set; } = "";
    }

    public sealed class ConflictingBlog
    {
        public int Id { get; set; }

        [Relationship(
            ForeignKey = nameof(ConflictingPost.ParentId),
            ForeignKeys = new[] { nameof(ConflictingPost.ParentId) })]
        public ICollection<ConflictingPost> Posts { get; set; } = new List<ConflictingPost>();
    }

    public sealed class ConflictingPost
    {
        public int ParentId { get; set; }
        public int OtherParentId { get; set; }
    }

    public sealed class EmptyArrayBlog
    {
        public int Id { get; set; }

        [Relationship(ForeignKeys = new string[0])]
        public ICollection<ConflictingPost> Posts { get; set; } = new List<ConflictingPost>();
    }

    public sealed class DuplicateNameBlog
    {
        public int Id { get; set; }

        [Relationship(ForeignKeys = new[] { nameof(ConflictingPost.ParentId), nameof(ConflictingPost.ParentId) })]
        public ICollection<ConflictingPost> Posts { get; set; } = new List<ConflictingPost>();
    }

    public sealed class MissingMemberBlog
    {
        public int Id { get; set; }

        [Relationship(ForeignKeys = new[] { "DoesNotExist" })]
        public ICollection<ConflictingPost> Posts { get; set; } = new List<ConflictingPost>();
    }

    public sealed class ArityMismatchBlog
    {
        public int TenantId { get; set; }
        public int Id { get; set; }

        [Relationship(
            ForeignKeys = new[] { nameof(ConflictingPost.ParentId), nameof(ConflictingPost.OtherParentId) },
            PrincipalKeys = new[] { nameof(ArityMismatchBlog.Id) })]
        public ICollection<ConflictingPost> Posts { get; set; } = new List<ConflictingPost>();
    }

    public sealed class FluentWinsBlog
    {
        public int Id { get; set; }

        [Relationship(ForeignKeys = new[] { "DoesNotExist" })]
        public ICollection<ConflictingPost> Posts { get; set; } = new List<ConflictingPost>();
    }

    public sealed class NoForeignKeyFormBlog
    {
        public int Id { get; set; }

        [Relationship]
        public ICollection<ConflictingPost> Posts { get; set; } = new List<ConflictingPost>();
    }

    private static IReadOnlyList<string> KeyNames(IReadOnlyList<IPropertyMetadata> key)
        => key.Select(p => p.PropertyInfo.Name).ToArray();

    [Fact]
    public void Attribute_ShouldDeclareOrderedForeignKeys_AndDefaultToTheFullPrincipalKey()
    {
        var blogBuilder = new EntityMetadataBuilder<AttrBlog>();
        blogBuilder.Property(b => b.TenantId).Key();
        blogBuilder.Property(b => b.Id).Key();
        var blog = blogBuilder.Build();
        DataContextCache.Metadata[typeof(AttrBlog)] = blog;
        DataContextCache.Metadata[typeof(AttrPost)] = new EntityMetadataBuilder<AttrPost>().Build();

        var relationship = blog.Relationships.Should().ContainSingle().Subject;

        KeyNames(relationship.ForeignKey).Should().ContainInOrder(nameof(AttrPost.TenantId), nameof(AttrPost.ParentId));
        KeyNames(relationship.PrincipalKey).Should().ContainInOrder(nameof(AttrBlog.TenantId), nameof(AttrBlog.Id));
    }

    [Fact]
    public void Attribute_ShouldDeclareExplicitPrincipalKeys_OnAReferenceNavigation()
    {
        var post = new EntityMetadataBuilder<AttrPost>().Build();
        DataContextCache.Metadata[typeof(AttrPost)] = post;
        DataContextCache.Metadata[typeof(AttrBlog)] = new EntityMetadataBuilder<AttrBlog>().Build();

        var relationship = post.Relationships.Should().ContainSingle().Subject;

        relationship.Kind.Should().Be(RelationshipKind.ManyToOne);
        KeyNames(relationship.ForeignKey).Should().ContainInOrder(nameof(AttrPost.TenantId), nameof(AttrPost.ParentId));
        KeyNames(relationship.PrincipalKey).Should().ContainInOrder(nameof(AttrBlog.TenantId), nameof(AttrBlog.Id));
    }

    [Fact]
    public void Attribute_ShouldAcceptAnExplicitNonKeyPrincipalComponent()
    {
        var blog = new EntityMetadataBuilder<ExplicitPrincipalBlog>().Build();
        DataContextCache.Metadata[typeof(ExplicitPrincipalBlog)] = blog;

        var relationship = blog.Relationships.Should().ContainSingle().Subject;

        KeyNames(relationship.ForeignKey).Should().ContainSingle().Which.Should().Be(nameof(ExplicitPrincipalPost.ParentCode));
        KeyNames(relationship.PrincipalKey).Should().ContainSingle().Which.Should().Be(nameof(ExplicitPrincipalBlog.Code));
        relationship.PrincipalKey[0].IsKey.Should().BeFalse("an explicit principal key may select a mapped non-key property");
    }

    [Fact]
    public void Attribute_LegacySingleForeignKey_ShouldStillDeclareTheRelationship()
    {
        var blog = new EntityMetadataBuilder<LegacyAttrBlog>().Build();

        var relationship = blog.Relationships.Should().ContainSingle().Subject;

        KeyNames(relationship.ForeignKey).Should().ContainSingle().Which.Should().Be(nameof(LegacyAttrPost.ParentId));
        KeyNames(relationship.PrincipalKey).Should().ContainSingle().Which.Should().Be(nameof(LegacyAttrBlog.Id));
    }

    [Fact]
    public void Attribute_SettingBothForeignKeyForms_ShouldBeRejected()
    {
        Action act = () => new EntityMetadataBuilder<ConflictingBlog>().Build();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*ConflictingBlog.Posts*sets both ForeignKey and ForeignKeys*");
    }

    [Fact]
    public void Attribute_WithNoForeignKeyForm_ShouldBeRejected()
    {
        Action act = () => new EntityMetadataBuilder<NoForeignKeyFormBlog>().Build();

        act.Should().Throw<NotSupportedException>().WithMessage("*must name the foreign-key property*");
    }

    [Fact]
    public void Attribute_WithEmptyForeignKeysArray_ShouldBeRejected()
    {
        Action act = () => new EntityMetadataBuilder<EmptyArrayBlog>().Build();

        act.Should().Throw<NotSupportedException>().WithMessage("*EmptyArrayBlog.Posts*empty ForeignKeys array*");
    }

    [Fact]
    public void Attribute_WithDuplicateForeignKeyNames_ShouldBeRejected()
    {
        Action act = () => new EntityMetadataBuilder<DuplicateNameBlog>().Build();

        act.Should().Throw<NotSupportedException>().WithMessage("*repeats the foreign key*ParentId*");
    }

    [Fact]
    public void Attribute_WithMissingForeignKeyMember_ShouldBeRejected()
    {
        Action act = () => new EntityMetadataBuilder<MissingMemberBlog>().Build();

        act.Should().Throw<NotSupportedException>().WithMessage("*foreign key 'DoesNotExist' at position 1 of 1*");
    }

    [Fact]
    public void Attribute_WithMismatchedKeyArity_ShouldBeRejectedAtBuild()
    {
        Action act = () => new EntityMetadataBuilder<ArityMismatchBlog>().Build();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*ArityMismatchBlog.Posts*2 foreign-key component(s) but 1 principal-key component(s)*");
    }

    [Fact]
    public void FluentDeclaration_ShouldWinWithoutValidatingTheShadowedAttribute()
    {
        var metadata = new EntityMetadataBuilder<FluentWinsBlog>()
            .HasMany(p => p.Posts, c => c.ParentId)
            .Build();
        DataContextCache.Metadata[typeof(FluentWinsBlog)] = metadata;

        var relationship = metadata.Relationships.Should().ContainSingle().Subject;
        KeyNames(relationship.ForeignKey).Should().ContainSingle().Which.Should().Be(nameof(ConflictingPost.ParentId));
    }

    private static Type EmitEmptyAnonymousType()
    {
        var assemblyName = new AssemblyName("T251EmptyAnonymous" + Guid.NewGuid().ToString("N"));
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName.Name!);
        var type = module.DefineType("<>f__AnonymousType99", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
        type.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes)!,
            []));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        return type.CreateType();
    }
}
