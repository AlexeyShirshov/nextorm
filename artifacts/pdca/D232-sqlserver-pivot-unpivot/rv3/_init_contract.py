#!/usr/bin/env python3
"""One-shot initializer for the rv3 manifest/contract/counters (run once, kept for provenance)."""
import json
import os
import subprocess

ROOT = "/home/alex/sources/nextorm"
A = os.path.join(ROOT, "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3")
R1 = os.path.join(ROOT, "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1")
HEAD = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True).stdout.strip()

rv1_manifest = json.load(open(os.path.join(R1, "manifest.json"), encoding="utf-8"))
rv1_contract = json.load(open(os.path.join(R1, "contract.json"), encoding="utf-8"))

DELTA_ROWS = [
    ("EC232-REPRO", ["AC-08", "AC-11", "V26", "V27", "V28"], "coder-prerequisite", "Always (before production edits)",
     "isolated repro children, production hashes, frozen oracles; C20 exit 0 only after a genuine builder red",
     "C20", "D232-PREREQ-typed-derived",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/baseline.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/production-before.json"]),
    ("EC232-DERIVED-UNIT", ["AC-01", "AC-03", "AC-08"], "coder-sql-tests", "Typed derived surface present",
     "derived builder/command unit tests; every typed derived cell/route case selected", "C21",
     "D232-PREREQ-typed-derived",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/derived/unit.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/derived/case-inventory.json"]),
    ("EC232-DERIVED-EXEC", ["AC-02", "AC-07", "AC-08", "AC-09"], "coder-integration", "Typed derived surface present",
     "SQL Server executed; per-provider state recorded", "C22", "D232-PREREQ-typed-derived",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/derived/execution.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/derived/provider-matrix.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/derived/container-state.json"]),
    ("EC232-SHARED-REGRESSION", ["AC-03", "AC-08", "AC-09", "AC-11"], "coder-regression", "Always",
     "eight project suites + container boundary; no protected file changed", "C23",
     "D232-PREREQ-typed-derived",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/regression/inventory.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/regression/oracle-comparison.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/regression/protected-files.json"]),
    ("EC232-ORACLE-AMEND", ["AC-02", "AC-11"], "coder-oracle", "Independent amendment unit",
     "exactly one key amended; before/after hashes and history verified", "C24",
     "D232-AMEND-unpivot-null-oracle",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/oracle/before.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/oracle/after.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/oracle/amendment.json"]),
    ("EC232-BUILD", ["AC-01", "AC-05", "AC-12"], "coder-build", "Always",
     "core + provider builds: 0 warnings / 0 errors", "C25", "D232-PREREQ-typed-derived",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/build/api-review.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/build/gate-review.json"]),
    ("EC232-DERIVED-BUILDER", ["AC-08", "AC-09", "V26"], "coder-sql-tests", "Builder route changed",
     "preserved projection/SQL/data and guarded controls", "C26", "D232-PREREQ-typed-derived",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/variants/derived-builder.json"]),
    ("EC232-DERIVED-COMMAND", ["AC-08", "V27"], "coder-sql-tests", "Command route changed",
     "equivalent emitted names, qualification and rows", "C27", "D232-PREREQ-typed-derived",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/variants/derived-command.json"]),
    ("EC232-PIVOTFREE-PROBE", ["AC-08", "AC-11", "V28"], "coder-prerequisite", "Always",
     "pivot-free diagnostic with the actual outcome and source provenance", "C28",
     "D232-PREREQ-typed-derived",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/variants/pivot-free.json"]),
    ("EC232-CASE-SENSITIVE-ALIAS", ["AC-08", "V29"], "coder-integration", "Derived surface present",
     "case-sensitive database resolution for the bare and aliased identifiers", "C29",
     "D232-PREREQ-typed-derived",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/variants/case-sensitive-alias.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/variants/database-collation.json"]),
    ("EC232-CACHE-LOCALITY", ["AC-09", "V24", "V26"], "coder-core-tests", "Always",
     "no sticky command/plan-cache state", "C30", "D232-PREREQ-typed-derived",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/cache/locality.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/cache/source-review.json"]),
    ("EC232-PREREQ-MUTATION", ["AC-11", "V26", "V29"], "coder-mutation", "New prerequisite code present",
     "every mandated mutant killed and the tree restored", "C31", "D232-PREREQ-typed-derived",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/mutation/prerequisite.json"]),
    ("EC232-RV3-SEAL", ["AC-01", "AC-02", "AC-03", "AC-04", "AC-05", "AC-06", "AC-07", "AC-08", "AC-09",
                        "AC-10", "AC-11", "AC-12"], "CHECK", "Always",
     "manifest/contract/provenance/counters valid; zero applicable open rows", "C32,C33", "D-07",
     ["artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/manifest.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/contract.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/provenance.json",
      "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/counters.json"]),
]

required = list(rv1_manifest["required_rows"]) + [row[0] for row in DELTA_ROWS]

rows = []
for row in rv1_manifest["rows"]:
    rows.append(row)
for row_id, criterion, owner, applicability, obligation, invocation, unit, artifacts in DELTA_ROWS:
    rows.append({"id": row_id, "status": "open", "reason": obligation + " (planned; " + invocation + " not yet run)"})

manifest = {
    "task": "T232 (#232) SQL Server typed PIVOT/UNPIVOT projection",
    "tree": HEAD,
    "status_file": "docs/specs/status/D232-sqlserver-pivot-unpivot-1.md",
    "contract_rv": 3,
    "manifest_revision": "rv=3 (additive; E232-01..17 carried verbatim from rv1, rv2 row IDs preserved, rv3 supersedes unexecuted rv2)",
    "required_rows": required,
    "rows": rows,
}
json.dump(manifest, open(os.path.join(A, "manifest.json"), "w", encoding="utf-8"), indent=1, sort_keys=True, ensure_ascii=False)
open(os.path.join(A, "manifest.json"), "a", encoding="utf-8").write("\n")

contract = dict(rv1_contract)
contract["contract_rv"] = 3
contract["plan_revision"] = 3
contract["manifest"] = "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/manifest.json"
contract["required_rows"] = required
contract["rows"] = list(rv1_contract["rows"])
for row_id, criterion, owner, applicability, obligation, invocation, unit, artifacts in DELTA_ROWS:
    contract["rows"].append({
        "id": row_id, "criterion": criterion, "obligation": obligation, "invocation": invocation,
        "owner": owner, "applicability": applicability, "unit": unit, "artifacts": artifacts,
    })
contract["rv3_delta"] = {
    "supersedes": "rv2 (issued in-session, never transcribed or executed; no rv2 artifact directory)",
    "carried_verbatim": "E232-01..E232-17 and C01..C19 keep their IDs, obligations and original rv1 paths",
    "preserved_rv2_ids": ["EC232-DERIVED-UNIT", "EC232-DERIVED-EXEC", "EC232-SHARED-REGRESSION",
                          "EC232-ORACLE-AMEND", "EC232-BUILD"],
    "check_regather_budget": "two targeted batches total per CHECK invocation, at most one repeat per row",
}
json.dump(contract, open(os.path.join(A, "contract.json"), "w", encoding="utf-8"), indent=1, sort_keys=True, ensure_ascii=False)
open(os.path.join(A, "contract.json"), "a", encoding="utf-8").write("\n")

counters = {
    "task": "T232 (#232)", "contract_rv": 3, "cycle": "N=1", "plan_revision": 3, "attempt": 1,
    "units": {"done": ["D-01", "D-02", "D-03", "D-04"],
              "active": ["D232-PREREQ-typed-derived", "D232-AMEND-unpivot-null-oracle", "D-05"],
              "pending": ["D-06", "D-07"], "superseded": []},
    "selected_executed_passed_failed_skipped_per_provider": {},
    "guards_covered_required": {"covered": 17, "required": 17},
    "branch_delta": {"added": None, "covered": None},
    "mutants_killed_attempted": {"killed": 6, "attempted": 6, "scope": "D-04 typed rewriter/guards (rv1)"},
    "applicable_evidence_complete_required": {"complete": 0, "required": len(required)},
    "sealed_hash_valid_artifacts_required": {"valid": None, "required": None},
}
json.dump(counters, open(os.path.join(A, "counters.json"), "w", encoding="utf-8"), indent=1, sort_keys=True, ensure_ascii=False)
open(os.path.join(A, "counters.json"), "a", encoding="utf-8").write("\n")

print("head", HEAD, "rows", len(rows), "required", len(required))
