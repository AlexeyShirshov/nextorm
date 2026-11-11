using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// The SQLite <c>total()</c> aggregate has no in-memory equivalent: the in-memory provider must reject
/// it through the SQLite function translation guard with the shared provider message instead of
/// faulting on the null <see cref="SqlFunctions.Sqlite"/> marker surface.
/// </summary>
public class SqliteTotalFunctionGuardTests
{
    private const string TotalMessage = "The SQLite function 'total' is not supported by this provider.";

    private readonly InMemoryRepository _sut;

    public SqliteTotalFunctionGuardTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 } });
    }

    [Fact]
    [Trait("Issue", "285")]
    public void Total_ShouldThrowBecauseNotSupported()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => SqlFunctions.Sqlite.total(it.Id))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage(TotalMessage);
    }

    [Fact]
    [Trait("Issue", "285")]
    public void Total_ShouldThrowForLiteralAndNullArguments()
    {
        var literal = () => _sut.SimpleEntity
            .Select(it => SqlFunctions.Sqlite.total(2.5))
            .ToList();

        var nullValue = () => _sut.SimpleEntity
            .Select(it => SqlFunctions.Sqlite.total((int?)null))
            .ToList();

        literal.Should().Throw<NotSupportedException>().WithMessage(TotalMessage);
        nullValue.Should().Throw<NotSupportedException>().WithMessage(TotalMessage);
    }

    [Fact]
    [Trait("Issue", "285")]
    public void Total_ShouldThrowOnEveryPreparationOfTheSameCommand()
    {
        var command = _sut.SimpleEntity.Select(it => SqlFunctions.Sqlite.total(it.Id));

        FluentActions.Invoking(() => command.ToList())
            .Should().Throw<NotSupportedException>().WithMessage(TotalMessage);

        // The rejected first preparation must not leave a cached plan that lets the second attempt through.
        FluentActions.Invoking(() => command.ToList())
            .Should().Throw<NotSupportedException>().WithMessage(TotalMessage);
    }

    [Fact]
    [Trait("Issue", "285")]
    public void UnmatchedSqliteMembers_ShouldThrowBecauseNotSupported()
    {
        var soundex = () => _sut.SimpleEntity
            .Select(it => SqlFunctions.Sqlite.soundex("soundex"))
            .ToList();

        var printf = () => _sut.SimpleEntity
            .Select(it => SqlFunctions.Sqlite.printf("%d", it.Id))
            .ToList();

        var jsonGroupArray = () => _sut.SimpleEntity
            .Select(it => SqlFunctions.Sqlite.json_group_array(it.Id))
            .ToList();

        soundex.Should().Throw<NotSupportedException>()
            .WithMessage("The SQLite function 'soundex' is not supported by this provider.");
        printf.Should().Throw<NotSupportedException>()
            .WithMessage("The SQLite function 'printf' is not supported by this provider.");
        jsonGroupArray.Should().Throw<NotSupportedException>()
            .WithMessage("The SQLite function 'json_group_array' is not supported by this provider.");
    }
}
