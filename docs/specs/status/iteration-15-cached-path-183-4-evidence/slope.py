#!/usr/bin/env python3
"""Regress Mean(ns) on N per variant/round and compare new/old slopes.

Input : /tmp/opencode/do-c/slope/r<R>-{old,new}/*report-full-compressed.json
Output: slope-summary.json, slope-summary.txt in the same root.

For out-of-process rounds (r=1..5) the ShortRun job is selected; DefaultJob is
ignored. r=6 is the in-process pair.
"""
import glob
import json
import math
import os
import random
import statistics

ROOT = "/tmp/opencode/do-c/slope"
ROUNDS_OOP = [1, 2, 3, 4, 5]
ROUND_INPROC = 6


def load_shortrun(path):
    with open(path) as f:
        data = json.load(f)
    points = []
    job_names = set()
    for b in data["Benchmarks"]:
        di = b.get("DisplayInfo", "")
        job_names.add(di.split(": ", 1)[1].split(" [")[0] if ": " in di else di)
        if "ShortRun" not in di:
            continue
        n = int(b["Parameters"].split("=")[1])
        mean = b["Statistics"]["Mean"]
        points.append((n, float(mean)))
    points.sort()
    return points, sorted(job_names)


def linreg(points):
    xs = [p[0] for p in points]
    ys = [p[1] for p in points]
    n = len(xs)
    mx = sum(xs) / n
    my = sum(ys) / n
    sxx = sum((x - mx) ** 2 for x in xs)
    sxy = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    slope = sxy / sxx
    intercept = my - slope * mx
    ss_tot = sum((y - my) ** 2 for y in ys)
    ss_res = sum((y - (slope * x + intercept)) ** 2 for x, y in zip(xs, ys))
    r2 = 1.0 - ss_res / ss_tot if ss_tot > 0 else float("nan")
    return slope, intercept, r2


def slope_for(round_no, variant):
    pat = os.path.join(ROOT, f"r{round_no}-{variant}", "*report-full-compressed.json")
    files = glob.glob(pat)
    if not files:
        return None
    points, job_names = load_shortrun(files[0])
    if len(points) < 2:
        return None
    slope, intercept, r2 = linreg(points)
    return {
        "round": round_no,
        "variant": variant,
        "points": points,
        "slope_ns_per_hit": slope,
        "intercept_ns": intercept,
        "r2": r2,
        "jobs_present": job_names,
        "file": files[0],
    }


def bootstrap_ci(vals, iters=20000, seed=183):
    rnd = random.Random(seed)
    meds = []
    n = len(vals)
    for _ in range(iters):
        sample = [vals[rnd.randrange(n)] for _ in range(n)]
        meds.append(statistics.median(sample))
    meds.sort()
    lo = meds[int(0.025 * iters)]
    hi = meds[int(0.975 * iters) - 1]
    return lo, hi


