# EV-R5 - D8a evidence (final)

ClickHouse backslash adjacent to apostrophe and trailing backslash: exact inline and captured
(`\'`, `\\'`, `'\`, `a\`, `a\\`, `\`, `\\`, `a\b`, mixed hostile). The previously failing
captured-`char` path is fixed by the ClickHouse parameter normalization
(`EV02/d8a-clickhouse-char.md`); the ClickHouse class is 3/3 green, 0 failed, 0 skipped.
Log: `D8a-clickhouse.log`.

## D8b boundary

Full run: the ClickHouse safety class emitted no failure/skip; the ClickHouse failure is `ClickHouseExtendedFunctionTests.ArrayLibrary_ShouldExecute` (DEF-T327-04, template recorder), not this corpus. Boundary red; row continues.
