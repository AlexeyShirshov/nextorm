using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using Npgsql;

namespace NextORM.Integration.Tests;

[Collection("Postgres")]
public sealed class PostgresStep2UnitATests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void MissingSupportsNames_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Ascii = SqlFunctions.Postgres.ascii("A"),
                BitLength = SqlFunctions.Postgres.bit_length("abc"),
                Char = SqlFunctions.Postgres.@char(65),
                Cot = SqlFunctions.Postgres.cot(1.0),
                OctetLength = SqlFunctions.Postgres.octet_length("abc"),
                Space = SqlFunctions.Postgres.space(3),
                CurrentXact = SqlFunctions.Postgres.pg_current_xact_id_if_assigned()
            })
            .First();

        r.Ascii.Should().Be(65);
        r.BitLength.Should().Be(24);
        r.Char.Should().Be("A");
        r.Cot.Should().BeApproximately(Math.Cos(1) / Math.Sin(1), 1e-12);
        r.OctetLength.Should().Be(3);
        r.Space.Should().Be("   ");
        r.CurrentXact.Should().BeNull();
    }

    [Fact]
    public void PadTwoArguments_ShouldUseDefaultSpace()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Left = SqlFunctions.Postgres.lpad("abc", 5),
                Right = SqlFunctions.Postgres.rpad("abc", 5)
            })
            .First();

        r.Left.Should().Be("  abc");
        r.Right.Should().Be("abc  ");
    }

    [Fact]
    public void ArityVariants_ShouldCompute()
    {
        var ctx = (DataContext)_sut.DataProvider;
        var indexOid = Scalar(ctx, "select oid from pg_class where relname = 'pg_class_oid_index'");
        var ruleOid = Scalar(ctx, "select oid from pg_rewrite where ev_class = 'pg_stat_activity'::regclass limit 1");
        var me = Scalar(ctx, "select current_user");

        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Index3 = SqlFunctions.Postgres.pg_get_indexdef(indexOid, 1, true),
                Rule2 = SqlFunctions.Postgres.pg_get_ruledef(ruleOid, true),
                Trigger1 = SqlFunctions.Postgres.pg_get_triggerdef("0"),
                Trigger2 = SqlFunctions.Postgres.pg_get_triggerdef("0", true),
                Has2 = SqlFunctions.Postgres.pg_has_role("pg_read_all_stats", "USAGE"),
                Has3 = SqlFunctions.Postgres.pg_has_role(me, me, "USAGE"),
                Json2 = SqlFunctions.Postgres.json_to_tsvector("{\"a\":\"x y\"}", "[\"string\"]"),
                Json3 = SqlFunctions.Postgres.json_to_tsvector("english", "{\"a\":\"x y\"}", "[\"string\"]"),
                SetWeight2 = SqlFunctions.Postgres.setweight(SqlFunctions.Postgres.to_tsvector("foo"), "A"),
                SetWeight3 = SqlFunctions.Postgres.setweight(
                    SqlFunctions.Parameter<string>(0), SqlFunctions.Parameter<string>(1),
                    SqlFunctions.Parameter<string?[]>(2)),
                Phrase2 = SqlFunctions.Postgres.tsquery_phrase(
                    SqlFunctions.Postgres.to_tsquery("foo"), SqlFunctions.Postgres.to_tsquery("bar")),
                Phrase3 = SqlFunctions.Postgres.tsquery_phrase(
                    SqlFunctions.Postgres.to_tsquery("foo"), SqlFunctions.Postgres.to_tsquery("bar"), 3)
            })
            .First(new object?[]
            {
                "'a':1 'b':2", "A", new string?[] { "a" }
            });

        r.Index3.Should().Contain("oid", "column 1 of the oid index is the oid column itself");
        r.Rule2.Should().StartWith("CREATE RULE \"_RETURN\" AS");
        r.Trigger1.Should().BeNull("OID 0 is not a trigger");
        r.Trigger2.Should().BeNull("OID 0 is not a trigger");
        r.Has2.Should().BeTrue("a superuser has USAGE on every role");
        r.Has3.Should().BeTrue("a role always has USAGE on itself");
        r.Json2.Should().Be("'x':1 'y':2");
        r.Json3.Should().Be("'x':1 'y':2");
        r.SetWeight2.Should().Be("'foo':1A");
        r.SetWeight3.Should().Be("'a':1A 'b':2");
        r.Phrase2.Should().Be("'foo' <-> 'bar'");
        r.Phrase3.Should().Be("'foo' <3> 'bar'");
    }

    private static string Scalar(DataContext ctx, string sql)
    {
        ctx.EnsureConnectionOpen();
        using var cmd = ctx.CreateCommand(sql);
        return Convert.ToString(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture)!;
    }
}
