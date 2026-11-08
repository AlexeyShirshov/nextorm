using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

public sealed class MariaDbStep2UnitBTests : IDisposable
{
    private readonly MariaDbDataContext? _ctx;

    public MariaDbStep2UnitBTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Execute("drop temporary table if exists mdfn_probe");
        Execute("create temporary table mdfn_probe (id int not null primary key)");
        Execute("insert into mdfn_probe (id) values (1)");
    }

    [Fact]
    public void InheritedJsonNames_ShouldMatchServerEvaluation()
    {
        var ctx = (DataContext)_ctx!;

        Vendor(ctx, "select json_insert('{\"a\":1}','$.b','2')").Should().Be(
            Project(_ctx!, x => SqlFunctions.MySql.json_insert("{\"a\":1}", "$.b", "2")));

        Vendor(ctx, "select json_replace('{\"a\":1}','$.a','9')").Should().Be(
            Project(_ctx!, x => SqlFunctions.MySql.json_replace("{\"a\":1}", "$.a", "9")));

        Vendor(ctx, "select json_merge_patch('{\"a\":1}','{\"a\":2}')").Should().Be(
            Project(_ctx!, x => SqlFunctions.MySql.json_merge_patch("{\"a\":1}", "{\"a\":2}")));

        Vendor(ctx, "select json_merge_preserve('{\"a\":1}','{\"a\":2}')").Should().Be(
            Project(_ctx!, x => SqlFunctions.MySql.json_merge_preserve("{\"a\":1}", "{\"a\":2}")));

        Vendor(ctx, "select json_array_append('[1]','$','2')").Should().Be(
            Project(_ctx!, x => SqlFunctions.MySql.json_array_append("[1]", "$", "2")));

        Vendor(ctx, "select json_array_insert('[1,3]','$[1]','2')").Should().Be(
            Project(_ctx!, x => SqlFunctions.MySql.json_array_insert("[1,3]", "$[1]", "2")));
    }

    [Fact]
    public void LpadTwoArguments_ShouldUseDefaultSpace()
    {
        var left = Project(_ctx!, x => SqlFunctions.Sql.lpad("abc", 5));
        var right = Project(_ctx!, x => SqlFunctions.Sql.rpad("abc", 5));

        left.Should().Be("  abc");
        right.Should().Be("abc  ");
    }

    [Fact]
    public void Grouping_ShouldBeRefusedByCapabilityGate()
    {
        FluentActions.Invoking(() => Project(_ctx!, x => SqlFunctions.MySql.grouping(1L)))
            .Should().Throw<NotSupportedException>().WithMessage("*grouping*not supported*");
    }

    /// <summary>
    /// <c>xxh3</c>/<c>xxh32</c> are the only render arms of <c>MariaDbNativeFunctions</c> the pinned 11.4
    /// server cannot execute, and they are the only arms the baseline coverage never entered. A dialect
    /// built for the declared 13.1 minimum opens the gate, so the rendered call really reaches the server;
    /// the 11.4 server answers 1305 ("FUNCTION … does not exist"). The unversioned target refuses the same
    /// call before any SQL, which is the boundary the gate exists to enforce.
    /// </summary>
    /// <param name="name">The gated surface member name.</param>
    [Theory]
    [InlineData("xxh3")]
    [InlineData("xxh32")]
    public void VersionGatedXxhNames_OnA13_1Context_ShouldReachTheServerAndSurfaceThe1305Refusal(string name)
    {
        Func<MariaDbDataContext, object?> translate = name switch
        {
            "xxh3" => c => Project<ulong?>(c, x => SqlFunctions.MySql.xxh3("abc")),
            "xxh32" => c => Project<long?>(c, x => SqlFunctions.MySql.xxh32("abc")),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown xxh name.")
        };

        FluentActions.Invoking(() => translate(_ctx!))
            .Should().Throw<NotSupportedException>(
                $"{name} must be refused before any SQL on the unversioned 11.4 target")
            .WithMessage($"*The {name} function is not supported by this provider.*");

        using var versioned = new MariaDbDataContext(
            MariaDbContainer.ConnectionString, new DataContextBuilder(), new Version(13, 1));

        // The versioned context owns its own connection: the probe table must exist there, so a later
        // 1146 cannot masquerade as the absent function.
        ExecuteOn(versioned, "drop temporary table if exists mdfn_probe");
        ExecuteOn(versioned, "create temporary table mdfn_probe (id int not null primary key)");
        ExecuteOn(versioned, "insert into mdfn_probe (id) values (1)");

        Project<int?>(versioned, x => SqlFunctions.MySql.lengthb("abc"))
            .Should().Be(3, "the probe table is readable on this connection, so the failure below is about the function");

        var refusal = FluentActions.Invoking(() => translate(versioned))
            .Should().Throw<MySqlConnector.MySqlException>(
                $"a 13.1 dialect opens the {name} gate, so the statement reaches the 11.4 server, which lacks it")
            .Which;

        refusal.Number.Should().Be(1305, "with the probe table present, 1305 is the server's 'FUNCTION … does not exist' refusal");
        refusal.Message.Should().Contain(name, "the refusal must name the missing function");
    }

    public void Dispose()
    {
        if (_ctx is null)
            return;

        try
        {
            Execute("drop temporary table if exists mdfn_probe");
        }
        finally
        {
            _ctx.Dispose();
        }
    }

    private static T Project<T>(MariaDbDataContext ctx, System.Linq.Expressions.Expression<Func<MdfnProbe, T>> projection) =>
        ctx.From<MdfnProbe>().Where(x => x.Id == 1).Select(projection).ToList()[0]!;

    private object? Vendor(DataContext ctx, string sql)
    {
        ctx.EnsureConnectionOpen();
        using var cmd = ctx.CreateCommand(sql);
        var v = cmd.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    private void Execute(string sql) => ExecuteOn(_ctx!, sql);

    private static void ExecuteOn(MariaDbDataContext context, string sql)
    {
        var ctx = (DataContext)context;
        ctx.EnsureConnectionOpen();
        using var command = ctx.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    [SqlTable("mdfn_probe")]
    public sealed class MdfnProbe
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
    }
}
