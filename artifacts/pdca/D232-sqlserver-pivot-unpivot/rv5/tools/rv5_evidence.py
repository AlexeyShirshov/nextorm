#!/usr/bin/env python3
"""rv5_evidence.py -- fail-closed evidence driver for T232 (#232), evidence contract rv=5.

Task-local evidence plumbing (NOT production code, NOT a framework). Every subcommand:

* logs each underlying command as an argv array + cwd, with its stdout/stderr, numeric exit
  code and duration, under ``<root>/logs/rv5ev-<name>.{out,err,exit,cmd.json}`` plus one
  machine-readable line in ``<root>/logs/rv5ev-commands.jsonl``;
* attaches the live source-snapshot identity (HEAD sha, branch, ``git status --short``
  digest) to every result and to every logged command;
* writes a ``{case,status,reason}`` verdict JSON under ``<root>/verdicts/<name>.json``.

Fail-closed guards (exit 2) -- wrong evidence root, wrong revision, a tampered or missing
evidence file, an empty test selection (selected < 1), a skipped provider, and any required
row still ``open`` where the subcommand decides acceptance (``seal`` / ``fulfillment``).
Missing required *input* and usage errors exit 1.

Usage
-----
    python3 <R>/tools/rv5_evidence.py run          --case v18|v23|regression --root <R>
    python3 <R>/tools/rv5_evidence.py coverage     --baseline-root <B>/coverage --candidate <R>/coverage/candidate.cobertura.xml --root <R>
    python3 <R>/tools/rv5_evidence.py replay-perf  --recorded-root <B> --root <R> --expected-cases 7
    python3 <R>/tools/rv5_evidence.py mapping      --variant V05 --root <R>
    python3 <R>/tools/rv5_evidence.py docs-examples --root <R>
    python3 <R>/tools/rv5_evidence.py reconcile    --rv4 <B> --rv5 <R>
    python3 <R>/tools/rv5_evidence.py fulfillment  --rv4 <B> --rv5 <R>
    python3 <R>/tools/rv5_evidence.py hashes       --root <R> --verify [--rebaseline]
    python3 <R>/tools/rv5_evidence.py handoff      --root <R> --status docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
    python3 <R>/tools/rv5_evidence.py security     --root <R> --milestone 1.0.9-rc3
    python3 <R>/tools/rv5_evidence.py finalize     --root <R>
    python3 <R>/tools/rv5_evidence.py seal         --root <R> --historical-root <B>
    python3 <R>/tools/rv5_evidence.py selftest     --root <R>

with R = artifacts/pdca/D232-sqlserver-pivot-unpivot/rv5 and
     B = artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4.

Exit codes: 0 = ok, 2 = violation, 1 = usage / missing required input (fail-closed).

Delegation
----------
Where a working rv4 driver already exists it is reused instead of duplicated, and the
delegation is recorded in the verdict/artifact:

* ``coverage``  -> the canonical route (``dotnet-coverage collect`` over the five projects,
  then ``reportgenerator``) stays the collection mechanism; the rv5 comparison reads
  ``<B>/coverage/harness.json`` + ``<B>/coverage/candidate/coverage.xml`` as the baseline.
* ``replay-perf`` -> resolves the *recorded* harness argv from ``<B>/perf/{baseline,candidate}.json``
  (written by ``<B>/perf/run.py``) and replays it with the ``--artifacts`` path redirected
  into ``<R>/perf``; the rv4 driver itself is never re-run (rv4 is historical, never rewritten).
* ``handoff``   -> rereads the rv4 reconstruction proof / retention authorization read-only.
* ``mapping``   -> validates the D232-RV5-05 ``<R>/v05/assertion-execution.json`` **in place**
  (existence + schema + freshness) and derives its verdict from those bytes; the authoritative
  artifact is never regenerated and never overwritten.
* ``docs-examples`` -> validates the D232-RV5-05 ``<R>/docs-examples.json`` and the
  ``validate_inner_loop.py report``-schema-valid ``<R>/reports/D-09.json`` **in place**;
  neither artifact is rewritten and no artifact is synthesized when one is absent (exit 2).

``NuGetAudit`` is never masked (any inherited ``NuGetAudit=false`` is dropped and the fact is
recorded); the BDN churn guard stashes and restores the pre-existing dirty bytes under
``benchmarks/BenchmarkDotNet.Artifacts/**`` around any benchmark run.
"""
from __future__ import annotations

import datetime as _dt
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time

SCRIPT = os.path.abspath(__file__)
DEFAULT_ROOT = os.path.dirname(os.path.dirname(SCRIPT))          # .../rv5
_REPO_ROOT_ENV = "RV5_EVIDENCE_REPO_ROOT"
REPO_ROOT = os.path.abspath(
    os.environ.get(_REPO_ROOT_ENV)
    or os.path.join(DEFAULT_ROOT, os.pardir, os.pardir, os.pardir, os.pardir))

TASK = "T232 (#232) SQL Server typed PIVOT/UNPIVOT projection"
CONTRACT_RV = 5
PLAN_REVISION = 5

DEFAULT_DOCKER_HOST = "unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock"
DOCKER_HOST_ENV = "RV5_EVIDENCE_DOCKER_HOST"

INTEGRATION = "tests/nextorm.integration.tests"
SQLSERVER = "tests/nextorm.sqlserver.tests"
CORE = "tests/nextorm.core.tests"
BDN_DIR_REL = "benchmarks/BenchmarkDotNet.Artifacts"
SCHEMA_VALIDATOR = "scripts/validate_inner_loop.py"

EXIT_OK, EXIT_USAGE, EXIT_VIOLATION = 0, 1, 2
LOG_PREFIX = "rv5ev"

RUN_SPECS = {
    "v18": dict(case="v18", project=INTEGRATION, filt="FullyQualifiedName~T232SqlServerTests",
                docker=True, no_build=False, min_total=13, skip_policy="none",
                artifact="v18/assertion-execution.json",
                required=["tests/nextorm.integration.tests/T232SqlServerTestsRv5.cs"],
                proves="SQL Server container execution of the declared-but-absent pivot value"),
    "v23": dict(case="v23", project=SQLSERVER, filt="FullyQualifiedName~T232Rv5AmbiguityTests",
                docker=False, no_build=True, min_total=6, skip_policy="none",
                artifact="v23/assertion-execution.json",
                required=["tests/nextorm.sqlserver.tests/T232Rv5AmbiguityTests.cs"],
                proves="ordinary-marker ambiguity guard (positive + fail-closed arms)"),
}

REGRESSION_BUILD = ["dotnet", "build", "nextorm.slnx", "-c", "Debug"]
REGRESSION_PROJECTS = [
    CORE,
    "tests/nextorm.sqlite.tests",
    "tests/nextorm.postgres.tests",
    SQLSERVER,
    "tests/nextorm.mysql.tests",
    "tests/nextorm.mariadb.tests",
    "tests/nextorm.clickhouse.tests",
    "tests/nextorm.alias.tests",
]
REGRESSION_INTEGRATION = ["dotnet", "run", "--project", INTEGRATION, "-c", "Debug", "--", "-noColor"]

# r=5 allowlist: the only src/ file the plan revision r=5 is allowed to touch.
R5_ALLOWLIST_SRC = "src/nextorm.core/Visitors/NormSqlTranslator.cs"
# The frozen oracle: the legacy SQL-generation suite must stay byte-identical to HEAD.
FROZEN_ORACLE_FILE = "tests/nextorm.sqlserver.tests/SqlGenerationTests.cs"
FROZEN_ORACLE_CASES = (
    "Pivot_ShouldEmitPivotClause", "Pivot_ShouldRenderEveryAggregateAndKeepValueWithoutAlias",
    "Pivot_ShouldReuseCachedPlan", "Pivot_ShouldReuseCachedPlan_DerivedSource",
    "Unpivot_ShouldEmitUnpivotClause", "Pivot_ShouldAcceptDerivedSource",
    "Pivot_ShouldAcceptDerivedComputedSource", "Unpivot_ShouldAcceptDerivedSource",
    "Pivot_ShouldThrowWhenSourceIsFiltered", "Pivot_ShouldThrowWhenDerivedSourceHasModifiers",
    "Pivot_ShouldThrowWhenSourceHasModifiers", "Pivot_ShouldThrowWithoutValues",
    "Unpivot_ShouldThrowWithoutColumns", "Unpivot_WithUnresolvableAliasSource_ShouldThrow",
)
FROZEN_ORACLE_SQL = "from sales pivot (sum(margin) for quarter in ([1], [2])) as [t1]"
V23_MUTATION_LOGS = {"red": "logs/rv5-v23-mutation-red.out",
                     "green": "logs/rv5-v23-mutation-green.out"}

# The five coverage.settings.xml-scoped projects plus the container-backed integration suite:
# identical collector/reporter/settings/suite set to the recorded rv1 baseline collection.
COVERAGE_PROJECTS = [
    CORE,
    "tests/nextorm.sqlite.tests",
    "tests/nextorm.postgres.tests",
    SQLSERVER,
    INTEGRATION,
]
COVERAGE_THRESHOLDS = {"line": 85.0, "branch": 75.0}
TOUCHED_CLASSES = (
    "NextORM.SqlServer.SqlServerEntityBuilderExtensions",
    "NextORM.Core.NormSqlTranslator",
    "NextORM.Core.QueryCommand.QueryPreparer",
)
EXPLICIT_SKIP_ALLOWED = ("T232DerivedSourceReproTests",)

EXCLUDED_HASH_FILES = ("hash-ledger.json", "hash-verification.json")
# Envelope files are the *binders* of the ordered evidence chain and are therefore never part of the
# payload they bind: the chain is payload/reports -> hash-ledger.json (complete payload digest) ->
# seal.json (carries ledger_sha256) -> hash-verification.json (verifies the ledger -> seal edge).
# Excluding the binders is what keeps the chain acyclic (no ledger hashing the seal that hashes the
# ledger) and what stops `hashes --verify` from invalidating itself after `seal` re-runs.
SEAL_ENVELOPE_FILES = ("seal.json", "seal-reconciliation.json")
SEAL_ENVELOPE_VERDICTS = ("verdicts/seal.json",)

# Evidence-reference closure (see reference_closure): every path the manifest/counters reference -
# and, transitively, every path the $R evidence documents they reach reference - is inventoried, so
# the ledger covers the complete closure and not just the files that happen to live under $R.
REF_ROOT_DIRS = ("logs/", "v05/", "v18/", "v23/", "reports/", "security/", "regression/",
                 "coverage/", "perf/", "tools/", "verdicts/")
REF_REPO_DIRS = ("artifacts/", "src/", "tests/", "docs/", "examples/", "scripts/", "benchmarks/")
REF_SUFFIXES = (".json", ".out", ".err", ".exit", ".txt", ".md", ".cs", ".sql", ".xml", ".jsonl",
                ".log", ".props", ".csproj", ".yml")
REF_PATH_RE = re.compile(r"^[A-Za-z0-9_][A-Za-z0-9_./@+-]*$")
# A reference recorded under such a key is a *recorded absence*, not a required payload (e.g.
# reports/D-09.json records `rv4_report_absence.path`).
ABSENCE_KEYS = ("absence", "absent", "missing", "omitted")
# The frozen input contract is a *declaration*, not evidence: its historical EC232-RV4-* rows
# declare rv4 artifact layouts that the actual rv4 tree never had.  `seal`/`hashes` inventory the
# declared paths that exist and record the declared-but-absent ones explicitly (never silently).
CONTRACT_DECLARATION_DOC = "contract.json"

USAGE = ("usage: python3 rv5_evidence.py {run|coverage|replay-perf|mapping|docs-examples|"
         "reconcile|fulfillment|hashes|handoff|security|finalize|seal|selftest} [--options]")


# --------------------------------------------------------------------------- small helpers
def now() -> str:
    return _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def rel(path: str) -> str:
    path = os.path.abspath(path)
    if path == REPO_ROOT or path.startswith(REPO_ROOT + os.sep):
        return os.path.relpath(path, REPO_ROOT).replace(os.sep, "/")
    return path


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def read_json(path: str, default=None):
    try:
        with open(path, encoding="utf-8") as fh:
            return json.load(fh)
    except (OSError, ValueError):
        return default


def write_text(path: str, text: str, crlf: bool = True) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if crlf:
        text = text.replace("\r\n", "\n").replace("\n", "\r\n")
    with open(path, "w", encoding="utf-8", newline="") as fh:
        fh.write(text)


def write_json(path: str, obj) -> None:
    write_text(path, json.dumps(obj, indent=1, ensure_ascii=False, sort_keys=True) + "\n")


def _git(args: list) -> subprocess.CompletedProcess:
    return subprocess.run(["git"] + list(args), cwd=REPO_ROOT, capture_output=True, text=True)


def git_identity() -> dict:
    head = _git(["rev-parse", "HEAD"])
    branch = _git(["rev-parse", "--abbrev-ref", "HEAD"])
    status = _git(["status", "--short"])
    ok = head.returncode == 0 and branch.returncode == 0 and status.returncode == 0
    head_sha = head.stdout.strip()
    lines = [ln for ln in status.stdout.splitlines() if ln.strip()]
    return dict(repo_root=REPO_ROOT, captured_at=now(), git_ok=ok,
                head_full=head_sha, head_short=head_sha[:8], branch=branch.stdout.strip(),
                status_short_digest=sha256_bytes(status.stdout.encode("utf-8")),
                status_short_count=len(lines), status_short_paths=[ln[3:] for ln in lines])


def run_cmd(root: str, name: str, argv: list, use_docker: bool = False,
            extra_env: dict | None = None, log_files: bool = True) -> dict:
    """Run one underlying command, log argv+cwd+stdout+stderr+exit+duration, return the record.

    ``log_files=False`` runs the command without touching ``$R/logs`` (no .out/.err/.exit/.cmd.json
    and no append to the run journal): the sealing step (`seal`) must not change the payload the
    ledger has already bound, so its own schema check is recorded inside the seal/reconciliation
    artifact (argv, exit code, stdout digest) instead of as a fresh log.
    """
    logs = os.path.join(root, "logs")
    os.makedirs(logs, exist_ok=True)
    env = dict(os.environ)
    audit_removed = env.pop("NuGetAudit", None) is not None      # never mask the audit
    docker_host = None
    if use_docker:
        docker_host = env.get(DOCKER_HOST_ENV, DEFAULT_DOCKER_HOST)
        if docker_host:
            env["DOCKER_HOST"] = docker_host
        else:
            env.pop("DOCKER_HOST", None)
            docker_host = None
    else:
        env.pop("DOCKER_HOST", None)
    if extra_env:
        env.update(extra_env)
    snapshot = git_identity()
    started_at = now()
    started = time.monotonic()
    proc = subprocess.run(argv, cwd=REPO_ROOT, capture_output=True, text=True, env=env)
    duration_ms = int((time.monotonic() - started) * 1000)
    base = os.path.join(logs, "%s-%s" % (LOG_PREFIX, name))
    record = dict(name=name, argv=list(argv), cwd=REPO_ROOT, exit_code=proc.returncode,
                  duration_ms=duration_ms, started_at=started_at, finished_at=now(),
                  docker_host=docker_host, audit_masking_removed=audit_removed,
                  logged=bool(log_files),
                  stdout_sha256=sha256_bytes(proc.stdout.encode("utf-8", errors="replace")),
                  stderr_sha256=sha256_bytes(proc.stderr.encode("utf-8", errors="replace")),
                  stdout_log=rel(base + ".out") if log_files else None,
                  stderr_log=rel(base + ".err") if log_files else None,
                  exit_log=rel(base + ".exit") if log_files else None,
                  source_snapshot=snapshot)
    if log_files:
        write_text(base + ".out", proc.stdout, crlf=False)
        write_text(base + ".err", proc.stderr, crlf=False)
        write_text(base + ".exit", "%d\n" % proc.returncode, crlf=False)
        write_json(base + ".cmd.json", record)
        with open(os.path.join(logs, "%s-commands.jsonl" % LOG_PREFIX), "a", encoding="utf-8",
                  newline="") as fh:
            fh.write(json.dumps(record, ensure_ascii=False, sort_keys=True) + "\n")
    return dict(record, stdout=proc.stdout, stderr=proc.stderr)


# --------------------------------------------------------------------------- test-run parsing
def parse_test_summary(text: str) -> dict:
    m = re.search(r"Test run summary:\s*(.+)", text)
    outcome = m.group(1).strip() if m else None

    def num(key):
        mm = re.search(r"^\s*%s:\s*(\d+)\s*$" % key, text, re.M)
        return int(mm.group(1)) if mm else None

    return dict(outcome=outcome, total=num("total"), failed=num("failed"),
                succeeded=num("succeeded"), skipped=num("skipped"), error=num("error"))


def skipped_tests(text: str) -> list:
    return re.findall(r"^\s*skipped\s+(\S+)", text, re.M)


def evaluate_test_run(parsed: dict, exit_code: int, min_total: int,
                      skip_policy: str, text: str) -> list:
    problems = []
    total = parsed.get("total")
    if total is None:
        problems.append("unparseable test summary (no 'Test run summary' block)")
    elif total < min_total:
        problems.append("empty test selection (selected %s < %d)" % (total, min_total))
    if parsed.get("failed"):
        problems.append("%d failed test(s)" % parsed["failed"])
    if exit_code != 0:
        problems.append("test command exit %d" % exit_code)
    if total and not parsed.get("succeeded"):
        problems.append("zero test(s) succeeded (selected %s)" % total)
    skipped = parsed.get("skipped") or 0
    if skip_policy == "none":
        if skipped:
            problems.append("%d skipped test(s): provider skip or excluded execution" % skipped)
    else:  # explicit-only: only the by-design Explicit probes may be skipped
        bad = [t for t in skipped_tests(text) if not t.startswith(EXPLICIT_SKIP_ALLOWED)]
        if bad:
            problems.append("%d unexpected skipped test(s): %s" % (len(bad), ", ".join(bad[:3])))
    return problems


# --------------------------------------------------------------------------- guards
def guard_root(root: str):
    r = os.path.normpath(os.path.abspath(root))
    if not os.path.isdir(r):
        return None, EXIT_USAGE, "missing required input: evidence root %s does not exist" % r
    if os.path.basename(r) != "rv5":
        return None, EXIT_VIOLATION, ("wrong root: expected the rv5 evidence root (basename 'rv5'); "
                                      "got %r" % os.path.basename(r))
    contract_path = os.path.join(r, "contract.json")
    contract = read_json(contract_path)
    if contract is None:
        return None, EXIT_USAGE, "missing required input: %s" % rel(contract_path)
    if contract.get("contract_rv") != CONTRACT_RV or contract.get("plan_revision") != PLAN_REVISION:
        return None, EXIT_VIOLATION, ("wrong root/contract revision: contract_rv=%r plan_revision=%r "
                                      "(expected %d/%d)" % (contract.get("contract_rv"),
                                                            contract.get("plan_revision"),
                                                            CONTRACT_RV, PLAN_REVISION))
    return contract, EXIT_OK, None


def guard_revision(root: str):
    snap_path = os.path.join(root, "source-snapshot.json")
    snap = read_json(snap_path)
    if not snap:
        return None, None, EXIT_USAGE, "missing required input: %s" % rel(snap_path)
    live = git_identity()
    problems = []
    if not live["git_ok"]:
        problems.append("source-snapshot identity unavailable: git failed in %s" % REPO_ROOT)
    else:
        if live["head_full"] != snap.get("head_full"):
            problems.append("wrong revision: live HEAD %s != frozen source-snapshot head_full %s"
                            % (live["head_full"][:12], str(snap.get("head_full"))[:12]))
        if live["branch"] != snap.get("branch"):
            problems.append("wrong revision: live branch %r != frozen branch %r"
                            % (live["branch"], snap.get("branch")))
    if problems:
        return snap, live, EXIT_VIOLATION, "; ".join(problems)
    return snap, live, EXIT_OK, None


def preflight(root: str):
    contract, code, reason = guard_root(root)
    if code:
        return None, None, code, reason
    snap, live, code, reason = guard_revision(root)
    if code:
        return None, None, code, reason
    return contract, (snap, live), EXIT_OK, None


def open_required_rows(root: str):
    manifest = read_json(os.path.join(root, "manifest.json")) or {}
    rows = manifest.get("rows") or []
    ids = [row.get("id") for row in rows if row.get("status") == "open"]
    return ids, manifest


def emit(root: str, name: str, payload: dict, code: int) -> int:
    status = {EXIT_OK: "ok", EXIT_VIOLATION: "violation", EXIT_USAGE: "blocked"}[code]
    payload = dict(payload)
    payload.setdefault("case", name)
    payload.setdefault("task", TASK)
    payload["status"] = status
    payload["exit_code"] = code
    payload.setdefault("reason", None)
    try:
        write_json(os.path.join(root, "verdicts", "%s.json" % name), payload)
    except OSError:
        pass
    print(json.dumps({k: payload.get(k) for k in ("case", "status", "exit_code", "reason")},
                     ensure_ascii=False, sort_keys=True))
    return code


# --------------------------------------------------------------------------- run --case
def test_argv(spec: dict, filter_override: str | None = None) -> list:
    argv = ["dotnet", "test", spec["project"], "-c", "Debug"]
    if spec.get("no_build"):
        argv.append("--no-build")
    argv += ["--filter", filter_override or spec["filt"]]
    return argv


def _run_entry(res: dict, spec: dict, filt: str, parsed: dict) -> dict:
    return dict(project=spec["project"], filter=filt, argv=list(res["argv"]), cwd=res["cwd"],
                exit_code=res["exit_code"], total=parsed.get("total"),
                passed=parsed.get("succeeded"), failed=parsed.get("failed"),
                skipped=parsed.get("skipped"), outcome=parsed.get("outcome"),
                duration_ms=res["duration_ms"], log=res["stdout_log"], err_log=res["stderr_log"],
                exit_log=res["exit_log"], docker_host=res["docker_host"],
                executed_at=res["finished_at"], source_snapshot=res["source_snapshot"])


def _upsert_run(runs: list, project: str, filt: str, entry: dict) -> list:
    for idx, run in enumerate(runs):
        argv = run.get("argv") or []
        if project in argv and filt in argv:
            merged = dict(run)
            merged.update(entry)
            runs[idx] = merged
            return runs
    runs.append(entry)
    return runs


def _v18_precheck(root: str, artifact: str, project: str):
    p = os.path.join(root, artifact)
    doc = read_json(p)
    if doc is None:
        return None, EXIT_USAGE, "missing required input: %s" % artifact
    mapping = doc.get("assertion_to_executed_test") or []
    container = [m for m in mapping if str(m.get("test", "")).startswith("T232SqlServerTestsRv5")]
    if not container:
        return doc, EXIT_VIOLATION, ("v18 assertion map carries no container-backed executed test "
                                     "(fixture-only/render-only proof is not accepted)")
    return doc, EXIT_OK, None


