using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T306 (#306): a single-source mapped-entity identity projection must render the same statement as the
/// unchanged <c>ToCommand()</c> control and materialize the same rows from a live SQLite database. The
/// pre-fix statement (<c>selec ...</c>) is rejected by SQLite with a syntax error, so this test fails
/// until the projection preparation returns the entity's mapped columns.
/// </summary>
[Collection("Sqlite")]
public sealed class T306IdentityProjectionIntegrationTests : ProviderTestSuite
{
    private const string Ddl = """
        drop table if exists entity_parent;
        create table entity_parent
        (
            id integer primary key,
            name text not null
        );
        insert into entity_parent (id, name) values
            (1, 'p1'),
            (2, 'p2');
        """;

    public T306IdentityProjectionIntegrationTests()
    {
        var dataContext = (DataContext)_sut.DataProvider;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(Ddl);
        command.ExecuteNonQuery();
    }

    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void IdentityProjection_ShouldMaterializeTheSameRowsAsTheControl()
    {
        var identity = _sut.DataProvider.From<T306EntityParent>()
            .OrderBy(x => x.Id)
            .Select(x => x)
            .ToList();

        var control = _sut.DataProvider.From<T306EntityParent>()
            .OrderBy(x => x.Id)
            .ToList();

        identity.Should().HaveCount(2);
        identity.Select(x => x.Id).Should().Equal(1, 2);
        identity.Select(x => x.Name).Should().Equal("p1", "p2");
        identity.Select(x => x.Id).Should().Equal(control.Select(x => x.Id));
        identity.Select(x => x.Name).Should().Equal(control.Select(x => x.Name));
    }
}

/// <summary>The mapped single source the identity projection reads (columns <c>id</c>, <c>name</c>).</summary>
[SqlTable("entity_parent")]
public sealed class T306EntityParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
