#!/usr/bin/env python3
"""One-shot initializer for the rv4 contract/manifest/accounting-correction (run once, kept for provenance).

rv=4 supersedes rv3 as the **current** evidence contract after the authoritative CHECK at
r=3 / n=2 / rv=3 FAILED (build / security / unsealed evidence; primary route CHECK -> PLAN).

It:
* preserves the 30 existing row IDs/obligations verbatim (E232-01..17, all EC232-*);
* reconciles the rv3 claims: carries E232-11 / E232-17 / EC232-RV3-SEAL forward `open`,
  reopens E232-09 / E232-13 / E232-16 and EC232-BUILD (`met` -> `open` with the CHECK reasons);
* adds the 11 rv4 delta rows (total 41 required rows), seeded `open` where unproven;
* writes an accounting-correction.json that supersedes the stale rv3/counters.json `attempt` field.
"""
import json
import os
import subprocess

ROOT = "/home/alex/sources/nextorm"
A = os.path.join(ROOT, "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4")
RV3 = os.path.join(ROOT, "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3")
R = "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4"
HEAD = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True).stdout.strip()

rv3_manifest = json.load(open(os.path.join(RV3, "manifest.json"), encoding="utf-8"))
rv3_contract = json.load(open(os.path.join(RV3, "contract.json"), encoding="utf-8"))

# --- Reconciliation of the carried rows -------------------------------------------------------
REOPEN = {
    "E232-09": "rv3 CHECK rejected the seal (build/security/unsealed evidence). Claimed `met` via C05/C08, "
               "but those runs required -p:NuGetAudit=false; the ordinary restore fails (NU1902/NU1903), so "
               "the environment is not reproducible. Re-verify on the rv4 boundary after D-13.",
    "E232-13": "rv3 CHECK rejected the example evidence: C13 was green only against a locally packed feed with "
               "NuGetAudit=false, and the literal C13 build fails. Reopen (D-08 packaging obligation + D-13).",
    "E232-16": "rv3 CHECK rejected the handoff evidence: the #232 D-06 commit (001f9c9c) folded pre-existing "
               "foreign #160 hunks into AdventureWorksQueries.cs without explicit authorization. Reopen (D-11).",
    "EC232-BUILD": "Reopened: the current ordinary build is not green — `dotnet build nextorm.slnx -c Debug` "
                   "exits 1 with 26 NU1902/NU1903 errors (D-13 prerequisite). Re-verify after D-13.",
}
CARRY_OPEN_REASON = {
    "E232-11": "Carried forward `open` from rv3: thresholds met (line 88.3 / branch 80.2) but the comparable "
               "touched-area percentage decreased; the rv3 seal was rejected, so this obligates the rv4 "
               "coverage remediation (D-10: thresholds AND comparable touched-scope non-decrease).",
    "E232-17": "Carried forward `open`: CHECK-owned schema/fulfillment/sealing; the rv3 seal failed. rv4 seal is "
               "CHECK-owned (EC232-RV4-SEAL) and closes only after the substantive gates.",
    "EC232-RV3-SEAL": "Carried forward `open`: the rv3 CHECK seal failed (build/security/unsealed evidence). "
                      "Retained as historical rv3 row; the current contract seal is EC232-RV4-SEAL.",
}