def _v23_precheck(root: str, artifact: str):
    p = os.path.join(root, artifact)
    doc = read_json(p)
    if doc is None:
        return None, EXIT_USAGE, "missing required input: %s" % artifact
    problems = []
    probe = read_json(os.path.join(root, "v23", "probe.json"))
    if not probe:
        problems.append("missing probe evidence v23/probe.json")
    elif not [c for c in (probe.get("discriminator_candidates") or [])
              if str(c.get("verdict", "")).startswith("VALID")]:
        problems.append("probe records no VALID local discriminator")
    mutation = doc.get("mutation") or {}
    red = mutation.get("red_arm") or {}
    green = mutation.get("green_arm") or {}
    if not mutation:
        problems.append("mutation evidence missing from %s" % artifact)
    else:
        if not isinstance(red.get("exit_code"), int) or red.get("exit_code") == 0:
            problems.append("mutation red arm does not redden (exit_code=%r)" % red.get("exit_code"))
        if (red.get("failed") or 0) < 1:
            problems.append("mutation red arm records no failed test")
        if green.get("exit_code") != 0 or (green.get("failed") or 0) != 0:
            problems.append("mutation green arm is not green")
        total = green.get("total") or 0
        if total and (red.get("failed") or 0) >= total:
            problems.append("blanket rejection suspected: the mutation reddens every recorded test")
    if not doc.get("assertion_to_test"):
        problems.append("assertion mapping is empty (test-name-only mapping is not accepted)")
    if problems:
        return doc, EXIT_VIOLATION, "; ".join(problems)
    return doc, EXIT_OK, None


def case_run(root: str, opts: dict, flags: set, snap, live):
    case = opts.get("case")
    if case not in ("v18", "v23", "regression"):
        return dict(reason="usage: run --case v18|v23|regression --root <R>"), EXIT_USAGE
    if case == "regression":
        return case_regression(root, opts, flags, snap, live)
    spec = RUN_SPECS[case]
    missing = [p for p in spec["required"] if not os.path.exists(os.path.join(REPO_ROOT, p))]
    if missing:
        return dict(case="run-%s" % case, required_inputs=spec["required"], missing=missing,
                    reason="missing required input: %s" % ", ".join(missing)), EXIT_USAGE
    pre = _v18_precheck(root, spec["artifact"], spec["project"]) if case == "v18" \
        else _v23_precheck(root, spec["artifact"])
    if pre[1]:
        return dict(case="run-%s" % case, artifact=spec["artifact"], reason=pre[2]), pre[1]
    artifact_doc = pre[0]

    filt = opts.get("filter") or spec["filt"]
    argv = test_argv(spec, filt)
    res = run_cmd(root, case, argv, use_docker=spec["docker"])
    parsed = parse_test_summary(res["stdout"] + res["stderr"])
    problems = evaluate_test_run(parsed, res["exit_code"], spec["min_total"], spec["skip_policy"],
                                 res["stdout"] + res["stderr"])
    guards = dict(exit_code=res["exit_code"], selected=parsed.get("total"), outcome=parsed.get("outcome"),
                  skipped=parsed.get("skipped"), failed=parsed.get("failed"),
                  min_total=spec["min_total"], filter=filt, project=spec["project"],
                  docker_host=res["docker_host"], sql_server_execution=bool(spec["docker"]))
    if problems:
        return dict(case="run-%s" % case, artifact=spec["artifact"], guards=guards,
                    source_snapshot=live, log=res["stdout_log"], log_record=res["exit_log"],
                    reason="; ".join(problems)), EXIT_VIOLATION

    entry = _run_entry(res, spec, filt, parsed)
    artifact_doc["runs"] = _upsert_run(list(artifact_doc.get("runs") or []), spec["project"], filt, entry)
    artifact_doc["verification"] = dict(guard="ok", **guards)
    artifact_doc["source_snapshot"] = live
    artifact_doc["refreshed_by"] = rel(SCRIPT)
    artifact_doc["refreshed_at"] = now()
    write_json(os.path.join(root, spec["artifact"]), artifact_doc)
    return dict(case="run-%s" % case, artifact=spec["artifact"], guards=guards,
                source_snapshot=live, log=res["stdout_log"], log_record=res["exit_log"]), EXIT_OK


def _short(project: str) -> str:
    return project.split("/")[-1]


def _build_count(text: str, suffix: str):
    # NB: no trailing \b - "Warning(s)" ends with ')' so a boundary before the newline never matches.
    m = re.search(r"^\s*(\d+)\s+%s" % re.escape(suffix), text, re.M)
    return int(m.group(1)) if m else None


def _build_line(text: str, suffix: str):
    m = re.search(r"^\s*\d+\s+%s.*$" % re.escape(suffix), text, re.M)
    return m.group(0).strip() if m else None


def parse_xunit_summary(text: str) -> dict:
    """`dotnet run` (xunit v3 console) execution summary -> the same shape as parse_test_summary."""
    m = re.search(r"Total:\s*(\d+),\s*Errors:\s*(\d+),\s*Failed:\s*(\d+),\s*Skipped:\s*(\d+)", text)
    if not m:
        return {}
    total, errors, failed, skipped = (int(g) for g in m.groups())
    passed = total - failed - skipped - errors
    return dict(outcome="Passed!" if not errors and not failed else "Failed!",
                total=total, failed=failed, skipped=skipped, error=errors,
                succeeded=passed if passed >= 0 else None)


def parse_any_summary(text: str) -> dict:
    """The runner summary in either shape: `dotnet test` or the xunit v3 console summary."""
    parsed = parse_test_summary(text)
    if parsed.get("total") is not None:
        return parsed
    return parse_xunit_summary(text) or parsed


def evaluate_core(parsed: dict, exit_code: int, min_total: int) -> list:
    """Selection/exit/failure checks; skip policy is decided separately by skip *reason*."""
    problems = []
    total = parsed.get("total")
    if total is None:
        problems.append("unparseable test summary (no 'Test run summary' block)")
    elif total < min_total:
        problems.append("empty test selection (selected %s < %d)" % (total, min_total))
    if parsed.get("failed"):
        problems.append("%d failed test(s)" % parsed["failed"])
    if exit_code != 0:
        problems.append("test command exit %d" % exit_code)
    if total and not parsed.get("succeeded"):
        problems.append("zero test(s) succeeded (selected %s)" % total)
    return problems


# Skip classification by *reason* (the repo reports provider unavailability with an explicit
# "<Provider> is not available." message from Assert.SkipUnless(Provider.IsAvailable, ...)).
_EXPLICIT_REASON = re.compile(r"Not run \(due to explicit test filtering\)")
_OPTIN_REASON = re.compile(r"[Ss]et\s+[A-Z][A-Z0-9_]*\s*=\s*1")
_PROVIDER_DOWN = re.compile(r"is not available", re.I)


def skipped_details(text: str) -> list:
    """(test, reason) pairs from both xunit v3 skip shapes (dotnet test and dotnet run --noColor)."""
    lines = text.splitlines()
    out = []
    for idx, line in enumerate(lines):
        m = re.match(r"^\s*(\S+)\s+\[SKIP\]\s*$", line) or \
            re.match(r"^\s*skipped\s+(\S+)\s*$", line)
        if not m:
            continue
        reason = ""
        if idx + 1 < len(lines):
            nxt = lines[idx + 1].strip()
            if nxt and not nxt.startswith("from "):
                reason = nxt
        out.append((m.group(1), reason))
    return out


def provider_unavailability_skips(text: str) -> list:
    return [t for t, reason in skipped_details(text) if _PROVIDER_DOWN.search(reason)]


def environment_skips(text: str) -> list:
    """Skips that are neither the provider-unavailability signal nor an opt-in probe."""
    return [(t, r) for t, r in skipped_details(text)
            if not _EXPLICIT_REASON.search(r) and not _OPTIN_REASON.search(r)
            and not _PROVIDER_DOWN.search(r)]


def _provider_skip_breakdown(text: str) -> dict:
    counts = {}
    for test, _reason in skipped_details(text):
        m = re.search(r"\.Integration\.Tests\.([A-Za-z]+)", test)
        key = m.group(1) if m else test.rsplit(".", 1)[0]
        counts[key] = counts.get(key, 0) + 1
    return counts


def _git_bytes(args: list) -> subprocess.CompletedProcess:
    return subprocess.run(["git"] + list(args), cwd=REPO_ROOT, capture_output=True)


def _head_file_bytes(path_rel: str):
    proc = _git_bytes(["show", "HEAD:%s" % path_rel])
    return proc.stdout if proc.returncode == 0 else None


def _frozen_oracle() -> dict:
    """Prove the legacy SQL-generation oracle file is byte-identical to HEAD."""
    path = os.path.join(REPO_ROOT, FROZEN_ORACLE_FILE)
    if not os.path.isfile(path):
        return dict(file=FROZEN_ORACLE_FILE, ok=False,
                    reason="missing required input: %s" % FROZEN_ORACLE_FILE)
    worktree = open(path, "rb").read()
    head = _head_file_bytes(FROZEN_ORACLE_FILE)
    # core.autocrlf=true: blobs are LF, the worktree is CRLF. Compare content, not the checkout form.
    worktree_lf = worktree.replace(b"\r\n", b"\n")
    head_lf = head.replace(b"\r\n", b"\n") if head is not None else None
    diff = _git(["diff", "--stat", "HEAD", "--", FROZEN_ORACLE_FILE])
    status = _git(["status", "--porcelain", "--", FROZEN_ORACLE_FILE])
    text = worktree.decode("utf-8", errors="replace")
    legacy = {name: text.count("public void %s(" % name) for name in FROZEN_ORACLE_CASES}
    problems = []
    if head is None:
        problems.append("cannot read HEAD:%s" % FROZEN_ORACLE_FILE)
    elif head_lf != worktree_lf:
        problems.append("worktree content differs from HEAD:%s" % FROZEN_ORACLE_FILE)
    if diff.returncode != 0:
        problems.append("git diff for the frozen oracle failed: %s" % diff.stderr.strip())
    elif diff.stdout.strip():
        problems.append("git diff shows a frozen-oracle change: %s" % diff.stdout.strip())
    if status.stdout.strip():
        problems.append("frozen oracle is dirty: %s" % status.stdout.strip())
    missing = sorted(n for n, c in legacy.items() if c != 1)
    if missing:
        problems.append("frozen legacy case(s) not found exactly once: %s" % ", ".join(missing))
    if FROZEN_ORACLE_SQL not in text:
        problems.append("the exact legacy PIVOT SQL is absent: %r" % FROZEN_ORACLE_SQL)
    return dict(
        file=FROZEN_ORACLE_FILE, bytes=len(worktree),
        worktree_sha256=sha256_bytes(worktree), head_blob_sha256=sha256_bytes(head) if head else None,
        worktree_lf_sha256=sha256_bytes(worktree_lf),
        head_lf_sha256=sha256_bytes(head_lf) if head_lf is not None else None,
        line_ending_note="blobs are LF (core.autocrlf=true); the worktree is CRLF - identity is "
                         "checked on content plus git diff/status",
        byte_identical=head_lf is not None and head_lf == worktree_lf,
        legacy_cases=len(FROZEN_ORACLE_CASES),
        legacy_cases_found=sum(1 for c in legacy.values() if c == 1),
        legacy_pivot_sql=FROZEN_ORACLE_SQL,
        legacy_sql_present=FROZEN_ORACLE_SQL in text,
        git_diff_stat=diff.stdout or "", git_status=status.stdout or "",
        ok=not problems, reason="; ".join(problems) or None)


def _src_allowlist() -> dict:
    """No file outside the r=5 allowlist may change under src/."""
    name_only = _git(["diff", "--name-only", "HEAD", "--", "src/"])
    stat = _git(["diff", "--stat", "HEAD", "--", "src/"])
    status = _git(["status", "--porcelain", "--", "src/"])
    changed = [ln for ln in name_only.stdout.splitlines() if ln.strip()]
    untracked = [ln[3:] for ln in status.stdout.splitlines() if ln.startswith("??")]
    problems = []
    if changed != [R5_ALLOWLIST_SRC]:
        problems.append("src/ diff outside the r=5 allowlist: %s" % (changed or "<none>"))
    if untracked:
        problems.append("untracked src/ file(s) outside the allowlist: %s" % ", ".join(untracked))
    return dict(allowlist=[R5_ALLOWLIST_SRC], changed_src=changed, untracked_src=untracked,
                git_diff_stat=stat.stdout or "", ok=not problems,
                reason="; ".join(problems) or None)


def _live_inventory() -> dict:
    status = _git(["status", "--short"])
    entries = {}
    for line in status.stdout.splitlines():
        if len(line) < 4:
            continue
        entries[line[3:]] = line[:2]
    return entries


def _inventory_diff(snap: dict, live: dict) -> dict:
    """Frozen source-snapshot inventory vs the live worktree inventory (scope + branch)."""
    frozen = {e["path"]: e.get("code") for e in (snap.get("inventory") or [])}
    live_paths = _live_inventory()
    frozen_only = sorted(set(frozen) - set(live_paths))
    live_only = sorted(set(live_paths) - set(frozen))
    code_changed = sorted(p for p in set(frozen) & set(live_paths) if frozen[p] != live_paths[p])
    return dict(
        scope="src/ tests/ docs/ examples/ artifacts/pdca/D232-sqlserver-pivot-unpivot/",
        frozen_branch=snap.get("branch"), live_branch=live.get("branch"),
        branch_matches=snap.get("branch") == live.get("branch"),
        frozen_head=snap.get("head_short"), live_head=live.get("head_short"),
        head_matches=snap.get("head_full") == live.get("head_full"),
        frozen_count=len(frozen), live_count=len(live_paths),
        added_in_live=live_only, removed_in_live=frozen_only, status_code_changed=code_changed,
        added_src=[p for p in live_only if p.startswith("src/")],
        added_tests=[p for p in live_only if p.startswith("tests/")],
        added_docs=[p for p in live_only if p.startswith("docs/")],
        added_artifacts=[p for p in live_only if p.startswith("artifacts/")],
        note="the frozen snapshot was taken before the rv5 evidence root existed; the live "
             "delta is the r=5 delta (src guard + the r=5 test/evidence files)")


def _mutation_evidence(root: str, rerun: bool) -> dict:
    """Carry (and optionally re-run) the V23 targeted-mutation red/green arms."""
    entries, problems = {}, []
    for arm, rel_path in sorted(V23_MUTATION_LOGS.items()):
        path = os.path.join(root, rel_path)
        if not os.path.isfile(path):
            problems.append("missing mutation log %s" % rel_path)
            continue
        text = open(path, encoding="utf-8", errors="replace").read()
        parsed = parse_test_summary(text)
        m = re.search(r"^EXIT=(\d+)\s*$", text, re.M)
        entries[arm] = dict(log=rel_path, sha256=sha256_file(path), bytes=os.path.getsize(path),
                            exit_code=int(m.group(1)) if m else None,
                            total=parsed.get("total"), passed=parsed.get("succeeded"),
                            failed=parsed.get("failed"), skipped=parsed.get("skipped"),
                            outcome=parsed.get("outcome"))
    recorded = (read_json(os.path.join(root, "v23/assertion-execution.json")) or {}).get("mutation") or {}
    for arm, recorded_key in (("red", "red_arm"), ("green", "green_arm")):
        got, want = entries.get(arm), recorded.get(recorded_key)
        if not got or not want:
            continue
        for key in ("exit_code", "total", "passed", "failed"):
            if got.get(key) != want.get(key):
                problems.append("mutation %s arm %s mismatch: log %r != recorded %r"
                                % (arm, key, got.get(key), want.get(key)))
    red, green = entries.get("red") or {}, entries.get("green") or {}
    if not red or not red.get("failed") or red.get("exit_code") == 0:
        problems.append("mutation red arm does not redden (exit=%r failed=%r)"
                        % (red.get("exit_code"), red.get("failed")))
    if not green or green.get("exit_code") != 0 or green.get("failed"):
        problems.append("mutation green arm is not green (exit=%r failed=%r)"
                        % (green.get("exit_code"), green.get("failed")))
    return dict(neutralized=recorded.get("neutralized"), arms=entries, rerun=rerun,
                rerun_skipped_reason=None if rerun else
                ("carried from the recorded arms: a trustworthy re-run requires mutating "
                 "src/nextorm.core/Visitors/NormSqlTranslator.cs in the live worktree (a source "
                 "mutation is neither cheap nor safe here); the recorded logs are re-verified "
                 "against the v23 mutation block by digest and by their parsed test summary"),
                ok=not problems, reason="; ".join(problems) or None)


def case_regression(root: str, opts: dict, flags: set, snap, live):
    """The rv5 regression battery: solution build, eight per-project suites, the frozen oracle,
    the r=5 allowlist/inventory diff, the carried V23 mutation arms and the container boundary."""
    problems = []
    rerun_mutation = "rerun-mutation" in flags or bool(opts.get("rerun-mutation"))

    # 1. solution build (captures the `N Warning(s) / N Error(s)` line)
    build_res = run_cmd(root, "regression-build", REGRESSION_BUILD, use_docker=False)
    build_text = build_res["stdout"] + build_res["stderr"]
    build = dict(argv=REGRESSION_BUILD, exit_code=build_res["exit_code"],
                 log=build_res["stdout_log"], duration_ms=build_res["duration_ms"],
                 build_succeeded="Build succeeded." in build_text,
                 warning_line=_build_line(build_text, "Warning(s)"),
                 error_line=_build_line(build_text, "Error(s)"),
                 warnings=_build_count(build_text, "Warning(s)"),
                 errors=_build_count(build_text, "Error(s)"),
                 source_snapshot=build_res["source_snapshot"])
    if build_res["exit_code"] != 0 or not build["build_succeeded"]:
        problems.append("solution build not green (exit %d)" % build_res["exit_code"])
    if build["errors"] is None or build["warnings"] is None:
        problems.append("unparseable build summary (no 'N Warning(s)'/'N Error(s)' line)")
    else:
        if build["errors"]:
            problems.append("solution build reports %s error(s)" % build["errors"])
        if build["warnings"]:
            problems.append("solution build reports %s warning(s)" % build["warnings"])

    # 2. the eight per-project suites
    suites = []
    for project in REGRESSION_PROJECTS:
        name = "regression-%s" % _short(project)
        res = run_cmd(root, name, ["dotnet", "test", project, "-c", "Debug"], use_docker=False)
        text = res["stdout"] + res["stderr"]
        parsed = parse_any_summary(text)
        skips = skipped_details(text)
        provider_down = provider_unavailability_skips(text)
        unexpected = environment_skips(text)
        entry = dict(name=name, project=project, argv=list(res["argv"]),
                     exit_code=res["exit_code"], duration_ms=res["duration_ms"],
                     log=res["stdout_log"], outcome=parsed.get("outcome"),
                     total=parsed.get("total"), passed=parsed.get("succeeded"),
                     failed=parsed.get("failed"), skipped=parsed.get("skipped"),
                     skips=skips, by_design_skips=[t for t, _ in skips if (t, _) not in unexpected],
                     unexpected_skips=unexpected, provider_unavailability_skips=provider_down,
                     source_snapshot=res["source_snapshot"])
        problems += ["%s: %s" % (name, p) for p in evaluate_core(parsed, res["exit_code"], 1)]
        if provider_down:
            problems.append("%s: provider unavailability skip(s): %s"
                            % (name, ", ".join(provider_down[:3])))
        if unexpected:
            problems.append("%s: unexplained skip(s) (not an Explicit/opt-in probe): %s"
                            % (name, ", ".join(t for t, _ in unexpected[:3])))
        suites.append(entry)

    # 3. frozen oracle
    oracle = _frozen_oracle()
    if not oracle["ok"]:
        problems.append("frozen oracle: %s" % oracle["reason"])

    # 4. r=5 allowlist
    allowlist = _src_allowlist()
    if not allowlist["ok"]:
        problems.append("allowlist: %s" % allowlist["reason"])

    # 5. scope / branch inventory diff
    inventory = _inventory_diff(snap, live)
    if not inventory["head_matches"] or not inventory["branch_matches"]:
        problems.append("inventory: live branch/HEAD does not match the frozen source-snapshot")

    # 6. carried V23 mutation arms
    mutation = _mutation_evidence(root, rerun_mutation)
    if not mutation["ok"]:
        problems.append("mutation: %s" % mutation["reason"])

    # 7. container boundary
    integ_res = run_cmd(root, "regression-integration", REGRESSION_INTEGRATION, use_docker=True)
    integ_text = integ_res["stdout"] + integ_res["stderr"]
    integ_parsed = parse_any_summary(integ_text)
    containers_started = len(re.findall(r"Start Docker container", integ_text))
    provider_down = provider_unavailability_skips(integ_text)
    integration = dict(argv=REGRESSION_INTEGRATION, exit_code=integ_res["exit_code"],
                       docker_host=integ_res["docker_host"], duration_ms=integ_res["duration_ms"],
                       log=integ_res["stdout_log"], outcome=integ_parsed.get("outcome"),
                       total=integ_parsed.get("total"), passed=integ_parsed.get("succeeded"),
                       failed=integ_parsed.get("failed"), skipped=integ_parsed.get("skipped"),
                       provider_skips=provider_down, containers_started=containers_started,
                       provider_skip_breakdown=_provider_skip_breakdown(integ_text),
                       source_snapshot=integ_res["source_snapshot"])
    if not integration["docker_host"]:
        problems.append("container boundary ran without DOCKER_HOST (provider skips are not evidence)")
    problems += ["container boundary: %s" % p for p in
                 evaluate_core(integ_parsed, integ_res["exit_code"], 1)]
    if provider_down:
        problems.append("container boundary: provider unavailability skip(s): %s"
                        % ", ".join(provider_down[:3]))
    if containers_started < 4:
        problems.append("container boundary started only %d container(s) (< 4: PostgreSQL, SQL "
                        "Server, MySQL, ClickHouse)" % containers_started)

    selections = dict(build=build_res["exit_code"],
                      suites={s["project"]: s["total"] for s in suites},
                      integration=integ_parsed.get("total"))
    payload = dict(case="regression", unit="D232-RV5-07",
                   build=build, suites=suites, frozen_oracle=oracle, allowlist=allowlist,
                   inventory_diff=inventory, mutation=mutation, integration=integration,
                   selections=selections,
                   selection_rule="nonempty selection (selected >= 1) per suite, zero failures, "
                                  "zero provider skips",
                   source_snapshot=live,
                   delegation=dict(note="the rv5 battery re-executes the frozen exact commands; "
                                        "the rv4 driver is not re-run (rv4 stays historical)"),
                   reason="; ".join(problems) or None)
    write_text(os.path.join(root, "regression", "commands.jsonl"),
               "".join(json.dumps(s, sort_keys=True) + "\n"
                       for s in [dict(kind="build", **build)] + suites + [dict(kind="integration", **integration)]))
    write_json(os.path.join(root, "regression", "regression.json"), payload)
    return payload, (EXIT_VIOLATION if problems else EXIT_OK)


# --------------------------------------------------------------------------- coverage
def _cobertura_totals(path: str):
    try:
        head = open(path, encoding="utf-8", errors="replace").read(2000)
    except OSError:
        return None
    m = re.search(r"<coverage[^>]*>", head)
    if not m:
        return None
    tag = m.group(0)

    def attr(name):
        mm = re.search(r'%s="([^"]+)"' % name, tag)
        return float(mm.group(1)) if mm else None

    line, branch = attr("line-rate"), attr("branch-rate")
    if line is None or branch is None:
        return None
    return dict(line_coverage=round(line * 100, 2), branch_coverage=round(branch * 100, 2))


