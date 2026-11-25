using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Core.Repositories;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed class NotInSqliteTests : IDisposable
{
    [Table("not_in_row")]
    private sealed class Row
    {
        public int Id { get; set; }
        public int? Value { get; set; }
    }

    private sealed class RowRepositories(IDataContext context) : RepositoryContext(context)
    {
        internal IRepository<Row> Rows => Repository<Row>();
    }

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SqliteDataContext _context;

    public NotInSqliteTests()
    {
        _connection.Open();
        using var setup = _connection.CreateCommand();
        setup.CommandText = """
            create table not_in_row (id integer primary key, value integer);
            insert into not_in_row (id, value) values (1, null), (2, 10), (3, 20), (4, 30);
            """;
        setup.ExecuteNonQuery();
        _context = new SqliteDataContext(_connection, new DataContextBuilder());
    }

    [Fact]
    public void CapturedList_ShouldUseTheCurrentValuesAndShape()
    {
        var excluded = new List<int> { 1, 3 };
        ReadIds(excluded).Should().Equal(2, 4);
        excluded[0] = 2;
        ReadIds(excluded).Should().Equal(1, 4);
        excluded.RemoveAt(1);
        ReadIds(excluded).Should().Equal(1, 3, 4);
        excluded.Clear();
        ReadIds(excluded).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void ListWithNull_ShouldExcludeNullAndMatchingValues()
    {
        _context.From<Row>()
            .Where(row => SqlFunctions.Sql.not_in<int?>(row.Value, null, 20))
            .OrderBy(row => row.Id).Select(row => row.Id).ToList()
            .Should().Equal(2, 4);

        _context.From<Row>()
            .Where(row => SqlFunctions.Sql.not_in(row.Value, (int?)null))
            .OrderBy(row => row.Id).Select(row => row.Id).ToList()
            .Should().Equal(2, 3, 4);
    }

    [Fact]
    public void Subquery_ShouldUseSqlNullSemantics()
    {
        var source = _context.From<Row>();
        var excluded = source.Where(row => row.Id < 3).Select(row => row.Id);
        source.Where(row => SqlFunctions.Sql.not_in(row.Id, excluded))
            .OrderBy(row => row.Id).Select(row => row.Id).ToList()
            .Should().Equal(3, 4);

        var nullableValues = source.Where(row => row.Id == 1).Select(row => row.Value);
        source.Where(row => SqlFunctions.Sql.not_in((int?)row.Id, nullableValues))
            .Select(row => row.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void RepositoryQueries_ShouldSupplyMembershipSubqueries()
    {
        var app = new RowRepositories(_context);
        var ids = app.Rows.Select(row => row.Id);

        _context.From<Row>()
            .Where(row => SqlFunctions.Sql.@in(row.Id, ids))
            .OrderBy(row => row.Id).Select(row => row.Id).ToList()
            .Should().Equal(1, 2, 3, 4);

        _context.From<Row>()
            .Where(row => SqlFunctions.Sql.not_in(row.Id, app.Rows
                .Where(candidate => candidate.Id == row.Id && candidate.Id > 2)
                .Select(candidate => candidate.Id)))
            .OrderBy(row => row.Id).Select(row => row.Id).ToList()
            .Should().Equal(1, 2);
    }

    [Fact]
    public void TupleList_ShouldExcludeOnlyMatchingTuples()
    {
        var excluded = new (int Id, int? Value)[] { (1, null), (3, 20), (4, 40) };
        _context.From<Row>()
            .Where(row => SqlFunctions.Sql.not_in(new ValueTuple<int, int?>(row.Id, row.Value), excluded))
            .OrderBy(row => row.Id).Select(row => row.Id).ToList()
            .Should().Equal(2, 4);
    }

    private List<int> ReadIds(IEnumerable<int> excluded) => _context.From<Row>()
        .Where(row => SqlFunctions.Sql.not_in(row.Id, excluded))
        .OrderBy(row => row.Id).Select(row => row.Id).ToList();

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
