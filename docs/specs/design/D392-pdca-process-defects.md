# D392 — PDCA process defects

Scope: process defects observed in cycle `N=1`, contract revision `r=1`, attempts `n=1..2` for issue #392. These are process defects, not vector-search product defects. Internal evidence paths are repository-relative.

## D392-P1 / CRLF-TRACKED-EVIDENCE

- **Stable key:** `D392-P1 / CRLF-TRACKED-EVIDENCE`.
- **Symptom:** After the first line-ending normalization, a later audit found six lone-LF terminators in the tracked status file. A second terminator-only repair was required. The same class of line-ending defect had affected tracked evidence/status twice.
- **Root cause:** The exact write path that reintroduced lone-LF terminators is unproven. The recorded sequence establishes recurrence after the first normalization and the second repair, but not which tool or edit caused it.
- **Effect on attempts/cycles:** In U5b, the first normalization was fix 1; recurrence reopened the CRLF gate and required fix 2. CHECK `r1/n1` then recorded the process defect, and the corrective work continued in `r1/n2`; no contract revision was made.
- **Evidence:** `AGENTS.md:83-88`; `.pdca/status/D392-1.md` §`DO result` (U5b entries; original lines 280, 284-285); `.pdca/D392/rv1/logs/u5b-repo-gates.log`; `.pdca/D392/rv1/logs/u5b-repo-gates-final.log`; `.pdca/D392/rv1/logs/u5b-repo-gates-after-crlf-fix-r1.log`; `.pdca/D392/rv1/logs/u5b-crlf-targeted-repair.log`.
- **Prevention:** Normalize every tracked text file after its final edit and before every commit; after the final edit, rerun a tracked-path line-ending audit that includes the status, manifest, and every manifest receipt. Do not treat an earlier clean audit as evidence for later file writes.

- **Third occurrence:** On 2026-11-14 the status file contained four lone-LF terminators on lines 318-321 (704 lines; CR-stripped SHA-256 `2153357176b1c2bd73222572d995d3c62dc9f409b23e7b9eafe59e8766480ff4`). The third terminator-only repair normalized those terminators without changing the CR-stripped status content; see `.pdca/D392/rv1/logs/u11-crlf-pre-repair-baseline.log` and `.pdca/D392/rv1/logs/u11-crlf-terminator-repair.log`.
- **Suspected cause (inference, unproven):** The write path emits LF for appended lines. This is an inference; the exact path/cause has not been established.
- **Mandatory write postcondition:** Every status/spec write must normalize the complete file to CRLF and then verify the result inside that write operation, before considering the write complete and before any commit. Verify 0 lone-LF terminators and verify the CR-stripped content hash is unchanged outside the newly appended lines; line-count changes are limited to appended lines. A later repository audit does not replace this per-write normalize-and-verify check.

## D392-P2 / VERSIONGATE-CACHE-IDENTITY

- **Stable key:** `D392-P2 / VERSIONGATE-CACHE-IDENTITY`.
- **Symptom:** A `BeSameAs` assertion checking reuse of the prepared command failed in full PostgreSQL test runs twice: once in a run with 2013 passed / 4 failed / 0 skipped, and again with 2020 passed / 1 failed / 0 skipped. Isolated/class reruns passed.
- **Root cause:** The test-isolation defect is proven: `VersionGateTests` asserted shared query-plan-cache identity outside the existing non-parallel `Query plan cache` collection. The historical trigger for each observed failure (for example, whether a concurrent global cache purge overlapped the lookup) remains unproven.
- **Effect on attempts/cycles:** The failures recurred across `r1/n1` and `r1/n2`; the latter blocked the full PostgreSQL run until U7 added the test class to the owning non-parallel collection. One isolation fix was applied; the assertion and product/cache implementation were unchanged. The class then passed 14/14 and the full PostgreSQL suite passed 2021/2021.
- **Evidence:** `.pdca/status/D392-1.md` §`DO result` (original lines 222, 301, 307-311) and §`Defect history` (original line 426); `tests/nextorm.postgres.tests/Dialect/VersionGateTests.cs:201`; `tests/nextorm.core.tests` cache-collection rationale in `DataModifyingCtePlanCacheTests.cs:6-11`; `.pdca/D392/rv1/logs/u6-test-postgres-tests.log`; `.pdca/D392/rv1/logs/u7-versiongate-postgres-final.log`; `.pdca/D392/rv1/logs/u7-versiongate-class-after-isolation.log`.
- **Prevention:** Any test asserting identity or reuse of a shared query-plan cache must belong to the owning non-parallel `Query plan cache` collection, so concurrent global cache clearing cannot invalidate the assertion.

