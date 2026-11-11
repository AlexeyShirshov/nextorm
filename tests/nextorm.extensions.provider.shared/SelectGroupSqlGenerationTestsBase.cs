using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using Xunit;

namespace NextORM.Extensions.ProviderTests;

/// <summary>
/// Exact-SQL oracles for the packaged grouping operators on every provider. Each provider supplies its
/// own complete expected statement (see <see cref="Expected"/>); no dialect is normalized into another
/// dialect's text. Every fact compares the whole rendered statement and, where a value is captured, the
/// bound parameter collection (names and values), including the provider representation a converter
/// produces.
/// </summary>
public abstract class SelectGroupSqlGenerationTestsBase
{
    /// <summary>Creates the default placeholder-connection context of the dialect under test.</summary>
    protected abstract IDataContext CreateContext();

    /// <summary>Creates a placeholder-connection context with quoted identifiers.</summary>
    protected abstract IDataContext CreateQuotedContext();

    /// <summary>The complete expected SQL string of each scenario, as this dialect renders it.</summary>
    protected abstract SelectGroupSqlExpectations Expected { get; }

    /// <summary>A converter-mapped scalar: the model string is stored upper-cased.</summary>
    public sealed class UpperCodeConverter : ValueConverter<string, string>
    {
        /// <inheritdoc/>
        public override string? ConvertToProvider(string? model) => model?.ToUpperInvariant();

        /// <inheritdoc/>
        public override string? ConvertFromProvider(string? provider) => provider;
    }

    /// <summary>The generation-only row shape; no connection is ever opened.</summary>
    protected sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public decimal Amount { get; set; }
        public int Group2 { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("order")]
        public int Order { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("code")]
        [ValueConverter(typeof(UpperCodeConverter))]
        public string? Code { get; set; }
    }

    private static (string Sql, string Parameters) Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var sql = (prepared.SqlStmt ?? string.Empty).Replace("\r\n", "\n");
        var parameters = string.Join(
            ";",
            prepared.DbCommand.Parameters.Cast<DbParameter>()
                .Select(p => p.ParameterName + "=" + (p.Value is DBNull ? "NULL" : p.Value)));
        return (sql, parameters);
    }

    /// <summary>The expected literal uses the file's line endings; generated SQL always uses LF.</summary>
    private static string Lf(string sql) => sql.ReplaceLineEndings("\n");

    [Fact]
    public void ThreeKeyGroupBy_ExactSql()
    {
        using var ctx = CreateContext();
        var (sql, parameters) = Prepare(ctx, ctx.From<Entity>().SelectGroupHaving(
            x => new { x.Category, x.Group2, x.Score, Total = SqlFunctions.Sql.count(x.Id) },
            r => r.Total > 5));

        sql.Should().Be(Lf(Expected.ThreeKeyGroupBy), "the whole statement, key list and order included");
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void GlobalAggregation_Having_ExactSql()
    {
        using var ctx = CreateContext();
        var (sql, parameters) = Prepare(ctx, ctx.From<Entity>().SelectGroupHaving(
            x => new { Total = SqlFunctions.Sql.count(x.Id) },
            r => r.Total > 5));

        sql.Should().Be(Lf(Expected.GlobalHaving), "a zero-key aggregate keeps a real HAVING and no GROUP BY");
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void ConstantProjection_WhereAndDistinct_ExactSql()
    {
        using var ctx = CreateContext();
        var (sql, parameters) = Prepare(ctx, ctx.From<Entity>()
            .Where(x => x.Id > 5)
            .SelectGroup(x => new { V = 1 }));

        sql.Should().Be(Lf(Expected.ConstantDistinct), "the constant predicate is folded into WHERE and DISTINCT is applied");
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void ConverterMappedKey_ExactSqlAndProviderValue()
    {
        using var ctx = CreateContext();
        var state = "active";
        var (sql, parameters) = Prepare(ctx, ctx.From<Entity>().SelectGroupHaving(
            x => new { x.Code, Total = SqlFunctions.Sql.count(x.Id) },
            r => r.Code == state));

        sql.Should().Be(Lf(Expected.ConverterKey), "a converter-mapped scalar is an ordinary key column");
        parameters.Should().Be("state=ACTIVE", "the converter maps the model value to the provider representation");
    }

    [Fact]
    public void ReservedWordKey_QuotedIdentifiers_ExactSql()
    {
        using var quoted = CreateQuotedContext();
        var (sql, parameters) = Prepare(quoted, quoted.From<Entity>()
            .SelectGroup(x => new { x.Order, Total = SqlFunctions.Sql.count(x.Id) }));

        sql.Should().Be(Lf(Expected.ReservedKeyQuoted), "the reserved-word column and every identifier are quoted");
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void SelectGroupHaving_TwoParameters_ExactSqlAndParameters()
    {
        using var ctx = CreateContext();
        var skip = "x";
        var floor = 5;
        var (sql, parameters) = Prepare(ctx, ctx.From<Entity>().SelectGroupHaving(
            x => new { x.Category, Total = SqlFunctions.Sql.count(x.Id) },
            r => r.Category != skip && r.Total > floor));

        sql.Should().Be(Lf(Expected.TwoParameters), "HAVING carries both predicates in order");
        parameters.Should().Be("skip=x;floor=5", "the bound parameter names and values, in occurrence order");
    }

    [Fact]
    public void NullableKey_ExactSql()
    {
        using var ctx = CreateContext();
        var (sql, parameters) = Prepare(ctx, ctx.From<Entity>()
            .SelectGroup(x => new { x.Score, Total = SqlFunctions.Sql.count(x.Id) }));

        sql.Should().Be(Lf(Expected.NullableKey));
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void OrderByAfterHaving_ExactSql()
    {
        using var ctx = CreateContext();
        var (sql, parameters) = Prepare(ctx, ctx.From<Entity>()
            .OrderBy(x => x.Category)
            .SelectGroupHaving(
                x => new { x.Category, Total = SqlFunctions.Sql.count(x.Id) },
                r => r.Total > 5));

        sql.Should().Be(Lf(Expected.Ordering), "ORDER BY renders after GROUP BY and HAVING");
        parameters.Should().BeEmpty();
    }
}

/// <summary>
/// The complete expected statement of each scenario for one provider. Every value is that dialect's real
/// rendering; two dialects are never collapsed into one string. A dialect that cannot render a scenario
/// exactly documents the limitation in the oracle map instead of weakening the comparison.
/// </summary>
/// <param name="ThreeKeyGroupBy">Three keys in first-appearance order plus an aggregate and a HAVING.</param>
/// <param name="GlobalHaving">Zero keys, a global aggregate and a real HAVING.</param>
/// <param name="ConstantDistinct">Constant projection: pre-group WHERE and <c>select distinct</c>.</param>
/// <param name="ConverterKey">A converter-mapped string scalar as a key and a HAVING on it.</param>
/// <param name="ReservedKeyQuoted">A reserved-word column as a key under quoted identifiers.</param>
/// <param name="TwoParameters">A HAVING with two captured parameters.</param>
/// <param name="NullableKey">A nullable column as the only key.</param>
/// <param name="Ordering">ORDER BY over the grouped result after HAVING.</param>
public sealed record SelectGroupSqlExpectations(
    string ThreeKeyGroupBy,
    string GlobalHaving,
    string ConstantDistinct,
    string ConverterKey,
    string ReservedKeyQuoted,
    string TwoParameters,
    string NullableKey,
    string Ordering);
