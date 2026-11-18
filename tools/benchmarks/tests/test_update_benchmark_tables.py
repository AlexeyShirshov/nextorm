#!/usr/bin/env python3
"""Self-tests for tools/benchmarks/update_benchmark_tables.py (pure parsing/rendering; no benchmark run).

Run with either::

    python3 -m unittest discover -s tools/benchmarks/tests -p 'test_update_benchmark_tables.py' -v
    python3 -m pytest tools/benchmarks/tests/test_update_benchmark_tables.py -q
"""
from __future__ import annotations

import datetime as dt
import hashlib
import json
import os
import shutil
import sys
import tempfile
import time
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


def write_suite_run_date_metadata(
    results_dir: Path,
    dates: dict[str, str] | None = None,
    source_evidence: str = "unit test run evidence",
) -> None:
    dates = dates or {}
    suites = {}
    for class_name in tables.SUITE_RUN_DATE_CLASSES:
        csv_file = results_dir / f"NextORM.Benchmark.{class_name}-report.csv"
        md_file = results_dir / f"NextORM.Benchmark.{class_name}-report-github.md"
        if not csv_file.is_file() or not md_file.is_file():
            write_report(results_dir, class_name, [])
        csv_path, md_path = tables.report_paths(results_dir, class_name)
        suites[class_name] = {
            "asOfDate": dates.get(class_name, "2026-10-17"),
            "sourceEvidence": source_evidence,
            "reportsSha256": {
                path.name: hashlib.sha256(
                    path.read_bytes().replace(b"\r\n", b"\n")
                ).hexdigest()
                for path in (csv_path, md_path)
            },
        }
    metadata = {
        "schemaVersion": 1,
        "dateBasis": "UTC suite-start date",
        "suites": suites,
    }
    (results_dir / tables.SUITE_RUN_DATE_METADATA).write_text(
        json.dumps(metadata, indent=2) + "\n",
        encoding="utf-8",
    )


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
        self.assertNotIn("Code |", table)

    def test_code_links_point_at_the_measured_source(self):
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
        with mock.patch.object(tables, "E2E_ROWS", (tables.E2E_ROWS[0],)):
            table = tables.build_e2e_table(self.results, "en", code_links=True)
        self.assertIn("| Method | Nextorm | Dapper | linq2db | EF Core | Code |", table)
        # The report link stays, and the new cell points at the class declaration in the source.
        self.assertIn(
            "[Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/"
            "BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md)",
            table,
        )
        self.assertIn(
            "| [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/nextorm.benchmark/"
            "Sqlite/SqliteBenchmarkIteration.cs#L",
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

    def test_report_dates_are_pinned_per_suite_and_ignore_mtimes(self):
        dates = {
            "SqliteBenchmarkIteration": "2026-10-17",
            tables.CATEGORY_A_CLASS: "2026-10-18",
            tables.CATEGORY_B_CLASS: "2026-10-19",
        }
        write_suite_run_date_metadata(self.results, dates)
        stamp = dt.datetime(2026, 10, 20, 12, 0, tzinfo=dt.timezone.utc).timestamp()
        for class_name in dates:
            csv_path, md_path = tables.report_paths(self.results, class_name)
            os.utime(csv_path, (stamp, stamp))
            os.utime(md_path, (stamp, stamp))

        for class_name, expected_date in dates.items():
            self.assertEqual(tables.report_date(self.results, class_name), expected_date)

        if not hasattr(time, "tzset"):
            self.skipTest("the platform cannot switch its process timezone")
        original_timezone = os.environ.get("TZ")
        try:
            for timezone in ("UTC", "Pacific/Kiritimati", "America/Adak"):
                os.environ["TZ"] = timezone
                time.tzset()
                for class_name, expected_date in dates.items():
                    self.assertEqual(tables.report_date(self.results, class_name), expected_date)
        finally:
            if original_timezone is None:
                os.environ.pop("TZ", None)
            else:
                os.environ["TZ"] = original_timezone
            time.tzset()

    def test_report_date_rejects_report_content_changed_after_metadata(self):
        write_suite_run_date_metadata(self.results)
        report = self.results / "NextORM.Benchmark.SqliteBenchmarkIteration-report.csv"
        report.write_text(report.read_text(encoding="utf-8") + "Changed,1 μs,1 B,\n", encoding="utf-8")

        with self.assertRaisesRegex(tables.TableError, "SHA-256 differs from suite-run-dates.json"):
            tables.report_date(self.results)

    def test_recording_suite_date_rebinds_metadata_to_current_reports(self):
        write_suite_run_date_metadata(self.results)
        tables._record_suite_run_dates(
            self.results,
            {"SqliteBenchmarkIteration": "2026-10-18"},
            "https://example.test/run/123",
        )

        self.assertEqual(tables.report_date(self.results), "2026-10-18")
        metadata = json.loads(
            (self.results / tables.SUITE_RUN_DATE_METADATA).read_text(encoding="utf-8")
        )
        self.assertEqual(
            metadata["suites"]["SqliteBenchmarkIteration"]["sourceEvidence"],
            "https://example.test/run/123",
        )

    def test_date_override_remains_effective_in_conditions(self):
        target = next(target for target in tables.TARGETS if target.name == "docs")
        conditions = {
            "cpu": "Test CPU",
            "logical": "8",
            "physical": "4",
            "runtime": "11.0.0",
            "bdn": "0.15.8",
            "job": "Job.Default",
            "toolchain": "InProcessEmitToolchain",
        }
        with mock.patch.object(tables, "read_run_conditions", return_value=conditions), mock.patch.object(
            tables, "report_date", return_value="2026-10-18"
        ):
            text = tables.build_conditions(target, self.results, "2026-10-17")

        self.assertIn("As of **2026-10-17**", text)
        self.assertIn("2026-10-18", text)


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
        write_suite_run_date_metadata(
            self.results,
            {tables.CATEGORY_A_CLASS: "2026-10-18"},
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

    def test_feature_rows_link_the_feature_benchmark(self):
        self._write_features()
        categories = tuple(item for item in tables.CATEGORY_A if item[0] in ("A_Cte", "A_CaseWhen"))
        with mock.patch.object(tables, "CATEGORY_A", categories):
            rows = tables.build_feature_rows(self.results, "en", code_links=True)
        for row in rows:
            # The report link stays; the code cell anchors the category's first arm (nextorm).
            self.assertIn("SqliteBenchmarkFeaturesFair-report-github.md)", row)
            self.assertIn(
                "| [code](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/"
                "nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFair.cs#L",
                row,
            )

    def test_code_link_fails_closed_when_the_category_leaves_the_source(self):
        write_report(
            self.results,
            tables.CATEGORY_A_CLASS,
            [
                "A_Nextorm_Prepared_Removed,10.0 μs,1.0 KB,A_Removed",
                "A_Dapper_Removed,12.0 μs,1.0 KB,A_Removed",
            ],
        )
        categories = (("A_Removed", "Removed", "Removed"),)
        with mock.patch.object(tables, "CATEGORY_A", categories):
            with self.assertRaises(tables.TableError):
                tables.build_feature_rows(self.results, "en", code_links=True)

    def test_only_the_readme_table_links_the_code(self):
        by_name = {target.name: target for target in tables.TARGETS}
        self.assertTrue(by_name["readme"].code_links)
        self.assertFalse(by_name["docs"].code_links)
        self.assertFalse(by_name["docs-ru"].code_links)

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


class Tier1TableTests(unittest.TestCase):
    def test_article_targets_generate_fresh_tier1_tables(self):
        article_targets = [target for target in tables.TARGETS if target.name in ("docs", "docs-ru")]
        self.assertEqual(len(article_targets), 2)
        for target in article_targets:
            self.assertTrue(set(tables.TIER1_BLOCKS).issubset(target.blocks))
            self.assertEqual(
                tables.read_text_preserving(target.path),
                tables.render_target(
                    target,
                    tables.DEFAULT_RESULTS_DIR,
                    tables.report_date(tables.DEFAULT_RESULTS_DIR),
                ),
            )

        blocks = tables.build_tier1_blocks(tables.DEFAULT_RESULTS_DIR, "en")
        run_metadata = tables._tier1_run_metadata(
            tables.DEFAULT_RESULTS_DIR,
            {
                key: tables._tier1_measurements(tables.DEFAULT_RESULTS_DIR, key)
                for key in tables.TIER1_RUN_REPORTS
            },
        )
        self.assertIn(run_metadata["asOfDate"], blocks["tier1-run-info"])
        self.assertIn("Job.Default", blocks["tier1-run-info"])
        self.assertIn("InProcessEmitToolchain", blocks["tier1-run-info"])
        self.assertIn(run_metadata["run"]["sourceHead"], blocks["tier1-run-info"])
        self.assertIn(str(run_metadata["run"]["localNextormProjects"]), blocks["tier1-run-info"])
        self.assertEqual(len(run_metadata["run"]["runtimeAssemblySha256"]), 10)
        self.assertEqual(run_metadata["run"]["matchedBuildRuntimeHashRows"], 11)
        self.assertEqual(len(run_metadata["run"]["sourceGeneratorAnalyzerSha256"]), 64)
        projection = tables._tier1_measurements(tables.DEFAULT_RESULTS_DIR, "projection")[0]
        self.assertIn(projection.method, blocks["tier1-projection"])
        self.assertIn(projection.mean, blocks["tier1-projection"])
        self.assertIn("B_Nextorm_ToDataReader", blocks["tier1-streaming-readers"])
        self.assertIn("Provider_Direct_ToDataReader_Empty", blocks["tier1-streaming-empty-readers"])
        self.assertEqual(len(blocks["tier1-projection"].splitlines()), 9)
        self.assertEqual(len(blocks["tier1-aggregates"].splitlines()), 23)
        self.assertEqual(len(blocks["tier1-paging"].splitlines()), 9)
        self.assertEqual(len(blocks["tier1-streaming-buffered"].splitlines()), 9)
        self.assertEqual(len(blocks["tier1-streaming-unbuffered"].splitlines()), 8)
        self.assertEqual(len(blocks["tier1-streaming-readers"].splitlines()), 7)
        self.assertEqual(len(blocks["tier1-streaming-empty-readers"].splitlines()), 6)
        self.assertEqual(len(blocks["tier1-write-json"].splitlines()), 13)
        self.assertEqual(len(blocks["tier1-csv"].splitlines()), 10)

    @staticmethod
    def _resumed_tier1_dir(temp: str, *, groups=None, **overrides) -> Path:
        results = Path(temp)
        for filename in tables.TIER1_REQUIRED_REPORTS:
            shutil.copy2(tables.DEFAULT_RESULTS_DIR / filename, results / filename)
        metadata = json.loads(
            (tables.DEFAULT_RESULTS_DIR / tables.TIER1_RUN_METADATA).read_text(encoding="utf-8")
        )
        run = metadata["run"]
        run["startedUtc"] = "2026-11-17T18:38:46Z"
        run["finishedUtc"] = "2026-11-18T02:56:28Z"
        run["groupRuns"] = [
            {"group": "projection", "startedUtc": "2026-11-17T18:38:46Z", "finishedUtc": "2026-11-17T18:46:00Z"},
            {"group": "aggregates", "startedUtc": "2026-11-17T18:46:00Z", "finishedUtc": "2026-11-17T18:53:03Z"},
            {"group": "paging", "startedUtc": "2026-11-17T18:53:03Z", "finishedUtc": "2026-11-17T19:00:05Z"},
            {"group": "streaming", "startedUtc": "2026-11-18T02:22:17Z", "finishedUtc": "2026-11-18T02:29:56Z"},
            {"group": "write-json", "startedUtc": "2026-11-18T02:29:56Z", "finishedUtc": "2026-11-18T02:47:27Z"},
            {"group": "csv", "startedUtc": "2026-11-18T02:47:27Z", "finishedUtc": "2026-11-18T02:56:28Z"},
        ]
        if groups is not None:
            run["groupRuns"] = groups
        run.update(overrides)
        (results / tables.TIER1_RUN_METADATA).write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
        return results

    def test_tier1_resumed_multi_date_run_renders_the_actual_range(self):
        with tempfile.TemporaryDirectory() as temp:
            results = self._resumed_tier1_dir(temp)
            for lang in ("en", "ru"):
                with self.subTest(lang=lang):
                    info = tables.build_tier1_blocks(results, lang)["tier1-run-info"]
                    self.assertIn("2026-11-17–2026-11-18", info)
                    self.assertIn("2026-11-17 18:38:46", info)
                    self.assertIn("2026-11-18 02:56:28", info)

    def test_tier1_multi_date_run_requires_explicit_group_windows(self):
        with tempfile.TemporaryDirectory() as temp:
            results = self._resumed_tier1_dir(temp, groups=[])
            with self.assertRaises(tables.TableError):
                tables.build_tier1_blocks(results, "en")

    def test_tier1_group_windows_must_be_ordered_and_non_overlapping(self):
        groups = [
            {"group": "projection", "startedUtc": "2026-11-17T18:38:46Z", "finishedUtc": "2026-11-17T18:46:00Z"},
            {"group": "aggregates", "startedUtc": "2026-11-17T18:40:00Z", "finishedUtc": "2026-11-17T18:53:03Z"},
            {"group": "csv", "startedUtc": "2026-11-18T02:47:27Z", "finishedUtc": "2026-11-18T02:56:28Z"},
        ]
        with tempfile.TemporaryDirectory() as temp:
            results = self._resumed_tier1_dir(temp, groups=groups)
            with self.assertRaises(tables.TableError):
                tables.build_tier1_blocks(results, "en")

    def test_tier1_group_window_cannot_end_before_it_starts(self):
        groups = [
            {"group": "projection", "startedUtc": "2026-11-17T18:38:46Z", "finishedUtc": "2026-11-17T18:30:00Z"},
            {"group": "csv", "startedUtc": "2026-11-18T02:47:27Z", "finishedUtc": "2026-11-18T02:56:28Z"},
        ]
        with tempfile.TemporaryDirectory() as temp:
            results = self._resumed_tier1_dir(temp, groups=groups)
            with self.assertRaises(tables.TableError):
                tables.build_tier1_blocks(results, "en")

    def test_tier1_group_windows_must_cover_the_overall_run(self):
        groups = [
            {"group": "projection", "startedUtc": "2026-11-17T18:40:00Z", "finishedUtc": "2026-11-17T18:46:00Z"},
            {"group": "csv", "startedUtc": "2026-11-18T02:47:27Z", "finishedUtc": "2026-11-18T02:56:28Z"},
        ]
        with tempfile.TemporaryDirectory() as temp:
            results = self._resumed_tier1_dir(temp, groups=groups)
            with self.assertRaises(tables.TableError):
                tables.build_tier1_blocks(results, "en")

    def test_tier1_same_day_run_keeps_the_single_date_contract(self):
        with tempfile.TemporaryDirectory() as temp:
            results = self._resumed_tier1_dir(
                temp,
                startedUtc="2026-11-17T18:38:46Z",
                finishedUtc="2026-11-17T19:00:05Z",
            )
            with self.assertRaises(tables.TableError):
                tables.build_tier1_blocks(results, "en")

    def test_tier1_resumed_run_still_validates_report_hashes(self):
        with tempfile.TemporaryDirectory() as temp:
            results = self._resumed_tier1_dir(temp)
            victim = results / tables.TIER1_REQUIRED_REPORTS[0]
            victim.write_bytes(victim.read_bytes() + b" ")
            with self.assertRaises(tables.TableError):
                tables.build_tier1_blocks(results, "en")

    def test_tier1_hash_manifest_accepts_lf_and_crlf_reports(self):
        for line_ending in (b"\n", b"\r\n"):
            with self.subTest(line_ending=line_ending), tempfile.TemporaryDirectory() as temp:
                results = Path(temp)
                for filename in tables.TIER1_REQUIRED_REPORTS:
                    report = tables.DEFAULT_RESULTS_DIR / filename
                    report_bytes = report.read_bytes().replace(b"\r\n", b"\n")
                    if line_ending == b"\r\n":
                        report_bytes = report_bytes.replace(b"\n", b"\r\n")
                    (results / filename).write_bytes(report_bytes)
                shutil.copy2(
                    tables.DEFAULT_RESULTS_DIR / tables.TIER1_RUN_METADATA,
                    results / tables.TIER1_RUN_METADATA,
                )

                tables.build_tier1_blocks(results, "en")

    def test_tier1_hash_manifest_rejects_a_changed_report(self):
        with tempfile.TemporaryDirectory() as temp:
            results = Path(temp)
            for filename in tables.TIER1_REQUIRED_REPORTS:
                shutil.copy2(tables.DEFAULT_RESULTS_DIR / filename, results / filename)
            shutil.copy2(
                tables.DEFAULT_RESULTS_DIR / tables.TIER1_RUN_METADATA,
                results / tables.TIER1_RUN_METADATA,
            )
            # Mutate a report independently of any particular measurement value: a hard-coded
            # iteration-20 number would silently become a no-op once the snapshot is refreshed. The
            # Markdown twin is part of the same hash inventory and is not parsed as a CSV, so the
            # freshness gate is what must reject the change.
            report = results / "NextORM.Benchmark.SqliteBenchmarkProjection-report-github.md"
            report.write_text(
                report.read_text(encoding="utf-8") + "tampered report line\n",
                encoding="utf-8",
            )
            with self.assertRaisesRegex(tables.TableError, "SHA-256 differs"):
                tables.build_tier1_blocks(results, "en")


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
