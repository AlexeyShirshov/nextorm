using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core.Repositories;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

public sealed class BoundedDeleteSqliteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundedDeleteCorrelatesKeysWhenTableNameMatchesTheSelectionAlias(bool quoted)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            create table nextorm_keys (nextorm_key_0 integer not null primary key, tenant integer not null);
            insert into nextorm_keys values (1,1),(2,1),(3,2);
            """;
        command.ExecuteNonQuery();
        var tap = new RecordingInterceptor();
        using var context = new SqliteDataContext(connection,
            new DataContextBuilder().UseQuotedIdentifiers(quoted).AddInterceptor(tap));

        context.From<CollisionRow>().Where(x => x.Tenant == 1).OrderBy(x => x.Key)
            .Limit(1).Delete().Should().Be(1);
        tap.Sql.Should().ContainSingle().Which.Should().StartWith("delete from ").And.NotContain(";");
        context.From<CollisionRow>().OrderBy(x => x.Key).Select(x => x.Key).ToList()
            .Should().Equal(2, 3);
    }

    [Table("nextorm_keys")]
    private sealed class CollisionRow
    {
        [Key, Column("nextorm_key_0")]
        public int Key { get; set; }

        [Column("tenant")]
        public int Tenant { get; set; }
    }

    [Fact]
    public async Task OrderedDeleteSelectsCompositeKeysAndRespectsCap()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table BoundedDeleteRow (Tenant int not null, Id int not null, primary key (Tenant, Id));
            insert into BoundedDeleteRow values (1, 1), (1, 2), (1, 3), (2, 3);
            """;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var commands = new RecordingInterceptor();
        await using var context = new SqliteDataContext(connection,
            new DataContextBuilder().AddInterceptor(commands));

        var rows = context.From<BoundedDeleteRow>().Where(row => row.Tenant == 1);
        var invalid = () => rows.Limit(2).Offset(1).Delete();
        invalid.Should().Throw<NotSupportedException>();
        var component = () => rows.WithComponent(new UnexpectedComponent()).Limit(2).Delete();
        component.Should().Throw<NotSupportedException>();
        commands.Sql.Should().BeEmpty();

        var count = await rows.OrderByDescending(row => row.Id).Limit(2)
            .DeleteAsync(TestContext.Current.CancellationToken);

        count.Should().Be(2);
        commands.Sql.Should().ContainSingle().Which.Should().StartWith("delete from ").And.NotContain(";");
        context.From<BoundedDeleteRow>().OrderBy(row => row.Tenant).ToList()
            .Select(row => (row.Tenant, row.Id)).Should().Equal((1, 1), (2, 3));
    }

    [Fact]
    public void RepositoryDeleteKeepsFiltersAndParticipatesInTheBorrowedTransaction()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            create table FilteredDeleteRow (Id int not null primary key, Hidden int not null);
            insert into FilteredDeleteRow values (1, 1), (2, 0), (3, 0);
            """;
        command.ExecuteNonQuery();
        using var context = new SqliteDataContext(connection, new DataContextBuilder());
        var app = new DeleteContext(context);

        using (var transaction = ((ITransactionManager)context).BeginTransaction())
        {
            app.Rows.Where(row => row.Id > 0).OrderBy(row => row.Id).Limit(1).Delete().Should().Be(1);
            app.Rows.Query().IgnoreFilters().OrderBy(row => row.Id).ToList()
                .Select(row => row.Id).Should().Equal(1, 3);
            app.Rows.Where(row => row.Id > 0).IgnoreFilters().OrderBy(row => row.Id)
                .Limit(1).Delete().Should().Be(1);
            app.Rows.Query().IgnoreFilters().ToList().Select(row => row.Id).Should().Equal(3);
            transaction.Rollback();
        }

        app.Rows.Query().IgnoreFilters().OrderBy(row => row.Id).ToList()
            .Select(row => row.Id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void RepositorySourcesUseMappedTablesAndFilteredBuilderStaysDerived()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            create table BoundedDeleteRow (Tenant int not null, Id int not null, primary key (Tenant, Id));
            insert into BoundedDeleteRow values (1, 1), (1, 2);
            """;
        command.ExecuteNonQuery();

        var commands = new RecordingInterceptor();
        using var context = new SqliteDataContext(connection, new DataContextBuilder().AddInterceptor(commands));
        var repo = new DeleteContext(context).UnfilteredRows;

        context.From(repo).ToList().Should().HaveCount(2);
        context.From(repo).Join(repo, (left, right) => left.Id == right.Id)
            .Select(pair => pair.Item1.Id).ToList().OrderBy(id => id).Should().Equal(1, 2);
        context.From(repo.Where(row => row.Id == 2)).ToList()
            .Select(row => row.Id).Should().Equal(2);

        commands.Sql.Should().HaveCount(3);
        commands.Sql[0].Should().NotContain("from (");
        commands.Sql[1].Should().Contain(" join ").And.NotContain("join (");
        commands.Sql[2].Should().Contain("from (");
    }

    private sealed class DeleteContext(IDataContext provider) : DbContext(provider)
    {
        public IRepository<FilteredDeleteRow> Rows =>
            Repository<FilteredDeleteRow>(builder => builder.HasQueryFilter(row => !row.Hidden));

        public IRepository<BoundedDeleteRow> UnfilteredRows => Repository<BoundedDeleteRow>();
    }

    private sealed class FilteredDeleteRow
    {
        [Key]
        public int Id { get; set; }

        public bool Hidden { get; set; }
    }

    private sealed class RecordingInterceptor : IQueryInterceptor
    {
        public List<string> Sql { get; } = [];

        public void CommandExecuting(CommandEventData eventData, DbCommand command) => Sql.Add(command.CommandText);
    }

    private sealed class UnexpectedComponent : IQueryComponent
    {
        public Type Slot => typeof(UnexpectedComponent);

        public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;

        public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context) =>
            throw new InvalidOperationException("DELETE must reject the component before preparation.");

        public void BuildQuery(IQueryComponentBuildContext context) =>
            throw new InvalidOperationException("DELETE must reject the component before rendering.");
    }

    private sealed class BoundedDeleteRow
    {
        [Key]
        public int Tenant { get; set; }

        [Key]
        public int Id { get; set; }
    }
}
