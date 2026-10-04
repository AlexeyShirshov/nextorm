#!/usr/bin/env python3
"""Iteration 15 / #183 Stage A evidence runner (manual, dependency-free).

Validates the Stage A evidence already recorded under
``benchmarks/BenchmarkDotNet.Artifacts/iteration15-stage-a/`` and described by
``docs/specs/performance/iteration-15-stage-a-manifest.json``.  It never
re-runs the benchmark suite: it verifies the recorded acceptance log, the
diagnostics / correctness JSON batches and the Stage A scope (no
``src/nextorm.core`` diff).  It is **not** a replacement for the absent
``scripts/validate_inner_loop.py``.

Missing, short or failed evidence is never converted into success.

Subcommands::

    perf --stage A             # 7-case / 0-failure acceptance log + ratios
    correctness --scope boundary
                               # boundary correctness + no sticky Cache=false
    scope --base <commit>      # git scope / G1-CTE predicate for CteHoister

Every invocation echoes the full nested command (argv and a shell form) and its
exit code; commands this script actually runs additionally echo duration and
stdout/stderr, and a JSON record is persisted under ``runner-runs/``.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = (
    REPO_ROOT / "docs" / "specs" / "performance" / "iteration-15-stage-a-manifest.json"
)
ARTIFACTS_DIR = (
    REPO_ROOT / "benchmarks" / "BenchmarkDotNet.Artifacts" / "iteration15-stage-a"
)
RUNS_DIR = ARTIFACTS_DIR / "runner-runs"

EXPECTED_CASES = 7
ACCEPTANCE_CATEGORY_FLAG = "--anyCategories=acceptance"
# The full, pinned set of canonical acceptance methods (never a subset).
ACCEPTANCE_METHODS = (
    "Nextorm_Count",
    "Nextorm_GroupByCount",
    "Nextorm_Cached",
    "Cached_PlanOnly_Param",
    "Prepared_ToList",
    "Cached_ToList",
    "Nextorm_Cached_ToListAsync",
)
PREPARED = "Prepared_ToList"
HIT_ARMS = ("where", "join")
BOUNDARY_KEYS = (
    "where_sql_match",
    "join_sql_match",
    "where_rows_match",
    "join_rows_match",
    "any",
    "first",
    "single",
    "where_rows",
    "sticky_cache_false_absent",
    "all_pass",
)
COMMAND_TIMEOUT_SECONDS = 120


class EvidenceError(RuntimeError):
    """Fatal validation error; reported as a non-zero exit code (never success)."""


# --------------------------------------------------------------------------- helpers


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def resolve_path(value) -> Path:
    path = Path(str(value))
    return path if path.is_absolute() else (REPO_ROOT / path)


def load_manifest(path: Path) -> dict:
    if not path.exists():
        raise EvidenceError(f"manifest not found: {path}")
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise EvidenceError(f"cannot read manifest {path}: {exc}") from exc
    if not isinstance(data, dict):
        raise EvidenceError(f"manifest {path} is not a JSON object")
    return data


def load_json(path: Path, what: str) -> dict:
    if not path.exists():
        raise EvidenceError(f"{what} not found: {path}")
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise EvidenceError(f"cannot read {what} {path}: {exc}") from exc
    if not isinstance(data, dict):
        raise EvidenceError(f"{what} {path} is not a JSON object")
    return data


def find_manifest_path(obj, suffix: str):
    """Recursively look for a string value ending with *suffix* in the manifest."""
    if isinstance(obj, str):
        return obj if obj.endswith(suffix) else None
    if isinstance(obj, dict):
        for value in obj.values():
            found = find_manifest_path(value, suffix)
            if found:
                return found
    elif isinstance(obj, list):
        for value in obj:
            found = find_manifest_path(value, suffix)
            if found:
                return found
    return None


def artifacts_dir(manifest: dict) -> Path:
    """Derive the evidence directory from the manifest's recorded acceptance log."""
    log = (manifest.get("acceptanceCanonical") or {}).get("log")
    if log:
        return resolve_path(log).parent
    return ARTIFACTS_DIR


def locate_evidence(manifest: dict, filename: str, directory: Path) -> Path:
    recorded = find_manifest_path(manifest, filename)
    if recorded:
        candidate = resolve_path(recorded)
        if candidate.exists():
            return candidate
    return directory / filename


