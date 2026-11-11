using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// D4g is scoped to the MySQL family: a dialect whose string-literal policy is not the MySQL policy
/// keeps the pre-D4g mutation SQL byte for byte, with every value bound as a parameter. No database is
/// touched; only the rendered text is inspected.
/// </summary>
public class MutationTextLoweringSqlGenerationTests
{
    [Fact]
    public void Insert_TextValue_ShouldKeepTheParameterPlaceholder()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.CreateInsertBuilder<IInsertEntity>()
            .Values(new InsertEntity { Name = "a", Age = 5 })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age, description) values ($p0, $p1, $p2)",
                "a non-MySQL-family dialect is byte-identical to the pre-D4g rendering");
    }

    [Fact]
    public void Update_TextValue_ShouldKeepTheParameterPlaceholder()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.CreateUpdateBuilder<IInsertEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update insert_entity set name = $p0 where id = 1");
    }
}
