# T297-cow — evidence root rv=5 (r=5 representation fix)

Scope: evidence contract **rv=5** for cycle T297-cow, cycle 1, plan revision **r=5**.
rv=5 supersedes rv=4 only for new r=5 runs; the frozen rv=4 manifest
(`.pdca/T297/rv3/manifest-rv4.json`) and its artifacts stay immutable history and are
imported here as `rv4-contract.snapshot.json` (byte-for-byte copy, sha256
`ab0daf185f68aabf12228645ad8c21cb6e1d0046ffe26f7155cbaf93af37467a`).

Contract input: `contract.json` (`contract_rv: 5`, `supersedes: 4`, rows `E01`–`E37`).
Evidence root: `.pdca/T297/rv5/`.
Manifest: `.pdca/T297/rv5/evidence-manifest.json` — **not created yet**; it is generated at
the DO→CHECK boundary.

Protocol pointer: `.pdca/status/T297-cow-1.md` §"Plan (r=5, 2026-10-19)".

Planned subdirectories (created by the named rows as work proceeds):

| Path | Row(s) | Content |
|---|---|---|
| `screening/` | E30, E31 | representation/accessor screening artifacts |
| `logs/` | E32, E36 | focused/full test logs, dispatch scope validation log |
| `reviews/` | E30, E31 | `representation.md`, `accessors.md` |
| `runner/` | E33 | acceptance runner implementation and exercised invocation |
| `coverage/` | E35 | whole-solution and integration coverage XML/Summary |
| `e19/` | E32 | protected-surface compatibility evidence |
| `acceptance-index/` | E34 | two valid same-round ABBA closure rounds and index |

E21 status at sealing: the directional causal reproduction is valid — **8/8 CIs wholly
>1.00, 32/32 valid blocks** — but the **≤1% precision half-width is UNMET** (measured
2.2–7.0%). That miss is retained as a **non-blocking audit obligation for the fix
decision**; the closure oracle is the **unchanged two-round ABBA**, not the diagnostic
precision gate. E21 stays `open` with its evidence paths retained; the honest miss is
recorded again in E37/ACT and never relabelled as met.

Boundaries: `.pdca/T297/rv2/**` and `.pdca/T297/rv3/**` are immutable history. Foreign
dirty paths under `tests/nextorm.integration.tests/**`, `docs/superpowers/**`,
`benchmarks/BenchmarkDotNet.Artifacts/**`, `AGENTS.md` and
`.github/workflows/dotnet.yml` are never edited, restored, normalized or staged.
