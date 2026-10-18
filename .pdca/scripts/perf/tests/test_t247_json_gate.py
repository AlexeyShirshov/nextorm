#!/usr/bin/env python3
"""Self-tests for .pdca/scripts/perf/t247_json_gate.py (stdlib unittest; no benchmark is run).

Run with either::

    python3 -m unittest discover -s .pdca/scripts/perf/tests -p 'test_t247_json_gate.py' -v
    python3 -m pytest .pdca/scripts/perf/tests/test_t247_json_gate.py -q

Every fixture is a synthetic evidence root mirroring the driver's own round artifacts
(``<root>/<phase>/round-<n>/round.json`` with a normalised ``cases`` list), so the real
pairing, threshold, intercept-fit, byte-control and acceptance paths are exercised without
executing BenchmarkDotNet.

Covered: ratio computation (pass/fail), the pinned 446095 B/op allocation cap (exact
boundary), a discordant round pair -> ``inconclusive`` (never an averaged pass), zero/empty
matches -> input error, the least-squares intercept fit (``a``/``b``/residuals and a
non-positive intercept -> fail), the seven-case acceptance inventory, and threshold
immutability (no threshold constant is reachable from the CLI).
"""
from __future__ import annotations

import argparse
import contextlib
import io
import json
import sys
import tempfile
import types
import unittest
from unittest import mock
from pathlib import Path

PERF_DIR = Path(__file__).resolve().parents[1]
REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(PERF_DIR))

import t247_json_gate as t247  # noqa: E402

WRITE = t247.WRITE_JSON_CLASS
FIXED = t247.FIXED_COST_CLASS
DTO = t247.DTO_STREAM_ARM
LINQ = t247.LINQ2DB_ARM
TOTAL = t247.PUBLIC_TOTAL_ARM
COMPILE = t247.COMPILE_ONLY_ARM
STEADY = t247.STEADY_WRITER_ARM

#: Synthetic final HEAD for fixtures.  The declared-diff provider is patched in the tests, so the
#: value only has to be stable across the two rounds of a phase.
FIXTURE_FINAL_HEAD = "d24702fixturefinalhead000000000000000000000000"


def patch_src_diff(case):
    """Patch the declared-diff provider so fixtures never depend on the live worktree."""
    patcher = mock.patch.object(
        t247,
        "SRC_DIFF_PROVIDER",
        lambda repo, base, head: (0, sorted(t247.DECLARED_T247_SRC_PATHS)),
    )
    patcher.start()
    case.addCleanup(patcher.stop)


def case(cls, method, rows, mean, allocated, state="Success"):
    return {
        "type": "NextORM.Benchmark.%s" % cls,
        "short_type": cls,
        "method": method,
        "display": "%s.%s: Default" % (cls, method),
        "job": "Default",
        "toolchain": "Default",
        "identity": "Default/Default",
        "mean_ns": mean,
        "allocated_b": allocated,
        "rows": rows,
        "state": state,
        "source": "fixture",
    }


def baseline_cases():
    return [
        case(WRITE, DTO, 1000, 100.0, 10_000),
        case(WRITE, LINQ, 1000, 100.0, 10_000),
        case(WRITE, DTO, 10000, 100.0, 100_000),
        case(WRITE, LINQ, 10000, 100.0, 100_000),
        case(WRITE, DTO, 100000, 100.0, 1_000_000),
        case(WRITE, LINQ, 100000, 100.0, 1_000_000),
        case(FIXED, TOTAL, 1, 1010.0, 100),
        case(FIXED, TOTAL, 10, 1100.0, 100),
        case(FIXED, TOTAL, 100, 2000.0, 100),
    ]


