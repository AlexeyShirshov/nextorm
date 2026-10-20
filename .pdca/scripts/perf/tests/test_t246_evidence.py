#!/usr/bin/env python3
"""Unit tests for scripts/perf/t246_evidence.py (stdlib unittest, no pytest)."""
from __future__ import annotations

import copy
import contextlib
import fcntl
import importlib.util
import io
import json
import os
import shutil
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
SPEC = importlib.util.spec_from_file_location(
    "t246_evidence", REPO / "scripts/perf/t246_evidence.py"
)
T246 = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(T246)


class T246TestCase(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="t246-test-"))

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)


class ThresholdAndRatioTests(unittest.TestCase):
    def test_exact_time_upper_boundary_is_1_00(self):
        ratio = T246.time_upper_ratio(100.0, 1.0, 102.0, 1.0)
        self.assertEqual(ratio, 1.0)
        self.assertTrue(ratio <= 1.00)

    def test_time_upper_above_boundary_fails(self):
        ratio = T246.time_upper_ratio(100.0, 1.0, 101.9, 1.0)
        self.assertGreater(ratio, 1.00)

    def test_nonpositive_denominator_rejected(self):
        with self.assertRaises(T246.Reject):
            T246.time_upper_ratio(100.0, 1.0, 1.0, 1.0)
        with self.assertRaises(T246.Reject):
            T246.time_upper_ratio(100.0, 1.0, 0.5, 0.5)

    def test_immutable_threshold_derivation_ceil(self):
        e_star, s_star = T246.derive_thresholds(0.01001, 0.01001)
        self.assertEqual(e_star, 0.0131)
        self.assertEqual(s_star, 0.0201)
        self.assertEqual(T246.derive_thresholds(0.027, 0.04), (0.0300, 0.0500))

    def test_threshold_caps_reject_overflow(self):
        with self.assertRaises(T246.Reject):
            T246.derive_thresholds(0.0271, 0.0)
        with self.assertRaises(T246.Reject):
            T246.derive_thresholds(0.0, 0.0401)

    def test_allocation_arithmetic(self):
        self.assertEqual(T246.allocation_ratio(1000, 1000), 1.0)
        self.assertLess(T246.allocation_ratio(800, 1040), 1.0)
        self.assertGreater(T246.allocation_ratio(8256, 1040), 1.0)
        self.assertEqual(12740 - 8256, 4484)
        self.assertEqual(5520 - 1040, 4480)
        self.assertEqual(12740 - 5520, 7220)
        self.assertEqual(8256 - 1040, 7216)
        self.assertEqual(8176 - 1040, 7136)

    def test_allocation_denominator_rejected(self):
        with self.assertRaises(T246.Reject):
            T246.allocation_ratio(100, 0)
        with self.assertRaises(T246.Reject):
            T246.allocation_ratio(None, 100)


