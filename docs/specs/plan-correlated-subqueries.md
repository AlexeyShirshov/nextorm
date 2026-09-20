# План реализации: общие коррелированные подзапросы

**Статус:** реализовано (SQL MVP, фазы 0–5) и запланированная граница для in-memory.
**Дата среза:** 2026-09-18 (рабочее дерево в середине рефакторинга — номера строк сверять повторно).

## Прогресс

| Фаза | Статус | Где |
|---|---|---|
| 0. Характеризация + ключ план-кэша | **Done** | `QueryPlanEqualityComparer` (Equals/GetHashCode по `OuterReferences`), `QueryCommand.CopyTo`; регрессия `CorrelatedQueryTests.CorrelatedPlanCache_ShouldDistinguishOuterColumns` |
| 1. Привязка внешних параметров | **Done** | `CorrelatedQueryExpressionVisitor.PushOuter`/`VisitLambda`/`ReferencesOuter`, `QueryCommand.QueryPreparer.PrepareColumns` |
| 2. Коррелированный скаляр (SQL) | **Done** | `CorrelatedQueryExpressionVisitor` скалярная ветка → `GetQueryCommand`; тесты `CorrelatedQueryTests`, `CommonTestSuite.CorrelatedQuery` |
| 3. `EXISTS`/`IN` вне `WHERE` | **Done** | тесты `CorrelatedInInSelect`, `CorrelatedExistsInOrderBy`, `CorrelatedExistsInSelect` |
| 4. Агрегатные терминалы | **Done (явная граница)** | `IsAggregateTerminal` → `NotSupportedException`; тест `AggregateTerminalInsideSubquery_ShouldThrowNotSupported` |
| 5. Глубина ≥ 2 | **Done (явная граница)** | `ContainsOuterRefMarker` → `NotSupportedException`; тест `NestedCorrelationDepth2_ShouldThrowNotSupported` |
| 6. In-memory | **Done (явная граница, MVP)** | `InMemoryQueryBuilder.GetPreparedQueryCommand` → `NotSupportedException` при `OuterReferences.Count > 0`; тесты `CorrelatedQueryInMemoryTests` |
| 7. Документация EN+RU | **Done** | `docs/guide/06-subqueries.md`, `docs/advanced/limitations.md`, `roadmap/sql-capabilities-gap-analysis.md` + RU-зеркала |

Полноценная построчная корреляция в in-memory (план §5, фаза 6, вариант «поддержать») и глубина ≥ 2
остаются follow-up задачами; текущее поведение — явный `NotSupportedException`, а не тихо неверный SQL.

**Связано:**
- [`roadmap/sql-capabilities-gap-analysis.md`](roadmap/sql-capabilities-gap-analysis.md) §4.1 и §4.2 — остаточные пробелы №1 и №2 («general correlated scalar subqueries», «correlated `APPLY`/`LATERAL`»);
- [`../advanced/limitations.md`](../advanced/limitations.md) — строка «General correlated scalar projection» (EN) и `docs/ru/advanced/limitations.md` (RU);
- `docs/guide/06-subqueries.md` — текущее описание подзапросов.

## 0. Резюме

Коррелированный подзапрос общего вида (скаляр в `SELECT`/`WHERE`/`ORDER BY`, а также `EXISTS`/`IN`/`ANY`/`ALL` вне `WHERE`) сегодня
падает с `InvalidOperationException: variable 'X' ... referenced from scope '', but it is not defined`. При этом **почти вся
инфраструктура корреляции уже существует** и работает для `EXISTS`/`IN`/`ANY`/`ALL` в `WHERE`:

| Механизм | Где |
|---|---|
| Маркер внешней ссылки `OuterRefMarker<T>` | `src/nextorm.core/OuterRefMarker.cs` |
| Замена `s.Id` → `OuterRefMarker<T>(idx).Ref` + регистрация в `IQueryRegistry.AddOuterReference` | `ExpressionExtensions.cs:156-170` (`ReplaceConstantsExpressionVisitor.VisitMember`) |
| Рендер маркера как `alias.column` | `Visitors/MemberTranslator.cs:198-224` |
| Рендер скалярного подзапроса как `(select ...)` | `Visitors/MemberTranslator.cs:402-424` (`VisitIndex`) |
| Принудительный алиас внешнего `FROM` при наличии внешних ссылок | `DataContext/SqlBuilder.cs:74` (`needAlias = hasJoins || _queryProvider.OuterReferences?.Count > 0`) |
| Хранилище внешних ссылок на команде | `Query/QueryCommand.cs:196,259` |

