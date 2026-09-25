# TODO: Трансляция lookup локальной коллекции (`dict[column]`) внутри запроса

> Рабочий план (design RFC). Источник: README репозитория примеров `~/sources/linq2db-apps-nextorm`,
> раздел «Engine gaps — verified on `nextorm 1.0.6-alpha`», пункт 11. Связано:
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) §4 п.50.

## Статус верификации (`nextorm 1.0.6-alpha`)

Воспроизводится харнессом `Gaps/` в `~/sources/linq2db-apps-nextorm` (`dotnet run --project Gaps`):
один и тот же запрос с `synchronised[s.TenantRegistryId.Value]` падает на **всех** провайдерах, но
разными ошибками.

| Провайдер | Ошибка |
|---|---|
| SQLite | `Must add values for the following parameters: $synchronisedtenant_registry_id` |
| PostgreSQL | `InvalidCastException: Writing values of 'Dictionary<int,DateTime>' is not supported for parameters …` |
| MySQL | `Parameter '@synchronisedtenant_registry_id' must be defined` |
| SQL Server | `No mapping exists from object type Dictionary<int,DateTime> …` |

## Пункт и цель

- **Проблема:** локальная коллекция (`Dictionary`/`List`/массив), индексируемая **колонкой запроса**
  (`dict[y.TenantRegistryId.Value]`), не транслируется: nextorm пытается отправить сам объект
  коллекции как параметр (в имени параметра оказывается и local, и колонка), драйверы падают.
- **Цель:** определить и реализовать предсказуемую семантику:
  либо транслировать lookup в SQL (derived-таблица `VALUES(dict)` + join/LATERAL, либо `CASE` по
  ключам, если ключи константны), либо **fail-fast на этапе подготовки** с понятным
  `NotSupportedException`, но не падать драйверной ошибкой.
- **Критерий приёмки:** SQL-generation тесты на три случая — (a) lookup по колонке → join к
  `VALUES`/`CASE`, (b) lookup по константе → свёртка на этапе подготовки, (c) невыразимый случай →
  `NotSupportedException` с сообщением; один SQL на всех SQL-провайдерах; `CommonTestSuite`
  воспроизводит jube query 12.

## Падающая форма

- jube query 12 (`GetEntityAnalysisModelsSynchronisationSchedulesByInstanceNameQuery.ExecuteAsync`):
  `y.ScheduleDate > tenants[y.TenantRegistryId.Value] && DateTime.UtcNow > y.ScheduleDate`.
- Обход в примере: обе стороны материализуются, флаг считается в C#.

## Гипотеза и область

- `Visitors/MemberTranslator`/`BaseExpressionVisitor` не распознают `IndexExpression`/индексатор по
  замкнутой коллекции и уходят в `VisitConstant`/member-access ветку, складывая объект коллекции в
  `Parameter`.
- Варианты реализации: (1) `CASE WHEN key = k1 THEN v1 …` (ключи константны), (2) derived `VALUES`
  (`PostgreSQL`/`SQL Server`) c `CROSS APPLY`/`JOIN`, (3) `IN`-список пар — самый переносимый, но
  тяжёлый при больших коллекциях. Для невыразимого случая — явный fail-fast.

## Файлы к изменению

- `src/nextorm.core/Visitors/{MemberTranslator,BaseExpressionVisitor,PredicateTranslator}.cs`,
  `Visitors/ExtendedScalarFunctionTranslator.cs` (если через `CASE`)
- `src/nextorm.core/DataContext/{SqlBuilder,SqlSourceRenderer}.cs` (derived `VALUES`/`CASE`)
- `src/nextorm.core/DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs` и провайдерные `*Dialect.cs`
- Тесты: `tests/nextorm.<p>.tests/SqlGenerationTests.cs` (6 провайдеров),
  `tests/nextorm.integration.tests/CommonTestSuite.*.cs`
- Доки EN+RU, `sql-capabilities-gap-analysis.md` §4 п.50

## Открытые вопросы

1. Транслировать или fail-fast? Минимум — не падать драйверной ошибкой; решение зафиксировать.
2. Если транслировать — какой предел размера коллекции, прежде чем уходить в `CASE`/derived `VALUES`
   vs параметризованный `IN`?
3. Как это сочетается с параметрами (`@pN` на значение) и план-кэшем (значения не должны попадать в
   ключ плана)?

## Источник

README портов, пункт 11; после закрытия — убрать/пометить в README примеров.
