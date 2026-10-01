using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace NextORM.Integration.Tests;

using P2 = Projection<IdnEntity, IdnEntity>;
using P3 = Projection<IdnEntity, IdnEntity, IdnEntity>;
using P4 = Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity>;
using P5 = Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>;
using P6 = Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>;
using P7 = Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>;
using P8 = Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>;
using U2 = UpdateJoinReturningBuilder<Projection<IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity>>;
using U3 = UpdateJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity>>;
using U4 = UpdateJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity>>;
using U5 = UpdateJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>>;
using U6 = UpdateJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>>;
using U7 = UpdateJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>>;
using U8 = UpdateJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>>;
using D2 = DeleteJoinReturningBuilder<Projection<IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity>>;
using D3 = DeleteJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity>>;
using D4 = DeleteJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity>>;
using D5 = DeleteJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>>;
using D6 = DeleteJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>>;
using D7 = DeleteJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>>;
using D8 = DeleteJoinReturningBuilder<Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>, Projection<IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity, IdnEntity>>;
using MX3 = Projection<IdnAlpha, IdnBeta, IdnAlpha>;
using MX8 = Projection<IdnAlpha, IdnBeta, IdnAlpha, IdnBeta, IdnAlpha, IdnBeta, IdnAlpha, IdnBeta>;