def final_cases(dto_1k=110.0, dto_10k=99.0, alloc_10k=90_000.0, total_100=900.0, total_1=405.0, total_10=450.0):
    return [
        case(WRITE, DTO, 1000, dto_1k, 10_000),
        case(WRITE, LINQ, 1000, 100.0, 10_000),
        case(WRITE, DTO, 10000, dto_10k, alloc_10k),
        case(WRITE, LINQ, 10000, 100.0, 100_000),
        case(WRITE, DTO, 100000, 100.0, 1_000_000),
        case(WRITE, LINQ, 100000, 100.0, 1_000_000),
        case(FIXED, TOTAL, 1, total_1, 100),
        case(FIXED, TOTAL, 10, total_10, 100),
        case(FIXED, TOTAL, 100, total_100, 100),
    ]


def phase_cases():
    return [
        case(FIXED, TOTAL, 100, 2000.0, 100),
        case(FIXED, COMPILE, 100, 1200.0, 100),
        case(FIXED, STEADY, 100, 300.0, 100),
    ]


BYTE_MARKER = (
    "R247_02_BYTE_CONTROL equal=true rows=1000 "
    "streamedSha256=AA materializedSha256=AA firstDiff=-1"
)


def write_round(root, phase, round_id, cases, byte_marker=True):
    round_dir = Path(root) / phase / round_id
    round_dir.mkdir(parents=True, exist_ok=True)
    invocations = []
    if byte_marker:
        console = round_dir / "public-json.console.log"
        console.write_text(BYTE_MARKER + "\n", encoding="utf-8")
        invocations.append({"tag": "public-json", "console_log": str(console), "exit_code": 0})
    head = t247.PAIRED_BASELINE_HEAD if phase == "baseline" else FIXTURE_FINAL_HEAD
    fingerprint = {"head": head, "dirty_paths": 0, "porcelain_sha256": "fixture"}
    record = {
        "round": round_id,
        "subcommand": phase,
        "valid": True,
        "reasons": [],
        "cases": cases,
        "invocations": invocations,
        "discovered_case_count": len(cases),
        "executed_case_count": len(cases),
        "fingerprint_before": fingerprint,
        "fingerprint_after": dict(fingerprint),
    }
    (round_dir / "round.json").write_text(json.dumps(record), encoding="utf-8")


def write_acceptance(root, rounds=("round-1", "round-2"), passing=True):
    out = Path(root) / "acceptance"
    out.mkdir(parents=True, exist_ok=True)
    entries = []
    for round_id in rounds:
        cases = [
            {"class": cls, "method": method, "present": True, "pass": passing, "ratio": 1.0}
            for cls, method in t247.ACCEPTANCE_CASES
        ]
        entries.append({"round": round_id, "case_count": 7, "cases": cases, "pass": passing})
    (out / "acceptance.json").write_text(json.dumps({"rounds": entries}), encoding="utf-8")


def good_fixture(root, **final_kwargs):
    write_round(root, "baseline", "round-1", baseline_cases())
    write_round(root, "baseline", "round-2", baseline_cases())
    write_round(root, "final", "round-1", final_cases(**final_kwargs))
    write_round(root, "final", "round-2", final_cases(**final_kwargs))
    write_round(root, "phase", "round-1", phase_cases(), byte_marker=False)
    write_round(root, "phase", "round-2", phase_cases(), byte_marker=False)
    write_acceptance(root)
    return Path(root)


def check(payload, check_id):
    for item in payload["checks"]:
        if item["id"] == check_id:
            return item
    raise AssertionError("no check %r in %s" % (check_id, [c["id"] for c in payload["checks"]]))


class CompareFixtureTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        patch_src_diff(self)

    def tearDown(self):
        self._tmp.cleanup()

    def test_good_fixture_passes(self):
        good_fixture(self.root)
        result = t247.evaluate_compare(self.root, REPO)
        self.assertEqual(result["status"], "pass", result)
        self.assertEqual(result["reason_code"], "ok")
        self.assertEqual(check(result, "R247-01-1k-ratio")["verdict"], "pass")
        self.assertEqual(check(result, "R247-01-10k-alloc")["verdict"], "pass")
        self.assertEqual(check(result, "R247-12-100row")["verdict"], "pass")
        self.assertEqual(check(result, "R247-12-intercept")["verdict"], "pass")
        self.assertEqual(check(result, "R247-02-byte-control")["verdict"], "pass")
        self.assertEqual(check(result, "R247-declared-src-diff")["verdict"], "pass")
        self.assertEqual(check(result, "R247-09-acceptance")["verdict"], "pass")

    def test_ratio_failure(self):
        good_fixture(self.root, dto_1k=200.0)
        result = t247.evaluate_compare(self.root, REPO)
        self.assertEqual(result["status"], "fail")
        self.assertEqual(result["reason_code"], "gate-fail")
        ratio = check(result, "R247-01-1k-ratio")
        self.assertEqual(ratio["verdict"], "fail")
        self.assertAlmostEqual(ratio["rounds"]["round-1"]["value"], 2.0)

    def test_allocation_cap_exact_boundary(self):
        cap = t247.THRESHOLDS["r247_01_alloc_10k_max_bytes"]
        self.assertEqual(cap, 446095)
        good_fixture(self.root, alloc_10k=446095.0)
        result = t247.evaluate_compare(self.root, REPO)
        self.assertEqual(check(result, "R247-01-10k-alloc")["verdict"], "pass")
        good_fixture(self.root, alloc_10k=446096.0)
        result = t247.evaluate_compare(self.root, REPO)
        self.assertEqual(check(result, "R247-01-10k-alloc")["verdict"], "fail")
        self.assertEqual(result["status"], "fail")

    def test_discordant_rounds_are_inconclusive(self):
        good_fixture(self.root)
        # Round 2 alone breaks the 1k ratio: one pass + one fail is never an averaged pass.
        write_round(self.root, "final", "round-2", final_cases(dto_1k=200.0))
        result = t247.evaluate_compare(self.root, REPO)
        self.assertEqual(check(result, "R247-01-1k-ratio")["verdict"], "inconclusive")
        self.assertEqual(result["status"], "inconclusive")
        self.assertEqual(result["reason_code"], "inconclusive-discordant-rounds")

    def test_missing_case_is_input_error(self):
        good_fixture(self.root)
        broken = [c for c in final_cases() if not (c["method"] == LINQ and c["rows"] == 100000)]
        write_round(self.root, "final", "round-1", broken)
        with self.assertRaises(t247.InputError):
            t247.evaluate_compare(self.root, REPO)

    def test_foreign_src_path_is_reported_as_declared_difference(self):
        good_fixture(self.root)
        foreign = mock.patch.object(
            t247,
            "SRC_DIFF_PROVIDER",
            lambda repo, base, head: (
                0,
                sorted(t247.DECLARED_T247_SRC_PATHS) + ["src/nextorm.core/Foreign.cs"],
            ),
        )
        with foreign:
            result = t247.evaluate_compare(self.root, REPO)
        declared = check(result, "R247-declared-src-diff")
        self.assertEqual(declared["verdict"], "fail")
        self.assertEqual(declared["rounds"]["round-1"]["foreign"], ["src/nextorm.core/Foreign.cs"])
        self.assertEqual(result["status"], "fail")

    def test_missing_acceptance_evidence_is_a_failing_check(self):
        good_fixture(self.root)
        (self.root / "acceptance" / "acceptance.json").unlink()
        result = t247.evaluate_compare(self.root, REPO)
        self.assertEqual(result["status"], "fail")
        acceptance = check(result, "R247-09-acceptance")
        self.assertEqual(acceptance["verdict"], "fail")
        self.assertEqual(acceptance["rounds"]["round-1"]["case_count"], 0)
        self.assertIn("never skipped", acceptance["detail"])

    def test_fewer_than_two_rounds_is_input_error(self):
        good_fixture(self.root)
        (self.root / "final" / "round-2" / "round.json").unlink()
        with self.assertRaises(t247.InputError):
            t247.evaluate_compare(self.root, REPO)

    def test_byte_control_mismatch_fails(self):
        good_fixture(self.root)
        marker = BYTE_MARKER.replace("equal=true", "equal=false")
        for round_id in ("round-1", "round-2"):
            (self.root / "baseline" / round_id / "public-json.console.log").write_text(
                marker + "\n", encoding="utf-8"
            )
        result = t247.evaluate_compare(self.root, REPO)
        self.assertEqual(check(result, "R247-02-byte-control")["verdict"], "fail")

    def test_intercept_nonpositive_fails(self):
        good_fixture(self.root, total_1=0.1, total_10=10.0, total_100=100.0)
        result = t247.evaluate_compare(self.root, REPO)
        fit = check(result, "R247-12-intercept")
        self.assertEqual(fit["verdict"], "fail")
        self.assertFalse(fit["rounds"]["round-1"]["both_intercepts_positive"])


