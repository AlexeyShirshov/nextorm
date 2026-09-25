using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// Runs the MySQL/MariaDB-only native functions against a real MariaDB server (a MariaDB
/// Testcontainers instance unless <c>NEXTORM_MARIADB_CONNECTION</c> points at an existing server).
/// MariaDB inherits the MySQL dialect, so this verifies the inherited native renderings execute.
/// </summary>
public sealed class MariaDbFunctionsIntegrationTests : IDisposable
{
    private readonly IDataContext _ctx;

    public MariaDbFunctionsIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    private EntityBuilder<IFnProbe> Probe => _ctx.From<IFnProbe>();

    [Fact]
    public void FindInSet_ShouldReturnPosition()
    {
        Probe
            .Select(_ => SqlFunctions.MySql.find_in_set("b", "a,b,c"))
            .First()
            .Should().Be(2);

        Probe
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.MySql.find_in_set("dadfasd", "xxx,dadfasd"))
            .First()
            .Should().Be(2);
    }

    [Fact]
    public void FieldEltSubstringIndexFormat_ShouldEvaluate()
    {
        var r = Probe
            .Select(_ => new
            {
                F = SqlFunctions.MySql.field("b", "a", "b"),
                E = SqlFunctions.MySql.elt(2, "a", "b"),
                S = SqlFunctions.MySql.substring_index("a.b.c", ".", 2),
                N = SqlFunctions.MySql.format(1234.5m, 2)
            })
            .First();

        r.F.Should().Be(2);
        r.E.Should().Be("b");
        r.S.Should().Be("a.b");
        r.N.Should().Be("1,234.50");
    }

    [Fact]
    public void DateFunctions_ShouldUsePercentTemplates()
    {
        var r = Probe
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Parsed = SqlFunctions.MySql.str_to_date("2023-01-02", "%Y-%m-%d"),
                Formatted = SqlFunctions.MySql.date_format(x.Dt, "%Y-%m-%d")
            })
            .First();

        r.Parsed.Should().Be(new DateTime(2023, 1, 2));
        r.Formatted.Should().Be("2023-01-01");

        Probe
            .Select(_ => SqlFunctions.MySql.unix_timestamp(SqlFunctions.MySql.from_unixtime(1672617600L)))
            .First()
            .Should().Be(1672617600L);
    }

    [Fact]
    public void Hashes_ShouldReturnHex()
    {
        var r = Probe
            .Select(_ => new
            {
                M = SqlFunctions.MySql.md5("abc"),
                S1 = SqlFunctions.MySql.sha1("abc"),
                S2 = SqlFunctions.MySql.sha2("abc", 256)
            })
            .First();

        r.M.Should().Be("900150983cd24fb0d6963f7d28e17f72");
        r.S1.Should().Be("a9993e364706816aba3e25717850c26c9cd0d89d");
        r.S2.Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void InetConversion_ShouldRoundTrip()
    {
        var r = Probe
            .Select(_ => new
            {
                N = SqlFunctions.MySql.inet_aton("127.0.0.1"),
                A = SqlFunctions.MySql.inet_ntoa(SqlFunctions.MySql.inet_aton("127.0.0.1"))
            })
            .First();

        r.N.Should().Be(2130706433L);
        r.A.Should().Be("127.0.0.1");
    }

    [Fact]
    public void JsonMutation_ShouldTransform()
    {
        Probe.Select(_ => SqlFunctions.MySql.json_set("{\"a\": 1}", "$.b", "2")).First()
            .Should().Contain("\"b\"");

        Probe.Select(_ => SqlFunctions.MySql.json_remove("{\"a\": 1, \"b\": 2}", "$.a")).First()
            .Should().NotContain("\"a\"");

        Probe.Select(_ => SqlFunctions.MySql.json_depth("{\"a\": [1, 2]}")).First()
            .Should().BeGreaterThanOrEqualTo(2);

        Probe.Select(_ => SqlFunctions.MySql.json_length("[1, 2, 3]")).First()
            .Should().Be(3);

        Probe.Select(_ => SqlFunctions.MySql.json_type("{\"a\": 1}")).First()
            .Should().Be("OBJECT");

        Probe.Select(_ => SqlFunctions.MySql.json_keys("{\"a\": 1}")).First()
            .Should().Contain("a");
    }

    [Fact]
    public void UuidFunctions_ShouldBeRejected()
    {
        var act = () => Probe
            .Select(_ => SqlFunctions.MySql.uuid_to_bin("6ccd780c-baba-1026-9564-5b8c656024db"))
            .First();

        act.Should().Throw<NotSupportedException>().WithMessage("*uuid_to_bin*not supported*");
    }

    [Fact]
    public void RegexpFunctions_ShouldEvaluate()
    {
        Probe.Select(_ => SqlFunctions.MySql.regexp_instr("abc", "b")).First().Should().Be(2);
        Probe.Select(_ => SqlFunctions.MySql.regexp_substr("ab12cd", "[0-9]+")).First().Should().Be("12");
        Probe.Select(_ => SqlFunctions.MySql.regexp_replace("ab12cd", "[0-9]", "")).First().Should().Be("abcd");
    }

    [Fact]
    public void NvlFunctions_ShouldEvaluate()
    {
        Probe.Select(_ => SqlFunctions.MySql.nvl<string>(null, "fallback")).First().Should().Be("fallback");
        Probe.Select(_ => SqlFunctions.MySql.nvl2<string>("x", "yes", "no")).First().Should().Be("yes");
    }

    [Fact]
    public void OracleDateFunctions_ShouldEvaluate()
    {
        var r = Probe
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Added = SqlFunctions.MySql.add_months(x.Dt, 2),
                Formatted = SqlFunctions.MySql.to_char(x.Dt, "YYYY-MM-DD")
            })
            .First();

        r.Added.Should().Be(new DateTime(2023, 3, 1, 10, 0, 0));
        r.Formatted.Should().Be("2023-01-01");
    }

    [Fact]
    public void Kdf_ShouldDeriveKey()
    {
        var key = Probe.Select(_ => SqlFunctions.MySql.kdf("foo", "bar", "info", "hkdf")).First();

        key.Should().NotBeNull();
        key!.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void JsonDetailedCompact_ShouldFormat()
    {
        Probe.Select(_ => SqlFunctions.MySql.json_compact("{ \"a\" : 1 }")).First()
            .Should().Be("{\"a\":1}");

        Probe.Select(_ => SqlFunctions.MySql.json_detailed("{\"a\":1}")).First()
            .Should().Contain("\n");
    }

    [Fact]
    public void SequenceFunctions_ShouldEvaluate()
    {
        Probe.Select(_ => SqlFunctions.MySql.nextval("fn_seq")).First().Should().Be(100);
        Probe.Select(_ => SqlFunctions.MySql.next_value_for("fn_seq")).First().Should().Be(110);
        Probe.Select(_ => SqlFunctions.MySql.setval("fn_seq", 500)).First().Should().Be(500);
        Probe.Select(_ => SqlFunctions.MySql.nextval("fn_seq")).First().Should().Be(510);
        Probe.Select(_ => SqlFunctions.MySql.lastval("fn_seq")).First().Should().Be(510);
    }

    private void Seed()
    {
        Execute("drop table if exists fn_probe");
        Execute("create table fn_probe (id int not null primary key, s varchar(100), dt datetime)");
        Execute("insert into fn_probe (id, s, dt) values (1, 'dadfasd', '2023-01-01 10:00:00')");
        Execute("drop sequence if exists fn_seq");
        Execute("create sequence fn_seq start with 100 increment by 10");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("fn_probe")]
internal interface IFnProbe
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("s")]
    string? S { get; set; }
    [Column("dt")]
    DateTime? Dt { get; set; }
}
