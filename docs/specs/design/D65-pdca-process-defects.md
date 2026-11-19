# D65 — PDCA process defects

This record captures process defects observed while carrying out the `issues-65-69`
campaign. It is separate from product defects and does not change the campaign contract.

## R65-69-PD1 — PLAN report persistence

- **Symptom:** a PLAN report was initially embedded in campaign status rather than persisted as a standalone complete source artifact, leaving its exact identity/read-back ambiguous.
- **Root cause:** source-report persistence and the status-file reference were conflated instead of maintaining a separately addressable immutable report.
- **Effect:** delayed exact report reconciliation and required a persistence/re-gather action; no product execution was credited for that correction.
- **Evidence:** `.pdca/status/issues-65-69.md`, Defect history entry R65-69-PD1; immutable PLAN sources `.pdca/status/issues-65-69-plan-r1.md` and `.pdca/status/issues-65-69-cycle-N2-plan.md`.
- **Prevention:** persist each planner return verbatim as a uniquely identified immutable artifact and store the exact path and integrity identity/size in status before subsequent phases.

## R65-69-PD2 — CHECK criteria drift

- **Symptom:** invented B-2/B-4 criteria appeared in a CHECK brief although they were not part of the approved plan.
- **Root cause:** the brief relied on an informal criteria summary instead of the exact plan AC identifiers and source text.
- **Effect:** CHECK scope was contaminated and required reconciliation; no invented criterion is part of the accepted set.
- **Evidence:** `.pdca/status/issues-65-69.md`, Defect history entry R65-69-PD2 and its recorded reconciliation event.
- **Prevention:** each CHECK dispatch must reproduce/reference the actual AC set by exact plan identity and reject IDs absent from that plan.

## R65-69-DML1 — MappingScope omitted from DML paths

- **Symptom:** scoped UPDATE initially ignored tenant schema/table/column remaps and parameter prefixes across the five dialects; N=1 follow-up also identified INSERT/DELETE and remaining MERGE/TRUNCATE gaps.
- **Root cause:** scope propagation had been integrated incrementally across mutation builders, but the coverage boundary did not include every mutation entry point and provider execution path before the candidate was treated as broad evidence.
- **Effect:** a corrective implementation fix was needed; earlier green tests did not prove the complete DML contract, leaving the candidate incomplete and requiring a remainder cycle N=2.
- **Evidence:** `.pdca/status/issues-65-69.md`, Defect history entry R65-69-DML1; `.pdca/issues-65-69/rv1/attempts/1/continuation/mapping-scope-dml-focused-class-xunit.log`; the exact N=2 scope is `.pdca/status/issues-65-69-cycle-N2-plan.md` §8.
- **Prevention:** maintain an entry-point matrix for INSERT/UPDATE/DELETE/MERGE/TRUNCATE and source variants; add scope assertions for table, schema, columns, parameter naming and cache isolation before reporting DML coverage complete.

## R65-69-EVIDENCE-TOOLING-01 — non-portable live evidence fixture during DO

- **Symptom:** an attempt-2 shared MappingScope live test was added and iterated as candidate evidence tooling. Its final provider batch selected one case and failed on five of six providers; ClickHouse selected zero. The test did not converge and was removed without changing product sources.
- **Root cause:** evidence tooling was treated as product work without an upfront provider-neutral fixture design or a literal check that the criterion required per-provider live scoped parity.
- **Effect:** N5/r1/n2 was consumed by fixture diagnosis and non-green provider runs; those runs remain diagnostic history and do not establish MappingScope live parity. The canonical capped suite was not rerun because it already passed against the same product sources before the temporary test file existed.
- **Evidence:** `.pdca/status/issues-65-69.md`, N5 remainder cycle / Defect history; `.pdca/issues-65-69/rv5/attempts/2/mappingscope-live-{sqlite,postgres,sqlserver,mysql,clickhouse,mariadb}-focused.log`; `.pdca/issues-65-69/rv5/attempts/2/revert-attempt2-live-fixture.log`; `.pdca/issues-65-69/rv5/attempts/1/canonical-capped-full-suite.log`.
- **Prevention:** read the criterion literally before adding an evidence test; where provider portability is truly required, design and validate provider-neutral fixtures up front, then run a selector sanity check with nonzero cases before expanding the matrix.

## R65-69-PD3 — incorrect failing-test identity in N3 DO report

- **Symptom:** the N3 attempt-3 DO report names `ClickHouseImplicitNavigationTests.Wide_count_boundary…` as the failing test, while the cited canonical full-suite transcript identifies `ClickHouseIntegrationTests.SequenceMatch_ShouldMatchPattern`.
- **Root cause:** the DO report's named test identity was not reconciled against the authoritative failure line in the canonical transcript before the report was returned.
- **Effect:** the failure could be triaged against the wrong test; an append-only correction was required. No additional attempt, product change, or criterion change was authorized by the correction.
- **Evidence:** `.pdca/issues-65-69/rv3/attempts/3/do-result.md:16`; `.pdca/issues-65-69/rv3/attempts/3/canonical-capped-full-suite-final.log`; `.pdca/issues-65-69/rv3/attempts/3/canonical-full-suite-inner.log`; `.pdca/status/issues-65-69.md`, Defect history R65-69-PD3.
- **Prevention:** before returning a DO report, reconcile each reported failing identity (namespace, type, method) to the exact authoritative run transcript and class-count audit; preserve any later correction append-only.

## R65-69-VALIDATOR-STATUS-01 — manifest outcome/schema mismatch

- **Symptom:** `validate_inner_loop.py manifest` rejected truthful `status: not_met` rows because the schema permits only `met`, `open`, or `na`; the N5 manifest had to retain the semantic outcome separately as `ac_status: not_met` and use `status: open` with a populated reason/evidence package.
- **Root cause:** the manifest's execution/schema status vocabulary does not model the distinct semantic criterion outcome `not_met`; the validator checks the former vocabulary.
- **Effect:** the first truthful not-met manifest failed validation (exit 2), and a schema adapter was needed to keep the criterion result honest while producing valid evidence. The helper itself was not modified; the adapter is not a validator fix.
- **Evidence:** `.pdca/issues-65-69/rv5/attempts/1/manifest-inner-loop-validator.log` (exit 2); `.pdca/issues-65-69/rv5/attempts/2/final-inner-loop-manifest-validator.log`; `.pdca/issues-65-69/rv5/attempts/2/final-evidence-lint.log`; `.pdca/status/issues-65-69.md`, Defect history R65-69-VALIDATOR-STATUS-01.
- **Prevention:** make the schema explicitly encode criterion outcomes including `not_met`, or maintain a documented mapping between schema status and semantic status. Until that change is authorized, preserve the exact semantic outcome in a separate field with its reason and evidence; never turn it into an empty/open placeholder.

