# Bounded controlled-cohort mutation check (issue #341, task T341 deliverable D1)

## What this is

`bounded-scope.py` is a repo-local, dependency-free (Python 3 stdlib only) mutation harness.
It applies a **hand-selected cohort of two string mutations** to one source file by byte offset,
rebuilds the scoped test project, runs one filtered test class, and reports whether each mutation
was killed or escaped.

It exists because the frozen Stryker pipeline for this repository could not produce a single killed
mutant even when the mutated literal is demonstrably covered. Instead of a mutation score, this
helper reports a **controlled-cohort detection rate** over a two-item cohort, in which one mutation
is *known* to be detectable (positive control) and one is *known* to be unreachable from the scoped
tests (escape control). It is a bounded, auditable substitute for the number Stryker failed to
produce — not a replacement for Stryker, and not a mutation score.

## Why: the Stryker defect this replaces

Recorded in this cycle, in chronological order:

1. **The whole-project attempt (T287 rv1)** ignored the declared three-file mutate scope twice,
   created **39 098** whole-project mutants and never reached a killed/survived classification; an
   earlier attempt in the same probe aborted after 640.6 s on a **15 249**-test whole-solution
   initial run (`.pdca/status/T287-1.md`). **T288** discovered **15 391** tests through the
   Microsoft.Testing.Platform runner (which ignores the filters it was given) and failed **3 697**
   test states (`.pdca/status/T288-1.md`). Both are whole-suite failures, not a scope defect of a
   correct config.
2. **The corrected scope (T341 rv1/rv2)** froze one source file and one test class, with
   `coverage-analysis: off` in rv2. Stryker reported `42053 mutants created` and
   `17 total mutants will be tested`; the final status totals were **0 Killed / 17 Survived /
   score 0.00 %**, with `Number of tests found: 12` agreeing with the TRX and with Roslyn. Six of
   those 17 mutants alter exactly the substrings the 12 tests assert
   (`tests/nextorm.postgres.tests/SqlGeneration/T287NullOrderingTests.cs:97,99,101,103`) and still
   survive.
3. **P3, the manual control (T341 rv2)** applied mutant id 10263's exact span
   (line 68, `" NULLS FIRST"` → `" NULLS LAST"`) by hand, with no Stryker in the path:
   **8 failed / 4 passed** of the 12 scoped tests, then **restored 12/12**. The rebuilt
   `nextorm.core.dll` had sha256 `1b6323ea…`, and the wrapper rebuilds the byte-identical assembly
   for the same edit today — so the mutation, the source line and the test detection are all sound.
4. **Attribution stays unresolved.** The *assembly substitution* inside the pipeline is evidenced
   (P3 produced a different, test-failing core assembly from the same bytes); *mutant activation*
   inside Stryker is **not** evidenced; no upstream issue is confirmed, and none is claimed here.
   T341 does not assert that Stryker is broken upstream, only that this pipeline did not deliver a
   killed mutant under a correct scope, twice, in two different configurations.

## Invocation

```
python3 tools/stryker/bounded-scope.py --config tools/stryker/t341-config.json \
    --max-mutants 2 --max-tests 12 --output <ABS DIR>
python3 tools/stryker/bounded-scope.py --self-test restore-mismatch --output <ABS DIR>
python3 tools/stryker/bounded-scope.py --self-test span-mismatch   --output <ABS DIR>
python3 tools/stryker/bounded-scope.py --help
```

`--output` must be an absolute directory inside the checkout (the cycle evidence root); relative
paths and paths that escape the repository are rejected. Everything the run writes stays there:
`source.orig` (the byte backup), `<step>.log` per build/test step, `<id>/<id>.trx`, `result.json`,
`commands-executed.json`, and `rejection.json` when the cohort is rejected before anything is built.

Order of operations: **resolve → validate → exclusive lock → baseline → each mutation (always
restored) → restored baseline → result**. Nothing is edited and nothing is built until the whole
config validates; the lock file (`.bounded-scope.lock`, `O_CREAT|O_EXCL`) makes a second run on the
same directory fail instead of interleaving two builds.