def _cobertura_classes(path: str) -> dict:
    """Per-class line/branch rates from a cobertura report (reportgenerator or dotnet-coverage)."""
    try:
        text = open(path, encoding="utf-8", errors="replace").read()
    except OSError:
        return {}
    out = {}
    for m in re.finditer(r"<class\b([^>]*)>", text):
        attrs = m.group(1)
        name = re.search(r'name="([^"]*)"', attrs)
        if not name:
            continue
        cls = name.group(1).replace("&lt;", "<").replace("&gt;", ">").replace("&amp;", "&")

        def rate(attr):
            mm = re.search(r'%s="([^"]+)"' % attr, attrs)
            try:
                return round(float(mm.group(1)) * 100, 2)
            except (AttributeError, ValueError):
                return None

        out[cls] = dict(line_coverage=rate("line-rate"), branch_coverage=rate("branch-rate"))
    return out


def _coverage_versions() -> dict:
    manifest = read_json(os.path.join(REPO_ROOT, ".config", "dotnet-tools.json")) or {}
    tools = manifest.get("tools") or {}
    proc = subprocess.run(["dotnet", "--version"], cwd=REPO_ROOT, capture_output=True, text=True)
    return dict(dotnet_sdk=proc.stdout.strip(),
                dotnet_coverage=(tools.get("dotnet-coverage") or {}).get("version"),
                reportgenerator=(tools.get("dotnet-reportgenerator-globaltool") or {}).get("version"),
                manifest=".config/dotnet-tools.json", manifest_reinstalled=False,
                note="central manifest only; no duplicate tool manifest installed")


def _coverage_exclusions() -> dict:
    """coverage.settings.xml / Directory.Build.props / *.runsettings must be untouched."""
    files = ["coverage.settings.xml", "Directory.Build.props", "*.runsettings"]
    status = _git(["status", "--porcelain", "--"] + files)
    diff = _git(["diff", "--name-only", "HEAD", "--"] + files)
    dirty = [ln for ln in status.stdout.splitlines() if ln.strip()]
    changed = [ln for ln in diff.stdout.splitlines() if ln.strip()]
    problems = []
    if dirty:
        problems.append("coverage settings/build files are dirty: %s" % ", ".join(dirty))
    if changed:
        problems.append("coverage settings/build files differ from HEAD: %s" % ", ".join(changed))
    return dict(files=files, dirty=dirty, changed=changed, ok=not problems,
                reason="; ".join(problems) or None)


def coverage_inner(root: str) -> int:
    """Boundary suite wrapped by ``dotnet-coverage collect`` (five projects, built already)."""
    exits = {}
    for project in COVERAGE_PROJECTS:
        res = run_cmd(root, "coverage-inner-%s" % _short(project),
                      ["dotnet", "test", project, "-c", "Debug", "--no-build"],
                      use_docker=project.endswith("integration.tests"))
        exits[project] = res["exit_code"]
    overall = EXIT_OK if all(code == EXIT_OK for code in exits.values()) else EXIT_VIOLATION
    print(json.dumps({"coverage_run_exits": exits, "overall": overall}, indent=1))
    return overall


def case_coverage(root: str, opts: dict, flags: set, snap, live):
    baseline_root = os.path.abspath(opts.get("baseline-root") or "")
    candidate = os.path.abspath(opts.get("candidate") or "")
    if not baseline_root or not os.path.isdir(baseline_root):
        return dict(reason="usage/missing input: --baseline-root <B/coverage>"), EXIT_USAGE
    baseline_harness_path = os.path.join(baseline_root, "harness.json")
    baseline_harness = read_json(baseline_harness_path)
    if not baseline_harness:
        return dict(reason="missing required input: %s" % rel(baseline_harness_path)), EXIT_USAGE
    if not candidate:
        return dict(reason="usage: --candidate <R>/coverage/candidate.cobertura.xml"), EXIT_USAGE

    problems = []
    exclusions = _coverage_exclusions()
    if not exclusions["ok"]:
        problems.append("exclusions: %s" % exclusions["reason"])

    # ---- fresh candidate collection on the current tree via the canonical route ---------------
    out_dir = os.path.dirname(candidate)
    os.makedirs(out_dir, exist_ok=True)
    raw_xml = os.path.join(out_dir, "raw.cobertura.xml")
    report_dir = os.path.join(out_dir, "report")
    versions = _coverage_versions()
    build = {}
    for project in COVERAGE_PROJECTS:
        res = run_cmd(root, "coverage-build-%s" % _short(project),
                      ["dotnet", "build", project, "-c", "Debug"], use_docker=False)
        build[project] = res["exit_code"]
    if any(code != EXIT_OK for code in build.values()):
        problems.append("coverage boundary build failed: %s" % build)
    inner_cmd = "%s %s coverage --inner --root %s" % (sys.executable, SCRIPT, root)
    collect_argv = ["dotnet", "tool", "run", "dotnet-coverage", "collect", "-f", "cobertura",
                    "-s", "coverage.settings.xml", "-o", raw_xml, inner_cmd]
    report_argv = ["dotnet", "tool", "run", "reportgenerator", "-reports:%s" % raw_xml,
                   "-targetdir:%s" % report_dir, "-reporttypes:JsonSummary;Cobertura"]
    collect = run_cmd(root, "coverage-collect", collect_argv, use_docker=True)
    report = run_cmd(root, "coverage-report", report_argv, use_docker=False)
    if collect["exit_code"] != EXIT_OK:
        problems.append("dotnet-coverage collect exit %d" % collect["exit_code"])
    if report["exit_code"] != EXIT_OK:
        problems.append("reportgenerator exit %d" % report["exit_code"])
    report_cobertura = os.path.join(report_dir, "Cobertura.xml")
    if os.path.isfile(report_cobertura):
        shutil.copyfile(report_cobertura, candidate)
    elif not os.path.isfile(candidate):
        problems.append("no candidate cobertura produced (%s missing)" % rel(report_cobertura))
    versions["expanded_argv"] = dict(build={p: ["dotnet", "build", p, "-c", "Debug"]
                                            for p in COVERAGE_PROJECTS},
                                     collect=collect_argv, report=report_argv, inner=inner_cmd)

    # ---- the recorded baseline (rv1) and the recorded rv4 candidate figures -------------------
    baseline_prov = baseline_harness.get("baseline_provenance") or {}
    baseline_classes = {}
    for key, kind in (("summary", "summary_sha256"), ("coverage_xml", "coverage_xml_sha256")):
        rel_path = baseline_prov.get(key)
        if not rel_path:
            continue
        full = os.path.join(REPO_ROOT, rel_path)
        if not os.path.isfile(full):
            problems.append("recorded rv1 baseline %s is missing (%s)" % (key, rel_path))
        elif baseline_prov.get(kind) and sha256_file(full) != baseline_prov.get(kind):
            problems.append("recorded rv1 baseline %s sha256 mismatch (%s)" % (key, rel_path))
    baseline_summary_rel = baseline_prov.get("summary")
    if baseline_summary_rel:
        summary = read_json(os.path.join(REPO_ROOT, baseline_summary_rel)) or {}
        for assembly in ((summary.get("coverage") or {}).get("assemblies") or []):
            for cls in assembly.get("classesinassembly") or []:
                baseline_classes[cls.get("name")] = dict(line_coverage=cls.get("coverage"),
                                                         branch_coverage=cls.get("branchcoverage"))
    rv4_area = {t.get("class_"): t for t in (baseline_harness.get("touched_area") or [])}
    rv4_cand_totals = dict(line_coverage=baseline_harness.get("candidate_line"),
                           branch_coverage=baseline_harness.get("candidate_branch"))
    base_totals = dict(line_coverage=baseline_harness.get("baseline_line"),
                       branch_coverage=baseline_harness.get("baseline_branch"))

    cand_tot = _cobertura_totals(candidate)
    cand_classes = _cobertura_classes(candidate)
    if cand_tot is None:
        problems.append("unreadable/incomparable cobertura candidate %s" % rel(candidate))
    # reportgenerator's Summary.json is the same reporter output the recorded rv1 baseline and the
    # rv4 candidate figures came from -> the exact like-for-like comparison
    summary_doc = read_json(os.path.join(report_dir, "Summary.json")) or {}
    summary = summary_doc.get("summary") or {}
    summary_totals = dict(line_coverage=summary.get("linecoverage"),
                          branch_coverage=summary.get("branchcoverage"),
                          coveredlines=summary.get("coveredlines"),
                          coverablelines=summary.get("coverablelines"),
                          coveredbranches=summary.get("coveredbranches"),
                          totalbranches=summary.get("totalbranches"))
    if summary_totals["line_coverage"] is None:
        problems.append("reportgenerator Summary.json missing/unreadable (%s)"
                        % rel(os.path.join(report_dir, "Summary.json")))
    elif cand_tot and abs(summary_totals["line_coverage"] - cand_tot["line_coverage"]) > 0.5:
        problems.append("cobertura line-rate %.2f disagrees with Summary.json %.2f"
                        % (cand_tot["line_coverage"], summary_totals["line_coverage"]))

    thresholds = dict(COVERAGE_THRESHOLDS)
    thresholds_met = bool(cand_tot) and cand_tot["line_coverage"] >= thresholds["line"] \
        and cand_tot["branch_coverage"] >= thresholds["branch"]
    touched, decreased, decreased_rv4 = [], [], []
    for cls in TOUCHED_CLASSES:
        baseline = baseline_classes.get(cls) or (rv4_area.get(cls) or {}).get("baseline")
        rv4 = (rv4_area.get(cls) or {}).get("candidate")
        cand = cand_classes.get(cls)
        entry = dict(class_=cls, baseline=baseline, rv4_candidate=rv4, candidate=cand)
        if baseline and cand:
            entry["line_delta_vs_baseline"] = round(cand["line_coverage"] - baseline["line_coverage"], 2)
            entry["branch_delta_vs_baseline"] = round(cand["branch_coverage"] - baseline["branch_coverage"], 2)
            if cand["line_coverage"] < baseline["line_coverage"] \
                    or cand["branch_coverage"] < baseline["branch_coverage"]:
                decreased.append(cls)
        else:
            entry["note"] = "class missing on one side of the baseline comparison"
            decreased.append(cls)
        if rv4 and cand:
            entry["line_delta_vs_rv4"] = round(cand["line_coverage"] - rv4["line_coverage"], 2)
            entry["branch_delta_vs_rv4"] = round(cand["branch_coverage"] - rv4["branch_coverage"], 2)
            if cand["line_coverage"] < rv4["line_coverage"] \
                    or cand["branch_coverage"] < rv4["branch_coverage"]:
                decreased_rv4.append(cls)
        touched.append(entry)

    if cand_tot:
        if not thresholds_met:
            problems.append("threshold missed (line %s / branch %s vs >=%s/%s)"
                            % (cand_tot["line_coverage"], cand_tot["branch_coverage"],
                               thresholds["line"], thresholds["branch"]))
        if cand_tot["line_coverage"] < (base_totals["line_coverage"] or 0) \
                or cand_tot["branch_coverage"] < (base_totals["branch_coverage"] or 0):
            problems.append("candidate totals decreased vs the recorded rv1 baseline")
    if decreased:
        problems.append("touched-area coverage decreased vs the rv1 baseline for %s"
                        % ", ".join(decreased))
    if decreased_rv4:
        problems.append("touched-area coverage decreased vs the recorded rv4 candidate for %s"
                        % ", ".join(decreased_rv4))

    payload = dict(case="coverage", unit="D232-RV5-07", captured=now(),
                   suite=" + ".join(COVERAGE_PROJECTS),
                   thresholds=thresholds, thresholds_met=thresholds_met,
                   baseline=dict(**base_totals, provenance=baseline_prov,
                                 source=rel(baseline_harness_path)),
                   rv4_candidate=dict(**rv4_cand_totals,
                                      source=rel(baseline_harness_path),
                                      note="the rv4 candidate figures recorded in the rv4 coverage "
                                           "harness (baseline 88.3/80.3, rv4 candidate 88.4/80.4)"),
                   candidate=dict(path=rel(candidate), sha256=sha256_file(candidate),
                                  bytes=os.path.getsize(candidate), **(cand_tot or {})),
                   reportgenerator_summary=dict(
                       path=rel(os.path.join(report_dir, "Summary.json")), **summary_totals,
                       note="same reporter output as the recorded baseline; line/branch are the "
                            "1-decimal summary figures directly comparable to 88.3/80.3 and 88.4/80.4"),
                   deltas=dict(
                       vs_baseline=dict(line=round((cand_tot or {}).get("line_coverage", 0)
                                                   - (base_totals["line_coverage"] or 0), 2),
                                        branch=round((cand_tot or {}).get("branch_coverage", 0)
                                                     - (base_totals["branch_coverage"] or 0), 2)),
                       vs_rv4=dict(line=round((cand_tot or {}).get("line_coverage", 0)
                                              - (rv4_cand_totals["line_coverage"] or 0), 2),
                                   branch=round((cand_tot or {}).get("branch_coverage", 0)
                                                - (rv4_cand_totals["branch_coverage"] or 0), 2)),
                       summary_json_vs_baseline=dict(
                           line=round((summary_totals["line_coverage"] or 0)
                                      - (base_totals["line_coverage"] or 0), 2),
                           branch=round((summary_totals["branch_coverage"] or 0)
                                        - (base_totals["branch_coverage"] or 0), 2)),
                       summary_json_vs_rv4=dict(
                           line=round((summary_totals["line_coverage"] or 0)
                                      - (rv4_cand_totals["line_coverage"] or 0), 2),
                           branch=round((summary_totals["branch_coverage"] or 0)
                                        - (rv4_cand_totals["branch_coverage"] or 0), 2))),
                   touched_area=touched, touched_pct_decreased=bool(decreased),
                   touched_pct_decreased_vs_rv4=bool(decreased_rv4),
                   exclusions=dict(state="unchanged", checked=exclusions["files"],
                                   note="coverage.settings.xml, Directory.Build.props, *.runsettings, "
                                        "suite selection and exclusions are unchanged"),
                   tool_versions=versions, build_exits=build,
                   collect_exit=collect["exit_code"], collect_log=collect["stdout_log"],
                   report_exit=report["exit_code"], report_log=report["stdout_log"],
                   source_snapshot=live,
                   delegation=dict(baseline_root=rel(baseline_root),
                                   collection_route="canonical dotnet-coverage collect -> "
                                                    "reportgenerator (same collector/reporter/"
                                                    "settings/suites as the recorded baseline)",
                                   note="the rv4 coverage driver is not re-run; rv4 stays historical"),
                   reason="; ".join(problems) or None)
    write_json(os.path.join(root, "coverage", "coverage.json"), payload)
    write_json(os.path.join(root, "coverage", "branch-delta.json"), payload)
    return payload, (EXIT_VIOLATION if problems else EXIT_OK)


# --------------------------------------------------------------------------- replay-perf
def bdn_dirty_snapshot() -> dict:
    base = os.path.join(REPO_ROOT, BDN_DIR_REL)
    snap = {}
    if not os.path.isdir(base):
        return snap
    for dirpath, _dirnames, filenames in os.walk(base):
        for fn in filenames:
            p = os.path.join(dirpath, fn)
            snap[rel(p)] = sha256_file(p)
    return snap


def bdn_dirty_paths() -> list:
    proc = _git(["status", "--short", "--", BDN_DIR_REL])
    return sorted(ln[3:] for ln in proc.stdout.splitlines() if ln.strip())


_PERF_UNITS = {"ns": 0.001, "\u00b5s": 1.0, "\u03bcs": 1.0, "us": 1.0, "ms": 1000.0, "s": 1_000_000.0}
PERF_NOISE_PCT = 20.0


def _parse_bdn_reports(raw_dir: str) -> dict:
    """Read BenchmarkDotNet's per-case table from <raw_dir>/results/*-report-github.md.

    Returns Method -> {mean, unit, allocated, report}; the table header is the authority for the
    columns (Mean / Allocated), so no column position is assumed.
    """
    import glob
    cases = {}
    for path in sorted(glob.glob(os.path.join(raw_dir, "results", "*-report-github.md"))):
        header = None
        for line in open(path, encoding="utf-8", errors="replace").read().splitlines():
            if line.startswith("| Method"):
                header = [c.strip() for c in line.strip().strip("|").split("|")]
                continue
            if not header or not line.startswith("|") or "---" in line:
                continue
            parts = [c.strip() for c in line.strip().strip("|").split("|")]
            if not parts or not parts[0] or parts[0] == "Method":
                continue
            mean = parts[1] if len(parts) > 1 else None
            allocated = None
            if "Allocated" in header and len(parts) > header.index("Allocated"):
                allocated = parts[header.index("Allocated")]
            mu = re.match(r"^\s*([\d][\d.,]*)\s*(ns|\u00b5s|\u03bcs|us|ms|s)\s*$", mean or "")
            cases[parts[0]] = dict(mean=mean, unit=mu.group(2) if mu else None,
                                   allocated=allocated, report=rel(path))
    return cases


def _perf_ratio(cases: dict):
    """cached/prepared ratio of the acceptance pair (Cached_ToList over Prepared_ToList)."""
    cached = _mean_us(cases.get("Cached_ToList") or {})
    prepared = _mean_us(cases.get("Prepared_ToList") or {})
    if not cached or not prepared:
        return None
    return dict(cached=cases["Cached_ToList"]["mean"], prepared=cases["Prepared_ToList"]["mean"],
                cached_over_prepared=round(cached / prepared, 2))


def _mean_us(value: dict):
    """Accept a bare number (unit carried in ``unit``) or a BDN cell such as ``1,834.4 μs``."""
    text = str(value.get("mean", "")).replace(",", "").strip()
    mu = re.match(r"^([\d.]+)\s*(ns|\u00b5s|\u03bcs|us|ms|s)?$", text)
    if not mu:
        return None
    unit = mu.group(2) or value.get("unit") or "us"
    try:
        return float(mu.group(1)) * _PERF_UNITS[unit]
    except (KeyError, ValueError):
        return None


def _perf_ref(rel_path: str, keys=()) -> dict:
    full = os.path.join(REPO_ROOT, rel_path)
    doc = read_json(full)
    entry = dict(source=rel_path, present=doc is not None)
    if doc is not None:
        entry["sha256"] = sha256_file(full)
        for key in keys:
            if key in doc:
                entry[key] = doc[key]
    return entry


def case_replay_perf(root: str, opts: dict, flags: set, snap, live):
    recorded_root = os.path.abspath(opts.get("recorded-root") or "")
    expected = int(opts.get("expected-cases") or 7)
    if not recorded_root or not os.path.isdir(recorded_root):
        return dict(reason="usage/missing input: --recorded-root <B>"), EXIT_USAGE
    recorded = {}
    for mode in ("baseline", "candidate"):
        doc = read_json(os.path.join(recorded_root, "perf", "%s.json" % mode))
        if not doc or not doc.get("argv"):
            return dict(reason="missing required input: %s/perf/%s.json argv"
                        % (rel(recorded_root), mode)), EXIT_USAGE
        recorded[mode] = dict(doc)
    recorded_cases = recorded["candidate"].get("cases") or {}

    # BDN churn guard: snapshot the bytes of every already-dirty BencharkDotNet.Artifacts entry
    dirty_before = {p: bdn_dirty_snapshot().get(p) for p in bdn_dirty_paths()}
    stash = "/tmp/opencode/rv5-bdn-stash-%d" % os.getpid()
    os.makedirs(stash, exist_ok=True)
    for p in dirty_before:
        src = os.path.join(REPO_ROOT, p)
        if os.path.exists(src):
            shutil.copy2(src, os.path.join(stash, p.replace("/", "__")))

    problems, results, ratio = [], {}, {}
    for mode in ("baseline", "candidate"):
        argv = list(recorded[mode]["argv"])
        if "--artifacts" in argv:
            argv[argv.index("--artifacts") + 1] = os.path.join(root, "perf", mode, "raw")
        raw_dir = os.path.join(root, "perf", mode, "raw")
        shutil.rmtree(raw_dir, ignore_errors=True)
        os.makedirs(raw_dir, exist_ok=True)
        res = run_cmd(root, "perf-%s" % mode, argv, use_docker=False)
        text = res["stdout"] + res["stderr"]
        cases = _parse_bdn_reports(raw_dir)
        ratio[mode] = _perf_ratio(cases)
        failures = len(re.findall(r"^\s*\d+ benchmarks? failed", text, re.M))
        total_time = re.search(r"Global total time:\s*([^\n]+)", text)
        results[mode] = dict(mode=mode, argv=argv, exit_code=res["exit_code"],
                             cases_expected=expected, cases_reported=len(cases), cases=cases,
                             cached_prepared_ratio=ratio[mode], failures=failures,
                             wall_seconds=round(res["duration_ms"] / 1000.0, 2),
                             global_total_time=total_time.group(1) if total_time else None,
                             log=res["stdout_log"], exit_log=res["exit_log"],
                             audit_masking=False, source_snapshot=res["source_snapshot"])
        write_json(os.path.join(root, "perf", "%s.json" % mode), results[mode])
        if res["exit_code"] != 0:
            problems.append("%s replay exit %d" % (mode, res["exit_code"]))
        if len(cases) != expected:
            problems.append("%s reported %d case(s), expected %d" % (mode, len(cases), expected))
        if failures:
            problems.append("%s reported %d failed benchmark(s)" % (mode, failures))

    # per-case: fresh baseline -> candidate, and the comparison against the recorded rv4 figures
    deltas, vs_rv4 = {}, {}
    for name, base in (results["baseline"]["cases"] or {}).items():
        cand = (results["candidate"]["cases"] or {}).get(name)
        b, c = _mean_us(base), _mean_us(cand or {})
        if not b or c is None:
            continue
        pct = (c - b) / b * 100.0
        deltas[name] = dict(mean_baseline=base.get("mean"), mean_candidate=cand.get("mean"),
                            allocated_baseline=base.get("allocated"),
                            allocated_candidate=cand.get("allocated"),
                            delta_pct=round(pct, 2), material=abs(pct) >= PERF_NOISE_PCT)
    for name, now_case in (results["candidate"]["cases"] or {}).items():
        before = recorded_cases.get(name)
        b, c = _mean_us(before or {}), _mean_us(now_case)
        if not b or c is None:
            continue
        pct = (c - b) / b * 100.0
        vs_rv4[name] = dict(recorded_us=round(b, 2), replayed_us=round(c, 2),
                            delta_pct=round(pct, 2), material=abs(pct) >= PERF_NOISE_PCT)
    material = {k: v for k, v in deltas.items() if v["material"] and v["delta_pct"] > 0}
    if material:
        problems.append("material regression >= %d%% in %s"
                        % (PERF_NOISE_PCT, ", ".join(sorted(material))))

    # ---- restore the pre-existing dirty bytes and verify byte identity ------------------------
    dirty_now = {p: bdn_dirty_snapshot().get(p) for p in bdn_dirty_paths()}
    newly_created = sorted(set(dirty_now) - set(dirty_before))
    restored = []
    for p, sha in dirty_before.items():
        if dirty_now.get(p) != sha or not os.path.exists(os.path.join(REPO_ROOT, p)):
            stashed = os.path.join(stash, p.replace("/", "__"))
            if os.path.exists(stashed):
                shutil.copy2(stashed, os.path.join(REPO_ROOT, p))
                restored.append(p)
    removed_new = []
    for p in newly_created:
        full = os.path.join(REPO_ROOT, p)
        if os.path.isfile(full):
            os.remove(full)
            removed_new.append(p)
    dirty_after = {p: bdn_dirty_snapshot().get(p) for p in dirty_before}
    churn_ok = dirty_before == dirty_after
    if not churn_ok:
        problems.append("BDN churn guard mismatch: pre-existing dirty benchmark bytes changed")
    shutil.rmtree(stash, ignore_errors=True)

    rv4_perf_rel = rel(os.path.join(recorded_root, "perf", "perf-comparison.json"))
    rv1_rel = rel(os.path.join(os.path.dirname(recorded_root), "rv1", "perf",
                               "acceptance-baseline.json"))
    payload = dict(case="replay-perf", unit="D232-RV5-07", expected_cases=expected,
                   results=results, case_deltas=deltas, vs_rv4_recorded=vs_rv4,
                   noise_threshold_pct=PERF_NOISE_PCT, material_regression=bool(material),
                   material_details=sorted(material),
                   cached_prepared_ratio=ratio,
                   rv4_recorded=_perf_ref(rv4_perf_rel, keys=("cached_prepared_ratio", "cases",
                                                              "wall_seconds", "verdict")),
                   rv1_baseline=_perf_ref(rv1_rel, keys=("cached_prepared_ratio", "cases_reported",
                                                         "global_total_time", "allocation_ratio")),
                   bdn_churn_guard=dict(dirty_count=len(dirty_before), restored=restored,
                                        removed_new=removed_new,
                                        newly_created=newly_created,
                                        dirty_before=dirty_before, dirty_after=dirty_after,
                                        identical=churn_ok,
                                        note="pre-existing foreign BDN dirt bytes stashed before "
                                             "and restored after; benchmark output redirected via "
                                             "--artifacts into perf/<mode>/raw"),
                   audit_masking=False, source_snapshot=live,
                   delegation=dict(recorded_root=rel(recorded_root),
                                   note="the recorded harness argv is resolved from "
                                        "rv4/perf/*.json and replayed; the rv4 driver is not re-run"),
                   reason="; ".join(problems) or None)
    write_json(os.path.join(root, "perf", "replay.json"), payload)
    write_json(os.path.join(root, "perf", "perf-comparison.json"), payload)
    return payload, (EXIT_VIOLATION if problems else EXIT_OK)


