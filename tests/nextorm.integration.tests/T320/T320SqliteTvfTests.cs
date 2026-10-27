using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Row shape of SQLite's bundled <c>json_each</c>: for a JSON array, <c>key</c> is the zero-based index
/// and <c>value</c> the element.
/// </summary>
public interface IT320SqJsonEachRow
{
    [Column("key")]
    long Key { get; set; }

    [Column("value")]
    long Value { get; set; }
}

/// <summary>
/// Row shape of SQLite's <c>json_each</c> when the elements are strings mapped through a value converter:
/// the raw column is SQLite TEXT (<c>Alpha</c>/<c>Beta</c>) and the CLR property is an enum.
/// </summary>
public interface IT320SqJsonEachWordRow
{
    [Column("key")]
    long Key { get; set; }

    [Column("value")]
    [ValueConverter(typeof(EnumToStringConverter<T320SqWord>))]
    T320SqWord Word { get; set; }
}

/// <summary>The CLR model type of the converted <c>json_each</c> row property.</summary>
public enum T320SqWord
{
    Alpha,
    Beta,
}

/// <summary>
/// Row shape of <c>json_each</c> with a nullable value column: a JSON <c>null</c> element materialises as
/// <see langword="null" />, never as a default or an exception.
/// </summary>
public interface IT320SqJsonEachNullableRow
{
    [Column("key")]
    long Key { get; set; }

    [Column("value")]
    long? Value { get; set; }
}

/// <summary>
/// The runtime TVF factory under live SQLite test: <c>json_each</c> is a built-in table-valued function
/// (JSON1 is compiled into the bundled SQLite), so it executes in-process with no database container.
/// </summary>
internal static class T320SqIntegrationTvf
{
    public static TvfEntityBuilder<IT320SqJsonEachRow> JsonEach(string json) => new("json_each");

    public static TvfEntityBuilder<IT320SqJsonEachNullableRow> JsonEachNullable(string json) => new("json_each");

    public static TvfEntityBuilder<IT320SqJsonEachWordRow> JsonEachWord(string json) => new("json_each");
}

