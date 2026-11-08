using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 3, batch 1 — the portable key-upsert form of <c>MergeBuilder&lt;TEntity&gt;</c> on live SQLite
/// (<c>src/nextorm.core/Builders/MergeBuilder.cs</c>, <c>MergeBuilder.Columns.cs</c> and
/// <c>DataContext/MergeAssignmentRenderer.cs</c>). The accepted facts execute a real
/// <c>INSERT ... ON CONFLICT ... DO UPDATE</c> and assert the persisted state; every refusal is a pre-SQL
/// guard (<c>AC-STEP-REAL-PROVIDER</c> class b) asserted by exception type plus the semantic identifier
/// the guard carries. The global-filter refusal/binding pair additionally proves that the fail-closed
/// capability guard is decided before the statement and that <c>IgnoreFilters()</c> is the documented
/// escape hatch.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep3MergePortableTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s3_merge_entity (id integer primary key, name text null, age int not null);
        delete from s3_merge_entity;
        create table if not exists s3_merge_filtered (id integer primary key, name text null, is_deleted integer not null default 0);
        delete from s3_merge_filtered;
        """;

    private static int _seed = -880_000_000;

    private static int NextKey() => Interlocked.Add(ref _seed, -1);

    public SqliteStep3MergePortableTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>Reads the class-owned row back, proving the statement reached the database.</summary>
    private (string? Name, int Age) Read(int id)
    {
        var row = Context.From<S3MergeEntity>()
            .Where(x => x.Id == id)
            .Select(x => new { x.Name, x.Age })
            .Single();
        return (row.Name, row.Age);
    }

    // ---------------------------------------------------------------------------------------------
    // Accepted expression forms (target/source/excluded roles)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void KeyUpsert_TargetAndSourceRoles_ShouldApplyTheComputedUpdate()
    {
        var id = NextKey();
        Context.CreateInsertBuilder<S3MergeEntity>()
            .Values(new S3MergeEntity { Id = id, Name = "base", Age = 10 })
            .Insert();

        var affected = Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = id, Name = "source", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Age = target.Age + source.Age })
            .WhenNotMatchedInsert()
            .Merge();

        affected.Should().Be(1);
        var row = Read(id);
        row.Age.Should().Be(15, "the UPDATE reads both the pre-update target and the source row");
        row.Name.Should().Be("base", "only the assigned column is written");
    }

    [Fact]
    public void KeyUpsert_ExcludedRole_ShouldReadTheProposedRow()
    {
        var id = NextKey();
        Context.CreateInsertBuilder<S3MergeEntity>()
            .Values(new S3MergeEntity { Id = id, Name = "base", Age = 10 })
            .Insert();

        Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = id, Name = "source", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate((_, _, excluded) => new { Age = excluded.Age })
            .WhenNotMatchedInsert()
            .Merge();

        var row = Read(id);
        row.Age.Should().Be(5, "the native proposed row carries the incoming source value");
        row.Name.Should().Be("base");
    }

    [Fact]
    public void KeyUpsert_InsertAssignments_ShouldInsertAndUpdateWithTheAssignedColumns()
    {
        var id = NextKey();
        Context.CreateInsertBuilder<S3MergeEntity>()
            .Values(new S3MergeEntity { Id = id, Name = "old", Age = 1 })
            .Insert();

        Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = id, Name = "new", Age = 2 })
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Age = target.Age + source.Age })
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name, Age = source.Age })
            .Merge();

        var updated = Read(id);
        updated.Age.Should().Be(3);
        updated.Name.Should().Be("old");

        var fresh = id - 1;
        Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = fresh, Name = "fresh", Age = 7 })
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Age = target.Age + source.Age })
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name, Age = source.Age })
            .Merge();

        Read(fresh).Should().Be(("fresh", 7), "the not-matched branch writes the assigned columns");
    }

    [Fact]
    public void KeyUpsert_ToSql_ShouldRenderTheNativeUpsertForm()
    {
        var id = NextKey();
        Context.CreateInsertBuilder<S3MergeEntity>()
            .Values(new S3MergeEntity { Id = id, Name = "base", Age = 10 })
            .Insert();

        var sql = Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = id, Name = "source", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Age = target.Age + source.Age })
            .WhenNotMatchedInsert()
            .ToSql();

        sql.Should().ContainEquivalentOf("on conflict", "SQLite renders its native key-upsert form");
        sql.Should().ContainEquivalentOf("do update");

        // The native form is not only rendered: executing it must persist the matched update.
        var affected = Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = id, Name = "source", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Age = target.Age + source.Age })
            .WhenNotMatchedInsert()
            .Merge();

        affected.Should().Be(1);
        var row = Read(id);
        row.Age.Should().Be(15, "the native upsert applies the matched update");
        row.Name.Should().Be("base", "only the assigned column is written");
    }

    // ---------------------------------------------------------------------------------------------
    // Guards reachable through the public builder
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void UsingTwice_ShouldBeRefused()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = NextKey(), Name = "a", Age = 1 })
            .Using(new S3MergeEntity { Id = NextKey(), Name = "b", Age = 2 });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Using can only be specified once per merge*");
    }

    [Fact]
    public void UsingEmptyBatch_ShouldBeRefused()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(Array.Empty<S3MergeEntity>());

        action.Should().Throw<ArgumentException>()
            .WithMessage("*At least one entity is required*");
    }

    [Fact]
    public void OnAfterOnKeys_ShouldBeRefused()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = NextKey(), Name = "a", Age = 1 })
            .OnKeys()
            .On((target, source) => target.Id == source.Id);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*On can only be specified once; OnKeys was already called*");
    }

    [Fact]
    public void OnKeysAfterOn_ShouldBeRefused()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = NextKey(), Name = "a", Age = 1 })
            .On((target, source) => target.Id == source.Id)
            .OnKeys();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*OnKeys can only be specified once; On(...) was already called*");
    }

    [Fact]
    public void OnWithoutBranches_ShouldBeRefused()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = NextKey(), Name = "a", Age = 1 })
            .On((target, source) => target.Id == source.Id)
            .Merge();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*On(...) requires at least one branch*");
    }

    [Fact]
    public void KeyInsertAssignmentMissingTheKey_ShouldBeRefusedByName()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = NextKey(), Name = "a", Age = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Name = source.Name })
            .Merge();

        action.Should().Throw<ArgumentException>()
            .WithMessage("*must assign conflict key 'Id' directly from source.Id*");
    }

    [Fact]
    public void KeyInsertAssignmentTransformingTheKey_ShouldBeRefusedByName()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = NextKey(), Name = "a", Age = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = source.Id + 1, Name = source.Name })
            .Merge();

        action.Should().Throw<ArgumentException>()
            .WithMessage("*must assign conflict key 'Id' directly from source.Id*");
    }

    [Fact]
    public void BatchSourceInUpdateAssignments_ShouldBeRefusedAsNonMaterialized()
    {
        var first = NextKey();
        var action = () => Context.CreateMergeBuilder<S3MergeEntity>()
            .Using([
                new S3MergeEntity { Id = first, Name = "a", Age = 1 },
                new S3MergeEntity { Id = first - 1, Name = "b", Age = 2 },
            ])
            .OnKeys()
            .WhenMatchedUpdate((_, source) => new { Age = source.Age })
            .WhenNotMatchedInsert()
            .Merge();

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*requires one materialized source row*");
    }

    [Fact]
    public void IdentityMatchKey_ShouldBeRefusedAsDatabaseGenerated()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeIdentityEntity>()
            .Using(new S3MergeIdentityEntity { Id = 1, Name = "a" })
            .OnKeys();

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*Id*database-generated and cannot be used as an upsert match key*");
    }

    [Fact]
    public void EntityWithoutKey_ShouldBeRefusedByName()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeKeylessEntity>()
            .Using(new S3MergeKeylessEntity { Code = 1, Name = "a" })
            .OnKeys();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*S3MergeKeylessEntity*has no key property*");
    }

    [Fact]
    public void KeyOnlyEntity_ShouldBeRefusedAsHavingNoWritableColumn()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeKeyOnlyEntity>()
            .Using(new S3MergeKeyOnlyEntity { Id = NextKey() })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*has only key columns*");
    }

    [Fact]
    public void FullMergeBranchWithAssignments_ShouldBeRefusedByTheProviderCapability()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = NextKey(), Name = "a", Age = 1 })
            .On((target, source) => target.Id == source.Id)
            .WhenMatched().ThenUpdate((target, source) => new { Age = target.Age + source.Age })
            .WhenNotMatched().ThenInsert()
            .Merge();

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*SqliteDialect*does not support MERGE assignment expressions*");
    }

    [Fact]
    public void FullMergeBranchWithoutAssignments_ShouldBeRefusedByTheProviderCapability()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeEntity>()
            .Using(new S3MergeEntity { Id = NextKey(), Name = "a", Age = 1 })
            .On((target, source) => target.Id == source.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*SqliteDialect*does not support the full MERGE statement*native upsert form*");
    }

    // ---------------------------------------------------------------------------------------------
    // IgnoreFilters overloads and the active-filter capability guard
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void IgnoreFiltersOverloads_OnAnUnfilteredEntity_ShouldExecuteTheUpsert()
    {
        var id = NextKey();

        Context.CreateMergeBuilder<S3MergeEntity>()
            .IgnoreFilters()
            .IgnoreFilters(Array.Empty<Type>())
            .IgnoreFilters(Array.Empty<string>())
            .IgnoreFilters(Array.Empty<string>(), typeof(S3MergeEntity))
            .Using(new S3MergeEntity { Id = id, Name = "ignored", Age = 4 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        Read(id).Should().Be(("ignored", 4));
    }

    [Fact]
    public void ActiveTargetFilter_ShouldRefuseTheKeyUpsertBeforeSql()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeFilteredEntity>()
            .Using(new S3MergeFilteredEntity { Id = NextKey(), Name = "blocked" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot isolate the write target*call IgnoreFilters()*");
    }

    [Fact]
    public async Task ActiveTargetFilter_ShouldRefuseTheAsyncKeyUpsertBeforeSql()
    {
        var act = FluentActions.Awaiting(() => Context.CreateMergeBuilder<S3MergeFilteredEntity>()
            .Using(new S3MergeFilteredEntity { Id = NextKey(), Name = "blocked" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .MergeAsync(TestContext.Current.CancellationToken));

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*cannot isolate the write target*call IgnoreFilters()*");
    }

    [Fact]
    public void IgnoreFilters_ShouldLiftTheActiveFilterRefusalAndPersistTheRow()
    {
        var id = NextKey();

        Context.CreateMergeBuilder<S3MergeFilteredEntity>()
            .IgnoreFilters()
            .Using(new S3MergeFilteredEntity { Id = id, Name = "allowed" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        var row = Context.From<S3MergeFilteredEntity>()
            .IgnoreFilters()
            .Where(x => x.Id == id)
            .Select(x => new { x.Name })
            .Single();
        row.Name.Should().Be("allowed");
    }

    [Fact]
    public async Task IgnoreFilters_ShouldLiftTheRefusalOnTheAsyncTerminal()
    {
        var id = NextKey();

        await Context.CreateMergeBuilder<S3MergeFilteredEntity>()
            .IgnoreFilters()
            .Using(new S3MergeFilteredEntity { Id = id, Name = "allowed-async" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .MergeAsync(TestContext.Current.CancellationToken);

        Context.From<S3MergeFilteredEntity>()
            .IgnoreFilters()
            .Where(x => x.Id == id)
            .Select(x => x.Name)
            .Single()
            .Should().Be("allowed-async");
    }

    [Fact]
    public void IgnoreFiltersByName_ThatDoesNotDisableTheTargetFilter_ShouldStillRefuse()
    {
        var action = () => Context.CreateMergeBuilder<S3MergeFilteredEntity>()
            .IgnoreFilters(new[] { "unrelated-filter" })
            .Using(new S3MergeFilteredEntity { Id = NextKey(), Name = "blocked" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot isolate the write target*call IgnoreFilters()*");
    }

    /// <summary>Marker table of the accepted key-upsert facts.</summary>
    [SqlTable("s3_merge_entity")]
    public sealed class S3MergeEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("age")]
        public int Age { get; set; }
    }

    /// <summary>Entity whose only key column is database-generated.</summary>
    [SqlTable("s3_merge_identity")]
    public sealed class S3MergeIdentityEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    /// <summary>Entity without any key property: <c>OnKeys</c> has nothing to resolve.</summary>
    [SqlTable("s3_merge_keyless")]
    public sealed class S3MergeKeylessEntity
    {
        [Column("code")]
        public int Code { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    /// <summary>Entity with a key column and no writable non-key column.</summary>
    [SqlTable("s3_merge_key_only")]
    public sealed class S3MergeKeyOnlyEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
    }

    /// <summary>Entity carrying an anonymous global filter, so the merge target cannot be isolated.</summary>
    [SqlTable("s3_merge_filtered")]
    [QueryFilter(FilterLambda = nameof(ActiveOnly))]
    public sealed class S3MergeFilteredEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        /// <summary>Anonymous filter over the target entity.</summary>
        public static Expression<Func<S3MergeFilteredEntity, bool>> ActiveOnly() => entity => !entity.IsDeleted;
    }
}
