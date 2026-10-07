using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

/// <summary>
/// Freeze/baseline for the generated join-alias public surface. The source generator is part of the
/// shipped contract, so its output shape must not drift silently: the namespace marker type, the
/// public <c>AliasProjection_*</c>/<c>AliasJoin_*</c> pairs, the <c>JoinSlot</c> positions and the
/// seven generated operator extensions are asserted here. This is the equivalent of a
/// <c>PublicAPI</c> baseline for a surface that is generated rather than hand-written.
/// </summary>
public class AliasGeneratedSurfaceTests
{
    private const string GeneratedNamespace = "NextORM.Generated.nextorm_alias_tests";

    private static readonly string[] JoinOperators =
        ["Join", "LeftJoin", "RightJoin", "FullJoin", "CrossJoin", "CrossApply", "OuterApply"];

    [Fact]
    public void Generated_public_type_set_is_frozen()
    {
        // The alias test assembly writes Alias.Buyer / Alias.Approver / Alias.Buyer2 across alias-only and
        // mixed (alias/positional) chains, plus the root alias Alias.Root (issue #160 Phase 2), so the
        // generator's whole public surface (one marker class, one builder/projection pair per discovered
        // slot schema, one extension class) is fully deterministic.
        // r=2: mixed chains add the P-encoded schemas; the digit alias proves names are not parsed as slots.
        // E160-13 F1: Buyer2 in slot 3 (P1_P2_A3_Buyer2) discriminates a trailing-digit slot parse, where
        // Buyer2 in slot 2 (P1_A2_Buyer2) cannot.
        // Phase 2: the root alias adds A1_Root (root only), A1_Root_A2_Buyer (alias join after it) and
        // A1_Root_P2 (a generated positional instance transition after the root alias).
        var names = typeof(Alias).Assembly.GetTypes()
            .Where(type => type.Namespace == GeneratedNamespace && !type.IsNested)
            .Where(type => type.Name == "Alias"
                || type.Name == "JoinAliasExtensions"
                || type.Name.StartsWith("AliasProjection_", StringComparison.Ordinal)
                || type.Name.StartsWith("AliasJoin_", StringComparison.Ordinal))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        names.Should().Equal(
            "Alias",
            "AliasJoin_A1_Root_A2_Buyer`2",
            "AliasJoin_A1_Root_P2`2",
            "AliasJoin_A1_Root`1",
            "AliasJoin_P1_A2_Buyer2`2",
            "AliasJoin_P1_A2_Buyer_A3_Approver`3",
            "AliasJoin_P1_A2_Buyer_P3_A4_Approver`4",
            "AliasJoin_P1_A2_Buyer_P3`3",
            "AliasJoin_P1_A2_Buyer`2",
            "AliasJoin_P1_P2_A3_Approver`3",
            "AliasJoin_P1_P2_A3_Buyer2`3",
            "AliasProjection_A1_Root_A2_Buyer`2",
            "AliasProjection_A1_Root_P2`2",
            "AliasProjection_A1_Root`1",
            "AliasProjection_P1_A2_Buyer2`2",
            "AliasProjection_P1_A2_Buyer_A3_Approver`3",
            "AliasProjection_P1_A2_Buyer_P3_A4_Approver`4",
            "AliasProjection_P1_A2_Buyer_P3`3",
            "AliasProjection_P1_A2_Buyer`2",
            "AliasProjection_P1_P2_A3_Approver`3",
            "AliasProjection_P1_P2_A3_Buyer2`3",
            "JoinAliasExtensions");
    }

    [Fact]
    public void Generated_marker_type_exposes_one_public_marker_per_alias()
    {
        var marker = typeof(Alias);
        marker.Namespace.Should().Be(GeneratedNamespace);
        marker.IsAbstract.Should().BeTrue("the marker type is static");
        marker.IsSealed.Should().BeTrue("the marker type is static");

        AssertMarker(marker, "Buyer");
        AssertMarker(marker, "Approver");
    }

    [Fact]
    public void Generated_projection_builder_pairs_are_public_and_slot_bound()
    {
        AssertPair(
            typeof(AliasProjection_P1_A2_Buyer<Order, Person>),
            typeof(AliasJoin_P1_A2_Buyer<Order, Person>),
            typeof(Projection<,>),
            ("Buyer", 2, typeof(Person)));

        AssertPair(
            typeof(AliasProjection_P1_A2_Buyer_A3_Approver<Order, Person, Person>),
            typeof(AliasJoin_P1_A2_Buyer_A3_Approver<Order, Person, Person>),
            typeof(Projection<,,>),
            ("Buyer", 2, typeof(Person)),
            ("Approver", 3, typeof(Person)));
    }

