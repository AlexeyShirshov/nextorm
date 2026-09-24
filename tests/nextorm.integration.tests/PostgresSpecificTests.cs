using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
    public void DataModifyingCte_ShouldInsertAndReturnRows()
    {
        var ctx = _sut.DataProvider;
        var marker = "dmcte_" + Guid.NewGuid().ToString("N");

        var returned = ctx.With("ins", ctx.InsertInto<IInsertEntity>()
                .Value(x => x.Name, marker)
                .Value(x => x.Age, 11)
                .Returning(x => new { x.Id, x.Name }))
            .From("ins")
            .Select(r => new { r.Id, r.Name })
            .ToList();

        returned.Should().ContainSingle();
        returned[0].Id.Should().BeGreaterThan(0);
        returned[0].Name.Should().Be(marker);

        ctx.From<IInsertEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Age })
            .ToList()
            .Should().ContainSingle().Which.Age.Should().Be(11);
    }

    [Fact]
    public void DataModifyingCte_InsertSelectBody_ShouldPersistRows()
    {
        var ctx = _sut.DataProvider;
        var marker = "dmcte_src_" + Guid.NewGuid().ToString("N");

        ctx.InsertInto<IInsertEntity>().Value(x => x.Name, marker).Value(x => x.Age, 31).Insert();

        var returned = ctx.With("ins", ctx.InsertInto<IInsertEntity>()
                .Values(ctx.From<IInsertEntity>().Where(x => x.Name == marker), s => new { s.Name, s.Age })
                .Returning(x => new { x.Name, x.Age }))
            .From("ins")
            .Select(r => new { r.Name, r.Age })
            .ToList();

        returned.Should().ContainSingle();
        returned[0].Name.Should().Be(marker);
        returned[0].Age.Should().Be(31);

        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList()
            .Should().HaveCount(2);
    }

    [Fact]
    public void DataModifyingCte_MutationBodyReferencingReadCte_ShouldPersist()
    {
        var ctx = _sut.DataProvider;
        var marker = "dmcte_ref_" + Guid.NewGuid().ToString("N");

        ctx.InsertInto<IInsertEntity>().Value(x => x.Name, marker).Value(x => x.Age, 41).Insert();

        var scope = ctx.With("src", ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Name, x.Age }));
        var insert = ctx.InsertInto<IInsertEntity>()
            .Values(scope.From("src"), a => new { Name = a.GetString("Name"), Age = a.GetInt32("Age") })
            .Returning(x => new { x.Name, x.Age });

        var returned = scope.With("ins", insert).From("ins").Select(r => new { r.Name, r.Age }).ToList();

        returned.Should().ContainSingle();
        returned[0].Age.Should().Be(41);

        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList()
            .Should().HaveCount(2);
    }

    [Fact]
    public void InsertFromMutationCte_ShouldPersistRows()
    {
        var ctx = _sut.DataProvider;
        var marker = "dmcte_main_" + Guid.NewGuid().ToString("N");

        var source = ctx.With("ins", ctx.InsertInto<IInsertEntity>()
                .Value(x => x.Name, marker)
                .Value(x => x.Age, 51)
                .Returning(x => new { x.Name, x.Age }))
            .From("ins");

        ctx.InsertInto<IInsertEntity>()
            .Values(source, r => new { r.Name, r.Age })
            .Insert()
            .Should().Be(1);

        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList()
            .Should().HaveCount(2);
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

    private static int MergeTestKey() => Random.Shared.Next(1_000_000, int.MaxValue);

    [Fact]
    public void FullMerge_Returning_ShouldReturnMergedRows()
    {
        var ctx = _sut.DataProvider;
        var id = MergeTestKey();
        var marker = "pm_" + Guid.NewGuid().ToString("N");

        var rows = ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = id, Name = marker, Age = 3 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Returning(x => new { x.Id, x.Name })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(id);
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public void FullMerge_MatchedDelete_ShouldDeleteMatchedRow()
    {
        var ctx = _sut.DataProvider;
        var deleteId = MergeTestKey();

        ctx.InsertInto<IMergeEntity>()
            .Values(new MergeEntity { Id = deleteId, Name = "old", Age = 1 })
            .Insert();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = deleteId, Name = "ignored", Age = 0 })
            .OnKeys()
            .WhenMatched().ThenDelete()
            .WhenNotMatched().ThenInsert()
            .Merge();

        ctx.From<IMergeEntity>().Where(x => x.Id == deleteId).Select(x => x.Id).ToList().Should().BeEmpty();
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

    [Fact]
    public void JsonBuildObject_ShouldProduceObject()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_build_object("id", x.Id, "s", x.String))
            .First();

        r.Should().Contain("\"id\"").And.Contain("dadfasd");
    }

    [Fact]
    public void JsonbBuildObject_ShouldProduceObject()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.jsonb_build_object("id", x.Id))
            .First();

        r.Should().Contain("\"id\"");
    }

    [Fact]
    public void JsonBuildArray_ShouldProduceArray()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_build_array(x.Id, x.String))
            .First();

        r.Should().StartWith("[").And.Contain("dadfasd");
    }

    [Fact]
    public void JsonbBuildArray_ShouldProduceArray()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.jsonb_build_array(x.Id))
            .First();

        r.Should().StartWith("[");
    }

    [Fact]
    public void ToJsonAndToJsonb_ShouldConvertScalar()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                J = SqlFunctions.Postgres.to_json(x.Id),
                B = SqlFunctions.Postgres.to_jsonb(x.String)
            })
            .First();

        r.J.Should().Be("1");
        r.B.Should().Be("\"dadfasd\"");
    }

    [Fact]
    public void JsonCast_ShouldParseText()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.jsonb_typeof(SqlFunctions.Postgres.json_cast("{\"a\":1}")))
            .First();

        r.Should().Be("object");
    }

    [Fact]
    public void JsonAgg_ShouldAggregateRows()
    {
        var r = _sut.ComplexEntity
            .Select(x => SqlFunctions.Postgres.json_agg(x.Id))
            .First();

        r.Should().StartWith("[").And.Contain("1");
    }

    [Fact]
    public void JsonbAgg_ShouldAggregateRows()
    {
        var r = _sut.ComplexEntity
            .Select(x => SqlFunctions.Postgres.jsonb_agg(x.Id))
            .First();

        r.Should().StartWith("[");
    }

    [Fact]
    public void JsonObjectAgg_ShouldAggregatePairs()
    {
        var r = _sut.ComplexEntity
            .Select(x => SqlFunctions.Postgres.json_object_agg(x.Id, x.String))
            .First();

        r.Should().Contain("\"1\"");
    }

    [Fact]
    public void JsonbObjectAgg_ShouldAggregatePairs()
    {
        var r = _sut.ComplexEntity
            .Select(x => SqlFunctions.Postgres.jsonb_object_agg(x.Id, x.String))
            .First();

        r.Should().Contain("dadfasd");
    }

    [Fact]
    public void JsonGet_ShouldIndexObject()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Raw = SqlFunctions.Postgres.json_get(SqlFunctions.Postgres.jsonb_build_object("a", x.Id), "a"),
                Text = SqlFunctions.Postgres.json_get_text(SqlFunctions.Postgres.jsonb_build_object("a", x.Id), "a")
            })
            .First();

        r.Raw.Should().Be("1");
        r.Text.Should().Be("1");
    }

    [Fact]
    public void JsonGetPath_ShouldIndexByPath()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_get_path_text(
                SqlFunctions.Postgres.jsonb_build_object("a", x.Id), new[] { "a" }))
            .First();

        r.Should().Be("1");
    }

    [Fact]
    public void JsonContains_ShouldDetectContainment()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Postgres.json_contains(
                SqlFunctions.Postgres.jsonb_build_object("a", 1, "b", 2),
                SqlFunctions.Postgres.jsonb_build_object("a", 1)))
            .First();

        r.Should().BeTrue();
    }

    [Fact]
    public void JsonExists_ShouldDetectKey()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Has = SqlFunctions.Postgres.json_exists(SqlFunctions.Postgres.jsonb_build_object("a", x.Id), "a"),
                Any = SqlFunctions.Postgres.json_exists_any(SqlFunctions.Postgres.jsonb_build_object("a", x.Id), new[] { "a", "b" }),
                All = SqlFunctions.Postgres.json_exists_all(SqlFunctions.Postgres.jsonb_build_object("a", x.Id), new[] { "a" })
            })
            .First();

        r.Has.Should().BeTrue();
        r.Any.Should().BeTrue();
        r.All.Should().BeTrue();
    }

    [Fact]
    public void JsonArrayLength_ShouldReturnCount()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                J = SqlFunctions.Postgres.json_array_length(SqlFunctions.Postgres.json_build_array(1, 2, 3)),
                B = SqlFunctions.Postgres.jsonb_array_length(SqlFunctions.Postgres.jsonb_build_array(1, 2))
            })
            .First();

        r.J.Should().Be(3);
        r.B.Should().Be(2);
    }

    [Fact]
    public void JsonTypeof_ShouldReturnKind()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Num = SqlFunctions.Postgres.json_typeof(SqlFunctions.Postgres.to_json(x.Id)),
                BNum = SqlFunctions.Postgres.jsonb_typeof(SqlFunctions.Postgres.to_jsonb(x.Id)),
                Str = SqlFunctions.Postgres.jsonb_typeof(SqlFunctions.Postgres.to_jsonb(x.String))
            })
            .First();

        r.Num.Should().Be("number");
        r.BNum.Should().Be("number");
        r.Str.Should().Be("string");
    }

    [Fact]
    public void JsonbSetAndInsert_ShouldModifyDocument()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Set = SqlFunctions.Postgres.jsonb_set(
                    SqlFunctions.Postgres.jsonb_build_object("a", 1), new[] { "a" }, "2"),
                Insert = SqlFunctions.Postgres.jsonb_insert(
                    SqlFunctions.Postgres.jsonb_build_object("a", 1), new[] { "b" }, "3")
            })
            .First();

        r.Set.Should().Contain("2");
        r.Insert.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void JsonbStripNullsAndPretty_ShouldTransform()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Strip = SqlFunctions.Postgres.jsonb_strip_nulls(
                    SqlFunctions.Postgres.jsonb_build_object("a", 1, "b", x.Int)),
                Pretty = SqlFunctions.Postgres.jsonb_pretty(SqlFunctions.Postgres.jsonb_build_object("a", 1))
            })
            .First();

        r.Strip.Should().NotContain("\"b\"");
        r.Pretty.Should().Contain("\"a\"");
    }

    [Fact]
    public void JsonbDeleteAndConcat_ShouldCombine()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Del = SqlFunctions.Postgres.jsonb_delete(SqlFunctions.Postgres.jsonb_build_object("a", 1, "b", 2), "b"),
                Cat = SqlFunctions.Postgres.json_concat(
                    SqlFunctions.Postgres.jsonb_build_object("a", 1),
                    SqlFunctions.Postgres.jsonb_build_object("b", 2))
            })
            .First();

        r.Del.Should().NotContain("\"b\"");
        r.Cat.Should().Contain("\"a\"").And.Contain("\"b\"");
    }

    [Fact]
    public void JsonPathFunctions_ShouldEvaluate()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Exists = SqlFunctions.Postgres.jsonb_path_exists(SqlFunctions.Postgres.jsonb_build_object("a", x.Id), "$.a"),
                Match = SqlFunctions.Postgres.jsonb_path_match(SqlFunctions.Postgres.jsonb_build_object("a", 1), "$.a == 1"),
                First = SqlFunctions.Postgres.jsonb_path_query_first(SqlFunctions.Postgres.jsonb_build_object("a", 7), "$.a"),
                Arr = SqlFunctions.Postgres.jsonb_path_query_array(SqlFunctions.Postgres.jsonb_build_object("a", 7), "$.a"),
                ViaJsonpath = SqlFunctions.Postgres.jsonb_path_exists(
                    SqlFunctions.Postgres.jsonb_build_object("a", x.Id), SqlFunctions.Postgres.jsonpath("$.a"))
            })
            .First();

        r.Exists.Should().BeTrue();
        r.Match.Should().BeTrue();
        r.First.Should().Contain("7");
        r.Arr.Should().Contain("7");
        r.ViaJsonpath.Should().BeTrue();
    }

    [Fact]
    public void ArrayFunctions_ShouldComputeOverLiterals()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Card = SqlFunctions.Postgres.cardinality(new[] { 1, 2, 3 }),
                Len = SqlFunctions.Postgres.array_length(new[] { 1, 2, 3 }, 1),
                Ndim = SqlFunctions.Postgres.array_ndims(new[] { 1, 2, 3 }),
                Low = SqlFunctions.Postgres.array_lower(new[] { 1, 2, 3 }, 1),
                Up = SqlFunctions.Postgres.array_upper(new[] { 1, 2, 3 }, 1),
                Pos = SqlFunctions.Postgres.array_position(new[] { 5, 6, 7 }, 6)
            })
            .First();

        r.Card.Should().Be(3);
        r.Len.Should().Be(3);
        r.Ndim.Should().Be(1);
        r.Low.Should().Be(1);
        r.Up.Should().Be(3);
        r.Pos.Should().Be(2);
    }

    [Fact]
    public void ArrayPredicates_ShouldCompareArrays()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Contains = SqlFunctions.Postgres.array_contains(new[] { 1, 2, 3 }, new[] { 1, 2 }),
                Overlaps = SqlFunctions.Postgres.array_overlaps(new[] { 1, 2 }, new[] { 2, 3 }),
                ContainedBy = SqlFunctions.Postgres.array_contained_by(new[] { 1, 2 }, new[] { 1, 2, 3 })
            })
            .First();

        r.Contains.Should().BeTrue();
        r.Overlaps.Should().BeTrue();
        r.ContainedBy.Should().BeTrue();
    }

    [Fact]
    public void ArrayManipulation_ShouldTransformArrays()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                App = SqlFunctions.Postgres.array_append(new[] { 1, 2 }, 3),
                Pre = SqlFunctions.Postgres.array_prepend(0, new[] { 1, 2 }),
                Cat = SqlFunctions.Postgres.array_cat(new[] { 1 }, new[] { 2 }),
                Rem = SqlFunctions.Postgres.array_remove(new[] { 1, 2, 1 }, 1),
                Rep = SqlFunctions.Postgres.array_replace(new[] { 1, 2, 1 }, 1, 9)
            })
            .First();

        r.App.Should().Equal(1, 2, 3);
        r.Pre.Should().Equal(0, 1, 2);
        r.Cat.Should().Equal(1, 2);
        r.Rem.Should().Equal(2);
        r.Rep.Should().Equal(9, 2, 9);
    }

    [Fact]
    public void ArrayStringAndPositions_ShouldConvert()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Join = SqlFunctions.Postgres.array_to_string(new[] { "a", "b", "c" }, ","),
                Split = SqlFunctions.Postgres.string_to_array("a,b,c", ","),
                Positions = SqlFunctions.Postgres.array_positions(new[] { 1, 2, 1 }, 1),
                Dims = SqlFunctions.Postgres.array_dims(new[] { 1, 2, 3 })
            })
            .First();

        r.Join.Should().Be("a,b,c");
        r.Split.Should().Equal("a", "b", "c");
        r.Positions.Should().Equal(1, 3);
        r.Dims.Should().Be("[1:3]");
    }

    [Fact]
    public void ArrayFillAndQuantifiers_ShouldWork()
    {
        var ones = _sut.SimpleEntity
            .Where(x => SqlFunctions.Postgres.any(x.Id, new[] { 1, 2, 3 }))
            .Select(x => x.Id)
            .ToList();

        ones.Should().Equal(1, 2, 3);

        var literals = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Fill = SqlFunctions.Postgres.array_fill(7, 3),
                AnyValue = 1 == SqlFunctions.Postgres.any(new[] { 1, 2 }),
                AllValue = 2 == SqlFunctions.Postgres.all(new[] { 2, 2 })
            })
            .First();

        literals.Fill.Should().Equal(7, 7, 7);
        literals.AnyValue.Should().BeTrue();
        literals.AllValue.Should().BeTrue();
    }

    [SqlTable("collation_probe")]
    internal interface ICollationProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }
        [Column("name")]
        [Collation("C")]
        string? Name { get; set; }
    }

    [Fact]
    public void ColumnCollation_ShouldApplyDeclaredBinaryCollation()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists collation_probe");
        Execute(ctx, "create table collation_probe (id bigint, name varchar(50))");

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

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
