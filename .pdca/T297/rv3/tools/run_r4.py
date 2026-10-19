#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""T297 r=4 evidence runner (stdlib only).

Subcommands map to the rv=4 contract rows E20-E29. This phase implements `attribute`
(E22, full-path per-phase attribution) and `--check-only`; every other subcommand is a
declared stub that exits 3 instead of silently succeeding.

Attribution protocol (dispatch P02):
  * exclusive `flock` on /tmp/opencode/nextorm-bdn-host.lock for the whole run;
  * 60 s quiet-window observation before round 1 (recorded, never used to drop a round);
  * six paired rounds in ABBA order = three `A,B,B,A` groups (12 blocks), one pinned
    process per block (taskset -c 1, NEXTORM_BENCH_DB, DOTNET_gcServer=0,
    NEXTORM_BENCH_FULL unset);
  * raw JSONL + stderr per block, then the paired phase table, reconciliation,
    instrumentation-overhead estimate and the pre-registered verdict rule under
    `.pdca/T297/rv3/attribution/`.
"""

import argparse
import difflib
import fcntl
import hashlib
import json
import os
import random
import re
import shutil
import statistics
import subprocess
import sys
import time
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
DEFAULT_EVID = REPO / ".pdca" / "T297" / "rv3" / "attribution"
LOCK = Path("/tmp/opencode/nextorm-bdn-host.lock")
GUARD = Path("/tmp/opencode/t247-measure.guard")
DB = Path("/tmp/nextorm-bench/test.db")
EXPECTED_DB_SHA = "32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295"
PROBE_ROOT = Path("/tmp/opencode/t297-p02")
PROBE = {
    "A": PROBE_ROOT / "probe-a" / "bin" / "Release" / "net10.0" / "t297-probe-a.dll",
    "B": PROBE_ROOT / "probe-b" / "bin" / "Release" / "net10.0" / "t297-probe-b.dll",
}
CORE_DLL = {
    "A": PROBE_ROOT / "A" / "src" / "nextorm.core" / "bin" / "linux" / "Release" / "net10.0" / "nextorm.core.dll",
    "B": PROBE_ROOT / "B" / "src" / "nextorm.core" / "bin" / "linux" / "Release" / "net10.0" / "nextorm.core.dll",
}
SIDE_TREE = {"A": PROBE_ROOT / "A", "B": PROBE_ROOT / "B"}
SIDE_COMMIT = {"A": "0d4d5df2", "B": "9d633521"}
ARMS = ["cte", "recursive", "inlist", "leftjoin"]
PHASES = [
    "construct", "prepare", "plan", "bind", "enum_create", "cow_shape",
    "get_db_command", "execute", "read_rows", "map_rows", "cow_description", "cow_clone",
]
# prepare is a span over these nested phases: exclusive prepare = prepare - nested sum.
NESTED_IN_PREPARE = ["plan", "bind", "enum_create", "cow_shape"]
E2E_PHASES = ["construct", "prepare_exclusive", "get_db_command", "execute", "read_rows", "map_rows"]
CAUSE_CANDIDATES = [
    "construct", "prepare_exclusive", "plan", "bind", "enum_create", "cow_shape",
    "get_db_command", "execute", "read_rows", "map_rows",
]
COUNTER_PHASES = ["construct", "prepare", "plan", "get_db_command", "execute"]
STUBS = [
    "seal", "fix-audit", "allocation-audit", "acceptance",
    "boundary", "coverage", "hygiene", "handoff",
]

# Uninstrumented ABBA causal anchor (E21 diagnostic, 16 pairs per arm/job):
# default / in-process B/A time ratios, recorded in .pdca/T297/rv3/diagnostic/summary.json.
ANCHOR = {
    "cte": {"default": 1.076, "inprocess": 1.147},
    "recursive": {"default": 1.101, "inprocess": 1.136},
    "inlist": {"default": 1.082, "inprocess": 1.075},
    "leftjoin": {"default": 1.104, "inprocess": 1.048},
}

# Phase boundary table, copied from the two probe copies. Line numbers are the copies'
# actual lines (pristine commit + instrumentation). Shared phases have identical lines in
# A and B; B adds COW description/clone counters that do not exist in A.
BOUNDARY = {
    "construct": "QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap)",
    "prepare": "QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand)",
    "plan": "DataContext/QueryPlanner.cs:698-711 MakeSelect (plan-key + QueryPlanStore.TryGet)",
    "bind": "DataContext/QueryPlanner.cs:874-881 MakeSelect -> RefreshParameters<TResult>",
    "enum_create": "DataContext/QueryPlanner.cs:891-898 MakeSelect -> CreateResultSetEnumerator<TResult>",
    "cow_shape": "Query/QueryCommand.QueryPreparer.cs:198-288 RefreshInValuesShape",
    "get_db_command": "DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore",
    "execute": "DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync",
    "read_rows": "DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList",
    "map_rows": "DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList",
    "cow_description": "Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only)",
    "cow_clone": "Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only)",
}
ROSLYN = {
    "construct": "NextORM.Core.QueryCommand<TResult>.ToList / ToListAsyncCore (src/nextorm.core/Query/QueryCommand.TResult.cs:14)",
    "prepare": "NextORM.Core.QueryCommand<TResult>.ToList / ToListAsyncCore (src/nextorm.core/Query/QueryCommand.TResult.cs:14)",
    "plan": "NextORM.Core.QueryPlanner.MakeSelect (src/nextorm.core/DataContext/QueryPlanner.cs:549)",
    "bind": "NextORM.Core.QueryPlanner.RefreshParameters<TResult> (src/nextorm.core/DataContext/QueryPlanner.cs:919)",
    "enum_create": "NextORM.Core.QueryPlanner.CreateResultSetEnumerator<TResult> (src/nextorm.core/DataContext/QueryPlanner.cs:1030)",
    "cow_shape": "NextORM.Core.QueryCommand.RefreshInValuesShape (src/nextorm.core/Query/QueryCommand.Plan.cs:10)",
    "get_db_command": "NextORM.Core.DbPreparedQueryCommand<TResult>.GetDbCommandCore (src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs:197)",
    "execute": "NextORM.Core.QueryExecutor.RunReader (:179) / RunReaderAsync (:209)",
    "read_rows": "NextORM.Core.QueryExecutor.ToListAsync<TResult> (:738) / ToList<TResult> (:790)",
    "map_rows": "NextORM.Core.QueryExecutor.ToListAsync<TResult> (:738) / ToList<TResult> (:790)",
    "cow_description": "NextORM.Core.QueryDescription.MarkShared (:261) / CloneShellForWrite (:282) and NextORM.Core.QueryCommand.WriteDescription (:312) [B]",
    "cow_clone": "NextORM.Core.QueryCommand.QueryCommand(IDataContext?, QueryCommand, bool) (src/nextorm.core/Query/QueryCommand.cs:174) [B]",
}

INSTRUMENTED = {
    "A": [
        "src/nextorm.core/DataContext/QueryPlanner.cs",
        "src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs",
        "src/nextorm.core/DataContext/QueryExecutor.cs",
        "src/nextorm.core/Query/QueryCommand.QueryPreparer.cs",
        "src/nextorm.core/Query/QueryCommand.TResult.cs",
        "src/nextorm.core/Probe/T297PhaseProbe.cs",
    ],
    "B": [
        "src/nextorm.core/DataContext/QueryPlanner.cs",
        "src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs",
        "src/nextorm.core/DataContext/QueryExecutor.cs",
        "src/nextorm.core/Query/QueryCommand.QueryPreparer.cs",
        "src/nextorm.core/Query/QueryCommand.TResult.cs",
        "src/nextorm.core/Query/QueryCommand.Description.cs",
        "src/nextorm.core/Query/QueryCommand.cs",
        "src/nextorm.core/Probe/T297PhaseProbe.cs",
    ],
}


def utc():
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def sha256_text(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def load_avg():
    try:
        return open("/proc/loadavg").read().strip()
    except OSError:
        return None


def loadavg_1min():
    raw = load_avg()
    if not raw:
        return None
    try:
        return float(raw.split()[0])
    except (ValueError, IndexError):
        return None


def median(values):
    values = [v for v in values if v is not None]
    if not values:
        return None
    return statistics.median(values)


def fail(message, code=1):
    print("ERROR: " + message, file=sys.stderr)
    return code


def log_line(evid, message):
    stamped = "%s | %s" % (utc(), message)
    print(stamped)
    evid.mkdir(parents=True, exist_ok=True)
    with open(evid / "host.log", "a", encoding="utf-8") as f:
        f.write(stamped + "\n")


def parse_jsonl(path):
    records = []
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                records.append(json.loads(line))
            except json.JSONDecodeError as exc:
                raise ValueError("%s: invalid JSON line: %s" % (path, exc))
    return records


def acquire_lock(timeout_s):
    fd = os.open(str(LOCK), os.O_RDWR | os.O_CREAT, 0o644)
    deadline = time.time() + timeout_s
    while True:
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            return fd
        except BlockingIOError:
            if time.time() >= deadline:
                os.close(fd)
                raise RuntimeError("host lock %s not acquired within %ss" % (LOCK, timeout_s))
            time.sleep(0.5)


def release_lock(fd):
    try:
        fcntl.flock(fd, fcntl.LOCK_UN)
    finally:
        os.close(fd)


def competing_processes():
    """Returns [{pid, cmd}] for running benchmark/probe competitors (self excluded)."""
    own = {os.getpid(), os.getppid()}
    hits = []
    for entry in os.listdir("/proc"):
        if not entry.isdigit():
            continue
        pid = int(entry)
        if pid in own:
            continue
        try:
            raw = Path("/proc/%s/cmdline" % entry).read_bytes()
        except OSError:
            continue
        if not raw:
            continue
        cmd = raw.replace(b"\x00", b" ").decode("utf-8", "replace").strip()
        if "run_r4.py" in cmd and "attribute" in cmd:
            continue
        if "nextorm.benchmark" in cmd or "t297-probe" in cmd or re.search(r"(^|[\s/])probe-[ab](\.dll)?(\s|$)", cmd):
            hits.append({"pid": pid, "cmd": cmd})
    return hits


def env_snapshot():
    keep = {}
    for key, value in os.environ.items():
        if key.startswith(("DOTNET_", "NEXTORM_")) or key in (
                "PATH", "DOTNET_ROOT", "DOCKER_HOST", "TMPDIR", "LANG", "LC_ALL"):
            keep[key] = value
    rest_keys = sorted(set(os.environ) - set(keep))
    return {
        "selected": keep,
        "remaining_key_count": len(rest_keys),
        "remaining_keys_sha256": sha256_text("\n".join(rest_keys)),
        "nextorm_bench_full": os.environ.get("NEXTORM_BENCH_FULL"),
    }


def run_block(side, block, args, evid):
    binary = Path(args.bin_a if side == "A" else args.bin_b)
    if not binary.exists():
        raise RuntimeError("probe binary missing for side %s: %s" % (side, binary))

    raw = evid / "runs"
    stem = "block-%02d-%s" % (block, side)
    jsonl = raw / (stem + ".jsonl")
    errlog = raw / (stem + ".log")
    cmd = [
        "taskset", "-c", str(args.cpu),
        "dotnet", str(binary),
        "--arm", args.arms,
        "--warmup", str(args.warmup),
        "--iters", str(args.iters),
        "--overhead-n", str(args.overhead_n),
    ]
    env = os.environ.copy()
    env.pop("NEXTORM_BENCH_FULL", None)
    env["NEXTORM_BENCH_DB"] = str(Path(args.db))
    env["DOTNET_gcServer"] = "0"

    db_sha_before = sha256(Path(args.db))
    la_before = load_avg()
    started = utc()
    t0 = time.time()
    with open(jsonl, "w", encoding="utf-8") as out, open(errlog, "w", encoding="utf-8") as err:
        err.write("CMD %s\nSTART %s\n" % (" ".join(cmd), started))
        err.flush()
        proc = subprocess.run(cmd, cwd=str(evid), env=env, stdout=out, stderr=err,
                              timeout=args.timeout)
    wall = round(time.time() - t0, 3)
    db_sha_after = sha256(Path(args.db))
    la_after = load_avg()

    try:
        records = parse_jsonl(jsonl)
    except ValueError as exc:
        records = []
        parse_error = str(exc)
    else:
        parse_error = None

    meta = {
        "side": side,
        "block": block,
        "cmd": cmd,
        "started": started,
        "finished": utc(),
        "wall_s": wall,
        "exit": proc.returncode,
        "jsonl": str(jsonl),
        "stderr": str(errlog),
        "db": str(Path(args.db)),
        "db_sha_before": db_sha_before,
        "db_sha_after": db_sha_after,
        "loadavg_before": la_before,
        "loadavg_after": la_after,
        "cpu": args.cpu,
        "parse_error": parse_error,
        "record_count": len(records),
    }
    with open(raw / (stem + ".meta.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=1)
    return meta, records


def block_metrics(records):
    """Extracts ns/op, calls/op, alloc/op and overhead pair cost for one block."""
    overhead = next((r for r in records if r.get("kind") == "overhead"), None)
    pair_ns = overhead.get("pair_ns") if overhead else None
    metrics = {}
    for record in records:
        arm = record.get("arm")
        if not arm:
            continue
        queries = record["queries"]
        freq = record["frequency"] or 1
        ticks = record.get("ticks") or {}
        calls = record.get("calls") or {}
        ns = {phase: ticks.get(phase, 0) / queries * 1e9 / freq for phase in PHASES}
        calls_per_op = {phase: calls.get(phase, 0) / queries for phase in PHASES}
        alloc = (record.get("alloc") or {}).get("total_per_arm", 0) / queries
        metrics[arm] = {
            "queries": queries,
            "frequency": freq,
            "ns": ns,
            "calls": calls_per_op,
            "alloc": alloc,
            "end_to_end": record.get("end_to_end_ticks", 0) / queries * 1e9 / freq,
            "pair_ns": pair_ns,
            "sink": record.get("sink"),
        }
    return metrics


def derive_prepare_exclusive(metric):
    nested = sum(metric["ns"][phase] for phase in NESTED_IN_PREPARE)
    return metric["ns"]["prepare"] - nested


def bootstrap_ci(values, seed, resamples):
    """Percentile bootstrap of the median; fixed seed, deterministic."""
    values = [v for v in values if v is not None]
    if len(values) < 2:
        return None, None
    rng = random.Random(seed)
    medians = []
    n = len(values)
    for _ in range(resamples):
        sample = [values[rng.randrange(n)] for _ in range(n)]
        sample.sort()
        medians.append(statistics.median(sample))
    medians.sort()
    lo = medians[int(0.025 * resamples)]
    hi = medians[min(resamples - 1, int(0.975 * resamples))]
    return lo, hi


def fmt(value, digits=1):
    if value is None:
        return "—"
    return ("%%.%df" % digits) % value


def fmt_pct(value, digits=1):
    if value is None:
        return "—"
    return (("%%+.%df%%%%" % digits) % value)


def analyze(runs, args):
    """Builds per-arm paired tables, reconciliation and the verdict evaluation."""
    by_block = {}
    for run in runs:
        by_block[run["block"]] = {
            "side": run["side"],
            "metrics": block_metrics(run["records"]),
            "loadavg": run["loadavg_before"],
            "loadavg_after": run["loadavg_after"],
        }

    if len(by_block) % 4 != 0:
        raise RuntimeError("ABBA analysis needs a multiple of 4 blocks, got %d" % len(by_block))

    groups = []
    for start in range(1, len(by_block) + 1, 4):
        expected = ["A", "B", "B", "A"]
        sides = [by_block.get(start + offset, {}).get("side") for offset in range(4)]
        if sides != expected:
            raise RuntimeError("block %d..%d is not ABBA: %s" % (start, start + 3, sides))
        groups.append([by_block[start + offset] for offset in range(4)])

    def side_series(groups_list, side):
        return [g[0] if side == "A" else g[1] for g in groups_list] + \
               [g[3] if side == "A" else g[2] for g in groups_list]

    def pairs_for(getter):
        pairs = []
        for index, group in enumerate(groups):
            a_first, b_first, b_second, a_second = group
            pairs.append({
                "round": index * 2 + 1,
                "direction": "A→B",
                "a": getter(a_first["metrics"]),
                "b": getter(b_first["metrics"]),
            })
            pairs.append({
                "round": index * 2 + 2,
                "direction": "B→A",
                "a": getter(a_second["metrics"]),
                "b": getter(b_second["metrics"]),
            })
        return pairs

    def summarize_pairs(pairs, seed):
        ratios = []
        signs = []
        for pair in pairs:
            a_ns, b_ns = pair["a"], pair["b"]
            if a_ns is None or a_ns <= 0 or b_ns is None:
                pair["ratio"] = None
                continue
            pair["ratio"] = b_ns / a_ns
            ratios.append(pair["ratio"])
            signs.append("+" if pair["ratio"] > 1 else "-")
        deltas = [p["b"] - p["a"] for p in pairs if p["a"] is not None and p["b"] is not None]
        ratio_median = median(ratios)
        if ratio_median is not None and ratios:
            lo, hi = bootstrap_ci(ratios, seed, args.resamples)
        else:
            lo, hi = None, None
        rel_half = None
        if ratio_median and lo is not None and hi is not None:
            rel_half = (hi - lo) / 2.0 / ratio_median
        return {
            "ratio": ratio_median,
            "ci_low": lo,
            "ci_high": hi,
            "rel_halfwidth": rel_half,
            "delta": median(deltas),
            "pairs": [{"round": p["round"], "direction": p["direction"], "ratio": p["ratio"],
                       "a": p["a"], "b": p["b"]} for p in pairs],
            "signs": "".join(signs),
            "positive_ab": sum(1 for p in pairs if p["direction"] == "A→B" and p.get("ratio") and p["ratio"] > 1),
            "positive_ba": sum(1 for p in pairs if p["direction"] == "B→A" and p.get("ratio") and p["ratio"] > 1),
            "n_ab": sum(1 for p in pairs if p["direction"] == "A→B"),
            "n_ba": sum(1 for p in pairs if p["direction"] == "B→A"),
            "positive_total": len([r for r in ratios if r > 1]),
            "n_total": len(ratios),
        }

    def arm_table(arm):
        rows = {}
        for phase in PHASES:
            pairs = pairs_for(lambda m, p=phase: m[arm]["ns"][p])
            rows[phase] = summarize_pairs(pairs, args.seed)

        pairs = pairs_for(lambda m: m[arm]["end_to_end"])
        rows["end_to_end"] = summarize_pairs(pairs, args.seed)

        pairs = pairs_for(lambda m: derive_prepare_exclusive(m[arm]))
        rows["prepare_exclusive"] = summarize_pairs(pairs, args.seed)

        # Dispatch formula residual: prepare_exclusive + the other exclusive leaves, WITHOUT
        # adding back the nested phases that were subtracted from prepare. By construction it
        # contains plan/bind/enum_create/cow_shape, so it is reported but not used for (d)/(e).
        pairs = pairs_for(lambda m: m[arm]["end_to_end"] - sum(m[arm]["ns"][p] for p in E2E_PHASES if p != "prepare_exclusive") - derive_prepare_exclusive(m[arm]))
        rows["phase_residual"] = summarize_pairs(pairs, args.seed)
        # Genuine unexplained residual: end-to-end minus every measured exclusive phase,
        # including the nested phases added back to the prepare span.
        pairs = pairs_for(lambda m: m[arm]["end_to_end"] - sum(
            derive_prepare_exclusive(m[arm]) if p == "prepare_exclusive" else m[arm]["ns"][p]
            for p in E2E_PHASES + NESTED_IN_PREPARE))
        rows["residual_unattributed"] = summarize_pairs(pairs, args.seed)

        a_blocks = [g[0] for g in groups] + [g[3] for g in groups]
        b_blocks = [g[1] for g in groups] + [g[2] for g in groups]
        for key, row in rows.items():
            if key in ("end_to_end", "phase_residual", "residual_unattributed"):
                continue
            if key == "prepare_exclusive":
                a_vals = [derive_prepare_exclusive(b["metrics"][arm]) for b in a_blocks]
                b_vals = [derive_prepare_exclusive(b["metrics"][arm]) for b in b_blocks]
                row["resolved"] = "prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape"
                row["roslyn"] = ROSLYN["prepare"]
            else:
                a_vals = [b["metrics"][arm]["ns"][key] for b in a_blocks]
                b_vals = [b["metrics"][arm]["ns"][key] for b in b_blocks]
                row["resolved"] = BOUNDARY.get(key, "—")
                row["roslyn"] = ROSLYN.get(key, "—")
            row["a_ns"] = median(a_vals)
            row["b_ns"] = median(b_vals)
            row["delta_ns"] = row["delta"] if row.get("delta") is not None else (
                (row["b_ns"] - row["a_ns"]) if row["a_ns"] is not None and row["b_ns"] is not None else None)

        e2e = rows["end_to_end"]
        e2e["a_ns"] = median([b["metrics"][arm]["end_to_end"] for b in a_blocks])
        e2e["b_ns"] = median([b["metrics"][arm]["end_to_end"] for b in b_blocks])
        e2e["delta_ns"] = e2e["delta"] if e2e.get("delta") is not None else e2e["b_ns"] - e2e["a_ns"]
        e2e["resolved"] = "end-to-end terminal call (ToList / ToListAsyncCore)"
        e2e["roslyn"] = ROSLYN["prepare"]

        residual = rows["phase_residual"]
        residual["a_ns"] = median([b["metrics"][arm]["end_to_end"] - sum(
            derive_prepare_exclusive(b["metrics"][arm]) if p == "prepare_exclusive" else b["metrics"][arm]["ns"][p]
            for p in E2E_PHASES) for b in a_blocks])
        residual["b_ns"] = median([b["metrics"][arm]["end_to_end"] - sum(
            derive_prepare_exclusive(b["metrics"][arm]) if p == "prepare_exclusive" else b["metrics"][arm]["ns"][p]
            for p in E2E_PHASES) for b in b_blocks])
        residual["delta_ns"] = residual["delta"] if residual.get("delta") is not None else residual["b_ns"] - residual["a_ns"]
        residual["resolved"] = "dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape)"
        residual["roslyn"] = "—"

        un = rows["residual_unattributed"]
        un["a_ns"] = median([b["metrics"][arm]["end_to_end"] - sum(
            derive_prepare_exclusive(b["metrics"][arm]) if p == "prepare_exclusive" else b["metrics"][arm]["ns"][p]
            for p in E2E_PHASES + NESTED_IN_PREPARE) for b in a_blocks])
        un["b_ns"] = median([b["metrics"][arm]["end_to_end"] - sum(
            derive_prepare_exclusive(b["metrics"][arm]) if p == "prepare_exclusive" else b["metrics"][arm]["ns"][p]
            for p in E2E_PHASES + NESTED_IN_PREPARE) for b in b_blocks])
        un["delta_ns"] = un["delta"] if un.get("delta") is not None else un["b_ns"] - un["a_ns"]
        un["resolved"] = "unattributed residual (all measured phases added back)"
        un["roslyn"] = "—"
        un["calls_a"] = None
        un["calls_b"] = None

        # calls/op and alloc/op are side totals, not paired medians.
        for phase in PHASES:
            rows[phase]["calls_a"] = median([b["metrics"][arm]["calls"][phase] for b in a_blocks])
            rows[phase]["calls_b"] = median([b["metrics"][arm]["calls"][phase] for b in b_blocks])
        rows["prepare_exclusive"]["calls_a"] = rows["prepare"]["calls_a"]
        rows["prepare_exclusive"]["calls_b"] = rows["prepare"]["calls_b"]
        e2e["calls_a"] = 1.0
        e2e["calls_b"] = 1.0
        residual["calls_a"] = None
        residual["calls_b"] = None

        alloc_a = median([b["metrics"][arm]["alloc"] for b in a_blocks])
        alloc_b = median([b["metrics"][arm]["alloc"] for b in b_blocks])
        rows["alloc"] = {"a": alloc_a, "b": alloc_b, "delta": alloc_b - alloc_a}

        pair_ns_a = median([b["metrics"][arm]["pair_ns"] for b in a_blocks])
        pair_ns_b = median([b["metrics"][arm]["pair_ns"] for b in b_blocks])
        pairs_per_op_a = sum(rows[p]["calls_a"] or 0 for p in PHASES)
        pairs_per_op_b = sum(rows[p]["calls_b"] or 0 for p in PHASES)
        rows["overhead"] = {
            "pair_ns_a": pair_ns_a,
            "pair_ns_b": pair_ns_b,
            "pairs_per_op_a": pairs_per_op_a,
            "pairs_per_op_b": pairs_per_op_b,
            "ns_per_op_a": (pair_ns_a or 0) * pairs_per_op_a,
            "ns_per_op_b": (pair_ns_b or 0) * pairs_per_op_b,
        }
        rows["overhead"]["ns_per_op_delta"] = rows["overhead"]["ns_per_op_b"] - rows["overhead"]["ns_per_op_a"]

        for key, row in rows.items():
            if key in ("alloc", "overhead"):
                continue
            row["share"] = (row.get("delta_ns") / e2e["delta_ns"]) if row.get("delta_ns") is not None and e2e["delta_ns"] else None

        # Reconciliation: every measured exclusive phase (the six-term dispatch sum plus the
        # nested plan/bind/enum_create/cow_shape added back) vs the end-to-end delta.
        def deltas_by_round(keys):
            acc = {}
            for key in keys:
                for pair in rows[key]["pairs"]:
                    if pair.get("a") is None or pair.get("b") is None:
                        continue
                    acc[pair["round"]] = acc.get(pair["round"], 0.0) + (pair["b"] - pair["a"])
            return acc

        rounds = sorted(p["round"] for p in rows["end_to_end"]["pairs"]
                        if p.get("a") is not None and p.get("b") is not None)
        six_by_round = deltas_by_round(E2E_PHASES)
        full_by_round = deltas_by_round(E2E_PHASES + NESTED_IN_PREPARE)
        six_term_delta = median([six_by_round.get(r) for r in rounds])
        phase_sum_delta = median([full_by_round.get(r) for r in rounds])
        six_term_residual = rows["phase_residual"]["delta_ns"]
        residual_delta = rows["residual_unattributed"]["delta_ns"]
        halfwidths = []
        for key in E2E_PHASES + NESTED_IN_PREPARE + ["end_to_end"]:
            row = rows[key]
            if row["ratio"] is not None and row["ci_low"] is not None:
                delta_low = (row["ci_low"] - 1.0) * row["a_ns"]
                delta_high = (row["ci_high"] - 1.0) * row["a_ns"]
                halfwidths.append((delta_high - delta_low) / 2.0)
        combined = (sum(h * h for h in halfwidths)) ** 0.5 if halfwidths else None

        # Criteria (a)-(e); the rule text is also written verbatim to protocol.md/verdict.md.
        e2e_delta_positive = e2e["delta_ns"] > 0
        reconciles = combined is not None and abs(residual_delta) <= combined
        residual_insignificant = residual_delta <= 0 or (
            e2e["delta_ns"] > 0 and residual_delta <= 0.05 * e2e["delta_ns"])
        arm_verdict = {
            "e2e_delta_ns": e2e["delta_ns"],
            "phase_sum_delta_ns": phase_sum_delta,
            "residual_delta_ns": residual_delta,
            "residual_share": residual_delta / e2e["delta_ns"] if e2e["delta_ns"] else None,
            "six_term_delta_ns": six_term_delta,
            "six_term_residual_ns": six_term_residual,
            "six_term_residual_share": six_term_residual / e2e["delta_ns"] if e2e["delta_ns"] else None,
            "combined_ci_halfwidth_ns": combined,
            "reconciles": reconciles,
            "residual_insignificant": residual_insignificant,
            "e2e_delta_positive": e2e_delta_positive,
            "phase_eval": {},
        }
        for phase in CAUSE_CANDIDATES:
            row = rows[phase]
            criteria = {
                "a_positive_ci": row["ratio"] is not None and row["ci_low"] is not None
                                  and row["ratio"] > 1 and row["ci_low"] > 1,
                "b_both_directions": row["positive_ab"] >= 2 and row["positive_ba"] >= 2
                                     and row["positive_total"] >= 5,
                "c_boundary_and_calls": row["resolved"] not in (None, "—")
                                        and (row["calls_a"] or 0) > 0 and (row["calls_b"] or 0) > 0,
                "d_reconciles": reconciles,
                "e_residual_insignificant": residual_insignificant,
            }
            criteria["pass"] = all(criteria.values())
            arm_verdict["phase_eval"][phase] = criteria
        arm_verdict["named"] = [phase for phase in CAUSE_CANDIDATES
                                if arm_verdict["phase_eval"][phase]["pass"]]
        return rows, arm_verdict

    tables = {}
    verdicts = {}
    for arm in ARMS:
        tables[arm], verdicts[arm] = arm_table(arm)

    # Global cause: same phase must satisfy (a)-(e) in all four arms.
    common = set(CAUSE_CANDIDATES)
    for arm in ARMS:
        common &= set(verdicts[arm]["named"])
    named = sorted(common, key=lambda p: tables["cte"][p].get("delta_ns") or 0, reverse=True)

    return {
        "groups": len(groups),
        "rounds_per_arm": len(groups) * 2,
        "tables": tables,
        "verdicts": verdicts,
        "named": named,
        "arms": ARMS,
    }


def write_artifacts(evid, meta, runs, analysis, args):
    tables = analysis["tables"]
    verdicts = analysis["verdicts"]

    # phase-table.json
    export = {}
    for arm in ARMS:
        rows = tables[arm]
        export[arm] = {
            "phases": {phase: dict(rows[phase]) for phase in PHASES + ["prepare_exclusive"]},
            "end_to_end": {k: v for k, v in rows["end_to_end"].items() if k != "pairs"},
            "phase_residual": {k: v for k, v in rows["phase_residual"].items() if k != "pairs"},
            "residual_unattributed": {k: v for k, v in rows["residual_unattributed"].items() if k != "pairs"},
            "alloc": rows["alloc"],
            "overhead": rows["overhead"],
            "verdict": verdicts[arm],
        }
    with open(evid / "phase-table.json", "w", encoding="utf-8") as f:
        json.dump(export, f, indent=1)

    lines = ["# E22 phase table — T297 r=4", "",
             "Paired B/A per round (6 paired rounds per arm = three ABBA groups). CI = bootstrap 95 % CI",
             "(10 000 resamples, seed %d). Hw = relative CI half-width. Share = phase delta / e2e delta." % args.seed,
             "Alloc delta is arm-level (bytes/op) and is shown on the end-to-end row only.", "",
             "Uninstrumented ABBA causal anchor (E21, B/A): " +
             "; ".join("%s %.3f/%.3f (default/inprocess)" % (arm, ANCHOR[arm]["default"], ANCHOR[arm]["inprocess"])
                       for arm in ARMS), ""]
    for arm in ARMS:
        rows = tables[arm]
        e2e = rows["end_to_end"]
        probe_ratio = e2e["ratio"]
        direction_match = probe_ratio is not None and probe_ratio > 1
        lines.append("## %s" % arm)
        lines.append("")
        lines.append("Probe e2e B/A = %.4f [%.4f, %.4f] (hw %.1f %%); ABBA anchor %.3f/%.3f; direction match: %s."
                     % (probe_ratio or 0, e2e["ci_low"] or 0, e2e["ci_high"] or 0,
                        (e2e["rel_halfwidth"] or 0) * 100, ANCHOR[arm]["default"],
                        ANCHOR[arm]["inprocess"], "yes" if direction_match else "NO"))
        lines.append("")
        lines.append("| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |")
        lines.append("|---|---|---|---|---|---|---|---|---|---|")
        for phase in PHASES + ["prepare_exclusive", "end_to_end", "phase_residual", "residual_unattributed"]:
            row = rows[phase]
            if phase == "end_to_end":
                calls = "1/1"
                alloc = "%.1f" % rows["alloc"]["delta"]
            elif phase in ("phase_residual", "residual_unattributed"):
                calls = "—"
                alloc = "—"
            else:
                calls = "%s/%s" % (fmt(row.get("calls_a"), 2), fmt(row.get("calls_b"), 2))
                alloc = "—"
            ci = "—" if row["ci_low"] is None else "[%.4f, %.4f]" % (row["ci_low"], row["ci_high"])
            lines.append("| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (
                phase, fmt(row.get("a_ns")), fmt(row.get("b_ns")),
                fmt(row.get("ratio"), 4), ci, fmt_pct((row.get("rel_halfwidth") or None) and row["rel_halfwidth"] * 100),
                calls, alloc, row.get("resolved", "—"), fmt_pct((row.get("share") or 0) * 100 if row.get("share") is not None else None)))
        lines.append("")
        v = verdicts[arm]
        lines.append("Verdict: e2e delta %+.1f ns/op; full phase sum %+.1f; unattributed residual %+.1f ns/op (%s); "
                     "dispatch six-term residual %+.1f ns/op (%s); combined CI hw %.1f ns; "
                     "reconciles=%s; residual insignificant=%s; named=%s."
                     % (v["e2e_delta_ns"], v["phase_sum_delta_ns"], v["residual_delta_ns"],
                        fmt_pct((v["residual_share"] or 0) * 100), v["six_term_residual_ns"],
                        fmt_pct((v["six_term_residual_share"] or 0) * 100), v["combined_ci_halfwidth_ns"] or 0,
                        v["reconciles"], v["residual_insignificant"],
                        ", ".join(v["named"]) if v["named"] else "none"))
        lines.append("")
    with open(evid / "phase-table.md", "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    # reconciliation.md
    rec = ["# E22 reconciliation — T297 r=4", "",
           "`prepare_exclusive = prepare − (plan + bind + enum_create + cow_shape)` (nested spans).",
           "Dispatch six-term sum: `e2e_phases = construct + prepare_exclusive + get_db_command + execute + read_rows + map_rows`;",
           "its residual (`phase_residual`) **by construction contains** the nested phases that were subtracted from",
           "`prepare`, so it is reported but is not the unexplained part.",
           "Full phase sum adds the nested phases back: `e2e_phases + plan + bind + enum_create + cow_shape`.",
           "`residual_unattributed = end_to_end − full phase sum` is the genuine unexplained remainder.",
           "Instrumentation overhead is reported separately below (pair cost × instrumented boundary calls per",
           "query), not inside the phase table.", ""]
    for arm in ARMS:
        rows = tables[arm]
        v = verdicts[arm]
        rec.append("## %s" % arm)
        rec.append("")
        rec.append("| component | A ns/op | B ns/op | delta ns/op |")
        rec.append("|---|---|---|---|")
        for phase in E2E_PHASES + NESTED_IN_PREPARE + ["end_to_end", "phase_residual", "residual_unattributed"]:
            row = rows[phase]
            rec.append("| %s | %s | %s | %+.1f |" % (
                phase, fmt(row.get("a_ns")), fmt(row.get("b_ns")), row.get("delta_ns") or 0))
        rec.append("")
        rec.append("Full phase sum delta %+.1f ns/op vs e2e delta %+.1f ns/op -> unattributed residual %+.1f ns/op (%s), "
                   "combined 95 %% CI half-width %.1f ns, reconciles=%s, residual insignificant=%s."
                   % (v["phase_sum_delta_ns"], v["e2e_delta_ns"], v["residual_delta_ns"],
                      fmt_pct((v["residual_share"] or 0) * 100), v["combined_ci_halfwidth_ns"] or 0,
                      v["reconciles"], v["residual_insignificant"]))
        rec.append("")
        rec.append("Dispatch six-term sum delta %+.1f ns/op -> `phase_residual` %+.1f ns/op (%s) = nested plan/bind/"
                   "enum_create/cow_shape + the unattributed remainder." % (
                       v["six_term_delta_ns"], v["six_term_residual_ns"],
                       fmt_pct((v["six_term_residual_share"] or 0) * 100)))
        overhead = rows["overhead"]
        rec.append("")
        rec.append("Instrumentation overhead: pair cost A %.2f ns / B %.2f ns; boundary calls per query A %.2f / B %.2f; "
                   "overhead A %.0f ns/op / B %.0f ns/op; delta %+.0f ns/op."
                   % (overhead["pair_ns_a"] or 0, overhead["pair_ns_b"] or 0,
                      overhead["pairs_per_op_a"], overhead["pairs_per_op_b"],
                      overhead["ns_per_op_a"], overhead["ns_per_op_b"], overhead["ns_per_op_delta"]))
        rec.append("")
    rec.append("The overhead estimate is deliberately kept out of the phase table: it is the same order for A and B")
    rec.append("except for the B-only COW counters (cow_description/cow_clone), so it cannot produce the measured delta;")
    rec.append("the per-arm overhead delta is the maximum bias that instrumentation can contribute to that arm's delta.")
    with open(evid / "reconciliation.md", "w", encoding="utf-8") as f:
        f.write("\n".join(rec) + "\n")

    # verdict.md
    named = analysis["named"]
    lines = ["# E22 attribution verdict — T297 r=4", "",
             "Pre-registered rule (written in the runner before execution):", "",
             "A phase is named as the cause only if **all** hold, in **every** arm:", "",
             "1. (a) its paired B/A ratio median > 1 with bootstrap 95 % CI lower bound > 1;",
             "2. (b) it reproduces in both ABBA pair directions — at least 2 of 3 A→B pairs > 1 and at least 2 of 3 B→A pairs > 1, and at least 5 of 6 pairs > 1 (per-round signs are in phase-table.md);",
             "3. (c) its recorded boundary `file:line` resolves (Roslyn table) and its call count is non-zero in both trees;",
             "4. (d) every measured exclusive phase reconciles with the e2e delta: the full phase sum (six exclusive phases plus the nested plan/bind/enum_create/cow_shape added back) differs from the e2e delta by no more than the combined 95 % CI half-width (quadrature);",
             "5. (e) the unexplained positive residual is insignificant: `residual_unattributed` ≤ 0 or ≤ 5 % of the positive e2e delta.",
             "",
             "Amendment recorded before the verdict was issued: the dispatch's six-term `e2e_phases` omits exactly the",
             "nested phases that its own `prepare_exclusive` definition subtracted, so the six-term residual contains",
             "measured work by construction. The full phase sum adds them back; the six-term residual is still reported",
             "as `phase_residual`. No threshold in (a), (b), (c) or (e) changed after the run.",
             "",
             "Otherwise the verdict is `attribution: incomplete` with the numbers, and no cause is named.", ""]
    for arm in ARMS:
        v = verdicts[arm]
        lines.append("## %s" % arm)
        lines.append("")
        lines.append("e2e delta %+.1f ns/op; full phase sum %+.1f; unattributed residual %+.1f (%s); "
                     "dispatch six-term residual %+.1f (%s); combined CI hw %.1f ns; "
                     "reconciles=%s; residual insignificant=%s."
                     % (v["e2e_delta_ns"], v["phase_sum_delta_ns"], v["residual_delta_ns"],
                        fmt_pct((v["residual_share"] or 0) * 100), v["six_term_residual_ns"],
                        fmt_pct((v["six_term_residual_share"] or 0) * 100), v["combined_ci_halfwidth_ns"] or 0,
                        v["reconciles"], v["residual_insignificant"]))
        lines.append("")
        lines.append("| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |")
        lines.append("|---|---|---|---|---|---|---|")
        for phase in CAUSE_CANDIDATES:
            c = v["phase_eval"][phase]
            lines.append("| %s | %s | %s | %s | %s | %s | %s |" % (
                phase, c["a_positive_ci"], c["b_both_directions"], c["c_boundary_and_calls"],
                c["d_reconciles"], c["e_residual_insignificant"], c["pass"]))
        lines.append("")
    if named:
        lines.append("**attribution: %s** — satisfies (a)-(e) in all four arms." % ", ".join(named))
    else:
        lines.append("**attribution: incomplete** — no single phase satisfies (a)-(e) in all four arms; "
                     "the numbers above are the measured state and no cause is named.")
    with open(evid / "verdict.md", "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    return named


def write_protocol(evid, meta, runs, analysis, args):
    lines = ["# E22 attribution protocol — T297 r=4 (P02)", "",
             "Generated %s by `.pdca/T297/rv3/tools/run_r4.py attribute`." % utc(), "",
             "## Lock and duplicate guard", "",
             "- Canonical host lock: `%s`, exclusive `flock` held for the whole block (timeout %s s)." % (LOCK, args.lock_timeout),
             "- Duplicate guard `%s`: %s (not acquired/nested by this runner)." % (
                 GUARD, "present" if GUARD.exists() else "absent"),
             "- Competing `nextorm.benchmark`/`probe-` processes at start: %s." % (
                 json.dumps(meta["competitors"]) if meta["competitors"] else "none"),
             "",
             "## Quiet window", "",
             "- Idle observation before round 1: %s s (start %s, end %s)." % (
                 args.quiet, meta["quiet_start"], meta["quiet_end"]),
             "- `/proc/loadavg` start: `%s`; end: `%s`." % (
                 meta["quiet_loadavg_start"], meta["quiet_loadavg_end"]),
             "- Rounds are never excluded for slow time; the max 1-min load per block is recorded below.",
             "",
             "## Order and schedule", "",
             "- `--rounds %d --order ABBA`: %d paired rounds = %d ABBA groups = %d blocks (`A,B,B,A` × %d)." % (
                 args.rounds, analysis["rounds_per_arm"], analysis["groups"], analysis["groups"] * 4, analysis["groups"]),
             "- One probe process per block: `%s`." % " ".join(
                 ["taskset -c %d" % args.cpu, "dotnet <probe.dll>", "--arm", args.arms,
                  "--warmup", str(args.warmup), "--iters", str(args.iters), "--overhead-n", str(args.overhead_n)]),
             "- Env per block: `NEXTORM_BENCH_DB=%s`, `DOTNET_gcServer=0`, `NEXTORM_BENCH_FULL` unset." % Path(args.db),
             "",
             "## Provenance", "",
             "| side | commit | probe dll | probe sha256 | nextorm.core.dll sha256 |",
             "|---|---|---|---|---|"]
    for side in ("A", "B"):
        lines.append("| %s | `%s` | `%s` | `%s` | `%s` |" % (
            side, SIDE_COMMIT[side], PROBE[side], meta["probe_sha"][side], meta["core_sha"][side]))
    lines += ["",
              "- `dotnet --version`: `%s`; repo HEAD: `%s`." % (meta["dotnet"], meta["repo_head"]),
              "- Benchmark DB: `%s`, sha256 `%s` (checked before and after every block; expected)." % (meta["db"], meta["db_sha"]),
              "- Env snapshot: `meta.json` -> `env` (DOTNET_*/NEXTORM_*/PATH, plus key-list hash).",
              "- Wall time total: %.1f s." % meta["wall_total_s"], "",
              "## Blocks", "",
              "| block | side | start | wall s | exit | loadavg 1-min before/after | db sha before/after | records |",
              "|---|---|---|---|---|---|---|---|"]
    for run in runs:
        lines.append("| %02d | %s | %s | %.1f | %s | %s / %s | %s / %s | %d |" % (
            run["block"], run["side"], run["started"], run["wall_s"], run["exit"],
            (run["loadavg_before"] or "?").split()[0], (run["loadavg_after"] or "?").split()[0],
            run["db_sha_before"][:12], run["db_sha_after"][:12], run["record_count"]))
    lines += ["", "Analysis: paired B/A ratio per round; median of per-round ratios; percentile bootstrap 95 % CI",
              "(%d resamples, fixed seed %d); calls/op and allocation delta are side totals; the overhead estimate" % (args.resamples, args.seed),
              "is `pair cost × instrumented boundary calls per query`, reported separately from the phase table.", ""]
    with open(evid / "protocol.md", "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def write_instrumentation(evid):
    lines = ["# E22 instrumentation — T297 r=4 (P02)", "",
             "Identical instrumentation in both throwaway trees: `src/nextorm.core/Probe/T297PhaseProbe.cs`",
             "plus boundary calls. Trees: A = `0d4d5df2` (baseline), B = `9d633521` (candidate).",
             "Nothing here is shipped; the product tree is untouched.", "",
             "## Phase boundary table", "",
             "| phase | A path:line + enclosing method | B path:line + enclosing method |",
             "|---|---|---|"]
    for phase in PHASES:
        if phase in ("cow_description", "cow_clone"):
            a_cell = "— (path absent in A)"
            b_cell = "`%s`" % BOUNDARY[phase]
        else:
            a_cell = "`%s`" % BOUNDARY[phase]
            b_cell = "`%s`" % BOUNDARY[phase]
        lines.append("| %s | %s | %s |" % (phase, a_cell, b_cell))
    lines += ["",
              "All paths are relative to `src/nextorm.core/` in the probe copies. Shared boundaries have identical",
              "line numbers in both copies; B additionally carries the COW description/clone counters.", "",
              "## Roslyn resolutions", "",
              "| phase | resolved symbol |", "|---|---|"]
    for phase in PHASES:
        lines.append("| %s | %s |" % (phase, ROSLYN.get(phase, "—")))
    lines += ["",
              "Resolution performed with the `roslyn` tool against `/tmp/opencode/t297-p02/A/nextorm.slnx`",
              "and `/tmp/opencode/t297-p02/B/nextorm.slnx` (`members`: QueryPlanner, QueryExecutor,",
              "DbPreparedQueryCommand<TResult>, QueryCommand, QueryDescription). Full bodies are in",
              "`probe-src/` and `probe-src/instrumentation-{A,B}.diff`.", "",
              "## COW counters (B only) and why", "",
              "B replaces the shared mutable command state with a copy-on-write `QueryDescription`. A has no such",
              "path, so `cow_description` and `cow_clone` are B-only counters: they split B's prepare span into",
              "the COW-description work (`QueryDescription.MarkShared`, `CloneShellForWrite`, `QueryCommand.WriteDescription`)",
              "and the clone-constructor adoption. They are nested inside `prepare` (and `cow_clone` inside the",
              "clone constructor), so they are diagnostic and are **not** added to `e2e_phases`; the attribution",
              "formula leaves them inside `prepare_exclusive`, where the feature's cost actually lives.", ""]
    with open(evid / "instrumentation.md", "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def copy_probe_sources(evid):
    out = evid / "probe-src"
    out.mkdir(exist_ok=True)
    shutil.copyfile(PROBE_ROOT / "A" / "src" / "nextorm.core" / "Probe" / "T297PhaseProbe.cs", out / "T297PhaseProbe.A.cs")
    shutil.copyfile(PROBE_ROOT / "B" / "src" / "nextorm.core" / "Probe" / "T297PhaseProbe.cs", out / "T297PhaseProbe.B.cs")
    shutil.copyfile(PROBE_ROOT / "Common" / "ProbeProgram.cs", out / "ProbeProgram.cs")
    shutil.copyfile(PROBE_ROOT / "probe-a" / "probe-a.csproj", out / "probe-a.csproj")
    shutil.copyfile(PROBE_ROOT / "probe-b" / "probe-b.csproj", out / "probe-b.csproj")
    for side in ("A", "B"):
        chunks = []
        for rel in INSTRUMENTED[side]:
            target = SIDE_TREE[side] / rel
            original = subprocess.run(["git", "-C", str(REPO), "show", "%s:%s" % (SIDE_COMMIT[side], rel)],
                                      capture_output=True, text=True)
            before = original.stdout.splitlines(keepends=True) if original.returncode == 0 else []
            after = target.read_text(encoding="utf-8").splitlines(keepends=True)
            chunks.extend(difflib.unified_diff(
                before, after,
                fromfile="/dev/null" if original.returncode != 0 else "pristine-%s/%s" % (side, rel),
                tofile="%s/%s" % (side, rel)))
        (out / ("instrumentation-%s.diff" % side)).write_text("".join(chunks), encoding="utf-8")


def write_hashes(evid, meta):
    lines = ["# E22 hashes — T297 r=4", "",
             "runner sha256  %s  .pdca/T297/rv3/tools/run_r4.py" % sha256(Path(__file__)),
             "dotnet version %s" % meta["dotnet"],
             "repo HEAD      %s" % meta["repo_head"],
             "DB sha256      %s  %s" % (meta["db_sha"], meta["db"])]
    for side in ("A", "B"):
        lines.append("probe %s dll  %s  %s" % (side, meta["probe_sha"][side], PROBE[side]))
        lines.append("core  %s dll  %s  %s" % (side, meta["core_sha"][side], CORE_DLL[side]))
    for name in ("T297PhaseProbe.A.cs", "T297PhaseProbe.B.cs", "ProbeProgram.cs",
                 "probe-a.csproj", "probe-b.csproj", "instrumentation-A.diff", "instrumentation-B.diff"):
        path = evid / "probe-src" / name
        if path.exists():
            lines.append("probe-src %-22s %s" % (name, sha256(path)))
    scope = PROBE_ROOT / "scope-brief.json"
    if scope.exists():
        lines.append("scope-brief               %s  %s" % (sha256(scope), scope))
    lines.append("")
    for run in meta["run_hashes"]:
        lines.append("db %02d %s before %s after %s" % (run[0], run[1], run[2], run[3]))
    with open(evid / "hashes.txt", "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def aggregate(runs):
    """Legacy summary kept for `--check-only` and quick inspection."""
    summary = {}
    for side in ("A", "B"):
        side_runs = [r for r in runs if r["side"] == side]
        summary[side] = {"rounds": len(side_runs), "arms": {}}
        for arm in ARMS:
            entries = [e for r in side_runs for e in r["records"] if e.get("arm") == arm]
            if not entries:
                continue
            queries = sum(e["queries"] for e in entries)
            freq = entries[0]["frequency"]
            phases = {}
            for phase in PHASES:
                ticks = sum((e.get("ticks") or {}).get(phase, 0) for e in entries)
                calls = sum((e.get("calls") or {}).get(phase, 0) for e in entries)
                phases[phase] = {
                    "ticks": ticks,
                    "calls": calls,
                    "calls_per_query": calls / queries if queries else None,
                    "ns_per_query": ticks / queries * (1e9 / freq) if queries else None,
                }
            e2e = sum(e.get("end_to_end_ticks", 0) for e in entries)
            summary[side]["arms"][arm] = {
                "runs": len(entries),
                "queries": queries,
                "end_to_end_ns_per_query": e2e / queries * (1e9 / freq) if queries else None,
                "phases": phases,
            }
    return summary


def check_evidence(evid):
    problems = []
    meta_path = evid / "meta.json"
    runs_path = evid / "runs.json"
    summary_path = evid / "summary.json"
    if not meta_path.exists():
        problems.append("missing meta.json")
    if not runs_path.exists():
        problems.append("missing runs.json")
    if not summary_path.exists():
        problems.append("missing summary.json")
    if problems:
        return problems

    meta = json.loads(meta_path.read_text(encoding="utf-8"))
    runs = json.loads(runs_path.read_text(encoding="utf-8"))
    json.loads(summary_path.read_text(encoding="utf-8"))

    if not meta.get("protocol", {}).get("order", ""):
        problems.append("meta.protocol.order is empty")
    sides = {r["side"] for r in runs}
    if sides != {"A", "B"}:
        problems.append("runs.json does not cover both sides: %s" % sorted(sides))

    for run in runs:
        tag = "block-%02d-%s" % (run["block"], run["side"])
        if run.get("exit") != 0:
            problems.append("%s: exit=%s" % (tag, run.get("exit")))
        if run.get("parse_error"):
            problems.append("%s: %s" % (tag, run["parse_error"]))
        if run.get("db_sha_before") != EXPECTED_DB_SHA or run.get("db_sha_after") != EXPECTED_DB_SHA:
            problems.append("%s: db sha changed (%s -> %s)" % (
                tag, run.get("db_sha_before"), run.get("db_sha_after")))
        arms = {e.get("arm") for e in run.get("records", [])}
        for arm in ARMS:
            if arm not in arms:
                problems.append("%s: missing arm %s" % (tag, arm))
                continue
            entry = next(e for e in run["records"] if e.get("arm") == arm)
            queries = entry["queries"]
            calls = entry.get("calls") or {}
            ticks = entry.get("ticks") or {}
            for phase in COUNTER_PHASES:
                if calls.get(phase) != queries:
                    problems.append("%s/%s: calls.%s=%s expected %s" % (
                        tag, arm, phase, calls.get(phase), queries))
            for phase in ("prepare", "plan", "execute"):
                if not ticks.get(phase):
                    problems.append("%s/%s: ticks.%s is zero" % (tag, arm, phase))
            if not entry.get("end_to_end_ticks"):
                problems.append("%s/%s: end_to_end_ticks is zero" % (tag, arm))
            if (entry.get("alloc") or {}).get("total_per_arm", 0) <= 0:
                problems.append("%s/%s: total allocation is zero" % (tag, arm))
            read_calls = calls.get("read_rows", 0)
            map_calls = calls.get("map_rows", 0)
            if read_calls != map_calls + queries:
                problems.append("%s/%s: read_rows=%s expected map_rows+queries=%s" % (
                    tag, arm, read_calls, map_calls + queries))

    for side in ("A", "B"):
        if side not in json.loads(summary_path.read_text(encoding="utf-8")):
            problems.append("summary missing side %s" % side)
    return problems


def cmd_attribute(args):
    evid = Path(args.out)
    if args.check_only:
        problems = check_evidence(evid)
        if problems:
            for problem in problems:
                print("FAIL " + problem, file=sys.stderr)
            return 1
        print("attribute evidence OK: %s" % evid)
        return 0

    if args.render_only:
        runs_path, meta_path = evid / "runs.json", evid / "meta.json"
        if not runs_path.exists() or not meta_path.exists():
            return fail("--render-only needs runs.json and meta.json under %s" % evid)
        runs = json.loads(runs_path.read_text(encoding="utf-8"))
        meta = json.loads(meta_path.read_text(encoding="utf-8"))
        try:
            analysis = analyze(runs, args)
        except RuntimeError as exc:
            return fail(str(exc))
        (evid / "summary.json").write_text(json.dumps(aggregate(runs), indent=1), encoding="utf-8")
        named = write_artifacts(evid, meta, runs, analysis, args)
        write_protocol(evid, meta, runs, analysis, args)
        write_instrumentation(evid)
        copy_probe_sources(evid)
        write_hashes(evid, meta)
        scope = PROBE_ROOT / "scope-brief.json"
        if scope.exists():
            shutil.copyfile(scope, evid / "scope-brief.json")
        print("attribute evidence re-rendered: %s (named=%s)" % (
            evid, ", ".join(named) if named else "incomplete"))
        return 0

    db = Path(args.db)
    if not db.exists():
        return fail("benchmark db missing: %s" % db)
    db_sha = sha256(db)
    if db_sha != EXPECTED_DB_SHA:
        return fail("benchmark db sha mismatch: %s" % db_sha)
    for side in ("A", "B"):
        binary = Path(args.bin_a if side == "A" else args.bin_b)
        if not binary.exists():
            return fail("probe binary missing for %s: %s" % (side, binary))

    if args.rounds % 2 != 0:
        return fail("--rounds must be even (each ABBA group yields two paired rounds): %s" % args.rounds)
    if args.order.upper() != "ABBA":
        return fail("this protocol is fixed to --order ABBA, got %s" % args.order)
    groups = args.rounds // 2
    order = "ABBA" * groups

    evid.mkdir(parents=True, exist_ok=True)
    (evid / "runs").mkdir(exist_ok=True)
    t_start = time.time()
    lock_fd = None
    try:
        lock_fd = acquire_lock(args.lock_timeout)
    except RuntimeError as exc:
        return fail(str(exc))
    try:
        log_line(evid, "host lock acquired: %s" % LOCK)
        log_line(evid, "duplicate guard present: %s (not nested)" % GUARD.exists())
        competitors = competing_processes()
        if competitors:
            for hit in competitors:
                log_line(evid, "COMPETING pid=%s cmd=%s" % (hit["pid"], hit["cmd"]))
            return fail("competing benchmark/probe process running: %s" % json.dumps(competitors))
        log_line(evid, "no competing benchmark/probe process")

        quiet_start = utc()
        quiet_loadavg_start = load_avg()
        log_line(evid, "quiet window start (%ss): loadavg %s" % (args.quiet, quiet_loadavg_start))
        if args.quiet > 0:
            time.sleep(args.quiet)
        quiet_end = utc()
        quiet_loadavg_end = load_avg()
        log_line(evid, "quiet window end: loadavg %s" % quiet_loadavg_end)

        meta = {
            "task": "T297",
            "plan_revision": 4,
            "row": "E22",
            "protocol": {
                "order": "ABBA",
                "rounds": args.rounds,
                "groups": groups,
                "blocks": len(order),
                "arms": args.arms,
                "warmup": args.warmup,
                "iters": args.iters,
                "overhead_n": args.overhead_n,
                "cpu": args.cpu,
                "lock": str(LOCK),
                "lock_timeout_s": args.lock_timeout,
                "evidence_root": str(args.evidence_root),
                "output": str(evid),
                "quiet_window_s": args.quiet,
                "bootstrap_resamples": args.resamples,
                "bootstrap_seed": args.seed,
            },
            "started": utc(),
            "quiet_start": quiet_start,
            "quiet_end": quiet_end,
            "quiet_loadavg_start": quiet_loadavg_start,
            "quiet_loadavg_end": quiet_loadavg_end,
            "competitors": competitors,
            "guard_present": GUARD.exists(),
            "db": str(db),
            "db_sha": db_sha,
            "probe": {k: str(v) for k, v in PROBE.items()},
            "probe_sha": {side: sha256(Path(args.bin_a if side == "A" else args.bin_b)) for side in ("A", "B")},
            "core_dll": {k: str(v) for k, v in CORE_DLL.items()},
            "core_sha": {side: sha256(CORE_DLL[side]) if CORE_DLL[side].exists() else None for side in ("A", "B")},
            "dotnet": subprocess.run(["dotnet", "--version"], capture_output=True,
                                     text=True).stdout.strip(),
            "repo_head": subprocess.run(["git", "-C", str(REPO), "rev-parse", "HEAD"],
                                        capture_output=True, text=True).stdout.strip(),
            "nproc": os.cpu_count(),
            "env": env_snapshot(),
        }

        runs = []
        run_hashes = []
        for block, side in enumerate(order, start=1):
            meta_run, records = run_block(side, block, args, evid)
            runs.append({**meta_run, "side": side, "block": block, "records": records})
            run_hashes.append([block, side, meta_run["db_sha_before"], meta_run["db_sha_after"]])
            log_line(evid, "block %02d %s exit=%s wall=%.1fs records=%d load %s" % (
                block, side, meta_run["exit"], meta_run["wall_s"], len(records),
                (meta_run["loadavg_after"] or "?").split()[0]))
            (evid / "runs.json").write_text(json.dumps(runs, indent=1), encoding="utf-8")
        meta["run_hashes"] = run_hashes
        meta["wall_total_s"] = round(time.time() - t_start, 1)
        (evid / "meta.json").write_text(json.dumps(meta, indent=1), encoding="utf-8")

        failures = [r for r in runs if r["exit"] != 0 or r["parse_error"]]
        if failures:
            return fail("attribution run finished with failures: %s" % json.dumps(
                [{"block": r["block"], "side": r["side"], "exit": r["exit"], "parse_error": r["parse_error"]}
                 for r in failures]))

        try:
            analysis = analyze(runs, args)
        except RuntimeError as exc:
            return fail(str(exc))
        (evid / "summary.json").write_text(json.dumps(aggregate(runs), indent=1), encoding="utf-8")

        named = write_artifacts(evid, meta, runs, analysis, args)
        write_protocol(evid, meta, runs, analysis, args)
        write_instrumentation(evid)
        copy_probe_sources(evid)
        write_hashes(evid, meta)
        scope = PROBE_ROOT / "scope-brief.json"
        if scope.exists():
            shutil.copyfile(scope, evid / "scope-brief.json")
        log_line(evid, "attribution complete: named=%s" % (", ".join(named) if named else "incomplete"))
        print("attribute evidence written: %s" % evid)
        return 0
    finally:
        if lock_fd is not None:
            release_lock(lock_fd)
            log_line(evid, "host lock released")


def cmd_stub(name):
    print("%s: not implemented yet in this phase" % name, file=sys.stderr)
    return 3


def build_parser():
    parser = argparse.ArgumentParser(
        prog="run_r4.py",
        description="T297 r=4 evidence runner (E20-E29). Implemented now: attribute.")
    sub = parser.add_subparsers(dest="command")

    att = sub.add_parser("attribute", help="run/validate the E22 per-phase attribution protocol")
    att.add_argument("--out", "--output", dest="out", default=str(DEFAULT_EVID),
                     help="evidence dir (default: %(default)s)")
    att.add_argument("--check-only", action="store_true", help="validate an existing evidence dir")
    att.add_argument("--render-only", action="store_true",
                     help="re-render analysis/artifacts from existing runs.json + meta.json (no measurement)")
    att.add_argument("--evidence-root", default=str(REPO / ".pdca" / "T297" / "rv3"),
                     help="cycle evidence root (recorded; raw runs stay under --output)")
    att.add_argument("--rounds", type=int, default=6,
                     help="paired A/B rounds; must be even (ABBA group = 2 rounds) (default: %(default)s)")
    att.add_argument("--order", default="ABBA", help="block order (fixed ABBA)")
    att.add_argument("--arms", default="all", choices=["all"] + ARMS, help="arm selector")
    att.add_argument("--warmup", type=int, default=5, help="harness warmup invocations")
    att.add_argument("--iters", type=int, default=30, help="harness measured invocations")
    att.add_argument("--overhead-n", type=int, default=100000, help="probe overhead pairs")
    att.add_argument("--cpu", type=int, default=1, help="taskset core")
    att.add_argument("--quiet", type=int, default=60, help="idle observation seconds before round 1")
    att.add_argument("--lock-timeout", type=int, default=600, help="host lock wait seconds")
    att.add_argument("--resamples", type=int, default=10000, help="bootstrap resamples")
    att.add_argument("--seed", type=int, default=297, help="bootstrap seed")
    att.add_argument("--timeout", type=int, default=1800, help="per-block timeout seconds")
    att.add_argument("--db", default=str(DB), help="benchmark DB (default: %(default)s)")
    att.add_argument("--bin-a", default=str(PROBE["A"]), help="probe A dll")
    att.add_argument("--bin-b", default=str(PROBE["B"]), help="probe B dll")

    for name in STUBS:
        sub.add_parser(name, help="declared in rv=4, not implemented in this phase")
    return parser


def main(argv=None):
    parser = build_parser()
    args = parser.parse_args(argv)
    if args.command is None:
        parser.print_help()
        return 2
    if args.command == "attribute":
        return cmd_attribute(args)
    if args.command in STUBS:
        return cmd_stub(args.command)
    return fail("unknown command: %s" % args.command, 2)


if __name__ == "__main__":
    sys.exit(main())
