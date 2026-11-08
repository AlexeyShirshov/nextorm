using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Provider-independent guards of the multi-table returning builders: the null-selector checks fired by
/// the public factory extensions and the non-database-backed execution gate. Native SQL generation is
/// covered by the provider test projects; no database is involved here.
/// </summary>
public class JoinReturningBuilderTests
{
    [Fact]
    public void DeleteJoinReturning_NullProjection_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        Expression<Func<Projection<ConventionalEntity, ConventionalEntity>, int>> projection = null!;

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .CreateDeleteJoinBuilder()
            .Returning(projection);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateDeleteJoinBuilder_NullReceiver_ShouldThrow()
    {
        JoinedEntityBuilder<ConventionalEntity, ConventionalEntity> query = null!;

        var act = () => query.CreateDeleteJoinBuilder();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateJoinReturning_NullProjection_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        Expression<Func<Projection<ConventionalEntity, ConventionalEntity>, int>> projection = null!;

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Returning(projection);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DeleteJoinReturning_IdentityProjection_ShouldBeAccepted()
    {
        using var ctx = new InMemoryDataContext();

        var builder = ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .CreateDeleteJoinBuilder()
            .Returning(p => p);

        builder.Should().NotBeNull();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .CreateDeleteJoinBuilder()
            .Returning();

        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateJoinReturning_IdentityProjection_ShouldBeAccepted()
    {
        using var ctx = new InMemoryDataContext();

        var builder = ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Returning(p => p);

        builder.Should().NotBeNull();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Returning();

        act.Should().NotThrow();
    }

    [Fact]
    public void DeleteJoinReturning_IdentityProjection_Arity3_ShouldParse()
    {
        using var ctx = new InMemoryDataContext();

        var builder = ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Item1.Id == b.Id)
            .CreateDeleteJoinBuilder()
            .Returning();

        builder.Should().NotBeNull();
    }

    [Fact]
    public void UpdateJoinReturning_IdentityProjection_Arity3_ShouldParse()
    {
        using var ctx = new InMemoryDataContext();

        var builder = ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Item1.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Returning();

        builder.Should().NotBeNull();
    }

    [Fact]
    public void InMemory_UpdateJoinReturning_ToList_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Name, "a")
            .Returning(p => new { p.Item1.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support returning updated rows*");
    }

    [Fact]
    public void InMemory_DeleteJoinReturning_ToList_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .CreateDeleteJoinBuilder()
            .Returning(p => new { p.Item1.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support returning removed rows*");
    }

    [Fact]
    public void InMemory_UpdateJoinReturning_ToSql_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Name, "a")
            .Returning(p => new { p.Item1.Id })
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot render SQL*");
    }

    [Fact]
    public void InMemory_DeleteJoinReturning_ToSql_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .CreateDeleteJoinBuilder()
            .Returning(p => new { p.Item1.Id })
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot render SQL*");
    }

    [Fact]
    public void UpdateJoinReturning_ScalarAnonymousCtorAndMemberInit_ShouldParse()
    {
        using var ctx = new InMemoryDataContext();

        var act = () =>
        {
            ctx.From<ConventionalEntity>()
                .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
                .CreateUpdateJoinBuilder()
                .Returning(p => p.Item1.Id);

            ctx.From<ConventionalEntity>()
                .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
                .CreateUpdateJoinBuilder()
                .Returning(p => new { TargetId = p.Item1.Id, SourceId = p.Item2.Id });

            ctx.From<ConventionalEntity>()
                .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
                .CreateUpdateJoinBuilder()
                .Returning(p => new JoinedReturningDto(p.Item1.Id, p.Item2.Id));

            ctx.From<ConventionalEntity>()
                .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
                .CreateUpdateJoinBuilder()
                .Returning(p => new JoinedReturningDto { TargetId = p.Item1.Id, SourceId = p.Item2.Id });
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void DeleteJoinReturning_ScalarCtorAndMemberInit_ShouldParse()
    {
        using var ctx = new InMemoryDataContext();

        var act = () =>
        {
            ctx.From<ConventionalEntity>()
                .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
                .CreateDeleteJoinBuilder()
                .Returning(p => p.Item1.Id);

            ctx.From<ConventionalEntity>()
                .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
                .CreateDeleteJoinBuilder()
                .Returning(p => new JoinedReturningDto(p.Item1.Id, p.Item2.Id));

            ctx.From<ConventionalEntity>()
                .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
                .CreateDeleteJoinBuilder()
                .Returning(p => new JoinedReturningDto { TargetId = p.Item1.Id, SourceId = p.Item2.Id });
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void JoinedReturningProjection_CastMember_ShouldParse()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionalEntity>();

        // A cast over a mapped member is unwrapped and accepted; the walker still visits its UnaryExpression.
        Expression<Func<Projection<ConventionalEntity, ConventionalEntity>, long>> projection = p => (long)p.Item1.Id;

        var act = () => JoinedReturningProjection.Parse(projection);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("binary")]
    [InlineData("conditional")]
    [InlineData("method")]
    [InlineData("array")]
    [InlineData("index")]
    [InlineData("invocation")]
    public void JoinedReturningProjection_WalkerShapes_ShouldRejectNonMemberProjection(string shape)
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionalEntity>(); // register the real-entity slot metadata

        LambdaExpression projection = shape switch
        {
            "binary" => (Expression<Func<Projection<ConventionalEntity, ConventionalEntity>, int>>)(p => p.Item1.Id + p.Item2.Id),
            "conditional" => (Expression<Func<Projection<ConventionalEntity, ConventionalEntity>, int>>)(p => p.Item1.Id > 0 ? p.Item1.Id : p.Item2.Id),
            "method" => (Expression<Func<Projection<ConventionalEntity, ConventionalEntity>, string>>)(p => p.Item1.Id.ToString()),
            "array" => (Expression<Func<Projection<ConventionalEntity, ConventionalEntity>, int[]>>)(p => new[] { p.Item1.Id, p.Item2.Id }),
            "index" => (Expression<Func<Projection<ConventionalEntity, ConventionalEntity>, char>>)(p => p.Item1.Name![0]),
            "invocation" => (Expression<Func<Projection<ConventionalEntity, ConventionalEntity>, int>>)(p => ((Func<int>)(() => p.Item1.Id))()),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
        };

        var act = () => JoinedReturningProjection.Parse(projection);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void JoinedReturningProjection_UnresolvableReferencedSlot_ShouldThrowQueryPreparationException()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionalEntity>(); // register the real-entity slot metadata

        // The second slot is neither a registered entity nor a reachable shape (it has no readable public
        // column), so the referenced member cannot be resolved at all.
        Expression<Func<Projection<ConventionalEntity, OpaqueSlot>, object>> projection =
            p => new { p.Item1.Id, Slot = p.Item2 };

        var act = () => JoinedReturningProjection.Parse(projection);

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*cannot resolve its referenced members*");
    }

    internal sealed class JoinedReturningDto
    {
        public JoinedReturningDto()
        {
        }

        public JoinedReturningDto(int targetId, int sourceId)
        {
            TargetId = targetId;
            SourceId = sourceId;
        }

        public int TargetId { get; set; }

        public int SourceId { get; set; }
    }

    internal sealed class OpaqueSlot
    {
        internal int Hidden { get; set; }
    }
}
