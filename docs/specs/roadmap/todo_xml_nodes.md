# TODO: SQL Server `xml.nodes()` — rowset

> Actionable-остаток XML-воркстрима SQL Server. Скалярные `.value`/`.query`/`.exist` реализованы
> (`SqlFunctions.SqlServer.xml_value`/`xml_query`/`xml_exist`, `XmlFunctions`/
> `XmlFunctions(name)`/`IXmlFunctions.Render`; `XmlSqlTranslator`). `.nodes` — rowset, отложен.
> Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.3.
> **Статус: заблокировано** отсутствием внешней ссылки внутри `FROM`-источника.

## Пункт и цель

- `xml.nodes('XQuery')` возвращает rowset и применим **только** в `CROSS APPLY`/`OUTER APPLY`:
  `FROM t CROSS APPLY t.xmlcol.nodes('/a/b') AS n(c)`, далее `n.c.value('...', '...')`.
- Сейчас `SqlServerDialect.XmlFunctions.Supports("nodes")` возвращает `false` — сознательно; скалярные
  методы гейтятся `name is "value" or "query" or "exist"`.
- Цель: дать композируемый rowset-источник из `.nodes(...)` с последующей проекцией/`value()`.

## Блокер (общий)

- **Нет механизма ссылки на внешнюю строку внутри `FROM`-подзапроса / `APPLY`** — тот же пробел, что
  коррелированный `APPLY`, «сырой SQL как композируемый источник», `CONTAINSTABLE`/`FREETEXTTABLE`
  (см. `sql-capabilities-gap-analysis.md`, `limitations.md`).
- `.nodes` отдаёт XML-колонку-как-функцию (rowset от значения колонки), а **не** таблицу/сущность,
  поэтому плохо ложится на существующую TVF-модель `[SqlTableFunction]`.

## Матрица «провайдер × форма»

Форма: «развернуть XML-значение в строки (XQuery) и работать с полученным rowset».

| Провайдер | Нативная форма | Источник |
| --- | --- | --- |
| SQL Server | `.nodes('XQuery')` → rowset (только `CROSS/OUTER APPLY`) | MS Learn: `nodes() Method (xml Data Type)` |
| PostgreSQL | другая форма: `xmltable(...)`, `xpath(...)` → `xml[]`; постфиксных методов нет | PostgreSQL 17: «9.15. XML Functions», «8.13. XML Type» |
| MySQL | другая форма, устарела: `ExtractValue`/`UpdateXML`; XML-типа нет | MySQL 8.4 Reference Manual: «12.11 XML Functions» |
| MariaDB | аналог MySQL: `ExtractValue`/`UpdateXML`; XML-типа нет | MariaDB KB: `ExtractValue`, `UpdateXML` |
| ClickHouse | ни XML-типа, ни XPath | ClickHouse function reference (string functions) |
| SQLite | XML-типа и XPath в ядре нет | SQLite: «Built-In Scalar SQL Functions» |
| InMemory | SQL не генерируется; XPath невыразим в LINQ-to-Objects | — |

## Что нужно построить

1. **Публичный API внешней ссылки в `FROM`-источнике** (например, `CrossApply`/`OuterApply`,
   принимающий источник, зависящий от колонок внешней строки) — общий механизм для `.nodes`,
   коррелированного `APPLY` и сырого SQL как источника.
2. **Рендер rowset-формы** `.nodes('xpath')` как `CROSS APPLY <xmlcol>.nodes('xpath') as <alias>(<col>)`
   с row-типом (по образцу `IRegexpMatchesRow`/`IGenerateRandomRow`).
3. **Диалектные хуки:** `XmlFunctions("nodes")` → `true` у SQL Server; `IXmlFunctions.Render`
   (или отдельный хук) для rowset-формы; гейт у остальных провайдеров + in-memory throw.
4. **Инфраструктура:** план-ключ, clone, подготовка `FROM`-источника, row reader XML-колонки.

## Критерий приёмки

- `ctx.From<T>().CrossApply(t => t.XmlCol.Nodes("/a/b"))` (или эквивалент) рендерит валидный T-SQL
  `cross apply ... nodes(...)` и выполняется на реальном SQL Server; поддерживается проекция
  `value()` над развёрнутыми строками.
- SQL-gen + интеграционный тест зелёные; прочие провайдеры бросают `NotSupportedException`;
  in-memory бросает; покрытие ≥ `MIN_LINE_COVERAGE`; аудит `nextorm-code-auditor`; docs EN+RU +
  `sql-capabilities-gap-analysis.md`.

## Источники и файлы

- `docs/specs/roadmap/sql-capabilities-gap-analysis.md` (gap #3).
- https://learn.microsoft.com/sql/t-sql/xml/nodes-method-xml-data-type
- Код: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `SqlDialectBase.cs`,
  `src/nextorm.sqlserver/SqlServerDialect.cs:51`, `src/nextorm.core/Visitors/XmlSqlTranslator.cs`,
  `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`, `src/nextorm.core/DataContext/SqlSourceRenderer.cs`,
  `src/nextorm.core/Builders/EntityBuilder.cs`.
