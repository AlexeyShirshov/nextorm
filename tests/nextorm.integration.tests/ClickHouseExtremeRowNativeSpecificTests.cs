using System.Data.Common;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse-specific regression coverage (issue #144) for the native extreme-row mechanism, pinned
/// against a real ClickHouse 25.8 server:
/// <list type="number">
/// <item>global <c>argMin/argMax(tuple(payload), key)</c> returns one whole source row;</item>
/// <item>grouped form with <c>GROUP BY</c>;</item>
/// <item>composite <c>(k1, k2)</c> key ordered lexicographically, Min and Max;</item>
/// <item>nullable payload fields survive inside the tuple and nullable group keys work;</item>
/// <item>empty filtered global input returns zero rows once suppressed (<c>HAVING count() &gt; 0</c>);</item>
/// <item>rows with a NULL extreme-key component are excluded before aggregation.</item>
/// </list>
/// All statements are executed through the provider's own connection, so the suite proves the exact
/// server-side semantics the renderer relies on rather than a portable substitute.
/// </summary>
public sealed class ClickHouseExtremeRowNativeSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static readonly object ProbeGate = new();

    // Dictionary keys cannot be null, so the nullable group key gets an unreachable sentinel.
    private const string NullGroup = "\u0000null-group";

    private static bool _probeSeeded;

    // Dedicated fixture: k1/k2 are nullable so the composite null-component exclusion is exercisable,
    // payload/flag are nullable so the tuple payload preservation is exercisable and g is a nullable
    // group key.
    private static readonly string[] ProbeSeedStatements =
    [
        "drop table if exists probe_extreme_144",
        """
        create table probe_extreme_144
        (
            id Int32,
            g Nullable(String),
            k1 Nullable(Int32),
            k2 Nullable(Int32),
            payload Nullable(String),
            flag Nullable(Int32)
        ) engine = Memory
        """,
        """
        insert into probe_extreme_144 (id, g, k1, k2, payload, flag) values
            (1, 'a', 1, 5, 'a-1-5', NULL),
            (2, 'a', 1, 9, 'a-1-9', 7),
            (3, 'a', 1, 9, 'a-1-9b', 8),
            (4, 'a', 2, 1, NULL, NULL),
            (5, 'b', 1, 2, 'b-1-2', 1),
            (6, 'b', 3, 0, 'b-3-0', 2),
            (7, NULL, 5, 5, 'null-group', 3),
            (8, 'c', NULL, 4, 'null-k1', 4),
            (9, 'c', 3, NULL, 'null-k2', 5)
        """
    ];

    private DataContext Context()
    {
        var context = (DataContext)_sut.DataProvider;
        EnsureProbe(context);
        return context;
    }

    private static void EnsureProbe(DataContext context)
    {
        if (_probeSeeded)
            return;

        lock (ProbeGate)
        {
            if (_probeSeeded)
                return;

            foreach (var statement in ProbeSeedStatements)
                Execute(context, statement);

            _probeSeeded = true;
        }
    }

    private static void Execute(DataContext context, string sql)
    {
        context.EnsureConnectionOpen();
        using var command = context.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static List<object?[]> Query(DataContext context, string sql)
    {
        context.EnsureConnectionOpen();
        using var command = context.GetConnection().CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        return Read(reader);
    }

    private static List<object?[]> Read(DbDataReader reader)
    {
        var rows = new List<object?[]>();
        while (reader.Read())
        {
            var values = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);

            rows.Add(values);
        }

        return rows;
    }

    // --- issue #144 D7: native vs forced-portable parity over the mapped fixture table -------------

    private static EntityBuilder<ExtremeRowParityEntity> Source(IDataContext context)
        => context.From<ExtremeRowParityEntity>();

    private static PortableClickHouseDataContext Portable()
        => new(ClickHouseContainer.ConnectionString, new DataContextBuilder());

    private static (int Id, int? Category, int? Amount, int? TieBreak, string? Label, int? N) Whole(
        ExtremeRowParityEntity e)
        => (e.Id, e.Category, e.Amount, e.TieBreak, e.Label, e.N);

    private static int GroupKey(int? category) => category ?? 0;

    private static string SqlOf<T>(IDataContext context, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText
            .Replace("\r\n", "\n");

    [Fact]
    public void ForcedPortableContext_ShouldRenderTheWindowLowering_WhileNativeRendersArgMax()
    {
        var nativeCommand = Source(_sut.DataProvider).SelectWhereMax(e => e.Amount, e => new { e.Id });
        var nativeSql = SqlOf(_sut.DataProvider, nativeCommand);
        nativeSql.Should().Contain("argMax(tuple(");
        nativeSql.Should().Contain("having count() > 0");
        nativeSql.Should().NotContain("row_number()");

        using var portable = Portable();
        var portableCommand = Source(portable).SelectWhereMax(e => e.Amount, e => new { e.Id });
        var portableSql = SqlOf(portable, portableCommand);
        portableSql.Should().Contain("row_number()");
        portableSql.Should().NotContain("argMax");
    }

    [Fact]
    public void NativeParity_UniqueGlobal_ShouldReturnTheSameWinnerOnBothPaths()
    {
        var native = Source(_sut.DataProvider).SelectWhereMax(e => e.Amount).ToList();

        using var portable = Portable();
        var forced = Source(portable).SelectWhereMax(e => e.Amount).ToList();

        native.Should().ContainSingle();
        forced.Should().ContainSingle();
        native.Select(Whole).Should().Equal(forced.Select(Whole));

        native[0].Id.Should().Be(7);
        native[0].Category.Should().BeNull();
        native[0].Label.Should().Be("null-group");
    }

    [Fact]
    public void NativeParity_TiedGlobal_One_ShouldReturnOneWholeWinnerFromTheValidSet()
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
        validSet.Select(Whole).Should().Contain(Whole(native[0]));
        validSet.Select(Whole).Should().Contain(Whole(forced[0]));
    }

    [Fact]
    public void NativeParity_Grouped_ShouldDropTheAllNullPartitionAndKeepTheNullableGroup()
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
        native.Should().NotContain(e => e.Category == 30);
        forced.Should().NotContain(e => e.Category == 30);

        var nativeByGroup = native.ToDictionary(e => GroupKey(e.Category));
        var forcedByGroup = forced.ToDictionary(e => GroupKey(e.Category));

        nativeByGroup[20].Id.Should().Be(6);
        nativeByGroup[40].Id.Should().Be(10);
        nativeByGroup[0].Id.Should().Be(7);
        nativeByGroup[0].Should().BeEquivalentTo(forcedByGroup[0]);
        nativeByGroup[20].Should().BeEquivalentTo(forcedByGroup[20]);
        nativeByGroup[40].Should().BeEquivalentTo(forcedByGroup[40]);
    }

    [Fact]
    public void NativeParity_NullablePayload_ShouldBePreservedInsideTheTuple()
    {
        // The k2 selector makes id 1 the unique maximum of group 10; its label/n are NULL and must
        // survive the native argMax(payload, key) tuple extraction.
        var native = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.TieBreak, ExtremeRowTies.One, e => new { e.Category })
            .ToList();

        using var portable = Portable();
        var forced = Source(portable)
            .SelectWhereMax(e => e.TieBreak, ExtremeRowTies.One, e => new { e.Category })
            .ToList();

        var nativeGroup10 = native.Single(e => GroupKey(e.Category) == 10);
        var forcedGroup10 = forced.Single(e => GroupKey(e.Category) == 10);

        nativeGroup10.Id.Should().Be(1);
        nativeGroup10.Label.Should().BeNull();
        nativeGroup10.N.Should().BeNull();
        forcedGroup10.Should().BeEquivalentTo(nativeGroup10);
    }

    [Fact]
    public void NativeParity_EmptyGlobalAndGrouped_ShouldReturnZeroRows()
    {
        // The native global aggregate would return one default tuple on an empty input; the renderer
        // suppresses it by counting the filtered source, so both paths return zero rows.
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
        // The global maximum k1=5 is id 7; excluding it makes k1=3 (id 6) the new maximum. The source
        // condition is shared by the native and the portable source, so both paths move to id 6.
        var native = Source(_sut.DataProvider).Where(e => e.Id != 7)
            .SelectWhereMax(e => e.Amount).ToList();

        using var portable = Portable();
        var forced = Source(portable).Where(e => e.Id != 7)
            .SelectWhereMax(e => e.Amount).ToList();

        native.Should().ContainSingle().Which.Id.Should().Be(6);
        forced.Should().ContainSingle().Which.Id.Should().Be(6);
        native.Select(Whole).Should().Equal(forced.Select(Whole));
    }

    [Fact]
    public void OutputOrderByAndDistinct_ShouldApplyAfterSelection()
    {
        // Grouped Max k1 yields one row per group; projecting only k1 gives the multiset {1, 1, 3, 5}.
        // The DISTINCT is an outer post-selection step, so it collapses the two k1=1 rows (groups 10
        // and 40); the user's OrderBy is likewise applied to the output only.
        var command = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, e => new { e.Amount }, ExtremeRowTies.One, e => new { e.Category });

        command.ToList().Should().HaveCount(4);
        command.Distinct().ToList().Should().HaveCount(3);

        var ordered = command.OrderBy(1, OrderDirection.Asc).ToList();
        ordered.Select(x => x.Amount).Should().BeInAscendingOrder();
    }

    [Fact]
    public void NativeGroupedSql_ShouldUseMappedPhysicalColumnNames()
    {
        var command = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, e => new { e.Id }, ExtremeRowTies.One, e => new { e.Category });

        var sql = SqlOf(_sut.DataProvider, command);

        // The selectors are the CLR properties Amount/Category; the native clauses must carry the
        // mapped physical names k1/g rather than the CLR names.
        sql.Should().Contain("`g`");
        sql.Should().Contain("`k1`");
        sql.Should().NotContain("Category");
    }

    [Fact]
    public void NativeParity_ScalarAndObjectAndNoDefaultCtorProjections_ShouldMatchPortable()
    {
        var nativeScalar = Source(_sut.DataProvider).SelectWhereMax(e => e.Amount, e => e.Label).ToList();
        var nativeObject = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, e => new { e.Id, e.Label, e.N })
            .ToList();
        var nativeDto = Source(_sut.DataProvider)
            .SelectWhereMax(e => e.Amount, e => new ExtremeRowParityDto(e.Id, e.Amount, e.Label))
            .ToList();

        using var portable = Portable();
        var forcedScalar = Source(portable).SelectWhereMax(e => e.Amount, e => e.Label).ToList();
        var forcedObject = Source(portable)
            .SelectWhereMax(e => e.Amount, e => new { e.Id, e.Label, e.N })
            .ToList();
        var forcedDto = Source(portable)
            .SelectWhereMax(e => e.Amount, e => new ExtremeRowParityDto(e.Id, e.Amount, e.Label))
            .ToList();

        nativeScalar.Should().Equal(forcedScalar).And.Equal(["null-group"]);
        nativeObject.Should().Equal(forcedObject);
        nativeDto.Should().BeEquivalentTo(forcedDto);
        nativeDto.Should().ContainSingle().Which.Should().BeEquivalentTo(new ExtremeRowParityDto(7, 5, "null-group"));
    }

    [Fact]
    public void GlobalOne_MaxTuple_ShouldReturnSingleWholeWinnerRow()
    {
        // argMax over a scalar key returns the payload tuple of the winning row; every tuple element
        // comes from that same row (whole-row integrity), verified against the two tied candidates.
        var rows = Query(Context(), """
            select
                tupleElement(t, 1) as id,
                tupleElement(t, 2) as g,
                tupleElement(t, 5) as payload,
                tupleElement(t, 6) as flag
            from
            (
                select argMax(tuple(id, g, k1, k2, payload, flag), k2) as t
                from probe_extreme_144
                where k2 is not null
                having count() > 0
            )
            """);

        rows.Should().ContainSingle();
        var row = rows[0];
        var id = (int)row[0]!;
        id.Should().BeOneOf(2, 3);
        row[1].Should().Be("a");
        if (id == 2)
        {
            row[2].Should().Be("a-1-9");
            row[3].Should().Be(7);
        }
        else
        {
            row[2].Should().Be("a-1-9b");
            row[3].Should().Be(8);
        }
    }

    [Fact]
    public void GroupedOne_MaxTuple_ShouldReturnOneWholeWinnerPerGroup()
    {
        var rows = Query(Context(), """
            select
                grp,
                tupleElement(t, 1) as id,
                tupleElement(t, 5) as payload,
                tupleElement(t, 6) as flag
            from
            (
                select g as grp, argMax(tuple(id, g, k1, k2, payload, flag), k2) as t
                from probe_extreme_144
                where k2 is not null
                group by grp
            )
            """);

        rows.Should().HaveCount(4);

        var byGroup = rows.ToDictionary(r => (string?)r[0] ?? NullGroup);

        var a = byGroup["a"];
        ((int)a[1]!).Should().BeOneOf(2, 3);
        var b = byGroup["b"];
        ((int)b[1]!).Should().Be(5);
        b[2].Should().Be("b-1-2");
        b[3].Should().Be(1);
        var c = byGroup["c"];
        ((int)c[1]!).Should().Be(8);
        c[2].Should().Be("null-k1");
        c[3].Should().Be(4);
        var nullGroup = byGroup[NullGroup];
        ((int)nullGroup[1]!).Should().Be(7);
        nullGroup[2].Should().Be("null-group");
        nullGroup[3].Should().Be(3);
    }

    [Fact]
    public void CompositeKey_MinAndMax_ShouldOrderLexicographically()
    {
        var max = Query(Context(), """
            select tupleElement(t, 1) as id, tupleElement(t, 5) as payload
            from
            (
                select argMax(tuple(id, g, k1, k2, payload, flag), (k1, k2)) as t
                from probe_extreme_144
                where k1 is not null and k2 is not null
                having count() > 0
            )
            """);

        max.Should().ContainSingle();
        ((int)max[0][0]!).Should().Be(7); // (5,5) is the lexicographic maximum: k1 dominates
        max[0][1].Should().Be("null-group");

        var min = Query(Context(), """
            select tupleElement(t, 1) as id, tupleElement(t, 5) as payload
            from
            (
                select argMin(tuple(id, g, k1, k2, payload, flag), (k1, k2)) as t
                from probe_extreme_144
                where k1 is not null and k2 is not null
                having count() > 0
            )
            """);

        min.Should().ContainSingle();
        ((int)min[0][0]!).Should().Be(5); // (1,2) < (1,5) < ... lexicographically
        min[0][1].Should().Be("b-1-2");
    }

    [Fact]
    public void NullablePayloadAndGroupKey_ShouldBePreserved()
    {
        // Group 'a' minimum k2 is row 4, whose payload is NULL: the NULL must survive inside the tuple
        // rather than being dropped by the aggregate or coerced to a default. The NULL group key must
        // also produce its own group (row 7).
        var rows = Query(Context(), """
            select grp, tupleElement(t, 1) as id, tupleElement(t, 5) as payload
            from
            (
                select g as grp, argMin(tuple(id, g, k1, k2, payload, flag), k2) as t
                from probe_extreme_144
                where k2 is not null
                group by grp
            )
            """);

        var byGroup = rows.ToDictionary(r => (string?)r[0] ?? NullGroup);

        var a = byGroup["a"];
        ((int)a[1]!).Should().Be(4);
        a[2].Should().BeNull(); // NULL payload preserved inside the tuple

        var nullGroup = byGroup[NullGroup];
        ((int)nullGroup[1]!).Should().Be(7);
        nullGroup[2].Should().Be("null-group");
    }

    [Fact]
    public void EmptyFilteredGlobal_ShouldReturnZeroRows_WhenSuppressed()
    {
        // A global aggregate over an empty input normally yields one default row; counting the
        // filtered input and requiring it positive suppresses that row.
        var suppressed = Query(Context(), """
            select tupleElement(t, 1) as id
            from
            (
                select argMax(tuple(id, g, k1, k2, payload, flag), k2) as t
                from probe_extreme_144
                where k2 is not null and 1 = 0
                having count() > 0
            )
            """);

        suppressed.Should().BeEmpty();

        // Control: without the suppression the same query returns exactly one default tuple, which is
        // why the native renderer must count the filtered input.
        var control = Query(Context(), """
            select tupleElement(t, 1) as id, tupleElement(t, 5) as payload
            from
            (
                select argMax(tuple(id, g, k1, k2, payload, flag), k2) as t
                from probe_extreme_144
                where k2 is not null and 1 = 0
            )
            """);

        control.Should().ContainSingle();
        ((int)control[0][0]!).Should().Be(0);
        control[0][1].Should().BeNull();
    }

    [Fact]
    public void NullKeyComponents_ShouldBeExcludedBeforeAggregation()
    {
        // The fixture holds 9 rows; rows 8 and 9 each have one NULL extreme-key component and must be
        // removed by the pre-aggregation filter, leaving 7 eligible rows for the composite selection.
        var total = Query(Context(), "select count() from probe_extreme_144");
        var eligible = Query(Context(), "select count() from probe_extreme_144 where k1 is not null and k2 is not null");

        ((ulong)total[0][0]!).Should().Be(9);
        ((ulong)eligible[0][0]!).Should().Be(7);

        // The composite winner (id 7) is one of the eligible rows; the NULL-key rows 8/9 never win.
        var max = Query(Context(), """
            select tupleElement(t, 1) as id
            from
            (
                select argMax(tuple(id, g, k1, k2, payload, flag), (k1, k2)) as t
                from probe_extreme_144
                where k1 is not null and k2 is not null
                having count() > 0
            )
            """);

        max.Should().ContainSingle();
        ((int)max[0][0]!).Should().Be(7);
    }

    // --- alias collision (#144): payload columns named like the internal aliases --------------

    [Fact]
    public void PayloadNamedLikeInternalAliases_Global_ShouldExecuteAndReturnTheWholeExtremeRow()
    {
        // Three payload columns are physically named "__nextorm_extreme" / "__nextorm_extreme_src" /
        // "__nextorm_extreme_tuple", the renderers' alias bases. With the raw bases the aggregate would
        // reference a name shadowed by the source-subquery alias and the derived select would project a
        // duplicate tuple alias; the collision-free aliases must let the query execute and return the
        // real whole winner row (k=3 is id 2, with all payload fields from that same row).
        var rows = _sut.DataProvider.From<ExtremeRowAliasEntity>()
            .SelectWhereMax(e => e.K)
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(2);
        rows[0].K.Should().Be(3);
        rows[0].DerivedAlias.Should().Be(7);
        rows[0].SourceAlias.Should().Be(2);
        rows[0].TupleAlias.Should().Be(2);
    }
}

/// <summary>
/// ClickHouse rendering with the optional native extreme-row renderer disabled. The type is distinct so
/// the plan cache - keyed by concrete context type - cannot alias it with the native context.
/// </summary>
internal sealed class PortableClickHouseDialect : ClickHouseDialect
{
    public static readonly PortableClickHouseDialect PortableInstance = new();

    public override IExtremeRowRenderer? ExtremeRowRenderer => null;
}

/// <summary>The forced-portable ClickHouse context used to exercise the window-function lowering.</summary>
internal sealed class PortableClickHouseDataContext : ClickHouseDataContext
{
    public PortableClickHouseDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, optionsBuilder)
    {
    }

    public override ISqlDialect Dialect => PortableClickHouseDialect.PortableInstance;
}