Не хватает трёх вещей: **(1)** привязки внешнего параметра в позициях, которые не являются телом `SqlFunctions.Sql`-вызова;
**(2)** распространения этой привязки на скалярную ветку (терминалы `First`/`Single`/`Any` на `QueryCommand`);
**(3)** исполнения коррелированных подзапросов в in-memory провайдере. Плюс отдельно чинится план-кэш (§3.4), иначе фича
опасна: два запроса, отличающиеся только внешней колонкой, могут переиспользовать чужой план.

## 1. Что уже есть и где ломается

### 1.1 Как корреляция собирается сегодня (только `WHERE` + `SqlFunctions.Sql`)

`QueryPreparer.PrepareWhere` (`Query/QueryCommand.QueryPreparer.cs:431`) прогоняет условие через
`CorrelatedQueryExpressionVisitor` (далее CQEV). Для `s => SqlFunctions.Sql.exists(inner)`:

1. `CQEV.VisitLambda` (`Visitors/CorrelatedQueryExpressionVisitor.cs:340-368`) кладёт `s` в стек `_outerParams` **только если тело
   лямбды — прямой вызов `SqlFunctions.Sql`** (`:350-364`);
2. ветка `exists`/`any`/`all` (`:69-135`) или `@in` (`:136-173`) зовёт `GetQueryCommand(exp)`;
3. `GetQueryCommand` (`:242-288`) выполняет `ReplaceConstantsExpressionVisitor(_outerParams, _queryProvider)` и заменяет `s.Id`
   на `OuterRefMarker<int>(idx).Ref`, регистрируя исходный `s.Id` в **внешней** команде (`_queryProvider` — это внешняя команда);
4. внутренняя команда готовится отдельно (`cmd.PrepareCommand`), её `WHERE` теперь содержит маркер;
5. SQL скалярного/`exists`-подзапроса рендерится `SqlBuilder.MakeSelect(inner)`, которому передан `visitor.QueryProvider`
   **внешней** команды, поэтому `MemberTranslator` (`:198-224`) достаёт `OuterReferences[idx]` и печатает `alias.column`.

### 1.2 Почему общий случай не работает

- **Проекции.** `PrepareColumns` (`QueryPreparer.cs:192-361`) зовёт `CQEV.Visit` **по каждому аргументу конструктора отдельно**
  (`:221`, `:242`, `:277`), а не по лямбде `it => new { ... }`. Значит `VisitLambda` никогда не видит `it`, `_outerParams` пуст,
  внешний член не заменяется, и `Expression.Lambda<Func<object?, QueryCommand>>(body, p).Compile()` (`CQEV:221`) падает на
  свободном параметре `it`.
- **`WHERE` со скаляром.** `Where(it => it.Id == sub.First())`: тело — `BinaryExpression`, не вызов `SqlFunctions.Sql`, поэтому
  `VisitLambda` не пушит `it`. Терминал `.First()` обрабатывается скалярной веткой `CQEV:201-237`, которая заменяет только
  closure-константу (`ReplaceConstantExpressionVisitor`) и не трогает внешние члены → тот же `Compile()` падает.
- **`EXISTS`/`IN`/`ANY`/`ALL` вне `WHERE`.** В `SELECT`/`ORDER BY` та же причина: `_outerParams` не заполнен.
- **Вложенность depth ≥ 2.** Даже в `WHERE`: внешние ссылки промежуточной команды регистрируются на промежуточной команде
  (её собственный `_queryProvider`), а рендер вложенной команды идёт с `QueryProvider` **верхней** команды
  (`NormSqlTranslator.cs:147`, `:193`) → маркер не разрешается. Сегодня это тоже падает, но на другом свободном параметре.

### 1.3 Подтверждённая характеризация (scratch-тесты на SQLite, SQL-generation без БД)

| Сценарий | Текущий результат |
|---|---|
| `Select(it => new { it.Id, sid = sub.Where(s => s.Id == it.Id).Select(s => s.Id).First() })` | `InvalidOperationException: variable 'it' ... not defined` |
| `Where(it => it.Id == sub.Where(s => s.Id == (int)it.Id).Select(s => s.Id).First())` | то же (`it`) |
| `Select(it => new { it.Id, has = SqlFunctions.Sql.exists(sub.Where(s => s.Id == it.Id)) })` | то же (`it`) |
| вложенный `exists` depth 2 в `WHERE` | `InvalidOperationException: variable 's' ... not defined` |

