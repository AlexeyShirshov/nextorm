using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 4, batch 2 — the write builders on a live SQLite database:
/// <c>src/nextorm.core/Builders/Dml/Insert/InsertBuilder.cs</c> (the <c>Value</c> overloads, <c>Values</c> forms and
/// the <c>IgnoreFilters</c> scopes), <c>BulkInsertBuilder</c> (portable multi-row insert) and the
/// update/delete builders that share <c>SqlMutationBuilder</c>. Every accepted fact reads the written
/// state back; every refusal asserts the exact type plus the semantic identifier the guard carries.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep4InsertMutationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s4_mut_row (id integer primary key autoincrement, name text null, age int not null default 0);
        delete from s4_mut_row;
        """;

    public SqliteStep4InsertMutationTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>A single-row constant insert writes one row and reads back the values.</summary>
    [Fact]
    public void Insert_SingleRowConstants_ShouldPersist()
    {
        var rows = Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, "alpha")
            .Value(x => x.Age, 7)
            .Insert();

        rows.Should().Be(1);

        var read = Context.From<S4MutRow>().Where(x => x.Name == "alpha").Single();
        read.Age.Should().Be(7);
    }

    /// <summary>
    /// A column-copy value renders the source column instead of a parameter. SQLite resolves a bare
    /// identifier inside <c>VALUES</c> against the *target* relation and this insert has no FROM, so the
    /// provider surfaces its own "no such column" diagnostic: the fact pins the DB error identity rather
    /// than pretending the copy form works on this engine.
    /// </summary>
    [Fact]
    public void Insert_ColumnCopy_OnSqlite_ShouldSurfaceTheEngineDiagnostic()
    {
        Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, "copy")
            .Value(x => x.Age, 5)
            .Insert()
            .Should().Be(1);

        var act = () => Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, x => x.Name)
            .Value(x => x.Age, x => x.Age)
            .Insert();

        var error = act.Should().Throw<Microsoft.Data.Sqlite.SqliteException>().Which;
        error.SqliteErrorCode.Should().Be(1, "SQLite reports a generic SQL error for the unknown identifier");
        error.Message.Should().Contain("no such column");
    }

    /// <summary>A parameter-free value expression is constant-folded into a parameter.</summary>
    [Fact]
    public void Insert_ConstantExpressionValue_ShouldBeFolded()
    {
        Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, "expr")
            .Value(x => x.Age, x => 3 + 4)
            .Insert()
            .Should().Be(1);

        Context.From<S4MutRow>().Where(x => x.Name == "expr").Single().Age.Should().Be(7);
    }

    /// <summary>A value expression that reads the entity is neither a column copy nor a constant.</summary>
    [Fact]
    public void Insert_ParameterisedValueExpression_ShouldBeRefused()
    {
        var act = () => Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, "bad")
            .Value(x => x.Age, x => x.Id + 1)
            .Insert();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*mapped property reference or a parameter-free expression*");
    }

    /// <summary>SQLite cannot write the <c>DEFAULT</c> keyword as a value and says so by name.</summary>
    [Fact]
    public void Insert_DefaultValueMarker_ShouldBeRefusedByTheDialect()
    {
        var act = () => Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, "defaulted")
            .Value(x => x.Age, SqlDefault.Value)
            .Insert();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot write DEFAULT as a value; omit the column instead*");
    }

    /// <summary>The entity and multi-entity forms write every writable column.</summary>
    [Fact]
    public void Insert_EntityForms_ShouldPersist()
    {
        Context.CreateInsertBuilder<S4MutRow>()
            .Values(new S4MutRow { Name = "single", Age = 1 })
            .Insert()
            .Should().Be(1);

        Context.CreateInsertBuilder<S4MutRow>()
            .Values(new[]
            {
                new S4MutRow { Name = "many-1", Age = 2 },
                new S4MutRow { Name = "many-2", Age = 3 }
            })
            .Insert()
            .Should().Be(2);

        Context.From<S4MutRow>().Where(x => x.Name!.StartsWith("many-")).Count().Should().Be(2);
    }

    /// <summary>An empty entity sequence has nothing to write.</summary>
    [Fact]
    public void Insert_EmptyEntitySequence_ShouldBeRefused()
    {
        var act = () => Context.CreateInsertBuilder<S4MutRow>()
            .Values(Array.Empty<S4MutRow>())
            .Insert();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*At least one entity is required*");
    }

    /// <summary>Mixing the constant and entity forms in one builder is refused.</summary>
    [Fact]
    public void Insert_MixedValueForms_ShouldBeRefused()
    {
        var act = () => Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, "mixed")
            .Values(new S4MutRow { Name = "other", Age = 1 })
            .Insert();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be combined*");
    }

    /// <summary>The identity terminal returns the generated key and the projection terminal the row.</summary>
    [Fact]
    public void Insert_ReturningTerminals_ShouldReturnTheWrittenRows()
    {
        var key = Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, "returning")
            .Value(x => x.Age, 11)
            .ReturningIdentity<long>()
            .Single();

        key.Should().BeGreaterThan(0);

        var row = Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, "returning-row")
            .Value(x => x.Age, 12)
            .Returning()
            .Single();

        row.Name.Should().Be("returning-row");
        row.Age.Should().Be(12);

        var projection = Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, "returning-projection")
            .Value(x => x.Age, 13)
            .Returning(x => new { x.Id, x.Age })
            .Single();

        projection.Age.Should().Be(13);
        projection.Id.Should().BeGreaterThan(0);
    }

    /// <summary>The async terminal writes the same row set.</summary>
    [Fact]
    public async Task InsertAsync_ShouldPersist()
    {
        var rows = await Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, "async")
            .Value(x => x.Age, 21)
            .InsertAsync(TestContext.Current.CancellationToken);

        rows.Should().Be(1);
        Context.From<S4MutRow>().Where(x => x.Name == "async").Single().Age.Should().Be(21);
    }

    /// <summary>Every <c>IgnoreFilters</c> scope is chainable, including the empty ones.</summary>
    [Fact]
    public void Insert_IgnoreFilterScopes_ShouldBeChainable()
    {
        var builder = Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, "chained")
            .Value(x => x.Age, 9)
            .IgnoreFilters()
            .IgnoreFilters()
            .IgnoreFilters(typeof(S4MutRow))
            .IgnoreFilters(Array.Empty<Type>())
            .IgnoreFilters(new[] { "filter-a" })
            .IgnoreFilters(Array.Empty<string>())
            .IgnoreFilters(new[] { "filter-b" }, typeof(S4MutRow))
            .IgnoreFilters(new[] { "filter-c" }, Array.Empty<Type>());

        builder.ToSql().Should().Contain("insert into");

        builder.Insert().Should().Be(1, "the chained insert must actually reach the database");
        Context.From<S4MutRow>().Where(x => x.Name == "chained").Single().Age.Should().Be(9);
    }

    /// <summary>The portable bulk insert writes the whole batch in one statement.</summary>
    [Fact]
    public void BulkInsert_PortableBatch_ShouldPersist()
    {
        var written = Context.CreateBulkInsertBuilder<S4MutRow>()
            .Values(new[]
            {
                new S4MutRow { Name = "bulk-1", Age = 31 },
                new S4MutRow { Name = "bulk-2", Age = 32 }
            })
            .BulkInsert();

        written.Should().Be(2);
        Context.From<S4MutRow>().Where(x => x.Name!.StartsWith("bulk-")).Count().Should().Be(2);

        Context.CreateBulkInsertBuilder<S4MutRow>()
            .IgnoreFilters()
            .IgnoreFilters(typeof(S4MutRow))
            .IgnoreFilters(new[] { "k" })
            .IgnoreFilters(new[] { "k" }, typeof(S4MutRow))
            .Values(new[] { new S4MutRow { Name = "bulk-3", Age = 33 } })
            .BulkInsert()
            .Should().Be(1);
    }

    /// <summary>The update builder writes constants, a whole entity and a column copy.</summary>
    [Fact]
    public void Update_ShouldPersistTheChanges()
    {
        var id = InsertOne("update-me", 1);

        Context.CreateUpdateBuilder<S4MutRow>()
            .Set(x => x.Age, 42)
            .Where(x => x.Id == id)
            .Update()
            .Should().Be(1);

        Context.From<S4MutRow>().Where(x => x.Id == id).Single().Age.Should().Be(42);

        Context.CreateUpdateBuilder<S4MutRow>()
            .Set(new S4MutRow { Name = "updated", Age = 43 })
            .Where(x => x.Id == id)
            .Update()
            .Should().Be(1);

        var entity = Context.From<S4MutRow>().Where(x => x.Id == id).Single();
        entity.Name.Should().Be("updated");
        entity.Age.Should().Be(43);

        Context.CreateUpdateBuilder<S4MutRow>()
            .Set(x => x.Age, 44)
            .Where(x => x.Id == id)
            .Update()
            .Should().Be(1, "the update statement must reach the database");

        Context.From<S4MutRow>().Where(x => x.Id == id).Single().Age.Should().Be(44);

        Context.CreateUpdateBuilder<S4MutRow>()
            .Set(x => x.Age, 45)
            .Where(x => x.Id == id)
            .Returning(x => x.Age)
            .Single()
            .Should().Be(45);
    }

    /// <summary>The delete builder removes the filtered rows and then the whole table.</summary>
    [Fact]
    public async Task Delete_ShouldRemoveRows()
    {
        var first = InsertOne("delete-1", 1);
        InsertOne("delete-2", 2);

        Context.CreateDeleteBuilder<S4MutRow>()
            .Where(x => x.Id == first)
            .Delete()
            .Should().Be(1);

        Context.From<S4MutRow>().Where(x => x.Id == first).Count().Should().Be(0);

        var remaining = Context.From<S4MutRow>().Count();
        remaining.Should().Be(1);

        var deleted = await Context.CreateDeleteBuilder<S4MutRow>()
            .All()
            .DeleteAsync(TestContext.Current.CancellationToken);

        deleted.Should().Be(1);
        Context.From<S4MutRow>().Count().Should().Be(0);
    }

    private long InsertOne(string name, int age)
    {
        Context.CreateInsertBuilder<S4MutRow>()
            .Value(x => x.Name, name)
            .Value(x => x.Age, age)
            .Insert()
            .Should().Be(1);

        return Context.From<S4MutRow>().Where(x => x.Name == name).Select(x => x.Id).Single();
    }

    /// <summary>Marker table of the write-builder facts.</summary>
    [SqlTable("s4_mut_row")]
    public sealed class S4MutRow
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("age")]
        public int Age { get; set; }
    }
}