## The two-mutation cohort

`tools/stryker/t341-config.json` pins `source_sha256` (measured with `sha256sum` before any edit) and
two byte-anchored mutations. Anchors are byte offsets, not line text: the tracked file is
UTF-8 **with BOM** and CRLF, so text decoding would shift offsets.

**M1 — `M1-native-nulls-first` (positive control), 68:36, `" NULLS FIRST"` → `" NULLS LAST"`.**
The native-NULLS arm of `NullsOrderingRenderer.RenderNative`. This is the exact span Stryker reported
for mutant 10263 (68:36–68:50) and the exact edit P3 hand-applied and measured as 8 failed / 4 passed.
1-based column 36 is the opening quote, so the anchored `find` carries the quotes; the literal value
is ` NULLS FIRST`. The flipped cell is asserted directly at `T287NullOrderingTests.cs:97`, and the
First arm is re-asserted through the parameterized, index-overload, then-by, paging, DISTINCT,
per-parent-rank and extreme-row tests — the 8 tests that fail under M1. The 4 that still pass carry
the `NULLS LAST`, `Default` or invalid-placement cells (`DistinctOn` asserts `NULLS LAST`, so the
flip does not touch it). **Expected: KILLED.**

**M2 — `M2-emulated-case-template` (escape control), 52:18, `CASE WHEN ` → `CASE x WHEN `.**
The literal text of the portable `CASE`-emulation template in `NullsOrderingRenderer.Render`, i.e.
the **non-native** render path. PostgreSQL never reaches it:

- every NULL placement is emitted through `_ctx.Dialect.RenderOrderBy` — `SqlBuilder.cs:883` (the
  rank window), `:902` (key occurrences) and `:1609` (command rendering);
- `PostgresDialect` overrides that member with `RenderNative` and declares
  `SupportsNullsOrdering => true` (`PostgresDialect.cs:92,96`), so the base/interface
  implementations that call `Render` (`SqlDialectBase.cs:137`, `ISqlDialect.cs:404`) are unreachable
  for a PostgreSQL context;
- all 12 scoped tests build their SQL from `PostgresTestContext.Create()`, and three of them assert
  that the emitted SQL does **not** contain `CASE` (`T287NullOrderingTests.cs:106,308,346`) — a guard
  that can only hold while the emulation is bypassed.

The replacement is deliberately *observably* different (`CASE x WHEN`, an extra token) rather than a
whitespace-only edit, so a test that did reach the path could not pass by accident. **Expected:
ESCAPED.** The escape control is what proves the harness reports `ESCAPED` for a mutation the scoped
tests genuinely cannot reach, instead of killing everything it builds.

No other literal qualified: `"0"`/`"1"` (the rank values) occur twice each, `" desc"` occurs on
lines 51 and 76, and `END ASC, ` would only restate the same unreachable surface. The cohort was not
expanded beyond the two roles the deliverable allows.

## Example result

`run-1` and `run-2` of this revision are byte-equivalent in every decision (same mutation sha256s,
same rebuilt assembly hashes, same statuses):

| Step | run-1 | run-2 |
| --- | --- | --- |
| baseline build / vstest exit | 0 / 0 | 0 / 0 |
| baseline tests | 12 executed, 12 passed, 0 failed | same |
| M1 (68:36 native literal) | build 0, vstest 1 → **KILLED**, 8 failed / 4 passed | same |
| M2 (52:18 CASE template) | build 0, vstest 0 → **ESCAPED**, 12 passed | same |
| restored baseline | 12/12 passed, source sha256 back to `051c66a9…` | same |
| invalid trials | 0 | 0 |
| score | `killed / (killed + escaped)` = 1/2 = **0.5** | 0.5 |

`score_label` is written verbatim as
`controlled-cohort detection rate (2 hand-selected string mutations; not a Stryker score)`.

### The exact limits of that number

- It is **not a mutation score** and must never be read as one. It is 1/2 over a two-item,
  hand-selected sample from one file; there is no coverage analysis, no mutator taxonomy, and no
  claim about the other 42 051 mutants Stryker enumerated.