class LaunchStatisticsTests(T246TestCase):
    def _stratum_fixture(self, operations=1):
        counts = {
            "invocation_count": 64,
            "unroll_factor": 1,
            "launch_count": 3,
            "warmup_count": 5,
            "iteration_count": 10,
        }
        means = {
            method: [5_000_000.0, 5_005_000.0, 5_010_000.0]
            for method in T246.STRATA["10k"]["methods"]
        }
        allocations = {
            method: 1000 for method in T246.STRATA["10k"]["methods"]
        }
        T246.build_stratum_fixture(
            self.tmp / "10k", "10k", means, allocations, counts=counts,
            operations=operations,
        )
        return self.tmp / "10k"

    def test_operations_greater_than_one_is_normalised_once(self):
        stratum_dir = self._stratum_fixture(operations=64)
        json_path = next(
            (stratum_dir / "results").glob("*-report-full-compressed.json")
        )
        document = json.loads(json_path.read_text(encoding="utf-8"))
        for benchmark in document["Benchmarks"]:
            workload = [
                measurement
                for measurement in benchmark["Measurements"]
                if measurement["IterationMode"] == "Workload"
                and measurement["IterationStage"] == "Result"
            ]
            self.assertEqual(len(workload), 30)
            for measurement in workload:
                self.assertEqual(measurement["Operations"], 64)
                # The retained JSON value is the iteration total, not ns/op.
                self.assertGreater(measurement["Nanoseconds"], 3.0e8)
        view = T246.parse_stratum(stratum_dir, "10k", 64)
        expected_launch_means = [5_000_000.0, 5_005_000.0, 5_010_000.0]
        for method, benchmark in view["benchmarks"].items():
            self.assertEqual(len(benchmark["launches"]), 3, method)
            for launch, expected in zip(
                benchmark["launches"], expected_launch_means
            ):
                # Exactly one division by 64: a missing or doubled division
                # moves the mean by 64x / 1/64 and fails this bound.
                self.assertLessEqual(
                    abs(launch["mean_ns"] - expected) / expected, 0.002, method
                )
            self.assertLessEqual(
                abs(benchmark["csv_mean_ns"] - 5_000_000.0) / 5_000_000.0, 0.002
            )

    def test_unnormalised_iteration_total_is_rejected(self):
        stratum_dir = self._stratum_fixture(operations=64)
        log_path = next(stratum_dir.glob("*.log"))
        text = log_path.read_text(encoding="utf-8")
        self.assertIn(" 64 op, ", text)
        # Keep the raw iteration totals but claim one operation: the log then
        # reports ~64x the JSON per-op mean and must be rejected.
        log_path.write_text(text.replace(" 64 op, ", " 1 op, "), encoding="utf-8")
        with self.assertRaises(T246.Reject) as caught:
            T246.parse_stratum(stratum_dir, "10k", 64)
        self.assertTrue(
            caught.exception.reason.startswith("log-json-launch-mean-mismatch"),
            caught.exception.reason,
        )

    def test_three_launch_statistics_are_independently_recoverable(self):
        view = T246.parse_stratum(self._stratum_fixture(), "10k", 64)
        for method, benchmark in view["benchmarks"].items():
            self.assertEqual(len(benchmark["launches"]), 3, method)
            for launch in benchmark["launches"]:
                self.assertEqual(launch["n"], 10)
                self.assertEqual(launch["requested_n"], 10)
                self.assertGreater(launch["error_ns"], 0)
                self.assertGreater(launch["median_ns"], 0)
            self.assertEqual(benchmark["pooled"]["level_raw"], 12)
            self.assertEqual(benchmark["pooled"]["level_pct"], 99.9)

    def test_pooled_only_statistics_are_rejected(self):
        stratum_dir = self._stratum_fixture()
        json_path = next((stratum_dir / "results").glob("*-report-full-compressed.json"))
        document = json.loads(json_path.read_text(encoding="utf-8"))
        for benchmark in document["Benchmarks"]:
            benchmark.pop("Measurements", None)
        T246.write_json(json_path, document)
        with self.assertRaises(T246.Reject):
            T246.parse_stratum(stratum_dir, "10k", 64)

    def test_unsupported_confidence_level_rejected(self):
        stratum_dir = self._stratum_fixture()
        json_path = next((stratum_dir / "results").glob("*-report-full-compressed.json"))
        document = json.loads(json_path.read_text(encoding="utf-8"))
        for benchmark in document["Benchmarks"]:
            benchmark["Statistics"]["ConfidenceInterval"]["Level"] = 7
        T246.write_json(json_path, document)
        with self.assertRaises(T246.Reject):
            T246.parse_stratum(stratum_dir, "10k", 64)

    def test_invocation_count_cannot_substitute_for_statistical_n(self):
        with self.assertRaises(T246.Reject):
            T246.validate_requested_n(64, 64)
        with self.assertRaises(T246.Reject):
            T246.validate_requested_n(11, 64)
        self.assertEqual(T246.validate_requested_n(10, 64), None)


