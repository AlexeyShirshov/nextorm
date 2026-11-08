using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 3, batch 1 — <c>[Relationship]</c> attribute declarations on a live SQLite database
/// (<c>src/nextorm.core/DataContext/Meta/Entities/EntityMetadataBuilder.cs</c>: <c>ValidateAttributeForeignKeyForm</c>,
/// <c>ResolveAttributeMembers</c>/<c>ResolveAttributeMember</c>, <c>ValidateAttributeArity</c>,
/// <c>TryGetCollectionElementType</c>/<c>TryGetGenericEnumerableElement</c>). At v4 every one of those
/// methods is at <c>0/N</c>, so both accepted forms and every guard are exercised here.
///
/// <para>
/// The accepted-path facts seed class-owned tables and assert the computed join result; every refusal is a
/// pre-SQL guard (<c>AC-STEP-REAL-PROVIDER</c> class b) asserted by exception type plus the semantic
/// identifier the guard carries (the navigation name and the offending member/form), never a bare
/// <c>Throw&lt;NotSupportedException&gt;()</c>.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep3AttributeRelationshipTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s3_attr_parent (id integer primary key, name text null);
        delete from s3_attr_parent;
        create table if not exists s3_attr_child (id integer primary key, parent_id int not null, label text null);
        delete from s3_attr_child;
        create table if not exists s3_attr_enum_parent (id integer primary key, name text null);
        delete from s3_attr_enum_parent;
        create table if not exists s3_attr_enum_child (id integer primary key, parent_id int not null, label text null);
        delete from s3_attr_enum_child;

        insert into s3_attr_parent (id, name) values (7001, 'p1'), (7002, 'p2'), (7003, 'p3');
        insert into s3_attr_child (id, parent_id, label) values
            (7101, 7001, 'a'), (7102, 7001, 'b'), (7103, 7002, 'c');
        insert into s3_attr_enum_parent (id, name) values (7201, 'e1'), (7202, 'e2');
        insert into s3_attr_enum_child (id, parent_id, label) values
            (7301, 7201, 'x'), (7302, 7201, 'y'), (7303, 7202, 'z');
        """;

    public SqliteStep3AttributeRelationshipTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();

        // Register the dependent types first so the principal-side collection provision resolves them.
        Context.From<S3AttrChild>();
        Context.From<S3AttrParent>();
        Context.From<S3AttrEnumChild>();
        Context.From<S3AttrEnumParent>();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>
    /// Accepted path: a collection navigation declared by <c>[Relationship(ForeignKey = ...)]</c> is
    /// excluded from the column map and feeds a real <c>JoinInto</c> that returns the computed children.
    /// </summary>
    [Fact]
    public void AttributeCollectionNavigation_JoinInto_ShouldFillChildrenFromTheLiveTable()
    {
        var parents = Context.From<S3AttrParent>()
            .OrderBy(p => p.Id)
            .JoinInto(Context.From<S3AttrChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Should().HaveCount(3);
        parents.Select(p => p.Name).Should().Equal("p1", "p2", "p3");
        parents[0].Children.Select(c => c.Label).Should().BeEquivalentTo(["a", "b"]);
        parents[1].Children.Select(c => c.Label).Should().Equal("c");
        parents[2].Children.Should().BeEmpty("parent 7003 owns no child");
    }

    /// <summary>
    /// Accepted path for the generic-enumerable element arm: an <c>IEnumerable&lt;TChild&gt;</c> navigation
    /// (not <c>ICollection&lt;TChild&gt;</c>) still resolves its element type, so the parent type maps and
    /// materializes its own columns from the live table (the navigation is excluded from the column map).
    /// </summary>
    [Fact]
    public void AttributeEnumerableNavigation_ShouldMapAndMaterializeTheParentColumns()
    {
        var parents = Context.From<S3AttrEnumParent>()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Name })
            .ToList();

        parents.Should().HaveCount(2);
        parents.Select(p => p.Name).Should().Equal("e1", "e2");
    }

    [Fact]
    public void BothForeignKeyForms_ShouldBeRefusedByName()
    {
        FluentActions.Invoking(() => Context.From<S3AttrBothForms>().ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*S3AttrBothForms.Children*sets both ForeignKey and ForeignKeys*");
    }

    [Fact]
    public void MissingForeignKeyForm_ShouldBeRefusedByName()
    {
        FluentActions.Invoking(() => Context.From<S3AttrNoForeignKey>().ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*S3AttrNoForeignKey.Children*must name the foreign-key property*");
    }

    [Fact]
    public void EmptyForeignKeysArray_ShouldBeRefusedByName()
    {
        FluentActions.Invoking(() => Context.From<S3AttrEmptyForeignKeys>().ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*S3AttrEmptyForeignKeys.Children*empty ForeignKeys array*");
    }

    [Fact]
    public void EmptyPrincipalKeysArray_ShouldBeRefusedByName()
    {
        FluentActions.Invoking(() => Context.From<S3AttrEmptyPrincipalKeys>().ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*S3AttrEmptyPrincipalKeys.Children*empty PrincipalKeys array*");
    }

    [Fact]
    public void EmptyForeignKeyName_ShouldBeRefusedWithItsPosition()
    {
        FluentActions.Invoking(() => Context.From<S3AttrEmptyMemberName>().ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*S3AttrEmptyMemberName.Children*empty foreign key name at position 1 of 2*");
    }

    [Fact]
    public void UnknownForeignKeyMember_ShouldBeRefusedWithTheMemberName()
    {
        FluentActions.Invoking(() => Context.From<S3AttrUnknownMember>().ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*S3AttrUnknownMember.Children*foreign key 'Missing'*not found on S3AttrUnknownChild*");
    }

    [Fact]
    public void DuplicateForeignKeyMember_ShouldBeRefusedWithItsPosition()
    {
        FluentActions.Invoking(() => Context.From<S3AttrDuplicateMember>().ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*S3AttrDuplicateMember.Children*repeats the foreign key 'ParentId' at position 2 of 2*");
    }

    [Fact]
    public void PrincipalKeyArityMismatch_ShouldBeRefusedWithBothComponentCounts()
    {
        FluentActions.Invoking(() => Context.From<S3AttrArityMismatch>().ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*S3AttrArityMismatch.Children*declares 2 foreign-key component(s) but 1 principal-key component(s)*");
    }

    [Fact]
    public void RelationshipOnStringProperty_ShouldBeRefusedAsNonNavigation()
    {
        FluentActions.Invoking(() => Context.From<S3AttrStringNavigation>().ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*S3AttrStringNavigation.Name*must be placed on a collection or a reference-typed navigation*");
    }

    [Fact]
    public void RelationshipOnValueTypeProperty_ShouldBeRefusedAsNonNavigation()
    {
        FluentActions.Invoking(() => Context.From<S3AttrValueTypeNavigation>().ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*S3AttrValueTypeNavigation.Count*must be placed on a collection or a reference-typed navigation*");
    }

    /// <summary>Accepted collection form mapped to the class-owned parent/child pair.</summary>
    [SqlTable("s3_attr_parent")]
    public sealed class S3AttrParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Relationship(ForeignKey = nameof(S3AttrChild.ParentId))]
        public ICollection<S3AttrChild> Children { get; } = new List<S3AttrChild>();
    }

    /// <summary>Dependent of the accepted collection form.</summary>
    [SqlTable("s3_attr_child")]
    public sealed class S3AttrChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("label")]
        public string? Label { get; set; }
    }

    /// <summary>Accepted generic-enumerable form (element arm of <c>TryGetCollectionElementType</c>).</summary>
    [SqlTable("s3_attr_enum_parent")]
    public sealed class S3AttrEnumParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Relationship(ForeignKey = nameof(S3AttrEnumChild.ParentId))]
        public IEnumerable<S3AttrEnumChild> Children { get; } = new List<S3AttrEnumChild>();
    }

    /// <summary>Dependent of the accepted generic-enumerable form.</summary>
    [SqlTable("s3_attr_enum_child")]
    public sealed class S3AttrEnumChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("label")]
        public string? Label { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // Refused declarations. Each type carries exactly one invalid attribute shape; the mapping guard
    // rejects it before any SQL. The tables are never created because the guard runs first.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Both the legacy and the array foreign-key form are set.</summary>
    public sealed class S3AttrBothForms
    {
        [Key]
        public int Id { get; set; }

        [Relationship(ForeignKey = nameof(S3AttrGuardChild.ParentId), ForeignKeys = new[] { nameof(S3AttrGuardChild.ParentId) })]
        public ICollection<S3AttrGuardChild> Children { get; } = new List<S3AttrGuardChild>();
    }

    /// <summary>Neither foreign-key form is set.</summary>
    public sealed class S3AttrNoForeignKey
    {
        [Key]
        public int Id { get; set; }

        [Relationship]
        public ICollection<S3AttrGuardChild> Children { get; } = new List<S3AttrGuardChild>();
    }

    /// <summary>An empty <c>ForeignKeys</c> array.</summary>
    public sealed class S3AttrEmptyForeignKeys
    {
        [Key]
        public int Id { get; set; }

        [Relationship(ForeignKeys = new string[0])]
        public ICollection<S3AttrGuardChild> Children { get; } = new List<S3AttrGuardChild>();
    }

    /// <summary>An empty <c>PrincipalKeys</c> array over a valid foreign key.</summary>
    public sealed class S3AttrEmptyPrincipalKeys
    {
        [Key]
        public int Id { get; set; }

        [Relationship(ForeignKey = nameof(S3AttrGuardChild.ParentId), PrincipalKeys = new string[0])]
        public ICollection<S3AttrGuardChild> Children { get; } = new List<S3AttrGuardChild>();
    }

    /// <summary>An empty member name in the second role position.</summary>
    public sealed class S3AttrEmptyMemberName
    {
        [Key]
        public int Id { get; set; }

        [Relationship(ForeignKeys = new[] { "", nameof(S3AttrGuardChild.ParentId) })]
        public ICollection<S3AttrGuardChild> Children { get; } = new List<S3AttrGuardChild>();
    }

    /// <summary>A member name that does not exist on the dependent type.</summary>
    public sealed class S3AttrUnknownMember
    {
        [Key]
        public int Id { get; set; }

        [Relationship(ForeignKeys = new[] { "Missing" })]
        public ICollection<S3AttrUnknownChild> Children { get; } = new List<S3AttrUnknownChild>();
    }

    /// <summary>The same member named twice.</summary>
    public sealed class S3AttrDuplicateMember
    {
        [Key]
        public int Id { get; set; }

        [Relationship(ForeignKeys = new[] { nameof(S3AttrTwoKeyChild.ParentId), nameof(S3AttrTwoKeyChild.ParentId) })]
        public ICollection<S3AttrTwoKeyChild> Children { get; } = new List<S3AttrTwoKeyChild>();
    }

    /// <summary>Two foreign-key components against one principal-key component.</summary>
    public sealed class S3AttrArityMismatch
    {
        [Key]
        public int Id { get; set; }

        [Relationship(
            ForeignKeys = new[] { nameof(S3AttrTwoKeyChild.ParentId), nameof(S3AttrTwoKeyChild.OtherId) },
            PrincipalKeys = new[] { nameof(Id) })]
        public ICollection<S3AttrTwoKeyChild> Children { get; } = new List<S3AttrTwoKeyChild>();
    }

    /// <summary>A <c>[Relationship]</c> on a scalar string property.</summary>
    public sealed class S3AttrStringNavigation
    {
        [Key]
        public int Id { get; set; }

        [Relationship(ForeignKey = "Whatever")]
        public string? Name { get; set; }
    }

    /// <summary>A <c>[Relationship]</c> on a value-type property.</summary>
    public sealed class S3AttrValueTypeNavigation
    {
        [Key]
        public int Id { get; set; }

        [Relationship(ForeignKey = "Whatever")]
        public int Count { get; set; }
    }

    /// <summary>Minimal dependent used by the form guards.</summary>
    public sealed class S3AttrGuardChild
    {
        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    /// <summary>Dependent for the unknown-member guard.</summary>
    public sealed class S3AttrUnknownChild
    {
        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    /// <summary>Dependent with two foreign-key candidates for the duplicate/arity guards.</summary>
    public sealed class S3AttrTwoKeyChild
    {
        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }

        public int OtherId { get; set; }
    }
}
