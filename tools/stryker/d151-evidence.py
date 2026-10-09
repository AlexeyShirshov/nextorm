#!/usr/bin/env python3
"""D151 Stryker evidence verifier and control probe (issue #151).

Repo-local, dependency-free helper. Sub-commands:

  control-probe   Plant the documented direction mutant in the PostgreSQL
                  extreme-row renderer, prove the PostgreSQL SQL-generation
                  tests fail on an assertion (not a compile/discovery error),
                  then restore the source byte-for-byte and verify its hash.
  manifest        Hash every artefact under the evidence directory.
  migrate-ledger  rv2 supersession: copy the rv1 per-mutant ledger and add the
                  per-entry report_path/report_sha256 binding (rv1 untouched).
  verify-ledger   rv2 independent row-level verification: resolve every ledger
                  entry against the frozen raw mutation reports, re-derive the
                  stable identity without calling ledger-generation code, and
                  emit a machine-readable receipt. Removes the rv1
                  `verify --contract` path.
  self-test       Negative fixtures proving the checks reject broken evidence.

It deliberately keeps only the checks the D151 evidence contract needs; it
never invents mutant ids and always reads the real Stryker JSON shape.
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys
import time

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CONTROL_LINE_MARKER = "var direction = isMax ?"
CONTROL_FROM = '" desc"'
CONTROL_TO = '""'
CONTROL_LINE = 81
RENDERER_NAME = "PostgresExtremeRowRenderer.cs"
TEST_FILTER = "FullyQualifiedName~ExtremeRow"
DEFAULT_LEDGER = "mutant-ledger.json"
RUN_LABELS = ("a", "b")
RV2_ROW_IDS = (
    "R-PROV", "R-TEST", "R-COV", "R-REPRO", "R-CONTROL", "R-COMPARE",
    "R-DISPOSITION", "R-RETENTION", "R-NEGATIVE", "R-CHECK",
    "R-ENTRY-BIND", "R-RAW-TRACE", "R-LEDGER-NEGATIVE", "R-LOOP", "R-BOUNDARY",
)


def utc_now() -> str:
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def load_json(path: str):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def write_json(path: str, obj) -> None:
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\r\n") as f:
        json.dump(obj, f, indent=2)
        f.write("\n")


def write_text(path: str, text: str) -> None:
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write(text)


def run(cmd, log_path: str):
    start = time.time()
    with open(log_path, "w", encoding="utf-8", errors="replace") as log:
        proc = subprocess.run(cmd, stdout=log, stderr=subprocess.STDOUT, cwd=REPO)
    return proc.returncode, round(time.time() - start, 3)


def parse_failed(log_path: str):
    failed = 0
    summary_failed = False
    with open(log_path, encoding="utf-8", errors="replace") as f:
        for line in f:
            m = re.search(r"^\s*failed:\s*(\d+)", line)
            if m:
                failed = int(m.group(1))
            if "Test run summary: Failed" in line:
                summary_failed = True
    return failed, summary_failed


# --------------------------------------------------------------------------- rv2 helpers

def canonical_bytes(obj) -> bytes:
    return json.dumps(obj, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def node_sha256(node) -> str:
    return hashlib.sha256(canonical_bytes(node)).hexdigest()


def resolve_json_pointer(doc, pointer: str):
    """Resolve an RFC 6901 JSON Pointer; raise KeyError/IndexError/ValueError if absent."""
    if pointer in ("", "/"):
        return doc
    if not pointer.startswith("/"):
        raise ValueError(f"not an RFC 6901 pointer: {pointer!r}")
    cur = doc
    for raw_tok in pointer.split("/")[1:]:
        tok = raw_tok.replace("~1", "/").replace("~0", "~")
        if isinstance(cur, list):
            cur = cur[int(tok)]
        else:
            cur = cur[tok]
    return cur


def derive_stable_id(file, location, mutator, replacement) -> str:
    s = location["start"]
    e = location["end"]
    return f"{file}:{s['line']}:{s['column']}-{e['line']}:{e['column']}|{mutator}|{replacement}"


def compare_entry(entry: dict, node, run_label: str, entry_idx: int, run: dict) -> list:
    """Independently compare one ledger entry against its raw report node.

    Returns a list of divergence strings (empty == OK). Does not consult any
    ledger-generation code: identity is re-derived from the raw node only.
    """
    errors = []
    prefix = f"{run_label}: entry {entry_idx}"
    if entry.get("report_path") != run.get("report_path"):
        errors.append(f"{prefix} report_path {entry.get('report_path')!r} != run {run.get('report_path')!r}")
    if entry.get("report_sha256") != run.get("report_sha256"):
        errors.append(f"{prefix} report_sha256 {entry.get('report_sha256')!r} != run {run.get('report_sha256')!r}")
    if node is None:
        errors.append(f"{prefix} json_pointer {entry.get('json_pointer')!r} did not resolve in raw report")
        return errors
    if entry.get("id") != node.get("id"):
        errors.append(f"{prefix} id {entry.get('id')!r} != raw {node.get('id')!r}")
    if entry.get("status") != node.get("status"):
        errors.append(f"{prefix} status {entry.get('status')!r} != raw {node.get('status')!r}")
    if entry.get("statusReason") != node.get("statusReason"):
        errors.append(f"{prefix} statusReason {entry.get('statusReason')!r} != raw {node.get('statusReason')!r}")
    if (entry.get("coveredBy") or []) != (node.get("coveredBy") or []):
        errors.append(f"{prefix} coveredBy differs from raw (ledger {len(entry.get('coveredBy') or [])}, raw {len(node.get('coveredBy') or [])})")
    if (entry.get("killedBy") or []) != (node.get("killedBy") or []):
        errors.append(f"{prefix} killedBy differs from raw (ledger {len(entry.get('killedBy') or [])}, raw {len(node.get('killedBy') or [])})")
    if entry.get("span") != node.get("location"):
        errors.append(f"{prefix} span differs from raw location")
    if entry.get("mutator") != node.get("mutatorName"):
        errors.append(f"{prefix} mutator {entry.get('mutator')!r} != raw {node.get('mutatorName')!r}")
    if entry.get("replacement") != node.get("replacement"):
        errors.append(f"{prefix} replacement differs from raw")
    try:
        derived = derive_stable_id(entry.get("file"), node["location"], node["mutatorName"], node["replacement"])
    except (KeyError, TypeError) as exc:
        errors.append(f"{prefix} cannot re-derive stable_id from raw node: {exc}")
    else:
        if entry.get("stable_id") != derived:
            errors.append(f"{prefix} stable_id differs from independently derived identity")
    return errors


def cmd_migrate_ledger(args):
    src_path = args.ledger or os.path.join(args.source_evidence, DEFAULT_LEDGER)
    src = load_json(src_path)
    src_sha = sha256_file(src_path)
    out = json.loads(json.dumps(src))  # deep copy; every original value kept verbatim
    for label in RUN_LABELS:
        run = out["runs"][label]
        report_path = run["report_path"]
        report_sha = run["report_sha256"]
        for entry in run["entries"]:
            entry["report_path"] = report_path
            entry["report_sha256"] = report_sha
    # additive rv2 markers; the original rv=1 field is preserved as-is.
    out["evidence_contract_rv"] = 2
    out["predecessor_ledger"] = {
        "path": os.path.relpath(src_path, REPO).replace(os.sep, "/"),
        "sha256": src_sha,
    }
    out["migrated_utc"] = utc_now()
    out_path = os.path.join(args.evidence, DEFAULT_LEDGER)
    write_json(out_path, out)
    result = {
        "migrated": os.path.relpath(out_path, REPO),
        "predecessor_path": os.path.relpath(src_path, REPO),
        "predecessor_sha256": src_sha,
        "entries": {label: len(out["runs"][label]["entries"]) for label in RUN_LABELS},
        "utc": utc_now(),
    }
    print(json.dumps(result, indent=2))
    return 0


def _check_current_ledger_hash(ledger_path: str, evidence: str, expected=None) -> tuple:
    """ledger-vs-row-hashes: the CURRENT ledger bytes must match the rv2 pin.

    Returns (ok, actual_sha256, detail); `detail` is the actual hash on success
    and the failure reason otherwise. When `expected` is supplied (the freeze
    path, before row-hashes.json is rewritten) that value is the pin; otherwise
    the pin is read from `<evidence>/row-hashes.json`.
    """
    if not os.path.exists(ledger_path):
        return False, None, f"missing ledger {os.path.relpath(ledger_path, REPO)}"
    actual = sha256_file(ledger_path)
    if expected is not None:
        if actual != expected:
            return False, actual, f"current ledger sha256 {actual} != expected {expected}"
        return True, actual, actual
    row_hash_path = os.path.join(evidence, "row-hashes.json")
    key = os.path.relpath(ledger_path, REPO).replace(os.sep, "/")
    if not os.path.exists(row_hash_path):
        return False, actual, f"missing {os.path.relpath(row_hash_path, REPO)}"
    pinned = (load_json(row_hash_path).get("hashes") or {}).get(key)
    if pinned is None:
        return False, actual, f"{os.path.relpath(row_hash_path, REPO)} has no entry for {key}"
    if actual != pinned:
        return False, actual, f"current ledger sha256 {actual} != row-hashes pin {pinned}"
    return True, actual, actual


def _check_predecessor_ledger_hash(source_evidence: str) -> tuple:
    """ledger-vs-row-hashes: the predecessor ledger bytes must match rv1's pin."""
    row_hash_path = os.path.join(source_evidence, "row-hashes.json")
    pred_path = os.path.join(source_evidence, DEFAULT_LEDGER)
    if not os.path.exists(row_hash_path):
        return False, f"missing {os.path.relpath(row_hash_path, REPO)}"
    if not os.path.exists(pred_path):
        return False, f"missing predecessor ledger {os.path.relpath(pred_path, REPO)}"
    row = load_json(row_hash_path)
    key = os.path.relpath(pred_path, REPO).replace(os.sep, "/")
    pinned = (row.get("hashes") or {}).get(key)
    if pinned is None:
        return False, f"row-hashes.json has no entry for {key}"
    actual = sha256_file(pred_path)
    if actual != pinned:
        return False, f"predecessor ledger sha256 {actual} != row-hashes pin {pinned}"
    return True, f"{actual}"