class ResolvedJobTests(T246TestCase):
    """H-N2: an out-of-process job is accepted by resolved settings, not name."""

    COUNTS = {
        "invocation_count": 64,
        "unroll_factor": 1,
        "launch_count": 3,
        "warmup_count": 5,
        "iteration_count": 10,
    }
    REQUIRED = {
        "invocationcount": 64,
        "iterationcount": 10,
        "launchcount": 3,
        "unrollfactor": 1,
        "warmupcount": 5,
    }

    def _fixture(self, **overrides):
        params = {
            "job_id": "Job-NREBDM",
            "csv_job": None,
            "toolchain": "Default",
            "job_header_override": None,
            "job_setting_overrides": None,
        }
        params.update(overrides)
        means = {
            method: [5_000_000.0, 5_005_000.0, 5_010_000.0]
            for method in T246.STRATA["10k"]["methods"]
        }
        allocations = {
            method: 1000 for method in T246.STRATA["10k"]["methods"]
        }
        T246.build_stratum_fixture(
            self.tmp / "10k", "10k", means, allocations, counts=self.COUNTS,
            job_id=params["job_id"], csv_job=params["csv_job"],
            toolchain=params["toolchain"],
            job_header_override=params["job_header_override"],
            job_setting_overrides=params["job_setting_overrides"],
        )
        return self.tmp / "10k"

    def _view_and_reasons(self, **overrides):
        view = T246.parse_stratum(self._fixture(**overrides), "10k", 64)
        return view, T246.validate_stratum_view(view, "10k", 64)

    def test_resolved_out_of_process_job_is_accepted(self):
        view, reasons = self._view_and_reasons()
        self.assertEqual(reasons, [])
        self.assertTrue(view["job"]["is_default"])
        self.assertTrue(view["job"]["out_of_process"])
        self.assertEqual(view["job"]["resolved"]["job_id"], "Job-NREBDM")
        self.assertEqual(view["job"]["resolved"]["settings"], self.REQUIRED)

    def test_truncated_or_bare_header_is_rejected(self):
        for header in ("Job-NREBDM", "Job-NREBDM()"):
            view, reasons = self._view_and_reasons(job_header_override=header)
            self.assertFalse(view["job"]["is_default"], header)
            self.assertTrue(
                any(reason.startswith("job-header-unresolved") for reason in reasons),
                reasons,
            )

    def test_csv_log_job_mismatch_is_rejected(self):
        view, reasons = self._view_and_reasons(csv_job="Job-OTHER")
        self.assertFalse(view["job"]["is_default"])
        self.assertIn("csv-log-job-mismatch:Job-OTHER!=Job-NREBDM", reasons)

    def test_in_process_toolchain_is_rejected_even_with_resolved_settings(self):
        view, reasons = self._view_and_reasons(toolchain="InProcessEmitToolchain")
        self.assertFalse(view["job"]["out_of_process"])
        self.assertFalse(view["job"]["is_default"])
        self.assertIn("job-toolchain-inprocess:InProcessEmitToolchain", reasons)

    def test_each_wrong_resolved_setting_is_named(self):
        wrong_values = {
            "InvocationCount": 32,
            "IterationCount": 9,
            "LaunchCount": 2,
            "UnrollFactor": 4,
            "WarmupCount": 0,
        }
        expected = {
            "InvocationCount": 64,
            "IterationCount": 10,
            "LaunchCount": 3,
            "UnrollFactor": 1,
            "WarmupCount": 5,
        }
        for name, wrong in wrong_values.items():
            view, reasons = self._view_and_reasons(
                job_setting_overrides={name: wrong}
            )
            key = name.lower()
            self.assertFalse(view["job"]["is_default"], name)
            self.assertIn(
                "job-setting-mismatch:%s:%d!=%d" % (key, wrong, expected[name]),
                reasons,
                name,
            )
            self.assertIn(
                "job-settings-csv-mismatch:%s:%d!=%d" % (key, wrong, expected[name]),
                reasons,
                name,
            )

    def test_missing_resolved_setting_is_named(self):
        view, reasons = self._view_and_reasons(
            job_header_override=(
                "Job-NREBDM(InvocationCount=64, IterationCount=10, "
                "LaunchCount=3, UnrollFactor=1)"
            )
        )
        self.assertFalse(view["job"]["is_default"])
        self.assertIn("job-setting-missing:warmupcount", reasons)

    def test_real_rv2_run1_header_parses_into_required_tuple(self):
        header = T246.parse_job_header(
            "// Job: Job-NREBDM(InvocationCount=64, IterationCount=10, "
            "LaunchCount=3, UnrollFactor=1, WarmupCount=5)\n"
        )
        self.assertEqual(header["job_id"], "Job-NREBDM")
        self.assertEqual(header["settings"], self.REQUIRED)