Scratch-файлы удалены; в фазе 0 эти сценарии становятся постоянными тестами.

### 1.4 In-memory

`OuterRefMarker` обрабатывается **только** в `MemberTranslator` (SQL-рендер); grep по провайдеру — совпадений нет. Ветка
`CQEV` для `!_forPrepare` (in-memory) для `exists`/`any`/`all` компилирует `Any<TResult>(cmd)` и **выполняет подзапрос сразу**
(`:81-101`, `:424-429`), т.е. корреляция per-row невозможна. Многоколоночные проекции идут через
`RowMaterializerBuilder.Build` (`DataContext/InMemoryRowMaterializer.cs:51`), где про маркеры вообще не знают. Компиляция
предикатов — `DataContext/InMemoryConditionFactory.cs:22-74` (`ParamCollectorVisitor` + `ParamLocalSubstitutionVisitor`), тоже
без маркеров. Вывод: in-memory — отдельный полноценный workstream (фаза 6).

## 2. Цель и не-цели

### Цель

1. Коррелированный **скалярный** подзапрос (одиночное значение) в `SELECT`, `WHERE`, `ORDER BY`, `HAVING` — SQL-провайдеры.
2. Коррелированные `EXISTS`/`IN`/`ANY`/`ALL` в тех же позициях (сейчас только `WHERE`).
3. Детерминированная семантика вложенности: depth 1 — поддержано; depth ≥ 2 — либо работает, либо **явный `NotSupportedException`
   с понятным сообщением** (никогда — тихо неверный SQL).
4. Корректный ключ план-кэша: `OuterReferences` участвуют в `Equals`/`GetHashCode` (иначе п.1–2 нельзя включать).
5. In-memory: исполнение коррелированных подзапросов per-row.
6. Документация EN+RU, `limitations`, gap-analysis.

### Не-цели

- Коррелированный `APPLY`/`LATERAL` (roadmap §4.2) — использует тот же outer-reference API, но своя поверхность `FROM`.
  Проектируем общий примитив, реализацию `APPLY` не делаем.
- Composable raw SQL, `SelectMany`/`GroupJoin` на SQL-провайдерах, DML, navigation properties.
- Новый публичный API: синтаксис уже выразим существующими терминалами `QueryCommand<T>`.

## 3. Жёсткие инварианты

1. **SQL для существующих коррелированных `EXISTS`/`IN`/`ANY`/`ALL` не меняется байт-в-байт** (golden-тесты
   `tests/nextorm.*.tests/SqlGenerationTests.cs` — основной оракул).
2. **Публичный API расширяется только аддитивно** (`api-design`: extend-only). `CorrelatedQueryExpressionVisitor` — уже
   публичный; новые члены добавлять `internal`/`public` без изменения существующих сигнатур.
3. **Число и порядок `_params.Add` не меняются** для уже работающих запросов (двухпроходность `_paramMode`).
4. **`OuterReferences` входят в ключ плана** (новый инвариант корректности).
5. **CRLF** во всех правках: `perl -pi -e 's/\r?\n/\r\n/g' <file>`.
6. **Сборка `TreatWarningsAsErrors`** — 0 предупреждений; подавления только явные, с обоснованием.
7. Внешняя ссылка привязывается к ближайшему корректному внешнему scope; при невозможности — явный `NotSupportedException`.

## 4. Сеть безопасности

| Проверка | Покрытие |
|---|---|
| `tests/nextorm.integration.tests/CommonTestSuite.CorrelatedQuery.cs` | 1 тест `TestWhere` (`:11`) — расширить до матрицы позиций и вложенности |
| `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs` | golden-кейсы подзапросов в `SELECT`/`WHERE`/`ORDER BY` (`:439-461`, `:623-669`) |
| `tests/nextorm.*.tests/SqlGenerationTests.cs` | exact-string SQL без БД: sqlite, sqlserver, postgres, mysql, mariadb, clickhouse |
| `tests/nextorm.core.tests/InMemoryTests.cs` | in-memory exists (`:231`) — добавить коррелированные скаляр/exists |
| Характеризационные тесты (фаза 0) | фиксируют `InvalidOperationException` до реализации и позитивный результат после |
| SQL Server / PostgreSQL / MySQL / ClickHouse | Testcontainers по `.opencode/skills/running-integration-tests/SKILL.md` |

