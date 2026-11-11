#!/usr/bin/env python3
"""Self-tests for tools/benchmarks/update_benchmark_tables.py (pure parsing/rendering; no benchmark run).

Run with either::

    python3 -m unittest discover -s tools/benchmarks/tests -p 'test_update_benchmark_tables.py' -v
    python3 -m pytest tools/benchmarks/tests/test_update_benchmark_tables.py -q
"""
from __future__ import annotations

import datetime as dt
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

PERF_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PERF_DIR))

import update_benchmark_tables as tables  # noqa: E402

HEADER = "Method,Mean,Allocated,Categories\n"


def write_report(results_dir: Path, class_name: str, rows: list) -> None:
    stem = f"NextORM.Benchmark.{class_name}"
    (results_dir / f"{stem}-report.csv").write_text(
        HEADER + "".join(row + "\n" for row in rows), encoding="utf-8"
    )
    (results_dir / f"{stem}-report-github.md").write_text("stub\n", encoding="utf-8")


class NumberTests(unittest.TestCase):
    def test_parse_time_units(self):
        self.assertEqual(tables.parse_time_ns("942.3 μs"), 942_300.0)
        self.assertEqual(tables.parse_time_ns("1,385.0 μs"), 1_385_000.0)
        self.assertEqual(tables.parse_time_ns("8.592 ms"), 8_592_000.0)
        self.assertEqual(tables.parse_time_ns("123 ns"), 123.0)

    def test_parse_time_rejects_unknown(self):
        with self.assertRaises(tables.TableError):
            tables.parse_time_ns("fast")

    def test_parse_bytes(self):
        self.assertEqual(tables.parse_bytes("792 B"), 792.0)
        self.assertEqual(tables.parse_bytes("2.14 MB"), 2.14 * 1024 * 1024)

    def test_format_round_trip(self):
        self.assertEqual(tables.format_time_ns(13_040.0), "13.04 μs")
        self.assertEqual(tables.format_time_ns(8_592_000.0), "8.59 ms")
        self.assertEqual(tables.format_time_ns(123.0), "123.00 ns")
        self.assertEqual(tables.format_bytes(672.0), "672 B")
        self.assertEqual(tables.format_bytes(2.56 * 1024), "2.56 KB")
        self.assertEqual(tables.format_bytes(2.14 * 1024**2), "2.14 MB")
        # The report's own unit wins (BDN picks one unit per column).
        self.assertEqual(tables.format_alloc_like("10224 B", 10224), "10,224 B")
        self.assertEqual(tables.format_alloc_like("2.14 MB", 2.14 * 1024**2), "2.14 MB")


