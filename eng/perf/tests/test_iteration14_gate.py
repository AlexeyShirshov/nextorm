#!/usr/bin/env python3
"""Self-tests for the Iteration-14 allocation gate (pure parser; no benchmark run).

Run with::

    python3 -m unittest discover -s eng/perf/tests -p 'test_iteration14_gate.py'

Synthetic BDN JSON reports mirror the exporter's schema
(``Benchmarks[].{Namespace,Type,Method,DisplayInfo,Memory.BytesAllocatedPerOperation,
OperationsPerInvoke?}``) so the tests exercise the real parsing and evaluation path
without executing BenchmarkDotNet.
"""

from __future__ import annotations

import contextlib
import io
import json
import re
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

GATE_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(GATE_DIR))

import iteration14_gate as gate  # noqa: E402


MANIFEST = gate.parse_manifest(str(gate.MANIFEST_PATH))
OPS = MANIFEST["logical_ops_per_invocation"]


def _display(short_type, method, toolchain):
    if toolchain == "Default":
        return (
            f"{short_type}.{method}: ShortRun("
            "IterationCount=3, LaunchCount=1, WarmupCount=3, RunStrategy=Throughput)"
        )
    return (
        f"{short_type}.{method}: ShortRun(Toolchain={toolchain}, "
        "IterationCount=3, LaunchCount=1, WarmupCount=3, RunStrategy=Throughput)"
    )


def _rows_by_type():
    by_type = {}
    for row in MANIFEST["rows"]:
        by_type.setdefault(row["type"], []).append(row)
    return by_type


def build_reports(tmpdir, bytes_per_op=None):
    """Write one synthetic BDN JSON report per manifest type.

    ``bytes_per_op`` maps ``(method, toolchain) -> B/op``; the default is the row's
    pinned budget, so a report built without overrides sits exactly at every budget.
    """
    bytes_per_op = bytes_per_op or {}
    written = []
    for full_type, rows in _rows_by_type().items():
        namespace, short_type = full_type.rsplit(".", 1)
        benchmarks = []
        for row in rows:
            for toolchain in MANIFEST["required_toolchains"]:
                per_op = bytes_per_op.get(
                    (row["method"], toolchain), row["max_bytes_per_op"]
                )
                benchmarks.append(
                    {
                        "Namespace": namespace,
                        "Type": short_type,
                        "Method": row["method"],
                        "DisplayInfo": _display(short_type, row["method"], toolchain),
                        "Memory": {"BytesAllocatedPerOperation": per_op * OPS},
                    }
                )
        path = Path(tmpdir) / f"{full_type}-report-full-compressed.json"
        path.write_text(json.dumps({"Benchmarks": benchmarks}), encoding="utf-8")
        written.append(path)
    return written


def load_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def save_json(path, data):
    Path(path).write_text(json.dumps(data), encoding="utf-8")


def find_entry(data, method, toolchain):
    for entry in data["Benchmarks"]:
        display = entry.get("DisplayInfo", "")
        if entry["Method"] != method:
            continue
        if toolchain == "Default" and "Toolchain=" not in display:
            return entry
        if toolchain != "Default" and f"Toolchain={toolchain}" in display:
            return entry
    raise AssertionError(f"no synthetic entry for {method}/{toolchain}")


def run_gate(reports_dir, runner=None, src_dir=None):
    argv = ["--no-run", "--reports-dir", str(reports_dir)]
    if src_dir is not None:
        argv += ["--bench-src-dir", str(src_dir)]
    with contextlib.redirect_stdout(io.StringIO()) as buffer:
        code = gate.gate_main(argv, runner=runner)
    return code, buffer.getvalue()


def contract_src(tmp, type_name, iterations):
    """Copy the real benchmark sources and rewrite one class constant.

    The normalization contract is read from source, so a temp source tree is the
    only way to deliberately violate ``Iterations == logical_ops_per_invocation``.
    """
    src = tmp / "src"
    src.mkdir(exist_ok=True)
    for path in Path(gate.BENCH_SRC_DIR).glob("*.cs"):
        shutil.copyfile(path, src / path.name)
    target = src / f"{type_name}.cs"
    text = target.read_text(encoding="utf-8")
    text, count = re.subn(
        r"(const\s+int\s+Iterations\s*=\s*)\d+", r"\g<1>" + str(iterations), text, count=1
    )
    assert count == 1, f"could not rewrite Iterations in {target}"
    target.write_text(text, encoding="utf-8")
    return src


