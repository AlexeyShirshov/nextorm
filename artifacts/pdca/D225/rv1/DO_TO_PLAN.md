# D225 / T225 — DO→PLAN candidate

- Task: T225 / #225; baseline B `aea0a9ba644a8be34f5e9bf39e4daf4a33861f17`; DO start HEAD
  `ee3ee9fe96b81fa8b8bbcac124aee550ec98ac3b`; branch `1.0.9-rc3`; cycle N=1, plan r=2, contract rv=1.
- Verdict: **DO→PLAN candidate** — the frozen plan r=2 is not executable coherently against HEAD. DO stopped
  before any footprint edit. No footprint file was modified.

## What broke

Plan r=2 was frozen at B (2026-10-13 08:37 +0500). Between the freeze and DO, commit

```
d76c5ae363e4e92b3bb77f57fbe7a878113a4d97  2026-10-13 14:49 +0500  #223 Composite-junction M:N SQL link carrier (T223, r=1 rv=1)
```

landed and **is an ancestor of HEAD** (`git merge-base --is-ancestor d76c5ae3 HEAD` → yes). T223 implemented
the composite-junction M:N SQL link carrier — i.e. exactly the deliverable the plan declared out of scope
("`Deferred:` implementing SQL composite-junction M:N — separate #223"; "Prerequisites: #223 is NOT a
dependency"). T223 also rewrote 7 of the plan's 16 footprint files to the **support** wording.

Consequences for the frozen criteria (`docs/specs/status/D225-joininto-mn-docs-delta-1.md:105-112`):

- **C3 is obsolete.** It requires the text `NotSupportedException("composite-junction M:N is not
  supported; see #223")`. That message is `git grep`-absent from `src/**` at HEAD; `JunctionLinkSourceFactory.cs:205-206`
  builds the composite carrier instead of throwing. The class the plan calls the executable oracle
  (`JoinIntoCompositeJunctionRejectionTests`) was re-pointed by T223 and now asserts **NotThrow** (line 113);
  it passes 4/4, exit 0. Writing C3 as specified would land false documentation that contradicts the
  implementation and T223's already-committed accurate text.
- **E01's decisive oracle is unsatisfiable.** E01 is defined as "proves the original AC07 meaning in the landed
  EN/RU docs **against the implementation**". The original AC07 meaning (rejection) contradicts the
  implementation (support), so no honest evidence row can mark E01 `met`.
- **Half of the carry is already done.** 14 of 26 hunks are `already-present` (superseded by T223); 4 are
  `obsolete`; 8 remain genuine work (stale pre-T213 text to remove) — but their correct replacement is the
  #213/#223 **support** statement, which the frozen C3 forbids.

Evidence: `disposition.md` (26-row map), `semantic-review.md` (C1–C4 audit), `oracle-rejection.log`,
`oracle-remainder.log`.

## Legal routes inside the frozen plan (checked, none viable)

- Write C3 verbatim → violates the plan's own Goal ("EN+RU documentation accurately describes the delivered
  `JoinInto` behavior"), its docs rule ("change only missing/inaccurate statements") and C5 (accurate EN/RU).
- Skip C3 and land only the support text → silently re-defines the contract's decisive criterion; forbidden for
  DO ("do not improvise a different plan"), and would need an rv supersession.
- Treat the 4 `already-present` + 8 `conflict-reworked` hunks as the whole job → does not satisfy C1/C3 as
  written and leaves E01 unprovable.

## Requested re-plan (input for a `planner` revision r=3)

1. **Re-baseline** against HEAD `ee3ee9fe…` (B is no longer the tip); keep the footprint (16 files).
2. **Re-scope the criteria** to the delivered behaviour: replace C3 with "composite-junction M:N is documented
   as **supported** in SQL generation and in-memory (#213/#223), with the fluent declaration helpers noted as
   single-column" — i.e. the wording T223 already used in `limitations.md:19` / `relationships.md:194,292`.
   Keep C1, C2 and C4; drop the exact-message requirement.
3. **Re-scope E01** accordingly (audit against the support statement, not the rejection).
4. **Re-derive the carry**: 4 obsolete hunks → support rewording; 8 `conflict-reworked` hunks → replace the
   stale remainder text (`29-implicit-navigation.md:257/263`; `capability-matrix.md:110`;
   `linq2db-comparison.md:109`; `linq2db-backlog-gap-analysis.md:54,345,404,504`) with #213/#223 support; add
   the missing M:N note to `eager-loading.md` and `02-joins.md` (+ RU). 14 hunks stay `already-present`.
5. **Consider** whether T225 is still needed as a separate task, or whether its residual delta folds into the
   #223 documentation closure (the 14 already-present hunks were landed by T223 under its own AC08).

## Re-entry preconditions (re-check at the new DO start)

- Reconfirm HEAD and that no further `Builders/Joins/**` change invalidates C1/C2/C4.
- Keep the two executed selectors as the oracle; re-run them at the new baseline.
- Preserve the external reorg and T223's landed docs; do not revert to the T213 rejection wording.
