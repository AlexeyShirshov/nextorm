# TODO: Runtime-override целевой таблицы для bulk insert (`BulkCopyOptions.TableName`)

> **Статус: реализовано (SHIPPED) в `1.0-b.1`.** Добавлены `BulkInsertOptions.TableName`/`TableSchema` и
> `BulkInsertOptionsBuilder.Table(string)` / `Table(string schema, string table)`; override действует на
> native (`COPY`/`SqlBulkCopy`) и portable (`INSERT ... VALUES`) путях и входит в ключ plan-кэша.
> Доки: `docs/guide/24-bulk-insert.md` (+RU); gap-analysis §4 п.47.

> Рабочий план (design RFC). Источник: README репозитория примеров `~/sources/linq2db-apps-nextorm`,
> раздел «Engine gaps surfaced in `nextorm 1.0.5-alpha`», пункт 7. Связано:
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) §4 п.47.

## Статус верификации (`nextorm 1.0.6-alpha`)

Валиден на всех провайдерах, compile-time (харнесс `Gaps/`: рефлексия по `BulkInsertOptions`): свойства
`TableName` нет. Падающая форма — Bitwarden scenario 4 (`Util/Seeder/Recipes/CollectionsRecipe`):
bulk copy в явную destination-таблицу выразить нечем.

## Пункт и цель

- **Проблема:** у linq2db `db.BulkCopy(entities)` цель задаётся в рантайме через
  `BulkCopyOptions { TableName = "…" }`; в nextorm целевая таблица фиксирована `[SqlTable]`-маппингом
  сущности, поэтому тип без маппинга нельзя перенацелить, а тип-донор схемы требует отдельного
  `[SqlTable]`.
- **Цель:** опция/перегрузка на `BulkInsertInto<T>`, задающая целевую таблицу (имя, при необходимости
  схема) в рантайме, без требования `[SqlTable]` и без дублирования типа.
- **Критерий приёмки:** `BulkInsertInto<T>(o => o.Table("schema", "table"))` (форма — открытый вопрос)
  работает для native (`COPY`/`SqlBulkCopy`) и portable (`INSERT ... VALUES`) путей; совместимо с
  `KeepIdentity()`/returning; SQL-generation тесты по провайдерам; публичный API аддитивен, покрыт
  XML-доком (`CS1591`).

## Падающая форма

- Bitwarden scenario 4 (`Util/Seeder/Recipes/CollectionsRecipe`) — bulk copy в явную
  destination-таблицу; обход: заводится отдельный тип с `[SqlTable]`, дублирующий схему.

## Гипотеза и область

- Целевое имя берётся из метаданных сущности в `Query/Mutations/BulkInsertCommand.cs` /
  `DataContext/SqlMutationBuilder.cs`; runtime-override должен пройти через
  `Builders/{BulkInsertBuilder,BulkInsertOptions}.cs` и оба исполнителя —
  `DataContext/PortableBulkInsertExecutor.cs` (portable) и провайдерные `BulkCopy`-пути.
- Затронут план-кэш: имя таблицы — часть ключа плана (иначе кэш смешает цели).

## Файлы к изменению

- `src/nextorm.core/Builders/{BulkInsertBuilder,BulkInsertOptions}.cs`,
  `src/nextorm.core/Query/Mutations/BulkInsertCommand.cs`,
  `src/nextorm.core/DataContext/{PortableBulkInsertExecutor,SqlMutationBuilder}.cs`
- Провайдерные bulk-пути и `*Dialect.cs` (`nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}`)
- Тесты: `tests/nextorm.<p>.tests/SqlGenerationTests.cs`, `CommonTestSuite.BulkInsert.cs`
- Доки EN+RU ([Bulk insert](../../guide/24-bulk-insert.md) +RU),
  `sql-capabilities-gap-analysis.md` §4 п.47

## Открытые вопросы

1. Форма API: `o.Table("t")` vs `o.Table(schema, table)` vs `o.Into(Type/TableAlias)`.
2. Взаимодействие с `[SqlTable]`: приоритет override над атрибутом, разрешать ли тип вообще без
   маппинга (как тогда выводится схема колонок?).
3. Учитывать ли override в returning-сценариях и в ключе план-кэша явно.

## Источник

README портов, пункт 7; после закрытия — убрать/пометить его в README примеров.
