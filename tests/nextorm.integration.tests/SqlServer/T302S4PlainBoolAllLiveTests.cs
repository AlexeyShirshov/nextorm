using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// S4/T302 (AC-S4-3, VM-S4-08): SQL Server <c>bit</c> ordering for <c>ExtremeRowTies.All</c> executed
/// against a live server. A plain non-nullable <c>bool</c> and a <c>bool?</c> are used as scalar value
/// keys and as composite components; the engine aggregates the order-preserving integer storage
/// (<c>case when v then 1 else 0 end</c>), so <c>false &lt; true</c> is preserved, <c>MAX(bit)</c> is
/// never emitted, and the full tied winner set comes back.
/// </summary>
[Collection("SqlServer")]
public sealed class T302S4PlainBoolAllSqlServerTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private const string TableName = "t302_s4_live_bool";

    private const string CreateSql =
        "create table t302_s4_live_bool (id int not null, ok bit not null, nb bit null, sub int null, " +
        "payload int null, grp int not null default 0)";

    [SqlTable("t302_s4_live_bool")]
    public sealed class Row
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("ok")]
        public bool Ok { get; set; }

        [Column("nb")]
        public bool? Nb { get; set; }

        [Column("sub")]
        public int? Sub { get; set; }

        [Column("payload")]
        public int? Payload { get; set; }

        [Column("grp")]
        public int Grp { get; set; }
    }

    private const string ConvertedTableName = "t302_s4_live_cbool";

    private const string CreateConvertedSql =
        "create table t302_s4_live_cbool (id int not null, ok int not null, sub int null, " +
        "payload int null, grp int not null default 0)";

    [SqlTable("t302_s4_live_cbool")]
    public sealed class ConvertedRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("ok")]
        [ValueConverter(typeof(T302S4BoolToIntConverter))]
        public bool Ok { get; set; }

        [Column("sub")]
        public int? Sub { get; set; }

        [Column("payload")]
        public int? Payload { get; set; }

        [Column("grp")]
        public int Grp { get; set; }
    }

    private EntityBuilder<Row> Source() => _sut.DataProvider.From<Row>();

    private EntityBuilder<ConvertedRow> ConvertedSource() => _sut.DataProvider.From<ConvertedRow>();

    private void Execute(string sql)
    {
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void Reset(params string[] values)
    {
        Execute($"drop table if exists {TableName}");
        Execute(CreateSql);
        Execute($"insert into {TableName} (id, ok, nb, sub, payload) values " +
                string.Join(", ", values));
    }

    private void ResetGrouped(params string[] values)
    {
        Execute($"drop table if exists {TableName}");
        Execute(CreateSql);
        Execute($"insert into {TableName} (id, ok, nb, sub, payload, grp) values " +
                string.Join(", ", values));
    }

    private void ResetConverted(params string[] values)
    {
        Execute($"drop table if exists {ConvertedTableName}");
        Execute(CreateConvertedSql);
        Execute($"insert into {ConvertedTableName} (id, ok, sub, payload) values " +
                string.Join(", ", values));
    }

    private void ResetConvertedGrouped(params string[] values)
    {
        Execute($"drop table if exists {ConvertedTableName}");
        Execute(CreateConvertedSql);
        Execute($"insert into {ConvertedTableName} (id, ok, sub, payload, grp) values " +
                string.Join(", ", values));
    }

    /// <summary>
    /// Scalar plain-<c>bool</c> <c>All</c>: the tied winners at <c>true</c> are returned for <c>Max</c> and
    /// the tied winners at <c>false</c> for <c>Min</c>, so the aggregate order is <c>false &lt; true</c>.
    /// </summary>
    [Fact]
    public void MaxMin_All_Global_PlainBool_Scalar_FalseBeforeTrue()
    {
        Reset(
            "(1, 1, null, null, 10)",
            "(2, 1, null, null, null)",
            "(3, 0, null, null, 30)",
            "(4, 0, null, null, 40)",
            "(5, 0, null, null, 50)");
        try
        {
            var max = Source().SelectWhereMax(e => e.Ok, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            max.Select(r => r.Id).Should().ContainInOrder(1, 2);
            max.Should().OnlyContain(r => r.Ok, "true must be the maximum of the bit domain");
            max.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 10, null });

            var min = Source().SelectWhereMin(e => e.Ok, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            min.Select(r => r.Id).Should().ContainInOrder(3, 4, 5);
            min.Should().OnlyContain(r => !r.Ok, "false must be the minimum of the bit domain");
        }
        finally { Execute($"drop table if exists {TableName}"); }
    }

    /// <summary>
    /// Composite plain-<c>bool</c> value key: <c>(true,100)</c> wins <c>Max</c> over <c>(true,2)</c> and
    /// <c>(false,999)</c>, <c>(false,5)</c> wins <c>Min</c>, and no component-wise mix is synthesized.
    /// </summary>
    [Fact]
    public void MaxMin_All_Global_PlainBool_Composite2_OrderingPreserved()
    {
        Reset(
            "(1, 1, null, 100, 9100)",
            "(2, 1, null, 2, 92)",
            "(3, 0, null, 5, 35)",
            "(4, 0, null, 999, 4999)");
        try
        {
            var max = Source().SelectWhereMax(e => new { e.Ok, e.Sub }, ExtremeRowTies.All).ToList();
            max.Should().ContainSingle();
            max[0].Id.Should().Be(1);
            max[0].Ok.Should().BeTrue();
            max[0].Sub.Should().Be(100);
            max.Should().NotContain(r => !r.Ok,
                "false is strictly below true, so no false row may win Max");
            max.Should().NotContain(r => r.Ok && r.Sub == 999,
                "stage 2 aggregates inside the surviving boolean prefix, so (true,999) cannot be synthesized");

            var min = Source().SelectWhereMin(e => new { e.Ok, e.Sub }, ExtremeRowTies.All).ToList();
            min.Should().ContainSingle();
            min[0].Id.Should().Be(3);
            min[0].Ok.Should().BeFalse();
            min[0].Sub.Should().Be(5);
            min.Should().NotContain(r => r.Ok,
                "a true row must never win Min while a false row exists");
            min.Should().NotContain(r => !r.Ok && r.Sub == 2,
                "the independent component-wise minimum must never be stitched in");
        }
        finally { Execute($"drop table if exists {TableName}"); }
    }

    /// <summary>
    /// Nullable <c>bool?</c> value key: a NULL is never a winner, the tied <c>true</c> rows win <c>Max</c>
    /// and the <c>false</c> row wins <c>Min</c>.
    /// </summary>
    [Fact]
    public void MaxMin_All_Global_NullableBool_Scalar_ExcludesNull()
    {
        Reset(
            "(1, 0, null, null, 10)",
            "(2, 0, 1, null, 20)",
            "(3, 0, 1, null, 30)",
            "(4, 0, 0, null, 40)");
        try
        {
            var max = Source().SelectWhereMax(e => e.Nb, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            max.Select(r => r.Id).Should().ContainInOrder(2, 3);
            max.Should().OnlyContain(r => r.Nb == true);

            var min = Source().SelectWhereMin(e => e.Nb, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            min.Select(r => r.Id).Should().ContainInOrder(4);
            min[0].Nb.Should().BeFalse();
            min.Should().NotContain(r => r.Nb == null, "the NULL value key is never a winner");
        }
        finally { Execute($"drop table if exists {TableName}"); }
    }

    /// <summary>
    /// Grouped scalar plain-<c>bool</c> value key (AC-S4-3 shape not covered before): every group gets its
    /// own extremum, the tied <c>true</c> rows win <c>Max</c> inside a group and the tied <c>false</c> rows
    /// win <c>Min</c>, so the group-by lowering keeps <c>false &lt; true</c> per group.
    /// </summary>
    [Fact]
    public void MaxMin_All_Grouped_Scalar_PlainBool_PerGroupExtremum()
    {
        ResetGrouped(
            "(1, 1, null, 100, 11, 1)",
            "(2, 1, null, 2, 12, 1)",
            "(3, 0, null, 5, 13, 1)",
            "(4, 0, null, 7, 24, 2)",
            "(5, 1, null, 1, 25, 2)",
            "(6, 1, null, 3, 26, 2)");
        try
        {
            var max = Source().SelectWhereMax(e => e.Ok, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            max.Select(r => r.Id).Should().ContainInOrder(1, 2, 5, 6);
            max.Select(r => r.Grp).Should().Equal(1, 1, 2, 2);
            max.Should().OnlyContain(r => r.Ok, "true is the per-group maximum of the bit domain");

            var min = Source().SelectWhereMin(e => e.Ok, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            min.Select(r => r.Id).Should().ContainInOrder(3, 4);
            min.Select(r => r.Grp).Should().Equal(1, 2);
            min.Should().OnlyContain(r => !r.Ok, "false is the per-group minimum of the bit domain");
        }
        finally { Execute($"drop table if exists {TableName}"); }
    }

    /// <summary>
    /// Grouped composite value key with a nullable <c>bool?</c> component (AC-S4-3 shape not covered
    /// before): the NULL component is never a winner, the lexicographic extremum of each group is
    /// returned, and the nullable <c>grp</c>-free plain column keeps the groups apart.
    /// </summary>
    [Fact]
    public void MaxMin_All_Grouped_Composite2_NullableBool_ExcludesNull()
    {
        ResetGrouped(
            "(1, 0, null, 50, 11, 1)",
            "(2, 0, 1, 10, 12, 1)",
            "(3, 0, 1, 2, 13, 1)",
            "(4, 0, 0, 99, 14, 1)",
            "(5, 0, null, 60, 25, 2)",
            "(6, 0, 0, 4, 26, 2)",
            "(7, 0, 0, 8, 27, 2)");
        try
        {
            var max = Source().SelectWhereMax(e => new { e.Nb, e.Sub }, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            max.Select(r => r.Id).Should().ContainInOrder(2, 7);
            max.Should().OnlyContain(r => r.Nb != null, "a NULL value component is never a winner");

            var min = Source().SelectWhereMin(e => new { e.Nb, e.Sub }, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            min.Select(r => r.Id).Should().ContainInOrder(4, 6);
            min.Should().OnlyContain(r => r.Nb != null, "a NULL value component is never a winner");
        }
        finally { Execute($"drop table if exists {TableName}"); }
    }

    /// <summary>
    /// Converted boolean (AC-S4-3 shape not covered before): a model <c>bool</c> stored in an <c>int</c>
    /// column through a <see cref="ValueConverterAttribute"/>. <c>false = 0 &lt; true = 1</c> holds through
    /// the converted storage, scalar and composite, for both directions.
    /// </summary>
    [Fact]
    public void MaxMin_All_ConvertedBool_Scalar_And_Composite2()
    {
        ResetConverted(
            "(1, 1, 2, 11)",
            "(2, 1, 9, 12)",
            "(3, 0, 5, 13)",
            "(4, 0, 7, 14)");
        try
        {
            var scalarMax = ConvertedSource().SelectWhereMax(e => e.Ok, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            scalarMax.Select(r => r.Id).Should().ContainInOrder(1, 2);
            scalarMax.Should().OnlyContain(r => r.Ok, "true = 1 is the maximum of the converted domain");

            var scalarMin = ConvertedSource().SelectWhereMin(e => e.Ok, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            scalarMin.Select(r => r.Id).Should().ContainInOrder(3, 4);
            scalarMin.Should().OnlyContain(r => !r.Ok, "false = 0 is the minimum of the converted domain");

            var compositeMax = ConvertedSource().SelectWhereMax(e => new { e.Ok, e.Sub }, ExtremeRowTies.All)
                .ToList();
            compositeMax.Should().ContainSingle();
            compositeMax[0].Id.Should().Be(2);
            compositeMax[0].Ok.Should().BeTrue();
            compositeMax[0].Sub.Should().Be(9);
            compositeMax.Should().NotContain(r => !r.Ok,
                "false is strictly below true, so no false row may win Max");

            var compositeMin = ConvertedSource().SelectWhereMin(e => new { e.Ok, e.Sub }, ExtremeRowTies.All)
                .ToList();
            compositeMin.Should().ContainSingle();
            compositeMin[0].Id.Should().Be(3);
            compositeMin[0].Ok.Should().BeFalse();
            compositeMin[0].Sub.Should().Be(5);
            compositeMin.Should().NotContain(r => r.Ok,
                "a true row must never win Min while a false row exists");
        }
        finally { Execute($"drop table if exists {ConvertedTableName}"); }
    }

    /// <summary>
    /// Converted boolean as a *grouped* value key (AC-S4-3 shape not covered before): every group gets its
    /// own extremum through the converted storage, scalar and composite, for both directions.
    /// </summary>
    [Fact]
    public void MaxMin_All_Grouped_ConvertedBool_Scalar_And_Composite2()
    {
        ResetConvertedGrouped(
            "(1, 1, 2, 11, 1)",
            "(2, 1, 9, 12, 1)",
            "(3, 0, 5, 13, 1)",
            "(4, 0, 7, 14, 2)",
            "(5, 1, 3, 15, 2)",
            "(6, 0, 4, 16, 2)");
        try
        {
            var scalarMax = ConvertedSource().SelectWhereMax(e => e.Ok, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            scalarMax.Select(r => r.Id).Should().ContainInOrder(2, 5);
            scalarMax.Should().OnlyContain(r => r.Ok, "true = 1 is the per-group maximum");

            var scalarMin = ConvertedSource().SelectWhereMin(e => e.Ok, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            scalarMin.Select(r => r.Id).Should().ContainInOrder(3, 6);
            scalarMin.Should().OnlyContain(r => !r.Ok, "false = 0 is the per-group minimum");

            var compositeMax = ConvertedSource()
                .SelectWhereMax(e => new { e.Ok, e.Sub }, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            compositeMax.Select(r => r.Id).Should().ContainInOrder(2, 5);
            compositeMax.Should().OnlyContain(r => r.Ok && r.Sub != null);

            var compositeMin = ConvertedSource()
                .SelectWhereMin(e => new { e.Ok, e.Sub }, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            compositeMin.Select(r => r.Id).Should().ContainInOrder(3, 6);
            compositeMin.Should().OnlyContain(r => !r.Ok);
        }
        finally { Execute($"drop table if exists {ConvertedTableName}"); }
    }
}

/// <summary>
/// AC-S4-3 "converted boolean": a model <c>bool</c> stored in an <c>int</c> column, so the provider
/// representation itself carries the <c>false &lt; true</c> order.
/// </summary>
public sealed class T302S4BoolToIntConverter : ValueConverter<bool, int>
{
    public override int ConvertToProvider(bool model) => model ? 1 : 0;

    public override bool ConvertFromProvider(int provider) => provider == 1;
}
