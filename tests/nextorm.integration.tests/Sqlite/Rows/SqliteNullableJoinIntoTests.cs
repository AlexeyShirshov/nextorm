using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteNullableJoinIntoTests : IDisposable
{
    private readonly SqliteTestProvider.IsolatedSqliteDatabase _database = SqliteTestProvider.CreateIsolatedDatabase();
    private readonly SqliteDataContext _db;

    public SqliteNullableJoinIntoTests()
    {
        _db = new SqliteDataContext(string.Format(SqliteTestProvider.ConnectionString, _database.DatabasePath),
            new DataContextBuilder());
        using var setup = _db.ExecuteRaw("""
            create table nullable_join_parent (Id integer primary key, Tenant integer not null, Link integer);
            create table nullable_join_child (Id integer primary key, Tenant integer, ParentLink integer not null);
            insert into nullable_join_parent values (1, 10, 20), (2, 11, 20), (3, 10, null);
            insert into nullable_join_child values (101, 10, 20), (102, 10, 20), (103, 11, 20), (104, null, 20), (105, 10, 21);
            """);
    }

    [Fact]
    public void SingleQueryLoadWith_CompositeNullableKeys_ShouldMatchEveryComponent()
    {
        var rows = _db.From<Parent>()
            .OrderBy(x => x.Id)
            .LoadWith(x => x.Children, EagerLoadMode.SingleQuery)
            .ToList();

        rows.Select(x => x.Id).Should().Equal(1, 2, 3);
        rows[0].Children.Select(x => x.Id).Should().BeEquivalentTo([101, 102]);
        rows[1].Children.Select(x => x.Id).Should().Equal(103);
        rows[2].Children.Should().BeEmpty();
    }

    [Fact]
    public async Task SingleQueryLoadWith_FilteredNullableKeys_ShouldRetainParentsOnAReboundQuery()
    {
        var minimum = 102;
        var query = _db.From<Parent>()
            .OrderBy(x => x.Id)
            .LoadWith(x => x.Children, ctx => ctx.From<Child>().Where(x => x.Id >= minimum), EagerLoadMode.SingleQuery);
        var token = TestContext.Current.CancellationToken;

        var rows = await query.ToListAsync(token);

        rows.Select(x => x.Id).Should().Equal(1, 2, 3);
        rows[0].Children.Select(x => x.Id).Should().Equal(102);
        rows[1].Children.Select(x => x.Id).Should().Equal(103);
        rows[2].Children.Should().BeEmpty();

        minimum = 103;
        rows = await query.ToListAsync(token);

        rows.Select(x => x.Id).Should().Equal(1, 2, 3);
        rows[0].Children.Should().BeEmpty();
        rows[1].Children.Select(x => x.Id).Should().Equal(103);
        rows[2].Children.Should().BeEmpty();
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    [SqlTable("nullable_join_parent")]
    public sealed class Parent
    {
        [Key]
        public int Id { get; set; }
        public int Tenant { get; set; }
        public int? Link { get; set; }

        [Relationship(ForeignKeys = new[] { nameof(Child.Tenant), nameof(Child.ParentLink) },
            PrincipalKeys = new[] { nameof(Tenant), nameof(Link) })]
        public ICollection<Child> Children { get; set; } = new List<Child>();
    }

    [SqlTable("nullable_join_child")]
    public sealed class Child
    {
        [Key]
        public int Id { get; set; }
        public int? Tenant { get; set; }
        public int ParentLink { get; set; }
    }
}