def require_pinned_evidence(manifest: dict, key: str, what: str) -> Path:
    """Fail-closed: the manifest must pin *key*'s path and sha256, and the file must match it."""
    pinned = (manifest.get("evidenceFiles") or {}).get(key)
    if not isinstance(pinned, dict):
        raise EvidenceError(f"manifest.evidenceFiles.{key} is missing (fail-closed)")
    rel = pinned.get("path")
    expected = pinned.get("sha256")
    if not rel or not expected:
        raise EvidenceError(
            f"manifest.evidenceFiles.{key} must pin both path and sha256 (fail-closed)"
        )
    path = resolve_path(rel)
    if not path.exists():
        raise EvidenceError(f"{what} not found: {path}")
    digest = sha256_file(path)
    if digest != expected:
        raise EvidenceError(
            f"{what} sha256 mismatch for {path}: {digest} != {expected}"
        )
    print(f"{what}: {path} sha256={digest} (pinned)")
    return path


def run_command(cmd) -> tuple:
    """Run *cmd*, echoing the full command/exit code/stdout/stderr/duration."""
    print(f"COMMAND (argv): {cmd}")
    print(f"$ {' '.join(cmd)}")
    started = time.monotonic()
    try:
        completed = subprocess.run(
            cmd,
            cwd=str(REPO_ROOT),
            capture_output=True,
            text=True,
            timeout=COMMAND_TIMEOUT_SECONDS,
        )
    except FileNotFoundError as exc:
        raise EvidenceError(f"cannot execute {cmd[0]!r}: {exc}") from exc
    except subprocess.TimeoutExpired as exc:
        raise EvidenceError(f"command timed out after {COMMAND_TIMEOUT_SECONDS}s: {cmd}") from exc
    duration = time.monotonic() - started
    print(f"exit code: {completed.returncode}  duration: {duration:.3f}s")
    if completed.stdout.strip():
        print("stdout:\n" + completed.stdout.rstrip())
    if completed.stderr.strip():
        print("stderr:\n" + completed.stderr.rstrip())
    return completed.returncode, completed.stdout, completed.stderr, duration


def persist(label: str, record: dict) -> Path:
    RUNS_DIR.mkdir(parents=True, exist_ok=True)
    path = RUNS_DIR / f"{label}.json"
    path.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(f"evidence record written: {path}")
    return path


def _ratio(value, base):
    if value is None or not base:
        return None
    return value / base


def _fmt_ratio(value) -> str:
    return "n/a" if value is None else f"{value:.3f}x"


# --------------------------------------------------------------------------- perf


