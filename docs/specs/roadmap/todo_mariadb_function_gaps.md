# TODO: Пробелы функций MariaDB (`NVL`/`NVL2`, `ADD_MONTHS`, `MONTHS_BETWEEN`, `TO_CHAR`/`TO_DATE`/`TO_NUMBER`, `KDF`, `XXH3`, `JSON_DETAILED`/`JSON_COMPACT`, последовательности)
> Tracking issue: [#79](https://github.com/AlexeyShirshov/nextorm/issues/79).

> **Статус: SHIPPED.** MariaDB-only имена добавлены на общую поверхность `MySqlFunctions`
> (`SqlFunctions.MySql`), гейтятся per name через `ISqlDialect.MySqlFunctions`; `MariaDbDialect`
> сообщает их, MySQL отклоняет. Публичная документация:
> [`guide/provider-specific/mysql.md#mariadb`](../../guide/provider-specific/mysql.md) (EN+RU).
> Раздел ниже — сохранённый рабочий план (RFC); итоговые решения — в §«Итог и решения».

> Рабочий план (design RFC). Источник: таблица «Summary: highest-value gaps»,
> строка **MariaDB**, в [`sql-function-coverage-gap.md`](sql-function-coverage-gap.md)
> (§«MariaDB»); новый пункт §4 в
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md).
> Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Фича:** MariaDB — надмножество MySQL, поэтому **базовый набор** (строки, даты, JSON, hash,
  `UUID_TO_BIN`, …) закрывается в [`todo_mysql_function_gaps.md`](todo_mysql_function_gaps.md);
  этот todo добавляет **MariaDB-специфичные** имена:
  `REGEXP_INSTR`/`REGEXP_REPLACE`/`REGEXP_SUBSTR` (расширенный regexp-набор),
  `NVL`/`NVL2`, `ADD_MONTHS`, `MONTHS_BETWEEN`, `TO_CHAR`/`TO_DATE`/`TO_NUMBER`,
  `KDF`, `XXH3`/`XXH32`, `JSON_DETAILED`/`JSON_COMPACT`, `NEXT VALUE FOR`/`NEXTVAL`/`SETVAL`/`LASTVAL`
  (последовательности).
- **Критерий приёмки:** каждое имя рендерится нативной формой MariaDB; **MySQL его отклоняет**
  (`NotSupportedException`), т.к. имя MariaDB-only; SQL-gen тесты + MariaDB-интеграция.
- **Prerequisite:** каркас поверхности `MySqlFunctions` из MySQL-todo (MariaDB наследует её).

## 2. Текущее состояние (проверено по коду)

| Слой | Где | Сейчас |
|---|---|---|
| Публичная поверхность | `src/nextorm.core/Query/SqlFunctions.*.cs` | `MariaDbFunctions` нет; `MySqlFunctions` — по `todo_mysql_function_gaps.md` |
| Диалект | `src/nextorm.mariadb/MariaDbDialect.cs` | наследует MySQL-диалект; своих функций-хуков нет |
| Покрытие | CommonFunctions | `NVL` можно эмулировать как `coalesce`, но `NVL2`/`TO_CHAR`/`TO_NUMBER`/`MONTHS_BETWEEN` аналогов нет |

## 3. Матрица провайдеров (провайдер × форма)

Источники: MariaDB built-in functions (string, numeric, date-time, json, aggregate, information,
sequence, encryption-hashing-compression, regular-expressions); MySQL 8.4 reference; PostgreSQL 18;
Microsoft Learn T-SQL; SQLite `lang_corefunc`/`json1`; ClickHouse function reference.

