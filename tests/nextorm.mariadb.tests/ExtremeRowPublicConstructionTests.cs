using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// External (no <c>InternalsVisibleTo</c>) contract for #154: an assembly that references
/// <c>nextorm.core</c> directly can construct the three extreme-row DTOs through public eager
/// constructors, rely on immediate construction-time validation, and drive its own
/// <see cref="IExtremeRowRenderer"/>. Reflection pins the public surface; the collection-identity
/// assertions pin the borrowed (non-copying) ownership.
/// </summary>
public class ExtremeRowPublicConstructionTests
{
    private static ExtremeRowRenderColumn Column(Type? clrType = null)
        => new(clrType ?? typeof(int), isNullable: false, isDirectMappedColumn: true, usesConverter: false);

    private static ExtremeRowRenderColumn[] Columns(params ExtremeRowRenderColumn[] columns) => columns;

    private static ExtremeRowDescription Description(
        bool isMax = true,
        IReadOnlyList<ExtremeRowRenderColumn>? keys = null,
        IReadOnlyList<ExtremeRowRenderColumn>? groups = null,
        IReadOnlyList<ExtremeRowRenderColumn>? payload = null)
        => new(isMax, keys ?? [Column()], groups ?? [], payload ?? [Column()]);

    private static ExtremeRowRenderRequest Request(
        string sourceSql = "select * from t",
        bool isMax = true,
        IReadOnlyList<string>? payloadAliases = null,
        IReadOnlyList<string>? keyAliases = null,
        IReadOnlyList<ExtremeRowRenderColumn>? keyColumns = null,
        IReadOnlyList<string>? groupAliases = null,
        KeywordCase keywordCase = KeywordCase.Lower)
        => new(
            sourceSql,
            isMax,
            payloadAliases ?? ["p"],
            keyAliases ?? ["k"],
            keyColumns ?? [Column()],
            groupAliases ?? [],
            keywordCase);

    // --- AC-01: public surface -----------------------------------------------------------------

    [Fact]
    public void AllThreeDtos_ShouldBeConstructibleFromThisNonFriendAssembly()
    {
        var column = Column();
        var description = Description(keys: [column]);
        var request = Request(keyColumns: [column]);

        column.ClrType.Should().Be(typeof(int));
        description.Keys.Should().ContainSingle();
        request.KeyColumns.Should().ContainSingle();
    }

    [Fact]
    public void PublicEagerConstructors_ShouldBeTheOnlyPublicConstructors()
    {
        AssertSinglePublicEagerCtor(typeof(ExtremeRowRenderColumn), 4);
        AssertSinglePublicEagerCtor(typeof(ExtremeRowDescription), 4);
        AssertSinglePublicEagerCtor(typeof(ExtremeRowRenderRequest), 7);
    }

    private static void AssertSinglePublicEagerCtor(Type type, int parameterCount)
    {
        var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        ctors.Should().ContainSingle($"{type.Name} must expose exactly one public eager constructor");
        ctors[0].GetParameters().Should().HaveCount(parameterCount);
        ctors[0].GetParameters().Should().OnlyContain(
            p => !typeof(Delegate).IsAssignableFrom(p.ParameterType)
                 && !(p.ParameterType.IsGenericType
                      && p.ParameterType.GetGenericTypeDefinition() == typeof(Lazy<>)),
            "no public factory/lazy overload may be introduced");
    }

    // --- AC-02: required references ------------------------------------------------------------

    [Fact]
    public void Column_NullClrType_ShouldThrowArgumentNullException()
    {
        Action act = () => _ = new ExtremeRowRenderColumn(null!, false, false, false);
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("clrType");
    }

    [Fact]
    public void Description_NullCollections_ShouldEachThrowWithTheRightParamName()
    {
        Action?[] acts =
        [
            () => _ = new ExtremeRowDescription(true, null!, [], [Column()]),
            () => _ = new ExtremeRowDescription(true, [Column()], null!, [Column()]),
            () => _ = new ExtremeRowDescription(true, [Column()], [], null!),
        ];
        string[] names = ["keys", "groups", "payload"];
        for (var i = 0; i < acts.Length; i++)
            acts[i]!.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be(names[i]);
    }

