#!/usr/bin/env python3
"""Bounded controlled-cohort mutation check (issue #341 / T341 deliverable D1).

The frozen Stryker pipeline on this repository could not produce a killed
mutant (rv1/rv2: 42 053 mutants created, 17 tested, 0 killed, score 0.00 %)
even though the same literal, hand-applied, is killed by the scoped tests
(P3: 8 failed / 4 passed, restored 12/12).  This helper replaces the mutation
score with a *bounded, hand-selected* cohort of two string mutations applied by
byte offset, and reports a controlled-cohort detection rate instead of a
Stryker score.

  python3 tools/stryker/bounded-scope.py --config tools/stryker/t341-config.json --max-mutants 2 --max-tests 12 --output <ABS DIR>
  python3 tools/stryker/bounded-scope.py --self-test restore-mismatch --output <ABS DIR>
  python3 tools/stryker/bounded-scope.py --self-test span-mismatch --output <ABS DIR>

Order of operations: resolve -> validate -> exclusive lock -> baseline -> each
mutation (always restored) -> restored baseline -> result.  A violation at any
step exits nonzero with an explicit message and never publishes a score.

Exit codes:
  0  cohort completed, result published
  2  usage error (argparse)
  3  configuration violation (nothing edited, nothing built)
  4  output directory already owned by another run
  5  baseline build/test is not the declared 12/12 green
  6  a mutation trial is INVALID
  7  restore mismatch (backup preserved at <output>/source.orig)
  8  the cohort result is not publishable (invalid trial, dirty restored
     baseline, or no killed+escaped pair)
  9  self-test failed (the production validator did not detect the defect)

Deliberately stdlib only, no third-party imports.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

TRX_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
BUILD_TIMEOUT = 180
TEST_TIMEOUT = 120
SCORE_LABEL = ("controlled-cohort detection rate "
               "(2 hand-selected string mutations; not a Stryker score)")
EXIT_OK = 0
EXIT_USAGE = 2
EXIT_CONFIG = 3
EXIT_LOCK = 4
EXIT_BASELINE = 5
EXIT_TRIAL = 6
EXIT_RESTORE = 7
EXIT_SCOPE = 8
EXIT_SELFTEST = 9

EXECUTED_OUTCOMES = ("Passed", "Failed")


class ConfigError(Exception):
    """A violation found before anything is edited or built."""


class TrialError(Exception):
    """A mutation trial could not produce a valid result."""


# ── small helpers ───────────────────────────────────────────────────────────


def utc_now() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: str) -> str:
    with open(path, "rb") as f:
        return sha256_bytes(f.read())


def write_bytes(path: str, data: bytes) -> None:
    parent = os.path.dirname(path)
    if parent:
        os.makedirs(parent, exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)


def write_json(path: str, doc) -> None:
    write_bytes(path, (json.dumps(doc, indent=2, ensure_ascii=False) + "\n").encode("utf-8"))


def inside(root: str, child: str) -> bool:
    root = os.path.realpath(root)
    child = os.path.realpath(child)
    try:
        return os.path.commonpath([root, child]) == root
    except ValueError:  # different drives
        return False


def dotted_env() -> dict:
    """Environment for the dotnet invocations (memory-bounded, as in rv1/rv2)."""
    env = os.environ.copy()
    env["DOTNET_GCHeapHardLimit"] = "0x80000000"
    env["DOTNET_gcServer"] = "0"
    return env


def resolve_output(raw: str) -> str:
    if not isinstance(raw, str) or not raw.strip():
        raise ConfigError("--output must be a non-empty absolute directory")
    if not os.path.isabs(raw):
        raise ConfigError("--output %r must be an absolute path inside %s" % (raw, REPO))
    resolved = os.path.realpath(raw)
    if not inside(REPO, resolved):
        raise ConfigError("--output %r escapes the repository root %r" % (raw, REPO))
    return resolved


# ── configuration ───────────────────────────────────────────────────────────


def repo_relative(raw, field: str) -> str:
    """Validate a repository-relative config path and return its absolute form."""
    if not isinstance(raw, str) or not raw.strip():
        raise ConfigError("%s must be a non-empty string" % field)
    if raw != raw.strip():
        raise ConfigError("%s %r has leading or trailing whitespace" % (field, raw))
    if os.path.isabs(raw) or raw.startswith("\\") or (len(raw) > 1 and raw[1] == ":"):
        raise ConfigError("%s %r must be repository-relative, not absolute" % (field, raw))
    if ".." in raw.replace("\\", "/").split("/"):
        raise ConfigError("%s %r escapes the repository root ('..')" % (field, raw))
    resolved = os.path.realpath(os.path.join(REPO, raw))
    if not inside(REPO, resolved):
        raise ConfigError("%s %r resolves outside the repository root" % (field, raw))
    return resolved


def locate_span(data: bytes, line, column, find_bytes: bytes) -> int:
    """Return the absolute byte offset of `find` at 1-based line:column.

    Byte-anchored on purpose: the tracked sources carry a UTF-8 BOM and CRLF,
    so text decoding would shift offsets and columns.
    """
    if not isinstance(line, int) or isinstance(line, bool) or line < 1:
        raise ConfigError("line must be an integer >= 1")
    if not isinstance(column, int) or isinstance(column, bool) or column < 1:
        raise ConfigError("column must be an integer >= 1 (1-based)")
    if not find_bytes:
        raise ConfigError("find must be a non-empty string")
    if b"\n" in find_bytes or b"\r" in find_bytes:
        raise ConfigError("find %r must sit on one source line" % find_bytes)
    occurrences = data.count(find_bytes)
    if occurrences != 1:
        raise ConfigError("find %r occurs %d times in the source; it must occur exactly once"
                          % (find_bytes, occurrences))
    lines = data.split(b"\n")
    if line > len(lines):
        raise ConfigError("line %d is outside the source (%d lines)" % (line, len(lines)))
    line_bytes = lines[line - 1]
    line_offset = line_bytes.find(find_bytes)
    if line_offset < 0:
        raise ConfigError("find %r does not occur on line %d (it occurs once elsewhere)"
                          % (find_bytes, line))
    if line_offset != column - 1:
        raise ConfigError("span mismatch: find %r sits at %d:%d (1-based) but the config declares "
                          "%d:%d; bytes at the declared span are %r"
                          % (find_bytes, line, line_offset + 1, line, column,
                             line_bytes[column - 1:column - 1 + len(find_bytes)]))
    offset = sum(len(part) + 1 for part in lines[:line - 1]) + line_offset
    if data[offset:offset + len(find_bytes)] != find_bytes:
        raise ConfigError("span mismatch: bytes at %d:%d are %r, find is %r"
                          % (line, column, data[offset:offset + len(find_bytes)], find_bytes))
    return offset


def apply_mutation(data: bytes, mutation: dict) -> tuple:
    """Apply one mutation to the baseline bytes; return (mutated, offset)."""
    find_bytes = mutation["find"].encode("utf-8")
    replace_bytes = mutation["replace"].encode("utf-8")
    offset = locate_span(data, mutation["line"], mutation["column"], find_bytes)
    mutated = data[:offset] + replace_bytes + data[offset + len(find_bytes):]
    if mutated == data:
        raise ConfigError("%s: find and replace are identical" % mutation["id"])
    if mutated[offset:offset + len(replace_bytes)] != replace_bytes:
        raise ConfigError("%s: the replacement bytes were not written at the declared span"
                          % mutation["id"])
    tail = offset + len(replace_bytes)
    if (mutated[:offset] != data[:offset]
            or mutated[tail:] != data[offset + len(find_bytes):]):
        raise ConfigError("%s: the mutation changed bytes outside the declared span" % mutation["id"])
    return mutated, offset


def load_config(path: str) -> dict:
    try:
        with open(path, "rb") as f:
            raw = f.read()
    except OSError as exc:
        raise ConfigError("cannot read --config %s: %s" % (path, exc))
    try:
        doc = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ConfigError("--config %s is not valid UTF-8 JSON: %s" % (path, exc))
    if not isinstance(doc, dict):
        raise ConfigError("--config %s must contain a JSON object" % path)
    return doc


def validate_config(doc: dict, max_mutants, max_tests) -> dict:
    if doc.get("version") != 1:
        raise ConfigError("version must be 1, got %r" % (doc.get("version"),))
    if not isinstance(max_mutants, int) or isinstance(max_mutants, bool) or max_mutants < 1:
        raise ConfigError("--max-mutants must be an integer >= 1")
    if not isinstance(max_tests, int) or isinstance(max_tests, bool) or max_tests < 1:
        raise ConfigError("--max-tests must be an integer >= 1")

    source_raw = doc.get("source")
    source_abs = repo_relative(source_raw, "source")
    if not os.path.isfile(source_abs):
        raise ConfigError("source %r does not exist" % source_raw)
    declared_sha = doc.get("source_sha256")
    if (not isinstance(declared_sha, str) or len(declared_sha) != 64
            or any(c not in "0123456789abcdef" for c in declared_sha)):
        raise ConfigError("source_sha256 must be 64 lowercase hex characters")
    actual_sha = sha256_file(source_abs)
    if actual_sha != declared_sha:
        raise ConfigError("source_sha256 mismatch: config declares %s, %s has %s"
                          % (declared_sha, source_raw, actual_sha))

    test_project_raw = doc.get("test_project")
    test_project_abs = repo_relative(test_project_raw, "test_project")
    if not os.path.isfile(test_project_abs) or not test_project_abs.endswith(".csproj"):
        raise ConfigError("test_project %r is not an existing .csproj" % test_project_raw)
    for field in ("test_assembly", "test_filter", "test_class", "configuration"):
        value = doc.get(field)
        if not isinstance(value, str) or not value.strip():
            raise ConfigError("%s must be a non-empty string" % field)
    assembly = doc["test_assembly"]
    if os.path.basename(assembly) != assembly or "/" in assembly or "\\" in assembly:
        raise ConfigError("test_assembly %r must be a bare file name" % assembly)

    bounds = doc.get("bounds")
    if not isinstance(bounds, dict):
        raise ConfigError("bounds must be an object")
    for field in ("max_mutants", "max_tests"):
        value = bounds.get(field)
        if not isinstance(value, int) or isinstance(value, bool) or value < 1:
            raise ConfigError("bounds.%s must be an integer >= 1" % field)

    expected = doc.get("expected_tests")
    if not isinstance(expected, list) or not expected:
        raise ConfigError("expected_tests must be a non-empty array")
    if not all(isinstance(name, str) and name.strip() for name in expected):
        raise ConfigError("expected_tests must contain non-empty strings")
    if len(set(expected)) != len(expected):
        raise ConfigError("expected_tests contains duplicates")
    if len(expected) > bounds["max_tests"]:
        raise ConfigError("expected_tests has %d entries, more than bounds.max_tests=%d"
                          % (len(expected), bounds["max_tests"]))
    if len(expected) > max_tests:
        raise ConfigError("expected_tests has %d entries, more than --max-tests %d"
                          % (len(expected), max_tests))

    mutations = doc.get("mutations")
    if not isinstance(mutations, list) or not mutations:
        raise ConfigError("mutations must be a non-empty array (an empty cohort is a failure)")
    if len(mutations) > bounds["max_mutants"]:
        raise ConfigError("mutations has %d entries, more than bounds.max_mutants=%d"
                          % (len(mutations), bounds["max_mutants"]))
    if len(mutations) > max_mutants:
        raise ConfigError("mutations has %d entries, more than --max-mutants %d"
                          % (len(mutations), max_mutants))

    source_bytes = open(source_abs, "rb").read()
    seen_ids = set()
    roles = []
    for mutation in mutations:
        if not isinstance(mutation, dict):
            raise ConfigError("every mutation must be an object")
        ident = mutation.get("id")
        if not isinstance(ident, str) or not ident.strip():
            raise ConfigError("every mutation needs a non-empty id")
        if ident in seen_ids:
            raise ConfigError("duplicate mutation id %r" % ident)
        if "/" in ident or "\\" in ident or " " in ident:
            raise ConfigError("mutation id %r must not contain spaces or path separators" % ident)
        seen_ids.add(ident)
        role = mutation.get("role")
        if role not in ("positive-control", "escape-control"):
            raise ConfigError("%s: role must be positive-control or escape-control" % ident)
        roles.append(role)
        find = mutation.get("find")
        replace = mutation.get("replace")
        if not isinstance(find, str) or not isinstance(replace, str):
            raise ConfigError("%s: find and replace must be strings" % ident)
        if not find:
            raise ConfigError("%s: find must be non-empty" % ident)
        if find == replace:
            raise ConfigError("%s: find and replace are identical" % ident)
        if not isinstance(mutation.get("line"), int) or not isinstance(mutation.get("column"), int):
            raise ConfigError("%s: line and column must be integers" % ident)
        locate_span(source_bytes, mutation["line"], mutation["column"], find.encode("utf-8"))

    if roles.count("positive-control") != 1:
        raise ConfigError("the cohort needs exactly one positive-control, found %d"
                          % roles.count("positive-control"))
    if roles.count("escape-control") != 1:
        raise ConfigError("the cohort needs exactly one escape-control, found %d"
                          % roles.count("escape-control"))

    return {
        "raw": doc,
        "source": source_raw,
        "source_abs": source_abs,
        "source_sha256": declared_sha,
        "test_project": test_project_raw,
        "test_assembly": doc["test_assembly"],
        "test_filter": doc["test_filter"],
        "test_class": doc["test_class"],
        "configuration": doc["configuration"],
        "expected_tests": list(expected),
        "mutations": mutations,
        "bounds": bounds,
    }


# ── command recording ───────────────────────────────────────────────────────


class Recorder:
    """Run argv lists without a shell, tee the output, record the exact argv."""

    def __init__(self, outdir: str) -> None:
        self.outdir = outdir
        self.items: list = []
        self.path = os.path.join(outdir, "commands-executed.json")

    def run(self, step: str, argv: list, timeout: int) -> tuple:
        log_path = os.path.join(self.outdir, step + ".log")
        started = time.time()
        timed_out = False
        try:
            proc = subprocess.run(argv, cwd=REPO, env=dotted_env(), timeout=timeout,
                                  capture_output=True)
            code = proc.returncode
            stdout, stderr = proc.stdout, proc.stderr
        except subprocess.TimeoutExpired as exc:
            timed_out = True
            code = None
            stdout = exc.stdout or b""
            stderr = exc.stderr or b""
        except OSError as exc:
            timed_out = False
            code = None
            stdout = b""
            stderr = ("cannot execute %r: %s\n" % (argv[0], exc)).encode("utf-8")
        elapsed = round(time.time() - started, 3)
        header = ("argv: %s\ncwd: %s\ntimeout_s: %d\nelapsed_s: %s\nexit: %s\ntimed_out: %s\n"
                  % (json.dumps(argv), REPO, timeout, elapsed, code, timed_out)).encode("utf-8")
        write_bytes(log_path, header + b"--- stdout ---\n" + stdout + b"\n--- stderr ---\n" + stderr)
        self.items.append({
            "step": step,
            "argv": argv,
            "cwd": REPO,
            "env": {"DOTNET_GCHeapHardLimit": "0x80000000", "DOTNET_gcServer": "0"},
            "timeout_s": timeout,
            "elapsed_s": elapsed,
            "exit": code,
            "timed_out": timed_out,
            "log": log_path,
        })
        write_json(self.path, {"version": 1, "generated_utc": utc_now(), "commands": self.items})
        return code, log_path


# ── TRX parsing ─────────────────────────────────────────────────────────────


def read_trx(path: str) -> list:
    if not os.path.isfile(path):
        raise TrialError("TRX not found at %s" % path)
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as exc:
        raise TrialError("TRX %s is not parseable: %s" % (path, exc))
    results = []
    for element in root.iter(TRX_NS + "UnitTestResult"):
        results.append({"name": element.get("testName") or "",
                        "outcome": element.get("outcome") or ""})
    return results


def classify_results(results: list, expected: list) -> dict:
    names = [item["name"] for item in results]
    passed = [item["name"] for item in results if item["outcome"] == "Passed"]
    failed = [item["name"] for item in results if item["outcome"] == "Failed"]
    other = [(item["name"], item["outcome"]) for item in results
             if item["outcome"] not in EXECUTED_OUTCOMES]
    problems = []
    if len(set(names)) != len(names):
        problems.append("duplicate test results")
    extra = sorted(set(names) - set(expected))
    missing = sorted(set(expected) - set(names))
    if extra:
        problems.append("unexpected tests: %s" % ", ".join(extra))
    if missing:
        problems.append("missing tests: %s" % ", ".join(missing))
    if other:
        problems.append("not executed: %s" % "; ".join("%s=%s" % pair for pair in other))
    return {
        "results": len(results),
        "passed": len(passed),
        "failed": len(failed),
        "not_executed": len(other),
        "problems": problems,
    }


def build_argv(cfg: dict, bin_dir: str) -> list:
    return ["dotnet", "build", cfg["test_project"], "-c", cfg["configuration"],
            "--no-incremental", "--output", bin_dir]


def test_argv(cfg: dict, bin_dir: str, results_dir: str, trx_name: str) -> list:
    return ["dotnet", "vstest", os.path.join(bin_dir, cfg["test_assembly"]),
            "/TestCaseFilter:" + cfg["test_filter"],
            "/Logger:trx;LogFileName=%s.trx" % trx_name,
            "/ResultsDirectory:" + results_dir]


# ── restore validation ──────────────────────────────────────────────────────


def validate_restored(target_path: str, backup_bytes: bytes, expected_sha: str):
    """Production restore validator: None when restored, an error message otherwise."""
    try:
        with open(target_path, "rb") as f:
            actual = f.read()
    except OSError as exc:
        return "restored target %s is unreadable: %s" % (target_path, exc)
    if actual != backup_bytes:
        return ("restored bytes differ from the backup (%d vs %d bytes, sha256 %s vs %s)"
                % (len(actual), len(backup_bytes), sha256_bytes(actual), expected_sha))
    actual_sha = sha256_bytes(actual)
    if actual_sha != expected_sha:
        return "restored sha256 %s != declared source_sha256 %s" % (actual_sha, expected_sha)
    return None


def restore_source(source_abs: str, backup_bytes: bytes, expected_sha: str):
    """Write the backup back and verify it; returns None on success."""
    try:
        write_bytes(source_abs, backup_bytes)
    except OSError as exc:
        return "cannot write the restored bytes: %s" % exc
    return validate_restored(source_abs, backup_bytes, expected_sha)


# ── cohort execution ────────────────────────────────────────────────────────


def finish(result: dict, outdir: str, reason, code: int) -> int:
    if reason:
        result["score"] = None
        result["score_label"] = SCORE_LABEL
        result["reason"] = reason
    write_json(os.path.join(outdir, "result.json"), result)
    if reason:
        print("bounded-scope: FAILED: %s" % reason, file=sys.stderr)
        print("bounded-scope: result (no score): %s"
              % os.path.join(outdir, "result.json"), file=sys.stderr)
        return code
    print(json.dumps({
        "tests": result["tests"],
        "killed": result["killed"],
        "escaped": result["escaped"],
        "invalid": result["invalid"],
        "score": result["score"],
        "score_label": result["score_label"],
        "result": os.path.join(outdir, "result.json"),
    }, indent=2))
    return code


def run_trial(rec: Recorder, cfg: dict, outdir: str, mutation: dict,
              backup: bytes, baseline_sha: str) -> dict:
    ident = mutation["id"]
    step_dir = os.path.join(outdir, ident)
    bin_dir = os.path.join(step_dir, "bin")
    entry = {
        "id": ident,
        "role": mutation["role"],
        "line": mutation["line"],
        "column": mutation["column"],
        "find": mutation["find"],
        "replace": mutation["replace"],
        "before_sha256": baseline_sha,
        "status": "INVALID",
        "reason": None,
        "restored": None,
    }
    try:
        mutated, offset = apply_mutation(backup, mutation)
        entry["span_offset"] = offset
        write_bytes(cfg["source_abs"], mutated)
        after = sha256_file(cfg["source_abs"])
        entry["after_sha256"] = after
        if after == baseline_sha:
            raise TrialError("the mutated source has the baseline sha256 (mutation not applied)")
        if open(cfg["source_abs"], "rb").read() != mutated:
            raise TrialError("the written source does not match the mutated bytes")

        code, log = rec.run(ident + "-build", build_argv(cfg, bin_dir), BUILD_TIMEOUT)
        entry["build_exit"] = code
        entry["build_log"] = log
        core_dll = os.path.join(bin_dir, "nextorm.core.dll")
        test_dll = os.path.join(bin_dir, cfg["test_assembly"])
        if os.path.isfile(core_dll):
            entry["core_assembly_sha256"] = sha256_file(core_dll)
        if os.path.isfile(test_dll):
            entry["assembly_sha256"] = sha256_file(test_dll)
        if code is None:
            raise TrialError("build timed out after %d s" % BUILD_TIMEOUT)
        if code != 0:
            raise TrialError("build failed with exit %d" % code)

        step_trx = ident
        code, log = rec.run(ident + "-test",
                            test_argv(cfg, bin_dir, step_dir, step_trx), TEST_TIMEOUT)
        entry["test_exit"] = code
        entry["test_log"] = log
        entry["trx_path"] = os.path.join(step_dir, step_trx + ".trx")
        if code is None:
            raise TrialError("test run timed out after %d s" % TEST_TIMEOUT)

        results = read_trx(entry["trx_path"])
        summary = classify_results(results, cfg["expected_tests"])
        entry.update({key: summary[key] for key in
                      ("results", "passed", "failed", "not_executed", "problems")})
        if summary["problems"]:
            raise TrialError("identity drift: %s" % "; ".join(summary["problems"]))
        if summary["failed"] > 0:
            entry["status"] = "KILLED"
        elif summary["passed"] == len(cfg["expected_tests"]):
            entry["status"] = "ESCAPED"
        else:
            raise TrialError("no result for every expected test (%d passed, %d failed)"
                             % (summary["passed"], summary["failed"]))
        return entry
    except (TrialError, ConfigError) as exc:
        entry["status"] = "INVALID"
        entry["reason"] = str(exc)
        return entry
    finally:
        error = restore_source(cfg["source_abs"], backup, cfg["source_sha256"])
        entry["restored"] = error is None
        if error is not None:
            entry["restore_error"] = error
            entry["backup_path"] = os.path.join(outdir, "source.orig")


def run_baseline(rec: Recorder, cfg: dict, outdir: str, label: str, step: str) -> dict:
    bin_dir = os.path.join(outdir, label, "bin")
    results_dir = os.path.join(outdir, label)
    record = {"label": label}
    code, log = rec.run(step + "-build", build_argv(cfg, bin_dir), BUILD_TIMEOUT)
    record["build_exit"] = code
    record["build_log"] = log
    if code is None:
        raise TrialError("%s build timed out after %d s" % (label, BUILD_TIMEOUT))
    if code != 0:
        raise TrialError("%s build failed with exit %d" % (label, code))
    code, log = rec.run(step + "-test", test_argv(cfg, bin_dir, results_dir, step), TEST_TIMEOUT)
    record["test_exit"] = code
    record["test_log"] = log
    record["trx_path"] = os.path.join(results_dir, step + ".trx")
    if code is None:
        raise TrialError("%s test run timed out after %d s" % (label, TEST_TIMEOUT))
    results = read_trx(record["trx_path"])
    summary = classify_results(results, cfg["expected_tests"])
    record.update({key: summary[key] for key in
                   ("results", "passed", "failed", "not_executed", "problems")})
    record["green"] = (code == 0 and summary["passed"] == len(cfg["expected_tests"])
                       and summary["failed"] == 0 and not summary["problems"])
    if not record["green"]:
        detail = "; ".join(summary["problems"]) or ("passed %d, failed %d, exit %s"
                                                   % (summary["passed"], summary["failed"], code))
        raise TrialError("%s is not the declared %d/%d green: %s"
                         % (label, len(cfg["expected_tests"]), len(cfg["expected_tests"]), detail))
    return record


def execute(cfg: dict, outdir: str) -> int:
    rec = Recorder(outdir)
    result = {
        "version": 1,
        "generated_utc": utc_now(),
        "source": cfg["source"],
        "source_sha256": cfg["source_sha256"],
        "test_project": cfg["test_project"],
        "test_filter": cfg["test_filter"],
        "test_class": cfg["test_class"],
        "configuration": cfg["configuration"],
        "tests": len(cfg["expected_tests"]),
        "killed": 0,
        "escaped": 0,
        "invalid": 0,
        "score": None,
        "score_label": SCORE_LABEL,
        "baseline": None,
        "restored_baseline": None,
        "mutations": [],
    }

    backup = open(cfg["source_abs"], "rb").read()
    baseline_sha = sha256_bytes(backup)
    if baseline_sha != cfg["source_sha256"]:
        return finish(result, outdir,
                      "source changed between validation and the baseline (sha256 %s != %s)"
                      % (baseline_sha, cfg["source_sha256"]), EXIT_CONFIG)
    write_bytes(os.path.join(outdir, "source.orig"), backup)

    try:
        result["baseline"] = run_baseline(rec, cfg, outdir, "baseline", "baseline")
    except TrialError as exc:
        return finish(result, outdir, str(exc), EXIT_BASELINE)

    for mutation in cfg["mutations"]:
        entry = run_trial(rec, cfg, outdir, mutation, backup, baseline_sha)
        result["mutations"].append(entry)
        if entry["restored"] is False:
            return finish(result, outdir,
                          "%s: restore mismatch: %s (backup preserved at %s)"
                          % (entry["id"], entry.get("restore_error"),
                             os.path.join(outdir, "source.orig")), EXIT_RESTORE)

    try:
        result["restored_baseline"] = run_baseline(rec, cfg, outdir, "restored", "restored")
    except TrialError as exc:
        return finish(result, outdir, "restored baseline: %s" % exc, EXIT_SCOPE)

    current_sha = sha256_file(cfg["source_abs"])
    result["restoration"] = {
        "verified": (current_sha == cfg["source_sha256"]
                     and all(item["restored"] for item in result["mutations"])),
        "source_sha256": current_sha,
        "expected_sha256": cfg["source_sha256"],
    }

    result["killed"] = sum(1 for item in result["mutations"] if item["status"] == "KILLED")
    result["escaped"] = sum(1 for item in result["mutations"] if item["status"] == "ESCAPED")
    result["invalid"] = sum(1 for item in result["mutations"] if item["status"] == "INVALID")

    reasons = []
    if result["invalid"]:
        reasons.append("%d INVALID trial(s): %s"
                       % (result["invalid"],
                          "; ".join("%s: %s" % (item["id"], item["reason"])
                                    for item in result["mutations"] if item["status"] == "INVALID")))
    if not result["restoration"]["verified"]:
        reasons.append("restoration not verified (source sha256 %s != %s)"
                       % (current_sha, cfg["source_sha256"]))
    if result["killed"] < 1 or result["escaped"] < 1:
        reasons.append("no killed+escaped pair (killed %d, escaped %d)"
                       % (result["killed"], result["escaped"]))
    if reasons:
        code = EXIT_TRIAL if result["invalid"] else EXIT_SCOPE
        return finish(result, outdir, "; ".join(reasons), code)

    result["score"] = result["killed"] / (result["killed"] + result["escaped"])
    return finish(result, outdir, None, EXIT_OK)


def cmd_run(args) -> int:
    try:
        outdir = resolve_output(args.output)
        os.makedirs(outdir, exist_ok=True)
    except OSError as exc:
        print("bounded-scope: REJECTED (output): cannot create %r: %s" % (args.output, exc),
              file=sys.stderr)
        return EXIT_CONFIG
    except ConfigError as exc:
        print("bounded-scope: REJECTED (output): %s" % exc, file=sys.stderr)
        return EXIT_CONFIG

    config_path = args.config if os.path.isabs(args.config) else os.path.join(REPO, args.config)
    try:
        cfg = validate_config(load_config(config_path), args.max_mutants, args.max_tests)
    except ConfigError as exc:
        write_json(os.path.join(outdir, "rejection.json"), {
            "version": 1,
            "generated_utc": utc_now(),
            "ok": False,
            "stage": "config",
            "config": args.config,
            "max_mutants": args.max_mutants,
            "max_tests": args.max_tests,
            "message": str(exc),
        })
        print("bounded-scope: REJECTED (config): %s" % exc, file=sys.stderr)
        print("bounded-scope: no source edit, no build, no score", file=sys.stderr)
        return EXIT_CONFIG

    lock_path = os.path.join(outdir, ".bounded-scope.lock")
    try:
        fd = os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o644)
    except FileExistsError:
        print("bounded-scope: REFUSED: %s already exists - another run owns this output directory"
              % lock_path, file=sys.stderr)
        return EXIT_LOCK
    try:
        os.write(fd, (json.dumps({"pid": os.getpid(), "utc": utc_now(),
                                  "config": config_path}) + "\n").encode("utf-8"))
        os.close(fd)
        fd = None
        return execute(cfg, outdir)
    finally:
        if fd is not None:
            os.close(fd)
        try:
            os.unlink(lock_path)
        except OSError:
            pass


# ── self-tests ──────────────────────────────────────────────────────────────


def self_test_restore_mismatch() -> dict:
    """Corrupt a restored fixture and require the production validator to reject it."""
    cases = []
    tmp = tempfile.mkdtemp(prefix="bounded-scope-restore-")
    target = os.path.join(tmp, "Restored.txt")
    backup = b"namespace Fixture;\r\n// original bytes\r\n"
    expected = sha256_bytes(backup)

    write_bytes(target, backup)
    accepted = validate_restored(target, backup, expected)
    cases.append({"case": "intact-restore-accepted",
                  "passed": accepted is None,
                  "detail": accepted})

    corrupted = backup.replace(b"original", b"corruptd")
    write_bytes(target, corrupted)
    detected = validate_restored(target, backup, expected)
    cases.append({"case": "corrupted-restore-rejected",
                  "passed": detected is not None,
                  "detail": detected})

    write_bytes(target, backup)
    detected_sha = validate_restored(target, backup, "0" * 64)
    cases.append({"case": "sha-mismatch-rejected",
                  "passed": detected_sha is not None,
                  "detail": detected_sha})

    os.unlink(target)
    detected_missing = validate_restored(target, backup, expected)
    cases.append({"case": "missing-target-rejected",
                  "passed": detected_missing is not None,
                  "detail": detected_missing})
    return {"self_test": "restore-mismatch", "cases": cases,
            "passed": all(case["passed"] for case in cases)}


def self_test_span_mismatch() -> dict:
    """Require the production byte-span locator to reject wrong spans."""
    cases = []
    data = b"line one\r\n    var sql = $\"CASE WHEN {key} IS NULL\";\r\nlast\r\n"
    expected_offset = data.index(b"CASE WHEN ")
    located = locate_span(data, 2, 17, b"CASE WHEN ")
    cases.append({"case": "declared-span-accepted",
                  "passed": located == expected_offset,
                  "detail": "offset %d" % located})

    for label, line, column, needle in (
            ("wrong-column-rejected", 2, 16, b"CASE WHEN "),
            ("wrong-line-rejected", 3, 17, b"CASE WHEN "),
            ("absent-find-rejected", 2, 17, b"CASE  WHEN ")):
        try:
            locate_span(data, line, column, needle)
            detail = "accepted (defect)"
            passed = False
        except ConfigError as exc:
            detail = str(exc)
            passed = True
        cases.append({"case": label, "passed": passed, "detail": detail})

    try:
        locate_span(data + data, 2, 17, b"CASE WHEN ")
        detail = "accepted (defect)"
        passed = False
    except ConfigError as exc:
        detail = str(exc)
        passed = True
    cases.append({"case": "duplicate-find-rejected", "passed": passed, "detail": detail})
    return {"self_test": "span-mismatch", "cases": cases,
            "passed": all(case["passed"] for case in cases)}


def cmd_self_test(args) -> int:
    try:
        outdir = resolve_output(args.output)
    except ConfigError as exc:
        print("bounded-scope: REJECTED (output): %s" % exc, file=sys.stderr)
        return EXIT_CONFIG
    os.makedirs(outdir, exist_ok=True)

    if args.self_test == "restore-mismatch":
        report = self_test_restore_mismatch()
    else:
        report = self_test_span_mismatch()
    report["generated_utc"] = utc_now()
    write_json(os.path.join(outdir, "self-test-%s.json" % args.self_test), report)
    print(json.dumps(report, indent=2))
    if report["passed"]:
        return EXIT_OK
    print("bounded-scope: SELF-TEST FAILED: the production validator did not detect %s"
          % ", ".join(case["case"] for case in report["cases"] if not case["passed"]),
          file=sys.stderr)
    return EXIT_SELFTEST


# ── CLI ─────────────────────────────────────────────────────────────────────


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(
        prog="bounded-scope.py", description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--config", help="repository-relative path to the cohort config JSON")
    parser.add_argument("--max-mutants", type=int, help="hard bound on the cohort size")
    parser.add_argument("--max-tests", type=int, help="hard bound on the expected test count")
    parser.add_argument("--output", required=True,
                        help="absolute output directory inside the checkout (evidence root)")
    parser.add_argument("--self-test", choices=("restore-mismatch", "span-mismatch"),
                        help="run a negative self-test instead of the cohort")
    args = parser.parse_args(argv)

    if args.self_test is not None:
        return cmd_self_test(args)

    missing = [name for name, value in (("--config", args.config),
                                        ("--max-mutants", args.max_mutants),
                                        ("--max-tests", args.max_tests)) if value is None]
    if missing:
        parser.error("missing required arguments: %s" % ", ".join(missing))
    return cmd_run(args)


if __name__ == "__main__":
    sys.exit(main())
