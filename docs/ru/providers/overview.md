# Обзор провайдеров

> Провайдер предоставляет диалект (правила SQL-текста) и подкласс [`DataContext`](xref:NextORM.Core.DataContext) (создание соединения + параметров); выберите тот, который соответствует базе данных, которую вы уже используете.

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Dependency injection](../getting-started/04-dependency-injection.md)

## Обзор

nextorm состоит из нейтрального к провайдеру ядра (`nextorm`, пространство имён `nextorm.core`) и одного пакета на
базу данных: `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`
и `nextorm.clickhouse`. Провайдер in-memory встроен
в пакет ядра. Провайдер вносит две вещи:

1. **диалект** — объект без состояния, который отрисовывает всё, что различается между базами данных (плейсхолдеры
   параметров, разбиение на страницы, квотирование, имена функций, флаги возможностей); и
2. **контекст** — подкласс [`DataContext`](xref:NextORM.Core.DataContext), который знает, как создавать соединение и параметры, и
   предоставляет диалект через своё свойство `Dialect`.

Генерация SQL полностью управляется [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`),
поэтому построитель SQL и посетители выражений никогда не содержат имён провайдеров. Семантика запросов (проекция,
фильтрация, соединения, группировка, операции над множествами, CTE, оконные функции, поддержка scalar/UDF/TVF) является общей;
различается только отрисовка.

## Выбор провайдера

| Ситуация | Провайдер |
|---|---|
| Локальная разработка, тесты, встраиваемая база данных, небольшие приложения | `nextorm.sqlite` |
| Вы уже используете Microsoft SQL Server / Azure SQL | `nextorm.sqlserver` |
| Вы уже используете PostgreSQL | `nextorm.postgres` |
| Вы уже используете MySQL | `nextorm.mysql` |
| Вы уже используете MariaDB | `nextorm.mariadb` |
| Вы уже используете ClickHouse | `nextorm.clickhouse` |
| Модульные тесты, которые не должны обращаться к базе данных, тесты кэша планов, тесты формы запросов | [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) (ядро) |

Используйте один и тот же код запросов со всеми провайдерами; перечисленные ниже различия провайдеров — единственное, что
меняется.

## Матрица поддержки

| Возможность | SQLite | SQL Server | PostgreSQL | MySQL | MariaDB | ClickHouse | In-memory |
|---|---|---|---|---|---|---|---|
| Пакет | `nextorm.sqlite` | `nextorm.sqlserver` | `nextorm.postgres` | `nextorm.mysql` | `nextorm.mariadb` | `nextorm.clickhouse` | встроен в `nextorm` |
| Плейсхолдер параметра | `$name` | `@name` | `@name` | `@name` | `@name` | `@name` | не применимо |
| Табличные параметры (`ProcedureParameter.Table<T>`) | JSON-документ `json_each` | нативно `SqlDbType.Structured` + `TypeName` | типизированный массив / документ `jsonb` | JSON-документ `JSON_TABLE` | JSON-документ `JSON_TABLE` | связанный `Array(T)`/`Array(Tuple(...))` + `arrayJoin` | бросает `NotSupportedException` |
| Транзакции (`ITransactionManager`) | поддерживается | поддерживается | поддерживается | поддерживается | поддерживается | бросает `NotSupportedException` | не применимо (нет соединения) |
| `INSERT ... VALUES` | поддерживается | поддерживается | поддерживается | поддерживается | поддерживается | поддерживается (малые батчи) | бросает `NotSupportedException` |
| Массовая вставка (`CreateBulkInsertBuilder`) | портируемый `INSERT ... VALUES` | нативный `SqlBulkCopy` | нативный бинарный `COPY` | портируемый `INSERT ... VALUES` | портируемый `INSERT ... VALUES` | портируемый `INSERT ... VALUES` | бросает `NotSupportedException` |
| Bulk-copy флаги (`CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers`) | бросает `NotSupportedException` | `SqlBulkCopyOptions` (только нативный путь) | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| Key upsert (`CreateMergeBuilder`) | `ON CONFLICT ... DO UPDATE` | `MERGE ... USING (VALUES ...)` | `ON CONFLICT ... DO UPDATE` | `ON DUPLICATE KEY UPDATE` | `ON DUPLICATE KEY UPDATE` | бросает `NotSupportedException` | применяется в контексте (upsert) |
| Полный `MERGE` (`WhenMatched`/`WhenNotMatched`, ветки) | бросает `NotSupportedException` | `MERGE ... WHEN MATCHED THEN ...` | `MERGE ... WHEN MATCHED THEN ...` (15+) | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| Условие совпадения merge (`On`/`WhenMatched(condition)`) | бросает `NotSupportedException` | `ON <condition>` / `WHEN ... AND <condition>` | `ON <condition>` / `WHEN ... AND <condition>` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| `MERGE ... RETURNING`/`OUTPUT` (`Returning`) | бросает `NotSupportedException` | `OUTPUT inserted.<col>` | `RETURNING target.<col>` (17+) | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| `DELETE` (`CreateDeleteBuilder`/`Delete`) | поддерживается | поддерживается | поддерживается | поддерживается | поддерживается | `ALTER TABLE ... DELETE ... SETTINGS mutations_sync = 1` | бросает `NotSupportedException` |
| `UPDATE` (`CreateUpdateBuilder`/`Update(entity)`) | поддерживается | поддерживается | поддерживается | поддерживается | поддерживается | `ALTER TABLE ... UPDATE ... SETTINGS mutations_sync = 1` | бросает `NotSupportedException` |
| `UPDATE ... RETURNING` (`Returning`) | `RETURNING` | `OUTPUT inserted.<col>` | `RETURNING` | бросает `NotSupportedException` | `RETURNING` при настроенной версии ≥13.0; иначе бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| `UPDATE ... FROM` (`CreateUpdateJoinBuilder`) | `UPDATE ... FROM` | `UPDATE <alias> ... FROM ... JOIN` | `UPDATE ... FROM` | `UPDATE ... JOIN ... SET` | `UPDATE ... JOIN ... SET` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| `DELETE ... RETURNING` (`Returning`) | `RETURNING` | `OUTPUT deleted.<col>` | `RETURNING` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| `TRUNCATE` (`CreateTruncateBuilder`) | бросает `NotSupportedException` | поддерживается | поддерживается | поддерживается | поддерживается | поддерживается | бросает `NotSupportedException` |
| Потоковое чтение LOB (`ToStream`/`ToTextReader`) | поддерживается (одна колонка `byte[]`/`string`; источник должен раскрывать `rowid`) | поддерживается (одна колонка `byte[]`/`string`) | поддерживается (одна колонка `byte[]`/`string`) | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | поддерживается (одна колонка `byte[]`/`string`; возвращает принадлежащий вызывающему `MemoryStream`/`StringReader`) |
| Многоколоночный reader (`ToDataReader`/`ToDataReaderAsync`) | поддерживается (буферизованный, без локатора; LOB не чанками) | поддерживается | поддерживается | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| Материализация запроса (`ToTempTable`/`ToTable`) | `CREATE [TEMPORARY] TABLE ... AS SELECT` | `SELECT ... INTO` (`ToTable`; уровня сессии через `#name`) | `CREATE [TEMPORARY] TABLE ... AS SELECT` | `CREATE [TEMPORARY] TABLE ... AS SELECT` | `CREATE [TEMPORARY] TABLE ... AS SELECT` | `CREATE TABLE ... ENGINE = MergeTree ... AS SELECT` (`ToTable`) | бросает `NotSupportedException` |
| Батч (`Batch`) | `;`-склеенная команда | `;`-склеенная команда (не `SqlBatch`) | `NpgsqlBatch` | `MySqlBatch` | `MySqlBatch` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| `DELETE ... USING`/join (`From<T>().Join(...).Delete()`, INNER) | бросает `NotSupportedException` | `DELETE <a> FROM ... JOIN ...` | `DELETE FROM ... USING ...` | `DELETE <a> FROM ... JOIN ...` | `DELETE <a> FROM ... JOIN ...` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| Сгенерированный ключ (`ReturningIdentity`/`ReturningKey`) | `RETURNING` | `OUTPUT inserted.<col>` | `RETURNING` | фолбэк `LAST_INSERT_ID()` | фолбэк `LAST_INSERT_ID()` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| Identity-функция (`ReturningIdentity<TKey>()`) | `last_insert_rowid()` | `SCOPE_IDENTITY()` | `lastval()` | `LAST_INSERT_ID()` | `LAST_INSERT_ID()` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| Возврат вставленных строк (`Returning`) | `RETURNING` | `OUTPUT inserted.<cols>` | `RETURNING` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| Только limit | `limit n` | `top(n)` | `limit n` | `limit n` | `limit n` | `limit n` | take в процессе |
| Limit + offset | `limit n offset m` | `offset m rows fetch next n rows only` | `limit n offset m` | `limit n offset m` | `limit n offset m` | `limit n offset m` | skip/take в процессе |
| Только offset | `limit -1 offset m` | `offset m rows` | `offset m` | `limit 18446744073709551615 offset m` | `limit 18446744073709551615 offset m` | `limit 18446744073709551615 offset m` | skip в процессе |
| Разбиение на страницы без `ORDER BY` | допускается | внедряет `order by (select null as anyorder)` | допускается | допускается | допускается | допускается | допускается |
| `INTERSECT ALL` / `EXCEPT ALL` (`*ALL`) | бросает `NotSupportedException` | бросает `NotSupportedException` | поддерживается | бросает `NotSupportedException` | поддерживается | поддерживается | не применимо |
| Ключевое слово рекурсивного CTE | `with recursive` | `with` (плюс `option (maxrecursion n)`) | `with recursive` | `with recursive` | `with recursive` | `with` (рекурсивные CTE не поддерживаются) | не применимо |
| Именованные окна / `GROUPS` / `EXCLUDE` | именованное окно + `GROUPS` + `EXCLUDE` | бросает (нет `WINDOW`-клаузы) | именованное окно + `GROUPS` + `EXCLUDE` | только именованное окно | только именованное окно | именованное окно + `GROUPS` | не применимо |
| Конкатенация строк | `\|\|` | `+` | `\|\|` | `concat(a, b)` | `concat(a, b)` | `concat(a, b)` | не применимо |
| Логический литерал | `1` / `0` | `1` / `0` (через `bit`) | `true` / `false` | `1` / `0` (`true` / `false`) | `1` / `0` (`true` / `false`) | `true` / `false` | не применимо |
| `??` (coalesce) | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | не применимо |
| `stdev` / `stdevp` | `stdev` / `stdevp` (пользовательские) | `stdev` / `stdevp` (нативные) | `stddev` / `stddev_pop` | `stddev_samp` / `stddev_pop` | `stddev_samp` / `stddev_pop` | `stddevSamp` / `stddevPop` | не применимо |
| `var` / `varp` | `var` / `varp` (пользовательские) | `var` / `varp` (нативные) | `variance` / `var_pop` | `var_samp` / `var_pop` | `var_samp` / `var_pop` | `varSamp` / `varPop` | не применимо |
| `date_trunc` | бросает | `datetrunc(...)` (2022+) | поддерживается | бросает | бросает | `dateTrunc(...)` | бросает |
| Арифметика дат (`date_add`, `date_diff`, `end_of_month`, `date_from_parts`, `DateTime.Add*`) | `datetime(x, n \|\| ' days')` / `date(...)` / разность `strftime` | поддерживается | поддерживается | поддерживается | поддерживается | `addDays(...)` … / `toLastDayOfMonth(...)` | бросает |
| Приведение / части даты (`SqlFunctions.ClickHouse.to_*`) | бросает | бросает | бросает | бросает | бросает | `toDate`/`toDateTime`/`toDate32`, `toYear`/…, `toStartOf*`, `toMonday`, `toYYYYMM`/`toYYYYMMDD`, `toUnixTimestamp` | бросает |
| `string_agg` | `group_concat(x, delimiter)` | поддерживается (2017+) | поддерживается | `group_concat(x separator delimiter)` | `group_concat(x separator delimiter)` | `arrayStringConcat(groupArray(...), ...)` | бросает |
| Полнотекст `contains` / `freetext` (кросс-провайдерные) | бросает | `contains` / `freetext` | `to_tsvector(...) @@ ...tsquery(...)` | `match(...) against(...)` | `match(...) against(...)` | бросает | бросает |
| Полнотекстовое ранжирование / score | бросает | `containstable` / `freetexttable` (`RANK`, табличная функция) | `ts_rank` / `ts_rank_cd` | бросает | бросает | бросает | бросает |
| Полнотекстовый поиск только в SQLite (FTS3/FTS4/FTS5) | поверхность запросов `SqlFunctions.Sqlite` (`Match`, FTS5 `FTS5bm25`/`Highlight`/`Snippet`/`Rank`, помощники FTS3/4, табличный `MatchTable` FTS5; FTS3/4 `Rank` требует зарегистрированной на соединении UDF `rank`) | бросает | бросает | бросает | бросает | бросает | бросает |
| Регулярные выражения (`Regex.IsMatch` / `Regex.Replace`, константный шаблон) | `s regexp ...` / `regexp_replace(...)` (регистрируемые) | `regexp_like(...)` / `regexp_replace(...)` (2025+) | `s ~ ...` / `regexp_replace(...)` | `regexp_like(...)` / `regexp_replace(...)` | `s regexp ...` / `regexp_replace(...)` | `match(...)` / `replaceRegexpAll(...)` | нативно `System.Text.RegularExpressions` |
| Битовые / статистические / `-If` агрегаты | бросает | бросает | поддерживается | бросает | бросает | `groupBit*`, `corr`/`covarPop`, `countIf`/… | бросает |
| `multi_if` (многоветвевный) | бросает | бросает | бросает | бросает | бросает | `multiIf(c1, v1, …, else)` | не применимо |
| `lag_in_frame` / `lead_in_frame` | бросает | бросает | бросает | бросает | бросает | `lagInFrame` / `leadInFrame` | не применимо |
| Квотирование идентификаторов / псевдонимов | одинарные кавычки: `as 't1'` | квадратные скобки: `as [t1]` | двойные кавычки: `as "t1"` | обратные кавычки: `` as `t1` `` | обратные кавычки: `` as `t1` `` | обратные кавычки: `` as `t1` `` | не применимо |
| Псевдоним производной таблицы (подзапрос в `FROM`) | не требуется | требуется | требуется | требуется | требуется | требуется | не применимо |
| Переопределение источника на запрос (`WithSchema`/`WithDatabase`/`WithServer`) | schema = attached-база (`main.t`); database — то же; server бросает | `schema.t`, `db.schema.t`, `server.db.schema.t` | `schema.t`; database/server бросают | `db.t` (database = schema); server бросает | как в MySQL | `db.t` (database = schema); server бросает | бросает (нет SQL-источника) |
| Псевдоним табличной функции | не требуется | требуется | требуется | требуется | требуется | требуется | источник TVF не поддерживается |
| Соединение `LEFT` / `RIGHT` / `FULL` / `CROSS` | да | да | да | без `FULL` | без `FULL` | да | да |
| Возможность соединения `RIGHT` / `FULL` | поддерживается | поддерживается | поддерживается | только `RIGHT` | только `RIGHT` | поддерживается | поддерживается |
| Нативный JSON (`json`/`jsonb`) / JSON-функции | JSON1 только через `SqlFunctions.Sqlite` (кросс-провайдерный API `Json.*` не замаплен) | JSON как текст: `json_value`/`json_query`/`json_modify`/`isjson` (`nvarchar`; нативный тип `json` в 2025+) | нативные `json`/`jsonb` + `json_agg`/`json_build_object` + операторы `->`/`->>`/`#>`/`@>`/`?` | `json_value`/`json_query`/`json_modify`/`isjson` поверх `JSON_EXTRACT`/`JSON_SET` | как в MySQL | строковый JSON `JSONExtract*`/`visitParam*`, JSONPath `json_value`/`json_query`/`json_exists`, `json_all_paths`/`to_json_string`; нативная колонка `JSON` маппится как «голый» `JsonObject` | не применимо |
| Массивы / higher-order функции / `ARRAY JOIN` | бросает | бросает | нативные массивы (`any`/`all`, `cardinality`, `array_agg`) | бросает | бросает | `Array(T)`, `arrayMap`/`arrayFilter`/`arrayExists`/… (higher-order), `ARRAY JOIN` (`ArrayJoin`/`LeftArrayJoin`) | бросает |
| Нативные range-типы / range поверх пары скаляров | пара `[RangeColumns]` | пара `[RangeColumns]` | нативные `int4range`…`daterange` + multirange (`Range<T>`/`Range<T>[]`); пара тоже принимается | пара `[RangeColumns]` | пара `[RangeColumns]` | пара `[RangeColumns]` | CLR `Range<T>`/`Range<T>[]` |
| Темпоральные таблицы (`FOR SYSTEM_TIME`) | бросает | `AS OF`/`BETWEEN`/`FROM … TO`/`CONTAINED IN`/`ALL` | бросает | бросает | `AS OF`/`BETWEEN`/`FROM … TO`/`ALL` (`CONTAINED IN` бросает) | бросает | бросает |
| Модифицирующие CTE | бросает | бросает | `With(name, insert/update/delete)` (тело требует `RETURNING`) | бросает | бросает | бросает | бросает |
| Модификаторы запроса (`DISTINCT ON`/`WITH TIES`/`TABLESAMPLE`) | бросает | `WITH TIES`; только `TABLESAMPLE SYSTEM` | `DISTINCT ON`; `WITH TIES`; `TABLESAMPLE SYSTEM`/`BERNOULLI` | бросает | бросает | бросает (есть свой `SAMPLE`) | бросает |
| Источник `PIVOT` / `UNPIVOT` | бросает | нативные T-SQL `PIVOT`/`UNPIVOT` | бросает | бросает | бросает | бросает | бросает |
| Клаузулы ClickHouse (`FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`/`LIMIT n BY`/`WITH TOTALS`/`GLOBAL IN`) | бросает | бросает | бросает | бросает | бросает | `Final()`/`Sample()`/`PreWhere()`/`Settings()`/`LimitBy()`/`WithTotals()`/`global_in` | бросает |
| Строгость соединений (`ANY`/`ALL`/`ASOF`, `SEMI`/`ANTI`/`PASTE`) / `GLOBAL` | бросает | бросает | бросает | бросает | бросает | `WithStrictness(...)` / `Global()` | бросает |
| Словари (`dictGet`) | бросает | бросает | бросает | бросает | бросает | `dict_get`/`dict_get_or_default`/`dict_has`/… (нужен настроенный dictionary) | бросает |
| Провайдерные табличные функции (`FROM`-источник) | `json_each`/`json_tree` | `string_split`/`openjson`/`containstable`/`freetexttable` | `generate_series`/`unnest`/`regexp_matches`/`jsonb_to_recordset` | бросает | бросает | `numbers`/`zeros`/`generateRandom`/`values` (`url`/`s3`/`file`/`remote`/`cluster` объявлены заранее, распределённое исполнение вне области) | источник TVF не поддерживается |
| Фильтрующие агрегаты (`FILTER (WHERE …)` / `-If`) | ANSI `count(*) filter (where …)` | бросает | ANSI `filter (where …)` (9.4+; гейт по версии) | бросает | бросает | комбинатор `-If` (`countIf`/`sumIf`/…) | бросает |
| Ordered-set / boolean / regression агрегаты | бросает | оконная форма `percentile_cont(f) within group (order by x) over (…)` (2012+) | `percentile_cont`/`percentile_disc`/`mode`, `bool_and`/`bool_or`/`every`, `regr_*` | бросает | оконная `percentile_cont` (10.3+) | `quantile*`/`median` (своё семейство); нет `bool_and`/`regr_*` | бросает |
| Настройки и последовательности PostgreSQL | бросает | бросает | `current_setting`/`set_config`, `nextval`/`setval`/`currval`/`lastval` | бросает | последовательности через `SqlFunctions.MySql` (`next_value_for`/`nextval`/`setval`/`lastval`) | бросает | бросает |
| Провайдерная библиотека скалярных функций (`SqlFunctions.<Provider>`) | `SqlFunctions.Sqlite` (core-скаляры, JSON1, дата, математика, запросы FTS3/4/5) | `SqlFunctions.SqlServer` (T-SQL-библиотека, XML-методы, JSON-как-текст, `string_split`/`openjson`) | `SqlFunctions.Postgres` (массивы, JSON/JSONB, ranges, семейства агрегатов, full-text `ts_*`, настройки/последовательности, `regexp_*`) | `SqlFunctions.MySql` (строки/дата/хэши/inet/JSON) | наследует `SqlFunctions.MySql` + добавки MariaDB (`nvl`, `add_months`, `to_char`, `xxh3`, последовательности, …) | `SqlFunctions.ClickHouse` (quantile/uniq/topK/sequence-агрегаты, массивы, map, хэши, словари, табличные функции) | нет (`SqlFunctions.Sql` вычисляет in-process) |
| Row values / кортежи (`(a, b)`, доступ к элементу, сравнение строк) | плоский конструктор `(a, b)` как прямой операнд предиката `==`/`!=` (`.ItemN` inline сворачивается; проекция/сортировка/группировка/аргументы функций и серверный `.ItemN` бросают `NotSupportedException`) | бросает `NotSupportedException` | `ROW(a, b)`, `(row).fN`, `ROW(...) = ROW(...)` (материализация raw row открыта, [#194](https://github.com/AlexeyShirshov/nextorm/issues/194)) | плоский конструктор `(a, b)` как прямой операнд предиката `==`/`!=` (`.ItemN` inline сворачивается; серверный `.ItemN` бросает) | как в MySQL | `tuple(a, b)`, `tupleElement`, сравнение | бросает `NotSupportedException` |

## Различия провайдеров: решения по унификации

Там, где провайдеры различаются, nextorm либо **унифицирует** поверхность в коде, либо **гейтит**
возможность (неподдерживающий провайдер бросает `NotSupportedException`), либо **документирует**
различие и оставляет его провайдерным. Решение по каждому известному расхождению:

| Различие | Решение | Обоснование / хук |
|---|---|---|
| Принимаемые поля `date_add`/`date_diff`/`date_trunc` | **Оставить провайдерным, гейт по полю** | `SupportsDateAddField`/`SupportsDateDiffField`/`SupportsDateTruncField` отклоняют неподдерживаемое поле; единый нормализованный набор молча менял бы результат (SQLite сворачивает `millisecond`/`quarter`, в SQL Server нет `decade`/`century`/`millennium`). |
| `FULL JOIN` в MySQL/MariaDB | **Гейт без полифилла** | `SupportsFullJoin => false`; переписывание в `LEFT JOIN … UNION … RIGHT JOIN` меняет форму строк/дедупликацию и может испортить план, поэтому неявно не эмитится. |
| `CUBE`/`GROUPING SETS` в MySQL/MariaDB (и весь `ROLLUP`/`CUBE`/`GROUPING SETS` в in-memory) | **Гейт без полифилла** | `SupportsCube`/`SupportsGroupingSets`; эмуляция через `UNION ALL` множит сканы и меняет семантику (`GROUPING()`), поэтому оставлена сырому SQL. |
| NULL-семантика `GREATEST`/`LEAST` | **Документированное различие** | PostgreSQL/SQL Server 2022+/ClickHouse игнорируют NULL-аргументы; MySQL/MariaDB и SQLite возвращают NULL, если любой аргумент NULL. Доступность гейтится `SupportsGreatestLeast`; поведение с NULL не переписывается. |
| Кросс-провайдерные скалярные функции (`left`/`rpad`/`concat_ws`/`translate`/`ascii`/…) | **Унифицированы, с гейтом по имени** | [`IScalarFunctions`](xref:NextORM.Core.IScalarFunctions) рендерит нативную форму каждого провайдера (SQLite `substr`/`unicode`, SQL Server паддинги через `REPLICATE`, ClickHouse `*UTF8`); имя, которое провайдер не может выразить, отклоняется. NULL-семантика `concat_ws` и отрицательный `n` у `left`/`right` — документированные различия, а не переписывание. |
| Шаблоны форматирования дат/чисел (`to_char`, `FORMAT`, `strftime`, `formatDateTime`) | **Закрыто — не унифицируемо** | Языки шаблонов несовместимы, поэтому форматирование остаётся провайдерными UDF `[SqlFunction]`; единого портируемого аргумента `template` нет. |
| `FOR JSON` / `FOR XML` | **Гейт (SQL Server)** | `SupportsForJson`/`SupportsForXml`. |
| Хинты уровня инструкции | **Унифицировано** | SQL Server `OPTION (...)`, PostgreSQL/MySQL/MariaDB встроенный `/*+ ... */`; SQLite/ClickHouse без синтаксиса и остаются под гейтом (см. [Хинты запросов](../guide/13-query-hints.md)). |
| Метки запроса (`WithTag`) | **Унифицировано** | Портируемый блочный комментарий `/* tag */` сразу после `SELECT` на всех SQL-провайдерах; нативные SQL Server `OPTION (LABEL)` и ClickHouse `log_comment` не используются, in-memory принимает вызов как no-op (см. [Метки запроса](../guide/13-query-hints.md)). |
| Блокирующие табличные хинты vs index hints | **Оставить провайдерным** | У `WITH (NOLOCK)` нет аналога среди index hints MySQL/MariaDB/SQLite (`USE INDEX`/`INDEXED BY` меняют план, а не блокировки), поэтому подключён только SQL Server (`SupportsTableHints`). |
| Хинты join / подзапроса / области видимости | **Две формы** | SQL Server рендерит join-хинт внутри join (`INNER LOOP JOIN`), а хинт области — как `WITH (...)` на каждой таблице; PostgreSQL/MySQL/MariaDB сворачивают все три во встроенный `/*+ ... */`; SQLite/ClickHouse/in-memory их отклоняют (см. [Хинты запросов](../guide/13-query-hints.md)). |
| Режимы ожидания блокировки строк (`NOWAIT`/`SKIP LOCKED`) | **Унифицировано** | [`ILockRenderer.Render`](xref:NextORM.Core.ILockRenderer.Render(NextORM.Core.LockMode,NextORM.Core.LockWaitMode,NextORM.Core.KeywordCase)) рендерит родную форму каждого способного провайдера: PostgreSQL/MySQL/MariaDB дописывают `NOWAIT`/`SKIP LOCKED` (MySQL переключает разделяемую блокировку на `FOR SHARE`), SQL Server добавляет `NOWAIT`/`READPAST` в блокирующий табличный хинт (`READPAST` приближает `SKIP LOCKED`); SQLite/ClickHouse/in-memory отклоняют любую блокировку строк. |
| Сырой SQL как композируемый источник `FROM` | **Унифицировано** | `FromSql` + `SupportsRawSqlSource` у всех SQL-провайдеров (см. [Сырой SQL](../guide/12-raw-sql.md)). |
| Уровни переопределения источника на запрос | **Гейтится по уровню** | `MakeQualifiedTableName` плюс `SupportsCrossDatabase`/`SupportsLinkedServer`: провайдер, не умеющий уровень, отклоняет `WithDatabase`/`WithServer` через `NotSupportedException`, а не молча теряет квалификатор. Schema/имя таблицы и сырой `WithTableExpression` — универсальные уровни у SQL-провайдеров. |
| `INTERSECT ALL`/`EXCEPT ALL` | **Гейт** | PostgreSQL и MariaDB поддерживают; SQL Server/SQLite/MySQL отклоняют через `SupportsIntersectExceptAll`. |
| Синтаксис, зависящий от версии сервера (`FILTER` в PostgreSQL, `UPDATE ... RETURNING` в MariaDB) | **Гейт по явно настроенной версии** | Провайдер несёт серверную `Version` (`SupportsUpdateReturning` отражает гейт MariaDB). PostgreSQL включает ANSI `FILTER` с 9.4+ и сохраняет прежнее поведение при незаданной версии (предполагается ≥9.4); MariaDB включает `UPDATE ... RETURNING` только при явной версии 13.0+. Живого определения версии сервера нет. PostgreSQL дополнительно фиксирует одну версию на конкретный тип контекста на всё время жизни процесса; MariaDB такой защиты не накладывает (её гейтируемый `UPDATE ... RETURNING` не кладётся в кэш планов). |

Строки таблицы [ограничений](../advanced/limitations.md) описывают итоговое поведение в рантайме.

### Гейты по версии сервера

Две возможности зависят от версии сервера, и обе гейтятся **явно настроенной версией** — nextorm
никогда не опрашивает живой сервер. Передайте версию сервера при создании контекста (у контекста
провайдера есть перегрузка конструктора с параметром `Version`); если её не задать, сохраняется
историческое поведение: PostgreSQL предполагает версию 9.4 или новее и эмитит `FILTER (WHERE ...)`, а
MariaDB оставляет `UPDATE ... RETURNING` отключённым. Фильтрующие агрегаты PostgreSQL требуют 9.4+ и
бросают `NotSupportedException` ниже; `UPDATE ... RETURNING` в MariaDB требует явной версии 13.0+.
PostgreSQL накладывает одну неизменяемую версию сервера на **конкретный тип контекста** на всё время
жизни процесса, потому что кэшируемый SQL `SELECT` зависит от гейта `FILTER`: второй контекст
PostgreSQL того же конкретного типа, запрашивающий другую версию, бросает `InvalidOperationException`,
поэтому для каждой версии PostgreSQL следует объявить отдельный подкласс контекста. MariaDB **не
накладывает такой защиты**: поскольку её единственная версионно-гейтируемая операция
(`UPDATE ... RETURNING`) — мутация, чей SQL не кладётся в кэш планов, на одном типе контекста допустимы
разные версии. `RETURNING` у insert/delete и `ANY_VALUE` (`any_agg`, 13.2) не затрагиваются.

## Как подключается диалект

Диалект реализует [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) или наследуется от [`SqlDialectBase`](xref:NextORM.Core.SqlDialectBase). В [`SqlDialectBase`](xref:NextORM.Core.SqlDialectBase) абстрактными являются только
[`MakeParam`](xref:NextORM.Core.ISqlDialect.MakeParam(System.String)) и [`MakePage`](xref:NextORM.Core.ISqlDialect.MakePage(NextORM.Core.Paging,System.Text.StringBuilder,NextORM.Core.KeywordCase)); у всех остальных членов есть рабочее значение по умолчанию ANSI, поэтому диалект
переопределяет только то, что отличается. Различия возможностей (разбиение на страницы, требующее `ORDER BY`, обязательные
псевдонимы подзапросов, `INTERSECT ALL`/`EXCEPT ALL`) выражаются свойствами, а не особыми случаями в
построителе SQL.

```csharp
// The built-in dialects are singletons exposed as a static Instance.
ISqlDialect sqlite = SqliteDialect.Instance;
ISqlDialect sqlServer = SqlServerDialect.Instance;
ISqlDialect postgres = PostgresDialect.Instance;
ISqlDialect mysql = MySqlDialect.Instance;
ISqlDialect mariaDb = MariaDbDialect.Instance;
ISqlDialect clickHouse = ClickHouseDialect.Instance;
```

Контекст провайдера возвращает свой диалект из переопределённого свойства:

```csharp
public class SqliteDataContext : DataContext
{
    public override ISqlDialect Dialect => SqliteDialect.Instance;
    // CreateDbConnection / CreateParam are provider specific.
}
```

Контракт диалекта намеренно исключает создание соединения/параметров (`CreateConnection`/
`CreateParam`) и отображение столбцов (`MapColumnExpression`): они находятся на контексте, потому что это
отдельная ось от SQL-текста.

## Регистрация провайдера

Каждый пакет провайдера добавляет методы расширения `UseXxx` на [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder), и у каждого контекста также есть
публичный конструктор, принимающий строку подключения или существующий `DbConnection`:

```csharp
using NextORM.Core;
using NextORM.Sqlite;      // or nextorm.sqlserver / nextorm.postgres

var builder = new DataContextBuilder().UseSqlite("app.db");   // provider-specific overload
using var ctx = builder.CreateDataContext();                   // returns IDataContext
```

С внедрением зависимостей:

```csharp
services.AddNextOrmContext(builder => builder.UseSqlite("app.db"));
```

См. [Dependency injection](../getting-started/04-dependency-injection.md) для регистраций с ключом и универсальных
регистраций.

## См. также

- [SQLite](sqlite.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [MySQL](mysql.md)
- [MariaDB](mariadb.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`,
`tests/nextorm.sqlite.tests/SqliteDialectTests.cs`, `tests/nextorm.sqlserver.tests/SqlServerDialectTests.cs`,
`tests/nextorm.postgres.tests/PostgresDialectTests.cs`, `tests/nextorm.mysql.tests/MySqlDialectTests.cs`,
`tests/nextorm.mariadb.tests/MariaDbDialectTests.cs`, `tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs`.