- The **escape control is expected to survive**; it is in the cohort precisely to keep the harness
  honest, and it lowers the figure by construction. A cohort without it would report 1/1 = "100 %",
  which would be meaningless.
- It measures **sensitivity of the 12 scoped tests to these two edits only**. It says nothing about
  soundness of the rest of the code, about other providers, or about the in-memory path.
- It is reproducible only while `source_sha256` matches: the wrapper refuses a source that drifted.
- No score is ever printed or written when a trial is invalid, when the restored baseline is not
  green, when restoration cannot be verified, or when the killed+escaped pair is missing; the
  `result.json` then carries `"score": null` and a `reason`.

## Fail-loud matrix

| Condition | Exit | Message shape |
| --- | --- | --- |
| config unreadable / not JSON / `version != 1` | 3 | `REJECTED (config): …` |
| config path absolute or escaping (`..`) | 3 | `… must be repository-relative …` |
| `source_sha256` mismatch | 3 | `source_sha256 mismatch: config declares …, <path> has …` |
| cohort empty / over `--max-mutants` / over `--max-tests` / over `bounds.*` | 3 | `mutations has N entries, more than …` |
| `find` not unique, absent on its line, or not at `line:column` | 3 | `span mismatch: find … sits at L:C but the config declares L:C; bytes at the declared span are …` |
| `find == replace` | 3 | `… find and replace are identical` |
| role counts other than one positive + one escape control | 3 | `the cohort needs exactly one …` |
| `test_assembly` not a bare file name | 3 | `… must be a bare file name` |
| `--output` relative or outside the checkout | 3 | `REJECTED (output): …` |
| output dir already locked | 4 | `REFUSED: … another run owns this output directory` |
| baseline build or test not the declared 12/12 green | 5 | `baseline is not the declared 12/12 green: …` |
| a trial is INVALID (build/run failure, identity drift, timeout, missing TRX) | 6 | `FAILED: N INVALID trial(s): …` |
| restore mismatch | 7 | `… restore mismatch: … (backup preserved at <output>/source.orig)` |
| restored baseline red / restoration unverified / no killed+escaped pair | 8 | `FAILED: …` |
| self-test could not make the validator reject | 9 | `SELF-TEST FAILED: …` |

Both the build steps and the test steps are bounded (180 s / 120 s) and run through
`subprocess.run(argv_list, timeout=…, capture_output=True)` — no shell, no interpolation. Each run's
exact argv, cwd, env, timeout and exit code is written to `commands-executed.json`.

## Negative tests

- `--self-test restore-mismatch` builds a disposable fixture, corrupts the "restored" target and
  requires the **production** restore validator to reject it (byte mismatch, sha mismatch and a
  missing target are all proven); the product source is never touched.
- `--self-test span-mismatch` requires the **production** span locator to reject a wrong column, a
  wrong line, an absent find and a duplicated find.
- Config negatives exercised in this revision (all exit 3 before any edit, build or score):
  `--max-mutants 1`, `--max-tests 11`, and an M1 copy whose `find` no longer matches its declared
  column. A rejection writes `rejection.json` and nothing else.

## Evidence paths

- This revision's runs: `.pdca/T341/rv3/run-1/` and `.pdca/T341/rv3/run-2/`
  (`result.json`, `commands-executed.json`, `wrapper.log`, `<id>/<id>.trx`, `*.log`, `source.orig`).
- Self-tests: `.pdca/T341/rv3/self-test/`; negatives: `.pdca/T341/rv3/negative/`.
- Frozen commands and the static lint receipt: `.pdca/T341/rv3/commands.json`,
  `.pdca/T341/rv3/plan-lint.json`.
- The Stryker evidence this replaces: `.pdca/T341/rv1/` (whole-core scope, 0 killed),
  `.pdca/T341/rv2/probe-stryker/` (PostgreSQL scope, 0 killed) and
  `.pdca/T341/rv2/probe-control/` (P3 hand-applied control, 8 failed / 4 passed, restored 12/12).
- Cycle status: `.pdca/status/T341-1.md` (plan revision 3).
