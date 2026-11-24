using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

public sealed class EmptyTempTableTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_QuotesIdentifiersAndDeclaresTypesAndKey(bool postgres)
    {
        ISqlDialect dialect = postgres ? PostgresDialect.Instance : SqliteDialect.Instance;
        EmptyTempTableSql.Build(dialect, KeywordCase.Lower, "a.\"b", [("id", typeof(string)), ("qty", typeof(int))], "id", !postgres)
            .Should().Be("create temp table \"a.\"\"b\" (\"id\" text not null primary key, \"qty\" integer)" + (postgres ? "" : " without rowid"));
    }

    [Fact]
    public void Postgres_RefusesWithoutRowIdRatherThanSilentlyDroppingIt()
    {
        var create = () => EmptyTempTableSql.Build(PostgresDialect.Instance, KeywordCase.Lower, "keys", [("id", typeof(string))], "id", true);
        create.Should().Throw<NotSupportedException>().WithMessage("*WITHOUT ROWID*SQLite*");
    }

    [Fact]
    public void Create_RefusesUndeclaredKey()
    {
        var create = () => EmptyTempTableSql.Build(SqliteDialect.Instance, KeywordCase.Lower, "keys", [("id", typeof(string))], "missing", false);
        create.Should().Throw<ArgumentException>().WithParameterName("primaryKey");
    }
}
