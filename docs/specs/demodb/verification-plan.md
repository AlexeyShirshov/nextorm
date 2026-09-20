# План сверки: прямой SQL ↔ nextorm

Цель: для каждого из 15 запросов из `docs/specs/*-demodb/` доказать, что **результат запроса
nextorm совпадает с результатом прямого SQL** на одних и тех же данных. Это одновременно
end-to-end проверка соответствия (semantic parity) и способ ловить регрессии API/транслятора.

## 1. Что именно сравниваем

Для каждого запроса фиксируются три артефакта:

1. **Эталон (direct SQL)** — текст из `docs/specs/*-demodb/*.sql`, выполненный «как есть» тем же
   ADO.NET-провайдером (`Npgsql` / `Microsoft.Data.SqlClient` / `ClickHouse.Driver`), что использует
   nextorm. Результат материализуется в `List<object?[]>` без ORM.
2. **Испытуемый (nextorm)** — LINQ-модель из `postgres-aviasales.md` /
   `mssql-adventureworks.md` / `clickhouse-analytics.md`, выполненная через `PostgresDataContext` /
   `SqlServerDataContext` / `ClickHouseDataContext` и материализованная в тот же табличный вид.
3. **Diff-отчёт** — нормализованные наборы строк + вердикт.

Для **raw**-позиций (ClickHouse, `PIVOT`) испытуемый выполняется через `WithSql`/`PrepareFromSql`
с тем же текстом и типизированной проекцией; это проверяет row mapping и совпадение результата, а
отсутствие штатного API фиксируется ссылкой на роадмап.

## 2. Где живёт тест

Новый, **опциональный** провайдер в существующей инфраструктуре интеграционных тестов
(`tests/nextorm.integration.tests`), по образцу `PostgresTestProvider`/`SqlServerTestProvider`/
`ClickHouseTestProvider`:

```
tests/nextorm.integration.tests/
  Providers/DemoDbContainer.cs            // контейнеры + загрузка датасетов
  Demodb/Sql/                            // копии/воркспейс-ссылки на docs/specs/*-demodb/*.sql
  Demodb/PostgresEntities.cs
  Demodb/AdventureWorksEntities.cs
  Demodb/ClickHouseEntities.cs
  Demodb/PostgresDemodbTests.cs          // [Trait("demodb","postgres")]
  Demodb/AdventureWorksDemodbTests.cs    // [Trait("demodb","mssql")]
  Demodb/ClickHouseDemodbTests.cs        // [Trait("demodb","clickhouse")]
  Demodb/ResultComparer.cs               // общий компаратор
```

Тесты **не** входят в общий `CommonTestSuite` (там одна схема/сид для всех провайдеров) и по
умолчанию **скипаются**: запускаются только при доступном контейнерном рантайме и/или заданных
переменных подключения. Это не должно утяжелять обычный CI (тяжёлые образы + многогигабайтные
датасеты).

## 3. Подготовка данных

### 3.1 PostgreSQL: демо-БД «Авиаперевозки»

* Образ: `postgres:17-alpine` (как в `PostgresContainer`).
* Датасет: официальная демо-БД PostgreSQL Pro, <https://postgrespro.ru/education/demodb>, лицензия
  MIT. Артефакт **пиннится** (URL + SHA-256) и кладётся в тестовые ресурсы; загрузка — через
  `.WithResourceMapping(path, "/docker-entrypoint-initdb.d/")` (init-скрипты) или копирование в
  контейнер и `psql -f`. Внутри архива — SQL-скрипт `pg_dump`, создающий БД `demo` (схема
  `bookings`).
* **Актуальная версия — `demo-20250901-3m.sql.gz`** (архив ~133 МБ / 139 299 795 байт, БД ~1,3 ГБ;
  варианты `6m`/`1y`/`2y` — на больших объёмах). Она требует PostgreSQL 15+ и содержит схему
  2025-09-01: `airplanes`/`airports` (view) + `airplanes_data`/`airports_data` (jsonb),
  `flights` + `routes` (темпоральный ключ), `segments.price`, представление `timetable`.
  Демо-запросы в `postgres-demodb/` **уже приведены к этой схеме** и статически проверены по DDL
  дампа (парсинг + существование всех таблиц/колонок).
