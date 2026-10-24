using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// #327 D8 (AC-R5/AC-R6, §8 ClickHouse live matrix): ClickHouse does not derive
/// <see cref="CommonTestSuite"/>, so its live literal corpus is pinned here. It covers the inline and
/// captured-parameter forms for the whole corpus (adjacent/trailing backslashes and every C0 code point
/// included), the default LIKE backslash escaping against the documented native semantics, and the
/// grammar constraint that ClickHouse rejects the <c>ESCAPE</c> clause in every spelling: the default
/// escape character omits the clause, a custom one throws the product's
/// <see cref="NotSupportedException"/>.
/// </summary>
public sealed class ClickHouseStringLiteralSafetyExecutionTests : ProviderTestSuite
{
    private const string ProbeTable = "sls_ch_probe";

    private static long _probeId = DateTime.UtcNow.Ticks;
    private static int _probeMarker = Random.Shared.Next(3_000_000, 4_000_000);

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext DataProvider => _sut.DataProvider;

    [Fact]
    [Trait("Issue", "327")]
    public void ClickHouse_InlineAndParameter_ShouldRoundTripEveryCase()
    {
        foreach (var literal in StringLiteralSafetyCases.Strings)
        {
            AssertExact(InlineText(literal.Value), literal.Value, $"ClickHouse inline literal case '{literal.Id}'");
            AssertExact(CapturedText(literal.Value), literal.Value, $"ClickHouse captured parameter case '{literal.Id}'");
        }

        foreach (var single in StringLiteralSafetyCases.Chars)
        {
            var inline = InlineChar(single.Value);
            inline.Should().Be(single.Value, $"ClickHouse inline char case '{single.Id}'");
            Encoding.UTF8.GetBytes(inline.ToString()).Should()
                .Equal(Encoding.UTF8.GetBytes(single.Value.ToString()), $"ClickHouse inline char bytes case '{single.Id}'");

            var captured = CapturedChar(single.Value);
            captured.Should().Be(single.Value, $"ClickHouse captured char case '{single.Id}'");
            Encoding.UTF8.GetBytes(captured.ToString()).Should()
                .Equal(Encoding.UTF8.GetBytes(single.Value.ToString()), $"ClickHouse captured char bytes case '{single.Id}'");
        }
    }

    [Fact]
    [Trait("Issue", "327")]
    public void ClickHouse_BackslashAdjacentToApostropheAndTrailingBackslash_ShouldBeExact()
    {
        string[] values = ["\\", "\\\\", "\\'", "\\\\'", "'\\", "a\\", "a\\\\", "a\\b", "a\0'\\😀 "];

        foreach (var value in values)
        {
            AssertExact(InlineText(value), value, "ClickHouse inline adjacent/trailing backslash");
            AssertExact(CapturedText(value), value, "ClickHouse captured adjacent/trailing backslash");
        }
    }

    [Fact]
    [Trait("Issue", "327")]
    public void ClickHouse_Like_ShouldUseDefaultBackslashEscapingWithoutAnEscapeClause()
    {
        RecreateProbeTable();
        var marker = NextProbeMarker();
        var ids = SeedLikeRows(marker);

        foreach (var pattern in StringLiteralSafetyLike.Patterns)
        {
            var expected = pattern.Matches.Select(value => ids[value]).OrderBy(id => id).ToList();
            LikeInline(marker, pattern).Should().Equal(expected,
                $"ClickHouse's default LIKE escaping for '{pattern.Id}'");
            LikeCaptured(marker, pattern).Should().Equal(expected,
                $"ClickHouse's captured LIKE pattern for '{pattern.Id}'");
        }

        // Grammar: the default escape character omits the ESCAPE clause entirely.
        var command = DataProvider.From<IClickHouseStringLiteralProbe>()
            .Where(StringLiteralSafetyLike.InlinePatternPredicate<IClickHouseStringLiteralProbe>(
                nameof(IClickHouseStringLiteralProbe.Marker),
                nameof(IClickHouseStringLiteralProbe.Svalue),
                marker,
                "dad%"))
            .Select(x => x.Id);

        var prepared = (DbPreparedQueryCommand<long>)((DataContext)DataProvider).GetPreparedQueryCommand(
            command, createEnumerator: false, storeInCache: false, CancellationToken.None);
        prepared.DbCommand.CommandText.Should().NotContainEquivalentOf("escape",
            "ClickHouse's parser rejects every ESCAPE spelling, so none may be emitted");

        // A custom escape character cannot be represented: fail closed with the product's message.
        var custom = () => DataProvider.From<IClickHouseStringLiteralProbe>()
            .Where(x => x.Marker == marker && SqlFunctions.Sql.like(x.Svalue, "%", "!"))
            .Select(x => x.Id)
            .ToList();

        custom.Should().Throw<NotSupportedException>()
            .WithMessage("*ClickHouse*ESCAPE*");
    }