class InterceptFitTests(unittest.TestCase):
    def test_exact_linear_points(self):
        fit = t247.linear_fit([(1, 1010.0), (10, 1100.0), (100, 2000.0)])
        self.assertAlmostEqual(fit["a"], 1000.0, places=6)
        self.assertAlmostEqual(fit["b"], 10.0, places=6)
        self.assertEqual(len(fit["residuals"]), 3)
        for residual in fit["residuals"]:
            self.assertAlmostEqual(residual, 0.0, places=6)

    def test_negative_intercept(self):
        fit = t247.linear_fit([(1, 0.0), (10, 10.0), (100, 100.0)])
        self.assertLess(fit["a"], 0.0)

    def test_degenerate_x_rejected(self):
        with self.assertRaises(t247.InputError):
            t247.linear_fit([(5, 1.0), (5, 2.0)])

    def test_too_few_points_rejected(self):
        with self.assertRaises(t247.InputError):
            t247.linear_fit([(1, 1.0)])


class CheckVerdictTests(unittest.TestCase):
    def test_all_pass(self):
        self.assertEqual(
            t247._check("x", "ratio", 1.0, {"r1": {"pass": True}, "r2": {"pass": True}})["verdict"],
            "pass",
        )

    def test_all_fail(self):
        self.assertEqual(
            t247._check("x", "ratio", 1.0, {"r1": {"pass": False}, "r2": {"pass": False}})["verdict"],
            "fail",
        )

    def test_discordant(self):
        self.assertEqual(
            t247._check("x", "ratio", 1.0, {"r1": {"pass": True}, "r2": {"pass": False}})["verdict"],
            "inconclusive",
        )


class CaseAccessTests(unittest.TestCase):
    def test_ratio_math(self):
        index = t247.index_cases(
            [case(WRITE, DTO, 1000, 150.0, 10), case(WRITE, LINQ, 1000, 100.0, 10)]
        )
        result = t247._ratio_check(index, index, WRITE, DTO, WRITE, LINQ, 1000, "round-1", 1.2)
        self.assertAlmostEqual(result["value"], 1.5)
        self.assertFalse(result["pass"])

    def test_zero_matched_cases_rejected(self):
        index = t247.index_cases([])
        with self.assertRaises(t247.InputError):
            t247.get_case(index, WRITE, DTO, 1000, "round-1")

    def test_missing_mean_and_allocation_rejected(self):
        index = t247.index_cases([case(WRITE, DTO, 1000, 0.0, None)])
        with self.assertRaises(t247.InputError):
            t247.case_mean(index, WRITE, DTO, 1000, "round-1")
        with self.assertRaises(t247.InputError):
            t247.case_alloc(index, WRITE, DTO, 1000, "round-1")

    def test_duplicate_case_rejected(self):
        with self.assertRaises(t247.InputError):
            t247.index_cases([case(WRITE, DTO, 1000, 1.0, 1), case(WRITE, DTO, 1000, 2.0, 2)])