class E2ETableTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.results = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def test_fastest_arm_per_library_is_bold(self):
        write_report(
            self.results,
            "SqliteBenchmarkIteration",
            [
                "Nextorm_Prepared,10.00 μs,800 B,",
                "Nextorm_Cached,12.00 μs,900 B,",
                "Dapper_Async,15.00 μs,1 KB,",
                "Linq2Db_Compiled,17.00 μs,2 KB,",
                "EFCore_Compiled,38.00 μs,10 KB,",
            ],
        )
        with mock.patch.object(tables, "E2E_ROWS", (tables.E2E_ROWS[0],)):
            table = tables.build_e2e_table(self.results, "en")
        self.assertIn(
            "| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/"
            "BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) "
            "| **10.00 μs** | 15.00 μs | 17.00 μs | 38.00 μs |",
            table,
        )

    def test_allocations_follow_the_fastest_arm(self):
        write_report(
            self.results,
            "SqliteBenchmarkIteration",
            [
                "Nextorm_SlowButLean,12.00 μs,100 B,",
                "Nextorm_Fast,10.00 μs,800 B,",
                "Dapper_Async,15.00 μs,900 B,",
                "Linq2Db_Compiled,17.00 μs,2 KB,",
                "EFCore_Compiled,38.00 μs,10 KB,",
            ],
        )
        with mock.patch.object(tables, "E2E_ROWS", (tables.E2E_ROWS[0],)):
            table = tables.build_allocations_table(self.results, "en")
        self.assertIn("| Data fetch | **800 B** |", table)

    def test_missing_library_fails_closed(self):
        write_report(
            self.results,
            "SqliteBenchmarkIteration",
            [
                "Nextorm_Prepared,10.00 μs,800 B,",
                "Dapper_Async,15.00 μs,1 KB,",
                "Linq2Db_Compiled,17.00 μs,2 KB,",
            ],
        )
        with self.assertRaises(tables.TableError):
            tables.build_e2e_table(self.results, "en")

    def test_missing_report_fails_closed(self):
        with self.assertRaises(tables.TableError):
            tables.build_e2e_table(self.results, "en")

    def test_mixed_jobs_fail_closed(self):
        path = self.results / "NextORM.Benchmark.SqliteBenchmarkIteration-report.csv"
        path.write_text(
            "Method,Job,Toolchain,Mean,Allocated,Categories\n"
            "Nextorm_Prepared,DefaultJob,Default,10.00 μs,1 KB,\n"
            "Nextorm_Prepared,ShortRun,InProcessEmitToolchain,9.00 μs,1 KB,\n",
            encoding="utf-8",
        )
        with self.assertRaises(tables.TableError):
            tables.read_measurements(path)

    def test_report_date_comes_from_the_report_mtime(self):
        write_report(
            self.results,
            "SqliteBenchmarkIteration",
            [
                "Nextorm_Prepared,10.00 μs,800 B,",
                "Dapper_Async,15.00 μs,1 KB,",
                "Linq2Db_Compiled,17.00 μs,2 KB,",
                "EFCore_Compiled,38.00 μs,10 KB,",
            ],
        )
        stamp = dt.datetime(2026, 10, 17, 12, 0).timestamp()
        os.utime(self.results / "NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md", (stamp, stamp))
        self.assertEqual(tables.report_date(self.results), "2026-10-17")


class FeatureRowTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.results = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _write_features(self):
        write_report(
            self.results,
            tables.CATEGORY_A_CLASS,
            [
                "A_Nextorm_Prepared_Cte,90.0 μs,7.0 KB,A_Cte",
                "A_Dapper_Cte,120.0 μs,13.0 KB,A_Cte",
                "A_Linq2Db_Compiled_Cte,150.0 μs,15.0 KB,A_Cte",
                "A_Nextorm_Prepared_CaseWhen,100.0 μs,7.0 KB,A_CaseWhen",
                "A_Dapper_CaseWhen,140.0 μs,13.0 KB,A_CaseWhen",
                "A_Linq2Db_Compiled_CaseWhen,150.0 μs,15.0 KB,A_CaseWhen",
                "A_EFCore_Compiled_CaseWhen,350.0 μs,70.0 KB,A_CaseWhen",
            ],
        )

    def test_per_query_values_come_from_the_category_a_report(self):
        self._write_features()
        categories = tuple(item for item in tables.CATEGORY_A if item[0] in ("A_Cte", "A_CaseWhen"))
        with mock.patch.object(tables, "CATEGORY_A", categories):
            rows = tables.build_feature_rows(self.results, "en")
        self.assertEqual(
            rows,
            [
                "| [`CASE WHEN`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/"
                "BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md)"
                " | **10.00 μs** | 14.00 μs | 15.00 μs | 35.00 μs |",
                "| [CTE](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/"
                "BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md)"
                " | **9.00 μs** | 12.00 μs | 15.00 μs | — |",
            ],
        )

    def test_category_a_block_carries_its_own_run_date(self):
        self._write_features()
        categories = tuple(item for item in tables.CATEGORY_A if item[0] in ("A_Cte", "A_CaseWhen"))
        with mock.patch.object(tables, "CATEGORY_A", categories):
            table = tables.build_category_a_table(self.results, "en")
        stamp = tables.report_date(self.results, tables.CATEGORY_A_CLASS)
        self.assertTrue(table.startswith(f"As of **{stamp}**.\n\n"), table)
        self.assertIn("| Feature | Nextorm (prepared) |", table)

    def test_unknown_feature_category_fails_closed(self):
        write_report(
            self.results,
            tables.CATEGORY_A_CLASS,
            ["A_Nextorm_Prepared_Unknown,1 μs,1 B,A_Unknown"],
        )
        with self.assertRaises(tables.TableError):
            tables.build_feature_rows(self.results, "en")

    def test_only_the_readme_table_grows_with_features(self):
        self._write_features()
        write_report(
            self.results,
            "SqliteBenchmarkIteration",
            [
                "Nextorm_Prepared,10.00 μs,800 B,",
                "Dapper_Async,15.00 μs,1 KB,",
                "Linq2Db_Compiled,17.00 μs,2 KB,",
                "EFCore_Compiled,38.00 μs,10 KB,",
            ],
        )
        categories = tuple(item for item in tables.CATEGORY_A if item[0] in ("A_Cte", "A_CaseWhen"))
        with mock.patch.object(tables, "E2E_ROWS", (tables.E2E_ROWS[0],)), mock.patch.object(
            tables, "CATEGORY_A", categories
        ):
            plain = tables.build_e2e_table(self.results, "en")
            extended = tables.build_e2e_table(self.results, "en", include_features=True)
        self.assertNotIn("| [CTE](", plain)
        self.assertIn("| [CTE](", extended)
        self.assertEqual(extended.count("\n"), plain.count("\n") + len(categories))

    def test_readme_is_the_only_target_with_feature_rows(self):
        by_name = {target.name: target for target in tables.TARGETS}
        self.assertTrue(by_name["readme"].feature_rows)
        self.assertFalse(by_name["docs"].feature_rows)
        self.assertFalse(by_name["docs-ru"].feature_rows)


class CategoryBTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.results = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def test_per_query_values_are_divided_by_iterations(self):
        write_report(
            self.results,
            tables.CATEGORY_B_CLASS,
            [
                "B_Nextorm_Cached_Cte,140.0 μs,40.0 KB,B_Cte",
                "B_Dapper_Cte,150.0 μs,13.0 KB,B_Cte",
                "B_Nextorm_Cached_CaseWhen,100.0 μs,20.0 KB,B_CaseWhen",
                "B_Dapper_CaseWhen,150.0 μs,13.0 KB,B_CaseWhen",
                "B_Linq2Db_CaseWhen,250.0 μs,30.0 KB,B_CaseWhen",
                "B_EFCore_CaseWhen,600.0 μs,110.0 KB,B_CaseWhen",
            ],
        )
        categories = tuple(item for item in tables.CATEGORY_B if item[0] in ("B_Cte", "B_CaseWhen"))
        with mock.patch.object(tables, "CATEGORY_B", categories):
            table = tables.build_category_b_table(self.results, "en")
        self.assertIn("| CTE | **14.00 μs / 4.00 KB** | 15.00 μs / 1.30 KB | — | — |", table)
        self.assertIn("| `CASE WHEN` | **10.00 μs / 2.00 KB** |", table)

    def test_unknown_category_fails_closed(self):
        write_report(
            self.results,
            tables.CATEGORY_B_CLASS,
            ["B_Nextorm_Cached_Unknown,1 μs,1 B,B_Unknown"],
        )
        with self.assertRaises(tables.TableError):
            tables.build_category_b_table(self.results, "en")


class BlockTests(unittest.TestCase):
    def test_blocks_are_replaced_and_line_endings_preserved(self):
        text = (
            "intro\r\n"
            "<!-- benchmarks:e2e:start -->\r\n"
            "old\r\n"
            "<!-- benchmarks:e2e:end -->\r\n"
            "tail\r\n"
        )
        updated = tables.replace_blocks(text, {"e2e": "a\nb"})
        self.assertEqual(
            updated,
            "intro\r\n"
            "<!-- benchmarks:e2e:start -->\r\n"
            "a\r\n"
            "b\r\n"
            "<!-- benchmarks:e2e:end -->\r\n"
            "tail\r\n",
        )

    def test_missing_marker_fails_closed(self):
        with self.assertRaises(tables.TableError):
            tables.replace_blocks("no markers\n", {"e2e": "x"})

    def test_read_text_preserving_keeps_crlf(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "doc.md"
            path.write_bytes(b"a\r\nb\r\n")
            self.assertEqual(tables.read_text_preserving(path), "a\r\nb\r\n")


if __name__ == "__main__":
    unittest.main()
