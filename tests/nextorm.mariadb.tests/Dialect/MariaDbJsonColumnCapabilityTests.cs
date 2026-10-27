using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// T325 (#325) — MariaDB's capability decision is its own, never inherited from MySQL. MariaDB's
/// <c>JSON</c> is an alias for <c>LONGTEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_bin</c> plus a
/// column-level <c>CHECK (json_valid(col))</c>: <c>information_schema.COLUMNS.DATA_TYPE</c> reports
/// <c>longtext</c> and the text is stored verbatim, so there is no native storage type to expose.
/// </summary>
public class MariaDbJsonColumnCapabilityTests
{
    private static readonly ISqlDialect Dialect = MariaDbDialect.Instance;

    [Fact]
    public void SupportsJson_IsFalse_AndDeclaredByMariaDbItself()
    {
        Dialect.SupportsJson.Should().BeFalse();

        // The override is explicit: MariaDbDialect derives from MySqlDialect, so an inherited answer
        // would silently follow MySQL's decision instead of MariaDB's own.
        var property = typeof(MariaDbDialect).GetProperty(nameof(ISqlDialect.SupportsJson));
        property.Should().NotBeNull();
        property!.DeclaringType.Should().Be(typeof(MariaDbDialect));
        typeof(MariaDbDialect).Should().BeAssignableTo<MySqlDialect>();
    }

    [Fact]
    public void Auto_ShouldResolveTheTextConverter()
    {
        var converter = JsonColumnConverterFactory.Create(typeof(MariaDbJsonCapabilityPoco), JsonColumnStorage.Auto);

        ((IJsonColumnConverter)converter).Resolve(Dialect).ProviderType.Should().Be(typeof(string));
    }

    [Fact]
    public void Text_ShouldResolveTheTextConverter()
    {
        var converter = JsonColumnConverterFactory.Create(typeof(MariaDbJsonCapabilityPoco), JsonColumnStorage.Text);

        ((IJsonColumnConverter)converter).Resolve(Dialect).ProviderType.Should().Be(typeof(string));
    }

    [Fact]
    public void Native_ShouldBeRefusedBeforeAnyCommandRuns()
    {
        var converter = JsonColumnConverterFactory.Create(typeof(MariaDbJsonCapabilityPoco), JsonColumnStorage.Native);

        var act = () => ((IJsonColumnConverter)converter).Resolve(Dialect);

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*{nameof(MariaDbDialect)} does not support a native JSON column*")
            .WithMessage($"*{nameof(JsonColumnStorage.Text)}*");
    }

    public sealed class MariaDbJsonCapabilityPoco
    {
        public string? Name { get; set; }
    }
}
