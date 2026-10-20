using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// QueryFilterValidator write-path coverage (cycle 8, unit D1) executed against a live file-based SQLite
/// database. Every fact declares a global query filter through the public fluent API and then drives a
/// real write terminal (<c>INSERT</c> of an entity, of column values, of an async bulk stream and of a
/// query source, plus the SQLite <c>MERGE</c> rejection), asserting either the written row read back with
/// <c>IgnoreFilters</c> or the exact <see cref="QueryFilterException"/> the validator emits, together with
/// the unchanged database state measured by a raw <c>count(*)</c>.
///
/// <para>
/// Targeted arms of <c>src/nextorm.core/DataContext/Meta/QueryFilterValidator.cs</c>:
/// <c>CreateEntityCheck</c> (:42-56, async bulk stream), <c>ValidateSourceAsync</c> (:161-172),
/// <c>BuildEntityPredicates</c> (:174-202), <c>BuildColumnPredicates</c> (:204-251, omitted and
/// unsupported rewrites), <c>CanonicalPropertyKey</c>/<c>ResolveOnConcreteType</c> (:258-291),
/// <c>BuildSourceViolation</c> (:318-385, keyed and generic messages), <c>Omitted</c>/<c>Unsupported</c>/
/// <c>UnsupportedFunc</c>/<c>NullValue</c>/<c>FilterKeyText</c>/<c>RequireValue</c> (:414-464),
/// <c>ParameterReferenceFinder</c> (:472-490), <c>ColumnSubstitutionVisitor</c> (:501-538) and
/// <c>ResultMemberRewriteVisitor</c> (:545-582). Entity mapping is declared here only; no shared file is
/// touched and no skip is declared.
/// </para>
///
/// <para>
/// Recorded, never asserted: an interface member read through a cast (<c>((ICf8Tenant)e).TenantId</c>) is
/// not substituted by <c>ColumnSubstitutionVisitor</c> because the member is read off a conversion, not
/// off the entity parameter, so the column-valued write is rejected fail-closed as "cannot validate"
/// (the live entity-row write of the same filter does validate). The <c>FilterKeyText(null)</c> arm
/// ("an active filter") is reached by the two-filter source pre-check.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class Cycle8FilterValidationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string TenantKey = "cycle8_query_filter_tenant";

    private const string Ddl = """
        drop table if exists cf8_target;
        drop table if exists cf8_item;
        create table cf8_item
        (
            id integer primary key,
            tenant_id int not null,
            name text not null,
            score int not null,
            note int null
        );
        create table cf8_target
        (
            id integer primary key,
            tenant_id int not null,
            name text not null
        );
        """;

    private static int _filtersRegistered;

    public Cycle8FilterValidationTests()
    {
        ExecuteRaw(Ddl);

        // The first registration per entity type wins process-wide, so register once for the class and
        // BEFORE any builder resolves the type (a builder would otherwise auto-publish an unfiltered
        // mapping).
        if (Interlocked.Exchange(ref _filtersRegistered, 1) == 0)
        {
            Context.From<Cf8Item>(b => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));
            Context.From<Cf8AnonItem>(b => b.HasQueryFilter(e => e.Name != "blocked"));
            Context.From<Cf8InheritedItem>(b => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));
            Context.From<Cf8InterfaceItem>(b => b.HasQueryFilter("tenant", (e, c) => ((ICf8Tenant)e).TenantId == (int)c.Properties[TenantKey]));
            Context.From<Cf8MethodItem>(b => b.HasQueryFilter("tenant", (e, c) => e.IsAllowed() && e.TenantId > 0));
            Context.From<Cf8FuncItem>(b => b.HasQueryFilter((e, c) => e.Where(x => x.TenantId == (int)c.Properties[TenantKey])));
            Context.From<Cf8ScoreItem>(b => b.HasQueryFilter("score", (e, c) => e.Score > 0));
            Context.From<Cf8NullableItem>(b => b.HasQueryFilter("note", (e, c) => e.Note > 0));
            Context.From<Cf8Target>(b => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));
            Context.From<Cf8TwoFilterTarget>(b => b
                .HasQueryFilter(e => e.Name != "blocked")
                .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));
        }

        Context.Properties[TenantKey] = 1;
    }

    private IDataContext Context => _sut.DataProvider;

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>The live row count of one id in a class-owned table, independent of any filter.</summary>
    private int RawCount(string table, int id)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = $"select count(*) from {table} where id = {id}";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static async IAsyncEnumerable<Cf8Item> Stream(params Cf8Item[] rows)
    {
        foreach (var row in rows)
        {
            await Task.Yield();
            yield return row;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 1. Entity validation: an accepted row is written, a violating row is rejected (keyed message)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// <c>BuildEntityPredicates</c> compiles the keyed tenant filter and the entity terminal validates the
    /// materialised row: the allowed row is written and read back, the foreign row throws with the keyed
    /// diagnostic and never reaches the table.
    /// </summary>
    [Fact]
    public void Insert_Entity_AllowedAndForeignRows_ShouldValidateTheMaterialisedRow()
    {
        Context.CreateInsertBuilder<Cf8Item>()
            .Values(new Cf8Item { Id = 8101, TenantId = 1, Name = "ent-ok", Score = 5 })
            .Insert()
            .Should().Be(1);

        Context.From<Cf8Item>().IgnoreFilters().Where(x => x.Id == 8101).Select(x => x.Name).Single().Should().Be("ent-ok");

        var foreign = new Cf8Item { Id = 8102, TenantId = 2, Name = "ent-foreign", Score = 5 };
        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8Item>().Values(foreign).Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*the 'tenant' filter*");

        RawCount("cf8_item", 8102).Should().Be(0, "the violating entity row is never written");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Entity validation through the anonymous filter key text
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// An anonymous filter reports "the anonymous filter" in the violation message; the allowed row of the
    /// same shape is written.
    /// </summary>
    [Fact]
    public void Insert_Entity_AnonymousFilter_ShouldNameTheAnonymousKeyAndWriteTheAllowedRow()
    {
        Context.CreateInsertBuilder<Cf8AnonItem>()
            .Values(new Cf8AnonItem { Id = 8111, TenantId = 1, Name = "anon-ok", Score = 1 })
            .Insert()
            .Should().Be(1);

        var blocked = new Cf8AnonItem { Id = 8112, TenantId = 1, Name = "blocked", Score = 1 };
        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8AnonItem>().Values(blocked).Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*the anonymous filter*");

        RawCount("cf8_item", 8112).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Entity validation through an inherited (base-declared) member
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The filter reads <c>TenantId</c> declared on an unmapped base type, so the write validator
    /// resolves it on the concrete entity type (the mapped <c>tenant_id</c> column): the allowed row is
    /// written and the foreign row is rejected by value, not by an "omitted column" error.
    /// </summary>
    [Fact]
    public void Insert_Entity_InheritedMemberFilter_ShouldResolveTheMappedColumn()
    {
        Context.CreateInsertBuilder<Cf8InheritedItem>()
            .Values(new Cf8InheritedItem { Id = 8121, TenantId = 1, Name = "base-ok", Score = 1 })
            .Insert()
            .Should().Be(1);

        Context.From<Cf8InheritedItem>().IgnoreFilters().Where(x => x.Id == 8121).Select(x => x.Name).Single().Should().Be("base-ok");

        var foreign = new Cf8InheritedItem { Id = 8122, TenantId = 2, Name = "base-foreign", Score = 1 };
        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8InheritedItem>().Values(foreign).Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*the 'tenant' filter*");

        RawCount("cf8_item", 8122).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Interface-cast filter: entity rows validate, column rows fail closed (observed)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A filter that reads its member through an interface cast validates the materialised entity row (the
    /// allowed row is written, the foreign row is rejected by value), while the column-valued write is
    /// rejected fail-closed as "cannot validate": the cast is not an entity-parameter read, so the
    /// substitution cannot express it. Both shapes execute live against the database.
    /// </summary>
    [Fact]
    public void Insert_InterfaceCastFilter_ShouldValidateEntitiesAndFailClosedOnColumnValues()
    {
        Context.CreateInsertBuilder<Cf8InterfaceItem>()
            .Values(new Cf8InterfaceItem { Id = 8131, TenantId = 1, Name = "iface-ok", Score = 1 })
            .Insert()
            .Should().Be(1);

        var foreign = new Cf8InterfaceItem { Id = 8132, TenantId = 2, Name = "iface-foreign", Score = 1 };
        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8InterfaceItem>().Values(foreign).Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*the 'tenant' filter*");

        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8InterfaceItem>()
                .Value(x => x.Id, 8133)
                .Value(x => x.TenantId, 1)
                .Value(x => x.Name, "iface-column")
                .Value(x => x.Score, 1)
                .Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*cannot validate*");

        RawCount("cf8_item", 8132).Should().Be(0);
        RawCount("cf8_item", 8133).Should().Be(0, "the fail-closed column write is never executed");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Column validation: accepted, violating and omitted filter column
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The <c>Value</c> column form compiles the filter over the written values: the allowed row is
    /// written and read back, a foreign tenant is a value violation, and a statement that omits the
    /// tenant column is rejected with the "does not write" diagnostic (fail-closed).
    /// </summary>
    [Fact]
    public void Insert_Value_ColumnForm_ShouldWriteAllowedRejectForeignAndRejectOmitted()
    {
        Context.CreateInsertBuilder<Cf8Item>()
            .Value(x => x.Id, 8201)
            .Value(x => x.TenantId, 1)
            .Value(x => x.Name, "col-ok")
            .Value(x => x.Score, 3)
            .Insert()
            .Should().Be(1);

        Context.From<Cf8Item>().IgnoreFilters().Where(x => x.Id == 8201).Select(x => x.Name).Single().Should().Be("col-ok");

        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8Item>()
                .Value(x => x.Id, 8202)
                .Value(x => x.TenantId, 2)
                .Value(x => x.Name, "col-foreign")
                .Value(x => x.Score, 3)
                .Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*the 'tenant' filter*");

        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8Item>()
                .Value(x => x.Id, 8203)
                .Value(x => x.Name, "col-omitted")
                .Value(x => x.Score, 3)
                .Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*does not write the 'TenantId' column*");

        RawCount("cf8_item", 8202).Should().Be(0);
        RawCount("cf8_item", 8203).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Column validation over a multi-row column sequence reports the failing row index
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A multi-row <c>Values(column, values)</c> statement is validated per row: the foreign row at index
    /// 1 is reported in the message and the statement is not executed at all.
    /// </summary>
    [Fact]
    public void Insert_Value_MultiRowSequence_ShouldReportTheViolatingRowIndex()
    {
        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8Item>()
                .Values(x => x.Id, new[] { 8301, 8302 })
                .Values(x => x.TenantId, new[] { 1, 2 })
                .Values(x => x.Name, new[] { "multi-a", "multi-b" })
                .Values(x => x.Score, new[] { 1, 1 })
                .Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*(row 1)*");

        RawCount("cf8_item", 8301).Should().Be(0);
        RawCount("cf8_item", 8302).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 7. Column validation treats a DEFAULT filter column as unwritten
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Writing <c>SqlDefault.Value</c> into the filter column makes the column carry no client value, so
    /// the snapshot omits it and the write is rejected with the same "does not write" diagnostic as a
    /// missing column.
    /// </summary>
    [Fact]
    public void Insert_Value_DefaultFilterColumn_ShouldBeTreatedAsOmitted()
    {
        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8Item>()
                .Value(x => x.Id, 8401)
                .Value(x => x.TenantId, SqlDefault.Value)
                .Value(x => x.Name, "col-default")
                .Value(x => x.Score, 1)
                .Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*does not write the 'TenantId' column*");

        RawCount("cf8_item", 8401).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Column validation: a written NULL for a non-nullable filter column
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A boxed <see langword="null"/> written into the non-nullable <c>Score</c> column is reached through
    /// the explicitly typed <c>Value&lt;object&gt;</c> overload: the substitution routes the read through
    /// <c>RequireValue</c>, which reports the null-value diagnostic instead of a
    /// <see cref="NullReferenceException"/>.
    /// </summary>
    [Fact]
    public void Insert_Value_NullIntoNonNullableFilterColumn_ShouldThrowNullValue()
    {
        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8ScoreItem>()
                .Value<object>(x => x.Score, (object)null!)
                .Value(x => x.Id, 8501)
                .Value(x => x.TenantId, 1)
                .Value(x => x.Name, "score-null")
                .Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*the 'score' filter*")
            .WithMessage("*written value for the 'Score' column is null*");

        RawCount("cf8_item", 8501).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 9. Column validation: a written NULL for a nullable filter column is a value violation
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The nullable <c>Note</c> column keeps the plain conversion, so a written NULL is compared as a SQL
    /// NULL and the row is rejected as a filter violation (not as an invalid null value).
    /// </summary>
    [Fact]
    public void Insert_Value_NullIntoNullableFilterColumn_ShouldBeAViolation()
    {
        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8NullableItem>()
                .Value(x => x.Id, 8511)
                .Value(x => x.TenantId, 1)
                .Value(x => x.Name, "note-null")
                .Value(x => x.Score, 1)
                .Value(x => x.Note, (int?)null)
                .Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*violates the 'note' filter*");

        RawCount("cf8_item", 8511).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 10. Column validation: an instance-member filter is rejected as unsupported
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A filter whose body calls an instance method leaves the entity parameter unbound after the
    /// substitution, so <c>ReferencesParameter</c>/<c>ParameterReferenceFinder</c> rejects the write as
    /// "cannot validate" instead of letting a compiled-expression exception escape.
    /// </summary>
    [Fact]
    public void Insert_Value_InstanceMemberFilter_ShouldThrowCannotValidate()
    {
        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8MethodItem>()
                .Value(x => x.Id, 8601)
                .Value(x => x.TenantId, 1)
                .Value(x => x.Name, "method-filter")
                .Value(x => x.Score, 1)
                .Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*cannot validate*instance member*");

        RawCount("cf8_item", 8601).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 11. Column validation: a builder-function filter is rejected fail-closed
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// A <c>FilterFunc</c> (builder-function) filter is evaluated only at plan build, so the write
    /// validator rejects the column-valued insert with the builder-function diagnostic.
    /// </summary>
    [Fact]
    public void Insert_Value_BuilderFunctionFilter_ShouldThrowUnsupportedFunc()
    {
        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Cf8FuncItem>()
                .Value(x => x.Id, 8611)
                .Value(x => x.TenantId, 1)
                .Value(x => x.Name, "func-filter")
                .Value(x => x.Score, 1)
                .Insert())
            .Should().Throw<QueryFilterException>()
            .WithMessage("*builder-function form*FilterFunc*");

        RawCount("cf8_item", 8611).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 12. Async bulk stream: every allowed row is written
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The streaming async bulk path installs <c>CreateEntityCheck</c>, so both allowed rows of the
    /// stream pass the per-row check and are written by the live bulk terminal.
    /// </summary>
    [Fact]
    public async Task BulkInsert_AsyncStream_ShouldWriteEveryAllowedRow()
    {
        var written = await Context.CreateBulkInsertBuilder<Cf8Item>()
            .Values(Stream(
                new Cf8Item { Id = 8701, TenantId = 1, Name = "bulk-a", Score = 1 },
                new Cf8Item { Id = 8702, TenantId = 1, Name = "bulk-b", Score = 2 }))
            .BulkInsertAsync(TestContext.Current.CancellationToken);

        written.Should().Be(2);
        Context.From<Cf8Item>().IgnoreFilters().Where(x => x.Id >= 8701 && x.Id <= 8702).OrderBy(x => x.Id)
            .Select(x => x.Name).ToList().Should().Equal("bulk-a", "bulk-b");
    }

    // ---------------------------------------------------------------------------------------------
    // 13. Async bulk stream: the foreign row is rejected by the streaming per-row check
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The async bulk stream validates each row before it is handed to the provider, so the foreign row
    /// throws the keyed diagnostic. The write is not atomic, so only the violating row is asserted absent.
    /// </summary>
    [Fact]
    public async Task BulkInsert_AsyncStream_ShouldRejectTheForeignRow()
    {
        var act = async () => await Context.CreateBulkInsertBuilder<Cf8Item>()
            .Values(Stream(
                new Cf8Item { Id = 8711, TenantId = 1, Name = "bulk-stream-ok", Score = 1 },
                new Cf8Item { Id = 8712, TenantId = 2, Name = "bulk-stream-foreign", Score = 1 }))
            .BulkInsertAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<QueryFilterException>().WithMessage("*the 'tenant' filter*");

        RawCount("cf8_item", 8712).Should().Be(0, "the violating row is never handed to the provider");
    }

    // ---------------------------------------------------------------------------------------------
    // 14. INSERT ... SELECT: a violating source is rejected by the keyed pre-check
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The query-source pre-check re-roots the single active filter onto the projection and reports the
    /// filter key; the target stays empty.
    /// </summary>
    [Fact]
    public void InsertFromQuery_ViolatingSource_ShouldThrowKeyedMessage()
    {
        Context.CreateInsertBuilder<Cf8Source>()
            .IgnoreFilters()
            .Values(new Cf8Source { Id = 8801, TenantId = 2, Name = "src-foreign", Score = 1 })
            .Insert()
            .Should().Be(1);

        var act = () => Context.CreateInsertBuilder<Cf8Target>()
            .Values(
                Context.From<Cf8Source>().IgnoreFilters().Where(x => x.Name == "src-foreign"),
                x => new Cf8Target { Id = x.Id, TenantId = x.TenantId, Name = x.Name })
            .Insert();

        act.Should().Throw<QueryFilterException>().WithMessage("*the 'tenant' filter*");
        RawCount("cf8_target", 8801).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 15. INSERT ... SELECT (async): two active filters produce the generic key text
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The asynchronous source pre-check ANDs two active filters, so the violation is not attributable to
    /// one key and the message reads "an active filter" (<c>FilterKeyText(null)</c>).
    /// </summary>
    [Fact]
    public async Task InsertFromQueryAsync_TwoActiveFilters_ShouldThrowTheGenericKeyText()
    {
        Context.CreateInsertBuilder<Cf8Source>()
            .IgnoreFilters()
            .Values(new Cf8Source { Id = 8811, TenantId = 2, Name = "src-two", Score = 1 })
            .Insert()
            .Should().Be(1);

        var act = async () => await Context.CreateInsertBuilder<Cf8TwoFilterTarget>()
            .Values(
                Context.From<Cf8Source>().IgnoreFilters().Where(x => x.Name == "src-two"),
                x => new Cf8TwoFilterTarget { Id = x.Id, TenantId = x.TenantId, Name = x.Name })
            .InsertAsync();

        await act.Should().ThrowAsync<QueryFilterException>().WithMessage("*violates an active filter*");
        RawCount("cf8_target", 8811).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 16. INSERT ... SELECT: a projection that omits the filter column fails closed
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The mapping omits <c>TenantId</c>, so <c>ResultMemberRewriteVisitor</c> marks the member missing and
    /// the write is rejected as "does not write the 'TenantId' column" before the statement is rendered.
    /// </summary>
    [Fact]
    public void InsertFromQuery_OmittedFilterColumn_ShouldThrowDoesNotWrite()
    {
        Context.CreateInsertBuilder<Cf8Source>()
            .IgnoreFilters()
            .Values(new Cf8Source { Id = 8821, TenantId = 1, Name = "src-omitted", Score = 1 })
            .Insert()
            .Should().Be(1);

        var act = () => Context.CreateInsertBuilder<Cf8Target>()
            .Values(
                Context.From<Cf8Source>().IgnoreFilters().Where(x => x.Name == "src-omitted"),
                x => new Cf8Target { Id = x.Id, Name = x.Name })
            .Insert();

        act.Should().Throw<QueryFilterException>().WithMessage("*does not write the 'TenantId' column*");
        RawCount("cf8_target", 8821).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // 17. MERGE: an active target filter cannot be isolated on SQLite
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// SQLite has no full-<c>MERGE</c> branch form, so a merge with an active target filter is rejected
    /// atomically before any connection or command: <see cref="NotSupportedException"/> with the
    /// write-target diagnostic, and no row is written.
    /// </summary>
    [Fact]
    public void Merge_WithActiveFilter_ShouldBeRejectedBeforeTheWrite()
    {
        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Cf8Target>()
                .Using(new Cf8Target { Id = 8901, TenantId = 1, Name = "merge-row" })
                .OnKeys()
                .WhenNotMatchedInsert()
                .Merge())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*cannot isolate the write target*");

        RawCount("cf8_target", 8901).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures: class-owned entity shapes over the class-owned tables
    // ---------------------------------------------------------------------------------------------

    /// <summary>Filtered item with the keyed, context-aware tenant filter (the main fixture).</summary>
    [SqlTable("cf8_item")]
    public sealed class Cf8Item
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }

        [Column("note")]
        public int? Note { get; set; }
    }

    /// <summary>Item with an anonymous filter: exercises the anonymous key text.</summary>
    [SqlTable("cf8_item")]
    public sealed class Cf8AnonItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }
    }

    /// <summary>Unmapped base type: <c>TenantId</c> is declared outside the mapped entity type.</summary>
    public abstract class Cf8ItemBase
    {
        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    /// <summary>Item whose tenant filter reads a member inherited from <see cref="Cf8ItemBase"/>.</summary>
    [SqlTable("cf8_item")]
    public sealed class Cf8InheritedItem : Cf8ItemBase
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }
    }

    /// <summary>The interface whose member the filter reads through a cast.</summary>
    public interface ICf8Tenant
    {
        int TenantId { get; }
    }

    /// <summary>Item whose filter reads the tenant member through an interface cast.</summary>
    [SqlTable("cf8_item")]
    public sealed class Cf8InterfaceItem : ICf8Tenant
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }
    }

    /// <summary>Item whose filter calls an instance method (unsupported for column validation).</summary>
    [SqlTable("cf8_item")]
    public sealed class Cf8MethodItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }

        /// <summary>Instance call that keeps the entity parameter referenced after the rewrite.</summary>
        public bool IsAllowed() => TenantId > 0;
    }

    /// <summary>Item whose filter is declared in the builder-function (<c>FilterFunc</c>) form.</summary>
    [SqlTable("cf8_item")]
    public sealed class Cf8FuncItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }
    }

    /// <summary>Item whose filter reads the non-nullable <c>Score</c> column.</summary>
    [SqlTable("cf8_item")]
    public sealed class Cf8ScoreItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }
    }

    /// <summary>Item whose filter reads the nullable <c>Note</c> column.</summary>
    [SqlTable("cf8_item")]
    public sealed class Cf8NullableItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }

        [Column("note")]
        public int? Note { get; set; }
    }

    /// <summary>Unfiltered source entity of the <c>INSERT ... SELECT</c> facts.</summary>
    [SqlTable("cf8_item")]
    public sealed class Cf8Source
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }
    }

    /// <summary>The filtered <c>INSERT ... SELECT</c> target with one active keyed filter.</summary>
    [SqlTable("cf8_target")]
    public sealed class Cf8Target
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>The filtered target with two active filters (anonymous plus keyed).</summary>
    [SqlTable("cf8_target")]
    public sealed class Cf8TwoFilterTarget
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }
}
