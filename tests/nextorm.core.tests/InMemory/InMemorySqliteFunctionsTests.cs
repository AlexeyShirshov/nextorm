using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory provider evaluates the subset of the SQLite surface (<see cref="SqlFunctions.Sqlite"/>)
/// that has a native CLR equivalent; the SQL-only members are left to the SQL providers.
/// </summary>
public class InMemorySqliteFunctionsTests
{
    private readonly InMemoryRepository _sut;

    public InMemorySqliteFunctionsTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }

    [Fact]
    public void CoreScalars_ShouldExecuteNatively()
    {
        var r = _sut.SimpleEntity
            .Where(it => it.Id == 1)
            .Select(it => new
            {
                H = SqlFunctions.Sqlite.hex("A"),
                O = SqlFunctions.Sqlite.octet_length("A"),
                Uni = SqlFunctions.Sqlite.unicode("A"),
                Ch = SqlFunctions.Sqlite.@char(65, 66),
                Ty = SqlFunctions.Sqlite.@typeof(1),
                Ifn = SqlFunctions.Sqlite.ifnull((string?)null, "x"),
                If = SqlFunctions.Sqlite.@if(it.Id == 1, "a", "b")
            })
            .ToList();

        r.Should().OnlyContain(x =>
            x.H == "41" && x.O == 1 && x.Uni == 65 && x.Ch == "AB" &&
            x.Ty == "integer" && x.Ifn == "x" && x.If == "a");
    }

    [Fact]
    public void Unhex_ShouldDecodeNatively()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { B = SqlFunctions.Sqlite.unhex("4142") })
            .ToList();

        r.Should().OnlyContain(x => x.B != null && x.B!.Length == 2 && x.B![0] == 0x41 && x.B![1] == 0x42);
    }

    [Fact]
    public void MathFunctions_ShouldExecuteNatively()
    {
        var r = _sut.SimpleEntity
            .Select(it => new
            {
                A = SqlFunctions.Sqlite.acos(1.0),
                L2 = SqlFunctions.Sqlite.log2(8.0),
                M = SqlFunctions.Sqlite.mod(5.0, 2.0),
                T = SqlFunctions.Sqlite.tanh(0.0)
            })
            .ToList();

        r.Should().OnlyContain(x =>
            Math.Abs(x.A!.Value) < 1e-9 &&
            Math.Abs(x.L2!.Value - 3.0) < 1e-9 &&
            Math.Abs(x.M!.Value - 1.0) < 1e-9 &&
            Math.Abs(x.T!.Value) < 1e-9);
    }

    // ---------------------------------------------------------------------------------------------
    // FTS3/FTS4/FTS5 (#181): the full-text members are SQL-only, so the in-memory provider must
    // throw an explicit NotSupportedException instead of a NullReferenceException or a fabricated
    // value.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_Match_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Where(it => SqlFunctions.Sqlite.Match("docs_fts", "foo"))
            .Select(it => new { it.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Match*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5Bm25_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => new { V = SqlFunctions.Sqlite.FTS5bm25("docs_fts") })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*FTS5bm25*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5Highlight_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => new { V = SqlFunctions.Sqlite.Highlight("docs_fts", 0, "[", "]") })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Highlight*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5Snippet_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => new { V = SqlFunctions.Sqlite.Snippet("docs_fts", 0, "[", "]", "...", 8) })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Snippet*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5Rank_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => new { V = SqlFunctions.Sqlite.Rank("docs_fts") })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Rank*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3RankOverMatchInfo_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => new { V = SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("docs_fts")) })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Rank*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3RowId_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => new { V = SqlFunctions.Sqlite.RowId("docs_fts") })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*RowId*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3Offsets_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => new { V = SqlFunctions.Sqlite.FTS3Offsets("docs_fts") })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*FTS3Offsets*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3MatchInfo_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => new { V = SqlFunctions.Sqlite.FTS3MatchInfo("docs_fts", "pcx") })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*FTS3MatchInfo*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3Snippet_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => new { V = SqlFunctions.Sqlite.FTS3Snippet("docs_fts") })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*FTS3Snippet*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5MatchTable_ShouldThrow()
    {
        var act = () => _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<SimpleEntity>("docs_fts", "foo"))
            .Select(r => new { r.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>();
    }
}
