# T328 — ClickHouse test timeout and bounded retry

Internal specification of the ClickHouse test-infrastructure change delivered for issue **#328**.
It documents the implemented behaviour of the shipped code and the evidence produced for plan
revision **r3** / evidence revision **rv3**; every code claim below was checked against the
sources named in the corresponding section.

| Item | Value |
| --- | --- |
| Tracking issue | #328 |
| Follow-up issue | #338 — `https://github.com/AlexeyShirshov/nextorm/issues/338` |
| Plan revision | r3 (narrowed retryable transport set) |
| Evidence revision | rv3 — `.pdca/T328/rv3/`, manifest `.pdca/T328/rv3/manifest.json` |
| Completion verdict | PASS on r3 scope; reset obligations BLOCKED-by-driver |
| Footprint | `tests/**` plus this document; no `src/**` change |

## 1. Scope

- ClickHouse test infrastructure only: the shared retry helpers under
  `tests/Shared/ClickHouseRetry/`, the ClickHouse test-provider connection-string resolution
  (`tests/nextorm.integration.tests/Providers/ClickHouseTestProvider.cs`), the audited ClickHouse
  read call sites, the diagnostic probes in
  `tests/nextorm.integration.tests/ClickHouse/ClickHouseRetryScenarioTests.cs`, and the
  observational reporter `tests/nextorm.integration.tests/Support/ClickHouseRetryReporter.cs`.
- No production change: the committed patch contains **zero** `src/**` paths. The ClickHouse
  provider is consumed as-is; the timeout is applied to the test connection string before a
  `ClickHouseDataContext` is constructed.
- No retry for any other provider. The shared helper is compiled into exactly two projects
  (`tests/nextorm.core.tests/nextorm.core.tests.csproj`, `nextorm.integration.tests.csproj`) and is
  referenced only by ClickHouse test files, the ClickHouse test provider and the reporter. No
  SQLite, PostgreSQL, SQL Server, MySQL/MariaDB or in-memory path uses it.
- No whole-test replay. The retry unit is one explicitly audited read operation; the enclosing
  test is executed once by the runner and its result is never replaced or re-run.
- Follow-up **#338** tracks the one retained, driver-blocked obligation (typed transport-reset
  detection); creating it neither satisfies nor waives that obligation.
- Related but out of scope: #329 (flaky-test tracking) is neither fixed nor concealed here.

## 2. Measured driver facts (`ClickHouse.Driver` 1.4.0)

All facts below were measured in the D0 binding step; sources:
`.pdca/T328/rv1/d0-bindings.md`, `.pdca/T328/rv2/d0b-reset-driver-repro.md`,
`.pdca/T328/rv3/def-328-04-driver-binding.md`.

- Package `ClickHouse.Driver` 1.4.0 (`Directory.Packages.props`), referenced transitively through
  `src/nextorm.clickhouse/nextorm.clickhouse.csproj`. Upstream repository
  `https://github.com/ClickHouse/clickhouse-cs`, commit `e26ccd238b26744dbb1da0feb86ed2ea487e76e7`
  (from `clickhouse.driver.nuspec`).
- The connection-string keyword `Timeout` is expressed in **seconds** (`double`), mapped to a
  `TimeSpan` property, default **120 s**
  (`ClickHouse.Driver/ADO/ClickHouseConnectionStringBuilder.cs:144-153`,
  `ClickHouseDefaults.cs:93`).
- `Timeout=0` is **accepted** and means an infinite `HttpClient.Timeout`
  (`DefaultPoolHttpClientFactory.cs:17`); it is not a zero-second timeout.
