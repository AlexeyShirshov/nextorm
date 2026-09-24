# TODO: C# string-семантика (ordinal-сравнения, format-спецификаторы, culture)
> Tracking issue: [#71](https://github.com/AlexeyShirshov/nextorm/issues/71).

> Рабочий план (design RFC). Gap-анализ: **G12** из
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md); linq2db
> [#5927](https://github.com/linq2db/linq2db/issues/5927) (ordinal `Compare`/`CompareOrdinal`
> сворачиваются в culture-sensitive), [#5921](https://github.com/linq2db/linq2db/issues/5921)
> (format-спецификаторы в `string.Format`/`$"…"` молча теряются). Смежное: nextorm issue `#28`
> «column collation» (в nextorm коллаций нет вовсе). Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## Пункт и цель

Сделать трансляцию C#-строковых операций **семантически верной** и **явно ограниченной**:

1. **Ordinal vs culture-sensitive** — `string.Compare`/`CompareOrdinal`/`Equals(..., StringComparison)`/
   `StartsWith`/`EndsWith`/`IndexOf`/`Contains(..., StringComparison)` и оператор `==`.
2. **Format-спецификаторы** — `string.Format`, интерполяция `$"{x:N2}"`, `x.ToString("yyyy-MM-dd")`,
   выравнивание `,10`.
3. **Culture/whitespace** — `ToUpper`/`ToLower` (locale БД), `Trim()` (Unicode-whitespace в .NET vs
   пробел/char в SQL).

**Критерий приёмки:** ни одна из этих форм не даёт **молча неверный** SQL: либо точная трансляция в
документированном подмножестве, либо `NotSupportedException` с понятным сообщением. Подмножество
покрыто SQL-gen + интеграцией, ordinal-семантика подтверждена и в InMemory.

## Почему это нужно (мотивация)

1. **Тихая неверность — худший класс багов.** В linq2db `#5927` `CompareOrdinal`/ordinal-`Compare`
   маппятся в culture-sensitive SQL (`=`, `like`, `order by`) и дают ложные совпадения; `#5921` теряет
   `:N2`/`:yyyy`. nextorm сегодня «честнее» (не транслирует вовсе), но и **не умеет** — надо закрыть
   подмножеством с контрактом.
2. **Ordinal нужен для детерминизма.** Сравнения по строковым ключам/хешам ожидают побайтовый порядок;
   обычный `=`/`like` в SQL зависит от коллации сервера (SQL Server по умолчанию регистронезависим).
3. **Format — повседневный сценарий** (отчёты, выгрузки). nextorm уже отдаёт `SqlFunctions.Format`
   для дат (docs `guide/11-scalar-functions.md:301`), но `string.Format`/`ToString(format)` не покрыты.
4. **Общий примитив.** Корректный ordinal-вариант и колонковая коллация — один и тот же примитив
   `COLLATE`/binary; выгодно строить вместе с nextorm `#28`.

## Текущее состояние (проверено по коду)

- `StringFunctionTranslator.TryTranslate` (`src/nextorm.core/Visitors/StringFunctionTranslator.cs:20-79`)
  покрывает **только culture-less** instance-методы: `ToUpper/ToLower/Trim/TrimStart/TrimEnd/Substring/
  Replace/Remove/Insert/IndexOf/LastIndexOf/PadLeft/PadRight/Contains/StartsWith/EndsWith/Split`, статики
  `IsNullOrEmpty/Join`. Комментарии прямо исключают CultureInfo/StringComparison-overload:
  `:29` («Only the culture-less overloads are portable») и `:230` («the char and StringComparison
  overloads have no portable SQL form»).
- **`string.Compare`/`CompareOrdinal`/`string.Format`/`ToString(format)` не обрабатываются** → при
  зависимости от колонки падает `NotSupportedException` (`BaseExpressionVisitor.cs:177`, повторно `:195`
  для `string`/`Math`/`DateTime`); константный вызов сворачивается в параметр (`:179-182`) — то есть
  константы считаются в C#, а «живые» выражения не поддержаны (не тихо-неверно).
- `string.Concat` обрабатывается (`BaseExpressionVisitor.cs:171-175` → `EmitStringConcat`); интерполяция
  без format-спецификатора опускается Roslyn до `string.Concat`, а **с** спецификатором — до
  `string.Format`, который не поддержан.
- **Коллаций нет вообще**: `rg -i "collation|collate" src` — 0 совпадений; `ISqlDialect` не имеет
  `Collate`/`SupportsCollation`; `==`/`string.Equals` рендерятся как SQL `=` и наследуют коллацию БД.
- `MakeUpper`/`MakeLower` → `upper(...)`/`lower(...)` (`SqlDialectBase.cs:387/389`) — зависят от локали
  БД; `MakeTrim` (`:391`) — `trim`/`ltrim`/`rtrim` (ClickHouse override `ClickHouseDialect.cs:436`).
- InMemory-контекст компилирует лямбды (`InMemoryDataContext.cs:221`) → выполняет **нативные** C#
  `Compare`/`Format`/`ToUpper`; расхождение SQL/InMemory нужно зафиксировать тестом.

## Провайдерная матрица

Источники: MS Learn (`COLLATE`, `FORMAT`, `String.Compare*` semantics), PostgreSQL 18 (`COLLATE`,
`to_char`), MySQL 8.4 (`COLLATE`, `FORMAT`/`DATE_FORMAT`), MariaDB KB, SQLite (`COLLATE`,
`printf`/`strftime`), ClickHouse (`format`/`formatDateTime`, порядок `String`).

| Возможность | SQL Server | PostgreSQL | MySQL / MariaDB | SQLite | ClickHouse | InMemory |
|---|---|---|---|---|---|---|
| per-expression `COLLATE` | да (`COLLATE name`) | да (`COLLATE "C"`) | да (`COLLATE utf8mb4_bin`) | ограничено (`BINARY`/`NOCASE`/`RTRIM`) | нет | н/д (ordinal) |
| ordinal/binary compare | `COLLATE …_BIN2` | `COLLATE "C"` | `COLLATE utf8mb4_bin` | `COLLATE BINARY` | нативный byte-order `String` | `CompareOrdinal` |
| ordinal-IgnoreCase | уточнить (`…_CI…`/case-fold) | `lower()`+`C` | `…_ci`+`utf8mb4_bin`? | `NOCASE` | `lower()` byte-order | `OrdinalIgnoreCase` |
| `string.Format`/`ToString(fmt)` | `format(v, 'N2')` | `to_char` | `format`/`date_format` | `printf`/`strftime` | `format`/`formatDateTime` | нативный `ToString(fmt)` |
| culture `ToUpper/Lower` | `upper/lower` (locale БД) | `upper/lower` (lc_ctype) | `upper/lower` (collation) | `upper/lower` (ASCII по умолчанию) | `upper/lower` (UTF-8) | `ToUpper(CultureInfo)` |

**Единообразие:** всё за новыми гейтами — `SupportsCollation`, `SupportsOrdinalComparison`,
`SupportsFormat`; провайдер, у которого нет формы, бросает `NotSupportedException`, а не подставляет
культурно-зависимый аналог.

## Ближайший CLR-аналог и тир

- `StringComparison`/`CultureInfo`/`IFormattable` — чистые CLR-понятия; прямого SQL-аналога нет.
  Тир: **«provider-faithful subset + explicit gate»**: документируем, что `Ordinal`/`Invariant`
  воспроизводимы, а `CurrentCulture`/`(CultureInfo)` — только если совпадает с коллацией БД, иначе отказ.
- `string.Format` → provider `format`/`to_char`; спецификаторы CLR (`N`, `D`, `X`, `F`, `yyyy`) ≠
  printf-спецификаторы — нужен маленький транслятор CLR→провайдер, а не передача строки как есть.

## Дизайн и публичный API

### 1. Явный fail-fast контракт (без нового API)

- `StringFunctionTranslator`/`ScalarFunctionTranslator` распознают неподдержанные overload'ы
  (`Compare*`, `Format`, `ToString(format)`, `*`-методы с `StringComparison`/`CultureInfo`) и кидают
  `NotSupportedException` **с указанием операнда** — раньше, чем общий фолбэк `:195`.
- Тесты-контракт (SQL-gen + InMemory) на каждый такой overload.
- **Цель фазы:** «никогда молча неверно» гарантировано уже сейчас, до реализации подмножества.

### 2. Format-подмножество

```csharp
// string.Format / интерполяция: только константная format-string
string.Format("Total: {0:N2}", e.Amount)   // e.Amount — колонка
$"{e.Created:yyyy-MM-dd}"                  // → string.Format(...)
```

- Разбирать `string.Format` с константным шаблоном и `,,` выравниванием; поддерживать узкий набор
  CLR-спецификаторов (`N/F/D/X` для чисел, `yyyy/MM/dd/HH/mm/ss` для дат) → provider `format`/`to_char`/
  `strftime`/`printf`.
- **Культура — только Invariant**; иначе `NotSupportedException`.
- `x.ToString(fmt)` (числа/даты) — та же функция.
- Неподдержанный спецификатор — исключение, **никогда** не отбрасывать.

### 3. Примитив коллации (общий с nextorm `#28`)

```csharp
public static class SqlFunctions { public static string Collate(string value, string collation); }
// fluent:  EntityBuilder<T>.Collate(...) / [Collate("...")] на свойстве
```

- Рендер: SQL Server/PG/MySQL/SQLite `value COLLATE <name>`; gate `SupportsCollation` (ClickHouse — off).
- Служит основой и для `#28` (колонковые коллации), и для ordinal-сравнений.

### 4. `StringComparison`-маппинг (поверх коллации)

- Таблица: `Ordinal` → binary-collation; `OrdinalIgnoreCase` → case-fold + binary; `InvariantCulture[IgnoreCase]`
  → `InvariantCulture`-коллация; `CurrentCulture[...]` → `NotSupportedException` (или коллация БД с
  документированной оговоркой).
- Покрыть `Compare`/`CompareOrdinal`/`Equals`/`StartsWith`/`EndsWith`/`IndexOf`/`Contains` с
  `StringComparison`; оператор `==` остаётся нативным SQL `=` (коллация БД) — задокументировать.

### 5. Culture/whitespace

- `ToUpperInvariant`/`ToLowerInvariant` → `upper`/`lower` (совпадает); `ToUpper(CultureInfo)` — отказ.
- `Trim()`: .NET убирает Unicode-whitespace, SQL `trim` — обычно только пробел; выровнять
  (документировать ограничение) или gate.

## Этапы внедрения

1. Fail-fast контракт + тесты (без изменения рендера).
2. Format-подмножество (`string.Format`/интерполяция/`ToString(fmt)`), Invariant-only; SQL-gen + интеграция.
3. Примитив коллации (`SqlFunctions.Collate` + fluent/атрибут, `SupportsCollation`) — синхронно с `#28`.
4. `StringComparison`-маппинг поверх (3).
5. Culture/whitespace-выравнивание + документация (EN+RU).

## План тестов

- SQL-gen по провайдерам: format (`tests/nextorm.postgres.tests`, `sqlserver`, `mysql`, `mariadb`,
  `sqlite`, `clickhouse`); ordinal/`COLLATE`; fail-fast `NotSupportedException`.
- Интеграция: `CommonTestSuite` ordinal-сравнение (PG/SQL Server/MySQL/SQLite) — подтвердить, что
  `CompareOrdinal` не совпадает там, где culture-вариант совпал бы; `InMemory` — нативные семантики.
- Format: интеграционные round-trip на числах/датах; не-Invariant культура → исключение.
- Покрытие: SQL Server/PostgreSQL в `coverage.settings.xml`; MySQL/SQLite/ClickHouse вне — зафиксировать.

## Открытые вопросы

1. `string.Compare(a, b)` **без** `StringComparison` — culture-sensitive по умолчанию: отказ или маппинг
   в коллацию БД с оговоркой? (Влияет на совместимость.)
2. Как представить `OrdinalIgnoreCase` без ISO-вариантов (`_BIN2` регистрозависим) — case-fold +
   binary, или не поддерживать на части провайдеров?
3. Как именно Roslyn опускает интерполяцию с выравниванием/форматом в expression tree — от этого
   зависит разбор `string.Format`.
4. Имена format-функций ClickHouse (`format` vs `formatDateTime`) и MySQL (`format` только для чисел) —
   уточнить фактическую доступность.
5. Ключ плана для `Collate`/format в кэше (`QueryPlanEqualityComparer`) — коллация должна участвовать в
   ключе.
6. Делать ли пункт 3 (коллация) **до** ordinal-маппинга, или `#28` — отдельный todo, а G12 только
   потребляет его (рекомендация: коллация — часть `#28`, G12 строит поверх).

## Файлы к изменению

- Новое: `src/nextorm.core/Builders/...` (fluent `.Collate(...)`), `src/nextorm.core/DataContext/Meta/CollateAttribute.cs`,
  `src/nextorm.core/Visitors/StringFormatTranslator.cs` (разбор `string.Format`/`ToString(fmt)`), тесты.
- Правки: `src/nextorm.core/Visitors/StringFunctionTranslator.cs:20-79` (`Compare*`, `StringComparison`,
  явные отказы), `ScalarFunctionTranslator.cs`, `BaseExpressionVisitor.cs:169-195` (порядок/сообщения),
  `DataContext/Dialect/ISqlDialect.cs` (`SupportsCollation`/`SupportsFormat`/`MakeCollate`/`MakeFormat`),
  `SqlDialectBase.cs:387-391`, `QueryPlanEqualityComparer.cs` (ключ), `SqlFunctions` (collate/format).
- Провайдеры: `nextorm.{sqlserver,postgres,mysql,mariadb,sqlite,clickhouse}/*Dialect.cs`
  (`MakeCollate`/`MakeFormat`/`Supports*`).
- Документация: `docs/guide/11-scalar-functions.md` (+RU), `docs/advanced/limitations.md` (+RU),
  `docs/advanced/api-reference.md` (+RU), `docs/specs/design/API-NAMING-REVIEW.md`.
- Смежное: nextorm issue `#28` (column collation) — общий примитив коллации.