* **Язык переводов.** В дампе `ALTER DATABASE demo SET "bookings.lang" TO 'en'`. Запросы читают
  `model ->> 'ru'`/`city ->> 'ru'` напрямую из `airplanes_data`/`airports_data`, поэтому
  `bookings.lang` не влияет. Если используете представления `airplanes`/`airports` (текст уже
  локализован), выполните `SET bookings.lang = 'ru'` или в connection string Npgsql
  `Options=-c bookings.lang=ru`.
* Seeding не нужен: дамп самодостаточен; `search_path` не важен, т.к. запросы используют
  `bookings.*` (в дампе он выставлен как `bookings, "$user", public`). Дамп создаёт БД `demo`,
  поэтому контекст/строку подключения надо нацелить на `Database=demo`, а не на `postgres`.
* Проверка готовности: `select count(*) from bookings.flights` (и `select bookings.version()`)
  до старта тестов.

### 3.2 SQL Server: AdventureWorks 2022

* Образ: `mcr.microsoft.com/mssql/server:2022-latest`.
* Датасет: `AdventureWorks2022.bak` (официальный релиз Microsoft), пиннится по SHA-256.
* Загрузка: скопировать `.bak` в контейнер и выполнить
  `/opt/mssql-tools18/bin/sqlcmd -Q "RESTORE DATABASE AdventureWorks FROM DISK = '/tmp/AdventureWorks2022.bak' WITH MOVE ..."`.
* Важно: `datetrunc` (SQL Server 2022) нужен для `date_trunc`; другие версии образа не подходят.
* **Сверка схемы.** Запросы в `mssql-demodb/` проверены по официальному install-скрипту
  `instawdb.sql` (`microsoft/sql-server-samples`, `samples/databases/adventure-works/oltp-install-script`,
  Updated 2025-11-14) — все ссылки «таблица.колонка» существуют. Исправлены две ошибки:
  `Purchasing.Vendor` не имеет колонки `VendorID` (PK — `BusinessEntityID`), а у
  `Production.WorkOrder` нет `ScheduledEndDate` (плановое окончание — `DueDate`).
* `Production.WorkOrder.EndDate` — nullable; в запросе `WHERE EndDate > DueDate` отсекает NULL,
  но сущность `IWorkOrder.EndDate` объявлена `DateTime?`.

### 3.3 ClickHouse: `hits_v1` + `daily_unique_users_mv`

