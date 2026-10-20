#!/usr/bin/env python3
"""T247 measurement wrapper: duplicate guard first, then the canonical host lock.

A duplicate launch is an observed failure mode for this task: the same command block was
submitted twice, and the second wrapper queued behind the host lock, ready to start a second
full ``phase --rounds 2`` block (overwriting ``.pdca/T247/rv2/phase/round-{1,2}`` and
``phase.json``) the moment the first one finished.  This wrapper therefore takes a
NON-BLOCKING marker lock on ``/tmp/opencode/t247-measure.guard`` before it queues:

    python3 .pdca/scripts/perf/t247_measure.py phase --rounds 2 --evidence-root .pdca/T247/rv2
    python3 .pdca/scripts/perf/t247_measure.py baseline --rounds 2 --evidence-root .pdca/T247/rv2
    python3 .pdca/scripts/perf/t247_measure.py final --rounds 2 --evidence-root .pdca/T247/rv2

The marker is a *duplicate* guard on its own file.  It is NOT a nested host lock: the single
canonical host lock stays the outer ``flock -x /tmp/opencode/nextorm-bdn-host.lock`` (the 
driver sees the inherited fd and records ``mode: inherited flock -x``).  The guard fd is held
for the whole block and released when this process exits, so it cannot leak across blocks.

Exit codes: 90 = another measurement wrapper is already running; anything else is the driver's
own exit code (2 input error, 4 round invalid, 5 prerequisite, 0 pass).
"""
from __future__ import annotations

import fcntl
import os
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
DRIVER = REPO / ".pdca" / "scripts" / "perf" / "t247_json_gate.py"
DEFAULT_GUARD_LOCK = Path("/tmp/opencode/t247-measure.guard")
DEFAULT_HOST_LOCK = Path("/tmp/opencode/nextorm-bdn-host.lock")
DUPLICATE_EXIT = 90
DUPLICATE_MESSAGE = "T247-DUPLICATE-GUARD: another measurement is running"
USAGE = "usage: t247_measure.py <baseline|phase|final> [driver args...]"


def guard_lock_path():
    """Path of the non-blocking duplicate marker (overridable for tests)."""
    return Path(os.environ.get("T247_GUARD_LOCK", str(DEFAULT_GUARD_LOCK)))


def host_lock_path():
    """Path of the canonical blocking host lock (protocol unchanged)."""
    return Path(os.environ.get("T247_HOST_LOCK", str(DEFAULT_HOST_LOCK)))


class DuplicateMeasurement(RuntimeError):
    """Another measurement wrapper already holds the non-blocking marker lock."""


def acquire_guard(path=None):
    """Take the non-blocking duplicate marker and return the held fd (keep it open)."""
    target = Path(path) if path is not None else guard_lock_path()
    target.parent.mkdir(parents=True, exist_ok=True)
    fd = os.open(str(target), os.O_RDWR | os.O_CREAT, 0o644)
    try:
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError:
        os.close(fd)
        raise DuplicateMeasurement(str(target))
    return fd


def driver_argv(args):
    """The only host lock: blocking ``flock -x`` around the driver."""
    return ["flock", "-x", str(host_lock_path()), sys.executable, str(DRIVER), *args]


def main(argv=None):
    args = list(sys.argv[1:] if argv is None else argv)
    try:
        guard_fd = acquire_guard()
    except DuplicateMeasurement:
        print(DUPLICATE_MESSAGE, file=sys.stderr)
        return DUPLICATE_EXIT
    try:
        if not args:
            print(USAGE, file=sys.stderr)
            return 2
        env = dict(os.environ)
        env["NEXTORM_BENCH_FULL"] = "1"
        return subprocess.run(driver_argv(args), cwd=str(REPO), env=env).returncode
    finally:
        os.close(guard_fd)


if __name__ == "__main__":
    sys.exit(main())