class Iteration14GateTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory(prefix="iteration14-gate-test-")
        self.tmp = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def test_complete_report_at_budgets_passes(self):
        build_reports(self.tmp)
        code, output = run_gate(self.tmp)
        self.assertEqual(code, 0, output)
        self.assertIn("within budget", output)

    def test_default_reports_dir_is_the_pinned_benchmark_artifacts_dir(self):
        # Regression: the benchmark pins its artifacts to benchmarks/BenchmarkDotNet.Artifacts
        # (BenchmarkArtifacts.Path, used as NextormConfig.ArtifactsPath). The gate must read the
        # reports from that same directory, never the legacy repo-root BenchmarkDotNet.Artifacts.
        self.assertEqual(
            gate.DEFAULT_REPORTS_DIR,
            gate.REPO_ROOT / "benchmarks" / "BenchmarkDotNet.Artifacts" / "results",
        )
        self.assertNotEqual(
            gate.DEFAULT_REPORTS_DIR,
            gate.REPO_ROOT / "BenchmarkDotNet.Artifacts" / "results",
        )

    def test_deliberate_over_budget_returns_nonzero(self):
        build_reports(self.tmp, {("Cte_Prepare_NoHash", "Default"): 15153})
        code, output = run_gate(self.tmp)
        self.assertEqual(code, 1)
        self.assertIn("Cte_Prepare_NoHash/Default", output)
        self.assertIn("15153.00 B/op > 15152", output)

    def test_zero_budget_rejects_positive_allocation(self):
        build_reports(self.tmp, {("Cte_Warm_Reused", "Default"): 1})
        code, output = run_gate(self.tmp)
        self.assertEqual(code, 1)
        self.assertIn("Cte_Warm_Reused/Default", output)

    def test_prepared_tolist_rejects_growth(self):
        build_reports(self.tmp, {("Prepared_ToList", "InProcessEmitToolchain"): 781})
        code, output = run_gate(self.tmp)
        self.assertEqual(code, 1)
        self.assertIn("Prepared_ToList/InProcessEmitToolchain", output)

    def test_missing_row_or_job_fails(self):
        paths = build_reports(self.tmp)
        by_type = {p.name.split("-report")[0]: p for p in paths}

        # Missing row: drop every entry of one pinned benchmark.
        path = by_type["NextORM.Benchmark.SqliteBenchmarkWarmDecompose"]
        data = load_json(path)
        data["Benchmarks"] = [b for b in data["Benchmarks"] if b["Method"] != "Cte_Construct"]
        save_json(path, data)

        # Missing job: drop only the Default-toolchain entry of another benchmark.
        data = load_json(path)
        data["Benchmarks"] = [
            b
            for b in data["Benchmarks"]
            if not (b["Method"] == "Join4_Construct" and "Toolchain=" not in b["DisplayInfo"])
        ]
        save_json(path, data)

        code, output = run_gate(self.tmp)
        self.assertEqual(code, 1)
        self.assertIn("missing row Cte_Construct", output)
        self.assertIn("missing job Join4_Construct/Default", output)

    def test_duplicate_or_invalid_result_fails(self):
        paths = build_reports(self.tmp)
        path = next(p for p in paths if "SqliteBenchmarkCachedPlan" in p.name)

        data = load_json(path)
        duplicate = find_entry(data, "Prepared_ToList", "Default")
        data["Benchmarks"].append(dict(duplicate))
        save_json(path, data)
        code, output = run_gate(self.tmp)
        self.assertEqual(code, 1)
        self.assertIn("duplicate result for", output)

        # Invalid (negative) allocation: rebuild clean and corrupt one value.
        build_reports(self.tmp)
        data = load_json(path)
        find_entry(data, "Prepared_ToList", "Default")["Memory"][
            "BytesAllocatedPerOperation"
        ] = -1
        save_json(path, data)
        code, output = run_gate(self.tmp)
        self.assertEqual(code, 1)
        self.assertIn("invalid allocation Prepared_ToList/Default", output)

    def test_operation_count_mismatch_fails(self):
        # (a) A report that explicitly carries a non-1 OperationsPerInvoke is rejected.
        build_reports(self.tmp)
        path = next(
            p
            for p in self.tmp.glob("*SqliteBenchmarkFeaturePlanBuild*.json")
        )
        data = load_json(path)
        find_entry(data, "Build_Baseline_SimpleSelect", "Default")[
            "OperationsPerInvoke"
        ] = OPS
        save_json(path, data)
        code, output = run_gate(self.tmp)
        self.assertEqual(code, 1)
        self.assertIn("normalization mismatch Build_Baseline_SimpleSelect/Default", output)

        # (b) The source contract is authoritative: a class constant that does not
        # match logical_ops_per_invocation fails even though real BDN JSON never
        # carries OperationsPerInvoke (this drives the actual fail-closed path).
        build_reports(self.tmp)
        src = contract_src(self.tmp, "SqliteBenchmarkFeaturePlanBuild", 50)
        code, output = run_gate(self.tmp, src_dir=src)
        self.assertEqual(code, 1)
        self.assertIn("normalization contract not verified", output)
        self.assertIn("Iterations=50, expected logical_ops_per_invocation=100", output)

    def test_unknown_or_absent_operation_count_fails(self):
        # Real BDN JSON has no OperationsPerInvoke; if the checked-in source
        # contract cannot be verified the gate must fail, never assume 100.
        build_reports(self.tmp)
        empty_src = self.tmp / "no-sources"
        empty_src.mkdir()
        code, output = run_gate(self.tmp, src_dir=empty_src)
        self.assertEqual(code, 1)
        self.assertIn("normalization contract not verified", output)
        self.assertIn("benchmark class not found", output)

    def test_inprocess_missing_job_fails(self):
        paths = build_reports(self.tmp)
        path = next(p for p in paths if "SqliteBenchmarkWarmDecompose" in p.name)
        data = load_json(path)
        data["Benchmarks"] = [
            b
            for b in data["Benchmarks"]
            if not (
                b["Method"] == "Cte_Construct"
                and "Toolchain=InProcessEmitToolchain" in b["DisplayInfo"]
            )
        ]
        save_json(path, data)
        code, output = run_gate(self.tmp)
        self.assertEqual(code, 1)
        self.assertIn("missing job Cte_Construct/InProcessEmitToolchain", output)

    def test_inprocess_over_budget_returns_nonzero(self):
        build_reports(self.tmp, {("Build_Sql", "InProcessEmitToolchain"): 9000})
        code, output = run_gate(self.tmp)
        self.assertEqual(code, 1)
        self.assertIn("over budget Build_Sql/InProcessEmitToolchain", output)
        self.assertIn("9000.00 B/op > 8982", output)

    def test_no_reports_found_fails(self):
        code, output = run_gate(self.tmp)
        self.assertEqual(code, 1)
        self.assertIn("no BDN JSON reports found", output)

    def test_timeout_fails(self):
        db = self.tmp / "seed" / "test.db"
        db.parent.mkdir(parents=True, exist_ok=True)
        db.write_bytes(b"")
        reports = self.tmp / "reports"
        reports.mkdir()
        with contextlib.redirect_stdout(io.StringIO()) as buffer:
            code = gate.gate_main(
                ["--reports-dir", str(reports), "--db", str(db)],
                runner=lambda args, path: (None, 1.5),
            )
        self.assertEqual(code, 1)
        self.assertIn("timed out after 1.5s", buffer.getvalue())

    def test_benchmark_failure_returns_nonzero(self):
        # Non-zero benchmark process exit is rejected before any parsing.
        db = self.tmp / "seed" / "test.db"
        db.parent.mkdir(parents=True, exist_ok=True)
        db.write_bytes(b"")
        reports = self.tmp / "reports"
        reports.mkdir()
        with contextlib.redirect_stdout(io.StringIO()) as buffer:
            code = gate.gate_main(
                [
                    "--reports-dir",
                    str(reports),
                    "--db",
                    str(db),
                ],
                runner=lambda args, path: (2, 0.5),
            )
        self.assertEqual(code, 1)
        self.assertIn("exited non-zero (2)", buffer.getvalue())

        # A benchmark report whose MemoryDiagnoser payload is absent also fails closed.
        build_reports(reports)
        path = next(p for p in reports.glob("*SqliteBenchmarkCachedPlan*.json"))
        data = load_json(path)
        find_entry(data, "Prepared_ToList", "Default")["Memory"] = None
        save_json(path, data)
        code, output = run_gate(reports)
        self.assertEqual(code, 1)
        self.assertIn("invalid allocation Prepared_ToList/Default", output)


if __name__ == "__main__":
    unittest.main()
