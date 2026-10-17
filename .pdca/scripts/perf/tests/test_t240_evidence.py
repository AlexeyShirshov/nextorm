#!/usr/bin/env python3
"""Self-tests for scripts/perf/t240_evidence.py (pure reader; no benchmark run).

Run with either::

    python3 -m unittest discover -s scripts/perf/tests -p 'test_t240_evidence.py' -v
    python3 -m pytest scripts/perf/tests/test_t240_evidence.py -q

Synthetic BDN JSON reports mirror the exporter schema
(``Benchmarks[].{Namespace,Type,Method,DisplayInfo,Memory.BytesAllocatedPerOperation,
Statistics.Mean}``) so the tests exercise the real pairing/ratio/verdict path without
executing BenchmarkDotNet.
"""
from __future__ import annotations

import contextlib
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

PERF_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PERF_DIR))

import t240_evidence as t240  # noqa: E402

TYPE = "SqliteBenchmarkFeaturesFairCached"
NAMESPACE = "NextORM.Benchmark"


def write_report(path, entries):
    """*entries* is a list of ``(method, allocated, mean)`` triples."""
    benchmarks = [
        {
            "Namespace": NAMESPACE,
            "Type": TYPE,
            "Method": method,
            "DisplayInfo": f"{TYPE}.{method}: DefaultJob",
            "Statistics": {"Mean": mean},
            "Memory": {"BytesAllocatedPerOperation": allocated},
        }
        for method, allocated, mean in entries
    ]
    Path(path).write_text(json.dumps({"Title": "T240 fixture", "Benchmarks": benchmarks}),
                          encoding="utf-8")
    return Path(path)


def base_entries(*, nextorm_alloc=120, linq_alloc=100, nextorm_mean=105, linq_mean=100):
    """One full five-scenario round at the requested (uniform) arm values."""
    entries = []
    for scenario in t240.SCENARIOS:
        entries.append((f"B_Nextorm_Cached_{scenario}", nextorm_alloc, nextorm_mean))
        entries.append((f"B_Linq2Db_{scenario}", linq_alloc, linq_mean))
    return entries


def run_main(argv):
    out, err = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        code = t240.main(argv)
    return code, out.getvalue(), err.getvalue()