| Функция | MariaDB | MySQL | PostgreSQL | SQL Server | SQLite | ClickHouse |
|---|---|---|---|---|---|---|
| `REGEXP_INSTR`/`REGEXP_REPLACE`/`REGEXP_SUBSTR` | есть | 8.0+ есть | `regexp_instr`/`regexp_replace`/`regexp_substr` | `REGEXP_*` (2025) | — | `extract`/`replaceRegexp*` |
| `NVL(a,b)` | есть | нет (`IFNULL`) | `coalesce` | `ISNULL` | `ifnull`/`coalesce` | `ifNull` |
| `NVL2(a,b,c)` | есть | нет | `CASE WHEN a IS NULL` | — | — | `if(isNull(a),c,b)` |
| `ADD_MONTHS(d,n)` | есть | нет (`DATE_ADD` INTERVAL) | `d + n * interval '1 month'` | `DATEADD(MONTH,n,d)` | `date(d,'+'||n||' months')` | `addMonths` |
| `MONTHS_BETWEEN(a,b)` | есть | нет | `extract(year…)*12 + …` | `DATEDIFF(MONTH,…)` | — | `age`/`dateDiff` |
| `TO_CHAR`/`TO_DATE`/`TO_NUMBER` | есть (Oracle-compat) | нет | `to_char`/`to_date`/`to_number` | `FORMAT`/`CONVERT`/`CAST` | — | `formatDateTime`/`toDate*`/`toFloat64` |
| `KDF(password, salt, kdf, …)` | есть | нет | — | — | — | — |
| `XXH3`/`XXH32` | есть | нет | — | — | — | `xxh3`/`xxHash32` |
| `JSON_DETAILED`/`JSON_COMPACT` | есть | нет | `jsonb_pretty`/`json` | — | `json`/`jsonb` | —/`prettyPrintJSON` |
| `NEXT VALUE FOR`/`NEXTVAL`/`SETVAL`/`LASTVAL` | есть | нет (AUTO_INCREMENT) | `nextval`/`setval`/`lastval` | `NEXT VALUE FOR` (только next) | — | — |

**Единообразие провайдеров:** имена MariaDB-only или Oracle-совместимые; почти у каждого есть
эмуляция, но точная семантика отличается (`NVL` vs `coalesce` — совпадает; `TO_CHAR`/`TO_NUMBER` —
Oracle-шаблон, не PG/SQL Server; `ADD_MONTHS` — перенос дня; `MONTHS_BETWEEN` — дробные месяцы).
Решение: **MariaDB-only поверхность** (общая с MySQL, но MySQL отвечает `Supports == false` на
эти имена). Пары с точным совпадением семантики (`NVL` ≈ `coalesce`, `ADD_MONTHS` ≈ `DATEADD`/
`addMonths`) — кандидаты на промотирование в `CommonFunctions` второй волной.

## 4. Ближайший CLR-аналог и тир

- **tier (b)**: MariaDB-имена + транслятор + per-name capability на общем `MySqlFunctions`.
- `NVL(a,b)` можно было бы свести к `coalesce`, но имя MariaDB-специфичное; держать как отдельный
  член и рендерить `NVL` (не подменять `coalesce`, чтобы SQL был предсказуем).
- `NEXT VALUE FOR` — не скаляр-функция, а **синтаксис** (`NEXT VALUE FOR seq`); рендер отличается от
  `nextval('seq')`, требует отдельной ветки транслятора, не `[SqlFunction]`.

## 5. Дизайн и публичный API (выбрано)

Члены добавляются на **ту же** общую поверхность `MySqlFunctions` (MariaDB наследует каркас MySQL,
как и требует prerequisite): `MariaDbDialect.MySqlFunctions` реализует MariaDB-имена, а
`MySqlDialect.MySqlFunctions` их **не** поддерживает (`Supports == false`), поэтому MySQL отклоняет
их с `NotSupportedException`. Отдельная `MariaDbFunctions`/`IMariaDbFunctions`-поверхность **не
заводится** — это продублировало бы `IMySqlFunctions` (параллельная иерархия) и разошлось бы с
планом §6 MySQL-todo («Общий `IMySqlFunctions` capability-объект»); seam для этого уже заложен в
`MariaDbNativeFunctions` («This is the seam a follow-up MariaDB-only surface extends with its own
Supports names»). Транслятор и in-memory-отклонение переиспользуются без изменений.

```csharp
public string? regexp_substr(string? value, string? pattern);
public int? regexp_instr(string? value, string? pattern);
public string? regexp_replace(string? value, string? pattern, string? replacement);
public T? nvl<T>(T? value, T? fallback);
public T? nvl2<T>(T? value, T? whenNotNull, T? whenNull);
public DateTime? add_months(DateTime? date, int months);
public double? months_between(DateTime? a, DateTime? b);
public string? to_char(DateTime? value, string? format);
public DateTime? to_date(string? value, string? format);
public double? to_number(string? value, string? format);
public byte[]? kdf(string? password, string? salt, string? info, string? kdfName);
public ulong? xxh3(string? value);
public long? xxh32(string? value);
public string? json_detailed(string? json);
public string? json_compact(string? json);
public long? next_value_for(string? sequence);
public long? nextval(string? sequence);
public long? setval(string? sequence, long value);
public long? lastval(string? sequence);
```

