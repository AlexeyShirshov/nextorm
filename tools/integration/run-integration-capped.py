#!/usr/bin/env python3
"""Run the integration suite one provider class at a time under cgroup caps.

Why this exists
---------------
The container suite starts PostgreSQL, SQL Server, MySQL and ClickHouse in the Podman WSL2
machine. On a 16 GB host that machine and the interactive Ubuntu distro share one global
``.wslconfig`` memory limit, so a single full-suite run (plus ``dotnet``'s own heap) can push the
whole system into the Windows pagefile: WSL stalls, `/mnt/c` interop times out and the OpenCode
server gets killed with the VM.

This wrapper keeps the run inside a bounded cgroup so a runaway test dies instead of taking the
desktop down, and it starts one provider at a time so only one database container is alive:

* ``systemd-run --user --scope`` for ``MemoryHigh``/``MemoryMax``/``MemorySwapMax`` (cgroup v2; the
  user manager must be running, which ``systemd=true`` in ``wsl.conf`` gives us);
* CPU and I/O weights written straight into the scope's cgroup by a one-line shim, because systemd
  249 silently ignores ``CPUQuota=`` and ``IOWeight=`` on transient *user* scopes: ``cpu.max``
  stays ``max 100000``, and with BFQ the knob is ``io.bfq.weight``, which systemd never writes;
* ``ionice -c 2 -n 7`` on top of that — BFQ honours per-process I/O priority;
* ``setsid --wait`` so the job survives an OpenCode server restart instead of dying with the
  parent shell;
* ``--build`` builds the test project once and every class then runs with ``--no-build``.

What this does *not* bound: the containers themselves live in the Podman VM, whose memory is
governed by ``%USERPROFILE%\\\\.wslconfig`` (the machine's own ``Resources.Memory`` is ignored on
the WSL provider). Trimming that file and running one provider at a time are the two halves of
the same fix.

Usage
-----
    # one provider, the fast iteration loop
    python3 tools/integration/run-integration-capped.py --providers postgres

    # every provider, each class as its own run, heavy ones last
    python3 tools/integration/run-integration-capped.py

    # explicit classes
    python3 tools/integration/run-integration-capped.py --class \\
        nextorm.integration.tests.PostgresIntegrationTests

    # the canonical full-suite evidence run (+ skip-budget gate)
    python3 tools/integration/run-integration-capped.py --full

    # see the exact commands, run nothing
    python3 tools/integration/run-integration-capped.py --dry-run

A *bare* full-suite run (``dotnet run`` without the wrapper) is not allowed on a memory-tight host:
it holds the Podman WSL2 VM next to the Ubuntu distro and takes the OpenCode server down with it.
``--full`` is the blessed way to produce the skip-budget evidence.

Exit code: 0 when every requested class passed, 1 otherwise.

Note on the skip budget: ``tests/nextorm.integration.tests/skip-budget.json`` is pinned against a
*canonical full-suite* run (``tools/integration/check-integration-skip-budget.py``). Per-class
runs are for local iteration and cannot be used as that evidence.
"""

from __future__ import annotations

import argparse
import datetime
import fcntl
import os
import pathlib
import re
import shutil
import subprocess
import sys
import time

DOCKER_SOCKET = "unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock"
PROJECT = "tests/nextorm.integration.tests"
DEFAULT_LOG_DIR = pathlib.Path("/tmp/opencode/integration")

# Ordered cheap -> heavy. Each class runs in its own process, so its container is created and
# stopped by the DatabaseContainers fixture before the next one starts.
PROVIDER_CLASSES = {
    "sqlite": [
        "nextorm.integration.tests.SqliteIntegrationTests",
        "nextorm.integration.tests.SqliteSpecificTests",
    ],
    "postgres": [
        "nextorm.integration.tests.PostgresIntegrationTests",
        "nextorm.integration.tests.PostgresSpecificTests",
    ],
    "sqlserver": [
        "nextorm.integration.tests.SqlServerIntegrationTests",
        "nextorm.integration.tests.SqlServerSpecificTests",
    ],
    "mysql": [
        "nextorm.integration.tests.MySqlIntegrationTests",
        "nextorm.integration.tests.MySqlSpecificTests",
    ],
    "clickhouse": [
        "nextorm.integration.tests.ClickHouseIntegrationTests",
    ],
}
PROVIDER_ORDER = ["sqlite", "postgres", "sqlserver", "mysql", "clickhouse"]