    [Fact]
    public void Request_NullReferences_ShouldEachThrowWithTheRightParamName()
    {
        Action?[] acts =
        [
            () => _ = new ExtremeRowRenderRequest(null!, true, ["p"], ["k"], [Column()], [], KeywordCase.Lower),
            () => _ = new ExtremeRowRenderRequest("s", true, null!, ["k"], [Column()], [], KeywordCase.Lower),
            () => _ = new ExtremeRowRenderRequest("s", true, ["p"], null!, [Column()], [], KeywordCase.Lower),
            () => _ = new ExtremeRowRenderRequest("s", true, ["p"], ["k"], null!, [], KeywordCase.Lower),
            () => _ = new ExtremeRowRenderRequest("s", true, ["p"], ["k"], [Column()], null!, KeywordCase.Lower),
        ];
        string[] names = ["sourceSql", "payloadAliases", "keyAliases", "keyColumns", "groupAliases"];
        for (var i = 0; i < acts.Length; i++)
            acts[i]!.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be(names[i]);
    }

    // --- AC-03: null collection elements -------------------------------------------------------

    [Fact]
    public void Description_NullElement_ShouldThrowArgumentExceptionWithParamAndIndex()
    {
        Action keysFirst = () => _ = new ExtremeRowDescription(true, [null!, Column()], [], [Column()]);
        Action keysLater = () => _ = new ExtremeRowDescription(true, [Column(), null!], [], [Column()]);
        Action groups = () => _ = new ExtremeRowDescription(true, [Column()], [null!], [Column()]);
        Action payload = () => _ = new ExtremeRowDescription(true, [Column()], [], [null!]);

        keysFirst.Should().Throw<ArgumentException>().WithParameterName("keys").WithMessage("*index 0*");
        keysLater.Should().Throw<ArgumentException>().WithParameterName("keys").WithMessage("*index 1*");
        groups.Should().Throw<ArgumentException>().WithParameterName("groups").WithMessage("*index 0*");
        payload.Should().Throw<ArgumentException>().WithParameterName("payload").WithMessage("*index 0*");
    }

    [Fact]
    public void Request_NullElement_ShouldThrowArgumentExceptionWithParamAndIndex()
    {
        Action payloadAliases = () => _ = Request(payloadAliases: new string[] { "a", null! });
        Action keyAliases = () => _ = Request(keyAliases: new string[] { null!, "k" });
        Action keyColumns = () => _ = Request(keyColumns: new ExtremeRowRenderColumn[] { Column(), null! });
        Action groupAliases = () => _ = Request(groupAliases: new string[] { null! });

        payloadAliases.Should().Throw<ArgumentException>().WithParameterName("payloadAliases").WithMessage("*index 1*");
        keyAliases.Should().Throw<ArgumentException>().WithParameterName("keyAliases").WithMessage("*index 0*");
        keyColumns.Should().Throw<ArgumentException>().WithParameterName("keyColumns").WithMessage("*index 1*");
        groupAliases.Should().Throw<ArgumentException>().WithParameterName("groupAliases").WithMessage("*index 0*");
    }

    // --- AC-04: source SQL, aliases, enum ------------------------------------------------------

    [Fact]
    public void Request_SourceSql_ShouldDistinguishNullEmptyWhitespaceAndOrdinary()
    {
        Action nullSql = () => _ = Request(sourceSql: null!);
        Action emptySql = () => _ = Request(sourceSql: string.Empty);
        Action whitespaceSql = () => _ = Request(sourceSql: "   ");

        nullSql.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("sourceSql");
        emptySql.Should().Throw<ArgumentException>().WithParameterName("sourceSql");
        whitespaceSql.Should().Throw<ArgumentException>().WithParameterName("sourceSql");

        Request(sourceSql: "select 1").SourceSql.Should().Be("select 1");
    }

