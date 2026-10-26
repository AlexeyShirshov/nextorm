using System.Data;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;

namespace NextORM.MySql.Tests;

/// <summary>
/// T340 (#340) DB-free policy seam for MariaDB. MariaDB inherits <see cref="MySqlDataContext"/>, so its
/// refusal must be its own value (the plan's "MariaDB" wording) instead of the inherited MySQL text.
/// <para>
/// At r1 (red) the provider policy property does not exist yet: both dialects still reach the generic
/// <c>SelectExpression</c> rejection, so this fact pins only the shared refusal. B3 extends it with the
/// exact per-dialect strings and the "MariaDB string differs from MySQL" assertion.
/// </para>
/// </summary>
public class TimeOnlyMariaDbPolicyTests
{
    private const string PlaceholderConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm";

    [Fact]
    public void MappedTimeOnly_ShouldBeRefusedOnBothDialects()
    {
        using var mySql = new MySqlDataContext(PlaceholderConnectionString, new DataContextBuilder());
        using var mariaDb = new MariaDbDataContext(PlaceholderConnectionString, new DataContextBuilder());

        var mySqlMessage = RefusalOf(mySql);
        var mariaDbMessage = RefusalOf(mariaDb);

        mySqlMessage.Should().Contain("TimeOnly");
        mariaDbMessage.Should().Contain("TimeOnly");
    }

    private static string RefusalOf(DataContext context)
    {
        var column = new SelectExpression(typeof(TimeOnly))
        {
            Index = 0,
            PropertyName = "T",
        };
        var record = Expression.Parameter(typeof(IDataRecord), "record");

        return FluentActions.Invoking(() => context.MapColumnExpression(column, record))
            .Should().Throw<NotSupportedException>()
            .Which.Message;
    }
}
