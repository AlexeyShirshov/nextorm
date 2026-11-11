using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T213 candidate 3 (r=5): the mixed mapped-entity + scalar projection admission is removed. This is the
/// end-to-end public proof that the shape has no working terminal: an ordinary
/// <c>EntityBuilder.Join(EntityBuilder&lt;scalar&gt;, predicate)</c> whole-projection of a mapped entity
/// plus a scalar previously rendered an incomplete SELECT list and an unterminated join predicate (the
/// scalar item column emitted as <c>Item2.</c>, the join ON truncated) — invalid SQL. With the admission
/// removed the same public query fails closed deterministically at preparation. The surviving consumer of
/// that shape, the composite-junction many-to-many SQL link source, is carried by #223 and already fails
/// closed.
/// </summary>
public class JoinIntoMixedProjectionPublicTests
{
    [Fact]
    public void MixedProjection_EntityPlusScalar_ThroughPublicJoin_ShouldFailClosed()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var scalarIds = ctx.From<MpScalar>().Select(s => s.Id);
        var builder = ctx.From<MpJunction>().Join(ctx.From(scalarIds), (j, s) => j.Id == s);

        Action act = () => builder.ToList();

        act.Should().Throw<QueryPreparationException>(
            "a mapped entity plus scalar whole-projection has no working public terminal and must fail closed");
    }

    [Fact]
    public void MixedProjection_EntityPlusNullableScalar_ThroughPublicJoin_ShouldFailClosed()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var scalarValues = ctx.From<MpScalar>().Select(s => (int?)s.Id);
        var builder = ctx.From<MpJunction>().Join(ctx.From(scalarValues), (j, s) => j.Id == s);

        Action act = () => builder.ToList();

        act.Should().Throw<QueryPreparationException>(
            "a nullable scalar item beside a mapped entity must fail closed");
    }
}

[SqlTable("mp_junction")]
public sealed class MpJunction
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

[SqlTable("mp_scalar")]
public sealed class MpScalar
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}
