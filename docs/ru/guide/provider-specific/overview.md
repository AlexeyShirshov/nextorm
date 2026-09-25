# Специфичный для провайдеров SQL

> nextorm нацелен на переносимую часть SQL, но не скрывает возможности конкретной СУБД. Конструкция
> доступна, когда активный диалект объявляет соответствующую возможность
> [`ISqlDialect`](xref:NextORM.Core.ISqlDialect), а провайдер, который её не поддерживает, отклоняет
> вызов через `NotSupportedException`.
>
> В этом разделе — только конструкции, **эксклюзивные для одного провайдера**: все остальные диалекты
> для них выбрасывают исключение. Конструкции, которые лишь *пишутся* по-разному или поддерживаются
> несколькими провайдерами (`greatest`/`least`, полнотекстовый поиск, JSON, табличные функции,
> `any_agg`, генераторы UUID, `PERCENTILE_CONT`), документированы на соответствующих тематических
> страницах. Эксклюзивная поверхность есть только у трёх провайдеров: ClickHouse, PostgreSQL и
> SQL Server.

**Что нужно знать:** [Запросы и проекции](../01-querying-and-projections.md) · [Обзор провайдеров](../../providers/overview.md) · [Ограничения и возможности вне области охвата](../../advanced/limitations.md)

## Как устроено гейтирование

Построитель запросов управляется диалектом: всё, что не входит в переносимую поверхность, защищено
свойством `Supports*`, предикатом по имени или хуком `Make*` на
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect). Реализации в `src/nextorm.<provider>` включают нужные
флаги и рендерят нативный синтаксис, а `SqlDialectBase` оставляет безопасные значения по умолчанию,
которые выбрасывают исключение. Поэтому:

* одно и то же LINQ-выражение компилируется для всех провайдеров, но выполняет его только тот, кто
  поддерживает конструкцию, — остальные получают `NotSupportedException` с понятным сообщением;
* строже всех контекст in-memory: он реализует только select, where, соединения, группировку,
  операции над множествами и окна над коллекциями в памяти, а всё, что требует SQL-движка, отклоняет;
* неподдерживаемая конструкция падает при **подготовке** запроса (SQL-провайдеры) или при перечислении
  (in-memory), а не в момент написания выражения.

## Провайдеры с эксклюзивной поверхностью

| Провайдер | Эксклюзивные конструкции |
|---|---|
| [ClickHouse](clickhouse.md) | Массивы и `ARRAY JOIN`, `LIMIT n BY expr`, `GROUP BY ... WITH TOTALS`, `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`, строгость соединений и `GLOBAL JOIN`, `GLOBAL IN`, агрегаты `uniq`/`quantile`/`any`/`argMin`/`argMax`, комбинаторы `-If`, словари, строковый JSON `JSONExtract`, табличные функции `numbers`/`zeros` |
| [MySQL и MariaDB](mysql.md) | Нативные строковые/условные идиомы (`FIND_IN_SET`/`FIELD`/`ELT`/`SUBSTRING_INDEX`/`FORMAT`), преобразование дат по `%`-шаблонам и функции Unix-эпохи (`STR_TO_DATE`/`DATE_FORMAT`/`FROM_UNIXTIME`/`UNIX_TIMESTAMP`), шестнадцатеричные хеши (`MD5`/`SHA1`/`SHA2`), преобразование IPv4 (`INET_ATON`/`INET_NTOA`), семейство мутации JSON и `UUID_TO_BIN`/`BIN_TO_UUID` (только MySQL) |
| [PostgreSQL](postgresql.md) | Нативные массивы и операторы, нативные `json`/`jsonb`, упорядоченные агрегаты (`percentile_* ... WITHIN GROUP`), регрессионные/logical/bit-агрегаты, расширенные скаляры и regexp, `DISTINCT ON`, `unnest` |
| [SQL Server](sqlserver.md) | `CHOOSE`, табличные хинты и `OPTION (...)`, `FOR JSON`/`FOR XML`, `string_split`/`openjson` |

## Провайдеры без эксклюзивной поверхности

У SQLite **нет** эксклюзивных конструкций: каждый включаемый им флаг возможности включён и хотя бы одним
другим диалектом. Поэтому его страница описывает только подключение и ограничения, а общие реализуемые
им конструкции находятся на тематических страницах:

* [SQLite](../../providers/sqlite.md) — скалярные `max`/`min`, части дат `strftime`, JSON1,
  зарегистрированные агрегаты `stdev`/`var`.

Страница MySQL/MariaDB описывает также общие конструкции семейства MySQL: JSON-как-текст,
полнотекстовый `MATCH ... AGAINST`, `ANY_VALUE`, `PERCENTILE_CONT`/`MEDIAN` и `UUID_v4()`/`UUID_v7()`.

## Модель переносимости

Страницы провайдеров описывают конструкцию со стороны конкретной СУБД; полные, провайдеро-нейтральные
разборы — в тематических разделах:

* [Фильтрация (WHERE)](../02-filtering-where.md) — `IN`/`Contains`, полнотекстовые предикаты;
* [Соединения](../03-joins.md) — типы соединений, APPLY/LATERAL, строгость ClickHouse и `GLOBAL JOIN`;
* [Группировка и агрегаты](../04-grouping-and-aggregates.md) — `ROLLUP`/`CUBE`, семейства агрегатов;
* [Сортировка и постраничная выборка](../05-sorting-and-paging.md) — `Limit`/`Offset`, `LIMIT n BY expr`;
* [Скалярные функции](../../scalar-functions/index.md) — массивы, JSON, UUID, session/info, поверхности провайдеров;
* [Табличные функции](../13-table-valued-functions.md) — `generate_series`, `string_split`, `numbers`, `zeros`;
* [Хинты запросов](../17-query-hints.md) — табличные хинты, `OPTION (RECOMPILE)`, модификаторы ClickHouse;
* [Поддержка JSON в разных провайдерах](../18-json.md) — все поверхности JSON рядом.

Переносимые функции, которые пишутся по-разному в каждой СУБД, живут на
[`CommonFunctions`](xref:NextORM.Core.CommonFunctions) и документированы на страницах функций:
`iif(condition, a, b)`, `gen_random_uuid()`/`uuidv7()`, а также session/info-функции
`current_user()`/`session_user()`/`current_schema()`/`version()`.

## См. также

* [Ограничения и возможности вне области охвата](../../advanced/limitations.md)
* [Обзор провайдеров](../../providers/overview.md)

---

Source: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`,
`src/nextorm.{postgres,sqlserver,clickhouse}/*Dialect.cs`.
