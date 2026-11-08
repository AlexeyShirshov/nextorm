using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.MariaDb;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.Sqlite;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// T286 (#286): the ClickHouse-only math functions <c>log2</c>, <c>cbrt</c>, <c>exp2</c> and
/// <c>exp10</c> have no in-memory equivalent and no cross-provider spelling, so the in-memory
/// rewriter must refuse both overloads before the marker body is compiled (a compiled marker would
/// return a default value, i.e. silently compute zero). Direct CLR invocation of a marker must throw
/// the provider message. The dialect guards of the other five providers are pinned here because this
/// project references every provider assembly.
/// </summary>
public class T286ClickHouseMathInMemoryTests
{
    private static readonly string[] Names = ["log2", "cbrt", "exp2", "exp10"];

    private readonly InMemoryRepository _sut;

    public T286ClickHouseMathInMemoryTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 } });
    }

    [SqlTable("t286_math_entity")]
    internal interface IT286MathEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("v")]
        double Value { get; set; }
    }

    [Theory]
    [InlineData("log2", false)]
    [InlineData("log2", true)]
    [InlineData("cbrt", false)]
    [InlineData("cbrt", true)]
    [InlineData("exp2", false)]
    [InlineData("exp2", true)]
    [InlineData("exp10", false)]
    [InlineData("exp10", true)]
    public void Query_ShouldFailClosedInMemory(string name, bool nullable)
    {
        Action act = (name, nullable) switch
        {
            ("log2", false) => () => _sut.SimpleEntity.Select(it => SqlFunctions.ClickHouse.log2(it.Id)).ToList(),
            ("log2", true) => () => _sut.SimpleEntity.Select(it => SqlFunctions.ClickHouse.log2((double?)it.Id)).ToList(),
            ("cbrt", false) => () => _sut.SimpleEntity.Select(it => SqlFunctions.ClickHouse.cbrt(it.Id)).ToList(),
            ("cbrt", true) => () => _sut.SimpleEntity.Select(it => SqlFunctions.ClickHouse.cbrt((double?)it.Id)).ToList(),
            ("exp2", false) => () => _sut.SimpleEntity.Select(it => SqlFunctions.ClickHouse.exp2(it.Id)).ToList(),
            ("exp2", true) => () => _sut.SimpleEntity.Select(it => SqlFunctions.ClickHouse.exp2((double?)it.Id)).ToList(),
            ("exp10", false) => () => _sut.SimpleEntity.Select(it => SqlFunctions.ClickHouse.exp10(it.Id)).ToList(),
            ("exp10", true) => () => _sut.SimpleEntity.Select(it => SqlFunctions.ClickHouse.exp10((double?)it.Id)).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not part of the T286 slice")
        };

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"The {name} function is not supported by the in-memory provider.");
    }

    [Theory]
    [InlineData("log2", false)]
    [InlineData("log2", true)]
    [InlineData("cbrt", false)]
    [InlineData("cbrt", true)]
    [InlineData("exp2", false)]
    [InlineData("exp2", true)]
    [InlineData("exp10", false)]
    [InlineData("exp10", true)]
    public void DirectInvocation_ShouldThrowTheProviderMessage(string name, bool nullable)
    {
        var functions = new ClickHouseFunctions();

        Action act = (name, nullable) switch
        {
            ("log2", false) => () => functions.log2(8d),
            ("log2", true) => () => functions.log2((double?)8d),
            ("cbrt", false) => () => functions.cbrt(8d),
            ("cbrt", true) => () => functions.cbrt((double?)8d),
            ("exp2", false) => () => functions.exp2(3d),
            ("exp2", true) => () => functions.exp2((double?)3d),
            ("exp10", false) => () => functions.exp10(2d),
            ("exp10", true) => () => functions.exp10((double?)2d),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not part of the T286 slice")
        };

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"The {name} function is not supported by this provider.");
    }

    [Fact]
    public void EveryOtherProvider_ShouldReportTheFourNamesUnsupported()
    {
        IScalarFunctions[] others =
        [
            PostgresDialect.Instance.ScalarFunctions!,
            SqlServerDialect.Instance.ScalarFunctions!,
            MySqlDialect.Instance.ScalarFunctions!,
            MariaDbDialect.Instance.ScalarFunctions!,
            SqliteDialect.Instance.ScalarFunctions!
        ];

        using (new AssertionScope())
        {
            foreach (var functions in others)
            {
                foreach (var name in Names)
                    functions.Supports(name).Should().BeFalse("'{0}' is ClickHouse-only", name);
            }
        }
    }

    [Fact]
    public void EveryOtherDialect_ShouldRejectTheCallsThroughTheTranslator()
    {
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());
        using var mysql = new MySqlDataContext("Server=localhost;Database=nextorm;User Id=root;Password=nextorm", new DataContextBuilder());
        using var mariaDb = new MariaDbDataContext("Server=localhost;Database=nextorm;User Id=root;Password=nextorm", new DataContextBuilder());
        using var sqlite = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var context in new IDataContext[] { postgres, sqlServer, mysql, mariaDb, sqlite })
            {
                foreach (var name in Names)
                    AssertRejected(context, name);
            }
        }

        static void AssertRejected(IDataContext context, string name)
        {
            Func<string> query = name switch
            {
                "log2" => () => SqlOf(context, context.From<IT286MathEntity>().Select(x => SqlFunctions.ClickHouse.log2(x.Value))),
                "cbrt" => () => SqlOf(context, context.From<IT286MathEntity>().Select(x => SqlFunctions.ClickHouse.cbrt(x.Value))),
                "exp2" => () => SqlOf(context, context.From<IT286MathEntity>().Select(x => SqlFunctions.ClickHouse.exp2(x.Value))),
                "exp10" => () => SqlOf(context, context.From<IT286MathEntity>().Select(x => SqlFunctions.ClickHouse.exp10(x.Value))),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not part of the T286 slice")
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>()
                .WithMessage($"The {name} function is not supported by this provider.");
        }

        static string SqlOf<T>(IDataContext context, QueryCommand<T> command)
            => ((DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
                .DbCommand.CommandText;
    }
}