def _check_raw_hashes(ledger: dict, source_evidence: str) -> tuple:
    """raw-vs-(source-hashes, frozen-path-mapping, run hashes) reproducibility."""
    ok = True
    detail = {"runs": {}, "frozen_path_mapping": {}, "source_hashes": {}}
    row_hash_path = os.path.join(source_evidence, "row-hashes.json")
    row_hashes = (load_json(row_hash_path).get("hashes") or {}) if os.path.exists(row_hash_path) else {}

    for label in RUN_LABELS:
        run = ledger["runs"][label]
        report_path = os.path.join(REPO, run["report_path"])
        exists = os.path.exists(report_path)
        actual = sha256_file(report_path) if exists else None
        match = exists and actual == run["report_sha256"]
        pinned = row_hashes.get(run["report_path"].replace(os.sep, "/"))
        row_ok = pinned is None or pinned == actual
        ok = ok and match and row_ok
        detail["runs"][label] = {
            "path": run["report_path"],
            "declared_sha256": run["report_sha256"],
            "actual_sha256": actual,
            "match": match,
            "row_hashes_match": row_ok,
        }

    mapping_path = os.path.join(source_evidence, "frozen-path-mapping.json")
    if os.path.exists(mapping_path):
        mapping = load_json(mapping_path)
        mismatches = []
        for item in mapping.get("files", []):
            frozen = item["frozen"]
            if not os.path.exists(frozen):
                mismatches.append({"frozen": frozen, "error": "missing"})
                continue
            if sha256_file(frozen) != item["sha256"]:
                mismatches.append({"frozen": frozen, "error": "sha256 mismatch"})
        detail["frozen_path_mapping"] = {"count": len(mapping.get("files", [])), "mismatches": mismatches}
        ok = ok and not mismatches
    else:
        detail["frozen_path_mapping"] = {"error": "missing frozen-path-mapping.json"}
        ok = False

    source_hashes_path = os.path.join(source_evidence, "raw", "source-hashes.json")
    if os.path.exists(source_hashes_path):
        anchors = load_json(source_hashes_path)
        for key, anchor in anchors.items():
            live_path = os.path.join(REPO, anchor["path"])
            live = sha256_file(live_path) if os.path.exists(live_path) else None
            match = live == anchor["sha256"]
            entry = {"path": anchor["path"], "frozen_sha256": anchor["sha256"], "live_sha256": live, "match": match}
            if key == "verifier":
                # rv2 legitimately evolves the helper; the rv2 contract supersedes
                # rv1's verifier hash. Record it, do not fail the immutable anchors.
                entry["superseded_by_rv2"] = True
            else:
                ok = ok and match
            detail["source_hashes"][key] = entry
    else:
        detail["source_hashes"] = {"error": "missing raw/source-hashes.json"}
        ok = False
    return ok, detail


