#!/usr/bin/env python3
"""verify.py -- task-local evidence plumbing for D232-sqlserver-pivot-unpivot (T232 / #232), rv=4.

Task-local plumbing, NOT production code and NOT a framework. rv=4 supersedes rv3 as the current
contract after the authoritative CHECK at r=3 / n=2 / rv=3 FAILED (build / security / unsealed
evidence). It expands and records the exact argv + cwd of every executed command, captures
stdout/stderr and the numeric exit code under ``logs/``, and writes ``<case>.json``.

Contract roots
--------------
``R``  = ``artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4`` (this file's directory).
``R1`` = ``artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1`` (E232-01..17 keep their rv1 paths).
``R3`` = ``artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3`` (E232/EC232 rv3 evidence is preserved).

Usage
-----
    python3 "$R/verify.py" --case <case>

Cases (the 11 rv4 delta rows + the D-08 packaging case)
    packaging  EC232-RV4-PACKAGING  D-08: csproj ProjectReference + docs + literal C13 exit 0
    v05        EC232-RV4-V05        D-09: TVF inner closure
    v18        EC232-RV4-V18        D-09: typo/absent data value closure
    v19        EC232-RV4-V19        D-09: dynamic PIVOT guard
    v21        EC232-RV4-V21        D-09: eager/single-query state closure
    coverage   EC232-RV4-COVERAGE   D-10: thresholds AND comparable touched-scope non-decrease
    ownership  EC232-RV4-OWNERSHIP  D-11: #160 ownership/authorization
    handoff    EC232-RV4-HANDOFF    D-11: per-hunk byte hashes / clean paths / CRLF
    security   EC232-RV4-SECURITY   D-13: pre-fix advisory capture + duplicate search + remedy
                                    ownership + an ordinary unmasked restore/build that is green
    perf       EC232-RV4-PERF       perf re-confirmation
    seal       EC232-RV4-SEAL       D-12: manifest schema + zero applicable open rows

Exit codes: 0 = ok, 2 = violation, 1 = usage/unreadable/missing required input (fail-closed).
"""
from __future__ import annotations

import datetime as _dt
import hashlib
import json
import os
import re
import subprocess
import sys

R = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(R, "..", "..", "..", ".."))
R_REL = os.path.relpath(R, ROOT).replace(os.sep, "/")
LOGS = os.path.join(R, "logs")

# A generic case verdict is written to <R>/<case>.json. For `ownership` that name collides with the
# live D-11 record <R>/ownership.json authored by ownership/run.py (the authoritative
# retention-authorization/AC-12 document), so the verdict goes to <R>/ownership-case.json instead
# and never clobbers that record.
# `handoff` has the same collision: its case command is `ownership/run.py handoff`, which writes the
# live detailed D-11 record <R>/handoff.json (status/hunk_byte_hashes/crlf/foreign_bytes_changed).
# The generic verdict therefore goes to <R>/handoff-case.json and never targets that live record.
RESULT_NAME = {"ownership": "ownership-case.json", "handoff": "handoff-case.json"}

DOCKER_HOST = "unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock"
SQLSERVER_TESTS = "tests/nextorm.sqlserver.tests"
INTEGRATION_TESTS = "tests/nextorm.integration.tests"
EXAMPLE_CSPROJ = "examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj"

# D-13 security case command routes (ordinary, unmasked -- no NuGetAudit=false, no
# RestoreAdditionalProjectSources). SECURITY_MASK_TOKENS below is the fail-closed gate.
SECURITY_RESTORE_ARGV = ["dotnet", "restore", "nextorm.slnx"]
SECURITY_BUILD_ARGV = ["dotnet", "build", "nextorm.slnx", "-c", "Debug"]