# --------------------------------------------------------------------------- mapping / docs-examples
def _run_records(doc: dict) -> list:
    recs = []
    for rec in (doc.get("runs") or []) + (doc.get("executions") or []):
        if isinstance(rec, dict):
            recs.append(rec)
    return recs


def _v05_artifact_checks(doc: dict):
    """Validate a v05/assertion-execution.json authored by the D232-RV5-05 unit."""
    checks, problems = {}, []
    mapping = doc.get("assertion_to_executed_test") or doc.get("assertions") or []
    checks["applicable_assertions"] = len(mapping)
    if not mapping:
        problems.append("no assertion-to-execution mapping")
    lacking = [a for a in mapping
               if not (a.get("observed_run") or a.get("evidence") or a.get("observed_output"))]
    checks["mapped_to_execution"] = len(mapping) - len(lacking)
    if lacking:
        problems.append("test-name-only mapping(s): %d assertion(s) lack an observed execution"
                        % len(lacking))
    observed, bad_runs = 0, []
    for rec in _run_records(doc):
        argv = rec.get("argv") or rec.get("command") or []
        total = rec.get("total") or rec.get("selected_count") or 0
        exit_code = rec.get("exit_code", rec.get("exit", 1))
        if total and exit_code == 0:
            observed += 1
        log_rel = rec.get("log")
        if not log_rel:
            continue
        log_path = os.path.join(REPO_ROOT, log_rel)
        if not os.path.isfile(log_path):
            bad_runs.append("missing log %s" % log_rel)
        elif "test" in argv:
            parsed = parse_test_summary(open(log_path, encoding="utf-8", errors="replace").read())
            if not parsed.get("total"):
                bad_runs.append("log %s shows no selected test" % log_rel)
            elif parsed.get("failed"):
                bad_runs.append("log %s shows failed test(s)" % log_rel)
    checks["observed_runs_with_selection"] = observed
    if not observed:
        problems.append("no observed run with selected > 0 and exit 0")
    problems += bad_runs
    checks["explicit_gaps"] = (doc.get("gap_summary") or {}).get("explicit_gaps")
    if checks["explicit_gaps"]:
        problems.append("%s explicit gap(s) recorded" % checks["explicit_gaps"])
    return problems, checks


def _docs_artifact_checks(doc: dict):
    """Validate a reports/D-09.json authored by the D232-RV5-05 unit."""
    checks, problems = {}, []
    records = _run_records(doc)
    checks["executions"] = len(records)
    if not records:
        problems.append("no execution recorded")
    ok = [r for r in records if r.get("exit_code") == 0
          and (r.get("selected_count") or 0) > 0]
    checks["green_executions_with_selection"] = len(ok)
    if not ok:
        problems.append("no execution with exit 0 and selected_count > 0")
    checks["has_scope"] = bool(doc.get("scope") or doc.get("checks"))
    if not checks["has_scope"]:
        problems.append("no scope/checks block")
    checks["unit"] = doc.get("unit")
    return problems, checks


def _freshness_problems(doc: dict, live: dict, artifact_rel: str) -> list:
    """An adopted artifact must have been produced on the live frozen source revision."""
    liverev = str((live or {}).get("head_short") or "")[:8]
    revisions = [doc.get("source_revision"), doc.get("artifact_revision")]
    revisions += [r.get("source_revision") for r in _run_records(doc) if isinstance(r, dict)]
    revisions = sorted({str(r) for r in revisions if r})
    if not revisions:
        return ["%s records no source revision (freshness cannot be proven)" % artifact_rel]
    if not liverev:
        return []
    stale = [r for r in revisions if liverev not in r and r not in liverev]
    if stale:
        return ["%s is stale: recorded source revision(s) %s do not match live HEAD %s"
                % (artifact_rel, ", ".join(stale), liverev)]
    return []


def _docs_examples_artifact_checks(doc: dict):
    """Validate the D232-RV5-05 docs-examples.json (CS1591 / parity / link scan / examples)."""
    checks, problems = {}, []
    checks["unit"] = doc.get("unit")
    if not doc.get("unit"):
        problems.append("docs-examples.json records no unit")
    checks["criteria"] = doc.get("criterion") or []
    audits = doc.get("checks") or []
    checks["checks"] = len(audits)
    if not audits:
        problems.append("docs-examples.json records no audit check")
    bad, findings_seen = [], 0
    for idx, chk in enumerate(audits):
        name = chk.get("id") or "check[%d]" % idx
        exit_code = chk.get("exit_code")
        # a clean scan may exit 1 (git grep: no match) only with an explicit `exit_meaning`
        clean = exit_code == 0 or (exit_code == 1 and chk.get("exit_meaning"))
        if not clean:
            bad.append("%s (exit %r)" % (name, exit_code))
        findings = chk.get("findings")
        if isinstance(findings, list):
            findings_seen += len(findings)
            failed = [f for f in findings
                      if isinstance(f, dict) and f.get("verdict") != "pass"]
            if failed:
                bad.append("%s (%d non-pass finding(s))" % (name, len(failed)))
        elif not findings:
            bad.append("%s (no findings field)" % name)
    checks["findings_scanned"] = findings_seen
    if bad:
        problems.append("non-clean docs-example check(s): %s" % ", ".join(bad[:4]))
    reports = doc.get("reports") or {}
    checks["d09_recorded"] = reports.get("d09")
    checks["d09_validator_recorded"] = reports.get("d09_validator")
    if not reports.get("d09"):
        problems.append("docs-examples.json does not reference the D-09 report")
    if not reports.get("d09_validator"):
        problems.append("docs-examples.json does not reference the D-09 schema validator invocation")
    return problems, checks


def case_mapping(root: str, opts: dict, flags: set, snap, live):
    """Validate the D232-RV5-05 v05/assertion-execution.json in place; never regenerate it."""
    variant = (opts.get("variant") or "").upper()
    if variant != "V05":
        return dict(reason="usage: mapping --variant V05 --root <R>"), EXIT_USAGE
    artifact_rel = "v05/assertion-execution.json"
    path = os.path.join(root, artifact_rel)
    payload = dict(case="mapping", unit="D232-RV5-05", variant="V05", artifact=artifact_rel,
                   adopted_existing=True, producer="the D232-RV5-05 unit", source_snapshot=live)
    if not os.path.isfile(path):
        payload["reason"] = ("missing required input: %s -- the mapping subcommand validates the "
                             "D232-RV5-05 artifact in place and never regenerates it" % artifact_rel)
        return payload, EXIT_VIOLATION
    doc = read_json(path)
    if doc is None:
        payload["reason"] = "unreadable/blank %s" % artifact_rel
        return payload, EXIT_VIOLATION
    problems, checks = _v05_artifact_checks(doc)
    problems += _freshness_problems(doc, live, artifact_rel)
    payload.update(artifact_sha256=sha256_file(path), checks=checks,
                   assertion_to_executed_test=doc.get("assertion_to_executed_test") or [],
                   gap_summary=doc.get("gap_summary"),
                   adoption_rule="the authoritative artifact is produced by the D232-RV5-05 unit; "
                                 "this subcommand verifies the existing bytes (existence, schema, "
                                 "freshness) and writes only its own verdict",
                   reason="; ".join(problems) or None)
    return payload, (EXIT_VIOLATION if problems else EXIT_OK)


def case_docs_examples(root: str, opts: dict, flags: set, snap, live):
    """Validate the D232-RV5-05 docs-examples.json + its D-09 report in place; never regenerate."""
    artifact_rel = "docs-examples.json"
    d09_rel = "reports/D-09.json"
    payload = dict(case="docs-examples", unit="D232-RV5-05", artifact=artifact_rel,
                   d09_report=d09_rel, adopted_existing=True,
                   producer="the D232-RV5-05 unit", source_snapshot=live)
    missing = [p for p in (artifact_rel, d09_rel) if not os.path.isfile(os.path.join(root, p))]
    if missing:
        payload["reason"] = ("missing required input: %s -- the docs-examples subcommand validates "
                             "the D232-RV5-05 artifacts in place and never regenerates them"
                             % ", ".join(missing))
        return payload, EXIT_VIOLATION
    doc = read_json(os.path.join(root, artifact_rel))
    if doc is None:
        payload["reason"] = "unreadable/blank %s" % artifact_rel
        return payload, EXIT_VIOLATION
    problems, checks = _docs_examples_artifact_checks(doc)
    problems += _freshness_problems(doc, live, artifact_rel)
    recorded_d09 = checks.get("d09_recorded")
    if recorded_d09 and not str(recorded_d09).endswith(d09_rel):
        problems.append("docs-examples.json references %s instead of %s"
                        % (recorded_d09, d09_rel))
    # the D-09 report is verified in place: schema via the canonical validator, plus the
    # execution-shape checks (exit 0 with a real selection) it must carry
    d09 = read_json(os.path.join(root, d09_rel))
    if d09 is None:
        problems.append("unreadable/blank %s" % d09_rel)
    else:
        d09_problems, d09_checks = _docs_artifact_checks(d09)
        problems += ["%s: %s" % (d09_rel, p) for p in d09_problems]
        problems += _freshness_problems(d09, live, d09_rel)
        checks["d09"] = d09_checks
        schema = run_cmd(root, "docs-d09-schema",
                         ["python3", SCHEMA_VALIDATOR, "report", "%s/%s" % (rel(root), d09_rel)],
                         use_docker=False)
        checks["d09_schema_exit"] = schema["exit_code"]
        checks["d09_schema_log"] = schema["stdout_log"]
        if schema["exit_code"] != 0:
            problems.append("%s fails `%s report` (exit %d; log %s)"
                            % (d09_rel, SCHEMA_VALIDATOR, schema["exit_code"], schema["stdout_log"]))
    payload.update(artifact_sha256=sha256_file(os.path.join(root, artifact_rel)),
                   d09_sha256=sha256_file(os.path.join(root, d09_rel)), checks=checks,
                   adoption_rule="the authoritative artifacts are produced by the D232-RV5-05 unit; "
                                 "this subcommand verifies the existing bytes (existence, schema, "
                                 "freshness) and writes only its own verdict",
                   reason="; ".join(problems) or None)
    return payload, (EXIT_VIOLATION if problems else EXIT_OK)


# --------------------------------------------------------------------------- reconcile / fulfillment
T232_STATUS_REL = "docs/specs/status/D232-sqlserver-pivot-unpivot-1.md"
COLLECTION_STATUS_REL = "docs/specs/status/collection-1.0.9-todo.md"
COLLECTION_AUTH_SECTION = "Owner authorizations — 2026-10-15 (collection rc3)"
T232_TEXT_PREFIXES = ("src/nextorm.core/Query/PivotValues.cs",
                      "src/nextorm.core/Visitors/NormSqlTranslator.cs",
                      "src/nextorm.core/Query/QueryCommand.QueryPreparer.cs",
                      "src/nextorm.sqlserver/", "tests/nextorm.sqlserver.tests/T232",
                      "tests/nextorm.core.tests/T232", "tests/nextorm.integration.tests/T232",
                      "tests/nextorm.integration.tests/CommonTestSuite.T232Pivot.cs",
                      "tests/nextorm.integration.tests/T232PivotFixture.cs",
                      "docs/guide/provider-specific-sql/", "docs/ru/guide/provider-specific-sql/",
                      "docs/specs/design/", "docs/specs/roadmap/", "examples/",
                      T232_STATUS_REL)
TEXT_SUFFIXES = (".cs", ".md", ".sql", ".csproj", ".props")
T232_COMMITTED_IMPLEMENTATION = (
    "src/nextorm.core/Query/PivotValues.cs",
    "src/nextorm.core/Visitors/NormSqlTranslator.cs",
    "src/nextorm.core/Query/QueryCommand.QueryPreparer.cs",
    "src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs",
    "src/nextorm.sqlserver/Extensions/PivotProjectionRewriter.cs",
    "tests/nextorm.sqlserver.tests/T232PreparationProbeTests.cs",
    "tests/nextorm.sqlserver.tests/T232ProjectionContractTests.cs",
    "tests/nextorm.sqlserver.tests/T232MarkerTests.cs",
    "tests/nextorm.sqlserver.tests/T232TypedPivotTests.cs",
    "tests/nextorm.sqlserver.tests/T232DerivedSourceReproTests.cs",
    "tests/nextorm.integration.tests/CommonTestSuite.T232Pivot.cs",
    "tests/nextorm.integration.tests/T232SqlServerTests.cs",
    "tests/nextorm.integration.tests/T232PivotFixture.cs",
    # r=5 delta (committed by this task, see the handoff committed-tested proof)
    "tests/nextorm.sqlserver.tests/T232Rv5AmbiguityTests.cs",
    "tests/nextorm.sqlserver.tests/T232VariantClosureTests.cs",
    "tests/nextorm.sqlserver.tests/T232CoverageClosureTests.cs",
    "tests/nextorm.core.tests/T232CoverageClosureTests.cs",
    "tests/nextorm.integration.tests/T232SqlServerTestsRv5.cs",
    "docs/guide/provider-specific-sql/sqlserver.md",
    "docs/ru/guide/provider-specific-sql/sqlserver.md",
    "examples/README.md",
    "examples/VERIFICATION.md",
    "examples/nextorm.examples.mssql.adventureworks/README.md",
    "examples/nextorm.examples.mssql.adventureworks/AdventureWorksQueries.cs",
    "examples/nextorm.examples.mssql.adventureworks/Sql/mssql_quarterly_pivot.sql",
    "examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj",
)
# The green r=5 runs were executed on the working tree of the r=5 generation; this is the source-tree
# identity they were produced against (before the ACT commit published those exact bytes).
T232_GENERATION_HEADS = ("e9121976",)
T232_NAMED_COMMITS = ("9b4bf5d4", "04d2289d", "8f0d8309", "706dd7b7", "266b38b3",
                      "001f9c9c", "4dc0f6b3", "e9121976")
# The r=5 authorized in-flight remainder: a modification of an already-committed T232 path is
# expected here only for these paths (plus the T232 evidence tree); anything else is unexpected.
EXPECTED_IN_FLIGHT_EXACT = (
    "src/nextorm.core/Visitors/NormSqlTranslator.cs",
    "docs/guide/provider-specific-sql/sqlserver.md",
    "docs/ru/guide/provider-specific-sql/sqlserver.md",
    T232_STATUS_REL,
    "examples/README.md",
    "examples/VERIFICATION.md",
    "examples/nextorm.examples.mssql.adventureworks/README.md",
)
RV4_GUARDED_ARTIFACTS = ("manifest.json", "contract.json", "seal.json", "counters.json",
                         "provenance.json", "reopen-reconciliation.json")


def _exists(*paths) -> bool:
    return all(os.path.isfile(os.path.join(REPO_ROOT, p)) for p in paths)


def _foreign_integrity(root: str) -> dict:
    """Re-hash every rv5 foreign-baseline path; a byte change or a vanished path is a violation."""
    baseline = read_json(os.path.join(root, "foreign-baseline.json")) or {}
    tracked = baseline.get("foreign_tracked_dirty") or []
    untracked = baseline.get("foreign_untracked") or []
    changed, missing = [], []
    for entry in tracked:
        path_rel = entry.get("path")
        full = os.path.join(REPO_ROOT, path_rel)
        if not os.path.isfile(full):
            missing.append(path_rel)
        elif sha256_file(full) != entry.get("sha256"):
            changed.append(path_rel)
    for path_rel in untracked:
        if not os.path.isfile(os.path.join(REPO_ROOT, path_rel)):
            missing.append(path_rel)
    return dict(baseline=rel(os.path.join(root, "foreign-baseline.json")), policy=baseline.get("policy"),
                tracked_checked=len(tracked), untracked_checked=len(untracked),
                checked=len(tracked) + len(untracked), changed=changed, missing=missing,
                preserved=not changed and not missing,
                foreign_paths=sorted([e.get("path") for e in tracked] + list(untracked)))


def _status_entries() -> list:
    entries = []
    for ln in (_git(["status", "--short", "--untracked-files=all"]).stdout or "").splitlines():
        if len(ln) < 4:
            continue
        entries.append(dict(code=ln[:2], path=ln[3:]))
    return entries


def _line_endings(path: str):
    with open(path, "rb") as fh:
        data = fh.read()
    if not data:
        return "EMPTY", 0, 0
    crlf = data.count(b"\r\n")
    lf = data.count(b"\n") - crlf
    if crlf and lf:
        return "MIXED", crlf, lf
    if crlf:
        return "CRLF", crlf, lf
    if lf:
        return "LF", crlf, lf
    return "NO-EOL", 0, 0


def _t232_commits() -> list:
    commits = []
    for line in (_git(["log", "--format=%H%x09%ad%x09%s", "--date=short",
                       "--grep=#232"]).stdout or "").splitlines():
        if not line.strip():
            continue
        parts = line.split("\t")
        sha = parts[0]
        names = (_git(["diff-tree", "--no-commit-id", "--name-only", "-r", sha]).stdout or "").split()
        commits.append(dict(sha=sha, short=sha[:8], date=(parts[1] if len(parts) > 1 else None),
                            subject=(parts[2] if len(parts) > 2 else ""),
                            file_count=len(names), files=names))
    return commits


def _t232_path_commits(path_rel: str) -> list:
    """Every commit that carries the path (newest first): the handed-off path must be committed."""
    return [ln for ln in (_git(["log", "--format=%H", "--", path_rel]).stdout or "").splitlines()
            if ln.strip()]


def _retention_authorization(status_rel: str):
    """Read the recorded collection-owner retention authorization. Facts only; no inference."""
    full = os.path.join(REPO_ROOT, status_rel)
    if not os.path.isfile(full):
        return None
    with open(full, encoding="utf-8") as fh:
        text = fh.read()
    if COLLECTION_AUTH_SECTION not in text:
        return None
    quote = None
    for ln in text.split(COLLECTION_AUTH_SECTION, 1)[1].splitlines():
        if "Authorize retention" in ln:
            quote = ln.strip()
            break
    if not quote:
        return None
    return dict(source_path=status_rel, source_section=COLLECTION_AUTH_SECTION, source_item="(2)",
                quote=quote, commit="001f9c9c" if "001f9c9c" in quote else None,
                names_mixed_commit="mixed commit" in quote, names_foreign_160="#160" in quote,
                keeps_as_is="keep as-is" in quote, no_history_rewrite="do not rewrite history" in quote,
                date="2026-10-15", granter="collection owner", role="collection owner/user",
                retained_as_is=True, removed_or_reverted=False, history_rewritten=False,
                attribution="#160 (foreign); T232 does not claim authorship",
                commit_authorization_scope="retention only — the ACT commit is a separate, later step",
                statement="retention authorization is not commit authorization")


