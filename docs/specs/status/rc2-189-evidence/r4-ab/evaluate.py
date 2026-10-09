#!/usr/bin/env python3
"""D189 r=4 deterministic A/B evaluator (measurement artifact, no product change).

Reads $E/manifest.json, parses the BDN JSON referenced by each selected run,
computes the pre-declared paired statistics and the fixed decision rule.

Exit codes:
  0 = no real regression (all 7 cases U <= 1.05)
  1 = proven regression   (some case L > 1.05)
  2 = missing / invalid / zero-match evidence
  3 = indeterminate       (no L > 1.05, but some interval crosses 1.05)

`Global total time` is informational only and never affects the exit code.
"""
import csv
import json
import math
import os
import statistics
import sys
from typing import NoReturn

E = os.path.dirname(os.path.abspath(__file__))
MANIFEST = os.path.join(E, "manifest.json")
Z = 4.786
THRESHOLD = 1.05
N_PAIRS = 8
CASES = [
    "NextORM.Benchmark.InMemoryBenchmarkAggregates.Nextorm_Count",
    "NextORM.Benchmark.InMemoryBenchmarkGroupBy.Nextorm_GroupByCount",
    "NextORM.Benchmark.SqliteBenchmarkAny.Nextorm_Cached",
    "NextORM.Benchmark.SqliteBenchmarkCachedPlan.Prepared_ToList",
    "NextORM.Benchmark.SqliteBenchmarkCachedPlan.Cached_ToList",
    "NextORM.Benchmark.SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param",
    "NextORM.Benchmark.SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync",
]


def fail(msg) -> NoReturn:
    print("INVALID:", msg, file=sys.stderr)
    with open(os.path.join(E, "verdict.json"), "w") as fh:
        json.dump({"exit_code": 2, "error": msg}, fh, indent=2)
    sys.exit(2)


def load_case_stats(run):
    """Return (per_case_stats, total_benchmarks, error). Reads BDN JSON files."""
    stats = {}
    total = 0
    for art in run.get("artifacts", []):
        path = art.get("copied_path")
        if not path or not os.path.exists(path):
            return None, 0, f"missing artifact {path}"
        with open(path) as fh:
            data = json.load(fh)
        for b in data.get("Benchmarks", []):
            total += 1
            name = b.get("FullName")
            st = b.get("Statistics", {})
            mem = b.get("Memory", {}) or {}
            if name in stats:
                return None, total, f"duplicate case {name}"
            stats[name] = {
                "mean": st.get("Mean"),
                "stddev": st.get("StandardDeviation"),
                "n": st.get("N"),
                "alloc": mem.get("BytesAllocatedPerOperation"),
            }
    return stats, total, None


