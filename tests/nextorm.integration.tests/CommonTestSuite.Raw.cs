using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Shared integration coverage for the raw command surface added in issue #70 phase 0+1
/// (<see cref="IRawCommandExecutor.ExecuteRaw(string, IReadOnlyList{ProcedureParameter})"/> and its async
/// twin): scalar and name-mapped entity result sets, DML, sequential result sets and transaction
/// rollback. Providers without multi-statement batches or transactions skip the corresponding tests
/// through the existing <see cref="ITestProvider"/> flags.
/// </summary>
public abstract partial class CommonTestSuite
{
    // Raw entity mapping binds by column name and requires a public parameterless constructor, so the
    // mapped type is a concrete class (interface mappings are only usable through LINQ).
    [SqlTable("complex_entity")]
    private sealed class RawComplexEntity
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("somestring")]
        public string? String { get; set; }

        [Column("nullableint")]
        public int? Int { get; set; }
    }

    // The Delete suite picks keys above 1_000_000; stay clear of that range so a row left behind by
    // another test can never collide with a raw test's marker.
    private static int RawKey() => Random.Shared.Next(2_000_000, int.MaxValue);

    [Fact]
    public void ExecuteRaw_ScalarWithParameter_ShouldReturnParameterValue()
    {
        var ctx = _sut.DataProvider;
        var value = RawKey();

        using var result = ctx.ExecuteRaw("select @v as value", [new ProcedureParameter("v", value)]);

        result.Read<int>().Should().Equal(value);
    }

    [Fact]
    public void ExecuteRaw_EntityMappedByColumnName_ShouldIgnorePropertyOrder()
    {
        var ctx = _sut.DataProvider;

        // The reader columns (somestring, nullableint, id) are deliberately not in property order
        // (Id, String, Int): raw mapping must bind by column name, not by position.
        using var result = ctx.ExecuteRaw(
            "select somestring, nullableint, id from complex_entity where id = @id",
            [new ProcedureParameter("id", 1L)]);

        var rows = result.Read<RawComplexEntity>();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(1L);
        rows[0].String.Should().Be("dadfasd");
        rows[0].Int.Should().BeNull();
    }

    [Fact]
    public void ExecuteRaw_Dml_ShouldPersistRowReadableByLinq()
    {
        var ctx = _sut.DataProvider;
        var id = RawKey();

        using (ctx.ExecuteRaw(
            "insert into delete_entity (id, name, age) values (@id, @name, @age)",
            [
                new ProcedureParameter("id", id),
                new ProcedureParameter("name", "raw-insert"),
                new ProcedureParameter("age", 7),
            ]))
        {
        }

        var rows = ctx.From<IDeleteEntity>()
            .Where(x => x.Id == id)
            .Select(x => new { x.Name, x.Age })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Name.Should().Be("raw-insert");
        rows[0].Age.Should().Be(7);
    }

    [Fact]
    public async Task ExecuteRawAsync_ScalarAndDml_ShouldRoundTrip()
    {
        var ctx = _sut.DataProvider;
        var value = RawKey();
        var id = RawKey();

        await using (var scalar = await ctx.ExecuteRawAsync(
            "select @v as value",
            [new ProcedureParameter("v", value)],
            TestContext.Current.CancellationToken))
        {
            var rows = new List<int>();
            await foreach (var row in scalar.ReadAsync<int>(TestContext.Current.CancellationToken))
                rows.Add(row);

            rows.Should().Equal(value);
        }

        await using (await ctx.ExecuteRawAsync(
            "insert into delete_entity (id, name, age) values (@id, @name, @age)",
            [
                new ProcedureParameter("id", id),
                new ProcedureParameter("name", "raw-async"),
                new ProcedureParameter("age", 8),
            ],
            TestContext.Current.CancellationToken))
        {
        }

        var persisted = await ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Name).ToListAsync();

        persisted.Should().Equal("raw-async");
    }

    [Fact]
    public void ExecuteRaw_MultipleResultSets_ShouldReadSequentially()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot execute a multi-statement batch in one round trip.");

        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

        result.Read<int>().Should().Equal(1);
        result.Read<int>().Should().Equal(2);
    }

    [Fact]
    public void ExecuteRaw_TransactionRollback_ShouldDiscardRawInsert()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        var id = RawKey();

        using (var tx = transactions.BeginTransaction())
        {
            using (ctx.ExecuteRaw(
                "insert into delete_entity (id, name, age) values (@id, @name, @age)",
                [
                    new ProcedureParameter("id", id),
                    new ProcedureParameter("name", "raw-tx"),
                    new ProcedureParameter("age", 9),
                ]))
            {
            }

            tx.Rollback();
        }

        ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Id).ToList().Should().BeEmpty();
    }
}