SUMMARY_RE = re.compile(
    r"Total:\s*(\d+),\s*Errors:\s*(\d+),\s*Failed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Not Run:\s*(\d+)"
)
CONTAINER_ID_RE = re.compile(r"container ([0-9a-f]{12,64})")
PODMAN = '"/mnt/c/Program Files/RedHat/Podman/podman.exe" --connection podman-machine-default'


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run nextorm integration test classes sequentially under cgroup caps.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument(
        "--providers",
        default=",".join(PROVIDER_ORDER),
        help="comma-separated providers in run order (default: %(default)s)",
    )
    parser.add_argument(
        "--class",
        dest="classes",
        action="append",
        default=[],
        metavar="FQCN",
        help="explicit fully-qualified test class; overrides --providers (repeatable)",
    )
    parser.add_argument("--memory-high", default="3G", help="cgroup MemoryHigh (default: %(default)s)")
    parser.add_argument("--memory-max", default="4G", help="cgroup MemoryMax (default: %(default)s)")
    parser.add_argument(
        "--swap-max",
        default="1G",
        help="cgroup MemorySwapMax, so the run cannot push the host into swap (default: %(default)s)",
    )
    parser.add_argument("--cpu-quota", default="600%", help="cgroup CPUQuota (default: %(default)s)")
    parser.add_argument(
        "--io-priority",
        type=int,
        default=7,
        help="ionice best-effort level 0..7, 7 is lowest (default: %(default)s)",
    )
    parser.add_argument(
        "--io-weight",
        type=int,
        default=50,
        help="io.bfq.weight written into the scope (100 is the default share; lower yields to interactive work)",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=0,
        metavar="SECONDS",
        help="per-class timeout, 0 disables it (default: %(default)s)",
    )
    parser.add_argument("--configuration", "-c", default="Debug", help="build configuration (default: %(default)s)")
    parser.add_argument("--build", action="store_true", help=f"build {PROJECT} before the first run")
    parser.add_argument("--no-caps", action="store_true", help="skip systemd-run/ionice, run plain")
    parser.add_argument("--no-detach", action="store_true", help="do not use setsid (die with the parent)")
    parser.add_argument("--gap", type=int, default=10, help="seconds between classes (default: %(default)s)")
    parser.add_argument("--log-dir", type=pathlib.Path, default=DEFAULT_LOG_DIR, help="default: %(default)s")
    parser.add_argument("--dry-run", action="store_true", help="print the commands and exit")
    parser.add_argument(
        "--full",
        action="store_true",
        help=(
            "run the CANONICAL full suite once (no -class, -notrait Category=Diagnostic) under the same "
            "caps, then check tests/nextorm.integration.tests/skip-budget.json against its log. This is "
            "the only supported way to produce skip-budget evidence on a memory-tight host: a plain "
            "full-suite run holds the Podman WSL2 VM next to the Ubuntu one and takes the OpenCode "
            "server down with it."
        ),
    )
    return parser.parse_args(argv)


def resolve_classes(args: argparse.Namespace) -> list[tuple[str, str]]:
    if args.classes:
        return [("custom", fqcn) for fqcn in args.classes]
    requested = [p.strip().lower() for p in args.providers.split(",") if p.strip()]
    unknown = [p for p in requested if p not in PROVIDER_CLASSES]
    if unknown:
        raise SystemExit(f"unknown provider(s): {', '.join(unknown)}; known: {', '.join(PROVIDER_ORDER)}")
    resolved: list[tuple[str, str]] = []
    for provider in PROVIDER_ORDER:
        if provider in requested:
            resolved.extend((provider, fqcn) for fqcn in PROVIDER_CLASSES[provider])
    return resolved


