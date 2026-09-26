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
  1. метка рендерится как комментарий на всех 6 SQL-провайдерах;
  2. метка входит в ключ плана (иначе кэш отдаст SQL с чужой/отсутствующей меткой);
  3. метка не ломает синтаксис (экранирование `*/`, `/*`, переносов строк);
  4. отсутствие метки не добавляет аллокаций/ветвлений в горячий путь;
  5. in-memory принимает вызов как no-op (SQL нет).
- **Не путать:** `QueryCommand<TResult>.Hint(...)` — это оптимизаторный хинт (`OPTION (...)` /
  `/*+ ... */`); `Tag` — именно комментарий.

## 2. Провайдерная матрица (форм)

Проверено по **собственным докам провайдеров** (ссылка на источник в столбце «Источник»); `—` означает
«неприменимо/нет нативной формы».

| Провайдер | Нативная форма | Блочный комментарий | Источник |
|---|---|---|---|
| PostgreSQL | `/* tag */`; нативно `application_name` (в `pg_stat_activity`) | Да, `/* ... */` (не вкладывается) | PostgreSQL 17 docs, «Lexical Structure — Comments»; `pg_stat_activity.query` хранит текст запроса |
| SQL Server | `/* tag */`; нативно `OPTION (LABEL = 'tag')` (Query Store label) | Да, `/* ... */` (**вкладывается**: `/*` внутри открывает вложенный комментарий) | MS Learn «Slash Star (Block Comment) (Transact-SQL)», «OPTION clause (Transact-SQL)» (`LABEL`) |
| MySQL | `/* tag */`; `/*! */` — version comment, `/*+ */` — optimizer hint | Да, `/* ... */` | MySQL 8.4 Reference Manual, «Comments» |
| MariaDB | `/* tag */`; `/*! */` / `/*+ */` — как в MySQL | Да, `/* ... */` | MariaDB docs, «Comments» |
| SQLite | `/* tag */` | Да, `/* ... */` | SQLite docs, «SQL Syntax — Comments» |
| ClickHouse | `/* tag */`; нативно `SETTINGS log_comment = 'tag'` (в `system.query_log`) | Да, `/* ... */` | ClickHouse docs, «Syntax — Comments»; «SETTINGS» (`log_comment`) |
| InMemory | — | — | SQL не генерируется; вызов принимается и игнорируется |

### Единообразие провайдеров (решение)

Метка рендерится **одним портируемым блочным комментарием `/* tag */` на всех шести SQL-провайдерах**.
Нативные `OPTION (LABEL)` (SQL Server) и `SETTINGS log_comment` (ClickHouse) **отклонены**:

- они не портируемы (это отдельная форма на каждый диалект → `Supports*`/`Make*` хуки и слияние с уже
  собранными `OPTION (...)` / `SETTINGS` на SQL Server/ClickHouse, куда метка должна коалесцироваться);
- комментарий виден во всех профилировщиках/логах/Query Store/`system.query_log` так же, как нативная
  метка, поэтому выгоды от нативной формы нет;
- единая форма означает **один** код рендера в ядре (`SqlBuilder`), без per-provider кода и без
  `Supports*`-флага, который надо было бы обосновывать.

In-memory — no-op: `QueryCommand.Tag` просто переносится в план, `InMemoryQueryExecutor` его не читает.

### Порядок и экранирование (решение)

- Комментарий вставляется **сразу после ключевого слова `SELECT`** (`select /* tag */ ...`), до списка
  колонок. Для PostgreSQL/MySQL/MariaDB это не мешает хинтам: `RenderQueryHints` вставляет `/*+ ... */`
  на позицию `i + "select".Length`, т. е. `select /*+ hint */ /* tag */ ...` — hint остаётся первым и
  распознаётся `pg_hint_plan`/оптимизатором.
- Всегда рендерится как `/* ` + очищенная метка + ` */` (пробел после `/*`), чтобы метка, начинающаяся
  с `!` или `+`, не включила MySQL/MariaDB executable-comment `/*!` или optimizer-hint `/*+`.
- Очистка: `\r\n`, `\r`, `\n` → пробел (комментарий остаётся однострочным); `*/` → `* /`; `/*` → `/ *`
  (SQL Server **вкладывает** блочные комментарии, поэтому нейтрализуется и `/*`, а не только `*/`).
- При `Tag is null` (или пустой) в горячих путях лишь одна null-проверка: `Equals`/`GetHashCode`/`CopyTo`
  не аллоцируют и не ветвятся дальше.

## 3. C#-аналог и tier

CLR-аналога нет → **tier b**: новый публичный метод (метка — не функция, а свойство запроса).
Имя — `WithTag` (конвенция репозитория `With*`: `WithQuotedIdentifiers`/`WithKeywordCase`/`WithForJson`);
backlog-токен `TagQuery` остаётся как ссылка на linq2db. Согласовано с
`docs/specs/design/API-NAMING-REVIEW.md` (см. также раздел «Статус реализации»).

## 4. Дизайн и публичный API

```csharp
// EntityBuilder<TEntity> и EntityBuilder (named-table): fluent, возвращает новую копию builder-а
public EntityBuilder<TEntity> WithTag(string tag);
public EntityBuilder WithTag(string tag);

// QueryCommand<TResult>: применяется после Select
public QueryCommand<TResult> WithTag(string tag);

// QueryCommand: сама метка (get публичный, set internal), как у QuoteIdentifiers/KeywordCase
public string? Tag { get; internal set; }
```

