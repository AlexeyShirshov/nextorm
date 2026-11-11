using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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

    /// <summary>A destination with a nullable value column (the typed-null RHS case).</summary>
    [SqlTable("ma_nullable")]
    private sealed class NullableItem
    {
        [Key]
        public int Id { get; set; }

        public int? Total { get; set; }

        public string? Name { get; set; }
    }

    /// <summary>A destination whose value is a server-clock column (the server-function case).</summary>
    [SqlTable("ma_stamped")]
    private sealed class Stamped
    {
        [Key]
        public int Id { get; set; }

        public DateTime Created { get; set; }
    }

    /// <summary>A destination the target-filter facts scope by tenant.</summary>
    [SqlTable("ma_scoped")]
    private sealed class Scoped
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }

        public int TenantId { get; set; }

        public int A { get; set; }

        public int Total { get; set; }
    }

    /// <summary>A provider-free renderer: <c>ToSql</c> renders text and never opens a connection.</summary>
    private sealed class RenderingContext(ISqlDialect dialect) : DataContext(new DataContextBuilder())
    {
        /// <summary>Connections the context tried to allocate; a fail-closed guard keeps this at zero.</summary>
        public int ConnectionAttempts { get; private set; }

        public override ISqlDialect Dialect => dialect;

        public override DbParameter CreateParam(string name, object? value)
            => throw new NotSupportedException("The render-only test context never mints a command parameter.");

        protected override DbConnection CreateDbConnection(string? connectionString)
        {
            ConnectionAttempts++;
            throw new InvalidOperationException("The test context must not open a connection.");
        }
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

    /// <summary>A dialect with the general multi-branch <c>MERGE</c> and a <c>RETURNING</c> terminal.</summary>
    private sealed class ReturningMergeDialect : SqlDialectBase
    {
        public static readonly ReturningMergeDialect Instance = new();

        public override bool SupportsMergeStatement => true;

        public override bool SupportsMergeConditionalBranches => true;

        public override bool SupportsReturning => true;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private static IDataContext FullMerge() => new RenderingContext(FullMergeDialect.Instance);

    private static IDataContext NativeUpsert() => new RenderingContext(NativeUpsertDialect.Instance);

    private const string TenantKey = "ma_tenant";

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

    // --- S6: parameterisation (R03) --------------------------------------------------------------

    [Fact]
    public void ConstantValue_ShouldBeBoundAsAParameter()
    {
        using var ctx = new CapturingContext(FullMergeDialect.Instance);

        var affected = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source) => new { Total = target.Total + 1 })
            .WhenNotMatched().ThenInsert()
            .Merge();

        affected.Should().Be(0);
        var command = ctx.Commands.Should().ContainSingle().Which;
        command.CommandText.Should().NotMatchRegex(@"\+\s*1\b", "a constant value is bound as a parameter, never inlined");
        var parameter = Parameter(command, ParameterTokenAfter(command.CommandText, "target.Total = "));
        parameter.Value.Should().Be(1);
        parameter.Value.Should().BeOfType<int>("the parameter keeps the destination value's CLR type");
    }

    [Fact]
    public void ServerFunction_ShouldNotBeFoldedIntoAClientConstant()
    {
        using var ctx = new CapturingContext(FullMergeDialect.Instance);

        ctx.CreateMergeBuilder<Stamped>()
            .Using(new Stamped { Id = 1 })
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source) => new { Created = DateTime.UtcNow })
            .WhenNotMatched().ThenInsert()
            .Merge();

        var command = ctx.Commands.Should().ContainSingle().Which;
        var value = Between(command.CommandText, "target.Created = ", " when not matched");
        value.Should().Be("target.Created = now() at time zone 'utc'",
            "a parameter-free server function stays server-side instead of becoming a captured timestamp");
    }

    // --- S7: null (R08) --------------------------------------------------------------------------

    [Fact]
    public void NullableValueTypeNull_ShouldBeANullParameter()
    {
        using var ctx = new CapturingContext(FullMergeDialect.Instance);

        ctx.CreateMergeBuilder<NullableItem>()
            .Using(new NullableItem { Id = 1, Name = "a" })
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source) => new { Total = (int?)null })
            .WhenNotMatched().ThenInsert()
            .Merge();

        var command = ctx.Commands.Should().ContainSingle().Which;
        command.CommandText.Should().NotMatchRegex(@"(?i)\bnull\b",
            "a typed null is a parameter, never the SQL NULL literal or an interpolated value");
        Parameter(command, ParameterTokenAfter(command.CommandText, "target.Total = ")).Value.Should().BeNull();
    }

    [Fact]
    public void TypedNullReference_ShouldBeANullParameter()
    {
        using var ctx = new CapturingContext(FullMergeDialect.Instance);

        ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source) => new { Name = (string?)null })
            .WhenNotMatched().ThenInsert()
            .Merge();

        var command = ctx.Commands.Should().ContainSingle().Which;
        command.CommandText.Should().NotMatchRegex(@"(?i)\bnull\b", "a typed null reference is parameterised");
        command.CommandText.Should().NotContain("''", "no interpolated empty literal stands in for the null");
        Parameter(command, ParameterTokenAfter(command.CommandText, "target.Name = ")).Value.Should().BeNull();
    }

    // --- S8: statement-wide allocator + reuse (R09) ----------------------------------------------

    [Fact]
    public void CapturedClosure_ShouldBeReevaluatedOnEveryBuild()
    {
        var delta = 5;
        using var ctx = new CapturingContext(FullMergeDialect.Instance);
        var builder = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source) => new { Total = source.A + delta })
            .WhenNotMatched().ThenInsert();

        builder.Merge().Should().Be(0);
        var first = ctx.Commands.Should().ContainSingle().Which;
        first.CommandText.Should().NotMatchRegex(@"\+\s*5\b", "the captured closure value is bound, never inlined");
        Parameter(first, ParameterTokenAfter(first.CommandText, "target.Total = ")).Value.Should().Be(5);

        delta = 7;
        builder.Merge().Should().Be(0);
        ctx.Commands.Should().HaveCount(2, "a second build renders a second statement");
        Parameter(ctx.Commands[1], ParameterTokenAfter(ctx.Commands[1].CommandText, "target.Total = "))
            .Value.Should().Be(7, "the builder retains no closure value from the previous statement");
    }

    [Fact]
    public void StatementWideAllocator_ShouldGiveEveryOccurrenceItsOwnParameter()
    {
        using var ctx = new CapturingContext(FullMergeDialect.Instance);
        ctx.Properties[TenantKey] = 1;
        ctx.From<Scoped>(b => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        ctx.CreateMergeBuilder<Scoped>()
            .Using(new Scoped { Id = 1, Name = "a", TenantId = 1, A = 1, Total = 2 })
            .On((t, s) => t.Id == s.Id && s.A > 4)
            .WhenMatched().ThenUpdate((target, source) => new { Total = target.Total + 5 })
            .WhenNotMatched().ThenInsert()
            .Merge();

        var command = ctx.Commands.Should().ContainSingle().Which;
        var names = command.Parameters.Cast<DbParameter>().Select(p => p.ParameterName).ToArray();

        names.Should().NotBeEmpty();
        names.Should().OnlyHaveUniqueItems("one statement-wide allocator hands out a distinct name per occurrence");
        foreach (var name in names)
        {
            Regex.Matches(command.CommandText, "@" + Regex.Escape(name) + @"\b").Count
                .Should().Be(1, $"parameter '{name}' must back exactly one occurrence");
        }

        command.CommandText.Should().Contain("target.TenantId = @", "the filter constant is a parameter");
        command.CommandText.Should().Contain("source.A > 4",
            "a predicate literal keeps the engine's existing inline rendering (QueryPlanner.RenderMergeCondition, not the assignment path)");
        command.CommandText.Should().NotMatchRegex(@"\+\s*5\b", "the assignment constant is a parameter");
    }

    [Fact]
    public void ExpressionStatement_ShouldNotMutateAReusableCommandCacheFlag()
    {
        using var ctx = new RenderingContext(FullMergeDialect.Instance);
        var shared = ctx.From<Item>().Select(x => x.Id);
        shared.Cache.Should().BeTrue("the reusable command starts cache-enabled");

        ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source) => new { Total = source.A })
            .WhenNotMatched().ThenInsert(source => new { Id = source.Id, Name = source.Name })
            .ToSql()
            .Should().Contain("when matched then update set ");

        shared.Cache.Should().BeTrue("building an expression-assignment statement must not clear the sticky plan-cache flag");
    }

    // --- S9: filters (R10) -----------------------------------------------------------------------

    [Fact]
    public void ActiveFilter_ExpressionInsert_ShouldRefuseBeforeExecution()
    {
        using var ctx = new RenderingContext(FullMergeDialect.Instance);
        ctx.Properties[TenantKey] = 1;
        ctx.From<Scoped>(b => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        var act = () => ctx.CreateMergeBuilder<Scoped>()
            .Using(new Scoped { Id = 1, Name = "a", TenantId = 1 })
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert(source => new { Id = source.Id, Name = source.Name })
            .Merge();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*MERGE expression INSERT with active target filters is not supported*IgnoreFilters()*");
        ctx.ConnectionAttempts.Should().Be(0, "the refusal is a metadata decision taken before any command is executed");
    }

    [Fact]
    public void ActiveFilter_IgnoredExpressionInsert_ShouldBuild()
    {
        using var ctx = new RenderingContext(FullMergeDialect.Instance);
        ctx.Properties[TenantKey] = 1;
        ctx.From<Scoped>(b => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        var sql = ctx.CreateMergeBuilder<Scoped>()
            .Using(new Scoped { Id = 1, Name = "a", TenantId = 1 })
            .IgnoreFilters()
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert(source => new { Id = source.Id, Name = source.Name })
            .ToSql();

        sql.Should().StartWith("merge into", "IgnoreFilters() is the explicit opt-out");
        sql.Should().Contain("when not matched then insert (Id, Name) values (source.Id, source.Name)");
    }

    [Fact]
    public void ActiveFilter_NoArgInsert_ShouldKeepTheFilteredStatement()
    {
        using var ctx = new RenderingContext(FullMergeDialect.Instance);
        ctx.Properties[TenantKey] = 1;
        ctx.From<Scoped>(b => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        var sql = ctx.CreateMergeBuilder<Scoped>()
            .Using(new Scoped { Id = 1, Name = "a", TenantId = 1 })
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        sql.Should().StartWith("merge into");
        sql.Should().Contain("target.TenantId = @", "the no-arg form keeps the target filter in its ON");
        sql.Should().Contain("when not matched then insert (");
    }

    // --- S10: mixed forms + Returning (R12) -----------------------------------------------------

    [Fact]
    public void MixedForms_ShouldRenderBothArms()
    {
        using var ctx = new RenderingContext(FullMergeDialect.Instance);

        var noArg = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        var mixed = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert(source => new { Id = source.Id, Name = source.Name })
            .ToSql();

        Between(mixed, "when matched then update set ", " when not matched")
            .Should().Be(Between(noArg, "when matched then update set ", " when not matched"),
                "the no-arg arm keeps its previous SQL shape");
        mixed.Should().Contain("when not matched then insert (Id, Name) values (source.Id, source.Name)");
    }

    [Fact]
    public void Returning_ShouldKeepItsColumnsAndInjectNoAssignmentColumn()
    {
        using var ctx = new RenderingContext(ReturningMergeDialect.Instance);

        var entity = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source) => new { Total = source.A })
            .WhenNotMatched().ThenInsert()
            .Returning()
            .ToSql();

        Tail(entity, " returning ").Should().StartWith("Id, Name, A, B, Total",
            "Returning() keeps the entity's own column list in metadata order");

        var projected = ctx.CreateMergeBuilder<Item>()
            .Using(Source())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((target, source) => new { Total = source.A })
            .WhenNotMatched().ThenInsert()
            .Returning(x => new { x.Id })
            .ToSql();

        var tail = Tail(projected, " returning ");
        tail.Should().StartWith("Id");
        tail.Should().NotContain("Total", "an assignment destination is never injected into the returning list");
    }

    /// <summary>Extracts the first placeholder token after <paramref name="marker"/> in rendered SQL.</summary>
    private static string ParameterTokenAfter(string sql, string marker)
    {
        var match = Regex.Match(sql, Regex.Escape(marker) + @"[^,]*?(@\w+)");
        match.Success.Should().BeTrue($"the SQL should carry a parameter after '{marker}'");
        return match.Groups[1].Value;
    }

    /// <summary>Resolves a rendered placeholder (<c>@p3</c>) to the parameter the command binds.</summary>
    private static DbParameter Parameter(RecordingCommand command, string token)
        => command.Parameters.Cast<DbParameter>().Single(p => p.ParameterName == token.TrimStart('@'));

    /// <summary>The SQL slice between two markers.</summary>
    private static string Between(string sql, string start, string end)
    {
        var from = sql.IndexOf(start, StringComparison.Ordinal);
        from.Should().BeGreaterThanOrEqualTo(0, $"the SQL should contain '{start}'");
        var to = sql.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        to.Should().BeGreaterThan(from, $"the SQL should contain '{end}' after '{start}'");
        return sql[from..to];
    }

    /// <summary>The SQL tail after a marker.</summary>
    private static string Tail(string sql, string marker)
    {
        var index = sql.IndexOf(marker, StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, $"the SQL should contain '{marker}'");
        return sql[(index + marker.Length)..];
    }

    /// <summary>
    /// A database-shaped context that records the mutation command instead of executing it: the
    /// recording connection mints the command the executor binds, and the terminal reports zero
    /// affected rows. It exists so a fact can assert the parameter list a statement would execute with.
    /// </summary>
    private sealed class CapturingContext(ISqlDialect dialect) : DataContext(new DataContextBuilder())
    {
        private readonly List<RecordingCommand> _commands = [];

        public override ISqlDialect Dialect => dialect;

        public IReadOnlyList<RecordingCommand> Commands => _commands;

        public override DbParameter CreateParam(string name, object? value)
            => new RecordingParameter(name) { Value = value };

        protected override DbConnection CreateDbConnection(string? connectionString) => new RecordingConnection(this);

        internal void Record(RecordingCommand command) => _commands.Add(command);
    }

    /// <summary>A connection that never opens: it only hands out the recording command.</summary>
    private sealed class RecordingConnection(CapturingContext owner) : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => "nextorm";

        public override string DataSource => "recording";

        public override string ServerVersion => "1.0";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Close() => _state = ConnectionState.Closed;

        public override void Open() => _state = ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
        {
            var command = new RecordingCommand();
            owner.Record(command);
            return command;
        }
    }

    /// <summary>The command the context binds: records the SQL and the parameters, executes nothing.</summary>
    private sealed class RecordingCommand : DbCommand
    {
        private readonly RecordingParameterCollection _parameters = new();

        [AllowNull]
        protected override DbConnection DbConnection { get; set; } = null!;

        protected override DbParameterCollection DbParameterCollection => _parameters;

        [AllowNull]
        protected override DbTransaction DbTransaction { get; set; } = null!;

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => 0;

        public override object? ExecuteScalar() => null;

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => new RecordingParameter(string.Empty);

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            => throw new NotSupportedException();
    }

    /// <summary>A parameter that keeps the value exactly as bound (a CLR null stays null).</summary>
    private sealed class RecordingParameter(string name) : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; }

        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = name;

        public override int Size { get; set; }

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        public override bool SourceColumnNullMapping { get; set; }

        public override object? Value { get; set; }

        public override void ResetDbType()
        {
        }
    }

    private sealed class RecordingParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;

        public override object SyncRoot => this;

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value!);
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains((DbParameter)value);

        public override bool Contains(string? value) => _items.Any(p => p.ParameterName == value);

        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_items).CopyTo(array, index);

        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

        public override int IndexOf(string? parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string? parameterName) => _items.RemoveAll(p => p.ParameterName == parameterName);

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string? parameterName) => _items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string? parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
                Add(value);
            else
                _items[index] = value;
        }
    }
}
