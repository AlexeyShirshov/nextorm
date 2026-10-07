# E160-21 — generated `new` instance overload shadowing (R160-11, owner D:160-04)

- Task: D160 (issue #160), branch `1.0.9-rc2`, cycle r=2, attempt n=1/3, contract `rv=3`.
- Tree identity: `85d1b37193a483b84f0a521de7110d39c23db251` (`git rev-parse HEAD`).
- Requirement: the generated `new` instance transitions on the chain types (`AliasJoin_*`,
  root `AliasJoin_A1_X<T>`, and the pure-positional `JoinedEntityBuilder` prefix) must win over the
  inherited `EntityBuilder<TEntity>.Join`/`Apply` overloads; positive alias extensions must stay
  applicable for alias steps.
- Mechanism (source): `JoinAliasGenerator.RenderPositionalMethods`
  (`src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs:961-1013`) emits `public new <AliasJoin_*> <op><TJoin>(...)`
  on the alias receiver (`AppendTypes`, `:943-955`); root aliases emit the full seven-operator set at
  `:708-731`. A C# extension method cannot shadow an applicable instance method, hence the generated
  `new` instance transitions; the marker-taking alias extensions remain a distinct signature family.

## Commands (argument arrays) and results

Build (compiles the generated surface the assertions bind against):

| command | exit | notes |
|---|---|---|
| `["dotnet","build","tests/nextorm.alias.tests","-c","Debug"]` | 0 | 0 warnings / 0 errors → `alias-tests-build.log` |

Targeted filters (runner: Microsoft.Testing.Platform, `--filter "FullyQualifiedName~<Class>"`):

| `--filter` selector | exit | selected | passed | log |
|---|---|---|---|---|
| `FullyQualifiedName~AliasProjectionShapeTests` | 0 | 5 | 5 | `AliasProjectionShapeTests.log` |
| `FullyQualifiedName~AliasGeneratedSurfaceTests` | 0 | 5 | 5 | `AliasGeneratedSurfaceTests.log` |
| `FullyQualifiedName~JoinAliasGeneratorDiagnosticTests` | 0 | 17 | 17 | `JoinAliasGeneratorDiagnosticTests.log` |
| `FullyQualifiedName~RootAliasTests` | 0 | 13 | 13 | `RootAliasTests.log` |
| `FullyQualifiedName~MixedJoinChainTests` | 0 | 8 | 8 | `MixedJoinChainTests.log` |
| `FullyQualifiedName~Generated_new_instance_transitions` (new tests only) | 0 | 2 | 2 | `new-tests.log` |

Full invocation shape:
`dotnet test tests/nextorm.alias.tests -c Debug --no-build --filter "FullyQualifiedName~<Class>"`.

## Proving tests

Positive — generated `new` instance transitions win over the inherited overloads:

- `AliasProjectionShapeTests.Generated_new_instance_transitions_hide_the_inherited_positional_overloads`
  (new): reflection over the compiled generated types proves `AliasJoin_P1_A2_Buyer<Order,Person>` and
  `AliasJoin_A1_Root<Order>` declare public, non-static, hide-by-sig (`new`) instance methods that
  return `AliasJoin_*`; the inherited `EntityBuilder<>` overload with the same name exists on the base;
  the root receiver declares the full seven-operator set.
- `AliasProjectionShapeTests.Generated_new_instance_transitions_win_over_inherited_and_keep_alias_extensions_applicable`
  (new): compile-time assignment to the concrete generated return types is itself the binding proof —
  positional-after-alias → `AliasJoin_P1_A2_Buyer_P3<Order,Person,Person>`, positional-after-root →
  `AliasJoin_A1_Root_P2<Order,Person>`, alias-after-alias → `AliasJoin_P1_A2_Buyer_A3_Approver<…>`,
  alias-after-root → `AliasJoin_A1_Root_A2_Buyer<Order,Person>`, alias-after-`JoinedEntityBuilder` →
  `AliasJoin_P1_P2_A3_Buyer2<…>`. If the inherited `EntityBuilder<TEntity>.Join` were selected instead,
  the static type would be `JoinedEntityBuilder<…>` and none of these assignments would compile. Runtime
  assertions then check the routed slot values.
- `AliasProjectionShapeTests.Positional_join_after_alias_binds_the_generated_receiver` (existing):
  same compile-time proof for `AliasJoin_P1_A2_Buyer_P3<…>`.
- `RootAliasTests.Generated_root_alias_supports_a_positional_join_after_it` (existing): runtime
  positional step after `.WithAlias(Alias.Root)` routes through the generated root receiver.
- `MixedJoinChainTests.Alias_then_positional_join_keeps_slot_identity_and_distinct_sql_aliases`
  (existing): the routed positional-after-alias step maps to `t3` in generated SQL.

Negative — alias extension stays applicable for alias steps:

- `AliasGeneratedSurfaceTests.Generated_extension_class_exposes_the_seven_alias_operators` (existing):
  every generated alias extension operator exists, is static with `ExtensionAttribute`, and is
  marker-taking (so it cannot collide with the generated `new` positional instance methods).
- `AliasProjectionShapeTests.Generated_new_instance_transitions_…keep_alias_extensions_applicable`
  (new): alias bindings on all receivers above, including the pure-positional `JoinedEntityBuilder`
  prefix.
- `MixedJoinChainTests.Positional_then_alias_join_binds_the_alias_to_the_next_slot`,
  `MixedJoinChainTests.Alternating_alias_positional_alias_assigns_slots_in_chain_order`,
  `MixedJoinChainTests.Digit_ending_alias_at_a_later_slot_keeps_its_positional_slot` (existing).
- `RootAliasTests.Generated_root_alias_with_an_alias_join_maps_slot1_to_t1` (existing).

No product change was required: the shadowing mechanism is already emitted; the two new tests are the
missing generated-shape/binding proof and live inside the alias test footprint
(`tests/nextorm.alias.tests/AliasProjectionShapeTests.cs`).

## Artifacts

- `README.md` (this file)
- `alias-tests-build.log`, `AliasProjectionShapeTests.log`, `AliasGeneratedSurfaceTests.log`,
  `JoinAliasGeneratorDiagnosticTests.log`, `RootAliasTests.log`, `MixedJoinChainTests.log`,
  `new-tests.log`
