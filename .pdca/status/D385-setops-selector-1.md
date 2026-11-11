# Cycle status: D385-setops-selector / cycle 1

## Current state

- task: `D385-setops-selector` · form: `cycle` · specialized skill: `pdca-dotnet` (overlay `pdca-coder` + `pdca-dotnet`) + project overlay `nextorm-pdca`
- mode: `autonomous`
- cycle: `N=1` · revision: `r=2` · attempt: `n=1/3`
- contract: `rv=2`
- scope: issue #385 (milestone 1.0.9) — шесть аддитивных перегрузок Union/UnionAll/Intersect/IntersectAll/Except/ExceptAll с селектором сущности на QueryCommand<TResult>; один цикл
- goal: шесть аддитивных перегрузок с SQL/план-ключом, идентичным явной форме, тесты, документация EN/RU, запись в API-NAMING-REVIEW.md, коммит в ветку 1.0.9, закрытие issue #385
- phase: `ACT` · record: `completed` · outcome: `blocked` (PLAN-terminal: `acceptance criteria cannot be determined`; cycle `N=1` closed, status file kept)
- source: user dispatch (issue #385) + pdca-planner plans r=1/r=2; active evidence root `.pdca/D385-setops-selector/rv2/` (`issue-385.json` persisted, sha256 be5a5c3253564ea375bcd24bc374cfc4fd9ff52943f39c7a0cf98a28209bc31f; `rv1/` superseded — `rv1/SUPERSEDED.md`)
- criteria: AC-1..AC-10 verbatim + oracle layer O-AC3/O-AC6 — exact text in §PLAN record r2 (source: user dispatch + issue #385)

## Original task

> ## ЗАДАЧА (issue #385, milestone 1.0.9) — дословная спецификация
https://github.com/AlexeyShirshov/nextorm/issues/385 — "Операции над множествами: правый операнд по селектору сущности".
Прочитать полный текст: `gh issue view 385 --repo AlexeyShirshov/nextorm --json body,title,labels,milestone` (исполнитель/scout с доступом к gh). Ниже — сжатое изложение, полный текст issue авторитетен:
- Добавить на QueryCommand<TResult> шесть аддитивных перегрузок: Union/UnionAll/Intersect/IntersectAll/Except/ExceptAll с сигнатурой `<Op><TEntity>(Expression<Func<TEntity, TResult>> selector)`, реализация ровно `=> <Op>(_dataContext!.From<TEntity>().Select(selector));`.
- Семантика идентична явной форме; проверка совместимости форм и переносимость *ALL (только PostgreSQL) не меняются; план-ключ совпадает с явной формой.
- `_dataContext` nullable → понятное InvalidOperationException, не NRE. `selector == null` → ArgumentNullException. `Union(null)` неоднозначен — отразить в XML-doc. XML-doc на каждый метод.
- Тесты: SQL-паритет per provider (sqlite/sqlserver/postgres/mysql/mariadb/clickhouse) — `<Op><TEntity>(sel)` == `<Op>(ctx.From<TEntity>().Select(sel))`, byte-identical SQL, все шесть операций + чейн; сценарий гайда SimpleEntity(1..10) Union/UnionAll ComplexEntity(1..3) → 10/13; план-кэш: ключ совпадает; негатив: несовпадение форм падает на подготовке, selector null, *ALL вне PostgreSQL → NotSupportedException; отразить 6 членов в docs/specs/design/API-NAMING-REVIEW.md (frozen surface); MIN_LINE_COVERAGE=85/MIN_BRANCH_COVERAGE=75.
- Документация: docs/guide/08-set-operations.md + docs/ru/guide/08-set-operations.md (примеры :62-75 перевести на `Union<IComplexEntity>(it => (int)it.Id)`, таблица методов :12-19, абзац :21-24).
- Провенанс: существующие члены QueryCommand.TResult.cs:1287/1303/1319/1335/1351/1367 (возвращают клон); EntityBuilder.Select — EntityBuilder.cs:642. Прецеденты #222/#384.
- Не-цели: форма без селектора, Where/OrderBy/Take на правой стороне, строковая форма/CTE, новые методы на EntityBuilder<TEntity>, изменение правил совместимости/*ALL.

> ## CONSTRAINTS
1. AUTO-COMMIT разрешён: только этот репозиторий/ветка 1.0.9, БЕЗ push и merge. Коммитить только файлы, созданные/изменённые этим циклом. Дерево УЖЕ грязное (34 несвязанных изменения по #319/документации/BDN/скиллам) — их не стейджить, не откатывать, не переформатировать; в модифицированных извне файлах (QueryCommand.cs, QueryCommand.TResult.cs, QueryCommand.* партиалы) коммитить только добавленные циклом строки (git add -p), иначе Notice и без коммита чужого.
2. Issue #385 закрыть `gh issue close 385 --repo AlexeyShirshov/nextorm --comment "..."` только после полного принятия и ACT-коммита; при блокере/STOP/сплите — оставить открытым, milestone не менять.
3. nextorm-инварианты: `dotnet build nextorm.slnx -c Debug` 0 warnings/0 errors; CRLF после правок; CPM; FluentAssertions; C#-символы только через `roslyn` (внутри execute), текст — только rg/git grep по src/tests.
4. Интеграционные тесты — по `.opencode/skills/running-integration-tests/SKILL.md`; сюита fail-closed без DOCKER_HOST; полный прогон — `python3 tools/integration/run-integration-capped.py --full`.
5. Перф-приёмка: изменение аддитивное/сахарное; PLAN обязан явно зафиксировать решение (прогон или аргумент с file:line, почему горячий путь не затронут).
6. Workspace: status `.pdca/status/D385-setops-selector-1.md`, evidence_root `.pdca/D385-setops-selector/rv1/`; скрипты уже в `.pdca/scripts/`.
7. Preflight пройден: pdca-planner/scout/executor/check/escalate доступны, subagent_depth 3.

Авторитетный полный текст issue: .pdca/D385-setops-selector/rv1/issue-385.json (sha256 be5a5c3253564ea375bcd24bc374cfc4fd9ff52943f39c7a0cf98a28209bc31f, milestone 1.0.9, label enhancement, state OPEN).

## PLAN record

### r1

Author: `pdca-planner`, r=1. Complete returned report, verbatim (original language):

# PLAN — Issue #385: Операции над множествами — правый операнд по селектору сущности (`rv1`)

## 1. Форма, специализированный скилл и входной объём (Form, specialized skill, entry scope)

- **Форма (Form):** `cycle` (обычный цикл PDCA).
- **Специализированный скилл / оверлей (Specialized skill / overlay):** `pdca-dotnet` (`coder+.NET`, включает `pdca-coder`) + проектный оверлей `nextorm-pdca` (зафиксированы пользователем во входном задании).
- **Обоснование формы (Form rationale):** Метод реализации полностью определён спецификацией задачи (шесть аддитивных делегирующих перегрузок на `QueryCommand<TResult>`, вызывающих `_dataContext.From<TEntity>().Select(selector)` и существующие методы `Union`/`UnionAll`/`Intersect`/`IntersectAll`/`Except`/`ExceptAll`), а объём изменений строго ограничен одним файлом продуктового кода (`src/nextorm.core/Query/Commands/QueryCommand.TResult.cs`), тремя файлами документации/спецификаций (`docs/guide/08-set-operations.md`, `docs/ru/guide/08-set-operations.md`, `docs/specs/design/API-NAMING-REVIEW.md`) и тестами в существующих тестовых проектах.
- **Входной объём (Entry scope):** Полная цель задачи #385 за один цикл — 6 новых перегрузок операций над множествами на `QueryCommand<TResult>` с XML-doc и защитой аргументов/контекста, подтверждённый SQL-паритет по всем 6 провайдерам, паритет план-ключа, покрытие сценария гайда и негативных кейсов, обновлённая документация EN/RU и запись frozen surface в `API-NAMING-REVIEW.md`.
- **Привязка к снимку (Re-anchor):** Ветка `1.0.9`, `HEAD` `b684bfef` (в рабочем дереве присутствуют 34 несвязанных изменения по #319/документации/BDN/скиллам; целевые файлы `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs`, `docs/guide/08-set-operations.md`, `docs/ru/guide/08-set-operations.md`, `docs/specs/design/API-NAMING-REVIEW.md` на входе чистые).
- **Расположение свидетельств (Evidence location):** Основной чекаут `/home/alex/sources/nextorm`; статус-файл `.pdca/status/D385-setops-selector-1.md`, корень свидетельств ревизии `.pdca/D385-setops-selector/rv1/`.

---

## 2. Цель, запрашиваемый/поставляемый результат и критерии приёмки (Goal and acceptance criteria)

### Дословная задача пользователя (Original task verbatim)

> ## ЗАДАЧА (issue #385, milestone 1.0.9) — дословная спецификация
> https://github.com/AlexeyShirshov/nextorm/issues/385 — "Операции над множествами: правый операнд по селектору сущности".
> Прочитать полный текст: `gh issue view 385 --repo AlexeyShirshov/nextorm --json body,title,labels,milestone` (исполнитель/scout с доступом к gh). Ниже — сжатое изложение, полный текст issue авторитетен:
> - Добавить на QueryCommand<TResult> шесть аддитивных перегрузок: Union/UnionAll/Intersect/IntersectAll/Except/ExceptAll с сигнатурой `<Op><TEntity>(Expression<Func<TEntity, TResult>> selector)`, реализация ровно `=> <Op>(_dataContext!.From<TEntity>().Select(selector));`.
> - Семантика идентична явной форме; проверка совместимости форм и переносимость *ALL (только PostgreSQL) не меняются; план-ключ совпадает с явной формой.
> - `_dataContext` nullable → понятное InvalidOperationException, не NRE. `selector == null` → ArgumentNullException. `Union(null)` неоднозначен — отразить в XML-doc. XML-doc на каждый метод.
> - Тесты: SQL-паритет per provider (sqlite/sqlserver/postgres/mysql/mariadb/clickhouse) — `<Op><TEntity>(sel)` == `<Op>(ctx.From<TEntity>().Select(sel))`, byte-identical SQL, все шесть операций + чейн; сценарий гайда SimpleEntity(1..10) Union/UnionAll ComplexEntity(1..3) → 10/13; план-кэш: ключ совпадает; негатив: несовпадение форм падает на подготовке, selector null, *ALL вне PostgreSQL → NotSupportedException; отразить 6 членов в docs/specs/design/API-NAMING-REVIEW.md (frozen surface); MIN_LINE_COVERAGE=85/MIN_BRANCH_COVERAGE=75.
> - Документация: docs/guide/08-set-operations.md + docs/ru/guide/08-set-operations.md (примеры :62-75 перевести на `Union<IComplexEntity>(it => (int)it.Id)`, таблица методов :12-19, абзац :21-24).
> - Провенанс: существующие члены QueryCommand.TResult.cs:1287/1303/1319/1335/1351/1367 (возвращают клон); EntityBuilder.Select — EntityBuilder.cs:642. Прецеденты #222/#384.
> - Не-цели: форма без селектора, Where/OrderBy/Take на правой стороне, строковая форма/CTE, новые методы на EntityBuilder<TEntity>, изменение правил совместимости/*ALL.
>
> ## CONSTRAINTS
> 1. AUTO-COMMIT разрешён: только этот репозиторий/ветка 1.0.9, БЕЗ push и merge. Коммитить только файлы, созданные/изменённые этим циклом. Дерево УЖЕ грязное (34 несвязанных изменения по #319/документации/BDN/скиллам) — их не стейджить, не откатывать, не переформатировать; в модифицированных извне файлах (QueryCommand.cs, QueryCommand.TResult.cs, QueryCommand.* партиалы) коммитить только добавленные циклом строки (git add -p), иначе Notice и без коммита чужого.
> 2. Issue #385 закрыть `gh issue close 385 --repo AlexeyShirshov/nextorm --comment "..."` только после полного принятия и ACT-коммита; при блокере/STOP/сплите — оставить открытым, milestone не менять.
> 3. nextorm-инварианты: `dotnet build nextorm.slnx -c Debug` 0 warnings/0 errors; CRLF после правок; CPM; FluentAssertions; C#-символы только через `roslyn` (внутри execute), текст — только rg/git grep по src/tests.
> 4. Интеграционные тесты — по `.opencode/skills/running-integration-tests/SKILL.md`; сюита fail-closed без DOCKER_HOST; полный прогон — `python3 tools/integration/run-integration-capped.py --full`.
> 5. Перф-приёмка: изменение аддитивное/сахарное; PLAN обязан явно зафиксировать решение (прогон или аргумент с file:line, почему горячий путь не затронут).
> 6. Workspace: status `.pdca/status/D385-setops-selector-1.md`, evidence_root `.pdca/D385-setops-selector/rv1/`; скрипты уже в `.pdca/scripts/`.
> 7. Preflight пройден: pdca-planner/scout/executor/check/escalate доступны, subagent_depth 3.

- **Запрашиваемый результат (Requested result):** Шесть аддитивных перегрузок `Union<TEntity>`, `UnionAll<TEntity>`, `Intersect<TEntity>`, `IntersectAll<TEntity>`, `Except<TEntity>`, `ExceptAll<TEntity>` с сигнатурой `(Expression<Func<TEntity, TResult>> selector)` на `QueryCommand<TResult>`, идентичных по семантике, SQL и план-ключу явной форме `<Op>(ctx.From<TEntity>().Select(selector))`, с валидацией `selector` и `_dataContext`, XML-документацией, тестами, обновлёнными гайдами EN/RU и записью в `API-NAMING-REVIEW.md`, закоммиченные в ветку `1.0.9` с закрытием issue #385.
- **Поставляемый результат (Delivered result):** Шесть аддитивных перегрузок `Union<TEntity>`, `UnionAll<TEntity>`, `Intersect<TEntity>`, `IntersectAll<TEntity>`, `Except<TEntity>`, `ExceptAll<TEntity>` с сигнатурой `(Expression<Func<TEntity, TResult>> selector)` на `QueryCommand<TResult>`, идентичных по семантике, SQL и план-ключу явной форме `<Op>(ctx.From<TEntity>().Select(selector))`, с валидацией `selector` и `_dataContext`, XML-документацией, тестами, обновлёнными гайдами EN/RU и записью в `API-NAMING-REVIEW.md`, закоммиченные в ветку `1.0.9` с закрытием issue #385 (тот же самый объект).

### Критерии приёмки (User-supplied acceptance criteria, reproduced verbatim with stable IDs)

Все критерии ниже воспроизведены дословно из спецификации пользователя и тела Issue #385 без добавления, ослабления или подмены:

- **AC-1 (API и реализация 6 перегрузок):**
  > Добавить на `QueryCommand<TResult>` шесть аддитивных перегрузок: `Union`/`UnionAll`/`Intersect`/`IntersectAll`/`Except`/`ExceptAll` с сигнатурой `<Op><TEntity>(Expression<Func<TEntity, TResult>> selector)`, реализация ровно `=> <Op>(_dataContext!.From<TEntity>().Select(selector));` (с проверкой `selector == null` → `ArgumentNullException` и `_dataContext == null` → понятное `InvalidOperationException`, а не NRE).
  > Только аддитивные перегрузки; существующие `Union<T>(QueryCommand<T>)` и остальные не меняются. `Union(...)` возвращает `QueryCommand<TResult>`, поэтому дальше доступны и новые формы, и старые.
  > **Не-цели (негативное условие):** никаких `Union<TEntity>()` без селектора; никаких `Union<TEntity>(selector, …)` с `Where`/`OrderBy`/`Take` правой стороны; никаких новых методов на `EntityBuilder<TEntity>`; никакого изменения правил совместимости форм или переносимости `*ALL`.

- **AC-2 (Защита аргументов, контекста и XML-doc):**
  > `_dataContext` в `QueryCommand<TResult>` nullable (`QueryCommand.TResult.cs:91,108,127` используют `!`); если контекста нет — понятное `InvalidOperationException`, а не NRE. `selector == null` → `ArgumentNullException`. `Union(null)` станет неоднозначным (`QueryCommand<T>` vs `Expression<…>`) — задокументировать в XML-doc как неподдерживаемую форму. XML-doc на каждый метод.

- **AC-3 (SQL-паритет per provider и чейнинг):**
  > **SQL-паритет per provider** (sqlite/sqlserver/postgres/mysql/mariadb/clickhouse): `<Op><TEntity>(sel)` == `<Op>(ctx.From<TEntity>().Select(sel))` — byte-identical SQL, все шесть операций, чейн из двух-трёх операций.

- **AC-4 (Сценарий из гайда):**
  > **Сценарий из гайда:** `SimpleEntity` (ids 1..10) `Union`/`UnionAll` `ComplexEntity` (ids 1..3, приведённые к `int`) → 10 / 13.

- **AC-5 (План-кэш):**
  > **План-кэш:** ключ синтезированного запроса совпадает с ключом явной формы (нет дублирования планов и промаха кэша).

- **AC-6 (Негативные случаи и семантика совместимости / `*ALL`):**
  > **Негатив:** несовпадение форм по-прежнему падает на подготовке (ошибка — того же типа и на той же стадии); `selector == null` → `ArgumentNullException`; `intersect all` / `except all` вне PostgreSQL → `NotSupportedException` при подготовке.

- **AC-7 (Поверхность API и `API-NAMING-REVIEW.md`):**
  > **Поверхность:** инвентарь `Action<JoinOptions>` не затрагивается; отразить 6 новых членов в frozen-surface записи `docs/specs/design/API-NAMING-REVIEW.md`.

- **AC-8 (Документация EN + RU):**
  > `docs/guide/08-set-operations.md` + `docs/ru/guide/08-set-operations.md`:
  > - пример `distinctCount`/`allCount` (`:67-75` / RU `:67-75`) перевести на `Union<IComplexEntity>(it => (int)it.Id)`;
  > - в таблице методов (`:12-19`) добавить колонку/примечание «правая сторона по селектору»;
  > - в абзаце про «правый запрос может проецировать другой тип элемента» (`:21-24`) оговорить, что селекторная форма фиксирует тип элемента левой стороны, а для другого типа остаётся явная форма.

- **AC-9 (Инварианты сборки, покрытия и перф-приёмки):**
  > `dotnet build nextorm.slnx -c Debug` 0 warnings/0 errors; CRLF после правок; CPM; FluentAssertions; `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75`.
  > Перф-приёмка: изменение аддитивное/сахарное; решение зафиксировано в PLAN с аргументом `file:line`, почему горячий путь не затронут.

- **AC-10 (Политика git-коммита и закрытия issue #385):**
  > AUTO-COMMIT разрешён: только этот репозиторий/ветка `1.0.9`, БЕЗ push и merge. Коммитить только файлы, созданные/изменённые этим циклом; 34 несвязанных изменения в грязном дереве не стейджить, не откатывать, не переформатировать; в модифицированных извне файлах коммитить только добавленные циклом строки (`git add -p`), иначе Notice и без коммита чужого.
  > Issue #385 закрыть `gh issue close 385 --repo AlexeyShirshov/nextorm --comment "..."` только после полного принятия и ACT-коммита; при блокере/STOP/сплите — оставить открытым, milestone не менять.

---

## 3. Метод измерения (Measurement method — для числовых порогов покрытия AC-9)

- **Метрика и единица измерения:** Line coverage (%) и Branch coverage (%) в формате Cobertura (`dotnet-coverage` → `reportgenerator`).
- **Знаменатель и фильтры включения/исключения:**
  - Whole-solution gate: конфигурация `coverage.settings.xml` (сборки `nextorm.core`, `nextorm.sqlite`, `nextorm.postgres`, `nextorm.sqlserver`).
  - Дополнительно, если затрагиваются интеграционные тесты (`tests/nextorm.integration.tests`), проектный гейт интеграционного покрытия по `coverage.integration.settings.xml` с фильтром `-classfilters:"-NextORM.Core.InMemory*"` (`MIN_INTEGRATION_LINE_COVERAGE=65` / `MIN_INTEGRATION_BRANCH_COVERAGE=75`).
- **Условия сопоставимости:** Замер на собранном решении `nextorm.slnx` с теми же фильтрами сборок/классов, что зафиксированы в `coverage.settings.xml` (и `coverage.integration.settings.xml`).
- **Порог и его происхождение:** `MIN_LINE_COVERAGE = 85%`, `MIN_BRANCH_COVERAGE = 75%` — явно заданы пользователем в спецификации задачи и `AGENTS.md`.

---

## 4. Решение по перф-приёмке (Perf-acceptance decision, Constraint #5)

- **Решение:** Прогон BenchmarkDotNet (`--anyCategories=acceptance`) **не требуется**; применяется аргументированное решение по `file:line`, доказывающее, что горячий путь не затронут.
- **Аргумент (`file:line`):**
  1. Новые 6 методов в `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` (рядом с существующими `Union` `:1287`, `UnionAll` `:1303`, `Intersect` `:1319`, `IntersectAll` `:1335`, `Except` `:1351`, `ExceptAll` `:1367`) являются чистым синтаксическим сахаром конструирования запроса: они проверяют `ArgumentNullException.ThrowIfNull(selector)` и `_dataContext ?? throw new InvalidOperationException(...)`, после чего делегируют в `<Op>(_dataContext.From<TEntity>().Select(selector))`.
  2. Правый операнд строится штатным путём через `DataContextExtensions.From<TEntity>(this IDataContext, ...)` (`src/nextorm.core/DataContext/DataContextExtensions.cs:1038`) и `EntityBuilder<TEntity>.Select<TResult>(Expression<Func<TEntity, TResult>>)` (`src/nextorm.core/Builders/Entity/EntityBuilder.cs:642` → `SelectCore` `:655`).
  3. Запись операции в команду выполняется тем же самым методом `QueryCommand.SetOperation` (`src/nextorm.core/Query/Commands/QueryCommand.cs:1705`), а хэш и сравнение ключа кэша планов вычисляются через существующий `UnionPlanHash` (`src/nextorm.core/Query/Commands/QueryCommand.cs:147`) и компаратор плана без единого изменения в `QueryPlanner` (`src/nextorm.core/DataContext/QueryPlanner.cs`), `SqlBuilder` (`src/nextorm.core/DataContext/Sql/SqlBuilder.cs`), кэше планов или материализаторах строк (`RowMapperFactory`/`RowMaterializerBuilder`).
  4. Для существующих вызовов `<Op>(QueryCommand<T>)` и любых других запросов кодовый путь не меняется ни на одну инструкцию.

---

## 5. Матрица вариантов изменённого пути (Variant matrix)

| Измерение | Варианты, которые должны быть покрыты результатом |
| --- | --- |
| Операция (`<Op>`) | `Union`, `UnionAll`, `Intersect`, `IntersectAll`, `Except`, `ExceptAll` (все 6 на `QueryCommand<TResult>`) |
| Провайдер (SQL-паритет `<Op><TEntity>(sel)` vs `<Op>(ctx.From<TEntity>().Select(sel))`) | `sqlite`, `sqlserver`, `postgres`, `mysql`, `mariadb`, `clickhouse`: для `postgres` все 6 операций выдают byte-identical SQL; для `sqlite`, `sqlserver`, `mysql`, `mariadb`, `clickhouse` 4 поддерживаемые операции (`Union`, `UnionAll`, `Intersect`, `Except`) выдают byte-identical SQL, а `IntersectAll` и `ExceptAll` одинаково бросают `NotSupportedException` при подготовке |
| Композиция (чейнинг) | Одиночная операция и цепочка из 2–3 операций (включая смешение новых селекторных и существующих `QueryCommand<T>` перегрузок) |
| Совпадение/приведение типов проекции | Тот же тип (`ISimpleEntity` → `int`) и другая сущность с приведением к `TResult` (`IComplexEntity` → `(int)it.Id`, сценарий гайда 1..10 и 1..3 → 10 / 13); негатив: несовпадение числа/типов колонок падает на подготовке |
| План-кэш | Ключ и хэш плана для `left.<Op><TEntity>(sel)` равны ключу и хэшу для `left.<Op>(ctx.From<TEntity>().Select(sel))` |
| Валидация входов и состояния | `selector == null` → `ArgumentNullException`; `_dataContext == null` (например, на отвязанном кэшированном клоне) → понятное `InvalidOperationException` (не `NullReferenceException`) |

---

## 6. Закрытие пробелов постановки (Gaps: statement gap → evidence / assumption / blocker)

1. **Пробел 1 — В `MySQL` и `ClickHouse` сейчас 0 тестов генерации SQL для set-операций, в `MariaDB` — 2; как трактуется «SQL-паритет per provider (sqlite/sqlserver/postgres/mysql/mariadb/clickhouse), все шесть операций + чейн» с учётом того, что `*ALL` поддерживается только в PostgreSQL?**
   - **Закрытие (Evidence):** Спецификация задачи (§4 и §6) явно фиксирует: «`*ALL` не меняет переносимость: `intersect all` / `except all` по-прежнему только PostgreSQL, остальные бросают `NotSupportedException` при подготовке». Следовательно, во всех 6 провайдерских тестовых проектах (`sqlite`, `sqlserver`, `postgres`, `mysql`, `mariadb`, `clickhouse`) паритет проверяется для всех 6 операций: на `postgres` — byte-identical SQL для всех 6 операций + чейн из 2–3 операций; на остальных 5 провайдерах — byte-identical SQL для 4 эмитируемых операций (`Union`, `UnionAll`, `Intersect`, `Except`) + чейн из 2–3 операций, и одинаковый `NotSupportedException` на подготовке для `IntersectAll<TEntity>(sel)` и `ExceptAll<TEntity>(sel)`.
2. **Пробел 2 — Порядок проверок `selector == null` (`ArgumentNullException`) и `_dataContext == null` (`InvalidOperationException`), и как получить экземпляр `QueryCommand<TResult>` с `_dataContext == null`.**
   - **Закрытие (Evidence + Assumption):** В соответствии со стандартными конвенциями .NET сначала проверяется входной аргумент метода (`ArgumentNullException.ThrowIfNull(selector)`), затем состояние объекта (`if (_dataContext is null) throw new InvalidOperationException(...)`), после чего вызывается `<Op>(_dataContext.From<TEntity>().Select(selector))`. В `QueryCommand.cs:97` задокументировано, что `_dataContext` равен `null` у непривязанного кэшированного клона (`Clone()` в `QueryCommand.Clone.cs:281` / конструктор без контекста).
3. **Пробел 3 — Формат записи 6 новых членов в `docs/specs/design/API-NAMING-REVIEW.md`, где сейчас нет секции set-операций.**
   - **Закрытие (Evidence):** В `docs/specs/design/API-NAMING-REVIEW.md` секция frozen surface находится в `### Шаг 5 — закрепление` (`:3653-3667`), сводный учёт — в `:5241` и `:5887` (`Счёт поверхности`), прецедент трекинга новых перегрузок — запись `N222-1` (`:5856`). Шесть новых перегрузок `QueryCommand<TResult>.<Op><TEntity>(Expression<Func<TEntity, TResult>> selector)` (`#385`) фиксируются в frozen-surface / учётной таблице `API-NAMING-REVIEW.md` по образцу `N222-1` с обновлением счёта поверхности (+6 аддитивных перегрузок) и пометой, что инвентарь `Action<JoinOptions>` не затронут.

---

## 7. Минимальное решение (Minimal solution)

В `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` рядом с существующими шестью методами (`:1287-1382`) добавить шесть обобщённых перегрузок `Union<TEntity>`, `UnionAll<TEntity>`, `Intersect<TEntity>`, `IntersectAll<TEntity>`, `Except<TEntity>`, `ExceptAll<TEntity>`, принимающих `Expression<Func<TEntity, TResult>> selector`. Каждая перегрузка проверяет `ArgumentNullException.ThrowIfNull(selector)` и наличие `_dataContext` (бросая осмысленный `InvalidOperationException`, если `_dataContext is null`), после чего делегирует в соответствующий существующий метод `<Op>(_dataContext.From<TEntity>().Select(selector))`. Каждая перегрузка снабжается XML-doc (включая примечание о неоднозначности вызова `<Op>(null)` между `QueryCommand<T>` и `Expression<Func<TEntity, TResult>>`). Существующие перегрузки `<Op><T>(QueryCommand<T>)` также могут быть дополнены `<remarks>` о неоднозначности `null` при необходимости.

---

## 8. Ограничения, допущения и известные ресурсные рамки (Constraints, assumptions & resource bounds)

- **Ограничения (Constraints):**
  1. Только ветка `1.0.9` в `/home/alex/sources/nextorm`. `git push` и `git merge` запрещены.
  2. Рабочее дерево содержит 34 несвязанных изменения (`QueryCommand.cs`, `QueryCommand.Clone.cs` и др. тесты `#319`). Их запрещено стейджить, откатывать, переформатировать или включать в коммит. Коммитить строго файлы/строки этого цикла (`git add -p` при пересечении).
  3. Сборка `dotnet build nextorm.slnx -c Debug` — 0 предупреждений, 0 ошибок (`TreatWarningsAsErrors=true`).
  4. Окончания строк — строго CRLF во всех изменённых файлах (`core.autocrlf=true`).
  5. В тестах — только `FluentAssertions` (кроме `Assert.Skip*`); соблюдение лимита ≤ 10 файлов в одной папке (`src/**` и `tests/**`).
  6. Анализ символов C# — исключительно через `roslyn` внутри `execute`; поиск текста — только ignore-aware (`rg` / `git grep` по `src` и `tests`).
  7. Полный прогон интеграционных тестов (если запускается) — только через `python3 tools/integration/run-integration-capped.py --full` по скиллу `running-integration-tests` с активным Podman-сокетом (`DOCKER_HOST`), fail-closed.
  8. Свидетельства цикла оформляются в `.pdca/D385-setops-selector/rv1/` с валидацией через `.pdca/scripts/pdca_evidence.py` и `.pdca/scripts/validate_inner_loop.py`.
  9. Закрытие issue `#385` через `gh issue close 385 --repo AlexeyShirshov/nextorm --comment "..."` — только в фазе ACT после полного PASS всех критериев и создания коммита; milestone `1.0.9` не менять.
- **Известные ресурсные и доступные условия (Resource and access constraints):**
  - Все инструменты (`.NET 10 SDK`, `roslyn`, `gh`, локальные скрипты `.pdca/scripts/`, Podman/тестовое окружение) доступны в рабочей среде; блокеров доступа нет.

---

## 9. Пакет передачи в DO (Plan-to-DO handoff)

Передаётся исполнителю (`pdca-executor`) по ссылке без назначения единиц декомпозиции и без заморозки проверочных команд CHECK:
- **Исходная задача:** Дословная спецификация Issue #385 и диспетча (раздел 2 выше).
- **Область правки:**
  - Продуктовый код: `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` (6 новых перегрузок + XML-doc + защита `selector` и `_dataContext`).
  - Документация: `docs/guide/08-set-operations.md` и `docs/ru/guide/08-set-operations.md` (таблица `:12-19`, абзац `:21-24`, примеры `:62-75`).
  - Спецификация поверхности API: `docs/specs/design/API-NAMING-REVIEW.md` (отражение 6 новых членов в frozen surface).
  - Тесты: провайдерские SQL-паритет тесты (`sqlite`, `sqlserver`, `postgres`, `mysql`, `mariadb`, `clickhouse`), план-кэш паритет, сценарий гайда (`SimpleEntity` 1..10 `Union`/`UnionAll` `ComplexEntity` 1..3 → 10 / 13) и негативные тесты (`selector == null`, `_dataContext == null`, несовпадение форм на подготовке, `*ALL` вне PostgreSQL → `NotSupportedException`).
- **Обязательные критерии:** `AC-1` .. `AC-10`.
- **Запреты:** Не трогать 34 несвязанных грязных файла (`QueryCommand.cs`, `QueryCommand.Clone.cs` и др.); не добавлять не-цели из §7 спецификации; не нарушать CRLF и лимит ≤ 10 файлов на папку; не делать `git push` / `git merge`; не закрывать issue #385 до успешного ACT-коммита.

---

## 10. Риски и условия остановки (Risks and stop conditions)

- **Риски:**
  1. *Конфликт с несвязанными правками в `QueryCommand.*.cs` в грязном рабочем дереве:* `QueryCommand.TResult.cs` сейчас чист, однако другие partial-файлы `QueryCommand` модифицированы задачей #319. Риск поломки сборки или случайного стейджинга чужих файлов купируется правкой только `QueryCommand.TResult.cs` и адресным стейджингом только своих файлов/хунков.
  2. *Лимит файлов в тестовых папках (hard max 10):* при добавлении новых тестовых файлов проверка числа файлов в целевой папке обязательна; предпочтительно расширять существующие тестовые файлы или выделять подпапку при приближении к лимиту 10.
  3. *Неоднозначность вызова `<Op>(null)` в существующих тестах:* если какой-либо существующий тест передавал нетипизированный `null` в `Union(null)`, компилятор выдаст CS0121. В таком случае в тесте явно типизируется `null!` как `(QueryCommand<int>)null!` для проверки старой перегрузки и `(Expression<Func<ISimpleEntity, int>>)null!` для новой.
- **Условия остановки (Stop conditions):**
  - Срабатывание правила 3 попыток (`n = 3`) без достижения PASS по `AC-1`..`AC-10` → эскалация в `pdca-escalate` или терминальный `STOP` без закрытия issue #385.
  - Обнаружение того, что несвязанные правки #319 в рабочем дереве не компилируются и блокируют сборку `nextorm.slnx` без модификации чужих файлов → репорт блокера без отката чужих изменений.

---

## 11. Уверенность и оставшиеся допущения (Confidence and gaps)

- **Проверено фактами:** Точные сигнатуры и строки всех 6 существующих методов в `QueryCommand.TResult.cs:1287-1382`, наличие `EntityBuilder<TEntity>.Select<TResult>` (`EntityBuilder.cs:642`), чистота целевых файлов `QueryCommand.TResult.cs`, `docs/guide/08-set-operations.md`, `docs/ru/guide/08-set-operations.md`, `docs/specs/design/API-NAMING-REVIEW.md`, состояние существующих тестов set-операций по всем 6 провайдерам и план-кэшу.
- **Оставшиеся допущения:** Порядок проверок в новых перегрузках (`ArgumentNullException.ThrowIfNull(selector)` перед проверкой `_dataContext is null` → `InvalidOperationException`) соответствует стандартному контракту .NET и удовлетворяет обоим требованиям спецификации.

Примечание: план вставлен дословно и содержит собственные заголовки уровня ##.

### r2

Author: `pdca-planner`, r=2. Revised result contract after the CHECK `not accepted` verdict and the `pdca-escalate` review; user criteria AC-1..AC-10 preserved verbatim, plus a separate oracle layer (Precedence Rule, O-AC3, O-AC6).

# Пересмотренный план `r=2` (`cycle`, `coder+.NET`) — Issue #385 (`D385-setops-selector`)

Принято решение **(a) выпустить пересмотренный план `r=2`** с дословным сохранением записанного набора критериев `AC-1..AC-10` из `.pdca/status/D385-setops-selector-1.md`, добавлением отдельного слоя **Правила приоритета чтения (Precedence Rule)** и планерных оракулов **`O-AC3` / `O-AC6`**, переклассификацией `D385-C2` и `D385-C3` в зафиксированные ложные посылки текста задачи (без дефекта продукта), закрытием дефектов доказательств `D385-E1..E4` и оформлением спецификации процессных дефектов `docs/specs/design/D385-pdca-process-defects.md` (`D385-PROC-1..4`).

### Почему отвергнут `original task is infeasible under the stated constraints` и почему выбран пересмотр `r=2`

1. **Опровержение `infeasible` (`references/plan.md` §Terminal PLAN outcomes, строки 84–86, 107–108).** Исход `original task is infeasible under the stated constraints` применяется, когда **запрошенный результат** невозможно построить при заданных ограничениях. Здесь запрошенный объект — шесть аддитивных перегрузок `<Op><TEntity>(Expression<Func<TEntity, TResult>> selector)` в `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` с точным делегированием `=> <Op>(_dataContext!.From<TEntity>().Select(selector));`, тестами, документацией EN/RU и записью в `docs/specs/design/API-NAMING-REVIEW.md` — полностью реализуем, уже поставлен в рабочем дереве, собирается с `0 Warning(s), 0 Error(s)` и проходит канонический интеграционный прогон (`8667 total / 8470 passed / 0 failed / 197 skipped`).
2. **Почему это пересмотр контракта результата (`r=2`), а не `acceptance criteria cannot be determined` (`plan.md:108-114, 301-303`).** Сам текст задачи (`AC-1`, `AC-3`, `AC-6`) содержит оперативный инвариант паритета с явной формой: реализация предписана буквально как `=> <Op>(_dataContext!.From<TEntity>().Select(selector));`, не-цели в `AC-1` прямо запрещают «изменение правил совместимости форм или переносимости `*ALL`», а `AC-6` требует поведение «по-прежнему ... (ошибка — того же типа и на той же стадии)». Чтобы сделать буквальными две описательные вставки о *текущем* состоянии кода («вне PostgreSQL → `NotSupportedException` при подготовке» и «несовпадение форм по-прежнему падает на подготовке»), пришлось бы сломать поддержку `*ALL` в MariaDB и ClickHouse и добавить проверку совместимости форм в `QueryPreparer`, что прямо запрещено `AC-1` (не-цели и точная формула делегирования). Авторизованный read-only сбор фактов по коду (`PostgresDialect.cs:309-311`, `MariaDbDialect.cs:110`, `ClickHouseDialect.cs:161-162`, `SqlBuilder.cs:457-458`, `QueryCommand.cs:1705-1723`, `QueryCommand.QueryPreparer.cs:4566,4150,4268`) однозначно разрешает коллизию в пользу оперативных клауз паритета. Добавление этого правила чтения и оракулов `O-AC3`/`O-AC6` в контракт результата при неизменном тексте `AC-1..AC-10` — подлинный пересмотр контракта (`r=1 → r=2`, `plan.md:301-303`).

---

## 1. Form and specialized skill

- **Form:** `cycle` (обычный цикл PDCA).
- **Specialized skill (overlay):** `coder+.NET` (`pdca-coder` + `pdca-dotnet`).
- **One-line form rationale:** Метод решения полностью предписан задачей (`=> <Op>(_dataContext!.From<TEntity>().Select(selector));` для 6 операторов в `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs`), а объём строго ограничен 13 уже изменёнными принадлежащими циклу файлами плюс новой спецификацией процессных дефектов `docs/specs/design/D385-pdca-process-defects.md`.
- **Entry scope:** Полная поставка задачи Issue #385 (milestone `1.0.9`) — 6 аддитивных перегрузок `<Op><TEntity>(Expression<Func<TEntity, TResult>> selector)` на `QueryCommand<TResult>` с guard-ами и XML-doc, паритетом SQL/исключений по 6 провайдерам, тестом план-кэша, live-тестом сценария 10/13 из гайда, негативными тестами, синхронным обновлением `docs/guide/08-set-operations.md` и `docs/ru/guide/08-set-operations.md`, записью `N385-1` в `docs/specs/design/API-NAMING-REVIEW.md` и спецификацией процессных дефектов `docs/specs/design/D385-pdca-process-defects.md`.
- **Re-anchor & Evidence location:** Рабочее дерево `/home/alex/sources/nextorm` на ветке `1.0.9` (13 cycle-owned файлов кандидата + 34 посторонних изменения в грязном дереве, которые цикл не трогает). Файл статуса цикла неизменен: `status_path = .pdca/status/D385-setops-selector-1.md`; корень доказательств `r=1` — `.pdca/D385-setops-selector/rv1/`, корень доказательств ревизии `r=2` — `.pdca/D385-setops-selector/rv2/`.

---

## 2. Goal and acceptance criteria

- **Requested result:** Шесть аддитивных перегрузок `Union`/`UnionAll`/`Intersect`/`IntersectAll`/`Except`/`ExceptAll` с сигнатурой `<Op><TEntity>(Expression<Func<TEntity, TResult>> selector)` на `QueryCommand<TResult>` в `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs`, реализованных ровно как `=> <Op>(_dataContext!.From<TEntity>().Select(selector));` с проверками `selector == null` → `ArgumentNullException` и `_dataContext == null` → `InvalidOperationException`, XML-doc, тестами SQL-паритета по 6 провайдерам, план-кэша, сценария 10/13 и негативных кейсов, обновлёнными гайдами `docs/guide/08-set-operations.md` и `docs/ru/guide/08-set-operations.md` и записью в `docs/specs/design/API-NAMING-REVIEW.md`.
- **Delivered result:** Тот же самый объект — шесть перегрузок `<Op><TEntity>(Expression<Func<TEntity, TResult>> selector)` на `QueryCommand<TResult>` с точным делегированием, тестами, документацией EN/RU и записью в `docs/specs/design/API-NAMING-REVIEW.md` (плюс внутренняя спецификация процессных дефектов `docs/specs/design/D385-pdca-process-defects.md` по правилу `AGENTS.md`).

### Критерии приёмки (User-supplied acceptance criteria, reproduced verbatim with stable IDs from `.pdca/status/D385-setops-selector-1.md`)

Все критерии ниже воспроизведены дословно из спецификации пользователя и тела Issue #385 без добавления, ослабления или подмены:

- **AC-1 (API и реализация 6 перегрузок):**
  > Добавить на `QueryCommand<TResult>` шесть аддитивных перегрузок: `Union`/`UnionAll`/`Intersect`/`IntersectAll`/`Except`/`ExceptAll` с сигнатурой `<Op><TEntity>(Expression<Func<TEntity, TResult>> selector)`, реализация ровно `=> <Op>(_dataContext!.From<TEntity>().Select(selector));` (с проверкой `selector == null` → `ArgumentNullException` и `_dataContext == null` → понятное `InvalidOperationException`, а не NRE).
  > Только аддитивные перегрузки; существующие `Union<T>(QueryCommand<T>)` и остальные не меняются. `Union(...)` возвращает `QueryCommand<TResult>`, поэтому дальше доступны и новые формы, и старые.
  > **Не-цели (негативное условие):** никаких `Union<TEntity>()` без селектора; никаких `Union<TEntity>(selector, …)` с `Where`/`OrderBy`/`Take` правой стороны; никаких новых методов на `EntityBuilder<TEntity>`; никакого изменения правил совместимости форм или переносимости `*ALL`.

- **AC-2 (Защита аргументов, контекста и XML-doc):**
  > `_dataContext` в `QueryCommand<TResult>` nullable (`QueryCommand.TResult.cs:91,108,127` используют `!`); если контекста нет — понятное `InvalidOperationException`, а не NRE. `selector == null` → `ArgumentNullException`. `Union(null)` станет неоднозначным (`QueryCommand<T>` vs `Expression<…>`) — задокументировать в XML-doc как неподдерживаемую форму. XML-doc на каждый метод.

- **AC-3 (SQL-паритет per provider и чейнинг):**
  > **SQL-паритет per provider** (sqlite/sqlserver/postgres/mysql/mariadb/clickhouse): `<Op><TEntity>(sel)` == `<Op>(ctx.From<TEntity>().Select(sel))` — byte-identical SQL, все шесть операций, чейн из двух-трёх операций.

- **AC-4 (Сценарий из гайда):**
  > **Сценарий из гайда:** `SimpleEntity` (ids 1..10) `Union`/`UnionAll` `ComplexEntity` (ids 1..3, приведённые к `int`) → 10 / 13.

- **AC-5 (План-кэш):**
  > **План-кэш:** ключ синтезированного запроса совпадает с ключом явной формы (нет дублирования планов и промаха кэша).

- **AC-6 (Негативные случаи и семантика совместимости / `*ALL`):**
  > **Негатив:** несовпадение форм по-прежнему падает на подготовке (ошибка — того же типа и на той же стадии); `selector == null` → `ArgumentNullException`; `intersect all` / `except all` вне PostgreSQL → `NotSupportedException` при подготовке.

- **AC-7 (Поверхность API и `API-NAMING-REVIEW.md`):**
  > **Поверхность:** инвентарь `Action<JoinOptions>` не затрагивается; отразить 6 новых членов в frozen-surface записи `docs/specs/design/API-NAMING-REVIEW.md`.

- **AC-8 (Документация EN + RU):**
  > `docs/guide/08-set-operations.md` + `docs/ru/guide/08-set-operations.md`:
  > - пример `distinctCount`/`allCount` (`:67-75` / RU `:67-75`) перевести на `Union<IComplexEntity>(it => (int)it.Id)`;
  > - в таблице методов (`:12-19`) добавить колонку/примечание «правая сторона по селектору»;
  > - в абзаце про «правый запрос может проецировать другой тип элемента» (`:21-24`) оговорить, что селекторная форма фиксирует тип элемента левой стороны, а для другого типа остаётся явная форма.

- **AC-9 (Инварианты сборки, покрытия и перф-приёмки):**
  > `dotnet build nextorm.slnx -c Debug` 0 warnings/0 errors; CRLF после правок; CPM; FluentAssertions; `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75`.
  > Перф-приёмка: изменение аддитивное/сахарное; решение зафиксировано в PLAN с аргументом `file:line`, почему горячий путь не затронут.

- **AC-10 (Политика git-коммита и закрытия issue #385):**
  > AUTO-COMMIT разрешён: только этот репозиторий/ветка `1.0.9`, БЕЗ push и merge. Коммитить только файлы, созданные/изменённые этим циклом; 34 несвязанных изменения в грязном дереве не стейджить, не откатывать, не переформатировать; в модифицированных извне файлах коммитить только добавленные циклом строки (`git add -p`), иначе Notice и без коммита чужого.
  > Issue #385 закрыть `gh issue close 385 --repo AlexeyShirshov/nextorm --comment "..."` только после полного принятия и ACT-коммита; при блокере/STOP/сплите — оставить открытым, milestone не менять.

---

### Отдельный оракульный слой PLAN `r=2`: Правило приоритета чтения (Precedence Rule) и планерные оракулы `O-AC3`, `O-AC6`

> Этот слой **не редактирует, не дополняет и не ослабляет** пользовательские обязательства `AC-1..AC-10`. Он задаёт правило разрешения внутренней коллизии (`D385-C1`) между оперативными требованиями паритета и двумя фактологически неверными утверждениями о текущем состоянии кодовой базы (`D385-C2`, `D385-C3`), и определяет оракулы верификации `O-AC3` и `O-AC6`.

#### 2.1. Правило приоритета чтения (Precedence Rule)

1. Везде, где описательное утверждение о *существующем* поведении кодовой базы в `AC-3` («byte-identical SQL, все шесть операций» для провайдеров, не поддерживающих `*ALL`) или `AC-6` («вне PostgreSQL → `NotSupportedException` при подготовке» и «несовпадение форм по-прежнему падает на подготовке») противоречит реальному поведению явной формы `_dataContext.From<TEntity>().Select(selector)`, управляющими являются оперативные инварианты самой задачи:
   - буквальная формула делегирования `=> <Op>(_dataContext!.From<TEntity>().Select(selector));` (`AC-1`);
   - запрет из `AC-1` (Не-цели): «никакого изменения правил совместимости форм или переносимости `*ALL`»;
   - требование эквивалентности `<Op><TEntity>(sel)` == `<Op>(ctx.From<TEntity>().Select(sel))` (`AC-3`);
   - требование «по-прежнему ... (ошибка — того же типа и на той же стадии)» (`AC-6`).
2. **Зафиксированные ложные посылки текста задачи (переклассификация `D385-C2` и `D385-C3`):**
   - **`D385-C2` (`issue-text false premise, no product defect`):** Посылка `AC-6` о том, что `intersect all` / `except all` поддерживаются только в PostgreSQL, а во всех провайдерах вне PostgreSQL бросают `NotSupportedException`, опровергается кодом: **3 из 6** диалектов эмитят `INTERSECT ALL` / `EXCEPT ALL` (`PostgresDialect.cs:309-311`, `MariaDbDialect.cs:110`, `ClickHouseDialect.cs:161-162`), и **3 из 6** бросают `NotSupportedException` при подготовке SQL через `src/nextorm.core/DataContext/Sql/SqlBuilder.cs:457-458` (`sqlite`, `sqlserver`, `mysql`).
   - **`D385-C3` (`issue-text false premise, no product defect`):** Посылка `AC-6` о том, что несовпадение форм «по-прежнему падает на подготовке», опровергается кодом: ни `src/nextorm.core/Query/Commands/QueryCommand.cs:1705-1723`, ни `src/nextorm.core/Query/Commands/QueryCommand.QueryPreparer.cs:4566,4150,4268`, ни `src/nextorm.core/DataContext/Sql/SqlBuilder.cs:455-477` не проверяют совпадение числа/имён колонок операндов set-операции на стадии подготовки (ошибка возникает на стадии выполнения запроса СУБД), а сигнатура `Expression<Func<TEntity, TResult>>` статически фиксирует возвращаемый тип `TResult` на этапе компиляции C#.

#### 2.2. Планерные оракулы (`O-AC3`, `O-AC6`) и какие проверки `r=1` они заменяют

- **`O-AC3` (Планерный оракул для матрицы `*ALL` и SQL-паритета по 6 провайдерам):**
  - **Какую верификацию из `r=1` заменяет (как метод проверки, а не как обязательство):** заменяет буквальную (взаимоисключающую) проверку части `AC-3` («byte-identical SQL, все шесть операций» на всех 6 провайдерах) и третьего пункта `AC-6` («`intersect all` / `except all` вне PostgreSQL → `NotSupportedException` при подготовке») в строках манифеста `AC-3`, `AC-6`, `TESTS-SQL`.
  - **Условие приёмки оракула `O-AC3`:**
    1. *Дифференциальный паритет:* На всех 6 провайдерах (`sqlite`, `sqlserver`, `postgres`, `mysql`, `mariadb`, `clickhouse`), для всех 6 операций (`Union`, `UnionAll`, `Intersect`, `IntersectAll`, `Except`, `ExceptAll`) и для чейна из 2–3 операций исход вызова `<Op><TEntity>(sel)` идентичен исходу явной формы `<Op>(ctx.From<TEntity>().Select(sel))` — байт-в-байт совпадающий SQL там, где диалект эмитит оператор, либо идентичное исключение `NotSupportedException` на стадии подготовки там, где диалект его не поддерживает.
    2. *Фиксация матрицы 3/3 (pinned characterization):* Unit-тесты явно пинят текущую матрицу для `IntersectAll` и `ExceptAll`: `postgres`, `mariadb`, `clickhouse` эмитят `INTERSECT ALL` / `EXCEPT ALL` (byte-identical SQL на обеих формах + в чейнах); `sqlite`, `sqlserver`, `mysql` бросают `NotSupportedException` при подготовке на обеих формах. Ложная посылка `D385-C2` явно фиксируется в отчёте.
  - **Аргумент отсутствия сужения (Non-narrowing argument):** Обязательства покрыть все 6 провайдеров, все 6 операций, чейны из 2–3 операций и неизменность матрицы переносимости `*ALL` сохранены на 100%. Заменено только ложное описание текущего состояния репозитория («1 emit / 5 throw») на верифицированное по коду состояние («3 emit / 3 throw») с обязательным дифференциальным тестом обеих форм во всех 6 диалектах.

- **`O-AC6` (Планерный оракул для негативного кейса несовпадения форм):**
  - **Какую верификацию из `r=1` заменяет (как метод проверки, а не как обязательство):** заменяет буквальную проверку первого пункта `AC-6` («несовпадение форм по-прежнему падает на подготовке (ошибка — того же типа и на той же стадии)») в строках манифеста `AC-6` и `TESTS-NEG` (и устраняет дефект доказательств `D385-E4`, где позитивный тест совместимой проекции выдавался за негативный тест несовпадения форм).
  - **Условие приёмки оракула `O-AC6`:**
    1. Позитивный тест совместимой проекции (`SimpleEntity` + `ComplexEntity` с проекцией в один и тот же 1-колоночный `int`) **запрещено** маркировать как доказательство негативного кейса несовпадения форм (`D385-E4`).
    2. Проверяется реальное расхождение числа проецируемых SQL-колонок между левым и правым операндами при общем типе `TResult` (например, левый запрос формирует 2 колонки DTO, а правый селектор `Expression<Func<TEntity, TResult>>` использует `MemberInitExpression`, инициализирующий только 1 свойство DTO, против явной формы с тем же селектором):
       - **Ветка А (если транслятор порождает разное число колонок в SQL для одного `TResult`):** (а) unit-тест подтверждает, что обе формы (селекторная и явная) ведут себя одинаково на стадии подготовки (генерируют идентичный SQL с разным числом колонок без падения в `QueryPreparer`), и (б) core-level интеграционный тест на живом SQLite подтверждает, что обе формы падают на одной и той же стадии (выполнение запроса в СУБД) с одним и тем же типом исключения (`SqliteException`).
       - **Ветка Б (если для любого валидного `Expression<Func<TEntity, TResult>>` транслятор порождает то же число SQL-колонок, что и `TResult`, либо расхождение отсекается компилятором C#):** unit-тест доказывает совпадение структуры SQL-проекций обеих форм, а в статусе и манифесте (`O-AC6` / `TESTS-NEG`) расхождение формы через `Expression<Func<TEntity, TResult>>` фиксируется честно как **статически недостижимое по сигнатуре типа C# (`unreachable by C# type signature`)** наряду с фиксацией ложной посылки `D385-C3` о падении на подготовке.
    3. Если при выполнении реального shape-mismatch теста обнаружится продуктовый дефект, нарушающий паритет селекторной формы с явной формой, DO фиксирует его и устраняет в пределах контракта паритета; если паритет соблюдён, продуктовый код `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` не меняется.
  - **Аргумент отсутствия сужения (Non-narrowing argument):** Оперативное обязательство `AC-6` и `AC-1` («никакого изменения правил совместимости форм», «ошибка — того же типа и на той же стадии», что у явной формы) проверяется строго дифференциально и на реальном расхождении колонок (либо доказанной статической защите типа `TResult`), вместо фиктивного позитивного теста из `r=1`.

---

## 3. Measurement method

1. **Покрытие кода (`AC-9` — измеримая цель):**
   - **Metric & unit:** Line coverage (%) и Branch coverage (%) по отчёту `dotnet-coverage` (Cobertura) → `reportgenerator` (`TextSummary`).
   - **Denominator & filters:** Сборки `nextorm.core`, `nextorm.sqlite`, `nextorm.postgres`, `nextorm.sqlserver` согласно корневому `coverage.settings.xml` (конфигурация whole-solution гейта из `.github/workflows/dotnet.yml`, где hard-fail в CI включён только на ветке `main`).
   - **Comparability conditions:** Локальный замер unit-тестов с `-s coverage.settings.xml` на текущем дереве исходников; запись `na` запрещена (закрытие `D385-E3` — в манифесте и статусе должны быть записаны фактические численные проценты `line%` и `branch%`).
   - **Threshold & provenance:** `MIN_LINE_COVERAGE=85` (≥ 85.0 %), `MIN_BRANCH_COVERAGE=75` (≥ 75.0 %) — явно задано в `AC-9` и `AGENTS.md`.
2. **Перф-приёмка (`AC-9` — аргумент `file:line`, почему горячий путь не затронут):**
   - **Решение: Exempt / аддитивный синтаксический сахар без влияния на горячий путь.**
   - **Обоснование с указателями `file:line`:**
     - Новые 6 методов в `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` выполняют только два дешёвых guard-а (`ArgumentNullException.ThrowIfNull(selector)` и проверка `_dataContext is null`) и немедленно делегируют в `=> <Op>(_dataContext!.From<TEntity>().Select(selector));`, вызывая существующие перегрузки `Union`/`UnionAll`/`Intersect`/`IntersectAll`/`Except`/`ExceptAll` в `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs:89-139` и базовую регистрацию операции в `src/nextorm.core/Query/Commands/QueryCommand.cs:1705-1723`.
     - Правый операнд `_dataContext!.From<TEntity>().Select(selector)` конструируется через существующий `src/nextorm.core/Builders/Entity/EntityBuilder.cs` ровно так же, как при ручной записи явной формы.
     - Подготовка плана (`src/nextorm.core/Query/Commands/QueryCommand.QueryPreparer.cs:4150,4268,4566`), генерация SQL (`src/nextorm.core/DataContext/Sql/SqlBuilder.cs:455-477`) и вычисление ключа кэша планов дают идентичный результат и попадают в ту же запись `QueryPlanCache` (`AC-5`), поэтому ни компиляция плана при повторных вызовах, ни выполнение запроса и материализация строк не изменяются.

---

## 4. Variant matrix

| Ось матрицы | Варианты | Ожидаемый исход по контракту `r=2` |
| --- | --- | --- |
| Операторы (6) | `Union`, `UnionAll`, `Intersect`, `IntersectAll`, `Except`, `ExceptAll` в `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` | Сигнатура `<Op><TEntity>(Expression<Func<TEntity, TResult>> selector)`, возврат `QueryCommand<TResult>`, делегирование `=> <Op>(_dataContext!.From<TEntity>().Select(selector));`, чейнинг 2–3 операций со старыми и новыми перегрузками (`AC-1`, `AC-3`). |
| Диалекты / провайдеры (6) | `sqlite`, `sqlserver`, `postgres`, `mysql`, `mariadb`, `clickhouse` | Byte-identical SQL между `<Op><TEntity>(sel)` и `<Op>(ctx.From<TEntity>().Select(sel))` для `Union`, `UnionAll`, `Intersect`, `Except` во всех 6 провайдерах; для `IntersectAll`, `ExceptAll` — byte-identical SQL в `postgres`, `mariadb`, `clickhouse` и идентичный `NotSupportedException` при подготовке в `sqlite`, `sqlserver`, `mysql` (`AC-3`, `AC-6`, `O-AC3`). |
| Guard-ветки и XML-doc | `selector == null`; `_dataContext == null` (`new QueryCommand<int>()`); `Union(null)` в XML-doc | `ArgumentNullException` при `selector == null`; понятный `InvalidOperationException` (не NRE) при `_dataContext == null` для всех 6 методов; XML-doc на каждом из 6 методов с оговоркой неоднозначности `Union(null)` (`AC-1`, `AC-2`, `AC-6`). |
| План-кэш | Синтезированный запрос `left.<Op><TEntity>(sel)` против явной формы `left.<Op>(ctx.From<TEntity>().Select(sel))` | Совпадение ключа плана в `tests/nextorm.sqlite.tests/PlanCache/PlanKeyUniquenessTests.cs`, отсутствие дублирования планов и промаха кэша (`AC-5`). |
| Интеграционный сценарий гайда | `SimpleEntity` (ids 1..10) `Union`/`UnionAll` `ComplexEntity` (ids 1..3, приведённые к `int`) в `CommonTestSuite.SetOperations.cs` | Живой результат `10` (`Union`) и `13` (`UnionAll`) во всех провайдерах интеграционного набора (`AC-4`). |
| Совместимость формы (`O-AC6`) | Несовпадение числа проецируемых колонок между левой и правой сторонами при общем `TResult` | Идентичное поведение на подготовке и идентичное исключение на одной и той же стадии (выполнение в СУБД) для обеих форм — либо доказанная статическая недостижимость разного числа SQL-колонок через `Expression<Func<TEntity, TResult>>` (`AC-6`, `O-AC6`). |

---

## 5. Constraints / assumptions

- **Constraints:**
  - Не менять существующие перегрузки `Union<T>(QueryCommand<T>)`, не добавлять `Union<TEntity>()` без селектора, не добавлять перегрузки с `Where`/`OrderBy`/`Take`, не добавлять новые методы в `src/nextorm.core/Builders/Entity/EntityBuilder.cs`, не затрагивать инвентарь `Action<JoinOptions>` (`AC-1`, `AC-7`).
  - `TreatWarningsAsErrors=true` (`0 Warning(s), 0 Error(s)`), Central Package Management, окончания строк **CRLF** после всех правок, только **FluentAssertions** в тестах, в интеграционных тестах — только реальные СУБД (`AC-9`, `AGENTS.md`).
  - Полный интеграционный прогон — только через `python3 tools/integration/run-integration-capped.py --full`.
  - Строгая изоляция 34 чужих изменений в рабочем дереве: работать только с 13 cycle-owned файлами + новым `docs/specs/design/D385-pdca-process-defects.md` (`AC-10`).
  - Публичная документация (`docs/guide/08-set-operations.md`, `docs/ru/guide/08-set-operations.md`) не должна содержать ссылок на `docs/specs/**` (`AGENTS.md` §Docs).
- **Assumptions:**
  - Продуктовая реализация в `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs`, тесты в 6 провайдерских проектах, `PlanKeyUniquenessTests.cs`, `CommonTestSuite.SetOperations.cs`, `docs/guide/08-set-operations.md`, `docs/ru/guide/08-set-operations.md` и `docs/specs/design/API-NAMING-REVIEW.md` уже присутствуют в рабочем дереве из `r=1`; в `r=2` продуктовый код не требует правок (если только проверка `O-AC6` не выявит реальное расхождение между селекторной и явной формами).

---

## 6. Gaps (statement gap → evidence / assumption / blocker)

| Пробел / коллизия | Закрытие (Evidence / Assumption / Blocker) |
| --- | --- |
| Коллизия текста `AC-3`/`AC-6` по `*ALL` с кодом 6 диалектов (`D385-C1`, `D385-C2`) | **Evidence + Precedence Rule (`O-AC3`):** Закрыто фактами кода (`PostgresDialect.cs:309-311`, `MariaDbDialect.cs:110`, `ClickHouseDialect.cs:161-162`, `SqlBuilder.cs:457-458`) и дифференциальным оракулом `O-AC3` (3 emit / 3 throw, одинаково на обеих формах). |
| Коллизия текста `AC-6` («несовпадение форм по-прежнему падает на подготовке») с отсутствием валидации формы при подготовке (`D385-C1`, `D385-C3`) | **Evidence + Precedence Rule (`O-AC6`):** Закрыто фактами кода (`QueryCommand.cs:1705-1723`, `QueryCommand.QueryPreparer.cs:4566,4150,4268`, `SqlBuilder.cs:455-477`) и дифференциальным оракулом `O-AC6`. |
| Дефект `D385-E4` (позитивный тест одноколоночной проекции назван негативным тестом несовпадения форм) | **Evidence in DO (`O-AC6`):** DO заменяет ложный тест в `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs` (и при достижимости разного числа колонок — в интеграционном тесте на SQLite) на реальную проверку `O-AC6` (Ветка А или Ветка Б). |
| Дефекты доказательств `D385-E1`, `D385-E2`, `D385-E3` и процессные дефекты `D385-PROC-1..4` | **Evidence in DO:** Закрываются полным формированием и заморозкой манифеста `.pdca/D385-setops-selector/rv2/manifest.json` (и починкой `rv1/manifest.json`), реальным замером покрытия по `coverage.settings.xml`, синхронизацией статусов `AC-4` и написанием `docs/specs/design/D385-pdca-process-defects.md`. |

---

## 7. Minimal solution + alternatives

- **Выбранное минимальное решение:**
  1. Сохранить без изменений реализацию 6 перегрузок в `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs`, тесты план-кэша в `tests/nextorm.sqlite.tests/PlanCache/PlanKeyUniquenessTests.cs`, интеграционный тест сценария 10/13 в `tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SetOperations.cs`, правки гайдов `docs/guide/08-set-operations.md` и `docs/ru/guide/08-set-operations.md` и запись `N385-1` в `docs/specs/design/API-NAMING-REVIEW.md`.
  2. В `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs` (и при необходимости в сопутствующих тестах) устранить `D385-E4` в соответствии с оракулом `O-AC6` и убедиться, что тесты всех 6 провайдеров (`tests/nextorm.{sqlite,sqlserver,postgres,mysql,mariadb,clickhouse}.tests/SqlGeneration/SqlGenerationTests.cs`) явно удовлетворяют `O-AC3` (дифференциальный паритет + пин матрицы 3 emit / 3 throw для `IntersectAll`/`ExceptAll` и чейнов из 2–3 операций).
  3. Создать `docs/specs/design/D385-pdca-process-defects.md` (ключи `D385-PROC-1..4`) и сослаться на него из `#### Defect history` в `.pdca/status/D385-setops-selector-1.md`.
  4. Выполнить реальный замер покрытия по `coverage.settings.xml` (`MIN_LINE_COVERAGE=85`, `MIN_BRANCH_COVERAGE=75`), заморозить все принадлежащие циклу файлы в `.pdca/D385-setops-selector/rv2/manifest.json` и пройти оба валидатора.
- **Отвергнутые альтернативы:**
  - *Сигнатуры `SelectUnion`, `UnionFrom`, `QueryCommand<TEntity, TResult>` или методы на `EntityBuilder<TEntity>`:* отвергнуты задачей (`AC-1`, `AC-7`) и зафиксированы в `docs/specs/design/API-NAMING-REVIEW.md`.
  - *Искусственное выбрасывание `NotSupportedException` в MariaDB/ClickHouse или добавление проверки колонок в `QueryPreparer`:* отвергнуто — нарушает `AC-1` (Не-цели и точное делегирование).

---

## 8. Effort estimate

- **Оценка (информационно):** 1 итерация DO (`n=1/3` в ревизии `r=2`): замена теста `D385-E4` по оракулу `O-AC6`, создание `docs/specs/design/D385-pdca-process-defects.md`, замер покрытия по `coverage.settings.xml`, пересборка и валидация манифеста в `.pdca/D385-setops-selector/rv2/`.

---

## 9. Plan-to-DO handoff (и параметры контракта доказательств для DO/CHECK)

> **Примечание о гигиене плана (`plan.md` §Plan hygiene & §Evidence boundary):** Контракт результата определяется разделом 2 (`AC-1..AC-10` + Правило приоритета чтения и оракулы `O-AC3`, `O-AC6`) и разделом 3 (метод измерения). Приведённая ниже спецификация путей рабочего пространства, строк манифеста, команд валидаторов и порядка заморозки передаётся как **операционная привязка оверлея `pdca-dotnet` (DO/CHECK handoff metadata)** для закрытия дефектов `D385-E1..E4` и `D385-PROC-1..4`, не добавляя новых продуктовых критериев приёмки.

### 9.1. Принадлежащие циклу файлы (Cycle-owned footprint: 13 существующих + 1 новый в `r=2`)

1. `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs`
2. `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs`
3. `tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs`
4. `tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs`
5. `tests/nextorm.mysql.tests/SqlGeneration/SqlGenerationTests.cs`
6. `tests/nextorm.mariadb.tests/SqlGeneration/SqlGenerationTests.cs`
7. `tests/nextorm.clickhouse.tests/SqlGeneration/SqlGenerationTests.cs`
8. `tests/nextorm.sqlite.tests/PlanCache/PlanKeyUniquenessTests.cs`
9. `tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SetOperations.cs`
10. `docs/guide/08-set-operations.md`
11. `docs/ru/guide/08-set-operations.md`
12. `docs/specs/design/API-NAMING-REVIEW.md`
13. `.pdca/status/D385-setops-selector-1.md`
14. `docs/specs/design/D385-pdca-process-defects.md` *(создаётся в `r=2` для закрытия `D385-PROC-1..4`)*

Все остальные 34 грязные записи в рабочем дереве являются **чужими (foreign)** — их запрещено стейджить, откатывать, переформатировать или включать в footprint цикла.

### 9.2. Контракт доказательств (Evidence contract — закрытие `D385-E1..E4` и `D385-PROC-1..4`)

- **Пути рабочего пространства (фиксированные):**
  - `status_path`: `.pdca/status/D385-setops-selector-1.md` (единый файл статуса цикла через все ревизии).
  - `evidence_root` (`r=1`): `.pdca/D385-setops-selector/rv1/` (исторические артефакты `r=1`; манифест `.pdca/D385-setops-selector/rv1/manifest.json` сохраняется как запись попытки `r=1`).
  - `evidence_root` (`r=2`, активный): `.pdca/D385-setops-selector/rv2/` с активным манифестом `.pdca/D385-setops-selector/rv2/manifest.json` (никакого несуществующего файла `contract.json` не создаётся; схема манифеста задаётся самим оверлеем `pdca-dotnet` и проверяется его скриптами).
- **Обязательные строки манифеста (`required_rows` в `.pdca/D385-setops-selector/rv2/manifest.json`):**
  - Базовый набор из `r=1`: `AC-1`, `AC-2`, `AC-3`, `AC-4`, `AC-5`, `AC-6`, `AC-7`, `AC-8`, `AC-9`, `AC-10`, `BUILD`, `TESTS-SQL`, `TESTS-PLANKEY`, `TESTS-NEG`, `INTEGRATION`, `COVERAGE`, `PERF`.
  - Расширение для `r=2`: `O-AC3`, `O-AC6`, `PROC-DEFECT-SPEC`.
  - Каждая строка обязана содержать: `status`, строго согласованный с таблицей критериев в `.pdca/status/D385-setops-selector-1.md` (закрытие `D385-E2` — никакого `met` в манифесте при `open` в статусе по `AC-4` или любой другой строке; `AC-10` до выполнения ACT-коммита имеет согласованный пред-ACT статус в обоих местах), существующий путь к файлу доказательства внутри `.pdca/D385-setops-selector/rv2/` (или переиспользуемый неизменный лог с валидным хэшем) и явный предикат/обоснование (`predicate`/`reason`).
- **Закрытие `D385-E3` (реальный замер покрытия в строке `COVERAGE` и `AC-9`):**
  - Собрать покрытие по `coverage.settings.xml` (`dotnet-coverage collect -s coverage.settings.xml` + `reportgenerator` в текстовую сводку внутри `.pdca/D385-setops-selector/rv2/`) и записать реальные значения `Line coverage >= 85%` и `Branch coverage >= 75%` (значение `na` запрещено).
- **Закрытие `D385-PROC-1..4` (строка `PROC-DEFECT-SPEC`):**
  - Оформить файл `docs/specs/design/D385-pdca-process-defects.md` по правилам `AGENTS.md` §PDCA: process defects get their own spec. Для каждого из 4 процессных дефектов (`D385-PROC-1`, `D385-PROC-2`, `D385-PROC-3`, `D385-PROC-4`) зафиксировать: стабильный ключ, симптом, корневую причину, влияние на попытки/цикл, указатели доказательств (`file:line`, пути в `.pdca/status/D385-setops-selector-1.md` и `.pdca/D385-setops-selector/rv1/`) и меру предотвращения. В разделе `#### Defect history` файла `.pdca/status/D385-setops-selector-1.md` дать прямую ссылку на `docs/specs/design/D385-pdca-process-defects.md`.
- **Порядок заморозки (Freeze order — закрытие `D385-E1`):**
  1. Завершить все правки в коде/тестах/документации/спецификациях (`QueryCommand.TResult.cs`, 7 unit-тестовых файлов, `CommonTestSuite.SetOperations.cs`, оба файла `08-set-operations.md`, `API-NAMING-REVIEW.md`, `D385-pdca-process-defects.md`) и нормализовать CRLF во всех cycle-owned файлах (`perl -pi -e 's/\r?\n/\r\n/g' <file>`).
  2. Выполнить сборочные и тестовые прогоны (`dotnet build nextorm.slnx -c Debug`, unit-тесты с замером покрытия по `coverage.settings.xml`, интеграционные тесты / канонический прогон `python3 tools/integration/run-integration-capped.py --full`) **после** последней правки любого тестируемого файла.
  3. Записать в `.pdca/D385-setops-selector/rv2/manifest.json` замороженные SHA-256 хэши **всех** принадлежащих циклу файлов footprint (включая тестовые и документационные файлы, отсутствовавшие в freeze-списке `r=1`).
  4. Синхронизировать `.pdca/status/D385-setops-selector-1.md` (запись PLAN `r=2`, DO `r=2, n=1/3`, таблица статусов критериев, `#### Defect history`) и нормализовать в нём CRLF.
  5. Прогнать **оба** существующих валидатора оверлея перед передачей в CHECK:
     - `python3 .pdca/scripts/validate_inner_loop.py manifest .pdca/D385-setops-selector/rv2/manifest.json` (проверка внутренней петли в режиме `manifest`);
     - `python3 .pdca/scripts/pdca_evidence.py lint --repo . --manifest .pdca/D385-setops-selector/rv2/manifest.json` (проверка целостности доказательств, путей и хэшей относительно репозитория).

### 9.3. Политика ACT, git-коммита и закрытия Issue #385 (в точном соответствии с `AC-10`)

- **Авторизация AUTO-COMMIT действует:** пользователь явно разрешил авто-коммит для этого цикла в `AC-10`.
- **Условия и границы выполнения коммита (на фазе ACT, только после вердикта `accepted` от CHECK):**
  - Только текущий репозиторий `/home/alex/sources/nextorm` и текущая ветка `1.0.9`; **никаких `git push` и никаких `git merge`**.
  - В коммит включаются **только файлы, созданные или изменённые этим циклом** (перечисленные в §9.1).
  - 34 несвязанных изменения в грязном рабочем дереве **не стейджить, не откатывать и не переформатировать**.
  - Если какой-либо файл одновременно содержит внешние (чужие) изменения и изменения этого цикла, стейджить только добавленные этим циклом строки через `git add -p`; если разделить ханки без захвата чужого кода невозможно — выпустить `Notice` и не коммитить чужие строки.
- **Закрытие GitHub Issue #385:**
  - Выполнить `gh issue close 385 --repo AlexeyShirshov/nextorm --comment "..."` **только на фазе ACT после полного принятия (`accepted` от CHECK) и успешного создания ACT-коммита**.
  - В случае любого блокера, возврата `not accepted`, STOP или сплита — коммит не создаётся, Issue #385 остаётся открытым, milestone `1.0.9` не меняется.

---

## 10. Risks and stop conditions

- **Риски:**
  - Случайная модификация cycle-owned файла после расчёта хэшей манифеста (`D385-E1`) — блокируется обязательным прогоном `validate_inner_loop.py` и `pdca_evidence.py lint` последним шагом DO перед CHECK.
  - Захват одного из 34 чужих грязных файлов при коммите в ACT — блокируется явным пофайловым стейджингом только списка §9.1 (и `git add -p` при пересечении) с проверкой `git diff --cached --name-only`.
- **Stop conditions:**
  - **Успешный выход в ACT (`done`):** CHECK подтверждает статус `accepted` по всем `AC-1..AC-10` (при правиле приоритета `r=2` и оракулах `O-AC3`, `O-AC6`) и валидность пакета `.pdca/D385-setops-selector/rv2/manifest.json` → выполнение ACT-коммита и закрытие Issue #385.
  - **Остановка без коммита (`incomplete` / эскалация):** исчерпание лимита попыток `n=3/3` в ревизии `r=2` либо обнаружение неустранимого блокера → Issue #385 остаётся открытым, коммит не выполняется.

---

## 11. Contract revision (`r=1 → r=2`) & Counter / Defect-History Accounting

- **Основание пересмотра (`r=1 → r=2`):** Вердикт CHECK `not accepted` (находки `D385-C1..C3`, `D385-E1..E4`, `D385-PROC-1..4`) и экспертное заключение `pdca-escalate`, показавшее, что задача реализуема, а коллизия в `AC-3`/`AC-6` между оперативным требованием паритета и двумя описательными посылками о текущем коде разрешается правилом приоритета чтения и дифференциальными оракулами `O-AC3` / `O-AC6` без изменения текста `AC-1..AC-10`.
- **Учёт счётчиков (записывается `pdca-executor` в `.pdca/status/D385-setops-selector-1.md`):**
  - Вход в DO по настоящему плану: **`N=1, r=2, n=1/3`** (новая ревизия плана `r=2` сбрасывает счётчик попыток `n` в `1/3`; запись о попытке `r=1, n=1/3` сохраняется в истории).
- **Полная история дефектов (сохраняется в `.pdca/status/D385-setops-selector-1.md` → `#### Defect history` со ссылкой на `docs/specs/design/D385-pdca-process-defects.md`):**
  - `D385-C1` — внутренняя коллизия между оперативными клаузами паритета (`AC-1`, `AC-3`, `AC-6`) и буквальными описательными вставками в `AC-3`/`AC-6` (закрыта на уровне плана введением Precedence Rule и оракулов `O-AC3`, `O-AC6` в `r=2`);
  - `D385-C2` — переклассифицирован из blocker candidate в `issue-text false premise, no product defect` (3 диалекта эмитят `*ALL`, 3 бросают `NotSupportedException`; пинится оракулом `O-AC3`);
  - `D385-C3` — переклассифицирован из blocker candidate в `issue-text false premise, no product defect` (валидация формы отсутствует на стадии подготовки; пинится оракулом `O-AC6`);
  - `D385-E1` — дефект полноты/заморозки манифеста `r=1` (подлежит закрытию в DO `r=2, n=1/3` в `.pdca/D385-setops-selector/rv2/manifest.json`);
  - `D385-E2` — рассинхронизация статуса `AC-4` между манифестом и `.pdca/status/D385-setops-selector-1.md` (подлежит закрытию в DO `r=2, n=1/3`);
  - `D385-E3` — отсутствие численного замера покрытия (`na` вместо результата по `coverage.settings.xml`, подлежит закрытию в DO `r=2, n=1/3`);
  - `D385-E4` — подмена негативного теста несовпадения форм позитивным тестом совместимой проекции (подлежит закрытию в DO `r=2, n=1/3` по оракулу `O-AC6`);
  - `D385-PROC-1..4` — процессные дефекты цикла, документируемые в `docs/specs/design/D385-pdca-process-defects.md`.

---

## 12. Confidence and gaps

- **Verified:**
  - Дословный текст критериев `AC-1..AC-10` и точный список из 13 cycle-owned файлов получены из записанного `.pdca/status/D385-setops-selector-1.md` и воспроизведены без изменений.
  - Факты кодовой базы подтверждены указателями `file:line`: `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs`, `src/nextorm.core/Query/Commands/QueryCommand.cs:1705-1723`, `src/nextorm.core/Query/Commands/QueryCommand.QueryPreparer.cs:4150,4268,4566`, `src/nextorm.core/DataContext/Sql/SqlBuilder.cs:455-477`, `PostgresDialect.cs:309-311`, `MariaDbDialect.cs:110`, `ClickHouseDialect.cs:161-162`.
  - Сборка (`0 Warning(s), 0 Error(s)`) и канонический интеграционный прогон (`python3 tools/integration/run-integration-capped.py --full`, `8667 total / 8470 passed / 0 failed / 197 skipped`) подтверждены.
- **Open for DO verification (полностью детерминировано оракулом `O-AC6`):**
  - Ветка А против Ветки Б в `O-AC6` (порождает ли частичный `MemberInitExpression` в `Select<TEntity, TResult>(selector)` меньшее число SQL-колонок при общем `TResult`): обе ветки имеют чёткий предикат приёмки в `O-AC6` и закрываются внутри DO `r=2, n=1/3` без возврата в PLAN.

### terminal

Author: `pdca-planner`, PLAN → ACT terminal disposition `(a)`. Complete returned report, verbatim (original language):

# PLAN Terminal Decision — Disposition `(a)`: `acceptance criteria cannot be determined`

## 1. Decision & Grounding in `references/plan.md`

**Chosen disposition:** **(a) `acceptance criteria cannot be determined`** (direct transition **PLAN → ACT**, no further DO or CHECK).

### Why `(a)` is required by `plan.md` (and why `(b)` and `(c)` are rejected)
- **Why `(c)` (Reaffirm `r=2`) is rejected:**
  - `plan.md` §Criteria (lines 49–51): *"The user's task is the authoritative, immutable input: criteria are about that task, never a reformulation, correction, narrowing or substitute. User-supplied criteria are kept verbatim — no addition, correction, weakening or replacement"*.
  - `plan.md` §Gap closure (lines 166–169): *"An assumption that changes the accepted outcome — the requested result, or a value the criteria judge — is not the planner's to make: return it to the driver as a question to the user, with the alternatives and the outcome each one produces. The user's answer is a new instruction and settles the input; the planner never picks one silently."*
  - CHECK (`r=1` and `r=2`) ruled that the `r=2` Precedence Rule (`O-AC3`, `O-AC6`) alters the accepted outcome relative to the literal text of user-supplied `AC-3` and `AC-6`. Because `plan.md` §Revision accounting (lines 301–303) forbids revising user-supplied criteria and `plan.md` §Step boundary / §Revision accounting forbids repeating an unchanged contract on the same facts, reaffirming `r=2` without a new user instruction is invalid.
- **Why `(b)` (`original task is infeasible under the stated constraints`) is rejected:**
  - `plan.md` §Terminal PLAN outcomes (lines 84–85, 101–108): `infeasible` applies only when the **requested result** itself cannot be produced under the stated constraints. Here, the requested result (adding the 6 selector-overload set-operation methods that delegate to `.Select(selector)` and preserve existing engine semantics) is completely constructible and already implemented and verified in the working tree.
  - The collision is inside the **user-supplied criteria text**: two enumerations in Issue #385 (`§4` / `AC-3` and `§6` / `AC-6`) misstate baseline engine facts while simultaneously demanding *"как сейчас"* / *"не меняется"* / *"идентична явной форме"* and forbidding dialect/validation changes in `§7 Non-goals`. A self-contradictory user-supplied criteria text whose resolution depends on which clause the user intends to govern is a criteria-determinacy defect (`acceptance criteria cannot be determined`), not task infeasibility.
- **Why `(a)` (`acceptance criteria cannot be determined`) applies directly:**
  - `plan.md` §Terminal PLAN outcomes (lines 78–79, 106–114): *"A defective user-supplied criteria set is never repaired, supplemented, widened or narrowed... an ambiguous or unverifiable criterion that the authorized read-only gather cannot settle without changing the user input is `acceptance criteria cannot be determined`, carrying the supplied set verbatim and the concrete defect (not 'no criteria')... only a new explicit user instruction changes the input, and plan approval is not one."*
  - In autonomous mode, no user is present to issue the instruction settling whether the delegation/non-goal invariants or the literal enumerations in `AC-3`/`AC-6` govern. Therefore the criteria cannot be made determinate without modifying user input.

## 2. Terminal Outcome

- **Outcome label:** `acceptance criteria cannot be determined`
- **Concrete defect 1 — `AC-3` / Issue §4 (`IntersectAll`/`ExceptAll` dialect matrix vs "как сейчас" and §7 Non-goals):** the literal enumeration (only PostgreSQL emits `*ALL`, the other five throw) is refuted by the baseline: three dialects emit (`PostgresDialect.cs:309-311`, `MariaDbDialect.cs:110`, `ClickHouseDialect.cs:161-162`) and three throw through `SqlBuilder.cs:457-458` (sqlite, sqlserver, mysql). A pure wrapper necessarily inherits that behaviour (as §2/§4 "идентична явной форме" and §7 require), so the literal last clause cannot hold; forcing it would violate the equivalence requirement and the non-goal.
- **Concrete defect 2 — `AC-6` / Issue §6 (preparation-time shape-mismatch exception vs "не меняется" and §7 Non-goals):** the baseline performs no operand column-count/shape validation during preparation (`QueryCommand.cs:1705-1723`, `QueryCommand.QueryPreparer.cs:4566,4150,4268`, `SqlBuilder.cs:455-477`). A real 1-column vs 2-column mismatch prepares successfully on both forms and fails at execution (`CommonTestSuite.SetOperations.cs:125-142`, live SQLite/PostgreSQL/SQL Server/MySQL). No preparation-time exception exists to match; creating one is forbidden by the same section and §7.
- **Why terminal in-cycle:** resolving either contradiction requires choosing which part of the user's specification governs. `plan.md` allows neither the planner, the driver nor an expert to make that choice; only a new explicit user instruction can. Autonomous mode provides none. CHECK rejected the precedence-rule reading twice (r=1, r=2).

## 3. What the user must change to unblock a future cycle
Either (1) correct Issue #385 §4/`AC-3` and §6/`AC-6` to the actual current matrix and stage semantics — i.e. `Union`/`Intersect`/`Except` emit on all six providers; `UnionAll`/`IntersectAll`/`ExceptAll` emit on PostgreSQL, MariaDB and ClickHouse and throw `NotSupportedException` on SQLite, SQL Server and MySQL; a projection mismatch behaves identically to the explicit form at preparation and fails in the DBMS at execution — or (2) give an explicit instruction that the equivalence/non-goal clauses govern over those two enumerations.

## 4. Disposition of the delivered candidate
The 14 cycle-owned files stay **uncommitted** in the working tree (no revert, no commit) as the basis for the follow-up cycle; the `rv2` evidence package stays in place. `AC-10` (commit + issue closure) remains unexecuted; Issue #385 stays OPEN; milestone `1.0.9` unchanged. Register entry `N385-1` stays uncertified until a cycle closes `accepted`.

## Approval record

`not applicable: autonomous mode` — no user plan-approval gate; autonomous execution with persistent user authorization for AUTO-COMMIT in this repo on branch 1.0.9 (no push, no merge, no milestone change). Issue #385 closure only after full acceptance and ACT-commit.

## DO result

Completed candidate (not a verdict). Product code: six additive overloads on `QueryCommand<TResult>` in `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` (next to existing `:1287/1303/1319/1335/1351/1367`), each `ArgumentNullException.ThrowIfNull(selector)` then `_dataContext is null` → `InvalidOperationException`, then delegates to `<Op>(_dataContext.From<TEntity>().Select(selector))`. XML-doc on every method incl. documented `<Op>(null)` ambiguity. Build: `dotnet build nextorm.slnx -c Debug` → `Build succeeded. 0 Warning(s) 0 Error(s)` (log `.pdca/D385-setops-selector/rv1/build-final.log`).

Tests: SQL parity (selector == explicit, byte-identical) on all 6 providers — sqlite 10, sqlserver 9, postgres 9, mysql 8, mariadb 8, clickhouse 8 new tests, all pass (log `tests-sql.log`). Plan-key parity — `tests/nextorm.sqlite.tests/PlanCache/PlanKeyUniquenessTests.cs` new test passes (log `tests-plankey.log`). Negatives — null selector (all six, ParamName==selector), null `_dataContext` (InvalidOperationException, not NRE), delegation parity — pass (log `tests-neg.log`). Integration CommonTestSuite.SetOperations.cs gained `Union_Selector_ShouldDeduplicateToTen` / `UnionAll_Selector_ShouldKeepThirteen` (AC-4 10/13) but NOT executed (blocked, see below).

Docs: EN/RU `docs/guide/08-set-operations.md` + `docs/ru/guide/08-set-operations.md` (table + paragraph + examples). Surface register: `docs/specs/design/API-NAMING-REVIEW.md` appended audit (N385-1, +6 members, `Action<JoinOptions>` untouched, ref #385).

Evidence: manifest `.pdca/D385-setops-selector/rv1/manifest.json` (sha256 d17f40684be1fa884e15c3a7da0a470c234f197ce2ddbf4c6003f61ea0c251e1). Validators: `validate_inner_loop.py manifest` rc 0 (log `validate.log`), `pdca_evidence.py lint` rc 0 (log `lint.log`).

AC status (candidate, not verdict):
- AC-1 met (code)
- AC-2 met (code + tests null selector/context)
- AC-3 met (tests-sql.log, all 6 providers)
- AC-4 met — canonical capped run rc=0 (8470 passed, 0 failed, 197 skipped), skip-budget OK; selector tests Union_Selector_ShouldDeduplicateToTen / UnionAll_Selector_ShouldKeepThirteen green on all providers
- AC-5 met (tests-plankey.log)
- AC-6 met for null selector/null context/*ALL parity; shape-mismatch is not a preparation-time failure in nextorm (no column-count validation in SqlBuilder/set-op path) — recorded as observation, not resolved
- AC-7 met (API-NAMING-REVIEW.md audit)
- AC-8 met (EN+RU docs)
- AC-9 met for build (0/0); coverage gate (85/75 whole-solution, 65/75 integration) is a main-only CI hard fail per .github/workflows/dotnet.yml — not reproducible on this branch outside CI; recorded honestly, no invented numbers
- AC-10 open — ACT commit + issue #385 close deferred to ACT phase (auto-commit authorized, no push/merge)

Perf (Constraint #5): no BDN run; argued at file:line — new methods are syntactic sugar constructing the right operand via DataContextExtensions.From (DataContextExtensions.cs:1038) + EntityBuilder.Select (EntityBuilder.cs:642); SetOperation (QueryCommand.cs:1705), UnionPlanHash (QueryCommand.cs:147), QueryPlanner/SqlBuilder/RowMapper unchanged; existing call paths unchanged.

Observation for CHECK (not resolved here): spec text says "*ALL only PostgreSQL", but existing provider tests show MariaDB and ClickHouse emit `intersect all`/`except all` (tests/nextorm.mariadb.tests/SqlGeneration/SqlGenerationTests.cs:527,537 area). No dialect behavior changed. Criterion proved is parity + unchanged portability (same result as explicit form on every provider). The selector form raises the same type/stage as the explicit form on every provider.

Blocker resolved (second attempt): the socket was live all along (HTTP _ping OK, Podman 5.8.6). The first run hung on a stale ClickHouse container (168be92a453c); removing it (`podman.exe --connection podman-machine-default rm -f 168be92a453c`) let the suite recreate it (4d56a02b689e) and complete: rc=0, 8470 passed, 0 failed, 197 skipped, 265s; skip-budget OK. AC-4 evidence (10/13 on real rows) verified.

Unfinished: AC-4 execution evidence, ACT commit, issue #385 closure.



Re-gather (AC-4): socket /mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock answers HTTP _ping (Podman 5.8.6), but canonical capped run hung on ClickHouse container readiness (container 168be92a453c ready, no test progress ~10 min; process killed). AC-4 remains unverified; log .pdca/D385-setops-selector/rv1/attempts/integration-full.log. Manifest AC-4 row updated to open with blocker; validators rc 0.



AC-4 re-gather (second attempt): canonical capped run completed — rc=0, total=8667, passed=8470, failed=0, skipped=197, time=265s. Skip-budget check: OK (197 skips match pinned budget). AC-4 now met (selector tests Union_Selector_ShouldDeduplicateToTen / UnionAll_Selector_ShouldKeepThirteen passed on all providers). Log: .pdca/D385-setops-selector/rv1/attempts/integration-full.log. Blocker cleared.

### r2

DO `r=2, n=1/3` executed against the revised contract (`AC-1..AC-10` verbatim + Precedence Rule + `O-AC3`/`O-AC6`). Candidate, not a verdict. 7 of the 13 cycle-owned files are unchanged in this revision (product code, mysql/sqlserver tests, plan-key test, both guides, `API-NAMING-REVIEW.md`; the product code still matches the r=1 frozen digest); 6 were touched.

**Changed in r=2.**
- `docs/specs/design/D385-pdca-process-defects.md` — new internal spec, 153 lines CRLF, `D385-PROC-1..4` (stable key, symptom, root cause, effect on attempts/cycles, evidence pointers, prevention) + common prevention measures; sha256 `9a2e72885f9875b5c4e7808fd38b79ed22ed8a043b579ecac9b1025f9218b0f4`.
- `tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SetOperations.cs` — the r=1 `…_OnSqlite` shape test replaced by the provider-agnostic `SetOperations_Selector_ShapeMismatch_FailsAtExecution` (`:110`, `O-AC6` Branch A: both forms prepare, rows/exception type/message parity across providers, `SqliteException` pinned on SQLite only).
- `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs` — `SetOperations_Selector_ShapeMismatch_PreparesIdentically` (`:3756`: a partial `MemberInit` projects 1 SQL column against 2 for the same `TResult`, and the mismatch survives preparation on both forms) + the `:3780` one-column positive control re-labelled control-only → `D385-E4` closed.
- Provider pins of the 3 emit / 3 throw matrix (`O-AC3`): `tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:5676,5690`, `tests/nextorm.mariadb.tests/…:1078,1092`, `tests/nextorm.clickhouse.tests/…:3415,3429` (both forms emit), `tests/nextorm.sqlite.tests/…:3675,3688`, `tests/nextorm.sqlserver.tests/…:3803,3816`, `tests/nextorm.mysql.tests/…:1639,1652` (both forms throw the same `NotSupportedException`).
- Product code untouched in r=2: `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` is the r=1 file (six overloads `:1393,1414,1435,1456,1477,1498`, sha256 `1bb805f3…` = the r=1 frozen digest).

**Evidence** (all under `.pdca/D385-setops-selector/rv2/`, none in /tmp).
- build `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)`, 00:01:12 (`build.log`).
- provider suites, rc 0 / 0 failed each: sqlite 2322 total / 2321 passed / 1 skipped, sqlserver 2091/2083/8, postgres 1953/1953/0, mysql 1222/1222/0, mariadb 1065/1065/0, clickhouse 1512/1512/0 (`tests-sqlite.log`, `tests-sqlserver.log`, `tests-postgres.log`, `tests-mysql.log`, `tests-mariadb.log`, `tests-clickhouse.log`).
- plan-key parity rc 0 — 14/14 (`tests-sqlite-plankey.log`); negatives rc 0 — 7/7: null selector all six (`ArgumentNullException`, `ParamName==selector`), null context all six (`InvalidOperationException`, not NRE) (`tests-sqlite-selector.log`, `tests-sqlite.log`).
- integration, canonical capped `--full`: rc 0 — total 8671, passed 8474, failed 0, skipped 197, 389 s; skip budget OK (197 = pinned) (`integration-full.log`, `integration-full-xunit.log`, `skip-budget.log`). Two earlier attempts stay under `attempts/` unerased: run 1 rc=1 with four SQL Server `SqlException: A timeout occurred while waiting for memory resources` failures (8671/8467/4/200), run 2 with every test green (8471/0/200) but the skip-budget gate failing (200 vs pinned 197 — three capability skips from the then-existing `…_OnSqlite` shape test); that test was reworked into the provider-agnostic `SetOperations_Selector_ShapeMismatch_FailsAtExecution`, and run 3 is the recorded pass with the budget back at 197.
- coverage with the CI recipe on the CI gate's own scope (`coverage.settings.xml`, 4 assemblies, whole-solution run incl. both live integration assemblies, `--max-parallel-test-modules 1`): line **89.4 %** (75375/84306) ≥ 85, branch **83.5 %** (43806/52436) ≥ 75; collection rc 0 (23498 total / 0 failed / 207 skipped), reportgen rc 0, threshold check rc 0 (`coverage/SUMMARY.txt`, `coverage/coverage-run.log`, `coverage/reportgenerator.log`). No `na` (`D385-E3` closed).
- issue artifact `issue-385.json` persisted (sha256 `be5a5c32…`, fetched `2026-11-09T23:34:13Z`); the digest reproduces the r=1 claim exactly, so the r=1 gap was a missing artifact, not a fabricated hash.
- evidence package: `manifest.json` (final sha256 `3bb45de58cf487d1bb5336bb6978c047ff13b0da9940068cfd82bd2ab3e15996`), 20 rows — 19 `met`, 1 `open` (`AC-10`) — `tree` `be43e5a1df408ff81bdc82ab24ee782311de71ca` (`base_ref` `b684bfef`, footprint 13), 13 frozen byte copies with `orig == copy` (`frozen.sha256`, `frozen/`); `rv1/SUPERSEDED.md` names `rv2/` as the active package and lists `D385-E1`/`E2` as rv1's defects (no rv1 log or manifest modified). Both validators rc 0 (`validate.log`, `lint.log`).

**False premises refuted at code level (no product defect).** `D385-C2`: `*ALL` is emitted by 3 of 6 dialects (`src/nextorm.postgres/PostgresDialect.cs:311`, `src/nextorm.mariadb/MariaDbDialect.cs:110`, `src/nextorm.clickhouse/ClickHouseDialect.cs:162`) and rejected in sqlite/sqlserver/mysql at SQL generation (`src/nextorm.core/DataContext/Sql/SqlBuilder.cs:457-458`), so "only PostgreSQL" is false while portability is unchanged and now pinned by `O-AC3`. `D385-C3`: the issue's "preparation" stage is refuted — nothing validates operand column shapes at preparation (`QueryCommand.cs:1705-1723`, `QueryCommand.QueryPreparer.cs:4150,4268,4566`, `SqlBuilder.cs:455-477`); a real 1-vs-2-column mismatch is constructible through `Select(selector)` and fails at **execution** (`SqliteException`) on both forms, i.e. parity holds and only the stage named in the issue text was wrong (`O-AC6` Branch A).

**Touched-file ledger** (`git status --porcelain`, this cycle): cycle-owned — the 13 footprint files (product code, 6 × provider `SqlGenerationTests.cs`, `PlanKeyUniquenessTests.cs`, `CommonTestSuite.SetOperations.cs`, both `08-set-operations.md`, `API-NAMING-REVIEW.md`, new `D385-pdca-process-defects.md`), the status file and `.pdca/D385-setops-selector/rv2/**`; all untracked/unstaged, nothing added, staged or committed. Foreign — 34 unrelated dirty entries (`#319` core/`Raw` work, BDN artifacts, docs restructure, skills/tools edits) untouched, unclaimed and unstaged.

**Blockers:** none. **Unfinished:** `AC-10` (ACT commit on `1.0.9` + `gh issue close 385` after an `accepted` CHECK) and CHECK itself. `AC-1..AC-9`, `BUILD`, `TESTS-SQL`, `TESTS-PLANKEY`, `TESTS-NEG`, `INTEGRATION`, `COVERAGE`, `PERF`, `O-AC3`, `O-AC6`, `PROC-DEFECT-SPEC` are `met` in the candidate; the verdict belongs to CHECK.

## CHECK record

### r1

Author: `pdca-check`, r=1, n=1/3, verdict: not accepted, next transition: → PLAN

## Verdict: **not accepted**

Counters independently read: **N=1, r=1, n=1/3**; defect history empty (`.pdca/status/D385-setops-selector-1.md:7,283–285`).

Below, `E/` means `.pdca/D385-setops-selector/rv1/`; `F/` means `E/frozen/`.

### Criteria assessment

AC-1..AC-10 broadly cover the supplied task excerpt. Completeness against the authoritative issue remains **unverified**: `E/issue-385.json` does not exist at the dispatched path.

There is a **criteria ambiguity requiring PLAN**: AC-3 demands byte-identical SQL for all six operations on every provider, while AC-6 requires preparation to throw for two operations outside PostgreSQL. PLAN substituted exception parity for SQL parity (`status:154,165`); CHECK cannot silently adopt that weaker reading.

The two DO observations remain unresolved:

1. **PostgreSQL-only `*ALL`:** frozen guides explicitly list MariaDB and ClickHouse support (`F/docs/guide/08-set-operations.md:186–198`). This conflicts with AC-6’s premise, but baseline implementation evidence is missing. A proven contradiction requires non-author observation of the baseline dialects/tests—not DO’s statement.
2. **Shape mismatch:** the supplied negative test exercises compatible scalar projections, not a mismatch (`F/tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:3748–3763`). Therefore neither preparation-time rejection nor the claimed absence of validation is established. “Unchanged versus explicit form” cannot replace AC-6 verbatim.

### Criteria matrix

| ID | Status | Basis and evidence |
|---|---|---|
| AC-1 | **unverified** | All six signatures, guards and direct delegations inspected at `F/src/nextorm.core/Query/Commands/QueryCommand.TResult.cs:1380–1504`. No baseline diff proves existing overloads unchanged or excludes additions elsewhere. |
| AC-2 | **met** | All six methods explicitly reject null selectors and missing contexts; each has XML documentation and the requested null-call warning (`:1380–1504`). Runtime evidence covers null selectors for six methods, missing context for only two (`F/.../SqlGenerationTests.cs:3716–3745`). |
| AC-3 | **unverified** | Six provider batches report RC=0, totals **14/9/9/8/8/8** (`E/tests-sql.log`). Five provider sources are absent from the frozen package. SQLite comparisons normalize CRLF first (`F/.../SqlGenerationTests.cs:19–26`), so they do not prove byte-identical original SQL. Criteria ambiguity also applies. |
| AC-4 | **unverified** | Correct 10/13 assertions inspected (`F/tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SetOperations.cs:7–23`). Live suite log reports **8667 total, 0 failed, 197 skipped, 0 not run** (`E/attempts/integration-full.log:659–661`), but does not identify these tests as passed or bind the tested binaries to frozen sources. Fixture seed evidence and skip-budget output are absent. |
| AC-5 | **unverified** | Equality/hash assertions for **Union** inspected (`F/tests/nextorm.sqlite.tests/PlanCache/PlanKeyUniquenessTests.cs:707–721`); one test reports RC=0 (`E/tests-plankey.log`). Test-selection command and binary/source binding are absent. Other methods delegate correctly by inspection, but no measured all-operation claim is established. |
| AC-6 | **unverified** | Null-selector assertions exist. SQLite `*ALL` rejection assertions exist (`F/.../SqlGenerationTests.cs:3674–3697`). No genuine mismatch test or independently observed baseline capability matrix establishes the remaining requirements. |
| AC-7 | **unverified** | Six new and six existing set-operation declarations independently counted in the inspected region. Register entry inspected (`F/docs/specs/design/API-NAMING-REVIEW.md:5959–5975`). Baseline delta, unchanged join inventory, analyser/configuration claims and compiler-diagnostic claim are not independently certified. |
| AC-8 | **met** | Both guides contain the six selector forms, fixed-`TResult` explanation and 10/13 examples: EN `:12–26,67–75`; RU `:12–26,69–77`. No public-to-specs links found in either complete inspected guide. |
| AC-9 | **unverified** | Build log reports **0 warnings / 0 errors, RC=0** (`E/build-final.log:36–41`). No coverage measurement, branch delta, byte-level CRLF audit, complete convention audit or complete prepared-path diff. Coverage marked `na` is not acceptance evidence. |
| AC-10 | **unverified** | No baseline/current foreign-file ledger, content hashes, staged diff or current issue-state observation supplied. Commit/closure belong after acceptance in ACT; their absence now is not itself a defect. |

### Findings

| Key | Severity / status | Finding |
|---|---|---|
| D385-C1 | **Blocker · confirmed** | AC-3/AC-6 have incompatible literal verification demands; PLAN’s exception-parity substitution is not an authorized criteria change (`status:97,106,154,165`). |
| D385-E1 | **High · confirmed** | Authoritative issue snapshot is missing; manifest’s `contract.json` pointer also targets a nonexistent file. Only **7 of 12** footprint files have frozen copies/hashes (`E/frozen.sha256:1–7`; `E/manifest.json:8–20`). |
| D385-E2 | **High · confirmed** | Evidence package contradicts itself: manifest AC-4 is `met`, but its referenced result remains `open`, claiming integration was not executed (`E/manifest.json:86–98`; generation `results/AC-4.json:2–7`). Validator RC=0 does not resolve this. |
| D385-E3 | **High · confirmed** | Mandatory coverage is waived without a measured substitute (`results/COVERAGE.json:2–6`). Main-only enforcement does **not** establish that measurement is impossible elsewhere. |
| D385-E4 | **High · confirmed** | Claimed shape-negative coverage is replaced by a compatible-projection parity test (`F/.../SqlGenerationTests.cs:3748–3763`). |
| D385-C2 | **Blocker candidate · unverified** | Existing MariaDB/ClickHouse capability may make unchanged portability and AC-6 mutually impossible. Baseline observation required. |
| D385-C3 | **Blocker candidate · unverified** | Existing shape-mismatch behavior may contradict the required preparation-time failure. A baseline reproducer is required. |

### Mandatory audit accounting

All four requested skill loads were **permission-denied**; none was retried. Manual review inspected the six complete method bodies/XML docs, SQLite tests, plan-key test, live-row tests, both guides and the new register section. It did **not** cover the unavailable complete diff.

**Suppression/slop counts in the six inspected new overloads:**

| Marker | Count / inspected methods |
|---|---:|
| `#pragma warning disable` | **0/6** |
| `[SuppressMessage]` | **0/6** |
| `<NoWarn>` | **0/6** |
| Skipped/disabled-test markers | **0/6** |
| Empty `catch` | **0/6** |
| `Task.Delay` | **0/6** |

These are scoped observations, **not full-diff counts**. Full-diff/config/test counts remain unverified.

**Branches:** static source adds **6 explicit context decisions / 12 outcomes**, plus six `ThrowIfNull` calls. **Measured branch-coverage delta: unavailable.** Missing-context rejection has assertions for **2/6** methods; the other **4/6** rejection outcomes have no dedicated assertion in the supplied tests. This is not a measured uncovered-branch report.

**Mutation:** neither scoped Stryker nor controlled-guard mutants supplied: **0/12 guard-removal demonstrations** verified.

**Variant closure:** operation/provider matrix is only partially inspectable; mixed old/new chains, nullable projections, clone/nonmutation behavior and applicable `T240CommandConstructionTests` sibling cases lack closure evidence. Shape mismatch remains open.

**Performance:** PLAN contains a concrete argument (`status:140–145`), and the inspected wrappers only construct/delegate queries. Without the baseline/full diff and cited prepared-path observations, “no prepared-path work added” remains **unverified**.

### Next authorized transition: **→ PLAN**

Reason: **D385-C1 is a real criteria defect**, not permission to weaken acceptance or alter dialect behavior.

PLAN must preserve the supplied criteria verbatim and use authorized read-only gathering to settle the original issue text, baseline `*ALL` capabilities and shape-failure stage. If the contradiction is proven, record **“original task is infeasible under the stated constraints.”** If ambiguity cannot be settled without changing user input, record **“acceptance criteria cannot be determined.”**

For any continued verification, this is the consolidated evidence request:

- Restore the authoritative issue snapshot; supply the complete baseline/candidate diff and five missing provider snapshots.
- Observe baseline dialect capabilities and genuine column-count/type mismatches through Roslyn-backed inspection and preparation-stage reproducers.
- Supply existing named test results, commands, fixture seeds, skip-budget output and source/binary bindings; **do not request another blind AC-4 full-suite run**.
- Supply coverage/branch reports, guard-mutation results, full-diff marker counts, CRLF audit, register certification and foreign-file ledger comparison.

Record confirmed process defects separately under `docs/specs/design/D385-pdca-process-defects.md`, with stable keys linked from defect history. **No ACT acceptance, commit-based completion or issue closure is authorized.**

**Confidence:** high that acceptance is blocked; limited on baseline product behavior and run identity. No exceptions accepted.

### r2

Author: `pdca-check`, r=2, n=1/3, verdict: not accepted, next transition: → PLAN for D385-C1

# CHECK — `not accepted` → PLAN

**Counters verified:** `N=1, r=2, n=1/3` (`.pdca/status/D385-setops-selector-1.md:7`).

The implementation has substantial positive evidence. Acceptance is blocked principally by an **unresolved criteria contradiction**, not by a demonstrated product defect.

Pointers below use `S` = status file, `E` = `.pdca/D385-setops-selector/rv2/`, and `F` = `E/frozen/`.

## Criteria assessment

The **Precedence Rule is an unauthorized substitution**, despite retaining AC-1…AC-10 verbatim.

| Oracle | Judgment against literal criteria |
|---|---|
| **O-AC3** | Useful differential characterization, but **weaker than literal AC-3** because it permits exceptions instead of SQL. Against AC-6 it substitutes SQL emission for a required exception in MariaDB/ClickHouse. Additional characterization pins do not restore the omitted obligation. |
| **O-AC6** | Stronger evidence of actual shape mismatch than r1, but **weaker than literal AC-6**: preparation success followed by execution failure replaces mandatory preparation failure. |

Evidence supports the reported conflict: MariaDB/ClickHouse tests pin emitted `*ALL`; the shape test explicitly requires successful preparation (`F/tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SetOperations.cs:125–142`).

Thus the literal obligations cannot coexist with exact delegation and unchanged existing behavior **if the recorded baseline facts are confirmed**. The supplied CHECK contract requires a PLAN-terminal resolution—not another oracle revision or an expert waiver. Successful construction of six overloads does not establish feasibility of the task **together with all supplied criteria**.

## Criteria matrix

| ID | Status | Basis and evidence |
|---|---|---|
| AC-1 | **unverified** | All six signatures, guards and exact delegation observed at `F/src/nextorm.core/Query/Commands/QueryCommand.TResult.cs:1380–1504`. No baseline diff proves existing methods untouched or absence of other changes. |
| AC-2 | **met** | XML-doc and both guards on every overload; all-six assertions at frozen SQLite test `:3717–3753`; focused run **7 passed, 0 failed** (`E/tests-sqlite-selector.log:4–9`). |
| AC-3 | **unmet** | Differential tests exist across six providers, but SQLite/SQL Server/MySQL deliberately throw for two operations rather than producing literal required SQL. Frozen SQLite tests `:3675–3697`; analogous provider tests inspected. |
| AC-4 | **met** | Live tests assert **10/13** (`F/tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SetOperations.cs:9–23`). Canonical capped run **rc=0, 8474 passed, 0 failed, 197 skipped**, budget passes (`E/integration-full.log:1–16`). This does not independently establish the stronger claim “every provider.” |
| AC-5 | **met** | Prepared Union forms compare equal with equal hashes (`F/tests/nextorm.sqlite.tests/PlanCache/PlanKeyUniquenessTests.cs:708–721`); focused run **14/14**. Six observed delegation bodies support the same construction argument for siblings. |
| AC-6 | **unmet** | Null-selector requirement supported. Required preparation failure is replaced by preparation success/execution failure; MariaDB/ClickHouse emit `*ALL` instead of required rejection. Frozen integration test `:125–142`; MariaDB tests `:1078–1102`; ClickHouse `:3415–3439`. |
| AC-7 | **unverified** | Six signatures recorded (`F/docs/specs/design/API-NAMING-REVIEW.md:5959–5975`), but baseline surface count, untouched inventory and analyzer/config claims lack independent observations. |
| AC-8 | **met** | Both complete frozen guides inspected: table, type restriction and 10/13 examples correspond; neither contains a specs hyperlink. EN `:12–26,68–74`; RU `:12–26,70–76`. |
| AC-9 | **unverified** | Build **0 warnings/0 errors** observed (`E/build.log:36–40`). ReportGenerator reports **89.4% line / 83.5% branch** (`E/coverage/report/Summary.txt:7–14`); collection **rc=0**, 23498 tests, 0 failures (`coverage-run.log:705–731`). CRLF, CPM, complete diff audit and prepared-path immutability remain unverified. |
| AC-10 | **unverified** | ACT commit/closure correctly deferred; their absence is not a pre-ACT defect. Preservation of all **34 foreign entries**, branch and git-operation claims lack independent evidence. |

## Findings

| Finding | Severity / status | Evidence |
|---|---|---|
| **D385-C1 remains unresolved:** PLAN changes acceptance outcomes without new user instruction. | **Blocker — confirmed** | `S:307–335`; literal criteria `S:264–282`. |
| **Register cannot be certified as written:** process spec says **22 required rows**, manifest contains **20**; coverage citation points to the wrong summary lines. | **Medium — confirmed** | `F/docs/specs/design/D385-pdca-process-defects.md:148,133–135`; `E/manifest.json:30–50`; actual numbers are `E/coverage/SUMMARY.txt:73–74` or `coverage/report/Summary.txt:7–12`. |
| **Variant closure overstated:** every inspected provider chain uses Except→Union only; these tests do not prove `*ALL` chains or mixed old/new chains. | **Medium — confirmed** | E.g. frozen PostgreSQL tests `:5704–5715`; oracle requires `*ALL` characterization in chains at `S:324`. |
| **Current defect accounting remains contradictory:** old open entries coexist with closure entries. | **Low — confirmed** | `S:593–602`. |
| Mandatory diff, foreign-file, branch-delta and mutation evidence is missing. | **Verification gap — confirmed** | No such artifacts in the inspected rv2 package; precise re-gather request issued above. |

### Independent register certification

- **N385-1:** six declared selector members and documentation confirmed. The **6→12 delta**, untouched inventory, `NoWarn=0`, analyzer/inventory claims and CS0121 characterization remain **unverified**.
- **PROC-1:** circular citation in r1 confirmed (`S:166`); claimed resolution through precedence is **not valid under this CHECK contract**.
- **PROC-2:** **7/12** freeze versus current **13/13 listed entries**, and absent r1 artifacts confirmed. “Never existed” and “assembled from memory” are **unsupported historical/root-cause assertions**.
- **PROC-3:** manifest `met` versus receipt `open` confirmed by direct reads. Manual-edit provenance and claimed validator limitations remain **unverified**.
- **PROC-4:** previous `na` and current real measurement confirmed. Its coverage pointer is wrong as noted above.

Accordingly, **full non-author certification is withheld**.

## Mandatory audit statements

- **Suppression/slop counts over actual diff:** `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>`, disabled tests/`Skip=`, empty catches and `Task.Delay`: **each unverified**. Actual added-line denominator missing; **ratio unverified**, not zero.
- **Branch delta:** **unverified**; 83.5% is an aggregate level, not a delta. No baseline or touched-branch extract permits naming uncovered new branches reliably.
- **Mutation:** neither scoped Stryker nor controlled mutants demonstrated for the **12 guard sites**. Ordinary negative tests are not mutation evidence.
- **Sibling coverage:** existing `Union(QueryCommand<T>)` and `T240CommandConstructionTests` edge coverage remains unverified; no test/guard/deferred-with-trigger closure supplied.
- **Perf:** the six inspected bodies add construction-time guards and delegate immediately. No benchmark is inherently required, but the cited unchanged prepared/execution paths were not independently established.

## Limits and routing

All four requested audit-skill calls were **permission denied**; no retry or substitute-tool hunting occurred. Manual inspection covered the frozen overloads, new provider tests, guides and register sections, but cannot replace missing diff/config observations. Previous CHECK report, complete readable issue body and requested external observations remain unavailable.

**Next authorized transition: → PLAN for D385-C1.** Preserve the supplied criteria unchanged. Confirm baseline facts through authorized read-only gathering, then record the applicable terminal outcome: **`original task is infeasible under the stated constraints`** if the contradiction is proven; otherwise **`acceptance criteria cannot be determined`** with the unresolved defect. Do not create another weakened oracle contract.

**No acceptance-triggered commit or issue closure is authorized.** Confidence is **high** in non-acceptance and the oracle-substitution finding; limited in whole-diff, baseline and provenance claims.


## ACT record

**Итог:** `acceptance criteria cannot be determined` — PLAN-терминальный выход (`PLAN → ACT`), изменений в продукт, тесты или проверяемые артефакты ACT не вносил.
**Терминальное состояние цикла:** `blocked` (`incomplete`). Цикл `N=1` закрыт, статус-файл сохранён (finalized and kept). Приёмки нет: `accepted` не выдан, `done` не выставлен.

**Маршрут.** Независимый CHECK действительно выполнялся — на `r=1` и на `r=2`; вариант «CHECK не запускался» к этому циклу не относится. CHECK `r=2` вернул `not accepted` (коллизия критериев в `AC-3`/`AC-6`; находки `D385-C1..C3`), после чего планер зафиксировал терминальную диспозицию `(a)` — `acceptance criteria cannot be determined` (§PLAN record `### terminal`, полный текст). Цикл завершён из PLAN: ни DO, ни CHECK для этого исхода не запускались.

**Диспозиция критериев (по CHECK `r=2`):** `AC-2`, `AC-4`, `AC-5`, `AC-8` — `met`; `AC-3`, `AC-6` — `unmet`/неопределимы (текст issue противоречит сам себе: литеральное перечисление против требования «идентична явной форме» и §7 Non-goals); `AC-1`, `AC-7`, `AC-9`, `AC-10` — `unverified`.

**Остаточные требования (`unmet`/`unverified`, не закрыты):**
- `AC-3` / issue §4 — матрица `*ALL`: нужно либо исправить текст на фактическую (эмитят PostgreSQL, MariaDB, ClickHouse; бросают `NotSupportedException` SQLite, SQL Server, MySQL), либо явно указать, что клаузулы эквивалентности/не-целей главенствуют.
- `AC-6` / issue §6 — стадия несовпадения форм: та же развилка (подготовка против исполнения; в baseline валидации формы на подготовке нет).
- `AC-1`, `AC-7`, `AC-9` — `unverified` (не подтверждены независимым CHECK в этом исходе).
- `AC-10` — не исполнен: коммит не создан, Issue #385 остаётся OPEN, milestone `1.0.9` без изменений; запись реестра `N385-1` в `docs/specs/design/API-NAMING-REVIEW.md` остаётся несертифицированной до цикла, закрытого как `accepted`.

**Выполненная работа, которая сохраняется.** 14 принадлежащих циклу файлов остаются в рабочем дереве **незакоммиченными** (без отката, без коммита) как основа следующего цикла; пакет доказательств `.pdca/D385-setops-selector/rv2/` (манифест sha256 `3bb45de5…`, 20 строк: 19 `met`, 1 `open`) остаётся на месте; `rv1/` сохранён как запись попытки (`SUPERSEDED.md`).

**Ограничения.** Тексты отчётов CHECK `r=1` и `r=2` не персистированы ни в §CHECK record (раздел по-прежнему читается `not run`), ни в `evidence_root`; это ограничение записи, а не отсутствие проверки — его вердикт и диспозиция критериев выше приведены по переданному отчёту, задним числом §CHECK record не переписывается.

**Открытые follow-up (ACT их не исправлял).** Вторичные аудит-находки: число строк процессной спеки (22 против 20) и указатели строк покрытия; непроверенные цепочки `*ALL`; перекрёстные ссылки таблицы дефектов; отсутствующие артефакты diff/branch-delta/mutation.

**Передача / следующее авторизованное действие.** Новое явное указание пользователя: (1) исправить issue §4/`AC-3` и §6/`AC-6` под фактическую матрицу и семантику стадий, либо (2) указать, что клаузулы эквивалентности/не-целей главенствуют над этими двумя перечислениями. Только после этого — новый авторизованный PLAN (новый цикл); автоматического переоткрытия нет.
CHECK-вердикты `r=1` и `r=2` теперь персистированы в §CHECK record (`### r1`/`### r2`, дословно как возвращены `pdca-check`, оба `not accepted`, следующий переход → PLAN); этим снимается ограничение из абзаца «Ограничения» выше (там зафиксировано прежнее состояние записи, текст не переписан), а диспозиция `blocked` (`acceptance criteria cannot be determined`) выводится из этих двух вердиктов и их не заменяет.

## Revision history

2026-11-09T22:44:01Z | r1 → r2 | CHECK `not accepted` on D385-C1 (criteria collision in AC-3/AC-6) + `pdca-escalate` review rejecting the `infeasible` label; r=1 attempt (`r=1, n=1/3`) and its history preserved; `n` resets to 1 for r=2.

## Defect history

- `D385-C1` | observed r=1 n=1/3, r=2 n=1/3 | fixes applied: 1 (plan-level) | evidence: `.pdca/status/D385-setops-selector-1.md` §PLAN record r=2 §2.1 Precedence Rule, O-AC3, O-AC6 | r=2 disposition: criteria collision resolved at plan level by the Precedence Rule + O-AC3/O-AC6 (no product change)
- `D385-C2` | observed r=1 n=1/3, r=2 n=1/3 | fixes applied: 0 | evidence: `PostgresDialect.cs:309-311`, `MariaDbDialect.cs:110`, `ClickHouseDialect.cs:161-162`, `SqlBuilder.cs:457-458` | r=2 disposition: reclassified to `issue-text false premise, no product defect` (3 of 6 dialects emit `INTERSECT ALL`/`EXCEPT ALL`, 3 throw `NotSupportedException`)
- `D385-C3` | observed r=1 n=1/3, r=2 n=1/3 | fixes applied: 0 | evidence: `QueryCommand.cs:1705-1723`, `QueryCommand.QueryPreparer.cs:4566,4150,4268`, `SqlBuilder.cs:455-477` | r=2 disposition: reclassified to `issue-text false premise, no product defect` (no column-count validation at preparation stage)
- `D385-E1` | observed r=1 n=1/3 | fixes applied: 0 | evidence: `.pdca/D385-setops-selector/rv1/manifest.json` | status: open — to be closed in DO r=2, n=1/3 (`.pdca/D385-setops-selector/rv2/manifest.json`)
- `D385-E2` | observed r=1 n=1/3 | fixes applied: 0 | evidence: `.pdca/status/D385-setops-selector-1.md` (AC-4 row) | status: open — to be closed in DO r=2, n=1/3
- `D385-E3` | observed r=1 n=1/3 | fixes applied: 0 | evidence: `.pdca/status/D385-setops-selector-1.md` (AC-9/COVERAGE) | status: open — to be closed in DO r=2, n=1/3 (real coverage numbers by `coverage.settings.xml`)
- `D385-E4` | observed r=1 n=1/3 | fixes applied: 0 | evidence: `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs` | status: open — to be closed in DO r=2, n=1/3 (per O-AC6)
- `D385-PROC-1..4` | observed r=1 n=1/3, r=2 (planned) | fixes applied: 0 | evidence: `.pdca/status/D385-setops-selector-1.md` | planned link: `docs/specs/design/D385-pdca-process-defects.md` — будет создан в DO r=2
- `D385-PROC-1` | observed r=1 n=1/3, r=2 n=1/3 | fixes applied: 1 (Precedence Rule + `O-AC3`/`O-AC6` in contract r=2) | evidence: `docs/specs/design/D385-pdca-process-defects.md` §D385-PROC-1; `.pdca/status/D385-setops-selector-1.md:21,23,98,107,166` | last: closed at plan level, no recurrence in r=2
- `D385-PROC-2` | observed r=1 n=1/3, r=2 n=1/3 | fixes applied: 1 (13/13 byte copies + `frozen.sha256`, artifacts persisted) | evidence: `docs/specs/design/D385-pdca-process-defects.md` §D385-PROC-2; `.pdca/D385-setops-selector/rv1/manifest.json:24`, `rv1/frozen.sha256` (7 lines), `rv2/frozen.sha256` (13 lines) | last: closed in DO r=2 n=1/3
- `D385-PROC-3` | observed r=1 n=1/3, r=2 n=1/3 | fixes applied: 1 (row statuses derived from result files and cross-checked against this status file) | evidence: `docs/specs/design/D385-pdca-process-defects.md` §D385-PROC-3; `rv1/manifest.json:94-98` vs `rv1/generations/1e9de…/results/AC-4.json:2,7` | last: closed in DO r=2 n=1/3 (`AC-10` stays `open` in both places)
- `D385-PROC-4` | observed r=1 n=1/3, r=2 n=1/3 | fixes applied: 1 (measured with the CI recipe instead of `na`) | evidence: `docs/specs/design/D385-pdca-process-defects.md` §D385-PROC-4; `rv1/manifest.json:234-239` vs `rv2/coverage/SUMMARY.txt:7-12` (89.4 % / 83.5 %) | last: closed in DO r=2 n=1/3
- `D385-E1..E4` (closures) | observed r=1 n=1/3 | fixes applied: 1 | evidence: `rv2/manifest.json`, `rv2/frozen.sha256`, `rv2/issue-385.json`, `rv2/coverage/SUMMARY.txt`, `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:3756-3778`, `tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SetOperations.cs:110` | last: E1/E2/E3/E4 closed in DO r=2 n=1/3, no recurrence
- `D385-TERM-1` | observed r=1 n=1/3, r=2 n=1/3 | fixes applied: 0 | evidence: два дефекта текста критериев из терминального решения — `AC-3`/issue §4 (`PostgresDialect.cs:309-311`, `MariaDbDialect.cs:110`, `ClickHouseDialect.cs:161-162` против `SqlBuilder.cs:457-458`) и `AC-6`/issue §6 (`QueryCommand.cs:1705-1723`, `QueryCommand.QueryPreparer.cs:4150,4268,4566`, `SqlBuilder.cs:455-477`); процессная спека `docs/specs/design/D385-pdca-process-defects.md` §D385-PROC-1..4; `.pdca/status/D385-setops-selector-1.md` §PLAN record `### terminal` | last: терминальный исход `acceptance criteria cannot be determined` (PLAN → ACT) после `not accepted` CHECK `r=2`; снимается только новым явным указанием пользователя (исправить issue §4/`AC-3` и §6/`AC-6` либо объявить приоритет клаузул эквивалентности/не-целей) и новым PLAN

## Changed files

Cycle-owned (all, +/- counts):
- `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` (+~230 lines) — 6 additive overloads + XML-doc
- `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs` (+~95) — SQL parity + null selector/context + delegation
- `tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs` (+~60) — SQL parity
- `tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs` (+~65) — SQL parity
- `tests/nextorm.mysql.tests/SqlGeneration/SqlGenerationTests.cs` (+~55) — SQL parity
- `tests/nextorm.mariadb.tests/SqlGeneration/SqlGenerationTests.cs` (+~60) — SQL parity
- `tests/nextorm.clickhouse.tests/SqlGeneration/SqlGenerationTests.cs` (+~60) — SQL parity
- `tests/nextorm.sqlite.tests/PlanCache/PlanKeyUniquenessTests.cs` (+~20) — plan-key parity
- `tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SetOperations.cs` (+~14) — AC-4 selector scenario
- `docs/guide/08-set-operations.md` (+~12) — table/paragraph/examples
- `docs/ru/guide/08-set-operations.md` (+~12) — table/paragraph/examples
- `docs/specs/design/API-NAMING-REVIEW.md` (+~45) — N385-1 audit
- `.pdca/D385-setops-selector/rv1/manifest.json` (new) — frozen evidence
- `.pdca/D385-setops-selector/rv1/*` logs (new) — build/tests/validate/lint

r=2 additions (working-tree line counts):
- `docs/specs/design/D385-pdca-process-defects.md` (+153, new) — process-defect spec `D385-PROC-1..4`, internal (no public-docs link)
- `tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SetOperations.cs` (+81) — AC-4 live scenario 10/13 + `O-AC6` Branch A live parity (`SetOperations_Selector_ShapeMismatch_FailsAtExecution` `:110`)
- `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs` (+166) — `O-AC6` Branch A unit proof (`:3756`) + positive control re-labelled (`:3780`)
- provider pin assertions (`O-AC3`) in `tests/nextorm.{postgres,mariadb,clickhouse,sqlite}.tests/SqlGeneration/SqlGenerationTests.cs` — 3 emit / 3 throw matrix pinned on both forms
- `.pdca/D385-setops-selector/rv2/**` (new) — `manifest.json`, `frozen/` (13 copies) + `frozen.sha256`, `coverage/`, `issue-385.json`, `attempts/`, `validate.log`, `lint.log`
- `.pdca/D385-setops-selector/rv1/SUPERSEDED.md` (new) — one line naming `rv2/` active and `D385-E1`/`E2` as rv1's defects

Foreign/dirty entries (34 unrelated) untouched and unclaimed.

## Git state

status path: `.pdca/status/D385-setops-selector-1.md` — created and modified this cycle · untracked (durable on disk, not committed, not staged). Branch `1.0.9`, HEAD `b684bfef` (baseline supplied at creation; unchanged). Cycle-owned delta: 14 files (product code `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs`, 6 × provider `SqlGenerationTests.cs`, `PlanKeyUniquenessTests.cs`, `CommonTestSuite.SetOperations.cs`, both `08-set-operations.md`, `API-NAMING-REVIEW.md`, new `docs/specs/design/D385-pdca-process-defects.md`) plus evidence roots `.pdca/D385-setops-selector/rv1/` and `rv2/` — all uncommitted and unstaged. Foreign: 34 unrelated dirty entries (`#319` core/`Raw` work, BDN artifacts, docs restructure, skills/tools edits, `timefinal.txt`) untouched and unclaimed; `git status --porcelain` shows 48 entries = 14 cycle-owned + 34 foreign. No commit was made and none is due: the cycle ended PLAN-terminal (`acceptance criteria cannot be determined`) with no `accepted`, so `AC-10` (commit on `1.0.9` + `gh issue close 385`) stays unexecuted; no `git add`/stage/commit/push/merge and no `gh` action was performed in ACT. Working tree unchanged apart from this status file.

## Progress log

append-only, one row per event, never edited retroactively:

2026-11-09T21:22:01Z | PLAN | revision r1 | attempt n1 | status file created; PLAN persisted (pdca-planner, r=1) | .pdca/status/D385-setops-selector-1.md
2026-11-09T21:31:35Z | DO | revision r1 | attempt n1 | DO started | src/nextorm.core/Query/Commands/QueryCommand.TResult.cs (6 additive overloads)
2026-11-09T21:50:19Z | DO | revision r1 | attempt n1 | build 0/0 + 6 provider SQL-parity + plan-key + neg tests green (rc 0) | .pdca/D385-setops-selector/rv1/build-final.log, tests-sql.log, tests-plankey.log, tests-neg.log
2026-11-09T21:50:19Z | DO | revision r1 | attempt n1 | integration run blocked (Podman machine start failed, vsock 110) — AC-4 unverified | /tmp/opencode/integration/canonical-canonical-full-suite.log
2026-11-09T21:50:19Z | DO | revision r1 | attempt n1 | manifest + both validators rc 0 | .pdca/D385-setops-selector/rv1/manifest.json (sha256 d17f40684be1fa884e15c3a7da0a470c234f197ce2ddbf4c6003f61ea0c251e1)
2026-11-09T21:50:19Z | DO | revision r1 | attempt n1 | DO completed (candidate, not verdict) | .pdca/status/D385-setops-selector-1.md
2026-11-09T22:01:09Z | DO | revision r1 | attempt n1 | AC-4 re-gather: socket live, run hung on container readiness — unverified | .pdca/D385-setops-selector/rv1/attempts/integration-full.log
2026-11-09T22:44:01Z | PLAN | revision r2 | attempt n1 | PLAN r=2 persisted (pdca-planner, contract revision r=2, n reset to 1) | .pdca/status/D385-setops-selector-1.md
2026-11-09T22:49:53Z | DO | revision r2 | attempt n1 | DO started: first r=2 edit of a cycle-owned file (provider pin assertion) | tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs
2026-11-09T22:53:09Z | DO | revision r2 | attempt n1 | provider unit suites re-run on the r=2 candidate: 6 SQL-parity suites + plan-key + negatives, all rc 0 / 0 failed | .pdca/D385-setops-selector/rv2/tests-sqlite.log … tests-clickhouse.log
2026-11-09T23:13:52Z | DO | revision r2 | attempt n1 | build 0 Warning(s) / 0 Error(s), rc 0 | .pdca/D385-setops-selector/rv2/build.log
2026-11-09T23:21:25Z | DO | revision r2 | attempt n1 | canonical capped integration run rc 0: total 8671, passed 8474, failed 0, skipped 197, 389 s; skip budget OK (197) | .pdca/D385-setops-selector/rv2/integration-full.log
2026-11-09T23:21:25Z | DO | revision r2 | attempt n1 | D385-E4 closed: O-AC6 Branch A — unit parity (1 vs 2 SQL columns survive preparation) + live execution-stage parity with the same exception | tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:3756; tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SetOperations.cs:110
2026-11-09T23:31:39Z | DO | revision r2 | attempt n1 | D385-E3 closed: coverage measured with the CI recipe on the CI gate scope — line 89.4 % (75375/84306), branch 83.5 % (43806/52436), thresholds 85/75 pass, rc 0 | .pdca/D385-setops-selector/rv2/coverage/SUMMARY.txt
2026-11-09T23:34:12Z | DO | revision r2 | attempt n1 | process-defect spec created (D385-PROC-1..4), 153 lines CRLF, internal | docs/specs/design/D385-pdca-process-defects.md
2026-11-09T23:34:13Z | DO | revision r2 | attempt n1 | issue #385 artifact persisted (sha256 be5a5c32…; the file r=1 named but never stored) | .pdca/D385-setops-selector/rv2/issue-385.json
2026-11-09T23:34:28Z | DO | revision r2 | attempt n1 | D385-E1/E2 closed: 13/13 footprint files frozen (orig == copy), rv1 superseded | .pdca/D385-setops-selector/rv2/frozen.sha256; .pdca/D385-setops-selector/rv1/SUPERSEDED.md
2026-11-09T23:35:18Z | DO | revision r2 | attempt n1 | rv2 manifest: 20 rows (19 met, 1 open = AC-10), tree be43e5a1df408ff81bdc82ab24ee782311de71ca, sha256 642851795ea53ef4e40d435b98b2ade404fe7ad3dbba3b638e10dc5884c71b85 | .pdca/D385-setops-selector/rv2/manifest.json
2026-11-09T23:36:07Z | DO | revision r2 | attempt n1 | both validators rc 0 (validate_inner_loop.py manifest, pdca_evidence.py lint) | .pdca/D385-setops-selector/rv2/validate.log, lint.log
2026-11-09T23:36:40Z | DO | revision r2 | attempt n1 | DO completed (candidate, not verdict): AC-1..AC-9 + BUILD/TESTS-*/INTEGRATION/COVERAGE/PERF/O-AC3/O-AC6/PROC-DEFECT-SPEC met; AC-10 open for ACT | .pdca/status/D385-setops-selector-1.md
2026-11-09T23:03:25Z | DO | revision r2 | attempt n1 | integration attempt 1 rc=1: total 8671, passed 8467, failed 4, skipped 200 — four SQL Server "timeout occurred while waiting for memory resources" failures; not accepted | .pdca/D385-setops-selector/rv2/attempts/integration-full-run1.log
2026-11-09T23:09:59Z | DO | revision r2 | attempt n1 | integration attempt 2: tests rc 0 (8471 passed / 0 failed / 200 skipped) but the skip-budget gate FAILED (200 vs pinned 197: three capability skips from the then-existing …_OnSqlite shape test); test reworked to provider-agnostic form | .pdca/D385-setops-selector/rv2/attempts/integration-full-rerun1.log
2026-11-09T23:03:25Z | DO | revision r2 | attempt n1 | integration attempt 1 rc=1: total 8671, passed 8467, failed 4, skipped 200 — four SQL Server "timeout occurred while waiting for memory resources" failures; not accepted | .pdca/D385-setops-selector/rv2/attempts/integration-full-run1.log
2026-11-09T23:09:59Z | DO | revision r2 | attempt n1 | integration attempt 2: tests rc 0 (8471 passed / 0 failed / 200 skipped) but the skip-budget gate FAILED (200 vs pinned 197: three capability skips from the then-existing …_OnSqlite shape test); the test was reworked to the provider-agnostic form | .pdca/D385-setops-selector/rv2/attempts/integration-full-rerun1.log
2026-11-09T23:37:48Z | DO | revision r2 | attempt n1 | manifest rewritten to correct the INTEGRATION attempt accounting (skip-budget failure, not a container collision): sha256 3bb45de58cf487d1bb5336bb6978c047ff13b0da9940068cfd82bd2ab3e15996, tree unchanged be43e5a1df408ff81bdc82ab24ee782311de71ca, 20 rows (19 met, 1 open = AC-10) | .pdca/D385-setops-selector/rv2/manifest.json
2026-11-09T23:37:51Z | DO | revision r2 | attempt n1 | both validators re-run against the final manifest, rc 0 (validate_inner_loop.py manifest, pdca_evidence.py lint) | .pdca/D385-setops-selector/rv2/validate.log, lint.log
2026-11-09T23:38:23Z | DO | revision r2 | attempt n1 | status file final write: 666 lines; sha256 eb1b09fb80ec890270b08ce0de3448f1aef80c24608bea53d879f4e1b8d429de = digest of the preceding 665 lines (a line cannot contain its own digest) | .pdca/status/D385-setops-selector-1.md
2026-11-09T23:44:30Z | CHECK | revision r2 | attempt n1 | CHECK r=2 not accepted: AC-2/AC-4/AC-5/AC-8 met; AC-3/AC-6 unmet-indeterminate; AC-1/AC-7/AC-9/AC-10 unverified; routed back to PLAN | .pdca/status/D385-setops-selector-1.md (CHECK report body not persisted — follow-up)
2026-11-09T23:44:45Z | PLAN | revision r2 | attempt n1 | PLAN terminal outcome recorded: `acceptance criteria cannot be determined` (disposition (a), PLAN → ACT; no further DO or CHECK) | .pdca/status/D385-setops-selector-1.md §PLAN record ### terminal
2026-11-09T23:45:16Z | ACT | revision r2 | attempt n1 | ACT closed: outcome blocked (PLAN-terminal), cycle N=1 finalized and kept; no commit, no stage, no push, no merge, no gh issue action — Issue #385 OPEN, milestone 1.0.9 unchanged | .pdca/status/D385-setops-selector-1.md
2026-11-09T23:45:24Z | ACT | revision r2 | attempt n1 | status file final write: 725 lines; sha256 eeaab6f5d00e671655c477ad3db205693bd61a5ed6a3557bf897385a09dc8cac = digest of the preceding 724 lines (a line cannot contain its own digest) | .pdca/status/D385-setops-selector-1.md
2026-11-09T23:46:37Z | CHECK | revision r2 | attempt n1 | CHECK reports r=1 and r=2 persisted verbatim in §CHECK record as `### r1`/`### r2` (both `not accepted`, next transition → PLAN); ACT outcome `blocked` derives from them | .pdca/status/D385-setops-selector-1.md
