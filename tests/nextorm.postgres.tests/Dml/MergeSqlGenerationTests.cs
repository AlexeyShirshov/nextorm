using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

[SqlTable("merge_entity")]
public interface IMergeEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    [Column("total")]
    int Total { get; set; }
}

public sealed class MergeEntity : IMergeEntity
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public int Total { get; set; }
}

[SqlTable("unkkey_entity")]
public interface IUnkeyedEntity
{
    [Column("name")]
    string? Name { get; set; }
}

public sealed class UnkeyedEntity : IUnkeyedEntity
{
    public string? Name { get; set; }
}

[SqlTable("keyonly_entity")]
public interface IKeyOnlyEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }
}

public sealed class KeyOnlyEntity : IKeyOnlyEntity
{
    public long Id { get; set; }
}

/// <summary>
/// SQL generation of the key-upsert builder on PostgreSQL (no database connection). PostgreSQL renders
/// <c>INSERT ... ON CONFLICT (&lt;keys&gt;) DO UPDATE SET ...</c> with <c>excluded.&lt;column&gt;</c>.
/// </summary>
public class MergeSqlGenerationTests
{
    [Fact]
    public void Merge_Entity_ShouldRenderOnConflictDoUpdate()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5, Total = 9 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into merge_entity (id, name, age) values (@p0, @p1, @p2) on conflict (id) do update set name = excluded.name, age = excluded.age");
    }

    [Fact]
    public void Merge_Batch_ShouldRenderMultipleRows()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using([
                new MergeEntity { Id = 1, Name = "a", Age = 1 },
                new MergeEntity { Id = 2, Name = "b", Age = 2 },
            ])
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into merge_entity (id, name, age) values (@p0, @p1, @p2), (@p3, @p4, @p5) on conflict (id) do update set name = excluded.name, age = excluded.age");
    }

    [Fact]
    public void Merge_QuotedIdentifiers_ShouldQuoteTableAndColumns()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into \"merge_entity\" (\"id\", \"name\", \"age\") values (@p0, @p1, @p2) on conflict (\"id\") do update set \"name\" = excluded.\"name\", \"age\" = excluded.\"age\"");
    }

    [Fact]
    public void OnKeys_WithoutKey_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateMergeBuilder<IUnkeyedEntity>()
            .Using(new UnkeyedEntity { Name = "a" })
            .OnKeys();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OnKeys_OnIdentityKey_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateMergeBuilder<IInsertEntity>()
            .Using(new InsertEntity { Name = "a", Age = 1 })
            .OnKeys();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Merge_OnKeyOnlyEntity_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateMergeBuilder<IKeyOnlyEntity>()
            .Using(new KeyOnlyEntity { Id = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Merge_WithoutUsing_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateMergeBuilder<IMergeEntity>()
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Merge_WithoutBranches_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a" })
            .OnKeys()
            .ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Using_Twice_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a" })
            .Using(new MergeEntity { Id = 2, Name = "b" });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FullMerge_AllBranches_ShouldRenderMerge()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_MatchedDelete_ShouldRenderDeleteBranch()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenDelete()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then delete when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_SelectedColumns_ShouldRenderSubset()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate((t, s) => new { Name = s.Name })
            .WhenNotMatched().ThenInsert(s => new { Id = s.Id, Name = s.Name })
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set name = source.name when not matched then insert (id, name) values (source.id, source.name)");
    }

    [Fact]
    public void FullMerge_NotMatchedBySource_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenNotMatchedBySource().ThenDelete()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void KeyUpsert_Returning_ShouldRenderReturning()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("insert into merge_entity (id, name, age) values (@p0, @p1, @p2) on conflict (id) do update set name = excluded.name, age = excluded.age returning id, name");
    }

    [Fact]
    public void FullMerge_ReturningProjection_ShouldRenderReturning()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age) returning target.id, target.name");
    }

    [Fact]
    public void FullMerge_QuerySource_ShouldRenderUsingSelect()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(ctx.From<IMergeEntity>())
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (select id, name, age, total\n from merge_entity) as source on target.id = source.id when matched then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_MatchedDoNothing_ShouldRenderDoNothing()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenDoNothing()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then do nothing when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_NotMatchedDoNothing_ShouldRenderDoNothing()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenDoNothing()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set name = source.name, age = source.age when not matched then do nothing");
    }

    [Fact]
    public void FullMerge_OnCondition_ShouldRenderOnSearchCondition()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .On((t, s) => t.Id == s.Id && s.Age > 0)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on (target.id = source.id and (source.age > 0)) when matched then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_MatchedCondition_ShouldRenderAndCondition()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched((t, s) => t.Name != s.Name).ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched and target.name != source.name then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_NotMatchedCondition_ShouldRenderAndCondition()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched((t, s) => s.Age > 0).ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set name = source.name, age = source.age when not matched and (source.age > 0) then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_QuerySourceWithParameter_ShouldRenderSourceParameterOnce()
    {
        using var ctx = PostgresTestContext.Create();
        var min = 5;

        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(ctx.From<IMergeEntity>().Where(x => x.Id > min))
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (select id, name, age, total\n from merge_entity\n where (id > cast(@min as bigint))) as source on target.id = source.id when matched then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void Merge_QuerySource_OnIdentityKey_ShouldRender()
    {
        using var ctx = PostgresTestContext.Create();

        // A query source projects the identity column, so an explicit On(...) over it is valid: the
        // guard only rejects a column the VALUES-derived source does not declare.
        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(ctx.From<IMergeEntity>())
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (select id, name, age, total\n from merge_entity) as source on target.id = source.id when matched then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void Merge_ValuesSource_WhenMatchedGeneratedColumn_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        // Total is computed and excluded from the VALUES-derived source, so a branch condition that
        // reaches it through the source has no matching derived column.
        var act = () => ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched((t, s) => s.Total > 0).ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*is not a column of the VALUES-derived merge source*");
    }

    [Fact]
    public void Merge_QuerySource_WhenMatchedGeneratedColumn_ShouldRender()
    {
        using var ctx = PostgresTestContext.Create();

        // The same generated column is a projected column of the query source, so on that path the
        // condition renders instead of being rejected.
        ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(ctx.From<IMergeEntity>())
            .OnKeys()
            .WhenMatched((t, s) => s.Total > 0).ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (select id, name, age, total\n from merge_entity) as source on target.id = source.id when matched and (source.total > 0) then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    // The MERGE assignment facts below render a real right-hand-side expression over the source row: the
    // destination is a target column, the operands read the source relation (or, on a native key upsert,
    // the target table and the proposed excluded row), and a constant becomes a bound parameter.
    // Every PostgreSQL fact uses the quoted context, because the destination/value role split is asserted
    // on exact identifiers.

    private static ExprEntity Source() => new() { Id = 1, A = 2, B = 3, Total = 4 };

    [Fact]
    public void FullMerge_ExpressionUpdate_ShouldWriteTheUnqualifiedTargetColumnFromTheSource()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        var sql = ctx.CreateMergeBuilder<IExprEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatched().ThenUpdate((t, s) => new { Total = s.A + s.B })
            .ToSql();

        sql.Should().Be("merge into \"expr_entity\" as target using (values (@p0, @p1, @p2, @p3)) as source (\"Id\", \"A\", \"B\", \"Total\") on target.\"Id\" = source.\"Id\" when matched then update set \"Total\" = (source.\"A\" + source.\"B\")");

        // The written column is the target column, but PostgreSQL leaves the SET destination unqualified:
        // only the value names the source relation.
        SetList(sql).Should().Be("\"Total\" = (source.\"A\" + source.\"B\")");
        SetList(sql).Should().NotStartWith("target.").And.NotStartWith("t.");
    }

    [Fact]
    public void FullMerge_ExpressionInsert_ShouldRenderColumnsAndValuesFromTheSource()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        ctx.CreateMergeBuilder<IExprEntity>()
            .Using(Source())
            .OnKeys()
            .WhenNotMatched().ThenInsert(s => new { Id = s.Id, Total = s.A + s.B })
            .ToSql()
            .Should().Be("merge into \"expr_entity\" as target using (values (@p0, @p1, @p2, @p3)) as source (\"Id\", \"A\", \"B\", \"Total\") on target.\"Id\" = source.\"Id\" when not matched then insert (\"Id\", \"Total\") values (source.\"Id\", (source.\"A\" + source.\"B\"))");
    }

    [Fact]
    public void FullMerge_ExcludedRoleAssignment_ShouldFailClosedBeforeExecution()
    {
        using var ctx = PostgresTestContext.CreateQuoted();
        var merge = ctx.CreateMergeBuilder<IExprEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatched().ThenUpdate((t, s, e) => new { Total = e.Total });

        var act = () => merge.ToSql();

        // A general MERGE has no proposed row, so the role fails closed with the provider and role named.
        act.Should().Throw<NotSupportedException>()
            .WithMessage("Provider 'PostgresDialect' does not support role 'excluded' in MERGE assignments.");
    }

    [Fact]
    public void FullMerge_ConstantInsideExpression_ShouldBindOneParameter()
    {
        using var ctx = PostgresTestContext.CreateQuoted();
        var merge = ctx.CreateMergeBuilder<IExprEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatched().ThenUpdate((t, s) => new { Total = s.A + 1 });

        var (sql, parameters) = Render(ctx, merge);

        sql.Should().Be("merge into \"expr_entity\" as target using (values (@p1, @p2, @p3, @p4)) as source (\"Id\", \"A\", \"B\", \"Total\") on target.\"Id\" = source.\"Id\" when matched then update set \"Total\" = (source.\"A\" + @p0)");
        sql.Should().NotContain("+ 1", "an assignment constant is a bound parameter, never an inlined literal");

        var token = SetList(sql).Split([' ', '(', ')'], StringSplitOptions.RemoveEmptyEntries)[^1];
        token.Should().StartWith("@");
        parameters.Should().Contain(p => token.EndsWith(p.Name, StringComparison.Ordinal) && Equals(p.Value, 1));
    }

    [Fact]
    public void KeyUpsert_ExpressionUpdate_ShouldReadTheTargetTableAndTheExcludedRow()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        ctx.CreateMergeBuilder<IExprEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((t, s, e) => new { Total = t.Total + e.A })
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into \"expr_entity\" (\"Id\", \"A\", \"B\", \"Total\") values (@p0, @p1, @p2, @p3) on conflict (\"Id\") do update set \"Total\" = (\"expr_entity\".\"Total\" + excluded.\"A\")");
    }

    [Fact]
    public void KeyUpsert_SourceRoleExpression_ShouldBindTheInputRowAsAParameter()
    {
        using var ctx = PostgresTestContext.CreateQuoted();
        var merge = ctx.CreateMergeBuilder<IExprEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { B = s.A })
            .WhenNotMatchedInsert();

        var (sql, parameters) = Render(ctx, merge);

        sql.Should().Be("insert into \"expr_entity\" (\"Id\", \"A\", \"B\", \"Total\") values (@p1, @p2, @p3, @p4) on conflict (\"Id\") do update set \"B\" = @p0");
        SetList(sql).Should().Be("\"B\" = @p0");
        SetList(sql).Should().NotContain("values(", "the original input row is bound as a statement parameter, never a VALUES(col) reference");
        parameters.Should().Contain(p => p.Name == "p0" && Equals(p.Value, 2));
    }

    /// <summary>
    /// The SET fragment of a rendered statement: everything after <c>update set </c> up to the next branch
    /// keyword or an optional <c>returning</c> clause.
    /// </summary>
    /// <param name="sql">The rendered statement.</param>
    /// <returns>The SET list alone.</returns>
    private static string SetList(string sql)
    {
        const string marker = "update set ";
        var start = sql.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = sql.IndexOf(" when ", start, StringComparison.Ordinal);
        if (end < 0)
            end = sql.IndexOf(" returning ", start, StringComparison.Ordinal);

        return end < 0 ? sql[start..] : sql[start..end];
    }

    /// <summary>
    /// The SQL and the parameter list the executor would bind. <c>ToSql()</c> exposes the text only, and the
    /// bound value of a constant assignment is the decisive fact, so the
    /// test reads the same pair the mutation terminal builds: the internal command seam with no returning
    /// columns (the plain statement) and the private builder every mutation shares.
    /// </summary>
    /// <param name="ctx">The rendering context.</param>
    /// <param name="merge">The merge builder under test.</param>
    /// <returns>The rendered SQL text and its parameters.</returns>
    private static (string Sql, IReadOnlyList<Parameter> Parameters) Render(IDataContext ctx, MergeBuilder<IExprEntity> merge)
    {
        var command = merge.BuildReturningCommand([]);
        var method = typeof(DataContext).GetMethod("BuildMutationSql", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return ((string, List<Parameter>))method.Invoke(ctx, [command])!;
    }
}

/// <summary>A destination with two operands, so an assignment value is a real expression over the source row.</summary>
[SqlTable("expr_entity")]
public interface IExprEntity
{
    [Key]
    [Column("Id")]
    long Id { get; set; }

    [Column("A")]
    int A { get; set; }

    [Column("B")]
    int B { get; set; }

    [Column("Total")]
    int Total { get; set; }
}

/// <summary>The concrete row type of <see cref="IExprEntity"/>.</summary>
public sealed class ExprEntity : IExprEntity
{
    public long Id { get; set; }

    public int A { get; set; }

    public int B { get; set; }

    public int Total { get; set; }
}