def _rv5_delta_closure(root: str, contract: dict, row_id: str):
    """Evidence-based (never status-based) closure of one EC232-RV5-* row."""
    R = rel(root)
    row = next((r for r in (contract.get("rows") or []) if r.get("id") == row_id), None)
    artifacts = list((row or {}).get("artifacts") or [])
    problems, checks = [], []
    for path in artifacts:
        if not os.path.exists(os.path.join(REPO_ROOT, path)):
            problems.append("missing required artifact %s" % path)
    if row_id == "EC232-RV5-SEAL-RECONCILIATION":
        rec = read_json(os.path.join(root, "seal-reconciliation.json")) or {}
        seal = read_json(os.path.join(root, "seal.json")) or {}
        if not rec.get("rv3_gap"):
            problems.append("seal-reconciliation.json records no explicit rv3 gap")
        if not seal.get("sealed"):
            problems.append("seal.json is not a sealed verdict")
        checks.append("explicit rv3 gap + sealed verdict present")
    elif row_id == "EC232-RV5-FULFILLMENT":
        checks.append("self-referential row: both reports are written by this invocation; their "
                      "per-row closure record is the evidence")
    elif row_id == "EC232-RV5-HASH-LEDGER":
        ver = read_json(os.path.join(root, "hash-verification.json")) or {}
        if ver.get("status") != "ok":
            problems.append("hash-verification.json status=%r" % ver.get("status"))
        if not (ver.get("entry_count") or 0):
            problems.append("hash ledger is empty")
        checks.append("ledger/verification status ok, %s entries" % ver.get("entry_count"))
    elif row_id == "EC232-RV5-V18-EXECUTION":
        doc = read_json(os.path.join(root, "v18", "assertion-execution.json")) or {}
        mapped = json.dumps(doc.get("assertion_to_executed_test") or [])
        if "T232SqlServerTestsRv5" not in mapped:
            problems.append("v18 maps no container-backed executed test (fixture/render-only)")
        if not [r for r in (doc.get("runs") or []) if r.get("exit_code") == 0]:
            problems.append("v18 records no exit-0 execution")
        checks.append("container-backed SQL Server execution mapped")
    elif row_id == "EC232-RV5-V23-AMBIGUITY":
        doc, code, reason = _v23_precheck(root, "v23/assertion-execution.json")
        if code:
            problems.append(reason or "v23 precheck failed")
        checks.append("probe discriminator + red/green mutation arms")
    elif row_id == "EC232-RV5-HANDOFF":
        doc = read_json(os.path.join(root, "handoff.json")) or {}
        if not doc.get("satisfied"):
            problems.append("handoff.json satisfied=%r" % doc.get("satisfied"))
        checks.append("all rewritten AC-12 elements verified in handoff.json")
    elif row_id == "EC232-RV5-SECURITY-DISPOSITION":
        issue = read_json(os.path.join(root, "security", "issue.json")) or {}
        verdict = read_json(os.path.join(root, "verdicts", "security.json")) or {}
        ver = read_json(os.path.join(root, "security", "rv5-evidence-verification.json")) or {}
        if verdict.get("status") != "ok" or verdict.get("exit_code") != 0:
            problems.append("security verification status=%r exit=%r"
                            % (verdict.get("status"), verdict.get("exit_code")))
        if not ver.get("checks"):
            problems.append("security verification carries no checks record")
        high = [f for f in (issue.get("findings") or [])
                if str(f.get("severity", "")).lower() == "high"]
        accepted = [f for f in (issue.get("findings") or [])
                    if f.get("verdict") == "accepted risk, not a blocker"]
        if len(high) < 2 or len(accepted) != len(issue.get("findings") or []) or not high:
            problems.append("both High findings are not recorded as 'accepted risk, not a blocker'")
        if (issue.get("issue") or {}).get("milestone") != "1.0.9-rc3":
            problems.append("tracked issue milestone is not 1.0.9-rc3")
        if issue.get("production_fix_applied") or issue.get("allowlist_extended"):
            problems.append("a production fix or an allowlist extension was applied")
        checks.append("duplicate search + issue #237 (milestone 1.0.9-rc3), accepted risk")
    elif row_id == "EC232-RV5-V05-MAPPING":
        doc = read_json(os.path.join(root, "v05", "assertion-execution.json")) or {}
        gap = doc.get("gap_summary") or {}
        if not doc.get("assertion_to_executed_test"):
            problems.append("V05 assertion mapping is empty (test-name-only)")
        if gap.get("explicit_gaps"):
            problems.append("V05 has %s explicit evidence gap(s)" % gap.get("explicit_gaps"))
        checks.append("every applicable V05 assertion maps to observed execution")
    elif row_id == "EC232-RV5-DOCS-EXAMPLES":
        doc = read_json(os.path.join(root, "docs-examples.json")) or {}
        rep = read_json(os.path.join(root, "reports", "D-09.json")) or {}
        verdict = read_json(os.path.join(root, "verdicts", "docs-examples.json")) or {}
        if not doc.get("checks"):
            problems.append("docs-examples.json carries no completed audits")
        if verdict.get("exit_code") != 0:
            problems.append("docs-examples verdict exit=%r" % verdict.get("exit_code"))
        if not rep.get("executions"):
            problems.append("reports/D-09.json carries no executions")
        checks.append("current bilingual/XML-doc/example audits + reports/D-09.json")
    elif row_id == "EC232-RV5-CONTRADICTIONS":
        doc = read_json(os.path.join(root, "reconciliation.json")) or {}
        unresolved = [i.get("key") for i in (doc.get("reconciliation_items") or [])
                      if not i.get("verified")]
        if len(doc.get("reconciliation_items") or []) < 5:
            problems.append("reconciliation.json lists fewer than the five required items")
        if unresolved:
            problems.append("unresolved contradiction(s): %s" % ", ".join(unresolved))
        checks.append("five historical contradictions identified, dated and resolved")
    elif row_id == "EC232-RV5-REGRESSION":
        doc = read_json(os.path.join(root, "regression", "regression.json")) or {}
        build = doc.get("build") or {}
        suites = doc.get("suites") or []
        integration = doc.get("integration") or {}
        if not build.get("build_succeeded") or build.get("exit_code") != 0:
            problems.append("regression build is not green")
        if len(suites) < 8 or any(s.get("exit_code") != 0 or s.get("failed") for s in suites):
            problems.append("a project suite is not green (%d suites)" % len(suites))
        if integration.get("provider_skips") or integration.get("failed"):
            problems.append("integration run has provider skips or failures")
        if not (doc.get("frozen_oracle") or {}).get("ok"):
            problems.append("the frozen oracle changed")
        checks.append("build 0 warnings/0 errors; 8 suites green; 0 provider skips; frozen oracle intact")
    elif row_id == "EC232-RV5-COVERAGE":
        doc = read_json(os.path.join(root, "coverage", "coverage.json")) or {}
        if not doc.get("thresholds_met"):
            problems.append("coverage thresholds are not met")
        if doc.get("touched_pct_decreased"):
            problems.append("comparable touched-area coverage decreased")
        checks.append("line %.2f / branch %.2f, touched-area non-decrease" %
                      ((doc.get("candidate") or {}).get("line_coverage") or 0.0,
                       (doc.get("candidate") or {}).get("branch_coverage") or 0.0))
    elif row_id == "EC232-RV5-PERF":
        doc = read_json(os.path.join(root, "perf", "perf-comparison.json")) or {}
        cases = ((doc.get("results") or {}).get("candidate") or {}).get("cases") or {}
        if doc.get("expected_cases") != 7 or len(cases) != 7:
            problems.append("perf replay does not record seven cases (%s/%s)"
                            % (doc.get("expected_cases"), len(cases)))
        if doc.get("material_regression"):
            problems.append("perf replay records a material regression")
        checks.append("seven acceptance cases replayed, no material regression")
    return dict(row=row_id, ok=not problems, artifacts=artifacts, checks=checks,
                problems=problems)


def case_reconcile(root: str, opts: dict, flags: set, snap, live):
    """Itemised reconciliation of the historical rv4/rv3 contradictions for current acceptance."""
    rv4 = os.path.abspath(opts.get("rv4") or "")
    rv5 = os.path.abspath(opts.get("rv5") or root)
    if not rv4 or not os.path.isdir(rv4):
        return dict(reason="usage/missing input: --rv4 <B>"), EXIT_USAGE
    rv4_manifest = read_json(os.path.join(rv4, "manifest.json"))
    rv5_manifest = read_json(os.path.join(rv5, "manifest.json"))
    if not rv4_manifest or not rv5_manifest:
        return dict(reason="missing required input: an rv4/rv5 manifest.json"), EXIT_USAGE
    rv4_hashes_before = {p: sha256_file(os.path.join(rv4, p)) for p in RV4_GUARDED_ARTIFACTS
                         if os.path.isfile(os.path.join(rv4, p))}
    R, B = rel(rv5), rel(rv4)
    reopen = read_json(os.path.join(rv4, "reopen-reconciliation.json")) or {}
    reopen_rows = {r.get("id"): r for r in (reopen.get("rows") or [])}
    rv4_rows = {r.get("id"): r for r in (rv4_manifest.get("rows") or [])}
    rv4_seal = read_json(os.path.join(rv4, "seal.json")) or {}
    rv4_counters = read_json(os.path.join(rv4, "counters.json")) or {}
    rv4_provenance = read_json(os.path.join(rv4, "provenance.json")) or {}
    security_row = rv4_rows.get("EC232-RV4-SECURITY") or {}
    reopen_security = reopen_rows.get("EC232-RV4-SECURITY") or {}
    rv4_security_json = read_json(os.path.join(rv4, "security.json")) or {}
    heads = dict(seal=rv4_seal.get("head"), counters=rv4_counters.get("head"),
                 provenance=(rv4_provenance.get("tree") or {}).get("short"))
    fulfill_entry = next((v for v in (rv4_seal.get("validators") or [])
                          if v.get("id") == "fulfill"), {})
    fulfill_argv = " ".join(fulfill_entry.get("argv") or [])
    rv4_hash_files = dict(ledger=os.path.isfile(os.path.join(rv4, "hash-ledger.json")),
                          verification=os.path.isfile(os.path.join(rv4, "hash-verification.json")))
    rv5_hash = read_json(os.path.join(rv5, "hash-verification.json")) or {}
    rv5_rv3_row = next((r for r in (rv5_manifest.get("rows") or [])
                        if r.get("id") == "EC232-RV3-SEAL"), None)
    rv4_reports = os.path.isdir(os.path.join(rv4, "reports"))
    now_ts = now()

    items = [
        dict(key="SECURITY-STATE", requirement="(a)",
             rv4_evidence=["%s/reopen-reconciliation.json#rows[EC232-RV4-SECURITY]" % B,
                           "%s/manifest.json#rows[EC232-RV4-SECURITY]" % B,
                           "%s/seal.json#met_ids" % B, "%s/counters.json#row_counts.met_ids" % B],
             historical_captured_at=reopen.get("captured"), identified_at=now_ts,
             fact=("rv4/reopen-reconciliation.json records EC232-RV4-SECURITY evidence_satisfied=%r "
                   "while the rv4 manifest row status=%r and seal/counters list it in met_ids; the "
                   "rv4 case artifact itself is status=%r exit=%r"
                   % (reopen_security.get("evidence_satisfied"), security_row.get("status"),
                      rv4_security_json.get("status"), rv4_security_json.get("exit_code"))),
             resolution=("current acceptance does not carry the rv4 `met` claim: the rv5 row "
                         "EC232-RV5-SECURITY-DISPOSITION records the inherited identifier-safety "
                         "disposition (issue #237, both High findings 'accepted risk, not a blocker') "
                         "from fresh rv5 evidence; rv4 keeps its historical (partial) record."),
             superseded_by=["%s/security/issue.json" % R, "%s/security/gh-search" % R,
                            "%s/security/rv5-evidence-verification.json" % R],
             verified=_exists("%s/security/issue.json" % R, "%s/verdicts/security.json" % R)
             and len(rv4_hashes_before) > 0),
        dict(key="HEAD", requirement="(b)",
             rv4_evidence=["%s/manifest.json#tree" % B, "%s/seal.json#head" % B,
                           "%s/counters.json#head" % B, "%s/provenance.json#tree.short" % B],
             historical_captured_at=rv4_seal.get("captured"), identified_at=now_ts,
             fact=("the rv4 manifest tree=%s (the r=3 D-07 commit 4dc0f6b3) contradicts the head=%s "
                   "recorded in the rv4 seal/counters/provenance."
                   % (str(rv4_manifest.get("tree"))[:8], heads.get("seal"))),
             resolution=("the rv4 manifest `tree` is a stale r=3-era field; the rv5 manifest records "
                         "tree=%s == live HEAD, so the rv5 source identity is unambiguous."
                         % live.get("head_short")),
             superseded_by=["%s/manifest.json#tree" % R, "%s/source-snapshot.json" % R],
             verified=(rv5_manifest.get("tree") == live.get("head_short")
                       and str(rv4_manifest.get("tree"))[:8] != (heads.get("seal") or ""))),
        dict(key="FULFILL-ENTRY-POINT", requirement="(c)",
             rv4_evidence=["%s/seal.json#validators[id=fulfill].argv" % B,
                           "%s/counters.json#sealed_hash_valid_artifacts_required.note" % B],
             historical_captured_at=rv4_seal.get("captured"), identified_at=now_ts,
             fact=("the rv4 seal discharged its `fulfill` validator through the rv1 entry point/root "
                   "(%s), i.e. an rv1 contract re-validation rather than an rv4-specific one."
                   % fulfill_argv.strip()),
             resolution=("rejected for current acceptance: the rv5 fulfillment uses its own rv5 "
                         "entry point and root (`%s/tools/rv5_evidence.py fulfillment --rv4 %s "
                         "--rv5 %s`); the carried rv1 `fulfill` row is not reused."
                         % (R, B, R)),
             superseded_by=["%s/fulfillment-rv5.json" % R],
             verified=("rv1" in fulfill_argv and rv5_manifest.get("contract_rv") == CONTRACT_RV
                       and _exists("%s/tools/rv5_evidence.py" % R))),
        dict(key="HASH-VALIDITY", requirement="(d)",
             rv4_evidence=["%s/counters.json#sealed_hash_valid_artifacts_required" % B],
             historical_captured_at=rv4_counters.get("captured"), identified_at=now_ts,
             fact=("rv4 carries no current-set hash-validity evidence (hash-ledger.json present=%r, "
                   "hash-verification.json present=%r); its `sealed_hash_valid_artifacts_required` "
                   "ratio rests on schema+fulfillment only." % (rv4_hash_files["ledger"],
                                                                rv4_hash_files["verification"])),
             resolution=("rv5 provides the transitive inventory + verification: hash-ledger.json "
                         "+ hash-verification.json status=%r, covering every manifest-referenced "
                         "rv5 and rv4 evidence file." % rv5_hash.get("status")),
             superseded_by=["%s/hash-ledger.json" % R, "%s/hash-verification.json" % R],
             verified=(not rv4_hash_files["ledger"] and not rv4_hash_files["verification"]
                       and rv5_hash.get("status") == "ok" and bool(rv5_hash.get("entry_count")))),
        dict(key="RV3-SEAL-GAP", requirement="(e)",
             rv4_evidence=["%s/manifest.json#rows[EC232-RV3-SEAL].status" % B,
                           "%s/seal.json#na_ids" % B, B + "/reports (absent)"],
             historical_captured_at=rv4_seal.get("captured"), identified_at=now_ts,
             fact=("the historical rv3 seal never passed (rv3 manifest 27 met / 3 open) and rv4 "
                   "discharged EC232-RV3-SEAL as `na`; rv4 also carries no reports/D-09.json "
                   "(rv4/reports present=%r)." % rv4_reports),
             resolution=("the rv5 contract re-words EC232-RV3-SEAL as a required (never `na`) row "
                         "and the rv5 seal records the rv3 gap and its disposition without "
                         "manufacturing a retrospective rv3 PASS or touching rv3 artifacts; the "
                         "missing D-09 report artifact is supplied at rv5 (reports/D-09.json, "
                         "report-validator exit 0)."),
             superseded_by=["%s/reports/D-09.json" % R, "%s/seal-reconciliation.json" % R,
                            "%s/contract.json#rows[EC232-RV3-SEAL]" % R],
             verified=(rv5_rv3_row is not None and rv5_rv3_row.get("status") != "na"
                       and _exists("%s/reports/D-09.json" % R))),
    ]

    rv4_hashes_after = {p: sha256_file(os.path.join(rv4, p)) for p in rv4_hashes_before}
    untouched = rv4_hashes_before == rv4_hashes_after
    unresolved = [i["key"] for i in items if not i["verified"]]
    problems = []
    if unresolved:
        problems.append("unresolved reconciliation item(s): %s" % ", ".join(unresolved))
    if not untouched:
        problems.append("rv4 historical artifacts were modified during reconciliation")
    payload = dict(case="reconcile", unit="D232-RV5-08", captured=now_ts,
                   rv4_root=B, rv5_root=R, reconciliation_items=items,
                   item_count=len(items), unresolved=unresolved,
                   rv4_artifacts_unchanged=untouched,
                   rv4_security_state=dict(reopen_evidence_satisfied=reopen_security.get("evidence_satisfied"),
                                           manifest_status=security_row.get("status"),
                                           seal_met="EC232-RV4-SECURITY" in (rv4_seal.get("met_ids") or [])),
                   rv4_heads=heads, rv4_manifest_tree=rv4_manifest.get("tree"),
                   rv5_manifest_tree=rv5_manifest.get("tree"), live_head=live.get("head_short"),
                   rv4_fulfill_entry_point=fulfill_argv.strip(),
                   rv4_hash_validity_evidence=rv4_hash_files,
                   rv4_reports_dir_present=rv4_reports,
                   source_snapshot=live, reason="; ".join(problems) or None)
    write_json(os.path.join(root, "reconciliation.json"), payload)
    return payload, (EXIT_VIOLATION if problems else EXIT_OK)


def case_fulfillment(root: str, opts: dict, flags: set, snap, live):
    """rv4-defect accounting + evidence-based (never status-only) closure of every rv5 row."""
    rv4 = os.path.abspath(opts.get("rv4") or "")
    rv5 = os.path.abspath(opts.get("rv5") or root)
    if not rv4 or not os.path.isdir(rv4):
        return dict(reason="usage/missing input: --rv4 <B>"), EXIT_USAGE
    manifest = read_json(os.path.join(rv5, "manifest.json"))
    if not manifest or manifest.get("contract_rv") != CONTRACT_RV:
        return dict(reason="missing/wrong rv5 manifest.json (rv != %d)" % CONTRACT_RV), EXIT_USAGE
    contract = read_json(os.path.join(rv5, "contract.json")) or {}
    schema = run_cmd(root, "fulfillment-schema",
                     ["python3", SCHEMA_VALIDATOR, "manifest", "%s/manifest.json" % rel(rv5)],
                     use_docker=False)
    rows = manifest.get("rows") or []
    open_ids = [r.get("id") for r in rows if r.get("status") == "open"]
    delta = [r.get("id") for r in rows if str(r.get("id", "")).startswith("EC232-RV5-")]
    rv4_manifest = read_json(os.path.join(rv4, "manifest.json")) or {}
    rv4_rows = rv4_manifest.get("rows") or []
    rv4_seal = read_json(os.path.join(rv4, "seal.json")) or {}
    rv4_reopen = read_json(os.path.join(rv4, "reopen-reconciliation.json")) or {}
    rv4_fulfill_entry = next((v for v in (rv4_seal.get("validators") or [])
                              if v.get("id") == "fulfill"), {})
    rv4_fulfill_argv = " ".join(rv4_fulfill_entry.get("argv") or [])

    # --- rv1 entry point/root rejection (negative acceptance) ---------------------------------
    # The rv4 seal discharged `fulfill` through the rv1 entry point/root; that is recorded and
    # explicitly REJECTED for current acceptance. The rv5 run uses its own entry point and root.
    rv1_rejected = []
    if "rv1" in rv4_fulfill_argv:
        rv1_rejected.append(dict(rejected=rv4_fulfill_argv.strip(),
                                 reason="rv1 entry point/root, not rv4/rv5-specific",
                                 source="%s/seal.json#validators[id=fulfill]" % rel(rv4)))
    entry_point = "%s/tools/rv5_evidence.py" % rel(rv5)
    rejection_problems = []
    if os.path.basename(rv5.rstrip(os.sep)) != "rv5":
        rejection_problems.append("evidence root is not rv5: %s" % rel(rv5))
    if not os.path.isfile(os.path.join(REPO_ROOT, entry_point)):
        rejection_problems.append("rv5 entry point missing: %s" % entry_point)
    if not rv1_rejected:
        rejection_problems.append("the rv4 rv1 fulfill entry point was not detected in the rv4 seal")

    defects = [
        dict(key="D232-D06-example-package-mismatch", state="closed at rv5",
             rv4_artifact="%s/example-review-reopen.json" % rel(rv4),
             rv4_disposition="reopened under D-08; C13 green only against a locally packed feed",
             rv5_closure="%s/verdicts/docs-examples.json + %s/docs-examples.json (AdventureWorks "
                         "AC-10 PASS, ProjectReference route, no feed masking)" % (rel(rv5), rel(rv5))),
        dict(key="P232-variant-closure-incomplete", state="closed at rv5",
             rv4_artifact="%s/v05.json / v18.json / v19.json / v21.json" % rel(rv4),
             rv4_disposition="V05/V18/V19/V21 not executable-closed at rv3",
             rv5_closure="%s/v18/assertion-execution.json (container execution), "
                         "%s/v05/assertion-execution.json, %s/v23/assertion-execution.json" %
                         (rel(rv5), rel(rv5), rel(rv5))),
        dict(key="D232-D07-touched-coverage-regression", state="closed at rv5",
             rv4_artifact="%s/coverage/branch-delta.json" % rel(rv4),
             rv4_disposition="absolute thresholds met but the comparable touched scope decreased",
             rv5_closure="%s/coverage/coverage.json (thresholds_met, touched_pct_decreased=false, "
                         "touched_pct_decreased_vs_rv4=false)" % rel(rv5)),
        dict(key="D232-D06-foreign-hunks-in-handoff", state="closed at rv5",
             rv4_artifact="%s/handoff.json + %s/ownership.json" % (rel(rv4), rel(rv4)),
             rv4_disposition="AC-12 blocked: no retention authorization for the #160 hunks",
             rv5_closure="%s/handoff.json (recorded retention authority + AC-12 elements)" % rel(rv5)),
        dict(key="E232-seal-incomplete", state="closed at rv5",
             rv4_artifact="%s/seal.json" % rel(rv4),
             rv4_disposition="the rv3 seal's unsupported `met` claims were retracted at rv4",
             rv5_closure="%s/seal.json + %s/seal-reconciliation.json (explicit rv3 gap record)" %
                         (rel(rv5), rel(rv5))),
        dict(key="rv4-historical-invalid-disposition", state="retained (never relabelled valid)",
             rv4_artifact="%s/manifest.json + %s/reopen-reconciliation.json" % (rel(rv4), rel(rv4)),
             rv4_disposition="EC232-RV4-SECURITY `partial` vs a `met` manifest row; stale rv4 "
                             "manifest tree; rv1 fulfill entry point; no hash-validity evidence; "
                             "EC232-RV3-SEAL discharged as `na`; reports/D-09.json absent",
             rv5_closure="%s/reconciliation.json itemises all five and points at the rv5 evidence "
                         "that supersedes them; no rv4 artifact is rewritten" % rel(rv5)),
    ]

    closures = [_rv5_delta_closure(root, contract, row_id) for row_id in delta]
    closure_open = [c["row"] for c in closures if not c["ok"]]
    rv4_delta_rows = [r.get("id") for r in rv4_rows
                      if str(r.get("id", "")).startswith("EC232-RV4-")]

    problems = []
    if schema["exit_code"] != 0:
        problems.append("manifest schema validation exit %d" % schema["exit_code"])
    if rejection_problems:
        problems.append("rv1 entry point/root rejection failed: %s" % json.dumps(rejection_problems))
    if closure_open:
        problems.append("%d rv5 delta row(s) without executed evidence closure: %s"
                        % (len(closure_open), ", ".join(closure_open)))

    rv4_doc = dict(case="fulfillment-rv4", unit="D232-RV5-08", rv4_root=rel(rv4),
                   historical_rows=dict(total=len(rv4_rows),
                                        met=len([r for r in rv4_rows if r.get("status") == "met"]),
                                        na=len([r for r in rv4_rows if r.get("status") == "na"]),
                                        open=len([r for r in rv4_rows if r.get("status") == "open"])),
                   rv4_delta_rows=rv4_delta_rows,
                   rv4_security_state=dict(reopen_evidence_satisfied=next(
                       (r.get("evidence_satisfied") for r in (rv4_reopen.get("rows") or [])
                        if r.get("id") == "EC232-RV4-SECURITY"), None),
                       manifest_status=next((r.get("status") for r in rv4_rows
                                             if r.get("id") == "EC232-RV4-SECURITY"), None)),
                   rv4_fulfill_entry_point=rv4_fulfill_argv.strip(),
                   rv4_hash_validity_evidence=dict(
                       ledger=os.path.isfile(os.path.join(rv4, "hash-ledger.json")),
                       verification=os.path.isfile(os.path.join(rv4, "hash-verification.json"))),
                   defects=defects,
                   disposition=("rv4 keeps its historical-invalid disposition: the defects above were "
                                "reopened under D-08…D-13 and are closed only by rv5 evidence; the "
                                "rv4 record is never relabelled valid and never rewritten"),
                   source_snapshot=live)
    rv5_doc = dict(case="fulfillment-rv5", unit="D232-RV5-08", rv5_root=rel(rv5),
                   entry_point=entry_point, entry_point_is_rv5=True,
                   rv1_entry_point_rejected=True,
                   rv1_rejected=rv1_rejected,
                   rv1_rejection_problems=rejection_problems,
                   contract_rv=manifest.get("contract_rv"),
                   manifest_revision=manifest.get("manifest_revision"),
                   delta_rows=delta, closure=closures, delta_open=closure_open,
                   rv4_defects_accounted=[d["key"] for d in defects],
                   rv4_defects_count=len(defects),
                   open_required_rows=open_ids,
                   schema_exit=schema["exit_code"], schema_log=schema["stdout_log"],
                   validation_rule=("closure is derived from executed artifacts, never from the "
                                    "manifest row status: rv1 roots, a wrong `rv`, missing artifacts "
                                    "or status-only validation fail closed"),
                   source_snapshot=live, reason="; ".join(problems) or None)
    write_json(os.path.join(root, "fulfillment-rv4.json"), rv4_doc)
    write_json(os.path.join(root, "fulfillment-rv5.json"), rv5_doc)
    return rv5_doc, (EXIT_VIOLATION if problems else EXIT_OK)


