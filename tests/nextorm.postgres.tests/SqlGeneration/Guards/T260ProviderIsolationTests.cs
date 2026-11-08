using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;
using NextORM.Sqlite;
using NextORM.SqlServer;

namespace NextORM.Postgres.Tests;

/// <summary>
/// T260 (#260) provider isolation (V19–V24, V27): the new per-name array capability seam defaults to
/// "supported" everywhere, so the PostgreSQL refusal of <c>array_reverse</c>/<c>array_sort</c> and the
/// version gate on <c>uuidv7</c> must stay inside the PostgreSQL dialect. Every other provider keeps the
/// contract it had before T260: its own array surface, its own UUID generators and its existing
/// rejection message for the PostgreSQL-only façade. Pure SQL-generation tests, no database is opened.
/// </summary>
public class T260ProviderIsolationTests
{
    private const string ArrayFamilyRefusal =
        "Arrays are not supported by this provider: array parameters, the any/all array quantifiers and the array functions require PostgreSQL.";

    private const string UuidFamilyRefusal =
        "The UUID generator functions are not supported by this provider.";

    private const string UuidV7Refusal =
        "The uuidv7 function is not supported by this provider.";

    private static readonly long[] Values = [1L, 2L, 3L];

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static void AssertArrayFunctionsRejected(IDataContext ctx)
    {
        var reverse = () => SqlOf(ctx, ctx.From<IComplexEntity>().Select(_ => new { V = SqlFunctions.Postgres.array_reverse(Values) }));
        var sort = () => SqlOf(ctx, ctx.From<IComplexEntity>().Select(_ => new { V = SqlFunctions.Postgres.array_sort(Values) }));

        reverse.Should().Throw<NotSupportedException>().WithMessage(ArrayFamilyRefusal);
        sort.Should().Throw<NotSupportedException>().WithMessage(ArrayFamilyRefusal);
    }

    // V19/V20/V21/V22 — the array refusal stays the pre-T260 family message, never the PostgreSQL
    // per-name message, and the UUID generator answer stays the provider's own.
    [Theory]
    [InlineData("sqlite", UuidFamilyRefusal)]
    [InlineData("sqlserver", UuidV7Refusal)]
    [InlineData("mysql", UuidFamilyRefusal)]
    [InlineData("mariadb", UuidV7Refusal)]
    public void AffectedFunctions_ShouldPreserveProviderContract(string provider, string expectedUuidMessage)
    {
        using IDataContext ctx = provider switch
        {
            "sqlite" => new SqliteDataContext("Data Source=:memory:", new DataContextBuilder()),
            "sqlserver" => new SqlServerDataContext(
                "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=true",
                new DataContextBuilder()),
            "mysql" => new MySqlDataContext(
                "Server=localhost;Database=nextorm;User Id=root;Password=nextorm",
                new DataContextBuilder()),
            "mariadb" => new MariaDbDataContext(
                "Server=localhost;Database=nextorm;User Id=root;Password=nextorm",
                new DataContextBuilder()),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "unknown provider")
        };

        AssertArrayFunctionsRejected(ctx);