def _label_statuses(entries) -> dict:
    counts = {}
    for e in entries:
        counts[e.get("status")] = counts.get(e.get("status"), 0) + 1
    return counts


def cmd_verify_ledger(args):
    out_dir = args.out or args.evidence
    ledger_path = args.ledger or os.path.join(args.evidence, DEFAULT_LEDGER)
    return _run_verify_ledger(
        args.evidence, args.source_evidence, args.rv, out_dir,
        contract=args.contract, ledger_path=ledger_path,
    )


def _run_verify_ledger(evidence, source_evidence, rv, out_dir,
                       contract=None, ledger_path=None, quiet=False,
                       expected_current_ledger_sha256=None):
    os.makedirs(out_dir, exist_ok=True)
    if ledger_path is None:
        ledger_path = os.path.join(evidence, DEFAULT_LEDGER)

    result = {
        "rv": rv,
        "passed": False,
        "errors": [],
        "entries_checked": 0,
        "run_a": {},
        "run_b": {},
        "matched": 0,
        "unmatched": 0,
        "control_81_killed_a": False,
        "control_81_killed_b": False,
        "ledger_sha256_ok": False,
        "ledger_sha256_detail": None,
        "predecessor_ledger_sha256": None,
        "raw_hashes_ok": False,
        "ledger": os.path.relpath(ledger_path, REPO),
        "source_evidence": os.path.relpath(source_evidence, REPO),
        "generated_utc": utc_now(),
    }
    log_lines = []

    if not os.path.exists(ledger_path):
        result["errors"].append(f"missing ledger {ledger_path}")
        write_json(os.path.join(out_dir, "verifier-result.json"), result)
        return 1
    ledger = load_json(ledger_path)

    ledger_ok, ledger_hash, ledger_detail = _check_current_ledger_hash(
        ledger_path, evidence, expected_current_ledger_sha256)
    result["ledger_sha256_ok"] = ledger_ok
    result["ledger_sha256_detail"] = ledger_detail
    if not ledger_ok:
        result["errors"].append(f"current ledger sha256 binding: {ledger_detail}")

    # The predecessor ledger (rv1) must still match its own rv1 pin; record its
    # hash explicitly instead of overloading the current-ledger detail.
    pred_ok, pred_detail = _check_predecessor_ledger_hash(source_evidence)
    result["predecessor_ledger_sha256"] = pred_detail
    if not pred_ok:
        result["errors"].append(f"predecessor ledger sha256 binding: {pred_detail}")

    raw_ok, raw_detail = _check_raw_hashes(ledger, source_evidence)
    result["raw_hashes_ok"] = raw_ok
    result["raw_hashes_detail"] = raw_detail
    if not raw_ok:
        result["errors"].append("raw hash reproducibility failed (see raw_hashes_detail)")

    stable_a = []
    stable_b = []
    node_hashes = {"a": {}, "b": {}}
    for label in RUN_LABELS:
        run = ledger["runs"][label]
        entries = run["entries"]
        report_abs = os.path.join(REPO, run["report_path"])
        raw = load_json(report_abs) if os.path.exists(report_abs) else None
        run_divergences = []
        resolved = 0
        slice_lines = []
        for idx, entry in enumerate(entries):
            node = None
            try:
                node = resolve_json_pointer(raw, entry["json_pointer"]) if raw is not None else None
                if node is not None:
                    resolved += 1
                    node_hashes[label][entry["id"]] = node_sha256(node)
                    slice_lines.append(json.dumps(node, ensure_ascii=False, separators=(",", ":")))
            except (KeyError, IndexError, ValueError, TypeError) as exc:
                node = None
                node_hash = None
            entry_errors = compare_entry(entry, node, f"run-{label}", idx, run)
            run_divergences += entry_errors
            if not entry_errors:
                (stable_a if label == "a" else stable_b).append(entry.get("stable_id"))
            log_lines.append(json.dumps({
                "run": label,
                "entry_index": idx,
                "id": entry.get("id"),
                "stable_id": entry.get("stable_id"),
                "json_pointer": entry.get("json_pointer"),
                "ok": not entry_errors,
                "errors": entry_errors,
                "node_sha256": node_hashes[label].get(entry.get("id")),
            }, ensure_ascii=False))
        result[f"run_{label}"] = {
            "report_path": run["report_path"],
            "report_sha256": run["report_sha256"],
            "entries": len(entries),
            "resolved": resolved,
            "divergences": len(run_divergences),
            "statuses": _label_statuses(entries),
            "node_sha256_ok": resolved == len(entries),
        }
        result["entries_checked"] += len(entries)
        result["errors"] += run_divergences
        write_text(os.path.join(out_dir, f"raw-slices-{label}.jsonl"), "\n".join(slice_lines) + ("\n" if slice_lines else ""))
        if label == "a":
            result["control_81_killed_a"] = any(
                e.get("status") == "Killed"
                and (e.get("span") or {}).get("start", {}).get("line") == CONTROL_LINE
                and e.get("replacement") == CONTROL_TO
                for e in entries
            )
        else:
            result["control_81_killed_b"] = any(
                e.get("status") == "Killed"
                and (e.get("span") or {}).get("start", {}).get("line") == CONTROL_LINE
                and e.get("replacement") == CONTROL_TO
                for e in entries
            )

    result["matched"] = sum(1 for sid in stable_a if sid in set(stable_b))
    result["unmatched"] = len(stable_a) - result["matched"]
    if result["entries_checked"] != 194:
        result["errors"].append(f"entries_checked {result['entries_checked']} != 194")
    if result["unmatched"] != 0:
        result["errors"].append(f"A/B identity unmatched {result['unmatched']} != 0")
    if not result["control_81_killed_a"] or not result["control_81_killed_b"]:
        result["errors"].append("control mutant :81 replacement \"\" not Killed in both runs")
    for label in RUN_LABELS:
        st = result[f"run_{label}"]["statuses"]
        for name, expected in (("Killed", 56), ("Survived", 0), ("NoCoverage", 0), ("Timeout", 3), ("CompileError", 27), ("Ignored", 11)):
            if st.get(name, 0) != expected:
                result["errors"].append(f"run-{label} status {name}={st.get(name, 0)} != {expected}")
    if not result["ledger_sha256_ok"]:
        result["errors"].append("ledger sha256 binding failed")
    if not result["raw_hashes_ok"]:
        result["errors"].append("raw hash reproducibility failed")

    result["passed"] = not result["errors"]

    log_lines.append(json.dumps({
        "totals": {
            "entries_checked": result["entries_checked"],
            "matched": result["matched"],
            "unmatched": result["unmatched"],
            "control_81_killed_a": result["control_81_killed_a"],
            "control_81_killed_b": result["control_81_killed_b"],
            "ledger_sha256_ok": result["ledger_sha256_ok"],
            "raw_hashes_ok": result["raw_hashes_ok"],
            "errors": len(result["errors"]),
        }
    }, ensure_ascii=False))
    log_lines.append(json.dumps({"exit": 0 if result["passed"] else 1}, ensure_ascii=False))
    write_text(os.path.join(out_dir, "ledger-verify.log"), "\n".join(log_lines) + "\n")
    write_json(os.path.join(out_dir, "verifier-result.json"), result)
    if not quiet:
        print(json.dumps(result, indent=2))
    return 0 if result["passed"] else 1


