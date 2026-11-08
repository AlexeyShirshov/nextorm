using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteStep4MutationStateTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s4_mstate_row (id integer primary key autoincrement, name text null, qty int not null default 0);
        delete from s4_mstate_row;
        """;

    public SqliteStep4MutationStateTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    [Fact]
    public void Update_Constant_ShouldPersist()
    {
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "a").Value(x => x.Qty, 1).Insert();
        Context.CreateUpdateBuilder<S4MStateRow>().Set(x => x.Qty, 9).Where(x => x.Name == "a").Update().Should().Be(1);
        Context.From<S4MStateRow>().Where(x => x.Name == "a").Single().Qty.Should().Be(9);
    }

    [Fact]
    public void Update_Expression_ShouldPersist()
    {
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "b").Value(x => x.Qty, 4).Insert();
        Context.CreateUpdateBuilder<S4MStateRow>().Set(x => x.Qty, x => x.Qty + 3).Where(x => x.Name == "b").Update().Should().Be(1);
        Context.From<S4MStateRow>().Where(x => x.Name == "b").Single().Qty.Should().Be(7);
    }

    [Fact]
    public void Update_TwoRows_ShouldPersist()
    {
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "c1").Value(x => x.Qty, 1).Insert();
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "c2").Value(x => x.Qty, 2).Insert();
        Context.CreateUpdateBuilder<S4MStateRow>().Set(x => x.Qty, 0).Update().Should().Be(2);
        Context.From<S4MStateRow>().ToList().Should().OnlyContain(x => x.Qty == 0);
    }

    [Fact]
    public void Update_Returning_ShouldReturnUpdatedRows()
    {
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "d").Value(x => x.Qty, 5).Insert();
        var updated = Context.CreateUpdateBuilder<S4MStateRow>().Set(x => x.Qty, 6).Where(x => x.Name == "d").Returning(x => new { x.Name, x.Qty }).ToList();
        updated.Should().ContainSingle();
        updated[0].Qty.Should().Be(6);
        Context.From<S4MStateRow>().Where(x => x.Name == "d").Single().Qty.Should().Be(6);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersist()
    {
        await Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "e").Value(x => x.Qty, 1).InsertAsync(TestContext.Current.CancellationToken);
        var affected = await Context.CreateUpdateBuilder<S4MStateRow>().Set(x => x.Qty, 2).Where(x => x.Name == "e").UpdateAsync(TestContext.Current.CancellationToken);
        affected.Should().Be(1);
        Context.From<S4MStateRow>().Where(x => x.Name == "e").Single().Qty.Should().Be(2);
    }

    [Fact]
    public void Delete_Where_ShouldRemoveRow()
    {
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "f").Value(x => x.Qty, 1).Insert();
        Context.CreateDeleteBuilder<S4MStateRow>().Where(x => x.Name == "f").Delete().Should().Be(1);
        Context.From<S4MStateRow>().Where(x => x.Name == "f").Count().Should().Be(0);
    }

    [Fact]
    public void Delete_All_ShouldRemoveAllRows()
    {
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "g1").Value(x => x.Qty, 1).Insert();
        Context.CreateDeleteBuilder<S4MStateRow>().All().Delete().Should().Be(1);
        Context.From<S4MStateRow>().Count().Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveRow()
    {
        await Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "h").Value(x => x.Qty, 1).InsertAsync(TestContext.Current.CancellationToken);
        var affected = await Context.CreateDeleteBuilder<S4MStateRow>().Where(x => x.Name == "h").DeleteAsync(TestContext.Current.CancellationToken);
        affected.Should().Be(1);
        Context.From<S4MStateRow>().Where(x => x.Name == "h").Count().Should().Be(0);
    }

    [Fact]
    public void Insert_Returning_ShouldReturnInsertedRows()
    {
        var inserted = Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "i").Value(x => x.Qty, 3).Returning(x => new { x.Id, x.Name }).ToList();
        inserted.Should().ContainSingle();
        inserted[0].Name.Should().Be("i");
        Context.From<S4MStateRow>().Where(x => x.Name == "i").Single().Qty.Should().Be(3);
    }

    [Fact]
    public void Insert_EntityForm_ShouldPersist()
    {
        Context.CreateInsertBuilder<S4MStateRow>().Values(new S4MStateRow { Name = "j", Qty = 4 }).Insert().Should().Be(1);
        Context.From<S4MStateRow>().Where(x => x.Name == "j").Single().Qty.Should().Be(4);
    }

    [Fact]
    public void Insert_TwoSingleRows_ShouldPersist()
    {
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "k1").Value(x => x.Qty, 1).Insert();
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "k2").Value(x => x.Qty, 2).Insert();
        Context.From<S4MStateRow>().Where(x => x.Name!.StartsWith("k")).Count().Should().Be(2);
    }

    [Fact]
    public void Insert_EmptyEntitySequence_ShouldBeRefused()
    {
        var act = () => Context.CreateInsertBuilder<S4MStateRow>().Values(Array.Empty<S4MStateRow>()).Insert();
        act.Should().Throw<ArgumentException>().WithMessage("*At least one entity is required*");
    }

    [Fact]
    public void Insert_ConstantExpression_ShouldPersist()
    {
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "l").Value(x => x.Qty, x => 2 + 3).Insert();
        Context.From<S4MStateRow>().Where(x => x.Name == "l").Single().Qty.Should().Be(5);
    }

    [Fact]
    public void Update_WherePrefix_ShouldPersist()
    {
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "m").Value(x => x.Qty, 1).Insert();
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "m2").Value(x => x.Qty, 2).Insert();
        var affected = Context.CreateUpdateBuilder<S4MStateRow>()
            .Set(x => x.Qty, x => x.Qty + 1)
            .Where(x => x.Name!.StartsWith("m"))
            .Update();
        affected.Should().Be(2);
        Context.From<S4MStateRow>().Where(x => x.Name!.StartsWith("m")).ToList().Should().OnlyContain(x => x.Qty >= 2);
    }

    [Fact]
    public void Delete_Returning_ShouldReturnDeletedRows()
    {
        Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "n").Value(x => x.Qty, 7).Insert();
        var deleted = Context.CreateDeleteBuilder<S4MStateRow>().Where(x => x.Name == "n").Returning(x => x.Name).ToList();
        deleted.Should().ContainSingle();
        deleted[0].Should().Be("n");
        Context.From<S4MStateRow>().Where(x => x.Name == "n").Count().Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_Returning_ShouldReturnDeletedRows()
    {
        await Context.CreateInsertBuilder<S4MStateRow>().Value(x => x.Name, "o").Value(x => x.Qty, 8).InsertAsync(TestContext.Current.CancellationToken);
        var deleted = await Context.CreateDeleteBuilder<S4MStateRow>().Where(x => x.Name == "o").Returning(x => x.Qty).ToListAsync(TestContext.Current.CancellationToken);
        deleted.Should().ContainSingle();
        deleted[0].Should().Be(8);
    }

    [Fact]
    public void Update_NoMatch_ShouldAffectZero()
    {
        Context.CreateUpdateBuilder<S4MStateRow>().Set(x => x.Qty, 1).Where(x => x.Name == "zzz").Update().Should().Be(0);
    }

    [Fact]
    public void Delete_NoMatch_ShouldAffectZero()
    {
        Context.CreateDeleteBuilder<S4MStateRow>().Where(x => x.Name == "zzz").Delete().Should().Be(0);
    }

    [SqlTable("s4_mstate_row")]
    public sealed class S4MStateRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }
}
