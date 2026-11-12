# D370 PDCA process defect: counter drift

- Stable key: `T370-PD-S1-COUNTERS`.
- Symptom: campaign header `n=0` contradicted Step S1/progress `n=1`; the counter incorrectly appeared reset despite the attempt already being recorded.
- Root cause: state was written by two different dispatches without a read-back reconcile.
- Effect: none on the result; risk that dispatch counters diverge and subsequent work is accounted against the wrong attempt.
- Evidence: `.pdca/status/T370.md` campaign header `Current step` (formerly line 25, `n=0`); Step S1 `Current state` line 138 (`n=1`); progress lines 219–222 (`n=1`).
- Prevention: single writer reconciles header and step counters from one read-back before every dispatch.


## T370-S2-RECORD-DRIFT

- Stable key: `T370-S2-RECORD-DRIFT`.
- Symptom: campaign header retained `N=1` and the S2 defect history was empty while S2 was in cycle N=2, attempt n=1, with five CHECK-confirmed/unverified defect findings.
- Root cause: status updates were appended to the step record without reconciling the campaign header and defect ledger against the latest CHECK report.
- Effect: the durable dispatch checkpoint and defect recurrence history could misstate current accounting and obscure outstanding obligations; no attempt increment is authorized.
- Evidence: `.pdca/status/T370.md` campaign state (previously line 25), Step S2 current state and CHECK report; CHECK report dated/persisted in this evidence round.
- Prevention: before every dispatch, read back and reconcile campaign N/step/r/n, defect keys and states from the current report; append exact CHECK source and progress entry in the same status write.

## T370-S3-PLAN-AUTHORITY

- Stable key: `T370-S3-PLAN-AUTHORITY`.
- Symptom: the S3 boundary decision was issued by the primary session instead of `pdca-planner`, after three consecutive planner dispatches failed at the endpoint with `Timed out waiting for response headers`; the same driver-added scope (the CSV byte[] detector) was not routed back through a re-plan.
- Root cause: the driver treated an infrastructure failure of a decision role as a transfer of that role's authority. The campaign's own S1 record already carried the same fallback (`Notice: autonomous driver fallback`), so the practice had precedent without a recorded rule change.
- Effect: the S3 order of operations was correct in substance but unauthorized in form; CHECK refused the candidate on authority grounds even though the numeric increment (−16) was met. One extra revision (r2) is required to re-establish the decision.
- Evidence: `.pdca/status/T370.md` §S3 PLAN (three timeout dispatches), §S3 CHECK record (T370-S3-PLAN-AUTHORITY), `.pdca/status/T370.md` Blockers line (S1 fallback).
- Prevention: a decision role's endpoint failure is an infrastructure blocker, not a delegation. The driver may gather facts and prepare the brief, but the boundary/criteria decision must be retried through the configured role (longer timeout, smaller brief) or the campaign must stop with a blocker; a driver-issued decision is only legitimate when the campaign record already carries an explicit, user-approved rule for that fallback.

## T370-S3-COMPATIBILITY

- Stable key: `T370-S3-COMPATIBILITY`.
- Symptom: the increment changed the observable result of the public `SelectExpression.GetDataRecordMethod()` (closed generic `GetFieldValue` `MethodInfo` → non-generic static reader) with only a decision record written after the fact; the campaign requires public compatibility "unless a design is explicitly approved first".
- Root cause: "recorded in a design note" was treated as equivalent to "approved by the plan before implementation"; the compatibility gate was read as a documentation obligation instead of an approval gate.
- Effect: CHECK marked S3-C3 unverified and routed the step to PLAN; the change may be sound, but its authorization is missing.
- Evidence: `.pdca/status/T370.md` §S3 CHECK record (T370-S3-COMPATIBILITY); `docs/specs/design/D370-stream-a-reader-accessors.md`; `src/nextorm.core/Expressions/SelectExpression.cs` `GetDataRecordMethod()`/`ByteArrayFieldValueReader`.
- Prevention: before DO, enumerate every public member whose behaviour the increment changes and list it in the PLAN record as an approval item with its intended new contract; PLAN must authorize it explicitly, and CHECK must be handed that authorization, not a design note written in DO.