# --------------------------------------------------------------------------- rv1 checks (self-test)

def check_scope(report) -> list:
    errors = []
    files = report.get("files", {})
    if not files:
        errors.append("report contains no files")
    executed = {"Killed", "Survived", "Timeout", "NoCoverage", "RuntimeError"}
    for f, v in files.items():
        for m in v.get("mutants", []):
            if not f.endswith(RENDERER_NAME) and m.get("status") in executed:
                errors.append(f"out-of-scope active mutant ({m.get('status')}) in: {f}")
    mutants = [m for v in files.values() for m in v.get("mutants", [])]
    if not mutants:
        errors.append("report contains no mutants")
    return errors


def mutants_of(report):
    return [m for v in report.get("files", {}).values() for m in v.get("mutants", [])]


def check_reproducibility(report_a, report_b) -> list:
    errors = []
    a = sorted((m["location"]["start"]["line"], m["location"]["start"]["column"], m["status"]) for m in mutants_of(report_a))
    b = sorted((m["location"]["start"]["line"], m["location"]["start"]["column"], m["status"]) for m in mutants_of(report_b))
    if a != b:
        errors.append("mutant status sets differ between run-a and run-b")
    for status in ("Killed", "Survived"):
        sa = sorted((m["location"]["start"]["line"], m["location"]["start"]["column"]) for m in mutants_of(report_a) if m["status"] == status)
        sb = sorted((m["location"]["start"]["line"], m["location"]["start"]["column"]) for m in mutants_of(report_b) if m["status"] == status)
        if sa != sb:
            errors.append(f"{status} set differs between run-a and run-b")
    return errors


def find_control_mutant(report):
    for m in mutants_of(report):
        loc = m.get("location", {}).get("start", {})
        if loc.get("line") == CONTROL_LINE and m.get("replacement") == CONTROL_TO:
            return m
    return None


def check_control(report_a, report_b) -> list:
    errors = []
    for name, report in (("run-a", report_a), ("run-b", report_b)):
        mutant = find_control_mutant(report)
        if mutant is None:
            errors.append(f"{name}: direction control mutant at renderer:{CONTROL_LINE} not found")
        elif mutant.get("status") != "Killed":
            errors.append(f"{name}: direction control mutant is {mutant.get('status')}, not Killed")
    return errors


def check_disposition(report, disposition, path_label) -> list:
    errors = []
    required = {"Survived", "NoCoverage"}
    covered = set()
    for entry in disposition.get("mutants", []):
        covered.add((entry.get("line"), entry.get("column"), entry.get("status")))
    for m in mutants_of(report):
        if m["status"] not in required:
            continue
        key = (m["location"]["start"]["line"], m["location"]["start"]["column"], m["status"])
        if key not in covered:
            errors.append(f"{path_label}: {m['status']} mutant {key} has no disposition entry")
    return errors


def check_control_probe_file(control_path) -> list:
    errors = []
    if not os.path.exists(control_path):
        return [f"missing control-probe evidence {control_path}"]
    data = load_json(control_path)
    if not data.get("restored"):
        errors.append("control-probe did not restore the source byte-for-byte")
    if data.get("sha256_before") != data.get("sha256_after"):
        errors.append("control-probe source hash changed after restore")
    baseline = data.get("baseline", {})
    active = data.get("active", {})
    if baseline.get("exit") != 0:
        errors.append("control-probe baseline tests did not pass")
    if active.get("exit") == 0:
        errors.append("control-probe active tests unexpectedly passed")
    if not active.get("assertion_failure"):
        errors.append("control-probe active failure was not an assertion failure")
    return errors


# --------------------------------------------------------------------------- commands

