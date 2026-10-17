#!/usr/bin/env python3
"""Unit tests for eng/perf/t246_evidence.py (stdlib unittest, no pytest)."""
from __future__ import annotations

import copy
import fcntl
import importlib.util
import json
import os
import shutil
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
SPEC = importlib.util.spec_from_file_location(
    "t246_evidence", REPO / "eng/perf/t246_evidence.py"
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
    def _stratum_fixture(self):
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
            self.tmp / "10k", "10k", means, allocations, counts=counts
        )
        return self.tmp / "10k"

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