    [Fact]
    public void Request_Aliases_ShouldRejectNullAndEmptyButAcceptWhitespaceOnly()
    {
        Action nullElement = () => _ = Request(payloadAliases: new string[] { "a", null! });
        Action emptyElement = () => _ = Request(payloadAliases: new string[] { "a", string.Empty });
        Action whitespaceOnly = () => _ = Request(payloadAliases: ["   "]);

        nullElement.Should().Throw<ArgumentException>().WithParameterName("payloadAliases");
        emptyElement.Should().Throw<ArgumentException>().WithParameterName("payloadAliases").WithMessage("*index 1*");
        whitespaceOnly.Should().NotThrow("whitespace-only is non-empty and therefore accepted");

        Request(payloadAliases: ["a", "b"]).PayloadAliases.Should().Equal("a", "b");
    }

    [Fact]
    public void Request_KeywordCase_ShouldAcceptEveryDeclaredValueAndRejectUndefined()
    {
        foreach (var value in Enum.GetValues<KeywordCase>())
            Request(keywordCase: value).KeywordCase.Should().Be(value);

        Action undefined = () => _ = Request(keywordCase: (KeywordCase)99);
        undefined.Should().Throw<ArgumentException>().WithParameterName("keywordCase");
    }

    // --- AC-05: shapes without invented rules --------------------------------------------------

    public static TheoryData<bool, bool> ShapeAndDirection()
    {
        var data = new TheoryData<bool, bool>();
        foreach (var isMax in new[] { true, false })
            foreach (var shape in new[] { false, true })
                data.Add(isMax, shape);
        return data;
    }

    [Theory]
    [MemberData(nameof(ShapeAndDirection))]
    public void EveryShape_ShouldConstructThroughDescriptionAndRequestInBothDirections(bool isMax, bool composite)
    {
        var key1 = Column();
        var key2 = Column(typeof(string));
        var groupKey = Column(typeof(int));

        // Global shape; grouped shape; composite-overlap (group repeats a key, payload overlaps a key); empty shape.
        ExtremeRowDescription[] descriptions = composite
            ?
            [
                Description(isMax),
                Description(isMax, keys: [key1], groups: [groupKey]),
                Description(isMax, keys: [key1, key2], groups: [key1], payload: [key1, key2]),
                Description(isMax, keys: [], groups: [], payload: []),
            ]
            :
            [
                Description(isMax, keys: [key1], groups: [], payload: [key2]),
                Description(isMax, keys: [key1], groups: [groupKey], payload: [key2]),
                Description(isMax, keys: [key1], groups: [], payload: []),
                Description(isMax, keys: [], groups: [], payload: []),
            ];

        foreach (var description in descriptions)
            description.IsMax.Should().Be(isMax);

        ExtremeRowRenderRequest[] requests = composite
            ?
            [
                Request(isMax: isMax),
                Request(isMax: isMax, keyAliases: ["k1"], groupAliases: ["g1"]),
                Request(isMax: isMax, payloadAliases: ["k1", "k2"], keyAliases: ["k1"], keyColumns: [key1, key2], groupAliases: ["k1"]),
                Request(isMax: isMax, payloadAliases: [], keyAliases: [], keyColumns: [], groupAliases: []),
            ]
            :
            [
                Request(isMax: isMax, payloadAliases: ["p"], keyAliases: ["k1"], keyColumns: [key1], groupAliases: []),
                Request(isMax: isMax, payloadAliases: ["p"], keyAliases: ["k1"], keyColumns: [key1], groupAliases: ["g1"]),
                Request(isMax: isMax, payloadAliases: [], keyAliases: ["k1"], keyColumns: [key1], groupAliases: []),
                Request(isMax: isMax, payloadAliases: [], keyAliases: [], keyColumns: [], groupAliases: []),
            ];

        foreach (var request in requests)
            request.IsMax.Should().Be(isMax);

        requests[^1].PayloadAliases.Should().BeEmpty();
        requests[^1].KeyAliases.Should().BeEmpty();
        requests[^1].GroupAliases.Should().BeEmpty();
    }

