# TODO: Alias/колонки производной таблицы из спроецированного запроса (`From(projected)`)

> **Статус: реализовано (SHIPPED) в `1.0-b.1`.** `SqlSourceRenderer.MakeFrom` всегда алиасит
> derived-источник; `MemberTranslator` резолвит экспонируемые имена колонок через
> `FindInScopeQueryCommand`; alias-identity входит в ключ plan-кэша. Доки: `docs/guide/03-joins.md`,
> `docs/guide/05-sorting-and-paging.md` (+RU); gap-analysis §4 п.44.

> Рабочий план (design RFC). Источник: README репозитория примеров `~/sources/linq2db-apps-nextorm`,
> раздел «Engine gaps — verified on `nextorm 1.0.6-alpha`», пункт 6. Связано:
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) §4 п.44.

## Статус верификации (`nextorm 1.0.6-alpha`)

Воспроизводится харнессом `Gaps/` в `~/sources/linq2db-apps-nextorm` (`dotnet run --project Gaps`).

| Провайдер | Пункт 6 (order over `From(projected)`) | Пункт 3 (join к фильтрованному builder'у) |
|---|---|---|
| SQLite | **падает** (`SQLite Error 1: 'no such column: t1.Count'`) | — |
| PostgreSQL | работает | — |
| MySQL | работает | — |
| SQL Server | работает | — |

- **Пункт 3/проблема A исправлен в 1.0.6-alpha** (join к builder'у, несущему `Where`, теперь корректно
  алиасит `ON`) — отдельная работа не нужна. См. также §5 ledger.
- Остаётся **пункт 6**, и только на SQLite: ссылка на колонку производной таблицы, построенной из
  спроецированного `QueryCommand`, не совпадает с фактическим alias/именем колонки. На остальных
  провайдерах тот же план рендерится корректно.

## Пункт и цель

- **Проблема B:** `db.From(grouped).OrderByDescending(x => x.Count)` эмитит `order by t1.Count`, тогда
  как производная таблица (результат `GroupBy(...).Select(new { …, Count = count() })`) называет колонку
  иначе — SQLite не находит её. На PostgreSQL/MySQL/SQL Server SQL получается рабочим.
- **Цель:** единый источник правды для идентичности колонок производной таблицы: `FROM`, `ORDER BY`,
  фильтры и проекции ссылаются на фактический alias/имя.
- **Критерий приёмки:** SQL-generation тест: `From(grouped).OrderByDescending(x => x.Agg).Page(...)` —
  ссылка на фактический alias; alias-identity входит в ключ плана; одинаковое поведение на всех
  SQL-провайдерах; `CommonTestSuite` (пейджинг/order over projected) на контейнерах.

## Падающая форма

- Пейджинг/сортировка через derived table из спроецированного запроса (`From(QueryCommand<T>)` +
  `OrderByDescending`); обход в примере: не страничить через производную таблицу.

## Гипотеза и область

- Рассогласование между рендером колонок/алиасов производного источника
  (`DataContext/SqlSourceRenderer.cs`) и рендером ссылок на эти колонки
  (`Visitors/{AliasResolver,AliasFromProjectionVisitor}.cs`,
  `Query/{DefaultAliasProvider,DefaultColumnsProvider}.cs`).
- SQLite-специфичность (на остальных провайдерах работает) указывает на квотирование/регистр имени
  колонки при оборачивании в derived table.

## Файлы к изменению

- `src/nextorm.core/DataContext/{SqlSourceRenderer,QueryPlanner}.cs`,
  `Visitors/{AliasResolver,AliasFromProjectionVisitor}.cs`,
  `Query/{DefaultAliasProvider,DefaultColumnsProvider,QueryPlanEqualityComparer}.cs`,
  `Builders/Joins/JoinedEntityBuilder.cs`
- Тесты: `tests/nextorm.<p>.tests/SqlGenerationTests.cs` (6 провайдеров),
  `tests/nextorm.integration.tests/CommonTestSuite.Join.cs` (+ пейджинг)
- Доки EN+RU (при изменении наблюдаемого поведения), `sql-capabilities-gap-analysis.md` §4 п.44

## Открытые вопросы

1. Нужен ли alias-identity отдельным ключом плана или он выводится из структуры источников (сравнить с
   H1/H2 в [`todo_join_projection_mapping.md`](todo_join_projection_mapping.md))?
2. Почему PG/MySQL/SQL Server принимают текущий SQL: совпадение регистра или реально другой рендер
   (проверить, что «работает» — не случайность).

## Источник

README портов, пункт 6 (пункт 3 закрыт в 1.0.6-alpha); после закрытия — убрать/пометить в README
примеров.