* Образ: `clickhouse/clickhouse-server:25.8-alpine` (как в `ClickHouseContainer`), `TZ=UTC`.
* `datasets.hits_v1`: официальный сэмпл Яндекс.Метрики — «Обезличенная веб-аналитика»
  (<https://clickhouse.com/docs/ru/get-started/sample-datasets/anon-web-analytics-metrica>).
  Пиннутый артефакт: `https://datasets.clickhouse.com/hits/tsv/hits_v1.tsv.xz`, распакованный
  `hits_v1.tsv` — MD5 `f3631b6295bf06989c1437491f7592cb`, ~8 873 898 строк. DDL таблицы (в т.ч.
  `ENGINE = MergeTree() PARTITION BY toYYYYMM(EventDate)`) берётся со страницы-источника; загрузка —
  `clickhouse-client --query "INSERT INTO datasets.hits_v1 FORMAT TSV"` (или HTTP).
* `datasets.visits_v1` (там же) в демо-запросах не используется, но загружается для полноты
  стенда.
* `datasets.daily_unique_users_mv` **не входит** в публичный датасет и создаётся фикстурой:
  `CREATE TABLE ... ENGINE = AggregatingMergeTree` + `CREATE MATERIALIZED VIEW ... AS SELECT EventDate,
  uniqState(UserID) ... GROUP BY EventDate` и разовая загрузка из `hits_v1`. Это делает
  `uniqMerge`-запрос воспроизводимым.
* Ограничение: `hits_v1` большой; для CI берётся срез (например, март 2014) — важен только
  результат на выбранном окне, но **оба** плеча (SQL и nextorm) должны видеть одни данные.

### 3.4 Внешняя БД вместо контейнера

Для каждой СУБД поддерживается переменная окружения, аналогично существующей инфраструктуре
(`NEXTORM_POSTGRES_CONNECTION` и т.п.). Если демо-БД уже развёрнута, тесты идут против неё:

```
NEXTORM_DEMODB_POSTGRES_CONNECTION   # база с загруженным demo dump (schema bookings)
NEXTORM_DEMODB_MSSQL_CONNECTION      # база AdventureWorks
NEXTORM_DEMODB_CLICKHOUSE_CONNECTION # база с datasets.hits_v1 и daily_unique_users_mv
```

`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` — стандартный
способ для локального запуска (см. `.opencode/skills/running-integration-tests`).

## 4. Выполнение запросов

### 4.1 Эталон

Читаем `.sql`, убираем оконные комментарии-заголовки (по желанию), выполняем на `DbConnection`:

```csharp
static async Task<List<object?[]>> RunDirectAsync(string connectionString, string sql)
{
    await using var conn = new NpgsqlConnection(connectionString);   // или SqlConnection/ClickHouseConnection
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    await using var reader = await cmd.ExecuteReaderAsync();
    var rows = new List<object?[]>();
    while (await reader.ReadAsync())
    {
        var values = new object?[reader.FieldCount];
        reader.GetValues(values);
        rows.Add(values);
    }
    return rows;
}
```

Колонки эталона берём из `reader.GetName(i)` (порядок и подписи как в исходном SQL).

### 4.2 Испытуемый (nextorm)

```csharp
await using var ctx = new PostgresDataContext(connectionString, new DataContextBuilder());
var rows = await QueryAircraftDelayChains(ctx);   // вызов LINQ-модели
```

Приведение к табличному виду: рефлексия по проекции (или явный маппер на каждый запрос) в
`object?[]` с фиксированным порядком колонок, соответствующим эталону.

### 4.3 Сырой SQL как испытуемый

Для raw-позиций используется `WithSql` с тем же текстом (при необходимости — с приведением
массивов/кортежей к строке) и DTO из `clickhouse-analytics.md`. Это проверяет, что nextorm
корректно исполняет и маппит запрос; сам SQL при этом не сравнивается, поэтому такой тест
дополнительно помечается `Skip`-метаданными «API gap: <ссылка на todo>».

## 5. Компаратор результатов

`ResultComparer` сравнивает нормализованные наборы:

```csharp
public static void AssertEquivalent(
    IReadOnlyList<string> columnNames,
    IReadOnlyList<object?[]> expected,   // direct SQL
    IReadOnlyList<object?[]> actual,     // nextorm
    ComparisonOptions options);
```

### 5.1 Нормализация

| Аспект | Правило |
|---|---|
| Порядок строк | Оба набора сортируются по **ключу сортировки запроса** (см. §6). Если у запроса нет детерминированного `ORDER BY`, ключ задаётся явно в обоих плечах (доп. `ORDER BY` добавляется и к эталону). |
| `NULL` | Единый sentinel (`DBNull`/`null`) — не «пустая строка». |
| Даты/время | Приводятся к UTC и к одному разрешению. `EventTime`/`timestamp` — как `DateTime` (`DateTimeKind.Utc`). |
| Числа | `decimal` сравнивается с масштабом до 1e-9; `double`/`float` — относительный допуск `1e-6`. Целые — точно. |
| Строки | Ordinal-сравнение (без учёта культуры); для проверки регистра — по договорённости. |
| JSON | `->>`/тестовое значение — как строка; для jsonb-объектов — канонизация ключей. |

### 5.2 Критерий совпадения

* совпадает число колонок и их имена (регистронезависимо, если провайдер не сохраняет регистр);
* совпадает число строк;
* для каждой пары строк все значения равны по правилам §5.1.

При расхождении в отчёт пишутся: индекс первой несовпавшей строки, имя колонки, оба значения,
и до 20 строк из обеих выборок.

### 5.3 Детерминированность

Сортировка/тай-брейки обязательны: SQL не гарантирует порядок групп. Для запросов без полного
`ORDER BY` (например, `business_occupancy_matrix` сортируется по `model`, а `passenger_stats` —
по `wasted DESC, passenger_name`) в эталон **добавляется** тот же полный ключ, что и в nextorm.
ClickHouse-функции `uniq*`/`quantile*` в сыром SQL и в nextorm должны быть одинаковыми; для
приблизительных (`uniq` без `Exact`) допускается сравнение на одном и том же сервере без
дополнительного допуска — обе стороны видят тот же ответ (проверка повторяется, чтобы исключить
нестабильность).

## 6. Матрица запросов

| # | Запрос | Ключ сортировки (полный) | Числовая политика | Замечание |
|---|---|---|---|---|
| PG-1 | aircraft_delay_chains | `airplane_code, scheduled_departure` | задержки `1e-9` | `date_diff('milliseconds')/60000.0` vs `EXTRACT(EPOCH)/60`: возможен граничный случай ровно 30/15 мин |
| PG-2 | business_occupancy_matrix | `model` | проценты `decimal`, 1 знак | `AVG(CASE)` = `avg(...) filter (where ...)` |
| PG-3 | passenger_noshow_analysis | `wasted DESC, passenger_name` | деньги точно (`decimal`) | фильтр по агрегатам |
| PG-4 | rolling_revenue_metrics | `sales_date DESC` | деньги/средние `1e-2` (ROUND 2) | фреймы `ROWS` |
| PG-5 | route_network_abc_xyz | `total_revenue DESC` | выручка `1e-2`, СКО `1e-6` | `stdev` = `STDDEV` (sample) |
| MS-1 | vip_churn | `LifetimeValue DESC` | деньги `1e-2` | `FORMAT` через UDF |
| MS-2 | rolling_kpi | `region, month DESC` | деньги `1e-2` | `datetrunc` (2022) |
| MS-3 | supply_chain | `ProdDelayDays DESC, DelayDays DESC` | целые дни | `date_add`/`date_diff` |
| MS-4 | product_abc_xyz | `TotalRevenue DESC` | выручка `1e-2`, СКО `1e-6` | `CAST(qty AS FLOAT)` |
| MS-5 | quarterly_pivot | `category` | маржа `1e-2`, `NULL` ≠ 0 | `PIVOT` → условный `SUM(CASE)` |
| CH-1 | array_analytics | `occurrence DESC` | счётчики точно | raw; массив → строка |
| CH-2 | funnel | `level ASC` | счётчики точно | raw; `windowFunnel` |
| CH-3 | incremental | `EventDate DESC` | счётчики точно | raw; `uniqMerge` |
| CH-4 | retention | `cohort_week DESC` | проценты `1e-2` | raw; матрица → строка |
| CH-5 | sessions | `hits DESC` | счётчики точно | `lag`/`sum_over` вместо `lagInFrame`/`runningAccumulate`; тай-брейк при равном `EventTime` |

## 7. Пограничные семантики (обязательно проверить)

1. **Усечение времени** (`date_diff('second')` vs `EXTRACT(EPOCH)`): PG-1. Если на демо-данных
   расхождение по границе появится — сузить до `milliseconds` (уже сделано в примере) или
   сравнивать с допуском ≤ 1 строки, зафиксировав это в тесте.
2. **Округление**: `ROUND` на SQL — «half away from zero»; `Math.Round` в .NET по умолчанию — к
   чётному. В примерах округление делается на стороне SQL (`Math.Round` транслируется в `round(...)`)
   либо через `MidpointRounding.AwayFromZero` в DTO (MS-5).
3. **`NULL` vs `0` в PIVOT**: условная агрегация должна возвращать `NULL` при отсутствии строк
   (тернарник `… ? margin : (decimal?)null`), иначе `0`.
4. **Порядок `NULL`** в `ORDER BY` различается (PostgreSQL — `NULL` первыми при `DESC`).
   Ключ сортировки должен однозначно определять порядок; спорные группы — исключать/дополнять
   тай-брейком.
5. **Деления**: целочисленное деление (`date_diff('day') / 7`) vs дробное — привести тип до деления
   (`/ 7.0`) там, где оригинал даёт нецелый результат.
6. **JSON**: `->>` даёт текст; при сравнении jsonb-объектов канонизировать (сортировка ключей).
7. **Массивы/кортежи ClickHouse**: материализуются только после приведения к строке; иначе тест
   пропускается с пометкой «row reader for arrays».
8. **Усечение/тай-зона**: `TZ=UTC` в контейнере, даты — без локальной конвертации.
9. **Приблизительные агрегаты ClickHouse**: `uniq`/`quantile` — детерминированы на одном сервере,
   но зависят от порядка вставки; прогонять обе стороны на одном и том же наборе.

## 8. Что считается «зелёным»

```
[PASS] PG-1 aircraft_delay_chains        rows: 142 = 142
[PASS] PG-2 business_occupancy_matrix    rows: 9 = 9
...
[GAP ] CH-2 funnel                       nextorm API gap (windowFunnel) -> WithSql, resultado equal
```

* **PASS** — совпадение по §5.2.
* **GAP** — штатного API нет; тест через `WithSql` проходит, запись добавляется в todo-роадмап.
* **FAIL** — расхождение; артефакт `TestResults/demodb/<provider>/<query>.diff.txt` с обеими
  выборками.

## 9. CI и ресурсы

* По умолчанию демо-тесты **скипаются** (как интеграционные без `DOCKER_HOST`), чтобы не тянуть
  AdventureWorks/`hits_v1` на каждый push.
* Отдельный (опциональный) job с меткой `demodb`, запускается вручную (`workflow_dispatch`) или по
  расписанию; образы и датасеты кэшируются.
* Для лёгкого контроля регрессий **дополнительно** добавляются SQL-generation тесты (без БД,
  как `SqlGenerationTests`) для тех запросов, что выражаются штатно: снапшот сгенерированного SQL
  ловит изменение транслятора в обычном CI.

## 10. Порядок внедрения

1. **Фаза 1.** PG-1…PG-5 и MS-1…MS-5: контейнеры + дампы + компаратор; параллельно — SQL-gen
   снапшоты для штатных запросов. Ожидаем PASS (MS-5 — с оговоркой `PIVOT`).
2. **Фаза 2.** CH-5 (sessions) на реальном `hits_v1`; CH-1…CH-4 — через `WithSql` с пометкой GAP,
   с занесением в `todo_clickhouse.md`.
3. **Фаза 3 (по мере закрытия пробелов).** Перевести CH-1/CH-2/CH-4 на LINQ по мере реализации
   `arrayMap`/`arrayFilter`, `windowFunnel`, `groupArray`/row reader массивов; снять пометку GAP.
4. **Фаза 4.** Встроить опциональный job в CI и зафиксировать эталонные снапшоты результатов
   (для быстрого диффа без реальных БД — если понадобится).

## 11. Критерий готовности

* Все 15 запросов имеют тест: PASS либо GAP+`WithSql`.
* Для каждого GAP есть ссылка на конкретный пункт `docs/specs/roadmap/todo_*.md`.
* Diff-отчёт сохраняется как артефакт; при FAIL — тест падает с понятным сообщением.
* Документация EN+RU при добавлении/переименовании публичного API обновляется в том же изменении
  (AGENTS.md).
