using FluentAssertions;
using NextORM.Core;
using Npgsql;
using System.Text.Json;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tests that assert PostgreSQL specific numeric behaviour, backed by a Testcontainers instance
/// unless NEXTORM_POSTGRES_CONNECTION points at an existing server.
/// </summary>
public sealed class PostgresSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void InfoFunctions_ShouldReturnServerValues()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                U = SqlFunctions.Sql.gen_random_uuid(),
                Db = SqlFunctions.Sql.current_database(),
                Ver = SqlFunctions.Sql.version(),
                User = SqlFunctions.Sql.current_user(),
                Session = SqlFunctions.Sql.session_user(),
                Schema = SqlFunctions.Sql.current_schema(),
                T = SqlFunctions.Postgres.pg_typeof(x.Id)
            })
            .First();

        r.U.Should().NotBe(Guid.Empty);
        r.Db.Should().NotBeNullOrEmpty();
        r.Ver.Should().Contain("PostgreSQL");
        r.User.Should().NotBeNullOrEmpty();
        r.Session.Should().NotBeNullOrEmpty();
        r.Schema.Should().NotBeNullOrEmpty();
        r.T.Should().Be("integer");
    }

    [Fact]
    public void QueryHint_ShouldExecuteAsPlainComment()
    {
        var ids = _sut.SimpleEntity
            .Select(it => it.Id)
            .Hint("SeqScan(simple_entity)")
            .ToList();

        ids.Should().NotBeEmpty();
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
    public void OrderByDescending_NullsFirst_ShouldSortData()
    {
        // PostgreSQL treats NULL as the largest value, so DESC puts the NULL group first.
        // The shared suite avoids depending on this; here it is asserted explicitly.
        var r = _sut.ComplexEntity.OrderByDescending(it => it.Int).Select(it => new { it.Id }).ToList();

        r[0].Id.Should().Be(1);
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
    public void SetSeed_ShouldReturnNullBecausePostgresReturnsVoid()
    {
        var value = _sut.SimpleEntity
            .Select(x => SqlFunctions.Postgres.setseed(0.5))
            .FirstOrDefault();

        value.Should().BeNull();
    }

    [Fact]
    public async Task ArrayShuffleSample_ShouldExecute()
    {
        var cmd = _sut.SimpleEntity
            .Select(x => x.Id)
            .PrepareFromSql(
                "select id from simple_entity where array_length(array_shuffle(array[1,2,3,4]), 1) = 4 "
                + "and array_length(array_sample(array[1,2,3,4], 2), 1) = 2",
                TestContext.Current.CancellationToken);

        var ids = await _sut.DataProvider.ToListAsync(cmd);

        ids.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CryptoHash_ShouldReturnSha256()
    {
        await using (var connection = new NpgsqlConnection(PostgresContainer.ConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand("create extension if not exists pgcrypto", connection);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var expected = Convert.FromHexString("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");

        var hashes = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                A = SqlFunctions.Postgres.digest("abc", "sha256"),
                B = SqlFunctions.Postgres.sha256(SqlFunctions.Parameter<byte[]>(0))
            })
            .First(new byte[] { 0x61, 0x62, 0x63 });

        hashes.A.Should().Equal(expected);
        hashes.B.Should().Equal(expected);
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
    public void StringJoinAndSplit_ShouldRoundTrip()
    {
        // "dadfasd" split on 'a' is {"d", "df", "sd"}, joined back with '-' gives "d-df-sd".
        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => string.Join("-", e.String!.Split('a')))
            .First()
            .Should().Be("d-df-sd");
    }

    [Fact]
    public void RegexpMatches_ShouldReturnCapturedGroups()
    {
        var source = "a1b2";
        var pattern = "\\d";
        var flags = "g";

        var groups = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Postgres.regexp_matches(source, pattern, flags))
            .Select(r => new { r.Matches })
            .ToList();

        groups.Should().HaveCount(2);
        groups.Select(g => g.Matches.Single()).Should().Equal("1", "2");
    }

    [Fact]
    public void RegexpSplitToTable_ShouldReturnFragments()
    {
        var source = "a,b,c";
        var pattern = ",";

        var fragments = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Postgres.regexp_split_to_table(source, pattern))
            .Select(r => r.Value)
            .ToList();

        fragments.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void JsonbArrayElementsText_ShouldReturnElements()
    {
        using var json = JsonDocument.Parse("[1,2,3]");

        var values = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_array_elements_text(json))
            .Select(r => r.Value)
            .ToList();

        values.Should().Equal("1", "2", "3");
    }

    [Fact]
    public void JsonbEachText_ShouldReturnEntries()
    {
        using var json = JsonDocument.Parse("""{"a":1,"b":2}""");

        var rows = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_each_text(json))
            .OrderBy(r => r.Key)
            .Select(r => new { r.Key, r.Value })
            .ToList();

        rows.Select(r => (r.Key, r.Value)).Should().Equal(("a", "1"), ("b", "2"));
    }

    [Fact]
    public void JsonbObjectKeys_ShouldReturnKeys()
    {
        using var json = JsonDocument.Parse("""{"a":1,"b":2}""");

        var keys = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_object_keys(json))
            .OrderBy(r => r.Key)
            .Select(r => r.Key)
            .ToList();

        keys.Should().Equal("a", "b");
    }

    [Fact]
    public void JsonbPathQuery_ShouldReturnMatches()
    {
        using var json = JsonDocument.Parse("""{"a":[1,2]}""");
        var path = "$.a[*]";

        var values = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_path_query(json, SqlFunctions.Postgres.jsonpath(path)))
            .Select(r => r.Value)
            .ToList();

        values.Should().Equal("1", "2");
    }

    [Fact]
    public void TsStat_ShouldReturnLexemeStatistics()
    {
        var query = "select to_tsvector('cat dog cat')";

        var stats = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Postgres.ts_stat(query))
            .OrderBy(r => r.Word)
            .Select(r => new { r.Word, r.Ndoc, r.Nentry })
            .ToList();

        stats.Select(s => (s.Word, s.Ndoc, s.Nentry)).Should().Equal(("cat", 1, 2), ("dog", 1, 1));
    }

    [Fact]
    public void NamedWindow_ShouldDeclareOnceAndReference()
    {
        var e = _sut.ComplexEntity;

        var rows = e
            .Window("w", partitionBy: [x => x.Int], orderBy: [e.Asc(x => x.Id)])
            .Select(x => new
            {
                x.Id,
                rn = SqlFunctions.Sql.row_number().Over("w"),
                total = SqlFunctions.Sql.sum_over(x.Id).Over("w")
            })
            .ToList();

        var byId = rows.ToDictionary(x => x.Id);

        // id 1 is alone in the null partition; ids 2 and 3 share the nullableint = 1 partition. The
        // window has an ORDER BY, so sum() is a running sum (the ORDER BY default frame).
        byId[1].rn.Should().Be(1);
        byId[2].rn.Should().Be(1);
        byId[3].rn.Should().Be(2);

        byId[1].total.Should().Be(1L);
        byId[2].total.Should().Be(2L);
        byId[3].total.Should().Be(5L);
    }

    [Fact]
    public void GroupsFrame_ShouldIncludeWholePeerGroups()
    {
        var rows = _sut.ComplexEntity
            .Select(x => new
            {
                x.Id,
                total = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Groups(1, 1))
            })
            .ToList();

        var byId = rows.ToDictionary(x => x.Id);

        // The seeded ids are distinct, so each peer group is a single row and a one-group frame on each
        // side covers the adjacent rows.
        byId[1].total.Should().Be(3L);
        byId[2].total.Should().Be(6L);
        byId[3].total.Should().Be(5L);
    }

    [Fact]
    public void FrameExclusion_ShouldRemoveCurrentRow()
    {
        var rows = _sut.ComplexEntity
            .Select(x => new
            {
                x.Id,
                n = SqlFunctions.Sql.count_over().Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.RowsUnboundedPrecedingToCurrentRow.WithExclusion(WindowFrameExclusion.CurrentRow))
            })
            .ToList();

        var byId = rows.ToDictionary(x => x.Id);

        byId[1].n.Should().Be(0);
        byId[2].n.Should().Be(1);
        byId[3].n.Should().Be(2);
    }
}