## 5. Фазы

| # | Фаза | Риск | Зависит |
|---|---|---|---|
| 0 | Характеризация + инвариант план-кэша | S | — |
| 1 | Обобщённая привязка внешних параметров (SQL-подготовка) | M | 0 |
| 2 | Рендер коррелированного скаляра (SQL) | M | 1 |
| 3 | `EXISTS`/`IN`/`ANY`/`ALL` вне `WHERE` | S–M | 1 |
| 4 | Агрегатные терминалы в подзапросе (`Count`/`Sum`/`Min`/`Max`/`Avg`) | M–H | 2 |
| 5 | Вложенность depth ≥ 2 | M–H | 1–3 |
| 6 | In-memory (per-row исполнение) | H | 1–2 |
| 7 | Документация EN+RU, `limitations`, gap-analysis | S | 2–6 |

Порядок: `0 → 1 → 2 → 3 → 4 → 5`; фаза 6 — независимо после 1–2; фаза 7 — в конце. Фазы 4 и 5 допускают откладывание
(фичу можно выпустить без них, но с явным `NotSupportedException`).

### Фаза 0 — Характеризация и план-кэш

1. Перенести scratch-кейсы §1.3 в постоянные тесты: SQL-generation (SQLite) + golden SQL по всем провайдерам.
2. **План-кэш.** `QueryPlanEqualityComparer.Equals` (`Query/QueryPlanEqualityComparer.cs:80-84`) сравнивает `ReferencedQueries`, но
   `OuterReferences` не сравниваются и не хешируются (`GetHashCode`, `:160-220`). Значит `where ... exists(inner(s.Id == o.A))` и
   `... o.B` (одинаковый тип) дают равные планы → переиспользование чужого SQL. Исправление:
   - добавить `Equals(x.OuterReferences, y.OuterReferences)` (структурно, через `ExpressionPlanEqualityComparer`);
   - добавить `OuterReferences` в `GetHashCode` (или новый `OuterReferencesPlanHash`);
   - regression-тест: две команды, отличающиеся только внешней колонкой, обязаны дать разный SQL при включённом кэше.
3. Проверить, не ломает ли это существующие планы (golden + plan-cache тесты `tests/nextorm.sqlite.tests/PlanCacheTests.cs`).

**Выход:** красные характеризационные тесты + зелёный regression план-кэша.

### Фаза 1 — Обобщённая привязка внешних параметров

Задача: `CQEV` должен знать внешние параметры текущей команды независимо от формы тела лямбды.

1. Дать `CQEV` возможность получать внешние параметры команды:
   - предпочтительно — передавать их при создании визитора в `QueryPreparer` (`:207`, `:241`, `:265`, `:391`, `:431`);
   - источник параметра: `cmd._exp.Parameters[0]` (проекция), `cmd._condition.Parameters[0]` (условие),
     `sort.SortExpression.Parameters[0]` (сортировка). Для `srcType`-команд без лямбды внешних параметров нет.
   - API визитора: `internal void PushOuter(ParameterExpression)` (или ctor-параметр) **аддитивно**, без ломания
     существующих публичных конструкторов.
2. Сохранить текущее поведение `VisitLambda` для `SqlFunctions.Sql`-тел (не дублировать: повторный push безвреден — `Contains`
   проверяется по значению).
3. **Унифицировать скалярную ветку** `CQEV:201-237` с `GetQueryCommand`: перед компиляцией делегата прогнать
   `ReplaceConstantsExpressionVisitor(_outerParams, _queryProvider)` по `node.Object`, чтобы внешние члены стали
   `OuterRefMarker` и зарегистрировались во внешней команде. Затем — существующая логика closure-инъекции
   (`TypeExpressionVisitor` Has1/Has2) и `ReplaceQueryCommand` (`:290-338`).
4. Не менять ветки `exists`/`any`/`all`/`@in` по существу — они уже используют `GetQueryCommand`.

**Инвариант фазы:** golden `exists`-SQL не меняется.

### Фаза 2 — Рендер коррелированного скаляра

1. Убедиться, что после фазы 1 цепочка работает: скалярный терминал заменяется на лямбду `(IQueryRegistry) => ReferencedQueries[idx]`,
   `MemberTranslator.VisitIndex` (`:402-424`) печатает `(select ...)`, маркер внутри — `alias.column`, `SqlBuilder.needAlias`
   включает алиас внешнего `FROM`.
