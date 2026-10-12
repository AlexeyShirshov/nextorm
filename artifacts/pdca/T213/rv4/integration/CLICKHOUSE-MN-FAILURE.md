# T213 D04-S — ClickHouse scalar M:N blocker (rv=4)

## Verdict

E-INT-clickhouse and T213-EC-SCALAR-clickhouse are **OPEN**. The required r=4 scope
(scalar M:N standalone + through `SingleQuery`) cannot be verified on ClickHouse: the
focused regression test fails at materialization with a provider type mismatch.

## Exact failure

- test: `NextORM.Integration.Tests.ClickHouseJoinIntoRemainderTests.JoinInto_ManyToMany_WithSingleQueryLoadWith_ShouldStitchTagsAndNotes`
- command: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class nextorm.integration.tests.ClickHouseJoinIntoRemainderTests -noColor`
- exit code: **1** (1 selected, 1 failed, 0 skipped)
- log: `artifacts/pdca/T213/rv4/integration/clickhouse-remainder-focus.log`
- exception: `System.InvalidCastException: Unable to cast object of type 'System.UInt64' to type 'System.Int32'.`
  - `ClickHouse.Driver.ADO.Readers.ClickHouseDataReader.GetInt32(Int32 ordinal)`
  - `src/nextorm.core/DataContext/QueryExecutor.cs(767,0) QueryExecutor.ToList`
  - `tests/nextorm.integration.tests/ClickHouseJoinIntoRemainderTests.cs(25,0)`

Shared full-suite run (all six providers, `integration.log`): exit 1, 3731 total,
**1 failed** (this test only), 197 skipped, 0 errors. SQLite / PostgreSQL / SQL Server /
MySQL / MariaDB executed their JoinInto cases green (see per-provider logs).

## Root cause (observed)

`JunctionLinkSourceFactory.BuildSource` renders the many-to-many junction as a derived
`row_number() over (partition by <fkP>, <fkC> order by <fkC>) as Occurrence` link source.
`JoinIntoLink<,>.Occurrence` is `int`, so the row mapper reads it with
`IDataRecord.GetInt32`. ClickHouse `row_number()` returns `UInt64`, which the driver
refuses to return through `GetInt32`. SQL Server/PostgreSQL/MySQL/MariaDB return a value
the driver accepts (their M:N + SingleQuery cases pass in the same run), so this is a
ClickHouse-only reader/dialect gap on the newly enabled scalar M:N path.

## Scope note

This is a **provider-layer correctness gap**, not the composite-junction work carried by
#223. A fix requires a product/design decision (a ClickHouse dialect wrap of the window
result, mirrored on the existing `WrapCount` hook, plus the SQL-gen expectation update).
The r=4 plan defines D04-S as *verification*; no plan unit authorizes this provider change,
so it is reported as a blocker for planner/escalate rather than implemented here.

## Test-fixture correction applied (in-scope)

`ClickHouseTestProvider.EnsureSeeded` did not create the `eager_note` Memory table that
`NavConScenarios`/`ClickHouseJoinIntoRemainderTests` insert into and load; the oldest
observed failure was `Table default.eager_note does not exist`. The drop/create was added
so the required case can actually execute and expose the real blocker above.

## Coverage effect (informational)

`dotnet-coverage collect` over the whole solution exited 2 solely because the bundled
solution test run includes this one failing test; `reportgenerator` exited 0 and the
filtered thresholds are met (line 88.6% >= 85, branch 80.7% >= 75). See
`artifacts/pdca/T213/rv4/coverage/`.