# case -> (row id, unit, required input files, argv, acceptance description)
CASES = {
    "packaging": ("EC232-RV4-PACKAGING", "D-08", [EXAMPLE_CSPROJ], None,
                  "csproj ProjectReferences present, docs note the exception, literal C13 exits 0"),
    "v05": ("EC232-RV4-V05", "D-09", [f"{SQLSERVER_TESTS}/T232VariantClosureTests.cs"],
            ["dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--no-build",
             "--filter", "FullyQualifiedName~T232VariantV05"], "V05 TVF inner closure, selected > 0, exit 0"),
    "v18": ("EC232-RV4-V18", "D-09", [f"{SQLSERVER_TESTS}/T232VariantClosureTests.cs"],
            ["dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--no-build",
             "--filter", "FullyQualifiedName~T232VariantV18"], "V18 typo/absent value, selected > 0, exit 0"),
    "v19": ("EC232-RV4-V19", "D-09", [f"{SQLSERVER_TESTS}/T232VariantClosureTests.cs"],
            ["dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--no-build",
             "--filter", "FullyQualifiedName~T232VariantV19"], "V19 dynamic PIVOT guard, selected > 0, exit 0"),
    "v21": ("EC232-RV4-V21", "D-09", [f"{SQLSERVER_TESTS}/T232VariantClosureTests.cs"],
            ["dotnet", "test", SQLSERVER_TESTS, "-c", "Debug", "--no-build",
             "--filter", "FullyQualifiedName~T232VariantV21"], "V21 eager/single-query state, selected > 0, exit 0"),
    "coverage": ("EC232-RV4-COVERAGE", "D-10", [f"{R_REL}/coverage/harness.json"],
                 ["python3", f"{R_REL}/coverage/run.py"], "thresholds met AND comparable touched-scope non-decrease"),
    "ownership": ("EC232-RV4-OWNERSHIP", "D-11", [f"{R_REL}/ownership.json"],
                  ["python3", f"{R_REL}/ownership/run.py", "ownership"],
                  "verified #160 sibling-owner identity + explicit authorization; T232-only footprint"),
    "handoff": ("EC232-RV4-HANDOFF", "D-11", [f"{R_REL}/ownership.json"],
                ["python3", f"{R_REL}/ownership/run.py", "handoff"],
                "per-hunk byte hashes, clean paths, CRLF"),
    "security": ("EC232-RV4-SECURITY", "D-13", [], SECURITY_RESTORE_ARGV,
                 "complete pre-fix advisory capture (identifiers / package path / exit codes) + "
                 "duplicate search + remedy ownership #236 + ordinary unmasked restore (0 NU19) "
                 "and build (0 warnings / 0 errors) green"),
    "perf": ("EC232-RV4-PERF", "D-10", [f"{R_REL}/perf/baseline.json"],
             ["python3", f"{R_REL}/perf/run.py"], "seven acceptance cases, no unexplained material regression"),
    "seal": ("EC232-RV4-SEAL", "D-12", [f"{R_REL}/manifest.json"], None,
             "manifest schema valid AND zero applicable open rows"),
}


def _now() -> str:
    return _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _sha(path: str) -> str:
    return hashlib.sha256(open(path, "rb").read()).hexdigest()


