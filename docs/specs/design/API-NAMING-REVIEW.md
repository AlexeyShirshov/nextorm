# Аудит публичного API nextorm: именования

**Дата:** 2026-09-17; **актуализация 18.09.2026 по HEAD `d21c473`** (Build Release `0/0`);
**P0-переименования выполнены 18.09.2026** (Build Debug `0/0`, тесты: core 151, sqlite 180, sqlserver 166,
postgres 150, mysql 31, mariadb 7, clickhouse 47, integration 833/0 failed)
**Область:** `src/nextorm.core`, `src/nextorm.postgres`, `src/nextorm.sqlite`, `src/nextorm.sqlserver`, `src/nextorm.mysql`, `src/nextorm.mariadb`, `src/nextorm.clickhouse`, `src/nextorm.core.sourcegenerator`
**Методика:** скилл `api-design` (Framework Design Guidelines) + `dotnet-xml-docs` (XML-документация). Основание для вывода — XML-комментарий (`<summary>`/`<param>`, если есть) либо тело метода/свойства. Проект в стадии **alpha**: обратная совместимость не поддерживается, имена меняются напрямую.

> **Примечание (20.09.2026):** полностью реализованные отчёты `WIP_*.md`, на которые ссылаются записи ниже, перенесены в документацию и удалены из `docs/specs/roadmap/`; ссылки на них в этом журнале — историческое свидетельство аудита.

## 1. Как читать отчёт

Приоритеты:

| Уровень | Значение |
|---------|----------|
| **P0** | Имя вводит в заблуждение или конфликтует с BCL; ошибка проектирования |
| **P1** | Нарушение .NET-конвенций именования (тип/член) |
| **P2** | Несогласованность / косметика / загрязнение публичной поверхности |

**Alpha-политика:** обратная совместимость не сохраняется. Старое имя заменяется новым **на месте** — в коде, тестах, примерах и документации. Ничего не помечаем `[Obsolete]`, не заводим алиасов, дублирующих членов и type forwarders. Поверхность замораживается только к релизу 1.0.

## 2. Покрытие XML-документацией (актуализация 18.09.2026)

Замер отражением по собранным сборкам (`ExportedTypes` + `GetMembers`) и сгенерированным `.xml`-файлам, HEAD `d21c473`:

| Категория | Документировано | Всего | Доля |
|-----------|----------------:|------:|-----:|
| Публичные типы | 113 | 158 | 72 % |
| Публичные методы | 213 | 1092 | 20 % |
| Публичные конструкторы | 8 | 130 | 6 % |
| Публичные свойства | 97 | 347 | 28 % |
| Публичные поля | 18 | 84 | 21 % |
| Публичные события | 0 | 1 | 0 % |

Область — 7 библиотечных сборок (`nextorm.core` + 6 провайдеров). Единственный публичный тип `nextorm.core.sourcegenerator` (`AnonymousClassEqualityComparer`) задокументирован; сам проект `GenerateDocumentationFile` не включает. Прошлый baseline (33/142 типа, 56/742 метода, 34/208 свойств) устарел — с тех пор XML-доки массово добавлены. `GenerateDocumentationFile=true` включён в 7 проектах, но `NoWarn=CS1591` скрывает пропуски — NuGet-потребители не видят IntelliSense-доков. **Недокументированных публичных типов — 45** (полный список в приложении A).

### Документация `greatest`/`least` после включения SQLite (актуализация 19.09.2026)

Включение `SupportsGreatestLeast` для SQLite (`src/nextorm.sqlite/SqliteDialect.cs:31`) — поведенческое
изменение без переименований; контракт `CommonFunctions.greatest`/`least` не менялся (добавлены лишь
`public override`-члены `MakeGreatest`/`MakeLeast`, которые при заморозке поверхности в Шаге 5 попадут
в `PublicAPI.Unshipped.txt`).

Пробелы документации, которые закрываются в том же изменении (AGENTS.md: `docs/**` и `docs/ru/**`):

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `src/nextorm.core/Query/SqlFunctions.cs:182–194` (XML) | NULL-семантика описана только для PostgreSQL и MySQL/MariaDB | добавить SQLite: скалярные `max`/`min` возвращают `NULL`, если любой аргумент `NULL` |
| `docs/advanced/limitations.md:31`, `docs/ru/advanced/limitations.md:31` | «SQLite … rejects them with `NotSupportedException`» | SQLite поддерживает; описать NULL-семантику |
| `docs/guide/11-scalar-functions.md:675`, `docs/ru/guide/11-scalar-functions.md:691` | матрица: SQLite — `NotSupportedException` | `max(a, b)` / `min(a, b)`, одноаргументная форма — `(a)` |
| `docs/guide/11-scalar-functions.md:476`, `docs/ru/guide/11-scalar-functions.md:485` | список провайдеров без SQLite | добавить SQLite |
| `docs/specs/roadmap/todo_mssql.md:149` | `[ ]` | `[x]` |

NULL-семантика — **не единственный** пробел: прозаическая документация прямо противоречит новому поведению.

### Текст-JSON MySQL/MariaDB (точечный аудит 19.09.2026)

Изменение включает текстовую JSON-поверхность (`json_value`/`json_query`/`json_modify`/`isjson`) для
MySQL/MariaDB: новый публичный член `ISqlDialect.MakeTextJsonFunction(string, IReadOnlyList<string>)`
(базовая реализация в `SqlDialectBase` делегирует старому `MakeTextJsonFunction(string)`),
`MySqlDialect.SupportsTextJson => true` + переопределения `MakeTextJsonFunction`/`MakeIsJson`;
`MariaDbDialect` наследует MySQL. Публичных переименований нет, `docs/**` и `docs/ru/**` обновлены.
Build Release — 0/0.

**Extend-only?** Для потребителя изменение аддитивно (добавлена перегрузка, старая сохранена). Для
реализаторов `ISqlDialect` добавление абстрактного члена — source-breaking, но в репозитории
`ISqlDialect` реализует только `SqlDialectBase` (все диалекты наследуют его), поэтому фактического
разрыва нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| A | P2 | `ISqlDialect.cs:286`, `SqlDialectBase.cs:317` | 1-аргументный `MakeTextJsonFunction(string name)` сохранён, но **ни один** диалект его не переопределяет; единственный вызов — базовая реализация новой перегрузки (`SqlDialectBase.cs:319-320`). Дублирующий публичный член против alpha-политики «без дублирующих членов» | Удалить 1-аргументный член; базовая перегрузка пишет `$"{name}({string.Join(", ", args)})"` |
| B | P2 | `ISqlDialect.cs:124-129`, `Visitors/TextJsonSqlTranslator.cs:9,43`, `Query/SqlFunctions.cs:32-39`, `Query/SqlFunctions.SqlServer.cs:7-9` | XML-доки устарели: «SQL Server opts in», «only provider … is SQL Server», «SQL Server-only surface»; в `SupportsTextJson` не упомянут `isjson` | Добавить MySQL/MariaDB, убрать «only», включить `isjson` |
| C | P2 | `ISqlDialect.cs:295` | Новый член публичного интерфейса. Шаг 5 (заморозка) открыт: `PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён | При заморозке внести в `PublicAPI.Unshipped.txt`; альтернатива — default interface method |

Пробелы документации EN/RU, видимые по этому изменению:

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/guide/11-scalar-functions.md:457-484`, `docs/ru/guide/11-scalar-functions.md:466-493` | Заголовок «JSON as text (SQL Server, MySQL/MariaDB)», но пример (`:476`/`:485`) и таблица (`:481-484`/`:490-493`) дают только SQL Server-рендер (`json_value(...)`) | Показать MySQL-рендер (`json_unquote(json_extract(...))`, `json_extract(...)`, `json_set(...)`, `json_valid(...)`) либо сослаться на `docs/guide/18-json.md:89-115` |
| `docs/advanced/limitations.md:30`, `docs/ru/advanced/limitations.md:30` | Перечислены `json_value`/`json_query`/`json_modify` без `isjson` | Добавить `isjson` (в строке 53 он уже есть) |

`docs/guide/18-json.md` (EN+RU) описывает MySQL-рендер корректно (`:89-115`/`:90-116`); регресс
только в `11-scalar-functions.md`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`find -name 'PublicAPI*.txt'` — пусто; `rg MakeTextJsonFunction src tests` — 1-аргументный член
вызывается только из базы.

### `DateTime.DayOfYear` (точечный аудит 19.09.2026)

Публичной поверхности изменение не касается: добавлены только `public override MakeDatePart` в
`MySqlDialect`/`ClickHouseDialect` (SQLite правит существующий `switch`, SQL Server не тронут); имён
типов/членов не менялось. Шаг 5 (заморозка) остаётся открытым — переопределения попадут в
`PublicAPI.Unshipped.txt` только при подключении `PublicApiAnalyzers`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| N1 | P2 | `MemberTranslator.cs:68` (`"doy"`) → `SqlServerDialect.cs:152` | Внутренний ключ-сокращение `"doy"` утекает в SQL Server как `datepart(doy, …)` — невалидный T-SQL (функциональный дефект, см. `code-smells-review.md`, Находка 8) | Исправить рендер SQL Server (обязательно); при желании канонизировать `"dayofyear"` + PostgreSQL-override `extract(doy …)` |
| N2 | P2 | `MySqlDialect.cs:159`, `ClickHouseDialect.cs:98` | Новые `public override` без XML-`<summary>` (как и `SqlServerDialect.cs:152`); `NoWarn=CS1591` во всех 7 библиотечных `.csproj` | Добавить `<summary>` при закрытии Шага 5 |