class ModeDetectorTests(unittest.TestCase):
    @staticmethod
    def observations(values):
        result = []
        run = 1
        launch = 1
        for value in values:
            result.append(
                {"run": run, "launch": launch, "median_ns": value, "mean_ns": value,
                 "error_ns": value * 0.001, "n": 10, "requested_n": 10}
            )
            launch += 1
            if launch > 3:
                launch = 1
                run += 1
        return result

    def test_gap_at_or_above_15_percent_splits(self):
        modes = T246.detect_labeled_modes(
            self.observations([100.0, 101.0, 102.0, 120.0, 121.0, 122.0,
                               100.5, 101.5, 102.5])
        )
        self.assertEqual(len(modes), 2)

    def test_gap_below_15_percent_stays_one_mode(self):
        modes = T246.detect_labeled_modes(
            self.observations([100.0, 101.0, 102.0, 114.0, 114.5, 115.0,
                               100.5, 101.5, 102.5])
        )
        self.assertEqual(len(modes), 1)

    def test_unreplicated_mode_is_rejected(self):
        observations = self.observations(
            [100.0, 101.0, 102.0, 140.0, 141.0, 142.0, 100.5, 101.5, 102.5]
        )
        modes = T246.detect_labeled_modes(observations)
        reasons = T246.mode_replication_reasons(modes, 3)
        self.assertTrue(any("unreplicated-mode" in reason for reason in reasons))

    def test_averaged_or_hidden_modes_are_rejected(self):
        observations = self.observations(
            [100.0, 101.0, 102.0, 140.0, 141.0, 142.0, 100.5, 101.5, 102.5]
        )
        declared_one = [
            {
                "index": 0,
                "members": [
                    {"run": item["run"], "launch": item["launch"]}
                    for item in observations
                ],
            }
        ]
        reasons = T246.check_mode_declaration(observations, declared_one, 3)
        self.assertTrue(any("declared-modes" in reason for reason in reasons))


class GateFixtureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = Path(tempfile.mkdtemp(prefix="t246-gate-"))
        cls.paths = T246.build_gate_fixture(cls.tmp)
        cls.base = T246.load_gate_inputs(cls.paths["runs"], cls.paths["budgets"])

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def evaluate_mutated(self, mutate):
        inputs = copy.deepcopy(self.base)
        mutate(inputs)
        return T246.evaluate_gate(inputs)

    def assert_rejected(self, mutate):
        result = self.evaluate_mutated(mutate)
        self.assertNotEqual(result["status"], "PASS")
        self.assertTrue(result["input_errors"] or result["reasons"])

    def test_reference_fixture_passes(self):
        result = T246.evaluate_gate(copy.deepcopy(self.base))
        self.assertEqual(result["status"], "PASS", result)
        self.assertGreater(result["measured"]["e_worst"], 0.0)
        self.assertGreater(result["measured"]["upper_ratio_worst"], 0.0)

    def test_allocation_exact_boundary_passes(self):
        def mutate(inputs):
            inputs["runs"][1]["strata"]["10k"]["benchmarks"][
                "B_Nextorm_ToDataReader"
            ]["bytes_per_op"] = 961_024

        result = self.evaluate_mutated(mutate)
        self.assertEqual(result["status"], "PASS", result["input_errors"])

    def test_allocation_above_boundary_fails(self):
        def mutate(inputs):
            inputs["runs"][1]["strata"]["10k"]["benchmarks"][
                "B_Nextorm_ToDataReader"
            ]["bytes_per_op"] = 961_025

        self.assert_rejected(mutate)

    def test_missing_arm_rejected(self):
        self.assert_rejected(
            lambda inputs: inputs["runs"][1]["strata"]["10k"]["benchmarks"].pop(
                "A_Nextorm_Prepared_ToDataReader"
            )
        )

    def test_duplicate_row_rejected(self):
        self.assert_rejected(
            lambda inputs: inputs["runs"][1]["strata"]["10k"]["duplicates"].append(
                "A_Nextorm_Prepared_ToDataReader"
            )
        )

    def test_na_status_rejected(self):
        self.assert_rejected(
            lambda inputs: inputs["runs"][1]["strata"]["10k"]["benchmarks"][
                "B_Nextorm_ToDataReader"
            ].__setitem__("bytes_per_op", None)
        )

    def test_absent_per_launch_error_rejected(self):
        self.assert_rejected(
            lambda inputs: inputs["runs"][1]["strata"]["10k"]["benchmarks"][
                "B_Nextorm_ToDataReader"
            ].__setitem__("launches", [])
        )

    def test_missing_empty_arm_rejected(self):
        self.assert_rejected(lambda inputs: inputs["runs"][2]["strata"].pop("empty"))

    def test_missing_dapper_rejected(self):
        self.assert_rejected(
            lambda inputs: inputs["runs"][1]["strata"]["10k"]["benchmarks"].pop(
                "Dapper_ToDataReader"
            )
        )

    def test_inprocess_job_rejected(self):
        def mutate(inputs):
            inputs["runs"][1]["strata"]["10k"]["job"].update(
                {
                    "job": "ShortRun",
                    "toolchain": "InProcessEmitToolchain",
                    "display_info": "Job-ABC InProcessEmitToolchain",
                    "out_of_process": False,
                    "is_default": False,
                }
            )

        self.assert_rejected(mutate)

    def test_count_mismatch_rejected(self):
        self.assert_rejected(
            lambda inputs: inputs["runs"][1]["strata"]["10k"]["counts"].__setitem__(
                "invocation_count", 32
            )
        )

    def test_db_sha_mismatch_rejected(self):
        self.assert_rejected(
            lambda inputs: inputs["runs"][1]["manifest"].__setitem__(
                "db_sha256", "0" * 64
            )
        )

    def test_parity_mismatch_rejected(self):
        self.assert_rejected(
            lambda inputs: inputs["runs"][1]["manifest"]["parity"].__setitem__(
                "rows", 9999
            )
        )

    def test_nonpositive_denominator_rejected(self):
        def mutate(inputs):
            for launch in inputs["runs"][1]["strata"]["10k"]["benchmarks"][
                "Linq2Db_ToDataReader"
            ]["launches"]:
                launch.update({"mean_ns": 10.0, "error_ns": 20.0})

        self.assert_rejected(mutate)

    def test_error_above_cap_rejected(self):
        def mutate(inputs):
            benchmark = inputs["runs"][1]["strata"]["10k"]["benchmarks"][
                "B_Nextorm_ToDataReader"
            ]
            benchmark["launches"][0]["error_ns"] = (
                benchmark["launches"][0]["mean_ns"] * 0.05
            )

        self.assert_rejected(mutate)

    def test_spread_above_cap_rejected(self):
        def mutate(inputs):
            for run in (2, 3):
                for launch in inputs["runs"][run]["strata"]["10k"]["benchmarks"][
                    "B_Nextorm_ToDataReader"
                ]["launches"]:
                    launch["mean_ns"] *= 1.05
                    launch["median_ns"] *= 1.05

        self.assert_rejected(mutate)

    def test_unsupported_confidence_level_rejected(self):
        self.assert_rejected(
            lambda inputs: inputs["runs"][1]["strata"]["10k"]["benchmarks"][
                "B_Nextorm_ToDataReader"
            ]["pooled"].update({"level_raw": 7, "level_pct": 95.0})
        )

    def test_stale_hash_rejected(self):
        self.assert_rejected(
            lambda inputs: inputs["qualification"].__setitem__(
                "treatment_hash", "0" * 64
            )
        )

    def test_qualification_reports_but_does_not_gate_product_ratios(self):
        inputs = copy.deepcopy(self.base)
        inputs["runs"][1]["strata"]["10k"]["benchmarks"][
            "B_Nextorm_ToDataReader"
        ]["bytes_per_op"] = 2_000_000
        reported = T246.analyze_campaign(
            inputs["runs"],
            inputs["budgets"],
            T246.QUALIFICATION_ERROR_CAP,
            T246.QUALIFICATION_SPREAD_CAP,
            enforce_product_gates=False,
        )
        self.assertEqual(reported["status"], "PASS", reported["reasons"])
        self.assertGreater(reported["measured"]["allocation_ratio_worst"], 1.0)
        gated = T246.analyze_campaign(
            inputs["runs"],
            inputs["budgets"],
            T246.QUALIFICATION_ERROR_CAP,
            T246.QUALIFICATION_SPREAD_CAP,
            enforce_product_gates=True,
        )
        self.assertEqual(gated["status"], "FAIL")
        self.assertTrue(any("allocation-ratio" in reason for reason in gated["reasons"]))

    def test_widened_empty_tolerance_rejected(self):
        def mutate(inputs):
            for row in inputs["budgets"]["rows"]:
                if row["stratum"] == "empty":
                    row["empty_tolerance"] = 1

        self.assert_rejected(mutate)

    def test_self_test_negative_fixtures_all_reject(self):
        payload = T246.self_test_cases()
        self.assertEqual(payload["status"], "PASS", payload)
        self.assertGreaterEqual(len(payload["cases"]), 17)
        for case in payload["cases"]:
            self.assertTrue(case["pass"], case)