# --- rv4 delta rows ---------------------------------------------------------------------------
DELTA_ROWS = [
    ("EC232-RV4-PACKAGING", ["AC-10"], "coder-example", "Always",
     "D-08: the AdventureWorks example consumes the current checkout (ProjectReference to nextorm.core/"
     "nextorm.sqlserver); the literal C13 build exits 0 with no CS/packaging error and no version override "
     "or feed masking", "packaging", "D-08",
     [f"{R}/packaging/packaging.json", f"{R}/security/c13-example-build.out"]),
    ("EC232-RV4-V05", ["AC-02", "AC-08"], "coder-sql-tests", "Typed surface present",
     "D-09: V05 TVF inner executable closure (deterministic SQL Server TVF + typed projection)",
     "v05", "D-09", [f"{R}/variants/v05.json"]),
    ("EC232-RV4-V18", ["AC-04", "AC-08"], "coder-sql-tests", "Typed surface present",
     "D-09: V18 typo/absent data value executable closure (emitted identifier / missing-cell behaviour)",
     "v18", "D-09", [f"{R}/variants/v18.json"]),
    ("EC232-RV4-V19", ["AC-04", "AC-08"], "coder-sql-tests", "Typed surface present",
     "D-09: V19 dynamic PIVOT guard / documented API boundary (legacy path retained)",
     "v19", "D-09", [f"{R}/variants/v19.json"]),
    ("EC232-RV4-V21", ["AC-09"], "coder-sql-tests", "Typed surface present",
     "D-09: V21 eager-load / single-query state executable closure; V14/V22 preserved",
     "v21", "D-09", [f"{R}/variants/v21.json"]),
    ("EC232-RV4-COVERAGE", ["AC-06"], "coder-coverage", "Always",
     "D-10: candidate coverage meets MIN_LINE_COVERAGE/MIN_BRANCH_COVERAGE AND the comparable touched-scope "
     "coverage does not decrease; denominators/exclusions unchanged", "coverage", "D-10",
     [f"{R}/coverage/summary.json"]),
    ("EC232-RV4-OWNERSHIP", ["AC-12"], "coder-handoff", "Always",
     "D-11: rv4/ownership.json records the verified #160 sibling-owner identity and explicit post-discovery "
     "authorization; the T232 footprint is T232-only", "ownership", "D-11",
     [f"{R}/ownership.json"]),
    ("EC232-RV4-HANDOFF", ["AC-12"], "coder-handoff", "Always",
     "D-11: rv4/handoff.json records per-hunk byte hashes, clean paths and CRLF for the corrected handoff",
     "handoff", "D-11", [f"{R}/handoff.json"]),
    ("EC232-RV4-SECURITY", ["AC-01", "AC-12"], "coder-security", "Always",
     "D-13: complete ordinary-restore advisory capture (identifiers/package paths/exit codes) plus a "
     "duplicate search; separate issue/remedy ownership is recorded", "security", "D-13",
     [f"{R}/security/advisories.json", f"{R}/security/gh-search/"]),
    ("EC232-RV4-PERF", ["AC-11"], "coder-perf", "Query translation/preparation changes",
     "Acceptance perf re-confirmation on the rv4 tree; no unexplained material regression", "perf", "D-10",
     [f"{R}/perf/comparison.json"]),
    ("EC232-RV4-SEAL", ["AC-01", "AC-02", "AC-03", "AC-04", "AC-05", "AC-06", "AC-07", "AC-08", "AC-09",
                        "AC-10", "AC-11", "AC-12"], "CHECK", "Always",
     "rv4 evidence reconciliation/seal: manifest/contract/provenance/counters valid; zero applicable open "
     "rows; unsupported `met` claims retracted; sealed only after the substantive gates", "seal", "D-12",
     [f"{R}/manifest.json", f"{R}/contract.json", f"{R}/provenance.json", f"{R}/counters.json"]),
]
DELTA_IDS = [d[0] for d in DELTA_ROWS]

required = list(rv3_manifest["required_rows"]) + DELTA_IDS

# --- manifest (seeded: met rows carried with rv3 evidence; reopenings + delta rows open) -------
rows = []
for r in rv3_manifest["rows"]:
    rid = r["id"]
    if rid in REOPEN:
        rows.append({"id": rid, "status": "open", "reason": REOPEN[rid],
                     "reopened_from": "met", "rv3_evidence": r.get("evidence", [])})
    elif rid in CARRY_OPEN_REASON:
        rows.append({"id": rid, "status": "open", "reason": CARRY_OPEN_REASON[rid],
                     "rv3_evidence": r.get("evidence", [])})
    else:
        rows.append(r)
