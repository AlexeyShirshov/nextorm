using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class InterfaceEntitySqliteTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    public InterfaceEntitySqliteTests()
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.GetConnection().CreateCommand();
        command.CommandText = """
            create table if not exists d34_main (entity_id integer primary key, label text null, note text null);
            create table if not exists d34_other (id integer primary key);
            delete from d34_main;
            delete from d34_other;
            insert into d34_main (entity_id, label, note) values (1, 'first', 'kept'), (2, null, null);
            insert into d34_other (id) values (1);
            """;
        command.ExecuteNonQuery();
    }

    [Fact]
    public void DirectAndIdentitySelectMatchConcreteRows()
    {
        var expected = Context.From<D34Concrete>().OrderBy(x => x.Id).ToList();
        var direct = Context.From<ID34Main>().OrderBy(x => x.Id).ToList();
        var identity = Context.From<ID34Main>().OrderBy(x => x.Id).Select(x => x).ToList();

        direct.Select(x => (x.Id, x.Label, x.Note)).Should().Equal(expected.Select(x => (x.Id, x.Label, x.Note)));
        identity.Select(x => (x.Id, x.Label, x.Note)).Should().Equal(expected.Select(x => (x.Id, x.Label, x.Note)));
    }

    [Fact]
    public void CompositeProjectionKeepsBothInterfacesAndMissingOuterSide()
    {
        var rows = Context.From<ID34Main>().LeftJoin(Context.From<ID34Other>(), (a, b) => a.Id == b.Id)
            .OrderBy(x => x.Item1.Id).Select(x => new { x.Item1, x.Item2 }).ToList();

        rows.Select(x => x.Item1.Id).Should().Equal(1, 2);
        rows[0].Item2.Id.Should().Be(1);
        rows[1].Item2.Should().BeNull();
    }

    [Fact]
    public void InsertReturningMaterializesTheDeclaredInterface()
    {
        var row = Context.CreateInsertBuilder<ID34Main>().Value(x => x.Label, "inserted")
            .Returning().ToList().Should().ContainSingle().Which;

        row.Label.Should().Be("inserted");
        row.Note.Should().BeNull();
        row.Id.Should().BeGreaterThan(2);
        var insertedId = row.Id;
        Context.From<ID34Main>().Where(x => x.Id == insertedId).Single().Label.Should().Be("inserted");
    }

    [Fact]
    public void RawCommandBindsInheritedAndInitPropertiesByNameInReaderOrder()
    {
        using var result = Context.ExecuteRaw("select note, entity_id, label from d34_main order by entity_id");
        var rows = result.Read<ID34Main>();

        rows.Select(x => (x.Id, x.Label, x.Note)).Should()
            .Equal((1, "first", "kept"), (2, null, null));
    }
}

public interface ID34Key
{
    [Key, Column("entity_id"), DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    int Id { get; }
}

[SqlTable("d34_main")]
internal interface ID34Main : ID34Key
{
    [Column("label")]
    string? Label { get; set; }

    [Column("note")]
    string? Note { get; init; }
}

[SqlTable("d34_other")]
internal interface ID34Other
{
    [Column("id")]
    int Id { get; }
}

[SqlTable("d34_main")]
internal sealed class D34Concrete
{
    [Column("entity_id")]
    public int Id { get; set; }

    [Column("label")]
    public string? Label { get; set; }

    [Column("note")]
    public string? Note { get; init; }
}