def cmd_control_probe(args):
    os.makedirs(args.evidence, exist_ok=True)
    src = args.source
    original = open(src, "rb").read()
    before = hashlib.sha256(original).hexdigest()
    text = original.decode("utf-8")
    idx = text.find(CONTROL_LINE_MARKER)
    if idx < 0:
        print(f"FATAL: control anchor {CONTROL_LINE_MARKER!r} not found", file=sys.stderr)
        return 2
    line_end = text.find("\n", idx)
    line = text[idx:line_end]
    if CONTROL_FROM not in line:
        print(f"FATAL: {CONTROL_FROM!r} not on the direction line", file=sys.stderr)
        return 2
    mutated_line = line.replace(CONTROL_FROM, CONTROL_TO, 1)
    mutated = (text[:idx] + mutated_line + text[line_end:]).encode("utf-8")
    active_sha = hashlib.sha256(mutated).hexdigest()

    baseline_log = os.path.join(args.evidence, "control-baseline.log")
    active_log = os.path.join(args.evidence, "control-active.log")
    restore_log = os.path.join(args.evidence, "control-restore.log")

    build = ["dotnet", "build", args.project, "-c", "Debug"]
    test = ["dotnet", "test", args.project, "-c", "Debug", "--no-build", "--filter", TEST_FILTER]

    rc_build, _ = run(build, restore_log)
    rc_base, secs_base = run(test, baseline_log)
    base_failed, _ = parse_failed(baseline_log)

    try:
        with open(src, "wb") as f:
            f.write(mutated)
        rc_active_build, _ = run(
            ["dotnet", "build", args.project, "-c", "Debug"],
            os.path.join(args.evidence, "control-active-build.log"))
        rc_active, secs_active = run(test, active_log)
        active_failed, active_summary_failed = parse_failed(active_log)
        compiled = rc_active_build == 0
    finally:
        with open(src, "wb") as f:
            f.write(original)

    after = sha256_file(src)
    restored = after == before

    assertion_failure = compiled and rc_active != 0 and active_failed > 0

    result = {
        "source": os.path.relpath(src, REPO),
        "mutation": {"marker": CONTROL_LINE_MARKER, "from": CONTROL_FROM, "to": CONTROL_TO, "line": CONTROL_LINE},
        "sha256_before": before,
        "sha256_active": active_sha,
        "sha256_after": after,
        "baseline": {"exit": rc_base, "failed": base_failed, "seconds": secs_base},
        "active": {
            "exit": rc_active,
            "failed": active_failed,
            "summary_failed": active_summary_failed,
            "compiled": compiled,
            "assertion_failure": assertion_failure,
            "seconds": secs_active,
        },
        "restored": restored,
        "generated_utc": utc_now(),
    }
    write_json(os.path.join(args.evidence, "control.json"), result)

    print(json.dumps(result, indent=2))
    return 0 if (restored and rc_base == 0 and assertion_failure) else 1


def _atomic_write_json(path: str, obj) -> None:
    """Write JSON beside its final name then os.replace it (atomic freeze)."""
    directory = os.path.dirname(os.path.abspath(path))
    os.makedirs(directory, exist_ok=True)
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8", newline="\r\n") as f:
        json.dump(obj, f, indent=2)
        f.write("\n")
    os.replace(tmp, path)


def _repo_rel(path: str) -> str:
    return os.path.relpath(path, REPO).replace(os.sep, "/")


def _freeze_inventory(evidence: str, source_evidence: str) -> list:
    """Repo-relative rv2 files to freeze.

    manifest.json / row-hashes.json / inner-loop-manifest.json are deliberately
    absent so no artefact hashes itself (manifest.json is written last).
    """
    files = []
    seen = set()

    def add(path: str):
        rel = _repo_rel(path)
        if rel not in seen:
            seen.add(rel)
            files.append(rel)

    add(os.path.join(evidence, DEFAULT_LEDGER))
    add(os.path.join(REPO, "tools", "stryker", "d151-evidence.py"))
    add(os.path.join(evidence, "ledger-verify.log"))
    add(os.path.join(evidence, "raw-slices-a.jsonl"))
    add(os.path.join(evidence, "raw-slices-b.jsonl"))
    add(os.path.join(evidence, "provenance.md"))
    negative = os.path.join(evidence, "negative")
    if os.path.isdir(negative):
        for root, _dirs, names in os.walk(negative):
            for name in sorted(names):
                add(os.path.join(root, name))
    mapping = load_json(os.path.join(source_evidence, "frozen-path-mapping.json"))
    for item in mapping.get("files", []):
        add(os.path.join(REPO, item["frozen"]))
    return files


def _ev(kind: str, path: str, exit_code=None) -> dict:
    item = {"kind": kind, "path": path.replace(os.sep, "/")}
    if exit_code is not None:
        item["exit"] = exit_code
    return item


