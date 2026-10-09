# D170 terminal STOP evidence

- task: D170 (GitHub issue #170) — collection `1.0.9-rc2`, group G1
- outcome: **incomplete (terminal STOP)** — no 4th corrective slot.
- STOP reason: CHECK r=1, r=2 and r=3 all failed on the **same defect family** —
  unmet R170-02 / R170-03 acceptance-test and variant/oracle closure (the ClickHouse row was
  satisfied, but the required variant matrix was not closed). A same-family CHECK failure at r=3
  is the sealed terminal revision, so escalation mandates STOP.
- preserved patch (all D170 changes, tracked diff + the new untracked test):
  - `docs/specs/status/rc2-170-evidence/D170-STOP-incomplete.patch`
- preserved per-revision evidence:
  - `artifacts/pdca/rc2-170/r1/`
  - `artifacts/pdca/rc2-170/r2/`
  - `artifacts/pdca/rc2-170/r3/`
- branch `1.0.9-rc2` was restored to the last verified-green tip `5b7fb9e7`; the task's
  product/test/doc paths were reset to it and the new test deleted (kept only in the patch).
- next allowed step: **none for D170**; issue #170 stays **OPEN**.
