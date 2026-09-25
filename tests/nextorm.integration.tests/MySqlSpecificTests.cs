using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tests that assert MySQL specific behaviour, backed by a Testcontainers instance unless
/// NEXTORM_MYSQL_CONNECTION points at an existing server.
/// </summary>
public sealed class MySqlSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    [Fact]
    public void InfoFunctions_ShouldReturnServerValues()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                User = SqlFunctions.Sql.current_user(),
                Session = SqlFunctions.Sql.session_user(),
                Schema = SqlFunctions.Sql.current_schema(),
                Db = SqlFunctions.Sql.current_database(),
                Ver = SqlFunctions.Sql.version()
            })
            .First();

        r.User.Should().NotBeNullOrEmpty();
        r.Session.Should().NotBeNullOrEmpty();
        r.Schema.Should().NotBeNullOrEmpty();
        r.Db.Should().NotBeNullOrEmpty();
        r.Ver.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AnyValueAggregate_ShouldReturnGroupValue()
    {
        var value = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Sql.any_agg(x.RequiredString) })
            .First();

        value.V.Should().Be("sdf");
    }

    [Fact]
    public void Stdev_ShouldMatchSampleStandardDeviation()
    {
        _sut.SimpleEntity
            .Select(x => SqlFunctions.Sql.stdev((double)x.Id))
            .First()
            .Should().BeApproximately(3.0276503540974917, 1e-12);
    }

    [Fact]
    public void StringConcat_ShouldUseConcatFunction()
    {
        // MySQL's infix || is a logical OR, so the provider must render concat(...).
        var value = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => x.String + "/" + x.RequiredString)
            .First();

        value.Should().Be("dadfasd/sdf");
    }

    [Fact]
    public void OrderByAscending_NullsFirst_ShouldSortData()
    {
        // MySQL sorts NULL first ascending; the row with the NULL nullableint is id 1.
        var r = _sut.ComplexEntity.OrderBy(it => it.Int).Select(it => new { it.Id }).ToList();

        r[0].Id.Should().Be(1);
    }

    [Fact]
    public void Offset_WithoutLimit_ShouldReturnRemainingRows()
    {
        var ids = _sut.SimpleEntity.Offset(8).Select(it => it.Id).ToList();

        ids.Should().Equal(9, 10);
    }

    [Fact]
    public void QueryHint_ShouldEmitOptimizerHintAndReturnRows()
    {
        var ids = _sut.SimpleEntity
            .Select(it => it.Id)
            .Hint("MAX_EXECUTION_TIME(1000)")
            .ToList();

        ids.Should().NotBeEmpty();
    }

    [Fact]
    public void DateAdd_ShouldShiftDate()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_add("day", 1, x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
    }

    [Fact]
    public void DateDiff_ShouldCountDayBoundaries()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_diff("day", x.Datetime, SqlFunctions.Sql.date_add("day", 3, x.Datetime)))
            .First();

        r.Should().Be(3);
    }

    [Fact]
    public void EndOfMonth_ShouldReturnLastDay()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.end_of_month(x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 31));
    }

    [Fact]
    public void StringAgg_ShouldConcatenateGroupValues()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.String != null)
            .Select(x => SqlFunctions.Sql.string_agg(x.String, ","))
            .First();

        r.Should().NotBeNull();
        r.Should().Contain("dadfasd").And.Contain("xxx");
    }

    [Fact]
    public void DateProjection_ShouldMaterialiseColumns()
    {
        // Exercises the row mapper (not the scalar path): the wider/native provider types have to
        // land in the DateTime?/int? projection members.
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Added = SqlFunctions.Sql.date_add("day", 1, x.Datetime),
                Days = SqlFunctions.Sql.date_diff("day", x.Datetime, SqlFunctions.Sql.date_add("day", 3, x.Datetime)),
                Last = SqlFunctions.Sql.end_of_month(x.Datetime)
            })
            .First();

        r.Added.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
        r.Days.Should().Be(3);
        r.Last.Should().Be(new DateTime(2023, 1, 31));
    }

    [Fact]
    public void JsonValue_ShouldExtractScalar()
    {
        var r = _sut.SimpleEntity
            .Select(_ => SqlFunctions.SqlServer.json_value("{\"id\": 7}", "$.id"))
            .First();

        r.Should().Be("7");
    }

    [Fact]
    public void JsonQuery_ShouldReturnFragment()
    {
        var r = _sut.SimpleEntity
            .Select(_ => SqlFunctions.SqlServer.json_query("{\"name\": \"bob\"}", "$.name"))
            .First();

        r.Should().Contain("bob");
    }

    [Fact]
    public void JsonModify_ShouldReplaceValue()
    {
        var r = _sut.SimpleEntity
            .Select(_ => SqlFunctions.SqlServer.json_modify("{\"name\": \"a\"}", "$.name", "b"))
            .First();

        r.Should().Contain("b");
    }

    [Fact]
    public void IsJson_ShouldDetectValidJson()
    {
        _sut.SimpleEntity
            .Select(_ => SqlFunctions.SqlServer.isjson("{\"id\": 1}"))
            .First()
            .Should().BeTrue();

        _sut.SimpleEntity
            .Select(_ => SqlFunctions.SqlServer.isjson("not json"))
            .First()
            .Should().BeFalse();
    }

    [Fact]
    public void LastIndexOf_ShouldReturnZeroBasedLastPosition()
    {
        // "dadfasd" has its last 'd' at index 6.
        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.String!.LastIndexOf("d"))
            .First()
            .Should().Be(6);
    }

    [Fact]
    public void UInt64Columns_ShouldMaterializeAsUlong()
    {
        // BIGINT UNSIGNED has no signed CLR counterpart, so it is read through the row reader's
        // DbDataReader.GetFieldValue<ulong> accessor shared with ClickHouse UInt64.
        var rows = _sut.DataProvider
            .From<IMySqlUInt64Entity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Value })
            .ToList();

        rows.Should().HaveCount(3);
        rows[0].Id.Should().Be(1UL);
        rows[0].Value.Should().Be(ulong.MaxValue);
        rows[2].Value.Should().Be(42UL);
    }

    [Fact]
    public void UInt64Projection_ShouldMaterializeValueAboveInt64Max()
    {
        var value = _sut.DataProvider
            .From<IMySqlUInt64Entity>()
            .OrderByDescending(x => x.Value)
            .Select(x => x.Value)
            .First();

        value.Should().Be(ulong.MaxValue);
    }

    [Fact]
    public void NullableUInt64_ShouldMaterializeNullAndValue()
    {
        var rows = _sut.DataProvider
            .From<IMySqlUInt64Entity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Maybe })
            .ToList();

        rows[0].Maybe.Should().Be(ulong.MaxValue);
        rows[1].Maybe.Should().BeNull();
        rows[2].Maybe.Should().Be(7UL);
    }

    [Fact]
    public void Cte_Recursive_WithDistinctUnion_ShouldProduceNumberSeries()
    {
        var ctx = _sut.DataProvider;

        var anchor = _sut.SimpleEntity.Where(s => s.Id == 1).Select(s => new CommonTestSuite.CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CommonTestSuite.CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.Union(step);

        var rows = ctx
            .WithRecursive("nums", body)
            .From("nums")
            .Select(t => new CommonTestSuite.CteNumberRow { n = t["n"].AsInt })
            .ToList();

        rows.Select(r => r.n).OrderBy(n => n).Should().Equal(1, 2, 3, 4, 5);
    }

    [SqlTable("collation_probe")]
    internal interface ICollationProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }
        [Column("name")]
        [Collation("utf8mb4_bin")]
        string? Name { get; set; }
    }

    [Fact]
    public void ColumnCollation_ShouldApplyDeclaredBinaryCollation()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists collation_probe");
        Execute(ctx, "create table collation_probe (id bigint, name varchar(50)) character set utf8mb4");

        try
        {
            Execute(ctx, "insert into collation_probe (id, name) values (1, 'abc')");
            Execute(ctx, "insert into collation_probe (id, name) values (2, 'ABC')");

            ctx.From<ICollationProbe>().Where(x => x.Name == "abc").Select(x => x.Id).ToList()
                .Should().Equal(1L);
        }
        finally
        {
            Execute(ctx, "drop table if exists collation_probe");
        }
    }

    [Fact]
    public void MySqlFunctions_FindInSet_ShouldReturnPositionAndFilter()
    {
        _sut.SimpleEntity
            .Select(_ => SqlFunctions.MySql.find_in_set("b", "a,b,c"))
            .First()
            .Should().Be(2);

        var ids = _sut.ComplexEntity
            .Where(x => SqlFunctions.MySql.find_in_set(x.String, "xxx,dadfasd") > 0)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1L, 2L);
    }

    [Fact]
    public void MySqlFunctions_FieldEltSubstringIndex_ShouldEvaluate()
    {
        var r = _sut.SimpleEntity
            .Select(_ => new
            {
                F = SqlFunctions.MySql.field("b", "a", "b"),
                E = SqlFunctions.MySql.elt(2, "a", "b"),
                S = SqlFunctions.MySql.substring_index("a.b.c", ".", 2)
            })
            .First();

        r.F.Should().Be(2);
        r.E.Should().Be("b");
        r.S.Should().Be("a.b");
    }

    [Fact]
    public void MySqlFunctions_DateFunctions_ShouldUsePercentTemplates()
    {
        _sut.SimpleEntity
            .Select(_ => SqlFunctions.MySql.format(1234.5m, 2))
            .First()
            .Should().Be("1,234.50");

        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Parsed = SqlFunctions.MySql.str_to_date("2023-01-02", "%Y-%m-%d"),
                Formatted = SqlFunctions.MySql.date_format(x.Datetime, "%Y-%m-%d")
            })
            .First();

        r.Parsed.Should().Be(new DateTime(2023, 1, 2));
        r.Formatted.Should().Be("2023-01-01");

        _sut.SimpleEntity
            .Select(_ => SqlFunctions.MySql.unix_timestamp(SqlFunctions.MySql.from_unixtime(1672617600L)))
            .First()
            .Should().Be(1672617600L);
    }

    [Fact]
    public void MySqlFunctions_Hashes_ShouldReturnHex()
    {
        var r = _sut.SimpleEntity
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
    public void MySqlFunctions_InetConversion_ShouldRoundTrip()
    {
        var r = _sut.SimpleEntity
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
    public void MySqlFunctions_JsonMutation_ShouldTransform()
    {
        _sut.SimpleEntity
            .Select(_ => SqlFunctions.MySql.json_set("{\"a\": 1}", "$.b", "2"))
            .First()
            .Should().Contain("\"b\"");

        _sut.SimpleEntity
            .Select(_ => SqlFunctions.MySql.json_remove("{\"a\": 1, \"b\": 2}", "$.a"))
            .First()
            .Should().NotContain("\"a\"");

        _sut.SimpleEntity
            .Select(_ => SqlFunctions.MySql.json_depth("{\"a\": [1, 2]}"))
            .First()
            .Should().BeGreaterThanOrEqualTo(2);

        _sut.SimpleEntity
            .Select(_ => SqlFunctions.MySql.json_length("[1, 2, 3]"))
            .First()
            .Should().Be(3);

        _sut.SimpleEntity
            .Select(_ => SqlFunctions.MySql.json_type("{\"a\": 1}"))
            .First()
            .Should().Be("OBJECT");

        _sut.SimpleEntity
            .Select(_ => SqlFunctions.MySql.json_keys("{\"a\": 1}"))
            .First()
            .Should().Contain("a");
    }

    [Fact]
    public void MySqlFunctions_UuidRoundTrip_ShouldPreserveUuid()
    {
        _sut.SimpleEntity
            .Select(_ => SqlFunctions.MySql.bin_to_uuid(
                SqlFunctions.MySql.uuid_to_bin("6ccd780c-baba-1026-9564-5b8c656024db")))
            .First()
            .Should().Be("6ccd780c-baba-1026-9564-5b8c656024db");
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("uint64_entity")]
public interface IMySqlUInt64Entity
{
    [Key]
    [Column("id")]
    ulong Id { get; set; }
    [Column("value")]
    ulong Value { get; set; }
    [Column("maybe")]
    ulong? Maybe { get; set; }
}