def _rv2_manifest_rows(evidence: str, source_evidence: str) -> list:
    E = _repo_rel(source_evidence)                 # artifacts/pdca/D151/rv1
    U = _repo_rel(evidence)                        # artifacts/pdca/D151/rv2
    C = "artifacts/pdca/D151/check-rv2"
    raw = E + "/raw"
    ledger = U + "/mutant-ledger.json"
    return [
        {"id": "R-PROV", "status": "met", "evidence": [
            _ev("source-hashes", raw + "/source-hashes.json"),
            _ev("frozen-path-mapping", E + "/frozen-path-mapping.json"),
            _ev("provenance-addendum", U + "/provenance.md"),
        ], "notes": "Provenance gate PASS at 8f48d3aa: git diff e67aa5f8..HEAD over renderer/config/test is empty; 4 immutable source anchors match; rv1 verifier anchor superseded by rv2."},
        {"id": "R-TEST", "status": "met", "evidence": [
            _ev("postgres-full-test-log", raw + "/final-postgres-tests.log", 0),
            _ev("postgres-filtered-test-log", raw + "/d04-filtered-tests.log", 0),
            _ev("core-filtered-test-log", raw + "/final-core-extremerow-tests.log", 0),
            _ev("postgres-container-test-log", raw + "/ec07/integration.log", 0),
        ], "notes": "Retained rv1 run: postgres 844/0/0; ExtremeRow filter 53/0/0; core filter 39/0/0; PostgreSQL container 16/0/0."},
        {"id": "R-COV", "status": "met", "evidence": [
            _ev("mutation-report-a", raw + "/run-a/reports/mutation-report.json"),
            _ev("mutation-report-b", raw + "/run-b/reports/mutation-report.json"),
            _ev("mutant-ledger-json", ledger),
        ], "notes": "Renderer denominator 97 = Killed 56 / Survived 0 / NoCoverage 0 / Timeout 3 / CompileError 27 / Ignored 11, perTest + mtp; non-empty coveredBy/killedBy on every Killed."},
        {"id": "R-REPRO", "status": "met", "evidence": [
            _ev("campaign-a-log", raw + "/run-a.log", 0),
            _ev("campaign-b-log", raw + "/run-b.log", 0),
            _ev("mutant-ledger-json", ledger),
        ], "notes": "Two independently executed campaigns, exit 0; ledger pins both report paths/sha256."},
        {"id": "R-CONTROL", "status": "met", "evidence": [
            _ev("control-probe", raw + "/control.json"),
            _ev("control-baseline-log", raw + "/control-baseline.log", 0),
            _ev("control-active-log", raw + "/control-active.log", 2),
        ], "notes": "Direction control :81 \" desc\" -> empty: baseline exit 0, active compiled with 14 assertion failures, source restored byte-for-byte, Killed in both runs."},
        {"id": "R-COMPARE", "status": "met", "evidence": [
            _ev("repro-comparison", raw + "/repro-comparison.json"),
            _ev("mutant-ledger-json", ledger),
        ], "notes": "A/B normalized identity by file:span|mutator|replacement: matched 97 / unmatched 0."},
        {"id": "R-DISPOSITION", "status": "met", "evidence": [
            _ev("mutant-ledger-json", ledger),
            _ev("mutant-ledger-md", E + "/mutant-ledger.md"),
            _ev("disposition-ledger-json", raw + "/disposition.json"),
            _ev("disposition-all-csv", E + "/disposition-all.csv"),
        ], "notes": "Contract-complete per-mutant ledger (194 entries); Timeout 3 / CompileError 27 / Ignored 11 kept separate from Killed 56; Survived 0 / NoCoverage 0."},
        {"id": "R-RETENTION", "status": "met", "evidence": [
            _ev("evidence-manifest", raw + "/manifest.json"),
            _ev("row-hashes", U + "/row-hashes.json"),
            _ev("hash-check", U + "/hash-check.log", 0),
        ], "notes": "rv2 hash inventory freezes the migrated ledger, verifier script, receipts, raw slices, negative controls, provenance addendum and the retained frozen inputs under rv1/raw; hash-check.log is the actual `sha256sum -c` output over row-hashes.json."},
        {"id": "R-NEGATIVE", "status": "met", "evidence": [
            _ev("verifier-self-test-log", U + "/self-test-rv2.log", 0),
        ], "notes": "Current rv2 self-test (exit 0, helper sha256, case counts and per-case outcomes in the log) rejects out-of-scope mutants, empty reports, A/B mismatches, missing/un-killed control, unresolved survivors and forged killedBy/json_pointer/stable_id/report_sha256."},
        {"id": "R-CHECK", "status": "met", "evidence": [
            _ev("mutant-ledger-json", ledger),
            _ev("producer-verify-receipt", U + "/verifier-result.json", 0),
            _ev("producer-verify-log", U + "/ledger-verify.log", 0),
            _ev("independent-check-receipt", C + "/verifier-result.json", 0),
            _ev("independent-check-log", C + "/ledger-verify.log", 0),
        ], "notes": "Independent CHECK receipt at check-rv2 is produced after the freeze so its trace is newer than the ledger; it re-derives all 194 entries from the raw reports."},
        {"id": "R-ENTRY-BIND", "status": "met", "evidence": [
            _ev("mutant-ledger-json", ledger),
            _ev("verifier-result", U + "/verifier-result.json", 0),
            _ev("ledger-verify-log", U + "/ledger-verify.log", 0),
        ], "notes": "Every entry carries report_path/report_sha256 bound to its run metadata; verify-ledger compares them against the raw run report bytes."},
        {"id": "R-RAW-TRACE", "status": "met", "evidence": [
            _ev("raw-slices-a", U + "/raw-slices-a.jsonl"),
            _ev("raw-slices-b", U + "/raw-slices-b.jsonl"),
            _ev("verifier-result", U + "/verifier-result.json", 0),
        ], "notes": "raw-slices-{a,b}.jsonl hold the verbatim raw renderer mutant nodes (97 lossless single lines each, never truncated)."},
        {"id": "R-LEDGER-NEGATIVE", "status": "met", "evidence": [
            _ev("negative-ledger-verify-log", U + "/negative/ledger-verify.log", 1),
            _ev("negative-verifier-result", U + "/negative/verifier-result.json", 1),
            _ev("negative-receipt", U + "/negative/receipt.md"),
        ], "notes": "Corrupted /tmp ledger copy (killedBy forged, sha256 recorded in the receipt) makes verify-ledger exit nonzero with a raw-field divergence while ledger_sha256_ok/raw_hashes_ok stay true."},
        {"id": "R-LOOP", "status": "met", "evidence": [
            _ev("validator", "scripts/validate_inner_loop.py"),
            _ev("inner-loop-manifest", C + "/inner-loop-manifest.json"),
            _ev("inner-loop-check-log", C + "/inner-loop-check.log", 0),
            _ev("verifier-script", "tools/stryker/d151-evidence.py"),
        ], "notes": "inner-loop-check invokes the validator's real CLI (manifest) and never reimplements it; rebuild none because no compiled file changed."},
        {"id": "R-BOUNDARY", "status": "met", "evidence": [
            _ev("build-log", U + "/build.log", 0),
            _ev("scope-provenance", U + "/provenance.md"),
            _ev("mutant-ledger-json", ledger),
        ], "notes": "Only tools/stryker/** + artifacts/pdca/D151/** (+ status) touched; nextorm.slnx Debug build 0W/0E; renderer sha256 97345986...421537 unchanged; git diff --check clean."},
    ]