def main():
    if not os.path.exists(MANIFEST):
        fail("manifest.json not found")
    with open(MANIFEST) as fh:
        m = json.load(fh)

    runs = m.get("runs", [])
    pairs = m.get("pairs", {})
    keys = sorted(pairs.keys())
    if keys != [f"{i:02d}" for i in range(1, N_PAIRS + 1)]:
        fail(f"expected pairs 01..08, got {keys}")

    selected = {}  # pair -> (A_case_stats, B_case_stats, A_run, B_run)
    for key in keys:
        p = pairs[key]
        sel = p.get("selected_attempt")
        attempt = None
        for a in p.get("attempts", []):
            if a.get("valid") and (sel is None or a.get("attempt") == sel):
                attempt = a.get("attempt")
                break
        if attempt is None:
            fail(f"pair {key}: no valid selected attempt")
        ra = next((r for r in runs if f"{r['pair']:02d}" == key
                   and r["attempt"] == attempt and r["side"] == "A"), None)
        rb = next((r for r in runs if f"{r['pair']:02d}" == key
                   and r["attempt"] == attempt and r["side"] == "B"), None)
        if ra is None or rb is None:
            fail(f"pair {key}: missing run record")
        sa, ta, ea = load_case_stats(ra)
        sb, tb, eb = load_case_stats(rb)
        if ea or eb:
            fail(f"pair {key}: {ea or eb}")
        assert sa is not None and sb is not None
        if ta != len(CASES) or tb != len(CASES):
            fail(f"pair {key}: benchmark count A={ta} B={tb} (expected {len(CASES)})")
        for side, st, tot in (("A", sa, ta), ("B", sb, tb)):
            if set(st) != set(CASES):
                miss = set(CASES) - set(st)
                extra = set(st) - set(CASES)
                fail(f"pair {key} side {side}: missing={sorted(miss)} extra={sorted(extra)}")
            for c in CASES:
                v = st[c]
                if v["mean"] is None or v["mean"] <= 0:
                    fail(f"pair {key} {side} {c}: bad mean")
                if v["stddev"] is None or v["stddev"] <= 0:
                    fail(f"pair {key} {side} {c}: bad stddev")
                if not v["n"] or v["n"] <= 0:
                    fail(f"pair {key} {side} {c}: bad N")
        selected[key] = (sa, sb, ra, rb)

    # per-case paired statistics
    rows = []
    any_L = False
    any_U_gt = False
    all_U_le = True
    for c in CASES:
        d_list = []
        v_list = []
        means_a, means_b, allocs_a, allocs_b, ns_a, ns_b = [], [], [], [], [], []
        for key in keys:
            sa, sb, _, _ = selected[key]
            a, b = sa[c], sb[c]
            d = math.log(b["mean"] / a["mean"])
            d_list.append(d)
            v = (a["stddev"] / a["mean"]) ** 2 / a["n"] + \
                (b["stddev"] / b["mean"]) ** 2 / b["n"]
            v_list.append(v)
            means_a.append(a["mean"])
            means_b.append(b["mean"])
            ns_a.append(a["n"])
            ns_b.append(b["n"])
            if a["alloc"] is not None:
                allocs_a.append(a["alloc"])
            if b["alloc"] is not None:
                allocs_b.append(b["alloc"])
        dbar = statistics.mean(d_list)
        s_d = statistics.stdev(d_list)  # sample (ddof=1) over 8 pairs
        se = max(s_d / math.sqrt(N_PAIRS), math.sqrt(sum(v_list)) / N_PAIRS)
        R = math.exp(dbar)
        L = math.exp(dbar - Z * se)
        U = math.exp(dbar + Z * se)
        if L > THRESHOLD:
            any_L = True
        if U > THRESHOLD:
            any_U_gt = True
            all_U_le = False
        med_a = statistics.median(allocs_a) if allocs_a else None
        med_b = statistics.median(allocs_b) if allocs_b else None
        growth = None
        if med_a is not None and med_b is not None:
            growth = med_b - med_a
            growth_limit = max(1024.0, 0.01 * med_a)
            growth_flag = growth > growth_limit
        else:
            growth_limit = None
            growth_flag = None
        rows.append({
            "case": c,
            "dbar": dbar,
            "s_d": s_d,
            "SE": se,
            "R": R,
            "L": L,
            "U": U,
            "mean_A_ns": statistics.mean(means_a),
            "mean_B_ns": statistics.mean(means_b),
            "N_A": sorted(set(ns_a)),
            "N_B": sorted(set(ns_b)),
            "alloc_median_A": med_a,
            "alloc_median_B": med_b,
            "alloc_growth": growth,
            "alloc_growth_limit": growth_limit,
            "alloc_growth_flag": growth_flag,
        })

    # global totals (informational)
    def side_total(side):
        vals = []
        for key in keys:
            sa, sb, ra, rb = selected[key]
            r = ra if side == "A" else rb
            g = (r.get("console_meta") or {}).get("global_total_sec")
            if g is not None:
                vals.append(g)
        return sum(vals)

    global_a = side_total("A")
    global_b = side_total("B")

    if any_L:
        exit_code = 1
    elif all_U_le:
        exit_code = 0
    else:
        exit_code = 3

    # per-case.csv
    with open(os.path.join(E, "per-case.csv"), "w", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["case", "mean_A_ns", "mean_B_ns", "dbar", "s_d", "SE",
                    "R", "L", "U", "U_gt_1.05", "L_gt_1.05",
                    "alloc_median_A_bytes", "alloc_median_B_bytes",
                    "alloc_growth_bytes", "alloc_growth_limit_bytes",
                    "alloc_growth_flag", "N_A", "N_B"])
        for r in rows:
            w.writerow([
                r["case"], f"{r['mean_A_ns']:.3f}", f"{r['mean_B_ns']:.3f}",
                f"{r['dbar']:.6f}", f"{r['s_d']:.6f}", f"{r['SE']:.6f}",
                f"{r['R']:.6f}", f"{r['L']:.6f}", f"{r['U']:.6f}",
                r["U"] > THRESHOLD, r["L"] > THRESHOLD,
                r["alloc_median_A"], r["alloc_median_B"],
                r["alloc_growth"], f"{r['alloc_growth_limit']:.1f}" if r["alloc_growth_limit"] else "",
                r["alloc_growth_flag"], "|".join(map(str, r["N_A"])),
                "|".join(map(str, r["N_B"])),
            ])

    # summary.md
    verdict_name = {0: "NO REGRESSION", 1: "PROVEN REGRESSION",
                    3: "INDETERMINATE", 2: "INVALID EVIDENCE"}[exit_code]
    with open(os.path.join(E, "summary.md"), "w") as fh:
        fh.write("# D189 r=4 — paired A/B acceptance measurement summary\n\n")
        fh.write(f"- Generated: {m.get('finished_utc', '?')}\n")
        fh.write(f"- Pairs: {N_PAIRS} (A=`{m['commits']['A']}`, B=`{m['commits']['B']}`)\n")
        fh.write(f"- Decision rule: no regression iff all 7 cases U<=1.05; "
                 f"proven regression iff any L>1.05; else indeterminate.\n")
        fh.write(f"- Replacement pairs used: {m.get('replacement_pairs_used', '?')}\n\n")
        fh.write(f"## Verdict: **{verdict_name}** (exit {exit_code})\n\n")
        fh.write("| # | case | R | L | U | U<=1.05 | L>1.05 | alloc A (B) | alloc B (B) | growth flag |\n")
        fh.write("|---|------|---|---|---|---------|--------|-------------|-------------|-------------|\n")
        for i, r in enumerate(rows, 1):
            fh.write(f"| {i} | `{r['case'].split('NextORM.Benchmark.',1)[1]}` | "
                     f"{r['R']:.4f} | {r['L']:.4f} | {r['U']:.4f} | "
                     f"{'yes' if r['U'] <= THRESHOLD else 'NO'} | "
                     f"{'YES' if r['L'] > THRESHOLD else 'no'} | "
                     f"{r['alloc_median_A']} | {r['alloc_median_B']} | "
                     f"{r['alloc_growth_flag']} |\n")
        fh.write(f"\n## Global total time (informational only — never affects exit code)\n\n")
        fh.write(f"- Sum over 8 A runs: {global_a:.2f} s\n")
        fh.write(f"- Sum over 8 B runs: {global_b:.2f} s\n")
        fh.write(f"\n## Notes\n\n")
        fh.write("- `N` used in `v_i` is BDN `Statistics.N` (post upper-outlier removal; "
                 "the run configured `--iterationCount 15`).\n")
        fh.write("- Allocated metric is BDN `BytesAllocatedPerOperation`; the median is taken "
                 "across the 8 runs per side. Growth flag: `B-A > max(1 KiB, 1% of median A)`.\n")

    with open(os.path.join(E, "verdict.json"), "w") as fh:
        json.dump({
            "exit_code": exit_code,
            "verdict": verdict_name,
            "threshold": THRESHOLD,
            "z": Z,
            "pairs": N_PAIRS,
            "replacement_pairs_used": m.get("replacement_pairs_used"),
            "any_U_gt_1.05": any_U_gt,
            "any_L_gt_1.05": any_L,
            "cases": [{
                "case": r["case"], "R": r["R"], "L": r["L"], "U": r["U"],
                "dbar": r["dbar"], "s_d": r["s_d"], "SE": r["SE"],
                "alloc_growth_flag": r["alloc_growth_flag"],
            } for r in rows],
            "global_total_sec": {"A": global_a, "B": global_b, "informational": True},
        }, fh, indent=2)

    print(f"evaluator exit_code={exit_code} ({verdict_name})")
    for r in rows:
        print(f"  {r['case'].split('NextORM.Benchmark.',1)[1]:55s} "
              f"R={r['R']:.4f} L={r['L']:.4f} U={r['U']:.4f}")
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
