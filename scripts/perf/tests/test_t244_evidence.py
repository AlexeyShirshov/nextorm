#!/usr/bin/env python3
"""Self-tests for scripts/perf/t244_evidence.py (stdlib unittest; no benchmark is run).

Run with either::

    python3 -m unittest discover -s scripts/perf/tests -p 'test_t244_evidence.py' -v
    python3 -m pytest scripts/perf/tests/test_t244_evidence.py -q

Every fixture is a synthetic BDN JSON export mirroring the exporter schema
(``Benchmarks[].{Namespace,Type,Method,DisplayInfo,OperationsPerInvoke,
Memory.BytesAllocatedPerOperation,Statistics.Mean}``), so the real pairing,
divisor, tier, attribution and staging paths are exercised without executing
BenchmarkDotNet.  The tests cover: per-class /10 vs /100 divisors, missing
comparator, duplicate rows, missing toolchain/job, NA, non-1 OperationsPerInvoke,
nonzero ``*_Warm_Reused``, one failing CTE row, checkpoint vs final, exit codes
0/1/2, allocation-failure persistence across rounds, the median-of-three timing
repeat rule, attribution nesting/double-count rejection and the residual bound,
malformed/missing artifacts, and staging only the current invocation's outputs.
"""
from __future__ import annotations

import contextlib
import io
import json
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

PERF_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PERF_DIR))

import t244_evidence as t244  # noqa: E402

NAMESPACE = "NextORM.Benchmark"
TYPE_CACHED = "SqliteBenchmarkFeaturesFairCached"
TYPE_FAIR = "SqliteBenchmarkFeaturesFair"
TYPE_WARM = "SqliteBenchmarkWarmDecompose"
TOOLCHAINS = ("Default", "InProcessEmitToolchain")


def display(bench_type, method, toolchain="Default", job="ShortRun"):
    toolchain_part = "" if toolchain == "Default" else f"Toolchain={toolchain}, "
    return (
        f"{bench_type}.{method}: {job}({toolchain_part}IterationCount=3, "
        "LaunchCount=1, WarmupCount=3)"
    )


def entry_toolchain(entry):
    text = entry["DisplayInfo"]
    if "Toolchain=" in text:
        return text.split("Toolchain=", 1)[1].split(",", 1)[0].split(")", 1)[0]
    return "Default"


def row(bench_type, method, allocated, mean=100.0, toolchain="Default", job="ShortRun", opi=None):
    return {
        "Namespace": NAMESPACE,
        "Type": bench_type,
        "Method": method,
        "DisplayInfo": display(bench_type, method, toolchain, job),
        "OperationsPerInvoke": opi,
        "Statistics": {"Mean": mean},
        "Memory": {"BytesAllocatedPerOperation": allocated},
    }


def write_report(path, rows):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"Title": "T244 fixture", "Benchmarks": rows}), encoding="utf-8")
    return path


def default_warm():
    # Raw-recipe-hit shape: full preparation (8000/9000) does NOT run on the warm path,
    # so the measured end-to-end arm is smaller than the preparation arms.
    return {
        "Construct": 6900.0,
        "Prepare_NoHash": 8000.0,
        "Prepare_Hash": 9000.0,
        "Warm_PlanOnly": 7000.0,
        "Warm_Reused": 0.0,
    }


def warm_rows(warm, family="Cte", toolchain="Default", plan_only_mean=100.0):
    rows = []
    for suffix in t244.WARM_METHODS:
        value = warm[suffix]
        mean = plan_only_mean if suffix == "Warm_PlanOnly" else 100.0
        rows.append(
            row(TYPE_WARM, f"{family}_{suffix}", int(round(value * 100)), mean, toolchain)
        )
    return rows


def cached_rows(
    cte=(100.0, 100.0),
    recursive=(100.0, 100.0),
    other=(100.0, 100.0),
    toolchain="Default",
    cte_mean=100.0,
):
    values = {"Cte": cte, "RecursiveCte": recursive}
    for scenario in t244.SCENARIOS:
        nextorm, linq = values.get(scenario, other)
        mean = cte_mean if scenario == "Cte" else 100.0
        rows = [
            row(TYPE_CACHED, f"B_Nextorm_Cached_{scenario}", int(round(nextorm * 10)), mean, toolchain),
            row(TYPE_CACHED, f"B_Linq2Db_{scenario}", int(round(linq * 10)), 100.0, toolchain),
        ]
        for entry in rows:
            yield entry


def prepared_rows(alloc=1000, mean=100.0, toolchain="Default"):
    for name in ("LeftJoin", "Join4", "Cte"):
        yield row(TYPE_FAIR, f"A_Nextorm_Prepared_{name}", alloc, mean, toolchain)


def write_round(
    round_dir,
    *,
    valid=True,
    reasons=None,
    cte=(100.0, 100.0),
    recursive=(100.0, 100.0),
    other=(100.0, 100.0),
    prepared=(1000, 100.0),
    warm=None,
    warm_plan_means=None,
    toolchains=TOOLCHAINS,
    cached_mutator=None,
    warm_mutator=None,
    raw_hit_counts=None,
):
    round_dir = Path(round_dir)
    round_dir.mkdir(parents=True, exist_ok=True)
    (round_dir / "round-validity.json").write_text(
        json.dumps({"valid": valid, "reasons": reasons or []}), encoding="utf-8"
    )
    warm = warm or default_warm()
    warm_plan_means = warm_plan_means or {}
    cached = []
    for toolchain in toolchains:
        cached.extend(cached_rows(cte, recursive, other, toolchain))
    if cached_mutator:
        cached_mutator(cached)
    write_report(
        round_dir / "results" / f"{NAMESPACE}.{TYPE_CACHED}-report-full-compressed.json", cached
    )
    prepared_entries = []
    for toolchain in toolchains:
        prepared_entries.extend(prepared_rows(prepared[0], prepared[1], toolchain))
    write_report(round_dir / "results" / f"{NAMESPACE}.{TYPE_FAIR}-report-full.json", prepared_entries)
    warm_entries = []
    for toolchain in toolchains:
        for family in t244.FAMILIES:
            warm_entries.extend(
                warm_rows(warm, family, toolchain, warm_plan_means.get(family, 100.0))
            )
    if warm_mutator:
        warm_mutator(warm_entries)
    write_report(
        round_dir / "results" / f"{NAMESPACE}.{TYPE_WARM}-report-full.json", warm_entries
    )
    if raw_hit_counts is not None:
        (round_dir / "raw-hits.json").write_text(json.dumps(raw_hit_counts), encoding="utf-8")
    return round_dir