class HostGuardTests(unittest.TestCase):
    """H-N3: an idle compiler/build server is not contamination."""

    ROOTS = {424242}

    @staticmethod
    def process(pid, pcpu, comm, args=""):
        return {"pid": pid, "pcpu": pcpu, "comm": comm, "args": args}

    def idle_server(self, pid=4100):
        # ps %CPU is a lifetime average: a server that compiled once keeps a
        # large value while being instantaneously idle.
        return self.process(
            pid, 38.5, "VBCSCompiler",
            "dotnet exec /usr/share/dotnet/sdk/10.0.401/Roslyn/bincore/VBCSCompiler.dll",
        )

    def idle_msbuild_node(self, pid=4101):
        return self.process(
            pid, 2.0, "MSBuild",
            "dotnet /usr/share/dotnet/sdk/10.0.401/MSBuild.dll /nodemode:1",
        )

    ACTIVE = (
        ("csc", "csc.dll",
         "dotnet exec /usr/share/dotnet/sdk/10.0.401/Roslyn/bincore/csc.dll /noconfig"),
        ("testhost", "testhost", "testhost --port 4242 --endpoint 127.0.0.1"),
        ("dotnet build", "dotnet", "dotnet build -c Release nextorm.slnx"),
        ("dotnet test", "dotnet", "dotnet test tests/nextorm.core.tests -c Debug"),
        ("dotnet publish", "dotnet", "dotnet publish -c Release -o out"),
        ("dotnet-coverage", "dotnet-coverage", "dotnet-coverage collect dotnet test"),
        ("BenchmarkDotNet", "dotnet",
         "dotnet exec /home/u/.nuget/packages/benchmarkdotnet/0.15.8/lib/BenchmarkDotNet.dll"),
    )

    def test_idle_compiler_server_alone_is_not_contamination(self):
        foreign = T246._foreign_processes(
            [self.idle_server(), self.idle_msbuild_node()], self.ROOTS
        )
        self.assertEqual(foreign, [])

    def test_each_active_competitor_is_rejected(self):
        for index, (label, comm, args) in enumerate(self.ACTIVE, start=4200):
            active = self.process(index, 55.0, comm, args)
            foreign = T246._foreign_processes([active], self.ROOTS)
            self.assertEqual([item["pid"] for item in foreign], [index], label)
            self.assertEqual(foreign[0]["comm"], comm, label)

    def test_active_process_alongside_idle_compiler_server_is_rejected(self):
        for index, (label, comm, args) in enumerate(self.ACTIVE, start=4300):
            active = self.process(index, 55.0, comm, args)
            foreign = T246._foreign_processes(
                [self.idle_server(), self.idle_msbuild_node(), active], self.ROOTS
            )
            self.assertEqual([item["pid"] for item in foreign], [index], label)

    def test_idle_server_is_recorded_in_the_summary_observably(self):
        samples = [
            {
                "monotonic": 1.0,
                "stratum": "10k",
                "ps_available": True,
                "foreign_pids": [],
                "foreign_cpu_percent": 0.0,
                "freq_available": False,
                "thermal_available": False,
                "throttle_total": None,
            }
        ]
        summary = T246.summarize_monitor(samples)
        self.assertFalse(summary["contaminated"])
        self.assertEqual(summary["foreign_pids"], [])


