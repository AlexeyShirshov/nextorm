using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T323 (#323): historical SQL baseline fixture over the legacy Select/GroupBy/HAVING surface.
/// These are generation-only assertions over the placeholder connection: the API under test is the
/// rendered text, never a connection.
/// <para>
/// The cases are written with the pre-change API shape (<c>Having</c> is only available on the
/// builder, before <c>Select</c>), so this exact file runs unchanged against the base checkout
/// <c>25d262b2</c>, where <c>T323_SQL_MODE=capture</c> writes
/// <c>$T323_SQL_BASELINE_DIR/clickhouse.before.json</c>; <c>compare</c> then asserts the raw
/// strings. With the variable unset only the local legacy invariants below run.
/// </para>
/// </summary>
public class T323LegacySqlBaselineTests
{
    private const string ProviderId = "clickhouse";
    private const string Anchor = "25d262b2";

    private const string PlainSelect = "plain_select";
    private const string GroupScalarCount = "group_scalar_count";
    private const string GroupAnonymousSum = "group_anonymous_sum";
    private const string HavingGrouped = "having_grouped";
    private const string HavingUngrouped = "having_ungrouped";
    private const string GlobalAggregate = "global_aggregate";
    private const string UdfScalar = "udf_scalar";
    private const string GroupOrderBy = "group_orderby";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private static readonly Lazy<Dictionary<string, string>> Cases = new(BuildBaseline);
    private static readonly Lazy<BaselineFile?> Expected = new(ReadBaseline);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    private static string Case(string caseId) => Cases.Value[caseId];

    private static string? Mode()
    {
        var raw = Environment.GetEnvironmentVariable("T323_SQL_MODE");
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    private static string BaselinePath()
    {
        var dir = Environment.GetEnvironmentVariable("T323_SQL_BASELINE_DIR");
        if (string.IsNullOrWhiteSpace(dir))
        {
            throw new InvalidOperationException(
                "T323_SQL_MODE is set but T323_SQL_BASELINE_DIR is not; point it at the baseline directory.");
        }

        if (!Directory.Exists(dir))
        {
            throw new InvalidOperationException(
                $"T323_SQL_BASELINE_DIR '{dir}' does not exist; create it before capturing or comparing.");
        }

        return Path.Combine(dir, ProviderId + ".before.json");
    }

    private static Dictionary<string, string> BuildBaseline()
    {
        var cases = new Dictionary<string, string>(StringComparer.Ordinal);

        using (var ctx = ClickHouseTestContext.Create())
        {
            cases[PlainSelect] = SqlOf(ctx, ctx.From<T323LegacyEntity>().Select(x => new { x.Id, x.Name }));
        }

        using (var ctx = ClickHouseTestContext.Create())
        {
            cases[GroupScalarCount] = SqlOf(ctx, ctx
                .From<T323LegacyEntity>()
                .GroupBy(x => x.Id)
                .Select(x => new { C = SqlFunctions.Sql.count() }));
        }

        using (var ctx = ClickHouseTestContext.Create())
        {
            cases[GroupAnonymousSum] = SqlOf(ctx, ctx
                .From<T323LegacyEntity>()
                .GroupBy(x => new { x.Id, x.Group2 })
                .Select(x => new { x.Id, x.Group2, S = SqlFunctions.Sql.sum(x.Amount) }));
        }

        using (var ctx = ClickHouseTestContext.Create())
        {
            cases[HavingGrouped] = SqlOf(ctx, ctx
                .From<T323LegacyEntity>()
                .GroupBy(x => x.Id)
                .Having(x => SqlFunctions.Sql.sum(x.Amount) > 10)
                .Select(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) }));
        }

        using (var ctx = ClickHouseTestContext.Create())
        {
            cases[HavingUngrouped] = SqlOf(ctx, ctx
                .From<T323LegacyEntity>()
                .Having(x => x.Id > 1)
                .Select(x => new { x.Id }));
        }

        using (var ctx = ClickHouseTestContext.Create())
        {
            cases[GlobalAggregate] = SqlOf(ctx, ctx
                .From<T323LegacyEntity>()
                .Select(x => new { S = SqlFunctions.Sql.sum(x.Amount) }));
        }

        using (var ctx = ClickHouseTestContext.Create())
        {
            cases[UdfScalar] = SqlOf(ctx, ctx
                .From<T323LegacyEntity>()
                .Select(x => new { V = T323LegacyUdf.MyScalar(x.Id) }));
        }

        using (var ctx = ClickHouseTestContext.Create())
        {
            cases[GroupOrderBy] = SqlOf(ctx, ctx
                .From<T323LegacyEntity>()
                .GroupBy(x => x.Id)
                .Select(x => new { x.Id, C = SqlFunctions.Sql.count() })
                .OrderBy(x => x.Id));
        }

        if (Mode() == "capture")
        {
            var file = new BaselineFile { Provider = ProviderId, Anchor = Anchor, Cases = cases };
            File.WriteAllText(BaselinePath(), JsonSerializer.Serialize(file, SerializerOptions));
        }

        return cases;
    }

    private static BaselineFile? ReadBaseline()
    {
        if (Mode() != "compare")
        {
            return null;
        }

        var path = BaselinePath();
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Baseline file '{path}' does not exist; capture it first.");
        }

        var file = JsonSerializer.Deserialize<BaselineFile>(File.ReadAllText(path), SerializerOptions);
        if (file is null)
        {
            throw new InvalidOperationException($"Baseline file '{path}' is empty or not valid JSON.");
        }

        return file;
    }

    private static void CompareCase(string caseId, string actual)
    {
        var expected = Expected.Value;
        if (expected is null)
        {
            return;
        }

        expected.Provider.Should().Be(ProviderId);
        expected.Anchor.Should().Be(Anchor);

        if (!expected.Cases.TryGetValue(caseId, out var sql))
        {
            // A case the dialect cannot render is deliberately absent from the baseline (recorded as a
            // capability limitation); the local invariants are the only assertion it gets.
            return;
        }

        actual.Should().Be(sql);
    }

    [Fact]
    public void PlainSelect_ShouldRenderLegacyProjection()
    {
        var sql = Case(PlainSelect);
        sql.Should().Contain("select");
        CompareCase(PlainSelect, sql);
    }

    [Fact]
    public void GroupScalarCount_ShouldRenderGroupBy()
    {
        var sql = Case(GroupScalarCount);
        sql.Should().Contain("group by");
        CompareCase(GroupScalarCount, sql);
    }

    [Fact]
    public void GroupAnonymousSum_ShouldRenderGroupBy()
    {
        var sql = Case(GroupAnonymousSum);
        sql.Should().Contain("group by");
        CompareCase(GroupAnonymousSum, sql);
    }

    [Fact]
    public void HavingGrouped_ShouldRenderHaving()
    {
        var sql = Case(HavingGrouped);
        sql.Should().Contain("group by");
        sql.Should().Contain("having");
        CompareCase(HavingGrouped, sql);
    }

    [Fact]
    public void HavingUngrouped_ShouldDropHaving()
    {
        var sql = Case(HavingUngrouped);
        sql.Should().NotContain("having");
        CompareCase(HavingUngrouped, sql);
    }

    [Fact]
    public void GlobalAggregate_ShouldRenderSumWithoutGroupBy()
    {
        var sql = Case(GlobalAggregate);
        sql.Should().Contain("sum(");
        sql.Should().NotContain("group by");
        CompareCase(GlobalAggregate, sql);
    }

    [Fact]
    public void UdfScalar_ShouldRenderMappedFunctionName()
    {
        var sql = Case(UdfScalar);
        sql.Should().Contain("t323_my_scalar");
        CompareCase(UdfScalar, sql);
    }

    [Fact]
    public void GroupOrderBy_ShouldRenderGroupBy()
    {
        var sql = Case(GroupOrderBy);
        sql.Should().Contain("group by");
        CompareCase(GroupOrderBy, sql);
    }

    private sealed class BaselineFile
    {
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("anchor")]
        public string Anchor { get; set; } = string.Empty;

        [JsonPropertyName("cases")]
        public Dictionary<string, string> Cases { get; set; } = new(StringComparer.Ordinal);
    }
}

public class T323LegacyEntity
{
    public int Id { get; set; }
    public int? Group2 { get; set; }
    public decimal Amount { get; set; }
    public string? Name { get; set; }
    public DateTime OrderedAt { get; set; }
}

public static class T323LegacyUdf
{
    [SqlFunction("t323_my_scalar")]
    public static int MyScalar(int value) => value;
}
