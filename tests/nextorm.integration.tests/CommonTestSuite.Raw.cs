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
    public void ExecuteRaw_MultipleResultSets_ReadSets_HeterogeneousPerSetTypes_ShouldReadInOrder()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot execute a multi-statement batch in one round trip.");

        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw("select 1 as a; select 'two' as b");

        var indices = new List<int>();
        var ints = new List<int>();
        var strings = new List<string>();
        foreach (var set in result)
        {
            indices.Add(set.Index);
            if (set.Index == 0)
                ints.AddRange(set.Read<int>());
            else
                strings.AddRange(set.Read<string>());
        }

        indices.Should().Equal(0, 1);
        ints.Should().Equal(1);
        strings.Should().Equal("two");
    }

    [Fact]
    public async Task ExecuteRawAsync_MultipleResultSets_ReadSetsAsync_HeterogeneousPerSetTypes_ShouldReadInOrder()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot execute a multi-statement batch in one round trip.");

        var ctx = _sut.DataProvider;
        var ct = TestContext.Current.CancellationToken;

        await using var result = await ctx.ExecuteRawAsync("select 1 as a; select 'two' as b", [], ct);

        var ints = new List<int>();
        var strings = new List<string>();
        await foreach (var set in result.WithCancellation(ct))
        {
            if (set.Index == 0)
            {
                await foreach (var value in set.ReadAsync<int>(ct))
                    ints.Add(value);
            }
            else
            {
                await foreach (var value in set.ReadAsync<string>(ct))
                    strings.Add(value);
            }
        }

        ints.Should().Equal(1);
        strings.Should().Equal("two");
    }

    [Fact]
    public void ExecuteRaw_MultipleResultSets_ReadSets_SkipsLeadingIntermediateAndTrailingColumnLessSets()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot execute a multi-statement batch in one round trip.");

        var ctx = _sut.DataProvider;

        // The no-op DML statements match no rows but still surface as zero-column results between and
        // around the selects, so they exercise leading/intermediate/trailing skipping.
        using var result = ctx.ExecuteRaw(
            "delete from delete_entity where 1 = 0; " +
            "select 1 as a; " +
            "update delete_entity set name = name where 1 = 0; " +
            "select 2 as b; " +
            "delete from delete_entity where 1 = 0");

        var indices = new List<int>();
        var values = new List<int>();
        foreach (var set in result)
        {
            indices.Add(set.Index);
            values.Add(set.Read<int>().Single());
        }

        indices.Should().Equal(0, 1);
        values.Should().Equal(1, 2);
    }

    [Fact]
    public void ExecuteRaw_MultipleResultSets_ReadSets_CursorFromAdvancedSet_IsStale()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot execute a multi-statement batch in one round trip.");

        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

        using var sets = result.GetEnumerator();
        sets.MoveNext().Should().BeTrue();
        var first = sets.Current;
        sets.MoveNext().Should().BeTrue();

        Action act = () => first.Read<int>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExecuteRaw_MultipleResultSets_ReadSets_ColumnNamesRemainStableAfterOuterAdvance()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot execute a multi-statement batch in one round trip.");

        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw("select 1 as a, 2 as b; select 3 as c");

        using var sets = result.GetEnumerator();
        sets.MoveNext().Should().BeTrue();
        var first = sets.Current;
        sets.MoveNext().Should().BeTrue();

        first.Index.Should().Be(0);
        first.FieldCount.Should().Be(2);
        first.ColumnNames.Should().Equal("a", "b");
        sets.Current.ColumnNames.Should().Equal("c");
    }

    [Fact]
    public void ExecuteRaw_MultipleResultSets_ReadSets_UnreadRowsAreAutoSkippedAndNextSetStaysIntact()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot execute a multi-statement batch in one round trip.");

        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw("select 1 as a union all select 10; select 2 as b");

        using var sets = result.GetEnumerator();
        sets.MoveNext().Should().BeTrue();
        // Deliberately leave set 0's rows unread: advancing must auto-skip them.
        sets.MoveNext().Should().BeTrue();
        sets.Current.Read<int>().Should().Equal(2);
    }

    [Fact]
    public void ExecuteRaw_MultipleResultSets_ReadSets_OutputsRemainReadableAfterExhaustion()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot execute a multi-statement batch in one round trip.");

        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

        var indices = new List<int>();
        foreach (var set in result)
        {
            indices.Add(set.Index);
            set.Read<int>().Should().NotBeEmpty();
        }

        indices.Should().Equal(0, 1);

        // The traversal ended cleanly, so the result (and its output snapshot) stays usable.
        result.OutputParameters.Should().BeEmpty();
    }

    [Fact]
    public void ExecuteRaw_MultipleResultSets_ReadSets_WhenOutputsReadFirst_TraversalThrows()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot execute a multi-statement batch in one round trip.");

        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

        // Reading outputs first closes the reader; the result sets are gone.
        result.OutputParameters.Should().BeEmpty();

        Action act = () => result.ToList();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExecuteRaw_MultipleResultSets_ReadSets_EarlyBreak_KeepsOwnerUsable()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot execute a multi-statement batch in one round trip.");

        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

        foreach (var set in result)
        {
            set.Read<int>().Should().Equal(1);
            break;
        }

        // Breaking disposes the outer enumerator and invalidates its cursors, but the ProcedureResult
        // (and therefore its output snapshot) is still owned by the caller.
        result.OutputParameters.Should().BeEmpty();
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