def cmd_perf(args) -> int:
    if args.stage != "A":
        raise EvidenceError(f"unsupported stage {args.stage!r} (only A)")
    manifest = load_manifest(args.manifest)
    if manifest.get("stage") != args.stage:
        raise EvidenceError(
            f"manifest stage={manifest.get('stage')!r} does not match --stage {args.stage!r}"
        )

    acceptance = manifest.get("acceptanceCanonical")
    if not isinstance(acceptance, dict):
        raise EvidenceError("manifest is missing acceptanceCanonical")

    recorded_cmd = acceptance.get("command")
    if not recorded_cmd:
        raise EvidenceError("acceptanceCanonical.command is missing")
    print(f"recorded acceptance command (argv): {recorded_cmd}")
    print(f"recorded acceptance command: {' '.join(str(x) for x in recorded_cmd)}")

    command_text = " ".join(str(x) for x in recorded_cmd)
    if ACCEPTANCE_CATEGORY_FLAG not in command_text:
        raise EvidenceError(
            f"recorded acceptance command lacks {ACCEPTANCE_CATEGORY_FLAG!r} "
            f"(fail-closed): {command_text}"
        )

    cases = acceptance.get("cases")
    if not isinstance(cases, list):
        raise EvidenceError("acceptanceCanonical.cases is missing")
    methods = [case.get("method") for case in cases]
    if len(cases) != EXPECTED_CASES:
        raise EvidenceError(
            f"expected exactly {EXPECTED_CASES} acceptance cases, found {len(cases)}"
        )
    if len(set(methods)) != len(methods):
        raise EvidenceError(f"duplicate acceptance case methods: {methods}")
    if PREPARED not in methods:
        raise EvidenceError(f"control case {PREPARED!r} is missing from acceptance cases")
    missing_methods = [name for name in ACCEPTANCE_METHODS if name not in methods]
    unexpected_methods = [name for name in methods if name not in ACCEPTANCE_METHODS]
    if missing_methods or unexpected_methods:
        raise EvidenceError(
            "acceptance cases do not match the known 7 methods: "
            f"missing={missing_methods} unexpected={unexpected_methods}"
        )

    failures = acceptance.get("failures")
    if failures != 0:
        raise EvidenceError(f"recorded acceptance failures={failures!r} (expected 0)")
    if acceptance.get("exitCode") != 0:
        raise EvidenceError(
            f"recorded acceptance exitCode={acceptance.get('exitCode')!r} (expected 0)"
        )
    for key in ("executedBenchmarks", "casesSelected"):
        if acceptance.get(key) != EXPECTED_CASES:
            raise EvidenceError(
                f"acceptanceCanonical.{key}={acceptance.get(key)!r} (expected {EXPECTED_CASES})"
            )

    log_rel = acceptance.get("log")
    if not log_rel:
        raise EvidenceError("acceptanceCanonical.log is missing")
    log_path = resolve_path(log_rel)
    if not log_path.exists():
        raise EvidenceError(
            f"acceptance log not found: {log_path} (refusing to re-run the benchmark)"
        )

    raw = log_path.read_bytes()
    digest = sha256_file(log_path)
    expected_digest = acceptance.get("logSha256")
    if not expected_digest:
        raise EvidenceError("acceptanceCanonical.logSha256 is missing (fail-closed)")
    if digest != expected_digest:
        raise EvidenceError(
            f"acceptance log sha256 mismatch for {log_path}: {digest} != {expected_digest}"
        )

    text = raw.decode("utf-8", errors="replace")
    lines = text.splitlines()
    if len(lines) < 50:
        raise EvidenceError(f"acceptance log is short ({len(lines)} lines): {log_path}")
    for marker in ("Found 7 benchmark(s) in total", "executed benchmarks: 7"):
        if marker not in text:
            raise EvidenceError(f"acceptance log lacks {marker!r}: {log_path}")
    bad_lines = [
        line
        for line in lines
        if "Failed" in line and "Failed to set up priority" not in line
    ]
    if bad_lines:
        raise EvidenceError(
            "acceptance log shows failure lines: " + " | ".join(bad_lines[:3])
        )

    print(
        f"acceptance: exit=0 cases={len(cases)} failures=0 "
        f"wall={acceptance.get('wallSeconds')}s bdnGlobal={acceptance.get('globalTotalTime')}"
    )
    print(f"acceptance log: {log_path} sha256={digest}")

    exit_txt = log_path.parent / "acceptance.exit.txt"
    if exit_txt.exists():
        print(f"acceptance.exit.txt: {exit_txt.read_text(encoding='utf-8').strip()}")

    prepared = next(case for case in cases if case.get("method") == PREPARED)
    base_mean = prepared.get("exact_mean_ns")
    base_bytes = prepared.get("exact_bytes_per_invocation")
    print()
    print(
        f"{'Case':<28} {'Mean':>11} {'Allocated':>12} {'t/prep':>8} {'alloc/prep':>11}"
    )
    for case in cases:
        name = str(case.get("method"))
        mean = str(case.get("run_mean"))
        allocated = str(case.get("run_allocated"))
        time_ratio = _fmt_ratio(_ratio(case.get("exact_mean_ns"), base_mean))
        alloc_ratio = _fmt_ratio(
            _ratio(case.get("exact_bytes_per_invocation"), base_bytes)
        )
        print(f"{name:<28} {mean:>11} {allocated:>12} {time_ratio:>8} {alloc_ratio:>11}")

    ratios = (manifest.get("ratios") or {}).get("canonicalRun") or {}
    print()
    print(
        "ratios canonicalRun: "
        f"cachedToListOverPreparedToList={ratios.get('cachedToListOverPreparedToList')} "
        f"growthVsBaselinePercent={ratios.get('growthVsBaselinePercent')}% "
        f"allocatedRatio={ratios.get('allocatedRatio')} "
        f"allocatedGrowthPercent={ratios.get('allocatedGrowthPercent')}% "
        f"exceeds20Percent={ratios.get('exceeds20Percent')}"
    )

    persist(
        f"perf-{args.stage}",
        {
            "subcommand": "perf",
            "stage": args.stage,
            "executed": False,
            "recorded_command": recorded_cmd,
            "recorded_exit_code": acceptance.get("exitCode"),
            "wall_seconds": acceptance.get("wallSeconds"),
            "cases": methods,
            "failures": failures,
            "log": str(log_path),
            "log_sha256": digest,
            "log_sha256_matches_manifest": (
                None if not expected_digest else digest == expected_digest
            ),
            "ratios": ratios,
            "validated": True,
        },
    )
    return 0


# --------------------------------------------------------------------------- correctness


