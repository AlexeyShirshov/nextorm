#!/usr/bin/env python3
"""T286b (issue #286) evidence runner -- the frozen command adapter of plan `r=1`, contract `rv1`.

Every frozen command ID of ``.pdca/status/T286b-1.md`` §11 is one subcommand here:

    preflight | build | unit | provider-sql | integration | coverage | benchmark | docs |
    closure | gate --gate helper-1|helper-2|provenance | family --family FNN

Contract rules implemented by this adapter (it is an execution/logging adapter, never an
alternative oracle):

* each command writes ``<evidence-root>/<dir>/result.json`` and ``<evidence-root>/<dir>/run.log``;
* every child invocation is stored in ``<evidence-root>/commands.json`` **before** its first
  execution, and the expanded argv actually used is echoed into the result;
* the complete stdout/stderr of every child is preserved verbatim (``run.log`` plus one
  ``*.stdout.log``/``*.stderr.log`` pair per child);
* a nonzero child exit is never converted into success;
* a test command that selects/executes zero cases is a hard failure;
* ``NEXTORM_SKIP_PROVIDERS`` is rejected for integration/coverage, and an integration run without
  ``DOCKER_HOST`` is rejected;
* test discovery uses ``-list tests`` (never ``--list-tests``);
* children run with ``cwd`` = the lane worktree, strictly serial (no parallel builds).

Usage (cwd = the lane worktree):

    python3 .pdca/scripts/T286b-evidence.py preflight --rv rv1
    python3 .pdca/scripts/T286b-evidence.py build --rv rv1
    python3 .pdca/scripts/T286b-evidence.py benchmark --rv rv1 --phase baseline

Standard library only. No ``shell=True`` anywhere.
"""

from __future__ import annotations

import argparse
import datetime
import fcntl
import http.client
import json
import os
import re
import socket
import subprocess
import sys
import time
import urllib.parse

# ---------------------------------------------------------------------------------------------
# Fixed locations and identities
# ---------------------------------------------------------------------------------------------

TASK = "T286b"
MAIN_TREE = "/home/alex/sources/nextorm"
LANE_DEFAULT = "/home/alex/sources/nextorm-worktrees/rc4/g1"
EVIDENCE_ROOT_DEFAULT = os.path.join(MAIN_TREE, ".pdca", TASK, "rv1")

GROUP_BRANCH = "collection/rc4/group-1"
TASK_BRANCH = "collection/rc4/task-1-12"
GROUP_SHA = "1b59e661bc5b3787300aa74687724b8f42605843"
DELTA_HISTORY = ("7407272a", "d20b91e3", "1b59e661")

LEASE = "/tmp/opencode/nextorm-container-lease.lock"
DOCKER_HOST = "unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock"
DOCKER_SOCKET = "/mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock"
CLICKHOUSE_IMAGE = "clickhouse/clickhouse-server:25.8-alpine"

CATALOG = "docs/specs/roadmap/sql-function-coverage-gap.md"
CH_API = "src/nextorm.core/Query/SqlFunctions.ClickHouse.cs"
CH_DIALECT = "src/nextorm.clickhouse/ClickHouseDialect.cs"
CH_TRANSLATOR = "src/nextorm.core/Visitors/ClickHouseNativeFunctionTranslator.cs"
CH_SCALAR_PINS = "tests/nextorm.integration.tests/ClickHouse/ClickHouseScalarFunctionsTests.cs"

SNAPSHOT_PATHS = (
    CATALOG,
    "docs/specs/roadmap/sql-capabilities-gap-analysis.md",
    CH_API,
    "src/nextorm.core/Query/SqlFunctions.cs",
    CH_TRANSLATOR,
    "src/nextorm.core/Visitors/ArraySqlTranslator.cs",
    "src/nextorm.core/Visitors/AdvancedAggregateTranslator.cs",
    "src/nextorm.core/Visitors/DictionarySqlTranslator.cs",
    "src/nextorm.core/Visitors/NormSqlTranslator.cs",
    CH_DIALECT,
    "src/nextorm.core/DataContext/Dialect/ISqlDialect.cs",
    "src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs",
    CH_SCALAR_PINS,
    "benchmarks/nextorm.benchmark/ClickHouse/ClickHouseTranslationBenchmark.cs",
)

FAMILIES = (
    ("F01", "String"),
    ("F02", "Numeric/math"),
    ("F03", "Rounding"),
    ("F04", "Date/time"),
    ("F05", "Array"),
    ("F06", "Tuple"),
    ("F07", "Map"),
    ("F08", "Higher-order"),
    ("F09", "JSON"),
    ("F10", "Hash"),
    ("F11", "Encoding"),
    ("F12", "UUID/ULID"),
    ("F13", "URL/IP"),
    ("F14", "Bit"),
    ("F15", "Type conversion"),
    ("F16", "Aggregate"),
    ("F17", "Window"),
    ("F18", "Dictionary"),
    ("F19", "Table functions"),
)
FAMILY_OF = {label: code for code, label in FAMILIES}

# Variant dimensions of plan §13 (stable IDs; new variants add IDs, never renumber).
DIMENSIONS = (
    ("V01", "Invocation/input"),
    ("V02", "Null/default"),
    ("V03", "CLR shape"),
    ("V04", "Strings/binary"),
    ("V05", "Numeric/conversion"),
    ("V06", "Date/time"),
    ("V07", "Collection"),
    ("V08", "Lambda"),
    ("V09", "Aggregate/window"),
    ("V10", "Dictionary"),
    ("V11", "Provider"),
    ("V12", "Flags/options"),
)
FAMILY_DIMENSIONS = {
    "F01": ("V01", "V02", "V03", "V04", "V11", "V12"),
    "F02": ("V01", "V02", "V03", "V05", "V11", "V12"),
    "F03": ("V01", "V02", "V03", "V05", "V11", "V12"),
    "F04": ("V01", "V02", "V03", "V06", "V11", "V12"),
    "F05": ("V01", "V02", "V03", "V07", "V11", "V12"),
    "F06": ("V01", "V02", "V03", "V07", "V11", "V12"),
    "F07": ("V01", "V02", "V03", "V07", "V11", "V12"),
    "F08": ("V01", "V02", "V03", "V07", "V08", "V11", "V12"),
    "F09": ("V01", "V02", "V03", "V04", "V11", "V12"),
    "F10": ("V01", "V02", "V03", "V04", "V11", "V12"),
    "F11": ("V01", "V02", "V03", "V04", "V11", "V12"),
    "F12": ("V01", "V02", "V03", "V04", "V11", "V12"),
    "F13": ("V01", "V02", "V03", "V04", "V11", "V12"),
    "F14": ("V01", "V02", "V03", "V05", "V11", "V12"),
    "F15": ("V01", "V02", "V03", "V05", "V11", "V12"),
    "F16": ("V01", "V02", "V03", "V05", "V09", "V11", "V12"),
    "F17": ("V01", "V02", "V03", "V05", "V09", "V11", "V12"),
    "F18": ("V01", "V02", "V03", "V10", "V11", "V12"),
    "F19": ("V01", "V11"),
}

# ---------------------------------------------------------------------------------------------
# Wildcard / ellipsis expansion table -- the concrete vendor-catalogue expansions of plan §U01.
#
# ``tokens`` are the source tokens as written in the §3 ClickHouse cell; ``members`` are the
# concrete ClickHouse function names of the vendor catalogue (25.8) the entry expands to. The
# ``basis`` field is the recorded expansion basis (vendor page + the rule used), so the expansion
# is auditable rather than guessed. ``explicit`` members already appear as literal tokens in the
# same family and are bound to them instead of becoming extra members.
# ---------------------------------------------------------------------------------------------

EXPANSIONS = (
    {
        "id": "W01",
        "family": "F01",
        "tokens": ["normalizeUTF8*"],
        "members": ["normalizeUTF8NFC", "normalizeUTF8NFD", "normalizeUTF8NFKC", "normalizeUTF8NFKD"],
        "basis": "clickhouse.com/docs/en/sql-reference/functions/string-functions#normalizeutf8 -- the four documented Unicode normalization forms",
    },
    {
        "id": "W02",
        "family": "F04",
        "tokens": ["fromUnixTimestamp*"],
        "members": ["fromUnixTimestamp", "fromUnixTimestamp64Milli", "fromUnixTimestamp64Micro", "fromUnixTimestamp64Nano"],
        "basis": "clickhouse.com/docs/en/sql-reference/functions/date-time-functions#fromunixtimestamp -- base form plus the three 64-bit precision-suffixed forms",
    },
    {
        "id": "W03",
        "family": "F09",
        "tokens": ["simpleJSONExtract*", "…OrDefault"],
        "members": [
            "simpleJSONExtractRaw", "simpleJSONExtractInt", "simpleJSONExtractUInt",
            "simpleJSONExtractFloat", "simpleJSONExtractBool", "simpleJSONExtractString",
            "JSONExtractUInt", "JSONExtractUIntOrDefault",
        ],
        "basis": "clickhouse.com/docs/en/sql-reference/functions/json-functions#simplejsonextractraw -- one simpleJSONExtract<type> per documented result type; the `…OrDefault` ellipsis expands to the OrDefault sibling of the named JSONExtractUInt accessor",
        "explicit": ["JSONExtractUInt"],
    },
    {
        "id": "W04",
        "family": "F10",
        "tokens": ["intHash*"],
        "members": ["intHash32", "intHash64"],
        "basis": "clickhouse.com/docs/en/sql-reference/functions/hash-functions#inthash32 -- the two documented widths",
    },
    {
        "id": "W05",
        "family": "F10",
        "tokens": ["ngramMinHash", "ngramSimHash", "wordShingle*"],
        "members": [
            "ngramMinHash", "ngramSimHash", "ngramMinHashArg", "ngramSimHashArg",
            "wordShingleMinHash", "wordShingleSimHash", "wordShingleMinHashArg", "wordShingleSimHashArg",
        ],
        "basis": "clickhouse.com/docs/en/sql-reference/functions/hash-functions#ngramminhash -- ngram pair plus the arg-returning sibling of each, and the wordShingle<Min|Sim>Hash[Arg] family",
        "explicit": ["ngramMinHash", "ngramSimHash"],
    },
    {
        "id": "W06",
        "family": "F11",
        "tokens": ["base32*", "base58*", "base64*", "URL", "bech32*"],
        "members": [
            "base32Encode", "base32Decode", "base58Encode", "base58Decode",
            "base64Encode", "base64Decode", "base64URLEncode", "base64URLDecode",
            "bech32Encode", "bech32Decode",
        ],
        "basis": "clickhouse.com/docs/en/sql-reference/functions/encoding-functions -- every documented base32/base58/base64/bech32 pair; the `(+URL)` marker expands to the base64URL<Encode|Decode> pair",
        "explicit": ["base64Encode", "base64Decode"],
    },
    {
        "id": "W07",
        "family": "F15",
        "tokens": ["toInt*", "toUInt*", "toFloat*", "toDecimal*", "toInterval*", "accurateCast*", "reinterpretAs*"],
        "members": [
            "toInt8", "toInt16", "toInt32", "toInt64", "toInt128", "toInt256",
            "toUInt8", "toUInt16", "toUInt32", "toUInt64", "toUInt128", "toUInt256",
            "toFloat32", "toFloat64",
            "toDecimal32", "toDecimal64", "toDecimal128", "toDecimal256",
            "toIntervalNanosecond", "toIntervalMicrosecond", "toIntervalMillisecond",
            "toIntervalSecond", "toIntervalMinute", "toIntervalHour", "toIntervalDay",
            "toIntervalWeek", "toIntervalMonth", "toIntervalQuarter", "toIntervalYear",
            "accurateCast", "accurateCastOrNull", "accurateCastOrDefault",
            "reinterpretAsUInt8", "reinterpretAsUInt16", "reinterpretAsUInt32",
            "reinterpretAsUInt64", "reinterpretAsUInt128", "reinterpretAsUInt256",
            "reinterpretAsInt8", "reinterpretAsInt16", "reinterpretAsInt32",
            "reinterpretAsInt64", "reinterpretAsInt128", "reinterpretAsInt256",
            "reinterpretAsFloat32", "reinterpretAsFloat64", "reinterpretAsDate",
            "reinterpretAsDateTime", "reinterpretAsString", "reinterpretAsFixedString",
            "reinterpretAsUUID",
        ],
        "basis": "clickhouse.com/docs/en/sql-reference/functions/type-conversion-functions -- one member per documented width/interval unit; the `*` in accurateCast* covers the OrNull/OrDefault variants only",
    },
    {
        "id": "W08",
        "family": "F12",
        "tokens": ["snowflakeIDToDateTime*"],
        "members": ["snowflakeIDToDateTime", "snowflakeIDToDateTime64"],
        "basis": "clickhouse.com/docs/en/sql-reference/functions/type-conversion-functions#snowflakeidtodatetime -- base and 64-bit precision form",
    },
    {
        "id": "W09",
        "family": "F13",
        "tokens": ["extractURLParameter*", "cutToFirstSignificantSubdomain*", "…"],
        "members": [
            "extractURLParameter", "extractURLParameterNames",
            "cutToFirstSignificantSubdomain", "cutToFirstSignificantSubdomainWithWWW",
            "cutToFirstSignificantSubdomainCustom", "cutToFirstSignificantSubdomainCustomWithWWW",
            "urlHash", "urlHierarchy", "urlPathHierarchy", "protocol", "domain",
            "domainWithoutWWW", "firstSignificantSubdomain", "firstSignificantSubdomainCustom",
            "topLevelDomain", "port", "path", "pathFull", "queryString", "fragment",
            "queryStringAndFragment", "cutWWW", "cutQueryString", "cutFragment",
            "cutQueryStringAndFragment", "decodeURLComponent", "encodeURLFormComponent",
            "decodeURLFormComponent", "IPv4StringToNum", "IPv4StringToNumOrDefault",
            "IPv4StringToNumOrNull", "IPv4NumToStringClassC", "IPv6StringToNum",
            "IPv6StringToNumOrDefault", "IPv6StringToNumOrNull", "IPv4ToIPv6", "cutIPv6",
        ],
        "basis": "clickhouse.com/docs/en/sql-reference/functions/url-functions + /ip-address-functions -- star entries expand to the documented Suffix/WithWWW/Custom variants; the literal ellipsis expands to the documented URL/IP family members not already named in the cell",
        "explicit": ["protocol", "domain", "port", "path", "queryString", "fragment", "topLevelDomain"],
    },
    {
        "id": "W10",
        "family": "F06",
        "tokens": ["tuple+", "tuple-", "tuple*", "tuple/"],
        "members": ["tuplePlus", "tupleMinus", "tupleMultiply", "tupleDivide"],
        "basis": "clickhouse.com/docs/en/sql-reference/functions/tuple-functions -- the four documented tuple arithmetic operators and their function spellings",
    },
    {
        "id": "W11",
        "family": "F16",
        "tokens": ["exponentialTimeDecayed*"],
        "members": [
            "exponentialTimeDecayedSum", "exponentialTimeDecayedMax",
            "exponentialTimeDecayedCount", "exponentialTimeDecayedAvg",
        ],
        "basis": "clickhouse.com/docs/en/sql-reference/aggregate-functions/reference/exponentialtimedecayed -- the four documented variants",
    },
    {
        "id": "W12",
        "family": "F16",
        "tokens": ["studentTTest*", "quantiles*", "timeSeries*", "groupArray*"],
        "members": [
            "studentTTest", "studentTTestOneSample",
            "quantiles", "quantilesExact", "quantilesExactLow", "quantilesExactHigh",
            "quantilesExactWeighted", "quantilesTiming", "quantilesTDigest", "quantilesBFloat16",
            "quantilesDD", "quantilesGK", "quantilesPrometheusHistogram",
            "timeSeriesGroupSum", "timeSeriesGroupRateSum", "timeSeriesInstantRateToGrid",
            "timeSeriesRateToGrid", "timeSeriesResampleToGridWithStaleness",
            "groupArray", "groupArrayLast", "groupArraySample", "groupArraySorted",
            "groupArrayMovingAvg", "groupArrayMovingSum", "groupArrayInsertAt", "groupArrayIntersect",
        ],
        "basis": "clickhouse.com/docs/en/sql-reference/aggregate-functions -- star entries expand to the documented algorithm-suffixed plural quantiles family, the timeSeries grid family and the groupArray* family; members already spelled out in the cell stay bound to their literal token",
        "explicit": [
            "groupArrayMovingAvg", "groupArrayMovingSum", "groupArraySample", "groupArraySorted",
            "groupArrayLast", "groupArrayInsertAt", "groupArrayIntersect", "quantiles",
        ],
    },
    {
        "id": "W13",
        "family": "F18",
        "tokens": ["dictGet*", "regionTo*"],
        "members": [
            "dictGet", "dictGetOrDefault", "dictGetOrNull", "dictGetHierarchy", "dictGetChildren",
            "dictGetKeys", "dictGetRoot", "dictGetAll", "dictGetDescendants",
            "regionToName", "regionToCity", "regionToArea", "regionToDistrict", "regionToCountry",
            "regionToContinent", "regionToTopContinent", "regionToPopulation", "regionToParent",
            "regionToParents", "regionHierarchy", "regionIn",
        ],
        "basis": "clickhouse.com/docs/en/sql-reference/functions/ext-dict-functions + /other-functions#regionToName -- the typed dictGet accessors and the documented regionTo* hierarchy family",
        "explicit": [
            "dictGetKeys", "dictGetRoot", "dictGetAll", "dictGetDescendants",
            "regionIn", "regionHierarchy",
        ],
    },
)

