#!/usr/bin/env python3
"""Build the frozen issue-#319 population manifest from the committed 2026-11-07
BenchmarkDotNet reports (commit f855b884).

Every NextORM arm in the 15 committed report classes is a timing control except the
raw-reader arms (excluded by the owner). Eligible allocation targets are the
NextORM-vs-linq2db matched-scenario rows whose allocation ratio exceeds 1.20.
"""
import csv
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RESULTS = os.path.join(ROOT, "benchmarks", "BenchmarkDotNet.Artifacts", "results")

CLASSES = [
    "Aggregates", "Any", "Csv", "FeaturesFair", "FeaturesFairCached", "First",
    "Iteration", "LargeIteration", "Paging", "Projection", "SimulateWork",
    "Single", "Streaming", "Where", "WriteJson",
]

# Scenario aliases between the NextORM and the linq2db method names as published.
ALIASES = {
    "ToList": "ToListAsync",
    "Page": "PageAsync",
    "Async": "AsyncStream",
}

RAW_READER = {"ToDataReader", "ToDataReader_Empty"}


def parse_bytes(text):
    text = text.strip().replace("\u00a0", " ").replace(",", "")
    m = re.match(r"^([0-9.]+)\s*(B|KB|MB)$", text)
    if not m:
        return None
    value = float(m.group(1))
    return value * {"B": 1, "KB": 1024, "MB": 1024 * 1024}[m.group(2)]


def strip_library(method):
    m = re.match(r"^(A_|B_)?Nextorm_(Prepared|Cached)?_?(.*)$", method)
    if m:
        cache = {"Prepared": "prepared", "Cached": "cached", None: "regular"}[m.group(2)]
        return "nextorm", m.group(3), cache
    m = re.match(r"^(A_|B_)?Linq2Db_(Compiled)?_?(.*)$", method)
    if m:
        cache = "compiled" if m.group(2) else "regular"
        return "linq2db", m.group(3), cache
    return None, method, None


def scenario_key(name):
    return ALIASES.get(name, name)


def read_rows(path):
    with open(path, newline="", encoding="utf-8-sig") as fh:
        rows = list(csv.DictReader(fh))
    out = []
    for row in rows:
        allocated = parse_bytes(row.get("Allocated", ""))
        mean = row.get("Mean", "")
        out.append({"method": row["Method"], "mean": mean, "bytes": allocated})
    return out


def main():
    manifest = []
    for cls in CLASSES:
        path = os.path.join(RESULTS, f"NextORM.Benchmark.SqliteBenchmark{cls}-report.csv")
        rows = read_rows(path)
        parsed = []
        for row in rows:
            lib, name, cache = strip_library(row["method"])
            if lib is None:
                continue
            parsed.append({**row, "lib": lib, "name": name, "cache": cache})
        for ne in parsed:
            if ne["lib"] != "nextorm":
                continue
            key = scenario_key(ne["name"])
            linq = next(
                (r for r in parsed
                 if r["lib"] == "linq2db" and scenario_key(r["name"]) == key),
                None,
            )
            if linq is None or ne["bytes"] is None or linq["bytes"] is None:
                pair = "unpaired"
            else:
                pair = "paired"
            ratio = (ne["bytes"] / linq["bytes"]) if pair == "paired" else None
            if ne["name"] in RAW_READER or ne["name"].startswith("ToDataReader"):
                status = "excluded-raw-reader"
            elif ratio is None:
                status = "unpaired-control"
            elif ratio > 1.20:
                status = "target"
            else:
                status = "control"
            manifest.append({
                "class": f"SqliteBenchmark{cls}",
                "method": ne["method"],
                "scenario": ne["name"] or "(default)",
                "pair": pair,
                "linq_method": linq["method"] if linq else "",
                "cache_mode": ne["cache"],
                "mean": ne["mean"].replace(",", ""),
                "allocated_b": "" if ne["bytes"] is None else f"{ne['bytes']:.0f}",
                "linq_allocated_b": "" if (linq is None or linq["bytes"] is None) else f"{linq['bytes']:.0f}",
                "alloc_ratio": "" if ratio is None else f"{ratio:.4f}",
                "status": status,
                "source": f"NextORM.Benchmark.SqliteBenchmark{cls}-report.csv",
            })

    manifest.sort(key=lambda r: (-(float(r["alloc_ratio"]) if r["alloc_ratio"] else 0.0), r["class"], r["scenario"]))

    cols = ["class", "method", "scenario", "pair", "linq_method", "cache_mode", "mean",
            "allocated_b", "linq_allocated_b", "alloc_ratio", "status", "source"]
    out_path = sys.argv[1]
    with open(out_path, "w", newline="", encoding="utf-8") as fh:
        writer = csv.DictWriter(fh, fieldnames=cols)
        writer.writeheader()
        writer.writerows(manifest)

    targets = [r for r in manifest if r["status"] == "target"]
    print(f"rows={len(manifest)} targets={len(targets)} "
          f"controls={sum(1 for r in manifest if r['status'] == 'control')} "
          f"unpaired={sum(1 for r in manifest if r['status'] == 'unpaired-control')} "
          f"excluded={sum(1 for r in manifest if r['status'] == 'excluded-raw-reader')}")
    for r in targets:
        print(f"  {r['class']:36} {r['scenario']:22} {r['cache_mode']:9} "
              f"{r['allocated_b']:>7} vs {r['linq_allocated_b']:>7} = {r['alloc_ratio']}")


if __name__ == "__main__":
    main()
