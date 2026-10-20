using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Raw-source binding (<c>From(string)</c>/<c>FromSql</c> + <c>BindEntity</c>) executed against a real
/// SQLite database, plus the composition guard: a source that already carries a predicate, projection,
/// join or any other SQL-affecting modifier must be rejected instead of silently dropping the binding.
/// One case per modifier state keeps the short-circuit chain of the guard fully walked.
/// </summary>
[Collection("Sqlite")]
public sealed class RawSourceBindingGuardTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void BoundNamedSource_ShouldExecute()
    {
        var ctx = _sut.DataProvider;

        var ids = ctx.From("simple_entity")
            .BindEntity<SimpleEntity>(new[] { "id" })
            .Where(x => x.Id > 5)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(6, 7, 8, 9, 10);
    }

    [Fact]
    public void BoundRawSqlSource_ShouldExecute()
    {
        var ctx = _sut.DataProvider;

        var ids = ctx.FromSql("select id from simple_entity where id <= 3")
            .BindEntity<SimpleEntity>(new[] { "id" })
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void BoundSource_ShouldReuseThePlanCache()
    {
        var ctx = _sut.DataProvider;

        for (var i = 0; i < 3; i++)
        {
            ctx.From("simple_entity")
                .BindEntity<SimpleEntity>(new[] { "id" })
                .Where(x => x.Id > 7)
                .Count()
                .Should().Be(3);
        }
    }

    [Fact]
    public void ComposedSource_ShouldRejectTheBinding()
    {
        var ctx = _sut.DataProvider;

        var cases = new (string Name, Func<EntityBuilder<TableAlias>> Compose)[]
        {
            ("predicate", () => ctx.From("simple_entity").Where(t => t["id"].AsInt > 0)),
            ("distinct", () => ctx.From("simple_entity").Distinct()),
            ("limit", () => ctx.From("simple_entity").Limit(1)),
            ("offset", () => ctx.From("simple_entity").Offset(1)),
            ("order by", () => ctx.From("simple_entity").OrderBy(t => t["id"].AsInt)),
            ("group by", () => ctx.From("simple_entity").GroupBy(t => t["id"].AsInt)),
            ("having", () => ctx.From("simple_entity").Having(t => t["id"].AsInt > 0)),
            ("window", () => ctx.From("simple_entity").Window(
                "w",
                new Expression<Func<TableAlias, object?>>[] { t => t["id"].AsInt })),
            ("row lock", () => ctx.From("simple_entity").ForUpdate()),
            ("table override", () => ctx.From("simple_entity").WithTableName("simple_entity")),
            ("schema override", () => ctx.From("simple_entity").WithSchema("main")),
            ("database override", () => ctx.From("simple_entity").WithDatabase("main")),
            ("server override", () => ctx.From("simple_entity").WithServer("primary")),
            ("in-scope hint", () => ctx.From("simple_entity").WithTablesInScopeHint("simple_entity"))
        };

        foreach (var (name, compose) in cases)
        {
            var composed = compose();
            FluentActions
                .Invoking(() => composed.BindEntity<SimpleEntity>(new[] { "id" }))
                .Should().Throw<InvalidOperationException>(name)
                .WithMessage("*must be called immediately after FromSql/From(string)*");
        }
    }

    [Fact]
    public void InvalidBinding_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;

        FluentActions
            .Invoking(() => ctx.From("simple_entity")
                .BindEntity<TableAlias>(new[] { "id" }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*cannot target TableAlias*");

        FluentActions
            .Invoking(() => ctx.From(ctx.From("simple_entity"))
                .BindEntity<SimpleEntity>(new[] { "id" }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*direct FromSql or From(string) source*");

        FluentActions
            .Invoking(() => ctx.From("simple_entity").BindEntity<SimpleEntity>(new[] { "id", " " }))
            .Should().Throw<ArgumentException>()
            .WithMessage("*non-null and non-blank*");

        FluentActions
            .Invoking(() => ctx.From("simple_entity").BindEntity<SimpleEntity>(null!))
            .Should().Throw<ArgumentNullException>()
            .WithMessage("*availableColumns*");
    }

    /// <summary>
    /// SqlBuilder.cs:2203 — a raw-SQL source parameter and a captured variable with the same name would
    /// bind two parameters named <c>id</c>; the build fails closed instead of letting the provider reject
    /// the command. Distinct names execute and return the filtered rows.
    /// </summary>
    [Fact]
    public void DuplicateParameterName_WithRawSqlSource_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        var id = 2;
        var floor = 3;

        FluentActions
            .Invoking(() => ctx.FromSql("select id from simple_entity where id > @id", new { id })
                .Where(t => t["id"].AsInt > id)
                .Select(t => t["id"].AsInt)
                .ToList())
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("*two parameters named 'id'*");

        ctx.FromSql("select id from simple_entity where id > @floor", new { floor })
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt)
            .ToList()
            .Should().Equal(4, 5, 6, 7, 8, 9, 10);
    }
}