WILDCARD_ENTRY_COUNT_CLAIM = 13  # plan §4 measurement of the §3 cell

ENV_EXACT = (
    "PATH", "HOME", "USER", "LOGNAME", "SHELL", "TMPDIR", "LANG", "LC_ALL",
    "DOTNET_ROOT", "DOTNET_CLI_HOME", "DOTNET_CLI_TELEMETRY_OPTOUT", "DOTNET_NOLOGO",
    "DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "DOTNET_CLI_UI_LANGUAGE", "NUGET_PACKAGES",
    "DOCKER_HOST", "TESTCONTAINERS_RYUK_DISABLED",
)
ENV_PREFIX = "NEXTORM_"
FORBIDDEN_ENV = "NEXTORM_SKIP_PROVIDERS"
CONTAINER_PROVIDERS = ("postgres", "sqlserver", "mysql", "mariadb", "clickhouse")

# No child may start a long-lived build server: a node/compiler spawned while a lease-holding
# child runs would inherit the flock file descriptor (an OFD lock survives the holder) and keep
# the collection lease after the run finishes. Node reuse and the shared compiler are therefore
# disabled for every child, and run_child() closes all fds but the standard three.
BUILD_SERVER_ENV = {
    "MSBUILDDISABLENODEREUSE": "1",
    "DOTNET_CLI_USE_MSBUILD_SERVER": "0",
    "UseSharedCompilation": "false",
}

# Lease policy (fixed after the D3 lease-hygiene notice):
#   integration -> the frozen C04 line wraps the runner in the outer flock; no internal lease.
#   coverage    -> the frozen C05 line provides the outer flock; no internal lease.
#   family      -> the frozen family line carries no outer flock; the runner takes the internal
#                  lease around each focused live child.
LEASE_POLICY = {
    "integration": "external",
    "coverage": "external",
    "family": "internal-per-live-child",
}


# ---------------------------------------------------------------------------------------------
# Small helpers
# ---------------------------------------------------------------------------------------------


def utc_now() -> str:
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def rel(path: str, root: str) -> str:
    try:
        return os.path.relpath(path, root)
    except ValueError:
        return path


def ensure_dir(path: str) -> str:
    os.makedirs(path, exist_ok=True)
    return path


def write_json(path: str, payload) -> str:
    ensure_dir(os.path.dirname(path))
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, indent=2, ensure_ascii=False)
        handle.write("\n")
    return path


def read_text(path: str) -> str:
    with open(path, "r", encoding="utf-8", errors="replace") as handle:
        return handle.read()


def lane_root() -> str:
    try:
        out = subprocess.run(
            ["git", "rev-parse", "--show-toplevel"],
            cwd=os.getcwd(), capture_output=True, text=True, timeout=60, shell=False,
        )
        if out.returncode == 0 and out.stdout.strip():
            return out.stdout.strip()
    except Exception:  # noqa: BLE001 - fall back to the pinned lane
        pass
    return LANE_DEFAULT


def build_env(require_docker: bool = False, forbid_optouts: bool = False) -> dict:
    env = {name: os.environ[name] for name in ENV_EXACT if name in os.environ}
    for name, value in os.environ.items():
        if name.startswith(ENV_PREFIX):
            env[name] = value
    if forbid_optouts and FORBIDDEN_ENV in env:
        raise RuntimeError(
            "%s=%r is set: a run with provider opt-outs cannot support evidence and is refused."
            % (FORBIDDEN_ENV, env[FORBIDDEN_ENV])
        )
    if require_docker and not env.get("DOCKER_HOST"):
        raise RuntimeError(
            "DOCKER_HOST is not set: the integration/coverage evidence is fail-closed and a run "
            "without the socket is refused (use %s)." % DOCKER_HOST
        )
    if "PATH" not in env:
        raise RuntimeError("PATH is missing from the environment allowlist")
    env.update(BUILD_SERVER_ENV)
    return env


def normalize(name: str) -> str:
    """Case/underscore-insensitive key used to bind a source token to a CLR member or SQL arm."""
    return re.sub(r"[^a-z0-9]", "", name.lower())


# ---------------------------------------------------------------------------------------------
# Test-summary parsing (xunit v3 console + Microsoft.Testing.Platform) and zero-test rejection
# ---------------------------------------------------------------------------------------------

_MTP_SUMMARY = re.compile(r"^\s*(total|failed|succeeded|passed|skipped|not run|notrun)\s*:\s*(\d+)\s*$", re.I)
_XUNIT_SUMMARY = re.compile(
    r"Total:\s*(\d+),\s*Errors:\s*(\d+),\s*Failed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Not Run:\s*(\d+)",
    re.I,
)


def parse_test_summary(output: str) -> dict | None:
    """Return {total, failed, skipped, not_run, passed, format} from the last summary block."""
    best = None
    if "Test run summary" in output:
        block = output.rsplit("Test run summary", 1)[1][:4000]
        counts = {}
        for line in block.splitlines():
            match = _MTP_SUMMARY.match(line)
            if match:
                counts[re.sub(r"\s+", "", match.group(1).lower())] = int(match.group(2))
        if counts.get("total") is not None:
            best = {
                "format": "microsoft-testing-platform",
                "total": counts.get("total"),
                "failed": counts.get("failed"),
                "passed": counts.get("succeeded", counts.get("passed")),
                "skipped": counts.get("skipped"),
                "not_run": counts.get("notrun"),
            }
    matches = list(_XUNIT_SUMMARY.finditer(output))
    if matches:
        match = matches[-1]
        candidate = {
            "format": "xunit-v3-console",
            "total": int(match.group(1)),
            "failed": int(match.group(3)),
            "skipped": int(match.group(4)),
            "not_run": int(match.group(5)),
            "passed": int(match.group(1)) - int(match.group(2)) - int(match.group(3)) - int(match.group(4)) - int(match.group(5)),
        }
        # Prefer the console summary (it is the one the skip-budget gate consumes).
        best = candidate
    return best


def count_listed_tests(output: str) -> int:
    total = 0
    for line in output.splitlines():
        stripped = line.strip()
        if not stripped:
            continue
        if stripped.startswith("Test run summary") or _MTP_SUMMARY.match(line) or _XUNIT_SUMMARY.search(line):
            continue
        if stripped.lower().startswith(("passed!", "failed!", "skipped!", "test run summary", "code coverage results")):
            continue
        if stripped.startswith("xUnit.net") or stripped.startswith("Running tests from"):
            continue
        total += 1
    return total


def is_test_argv(argv) -> bool:
    names = {os.path.basename(arg) for arg in argv}
    if "dotnet" not in names:
        return False
    if "test" in argv:
        # `dotnet test <project|solution>` or `dotnet test --filter ...`
        return True
    if "run" in argv:
        # `dotnet run --project tests/X.tests ...`: the xunit v3 runner is the test host.
        return any(arg.startswith("tests/") or ".tests" in arg for arg in argv)
    return False


def is_listing_argv(argv) -> bool:
    return "-list" in argv or "--list-tests" in argv


PROVIDER_SKIP = re.compile(
    r"(?im)(unavailable|not available|container|connection|docker|socket).*(postgres|sql server|mssql|mysql|mariadb|clickhouse)"
    r"|(postgres|sql server|mssql|mysql|mariadb|clickhouse).*(unavailable|not available|container|connection|docker|socket)"
)
SKIP_ENTRY = re.compile(r"\[SKIP\]")
SKIPPED_LINE = re.compile(r"(?im)^\s*Skipped:\s*\d+")
RUNTIME_LINE = re.compile(r"^\[testcontainers")


def skip_view(output: str) -> str:
    """Only the skip-attributable lines of a test run (`TEST [SKIP]` + its single reason line).

    Container-runtime chatter ("Docker container ... mssql-tools") is not a skip reason and must
    never be read as provider-attributable: that false positive is what D21-DEF-3 fixes.
    """
    lines = output.splitlines()
    picked = []
    for index, line in enumerate(lines):
        if SKIP_ENTRY.search(line):
            picked.append(line)
            nxt = index + 1
            while nxt < len(lines) and not lines[nxt].strip():
                nxt += 1
            if nxt < len(lines) and not RUNTIME_LINE.match(lines[nxt]):
                picked.append(lines[nxt])
        elif SKIPPED_LINE.match(line):
            picked.append(line)
    return "\n".join(picked)


# ---------------------------------------------------------------------------------------------
# Child execution and row persistence
# ---------------------------------------------------------------------------------------------


def run_child(argv, cwd: str, env: dict, timeout_s: int, name: str = "") -> dict:
    started_at = utc_now()
    started = time.monotonic()
    state = "finished"
    exit_code = None
    stdout = ""
    stderr = ""
    try:
        completed = subprocess.run(
            list(argv), cwd=cwd, env=env, capture_output=True, text=True, errors="replace",
            timeout=timeout_s, shell=False, close_fds=True,
        )
        exit_code = completed.returncode
        stdout = completed.stdout or ""
        stderr = completed.stderr or ""
    except subprocess.TimeoutExpired as exc:
        state = "timed-out"
        stdout = exc.stdout or ""
        stderr = exc.stderr or ""
        if isinstance(stdout, bytes):
            stdout = stdout.decode("utf-8", "replace")
        if isinstance(stderr, bytes):
            stderr = stderr.decode("utf-8", "replace")
        stderr += "\n[runner] child exceeded timeout_s=%s and was killed\n" % timeout_s
    except FileNotFoundError as exc:
        state = "spawn-failed"
        stderr = "[runner] %s\n" % exc
    duration = round(time.monotonic() - started, 3)
    output = stdout + ("\n" + stderr if stderr else "")
    return {
        "name": name,
        "argv": [str(arg) for arg in argv],
        "cwd": cwd,
        "started_at": started_at,
        "ended_at": utc_now(),
        "duration_s": duration,
        "state": state,
        "exit": exit_code,
        "stdout": stdout,
        "stderr": stderr,
        "output": output,
        "summary": parse_test_summary(output) if (is_test_argv(argv) and not is_listing_argv(argv)) else None,
        "listed": count_listed_tests(output) if is_listing_argv(argv) else None,
    }


