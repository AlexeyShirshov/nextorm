#!/usr/bin/env python3
"""Self-tests for ``scripts/perf/d243_aggregate_gate.py`` (fixtures only; no benchmark run).

Run with either::

    python3 -m unittest discover -s scripts/perf/tests -p 'test_d243_aggregate_gate.py' -v
    python3 -m pytest scripts/perf/tests/test_d243_aggregate_gate.py -q

Each test builds a synthetic ``before``/``after`` artifact tree shaped like the real BDN
output (``<root>/four-<i>/results/NextORM.Benchmark.SqliteBenchmarkAggregates-report.csv``)
and drives the gate's real ``main()`` entry point, asserting its exit code and JSON payload.
The fixtures copy the archived ``before`` numbers (Count NextORM ``2.91 KB`` / LINQ
``2.8 KB``, Sum NextORM ``3.31 KB`` / LINQ ``3.41 KB``) so the red/green boundary is the
committed one.
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

import d243_aggregate_gate as gate  # noqa: E402

REPORT = "NextORM.Benchmark.SqliteBenchmarkAggregates-report.csv"
HEADER = "Method,Job,Mean,Allocated"


def arm_row(method, mean, allocated):
    return f"{method},ShortRun,{mean},{allocated}"


def write_round(
    root,
    round_name,
    *,
    count_alloc="2.91 KB",
    sum_alloc="3.31 KB",
    count_mean="18.40 \u03bcs",
    linq_count_mean="24.30 \u03bcs",
    sum_mean="280.00 \u03bcs",
    linq_sum_mean="292.00 \u03bcs",
):
    """Write one synthetic BDN round and return its directory."""
    round_dir = Path(root) / round_name / "results"
    round_dir.mkdir(parents=True, exist_ok=True)
    lines = [
        HEADER,
        arm_row("B_Nextorm_Cached_Count", count_mean, count_alloc),
        arm_row("B_Linq2Db_Count", linq_count_mean, "2.80 KB"),
        arm_row("B_Nextorm_Cached_Sum", sum_mean, sum_alloc),
        arm_row("B_Linq2Db_Sum", linq_sum_mean, "3.41 KB"),
    ]
    (round_dir / REPORT).write_text("\n".join(lines) + "\n", encoding="utf-8")
    return Path(root) / round_name


def write_tree(root, rounds=("four-1", "four-2", "four-3"), **overrides):
    for index, round_name in enumerate(rounds):
        kwargs = {key: value[index] if isinstance(value, (list, tuple)) else value
                  for key, value in overrides.items()}
        write_round(root, round_name, **kwargs)


def run_gate(before, after, output):
    """Drive ``main()``; return (exit code, parsed JSON, stdout+stderr text)."""
    buffer = io.StringIO()
    with contextlib.redirect_stdout(buffer), contextlib.redirect_stderr(buffer):
        code = gate.main(
            [
                "--before", str(before),
                "--after", str(after),
                "--output", str(output),
                "--summary", str(Path(output).with_suffix(".md")),
            ]
        )
    payload = json.loads(Path(output).read_text(encoding="utf-8"))
    return code, payload, buffer.getvalue()


class GreenFixtureTests(unittest.TestCase):
    def test_vanilla_green_fixture_exits_zero(self):
        with tempfile.TemporaryDirectory() as tmp:
            before = Path(tmp) / "before"
            after = Path(tmp) / "after"
            write_tree(before)
            write_tree(after, count_alloc="2.70 KB", sum_alloc="3.30 KB")
            code, payload, text = run_gate(before, after, Path(tmp) / "gate.json")

            self.assertEqual(code, 0, text)
            self.assertEqual(payload["status"], "pass")
            self.assertEqual(payload["violations"], [])
            self.assertEqual(len(payload["rows"]), 6)  # 2 terminals x 3 rounds
            self.assertEqual(len(payload["time_rows"]), 2)
            for row in payload["rows"]:
                self.assertTrue(row["pass"], row)
                self.assertAlmostEqual(row["slack_b"], row["threshold_b"] - row["bytes_per_op"])
                # The display window is reported, never an invented integer.
                self.assertAlmostEqual(row["quantization_window_b"], 5.12)
                self.assertNotEqual(row["bytes_per_op"], int(row["bytes_per_op"]))
            # Count and Sum are budgeted separately.
            self.assertEqual({row["terminal"]: row["threshold_b"] for row in payload["rows"]},
                             {"Count": gate.THRESHOLDS_B["Count"], "Sum": gate.THRESHOLDS_B["Sum"]})


class AllocationFailureTests(unittest.TestCase):
    def test_single_count_row_over_budget_exits_one_and_names_the_round(self):
        with tempfile.TemporaryDirectory() as tmp:
            before = Path(tmp) / "before"
            after = Path(tmp) / "after"
            write_tree(before)
            # 3.00 KB = 3072 B/op -> over the 2864 B Count budget in four-2 only.
            write_tree(after, count_alloc=("2.70 KB", "3.00 KB", "2.70 KB"), sum_alloc="3.30 KB")
            code, payload, text = run_gate(before, after, Path(tmp) / "gate.json")

            self.assertEqual(code, 1, text)
            self.assertEqual(payload["status"], "fail")
            self.assertEqual(len(payload["violations"]), 1, payload["violations"])
            violation = payload["violations"][0]
            self.assertEqual(violation["kind"], "allocation")
            self.assertEqual(violation["terminal"], "Count")
            self.assertEqual(violation["round"], "four-2")
            self.assertAlmostEqual(violation["bytes_per_op"], 3072.0)
            self.assertAlmostEqual(violation["over_by_b"], 208.0)
            self.assertIn("four-2", text)
            failing = [row for row in payload["rows"] if not row["pass"]]
            self.assertEqual([(r["terminal"], r["round"]) for r in failing],
                             [("Count", "four-2")])

    def test_sum_passes_while_count_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            before = Path(tmp) / "before"
            after = Path(tmp) / "after"
            write_tree(before)
            # Count 2.90 KB = 2969.6 B (over 2864); Sum 3.20 KB = 3276.8 B (under 3490).
            write_tree(after, count_alloc="2.90 KB", sum_alloc="3.20 KB")
            code, payload, text = run_gate(before, after, Path(tmp) / "gate.json")

            self.assertEqual(code, 1, text)
            by_terminal = {}
            for row in payload["rows"]:
                by_terminal.setdefault(row["terminal"], []).append(row)
            self.assertTrue(all(not row["pass"] for row in by_terminal["Count"]))
            self.assertTrue(all(row["pass"] for row in by_terminal["Sum"]), by_terminal["Sum"])
            self.assertTrue(all(row["slack_b"] > 0 for row in by_terminal["Sum"]))
            self.assertEqual([v["terminal"] for v in payload["violations"]], ["Count"] * 3)


class TimePredicateTests(unittest.TestCase):
    def test_slow_after_round_fails_the_ratio_predicate(self):
        with tempfile.TemporaryDirectory() as tmp:
            before = Path(tmp) / "before"
            after = Path(tmp) / "after"
            write_tree(before)
            # q_before(Count) ~= 0.757; after nextorm gets 1.5x LINQ -> both predicates miss.
            write_tree(after, count_alloc="2.70 KB", sum_alloc="3.30 KB",
                       count_mean="36.00 \u03bcs")
            code, payload, text = run_gate(before, after, Path(tmp) / "gate.json")

            self.assertEqual(code, 1, text)
            self.assertTrue(all(row["pass"] for row in payload["rows"]), payload["rows"])
            count_time = [row for row in payload["time_rows"] if row["terminal"] == "Count"][0]
            self.assertAlmostEqual(count_time["median_q_after"], 36.0 / 24.3, places=6)
            self.assertFalse(count_time["pass_absolute"])
            self.assertFalse(count_time["pass_relative"])
            sum_time = [row for row in payload["time_rows"] if row["terminal"] == "Sum"][0]
            self.assertTrue(sum_time["pass"], sum_time)
            self.assertEqual([(v["kind"], v["terminal"]) for v in payload["violations"]],
                             [("time", "Count")])


class InputErrorTests(unittest.TestCase):
    def test_missing_after_round_directory_exits_two(self):
        with tempfile.TemporaryDirectory() as tmp:
            before = Path(tmp) / "before"
            after = Path(tmp) / "after"
            write_tree(before)
            write_tree(after, rounds=("four-1",))
            code, payload, text = run_gate(before, after, Path(tmp) / "gate.json")

            self.assertEqual(code, 2, text)
            self.assertEqual(payload["status"], "input-error")
            self.assertEqual(payload["rows"], [])
            self.assertIn("four-2", payload["error"])
            self.assertIn("missing", payload["error"])

    def test_missing_after_dir_exits_two(self):
        with tempfile.TemporaryDirectory() as tmp:
            before = Path(tmp) / "before"
            write_tree(before)
            code, payload, text = run_gate(before, Path(tmp) / "nope", Path(tmp) / "gate.json")
            self.assertEqual(code, 2, text)
            self.assertEqual(payload["status"], "input-error")

    def test_malformed_csv_exits_two(self):
        cases = {
            "missing Allocated column": "Method,Job,Mean\n"
                                        "B_Nextorm_Cached_Count,ShortRun,18.4 \u03bcs\n",
            "ragged row": HEADER + "\n"
                          "B_Nextorm_Cached_Count,ShortRun,18.4 \u03bcs,2.91 KB,EXTRA\n",
            "unparsable allocated": HEADER + "\n"
                                    "B_Nextorm_Cached_Count,ShortRun,18.4 \u03bcs,lots\n"
                                    "B_Linq2Db_Count,ShortRun,24.3 \u03bcs,2.8 KB\n"
                                    "B_Nextorm_Cached_Sum,ShortRun,280 \u03bcs,3.31 KB\n"
                                    "B_Linq2Db_Sum,ShortRun,292 \u03bcs,3.41 KB\n",
        }
        for name, body in cases.items():
            with self.subTest(name), tempfile.TemporaryDirectory() as tmp:
                before = Path(tmp) / "before"
                after = Path(tmp) / "after"
                write_tree(before)
                write_tree(after, count_alloc="2.70 KB", sum_alloc="3.30 KB")
                report = (Path(after) / "four-2" / "results" / REPORT)
                report.write_text(body, encoding="utf-8")
                code, payload, text = run_gate(before, after, Path(tmp) / "gate.json")
                self.assertEqual(code, 2, text)
                self.assertEqual(payload["status"], "input-error")

    def test_arm_set_mismatch_exits_two(self):
        with tempfile.TemporaryDirectory() as tmp:
            before = Path(tmp) / "before"
            after = Path(tmp) / "after"
            write_tree(before)
            write_tree(after, count_alloc="2.70 KB", sum_alloc="3.30 KB")
            report = Path(after) / "four-3" / "results" / REPORT
            body = "\n".join(report.read_text(encoding="utf-8").splitlines()[:-1]) + "\n"
            report.write_text(body, encoding="utf-8")  # drop the LINQ Sum arm
            code, payload, text = run_gate(before, after, Path(tmp) / "gate.json")
            self.assertEqual(code, 2, text)
            self.assertEqual(payload["status"], "input-error")
            self.assertIn("B_Linq2Db_Sum", payload["error"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
