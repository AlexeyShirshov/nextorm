using System.Linq.Expressions;
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
        // #324: the correlated-APPLY coverage adds the after-join alias receiver Root3/Buyer3 with its
        // positional continuation (A1_Root3, A1_Root3_P2, A1_Root3_A2_Buyer3, A1_Root3_A2_Buyer3_P3) and
        // the marker APPLY schema A1_Root_A2_Buyer_A3_Approver (one schema serves both kinds and both
        // lambda forms, so the four transitions do not multiply the frozen surface).
        // #206: Alias.Target is reused by two independent queries with different joined types
        // (Person/Product); the last-step joined type is the method type parameter, so the two chains
        // collapse into the single AliasJoin_P1_A2_Target pair added here.
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
            "AliasJoin_A1_Root3_A2_Buyer3_P3`3",
            "AliasJoin_A1_Root3_A2_Buyer3`2",
            "AliasJoin_A1_Root3_P2`2",
            "AliasJoin_A1_Root3`1",
            "AliasJoin_A1_Root_A2_Buyer_A3_Approver`3",
            "AliasJoin_A1_Root_A2_Buyer`2",
            "AliasJoin_A1_Root_P2`2",
            "AliasJoin_A1_Root`1",
            "AliasJoin_P1_A2_Buyer2`2",
            "AliasJoin_P1_A2_Buyer_A3_Approver`3",
            "AliasJoin_P1_A2_Buyer_P3_A4_Approver`4",
            "AliasJoin_P1_A2_Buyer_P3`3",
            "AliasJoin_P1_A2_Buyer`2",
            "AliasJoin_P1_A2_StoredBuyer_P3`3",
            "AliasJoin_P1_A2_StoredBuyer`2",
            "AliasJoin_P1_A2_Target`2",
            "AliasJoin_P1_P2_A3_Approver`3",
            "AliasJoin_P1_P2_A3_Buyer2`3",
            "AliasProjection_A1_Root3_A2_Buyer3_P3`3",
            "AliasProjection_A1_Root3_A2_Buyer3`2",
            "AliasProjection_A1_Root3_P2`2",
            "AliasProjection_A1_Root3`1",
            "AliasProjection_A1_Root_A2_Buyer_A3_Approver`3",
            "AliasProjection_A1_Root_A2_Buyer`2",
            "AliasProjection_A1_Root_P2`2",
            "AliasProjection_A1_Root`1",
            "AliasProjection_P1_A2_Buyer2`2",
            "AliasProjection_P1_A2_Buyer_A3_Approver`3",
            "AliasProjection_P1_A2_Buyer_P3_A4_Approver`4",
            "AliasProjection_P1_A2_Buyer_P3`3",
            "AliasProjection_P1_A2_Buyer`2",
            "AliasProjection_P1_A2_StoredBuyer_P3`3",
            "AliasProjection_P1_A2_StoredBuyer`2",
            "AliasProjection_P1_A2_Target`2",
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

            // Two forms share an operator name: the source-explicit form ends in the marker, while the
            // source-less form (#322) ends in the optional Action<JoinOptions> with the marker before it.
            overloads.Should().OnlyContain(method =>
                IsSourceExplicitAliasForm(method) || IsSourceLessAliasForm(method));
            overloads.Should().Contain(
                method => IsSourceExplicitAliasForm(method),
                $"{joinOperator} must keep its source-explicit marker-last overload");
        }
    }

    [Fact]
    public void Source_explicit_alias_join_overloads_stay_option_less()
    {
        // C3 dismissal (issue #160): the source-explicit alias extension surface deliberately has no
        // per-join `Action<JoinOptions>` overload. A caller writing `.Join(inner, condition, configure,
        // Alias.X)` must not bind a generated alias overload that silently accepts (and drops) the
        // options; the options-taking overloads are the generated positional `new` instance transitions,
        // which carry no marker and therefore cannot be mistaken for an alias step. Pin that no
        // marker-last (source-explicit) join extension carries an `Action<JoinOptions>`, and specifically
        // not the 4-argument `(inner, condition, Action<JoinOptions>, marker)` form. Frozen per form:
        // only the separate source-less form (#322) accepts options, and it puts them after the marker.
        var extensions = typeof(Alias).Assembly.GetType(GeneratedNamespace + ".JoinAliasExtensions")!;
        var optionsType = typeof(Action<JoinOptions>);

        var methods = extensions.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => JoinOperators.Contains(method.Name))
            .Where(IsSourceExplicitAliasForm)
            .ToArray();

        methods.Should().NotBeEmpty();
        methods.Should().OnlyContain(method =>
            method.GetParameters().Last().ParameterType.Name.EndsWith("Marker", StringComparison.Ordinal));

        // `this` is the first reflected parameter, so the explicit call arguments are parameters 1..end.
        var fourArgumentOptionsForm = methods.Where(method =>
        {
            var explicitParameters = method.GetParameters().Skip(1).ToArray();
            return explicitParameters.Length == 4
                && explicitParameters[2].ParameterType == optionsType
                && explicitParameters[3].ParameterType.Name.EndsWith("Marker", StringComparison.Ordinal);
        }).ToArray();

        fourArgumentOptionsForm.Should().BeEmpty(
            "a source-explicit alias `.Join(inner, condition, Action<JoinOptions>, marker)` overload must not exist (C3)");

        // Stronger and direct: the source-explicit form never takes an `Action<JoinOptions>` at all.
        methods.SelectMany(method => method.GetParameters())
            .Should().NotContain(parameter => parameter.ParameterType == optionsType);
    }

    [Fact]
    public void Source_less_alias_join_overloads_take_optional_options_after_the_marker()
    {
        // #322 freeze, form-specific counterpart of the C3 dismissal: the source-less form has no source
        // parameter at all (the implicit `From<TJoin>()` lives in the core seam) and its signature ends in
        // `marker, Action<JoinOptions> options = null`. The exact explicit-generic call shape is compiled
        // through `DeclareSourceLessCallShapes`, which is bound but never executed.
        Action<EntityBuilder<Order>> binder = DeclareSourceLessCallShapes;
        binder.Should().NotBeNull("the source-less call shapes must compile against the emitted surface");

        var extensions = typeof(Alias).Assembly.GetType(GeneratedNamespace + ".JoinAliasExtensions")!;
        var optionsType = typeof(Action<JoinOptions>);

        var methods = extensions.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => JoinOperators.Contains(method.Name))
            .Where(IsSourceLessAliasForm)
            .ToArray();

        methods.Should().NotBeEmpty("the source-less form must emit join overloads");

        foreach (var joinOperator in new[] { "Join", "LeftJoin", "RightJoin", "FullJoin" })
        {
            methods.Should().Contain(
                method => method.Name == joinOperator,
                $"the source-less form must cover the conditional operator {joinOperator}");
        }

        // No source argument: `this, condition, marker, options` and nothing else.
        methods.Should().OnlyContain(method => method.GetParameters().Length == 4);
        methods.Should().OnlyContain(method =>
            method.GetParameters()[1].ParameterType.IsGenericType
            && method.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>));
        methods.Should().OnlyContain(method =>
            method.GetParameters()[2].ParameterType.Name.EndsWith("Marker", StringComparison.Ordinal));
        methods.Should().OnlyContain(method =>
            method.GetParameters()[3].ParameterType == optionsType
            && method.GetParameters()[3].IsOptional
            && method.GetParameters()[3].DefaultValue == null);
    }

    /// <summary>
    /// Compile-only declaration of the #322 source-less call shapes. It is bound (never invoked) by
    /// <see cref="Source_less_alias_join_overloads_take_optional_options_after_the_marker"/> so the
    /// compiler must resolve the exact explicit-generic form against the emitted extensions: marker-only
    /// and marker-with-options, for every conditional operator, without a source argument.
    /// </summary>
    private static void DeclareSourceLessCallShapes(EntityBuilder<Order> orders)
    {
        _ = orders.Join<Person>((l, r) => l.Id == r.Id, Alias.Buyer);
        _ = orders.Join<Person>((l, r) => l.Id == r.Id, Alias.Buyer, options => options.WithJoinHint("loop"));
        _ = orders.LeftJoin<Person>((l, r) => l.Id == r.Id, Alias.Buyer);
        _ = orders.RightJoin<Person>((l, r) => l.Id == r.Id, Alias.Buyer);
        _ = orders.FullJoin<Person>((l, r) => l.Id == r.Id, Alias.Buyer, null);
    }

    /// <summary>The shape of a source-explicit alias extension: the last parameter is the alias marker.</summary>
    private static bool IsSourceExplicitAliasForm(MethodInfo method)
        => method.GetParameters().Last().ParameterType.Name.EndsWith("Marker", StringComparison.Ordinal);

    /// <summary>
    /// The shape of a source-less alias extension (#322): `this receiver, condition, marker, options`,
    /// i.e. no source parameter and the optional options after the marker.
    /// </summary>
    private static bool IsSourceLessAliasForm(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return parameters.Length == 4
            && parameters[^1].ParameterType == typeof(Action<JoinOptions>)
            && parameters[^2].ParameterType.Name.EndsWith("Marker", StringComparison.Ordinal);
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
