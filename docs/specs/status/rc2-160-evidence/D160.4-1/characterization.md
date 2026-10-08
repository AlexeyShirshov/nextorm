# D160.4-1 — Defect A characterization: source position → JoinSlot → alias t-index

Unit `.4-1`, plan `r=4`, attempt `n=1/3`, contract `rv=5`, cycle 1. Tree `40b1a159bdf60289ebf5a01b9fffc36786cdd94d`
+branch `1.0.9-rc2` working-tree candidate. Generated code emitted with
`EmitCompilerGeneratedFiles=true` (raw dump: `join-alias-generated-red.g.cs`, build log `gen-red-build.log`).

## The single ordinal invariant

The generator assigns each chain position an ordinal `Slot` (root = 1, first join = 2, nth join = n+1) in
`BuildSchema` (`JoinAliasGenerator.cs`): `members.Add(new AliasMember(step.Alias, slot))` with `slot = i + 2`.
That one stored ordinal must drive BOTH:

- the projection parameter used for the member type — `public T{slot} {Alias}`;
- the runtime slot attribute — `[JoinSlot(slot)]`.

The runtime resolves an alias expression member to a table alias as
`ProjectionAliasCache.GetMemberPosition` → `JoinSlotAttribute.Position - 1` → `GetOccurrence` →
`ResolveProjectionItemAliasIndex` → `AliasProvider.FindAlias` → `t{position}`
(`AliasFromProjectionVisitor.ResolveAliasCore`, `MemberProjectionVisitor` `MemberTranslator.cs:475-494`).
`CreateJoined`/`CreateAliasJoined` append joins in chain order, so the runtime table alias for chain
ordinal `N` is always `tN`. Therefore `[JoinSlot(N)]` and `public TN` MUST carry the same `N`. The
attribute must never be derived from the member name or the CLR type.

The dirty-tree candidate emitted `[JoinSlot(member.Slot + 1)]` while keeping `public T{member.Slot}` — a
second, divergent ordinal. That is the Defect A root cause.

## Table

`Slot` = the one generator ordinal / runtime table alias index (`t{Slot}`). `red attr` is what the dirty
candidate emitted (`Slot + 1`); `correct attr` is the reconciled value.

| # | source position / chain | generated type | alias member | member type | correct attr | red attr | runtime t-index |
|---|---|---|---|---|---|---|---|
| 1 | root `.WithAlias(Alias.Root)` | `AliasProjection_A1_Root<T1>` | `Root` | `T1` | `JoinSlot(1)` | `JoinSlot(2)` | `t1` |
| 2 | positional→alias (`simple.Join(c).Join(c, Alias.Approver)`) | `AliasProjection_P1_P2_A3_Approver<T1,T2,T3>` | `Approver` | `T3` | `JoinSlot(3)` | `JoinSlot(4)` | `t3` |
| 3 | alias→positional→alias (`Alias.Buyer`, pos, `Alias.Approver`) | `AliasProjection_P1_A2_Buyer_P3_A4_Approver<T1,T2,T3,T4>` | `Buyer` / `Approver` | `T2` / `T4` | `JoinSlot(2)` / `JoinSlot(4)` | `JoinSlot(3)` / `JoinSlot(5)` | `t2` / `t4` |
| 4 | repeated CLR type (two `ComplexEntity` aliased joins) | `AliasProjection_P1_A2_Buyer_A3_Approver<T1,T2,T3>` | `Buyer` / `Approver` | `T2` / `T3` | `JoinSlot(2)` / `JoinSlot(3)` | `JoinSlot(3)` / `JoinSlot(4)` | `t2` / `t3` |
| 5 | `Buyer2` in slot 3 (positional prefix then alias) | `AliasProjection_P1_P2_A3_Buyer2<T1,T2,T3>` | `Buyer2` | `T3` | `JoinSlot(3)` | `JoinSlot(4)` | `t3` |
| 6 | correlated APPLY alias (single-entity receiver) | `AliasProjection_P1_A2_Applied<T1,T2>` | `Applied` | `T2` | `JoinSlot(2)` | `JoinSlot(3)` | `t2` |
| 6b | correlated APPLY alias after an aliased join | `AliasProjection_P1_A2_Buyer_A3_Applied<T1,T2,T3>` | `Buyer` / `Applied` | `T2` / `T3` | `JoinSlot(2)` / `JoinSlot(3)` | `JoinSlot(3)` / `JoinSlot(4)` | `t2` / `t3` |

## Observed red (provider SQL, postgres representative, `S-postgres.log`)

- `Repeated_clr_type_resolves_to_distinct_slots`: `First` (slot 2) rendered `select t3.id` — the
  `+1` attribute resolved the first alias to the second joined table.
- `Alternating_alias_positional_alias_assigns_slots_in_chain_order`: `Second` (slot 4) resolved out of
  range (`GetOccurrence` → null) and fell back to the first same-type occurrence → `select t2.id`
  instead of `t4`.
- `Mixed_positional_then_alias_join_matches_the_positional_chain`: `Approver` (slot 3) → `select t2.id`
  instead of `t3`.

## Reconciliation

Fix: emit `[JoinSlot(member.Slot)]` (the same stored ordinal as `T{member.Slot}`); remove the `+1`.
No change to the runtime or to `IsItemAlias` agreement/rejection sites is required — they already use no
member-name-derived ordinal after the candidate's `IsItemAlias(name)` (rejects the whole `ItemN` shape)
and correlated-prefix changes. Corrected dump: `join-alias-generated-green.g.cs`.
