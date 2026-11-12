# D381 — PDCA process defects

Scope: process findings from cycle `N=1`, contract revision `r=1`, attempts `n=1..3` for issue #381. These are process defects, not PgVector product defects. Internal evidence paths are relative to the repository unless otherwise noted.

## D4-public-measurement-coverage

- **Stable key:** `D4-public-measurement-coverage`.
- **Symptom:** The first CHECK found required measurements absent (byte-level CRLF, direct `.cs` folder counts, public specs-link scan, and EN/RU numbering). The n2 scan covered `docs/**/*.md` but omitted root `readme.md`, leaving the public-spec-link prohibition unverified.
- **Root cause:** The DO measurement inventory was not derived as a complete coverage map from the criterion's whole public surface. It treated Markdown below `docs/` as exhaustive and did not enumerate root-level public text and link-bearing YAML/TOC files.
- **Effect on attempts/cycles:** CHECK `r1/n1` found the missing-measurement set (F4), so the candidate looped to DO `n2/3`. The n2 measurement pass closed CRLF/folder-count/numbering and docs Markdown checks, but CHECK `r1/n2` identified the readme omission (F4 residual). Completing the whole public-text inventory consumed work in `n3/3`; the residual is now measured rather than silently treated as complete.
- **Evidence:** `.pdca/d381-pgvector/rv1/check-r1-report.md` (F4, missing measurements); `.pdca/d381-pgvector/rv1/check-r2-report.md` (F4, readme omission); `.pdca/status/d381-pgvector-1.md` (`## Defect history`, D4); `.pdca/d381-pgvector/rv1/measure-public-spec-links.txt` (the earlier limited scan); `.pdca/d381-pgvector/rv1/logs/public-spec-links-scan-final.log` (earlier docs-only command); `.pdca/d381-pgvector/rv1/measure-public-spec-links-n3.txt` and `logs/public-spec-links-n3.log` (n3 full public link-target scan: 586 files, 0 matches, rc=0).
- **Prevention:** Before freezing a docs-related candidate, enumerate all public text roots and formats from the criterion: root `readme.md`, public `docs/**` and `docs/ru/**` Markdown, and link-bearing YAML/TOC files. Persist the exact file set, command, exit status, match count and result. Keep separate explicit checks for CRLF, source-folder counts and each EN/RU numbered series; do not label a partial scan global.

## D7-process-spec-omission

- **Stable key:** `D7-process-spec-omission`.
- **Symptom:** The D4 process defect and its effects were recorded in cycle status but no separate `docs/specs/design/` process-spec was created, despite the repository rule requiring one for PDCA process defects.
- **Root cause:** The process-defect rule was not activated when CHECK's F4 exposed a measurement-procedure failure; status logging was treated as sufficient, and no closeout gate checked for the required spec/link.
- **Effect on attempts/cycles:** The omission survived the n2 candidate and was confirmed by CHECK `r1/n2` as F7. It required corrective work in `n3/3` alongside the remaining F4 measurement. It did not change the product acceptance criteria or identify a PgVector code defect.
- **Evidence:** `AGENTS.md:85–91`; `.pdca/d381-pgvector/rv1/check-r2-report.md` (F7); `.pdca/status/d381-pgvector-1.md` (`## Defect history`, D7); `.pdca/status/d381-pgvector-1.md` (`## Progress log`, CHECK → DO n3/3).
- **Prevention:** When a PDCA cycle records a process defect, create one internal design spec in the same cycle with stable key, symptom, cause, cycle/attempt effect, evidence and prevention. Add its path to that stable defect's status row; before freezing, cross-check every process-defect history key against a corresponding `docs/specs/design/` record. Do not patch the skill during the running cycle.