- Хранение: `string? Tag` на `QueryCommand`; `internal string? Tag` на `EntityBuilder<TEntity>` /
  `EntityBuilder`. Переносится в команду в `Select`/`ToCommand` и в `CopyProjectionIndependentStateTo`
  (builder clone), а в `QueryCommand.CopyTo` — на live- и cache-clone.
- Рендер: `SqlBuilder.MakeSelect` — в блоке `if (!_ctx.ParamMode)` сразу после `Kw("select ")`.
  В parameter-mode (обход параметров) комментарий не рендерится.
- План-ключ: `QueryPlanEqualityComparer.Equals` сравнивает `Tag` (ordinal); `GetHashCode` добавляет
  непустую метку в `XxHash32` напрямую (как `Settings`). Отдельного `TagPlanHash`-поля нет — прямая
  инкрементальная хэш-функция дешевле и сохраняет контракт `Equals => equal hash` для любого пути
  подготовки (в т. ч. `dontCalculateHash`).
- Extend-only: `Tag == null` → ни новый SQL-текст, ни новое значение хэша, ни ветвление в рендере.

## 5. План этапов

1. `QueryCommand.Tag` + `CopyTo` + сравнение/хэш в `QueryPlanEqualityComparer`.
2. `QueryCommand<TResult>.WithTag` + XML-док.
3. `EntityBuilder<TEntity>.WithTag` и `EntityBuilder.WithTag` + проброс в `Select`/`ToCommand`/clone.
4. Рендер комментария в `SqlBuilder.MakeSelect` (все SQL-провайдеры) + экранирование.
5. In-memory: принять и проигнорировать (тест).
6. Тесты + доки (EN+RU) + реестр API + план.

## 6. План тестов

- SQL-gen на 6 провайдерах (`tests/nextorm.<provider>.tests/SqlGenerationTests.cs`):
  комментарий присутствует сразу после `SELECT`; экранирование `*/`, `/*` и переносов.
- План-ключ: `tests/nextorm.sqlite.tests/PlanKeyUniquenessTests.cs` — варианты `tag-a`/`tag-b`/без метки
  уникальны, пересборка даёт тот же ключ, `CloneForCache` сохраняет ключ.
- In-memory/core: `tests/nextorm.core.tests/InMemoryTests.cs` — `WithTag` не меняет результат.
- Опционально интеграция: метка видна в `system.query_log` (ClickHouse) / `pg_stat_activity`
  (PostgreSQL) — не обязательно для приёмки.

## 7. Файлы к изменению

- `src/nextorm.core/Query/QueryCommand.cs` (`Tag`), `QueryCommand.Clone.cs` (`CopyTo`),
  `QueryCommand.TResult.cs` (`WithTag`), `Query/QueryPlanEqualityComparer.cs`.
- `src/nextorm.core/Builders/EntityBuilder.cs` (`WithTag`, проброс состояния).
- `src/nextorm.core/DataContext/SqlBuilder.cs` (рендер комментария).
- Тесты: `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/SqlGenerationTests.cs`,
  `tests/nextorm.sqlite.tests/PlanKeyUniquenessTests.cs`, `tests/nextorm.core.tests/InMemoryTests.cs`.
- Доки: `docs/guide/17-query-hints.md` (+RU), `docs/advanced/api-reference.md` (+RU).
- Спеки: `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/roadmap/sql-capabilities-gap-analysis.md`,
  этот план-файл.

## 8. Открытые вопросы (решено)

1. **Нативная форма или портируемый комментарий?** → Портируемый `/* tag */` на всех SQL-провайдерах.
   `OPTION (LABEL)`/`log_comment` отклонены: не портируемы, требуют слияния с `OPTION`/`SETTINGS`,
   а комментарий уже виден в Query Store/`system.query_log`.
2. **Как сливать с `Hint`/`Settings`?** → Никак: `Tag` — отдельное состояние. Hint рендерится **перед**
   меткой (hint-сканер вставляет `/*+ ... */` сразу после `SELECT`, метка идёт следом); `Settings` —
   отдельное завершающее предложение ClickHouse.
3. **Имя: `WithTag` vs `Tag` vs `TagQuery`?** → `WithTag` (конвенция `With*`), свойство `Tag`.

## Статус реализации

Реализовано (uncommitted, worktree `issue-98-query-tag`):

- `QueryCommand.Tag` (public get / internal set), перенос в `CopyTo` (live + cache clone).
- `QueryPlanEqualityComparer`: ordinal-сравнение `Tag` в `Equals`, прямое добавление в `XxHash32`
  в `GetHashCode`; контракт `Equals => equal hash` сохранён.
- `QueryCommand<TResult>.WithTag`, `EntityBuilder<TEntity>.WithTag`, `EntityBuilder.WithTag`;
  проброс состояния в `Select`/`ToCommand`/builder-clone.
- `SqlBuilder.MakeSelect`: рендер `/* tag */` сразу после `SELECT`, экранирование `*/`/`/*`/CR/LF,
  `/* `-префикс. В param-mode не рендерится; при `Tag == null` путь не меняется.
- In-memory: `WithTag` принимается, результат не меняется.
- Тесты: SQL-gen на 6 провайдерах, план-ключ (SQLite), in-memory (core).
- Доки EN+RU, реестр API, gap-analysis.

Отложено: интеграционная проверка видимости метки в `system.query_log`/`pg_stat_activity`
(не требуется критерием приёмки; SQL-gen покрывает рендер).
