# TODO: комментарий-метка запроса (`TagQuery`)

> Tracking issue: [#98](https://github.com/AlexeyShirshov/nextorm/issues/98).

> Рабочий план (design RFC). Источник: подсекция «Пропущенная ось: shipped-поверхность
> `LinqExtensions`» в [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md)
> (Gap-пункт `TagQuery`). Аналог linq2db `LinqExtensions.TagQuery<T>(this IQueryable<T>, string tag)`.

## 1. Пункт и цель

- **Фича:** пользователь может прикрепить произвольную строку-метку к запросу, и nextorm рендерит её как
  **SQL-комментарий** (не оптимизаторный хинт) в генерируемом SQL, чтобы метку было видно в логах,
  профилировщике, `pg_stat_activity`, SQL Server Query Store, ClickHouse `system.query_log`.
- **Критерий приёмки:**
  1. метка рендерится как комментарий на всех SQL-провайдерах;
  2. метка входит в ключ плана (иначе кэш отдаст SQL с чужой/отсутствующей меткой);
  3. метка не ломает синтаксис (экранирование `*/`, переносов строк);
  4. отсутствие метки не добавляет аллокаций/ветвлений в горячий путь;
  5. in-memory принимает вызов как no-op (SQL нет).
- **Не путать:** `QueryCommand<TResult>.Hint(...)` — это оптимизаторный хинт (`OPTION (...)` /
  `/*+ ... */`); `Tag` — именно комментарий.

## 2. Провайдерная матрица (форм)

Источник по SQL Server — MS Learn «OPTION clause»/«Query hints» (проверено); по остальным — публичные
доки провайдеров; `—` означает «неприменимо».

| Провайдер | Нативная форма | Комментарий |
|---|---|---|
| PostgreSQL | `/* tag */` (либо `application_name`) | Блочный комментарий в любом месте; `pg_stat_activity` видит запрос. |
| SQL Server | `/* tag */`; нативно `OPTION (LABEL = 'tag')` | `LABEL` — родная метка для Query Store; конфликтует с уже собранными `OPTION (...)` — либо сливать, либо рендерить как комментарий. |
| MySQL / MariaDB | `/* tag */` | Плейн-комментарий; **не** `/*+ */` — та форма зарезервирована под optimizer hints. |
| SQLite | `/* tag */` | — |
| ClickHouse | `/* tag */`; нативно `SETTINGS log_comment='tag'` | `log_comment` попадает в `system.query_log`; тоже конфликтует со `SETTINGS`-клаузой. |
| InMemory | — | SQL не генерируется; вызов принимается и игнорируется. |

**Единообразие:** метка — комментарий, доступный везде → кросс-провайдерная поверхность
(`QueryCommand`/`EntityBuilder`), per-provider `Make*` не обязателен (форма комментария одна). Если
решим использовать нативные `OPTION (LABEL)`/`log_comment`, добавляются `Supports*`/`Make*` хуки и
слияние с `Hint`/`Settings`.

## 3. C#-аналог и tier

CLR-аналога нет → **tier b**: новый публичный метод (метка — не функция, а свойство запроса).
Имя SQL-токена: `TagQuery` (linq2db) либо `Tag`/`WithTag`. Согласовать с
`docs/specs/design/API-NAMING-REVIEW.md`.

## 4. Дизайн и публичный API (предложение)

```csharp
// EntityBuilder<TEntity>
public EntityBuilder<TEntity> WithTag(string tag);
// QueryCommand<TResult>
public QueryCommand<TResult> WithTag(string tag);
```

- Хранение: `string? Tag` на `QueryCommand` (рядом с `Hints`/`Settings`), в `CopyTo`/`Clone`.
- Рендер: `SqlBuilder`/`SqlSourceRenderer` вставляет `/* tag */` сразу после `SELECT` (до `/*+`-хинтов).
- Экранирование: `tag.Replace("*/", "* /")`, вырезать `\r`/`\n` (защита от инъекции в комментарий).
- План-ключ: добавить `Tag` в `Query/QueryPlanEqualityComparer.cs` + `TagPlanHash` (кэш не должен
  отдавать SQL с чужой меткой).
- Extend-only: `Tag == null` → путь не меняется.

## 5. План этапов

1. Добавить `WithTag` на `EntityBuilder<T>`/`QueryCommand<T>` + XML-док.
2. Рендер комментария в `SqlBuilder`/`SqlSourceRenderer` (все SQL-провайдеры).
3. План-ключ (`QueryPlanEqualityComparer`, план-хэш).
4. In-memory: принять и проигнорировать.
5. Тесты + доки (EN+RU) + реестр API.

## 6. План тестов

- SQL-gen (`tests/nextorm.*.tests/SqlGenerationTests.cs`): комментарий присутствует на всех 6 SQL-провайдерах;
  экранирование `*/`; два запроса с разными метками не делят план (SQLite/PostgreSQL).
- In-memory: вызов не падает и не меняет результат.
- Опционально интеграция: метка видна в `system.query_log` (ClickHouse) / `pg_stat_activity` (PostgreSQL).

## 7. Файлы к изменению

- `src/nextorm.core/Builders/EntityBuilder.cs`, `src/nextorm.core/Query/QueryCommand.cs` (`Tag`),
  `src/nextorm.core/DataContext/SqlBuilder.cs`/`SqlSourceRenderer.cs`, `Query/QueryPlanEqualityComparer.cs`.
- Доки: `docs/guide/17-query-hints.md` (+RU) либо новый раздел, `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`.

## 8. Открытые вопросы

1. Нативная форма (`OPTION (LABEL)` / `log_comment`) или портируемый комментарий? От этого зависит,
   нужны ли `Supports*`/`Make*`.
2. Как сливать метку с уже существующими `Hint`/`Settings` (порядок, дедупликация)?
3. Имя: `WithTag` vs `Tag` vs `TagQuery`.