def _run(case: str, argv: list[str]) -> dict:
    os.makedirs(LOGS, exist_ok=True)
    out_p, err_p, exit_p = (os.path.join(LOGS, f"{case}.{s}") for s in ("out", "err", "exit"))
    cmd_p = os.path.join(LOGS, f"{case}.cmd.json")
    cmd = dict(argv=argv, cwd=ROOT, env={"DOCKER_HOST": DOCKER_HOST}, captured=_now())
    json.dump(cmd, open(cmd_p, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    open(cmd_p, "a", encoding="utf-8").write("\n")
    env = dict(os.environ)
    if case in ("v05", "v18", "v19", "v21"):
        env["DOCKER_HOST"] = DOCKER_HOST
    proc = subprocess.run(argv, cwd=ROOT, capture_output=True, text=True, env=env)
    open(out_p, "w", encoding="utf-8").write(proc.stdout)
    open(err_p, "w", encoding="utf-8").write(proc.stderr)
    open(exit_p, "w", encoding="utf-8").write(str(proc.returncode))
    return dict(exit=proc.returncode, stdout=proc.stdout, stderr=proc.stderr,
                out=os.path.relpath(out_p, ROOT).replace(os.sep, "/"),
                err=os.path.relpath(err_p, ROOT).replace(os.sep, "/"))


def _write(case: str, payload: dict, artifact: str | None = None) -> None:
    payload.setdefault("case", case)
    payload.setdefault("captured", _now())
    payload.setdefault("cwd", ROOT)
    text = json.dumps(payload, indent=1, ensure_ascii=False, sort_keys=True) + "\n"
    open(os.path.join(R, RESULT_NAME.get(case, f"{case}.json")), "w", encoding="utf-8").write(text)
    if artifact:
        os.makedirs(os.path.dirname(os.path.join(R, artifact)), exist_ok=True)
        open(os.path.join(R, artifact), "w", encoding="utf-8").write(text)


def case_packaging() -> tuple[dict, int]:
    csproj = open(os.path.join(ROOT, EXAMPLE_CSPROJ), encoding="utf-8").read()
    has_core = "../../src/nextorm.core/nextorm.core.csproj" in csproj
    has_sql = "../../src/nextorm.sqlserver/nextorm.sqlserver.csproj" in csproj
    no_pkg = ('Include="nextorm" ' not in csproj and 'Include="nextorm.sqlserver" ' not in csproj)
    docs = ""
    for p in ("examples/README.md", "examples/VERIFICATION.md",
              "examples/nextorm.examples.mssql.adventureworks/README.md"):
        docs += open(os.path.join(ROOT, p), encoding="utf-8").read()
    docs_note = "explicit exception" in docs and "current checkout" in docs
    res = _run("packaging", ["dotnet", "build", EXAMPLE_CSPROJ, "-c", "Debug"])
    cs_errors = [ln for ln in (res["stdout"] + res["stderr"]).splitlines()
                 if " error " in ln and "NU19" not in ln]
    advisory_only = bool(re.search(r" error NU19\d\d", res["stdout"] + res["stderr"]))
    ok = has_core and has_sql and no_pkg and docs_note and res["exit"] == 0
    payload = dict(
        row=CASES["packaging"][0], unit="D-08", status="met" if ok else "open",
        assertions=dict(project_reference_core=has_core, project_reference_sqlserver=has_sql,
                        no_nextorm_packageref=no_pkg, docs_state_exception=docs_note),
        c13=dict(exit=res["exit"], log=res["out"], cs_or_packaging_errors=cs_errors,
                 advisory_only=advisory_only),
        reason=(None if ok else
                "literal C13 exits 1 (advisory-only NU1902/NU1903 -> D-13); packaging route itself adds no "
                "CS/packaging error"),
        artifact=f"{R_REL}/packaging/packaging.json",
    )
    return payload, (0 if ok else 2)


# --------------------------------------------------------------------------- security (D-13)
# Contract row EC232-RV4-SECURITY / blocker B232-AUDIT-DEPENDENCY: the row is met when the
# historical pre-fix advisory capture is retained AND an ordinary audited restore/build is green
# (docs/specs/status/D232-sqlserver-pivot-unpivot-1.md, section "Open defect blocks (r=4)").
SECURITY_MASK_TOKENS = ("NuGetAudit=false", "RestoreAdditionalProjectSources")
SEC_REL = f"{R_REL}/security"
SEC_PREP_REL = f"{SEC_REL}/advisory-capture-prepin.json"
SEC_ADVISORIES_REL = f"{SEC_REL}/advisories.json"
SEC_ISSUE_REL = f"{SEC_REL}/issue.json"
SEC_GH_SEARCH_REL = f"{SEC_REL}/gh-search"
BUILD_OUT_REL = f"{R_REL}/build/solution-build.out"
BUILD_EXIT_REL = f"{R_REL}/build/solution-build.exit"
SEC_GH_FILES = ("mine.txt", "nu1902.txt", "nu1903.txt", "pkcs.txt", "sqldata.txt")
SEC_ADVISORY_KEYS = (("NU1902", "GHSA-2fmv-rv2c-59x7"), ("NU1903", "GHSA-846f-f764-76mx"))
SEC_PACKAGE = "System.Security.Cryptography.Pkcs"
SEC_DEPENDENCY = "Microsoft.Data.SqlClient"
SEC_TRACKING_ISSUE = 236
SEC_PREDICATE_OLD = (
    "exit 0 iff the freshly executed `dotnet restore nextorm.slnx` output still carries "
    "NU1902/NU1903 advisory lines AND $R/security/gh-search/*.txt exists -- i.e. the advisories "
    "must STILL be live")
SEC_PREDICATE_NEW = (
    "exit 0 iff (1) $R/security/advisory-capture-prepin.json and $R/security/advisories.json are "
    "present with complete pre-fix data -- both advisories (NU1902 GHSA-2fmv-rv2c-59x7 / NU1903 "
    "GHSA-846f-f764-76mx), the Microsoft.Data.SqlClient -> System.Security.Cryptography.Pkcs "
    "package path and the recorded numeric exit codes; (2) the duplicate-search records "
    "($R/security/gh-search/*.txt) and the remedy-ownership record ($R/security/issue.json -> "
    "tracking issue #236) exist; (3) a freshly executed ordinary (unmasked) `dotnet restore "
    "nextorm.slnx` exits 0 with zero NU19 occurrences in its output; (4) an ordinary (unmasked) "
    "`dotnet build nextorm.slnx -c Debug` is green (exit 0, 0 Warning(s) / 0 Error(s)); "
    "(5) NuGetAudit=false / RestoreAdditionalProjectSources in any captured/executed argv or in "
    "this case fails closed; (6) any missing/blank input of (1)-(4) fails closed")


def _json_doc(rel: str):
    """Read a JSON evidence document under ROOT; return (doc, problem-or-None)."""
    path = os.path.join(ROOT, rel)
    if not os.path.exists(path):
        return None, "missing evidence file %s" % rel
    try:
        with open(path, encoding="utf-8") as fh:
            doc = json.load(fh)
    except (OSError, ValueError) as exc:
        return None, "unreadable/blank evidence file %s (%s)" % (rel, exc)
    if not doc:
        return None, "blank evidence file %s" % rel
    return doc, None


def _security_argv_sources() -> list[tuple[str, list]]:
    """Every executed/captured argv of the security case (input of the masking gate)."""
    sources = [("case:security restore argv", list(SECURITY_RESTORE_ARGV)),
               ("case:security build argv", list(SECURITY_BUILD_ARGV))]
    for rel, key in ((SEC_ADVISORIES_REL, "commands"), (SEC_ISSUE_REL, "failing_commands")):
        doc, _ = _json_doc(rel)
        for idx, entry in enumerate((doc or {}).get(key) or []):
            if isinstance(entry, dict) and isinstance(entry.get("argv"), list):
                sources.append(("%s:%s[%d].argv" % (rel, key, idx), entry["argv"]))
    return sources


def _security_masking() -> list[dict]:
    """Fail-closed gate (5): no captured/executed argv may mask the NuGet audit."""
    hits = []
    for source, argv in _security_argv_sources():
        text = " ".join(str(a) for a in argv)
        for token in SECURITY_MASK_TOKENS:
            if token in text:
                hits.append(dict(source=source, argv=argv, token=token))
    return hits


def _security_build_capture() -> dict:
    """(4) Ordinary unmasked solution build: reuse the fresh capture, re-run when inconsistent."""
    captured = dict(argv=list(SECURITY_BUILD_ARGV), cwd=ROOT, masked=False)
    out_p = os.path.join(ROOT, BUILD_OUT_REL)
    exit_p = os.path.join(ROOT, BUILD_EXIT_REL)
    exit_code, out_text, log_rel = None, "", BUILD_OUT_REL
    if os.path.exists(out_p) and os.path.exists(exit_p):
        try:
            exit_code = int(open(exit_p, encoding="utf-8").read().strip())
            out_text = open(out_p, encoding="utf-8", errors="replace").read()
            captured["reused"] = True
        except (OSError, ValueError):
            exit_code, out_text = None, ""
    consistent = (exit_code == 0 and "Build succeeded." in out_text
                  and "0 Warning(s)" in out_text and "0 Error(s)" in out_text)
    if not consistent:
        res = _run("security-build", list(SECURITY_BUILD_ARGV))
        exit_code, out_text, log_rel = res["exit"], res["stdout"] + res["stderr"], res["out"]
        captured["reused"] = False
    captured.update(exit=exit_code, build_succeeded="Build succeeded." in out_text,
                    warnings_zero="0 Warning(s)" in out_text,
                    errors_zero="0 Error(s)" in out_text,
                    nu19_occurrences=len(re.findall(r"NU19", out_text)), log=log_rel)
    return captured


def write_security_predicate_change(payload: dict) -> None:
    """Record the D-13 predicate repair (old vs new) beside the security evidence (CRLF)."""
    doc = dict(
        case="security", row=CASES["security"][0], unit="D-13",
        task="T232 (#232) r=4 DO -- D-13 security prerequisite coordination",
        contract_row="EC232-RV4-SECURITY (blocker B232-AUDIT-DEPENDENCY, P1)",
        contract_required_acceptance=(
            "advisory captured with complete identifiers/package paths/exit codes; duplicate "
            "search run; remedy ownership tracked by a separate issue; an ordinary audited "
            "restore/build must be green (docs/specs/status/D232-sqlserver-pivot-unpivot-1.md, "
            "section 'Open defect blocks (r=4)', key B232-AUDIT-DEPENDENCY)"),
        old_predicate=SEC_PREDICATE_OLD,
        new_predicate=SEC_PREDICATE_NEW,
        weakened=False,
        change="predicate repaired strictly in the direction of the contract row: the stale "
               "'advisories must still be live' requirement is replaced by 'complete pre-fix "
               "capture retained AND an ordinary unmasked restore/build is green'",
        justification=("the contract's required_acceptance demands both a complete advisory "
                       "capture and an ordinary audited restore/build that is green; the remedy "
                       "landed as T236 (Directory.Packages.props:18 pins "
                       "System.Security.Cryptography.Pkcs 10.0.13) so the live-advisory predicate "
                       "was stale, and the new predicate adds a green unmasked restore/build gate "
                       "instead of dropping any required acceptance element"),
        no_weakening_check=dict(
            stale_requirement="`dotnet restore` output must still carry NU1902/NU1903 advisory "
                              "lines -- REMOVED (contradicts the required green audited restore)",
            retained=["complete pre-fix advisory capture (never deleted; kept as evidence that "
                      "the advisory was real)",
                      "advisory identifiers NU1902/GHSA-2fmv-rv2c-59x7 and "
                      "NU1903/GHSA-846f-f764-76mx",
                      "affected package path Microsoft.Data.SqlClient -> "
                      "System.Security.Cryptography.Pkcs",
                      "recorded numeric exit codes", "duplicate search records",
                      "remedy ownership tracked by separate issue #236"],
            added=["re-executed ordinary restore must exit 0 with zero NU19 occurrences",
                   "ordinary unmasked solution build must be green (0 Warning(s) / 0 Error(s))",
                   "fail-closed (exit 2) on NuGetAudit=false / RestoreAdditionalProjectSources "
                   "in any captured/executed argv or in the case itself"]),
        evidence=dict(pre_fix_capture=[SEC_PREP_REL, SEC_ADVISORIES_REL],
                      duplicate_search=SEC_GH_SEARCH_REL, remedy_ownership=SEC_ISSUE_REL,
                      restore=(payload.get("restore") or {}).get("log"),
                      build=(payload.get("build") or {}).get("log")),
        fix="T236 (#236) -- Directory.Packages.props:18 System.Security.Cryptography.Pkcs 10.0.13",
        predicate_status=payload.get("status"), captured=_now(), cwd=ROOT,
    )
    text = json.dumps(doc, indent=1, ensure_ascii=False, sort_keys=True) + "\n"
    with open(os.path.join(R, "security", "predicate-change.json"), "w", encoding="utf-8",
              newline="") as fh:
        fh.write(text.replace("\n", "\r\n"))


def case_security() -> tuple[dict, int]:
    problems: list[str] = []
    details: dict = {}
    checks = dict(advisory_capture=False, duplicate_search=False, remedy_ownership=False,
                  restore_green_unmasked=False, build_green_unmasked=False)

    # (5) masking gate first: nothing is credited while the audit is masked
    masking = _security_masking()
    details["masking"] = dict(tokens=list(SECURITY_MASK_TOKENS), violations=masking,
                              checked_argv_sources=[s for s, _ in _security_argv_sources()])
    if masking:
        problems.append("audit masking in argv: %s"
                        % ", ".join(sorted({h["token"] for h in masking})))

    # (1) historical pre-fix capture, complete, retained as-is (never deleted)
    prep, prep_err = _json_doc(SEC_PREP_REL)
    adv, adv_err = _json_doc(SEC_ADVISORIES_REL)
    problems += [p for p in (prep_err, adv_err) if p]
    if prep is not None and adv is not None:
        found = {}
        for code, ghsa in SEC_ADVISORY_KEYS:
            found[code] = all(
                any(isinstance(a, dict) and a.get("code") == code and a.get("ghsa") == ghsa
                    for a in (doc.get("advisories") or []))
                for doc in (prep, adv))
            if not found[code]:
                problems.append("advisory %s/%s not recorded in both captures" % (code, ghsa))
        prep_text = json.dumps(prep, ensure_ascii=False)
        adv_text = json.dumps(adv, ensure_ascii=False)
        path_ok = (SEC_PACKAGE in prep_text and SEC_PACKAGE in adv_text
                   and SEC_DEPENDENCY in (prep_text + adv_text))
        if not path_ok:
            problems.append("affected package path %s -> %s not recorded in the captures"
                            % (SEC_DEPENDENCY, SEC_PACKAGE))
        exits = [prep.get("restore_exit")] + [c.get("exit_code")
                                              for c in (adv.get("commands") or [])
                                              if isinstance(c, dict)]
        exits_ok = bool(exits) and all(isinstance(e, int) for e in exits)
        if not exits_ok:
            problems.append("capture does not record a numeric exit code for every command")
        checks["advisory_capture"] = path_ok and exits_ok and bool(found) and all(found.values())
        details["advisory_capture"] = dict(
            pre_fix_capture=[SEC_PREP_REL, SEC_ADVISORIES_REL], retained=True, deleted=False,
            advisory_identifiers=found, package_path=[SEC_DEPENDENCY, SEC_PACKAGE],
            recorded_exit_codes=exits, root_cause=adv.get("root_cause"),
            note="the historical pre-fix capture is kept as the evidence that the advisory was "
                 "real; only the predicate that demanded still-live advisories was repaired")

    # (2) duplicate search + remedy ownership tracked by a separate issue
    gh_dir = os.path.join(ROOT, SEC_GH_SEARCH_REL)
    gh_files = (sorted(n for n in os.listdir(gh_dir) if n.endswith(".txt"))
                if os.path.isdir(gh_dir) else [])
    gh_blank = [n for n in gh_files if not open(os.path.join(gh_dir, n), encoding="utf-8",
                                                errors="replace").read().strip()]
    gh_missing = [n for n in SEC_GH_FILES if n not in gh_files]
    if gh_missing:
        problems.append("duplicate-search record(s) missing: %s" % ", ".join(gh_missing))
    if gh_blank:
        problems.append("blank duplicate-search record(s): %s" % ", ".join(gh_blank))
    checks["duplicate_search"] = bool(gh_files) and not gh_missing and not gh_blank
    details["duplicate_search"] = dict(directory=SEC_GH_SEARCH_REL, files=gh_files,
                                       required=list(SEC_GH_FILES), missing=gh_missing,
                                       blank=gh_blank)
    issue, issue_err = _json_doc(SEC_ISSUE_REL)
    if issue_err:
        problems.append(issue_err)
    else:
        iss = issue.get("issue") or {}
        dup = issue.get("duplicate_search") or {}
        iss_ok = (iss.get("number") == SEC_TRACKING_ISSUE
                  and str(SEC_TRACKING_ISSUE) in str(iss.get("url") or ""))
        if not iss_ok:
            problems.append("remedy-ownership record does not name tracking issue #%d"
                            % SEC_TRACKING_ISSUE)
        if not str(dup.get("result") or "").strip():
            problems.append("duplicate-search result is blank in %s" % SEC_ISSUE_REL)
        checks["remedy_ownership"] = bool(iss_ok)
        details["remedy_ownership"] = dict(source=SEC_ISSUE_REL, issue_number=iss.get("number"),
                                           url=iss.get("url"), state=iss.get("state"),
                                           blocker_key=iss.get("blocker_key"),
                                           duplicate_search_result=dup.get("result"))

    # (3) ordinary (unmasked) restore, re-executed here: real exit code + real output
    res = _run("security", list(SECURITY_RESTORE_ARGV))
    text = res["stdout"] + res["stderr"]
    nu19 = len(re.findall(r"NU19", text))
    if res["exit"] != 0:
        problems.append("ordinary `dotnet restore nextorm.slnx` exit %d (expected 0)" % res["exit"])
    if nu19:
        problems.append("ordinary restore output still carries %d NU19 occurrence(s)" % nu19)
    checks["restore_green_unmasked"] = res["exit"] == 0 and nu19 == 0
    details["restore"] = dict(argv=list(SECURITY_RESTORE_ARGV), cwd=ROOT, exit=res["exit"],
                              masked=False, nu19_occurrences=nu19, log=res["out"])

    # (4) ordinary (unmasked) solution build: reuse the fresh capture or re-run it
    build = _security_build_capture()
    if build["exit"] != 0 or not build["warnings_zero"] or not build["errors_zero"] \
            or build["nu19_occurrences"]:
        problems.append("ordinary `dotnet build nextorm.slnx -c Debug` is not green (exit %s, "
                        "0 Warning(s)=%s, 0 Error(s)=%s, NU19=%d)"
                        % (build["exit"], build["warnings_zero"], build["errors_zero"],
                           build["nu19_occurrences"]))
    checks["build_green_unmasked"] = (build["exit"] == 0 and build["warnings_zero"]
                                      and build["errors_zero"] and not build["nu19_occurrences"])
    details["build"] = build

    ok = all(checks.values())
    payload = dict(row=CASES["security"][0], unit="D-13", status="met" if ok else "open",
                   acceptance=CASES["security"][4], checks=checks,
                   predicate=dict(old=SEC_PREDICATE_OLD, new=SEC_PREDICATE_NEW, weakened=False,
                                  changed_this_unit=True,
                                  recorded=f"{SEC_REL}/predicate-change.json"),
                   masking=details.pop("masking"), **details,
                   reason=None if ok else "fail-closed: " + "; ".join(problems))
    write_security_predicate_change(payload)
    return payload, (0 if ok else 2)


def case_seal() -> tuple[dict, int]:
    manifest = os.path.join(R, "manifest.json")
    res = _run("seal", ["python3", "scripts/validate_inner_loop.py", "manifest", manifest])
    data = json.load(open(manifest, encoding="utf-8"))
    open_rows = [r["id"] for r in data["rows"] if r.get("status") == "open"]
    ok = res["exit"] == 0 and not open_rows
    payload = dict(row=CASES["seal"][0], unit="D-12", schema_exit=res["exit"],
                   status="met" if ok else "open", open_rows=open_rows,
                   reason=None if ok else f"{len(open_rows)} applicable rows still open")
    return payload, (0 if ok else 2)


def main(argv: list[str]) -> int:
    if len(argv) != 2 or argv[0] != "--case":
        sys.stderr.write("usage: python3 verify.py --case <case>\n")
        return 1
    case = argv[1]
    if case not in CASES:
        sys.stderr.write(f"unknown case: {case!r}\n")
        return 1
    row, unit, inputs, cmd, acceptance = CASES[case]
    os.makedirs(LOGS, exist_ok=True)

    if case == "packaging":
        payload, code = case_packaging()
        _write(case, payload, "packaging/packaging.json")
        print(json.dumps({k: payload[k] for k in ("case", "status", "assertions", "reason")}, ensure_ascii=False))
        return code
    if case == "security":
        payload, code = case_security()
        _write(case, payload)
        print(json.dumps({k: payload[k] for k in ("case", "status", "checks", "reason")},
                         ensure_ascii=False))
        return code
    if case == "seal":
        payload, code = case_seal()
        _write(case, payload)
        print(json.dumps({k: payload[k] for k in ("case", "status", "open_rows", "reason")}, ensure_ascii=False))
        return code

    missing = [p for p in inputs if not os.path.exists(os.path.join(ROOT, p))]
    if missing:
        payload = dict(row=row, unit=unit, status="blocked", missing_inputs=missing,
                       reason="required input not present (planned future unit); fail-closed")
        _write(case, payload)
        print(json.dumps({k: payload[k] for k in ("case", "status", "missing_inputs")}, ensure_ascii=False))
        return 1
    if cmd is None:
        payload = dict(row=row, unit=unit, status="blocked",
                       reason="no executable route wired yet; fail-closed")
        _write(case, payload)
        return 1
    res = _run(case, cmd)
    payload = dict(row=row, unit=unit, acceptance=acceptance, exit=res["exit"], log=res["out"],
                   status="met" if res["exit"] == 0 else "open",
                   reason=None if res["exit"] == 0 else f"command exit {res['exit']}")
    _write(case, payload)
    print(json.dumps({k: payload[k] for k in ("case", "status", "exit")}, ensure_ascii=False))
    return 0 if res["exit"] == 0 else 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