/// <summary>
/// #320 (T320) live SQLite evidence for a runtime TVF source (matrix row MX-SQ, AC04/AC06/AC09). The
/// function is executed against a real file-based database: rows materialise, an ordinary join over the
/// function returns the intersecting rows, the correlated APPLY is rejected by name (SQLite has no
/// APPLY/LATERAL), and a capture change on the same cached plan instance changes the row set.
/// </summary>
[Trait("Category", "T320")]
public sealed class T320SqliteTvfTests(ITestOutputHelper output) : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext context, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static List<string> ParametersOf<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")];

    /// <summary>
    /// A literal-argument runtime TVF executes on the file-based database and materialises every row the
    /// function returns.
    /// </summary>
    [Fact]
    public void RuntimeTvf_WithLiteralArgument_ShouldExecuteAndMaterialiseRows()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        var command = Context.From(() => T320SqIntegrationTvf.JsonEach("[1,2,3]"))
            .Select(r => r.Value);

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"json_each sql: <<{sql}>>");

        sql.Should().Contain("json_each(", "the runtime descriptor renders the bundled table-valued function");

        var rows = command.ToList().OrderBy(v => v).ToArray();

        rows.Should().Equal(new long[] { 1, 2, 3 }, "json_each('[1,2,3]') yields three rows with their element values");
    }

    /// <summary>
    /// An ordinary (non-APPLY) join over the TVF source is supported and returns the intersecting rows.
    /// </summary>
    [Fact]
    public void OrdinaryJoinOverTvf_ShouldReturnTheIntersectingRows()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        var command = Context.From(() => T320SqIntegrationTvf.JsonEach("[1,2,3]"))
            .Join(Context.From<ISimpleEntity>(), (tvf, entity) => tvf.Value == entity.Id)
            .Select(p => new { Number = p.Item1.Value, Id = p.Item2.Id })
            .OrderBy(x => x.Number);

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"join sql: <<{sql}>>");

        sql.Should().Contain("join simple_entity", "the ordinary table is the joined source");

        var rows = command.ToList();

        rows.Should().HaveCount(3, "json_each('[1,2,3]') intersects the seeded ids 1..3");
        rows.Select(r => r.Number).Should().Equal(new long[] { 1, 2, 3 }, "the joined rows carry the TVF column");
        rows.Select(r => r.Id).Should().Equal(new[] { 1, 2, 3 }, "the ordinary side carries the table column");
    }

    /// <summary>
    /// SQLite has no APPLY/LATERAL source: the correlated forms are rejected by name while the real
    /// database is reachable, never silently rendered as an ordinary join.
    /// </summary>
    [Fact]
    public void CorrelatedApply_ShouldBeRejectedByTheDialect()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        Context.From<IComplexEntity>().Count().Should().Be(3, "the real database is reachable before the rejected calls");

        Action cross = () => Prepare(
            Context,
            Context.From<IComplexEntity>()
                .CrossApply(row => T320SqIntegrationTvf.JsonEach(row.RequiredString))
                .Select(p => p.Item1.Id),
            storeInCache: false);
        cross.Should().Throw<NotSupportedException>()
            .WithMessage("*CrossApply join is not supported by this SQL dialect*");

        Action outer = () => Prepare(
            Context,
            Context.From<IComplexEntity>()
                .OuterApply(row => T320SqIntegrationTvf.JsonEach(row.RequiredString))
                .Select(p => p.Item1.Id),
            storeInCache: false);
        outer.Should().Throw<NotSupportedException>()
            .WithMessage("*OuterApply join is not supported by this SQL dialect*");
    }

    /// <summary>
    /// A captured argument is a bound parameter: changing it and executing again reuses the same cached
    /// plan instance, refreshes the parameter and therefore changes the row set.
    /// </summary>
    [Fact]
    public void CapturedArgument_ShouldRefreshOnCacheHitAndChangeTheRowSet()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        Context.PurgeQueryCache();

        var capturedJson = "[1,2]";
        QueryCommand<long> Build() => Context.From(() => T320SqIntegrationTvf.JsonEach(capturedJson)).Select(r => r.Value);

        var first = Prepare(Context, Build(), storeInCache: true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        var firstObserved = $"rendered statement: <<{firstSql}>>; parameters: [{string.Join(", ", ParametersOf(first))}]";
        output.WriteLine($"first run: {firstObserved}");

        ParametersOf(first).Should().Equal(new[] { "capturedJson=[1,2]" }, $"the capture is a bound parameter; {firstObserved}");
        firstSql.Should().Contain("json_each(", firstObserved);
        firstSql.Should().NotContain("[1,2]", $"a captured value is never inlined; {firstObserved}");

        var firstRows = Build().ToList().OrderBy(x => x).ToArray();
        firstRows.Should().Equal(new long[] { 1, 2 }, $"the captured document has two elements; {firstObserved}");

        capturedJson = "[3,4,5]";

        var second = Prepare(Context, Build(), storeInCache: true);
        var secondSql = Normalize(second.DbCommand.CommandText);
        var secondObserved = $"rendered statement: <<{secondSql}>>; parameters: [{string.Join(", ", ParametersOf(second))}]";
        output.WriteLine($"second run: {secondObserved}");

        ReferenceEquals(first, second).Should().BeTrue(
            $"the same shape must reuse the cached plan so the refresh path is exercised; {secondObserved}");
        secondSql.Should().Be(firstSql, $"a capture change must not re-render the statement; {secondObserved}");
        ParametersOf(second).Should().Equal(
            new[] { "capturedJson=[3,4,5]" },
            $"the cache hit must bind the new capture, never the stale document; {secondObserved}");

        var secondRows = Build().ToList().OrderBy(x => x).ToArray();
        secondRows.Should().Equal(
            new long[] { 3, 4, 5 },
            $"the refreshed capture selects three elements on the same context; {secondObserved}");
    }

    /// <summary>
    /// AC05 (live projected rows): a composed chain — the factory call plus a <c>Where</c>, an ordering and
    /// a type-changing projection — executes on the file database and the projected rows carry the values
    /// the function returns, filtered and ordered by the composed operations.
    /// </summary>
    [Fact]
    public void ComposedChain_WithWhereOrderingAndTypeChangingSelect_ShouldProjectTheLiveRows()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        var json = "[3,1,4,1,5]";

        // Where and the ordering are composed on the descriptor inside the source lambda, so the lowering
        // re-applies them over the originating factory call; the projection then changes the result type.
        var command = Context.From(() => T320SqIntegrationTvf.JsonEach(json)
                .Where(row => row.Value > 1)
                .OrderBy(row => row.Value))
            .Select(row => new { row.Key, Scaled = row.Value * 10, IsOdd = row.Value % 2 == 1 });

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"composed chain sql: <<{sql}>>");

        sql.Should().Contain("json_each(", $"the factory call stays the source; <<{sql}>>");
        sql.Should().Contain(" where ", $"the composed predicate is applied; <<{sql}>>");
        sql.Should().Contain(" order by ", $"the composed ordering is applied; <<{sql}>>");

        var rows = command.ToList();
        var observed = string.Join(
            "; ",
            rows.Select(row => $"key={row.Key} scaled={row.Scaled} isOdd={row.IsOdd}"));
        output.WriteLine($"composed chain rows: [{observed}]");

        rows.Should().HaveCount(3, $"only the elements above 1 survive the predicate; [{observed}]");
        rows.Select(row => row.Key).Should().Equal(new long[] { 0, 2, 4 }, $"ordered by element value; [{observed}]");
        rows.Select(row => row.Scaled).Should().Equal(new long[] { 30, 40, 50 }, $"the projected value is computed per row; [{observed}]");
        rows.Select(row => row.IsOdd).Should().Equal(new[] { true, false, true }, $"the projection is evaluated per row; [{observed}]");
    }

    /// <summary>
    /// AC05 (live projected rows): the same source reached through the copy path — the descriptor upcast to
    /// <see cref="EntityBuilder{T}" /> and cloned — keeps its runtime descriptor type, and the copy bound to
    /// the context executes on the file database with the same statement, the same bound argument and the
    /// same rows as the direct consumer.
    /// </summary>
    [Fact]
    public void CloneAndUpcast_ShouldExecuteTheSameSourceArgumentAndRowsLive()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        var json = "[6,7]";

        // (1) Descriptor-level copy: the unbound runtime descriptor, reached through the EntityBuilder<T>
        // static type, keeps the runtime descriptor type and its metadata when cloned.
        var descriptor = T320SqIntegrationTvf.JsonEach(json);
        EntityBuilder<IT320SqJsonEachRow> upcastDescriptor = descriptor;
        var descriptorClone = upcastDescriptor.Clone();

        descriptorClone.Should().BeOfType<TvfEntityBuilder<IT320SqJsonEachRow>>(
            "the copy path keeps the runtime descriptor type of the unbound descriptor");
        descriptorClone.Should().NotBeSameAs(upcastDescriptor, "the copy path returns a copy, not the same descriptor");
        ((TvfEntityBuilder<IT320SqJsonEachRow>)descriptorClone).Name.Should().Be(
            "json_each", "the cloned descriptor still names the table-valued function");

        // (2) Bound copy executed live: the descriptor bound to the context, upcast and cloned, renders and
        // executes exactly like the direct consumer, argument included.
        var bound = Context.From(() => T320SqIntegrationTvf.JsonEach(json));
        EntityBuilder<IT320SqJsonEachRow> upcastBound = bound;
        var clone = upcastBound.Clone();

        clone.Should().NotBeSameAs(upcastBound, "the copy path returns a copy, not the same bound builder");

        var direct = Prepare(Context, bound.Select(row => row.Value), storeInCache: false);
        var cloned = Prepare(Context, clone.Select(row => row.Value), storeInCache: false);

        var directObserved =
            $"sql=<<{Normalize(direct.DbCommand.CommandText)}>>; parameters: [{string.Join(", ", ParametersOf(direct))}]";
        var clonedObserved =
            $"sql=<<{Normalize(cloned.DbCommand.CommandText)}>>; parameters: [{string.Join(", ", ParametersOf(cloned))}]";
        output.WriteLine($"direct consumer: {directObserved}");
        output.WriteLine($"cloned consumer: {clonedObserved}");

        Normalize(cloned.DbCommand.CommandText).Should().Be(
            Normalize(direct.DbCommand.CommandText),
            $"the clone renders the statement of the source it was copied from; {clonedObserved}");
        ParametersOf(cloned).Should().Equal(
            ParametersOf(direct),
            $"the clone binds the same argument value as the source; direct {directObserved}; cloned {clonedObserved}");
        ParametersOf(direct).Should().ContainSingle(
            $"the descriptor carries exactly one argument slot; {directObserved}").Which
            .Should().EndWith("=[6,7]", $"the bound argument is the captured document; {directObserved}");

        var directRows = bound.Select(row => row.Value).ToList().OrderBy(value => value).ToArray();
        var clonedRows = clone.Select(row => row.Value).ToList().OrderBy(value => value).ToArray();
        output.WriteLine(
            $"direct rows: [{string.Join(", ", directRows)}]; cloned rows: [{string.Join(", ", clonedRows)}]");

        directRows.Should().Equal(new long[] { 6, 7 }, "the direct consumer materialises the two elements");
        clonedRows.Should().Equal(new long[] { 6, 7 }, "the cloned consumer materialises the same two elements");
    }

    /// <summary>
    /// AC05 (live projected rows): two consumers of the same descriptor factory on two different contexts
    /// each keep their own captured argument and their own rows — no cross-context leak — and neither
    /// preparation touches the sticky <c>QueryCommand.Cache</c> flag of the shared <c>Any</c> command.
    /// </summary>
    [Fact]
    public void TwoConsumersOfTheSameDescriptor_ShouldKeepTheirOwnRowsAndLeaveTheSharedAnyCommandCacheable()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        using var otherContext = Provider.CreateContext();

        Context.From<ISimpleEntity>().Any().Should().BeTrue("the seeded database is reachable on the first context");
        otherContext.From<ISimpleEntity>().Any().Should().BeTrue("the same file database is reachable on the second context");

        ((IQueryCache)Context).AnyCommand!.Value.Cache.Should().BeTrue("the shared Any command starts cacheable");
        ((IQueryCache)otherContext).AnyCommand!.Value.Cache.Should().BeTrue("the shared Any command starts cacheable");

        var jsonA = "[1,2]";
        var jsonB = "[7,8,9]";

        var first = Prepare(
            Context,
            Context.From(() => T320SqIntegrationTvf.JsonEach(jsonA)).Select(row => row.Value),
            storeInCache: true);
        var second = Prepare(
            otherContext,
            otherContext.From(() => T320SqIntegrationTvf.JsonEach(jsonB)).Select(row => row.Value),
            storeInCache: true);

        var firstObserved = $"sql=<<{Normalize(first.DbCommand.CommandText)}>>; parameters: [{string.Join(", ", ParametersOf(first))}]";
        var secondObserved = $"sql=<<{Normalize(second.DbCommand.CommandText)}>>; parameters: [{string.Join(", ", ParametersOf(second))}]";
        output.WriteLine($"context A consumer: {firstObserved}");
        output.WriteLine($"context B consumer: {secondObserved}");

        ParametersOf(first).Should().Equal(new[] { "jsonA=[1,2]" }, $"each consumer binds its own capture; {firstObserved}");
        ParametersOf(second).Should().Equal(new[] { "jsonB=[7,8,9]" }, $"the second context never sees the first capture; {secondObserved}");

        var rowsA = Context.From(() => T320SqIntegrationTvf.JsonEach(jsonA)).Select(row => row.Value).ToList().OrderBy(value => value).ToArray();
        var rowsB = otherContext.From(() => T320SqIntegrationTvf.JsonEach(jsonB)).Select(row => row.Value).ToList().OrderBy(value => value).ToArray();
        var rowsAAgain = Context.From(() => T320SqIntegrationTvf.JsonEach(jsonA)).Select(row => row.Value).ToList().OrderBy(value => value).ToArray();
        output.WriteLine(
            $"rows A: [{string.Join(", ", rowsA)}]; rows B: [{string.Join(", ", rowsB)}]; "
            + $"rows A after B: [{string.Join(", ", rowsAAgain)}]; "
            + $"AnyCache A={((IQueryCache)Context).AnyCommand!.Value.Cache}; AnyCache B={((IQueryCache)otherContext).AnyCommand!.Value.Cache}");

        rowsA.Should().Equal(new long[] { 1, 2 }, "the first context materialises its own document");
        rowsB.Should().Equal(new long[] { 7, 8, 9 }, "the second context materialises its own document");
        rowsAAgain.Should().Equal(new long[] { 1, 2 }, "executing the second context does not change the first context's rows");

        ((IQueryCache)Context).AnyCommand!.Value.Cache.Should().BeTrue(
            "TVF preparation must not set the sticky QueryCommand.Cache=false flag on the shared Any command");
        ((IQueryCache)otherContext).AnyCommand!.Value.Cache.Should().BeTrue(
            "TVF preparation must not set the sticky QueryCommand.Cache=false flag on the shared Any command");
    }

    /// <summary>
    /// AC06 (mapped-row materialisation): a TVF row with a nullable column over a JSON array that contains a
    /// <c>null</c> element materialises that element as <see langword="null" /> — not a default value and
    /// not an exception — while the present elements keep their values.
    /// </summary>
    [Fact]
    public void NullableRow_ShouldMaterialiseTheNullElementAndThePresentValues()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        var command = Context.From(() => T320SqIntegrationTvf.JsonEachNullable("[1,null,3]").OrderBy(row => row.Key))
            .Select(row => new { row.Key, row.Value });

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"nullable row sql: <<{sql}>>");

        var rows = command.ToList();
        var observed = string.Join(
            "; ",
            rows.Select(row => $"key={row.Key} value={(row.Value is null ? "null" : row.Value.ToString())}"));
        output.WriteLine($"nullable row rows: [{observed}]");

        rows.Should().HaveCount(3, $"the array has three elements; [{observed}]");
        rows.Select(row => row.Key).Should().Equal(new long[] { 0, 1, 2 }, $"the row keys are the element indexes; [{observed}]");
        rows[0].Value.Should().Be(1L, $"the first element is present; [{observed}]");
        rows[1].Value.Should().BeNull($"a JSON null element materialses as null, never as a default; [{observed}]");
        rows[2].Value.Should().Be(3L, $"the last element is present; [{observed}]");
    }

    /// <summary>
    /// AC06 (mapped row metadata + converter): the row property carries a real <see cref="ValueConverterAttribute" />
    /// registration (<see cref="EnumToStringConverter{TEnum}" />), the metadata reports it with the SQLite
    /// provider type, and a live execution of the TVF returns the converted CLR values.
    /// </summary>
    [Fact]
    public void ValueConverter_OnTheTvfRowProperty_ShouldRoundTripTheLiveValues()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        var metadata = new EntityMetadataBuilder<IT320SqJsonEachWordRow>().Build();
        var property = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(IT320SqJsonEachWordRow.Word));
        property.Converter.Should().BeOfType<EnumToStringConverter<T320SqWord>>(
            "the row property is mapped through the repo's attributed converter facility");
        property.Converter!.ProviderType.Should().Be(typeof(string), "the stored SQLite type is the converted one");

        var json = "[\"Alpha\",\"Beta\"]";
        var command = Context.From(() => T320SqIntegrationTvf.JsonEachWord(json).OrderBy(row => row.Key))
            .Select(row => row.Word);

        var prepared = Prepare(Context, command, storeInCache: false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        output.WriteLine(
            $"converter metadata: converter={property.Converter.GetType().Name}; providerType={property.Converter.ProviderType.Name}; "
            + $"sql=<<{sql}>>; parameters: [{string.Join(", ", ParametersOf(prepared))}]");

        var rows = command.ToList();
        output.WriteLine($"converter rows: [{string.Join(", ", rows)}]");

        rows.Should().Equal(
            new[] { T320SqWord.Alpha, T320SqWord.Beta },
            "the TEXT values are converted back to their CLR model values by the mapped converter");
    }

    /// <summary>
    /// AC06 (options on a live source): <c>FromOptions</c> on the TVF <c>From</c> overload is applied to a
    /// real execution — a derived-table hint is neutral (same statement, same rows, no hint text) while a
    /// source-level row lock fails closed by name.
    /// </summary>
    [Fact]
    public void FromOptions_OnATvfSource_ShouldBeNeutralOrFailClosedWithLiveRows()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        var json = "[2,4]";

        var plain = Context.From(() => T320SqIntegrationTvf.JsonEach(json)).Select(row => row.Value);
        var plainPrepared = Prepare(Context, plain, storeInCache: true);
        var plainSql = Normalize(plainPrepared.DbCommand.CommandText);
        var plainRows = plain.ToList().OrderBy(value => value).ToArray();

        var hinted = Context.From(
                () => T320SqIntegrationTvf.JsonEach(json),
                options => options.WithSubQueryHint("NestLoop(t1)"))
            .Select(row => row.Value);
        var hintedPrepared = Prepare(Context, hinted, storeInCache: true);
        var hintedSql = Normalize(hintedPrepared.DbCommand.CommandText);
        var hintedRows = hinted.ToList().OrderBy(value => value).ToArray();

        output.WriteLine(
            $"plain: sql=<<{plainSql}>>; rows=[{string.Join(", ", plainRows)}]; "
            + $"hinted: sql=<<{hintedSql}>>; rows=[{string.Join(", ", hintedRows)}]");

        hintedSql.Should().Be(plainSql, "a derived-table hint is neutral on a function source, so the statement is unchanged");
        hintedSql.Should().NotContain("NestLoop", "the hint must not leak into the statement text");
        hintedRows.Should().Equal(plainRows, "the neutral option changes no row");
        plainRows.Should().Equal(new long[] { 2, 4 }, "the live source still returns its own rows");
        ReferenceEquals(hintedPrepared, plainPrepared).Should().BeTrue(
            "the neutral option reuses the cached plan instance instead of forking the identity");

        Action locked = () => Context.From(
                () => T320SqIntegrationTvf.JsonEach(json),
                options => options.ForUpdate())
            .Select(row => row.Value)
            .ToList();

        locked.Should().Throw<NotSupportedException>()
            .WithMessage("*can only target a physical table source*");
    }
}
