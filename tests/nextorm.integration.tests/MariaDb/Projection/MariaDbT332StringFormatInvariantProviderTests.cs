using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T332 MariaDB live coverage. MariaDB does not run the shared <see cref="CommonTestSuite"/> matrix, so the
/// same narrow behaviour — the canonical <c>CultureInfo.InvariantCulture</c> static property, the
/// pre-existing constant and null-provider arms, and the exact refusal — is verified here against a real
/// MariaDB server (a Testcontainers instance unless <c>NEXTORM_MARIADB_CONNECTION</c> points elsewhere).
/// </summary>
public sealed class MariaDbT332StringFormatInvariantProviderTests : IDisposable
{
    private const string RejectionMessage =
        "string.Format is only supported with CultureInfo.InvariantCulture; the current/other cultures have no portable SQL form.";

    private readonly IDataContext _ctx;

    public MariaDbT332StringFormatInvariantProviderTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    private EntityBuilder<IT332FormatProbe> Probe => _ctx.From<IT332FormatProbe>();

    [Fact]
    public void T332StringFormat_StaticPropertyInvariantProvider_ShouldExecute()
    {
        var value = Probe
            .Where(x => x.Id == 1)
            .Select(x => string.Format(CultureInfo.InvariantCulture, "{0}-{1}", x.Id, x.S))
            .First();

        value.Should().Be("1-dadfasd");
    }

    [Fact]
    public void T332StringFormat_ConstantInvariantProvider_ShouldExecute()
    {
        var value = Probe
            .Where(x => x.Id == 1)
            .Select(ConstantProviderCase("{0}-{1}"))
            .First();

        value.Should().Be("1-dadfasd");
    }

    [Fact]
    public void T332StringFormat_NullProvider_ShouldKeepBehavingLikeTheProviderlessOverload()
    {
        var value = Probe
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                LiteralNull = string.Format((IFormatProvider?)null, "{0}-{1}", x.Id, x.S),
                NoProvider = string.Format("{0}-{1}", x.Id, x.S)
            })
            .First();

        value.LiteralNull.Should().Be(value.NoProvider);
    }

    [Fact]
    public void T332StringFormat_NonCanonicalProvider_ShouldRejectWithExactMessage()
    {
        var act = () => Probe
            .Where(x => x.Id == 1)
            .Select(x => string.Format(CultureInfo.CurrentCulture, "{0}", x.Id))
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage(RejectionMessage);
    }

    private void Seed()
    {
        Execute("drop table if exists t332_format_probe");
        Execute("create table t332_format_probe (id int not null primary key, s varchar(100))");
        Execute("insert into t332_format_probe (id, s) values (1, 'dadfasd')");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// The programmatic constant shape accepted since before the defect:
    /// <c>Expression.Constant(CultureInfo.InvariantCulture)</c> rather than a static property access.
    /// </summary>
    private static Expression<Func<IT332FormatProbe, string>> ConstantProviderCase(string format)
    {
        var parameter = Expression.Parameter(typeof(IT332FormatProbe), "x");
        var formatMethod = typeof(string).GetMethod(
            nameof(string.Format),
            [typeof(IFormatProvider), typeof(string), typeof(object), typeof(object)])!;
        var body = Expression.Call(
            formatMethod,
            Expression.Constant(CultureInfo.InvariantCulture),
            Expression.Constant(format),
            Expression.Convert(Expression.Property(parameter, nameof(IT332FormatProbe.Id)), typeof(object)),
            Expression.Convert(Expression.Property(parameter, nameof(IT332FormatProbe.S)), typeof(object)));
        return Expression.Lambda<Func<IT332FormatProbe, string>>(body, parameter);
    }
}

[SqlTable("t332_format_probe")]
internal interface IT332FormatProbe
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("s")]
    string? S { get; set; }
}