def base_env(args: argparse.Namespace) -> dict[str, str]:
    env = dict(os.environ)
    env["DOCKER_HOST"] = DOCKER_SOCKET
    # The suite is fail-closed; an inherited opt-out would silently turn it green.
    env.pop("NEXTORM_SKIP_PROVIDERS", None)
    env.setdefault("DOTNET_gcServer", "0")
    env.setdefault("DOTNET_GCHeapHardLimit", "0x80000000")
    env.setdefault("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
    env.update(session_bus_vars())
    return env


def session_bus_vars() -> dict[str, str]:
    """systemd-run --user needs a reachable user bus; WSLg's XDG_RUNTIME_DIR is not enough."""
    runtime = f"/run/user/{os.getuid()}"
    if os.path.isdir(runtime):
        return {"XDG_RUNTIME_DIR": runtime, "DBUS_SESSION_BUS_ADDRESS": f"unix:path={runtime}/bus"}
    return {}


def systemd_env() -> dict[str, str]:
    env = dict(os.environ)
    env.update(session_bus_vars())
    return env


def probe_systemd() -> bool:
    if not shutil.which("systemd-run"):
        return False
    try:
        probe = subprocess.run(
            ["systemd-run", "--user", "--scope", "--quiet", "--collect", "--", "true"],
            env=systemd_env(),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=30,
        )
    except (OSError, subprocess.TimeoutExpired):
        return False
    return probe.returncode == 0


def cgroup_shim(args: argparse.Namespace) -> list[str]:
    """Write the CPU and BFQ weights into the scope's own cgroup, then exec the real command.

    systemd 249 (Ubuntu 22.04) silently ignores ``CPUQuota=`` and ``IOWeight=`` on transient *user
    scopes*: ``cpu.max`` stays ``max 100000`` and with BFQ the file systemd would need is
    ``io.bfq.weight``, which it never writes. Writing them from inside the scope works and is
    inherited by the child, so the limits are real rather than decorative.
    """
    try:
        percent = float(args.cpu_quota.rstrip("%"))
    except ValueError:
        raise SystemExit(f"--cpu-quota expects a percentage such as 600%, got {args.cpu_quota!r}")
    quota_us = int(percent * 1000)
    script = (
        'd=/sys/fs/cgroup$(cut -d: -f3 /proc/self/cgroup); '
        f'echo "{quota_us} 100000" > "$d/cpu.max" 2>/dev/null || true; '
        f'echo {args.io_weight} > "$d/io.bfq.weight" 2>/dev/null || true; '
        'exec "$@"'
    )
    return ["sh", "-c", script, "sh"]


def wrap_guarded(args: argparse.Namespace, inner: list[str], caps: bool) -> list[str]:
    if args.timeout > 0:
        inner = ["timeout", str(args.timeout)] + inner
    if args.io_priority >= 0 and shutil.which("ionice"):
        inner = ["ionice", "-c", "2", "-n", str(args.io_priority)] + inner
    if caps:
        inner = cgroup_shim(args) + inner
    if not args.no_detach and shutil.which("setsid"):
        inner = ["setsid", "--wait"] + inner
    if caps:
        inner = [
            "systemd-run",
            "--user",
            "--scope",
            "--quiet",
            "--collect",
            "-p",
            f"MemoryHigh={args.memory_high}",
            "-p",
            f"MemoryMax={args.memory_max}",
            "-p",
            f"MemorySwapMax={args.swap_max}",
            "--",
        ] + inner
    return inner


def suite_arguments(args: argparse.Namespace, fqcn: str | None) -> list[str]:
    arguments = [
        "dotnet",
        "run",
        "--project",
        PROJECT,
        "-c",
        args.configuration,
        "--no-build",
        "--",
        "-noColor",
        "-notrait",
        "Category=Diagnostic",
    ]
    if fqcn is not None:
        arguments += ["-class", fqcn]
    return arguments


def build_command(args: argparse.Namespace, fqcn: str, caps: bool) -> list[str]:
    return wrap_guarded(args, suite_arguments(args, fqcn), caps)


def build_full_command(args: argparse.Namespace, caps: bool) -> list[str]:
    """The canonical full-suite command (the one the skip budget is pinned against)."""
    return wrap_guarded(args, suite_arguments(args, None), caps)


def preflight() -> None:
    subprocess.run(["free", "-h"], check=False)
    pressure = pathlib.Path("/proc/pressure/io")
    if pressure.exists():
        print("io pressure:", pressure.read_text().strip(), flush=True)
    if not pathlib.Path(DOCKER_SOCKET.removeprefix("unix://")).exists():
        print(
            "WARNING: podman socket is missing; container-backed classes will fail closed.\n"
            '  start it with: "/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start',
            file=sys.stderr,
            flush=True,
        )


def find_running_suite() -> list[str]:
    """A suite already running (another terminal or an orphan of a killed wrapper) would share the
    same reusable containers and the same 12 GB podman VM — that collision is what the skill warns
    about, so refuse to start a second one.

    Matches the test *host* executable, not arbitrary command lines: a shell that merely mentions
    the assembly name (a `ps`/`grep` inspection) must not block the run.
    """
    found: list[str] = []
    host_names = {"nextorm.integration.tests", "nextorm.integration.tests.dll"}
    for entry in pathlib.Path("/proc").iterdir():
        if not entry.name.isdigit() or int(entry.name) == os.getpid():
            continue
        try:
            raw = (entry / "cmdline").read_bytes()
        except OSError:
            continue
        argv = [part.decode("utf-8", "replace") for part in raw.split(b"\0") if part]
        if not argv:
            continue
        executable = os.path.basename(argv[0])
        is_host = executable in host_names
        is_test_dotnet = executable == "dotnet" and len(argv) > 1 and os.path.basename(argv[1]) in host_names
        if is_host or is_test_dotnet:
            found.append(f"{entry.name} {' '.join(argv[:3])}")
    return found


def parse_summary(text: str) -> dict[str, int]:
    match = SUMMARY_RE.search(text)
    if not match:
        return {}
    total, errors, failed, skipped, not_run = (int(group) for group in match.groups())
    return {
        "total": total,
        "errors": errors,
        "failed": failed,
        "skipped": skipped,
        "not_run": not_run,
        "passed": total - errors - failed - skipped - not_run,
    }


def run_class(
    provider: str,
    fqcn: str,
    command: list[str],
    env: dict[str, str],
    log_path: pathlib.Path,
) -> dict[str, object]:
    log_path.parent.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    chunks: list[str] = []
    with log_path.open("w", encoding="utf-8", errors="replace") as log:
        log.write(f"# {datetime.datetime.now().isoformat(timespec='seconds')}\n")
        log.write("# " + " ".join(command) + "\n\n")
        log.flush()
        process = subprocess.Popen(
            command,
            env=env,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
            errors="replace",
        )
        assert process.stdout is not None
        try:
            for line in process.stdout:
                chunks.append(line)
                log.write(line)
                log.flush()
        finally:
            process.wait()
        rc = process.returncode
    elapsed = time.monotonic() - started
    tail = "".join(chunks[-40:])
    summary = parse_summary(tail)
    return {
        "provider": provider,
        "class": fqcn,
        "rc": rc,
        "seconds": elapsed,
        "summary": summary,
        "log": log_path,
    }


def run_skip_budget_check(log_path: pathlib.Path, env: dict[str, str]) -> int:
    """Compare the canonical log's skip set with the pinned budget (the merge evidence gate)."""
    checker = pathlib.Path("tools/integration/check-integration-skip-budget.py")
    if not checker.exists():
        print(f"skip-budget checker not found: {checker}", file=sys.stderr, flush=True)
        return 2
    print(f"\n$ python3 {checker} --log {log_path}", flush=True)
    return subprocess.run([sys.executable, str(checker), "--log", str(log_path)], env=env, check=False).returncode


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.full and args.classes:
        raise SystemExit("--full runs the whole suite; it cannot be combined with --class")
    classes = [] if args.full else resolve_classes(args)
    env = base_env(args)

    caps = not args.no_caps and probe_systemd()
    if caps:
        print(
            f"caps: MemoryHigh={args.memory_high} MemoryMax={args.memory_max} "
            f"MemorySwapMax={args.swap_max} cpu.max={args.cpu_quota} "
            f"io.bfq.weight={args.io_weight} ionice=BE/{args.io_priority} "
            f"detach={'off' if args.no_detach else 'setsid'}",
            flush=True,
        )
    if not caps and not args.no_caps:
        print(
            "WARNING: systemd --user scope unavailable, running without cgroup caps\n"
            "  (check that systemd=true in /etc/wsl.conf and /run/user/<uid>/bus exists)",
            file=sys.stderr,
            flush=True,
        )

    if args.full:
        commands = [("canonical", "canonical-full-suite", build_full_command(args, caps))]
    else:
        commands = [(provider, fqcn, build_command(args, fqcn, caps)) for provider, fqcn in classes]
    if args.dry_run:
        for provider, fqcn, command in commands:
            print(f"# {provider}: {fqcn}")
            print(" ".join(command))
        return 0

    log_dir: pathlib.Path = args.log_dir
    log_dir.mkdir(parents=True, exist_ok=True)
    lock_path = log_dir / "run.lock"
    lock_file = lock_path.open("w")
    try:
        fcntl.flock(lock_file, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError:
        raise SystemExit(f"another capped run holds {lock_path}; wait for it or remove the file")

    running = find_running_suite()
    if running:
        print(
            "another integration suite is already running; the reusable containers are shared:",
            file=sys.stderr,
        )
        for line in running:
            print(f"  {line}", file=sys.stderr)
        return 1

    preflight()

    if args.build:
        print(f"$ dotnet build {PROJECT} -c {args.configuration}", flush=True)
        built = subprocess.run(
            ["dotnet", "build", PROJECT, "-c", args.configuration],
            env=env,
            check=False,
        )
        if built.returncode != 0:
            return 1

    results: list[dict[str, object]] = []
    for index, (provider, fqcn, command) in enumerate(commands):
        if index and args.gap > 0:
            time.sleep(args.gap)
        short = fqcn.rsplit(".", 1)[-1]
        log_path = log_dir / f"{provider}-{short}.log"
        print(f"\n=== [{index + 1}/{len(commands)}] {short} (log: {log_path}) ===", flush=True)
        try:
            result = run_class(provider, fqcn, command, env, log_path)
        except KeyboardInterrupt:
            print("\ninterrupted; the detached child keeps running", file=sys.stderr, flush=True)
            return 130
        results.append(result)
        summary = result["summary"]
        verdict = "PASS" if result["rc"] == 0 else "FAIL"
        if result["rc"] != 0:
            failed_text = log_path.read_text(encoding="utf-8", errors="replace")
            if "container state improper" in failed_text:
                ids = sorted(set(CONTAINER_ID_RE.findall(failed_text)))
                print(
                    "hint: podman left a container in an improper state (SQL Server ignores SIGTERM, so "
                    "the stop path SIGKILLs it). Remove it and rerun:",
                    file=sys.stderr,
                    flush=True,
                )
                print(f"  {PODMAN} rm -f {' '.join(ids)}", file=sys.stderr, flush=True)
            elif "no such container" in failed_text or "Could not start a" in failed_text:
                print(
                    "hint: a reused container is in a bad state (left over from a killed run); "
                    f"reset it with {PODMAN} stop -a",
                    file=sys.stderr,
                    flush=True,
                )
        print(
            f"=== {verdict} {short}: rc={result['rc']} "
            f"total={summary.get('total', '?')} passed={summary.get('passed', '?')} "
            f"failed={summary.get('failed', '?')} skipped={summary.get('skipped', '?')} "
            f"({result['seconds']:.0f}s) ===",
            flush=True,
        )

    print("\nsummary")
    print(f"{'class':45} {'rc':>3} {'passed':>7} {'failed':>7} {'skipped':>8} {'time':>7}")
    for result in results:
        summary = result["summary"]
        print(
            f"{result['class'].rsplit('.', 1)[-1]:45} {result['rc']:>3} "
            f"{summary.get('passed', '?'):>7} {summary.get('failed', '?'):>7} "
            f"{summary.get('skipped', '?'):>8} {result['seconds']:>6.0f}s"
        )

    failed = [result for result in results if result["rc"] != 0]
    if failed:
        print("\nfailed logs:")
        for result in failed:
            print(f"  {result['log']}")
        return 1

    if args.full and results:
        budget_rc = run_skip_budget_check(results[0]["log"], env)
        if budget_rc != 0:
            print("skip budget gate failed for the canonical run", file=sys.stderr, flush=True)
            return budget_rc
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