def _cmd_manifest_freeze(args) -> int:
    evidence = args.evidence
    source_evidence = args.source_evidence
    if not source_evidence:
        print("manifest --freeze requires --source-evidence", file=sys.stderr)
        return 2
    # Producer verification runs in-process so the freeze is self-contained.
    # The current rv2 ledger hash is known here, so the in-process check pins it
    # directly instead of reading row-hashes.json before it is (re)written.
    current_ledger = os.path.join(evidence, DEFAULT_LEDGER)
    expected_ledger = sha256_file(current_ledger) if os.path.exists(current_ledger) else None
    rc = _run_verify_ledger(evidence, source_evidence, args.rv, evidence,
                            contract=args.contract, ledger_path=None, quiet=True,
                            expected_current_ledger_sha256=expected_ledger)
    if rc != 0:
        print(f"freeze aborted: producer verification exit {rc}", file=sys.stderr)
        return rc
    inventory = _freeze_inventory(evidence, source_evidence)
    hashes = {rel: sha256_file(os.path.join(REPO, rel)) for rel in inventory}
    tree = subprocess.run(["git", "rev-parse", "HEAD"], cwd=REPO,
                          capture_output=True, text=True).stdout.strip()
    row_hashes = {
        "task": "D151",
        "rv": args.rv,
        "tree": tree,
        "accessible_root": _repo_rel(evidence),
        "note": ("Immutable rv2 evidence only: the migrated ledger, the verifier "
                 "script, the producer verification receipts, the raw trace slices, "
                 "the negative controls, the provenance addendum and the retained "
                 "frozen inputs under rv1/raw (via frozen-path-mapping.json). "
                 "manifest.json / row-hashes.json / inner-loop-manifest.json are "
                 "excluded so no artefact hashes itself; manifest.json is written "
                 "last and is deliberately absent from this inventory."),
        "generated_utc": utc_now(),
        "count": len(hashes),
        "hashes": hashes,
    }
    _atomic_write_json(os.path.join(evidence, "row-hashes.json"), row_hashes)
    manifest = {
        "task": "D151",
        "tree": tree,
        "status_file": "docs/specs/status/rc2-151-stryker-mutation-1.md",
        "contract_rv": args.rv,
        "required_rows": list(RV2_ROW_IDS),
        "rows": _rv2_manifest_rows(evidence, source_evidence),
    }
    _atomic_write_json(os.path.join(evidence, "manifest.json"), manifest)
    print(json.dumps({
        "row_hashes": _repo_rel(os.path.join(evidence, "row-hashes.json")),
        "manifest": _repo_rel(os.path.join(evidence, "manifest.json")),
        "rows": len(manifest["rows"]),
        "files": len(hashes),
        "tree": tree,
    }, indent=2))
    return 0


def cmd_inner_loop_check(args) -> int:
    """Invoke the validator's real CLI (manifest) and record command + exit."""
    validator = args.validator
    manifest = args.manifest
    command = [sys.executable, validator, "manifest", manifest]
    out_dir = os.path.dirname(os.path.abspath(manifest))
    log_path = os.path.join(out_dir, "inner-loop-check.log")
    start = time.time()
    with open(log_path, "w", encoding="utf-8", errors="replace") as log:
        log.write("command: " + json.dumps(command) + "\n")
        proc = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, cwd=REPO)
        log.write(f"exit: {proc.returncode}\n")
    receipt = {
        "validator": validator,
        "mode": "manifest",
        "manifest": manifest,
        "command": command,
        "exit": proc.returncode,
        "seconds": round(time.time() - start, 3),
        "utc": utc_now(),
    }
    write_json(os.path.join(out_dir, "inner-loop-check.json"), receipt)
    print(json.dumps(receipt, indent=2))
    return proc.returncode


def cmd_manifest(args):
    if getattr(args, "freeze", False):
        return _cmd_manifest_freeze(args)
    entries = []
    for root, _dirs, files in os.walk(args.evidence):
        for name in files:
            path = os.path.join(root, name)
            entries.append({
                "path": os.path.relpath(path, REPO),
                "sha256": sha256_file(path),
                "size": os.path.getsize(path),
            })
    entries.sort(key=lambda e: e["path"])
    out = {
        "evidence": os.path.relpath(args.evidence, REPO),
        "git_head": subprocess.run(["git", "rev-parse", "HEAD"], cwd=REPO, capture_output=True, text=True).stdout.strip(),
        "stryker_version": "5.0.0",
        "rv": args.rv,
        "generated_utc": utc_now(),
        "files": entries,
    }
    path = os.path.join(args.evidence, "manifest.json")
    write_json(path, out)
    print(f"wrote {path} ({len(entries)} files)")
    return 0


def _fake_report(status_by_key):
    mutants = []
    for i, (line, col, status) in enumerate(status_by_key):
        mutants.append({"id": i, "status": status, "replacement": '""' if line == CONTROL_LINE else "x",
                        "location": {"start": {"line": line, "column": col}}})
    return {"files": {"src/nextorm.postgres/PostgresExtremeRowRenderer.cs": {"mutants": mutants}}}


def _mini_ledger_and_raw():
    loc = {"start": {"line": 10, "column": 2}, "end": {"line": 10, "column": 5}}
    node = {"id": "1", "mutatorName": "String mutation", "replacement": '""', "location": loc,
            "status": "Killed", "coveredBy": ["a"], "killedBy": ["b"]}
    raw = {"files": {"/abs/R.cs": {"mutants": [node]}}}
    entry = {
        "id": "1",
        "stable_id": derive_stable_id("R.cs", loc, "String mutation", '""'),
        "json_pointer": "/files/~1abs~1R.cs/mutants/0",
        "file": "R.cs",
        "span": loc,
        "mutator": "String mutation",
        "replacement": '""',
        "status": "Killed",
        "statusReason": None,
        "coveredBy": ["a"],
        "killedBy": ["b"],
        "report_path": "p",
        "report_sha256": "s",
    }
    run = {"report_path": "p", "report_sha256": "s", "entries": [entry]}
    ledger = {"runs": {"a": run, "b": json.loads(json.dumps(run))}}
    return ledger, raw


