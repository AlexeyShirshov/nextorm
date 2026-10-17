# Feature benchmark follow-ups — 2026-10-17

Snapshot: `docs/comparisons/feature-benchmarks-2026-10-17.md` (release 1.0.9-rc2, branch
`1.0.9-rc3`, HEAD `881bf497`). Warm path only; no prepared arms were measured.

The five rc3 tickets below were created from the cases where Nextorm is worse than linq2db. All URLs
were verified with `gh issue view` after creation.

| Case | Nextorm | linq2db | Ratio | Issue |
|---|---:|---:|---:|---|
| Count allocation | 3,824 B | 2,864 B | 1.34× | [#243](https://github.com/AlexeyShirshov/nextorm/issues/243) |
| Sum allocation | 4,226 B | 3,490 B | 1.21× | [#243](https://github.com/AlexeyShirshov/nextorm/issues/243) |
| CTE allocation | 10.89 KB | 9.06 KB | 1.20× | [#244](https://github.com/AlexeyShirshov/nextorm/issues/244) |
| Recursive CTE allocation | 13.31 KB | 8.39 KB | 1.59× | [#244](https://github.com/AlexeyShirshov/nextorm/issues/244) |
| EXCEPT allocation | 6.38 KB | 5.05 KB | 1.26× | [#245](https://github.com/AlexeyShirshov/nextorm/issues/245) |
| INTERSECT allocation | 6.36 KB | 5.01 KB | 1.27× | [#245](https://github.com/AlexeyShirshov/nextorm/issues/245) |
| Raw reader time/allocation | 4.677 ms / 947.98 KB | 3.942 ms / 943.00 KB | 1.19× / 1.005× | [#246](https://github.com/AlexeyShirshov/nextorm/issues/246) |
| JSON streaming, 10k rows | 10.352 ms / 435.64 KB | 9.150 ms / 963.56 KB | 1.13× time; 0.45× allocation | [#247](https://github.com/AlexeyShirshov/nextorm/issues/247) |

## Tracking notes

- #240 is the closed construction-allocation predecessor (owner accepted the RecursiveCte/Except/
  Intersect residual on 2026-10-16). A comment with the new evidence and links to #244/#245 was added
  there; the frozen D201-3 recipe and the previously agreed 1.20× threshold are not changed by these
  tickets.
- #201 is the closed cached-path parity goal; its acceptance was scoped to the five Category-B
  scenarios, so Count/Sum (#243), the raw reader (#246) and JSON streaming (#247) are separate scopes.
- The JSON ticket explicitly separates zero materialization from zero allocation: the 435.64 KB is
  mostly per-row string extraction, not a DTO list. It also adds a `ToList_Dto + JsonSerializer`
  control so the direct-writer comparison is fair.
