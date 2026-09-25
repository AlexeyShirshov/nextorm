# TODO: `KeepIdentity()` на таблице без identity-колонки (SQL Server error 8106)

> **Статус: реализовано (SHIPPED) в `1.0-b.1`.** `KeepIdentity()` эффективен только когда маппинг
> объявляет identity-колонку (silent ignore, совместимо с linq2db); `SET IDENTITY_INSERT` /
> `OVERRIDING SYSTEM VALUE` на не-identity таблицах не эмитятся. Доки: `docs/guide/24-bulk-insert.md`
> (+RU); gap-analysis §4 п.48.

> Рабочий план (design RFC). Источник: README репозитория примеров `~/sources/linq2db-apps-nextorm`,
> раздел «Engine gaps surfaced in `nextorm 1.0.5-alpha`», пункт 8. Связано:
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) §4 п.48.

## Статус верификации (`nextorm 1.0.6-alpha`)

Валиден только на SQL Server (харнесс `Gaps/`: `KeepIdentity() on a table without identity`):
`SqlException: Table 'gap_nonidentity' does not have the identity property. Cannot perform SET
operation.` SQLite/PostgreSQL/MySQL тот же вызов переносят. Падающая форма — Bitwarden scenario 1/2
(`CipherRepository.CreateAsync`, `DefaultBulkCopyOptions` с `KeepIdentity = true`).

## Пункт и цель

- **Проблема:** `KeepIdentity()` на таблице без identity-колонки заставляет SQL Server выполнить
  `SET IDENTITY_INSERT`, который падает с ошибкой 8106. linq2db принимает
  `BulkCopyOptions { KeepIdentity = true }` безусловно, поэтому перенос ожидания ломается.
- **Цель:** определить предсказуемую семантику: либо `KeepIdentity()` на таблице без identity-колонки
  тихо игнорируется (как linq2db), либо падает **на этапе подготовки** с понятным сообщением — но не
  серверной ошибкой 8106.
- **Критерий приёмки:** SQL-generation/интеграционный тест на SQL Server воспроизводит таблицу без
  identity-колонки и проверяет выбранную семантику; на portable-провайдерах `SET IDENTITY_INSERT` не
  эмитится; поведение документировано (EN+RU).

## Падающая форма

- Bitwarden port: демо-ключи — явные GUID-строки, поэтому `KeepIdentity()` в примере опускается; при
  включении на не-identity таблице — SQL Server error 8106.

## Гипотеза и область

- Признак identity-колонки уже выводится из метаданных (`DataContext/Meta/*`); проверку нужно
  добавить либо в `Builders/{BulkInsertOptions,BulkInsertBuilder}.cs` (fail-fast на подготовке), либо в
  SQL Server-ветку `DataContext/SqlMutationBuilder.cs` / `nextorm.sqlserver` bulk-пути (не эмитить
  `SET IDENTITY_INSERT`, когда identity-колонки нет).
- Согласовать с [`todo_bulk_insert_destination_table.md`](todo_bulk_insert_destination_table.md):
  override цели меняет метаданные таблицы, от которых зависит решение об identity.

## Файлы к изменению

- `src/nextorm.core/Builders/{BulkInsertOptions,BulkInsertBuilder}.cs`,
  `src/nextorm.core/DataContext/{SqlMutationBuilder,PortableBulkInsertExecutor}.cs`,
  `src/nextorm.core/DataContext/Meta/*`
- `src/nextorm.sqlserver/*Dialect.cs` и SQL Server bulk-путь
- Тесты: `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`,
  `tests/nextorm.integration.tests/CommonTestSuite.BulkInsert.cs` (SQL Server)
- Доки EN+RU ([Bulk insert](../../guide/24-bulk-insert.md) +RU),
  `sql-capabilities-gap-analysis.md` §4 п.48

## Открытые вопросы

1. Игнорировать (совместимость с linq2db) или fail-fast с явным сообщением? Решение зафиксировать в
   доке.
2. Как это взаимодействует с `TableName`-override (схема может не совпадать с типом-донором)?

## Источник

README портов, пункт 8; после закрытия — убрать/пометить его в README примеров.
