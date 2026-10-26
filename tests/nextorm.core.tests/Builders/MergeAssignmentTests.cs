using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T301 (#301) assignment-expression coverage on the public builder surface: initializer shapes,
/// destination validation, role binding, the key-upsert conflict-key rule and the in-memory
/// fail-closed behaviour. No database is involved — the SQL is rendered by a provider-free context and
/// the in-memory cases execute against <see cref="InMemoryDataContext"/>.
/// </summary>
public class MergeAssignmentTests
{
    [SqlTable("ma_item")]
    private sealed class Item
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }

        public int A { get; set; }

        public int B { get; set; }

        public int Total { get; set; }
    }

    [SqlTable("ma_generated")]
    private sealed class Generated
    {
        [Key]
        public int Id { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Serial { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int Doubled { get; set; }

        public string? Name { get; set; }
    }

    /// <summary>A provider-free renderer: <c>ToSql</c> renders text and never opens a connection.</summary>
    private sealed class RenderingContext(ISqlDialect dialect) : DataContext(new DataContextBuilder())
    {
        public override ISqlDialect Dialect => dialect;

        public override DbParameter CreateParam(string name, object? value)
            => throw new NotSupportedException("The render-only test context never mints a command parameter.");

        protected override DbConnection CreateDbConnection(string? connectionString)
            => throw new InvalidOperationException("The test context must not open a connection.");
    }

    /// <summary>A dialect with the general multi-branch <c>MERGE</c> and no native proposed row.</summary>
    private sealed class FullMergeDialect : SqlDialectBase
    {
        public static readonly FullMergeDialect Instance = new();

        public override bool SupportsMergeStatement => true;

        public override bool SupportsMergeConditionalBranches => true;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>A dialect whose key upsert is the native <c>ON CONFLICT</c> form (a proposed row exists).</summary>
    private sealed class NativeUpsertDialect : SqlDialectBase
    {
        public static readonly NativeUpsertDialect Instance = new();

        public override bool SupportsOnConflict => true;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private static IDataContext FullMerge() => new RenderingContext(FullMergeDialect.Instance);

    private static IDataContext NativeUpsert() => new RenderingContext(NativeUpsertDialect.Instance);

    private static Item Source() => new() { Id = 1, Name = "a" };

    // --- S1: initializer shapes (R01 parser side) ------------------------------------------------

    [Fact]
    public void AnonymousInitializer_UpdateBranch_ShouldCarryTheAssignedColumns()
    {
        using var ctx = FullMerge();

        var sql = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source) => new { source.Name, Total = source.A + source.B })
            .WhenNotMatched().ThenInsert()
            .ToSql();

        sql.Should().Contain("when matched then update set ");
        sql.Should().Contain("target.Name = source.Name", "the anonymous member binding is an assignment");
        sql.Should().Contain("target.Total = (source.A + source.B)",
            "the right-hand side expression stays an expression");
        sql.Should().NotContain("target.A = source.A", "an explicit set writes only its assigned destinations");
    }

    [Fact]
    public void TypedInitializer_UpdateBranch_ShouldCarryTheAssignedColumn()
    {
        using var ctx = FullMerge();

        var sql = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source) => new Item { Name = source.Name })
            .WhenNotMatched().ThenInsert()
            .ToSql();

        sql.Should().Contain("Name = source.Name");
        sql.Should().NotContain("A = source.A", "a typed initializer writes only the named members");
    }

    [Fact]
    public void BareMemberInsert_ShouldBeRejectedBeforeExecution()
    {
        using var ctx = FullMerge();
        var builder = ctx.CreateMergeBuilder<Item>().Using(Source()).On((t, s) => t.Id == s.Id);

        var act = () => builder.WhenNotMatched().ThenInsert(source => source.Name);

        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("assignments");
        exception.Message.Should().Contain("non-empty anonymous initializer");
    }

    [Fact]
    public void EmptyInitializer_ShouldBeRejectedBeforeExecution()
    {
        using var ctx = FullMerge();
        var builder = ctx.CreateMergeBuilder<Item>().Using(Source()).On((t, s) => t.Id == s.Id);

        var act = () => builder.WhenMatched().ThenUpdate((target, source) => new { });

        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("assignments");
        exception.Message.Should().Contain("non-empty anonymous initializer");
    }

    [Fact]
    public void NonInitializerBody_ShouldBeRejectedBeforeExecution()
    {
        using var ctx = FullMerge();
        var builder = ctx.CreateMergeBuilder<Item>().Using(Source()).On((t, s) => t.Id == s.Id);

        var act = () => builder.WhenMatched().ThenUpdate((target, source) => source.A + source.B);

        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("assignments");
        exception.Message.Should().Contain("non-empty anonymous initializer");
    }

    // --- S2: destination validation (R02) -------------------------------------------------------

    [Fact]
    public void UnknownDestination_ShouldNameThePropertyAndTheEntity()
    {
        using var ctx = FullMerge();
        var builder = ctx.CreateMergeBuilder<Item>().Using(Source()).On((t, s) => t.Id == s.Id);

        var act = () => builder.WhenMatched().ThenUpdate((target, source) => new { Nope = source.Name });

        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("assignments");
        exception.Message.Should().Contain("Nope");
        exception.Message.Should().Contain(nameof(Item));
    }

    [Fact]
    public void DuplicateDestination_ShouldNameTheProperty()
    {
        using var ctx = FullMerge();
        var builder = ctx.CreateMergeBuilder<Item>().Using(Source()).On((t, s) => t.Id == s.Id);

        var target = Expression.Parameter(typeof(Item), "target");
        var source = Expression.Parameter(typeof(Item), "source");
        var name = typeof(Item).GetProperty(nameof(Item.Name))!;
        var duplicate = Expression.MemberInit(
            Expression.New(typeof(Item)),
            Expression.Bind(name, Expression.Property(source, nameof(Item.Name))),
            Expression.Bind(name, Expression.Property(source, nameof(Item.Name))));
        var assignments = Expression.Lambda<Func<Item, Item, object?>>(duplicate, target, source);

        var act = () => builder.WhenMatched().ThenUpdate(assignments);

        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("assignments");
        exception.Message.Should().Contain(nameof(Item.Name));
        exception.Message.Should().Contain("more than once");
    }

    [Fact]
    public void IncompatibleValueType_ShouldNameThePropertyAndTheType()
    {
        using var ctx = FullMerge();
        var builder = ctx.CreateMergeBuilder<Item>().Using(Source()).On((t, s) => t.Id == s.Id);

        var act = () => builder.WhenMatched().ThenUpdate((target, source) => new { Total = source.Name });

        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("assignments");
        exception.Message.Should().Contain(nameof(Item.Total));
        exception.Message.Should().Contain(typeof(string).FullName!);
    }

    [Fact]
    public void IdentityDestination_OnUpdate_ShouldNameTheKindAndTheProperty()
    {
        using var ctx = FullMerge();
        var builder = ctx.CreateMergeBuilder<Generated>()
            .Using(new Generated { Id = 1, Name = "a" })
            .On((t, s) => t.Id == s.Id);

        var act = () => builder.WhenMatched().ThenUpdate((target, source) => new { Serial = source.Serial });

        var exception = act.Should().Throw<NotSupportedException>().Which;
        exception.Message.Should().Contain("identity");
        exception.Message.Should().Contain(nameof(Generated.Serial));
        exception.Message.Should().Contain("UPDATE");
    }

    [Fact]
    public void IdentityDestination_OnInsert_ShouldNameTheKindAndTheProperty()
    {
        using var ctx = FullMerge();
        var builder = ctx.CreateMergeBuilder<Generated>()
            .Using(new Generated { Id = 1, Name = "a" })
            .On((t, s) => t.Id == s.Id);

        var act = () => builder.WhenNotMatched().ThenInsert(source => new { Serial = source.Serial });

        var exception = act.Should().Throw<NotSupportedException>().Which;
        exception.Message.Should().Contain("identity");
        exception.Message.Should().Contain(nameof(Generated.Serial));
        exception.Message.Should().Contain("INSERT");
    }

    [Fact]
    public void ComputedDestination_OnUpdate_ShouldNameTheKindAndTheProperty()
    {
        using var ctx = FullMerge();
        var builder = ctx.CreateMergeBuilder<Generated>()
            .Using(new Generated { Id = 1, Name = "a" })
            .On((t, s) => t.Id == s.Id);

        var act = () => builder.WhenMatched().ThenUpdate((target, source) => new { Doubled = target.Id });

        var exception = act.Should().Throw<NotSupportedException>().Which;
        exception.Message.Should().Contain("computed");
        exception.Message.Should().Contain(nameof(Generated.Doubled));
        exception.Message.Should().Contain("UPDATE");
    }

    [Fact]
    public void ComputedDestination_OnInsert_ShouldNameTheKindAndTheProperty()
    {
        using var ctx = FullMerge();
        var builder = ctx.CreateMergeBuilder<Generated>()
            .Using(new Generated { Id = 1, Name = "a" })
            .On((t, s) => t.Id == s.Id);

        var act = () => builder.WhenNotMatched().ThenInsert(source => new { Doubled = source.Id });

        var exception = act.Should().Throw<NotSupportedException>().Which;
        exception.Message.Should().Contain("computed");
        exception.Message.Should().Contain(nameof(Generated.Doubled));
        exception.Message.Should().Contain("INSERT");
    }

    [Fact]
    public void MergeKeyDestination_OnUpdate_ShouldNameTheKindAndTheProperty()
    {
        using var ctx = FullMerge();
        var builder = ctx.CreateMergeBuilder<Item>().Using(Source()).On((t, s) => t.Id == s.Id);

        var act = () => builder.WhenMatched().ThenUpdate((target, source) => new { Id = source.Id });

        var exception = act.Should().Throw<NotSupportedException>().Which;
        exception.Message.Should().Contain("merge key");
        exception.Message.Should().Contain(nameof(Item.Id));
        exception.Message.Should().Contain("UPDATE");
    }

    // --- S3: role binding (R04/R06) -------------------------------------------------------------

    [Fact]
    public void ExcludedRole_OnFormWithoutAProposedRow_ShouldBeRefusedAtRender()
    {
        using var ctx = FullMerge();
        var merge = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source, excluded) => new { Total = excluded.Total })
            .WhenNotMatched().ThenInsert();

        var act = () => merge.ToSql();

        var exception = act.Should().Throw<NotSupportedException>().Which;
        exception.Message.Should().Contain("role");
        exception.Message.Should().Contain("excluded");
    }

    [Fact]
    public void UnusedThirdParameter_ShouldBeAcceptedAndAddNoRole()
    {
        using var ctx = FullMerge();

        var unused = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source, ignored) => new { Total = source.A })
            .WhenNotMatched().ThenInsert()
            .ToSql();

        unused.Should().Contain("Total = source.A");
        unused.Should().NotContain("excluded", "a declared but unread parameter adds no role");
    }

    [Fact]
    public void RoleBinding_ShouldFollowParameterIdentityNotName()
    {
        using var ctx = FullMerge();

        var renamed = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((left, right) => new { Total = right.A })
            .WhenNotMatched().ThenInsert()
            .ToSql();

        var unused = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source, ignored) => new { Total = source.A })
            .WhenNotMatched().ThenInsert()
            .ToSql();

        unused.Should().Be(renamed, "the role comes from the parameter position, never from its name");
    }

    // --- S4: key-upsert conflict-key binding (R06) ----------------------------------------------

    [Fact]
    public void KeyUpsertInsert_ConstantKey_ShouldBeRejected()
    {
        using var ctx = NativeUpsert();
        var merge = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = 1, Name = source.Name });

        var act = () => merge.ToSql();

        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("assignments");
        exception.Message.Should().Contain("conflict key 'Id'");
        exception.Message.Should().Contain("source.Id");
    }

    [Fact]
    public void KeyUpsertInsert_TransformedKey_ShouldBeRejected()
    {
        using var ctx = NativeUpsert();
        var merge = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = source.Id + 0, Name = source.Name });

        var act = () => merge.ToSql();

        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("assignments");
        exception.Message.Should().Contain("conflict key 'Id'");
    }

    [Fact]
    public void KeyUpsertInsert_ForeignPropertyKey_ShouldBeRejected()
    {
        using var ctx = NativeUpsert();
        var merge = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = source.A, Name = source.Name });

        var act = () => merge.ToSql();

        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("assignments");
        exception.Message.Should().Contain("conflict key 'Id'");
    }

    [Fact]
    public void KeyUpsertInsert_DirectSourceKey_ShouldBeAccepted()
    {
        using var ctx = NativeUpsert();

        var sql = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name })
            .ToSql();

        sql.Should().StartWith("insert into ma_item(Id, Name) values (");
        sql.Should().Contain("on conflict (Id) do update set ");
    }

    [Fact]
    public void FullMergeInsert_WithConstantKey_ShouldBeExemptFromTheKeyRule()
    {
        using var ctx = FullMerge();

        var sql = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert(source => new { Id = 1, Name = source.Name })
            .ToSql();

        sql.Should().Contain("then insert (Id, Name) values (");
        sql.Should().Contain("Name", "the full-MERGE insert form keeps its own assignments");
    }

    // --- S5: in-memory (R11) --------------------------------------------------------------------

    [Fact]
    public void InMemory_ExpressionUpdate_ShouldFailClosed()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Data[typeof(Item)] = new List<Item>();

        var act = () => ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Name = source.Name })
            .WhenNotMatchedInsert()
            .Merge();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*does not support MERGE assignment expressions*");
    }

    [Fact]
    public void InMemory_ExpressionInsert_ShouldFailClosed()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Data[typeof(Item)] = new List<Item>();

        var act = () => ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Name = source.Name })
            .Merge();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*does not support MERGE assignment expressions*");
    }

    [Fact]
    public void InMemory_NoArgKeyUpsert_ShouldStillUpsert()
    {
        using var ctx = new InMemoryDataContext();
        var rows = new List<Item> { new() { Id = 1, Name = "old", A = 1 } };
        ctx.Data[typeof(Item)] = rows;

        var updated = ctx.CreateMergeBuilder<Item>()
            .Using(new Item { Id = 1, Name = "new", A = 2 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        updated.Should().Be(1);
        rows.Should().ContainSingle();
        rows[0].Name.Should().Be("new");
        rows[0].A.Should().Be(2);

        var inserted = ctx.CreateMergeBuilder<Item>()
            .Using(new Item { Id = 2, Name = "b" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        inserted.Should().Be(1);
        rows.Should().HaveCount(2);
        rows.Single(r => r.Id == 2).Name.Should().Be("b");
    }
}