/// <summary>
/// Production integration coverage (real PostgreSQL) of the identity (whole-<c>Projection</c>) multi-table
/// <c>RETURNING</c> form for joined <c>UPDATE ... FROM ... RETURNING</c> and <c>DELETE ... USING ... RETURNING</c>:
/// slot order and <c>ItemN</c> values, self-joins of one CLR type, collisions across different types with
/// identical property names, per-slot SQL aliases, mutation-CTE consumption, a derived joined side, zero-row
/// results and the explicit-projection regression. The feature is PostgreSQL-only, so this file is
/// provider-specific rather than part of <see cref="CommonTestSuite"/>.
/// </summary>
public sealed class IdentityReturningIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    // --- A. arity sweep: standalone materialization of every slot, parametersless identity and lambda identity ---

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void UpdateJoinIdentity_ShouldMaterializeEverySlot(int arity)
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            RunUpdateIdentity(ctx, arity);
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void DeleteJoinIdentity_ShouldMaterializeEverySlot(int arity)
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            RunDeleteIdentity(ctx, arity);
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void UpdateJoinIdentity_LambdaForm_ShouldEqualParameterless(int arity)
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            switch (arity)
            {
                case 2: AssertUpdateIdentity(Update2Lambda(ctx), 2); break;
                case 3: AssertUpdateIdentity(Update3Lambda(ctx), 3); break;
                case 8: AssertUpdateIdentity(Update8Lambda(ctx), 8); break;
                default: throw new ArgumentOutOfRangeException(nameof(arity));
            }
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void DeleteJoinIdentity_LambdaForm_ShouldEqualParameterless(int arity)
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            switch (arity)
            {
                case 2: AssertDeleteIdentity(Delete2Lambda(ctx), 2); break;
                case 3: AssertDeleteIdentity(Delete3Lambda(ctx), 3); break;
                case 8: AssertDeleteIdentity(Delete8Lambda(ctx), 8); break;
                default: throw new ArgumentOutOfRangeException(nameof(arity));
            }
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    // --- B. detailed standalone semantics at arity 2 (post-update / deleted-row values, self-join slots) ---

    [Fact]
    public void UpdateJoinIdentity_ShouldReturnPostUpdateTargetValues()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var row = Update2(ctx).Single();

            ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
            ((string?)Value(Item(row, 1), "Name")).Should().Be("n2");
            ((int)Value(Item(row, 1), "Age")!).Should().Be(10);
            ((int)Value(Item(row, 2), "Id")!).Should().Be(2);
            ((string?)Value(Item(row, 2), "Name")).Should().Be("n2");
            ((int)Value(Item(row, 2), "Age")!).Should().Be(20);

            // The write persists.
            ctx.From<IdnEntity>().Where(x => x.Id == 1).Select(x => x.Name).Single().Should().Be("n2");
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Fact]
    public void DeleteJoinIdentity_ShouldReturnDeletedRowValues()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var row = Delete2(ctx).Single();

            // Item1 is the deleted target; its original values are returned.
            ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
            ((string?)Value(Item(row, 1), "Name")).Should().Be("n1");
            ((int)Value(Item(row, 1), "Age")!).Should().Be(10);
            ((int)Value(Item(row, 2), "Id")!).Should().Be(2);
            ((string?)Value(Item(row, 2), "Name")).Should().Be("n2");

            ctx.From<IdnEntity>().Where(x => x.Id == 1).Select(x => x.Id).ToList().Should().BeEmpty();
            ctx.From<IdnEntity>().Where(x => x.Id == 2).Select(x => x.Id).ToList().Should().Equal(2);
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Fact]
    public void UpdateJoinIdentity_SelfJoinSameType_ShouldKeepSlotsDistinct()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var sql = Update2(ctx).ToSql();
            AssertUniqueSlotAliases(sql, 2, "id", "name", "age");

            var row = Update2(ctx).Single();

            // Same CLR type and identical property names, but the two slots stay distinct.
            ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
            ((int)Value(Item(row, 2), "Id")!).Should().Be(2);
            ((int)Value(Item(row, 1), "Id")!).Should().NotBe((int)Value(Item(row, 2), "Id")!);
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    // --- C. collisions: different types, repeated/nonadjacent types, physical names, converter/duration metadata ---

    [Fact]
    public void UpdateJoinIdentity_DifferentTypesSamePropertyNames_ShouldMaterializeBothSlots()
    {
        var ctx = _sut.DataProvider;
        CreateAlphaBeta(ctx);
        try
        {
            var builder = ctx.From<IdnAlpha>()
                .Join(ctx.From<IdnBeta>(), (a, b) => a.Id == 1 && b.Id == 2)
                .UpdateJoin()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning();

            AssertUniqueSlotAliases(builder.ToSql(), 2, "id", "name", "age");

            var row = builder.Single();

            Item(row, 1).Should().BeOfType<IdnAlpha>();
            Item(row, 2).Should().BeOfType<IdnBeta>();
            ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
            ((string?)Value(Item(row, 1), "Name")).Should().Be("b2");
            ((int)Value(Item(row, 2), "Id")!).Should().Be(2);
            ((string?)Value(Item(row, 2), "Name")).Should().Be("b2");
        }
        finally
        {
            DropAlphaBeta(ctx);
        }
    }

    [Fact]
    public void UpdateJoinIdentity_MixedRepeatedNonAdjacentTypes_Arity3()
    {
        var ctx = _sut.DataProvider;
        CreateAlphaBeta(ctx);
        try
        {
            var builder = ctx.From<IdnAlpha>()
                .Join(ctx.From<IdnBeta>(), (a, b) => a.Id == 1 && b.Id == 2)
                .Join(ctx.From<IdnAlpha>(), (p, c) => c.Id == 3)
                .UpdateJoin()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning();

            AssertUniqueSlotAliases(builder.ToSql(), 3, "id", "name", "age");

            var row = builder.Single();

            Item(row, 1).Should().BeOfType<IdnAlpha>();
            Item(row, 2).Should().BeOfType<IdnBeta>();
            Item(row, 3).Should().BeOfType<IdnAlpha>();

            ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
            ((string?)Value(Item(row, 1), "Name")).Should().Be("b2");
            ((int)Value(Item(row, 2), "Id")!).Should().Be(2);
            ((string?)Value(Item(row, 2), "Name")).Should().Be("b2");
            ((int)Value(Item(row, 3), "Id")!).Should().Be(3);
            ((string?)Value(Item(row, 3), "Name")).Should().Be("a3");
        }
        finally
        {
            DropAlphaBeta(ctx);
        }
    }

    [Fact]
    public void UpdateJoinIdentity_MixedRepeatedNonAdjacentTypes_Arity8()
    {
        var ctx = _sut.DataProvider;
        CreateAlphaBeta(ctx);
        try
        {
            var builder = ctx.From<IdnAlpha>()
                .Join(ctx.From<IdnBeta>(), (a, b) => a.Id == 1 && b.Id == 2)
                .Join(ctx.From<IdnAlpha>(), (p, c) => c.Id == 3)
                .Join(ctx.From<IdnBeta>(), (p, c) => c.Id == 4)
                .Join(ctx.From<IdnAlpha>(), (p, c) => c.Id == 5)
                .Join(ctx.From<IdnBeta>(), (p, c) => c.Id == 6)
                .Join(ctx.From<IdnAlpha>(), (p, c) => c.Id == 7)
                .Join(ctx.From<IdnBeta>(), (p, c) => c.Id == 8)
                .UpdateJoin()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning();

            AssertUniqueSlotAliases(builder.ToSql(), 8, "id", "name", "age");

            var row = builder.Single();

            for (var slot = 1; slot <= 8; slot++)
            {
                var expectedType = slot % 2 == 1 ? typeof(IdnAlpha) : typeof(IdnBeta);
                Item(row, slot).Should().BeOfType(expectedType);
                ((int)Value(Item(row, slot), "Id")!).Should().Be(slot);
                var expectedName = slot == 1 ? "b2" : slot % 2 == 1 ? $"a{slot}" : $"b{slot}";
                ((string?)Value(Item(row, slot), "Name")).Should().Be(expectedName);
            }
        }
        finally
        {
            DropAlphaBeta(ctx);
        }
    }

    [Fact]
    public void UpdateJoinIdentity_PhysicalNamesDifferFromClrNames()
    {
        var ctx = _sut.DataProvider;
        CreateMapped(ctx);
        try
        {
            var builder = ctx.From<IdnMapped>()
                .Join(ctx.From<IdnMapped>(), (a, b) => a.Id == 1 && b.Id == 2)
                .UpdateJoin()
                .Set(p => p.Item1.Label, p => p.Item2.Label)
                .Returning();

            var sql = builder.ToSql();
            // The identity alias is derived from the physical column name, not the CLR member name.
            sql.Should().Contain("\"__s1_phys_code\"").And.Contain("\"__s2_phys_code\"");
            sql.Should().Contain("\"__s1_phys_label\"").And.Contain("\"__s2_phys_amount\"");
            AssertUniqueSlotAliases(sql, 2, "id", "phys_code", "phys_label", "phys_amount");

            var row = builder.Single();

            ((int)Value(Item(row, 1), "Code")!).Should().Be(100);
            ((string?)Value(Item(row, 1), "Label")).Should().Be("two");
            ((int)Value(Item(row, 1), "Amount")!).Should().Be(15);
            ((int)Value(Item(row, 2), "Code")!).Should().Be(200);
            ((string?)Value(Item(row, 2), "Label")).Should().Be("two");
            ((int)Value(Item(row, 2), "Amount")!).Should().Be(25);
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_mapped");
        }
    }

    [Fact]
    public void UpdateJoinIdentity_RepeatedAndAliasShapedColumns_ShouldMaterializeDistinctSlots()
    {
        var ctx = _sut.DataProvider;
        CreateAliasCollision(ctx);
        try
        {
            var builder = ctx.From<IdnAliasCollision>()
                .Join(ctx.From<IdnAliasCollision>(), (a, b) => a.Id == 1 && b.Id == 2)
                .UpdateJoin()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning();

            var sql = builder.ToSql();
            // "id" is mapped twice; the literal "id_2" is bumped past the disambiguated repeated column.
            sql.Should().Contain("\"__s1_id_2_2\"").And.Contain("\"__s2_id_2_2\"");

            var row = builder.Single();
            AssertAliasCollisionSlot(row, 1, id: 1, secondary: 11, name: "two");
            AssertAliasCollisionSlot(row, 2, id: 2, secondary: 22, name: "two");
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_alias_collision");
        }
    }

    [Fact]
    public void MutationCteUpdateIdentity_RepeatedAndAliasShapedColumns_ShouldMaterializeDistinctSlots()
    {
        var ctx = _sut.DataProvider;
        CreateAliasCollision(ctx);
        try
        {
            var scope = ctx.With("upd", ctx.From<IdnAliasCollision>()
                .Join(ctx.From<IdnAliasCollision>(), (a, b) => a.Id == 1 && b.Id == 2)
                .UpdateJoin()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning());

            var sql = SqlOf(ctx, scope.From("upd").Select(r => r));
            sql.Should().Contain("\"__s1_id_2_2\"").And.Contain("\"__s2_id_2_2\"");

            var rows = scope.From("upd").Select(r => r).ToList();
            rows.Should().ContainSingle();

            var row = (object)rows[0]!;
            AssertAliasCollisionSlot(row, 1, id: 1, secondary: 11, name: "two");
            AssertAliasCollisionSlot(row, 2, id: 2, secondary: 22, name: "two");
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_alias_collision");
        }
    }

    [Fact]
    public void UpdateJoinIdentity_ConverterAndDurationMetadata_ShouldMaterialize()
    {
        var ctx = _sut.DataProvider;
        CreateConverted(ctx);
        try
        {
            var builder = ctx.From<IdnConverted>()
                .Join(ctx.From<IdnConverted>(), (a, b) => a.Id == 1 && b.Id == 2)
                .UpdateJoin()
                .Set(p => p.Item1.State, p => p.Item2.State)
                .Returning();

            var sql = builder.ToSql();
            // The enum converter's provider type (text) and the duration columns keep their identity aliases.
            sql.Should().Contain("\"__s1_state\"").And.Contain("\"__s2_state\"");
            sql.Should().Contain("\"__s1_span\"").And.Contain("\"__s1_span_sec\"");
            AssertUniqueSlotAliases(sql, 2, "id", "state", "span", "span_sec");

            var row = builder.Single();

            ((IdnState)Value(Item(row, 1), "State")!).Should().Be(IdnState.Closed);
            ((IdnState)Value(Item(row, 2), "State")!).Should().Be(IdnState.Closed);
            ((TimeSpan)Value(Item(row, 1), "Span")!).Should().Be(TimeSpan.FromSeconds(10));
            ((TimeSpan)Value(Item(row, 1), "SpanSeconds")!).Should().Be(TimeSpan.FromSeconds(90));
            ((TimeSpan)Value(Item(row, 2), "Span")!).Should().Be(TimeSpan.FromSeconds(20));
            ((TimeSpan)Value(Item(row, 2), "SpanSeconds")!).Should().Be(TimeSpan.FromSeconds(30));
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_converted");
        }
    }

    // --- D. mutation-CTE consumption: full projection plus separate slot select/filter ---

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void MutationCteUpdateIdentity_ShouldMaterializeFullProjection(int arity)
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            switch (arity)
            {
                case 2: AssertCteUpdateIdentity(ctx, Update2(ctx), 2); break;
                case 3: AssertCteUpdateIdentity(ctx, Update3(ctx), 3); break;
                case 8: AssertCteUpdateIdentity(ctx, Update8(ctx), 8); break;
                default: throw new ArgumentOutOfRangeException(nameof(arity));
            }
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void MutationCteDeleteIdentity_ShouldMaterializeFullProjection(int arity)
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            switch (arity)
            {
                case 2: AssertCteDeleteIdentity(ctx, Delete2(ctx), 2); break;
                case 3: AssertCteDeleteIdentity(ctx, Delete3(ctx), 3); break;
                case 8: AssertCteDeleteIdentity(ctx, Delete8(ctx), 8); break;
                default: throw new ArgumentOutOfRangeException(nameof(arity));
            }
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Fact]
    public void MutationCteIdentity_ShouldSelectAndFilterSlotsSeparately()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var scope = ctx.With("upd", Update2(ctx));

            var read = scope.From("upd").Select(r => new { A = r.Item1.Id, B = r.Item2.Id });
            var sql = SqlOf(ctx, read);
            sql.Should().Contain("\"__s1_id\"").And.Contain("\"__s2_id\"");
            AssertUniqueSlotAliases(sql, 2, "id", "name", "age");

            var ids = read.ToList();
            ids.Should().ContainSingle();
            ids[0].A.Should().Be(1);
            ids[0].B.Should().Be(2);

            var byFirst = scope.From("upd").Where(r => r.Item1.Id == 1)
                .Select(r => new { A = r.Item1.Id, B = r.Item2.Id }).ToList();
            byFirst.Should().ContainSingle().Which.B.Should().Be(2);

            var bySecond = scope.From("upd").Where(r => r.Item2.Id == 2)
                .Select(r => new { A = r.Item1.Id, B = r.Item2.Id }).ToList();
            bySecond.Should().ContainSingle().Which.A.Should().Be(1);
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    // --- E. one allowed derived joined side with a complete shape ---

    [Fact]
    public void DerivedJoinedSide_UpdateIdentity_ShouldMaterializeCompleteShape()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var derived = ctx.From<IdnEntity>().Where(x => x.Id == 2)
                .Select(x => new IdnEntity { Id = x.Id, Name = x.Name, Age = x.Age });

            var builder = ctx.From<IdnEntity>()
                .Join(_sut.From(derived), (a, b) => a.Id == 1 && b.Id == 2)
                .UpdateJoin()
                .Set(p => p.Item1.Name, "changed")
                .Returning();

            var row = builder.Single();

            ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
            ((string?)Value(Item(row, 1), "Name")).Should().Be("changed");
            ((int)Value(Item(row, 1), "Age")!).Should().Be(10);
            ((int)Value(Item(row, 2), "Id")!).Should().Be(2);
            ((string?)Value(Item(row, 2), "Name")).Should().Be("n2");
            ((int)Value(Item(row, 2), "Age")!).Should().Be(20);
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Fact]
    public void DerivedJoinedSide_DeleteIdentity_ShouldMaterializeCompleteShape()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var derived = ctx.From<IdnEntity>().Where(x => x.Id == 2)
                .Select(x => new IdnEntity { Id = x.Id, Name = x.Name, Age = x.Age });

            var row = ctx.From<IdnEntity>()
                .Join(_sut.From(derived), (a, b) => a.Id == 1 && b.Id == 2)
                .CreateDeleteJoinBuilder()
                .Returning()
                .Single();

            ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
            ((string?)Value(Item(row, 1), "Name")).Should().Be("n1");
            ((int)Value(Item(row, 2), "Id")!).Should().Be(2);
            ((string?)Value(Item(row, 2), "Name")).Should().Be("n2");
            ((int)Value(Item(row, 2), "Age")!).Should().Be(20);

            ctx.From<IdnEntity>().Where(x => x.Id == 1).Select(x => x.Id).ToList().Should().BeEmpty();
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    // --- E2. metadata-less derived joined side with a complete shape: real per-slot values ---

    [Fact]
    public void DerivedMetadataLessFullShape_UpdateIdentity_ShouldMaterializeCompleteShape()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var derived = ctx.From<IdnEntity>().Where(x => x.Id == 2)
                .Select(x => new IdnDerivedShape { Id = x.Id, Name = x.Name, Age = x.Age });

            var builder = ctx.From<IdnEntity>()
                .Join(_sut.From(derived), (a, b) => a.Id == 1 && b.Id == 2)
                .UpdateJoin()
                .Set(p => p.Item1.Name, "changed")
                .Returning();

            var sql = builder.ToSql();
            // Slot 1 is a registered entity (physical column aliases); slot 2 is a metadata-less
            // shape, so its identity aliases use the CLR member names the derived read exposes.
            sql.Should().Contain("\"__s1_id\"").And.Contain("\"__s1_name\"").And.Contain("\"__s1_age\"");
            sql.Should().Contain("\"__s2_Id\"").And.Contain("\"__s2_Name\"").And.Contain("\"__s2_Age\"");

            var row = builder.Single();

            ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
            ((string?)Value(Item(row, 1), "Name")).Should().Be("changed");
            ((int)Value(Item(row, 1), "Age")!).Should().Be(10);
            ((int)Value(Item(row, 2), "Id")!).Should().Be(2);
            ((string?)Value(Item(row, 2), "Name")).Should().Be("n2");
            ((int)Value(Item(row, 2), "Age")!).Should().Be(20);

            ctx.From<IdnEntity>().Where(x => x.Id == 1).Select(x => x.Name).Single().Should().Be("changed");
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Fact]
    public void DerivedMetadataLessFullShape_DeleteIdentity_ShouldMaterializeCompleteShape()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var derived = ctx.From<IdnEntity>().Where(x => x.Id == 2)
                .Select(x => new IdnDerivedShape { Id = x.Id, Name = x.Name, Age = x.Age });

            var builder = ctx.From<IdnEntity>()
                .Join(_sut.From(derived), (a, b) => a.Id == 1 && b.Id == 2)
                .CreateDeleteJoinBuilder()
                .Returning();

            var sql = builder.ToSql();
            sql.Should().Contain("\"__s1_id\"").And.Contain("\"__s1_name\"").And.Contain("\"__s1_age\"");
            sql.Should().Contain("\"__s2_Id\"").And.Contain("\"__s2_Name\"").And.Contain("\"__s2_Age\"");

            var row = builder.Single();

            ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
            ((string?)Value(Item(row, 1), "Name")).Should().Be("n1");
            ((int)Value(Item(row, 1), "Age")!).Should().Be(10);
            ((int)Value(Item(row, 2), "Id")!).Should().Be(2);
            ((string?)Value(Item(row, 2), "Name")).Should().Be("n2");
            ((int)Value(Item(row, 2), "Age")!).Should().Be(20);

            ctx.From<IdnEntity>().Where(x => x.Id == 1).Select(x => x.Id).ToList().Should().BeEmpty();
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Fact]
    public void DerivedMetadataLessFullShape_UpdateIdentity_MutationCteShouldMaterialize()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var derived = ctx.From<IdnEntity>().Where(x => x.Id == 2)
                .Select(x => new IdnDerivedShape { Id = x.Id, Name = x.Name, Age = x.Age });

            var scope = ctx.With("upd", ctx.From<IdnEntity>()
                .Join(_sut.From(derived), (a, b) => a.Id == 1 && b.Id == 2)
                .UpdateJoin()
                .Set(p => p.Item1.Name, "changed")
                .Returning());

            var sql = SqlOf(ctx, scope.From("upd").Select(r => r));
            sql.Should().Contain("\"__s1_id\"").And.Contain("\"__s2_Id\"");

            var rows = scope.From("upd").Select(r => r).ToList();
            rows.Should().ContainSingle();

            var row = (object)rows[0]!;
            ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
            ((string?)Value(Item(row, 1), "Name")).Should().Be("changed");
            ((int)Value(Item(row, 1), "Age")!).Should().Be(10);
            ((int)Value(Item(row, 2), "Id")!).Should().Be(2);
            ((string?)Value(Item(row, 2), "Name")).Should().Be("n2");
            ((int)Value(Item(row, 2), "Age")!).Should().Be(20);
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Fact]
    public void DerivedMetadataLessFullShape_DeleteIdentity_MutationCteShouldMaterialize()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var derived = ctx.From<IdnEntity>().Where(x => x.Id == 2)
                .Select(x => new IdnDerivedShape { Id = x.Id, Name = x.Name, Age = x.Age });

            var scope = ctx.With("del", ctx.From<IdnEntity>()
                .Join(_sut.From(derived), (a, b) => a.Id == 1 && b.Id == 2)
                .CreateDeleteJoinBuilder()
                .Returning());

            var sql = SqlOf(ctx, scope.From("del").Select(r => r));
            sql.Should().Contain("\"__s1_id\"").And.Contain("\"__s2_Id\"");

            var rows = scope.From("del").Select(r => r).ToList();
            rows.Should().ContainSingle();

            var row = (object)rows[0]!;
            ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
            ((string?)Value(Item(row, 1), "Name")).Should().Be("n1");
            ((int)Value(Item(row, 1), "Age")!).Should().Be(10);
            ((int)Value(Item(row, 2), "Id")!).Should().Be(2);
            ((string?)Value(Item(row, 2), "Name")).Should().Be("n2");
            ((int)Value(Item(row, 2), "Age")!).Should().Be(20);

            ctx.From<IdnEntity>().Where(x => x.Id == 1).Select(x => x.Id).ToList().Should().BeEmpty();
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    // --- E3. partial joined shape: fail before the database, naming slot/member ---

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DerivedPartialShape_Identity_ShouldRejectNamingSlotAndMember(bool update)
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            // The metadata-less joined shape declares Id, Name and Age, but the derived joined side
            // supplies only Id and Name: the required Age member cannot be resolved from the source shape.
            var derived = ctx.From<IdnEntity>().Where(x => x.Id == 2)
                .Select(x => new IdnDerivedShape { Id = x.Id, Name = x.Name });

            Action act = update
                ? () => ctx.From<IdnEntity>()
                    .Join(_sut.From(derived), (a, b) => a.Id == 1 && b.Id == 2)
                    .UpdateJoin()
                    .Set(p => p.Item1.Name, "changed")
                    .Returning()
                    .ToSql()
                : () => ctx.From<IdnEntity>()
                    .Join(_sut.From(derived), (a, b) => a.Id == 1 && b.Id == 2)
                    .CreateDeleteJoinBuilder()
                    .Returning()
                    .ToSql();

            act.Should().Throw<QueryPreparationException>()
                .WithMessage("*Age*item 2*");

            // Rejected before any database execution: the target row is untouched.
            ctx.From<IdnEntity>().Where(x => x.Id == 1).Select(x => x.Name).Single().Should().Be("n1");
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DerivedPartialShape_Identity_MutationCteShouldRejectNamingSlotAndMember(bool update)
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            // Same incomplete joined shape as the standalone case, but consumed through the mutation
            // CTE route: the CTE read shape must reject it with the same slot/member diagnostic.
            var derived = ctx.From<IdnEntity>().Where(x => x.Id == 2)
                .Select(x => new IdnDerivedShape { Id = x.Id, Name = x.Name });

            // The mutation body is rendered lazily when the CTE read is prepared, so the diagnostic is
            // observed while preparing the read rather than at ctx.With itself.
            Action act = () =>
            {
                var scope = update
                    ? ctx.With("upd", ctx.From<IdnEntity>()
                        .Join(_sut.From(derived), (a, b) => a.Id == 1 && b.Id == 2)
                        .UpdateJoin()
                        .Set(p => p.Item1.Name, "changed")
                        .Returning())
                    : ctx.With("del", ctx.From<IdnEntity>()
                        .Join(_sut.From(derived), (a, b) => a.Id == 1 && b.Id == 2)
                        .CreateDeleteJoinBuilder()
                        .Returning());

                _ = SqlOf(ctx, scope.From(update ? "upd" : "del").Select(r => r));
            };

            act.Should().Throw<QueryPreparationException>()
                .WithMessage("*Age*item 2*");

            // Rejected before any database execution: the target row is untouched.
            ctx.From<IdnEntity>().Where(x => x.Id == 1).Select(x => x.Name).Single().Should().Be("n1");
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    // --- F. zero-row mutation returns nothing, no fabricated items ---

    [Fact]
    public void ZeroRowUpdateIdentity_ShouldReturnEmpty()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var rows = ctx.From<IdnEntity>()
                .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
                .UpdateJoin()
                .Set(p => p.Item1.Name, "x")
                .Where(p => p.Item1.Id < 0)
                .Returning()
                .ToList();

            rows.Should().BeEmpty();
            ctx.From<IdnEntity>().Where(x => x.Id == 1).Select(x => x.Name).Single().Should().Be("n1");
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    [Fact]
    public void ZeroRowDeleteIdentity_ShouldReturnEmpty()
    {
        var ctx = _sut.DataProvider;
        CreateIdnEntity(ctx);
        try
        {
            var rows = ctx.From<IdnEntity>()
                .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
                .Where(p => p.Item1.Id < 0)
                .CreateDeleteJoinBuilder()
                .Returning()
                .ToList();

            rows.Should().BeEmpty();
            ctx.From<IdnEntity>().Where(x => x.Id == 1).Select(x => x.Id).ToList().Should().Equal(1);
        }
        finally
        {
            Execute(ctx, "drop table if exists idn_entity");
        }
    }

    // --- G. regression: explicit projections still materialize; no implicit RETURNING without Returning() ---

    [Fact]
    public void ExplicitReturningProjections_ShouldStillMaterialize()
    {
        var ctx = _sut.DataProvider;
        CreateAlphaBeta(ctx);
        try
        {
            // No Returning() at all: neither operation emits an implicit RETURNING list.
            ctx.From<IdnAlpha>()
                .Join(ctx.From<IdnBeta>(), (a, b) => a.Id == 1 && b.Id == 2)
                .UpdateJoin()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .ToSql().Should().NotContainEquivalentOf("returning");
            ctx.From<IdnAlpha>()
                .Join(ctx.From<IdnBeta>(), (a, b) => a.Id == 1 && b.Id == 2)
                .ToSql().Should().NotContainEquivalentOf("returning");

            var update = ctx.From<IdnAlpha>()
                .Join(ctx.From<IdnBeta>(), (a, b) => a.Id == 1 && b.Id == 2)
                .UpdateJoin()
                .Set(p => p.Item1.Name, p => p.Item2.Name);

            var scalar = update.Returning(p => p.Item1.Id).Single();
            scalar.Should().Be(1);

            var anonymous = update.Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name }).Single();
            anonymous.TargetId.Should().Be(1);
            anonymous.SourceName.Should().Be("b2");

            var viaCtor = update.Returning(p => new IdnReturningDto(p.Item1.Id, p.Item2.Name)).Single();
            viaCtor.TargetId.Should().Be(1);
            viaCtor.SourceName.Should().Be("b2");

            var viaMemberInit = update.Returning(p => new IdnReturningDto { TargetId = p.Item1.Id, SourceName = p.Item2.Name }).Single();
            viaMemberInit.TargetId.Should().Be(1);
            viaMemberInit.SourceName.Should().Be("b2");

            // A joined SELECT without a mutation keeps its normal projection.
            ctx.From<IdnAlpha>()
                .Join(ctx.From<IdnBeta>(), (a, b) => a.Id == 1 && b.Id == 2)
                .Select(p => new { TargetId = p.Item1.Id, SourceId = p.Item2.Id })
                .Single().TargetId.Should().Be(1);
        }
        finally
        {
            DropAlphaBeta(ctx);
        }
    }

    // --- arity-specific builders ---

    private static U2 Update2(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .UpdateJoin().Set(p => p.Item1.Name, p => p.Item2.Name).Returning();

    private static U3 Update3(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .UpdateJoin().Set(p => p.Item1.Name, p => p.Item2.Name).Returning();

    private static U4 Update4(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4)
        .UpdateJoin().Set(p => p.Item1.Name, p => p.Item2.Name).Returning();

    private static U5 Update5(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 5)
        .UpdateJoin().Set(p => p.Item1.Name, p => p.Item2.Name).Returning();

    private static U6 Update6(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 5)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 6)
        .UpdateJoin().Set(p => p.Item1.Name, p => p.Item2.Name).Returning();

    private static U7 Update7(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 5)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 6)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 7)
        .UpdateJoin().Set(p => p.Item1.Name, p => p.Item2.Name).Returning();

    private static U8 Update8(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 5)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 6)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 7)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 8)
        .UpdateJoin().Set(p => p.Item1.Name, p => p.Item2.Name).Returning();

    private static U2 Update2Lambda(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .UpdateJoin().Set(p => p.Item1.Name, p => p.Item2.Name).Returning(p => p);

    private static U3 Update3Lambda(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .UpdateJoin().Set(p => p.Item1.Name, p => p.Item2.Name).Returning(p => p);

    private static U8 Update8Lambda(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 5)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 6)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 7)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 8)
        .UpdateJoin().Set(p => p.Item1.Name, p => p.Item2.Name).Returning(p => p);

    private static D2 Delete2(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2).CreateDeleteJoinBuilder().Returning();

    private static D3 Delete3(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3).CreateDeleteJoinBuilder().Returning();

    private static D4 Delete4(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4).CreateDeleteJoinBuilder().Returning();

    private static D5 Delete5(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 5).CreateDeleteJoinBuilder().Returning();

    private static D6 Delete6(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 5)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 6).CreateDeleteJoinBuilder().Returning();

    private static D7 Delete7(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 5)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 6)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 7).CreateDeleteJoinBuilder().Returning();

    private static D8 Delete8(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 5)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 6)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 7)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 8).CreateDeleteJoinBuilder().Returning();

    private static D2 Delete2Lambda(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2).CreateDeleteJoinBuilder().Returning(p => p);

    private static D3 Delete3Lambda(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3).CreateDeleteJoinBuilder().Returning(p => p);

    private static D8 Delete8Lambda(IDataContext ctx) => ctx.From<IdnEntity>()
        .Join(ctx.From<IdnEntity>(), (a, b) => a.Id == 1 && b.Id == 2)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 3)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 4)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 5)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 6)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 7)
        .Join(ctx.From<IdnEntity>(), (p, c) => c.Id == 8).CreateDeleteJoinBuilder().Returning(p => p);

    private static void RunUpdateIdentity(IDataContext ctx, int arity)
    {
        switch (arity)
        {
            case 2: AssertUpdateIdentity(Update2(ctx), 2); break;
            case 3: AssertUpdateIdentity(Update3(ctx), 3); break;
            case 4: AssertUpdateIdentity(Update4(ctx), 4); break;
            case 5: AssertUpdateIdentity(Update5(ctx), 5); break;
            case 6: AssertUpdateIdentity(Update6(ctx), 6); break;
            case 7: AssertUpdateIdentity(Update7(ctx), 7); break;
            case 8: AssertUpdateIdentity(Update8(ctx), 8); break;
            default: throw new ArgumentOutOfRangeException(nameof(arity));
        }
    }

    private static void RunDeleteIdentity(IDataContext ctx, int arity)
    {
        switch (arity)
        {
            case 2: AssertDeleteIdentity(Delete2(ctx), 2); break;
            case 3: AssertDeleteIdentity(Delete3(ctx), 3); break;
            case 4: AssertDeleteIdentity(Delete4(ctx), 4); break;
            case 5: AssertDeleteIdentity(Delete5(ctx), 5); break;
            case 6: AssertDeleteIdentity(Delete6(ctx), 6); break;
            case 7: AssertDeleteIdentity(Delete7(ctx), 7); break;
            case 8: AssertDeleteIdentity(Delete8(ctx), 8); break;
            default: throw new ArgumentOutOfRangeException(nameof(arity));
        }
    }

    private static void AssertCteUpdateIdentity<TProjection>(IDataContext ctx, UpdateJoinReturningBuilder<TProjection, TProjection> builder, int arity)
    {
        var scope = ctx.With("upd", builder);

        var rows = scope.From("upd").Select(r => r).ToList();

        rows.Should().ContainSingle();
        AssertUpdateRow((object)rows[0]!, arity);
    }

    private static void AssertCteDeleteIdentity<TProjection>(IDataContext ctx, DeleteJoinReturningBuilder<TProjection, TProjection> builder, int arity)
    {
        var scope = ctx.With("del", builder);

        var rows = scope.From("del").Select(r => r).ToList();

        rows.Should().ContainSingle();
        AssertDeleteRow((object)rows[0]!, arity);
    }

    private static void AssertUpdateIdentity<TProjection>(UpdateJoinReturningBuilder<TProjection, TProjection> builder, int arity)
    {
        AssertUniqueSlotAliases(builder.ToSql(), arity, "id", "name", "age");

        var rows = builder.ToList();

        rows.Should().ContainSingle();
        AssertUpdateRow((object)rows[0]!, arity);
    }

    private static void AssertDeleteIdentity<TProjection>(DeleteJoinReturningBuilder<TProjection, TProjection> builder, int arity)
    {
        AssertUniqueSlotAliases(builder.ToSql(), arity, "id", "name", "age");

        var rows = builder.ToList();

        rows.Should().ContainSingle();
        AssertDeleteRow((object)rows[0]!, arity);
    }

    private static void AssertUpdateRow(object row, int arity)
    {
        // Item1 is the target and carries the post-update values (its name was set from Item2).
        ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
        ((string?)Value(Item(row, 1), "Name")).Should().Be("n2");
        ((int)Value(Item(row, 1), "Age")!).Should().Be(10);

        var ids = new List<int>(arity) { (int)Value(Item(row, 1), "Id")! };
        for (var slot = 2; slot <= arity; slot++)
        {
            var item = Item(row, slot);
            ((int)Value(item, "Id")!).Should().Be(slot);
            ((string?)Value(item, "Name")).Should().Be($"n{slot}");
            ((int)Value(item, "Age")!).Should().Be(slot * 10);
            ids.Add((int)Value(item, "Id")!);
        }

        ids.Should().OnlyHaveUniqueItems();
        ids.Should().Equal(Enumerable.Range(1, arity));
    }

    private static void AssertDeleteRow(object row, int arity)
    {
        // Item1 is the deleted target and carries its original values.
        ((int)Value(Item(row, 1), "Id")!).Should().Be(1);
        ((string?)Value(Item(row, 1), "Name")).Should().Be("n1");
        ((int)Value(Item(row, 1), "Age")!).Should().Be(10);

        for (var slot = 2; slot <= arity; slot++)
        {
            var item = Item(row, slot);
            ((int)Value(item, "Id")!).Should().Be(slot);
            ((string?)Value(item, "Name")).Should().Be($"n{slot}");
            ((int)Value(item, "Age")!).Should().Be(slot * 10);
        }
    }

    private static void AssertUniqueSlotAliases(string sql, int arity, params string[] columns)
    {
        for (var slot = 1; slot <= arity; slot++)
            foreach (var column in columns)
                sql.Should().Contain($"\"__s{slot}_{column}\"");

        var aliases = Regex.Matches(sql, "as\\s+\"(__s\\d+_[a-z0-9_]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        aliases.Should().HaveCount(arity * columns.Length);
        aliases.Should().OnlyHaveUniqueItems();
    }

    private static object Item(object projection, int slot)
        => projection.GetType().GetProperty("Item" + slot)!.GetValue(projection)!;

    private static object? Value(object item, string name)
        => item.GetType().GetProperty(name)!.GetValue(item);

    // --- schema and seeding ---

    private static void CreateIdnEntity(IDataContext ctx)
    {
        Execute(ctx, "drop table if exists idn_entity");
        Execute(ctx, "create table idn_entity (id integer primary key, name varchar(50), age integer)");
        for (var i = 1; i <= 8; i++)
            ctx.InsertInto<IdnEntity>().Value(x => x.Id, i).Value(x => x.Name, $"n{i}").Value(x => x.Age, i * 10).Insert();
    }

    private static void CreateAlphaBeta(IDataContext ctx)
    {
        Execute(ctx, "drop table if exists idn_alpha");
        Execute(ctx, "drop table if exists idn_beta");
        Execute(ctx, "create table idn_alpha (id integer primary key, name varchar(50), age integer)");
        Execute(ctx, "create table idn_beta (id integer primary key, name varchar(50), age integer)");
        for (var i = 1; i <= 8; i++)
        {
            ctx.InsertInto<IdnAlpha>().Value(x => x.Id, i).Value(x => x.Name, $"a{i}").Value(x => x.Age, i * 10).Insert();
            ctx.InsertInto<IdnBeta>().Value(x => x.Id, i).Value(x => x.Name, $"b{i}").Value(x => x.Age, i * 10).Insert();
        }
    }

    private static void DropAlphaBeta(IDataContext ctx)
    {
        Execute(ctx, "drop table if exists idn_alpha");
        Execute(ctx, "drop table if exists idn_beta");
    }

    private static void CreateMapped(IDataContext ctx)
    {
        Execute(ctx, "drop table if exists idn_mapped");
        Execute(ctx, "create table idn_mapped (id integer primary key, phys_code integer, phys_label varchar(50), phys_amount integer)");
        ctx.InsertInto<IdnMapped>().Value(x => x.Id, 1).Value(x => x.Code, 100).Value(x => x.Label, "one").Value(x => x.Amount, 15).Insert();
        ctx.InsertInto<IdnMapped>().Value(x => x.Id, 2).Value(x => x.Code, 200).Value(x => x.Label, "two").Value(x => x.Amount, 25).Insert();
    }

    private static void CreateAliasCollision(IDataContext ctx)
    {
        Execute(ctx, "drop table if exists idn_alias_collision");
        Execute(ctx, "create table idn_alias_collision (id integer primary key, id_2 integer, name varchar(50))");
        Execute(ctx, "insert into idn_alias_collision (id, id_2, name) values (1, 11, 'one'), (2, 22, 'two')");
    }

    private static void AssertAliasCollisionSlot(object row, int slot, int id, int secondary, string? name)
    {
        var item = Item(row, slot);
        ((int)Value(item, "Id")!).Should().Be(id);
        ((int)Value(item, "IdTwin")!).Should().Be(id);
        ((int)Value(item, "Secondary")!).Should().Be(secondary);
        ((string?)Value(item, "Name")).Should().Be(name);
    }

    private static void CreateConverted(IDataContext ctx)
    {
        Execute(ctx, "drop table if exists idn_converted");
        Execute(ctx, "create table idn_converted (id integer primary key, state text, span interval, span_sec interval)");
        ctx.InsertInto<IdnConverted>()
            .Value(x => x.Id, 1)
            .Value(x => x.State, IdnState.Active)
            .Value(x => x.Span, TimeSpan.FromSeconds(10))
            .Value(x => x.SpanSeconds, TimeSpan.FromSeconds(90))
            .Insert();
        ctx.InsertInto<IdnConverted>()
            .Value(x => x.Id, 2)
            .Value(x => x.State, IdnState.Closed)
            .Value(x => x.Span, TimeSpan.FromSeconds(20))
            .Value(x => x.SpanSeconds, TimeSpan.FromSeconds(30))
            .Insert();
    }

    // The raw-command helper mirrors PostgresSpecificTests.Execute.
    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    // The SQL-of-command helper mirrors DynamicColumnsMariaDbContainerTests.SqlOf; the SQL-generation
    // tests use the same prepared-command mechanism.
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");
}

