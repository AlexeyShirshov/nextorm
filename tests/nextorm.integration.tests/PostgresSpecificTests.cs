using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Text;
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
    public void JsonbToRecord_WithDeclaredSchema_ShouldReturnRow()
    {
        using var json = JsonDocument.Parse("""{"a":1,"b":"x"}""");

        var row = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_to_record<IDynamicRecordRow>(json))
            .Select(r => new { r.A, r.B })
            .First();

        (row.A, row.B).Should().Be((1, "x"));
    }

    [Fact]
    public void JsonbToRecordset_WithDeclaredSchema_ShouldReturnRows()
    {
        using var json = JsonDocument.Parse("""[{"a":1,"b":"x"},{"a":2,"b":"y"}]""");

        var rows = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Postgres.jsonb_to_recordset<IDynamicRecordRow>(json))
            .OrderBy(r => r.A)
            .Select(r => new { r.A, r.B })
            .ToList();

        rows.Select(r => (r.A, r.B)).Should().Equal((1, "x"), (2, "y"));
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

        var returned = ctx.With("ins", ctx.CreateInsertBuilder<IInsertEntity>()
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

        ctx.CreateInsertBuilder<IInsertEntity>().Value(x => x.Name, marker).Value(x => x.Age, 31).Insert();

        var returned = ctx.With("ins", ctx.CreateInsertBuilder<IInsertEntity>()
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

        ctx.CreateInsertBuilder<IInsertEntity>().Value(x => x.Name, marker).Value(x => x.Age, 41).Insert();

        var scope = ctx.With("src", ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Name, x.Age }));
        var insert = ctx.CreateInsertBuilder<IInsertEntity>()
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

        var source = ctx.With("ins", ctx.CreateInsertBuilder<IInsertEntity>()
                .Value(x => x.Name, marker)
                .Value(x => x.Age, 51)
                .Returning(x => new { x.Name, x.Age }))
            .From("ins");

        ctx.CreateInsertBuilder<IInsertEntity>()
            .Values(source, r => new { r.Name, r.Age })
            .Insert()
            .Should().Be(1);

        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList()
            .Should().HaveCount(2);
    }

    [SqlTable("dmcte_entity")]
    internal interface IDmCteEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("name")]
        string? Name { get; set; }
        [Column("age")]
        int Age { get; set; }
    }

    [SqlTable("dmcte_target")]
    internal interface IDmCteTarget
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("name")]
        string? Name { get; set; }
        [Column("age")]
        int Age { get; set; }
    }

    [SqlTable("dmcte_source")]
    internal interface IDmCteSource
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("name")]
        string? Name { get; set; }
        [Column("age")]
        int Age { get; set; }
    }

    // Concrete entity classes (not the interfaces above): the standalone join terminals must materialise
    // real rows through the positional projection, which the interface metadata could not shape.
    [SqlTable("dmcte_target")]
    internal sealed class DmCteTargetRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("name")]
        public string? Name { get; set; }
        [Column("age")]
        public int Age { get; set; }
    }

    [SqlTable("dmcte_source")]
    internal sealed class DmCteSourceRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("name")]
        public string? Name { get; set; }
        [Column("age")]
        public int Age { get; set; }
    }

    // A plain projection target (not an entity): covers the DTO positional-constructor and the
    // member-init RETURNING shapes for the join-returning terminals.
    internal sealed class JoinReturningDto
    {
        public JoinReturningDto()
        {
        }

        public JoinReturningDto(int targetId, string? sourceName)
        {
            TargetId = targetId;
            SourceName = sourceName;
        }

        public int TargetId { get; set; }

        public string? SourceName { get; set; }
    }

    private static void CreateDmCteTable(IDataContext ctx, string table)
    {
        Execute(ctx, $"drop table if exists {table}");
        Execute(ctx, $"create table {table} (id integer primary key, name varchar(100), age integer)");
    }

    private static void DropDmCteTable(IDataContext ctx, string table)
        => Execute(ctx, $"drop table if exists {table}");

    [Fact]
    public void DataModifyingCte_UpdateReturning_ShouldReturnAndPersistUpdatedRows()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_entity");

        try
        {
            ctx.CreateInsertBuilder<IDmCteEntity>()
                .Value(x => x.Id, 1)
                .Value(x => x.Name, "before")
                .Value(x => x.Age, 5)
                .Insert();

            // The updated value is NULL, so the RETURNING read also exercises a nullable result bound.
            var returned = ctx.With("upd", ctx.CreateUpdateBuilder<IDmCteEntity>()
                    .Set(x => x.Name, (string?)null)
                    .Set(x => x.Age, 9)
                    .Where(x => x.Id == 1)
                    .Returning(x => new { x.Id, x.Name, x.Age }))
                .From("upd")
                .Select(r => new { r.Id, r.Name, r.Age })
                .ToList();

            returned.Should().ContainSingle();
            returned[0].Id.Should().Be(1);
            returned[0].Name.Should().BeNull();
            returned[0].Age.Should().Be(9);

            var persisted = ctx.From<IDmCteEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new { x.Name, x.Age })
                .Single();

            persisted.Name.Should().BeNull();
            persisted.Age.Should().Be(9);
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_entity");
        }
    }

    [Fact]
    public void DataModifyingCte_UpdateReturning_NoMatch_ShouldReturnEmptyAndLeaveRowUnchanged()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_entity");

        try
        {
            ctx.CreateInsertBuilder<IDmCteEntity>()
                .Value(x => x.Id, 1)
                .Value(x => x.Name, "keep")
                .Value(x => x.Age, 5)
                .Insert();

            var returned = ctx.With("upd", ctx.CreateUpdateBuilder<IDmCteEntity>()
                    .Set(x => x.Name, "changed")
                    .Set(x => x.Age, 9)
                    .Where(x => x.Id == 999)
                    .Returning(x => new { x.Id, x.Name }))
                .From("upd")
                .Select(r => new { r.Id, r.Name })
                .ToList();

            returned.Should().BeEmpty();

            var persisted = ctx.From<IDmCteEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new { x.Name, x.Age })
                .Single();

            persisted.Name.Should().Be("keep");
            persisted.Age.Should().Be(5);
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_entity");
        }
    }

    [Fact]
    public void DataModifyingCte_DeleteReturning_ShouldReturnDeletedRowsAndRemoveThem()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_entity");

        try
        {
            ctx.CreateInsertBuilder<IDmCteEntity>()
                .Value(x => x.Id, 1)
                .Value(x => x.Name, "doomed")
                .Value(x => x.Age, 3)
                .Insert();

            var returned = ctx.With("del", ctx.CreateDeleteBuilder<IDmCteEntity>()
                    .Where(x => x.Id == 1)
                    .Returning(x => new { x.Id, x.Name, x.Age }))
                .From("del")
                .Select(r => new { r.Id, r.Name, r.Age })
                .ToList();

            returned.Should().ContainSingle();
            returned[0].Id.Should().Be(1);
            returned[0].Name.Should().Be("doomed");
            returned[0].Age.Should().Be(3);

            ctx.From<IDmCteEntity>().Where(x => x.Id == 1).Select(x => x.Id).ToList()
                .Should().BeEmpty();
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_entity");
        }
    }

    [Fact]
    public void DataModifyingCte_DeleteReturning_NoMatch_ShouldReturnEmptyAndKeepRow()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_entity");

        try
        {
            ctx.CreateInsertBuilder<IDmCteEntity>()
                .Value(x => x.Id, 1)
                .Value(x => x.Name, "keep")
                .Value(x => x.Age, 3)
                .Insert();

            var returned = ctx.With("del", ctx.CreateDeleteBuilder<IDmCteEntity>()
                    .Where(x => x.Id == 999)
                    .Returning(x => new { x.Id, x.Name }))
                .From("del")
                .Select(r => new { r.Id, r.Name })
                .ToList();

            returned.Should().BeEmpty();

            ctx.From<IDmCteEntity>().Where(x => x.Id == 1).Select(x => x.Name).ToList()
                .Should().Equal("keep");
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_entity");
        }
    }

    [Fact]
    public void DataModifyingCte_UpdateJoinReturning_ShouldReturnJoinedSourceAndMutateTarget()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            ctx.CreateInsertBuilder<IDmCteTarget>()
                .Value(x => x.Id, 1)
                .Value(x => x.Name, "target-before")
                .Value(x => x.Age, 42)
                .Insert();
            ctx.CreateInsertBuilder<IDmCteSource>()
                .Value(x => x.Id, 2)
                .Value(x => x.Name, "source-name")
                .Value(x => x.Age, 42)
                .Insert();

            var update = ctx.From<IDmCteTarget>()
                .Join(ctx.From<IDmCteSource>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name });

            var returned = ctx.With("upd", update)
                .From("upd")
                .Select(r => new { r.TargetId, r.SourceName })
                .ToList();

            returned.Should().ContainSingle();
            returned[0].TargetId.Should().Be(1);
            returned[0].SourceName.Should().Be("source-name");

            ctx.From<IDmCteTarget>().Where(x => x.Id == 1).Select(x => x.Name).Single()
                .Should().Be("source-name");
            ctx.From<IDmCteSource>().Where(x => x.Id == 2).Select(x => x.Name).Single()
                .Should().Be("source-name");
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void DataModifyingCte_DeleteJoinReturning_ShouldReturnJoinedSourceAndDeleteTarget()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            ctx.CreateInsertBuilder<IDmCteTarget>()
                .Value(x => x.Id, 1)
                .Value(x => x.Name, "doomed")
                .Value(x => x.Age, 42)
                .Insert();
            ctx.CreateInsertBuilder<IDmCteSource>()
                .Value(x => x.Id, 2)
                .Value(x => x.Name, "keeper")
                .Value(x => x.Age, 42)
                .Insert();

            var delete = ctx.From<IDmCteTarget>()
                .Join(ctx.From<IDmCteSource>(), (a, b) => a.Age == b.Age)
                .CreateDeleteJoinBuilder()
                .Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name });

            var returned = ctx.With("del", delete)
                .From("del")
                .Select(r => new { r.TargetId, r.SourceName })
                .ToList();

            returned.Should().ContainSingle();
            returned[0].TargetId.Should().Be(1);
            returned[0].SourceName.Should().Be("keeper");

            ctx.From<IDmCteTarget>().Where(x => x.Id == 1).Select(x => x.Id).ToList()
                .Should().BeEmpty();
            ctx.From<IDmCteSource>().Where(x => x.Id == 2).Select(x => x.Id).ToList()
                .Should().Equal(2);
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void JoinUpdate_ReturningProjection_ShouldReturnUpdatedRows()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            ctx.CreateInsertBuilder<DmCteTargetRow>().Value(x => x.Id, 1).Value(x => x.Name, "target-before").Value(x => x.Age, 42).Insert();
            ctx.CreateInsertBuilder<DmCteSourceRow>().Value(x => x.Id, 2).Value(x => x.Name, "source-name").Value(x => x.Age, 42).Insert();

            var returned = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name })
                .ToList();

            returned.Should().ContainSingle();
            returned[0].TargetId.Should().Be(1);
            returned[0].SourceName.Should().Be("source-name");

            ctx.From<DmCteTargetRow>().Where(x => x.Id == 1).Select(x => x.Name).Single()
                .Should().Be("source-name");
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void JoinDelete_ReturningProjection_ShouldReturnDeletedRows()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            ctx.CreateInsertBuilder<DmCteTargetRow>().Value(x => x.Id, 1).Value(x => x.Name, "doomed").Value(x => x.Age, 42).Insert();
            ctx.CreateInsertBuilder<DmCteSourceRow>().Value(x => x.Id, 2).Value(x => x.Name, "keeper").Value(x => x.Age, 42).Insert();

            var returned = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateDeleteJoinBuilder()
                .Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name })
                .ToList();

            returned.Should().ContainSingle();
            returned[0].TargetId.Should().Be(1);
            returned[0].SourceName.Should().Be("keeper");

            ctx.From<DmCteTargetRow>().Where(x => x.Id == 1).Select(x => x.Id).ToList()
                .Should().BeEmpty();
            ctx.From<DmCteSourceRow>().Where(x => x.Id == 2).Select(x => x.Id).ToList()
                .Should().Equal(2);
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    private static void SeedJoinPair(IDataContext ctx, int targetId, string targetName, int age, int sourceId, string sourceName)
    {
        ctx.CreateInsertBuilder<DmCteTargetRow>()
            .Value(x => x.Id, targetId)
            .Value(x => x.Name, targetName)
            .Value(x => x.Age, age)
            .Insert();
        ctx.CreateInsertBuilder<DmCteSourceRow>()
            .Value(x => x.Id, sourceId)
            .Value(x => x.Name, sourceName)
            .Value(x => x.Age, age)
            .Insert();
    }

    [Fact]
    public void JoinUpdate_ReturningProjection_SingleAndToList_ShouldReturnUpdatedRows()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "target-before", 42, 2, "source-name");

            var single = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name })
                .Single();

            single.TargetId.Should().Be(1);
            single.SourceName.Should().Be("source-name");

            var all = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name })
                .ToList();

            all.Should().ContainSingle();
            all[0].TargetId.Should().Be(1);
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public async Task JoinUpdate_ReturningProjection_SingleAsyncAndToListAsync_ShouldReturnUpdatedRows()
    {
        var ctx = _sut.DataProvider;
        var ct = TestContext.Current.CancellationToken;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "target-before", 42, 2, "source-name");

            var single = await ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name })
                .SingleAsync(ct);

            single.TargetId.Should().Be(1);
            single.SourceName.Should().Be("source-name");

            var all = await ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name })
                .ToListAsync(ct);

            all.Should().ContainSingle();
            all[0].SourceName.Should().Be("source-name");

            ctx.From<DmCteTargetRow>().Where(x => x.Id == 1).Select(x => x.Name).Single()
                .Should().Be("source-name");
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void JoinUpdate_ReturningProjection_ScalarCtorAndMemberInit_ShouldReturnShapes()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "target-before", 42, 2, "source-name");

            var scalar = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => p.Item1.Id)
                .Single();
            scalar.Should().Be(1);

            var viaCtor = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => new JoinReturningDto(p.Item1.Id, p.Item2.Name))
                .Single();
            viaCtor.TargetId.Should().Be(1);
            viaCtor.SourceName.Should().Be("source-name");

            var viaMemberInit = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => new JoinReturningDto { TargetId = p.Item1.Id, SourceName = p.Item2.Name })
                .Single();
            viaMemberInit.TargetId.Should().Be(1);
            viaMemberInit.SourceName.Should().Be("source-name");
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void JoinUpdate_ReturningProjection_SameNamedColumns_ShouldReturnBothExplicitly()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "target-before", 42, 2, "source-name");

            // Both entities expose a column named "id"; the RETURNING list must qualify each by its own
            // table alias so the two projected members stay distinct.
            var row = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => new { TargetId = p.Item1.Id, SourceId = p.Item2.Id })
                .Single();

            row.TargetId.Should().Be(1);
            row.SourceId.Should().Be(2);
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void JoinUpdate_ReturningProjection_NoMatch_ShouldReturnEmptyAndRejectSingle()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "target-before", 42, 2, "source-name");

            var empty = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Where(p => p.Item1.Id < 0)
                .Returning(p => new { p.Item1.Id })
                .ToList();
            empty.Should().BeEmpty();

            var act = () => ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Where(p => p.Item1.Id < 0)
                .Returning(p => new { p.Item1.Id })
                .Single();

            act.Should().Throw<InvalidOperationException>().WithMessage("*touched no row*");
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void JoinUpdate_ReturningProjection_MultipleMatches_SingleShouldThrow()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "target-a", 42, 9, "source-name");
            ctx.CreateInsertBuilder<DmCteTargetRow>().Value(x => x.Id, 2).Value(x => x.Name, "target-b").Value(x => x.Age, 42).Insert();

            var act = () => ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => new { p.Item1.Id })
                .Single();

            act.Should().Throw<InvalidOperationException>().WithMessage("*more than one row*");
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void JoinDelete_ReturningProjection_Single_ShouldReturnDeletedRow()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "doomed", 42, 2, "keeper");

            var returned = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateDeleteJoinBuilder()
                .Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name })
                .Single();

            returned.TargetId.Should().Be(1);
            returned.SourceName.Should().Be("keeper");
            ctx.From<DmCteTargetRow>().Where(x => x.Id == 1).Select(x => x.Id).ToList().Should().BeEmpty();
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public async Task JoinDelete_ReturningProjection_SingleAsyncAndToListAsync_ShouldReturnDeletedRows()
    {
        var ctx = _sut.DataProvider;
        var ct = TestContext.Current.CancellationToken;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "doomed", 42, 2, "keeper");

            var single = await ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateDeleteJoinBuilder()
                .Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name })
                .SingleAsync(ct);

            single.TargetId.Should().Be(1);
            single.SourceName.Should().Be("keeper");
            ctx.From<DmCteTargetRow>().Where(x => x.Id == 1).Select(x => x.Id).ToList().Should().BeEmpty();

            // Re-seed and use the list terminal so both async terminals are exercised.
            ctx.CreateInsertBuilder<DmCteTargetRow>().Value(x => x.Id, 1).Value(x => x.Name, "doomed").Value(x => x.Age, 42).Insert();

            var all = await ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateDeleteJoinBuilder()
                .Returning(p => new { TargetId = p.Item1.Id, SourceName = p.Item2.Name })
                .ToListAsync(ct);

            all.Should().ContainSingle();
            all[0].TargetId.Should().Be(1);
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void JoinDelete_ReturningProjection_ScalarAndSameNamedColumns_ShouldReturnShapes()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "doomed", 42, 2, "keeper");

            var scalar = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateDeleteJoinBuilder()
                .Returning(p => p.Item1.Id)
                .Single();
            scalar.Should().Be(1);

            ctx.CreateInsertBuilder<DmCteTargetRow>().Value(x => x.Id, 1).Value(x => x.Name, "doomed").Value(x => x.Age, 42).Insert();

            var sameNames = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .CreateDeleteJoinBuilder()
                .Returning(p => new { TargetId = p.Item1.Id, SourceId = p.Item2.Id })
                .Single();

            sameNames.TargetId.Should().Be(1);
            sameNames.SourceId.Should().Be(2);
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void JoinDelete_ReturningProjection_NoMatch_ShouldReturnEmptyAndRejectSingle()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "safe", 42, 2, "keeper");

            var empty = ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .Where(p => p.Item1.Id < 0)
                .CreateDeleteJoinBuilder()
                .Returning(p => new { p.Item1.Id })
                .ToList();
            empty.Should().BeEmpty();

            var act = () => ctx.From<DmCteTargetRow>()
                .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                .Where(p => p.Item1.Id < 0)
                .CreateDeleteJoinBuilder()
                .Returning(p => new { p.Item1.Id })
                .Single();

            act.Should().Throw<InvalidOperationException>().WithMessage("*removed no row*");
            ctx.From<DmCteTargetRow>().Where(x => x.Id == 1).Select(x => x.Id).ToList().Should().Equal(1);
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void DataModifyingCte_JoinUpdateScope_RepeatedExecution_ShouldMutateFreshly()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "t0", 42, 2, "s1");

            var scope = ctx.With("upd", ctx.From<DmCteTargetRow>()
                    .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                    .CreateUpdateJoinBuilder()
                    .Set(p => p.Item1.Name, p => p.Item2.Name)
                    .Returning(p => new { p.Item1.Id }))
                .From("upd")
                .Select(r => new { r.Id });

            scope.ToList().Should().ContainSingle();
            ctx.From<DmCteTargetRow>().Where(x => x.Id == 1).Select(x => x.Name).Single().Should().Be("s1");

            // Change the source and re-run the very same scope: the mutation must execute afresh, not
            // return a stale cached result.
            ctx.CreateUpdateBuilder<DmCteSourceRow>().Set(x => x.Name, "s2").Where(x => x.Id == 2).Update();

            scope.ToList().Should().ContainSingle();
            ctx.From<DmCteTargetRow>().Where(x => x.Id == 1).Select(x => x.Name).Single().Should().Be("s2");
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
    }

    [Fact]
    public void DataModifyingCte_JoinDeleteScope_RepeatedExecution_ShouldDeleteFreshly()
    {
        var ctx = _sut.DataProvider;
        CreateDmCteTable(ctx, "dmcte_target");
        CreateDmCteTable(ctx, "dmcte_source");

        try
        {
            SeedJoinPair(ctx, 1, "doomed", 42, 2, "keeper");

            var scope = ctx.With("del", ctx.From<DmCteTargetRow>()
                    .Join(ctx.From<DmCteSourceRow>(), (a, b) => a.Age == b.Age)
                    .CreateDeleteJoinBuilder()
                    .Returning(p => new { p.Item1.Id }))
                .From("del")
                .Select(r => new { r.Id });

            scope.ToList().Should().ContainSingle();
            ctx.From<DmCteTargetRow>().Where(x => x.Id == 1).Select(x => x.Id).ToList().Should().BeEmpty();

            ctx.CreateInsertBuilder<DmCteTargetRow>().Value(x => x.Id, 1).Value(x => x.Name, "doomed").Value(x => x.Age, 42).Insert();

            scope.ToList().Should().ContainSingle();
            ctx.From<DmCteTargetRow>().Where(x => x.Id == 1).Select(x => x.Id).ToList().Should().BeEmpty();
            ctx.From<DmCteSourceRow>().Where(x => x.Id == 2).Select(x => x.Id).ToList().Should().Equal(2);
        }
        finally
        {
            DropDmCteTable(ctx, "dmcte_target");
            DropDmCteTable(ctx, "dmcte_source");
        }
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

        var rows = ctx.CreateMergeBuilder<IMergeEntity>()
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

        ctx.CreateInsertBuilder<IMergeEntity>()
            .Values(new MergeEntity { Id = deleteId, Name = "old", Age = 1 })
            .Insert();

        ctx.CreateMergeBuilder<IMergeEntity>()
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

    [SqlTable("pg_range_entity")]
    internal interface IPgRangeEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("during")]
        Range<int> During { get; set; }
    }

    [Fact]
    public void Range_RoundTripAndOverlaps_ShouldPreserveBounds()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists pg_range_entity");
        Execute(ctx, "create table pg_range_entity (id integer primary key, during int4range)");

        try
        {
            ctx.CreateInsertBuilder<IPgRangeEntity>()
                .Value(x => x.Id, 1)
                .Value(x => x.During, new Range<int>(10, 20))
                .Insert();
            ctx.CreateInsertBuilder<IPgRangeEntity>()
                .Value(x => x.Id, 2)
                .Value(x => x.During, Range<int>.Empty)
                .Insert();
            ctx.CreateInsertBuilder<IPgRangeEntity>()
                .Value(x => x.Id, 3)
                .Value(x => x.During, new Range<int>(0, 15, lowerInclusive: true, upperInclusive: false, lowerInfinite: true, upperInfinite: false))
                .Insert();

            var rows = ctx.From<IPgRangeEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.During }).ToList();

            rows.Should().HaveCount(3);
            rows[0].During.Should().Be(new Range<int>(10, 20));
            rows[1].During.IsEmpty.Should().BeTrue();
            rows[2].During.LowerInfinite.Should().BeTrue();
            rows[2].During.Upper.Should().Be(15);
            rows[2].During.UpperInclusive.Should().BeFalse();

            var emptyInspection = ctx.From<IPgRangeEntity>()
                .Where(x => x.Id == 2)
                .Select(x => new
                {
                    E = SqlFunctions.Postgres.isempty(x.During),
                    Lf = SqlFunctions.Postgres.lower_inf(x.During),
                    Uf = SqlFunctions.Postgres.upper_inf(x.During)
                })
                .ToList();

            emptyInspection.Should().ContainSingle();
            emptyInspection[0].E.Should().BeTrue();
            emptyInspection[0].Lf.Should().BeFalse();
            emptyInspection[0].Uf.Should().BeFalse();

            var window = new Range<int>(15, 25);
            var overlapping = ctx.From<IPgRangeEntity>()
                .Where(x => SqlFunctions.Postgres.overlaps(x.During, window))
                .Select(x => x.Id)
                .ToList();

            overlapping.Should().BeEquivalentTo(new[] { rows[0].Id });

            var containing35 = ctx.From<IPgRangeEntity>()
                .Where(x => SqlFunctions.Postgres.range_contains(x.During, 35))
                .Select(x => x.Id)
                .ToList();

            containing35.Should().BeEmpty();
        }
        finally
        {
            Execute(ctx, "drop table if exists pg_range_entity");
        }
    }

    [SqlTable("pg_range_types")]
    internal interface IPgRangeTypes
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("r_i4")]
        Range<int> I4 { get; set; }
        [Column("r_i8")]
        Range<long> I8 { get; set; }
        [Column("r_num")]
        Range<decimal> Num { get; set; }
        [Column("r_ts")]
        Range<DateTime> Ts { get; set; }
        [Column("r_tstz")]
        Range<DateTimeOffset> Tstz { get; set; }
        [Column("r_date")]
        Range<DateOnly> Dt { get; set; }
    }

    [Fact]
    public void Range_AllTypes_RoundTrip()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists pg_range_types");
        Execute(ctx, "create table pg_range_types (id integer primary key, r_i4 int4range, r_i8 int8range, r_num numrange, r_ts tsrange, r_tstz tstzrange, r_date daterange)");

        var tsLower = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var tsUpper = new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Unspecified);
        var tstzLower = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tstzUpper = new DateTimeOffset(2023, 1, 2, 0, 0, 0, TimeSpan.Zero);

        try
        {
            ctx.CreateInsertBuilder<IPgRangeTypes>()
                .Value(x => x.Id, 1)
                .Value(x => x.I4, new Range<int>(10, 20))
                .Value(x => x.I8, new Range<long>(1_000_000_000_000L, 2_000_000_000_000L))
                .Value(x => x.Num, new Range<decimal>(1.5m, 2.5m))
                .Value(x => x.Ts, new Range<DateTime>(tsLower, tsUpper))
                .Value(x => x.Tstz, new Range<DateTimeOffset>(tstzLower, tstzUpper))
                .Value(x => x.Dt, new Range<DateOnly>(new DateOnly(2023, 1, 1), new DateOnly(2023, 2, 1)))
                .Insert();

            ctx.CreateInsertBuilder<IPgRangeTypes>()
                .Value(x => x.Id, 2)
                .Value(x => x.I4, Range<int>.Empty)
                .Value(x => x.I8, Range<long>.Empty)
                .Value(x => x.Num, Range<decimal>.Empty)
                .Value(x => x.Ts, Range<DateTime>.Empty)
                .Value(x => x.Tstz, Range<DateTimeOffset>.Empty)
                .Value(x => x.Dt, Range<DateOnly>.Empty)
                .Insert();

            var rows = ctx.From<IPgRangeTypes>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.I4, x.I8, x.Num, x.Ts, x.Tstz, x.Dt })
                .ToList();

            rows.Should().HaveCount(2);

            rows[0].I4.Should().Be(new Range<int>(10, 20));
            rows[0].I8.Should().Be(new Range<long>(1_000_000_000_000L, 2_000_000_000_000L));
            rows[0].Num.Should().Be(new Range<decimal>(1.5m, 2.5m));
            rows[0].Ts.Should().Be(new Range<DateTime>(tsLower, tsUpper));
            rows[0].Tstz.Should().Be(new Range<DateTimeOffset>(tstzLower, tstzUpper));
            rows[0].Dt.Should().Be(new Range<DateOnly>(new DateOnly(2023, 1, 1), new DateOnly(2023, 2, 1)));

            rows[1].I4.IsEmpty.Should().BeTrue();
            rows[1].I8.IsEmpty.Should().BeTrue();
            rows[1].Num.IsEmpty.Should().BeTrue();
            rows[1].Ts.IsEmpty.Should().BeTrue();
            rows[1].Tstz.IsEmpty.Should().BeTrue();
            rows[1].Dt.IsEmpty.Should().BeTrue();
        }
        finally
        {
            Execute(ctx, "drop table if exists pg_range_types");
        }
    }

    [SqlTable("pg_multirange_entity")]
    internal interface IPgMultirangeEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("during")]
        Range<int>[] During { get; set; }
    }

    [SqlTable("pg_range_agg_entity")]
    internal interface IPgRangeAggEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("during")]
        Range<int> During { get; set; }
    }

    [Fact]
    public void Multirange_RoundTripAndOperators()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists pg_multirange_entity");
        Execute(ctx, "create table pg_multirange_entity (id integer primary key, during int4multirange)");

        try
        {
            ctx.CreateInsertBuilder<IPgMultirangeEntity>()
                .Value(x => x.Id, 1)
                .Value(x => x.During, new[] { new Range<int>(1, 5), new Range<int>(10, 20) })
                .Insert();

            var row = ctx.From<IPgMultirangeEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new { x.During })
                .First();

            row.During.Should().HaveCount(2);
            row.During[0].Should().Be(new Range<int>(1, 5));
            row.During[1].Should().Be(new Range<int>(10, 20));

            var probe = ctx.From<IPgMultirangeEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new
                {
                    Overlaps = SqlFunctions.Postgres.overlaps(x.During, new[] { new Range<int>(4, 6) }),
                    Contains = SqlFunctions.Postgres.range_contains(x.During, new Range<int>(2, 3)),
                    Merged = SqlFunctions.Postgres.range_merge(x.During)
                })
                .First();

            probe.Overlaps.Should().BeTrue();
            probe.Contains.Should().BeTrue();
            probe.Merged.Should().Be(new Range<int>(1, 20));
        }
        finally
        {
            Execute(ctx, "drop table if exists pg_multirange_entity");
        }
    }

    [Fact]
    public void RangeAggregates_ShouldAggregateRanges()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists pg_range_agg_entity");
        Execute(ctx, "create table pg_range_agg_entity (id integer primary key, during int4range)");

        try
        {
            foreach (var (id, range) in new[]
            {
                (1, new Range<int>(1, 5)),
                (2, new Range<int>(3, 8)),
                (3, new Range<int>(10, 20))
            })
            {
                ctx.CreateInsertBuilder<IPgRangeAggEntity>()
                    .Value(x => x.Id, id)
                    .Value(x => x.During, range)
                    .Insert();
            }

            var result = ctx.From<IPgRangeAggEntity>()
                .Select(x => new
                {
                    Agg = SqlFunctions.Postgres.range_agg(x.During),
                    Inter = SqlFunctions.Postgres.range_intersect_agg(x.During)
                })
                .First();

            result.Agg.Should().HaveCount(2);
            result.Agg[0].Should().Be(new Range<int>(1, 8));
            result.Agg[1].Should().Be(new Range<int>(10, 20));
            result.Inter.IsEmpty.Should().BeTrue();
        }
        finally
        {
            Execute(ctx, "drop table if exists pg_range_agg_entity");
        }
    }

    [Fact]
    public void ExecuteRaw_JsonbParameter_ShouldBindThroughProviderParameter()
    {
        // A JsonDocument value is bound by PostgresDataContext.CreateParam as jsonb, so the jsonb
        // operators in the raw command accept it without an explicit cast.
        var ctx = _sut.DataProvider;
        using var document = JsonDocument.Parse("""{"name":"alice","age":30}""");

        using var result = ctx.ExecuteRaw(
            "select @v ->> 'name' as value",
            [new ProcedureParameter("v", document)]);

        result.Read<string>().Should().Equal("alice");
    }

    [Fact]
    public void ExecuteProcedure_InOutParameter_ShouldReturnUpdatedValue()
    {
        // Npgsql maps CommandType.StoredProcedure to CALL. PostgreSQL exposes an INOUT parameter's
        // result as a result-set column named after the parameter; Npgsql's output-parameter support
        // copies that column's first-row value into the parameter when the reader is closed, so
        // ProcedureResult.OutputParameters is populated.
        var ctx = _sut.DataProvider;
        var proc = "p_raw_inout_" + Guid.NewGuid().ToString("N")[..12];

        Execute(ctx, $"create procedure {proc}(inout p int, in q int) language plpgsql as $$ begin p := p + q; end; $$");

        try
        {
            using var result = ctx.ExecuteProcedure(
                proc,
                [
                    new ProcedureParameter("p", 10, Direction: ParameterDirection.InputOutput, DbType: DbType.Int32),
                    new ProcedureParameter("q", 5, DbType: DbType.Int32),
                ]);

            var outputs = result.OutputParameters;
            outputs.Should().ContainSingle();
            outputs[0].Name.Should().Be("p");
            outputs[0].Value.Should().Be(15);
        }
        finally
        {
            Execute(ctx, $"drop procedure if exists {proc}");
        }
    }

    [Fact]
    public async Task ExecuteProcedureAsync_InOutParameter_ShouldReturnUpdatedValue()
    {
        var ctx = _sut.DataProvider;
        var proc = "p_raw_ainout_" + Guid.NewGuid().ToString("N")[..12];

        Execute(ctx, $"create procedure {proc}(inout p int, in q int) language plpgsql as $$ begin p := p + q; end; $$");

        try
        {
            await using var result = await ctx.ExecuteProcedureAsync(
                proc,
                [
                    new ProcedureParameter("p", 10, Direction: ParameterDirection.InputOutput, DbType: DbType.Int32),
                    new ProcedureParameter("q", 5, DbType: DbType.Int32),
                ],
                TestContext.Current.CancellationToken);

            var outputs = result.OutputParameters;
            outputs.Should().ContainSingle();
            outputs[0].Name.Should().Be("p");
            outputs[0].Value.Should().Be(15);
        }
        finally
        {
            Execute(ctx, $"drop procedure if exists {proc}");
        }
    }

    [Fact]
    public void ExecuteProcedure_InsertSideEffect_ShouldPersistRow()
    {
        var ctx = _sut.DataProvider;
        var proc = "p_raw_ins_" + Guid.NewGuid().ToString("N")[..12];
        var id = RawProcedureKey();

        Execute(ctx, $"create procedure {proc}(in p_id int, in p_name text) language plpgsql as $$ begin insert into delete_entity (id, name, age) values (p_id, p_name, 1); end; $$");

        try
        {
            using (ctx.ExecuteProcedure(
                proc,
                [
                    new ProcedureParameter("p_id", id, DbType: DbType.Int32),
                    new ProcedureParameter("p_name", "proc-insert"),
                ]))
            {
            }

            ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Name).ToList().Should().Equal("proc-insert");
        }
        finally
        {
            Execute(ctx, $"drop procedure if exists {proc}");
        }
    }

    [Fact]
    public void ExecuteRaw_FunctionViaSelect_ShouldReturnValue()
    {
        // PostgreSQL functions are not procedures: Npgsql's CommandType.StoredProcedure generates
        // CALL, so a function is invoked through ExecuteRaw instead.
        var ctx = _sut.DataProvider;
        var fn = "p_raw_fn_" + Guid.NewGuid().ToString("N")[..12];

        Execute(ctx, $"create function {fn}(a int) returns int language sql as $$ select a * 2 $$");

        try
        {
            using var result = ctx.ExecuteRaw($"select {fn}(@a) as value", [new ProcedureParameter("a", 21, DbType: DbType.Int32)]);

            result.Read<int>().Should().Equal(42);
        }
        finally
        {
            Execute(ctx, $"drop function if exists {fn}(int)");
        }
    }

    private sealed class TvpEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    [Trait("D162", "Conformance")]
    public void ExecuteRaw_TableParameter_Scalar_ShouldUseUnnest()
    {
        var ctx = _sut.DataProvider;

        using (var result = ctx.ExecuteRaw(
            "select count(*) as c from unnest(@ids) as x",
            [ProcedureParameter.Table("ids", new[] { 1, 2, 3 })]))
        {
            result.Read<long>().Should().Equal(3L);
        }

        using (var result = ctx.ExecuteRaw(
            "select sum(x) as s from unnest(@ids) as x",
            [ProcedureParameter.Table("ids", new[] { 1, 2, 3 })]))
        {
            result.Read<long>().Should().Equal(6L);
        }
    }

    [Fact]
    [Trait("D162", "Conformance")]
    public void ExecuteRaw_TableParameter_EmptyScalar_ShouldReadNoRows()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select count(*) as c from unnest(@ids) as x",
            [ProcedureParameter.Table("ids", Array.Empty<int>())]);

        result.Read<long>().Should().Equal(0L);
    }

    [Fact]
    [Trait("D162", "Conformance")]
    public void ExecuteRaw_TableParameter_Entity_ShouldUseJsonbToRecordset()
    {
        var ctx = _sut.DataProvider;

        var rows = new[]
        {
            new TvpEntity { Id = 1, Name = "alpha" },
            new TvpEntity { Id = 2, Name = null },
        };

        using var result = ctx.ExecuteRaw(
            "select x.\"Id\", x.\"Name\" from jsonb_to_recordset(@rows) as x(\"Id\" int, \"Name\" text) order by x.\"Id\"",
            [ProcedureParameter.Table("rows", rows)]);

        var read = result.Read<TvpEntity>();

        read.Should().HaveCount(2);
        read[0].Id.Should().Be(1);
        read[0].Name.Should().Be("alpha");
        read[1].Id.Should().Be(2);
        read[1].Name.Should().BeNull();
    }

    [Fact]
    [Trait("D162", "Conformance")]
    public void ExecuteRaw_TableParameterWithTypeName_ShouldThrowArgumentException()
    {
        var ctx = _sut.DataProvider;

        var act = () => ctx.ExecuteRaw(
            "select 1",
            [ProcedureParameter.Table("p", "my_type", new[] { 1 })]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("D162", "Conformance")]
    public void ExecuteRaw_TableParameter_DateTime_ShouldRoundTripUnnest()
    {
        var ctx = _sut.DataProvider;
        var dates = new[]
        {
            new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
            new DateTime(2024, 6, 7, 8, 9, 10, DateTimeKind.Unspecified),
        };

        using (var count = ctx.ExecuteRaw(
            "select count(*) as c from unnest(@dates) as x",
            [ProcedureParameter.Table("dates", dates)]))
        {
            count.Read<long>().Should().Equal(2L);
        }

        using (var first = ctx.ExecuteRaw(
            "select x from unnest(@dates) as x order by x limit 1",
            [ProcedureParameter.Table("dates", dates)]))
        {
            first.Read<DateTime>().Should().Equal(new DateTime(2024, 1, 2, 3, 4, 5));
        }
    }

    [Fact]
    [Trait("D162", "Conformance")]
    public void ExecuteRaw_TableParameter_NullableIntArray_ShouldKeepNulls()
    {
        var ctx = _sut.DataProvider;
        var ids = new int?[] { 1, null, 3 };

        using (var count = ctx.ExecuteRaw(
            "select count(*) as c from unnest(@ids) as x",
            [ProcedureParameter.Table("ids", ids)]))
        {
            count.Read<long>().Should().Equal(3L);
        }

        using (var nonNull = ctx.ExecuteRaw(
            "select count(x) as c from unnest(@ids) as x",
            [ProcedureParameter.Table("ids", ids)]))
        {
            nonNull.Read<long>().Should().Equal(2L);
        }

        using (var sum = ctx.ExecuteRaw(
            "select sum(x) as s from unnest(@ids) as x",
            [ProcedureParameter.Table("ids", ids)]))
        {
            sum.Read<long>().Should().Equal(4L);
        }
    }

    [SqlTable("complex_entity")]
    internal interface IPgXminReadEntity
    {
        [Key]
        [Column("id")]
        long Id { get; set; }
        [Column("xmin")]
        uint Revision { get; set; }
    }

    [SqlTable("delete_entity")]
    internal interface IPgXminEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("name")]
        string? Name { get; set; }
        [Column("xmin")]
        uint Revision { get; set; }
    }

    [Fact]
    public void Xmin_ReadIntoUInt_ShouldReturnPositiveRevision()
    {
        // PostgreSQL exposes xmin as xid, a 32-bit unsigned integer; the projection reads it through
        // GetFieldValue<uint>. The seeded complex_entity row 1 always has a live xmin.
        var revision = _sut.DataProvider
            .From<IPgXminReadEntity>()
            .Where(x => x.Id == 1)
            .Select(x => x.Revision)
            .First();

        revision.Should().BeGreaterThan(0u);
    }

    [Fact]
    public void Xmin_GuardedUpdate_ShouldRejectStaleToken()
    {
        var ctx = _sut.DataProvider;
        var id = RawProcedureKey();

        ctx.CreateInsertBuilder<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "before", Age = 1 })
            .Insert();

        var token = ctx.From<IPgXminEntity>()
            .Where(x => x.Id == id)
            .Select(x => x.Revision)
            .First();

        token.Should().BeGreaterThan(0u);

        // xmin is the id of the transaction that wrote the row version, so a new version only gets a
        // different xmin when it is written by a different committed transaction. Running the guarded
        // update in its own committed transaction (while the insert above was already autocommitted)
        // makes that boundary explicit and the stale-token assertion deterministic.
        var transactions = (ITransactionManager)ctx;
        uint newToken;
        using (var tx = transactions.BeginTransaction())
        {
            // The current token matches: the guarded update touches exactly the one row and returns its new xmin.
            var updated = ctx.CreateUpdateBuilder<IPgXminEntity>()
                .Set(x => x.Name, "after")
                .Where(x => x.Id == id && x.Revision == token)
                .Returning(x => x.Revision)
                .ToList();

            updated.Should().ContainSingle();
            newToken = updated[0];
            tx.Commit();
        }

        // The update wrote a new row version in its own transaction, so the token actually changed.
        newToken.Should().NotBe(token);

        // The old token is stale and affects zero rows.
        var stale = ctx.CreateUpdateBuilder<IPgXminEntity>()
            .Set(x => x.Name, "stale")
            .Where(x => x.Id == id && x.Revision == token)
            .Returning(x => x.Revision)
            .ToList();

        stale.Should().BeEmpty();

        // The token observed after the first update is current and still affects exactly one row.
        var current = ctx.CreateUpdateBuilder<IPgXminEntity>()
            .Set(x => x.Name, "current")
            .Where(x => x.Id == id && x.Revision == newToken)
            .Returning(x => x.Revision)
            .ToList();

        current.Should().ContainSingle();

        ctx.From<IPgXminEntity>().Where(x => x.Id == id).Select(x => x.Name).Single().Should().Be("current");
    }

    [SqlTable("pg_oid_entity")]
    internal interface IPgOidEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("oid_value")]
        uint Oid { get; set; }
        [Column("cid_value")]
        uint Cid { get; set; }
    }

    [Fact]
    public void Cid_Column_ShouldReadAsUInt()
    {
        // cid is a 32-bit unsigned system type surfaced as System.UInt32 in Npgsql, like oid; the
        // projection reads it through GetFieldValue<uint> even though uint parameters bind as xid.
        var cid = _sut.DataProvider
            .From<IPgOidEntity>()
            .Where(x => x.Id == 1)
            .Select(x => x.Cid)
            .First();

        cid.Should().Be(7u);
    }

    [Fact]
    public void Oid_Column_ShouldReadAsUInt()
    {
        // oid is a 32-bit unsigned system type surfaced as System.UInt32 in Npgsql; nextorm reads it
        // into a uint property even though it renders uint as xid for parameters.
        var oid = _sut.DataProvider
            .From<IPgOidEntity>()
            .Where(x => x.Id == 1)
            .Select(x => x.Oid)
            .First();

        oid.Should().Be(42u);
    }

    [Fact]
    public void Oid_Column_FilterViaLongCast_ShouldMatch()
    {
        // nextorm binds uint as xid and PostgreSQL has no oid = xid operator, so an oid column is
        // compared through an explicit bigint cast; the right-hand value is widened to long so the
        // parameter binds as bigint too (a uint parameter would be xid and xid has no bigint cast).
        var oid = _sut.DataProvider
            .From<IPgOidEntity>()
            .Where(x => x.Id == 1)
            .Select(x => x.Oid)
            .First();
        long expected = oid;

        var ids = _sut.DataProvider
            .From<IPgOidEntity>()
            .Where(e => (long)e.Oid == (long)expected)
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(1);
    }

    [Fact]
    public void Oid_Column_CompareToUIntParam_ShouldFailWithOperatorMissing()
    {
        // A uint parameter is bound as xid and PostgreSQL has no oid = xid operator, so comparing the
        // column to it directly fails with SQLSTATE 42883 (undefined_function).
        var expected = _sut.DataProvider
            .From<IPgOidEntity>()
            .Where(x => x.Id == 1)
            .Select(x => x.Oid)
            .First();

        var act = () => _sut.DataProvider
            .From<IPgOidEntity>()
            .Where(e => e.Oid == expected)
            .Select(e => e.Id)
            .ToList();

        act.Should().Throw<PostgresException>()
            .Which.SqlState.Should().Be("42883");
    }

    // D176.5 provider-native array source: array_agg over a string column is a real PostgreSQL text[],
    // exercised through the recursive JSON writer inside an object member.
    [Fact]
    public void WriteJson_NativeStringArrayAgg_ShouldMatchSerializer()
    {
        var command = _sut.ComplexEntity
            .Select(x => new { Strings = SqlFunctions.Postgres.array_agg(x.RequiredString) });

        using var stream = new MemoryStream();
        command.WriteJson(stream);

        Encoding.UTF8.GetString(stream.ToArray())
            .Should().Be(JsonSerializer.Serialize(command.ToList()));
    }

    // D176 loop-back T5: a provider-native array that is SQL NULL must emit JSON null, and an empty
    // native array must emit []; both beyond the shared non-empty array_agg string source.
    [Fact]
    public void WriteJson_NativeArray_NullAndEmpty_ShouldMatchSerializer()
    {
        // Aggregate over no rows: PostgreSQL returns SQL NULL for array_agg.
        var nullCommand = _sut.ComplexEntity.Where(x => x.Id < 0)
            .Select(x => new { Strings = SqlFunctions.Postgres.array_agg(x.RequiredString) });
        using (var stream = new MemoryStream())
        {
            nullCommand.WriteJson(stream);
            var actual = Encoding.UTF8.GetString(stream.ToArray());
            actual.Should().Be(JsonSerializer.Serialize(nullCommand.ToList()));
            actual.Should().Contain("\"Strings\":null", "a SQL NULL array is JSON null, never []");
        }

        // string_to_array('', ',') is a real empty text[] (no aggregate, no array literal translation).
        var emptyCommand = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Empty = SqlFunctions.Postgres.string_to_array("", ","),
            });
        using (var stream = new MemoryStream())
        {
            emptyCommand.WriteJson(stream);
            var actual = Encoding.UTF8.GetString(stream.ToArray());
            actual.Should().Be(JsonSerializer.Serialize(emptyCommand.ToList()));
            actual.Should().Contain("\"Empty\":[]", "an empty native array is []");
        }
    }

    private static int RawProcedureKey() => Random.Shared.Next(2_000_000, int.MaxValue);

    public interface IDynamicRecordRow
    {
        [Column("a")]
        int A { get; set; }
        [Column("b")]
        string? B { get; set; }
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