    [Fact]
    public void Generated_extension_class_exposes_the_seven_alias_operators()
    {
        var extensions = typeof(Alias).Assembly.GetType(GeneratedNamespace + ".JoinAliasExtensions");
        extensions.Should().NotBeNull();
        extensions!.IsAbstract.Should().BeTrue("the extension class is static");
        extensions.IsSealed.Should().BeTrue("the extension class is static");

        var methods = extensions.GetMethods(BindingFlags.Public | BindingFlags.Static);
        foreach (var joinOperator in JoinOperators)
        {
            var overloads = methods.Where(method => method.Name == joinOperator).ToArray();
            overloads.Should().NotBeEmpty($"the generator must emit an alias extension for {joinOperator}");
            overloads.Should().OnlyContain(method =>
                method.IsDefined(typeof(ExtensionAttribute), inherit: false));
            overloads.Should().OnlyContain(method =>
                method.ReturnType.Name.StartsWith("AliasJoin_", StringComparison.Ordinal));
            overloads.Should().OnlyContain(method =>
                method.GetParameters().Length >= 2
                && method.GetParameters().Last().ParameterType.Name.EndsWith("Marker", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Generated_extension_methods_have_unique_signatures_and_keep_the_frozen_baseline()
    {
        // #159 C1 regression freeze: distinct chains may share an emitted method (they differ only in an
        // intermediate step's source kind, which the signature does not carry), so the generator now
        // renders each method in full and collapses byte-identical duplicates. That must never rename or
        // drop a previously emitted member: every signature present before the change is still emitted,
        // and no two methods share a signature (the duplicate-signature CS0111 class).
        var extensions = typeof(Alias).Assembly.GetType(GeneratedNamespace + ".JoinAliasExtensions")!;
        var methods = extensions.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => JoinOperators.Contains(method.Name))
            .Select(Signature)
            .ToArray();

        methods.Should().OnlyHaveUniqueItems();
        methods.Should().Contain(FrozenExtensionSignatures);
    }

    private static string Signature(MethodInfo method)
    {
        var parameters = string.Join(", ", method.GetParameters().Select(parameter => FormatType(parameter.ParameterType)));
        return FormatType(method.ReturnType) + " " + method.Name + "`" + method.GetGenericArguments().Length + "(" + parameters + ")";
    }

    private static string FormatType(Type type)
    {
        if (type.IsGenericParameter) return type.Name;
        if (type.IsGenericType)
        {
            var name = type.GetGenericTypeDefinition().FullName!;
            name = name[..name.IndexOf('`')];
            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(FormatType)) + ">";
        }

        return type.FullName ?? type.Name;
    }

    // Captured on r1/n3 before the C1 regression chains were added; all of these must survive the dedup.
    private static readonly string[] FrozenExtensionSignatures =
    [
        "NextORM.Generated.nextorm_alias_tests.AliasJoin_P1_A2_Buyer<NextORM.AliasTests.Order, TJoin> CrossApply`1(NextORM.Core.EntityBuilder<NextORM.AliasTests.Order>, NextORM.Core.EntityBuilder<TJoin>, NextORM.Generated.nextorm_alias_tests.Alias+BuyerMarker)",
        "NextORM.Generated.nextorm_alias_tests.AliasJoin_P1_A2_Buyer<NextORM.AliasTests.Order, TJoin> CrossJoin`1(NextORM.Core.EntityBuilder<NextORM.AliasTests.Order>, NextORM.Core.EntityBuilder<TJoin>, NextORM.Generated.nextorm_alias_tests.Alias+BuyerMarker)",
        "NextORM.Generated.nextorm_alias_tests.AliasJoin_P1_A2_Buyer<NextORM.AliasTests.Order, TJoin> FullJoin`1(NextORM.Core.EntityBuilder<NextORM.AliasTests.Order>, NextORM.Core.EntityBuilder<TJoin>, System.Linq.Expressions.Expression<System.Func<NextORM.AliasTests.Order, TJoin, System.Boolean>>, NextORM.Generated.nextorm_alias_tests.Alias+BuyerMarker)",
        "NextORM.Generated.nextorm_alias_tests.AliasJoin_P1_A2_Buyer<NextORM.AliasTests.Order, TJoin> Join`1(NextORM.Core.EntityBuilder<NextORM.AliasTests.Order>, NextORM.Core.Cte<TJoin>, System.Linq.Expressions.Expression<System.Func<NextORM.AliasTests.Order, TJoin, System.Boolean>>, NextORM.Generated.nextorm_alias_tests.Alias+BuyerMarker)",
        "NextORM.Generated.nextorm_alias_tests.AliasJoin_P1_A2_Buyer<NextORM.AliasTests.Order, TJoin> Join`1(NextORM.Core.EntityBuilder<NextORM.AliasTests.Order>, NextORM.Core.EntityBuilder<TJoin>, System.Linq.Expressions.Expression<System.Func<NextORM.AliasTests.Order, TJoin, System.Boolean>>, NextORM.Generated.nextorm_alias_tests.Alias+BuyerMarker)",
        "NextORM.Generated.nextorm_alias_tests.AliasJoin_P1_A2_Buyer<NextORM.AliasTests.Order, TJoin> LeftJoin`1(NextORM.Core.EntityBuilder<NextORM.AliasTests.Order>, NextORM.Core.EntityBuilder<TJoin>, System.Linq.Expressions.Expression<System.Func<NextORM.AliasTests.Order, TJoin, System.Boolean>>, NextORM.Generated.nextorm_alias_tests.Alias+BuyerMarker)",
        "NextORM.Generated.nextorm_alias_tests.AliasJoin_P1_A2_Buyer<NextORM.AliasTests.Order, TJoin> OuterApply`1(NextORM.Core.EntityBuilder<NextORM.AliasTests.Order>, NextORM.Core.EntityBuilder<TJoin>, NextORM.Generated.nextorm_alias_tests.Alias+BuyerMarker)",
        "NextORM.Generated.nextorm_alias_tests.AliasJoin_P1_A2_Buyer<NextORM.AliasTests.Order, TJoin> RightJoin`1(NextORM.Core.EntityBuilder<NextORM.AliasTests.Order>, NextORM.Core.EntityBuilder<TJoin>, System.Linq.Expressions.Expression<System.Func<NextORM.AliasTests.Order, TJoin, System.Boolean>>, NextORM.Generated.nextorm_alias_tests.Alias+BuyerMarker)",
        "NextORM.Generated.nextorm_alias_tests.AliasJoin_P1_A2_Buyer<NextORM.AliasTests.Person, TJoin> Join`1(NextORM.Core.EntityBuilder<NextORM.AliasTests.Person>, NextORM.Core.EntityBuilder<TJoin>, System.Linq.Expressions.Expression<System.Func<NextORM.AliasTests.Person, TJoin, System.Boolean>>, NextORM.Generated.nextorm_alias_tests.Alias+BuyerMarker)",
        "NextORM.Generated.nextorm_alias_tests.AliasJoin_P1_A2_Buyer_A3_Approver<NextORM.AliasTests.Order, NextORM.AliasTests.Person, TJoin> Join`1(NextORM.Generated.nextorm_alias_tests.AliasJoin_P1_A2_Buyer<NextORM.AliasTests.Order, NextORM.AliasTests.Person>, NextORM.Core.EntityBuilder<TJoin>, System.Linq.Expressions.Expression<System.Func<NextORM.Generated.nextorm_alias_tests.AliasProjection_P1_A2_Buyer<NextORM.AliasTests.Order, NextORM.AliasTests.Person>, TJoin, System.Boolean>>, NextORM.Generated.nextorm_alias_tests.Alias+ApproverMarker)",
    ];

    private static void AssertMarker(Type marker, string alias)
    {
        var markerType = marker.GetNestedType(alias + "Marker", BindingFlags.Public);
        markerType.Should().NotBeNull();
        markerType!.IsNestedPublic.Should().BeTrue();
        markerType.IsSealed.Should().BeTrue();

        var property = marker.GetProperty(alias, BindingFlags.Public | BindingFlags.Static);
        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(markerType);
        property.GetMethod!.IsStatic.Should().BeTrue();
    }

    private static void AssertPair(
        Type projection,
        Type builder,
        Type projectionBase,
        params (string Name, int Slot, Type Entity)[] aliases)
    {
        projection.IsPublic.Should().BeTrue();
        projection.IsAbstract.Should().BeFalse();
        projection.BaseType!.GetGenericTypeDefinition().Should().Be(projectionBase);

        builder.IsPublic.Should().BeTrue();
        builder.BaseType!.GetGenericTypeDefinition().Should().Be(typeof(EntityBuilder<>));
        builder.BaseType.GetGenericArguments().Single().Should().Be(projection);

        foreach (var (name, slot, entity) in aliases)
        {
            var property = projection.GetProperty(name);
            property.Should().NotBeNull();
            property!.GetMethod!.IsStatic.Should().BeFalse();
            property.PropertyType.Should().Be(entity);

            var joinSlot = property.GetCustomAttribute<JoinSlotAttribute>();
            joinSlot.Should().NotBeNull();
            joinSlot!.Position.Should().Be(slot);
        }
    }
}