2. Целевой SQL (SQLite-подобный):
   ```sql
   select t1.id, (select s.id from simple_entity s where s.id = t1.id limit 1) as sid
   from complex_entity t1
   ```
   Проверить отсутствие коллизий алиасов: внутренний `SqlBuilder` получает общий `IAliasProvider` (`NormSqlTranslator.cs:147`,
   `MemberTranslator.cs:412`), поэтому счётчик алиасов общий — зафиксировать тестом.
3. Терминалы: `First`/`Single` → `Limit = 1` (уже в `ReplaceQueryCommand:300-307`); `FirstOrDefault`/`SingleOrDefault` → `Limit = 1`
   (проверить, что префикс `First`/`Single` их покрывает — да; добавить golden и NULL-семантику).
4. **Открытый риск:** внешняя ссылка на член join-проекции (`p.Item1.Id`). `ReplaceConstantsExpressionVisitor.VisitMember`
   заменяет только **одно** звено от параметра (`p.Item1`), затем `MemberTranslator` не разворачивает `marker.Ref.Id`
   в колонку. Либо расширить обработку цепочки (регистрировать полный путь `p.Item1.Id` / разворачивать `marker.Ref.<member>`),
   либо в этой фазе явно отклонять с `NotSupportedException` и вынести в отдельный item. Спайк ≤0.5 дня.
5. Golden SQL по 6 диалектам + интеграционные на SQLite (и Testcontainers, если доступны).

### Фаза 3 — `EXISTS`/`IN`/`ANY`/`ALL` вне `WHERE`

1. После фазы 1 ветки `exists`/`any`/`all`/`@in` должны работать в `SELECT`/`ORDER BY`/`HAVING` без изменений — закрыть тестами.
2. Проверить `Any` на `EntityBuilder` в проекции (`CommonTestSuite.SqlCommand.cs:459-461` — сейчас только non-correlated).
3. Зафиксировать ограничение `SqlFunctions.Sql.any`/`all` на SQLite/ClickHouse (SQL-корректность — забота диалекта/БД), не менять.

### Фаза 4 — Агрегатные терминалы в подзапросе (опционально, можно отложить)

`EntityBuilder.Count()`/`Sum(...)`/`Min(...)` — терминалы, которые **исполняют** запрос и внутри дерева-выражения выглядят как
`MethodCallExpression` на `EntityBuilder<TEntity>`. Ветка `CQEV:177-200` строит из `node.Object` «сырую» команду, а
`ReplaceQueryCommand` не знает агрегатов → `(select ...)` без агрегата = неверный SQL.

1. Научить `ReplaceQueryCommand` (или отдельный распознаватель) переписывать проекцию внутренней команды на соответствующий
   `SqlFunctions.Sql.count()/sum()/...` (ср. `EntityBuilder.AggregateCore`, `Builders/EntityBuilder.cs:777-780`) и ставить `SingleRow = true`.
2. Альтернатива (если переписывание проекции рискованно): явный `NotSupportedException` с подсказкой использовать
   `Select(x => SqlFunctions.Sql.count()).First()`.
3. Тесты: `count`, `sum`, `min`, `max`, `avg` в скалярной проекции, коррелированные и нет.

### Фаза 5 — Вложенность depth ≥ 2

1. Диагноз: внешняя ссылка промежуточной команды регистрируется на ней, а рендер идёт с `QueryProvider` верхней команды.
2. Варианты:
   - **(a) запретить:** детектировать в `CQEV`, что `node` подзапроса уже содержит `OuterRefMarker` (подзапрос внутри подзапроса),
     и бросать `NotSupportedException("Вложенная корреляция глубины > 1 не поддерживается")`. Дёшево, безопасно, честно.
   - **(b) поддержать:** пробрасывать цепочку registry (родитель → текущая) в `SqlBuilder`/`BaseExpressionVisitor`, чтобы
     `MemberTranslator` разрешал маркер в registry того scope, где он зарегистрирован. Дороже, трогает горячий рендер.
3. Рекомендация: сделать (a) в этой фазе, (b) — задокументированным backlog-item. Критерий — отсутствие тихо неверного SQL.

### Фаза 6 — In-memory (per-row исполнение)

