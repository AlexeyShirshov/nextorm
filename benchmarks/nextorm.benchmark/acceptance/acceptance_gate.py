#!/usr/bin/env python3
"""#168 (D4) acceptance perf gate: paired ABBA BenchmarkDotNet measurement.

Baseline = a `git archive` extraction of the recorded SHA (no worktree, no commit); candidate = the
current working tree. The same benchmark source (this folder) is attached to both trees via the
benchmark-only MSBuild injection target `AcceptanceHarness.targets`, passed to both builds as
`-p:CustomAfterMicrosoftCommonTargets=<targets> -p:AcceptanceBenchmark=true`.

Each A/B run is a fresh `dotnet <benchmark dll>` process, Release, same SDK/machine, filtered to the
D168 acceptance benchmark with `--anyCategories=acceptance`, 20 iterations / 5 warmups. `--filter '*'`
is intentionally scoped to the D168 type because six other pre-existing `acceptance`-category
benchmarks in this project would otherwise be measured too; the category filter is unchanged.

Gate (from the plan): allocations must drop and the numeric-source boxing count (strict reader
`GetValue` calls) must be 0, else FAIL (time gate moot). Time: median round ratio <= 1.05 with no round
> 1.20 -> PASS; any round > 1.20 -> FAIL; > 1.05 in >= 2/3 rounds -> FAIL; otherwise INCONCLUSIVE ->
three more 40-iteration rounds.
"""

from __future__ import annotations

import json
import glob
import os
import re
import shutil
import statistics
import subprocess
import sys
from datetime import datetime, timezone

BENCH_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = os.path.dirname(os.path.dirname(BENCH_DIR))
HARNESS_DIR = os.path.join(BENCH_DIR, "acceptance")
TARGETS = os.path.join(HARNESS_DIR, "AcceptanceHarness.targets")
BASE_SHA = "6bf362958ee4336e83a2a7550e49038184ff1caf"
OUT = os.path.join(REPO, "artifacts", "d168")
WORK = os.environ.get("D168_GATE_WORK", "/tmp/opencode/d168-gate")
BASELINE = os.path.join(WORK, "baseline")
CANDIDATE = REPO
PROJECT = os.path.join("benchmarks", "nextorm.benchmark", "nextorm.benchmark.csproj")
BENCH_FILTER = "*SqlServerBufferedNumericAcceptanceBenchmark*"
CATEGORY = "acceptance"
DLL_GLOB = os.path.join("benchmarks", "nextorm.benchmark", "bin", "*", "Release", "net10.0", "nextorm.benchmark.dll")

ROUNDS = int(os.environ.get("D168_GATE_ROUNDS", "3"))
ITERATIONS = int(os.environ.get("D168_GATE_ITERATIONS", "20"))
WARMUP = int(os.environ.get("D168_GATE_WARMUP", "5"))
EXTRA_ROUNDS = 3
EXTRA_ITERATIONS = 40

SELFCHECK_RE = re.compile(r"D168_SELFCHECK getValueCalls=(\d+) typedReadCalls=(\d+)")


def log(msg: str) -> None:
    print(msg, flush=True)


def run(cmd, cwd, log_path, extra_env=None):
    env = os.environ.copy()
    if extra_env:
        env.update(extra_env)
    with open(log_path, "w", encoding="utf-8") as handle:
        proc = subprocess.run(cmd, cwd=cwd, stdout=handle, stderr=subprocess.STDOUT, env=env)
    return proc.returncode


def prepare_baseline() -> None:
    if os.path.isdir(BASELINE):
        shutil.rmtree(BASELINE)
    os.makedirs(BASELINE, exist_ok=True)
    archive = subprocess.run(
        ["git", "-C", REPO, "archive", "--format=tar", BASE_SHA],
        stdout=subprocess.PIPE, check=True,
    ).stdout
    tar = subprocess.run(["tar", "-x", "-C", BASELINE], input=archive, check=True)
    _ = tar
    shutil.copytree(HARNESS_DIR, os.path.join(BASELINE, "benchmarks", "nextorm.benchmark", "acceptance"))
    # Identical injection path in the baseline tree, mirroring the candidate.
    with open(TARGETS, "r", encoding="utf-8") as src, \
            open(os.path.join(BASELINE, "benchmarks", "nextorm.benchmark", "acceptance", "AcceptanceHarness.targets"), "w", encoding="utf-8") as dst:
        dst.write(src.read())