class Row:
    """One command row: children, checks, result.json + run.log in its own directory."""

    def __init__(self, cmd_id: str, directory: str, root: str, cwd: str, extra: dict | None = None):
        self.cmd_id = cmd_id
        self.directory = directory
        self.root = root
        self.cwd = cwd
        self.started_at = utc_now()
        self.started = time.monotonic()
        self.children: list[dict] = []
        self.reasons: list[str] = []
        self.checks: dict = {}
        self.artifacts: list[str] = []
        self.test_totals: dict | None = None
        self.extra = extra or {}

    def add_artifact(self, path: str):
        if path:
            self.artifacts.append(rel(path, self.root))

    def fail(self, reason: str):
        self.reasons.append(reason)

    # -- execution ---------------------------------------------------------------------------
    def run(self, argv, timeout_s: int = 3600, env: dict | None = None, name: str = "",
            require_zero: bool = True, min_listed: int | None = None,
            require_summary: bool = True, min_total: int = 1,
            allow_provider_skips: bool = True, log_path_name: str | None = None) -> dict:
        child = run_child(argv, self.cwd, env or build_env(), timeout_s, name=name)
        order = len(self.children)
        child["order"] = order
        child["timeout_s"] = timeout_s
        logs_dir = ensure_dir(os.path.join(self.directory, "logs"))
        stem = "%02d-%s" % (order, (name or os.path.basename(child["argv"][0]) or "child").replace(os.sep, "-"))
        stdout_path = os.path.join(logs_dir, stem + ".stdout.log")
        stderr_path = os.path.join(logs_dir, stem + ".stderr.log")
        with open(stdout_path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(child["stdout"])
        with open(stderr_path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(child["stderr"])
        child["stdout_path"] = rel(stdout_path, self.root)
        child["stderr_path"] = rel(stderr_path, self.root)
        if log_path_name:
            child["log_path"] = log_path_name
            with open(os.path.join(self.directory, log_path_name), "w", encoding="utf-8", newline="\n") as handle:
                handle.write(child["output"])
            self.add_artifact(os.path.join(self.directory, log_path_name))
        self.children.append(child)
        self.add_artifact(stdout_path)
        self.add_artifact(stderr_path)

        if require_zero and (child["state"] != "finished" or child["exit"] != 0):
            self.fail("child %r exited %r (%s)" % (child["argv"], child["exit"], child["state"]))
            return child

        if is_listing_argv(child["argv"]) and min_listed is not None:
            listed = child["listed"] or 0
            if listed < min_listed:
                self.fail("discovery %r listed %s < min_listed=%s" % (child["argv"], listed, min_listed))

        if child["summary"] is not None:
            self.test_totals = child["summary"]
            total = child["summary"].get("total") or 0
            if require_summary and total < min_total:
                self.fail(
                    "zero/insufficient test selection in %r: total=%s (failed=%s skipped=%s)"
                    % (child["argv"], total, child["summary"].get("failed"), child["summary"].get("skipped"))
                )
            if (child["summary"].get("failed") or 0) != 0:
                self.fail("failed=%s in %r" % (child["summary"].get("failed"), child["argv"]))
            skipped = child["summary"].get("skipped") or 0
            if not allow_provider_skips and skipped:
                provider = PROVIDER_SKIP.search(skip_view(child["output"]))
                if provider:
                    self.fail("provider-attributable skip in %r: %r" % (child["argv"], provider.group(0)[:160]))
        elif is_test_argv(child["argv"]) and not is_listing_argv(child["argv"]) and require_summary:
            self.fail("no test summary parsed for %r" % (child["argv"],))
        return child

    def run_docker_probe(self, probe: dict):
        """Record a docker-API probe as one synthetic child (transcript in stdout)."""
        child = {
            "name": "docker-api-probe",
            "argv": probe.get("argv", ["docker-api", "dictionary-fixture-probe"]),
            "cwd": self.cwd,
            "started_at": probe.get("started_at", utc_now()),
            "ended_at": probe.get("ended_at", utc_now()),
            "duration_s": probe.get("duration_s", 0.0),
            "state": probe.get("state", "finished"),
            "exit": probe.get("exit", 0),
            "stdout": probe.get("transcript", ""),
            "stderr": "",
            "output": probe.get("transcript", ""),
            "summary": None,
            "listed": None,
            "order": len(self.children),
            "timeout_s": probe.get("timeout_s", 0),
            "stdout_path": "",
            "stderr_path": "",
            "kind": "docker-api",
        }
        logs_dir = ensure_dir(os.path.join(self.directory, "logs"))
        path = os.path.join(logs_dir, "docker-dictionary-fixture.json")
        write_json(path, probe)
        child["stdout_path"] = rel(path, self.root)
        self.children.append(child)
        self.add_artifact(path)
        if child["exit"] != 0:
            self.fail("dictionary fixture probe failed: %s" % probe.get("reason", "see transcript"))

    # -- persistence -------------------------------------------------------------------------
    def finish(self, ok: bool | None = None) -> dict:
        ended_at = utc_now()
        if ok is None:
            ok = not self.reasons
        payload = {
            "task": TASK,
            "command": self.cmd_id,
            "directory": rel(self.directory, self.root),
            "argv": [os.path.abspath(sys.argv[0])] + sys.argv[1:],
            "cwd": self.cwd,
            "env": {k: v for k, v in build_env().items() if k in ("DOCKER_HOST",) or k.startswith("NEXTORM_")},
            "started_at": self.started_at,
            "ended_at": ended_at,
            "duration_s": round(time.monotonic() - self.started, 3),
            "children": [
                {key: value for key, value in child.items() if key not in ("stdout", "stderr", "output")}
                for child in self.children
            ],
            "test_totals": self.test_totals,
            "checks": self.checks,
            "artifacts": self.artifacts,
            "reasons": self.reasons,
            "ok": ok,
            "exit": 0 if ok else 1,
            **self.extra,
        }
        write_json(os.path.join(self.directory, "result.json"), payload)
        self.write_run_log(payload)
        return payload

    def write_run_log(self, payload: dict):
        path = os.path.join(self.directory, "run.log")
        rerun = os.path.exists(path)
        with open(path, "a", encoding="utf-8", newline="\n") as handle:
            if rerun:
                handle.write("\n--- rerun %s ---\n" % utc_now())
            handle.write("=== %s %s | %s ===\n" % (TASK, self.cmd_id, utc_now()))
            handle.write("cwd: %s\n" % self.cwd)
            for child in self.children:
                handle.write("\n$ %s\n" % " ".join(child["argv"]))
                handle.write("name: %s\nstarted_at: %s\nended_at: %s\nstate: %s\nexit: %s\nduration_s: %s\n"
                             % (child.get("name"), child["started_at"], child["ended_at"], child["state"],
                                child["exit"], child["duration_s"]))
                if child.get("summary"):
                    handle.write("summary: %s\n" % json.dumps(child["summary"]))
                if child.get("listed") is not None:
                    handle.write("listed: %s\n" % child["listed"])
                handle.write("--- stdout ---\n")
                handle.write(child["stdout"])
                if child["stdout"] and not child["stdout"].endswith("\n"):
                    handle.write("\n")
                if child["stderr"]:
                    handle.write("--- stderr ---\n")
                    handle.write(child["stderr"])
                    if not child["stderr"].endswith("\n"):
                        handle.write("\n")
                handle.write("--- end child ---\n")
            handle.write("\n--- checks ---\n%s\n" % json.dumps(payload.get("checks"), indent=2, ensure_ascii=False))
            handle.write("--- result --- ok=%s exit=%s reasons=%s\n"
                         % (payload["ok"], payload["exit"], json.dumps(payload["reasons"], ensure_ascii=False)))
            handle.write("=== end %s | %s ===\n" % (self.cmd_id, utc_now()))


# ---------------------------------------------------------------------------------------------
# Command definitions (the expanded child invocations live in commands.json)
# ---------------------------------------------------------------------------------------------


def dotnet_build(project: str) -> list:
    return ["dotnet", "build", project, "-c", "Debug"]


def dotnet_test(project: str, extra=None) -> list:
    argv = ["dotnet", "test", project, "-c", "Debug"]
    return argv + list(extra or [])


def integration_run(extra=None, internal_lease: bool = True) -> list:
    """A live integration child.

    ``internal_lease`` is true only for the focused live children of ``family``, whose frozen
    invocation carries no outer ``flock``. ``integration`` and ``coverage`` are wrapped by their
    frozen command lines (C04/C05), so an internal lease there would deadlock against the outer one.
    """
    argv = [
        "env", "DOCKER_HOST=" + DOCKER_HOST,
        "dotnet", "run", "--project", "tests/nextorm.integration.tests", "-c", "Debug", "--no-build", "--",
        "-noColor", "-notrait", "Category=Diagnostic",
    ] + list(extra or [])
    return (["flock", LEASE] + argv) if internal_lease else argv


def _has_flock(argv) -> bool:
    return bool(argv) and argv[0] == "flock" and len(argv) > 1 and argv[1] == LEASE


def _lease_acquirable() -> bool:
    """True when the collection lease is currently free (no other process holds it)."""
    if not os.path.exists(LEASE):
        return True
    fd = os.open(LEASE, os.O_RDWR | os.O_CREAT, 0o644)
    try:
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        return False
    else:
        fcntl.flock(fd, fcntl.LOCK_UN)
        return True
    finally:
        os.close(fd)


def lease_held_externally() -> bool:
    return os.path.exists(LEASE) and not _lease_acquirable()


def require_external_lease(scope: str) -> None:
    if not lease_held_externally():
        raise RuntimeError(
            "%s must be run under `flock %s ...`: its frozen command line provides the outer lease, "
            "and the runner takes no internal lease for it (an internal one would deadlock)" % (scope, LEASE))


def lease_policy_selfcheck() -> dict:
    """Static policy assertions plus a no-container dry run of the lock path (C04 shape)."""
    manifest = command_manifest(EVIDENCE_ROOT_DEFAULT, LANE_DEFAULT)
    integration_child = manifest["commands"]["integration"][1]["argv"]
    coverage_children = [child["argv"] for child in COVERAGE_CHILDREN]
    family_live = integration_run(["-class", "X"])

    stale = sorted(_build_server_pid_hint())
    result = {
        "checked_at": utc_now(),
        "policy": dict(LEASE_POLICY),
        "build_server_env": dict(BUILD_SERVER_ENV),
        "close_fds": True,
        "command_children": {
            "integration_internal_flock": _has_flock(integration_child),
            "coverage_internal_flock_count": sum(1 for argv in coverage_children if _has_flock(argv)),
            "family_live_internal_flock": _has_flock(family_live),
        },
        "reuse_nodes_visible": stale,
        "dry_run": {},
        "ok": True,
    }
    # The static contract: no internal lease for the externally wrapped rows, one for a family
    # live child. A violation is a hard failure.
    if result["command_children"]["integration_internal_flock"]:
        result["ok"] = False
    if result["command_children"]["coverage_internal_flock_count"]:
        result["ok"] = False
    if not result["command_children"]["family_live_internal_flock"]:
        result["ok"] = False

    # Dynamic dry run: hold the lease the way C04 does, prove an inner `flock -n` cannot take it
    # (that is exactly the self-deadlock), then prove it is released. No container is touched.
    if lease_held_externally():
        result["dry_run"] = {"skipped": "the lease is held by the invoking context (C04/C05 wrapper)"}
    else:
        holder = subprocess.Popen(
            ["flock", LEASE, "sleep", "1"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            close_fds=True)
        try:
            deadline = time.monotonic() + 3
            while time.monotonic() < deadline and _lease_acquirable():
                time.sleep(0.05)
            inner = subprocess.run(
                ["flock", "-n", LEASE, "true"], capture_output=True, close_fds=True, timeout=10)
            result["dry_run"]["inner_flock_rc_while_outer_held"] = inner.returncode
            result["dry_run"]["inner_expected_rc"] = 1
            if inner.returncode != 1:
                result["ok"] = False
        finally:
            holder.wait(timeout=10)
        released = subprocess.run(
            ["flock", "-n", LEASE, "true"], capture_output=True, close_fds=True, timeout=10)
        result["dry_run"]["outer_flock_rc_after_release"] = released.returncode
        result["dry_run"]["outer_expected_rc"] = 0
        if released.returncode != 0:
            result["ok"] = False
    return result


def _build_server_pid_hint() -> list:
    """Pids of idle MSBuild nodes/VBCSCompiler, recorded for the recurrence check (never killed)."""
    pids = []
    try:
        out = subprocess.run(["pgrep", "-af", "MSBuild.dll|VBCSCompiler"], capture_output=True,
                             text=True, close_fds=True, timeout=20)
        pids = [line.split(" ", 1)[0] for line in out.stdout.splitlines() if line.strip()]
    except Exception:  # noqa: BLE001 - a missing pgrep is not a preflight failure
        return []
    return pids


def cmd_lease_check(args, root: str, lane: str) -> dict:
    directory = ensure_dir(os.path.join(root, "lease-check"))
    row = Row("lease-check", directory, root, lane)
    check = lease_policy_selfcheck()
    write_json(os.path.join(directory, "lease-policy.json"), check)
    row.checks = check
    row.add_artifact(os.path.join(directory, "lease-policy.json"))
    if not check["ok"]:
        row.fail("lease policy self-check failed: %s" % json.dumps(check, ensure_ascii=False))
    return row.finish()


def benchmark_run(phase: str, artifacts_dir: str) -> list:
    return [
        "dotnet", "run", "--project", "benchmarks/nextorm.benchmark", "-c", "Release",
        "--filter", "*ClickHouseTranslationBenchmark*",
        "--artifacts", artifacts_dir,
    ]


COVERAGE_CHILDREN = (
    {"name": "coverage-solution-collect", "argv": [
        "env", "DOCKER_HOST=" + DOCKER_HOST,
        "dotnet", "tool", "run", "dotnet-coverage", "collect",
        "-s", "coverage.settings.xml", "-f", "cobertura",
        "-o", "tests/coverage/coverage.cobertura.xml",
        "dotnet test --no-build --verbosity normal",
    ], "timeout_s": 7200},
    {"name": "coverage-solution-report", "argv": [
        "dotnet", "tool", "run", "reportgenerator",
        "-reports:tests/coverage/coverage.cobertura.xml",
        "-targetdir:tests/coverage/report",
        "-reporttypes:Html;TextSummary;Cobertura",
        "-riskhotspotassemblyfilters:+nextorm.*",
    ], "timeout_s": 1800},
    {"name": "coverage-integration-collect", "argv": [
        "env", "DOCKER_HOST=" + DOCKER_HOST,
        "dotnet", "tool", "run", "dotnet-coverage", "collect",
        "-s", "coverage.integration.settings.xml", "-f", "cobertura",
        "-o", "tests/coverage/integration.cobertura.xml",
        "dotnet test tests/nextorm.integration.tests --no-build --verbosity normal",
    ], "timeout_s": 7200},
    {"name": "coverage-integration-report", "argv": [
        "dotnet", "tool", "run", "reportgenerator",
        "-reports:tests/coverage/integration.cobertura.xml",
        "-targetdir:tests/coverage/integration-report",
        "-reporttypes:Html;TextSummary",
        "-riskhotspotassemblyfilters:+nextorm.*",
    ], "timeout_s": 1800},
    {"name": "coverage-integration-gate", "argv": [
        "dotnet", "tool", "run", "reportgenerator",
        "-reports:tests/coverage/integration.cobertura.xml",
        "-targetdir:tests/coverage/integration-report-gate",
        "-reporttypes:Html;TextSummary",
        "-classfilters:-NextORM.Core.InMemory*",
        "-riskhotspotassemblyfilters:+nextorm.*",
    ], "timeout_s": 1800},
)

PROVIDER_SQL_PROJECTS = (
    "tests/nextorm.clickhouse.tests",
    "tests/nextorm.sqlite.tests",
    "tests/nextorm.postgres.tests",
    "tests/nextorm.sqlserver.tests",
    "tests/nextorm.mysql.tests",
    "tests/nextorm.mariadb.tests",
)

HELPERS = {
    "helper-1": {"script": ("validate_inner_loop.py", "manifest"), "extra": ()},
    "helper-2": {"script": ("pdca_evidence.py", "lint"), "extra": ("--scope-root",)},
    "provenance": {"script": ("pdca_evidence.py", "lint"), "extra": ()},
}


def frozen_manifest_path(root: str) -> str:
    """The frozen evidence manifest: `evidence-manifest.json`, else the legacy `manifest.json`."""
    for name in ("evidence-manifest.json", "manifest.json"):
        candidate = os.path.join(root, name)
        if os.path.exists(candidate):
            return candidate
    return os.path.join(root, "evidence-manifest.json")


def helper_argv(gate: str, root: str) -> list:
    """The canonical helper invocation resolved at D0 (main-tree script copies, cwd = lane)."""
    script, subcommand = HELPERS[gate]["script"]
    argv = ["python3", os.path.join(MAIN_TREE, ".pdca", "scripts", script), subcommand]
    manifest_path = frozen_manifest_path(root)
    if subcommand == "manifest":
        return argv + [manifest_path]
    argv += ["--repo", ".", "--manifest", manifest_path]
    if "--scope-root" in HELPERS[gate]["extra"]:
        argv += ["--scope-root", root]
    return argv


def command_manifest(root: str, lane: str, phase: str = "baseline") -> dict:
    """The expanded child invocations, written to commands.json before the first execution."""
    manifest = {
        "task": TASK,
        "rv": "rv1",
        "repo": MAIN_TREE,
        "worktree": lane,
        "evidence_root": root,
        "container_lease": LEASE,
        "docker_host": DOCKER_HOST,
        "lease_policy": {
            "policy": LEASE_POLICY,
            "build_server_env": BUILD_SERVER_ENV,
            "close_fds": True,
            "reason": "the frozen C04/C05 lines hold the outer flock; a family live child is wrapped "
                      "internally because its frozen line does not",
        },
        "benchmark_host_lock": "/tmp/opencode/nextorm-bdn-host.lock",
        "source_revision": GROUP_SHA,
        "commands": {},
    }
    manifest["commands"]["preflight"] = [
        {"name": "git-head", "argv": ["git", "-C", lane, "rev-parse", "HEAD"]},
        {"name": "git-group", "argv": ["git", "-C", lane, "rev-parse", GROUP_BRANCH]},
        {"name": "git-status", "argv": ["git", "-C", lane, "status", "--porcelain"]},
        {"name": "git-branch", "argv": ["git", "-C", lane, "rev-parse", "--abbrev-ref", "HEAD"]},
        {"name": "git-log", "argv": ["git", "-C", lane, "log", "--no-walk", "--oneline"] + list(DELTA_HISTORY)},
        {"name": "main-head", "argv": ["git", "-C", MAIN_TREE, "rev-parse", "HEAD"]},
        {"name": "roslyn-members", "argv": ["roslynq", "nextorm.slnx", "members", "NextORM.Core.ClickHouseFunctions"]},
        {"name": "roslyn-members-scalars", "argv": ["roslynq", "nextorm.slnx", "members", "NextORM.ClickHouse.ClickHouseScalarFunctions"]},
        {"name": "docker-ping", "argv": ["docker-api", "GET", "/_ping"]},
        {"name": "docker-image-inspect", "argv": ["docker-api", "GET", "/images/%s/json" % CLICKHOUSE_IMAGE]},
        {"name": "dictionary-fixture-probe", "argv": ["docker-api", "run-dictionary-fixture", CLICKHOUSE_IMAGE]},
    ]
    manifest["commands"]["build"] = [
        {"name": "build-core", "argv": dotnet_build("src/nextorm.core/nextorm.core.csproj")},
        {"name": "build-clickhouse", "argv": dotnet_build("src/nextorm.clickhouse/nextorm.clickhouse.csproj")},
        {"name": "build-core-tests", "argv": dotnet_build("tests/nextorm.core.tests")},
        {"name": "build-clickhouse-tests", "argv": dotnet_build("tests/nextorm.clickhouse.tests")},
        {"name": "build-integration-tests", "argv": dotnet_build("tests/nextorm.integration.tests")},
        {"name": "build-benchmark", "argv": ["dotnet", "build", "benchmarks/nextorm.benchmark", "-c", "Release"]},
    ]
    manifest["commands"]["unit"] = [
        {"name": "unit-core", "argv": dotnet_test("tests/nextorm.core.tests"), "min_total": 1},
        {"name": "unit-clickhouse", "argv": dotnet_test("tests/nextorm.clickhouse.tests"), "min_total": 1},
    ]
    manifest["commands"]["provider-sql"] = [
        {"name": "sql-" + project.split("/")[1], "argv": dotnet_test(project), "min_total": 1}
        for project in PROVIDER_SQL_PROJECTS
    ]
    manifest["commands"]["integration"] = [
        {"name": "build-integration", "argv": dotnet_build("tests/nextorm.integration.tests")},
        {"name": "integration-suite", "argv": integration_run(internal_lease=False), "min_total": 1,
         "log": "integration-suite.log"},
        {"name": "integration-skip-budget", "argv": [
            "python3", ".pdca/scripts/check-integration-skip-budget.py",
            "--log", os.path.join(root, "integration", "integration-suite.log"),
        ]},
    ]
    manifest["commands"]["coverage"] = [dict(child) for child in COVERAGE_CHILDREN]
    manifest["commands"]["benchmark"] = [
        {"name": "benchmark-%s" % phase, "argv": benchmark_run(phase, os.path.join(root, "performance", phase, "bdn"))}
    ]
    manifest["commands"]["docs"] = [
        {"name": "docfx", "argv": ["dotnet", "docfx", "docs/docfx.json"]},
    ]
    manifest["commands"]["closure"] = []
    manifest["commands"]["gate"] = {
        gate: [{"name": gate, "argv": helper_argv(gate, root)}] for gate in HELPERS
    }
    manifest["commands"]["family"] = {
        code: [
            {"name": "family-discovery-clickhouse", "argv": [
                "dotnet", "run", "--project", "tests/nextorm.clickhouse.tests", "-c", "Debug", "--no-build", "--",
                "-list", "tests"]},
            {"name": "family-discovery-integration", "argv": [
                "dotnet", "run", "--project", "tests/nextorm.integration.tests", "-c", "Debug", "--no-build", "--",
                "-list", "tests"]},
            {"name": "family-selection-required", "argv": [
                "python3", "-c",
                "import json,sys;json.load(open(%r))" % os.path.join(root, "families", code, "selection.json")]},
            {"name": "family-unit", "argv": None, "from_selection": "unit_filter"},
            {"name": "family-live", "argv": None, "from_selection": "live_classes"},
        ]
        for code, _label in FAMILIES
    }
    return manifest


def persist_manifest(root: str, lane: str, phase: str = "baseline"):
    manifest = command_manifest(root, lane, phase)
    write_json(os.path.join(root, "commands.json"), manifest)
    return manifest


# ---------------------------------------------------------------------------------------------
# Catalog row parsing and inventory construction
# ---------------------------------------------------------------------------------------------


def catalog_row_path(lane: str) -> str:
    return os.path.join(lane, CATALOG)


def parse_catalog_row(lane: str) -> dict:
    """Read the §3 ClickHouse cell of the catalog with a script (never by hand: 5.4 kB)."""
    path = catalog_row_path(lane)
    raw = open(path, "rb").read()
    lines = raw.split(b"\n")
    candidates = []
    for index, line in enumerate(lines):
        if line.startswith(b"| **ClickHouse** |"):
            candidates.append((index + 1, line))
    if len(candidates) != 4:
        raise RuntimeError("expected four ClickHouse rows (§1-§4), found %d in %s" % (len(candidates), path))
    # §1 :44, §2 :65, §3 :79, §4 :93 -- the residual row is the longest of the section rows.
    line_no, raw_line = max(candidates[1:], key=lambda item: len(item[1]))
    text = raw_line.decode("utf-8").rstrip("\r")
    sections = {}
    for index, (_, other) in enumerate(candidates):
        sections["section%d" % (index + 1)] = {
            "line": candidates[index][0],
            "bytes": len(other),
            "text": other.decode("utf-8").rstrip("\r"),
        }
    parts = re.split(r"\*\*([^*]+):\*\*", text)
    families = []
    for index in range(1, len(parts), 2):
        label = parts[index].strip()
        body = parts[index + 1]
        tokens = re.findall(r"`([^`]*)`", body)
        families.append({
            "family": FAMILY_OF.get(label),
            "label": label,
            "tokens": tokens,
            "token_count": len(tokens),
            "body": body,
            "has_bare_ellipsis": "…" in body,
        })
    return {
        "path": CATALOG,
        "line": line_no,
        "bytes": len(raw_line),
        "chars": len(text),
        "token_count": sum(len(family["tokens"]) for family in families),
        "family_count": len(families),
        "families": families,
        "sections": sections,
    }


def load_api_members(roslyn_text: str) -> dict:
    """ClickHouseFunctions members from the Roslyn member list captured by preflight (not a text search)."""
    names = []
    lines = [line for line in roslyn_text.splitlines() if line.strip()]
    for line in lines:
        match = re.search(r"ClickHouseFunctions\.([A-Za-z_][A-Za-z0-9_]*)", line)
        if match:
            names.append(match.group(1))
    return {
        "member_lines": len(lines),
        "method_count": sum(1 for line in lines if "public method" in line),
        "count": len(lines),
        "names": sorted(set(names)),
        "source": "roslynq members NextORM.Core.ClickHouseFunctions (preflight child)",
    }


def load_render_arm_names(lane: str) -> dict:
    """The dialect's scalar capability names (`Supports(...)`) and the Render switch arms."""
    text = read_text(os.path.join(lane, CH_DIALECT))
    class_at = text.find("class ClickHouseScalarFunctions")
    if class_at < 0:
        raise RuntimeError("ClickHouseScalarFunctions class not found in %s" % CH_DIALECT)
    start = text.find("public bool Supports(string name) => name is", class_at)
    if start < 0:
        raise RuntimeError("ClickHouseScalarFunctions.Supports not found in %s" % CH_DIALECT)
    end = text.find(";", start)
    block = text[start:end]
    names = re.findall(r'"([a-z0-9_]+)"', block)
    render_start = text.find("public string Render(string name, IReadOnlyList<string> args) => name switch", start)
    render_end = text.find("\n    }", render_start)
    render_block = text[render_start:render_end] if render_start >= 0 else ""
    arms = re.findall(r'^\s*"([a-z0-9_]+)"\s*=>', render_block, re.M)

    # The test-side manifest of arms (the source of the 81 pin) counted from its literal array.
    test_text = read_text(os.path.join(lane, CH_SCALAR_PINS))
    test_at = test_text.find("RenderArms =")
    test_block = test_text[test_at:test_text.find("];", test_at)] if test_at >= 0 else ""
    test_arms = re.findall(r'^\s*\("([A-Za-z0-9_]+)",', test_block, re.M)
    supported_at = test_text.find("SupportedNames =")
    supported_block = test_text[supported_at:test_text.find("];", supported_at)] if supported_at >= 0 else ""
    test_names = re.findall(r'"([A-Za-z0-9_]+)"', supported_block)

    return {
        "supports_count": len(names),
        "supports": names,
        "render_arm_count": len(arms),
        "render_arms": arms,
        "unsupported_arms": [arm for arm in arms if arm not in names],
        "test_render_arms": len(test_arms),
        "test_supported_names": len(test_names),
        "test_only_names": sorted(set(test_names) - set(names)),
        "dialect_only_names": sorted(set(names) - set(test_names)),
    }


def parse_scalar_pins(lane: str) -> dict:
    text = read_text(os.path.join(lane, CH_SCALAR_PINS))
    pins = {}
    for match in re.finditer(r"HaveCount\((\d+),\s*\"([^\"]*)\"", text):
        pins[match.group(1) + ":" + match.group(2)[:60]] = int(match.group(1))
    lines = {}
    for index, line in enumerate(text.splitlines(), 1):
        if "HaveCount(" in line and ("scalar-function names" in line or "pad forms" in line):
            lines[index] = line.strip()
    return {"pins": lines, "values": [int(v) for v in re.findall(r"HaveCount\((\d+)", text)], "path": CH_SCALAR_PINS}


# Explicit alias bindings resolved at D0: source token (normalized) -> API member (normalized).
# Every entry records a real binding this runner verified; nothing else is aliased.
ALIASES = {
    "median": "quantile",
    "uuitnumtostring": "uuid_num_to_string",
    "uuidnumtostring": "uuid_num_to_string",
    "uuidstringtonum": "uuid_string_to_num",
    "uuidtonum": "uuid_to_num",
    "uuidv7todatetime": "uuidv7_to_date_time",
    "uuidtov4": "uuidv4",
    "ulidstringtodatetime": "ulid_string_to_date_time",
    "multif": "multi_if",
    "ifnull": "if_null",
}

REFUSAL_CANDIDATES = {
    "untuple": "the function expands one tuple value into a dynamic set of columns; a scalar CLR return type cannot name or declare that result shape",
    "flattenTuple": "the result column set and element types are derived from the runtime tuple type; no CLR projection can express it",
    "arrayReduce": "the aggregate is named by a runtime SQL string argument; there is no typed aggregate/lambda contract that can be closed by a signature",
    "arrayReduceInRanges": "same as arrayReduce: the aggregate name is a runtime string argument, not a typed contract",
    "toIntervalNanosecond": "returns the ClickHouse-only Interval type (a tagged interval usable only inside date arithmetic); no CLR type in the surface represents it",
    "toIntervalMicrosecond": "returns the ClickHouse-only Interval type; no CLR counterpart",
    "toIntervalMillisecond": "returns the ClickHouse-only Interval type; no CLR counterpart",
    "toIntervalSecond": "returns the ClickHouse-only Interval type; no CLR counterpart",
    "toIntervalMinute": "returns the ClickHouse-only Interval type; no CLR counterpart",
    "toIntervalHour": "returns the ClickHouse-only Interval type; no CLR counterpart",
    "toIntervalDay": "returns the ClickHouse-only Interval type; no CLR counterpart",
    "toIntervalWeek": "returns the ClickHouse-only Interval type; no CLR counterpart",
    "toIntervalMonth": "returns the ClickHouse-only Interval type; no CLR counterpart",
    "toIntervalQuarter": "returns the ClickHouse-only Interval type; no CLR counterpart",
    "toIntervalYear": "returns the ClickHouse-only Interval type; no CLR counterpart",
    "toDecimal256": "Decimal256 has a 256-bit mantissa; System.Decimal is 96-bit and cannot represent the value without silent precision loss",
    "stochasticLinearRegression": "returns an AggregateFunction state (opaque binary); only its -Merge form yields a tuple and the state itself has no CLR representation",
    "stochasticLogisticRegression": "returns an AggregateFunction state (opaque binary); no CLR representation",
    "exponentialMovingAverage": "returns an AggregateFunction state (opaque binary); no CLR representation",
    "histogram": "returns a dynamic Tuple(Array(T), Array(UInt64)) whose element types follow the input type; not expressible as a single typed CLR projection",
}

PORTABLE_ANCHORS = {
    "plus": "CLR + is translated by NormSqlTranslator on every provider",
    "minus": "CLR - is translated by NormSqlTranslator on every provider",
    "multiply": "CLR * is translated by NormSqlTranslator on every provider",
    "divide": "CLR / is translated by NormSqlTranslator on every provider",
    "negate": "CLR unary - is translated by NormSqlTranslator on every provider",
    "e": "Math.E constant; the portable surface already carries the constant, no provider function is required",
    "toString": "the row itself records that numeric/date casts are reachable through the CLR cast translation",
    "toBool": "bool compare/convert is reachable through the CLR conversion path recorded in the row",
    "toFixedString": "fixed-width string is not a portable CLR shape; the CLR string is the portable equivalent",
}
# The cell states that numeric/date casts are reachable through the CLR cast: the numeric cast
# expansions are therefore candidates for the portable path, not ClickHouse-only by default.
for _cast in (
    "toInt8", "toInt16", "toInt32", "toInt64", "toInt128", "toInt256",
    "toUInt8", "toUInt16", "toUInt32", "toUInt64", "toUInt128", "toUInt256",
    "toFloat32", "toFloat64", "toDecimal32", "toDecimal64", "toDecimal128",
):
    PORTABLE_ANCHORS[_cast] = (
        "the cell records numeric casts as reachable through the CLR cast; verify that the "
        "ClickHouse cast translation emits %s before reusing it" % _cast
    )


def build_inventory(lane: str, api: dict, arms: dict, pins: dict) -> dict:
    row = parse_catalog_row(lane)
    api_keys = {normalize(name): name for name in api["names"]}
    supports_keys = {normalize(name): name for name in arms["supports"]}

    def covered_by(canonical: str):
        key = normalize(ALIASES.get(normalize(canonical), canonical))
        reasons = []
        if key in api_keys:
            reasons.append("ClickHouseFunctions." + api_keys[key])
        if key in supports_keys:
            reasons.append("ClickHouseScalarFunctions.Supports(%r)" % supports_keys[key])
        return reasons

    members_by_family: dict[str, list] = {}
    for family in row["families"]:
        members_by_family[family["family"]] = []

    marker_tokens = {"64", "Ranked", "WithTypes", "Position", "URL"}
    host_tokens = {token for entry in EXPANSIONS for token in entry["tokens"] if "*" in token or "…" in token}
    host_expansion = {}
    for entry in EXPANSIONS:
        for token in entry["tokens"]:
            if token in host_tokens:
                host_expansion.setdefault(token, []).append(entry["id"])

    sequence = 0
    literal_members: dict[str, dict] = {}
    token_accounting = []
    for family in row["families"]:
        code = family["family"]
        if code is None:
            raise RuntimeError("catalog family %r has no stable code" % family["label"])
        for token in family["tokens"]:
            if token in host_tokens:
                token_accounting.append({
                    "token": token, "family": code, "kind": "wildcard-host",
                    "expansions": host_expansion.get(token, []), "member_ids": [],
                })
                continue
            if token in marker_tokens:
                token_accounting.append({
                    "token": token, "family": code, "kind": "variant-marker",
                    "expansions": [], "member_ids": [],
                })
                continue
            sequence += 1
            member = {
                "id": "%s-M%04d" % (code, sequence),
                "family": code,
                "family_label": family["label"],
                "canonical": token,
                "source_token": token,
                "expansion_id": None,
                "kind": "literal",
                "status": None,
                "status_basis": "",
                "covered_by": [],
                "variants": [],
            }
            literal_members[token] = member
            members_by_family[code].append(member)
            token_accounting.append({
                "token": token, "family": code, "kind": "literal",
                "expansions": [], "member_ids": [member["id"]],
            })

    # Bind wildcard expansions; each expansion member becomes a concrete member (or binds to its
    # literal token when the cell already spells it out).
    unresolved = []
    for entry in EXPANSIONS:
        code = entry["family"]
        explicit = set(entry.get("explicit") or [])
        for name in entry["members"]:
            if name in explicit and name in literal_members and literal_members[name]["family"] == code:
                target = literal_members[name]
                target["expansion_id"] = entry["id"]
                target["kind"] = "literal+expansion"
                continue
            sequence += 1
            member = {
                "id": "%s-M%04d" % (code, sequence),
                "family": code,
                "family_label": dict(FAMILIES)[code],
                "canonical": name,
                "source_token": "|".join(entry["tokens"]),
                "expansion_id": entry["id"],
                "kind": "expansion",
                "status": None,
                "status_basis": entry["basis"],
                "covered_by": [],
                "variants": [],
            }
            members_by_family[code].append(member)
        landed = {m["canonical"] for m in members_by_family[code]}
        missing = [name for name in entry["members"] if name not in landed]
        if missing:
            unresolved.append({"expansion": entry["id"], "missing": missing})
        for token in entry["tokens"]:
            if token in host_tokens:
                for record in token_accounting:
                    if record["token"] == token and record["family"] == code:
                        record["member_ids"].append(entry["id"])

    # Classify.
    for code, members in members_by_family.items():
        for member in members:
            canonical = member["canonical"]
            reasons = covered_by(canonical)
            if reasons:
                member["status"] = "already-covered"
                member["covered_by"] = reasons
                member["status_basis"] = "bound to the shipped member/arm surface at %s" % GROUP_SHA[:8]
            elif canonical in REFUSAL_CANDIDATES:
                member["status"] = "candidate-refusal"
                member["status_basis"] = REFUSAL_CANDIDATES[canonical]
            elif canonical in PORTABLE_ANCHORS:
                member["status"] = "candidate-portable-equivalent"
                member["status_basis"] = PORTABLE_ANCHORS[canonical]
            else:
                member["status"] = "candidate-clickhouse-only"
                member["status_basis"] = "no shipped member/arm emits %r; no verified portable equivalent at D0" % canonical
            member["variants"] = [
                {"id": "%s.%s" % (member["id"], dim), "dimension": label, "state": "open"}
                for dim in FAMILY_DIMENSIONS[code]
                for dim_label in [dict(DIMENSIONS)[dim]]
                for label in [dim_label]
            ]

    # §3 -> §1 comparison: a member that is already covered but still published in §3 is drift.
    section1 = row["sections"]["section1"]["text"]
    section3 = row["sections"]["section3"]["text"]
    s1_tokens = set(re.findall(r"`([^`]*)`", section1))
    drift = []
    for code, members in members_by_family.items():
        for member in members:
            if member["status"] == "already-covered" and member["canonical"] in section3:
                drift.append({"id": member["id"], "canonical": member["canonical"], "family": code})

    per_family = {}
    for code, label in FAMILIES:
        members = members_by_family[code]
        source = next((f for f in row["families"] if f["family"] == code), None)
        per_family[code] = {
            "label": label,
            "token_count": source["token_count"] if source else 0,
            "source_tokens": source["tokens"] if source else [],
            "cell_body": source["body"].strip() if source else "",
            "member_count": len(members),
            "literal_members": sum(1 for m in members if m["kind"].startswith("literal")),
            "expansion_members": sum(1 for m in members if m["kind"] == "expansion"),
            "by_status": {},
        }
        if code == "F19":
            per_family[code]["boundary"] = True
            per_family[code]["note"] = (
                "the cell carries no function tokens: it states that the external-engine family is out "
                "of scope (section 4). D19 must prove that no §3 member was excluded by this label."
            )
        for member in members:
            per_family[code]["by_status"][member["status"]] = per_family[code]["by_status"].get(member["status"], 0) + 1

    expansion_records = []
    for entry in EXPANSIONS:
        expansion_records.append({
            "id": entry["id"],
            "family": entry["family"],
            "tokens": entry["tokens"],
            "basis": entry["basis"],
            "members": entry["members"],
            "member_count": len(entry["members"]),
            "unbound": [name for name in entry["members"] if name not in {m["canonical"] for m in members_by_family[entry["family"]]}],
        })

    wildcard_tokens = sorted({
        token
        for family in row["families"]
        for token in family["tokens"]
        if "*" in token or "…" in token
    })
    marker_tokens_present = sorted({
        token for family in row["families"] for token in family["tokens"] if token in marker_tokens
    })

    inventory = {
        "task": TASK,
        "rv": "rv1",
        "source_revision": GROUP_SHA,
        "catalog": {
            "path": row["path"],
            "line": row["line"],
            "bytes": row["bytes"],
            "chars": row["chars"],
            "token_count": row["token_count"],
            "family_count": row["family_count"],
            "section_lines": {key: value["line"] for key, value in row["sections"].items()},
            "section_bytes": {key: value["bytes"] for key, value in row["sections"].items()},
        },
        "accounting": {
            "source_tokens": row["token_count"],
            "tokens_accounted": len(token_accounting),
            "literal_tokens": sum(1 for record in token_accounting if record["kind"] == "literal"),
            "wildcard_host_tokens": sum(1 for record in token_accounting if record["kind"] == "wildcard-host"),
            "variant_marker_tokens": marker_tokens_present,
            "families": row["family_count"],
            "wildcard_entries": len(EXPANSIONS),
            "wildcard_entry_claim": WILDCARD_ENTRY_COUNT_CLAIM,
            "wildcard_source_tokens": wildcard_tokens,
            "wildcard_source_token_count": len(wildcard_tokens),
            "members_total": sum(len(members) for members in members_by_family.values()),
            "unresolved_expansions": len(unresolved),
            "unresolved_detail": unresolved,
            "zero_token_families": [code for code, label in FAMILIES
                                    if per_family[code]["token_count"] == 0],
            "unaccounted_tokens": row["token_count"] - len(token_accounting),
        },
        "token_accounting": token_accounting,
        "cross_checks": {
            "roslyn_member_lines": api["member_lines"],
            "api_member_names": len(api["names"]),
            "dialect_supports_names": arms["supports_count"],
            "dialect_render_arms": arms["render_arm_count"],
            "dialect_unsupported_arms": arms["unsupported_arms"],
            "test_supported_names": arms["test_supported_names"],
            "test_render_arms": arms["test_render_arms"],
            "test_only_names": arms["test_only_names"],
            "dialect_only_names": arms["dialect_only_names"],
            "scalar_pin_values": pins["values"],
            "scalar_pin_lines": {str(k): v for k, v in pins["pins"].items()},
            "expected": {"api_members": 205, "scalar_names": 79, "scalar_arms": 81},
            "reconciliation": "Roslyn lists 205 member lines (class + 203 public methods + nested MultiIfBranch<T>); the dialect accepts 79 scalar names (79 test SupportedNames) and the test manifest carries 81 arms = 79 names + the two optional pad forms; the Render switch has 80 accepted arms because array_except is a defensive refusal arm (#252).",
        },
        "per_family": per_family,
        "expansions": expansion_records,
        "catalog_drift": drift,
        "members": {code: members for code, members in members_by_family.items()},
        "status_legend": {
            "already-covered": "a shipped ClickHouseFunctions member or ClickHouseScalarFunctions.Supports name emits it today",
            "candidate-portable-equivalent": "not shipped; semantics are reachable through an existing portable member/CLR translation recorded in status_basis",
            "candidate-clickhouse-only": "not shipped and no verified portable equivalent; the default delivery location",
            "candidate-refusal": "not shipped; a concrete CLR-shape limitation is recorded in status_basis and must be proved by a negative test",
        },
        "variant_dimensions": [{"id": dim, "dimension": label} for dim, label in DIMENSIONS],
    }
    return inventory


def inventory_markdown(inventory: dict) -> str:
    lines = []
    add = lines.append
    add("# T286b rv1 -- §3 ClickHouse residual inventory (D0/E00)")
    add("")
    add("- source revision: `%s`" % inventory["source_revision"])
    add("- catalog cell: `%s:%s`, **%s bytes**, **%s tokens**, **%s families**"
        % (inventory["catalog"]["path"], inventory["catalog"]["line"],
           inventory["catalog"]["bytes"], inventory["catalog"]["token_count"],
           inventory["catalog"]["family_count"]))
    accounting = inventory["accounting"]
    add("- wildcard/ellipsis entries expanded: **%s** (plan claim %s); wildcard-bearing source tokens: **%s**; "
        "variant-marker tokens: %s"
        % (accounting["wildcard_entries"], accounting["wildcard_entry_claim"],
           accounting["wildcard_source_token_count"], ", ".join("`%s`" % t for t in accounting["variant_marker_tokens"])))
    add("- concrete members: **%s** (%s literal + %s expansion); **unresolved expansions: %s**"
        % (accounting["members_total"],
           sum(v["literal_members"] for v in inventory["per_family"].values()),
           sum(v["expansion_members"] for v in inventory["per_family"].values()),
           accounting["unresolved_expansions"]))
    cross = inventory["cross_checks"]
    add("- cross-checks: `ClickHouseFunctions` %s member lines (%s names); dialect `Supports` %s names, %s render arms"
        % (cross["roslyn_member_lines"], cross["api_member_names"],
           cross["dialect_supports_names"], cross["dialect_render_arms"]))
    add("- scalar pins: %s" % ", ".join(str(v) for v in cross["scalar_pin_values"]))
    add("")
    add("## Per-family counts (counted from the cell, not estimated)")
    add("")
    add("| ID | Family | Source tokens | Members | literal | expansion | already-covered | portable-equivalent | clickhouse-only | refusal |")
    add("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|")
    for code, label in FAMILIES:
        row = inventory["per_family"][code]
        by = row["by_status"]
        add("| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |"
            % (code, label, row["token_count"], row["member_count"], row["literal_members"],
               row["expansion_members"], by.get("already-covered", 0),
               by.get("candidate-portable-equivalent", 0),
               by.get("candidate-clickhouse-only", 0), by.get("candidate-refusal", 0)))
    add("")
    add("## Wildcard/ellipsis expansions")
    add("")
    add("| Entry | Family | Source token(s) | Members | Expansion basis |")
    add("|---|---|---|---:|---|")
    for entry in inventory["expansions"]:
        add("| %s | %s | %s | %s | %s |"
            % (entry["id"], entry["family"], ", ".join("`%s`" % t for t in entry["tokens"]),
               entry["member_count"], entry["basis"]))
    add("")
    add("## Status rules applied at D0")
    add("")
    for status, meaning in inventory["status_legend"].items():
        add("- `%s` -- %s" % (status, meaning))
    add("")
    add("Member IDs are family-scoped and stable (`<family>-M<seq>`); variant slots are "
        "`<member>.<Vnn>` over the plan §13 dimensions and are assigned, not executed, at D0.")
    add("")
    if inventory["catalog_drift"]:
        add("### Catalog drift (implemented but still listed in §3)")
        add("")
        for item in inventory["catalog_drift"]:
            add("- `%s` (%s, %s)" % (item["canonical"], item["id"], item["family"]))
        add("")
    add("### Members")
    add("")
    for code, label in FAMILIES:
        members = inventory["members"][code]
        add("#### %s %s (%s members)" % (code, label, len(members)))
        add("")
        add("| ID | Canonical | Kind | Status | Basis | Variants |")
        add("|---|---|---|---|---|---:|")
        for member in members:
            add("| %s | `%s` | %s | %s | %s | %s |"
                % (member["id"], member["canonical"], member["kind"], member["status"],
                   member["status_basis"].replace("|", "\\|"), len(member["variants"])))
        add("")
    return "\n".join(lines) + "\n"


# ---------------------------------------------------------------------------------------------
# Docker API over the Podman unix socket (used only by the D0 metadata probes)
# ---------------------------------------------------------------------------------------------


class UnixHTTPConnection(http.client.HTTPConnection):
    def __init__(self, socket_path: str, timeout: int = 120):
        super().__init__("localhost", timeout=timeout)
        self.socket_path = socket_path

    def connect(self):
        sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        sock.settimeout(self.timeout)
        sock.connect(self.socket_path)
        self.sock = sock


def docker_api(method: str, path: str, body=None, timeout: int = 120):
    payload = json.dumps(body).encode() if body is not None else None
    conn = UnixHTTPConnection(DOCKER_SOCKET, timeout=timeout)
    headers = {"Content-Type": "application/json"} if payload is not None else {}
    conn.request(method, "/v1.40" + path, body=payload, headers=headers)
    response = conn.getresponse()
    data = response.read()
    conn.close()
    return response.status, data


def docker_exec(container: str, cmd: list, timeout: int = 120) -> dict:
    status, body = docker_api("POST", "/containers/%s/exec" % container,
                              {"Cmd": cmd, "AttachStdout": True, "AttachStderr": True, "Tty": True}, timeout)
    if status != 201:
        return {"argv": cmd, "exit": -1, "output": "exec-create failed http %s: %s" % (status, body[:400].decode("utf-8", "replace"))}
    exec_id = json.loads(body.decode())["Id"]
    conn = UnixHTTPConnection(DOCKER_SOCKET, timeout=timeout)
    conn.request("POST", "/v1.40/exec/%s/start" % exec_id,
                 body=json.dumps({"Detach": False, "Tty": True}).encode(),
                 headers={"Content-Type": "application/json"})
    response = conn.getresponse()
    data = response.read()
    conn.close()
    inspect_status, inspect_body = docker_api("GET", "/exec/%s/json" % exec_id)
    exit_code = -1
    if inspect_status == 200:
        exit_code = json.loads(inspect_body.decode()).get("ExitCode", -1)
    return {"argv": cmd, "exit": exit_code, "output": data.decode("utf-8", "replace")}


def docker_ping_probe() -> dict:
    started_at = utc_now()
    started = time.monotonic()
    transcript = []
    exit_code = 0
    try:
        status, body = docker_api("GET", "/_ping", timeout=30)
        transcript.append("GET /_ping -> %s %s" % (status, body.decode("utf-8", "replace").strip()))
        if status != 200:
            exit_code = 1
    except Exception as exc:  # noqa: BLE001
        exit_code = 1
        transcript.append("GET /_ping -> EXCEPTION %s" % exc)
    return {
        "argv": ["docker-api", "GET", "/_ping"],
        "started_at": started_at, "ended_at": utc_now(),
        "duration_s": round(time.monotonic() - started, 3),
        "state": "finished" if exit_code == 0 else "failed",
        "exit": exit_code, "reason": "" if exit_code == 0 else "socket/API not reachable",
        "transcript": "\n".join(transcript) + "\n",
    }


def docker_image_probe(image: str) -> dict:
    started_at = utc_now()
    started = time.monotonic()
    transcript = []
    exit_code = 0
    identity = {}
    try:
        status, body = docker_api("GET", "/images/%s/json" % image, timeout=60)
        transcript.append("GET /images/%s/json -> %s" % (image, status))
        if status == 200:
            identity = json.loads(body.decode())
            transcript.append(json.dumps({key: identity.get(key) for key in ("Id", "RepoTags", "RepoDigests")}))
        else:
            exit_code = 1
            transcript.append(body[:400].decode("utf-8", "replace"))
    except Exception as exc:  # noqa: BLE001
        exit_code = 1
        transcript.append("GET /images/%s/json -> EXCEPTION %s" % (image, exc))
    probe = {
        "argv": ["docker-api", "GET", "/images/%s/json" % image],
        "started_at": started_at, "ended_at": utc_now(),
        "duration_s": round(time.monotonic() - started, 3),
        "state": "finished" if exit_code == 0 else "failed",
        "exit": exit_code, "reason": "" if exit_code == 0 else "image not present",
        "transcript": "\n".join(transcript) + "\n",
    }
    probe["identity"] = identity
    return probe


def dictionary_fixture_probe() -> dict:
    """Prove the dictionary fixture capability: create/insert/dictGet/drop on the pinned image."""
    started_at = utc_now()
    started = time.monotonic()
    transcript = []
    exit_code = 0
    reason = ""
    container = None
    name = "t286b-dict-probe-%d" % int(time.time())

    def log(line: str):
        transcript.append(line)

    try:
        status, body = docker_api("GET", "/_ping", timeout=30)
        log("GET /_ping -> %s %s" % (status, body.decode("utf-8", "replace").strip()))

        status, body = docker_api("GET", "/images/%s/json" % CLICKHOUSE_IMAGE, timeout=60)
        image = json.loads(body.decode()) if status == 200 else {}
        log("GET /images/%s/json -> %s id=%s" % (CLICKHOUSE_IMAGE, status, image.get("Id")))

        status, body = docker_api("POST", "/containers/create?name=%s" % name, {
            "Image": CLICKHOUSE_IMAGE,
            "Env": ["CLICKHOUSE_SKIP_USER_SETUP=1"],
            "HostConfig": {"AutoRemove": False},
        }, timeout=120)
        if status not in (201, 200):
            raise RuntimeError("container create failed http %s: %s" % (status, body[:400].decode("utf-8", "replace")))
        container = json.loads(body.decode())["Id"]
        log("POST /containers/create -> %s id=%s" % (status, container))

        status, body = docker_api("POST", "/containers/%s/start" % container, timeout=120)
        log("POST /containers/%s/start -> %s" % (container, status))
        if status not in (204, 304):
            raise RuntimeError("container start failed http %s: %s" % (status, body[:400].decode("utf-8", "replace")))

        ready = False
        for attempt in range(60):
            probe = docker_exec(container, ["clickhouse-client", "--query", "SELECT 1"], timeout=30)
            if probe["exit"] == 0 and "1" in probe["output"]:
                ready = True
                log("readiness %d: clickhouse-client SELECT 1 -> 0" % attempt)
                break
            time.sleep(1)
        if not ready:
            raise RuntimeError("clickhouse-server did not become ready within 60s")

        version = docker_exec(container, ["clickhouse-client", "--query",
                                          "SELECT version(), timezone(), currentDatabase() FORMAT TSV"], timeout=60)
        log("version probe exit=%s output=%r" % (version["exit"], version["output"]))
        if version["exit"] != 0:
            raise RuntimeError("version probe failed: %r" % version["output"][:400])

        setup_sql = (
            "CREATE DATABASE IF NOT EXISTS t286b ENGINE = Atomic;"
            "CREATE TABLE t286b.dict_src (id UInt64, val String) ENGINE = MergeTree ORDER BY id;"
            "INSERT INTO t286b.dict_src VALUES (1, 'alpha'), (2, 'beta');"
            "CREATE DICTIONARY t286b.dict (id UInt64, val String) PRIMARY KEY id "
            "SOURCE(CLICKHOUSE(HOST 'localhost' PORT 9000 USER 'default' DB 't286b' TABLE 'dict_src')) "
            "LAYOUT(HASHED()) LIFETIME(0);"
            "SYSTEM RELOAD DICTIONARY t286b.dict;"
        )
        setup = docker_exec(container, ["clickhouse-client", "--multiquery", "--query", setup_sql], timeout=120)
        log("create/insert/reload exit=%s output=%r" % (setup["exit"], setup["output"]))
        if setup["exit"] != 0:
            raise RuntimeError("dictionary fixture setup failed: %r" % setup["output"][:800])

        lookup = docker_exec(container, ["clickhouse-client", "--query",
                                         "SELECT dictGet('t286b.dict', 'val', toUInt64(2))"], timeout=60)
        log("dictGet exit=%s output=%r" % (lookup["exit"], lookup["output"]))
        if lookup["exit"] != 0 or "beta" not in lookup["output"]:
            raise RuntimeError("dictGet returned %r (expected 'beta')" % lookup["output"][:400])

        teardown = docker_exec(container, ["clickhouse-client", "--multiquery", "--query",
                                           "DROP DICTIONARY IF EXISTS t286b.dict;"
                                           "DROP TABLE IF EXISTS t286b.dict_src;"
                                           "DROP DATABASE IF EXISTS t286b;"], timeout=120)
        log("drop exit=%s output=%r" % (teardown["exit"], teardown["output"]))
        if teardown["exit"] != 0:
            raise RuntimeError("dictionary fixture teardown failed: %r" % teardown["output"][:800])

        log("RESULT dictionary fixture capability = available (create/insert/dictGet(2)='beta'/drop all rc 0)")
    except Exception as exc:  # noqa: BLE001 - the probe must report, never raise past the runner
        exit_code = 1
        reason = str(exc)
        log("RESULT dictionary fixture capability = FAILED: %s" % reason)
    finally:
        if container:
            status, body = docker_api("POST", "/containers/%s/stop" % container, timeout=120)
            log("POST /containers/%s/stop -> %s %s" % (container, status, body[:120].decode("utf-8", "replace")))
            status, body = docker_api("DELETE", "/containers/%s?force=1" % container, timeout=120)
            log("DELETE /containers/%s -> %s" % (container, status))

    return {
        "argv": ["docker-api", "run-dictionary-fixture", CLICKHOUSE_IMAGE],
        "image": CLICKHOUSE_IMAGE,
        "started_at": started_at,
        "ended_at": utc_now(),
        "duration_s": round(time.monotonic() - started, 3),
        "state": "finished" if exit_code == 0 else "failed",
        "exit": exit_code,
        "reason": reason,
        "transcript": "\n".join(transcript) + "\n",
        "capability": "available" if exit_code == 0 else "unavailable",
        "container": container,
    }


# ---------------------------------------------------------------------------------------------
# Frozen test-scope / matrix / benchmark manifests (plan §6, §12, §15)
# ---------------------------------------------------------------------------------------------


def test_selection(root: str, lane: str, inventory: dict) -> dict:
    affected = [project for project in PROVIDER_SQL_PROJECTS if os.path.isdir(os.path.join(lane, project))]
    return {
        "rv": "rv1",
        "configuration": "Debug",
        "solution": os.path.join(MAIN_TREE, "nextorm.slnx"),
        "worktree": lane,
        "execution": {
            "scopedBuildsOnly": True,
            "buildParallelism": 1,
            "selectionManifest": os.path.join(root, "test-selection.json"),
            "zeroSelectedTestsAllowed": False,
            "listingArguments": ["-list", "tests"],
            "containerLease": LEASE,
        },
        "unit": [{"project": "tests/nextorm.core.tests", "selection": "all", "required": True},
                 {"project": "tests/nextorm.clickhouse.tests", "selection": "all", "required": True}],
        "providerSql": [{"project": project, "selection": "all", "required": True} for project in affected],
        "integration": {
            "project": "tests/nextorm.integration.tests",
            "selection": "all",
            "required": True,
            "realDatabaseRequired": True,
            "requiredProviders": ["sqlite", "postgres", "sqlserver", "mysql", "clickhouse"],
            "requiredSupportedCellsSkipped": 0,
            "providerOptOutsAllowed": False,
            "dockerHost": DOCKER_HOST,
            "collectionLease": LEASE,
            "memberMatrix": os.path.join(root, "provider-family-matrix.json"),
            "skipBudget": "python3 .pdca/scripts/check-integration-skip-budget.py --log <run log>",
        },
        "benchmark": {
            "required": True,
            "configuration": "Release",
            "hostLock": "/tmp/opencode/nextorm-bdn-host.lock",
            "scenarioManifest": os.path.join(root, "benchmark-scenarios.json"),
            "filter": "*ClickHouseTranslationBenchmark*",
        },
        "familySelection": {
            "manifest": os.path.join(root, "families", "<FNN>", "selection.json"),
            "note": "every family row requires its own selection.json produced by the family task; the runner refuses a family run without it",
        },
        "inventoryMembers": inventory["accounting"]["members_total"],
    }


PROVIDER_MATRIX = (
    # family, ClickHouse, SQLite, PostgreSQL, SQL Server, MySQL/MariaDB
    ("F01", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F02", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F03", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F04", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F05", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F06", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F07", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F08", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F09", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F10", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F11", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F12", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F13", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F14", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F15", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F16", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F17", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F18", "CH", "G/P", "G/P", "G/P", "G/P"),
    ("F19", "B", "B", "B", "B", "B"),
)


def provider_family_matrix(inventory: dict) -> dict:
    families = []
    for code, ch, sqlite, postgres, sqlserver, mysql in PROVIDER_MATRIX:
        per = inventory["per_family"][code]
        families.append({
            "family": code,
            "label": per["label"],
            "members": per["member_count"],
            "supported_members": per["by_status"].get("already-covered", 0)
            + per["by_status"].get("candidate-portable-equivalent", 0),
            "refusal_members": per["by_status"].get("candidate-refusal", 0),
            "cells": {
                "clickhouse": ch,
                "sqlite": sqlite,
                "postgres": postgres,
                "sqlserver": sqlserver,
                "mysql_mariadb": mysql,
            },
        })
    return {
        "rv": "rv1",
        "source": os.path.join(MAIN_TREE, ".pdca", TASK, "rv1", "inventory.json"),
        "codes": {
            "CH": "execute every supported concrete member against real ClickHouse; test every justified refusal",
            "G/P": "test the ClickHouse-only provider guard; execute existing portable equivalents affected by this change",
            "B": "verify the §3/§4 boundary and retain any §3 overlap",
        },
        "families": families,
    }


def benchmark_scenarios(root: str, lane: str, inventory: dict) -> dict:
    return {
        "rv": "rv1",
        "task": TASK,
        "issue": "#286",
        "required": True,
        "configuration": "Release",
        "baseline": "integrated post-T276 lane (%s) before T286b feature edits" % GROUP_SHA[:8],
        "candidate": "the same scenarios re-measured after the feature edits",
        "hostLock": "/tmp/opencode/nextorm-bdn-host.lock",
        "command": "python3 .pdca/scripts/T286b-evidence.py benchmark --rv rv1 --phase %s",
        "bdn": {
            "project": "benchmarks/nextorm.benchmark",
            "filter": "*ClickHouseTranslationBenchmark*",
            "artifacts": os.path.join(root, "performance", "<phase>", "bdn"),
            "config": "NextormConfig (Job.ShortRun + InProcessEmitToolchain unless NEXTORM_BENCH_FULL=1)",
            "warmup": 3,
            "iterations": 3,
            "database_executed": False,
            "separation": "every arm builds a QueryCommand and calls GetPreparedQueryCommand; the rendered CommandText length is the sink, so database time is zero by construction",
        },
        "scenarios": [
            {"id": "S1-scalar", "plan": "translation and preparation of representative scalar families",
             "methods": ["ScalarFamily_Prepare", "ScalarRender_DirectArms"],
             "source": "benchmarks/nextorm.benchmark/ClickHouse/ClickHouseTranslationBenchmark.cs"},
            {"id": "S2-higher-order", "plan": "array/map higher-order translation",
             "methods": ["ArrayHigherOrder_Prepare", "Map_Prepare"],
             "source": "benchmarks/nextorm.benchmark/ClickHouse/ClickHouseTranslationBenchmark.cs"},
            {"id": "S3-aggregate-window", "plan": "aggregate and window translation",
             "methods": ["AggregateWindow_Prepare"],
             "source": "benchmarks/nextorm.benchmark/ClickHouse/ClickHouseTranslationBenchmark.cs"},
            {"id": "S4-dictionary", "plan": "dictionary translation",
             "methods": ["Dictionary_Prepare"],
             "source": "benchmarks/nextorm.benchmark/ClickHouse/ClickHouseTranslationBenchmark.cs"},
            {"id": "S5-control", "plan": "existing unaffected query preparation/cache control",
             "methods": ["Control_PlainProjection_Prepare", "Control_CachedPlan_Prepare"],
             "source": "benchmarks/nextorm.benchmark/ClickHouse/ClickHouseTranslationBenchmark.cs"},
        ],
        "inventory_members": inventory["accounting"]["members_total"],
        "acceptance": "a repeatable regression outside baseline noise requires investigation and remediation; the plan invents no percentage threshold",
    }


# ---------------------------------------------------------------------------------------------
# Subcommands
# ---------------------------------------------------------------------------------------------


def cmd_preflight(args, root: str, lane: str) -> dict:
    directory = ensure_dir(os.path.join(root, "preflight"))
    persist_manifest(root, lane, args.phase)
    row = Row("preflight", directory, root, lane)
    env = build_env()
    git_env = dict(env)
    git_env["GIT_OPTIONAL_LOCKS"] = "0"

    head = row.run(["git", "-C", lane, "rev-parse", "HEAD"], env=git_env, name="git-head")
    group = row.run(["git", "-C", lane, "rev-parse", GROUP_BRANCH], env=git_env, name="git-group")
    status = row.run(["git", "-C", lane, "status", "--porcelain"], env=git_env, name="git-status")
    branch = row.run(["git", "-C", lane, "rev-parse", "--abbrev-ref", "HEAD"], env=git_env, name="git-branch")
    log = row.run(["git", "-C", lane, "log", "--no-walk", "--oneline"] + list(DELTA_HISTORY),
                  env=git_env, name="git-log-delta-history")
    main_head = row.run(["git", "-C", MAIN_TREE, "rev-parse", "HEAD"], env=git_env, name="git-main-head")

    head_sha = head["stdout"].strip()
    group_sha = group["stdout"].strip()
    branch_name = branch["stdout"].strip()
    main_sha = main_head["stdout"].strip()

    allowed_untracked = {
        ".pdca/status/T279-1.md": "pre-existing allowance recorded by the dispatch brief (untouched)",
        ".pdca/status/T276-1.md": "predecessor T276 status record, integrated at d20b91e3; same content as the main-tree copy (untouched)",
        ".pdca/scripts/T286b-evidence.py": "task-owned evidence runner (staged at ACT)",
    }
    allowed_untracked_prefixes = {
        "benchmarks/nextorm.benchmark/ClickHouse/": "task-owned baseline benchmark source (E25)",
    }
    modified = []
    unexpected = []
    untracked = []
    for line in status["stdout"].splitlines():
        if not line.strip():
            continue
        if line.startswith("?? "):
            path = line[3:].strip()
            untracked.append(path)
            if path in allowed_untracked:
                continue
            if any(path.startswith(prefix) for prefix in allowed_untracked_prefixes):
                continue
            unexpected.append(path)
        else:
            modified.append(line)

    dependency_ok = group_sha == GROUP_SHA and head_sha == GROUP_SHA and not modified and not unexpected
    if group_sha != GROUP_SHA:
        row.fail("lane %s is at %s, expected %s" % (GROUP_BRANCH, group_sha, GROUP_SHA))
    if head_sha != GROUP_SHA:
        row.fail("lane HEAD is %s, expected the group tip %s before the first T286b edit" % (head_sha, GROUP_SHA))
    if modified:
        row.fail("tracked files are modified in the lane: %s" % modified)
    if unexpected:
        row.fail("unexpected untracked entries in the lane: %s" % unexpected)
    if branch_name != TASK_BRANCH:
        row.fail("lane is on %r, expected the task branch %r" % (branch_name, TASK_BRANCH))

    # Delta history (plan §2 baseline + intervening predecessor changes).
    history = []
    for line in log["stdout"].splitlines():
        parts = line.split(" ", 1)
        if len(parts) == 2:
            history.append({"sha": parts[0], "subject": parts[1]})

    # Roslyn member list, captured into the snapshot directory (not a text search: `roslynq`).
    roslyn = row.run(["roslynq", "nextorm.slnx", "members", "NextORM.Core.ClickHouseFunctions"],
                     timeout_s=900, name="roslyn-members")
    row.run(["roslynq", "nextorm.slnx", "members", "NextORM.ClickHouse.ClickHouseScalarFunctions"],
            timeout_s=900, name="roslyn-members-scalars")

    # Source snapshots (git show dumps at the current sha; task-owned new files from the worktree).
    snapshots_dir = ensure_dir(os.path.join(root, "preflight", "source-snapshots"))
    snapshot_records = []
    for path in SNAPSHOT_PATHS:
        show = row.run(["git", "-C", lane, "show", "%s:%s" % (GROUP_SHA, path)],
                       env=git_env, name="snapshot-" + path.replace("/", "_"), require_zero=False)
        origin = "git-show"
        content = show["stdout"]
        if show["exit"] != 0:
            worktree_path = os.path.join(lane, path)
            if os.path.exists(worktree_path):
                content = read_text(worktree_path)
                origin = "worktree"
                show["state"] = "finished"
            else:
                row.fail("snapshot unavailable for %s (git show rc=%s, no worktree file)"
                         % (path, show["exit"]))
                continue
        target = os.path.join(snapshots_dir, path.replace("/", "__"))
        with open(target, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(content)
        snapshot_records.append({"path": path, "snapshot": rel(target, root),
                                 "bytes": len(content), "origin": origin})
    # Roslyn-derived member list artifact.
    members_text = roslyn["stdout"]
    members_path = os.path.join(snapshots_dir, "clickhouse-functions-members.roslyn.txt")
    with open(members_path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(members_text)

    api = load_api_members(members_text)
    if api["member_lines"] != 205:
        row.fail("ClickHouseFunctions Roslyn member lines=%s, expected 205" % api["member_lines"])

    arms = load_render_arm_names(lane)
    pins = parse_scalar_pins(lane)
    inventory = build_inventory(lane, api, arms, pins)

    write_json(os.path.join(root, "inventory.json"), inventory)
    with open(os.path.join(root, "inventory.md"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write(inventory_markdown(inventory))
    with open(os.path.join(root, "preflight", "inventory.json"), "w", encoding="utf-8", newline="\n") as handle:
        json.dump(inventory, handle, indent=2, ensure_ascii=False)
    with open(os.path.join(root, "preflight", "inventory.md"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write(inventory_markdown(inventory))

    selection = test_selection(root, lane, inventory)
    write_json(os.path.join(root, "test-selection.json"), selection)
    write_json(os.path.join(root, "preflight", "test-selection.json"), selection)
    matrix = provider_family_matrix(inventory)
    write_json(os.path.join(root, "provider-family-matrix.json"), matrix)
    write_json(os.path.join(root, "preflight", "provider-family-matrix.json"), matrix)
    scenarios = benchmark_scenarios(root, lane, inventory)
    write_json(os.path.join(root, "benchmark-scenarios.json"), scenarios)
    write_json(os.path.join(root, "preflight", "benchmark-scenarios.json"), scenarios)

    # Metadata probes (container lease is held by the caller's flock; the socket is the only access).
    ping = docker_ping_probe()
    row.run_docker_probe(ping)
    image_probe = docker_image_probe(CLICKHOUSE_IMAGE)
    row.run_docker_probe(image_probe)
    image_identity = image_probe.get("identity") or {}
    fixture = dictionary_fixture_probe()
    row.run_docker_probe(fixture)
    write_json(os.path.join(directory, "metadata", "dictionary-fixture-probe.json"), fixture)
    write_json(os.path.join(directory, "metadata", "image.json"), {
        "image": CLICKHOUSE_IMAGE,
        "id": image_identity.get("Id"),
        "repo_digests": image_identity.get("RepoDigests"),
        "repo_tags": image_identity.get("RepoTags"),
        "inspect_exit": image_probe["exit"],
        "ping": ping["transcript"].strip(),
    })

    # Baseline record.
    baseline = {
        "task": TASK,
        "rv": "rv1",
        "lane": lane,
        "branch": branch_name,
        "head": head_sha,
        "group_branch": GROUP_BRANCH,
        "group_head": group_sha,
        "expected_group_head": GROUP_SHA,
        "main_tree": MAIN_TREE,
        "main_head": main_sha,
        "delta_history": history,
        "untracked": untracked,
        "untracked_allowed": allowed_untracked,
        "untracked_allowed_prefixes": allowed_untracked_prefixes,
        "modified_tracked": modified,
        "dependency_satisfied": dependency_ok,
        "prerequisite_evidence": {
            "T335": {"expected": "1b59e661", "observed": group_sha, "ok": group_sha == GROUP_SHA},
            "T276": {"expected": "d20b91e3 integrated", "observed": "d20b91e3" in [h["sha"] for h in history],
                     "ok": "d20b91e3" in [h["sha"] for h in history]},
            "T286a": {"expected": "7407272a integrated", "observed": "7407272a" in [h["sha"] for h in history],
                      "ok": "7407272a" in [h["sha"] for h in history]},
        },
        "branch_created": branch_name == TASK_BRANCH,
        "generated_at": utc_now(),
    }
    write_json(os.path.join(directory, "baseline.json"), baseline)

    # Zero-test / coverage guards for the D0 metadata.
    checks = {
        "dependency_satisfied": dependency_ok,
        "branch": branch_name,
        "head": head_sha,
        "group_head": group_sha,
        "modified_tracked": modified,
        "unexpected_untracked": unexpected,
        "inventory_source_tokens": inventory["accounting"]["source_tokens"],
        "inventory_wildcard_entries": inventory["accounting"]["wildcard_entries"],
        "inventory_members": inventory["accounting"]["members_total"],
        "inventory_unresolved_expansions": inventory["accounting"]["unresolved_expansions"],
        "per_family_member_counts": {code: inventory["per_family"][code]["member_count"] for code, _ in FAMILIES},
        "api_member_lines": api["member_lines"],
        "api_member_methods": api["method_count"],
        "dialect_supports": arms["supports_count"],
        "dialect_render_arms": arms["render_arm_count"],
        "dialect_unsupported_arms": arms["unsupported_arms"],
        "test_supported_names": arms["test_supported_names"],
        "test_render_arms": arms["test_render_arms"],
        "scalar_pin_values": pins["values"],
        "container_ping": ping["transcript"].strip(),
        "clickhouse_image": CLICKHOUSE_IMAGE,
        "clickhouse_image_id": image_identity.get("Id"),
        "dictionary_fixture": fixture["capability"],
    }
    row.checks = checks

    # Lease policy: no internal lease on the externally wrapped rows, one per family live child,
    # and no build server may inherit a lease fd (see preflight/lease-policy.json).
    lease_check = lease_policy_selfcheck()
    write_json(os.path.join(directory, "lease-policy.json"), lease_check)
    row.checks["lease_policy"] = lease_check
    row.add_artifact(os.path.join(directory, "lease-policy.json"))
    if not lease_check["ok"]:
        row.fail("lease policy self-check failed: %s" % json.dumps(lease_check["command_children"]))

    if inventory["accounting"]["source_tokens"] != 313:
        row.fail("source token count %s != 313" % inventory["accounting"]["source_tokens"])
    if inventory["accounting"]["families"] != 19:
        row.fail("family count %s != 19" % inventory["accounting"]["families"])
    if inventory["accounting"]["unresolved_expansions"] != 0:
        row.fail("unresolved expansions: %s" % inventory["accounting"]["unresolved_expansions"])
    if inventory["accounting"]["wildcard_entries"] != WILDCARD_ENTRY_COUNT_CLAIM:
        row.fail("wildcard entry count %s != plan claim %s"
                 % (inventory["accounting"]["wildcard_entries"], WILDCARD_ENTRY_COUNT_CLAIM))
    if api["member_lines"] != 205:
        row.checks["api_member_count_note"] = "roslyn member lines=%s (expected 205)" % api["member_lines"]
    # The pinned inventory test owns the counts; the preflight only requires that its two
    # HaveCount pins agree with the dialect tables read in this run (no hardcoded drift).
    pin_text = read_text(os.path.join(lane, CH_SCALAR_PINS))
    name_pin = re.search(r'HaveCount\((\d+),\s*"[^"]*scalar-function names"', pin_text)
    arm_pin = re.search(r'HaveCount\((\d+),\s*"[^"]*(?:pad forms|optional forms)', pin_text)
    if not name_pin or int(name_pin.group(1)) != arms["supports_count"]:
        row.fail("pinned supported-name count %s != dialect Supports() %s"
                 % (name_pin.group(1) if name_pin else "missing", arms["supports_count"]))
    if not arm_pin or int(arm_pin.group(1)) != arms["test_render_arms"]:
        row.fail("pinned render-arm count %s != test arms %s"
                 % (arm_pin.group(1) if arm_pin else "missing", arms["test_render_arms"]))
    if arms["test_only_names"] or arms["dialect_only_names"]:
        row.fail("test/dialect scalar name sets differ: test-only=%s dialect-only=%s"
                 % (arms["test_only_names"], arms["dialect_only_names"]))
    if ping["exit"] != 0:
        row.fail("container socket/API is not reachable")
    if fixture["capability"] != "available":
        row.fail("dictionary fixture capability probe failed: %s" % fixture["reason"])

    row.add_artifact(os.path.join(root, "commands.json"))
    row.add_artifact(os.path.join(root, "inventory.json"))
    row.add_artifact(os.path.join(root, "inventory.md"))
    row.add_artifact(os.path.join(directory, "baseline.json"))
    row.add_artifact(members_path)
    return row.finish()


def cmd_build(args, root: str, lane: str) -> dict:
    directory = ensure_dir(os.path.join(root, "build"))
    manifest = persist_manifest(root, lane, args.phase)
    row = Row("build", directory, root, lane)
    for spec in manifest["commands"]["build"]:
        row.run(spec["argv"], timeout_s=1800, name=spec["name"])
    return row.finish()


def cmd_unit(args, root: str, lane: str) -> dict:
    directory = ensure_dir(os.path.join(root, "unit"))
    manifest = persist_manifest(root, lane, args.phase)
    row = Row("unit", directory, root, lane)
    for spec in manifest["commands"]["unit"]:
        row.run(spec["argv"], timeout_s=3600, name=spec["name"], min_total=1)
    return row.finish()


def cmd_provider_sql(args, root: str, lane: str) -> dict:
    directory = ensure_dir(os.path.join(root, "provider-sql"))
    manifest = persist_manifest(root, lane, args.phase)
    row = Row("provider-sql", directory, root, lane)
    for spec in manifest["commands"]["provider-sql"]:
        row.run(spec["argv"], timeout_s=3600, name=spec["name"], min_total=1)
    return row.finish()


def cmd_integration(args, root: str, lane: str) -> dict:
    directory = ensure_dir(os.path.join(root, "integration"))
    manifest = persist_manifest(root, lane, args.phase)
    row = Row("integration", directory, root, lane)
    try:
        require_external_lease("integration (C04)")
        env = build_env(require_docker=True, forbid_optouts=True)
    except RuntimeError as exc:
        row.fail(str(exc))
        return row.finish()
    row.run(manifest["commands"]["integration"][0]["argv"], timeout_s=1800, name="build-integration", env=env)
    child = row.run(manifest["commands"]["integration"][1]["argv"], timeout_s=7200, name="integration-suite",
                    env=env, min_total=1, allow_provider_skips=False, log_path_name="integration-suite.log")
    if child["summary"]:
        row.checks["skipped_total"] = child["summary"].get("skipped")
        row.checks["failed_total"] = child["summary"].get("failed")
        row.checks["total"] = child["summary"].get("total")
        skipped_view = skip_view(child["output"])
        provider_skip = PROVIDER_SKIP.search(skipped_view)
        row.checks["required_supported_cells_skipped"] = 0 if not provider_skip else "see run.log"
        row.checks["provider_skip_excerpt"] = provider_skip.group(0)[:160] if provider_skip else None
    row.run(manifest["commands"]["integration"][2]["argv"], timeout_s=600, name="integration-skip-budget",
            env=env, require_zero=True)
    return row.finish()


def cmd_coverage(args, root: str, lane: str) -> dict:
    directory = ensure_dir(os.path.join(root, "coverage"))
    manifest = persist_manifest(root, lane, args.phase)
    row = Row("coverage", directory, root, lane)
    try:
        require_external_lease("coverage (C05)")
        env = build_env(require_docker=True, forbid_optouts=True)
    except RuntimeError as exc:
        row.fail(str(exc))
        return row.finish()
    for spec in manifest["commands"]["coverage"]:
        row.run(spec["argv"], timeout_s=spec.get("timeout_s", 3600), name=spec["name"], env=env)
    summary_paths = {
        "solution": "tests/coverage/report/Summary.txt",
        "integration": "tests/coverage/integration-report-gate/Summary.txt",
    }
    for label, relative in summary_paths.items():
        path = os.path.join(lane, relative)
        if os.path.exists(path):
            text = read_text(path)
            row.checks[label + "_summary"] = [line for line in text.splitlines() if "Line coverage" in line or "Branch coverage" in line]
            row.add_artifact(path)
        else:
            row.fail("coverage summary missing: %s" % relative)
    return row.finish()


def cmd_benchmark(args, root: str, lane: str) -> dict:
    phase = args.phase or "baseline"
    if phase not in ("baseline", "candidate"):
        raise SystemExit("--phase must be baseline or candidate")
    directory = ensure_dir(os.path.join(root, "performance", phase))
    manifest = persist_manifest(root, lane, phase)
    row = Row("benchmark-" + phase, directory, root, lane)
    artifacts_dir = ensure_dir(os.path.join(directory, "bdn"))
    argv = benchmark_run(phase, artifacts_dir)
    child = row.run(argv, timeout_s=7200, name="benchmark-" + phase, require_summary=False)

    # Summary-table columns of the NextormConfig layout (Job and RatioSD are hidden):
    # Method | Mean | Error | StdDev | Ratio | Gen0 | Gen1 | Allocated | Alloc Ratio
    rows = []
    header = None
    for line in child["stdout"].splitlines():
        if line.startswith("| Method"):
            header = [cell.strip() for cell in line.strip("|").split("|")]
            continue
        if header and line.startswith("|") and "---" not in line:
            cells = [cell.strip() for cell in line.strip("|").split("|")]
            if len(cells) != len(header):
                continue
            record = dict(zip(header, cells))
            rows.append({
                "method": record.get("Method", ""),
                "mean": record.get("Mean", ""),
                "error": record.get("Error", ""),
                "stddev": record.get("StdDev", ""),
                "ratio": record.get("Ratio", ""),
                "gen0": record.get("Gen0", ""),
                "gen1": record.get("Gen1", ""),
                "allocated": record.get("Allocated", ""),
                "alloc_ratio": record.get("Alloc Ratio", ""),
            })

    # The complete BDN environment block(s), deduplicated in launch order.
    environment = []
    collecting = False
    for line in child["stdout"].splitlines():
        if line.startswith("BenchmarkDotNet v"):
            collecting = True
        elif collecting and line.startswith("| "):
            collecting = False
        if collecting:
            stripped = line.rstrip()
            if stripped and stripped not in environment:
                environment.append(stripped)

    full = os.environ.get("NEXTORM_BENCH_FULL", "")
    payload = {
        "phase": phase,
        "argv": argv,
        "bdn_artifacts": artifacts_dir,
        "raw_stdout": child["stdout_path"],
        "protocol": {
            "config": "NextormConfig",
            "toolchain": "Job.Default (out-of-process)" if full == "1" else "Job.ShortRun + InProcessEmitToolchain",
            "nextorm_bench_full": full,
            "warmup_count": "default (30) per BDN Job.Default" if full == "1" else 3,
            "iteration_count": "default (30) per BDN Job.Default" if full == "1" else 3,
            "invocation_count": 1,
            "database_executed": False,
            "separation": "every arm builds a QueryCommand and calls GetPreparedQueryCommand; the rendered "
                          "CommandText length is the sink, so database time is zero by construction",
        },
        "environment": environment,
        "results": rows,
        "stdout_tail": child["stdout"][-4000:],
        "generated_at": utc_now(),
    }
    write_json(os.path.join(directory, "measurements.json"), payload)
    row.checks["benchmark_rows"] = len(rows)
    row.checks["methods"] = [item["method"] for item in rows]
    row.checks["database_executed"] = False
    row.checks["protocol"] = payload["protocol"]
    row.checks["bdn_artifacts"] = artifacts_dir
    if not rows:
        row.fail("no BenchmarkDotNet summary rows parsed -- the benchmark produced no measurement")
    row.add_artifact(os.path.join(directory, "measurements.json"))
    row.add_artifact(artifacts_dir)
    return row.finish()


def cmd_docs(args, root: str, lane: str) -> dict:
    directory = ensure_dir(os.path.join(root, "docs"))
    manifest = persist_manifest(root, lane, args.phase)
    row = Row("docs", directory, root, lane)
    row.run(manifest["commands"]["docs"][0]["argv"], timeout_s=3600, name="docfx")

    # Public-link rule: a *public page* must not link to docs/specs/**. The internal records are
    # not public pages and are pruned as sources: docs/specs/** is the DocFX-excluded specs tree
    # itself and docs/superpowers/** holds internal planning records (both pre-date T286b).
    internal_sources = ("docs/specs", "docs/superpowers")

    def is_internal(relative_dir):
        return any(relative_dir == tree or relative_dir.startswith(tree + "/")
                   for tree in internal_sources)

    excluded_sources = {}
    for tree in internal_sources:
        count = 0
        for current, _dirs, files in os.walk(os.path.join(lane, tree)):
            count += sum(1 for name in files if name.endswith(".md"))
        excluded_sources[tree] = count
    row.checks["public_link_sources_excluded"] = excluded_sources

    offenders = []
    public_roots = ["docs", "docs/ru"]
    for public_root in public_roots:
        base = os.path.join(lane, public_root)
        for current, dirs, files in os.walk(base):
            relative_dir = rel(current, lane).replace(os.sep, "/")
            dirs[:] = [
                name for name in dirs
                if not is_internal(relative_dir + "/" + name) and name not in ("api", "_site")
            ]
            if is_internal(relative_dir):
                continue
            for name in files:
                if not name.endswith(".md"):
                    continue
                path = os.path.join(current, name)
                text = read_text(path)
                if "docs/specs/" in text or "../specs/" in text:
                    offenders.append(rel(path, lane))
    with open(os.path.join(lane, "readme.md"), encoding="utf-8", errors="replace") as handle:
        if "docs/specs/" in handle.read():
            offenders.append("readme.md")
    row.checks["public_links_to_specs"] = offenders
    if offenders:
        row.fail("public documentation links to internal specs: %s" % offenders)

    # EN/RU parity of the ClickHouse pages.
    en = os.path.join(lane, "docs/guide/provider-specific-sql/clickhouse.md")
    ru = os.path.join(lane, "docs/ru/guide/provider-specific-sql/clickhouse.md")
    row.checks["en_page"] = os.path.exists(en)
    row.checks["ru_page"] = os.path.exists(ru)
    if not (os.path.exists(en) and os.path.exists(ru)):
        row.fail("EN/RU ClickHouse provider pages are not both present")
    return row.finish()


def cmd_closure(args, root: str, lane: str) -> dict:
    directory = ensure_dir(os.path.join(root, "closure"))
    row = Row("closure", directory, root, lane)
    inventory_path = os.path.join(root, "inventory.json")
    if not os.path.exists(inventory_path):
        row.fail("inventory.json is missing: run preflight first")
        return row.finish()
    inventory = json.load(open(inventory_path, encoding="utf-8"))
    families_dir = os.path.join(root, "families")
    produced = {}
    if os.path.isdir(families_dir):
        for name in sorted(os.listdir(families_dir)):
            result_path = os.path.join(families_dir, name, "result.json")
            if os.path.exists(result_path):
                produced[name] = json.load(open(result_path, encoding="utf-8"))
    missing = [code for code, _ in FAMILIES if code not in produced]
    failed = [code for code, result in produced.items() if not result.get("ok")]
    covered = sum(1 for code, members in inventory["members"].items() for _ in members)
    row.checks["families_expected"] = len(FAMILIES)
    row.checks["families_evidenced"] = len(produced)
    row.checks["families_missing"] = missing
    row.checks["families_failed"] = failed
    row.checks["members"] = covered
    if missing:
        row.fail("families without evidence: %s" % missing)
    if failed:
        row.fail("families whose evidence row failed: %s" % failed)
    return row.finish()


def cmd_gate(args, root: str, lane: str) -> dict:
    gate = args.gate
    if gate not in HELPERS:
        raise SystemExit("--gate must be one of helper-1, helper-2, provenance")
    directory = ensure_dir(os.path.join(root, "gates", gate))
    manifest_path = frozen_manifest_path(root)
    argv = helper_argv(gate, root)
    persist_manifest(root, lane, args.phase)
    row = Row("gate-" + gate, directory, root, lane)
    row.checks["argv"] = argv
    row.checks["manifest_exists"] = os.path.exists(manifest_path)
    if not os.path.exists(manifest_path):
        row.fail("evidence manifest is missing: %s (the gate runs at closure, E29-E31)" % manifest_path)
        return row.finish()
    env = build_env()
    env["PYTHONPATH"] = os.pathsep.join(filter(None, [os.path.join(MAIN_TREE, ".pdca", "scripts"), env.get("PYTHONPATH")]))
    row.run(argv, timeout_s=1800, name=gate, env=env)
    return row.finish()


def cmd_family(args, root: str, lane: str) -> dict:
    family = (args.family or "").upper()
    if family not in {code for code, _ in FAMILIES}:
        raise SystemExit("--family must be one of %s" % ", ".join(code for code, _ in FAMILIES))
    directory = ensure_dir(os.path.join(root, "families", family))
    manifest = persist_manifest(root, lane, args.phase)
    row = Row("family-" + family, directory, root, lane)
    selection_path = os.path.join(directory, "selection.json")
    row.checks["selection_path"] = selection_path
    if not os.path.exists(selection_path):
        row.fail("family selection is missing: %s -- the family task must publish unit_filter and "
                 "live_classes before the row can be executed (a zero-test run is refused)" % selection_path)
        return row.finish()
    selection = json.load(open(selection_path, encoding="utf-8"))
    row.checks["selection"] = selection

    row.run(manifest["commands"]["family"][family][0]["argv"], timeout_s=1800, name="discovery-clickhouse", min_listed=1)
    row.run(manifest["commands"]["family"][family][1]["argv"], timeout_s=1800, name="discovery-integration", min_listed=1)

    unit_filter = selection.get("unit_filter")
    if unit_filter:
        row.run(["dotnet", "test", "tests/nextorm.clickhouse.tests", "-c", "Debug",
                 "--filter", unit_filter], timeout_s=3600, name="family-unit", min_total=1)
    else:
        row.fail("selection.json carries no unit_filter; a zero-test unit selection is refused")

    live_classes = selection.get("live_classes") or []
    if not live_classes:
        row.fail("selection.json carries no live_classes; the family has no live ClickHouse cell")
    env = build_env(require_docker=True, forbid_optouts=True)
    for index, class_name in enumerate(live_classes):
        row.run(integration_run(["-class", class_name]), timeout_s=3600,
                name="family-live-%d" % index, env=env, min_total=1, allow_provider_skips=False)
    return row.finish()


COMMANDS = {
    "preflight": cmd_preflight,
    "build": cmd_build,
    "unit": cmd_unit,
    "provider-sql": cmd_provider_sql,
    "integration": cmd_integration,
    "coverage": cmd_coverage,
    "benchmark": cmd_benchmark,
    "docs": cmd_docs,
    "closure": cmd_closure,
    "gate": cmd_gate,
    "family": cmd_family,
    "lease-check": cmd_lease_check,
}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="T286b evidence runner (frozen plan r=1, contract rv1)")
    parser.add_argument("command", choices=sorted(COMMANDS))
    parser.add_argument("--rv", default="rv1")
    parser.add_argument("--evidence-root", default=EVIDENCE_ROOT_DEFAULT)
    parser.add_argument("--phase", default="baseline", help="benchmark phase: baseline|candidate")
    parser.add_argument("--gate", default=None, help="helper-1|helper-2|provenance")
    parser.add_argument("--family", default=None, help="F01..F19")
    args = parser.parse_args(argv)
    if args.rv != "rv1":
        print("only rv1 is the frozen contract revision for this runner", file=sys.stderr)
        return 2
    root = os.path.abspath(args.evidence_root)
    ensure_dir(root)
    lane = lane_root()
    try:
        payload = COMMANDS[args.command](args, root, lane)
    except RuntimeError as exc:
        print("FAIL: %s" % exc, file=sys.stderr)
        return 1
    print("%s %s: ok=%s exit=%s reasons=%s"
          % (TASK, payload["command"], payload["ok"], payload["exit"], json.dumps(payload["reasons"], ensure_ascii=False)))
    return payload["exit"]


if __name__ == "__main__":
    raise SystemExit(main())