- Типизация по докам MariaDB: `XXH3` → `BIGINT UNSIGNED` (`ulong?`), `XXH32` → `INT UNSIGNED`
  (в движке нет reader-пути для `uint`, поэтому результат расширяется до `long?`); `KDF` возвращает
  binary → `byte[]?`, `TO_NUMBER` всегда `DOUBLE` → `double?`).
- `NVL`/`NVL2` дженерики; рендер `NVL`/`NVL2` без подмены на `coalesce`.
- Имена последовательностей (`next_value_for`/`nextval`/`setval`/`lastval`) принимают имя как
  строковый литерал и рендерятся **идентификатором** (`next value for s`, `nextval(s)`) — кавычки
  снимаются рендерером.

## 6. Диалект-план

- Общий `IMySqlFunctions` (из MySQL-todo): `MariaDbNativeFunctions` (декоратор) реализует
  MariaDB-only имена (`Supports == true`, `Render`) и делегирует наследуемые MySQL-имена;
  `MySqlNativeFunctions` — `false` для этих имён, поэтому MySQL их отклоняет.
- Транслятор: переиспользуется существующий `MySqlFunctionTranslator.cs` (распознаёт по
  `DeclaringType`); `NEXT VALUE FOR` рендерится префиксным синтаксисом через capability `Render`.
- Никаких family-флагов: поддержка решается per name.

## 7. Этапы внедрения

1. Regexp + Oracle-compat-условные (`regexp_*`, `nvl`, `nvl2`); SQL-gen.
2. Дата/числа (`add_months`, `months_between`, `to_char`/`to_date`/`to_number`); SQL-gen.
3. Hash/JSON (`kdf`, `xxh3`, `json_detailed`/`json_compact`); SQL-gen.
4. Последовательности (`next_value_for`/`nextval`/`setval`/`lastval`); интеграция.
5. Документация EN+RU; обновить `sql-function-coverage-gap.md`.

## 8. План тестов

- SQL-gen: `tests/nextorm.mariadb.tests/SqlGenerationTests.cs` + `MariaDbDialectTests.cs`; и негатив
  в `tests/nextorm.mysql.tests` (MariaDB-only имена → `NotSupportedException`).
- Интеграция: `tests/nextorm.integration.tests/MariaDbSpecificTests.cs` — sequence (`NEXT VALUE FOR`),
  `NVL2`, `MONTHS_BETWEEN`, `TO_CHAR`.
- Coverage: `nextorm.mariadb` **не входит** в `coverage.settings.xml` — числа не сдвинут; указать явно.

## 9. Открытые вопросы (решено)

1. Общая `MySqlFunctions` с per-name гейтом (через `MariaDbNativeFunctions`); отдельная
   `MariaDbFunctions` не заводится (§5).
2. `nvl` остаётся MariaDB-only и рендерится как `NVL` (не подменяется `coalesce`) — промоушен в
   `CommonFunctions` отдельной волной.
3. `TO_CHAR`/`TO_DATE`/`TO_NUMBER` поддержаны сейчас (SQL-gen); `TO_DATE` — с MariaDB 12.3,
   `TO_NUMBER` — с 12.2, поэтому контейнерная интеграция на `mariadb:11.4` их не покрывает.
4. `KDF` типизирован `byte[]?` (binary), сигнатура `(password, salt, info, kdf_name)`.
5. `XXH32` добавлен рядом с `XXH3` (`long?`/`ulong?`); обе функции — с MariaDB 13.1, SQL-gen-only.
   `MONTHS_BETWEEN` — с MariaDB 12.2, тоже SQL-gen-only.

## 10. Файлы к изменению

- Правки: `src/nextorm.core/Query/SqlFunctions.MySql.cs` (MariaDB-имена на общей поверхности),
  `src/nextorm.core/Visitors/MySqlFunctionTranslator.cs`, `src/nextorm.core/DataContext/Dialect/*`,
  `src/nextorm.mariadb/MariaDbDialect.cs` (реализация MariaDB-имён), `src/nextorm.mysql/MySqlDialect.cs`
  (гейт).
- Тесты: `tests/nextorm.mariadb.tests/*`, `tests/nextorm.mysql.tests/*`,
  `tests/nextorm.integration.tests/MariaDbSpecificTests.cs`.
