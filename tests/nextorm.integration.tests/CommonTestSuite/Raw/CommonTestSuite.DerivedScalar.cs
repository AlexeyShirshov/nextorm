using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T312 (#312): live execution of a derived source whose projection is an unaliased computed scalar.
/// Every case builds the query, executes it against the provider and compares the returned values with
/// independently specified expectations; rendered SQL is never the oracle here (the SQL-generation
/// contract lives in the provider unit tests). Before the fix SQLite fails with
/// <c>no such column: c0</c> and PostgreSQL with SQLSTATE 42703 referring to <c>"c0"</c>.
/// </summary>
public abstract partial class CommonTestSuite
{
    /// <summary>AC1: the ordered form from the issue must return exactly the tripled inner values.</summary>
    [Fact]
    public void T312_DerivedScalar_OrderedDerived_ShouldReturnTripledValues()
    {
        var inner = _sut.SimpleEntity.Where(s => s.Id <= 2).Select(s => s.Id * 2);

        var rows = _sut.From(inner).OrderBy(x => x).Select(x => x * 3).ToList();

        rows.Should().Equal([6, 12]);
    }

    /// <summary>AC2: a plain derived source executes without an ordering/reference wrapper.</summary>
    [Fact]
    public void T312_DerivedScalar_PlainDerived_ShouldReturnProjectedValues()
    {
        var inner = _sut.SimpleEntity.Where(s => s.Id <= 2).Select(s => s.Id * 2);

        var rows = _sut.From(inner).Select(x => x + 1).ToList();

        rows.Should().BeEquivalentTo([3, 5]);
    }

    /// <summary>AC2: two nested derived scalar sources, each with its own computed projection.</summary>
    [Fact]
    public void T312_DerivedScalar_NestedDerived_ShouldReturnProjectedValues()
    {
        var inner = _sut.SimpleEntity.Where(s => s.Id <= 2).Select(s => s.Id * 2);
        var middle = _sut.From(inner).Select(x => x * 3);

        var rows = _sut.From(middle).Select(y => y / 2).ToList();

        rows.Should().BeEquivalentTo([3, 6]);
    }

    /// <summary>
    /// AC2: a derived join whose projection is a computed scalar over the joined row. The two expected
    /// values come from independent inputs (simple ids 1..2 joined to complex ids 1..2).
    /// </summary>
    [Fact]
    public void T312_DerivedScalar_DerivedJoin_ShouldReturnIndependentlyExpectedValues()
    {
        var joinedInner = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => p.Item1.Id * 2);

        var rows = _sut.From(joinedInner).OrderBy(x => x).Select(x => x * 3).ToList();

        // simple_entity ids 1..10 joined to complex_entity ids 1..3 give 1,2,3; doubled then tripled.
        rows.Should().Equal([6, 12, 18]);
    }

    /// <summary>
    /// AC3: the real lazy-temporary-table (CTAS) materialisation path returns the same values. A
    /// provider without temporary CTAS must refuse the materialisation with the documented exception
    /// (capability-tolerant control, not a skip), so the case executes everywhere.
    /// </summary>
    [Fact]
    public void T312_DerivedScalar_TempTableCtas_ShouldReturnTripledValues()
    {
        var inner = _sut.SimpleEntity.Where(s => s.Id <= 2).Select(s => s.Id * 2);
        var source = _sut.From(inner).OrderBy(x => x).Select(x => x * 3).AsTempTable();

        // The temp-table read projects the created column under a named member, so its ORDER BY
        // resolves the projection item; a bare scalar ORDER BY over a raw temp-table source renders no
        // key (an unrelated limitation, not part of this contract).
        var read = () => _sut.DataProvider.From(source)
            .Select(t => new { V = t.GetInt32("c0") })
            .OrderBy(x => x.V)
            .ToList();

        if (!Provider.SupportsTemporaryCreateTableAsSelect)
        {
            // Capability control: the provider rejects the temporary materialisation before executing
            // anything (SQL Server keeps only the persistent ToTable form). Pinned refusal, not a skip.
            read.Should().Throw<NotSupportedException>()
                .WithMessage("*cannot materialise a query into a temporary table*");
            return;
        }

        read().Select(x => x.V).Should().Equal([6, 12]);
    }

    /// <summary>
    /// The same prepared command is executed twice in sequence: the derived source and its output
    /// identifier must stay stable across the second (cached) execution.
    /// </summary>
    [Fact]
    public void T312_DerivedScalar_SequentialPreparedReuse_ShouldReturnStableValues()
    {
        var inner = _sut.SimpleEntity.Where(s => s.Id <= 2).Select(s => s.Id * 2);
        var command = _sut.From(inner).OrderBy(x => x).Select(x => x * 3);

        var first = command.ToList();
        var second = command.ToList();

        first.Should().Equal([6, 12]);
        second.Should().Equal([6, 12]);
    }
}