1. Ввести обработку `OuterRefMarker<T>(idx).Ref` в in-memory конвейере: при исполнении **внутреннего** запроса на каждую
   внешнюю строку подставлять значение внешнего выражения.
   - Механизм: при построении делегата row-материализатора внешней команды (для `exists`/`Any`/`AnyAsync` и скалярных
     терминалов) маркер заменяется на доступ к текущей внешней строке (`Expression` над `TEntity`), а не на константу.
2. Переработать ветки `CQEV` для `!_forPrepare`: `exists`/`any`/`all` сейчас выполняют `Any<TResult>(cmd)` **один раз** при сборке
   материализатора (`:81-101`) — должно стать замыканием `(TEntity row) => inner(row).Any()`.
3. Скалярные терминалы: `ReplaceQueryCommand` при `!_forPrepare` бросает `NotImplementedException` (`:293`) — реализовать
   возврат скаляра через `GetPreparedQueryCommand`/`CreateEnumerator` с подстановкой внешней строки.
4. Многоколоночные проекции — через `RowMaterializerBuilder.Build` (`InMemoryRowMaterializer.cs:51`), а не только `OneColumn`.
5. Условия внутренней команды — `InMemoryConditionFactory` должна уметь подставлять внешние маркеры (расширить
   `ParamLocalSubstitutionVisitor`/добавить `OuterRefSubstitutionVisitor`).
6. Тесты в `tests/nextorm.core.tests/InMemoryTests.cs` (коррелированные скаляр/exists, depth 1).
7. Если объём окажется чрезмерным — допустимо разделить на «SQL MVP» и «in-memory follow-up», но тогда `InMemoryDataContext`
   обязан бросать понятный `NotSupportedException` на коррелированный подзапрос (сейчас он либо падает невнятно, либо
   считает одно значение). Решение зафиксировать до начала фазы.

### Фаза 7 — Документация

1. `docs/guide/06-subqueries.md` + `docs/ru/guide/06-subqueries.md`: убрать оговорку «correlated only via `EXISTS`/`IN`/...»,
   добавить примеры correlated scalar в `SELECT`/`WHERE`/`ORDER BY`.
2. `docs/advanced/limitations.md` + `docs/ru/...`: удалить строку «General correlated scalar projection»; оставить строку про
   depth ≥ 2 / join-projection, если они остаются ограничениями.
3. `docs/specs/roadmap/sql-capabilities-gap-analysis.md`: обновить §4.1 (status → partial/done), таблицу §2 (`Correlated subquery`,
   `Scalar subquery in SELECT/WHERE/ORDER BY`) и «Future workstreams».
4. `docs/advanced/api-reference.md` + RU — только если добавлены публичные члены (ожидается: нет).
5. Проверить `docs/docfx.json` сборку: `dotnet docfx docs/docfx.json`.

## 6. Протокол проверки фазы

1. `dotnet build nextorm.sln -c Debug` → 0 warnings / 0 errors.
2. `dotnet test tests/nextorm.core.tests -c Debug` (in-memory) и `dotnet test tests/nextorm.sqlite.tests -c Debug` → Failed 0.
3. SQL-generation без БД: `dotnet test tests/nextorm.sqlite.tests -c Debug`,
   `dotnet test tests/nextorm.sqlserver.tests -c Debug`, `dotnet test tests/nextorm.postgres.tests -c Debug`,
   `dotnet test tests/nextorm.mysql.tests -c Debug`, `dotnet test tests/nextorm.mariadb.tests -c Debug`,
   `dotnet test tests/nextorm.clickhouse.tests -c Debug`.
4. Интеграционные при доступном Podman:
   ```bash
   DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
     dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
   ```
   Без `DOCKER_HOST` зелёный прогон доказывает только SQLite (см. `ProviderTestSuite`).
5. Golden-дифф: SQL существующих `exists`-запросов не изменился (сравнение exact-string).
6. Ручная сверка плана: `EXPLAIN QUERY PLAN` на SQLite для коррелированного скаляра (зависимый подзапрос должен ссылаться на
   внешний алиас).

### Результаты прогона (2026-09-18)

* `dotnet build nextorm.sln -c Debug` — 0 warnings / 0 errors.
* SQL-generation (без БД): core 153, sqlite 195, sqlserver 167, postgres 151, mysql 31, mariadb 7, clickhouse 47 — 0 failed.
* Полный `tests/nextorm.integration.tests` через Podman (`DOCKER_HOST=...podman-user.sock`):
  **879 тестов, 852 succeeded, 27 capability-skip, 0 failed** (SQLite + PostgreSQL + SQL Server + MySQL + ClickHouse).
