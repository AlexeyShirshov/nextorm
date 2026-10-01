using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation of the identity (whole-<c>Projection</c>) multi-table RETURNING form on PostgreSQL:
/// every returnable mapped property of every item slot is emitted under a deterministic per-slot alias
/// (<c>__sN_column</c>), a self-join of one type keeps its slots distinct, and the outer CTE read
/// addresses the stored aliases rather than a name-only lookup. No database is involved.
/// </summary>
public class JoinReturningIdentitySqlGenerationTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void UpdateJoinIdentityReturning_ShouldEmitUniquePerSlotAliases(int arity)
    {
        using var ctx = PostgresTestContext.Create();

        var sql = UpdateSql(ctx, arity);

        sql.Should().Contain("returning ");
        for (var slot = 1; slot <= arity; slot++)
        {
            sql.Should().Contain($"__s{slot}_id");
            sql.Should().Contain($"__s{slot}_name");
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void DeleteJoinIdentityReturning_ShouldEmitUniquePerSlotAliases(int arity)
    {
        using var ctx = PostgresTestContext.Create();

        var sql = DeleteSql(ctx, arity);

        sql.Should().Contain("returning ");
        for (var slot = 1; slot <= arity; slot++)
        {
            sql.Should().Contain($"__s{slot}_id");
            sql.Should().Contain($"__s{slot}_name");
        }
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(8, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(8, false)]
    public void UpdateJoinIdentity_ReturningShouldEqualLambdaForm(int arity, bool update)
    {
        using var ctx = PostgresTestContext.Create();

        var parameterless = update ? UpdateSql(ctx, arity, identityLambda: false) : DeleteSql(ctx, arity, identityLambda: false);
        var lambda = update ? UpdateSql(ctx, arity, identityLambda: true) : DeleteSql(ctx, arity, identityLambda: true);

        lambda.Should().Be(parameterless);
    }

    [Fact]
    public void UpdateJoinIdentityReturning_SelfJoinSameType_ShouldKeepSlotsDistinct()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = UpdateSql(ctx, 2);

        sql.Should().Contain("returning t1.id as \"__s1_id\"");
        sql.Should().Contain("t2.id as \"__s2_id\"");
        sql.Should().NotContain("t1.id as \"__s2_id\"");
        sql.Should().NotContain("t2.id as \"__s1_id\"");
    }

    [Fact]
    public void DeleteJoinIdentityReturning_SelfJoinSameType_ShouldKeepSlotsDistinct()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = DeleteSql(ctx, 2);

        sql.Should().Contain("returning t1.id as \"__s1_id\"");
        sql.Should().Contain("t2.id as \"__s2_id\"");
    }

    [Fact]
    public void UpdateJoinIdentity_RepeatedAndAliasShapedPhysicalColumns_ShouldDisambiguateAliases()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.From<AliasCollisionEntity>()
            .Join(ctx.From<AliasCollisionEntity>(), (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Name, "x")
            .Returning()
            .ToSql();

        // Metadata maps two properties onto column "id" and one onto the literal alias-shaped column
        // "id_2": the whole-output allocator must keep every emitted slot-1 alias unique. The expected
        // aliases are computed from the same metadata order and the same allocator the renderer uses, so
        // the disambiguation is asserted end-to-end (parse shape and emitted RETURNING agree).
        var properties = new EntityMetadataBuilder<AliasCollisionEntity>().Build().Properties;
        var pairs = new (int Slot, string Column)[properties.Count];
        for (var i = 0; i < properties.Count; i++)
            pairs[i] = (0, properties[i].ColumnName);

        var expected = ProjectionAliasCache.AllocateIdentityAliases(pairs);

        var emitted = Regex.Matches(sql, "as\\s+\"(__s1_[A-Za-z0-9_]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        emitted.Should().Equal(expected);
        emitted.Should().OnlyHaveUniqueItems();
        // "id_2" starts on the same base the repeated "id" is forced onto, so it must itself be bumped.
        emitted.Should().Contain("__s1_id_2_2");
    }

    [Fact]
    public void UpdateJoinIdentity_MutationCteRead_ShouldReferenceStoredAliases()
    {
        using var ctx = PostgresTestContext.Create();

        var scope = ctx.With("upd", Join2(ctx).UpdateJoin().Set(p => p.Item1.Name, "x").Returning());
        var sql = SqlOf(ctx, scope.From("upd").Select(r => new { A = r.Item1.Id, B = r.Item2.Id }));

        sql.Should().Contain("update merge_entity as \"t1\"");
        sql.Should().Contain("returning t1.id as \"__s1_id\"");
        sql.Should().Contain("select \"__s1_id\", \"__s2_id\" from upd as \"t1\"");
    }

    [Fact]
    public void DeleteJoinIdentity_MutationCteRead_ShouldReferenceStoredAliases()
    {
        using var ctx = PostgresTestContext.Create();

        var scope = ctx.With("del", Join2(ctx).CreateDeleteJoinBuilder().Returning());
        var sql = SqlOf(ctx, scope.From("del").Select(r => new { A = r.Item1.Id, B = r.Item2.Id }));

        sql.Should().Contain("delete from merge_entity as \"t1\"");
        sql.Should().Contain("returning t1.id as \"__s1_id\"");
        sql.Should().Contain("select \"__s1_id\", \"__s2_id\" from del as \"t1\"");
    }

    [Fact]
    public void UpdateJoinIdentity_MutationCteRead_ShouldFilterOnStoredSlotAlias()
    {
        using var ctx = PostgresTestContext.Create();

        var scope = ctx.With("upd", Join2(ctx).UpdateJoin().Set(p => p.Item1.Name, "x").Returning());
        var sql = SqlOf(ctx, scope.From("upd").Where(r => r.Item1.Id == 1).Select(r => new { A = r.Item1.Id, B = r.Item2.Id }));

        sql.Should().Contain("where t1.\"__s1_id\" = 1");
    }

    [Fact]
    public void UpdateJoinIdentity_DerivedFullShapeJoinedSide_ShouldGenerateConsistentAliases()
    {
        using var ctx = PostgresTestContext.Create();

        var derived = ctx.From<IdentityDerivedEntity>().Where(x => x.Id > 0)
            .Select(x => new IdentityDerivedEntity { Id = x.Id, Name = x.Name });

        var direct = ctx.From<IdentityDerivedEntity>()
            .Join(derived, (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Name, "x")
            .Returning()
            .ToSql();

        direct.Should().Contain("returning t1.id as \"__s1_id\"");
        direct.Should().Contain("t2.id as \"__s2_id\"");

        var scope = ctx.With("upd", ctx.From<IdentityDerivedEntity>()
            .Join(derived, (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Name, "x")
            .Returning());
        var read = SqlOf(ctx, scope.From("upd").Select(r => new { A = r.Item1.Id, B = r.Item2.Id }));

        read.Should().Contain("select \"__s1_id\", \"__s2_id\" from upd as \"t1\"");
    }

    [Fact]
    public void DeleteJoinIdentity_DerivedFullShapeJoinedSide_ShouldGenerateConsistentAliases()
    {
        using var ctx = PostgresTestContext.Create();

        var derived = ctx.From<IdentityDerivedEntity>().Where(x => x.Id > 0)
            .Select(x => new IdentityDerivedEntity { Id = x.Id, Name = x.Name });

        var direct = ctx.From<IdentityDerivedEntity>()
            .Join(derived, (a, b) => a.Id == b.Id)
            .CreateDeleteJoinBuilder()
            .Returning()
            .ToSql();

        direct.Should().Contain("returning t1.id as \"__s1_id\"");
        direct.Should().Contain("t2.id as \"__s2_id\"");
    }

    [Fact]
    public void UpdateJoinIdentity_MetadataLessDerivedFullShape_ShouldGenerateConsistentAliases()
    {
        using var ctx = PostgresTestContext.Create();

        var derived = ctx.From<IdentityDerivedEntity>().Where(x => x.Id > 0)
            .Select(x => new IdentityDerivedShape { Id = x.Id, Name = x.Name });

        var direct = ctx.From<IdentityDerivedEntity>()
            .Join(derived, (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Name, "x")
            .Returning()
            .ToSql();

        direct.Should().Contain("__s1_id");
        direct.Should().Contain("__s1_name");
        direct.Should().Contain("__s2_Id");
        direct.Should().Contain("__s2_Name");

        var scope = ctx.With("upd", ctx.From<IdentityDerivedEntity>()
            .Join(derived, (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Name, "x")
            .Returning());
        var read = SqlOf(ctx, scope.From("upd").Select(r => r));

        read.Should().Contain("select \"__s1_id\", \"__s1_name\", \"__s2_Id\", \"__s2_Name\" from upd as \"t1\"");
    }

    [Fact]
    public void DeleteJoinIdentity_MetadataLessDerivedFullShape_ShouldGenerateConsistentAliases()
    {
        using var ctx = PostgresTestContext.Create();

        var derived = ctx.From<IdentityDerivedEntity>().Where(x => x.Id > 0)
            .Select(x => new IdentityDerivedShape { Id = x.Id, Name = x.Name });

        var direct = ctx.From<IdentityDerivedEntity>()
            .Join(derived, (a, b) => a.Id == b.Id)
            .CreateDeleteJoinBuilder()
            .Returning()
            .ToSql();

        direct.Should().Contain("__s1_id");
        direct.Should().Contain("__s2_Id");

        var scope = ctx.With("del", ctx.From<IdentityDerivedEntity>()
            .Join(derived, (a, b) => a.Id == b.Id)
            .CreateDeleteJoinBuilder()
            .Returning());
        var read = SqlOf(ctx, scope.From("del").Select(r => r));

        read.Should().Contain("select \"__s1_id\", \"__s1_name\", \"__s2_Id\", \"__s2_Name\" from del as \"t1\"");
    }

    [Fact]
    public void UpdateJoin_WithoutReturning_ShouldNotEmitReturningColumns()
    {
        using var ctx = PostgresTestContext.Create();

        Join2(ctx).UpdateJoin().Set(p => p.Item1.Name, "x").ToSql().Should().NotContain("returning");
    }

    [Fact]
    public void DeleteJoin_WithoutReturning_ShouldNotEmitReturningColumns()
    {
        using var ctx = PostgresTestContext.Create();

        Join2(ctx).ToSql().Should().NotContain("returning");
    }

    // --- unsupported join forms stay rejected for the identity Returning() form (INNER-only is by design) ---

    [Theory]
    [InlineData("left")]
    [InlineData("cross")]
    public void UpdateJoinIdentity_UnsupportedJoinForm_ShouldReject(string joinForm)
    {
        using var ctx = PostgresTestContext.Create();

        Action act = joinForm switch
        {
            "left" => () => ctx.From<IMergeEntity>()
                .LeftJoin(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
                .UpdateJoin()
                .Set(p => p.Item1.Name, "x")
                .Returning()
                .ToSql(),
            "cross" => () => ctx.From<IMergeEntity>()
                .CrossJoin(ctx.From<IMergeEntity>())
                .UpdateJoin()
                .Set(p => p.Item1.Name, "x")
                .Returning()
                .ToSql(),
            _ => throw new ArgumentOutOfRangeException(nameof(joinForm)),
        };

        act.Should().Throw<NotSupportedException>().WithMessage("*only supports INNER joins*");
    }

    [Theory]
    [InlineData("left")]
    [InlineData("cross")]
    public void DeleteJoinIdentity_UnsupportedJoinForm_ShouldReject(string joinForm)
    {
        using var ctx = PostgresTestContext.Create();

        Action act = joinForm switch
        {
            "left" => () => ctx.From<IMergeEntity>()
                .LeftJoin(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
                .CreateDeleteJoinBuilder()
                .Returning()
                .ToSql(),
            "cross" => () => ctx.From<IMergeEntity>()
                .CrossJoin(ctx.From<IMergeEntity>())
                .CreateDeleteJoinBuilder()
                .Returning()
                .ToSql(),
            _ => throw new ArgumentOutOfRangeException(nameof(joinForm)),
        };

        act.Should().Throw<NotSupportedException>().WithMessage("*only supports INNER joins*");
    }

    [Fact]
    public void UpdateJoinIdentity_UnsupportedJoinForm_MutationCteBody_ShouldReject()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.With("upd", ctx.From<IMergeEntity>()
            .LeftJoin(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Name, "x")
            .Returning());

        act.Should().Throw<NotSupportedException>().WithMessage("*only supports INNER joins*");
    }

    // --- helpers ---

    private static string UpdateSql(IDataContext ctx, int arity, bool identityLambda = false) => arity switch
    {
        2 => BuildUpdate2(ctx, identityLambda).ToSql(),
        3 => BuildUpdate3(ctx, identityLambda).ToSql(),
        8 => BuildUpdate8(ctx, identityLambda).ToSql(),
        _ => throw new ArgumentOutOfRangeException(nameof(arity)),
    };

    private static string DeleteSql(IDataContext ctx, int arity, bool identityLambda = false) => arity switch
    {
        2 => BuildDelete2(ctx, identityLambda).ToSql(),
        3 => BuildDelete3(ctx, identityLambda).ToSql(),
        8 => BuildDelete8(ctx, identityLambda).ToSql(),
        _ => throw new ArgumentOutOfRangeException(nameof(arity)),
    };

    private static UpdateJoinReturningBuilder<Projection<IMergeEntity, IMergeEntity>, Projection<IMergeEntity, IMergeEntity>> BuildUpdate2(IDataContext ctx, bool lambda)
    {
        var update = Join2(ctx).UpdateJoin().Set(p => p.Item1.Name, "x");
        return lambda ? update.Returning(p => p) : update.Returning();
    }

    private static UpdateJoinReturningBuilder<Projection<IMergeEntity, IMergeEntity, IMergeEntity>, Projection<IMergeEntity, IMergeEntity, IMergeEntity>> BuildUpdate3(IDataContext ctx, bool lambda)
    {
        var update = Join3(ctx).UpdateJoin().Set(p => p.Item1.Name, "x");
        return lambda ? update.Returning(p => p) : update.Returning();
    }

    private static UpdateJoinReturningBuilder<Projection<IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity>, Projection<IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity>> BuildUpdate8(IDataContext ctx, bool lambda)
    {
        var update = Join8(ctx).UpdateJoin().Set(p => p.Item1.Name, "x");
        return lambda ? update.Returning(p => p) : update.Returning();
    }

    private static DeleteJoinReturningBuilder<Projection<IMergeEntity, IMergeEntity>, Projection<IMergeEntity, IMergeEntity>> BuildDelete2(IDataContext ctx, bool lambda)
        => lambda ? Join2(ctx).CreateDeleteJoinBuilder().Returning(p => p) : Join2(ctx).CreateDeleteJoinBuilder().Returning();

    private static DeleteJoinReturningBuilder<Projection<IMergeEntity, IMergeEntity, IMergeEntity>, Projection<IMergeEntity, IMergeEntity, IMergeEntity>> BuildDelete3(IDataContext ctx, bool lambda)
        => lambda ? Join3(ctx).CreateDeleteJoinBuilder().Returning(p => p) : Join3(ctx).CreateDeleteJoinBuilder().Returning();

    private static DeleteJoinReturningBuilder<Projection<IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity>, Projection<IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity>> BuildDelete8(IDataContext ctx, bool lambda)
        => lambda ? Join8(ctx).CreateDeleteJoinBuilder().Returning(p => p) : Join8(ctx).CreateDeleteJoinBuilder().Returning();

    private static JoinedEntityBuilder<IMergeEntity, IMergeEntity> Join2(IDataContext ctx)
        => ctx.From<IMergeEntity>().Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id);

    private static JoinedEntityBuilder<IMergeEntity, IMergeEntity, IMergeEntity> Join3(IDataContext ctx)
        => Join2(ctx).Join(ctx.From<IMergeEntity>(), (a, b) => a.Item1.Id == b.Id);

    private static JoinedEntityBuilder<IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity, IMergeEntity> Join8(IDataContext ctx)
        => Join3(ctx)
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Item1.Id == b.Id)
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Item1.Id == b.Id)
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Item1.Id == b.Id)
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Item1.Id == b.Id)
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Item1.Id == b.Id);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText.Replace("\r\n", "\n");

    [SqlTable("identity_derived_entity")]
    public sealed class IdentityDerivedEntity
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    // Metadata-less shape: never registered with From<T>/Join<T>, so its columns are its readable CLR
    // surface. Exercises the parse/render parity for a derived/read-CTE joined slot.
    public sealed class IdentityDerivedShape
    {
        public long Id { get; set; }

        public string? Name { get; set; }
    }

    // No database is involved; the fixture exists so the whole-output alias allocator sees a repeated
    // physical column ("id" mapped twice) plus a literal alias-shaped column ("id_2") whose base collides
    // with the disambiguated alias of the repeated column.
    [SqlTable("alias_collision_entity")]
    public sealed class AliasCollisionEntity
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("id")]
        public long IdTwin { get; set; }

        [Column("id_2")]
        public long Secondary { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }
}