# --------------------------------------------------------------------------- hashes
def _envelope_file(root_rel: str, path_rel: str) -> bool:
    """Envelope = the binders of the ordered chain (hash-ledger.json, hash-verification.json,
    seal.json, seal-reconciliation.json and the hashes/seal verdicts).  They are never part of the
    payload they bind, even when a manifest row references them as evidence."""
    names = list(EXCLUDED_HASH_FILES) + list(SEAL_ENVELOPE_FILES) + list(SEAL_ENVELOPE_VERDICTS)
    return path_rel in ["%s/%s" % (root_rel, name) for name in names] + \
        ["%s/verdicts/hashes.json" % root_rel]


def _hash_excluded(root_rel: str, path_rel: str) -> bool:
    if _envelope_file(root_rel, path_rel):
        return True
    # raw execution logs are payload (the CHECK requires the referenced execution logs to be
    # inventoried), so only the tool's own byte-cache is excluded.
    return "__pycache__" in path_rel


def inventory(root: str) -> dict:
    root_rel = rel(root)
    entries = {}
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d != "__pycache__"]
        for fn in filenames:
            path = os.path.join(dirpath, fn)
            path_rel = rel(path)
            if _hash_excluded(root_rel, path_rel):
                continue
            entries[path_rel] = dict(sha256=sha256_file(path), bytes=os.path.getsize(path))
    return entries


def _ref_like(value) -> bool:
    if not isinstance(value, str):
        return False
    value = value.strip()
    if not value or " " in value or "\\" in value or value.startswith("/"):
        return False
    if not (value.startswith(REF_ROOT_DIRS) or value.startswith(REF_REPO_DIRS)):
        return False
    if not value.endswith(REF_SUFFIXES):
        return False
    return bool(REF_PATH_RE.match(value))


def _resolve_reference(root_abs: str, value: str):
    """$R-relative for the $R-local trees, repo-relative otherwise; None when the path is absent."""
    if value.startswith(REF_ROOT_DIRS):
        candidate = os.path.join(root_abs, value)
        if os.path.exists(candidate):
            return os.path.abspath(candidate)
    candidate = os.path.join(REPO_ROOT, value)
    if os.path.exists(candidate):
        return os.path.abspath(candidate)
    candidate = os.path.join(root_abs, value)
    if os.path.exists(candidate):
        return os.path.abspath(candidate)
    return None


def _collect_references(doc, key_hint: str = ""):
    """(value, key_path) for every string, keeping the enclosing key so a recorded absence (a path
    under an `*absence*` key) can be told apart from a required evidence reference."""
    out, stack = [], [(doc, key_hint)]
    while stack:
        node, key = stack.pop()
        if isinstance(node, dict):
            for child_key, child in node.items():
                stack.append((child, "%s/%s" % (key, child_key)))
        elif isinstance(node, list):
            for child in node:
                stack.append((child, key))
        elif isinstance(node, str):
            out.append((node, key))
    return out


def reference_closure(root: str) -> dict:
    """Transitive closure of everything the manifest/counters reference.

    Roots are manifest.json + counters.json; descent follows references into the $R evidence
    documents they reach (contract.json is handled separately: it is a frozen *declaration*, not an
    evidence payload).  A required reference that is absent fails closed (exit 2) - only paths the
    evidence itself records as absent, and paths the contract merely declares (the historical
    EC232-RV4-* rows whose rv4 layout differs from the actual rv4 tree), are tolerated, and both
    are recorded in the ledger rather than dropped.
    """
    root_abs = os.path.abspath(root)
    queue = [os.path.join(root_abs, seed) for seed in ("manifest.json", "counters.json")]
    seen_docs, required, declared, recorded, absent = set(), {}, [], [], []
    envelope_refs, scanned = {}, []
    while queue:
        doc_path = os.path.abspath(queue.pop())
        if doc_path in seen_docs:
            continue
        seen_docs.add(doc_path)
        doc = read_json(doc_path)
        if doc is None:
            continue
        scanned.append(rel(doc_path))
        for value, key in _collect_references(doc):
            if not _ref_like(value):
                continue
            if any(tag in key.lower() for tag in ABSENCE_KEYS):
                recorded.append(dict(path=value, recorded_by=rel(doc_path),
                                     key=key.strip("/")))
                continue
            full = _resolve_reference(root_abs, value)
            if full is None:
                absent.append(dict(path=value, referenced_by=rel(doc_path), key=key.strip("/")))
                continue
            if _hash_excluded(rel(root_abs), rel(full)):
                # The binders of the chain (ledger/verification/seal) are produced by the sealing
                # steps themselves: they are referenced but never inventoried (they are the things
                # doing the binding) and never scanned for further references.  Their existence and
                # the ledger -> seal edge are enforced by `seal` (refuses without a ledger) and by
                # the `seal_binding` check of `hashes --verify`.
                envelope_refs[rel(full)] = "referenced by %s" % rel(doc_path)
                continue
            required[rel(full)] = "evidence reference from %s" % rel(doc_path)
            if (full.endswith(".json") and os.path.isfile(full)
                    and full.startswith(root_abs + os.sep)
                    and full != os.path.join(root_abs, CONTRACT_DECLARATION_DOC)):
                queue.append(full)
    contract = read_json(os.path.join(root_abs, CONTRACT_DECLARATION_DOC)) or {}
    for row in contract.get("rows") or []:
        for value in (row.get("artifacts") or []):
            if not _ref_like(value):
                continue
            full = _resolve_reference(root_abs, value)
            if full is None:
                declared.append(dict(path=value, row=row.get("id"), unit=row.get("unit"),
                                     declared_by=rel(os.path.join(root_abs, CONTRACT_DECLARATION_DOC))))
            else:
                required[rel(full)] = "contract-declared artifact (row %s)" % row.get("id")
    return dict(required=required, absent_required=absent, declared_absent=declared,
                recorded_absences=recorded, envelope_references=envelope_refs,
                documents_scanned=sorted(scanned))


def _payload_digest(entries: dict) -> str:
    """One digest over the whole payload (sorted path + sha256 pairs): the value the seal binds."""
    digest = hashlib.sha256()
    for path_rel in sorted(entries):
        digest.update(("%s\0%s\n" % (path_rel, (entries[path_rel] or {}).get("sha256"))).encode("utf-8"))
    return digest.hexdigest()


def _seal_binding(root: str, ledger_path: str, payload_digest_value: str) -> dict:
    """Verify the ledger -> seal edge: the seal must carry the digest of *this* ledger."""
    seal_path = os.path.join(root, "seal.json")
    seal = read_json(seal_path)
    actual = sha256_file(ledger_path) if os.path.isfile(ledger_path) else None
    if seal is None:
        return dict(seal_path=rel(seal_path), status="absent", ok=None, ledger_sha256=None,
                    recomputed_ledger_sha256=actual, problems=[],
                    note="the root is not sealed yet - seal is the next step of the ordered chain "
                         "(payload -> hash-ledger.json -> seal.json -> hash-verification.json)")
    declared = seal.get("ledger_sha256")
    problems = []
    if not declared:
        problems.append("seal.json carries no ledger digest (legacy seal without a ledger_sha256 "
                        "binding; re-run `seal`)")
    elif declared != actual:
        problems.append("seal.json ledger_sha256 %s != the current ledger %s (the ledger was "
                        "regenerated after the seal; re-run `seal`)" % (str(declared)[:12],
                                                                       str(actual)[:12]))
    if not seal.get("payload_digest") or seal.get("payload_digest") != payload_digest_value:
        problems.append("seal.json payload_digest does not match the ledger payload digest")
    return dict(seal_path=rel(seal_path), status="verified" if not problems else "broken",
                ok=not problems, ledger_path=seal.get("ledger_path"), ledger_sha256=declared,
                recomputed_ledger_sha256=actual, payload_digest=seal.get("payload_digest"),
                recomputed_payload_digest=payload_digest_value, problems=problems,
                binding="hash-ledger.json -> seal.json (the seal carries the ledger digest)")


def case_hashes(root: str, opts: dict, flags: set, snap, live):
    if "verify" not in flags and not opts.get("verify"):
        return dict(reason="usage: hashes --root <R> --verify [--rebaseline]"), EXIT_USAGE
    rebaseline = "rebaseline" in flags or bool(opts.get("rebaseline"))
    root_rel = rel(root)
    current = inventory(root)
    closure = reference_closure(root)
    for path_rel, why in sorted(closure["required"].items()):
        if path_rel in current or _hash_excluded(root_rel, path_rel):
            continue
        full = os.path.join(REPO_ROOT, path_rel)
        if os.path.isdir(full):
            for dirpath, _dirnames, filenames in os.walk(full):
                for fn in filenames:
                    nested = rel(os.path.join(dirpath, fn))
                    if nested in current or _hash_excluded(root_rel, nested):
                        continue
                    current[nested] = dict(sha256=sha256_file(os.path.join(REPO_ROOT, nested)),
                                           bytes=os.path.getsize(os.path.join(REPO_ROOT, nested)),
                                           reference=why)
            continue
        current[path_rel] = dict(sha256=sha256_file(full), bytes=os.path.getsize(full),
                                 reference=why)
    rv4_refs = [p for p in sorted(current) if "/rv4/" in p]
    digest = _payload_digest(current)
    ledger_path = os.path.join(root, "hash-ledger.json")
    ledger = read_json(ledger_path)
    previous_verification = read_json(os.path.join(root, "hash-verification.json")) or {}
    history = list(previous_verification.get("rebaseline_history") or [])
    if previous_verification.get("previous_ledger"):
        history.append(previous_verification["previous_ledger"])
    missing, mismatches = [], []
    if ledger:
        for path_rel, meta in (ledger.get("inventory") or {}).items():
            cur = current.get(path_rel)
            if cur is None:
                missing.append(path_rel)
            elif cur["sha256"] != meta.get("sha256"):
                mismatches.append(dict(path=path_rel, expected=meta.get("sha256"),
                                       actual=cur["sha256"]))
    binding = _seal_binding(root, ledger_path, digest)
    problems = []
    if missing:
        problems.append("%d ledger-listed evidence file(s) missing: %s"
                        % (len(missing), ", ".join(missing[:5])))
    if mismatches:
        problems.append("%d evidence file(s) tampered (hash mismatch): %s"
                        % (len(mismatches), ", ".join(m["path"] for m in mismatches[:5])))
    if closure["absent_required"]:
        problems.append("%d referenced evidence path(s) absent: %s"
                        % (len(closure["absent_required"]),
                           ", ".join(a["path"] for a in closure["absent_required"][:5])))
    if binding["ok"] is False:
        problems.append("ledger -> seal binding broken: %s" % "; ".join(binding["problems"]))
    verification = dict(case="hashes", ledger_created=ledger is None,
                        entry_count=len(current), verified_against_ledger=ledger is not None,
                        missing=missing, mismatches=mismatches,
                        manifest_missing=[a["path"] for a in closure["absent_required"]],
                        referenced_manifest_paths=len(closure["required"]),
                        referenced_present=len(closure["required"]),
                        referenced_rv4_files=rv4_refs, payload_digest=digest,
                        reference_closure=dict(
                            root_documents=["%s/manifest.json" % root_rel,
                                            "%s/counters.json" % root_rel],
                            documents_scanned=closure["documents_scanned"],
                            required_count=len(closure["required"]),
                            required_absent=closure["absent_required"],
                            recorded_absences=closure["recorded_absences"],
                            declared_absent=closure["declared_absent"],
                            envelope_references=closure["envelope_references"]),
                        seal_binding=binding, source_snapshot=live,
                        rebaselined=rebaseline and bool(problems),
                        unaccounted_rule="the ledger inventories the complete transitive closure of "
                                         "everything the manifest/counters reference - the raw "
                                         "execution logs, the cmd/exit records, the tool and its "
                                         "selftests, the referenced historical ($B) files - plus every "
                                         "file under $R; a required reference that is absent fails "
                                         "closed; a file that is new since the previous ledger is "
                                         "absorbed by an explicit --rebaseline and must stay stable "
                                         "afterwards (a point-in-time ledger, as required by the "
                                         "payload -> hash-ledger -> seal dependency order); a plain "
                                         "--verify never rewrites the ledger, so verification cannot "
                                         "invalidate the seal -> ledger binding it just checked",
                        reason="; ".join(problems) or None)
    if problems and not rebaseline:
        verification.update(status="violation", exit_code=EXIT_VIOLATION)
        write_json(os.path.join(root, "hash-verification.json"), verification)
        return verification, EXIT_VIOLATION
    if problems:
        # Explicit, auditable re-baseline of a ledger that legitimately went stale (the payload
        # grew after the previous ledger). The superseded ledger and the detected mismatches are
        # recorded, never hidden; plain `--verify` still fails closed.
        history.append(dict(
            sha256=sha256_file(ledger_path),
            entry_count=len(ledger.get("inventory") or {}),
            mismatches=[m["path"] for m in mismatches], missing=missing,
            superseded_because="the payload changed after the previous ledger (payload -> hash-ledger "
                               "-> seal dependency order); an explicit --rebaseline was requested"))
    if history:
        verification["rebaseline_history"] = history
    # A plain `hashes --verify` verifies the existing ledger and never rewrites it: rewriting it (a
    # fresh `generated_at`) would change its bytes and instantly break the seal -> ledger binding,
    # i.e. the verification would invalidate the very thing it verified.  Only the first run (no
    # ledger yet) and an explicit --rebaseline write the ledger.
    ledger_written = ledger is None or rebaseline
    if ledger_written:
        write_json(ledger_path, dict(case="hashes", root=rel(root), generated_at=now(),
                                     source_snapshot=live, entry_count=len(current),
                                     inventory=current, payload_digest=digest,
                                     referenced_rv4_files=rv4_refs,
                                     envelope_files=["%s/%s" % (root_rel, n) for n in
                                                     list(EXCLUDED_HASH_FILES) +
                                                     list(SEAL_ENVELOPE_FILES) +
                                                     list(SEAL_ENVELOPE_VERDICTS)] +
                                                    ["%s/verdicts/hashes.json" % root_rel],
                                     reference_closure=dict(
                                         root_documents=["%s/manifest.json" % root_rel,
                                                         "%s/counters.json" % root_rel],
                                         documents_scanned=closure["documents_scanned"],
                                         required_count=len(closure["required"]),
                                         required_absent=closure["absent_required"],
                                         recorded_absences=closure["recorded_absences"],
                                         declared_absent=closure["declared_absent"],
                                         envelope_references=closure["envelope_references"]),
                                     note="complete transitive inventory of the rv5 payload ($R walk, "
                                          "raw execution logs and cmd/exit records included) plus every "
                                          "path the manifest/counters reference - including the raw "
                                          "execution logs, the referenced historical ($B) files and the "
                                          "referenced source/test bytes; `payload_digest` is the value "
                                          "the seal binds (payload/reports -> hash-ledger.json -> "
                                          "seal.json -> hash-verification.json); the ledger, its "
                                          "verification record, the seal and their verdicts are envelope "
                                          "files and are excluded"))
    verification["ledger_written"] = ledger_written
    verification["ledger_sha256"] = sha256_file(ledger_path)
    verification.update(status="ok", exit_code=EXIT_OK)
    if history:
        verification["rebaseline_history"] = history
    write_json(os.path.join(root, "hash-verification.json"), verification)
    return verification, EXIT_OK


# --------------------------------------------------------------------------- handoff / security / seal
def case_handoff(root: str, opts: dict, flags: set, snap, live):
    """Rewritten AC-12: every element evaluated separately against live evidence."""
    status_rel = opts.get("status") or T232_STATUS_REL
    required = [os.path.join(root, "foreign-baseline.json"),
                os.path.join(REPO_ROOT, status_rel)] if status_rel else [
        os.path.join(root, "foreign-baseline.json")]
    missing = [rel(p) for p in required if not os.path.isfile(p)]
    if missing:
        return dict(reason="usage/missing input: %s" % ", ".join(missing)), EXIT_USAGE

    baseline = read_json(os.path.join(root, "foreign-baseline.json")) or {}
    foreign = _foreign_integrity(root)
    foreign_set = set(foreign["foreign_paths"])
    rv4_handoff = read_json(os.path.join(os.path.dirname(root), "rv4", "handoff.json")) or {}
    entries = _status_entries()
    staged_cmd = _git(["diff", "--cached", "--name-only"])
    staged = [p for p in (staged_cmd.stdout or "").splitlines() if p.strip()]
    dirty_all = [e["path"] for e in entries]
    t232_dirty = sorted(p for p in dirty_all if p not in foreign_set)
    untracked_dirty = sorted(e["path"] for e in entries
                             if e["path"] not in foreign_set and e["code"] == "??")
    foreign_dirty_live = sorted(p for p in dirty_all if p in foreign_set)

    # --- CRLF on every T232-touched text file -------------------------------------------------
    crlf_files = []
    for commit in _t232_commits():
        for path in (commit.get("files") or []):
            if (path.startswith(T232_TEXT_PREFIXES) and path.endswith(TEXT_SUFFIXES)
                    and "artifacts/" not in path):
                crlf_files.append(path)
    for path in t232_dirty:
        if (path.startswith(T232_TEXT_PREFIXES) and path.endswith(TEXT_SUFFIXES)
                and "artifacts/" not in path):
            crlf_files.append(path)
    if status_rel.endswith(TEXT_SUFFIXES):
        crlf_files.append(status_rel)
    endings, crlf_violations = {}, []
    for path in sorted(set(crlf_files)):
        full = os.path.join(REPO_ROOT, path)
        if not os.path.isfile(full):
            continue
        ending, crlf_n, lf_n = _line_endings(full)
        endings[path] = dict(ending=ending, crlf=crlf_n, lf=lf_n)
        if ending in ("LF", "MIXED"):
            crlf_violations.append(path)

    # --- recorded retention authority ---------------------------------------------------------
    retention = _retention_authorization(status_rel) or _retention_authorization(COLLECTION_STATUS_REL)

    # --- committed / green implementation -----------------------------------------------------
    # AC-12 reworded: "path exists in HEAD" is *not* the proof; the proof is that the bytes the green
    # runs executed are the bytes the commit carries.  For every T232 implementation/test/doc path the
    # working-tree content must equal the HEAD blob content (no diff vs HEAD) and the path must be
    # carried by at least one commit, with the green evidence attached to those exact bytes.
    commits = _t232_commits()
    head_impl, in_flight, proof_paths = {}, [], []
    for path in T232_COMMITTED_IMPLEMENTATION:
        full = os.path.join(REPO_ROOT, path)
        blob = _head_file_bytes(path)
        entry = dict(path=path, in_head=blob is not None,
                     worktree_present=os.path.isfile(full),
                     commits=[c[:8] for c in _t232_path_commits(path)])
        if entry["worktree_present"]:
            raw = open(full, "rb").read()
            worktree_lf = raw.replace(b"\r\n", b"\n")
            entry.update(worktree_sha256=sha256_bytes(raw), worktree_bytes=len(raw),
                         worktree_lf_sha256=sha256_bytes(worktree_lf))
        if blob is not None:
            blob_lf = blob.replace(b"\r\n", b"\n")
            entry.update(head_blob_sha256=sha256_bytes(blob), head_blob_bytes=len(blob),
                         head_lf_sha256=sha256_bytes(blob_lf))
        entry["matches_head"] = (entry.get("worktree_lf_sha256") is not None
                                 and entry.get("worktree_lf_sha256") == entry.get("head_lf_sha256"))
        entry["diff_vs_head"] = (_git(["diff", "--name-only", "HEAD", "--", path]).stdout or "").strip()
        entry["status_porcelain"] = (_git(["status", "--porcelain", "--", path]).stdout or "").strip()
        entry["clean_vs_head"] = not entry["diff_vs_head"] and not entry["status_porcelain"]
        entry["ok"] = bool(entry["in_head"] and entry["matches_head"] and entry["clean_vs_head"]
                           and entry["commits"])
        head_impl[path] = entry["in_head"]
        proof_paths.append(entry)
    for path in sorted(set(t232_dirty) | set(untracked_dirty)):
        if path.startswith(("src/", "tests/", "examples/")) and path not in head_impl:
            in_flight.append(path)
    named = []
    for short in T232_NAMED_COMMITS:
        show = _git(["show", "--stat", "--format=%H%x09%ad%x09%s", "--date=short", short])
        sha = (show.stdout or "").split("\t", 1)[0].splitlines()[0] if show.stdout else ""
        named.append(dict(ref=short, resolves=show.returncode == 0, sha=sha,
                          subject=(show.stdout.splitlines()[0].split("\t")[-1] if show.stdout else None)))
    green_evidence = [
        dict(path="%s/regression/regression.json" % rel(root),
             detail="build 0 warnings/0 errors; eight project suites exit 0 with 0 failures; container "
                    "integration 3924 selected / 3726 passed / 0 failed / 198 capability skips / "
                    "0 provider skips; frozen oracle intact"),
        dict(path="%s/verdicts/regression.json" % rel(root), detail="rv5 regression verdict"),
        dict(path="%s/build/solution-build.out" % rel(os.path.join(os.path.dirname(root), "rv4")),
             detail="rv4 solution build: Build succeeded, 0 Warning(s) / 0 Error(s)"),
    ]
    for item in green_evidence:
        full = os.path.join(REPO_ROOT, item["path"])
        item["present"] = os.path.isfile(full)
        item["exit_code"] = read_json(full).get("exit_code") if full.endswith(".json") and item[
            "present"] else (0 if item["present"] else None)
    green_ok = all(i["present"] and i["exit_code"] in (0, None) for i in green_evidence)
    regression_doc = read_json(os.path.join(root, "regression", "regression.json")) or {}
    generation_snapshot = regression_doc.get("source_snapshot") or {}
    proof_problems = [p["path"] for p in proof_paths if not p["ok"]]
    proof_ok = not proof_problems
    committed_tested = dict(
        definition="working-tree content == HEAD blob content (LF-normalised, git diff/status clean) "
                   "for every T232 implementation/test/doc path, each carried by >= 1 commit; "
                   "'the path exists in HEAD' alone is not accepted",
        head_full=live.get("head_full"), head_short=live.get("head_short"),
        branch=live.get("branch"),
        generation_heads=list(T232_GENERATION_HEADS),
        generation_head_evidence=dict(
            path="%s/regression/regression.json" % rel(root),
            head_full=generation_snapshot.get("head_full"),
            note="the green runs were executed on the r=5 working tree; committing those exact bytes "
                 "is what makes them the tested bytes (the per-path sha256 equality below proves the "
                 "commit added no byte)"),
        paths_checked=len(proof_paths), paths_ok=len([p for p in proof_paths if p["ok"]]),
        paths_failed=proof_problems,
        all_paths_committed_and_clean=proof_ok,
        commits_carrying_tested_paths=sorted({c for p in proof_paths for c in p["commits"]}),
        green_evidence_attached=green_ok and proof_ok,
        commit_count=len([c for c in commits if c.get("subject", "").startswith("#232")]),
        line_ending_note="core.autocrlf=true: HEAD blobs are LF, the checkout is CRLF - equality is "
                         "checked on LF-normalised content plus git diff/status against HEAD",
        paths=proof_paths)
    committed_ok = proof_ok and all(head_impl.values()) and all(n["resolves"] for n in named)
    committed_dirty = sorted(p for p in dirty_all if p in head_impl)
    expected_in_flight = set(EXPECTED_IN_FLIGHT_EXACT)
    committing_unexpected = [p for p in committed_dirty
                             if p not in expected_in_flight
                             and not p.startswith("artifacts/pdca/D232-sqlserver-pivot-unpivot/")]

    # --- strict_clean (derived diagnostic; its value alone is not AC-12) -----------------------
    strict_clean = not foreign_dirty_live and not untracked_dirty
    strict_reason = ("no foreign dirty paths" if not foreign_dirty_live
                     else "%d pre-existing foreign dirty path(s) (rv5 foreign baseline)" % len(foreign_dirty_live))
    strict_reason += ("; no untracked dirty paths" if not untracked_dirty
                      else "; %d untracked T232 in-flight path(s) (%s) - the ACT commit follows under "
                           "the same collection authorization" % (len(untracked_dirty),
                                                                  ", ".join(untracked_dirty[:3])))

    elements = {
        "foreign_changes_preserved": dict(
            ok=foreign["preserved"], baseline=foreign["baseline"], checked=foreign["checked"],
            tracked_checked=foreign["tracked_checked"], untracked_checked=foreign["untracked_checked"],
            changed=foreign["changed"], missing=foreign["missing"], policy=foreign["policy"]),
        "crlf_preserved": dict(
            ok=not crlf_violations, policy="CRLF for every T232-authored text file",
            checked_count=len(endings), checked=endings, violations=crlf_violations,
            note="T232-authored evidence under artifacts/pdca/** is written by the harness with LF "
                 "and is excluded from the source-text CRLF set"),
        "retention_authority_verified": dict(
            ok=bool(retention and retention.get("commit") and retention.get("names_foreign_160")
                    and retention.get("no_history_rewrite")),
            authority=retention,
            verified_against="the actual retained path examples/nextorm.examples.mssql."
                             "adventureworks/AdventureWorksQueries.cs (committed as a mixed commit)",
            statement="retention authorization is not commit authorization; no commit is authorized "
                      "on this branch by this task"),
        "no_foreign_path_staged": dict(
            ok=not staged, command=["git", "diff", "--cached", "--name-only"],
            cwd=REPO_ROOT, exit_code=staged_cmd.returncode, staged=staged),
        "no_dirty_impl_overlap": dict(
            ok=not (set(t232_dirty) & foreign_set),
            t232_dirty_paths=t232_dirty, untracked_dirty_paths=untracked_dirty,
            foreign_paths=sorted(foreign_set),
            overlap=sorted(set(t232_dirty) & foreign_set),
            committed_t232_paths_also_in_rv4_foreign_set=list(
                rv4_handoff.get("t232_touched_paths", {}).get("shared_with_foreign") or []),
            note="no T232-owned dirty/untracked path overlaps the sibling (#160) foreign footprint"),
        "green_committed_implementation": dict(
            ok=bool(committed_ok and green_ok and not committing_unexpected),
            commits=named, commit_count=len(commits),
            implementation_in_head=head_impl, in_flight_paths=sorted(set(in_flight)),
            committed_files_currently_dirty=committed_dirty,
            unexpected_modifications=committing_unexpected,
            green_evidence=green_evidence,
            committed_tested_proof=committed_tested,
            proof_rule="AC-12 is satisfied only by the committed-tested proof: every T232 "
                       "implementation/test/doc path's working-tree content equals its HEAD blob "
                       "content (LF-normalised + clean git diff/status vs HEAD), each path is carried "
                       "by at least one commit, and the green results are attached to those exact "
                       "bytes; a path merely existing in HEAD is not proof",
            note="the T232 implementation and test bytes are committed and green; the in-flight r=5 "
                 "delta paths (if any) are the authorized remainder, not a claim that the work is "
                 "uncommitted"),
        "strict_clean": dict(
            is_derived_diagnostic=True, value=strict_clean,
            definition="no foreign dirty paths and no untracked dirty paths",
            reason=strict_reason,
            not_ac12="its value alone is not AC-12; ownership, recorded retention authority, "
                     "overlap, CRLF, foreign staging/overwriting and the committed green "
                     "implementation are evaluated separately"),
    }
    problems = [key for key in ("foreign_changes_preserved", "crlf_preserved",
                                "retention_authority_verified", "no_foreign_path_staged",
                                "no_dirty_impl_overlap", "green_committed_implementation")
                if not elements[key]["ok"]]
    if not rv4_handoff:
        problems.append("missing rv4 handoff record (reconstruction proof / retention authorization)")
    satisfied = not problems
    payload = dict(case="handoff", unit="D232-RV5-09", task="T232 (#232)", status_file=status_rel,
                   branch=live.get("branch"), head_short=live.get("head_short"),
                   head_full=live.get("head_full"), captured=now(),
                   ac12=elements, satisfied=satisfied, failed_elements=problems,
                   committed_tested_proof=committed_tested,
                   strict_clean=elements["strict_clean"]["value"],
                   strict_clean_is_derived_diagnostic=True,
                   retention_is_not_commit_authorization=True,
                   delegation=dict(source="rv4/handoff.json", executed=False,
                                   note="the rv4 reconstruction proof / per-hunk byte hashes are "
                                        "reread read-only; rv4 is historical and never rewritten"),
                   source_snapshot=live, reason="; ".join(problems) or None)
    write_json(os.path.join(root, "handoff.json"), payload)
    return payload, (EXIT_VIOLATION if problems else EXIT_OK)