* Регрессия план-кэша подтверждена: при временно отключённом сравнении `OuterReferences` тест
  `CorrelatedPlanCache_ShouldDistinguishOuterColumns` падает (SQL для `it.B` переиспользует `t1.a`), с фиксом — зелёный.
* Регрессия кэша делегатов подтверждена: при временно отключённом guard'е `ContainsOuterRefMarker(body)` тест
  `CorrelatedExpressionsCache_ShouldNotReuseMarkerIndex` падает с `ArgumentOutOfRangeException` (маркер чужого индекса),
  с фиксом — зелёный. HAVING-guard покрыт `CorrelatedSubqueryInHaving_ShouldThrowNotSupported`.
* Регрессия alias подтверждена репро из §9.1.6 (до фикса второй подзапрос рендерился как `from x as 't3' where t2.id = ...`).

## 7. Риски

| Риск | Митигация |
|---|---|
| План-кэш отдаёт чужой SQL (внешние ссылки не в ключе) | Фаза 0 как **предусловие**; regression-тест до включения фичи |
| Скалярная ветка `CQEV` переплетена с closure-инъекцией и кэшем выражений | Унификация через `GetQueryCommand`; golden `exists` не меняется; точечные характеризационные тесты |
| Алиасы внешний/внутренний (`t1`/`t2`) конфликтуют | §9.1.6: source scope в `DefaultColumnsProvider` + `includeOuterScopes` для внешних маркеров; регрессионные тесты scalar/exists (SQL-gen + интеграционные) |
| Внешняя ссылка на join-проекцию (`p.Item1.Id`) | Спайк в фазе 2; fallback — явный `NotSupportedException`, отдельный item |
| Кэш делегатов подзапроса хранит чужой `OuterRefMarker(idx)` | §9.5: тело с маркером не кэшируется; регрессионный тест `CorrelatedExpressionsCache_ShouldNotReuseMarkerIndex` |
| Подзапрос в `HAVING` даёт молча неверный SQL | §9.4: guard `ContainsSubquery` → `NotSupportedException`; тест `CorrelatedSubqueryInHaving_ShouldThrowNotSupported` |
| Depth ≥ 2: тихо неверный SQL | Фаза 5(a) — детект маркера в подзапросе → `NotSupportedException` |
| Агрегаты в подзапросе дают неверный SQL | Фаза 4: либо переписывание проекции, либо явный `NotSupportedException` (никогда — молча) |
| In-memory объём недооценён | Фаза 6 отделена; допустим осознанный `NotSupportedException` как MVP-граница |
| Рабочее дерево в середине чужого рефакторинга (сборка нестабильна) | Не трогать файлы вне задачи; при падении сборки из-за чужих правок — фиксировать, не чинить; строки сверять перед правкой |
| Порядок `_params` ломается при двухпроходном рендере | Не менять порядок `Visit`; golden-сравнение параметров |

## 8. Критерии готовности

- Коррелированный скалярный подзапрос работает в `SELECT`/`WHERE`/`ORDER BY` на SQLite, SQL Server, PostgreSQL, MySQL/MariaDB,
  ClickHouse (golden + интеграционные тесты).
- `EXISTS`/`IN`/`ANY`/`ALL` работают во всех позициях, golden `WHERE`-вариант не изменился.
- `OuterReferences` участвуют в ключе план-кэша; regression-тест зелёный.
- Depth ≥ 2 и join-projection дают либо корректный SQL, либо явный `NotSupportedException`.
- In-memory: коррелированный скаляр/exists depth 1 исполняется per-row, либо документированная MVP-граница с понятной ошибкой.
- Публичный API не сломан (только аддитивные изменения).
- Документация EN+RU обновлена; DocFX собирается.

## 9. Открытые вопросы

Статус после реализации (2026-09-18):

1. **Общий outer-reference API для `APPLY`/`LATERAL`.** Оставлено `internal` (`PushOuter`/`OuterScope`), как и предлагалось;
   публичный контракт не вводили. Открыто для будущего workstream (roadmap §4.2).
