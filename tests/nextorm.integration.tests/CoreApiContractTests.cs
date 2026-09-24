using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

public sealed class CoreApiContractTests
{
    [Fact]
    public void TypeExtensions_ShouldClassifyTypes()
    {
        var anonymous = new { A = 1 };

        anonymous.GetType().IsAnonymous().Should().BeTrue();
        typeof(CoreApiContractTests).IsAnonymous().Should().BeFalse();

        var captured = 1;
        Action closure = () => _ = captured;
        closure.Target!.GetType().IsClosure().Should().BeTrue();
        typeof(CoreApiContractTests).IsClosure().Should().BeFalse();

        typeof(Tuple<int>).IsTuple().Should().BeTrue();
        typeof((int, int)).IsTuple().Should().BeTrue();
        typeof(int).IsTuple().Should().BeFalse();
        typeof(int).TryGetProjectionDimension(out var none).Should().BeFalse();
        none.Should().Be(0);

        typeof(Projection<int, int>).TryGetProjectionDimension(out var dim).Should().BeTrue();
        dim.Should().Be(2);

        typeof(int).Similar(typeof(int?)).Should().BeTrue();
        typeof(int?).Similar(typeof(int)).Should().BeTrue();
        typeof(long).Similar(typeof(int)).Should().BeFalse();
        typeof(int).Similar(null!).Should().BeFalse();

        typeof(int).IsScalar().Should().BeTrue();
        typeof(string).IsScalar().Should().BeTrue();
        typeof(object).IsScalar().Should().BeFalse();
    }

    [Fact]
    public void SnakeCaseNamingConvention_ShouldConvertNames()
    {
        var convention = SnakeCaseNamingConvention.Instance;

        convention.TableName("SimpleEntity", false).Should().Be("simple_entity");
        convention.TableName("IProduct", true).Should().Be("product");
        convention.TableName("Idle", true).Should().Be("idle");
        convention.ColumnName("FirstName").Should().Be("first_name");
        convention.ColumnName("OrderID").Should().Be("order_id");
        convention.ColumnName("HTTPServer").Should().Be("http_server");
        convention.ColumnName("utf8Value").Should().Be("utf8_value");
        convention.ColumnName(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void DataContextBuilder_ShouldExposeConfiguration()
    {
        var builder = new DataContextBuilder()
            .UseLoggerFactory(NullLoggerFactory.Instance)
            .LogSensitiveData(true)
            .UseQuotedIdentifiers()
            .UseNamingConvention(SnakeCaseNamingConvention.Instance)
            .UseUppercaseKeywords();

        builder.ShouldLogSensitiveData.Should().BeTrue();
        builder.QuoteIdentifiers.Should().BeTrue();
        builder.NamingConvention.Should().BeSameAs(SnakeCaseNamingConvention.Instance);
        builder.KeywordCase.Should().Be(KeywordCase.Upper);

        builder.UseKeywordCase(KeywordCase.Lower).KeywordCase.Should().Be(KeywordCase.Lower);
        builder.UseUppercaseKeywords(false).KeywordCase.Should().Be(KeywordCase.Lower);
        builder.UseQuotedIdentifiers(false).QuoteIdentifiers.Should().BeFalse();
        builder.UseNamingConvention(null).NamingConvention.Should().BeNull();

        var act = () => builder.CreateDataContext();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ProviderOptions_ShouldConfigureFactory()
    {
        new DataContextBuilder().UsePostgres("Host=localhost").Factory.Should().NotBeNull();
        new DataContextBuilder().UsePostgres(new Npgsql.NpgsqlConnection()).Factory.Should().NotBeNull();
        new DataContextBuilder().UseSqlServer("Server=localhost").Factory.Should().NotBeNull();
        new DataContextBuilder().UseSqlServer(new Microsoft.Data.SqlClient.SqlConnection()).Factory.Should().NotBeNull();

        var path = Path.Combine(Path.GetTempPath(), $"nextorm-{Guid.NewGuid():N}.db");
        File.WriteAllBytes(path, []);
        try
        {
            new DataContextBuilder().UseSqlite(path).Factory.Should().NotBeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ServiceCollection_ShouldRegisterScopedContext()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");

        var services = new ServiceCollection();
        services.AddNextOrmContext(b => b.UseSqlite(connection));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        context.Should().BeSameAs(scope.ServiceProvider.GetRequiredService<IDataContext>());
    }

    [Fact]
    public void ServiceCollection_ShouldRegisterKeyedContext()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");

        var services = new ServiceCollection();
        services.AddKeyedNextOrmContext(b => b.UseSqlite(connection), "primary");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("primary");
        context.Should().NotBeNull();
    }

    [Fact]
    public void ServiceCollection_ShouldRejectNullArguments()
    {
        IServiceCollection services = new ServiceCollection();

        var nullServices = () => ((IServiceCollection)null!).AddNextOrmContext(b => { });
        nullServices.Should().Throw<ArgumentNullException>();

        var nullBuilder = () => services.AddNextOrmContext((Action<DataContextBuilder>)null!);
        nullBuilder.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DomainExceptions_ShouldCarryMessageAndInner()
    {
        var inner = new InvalidOperationException("inner");

        new DataContextException().Should().BeOfType<DataContextException>();
        new DataContextException("message").Message.Should().Be("message");
        new DataContextException("message", inner).InnerException.Should().BeSameAs(inner);

        new BuildSqlCommandException().Should().BeAssignableTo<DataContextException>();
        new BuildSqlCommandException("message").Message.Should().Be("message");
        new BuildSqlCommandException("message", inner).InnerException.Should().BeSameAs(inner);

        new QueryPreparationException().Should().BeAssignableTo<DataContextException>();
        new QueryPreparationException("message").Message.Should().Be("message");
        new QueryPreparationException("message", inner).InnerException.Should().BeSameAs(inner);
    }
}
