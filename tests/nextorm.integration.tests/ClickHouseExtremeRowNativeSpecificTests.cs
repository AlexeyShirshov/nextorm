using System.Data.Common;
using System.Linq.Expressions;
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

    // --- issue #150 D150.4: floating-key native vs forced-portable parity ----------------------

    private static EntityBuilder<ExtremeRowFloatEntity> FloatSource(IDataContext context)
        => context.From<ExtremeRowFloatEntity>();

    private static (int Id, int? Grp, float F32, double F64, float? F32n, double? F64n, int I1, int I2, string? Payload)
        WholeFloat(ExtremeRowFloatEntity e)
        => (e.Id, e.Grp, e.F32, e.F64, e.F32n, e.F64n, e.I1, e.I2, e.Payload);

    private static EntityBuilder<ExtremeRowDecimalEntity> DecimalSource(IDataContext context)
        => context.From<ExtremeRowDecimalEntity>();

    private static (int Id, decimal? Dec, string? Payload) WholeDecimal(ExtremeRowDecimalEntity e)
        => (e.Id, e.Dec, e.Payload);

    private static int FloatGroupKey(int? grp) => grp ?? 0;

    private static int ScalarInt(DataContext context, string sql)
        => (int)Query(context, sql).Single()[0]!;

    /// <summary>
    /// Runs the same dataset through the native whole-row selection and the forced-portable oracle
    /// (whose <c>All</c> form is the winner set) and requires every native winner to be a real whole
    /// source row. An empty oracle (all-NULL or empty input) requires an empty native result.
    /// </summary>
    private void AssertGlobalParity<TKey>(string dataset, bool isMax, Expression<Func<ExtremeRowFloatEntity, TKey>> key)
    {
        using var portable = Portable();
        var oracle = (isMax
                ? FloatSource(portable).Where(e => e.Dataset == dataset).SelectWhereMax(key, ExtremeRowTies.All)
                : FloatSource(portable).Where(e => e.Dataset == dataset).SelectWhereMin(key, ExtremeRowTies.All))
            .ToList()
            .Select(WholeFloat)
            .ToHashSet();

        var native = (isMax
                ? FloatSource(_sut.DataProvider).Where(e => e.Dataset == dataset).SelectWhereMax(key, ExtremeRowTies.One)
                : FloatSource(_sut.DataProvider).Where(e => e.Dataset == dataset).SelectWhereMin(key, ExtremeRowTies.One))
            .ToList()
            .Select(WholeFloat)
            .ToList();

        if (oracle.Count == 0)
        {
            native.Should().BeEmpty($"{dataset} has no winning row on either path");
            return;
        }

        native.Should().NotBeEmpty($"{dataset} must yield a winner");
        native.Should().ContainSingle($"{dataset} native One form must return exactly one winner");
        foreach (var row in native)
            oracle.Should().Contain(row, $"{dataset} native winner must be a real whole row");
    }

    /// <summary>
    /// Per-group form of <see cref="AssertGlobalParity{TKey}"/>: the native <c>One</c> pick of every
    /// group must be a member of the portable <c>All</c> winner set of that same group, and both paths
    /// must expose the same group keys (including a NULL group).
    /// </summary>
    private void AssertGroupedParity<TKey>(string dataset, bool isMax, Expression<Func<ExtremeRowFloatEntity, TKey>> key)
    {
        Expression<Func<ExtremeRowFloatEntity, object?>> group = e => new { e.Grp };

        using var portable = Portable();
        var oracle = (isMax
                ? FloatSource(portable).Where(e => e.Dataset == dataset).SelectWhereMax(key, ExtremeRowTies.All, group)
                : FloatSource(portable).Where(e => e.Dataset == dataset).SelectWhereMin(key, ExtremeRowTies.All, group))
            .ToList();
        var native = (isMax
                ? FloatSource(_sut.DataProvider).Where(e => e.Dataset == dataset).SelectWhereMax(key, ExtremeRowTies.One, group)
                : FloatSource(_sut.DataProvider).Where(e => e.Dataset == dataset).SelectWhereMin(key, ExtremeRowTies.One, group))
            .ToList();

        var oracleByGroup = oracle.GroupBy(e => FloatGroupKey(e.Grp))
            .ToDictionary(g => g.Key, g => g.Select(WholeFloat).ToHashSet());
        var nativeByGroup = native.GroupBy(e => FloatGroupKey(e.Grp))
            .ToDictionary(g => g.Key, g => g.ToList());

        nativeByGroup.Keys.Should().BeEquivalentTo(oracleByGroup.Keys, $"{dataset} must expose the same groups");
        foreach (var (groupKey, rows) in nativeByGroup)
        {
            rows.Should().ContainSingle();
            oracleByGroup[groupKey].Should().Contain(WholeFloat(rows[0]));
        }
    }

    [Fact]
    public void FloatNativeParity_SingleFloat64Global_MinAndMax_AcrossDatasets()
    {
        foreach (var dataset in new[] { "finite", "mixednan", "allnan", "inf", "zeros", "nanfirst", "nanlast", "naninf" })
        {
            AssertGlobalParity(dataset, isMax: true, e => e.F64);
            AssertGlobalParity(dataset, isMax: false, e => e.F64);
        }
    }

    [Fact]
    public void FloatNativeParity_NullableFloat64Global_MinAndMax_AcrossDatasets()
    {
        foreach (var dataset in new[] { "nulls", "allnull" })
        {
            AssertGlobalParity(dataset, isMax: true, e => e.F64n);
            AssertGlobalParity(dataset, isMax: false, e => e.F64n);
        }
    }

    [Fact]
    public void FloatNativeParity_SingleFloat32Global_MinAndMax_AcrossDatasets()
    {
        foreach (var dataset in new[] { "f32", "nanfirst", "nanlast", "mixednan" })
        {
            AssertGlobalParity(dataset, isMax: true, e => e.F32);
            AssertGlobalParity(dataset, isMax: false, e => e.F32);
        }
    }

    [Fact]
    public void FloatNativeParity_NullableFloat32Global_MinAndMax_AcrossDatasets()
    {
        foreach (var dataset in new[] { "nulls", "allnull" })
        {
            AssertGlobalParity(dataset, isMax: true, e => e.F32n);
            AssertGlobalParity(dataset, isMax: false, e => e.F32n);
        }
    }

    [Fact]
    public void FloatNativeParity_CompositeAndThreeComponentGlobal_MinAndMax_ShouldMatchPortable()
    {
        AssertGlobalParity("composite", isMax: true, e => new { e.I1, e.F64 });
        AssertGlobalParity("composite", isMax: false, e => new { e.I1, e.F64 });
        AssertGlobalParity("compfloat", isMax: true, e => new { e.F64, e.I1 });
        AssertGlobalParity("compfloat", isMax: false, e => new { e.F64, e.I1 });
        // The floating component must adapt in every position, not only trailing: leading and middle.
        AssertGlobalParity("threecomp", isMax: true, e => new { e.I1, e.I2, e.F64 });
        AssertGlobalParity("threecomp", isMax: false, e => new { e.I1, e.I2, e.F64 });
        AssertGlobalParity("threecomp", isMax: true, e => new { e.F64, e.I1, e.I2 });
        AssertGlobalParity("threecomp", isMax: false, e => new { e.F64, e.I1, e.I2 });
        AssertGlobalParity("threecomp", isMax: true, e => new { e.I1, e.F64, e.I2 });
        AssertGlobalParity("threecomp", isMax: false, e => new { e.I1, e.F64, e.I2 });
    }

    [Fact]
    public void FloatNativeParity_GroupedSingleFloat64_MinAndMax_AcrossDatasets()
    {
        foreach (var dataset in new[] { "finite", "mixednan", "nanfirst", "nanlast", "allnan", "inf", "zeros" })
        {
            AssertGroupedParity(dataset, isMax: true, e => e.F64);
            AssertGroupedParity(dataset, isMax: false, e => e.F64);
        }
    }

    [Fact]
    public void FloatNativeParity_GroupedNullableFloatingKeys_MinAndMax_AcrossDatasets()
    {
        foreach (var dataset in new[] { "nulls", "allnull" })
        {
            AssertGroupedParity(dataset, isMax: true, e => e.F64n);
            AssertGroupedParity(dataset, isMax: false, e => e.F64n);
            AssertGroupedParity(dataset, isMax: true, e => e.F32n);
            AssertGroupedParity(dataset, isMax: false, e => e.F32n);
        }
    }

    [Fact]
    public void FloatNativeParity_GroupedFloat32Widening_MinAndMax_AcrossDatasets()
    {
        foreach (var dataset in new[] { "f32", "nanfirst", "nanlast", "mixednan" })
        {
            AssertGroupedParity(dataset, isMax: true, e => e.F32);
            AssertGroupedParity(dataset, isMax: false, e => e.F32);
        }
    }

    [Fact]
    public void FloatNativeParity_GroupedCompositeAndThreeComponent_MinAndMax_ShouldMatchPortable()
    {
        AssertGroupedParity("composite", isMax: true, e => new { e.I1, e.F64 });
        AssertGroupedParity("composite", isMax: false, e => new { e.I1, e.F64 });
        AssertGroupedParity("compfloat", isMax: true, e => new { e.F64, e.I1 });
        AssertGroupedParity("compfloat", isMax: false, e => new { e.F64, e.I1 });
        AssertGroupedParity("threecomp", isMax: true, e => new { e.I1, e.I2, e.F64 });
        AssertGroupedParity("threecomp", isMax: false, e => new { e.I1, e.I2, e.F64 });
        AssertGroupedParity("threecomp", isMax: true, e => new { e.F64, e.I1, e.I2 });
        AssertGroupedParity("threecomp", isMax: false, e => new { e.F64, e.I1, e.I2 });
        AssertGroupedParity("threecomp", isMax: true, e => new { e.I1, e.F64, e.I2 });
        AssertGroupedParity("threecomp", isMax: false, e => new { e.I1, e.F64, e.I2 });
    }

    [Fact]
    public void FloatNativeParity_UniqueGlobal_ShouldReturnTheSameWinnerOnBothPaths()
    {
        var native = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "mixednan").SelectWhereMax(e => e.F64).ToList();

        using var portable = Portable();
        var forced = FloatSource(portable).Where(e => e.Dataset == "mixednan").SelectWhereMax(e => e.F64).ToList();

        native.Should().ContainSingle();
        forced.Should().ContainSingle();
        native.Select(WholeFloat).Should().Equal(forced.Select(WholeFloat));

        // The unique finite maximum is 2.5 (id 12); the NaN (id 11) must never win.
        native[0].Id.Should().Be(12);
        native[0].F64.Should().Be(2.5);
    }

    [Fact]
    public void FloatNativeParity_TiedGlobal_One_ShouldReturnOneWholeWinnerFromTheValidSet()
    {
        using var portable = Portable();

        // The finite maximum f64=10 ties ids 3 and 5; the minimum -12.75 ties ids 4 and 7.
        var maxSet = FloatSource(portable).Where(e => e.Dataset == "finite").SelectWhereMax(e => e.F64, ExtremeRowTies.All).ToList();
        maxSet.Select(e => e.Id).Should().BeEquivalentTo(new[] { 3, 5 });
        var minSet = FloatSource(portable).Where(e => e.Dataset == "finite").SelectWhereMin(e => e.F64, ExtremeRowTies.All).ToList();
        minSet.Select(e => e.Id).Should().BeEquivalentTo(new[] { 4, 7 });

        var nativeMax = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "finite").SelectWhereMax(e => e.F64).ToList();
        var nativeMin = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "finite").SelectWhereMin(e => e.F64).ToList();
        nativeMax.Should().ContainSingle();
        nativeMin.Should().ContainSingle();
        maxSet.Select(WholeFloat).Should().Contain(WholeFloat(nativeMax[0]));
        minSet.Select(WholeFloat).Should().Contain(WholeFloat(nativeMin[0]));

        // All-NaN and both signed zeros are ties too: every row is a valid whole winner.
        foreach (var (dataset, expectedIds) in new (string, int[])[]
        {
            ("allnan", [20, 21]),
            ("zeros", [40, 41])
        })
        {
            var oracle = FloatSource(portable).Where(e => e.Dataset == dataset).SelectWhereMax(e => e.F64, ExtremeRowTies.All).ToList();
            var native = FloatSource(_sut.DataProvider).Where(e => e.Dataset == dataset).SelectWhereMax(e => e.F64).ToList();
            oracle.Select(e => e.Id).Should().BeEquivalentTo(expectedIds);
            native.Should().ContainSingle();
            oracle.Select(WholeFloat).Should().Contain(WholeFloat(native[0]));
        }
    }

    [Fact]
    public void FloatNativeParity_NullablePayloadAndNullGroup_ShouldBePreserved()
    {
        var native = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "nullgrp").SelectWhereMax(e => e.F64).ToList();

        using var portable = Portable();
        var forced = FloatSource(portable).Where(e => e.Dataset == "nullgrp").SelectWhereMax(e => e.F64).ToList();

        // The unique maximum f64=5 (id 141) carries a NULL payload that must survive the tuple.
        native.Should().ContainSingle().Which.Id.Should().Be(141);
        native[0].Payload.Should().BeNull();
        forced.Should().ContainSingle().Which.Id.Should().Be(141);
        native.Select(WholeFloat).Should().Equal(forced.Select(WholeFloat));

        // Grouped: the NULL group is its own group (id 140) and group 10 keeps the NULL-payload winner.
        var nativeGrouped = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "nullgrp")
            .SelectWhereMax(e => e.F64, ExtremeRowTies.One, e => new { e.Grp }).ToList();
        var forcedGrouped = FloatSource(portable).Where(e => e.Dataset == "nullgrp")
            .SelectWhereMax(e => e.F64, ExtremeRowTies.One, e => new { e.Grp }).ToList();
        var nativeByGroup = nativeGrouped.ToDictionary(e => FloatGroupKey(e.Grp));
        var forcedByGroup = forcedGrouped.ToDictionary(e => FloatGroupKey(e.Grp));

        nativeByGroup.Keys.Should().BeEquivalentTo(forcedByGroup.Keys);
        nativeByGroup[0].Id.Should().Be(140);
        nativeByGroup[10].Id.Should().Be(141);
        nativeByGroup[10].Payload.Should().BeNull();
        nativeByGroup[0].Should().BeEquivalentTo(forcedByGroup[0]);
        nativeByGroup[10].Should().BeEquivalentTo(forcedByGroup[10]);

        // Min over the same dataset: the NULL group keeps id 140 (f64=3) and group 10 moves to the
        // minimum id 142 (f64=1). Only the Max direction was pinned before.
        var nativeMin = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "nullgrp")
            .SelectWhereMin(e => e.F64, ExtremeRowTies.One, e => new { e.Grp }).ToList();
        var forcedMin = FloatSource(portable).Where(e => e.Dataset == "nullgrp")
            .SelectWhereMin(e => e.F64, ExtremeRowTies.One, e => new { e.Grp }).ToList();
        var nativeMinByGroup = nativeMin.ToDictionary(e => FloatGroupKey(e.Grp));
        var forcedMinByGroup = forcedMin.ToDictionary(e => FloatGroupKey(e.Grp));

        nativeMinByGroup.Keys.Should().BeEquivalentTo(forcedMinByGroup.Keys);
        nativeMinByGroup[0].Id.Should().Be(140);
        nativeMinByGroup[10].Id.Should().Be(142);
        nativeMinByGroup[0].Should().BeEquivalentTo(forcedMinByGroup[0]);
        nativeMinByGroup[10].Should().BeEquivalentTo(forcedMinByGroup[10]);
    }

    [Fact]
    public void FloatNativeParity_EmptyGlobalAndGrouped_ShouldReturnZeroRows()
    {
        var nativeGlobal = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "empty").SelectWhereMax(e => e.F64).ToList();
        var nativeGrouped = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "empty")
            .SelectWhereMax(e => e.F64, ExtremeRowTies.One, e => new { e.Grp }).ToList();
        nativeGlobal.Should().BeEmpty();
        nativeGrouped.Should().BeEmpty();

        using var portable = Portable();
        var forcedGlobal = FloatSource(portable).Where(e => e.Dataset == "empty").SelectWhereMax(e => e.F64).ToList();
        var forcedGrouped = FloatSource(portable).Where(e => e.Dataset == "empty")
            .SelectWhereMax(e => e.F64, ExtremeRowTies.One, e => new { e.Grp }).ToList();
        forcedGlobal.Should().BeEmpty();
        forcedGrouped.Should().BeEmpty();
    }

    [Fact]
    public void FloatNativeParity_ScalarAndObjectProjections_ShouldMatchPortable()
    {
        var nativeScalar = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "mixednan")
            .SelectWhereMax(e => e.F64, e => e.Payload).ToList();
        var nativeObject = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "mixednan")
            .SelectWhereMax(e => e.F64, e => new { e.Id, e.Payload, e.F64 }).ToList();

        using var portable = Portable();
        var forcedScalar = FloatSource(portable).Where(e => e.Dataset == "mixednan")
            .SelectWhereMax(e => e.F64, e => e.Payload).ToList();
        var forcedObject = FloatSource(portable).Where(e => e.Dataset == "mixednan")
            .SelectWhereMax(e => e.F64, e => new { e.Id, e.Payload, e.F64 }).ToList();

        nativeScalar.Should().Equal(forcedScalar).And.Equal(["m3"]);
        nativeObject.Should().BeEquivalentTo(forcedObject);
        nativeObject.Should().ContainSingle().Which.Id.Should().Be(12);
    }

    [Fact]
    public void Counterexample_DirectArgMaxOnFloatingKey_ShouldDiverge_WhileShippedAdaptationMatchesPortable()
    {
        var context = Context();

        // C0: direct argMax seeds with the first row and x > NaN is false, so a leading NaN is never
        // replaced. nanfirst nan(id 90) is a finite-minimum row, yet direct argMax keeps it over id 92.
        // These assertions pin ClickHouse's observed aggregate seeding / NaN-comparison semantics on
        // server 25.8.33.6; a version bump must re-run this class before the adaptation is trusted.
        ScalarInt(context,
                "select tupleElement(argMax(tuple(id, payload), f64), 1) from extreme_float_150 where dataset = 'nanfirst'")
            .Should().Be(90);
        // naninf: the leading NaN (id 100) survives the direct aggregate over +inf (id 101).
        ScalarInt(context,
                "select tupleElement(argMax(tuple(id, payload), f64), 1) from extreme_float_150 where dataset = 'naninf'")
            .Should().Be(100);
        // Float32: NaN (id 80) beats +inf (id 82) under the direct aggregate.
        ScalarInt(context,
                "select tupleElement(argMax(tuple(id, payload), f32), 1) from extreme_float_150 where dataset = 'f32'")
            .Should().Be(80);

        // The shipped direction-aware adaptation ignores NaN and matches the forced-portable winner.
        using var portable = Portable();
        var shipped = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "nanfirst").SelectWhereMax(e => e.F64).ToList();
        var forced = FloatSource(portable).Where(e => e.Dataset == "nanfirst").SelectWhereMax(e => e.F64).ToList();
        shipped.Should().ContainSingle().Which.Id.Should().Be(92);
        forced.Should().ContainSingle().Which.Id.Should().Be(92);
        shipped.Select(WholeFloat).Should().Equal(forced.Select(WholeFloat));

        var shippedF32 = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "f32").SelectWhereMax(e => e.F32).ToList();
        var forcedF32 = FloatSource(portable).Where(e => e.Dataset == "f32").SelectWhereMax(e => e.F32).ToList();
        shippedF32.Should().ContainSingle().Which.Id.Should().Be(82);
        forcedF32.Should().ContainSingle().Which.Id.Should().Be(82);
    }

    [Fact]
    public void Counterexample_DirectArgMinOnFloatingKey_ShouldDiverge_WhileShippedAdaptationMatchesPortable()
    {
        var context = Context();

        // These assertions pin the observed ClickHouse 25.8.33.6 aggregate seeding semantics (same as
        // the C0 Max guard above); re-run this class after a server-version bump.
        // nanfirst argMin(nanfirst) -> 90 (NaN) vs the portable minimum 91.
        ScalarInt(context,
                "select tupleElement(argMin(tuple(id, payload), f64), 1) from extreme_float_150 where dataset = 'nanfirst'")
            .Should().Be(90);
        // naninf argMin -> 100 (leading NaN) vs the portable minimum -inf (id 102).
        ScalarInt(context,
                "select tupleElement(argMin(tuple(id, payload), f64), 1) from extreme_float_150 where dataset = 'naninf'")
            .Should().Be(100);

        using var portable = Portable();
        var shipped = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "nanfirst").SelectWhereMin(e => e.F64).ToList();
        var forced = FloatSource(portable).Where(e => e.Dataset == "nanfirst").SelectWhereMin(e => e.F64).ToList();
        shipped.Should().ContainSingle().Which.Id.Should().Be(91);
        forced.Should().ContainSingle().Which.Id.Should().Be(91);
        shipped.Select(WholeFloat).Should().Equal(forced.Select(WholeFloat));

        var shippedNanInf = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "naninf").SelectWhereMin(e => e.F64).ToList();
        var forcedNanInf = FloatSource(portable).Where(e => e.Dataset == "naninf").SelectWhereMin(e => e.F64).ToList();
        shippedNanInf.Should().ContainSingle().Which.Id.Should().Be(102);
        forcedNanInf.Should().ContainSingle().Which.Id.Should().Be(102);
    }

    [Fact]
    public void Counterexample_NaiveC1AndWideningOnly_ShouldDiverge_WhileShippedAdaptationMatchesPortable()
    {
        var context = Context();

        // C1 naive (isNaN(k), k) applied to Max makes NaN the largest, so mixednan's NaN (id 11) beats
        // the portable finite maximum id 12; the shipped Max flag is (isNaN(k) = 0), which ranks NaN
        // last. Observed on ClickHouse 25.8.33.6 (see the C0/C2 version note above).
        ScalarInt(context,
                "select tupleElement(argMax(tuple(id, payload), (isNaN(f64), f64)), 1) from extreme_float_150 where dataset = 'mixednan'")
            .Should().Be(11);

        // C2 widening alone: toFloat64(f32) still seeds on the leading Float32 NaN (id 80) over +inf
        // id 82, so the flag is required in addition to the widening.
        ScalarInt(context,
                "select tupleElement(argMax(tuple(id, payload), toFloat64(f32)), 1) from extreme_float_150 where dataset = 'f32'")
            .Should().Be(80);

        using var portable = Portable();
        var shippedMixed = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "mixednan").SelectWhereMax(e => e.F64).ToList();
        var forcedMixed = FloatSource(portable).Where(e => e.Dataset == "mixednan").SelectWhereMax(e => e.F64).ToList();
        shippedMixed.Should().ContainSingle().Which.Id.Should().Be(12);
        forcedMixed.Should().ContainSingle().Which.Id.Should().Be(12);

        var shippedF32 = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "f32").SelectWhereMax(e => e.F32).ToList();
        var forcedF32 = FloatSource(portable).Where(e => e.Dataset == "f32").SelectWhereMax(e => e.F32).ToList();
        shippedF32.Should().ContainSingle().Which.Id.Should().Be(82);
        forcedF32.Should().ContainSingle().Which.Id.Should().Be(82);
    }

    [Fact]
    public void NegativeRecheck_FloatingGroupWideFloatingAndDecimalKeys_ShouldRefuseNativeAndMatchPortable()
    {
        using var portable = Portable();

        // Floating group key: only integral group keys are in the native allowlist, so the renderer must
        // refuse and keep the portable window lowering, executed and matched against the oracle.
        var floatingGroupCommand = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "finite")
            .SelectWhereMax(e => e.F64, ExtremeRowTies.One, e => new { e.F64 }).ToCommand();
        SqlOf(_sut.DataProvider, floatingGroupCommand).Should().Contain("row_number()").And.NotContain("argMax");

        var floatingGroupNative = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "finite")
            .SelectWhereMax(e => e.F64, ExtremeRowTies.One, e => new { e.F64 }).ToList();
        var floatingGroupPortable = FloatSource(portable).Where(e => e.Dataset == "finite")
            .SelectWhereMax(e => e.F64, ExtremeRowTies.One, e => new { e.F64 }).ToList();
        floatingGroupNative.Select(WholeFloat).Should().BeEquivalentTo(floatingGroupPortable.Select(WholeFloat));

        // Arity > 3 with a floating component: the floating key cap is a deferred shape, so the
        // integral-only 4-component key is native but this one must stay portable.
        var wideKeyCommand = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "threecomp")
            .SelectWhereMax(e => new { e.Id, e.I1, e.I2, e.F64 }, e => new { e.Payload });
        SqlOf(_sut.DataProvider, wideKeyCommand).Should().Contain("row_number()").And.NotContain("argMax");

        var wideKeyNative = FloatSource(_sut.DataProvider).Where(e => e.Dataset == "threecomp")
            .SelectWhereMax(e => new { e.Id, e.I1, e.I2, e.F64 }, e => new { e.Payload }).ToList();
        var wideKeyPortable = FloatSource(portable).Where(e => e.Dataset == "threecomp")
            .SelectWhereMax(e => new { e.Id, e.I1, e.I2, e.F64 }, e => new { e.Payload }).ToList();
        wideKeyNative.Should().BeEquivalentTo(wideKeyPortable);

        // Decimal key: outside the allowlist, must stay portable and match on the real server.
        var decimalCommand = DecimalSource(_sut.DataProvider).SelectWhereMax(e => e.Dec).ToCommand();
        SqlOf(_sut.DataProvider, decimalCommand).Should().Contain("row_number()").And.NotContain("argMax");

        var decimalNative = DecimalSource(_sut.DataProvider).SelectWhereMax(e => e.Dec).ToList();
        var decimalPortable = DecimalSource(portable).SelectWhereMax(e => e.Dec).ToList();
        decimalNative.Select(WholeDecimal).Should().Equal(decimalPortable.Select(WholeDecimal));
        decimalNative.Should().ContainSingle().Which.Id.Should().Be(2);

        // Float16 (CLR Half) has no ClickHouse column mapping at all, so only the SQL refusal is
        // verifiable; the portable query cannot be executed because there is no physical Half column to
        // create or read back. This is an intentional documented restriction, not a silent demotion.
        var half = _sut.DataProvider.From<ExtremeRowHalfEntity>().SelectWhereMax(e => e.H, x => new { x.Id });
        SqlOf(_sut.DataProvider, half).Should().Contain("row_number()").And.NotContain("argMax");
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
