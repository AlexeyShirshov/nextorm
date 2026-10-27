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

    [Fact]
    public void NativeAndAuto_ShouldResolveTheTextConverter_WhenTheNativeProviderTypeIsString()
    {
        // T326 (#326): SQL Server 2025+ has a native `json` column but transports it as text, so the
        // dialect declares string as its native provider type. Both storage forms must then resolve to
        // the existing text converter: one serializer, no DOM parameter, no second representation.
        var auto = JsonColumnConverterFactory.Create(typeof(JsonColumnSelectionPoco), JsonColumnStorage.Auto);
        var native = JsonColumnConverterFactory.Create(typeof(JsonColumnSelectionPoco), JsonColumnStorage.Native);
        var text = JsonColumnConverterFactory.Create(typeof(JsonColumnSelectionPoco), JsonColumnStorage.Text);

        var resolvedAuto = ((IJsonColumnConverter)auto).Resolve(StringNativeDialect.Instance);
        var resolvedNative = ((IJsonColumnConverter)native).Resolve(StringNativeDialect.Instance);
        var resolvedText = ((IJsonColumnConverter)text).Resolve(StringNativeDialect.Instance);

        resolvedAuto.Should().BeOfType<JsonColumnConverter<JsonColumnSelectionPoco, string>>();
        resolvedNative.Should().BeOfType<JsonColumnConverter<JsonColumnSelectionPoco, string>>();
        resolvedText.Should().BeOfType<JsonColumnConverter<JsonColumnSelectionPoco, string>>();

        resolvedAuto.ProviderType.Should().Be(typeof(string));
        resolvedNative.ProviderType.Should().Be(typeof(string));

        // No DOM parameter appears: the value the provider receives is the JSON document text.
        var model = new JsonColumnSelectionPoco { Name = "value" };
        resolvedAuto.ConvertToProvider(model).Should().BeOfType<string>();
        resolvedNative.ConvertToProvider(model).Should().BeOfType<string>();
        resolvedNative.ConvertToProvider(model).Should().Be(resolvedText.ConvertToProvider(model));

        // The Auto wrapper itself is stable: every resolve of the same instance returns the same converter.
        ((IJsonColumnConverter)auto).Resolve(StringNativeDialect.Instance).Should().BeSameAs(resolvedAuto);
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

    /// <summary>
    /// A dialect with a native JSON column that is transported as text (SQL Server 2025+/Azure SQL):
    /// the capability is on, but the provider representation is the plain CLR <c>string</c>.
    /// </summary>
    private sealed class StringNativeDialect : SqlDialectBase
    {
        internal static readonly StringNativeDialect Instance = new();

        public override bool SupportsJson => true;

        public override Type NativeJsonProviderType => typeof(string);

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }
}