def build(tree: str, label: str) -> int:
    targets = os.path.join(tree, "benchmarks", "nextorm.benchmark", "acceptance", "AcceptanceHarness.targets")
    cmd = [
        "dotnet", "build", os.path.join(tree, PROJECT), "-c", "Release",
        "-p:AcceptanceBenchmark=true",
        f"-p:CustomAfterMicrosoftCommonTargets={targets}",
    ]
    return run(cmd, tree, os.path.join(OUT, f"bdn-build-{label}.log"))


def find_dll(tree: str) -> str:
    matches = glob.glob(os.path.join(tree, DLL_GLOB))
    if not matches:
        raise RuntimeError(f"No benchmark dll under {tree}")
    return matches[0]


def run_one(tree: str, run_id: str, iterations: int, warmup: int) -> dict:
    art = os.path.join(WORK, "runs", run_id)
    shutil.rmtree(art, ignore_errors=True)
    os.makedirs(art, exist_ok=True)
    log_path = os.path.join(OUT, f"bdn-{run_id}.log")
    dll = find_dll(tree)
    code = run(
        ["dotnet", dll, "--filter", BENCH_FILTER, f"--anyCategories={CATEGORY}"],
        tree, log_path,
        {"NEXTORM_ACCEPTANCE_ARTIFACTS": art,
         "NEXTORM_ACCEPTANCE_ITERATIONS": str(iterations),
         "NEXTORM_ACCEPTANCE_WARMUP": str(warmup)},
    )
    with open(log_path, "r", encoding="utf-8", errors="replace") as handle:
        text = handle.read()
    selfcheck = SELFCHECK_RE.search(text)
    reports = glob.glob(os.path.join(art, "results", "*report-full.json"))
    if code != 0 or not reports:
        raise RuntimeError(f"Run {run_id} failed (exit {code}); see {log_path}")
    with open(reports[0], "r", encoding="utf-8") as handle:
        summary = json.load(handle)
    bench = next(b for b in summary["Benchmarks"] if b.get("MethodTitle") == "MaterializeRows")
    stats = bench["Statistics"]
    memory = bench["Memory"]
    return {
        "run": run_id,
        "tree": tree,
        "exit": code,
        "getValueCalls": int(selfcheck.group(1)) if selfcheck else None,
        "typedReadCalls": int(selfcheck.group(2)) if selfcheck else None,
        "meanPerRowNs": stats["Mean"],
        "stdErrPerRowNs": stats["StandardError"],
        "stdDevPerRowNs": stats["StandardDeviation"],
        "allocatedBytesPerRow": memory["BytesAllocatedPerOperation"],
        "gen0Per1000": memory["Gen0Collections"],
        "log": log_path,
    }


def mean(values):
    return statistics.fmean(values)


def classify(run_pairs: list[tuple[dict, dict, dict, dict]], cand_alloc: float, base_alloc: float) -> dict:
    cand_box = max(r["getValueCalls"] for pair in run_pairs for r in (pair[1], pair[2]))
    ratios = []
    for a1, b1, b2, a2 in run_pairs:
        base = mean([a1["meanPerRowNs"], a2["meanPerRowNs"]])
        cand = mean([b1["meanPerRowNs"], b2["meanPerRowNs"]])
        ratios.append(cand / base)

    median_ratio = statistics.median(ratios)
    allocation_ok = cand_alloc < base_alloc
    boxing_ok = cand_box == 0

    if not allocation_ok or not boxing_ok:
        verdict = "FAIL"
        reason = f"allocation drop={allocation_ok} (cand {cand_alloc} B/row vs base {base_alloc} B/row), boxing=0 {boxing_ok} (cand max {cand_box})"
    elif any(r > 1.20 for r in ratios):
        verdict = "FAIL"
        reason = "at least one round ratio > 1.20"
    elif median_ratio <= 1.05:
        verdict = "PASS"
        reason = f"median ratio {median_ratio:.4f} <= 1.05, no round > 1.20"
    elif sum(1 for r in ratios if r > 1.05) >= (2 * len(ratios) + 2) // 3:
        verdict = "FAIL"
        reason = f"ratio > 1.05 in >= 2/3 rounds (median {median_ratio:.4f})"
    else:
        verdict = "INCONCLUSIVE"
        reason = f"median ratio {median_ratio:.4f}"

    return {
        "verdict": verdict,
        "reason": reason,
        "ratios": ratios,
        "medianRatio": median_ratio,
        "candidateAllocatedBytesPerRow": cand_alloc,
        "baselineAllocatedBytesPerRow": base_alloc,
        "candidateBoxingCount": cand_box,
    }