- Доки: `docs/guide/provider-specific/mariadb.md` (+RU), `docs/guide/11-scalar-functions.md` (+RU),
  `docs/providers/mariadb.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/roadmap/sql-function-coverage-gap.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр §5 — 1 блокер (перехват диспетча, ×5 имён) + 1 блокер контракта**.

- **[OCP]/[LSP] 🔴** Name-based диспетчер перехватит `regexp_instr`/`regexp_replace`/`to_char`/`to_date`/`to_number`: `:72,73,78,79,80` повторяют имена `PostgresFunctions` из `ExtendedScalarFunctionTranslator.DirectFunctions` (`ExtendedScalarFunctionTranslator.cs:35,37,38,39`), который матчит по имени (`:65`) и вызывается раньше провайдерных (`NormSqlTranslator.cs:452`); на MariaDB `SupportsExtendedScalarFunctions == false` → `RequireExtended` бросит до рендера MariaDB-формы. Fix: гейт по `DeclaringType == typeof(MySqlFunctions)` либо порядок транслятора (прецедент `JsonSqlTranslator.cs:30-33`).
- **[ISP]/[LSP] 🟡** MariaDB-only имена на поверхности `SqlFunctions.MySql` (`:53-54,98`), при этом MySQL отвечает `Supports == false` → публичное имя вводит в заблуждение (план сам признаёт §9.1 `:121`). Fix: provider-нейтральное имя поверхности либо отдельная `MariaDbFunctions`; зафиксировать до объявления API.
- **[DRY] 🟡** План полностью опирается на `MySqlFunctions`/`IMySqlFunctions` из `todo_mysql_function_gaps.md` (`:21,59,97-98`); если владелец выберет отдельную `MariaDbFunctions` (альтернатива `:91-92`), план невалиден. Fix: зафиксировать prerequisite и синхронизировать после решения по MySQL-плану.
- **[TYPE] 🟡** `kdf` — `string?` (`:81`) vs бинарный/digest-подобный возврат; §9.4 (`:124`) оставляет выбор. Fix: решить §9.4 до реализации (предпочтительно `byte[]?`).
- **[TYPE]/[OCP] ℹ️** `next_value_for` (`:62-63,85,99-100`): `NEXT VALUE FOR seq` — синтаксис, не `name(args)`, ломает контракт `Render(name,args)`. Fix: рендерить отдельной ветвью транслятора, зафиксировать в §5/§6.
## 11. Итог и решения (shipped)

- **Публичная поверхность:** `MySqlFunctions` выросла с 28 до **47** членов (+19 MariaDB-only);
  маркер прежний — `SqlFunctions.MySql`. Отдельная `MariaDbFunctions`/`IMariaDbFunctions` не заведена.
- **Диалект:** `MariaDbNativeFunctions` (декоратор `IMySqlFunctions`) получил набор MariaDB-only имён
  (`IsMariaDbOnly`) поверх наследуемых MySQL-имён и исключения `uuid_to_bin`/`bin_to_uuid`;
  `MySqlNativeFunctions` их не сообщает ⇒ MySQL отклоняет с `NotSupportedException`.
- **Транслятор / in-memory:** переиспользованы `MySqlFunctionTranslator` и
  `InMemoryScalarFunctionRewriter` (отклоняет любой `MySqlFunctions`-член) — правок не потребовалось.
- **Типизация:** `xxh3 -> ulong?` (BIGINT UNSIGNED), `xxh32 -> long?` (INT UNSIGNED, `uint` не
  поддержан reader-путём движка), `kdf -> byte[]?` (binary), `to_number -> double?` (DOUBLE).
  Имена последовательностей принимаются строкой и рендерятся идентификатором.
- **Версии MariaDB:** `regexp_*`/`nvl`/`nvl2`/`json_detailed`/`json_compact`/последовательности —
  старые; `add_months`/`to_char` 10.6+; `kdf` 11.3+; `months_between`/`to_number` 12.2+;
  `to_date` 12.3+; `xxh3`/`xxh32` 13.1+. Контейнер (`mariadb:11.4`) покрывает первую группу;
  остальные — SQL-gen-only.
- **Тесты:** SQL-gen MariaDB **13/13**, MySQL-негатив **1/1**, in-memory **2/2**, контейнерно
  MariaDB `~MariaDbFunctions` **13/13** на `mariadb:11.4`.
- **Coverage:** `nextorm.mariadb`/`nextorm.mysql` не входят в `coverage.settings.xml` — числа не
  сдвинуты (SQL-gen добавлен независимо).
