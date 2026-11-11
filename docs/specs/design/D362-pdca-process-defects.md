# D362 — PDCA process defects

Internal record of **process** defects observed while running the D362 `pdca` cycle. Product defects are
not listed here (they belong to GitHub issues); every entry below is a defect of the cycle's own evidence,
records or decisions. This is not public documentation and is not linked from `docs/**`.

---

## PD1 — `DO-EFFORT-STOP`: DO stopped for effort with an inadmissible cause

- **Symptom:** a DO turn returned `DO → ACT` because the remaining unit was judged too large/effortful;
  the size/effort estimate was presented as the blocker instead of missing data or a contract conflict.
- **Root cause:** the executor treated its own effort budget as a contract fact. The base policy admits no
  "too much work" stop in autonomous mode (there is no task cutoff), and the plan had already been
  approved by the driver.
- **Effect:** one DO turn produced no contract progress, required a driver ruling and left a withdrawn
  ACT record in the append-only history — an extra turn and an extra CHECK/ACT bookkeeping cycle.
- **Evidence:** status `## ACT record` (line 278): "The earlier DO→ACT entry was withdrawn on driver ruling
  (effort/size is not an admissible cause)"; the corresponding progress-log entry.
- **Prevention:** an executor reports only (a) missing data, (b) a contract contradiction, or (c) a
  resource blocker. Size, token count or elapsed time are accounting, never a stop cause. The ACT record
  must name an admissible cause before the outcome changes.

## PD2 — `COVERAGE-INVALID-ENV`: an environment-broken coverage number used for a systemic conclusion

- **Symptom:** the D10d integration coverage run recorded **4015 container connection failures** across
  every provider and produced aggregate **branch 71.5 %**. D10e classified the failures as environmental
  but still declared the number "valid coverage evidence"; D10f then derived a "the 75 % branch gate is
  systemic" conclusion from it.
- **Root cause:** a measurement was accepted as evidence without first checking that every provider
  actually executed. Environmental failures were separated from the *test verdict* but not from the
  *number*.
- **Effect:** a wrong "systemic finding" reached CHECK; the coverage unit had to be re-measured (D10g,
  clean run: **line 81 % / branch 75.3 % = 40041/53166, both PASS**) and the earlier analysis had to be
  marked superseded. One extra unit cycle and a CHECK finding.
- **Evidence:** status rows D10d/D10e/D10f/D10g (lines 250–253);
  `rv1/coverage/integration/invalid-broken-env/`; `rv1/coverage/integration/report/Summary.txt`;
  supersession banners in `rv1/unit-evidence.md` (D10e/D10f) and
  `rv1/coverage/integration/gap-analysis.md`.
- **Prevention:** a coverage run is evidence only when the pre-warm and the run report **0 failed** and
  **0 container-failure lines**; otherwise the artifacts are marked invalid and re-run. Any conclusion
  built on a coverage number must state the run's failure count and the clean re-measure.

## PD3 — `DEFECT-HISTORY-NONE`: defect history left as `none` while repeated fixes were applied

- **Symptom:** `## Defect history` stayed `none` through D8–D8g (four allocation/perf fixes) and through
  the later P4/P5a defects; a reader could not see the repeated attempts or their outcomes.
- **Root cause:** the defect history was treated as an ACT-only field, so DO never opened an entry when a
  defect was discovered mid-unit; the field was only considered when a final verdict was written.
- **Effect:** CHECK could not reconstruct the fix history; P4's finding had to be retro-fitted and R3 had
  to rebuild the whole section from the unit rows and evidence logs.
- **Evidence:** status `## Defect history` (historical note above the list); rows D8–D8g (lines 238–245),
  P4 and P5a/P5b rows; `rv1/audit/oracle-map.md` AC6.
- **Prevention:** a defect found in DO gets a stable key entry **at discovery time** (symptom, first seen,
  units/attempts, fix count, evidence), updated on resolution. `none` is valid only when no defect was
  ever found in the cycle.

## PD4 — `PERF-MIXED-DATASETS`: performance artifacts mixed three datasets/configurations

- **Symptom:** the performance evidence reported three different numbers for the same method, taken from
  different runs/configurations; CHECK #1 recorded this as F2 ("perf artifacts use inconsistent noise
  datasets") and the verdict table could not be trusted.
- **Root cause:** numbers were copied between artifacts produced by different jobs, configurations and
  noise datasets instead of being computed once from a single frozen dataset with one stated formula.
- **Effect:** the performance verdict had to be recomputed from four consistent runs with a derived A/A
  band (P2a/P2b); a whole CHECK round was consumed by the inconsistency.
- **Evidence:** `rv1/perf-final/TABLES.md`, `rv1/perf-final/VERDICT.md`, CHECK #1 finding F2 in the status
  file.
- **Prevention:** one frozen protocol, one dataset, one formula. Every table names the run ids and derives
  the A/A band from those runs only; no number is carried over from an older artifact.

## PD5 — `WEAK-ORACLE-COMPLETE`: vacuous/weak test oracles recorded as completed units

- **Symptom:** units D5c/D5d/D5e marked AC3/AC4/AC6/AC7 as met on tests that asserted only
  `NotBeEmpty`/`ContainSingle`, a SQL substring, a generic `Exception`, or a query that did not exercise
  the named behaviour. Examples: the vacuous global-aggregate unit test, the dead
  `SelectGroupLiveTests` base, the "same-slot replacement" test that never applied the same slot twice.
- **Root cause:** "a test exists and is green" was accepted as "the criterion is verified"; the criterion
  text was not decomposed into named elements with a concrete oracle each.
- **Effect:** CHECK could not accept the ACs; T1/T2/T3 had to add exact expected rows, exact per-dialect
  SQL, concrete exception types/messages and behavioural assertions — three extra remediation units.
- **Evidence:** `rv1/audit/oracle-map.md` AC3/AC4/AC6/AC7 gap verdicts before T1–T3;
  `D362SelectGroupMatrixTests.GlobalAggregation_NoGroupBy_HasHaving` (vacuous before the fix);
  `SelectGroupLiveTests.cs` (dead before deletion); `D362CompositionTests.SameSlot_Replacement_NotDuplication`
  (vacuous before T3).
- **Prevention:** a criterion is met only when every named element has an oracle asserting an expected
  value, exception type/message or exact statement. Parity, self-consistency and "not empty" are not
  oracles; a test that cannot fail for the named behaviour is a defect, not a completed unit.
