using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T356 (#347): an identity read of a single-column derived source (<c>From(inner).Select(x => x)</c>)
/// must never resolve to a zero-column projection, whatever the process-wide caches already hold.
/// Two independent state shapes used to break it: an auto-registered (property-less) metadata entry
/// for the scalar CLR type, which the identity arm mistook for a mapped entity and turned into an
/// EMPTY select list cached under that type, and an EMPTY select-list cache entry itself, which the
/// arm read as the authoritative column shape. Both are asserted here deterministically — no suite
/// ordering is involved — by constructing the state through the public API on one context.
/// </summary>
/// <remarks>
/// Joins the serialized "DataContextCache clear" collection: both tests seed and restore entries in
/// the process-wide <see cref="DataContextCache"/>.
/// </remarks>
[Collection("DataContextCache clear")]
public class T356ScalarIdentityCacheTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static QueryCommand<int> ScalarSource(IDataContext ctx)
        => ctx.From<ISimpleEntity>().Where(x => x.Id <= 2).Select(x => x.Id);

    private static void ClearScalarState()
    {
        DataContextCache.SelectListCache.Remove(typeof(int));
        DataContextCache.Metadata.Remove(typeof(int));
    }

    /// <summary>
    /// AC1: a property-less metadata entry auto-registered for a scalar CLR type (the raw/bind path
    /// registers any result type) is not a mapped entity. The identity read of a scalar derived source
    /// still prepares its one real column and leaves no entity-projection entry under
    /// <c>typeof(int)</c> — warm and cold behave identically.
    /// </summary>
    [Fact]
    public void ScalarMetadataEntry_DoesNotProduceColumnlessIdentityRead()
    {
        ClearScalarState();
        try
        {
            using var ctx = SqliteTestContext.Create();

            // Public-API seeding, exactly what an earlier raw/bind command does in a long-lived
            // process: registering the mapping of the *scalar* result type.
            _ = ctx.From<int>();
            DataContextCache.Metadata.ContainsKey(typeof(int))
                .Should().BeTrue("the seeding step must really register the scalar metadata entry");

            var sql = SqlOf(ctx, ctx.From(ScalarSource(ctx)).Select(x => x));

            sql.Should().Contain("select", "the identity read must render a real projection");
            sql.Should().Contain("id", "the derived source's only column is the projection");
            DataContextCache.SelectListCache.TryGetValue(typeof(int), out var cached);
            cached.Should().BeNull("a scalar CLR type must never receive an entity-projection entry");
        }
        finally
        {
            ClearScalarState();
        }
    }

    /// <summary>
    /// AC2: an EMPTY select-list cache entry is never a usable column shape. A pre-seeded empty entry
    /// for a genuinely mapped entity is treated as a cache miss, so the identity read rebuilds the
    /// entity's columns instead of rendering an empty select list.
    /// </summary>
    [Fact]
    public void EmptyCachedEntityColumns_AreRebuiltAsACacheMiss()
    {
        DataContextCache.SelectListCache[typeof(SimpleEntity)] = [];
        try
        {
            using var ctx = SqliteTestContext.Create();

            var sql = SqlOf(ctx, ctx.From<SimpleEntity>().Select(x => x));

            sql.Should().Contain("select");
            sql.Should().Contain("id", "an empty entry is a miss, so the entity columns are rebuilt");
        }
        finally
        {
            DataContextCache.SelectListCache.Remove(typeof(SimpleEntity));
        }
    }
}