class ParseCheckTests(T246TestCase):
    """C-H-PARSE: offline reconciliation of captured log/JSON per-op values."""

    COUNTS = {
        "invocation_count": 64,
        "unroll_factor": 1,
        "launch_count": 3,
        "warmup_count": 5,
        "iteration_count": 10,
    }

    def _runs_root(self, operations=64):
        means = {
            method: [5_000_000.0, 5_005_000.0, 5_010_000.0]
            for method in T246.STRATA["10k"]["methods"]
        }
        allocations = {
            method: 1000 for method in T246.STRATA["10k"]["methods"]
        }
        T246.build_stratum_fixture(
            self.tmp / "run-1" / "10k", "10k", means, allocations,
            counts=self.COUNTS, operations=operations,
        )
        return self.tmp

    def test_parse_check_accepts_operations_64_fixture(self):
        root = self._runs_root()
        entry = T246.parse_check_run(root / "run-1")
        self.assertEqual(entry["verdict"], "pass", entry)
        self.assertEqual(entry["operations"], 64)
        self.assertLessEqual(entry["relative_diff"], 1e-9)
        self.assertAlmostEqual(entry["unnormalised_ratio"], 64.0, delta=1e-6)
        self.assertAlmostEqual(
            entry["unnormalised_total_ns"] / entry["log_per_op_ns"], 64.0,
            delta=1e-6,
        )
        for key in ("log", "json", "csv"):
            self.assertTrue(Path(entry[key]).is_file(), key)

    def test_parse_check_cli_accepts_fixture_and_prints_summary(self):
        root = self._runs_root()
        buffer = io.StringIO()
        with contextlib.redirect_stdout(buffer):
            exit_code = T246.main(["parse-check", "--runs", str(root)])
        self.assertEqual(exit_code, 0)
        payload = json.loads(buffer.getvalue())
        self.assertEqual(
            payload["summary"],
            {"runs_checked": 1, "passed": 1, "failed": 0},
        )
        self.assertEqual(len(payload["runs"]), 1)
        self.assertEqual(payload["runs"][0]["verdict"], "pass")

    def test_parse_check_rejects_operation_count_mismatch(self):
        root = self._runs_root()
        log_path = next((root / "run-1" / "10k").glob("*.log"))
        log_path.write_text(
            log_path.read_text(encoding="utf-8").replace(" 64 op, ", " 1 op, "),
            encoding="utf-8",
        )
        entry = T246.parse_check_run(root / "run-1")
        self.assertEqual(entry["verdict"], "fail")
        self.assertIn("ambiguous-operations", entry["reason"])

    def test_parse_check_cli_fails_and_returns_nonzero(self):
        root = self._runs_root()
        csv_path = next((root / "run-1" / "10k" / "results").glob("*-report.csv"))
        csv_path.unlink()
        buffer = io.StringIO()
        with contextlib.redirect_stdout(buffer):
            exit_code = T246.main(["parse-check", "--runs", str(root)])
        self.assertEqual(exit_code, T246.EXIT_FAIL)
        payload = json.loads(buffer.getvalue())
        self.assertEqual(
            payload["summary"],
            {"runs_checked": 1, "passed": 0, "failed": 1},
        )
        self.assertIn("missing-measurement-data:csv", payload["runs"][0]["reason"])


