using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #237 (T237) D-05: real execution coverage for the raw-name / always-safe-quoted identifier
/// contract of <see cref="SqlFunctions.Column{T}"/>. A cross-provider fixture table carries columns
/// whose names require quoting on every provider (a space, a dot and a reserved word); each is read
/// through <c>Column&lt;T&gt;</c> in both quoting modes and the rows must be identical, while a
/// legacy <c>TableAlias</c> raw accessor for a plain name keeps working (negative control).
/// </summary>
/// <remarks>
/// This is a new <c>CommonTestSuite</c> partial (rather than a case in an existing partial) because
/// the whole case is one contract — a fixture table plus its two-mode read and negative control — and
/// it is exercised by the shared suite plus one host below; keeping it in its own file mirrors
/// <c>T232PivotFixture.cs</c>. ClickHouse hosts it directly because (like MariaDB) it has no
/// <see cref="CommonTestSuite"/>-derived integration class; MariaDB reuses the MySQL dialect route and
/// is deliberately not run as a separate database. The injection payload
/// <c>0+(SELECT COUNT(*) FROM sys.tables)</c> cannot exist as a real column on any provider, so its
/// assertion stays at the SQL-generation level (the no-DB <c>IdentifierSafetyTests</c> in the core and
/// per-provider projects) and is not faked here as an execution.
/// </remarks>
public abstract partial class CommonTestSuite
{
    /// <summary>
    /// Reads the quoting-required fixture columns by name in both quoting modes and asserts the rows,
    /// the mode independence and the untouched legacy raw path.
    /// </summary>
    [Fact]
    public void IdentifierSafety_QuotingRequiredNames_ReadByIdenticallyInBothQuotingModes()
        => IdentifierSafetyFixture.AssertQuotingRequiredNamesRead(_sut.DataProvider, Provider.Name);
}

/// <summary>
/// Re-pins the shared identifier-safety case for ClickHouse, which (like MariaDB) has no
/// <see cref="CommonTestSuite"/>-derived integration class and uses its own container harness.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseIdentifierSafetyTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    /// <summary>Executes the shared identifier-safety case against the ClickHouse container.</summary>
    [Fact]
    public void IdentifierSafety_QuotingRequiredNames_ReadByIdenticallyInBothQuotingModes()
        => IdentifierSafetyFixture.AssertQuotingRequiredNamesRead(_sut.DataProvider, Provider.Name);
}

/// <summary>
/// Shared fixture and assertions of the #237 cross-provider identifier-safety case. It owns one table
/// whose column names require quoting on every provider and never asserts beyond the frozen fixture
/// rows; the caller supplies the provider context and name.
/// </summary>
internal static class IdentifierSafetyFixture
{
    /// <summary>The fixture table (one per provider database, created once per provider).</summary>
    internal const string Table = "t237_ident";

    private static readonly object Gate = new();
    private static readonly HashSet<string> Seeded = new();

    /// <summary>The frozen fixture rows, ordered by <c>id</c>.</summary>
    private static readonly (int Id, int Price, int Dotted, int Reserved)[] Rows =
    [
        (1, 41, 71, 11),
        (2, 42, 72, 12)
    ];

    /// <summary>
    /// Creates and seeds the fixture table once per provider, reads the quoting-required columns in
    /// both quoting modes and asserts the frozen rows, their mode independence and the legacy control.
    /// </summary>
    /// <param name="ctx">The provider context under test.</param>
    /// <param name="provider">The provider name (<c>sqlite</c>/<c>sqlserver</c>/<c>postgres</c>/<c>mysql</c>/<c>clickhouse</c>).</param>
    internal static void AssertQuotingRequiredNamesRead(IDataContext ctx, string provider)
    {
        EnsureSeeded(ctx, provider);

        var unquoted = Read(ctx, quoted: false);
        unquoted.Should().Equal(Rows,
            $"{provider}: Column<T> reads quoting-required names in the default (unquoted) mode");

        var quoted = Read(ctx, quoted: true);
        quoted.Should().Equal(Rows,
            $"{provider}: the same rows must be returned with identifier quoting turned on");
        quoted.Should().Equal(unquoted,
            $"{provider}: the by-name identifier is independent of the QuoteIdentifiers option");

        // Negative control: the legacy, option-gated TableAlias raw path for a name that needs no
        // quoting still emits the bare identifier in the default mode and keeps working.
        var ids = ctx.From(Table)
            .Select(t => t.GetInt32("id"))
            .ToList()
            .OrderBy(x => x)
            .ToList();
        ids.Should().Equal(Rows.Select(r => r.Id),
            $"{provider}: the legacy TableAlias raw accessor for a plain name keeps working");
    }

    private static List<(int Id, int Price, int Dotted, int Reserved)> Read(IDataContext ctx, bool quoted)
    {
        var builder = ctx.From(Table);
        if (quoted)
            builder = builder.WithQuotedIdentifiers();

        return builder
            .Select(t => new
            {
                Id = SqlFunctions.Column<int>(t, "id"),
                Price = SqlFunctions.Column<int>(t, "unit price"),
                Dotted = SqlFunctions.Column<int>(t, "a.b"),
                Reserved = SqlFunctions.Column<int>(t, "order")
            })
            .ToList()
            .Select(r => (r.Id, r.Price, r.Dotted, r.Reserved))
            .OrderBy(r => r.Id)
            .ToList();
    }

    private static void EnsureSeeded(IDataContext ctx, string provider)
    {
        lock (Gate)
        {
            if (!Seeded.Add(provider))
                return;

            // The fixture scaffolding quotes the physical names with the provider's own delimiter so
            // the DDL is provider-correct; the assertion never uses it as an oracle (the expectation
            // is the frozen row set and the mode independence above).
            var dialect = ((DataContext)ctx).Dialect;
            var table = dialect.QuoteIdentifier(Table);
            var price = dialect.QuoteIdentifier("unit price");
            var dotted = dialect.QuoteIdentifier("a.b");
            var reserved = dialect.QuoteIdentifier("order");

            Exec(ctx, $"drop table if exists {table}");

            var ddl = provider == "clickhouse"
                ? $"create table {table} (id Int32, {price} Int32, {dotted} Int32, {reserved} Int32) engine = Memory"
                : $"create table {table} (id int not null, {price} int not null, {dotted} int not null, {reserved} int not null)";
            Exec(ctx, ddl);

            Exec(ctx, $"insert into {table} (id, {price}, {dotted}, {reserved}) values (1, 41, 71, 11), (2, 42, 72, 12)");
        }
    }

    private static void Exec(IDataContext ctx, string sql)
    {
        using var _ = ctx.ExecuteRaw(sql);
    }
}