public enum IdnState
{
    Unknown,
    Active,
    Closed,
}

[SqlTable("idn_entity")]
internal sealed class IdnEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("age")]
    public int Age { get; set; }
}

[SqlTable("idn_alpha")]
internal sealed class IdnAlpha
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("age")]
    public int Age { get; set; }
}

[SqlTable("idn_beta")]
internal sealed class IdnBeta
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("age")]
    public int Age { get; set; }
}

[SqlTable("idn_mapped")]
internal sealed class IdnMapped
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("phys_code")]
    public int Code { get; set; }

    [Column("phys_label")]
    public string? Label { get; set; }

    [Column("phys_amount")]
    public int Amount { get; set; }
}

// Mirrors the AliasCollisionEntity fixture used by the SQL-generation tests: metadata maps two
// properties onto the physical column "id" and one onto the literal alias-shaped column "id_2",
// whose base collides with the disambiguated alias of the repeated column.
[SqlTable("idn_alias_collision")]
internal sealed class IdnAliasCollision
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id")]
    public int IdTwin { get; set; }

    [Column("id_2")]
    public int Secondary { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("idn_converted")]
internal sealed class IdnConverted
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("state")]
    [ValueConverter(typeof(EnumToStringConverter<IdnState>))]
    public IdnState State { get; set; }

    [Column("span")]
    public TimeSpan Span { get; set; }

    [Column("span_sec")]
    [Duration(DurationUnit.Seconds)]
    public TimeSpan SpanSeconds { get; set; }
}

// Metadata-less derived/read shape: never registered with From<T>/Join<T>, so the identity expansion
// falls back to its readable CLR surface (mirrors the SQL-generation fixture IdentityDerivedShape).
internal sealed class IdnDerivedShape
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int Age { get; set; }
}

internal sealed class IdnReturningDto
{
    public IdnReturningDto()
    {
    }

    public IdnReturningDto(int targetId, string? sourceName)
    {
        TargetId = targetId;
        SourceName = sourceName;
    }

    public int TargetId { get; set; }

    public string? SourceName { get; set; }
}