Пробелы документации EN/RU (AGENTS.md: `docs/**` и `docs/ru/**`):

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/guide/11-scalar-functions.md:202-204`, `docs/ru/guide/11-scalar-functions.md:207-209` | Перечислены `.Year`/`.Month`/`.Day`/`.Hour`/`.Minute`/`.Second`, `.DayOfYear` отсутствует | Добавить `.DayOfYear` и `doy`-рендер (SQL Server `datepart(dayofyear, …)` после фикса) |
| `docs/providers/sqlite.md:19`, `docs/ru/providers/sqlite.md:19` | Список `strftime`-частей без `doy` | Добавить `doy` → `%j` (`cast(... as integer)`) |
| `docs/specs/roadmap/todo_mssql.md:64-66` | «`DateTime.DayOfYear`/`DayOfWeek` напрямую не маппятся» | `DayOfYear` закрыт (с исправленным T-SQL); оставить `DayOfWeek`/`DATEFIRST` |
| `docs/specs/roadmap/WIP_datepart_dayofyear.md:35` | «`doy` — допустимое сокращение T-SQL» | Неверно: T-SQL принимает `dayofyear`/`dy`/`y` (Microsoft Learn DATEPART); исправить и закрыть WIP |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0/0**; `find -name 'PublicAPI*.txt'` — пусто.

### Оконные `percent_rank`/`cume_dist` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет: добавлены `CommonFunctions.percent_rank()` /
`cume_dist()` → `WindowFunction<double>` (`SqlFunctions.cs:351,354`; XML-`<summary>` есть) и две
ветки `WindowSql.MapWindowFunctionName` (`WindowSql.cs:23-24`, через `nameof`, без строковых
литералов). Имена `snake_case` — сознательное SQL-зеркало DSL (реестр §3 «Отмечено, но менять не
рекомендуется»). Возврат `double` корректен: SQL-стандарт даёт `double precision` в `[0,1]`/`(0,1]`,
и все 5 диалектов (`double precision`/`float`/`double`/`real`/`Float64`) читаются в `System.Double`;
не-генерик тип согласован с остальными ранжирующими функциями (`row_number`/`rank`/`dense_rank`).
Дефолтная XML-документация у обоих членов присутствует. Build Release — 0/0.

Шаг 5 (заморозка) остаётся открытым: `PublicAPI.*.txt` нет. При подключении `PublicApiAnalyzers`
внести `NextORM.Core.SqlFunctions.CommonFunctions.percent_rank() -> NextORM.Core.WindowFunction<double>!`
и `cume_dist()`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| W1 | P2 | `docs/guide/10-window-functions.md:11-13`, `docs/ru/guide/10-window-functions.md:10-13` | Обзор перечисляет типы результата «`int` для ранжирующих, `T?` для значений/агрегатов» и не упоминает новое `double` у `percent_rank`/`cume_dist` | Добавить `double` для распределений |
| W2 | P2 | `docs/guide/10-window-functions.md:244-248`, `docs/ru/guide/10-window-functions.md:247-251` | `Source:`-футер устарел: `SqlFunctions.cs:23` — это свойство `Sql`, сам `WindowFunction<T>` — `WindowFunctions.cs:16`, `SqlFunctions.cs:221` — `date_diff`, оконные методы — `:314-372`; ссылки на интеграционные тесты `:72`/`:99` сдвинуты вставкой нового теста на `:54` | Пересчитать номера строк; убрать `WindowFunction<T>` из `SqlFunctions.cs` |
| W3 | P2 | `docs/guide/10-window-functions.md:10`, `docs/ru/guide/10-window-functions.md:10` | Пустой inline-код ``  `` — потеряна ссылка на `SqlFunctions.Sql` (пре-существующий, но файл затронут этой правкой) | Восстановить `` `SqlFunctions.Sql` `` |
| W4 | P2 | `docs/guide/10-window-functions.md:220-234`, `docs/ru/guide/10-window-functions.md:222-237` | «every SQL provider … supports all functions» — на ClickHouse `percent_rank`/`cume_dist` проверены только SQL-gen, рантайм-проверки нет (PG/SQL Server/MySQL/SQLite покрыты) | Добавить ClickHouse в интеграционный прогон либо оговорить проверенные провайдеры |
| W5 | P2 | `docs/specs/roadmap/todo_mssql.md` | Блокер `nth_value` — именно SQL Server (функции нет), но отложенность записана только в `todo_postgres.md:88-91` | Внести `[ ] nth_value` в MSSQL-бэклог |

`nth_value` **не** следует добавлять только с флагом `SupportsNthValue`: одного флага мало — у
SQL Server функции нет вовсе (нужна эмуляция), а на остальных результат зависит от рамки
(дефолтная `range unbounded preceding and current row` даёт `NULL` только до набора `n` строк).
Решение отложить — верное; при возврате к фиче нужен либо принудительный полный фрейм, либо
эмуляция через `first_value`.

> **Актуализация 19.09.2026 (верификация).** Прогноз реализован: `nth_value` добавлен
> (`CommonFunctions.nth_value`, `SqlFunctions.cs:453`) с флагом `ISqlDialect.SupportsNthValue`
> (`:160`), SQL Server гейтит `NotSupportedException` (`WindowFunctionTranslator.cs:50-51`).
> W1/W3/W4/W5 закрыты; прежняя аргументация о переносе решения устарела. Детали и список
> заморозки — в разделе «Повторная верификация cross-provider `iif`/`choose`/`nth_value`».

### ClickHouse `uniq`/`uniqExact`/`uniqCombined`/`uniqHLL12` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены: четыре метода
`ClickHouseFunctions.uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` → `long`
(`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:46,49,52,55`; XML-`<summary>` у всех),
`ISqlDialect.SupportsUniqAggregates` (`ISqlDialect.cs:236`) и
`ISqlDialect.MakeUniqAggregate(string, string)` (`ISqlDialect.cs:245`; реализация по умолчанию —
`SqlDialectBase.cs:51`). Имена snake_case — сознательное SQL-зеркало (реестр §3 «Отмечено, но менять
не рекомендуется», как `percent_rank`/`count_if`), P1 нет. Build Release — **0/0**.

**`long` (+ `toInt64`) — верный контракт.** Нативный `uniq*` возвращает `UInt64`, который
материализатор и драйвер ClickHouse не принимают; `long` согласован с `count_big`
(`SqlFunctions.cs:244`), а `toInt64(...)` — единственное приведение, сохраняющее 64-битный диапазон
(в `Int32`, как у `count`/`count_distinct`, значение не влезает). Теоретический overflow при
distinct-count > `Int64.MaxValue` для `uniq`/`uniqHLL12` не определён, но практически недостижим.

**Хук `MakeUniqAggregate` оправдан.** Без него `toInt64` пришлось бы либо хардкодить в
`AdvancedAggregateTranslator` (утечка провайдера в ядро), либо переопределять `MakeAggregate`, который
возвращает только имя функции и используется ещё и в `group by`. Дефолт `MakeAggregate(name)(argument)`
сохраняет паритет для диалекта без каста; дублирующего публичного члена, как в случае
`MakeTextJsonFunction(string)`, здесь нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| U1 | P2 | `docs/guide/04-grouping-and-aggregates.md:387-402`, `docs/ru/guide/04-grouping-and-aggregates.md:390-404` | Новое семейство distinct-count отсутствует в таблице семейств и в абзаце про ClickHouse (`arg_min`/`arg_max` и `-If` перечислены, `uniq` — нет) | Добавить строку `uniq`… + `SupportsUniqAggregates` и упоминание в обоих деревьях |
| U2 | P2 | `docs/ru/advanced/api-reference.md:58` | Строка `SqlFunctions` перечисляет только `Sql`/`Parameter`; строки `ClickHouse` (EN `:62`) в RU нет вовсе | Синхронизировать RU с EN (AGENTS.md: `docs/**` и `docs/ru/**`) |
| U3 | P2 | `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:5-9` | Классовый `<summary>` описывает только `argMin`/`argMax` и `-If`; новый uniq-набор не упомянут | Дополнить перечень |
| U4 | P2 | `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:236,245` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет). Абстрактный `MakeUniqAggregate` — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | Внести в `PublicAPI.Unshipped.txt` при заморозке (ср. C в аудите текст-JSON) |
| U5 | P2 | `src/nextorm.clickhouse/ClickHouseDialect.cs:45,48` | Новые `public override` без XML-`<summary>` — как и прочие override диалекта | Добавить при закрытии Шага 5 (ср. N2) |
| U6 | P2 | `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:232-235` | `Contain("uniqExact(somestring)")` проходит и без обёртки; слой `toInt64` закреплён только интеграционным тестом, `MakeUniqAggregate` напрямую не тестируется | Добавить `Contain("toInt64(uniqExact(")` и dialect-assert `MakeUniqAggregate("uniq_exact", "x") == "toInt64(uniqExact(x))"` |

Пробелы документации EN/RU, видимые по этому изменению:

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/guide/04-grouping-and-aggregates.md:391,401`, `docs/ru/guide/04-grouping-and-aggregates.md:394,404` | Перечислены `arg_min`/`arg_max` и `-If`, семейства distinct-count нет | Добавить `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` и `SupportsUniqAggregates` |
| `docs/ru/advanced/api-reference.md:58` | Строка `SqlFunctions` — только `Sql`/`Parameter` | Синхронизировать с EN (`Postgres`/`SqlServer`/`ClickHouse` + отдельная строка `ClickHouse`) |
| `docs/specs/roadmap/todo_clickhouse.md:194` | `[~] uniq/uniqExact` в шортлисте | `[x]` (пункт `:92` уже закрыт) |
| `docs/specs/roadmap/WIP_clickhouse_uniq.md:46` | «План» всё ещё обещает ветки `EmitSimple` и «новый диалект-хук не нужен» | Исторический журнал; итог в разделе «Результат» (`:79-83`) корректен, править не обязательно |
| `docs/providers/clickhouse.md:32`, `docs/ru/providers/clickhouse.md:32` | «`uniq`/… как `uniqExact`/`uniqCombined`/`uniqHLL12`» — при этом `uniq` рендерится как `uniq` | Уточнить, что `uniq` → `uniq`, остальные три — по именам |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `find -name 'PublicAPI*.txt'`
— пусто; ClickHouse unit-тесты и интеграционный `UniqAggregates_ShouldCountDistinctValues` — зелёные
(по данным изменения); PostgreSQL-тест `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1324-1326`
фиксирует `NotSupportedException`.

### ClickHouse параметрические `quantile`/`median` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.quantile`/`quantile_exact`/`quantile_timing` (`double?`, аргументы `double level, T? value`)
и `median` (`double?`, один аргумент) — `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:63,66,69,72`
(у всех `<summary>`); `ISqlDialect.SupportsQuantileAggregates` (`ISqlDialect.cs:252`, база `false` —
`SqlDialectBase.cs:53`) и `ISqlDialect.MakeQuantile(string, string, string)` (`ISqlDialect.cs:261`; база
`$"{MakeAggregate(name)}({level})({value})"` — `SqlDialectBase.cs:55-56`); `ClickHouseDialect` переопределяет
флаг (`:46`), `MakeAggregate` (`quantile_exact` → `quantileExact`, `quantile_timing` → `quantileTiming`, `:127-128`)
и `MakeQuantile` (`toFloat64(...)`, `:53-54`); транслятор — `AdvancedAggregateTranslator.EmitQuantile` (`:186-203`).
Имена snake_case — сознательное SQL-зеркало (реестр §3, как `uniq`/`arg_min`/`percent_rank`), P1 нет.
Build Release — **0/0**.

**`double?` + `toFloat64`: контракт верный, но применён не везде.** По документации ClickHouse
`quantile(level)(x)` для числовых типов даёт `Float64`, но `Decimal`/`Date`/`DateTime`/`DateTime64`
сохраняют входной формат; `quantileExact` сохраняет числовой входной тип; `quantileTiming` возвращает
`Float32`. Приведение к CLR `double` поэтому обязательно, и `MakeQuantile` с `toFloat64(...)` его обеспечивает.
**Однако `median(x)` (псевдоним `quantile(0.5)(x)`) рендерится через `EmitSimple` без обёртки**
(`AdvancedAggregateTranslator.cs:132-134`): для `Decimal`/`Date`/`DateTime`-колонок он вернёт входной тип и
нарушит объявленный `double?` (`SqlFunctions.ClickHouse.cs:72`). Все тесты используют `x.Id` (`Int32` → `Float64`),
поэтому дефект не покрыт (Q2).

**ClickHouse-only размещение — верно.** Двухскобочная форма `quantile(level)(value)` есть только в ClickHouse;
PostgreSQL выражает квантили иначе (`percentile_cont`/`percentile_disc`/`mode` через `WITHIN GROUP`,
`SupportsOrderedAggregates`/`MakeWithinGroup`), у SQL Server/MySQL/SQLite аналога нет. Перенос на
`CommonFunctions` подразумевал бы поддержку всеми диалектами — неверно; `ClickHouseFunctions` согласован
с `arg_min`/`uniq`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| Q1 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-11` | Два `<summary>` на классе `ClickHouseFunctions`: большой (строки 5–10) и следующий за ним однострочный (строка 11). В сгенерированном `nextorm.core.xml` (`T:NextORM.Core.ClickHouseFunctions`) оба попадают в `<member>` — DocFX/IntelliSense получат дубль. Большой summary при этом не упоминает новое семейство `quantile`/`median` | Удалить устаревший однострочник (строка 11), а в основную `<summary>` добавить `quantile`/`quantileExact`/`quantileTiming`/`median` |
| Q2 | P2 | `Visitors/AdvancedAggregateTranslator.cs:132-134` | `median` идёт через `EmitSimple` и не оборачивается в `toFloat64`; `median(x)` = `quantile(0.5)(x)` сохраняет `Decimal`/`Date`/`DateTime`, а объявленный возврат — `double?` (`SqlFunctions.ClickHouse.cs:72`). Функциональный дефект для не-числовых `T` | Рендерить `median` через приведение (`toFloat64(median(value))`): отдельный унарный хук либо маршрут через `MakeQuantile("quantile", "0.5", value)`; добавить тест на `DateTime`/`decimal`-колонку |
| Q3 | P2 | `ISqlDialect.cs:163,252,261` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет). Абстрактные члены — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`). Обновлено 19.09.2026: добавлен `SupportsPercentileWindow` | Внести `SupportsQuantileAggregates`/`MakeQuantile`/`MakeMedian` и `SupportsPercentileWindow` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/XP7) |
| Q4 | P2 | `ClickHouseDialect.cs:46,53-54` | Новые `public override` (`SupportsQuantileAggregates`, `MakeQuantile`) без XML-`<summary>` — как и прочие override диалекта | Добавить при закрытии Шага 5 (ср. N2/U5) |
| Q5 | P2 | `docs/specs/roadmap/todo_clickhouse.md:103,106,197` | Пункт `[ ]`, «Методы `CommonFunctions.quantile`» (реализовано на `ClickHouseFunctions`); шортлист `[ ]` | `[x]`; `ClickHouseFunctions.quantile`; строку 197 разделить — квантили `[x]`, `groupArray`/`topK` оставить `[ ]` |

Пробелы документации EN/RU, видимые по этому изменению. Заявленные правки трёх прозаических файлов
в рабочем дереве **отсутствуют**: `rg -i "quantile|median"` по `docs/providers/clickhouse.md`,
`docs/guide/04-grouping-and-aggregates.md`, `docs/advanced/api-reference.md` и их RU-зеркалам пуст
(единственные совпадения — `percentile_cont` в примере guide/04).

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/providers/clickhouse.md:33`, `docs/ru/providers/clickhouse.md:34` | Список агрегатов заканчивается `uniq*` | Добавить пункт: `quantile(level)(value)`/`quantileExact`/`quantileTiming` → `toFloat64(...)`, `median(x)` |
| `docs/guide/04-grouping-and-aggregates.md:391-392,397-404`, `docs/ru/guide/04-grouping-and-aggregates.md:394-395,400-407` | Проза и таблица семейств перечисляют ClickHouse-агрегаты без квантилей (таблица заканчивается `Ordered-set`) | Добавить в прозу и строку таблицы: `quantile`/`quantile_exact`/`quantile_timing`/`median`, `SupportsQuantileAggregates`, ClickHouse |
| `docs/advanced/api-reference.md:62` (EN) | Строка `ClickHouse` без квантилей; в `docs/ru/advanced/api-reference.md` строки `ClickHouse` нет вовсе (пре-существующий U2) | Дополнить EN и завести RU-строку с `quantile`/`median` |
| `docs/specs/roadmap/sql-capabilities-gap-analysis.md:106,148,155` | Перечислены ClickHouse-агрегаты (`argMin`/`argMax`, `-If`, `groupBit*`, `corr`) без квантилей | Добавить `quantile`/`median` в перечень |
| `docs/specs/roadmap/WIP_quantile.md:74` | «ids 1..10, медиана 5.5»; раздела «Результат» нет | Фактические ids 1..3, медиана 2 (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:46`); добавить итог |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
`find -name 'PublicAPI*.txt'` — пусто; ClickHouse unit **59/59**, PostgreSQL **154/154** (прогнано заново),
интеграционный `QuantileAggregates_ShouldReturnQuantile` — зелёный (по данным изменения); `rg -i "quantile|median"`
по трём прозаическим файлам EN+RU — пусто.

### ClickHouse `any`/`anyLast` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.any_agg`/`any_last` → `T?` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:80,83`;
XML-`<summary>` у обоих, `any_agg` ссылается на `CommonFunctions.any`), `ISqlDialect.SupportsAnyAggregates`
(`ISqlDialect.cs:274`, база `false` — `SqlDialectBase.cs:59`), `ClickHouseDialect.SupportsAnyAggregates => true`
(`ClickHouseDialect.cs:52`, с `<summary>`) и маппинг `MakeAggregate`: `any_agg → any`, `any_last → anyLast`
(`ClickHouseDialect.cs:139-140`); транслятор — `AdvancedAggregateTranslator` (`:123-128`, переиспользован
`EmitSimple`). Build Release — **0/0**; ClickHouse unit **62/62**, PostgreSQL **155/155** (прогнано заново).

**`any_agg` вместо перегрузки `any` — верное решение.** `CommonFunctions.any<T>(QueryCommand<T>)`
(`SqlFunctions.cs:175`) — квантор подзапроса, а трансляция диспетчеризуется **по имени метода**:
`TypeFacts.IsPredicateCall` (`TypeFacts.cs:40`) помечает любой метод с именем `any` предикатом,
`NormSqlTranslator` (`:77-90`) и `ArraySqlTranslator` (`:31`) маппят `"any"`,
`CorrelatedQueryExpressionVisitor` (`:120`) ловит `nameof(CommonFunctions.any)`. Перегрузка `any<T>(T?)`
дала бы коллизию по имени в этих местах (особенно в `IsPredicateCall`, где нет проверки объявляющего
типа), поэтому `any_agg` — не косметика, а необходимость; зафиксировано в `todo_clickhouse.md:112-114`
и `WIP_any_agg.md:15-17`. Имя `any_agg`/`any_last` — snake_case SQL-зеркало (реестр §3 «Отмечено, но
менять не рекомендуется»), P1 нет; асимметрия суффикса (`any_agg` vs `any_last`) косметична.

**`T?` — верный контракт.** ClickHouse `any`/`anyLast` возвращают тип входа без приведения, поэтому
`EmitSimple` без каста корректен (в отличие от `uniq*`/`quantile*`, где понадобились
`toInt64`/`toFloat64`). `T?` согласован с `arg_min`/`arg_max` (`SqlFunctions.ClickHouse.cs:20,23`) и
`sum_if`/`min_if`/`max_if` (`:33,39,42`). Для не-null входов ClickHouse отдаёт не-null значение
(0/пустая строка на пустом входе), т.е. `T?` — безопасное расширение; материализация покрыта
интеграционным `AnyAggregates_ShouldReturnRowValue`. Функционального дефекта, аналогичного `median`
(Q2), здесь нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| A1 | P2 | `docs/specs/roadmap/sql-capabilities-gap-analysis.md:106,148-155` | Новое row-picking семейство отсутствует в перечне ClickHouse-агрегатов (там `argMin`/`argMax`, `uniq*`, `quantile*`/`median`, `-If`), хотя `WIP_any_agg.md:53` заявляет этот файл в списке правок | Добавить `any`/`anyLast` в строку `:106` и в прозу `:149-155` |
| A2 | P2 | `SqlDialectBase.cs:59` | Новый `public override` без XML-`<summary>` (как и соседние override базы) | Добавить при закрытии Шага 5 (ср. N2/U5/Q4) |
| A3 | P2 | `ISqlDialect.cs:306,313` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет). Абстрактные — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`). Обновлено 19.09.2026: `any_agg` переехал `ClickHouseFunctions` → `CommonFunctions` (cross-provider) и гейтится новым `SupportsAnyValueAggregate`; `SupportsAnyAggregates` сужен до `any_last` | Внести `SupportsAnyAggregates`, `SupportsAnyValueAggregate` и `CommonFunctions.any_agg<T>` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/XP7) |

Пробелы документации EN/RU, видимые по этому изменению:

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/specs/roadmap/sql-capabilities-gap-analysis.md:106,149-155` | ClickHouse-агрегаты перечислены без `any`/`anyLast` | Добавить row-picking `any`/`anyLast` |
| `docs/advanced/api-reference.md:62` (EN) | «…`uniq`/`uniq_exact`/… the parameterised…» — грамматический разрыв (строка затронута правкой) | Косметика: «…`uniq*`, the parameterised…» |
| `docs/specs/roadmap/WIP_any_agg.md:44` | Запланированное имя теста `AnyAggregates_ShouldThrowBecausePostgresHasNoAny` | Фактическое — `…PostgresHasNoAnyAggregate` (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1341`) |

RU-зеркала синхронизированы: `docs/ru/providers/clickhouse.md:36,100`,
`docs/ru/guide/04-grouping-and-aggregates.md:408`, `docs/ru/advanced/api-reference.md:60`.
EN-документация также обновлена: `docs/guide/04-grouping-and-aggregates.md:405`,
`docs/providers/clickhouse.md:36,100`, `docs/advanced/api-reference.md:62`.
`todo_clickhouse.md:112-114` закрыт `[x]`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `find -name
'PublicAPI*.txt'` — пусто; `rg SupportsAnyAggregates src` — 5 вхождений (интерфейс/база/диалект/транслятор);
ClickHouse unit **62/62**, PostgreSQL **155/155**; `AnyAggregates_ShouldReturnRowValue` — зелёный на
реальном ClickHouse (по данным изменения).

### ClickHouse строковый JSON `JSONExtract*` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.json_extract_string`/`json_extract_raw`/`json_type` → `string?`,
`json_extract_int`/`json_length` → `long`, `json_extract_float` → `double`,
`json_extract_bool`/`json_has` → `bool` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:91-112`;
XML-`<summary>` у всех восьми); `ISqlDialect.SupportsJsonExtract` (`ISqlDialect.cs:282`, база `false` —
`SqlDialectBase.cs:62`) и `ISqlDialect.MakeJsonExtract(string, IReadOnlyList<string>)`
(`ISqlDialect.cs:289`; база `$"{name}({join})"` — `SqlDialectBase.cs:64-65`); `ClickHouseDialect`
переопределяет флаг (`:55`) и хук (`:61-78`) с маппингом snake→camel и `toInt64(JSONLength(...))`;
транслятор — `JsonExtractSqlTranslator` (`Visitors/JsonExtractSqlTranslator.cs`), подключён в
`NormSqlTranslator` после `TextJsonSqlTranslator` (`:345-346`). Имена snake_case — сознательное
SQL-зеркало (реестр §3, как `uniq`/`quantile`/`any_agg`), P1 нет. Build Release — **0/0**;
ClickHouse unit **64/64**, PostgreSQL **156/156** (прогнано заново).

**Возвратные типы верны.** ClickHouse: `JSONExtractInt`→`Int64` (`long`), `JSONExtractFloat`→`Float64`
(`double`), `JSONExtractString`/`JSONExtractRaw`/`JSONType`→`String` (`string?`),
`JSONLength`→`UInt64` (обёрнут в `toInt64`, как `uniq*`, — иначе row reader не материализует),
`JSONExtractBool`/`JSONHas`→`UInt8` (декларированы `bool`). Приведение `notEquals(..., 0)` из плана WIP
не понадобилось: интеграционный `JsonExtract_ShouldReadStringJson` проходит на реальном ClickHouse с
`B == true`; отсутствие значения даёт `0`/`''`, поэтому `long`/`double`/`bool` корректно non-nullable,
а `string?` — безопасное расширение.

**Хук `MakeJsonExtract` — верная форма.** Совпадает с `MakeTextJsonFunction(string, IReadOnlyList<string>)`
(то же вынесение провайдерного знания в диалект), принимает уже отрендеренные аргументы и **N-арный**
список, поэтому будущие variadic-пути ClickHouse (`JSONExtractString(json, 'a', 'b')`) не потребуют
менять сигнатуру хука — только снять guard `Arguments.Count == 2` в трансляторе. Каст `json_length`
выполнен в диалекте, как `toInt64`/`toFloat64` у `uniq*`/`quantile*`; дублирующего публичного члена,
как `MakeTextJsonFunction(string)`, здесь нет. Отдельный от `MakeTextJsonFunction` хук оправдан:
ClickHouse не поддерживает `SupportsTextJson`-набор (`json_value`/`json_query`/`json_modify`/`isjson`),
а общий флаг дал бы ложный гейт. 2-аргументное `(json, path)` покрывает вложенность
точечным путём (`'a.b'`), который ClickHouse принимает; ограничение приемлемо, но должно быть
зафиксировано в прозе (J-таблица).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| J1 | P2 | `ISqlDialect.cs:282,289` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет). Абстрактный `MakeJsonExtract` — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | Внести в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/A3) |
| J2 | P2 | `docs/specs/roadmap/todo_clickhouse.md:124-129` | Пункт всё ещё `[ ]`; текст приписывает family к `CommonFunctions.json_extract_*`, тогда как реализовано на `ClickHouseFunctions.json_extract_*`; не отражён частичный объём (arrays/`JSON`-тип/`visitParam` отложены) | Отметить `[~]`: закрыть 8 скалярных 2-арг, перечислить отложенные; `CommonFunctions` → `ClickHouseFunctions` |
| J3 | P2 | `docs/providers/clickhouse.md:28-36`, `:101`, `docs/ru/providers/clickhouse.md:28-36`, `:101` | Новое семейство не добавлено в список функций; строка таблицы `Arrays / JSON / extended scalars → not supported (PostgreSQL-only)` прямо противоречит строковому JSON (нужно развести native JSON и строковый `JSONExtract`) | Дополнить список и уточнить строку таблицы в обеих ветках |
| J4 | P2 | `docs/guide/11-scalar-functions.md:28`, `docs/ru/guide/11-scalar-functions.md:30` | Обзор ClickHouse-поверхности: «`arg_min`/`arg_max` и `-If`» — нет `JSONExtract*`/`JSONHas` | Добавить строковый JSON (оба зеркала) |
| J5 | P2 | `docs/advanced/api-reference.md:62`, `docs/ru/advanced/api-reference.md:60` | Строка `ClickHouse` без `JSONExtract*`; RU-строка `:60` — пре-существующий пробел (A1/U2), но затронута этой правкой | Дополнить обе ветки |
| J6 | P2 | `docs/specs/roadmap/sql-capabilities-gap-analysis.md:106,121-123,156-164` | Перечень ClickHouse-функций без строкового JSON; WIP (`WIP_jsonextract.md:78`) заявляет файл в списке правок | Добавить строку `JSONExtract*`/`JSONHas`, `SupportsJsonExtract` |
| J7 | P2 | `docs/specs/roadmap/WIP_jsonextract.md:73-78` | Нет раздела «Результат» с фактическими тестами/итогом (конвенция соседних WIP); путь `sql-capabilities-gap-analysis.md` указан без `docs/specs/roadmap/` | Добавить итог и фактический путь |

Пробелы документации EN/RU, видимые по этому изменению. Заявленные правки трёх прозаических файлов
в рабочем дереве **отсутствуют**: `rg -i "json_extract|JSONExtract|JSONHas|JSONLength|SupportsJsonExtract"`
по `docs/providers/clickhouse.md`, `docs/guide/11-scalar-functions.md`, `docs/advanced/api-reference.md`
и их RU-зеркалам **пуст** (совпадения есть только в WIP/todo и в разделах про MySQL/SQL Server text-JSON).

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/providers/clickhouse.md:28-36`, `:101`, `docs/ru/providers/clickhouse.md:28-36`, `:101` | Список функций заканчивается `any_agg`/`any_last`; таблица: «Arrays / JSON / extended scalars — not supported (PostgreSQL-only)» | `json_extract_string`/`_int`/`_float`/`_bool`/`_raw`, `json_has`, `json_length` (`toInt64(JSONLength(...))`), `json_type` → `JSONExtract*`/`JSONHas`; развести native JSON и строковый JSON |
| `docs/guide/11-scalar-functions.md:24-29`, `docs/ru/guide/11-scalar-functions.md:24-30` | ClickHouse = «`arg_min`/`arg_max` и `-If`» | Добавить строковый JSON; при желании — секция и строка матрицы (`:692`/`:708`) |
| `docs/advanced/api-reference.md:62`, `docs/ru/advanced/api-reference.md:60` | Строка `ClickHouse` без JSON | Дополнить `JSONExtract*`/`JSONHas` |
| `docs/specs/roadmap/sql-capabilities-gap-analysis.md:106,121-123,156-164` | Строковый JSON ClickHouse не упомянут | Добавить строку/прозу |
| `docs/specs/roadmap/todo_clickhouse.md:124-129` | `[ ]`; `CommonFunctions.json_extract_*` | `[~]`; `ClickHouseFunctions.json_extract_*`; частичный объём |
| `docs/specs/roadmap/WIP_jsonextract.md:73-78` | Нет итога; путь без `docs/specs/roadmap/` | Добавить «Результат» и исправить путь |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано);
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **64/64**,
`tests/nextorm.postgres.tests` — **156/156** (прогнано заново); `find -name 'PublicAPI*.txt'` — пусто;
XML-`<summary>` у всех восьми новых методов и обоих новых членов `ISqlDialect` присутствуют.

### ClickHouse `visitParamExtract*` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.visit_param_extract_string`/`visit_param_extract_raw` → `string?`,
`visit_param_extract_int` → `long`, `visit_param_extract_float` → `double`,
`visit_param_extract_bool` → `bool` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:119-131`;
XML-`<summary>` у всех пяти). Диалект — `ClickHouseDialect.MakeJsonExtract`
(`src/nextorm.clickhouse/ClickHouseDialect.cs:73-77`, snake→camel); транслятор — пять веток
`JsonExtractSqlTranslator` (`src/nextorm.core/Visitors/JsonExtractSqlTranslator.cs:46-60`). Имена
snake_case — сознательное SQL-зеркало (реестр §3, как `json_extract_*`/`uniq`/`quantile`/`any_agg`),
P1 нет; второй параметр назван `name`, а не `path`, и это верно: `visitParamExtract*` принимает
плоское имя поля, а не точечный путь. Build Release — **0/0**; ClickHouse unit **65/65**, PostgreSQL
**157/157**; интеграционный `VisitParamExtract_ShouldReadFlatJson` — зелёный на реальном ClickHouse.

**Переиспользование `SupportsJsonExtract`/`MakeJsonExtract`/`JsonExtractSqlTranslator` — верно,
отдельный флаг/хук не нужен.** Оба семейства — ClickHouse-only извлечение JSON **из текстовой
колонки**; `SupportsJsonExtract => true` выставляет только ClickHouse (база `false` —
`SqlDialectBase.cs:62`), поэтому новый флаг был бы истинен ровно там же, где существующий, т.е.
дублировал бы публичный член `ISqlDialect` (лишний source-breaking контракт к Шагу 5) без
функционального эффекта. Хук `MakeJsonExtract(string name, IReadOnlyList<string> args)` уже
ключёван по имени и N-арен, поэтому пять новых пар добавлены одним `switch` без смены сигнатуры;
отдельный хук, как и отдельный транслятор, не дал бы ничего, кроме копии. Это же решение
зафиксировано в `WIP_visitparam.md:15-17` («тот же строково-JSON family … новых хуков нет»).

**Возвратные типы верны.** ClickHouse: `visitParamExtractInt`→`Int64` (`long`),
`visitParamExtractFloat`→`Float64` (`double`), `visitParamExtractString`/`visitParamExtractRaw`→
`String` (`string?`), `visitParamExtractBool`→`UInt8` (декларирован `bool`, материализуется, как
`JSONExtractBool`). Каст не нужен: в отличие от `JSONLength`/`uniq*` (`UInt64`) и
`quantile*`/`median`, `visitParamExtract*` не отдаёт `UInt64`, поэтому `toInt64`/`toFloat64` не
добавлены — это соответствует поведению реального ClickHouse (интеграционный тест).

**Отличие от J-раздела: новых членов `ISqlDialect` нет.** `SupportsJsonExtract` и `MakeJsonExtract`
уже введены правкой `JSONExtract*` (J1). При заморозке (Шаг 5) в `PublicAPI.Unshipped.txt` войдут
только пять методов `ClickHouseFunctions.visit_param_extract_*`; `PublicAPI.*.txt` по-прежнему нет.
Этим изменением закрыты `J3` (provider/clickhouse.md список `:37-42` + таблица `:107-108`),
`J5` (api-reference EN `:62` / RU `:60`) и `J6` (gap-analysis таблица `:123`); `J2` — частично
(`todo_clickhouse.md:133-137` — `[x]`, пункт `:124-131` остаётся `[~]` по отложенным
`JSONExtractArrayRaw`/`JSON_VALUE`); `J4` — проза `guide/11` дополнена `JSONExtract*`, но см. V2.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| V1 | P2 | `ISqlDialect.cs:277-282,284-288`, `ClickHouseDialect.cs:54`, `JsonExtractSqlTranslator.cs:5-11`, `SqlFunctions.ClickHouse.cs:5-13` | XML-доки гейта/хука/транслятора и классовые `<summary>` описывают только `JSONExtract*`/`JSONHas`; новое семейство `visitParamExtract*` не упомянуто, хотя проходит через тот же флаг/хук (ср. U3/Q1/N2) | Дополнить все пять мест плоским JSON `visitParamExtract*` |
| V2 | P2 | `docs/guide/11-scalar-functions.md:28-29`, `docs/ru/guide/11-scalar-functions.md:31` | Обзор ClickHouse-поверхности: «…and the string-JSON `JSONExtract*` family» — быстрый разбор плоского JSON не упомянут (файл затронут J4-дополнением) | Добавить `visitParamExtract*` (либо сослаться на `docs/guide/18-json.md:22-25`) в оба зеркала |
| V3 | P2 | `docs/guide/18-json.md:42,297-298`, `docs/ru/guide/18-json.md:42,299-300` | Матрица: ClickHouse «JSON-as-text functions = No», а ниже «SQLite and ClickHouse … nextorm does not expose them yet» — прямо противоречит буллету `:22-25`/`:22-26` и `docs/providers/clickhouse.md:107` (пре-существующая нестыковка, видимая в правленом файле) | Поставить ClickHouse `Supported ([SupportsJsonExtract])` в матрице и переписать буллет `:297`/`:299` (исключить ClickHouse; остаётся SQLite/JSON1 native-JSON) |
| V4 | P2 | `docs/specs/roadmap/sql-capabilities-gap-analysis.md:154-166` | Проза ClickHouse-функций (`:154-158`) и абзац JSON (`:164-166`) перечисляют `JSONExtract*`/`JSONHas`/`JSONLength`/`JSONType` без `visitParamExtract*`, хотя таблица `:123` его содержит | Добавить `visitParamExtract*` в прозу (или явно отнести к строковому JSON-семейству) |

Пробелы документации EN/RU, видимые по этому изменению: прозаические файлы, заявленные в изменении
(`providers/clickhouse.md` EN+RU, `guide/18-json.md` EN+RU, `advanced/api-reference.md` EN+RU),
обновлены и содержат `visit_param_extract_*`; `todo_clickhouse.md:133-137` — `[x]`,
`WIP_visitparam.md:42-50` — раздел «Результат» есть, `sql-capabilities-gap-analysis.md:123`
дополнен. Остаются перечисленные V2–V4.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано);
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **65/65**,
`tests/nextorm.postgres.tests` — **157/157** (прогнано заново); `find -name 'PublicAPI*.txt'` — пусто;
`rg "visit_param|visitParam" src tests` — методы/маппинг/ветки/тесты на месте; XML-`<summary>` у всех
пяти новых методов присутствуют.

### ClickHouse JSONPath-скаляры `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (точечный аудит 20.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.json_value`/`json_query` → `string?`, `json_exists` → `bool`
(`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:113-124`; XML-`<summary>` у всех трёх);
`ISqlDialect.SupportsJsonPath` (`ISqlDialect.cs:354-360`; база `false` — `SqlDialectBase.cs:100-101`);
`ClickHouseDialect.SupportsJsonPath => true` (`src/nextorm.clickhouse/ClickHouseDialect.cs:72-73`);
три пары имён добавлены в существующий `ClickHouseDialect.MakeJsonExtract` (`:96-98`). Транслятор —
`JsonExtractSqlTranslator` (ветки `:48-56`, рендер `EmitJsonPathFunction` `:101-128`), подключён в
`NormSqlTranslator` после `TextJsonSqlTranslator` (`:376`). Коллизии имён с другими поверхностями
(`SqlServerFunctions.json_value`/`json_query`, `PostgresFunctions.json_exists`) устранены на уровне
трансляторов guard'ом по `DeclaringType` (`TextJsonSqlTranslator.cs:20-21`, `JsonSqlTranslator.cs:30-33`);
корректность возвратных типов подтверждена интеграционным `JsonPath_ShouldReadStringJson` на реальном
ClickHouse (`V="hi"`, `Q="[{\"x\":1}]"`, `E=true`). Имена snake_case — сознательное SQL-зеркало
(реестр §3), P1 нет. Build Release — **0/0**; unit: clickhouse **101/101**, postgres **174/174**,
sqlserver **177/177**, mysql **44/44**, sqlite **207/207**, core **157/157** (0 failed).

**Отдельный `SupportsJsonPath` избыточен (ответ на вопрос 2).** Флаг выставлен `true` ровно там же,
где `SupportsJsonExtract`: `rg SupportsJsonExtract src` даёт единственный оверрайд — `ClickHouseDialect.cs:70`,
`rg SupportsJsonPath src` — единственный оверрайд `ClickHouseDialect.cs:73`. Два флага не различают ни
одного провайдера, т.е. дублируют один capability. Это прямо противоречит уже принятому в реестре
решению по `visitParamExtract*` (раздел V: «отдельный флаг … дублировал бы публичный член `ISqlDialect`
… без функционального эффекта») и alpha-политике «без дублирующих членов» (§1). Более того,
JSONPath-семантика SQL Server/MySQL (те же `json_value(json, '$.path')`) уже гейтится
`SupportsTextJson`, т.е. по сути это третий флаг для того же концепта. Причина, по которой гейт вообще
нужен, — точное сообщение об ошибке; его можно сохранить без флага.

**Хук `MakeJsonExtract` переиспользован — по форме верно, по имени/докам устарел (см. JP2).**
Хук ключёван по имени и N-арен (обоснование разделов J/V), поэтому три пары маппинга добавлены одним
`switch` без смены сигнатуры; `MakeJsonExtract`/`SupportsJsonExtract` — то же строково-JSON семейство.
Но публичное имя `MakeJsonExtract` и его XML-док (`ISqlDialect.cs:347-353`) описывают только
`JSONExtract*`/`visitParamExtract*`, тогда как теперь хук рендерит и `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| JP1 | P2 | `ISqlDialect.cs:354-360`, `SqlDialectBase.cs:100-101`, `ClickHouseDialect.cs:73`, `JsonExtractSqlTranslator.cs:108` | `SupportsJsonPath` истинен ровно там же, где `SupportsJsonExtract` (оба — только ClickHouse), т.е. дублирующий член публичного интерфейса (source-breaking к Шагу 5) без функционального эффекта; нарушает прецедент V-раздела и alpha-политику «без дублирующих членов» | Удалить `SupportsJsonPath` (интерфейс/база/диалект) и гейтить `EmitJsonPathFunction` по `SupportsJsonExtract`, сохранив JSONPath-сообщение; синхронно поправить `ClickHouseDialectTests.cs:93`, `docs/guide/18-json.md` (+RU), `docs/providers/clickhouse.md` (+RU). Если флаг решено оставить — зафиксировать реальный провайдерный разрыв, иначе это дубль |
| JP2 | P2 | `ISqlDialect.cs:347-353`, `SqlDialectBase.cs:97`, `ClickHouseDialect.cs:79-104` | XML-док хука `MakeJsonExtract` и базовая `<summary>` заявляют только `JSONExtract*`/`visitParamExtract*`, но хук теперь маппит и `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS`; публичное имя перестало описывать полную ответственность | Обновить доки до «строково-JSON/JSONPath name map»; при заморозке поверхности рассмотреть нейтральное `MakeJsonFunction`/`MakeJsonTextFunction` (alpha допускает) |
| JP3 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-14`, `Query/SqlFunctions.cs:41-51`, `Visitors/JsonExtractSqlTranslator.cs:5-13` | Классовые `<summary>` `ClickHouseFunctions`/`JsonExtractSqlTranslator` и `<summary>` свойства `SqlFunctions.ClickHouse` перечисляют `JSONExtract*`/`visitParamExtract*`, но не JSONPath (кумулятивно, продолжение V1/D3) | Дополнить все три JSONPath-скалярами либо заменить перечень обобщённой формулировкой со ссылкой на `ClickHouseFunctions` |
| JP4 | P2 | `docs/guide/11-scalar-functions.md:29`, `docs/ru/guide/11-scalar-functions.md:31`; `docs/guide/11-scalar-functions.md:746`, `docs/ru/guide/11-scalar-functions.md:762` | Обзор ClickHouse-поверхности не упоминает JSONPath-скаляры; строка матрицы `JSONPath (jsonb_path_*)` по-прежнему помечает ClickHouse как `NotSupportedException` (продолжение V2) | Добавить JSONPath в обзорный буллет; уточнить, что строка матрицы — про `jsonb_path_*` (PG-only), а `json_value`/`json_query`/`json_exists` есть у ClickHouse |
| JP5 | P2 | `PublicAPI.*.txt` отсутствуют | Новые члены (`SupportsJsonPath` — если оставлен, 3 метода, оверрайд) не трекаются; Шаг 5 открыт | При включении `PublicApiAnalyzers` внести в `PublicAPI.Unshipped.txt` (ср. J1/D1/GLI2) |

Пробелы документации EN/RU: `docs/guide/18-json.md` (EN `:25-27,44`, RU `:25-27,44`),
`docs/providers/clickhouse.md` (EN `:46-47,138`, RU `:45-46,137`) и `docs/advanced/api-reference.md`
(EN/RU `:62`) обновлены и синхронны по именам; `WIP_clickhouse_json_path.md:85-96` содержит раздел
«Результат», `todo_clickhouse.md:197-209` — `[~]` с тестами. Остаются JP3/JP4.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project
tests/<p> -c Release --no-build` — clickhouse **101/101**, postgres **174/174**, sqlserver **177/177**,
mysql **44/44**, mariadb **14/14**, sqlite **207/207**, core **157/157**; `find -name 'PublicAPI*.txt'`
— пусто; XML-`<summary>` у трёх новых методов и `SupportsJsonPath` (+ оверрайд) присутствуют.

### ClickHouse функции словарей `dictGet`/`dictGetOrDefault`/`dictHas` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.dict_get<TValue,TKey>`/`dict_get_or_default<TValue,TKey>` → `TValue?`,
`dict_has<TKey>` → `bool` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:139,142,145`;
XML-`<summary>` у всех трёх); `ISqlDialect.SupportsDictionaries` (`ISqlDialect.cs:297`, база `false` —
`SqlDialectBase.cs:67`) и `ISqlDialect.MakeDictionaryFunction(string, IReadOnlyList<string>)`
(`ISqlDialect.cs:303`; база `$"{name}({join})"` — `SqlDialectBase.cs:69-70`); `ClickHouseDialect`
переопределяет флаг (`:86`, с `<summary>`) и хук (`:88-100`) с маппингом `dict_get`→`dictGet`,
`dict_get_or_default`→`dictGetOrDefault`, `dict_has`→`dictHas`; транслятор — `DictionarySqlTranslator`
(`:17-56`), подключён в `NormSqlTranslator` после `JsonExtractSqlTranslator` (`:348-349`).
Имена snake_case — сознательное SQL-зеркало (реестр §3 «Отмечено, но менять не рекомендуется», как
`uniq`/`quantile`/`any_agg`/`json_extract_*`), P1 нет. Build Release — **0/0**; ClickHouse unit **67/67**,
PostgreSQL **158/158** (прогнано заново).

**`TValue`/`TKey` — верный контракт.** Тип атрибута словаря задаётся в `CREATE DICTIONARY` и статически
неизвестен, поэтому значение параметризовано (`TValue`), а ключ — вторым параметром (`TKey`), по образцу
`arg_min<TValue, TBy>` (`SqlFunctions.ClickHouse.cs:21`). `TValue?` — безопасное расширение: отсутствие
id даёт дефолт типа атрибута (`0`/`''`, NULL у `Nullable`), а не ошибку; согласовано с `arg_min`/`any_agg`
(разделы U/A). `dict_get_or_default` тоже возвращает `TValue?`, т.к. `defaultValue` объявлен `TValue?`
(пользователь может передать `null`). `dict_has` → non-nullable `bool` корректен: нативный `UInt8`
материализуется как `bool` — тот же путь, что у `JSONExtractBool`/`JSONHas` (раздел J).
Плата за универсальность: `TValue` не выводится из аргументов, поэтому вызов всегда указывает оба
generic-параметра (`dict_get<string, long>(…)`); типизированные `dictGetString`/`dictGetInt*` отложены
(вне объёма) и сняли бы это неудобство.

**Отдельные `SupportsDictionaries`/`MakeDictionaryFunction`/`DictionarySqlTranslator` — оправданы.**
Словари — не JSON: переиспользование `SupportsJsonExtract` сделало бы гейт ложным (флаг заявлял бы
строковый JSON у диалекта со словарями и наоборот), а `MakeJsonExtract` маппит только `JSONExtract*`/
`visitParamExtract*`-имена. Отдельный транслятор согласован с уже существующим разбиением
`TextJsonSqlTranslator`/`JsonExtractSqlTranslator` и даёт `NotSupportedException` с точным текстом
(`DictionarySqlTranslator.cs:37-38`). Хук не создаёт дублирующего публичного члена: база `false` +
рендер по умолчанию, как у соседних семейств.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| D1 | P2 | `ISqlDialect.cs:297,303` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет). Абстрактные члены — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | Внести `SupportsDictionaries` + `MakeDictionaryFunction` и три метода `ClickHouseFunctions` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3) |
| D2 | P2 | `SqlDialectBase.cs:69-70` | Базовая реализация `MakeDictionaryFunction` без XML-`<summary>` (как `MakeJsonExtract` `:64-65`); `SupportsDictionaries` `:66` уже с `<summary>` | Добавить `<summary>` при закрытии Шага 5 (ср. N2/U5/Q4/A2) |
| D3 | P2 | `Query/SqlFunctions.cs:40-45` | XML-`<summary>` свойства `SqlFunctions.ClickHouse` перечисляет только `argMin`/`argMax` и `-If`, не упоминая `uniq`/`quantile`/`any`/строковый JSON/словари (кумулятивно устарело) | Дополнить перечень либо заменить на обобщённую формулировку со ссылкой на `ClickHouseFunctions` |

Пробелы документации EN/RU, видимые по этому изменению. Заявленные правки прозаических файлов
(`docs/providers/clickhouse.md` EN+RU `:42-43,109`, `docs/advanced/api-reference.md:62` / RU `:60`,
`todo_clickhouse.md:138-144`, `sql-capabilities-gap-analysis.md:124,166`) в рабочем дереве
**присутствуют**, RU-зеркала синхронизированы.

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/guide/11-scalar-functions.md:28-29`, `docs/ru/guide/11-scalar-functions.md:30-31` | Обзор ClickHouse-поверхности: «`arg_min`/`arg_max`, the `-If` combinator, the string-JSON … fast path» — функции словарей отсутствуют | Добавить `dict_get`/`dict_get_or_default`/`dict_has` в оба зеркала |
| `docs/specs/roadmap/todo_clickhouse.md:221` | `5. [~] JSONExtract*/visitParamExtract* — …; dictGet* (уровень 2) остаётся` | Убрать `dictGet*`: пункт закрыт (`:138-144` — `[x]`) |
| `docs/specs/roadmap/WIP_dictget.md:55` | План теста `DictGet_ShouldUseClickHouseNames` | Фактическое имя — `DictFunctions_ShouldUseClickHouseNames` (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:327`) |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано);
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **67/67**,
`tests/nextorm.postgres.tests` — **158/158** (прогнано заново); `find -name 'PublicAPI*.txt'` — пусто;
XML-`<summary>` у трёх новых методов и двух новых членов `ISqlDialect` присутствуют (кроме базовой
`SqlDialectBase.MakeDictionaryFunction`, D2).

### PostgreSQL uuid-генераторы и session/info-функции (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены: восемь `public`-методов
`PostgresFunctions.gen_random_uuid()`/`uuidv7()` → `Guid?`, `pg_typeof(object?)`/`current_user()`/
`session_user()`/`current_schema()`/`current_database()`/`version()` → `string?`
(`src/nextorm.core/Query/SqlFunctions.Postgres.cs:403,406,409,412,415,418,421,424`; XML-`<summary>` у всех
восьми). Трансляция — `ExtendedScalarFunctionTranslator` (`KeywordFunctions`: `current_user`/`session_user`/
`current_schema`; `DirectFunctions`: `gen_random_uuid`/`uuidv7`/`current_database`/`version`;
`pg_typeof` — отдельная ветка `EmitPgTypeOf`, `:112-125`). Новый `public override`
`PostgresDialect.MakeTypeName(Type)` (`src/nextorm.postgres/PostgresDialect.cs:23`, с `<summary>`) маппит
`string` → `text`. Имена snake_case — сознательное SQL-зеркало (реестр §3 «Отмечено, но менять не
рекомендуется», как `uniq`/`quantile`/`dict_get`), P1 нет; BCL-конфликтов (`version`/`current_user` и т.п.)
нет. Build Release — **0/0**.

**`MakeTypeName(typeof(string))` → `text` — верное место.** Рендер имени типа — ответственность диалекта;
хардкод `"text"` в ядре (`EmitPgTypeOf`) протёк бы провайдерным знанием. База
(`SqlDialectBase.cs:118-128`, `_ => type.Name`) отдаёт для `string` CLR-имя `String` — невалидный
cast-таргет; в PostgreSQL правильный текстовый тип — `text`. Бласт-радиус переопределения узкий:
`MakeTypeName(typeof(string))` достижим только из `EmitPgTypeOf` — два других вызова
(`PredicateTranslator.cs:19-25`, `BaseExpressionVisitor.cs:87-95`) отфильтрованы
`TypeFacts.TryGetNumericConversion` (`TypeFacts.cs:49-58`, только numeric→numeric). Скрытый риск на
будущее: `MySQL`/`ClickHouse` тоже отдают `String` для `string` (их `MakeTypeName` не маппит `string`) —
сегодня не вызывается, но при появлении строкового каста потребуется такой же override.

**Переиспользование `SupportsExtendedScalarFunctions` — верно.** Флаг истинен только у
`PostgresDialect` (`:75`), база — `false` (`SqlDialectBase.cs:42`); все восемь функций PostgreSQL-only
(`gen_random_uuid` — PG13+, `uuidv7` — PG18+, session/info — PG). Отдельный флаг был бы дублирующим
публичным членом `ISqlDialect` (лишний source-breaking контракт к Шагу 5) без функционального эффекта;
SQLite-тест `SessionInfoFunctions_ShouldThrowBecauseSqliteLacksThem` подтверждает reject-путь.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| PG1 | P2 | `SqlFunctions.Postgres.cs:403,406,409,412,415,418,421,424` + `PostgresDialect.cs:23` | Восемь новых `public`-методов и `public override MakeTypeName` — новые члены публичной поверхности; Шаг 5 открыт (`PublicAPI.*.txt` нет) | Внести все восемь + `PostgresDialect.MakeTypeName(System.Type)` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3/D1) |
| PG2 | P2 | `docs/guide/11-scalar-functions.md:251`, `docs/ru/guide/11-scalar-functions.md:256` | Таблица «PostgreSQL extended date and time» перечисляет `current_date`/`localtime`, но не новое семейство; в `docs/guide/11` (EN+RU) `gen_random_uuid`/`uuidv7`/`pg_typeof`/`current_*`/`version` отсутствуют (`rg` пуст) | Добавить строки/блок: keyword-формы `current_user`/`session_user`/`current_schema`, функции `current_database()`/`version()`/`gen_random_uuid()`/`uuidv7()` (PG18+)/`pg_typeof()` в обе ветки |
| PG3 | P2 | `docs/providers/postgres.md:41-42`, `docs/ru/providers/postgres.md:41-42`, `docs/advanced/api-reference.md:60`, `docs/ru/advanced/api-reference.md:60` | `uuidv7` указан без оговорки «PostgreSQL 18+»; на PG13–17 вызов падает в рантайме. Оговорка есть только в XML (`SqlFunctions.Postgres.cs:405`) и `todo_postgres.md:114` | Добавить «(PostgreSQL 18+)» в прозу EN+RU |
| PG4 | P2 | `docs/specs/roadmap/WIP_pg_info_functions.md:53` | «`pg_typeof` = `bigint` для `id`» — фактически `integer`: `tests/nextorm.integration.tests/PostgresSpecificTests.cs:32` (`SimpleEntity.Id` — `int`) | Исправить на `integer` (или «тип `id`-колонки») |
| PG5 | P2 | `SqlFunctions.Postgres.cs:6-12` | Два `<summary>` подряд на публичном `PostgresFunctions` → дубль в `nextorm.core.xml`/DocFX (аналог Q1); пре-существующее | Удалить лишний `<summary>` (`:12`) |
| PG6 | P2 | `tests/nextorm.postgres.tests/PostgresDialectTests.cs` | `PostgresDialect.MakeTypeName(typeof(string)) == "text"` не закреплён напрямую — только косвенно через `cast(pg_typeof(id) as text)` (`SqlGenerationTests.cs:1415`) | Добавить `Dialect.MakeTypeName(typeof(string)).Should().Be("text")` (+ fallback, напр. `typeof(long)` → `bigint`) |
| PG7 | P2 | `docs/specs/roadmap/sql-capabilities-gap-analysis.md:40,250` | Перечень PostgreSQL extended scalar не упоминает uuid-генераторы/`pg_typeof`/`version` (есть только `current_*`); WIP `:71` заявляет файл в списке правок | Дополнить перечень (uuid/`pg_typeof`/`version`) |

Пробелы документации EN/RU. `docs/providers/postgres.md` (EN `:41-42` / RU `:41-42`) и
`docs/advanced/api-reference.md` (EN `:60` / RU `:60`) обновлены и **синхронизированы**; `todo_postgres.md:108-114`
закрыт `[x]`. Не закрыты PG2/PG3 (гид и оговорка PG18+). Единственный `internal`-пробел: классовая
`<summary>` `ExtendedScalarFunctionTranslator.cs:5-16` не перечисляет новое семейство (на CS1591 не
влияет, тип `internal`).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
`find -name 'PublicAPI*.txt'` — пусто; XML-`<summary>` у всех восьми методов и у `MakeTypeName`-override
присутствуют; интеграционный `InfoFunctions_ShouldReturnServerValues` — зелёный (по данным изменения).

> **Актуализация 19.09.2026 — промоушен session/info-функций.** Пять методов
> `current_user`/`session_user`/`current_schema`/`current_database`/`version` перенесены с
> `PostgresFunctions` на `CommonFunctions` и получили отдельный семейный контракт; строка PG1 и адреса
> `SqlFunctions.Postgres.cs:412,415,418,421,424` относятся к историческому снимку. На `PostgresFunctions`
> остались только PG-only `gen_random_uuid`/`uuidv7`/`pg_typeof` (`SqlFunctions.Postgres.cs:402,405,408`),
> по-прежнему под `SupportsExtendedScalarFunctions`. PG4/PG7 (`WIP_pg_info_functions.md` /
> `sql-capabilities-gap-analysis.md`) частично устарели: семейство теперь кросс-провайдерное, а
> `WIP_pg_info_functions.md` продолжен `WIP_session_info_functions.md`.

> **Актуализация 19.09.2026 — UUID-генераторы промоутнуты.** Шаг 1 показал, что
> `gen_random_uuid`/`uuidv7` — тоже кросс-провайдерное семейство (SQL Server `newid()`, MariaDB
> `UUID_v4()`/`UUID_v7()`, ClickHouse `generateUUIDv4()`/`generateUUIDv7()`). Методы перенесены на
> `CommonFunctions` (`SqlFunctions.cs:225` — `gen_random_uuid`, `:233` — `uuidv7`; XML-`<summary>` у
> обоих) и доступны как `SqlFunctions.Sql.*`; на `PostgresFunctions` остался только PG-only
> `pg_typeof` (`SqlFunctions.Postgres.cs:402`), поэтому адреса `:402,405` и строка PG1/PG5 относятся
> к историческому снимку. Контракт `ISqlDialect` — `SupportsUuidGenerators` (`ISqlDialect.cs:231`),
> `SupportsUuidGenerator(name)` (`:237`), `MakeUuidGenerator(name)` (`:243`); база —
> `SqlDialectBase.cs:63,65,70`. Транслятор — `Visitors/UuidFunctionTranslator.cs` (45 строк),
> подключён в `NormSqlTranslator.cs:336` после session/info; два имени удалены из
> `ExtendedScalarFunctionTranslator.DirectFunctions`. Диалекты: `PostgresDialect.cs:94-104`,
> `SqlServerDialect.cs:354-363` (только v4), `ClickHouseDialect.cs:108-118`, `MariaDbDialect.cs:20-30`;
> MySQL/SQLite наследуют `false` из базы. PG2–PG4/PG6/PG7 закрыты: `guide/11` (EN+RU),
> `providers/postgres.md` (EN+RU), `api-reference` (EN+RU), `todo_postgres.md`,
> `sql-capabilities-gap-analysis.md` приведены к кросс-провайдерной форме, а
> `MakeTypeName(typeof(string))` закреплён (`tests/nextorm.postgres.tests/PostgresDialectTests.cs:19-21`).