class ThresholdImmutabilityTests(unittest.TestCase):
    def test_thresholds_are_a_frozen_mapping(self):
        self.assertIsInstance(t247.THRESHOLDS, types.MappingProxyType)
        with self.assertRaises(TypeError):
            t247.THRESHOLDS["r247_01_ratio_1k_max"] = 99.0

    def test_pinned_values(self):
        self.assertEqual(t247.THRESHOLDS["r247_01_ratio_1k_max"], 1.2)
        self.assertEqual(t247.THRESHOLDS["r247_01_ratio_10k_max"], 1.0)
        self.assertEqual(t247.THRESHOLDS["r247_01_alloc_10k_max_bytes"], 446095)
        self.assertEqual(t247.THRESHOLDS["r247_12_100row_fraction_max"], 0.50)
        self.assertEqual(t247.THRESHOLDS["r247_12_intercept_fraction_max"], 0.50)
        self.assertEqual(t247.THRESHOLDS["r247_09_acceptance_cases"], 7)
        self.assertEqual(t247.THRESHOLDS["r247_09_acceptance_max_wall_s"], 240)
        self.assertEqual(t247.THRESHOLDS["r247_09_acceptance_case_ratio_max"], 1.20)

    def test_no_cli_option_configures_a_threshold(self):
        parser = t247.build_parser()
        dests = {action.dest for action in parser._actions}
        for banned in ("threshold", "ratio", "alloc", "cap", "fraction", "budget"):
            for dest in dests:
                self.assertNotIn(banned, dest, "CLI option %r exposes a threshold" % dest)
        help_text = parser.format_help()
        self.assertNotIn("446095", help_text)
        self.assertNotIn("threshold", help_text.lower())

    def test_parser_rejects_threshold_flags(self):
        for flag in ("--ratio", "--threshold", "--alloc-cap"):
            with contextlib.redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit):
                    t247.build_parser().parse_args(["compare", flag, "1.0"])


class CommandTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        patch_src_diff(self)

    def tearDown(self):
        self._tmp.cleanup()

    def _args(self, **overrides):
        base = {"evidence_root": str(self.root), "repo": str(REPO), "output": None}
        base.update(overrides)
        return argparse.Namespace(**base)

    def test_compare_command_pass(self):
        good_fixture(self.root)
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = t247.cmd_compare(self._args())
        self.assertEqual(code, t247.EXIT_OK)
        comparison = json.loads((self.root / "compare" / "comparison.json").read_text(encoding="utf-8"))
        self.assertEqual(comparison["status"], "pass")
        self.assertTrue((self.root / "compare" / "small-row.json").exists())
        self.assertTrue((self.root / "compare" / "intercept-fit.json").exists())

    def test_compare_command_zero_match_is_input_error(self):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = t247.cmd_compare(self._args())
        self.assertEqual(code, t247.EXIT_INPUT)

    def test_compare_command_discordant_is_inconclusive(self):
        good_fixture(self.root)
        write_round(self.root, "final", "round-2", final_cases(dto_1k=200.0))
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = t247.cmd_compare(self._args())
        self.assertEqual(code, t247.EXIT_INCONCLUSIVE)

    def test_phases_command_attributes_and_labels_residual(self):
        write_round(self.root, "phase", "round-1", phase_cases(), byte_marker=False)
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = t247.cmd_phases(self._args())
        self.assertEqual(code, t247.EXIT_OK)
        payload = json.loads(
            (self.root / "phases" / "phase-attribution.json").read_text(encoding="utf-8")
        )
        self.assertEqual(payload["measured_compile"]["arm"], COMPILE)
        self.assertEqual(payload["steady_writer"]["arm"], STEADY)
        self.assertEqual(payload["public_total"]["arm"], TOTAL)
        self.assertAlmostEqual(payload["unattributed_residual"]["mean_ns"]["round-1"], 800.0)
        self.assertIn("un-attributed", payload["unattributed_residual"]["label"])
        self.assertIn("NOT an independently measured preparation", payload["unattributed_residual"]["label"])

    def test_phases_command_missing_compile_arm(self):
        write_round(self.root, "phase", "round-1", [case(FIXED, TOTAL, 100, 2000.0, 100)], byte_marker=False)
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = t247.cmd_phases(self._args())
        self.assertEqual(code, t247.EXIT_INPUT)

    def test_preflight_prerequisites_resolve(self):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = t247.cmd_preflight(self._args())
        payload = json.loads((self.root / "preflight" / "preflight.json").read_text(encoding="utf-8"))
        self.assertIn(payload["status"], ("pass", "input-error"))
        self.assertIn("tools", payload)
        self.assertIn("database", payload)
        if code == t247.EXIT_OK:
            self.assertEqual(payload["unresolved"], [])