def cmd_correctness(args) -> int:
    if args.scope != "boundary":
        raise EvidenceError(f"unsupported scope {args.scope!r} (only boundary)")
    manifest = load_manifest(args.manifest)

    correctness_path = require_pinned_evidence(manifest, "correctness", "correctness JSON")
    diagnostics_path = require_pinned_evidence(manifest, "diagnostics", "diagnostics JSON")
    correctness = load_json(correctness_path, "correctness JSON")
    diagnostics = load_json(diagnostics_path, "diagnostics JSON")

    checks = correctness.get("correctness")
    if not isinstance(checks, dict):
        raise EvidenceError("correctness JSON has no 'correctness' object")
    missing = [key for key in BOUNDARY_KEYS if key not in checks]
    if missing:
        raise EvidenceError(f"correctness JSON missing boundary keys: {missing}")
    failed = [key for key in BOUNDARY_KEYS if checks.get(key) is not True]
    if failed:
        raise EvidenceError(
            "boundary correctness checks not all true: " + ", ".join(failed)
        )

    if manifest.get("scope", {}).get("stickyCacheFalseAbsent") is not True:
        raise EvidenceError("manifest scope.stickyCacheFalseAbsent is not true")
    manifest_boundary = manifest.get("correctnessBoundary")
    if not isinstance(manifest_boundary, dict):
        raise EvidenceError("manifest is missing correctnessBoundary")
    manifest_failed = [
        key for key in BOUNDARY_KEYS if manifest_boundary.get(key) is not True
    ]
    if manifest_failed:
        raise EvidenceError(
            "manifest correctnessBoundary not all true: " + ", ".join(manifest_failed)
        )

    for arm in HIT_ARMS:
        proof = diagnostics.get(arm)
        if not isinstance(proof, dict):
            raise EvidenceError(f"diagnostics JSON has no {arm!r} hit proof")
        if proof.get("hitProofOk") is not True:
            raise EvidenceError(f"diagnostics {arm}: hitProofOk is not true")
        if proof.get("paramValueMismatches") != 0:
            raise EvidenceError(
                f"diagnostics {arm}: paramValueMismatches={proof.get('paramValueMismatches')!r}"
            )
        if proof.get("distinctSql") != 1:
            raise EvidenceError(
                f"diagnostics {arm}: distinctSql={proof.get('distinctSql')!r} (expected 1)"
            )
        if proof.get("misses") != 1:
            raise EvidenceError(f"diagnostics {arm}: misses={proof.get('misses')!r} (expected 1)")
        if proof.get("hits") != (proof.get("iterations") or 0) - 1:
            raise EvidenceError(
                f"diagnostics {arm}: hits={proof.get('hits')!r} != iterations-1"
            )

    print()
    for key in BOUNDARY_KEYS:
        print(f"  {key} = true")
    for arm in HIT_ARMS:
        proof = diagnostics[arm]
        print(
            f"  hit-proof {arm}: misses={proof.get('misses')} hits={proof.get('hits')} "
            f"distinctSql={proof.get('distinctSql')} "
            f"paramValueMismatches={proof.get('paramValueMismatches')}"
        )
    print("sticky Cache=false / _dontCache: ABSENT (sticky_cache_false_absent=true)")

    persist(
        f"correctness-{args.scope}",
        {
            "subcommand": "correctness",
            "scope": args.scope,
            "executed": False,
            "correctness_json": str(correctness_path),
            "correctness_sha256": sha256_file(correctness_path),
            "diagnostics_json": str(diagnostics_path),
            "diagnostics_sha256": sha256_file(diagnostics_path),
            "boundary_checks": {key: checks[key] for key in BOUNDARY_KEYS},
            "sticky_cache_false_absent": checks["sticky_cache_false_absent"],
            "validated": True,
        },
    )
    return 0


# --------------------------------------------------------------------------- scope


