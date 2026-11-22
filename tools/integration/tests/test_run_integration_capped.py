import contextlib
import hashlib
import importlib.util
import io
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch


RUNNER_PATH = pathlib.Path(__file__).resolve().parents[1] / "run-integration-capped.py"
SPEC = importlib.util.spec_from_file_location("run_integration_capped", RUNNER_PATH)
assert SPEC is not None and SPEC.loader is not None
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)


class FakeProcess:
    def __init__(self, output: str, exit_code: int = 0) -> None:
        self.stdout = io.StringIO(output)
        self.exit_code = exit_code
        self.returncode: int | None = None

    def wait(self) -> int:
        self.returncode = self.exit_code
        return self.exit_code


class IntegrationRunnerReceiptTests(unittest.TestCase):
    def test_candidate_identity_hashes_changes_without_storing_diff(self) -> None:
        results = [
            subprocess.CompletedProcess([], 0, stdout="abc123\n"),
            subprocess.CompletedProcess([], 0, stdout=" M src/file.py\n"),
            subprocess.CompletedProcess([], 0, stdout=b""),
            subprocess.CompletedProcess([], 0, stdout=b"private diff contents"),
        ]
        with patch.object(runner.subprocess, "run", side_effect=results):
            candidate = runner.candidate_identity()

        self.assertEqual("abc123", candidate["git_head"])
        self.assertTrue(candidate["worktree_dirty"])
        self.assertEqual(1, candidate["status_entries"])
        self.assertEqual(
            hashlib.sha256(b"private diff contents").hexdigest(),
            candidate["tracked_diff_sha256"],
        )
        self.assertNotIn("private diff contents", json.dumps(candidate))

    def test_test_assembly_identity_hashes_the_selected_build_output(self) -> None:
        payload = b"offline test assembly"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = pathlib.Path(temporary_directory)
            assembly = (
                root
                / runner.PROJECT
                / "bin"
                / "linux"
                / "Debug"
                / "net11.0"
                / "nextorm.integration.tests.dll"
            )
            assembly.parent.mkdir(parents=True)
            assembly.write_bytes(payload)
            with patch.object(runner, "REPO_ROOT", root):
                identity = runner.test_assembly_identity("Debug")

        self.assertEqual("verified", identity["status"])
        self.assertEqual(hashlib.sha256(payload).hexdigest(), identity["sha256"])

    def test_run_class_records_scope_command_result_and_redacts_endpoint(self) -> None:
        secret = "postgresql://user:secret-value@127.0.0.1:5432/db"
        env = dict(os.environ)
        env["NEXTORM_POSTGRES_CONNECTION"] = secret
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = pathlib.Path(temporary_directory)
            receipt_path = root / "integration-receipt.jsonl"
            log_path = root / "postgres-tests.log"
            receipt = {
                "path": receipt_path,
                "run_id": "test-run",
                "candidate": {"git_head": "abc123", "worktree_dirty": True},
                "scope": {"kind": "provider-classes", "classes": ["Example.Tests"]},
            }
            command = [
                sys.executable,
                "-c",
                "import os; print(os.environ['NEXTORM_POSTGRES_CONNECTION']); "
                "print('Total: 3, Errors: 0, Failed: 0, Skipped: 1, Not Run: 0')",
            ]

            result = runner.run_class("postgres", "Example.Tests", command, env, log_path, receipt)

            records = [json.loads(line) for line in receipt_path.read_text(encoding="utf-8").splitlines()]
            receipt_text = receipt_path.read_text(encoding="utf-8")
            log = log_path.read_text(encoding="utf-8")

        self.assertEqual(0, result["rc"])
        self.assertEqual(
            {"total": 3, "passed": 2, "failed": 0, "skipped": 1, "not_run": 0, "errors": 0},
            result["summary"],
        )
        self.assertEqual(["run_started", "run_finished"], [record["event"] for record in records])
        self.assertEqual(command, records[0]["command"])
        self.assertEqual(0, records[1]["exit_code"])
        self.assertEqual("postgres-tests.log", pathlib.Path(records[1]["log"]).name)
        self.assertEqual("NEXTORM_POSTGRES_CONNECTION", records[0]["resource"]["override_variables"][0])
        self.assertIn("<redacted>", log)
        self.assertNotIn(secret, log)
        self.assertNotIn(secret, receipt_text)
        self.assertEqual("passed", result["status"])

    def run_main_with_fake_processes(
        self,
        outputs: list[str],
        *,
        build: bool = False,
    ) -> tuple[int, list[dict[str, object]], list[list[str]], list[bool]]:
        with tempfile.TemporaryDirectory() as temporary_directory:
            spawned_commands: list[list[str]] = []
            remaining_outputs = list(outputs)
            assembly = {
                "status": "verified",
                "path": "tests/nextorm.integration.tests/bin/linux/Debug/net11.0/nextorm.integration.tests.dll",
                "sha256": "test-assembly-sha256",
                "size_bytes": 128,
            }

            def fake_popen(command: list[str], **_: object) -> FakeProcess:
                spawned_commands.append(command)
                return FakeProcess(remaining_outputs.pop(0))

            args = [
                "--class",
                "Example.Tests",
                "--log-dir",
                temporary_directory,
                "--gap",
                "0",
            ]
            if build:
                args.append("--build")
            with (
                patch.object(runner, "probe_systemd", return_value=True),
                patch.object(runner, "preflight"),
                patch.object(runner, "find_running_suite", return_value=[]),
                patch.object(runner, "candidate_identity", return_value={"git_head": "abc123"}),
                patch.object(runner, "test_assembly_identity", return_value=assembly),
                patch.object(runner.subprocess, "Popen", side_effect=fake_popen),
                patch.object(runner.shutil, "which", return_value=None),
            ):
                result = runner.main(args)

            receipt_path = pathlib.Path(temporary_directory) / "integration-receipt.jsonl"
            records = [
                json.loads(line)
                for line in receipt_path.read_text(encoding="utf-8").splitlines()
            ]
            logged_paths = [
                pathlib.Path(record["log"]).is_file()
                for record in records
                if record.get("event") == "run_finished" and record.get("log")
            ]
            return result, records, spawned_commands, logged_paths

    def test_main_builds_and_runs_with_caps_and_persists_receipt(self) -> None:
        result, records, commands, logs_exist = self.run_main_with_fake_processes(
            [
                "Build succeeded.\n",
                "Total: 2, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0\n",
            ],
            build=True,
        )

        self.assertEqual(0, result)
        self.assertEqual(2, len(commands))
        self.assertEqual("systemd-run", commands[0][0])
        self.assertIn("MemoryMax=4G", commands[0])
        self.assertIn("MemorySwapMax=1G", commands[0])
        dotnet_index = commands[0].index("dotnet")
        self.assertEqual(
            ["dotnet", "build", runner.PROJECT, "-c", "Debug"],
            commands[0][dotnet_index:],
        )
        build_start = next(
            record
            for record in records
            if record.get("event") == "run_started" and record.get("purpose") == "build"
        )
        self.assertEqual(commands[0], build_start["command"])
        invocation = next(record for record in records if record["event"] == "invocation_started")
        self.assertEqual(["Example.Tests"], invocation["scope"]["classes"])
        artifact = next(record for record in records if record["event"] == "artifact_observed")
        self.assertEqual("post-build", artifact["stage"])
        self.assertEqual("test-assembly-sha256", artifact["identity"]["sha256"])
        test_start = next(
            record
            for record in records
            if record.get("event") == "run_started" and record.get("purpose") == "integration-test"
        )
        self.assertEqual(["Example.Tests"], [test_start["test_class"]])
        test_finish = next(
            record
            for record in records
            if record.get("event") == "run_finished" and record.get("purpose") == "integration-test"
        )
        self.assertEqual("passed", test_finish["status"])
        self.assertEqual(0, test_finish["exit_code"])
        self.assertEqual("test-assembly-sha256", test_finish["test_assembly"]["sha256"])
        self.assertTrue(all(logs_exist))

    def test_zero_or_missing_summary_is_incomplete_not_passed(self) -> None:
        for output in (
            "",
            "Total: 0, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0\n",
        ):
            with self.subTest(output=output):
                result, records, _, _ = self.run_main_with_fake_processes([output])

                self.assertEqual(1, result)
                finished = next(
                    record
                    for record in records
                    if record.get("event") == "run_finished"
                    and record.get("purpose") == "integration-test"
                )
                self.assertEqual("incomplete", finished["status"])
                self.assertEqual(0, finished["exit_code"])

    def test_main_rejects_an_empty_provider_selection(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            with (
                patch.object(runner, "probe_systemd", return_value=True),
                contextlib.redirect_stderr(io.StringIO()),
            ):
                result = runner.main(
                    [
                        "--providers",
                        "",
                        "--log-dir",
                        temporary_directory,
                        "--dry-run",
                    ]
                )

        self.assertEqual(2, result)

    def test_external_endpoint_is_not_reported_as_torn_down(self) -> None:
        secret = "Server=127.0.0.1;Password=do-not-log"

        resource = runner.resource_lifecycle(
            "sqlserver",
            {"NEXTORM_SQLSERVER_CONNECTION": secret},
        )

        self.assertEqual("external or unspecified", resource["ownership"])
        self.assertEqual("not performed or verified by this runner", resource["cleanup"])
        self.assertNotIn(secret, json.dumps(resource))

    def test_runner_refuses_implicit_uncapped_execution(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            stderr = io.StringIO()
            fake_result = {
                "rc": 0,
                "summary": {"passed": 1, "failed": 0, "skipped": 0},
                "seconds": 0.0,
                "log": pathlib.Path(temporary_directory) / "fake.log",
                "class": "Example.Tests",
            }
            with (
                patch.object(runner, "probe_systemd", return_value=False),
                patch.object(runner, "preflight") as preflight,
                patch.object(runner, "find_running_suite", return_value=[]),
                patch.object(runner, "run_class", return_value=fake_result) as run_class,
                contextlib.redirect_stderr(stderr),
            ):
                result = runner.main(
                    ["--providers", "sqlite", "--log-dir", temporary_directory, "--gap", "0"]
                )

        self.assertEqual(2, result)
        self.assertFalse(preflight.called)
        self.assertFalse(run_class.called)
        self.assertIn("refusing an uncapped run", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
