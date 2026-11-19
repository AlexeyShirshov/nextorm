using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public Task D187_BoundedDeleteKeepsCompositeKeysAndRespectsCap() =>
        RunD187BoundedDelete((DataContext)_sut.DataProvider, TestContext.Current.CancellationToken);

    internal static async Task RunD187BoundedDelete(DataContext context, CancellationToken cancellation)
    {
        context.EnsureConnectionOpen();

        void Execute(string sql)
        {
            using var command = context.CreateCommand(sql);
            command.ExecuteNonQuery();
        }

        Execute("drop table if exists d187_bounded_delete");
        Execute("create table d187_bounded_delete (tenant int not null, id int not null, primary key (tenant, id))");
        try
        {
            Execute("insert into d187_bounded_delete (tenant, id) values (1, 1), (1, 2), (1, 3), (1, 4), (1, 5), (2, 5)");
            var candidates = context.From<D187DeleteRow>().Where(row => row.Tenant == 1);
            var limited = candidates.OrderByDescending(row => row.Id).Limit(2);

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Func<Task> cancelledDelete = () => limited.DeleteAsync(cancelled.Token);
            await cancelledDelete.Should().ThrowAsync<OperationCanceledException>();
            (await candidates.CountAsync(cancellation)).Should().Be(5);

            (await limited.DeleteAsync(cancellation)).Should().Be(2);
            (await candidates.OrderBy(row => row.Id).Select(row => row.Id).ToListAsync(cancellation))
                .Should().Equal(1, 2, 3);

            context.CreateDeleteBuilder<D187DeleteRow>().Where(row => row.Tenant == 1)
                .Limit(2).Delete().Should().Be(2);
            candidates.Count().Should().Be(1);
            context.From<D187DeleteRow>().Where(row => row.Tenant == 2 && row.Id == 5)
                .Count().Should().Be(1);
        }
        finally
        {
            Execute("drop table d187_bounded_delete");
        }
    }

    [Table("d187_bounded_delete")]
    private sealed class D187DeleteRow
    {
        [Key, Column("tenant")]
        public int Tenant { get; set; }

        [Key, Column("id")]
        public int Id { get; set; }
    }
}