## D392-P3 / UNCOMMITTED-DISPATCH-LOSS

- **Stable key:** `D392-P3 / UNCOMMITTED-DISPATCH-LOSS`.
- **Symptom:** The F-01 dispatch was interrupted by a server restart while its 16-path candidate remained dirty and had not been recorded at a unit boundary; the next dispatch had to re-anchor and recover the interrupted candidate.
- **Root cause:** The dispatch interruption is recorded in the handoff. The reason for the server restart itself is not established. The process gap was that no durable unit-boundary commit and status record had been made before further work depended on the candidate.
- **Effect on attempts/cycles:** Recovery work in `r1/n2` had to inspect and retain the interrupted candidate, revalidate it, and then close U6 with an authorized commit. This added recovery/reconciliation work but did not change `r` or independently consume another cycle attempt.
- **Evidence:** `.pdca/status/D392-1.md` §`DO result` (original lines 288-290, 663-670); `.pdca/status/D392-1.md` §`Git state` (original line 528); `.pdca/D392/rv1/logs/u6-test-postgres-unified-vector-search.log`; `.pdca/D392/rv1/logs/u6-test-integration-unified-vector-search.log`.
- **Prevention:** Commit each completed unit at its boundary when authorized, and record the unit state/evidence in the cycle status before starting dependent work. After interruption, re-anchor and reconcile the status before continuing.


## D392-P4 / SELF-REFERENTIAL-HEAD-FREEZE

- **Stable key:** `D392-P4 / SELF-REFERENTIAL-HEAD-FREEZE`.
- **Symptom:** The r1 evidence-identity rule required the frozen, tested, and final Git `HEAD` commit hashes to be equal. Status/evidence commits advanced `HEAD` after the verified product run (`9cd05457`) while the product tree stayed unchanged; all three r1 attempts (n=1..3) were blocked by F-02 despite no open product or acceptance-criteria defect.
- **Root cause:** The rule treated commit identity as result identity. A tracked status/evidence commit necessarily changes `HEAD`, even when every delivered product path and its proving content remain identical; consequently the equality condition was self-referential and could not be maintained while recording the cycle.
- **Effect on attempts/cycles:** F-02 recurred across r1/n1, r1/n2, and r1/n3 with zero product fixes for this defect. Rebinding evidence by commit SHA consumed the full r1 attempt budget and required the genuine r2 contract revision; no product, test, or public-documentation change was needed to resolve this process defect.
- **Evidence:** `.pdca/status/D392-1.md` §`CHECK record` (r1/n1 and r1/n2 F-02 findings), §`Defect history` (F-02 observations and r1/n3 record); `.pdca/D392/rv1/artifacts/u13-frozen-identity.md:5-10` (tested HEAD `9cd05457…` differs from final HEAD Z while both product-content digests are `ebe5b84c…`); `.pdca/D392/rv1/logs/u13-integration-full.log` (canonical run at the tested content); `.pdca/status/D392-1.md` §`PLAN record` r2 (revised identity contract).
- **Prevention:** Freeze content identity after writing this spec: `D_full = git ls-tree -r HEAD -- src tests docs | git hash-object --stdin` includes the final `D392-P4` content; `D_code = git ls-tree -r HEAD -- src tests docs | grep -v 'docs/specs/' | git hash-object --stdin` excludes internal specs. Require `D_code(r2) == D_code(9cd05457)` and an empty `git diff 9cd05457..HEAD -- src tests`; persist both digests, HEAD, clean status and path counts. At CHECK require clean `src`/`tests`/`docs`, `D_full(HEAD) == D_full(DO)`, and only `.pdca/**` commits after the digest anchor. Do not modify any `src`/`tests`/`docs` path after freezing `D_full`.
