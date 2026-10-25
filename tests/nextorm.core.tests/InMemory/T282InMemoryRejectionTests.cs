using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// T282 (#282): the in-memory provider has no server-side BASE64, crypto or decimal parsing, so all
/// five markers must fail closed with the literal message the rewriter already uses for a SQL-only
/// function. A compiled marker call would otherwise fault on the null sentinel surface or fabricate a
/// value, so each name is asserted individually.
/// </summary>
public class T282InMemoryRejectionTests
{
    private readonly InMemoryRepository _sut;

    public T282InMemoryRejectionTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 } });
    }

    private static string Message(string name) =>
        $"The {name} function is not supported by the in-memory provider.";

    [Fact]
    public void T282_Base64Encode_ShouldFailClosedInMemory()
    {
        var act = () => _sut.SimpleEntity.Select(_ => new { V = SqlFunctions.Sql.base64_encode(new byte[] { 1, 2, 3 }) }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage(Message("base64_encode"));
    }

    [Fact]
    public void T282_Base64Decode_ShouldFailClosedInMemory()
    {
        var act = () => _sut.SimpleEntity.Select(_ => new { V = SqlFunctions.Sql.base64_decode("AQID") }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage(Message("base64_decode"));
    }

    [Fact]
    public void T282_CryptGenRandom_ShouldFailClosedInMemory()
    {
        var act = () => _sut.SimpleEntity.Select(_ => new { V = SqlFunctions.Sql.crypt_gen_random(16) }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage(Message("crypt_gen_random"));
    }

    [Fact]
    public void T282_Parse_ShouldFailClosedInMemory()
    {
        var act = () => _sut.SimpleEntity.Select(_ => new { V = SqlFunctions.SqlServer.parse("1234.50") }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage(Message("parse"));
    }

    [Fact]
    public void T282_TryParse_ShouldFailClosedInMemory()
    {
        var act = () => _sut.SimpleEntity.Select(_ => new { V = SqlFunctions.SqlServer.try_parse("1234.50") }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage(Message("try_parse"));
    }

    [Fact]
    public void T282_NullBinaryLiteral_ShouldFailClosedInMemory()
    {
        var act = () => _sut.SimpleEntity.Select(_ => new { V = SqlFunctions.Sql.base64_encode(null) }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage(Message("base64_encode"));
    }
}