class T240EvidenceTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory(prefix="t240-evidence-test-")
        self.tmp = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def report(self, name, entries):
        return write_report(self.tmp / name, entries)

    # (i) exactly at the 1.20 / 1.05 boundary -> MET
    def test_boundary_at_tolerance_is_met(self):
        path = self.report("round-0.json", base_entries())
        output = self.tmp / "ratios.json"
        code, stdout, _ = run_main(
            ["--round", str(path), "--output", str(output), "--alloc-tol", "1.20", "--time-tol", "1.05"]
        )
        self.assertEqual(code, 0, stdout)
        self.assertIn(str(path), stdout)  # exact consumed BDN JSON path
        payload = json.loads(output.read_text(encoding="utf-8"))
        self.assertEqual(payload["overall"], "MET")
        for scenario in t240.SCENARIOS:
            entry = payload["scenarios"][scenario]
            self.assertEqual(entry["decision"], "MET")
            self.assertEqual(entry["rounds"][0]["alloc_ratio"], 1.2)
            self.assertEqual(entry["rounds"][0]["alloc_verdict"], "MET")
            self.assertEqual(entry["rounds"][0]["time_ratio"], 1.05)
            self.assertEqual(entry["rounds"][0]["time_verdict"], "MET")

    def test_just_over_tolerance_fails(self):
        path = self.report("round-0.json", base_entries(nextorm_alloc=121, nextorm_mean=106))
        output = self.tmp / "ratios.json"
        code, stdout, _ = run_main(["--round", str(path), "--output", str(output)])
        self.assertNotEqual(code, 0, stdout)
        payload = json.loads(output.read_text(encoding="utf-8"))
        self.assertEqual(payload["overall"], "FAIL")
        self.assertEqual(payload["scenarios"]["Cte"]["rounds"][0]["alloc_verdict"], "FAIL")

    # (ii) duplicate arm -> rejection (nonzero)
    def test_duplicate_arm_rejected(self):
        entries = base_entries() + [("B_Nextorm_Cached_Cte", 120, 105)]
        path = self.report("round-0.json", entries)
        code, _, stderr = run_main(["--round", str(path)])
        self.assertEqual(code, t240.EXIT_REJECT)
        self.assertIn("duplicate arm", stderr)

    # (iii) missing arm -> rejection (nonzero)
    def test_missing_arm_rejected(self):
        entries = [e for e in base_entries() if e[0] != "B_Linq2Db_Distinct"]
        path = self.report("round-0.json", entries)
        code, _, stderr = run_main(["--round", str(path)])
        self.assertEqual(code, t240.EXIT_REJECT)
        self.assertIn("missing LINQ to DB arm", stderr)

    def test_scenario_not_found_rejected(self):
        path = self.report("round-0.json", [("B_Nextorm_Cached_Cte", 120, 105)])
        code, _, stderr = run_main(["--round", str(path)])
        self.assertEqual(code, t240.EXIT_REJECT)
        self.assertIn("missing", stderr)

    def test_null_allocation_rejected(self):
        entries = base_entries()
        entries[0] = ("B_Nextorm_Cached_Cte", None, 105)
        path = self.report("round-0.json", entries)
        code, _, stderr = run_main(["--round", str(path)])
        self.assertEqual(code, t240.EXIT_REJECT)
        self.assertIn("null/NA", stderr)

    def test_zero_denominator_rejected(self):
        path = self.report("round-0.json", base_entries(linq_alloc=0))
        code, _, stderr = run_main(["--round", str(path)])
        self.assertEqual(code, t240.EXIT_REJECT)
        self.assertIn("zero", stderr)

    # (iv) one round FAIL / one MET -> overall FAIL
    def test_one_round_fail_one_met_overall_fail(self):
        round0 = self.report("round-0.json", base_entries(nextorm_alloc=130, nextorm_mean=100))
        round1 = self.report("round-1.json", base_entries(nextorm_alloc=110, nextorm_mean=100))
        output = self.tmp / "ratios.json"
        code, stdout, _ = run_main(
            ["--round", str(round0), "--round", str(round1), "--output", str(output)]
        )
        self.assertNotEqual(code, 0, stdout)
        payload = json.loads(output.read_text(encoding="utf-8"))
        self.assertEqual(payload["overall"], "FAIL")
        self.assertEqual(payload["round_count"], 2)
        cte_rounds = payload["scenarios"]["Cte"]["rounds"]
        self.assertEqual([r["alloc_verdict"] for r in cte_rounds], ["FAIL", "MET"])
        self.assertEqual(payload["scenarios"]["Cte"]["decision"], "FAIL")

    def test_two_rounds_all_met_overall_met(self):
        round0 = self.report("round-0.json", base_entries())
        round1 = self.report("round-1.json", base_entries())
        code, _, _ = run_main(["--round", str(round0), "--round", str(round1)])
        self.assertEqual(code, 0)

    def test_directory_round_discovers_reports(self):
        results = self.tmp / "round-0" / "results"
        results.mkdir(parents=True)
        write_report(results / f"{NAMESPACE}.{TYPE}-report-full-compressed.json", base_entries())
        code, stdout, _ = run_main(["--round", str(self.tmp / "round-0")])
        self.assertEqual(code, 0, stdout)
        self.assertIn("report-full-compressed.json", stdout)

    def test_manifest_recorded(self):
        path = self.report("round-0.json", base_entries())
        manifest = self.tmp / "manifest.json"
        manifest.write_text(json.dumps({"task": "D240", "contract_rv": 1}), encoding="utf-8")
        output = self.tmp / "ratios.json"
        code, _, _ = run_main(["--round", str(path), "--manifest", str(manifest), "--output", str(output)])
        self.assertEqual(code, 0)
        payload = json.loads(output.read_text(encoding="utf-8"))
        self.assertEqual(payload["manifest_task"], "D240")
        self.assertEqual(payload["manifest_contract_rv"], 1)

    def test_no_round_is_a_rejection(self):
        code, _, stderr = run_main([])
        self.assertEqual(code, t240.EXIT_REJECT)
        self.assertIn("--round", stderr)


if __name__ == "__main__":
    unittest.main()