**Находки по промоушену UUID (точечный аудит 19.09.2026).** Публичная поверхность аддитивна
(переезд `PostgresFunctions` → `CommonFunctions`, не переименование): для потребителя
`SqlFunctions.Postgres.gen_random_uuid()` продолжает компилироваться через наследование. Новые
члены: два метода `CommonFunctions` (`SqlFunctions.cs:225,233`), три члена `ISqlDialect`
(`ISqlDialect.cs:231,237,243`; база `SqlDialectBase.cs:63,65,70`), 12 `public override` в четырёх
диалектах. XML-`<summary>` есть у всех 20 новых публичных членов, включая обе ссылки гейта —
умбреллу и `SupportsUuidGenerator(string)` (в отличие от S4 у session/info, который уже закрыт).
Build Release — **0/0**.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| UG1 | P2 | `SqlFunctions.cs:225,233`; `docs/guide/11-scalar-functions.md:277-284` (EN+RU) | Асимметрия имён: v4 сохранил PostgreSQL-написание `gen_random_uuid`, v7 — версионное `uuidv7`. Теперь семейство кросс-провайдерное, и PG-имя утекает в вызовы SQL Server/MariaDB/ClickHouse (`SqlFunctions.Sql.gen_random_uuid()` → `newid()`/`uuid_v4()`/`generateUUIDv4()`). Имя унаследовано от `PostgresFunctions` (не регресс), а snake_case-зеркало уже принято в §3 | При заморозке Шага 5 рассмотреть симметричную пару `uuidv4`/`uuidv7` (alpha-политика — замена на месте: код, тесты, XML-доки, `docs/**` + `docs/ru/**`). Не блокирует |
| UG2 | P2 | `ISqlDialect.cs:231,237,243`; `SqlDialectBase.cs:63,65,70`; `SqlFunctions.cs:225,233`; `PostgresDialect.cs:94,97,100`, `SqlServerDialect.cs:354,357,360`, `ClickHouseDialect.cs:108,111,114`, `MariaDbDialect.cs:20,23,26` | Новые члены публичного интерфейса (абстрактные — source-breaking для внешних реализаторов; в репозитории реализует только `SqlDialectBase`), два метода переехали `PostgresFunctions` → `CommonFunctions`, 12 `public override` в четырёх диалектах. Шаг 5 открыт: `PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён | Внести три члена `ISqlDialect`, два метода `CommonFunctions` и override в `PublicAPI.Unshipped.txt` при заморозке; переезд даст пару RS0017+RS0016 (ср. S1/PG1/U4/Q3/J1/A3/D1) |
| UG3 | P2 | `PostgresDialect.cs:97`, `MariaDbDialect.cs:23`; `docs/guide/11-scalar-functions.md:283-284` (EN+RU) | Флаг возможностей не зависит от версии сервера: `uuidv7` разрешён на любой PostgreSQL (функция с PG18) и любой MariaDB (11.7+), `gen_random_uuid` — на PG13+. На старом сервере клиент эмитит SQL, падающий на сервере, вместо `NotSupportedException` библиотеки. Версию диалект не знает; ограничение оговорено в XML-доках (`SqlFunctions.cs:222-223,230-231`) и `guide/11` | Оставить как документированное ограничение; при желании — интеграционный прогон на PG18+ / MariaDB 11.7+, чтобы закрепить |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
`dotnet test` (Release, `--no-build`) — core **157/157**, postgres **168/168**, sqlserver **176/176**,
mysql **42/42**, mariadb **11/11**, clickhouse **89/89**, sqlite **205/205** (failed — 0); integration
**909 / 0 failed / 696 skipped** (без `DOCKER_HOST`: реальные PostgreSQL/SQL Server/MySQL/ClickHouse не
запускались, `uuidv7()` исполнялся только в unit-слое); `find -name 'PublicAPI*.txt'` — пусто;
новых публичных типов нет → Приложение A (45) не меняется.

### Session/info-функции промоутнуты в `CommonFunctions` (точечный аудит 19.09.2026)

Публичная поверхность **аддитивна** (пять методов переехали, не переименованы): `current_user()`,
`session_user()`, `current_schema()`, `current_database()`, `version()` → `string?` теперь объявлены на
`CommonFunctions` (`src/nextorm.core/Query/SqlFunctions.cs:177,183,189,195,201`; XML-`<summary>` у всех
пяти) и доступны как `SqlFunctions.Sql.*`; `SqlFunctions.Postgres.*` продолжает компилироваться через
`PostgresFunctions : CommonFunctions` (`SqlFunctions.Postgres.cs:12`). Новый контракт `ISqlDialect`
(`ISqlDialect.cs:203,210,216`; база — `SqlDialectBase.cs:45-48`): умбрелла `SupportsSessionInfoFunctions`
+ `SupportsSessionInfoFunction(string)` + `MakeSessionInfoFunction(string)`. Транслятор —
`Visitors/SessionInfoFunctionTranslator.cs` (47 строк), подключён в `NormSqlTranslator` (`:330`) после
`BuiltinFunctionTranslator`; пять имён удалены из `ExtendedScalarFunctionTranslator`
(`current_date`/`current_time`/`localtime`/`localtimestamp` и `gen_random_uuid`/`uuidv7`/`pg_typeof`
остались). Диалекты: `PostgresDialect.cs:79-89`, `SqlServerDialect.cs:333-345`, `MySqlDialect.cs:50-62`
(наследуется `MariaDbDialect`), `ClickHouseDialect.cs:89-99` (3 из 5), `SqliteDialect.cs:32-37` (только
`version` → `sqlite_version()`). Build Release — **0/0**.

**Три члена — звучат и не дублируют друг друга.** Форма «умбрелла + предикат по имени + рендер по имени»
повторяет уже принятую `SupportsDateTrunc` + `SupportsDateTruncField(string)` +
`MakeDateTrunc(string,string)`; `SupportsTableFunction(string)` — принципиально другая форма (там нет
умбреллы/`Make`-хука и нет частично поддерживаемых семейств), поэтому на неё ориентироваться не нужно.
Умбрелла не избыточна: во-первых, даёт отдельное сообщение уровня семейства
(`SessionInfoFunctionTranslator.cs:33-34`), во-вторых, без неё `SupportsSessionInfoFunction` пришлось бы
считать «умбреллой вообще для любого имени». Предикат по имени обязателен: ClickHouse умеет 3 из 5,
SQLite — 1 из 5, и общий флаг дал бы ложное разрешение. Дефолт `MakeSessionInfoFunction` — `throw`
(`SqlDialectBase.cs:47-48`) — достижим только при рассогласовании предиката и рендера, как у
`MakeRepeat`/`MakeStringPosition`.

**Именование — верное, P1 нет.** Пять имён — SQL/PG-зеркало (`current_user`, `session_user`,
`current_schema` — keyword-формы; `current_database()`, `version()` — функции), что уже закреплено в §3
«Отмечено, но менять не рекомендуется» (как `uniq`/`quantile`/`dict_get`/`json_extract_*`). BCL-конфликта
нет: `version` — метод, а не тип, и не пересекается с `System.Version`. Переезд — это перенос
поверхности, а не переименование; для потребителя вызовы `SqlFunctions.Postgres.*` не меняются, поэтому
это не P1-находка, а допустимое P2-изменение поверхности (S1/S5).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| S1 | P2 | `ISqlDialect.cs:203,210,216`; `SqlFunctions.cs:177-201` | Новые члены публичного интерфейса (абстрактные — source-breaking для внешних реализаторов, в репозитории реализует только `SqlDialectBase`) и пять методов, переехавших на `CommonFunctions`; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Переезд `PostgresFunctions` → `CommonFunctions` в PublicApiAnalyzers даст пару RS0017 (удаление из производного) + RS0016 (добавление в базовый) | Внести три члена `ISqlDialect` и пять методов `CommonFunctions` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3/D1/I8/PG1). Рантайм-совместимость не страдает: унаследованный метод резолвится через базовый тип |
| S2 | P2 | `SqlDialectBase.cs:45-48` | Три новых `public virtual`-члена без XML-`<summary>` (умбрелла — только `//`-комментарий; у двух методов — ни `//`, ни `///`), тогда как соседние `SupportsAnyAggregates` (`:65`), `SupportsJsonExtract` (`:67`), `MakeJsonExtract` (`:70`) документированы | Добавить `<summary>` при закрытии Шага 5 (ср. A2/D2/N2/U5/Q4) |
| S3 | P2 | `PostgresDialect.cs:79-89`, `MySqlDialect.cs:50-62`, `SqliteDialect.cs:32-37` | Переопределения `SupportsSessionInfoFunctions`/`SupportsSessionInfoFunction`/`MakeSessionInfoFunction` без XML-`<summary>` (у MySQL/SQLite — `//`-комментарии, у PostgreSQL — один комментарий на три члена). У SQL Server (`:330-345`) и ClickHouse (`:84-99`) `<summary>` есть — разнобой внутри одного семейства | Добавить `<summary>` в трёх диалектах при закрытии Шага 5 |
| S4 | P2 | `SqlFunctions.cs:173-201` | XML-доки всех пяти методов ссылаются только на умбреллу `ISqlDialect.SupportsSessionInfoFunctions`, но фактический гейт — `SupportsSessionInfoFunction(name)`. На SQLite и ClickHouse умбрелла `true`, а `session_user()`/`current_schema()` бросают — док вводит в заблуждение | Сослаться также на `SupportsSessionInfoFunction(string)` (или только на него) в пяти `<summary>` |
| S5 | P2 | `docs/guide/11-scalar-functions.md:252`, `docs/ru/guide/11-scalar-functions.md:257`; `docs/advanced/api-reference.md:60`, `docs/ru/advanced/api-reference.md:61`; `docs/providers/postgres.md:41-42` | Проза и API-reference по-прежнему подают пять методов как `SqlFunctions.Postgres.*` / PostgreSQL-only; в EN-строке `Sql` (`api-reference.md:58`) семейство не упомянуто, у SQL Server/MySQL/MariaDB/ClickHouse/SQLite в `docs/providers/*.md` его нет (`rg` пуст). AGENTS.md требует синхронно править `docs/**` и `docs/ru/**` | Перевести пять методов в строку `Sql` (`SqlFunctions.Sql.*`), завести кросс-провайдерную таблицу «функция × диалект» (PG/SQL Server/MySQL/MariaDB — все 5; ClickHouse — 3; SQLite — `version`) и продублировать в RU |