def main() -> int:
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(WORK, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    log(f"[{stamp}] preparing baseline {BASE_SHA} in {BASELINE}")
    prepare_baseline()

    log("building baseline ...")
    if build(BASELINE, "baseline") != 0:
        log("BASELINE BUILD FAILED")
        return 2
    log("building candidate ...")
    if build(CANDIDATE, "candidate") != 0:
        log("CANDIDATE BUILD FAILED")
        return 2

    all_runs = []

    def do_rounds(round_count: int, iterations: int, warmup: int, tag: str):
        pairs = []
        for r in range(1, round_count + 1):
            prefix = f"{tag}r{r}"
            a1 = run_one(BASELINE, f"{prefix}-A1", iterations, warmup)
            b1 = run_one(CANDIDATE, f"{prefix}-B1", iterations, warmup)
            b2 = run_one(CANDIDATE, f"{prefix}-B2", iterations, warmup)
            a2 = run_one(BASELINE, f"{prefix}-A2", iterations, warmup)
            all_runs.extend([a1, b1, b2, a2])
            base = mean([a1["meanPerRowNs"], a2["meanPerRowNs"]])
            cand = mean([b1["meanPerRowNs"], b2["meanPerRowNs"]])
            log(f"  {prefix}: base {base:9.2f} ns/row  cand {cand:9.2f} ns/row  ratio {cand / base:.4f}")
            pairs.append((a1, b1, b2, a2))
        return pairs

    pairs = do_rounds(ROUNDS, ITERATIONS, WARMUP, "r")
    base_alloc = mean([r["allocatedBytesPerRow"] for pair in pairs for r in (pair[0], pair[3])])
    cand_alloc = mean([r["allocatedBytesPerRow"] for pair in pairs for r in (pair[1], pair[2])])
    result = classify(pairs, cand_alloc, base_alloc)

    if result["verdict"] == "INCONCLUSIVE":
        log("inconclusive -> three extra 40-iteration rounds")
        pairs = pairs + do_rounds(EXTRA_ROUNDS, EXTRA_ITERATIONS, WARMUP, "x")
        base_alloc = mean([r["allocatedBytesPerRow"] for pair in pairs for r in (pair[0], pair[3])])
        cand_alloc = mean([r["allocatedBytesPerRow"] for pair in pairs for r in (pair[1], pair[2])])
        result = classify(pairs, cand_alloc, base_alloc)
        if result["verdict"] == "INCONCLUSIVE":
            result["verdict"] = "NOT PASS (still inconclusive after extra rounds)"

    report = {
        "generatedUtc": stamp,
        "baselineSha": BASE_SHA,
        "rounds": ROUNDS,
        "iterations": ITERATIONS,
        "warmup": WARMUP,
        "sdk": subprocess.run(["dotnet", "--version"], stdout=subprocess.PIPE, text=True).stdout.strip(),
        "result": result,
        "runs": all_runs,
    }
    report_path = os.path.join(OUT, "bdn-gate-report.json")
    with open(report_path, "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2)
    log(f"VERDICT: {result['verdict']} - {result['reason']}")
    log(f"report: {report_path}")
    return 0 if result["verdict"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
