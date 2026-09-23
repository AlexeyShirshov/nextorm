# TODO: `WITH ... UPDATE` / `WITH ... DELETE` (CTE не пробрасывается в мутацию)

> Рабочий план (bugfix RFC). Источник — **G18** из
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md), где помечен как
> **Gap (баг)**. linq2db-ориентир — `linq2db#3015` (там CTE в UPDATE уже работает и требует лишь
> переноса в source на части провайдеров; здесь пробел глубже — CTE **не эмитится вообще**). Связано с
> `todo_stored_procedures.md` (произвольные параметризованные команды). Публичного API фикс не меняет;
> если всё же появится — `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Баг:** CTE, поданный как источник в multi-table `UPDATE`/`DELETE`, теряется — генерируется
  `update … from <cte> …` **без** `with <cte> as (…)`, т.е. невалидный SQL.
- **Критерий приёмки:** `WITH c AS (…) UPDATE t SET … FROM/JOIN c …` и (PG) `WITH c AS (…) DELETE …
  USING c …` рендерятся и исполняются; соответствующая `SELECT`-форма с тем же CTE даёт тот же `WITH`;
  регресс-тесты на SQL-gen (SQLite/PG/SQL Server/MySQL) и интеграцию (PG/SQLite).

## 2. Воспроизведение (проверено генерацией на SQLite)

```csharp
var e = ctx.From<ISimpleEntity>();
var scope = ctx.With("c", e.Where(x => x.Id > 0).Select(x => new { x.Id }));
var joined = e.Join(scope.From("c"), (t, c) => t.Id == c["id"].AsInt);
joined.UpdateJoin().Set(p => p.Item1.Id, 0).ToSql();
```

Фактический SQL:

```sql
update simple_entity as 't1' set id = $p0 from c as 't2' where t1.id = t2.id
```

`with c as (…)` отсутствует → `no such table: c`. (`UPDATE ... FROM`/join сам по себе работает —
`UpdateJoinSqlGenerationTests.cs`, `UpdateJoin_ShouldChangeTargetFromJoinedRow`.)

## 3. Текущее состояние (проверено по коду)

- CTE-объявления живут в `EntityBuilder.Ctes` (`src/nextorm.core/Builders/EntityBuilder.cs:158`) и
  переносятся в `QueryCommand.Ctes` при `Select` (`EntityBuilder.cs:202`).
- `EntityBuilder.JoinCore` строит `JoinedEntityBuilder` с `Ctes = Ctes` — **только с левой стороны**
  (`EntityBuilder.cs:1292`); CTE присоединяемого источника (`ctx.With(…).From("c")`) отбрасывается.
  Правый источник — единственный, куда можно положить CTE (слева `From("c")` даёт не-writable
  `TableAlias`, а target обязан быть физической таблицей, `SqlBuilder.cs:660`).
- `JoinedMutationSource.Prepare` (`src/nextorm.core/Builders/Joins/JoinedMutationSource.cs:24`) делает
  `query.Select(...)` — если `query.Ctes` заполнен, он бы попал в `QueryCommand.Ctes`; сейчас он пуст из-за
  п. выше.
- Рендер мутации **не вызывает** `MakeWithClause`: `QueryPlanner.RenderUpdateJoin`
  (`src/nextorm.core/DataContext/QueryPlanner.cs:296`) и `RenderDeleteJoin` (`:268`) сразу зовут
  `MakeUpdateJoin`/`MakeDeleteJoin` (`SqlBuilder.cs:634`/`:540`), которые умеют только `update…from`/
  `delete…using`. Для сравнения, `QueryPlanner.RenderSource` (`:95-102`) и `SqlBuilder.MakeSelect`
  (`:63-64`) хойстят `WITH` через `SqlSourceRenderer.MakeWithClause` (`SqlSourceRenderer.cs:22`).
- `UpdateBuilder<TEntity>` (single-table `UPDATE … WHERE`) вообще не имеет `Ctes`
  (`src/nextorm.core/Builders/UpdateBuilder.cs:19`).
- `MutationCteQuery`/`CteQuery` покрывают только `INSERT ... RETURNING` body и read-CTE (`CteQuery.cs`).

## 4. Матрица провайдеров

`WITH <cte> AS (…)` перед мутацией и multi-table форма. Источники: MS Learn (`WITH … UPDATE … FROM`);
PostgreSQL 18 (`WITH`, `UPDATE … FROM`, `DELETE … USING`); SQLite (`WITH` перед `UPDATE`/`DELETE`,
`UPDATE … FROM`); MySQL 8.0 (`WITH` с `UPDATE`/`DELETE`); MariaDB KB (ограничения CTE в DML).

| Провайдер | Multi-table UPDATE | `WITH` перед мутацией | Форма | Источник |
|---|---|---|---|---|
| SQL Server | да | да | `WITH c AS (…) UPDATE <alias> SET … FROM t JOIN c ON …` | MS Learn |
| PostgreSQL | да (`FROM`) | да | `WITH c AS (…) UPDATE t SET … FROM c …`; DELETE: `… DELETE FROM t USING c …` | PG 18 |
| SQLite | да (`FROM`) | да | `WITH c AS (…) UPDATE t SET … FROM c …` | sqlite.org |
| MySQL | да (`JOIN`) | да (8.0) | `WITH c AS (…) UPDATE t JOIN c ON … SET …` | MySQL ref |
| MariaDB | да (`JOIN`) | **ограничения** | CTE в `UPDATE`/`DELETE` есть, но нельзя ссылаться на обновляемую таблицу и т.п. — проверить | MariaDB KB |
| ClickHouse | — (`SupportsUpdateJoin=false`) | — | — | — |
| InMemory | — | — | — | — |

**Единообразие:** фикс общий (`SupportsUpdateJoin` + `MakeUpdateJoin`/`MakeDeleteJoin`); MariaDB —
отдельно проверить/задокументировать ограничения.

## 5. Дизайн (bugfix, без нового публичного API)

1. **Слить CTE обеих сторон join.** В `EntityBuilder.JoinCore` (и в raw/CTE-путях
   `EntityBuilder.cs:1894`/`:1969`) переносить не только левый `Ctes`, но и `Ctes` присоединяемого
   источника. Хелпер `MergeCtes(left, right)`: объединить по имени (при коллизии — понятная ошибка),
   левые раньше правых.
2. **Донести до команды.** `JoinedMutationSource.Prepare` уже отдаёт `query.Select(...)` с `Ctes`; после
   п.1 `UpdateJoinCommand`/`DeleteJoinCommand` получат декларации через `MutationCommand.Source`.
3. **Хойст `WITH`.** В `QueryPlanner.RenderUpdateJoin` (`:296`) и `RenderDeleteJoin` (`:268`) по образцу
   `RenderSource` (`:95`): если `source.Ctes` непуст, вызвать `SqlSourceRenderer.MakeWithClause`, отдать
   билдеру клон без `Ctes` и префиксовать результат `withSql`. Общий параметр-провайдер, чтобы нумерация
   параметров совпала.
4. **Single-table `Update`/`DeleteFrom` с CTE** — отдельно не поддерживать в этом фиксе (open question),
   т.к. API-входа для CTE там нет.

## 6. Этапы

1. `MergeCtes` + проброс в `JoinCore`; SQL-gen тест на хойст `WITH` в `UPDATE ... FROM`.
2. Хойст в `RenderUpdateJoin` (SQLite/PG/SQL Server/MySQL).
3. То же для `RenderDeleteJoin` (PG `DELETE … USING`).
4. Регресс-тесты + проверка `CtesPlanHash`/кэша плана (declarations входят в plan-equality уже для
   SELECT; подтвердить, что мутационный путь их учитывает).
5. Доки/ограничения.

## 7. План тестов

- SQL-gen (`tests/nextorm.{sqlite,postgres,sqlserver}.tests`, `tests/nextorm.mysql.tests`): `WITH c AS (…)
  UPDATE … FROM/JOIN` — `StartWith("with c as (")` + `Contain("update")`.
- SQL-gen PG: `WITH c AS (…) DELETE FROM t USING c`.
- Интеграция: PG (`PostgresSpecificTests.cs`) и SQLite — реальный `UPDATE ... FROM cte`; PG `DELETE …
  USING cte`.
- Негатив: одинаковые имена CTE слева/справа → понятная ошибка.
- Покрытие: core/postgres/sqlserver/sqlite в `coverage.settings.xml`; MySQL — вне, зафиксировать.

## 8. Открытые вопросы

1. Сливать CTE join'а неявно или ввести явный вход (метод на `CteQuery` для мутаций)?
2. Коллизии имён CTE (`c` объявлен и слева, и справа) — ошибка или переопределение?
3. Нужен ли CTE в single-table `Update<T>().Where(…)` (сейчас недостижим) и в `DeleteFrom`?
4. Ограничения MariaDB для CTE в DML — gate-off (`SupportsWithInDml`) или документировать?
5. Не разъезжается ли `UpdateJoin` для `PostgreSQL` (цель без алиаса) при добавлении `WITH`.

## 9. Файлы к изменению

- `src/nextorm.core/Builders/EntityBuilder.cs` (`JoinCore:1292`, raw-пути `:1894`/`:1969`) — `MergeCtes`.
- `src/nextorm.core/DataContext/QueryPlanner.cs` (`RenderUpdateJoin:296`, `RenderDeleteJoin:268`) — хойст.
- `src/nextorm.core/DataContext/SqlBuilder.cs` (`MakeUpdateJoin:634`, `MakeDeleteJoin:540`) — при
  необходимости принять `withSql`.
- `src/nextorm.core/Builders/Joins/JoinedMutationSource.cs` — подтвердить проброс `Ctes`.
- Тесты: `tests/nextorm.{sqlite,postgres,sqlserver,mysql}.tests/*UpdateJoin*`,
  `tests/nextorm.integration.tests/{PostgresSpecificTests.cs,SqliteSpecificTests.cs}`.
- Доки: `docs/advanced/limitations.md` (+RU) при документировании ограничений.
