# T297-cow — evidence root rv=6 (r=6 bounded recipe decomposition after screening FAIL)

Scope: evidence contract **rv=6** for cycle T297-cow, cycle 1, plan revision **r=6**.
rv=6 supersedes rv=5 only for new r=6 runs. The rv=5 contract is imported here byte-for-byte
as `rv5-contract.snapshot.json` (sha256
`43f319452775f70ab8f071766c4882e2deb350ba099b7cd9004090081c078883`) and stays immutable
history together with its evidence root `.pdca/T297/rv5/`.

Contract input: `contract.json` (`contract_rv: 6`, `supersedes: 5`, `required_rows` `E01`–`E40`).
Evidence root: `.pdca/T297/rv6/`.
Manifest: `.pdca/T297/rv6/evidence-manifest.json` — **not created yet**; it is generated at
the DO→CHECK boundary only.

Protocol pointer: `.pdca/status/T297-cow-1.md` §"Plan (r=6, 2026-10-19)".
r=5 history pointer: `.pdca/T297/rv5/README.md`, `.pdca/T297/rv5/contract.json`,
`rv5-contract.snapshot.json` (this directory).

## Path normalization amendment

The planner's brief named the directory `.pdca/T297-cow/rv6/`. Every existing revision of this
cycle lives under `.pdca/T297/rv<k>/` (`rv1`, `rv2`, `rv3`, `rv5`), and `.pdca/T297-cow/` does not
exist. The evidence root is therefore normalized to **`.pdca/T297/rv6/`**; all row artifact
pointers, the status header and the manifest path use the normalized form. No contract obligation
depends on the directory name.

## Screening FAIL that forced r=6

The r=5 flat-root candidate (`777e9233`, P03a) was screened with the bounded six-round probe
(`.pdca/T297/rv5/screening/accessors/`) and **failed the screening bar** (median ≤1.02, CI upper
≤1.05) on every end-to-end comparison and on the recipe phase:

- e2e C/A: cte **1.0626**, recursive **1.0931**, inlist **1.0672**, leftjoin **1.1118**;
- `planner_recipe`: **1.1530 / 1.1127 / 1.0522 / 1.2337**;
- `plan`: 1.4172 / 1.457 / 1.196 / 1.441; `construct` PASS only cte (0.9894);
  InList `prepare_pipeline` 1.1133.

The flat root is therefore **rejected**: r=6 reverts P03a's production change (E40), then spends one
bounded instrumented pass on the recipe span (E38) before any further fix.

## Planned subdirectories (created by the named rows as work proceeds)

| Path | Row(s) | Content |
|---|---|---|
| `loc/` | E38 | sub-phase decomposition of `QueryPlanner.cs:614-662`, definitions, invocation counts, reconciliation |
| `screen/` | E39 | screening artifacts of any later approved candidate (unchanged six-round paired ABBA) |
| `rollback/` | E40 | scoped inverse patch of P03a + build/core/E19 verification |
| `logs/` | scope | dispatch scope validation log (`test-scope.log`) |

## Boundaries

- Rows E01–E37 are retained with unchanged IDs and obligations; only their `rv` is 6.
- E38/REQ-LOC, E39/REQ-SCREEN, E40/REQ-ROLLBACK are the new r=6 rows.
- `.pdca/T297/rv1/**`, `rv2/**`, `rv3/**`, `rv5/**` are immutable history.
- No product edit is made by this revision of the contract; `footprint` is the rv=5 footprint
  unchanged (the rollback touches only P03a's production paths inside it).
- Foreign dirty paths under `tests/nextorm.integration.tests/**`, `docs/superpowers/**`,
  `benchmarks/BenchmarkDotNet.Artifacts/**`, `AGENTS.md` and `.github/workflows/dotnet.yml` are
  never edited, restored, normalized or staged.