**Актуализация 19.09.2026 (промоушен UUID).** S2/S3/S4/S5 закрыты текущим кодом: XML-`<summary>` есть
у всех трёх членов базы (`SqlDialectBase.cs:46-58`) и у переопределений PostgreSQL/MySQL/SQLite
(`PostgresDialect.cs:77-91`, `MySqlDialect.cs:51-67`, `SqliteDialect.cs:31-39`); XML-доки всех пяти
методов ссылаются и на умбреллу, и на `SupportsSessionInfoFunction(string)` (`SqlFunctions.cs:184-217`);
проза EN+RU переведена на `SqlFunctions.Sql.*` и матрицу «функция × диалект»
(`docs/guide/11-scalar-functions.md:260-272` и RU, `docs/providers/*.md`,
`docs/advanced/api-reference.md:59` и RU). S1 (заморозка `PublicAPI.*.txt`) остаётся в силе.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet test` (Release,
`--no-build`) — core **153/153**, postgres **163/163**, sqlserver **171/171**, mysql **38/38**,
mariadb **8/8**, clickhouse **73/73**, sqlite **202/202**, failed — 0; `find -name 'PublicAPI*.txt'` —
пусто; XML-`<summary>` у пяти методов `CommonFunctions` и трёх членов `ISqlDialect` присутствуют;
интеграционные `InfoFunctions_ShouldReturnServerValues` покрыты для PG/SQL Server/MySQL/ClickHouse и
`InfoFunctions_ShouldReturnSqliteVersion` — для SQLite.

### SQL Server условные `IIF`/`CHOOSE` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`SqlServerFunctions.iif<TResult>(bool, TResult?, TResult?)` / `choose<TResult>(int, params TResult?[])` →
`TResult?` (`src/nextorm.core/Query/SqlFunctions.SqlServer.cs:66,73`; XML-`<summary>` у обоих),
`ISqlDialect.SupportsIifChoose` (`ISqlDialect.cs:297`, база `false` — `SqlDialectBase.cs:68`,
SQL Server `true` — `SqlServerDialect.cs:327`). Рендер — `BuiltinFunctionTranslator`
(`:34-39` ветки, `EmitIifChoose` `:115-121`, `FlattenChoose` `:124-134`) через
`SqlOperandTranslator.EmitFunction` (`SqlOperandTranslator.cs:144`); новых `Make*`-хуков нет, т.к.
имена SQL совпадают с CLR. Имена `iif`/`choose` — нижний регистр как SQL-зеркало (реестр §3
«Отмечено, но менять не рекомендуется», как `nullif`/`greatest`), BCL-конфликтов нет, P1 нет.
Build Release — **0/0** (прогнано заново); SQL Server unit **169/169**, PostgreSQL **161/161**
(прогнано заново).

**`TResult?` — верный контракт, в русле DSL.** У неограниченного generic-параметра `T?` — это
nullable-*аннотация*, не `Nullable<T>`: для `int` метаданные дают `int`, для `string` — `string?`.
Поэтому `iif(cond, 1, 0)`/`choose(i, 1, 2)` остаются non-null `int`, а ссылочные ветви корректно
допускают NULL (`CHOOSE` вне диапазона, NULL-ветвь `IIF`) — то же решение, что у `greatest<T>` и
`dict_get<TValue,TKey>`. Оговорка по value-типам: XML-док `choose` обещает «NULL when out of range»
(`SqlFunctions.SqlServer.cs:69-72`), но для `choose(i, 1, 2, 3)` NULL не выражается (`TResult` = `int`),
и материализация DBNull в `int` может упасть; это не новый дефект (та же семантика у
`greatest`/`sum_if`), но документированный NULL-кейс не покрыт — см. I6.

**`iif` отдельным методом (а не маппингом `?:`) — верно.** C#-тернарник — `ConditionalExpression`,
который уже рендерится в переносимый `case when ... end` (`MakeCase`; `PredicateTranslator.cs`).
Подмена его на T-SQL `IIF` сломала бы переносимость и SQL-снапшоты остальных 5 диалектов. `IIF` —
SQL-Server-специфичная форма, поэтому явный opt-in метод на `SqlServerFunctions` с гейтом (аналогично
`json_value`/`string_split`) — правильный компромисс; у `CHOOSE` C#-аналога нет вовсе. Плата —
`IIF` семантически эквивалентен `CASE` (T-SQL документирует `IIF` как сахар над `CASE`), т.е. на
SQL Server метод факультативен; это осознанный паритет с T-SQL, не дефект.

**`FlattenChoose` — верная обработка `params`.** Компилятор C# заворачивает variadic-аргументы в один
`NewArrayExpression`; `FlattenChoose` (`:124-134`) разворачивает его в позиционные операнды, повторяя
`FlattenParams` у `greatest`/`least` (`:329-332`). Для inline-формы `choose(i, "a", "b", "c")` это
даёт ровно `choose(i, 'a', 'b', 'c')` (закреплено тестом). Пограничный случай — явный/захваченный
массив (`var v = new[]{"a","b"}; choose(i, v)`): второй аргумент не `NewArrayExpression`,
`FlattenChoose` возвращает его как массив-операнд, и `SqlOperandTranslator.AppendArrayOperand` молча
биндит его одним параметром → `choose(@i, @p)` невалиден для SQL Server. Это **не регресс** (та же
дыра у `greatest`/`least` `FlattenParams`), но у нового variadic-метода стоит либо бросать понятный
`NotSupportedException` на не-`NewArrayExpression`, либо оговорить в XML-доке (I6). Guard
`Arguments.Count == 2` у `choose` избыточен (`params` всегда даёт 2 аргумента), но безвреден.

**`SupportsIifChoose` — правильный гейт для введённой поверхности.** Единый флаг покрывает обе
функции; это безопасно (только отклоняет, ложного разрешения не даёт), потому что `iif`/`choose` живут
на `SqlServerFunctions` и `true` выставляет только SQL Server. Дробить на `SupportsIif`/`SupportsChoose`
сейчас незачем: второго потребителя нет, а лишний член `ISqlDialect` — лишний source-breaking контракт
к Шагу 5 (то же рассуждение, что в V-разделе о переиспользовании `SupportsJsonExtract`). SQLite
действительно умеет `iif` (3.32+) без `choose`, но в этом изменении `iif` под SQLite **не
предъявляется**; если он появится, флаг придётся разделить (и, вероятно, перенести `iif` в
`CommonFunctions`/`SqliteFunctions`). До тех пор флаг консервативен и верен.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| I1 | P2 | `Query/SqlFunctions.SqlServer.cs:6-11` | Два `<summary>` подряд на `SqlServerFunctions` (строки 6–10 и 11) → дубль в `nextorm.core.xml`/DocFX (аналог Q1/PG5). Основной summary устарел: «Text-JSON surface … and the SQL Server table functions», `iif`/`choose` не упомянуты | Удалить однострочник `:11`; в основную `<summary>` добавить условные `iif`/`choose` |
| I2 | P2 | `Visitors/BuiltinFunctionTranslator.cs:113` | Осиротевший `<summary>` про `date_trunc` остался над `EmitIifChoose` (две `<summary>` на методе); сам `EmitDateTrunc` (`:136`) теперь без доки — вставка метода разорвала пару «док ↔ метод» | Удалить `:113` (или вернуть его над `EmitDateTrunc`, `:136`) |
| I3 | P2 | `Query/SqlFunctions.cs:32-39` | XML-`<summary>` свойства `SqlFunctions.SqlServer` перечисляет только text-JSON и `string_split`/`openjson`; `iif`/`choose` не упомянуты (кумулятивно устарело, аналог D3/U3) | Дополнить перечень условными `iif`/`choose` |
| I4 | P2 | `docs/ru/advanced/api-reference.md` | RU-зеркало не содержит строки `SqlFunctions.SqlServer` вовсе (EN `:61` этой правкой дополнена `iif`/`choose`); `Postgres`/`ClickHouse` в RU есть (`:60`,`:61`) — пре-существующий пробел U2, но изменённая строка EN не отражена | Завести RU-строку `SqlServer` с `iif`/`choose` (AGENTS.md: `docs/**` и `docs/ru/**`) |
| I5 | P2 | `docs/guide/11-scalar-functions.md:496`, `docs/ru/guide/11-scalar-functions.md:505` | Раздел «Conditional helpers»/«Условные функции» (только `nullif`/`greatest`/`least`/`num_*`) не упоминает новое `iif`/`choose`; обновлены лишь `providers/sqlserver.md` и EN api-reference | Добавить `iif`/`choose` + `SupportsIifChoose` в оба зеркала |
| I6 | P2 | `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:46`; `tests/nextorm.integration.tests/SqlServerSpecificTests.cs:15-31` | `iif` закреплён только слабым `Contain("iif(")`; `choose` вне диапазона (документированный NULL) и захваченный массив не покрыты | Усилить до полного `iif(id > 0, 'yes', 'no')`; добавить `choose(99, …)` → `null` (string) и тест/guard на не-`NewArrayExpression` |
| I7 | P2 | `docs/specs/roadmap/WIP_iif_choose.md:42` | Заявлен «SQL-gen PostgreSQL/SQLite: `IifChoose_ShouldThrow`», но SQLite-теста нет (`rg IifChoose tests` — только postgres/sqlserver/integration) | Исправить на PostgreSQL; SQLite добавить отдельно либо убрать из плана |
| I8 | P2 | `ISqlDialect.cs:297`, `SqlDialectBase.cs:68`, `SqlServerDialect.cs:327`, `Query/SqlFunctions.SqlServer.cs:66,73` | Новые члены публичной поверхности; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён) | Внести в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3/D1/PG1) |

**Пробелы документации EN/RU.** Обновлены и синхронизированы: `docs/providers/sqlserver.md`
(EN `:105-107`,`:182`; RU `:107-109`,`:185`), `docs/advanced/api-reference.md:61` (EN),
`docs/specs/roadmap/todo_mssql.md:62-66` (`[x]`),
`docs/specs/roadmap/sql-capabilities-gap-analysis.md:125`. Не
отражено: `docs/ru/advanced/api-reference.md` (I4), `docs/guide/11` EN+RU (I5),
`docs/specs/roadmap/WIP_iif_choose.md:42` (I7).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
`dotnet test tests/nextorm.sqlserver.tests -c Release --no-build` — **169/169**, `tests/nextorm.postgres.tests`
— **161/161** (прогнано заново); `find -name 'PublicAPI*.txt'` — пусто; `rg "SupportsIifChoose" src`
— 4 вхождения (интерфейс/база/диалект/транслятор).

> **Актуализация 19.09.2026 (верификация).** I1–I5 и I7 закрыты текущим кодом, I6 закрыт частично,
> I8 заменён: объединённый `SupportsIifChoose` удалён и разделён на `SupportsIif`/`SupportsChoose`
> (+ хук `MakeIif`), `iif` переехал на `CommonFunctions`. `SqlServerFunctions.iif` в коде больше не
> объявлен (работает по наследованию). Разбор и актуальный список заморозки — в разделе «Повторная
> верификация cross-provider `iif`/`choose`/`nth_value`».

### ClickHouse `GROUP BY ... WITH TOTALS` (точечный аудит 19.09.2026)

Область: `EntityBuilder<TEntity>.WithTotals()` (`EntityBuilder.cs:444-451`), `GroupByWithTotals` на
`QueryDefinition` (`:41`) и `QueryCommand` (`:159`), `ISqlDialect.SupportsGroupByWithTotals`/
`MakeGroupByTotals` (`ISqlDialect.cs:313-324`) + реализации (`SqlDialectBase.cs:73-77`,
`ClickHouseDialect.cs:140-151`). Плюмбинг: `Select`/`ToCommand`/`CopyTo` + join-инициализаторы,
`QueryPlanEqualityComparer`, `QueryPreparer`, `SqlBuilder`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| WT1 | P2 | `ISqlDialect.cs:313-324`, `SqlDialectBase.cs:73-77`, `ClickHouseDialect.cs:140-151`, `EntityBuilder.cs:444` | Новые члены публичной поверхности; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Абстрактные `SupportsGroupByWithTotals`/`MakeGroupByTotals` — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | Внести `WithTotals()`, `GroupByWithTotals` (`QueryDefinition`/`QueryCommand`), `SupportsGroupByWithTotals` + `MakeGroupByTotals` (интерфейс/база/ClickHouse) в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3/D1/PG1/I8) |
| WT2 | P2 | `docs/guide/04-grouping-and-aggregates.md:193-197`; `docs/ru/guide/04-grouping-and-aggregates.md:175-179` | Абзац о `WITH TOTALS` лежит внутри раздела `## GROUPING SETS`, хотя модификатор общий (сочетается с ROLLUP/CUBE) и помещён туда по остаточному принципу; в RU он идёт до объяснения grouping sets, в EN — после. Раздел «Provider differences» (`:504-510` EN / `:505-511` RU) перечисляет ROLLUP/CUBE/GROUPING SETS, но не WITH TOTALS | Вынести абзац в отдельный подраздел после ROLLUP/CUBE (EN+RU) и добавить в «Provider differences» строку про ClickHouse-only + ограничение `ClickHouse.Driver` |

**Именование — конвенции соблюдены, P0/P1 нет.** `WithTotals()` (ср. `WithTableHint`),
`GroupByWithTotals` (ср. `GroupingType`/`GroupingSets`), `SupportsGroupByWithTotals` (ср.
`SupportsRollup`/`SupportsCube`/`SupportsGroupingSets`), `MakeGroupByTotals` (ср. `MakeGrouping`/
`MakeGroupingSets`). Кириллических/BCL-конфликтных имён нет.

**Дизайн «флаг + `MakeGroupByTotals`» выбран верно** (вместо расширения `GroupingType`/`MakeGrouping`).
`WITH TOTALS` ортогонален `ROLLUP`/`CUBE`: грамматика ClickHouse прямо допускает несколько модификаторов
(`ParserSelectQuery.cpp`: «multiple modifiers allowed, e.g. WITH ROLLUP WITH CUBE WITH TOTALS»), а
`MakeGrouping(columns, groupingType)` по контракту одно-модификаторный. Расширение `GroupingType` дало бы
комбинаторные значения (`RollupWithTotals`, `CubeWithTotals`, …) и сломало бы этот контракт. Пара
`SupportsX` + `MakeX` точно повторяет существующий паттерн (`SupportsRollup`/`MakeGrouping`,
`SupportsGroupingSets`/`MakeGroupingSets`).

**Ключ плана — полон, но продублирован.** Флаг участвует в `Equals` (`QueryPlanEqualityComparer.cs:45`)
и `GetHashCode` (`:185`) — этого достаточно. Дополнительный `+1` в `GroupingPlanHash`
(`QueryCommand.QueryPreparer.cs:500-505`) избыточен, а его комментарий неверен. `CopyTo` (`QueryCommand.Clone.cs:45-46`)
флаг явно не копирует, но клон строится через `Definition`, который его несёт (`QueryCommand.cs:120`),
поэтому корректность держится. Подробности и рекомендации — в `code-smells-review.md` (аудит WITH TOTALS).

**Запрет `GROUPING SETS` — верен.** ClickHouse сам отклоняет комбинацию:
`NOT_IMPLEMENTED: "WITH TOTALS and GROUPING SETS are not supported together"`
(`src/Interpreters/InterpreterSelectQuery.cpp:1849`). nextorm бросает `NotSupportedException` до рендера
(`SqlBuilder.cs:130-131`). Проверка не привязана к флагу диалекта, но сегодня флаг
`SupportsGroupByWithTotals` выставлен только у ClickHouse, так что решение консервативное и безопасное;
если появится провайдер с поддержкой комбинации, проверку придётся сделать диалект-зависимой.

**XML-документация — все новые члены задокументированы.** `<summary>` есть у `WithTotals`,
`QueryDefinition.GroupByWithTotals`, `QueryCommand.GroupByWithTotals`, `SupportsGroupByWithTotals`/
`MakeGroupByTotals` (интерфейс, база, ClickHouse). Новых публичных типов нет → Приложение A (45,
исторический список на `d21c473`) не меняется. `CS1591` по-прежнему в `<NoWarn>` 7 библиотечных
`.csproj` (Шаг 5).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **73/73**,
`tests/nextorm.postgres.tests --filter FullyQualifiedName~GroupByWithTotals` — **1/1**; `find -name
'PublicAPI*.txt'` — пусто; `AnalysisLevel=latest-all` (rebuild) — **0** предупреждений на новых строках
(в т.ч. `ClickHouseDialect.cs:140-151`).

### ClickHouse `LIMIT n BY expr` (точечный аудит 19.09.2026)

Область: `EntityBuilder<TEntity>.LimitBy<TResult>` ×2 (`Builders/EntityBuilder.cs:204-226`),
`ISqlDialect.SupportsLimitBy`/`MakeLimitBy` (`DataContext/Dialect/ISqlDialect.cs:621-630`),
`SqlDialectBase` (`:96-102`), `ClickHouseDialect` (`:148-158`); `LimitByClause` — `internal`
(`Query/LimitByClause.cs:11`) → в публичную поверхность не входит. Build Release — **0/0**;
XML-`<summary>` есть у **7 из 8** новых публичных членов (единственный пропуск — `SqlDialectBase.SupportsLimitBy`).
Новых публичных типов нет → Приложение A (45) без изменений.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| LIM1 | P2 | `ISqlDialect.cs:625,630` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Абстрактные — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | Внести `SupportsLimitBy` + `MakeLimitBy`, `EntityBuilder<TEntity>.LimitBy` ×2 и override'ы `ClickHouseDialect` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3/D1/PG1/I8/WT1) |
| LIM2 | P2 | `SqlDialectBase.cs:96` | Новый `public virtual bool SupportsLimitBy => false` без XML-`<summary>` (соседний `MakeLimitBy` `:98-101` документирован) — тот же разнобой, что D2/S2/U5/N2/Q4/A2 | Добавить `<summary>` при закрытии Шага 5 |
| LIM3 | P2 | `docs/providers/clickhouse.md` (+`docs/ru/providers/clickhouse.md`), `docs/guide/05-sorting-and-paging.md` (+RU) | Новый публичный `LimitBy`/`LIMIT BY` не упомянут в пользовательских доках: страница ClickHouse перечисляет `limit`/`offset` (EN `:47-48` / RU `:47-48`) и таблицу возможностей (`:113`), но не `limit by`; гайд по пейджингу знает только `Limit`/`Offset`/`Page` (EN `:15,111-159`). AGENTS.md требует правку `docs/**` **и** `docs/ru/**` в том же изменении | Добавить bullet + строку таблицы (`LIMIT n BY` / `SupportsLimitBy`) и абзац в гайд (EN+RU) |
| LIM4 | P2 | `docs/specs/roadmap/todo_clickhouse.md:176` | Пункт `[ ] LIMIT n BY expr` не отмечен выполненным, хотя WIP и код на месте; `WIP_limit_by.md:70-72` («Результат») пуст | Поставить `[x]` и заполнить «Результат» (ср. I7 — расхождение WIP с фактом) |

ℹ️ **Наблюдения (фикс не требуется):**
- `EntityBuilder<TEntity>.LimitBy<TResult>(int, Expression<Func<TEntity,TResult>>)` использует `TResult`
  для **селектора ключа**; по Framework Design Guidelines это `TKey`. Но ровно так же назван уже
  существующий `GroupBy<TResult>` (`EntityBuilder.cs:433,445,458,488`), поэтому переименование имеет
  смысл только парой (иначе рассогласование сильнее); не находка.
- `LimitByClause` внутренний — верно: `QueryDefinition.LimitBy`/`QueryCommand.LimitBy` помечены
  `internal`, `EntityBuilder.LimitByClause` — `internal`, так что тип не протекает в публичную
  поверхность и не требует документации Приложения A.
- XML-док `ISqlDialect` `<see cref="LimitByClause"/>` (`:622-623`) ссылается на `internal`-тип из
  публичного члена: внутри сборки это разрешается, но потребитель в IntelliSense/DocFX может увидеть
  неразрешимую ссылку — можно заменить на прозу «the LIMIT BY clause».

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **76/76**,
`tests/nextorm.postgres.tests` — **164/164**, `tests/nextorm.core.tests` — **155/155**;
`rg -i "limitby" docs --glob '!docs/specs/**'` — пусто (подтверждает LIM3; совпадения только в
`docs/specs/roadmap/`).

### ClickHouse-модификаторы `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS` (точечный аудит 19.09.2026)

Область: `EntityBuilder<TEntity>.Final`/`Sample`×2/`Settings`/`PreWhere` (`Builders/EntityBuilder.cs:195-264`), `QueryDefinition.Final`/`SampleRatio`/`SampleOffset`/`Settings`/`PreWhere` (`Query/QueryDefinition.cs:45-53`), `QueryCommand`-свойства + `PreparedPreWhere`/`PreWhereShapeHash` (`Query/QueryCommand.cs:181-194`), `ISqlDialect.SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings` + `MakeFinal`/`MakeSample`/`MakeSettings` (`DataContext/Dialect/ISqlDialect.cs:651-673`), `SqlDialectBase` (`:105-131`), `ClickHouseDialect` (`:154-164`). Build Release — **0/0**; XML-`<summary>` есть у **всех** новых публичных членов; новых публичных типов нет → Приложение A (45) без изменений.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| FM1 | P2 | ✅ **Закрыто 19.09.2026.** `docs/providers/clickhouse.md:52-56,124` (+RU `:50-53,124`), `docs/guide/17-query-hints.md:84-110` (+RU `:85-111`) | Было: EN/RU не упоминали `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`. Стало: список модификаторов + `xref` на `Supports*` в таблице возможностей, пример SQL, оговорка про `Memory`-движок; оба зеркала синхронны (AGENTS.md) | — |
| FM2 | P2 | `EntityBuilder.cs:235-251`; `SqlDialectBase.cs:130-131` | Публичная `Settings(params (string Key, string Value)[])` использует `ValueTuple` (имена элементов не часть CLR-контракта) и не совпадает по типу с `IReadOnlyList<KeyValuePair<string,string>>` на `QueryCommand`/`QueryDefinition`; значения интерполируются **сырыми** (`key = value`), поэтому строковый параметр ClickHouse (`'…'`) передаётся только вручную — безопасной перегрузки нет | Привести к одному типу (`IEnumerable<KeyValuePair<string,string>>`/`IReadOnlyList<…>`) либо оставить tuple, но добавить типизированную/экранирующую перегрузку; XML-док уже предупреждает — закрепить в Шаге 5 |
| FM3 | P2 | `QueryCommand.cs:181-194` (ср. `:178`) | Новые `Final`/`SampleRatio`/`SampleOffset`/`Settings`/`PreWhere` — `public` с `internal set`, тогда как ClickHouse-сосед `LimitBy` — `internal`; ClickHouse-специфичное состояние выставлено наружу непоследовательно | Выбрать один уровень: `internal` как `LimitBy` либо сознательно `public` + запись в `PublicAPI.Unshipped.txt` (FM5) |
| FM4 | P2 | `SqlBuilder.cs:79-93` | `FINAL`/`SAMPLE` дописываются к **любому** `from`, включая производную таблицу (`DataContextExtensions.From(QueryCommand)`, `DataContextExtensions.cs:87-88`) и TVF (`FromTableFunction`, `:104`); ClickHouse допускает их только у физической таблицы (`FROM table [AS alias] FINAL …`) → `from (select …) as t final` — синтаксическая ошибка. Guard'а нет | Бросать понятный `NotSupportedException`, когда `from.SubQuery`/`from.TableFunction` не `null`, либо оговорить ограничение в XML-доке (ср. наблюдения LIM) |
| FM5 | P2 | `ISqlDialect.cs:651-673`, `SqlDialectBase.cs:105-131`, `ClickHouseDialect.cs:154-164`, `QueryDefinition.cs:45-53`, `QueryCommand.cs:181-194`, `EntityBuilder.cs:195-264` | Новые члены публичной поверхности; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Абстрактные члены `ISqlDialect` — source-breaking для внешних реализаторов (в репозитории реализует только `SqlDialectBase`) | Внести `Final`/`Sample`×2/`Settings`/`PreWhere`, свойства `QueryDefinition`/`QueryCommand` и 4 пары `SupportsX`/`MakeX` в `PublicAPI.Unshipped.txt` при заморозке (ср. LIM1/WT1/I8) |
| FM6 | P2 | `EntityBuilder.cs:219,222` | `Sample(ratio[, offset])` пропускает `double.NaN`: `ratio is < 0 or > 1` для `NaN` ложно, поэтому `Sample(double.NaN)` проходит и рендерится как `sample NaN` (`SqlDialectBase.cs:117`) — ошибка ClickHouse в рантайме вместо `ArgumentOutOfRangeException`; `Infinity` ловится (`> 1`) | Добавить `double.IsFinite(ratio)`/`IsFinite(offset)` (или `ratio is not (>= 0 and <= 1)`) |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именования — конвенции соблюдены, P0/P1 нет.** `Final()`/`Sample()`/`PreWhere()` — императивные fluent-методы (ср. `Where`/`LimitBy`); `Settings` — существительное (ср. `Distinct`/`LimitBy`); `SupportsFinal`/`MakeFinal` и т.д. точно повторяют паттерн `SupportsLimitBy`/`MakeLimitBy`/`SupportsRollup`/`MakeGrouping`. BCL-конфликтов нет.
- **`IsFinal` (builder) vs `Final` (command/definition)** — разнобой `Is`-префикса (`IsDistinct` везде `Is`); косметика, менять только парой с `IsDistinct`.
- **Порядок рендера верен.** FINAL/SAMPLE после алиаса, `PREWHERE` после `JOIN` и до `WHERE`, `SETTINGS` в конце — совпадает с грамматикой ClickHouse (`ParserTableExpression`/`ASTTableExpression`); включая случай `needAlias` (join/correlated).
- **`SqlDialectBase` даёт generic-тела `MakeFinal`/`MakeSample`/`MakeSettings` при флагах `false`** — достижимы только через диалект, выставивший флаг (как `MakeGrouping`); для контраста `MakeLimitBy` бросает `NotSupportedException` — разнобой стиля (ℹ️).
- **WIP-расхождения.** `WIP_query_modifiers.md:48-56` заявляет имена тестов, которых нет; фактические — `QueryModifiers_ShouldThrowBecausePostgresHasNone` (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1440`), `MakeQueryModifiers_ShouldRender` (`tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs`) и один интеграционный `QueryModifiers_ShouldExecute` (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:141`, только SETTINGS). ✅ `WIP_query_modifiers.md:63-82` («Результат») заполнен 19.09.2026 (ранее запись «`:63-65` пуст» устарела); остаётся расхождение имён: WIP обещает `Final_ShouldThrowBecausePostgresHasNoFinal`/три интеграционных `*_ShouldExecute`, фактические — один `QueryModifiers_ShouldThrowBecausePostgresHasNone` и один `QueryModifiers_ShouldExecute`; `Sample`/`Settings` на in-memory тем же guard'ом покрыты, но тестом прямо не утверждены.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **83/83**, `tests/nextorm.core.tests` — **156/156**; `tests/nextorm.postgres.tests` — **165/165** (прежний unrelated-провал `OrderedSetAggregates_ShouldEmitWithinGroup` в текущем дереве зелёный); модификаторы в `docs/providers`/`docs/guide` EN+RU описаны (FM1 закрыт); `find -name 'PublicAPI*.txt'` — пусто (FM5).

### Cross-provider `any_agg` и оконные `percentile_cont`/`percentile_disc` (точечный аудит 19.09.2026)

Область: промоушен `any_agg` из `ClickHouseFunctions` в `CommonFunctions` (`Query/SqlFunctions.cs:318-324`), `ClickHouseFunctions.any_last` (`Query/SqlFunctions.ClickHouse.cs:82`); флаги `ISqlDialect.SupportsAnyValueAggregate`/`SupportsAnyAggregates`/`SupportsPercentileWindow` (`DataContext/Dialect/ISqlDialect.cs:163,306,313`) + `SqlDialectBase` (`:36-37,75-79`); `MySqlDialect` (`:49,215`), `MariaDbDialect` (`:17`), `ClickHouseDialect` (`:51-55,239`), `SqlServerDialect` (`:114-116`); оконные `CommonFunctions.percentile_cont`/`percentile_disc` (`SqlFunctions.cs:404-416`) против ordered-set `PostgresFunctions.percentile_cont`/`percentile_disc` (`SqlFunctions.Postgres.cs:452-459`); `WindowSql.cs:25-26`, `WindowFunctionTranslator.cs:50-52,78-85`, `NormSqlTranslator.cs:67-73`, `AdvancedAggregateTranslator.cs:84-89,123-128`. Тесты: `AnyValueAggregate_*` в mysql/clickhouse/sqlserver/sqlite/postgres + 6 dialect-assertions; `PercentileWindow_*` в sqlserver/mariadb/mysql; интеграционные `MySqlSpecificTests.AnyValueAggregate_ShouldReturnGroupValue`, `SqlServerSpecificTests.PercentileWindow_ShouldReturnValue`. Build Release — **0/0**; XML-`<summary>` есть у всех новых `CommonFunctions`/`ISqlDialect`-членов; новых публичных типов нет → Приложение A (45) без изменений.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| XP1 | P2 | `SqlFunctions.cs:318-321`, `ISqlDialect.cs:308-313`, `SqlDialectBase.cs:78` | XML-доки утверждают, что `any_agg` рендерится как `ANY_VALUE(x)` **SQL Server**'ом («Rendered as `ANY_VALUE(x)` by SQL Server and MySQL/MariaDB»; «SQL Server, MySQL/MariaDB and ClickHouse opt into…»), но `SqlServerDialect` `SupportsAnyValueAggregate` **не переопределяет** — остаётся `false` (`tests/nextorm.sqlserver.tests/SqlServerDialectTests.cs:242`, `SqlGenerationTests.cs:1477`). `ANY_VALUE` есть только в SQL Server 2025/Fabric, включение отложено осознанно | Убрать SQL Server из всех трёх доков: `ANY_VALUE` — MySQL/MariaDB, `any` — ClickHouse; оговорить, что SQL Server 2025+ намеренно не включён |
| XP2 | P1 | `SqlFunctions.cs:413` vs `SqlFunctions.Postgres.cs:456` (и `:416`/`:459`) | Одноимённые `percentile_cont`/`percentile_disc`: оконная (`CommonFunctions`, возврат `WindowFunction<T?>`) и ordered-set (`PostgresFunctions`, возврат `T?`). `PostgresFunctions : CommonFunctions`, поэтому оконная перегрузка **наследуется** на Postgres-поверхность, и `SqlFunctions.Postgres.percentile_cont(0.5, x.Id)` биндится к ней, падая «must be completed with Over(...)» (`NormSqlTranslator.cs:71-73`) вместо ordered-set. Имя-SQL-зеркало осознанно (реестр §3), поэтому не переименовываем | В XML-док `PostgresFunctions.percentile_cont/disc` явно указать: value-форма требует `Expression<Func<T>>` (лямбда), а унаследованная `(double, T?)` — оконная и на PostgreSQL недоступна; добавить регресс-тест на сообщение для value-формы. Диспетчеризацию транслятора укрепить — см. `code-smells-review.md`, Находка 13 |
| XP3 | P2 | `ISqlDialect.cs:306` | `SupportsAnyAggregates` (мн. ч.) после промоушена `any_agg` гейтит **только** `any_last` (док `:300-306` сужен) — имя вводит в заблуждение | Переименовать на месте в `SupportsAnyLastAggregate` (alpha; ср. A3 и реестр §3) либо оставить с явной оговоркой; при заморозке внести в `PublicAPI.Unshipped.txt` |
| XP4 | P2 | `SqlServerDialect.cs:114-116` | Новый `public override SupportsPercentileWindow` задокументирован `//`, а не `///`; `<summary>` отсутствует (у базы `SqlDialectBase.cs:36-37` и MariaDB `MariaDbDialect.cs:16-17` — есть) | Добавить `<summary>` при закрытии Шага 5 (ср. N2/U5/Q4/A2) |
| XP5 | P2 | `SqlFunctions.ClickHouse.cs:5-13`, `SqlFunctions.cs:41-49` | Доки `ClickHouseFunctions` и свойства `SqlFunctions.ClickHouse` по-прежнему перечисляют `any`/`anyLast` как ClickHouse-only; `any` (= `any_agg`) переехал в `CommonFunctions` и стал cross-provider (MySQL/MariaDB/ClickHouse), у ClickHouse остался только `any_last` | Убрать `any` из обоих списков, оставить `anyLast` со ссылкой на `CommonFunctions.any_agg` |
| XP6 | P2 | `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:299`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1346` | Вызовы `SqlFunctions.ClickHouse.any_agg` компилируются через наследование от `CommonFunctions`, но вводят в заблуждение: `AnyAggregates_ShouldUseClickHouseNames` теперь покрывает не-ClickHouse метод, а postgres-тест «нет any» идёт через ClickHouse-поверхность. В PublicApiAnalyzers это даст пару RS0017 (удалён из производного) + RS0016 (добавлен в базовый) | Заменить на `SqlFunctions.Sql.any_agg`; в ClickHouse-тесте оставить только `any_last` (дубль `any` уже покрыт `AnyValueAggregate_ShouldUseClickHouseAny:308`) |
| XP7 | P2 | `ISqlDialect.cs:163,313`; `SqlDialectBase.cs:37,79`; `MySqlDialect.cs:49`; `MariaDbDialect.cs:17`; `ClickHouseDialect.cs:55`; `SqlServerDialect.cs:116`; `SqlFunctions.cs:324,413,416` | Новые члены публичной поверхности; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Абстрактные `SupportsAnyValueAggregate`/`SupportsPercentileWindow` — source-breaking для внешних реализаторов (в репозитории реализует только `SqlDialectBase`); перенос `any_agg` из производного `ClickHouseFunctions` в базовый `CommonFunctions` — пара RS0017+RS0016 | Внести в `PublicAPI.Unshipped.txt` при заморозке: `SupportsAnyValueAggregate`, `SupportsPercentileWindow`, `CommonFunctions.any_agg<T>`, `percentile_cont<T>`, `percentile_disc<T>` + override'ы (ср. A3/Q3/C/U4) |

ℹ️ **Наблюдения (фикс не требуется):**
- **`any_agg` рядом с `any` — верное решение сохранено.** Перенос в `CommonFunctions` не создаёт коллизии с квантором `any` (`CommonFunctions.any<T>(QueryCommand<T>)`), т.к. `TypeFacts.IsPredicateCall`/`NormSqlTranslator`/`ArraySqlTranslator`/`CorrelatedQueryExpressionVisitor` диспетчеризуют по имени `"any"`, а `any_agg` его не даёт; обоснование из аудита A (выше) остаётся в силе.
- **Флаги согласованы с паттерном `SupportsX` (default `false` в базе).** `T?`-контракт `any_agg` корректен (в отличие от `uniq`/`quantile`, каст не нужен); SQL Server намеренно `false`.
- **Оконный рендер верен.** `percentile_cont(fraction) within group (order by value) over (...)` (`WindowFunctionTranslator.cs:78-85`) — стандартная форма SQL Server/MariaDB; PostgreSQL ordered-set (`MakeWithinGroup`, без `over`) отделён проверкой `DeclaringType` (`NormSqlTranslator.cs:71-73`).
- **`percentile_disc`-summary короче, чем у `percentile_cont`, но присутствует**; `CS1591` подавлен в 7 `.csproj` (Шаг 5).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/<p> -c Release --no-build` — core **156/156**, mysql **40/40**, mariadb **9/9**, clickhouse **83/83**, sqlserver **173/173**, postgres **165/165**, sqlite **203/203** (0 failed); `find -name 'PublicAPI*.txt'` — пусто; контейнерные интеграционные тесты не запускались (Podman-сокет отсутствует) — SQL-gen + все unit зелёные.

**Статус фиксов (19.09.2026, внесены в том же изменении):** XP1, XP2, XP4, XP5, XP6 и Находка 13 устранены — SQL Server убран из XML-доков `any_agg` (три места); `PostgresFunctions.percentile_cont`/`percentile_disc` получили пояснение про лямбда-форму и ссылку на оконную перегрузку; `SqlServerDialect.SupportsPercentileWindow` документирован `<summary>`; доки `ClickHouseFunctions`/`SqlFunctions.ClickHouse` оставляют только `any_last`; тесты переведены на `SqlFunctions.Sql.any_agg`; `AdvancedAggregateTranslator` гейтит percentile по `DeclaringType == typeof(PostgresFunctions)`. XP3 (имя `SupportsAnyAggregates`) оставлен с явной оговоркой в доке; XP7 (PublicAPI) — при заморозке Шага 5.

### ClickHouse табличные функции `numbers`/`numbers_mt` (точечный аудит 19.09.2026)

Область: `SqlFunctions.INumbersRow` (`Query/SqlFunctions.cs:99-108`), `ClickHouseFunctions.numbers` ×3 + `numbers_mt` (`Query/SqlFunctions.ClickHouse.cs:145-167`), `ISqlDialect.SupportsTableFunction`/`WrapTableFunction` (`DataContext/Dialect/ISqlDialect.cs:643,671`), `SqlDialectBase.WrapTableFunction` (`:122`), `ClickHouseDialect.SupportsTableFunction`/`WrapTableFunction` (`:165-176`), `SqlSourceRenderer.MakeTableFunction` (`:274-338`, обёртка `:312-317`). Build Release — **0/0**; unit: clickhouse **89/89**, core **157/157**, postgres **168/168** (0 failed); контейнерная интеграция `NumbersTableFunction_ShouldReturnThreeRows` — зелёная (реальный ClickHouse). XML-`<summary>` есть у всех новых публичных членов. **Новый публичный тип** `SqlFunctions.INumbersRow` задокументирован → Приложение A (45) без изменений; Шаг 5 по-прежнему открыт.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| TF1 | P2 | `DataContext/Dialect/ISqlDialect.cs:669-671` | Новый член публичного интерфейса (абстрактный — source-breaking для внешних реализаторов `ISqlDialect`; в репозитории реализует только `SqlDialectBase`). Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён) | Внести `ISqlDialect.WrapTableFunction`, `SqlDialectBase.WrapTableFunction`, `ClickHouseDialect.WrapTableFunction`, `SqlFunctions.INumbersRow` (+`Value`) и 4 метода `ClickHouseFunctions.numbers*` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3/D1/PG1/I8/WT1/LIM1/FM5) |
| TF2 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-13`, `Query/SqlFunctions.cs:41-49` | Док `ClickHouseFunctions` и свойства `SqlFunctions.ClickHouse` перечисляют агрегаты/JSON/словари, но **не** `numbers`/`numbers_mt` (кумулятивно устарело, ср. D3/U3/I3/XP5) | Дополнить оба перечня табличными функциями |
| TF3 | P2 | `DataContext/Dialect/ISqlDialect.cs:637-643` | `<summary>` `SupportsTableFunction` перечисляет `generate_series`/`unnest`/`string_split`/`openjson`, но не `numbers`/`numbers_mt` | Добавить ClickHouse-пример |
| TF4 | P2 | `docs/advanced/api-reference.md:62`, `docs/ru/advanced/api-reference.md:62` | Строка `ClickHouse` не содержит `numbers`/`numbers_mt` (EN и RU синхронны между собой, но обе устарели) | Добавить табличные функции в обе ветки |
| TF5 | P2 | `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:462-486` | `TableFunction_Numbers_ShouldEmitCall`/`_NumbersMt_` проверяют только `Contain("from numbers(@count)")`, что верно и **без** обёртки; хук `WrapTableFunction` (суть изменения) и `numbers_mt`-обёртка юнит-тестом не закреплены — слой `(select toInt64(number) as number from …)` держит только интеграционный тест (аналог U6 для `uniq`) | Усилить до `Contain("(select toInt64(number) as number from numbers(@count))")` + добавить dialect-assert `WrapTableFunction("numbers", "numbers(@count)") == "(select toInt64(number) as number from numbers(@count))"` и `WrapTableFunction("numbers_mt", …)` |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** `numbers`/`numbers_mt` — сознательное SQL-зеркало (реестр §3, как `uniq`/`quantile`/`any_agg`); `INumbersRow` повторяет `IGenerateSeriesRow`/`IUnnestRow<T>`/`IStringSplitRow`/`IOpenJsonRow`. BCL-конфликтов нет.
- **`WrapTableFunction` vs `Make*`-семейство.** Имя выбивается из `MakeApply`/`MakeFinal`/`MakeGrouping` (глагол `Wrap`, не `Make`), но семантически точнее; косметика.
- **Пользовательские TVF.** `SqlSourceRenderer.cs:312` вызывает хук для **любой** `[SqlTableFunction]`, а ClickHouse-диалект ключуется по имени: пользовательская TVF с именем `numbers` будет ошибочно обёрнута. Край, не дефект текущего API.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **89/89**, `tests/nextorm.core.tests` — **157/157**, `tests/nextorm.postgres.tests` — **168/168**; `rg "toInt64\(number\)|WrapTableFunction" tests/` — пусто (подтверждает TF5); `find -name 'PublicAPI*.txt'` — пусто; `docs/guide/13-table-valued-functions.md` EN+RU `:240-261`, `docs/providers/clickhouse.md` EN+RU `:53,127`, `todo_clickhouse.md:212-219` (`[x]`), `sql-capabilities-gap-analysis.md:107` — синхронны.

### ClickHouse табличные функции `zeros`/`zeros_mt` (точечный аудит 20.09.2026)

Область: `SqlFunctions.IZerosRow` (`Query/SqlFunctions.cs:111-120`), `ClickHouseFunctions.zeros`/`zeros_mt` (`Query/SqlFunctions.ClickHouse.cs:170-185`), `ClickHouseDialect.SupportsTableFunction` (`src/nextorm.clickhouse/ClickHouseDialect.cs:183-185`), `SqlSourceRenderer.MakeTableFunction` (`DataContext/SqlSourceRenderer.cs:311-317`), док класса `ClickHouseFunctions` (`Query/SqlFunctions.ClickHouse.cs:5-12`), док свойства `SqlFunctions.ClickHouse` (`Query/SqlFunctions.cs:41-49`), док `ISqlDialect.SupportsTableFunction` (`DataContext/Dialect/ISqlDialect.cs:653-661`); тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:555-579`, `ClickHouseDialectTests.cs:98-105`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1458-1468`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:177-187`. Build Release — **0/0**; unit: clickhouse **96/96**, core **157/157**, postgres **171/171** (0 failed); контейнерная интеграция `ZerosTableFunction_ShouldReturnThreeRows` — зелёная на реальном ClickHouse (зафиксировано в `WIP_clickhouse_table_functions_remaining.md`; в этом аудите не перезапускалась — `docker`/`podman` CLI недоступны). XML-`<summary>` есть у типа `IZerosRow` и обоих методов; свойство `Value` — без отдельного `<summary>`, ровно как `INumbersRow.Value`/`IGenerateSeriesRow.Value` (тип документирован, посвойственного док-стиля в семействе row-интерфейсов нет, `CS1591` в `<NoWarn>`). **Новый публичный тип** `SqlFunctions.IZerosRow` задокументирован → Приложение A (45) без изменений; Шаг 5 по-прежнему открыт.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| Z1 | P2 | `Query/SqlFunctions.cs:116-120`; `Query/SqlFunctions.ClickHouse.cs:177-185` | Новые публичные члены (`IZerosRow` + `Value`, `zeros`, `zeros_mt`) не отслеживаются: `PublicAPI.Shipped/Unshipped.txt` отсутствуют, `PublicApiAnalyzers` не подключён. Шаг 5 открыт | Внести `SqlFunctions.IZerosRow` (+`Value.get/set`) и `ClickHouseFunctions.zeros`/`zeros_mt` в `PublicAPI.Unshipped.txt` при заморозке (ср. TF1/CNT1) |
| Z2 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-12`; `Query/SqlFunctions.cs:41-49`; `DataContext/Dialect/ISqlDialect.cs:653-661` | Кумулятивный док-пробел: `ClickHouseFunctions`, свойство `SqlFunctions.ClickHouse` и `<summary>` `SupportsTableFunction` перечисляют `numbers`/`numbers_mt`, но **не** `zeros`/`zeros_mt` (TF2/TF3 для `numbers` уже дописаны — здесь тот же пробел по новому семейству) | Дополнить `zeros`/`zeros_mt` во всех трёх перечнях |
| Z3 | P2 | `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:555-579` | `TableFunction_Zeros_ShouldEmitCall`/`_ZerosMt_` проверяют только `Contain("from zeros(@count)")`, что пройдёт и при появлении лишней обёртки; свойство «`zeros` не нуждается в `WrapTableFunction`» (суть решения) не закреплено негативно (аналог TF5; у `numbers_mt` ассерт тоже по-прежнему слабый) | Добавить dialect-assert `WrapTableFunction("zeros", call) == call` и `WrapTableFunction("zeros_mt", call) == call`; при желании усилить SQL-gen до полного `from zeros(@count) as t1` |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** `zeros`/`zeros_mt` — сознательное SQL-зеркало (реестр §3, как `numbers`/`uniq`/`quantile`); `IZerosRow` + `Value` точно повторяют `INumbersRow` + `Value`, а также `IGenerateSeriesRow`/`IUnnestRow<T>`/`IStringSplitRow`/`IOpenJsonRow`. BCL-конфликтов нет.
- **`byte Value` против `long Value` — не несогласованность.** `IZerosRow.Value` — `byte`, потому что колонка ClickHouse `zero` имеет тип `UInt8`; `INumbersRow.Value` — `long`, потому что `number` — `UInt64` и обёртка кастует к `Int64`. Оба факта задокументированы в XML и в `docs/guide/13-table-valued-functions.md` (EN+RU).
- **Материализация без обёртки подтверждена.** `WrapTableFunction` оборачивает только `numbers`/`numbers_mt` (`ClickHouseDialect.cs:191-194`); `MakeTypeName` отображает `typeof(byte) → "UInt8"` (`:364`), чтение идёт через `GetByte` (`SelectExpression.cs`), поэтому `zeros` материализуется напрямую — интеграционный тест на реальном ClickHouse зелёный.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **96/96**, `tests/nextorm.core.tests` — **157/157**, `tests/nextorm.postgres.tests` — **171/171**; `grep -rn "WrapTableFunction" tests/` — пусто (подтверждает Z3); `find -name 'PublicAPI*.txt'` — пусто (подтверждает Z1); EN+RU `docs/advanced/api-reference.md`, `docs/providers/clickhouse.md`, `docs/guide/13-table-valued-functions.md` и `docs/specs/roadmap/{todo_clickhouse.md,WIP_clickhouse_table_functions_remaining.md,sql-capabilities-gap-analysis.md}` содержат `zeros`/`zeros_mt` (синхронны).

### ClickHouse счётчики `count`/`count_big`/`count_distinct`/`count_big_distinct`/`count_if`/`count_over` (точечный аудит 19.09.2026)

Область: `DataContext/Dialect/ISqlDialect.cs` (`MakeCount` `:662-666`, `WrapsCountResult` `:667-672`, `WrapCount` `:673-680`), `SqlDialectBase.cs` (`:407-413`), `ClickHouseDialect.cs` (`:150-158`), `Visitors/{NormSqlTranslator,AdvancedAggregateTranslator,WindowFunctionTranslator}.cs`, тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:315-347`, `tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs:74-80`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:18-42,338-344`. XML-`<summary>` есть у обоих новых членов `ISqlDialect` и базовых реализаций; новых публичных типов нет → Приложение A (45) без изменений; Шаг 5 по-прежнему открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Build Release — **0/0**; unit: clickhouse **94/94**, core **157/157**; контейнерная интеграция ClickHouse — **3/3**.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| CNT1 | P2 | `ISqlDialect.cs:672,680`; `SqlDialectBase.cs:411,413`; `ClickHouseDialect.cs:155,157` | Новые члены публичного интерфейса (абстрактные — source-breaking для внешних реализаторов `ISqlDialect`; в репозитории реализует только `SqlDialectBase`). Шаг 5 открыт | Внести `ISqlDialect.WrapsCountResult`/`WrapCount` и их оверрайды в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/A3/D1/TF1) |
| CNT2 | P2 | `docs/guide/04-grouping-and-aggregates.md:503`; `docs/ru/guide/04-grouping-and-aggregates.md:504` | Таблица «Provider differences» утверждает, что ClickHouse для `count_big` «emits `count(*)` (already 64-bit)». После изменения неверно: `count_big()` → `toInt64(count(*))`, `count()` → `toInt32(count(*))`. EN и RU синхронны между собой, но обе устарели | Обновить строку ClickHouse в обеих ветках; дополнить абзац `:273-275` (RU `:275-278`), который перечисляет SQL Server/SQLite/PostgreSQL без ClickHouse |
| CNT3 | P2 | `docs/providers/clickhouse.md:31-33`; `docs/ru/providers/clickhouse.md:31-34` | Перечень агрегатов описывает `toInt64(...)` для `uniq*`, но не упоминает приведение `count`/`count_big`/`count_distinct`/`count_big_distinct` (и `count_if`) к `Int32`/`Int64` | Добавить обёртку счётчиков в обе ветки |
| CNT4 | P2 | `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:315-332`; `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:24-42` | `count_big_distinct` — единственный из шести вариантов без unit/integration-ассерта (`rg count_big_distinct tests/` — 0), хотя назван в контракте изменения | Добавить `BigD = SqlFunctions.Sql.count_big_distinct(x.Int)` и `Contain("toInt64(count(distinct nullableint))")`; по желанию — строку в integration-тест |
| CNT5 | P2 | `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:26-27` | Комментарий говорит «the dialect casts them to Int64», но `count()`/`count_distinct()`/`count_if()` кастятся в `Int32` (`toInt64` — только у `*_big`) | Уточнить: Int32 для int-вариантов, Int64 для `count_big*` |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** `WrapCount(string, bool)` повторяет уже принятый в реестре `WrapTableFunction` (верб `Wrap` — сознательная девиация от `Make*`-семейства, см. TF-аудит `:846`). `WrapsCountResult` — семантический предикат («результат требует обёртки»), как `ISqlDialect.RequireSubqueryAlias`/`EnforcesScalarSubqueryCardinality`, а не `Supports*`-capability; флаг управляет ветвлением/аллокацией, а не гейтит поддержку фичи. Строгая альтернатива `SupportsCountWrapping` — менее точна. Переименование не требуется.
- **`MakeCount` + `WrapCount` против `MakeUniqAggregate`.** Для `uniq*` обёртка целиком живёт в `Make*`-хуке (`ClickHouseDialect.cs:151-152`); для count — пост-хук, потому что аргументы рендерятся между `MakeCount(...)` и `)`. Асимметрия осознанная и оправдана — иначе `MakeCount` не смог бы получить уже отрендеренные аргументы.
- **`WrapsCountResult` — не первый `bool`-член `ISqlDialect` без префикса `Supports`.** `RequireSubqueryAlias` и `EnforcesScalarSubqueryCardinality` — того же класса (поведение, не capability); расширение не ухудшает однородность.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; clickhouse unit **94/94**, core **157/157**; ClickHouse integration **3/3**; `find -name 'PublicAPI*.txt'` — пусто (подтверждает CNT1); в `docs/` (кроме этого реестра) имён `WrapsCountResult`/`WrapCount` нет, поэтому CNT2/CNT3 — единственные doc-пробелы изменения.

### Точечный аудит 2 — cross-provider conditional/any_agg/percent_rank (19.09.2026)

Публичная поверхность аддитивна плюс одна замена флага; переименований имён членов нет. Новые/изменённые
члены:

- `CommonFunctions.iif<TResult>(bool, TResult?, TResult?)` → `TResult?` — условная функция перенесена с
  `SqlServerFunctions` на кросс-провайдерный `SqlFunctions.Sql` (`Query/SqlFunctions.cs:254`; XML-`<summary>`
  есть).
- `CommonFunctions.nth_value<T>(T?, int)` → `WindowFunction<T?>` — оконная функция
  (`Query/SqlFunctions.cs:440`; XML-`<summary>` есть); поддержана PostgreSQL/MySQL/MariaDB/SQLite/ClickHouse,
  SQL Server гейтит.
- `ISqlDialect.SupportsIif` (`ISqlDialect.cs:373`, база `false` — `SqlDialectBase.cs:106`) +
  `MakeIif(string, string, string)` (`ISqlDialect.cs:610`, база — `SqlDialectBase.cs:331`);
  нативные формы `iif`/`if`/`case when`.
- `ISqlDialect.SupportsChoose` (`ISqlDialect.cs:379`, база `false` — `SqlDialectBase.cs:108`) — `choose`
  остаётся SQL Server-only.
- `ISqlDialect.SupportsNthValue` (`ISqlDialect.cs:160`, база `false` — `SqlDialectBase.cs:36`) — гейт
  `nth_value`.
- `MariaDbDialect.SupportsAnyValueAggregate => false` (`MariaDbDialect.cs:24`) — MariaDB больше не наследует
  MySQL-включение (`ANY_VALUE` нет до 13.2, MDEV-10426).
- `ClickHouseDialect.SupportsPercentRankCumeDist => true` (`ClickHouseDialect.cs:64`) — прежнее утверждение,
  что ClickHouse не умеет `percent_rank`/`cume_dist`, было неверным (ошибка возникает при вызове без `OVER`).
- `ClickHouseDialect.SupportsNthValue => true` (`ClickHouseDialect.cs:67`).

**`SupportsIifChoose` разделён.** Прежний объединённый флаг удалён и заменён парой
`SupportsIif`/`SupportsChoose`: `iif` стал кросс-провайдерным, `choose` остался SQL Server-only.
Build Release — **0/0**.

**Пробелы документации EN/RU.** Правки затронули `docs/providers/*.md`,
`docs/guide/11-scalar-functions.md`, `docs/guide/10-window-functions.md`,
`docs/guide/04-grouping-and-aggregates.md`, `docs/advanced/api-reference.md` и их RU-зеркала;
`todo_mssql.md`, `todo_postgres.md`, `todo_clickhouse.md` и `sql-capabilities-gap-analysis.md`
синхронизированы. Новых публичных типов нет → Приложение A (45) без изменений.

**Шаг 5 (заморозка) открыт:** `PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён. При заморозке
внести `CommonFunctions.iif<TResult>`, `CommonFunctions.nth_value<T>`, `ISqlDialect.SupportsIif`,
`SupportsChoose`, `SupportsNthValue`, `MakeIif`, `MariaDbDialect.SupportsAnyValueAggregate`,
`ClickHouseDialect.SupportsPercentRankCumeDist` и `ClickHouseDialect.SupportsNthValue`
(ср. C/U4/Q3/J1/A3/D1/PG1/I8/WT1/LIM1/FM5/CNT1).

### Повторная верификация cross-provider `iif`/`choose`/`nth_value` (19.09.2026)

Независимый повторный проход по изменению, уже зарегистрированному выше как «Точечный аудит 2 —
cross-provider conditional/any_agg/percent_rank (19.09.2026)». Подтверждает выводы, **исправляет
устаревшие номера строк** того раздела (`CommonFunctions.iif` — `SqlFunctions.cs:267`, а не `:254`;
`nth_value` — `:453`, а не `:440`; `ISqlDialect.MakeIif` — `:614`, а не `:610`) и фиксирует
пробелы, которых тот проход не отметил.

Область: `Query/SqlFunctions.cs` (`CommonFunctions.iif` `:267`, `nth_value` `:453`),
`Query/SqlFunctions.SqlServer.cs` (`choose` `:67`; `iif` больше не объявлен),
`DataContext/Dialect/ISqlDialect.cs` (`SupportsPercentRankCumeDist` `:154`, `SupportsNthValue`
`:160`, `SupportsIif` `:377`, `SupportsChoose` `:383`, `MakeIif` `:614`) и `SqlDialectBase.cs`
(`:35`, `:36`, `:106`, `:108`, `:331`), `Visitors/BuiltinFunctionTranslator.cs` (`EmitIif`
`:114-132`, `EmitChoose` `:135-141`), `Visitors/WindowFunctionTranslator.cs` (`nth_value`-гейт
`:50-51`), `Visitors/WindowSql.cs` (`:23`), шесть диалектов. Build Release — **0/0** (прогнано
заново); `PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён.

**Имена и аннотации — P0/P1 нет.** `iif`/`choose`/`nth_value` — нижний регистр как SQL-зеркало
(реестр §3, как `nullif`/`greatest`/`any_agg`), BCL-конфликтов нет. Nullable-аннотации в русле DSL:
у неограниченного `T` запись `T?` — аннотация, не `Nullable<T>` (для `int` метаданные дают `int`,
для `string` — `string?`), поэтому `iif(cond, 1, 0)` остаётся non-null `int`;
`nth_value<T>(T?) -> WindowFunction<T?>` согласован с `lag`/`lead`/`first_value`/`last_value`.
`SupportsIif`/`SupportsChoose`/`SupportsNthValue`/`MakeIif` — PascalCase по схеме `Supports*`/`Make*`.

**Разделение флага конвенциям не противоречит, но это замена, а не расширение.** `SupportsIifChoose`
удалён из `ISqlDialect`, вместо него `SupportsIif`/`SupportsChoose` + хук `MakeIif`; в терминах
extend-only это breaking (RS0017+RS0016), допустимый только alpha-политикой §1 (`PublicAPI.*.txt`
нет, поверхность не заморожена). Разделение оправдано: `iif` стал кросс-провайдерным (`SupportsIif`
истинно у всех 5 SQL-диалектов, `MakeIif` переопределён у каждого), `choose` остался SQL Server-only;
прежняя аргументация «дробить незачем» устарела вместе с переносом `iif`. Переезд `iif`
`SqlServerFunctions`→`CommonFunctions` source-совместим (наследование сохраняет
`SqlFunctions.SqlServer.iif`), binary-breaking для скомпилированных ссылок.

**Статус ранее зарегистрированных находок (раздел «SQL Server условные `IIF`/`CHOOSE`», W-раздел):**

| # | Статус | Комментарий |
|---|--------|-------------|
| I1 | ✅ закрыта | `SqlFunctions.SqlServer.cs:6-11` — один `<summary>` |
| I2 | ✅ закрыта | `BuiltinFunctionTranslator.cs` — отдельные `<summary>` у `EmitIif` (`:113`), `EmitChoose` (`:134`), `EmitDateTrunc` (`:156`) |
| I3 | ✅ закрыта | `SqlFunctions.cs:32-39` — `choose` и перенос `iif` на `CommonFunctions` упомянуты |
| I4 | ✅ закрыта | `docs/ru/advanced/api-reference.md:60` — RU-строка `SqlServer` с `choose` появилась |
| I5 | ✅ закрыта | `docs/guide/11-scalar-functions.md:556,561-563`; RU `:564,570-572` |
| I6 | 🟡 частично | Закрыто: `choose(99, …)` → `null` (`tests/nextorm.integration.tests/SqlServerSpecificTests.cs:30-37`) и полный ассерт `choose(1, 'a', 'b', 'c')` (`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:48`). Осталось: `iif` проверяется разбитыми `Contain("iif(")`+`Contain("'yes', 'no')")` (`:46-47`), guard на захваченный массив не добавлен |
| I7 | ✅ закрыта | `docs/specs/roadmap/WIP_iif_choose.md:47-49` — `Iif_ShouldUseNativeForm`; SQLite-тест есть (`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1423-1425`) |
| I8 | ♻️ заменена | `SupportsIifChoose` удалён; актуальный список заморозки — IF6 ниже |
| W1 | ✅ закрыта | `docs/guide/10-window-functions.md:13` (+RU) — `double` для `percent_rank`/`cume_dist` |
| W3 | ✅ закрыта | обзор снова ссылается на `` `SqlFunctions.Sql` `` |
| W4 | ✅ закрыта | `tests/nextorm.integration.tests/CommonTestSuite.Window.cs:54`; `ClickHouseDialect.SupportsPercentRankCumeDist => true` (`:64`) |
| W5 | ✅ закрыта | `docs/specs/roadmap/todo_mssql.md:175` `[x]`; `todo_postgres.md:90` `[x]` |
| W2 | ⏸ не проверялась | `Source:`-футер `guide/10` — вне области этого прохода |

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| IF1 | P2 | `MySqlDialect.cs:34`, `PostgresDialect.cs:61`, `SqliteDialect.cs:46`, `ClickHouseDialect.cs:30` | Четыре новых `public override string MakeIif(...)` без XML-`<summary>` (у `SqlServerDialect.cs:336` есть). `NoWarn=CS1591` во всех 7 библиотечных `.csproj` скрывает пропуск от сборки и NuGet-потребителя | Добавить `<summary>` по образцу `SupportsIif`; закрыть при Шаге 5 (ср. N2/U5/Q4) |
| IF2 | P2 | `MySqlDialect.cs:38,40`, `PostgresDialect.cs:65,67`, `SqliteDialect.cs:50,53` | Шесть затронутых `public override`-флагов (`SupportsPercentRankCumeDist` ×3, `SupportsNthValue` ×3) задокументированы строчными `//`-комментариями, а не XML-`<summary>` (`MySqlDialect.SupportsNthValue` — вообще без доки); CS1591 скрыт | Заменить/добавить `<summary>` при закрытии Шага 5 (ср. IF1/N2) |
| IF3 | P2 | `SqlDialectBase.cs:6-11` vs `:331-332` | Классовый док обещает «a dialect can never inherit a placeholder that throws at runtime», но новый `MakeIif` — throwing-заглушка (как прежние `MakeSessionInfoFunction` `:58`, `MakeUuidGenerator` `:71`, `MakeStringReverse` `:309`); инвариант неверен | Исправить док (оговорить хуки, гейтированные `Supports*`), либо перенести пояснение в `<remarks>` |
| IF4 | P2 | `Visitors/BuiltinFunctionTranslator.cs:5-17` | Классовый `<summary>` перечисляет только `nullif`, `greatest`/`least`, `date_trunc`, `string_agg`/`array_agg`; `iif`/`choose` (и `date_add`/`date_diff`/`end_of_month`/`date_from_parts`/`contains`/`freetext`) не упомянуты — кумулятивно устарело (ср. D3/U3/GLI1) | Дополнить перечень, в первую очередь `iif`/`choose` |
| IF5 | ✅ закрыто (20.09.2026) | `ISqlDialect.cs:625`; `SqlDialectBase.cs:352` | `MakeIif` абстрактен в `ISqlDialect`, но база даёт throwing-реализацию: внешний реализатор интерфейса обязан его реализовать (source-breaking), а наследник базы с `SupportsIif => true` без override упадёт в рантайме | **Реализовано (вариант A):** `ISqlDialect.MakeIif` стал default interface method с throwing-телом (source-breaking для внешних реализаторов снят); `SqlDialectBase.MakeIif` оставлен `virtual` — 6 диалектов по-прежнему `override`; добавлен контрактный тест `tests/nextorm.integration.tests/DialectCapabilityContractTests.cs` (обобщён на `SupportsIif`/`SupportsSessionInfoFunctions`/`SupportsUuidGenerators`/`SupportsLimitBy` ⇒ соответствующий `Make*` переопределён) |
| IF6 | P2 | проект целиком | **Шаг 5 (заморозка) открыт:** `PublicAPI.Shipped/Unshipped.txt` нет, `PublicApiAnalyzers` не подключён, `ApiCompat`/API-approval-теста нет | Внести перечисленное ниже в `PublicAPI.Unshipped.txt` при заморозке |

**Список заморозки (IF6).** Новые члены: `CommonFunctions.iif<TResult>(bool, TResult?, TResult?) -> TResult?`,
`CommonFunctions.nth_value<T>(T?, int) -> WindowFunction<T?>`, `ISqlDialect.SupportsIif.get -> bool`,
`ISqlDialect.SupportsChoose.get -> bool`, `ISqlDialect.SupportsNthValue.get -> bool`,
`ISqlDialect.SupportsPercentRankCumeDist.get -> bool`,
`ISqlDialect.MakeIif(string, string, string) -> string`, плюс `public override`-члены в `SqlDialectBase`
и шести диалектах (`SupportsIif`, `MakeIif`, `SupportsChoose`, `SupportsNthValue`,
`SupportsPercentRankCumeDist`, `MariaDbDialect.SupportsAnyValueAggregate`) — по принятой в реестре
конвенции (ср. U5/Q4/N2). Удаления (RS0017): `ISqlDialect.SupportsIifChoose` и
`SqlServerFunctions.iif`; для `iif` PublicApiAnalyzers даст пару RS0017 (производный тип) + RS0016
(базовый тип) — как при промоушене session/UUID-функций (ср. S1/UG2).

**Статус фиксов P2 (20.09.2026, внесены в том же изменении):** IF1–IF4 устранены — XML-`<summary>`
добавлены к `MakeIif` во всех четырёх провайдерских диалектах и к шести флагам
`SupportsPercentRankCumeDist`/`SupportsNthValue`; классовый док `SqlDialectBase` переформулирован
(хуки, гейтированные `Supports*`, могут бросать в базе); `<summary>` `BuiltinFunctionTranslator`
дополнен `iif`/`choose`, date-хелперами и full-text. IF5 закрыт (DIM + контрактный тест). IF6
остаётся открытым (Шаг 5 — заморозка `PublicAPI.*.txt`).

**Общий вывод по «`Supports*` ⇒ не реализовано»:** это структурная проблема не только `iif`.
Диалект может объявить флаг `true` без рабочей реализации только там, где база **бросает**:
`MakeIif`, `MakeSessionInfoFunction`, `MakeUuidGenerator`, `MakeLimitBy` (плюс `protected`
`MakeStringPosition`/`MakeStringReverse`). Флаги с рабочим ANSI-дефолтом (`SupportsGreatestLeast`,
`SupportsDateTrunc`, `SupportsDateArithmetic`) безопасны. Контрактный тест покрывает бросающие пары;
стратегическое решение — «capability object» (`IifRenderer? Iif { get; }`, где наличие рендерера и
есть флаг); план вынесен в `docs/specs/todo_dialect_capabilities.md`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0/0** (прогнано заново);
`find -name 'PublicAPI*.txt'` — пусто; `rg "SupportsIifChoose|EmitIifChoose" src` — 0;
`rg "SqlServerFunctions.iif" src` — 0.

### ClickHouse distributed `GLOBAL IN` (точечный аудит 20.09.2026)

Область: `ClickHouseFunctions.global_in<T>` ×3 (`Query/SqlFunctions.ClickHouse.cs:146-158`),
`ISqlDialect.SupportsGlobalPredicates` (`DataContext/Dialect/ISqlDialect.cs:687-691`, база `false` —
`SqlDialectBase.cs:417-418`), `ClickHouseDialect.SupportsGlobalPredicates => true`
(`src/nextorm.clickhouse/ClickHouseDialect.cs:196-197`), трансляторы
`Visitors/{NormSqlTranslator.cs:160-222,InValuesTranslator.cs:36,86,CorrelatedQueryExpressionVisitor.cs:116-117,184-186}`;
тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:581-609`, `ClickHouseDialectTests.cs:239`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1470-1483`,
`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:189-211`. Build Release — **0/0**;
unit: clickhouse **99/99**, postgres **172/172**. XML-`<summary>` есть у всех трёх перегрузок
`global_in` и у `ISqlDialect.SupportsGlobalPredicates`; новых публичных типов нет → Приложение A (45)
без изменений.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| GLI1 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-14`; `Query/SqlFunctions.cs:41-51` | Кумулятивный док-пробел (ср. Z2): классовая `<summary>` `ClickHouseFunctions` и `<summary>` свойства `SqlFunctions.ClickHouse` перечисляют семейства, но **не** `global_in`, хотя `docs/advanced/api-reference.md` EN+RU (`:62`) его уже упоминают | Дополнить оба перечня `global_in` |
| GLI2 | P2 | `Query/SqlFunctions.ClickHouse.cs:152-158`; `DataContext/Dialect/ISqlDialect.cs:691` | Новые публичные члены (`global_in` ×3, `SupportsGlobalPredicates`) не отслеживаются: `PublicAPI.Shipped/Unshipped.txt` отсутствуют, `PublicApiAnalyzers` не подключён. `SupportsGlobalPredicates` — абстрактный член публичного интерфейса (source-breaking для внешних реализаторов). Шаг 5 открыт | Внести оба члена в `PublicAPI.Unshipped.txt` при заморозке (ср. TF1/CNT1) |
| GLI3 | P2 | `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1470-1483`; `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:597-609` | Негативный тест покрывает только подзапросную форму; value-list `global_in` на неподдерживающем провайдере не проверена (гейт `NormSqlTranslator.cs:217` не закреплён). Value-list тест использует захваченный `List<int>`, т.е. идёт по некэшируемому пути (код-смелл Находка 14) и не ассертит переиспользование плана | Добавить `GlobalIn_Values_UnsupportedByProvider_ShouldThrow`; после фикса Находки 14 — тест переиспользования плана по образцу `InListCacheTests` (`tests/nextorm.sqlite.tests`) |

ℹ️ **Наблюдения (фикс не требуется):**

- **Именование — конвенции соблюдены, P0/P1 нет.** `global_in` — сознательное SQL-зеркало
  (`global_in` ≈ `GLOBAL IN`, как `@in`, `count_big`, `any_agg`); это не новое нарушение, а
  зафиксированное решение `API-NAMING-REVIEW.md` §3 «Отмечено, но менять не рекомендуется».
  Тип возврата `bool`, параметры `column`/`cmd`/`values`, формы перегрузок полностью повторяют
  `CommonFunctions.@in` (`SqlFunctions.cs:248-252`) — вопрос 3 закрыт положительно. BCL-конфликтов нет.
- **`SupportsGlobalPredicates` — именование согласовано.** Префикс `Supports*` + плюрал `Predicates`
  (флаг задуман под `GLOBAL IN` и будущий `GLOBAL JOIN`, `WIP_global_in.md:69-75`) — шире текущей
  реализации, но не вводит в заблуждение. `SqlDialectBase` даёт безопасный дефолт `false`.
- **Размещение члена в `ISqlDialect`.** `SupportsGlobalPredicates` объявлен между
  `MakeSubqueryPredicate` и `MakePage`, а не в начале с остальными `Supports*`-флагами; косметика, на
  именование не влияет.
- **Уровень `P2`, не `P1`.** Новые имена не конфликтуют с BCL и не нарушают конвенций типов;
  GLI1–GLI3 — это док-пробел, отсутствие трекинга поверхности и пробел тестов, а не переименования.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; clickhouse unit
**99/99**, postgres **172/172**; `find -name 'PublicAPI*.txt'` — пусто (подтверждает GLI2);
`rg "global_in|SupportsGlobalPredicates" docs/` — `docs/providers/clickhouse.md` (+RU),
`docs/advanced/api-reference.md` (+RU) и roadmap-спеки синхронны; `grep -n "global_in" src/nextorm.core/Query/SqlFunctions.ClickHouse.cs` —
3 перегрузки, `<summary>` у каждой; пары EN/RU (`docs/providers/clickhouse.md:68-70`,
`docs/ru/providers/clickhouse.md:67-69`, `docs/advanced/api-reference.md:62`,
`docs/ru/advanced/api-reference.md:62`) совпадают по именам.

### ClickHouse join strictness `ANY`/`ALL`/`ASOF` (точечный аудит 20.09.2026)

Публичная поверхность аддитивна, переименований нет; правка `docs/**`+`docs/ru/**` по именам не
требуется (прозы обновлены автором фичи — `WIP_clickhouse_join_strictness.md:81-84`). Новые члены:

- `JoinStrictness { Default, Any, All, Asof }` — `Expressions/JoinExpression.cs:30-40` (XML-`<summary>`
  у типа и каждого члена);
- `JoinExpression.Strictness` — `:51` (XML-`<summary>` есть; сеттер **`internal set`** с 20.09.2026 —
  сужен по Находке 16, публичная мутация план-ключа устранена; сам член по-прежнему войдёт в
  `PublicAPI.Unshipped.txt` как `.get`-свойство);
- `EntityBuilder<TEntity>.WithStrictness(JoinStrictness)` — `Builders/EntityBuilder.cs:280`
  (XML-`<summary>`+`<exception>`), плюс 7 ковариантных `new`-перегрузок
  `JoinedEntityBuilder<T1..T8>.WithStrictness` — `Builders/Joins/JoinedEntityBuilder.cs:60,111,157,203,249,295,329`
  (все с `<inheritdoc/>`);
- `ISqlDialect.SupportsJoinStrictness` — `DataContext/Dialect/ISqlDialect.cs:59` (база `false` —
  `SqlDialectBase.cs:26`) и `ISqlDialect.MakeJoinKeyword(JoinType, JoinStrictness)` — `:65`
  (база — ANSI-форма + `NotSupportedException` на non-default — `SqlDialectBase.cs:191-204`);
  оверрайды ClickHouse — `src/nextorm.clickhouse/ClickHouseDialect.cs:25,31-54` (оба с XML-`<summary>`).

**CS1591/XML-doc (вопрос 4).** XML-комментарий присутствует у всех новых публичных членов и их
оверрайдов, поэтому Приложение A (45) не меняется — новых недокументированных публичных типов нет.
Именование — конвенции соблюдены, P0/P1 нет: `JoinStrictness` повторяет официальный термин ClickHouse
(`join_strictness`), `Asof` — PascalCase (не `ASOF`), как `Http`/`Json`; конфликтов с BCL нет;
`WithStrictness` согласован с уже существующим `WithTotals` (`EntityBuilder.cs:595`), `MakeJoinKeyword` —
с `MakeApply`/`MakeFinal`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| JS1 | P2 | `JoinExpression.cs:51`; `ISqlDialect.cs:59,65`; `EntityBuilder.cs:280`; `JoinedEntityBuilder.cs:60,111,157,203,249,295,329`; `PublicAPI.*.txt` отсутствуют | Новые публичные члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). «`JoinExpression.Strictness` — публичный **сеттер**, пишущий в план-ключ» — **исправлено 20.09.2026**: `Strictness`/`IsGlobal` сужены до `internal set` (Находка 16 закрыта) | При заморозке внести члены в `PublicAPI.Unshipped.txt` (актуальная арность `MakeJoinKeyword` и новые члены — см. GG1/GG2); сужение сеттера — сделано |
| JS2 | P2 | `ISqlDialect.cs:59,65` vs `ISqlDialect.cs:626-632` (`MakeIif` DIM) | Оба новых члена интерфейса абстрактные — source-breaking для внешних реализаторов `ISqlDialect`, тогда как соседний `MakeIif` намеренно сделан default interface method «so that existing external `ISqlDialect` implementations … keep compiling»; в одном контракте разная политика совместимости | Alpha-политика допускает разрыв — зафиксировать решение явно (трекинг JS2); throwing-DIM отложил бы разрыв на runtime для `Default`-join'ов, поэтому предпочтительнее оставить abstract и записать обоснование |

ℹ️ **Наблюдения (фикс не требуется).**

- **Именование — P0/P1 нет.** `JoinStrictness`/`Asof`/`WithStrictness`/`SupportsJoinStrictness`/
  `MakeJoinKeyword` не конфликтуют с BCL и следуют суффиксным конвенциям (`*Strictness` — термин
  ClickHouse; `Supports*`, `Make*`, `With*` — как у соседних модификаторов). Вопрос 4 закрыт
  положительно.
- **Дублирования публичной поверхности нет.** Отдельный флаг `SupportsJoinStrictness` оправдан: это
  ClickHouse-only capability, и он не совпадает ни с одним существующим флагом (в отличие от снятого
  `SupportsJsonPath` — JP1). 7 `new`-перегрузок — вынужденная ковариантность, не дубли контракта
  (code-smells ℹ️, вопрос 2).
- **Двойная валидация не про нейминг (вопрос 3).** Гейт рендера + `throw` базового хука — то же
  разделение «флаг/эмиттер», что у `SupportsFinal`/`MakeFinal`; на именование не влияет.
- **`MakeIif`-DIM — единственное отклонение** в `ISqlDialect`; см. JS2. Остальные недавние члены
  (`SupportsDictionaries`/`MakeDictionaryFunction`, `SupportsGlobalPredicates`) также абстрактные, так
  что `WithStrictness`-фича следует доминирующей практике, а не ломает её.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `find -name 'PublicAPI*.txt'`
— пусто (подтверждает JS1); XML-`<summary>` у `JoinStrictness`, `JoinExpression.Strictness`,
`ISqlDialect.SupportsJoinStrictness`, `ISqlDialect.MakeJoinKeyword` и обоих оверрайдов ClickHouse
присутствуют; Приложение A (45) без изменений; `grep -l "WithStrictness\|JoinStrictness" docs` —
`docs/providers/clickhouse.md` (+RU), `docs/advanced/api-reference.md` (+RU),
`docs/specs/roadmap/WIP_clickhouse_join_strictness.md` — имена EN/RU синхронны.

### ClickHouse GLOBAL JOIN (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет, `docs/**`+`docs/ru/**` обновлены автором
(`docs/providers/clickhouse.md:80-82` + RU, `docs/advanced/api-reference.md` + RU). Новые/изменённые члены:

- `JoinExpression.IsGlobal` — `Expressions/JoinExpression.cs:52-58` (`public bool { get; internal set; }`,
  XML-`<summary>` есть; `CloneForCache` — `:72`, план-ключ — `JoinExpressionPlanEqualityComparer.cs:36,58`);
- `ISqlDialect.SupportsGlobalJoin` — `DataContext/Dialect/ISqlDialect.cs:60-64` (default `false` —
  `SqlDialectBase.cs:27`; override ClickHouse — `ClickHouseDialect.cs:27-28`);
- `ISqlDialect.MakeJoinKeyword` **расширен** до `(JoinType, JoinStrictness, bool isGlobal)` —
  `ISqlDialect.cs:65-71` (база — `SqlDialectBase.cs:190-210`; ClickHouse — `ClickHouseDialect.cs:30-57`,
  рендер `" global[ type][ any|all|asof] join "`);
- `EntityBuilder<TEntity>.Global()` — `Builders/EntityBuilder.cs:285-293` (XML-`<summary>`+`<exception>`),
  плюс 7 ковариантных `new Global()` на `JoinedEntityBuilder<T1..T8>` —
  `Builders/Joins/JoinedEntityBuilder.cs:60,113,159,205,251,297,331` (все с `<inheritdoc/>`).

**CS1591/XML-doc.** XML-`<summary>` присутствует у всех новых/изменённых публичных членов и их оверрайдов
(`IsGlobal`, `SupportsGlobalJoin` интерфейс+база+ClickHouse, базовая и ClickHouse-реализации
`MakeJoinKeyword`, базовая `Global()`); 7 ковариантных перегрузок — `<inheritdoc/>`. Новых публичных
типов нет → Приложение A (45) без изменений. Именование — конвенции соблюдены, P0/P1 нет: `Is*` для bool,
`Supports*` — как `SupportsGlobalPredicates`/`SupportsJoinStrictness`, `Global()` — терсный модификатор
рядом с `Distinct()`/`Final()`/`Sample()`. Различение `SupportsGlobalPredicates` (GLOBAL IN) и
`SupportsGlobalJoin` (GLOBAL JOIN) однозначно.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| GG1 | P2 | `JoinExpression.cs:58`; `ISqlDialect.cs:64,71`; `SqlDialectBase.cs:27,192`; `ClickHouseDialect.cs:28,34`; `EntityBuilder.cs:293`; `JoinedEntityBuilder.cs:60,113,159,205,251,297,331`; `PublicAPI.*.txt` отсутствуют | Новые/изменённые публичные члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). `SupportsGlobalJoin` и `MakeJoinKeyword` — абстрактные члены публичного интерфейса: source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`). `MakeJoinKeyword` меняет **арность** существующего члена — пара RS0017+RS0016 при заморозке | При заморозке внести: `JoinExpression.IsGlobal.get`, `ISqlDialect.SupportsGlobalJoin.get`, `ISqlDialect.MakeJoinKeyword(JoinType, JoinStrictness, bool) -> string`, `SqlDialectBase`/`ClickHouseDialect`-оверрайды и `EntityBuilder<TEntity>.Global()` + 7 `new`-перегрузок. Зафиксировать alpha-разрыв явно (продолжение политики JS2); альтернатива для flag-членов — default interface property `SupportsGlobalJoin => false` (сняла бы source-разрыв), но отклонена ради единообразия с доминирующей парой `abstract`+`SqlDialectBase.virtual` |
| GG2 | P2 | `ISqlDialect.cs:71` | Расширение сигнатуры `MakeJoinKeyword` (а не отдельный `MakeGlobalJoinKeyword`) — churn публичного интерфейса, добавленного предыдущей фичей | **Принято:** метод не выпущен (alpha), внешних реализаторов нет, оба модификатора рендерятся в одном keyword (`global left any join`), поэтому единый хук связнее; зафиксировать в списке заморозки GG1 (см. code-smells, вопрос 2) |

ℹ️ **Наблюдения (фикс не требуется).**

- **Композиция `Global()`/`WithStrictness(...)` в любом порядке — корректна** (`ReplaceLastJoin`
  переносит оба флага; см. code-smells, вопрос 1). Единственный пробел — тест обратного порядка
  (Находка 18), не нейминг.
- **`WithStrictness`/`Global` называют действие, а не «join»** — оба модификатора применяются к
  последнему join'у, имя сохраняет arity-агностичность; `Global()` не конфликтует с BCL.
- **Отдельный флаг `SupportsGlobalJoin` оправдан** — ClickHouse-only capability, не совпадает с
  `SupportsGlobalPredicates` (разные фичи) и не дублирует `SupportsJoinStrictness`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе);
`find -name 'PublicAPI*.txt'` — пусто (подтверждает GG1); XML-`<summary>` у `IsGlobal`,
`SupportsGlobalJoin`, `Global()` и `MakeJoinKeyword` (интерфейс/база/ClickHouse) присутствуют;
Приложение A (45) без изменений; `grep -l "SupportsGlobalJoin\|EntityBuilder.Global" docs` —
`docs/providers/clickhouse.md` (+RU), `docs/specs/roadmap/{todo_clickhouse.md,WIP_global_join.md,sql-capabilities-gap-analysis.md}` — имена EN/RU синхронны. Тесты в этом проходе не перезапускались (только build); ссылки — по `WIP_global_join.md:53-60`.

### ClickHouse массивы и `arrayJoin` (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет. Новые/изменённые члены:

- `ISqlDialect.SupportsArrayFunctions` — `DataContext/Dialect/ISqlDialect.cs:136-143` (abstract; default
  `false` — `SqlDialectBase.cs:33`; override ClickHouse — `src/nextorm.clickhouse/ClickHouseDialect.cs:30-35`);
- `ISqlDialect.SupportsArrayJoin` — `ISqlDialect.cs:144-149` (default `false` — `SqlDialectBase.cs:34`;
  override ClickHouse — `ClickHouseDialect.cs:36-38`);
- `ISqlDialect.MakeArrayFunction(string name, string call)` — `ISqlDialect.cs:682-689` (**default interface
  method**, identity); `SqlDialectBase.cs:421-423` (identity); override ClickHouse — `ClickHouseDialect.cs:40-45`
  (`length`/`indexOf` в `toInt64(...)`);
- `ClickHouseFunctions.array_join<T>(T[]) -> T`, `length<T>(T[]) -> long`, `has<T>(T[], T) -> bool`,
  `index_of<T>(T[], T) -> long`, `has_any<T>(T[], T[]) -> bool`, `has_all<T>(T[], T[]) -> bool`,
  `array_string_concat<T>(T[], string? = null) -> string`, `split_by_char(string?, string?) -> string[]`,
  `array_sort<T>(T[]) -> T[]`, `array_reverse<T>(T[]) -> T[]`, `array_distinct<T>(T[]) -> T[]` —
  `Query/SqlFunctions.ClickHouse.cs:215-253`.

**CS1591/XML-doc.** XML-`<summary>` присутствует у всех 11 новых методов, обоих флагов
(интерфейс+база+ClickHouse) и у трёх реализаций `MakeArrayFunction`; новых публичных типов нет →
Приложение A (45) без изменений. Именование — конвенции соблюдены (snake_case DSL — SQL-зеркало,
`Supports*`/`Make*` — как у соседних семейств), P0/P1 по именам нет. Отдельный CH-класс оправдан — см. ℹ️.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| AR1 | P2 | `ISqlDialect.cs:143,149,689`; `SqlDialectBase.cs:33,34,423`; `ClickHouseDialect.cs:35,38,44`; `SqlFunctions.ClickHouse.cs:215-253`; `PublicAPI.*.txt` отсутствуют | Новые члены публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). `SupportsArrayFunctions`/`SupportsArrayJoin` — абстрактные члены: source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`); `MakeArrayFunction` — DIM, разрыва не создаёт | При заморозке внести: два флага, `ISqlDialect.MakeArrayFunction(string, string) -> string`, override'ы `SqlDialectBase`/`ClickHouseDialect` и 11 методов `ClickHouseFunctions` (ср. A3/JS2/GG1/XP7) |
| AR2 | **P1** | `docs/providers/clickhouse.md:154`; `docs/ru/providers/clickhouse.md:154`; `docs/advanced/api-reference.md:62`; `docs/ru/advanced/api-reference.md:62`; `docs/guide/11-scalar-functions.md:399-400`; `docs/ru/guide/11-scalar-functions.md:406-407` | Документация противоречит реализованной поверхности и не обновлена в обеих языковых ветках (правило AGENTS.md «`docs/**` **и** `docs/ru/**`»): строка таблицы «Arrays / native JSON / extended scalars → not supported (PostgreSQL-only)» / «Массивы / нативный JSON / расширенные скаляры → не поддерживаются (только PostgreSQL)» прямо отрицает CH-массивы; описание `SqlFunctions.ClickHouse` в api-reference не перечисляет array-семейство; guide/11 содержит только `SqlFunctions.Postgres.array_*` | Добавить array-семейство (`array_join`/`length`/`has`/`index_of`/`has_any`/`has_all`/`array_string_concat`/`split_by_char`/`array_sort`/`array_reverse`/`array_distinct`) в описание `ClickHouse` (api-reference EN+RU), строку таблицы и guide/11 (EN+RU) — одним изменением |
| AR3 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-14`; `Query/SqlFunctions.cs:41-51`; `Visitors/ArraySqlTranslator.cs:5-18` | Классовые `<summary>` не перечисляют новое array-семейство (публичные `ClickHouseFunctions`/`SqlFunctions.ClickHouse`) и всё ещё описывают `ArraySqlTranslator` как PG-only (`SupportsArrays`/`CommonFunctions`); у array-возвращающих методов (`array_sort`/`array_reverse`/`array_distinct`/`split_by_char`) док не оговаривает «только вложенно» | Дополнить оба публичных `<summary>` перечнем array-функций и обновить summary `ArraySqlTranslator` (CH + `SupportsArrayFunctions`/`SupportsArrayJoin`); вернуть оговорку о вложенном использовании (ср. guide/11) |
| AR4 | P2 | `ISqlDialect.cs:689` vs `SqlDialectBase.cs:33,34,423` | `MakeArrayFunction` — DIM с identity-телом, тогда как два соседних новых флага абстрактны, а родственный `MakeJsonExtract` — abstract+base.virtual; в контракте смешаны две политики совместимости | **Принято:** identity — безопасный default для не-CH реализаторов, а CH всё равно override'ит; зафиксировать решение в списке заморозки (продолжение JS2) либо унифицировать политику флагов через default interface properties `=> false` |

ℹ️ **Наблюдения (фикс не требуется).**

- **Промоушен в `CommonFunctions` не нужен.** `ClickHouseFunctions` не наследует `PostgresFunctions`;
  нативные имена/семантика различаются (`length` vs `cardinality`, `has` vs `@>`, `indexOf` vs
  `array_position`, `arrayStringConcat` vs `array_to_string`, `arrayJoin` без PG-аналога), а CH-поверхность
  работает по array-колонкам, PG — по array-параметрам. Правило «≥2 провайдера выражают одно и то же →
  `CommonFunctions`» применимо только к идентичной C#-поверхности (как `any_agg`/session-info/uuid/iif), так
  что дублирование здесь осознанное.
- **`MakeArrayFunction`/флаги не могут быть `internal`.** `ISqlDialect` реализуется сборками-провайдерами
  (`nextorm.clickhouse`), поэтому члены обязаны оставаться публичными — та же причина, по которой публичны
  `Paging`/`ISqlDialect.MakePage`. Ограничение «только PostgreSQL/ClickHouse» выражается флагом, а не видимостью.
- **`array_join<T>(T[]) -> T` — корректная модель row-expansion.** Возврат элемента (а не `T[]`) согласован с
  ClickHouse `arrayJoin` и исключает попытку материализовать массив; array-возвращающие `array_sort`/
  `array_reverse`/`array_distinct`/`split_by_char` предназначены только для вложенного использования (см. AR3).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе);
`find -name 'PublicAPI*.txt'` — пусто (подтверждает AR1); XML-`<summary>` у 11 array-методов, обоих флагов
и `MakeArrayFunction` (интерфейс/база/ClickHouse) присутствуют; Приложение A (45) без изменений. Тесты в
этом проходе: clickhouse **122/122**, postgres **177/177** (0 failed); ссылки — по разделу
`code-smells-review.md`, «ClickHouse массивы и `arrayJoin`».

### ClickHouse `ARRAY JOIN` (клауза уровня `FROM`) (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет. Новые члены:

- `EntityBuilder<TEntity>.ArrayJoin<TArray>(Expression<Func<TEntity,TArray>>) -> EntityBuilder<TEntity>`
  (`Builders/EntityBuilder.cs:291-292`) и `LeftArrayJoin<TArray>(...)` (`:299-300`) — XML `<summary>` есть;
- 14 ковариантных `new`-перегрузок на `JoinedEntityBuilder<T1..T8>`
  (`Builders/Joins/JoinedEntityBuilder.cs:65-68,122-125,172-175,222-225,272-275,322-325,360-363`) —
  **без XML-доков** (AJ3);
- `public enum ArrayJoinKind { Inner = 0, Left = 1 }` (`Expressions/ArrayJoinKind.cs:8-13`) — XML `<summary>`
  есть у типа и обоих членов; имя повторяет ClickHouse (`ASTArrayJoin::Kind::Inner/Left`);
- `QueryCommand.ArrayJoinExpressions` (`Query/QueryCommand.cs:201`) и `ArrayJoinKind` (`:203`) — public;
  `QueryDefinition.ArrayJoins`/`ArrayJoinKind` (`:58-60`), `QueryCommand._arrayJoins`/`_preparedArrayJoin`
  (`:26-27`) — internal;
- `ISqlDialect.SupportsArrayJoinClause` (`DataContext/Dialect/ISqlDialect.cs:777-781`, abstract) +
  `ISqlDialect.MakeArrayJoin(ArrayJoinKind, IReadOnlyList<string>)` (`:782-787`, abstract);
  `SqlDialectBase` (`:153-161`, `virtual`/throw) и `ClickHouseDialect`
  (`src/nextorm.clickhouse/ClickHouseDialect.cs:270-278`, override) — XML `<summary>` есть у всех.

Новых публичных **типов** один — задокументированный `ArrayJoinKind` → Приложение A (45) без изменений.
Именование — конвенции соблюдены: `Supports*`/`Make*` как у соседних семейств, `ArrayJoinKind` не конфликтует
с BCL и не путается со скалярным `SupportsArrayJoin` (это флаг функции `arrayJoin`, `Clause` — флаг клаузы).
P0/P1 по именам нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| AJ1 | P2 | `ISqlDialect.cs:777-787`; `SqlDialectBase.cs:153-161`; `ClickHouseDialect.cs:270-278`; `EntityBuilder.cs:291-300`; `ArrayJoinKind.cs:8-13`; `PublicAPI.*.txt` отсутствуют | Новые члены публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). `SupportsArrayJoinClause`/`MakeArrayJoin` — абстрактные члены: source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | При заморозке внести: `ArrayJoinKind` (+`Inner`/`Left`), оба fluent-метода и 14 `new`-перегрузок, `ISqlDialect.SupportsArrayJoinClause`/`MakeArrayJoin(ArrayJoinKind, IReadOnlyList<string>)`, override'ы `SqlDialectBase`/`ClickHouseDialect`, `QueryCommand.ArrayJoinExpressions`/`ArrayJoinKind` (ср. AR1/GG1/JS1/FM5) |
| AJ2 | P2 | `Query/QueryCommand.cs:201` | `public IReadOnlyList<Expression>? ArrayJoinExpressions => _preparedArrayJoin;` раскрывает **подготовленную** форму (`Expression[]`) публично, тогда как аналогичное `PreparedPreWhere` (`:205`) — `internal`, а `PreWhere` (`:196`) публично отдаёт исходную лямбду. Конкретный тип — массив: `(Expression[])cmd.ArrayJoinExpressions` позволяет мутировать вход план-ключа (тот же класс, что Находка 16/5 в `code-smells-review.md`), плюс утечка внутренней поверхности. Потребители — только внутри `nextorm.core` (`SqlBuilder.cs:105`, `InMemoryQueryBuilder.cs:98`, `QueryPlanEqualityComparer.cs:71,313`) | Сузить до `internal` (или переименовать в `PreparedArrayJoin` и оставить `internal`); если нужен публичный доступ — отдавать `Array.AsReadOnly(...)`/исходные `ArrayJoins` |
| AJ3 | P2 | `Builders/Joins/JoinedEntityBuilder.cs:65-68,122-125,172-175,222-225,272-275,322-325,360-363` | 14 `new ArrayJoin`/`LeftArrayJoin` не имеют XML-доков, тогда как соседние `Global`/`WithStrictness` помечены `/// <inheritdoc/>`; `CS1591` в `<NoWarn>` (`nextorm.core.csproj:9`, `nextorm.clickhouse.csproj:9`, …), поэтому сборка 0/0 и пробел не виден | Добавить `/// <inheritdoc/>` к каждой перегрузке (док базового метода уже содержит оговорку «элемент не привязан к CLR-члену») |
| AJ4 | P2 | `Expressions/ArrayJoinKind.cs:8-13`; `Query/QueryCommand.cs:203` | У enum нет члена «нет клаузы»: `ArrayJoinKind` по умолчанию `Inner`, поэтому у команды без `ARRAY JOIN` свойство возвращает `Inner` и не отличимо от настоящей `Inner`-клаузы без чтения `ArrayJoinExpressions`. С выражениями всё корректно; вопрос — только о выразительности (alpha, поверхность не заморожена) | По желанию — `None = 0` (и `Inner = 1`, `Left = 2`) либо XML-оговорка «значим только при непустом `ArrayJoinExpressions`» |
| AJ5 | P2 | `docs/providers/clickhouse.md:154`; `docs/ru/providers/clickhouse.md:154`; `docs/guide/11-scalar-functions.md:423,438,447`; `docs/ru/guide/11-scalar-functions.md:431,446,456`; `docs/advanced/api-reference.md` | Публичная док-поверхность EN/RU описывает только скаляр `SqlFunctions.ClickHouse.array_join(...)`/`arrayJoin(array)`, но не новую `FROM`-клаузу `EntityBuilder.ArrayJoin`/`LeftArrayJoin` (правило AGENTS.md «`docs/**` **и** `docs/ru/**`»); в `api-reference` семейства нет вовсе | Добавить `ArrayJoin`/`LeftArrayJoin` (+ `ArrayJoinKind`) в provider-таблицу ClickHouse, guide/11 и `api-reference` EN+RU одним изменением (ср. AR2) |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе);
`dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **129/129**;
`tests/nextorm.core.tests` — **160/160** (0 failed); XML-`<summary>` у `ArrayJoinKind` (тип + 2 члена), обоих
fluent-методов, `SupportsArrayJoinClause`/`MakeArrayJoin` (интерфейс/база/ClickHouse) и `ArrayJoinExpressions`
присутствует; `grep -l "ArrayJoin" docs` подтверждает AJ5 (в EN/RU-доках — только скаляр, клаузы нет);
Приложение A (45) без изменений; `find -name 'PublicAPI*.txt'` — пусто (подтверждает AJ1, Шаг 5 открыт).

### ClickHouse `ARRAY JOIN` — привязка элемента (`ArrayJoinElement`, вариант A1) (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет. Новые члены:

- `public interface IArrayJoinProjection` (`Expressions/ArrayJoinProjection.cs:9`) — маркер;
- `public class ArrayJoinProjection<TEntity, TElement> : IArrayJoinProjection` (`:21`) с `Item1` (`:24`) и
  `Element` (`:27`) — XML `<summary>` есть у типа и обоих свойств;
- `EntityBuilder<TEntity>.ArrayJoinElement<TElement>(Expression<Func<TEntity,TElement[]>>) ->
  EntityBuilder<ArrayJoinProjection<TEntity,TElement>>` (`Builders/EntityBuilder.cs:343`) и
  `LeftArrayJoinElement<TElement>(...)` (`:354`) — XML `<summary>`/`<typeparam>` есть, `<exception>` — нет (AJ9);
- внутренние `ArrayJoinNames` (`ElementAlias = "__nextorm_aj_element"` `:38`, `ElementMember` `:41`),
  `EntityBuilder.SourceEntityType` (`:96-102`), `BindArrayJoinElement` (`:103-104`), `ToArrayJoinElement`
  (`:357-393`), `CopyProjectionIndependentStateTo` (`:530-556`); проброс в `QueryDefinition.BindArrayJoinElement`
  (`Query/QueryDefinition.cs:65`), `QueryCommand.BindArrayJoinElement` (`Query/QueryCommand.cs:211`),
  `QueryCommand.Clone` (`Query/QueryCommand.Clone.cs:57`), план-ключ
  (`Query/QueryPlanEqualityComparer.cs:71,315`), рендер (`DataContext/SqlBuilder.cs:119-121`), трансляцию
  (`Visitors/MemberTranslator.cs:94-109`).

Новых публичных **типов** два — оба задокументированы → Приложение A (45) без изменений. P0/P1 по именам нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| AJ6 | P2 | `Builders/EntityBuilder.cs:343,354` | Сигнатура `Expression<Func<TEntity, TElement[]>>` принимает **только CLR-массив**, тогда как соседний `ArrayJoin<TArray>` (`:291,299`) принимает `Expression<Func<TEntity, TArray>>` и валидирует произвольный `IEnumerable` (`:320`). Колонка, отображённая как `List<T>`/последовательность, не сможет использовать привязку элемента | Принять `Expression<Func<TEntity, IEnumerable<TElement>>>` (или добавить перегрузку), выровнявшись с `ArrayJoin<TArray>`; иначе явно оговорить «только массивы» в `<summary>` |
| AJ7 | P2 | `Expressions/ArrayJoinProjection.cs:9,21` | Маркер `IArrayJoinProjection` не входит в семейство `IProjection`, но носит то же имя `*Projection`: код, диспетчеризуемый по `IProjection` (маппинг, InMemory, `AliasFromProjectionVisitor`), молча его не увидит. Наследовать **нельзя**: `MemberTranslator.cs:111-119` на `IProjection`-ветке отрендерит `Item1.` как член проекции | Переименовать, убрав `Projection` (`ArrayJoinElement<TEntity,TElement>`/`IArrayJoinElement`), либо оставить и явно закрепить в `<summary>` «вне семейства `IProjection`» (текущий текст уже это фиксирует — достаточно ссылки) |
| AJ8 | P2 | `Expressions/ArrayJoinProjection.cs`; `Builders/EntityBuilder.cs:343,354`; `PublicAPI.*.txt` отсутствуют | Новые публичные члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт) | При заморозке внести `IArrayJoinProjection`, `ArrayJoinProjection<,>` (+`Item1`/`Element`), `ArrayJoinElement<TElement>`/`LeftArrayJoinElement<TElement>` (ср. AJ1/AR1) |
| AJ9 | P2 | `Builders/EntityBuilder.cs:336-355` (throw `:361-374`) | У обоих публичных методов нет `<exception>`-доков, хотя они бросают `NotSupportedException` (in-memory, `:362`) и `InvalidOperationException` (уже привязано `:365`, `Where`/`Having` до вызова `:368`, joined-запрос `:371`, смешение вида `:374`); соседний `WithStrictness` (`:402`) исключение документирует | Добавить `<exception cref="NotSupportedException">`/`<exception cref="InvalidOperationException">` с перечнем условий |

Публичная док-поверхность EN/RU (`docs/**` и `docs/ru/**`) новых методов не описывает — это расширение
AJ5 (правило AGENTS.md), нового номера не заводим; добавить `ArrayJoinElement`/`LeftArrayJoinElement` и
`ArrayJoinProjection<,>` вместе с клаузой AJ5. Сгенерированный SQL-алиас `__nextorm_aj_element` остаётся
`internal`-константой и в поверхность не входит — публично-значимого магического идентификатора нет.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе);
unit-наборы — core **161/161**, clickhouse **135/135**, postgres **179/179** (0 failed, Release, `--no-build`);
XML-`<summary>` у `IArrayJoinProjection`, `ArrayJoinProjection<,>` (тип + `Item1`/`Element`), обоих fluent-методов
и внутренних `SourceEntityType`/`BindArrayJoinElement`/`CopyProjectionIndependentStateTo` присутствует;
cref'ы `ArrayJoinProjection{TEntity, TElement}.Element` (`ArrayJoinProjection.cs:40`, `QueryDefinition.cs:63`,
`QueryCommand.cs:209`) разрешаются — сборка 0/0; `find -name 'PublicAPI*.txt'` — пусто (подтверждает AJ8);
`grep -rn "ArrayJoinElement" docs` — пусто (подтверждает расширение AJ5). Контейнерные интеграционные тесты
в этом проходе не перезапускались (по отчёту автора — 3/3 зелёные).

### PostgreSQL/ANSI query modifiers — `DISTINCT ON`/`TABLESAMPLE`/`WITH TIES`/row locking (точечный аудит 20.09.2026)

Публичная поверхность аддитивна (два новых public-типа + fluent-методы и dialect-хуки); переименований
в этом изменении нет. Новые члены:

- `public enum TablesampleMethod { System, Bernoulli }` (`Query/TablesampleMethod.cs:4`) и
  `public enum LockMode { Update, Share }` (`Query/LockMode.cs:4`) — `<summary>` у типа и **обоих**
  членов каждого;
- `EntityBuilder<TEntity>.DistinctOn<TResult>(Expression<Func<TEntity,TResult>>)` (`Builders/EntityBuilder.cs:493`),
  `.Tablesample(double, TablesampleMethod, double?)` (`:510`), `.ForUpdate()` (`:525`), `.ForShare()` (`:532`),
  `.WithTies()` (`:570`) — у всех `<summary>`; `<param>` нет у первых двух (только `<paramref>`);
- `Paging.WithTies` (`Builders/Paging.cs:39`) — `<summary>` есть; см. QM2;
- `ISqlDialect.SupportsDistinctOn`/`MakeDistinctOn` (`DataContext/Dialect/ISqlDialect.cs:758,763`),
  `SupportsTablesample`/`SupportsTablesampleMethod`/`MakeTablesample` (`:786,792,797`),
  `SupportsWithTies` (`:840`), `SupportsLocking`/`MakeLock` (`:832,834`), изменённый
  `MakeTop(int,bool,out string?)` (`:826`) — `<summary>` есть у всех, кроме полноты `<param>`/`<returns>`
  у `MakeTop` (QM4). Внутренние `DistinctOnClause`/`TablesampleClause`/`LockClause`, `QueryCommand.*`,
  `QueryDefinition.*` в поверхность не входят.

Два новых публичных типа задокументированы → Приложение A (45 недокументированных) не растёт; общее
число публичных типов 158→160. Полный пересчёт отражением — при закрытии Шага 5 (таблица §2 выше —
снимок 18.09.2026).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| QM1 | P1 | `Query/TablesampleMethod.cs:4` (и файл), `Builders/EntityBuilder.cs:510`, `ISqlDialect.cs:786,792,797`, `SqlDialectBase.cs:160,166,173`, `PostgresDialect.cs:91,94,97`, `SqlServerDialect.cs:54,57,60`, `QueryCommand.cs:207`, `QueryDefinition.cs:47` | `Tablesample`/`TablesampleMethod` — составное слово «table sample» слито в одно; Capitalization Conventions .NET требуют `TableSample`/`TableSampleMethod` (`Table` + `Sample`, как `TableName`). `DistinctOn`/`WithTies`/`LockMode` — корректны | Переименовать на месте (alpha): `TableSampleMethod`, `TableSampleClause`, `EntityBuilder.TableSample`, `SupportsTableSample`/`SupportsTableSampleMethod`/`MakeTableSample`; обновить тесты и `docs/**`+`docs/ru/**` (AGENTS.md) |
| QM2 | P2 | `Builders/Paging.cs:39` | Публичное bool-свойство `WithTies` без префикса `Is`/`Has`, тогда как соседние `IsEmpty`/`IsTop` (`:41-42`) его имеют; `Paging` — публичный контракт (`ISqlDialect.MakePage(Paging,…)`/`MakeTop`), поэтому имя входит в заморозку | `HasWithTies`/`IsWithTies`; либо оставить как SQL-зеркало и зафиксировать в `<summary>` (тогда — строка в «Отмечено, но менять не рекомендуется», ср. DSL-исключение) |
| QM3 | P2 | `ISqlDialect.cs:826`, `SqlDialectBase.cs:511`, `SqlServerDialect.cs:331` | `bool MakeTop(int limit, bool withTies, out string? topStmt)` — bool-возвращающий метод без `Try`-префикса; `bool`+`out` дублирует идиому nullable-возврата `GetPagingOrderBy` (`:846`). Смена сигнатуры — source-breaking для внешних реализаторов `ISqlDialect` (alpha; в репозитории — только `SqlDialectBase`/`SqlServerDialect`) | `TryMakeTop(...)` (Try-паттерн) либо `string? MakeTop(int limit, bool withTies)`; при заморозке учесть смену подписи в `PublicAPI.Unshipped.txt` |
| QM4 | P2 | `ISqlDialect.cs:821-826`; `Builders/EntityBuilder.cs:486-492,505-509`; `SqlDialectBase.cs:511`; `SqlServerDialect.cs:331` | `MakeTop` использует `<paramref name="withTies"/>`, но `<param>`/`<returns>`/`<paramref name="topStmt"/>` отсутствуют; `DistinctOn`/`Tablesample` ссылаются на `exp`/`percent`/`method`/`seed` без `<param>`; override `SqlDialectBase.MakeTop`/`SqlServerDialect.MakeTop` без `<summary>` (ср. U5/S2/LIM2) | Дописать `<param>`/`<returns>` и `<summary>` override'ов при закрытии Шага 5 |
| QM5 | P2 | `docs/guide/provider-specific/postgresql.md:85-89` + `docs/ru/guide/provider-specific/postgresql.md:86-90`; `docs/guide/provider-specific/overview.md:30` + RU `:31`; `docs/advanced/api-reference.md` (+RU); `docs/guide/05-sorting-and-paging.md`, `docs/guide/08-distinct.md` (+RU) | Провайдер-страница PostgreSQL **прямо противоречит коду**: `DISTINCT ON`, `LIMIT … WITH TIES`, `TABLESAMPLE`, `FOR UPDATE`/`FOR SHARE` перечислены в «Not yet supported», хотя реализованы; матрица `overview.md` и `advanced/api-reference.md`, гайды сортировки/`DISTINCT` новые модификаторы не упоминают (`rg` пуст). AGENTS.md требует `docs/**` **и** `docs/ru/**` | Перенести четыре модификатора из «Not yet supported» в перечень поддержанного, дополнить матрицу, api-reference и гайды; синхронно EN+RU |
| QM6 | P2 | `PublicAPI.*.txt` отсутствуют; `ISqlDialect.cs:758,763,786,792,797,826,832,834,840`; `Builders/EntityBuilder.cs:493,510,525,532,570`; `Builders/Paging.cs:39` | Новые/изменённые публичные члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). Абстрактные члены + смена `MakeTop` — source-breaking для внешних реализаторов `ISqlDialect` | Внести в `PublicAPI.Unshipped.txt` при заморозке: `TablesampleMethod`(+2), `LockMode`(+2), пять fluent-методов, `Paging.WithTies`, девять dialect-членов и изменённый `MakeTop` (даст пару RS0017/RS0016) |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; XML-`<summary>` у
`TablesampleMethod`/`LockMode` (тип + члены), пяти fluent-методов, `Paging.WithTies` и девяти dialect-членов
присутствует; у `MakeTop` — только `<summary>`+`<paramref>` (QM4); `find -name 'PublicAPI*.txt'` — пусто
(подтверждает QM6); unit-наборы Release `--no-build` — core **165/165**, postgres **194/194**,
sqlserver **185/185**, mysql **49/49**, mariadb **14/14**, sqlite **209/209**, clickhouse **137/137**.

### PostgreSQL text-search и temporal tables (точечный аудит 20.09.2026)

Публичная поверхность аддитивна (два новых public-типа + fluent-метод и dialect-хуки); переименований
нет. Новые члены:

- `PostgresFunctions.to_tsvector`/`to_tsquery`/`plainto_tsquery`/`phraseto_tsquery`/`websearch_to_tsquery`
  (`string`→`string?`), `ts_match(string?, string?)`→`bool`, `ts_rank(string?, string?)`→`double?`,
  `ts_headline(string, string?)`→`string?` (`Query/SqlFunctions.Postgres.cs:415-439`) — `<summary>` у всех,
  `<param>`/`<returns>` нет (стиль всего файла);
- `public enum TemporalKind { AsOf, Between, FromTo, ContainedIn, All }` (`Query/TemporalKind.cs:4`) и
  `public sealed class TemporalClause` (`Query/TemporalClause.cs:8`) — `<summary>` у типа и **всех** членов
  (5 фабрик + `Kind`/`From`/`To`), у фабрик — `<paramref>` без `<param>`;
- `EntityBuilder<TEntity>.ForSystemTime(TemporalClause)` (`Builders/EntityBuilder.cs:535`) — `<summary>`+`<param>`;
- `ISqlDialect.SupportsTextSearchFunctions` (`:251`), `SupportsTemporalTable` (`:812`),
  `SupportsTemporalKind(TemporalKind)` (`:818`), `MakeTemporalTable(TemporalClause)` (`:823`) и базовые
  `SqlDialectBase` (`:54,179,185,191`) плюс override'ы `SqlServerDialect` (`:69,72`), `MariaDbDialect`
  (`:42,45`), `PostgresDialect` (`:111`) — `<summary>` у всех; `MakeTemporalTable`/`SupportsTemporalKind`
  без `<param>`; override `MakeTemporalTable` без `<inheritdoc/>`.

Наименования `TemporalKind`/`TemporalClause`/`ForSystemTime` — корректны (PascalCase, глагольная фраза);
`ts_*` — SQL-зеркало, остаётся под DSL-исключением («Отмечено, но менять не рекомендуется»). Два новых
типа задокументированы → Приложение A (45) не растёт, поверхность §2 160→162. Документация обновлена
синхронно EN+RU (`docs/guide/provider-specific/postgresql.md` (+RU), `docs/guide/provider-specific/sqlserver.md`
(+RU), `docs/advanced/api-reference.md` (+RU), `docs/providers/overview.md` (+RU)) — нарушения класса QM5 нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| QM7 | P2 | `Query/SqlFunctions.Postgres.cs:415-439`; `Query/TemporalClause.cs:27-51`; `ISqlDialect.cs:818,823`; `SqlDialectBase.cs:185,191` | У новых публичных членов только `<summary>`: `ts_*`-методы ссылаются на `document`/`query`/`tsvector`/`tsquery` без `<param>`; `MakeTemporalTable`/`SupportsTemporalKind` — без `<param>`; override `SqlDialectBase.MakeTemporalTable` — без `<inheritdoc/>` (ср. QM4) | Дописать `<param>`/`<returns>` и `<inheritdoc/>` overrid'ов при закрытии Шага 5 |
| QM8 | P2 | `PublicAPI.*.txt` отсутствуют; `Query/TemporalKind.cs:4`; `Query/TemporalClause.cs:8`; `Builders/EntityBuilder.cs:535`; `ISqlDialect.cs:251,812,818,823`; `Query/SqlFunctions.Postgres.cs:415-439` | Новые публичные члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт) | Внести в `PublicAPI.Unshipped.txt` при заморозке: `TemporalKind`(+5), `TemporalClause`(тип + `Kind`/`From`/`To` + 5 фабрик), `ForSystemTime`, четыре dialect-члена и восемь `PostgresFunctions`-методов |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `<summary>` у `TemporalKind`
(тип + члены), `TemporalClause` (тип + члены), `ForSystemTime`, восьми `ts_*`-методов и четырёх dialect-членов
присутствует; у `MakeTemporalTable`/`SupportsTemporalKind`/`ts_*` — только `<summary>` (QM7); `rg --files
-g 'PublicAPI*.txt'` — пусто (QM8); unit (Release, `--no-build`, этот проход): core **166/166**,
postgres **198/198**, sqlserver **191/191**, mariadb **16/16**, sqlite **211/211**, clickhouse **139/139**.

### PostgreSQL `round`/`extract`/`date_part`/`setseed`/`digest`+`sha256`/`array_shuffle`+`array_sample` (точечный аудит 20.09.2026)

Публичная поверхность **аддитивна**: переименований нет, новых public-типов нет (Приложение A — 45 без
изменений; счётчик типов §2 не растёт). Новые члены:

- `CommonFunctions.extract(string part, DateTime? value) -> int?` (`Query/SqlFunctions.cs:333`),
  `CommonFunctions.date_part(string part, DateTime? value) -> double?` (`:342`) — `<summary>` есть,
  `<param>`/`<returns>` нет (стиль файла);
- `PostgresFunctions.array_shuffle<T>(T[] array) -> T[]` (`Query/SqlFunctions.Postgres.cs:91`),
  `array_sample<T>(T[] array, int n) -> T[]` (`:94`), `setseed(double? seed) -> double?` (`:290`),
  `digest(string? data, string? type) -> byte[]?` (`:359`), `digest(byte[]? data, string? type) -> byte[]?`
  (`:367`), `sha256(byte[]? data) -> byte[]?` (`:374`) — `<summary>` у всех;
- `ISqlDialect.SupportsRandomSeed` (`DataContext/Dialect/ISqlDialect.cs:249`), `SupportsCryptoFunctions`
  (`:257`), `SupportsDatePart(string)` (`:662`) — **абстрактные** (source-breaking для внешних
  реализаторов); `MakeMathFunction(string, IReadOnlyList<string>, IReadOnlyList<Type>)` (`:678`) — **DIM**;
- `SqlDialectBase.SupportsRandomSeed` (`:53`), `SupportsCryptoFunctions` (`:55`), `SupportsDatePart`
  (`:424`), `MakeMathFunction` 3-арг. (`:433`);
- override'ы: `PostgresDialect.SupportsDatePart` (`:75`), `MakeDatePart` (`:79`, новый override),
  `SupportsRandomSeed` (`:119`), `SupportsCryptoFunctions` (`:122`), `MakeMathFunction` 3-арг. (`:180`);
  `MySqlDialect` (`:194,206`), `SqliteDialect` (`:149,168`), `SqlServerDialect` (`:181,192`),
  `ClickHouseDialect` (`:336,348`) — `SupportsDatePart` у всех пяти, `MakeDatePart` изменён у четырёх.

**Контракты.** Корректны: `extract`→`int?` (целые части), `date_part`→`double?` (только `epoch`),
`digest`/`sha256`→`byte[]?` (PG `bytea`), `array_shuffle`/`array_sample`→`T[]`. Отмечено:
`setseed`→`double?` при PG `void` — контракт «всегда `null`» описан в `<summary>`, но `<returns>` нет;
`digest(null, "sha256")` неоднозначен между `string?`/`byte[]?` на стороне вызова (транслятор
диспетчеризует по имени и рендерит одинаково — рантайм-эффекта нет). `date_part` намеренно принимает
только `epoch` (числовой аналог integer-`extract`); 3-арг. `MakeMathFunction` — **не** мёртвый дубль
`MakeTextJsonFunction(string)`: DIM снимает source-breaking, `virtual` в базе нужен для override
`PostgresDialect`. FQN списка заморозки сверены с XML-документацией собранной сборки
(`M:NextORM.Core.CommonFunctions.extract`, `M:NextORM.Core.PostgresFunctions.array_shuffle`).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| RD1 | P2 | `PostgresDialect.cs:180-183`, `SqlDialectBase.cs:424` | Новые публичные члены без XML-`<summary>`: override `PostgresDialect.MakeMathFunction(string, IReadOnlyList<string>, IReadOnlyList<Type>)` задокументирован `//`, `SqlDialectBase.SupportsDatePart(string)` — без доки; четыре изменённых `MakeDatePart`-override'а (mysql/sqlite/sqlserver/clickhouse) тоже `//`. `NoWarn=CS1591` во всех 7 библиотечных `.csproj` скрывает пропуск (ср. N2/U5/Q4/A2) | Добавить `<summary>` при закрытии Шага 5 |
| RD2 | P2 | `ISqlDialect.cs:249,257,662`; `SqlDialectBase.cs:53,55,424,433`; `PostgresDialect.cs:75,79,119,122,180`; `MySqlDialect.cs:194,206`; `SqliteDialect.cs:149,168`; `SqlServerDialect.cs:181,192`; `ClickHouseDialect.cs:336,348` | Новые/изменённые члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). `SupportsRandomSeed`/`SupportsCryptoFunctions`/`SupportsDatePart` — абстрактные: source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`); `MakeMathFunction` 3-арг. — DIM, разрыва не создаёт | Внести в `PublicAPI.Unshipped.txt` при заморозке (полный список ниже; ср. C/U4/Q3/A3/J1/D1/UG2/S1) |
| RD3 | P2 | `Query/SqlFunctions.Postgres.cs:285-290`; `Query/SqlFunctions.cs:333,342`; `Query/SqlFunctions.Postgres.cs:359,367` | `setseed` возвращает `double?` при PG-`void` — `<summary>` описывает «always null», но `<returns>`/`<remarks>` нет; `digest(null, "sha256")` неоднозначен между перегрузками | Дописать `<returns>` у `setseed`; неоднозначность перегрузок оставить (SQL одинаков) |
| RD4 | P2 | `docs/advanced/api-reference.md:59` + `docs/ru/advanced/api-reference.md:59` | Строка `CommonFunctions` перечисляет `date_trunc`/`date_add`/`date_diff`/`date_from_parts`/`end_of_month`, но не новые `extract`/`date_part` (EN и RU синхронно). Гайд `docs/guide/11-scalar-functions.md` (+RU) и `docs/providers/postgres.md` (+RU) обновлены — расхождения класса QM5 нет | Дополнить перечисление `extract`/`date_part` в обеих ветках |
| RD5 | P2 | `Query/SqlFunctions.cs:342`; `BuiltinFunctionTranslator.cs:292-293` | `date_part` отклоняет все части, кроме `epoch`, тогда как в PostgreSQL `date_part('year', x)` — валидный double-аналог `extract`. Сужение описано в XML, но расходится с PG-ожиданием | Оставить нормализованную форму и явнее сослаться на `extract` в XML/прозе либо расширить `date_part` до числовых частей |

**Обновление (todo-pg, аудит применён).** RD1 закрыта: `<summary>` добавлены `SqlDialectBase.SupportsDatePart`,
`PostgresDialect.MakeMathFunction(...3-арг.)` и четырём `MakeDatePart`-override'ам
(mysql/sqlite/sqlserver/clickhouse). RD3 закрыта: у `setseed` появился `<returns>`. RD4 закрыта: в
`docs/advanced/api-reference.md` (+RU) добавлены `extract`/`date_part`, `array_shuffle`/`array_sample`,
`digest`/`sha256`, `setseed`. RD5 закрыта: у `date_part` добавлен `<remarks>` со ссылкой на `extract`.
RD2 остаётся открытой — `PublicAPI.Unshipped.txt` не заведён (Шаг 5 вне рамок этого пункта), подписи
для заморозки ниже сохранены. Слияние `todo-ch` добавляет к RD2 ClickHouse-часть поверхности
(24 `to_*`, `generate_random` ×2, `IGenerateRandomRow`, 4 члена `ISqlDialect`/`SqlDialectBase` и 4
переопределения в `ClickHouseDialect`); её подписи дописаны в тот же блок ниже (`# --- продолжение RD2`),
отдельная находка не заводится — см. раздел «Слияние `todo-ch`…» после OJW1.

**Шаг 5 — точные подписи для `PublicAPI.Unshipped.txt` (RD2)** (формат Roslyn PublicAPI, `#nullable enable`):

```text
NextORM.Core.ISqlDialect.SupportsRandomSeed.get -> bool
NextORM.Core.ISqlDialect.SupportsCryptoFunctions.get -> bool
NextORM.Core.ISqlDialect.SupportsDatePart(string! part) -> bool
NextORM.Core.ISqlDialect.MakeMathFunction(string! name, System.Collections.Generic.IReadOnlyList<string!>! args, System.Collections.Generic.IReadOnlyList<System.Type!>! argTypes) -> string!
NextORM.Core.SqlDialectBase.SupportsRandomSeed.get -> bool
NextORM.Core.SqlDialectBase.SupportsCryptoFunctions.get -> bool
NextORM.Core.SqlDialectBase.SupportsDatePart(string! part) -> bool
NextORM.Core.SqlDialectBase.MakeMathFunction(string! name, System.Collections.Generic.IReadOnlyList<string!>! args, System.Collections.Generic.IReadOnlyList<System.Type!>! argTypes) -> string!
NextORM.Postgres.PostgresDialect.SupportsRandomSeed.get -> bool
NextORM.Postgres.PostgresDialect.SupportsCryptoFunctions.get -> bool
NextORM.Postgres.PostgresDialect.SupportsDatePart(string! part) -> bool
NextORM.Postgres.PostgresDialect.MakeDatePart(string! part, string! value) -> string!
NextORM.Postgres.PostgresDialect.MakeMathFunction(string! name, System.Collections.Generic.IReadOnlyList<string!>! args, System.Collections.Generic.IReadOnlyList<System.Type!>! argTypes) -> string!
NextORM.MySql.MySqlDialect.SupportsDatePart(string! part) -> bool
NextORM.Sqlite.SqliteDialect.SupportsDatePart(string! part) -> bool
NextORM.SqlServer.SqlServerDialect.SupportsDatePart(string! part) -> bool
NextORM.ClickHouse.ClickHouseDialect.SupportsDatePart(string! part) -> bool
NextORM.Core.CommonFunctions.extract(string! part, System.DateTime? value) -> int?
NextORM.Core.CommonFunctions.date_part(string! part, System.DateTime? value) -> double?
NextORM.Core.PostgresFunctions.array_shuffle<T>(T[]! array) -> T[]!
NextORM.Core.PostgresFunctions.array_sample<T>(T[]! array, int n) -> T[]!
NextORM.Core.PostgresFunctions.setseed(double? seed) -> double?
NextORM.Core.PostgresFunctions.digest(string? data, string? type) -> byte[]?
NextORM.Core.PostgresFunctions.digest(byte[]? data, string? type) -> byte[]?
NextORM.Core.PostgresFunctions.sha256(byte[]? data) -> byte[]?
# --- продолжение RD2: ClickHouse-поверхность слияния todo-ch (20.09.2026) ---
NextORM.Core.ISqlDialect.SupportsDateConversionFunctions.get -> bool
NextORM.Core.ISqlDialect.SupportsStringSplit.get -> bool
NextORM.Core.ISqlDialect.MakeDateConversion(string! name, System.Collections.Generic.IReadOnlyList<string!>! args) -> string!
NextORM.Core.ISqlDialect.MakeStringSplit(string! separator, string! value) -> string!
NextORM.Core.SqlDialectBase.SupportsDateConversionFunctions.get -> bool
NextORM.Core.SqlDialectBase.SupportsStringSplit.get -> bool
NextORM.Core.SqlDialectBase.MakeDateConversion(string! name, System.Collections.Generic.IReadOnlyList<string!>! args) -> string!
NextORM.Core.SqlDialectBase.MakeStringSplit(string! separator, string! value) -> string!
NextORM.ClickHouse.ClickHouseDialect.SupportsDateConversionFunctions.get -> bool
NextORM.ClickHouse.ClickHouseDialect.SupportsStringSplit.get -> bool
NextORM.ClickHouse.ClickHouseDialect.MakeDateConversion(string! name, System.Collections.Generic.IReadOnlyList<string!>! args) -> string!
NextORM.ClickHouse.ClickHouseDialect.MakeStringSplit(string! separator, string! value) -> string!
NextORM.Core.ClickHouseFunctions.to_date<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_date_time<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_date32<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_year<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_quarter<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_month<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_day_of_month<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_day_of_week<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_day_of_year<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_hour<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_minute<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_second<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_start_of_year<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_quarter<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_month<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_week<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_day<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_hour<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_minute<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_second<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_monday<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_yyyymm<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_yyyymmdd<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_unix_timestamp<T>(T? value) -> long?
NextORM.Core.ClickHouseFunctions.generate_random() -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IGenerateRandomRow!>!
NextORM.Core.ClickHouseFunctions.generate_random(long seed) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IGenerateRandomRow!>!
NextORM.Core.SqlFunctions.IGenerateRandomRow
NextORM.Core.SqlFunctions.IGenerateRandomRow.Id.get -> long
NextORM.Core.SqlFunctions.IGenerateRandomRow.Id.set -> void
NextORM.Core.SqlFunctions.IGenerateRandomRow.Value.get -> double
NextORM.Core.SqlFunctions.IGenerateRandomRow.Value.set -> void
NextORM.Core.SqlFunctions.IGenerateRandomRow.Name.get -> string?
NextORM.Core.SqlFunctions.IGenerateRandomRow.Name.set -> void
# --- продолжение RD2: PostgreSQL наборные функции (20.09.2026) ---
NextORM.Core.SqlFunctions.IRegexpMatchesRow
NextORM.Core.SqlFunctions.IRegexpMatchesRow.Matches.get -> string![]!
NextORM.Core.SqlFunctions.IRegexpMatchesRow.Matches.set -> void
NextORM.Core.SqlFunctions.IRegexpSplitToTableRow
NextORM.Core.SqlFunctions.IRegexpSplitToTableRow.Value.get -> string?
NextORM.Core.SqlFunctions.IRegexpSplitToTableRow.Value.set -> void
NextORM.Core.SqlFunctions.IJsonArrayElementsRow
NextORM.Core.SqlFunctions.IJsonArrayElementsRow.Value.get -> string?
NextORM.Core.SqlFunctions.IJsonArrayElementsRow.Value.set -> void
NextORM.Core.SqlFunctions.IJsonbEachRow
NextORM.Core.SqlFunctions.IJsonbEachRow.Key.get -> string?
NextORM.Core.SqlFunctions.IJsonbEachRow.Key.set -> void
NextORM.Core.SqlFunctions.IJsonbEachRow.Value.get -> string?
NextORM.Core.SqlFunctions.IJsonbEachRow.Value.set -> void
NextORM.Core.SqlFunctions.IJsonObjectKeysRow
NextORM.Core.SqlFunctions.IJsonObjectKeysRow.Key.get -> string?
NextORM.Core.SqlFunctions.IJsonObjectKeysRow.Key.set -> void
NextORM.Core.SqlFunctions.IJsonPathQueryRow
NextORM.Core.SqlFunctions.IJsonPathQueryRow.Value.get -> string?
NextORM.Core.SqlFunctions.IJsonPathQueryRow.Value.set -> void
NextORM.Core.SqlFunctions.ITsStatRow
NextORM.Core.SqlFunctions.ITsStatRow.Word.get -> string?
NextORM.Core.SqlFunctions.ITsStatRow.Word.set -> void
NextORM.Core.SqlFunctions.ITsStatRow.Ndoc.get -> int?
NextORM.Core.SqlFunctions.ITsStatRow.Ndoc.set -> void
NextORM.Core.SqlFunctions.ITsStatRow.Nentry.get -> int?
NextORM.Core.SqlFunctions.ITsStatRow.Nentry.set -> void
NextORM.Core.PostgresFunctions.regexp_matches(string? source, string? pattern) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IRegexpMatchesRow!>!
NextORM.Core.PostgresFunctions.regexp_matches(string? source, string? pattern, string? flags) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IRegexpMatchesRow!>!
NextORM.Core.PostgresFunctions.regexp_split_to_table(string? source, string? pattern) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IRegexpSplitToTableRow!>!
NextORM.Core.PostgresFunctions.regexp_split_to_table(string? source, string? pattern, string? flags) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IRegexpSplitToTableRow!>!
NextORM.Core.PostgresFunctions.jsonb_array_elements(object? json) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonArrayElementsRow!>!
NextORM.Core.PostgresFunctions.jsonb_array_elements_text(object? json) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonArrayElementsRow!>!
NextORM.Core.PostgresFunctions.jsonb_each(object? json) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonbEachRow!>!
NextORM.Core.PostgresFunctions.jsonb_each_text(object? json) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonbEachRow!>!
NextORM.Core.PostgresFunctions.jsonb_object_keys(object? json) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonObjectKeysRow!>!
NextORM.Core.PostgresFunctions.jsonb_path_query(object? json, string? path) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonPathQueryRow!>!
NextORM.Core.PostgresFunctions.ts_stat(string? query) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.ITsStatRow!>!
NextORM.Core.PostgresFunctions.jsonpath(string? path) -> string?
NextORM.Postgres.PostgresDialect.WrapTableFunction(string! name, string! call) -> string!
```

`MakeDatePart` у mysql/sqlite/sqlserver/clickhouse уже существовал (сигнатура не менялась) — в заморозку
не входит, как и удалений (RS0017) в этом изменении нет.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `find -name
'PublicAPI*.txt'` — пусто (подтверждает RD2); `<summary>` присутствует у всех новых членов, кроме
перечисленных в RD1; unit (Release, `--no-build`, этот проход): core **166/166**, postgres **206/206**,
sqlserver **195/195**, mysql **52/52**, mariadb **19/19**, sqlite **220/220**, clickhouse **143/143**;
интеграционные (Podman socket) — **11/11 passed, 0 skipped**.
### SQL Server `OPENJSON ... WITH` через `SqlTableFunctionAttribute.WithClause` (точечный аудит 20.09.2026)

Область: `SqlTableFunctionAttribute.WithClause` (`src/nextorm.core/SqlTableFunctionAttribute.cs:34`), `TableFunctionExpression.WithClause` и перегрузка конструктора (`Expressions/TableFunctionExpression.cs:20-46`), `SqlSourceRenderer.MakeTableFunction` (`DataContext/SqlSourceRenderer.cs:320-330`), док `SqlServerFunctions.openjson` (`Query/SqlFunctions.SqlServer.cs:50-60`). Тесты: `SqlGenerationTests.OpenJsonWith_ShouldAppendWithClause`, `SqlServerSpecificTests.OpenJson_WithTypedSchema_ShouldReturnTypedColumns`. Build Release — **0/0**; sqlserver unit **194/194**; контейнерная интеграция (реальный SQL Server через Testcontainers) — **1/1**. XML-`<summary>` есть у `WithClause` (свойство атрибута и свойство выражения) и обоих конструкторов; новых публичных **типов** нет → Приложение A (45) без изменений; Шаг 5 открыт.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| OJW1 | P2 | `SqlTableFunctionAttribute.cs:34`; `Expressions/TableFunctionExpression.cs:20,46` | Новые публичные члены (`SqlTableFunctionAttribute.WithClause`, `TableFunctionExpression.WithClause` + перегрузка ctor) не отслеживаются: `PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён. Шаг 5 открыт | Внести их в `PublicAPI.Unshipped.txt` при заморозке (ср. TF1/Z1/CNT1) |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** `WithClause` — PascalCase-существительное, зеркалит SQL `WITH (...)`; BCL-конфликтов нет.
- **`WithClause` — декларация формы, не инъекция SQL.** Значение эмитится дословно, как provider-нативный `[SqlFunction]`; это то же DSL-исключение для SQL-зеркал, что уже принято в реестре.
- **Обратная совместимость.** Старый публичный конструктор `TableFunctionExpression(string, string?, MethodCallExpression)` сохранён и делегирует новому; изменения существующей сигнатуры нет.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet test tests/nextorm.sqlserver.tests -c Debug` — **194/194**; `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -method nextorm.integration.tests.SqlServerSpecificTests.OpenJson_WithTypedSchema_ShouldReturnTypedColumns` — **1/1**; `rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает OJW1); EN+RU `docs/guide/13-table-valued-functions.md`, `docs/providers/sqlserver.md` синхронны.

### Слияние `todo-ch`: ClickHouse date conversion + `string.Split` (точечный аудит 20.09.2026)

Область: `Query/SqlFunctions.ClickHouse.cs` (24 `to_*`-метода, `generate_random()`/`generate_random(long)`,
`SqlFunctions.IGenerateRandomRow`), `src/nextorm.clickhouse/ClickHouseDialect.cs`
(`MakeDatePart`/`SupportsDatePart`/`MakeDateConversion`/`SupportsDateConversionFunctions`/
`SupportsStringSplit`/`MakeStringSplit`/`WrapTableFunction`), `ISqlDialect`/`SqlDialectBase`
(те же новые члены), `Visitors/DateConversionSqlTranslator.cs` (internal, внешней поверхности не даёт).
Build Release — **0 warnings / 0 errors** (этот проход); `rg --files -g 'PublicAPI*.txt'` — пусто.

**Итог: новых P0/P1 нет; единственная P2 — уже открытая RD2 (ClickHouse-подписи дописаны в её блок
Шага 5), OJW1 — без изменений.**

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| RD2 (CH-часть) | P2 | `ISqlDialect.cs:149-150,228,738,765`; `SqlDialectBase.cs:35,47,494,527`; `ClickHouseDialect.cs:41,47,106,490`; `Query/SqlFunctions.ClickHouse.cs:230-239,286-358`; `Query/SqlFunctions.cs:134` | CH-часть поверхности не трекается (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2, не новая находка.** `SupportsStringSplit`/`SupportsDateConversionFunctions` — абстрактные члены `ISqlDialect` ⇒ source-breaking для внешних реализаторов (как `SupportsRandomSeed`/`SupportsCryptoFunctions`); `MakeDateConversion`/`MakeStringSplit` — DIM в интерфейсе и `virtual` в базе, разрыва не создают | Подписи внесены в блок Шага 5 (RD2); при заморозке трекать в `PublicAPI.Unshipped.txt` |

ℹ️ **Наблюдения (фикс не требуется):**
- **`MakeDatePart` — слияние PG+CH когерентно (проверено).** `dow` = `toInt32(toDayOfWeek(x) % 7)` даёт `0`=Sunday..`6`=Saturday, как `extract(dow)` у остальных; `isodow` = `toInt32(toDayOfWeek(x))` = ISO 1..7; `week` = `toISOWeek` (ISO 8601; `week` уже в базовом `DatePartFields`, `SqlDialectBase.cs:421-425`); остальные части — `toXxx` с `toInt32`; `epoch` — `toFloat64(toUnixTimestamp(...))`. `SupportsDatePart` CH добавляет `dow`/`isodow`/`epoch` поверх базы — наборы провайдеров согласованы.
- **`to_day_of_week` и `extract('dow')` расходятся намеренно.** `MakeDateConversion("to_day_of_week")` = `toInt32(toDayOfWeek(x))` (CH-нативные 1..7, Mon..Sun), что совпадает с XML-доком метода (`SqlFunctions.ClickHouse.cs:306`); нормализованный `0..6` — только у `extract`/`date_part`. Оба поведения задокументированы, поэтому это не P2-несогласованность.
- **XML-doc coverage полная.** `<summary>` есть у всех 24 `to_*`, обоих `generate_random`, `IGenerateRandomRow`, `SupportsStringSplit`/`SupportsDateConversionFunctions`/`MakeDateConversion`/`MakeStringSplit` в `ISqlDialect` и `SqlDialectBase`, у четырёх CH-переопределений и `SqlTableFunctionAttribute.WithClause`. Свойства `IGenerateRandomRow.Id/Value/Name` — без индивидуальных `<summary>`, как у соседних row-интерфейсов (`IZerosRow`, `INumbersRow`); Приложение A (45) без изменений. `CS1591` по-прежнему в `<NoWarn>` 7 `.csproj`.
- **`IGenerateRandomRow`/`generate_random` — нейминг конвенциям соответствует.** `I…Row` — как `IZerosRow`/`INumbersRow`/`IGenerateSeriesRow`; snake_case `to_*`/`generate_random` — принятое DSL-исключение для SQL-зеркал. BCL-конфликтов нет.
- **`MakeDateConversion`/`MakeStringSplit` — capability-hook, не понижение видимости.** Имена зеркалят уже принятые `MakeDatePart`/`MakeArrayFunction`; гейт `Supports*` стоит в трансляторе до рендера (`DateConversionSqlTranslator.cs:100-105`, `StringFunctionTranslator.cs:342`).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (этот проход); в диффе `src`+`tests` новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; `rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает RD2/OJW1). Тесты в этом проходе не перезапускались (только build).

### PostgreSQL наборные функции через `[SqlTableFunction]` (точечный аудит 20.09.2026)

Область: `Query/SqlFunctions.cs` (7 row-интерфейсов: `IRegexpMatchesRow`, `IRegexpSplitToTableRow`,
`IJsonArrayElementsRow`, `IJsonbEachRow`, `IJsonObjectKeysRow`, `IJsonPathQueryRow`, `ITsStatRow`),
`Query/SqlFunctions.Postgres.cs` (11 методов-`[SqlTableFunction]` + скалярный `jsonpath`),
`src/nextorm.postgres/PostgresDialect.cs` (`SupportsTableFunction` — новые имена,
`WrapTableFunction` — новый override), `Expressions/SelectExpression.cs` (ветка `string[]`),
`Visitors/JsonSqlTranslator.cs` (ветка `jsonpath`, internal). `SqlDialectBase.SupportsTableFunction`
не менялся (base уже `false`); новых `Supports*`/`Make*`-флагов нет — используется строковый гейт по
имени. Build Release — **0/0**; `rg --files -g 'PublicAPI*.txt'` — пусто.

**Итог: новых P0/P1 нет; единственная P2 — уже открытая RD2 (PG-подписи дописаны в её блок
Шага 5), OJW1/RD2-CH — без изменений.**

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| RD2 (PG-setof) | P2 | `Query/SqlFunctions.cs`; `Query/SqlFunctions.Postgres.cs`; `src/nextorm.postgres/PostgresDialect.cs` | Поверхность не трекается (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2, не новая находка.** Новых абстрактных членов `ISqlDialect` нет → source-разрыва для внешних реализаторов нет; `WrapTableFunction` — `virtual` override, разрыва не создаёт | Подписи внесены в блок Шага 5 (RD2); при заморозке трекать в `PublicAPI.Unshipped.txt` |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** Методы зеркалят SQL-токены (`regexp_matches`,
  `jsonb_array_elements_text`, `ts_stat`) — принятое DSL-исключение (как `generate_series`/`to_*`);
  `jsonpath` назван по типу `jsonpath`. `I…Row` — как `IGenerateSeriesRow`/`IStringSplitRow`.
  BCL-конфликтов нет.
- **`WrapTableFunction` — исправление существующего бага, не новое поведение.** PostgreSQL заменяет
  единственную колонку скалярной наборной функции на псевдоним в `FROM`, а nextorm всегда алиасит
  производный источник (`RequireSubqueryAlias`), поэтому `generate_series`/`unnest` уже были
  неработоспособны на реальном PostgreSQL (интеграционного теста не было). Обёртка
  `(select <name> from <call>)` восстанавливает имя колонки; функции с явной колонкой не оборачиваются.
  Подтверждено реальными интеграционными тестами.
- **`string[]` в row reader — точечно, не общая материализация массивов.** Ветка в
  `SelectExpression.GetDataRecordMethod` (`GetValueMI`) добавлена по образцу `byte[]`; она нужна
  только для `regexp_matches` (`text[]`) и не включает заблокированный пункт триажа «row reader
  `Array(T)`/`Tuple`» (общая типизированная материализация массивов по-прежнему не реализована).
- **`jsonpath` — не инъекция SQL.** Рендерится `cast(expr as jsonpath)` через `SqlOperandTranslator`,
  как уже делает `EmitJsonPathFunction` для `jsonb_path_query_first`; аргумент параметризуется
  штатно.
- **XML-doc coverage полная.** `<summary>` есть у 7 row-интерфейсов, 11 методов-`[SqlTableFunction]`,
  `jsonpath`, override `WrapTableFunction`; свойства row-интерфейсов — без индивидуальных `<summary>`,
  как у соседних (`IGenerateSeriesRow`, `INumbersRow`). Приложение A (45) без изменений; `CS1591`
  по-прежнему в `<NoWarn>` 7 `.csproj`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; unit (Debug): core
**166/166**, postgres **218/218**, sqlite **221/221**, sqlserver **198/198**, mysql **53/53**,
mariadb **20/20**, clickhouse **155/155**; контейнерная интеграция (Podman socket) —
`PostgresSpecificTests` **18/18**, `PostgresIntegrationTests` (общий набор) **211/211, 6 skipped**
(TVF-набор пропускается для PostgreSQL). Rejection — `PostgresTableFunctions_ShouldThrowBecauseOnlyPostgresHasThem`
в sqlite/sqlserver/mysql/mariadb/clickhouse. EN+RU `docs/guide/13-table-valued-functions.md`,
`docs/guide/provider-specific/postgresql.md`, `docs/providers/postgres.md` синхронны.

### Коррелированный скаляр в проекции: ссылка на член join-проекции (точечный аудит 20.09.2026)

Область: `Visitors/MemberTranslator.VisitMember` + новый `private static`
`MemberTranslator.TryTranslateProjectionOuterReference` (`src/nextorm.core/Visitors/MemberTranslator.cs`).
Изменение **внутреннее** (`MemberTranslator` — `internal`), публичный API не затронут: новые члены —
`private`, новых публичных типов/методов/свойств нет. Build Release — **0 warnings / 0 errors**
(этот проход); `rg --files -g 'PublicAPI*.txt'` — пусто (Шаг 5 открыт).

**Итог: публичных P0/P1/P2 нет.**

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| — | — | — | Новых публичных членов нет; внутренний метод `TryTranslateProjectionOuterReference` не требует трекинга `PublicAPI.*` | — |

ℹ️ **Наблюдения (фикс не требуется):**
- **Публичная поверхность не расширялась.** Корреляция по-прежнему выражается существующими
  терминалами `QueryCommand<T>` и `SqlFunctions.Sql.exists/@in/any/all`; отдельный `OuterRef*`-API
  сознательно не вводился (`plan-correlated-subqueries.md` §2 «Не-цели»). Дублирование поверхности
  запрещено границами скилла, поэтому «публичный API поверх `OuterRefMarker`» не добавлялся.
- **Именование — конвенции соблюдены.** `TryTranslateProjectionOuterReference` — private,
  `Try…`-префикс как у соседних `TryTranslate`; XML-`<summary>` присутствует.
- **Гейт провайдеров не нужен.** Коррелированный скалярный подзапрос — ANSI; ограничение относится к
  in-memory, который уже бросает `NotSupportedException` (`InMemoryQueryBuilder`). Новый
  `Supports*`-флаг не вводился намеренно (нет невыразимого провайдера).
- **`AliasFromProjectionVisitor` — переиспользован, не продублирован.** Алиас элемента `ItemN`
  берётся тем же visitor'ом, что и в существующей ветке `OuterRefMarker`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; SQL-gen: core
**166/166**, sqlite **222/222**, sqlserver **198/198**, postgres **209/209**, mysql **52/52**,
mariadb **19/19**, clickhouse **154/154**; интеграция (Podman socket): SQL Server
`-class …SqlServerIntegrationTests -method "*Correlated*"` — **14/14**, Postgres/MySQL по **2/2**,
SQLite `-method "*OnJoinProjection*"` — **2/2**; в диффе `src`+`tests` новых
`#pragma`/`SuppressMessage`/`NoWarn` — **0**; `rg --files -g 'PublicAPI*.txt'` — пусто.

## 3. Находки

### Статус находок (актуализация 18.09.2026)

P0 (#1–#9) и **все P1/P2 выполнены** 18.09.2026 (alpha; см. §5.1–§5.4). Актуальные `file:line` (после переименований):

| # | Ур. | Текущее место | Статус |
|---|---|---|---|
| 1 | P0 | `Expressions/XxHash32.cs:19` (`XxHash32`, больше не `HashCode`) | ✅ исправлено |
| 2 | P0 | `Query/IQueryRegistry.cs:15` | ✅ исправлено |
| 3 | P0 | `DataContext/DataContextExtensions.cs` (`With`/`WithRecursive` слиты сюда) | ✅ исправлено |
| 4 | P0 | `Builders/Joins/JoinedEntityBuilder.cs` | ✅ исправлено |
| 5 | P0 | `DataContext/QueryPreparationException.cs:14` (наследует `DataContextException`) | ✅ исправлено |
| 6 | P0 | `Query/RawSqlOverride.cs` (`internal`, свойства) | ✅ исправлено |
| 7 | P0 | `Builders/Projection.cs` (`Item1`…`Item8`) | ✅ исправлено |
| 8 | P0 | `BasicHelpers` удалён (мёртвый код, 0 вхождений) | ✅ исправлено |
| 9 | P0 | `Builders/TableAlias.cs` (`GetInt32`/`GetInt64`/…, параметр `columnName`) | ✅ исправлено |
| 10 | P1 | `Query/SqlFunctions.cs` (`SqlFunctions` + `CommonFunctions`; `*MI`/`SQLExpression` — `internal`) | ✅ исправлено |
| 11 | P1 | `DataContext/Meta/IEntityMetadata.cs:11`, `IPropertyMetadata.cs:13` | ✅ исправлено |
| 12 | P1 | `Query/WindowFunctions.cs` (Window* вынесены из DSL) | ✅ исправлено |
| 13 | P1 | `Parameter.cs:10`, `Query/IParameterProvider.cs:12,19` | ✅ исправлено |
| 14 | P1 | `ExpressionExtensions.cs`, `Visitors/ReplaceExpressionVisitor.cs` | ✅ исправлено |
| 15 | P1 | `Visitors/ParameterVisitors.cs:12` | ✅ исправлено |
| 16 | P1 | `DataContext/ValueList.cs:15,25,92,100` | ✅ исправлено |
| 17 | P1 | `Builders/Paging.cs:17`, `Expressions/Sorting.cs:20` (свойства, не поля) | ✅ исправлено |
| 18 | P1 | `DataContext/SqlBuilder.cs:16` | ✅ исправлено |
| 19 | P2 | `Builders/Paging.cs` (`Limit`/`Offset` — свойства с валидацией), `Expressions/Sorting.cs` | ✅ исправлено |
| 20 | P2 | `SqlTableAttribute.cs`, `Query/IColumnsProvider.cs` (файл = тип) | ✅ исправлено |
| 21 | P2 | `Payload/IPayload.cs` удалён (пустой маркер без использования) | ✅ исправлено |
| 22 | P2 | `DataContext/DataContext.cs`, `DI/DataContextBuilder.cs`, `*DataContext`, `InMemoryDataContext` | ✅ исправлено |
| 23 | P2 | `Query/RawSqlOverride.cs` (internal) | ✅ закрыто вместе с P0-6 |
| 24 | P2 | все `Default*Provider` в `Query/`; `DefaultParameterProvider.cs` выделен | ✅ исправлено |
| 25 | P2 | `nextorm.core.sourcegenerator/AnonymousClassGenerator.cs` (`internal`, переименован) | ✅ исправлено |
| 26 | P2 | all `src` — `namespace NextORM.Core` / `NextORM.<Provider>` | ✅ исправлено |
| 27 | P1 | `SqlFunctions.Postgres`/`.SqlServer`/`.ClickHouse` (`PostgresFunctions`/`SqlServerFunctions`/`ClickHouseFunctions`) | ✅ исправлено |
| 28 | P2 | `{Provider}DataContextOptionsBuilderExtensions` (уникальные имена, файлы переименованы) | ✅ исправлено |

### P0 — вводят в заблуждение / конфликтуют с BCL

Все пункты P0 **выполнены** (alpha: переименование на месте, без `[Obsolete]`/алиасов).

| # | Было | Стало | Комментарий |
|---|------|-------|-------------|
| 1 | `HashCode` (public struct) | `XxHash32` (public) | Затенение `System.HashCode` устранено; реализация xxHash32. Файл `Expressions/HashCode.cs` → `XxHash32.cs`; тест `HashCodeTests` → `XxHash32Tests`. `internal` не сделан, т.к. тип покрыт тестами из отдельной сборки |
| 2 | `IQueryProvider` | `IQueryRegistry` | Конфликт с `System.Linq.IQueryProvider` устранён. Файл/интерфейс переименованы |
| 3 | `IDataContextExtensions`, `DataContextExtensions` | `DataContextExtensions` | `With`/`WithRecursive` перенесены в `DataContextExtensions.cs`; файл-дубль удалён (было два `*Extensions` для одного `IDataContext`) |
| 4 | `EntityP2`…`EntityP8` | `JoinedEntityBuilder<T1,T2>`…`<T1..T8>` | Файл `JoinCommandBuilder.cs` → `JoinedEntityBuilder.cs` |
| 5 | `PrepareException : Exception` | `QueryPreparationException : DataContextException` | Заодно `BuildSqlCommandException : DataContextException` — единая библиотечная база |
| 6 | `DbQueryCommandExtension` (public поля) | `RawSqlOverride` (`internal sealed`, свойства) | Удалён из публичной поверхности; `ManualSql`/`MakeParams` — свойства |
| 7 | `Projection.t1`…`t8` | `Projection.Item1`…`Item8` | Рекомендация `T1`…`T8` **нереализуема**: член не может совпадать с именем generic-параметра (`CS0102`). Использованы идиоматичные tuple-имена `ItemN` (как в `System.Tuple`). Обновлены `MemberTranslator` (позиция по числовому суффиксу), `AliasFromProjectionVisitor` (`ItemN` → SQL-алиас `tN`), `InMemoryDataContext`/`QueryPlanner` (reflection по имени `ItemN`) |
| 8 | `BasicHelpers` + файл `BasicHelpders.cs` | удалён | Мёртвый код: 0 вхождений в `src`/`tests`/`benchmarks`; класс был свалкой неиспользуемых extension-методов |
| 9 | `TableAlias.Int/Long/Short/String/Float/...` | `GetInt32`/`GetInt64`/`GetInt16`/`GetString`/`GetSingle`/… | Параметр `string _` → `string columnName`. Обновлён `BaseExpressionVisitor` (`nameof(TableAlias.*)`) и примеры в доках |

### P1 — нарушения конвенций именования типов

| # | Имя | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| 10 | `NORM_SQL`, `NORM` | `src/nextorm.core/Query/NORM.cs:7,162` | `NORM_SQL` — snake_case + вложенный тип; `NORM` — аббревиатура CAPS; весь fluent-surface использует snake_case (`count_big`, `avg_distinct`, `row_number`) и C#-keywords (`var`, `all`, `any`, `@in`). Домен — SQL-подобный DSL, поэтому частично оправдано, но публично это нарушение | Переименовать в `SqlFunctions` (или `NORM.Sql`) с PascalCase-методами и таблицей маппинга; публичные поля `*MI`/`SQLExpression` (MethodInfo/ConstantExpression) сделать `internal` |
| 11 | `IEntityMeta`, `IPropertyMeta` | `src/nextorm.core/DataContext/Meta/IEntityMeta.cs:3`, `IPropertyMeta.cs:5` | `Meta` — сокращение; рядом уже `EntityMetadataBuilder` — рассогласование | `IEntityMetadata`, `IPropertyMetadata` |
| 12 | `NORM.NORM_SQL` вложенность `WindowFunction<T>`, `WindowOrder`, `WindowFrame`, `WindowFrameBound`, `WindowFrameType`, `WindowFrameBoundKind` | `NORM.cs:23,63,79,86,96,125` | Публичные вложенные типы в DSL-классе; FQN вида `NORM.WindowFrameType` неочевидны | Вынести в `nextorm.core` как самостоятельные типы (или в `SqlFunctions.*`) |
| 13 | `Param`, `ParamList`, `IParamProvider`, `DefaultParamProvider`, `ParamExpressionVisitor2`, `NORM.Param<T>` | `ParamList.cs:3,8`, `Query/IParamProvider.cs` и др. | `Param` — сокращение `Parameter`; `ParamExpressionVisitor2` — хвостовая цифра-версия | `Parameter`, `ParameterList`; `ParamExpressionVisitor2` → осмысленное имя или `internal` |
| 14 | `TypeExpressionVisitor<T>`, `TwoTypeExpressionVisitor<T1,T2>`, `ReplaceConstantsExpressionVisitor`, `ReplaceConstantVisitor` | `ExpressionExtensions.cs:31,51,81`, `Visitors/ReplaceExpressionVisitor.cs:21` | Несогласованный суффикс (`*Visitor` vs `*ExpressionVisitor`), singular/plural (`Constant` vs `Constants`), «Two» vs цифра | Единый суффикс `ExpressionVisitor`; `ReplaceConstantsExpressionVisitor` |
| 15 | `TestSpecialMethodCallVisitor` | `src/nextorm.core/Visitors/ParamExpressionVisitor.cs:5` | Префикс `Test` в production-типе; публичный | `internal` либо `SpecialMethodCallVisitor` |
| 16 | `Buffer3`, `Buffer10`, `ValueList`, `ValueList3` | `src/nextorm.core/DataContext/ValueList.cs:7,11,74,78` | Магические числа в имени; `ValueList3` не согласован с `ValueList`; реализации внутреннего буфера | `internal` или единая схема `ValueList<T>`/`ValueList3<T>` с документированной арифметикой |
| 17 | `Paging`, `Sorting` (structs) | `Paging.cs:2`, `Sorting.cs:3` | Публичные структуры-детали реализации | `internal` либо оформить как публичный контракт (см. #19) |
| 18 | `SqlBuilder` (public struct) | `src/nextorm.core/DataContext/SqlBuilder.cs:9` | Деталь реализации (диалект, провайдеры); конструктор с 9 параметрами | `internal` |
| 27 | `NORM.PG`, `NORM.MS`, `NORM.CLK` (public nested) | `src/nextorm.core/Query/NORM.PG.cs:15`, `NORM.MS.cs:14`, `NORM.CLK.cs:13` | Провайдер-специфичные DSL-классы названы аббревиатурами из 2–3 букв; углубляют вложенность `NORM.*` | `NORM.Postgres`/`NORM.SqlServer`/`NORM.ClickHouse` (или вынести из `NORM`) |

#### P1, безопасная партия — выполнено 18.09.2026

Переименования публичной поверхности и перевод деталей реализации в `internal` (alpha, без алиасов):

| # | Было | Стало | Комментарий |
|---|------|-------|-------------|
| 11 | `IEntityMeta`, `IPropertyMeta` | `IEntityMetadata`, `IPropertyMetadata` | Файлы и `internal`-реализации `EntityMeta`/`PropertyMeta` → `EntityMetadata`/`PropertyMetadata` |
| 13 | `Param`, `ParamList`, `IParamProvider`, `DefaultParamProvider`, `ParamExpressionVisitor2`, `NORM.Param<T>` | `Parameter`, ~~`ParamList`~~, `IParameterProvider`, `DefaultParameterProvider`, `ParameterBinderVisitor` (`internal`), `NORM.Parameter<T>` | `ParamList` был мёртвым (0 вхождений) — удалён; `ParamExpressionVisitor2` сделан `internal` и переименован. Файл `Query/IParamProvider.cs` → `IParameterProvider.cs` |
| 14 | `TwoTypeExpressionVisitor<T1,T2>`, `ReplaceParameterVisitor`, `ReplaceConstantVisitor` | `TypeExpressionVisitor<T1,T2>`, `ReplaceParameterExpressionVisitor`, `ReplaceConstantExpressionVisitor` | Единый суффикс `ExpressionVisitor` + арность `T1, T2`; `ReplaceConstantsExpressionVisitor` (plural — полная параметризация) сохранён как намеренно отдельный тип |
| 15 | `TestSpecialMethodCallVisitor` (public) | `TestSpecialMethodCallVisitor` (`internal`) | Префикс `Test` больше не протекает в публичную поверхность |
| 16 | `Buffer3`, `Buffer10`, `ValueList`, `ValueList3` (public) | те же имена, `internal` | Магические числа не значимы для потребителя |
| 18 | `SqlBuilder` (public struct) | `SqlBuilder` (`internal`) | Деталь реализации с 9-параметрическим конструктором |

#### P1, DSL и контракт `Paging`/`Sorting` — выполнено 18.09.2026

| # | Было | Стало | Комментарий |
|---|------|-------|-------------|
| 10 | `NORM`, `NORM_SQL` | `SqlFunctions` (FQN `NextORM.Core.SqlFunctions`), `CommonFunctions` (FQN `NextORM.Core.CommonFunctions`) | Типы переименованы; `*MI`/`SQLExpression` — `internal`. Методы DSL **оставлены** snake_case/PascalCase-by-keyword (`count_big`, `@in`, `any`/`all`/`var`) как SQL-зеркало — см. «Отмечено, но менять не рекомендуется» |
| 12 | `NORM.WindowFunction<T>`, `NORM.WindowOrder`, `NORM.WindowFrame`, `NORM.WindowFrameBound`, `NORM.WindowFrameType`, `NORM.WindowFrameBoundKind` | `WindowFunction<T>`, `WindowOrder`, `WindowFrame`, `WindowFrameBound`, `WindowFrameType`, `WindowFrameBoundKind` (top-level) | Вынесены в `Query/WindowFunctions.cs` |
| 27 | `NORM.PG`/`MS`/`CLK` + `NORM.PG_SQL`/`MS_SQL`/`CLK_SQL` | `PostgresFunctions`/`SqlServerFunctions`/`ClickHouseFunctions` + `SqlFunctions.Postgres`/`.SqlServer`/`.ClickHouse` | Пользовательский путь `SqlFunctions.Postgres.any(...)`; `Sql`/`SQL` |
| 17 | `Paging`/`Sorting` (public mutable fields) | `Paging`/`Sorting` (валидирующие свойства) | `internal` невозможен: `ISqlDialect.MakePage(Paging, …)` публичен и реализуется 6 сборками-провайдерами. `Paging.Limit/Offset` валидируют неотрицательность; `Sorting.Direction/PreparedExpression` — свойства (см. #19) |

### P2 — несогласованность публичной поверхности

| # | Имя | Файл:строка | Проблема |
|---|-----|-------------|----------|
| 19 | `Paging.Limit/Offset`, `Sorting.Direction/PreparedExpression` | `Paging.cs:4-5`, `Sorting.cs:6-7` | Публичные **изменяемые поля** вместо свойств; нет валидации, сигнатуру нельзя менять без правки всех вызовов |
| 20 | `SqlTableAttribute` в файле `TableAttribute.cs`; `IColumnsProvider` в файле `ISourceProvider.cs` | `TableAttribute.cs:3`, `Query/ISourceProvider.cs:7` | Несоответствие «файл ↔ тип»; мешает навигации |
| 21 | `IPayload` | `src/nextorm.core/Payload/IPayload.cs:3` | Пустой маркерный интерфейс без документации и без членов |
| 22 | `DbContext`, `IDataContext`, `InMemoryContext`, `DbContextBuilder`, `DataContextOptionsBuilderExtensions` | `DataContext/DbContext.cs`, `DI/DataContextOptionsBuilder.cs:5`, providers `DI/…` | Смешение «DbContext» и «DataContext»/«Context»; builder называется `DbContextBuilder`, а его extensions — `DataContextOptionsBuilderExtensions` |
| 23 | ~~`DbQueryCommandExtension` (sing.) vs `*Extensions`~~ | ✅ закрыто вместе с P0-6: тип стал `internal` и выведен из публичной поверхности |
| 24 | `DefaultColumnsProvider` в `Query/`, `DefaultAliasProvider` в `DataContext/`, `DefaultParamProvider` в `Query/IParamProvider.cs` | | Непоследовательное размещение `Default*`-реализаций |
| 25 | `AnonymousClassEqualityComparer` (public) | `src/nextorm.core.sourcegenerator/AnonymousClassEqualityComparer.cs:6` | Публичный тип генератора; должен быть `internal` |
| 26 | namespace `nextorm.core` (lowercase) | все файлы `src` | Пространства имён .NET должны быть PascalCase (`NextORM.Core`) |
| 28 | `DataContextOptionsBuilderExtensions` | `src/nextorm.{postgres,sqlite,sqlserver,mysql,mariadb,clickhouse}/DI/DataContextOptionsBuilderExtensions.cs` | Одноимённый публичный класс-расширение в 6 провайдерах; при нескольких `using` IntelliSense/`cref` неоднозначны, документация дублируется. Разнести по namespace или дать уникальные имена (`PostgresDataContextOptionsBuilderExtensions` …) |

#### P2 — выполнено 18.09.2026

- #19: `Paging.Limit`/`Offset` — свойства с проверкой неотрицательности; `Sorting.Direction`/`PreparedExpression` — свойства (backing-поля приватные). Object-initializer'ы и `sorting[i].Direction = …` продолжат работать.
- #20: `TableAttribute.cs` → `SqlTableAttribute.cs`; `Query/ISourceProvider.cs` → `Query/IColumnsProvider.cs`.
- #21: `IPayload` удалён (пустой маркер, 0 потребителей); `: IPayload` снят с `CreateEnumeratorPayload`/`CreateMainEnumeratorPayload`.
- #22: унифицировано на `DataContext`: `DbContext` → `DataContext`, `DbContextBuilder` → `DataContextBuilder` (`CreateDbContext` → `CreateDataContext`), провайдерные `*DbContext` → `*DataContext`, `InMemoryContext` → `InMemoryDataContext`. EF-типы (`Microsoft.EntityFrameworkCore.DbContext`, `EFDataContext`, `EFInMemoryDataContext`) не затронуты.
- #24: `DefaultAliasProvider.cs` перенесён в `Query/`; `DefaultParameterProvider` выделен в `Query/DefaultParameterProvider.cs` (рядом с `DefaultColumnsProvider`).
- #25: `AnonymousClassEqualityComparer` → `internal AnonymousClassGenerator` (`AnonymousClassGenerator.cs`).
- #26: `namespace nextorm.core` → `NextORM.Core`, `nextorm.<provider>` → `NextORM.<Provider>`, `nextorm.core.sourcegenerator` → `NextORM.Core.SourceGenerator`; обновлены `using`/квалифицированные ссылки и reflection-строка `NextORM.Core.Projection` в `InMemoryProjectionFactory`.
- #28: классы и файлы переименованы в `{Provider}DataContextOptionsBuilderExtensions` (Postgres/Sqlite/SqlServer/MySql/MariaDb/ClickHouse).

### Отмечено, но менять не рекомендуется

- `Sql*` (`SqlTableAttribute`, `ISqlDialect`, `SqlDialectBase`) — распространённая аббревиатура (как `System.Data.SqlClient`), допустима.
- Методы DSL (`@in`, `var`, `all`, `any`, `count_big`, …) — **оставлены** как есть: это SQL-зеркало, а C#-keywords вынуждены из-за деревьев выражений; перевод в PascalCase требует отдельной таблицы «метод → SQL-токен» и правки всего translator-слоя, который диспетчеризует по `nameof`. Решение осознанное: переименованы только типы (#10).
- `I*` интерфейсы, `*Exception`, `*Attribute`, `*Builder`, `*Options` — суффиксы соблюдены.

## 4. План работ

Проект в стадии **alpha** — обратная совместимость не сохраняется. Все пункты выполняются **прямыми переименованиями на месте**, с одновременным обновлением кода, тестов, примеров и документации в одном изменении.

### Шаг 1 — инвентаризация использований
- Для каждого пункта #1–#26 собрать вхождения старого имени по всему репозиторию:
  `rg -n "\bOldName\b" src test benchmarks docs`.
- Составить таблицу «старое имя → новое имя → где обновить» (код, тесты, примеры, XML-доки, `docs/**` EN, `docs/ru/**`).
- **Критерий:** для каждого старого имени известны все места правки; ничего не «забыто заранее».

### Шаг 2 — переименование кода и тестов (P0 ✅, безопасная партия P1 ✅; DSL/`Paging`/P2 открыты)
- P0 (#1–#9) переименован на месте — см. §3 и §5.1.
- P1 (#10–#18, #27) — см. §3, §5.3 и §5.4.
- P2 (#19–#28) — см. §3 и §5.4.
- **Критерий (выполнен):** build Debug 0/0; core **153**, sqlite **189**, sqlserver **167**, postgres **151**, mysql **31**, mariadb **7**, clickhouse **47**, integration **853 / Failed 0 / Skipped 23**; старых имён в коде (кроме исторических `<remarks>`) не осталось.

### Шаг 3 — namespace (пункт #26) — выполнено
- `nextorm.core` → `NextORM.Core`, `nextorm.<provider>` → `NextORM.<Provider>`, `nextorm.core.sourcegenerator` → `NextORM.Core.SourceGenerator` — прямая замена `namespace`/`using`/квалифицированных ссылок и reflection-строки `NextORM.Core.Projection` (`InMemoryProjectionFactory`).
- **Критерий (выполнен):** build 0/0; все тесты (core + 6 провайдеров + integration) зелёные.

### Шаг 4 — перегенерация документации (обязательно, в том же изменении)
Правило `AGENTS.md`: переименование публичного типа/метода требует обновления доков.
- Обновить XML-`<summary>`/`<remarks>`: текст, `<see cref>`, `<paramref>`, `<typeparamref>`, ссылки на имена типов.
- Обновить `docs/**` (EN) и `docs/ru/**` (RU): код-примеры, проза, пути к файлам, `advanced/api-reference.md` в обеих ветках.
- Проверить оба дерева: `rg -n "\bOldName\b" docs` — пусто для каждого старого имени.
- Перегенерировать сайт DocFX с чистой сборкой:
  `rm -rf docs/api docs/_site docs/xrefmap.yml && dotnet docfx docs/docfx.json`
  — 0 warnings / 0 errors; страницы API пересозданы под новыми именами (`EntityBuilder.yml`, `JoinedEntityBuilder-1.yml`, …), старые yml удалены.
- Нормализовать все `.md` и `.cs` в CRLF (`perl -pi -e 's/\r?\n/\r\n/g' <file>`).
- **Критерий:** `dotnet docfx` 0/0; grep старых имён по `docs/` пуст; CRLF сохранён.

### Шаг 5 — закрепление (ОТКРЫТО, поверхность не заморожена)

Актуализация 18.09.2026: ничего из шага 5 не сделано.
- `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` отсутствуют; `Microsoft.CodeAnalysis.PublicApiAnalyzers` не подключён; `EnablePackageValidation`/ApiCompat и API-approval тест не настроены.
- `CS1591` по-прежнему в `<NoWarn>` **всех 7** библиотечных `.csproj` (`nextorm.core.csproj:9`, `nextorm.postgres.csproj:9`, `nextorm.sqlite.csproj:8`, `nextorm.sqlserver.csproj:17`, `nextorm.mysql.csproj:9`, `nextorm.mariadb.csproj:9`, `nextorm.clickhouse.csproj:9`).
- `CA1716`/`CA1724`/`CA1002`/`CA1051` в `Directory.Build.props` не включены (там только `TreatWarningsAsErrors=true`). Прогон `nextorm.core` при `AnalysisLevel=latest-all` даёт по ним: `CA1716` — 46, `CA1002` — 14, `CA1051` — 46, `CA1724` — 0 (уникальных мест). Правила входят в набор `latest-all` и сборкой по умолчанию не гейтятся.
- Единственное закрепление на сегодня — XML-`<remarks>` со ссылками на этот отчёт в местах находок (P0-1, P0-5, P0-6, P0-9, P1-16, P2-19, P2-25 и др.).
- **Критерий (не выполнен):** build 0/0; новые нарушения именования валят сборку.

## 5. Что сделано

### 5.1. P0-переименования (выполнено 18.09.2026)

- Инвентаризация: для каждого P0-имени собраны вхождения в `src`/`tests`/`benchmarks`/`docs`, составлена таблица «старое → новое».
- Выполнены переименования #1–#9 (см. §3): `HashCode`→`XxHash32`, `IQueryProvider`→`IQueryRegistry`, слияние `IDataContextExtensions` в `DataContextExtensions`, `EntityP2..P8`→`JoinedEntityBuilder<...>`, `PrepareException`→`QueryPreparationException` (+ общая база `DataContextException` для `BuildSqlCommandException`), `DbQueryCommandExtension`→`RawSqlOverride` (`internal`, свойства), `Projection.tN`→`ItemN`, удаление `BasicHelpers`, `TableAlias`→глагольные `Get*`.
- Reflection-точки, зависевшие от имён, обновлены синхронно: `MemberTranslator` (позиция по числовому суффиксу `ItemN`), `AliasFromProjectionVisitor` (`ItemN` → SQL-алиас `tN` через `DefaultAliasProvider.GetAliasName`), `InMemoryDataContext`/`QueryPlanner` (`GetProperty("Item" + dim)` / `"Item1"`).
- Удалены мёртвые тест-файлы `*DELETE*.cs` (заодно закрывают Находку 7 code-smells-review): они ссылались на старые имена и не исполнялись.
- Обновлены `docs/**` (EN) и `docs/ru/**` (RU): примеры join (`p.ItemN`), аксессоры `TableAlias`, `advanced/api-reference.md` (обе ветки), roadmap- и performance-спеки. `rg` по старым именам в `docs/` (кроме этого реестра) — пусто.
- Проверка: build Debug **0/0**; core **151**, sqlite **180**, sqlserver **166**, postgres **150**, mysql **31**, mariadb **7**, clickhouse **47**, integration **833 / Failed 0 / Skipped 23**.

### 5.2. Исходный аудит

- Проведён аудит, отчёт сохранён в `docs/specs/design/API-NAMING-REVIEW.md`.
- На момент аудита переименования **не выполнялись** — только план и отчёт.
- Добавлены XML-`<summary>`/`<remarks>` (и `<param>`/`<typeparam>` где нужно) для публичных типов и членов, отмеченных в находках P0/P1 и части P2:

  | Файл | Задокументировано |
  |------|-------------------|
  | `Expressions/XxHash32.cs` | `XxHash32` |
  | `Query/IQueryRegistry.cs` | `IQueryRegistry` |
  | `Query/ISourceProvider.cs` | `IColumnsProvider` |
  | `Query/IParamProvider.cs` | `IParamProvider`, `DefaultParamProvider` |
  | `Query/NORM.cs` | `NORM`, `NORM.NORM_SQL` |
  | `Query/RawSqlOverride.cs` | тип + свойства (стал `internal`) |
  | `ParamList.cs` | `ParamList`, `Param` |
  | `BasicHelpders.cs` | `BasicHelpers`, `BasicHelpers.Var` |
  | `Builders/EntityBuilder.cs` | `EntityBuilder<TEntity>`, `EntityBuilder` |
  | `Builders/EntityExtensions.cs` | `EntityExtensions` |
  | `Builders/Joins/JoinedEntityBuilder.cs` | `JoinedEntityBuilder<T1..T8>` (стало) |
  | `Builders/Projection.cs` | `Projection<T1..T8>` |
  | `Builders/TableAlias.cs` | `TableAlias`, `TableColumn` |
  | `Builders/Paging.cs` | `Paging` |
  | `Expressions/Sorting.cs` | `Sorting` |
  | `DataContext/SqlBuilder.cs` | `SqlBuilder` |
  | `DataContext/ValueList.cs` | `Buffer10`, `ValueList`, `Buffer3`, `ValueList3`, `ListExtensions` |
  | `DataContext/{DataContextException,BuildSqlCommandException,QueryPreparationException}.cs` | иерархия исключений |
  | `DataContext/Meta/{IEntityMeta,IPropertyMeta,EntityMetadataBuilder,EntityPropertyBuilder}.cs` | metadata-контракты |
  | `DataContext/Cache/*.cs` | `IPreparedQueryCommand`, `PreparedQueryCommand`, `DbPreparedQueryCommand`, `InMemoryCompiledQuery`, `QueryPlan` |
  | `DI/DataContextOptionsBuilder.cs` | `DbContextBuilder` |
  | `ExpressionExtensions.cs` | `ExpressionExtensions`, `TypeExpressionVisitor`, `TwoTypeExpressionVisitor`, `ReplaceConstantsExpressionVisitor`, `PredicateExpressionVisitor` |
  | `Visitors/*.cs` | `BaseExpressionVisitor`, `AliasFromProjectionVisitor`, `CorrelatedQueryExpressionVisitor`, `MemberExpressionVisitor`, `WhereExpressionVisitor`, `TestSpecialMethodCallVisitor`, `ParamExpressionVisitor2`, `ReplaceParameterVisitor`, `ReplaceConstantVisitor` |
  | `nextorm.core.sourcegenerator/AnonymousClassEqualityComparer.cs` | `AnonymousClassEqualityComparer` (найденная MISNOMER: генератор назван «EqualityComparer») |

- Проверка: `dotnet build nextorm.sln` — 0 warnings / 0 errors; `dotnet vstest` — 111 passed; `dotnet docfx docs/docfx.json` — 0 warnings / 0 errors.
- **Актуализация 18.09.2026:** задокументировано 113/158 публичных типов (72 %), 213/1092 методов (20 %), 97/347 свойств (28 %); недокументированных типов — 45 (приложение A). **P0, P1 и P2 выполнены** (см. §5.1, §5.3 и §5.4).

### 5.3. P1, безопасная партия (выполнено 18.09.2026)

- Инвентаризация P1-имён по `src`/`tests`/`benchmarks` с разделением на чисто публичные переименования и перевод деталей реализации в `internal`.
- Выполнены #11, #13, #14, #15, #16, #18 (см. таблицу «Было → Стало» в §3):
  - #11: `IEntityMeta`/`IPropertyMeta` → `IEntityMetadata`/`IPropertyMetadata`; файлы и `internal`-реализации `EntityMeta`/`PropertyMeta` → `EntityMetadata`/`PropertyMetadata`.
  - #13: `Param` → `Parameter` (+ новый файл `Parameter.cs`), `ParamList` удалён как мёртвый (0 вхождений), `IParamProvider`/`DefaultParamProvider` → `IParameterProvider`/`DefaultParameterProvider` (файл `Query/IParamProvider.cs` → `IParameterProvider.cs`), `NORM.Param<T>` → `NORM.Parameter<T>`, `ParamExpressionVisitor2` → `internal ParameterBinderVisitor` (файл `Visitors/ParamExpressionVisitor.cs` → `ParameterVisitors.cs`).
  - #14: `TwoTypeExpressionVisitor<T1,T2>` → `TypeExpressionVisitor<T1,T2>` (арность вместо «Two»), `ReplaceParameterVisitor`/`ReplaceConstantVisitor` → `ReplaceParameterExpressionVisitor`/`ReplaceConstantExpressionVisitor`; `ReplaceConstantsExpressionVisitor` оставлен (намеренно отдельный проход полной параметризации).
  - #15/#16/#18: `TestSpecialMethodCallVisitor`, `Buffer3`/`Buffer10`/`ValueList`/`ValueList3`, `SqlBuilder` переведены в `internal`.
- Reflection/`nameof`-точки, зависевшие от имён: `nameof(NORM.Param)` → `nameof(NORM.Parameter)` в `TypedParamVisitor`/`NormSqlTranslator`/`ParameterVisitors`; `node.Method.Name` сравнивается с новым именем.
- Обновлены `docs/**` (EN) и `docs/ru/**` (RU): `NORM.Param` → `NORM.Parameter` (16 файлов), `IEntityMeta` → `IEntityMetadata`, а также профильные спеки (`plan-*`, `design/*`, `performance/*`).
- Проверка: build Debug **0/0**; core **151**, sqlite **180**, sqlserver **166**, postgres **150**, mysql **31**, mariadb **7**, clickhouse **47**, integration **833 / Failed 0 / Skipped 23**.

### 5.4. P1 DSL/контракт и весь P2 (выполнено 18.09.2026)

- #10/#12/#27: `NORM` → `SqlFunctions`, `NORM_SQL` → `CommonFunctions`; провайдерные `PG`/`MS`/`CLK` → `PostgresFunctions`/`SqlServerFunctions`/`ClickHouseFunctions`; аксессоры `SqlFunctions.Sql`/`.Postgres`/`.SqlServer`/`.ClickHouse`; `WindowFunction<T>`/`WindowOrder`/`WindowFrame`/`WindowFrameBound`/`WindowFrameType`/`WindowFrameBoundKind` вынесены в `Query/WindowFunctions.cs`; `*MI`/`SQLExpression` → `internal`. Методы DSL оставлены (SQL-зеркало; см. §3 «Отмечено»).
- #17/#19: `Paging.Limit`/`Offset` — свойства с проверкой неотрицательности; `Sorting.Direction`/`PreparedExpression` — свойства.
- #20: `TableAttribute.cs` → `SqlTableAttribute.cs`; `Query/ISourceProvider.cs` → `Query/IColumnsProvider.cs`.
- #21: `IPayload` удалён.
- #22: унификация на `DataContext`: база `DataContext`, провайдерные `*DataContext`, `DataContextBuilder` (+`CreateDataContext`), `InMemoryDataContext`. EF-типы (`Microsoft.EntityFrameworkCore.DbContext`, `EFDataContext`, `EFInMemoryDataContext`) не тронуты.
- #24/#25/#28: `DefaultAliasProvider`/`DefaultParameterProvider`/`DefaultColumnsProvider` в `Query/`; `AnonymousClassEqualityComparer` → `internal AnonymousClassGenerator`; `{Provider}DataContextOptionsBuilderExtensions`.
- #26: namespaces → `NextORM.Core` / `NextORM.<Provider>` / `NextORM.Core.SourceGenerator` (включая reflection-строку `NextORM.Core.Projection`).
- Обновлены `docs/**` (EN) и `docs/ru/**` (RU), `advanced/api-reference.md` в обеих ветках, этот реестр; CRLF сохранён.
- Дополнительно (god-class work, 18.09.2026): новые `internal`-типы `SqlBuildContext`, `SqlSourceRenderer`, `StringFunctionTranslator`, `MathFunctionTranslator`, `DateTimeFunctionTranslator`, `TableAliasAccessors`; новый **публичный** `NextORM.Core.EntityBuilderExtensions` — туда переехали терминальные операторы `EntityBuilder<TEntity>` (`Any`/`ToList`/`First`/`Single`/`Last`/`Count`/агрегаты/`To*`/`Prepare`/`AnyCommand`/`*Or*Command`). Вызовы не изменились (extension-методы), но потребителю нужен `using NextORM.Core;` (он и так необходим для `From<T>()`/`SqlFunctions`). Добавлены регресс-тесты `BackendMixingTests` (F3).
- Дополнительно (параметры, 18.09.2026): длинные списки параметров ≥6 разобраны параметр-объектами. Новые **публичные** типы: `QueryDefinition` (ctor-ы `QueryCommand`/`QueryCommand<TResult>` и `DataContextExtensions.CreateCommand<T>` теперь принимают его), `PrepareFromSqlMode` (флаги вместо пары `nonStreamUsing`/`storeInCache` в `PrepareFromSql`), `PreparedCommandOptions` (ctor `DbPreparedQueryCommand<TResult>`). Удалены длинные публичные overload'ы: 12-параметровый ctor `BaseExpressionVisitor`, 10-параметровый primary ctor `WhereExpressionVisitor`, публичные ctors `QueryCommand`/`QueryCommand<TResult>` (кроме `(IDataContext?, QueryDefinition)`), 4 перегрузки `CreateCommand`, а также мёртвый `InMemoryDataContext.PrepareFromSql` (in-memory raw SQL теперь бросает `NotSupportedException`). Внутренние новые типы: `SqlBuildContext`, `FromRenderOptions`, `AggregateTypeInfo`, `LoggingOptions`, `ProviderHooks`, `ConnectionHooks`.
- Проверка: build Debug **0/0**; core **153**, sqlite **195**, sqlserver **167**, postgres **151**, mysql **31**, mariadb **7**, clickhouse **47**; integration — провайдерные тесты зелёные, 22 падения только в `Correlated*`/`SubQuerySelectOrderSingle` (feature-работа над scalar-subquery cardinality, не связана с рефакторингом параметров); DocFX 0 errors.
- Бенчмарки (A/B против `d21c473`, `SqliteBenchmarkJoin`/`Where`): `Prepared`-пути по аллокациям идентичны (13.01/92.42/107.64 KB); в `Cached`-путях +~555 B/запрос (Join 94.05→99.6 KB, Where 535.8→591.3 KB) — это код параллельно влившегося рефакторинга (correlated-subquery `OuterReferences` в `QueryPlanEqualityComparer`/`QueryCommand.Clone`, выкачка `InMemory*`), а не переименований P0/P1/P2: откат `Paging`/`Sorting` к полям аллокации не изменил. `Paging`/`Sorting`, DSL- и namespace-переименования аллопрофиль не меняют.


## Приложение A — публичные типы без XML-документации (45)

Полный машинный список (FQN, включая вложенные публичные типы), получен отражением по 7 собранным библиотечным сборкам (HEAD `d21c473`). Использовать как бэклог Шага 4. Generic-арность записана в метаданных (`` `1 `` = один generic-параметр). Прошлый список из 109 позиций устарел — после массового добавления `<summary>`/`<remarks>` он сократился до 45.

```
nextorm.clickhouse.ClickHouseDbContext
nextorm.clickhouse.DataContextOptionsBuilderExtensions
nextorm.core.DbContext
nextorm.core.DefaultAliasProvider
nextorm.core.DefaultColumnsProvider
nextorm.core.ExpressionCache`1
nextorm.core.ExpressionKey
nextorm.core.ExpressionPlanEqualityComparer
nextorm.core.FromExpression
nextorm.core.FromExpressionPlanEqualityComparer
nextorm.core.IAliasProvider
nextorm.core.IEqualityComparerExtensions
nextorm.core.InMemoryCommandBuilderExtensions
nextorm.core.InMemoryContext
nextorm.core.InMemoryEnumerator`2
nextorm.core.InMemoryEnumeratorAdapter`2
nextorm.core.InMemoryPreparedQueryCommand`1
nextorm.core.IPayload
nextorm.core.IValueEqualityComparer`1
nextorm.core.JoinExpression
nextorm.core.JoinExpressionPlanEqualityComparer
nextorm.core.JoinType
nextorm.core.MemberInfoExtensions
nextorm.core.OrderDirection
nextorm.core.OuterRefMarker`1
nextorm.core.QueryCommand
nextorm.core.QueryCommand`1
nextorm.core.QueryCommandExtensions
nextorm.core.QueryPlanEqualityComparer
nextorm.core.ResultSetEnumerator`1
nextorm.core.SelectExpression
nextorm.core.SelectExpressionPlanEqualityComparer
nextorm.core.SortingExpressionPlanEqualityComparer
nextorm.core.SqlTableAttribute
nextorm.core.TypeExtensions
nextorm.mariadb.DataContextOptionsBuilderExtensions
nextorm.mariadb.MariaDbContext
nextorm.mysql.DataContextOptionsBuilderExtensions
nextorm.mysql.MySqlDbContext
nextorm.postgres.DataContextOptionsBuilderExtensions
nextorm.postgres.PostgresDbContext
nextorm.sqlite.DataContextOptionsBuilderExtensions
nextorm.sqlite.SqliteDbContext
nextorm.sqlserver.DataContextOptionsBuilderExtensions
nextorm.sqlserver.SqlServerDbContext
```
nextorm.core.sourcegenerator/AnonymousClassEqualityComparer.cs:6   AnonymousClassEqualityComparer
nextorm.core/BuildSqlCommandException.cs:6                       BuildSqlCommandException
nextorm.core/Builders/EntityBuilder.cs:16                        EntityBuilder<TEntity>
nextorm.core/Builders/EntityBuilder.cs:531                       EntityBuilder
nextorm.core/Builders/EntityExtensions.cs:3                      EntityExtensions
nextorm.core/Builders/InMemoryCommandBuilder.cs:31               InMemoryCommandBuilderExtensions
nextorm.core/Builders/Joins/JoinedEntityBuilder.cs:13,...         JoinedEntityBuilder<T1..T8>
nextorm.core/Builders/Paging.cs:2                                Paging
nextorm.core/Builders/Projection.cs:28..162                      Projection<T1..T8>
nextorm.core/Builders/TableAlias.cs:4,37                         TableAlias, TableColumn
nextorm.core/DI/DataContextOptionsBuilder.cs:5                   DbContextBuilder
nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs:7       DbPreparedQueryCommand<T>
nextorm.core/DataContext/Cache/IPreparedQueryCommand.cs:5        IPreparedQueryCommand<T>
nextorm.core/DataContext/Cache/InMemoryCompiledQuery.cs:4        InMemoryCompiledQuery<,>
nextorm.core/DataContext/Cache/PreparedQueryCommand.cs:2         PreparedQueryCommand<,>
nextorm.core/DataContext/Cache/QueryPlan.cs:5                    QueryPlan
nextorm.core/DataContext/DataContextException.cs:6               DataContextException
nextorm.core/DataContext/DbContext.cs:13                         DbContext
nextorm.core/DataContext/DefaultAliasProvider.cs:7               DefaultAliasProvider
nextorm.core/DataContext/InMemoryDataContext.cs:9                InMemoryContext
nextorm.core/DataContext/InMemoryEnumerator.cs:7                 InMemoryEnumerator<,>
nextorm.core/DataContext/InMemoryEnumeratorAdapter.cs:4          InMemoryEnumeratorAdapter<,>
nextorm.core/DataContext/InMemoryPreparedQueryCommand.cs:16      InMemoryPreparedQueryCommand<>
nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:7         EntityMetadataBuilder<T>
nextorm.core/DataContext/Meta/EntityPropertyBuilder.cs:6         EntityPropertyBuilder<T>
nextorm.core/DataContext/Meta/IEntityMeta.cs:3                   IEntityMeta
nextorm.core/DataContext/Meta/IPropertyMeta.cs:5                 IPropertyMeta
nextorm.core/DataContext/QueryPreparationException.cs:6                   QueryPreparationException
nextorm.core/DataContext/ResultSetEnumerator.cs:10               ResultSetEnumerator<>
nextorm.core/DataContext/SqlBuilder.cs:9                         SqlBuilder
nextorm.core/DataContext/ValueList.cs:7,11,74,78,141             Buffer10, ValueList, Buffer3, ValueList3, ListExtensions
nextorm.core/ExpressionCache.cs:5,10                             ExpressionCache<T>, ExpressionKey
nextorm.core/ExpressionExtensions.cs:7,31,51,81,148              ExpressionExtensions, TypeExpressionVisitor, TwoTypeExpressionVisitor, ReplaceConstantsExpressionVisitor, PredicateExpressionVisitor
nextorm.core/Expressions/FromExpression.cs:3                     FromExpression
nextorm.core/Expressions/FromExpressionPlanEqualityComparer.cs:4 FromExpressionPlanEqualityComparer
nextorm.core/Expressions/XxHash32.cs:12                          XxHash32
nextorm.core/Expressions/IEqualityComparerExtensions.cs:5        IEqualityComparerExtensions
nextorm.core/Expressions/JoinExpression.cs:5,15                  JoinType, JoinExpression
nextorm.core/Expressions/JoinExpressionPlanEqualityComparer.cs:5 JoinExpressionPlanEqualityComparer
nextorm.core/Expressions/OrderDirection.cs:3                     OrderDirection
nextorm.core/Expressions/SelectExpression.cs:8                   SelectExpression
nextorm.core/Expressions/SelectExpressionPlanEqualityComparer.cs:4 SelectExpressionPlanEqualityComparer
nextorm.core/Expressions/Sorting.cs:3                            Sorting
nextorm.core/Expressions/SortingExpressionPlanEqualityComparer.cs:3,59 SortingExpressionPlanEqualityComparer, IValueEqualityComparer<T>
nextorm.core/MemberInfoExtensions.cs:5                           MemberInfoExtensions
nextorm.core/OuterRefMarker.cs:5                                 OuterRefMarker<T>
nextorm.core/ParamList.cs:3,8                                    ParamList, Param
nextorm.core/Payload/IPayload.cs:3                               IPayload
nextorm.core/Query/DefaultColumnsProvider.cs:7                   DefaultColumnsProvider
nextorm.core/Query/ExpressionPlanEqualityComparer.cs:11          ExpressionPlanEqualityComparer
nextorm.core/Query/IAliasProvider.cs:5                           IAliasProvider
nextorm.core/Query/IParamProvider.cs:5,9                         IParamProvider, DefaultParamProvider
nextorm.core/Query/IQueryRegistry.cs:5                           IQueryRegistry
nextorm.core/Query/ISourceProvider.cs:7                          IColumnsProvider
nextorm.core/Query/NORM.cs:7,162                                 NORM, NORM_SQL (+ nested Window*)
nextorm.core/Query/QueryCommand.*.cs                             QueryCommand (partials), QueryCommand<TResult>
nextorm.core/Query/QueryCommandExtensions.cs:5                   QueryCommandExtensions
nextorm.core/Query/QueryPlanEqualityComparer.cs:6                QueryPlanEqualityComparer
nextorm.core/SqlFunctionAttribute.cs:14                          SqlFunctionAttribute
nextorm.core/SqlTableFunctionAttribute.cs:17                     SqlTableFunctionAttribute
nextorm.core/TableAttribute.cs:3                                 SqlTableAttribute
nextorm.core/TypeExtensions.cs:6                                 TypeExtensions
nextorm.core/Visitors/*.cs                                       AliasFromProjectionVisitor, BaseExpressionVisitor, CorrelatedQueryExpressionVisitor, MemberExpressionVisitor, TestSpecialMethodCallVisitor, ParamExpressionVisitor2, ReplaceParameterVisitor, ReplaceConstantVisitor, WhereExpressionVisitor
nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs:6     DataContextOptionsBuilderExtensions
nextorm.postgres/PostgresDbContext.cs:7                          PostgresDbContext
nextorm.postgres/PostgresDialect.cs                              PostgresDialect
nextorm.sqlite/...                                               SqliteDbContext, SqliteDialect, DataContextOptionsBuilderExtensions
nextorm.sqlserver/...                                            SqlServerDbContext, SqlServerDialect, DataContextOptionsBuilderExtensions
```
