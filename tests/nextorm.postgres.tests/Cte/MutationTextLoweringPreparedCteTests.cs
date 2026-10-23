using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// D4g never touches the prepared render: a data-modifying CTE body is placed inside a prepared
/// <c>SELECT</c>, whose values are transported by the D4 parameter template, so its text values stay
/// bound placeholders (no literal lowering, no <c>_utf8mb4</c> spelling). Placeholder connection only.
/// </summary>
public class MutationTextLoweringPreparedCteTests
{
    public sealed class CteRow
    {
        public long Id { get; set; }
    }

    [Fact]
    public void DataModifyingCteBody_ShouldKeepTheTextParameterPlaceholder()
    {
        using var ctx = PostgresTestContext.Create();
        var value = "a";

        var dml = ctx.With("u", ctx.CreateUpdateBuilder<IMergeEntity>()
                .Set(x => x.Name, value)
                .Where(x => x.Id == 1)
                .Returning(x => new { x.Id }))
            .From("u")
            .Select(r => new CteRow { Id = r.Id });

        var prepared = (DbPreparedQueryCommand<CteRow>)ctx.GetPreparedQueryCommand(dml, false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Should()
            .StartWith("with u as (update merge_entity set name = @")
            .And.NotContain("_utf8mb4", "the prepared CTE keeps the parameter template, never a literal");
        prepared.DbCommand.Parameters.Cast<DbParameter>()
            .Should().ContainSingle(p => (string?)p.Value == "a",
                "the value stays a bound template parameter");
    }

    [Fact]
    public void DataModifyingCteBody_ShouldKeepTheTextKeyPlaceholderInADelete()
    {
        using var ctx = PostgresTestContext.Create();
        var key = "k1";

        var dml = ctx.With("d", ctx.CreateDeleteBuilder<IMergeEntity>()
                .Where(x => x.Name == key)
                .Returning(x => new { x.Id }))
            .From("d")
            .Select(r => new CteRow { Id = r.Id });

        var prepared = (DbPreparedQueryCommand<CteRow>)ctx.GetPreparedQueryCommand(dml, false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Should()
            .StartWith("with d as (delete from merge_entity where ")
            .And.NotContain("_utf8mb4");
        prepared.DbCommand.Parameters.Cast<DbParameter>()
            .Should().Contain(p => (string?)p.Value == "k1");
    }
}