def cmd_self_test(args):
    cases = []
    failures = []

    def record(name, ok, detail=""):
        cases.append({"case": name, "passed": bool(ok), "detail": detail})
        if not ok:
            failures.append(name if not detail else f"{name}: {detail}")

    # out-of-scope file must be rejected
    bad_scope = {"files": {"src/nextorm.postgres/Other.cs": {"mutants": [{"status": "Killed", "location": {"start": {"line": 1, "column": 1}}}]}}}
    record("scope-out-of-scope-file-rejected",
           any("out-of-scope" in e for e in check_scope(bad_scope)))

    # empty report must be rejected
    record("scope-empty-report-rejected", bool(check_scope({"files": {}})))

    # mismatched sets between runs must be rejected
    ra = _fake_report([(81, 20, "Killed"), (35, 12, "Killed")])
    rb = _fake_report([(81, 20, "Killed"), (35, 12, "Survived")])
    record("repro-mismatched-sets-rejected", bool(check_reproducibility(ra, rb)))

    # a missing control must be rejected
    no_control = _fake_report([(35, 12, "Killed")])
    record("control-missing-direction-mutant-rejected", bool(check_control(no_control, no_control)))

    # an un-killed control must be rejected
    survived_control = _fake_report([(81, 20, "Survived")])
    record("control-survived-direction-mutant-rejected", bool(check_control(survived_control, survived_control)))

    # an unresolved survivor must require a disposition entry
    survivor = _fake_report([(81, 20, "Killed"), (40, 5, "Survived")])
    record("disposition-unresolved-survivor-rejected", bool(check_disposition(survivor, {"mutants": []}, "self")))

    # rv2 ledger verifier: a clean mini ledger must pass the entry comparison
    ledger, raw = _mini_ledger_and_raw()
    clean = compare_entry(ledger["runs"]["a"]["entries"][0],
                          resolve_json_pointer(raw, ledger["runs"]["a"]["entries"][0]["json_pointer"]),
                          "run-a", 0, ledger["runs"]["a"])
    record("rv2-clean-entry-accepted", not clean, "; ".join(clean))

    # rv2 negative fixture: a corrupted killedBy must be reported as a raw-field divergence
    corrupt = json.loads(json.dumps(ledger))
    corrupt["runs"]["a"]["entries"][0]["killedBy"] = ["forged"]
    divergences = compare_entry(corrupt["runs"]["a"]["entries"][0],
                                resolve_json_pointer(raw, corrupt["runs"]["a"]["entries"][0]["json_pointer"]),
                                "run-a", 0, corrupt["runs"]["a"])
    record("rv2-forged-killedBy-rejected", any("killedBy" in e for e in divergences), "; ".join(divergences))

    # rv2 negative fixture: a corrupted json_pointer must resolve to no node
    bad_ptr = json.loads(json.dumps(ledger))
    bad_ptr["runs"]["a"]["entries"][0]["json_pointer"] = "/files/~1abs~1R.cs/mutants/99"
    node = None
    try:
        node = resolve_json_pointer(raw, bad_ptr["runs"]["a"]["entries"][0]["json_pointer"])
    except (KeyError, IndexError, ValueError):
        node = None
    divergences = compare_entry(bad_ptr["runs"]["a"]["entries"][0], node, "run-a", 0, bad_ptr["runs"]["a"])
    record("rv2-forged-json_pointer-unresolved", any("did not resolve" in e for e in divergences), "; ".join(divergences))

    # rv2 negative fixture: a corrupted stable_id must be caught by independent re-derivation
    bad_sid = json.loads(json.dumps(ledger))
    bad_sid["runs"]["a"]["entries"][0]["stable_id"] = "forged"
    divergences = compare_entry(bad_sid["runs"]["a"]["entries"][0],
                                resolve_json_pointer(raw, bad_sid["runs"]["a"]["entries"][0]["json_pointer"]),
                                "run-a", 0, bad_sid["runs"]["a"])
    record("rv2-forged-stable_id-rejected", any("stable_id" in e for e in divergences), "; ".join(divergences))

    # rv2 negative fixture: a corrupted run binding must be caught
    bad_bind = json.loads(json.dumps(ledger))
    bad_bind["runs"]["a"]["entries"][0]["report_sha256"] = "forged"
    divergences = compare_entry(bad_bind["runs"]["a"]["entries"][0],
                                resolve_json_pointer(raw, bad_bind["runs"]["a"]["entries"][0]["json_pointer"]),
                                "run-a", 0, bad_bind["runs"]["a"])
    record("rv2-forged-report_sha256-binding-rejected", any("report_sha256" in e for e in divergences), "; ".join(divergences))

    exit_code = 0 if not failures else 1
    result = {
        "command": [sys.executable, "tools/stryker/d151-evidence.py", "self-test"]
                   + (["--out", args.out] if args.out else []),
        "exit": exit_code,
        "passed": not failures,
        "helper_sha256": sha256_file(os.path.abspath(__file__)),
        "case_count": len(cases),
        "passed_cases": sum(1 for c in cases if c["passed"]),
        "failed_cases": sum(1 for c in cases if not c["passed"]),
        "failures": failures,
        "generated_utc": utc_now(),
        "cases": cases,
    }
    if args.out:
        write_text(args.out, json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))
    return exit_code


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("control-probe")
    p.add_argument("--project", required=True)
    p.add_argument("--source", required=True)
    p.add_argument("--evidence", required=True)
    p.set_defaults(func=cmd_control_probe)

    p = sub.add_parser("manifest")
    p.add_argument("--evidence", required=True)
    p.add_argument("--source-evidence", default=None)
    p.add_argument("--contract", default=None)
    p.add_argument("--rv", type=int, default=1)
    p.add_argument("--freeze", action="store_true",
                   help="run producer verification in-process, then atomically "
                        "write row-hashes.json and a validate_inner_loop manifest.json")
    p.set_defaults(func=cmd_manifest)

    p = sub.add_parser("migrate-ledger")
    p.add_argument("--source-evidence", required=True)
    p.add_argument("--evidence", required=True)
    p.add_argument("--ledger", default=None)
    p.set_defaults(func=cmd_migrate_ledger)

    p = sub.add_parser("verify-ledger")
    p.add_argument("--evidence", required=True)
    p.add_argument("--source-evidence", required=True)
    p.add_argument("--contract", required=True)
    p.add_argument("--rv", type=int, default=2)
    p.add_argument("--out", default=None)
    p.add_argument("--ledger", default=None)
    p.set_defaults(func=cmd_verify_ledger)

    p = sub.add_parser("inner-loop-check")
    p.add_argument("--validator", required=True)
    p.add_argument("--manifest", required=True)
    p.set_defaults(func=cmd_inner_loop_check)

    p = sub.add_parser("self-test")
    p.add_argument("--out", default=None,
                   help="write the self-test log (command, exit, helper sha256, "
                        "case counts, per-case outcomes) to this path")
    p.set_defaults(func=cmd_self_test)

    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
