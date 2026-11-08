using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 3, batch 2 — bound-source global filters on a CROSS join, where the join can carry no <c>ON</c>
/// predicate (<c>src/nextorm.core/Query/QueryCommand.QueryPreparer.cs</c>:
/// <c>CollectBoundCrossJoinFilters</c> and <c>AppendPendingCrossJoinFilters</c>, both 0/N at v4). The
/// bound side is an explicitly mapped raw SQL source (<c>FromSql(...).BindEntity</c>) carrying a keyed
/// tenant filter and an anonymous soft-delete filter; the main side carries its own anonymous filter and
/// an explicit predicate. The accepted fact computes the surviving cross product, and the control facts
/// prove that the filters are the binding's and not an unconditional restriction.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep3CrossJoinFilterTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string TenantKey = "s3_xj_tenant";

    private const string Ddl = """
        create table if not exists s3_xj_main (id integer primary key, name text null, is_deleted integer not null default 0);
        delete from s3_xj_main;
        create table if not exists s3_xj_bound (id integer primary key, tenant_id int not null, is_deleted integer not null default 0, name text null);
        delete from s3_xj_bound;
        insert into s3_xj_main (id, name, is_deleted) values (11, 'm11', 0), (12, 'm12', 1), (13, 'm13', 0);
        insert into s3_xj_bound (id, tenant_id, is_deleted, name) values
            (101, 1, 0, 'b101'), (102, 1, 1, 'b102'), (103, 2, 0, 'b103'), (104, 1, 0, 'b104');
        """;

    private const string BoundSql = "select id, tenant_id, is_deleted, name from s3_xj_bound";

    public SqliteStep3CrossJoinFilterTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();

        Context.Properties[TenantKey] = 1;
    }

    private IDataContext Context => _sut.DataProvider;

    private EntityBuilder<S3XjBound> BoundSource()
        => Context.FromSql(BoundSql).BindEntity<S3XjBound>(["id", "tenant_id", "is_deleted", "name"]);

    /// <summary>
    /// Both global filters of the bound side are re-rooted into the statement WHERE, and the main side's
    /// own filter plus explicit predicate still apply.
    /// </summary>
    [Fact]
    public void BoundCrossJoin_BothSideFilters_ShouldFilterTheCrossProduct()
    {
        var rows = Context.From<S3XjMain>()
            .Where(m => m.Id <= 20)
            .CrossJoin(BoundSource())
            .Where(p => p.Item2.Name != null)
            .Select(p => new { MainId = p.Item1.Id, BoundId = p.Item2.Id })
            .ToList();

        // Active main rows 11 and 13 cross the active tenant-1 bound rows 101 and 104.
        rows.Should().HaveCount(4);
        rows.Select(r => r.MainId).Distinct().OrderBy(id => id).Should().Equal(11, 13);
        rows.Select(r => r.BoundId).Distinct().OrderBy(id => id).Should().Equal(101, 104);
    }

    /// <summary>The bound filters restrict only the joined side, not the main source.</summary>
    [Fact]
    public void BoundCrossJoin_ShouldNotFilterTheMainSide()
    {
        var mainIds = Context.From<S3XjMain>()
            .Where(m => m.Id <= 20)
            .CrossJoin(BoundSource())
            .Select(p => p.Item1.Id)
            .ToList();

        mainIds.Distinct().OrderBy(id => id).Should().Equal(11, 13);
    }

    /// <summary>An explicit <c>IgnoreFilters()</c> disables both sides, so the tenant-2 and deleted rows reappear.</summary>
    [Fact]
    public void BoundCrossJoin_IgnoreFilters_ShouldLiftEveryFilter()
    {
        var rows = Context.From<S3XjMain>()
            .IgnoreFilters()
            .Where(m => m.Id == 11)
            .CrossJoin(BoundSource())
            .Where(p => p.Item2.Id >= 101)
            .Select(p => new { MainId = p.Item1.Id, BoundId = p.Item2.Id })
            .ToList();

        rows.Should().HaveCount(4, "every bound row crosses the single unfiltered main row");
        rows.Select(r => r.BoundId).OrderBy(id => id).Should().Equal(101, 102, 103, 104);
    }

    /// <summary>Main-side fixture with an anonymous soft-delete filter.</summary>
    [SqlTable("s3_xj_main")]
    [QueryFilter(FilterLambda = nameof(ActiveOnly))]
    public sealed class S3XjMain
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        public static Expression<Func<S3XjMain, bool>> ActiveOnly() => row => !row.IsDeleted;
    }

    /// <summary>Bound raw-source fixture with a keyed tenant filter and an anonymous soft-delete filter.</summary>
    [SqlTable("s3_xj_bound")]
    [QueryFilter(FilterLambda = nameof(ActiveOnly))]
    [QueryFilter(FilterKey = "tenant", FilterLambda = nameof(Tenant))]
    public sealed class S3XjBound
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public static Expression<Func<S3XjBound, bool>> ActiveOnly() => row => !row.IsDeleted;

        public static Expression<Func<S3XjBound, IDataContext, bool>> Tenant()
            => (row, context) => row.TenantId == (int)context.Properties[TenantKey];
    }
}