class DuplicateGuardWrapperTests(unittest.TestCase):
    """The measurement wrapper refuses a duplicate launch before it queues for the host lock.

    Observed failure mode: a tool double-launch queued a second wrapper behind the host lock,
    which would have started a second full ``phase --rounds 2`` block and overwritten the
    evidence of the running block.  The wrapper now takes a NON-BLOCKING marker lock
    (``/tmp/opencode/t247-measure.guard``) before the blocking host lock.
    """

    def setUp(self):
        import t247_measure

        self.measure = t247_measure
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.guard = Path(self._tmp.name) / "t247-measure.guard"

    def test_second_wrapper_exits_90_before_any_driver_run(self):
        import fcntl
        import os

        holder = os.open(str(self.guard), os.O_RDWR | os.O_CREAT, 0o644)
        try:
            fcntl.flock(holder, fcntl.LOCK_EX | fcntl.LOCK_NB)
            runs = []
            stderr = io.StringIO()
            with mock.patch.dict(os.environ, {"T247_GUARD_LOCK": str(self.guard)}):
                with mock.patch.object(
                    self.measure.subprocess, "run", lambda *a, **k: runs.append(a)
                ):
                    with contextlib.redirect_stderr(stderr):
                        code = self.measure.main(["phase", "--rounds", "2"])
            self.assertEqual(code, 90)
            self.assertEqual(code, self.measure.DUPLICATE_EXIT)
            self.assertIn("T247-DUPLICATE-GUARD", stderr.getvalue())
            self.assertEqual(runs, [])  # the driver is never launched
        finally:
            os.close(holder)

    def test_marker_is_held_while_the_host_locked_driver_runs(self):
        calls = []

        def fake_run(argv, cwd=None, env=None):
            calls.append({"argv": list(argv), "cwd": cwd, "env": dict(env or {})})
            with self.assertRaises(self.measure.DuplicateMeasurement):
                self.measure.acquire_guard(self.guard)
            return types.SimpleNamespace(returncode=7)

        import os

        with mock.patch.dict(os.environ, {"T247_GUARD_LOCK": str(self.guard)}):
            with mock.patch.object(self.measure.subprocess, "run", fake_run):
                code = self.measure.main(["phase", "--rounds", "2", "--evidence-root", "x"])
        self.assertEqual(code, 7)  # the driver's own exit code is propagated
        self.assertEqual(len(calls), 1)
        argv = calls[0]["argv"]
        self.assertEqual(argv[:3], ["flock", "-x", str(self.measure.DEFAULT_HOST_LOCK)])
        self.assertIn("t247_json_gate.py", argv[4])
        self.assertNotIn(str(self.guard), " ".join(argv))  # marker never nested as a host lock
        self.assertEqual(calls[0]["env"]["NEXTORM_BENCH_FULL"], "1")

    def test_no_arguments_after_a_free_guard_is_usage_error(self):
        import os

        with mock.patch.dict(os.environ, {"T247_GUARD_LOCK": str(self.guard)}):
            stderr = io.StringIO()
            with contextlib.redirect_stderr(stderr):
                code = self.measure.main([])
        self.assertEqual(code, 2)
        self.assertIn("usage:", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