for row_id, criterion, owner, applicability, obligation, case, unit, artifacts in DELTA_ROWS:
    rows.append({"id": row_id, "status": "open",
                 "reason": obligation + f" (planned; python3 \"$R/verify.py\" --case {case} not yet run at r=4/n=1)"})

manifest = {
    "task": "T232 (#232) SQL Server typed PIVOT/UNPIVOT projection",
    "tree": HEAD,
    "status_file": "docs/specs/status/D232-sqlserver-pivot-unpivot-1.md",
    "contract_rv": 4,
    "manifest_revision": "rv=4 (supersedes rv3 as current contract after the r=3/n=2 CHECK failure; "
                         "E232-01..17 / EC232-* IDs+obligations carried verbatim; 4 claims reconciled; 11 delta rows)",
    "required_rows": required,
    "rows": rows,
}
json.dump(manifest, open(os.path.join(A, "manifest.json"), "w", encoding="utf-8"), indent=1, sort_keys=True,
          ensure_ascii=False)
open(os.path.join(A, "manifest.json"), "a", encoding="utf-8").write("\n")

# --- contract ---------------------------------------------------------------------------------
contract = dict(rv3_contract)
contract["contract_rv"] = 4
contract["plan_revision"] = 4
contract["manifest"] = f"{R}/manifest.json"
contract["required_rows"] = required
contract["rows"] = list(rv3_contract["rows"])
for row_id, criterion, owner, applicability, obligation, case, unit, artifacts in DELTA_ROWS:
    contract["rows"].append({
        "id": row_id, "criterion": criterion, "obligation": obligation,
        "invocation": 'python3 "$R/verify.py" --case ' + case, "owner": owner,
        "applicability": applicability, "unit": unit, "artifacts": artifacts,
    })
contract["rv4_delta"] = {
    "supersedes": "rv3 as the *current* contract (the authoritative CHECK at r=3/n=2/rv=3 FAILED: build/"
                  "security/unsealed evidence; primary route CHECK -> PLAN). rv3 history is preserved, not rewritten.",
    "carried_verbatim": "E232-01..E232-17 and all EC232-* keep their IDs and obligations; original rv1/rv3 "
                        "artifact paths are unchanged",
    "reconciled_claims": {
        "carried_open": ["E232-11", "E232-17", "EC232-RV3-SEAL"],
        "reopened_met_to_open": ["E232-09", "E232-13", "E232-16", "EC232-BUILD"],
    },
    "delta_rows": DELTA_IDS,
    "check_regather_budget": "two targeted batches total per CHECK invocation, at most one repeat per row",
    "counters_rule": "recompute counters from rows; never copy the rv3 '27 met' figure",
}
json.dump(contract, open(os.path.join(A, "contract.json"), "w", encoding="utf-8"), indent=1, sort_keys=True,
          ensure_ascii=False)
open(os.path.join(A, "contract.json"), "a", encoding="utf-8").write("\n")

met = sum(1 for r in rows if r.get("status") == "met")
opn = sum(1 for r in rows if r.get("status") == "open")
correction = {
    "task": "T232 (#232)",
    "supersedes": f"{R}/../rv3/counters.json (`attempt` field only)",
    "reason": "the authoritative CHECK ran at r=3 / n=2 / rv=3; rv3/counters.json carried a stale `attempt: 1`",
    "corrected": {"attempt": 2, "plan_revision": 3, "contract_rv": 3, "note": "historical rv3 accounting, corrected"},
    "rv4": {"contract_rv": 4, "plan_revision": 4, "attempt": 1, "cycle": "N=1",
            "required_rows": len(required), "met": met, "open": opn},
    "note": "rv4 counters are recomputed from rows, never copied from the rv3 '27 met' figure.",
}
json.dump(correction, open(os.path.join(A, "accounting-correction.json"), "w", encoding="utf-8"), indent=1,
          sort_keys=True, ensure_ascii=False)
open(os.path.join(A, "accounting-correction.json"), "a", encoding="utf-8").write("\n")

print("head", HEAD, "required", len(required), "rows", len(rows), "met", met, "open", opn)
