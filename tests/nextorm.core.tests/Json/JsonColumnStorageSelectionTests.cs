using System.Text;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T325 (#325) — storage selection for <see cref="JsonColumnStorage"/>. Capability is the only dialect
/// knob: <see cref="ISqlDialect.SupportsJson"/> means both "a genuine native column type exists" and
/// "an <see cref="JsonColumnStorage.Auto"/> mapping resolves to it". MySQL deliberately does not opt in,
/// because MySqlConnector cannot bind a JSON DOM value (see the MySQL/MariaDB capability tests and the
/// live storage tests); MariaDB's <c>JSON</c> is a validating <c>LONGTEXT</c> alias, not native storage.
/// </summary>
public class JsonColumnStorageSelectionTests
{
    [Fact]
    public void Auto_ShouldResolveText_WhenDialectHasNoNativeJsonStorage()
    {
        var converter = JsonColumnConverterFactory.Create(typeof(JsonColumnSelectionPoco), JsonColumnStorage.Auto);

        var resolved = ((IJsonColumnConverter)converter).Resolve(TextOnlyDialect.Instance);

        resolved.ProviderType.Should().Be(typeof(string));
        resolved.ConvertToProvider(new JsonColumnSelectionPoco { Name = "value" }).Should().BeOfType<string>();
    }

    [Fact]
    public void Auto_ShouldResolveNative_WhenDialectAdvertisesNativeJsonStorage()
    {
        // The pre-T325 semantics are unchanged for a dialect that inherits the base: capability alone
        // selects the native representation for Auto — there is no separate opt-in policy member.
        var converter = JsonColumnConverterFactory.Create(typeof(JsonColumnSelectionPoco), JsonColumnStorage.Auto);

        var resolved = ((IJsonColumnConverter)converter).Resolve(NativeDialect.Instance);

        resolved.ProviderType.Should().Be(typeof(JsonElement));
        resolved.ConvertToProvider(new JsonColumnSelectionPoco { Name = "value" }).Should().BeOfType<JsonElement>();
    }

    [Fact]
    public void Text_ShouldResolveText_EvenWhenDialectHasNativeJsonStorage()
    {
        var converter = JsonColumnConverterFactory.Create(typeof(JsonColumnSelectionPoco), JsonColumnStorage.Text);

        ((IJsonColumnConverter)converter).Resolve(NativeDialect.Instance).ProviderType.Should().Be(typeof(string));
    }

    [Fact]
    public void Native_ShouldThrowNamedNotSupported_WhenDialectHasNoNativeJsonStorage()
    {
        var converter = JsonColumnConverterFactory.Create(typeof(JsonColumnSelectionPoco), JsonColumnStorage.Native);

        var act = () => ((IJsonColumnConverter)converter).Resolve(TextOnlyDialect.Instance);

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*{nameof(TextOnlyDialect)} does not support a native JSON column*")
            .WithMessage($"*{nameof(JsonColumnStorage.Text)}*");
    }

    [Fact]
    public void DialectSurface_ShouldNotCarryASeparateAutoSelectionPolicyMember()
    {
        // Pinned: T325 r=1 introduced `UseNativeJsonStorageByDefault` and r=2 removed it. A separate
        // capability/policy split is not part of the shipped contract — adding one back must fail here.
        typeof(ISqlDialect).GetProperty("UseNativeJsonStorageByDefault").Should().BeNull();
        typeof(ISqlDialect).GetMethod("get_UseNativeJsonStorageByDefault").Should().BeNull();
        typeof(SqlDialectBase).GetProperty("UseNativeJsonStorageByDefault").Should().BeNull();
    }

    public sealed class JsonColumnSelectionPoco
    {
        public string? Name { get; set; }
    }

    /// <summary>A dialect with the base defaults: no native JSON storage.</summary>
    private sealed class TextOnlyDialect : SqlDialectBase
    {
        internal static readonly TextOnlyDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>A dialect that only opts into the capability, exactly like PostgreSQL/ClickHouse.</summary>
    private sealed class NativeDialect : SqlDialectBase
    {
        internal static readonly NativeDialect Instance = new();

        public override bool SupportsJson => true;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }
}
