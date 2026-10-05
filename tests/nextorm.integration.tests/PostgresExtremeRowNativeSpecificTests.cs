using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

/// <summary>
/// Issue #144 D7 integration parity for PostgreSQL: the automatically selected native strategy
/// (<c>DISTINCT ON</c> grouped / <c>ORDER BY ... LIMIT 1</c> global) is compared against the forced
/// portable window-function lowering on a real PostgreSQL server. The portable side runs on a distinct
/// concrete context type (<see cref="PortablePostgresDataContext"/>, whose
/// <see cref="PortablePostgresDialect.ExtremeRowRenderer"/> is <see langword="null"/>) so the plan cache,
/// keyed by <c>ContextType</c>, can never alias the two strategies.
/// </summary>
/// <remarks>
/// The assertions follow the #144 contract: a unique extreme yields exactly the same whole winner on
/// both paths; a tie yields exactly one real whole source row (membership in the valid winner set, not
/// byte equality with the portable pick); nullable key components are excluded, an all-null partition
/// disappears, nullable groups/payloads survive, an empty source yields no rows, and projection /
/// output OrderBy / Distinct are applied after winner selection.
/// </remarks>
public sealed class PostgresExtremeRowNativeSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private static EntityBuilder<ExtremeRowParityEntity> Source(IDataContext context)
        => context.From<ExtremeRowParityEntity>();

    private static PortablePostgresDataContext Portable()
        => new(PostgresContainer.ConnectionString, new DataContextBuilder());

    // The whole row as a comparable tuple: a mixed native row (fields taken from different source rows)
    // cannot accidentally match a real winner.
    private static (int Id, int? Category, int? Amount, int? TieBreak, string? Label, int? N) Whole(
        ExtremeRowParityEntity e)
        => (e.Id, e.Category, e.Amount, e.TieBreak, e.Label, e.N);

    private static int GroupKey(int? category) => category ?? 0;

    private static string SqlOf<T>(IDataContext context, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText
            .Replace("\r\n", "\n");

    // --- strategy proof ------------------------------------------------------------------------

    [Fact]
    public void ForcedPortableContext_ShouldRenderTheWindowLowering_WhileNativeRendersTheNativeOne()
    {
        var nativeCommand = Source(_sut.DataProvider).SelectWhereMax(e => e.Amount, e => new { e.Id });
        var nativeSql = SqlOf(_sut.DataProvider, nativeCommand);
        nativeSql.Should().Contain("limit 1");
        nativeSql.Should().NotContain("row_number()");

        using var portable = Portable();
        var portableCommand = Source(portable).SelectWhereMax(e => e.Amount, e => new { e.Id });
        var portableSql = SqlOf(portable, portableCommand);
        portableSql.Should().Contain("row_number()");
        portableSql.Should().NotContain("limit 1");
    }

    [Fact]
    public void NativeGroupedSql_ShouldUseMappedPhysicalColumnNames()
    {
        var command = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, e => new { e.Id }, ExtremeRowTies.One, e => new { e.Category });

        var sql = SqlOf(_sut.DataProvider, command);

        sql.Should().Contain("distinct on (\"g\")");
        sql.Should().Contain("order by \"g\", \"k1\" desc");
        sql.Should().NotContain("Category");
    }

    // --- unique keys: identical winner on both paths -------------------------------------------

    [Fact]
    public void UniqueKey_Global_ShouldReturnTheSameWinnerOnBothPaths()
    {
        var native = Source(_sut.DataProvider).SelectWhereMax(e => e.Amount).ToList();

        using var portable = Portable();
        var forced = Source(portable).SelectWhereMax(e => e.Amount).ToList();

        native.Should().ContainSingle();
        forced.Should().ContainSingle();
        native.Select(Whole).Should().Equal(forced.Select(Whole));

        // The unique global maximum k1=5 is id 7 in the nullable group.
        native[0].Id.Should().Be(7);
        native[0].Category.Should().BeNull();
        native[0].Label.Should().Be("null-group");
        native[0].N.Should().Be(3);
    }

    [Fact]
    public void UniqueKey_Grouped_ShouldReturnTheSameWinnerPerGroupOnBothPaths()
    {
        var native = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, ExtremeRowTies.One, e => new { e.Category })
            .ToList();

        using var portable = Portable();
        var forced = Source(portable)
            .SelectWhereMax(e => e.Amount, ExtremeRowTies.One, e => new { e.Category })
            .ToList();

        native.Should().HaveCount(4);
        forced.Should().HaveCount(4);

        var nativeByGroup = native.ToDictionary(e => GroupKey(e.Category));
        var forcedByGroup = forced.ToDictionary(e => GroupKey(e.Category));

        // Groups 20, 40 and the null group each have a unique extreme winner.
        nativeByGroup[20].Id.Should().Be(6);
        nativeByGroup[40].Id.Should().Be(10);
        nativeByGroup[0].Id.Should().Be(7);

        nativeByGroup[20].Should().BeEquivalentTo(forcedByGroup[20]);
        nativeByGroup[40].Should().BeEquivalentTo(forcedByGroup[40]);
        nativeByGroup[0].Should().BeEquivalentTo(forcedByGroup[0]);
    }

    [Fact]
    public void CompositeKey_MinAndMax_ShouldReturnTheSameWinnerOnBothPaths()
    {
        var nativeMax = Source(_sut.DataProvider)
            .SelectWhereMax(e => new { e.Amount, e.TieBreak }, e => new { e.Id })
            .ToList();
        var nativeMin = Source(_sut.DataProvider)
            .SelectWhereMin(e => new { e.Amount, e.TieBreak }, e => new { e.Id })
            .ToList();

        using var portable = Portable();
        var forcedMax = Source(portable)
            .SelectWhereMax(e => new { e.Amount, e.TieBreak }, e => new { e.Id })
            .ToList();
        var forcedMin = Source(portable)
            .SelectWhereMin(e => new { e.Amount, e.TieBreak }, e => new { e.Id })
            .ToList();

        nativeMax.Should().ContainSingle().Which.Id.Should().Be(7); // (5, 5) dominates on k1
        nativeMin.Should().ContainSingle().Which.Id.Should().Be(10); // (1, 5) is the lexicographic minimum
        nativeMax.Should().Equal(forcedMax);
        nativeMin.Should().Equal(forcedMin);
    }

    // --- ties: exactly one real whole winner ----------------------------------------------------

    [Fact]
    public void TiedKey_Global_One_ShouldReturnExactlyOneWholeWinnerFromTheValidSet()
    {
        using var portable = Portable();

        // The portable All form is the oracle: every row tied on the minimum k1=1 (ids 1, 2, 3, 10).
        var validSet = Source(portable).SelectWhereMin(e => e.Amount, ExtremeRowTies.All).ToList();
        var expected = new (int Id, int? Category, int? Amount, int? TieBreak, string? Label, int? N)[]
        {
            (1, 10, 1, 99, null, null),
            (2, 10, 1, 9, "a-1-9", 7),
            (3, 10, 1, 9, "a-1-9b", 8),
            (10, 40, 1, 5, "d-1-5", 9),
        };
        validSet.Select(Whole).Should().BeEquivalentTo(expected);

        var native = Source(_sut.DataProvider).SelectWhereMin(e => e.Amount).ToList();
        var forced = Source(portable).SelectWhereMin(e => e.Amount).ToList();

        native.Should().ContainSingle();
        forced.Should().ContainSingle();
        native[0].Amount.Should().Be(1);
        forced[0].Amount.Should().Be(1);

        // Both picks are a real whole winner: no field may come from a different source row.
        validSet.Select(Whole).Should().Contain(Whole(native[0]));
        validSet.Select(Whole).Should().Contain(Whole(forced[0]));
    }

    [Fact]
    public void TiedKey_Grouped_One_ShouldReturnExactlyOneWholeWinnerPerTiedGroup()
    {
        using var portable = Portable();

        var oracle = Source(portable)
            .SelectWhereMax(e => e.Amount, ExtremeRowTies.All, e => new { e.Category })
            .ToList();

        var group10Winners = oracle.Where(e => GroupKey(e.Category) == 10).Select(Whole).ToList();
        var expected = new (int Id, int? Category, int? Amount, int? TieBreak, string? Label, int? N)[]
        {
            (1, 10, 1, 99, null, null),
            (2, 10, 1, 9, "a-1-9", 7),
            (3, 10, 1, 9, "a-1-9b", 8),
        };
        group10Winners.Should().BeEquivalentTo(expected);

        var native = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, ExtremeRowTies.One, e => new { e.Category })
            .ToList();

        var nativeGroup10 = native.Single(e => GroupKey(e.Category) == 10);
        group10Winners.Should().Contain(Whole(nativeGroup10));
    }

    // --- null semantics -------------------------------------------------------------------------

    [Fact]
    public void NullableKeyComponents_ShouldBeExcludedBeforeSelection()
    {
        var native = Source(_sut.DataProvider).SelectWhereMax(e => e.Amount).ToList();
        native.Should().ContainSingle();
        native[0].Id.Should().Be(7);

        // Rows 4, 8 and 9 each carry a NULL extreme-key component under the k1 selector; none may win.
        var grouped = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, ExtremeRowTies.One, e => new { e.Category })
            .ToList();
        grouped.Select(e => e.Id).Should().NotContain([4, 8, 9]);
    }

    [Fact]
    public void AllNullPartition_ShouldBeDropped()
    {
        using var portable = Portable();

        var native = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, ExtremeRowTies.One, e => new { e.Category })
            .ToList();
        var forced = Source(portable)
            .SelectWhereMax(e => e.Amount, ExtremeRowTies.One, e => new { e.Category })
            .ToList();

        // Group 30 holds only k1-NULL rows (ids 8 and 9) and must vanish from both paths.
        native.Should().NotContain(e => e.Category == 30);
        forced.Should().NotContain(e => e.Category == 30);
        native.Should().HaveCount(4);
        forced.Should().HaveCount(4);
    }

    [Fact]
    public void NullableGroupAndPayload_ShouldBePreserved()
    {
        // The k2 selector makes id 1 the unique maximum of group 10; its label is NULL and the NULL
        // must survive inside the winning whole row. The NULL group key must remain its own group.
        var native = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.TieBreak, ExtremeRowTies.One, e => new { e.Category })
            .ToList();

        using var portable = Portable();
        var forced = Source(portable)
            .SelectWhereMax(e => e.TieBreak, ExtremeRowTies.One, e => new { e.Category })
            .ToList();

        var nativeByGroup = native.ToDictionary(e => GroupKey(e.Category));
        var forcedByGroup = forced.ToDictionary(e => GroupKey(e.Category));

        nativeByGroup[10].Id.Should().Be(1);
        nativeByGroup[10].Label.Should().BeNull();
        nativeByGroup[10].N.Should().BeNull();
        forcedByGroup[10].Should().BeEquivalentTo(nativeByGroup[10]);

        nativeByGroup[0].Id.Should().Be(7);
        nativeByGroup[0].Label.Should().Be("null-group");
        forcedByGroup[0].Should().BeEquivalentTo(nativeByGroup[0]);
    }

    // --- empty / filtered -----------------------------------------------------------------------

    [Fact]
    public void EmptyGlobalAndGrouped_ShouldReturnZeroRows()
    {
        var nativeGlobal = Source(_sut.DataProvider).Where(e => e.Label == "no-such")
            .SelectWhereMax(e => e.Amount).ToList();
        var nativeGrouped = Source(_sut.DataProvider).Where(e => e.Label == "no-such")
            .SelectWhereMax(e => e.Amount, ExtremeRowTies.One, e => new { e.Category }).ToList();
        nativeGlobal.Should().BeEmpty();
        nativeGrouped.Should().BeEmpty();

        using var portable = Portable();
        var forcedGlobal = Source(portable).Where(e => e.Label == "no-such")
            .SelectWhereMax(e => e.Amount).ToList();
        var forcedGrouped = Source(portable).Where(e => e.Label == "no-such")
            .SelectWhereMax(e => e.Amount, ExtremeRowTies.One, e => new { e.Category }).ToList();
        forcedGlobal.Should().BeEmpty();
        forcedGrouped.Should().BeEmpty();
    }

    [Fact]
    public void WhereRemovingThePreviousMaximum_ShouldChangeTheWinner()
    {
        var native = Source(_sut.DataProvider).Where(e => e.Id != 7)
            .SelectWhereMax(e => e.Amount).ToList();

        using var portable = Portable();
        var forced = Source(portable).Where(e => e.Id != 7)
            .SelectWhereMax(e => e.Amount).ToList();

        native.Should().ContainSingle().Which.Id.Should().Be(6); // k1=3 becomes the maximum
        forced.Should().ContainSingle().Which.Id.Should().Be(6);
        native.Select(Whole).Should().Equal(forced.Select(Whole));
    }

    // --- projection / ordering / distinct are applied after selection ---------------------------

    [Fact]
    public void ScalarProjection_ShouldProjectTheSelectedWinner()
    {
        var native = Source(_sut.DataProvider).SelectWhereMax(e => e.Amount, e => e.Label).ToList();

        using var portable = Portable();
        var forced = Source(portable).SelectWhereMax(e => e.Amount, e => e.Label).ToList();

        native.Should().Equal(forced).And.Equal(["null-group"]);
    }

    [Fact]
    public void ObjectAndNoDefaultCtorProjections_ShouldProjectTheSelectedWinner()
    {
        var nativeObject = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, e => new { e.Id, e.Label, e.N })
            .ToList();
        var nativeDto = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, e => new ExtremeRowParityDto(e.Id, e.Amount, e.Label))
            .ToList();
        var nativeRecord = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, e => new ExtremeRowParityRecord(e.Id, e.Amount, e.Label))
            .ToList();

        using var portable = Portable();
        var forcedObject = Source(portable)
            .SelectWhereMax(e => e.Amount, e => new { e.Id, e.Label, e.N })
            .ToList();
        var forcedDto = Source(portable)
            .SelectWhereMax(e => e.Amount, e => new ExtremeRowParityDto(e.Id, e.Amount, e.Label))
            .ToList();
        var forcedRecord = Source(portable)
            .SelectWhereMax(e => e.Amount, e => new ExtremeRowParityRecord(e.Id, e.Amount, e.Label))
            .ToList();

        nativeObject.Should().Equal(forcedObject);
        nativeDto.Should().BeEquivalentTo(forcedDto);
        nativeRecord.Should().Equal(forcedRecord);

        nativeObject.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Id = 7, Label = "null-group", N = (int?)3 });
        nativeDto.Should().ContainSingle().Which.Should().BeEquivalentTo(new ExtremeRowParityDto(7, 5, "null-group"));
        nativeRecord.Should().Equal(new ExtremeRowParityRecord(7, 5, "null-group"));
    }

    [Fact]
    public void OutputOrderByAndDistinct_ShouldApplyAfterSelection()
    {
        // Grouped Max k1 yields one row per group. Projecting only k1 gives the multiset {1, 1, 3, 5};
        // the DISTINCT is an outer post-selection step, so it collapses the two k1=1 rows.
        var command = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, e => new { e.Amount }, ExtremeRowTies.One, e => new { e.Category });

        command.ToList().Should().HaveCount(4);
        command.Distinct().ToList().Should().HaveCount(3);

        // The user's ORDER BY is likewise applied to the output, never interleaved into winner selection.
        var ordered = command.OrderBy(1, OrderDirection.Asc).ToList();
        ordered.Select(x => x.Amount).Should().BeInAscendingOrder();
    }

    // --- alias collision (#144): a key column named like the internal derived-table alias -----

    [Fact]
    public void KeyColumnNamedLikeDerivedAlias_Grouped_ShouldExecuteTheNativeQueryCorrectly()
    {
        // The extreme key's physical name is "__nextorm_extreme", the renderer's derived-table alias base.
        // Reusing it verbatim would make PostgreSQL reject the ORDER BY as ambiguous against the table
        // alias; the collision-free alias must let the native grouped query execute and return the real
        // per-group winner (group 10 -> id 2 with the key 7; group 20 -> id 3).
        var rows = _sut.DataProvider.From<ExtremeRowAliasEntity>()
            .SelectWhereMax(e => e.DerivedAlias, ExtremeRowTies.One, e => new { e.G })
            .ToList();

        rows.Should().HaveCount(2);
        var group10 = rows.Single(r => r.G == 10);
        group10.Id.Should().Be(2);
        group10.DerivedAlias.Should().Be(7);
        rows.Single(r => r.G == 20).Id.Should().Be(3);
    }
}

/// <summary>
/// PostgreSQL rendering with the optional native extreme-row renderer disabled. The type is distinct so
/// the plan cache - keyed by concrete context type - cannot alias it with the native context.
/// </summary>
internal sealed class PortablePostgresDialect : PostgresDialect
{
    public static readonly PortablePostgresDialect PortableInstance = new();

    public override IExtremeRowRenderer? ExtremeRowRenderer => null;
}

/// <summary>The forced-portable PostgreSQL context used to exercise the window-function lowering.</summary>
internal sealed class PortablePostgresDataContext : PostgresDataContext
{
    public PortablePostgresDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, optionsBuilder)
    {
    }

    public override ISqlDialect Dialect => PortablePostgresDialect.PortableInstance;
}
