# T297-cow — evidence root rv=7 (r=7 one bounded description-adoption candidate after ESCALATE A)

Scope: evidence contract **rv=7** for cycle T297-cow, cycle 1, plan revision **r=7**.
rv=7 supersedes rv=6 only for new r=7 runs. The rv=6 contract is imported here byte-for-byte
as `rv6-contract.snapshot.json` and stays immutable history together with its evidence root
`.pdca/T297/rv6/`.

Contract input: `contract.json` (`contract_rv: 7`, `supersedes: 6`, `required_rows` `E01`–`E42`).
Evidence root: `.pdca/T297/rv7/`.
Manifest: `.pdca/T297/rv7/evidence-manifest.json` — **not created yet**; it is generated at
the DO→CHECK boundary only.

Protocol pointer: `.pdca/status/T297-cow-1.md` §"Plan (r=7, 2026-10-19)".
r=6 history pointer: `.pdca/T297/rv6/README.md`, `.pdca/T297/rv6/contract.json`,
`rv6-contract.snapshot.json` (this directory).

## Escalate ruling A — one bounded adoption candidate

After the r=6 P04 decomposition failed the 80 % attribution test (top contributors `copyto`
3.46–3.50× at 29–45 % share, `capture` 26–59 %, `qplan_hash` 76.9 % for InList), `escalate`
ruled **option A**: run **one** bounded candidate that keeps the storage representation and
attacks the member-by-member description transfer itself.

**Candidate:** `ApplyPreparationFrom` adopts `clone._description` **by reference** under a
copy-on-write guard instead of copying the description members one by one through
`CopyTo(copyAll: true)`.

- `MarkShared` is called on the adopted instance so both the recipe and the fresh command
  clone-on-write; the fresh command clears `_definitionMemo`.
- Every **non-description** member keeps the exact element-wise transfer `CopyTo(copyAll:true)`
  performed (preparation-backed, plain/protected/binding and `copyAll` fields), followed by the
  existing `_joins` / `_whereBasePlanHash` / `_preparedArrayJoin` / `OneColumn` writes.
- `_preparation` is **NOT** adopted.
- Safety invariants (each must hold before the candidate is screened): no writer touches
  `_description` outside `WriteDescription` (verified by scout); the recipe clone is per-thread
  and immutable after preparation; shared-`Any` commands are excluded from the recipe path.

Verification for E41: a reflection parity test comparing the old `CopyTo` transfer with the new
path over all 18 `QueryCommand` instance fields plus the description's deep contents, a COW
isolation test (a write on the fresh command after adoption does not mutate the recipe and vice
versa, over two consecutive reuses), the full core suite, the COW/recipe suites and E19.

## Stop-loss

E42 reuses E39's **exact uninstrumented six-round paired ABBA** screen on the adopted candidate:
median ≤1.02 and CI upper ≤1.05 for every registered comparison. **If the screen fails, stop** —
no further candidate, no retry, no threshold relaxation. #297 stays open with the measured
residuals B **1.076 / 1.101 / 1.082 / 1.104**, C **1.063 / 1.093 / 1.067 / 1.112**, the
adopted-candidate result and the next lever (**qplan hash/fingerprint for InList; comparer
footprint widening**).

## Planned subdirectories (created by the named rows as work proceeds)

| Path | Row(s) | Content |
|---|---|---|
| `adopt/` | E41 | parity test, COW isolation test, full core + COW/recipe + E19 logs |
| `screen/` | E42 | adopted-candidate six-round ABBA table and the stop-loss verdict |
| `logs/` | scope | dispatch scope validation log (`test-scope.log`) |

## Boundaries

- Rows E01–E40 are retained with unchanged IDs and obligations; only their `rv` is 7.
- E41/REQ-ADOPT and E42/REQ-SCREEN-A are the new r=7 rows.
- `.pdca/T297/rv1/**`, `rv2/**`, `rv3/**`, `rv5/**`, `rv6/**` are immutable history.
- `footprint` is the rv=6 footprint unchanged; the candidate lives in
  `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, no new file is needed.
- Foreign dirty paths under `tests/nextorm.integration.tests/**`, `docs/superpowers/**`,
  `benchmarks/BenchmarkDotNet.Artifacts/**`, `AGENTS.md` and `.github/workflows/dotnet.yml` are
  never edited, restored, normalized or staged.