## T370-S5-INSTRUMENT-DENOMINATORS

- Stable key: `T370-S5-INSTRUMENT-DENOMINATORS`.
- Symptom: the S5 planner return stated the probe instrument as 11 / 11 / 10 = 32 cells; the probes declare 10 / 11 / 9 = 30.
- Root cause: the boundary brief described the provider queries by class rather than restating the probe declaration; the planner inferred totals from the earlier baseline fractions.
- Effect: every criterion denominator in the first S5 return was wrong; a second planner call was needed before the step could be judged.
- Evidence: `.pdca/status/T370.md` §Step S5 `### r1` (first totals and the restated criteria); the three probe `Program.cs` query arrays.
- Prevention: the boundary brief must carry the exact per-provider cell count from the instrument source, not a fraction or a class summary; the planner's return is rejected without it.

## T370-S5-FORECAST-CRITERION

- Stable key: `T370-S5-FORECAST-CRITERION`.
- Symptom: `S5-C3` prescribed "24/30 OK + 6/30 fail-closed" — a prediction of which cells would fail — and the real fix made all 30 pass, so the frozen criterion could not be met by a strictly better result.
- Root cause: the defect distribution was written into the acceptance criterion instead of being used as the expected outcome.
- Effect: CHECK r1 refused the step on a criteria defect; an extra PLAN revision was needed.
- Evidence: `.pdca/status/T370.md` §Step S5 `CHECK record (r1)` finding `T370-S5-CRITERIA`; `### r2` for the corrected wording.
- Prevention: criteria state the required property ("every supported shape executes natively with exact JIT equivalence; unsupported shapes fail closed deterministically"), never the count of expected failures.

## T370-S5-EVIDENCE-PACKAGING

- Stable key: `T370-S5-EVIDENCE-PACKAGING`.
- Symptom: the semantic-oracle copy was deleted after its run, so CHECK could not inspect whether the canonicalisation preserved query semantics and row multiplicity; it requested a consolidated evidence round.
- Root cause: the evidence round treated a scratch tool as disposable instead of as part of the evidence.
- Effect: one additional evidence round (not a counted product attempt) and a second CHECK on the same candidate.
- Evidence: `.pdca/status/T370.md` §Step S5 `CHECK record (r2)` finding `T370-S5-SEMANTICS`; `candidate/oracle-clickhouse/` for the preserved source.
- Prevention: any scratch instrument whose output supports a criterion is preserved with its exact diff, its commands and a README under the evidence root before the run is deleted.

## T370-S5-RECORD-POINTERS

- Stable key: `T370-S5-RECORD-POINTERS`.
- Symptom: the dispatch cited the registry under `attempts/` although it lives at the evidence root, the suppression scan's `^\+` filter missed the new file's declaration, and the status kept a `pending` CHECK marker after the report was persisted.
- Root cause: pointers were written from memory and the scan assumed every change was inside a tracked diff hunk.
- Effect: three minor CHECK findings; no effect on the verdict after correction.
- Evidence: `.pdca/status/T370.md` §Step S5 `CHECK record (r2)` finding `T370-S5-RECORD`; `suites-s5.md` corrected pointers.
- Prevention: evidence pointers are copied from the produced path, scans enumerate changed *and new* files, and a persisted report replaces its `pending` marker in the same write.

## T370-S7-RECORD

- Stable key: `T370-S7-RECORD`.
- Symptom: the S7 CHECK report was dispatched and answered but the status section still said `pending` while the next dispatch cited it as persisted.
- Root cause: the report was held in the conversation and not written until after the following dispatch.
- Effect: one minor CHECK finding; no effect on the verdict after reconciliation.
- Evidence: `.pdca/status/T370.md` §Step S7 `CHECK record (r1)` (previously `pending`).
- Prevention: persist every CHECK report in the same write that advances the step's phase, before dispatching the next role.
