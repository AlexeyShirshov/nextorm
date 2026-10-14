# D226 / rv1 — documentation review (E226-07)

- task: T226 (#226), contract rv=1, corrective DO n=2/3
- date: 2026-10-14
- **Ownership note**: the four guide pages are owned by a separate docs stream in this DO brief; this
  review only *verifies* them and never edits them (the earlier CHECK finding about the signature
  snippet was accepted as non-defective — the API is get-only, so `{ get; }` is correct).

## 1. XML documentation on the new public members

- `ProcedureResult.RecordsAffected` — `///` summary present (value/sentinel, availability moment,
  early Dispose, non-throwing cached read): `src/nextorm.core/DataContext/ProcedureResult.cs:74-83`.
- `BatchResult.RecordsAffected` — `///` summary present (one driver total, captured at release,
  not derived from `ResultSetCount`): `src/nextorm.core/Builders/BatchBuilder.cs:395-404`.
- Internal members documented as well: `CommandReaderOwner.RecordsAffected`,
  `CommandReaderOwner.ReadRecordsAffected` (`src/nextorm.core/DataContext/CommandReaderOwner.cs:38-57`).
- Build with `TreatWarningsAsErrors=true` reports **0 Warning(s) 0 Error(s)**, so no documentation
  warning (CS1591) is outstanding for the changed surface.

## 2. Guide pages (verified, not edited)

| page | `RecordsAffected` mentions | line endings |
|---|---|---|
| `docs/guide/12-raw-sql.md` | 3 | CRLF |
| `docs/ru/guide/12-raw-sql.md` | 3 | CRLF |
| `docs/guide/23-sql-batch.md` | 2 | CRLF |
| `docs/ru/guide/23-sql-batch.md` | 2 | CRLF |

- **EN/RU parity**: identical mention counts per pair (3/3 and 2/2), so both sides of each guide
  cover the member; the RU file is longer by 8 lines, which is inline with the RU prose style used
  by the rest of the guide and not a missing section.
- **CRLF**: all four pages are CRLF (checked with `file`).
- **No specs links from public docs**: `grep -rn "docs/specs" docs/guide/ docs/ru/guide/` returns
  nothing, so the internal-specs rule holds for the touched guides.
- Coverage of the required points (value/sentinel, availability moment, early Dispose, single batch
  count, ClickHouse limits) is attested by the docs stream; this review confirms presence and
  parity, not wording.

## 3. Status / evidence hygiene

- T53 freeze candidates are registered in the cycle status file (2 members, tag `#226`), and T53's
  own status file was deliberately not touched.
- The evidence artifacts named by the plan's A/C/M/I/U slots exist under
  `artifacts/pdca/D226/rv1/`; `verification: bash artifacts/pdca/D226/rv1/verify-plan-evidence.sh`.
