using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteStep4BuiltinFunctionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    /// <summary>The portable iif conditional renders and executes on SQLite.</summary>
    [Fact]
    public void Iif_ShouldExecuteOnSqlite()
    {
        var r = Context.From<ISimpleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.iif(x.Id == 1, 10, 20))
            .Single();

        r.Should().Be(10);
    }

    /// <summary>greatest/least render and execute on SQLite.</summary>
    [Fact]
    public void GreatestLeast_ShouldExecuteOnSqlite()
    {
        var r = Context.From<ISimpleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                G = SqlFunctions.Sql.greatest<int>(1, 2, 3),
                L = SqlFunctions.Sql.least<int>(1, 2, 3)
            })
            .Single();

        r.G.Should().Be(3);
        r.L.Should().Be(1);
    }

    /// <summary>nullif renders and executes on SQLite.</summary>
    [Fact]
    public void NullIf_ShouldExecuteOnSqlite()
    {
        var r = Context.From<ISimpleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.nullif<int?>(5, 5))
            .Single();

        r.Should().BeNull();
    }

    /// <summary>end_of_month renders and executes on SQLite.</summary>
    [Fact]
    public void EndOfMonth_ShouldExecuteOnSqlite()
    {
        var r = Context.From<ISimpleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.end_of_month(new DateTime(2024, 2, 15)))
            .Single();

        r.Should().Be(new DateTime(2024, 2, 29));
    }

    /// <summary>date_from_parts renders and executes on SQLite.</summary>
    [Fact]
    public void DateFromParts_ShouldExecuteOnSqlite()
    {
        var r = Context.From<ISimpleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_from_parts(2024, 2, 29))
            .Single();

        r.Should().Be(new DateTime(2024, 2, 29));
    }

    /// <summary>date_trunc is refused before SQL on SQLite.</summary>
    [Fact]
    public void DateTrunc_ShouldBeUnsupportedOnSqlite()
    {
        var act = () => Context.From<ISimpleEntity>()
            .Select(x => SqlFunctions.Sql.date_trunc("month", x.Id == 0 ? DateTime.Now : DateTime.Now))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*date_trunc function is not supported*");
    }

    /// <summary>choose is refused before SQL on SQLite.</summary>
    [Fact]
    public void Choose_ShouldBeUnsupportedOnSqlite()
    {
        var act = () => Context.From<ISimpleEntity>()
            .Select(x => SqlFunctions.SqlServer.choose(1, 1, 2))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*choose conditional function is not supported*");
    }


    /// <summary>full-text contains is refused before SQL on SQLite.</summary>
    [Fact]
    public void ContainsFullText_ShouldBeUnsupportedOnSqlite()
    {
        var act = () => Context.From<ISimpleEntity>()
            .Select(x => SqlFunctions.Sql.contains(x.Id, "q"))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*full-text predicates (contains/freetext)*");
    }

    /// <summary>full-text freetext is refused before SQL on SQLite.</summary>
    [Fact]
    public void Freetext_ShouldBeUnsupportedOnSqlite()
    {
        var act = () => Context.From<ISimpleEntity>()
            .Select(x => SqlFunctions.Sql.freetext(x.Id, "q"))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*full-text predicates (contains/freetext)*");
    }

    /// <summary>date_add field validation: an unknown field is refused.</summary>
    [Fact]
    public void DateAdd_UnknownField_ShouldBeRefused()
    {
        var act = () => Context.From<ISimpleEntity>()
            .Select(x => SqlFunctions.Sql.date_add("fortnight", 1, DateTime.Now))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*is not a supported date_add field*");
    }

    /// <summary>date_diff field validation: an unknown field is refused.</summary>
    [Fact]
    public void DateDiff_UnknownField_ShouldBeRefused()
    {
        var act = () => Context.From<ISimpleEntity>()
            .Select(x => SqlFunctions.Sql.date_diff("fortnight", DateTime.Now, DateTime.Now.AddDays(1)))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*is not a supported date_diff field*");
    }

    /// <summary>date_part numeric epoch mismatch is refused.</summary>
    [Fact]
    public void DatePart_EpochMismatch_ShouldBeRefused()
    {
        var act = () => Context.From<ISimpleEntity>()
            .Select(x => SqlFunctions.Sql.date_part("year", DateTime.Now))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*date_part only provides the numeric 'epoch' part*");
    }

    /// <summary>extract on epoch is refused (use date_part).</summary>
    [Fact]
    public void Extract_Epoch_ShouldBeRefused()
    {
        var act = () => Context.From<ISimpleEntity>()
            .Select(x => SqlFunctions.Sql.extract("epoch", DateTime.Now))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*'epoch' is a numeric date part; use date_part*");
    }

    /// <summary>greatest/least requires at least one argument.</summary>
    [Fact]
    public void GreatestLeast_NoArgs_ShouldBeRefused()
    {
        var act = () => Context.From<ISimpleEntity>()
            .Select(x => SqlFunctions.Sql.greatest<int>())
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*greatest/least requires at least one argument*");
    }
}