def main():
    results = {}
    for r in ROUNDS_OOP + [ROUND_INPROC]:
        for v in ("old", "new"):
            s = slope_for(r, v)
            if s is not None:
                results[f"r{r}-{v}"] = s

    oop = []
    ratios = []
    for r in ROUNDS_OOP:
        o = results.get(f"r{r}-old")
        nw = results.get(f"r{r}-new")
        if not o or not nw:
            oop.append({"round": r, "incomplete": True})
            continue
        ratio = nw["slope_ns_per_hit"] / o["slope_ns_per_hit"]
        ratios.append(ratio)
        oop.append({
            "round": r,
            "old_slope_ns_per_hit": o["slope_ns_per_hit"],
            "new_slope_ns_per_hit": nw["slope_ns_per_hit"],
            "old_intercept_ns": o["intercept_ns"],
            "new_intercept_ns": nw["intercept_ns"],
            "old_r2": o["r2"],
            "new_r2": nw["r2"],
            "ratio_new_over_old": ratio,
        })

    summary = {
        "rounds": oop,
        "completed_rounds": len(ratios),
        "median_ratio": statistics.median(ratios) if ratios else None,
        "mean_ratio": statistics.fmean(ratios) if ratios else None,
        "geomean_ratio": (math.exp(sum(math.log(x) for x in ratios) / len(ratios)) if ratios else None),
        "min_ratio": min(ratios) if ratios else None,
        "max_ratio": max(ratios) if ratios else None,
        "sign_new_faster": sum(1 for x in ratios if x < 1.0),
        "sign_new_slower": sum(1 for x in ratios if x > 1.0),
        "bootstrap_95ci_median_ratio": (bootstrap_ci(ratios) if len(ratios) >= 2 else None),
        "note": "n=5 -> nonparametric bracket is min/max; bootstrap percentile 95% CI also given",
    }

    # in-process cross-check
    o6 = results.get("r6-old")
    n6 = results.get("r6-new")
    if o6 and n6:
        summary["in_process_r6"] = {
            "old_slope_ns_per_hit": o6["slope_ns_per_hit"],
            "new_slope_ns_per_hit": n6["slope_ns_per_hit"],
            "old_intercept_ns": o6["intercept_ns"],
            "new_intercept_ns": n6["intercept_ns"],
            "old_r2": o6["r2"],
            "new_r2": n6["r2"],
            "ratio_new_over_old": n6["slope_ns_per_hit"] / o6["slope_ns_per_hit"],
        }

    with open(os.path.join(ROOT, "slope-summary.json"), "w") as f:
        json.dump(summary, f, indent=2)

    lines = []
    lines.append("round  variant  slope(ns/hit)  intercept(ns)  R2        ratio(new/old)")
    lines.append("-" * 78)
    for entry in oop:
        if entry.get("incomplete"):
            lines.append(f"r{entry['round']:<5}  INCOMPLETE")
            continue
        r = entry["round"]
        lines.append(f"r{r}-old   old      {entry['old_slope_ns_per_hit']:12.6f}  {entry['old_intercept_ns']:12.3f}  {entry['old_r2']:.6f}")
        lines.append(f"r{r}-new   new      {entry['new_slope_ns_per_hit']:12.6f}  {entry['new_intercept_ns']:12.3f}  {entry['new_r2']:.6f}   {entry['ratio_new_over_old']:.6f}")
    lines.append("")
    lines.append(f"completed out-of-process rounds : {summary['completed_rounds']}")
    lines.append(f"per-round ratio new/old          : {[round(x, 6) for x in ratios]}")
    lines.append(f"median ratio new/old             : {summary['median_ratio']:.6f}")
    lines.append(f"mean ratio / geomean             : {summary['mean_ratio']:.6f} / {summary['geomean_ratio']:.6f}")
    lines.append(f"min / max ratio (n=5 bracket)    : {summary['min_ratio']:.6f} / {summary['max_ratio']:.6f}")
    lines.append(f"bootstrap 95% CI of median ratio : {summary['bootstrap_95ci_median_ratio']}")
    lines.append(f"sign stable: new faster rounds   : {summary['sign_new_faster']} / {len(ratios)}")
    lines.append(f"sign stable: new slower rounds   : {summary['sign_new_slower']} / {len(ratios)}")
    if "in_process_r6" in summary:
        ip = summary["in_process_r6"]
        lines.append("")
        lines.append("in-process cross-check (r=6):")
        lines.append(f"  old slope {ip['old_slope_ns_per_hit']:.6f} ns/hit (R2 {ip['old_r2']:.6f}, intercept {ip['old_intercept_ns']:.3f} ns)")
        lines.append(f"  new slope {ip['new_slope_ns_per_hit']:.6f} ns/hit (R2 {ip['new_r2']:.6f}, intercept {ip['new_intercept_ns']:.3f} ns)")
        lines.append(f"  ratio new/old {ip['ratio_new_over_old']:.6f} (sign {'new faster' if ip['ratio_new_over_old'] < 1 else 'new slower'})")
    text = "\n".join(lines) + "\n"
    with open(os.path.join(ROOT, "slope-summary.txt"), "w") as f:
        f.write(text)
    print(text)


if __name__ == "__main__":
    main()