def cmd_scope(args) -> int:
    base = args.base
    cte_cmd = [
        "git",
        "diff",
        "--exit-code",
        base,
        "--",
        "src/nextorm.core/Builders/CteHoister.cs",
    ]
    core_cmd = ["git", "diff", "--exit-code", base, "--", "src/nextorm.core"]
    # Untracked/ignored new files under core would bypass the base diff, so they are checked too.
    # Ignored build outputs (bin/obj) are excluded via --exclude-standard / default status output.
    core_untracked_status_cmd = [
        "git",
        "status",
        "--porcelain=v1",
        "--",
        "src/nextorm.core",
    ]
    core_untracked_ls_cmd = [
        "git",
        "ls-files",
        "--others",
        "--exclude-standard",
        "--",
        "src/nextorm.core",
    ]

    cte_code, cte_out, cte_err, cte_duration = run_command(cte_cmd)
    core_code, core_out, core_err, core_duration = run_command(core_cmd)
    stat_code, stat_out, stat_err, stat_duration = run_command(core_untracked_status_cmd)
    lsf_code, lsf_out, lsf_err, lsf_duration = run_command(core_untracked_ls_cmd)

    g1_cte_changed = cte_code != 0
    core_diff_present = core_code != 0
    core_untracked = bool(stat_out.strip()) or bool(lsf_out.strip())
    print()
    print(f"G1-CTE predicate (CteHoister changed vs {base}) = {str(g1_cte_changed).lower()}")
    print(f"core diff present (src/nextorm.core vs {base}) = {str(core_diff_present).lower()}")
    print(f"core untracked/ignored new files present = {str(core_untracked).lower()}")
    if core_out.strip():
        print("core diff:")
        print(core_out.rstrip())
    if stat_out.strip():
        print("core git status --porcelain:")
        print(stat_out.rstrip())
    if lsf_out.strip():
        print("core untracked files:")
        print(lsf_out.rstrip())

    manifest = None
    if args.manifest.exists():
        try:
            manifest = load_manifest(args.manifest)
        except EvidenceError as exc:
            print(f"note: manifest cross-check skipped: {exc}")
    if manifest is not None:
        recorded = manifest.get("scope") or {}
        print(
            "manifest scope cross-check: "
            f"g1CteChanged={recorded.get('g1CteChanged')} "
            f"coreDiff={recorded.get('coreDiff')} "
            f"stickyCacheFalseAbsent={recorded.get('stickyCacheFalseAbsent')}"
        )

    persist(
        "scope",
        {
            "subcommand": "scope",
            "executed": True,
            "base": base,
            "commands": [
                {
                    "command": cte_cmd,
                    "exit_code": cte_code,
                    "duration_seconds": cte_duration,
                    "stdout": cte_out,
                    "stderr": cte_err,
                },
                {
                    "command": core_cmd,
                    "exit_code": core_code,
                    "duration_seconds": core_duration,
                    "stdout": core_out,
                    "stderr": core_err,
                },
                {
                    "command": core_untracked_status_cmd,
                    "exit_code": stat_code,
                    "duration_seconds": stat_duration,
                    "stdout": stat_out,
                    "stderr": stat_err,
                },
                {
                    "command": core_untracked_ls_cmd,
                    "exit_code": lsf_code,
                    "duration_seconds": lsf_duration,
                    "stdout": lsf_out,
                    "stderr": lsf_err,
                },
            ],
            "g1_cte_changed": g1_cte_changed,
            "core_diff_present": core_diff_present,
            "core_untracked_present": core_untracked,
            "validated": not (g1_cte_changed or core_diff_present or core_untracked),
        },
    )

    if cte_code > 1 or core_code > 1 or stat_code > 1 or lsf_code > 1:
        print(
            "FAIL: git command exited with an error "
            f"({cte_code}/{core_code}/{stat_code}/{lsf_code})"
        )
        return 2
    if g1_cte_changed or core_diff_present or core_untracked:
        print("FAIL: Stage A scope violation (core changed or untracked core files present)")
        return 1
    return 0


# --------------------------------------------------------------------------- main


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Iteration 15 / #183 Stage A evidence runner")
    parser.add_argument(
        "--manifest",
        type=Path,
        default=MANIFEST_PATH,
        help="Stage A manifest (default: docs/specs/performance/iteration-15-stage-a-manifest.json)",
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    perf = subparsers.add_parser("perf", help="validate the 7-case acceptance log")
    perf.add_argument("--stage", default="A")
    perf.set_defaults(func=cmd_perf)

    correctness = subparsers.add_parser(
        "correctness", help="validate boundary correctness and sticky Cache=false"
    )
    correctness.add_argument("--scope", default="boundary")
    correctness.set_defaults(func=cmd_correctness)

    scope = subparsers.add_parser("scope", help="git scope / G1-CTE predicate")
    scope.add_argument("--base", required=True)
    scope.set_defaults(func=cmd_scope)

    return parser


def main(argv=None) -> int:
    args = build_parser().parse_args(argv)
    print(f"# iteration15_evidence.py {args.command}")
    try:
        return args.func(args)
    except EvidenceError as exc:
        print(f"FAIL: {exc}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