def build_phase(root, rounds=2, **kwargs):
    root = Path(root)
    for index in range(1, rounds + 1):
        write_round(root / f"round-{index:02d}", **kwargs)
    return root


def run_main(argv):
    out, err = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        code = t244.main(argv)
    return code, out.getvalue(), err.getvalue()


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


class T244TestCase(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory(prefix="t244-evidence-test-")
        self.tmp = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def evaluate(
        self,
        *,
        baseline_kwargs=None,
        candidate_kwargs=None,
        tier="final",
        rounds=2,
    ):
        baseline = build_phase(self.tmp / "baseline", rounds, **(baseline_kwargs or {}))
        candidate = build_phase(self.tmp / "candidate", rounds, **(candidate_kwargs or {}))
        output = self.tmp / "evaluation.json"
        code, stdout, stderr = run_main(
            [
                "evaluate",
                "--baseline",
                str(baseline),
                "--candidate",
                str(candidate),
                "--output",
                str(output),
                "--tier",
                tier,
            ]
        )
        return code, read(output), stdout + stderr


class DivisorTests(T244TestCase):
    def test_divisors_are_per_class(self):
        self.assertEqual(t244.CLASS_DIVISORS[TYPE_CACHED], 10)
        self.assertEqual(t244.CLASS_DIVISORS[TYPE_FAIR], 10)
        self.assertEqual(t244.CLASS_DIVISORS[TYPE_WARM], 100)

    def test_fair_cached_bytes_are_divided_by_ten(self):
        code, payload, log = self.evaluate()
        self.assertEqual(code, 0, log)
        cte = next(row for row in payload["rows"] if row["scenario"] == "Cte")
        self.assertEqual(cte["divisor"], 10)
        self.assertEqual(cte["rounds"][0]["nextorm_bytes_per_query"], 100.0)
        self.assertEqual(cte["rounds"][0]["linq2db_bytes_per_query"], 100.0)
        self.assertEqual(cte["rounds"][0]["allocation_ratio"], 1.0)

    def test_warm_decompose_bytes_are_divided_by_hundred(self):
        round_dir = write_round(self.tmp / "round-01")
        output = self.tmp / "attribution.json"
        code, _, log = run_main(
            ["attribute", "--round", str(round_dir), "--output", str(output)]
        )
        self.assertEqual(code, 0, log)
        payload = read(output)
        family = payload["families"][0]
        construct = next(p for p in family["phases"] if p["phase"] == "Construct")
        self.assertEqual(construct["bytes_per_query"], 6900.0)  # 690000 raw / 100
        self.assertEqual(payload["divisor"], 100)

    def test_divisor_contract_pins_the_table(self):
        contract = t244.verify_divisor_contract(t244.DEFAULT_REPO, {TYPE_WARM})
        self.assertEqual(contract[TYPE_WARM]["divisor"], 100)
        self.assertEqual(contract[TYPE_WARM]["iterations"], 100)
        with mock.patch.dict(t244.CLASS_DIVISORS, {TYPE_WARM: 10}):
            with self.assertRaises(t244.Reject):
                t244.verify_divisor_contract(t244.DEFAULT_REPO, {TYPE_WARM})


class MalformedEvidenceTests(T244TestCase):
    def test_missing_comparator_rejected(self):
        def drop(report_rows):
            report_rows[:] = [
                entry
                for entry in report_rows
                if not (entry["Method"] == "B_Linq2Db_Intersect" and entry_toolchain(entry) == "Default")
                and not (
                    entry["Method"] == "B_Linq2Db_Intersect"
                    and entry_toolchain(entry) == "InProcessEmitToolchain"
                )
            ]

        code, payload, log = self.evaluate(candidate_kwargs={"cached_mutator": drop})
        self.assertEqual(code, 1, log)
        self.assertIn("missing comparator", payload["error"])
        self.assertEqual(payload["status"], "malformed")

    def test_duplicate_row_rejected(self):
        def duplicate(report_rows):
            report_rows.append(
                row(TYPE_CACHED, "B_Nextorm_Cached_Cte", 1000, 100.0, "Default")
            )

        code, payload, log = self.evaluate(candidate_kwargs={"cached_mutator": duplicate})
        self.assertEqual(code, 1, log)
        self.assertIn("duplicate", payload["error"])

    def test_missing_job_rejected(self):
        def wrong_job(report_rows):
            for entry in report_rows:
                if entry["Method"] == "B_Nextorm_Cached_Cte" and entry_toolchain(entry) == "Default":
                    entry["DisplayInfo"] = display(
                        TYPE_CACHED, entry["Method"], "Default", job="LongRun"
                    )

        code, payload, log = self.evaluate(candidate_kwargs={"cached_mutator": wrong_job})
        self.assertEqual(code, 1, log)
        self.assertIn("job", payload["error"].lower())

    def test_missing_toolchain_row_is_malformed(self):
        # Candidate has only the InProcess job; the Default comparison cannot be paired.
        code, payload, log = self.evaluate(
            candidate_kwargs={"toolchains": ("InProcessEmitToolchain",)}
        )
        self.assertEqual(code, 1, log)
        self.assertEqual(payload["status"], "malformed")

    def test_missing_job_specific_comparator_rejected(self):
        def drop_inprocess_reference(report_rows):
            report_rows[:] = [
                entry
                for entry in report_rows
                if not (
                    entry["Method"] == "B_Linq2Db_RecursiveCte"
                    and entry_toolchain(entry) == "InProcessEmitToolchain"
                )
            ]

        code, payload, log = self.evaluate(
            candidate_kwargs={"cached_mutator": drop_inprocess_reference}
        )
        self.assertEqual(code, 1, log)
        self.assertIn("missing comparator", payload["error"])
        self.assertIn("InProcessEmitToolchain", payload["error"])

    def test_na_allocation_rejected(self):
        def make_na(report_rows):
            for entry in report_rows:
                if entry["Method"] == "B_Nextorm_Cached_Cte" and entry_toolchain(entry) == "Default":
                    entry["Memory"]["BytesAllocatedPerOperation"] = None

        code, payload, log = self.evaluate(candidate_kwargs={"cached_mutator": make_na})
        self.assertEqual(code, 1, log)
        self.assertIn("NA/null allocation", payload["error"])

    def test_string_na_allocation_rejected(self):
        def make_na(report_rows):
            for entry in report_rows:
                if entry["Method"] == "B_Linq2Db_Cte" and entry_toolchain(entry) == "Default":
                    entry["Memory"]["BytesAllocatedPerOperation"] = "NA"

        code, payload, log = self.evaluate(candidate_kwargs={"cached_mutator": make_na})
        self.assertEqual(code, 1, log)
        self.assertIn("NA/null allocation", payload["error"])

    def test_non_one_operations_per_invoke_rejected(self):
        def set_opi(report_rows):
            for entry in report_rows:
                if entry["Method"] == "B_Linq2Db_Cte" and entry_toolchain(entry) == "Default":
                    entry["OperationsPerInvoke"] = 10

        code, payload, log = self.evaluate(candidate_kwargs={"cached_mutator": set_opi})
        self.assertEqual(code, 1, log)
        self.assertIn("OperationsPerInvoke", payload["error"])

    def test_missing_round_validity_rejected(self):
        baseline = build_phase(self.tmp / "baseline")
        candidate = build_phase(self.tmp / "candidate")
        (candidate / "round-02" / "round-validity.json").unlink()
        output = self.tmp / "evaluation.json"
        code, _, log = run_main(
            [
                "evaluate",
                "--baseline",
                str(baseline),
                "--candidate",
                str(candidate),
                "--output",
                str(output),
            ]
        )
        self.assertEqual(code, 1, log)
        self.assertIn("round-validity.json", read(output)["error"])

    def test_too_few_valid_rounds_rejected(self):
        baseline = build_phase(self.tmp / "baseline")
        candidate = build_phase(self.tmp / "candidate")
        write_round(candidate / "round-02", valid=False, reasons=["external overlap"])
        output = self.tmp / "evaluation.json"
        code, _, log = run_main(
            [
                "evaluate",
                "--baseline",
                str(baseline),
                "--candidate",
                str(candidate),
                "--output",
                str(output),
            ]
        )
        self.assertEqual(code, 1, log)
        payload = read(output)
        self.assertIn("valid round", payload["error"])

    def test_unparsable_report_rejected(self):
        baseline = build_phase(self.tmp / "baseline")
        candidate = build_phase(self.tmp / "candidate")
        broken = (
            candidate
            / "round-01"
            / "results"
            / f"{NAMESPACE}.{TYPE_FAIR}-report-full.json"
        )
        broken.write_text("{not json", encoding="utf-8")
        output = self.tmp / "evaluation.json"
        code, _, log = run_main(
            [
                "evaluate",
                "--baseline",
                str(baseline),
                "--candidate",
                str(candidate),
                "--output",
                str(output),
            ]
        )
        self.assertEqual(code, 1, log)

    def test_missing_warm_phase_rejected_in_attribution(self):
        def drop_prepare(warm_entries):
            warm_entries[:] = [
                entry
                for entry in warm_entries
                if not (
                    entry["Method"] == "Cte_Prepare_Hash"
                    and entry_toolchain(entry) == "Default"
                )
            ]

        round_dir = write_round(self.tmp / "round-01", warm_mutator=drop_prepare)
        output = self.tmp / "attribution.json"
        code, _, log = run_main(
            ["attribute", "--round", str(round_dir), "--output", str(output)]
        )
        self.assertEqual(code, 1, log)
        self.assertIn("missing phase", read(output)["error"])

    def test_missing_reference_rejected_in_attribution(self):
        round_dir = write_round(self.tmp / "round-01")
        cached = round_dir / "results" / f"{NAMESPACE}.{TYPE_CACHED}-report-full-compressed.json"
        cached.unlink()
        output = self.tmp / "attribution.json"
        code, _, log = run_main(
            ["attribute", "--round", str(round_dir), "--output", str(output)]
        )
        self.assertEqual(code, 1, log)
        self.assertIn("missing same-round linq2db reference", read(output)["error"])


class VerdictTests(T244TestCase):
    def test_final_tier_passes_when_both_cte_rows_are_one(self):
        code, payload, log = self.evaluate(tier="final")
        self.assertEqual(code, 0, log)
        self.assertEqual(payload["tier_status"], "final")
        self.assertEqual(payload["status"], "passed")
        self.assertEqual(payload["unresolved_final"], [])

    def test_checkpoint_is_not_final(self):
        code, payload, log = self.evaluate(
            candidate_kwargs={"cte": (110.0, 100.0), "recursive": (105.0, 100.0)},
            tier="final",
        )
        self.assertEqual(code, 2, log)
        self.assertEqual(payload["tier_status"], "checkpoint")
        self.assertEqual(payload["status"], "unmet")
        self.assertEqual(set(payload["unresolved_final"]), {"R244-01", "R244-02"})

    def test_checkpoint_tier_passes_at_one_point_one(self):
        code, payload, log = self.evaluate(
            candidate_kwargs={"cte": (110.0, 100.0), "recursive": (120.0, 100.0)},
            tier="checkpoint",
        )
        self.assertEqual(code, 0, log)
        self.assertEqual(payload["tier_status"], "checkpoint")
        self.assertEqual(payload["status"], "passed")

    def test_one_failing_cte_row_blocks_checkpoint(self):
        code, payload, log = self.evaluate(
            candidate_kwargs={"cte": (100.0, 100.0), "recursive": (121.0, 100.0)},
            tier="checkpoint",
        )
        self.assertEqual(code, 2, log)
        self.assertEqual(payload["tier_status"], "fail")
        recursive = next(row for row in payload["rows"] if row["scenario"] == "RecursiveCte")
        self.assertEqual(recursive["tier_status"], "fail")
        self.assertTrue(any("R244-02" in failure for failure in payload["failures"]))

    def test_allocation_failure_persists_across_rounds(self):
        baseline = build_phase(self.tmp / "baseline")
        candidate = self.tmp / "candidate"
        write_round(candidate / "round-01", cte=(100.0, 100.0))
        write_round(candidate / "round-02", cte=(130.0, 100.0))
        output = self.tmp / "evaluation.json"
        code, _, log = run_main(
            [
                "evaluate",
                "--baseline",
                str(baseline),
                "--candidate",
                str(candidate),
                "--output",
                str(output),
                "--tier",
                "checkpoint",
            ]
        )
        self.assertEqual(code, 2, log)
        payload = read(output)
        cte = next(row for row in payload["rows"] if row["scenario"] == "Cte")
        self.assertEqual(
            sorted({entry["round"] for entry in cte["rounds"]}), ["round-01", "round-02"]
        )
        self.assertEqual(cte["worst_allocation_ratio"], 1.3)
        self.assertEqual(cte["tier_status"], "fail")

    def test_nonzero_warm_reused_fails(self):
        warm = default_warm()
        warm["Warm_Reused"] = 100.0
        code, payload, log = self.evaluate(candidate_kwargs={"warm": warm})
        self.assertEqual(code, 2, log)
        self.assertTrue(any("R244-05" in failure for failure in payload["failures"]))
        reused = [entry for entry in payload["warm_reused"] if entry["family"] == "Cte"]
        self.assertTrue(reused)
        self.assertFalse(any(entry["ok"] for entry in reused))

    def test_prepared_control_regression_fails(self):
        code, payload, log = self.evaluate(
            candidate_kwargs={"prepared": (1040, 100.0)}
        )
        self.assertEqual(code, 2, log)
        self.assertTrue(any("R244-06" in failure for failure in payload["failures"]))

    def test_prepared_control_within_tolerance_passes(self):
        code, payload, log = self.evaluate(
            candidate_kwargs={"prepared": (1030, 103.0)}
        )
        self.assertEqual(code, 0, log)
        self.assertTrue(all(entry["ok"] for entry in payload["prepared_controls"]))

    def test_category_b_regression_above_baseline_fails(self):
        code, payload, log = self.evaluate(
            candidate_kwargs={"other": (101.0, 100.0)}
        )
        self.assertEqual(code, 2, log)
        self.assertTrue(any("R244-07" in failure for failure in payload["failures"]))

    def test_exit_code_semantics(self):
        self.assertEqual(t244.EXIT_OK, 0)
        self.assertEqual(t244.EXIT_MALFORMED, 1)
        self.assertEqual(t244.EXIT_NUMERIC, 2)
        code, payload, _ = self.evaluate()
        self.assertEqual(code, 0)
        code, _, _ = self.evaluate(candidate_kwargs={"cte": (150.0, 100.0)}, tier="checkpoint")
        self.assertEqual(code, 2)
        code, _, _ = self.evaluate(candidate_kwargs={"other": (1.0, 0.0)})
        self.assertEqual(code, 1)


class WarmTimingTests(T244TestCase):
    def test_median_of_three_settles_a_boundary_straddle(self):
        baseline = build_phase(self.tmp / "baseline")
        candidate = self.tmp / "candidate"
        write_round(candidate / "round-01", warm_plan_means={"Cte": 118.0})
        write_round(candidate / "round-02", warm_plan_means={"Cte": 125.0})
        output = self.tmp / "evaluation.json"
        code, _, log = run_main(
            [
                "evaluate",
                "--baseline",
                str(baseline),
                "--candidate",
                str(candidate),
                "--output",
                str(output),
            ]
        )
        self.assertEqual(code, 2, log)
        payload = read(output)
        cte = next(entry for entry in payload["timing"] if entry["family"] == "Cte" and entry["arm"] == "WarmDecompose")
        self.assertEqual(cte["rule"], "needs-third-round")
        self.assertIsNone(cte["ok"])
        self.assertTrue(any("inconclusive" in failure for failure in payload["failures"]))

    def test_third_round_uses_median_of_three(self):
        baseline = build_phase(self.tmp / "baseline")
        candidate = self.tmp / "candidate"
        write_round(candidate / "round-01", warm_plan_means={"Cte": 118.0})
        write_round(candidate / "round-02", warm_plan_means={"Cte": 125.0})
        write_round(candidate / "round-03", warm_plan_means={"Cte": 115.0})
        output = self.tmp / "evaluation.json"
        code, _, log = run_main(
            [
                "evaluate",
                "--baseline",
                str(baseline),
                "--candidate",
                str(candidate),
                "--output",
                str(output),
            ]
        )
        self.assertEqual(code, 0, log)
        payload = read(output)
        cte = next(entry for entry in payload["timing"] if entry["family"] == "Cte" and entry["arm"] == "WarmDecompose")
        self.assertEqual(cte["rule"], "median-of-three")
        self.assertAlmostEqual(cte["median"], 1.18)
        self.assertTrue(cte["ok"])

    def test_every_round_outside_band_fails(self):
        baseline = build_phase(self.tmp / "baseline")
        candidate = self.tmp / "candidate"
        write_round(candidate / "round-01", warm_plan_means={"Cte": 125.0})
        write_round(candidate / "round-02", warm_plan_means={"Cte": 130.0})
        output = self.tmp / "evaluation.json"
        code, _, log = run_main(
            [
                "evaluate",
                "--baseline",
                str(baseline),
                "--candidate",
                str(candidate),
                "--output",
                str(output),
            ]
        )
        self.assertEqual(code, 2, log)
        payload = read(output)
        cte = next(entry for entry in payload["timing"] if entry["family"] == "Cte" and entry["arm"] == "WarmDecompose")
        self.assertFalse(cte["ok"])
        self.assertTrue(any("+20%" in failure or "timing ratio" in failure for failure in payload["failures"]))


class AttributionTests(T244TestCase):
    def test_residual_subtracts_only_correctly_nested_phases(self):
        round_dir = write_round(self.tmp / "round-01")
        output = self.tmp / "attribution.json"
        code, _, log = run_main(
            ["attribute", "--round", str(round_dir), "--output", str(output)]
        )
        self.assertEqual(code, 0, log)
        payload = read(output)
        family = payload["families"][0]
        # Construct 6900, PlanKey 0, end-to-end 7000 => residual 100.  Subtracting the
        # (non-nested) Prepare_Hash 9000 would make this negative and be rejected.
        self.assertAlmostEqual(family["residual_bytes_per_query"], 100.0)
        self.assertEqual(family["accounting"]["subtracted"], ["Construct", "PlanKey"])
        self.assertIn("Prepare_Hash", family["accounting"]["excluded_as_not_nested"])
        phase_names = [phase["phase"] for phase in family["phases"]]
        for name in ("Construct", "Prepare_NoHash", "Prepare_Hash", "Warm_PlanOnly", "RawHit", "PlanKey", "end-to-end"):
            self.assertIn(name, phase_names)
        prepare_hash = next(p for p in family["phases"] if p["phase"] == "Prepare_Hash")
        self.assertTrue(prepare_hash["inclusive"])
        self.assertFalse(prepare_hash["nested_in_warm"])

    def test_double_counted_nesting_is_rejected(self):
        warm = default_warm()
        warm["Construct"] = 7200.0  # Construct + PlanKey exceeds end-to-end 7000
        round_dir = write_round(self.tmp / "round-01", warm=warm)
        output = self.tmp / "attribution.json"
        code, _, log = run_main(
            ["attribute", "--round", str(round_dir), "--output", str(output)]
        )
        self.assertEqual(code, 1, log)
        self.assertIn("inconsistent nesting", read(output)["error"])

    def test_phase_ordering_violation_is_rejected(self):
        warm = default_warm()
        warm["Prepare_NoHash"] = 4000.0  # smaller than Construct 6900
        round_dir = write_round(self.tmp / "round-01", warm=warm)
        output = self.tmp / "attribution.json"
        code, _, log = run_main(
            ["attribute", "--round", str(round_dir), "--output", str(output)]
        )
        self.assertEqual(code, 1, log)
        self.assertIn("inconsistent phase ordering", read(output)["error"])

    def test_residual_above_bound_flags_re_gather(self):
        warm = default_warm()
        warm["Warm_PlanOnly"] = 20000.0
        warm["Construct"] = 5000.0
        round_dir = write_round(self.tmp / "round-01", warm=warm)
        output = self.tmp / "attribution.json"
        code, _, log = run_main(
            ["attribute", "--round", str(round_dir), "--output", str(output)]
        )
        self.assertEqual(code, 2, log)
        payload = read(output)
        self.assertEqual(payload["status"], "bounded-re-gather-required")
        self.assertEqual(payload["residual"]["flag"], "bounded-re-gather-required")
        family = payload["families"][0]
        self.assertGreater(family["residual_bytes_per_query"], family["residual_bound"])

    def test_ranking_is_per_row_and_uses_the_eligibility_rule(self):
        warm = default_warm()
        warm["Construct"] = 4000.0
        warm["Warm_PlanOnly"] = 4200.0
        warm["Prepare_NoHash"] = 4500.0
        warm["Prepare_Hash"] = 4600.0
        round_dir = write_round(self.tmp / "round-01", warm=warm)
        output = self.tmp / "attribution.json"
        code, _, log = run_main(
            ["attribute", "--round", str(round_dir), "--output", str(output)]
        )
        self.assertEqual(code, 0, log)
        payload = read(output)
        rows = [entry for entry in payload["ranking"] if entry["family"] == "Cte"]
        self.assertTrue(all(entry["frozen"] is False for entry in rows))
        for entry in rows:
            self.assertGreaterEqual(
                entry["removable_bytes_per_query"],
                min(t244.ELIGIBLE_ABS_BYTES, t244.ELIGIBLE_FRACTION * entry["gap_bytes_per_query"]),
            )

    def test_raw_hit_counts_unavailable_are_never_inferred(self):
        round_dir = write_round(self.tmp / "round-01")
        output = self.tmp / "attribution.json"
        code, _, log = run_main(
            ["attribute", "--round", str(round_dir), "--output", str(output)]
        )
        self.assertEqual(code, 0, log)
        payload = read(output)
        self.assertEqual(payload["raw_hits"]["note"], "raw-hit-count: unavailable")
        self.assertEqual(payload["families"][0]["raw_hit_count"], "unavailable")

    def test_raw_hit_counts_are_recorded_when_present(self):
        round_dir = write_round(
            self.tmp / "round-01",
            raw_hit_counts={
                "Cte": {"hits": 3, "misses": 0},
                "RecursiveCte": {"hits": 3, "misses": 0},
            },
        )
        output = self.tmp / "attribution.json"
        code, _, log = run_main(
            ["attribute", "--round", str(round_dir), "--output", str(output)]
        )
        self.assertEqual(code, 0, log)
        payload = read(output)
        self.assertTrue(payload["raw_hits"]["available"])
        self.assertEqual(payload["families"][0]["raw_hit_count"], {"hits": 3, "misses": 0})

    def test_malformed_raw_hit_counts_rejected(self):
        round_dir = write_round(
            self.tmp / "round-01", raw_hit_counts={"Cte": {"hits": "three", "misses": 0}}
        )
        output = self.tmp / "attribution.json"
        code, _, log = run_main(
            ["attribute", "--round", str(round_dir), "--output", str(output)]
        )
        self.assertEqual(code, 1, log)


class ArtifactStagingTests(T244TestCase):
    def test_only_new_outputs_are_staged(self):
        root = self.tmp / "artifacts"
        results = root / "results"
        results.mkdir(parents=True)
        stale = results / f"{NAMESPACE}.{TYPE_CACHED}-report-full-compressed.json"
        write_report(stale, [row(TYPE_CACHED, "B_Nextorm_Cached_Cte", 9999, 1.0)])
        before = t244.inventory_artifacts(root)
        fresh = results / f"{NAMESPACE}.{TYPE_WARM}-report-full.json"
        write_report(fresh, [row(TYPE_WARM, "Cte_Construct", 100, 1.0)])
        staged_dir = self.tmp / "1-warm-decompose"
        staged, after, unchanged = t244.stage_invocation_artifacts(root, before, staged_dir)
        staged_names = {entry["rel"] for entry in staged}
        self.assertIn(f"results/{fresh.name}", staged_names)
        self.assertNotIn(f"results/{stale.name}", staged_names)
        self.assertFalse((staged_dir / "results" / stale.name).exists())
        self.assertIn(f"results/{stale.name}", unchanged)
        self.assertIn(f"results/{stale.name}", after)

    def test_rewritten_file_counts_as_this_invocations_output(self):
        root = self.tmp / "artifacts"
        root.mkdir(parents=True)
        target = root / "report.csv"
        target.write_text("old", encoding="utf-8")
        before = t244.inventory_artifacts(root)
        target.write_text("new content", encoding="utf-8")
        staged, _, _ = t244.stage_invocation_artifacts(root, before, self.tmp / "staged")
        self.assertEqual([entry["rel"] for entry in staged], ["report.csv"])

    def test_empty_inventory_and_no_new_outputs(self):
        root = self.tmp / "artifacts"
        root.mkdir()
        before = t244.inventory_artifacts(root)
        staged, _, _ = t244.stage_invocation_artifacts(root, before, self.tmp / "staged")
        self.assertEqual(staged, [])

    def test_validate_staged_report_rejects_mixed_class(self):
        staged = self.tmp / "1-faircached"
        write_report(
            staged / "results" / f"{NAMESPACE}.{TYPE_CACHED}-report-full.json",
            [row(TYPE_WARM, "Cte_Construct", 100, 1.0)],
        )
        with self.assertRaises(t244.Reject) as ctx:
            t244.validate_staged_report(staged, TYPE_CACHED)
        self.assertIn("mixed artifacts", str(ctx.exception))

    def test_validate_staged_report_rejects_missing_arm(self):
        staged = self.tmp / "1-warm-decompose"
        entries = warm_rows(default_warm(), "Cte") + warm_rows(default_warm(), "RecursiveCte")
        entries = [entry for entry in entries if entry["Method"] != "RecursiveCte_Warm_Reused"]
        write_report(staged / "results" / "warm-report.json", entries)
        with self.assertRaises(t244.Reject) as ctx:
            t244.validate_staged_report(staged, TYPE_WARM)
        self.assertIn("missing reference", str(ctx.exception))

    def test_validate_staged_report_rejects_stale_only_directory(self):
        staged = self.tmp / "1-faircached"
        staged.mkdir(parents=True)
        with self.assertRaises(t244.Reject):
            t244.validate_staged_report(staged, TYPE_CACHED)


class LockAndProcessTests(T244TestCase):
    def test_lock_is_exclusive_and_reusable_after_release(self):
        lock_path = self.tmp / "host.lock"
        first = t244.acquire_lock(lock_path, timeout=0.0)
        try:
            with self.assertRaises(t244.MeasureRefused):
                t244.acquire_lock(lock_path, timeout=0.0)
        finally:
            first.release()
        second = t244.acquire_lock(lock_path, timeout=0.0)
        second.release()
        self.assertEqual(second.record()["path"], str(lock_path))

    def test_inherited_lock_is_not_reacquired(self):
        lock_path = self.tmp / "host.lock"
        lock_path.write_text("", encoding="utf-8")
        fd = os.open(str(lock_path), os.O_RDWR)
        try:
            with mock.patch.object(t244, "inherited_lock_fd", return_value=fd):
                lock = t244.acquire_lock(lock_path, timeout=0.0)
            self.assertTrue(lock.inherited)
            self.assertEqual(lock.record()["mode"], "inherited flock -x")
        finally:
            os.close(fd)

    def test_conflicting_workload_is_detected_but_own_chain_is_not(self):
        snapshot = [
            {"pid": os.getpid(), "ppid": 1, "args": "python3 scripts/perf/t244_evidence.py measure"},
            {"pid": 999999, "ppid": 1, "args": "dotnet run --project benchmarks/nextorm.benchmark"},
            {"pid": 999998, "ppid": 1, "args": "/usr/bin/bash -lc ls"},
        ]
        conflicts = t244.conflicting_processes(snapshot)
        self.assertEqual([entry["pid"] for entry in conflicts], [999999])


class DocsCheckTests(T244TestCase):
    def write_docs(self, repo, *, extra=""):
        for rel, token in t244.DOC_MAPPING:
            path = repo / rel
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(f"# {token}\n{extra}\n", encoding="utf-8")
        return repo

    def write_evaluation(self, tier):
        path = self.tmp / "evaluation.json"
        path.write_text(json.dumps({"tier_status": tier}), encoding="utf-8")
        return path

    def test_checkpoint_with_final_claim_is_inconsistent(self):
        repo = self.write_docs(self.tmp / "repo")
        (repo / "docs/specs/performance/benchmark-report.md").write_text(
            "cte: T244 is done. The final target met.\n", encoding="utf-8"
        )
        code, _, log = run_main(
            [
                "docs-check",
                "--repo",
                str(repo),
                "--evaluation",
                str(self.write_evaluation("checkpoint")),
            ]
        )
        self.assertEqual(code, 2, log)
        payload = read(self.tmp / "docs-check.json")
        self.assertFalse(payload["consistent"])
        self.assertTrue(payload["claim_hits"])

    def test_negated_wording_is_not_a_claim(self):
        repo = self.write_docs(self.tmp / "repo")
        (repo / "docs/specs/status/D244-warm-cte-1.md").write_text(
            "d244: must not mark T244/#244 finally done at checkpoint\n", encoding="utf-8"
        )
        code, _, log = run_main(
            [
                "docs-check",
                "--repo",
                str(repo),
                "--evaluation",
                str(self.write_evaluation("checkpoint")),
            ]
        )
        self.assertEqual(code, 0, log)
        self.assertTrue(read(self.tmp / "docs-check.json")["consistent"])

    def test_checkpoint_wording_with_final_measurement_is_inconsistent(self):
        repo = self.write_docs(self.tmp / "repo")
        (repo / "docs/specs/performance/acceptance-benchmarks.md").write_text(
            "cte: acceptance tier: checkpoint\n", encoding="utf-8"
        )
        code, _, log = run_main(
            [
                "docs-check",
                "--repo",
                str(repo),
                "--evaluation",
                str(self.write_evaluation("final")),
            ]
        )
        self.assertEqual(code, 2, log)

    def test_missing_doc_is_malformed(self):
        repo = self.write_docs(self.tmp / "repo")
        (repo / "docs/comparisons/benchmarks.md").unlink()
        code, _, log = run_main(
            [
                "docs-check",
                "--repo",
                str(repo),
                "--evaluation",
                str(self.write_evaluation("checkpoint")),
            ]
        )
        self.assertEqual(code, 1, log)
        self.assertIn("docs mapping target missing", read(self.tmp / "docs-check.json")["error"])


class InvariantsTests(T244TestCase):
    def make_repo(self):
        repo = self.tmp / "repo"
        files = list(t244.MANDATORY_FROZEN) + ["src/nextorm.core/Query/QueryCommand.cs"]
        for rel in files:
            path = repo / rel
            path.parent.mkdir(parents=True, exist_ok=True)
            extra = ""
            if rel.endswith("QueryCommand.cs"):
                extra = "private bool HasEverPrepared;\nprivate bool ShapeValuesStable;\n"
            path.write_text(f"// {rel}\n{extra}", encoding="utf-8")
        mapping = {
            "task": "T244",
            "frozen_regions": [
                {"path": rel, "sha256": t244.sha256_file(repo / rel), "reason": "frozen"}
                for rel in t244.MANDATORY_FROZEN
            ],
            "wip_markers": [
                {
                    "path": "src/nextorm.core/Query/QueryCommand.cs",
                    "marker": name,
                    "sha256": t244.sha256_file(repo / "src/nextorm.core/Query/QueryCommand.cs"),
                }
                for name in t244.MANDATORY_WIP_MARKERS
            ],
        }
        return repo, mapping

    def run_invariants(self, repo, mapping):
        map_path = self.tmp / "execution-map.json"
        map_path.write_text(json.dumps(mapping), encoding="utf-8")
        output = self.tmp / "invariants.json"
        code, _, log = run_main(
            [
                "invariants",
                "--repo",
                str(repo),
                "--execution-map",
                str(map_path),
                "--output",
                str(output),
            ]
        )
        return code, read(output), log

    def test_unchanged_regions_pass(self):
        repo, mapping = self.make_repo()
        code, payload, log = self.run_invariants(repo, mapping)
        self.assertEqual(code, 0, log)
        self.assertEqual(payload["status"], "preserved")

    def test_changed_region_fails_closed(self):
        repo, mapping = self.make_repo()
        target = repo / t244.MANDATORY_FROZEN[0]
        target.write_text("// changed\n", encoding="utf-8")
        code, payload, log = self.run_invariants(repo, mapping)
        self.assertEqual(code, 2, log)
        self.assertEqual(payload["status"], "changed")
        self.assertIn(t244.MANDATORY_FROZEN[0], payload["changed"])

    def test_missing_mandatory_region_is_malformed(self):
        repo, mapping = self.make_repo()
        mapping["frozen_regions"] = [
            entry for entry in mapping["frozen_regions"] if entry["path"] != t244.MANDATORY_FROZEN[1]
        ]
        code, payload, log = self.run_invariants(repo, mapping)
        self.assertEqual(code, 1, log)
        self.assertIn("mandatory frozen region", payload["error"])

    def test_missing_wip_marker_is_malformed(self):
        repo, mapping = self.make_repo()
        mapping["wip_markers"] = mapping["wip_markers"][:1]
        code, payload, log = self.run_invariants(repo, mapping)
        self.assertEqual(code, 1, log)
        self.assertIn("WIP marker", payload["error"])

    def test_deleted_frozen_file_is_malformed(self):
        repo, mapping = self.make_repo()
        (repo / t244.MANDATORY_FROZEN[0]).unlink()
        code, payload, log = self.run_invariants(repo, mapping)
        self.assertEqual(code, 1, log)
        self.assertEqual(payload["status"], "missing")


class MeasureTests(T244TestCase):
    class FakeLock:
        def __init__(self):
            self.released = False

        def record(self):
            return {
                "path": str(t244.LOCK_PATH),
                "mode": "flock(LOCK_EX)",
                "inherited": False,
                "acquired_at": "2026-10-17T00:00:00Z",
                "released_at": "2026-10-17T00:00:01Z" if self.released else None,
            }

        def release(self):
            self.released = True

    def fake_collect(self, *, exit_code=0, staged=None):
        state = {"head": "abc", "porcelain_sha256": "def", "dirty_paths": 0}
        return {
            "argv": ["dotnet"],
            "command_line": "cd repo && env -u NEXTORM_BENCH_FULL dotnet",
            "cwd": "repo",
            "started_at": "2026-10-17T00:00:00Z",
            "ended_at": "2026-10-17T00:00:01Z",
            "duration_s": 1.0,
            "exit_code": exit_code,
            "stdout_log": "console.log",
            "source_state_before": state,
            "source_state_after": state,
            "assembly_before": {"sha256": "aaa"},
            "assembly_after": {"sha256": "aaa"},
            "db_before": {"sha256": "ddd"},
            "db_after": {"sha256": "ddd"},
            "processes_before": [],
            "processes_after": [],
            "conflicts_before": [],
            "conflicts_after": [],
            "staged_artifacts": staged or [],
            "stale_artifacts": ["results/stale-report-full.json"],
            "inventory_size_after": 2,
        }

    def run_measure(self, *, collect, validation=None):
        output = self.tmp / "baseline" / "round-01"
        lock = self.FakeLock()
        patches = [
            mock.patch.object(t244, "acquire_lock", return_value=lock),
            mock.patch.object(t244, "child_environment", return_value=({}, {})),
            mock.patch.object(t244, "processes_snapshot", return_value=[]),
            mock.patch.object(t244, "source_state", return_value={"head": "abc", "porcelain_sha256": "def", "dirty_paths": 0}),
            mock.patch.object(t244, "resolve_db", return_value={"path": "db", "source": "test", "sha256": "ddd", "size": 1}),
            mock.patch.object(t244, "benchmark_assembly", return_value={"sha256": "aaa"}),
            mock.patch.object(t244, "host_identity", return_value={"node": "test"}),
            mock.patch.object(t244, "dotnet_info", return_value={"sdk_version": "10.0.100"}),
            mock.patch.object(t244, "collect_invocation", side_effect=collect),
        ]
        if validation is not None:
            patches.append(mock.patch.object(t244, "validate_staged_report", side_effect=validation))
        for patcher in patches:
            patcher.start()
            self.addCleanup(patcher.stop)
        code, stdout, stderr = run_main(
            [
                "measure",
                "--repo",
                str(self.tmp),
                "--phase",
                "baseline",
                "--round",
                "1",
                "--output",
                str(output),
            ]
        )
        return code, output, lock, stdout + stderr

    def test_failed_invocation_is_recorded_and_returns_2(self):
        code, output, lock, log = self.run_measure(collect=lambda *a, **k: self.fake_collect(exit_code=1))
        self.assertEqual(code, 2, log)
        self.assertTrue(lock.released)
        payload = read(output / "provenance.json")
        self.assertFalse(payload["validity"]["valid"])
        self.assertTrue(any("exited 1" in reason for reason in payload["validity"]["reasons"]))
        self.assertEqual(len(payload["invocations"]), 3)
        commands = (output / "commands.log").read_text(encoding="utf-8")
        self.assertIn("env -u NEXTORM_BENCH_FULL", commands)
        self.assertIn("--job short", commands)
        self.assertIn("--exporters json", commands)
        validity = read(output / "round-validity.json")
        self.assertFalse(validity["valid"])

    def test_unfavorable_but_structurally_valid_round_is_not_invalidated(self):
        def collect(*args, **kwargs):
            return self.fake_collect(staged=[{"rel": "results/x-report.json", "sha256": "s", "size": 1}])

        code, output, lock, log = self.run_measure(
            collect=collect, validation=lambda *args, **kwargs: {"methods": 1}
        )
        self.assertEqual(code, 0, log)
        self.assertTrue(lock.released)
        self.assertTrue(read(output / "round-validity.json")["valid"])


class ProviderFailureTests(unittest.TestCase):
    def test_phase_ordering_helper_rejects_mixed_rows(self):
        index = t244.job_identity(display(TYPE_CACHED, "B_Nextorm_Cached_Cte", "InProcessEmitToolchain"))
        self.assertEqual(index["job"], "ShortRun")
        self.assertEqual(index["toolchain"], "InProcessEmitToolchain")
        self.assertEqual(index["identity"], "ShortRun/InProcessEmitToolchain")

    def test_ratio_rejects_zero_denominator(self):
        self.assertIsNone(t244.safe_ratio(1.0, 0.0))
        self.assertEqual(t244.safe_ratio(0.0, 0.0), 1.0)

    def test_negative_residual_within_tolerance_is_zeroed(self):
        buckets = {"End": 100.0, "Construct": 101.0}
        signed = buckets["End"] - buckets["Construct"]
        self.assertGreaterEqual(signed, -max(16.0, 0.02 * buckets["End"]))
        self.assertEqual(max(0.0, signed), 0.0)


if __name__ == "__main__":
    unittest.main(verbosity=2)
