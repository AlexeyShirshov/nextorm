using System.Reflection;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.MariaDb;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.Sqlite;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// T282 (#282) core contract for the five new built-in functions: the fixed one-argument signatures,
/// the generated XML documentation, the safe capability defaults (false plus the unsupported-provider
/// exception) and the direct-marker guard.
/// </summary>
public sealed class T282CommonFunctionsContractTests
{
    private static string XmlDocPath => Path.Combine(AppContext.BaseDirectory, "nextorm.core.xml");

    /// <summary>
    /// The five members keep the reviewed shape: exactly one parameter, the declared return type and no
    /// optional/variadic overload.
    /// </summary>
    [Theory]
    [InlineData("base64_encode", typeof(CommonFunctions), typeof(byte[]), typeof(string))]
    [InlineData("base64_decode", typeof(CommonFunctions), typeof(string), typeof(byte[]))]
    [InlineData("crypt_gen_random", typeof(CommonFunctions), typeof(int?), typeof(byte[]))]
    [InlineData("parse", typeof(SqlServerFunctions), typeof(string), typeof(decimal?))]
    [InlineData("try_parse", typeof(SqlServerFunctions), typeof(string), typeof(decimal?))]
    public void T282_PublicSignatures_ShouldBeSingleArgumentMembers(
        string name, Type declaringType, Type parameterType, Type returnType)
    {
        var method = declaringType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull($"'{name}' is part of the public surface");
        method!.GetParameters().Should().ContainSingle();
        method.GetParameters()[0].ParameterType.Should().Be(parameterType);
        method.GetParameters()[0].IsOptional.Should().BeFalse();
        method.ReturnType.Should().Be(returnType);
        method.IsStatic.Should().BeFalse();
    }

    /// <summary>Every new public member carries generated XML documentation.</summary>
    [Theory]
    [InlineData("M:NextORM.Core.CommonFunctions.base64_encode(System.Byte[])")]
    [InlineData("M:NextORM.Core.CommonFunctions.base64_decode(System.String)")]
    [InlineData("M:NextORM.Core.CommonFunctions.crypt_gen_random(System.Nullable{System.Int32})")]
    [InlineData("M:NextORM.Core.SqlServerFunctions.parse(System.String)")]
    [InlineData("M:NextORM.Core.SqlServerFunctions.try_parse(System.String)")]
    public void T282_PublicMembers_ShouldCarryXmlDocumentation(string memberId)
    {
        var xml = File.ReadAllText(XmlDocPath);

        xml.Should().Contain($"<member name=\"{memberId}\">");
    }

    /// <summary>
    /// The capability flags are default interface members returning <c>false</c>, and the default
    /// renderers fail closed with the unsupported-provider message; an external dialect written before
    /// these members existed keeps that safe behaviour.
    /// </summary>
    [Theory]
    [InlineData(nameof(ISqlDialect.SupportsBase64Encode), nameof(ISqlDialect.MakeBase64Encode), "base64_encode")]
    [InlineData(nameof(ISqlDialect.SupportsBase64Decode), nameof(ISqlDialect.MakeBase64Decode), "base64_decode")]
    [InlineData(nameof(ISqlDialect.SupportsCryptGenRandom), nameof(ISqlDialect.MakeCryptGenRandom), "crypt_gen_random")]
    public void T282_CapabilityDefaults_ShouldBeSafe(string supportsName, string makeName, string functionName)
    {
        var supports = typeof(ISqlDialect).GetProperty(supportsName);
        supports.Should().NotBeNull();
        supports!.GetMethod!.IsAbstract.Should().BeFalse($"{supportsName} must stay a default interface member");

        var dialect = SqliteDialect.Instance;
        ((bool)supports.GetValue(dialect)!).Should().BeFalse($"SQLite has no {functionName}");

        var make = typeof(ISqlDialect).GetMethod(makeName, [typeof(string)]);
        make.Should().NotBeNull();
        FluentActions.Invoking(() => make!.Invoke(dialect, ["[x]"]))
            .Should().Throw<TargetInvocationException>()
            .WithInnerException<NotSupportedException>()
            .WithMessage($"The {functionName} function is not supported by this provider.");
    }

    /// <summary>SQL Server, PostgreSQL, MySQL and MariaDB opt in; ClickHouse and SQLite do not.</summary>
    [Fact]
    public void T282_Capabilities_ShouldMatchTheFrozenMatrix()
    {
        SqlServerDialect.Instance.SupportsBase64Encode.Should().BeTrue();
        PostgresDialect.Instance.SupportsBase64Encode.Should().BeTrue();
        MySqlDialect.Instance.SupportsBase64Encode.Should().BeTrue();
        MariaDbDialect.Instance.SupportsBase64Encode.Should().BeTrue();
        ClickHouseDialect.Instance.SupportsBase64Encode.Should().BeFalse();
        SqliteDialect.Instance.SupportsBase64Encode.Should().BeFalse();

        SqlServerDialect.Instance.SupportsBase64Decode.Should().BeTrue();
        PostgresDialect.Instance.SupportsBase64Decode.Should().BeTrue();
        MySqlDialect.Instance.SupportsBase64Decode.Should().BeTrue();
        MariaDbDialect.Instance.SupportsBase64Decode.Should().BeTrue();
        ClickHouseDialect.Instance.SupportsBase64Decode.Should().BeFalse();
        SqliteDialect.Instance.SupportsBase64Decode.Should().BeFalse();

        SqlServerDialect.Instance.SupportsCryptGenRandom.Should().BeTrue();
        PostgresDialect.Instance.SupportsCryptGenRandom.Should().BeTrue();
        MySqlDialect.Instance.SupportsCryptGenRandom.Should().BeTrue();
        MariaDbDialect.Instance.SupportsCryptGenRandom.Should().BeTrue();
        ClickHouseDialect.Instance.SupportsCryptGenRandom.Should().BeFalse();
        SqliteDialect.Instance.SupportsCryptGenRandom.Should().BeFalse();
    }

    /// <summary>A direct CLR call of a marker never evaluates a value; it raises instead of returning one.</summary>
    [Fact]
    public void T282_Markers_ShouldThrowWhenCalledDirectly()
    {
        var common = new CommonFunctions();

        FluentActions.Invoking(() => common.base64_encode(new byte[] { 1, 2 })).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => common.base64_decode("AA==")).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => common.crypt_gen_random(16)).Should().Throw<NotSupportedException>();

        var sqlServer = new SqlServerFunctions();

        FluentActions.Invoking(() => sqlServer.parse("1")).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => sqlServer.try_parse("1")).Should().Throw<NotSupportedException>();
    }

    /// <summary>An argument list with the wrong arity is rejected before any SQL is emitted.</summary>
    [Fact]
    public void T282_Translator_ShouldRejectWrongArity()
    {
        FluentActions.Invoking(() => PortableBinaryFunctionTranslator.RequireSingleArgument("base64_encode", 2))
            .Should().Throw<ArgumentException>();

        FluentActions.Invoking(() => PortableBinaryFunctionTranslator.RequireSingleArgument("crypt_gen_random", 0))
            .Should().Throw<ArgumentException>();

        FluentActions.Invoking(() => PortableBinaryFunctionTranslator.RequireSingleArgument("base64_decode", 1))
            .Should().NotThrow();
    }
}