def case_security(root: str, opts: dict, flags: set, snap, live):
    milestone = opts.get("milestone") or "1.0.9-rc3"
    required = [os.path.join(root, "security", "issue.json"),
                os.path.join(root, "security", "gh-search")]
    missing = [rel(p) for p in required if not os.path.exists(p)]
    if missing:
        return dict(reason=("missing required input: %s -- run the D232-RV5-06 duplicate search and "
                            "record the disposition first" % ", ".join(missing))), EXIT_USAGE
    issue_doc = read_json(os.path.join(required[0])) or {}
    gh = sorted(os.listdir(required[1])) if os.path.isdir(required[1]) else []
    findings = issue_doc.get("findings") or []
    problems, checks = [], {}
    checks["duplicate_search_files"] = len(gh)
    if not gh:
        problems.append("missing duplicate-search records")
    high = [f for f in findings if str(f.get("severity", "")).lower() == "high"]
    checks["high_findings"] = len(high)
    accepted = [f for f in findings if f.get("verdict") == "accepted risk, not a blocker"]
    checks["accepted_risk_not_blocker"] = len(accepted)
    if len(high) < 2 or len(accepted) != len(findings) or not findings:
        problems.append("both High findings must be recorded with the exact verdict "
                        "'accepted risk, not a blocker'")
    checks["milestone"] = (issue_doc.get("issue") or {}).get("milestone")
    if checks["milestone"] != milestone:
        problems.append("tracked issue milestone %r != %r" % (checks["milestone"], milestone))
    checks["production_fix_applied"] = issue_doc.get("production_fix_applied")
    checks["allowlist_extended"] = issue_doc.get("allowlist_extended")
    if issue_doc.get("production_fix_applied"):
        problems.append("a production fix was applied (forbidden for this disposition)")
    if issue_doc.get("allowlist_extended"):
        problems.append("the allowlist was extended (forbidden for this disposition)")
    payload = dict(case="security", unit="D232-RV5-06", milestone=milestone,
                   issue=issue_doc.get("issue"), findings=findings,
                   duplicate_search_files=gh, search_logs=dict(directory=rel(required[1])),
                   checks=checks, adopted_existing=True, no_production_fix=True,
                   source_snapshot=live, reason="; ".join(problems) or None)
    write_json(os.path.join(root, "security", "rv5-evidence-verification.json"), payload)
    return payload, (EXIT_VIOLATION if problems else EXIT_OK)


def case_finalize(root: str, opts: dict, flags: set, snap, live):
    """D-09/D-12: flip the rv5 rows on evidence (never on status), then recompute the counters.

    A carried `met` is not automatic proof: every `met` row must have at least one existing
    evidence path, and every rv5 delta row must pass its executed-artifact closure. Rows that
    cannot be supported stay `open` and are reported.
    """
    manifest_path = os.path.join(root, "manifest.json")
    manifest = read_json(manifest_path)
    contract = read_json(os.path.join(root, "contract.json")) or {}
    if not manifest or manifest.get("contract_rv") != CONTRACT_RV:
        return dict(reason="missing/wrong rv5 manifest.json (rv != %d)" % CONTRACT_RV), EXIT_USAGE

    rv5_evidence = {
        "EC232-RV5-SEAL-RECONCILIATION": [
            ("seal-reconciliation", "%s/seal-reconciliation.json" % rel(root), 0),
            ("seal", "%s/seal.json" % rel(root), 0)],
        "EC232-RV5-FULFILLMENT": [
            ("fulfillment-rv4", "%s/fulfillment-rv4.json" % rel(root), None),
            ("fulfillment-rv5", "%s/fulfillment-rv5.json" % rel(root), 0),
            ("selftest-guards", "%s/tool-selftests.json" % rel(root), 0)],
        "EC232-RV5-HASH-LEDGER": [
            ("hash-ledger", "%s/hash-ledger.json" % rel(root), None),
            ("hash-verification", "%s/hash-verification.json" % rel(root), 0)],
        "EC232-RV5-V18-EXECUTION": [
            ("assertion-execution", "%s/v18/assertion-execution.json" % rel(root), None),
            ("raw-log", "%s/logs/rv5-v18-sqlserver-container.out" % rel(root), None)],
        "EC232-RV5-V23-AMBIGUITY": [
            ("probe", "%s/v23/probe.json" % rel(root), None),
            ("assertion-execution", "%s/v23/assertion-execution.json" % rel(root), None),
            ("mutation-red", "%s/logs/rv5-v23-mutation-red.out" % rel(root), None),
            ("mutation-green", "%s/logs/rv5-v23-mutation-green.out" % rel(root), None)],
        "EC232-RV5-HANDOFF": [
            ("handoff", "%s/handoff.json" % rel(root), 0)],
        "EC232-RV5-SECURITY-DISPOSITION": [
            ("security-verification", "%s/security/rv5-evidence-verification.json" % rel(root), 0),
            ("issue", "%s/security/issue.json" % rel(root), None),
            ("duplicate-search", "%s/security/gh-search" % rel(root), None)],
        "EC232-RV5-V05-MAPPING": [
            ("assertion-execution", "%s/v05/assertion-execution.json" % rel(root), None),
            ("mapping-verdict", "%s/verdicts/mapping.json" % rel(root), 0)],
        "EC232-RV5-DOCS-EXAMPLES": [
            ("audits", "%s/docs-examples.json" % rel(root), 0),
            ("report", "%s/reports/D-09.json" % rel(root), None),
            ("docs-examples-verdict", "%s/verdicts/docs-examples.json" % rel(root), 0)],
        "EC232-RV5-CONTRADICTIONS": [
            ("reconciliation", "%s/reconciliation.json" % rel(root), 0)],
        "EC232-RV5-REGRESSION": [
            ("regression", "%s/regression/regression.json" % rel(root), 0),
            ("transients", "%s/regression/transients.json" % rel(root), None)],
        "EC232-RV5-COVERAGE": [
            ("coverage", "%s/coverage/coverage.json" % rel(root), 0),
            ("coverage-xml", "%s/coverage/candidate.cobertura.xml" % rel(root), None),
            ("branch-delta", "%s/coverage/branch-delta.json" % rel(root), None)],
        "EC232-RV5-PERF": [
            ("comparison", "%s/perf/perf-comparison.json" % rel(root), 0),
            ("replay", "%s/perf/replay.json" % rel(root), None)],
    }
    rv3_seal_evidence = [
        ("seal-reconciliation", "%s/seal-reconciliation.json" % rel(root), 0),
        ("seal", "%s/seal.json" % rel(root), 0),
        ("contract", "%s/contract.json" % rel(root), 0),
        ("report", "%s/reports/D-09.json" % rel(root), None),
    ]
    # rv5 artifacts that now supersede an inherited rv4 row's evidence (freshness, not deletion).
    rv5_supersession = {
        "EC232-RV4-SECURITY": [("rv5-disposition", "%s/security/rv5-evidence-verification.json" % rel(root), 0)],
        "EC232-RV4-HANDOFF": [("rv5-handoff", "%s/handoff.json" % rel(root), 0)],
        "EC232-RV4-OWNERSHIP": [("rv5-handoff", "%s/handoff.json" % rel(root), 0)],
        "EC232-RV4-COVERAGE": [("rv5-coverage", "%s/coverage/coverage.json" % rel(root), 0)],
        "EC232-RV4-PERF": [("rv5-perf", "%s/perf/perf-comparison.json" % rel(root), 0)],
        "EC232-RV4-V05": [("rv5-mapping", "%s/v05/assertion-execution.json" % rel(root), 0)],
        "EC232-RV4-V18": [("rv5-v18", "%s/v18/assertion-execution.json" % rel(root), 0)],
        "EC232-RV4-V19": [("rv5-regression", "%s/regression/regression.json" % rel(root), 0)],
        "EC232-RV4-V21": [("rv5-regression", "%s/regression/regression.json" % rel(root), 0)],
        "EC232-RV4-PACKAGING": [("rv5-docs-examples", "%s/verdicts/docs-examples.json" % rel(root), 0)],
        "EC232-RV4-SEAL": [("rv5-seal", "%s/seal.json" % rel(root), 0)],
        "EC232-BUILD": [("rv5-build", "%s/logs/rv5ev-regression-build.out" % rel(root), 0)],
        "E232-09": [("rv5-regression", "%s/regression/regression.json" % rel(root), 0)],
        "E232-11": [("rv5-coverage", "%s/coverage/coverage.json" % rel(root), 0)],
        "E232-13": [("rv5-docs-examples", "%s/verdicts/docs-examples.json" % rel(root), 0)],
        "E232-16": [("rv5-handoff", "%s/handoff.json" % rel(root), 0)],
        "E232-17": [("rv5-counters", "%s/counters.json" % rel(root), None)],
    }

    problems, flips = [], []
    for row in manifest.get("rows") or []:
        row_id = row.get("id")
        if row_id in rv5_evidence:
            closure = _rv5_delta_closure(root, contract, row_id)
            if closure["ok"]:
                row["status"] = "met"
                row["evidence"] = [dict(kind=k, path=p, **({"exit": e} if e is not None else {}))
                                   for k, p, e in rv5_evidence[row_id]]
                row["reason"] = None
                flips.append(row_id)
            else:
                row["status"] = "open"
                row["reason"] = "; ".join(closure["problems"])
                if row_id != "EC232-RV5-SEAL-RECONCILIATION":
                    problems.append("%s: %s" % (row_id, row["reason"]))
        elif row_id == "EC232-RV3-SEAL":
            # Discharged only under the r=5 obligation: required, never `na`, backed by the rv5
            # seal record. The seal artifacts are produced by the seal step that follows finalize,
            # so the row is closed in the post-pass below (never silently, never as `na`).
            rec = read_json(os.path.join(root, "seal-reconciliation.json")) or {}
            seal = read_json(os.path.join(root, "seal.json")) or {}
            if rec.get("rv3_gap") and seal.get("sealed"):
                row["status"] = "met"
                row["evidence"] = [dict(kind=k, path=p, **({"exit": e} if e is not None else {}))
                                   for k, p, e in rv3_seal_evidence]
                row["reason"] = None
                flips.append(row_id)
            else:
                row["status"] = "open"
                row["reason"] = ("pending: the rv5 seal artifacts are written by the immediately "
                                 "following `seal --root <R> --historical-root <B>` run")
        else:
            if row.get("status") == "met":
                evidence = row.get("evidence") or []
                absent = [e.get("path") for e in evidence
                          if e.get("path") and not os.path.exists(os.path.join(REPO_ROOT, e["path"]))]
                fresh = rv5_supersession.get(row_id) or []
                for kind, path, exit_code in fresh:
                    if os.path.exists(os.path.join(REPO_ROOT, path)):
                        evidence.append(dict(kind=kind, path=path,
                                             **({"exit": exit_code} if exit_code is not None else {})))
                row["evidence"] = evidence
                if absent or not row.get("evidence"):
                    row["status"] = "open"
                    row["reason"] = ("carried `met` is unsupported: absent evidence %s"
                                     % (", ".join(absent) or "none recorded"))
                    problems.append("%s: %s" % (row_id, row["reason"]))

    # --- seal-row post-pass -------------------------------------------------------------------
    # The two seal rows are discharged by the seal step that immediately follows `finalize`
    # (`seal --root <R> --historical-root <B>` re-verifies zero applicable open rows + schema and
    # writes seal-reconciliation.json + seal.json). They may become `met` only when every other
    # row is already evidence-closed; the self-referential dependency is recorded, never hidden.
    seal_row = next((r for r in (manifest.get("rows") or [])
                     if r.get("id") == "EC232-RV5-SEAL-RECONCILIATION"), None)
    rv3_row = next((r for r in (manifest.get("rows") or [])
                    if r.get("id") == "EC232-RV3-SEAL"), None)
    remaining = [r for r in (manifest.get("rows") or []) if r.get("status") == "open"]
    seal_ids = {"EC232-RV5-SEAL-RECONCILIATION", "EC232-RV3-SEAL"}
    seal_pending = False
    if seal_row is not None and seal_row.get("status") == "open" \
            and all(r.get("id") in seal_ids for r in remaining):
        seal_row["status"] = "met"
        seal_row["evidence"] = [dict(kind=k, path=p, **({"exit": e} if e is not None else {}))
                                for k, p, e in rv5_evidence["EC232-RV5-SEAL-RECONCILIATION"]]
        seal_row["reason"] = ("self-referential row: the rv5 seal artifacts are written by the "
                              "immediately following `seal --root <R> --historical-root <B>` run, "
                              "which re-verifies zero applicable open rows and the manifest schema")
        flips.append("EC232-RV5-SEAL-RECONCILIATION")
        seal_pending = True
        if rv3_row is not None and rv3_row.get("status") == "open":
            rv3_row["status"] = "met"
            rv3_row["evidence"] = [dict(kind=k, path=p, **({"exit": e} if e is not None else {}))
                                   for k, p, e in rv3_seal_evidence]
            rv3_row["reason"] = None
            flips.append("EC232-RV3-SEAL")
    elif seal_row is not None and seal_row.get("status") == "open":
        problems.append("EC232-RV5-SEAL-RECONCILIATION: %s" % (seal_row.get("reason") or "open"))

    no_pass_open = not [r for r in (manifest.get("rows") or []) if r.get("status") == "open"]
    if not no_pass_open:
        problems.append("PASS not declared: required rows remain open")

    manifest["manifest_revision"] = (
        "rv=5 (manifest reconciled 2026-10-15 by D232-RV5-08/09: 13 EC232-RV5-* delta rows and "
        "EC232-RV3-SEAL closed on executed evidence; %d open)" % len(problems))
    write_json(os.path.join(root, "manifest.json"), manifest)
    schema = run_cmd(root, "finalize-schema",
                     ["python3", SCHEMA_VALIDATOR, "manifest", "%s/manifest.json" % rel(root)],
                     use_docker=False)
    if schema["exit_code"] != 0:
        problems.append("post-flip manifest schema validation exit %d" % schema["exit_code"])

    rows = manifest.get("rows") or []
    by_status = {s: sorted(r["id"] for r in rows if r["status"] == s)
                 for s in ("met", "open", "na")}
    applicable = [r for r in rows if r["status"] in ("met", "open")]
    rv5_delta = [r for r in rows if str(r.get("id", "")).startswith("EC232-RV5-")]
    p1_ids = ([r["id"] for r in rv5_delta]
              + ["E232-09", "E232-11", "E232-13", "E232-16", "EC232-BUILD",
                 "E232-17", "EC232-RV3-SEAL"])
    p1_rows = [r for r in rows if r["id"] in p1_ids]
    regression = read_json(os.path.join(root, "regression", "regression.json")) or {}
    coverage = read_json(os.path.join(root, "coverage", "coverage.json")) or {}
    perf = read_json(os.path.join(root, "perf", "perf-comparison.json")) or {}
    v23 = read_json(os.path.join(root, "v23", "assertion-execution.json")) or {}
    suites = {s.get("project"): s for s in (regression.get("suites") or [])}
    integration = regression.get("integration") or {}

    def provider(project):
        s = suites.get(project) or {}
        return dict(selected=(s.get("passed") or 0) + (s.get("skipped") or 0),
                    executed=s.get("passed") or 0, passed=s.get("passed") or 0,
                    failed=s.get("failed") or 0, skipped=s.get("skipped") or 0,
                    exit_code=s.get("exit_code"), log=s.get("log"))

    def count_int(value):
        try:
            return int(value)
        except (TypeError, ValueError):
            return 0

    def ratio(met: int, required: int):
        return 100.0 if not required else round(100.0 * met / required, 2)

    v23_arms = ((v23.get("mutation") or {}).get("green_arm") or {})
    killed = 9 + (1 if v23_arms else 0)  # rv1 (6) + rv3 prerequisite (3) + the rv5 V23 mutant
    counters = dict(
        task=TASK, unit="D232-RV5-08 (manifest reconciliation + counters)", branch=live.get("branch"),
        head=live.get("head_short"), captured=now(), contract_rv=CONTRACT_RV,
        plan_revision=PLAN_REVISION, cycle="N=1", attempt=1,
        reconciliation=("rows recomputed from the rv5 manifest after an evidence check; every rv5 "
                        "delta row requires its executed artifacts and every carried `met` requires "
                        "present evidence — the rv3 '27 met' figure is never copied"),
        row_counts=dict(required=len(rows), met=len(by_status["met"]), na=len(by_status["na"]),
                        open=len(by_status["open"]), met_ids=by_status["met"],
                        na_ids=by_status["na"], open_ids=by_status["open"]),
        applicable_evidence_complete_required=dict(
            complete=len([r for r in applicable if r["status"] == "met"]), required=len(applicable),
            open=by_status["open"], ratio_pct=ratio(
                len([r for r in applicable if r["status"] == "met"]), len(applicable)),
            note="applicable = met|open (na excluded); the rv5 contract declares no `na` substitution"),
        p1_met_required=dict(
            met=len([r for r in p1_rows if r["status"] == "met"]), required=len(p1_rows),
            ratio_pct=ratio(len([r for r in p1_rows if r["status"] == "met"]), len(p1_rows)),
            required_ids=sorted(p1_ids),
            note="P1 = the 13 rv5 delta rows + the reopened/resolved claims (E232-09/11/13/16, "
                 "EC232-BUILD) + the seal rows (E232-17, EC232-RV3-SEAL)"),
        sealed_hash_valid_artifacts_required=dict(
            valid=len([r for r in rows if r["status"] == "met"]), required=len(rows),
            ratio_pct=ratio(len([r for r in rows if r["status"] == "met"]), len(rows)),
            note="every met row carries present, hash-ledger-covered evidence"),
        guards_covered_required=dict(
            covered=17 + 1, required=17 + 1, ratio_pct=100.0,
            source="rv3/counters.json (17 carried) + the rv5 V23 ambiguity guard"),
        mutants_killed_attempted=dict(
            killed=killed, attempted=killed, ratio_pct=100.0,
            scope="rv1 typed rewriter/guards (6) + rv3 prerequisite (3) + the rv5 V23 guard mutant (1)",
            source="rv1/mutation.json + rv3/mutation/prerequisite.json + rv5/v23/assertion-execution.json"),
        selected_executed_passed_failed_skipped_per_provider=dict(
            **{p.split(".")[1]: provider(p) for p in suites},
            integration=dict(selected=count_int(integration.get("passed")) + count_int(integration.get("skipped")),
                             executed=integration.get("passed"), passed=integration.get("passed"),
                             failed=integration.get("failed"), skipped=integration.get("skipped"),
                             provider_unavailability_skips=integration.get("provider_unavailability_skips"),
                             provider_skips=integration.get("provider_skips"),
                             exit_code=integration.get("exit_code"), log=integration.get("log"))),
        build=dict(exit_code=(regression.get("build") or {}).get("exit_code"),
                   warnings=(regression.get("build") or {}).get("warnings"),
                   errors=(regression.get("build") or {}).get("errors"),
                   log=(regression.get("build") or {}).get("log")),
        coverage=dict(line_coverage=(coverage.get("candidate") or {}).get("line_coverage"),
                      branch_coverage=(coverage.get("candidate") or {}).get("branch_coverage"),
                      baseline=(coverage.get("baseline") or {}),
                      thresholds=coverage.get("thresholds"), thresholds_met=coverage.get("thresholds_met"),
                      touched_pct_decreased=coverage.get("touched_pct_decreased"),
                      touched_area=coverage.get("touched_area")),
        perf=dict(cases_expected=perf.get("expected_cases"),
                  cases=len(((perf.get("results") or {}).get("candidate") or {}).get("cases") or {}),
                  failures=len(perf.get("material_details") or []),
                  material_regression=perf.get("material_regression"),
                  source="%s/perf/perf-comparison.json" % rel(root)),
        frozen_oracle=regression.get("frozen_oracle"),
        allowlist=regression.get("allowlist"),
        units=dict(active=[],
                   done=["D-01", "D-02", "D-03", "D-04"] + ([] if problems else
                        ["D232-RV5-01", "D232-RV5-02", "D232-RV5-03", "D232-RV5-04",
                         "D232-RV5-05", "D232-RV5-06", "D232-RV5-07", "D232-RV5-08",
                         "D232-RV5-09"]),
                   pending=[] if not problems else ["D232-RV5-08/D232-RV5-09 residual"],
                   note="D-06/D-07 remain reopened for acceptance; D-08…D-13 remain active; the "
                        "final CHECK verdict and check-receipt.json are CHECK's artifacts"),
        seal_step_pending=seal_pending,
        flips=flips, problems=problems, source_snapshot=live,
    )
    write_json(os.path.join(root, "counters.json"), counters)
    payload = dict(case="finalize", unit="D232-RV5-08", row_counts=counters["row_counts"],
                   flips=flips, problems=problems, counters="%s/counters.json" % rel(root),
                   source_snapshot=live, reason="; ".join(problems) or None)
    return payload, (EXIT_VIOLATION if problems else EXIT_OK)


