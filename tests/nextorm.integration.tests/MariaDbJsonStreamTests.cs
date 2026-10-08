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

    // D178 (#178) enum storage mirrors: MariaDB does not derive CommonTestSuite.
    [Fact]
    public void EnumStorage_NumericMember_ShouldMatchJsonSerializer()
        => CommonTestSuite.EnumStorageNumericMember(_sut);

    [Fact]
    public void EnumStorage_StringMember_ShouldMatchJsonSerializer()
        => CommonTestSuite.EnumStorageStringMember(_sut);

    [Fact]
    public void EnumStorage_GenericStringMember_ShouldMatchJsonSerializer()
        => CommonTestSuite.EnumStorageGenericStringMember(_sut);

    [Fact]
    public void EnumStorage_FlatObject_ShouldMatchJsonSerializer()
        => CommonTestSuite.EnumStorageFlatObject(_sut);

    [Fact]
    public void EnumStorage_FieldType_ShouldBeNumeric()
        => CommonTestSuite.RecordEnumStorageFieldType(_sut, "mariadb");

    // D176.5 phase-2 mirrors: MariaDB does not derive CommonTestSuite, so the shared nested/object/
    // slot/conditional bodies are re-pinned here explicitly (inheriting the facts is not enough).

    [Fact]
    public void WriteJson_NestedAnonymous_ShouldMatchSerializer()
        => CommonTestSuite.WriteJsonNestedAnonymous(_sut);

    [Fact]
    public void WriteJson_NestedMultipleLevels_ShouldMatchSerializer()
        => CommonTestSuite.WriteJsonNestedMultipleLevels(_sut);

    [Fact]
    public void WriteJson_NestedNamed_ShouldMatchSerializer()
        => CommonTestSuite.WriteJsonNestedNamed(_sut);

    [Fact]
    public void WriteJson_NestedAllNullChild_ShouldStayObject()
        => CommonTestSuite.WriteJsonNestedAllNullChild(_sut);

    [Fact]
    public void WriteJson_NestedSameNameDifferentScopes_ShouldBeAccepted()
        => CommonTestSuite.WriteJsonNestedSameNameDifferentScopes(_sut);

    [Fact]
    public void WriteJson_ConditionalNullArmTrue_ShouldMatchSerializer()
        => CommonTestSuite.WriteJsonConditionalNullArmTrue(_sut);

    [Fact]
    public void WriteJson_ConditionalNullArmFalse_ShouldMatchSerializer()
        => CommonTestSuite.WriteJsonConditionalNullArmFalse(_sut);

    [Fact]
    public void WriteJson_ConditionalAllNullObject_ShouldStayObject()
        => CommonTestSuite.WriteJsonConditionalAllNullObject(_sut);

    [Fact]
    public void WriteJson_ConditionalMemberInit_ShouldMatchSerializer()
        => CommonTestSuite.WriteJsonConditionalMemberInit(_sut);

    [Fact]
    public void WriteJson_ConditionalBothArmsConstruction_ShouldThrowBeforeOutput()
        => CommonTestSuite.WriteJsonConditionalBothArmsConstruction(_sut);

    [Fact]
    public void WriteJson_BareProjectionLeftJoin_ShouldMatchItem1Item2()
        => CommonTestSuite.WriteJsonBareProjectionLeftJoin(_sut);

    [Fact]
    public void WriteJson_BareProjectionInnerJoin_ShouldMatchItem1Item2()
        => CommonTestSuite.WriteJsonBareProjectionInnerJoin(_sut);

    [Fact]
    public void WriteJson_DuplicateLeafNamesAcrossSlots_ShouldBeAccepted()
        => CommonTestSuite.WriteJsonDuplicateLeafNamesAcrossSlots(_sut);

    [Fact]
    public void WriteJson_EntityAndScalarSlot_ShouldNotWrapScalar()
        => CommonTestSuite.WriteJsonEntityAndScalarSlot(_sut);

    [Fact]
    public void WriteJson_ScalarScalarSlots_ShouldBeFlatScalars()
        => CommonTestSuite.WriteJsonScalarScalarSlots(_sut);

    [Fact]
    public void WriteJson_ByteArrayNested_ShouldBeBase64()
        => CommonTestSuite.WriteJsonByteArrayNested(_sut);

    [Fact]
    public void WriteJson_ByteArray_ShouldMatchJsonSerializer()
        => CommonTestSuite.WriteJsonByteArray(_sut);

    [Fact]
    public void WriteJson_DuplicateNameWithinOneObject_ShouldThrowBeforeOutput()
        => CommonTestSuite.WriteJsonDuplicateNameWithinOneObject(_sut);

    [Fact]
    public async Task WriteJson_WholeEntityAsync_ShouldMatchSync()
        => await CommonTestSuite.WriteJsonWholeEntityAsyncMatchesSync(_sut);

    [Fact]
    public async Task WriteJson_EntityAndScalarSlotAsync_ShouldMatchSync()
        => await CommonTestSuite.WriteJsonEntityAndScalarSlotAsyncMatchesSync(_sut);

    [Fact]
    public async Task WriteJson_ScalarScalarSlotsAsync_ShouldMatchSync()
        => await CommonTestSuite.WriteJsonScalarScalarSlotsAsyncMatchesSync(_sut);

    [Fact]
    public async Task WriteJson_ConditionalRootAsync_ShouldMatchSync()
        => await CommonTestSuite.WriteJsonConditionalRootAsyncMatchesSync(_sut);

    [Fact]
    public async Task WriteJson_NestedAsync_ShouldMatchSync()
        => await CommonTestSuite.WriteJsonNestedAsyncMatchesSync(_sut);

    [Fact]
    public async Task WriteJson_SlotAsync_ShouldMatchSync()
        => await CommonTestSuite.WriteJsonSlotAsyncMatchesSync(_sut);

    [Fact]
    public async Task WriteJson_ConditionalAsync_ShouldMatchSync()
        => await CommonTestSuite.WriteJsonConditionalAsyncMatchesSync(_sut);

    [Fact]
    public async Task WriteJson_ByteArrayAsync_ShouldMatchSync()
        => await CommonTestSuite.WriteJsonByteArrayAsyncMatchesSync(_sut);

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

        // D176.5 phase-2 slot/Base64 mirrors use the same binary fixture as the shared providers.
        Execute("drop table if exists binary_entity");
        Execute("create table binary_entity (id int not null primary key, data varbinary(16) null)");
        Execute("insert into binary_entity (id, data) values (1, x'01020304'), (2, null)");

        // D178 (#178) enum storage: an enum is persisted as its underlying integer.
        Execute("drop table if exists enum_entity");
        Execute("create table enum_entity (id int not null primary key, state int not null, nullable_state int null)");
        Execute("insert into enum_entity (id, state, nullable_state) values (1, 7, null), (2, -3, 7), (3, 0, -3)");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