    private string? InlineText(string? value)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "e");
        var projection = Expression.Lambda<Func<IComplexEntity, string?>>(
            Expression.Constant(value, typeof(string)), parameter);

        return _sut.ComplexEntity.Limit(1).Select(projection).First();
    }

    private string? CapturedText(string? value)
    {
        var captured = value;
        return _sut.ComplexEntity.Limit(1).Select(e => captured).First();
    }

    private char InlineChar(char value)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "e");
        var projection = Expression.Lambda<Func<IComplexEntity, char>>(
            Expression.Constant(value, typeof(char)), parameter);

        return _sut.ComplexEntity.Limit(1).Select(projection).First();
    }

    private char CapturedChar(char value)
    {
        var captured = value;
        return _sut.ComplexEntity.Limit(1).Select(e => captured).First();
    }

    private void RecreateProbeTable()
    {
        Execute($"drop table if exists {ProbeTable}");
        Execute($"create table {ProbeTable} (id Int64, marker Int32, svalue Nullable(String)) engine = Memory");
        Execute($"insert into {ProbeTable} (id, marker, svalue) values (1, 0, 'seed-row')");
    }

    private void Execute(string sql)
    {
        var context = (DataContext)DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private Dictionary<string, long> SeedLikeRows(int marker)
    {
        var ids = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (_, value) in StringLiteralSafetyLike.LikeRows)
        {
            var rowId = NextProbeId();
            ids.Add(value, rowId);
            DataProvider.CreateInsertBuilder<IClickHouseStringLiteralProbe>()
                .Value(x => x.Id, rowId)
                .Value(x => x.Marker, marker)
                .Value(x => x.Svalue, value)
                .Insert();
        }

        return ids;
    }

    private List<long> LikeInline(int marker, LikePatternCase pattern)
        => DataProvider.From<IClickHouseStringLiteralProbe>()
            .Where(StringLiteralSafetyLike.InlinePatternPredicate<IClickHouseStringLiteralProbe>(
                nameof(IClickHouseStringLiteralProbe.Marker),
                nameof(IClickHouseStringLiteralProbe.Svalue),
                marker,
                pattern.Pattern,
                pattern.ExplicitEscape))
            .Select(x => x.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

    private List<long> LikeCaptured(int marker, LikePatternCase pattern)
    {
        var captured = pattern.Pattern;

        return pattern.ExplicitEscape
            ? DataProvider.From<IClickHouseStringLiteralProbe>()
                .Where(x => x.Marker == marker && SqlFunctions.Sql.like(x.Svalue, captured, "\\"))
                .Select(x => x.Id)
                .ToList()
                .OrderBy(id => id)
                .ToList()
            : DataProvider.From<IClickHouseStringLiteralProbe>()
                .Where(x => x.Marker == marker && SqlFunctions.Sql.like(x.Svalue, captured))
                .Select(x => x.Id)
                .ToList()
                .OrderBy(id => id)
                .ToList();
    }

    private static void AssertExact(string? actual, string expected, string because)
    {
        actual.Should().Be(expected, because);
        Encoding.UTF8.GetBytes(actual!).Should().Equal(Encoding.UTF8.GetBytes(expected), because);
    }

    private static long NextProbeId() => Interlocked.Increment(ref _probeId);

    private static int NextProbeMarker() => Interlocked.Increment(ref _probeMarker);
}
