# T206 rv2 — R01/R02 remain GAP

Status: R01/R02 stay **GAP (open)**. They are unreachable by construction on HEAD `a60ecb84`;
this is a static proof from the current tree, **not** a re-run of the old unsat collision fixture.

## What R01/R02 asked
A reachable pair of individually-valid supported alias chains that render the **same generated
extension signature with different bodies**, proving via `GeneratorDriver` that the pre-guard output
would be a `CS0111` while the guarded output is not, and that `NORMGEN007` is reported (with a real
invocation location rather than `Location.None`).

## Proof: equal signature ⇒ byte-identical body
`JoinAliasGenerator.cs` (current tree, 1363 lines):

- Extension emission loop / collision guard: `AppendExtensions` `:1049-1070`.
  - `:1055` renders the method and returns its signature;
  - `:1058-1066` reports `NORMGEN007` **only** when `seenSignatures` already holds the same
    signature string with a *different* method text (`:1062`, `Location.None`); identical text is
    collapsed (`continue`).
- One method render: `RenderAliasExtension` `:1125-1191`.
  - Body text inputs are `returnType`, `receiverType`, `sourceParameterType`, `conditionParameterType`,
    `markerType`, `joinType`, `step.Operator`, `step.Conditionless` (`:1168-1186`).
  - `:1155` builds `returnArguments = concreteArguments + { "TJoin" }` — i.e. the **last step's
    `JoinedType` is replaced by the method type parameter `TJoin`** and never enters the body.
  - `:1188-1190` returns the signature, which is composed of exactly `step.Operator`, `receiverType`,
    `sourceParameterType`, `conditionParameterType`, `markerType` and `returnType`.
  - `joinType` is a pure function of `step.Operator` (`JoinTypeName` `:1194-1204`); `Conditionless`
    only toggles the condition parameter/argument, which is itself reflected in the signature (the
    `conditionParameterType` component is present iff `!Conditionless`).

Therefore every body input is a component (or a deterministic function of a component, or absent
exactly when the corresponding signature component is absent) of the returned signature string:
**equal signature ⇒ byte-identical body**. The `NORMGEN007` branch at `:1060-1063` cannot be reached
by any pair of supported chains, so R01 (reachable same-signature/different-body fixture) and R02
(location of the report) have no constructible witness. This matches issue #206's owner note: "No
reachable reproducer for this class is known today; it is guarded, not fixed."

## What rv2 does instead
- Demonstrates R03 (byte-identical duplicate collapse + intermediate `IsCte`) and R07
  (order-independent, repeatable generation) with supported syntax only.
- Does **not** add or attempt any last-step different-body `CS0111` collision fixture (that is GAP).

## R01/R02 disposition
Remain **GAP**. Satisfying them requires making the last-step `JoinedType` participate in the emitted
identity (signature-disambiguating API change) — a Deferred design alternative, out of T206 rv2 scope.
Evidence: `recon.log` (all four executions green), `D1D2-evidence.json`, `baseline.txt`.

## Residual R05 obligation — no demonstrated workaround
R05 as written requires the EN/RU joins guides to describe NORMGEN007, its limitation, **and a
verified workaround**. rv2 discharges only the first two: `docs/guide/02-joins.md` and
`docs/ru/guide/02-joins.md` document NORMGEN007 as a defensive guard and state that diverging
last-step projections are not independently supported. **No verified workaround exists** — because
the collision branch is unreachable by construction (purity proof above), there is no diagnostic
scenario for which a remediation could be demonstrated, and inventing one would violate the R05
negative ("promising an unverified workaround" fails). The workaround sub-obligation is therefore
retained **open** as part of the R05/#206 gap, not silently dropped; it closes only if a committed
supported-syntax reproducer (or an approved identity-changing design) invalidates the purity proof
and yields a reachable collision with an observable remediation.

## Ratified disposition (r=2)
R01, R02, and the R05 workaround sub-obligation are **GAP-OPEN**, retained in #206 under milestone
`1.0.9-rc2`; R03, R04, R06, R07 are candidate-MET with rv2 evidence. R05 documentation (limitation)
is met; the demonstrated-workaround part stays open.
