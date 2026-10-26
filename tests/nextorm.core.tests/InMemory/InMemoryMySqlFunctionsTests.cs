using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory provider cannot evaluate the MySQL/MariaDB-only surface and rejects it with a clear
/// message instead of dereferencing the <c>SqlFunctions.MySql</c> marker.
/// </summary>
public class InMemoryMySqlFunctionsTests
{
    private readonly InMemoryRepository _sut;

    public InMemoryMySqlFunctionsTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 } });
    }

    [Fact]
    public void MySqlFunctions_ShouldThrowBecauseNotEvaluatable()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => SqlFunctions.MySql.find_in_set("a", "a,b"))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*find_in_set*MySQL/MariaDB*in-memory*");
    }

    [Fact]
    public void JsonStorageFree_ShouldThrowBecauseNotSupported()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => SqlFunctions.MySql.json_storage_free("{\"a\":1}"))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*json_storage_free*MySQL/MariaDB*in-memory*");
    }

    [Fact]
    public void MariaDbOnlyFunctions_ShouldThrowBecauseNotEvaluatable()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => SqlFunctions.MySql.nvl<string>(null, "fallback"))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*nvl*MySQL/MariaDB*in-memory*");
    }

    [Fact]
    public void T284_JsonFunctions_ShouldThrowOnUnsupportedProvider()
    {
        var act = () => _sut.SimpleEntity
            .Select(it => SqlFunctions.MySql.json_equals("{\"a\":1}", "{\"a\":1}"))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*json_equals*MySQL/MariaDB*in-memory*");
    }
}