2. **Семантика скаляра без строки. — ДОРАБОТАНО.** Изначально `FirstOrDefault<int>` вёл себя как `First<int>` (кидал),
   потому что терминал не доносился до материализации. Исправлено: `ReplaceQueryCommand` записывает `DefaultOnEmpty`/
   `SingleScalar` в целевую команду, `PrepareColumns` переносит `DefaultOnNull` в `SelectExpression`, а
   `RowMapperFactory.MapColumn` (и override SQL Server) на SQL NULL возвращает `default(T)` для `*OrDefault`. Итог:
   nullable-проекция без строки → `null`; non-nullable value + `*OrDefault` → `default`; non-nullable `First`/`Single` → исключение.
   `Single`/`SingleOrDefault` при >1 строке: команда рендерится с `limit 2`, и провайдеры с проверкой кардинальности
   (PG/SQL Server/MySQL/MariaDB/ClickHouse) бросают; SQLite её не проверяет, поэтому `Single`/`SingleOrDefault` в скалярном
   подзапросе отклоняются `NotSupportedException` (`ISqlDialect.EnforcesScalarSubqueryCardinality` = false у SQLite).
   Флаги учтены в `QueryPlanEqualityComparer`/`SelectExpressionPlanEqualityComparer`/`CopyTo` и в ключе маппера.
   Тесты: `CommonTestSuite.CorrelatedQuery.cs` (value-OrDefault, Single value/throw, gated по
   `ITestProvider.EnforcesScalarSubqueryCardinality`), `SqliteSpecificTests.CorrelatedScalarSingle_ShouldThrowNotSupported`,
   обновлён `SubQuerySelectOrderSingle_ShouldReturnData`. Задокументировано в `limitations` (EN+RU).
3. **Коррелированная ссылка на вычисленное внешнее выражение** (`c.Id + 1`). Поддерживается:
   `ReplaceConstantsExpressionVisitor.VisitMember` переписывает каждый внешний член, а арифметика уходит обычному
   транслятору; покрыто `it.Id + 100` (интеграционный тест). `upper(c.Name)` с внешней функцией — не проверялось.
4. **`HAVING`.** Не поддержан: подзапрос (коррелированный и любой) в `HAVING` теперь даёт `NotSupportedException`
   (guard `ContainsSubquery` в `PrepareGrouping`, тест `CorrelatedSubqueryInHaving_ShouldThrowNotSupported`). До guard
   генерировался молча неверный SQL вида `having id = cast($innercast(.id ...))`. Полная поддержка (прогон `CQEV` над
   `_having`, `PreparedHaving`) — отдельная задача.
5. **Кэш выражений подзапросов.** **Найден реальный баг.** `GetQueryCommand` кэшировал скомпилированный делегат по `exp`,
   но `OuterRefMarker(idx)` зашит в тело делегата, а `idx` зависит от числа внешних ссылок *текущей* внешней команды.
   Переиспользование давало `ArgumentOutOfRangeException` или чужую колонку. **Исправлено:** тело, содержащее
   `OuterRefMarker`, больше не кладётся в `DataContextCache.ExpressionsCache` (компилируется на каждую внешнюю команду);
   регрессионный тест `CorrelatedExpressionsCache_ShouldNotReuseMarkerIndex`.

### 9.1. Новые находки вне исходных вопросов

6. **Alias у нескольких соседних подзапросов одного типа (пред-существующий). — ИСПРАВЛЕНО.** При `needAlias`
   (корреляция или join) два подзапроса к одной сущности в одном запросе получали alias первого из них: второй
   рендерился как `(select ... from x as 't3' where t2.id = ...)` — WHERE ссылался на `t2`. Затрагивало и коррелированный
   скаляр, и `EXISTS`.
   **Фикс:** `DefaultColumnsProvider` получил *source scope* — базовый индекс, отмечающий начало записей текущей
   команды; `SqlBuilder.MakeSelect` открывает scope (`PushSourceScope`/`PopSourceScope`), а `FindAlias(ParameterExpression,
   …)` не смотрит ниже него. Для внешней ссылки (`OuterRefMarker`) резолв идёт через
   `AliasResolver.GetOuterAliasFromParam` с `includeOuterScopes: true`, потому что её параметр принадлежит объемлющей
   команде. Регрессионные тесты: `CorrelatedSiblingSubqueriesOfSameType_ShouldUseTheirOwnAlias`,
   `CorrelatedSiblingExistsOfSameType_ShouldUseTheirOwnAlias` (SQL-gen) и
   `CorrelatedSiblingSubqueriesOfSameType_ShouldEvaluatePerRow` (интеграционный, все провайдеры).

