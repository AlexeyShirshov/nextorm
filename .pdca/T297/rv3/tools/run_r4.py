#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""T297 r=4 evidence runner (stdlib only).

Subcommands map to the rv=4 contract rows E20-E29. This phase implements `attribute`
(E22, full-path per-phase attribution) and `--check-only`; every other subcommand is a
declared stub that exits 3 instead of silently succeeding.

Attribution protocol (dispatch P02b):
  * exclusive `flock` on /tmp/opencode/nextorm-bdn-host.lock for the whole run;
  * alternating A/B blocks in ABBA order, one pinned process per block
    (taskset -c 1, NEXTORM_BENCH_DB, DOTNET_gcServer=0, NEXTORM_BENCH_FULL unset);
  * raw JSONL + stderr per block, `runs.json`, `summary.json`, `meta.json`, `host.log`
    under `.pdca/T297/rv3/attribution/` (never outside the cycle evidence root or
    /tmp/opencode/t297-p02/).
"""

import argparse
import fcntl
import hashlib
import json
import os
import subprocess
import sys
import time
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
DEFAULT_EVID = REPO / ".pdca" / "T297" / "rv3" / "attribution"
LOCK = Path("/tmp/opencode/nextorm-bdn-host.lock")
DB = Path("/tmp/nextorm-bench/test.db")
EXPECTED_DB_SHA = "32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295"
PROBE = {
    "A": Path("/tmp/opencode/t297-p02/probe-a/bin/Release/net10.0/t297-probe-a.dll"),
    "B": Path("/tmp/opencode/t297-p02/probe-b/bin/Release/net10.0/t297-probe-b.dll"),
}
ARMS = ["cte", "recursive", "inlist", "leftjoin"]
PHASES = [
    "construct", "prepare", "plan", "bind", "enum_create", "cow_shape",
    "get_db_command", "execute", "read_rows", "map_rows", "cow_description", "cow_clone",
]
COUNTER_PHASES = ["construct", "prepare", "plan", "get_db_command", "execute"]
STUBS = [
    "seal", "fix-audit", "allocation-audit", "acceptance",
    "boundary", "coverage", "hygiene", "handoff",
]


def utc():
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def load_avg():
    try:
        return open("/proc/loadavg").read().strip()
    except OSError:
        return None


def fail(message, code=1):
    print("ERROR: " + message, file=sys.stderr)
    return code


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


def acquire_lock():
    fd = os.open(str(LOCK), os.O_RDWR | os.O_CREAT, 0o644)
    try:
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        os.close(fd)
        raise RuntimeError("host lock %s is held by another run" % LOCK)
    return fd


def release_lock(fd):
    try:
        fcntl.flock(fd, fcntl.LOCK_UN)
    finally:
        os.close(fd)


def run_block(side, index, args, evid):
    binary = Path(args.bin_a if side == "A" else args.bin_b)
    if not binary.exists():
        raise RuntimeError("probe binary missing for side %s: %s" % (side, binary))

    raw = evid / "raw"
    stem = "round-%02d-%s" % (index, side)
    jsonl = raw / (stem + ".jsonl")
    errlog = raw / (stem + ".err")
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
    started = utc()
    t0 = time.time()
    with open(jsonl, "w", encoding="utf-8") as out, open(errlog, "w", encoding="utf-8") as err:
        err.write("CMD %s\nSTART %s\n" % (" ".join(cmd), started))
        err.flush()
        proc = subprocess.run(cmd, cwd=str(evid), env=env, stdout=out, stderr=err,
                              timeout=args.timeout)
    wall = round(time.time() - t0, 3)
    db_sha_after = sha256(Path(args.db))

    try:
        records = parse_jsonl(jsonl)
    except ValueError as exc:
        records = []
        parse_error = str(exc)
    else:
        parse_error = None

    meta = {
        "side": side,
        "round": index,
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
        "loadavg": load_avg(),
        "cpu": args.cpu,
        "parse_error": parse_error,
        "records": len(records),
    }
    with open(raw / (stem + ".meta.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=1)
    return meta, records


def aggregate(runs):
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
            leaf_ns = sum(phases[p]["ns_per_query"] for p in
                          ("construct", "prepare", "get_db_command", "execute", "read_rows", "map_rows")
                          if phases[p]["ns_per_query"] is not None)
            summary[side]["arms"][arm] = {
                "runs": len(entries),
                "queries": queries,
                "end_to_end_ns_per_query": e2e / queries * (1e9 / freq) if queries else None,
                "leaf_ns_per_query": leaf_ns,
                "unattributed_ns_per_query": (e2e / queries * (1e9 / freq) - leaf_ns) if queries else None,
                "alloc_per_query": sum((e.get("alloc") or {}).get("total_per_arm", 0) for e in entries) / queries
                if queries else None,
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
    summary = json.loads(summary_path.read_text(encoding="utf-8"))

    order = meta.get("protocol", {}).get("order", "")
    if not order:
        problems.append("meta.protocol.order is empty")
    sides = {r["side"] for r in runs}
    if sides != {"A", "B"}:
        problems.append("runs.json does not cover both sides: %s" % sorted(sides))

    for run in runs:
        tag = "round-%02d-%s" % (run["round"], run["side"])
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
        if side not in summary:
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

    evid.mkdir(parents=True, exist_ok=True)
    (evid / "raw").mkdir(exist_ok=True)
    lock_fd = None
    try:
        lock_fd = acquire_lock()
        order = (args.order.upper() or "ABBA") * args.rounds
        meta = {
            "task": "T297",
            "plan_revision": 4,
            "row": "E22",
            "protocol": {
                "order": order,
                "rounds": args.rounds,
                "arms": args.arms,
                "warmup": args.warmup,
                "iters": args.iters,
                "overhead_n": args.overhead_n,
                "cpu": args.cpu,
                "lock": str(LOCK),
            },
            "started": utc(),
            "db": str(db),
            "db_sha": db_sha,
            "probe": {k: str(v) for k, v in PROBE.items()},
            "dotnet": subprocess.run(["dotnet", "--version"], capture_output=True,
                                     text=True).stdout.strip(),
            "nproc": os.cpu_count(),
        }
        (evid / "meta.json").write_text(json.dumps(meta, indent=1), encoding="utf-8")

        runs, log_lines = [], []
        for index, side in enumerate(order, start=1):
            meta_run, records = run_block(side, index, args, evid)
            runs.append({"side": side, "round": index, "records": records, **meta_run})
            line = "%s | block %02d %s exit=%s wall=%.1fs records=%d" % (
                utc(), index, side, meta_run["exit"], meta_run["wall_s"], len(records))
            log_lines.append(line)
            print(line)
            with open(evid / "host.log", "a", encoding="utf-8") as f:
                f.write(line + "\n")
            (evid / "runs.json").write_text(json.dumps(runs, indent=1), encoding="utf-8")
        summary = aggregate(runs)
        (evid / "summary.json").write_text(json.dumps(summary, indent=1), encoding="utf-8")

        failures = []
        for run in runs:
            if run["exit"] != 0:
                failures.append("round-%02d-%s exit=%s" % (run["round"], run["side"], run["exit"]))
        if failures:
            return fail("attribution run finished with failures: %s" % ", ".join(failures))
        print("attribute evidence written: %s" % evid)
        return 0
    except RuntimeError as exc:
        return fail(str(exc))
    finally:
        if lock_fd is not None:
            release_lock(lock_fd)


def cmd_stub(name):
    print("%s: not implemented yet in this phase" % name, file=sys.stderr)
    return 3


def build_parser():
    parser = argparse.ArgumentParser(
        prog="run_r4.py",
        description="T297 r=4 evidence runner (E20-E29). Implemented now: attribute.")
    sub = parser.add_subparsers(dest="command")

    att = sub.add_parser("attribute", help="run/validate the E22 per-phase attribution protocol")
    att.add_argument("--out", default=str(DEFAULT_EVID), help="evidence dir (default: %(default)s)")
    att.add_argument("--check-only", action="store_true", help="validate an existing evidence dir")
    att.add_argument("--rounds", type=int, default=3, help="ABBA block repetitions (default: %(default)s)")
    att.add_argument("--order", default="ABBA", help="block order (default: ABBA)")
    att.add_argument("--arms", default="all", choices=["all"] + ARMS, help="arm selector")
    att.add_argument("--warmup", type=int, default=3, help="harness warmup invocations")
    att.add_argument("--iters", type=int, default=20, help="harness measured invocations")
    att.add_argument("--overhead-n", type=int, default=100000, help="probe overhead pairs")
    att.add_argument("--cpu", type=int, default=1, help="taskset core")
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
