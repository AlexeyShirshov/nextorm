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
                D = SqlFunctions.Sqlite.degrees(SqlFunctions.Sqlite.pi()),
                L2 = SqlFunctions.Sqlite.log2(8.0),
                M = SqlFunctions.Sqlite.mod(5.0, 2.0),
                R = SqlFunctions.Sqlite.radians(180.0),
                T = SqlFunctions.Sqlite.tanh(0.0)
            })
            .ToList();

        r.Should().OnlyContain(x =>
            Math.Abs(x.A!.Value) < 1e-9 &&
            Math.Abs(x.D!.Value - 180.0) < 1e-9 &&
            Math.Abs(x.L2!.Value - 3.0) < 1e-9 &&
            Math.Abs(x.M!.Value - 1.0) < 1e-9 &&
            Math.Abs(x.R!.Value - Math.PI) < 1e-9 &&
            Math.Abs(x.T!.Value) < 1e-9);
    }
}
