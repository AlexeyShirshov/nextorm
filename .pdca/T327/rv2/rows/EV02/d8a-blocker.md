# D8a blocker - two product defects exposed by the live six-provider run

No product code was changed for these (D8a scope forbids it); tests were not weakened.

## Defect 1 - projection-only text parameters bypass the MySQL-family hex specialization

**Symptom.** `MySqlStringLiteralSafetyExecutionTests` and `MariaDbStringLiteralSafetyExecutionTests`
fail 9 of 12 `(sql_mode x NoBackslashEscapes)` cells. The 3 passing cells are exactly those where the
driver flag agrees with the live server mode. Failures:

- server `NO_BACKSLASH_ESCAPES` + flag absent/false: a captured `"\\"` comes back `"\\\\"` (doubled).
- server default/`ANSI_QUOTES` + flag true: server parse error
  `You have an error in your SQL syntax ... near ''\' from sls_*_probe limit 1`.
- post-mode-change values follow the same mismatch rule.

**Root cause (verified).** A captured value used only in the SELECT list is rendered into the separate
`selectBuilder` and inserted with `RelocateInsertion`, but its recorded placeholder occurrence is never
merged into the statement capture:

- `src/nextorm.core/DataContext/SqlBuilder.cs:559` (select item appended) and `:592-594`
  (`selectText` inserted, only `root?.RelocateInsertion(...)`; no occurrence merge).
- `src/nextorm.core/DataContext/SqlSourceRenderer.cs` `MakeColumn` returns text only.
- Reflection on the prepared command: projection template `OccurrenceCount = 0` (`Bindings=1`) while
  the same captured value in a predicate yields `OccurrenceCount = 3`.
- `src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs:335,383` - `SpecializeMySql` runs over
  `template.Occurrences`, so nothing is lowered; the value stays a raw `MySqlParameter` and the
  driver's client-side text escaping (governed by the connection's `NoBackslashEscapes`, not the live
  session mode) decides what the server stores.

Repro (MySQL, one cell): `SET SESSION sql_mode='NO_BACKSLASH_ESCAPES'` on the context connection, then
`var v = "\\"; ctx.From<T>().Limit(1).Select(e => v).First()` returns `"\\\\"`.

This violates AC03/AC11 ("no dependence on driver text escaping") for the SELECT parameter form that
D8's dispatch explicitly requires (`Select(e => v)`).

## Defect 2 - ClickHouse captured `char` parameter is rejected by the driver

**Symptom.** `ClickHouseStringLiteralSafetyExecutionTests.ClickHouse_InlineAndParameter_ShouldRoundTripEveryCase`
fails in `CapturedChar` with `System.ArgumentOutOfRangeException: Unknown type: System.Char`.
Stack: `ClickHouse.Driver.Types.TypeConverter.ToClickHouseType`.

**Root cause.** The CLR `char` is passed to the provider parameter unchanged:
`src/nextorm.clickhouse/ClickHouseDataContext.cs:177` `CreateParam(string, object?)` (core abstract
`src/nextorm.core/DataContext/DataContext.cs:641`); the ClickHouse driver maps only known CLR types.
The inline path is fixed and exact; the parameter path needs char -> one-character-string
normalization (the mapping already exists for TVPs at
`src/nextorm.core/DataContext/TableParameterBinder.cs:271`).

## Consequence

D8a is not complete: the MySQL/MariaDB four-mode matrix (EV03/EV-R2/EV-R4) and the ClickHouse
parameter cell (EV02) are red. The D8a test commit was deliberately not made.

## Resolution (binding planner decision: corrections within existing D4)

- Defect 1 fixed by `#327 D4: record select-list parameter occurrences`: `SqlSourceRenderer.MakeColumn`
  registers the column fragment and `SqlBuilder` merges its occurrences at the select-list offset after
  the head insertions. Verified: projection `OccurrenceCount` 0 -> 1; predicate stays 3; full
  MySQL/MariaDB 12-cell matrix 12/12; predicate warm-command regression added.
- Defect 2 fixed by `#327 D4: normalize ClickHouse char parameters`: the ClickHouse parameter subclass
  normalizes `char` to a one-character string at every driver `Value` assignment (cold bind and
  refresh). Verified: ClickHouse class 3/3, captured char exact; assembly surface unchanged.

All seven targeted suites are green with zero failed and zero skipped; see `d8a-live-counts.md`.