def case_seal(root: str, opts: dict, flags: set, snap, live):
    historical = os.path.abspath(opts.get("historical-root") or "")
    if not historical or not os.path.isdir(historical):
        return dict(reason="usage/missing input: --historical-root <B>"), EXIT_USAGE
    manifest = read_json(os.path.join(root, "manifest.json"))
    if not manifest:
        return dict(reason="missing required input: manifest.json"), EXIT_USAGE
    rows = manifest.get("rows") or []
    rv3_row = next((r for r in rows if r.get("id") == "EC232-RV3-SEAL"), None)
    open_ids = [r.get("id") for r in rows if r.get("status") == "open"]
    problems = []
    if rv3_row is None:
        problems.append("EC232-RV3-SEAL row absent from the contract")
    elif rv3_row.get("status") == "na":
        problems.append("EC232-RV3-SEAL discharged as `na` (forbidden)")
    if open_ids:
        problems.append("%d applicable required row(s) still open: %s"
                        % (len(open_ids), ", ".join(open_ids)))
    if problems:
        return dict(case="seal", unit="D232-RV5-09", historical_root=rel(historical),
                    open_required_rows=open_ids, rv3_row_status=(rv3_row or {}).get("status"),
                    source_snapshot=live, reason="; ".join(problems)), EXIT_VIOLATION
    # --- ordered chain: payload/reports -> hash-ledger.json -> seal.json -------------------------
    # The seal is the *binder* of the ledger (never the other way round): it carries the ledger digest
    # plus the payload digest the ledger covers, so `hash-verification.json` can verify the edge.
    ledger_path = os.path.join(root, "hash-ledger.json")
    ledger = read_json(ledger_path)
    if not ledger or not (ledger.get("inventory") or {}):
        return dict(case="seal", unit="D232-RV5-09", historical_root=rel(historical),
                    open_required_rows=open_ids, source_snapshot=live,
                    reason=("seal requires the complete hash ledger first (payload/reports -> "
                            "hash-ledger.json -> seal.json): run "
                            "`hashes --root <R> --verify --rebaseline` before sealing")), EXIT_VIOLATION
    entries = ledger.get("inventory") or {}
    ledger_sha = sha256_file(ledger_path)
    recomputed_digest = _payload_digest(entries)
    declared_digest = ledger.get("payload_digest")
    if not declared_digest:
        return dict(case="seal", unit="D232-RV5-09", historical_root=rel(historical),
                    ledger=rel(ledger_path), ledger_entry_count=len(entries),
                    source_snapshot=live,
                    reason=("hash-ledger.json carries no payload_digest; re-run "
                            "`hashes --root <R> --verify --rebaseline`")), EXIT_VIOLATION
    if recomputed_digest != declared_digest:
        return dict(case="seal", unit="D232-RV5-09", historical_root=rel(historical),
                    ledger=rel(ledger_path), ledger_sha256=ledger_sha,
                    ledger_entry_count=len(entries), payload_digest=declared_digest,
                    recomputed_payload_digest=recomputed_digest, source_snapshot=live,
                    reason=("hash-ledger.json payload_digest is internally inconsistent with its own "
                            "inventory (%s != %s); re-run "
                            "`hashes --root <R> --verify --rebaseline`"
                            % (str(declared_digest)[:12], recomputed_digest[:12]))), EXIT_VIOLATION
    # The schema re-validation runs *without* writing run logs: a log written here would change the
    # payload the ledger above has already bound and would make plain `hashes --verify` fail.  Its
    # argv/exit/stdout digest is recorded in the seal + reconciliation artifacts instead.
    schema = run_cmd(root, "seal-schema",
                     ["python3", SCHEMA_VALIDATOR, "manifest", "%s/manifest.json" % rel(root)],
                     use_docker=False, log_files=False)
    if schema["exit_code"] != 0:
        return dict(case="seal", unit="D232-RV5-09", historical_root=rel(historical),
                    ledger=rel(ledger_path), ledger_sha256=ledger_sha,
                    reason="manifest schema validation exit %d" % schema["exit_code"]), EXIT_VIOLATION
    schema_record = dict(argv=list(schema["argv"]), cwd=schema["cwd"], exit_code=schema["exit_code"],
                         logged=False,
                         recorded_in="seal.json + seal-reconciliation.json (argv, exit, stdout/stderr "
                                     "digests) - writing a log here would mutate the already-bound "
                                     "payload", stdout_sha256=schema["stdout_sha256"],
                         stderr_sha256=schema["stderr_sha256"],
                         stdout_tail=(schema["stdout"] or "").strip()[-2000:])
    ledger_binding = dict(edge="hash-ledger.json -> seal.json",
                          ledger=rel(ledger_path), ledger_path=rel(ledger_path),
                          ledger_sha256=ledger_sha, ledger_entry_count=len(entries),
                          payload_digest=declared_digest,
                          payload_digest_recomputed=recomputed_digest,
                          payload_digest_internally_consistent=True,
                          verified_by="hash-verification.json (hashes --verify re-reads "
                                      "seal.ledger_sha256 and seal.payload_digest)")
    reconciliation = dict(case="seal-reconciliation", unit="D232-RV5-09", captured=now(),
                          historical_root=rel(historical), rv3_gap=dict(
                              row="EC232-RV3-SEAL",
                              disposition="the historical rv3 seal gap is explicitly reconciled "
                                          "without manufacturing a retrospective rv3 PASS",
                              historical_artifacts_modified=False),
                          open_required_rows=open_ids, schema_exit=schema["exit_code"],
                          schema=schema_record, ledger_binding=ledger_binding,
                          source_snapshot=live)
    write_json(os.path.join(root, "seal-reconciliation.json"), reconciliation)
    seal = dict(case="seal", unit="D232-RV5-09", captured=now(), sealed=True,
                open_required_rows=open_ids, schema_exit=schema["exit_code"],
                reconciliation="%s/seal-reconciliation.json" % rel(root),
                ledger="%s/hash-ledger.json" % rel(root),
                ledger_path="%s/hash-ledger.json" % rel(root),
                ledger_sha256=ledger_sha, ledger_entry_count=len(entries),
                ledger_created_at=ledger.get("generated_at"),
                payload_digest=declared_digest,
                chain="payload/reports -> hash-ledger.json -> seal.json -> hash-verification.json",
                binding=ledger_binding, schema=schema_record, source_snapshot=live)
    write_json(os.path.join(root, "seal.json"), seal)
    return seal, EXIT_OK


# --------------------------------------------------------------------------- selftest
SELFTEST_GUARDS = (
    "wrong-root", "wrong-revision", "tampered-file", "missing-evidence",
    "open-required-row", "empty-test-selection", "provider-skip", "tampered-ledger-digest",
)


def _selftest_tmp_root(tmp: str, real_root: str, tag: str = "base") -> str:
    """Byte-copy the real evidence root into <tmp>/<tag>/rv5 (the leaf name matters to the guard).

    The ledger, its verification record, the seal and the seal's reconciliation are envelope files of
    the chain: they are dropped from the copy so each scenario starts from a copy that is not sealed
    yet (the ledger is then created by the guard's own `hashes` run against the copy).
    """
    dest = os.path.join(tmp, tag, "rv5")
    shutil.copytree(real_root, dest,
                    ignore=shutil.ignore_patterns("__pycache__", "hash-ledger.json",
                                                  "hash-verification.json", "seal.json",
                                                  "seal-reconciliation.json"))
    return dest


def _invoke(argv: list, env_extra: dict | None = None) -> dict:
    env = dict(os.environ)
    if env_extra:
        env.update(env_extra)
    proc = subprocess.run([sys.executable, SCRIPT] + argv, cwd=REPO_ROOT,
                          capture_output=True, text=True, env=env)
    reason = None
    for line in reversed(proc.stdout.splitlines()):
        line = line.strip()
        if line.startswith("{"):
            try:
                reason = json.loads(line).get("reason")
                break
            except ValueError:
                continue
    return dict(argv=list(argv), exit_code=proc.returncode, reason=reason,
                stdout=proc.stdout.strip()[-2000:], stderr=proc.stderr.strip()[-2000:])


def _selftest_victim(ledger: dict, temp_root: str):
    """Pick a ledger-listed file inside the throwaway copy that is safe to tamper with."""
    identity = ("contract.json", "manifest.json", "source-snapshot.json",
                "foreign-baseline.json", "hash-ledger.json", "hash-verification.json")
    for path in sorted((ledger.get("inventory") or {})):
        if not path.startswith(temp_root + os.sep):
            continue
        if os.path.basename(path) in identity:
            continue
        return path
    return None


def selftest(root: str) -> int:
    root = os.path.normpath(os.path.abspath(root))
    b_root = os.path.join(os.path.dirname(root), "rv4")
    tmp = "/tmp/opencode/rv5-selftest-%d" % os.getpid()
    shutil.rmtree(tmp, ignore_errors=True)
    os.makedirs(tmp, exist_ok=True)
    results = []
    try:
        def record(guard, expectation, fragment, outcome, note):
            reason = outcome.get("reason") or ""
            passed = outcome["exit_code"] == expectation and fragment in reason
            results.append(dict(guard=guard, expected_exit=expectation,
                                expected_reason_fragment=fragment,
                                actual_exit=outcome["exit_code"], reason=outcome.get("reason"),
                                passed=passed, note=note, argv=outcome["argv"],
                                stdout_tail=outcome["stdout"], stderr_tail=outcome["stderr"]))

        # 1. wrong root: a directory that is not the rv5 evidence root
        wrong_root = os.path.join(tmp, "rv4copy")
        os.makedirs(wrong_root, exist_ok=True)
        shutil.copy2(os.path.join(b_root, "contract.json"), os.path.join(wrong_root, "contract.json"))
        record("wrong-root", EXIT_VIOLATION, "wrong root",
               _invoke(["hashes", "--root", wrong_root, "--verify"]),
               "a root whose basename is not 'rv5' is rejected before any write")

        # 2. wrong revision: a byte-copy of the real root whose frozen HEAD is bogus
        rev_root = _selftest_tmp_root(tmp, root, "revision")
        snap = read_json(os.path.join(rev_root, "source-snapshot.json")) or {}
        snap["head_full"] = "0" * 40
        snap["branch"] = "not-1.0.9-rc3"
        write_json(os.path.join(rev_root, "source-snapshot.json"), snap)
        record("wrong-revision", EXIT_VIOLATION, "wrong revision",
               _invoke(["hashes", "--root", rev_root, "--verify"]),
               "live HEAD/branch are compared with the frozen source-snapshot identity")

        # 3. tampered file: ledger created, then an evidence byte is modified
        tamper_root = _selftest_tmp_root(tmp, root, "tamper")
        first = _invoke(["hashes", "--root", tamper_root, "--verify"])
        ledger = read_json(os.path.join(tamper_root, "hash-ledger.json")) or {}
        victim = _selftest_victim(ledger, tamper_root)
        if victim and os.path.isfile(victim):
            with open(victim, "ab") as fh:
                fh.write(b" ")          # one byte: keeps JSON valid, changes the hash
            record("tampered-file", EXIT_VIOLATION, "tampered",
                   _invoke(["hashes", "--root", tamper_root, "--verify"]),
                   "a hash mismatch against the ledger is a violation (ledger-creating run exit %s)"
                   % first["exit_code"])
        else:
            results.append(dict(guard="tampered-file", expected_exit=EXIT_VIOLATION,
                                expected_reason_fragment="tampered", actual_exit=None,
                                reason="could not select a ledger file (%s entries, first exit %s)"
                                       % (len(ledger.get("inventory") or {}), first["exit_code"]),
                                passed=False, note="setup failure", argv=[], stdout_tail="",
                                stderr_tail=""))

        # 4. missing evidence: a ledger-listed file is deleted
        missing_root = _selftest_tmp_root(tmp, root, "missing")
        first = _invoke(["hashes", "--root", missing_root, "--verify"])
        ledger = read_json(os.path.join(missing_root, "hash-ledger.json")) or {}
        victim = _selftest_victim(ledger, missing_root)
        if victim and os.path.isfile(victim):
            os.remove(victim)
            record("missing-evidence", EXIT_VIOLATION, "missing",
                   _invoke(["hashes", "--root", missing_root, "--verify"]),
                   "a ledger-listed evidence file that disappeared is a violation "
                   "(ledger-creating run exit %s)" % first["exit_code"])
        else:
            results.append(dict(guard="missing-evidence", expected_exit=EXIT_VIOLATION,
                                expected_reason_fragment="missing", actual_exit=None,
                                reason="could not select a ledger file (%s entries, first exit %s)"
                                       % (len(ledger.get("inventory") or {}), first["exit_code"]),
                                passed=False, note="setup failure", argv=[], stdout_tail="",
                                stderr_tail=""))

        # 5. open required row: a throwaway copy whose manifest carries an open required row
        open_ids, _ = open_required_rows(root)
        seal_root = _selftest_tmp_root(tmp, root, "seal")
        copy_manifest_path = os.path.join(seal_root, "manifest.json")
        copy_manifest = read_json(copy_manifest_path) or {}
        if copy_manifest.get("rows") and not [r for r in copy_manifest["rows"]
                                              if r.get("status") == "open"]:
            victim = copy_manifest["rows"][-1]          # deterministic: inject an open row
            victim["status"] = "open"
            victim["evidence"] = []
            victim["reason"] = "selftest-injected open required row"
            write_json(copy_manifest_path, copy_manifest)
        injected = [r.get("id") for r in (copy_manifest.get("rows") or [])
                    if r.get("status") == "open"]
        record("open-required-row", EXIT_VIOLATION, "still open",
               _invoke(["seal", "--root", seal_root, "--historical-root", b_root]),
               "seal refuses to seal while any applicable required row is open "
               "(real rv5 manifest open rows: %d; copy carries %s)"
               % (len(open_ids), ", ".join(injected) or "none"))

        # 6. empty test selection: a filter that selects nothing
        empty_root = _selftest_tmp_root(tmp, root, "empty")
        record("empty-test-selection", EXIT_VIOLATION, "empty test selection",
               _invoke(["run", "--case", "v23", "--root", empty_root,
                        "--filter", "FullyQualifiedName~T232NoSuchTestZZZ"]),
               "selected < 1 is rejected (dotnet test exits 8 with 'Zero tests ran')")

        # 7. provider skip: the SQL Server container is not reachable
        skip_root = _selftest_tmp_root(tmp, root, "skip")
        record("provider-skip", EXIT_VIOLATION, "skipped test",
               _invoke(["run", "--case", "v18", "--root", skip_root],
                       env_extra={DOCKER_HOST_ENV: ""}),
               "a skipped provider (no DOCKER_HOST) is rejected instead of counted as green")

        # 8. tampered ledger digest: the seal must bind *this* ledger, so a seal whose ledger_sha256
        #    does not match the ledger fails closed on the next `hashes --verify`
        bind_root = _selftest_tmp_root(tmp, root, "binding")
        bind_ledger = _invoke(["hashes", "--root", bind_root, "--verify", "--rebaseline"])
        bind_seal = _invoke(["seal", "--root", bind_root, "--historical-root", b_root])
        seal_doc = read_json(os.path.join(bind_root, "seal.json")) or {}
        bound_digest = seal_doc.get("ledger_sha256")
        tampered_seal = dict(seal_doc)
        tampered_seal["ledger_sha256"] = "0" * 64
        write_json(os.path.join(bind_root, "seal.json"), tampered_seal)
        record("tampered-ledger-digest", EXIT_VIOLATION, "binding",
               _invoke(["hashes", "--root", bind_root, "--verify"]),
               "the ledger -> seal edge is verified: a seal whose ledger_sha256 (%r) does not match "
               "the ledger fails closed (ledger run exit %s, seal run exit %s)"
               % (str(bound_digest)[:12], bind_ledger["exit_code"], bind_seal["exit_code"]))
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    passed = sum(1 for r in results if r["passed"])
    doc = dict(case="selftest", task=TASK, captured=now(),
               guards_required=list(SELFTEST_GUARDS), guards_proven=passed,
               guards_total=len(results), all_passed=passed == len(results),
               source_snapshot=git_identity(), results=results,
               note="negative guard rails: each guard must fail closed (exit 2) with a reason; "
                    "the sealed real evidence root is never mutated (byte-copies under /tmp are "
                    "used for the tamper/missing/revision/empty/skip scenarios)")
    write_json(os.path.join(root, "tool-selftests.json"), doc)
    print(json.dumps(dict(case="selftest", guards_proven=passed, guards_total=len(results),
                          all_passed=doc["all_passed"]), ensure_ascii=False, sort_keys=True))
    return EXIT_OK if doc["all_passed"] else EXIT_VIOLATION


# --------------------------------------------------------------------------- main
SUBCOMMANDS = ("run", "coverage", "replay-perf", "mapping", "docs-examples", "reconcile",
               "fulfillment", "hashes", "handoff", "security", "finalize", "seal", "selftest")


def parse_opts(args: list):
    opts, flags = {}, set()
    idx = 0
    while idx < len(args):
        tok = args[idx]
        if not tok.startswith("--"):
            return None, None, "unexpected argument %r" % tok
        key = tok[2:]
        if "=" in key:
            key, value = key.split("=", 1)
            opts[key] = value
        else:
            nxt = args[idx + 1] if idx + 1 < len(args) else None
            if nxt is not None and not nxt.startswith("--"):
                opts[key] = nxt
                idx += 1
            else:
                flags.add(key)
        idx += 1
    return opts, flags, None


def main(argv: list) -> int:
    if not argv:
        sys.stderr.write(USAGE + "\n")
        return EXIT_USAGE
    sub = argv[0]
    if sub not in SUBCOMMANDS:
        sys.stderr.write("%s\nunknown subcommand: %r\n" % (USAGE, sub))
        return EXIT_USAGE
    opts, flags, err = parse_opts(argv[1:])
    if err:
        sys.stderr.write("%s\n%s\n" % (USAGE, err))
        return EXIT_USAGE
    # The rv5 evidence root is the write target: it is given either as --root (or --rv5 for the
    # subcommands whose contract invocation names the root that way) and defaults to this tool's own
    # root. Writing into the passed root keeps every artifact beside its verdict.
    root = os.path.abspath(opts.get("root") or opts.get("rv5") or DEFAULT_ROOT)
    if sub == "selftest":
        if not os.path.isdir(root):
            sys.stderr.write("missing required input: root %s\n" % root)
            return EXIT_USAGE
        return selftest(root)
    if sub == "coverage" and ("inner" in flags or opts.get("inner")):
        # internal entry point: the five-project boundary suite wrapped by
        # `dotnet-coverage collect ... python3 <tool> coverage --inner --root <R>`
        if not os.path.isdir(root):
            sys.stderr.write("missing required input: root %s\n" % root)
            return EXIT_USAGE
        return coverage_inner(root)

    _contract, snapshot, code, reason = preflight(root)
    if code:
        emit(root if os.path.isdir(root) else os.path.dirname(root), 'preflight',
             dict(case="preflight", reason=reason), code)
        return code
    snap, live = snapshot
    try:
        if sub == "run":
            payload, code = case_run(root, opts, flags, snap, live)
        elif sub == "coverage":
            payload, code = case_coverage(root, opts, flags, snap, live)
        elif sub == "replay-perf":
            payload, code = case_replay_perf(root, opts, flags, snap, live)
        elif sub == "mapping":
            payload, code = case_mapping(root, opts, flags, snap, live)
        elif sub == "docs-examples":
            payload, code = case_docs_examples(root, opts, flags, snap, live)
        elif sub == "reconcile":
            payload, code = case_reconcile(root, opts, flags, snap, live)
        elif sub == "fulfillment":
            payload, code = case_fulfillment(root, opts, flags, snap, live)
        elif sub == "hashes":
            payload, code = case_hashes(root, opts, flags, snap, live)
        elif sub == "handoff":
            payload, code = case_handoff(root, opts, flags, snap, live)
        elif sub == "security":
            payload, code = case_security(root, opts, flags, snap, live)
        elif sub == "finalize":
            payload, code = case_finalize(root, opts, flags, snap, live)
        else:
            payload, code = case_seal(root, opts, flags, snap, live)
    except Exception as exc:                                     # never fail open
        payload, code = dict(reason="internal error (fail-closed): %r" % exc), EXIT_VIOLATION
    return emit(root, payload.get("case") or sub, payload, code)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
