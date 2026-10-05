using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// Mirrors the shared <see cref="CommonTestSuite"/> managed JSON streaming facts (issue #39) against a
/// real MariaDB server. MariaDB does not derive <see cref="CommonTestSuite"/> because it runs against
/// its own container and the shared provider matrix targets MySQL; the shared bodies are reused through
/// their internal static helpers over a small <c>simple_entity</c>/<c>complex_entity</c> fixture seeded
/// with the same rows as every other provider. A MariaDB Testcontainers instance is used unless
/// <c>NEXTORM_MARIADB_CONNECTION</c> points at an existing server.
/// </summary>
public sealed class MariaDbJsonStreamTests : IDisposable
{
    private readonly IDataContext _ctx;
    private readonly TestDataRepository _sut;

    public MariaDbJsonStreamTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
        _sut = new TestDataRepository(_ctx);
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void WriteJson_Array_ShouldMatchJsonSerializer_ForScalar()
        => CommonTestSuite.WriteJsonArrayScalar(_sut);

    [Fact]
    public void WriteJson_Array_ShouldMatchJsonSerializer_ForFlatDto()
        => CommonTestSuite.WriteJsonArrayFlatDto(_sut);

    [Fact]
    public void WriteJson_NdJson_ShouldMatchLineByLine()
        => CommonTestSuite.WriteJsonNdJson(_sut);

    [Fact]
    public void WriteJson_IgnoreNull_ShouldMatchSerializer()
        => CommonTestSuite.WriteJsonIgnoreNull(_sut);

    [Fact]
    public void WriteJson_EmptyResult_ShouldProduceEmptyArray()
        => CommonTestSuite.WriteJsonEmptyResult(_sut);

    [Fact]
    public void WriteJson_TypedColumns_ShouldMatchJsonSerializer()
        => CommonTestSuite.WriteJsonTypedColumns(_sut);

    // Same schema and rows as MySqlTestProvider/ClickHouseTestProvider, kept local because MariaDB
    // does not share the provider seeding matrix.
    private void Seed()
    {
        Execute("drop table if exists simple_entity");
        Execute("create table simple_entity (id int not null primary key)");
        Execute("insert into simple_entity (id) values (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)");

        Execute("drop table if exists complex_entity");
        Execute(
            "create table complex_entity (" +
            "id bigint not null primary key, " +
            "nullableint int null, " +
            "somestring varchar(100) null, " +
            "tinyval tinyint not null, " +
            "small smallint null, " +
            "r float null, " +
            "d double null, " +
            "m decimal(18, 2) null, " +
            "dt datetime null, " +
            "onlydate date not null, " +
            "b tinyint(1) null, " +
            "requiredstring varchar(100) not null)");
        Execute(
            "insert into complex_entity (id, nullableint, somestring, tinyval, small, r, d, m, dt, onlydate, b, requiredstring) values " +
            "(1, null, 'dadfasd', 2, 3, 4, 5, 6, '2023-01-01 10:00:00', '2023-01-01', true, 'sdf'), " +
            "(2, 1, 'xxx', 2, 3, 4, 5, 6, '2023-01-01 00:00:00', '2023-01-01', false, 'asdfgoi'), " +
            "(3, 1, null, 2, 3, null, 5, 6, '2023-01-01 00:00:00', '2023-01-01', false, '34mfs')");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