    [Fact]
    public void CompositeOverlapAcrossLists_ShouldBeAccepted()
    {
        var key = Column();
        var description = Description(keys: [key], groups: [key], payload: [key]);
        var request = Request(payloadAliases: ["x"], keyAliases: ["x"], keyColumns: [key], groupAliases: ["x"]);

        description.Keys.Should().ContainSingle();
        request.KeyAliases.Should().ContainSingle();
    }

    // --- AC-06: borrowed identity --------------------------------------------------------------

    [Fact]
    public void Description_ShouldBorrowEveryCollectionIncludingEagerPayload()
    {
        var keys = new List<ExtremeRowRenderColumn> { Column() };
        var groups = new List<ExtremeRowRenderColumn> { Column() };
        var payload = new List<ExtremeRowRenderColumn> { Column() };
        var description = new ExtremeRowDescription(true, keys, groups, payload);

        ReferenceEquals(description.Keys, keys).Should().BeTrue();
        ReferenceEquals(description.Groups, groups).Should().BeTrue();
        ReferenceEquals(description.Payload, payload).Should().BeTrue("the eager payload must not be copied or wrapped");
    }

    [Fact]
    public void Request_ShouldBorrowEveryCollection()
    {
        var payloadAliases = new List<string> { "p" };
        var keyAliases = new List<string> { "k" };
        var keyColumns = new List<ExtremeRowRenderColumn> { Column() };
        var groupAliases = new List<string> { "g" };
        var request = new ExtremeRowRenderRequest("select 1", true, payloadAliases, keyAliases, keyColumns, groupAliases, KeywordCase.Upper);

        ReferenceEquals(request.PayloadAliases, payloadAliases).Should().BeTrue();
        ReferenceEquals(request.KeyAliases, keyAliases).Should().BeTrue();
        ReferenceEquals(request.KeyColumns, keyColumns).Should().BeTrue();
        ReferenceEquals(request.GroupAliases, groupAliases).Should().BeTrue();
    }

    // --- AC-08: capability independence --------------------------------------------------------

    [Fact]
    public void ValidDto_WithDecliningRenderer_ShouldRejectIndependentlyOfConstruction()
    {
        var renderer = new StubRenderer(canRender: false);
        var description = Description();
        var request = Request();

        renderer.CanRender(description).Should().BeFalse("CanRender is a separate decision");
        renderer.RenderCalls.Should().Be(0);
        renderer.CanRenderCalls.Should().Be(1);

        // Construction must not have consulted the renderer at all.
        renderer.CanRenderCalls.Should().Be(1);
        request.SourceSql.Should().Be("select * from t");
    }

    [Fact]
    public void SupportedDto_ShouldRenderThroughACustomRendererWithoutConstructionCalls()
    {
        var renderer = new StubRenderer(canRender: true);

        var description = Description(isMax: false);
        var request = Request(sourceSql: "select * from t where k is not null", isMax: false);

        renderer.CanRenderCalls.Should().Be(0, "constructing a DTO must not call CanRender");
        renderer.RenderCalls.Should().Be(0, "constructing a DTO must not call Render");

        renderer.CanRender(description).Should().BeTrue();
        renderer.Render(request).Should().Be("native(select * from t where k is not null)");
        renderer.RenderCalls.Should().Be(1);
    }

    private sealed class StubRenderer(bool canRender) : IExtremeRowRenderer
    {
        public int CanRenderCalls { get; private set; }

        public int RenderCalls { get; private set; }

        public bool CanRender(ExtremeRowDescription description)
        {
            CanRenderCalls++;
            return canRender;
        }

        public string Render(ExtremeRowRenderRequest request)
        {
            RenderCalls++;
            return "native(" + request.SourceSql + ")";
        }
    }
}
