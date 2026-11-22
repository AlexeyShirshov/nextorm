using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Campaign 13 probe: <c>PreWhere</c> carrying an In-values predicate, the shape that feeds
/// <c>QueryPreparer</c>'s PreWhere In-value partitions. The fact asserts the surviving rows.
/// </summary>
[Collection("ClickHouse")]
public sealed class C13PreWhereInValuesClickHouseTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext Db => _sut.DataProvider;

    /// <summary>Captured IN lists are rebound when a prepared PREWHERE query is reused.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreWhereWithInValues_ShouldRebindWhenTheListChanges(bool filterGroups)
    {
        Execute("drop table if exists c13_prewhere");
        Execute("create table c13_prewhere (id Int32, grp Int32) engine = MergeTree order by id");
        Execute("insert into c13_prewhere (id, grp) values (1, 1), (2, 1), (3, 2), (4, 3)");

        try
        {
            int[] wanted = [1, 3];
            int[] groups = [1, 2];

            var source = Db.From<C13PreWhere>().PreWhere(x => wanted.Contains(x.Id));
            if (filterGroups)
                source = source.Where(x => groups.Contains(x.Grp));

            var query = source
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            query.ToList().Should().Equal(1, 3);

            wanted = [2, 3, 4];
            groups = [2, 3];
            if (filterGroups)
                query.ToList().Should().Equal(3, 4);
            else
                query.ToList().Should().Equal(2, 3, 4);

            wanted = [];
            query.ToList().Should().BeEmpty();

            wanted = [1];
            groups = [1];
            query.ToList().Should().Equal(1);
        }
        finally
        {
            Execute("drop table if exists c13_prewhere");
        }
    }

    private void Execute(string sql)
    {
        var context = (DataContext)Db;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>The probe row.</summary>
    [SqlTable("c13_prewhere")]
    public sealed class C13PreWhere
    {
        /// <summary>The primary key.</summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>The optional group.</summary>
        [Column("grp")]
        public int Grp { get; set; }
    }
}