class DriverInvariantTests(T246TestCase):
    def config(self, **overrides):
        config = {
            "root": str(self.tmp),
            "phase": "baseline",
            "runs": 3,
            "max_attempts": 4,
            "lock": str(self.tmp / "host.lock"),
            "lock_wait_seconds": 1,
            "run_timeout_seconds": 600,
            "total_budget_seconds": 7200,
            "quiet_seconds": 0,
            "caller_argv": ["test"],
        }
        config.update(overrides)
        return config

    def stub_executor(self, outcomes, calls, lock_probe, duration=1.0):
        def execute(config, attempt_index, invocation_counts):
            calls.append((attempt_index, dict(invocation_counts)))
            fd = os.open(config["lock"], os.O_CREAT | os.O_RDWR, 0o644)
            try:
                try:
                    fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
                    lock_probe.append(False)
                    fcntl.flock(fd, fcntl.LOCK_UN)
                except BlockingIOError:
                    lock_probe.append(True)
            finally:
                os.close(fd)
            outcome = outcomes[min(len(calls) - 1, len(outcomes) - 1)]
            if outcome == "valid":
                return {
                    "valid": True,
                    "reason": None,
                    "duration_seconds": duration,
                    "run_dir": str(self.tmp / "perf" / config["phase"] /
                                   ("run-%d" % attempt_index)),
                }
            return {
                "valid": False,
                "reason": "fixture-invalid",
                "duration_seconds": duration,
            }

        return execute

    def test_single_lock_lifetime_for_complete_block(self):
        calls = []
        lock_probe = []
        result = T246.measure_campaign(
            self.config(), self.stub_executor(["valid"], calls, lock_probe)
        )
        self.assertEqual(result["exit_code"], 0)
        self.assertEqual(len(calls), 3)
        self.assertTrue(all(lock_probe))
        ledger = json.loads((self.tmp / "perf" / "attempt-ledger.json").read_text())
        self.assertEqual(len(ledger["lock_history"]), 1)
        self.assertEqual(ledger["lock_history"][0]["acquisitions"], 1)
        self.assertIn("acquired_at", ledger["lock_history"][0])
        self.assertIn("released_at", ledger["lock_history"][0])

    def test_stops_after_three_valid_runs(self):
        calls = []
        result = T246.measure_campaign(
            self.config(), self.stub_executor(["valid"], calls, [])
        )
        self.assertEqual(result["exit_code"], 0)
        self.assertEqual(len(calls), 3)

    def test_attempt_ceiling_is_terminal_gap(self):
        calls = []
        result = T246.measure_campaign(
            self.config(max_attempts=2),
            self.stub_executor(["invalid"], calls, []),
        )
        self.assertEqual(result["exit_code"], T246.EXIT_GAP)
        self.assertEqual(len(calls), 2)
        gap = json.loads(
            (self.tmp / "perf" / "baseline" / "TERMINAL_GAP.json").read_text()
        )
        self.assertIn("TERMINAL_GAP", gap["reason"])

    def test_eight_execution_ceiling_is_shared(self):
        ledger = {
            "task": T246.TASK,
            "schema": 1,
            "attempts": [
                {"phase": "baseline", "attempt": index + 1, "duration_seconds": 1.0}
                for index in range(8)
            ],
            "lock_history": [],
            "overhead_seconds": 0.0,
            "remedies": {},
            "invocation_counts": {},
        }
        T246.write_json(self.tmp / "perf" / "attempt-ledger.json", ledger)
        with self.assertRaises(T246.Reject) as caught:
            T246.measure_campaign(self.config(phase="final"), lambda *a: None)
        self.assertIn("total-execution-ceiling", caught.exception.reason)

    def test_shared_two_hour_budget_rejects_before_start(self):
        ledger = {
            "task": T246.TASK,
            "schema": 1,
            "attempts": [
                {"phase": "baseline", "attempt": 1, "duration_seconds": 7200.0}
            ],
            "lock_history": [],
            "overhead_seconds": 0.0,
            "remedies": {},
            "invocation_counts": {},
        }
        T246.write_json(self.tmp / "perf" / "attempt-ledger.json", ledger)
        calls = []
        with self.assertRaises(T246.Reject) as caught:
            T246.measure_campaign(
                self.config(phase="final"),
                self.stub_executor(["valid"], calls, []),
            )
        self.assertIn("measurement-budget-exhausted", caught.exception.reason)
        self.assertEqual(calls, [])

    def test_one_authorized_invocation_doubling(self):
        ledger = {"attempts": [], "remedies": {}, "invocation_counts": {}}
        counts = {"10k": 64, "empty": 4096}
        new_count = T246.apply_invocation_remedy(ledger, "baseline", "10k", counts, 1)
        self.assertEqual(new_count, 128)
        remedy = ledger["remedies"]["baseline"]["10k"]
        self.assertEqual((remedy["from"], remedy["to"], remedy["attempt"]), (64, 128, 1))
        with self.assertRaises(T246.Reject):
            T246.apply_invocation_remedy(ledger, "baseline", "10k", counts, 2)

    def test_remedy_invalidates_earlier_runs_of_phase(self):
        ledger = {
            "attempts": [
                {"phase": "baseline", "attempt": 1, "valid": True,
                 "invocation_counts": {"10k": 64, "empty": 4096}},
            ],
            "remedies": {},
            "invocation_counts": {},
        }
        T246.apply_invocation_remedy(
            ledger, "baseline", "10k", {"10k": 64, "empty": 4096}, 2
        )
        self.assertFalse(ledger["attempts"][0]["valid"])
        self.assertEqual(
            ledger["attempts"][0]["reason"], "invocation-count-remedy-incompatible"
        )

    def test_iteration_count_is_fixed_and_never_promoted(self):
        self.assertEqual(T246.ITERATION_COUNT, 10)
        with self.assertRaises(T246.Reject):
            T246.validate_requested_n(64, 64)
        with self.assertRaises(T246.Reject):
            T246.validate_requested_n(9, 64)

    def test_driver_rejects_invocation_count_substituted_for_n(self):
        with self.assertRaises(T246.Reject) as caught:
            T246.validate_requested_n(4096, 4096)
        self.assertIn("invocation-count-substituted", caught.exception.reason)

    def test_second_duration_warning_is_rejected_by_remedy(self):
        ledger = {"attempts": [], "remedies": {}, "invocation_counts": {}}
        counts = {"10k": 128, "empty": 4096}
        ledger["remedies"] = {"baseline": {"10k": {"from": 64, "to": 128}}}
        with self.assertRaises(T246.Reject):
            T246.apply_invocation_remedy(ledger, "baseline", "10k", counts, 3)


if __name__ == "__main__":
    unittest.main()