        var uuid = () => SqlOf(ctx, ctx.From<IComplexEntity>().Select(_ => new { V = SqlFunctions.Sql.uuidv7() }));
        uuid.Should().Throw<NotSupportedException>().WithMessage(expectedUuidMessage);
    }

    // V23 — ClickHouse keeps both of its own answers: the native array functions render and its UUID
    // generators are unaffected. The PostgreSQL façade keeps its established rejection, so the T260
    // refusal never reaches this dialect.
    [Fact]
    public void ClickHouse_ShouldKeepItsOwnArrayAndUuidSurface()
    {
        using var ctx = new ClickHouseDataContext(
            "Host=localhost;Port=8123;Database=nextorm;Username=default;Password=nextorm",
            new DataContextBuilder());

        SqlOf(ctx, ctx.From<IArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_reverse(x.Tags) }))
            .Should().Contain("arrayReverse(tags)");
        SqlOf(ctx, ctx.From<IArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_sort(x.Tags) }))
            .Should().Contain("arraySort(tags)");
        SqlOf(ctx, ctx.From<IComplexEntity>().Select(_ => new { V = SqlFunctions.Sql.uuidv7() }))
            .Should().Contain("generateUUIDv7()");

        AssertArrayFunctionsRejected(ctx);
    }

    // V24 — the in-memory provider has no SQL dialect and no per-name seam: it keeps evaluating the
    // call client-side (the SQL-only façade stub returns null, which is the pre-existing baseline
    // recorded in the evidence), and above all it never raises the PostgreSQL refusal. Unit-only
    // evidence; the in-memory provider is never integration evidence.
    [Fact]
    public void InMemory_ShouldNotInheritThePostgresRefusal()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<IArrayEntity>().WithData([new ArrayEntityRow { Id = 1, Tags = ["b", "a"] }]);

        var reverse = () => ctx.From<IArrayEntity>()
            .Select(x => new { V = SqlFunctions.Postgres.array_reverse(x.Tags) }).ToList();
        var sort = () => ctx.From<IArrayEntity>()
            .Select(x => new { V = SqlFunctions.Postgres.array_sort(x.Tags) }).ToList();

        reverse.Should().NotThrow<NotSupportedException>("the PostgreSQL per-name refusal must not leak into the in-memory provider");
        sort.Should().NotThrow<NotSupportedException>("the PostgreSQL per-name refusal must not leak into the in-memory provider");
    }

    // V19–V23, V27 — the capability seam defaults to "supported" for every non-PostgreSQL dialect, so
    // only the PostgreSQL override can refuse a name; the family flags keep their existing values.
    [Fact]
    public void NonPostgresDialects_ShouldKeepTheDefaultArrayFunctionCapability()
    {
        ISqlDialect[] dialects =
        [
            SqliteDialect.Instance,
            SqlServerDialect.Instance,
            MySqlDialect.Instance,
            MariaDbDialect.Instance,
            ClickHouseDialect.Instance
        ];

        foreach (var dialect in dialects)
        {
            dialect.SupportsArrayFunction("array_reverse").Should().BeTrue($"{dialect.GetType().Name} has no per-name gate");
            dialect.SupportsArrayFunction("array_sort").Should().BeTrue($"{dialect.GetType().Name} has no per-name gate");
            dialect.SupportsArrayFunction("array_length").Should().BeTrue($"{dialect.GetType().Name} has no per-name gate");
        }

        ClickHouseDialect.Instance.SupportsArrayFunctions.Should().BeTrue("the family flag is unchanged by T260");
        SqlServerDialect.Instance.SupportsArrayFunctions.Should().BeFalse("the family flag is unchanged by T260");
        SqliteDialect.Instance.SupportsArrays.Should().BeFalse("the array family flag is unchanged by T260");
    }

    // V27 — the PostgreSQL override is narrow: an unrelated array name keeps the family default on
    // every configured version, and the family flags themselves are untouched.
    [Fact]
    public void PostgresRefusal_ShouldNotAffectUnrelatedArrayNames()
    {
        foreach (var version in new Version?[] { null, new Version(17, 0), new Version(18, 0) })
        {
            var dialect = new PostgresDialect(version);
            var label = version?.ToString() ?? "unset";

            dialect.SupportsArrayFunction("array_length").Should().BeTrue($"array_length at {label}");
            dialect.SupportsArrayFunction("array_append").Should().BeTrue($"array_append at {label}");
            dialect.SupportsArrayFunction("array_shuffle").Should().BeTrue($"array_shuffle at {label}");
            dialect.SupportsArrayFunction("string_to_array").Should().BeTrue($"string_to_array at {label}");
            dialect.SupportsArrays.Should().BeTrue($"the PostgreSQL array family at {label}");
            dialect.SupportsArrayFunctions.Should().BeFalse($"the ClickHouse-only family flag at {label}");
        }
    }

    private sealed class ArrayEntityRow : IArrayEntity
    {
        public int Id { get; set; }

        public string[] Tags { get; set; } = [];
    }
}
