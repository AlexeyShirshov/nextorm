# WIP: `digest` / `sha256` (pgcrypto, PostgreSQL)

> Статус: **реализовано** (`todo_postgres.md:58`, ветка `todo-pg`).

> Рабочий план по скиллу `implementing-todo-features`. Источник: `todo_postgres.md:58`,
> `todo_phase2.md` → PostgreSQL — не заблокировано.

## Пункт и цель

- Пункт: **`digest` / `sha256`** — скалярные хеш-функции; `digest` требует расширения `pgcrypto`.
- Провайдер-источник: PostgreSQL.
- Критерий приёмки: `PostgresFunctions.digest(data, type)` и `PostgresFunctions.sha256(data)`
  рендерятся в одноимённые PostgreSQL-функции под отдельным флагом `SupportsCryptoFunctions`;
  остальные провайдеры бросают `NotSupportedException`.

## Матрица «провайдер × форма» (шаг 1)

| Провайдер | digest (алгоритм-диспетчер) | sha256 | Источник |
| --- | --- | --- | --- |
| PostgreSQL | `digest(data text\|bytea, type text) returns bytea` — **pgcrypto** | `sha256(bytea) returns bytea` — **ядро** (binary-string function) | https://www.postgresql.org/docs/current/pgcrypto.html , https://www.postgresql.org/docs/current/functions-binarystring.html |
| SQL Server | `HASHBYTES('SHA2_256', data) returns varbinary(32)` (только алгоритм-аргумент) | `HASHBYTES('SHA2_256', data)` | https://learn.microsoft.com/sql/t-sql/functions/hashbytes-transact-sql |
| MySQL | `SHA2(str, 256)` возвращает hex-строку; бинарный вид — `UNHEX(...)` | `UNHEX(SHA2(str, 256))` | https://dev.mysql.com/doc/refman/8.4/en/encryption-functions.html |
| MariaDB | `SHA2(str, 256)` / `UNHEX(...)` | то же | https://mariadb.com/kb/en/sha2/ |
| ClickHouse | `SHA256(str) returns FixedString(32)` (нет строкового type-аргумента) | `SHA256(str)` | https://clickhouse.com/docs/en/sql-reference/functions/hash-functions |
| SQLite | —: хеш-функций в ядре нет | — | https://www.sqlite.org/lang_corefunc.html |
| InMemory | скалярные `SqlFunctions` в in-memory не оцениваются | — | — |

**Единообразие провайдеров.** `digest` — функция-диспетчер pgcrypto по строке алгоритма; прямого
аналога у других провайдеров нет (`HASHBYTES`/`SHA2` принимают фиксированный алгоритм, тип результата
отличается: `varbinary`, hex-`text`, `FixedString`). `sha256` (ядро PostgreSQL, не расширение) идейно
пересекается с `HASHBYTES('SHA2_256', ...)`/`SHA2(...,256)`/`SHA256(...)`, но единая кросс-провайдерная
поверхность хеширования потребовала бы отдельного дизайна (тип результата и тип аргумента различаются:
`bytea`/`varbinary`/hex/`FixedString`), а SQLite не умеет вовсе. В nextorm хеширование уже является
PostgreSQL-only поверхностью (`md5` в `ExtendedScalarFunctionTranslator`), поэтому `digest`/`sha256`
остаются на `PostgresFunctions` под отдельным флагом `SupportsCryptoFunctions` (base `false`,
PostgreSQL `true`); кросс-провайдерный хеш-дизайн — отдельная задача. `digest` дополнительно требует
`CREATE EXTENSION pgcrypto` на сервере (это фиксируется в XML-doc и docs).

## Ближайший C#-аналог и уровень

- CLR-аналога в дереве выражений нет (`System.Security.Cryptography` не транслируется).
- Уровень **(b)**: методы `PostgresFunctions` + ветка `ExtendedScalarFunctionTranslator` + флаг.

## Диалектный план

- `ISqlDialect.SupportsCryptoFunctions` (base `false`), `PostgresDialect` → `true`.
- `ExtendedScalarFunctionTranslator`: ветка `digest`/`sha256` → `RequireCrypto` затем `EmitCrypto`
  (прямой `VisitToString` аргументов, а не `EmitFunction`, потому что `byte[]`-аргумент
  `SqlOperandTranslator.AppendArgument` принял бы за array-операнд и попытался материализовать).
- `MakeCrypto`/`Make*` не нужны: PostgreSQL рендерит одноимённые функции.

## Публичный API

```csharp
public byte[]? PostgresFunctions.digest(string? data, string? type);
public byte[]? PostgresFunctions.digest(byte[]? data, string? type);
public byte[]? PostgresFunctions.sha256(byte[]? data);
```

## План тестов

- SQL-gen PostgreSQL: `digest('abc', 'sha256')`, `digest(@norm_p0, 'sha256')`, `sha256(@norm_p0)`.
- Rejection (SQLite/SQL Server/MySQL/ClickHouse): вызов бросает `NotSupportedException`.
- Прямой хук (`PostgresDialectTests`): `SupportsCryptoFunctions` = `true`.
- Интеграция (`PostgresSpecificTests`): `create extension if not exists pgcrypto`, затем
  `digest('abc','sha256')`/`sha256(bytea)` возвращают 32 байта и совпадают с эталоном
  `ba7816bf...` (реальная PG17).
- Базовая линия покрытия: снимается после сборки.

## Файлы доков/специй

- `docs/specs/roadmap/todo_postgres.md` (чекбокс), `docs/guide/11-scalar-functions.md` + RU,
  `docs/providers/postgres.md` + RU, `docs/specs/roadmap/sql-capabilities-gap-analysis.md`.