- Non-numeric text **silently falls back to 120 s** (the driver's `double.TryParse` guard);
  a negative value throws `InvalidOperationException("Timeout cannot be negative")` in
  `ClickHouseClientSettings.Validate()`.
- `ClickHouseCommand.CommandTimeout` exists but is **not honoured**
  (`ClickHouseCommand.cs:47-48`).
- The ADO surface does **not** translate transport failures: query send is a pass-through and the
  only catch rethrows unchanged. A client timeout therefore surfaces as the BCL
  `TaskCanceledException`/`OperationCanceledException` (with a `TimeoutException` inner); a
  connection reset surfaces as a `ClickHouseServerException` (pre-response RST, `ErrorCode = -1`,
  no inner chain) or a bare `HttpIOException` (partial-response truncation).
- `ClickHouseServerException : DbException`; the server error code is the inherited
  `DbException.ErrorCode`, `-1` when the response code cannot be parsed.

## 3. Setting `NEXTORM_CLICKHOUSE_HTTP_TIMEOUT_SECONDS`

Implemented by `tests/Shared/ClickHouseRetry/ClickHouseTestTimeout.cs` and consumed by
`ClickHouseTestProvider.EffectiveConnectionString()`.

### 3.1 Precedence

Resolved explicitly, in this order:

1. `NEXTORM_CLICKHOUSE_HTTP_TIMEOUT_SECONDS` (the knob), when present;
2. a `Timeout` key already present in the resolved connection string;
3. the driver default of **120 s**.

The precedence is decided in code (`ClickHouseTestTimeout.Build`), never by duplicate-key ordering
in the connection string. With a present knob the key is written through
`DbConnectionStringBuilder`, so an existing `Timeout` is replaced by value. With an absent knob the
operator's connection string is returned **byte-identical** — the very same instance — so an
operator-supplied `Timeout=0` keeps its infinite meaning and is not normalised, validated or
rewritten.

### 3.2 Rejected values

A present knob must parse as a positive 32-bit integer under `NumberStyles.None`. The rejection
domain is therefore: empty, whitespace-only, non-integer text, `0`, negative, fractional (`1.5`),
exponent (`1e3`), a leading `+` or space, and 32-bit overflow. Every rejected value throws
`ClickHouseTestTimeoutConfigurationException`, a dedicated `InvalidOperationException` subclass
whose message names the variable and the accepted domain. There is **no fallback** to the driver
default or to the connection string, and an invalid knob is validated before the memoisation cache
is touched, so it fails on every call rather than being memoised into a usable result.

### 3.3 Diagnostics

When the knob replaces a timeout that was already present in the connection string, exactly **one**
credential-free diagnostic line is written per process:

```text
NEXTORM_CLICKHOUSE_HTTP_TIMEOUT_SECONDS override: timeout_source=knob effective_seconds=<int> connection_string_timeout_overridden=true
```

The diagnostic is emitted at most once per resolved configuration (`EmitDiagnosticOnce`), and
results are memoised per `(connectionString, knob)` pair. A rejected knob value is echoed only when
it is demonstrably safe (short printable ASCII with no separators or credential-like words);
otherwise the message says the value is not echoed because it may contain credentials. Neither the
diagnostic nor the exception message ever contains the connection string.

## 4. Opt-in model

Implemented in `tests/Shared/ClickHouseRetry/ClickHouseReadOperation.cs`,
`ClickHouseReadRetry.cs`, `ClickHouseReadRetrier.cs` and `ClickHouseRetryLedger.cs`.

### 4.1 Categories

`ClickHouseReadOperation.Audited(testIdentity, operationIdentity, category)` is the only
constructor for a non-default operation. Three read categories are retry-eligible:

- `PointRead` — a single-row lookup;
- `MaterialisedListRead` — a bounded list materialised before control returns to the test;
- `ScalarAggregate` — `COUNT`, `MAX`, and similar scalar aggregates over a read-only query.

`StreamedRead`, `Write`, `Ddl`, `FixtureMutation` and `UnknownEffect` are rejected at construction
with `ArgumentException` naming the category. A streamed read is excluded because a partially
consumed stream cannot be replayed safely: the second attempt would restart mid-sequence and the
outstanding enumeration is not guaranteed to be quiescent when the first attempt fails.

### 4.2 One retry-controlled operation per test invocation

`ClickHouseReadRetrier.ExecuteAsync` calls `ClickHouseRetryLedger.Default.TryClaimOperation` before
the first attempt starts. A second claim for the same test identity throws `InvalidOperationException`
before any operation runs. This is what keeps a multi-read test from replaying more than one
operation and what keeps the attempt accounting honest when a first attempt simply succeeds.

### 4.3 Enrollment

Enrollment is a per-call-site decision: the site's synchronous terminal becomes its async
counterpart and is wrapped by
`ClickHouseReadRetry.ExecuteAsync(operation, read, cancellationToken)`. The delegate contains only
the read; every assertion, expected exception and test name stays outside it.

The finite manifest is `.pdca/T328/rv2/enrollment-manifest.json`, with the audit and per-class
breakdown in `.pdca/T328/rv2/ev09-call-site-audit.md` §10. Final state: **42 methods in 7 classes**
enrolled, one site per method.

| Class | Enrolled methods |
| --- | --- |
| `ClickHouseHashAndFormatTests` | 2 |
| `ClickHouseFunctionGapTests` | 5 |
| `ClickHouseFunctionExecutionTests` | 10 |
| `ClickHouseT332StringFormatInvariantProviderTests` | 3 |
| `ClickHouseDateAndSessionExecutionTests` | 8 |
| `ClickHouseTypedCteIntegrationTests` | 1 |
| `ClickHouseExtendedFunctionTests` | 13 |
| **Total** | **42** |

Seven further audited sites in the same classes are deliberately **not** enrolled because their
test invocation already spends its single operation budget: S27 and S28 (`GroupingModifiers` rollup
and cube), S30 (`OffsetOnlyPaging` baseline), S38 (`ConditionalAndWindowHelpers` conditional read),
S45 (`T277_ArrayFilter_NegativeControls` no-match read), S48 and S49 (`T277_HigherOrder_SourceMatrix`
`IntCell`/`BoolCell`). The enrolled site is the terminal immediately before the method's final
assertion, so no invocation can claim twice. The one structural change forced by this rule is the
per-row cell helper in `T277_HigherOrder_SourceMatrix`: `LongCell` became `LongCellAsync` (the
enrolled site) and `ExecuteHigherOrderCell` became `ExecuteHigherOrderCellAsync`; `IntCell` and
`BoolCell` stay synchronous and unenrolled.

## 5. Classifier

Implemented by `tests/Shared/ClickHouseRetry/ClickHouseTransientClassifier.cs`. The decision table
is **exclusion-first** and evaluated over the whole flattened exception chain; message text never
decides anything.

### 5.1 Exclusion order

1. **Caller cancellation** — `cancellationToken.IsCancellationRequested` ⇒ `CallerCancelled`, before
   any other inspection. The exception's own token identity is deliberately **not** compared: the
   driver links its token to the caller's, so a `TaskCanceledException` token is not evidence of
   caller intent (measured). General/unknown cancellation therefore does not retry.
2. **Typed database/server error** — any chain node that is a `DbException`
   (including `ClickHouseServerException`) ⇒ `ServerError`, even when a message contains "timeout".
   This covers ClickHouse code 60 (`UNKNOWN_TABLE`) and code 216
   (`QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING`).
3. **Assertion failure** — any node that is `XunitException` or `AssertionFailedException` ⇒
   `AssertionFailure`. Wrapping an assertion never makes it retryable.

### 5.2 The single positive rule

`ClientHttpTimeout` is returned only when **all** of the following hold:

- every node of the flattened chain is one of the tolerated transport shapes:
  `OperationCanceledException` (and its `TaskCanceledException` subclass), `TimeoutException`,
  `HttpRequestException`, `IOException`, `SocketException`;
- **exactly one** such node is an `OperationCanceledException` whose own inner chain contains a
  `TimeoutException`;
- the caller token is not cancelled.

`IOException` is tolerated because it appears between the driver's cancellation wrapper and the
socket on the measured chain, but it is never a positive match on its own. An
`AggregateException` is flattened (its `InnerExceptions` are walked) yet the `AggregateException`
node itself is not an allowed node, so an aggregate wrapper fails closed. Chain walking is
cycle-safe (reference identity) and bounded to 16 nodes; a longer chain is treated as unknown.

Everything else — a bare `TimeoutException`, a bare `IOException`/`HttpIOException`, an
`HttpRequestException` without a positive transport signal, an unknown wrapper anywhere in the
chain, a reset-shaped `ClickHouseServerException` (including `ErrorCode = -1`), or a
message-only lookalike — classifies as `UnknownShape` and is **non-retryable**.

### 5.3 Connection resets are not retried, and why

Driver 1.4.0 exposes no typed reset signal. Measured: a pre-response RST surfaces as
`ClickHouseServerException` with `ErrorCode = -1` and **no inner chain**; a partial-response
truncation surfaces as a bare `HttpIOException` with no `HttpRequestException` wrapper and no
`SocketException`. Because `-1` also denotes an unparseable genuine server error with the same
exception type and no usable inner chain, treating it as a reset would invert fail-closed
classification and retry genuine server errors. Reset-shaped faults therefore stay `ServerError`
or `UnknownShape`, execute exactly once, and preserve the original exception
(`DEF-328-04`, follow-up #338).

> **Update 2026-10-28 (T362):** the first sentence above is only half true, and the split is now
> measured. A post-response RST surfaces as a **bare `HttpIOException` carrying
> `HttpRequestError.ResponseEnded`** — a typed signal produced by the HTTP layer *below* the driver,
> not by the driver — so it **is** retried (new `ClickHouseFailureKind.TransportResponseReset`).
> A pre-response RST is `ClickHouseServerException`/`ErrorCode = -1`/no inner chain and was measured
> to be **identical** to the driver's own empty-body HTTP 503 path (same type, code and message), so
> it stays fail-closed. Both shapes and the raw evidence:
> `.pdca/T362/rv1/probe-shapes.md`, `.pdca/T362/rv1/probe-driver-shapes.log`; the reproducible form
> is `tests/nextorm.core.tests/ClickHouseRetry/ClickHouseResponseResetTests.cs`. The consequence for
> §9 is recorded there.

## 6. Lifecycle

Implemented by `ClickHouseReadRetrier.ExecuteAsync`.

- **At most two attempts.** `AttemptLimit = 2`; a second failure is recorded and rethrown, never
  repeated. There is no third attempt.
- **Quiescence before classification.** The first attempt is awaited to completion before its
  failure is classified; `EnsureAttemptTerminated` asserts the attempt task `IsCompleted` and
  throws `InvalidOperationException` when quiescence is unproven. A delegate that throws
  synchronously never yields an attempt task and is rethrown without a retry.
- **One cancellable backoff.** The only retry waits on `Task.Delay(TimeSpan.FromSeconds(1),
  cancellationToken)` — one second, cancellable by the caller's token.
- **Cancellation during backoff** is caught as `OperationCanceledException`, records the distinct
  `backoff_cancelled` reason, and starts **no** second attempt.
- **Exactly one additional attempt** after a retryable classification and an uninterrupted backoff.
- **Original exception preserved.** Every failure is rethrown with
  `ExceptionDispatchInfo.Capture(...).Throw()`, so the runner observes the original exception type
  and stack.
- **Results are never transformed.** The value of `T` from the winning attempt is returned
  unchanged; the retrier neither wraps nor disposes it.
- **The scheduled record precedes the delay**, so a recovery can never look like a first-attempt
  success.

## 7. Accounting

### 7.1 Ledger record

`ClickHouseRetryAttemptRecord` carries, per observed attempt outcome:

| Field | Meaning |
| --- | --- |
| `TestIdentity` | The runner display name (`TestContext.Current.Test.TestDisplayName`) |
| `OperationIdentity` | `<Class>.<Method>#<n>` as declared at the call site |
| `Attempt` | 1 or 2 |
| `NextAttempt` | `Attempt + 1` when a retry was scheduled, otherwise `null` |
| `AttemptLimit` | 2 |
| `ReasonCode` | Short sanitised token: `client_http_timeout`, `server_error`, `assertion_failure`, `unknown_shape`, `caller_cancelled`, `backoff_cancelled`, `attempt_succeeded` |
| `ScheduledRetry` | Whether a retry was scheduled after this outcome |
| `Succeeded` | Whether this attempt returned successfully |

No message body, query, connection string or credential enters a record. The ledger
(`ClickHouseRetryLedger`) is thread-safe (`ConcurrentDictionary` + `Interlocked`) and accounting-only:
it never decides whether a retry happens.

### 7.2 Reporter

`tests/nextorm.integration.tests/Support/ClickHouseRetryReporter.cs` is a thin observer, registered
with `[assembly: RegisterRunnerReporter(typeof(NextOrm.TestInfrastructure.ClickHouseRetryReporter))]`.
It declares `IsEnvironmentallyEnabled => true` (so it needs no switch), wraps
`DefaultRunnerReporterMessageHandler`, and forwards **every** runner message unchanged. It only
records test display names and pass/fail outcomes and prints exactly one summary block at assembly
completion — including runs where tests failed. Observation is exception-isolated, so accounting can
never change native behaviour, and native selected/passed/failed/skipped counts are never inflated.

### 7.3 Output format

```text
CLICKHOUSE-RETRY-TEST test=<identity> attempts=<1|2> retried=<true|false> operation_recovered=<true|false> final=<passed|failed|unknown> recovered=<true|false>
CLICKHOUSE-RETRY-SUMMARY retried_tests=<n> additional_attempts=<n> recovered_tests=<n> exhausted_retries=<n> not_recovered_tests=<n> unretried_failures=<n> attempted_once=<n>
```

| Field | Definition |
| --- | --- |
| `attempts` | Highest attempt number observed for the test |
| `retried` | A retry was scheduled at least once |
| `operation_recovered` | Attempt 2 returned successfully |
| `final` | The runner's final outcome for the test identity, or `unknown` when not observed |
| `recovered` | `operation_recovered` **and** `final = passed`; a later assertion failure is never a recovery |
| `retried_tests` | Tests with a scheduled retry |
| `additional_attempts` | Tests that actually executed attempt 2 |
| `recovered_tests` | Tests satisfying `recovered` |
| `exhausted_retries` | Tests whose attempt 2 also failed |
| `not_recovered_tests` | Tests with a scheduled retry that did not satisfy `recovered` |
| `unretried_failures` | Tests with no scheduled retry whose final outcome was failed |
| `attempted_once` | Tests with exactly one attempt and a known final outcome |

## 8. Live verification (actual results)

### 8.1 Environment

- Server **ClickHouse 25.8.33.6**, container image `clickhouse/clickhouse-server:25.8-alpine`
  (`endpoint_container=true`, `endpoint_external=false`), measured by the live `version` probe and
  recorded in `.pdca/T328/rv3/c05-version.log`.
- One shared container lease, `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`,
  `NEXTORM_SKIP_PROVIDERS` unset. Every probe ran against the real server; nothing is simulated.

### 8.2 The nine isolated scenarios

Each invocation selected exactly one case via `-trait "T328Scenario=<scenario>"`; the full exit map
is `.pdca/T328/rv3/c05-exits.json`.

| Scenario | Exit | Result and key evidence |
| --- | --- | --- |
| `version` | 0 | `25.8.33.6`, container endpoint, correlatable identity — `.pdca/T328/rv3/c05-version.log` |
| `timeout-recovery` | 0 | chain `TaskCanceledException > TimeoutException > TaskCanceledException > IOException > SocketException`; `client_http_timeout`; 2 attempts, 2 records, 1 delay of 1000 ms; `recovered=true final=passed` — `.pdca/T328/rv3/c05-timeout-recovery.log` |
| `reset-not-retried` | 1 | expected-negative: 1 attempt, 0 retry records, original `ClickHouseServerException` with `ErrorCode=-1`; endpoint proof `target_connected=true`, `client_bytes=671`, `server_bytes=0`, `reset_stage=request_forwarded` — `.pdca/T328/rv3/c05-reset-not-retried.log`, `.pdca/T328/rv3/ev10-endpoint-proof.json` |
| `retry-exhaustion` | 1 | 2 attempts, 2 records, both `client_http_timeout` with the identical measured chain, one 1000 ms delay, no third attempt; `exhausted_retries=1 not_recovered_tests=1` — `.pdca/T328/rv3/c05-retry-exhaustion.log` |
| `assertion-failure` | 1 | 1 read, genuine `42 != 41` assertion, 0 retry records — `.pdca/T328/rv3/c05-assertion-failure.log` |
| `unknown-table` | 1 | genuine server code `60` (`unknown_table`), 1 attempt — `.pdca/T328/rv3/c05-unknown-table.log` |
| `code216` | 1 | genuine server code `216` (`QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING`), 1 attempt — `.pdca/T328/rv3/c05-code216.log` |
| `unsafe-operation` | 1 | 5 ineligible categories rejected at construction, 0 commands executed — `.pdca/T328/rv3/c05-unsafe-operation.log` |
| `recovered-then-assertion` | 1 | 2 attempts, `operation_recovered=true final=failed recovered=false`, `not_recovered_tests=1` — `.pdca/T328/rv3/c05-recovered-then-assertion.log` |

Expected-nonzero exits are assertions inside deliberately failing probes, not verification failures:
each probe asserts its facts first and then fails so that the runner records a genuine failed test.
`reset-not-retried` is the declared expected-negative case for V18.

### 8.3 Canonical suite and coverage

- Canonical integration suite (C04, diagnostic probes excluded): **5285 total / 0 failed /
  197 skipped**, exit 0; the skip gate reports `OK: 197 skips match the pinned budget`
  (`.pdca/T328/rv3/integration.log`, `.pdca/T328/rv3/c06-integration-skipgate.part3.log`). The
  reporter emitted one summary line with `attempted_once=66`, i.e. 66 read operations ran once with
  no retry on a fault-free run.
- Solution coverage (C06): line **90.1 %** (56 981/63 175) and branch **83.9 %** (32 595/38 849),
  gates 85/75 cleared (`.pdca/T328/rv3/solution-coverage/Summary.txt`). The solution collect exits
  1 because of the one C02 failure recorded in §10.4, later shown to be attributable interference and
  fixed after the run.
- Integration coverage (C06): line **81.5 %** (49 274/60 393) and branch **75.5 %**
  (29 366/38 855), gates 65/75 cleared (`.pdca/T328/rv3/integration-coverage/Summary.txt`).

## 9. Blocked obligations and completion verdict

The following obligations are **BLOCKED-by-driver**, not waived:

**BLOCKED-by-driver: AC01 reset half; AC09 reset half; V02; reset half of EV01/EV02/EV06 (DEF-328-04). Trigger: driver exposes a typed reset signal.**

> **Update 2026-10-28 (T362, #338 option A):** the **post-response** half is no longer blocked and is
> delivered — `HttpIOException`/`ResponseEnded` is a typed signal from the HTTP layer, and it is now
> retried for audited read-only operations. The **pre-response** half remains fail-closed with a
> measured reason (identical to the driver's empty-body HTTP 503 surface) instead of an assumed one.
> This paragraph is the historical T328 verdict and is left in place; the current policy is the
> classifier's XML doc and `tests/nextorm.core.tests/ClickHouseRetry/ClickHouseResponseResetTests.cs`.

- Driver limitation record: `.pdca/T328/rv3/def-328-04-driver-binding.md`.
- Follow-up issue: **#338** — `https://github.com/AlexeyShirshov/nextorm/issues/338`
  (open, milestone `1.0.9-rc4`).
- Until that trigger is met, every reset-shaped fault executes once, fails closed, and preserves
  the original exception; no reset recovery is claimed.
- The required completion wording is:
  **PASS on r3 scope; reset obligations BLOCKED-by-driver**.
  An unqualified PASS is never issued.

## 10. Execution bindings and deviations

### 10.1 D328-1 — working directory

Plan §10 froze `/home/alex/sources/nextorm` as the working directory for every command. The cycle
executed the frozen commands in its isolated collection worktree
`/home/alex/sources/nextorm-worktrees/rc4/t328` (branch `collection/rc4/task-10-1`). All literal
relative paths are unchanged, and the evidence root stays in-repo (`.pdca/T328/rv3/`) so
`pdca_evidence.py lint` path-escape rules hold. `.pdca/**` is git-ignored, so evidence is not
committed; the contract, status file and this document are.

### 10.2 D328-2 — helper drift

The worktree executes the **committed** copy of `.pdca/scripts/validate_inner_loop.py`
(56 122 B, sha256 `5c49f6d4647fe7ddf64f51afa4ac38fb915204428719ba15a6d17f2fca18e86f`). The main tree
carries an uncommitted modified copy (56 130 B, sha256
`fd2e6408f4de57eae4990ba81347f8efafe09520aa5a597cf8927dbaefc2acb8`). T328 does not edit the helper;
the drift is recorded, not reconciled.

### 10.3 Corrected C06 argv bindings

The frozen C06 forms were corrected twice after measurement; both corrected argv are recorded and
the scope is unchanged.

1. **Solution collection.** Frozen form
   `dotnet test nextorm.slnx -c Debug --no-build -- -notrait "Category=Diagnostic"`, expanded under
   `dotnet-coverage`, selects **zero tests** in this SDK's Microsoft.Testing.Platform bridge
   (`Zero tests ran / error: 1 / exit 5`). The binding was corrected to one `dotnet-coverage collect`
   wrapper over the repository's documented native runner, one project at a time:

   ```text
   dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o .pdca/T328/rv3/solution.cobertura.xml "bash .pdca/T328/rv3/c06-solution-run.sh"
   dotnet run --project tests/<project> -c Debug --no-build -- -noColor -notrait "Category=Diagnostic"
   ```

   Why: the bridge does not forward `--` arguments, so the frozen form cannot select a case; the
   replacement runs every test project of `nextorm.slnx` with the canonical diagnostic exclusion
   applied and no project excluded.

2. **Integration collection.** Frozen form
   `dotnet test tests/nextorm.integration.tests -c Debug --no-build -- -noColor -notrait "Category=Diagnostic"`
   likewise selected zero tests and produced an empty cobertura (`<packages />`, 178 B). The
   corrected binding is:

   ```text
   dotnet tool run dotnet-coverage collect -s coverage.integration.settings.xml -f cobertura -o .pdca/T328/rv3/integration.cobertura.xml "dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -notrait \"Category=Diagnostic\""
   ```

   Why: the native runner form selects the integration project's 5285 tests (0 failed, 197
   skipped) and makes the gate measurable; settings and thresholds were not changed.

### 10.4 C02 failure: attributable interference, fixed after the run

`NextORM.Core.Tests.TimedDictionaryEvictionTests.ExpireTimedEntriesForTesting_Should_Age_All_Five_ProcessWide_Caches`
(`tests/nextorm.core.tests/Caching/TimedDictionaryEvictionTests.cs:176`) failed with `Expected
DataContextCache.Metadata.ContainsKey(metadataKey) to be False, but found True.` The test was not
broken: the failure was **interference**. `QueryCacheControlsTests` seeds
`DataContextCache.Metadata[SimpleEntity]` (`StoreInCache_False_Should_Not_Mutate_Command_Cache_Flag`,
`Disabled_QueryCache_Should_Rebuild_…`) and did not clear it; `CachedPreparationRecipeTests` is a
second latent leaker. `TimedDictionary.MakeEntry` freezes `Timed = CacheSlidingExpiration > 0` at
insert, so entries created under the zero window are permanently unageable — the leak only bites
when the leaking class runs before `TimedDictionaryEvictionTests`.

The T328 assembly change flipped xunit v3's per-assembly ordering seed (`1371423718` on the branch
versus baseline `1434055667`), which reordered the `QueryCacheControlsTests` block immediately
before the eviction class. Parallelism was irrelevant (the assembly is
`[assembly: Parallelization(Mode = ParallelMode.None)]`; `-parallelMode none` reproduces the
failure).

Fixed in **`f8888a7c`** by making `QueryCacheControlsTests` an `IDisposable` whose `Dispose()`
calls `DataContextCache.Clear()` (one file, +10/−1). The eviction test, its assertions and `src/**`
were untouched; no assertion relaxed, no skip, no reorder.

Proof: two consecutive full `nextorm.core.tests` runs with build — **2739/2739 passed, 0 failed**,
exit 0 (`.pdca/T328/rv3/core-tests-fixed-1.log`, `.pdca/T328/rv3/core-tests-fixed-2.log`); the
eviction class alone **8/8** (`.pdca/T328/rv3/c02-class-alone-postfix.log`); the five T328 classes
**73/73** (`.pdca/T328/rv3/c02-t328-classes-postfix.log`). Controls: baseline `317e03e5` **2666/0**
(`.pdca/T328/rv3/c02-baseline-317e03e5-core-tests.log`), T327 `8b931e23` **2858/0**, pre-fix branch
`a827398b` **2739/1** (2/2).

The failure is therefore attributable and fixed — not pre-existing, not non-attributable, and not
red on baseline. It is a systemic instance, not a one-off: process-wide caches make the suite
order/parallelism-sensitive (third case today), tracked in **#339** —
`https://github.com/AlexeyShirshov/nextorm/issues/339`.
