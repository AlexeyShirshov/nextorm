# Провайдер-специфичные extension-методы вместо членов EntityBuilder

- **Дата:** 2026-10-05
- **Статус (явно):**
  - **conversational design — APPROVED**: обсуждавшийся в чате архитектурный дизайн одобрен пользователем 2026-10-05.
  - **written spec — AWAITING USER REVIEW**: эта письменная спецификация ещё не утверждена пользователем.
  - **implementation plan — NOT WRITTEN / NOT APPROVED**: детальный план реализации не создан и не одобрен.
  - **implementation — NOT AUTHORIZED**: product-код, тесты, конфиги не редактируются; сборки, установка зависимостей, коммиты, push и merge не авторизованы.
  - Одобрение дизайна в чате **не** является одобрением спецификации; спецификация **не** является разрешением на реализацию.
- **Тип документа:** дизайн-спека (записанные решения), не детальный план реализации.
- **Трекинг:** GitHub issue [#191](https://github.com/AlexeyShirshov/nextorm/issues/191) — «Вынести провайдер-специфичные методы и их реализацию из EntityBuilder в extensions», статус OPEN; milestone **1.0.9-rc2** ([milestone/20](https://github.com/AlexeyShirshov/nextorm/milestone/20)). Issue и milestone проверены (VERIFIED) через `gh issue view` / `gh api`. Связанный (не переоткрывается и не изменяется) issue [#122](https://github.com/AlexeyShirshov/nextorm/issues/122) — CLOSED.
- **Локальный путь этой спеки:** `docs/superpowers/specs/2026-10-05-provider-specific-extensions-design.md` (файл в этом репозитории, ещё не закоммичен; в рамках этой задачи создаётся только он).
- **Доказательная база:** ссылки на код в §§2 и 11 — навигационные ориентиры (line pointers) на состояние до рефакторинга, а не воспроизведённые результаты. В этой задаче никакие сборки/тесты/бенчмарки не запускались и не заявляются.

## 1. Назначение

Общий API `EntityBuilder` не должен содержать провайдер-специфичные операции — **даже** если они остаются лишь внутренними forwarding-целями, за которыми стоит публичное extension-переоткрытие. Текущее состояние после #122 оставляет реализацию провайдер-специфичных операций в общем ядре: ClickHouse-расширения — это тонкие делегаты к `internal` instance-методам `EntityBuilder`/`JoinedEntityBuilder`/`FromOptions`/`JoinOptions`, а PostgreSQL `DistinctOn` и SQL Server `Pivot`/`Unpivot` и сегодня являются публичными instance-методами общего builder'а. Это засоряет общий API, удерживает провайдерную логику в core и не даёт независимо развивать провайдерные операции.

Цель — **вынести из общего builder'а и публичные методы, и их реализацию** в extension-методы провайдерных сборок, **без** введения провайдерной иерархии builder'ов (никаких `ClickHouseEntityBuilder : EntityBuilder`, никаких провайдерных `new`-override'ов). Многопровайдерные операции остаются общими; для провайдера-native синтаксиса не требуется поддержка в каждом провайдере — достаточно supported/emulated-разбиения на уровне диалекта.

Границы явно ограничены инвентарём §2: это **не** сплошной редизайн диалектов, AST, `QueryCommand`, query planner или всех hint API.

## 2. Область (operation inventory)

### 2.1. ClickHouse (58 существующих публичных extension-методов)

Инвентарь из #122 сохраняется в том же объёме; меняется только носитель: тела переезжают в провайдерные extension-классы, а core-объявления и реализация удаляются.

- **16 generic `EntityBuilder`-операций** (сейчас internal instance-объявления): `Final`, `Settings`, `PreWhere`, `ArrayJoin`, `LeftArrayJoin`, `ArrayJoinElement`, `LeftArrayJoinElement`, `LimitBy` (две перегрузки), `WithTotals`, `SemiJoin` (две), `AntiJoin` (две), `PasteJoin` (две).
- **6 non-generic `EntityBuilder`-операций:** `SemiJoin`, `AntiJoin`, `PasteJoin` (по две перегрузки, включая `QueryCommand`-формы).
- **32 метода `JoinedEntityBuilder`:** аритность 2..7 — `SemiJoin`/`AntiJoin`/`PasteJoin`/`ArrayJoin`/`LeftArrayJoin`; аритность 8 — только `ArrayJoin`/`LeftArrayJoin`.
- **4 option-метода:** `FromOptions.Sample` (две перегрузки), `JoinOptions.Global` и `JoinOptions.WithStrictness`.

Существующие сигнатуры, namespace (`NextORM.ClickHouse`) и имена static-host-классов **не меняются**. Все существующие ClickHouse option-расширения (`FromOptions.Sample`×2, `JoinOptions.Global`/`WithStrictness`) остаются ровно на `ClickHouseEntityBuilderExtensions`, а joined-методы — на `ClickHouseJoinedEntityBuilderExtensions`; никакого разделения/переименования static-host'ов в этой задаче нет. Это в том числе `QueryCommand`-перегрузки `SemiJoin`/`AntiJoin`/`PasteJoin`, обе формы `Sample` и TableAlias-режим non-generic.

### 2.2. PostgreSQL

`DistinctOn<TResult>(Expression<Func<TEntity, TResult>>)` переносится из публичного instance-метода общего `EntityBuilder<TEntity>` в `NextORM.Postgres.PostgresEntityBuilderExtensions` (`src/nextorm.postgres/Extensions/PostgresEntityBuilderExtensions.cs`). Сохраняются тот же ресивер, вывод generic-параметра и возврат `EntityBuilder<TEntity>`.

### 2.3. SQL Server

`Pivot(PivotAggregate, Expression<Func<TEntity, object?>>, Expression<Func<TEntity, object?>>, params PivotValue[])` и `Unpivot(string, string, params UnpivotColumn[])` переносятся в `NextORM.SqlServer.SqlServerEntityBuilderExtensions` (`src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs`). Возврат `EntityBuilder<TableAlias>` сохраняется без изменений.

### 2.4. Что остаётся общим / вне области

- Остаются общими многопровайдерные (supported/emulated) операции: `ForUpdate`/`ForShare` (PG/SQLServer/MySQL/MariaDB), `ForSystemTime` (SQLServer/MariaDB), `WithTies` (PG/SQLServer), `Rollup`/`Cube`/`GroupingSets` и прочие действительно общие операции.
- Существующие generic hint-пути **не** затягиваются в этот рефакторинг.
- Прочие провайдер-only option-API, кроме четырёх ClickHouse option-методов из §2.1, — **OUT OF SCOPE**.
- Диалекты, AST как система, `QueryCommand`, query planner, общая механика hint'ов — не переписываются.

## 3. Архитектура

### 3.1. Удаление из общего builder'а

- Из generic и non-generic `EntityBuilder`, из `JoinedEntityBuilder` всех аритностей и из четырёх ClickHouse option-объявлений удаляются провайдер-специфичные instance-объявления **вместе с телами**.
- Запрещены:
  - internal stub/forwarding-тела под исходными именами операций;
  - `Obsolete`-заглушки совместимости;
  - переименованные провайдер-only хелперы внутри core;
  - static core-утилита, которая просто принимает старые тела провайдерных методов.
- Provider-only private-логика мигрирует в private-хелперы соответствующих провайдерных extensions: `AddArrayJoin`, `ToArrayJoinElement`, `ResolvePivotInner`, `AddSemiAntiJoin` (generic) и `AddSemiAntiJoin` (non-generic).

### 3.2. Что остаётся в core

- Типизированное AST/state: `DistinctOnClause`, `PivotExpression`, `PivotValue`, `UnpivotColumn`, `ArrayJoinProjection` — остаются в core.
- Dialect capabilities, rendering и cache equality/hashing — остаются в core.
- Поля/свойства состояния, представляющие модификаторы запроса, **могут** остаться в builder'ах/ядре: правило запрещает реализации операций, а не типизированное представление состояния.
- Перемещённые реализации читают и пишут существующие **internal typed get/set properties** на клонированных builder'ах: `ArrayJoins`, `ArrayJoinKind`, `SourceEntityType`, `BindArrayJoinElement`, `SettingsList`, `PreWhereCondition`, `DistinctOnClause`, `SourceFrom`. Это прямой доступ к типизированному состоянию и существующим copy-механизмам, **не** state bags и **не** новые аллокации.
- Private fields публично не раскрываются; operation-specific core-методы для воссоздания старых тел не добавляются. Для отсутствующего validation read-state (например, `Having`) допускается минимальный internal getter; нейтральные copy/validation-хелперы, общие для общих операций, могут получить internal-видимость — как уже одобрено в §3.4. В CH-опциях меняются только необходимые setter'ы `private` → `internal` (§3.3).
- Запрещены: `Dictionary<string, object>`, `dynamic`/boxing, delegate dispatch, provider wrapper instance, новая per-builder state bag allocation, новая иерархия builder'ов.

### 3.3. Доступность и IVT

- Добавляется `InternalsVisibleTo` для `nextorm.postgres` и `nextorm.sqlserver`; существующий IVT для `nextorm.clickhouse` сохраняется.
- Внутреннее состояние builder'а в основном уже реализовано через get/set. Для ClickHouse `FromOptions`/`JoinOptions` setter'ы **только тех** полей, которые нужны мигрированным методам, переводятся `private` → `internal`; общая семантика опций и возврат того же объекта сохраняются. Публичная мутабельность не расширяется.

### 3.4. Расширение нейтральных хелперов

До `internal` (и не шире) точечно расширяются только необходимые нейтральные хелперы:

- `CopyProjectionIndependentStateTo`;
- shared `EnsureNoEagerLoadState`;
- source/state access (read-доступ к `Having`, если он нужен для array-join guard);
- `GetJoinSource` и shared overload'ы `JoinCore`/эквиваленты аритности.

Это общие пути композиции запросов, **не** провайдер-операционные internal-обёртки. Дублировать shared `JoinCore`/`CreateJoined` в провайдерах запрещено; extension `PasteJoin` передаёт `JoinType.Paste` в существующую общую join-машину.

Отдельно различается non-generic `EntityBuilder : ICloneable` — он **не** является подклассом `EntityBuilder<TableAlias>`; его существующее разрешение источника (source resolution) и копирование (copy) сохраняются. Произвольные private-члены глобально не расширяются.

### 3.5. Клонирование и мутация

- Extension-модификаторы клонируют существующий builder **ровно один раз** — там, где оригинальный метод клонировал, — и затем мутируют только клон через типизированное состояние.
- Сохраняются существующие копии массивов и паттерны аллокации expression-записей.
- Новые projection-builder'ы создаются один раз, как и прежде.
- Фактический runtime joined type и fluent return type обеспечиваются существующим virtual cloning — без новых override'ов и иерархии.
- Для option-методов сохраняется исходная in-place семантика.

## 4. Семантика, ошибки, кэш

### 4.1. Сохраняемая семантика

- Порядок операций и композиция; повторный `PreWhere` — AND-композиция с заменой параметров.
- `Settings` — merge и порядок.
- Отклонение смешанных видов `ArrayJoin`, left-семантика пустого массива.
- Ограничения `ArrayJoinElement` + source binding + копирование независимого состояния (copied independent state).
- Взаимоисключение `Distinct`/`DistinctOn`.
- `Pivot`/`Unpivot` — конструирование, alias, а также modifier/eager-load validation.
- `Semi`/`Anti` — проверки cardinality, предиката и query-source; `Paste` — restrictions и аритность.

### 4.2. Кэш

- Shared `Clone`/`CopyTo`/query command state transfer, cache key equality/hash и prepare behavior остаются без изменений.
- Провайдерные узлы продолжают участвовать в кэше.
- Не трогаются shared sticky cache flags `Any`/`Count`/`All`; cache bypass не вводится.

### 4.3. Валидация и ошибки

- Capability validation остаётся на существующей стадии построения SQL, **даже если** extension вызван на контексте неподходящего провайдера.
- Импорт provider-namespace не является compile-time проверкой провайдерного контекста.
- Существующие типы исключений, порядок валидации и сообщения сохраняются, кроме случая, когда само удаление метода неизбежно даёт compile-ошибку (это документируется как часть break'а).

## 5. Совместимость

- Потребителю, который раньше ссылался только на core, нужно добавить `using NextORM.Postgres` / `using NextORM.SqlServer` и ссылку на соответствующую провайдерную сборку.
- Source-совместимость ClickHouse-поверхности не меняется (namespace, сигнатуры, имена host-классов прежние).
- Удаление публичных instance-методов PG `DistinctOn` и SQLServer `Pivot`/`Unpivot` из общего builder'а — **binary breaking** для этих двух провайдеров: потребитель обязан пересобраться, добавить `using NextORM.Postgres`/`using NextORM.SqlServer` и ссылку на провайдерную сборку. Для ClickHouse внешние публичные extension-методы **не меняются**; удаление внутренних instance-методов само по себе не является новым публичным binary break'ом CH (их и так не было в публичной поверхности после #122). `Obsolete`/compatibility-заглушек не создаётся ни в `EntityBuilder`, ни в провайдерах.
- Публичные AST/helper-типы остаются в core; никаких несвязанных перемещений/переименований типов и никакого нового public generic builder API.

## 6. Тесты и доказательства (требования, не результаты)

Ничего из этого в данной задаче не запускалось.

- **Компиляция:** существующие SQL-generation наборы core и провайдеров собираются после правки импортов/ссылок.
- **Non-IVT consumer:** non-IVT тесты (текущий `tests/nextorm.clickhouse.extensions.tests` и новые PG/SQLServer-эквиваленты **либо** один чётко изолированный extension-consumer-проект без IVT) проверяют как точки входа все перемещённые overload-группы и fluent-ресиверы, включая ClickHouse аритности 2..8 и non-generic, а также статическую привязку к реальным методам extension-host-классов. Эти тестовые сборки остаются non-friend (`InternalsVisibleTo` к ним не добавляется) постоянно.
- **API-shape:** reflection/semantic assertions ограничивают проверку отсутствия **строго перечисленными** мигрированными **operation**-instance-методами и перечисленными provider-only helper-методами, а **не** всеми instance-методами. Разрешены: property getter/setter accessors для сохраняемого типизированного состояния, конструкторы, shared нейтральные хелперы и общие публичные операции. Reflection использует `BindingFlags.Instance | Public | NonPublic | DeclaredOnly`, скоупится по именам/сигнатурам/generic-арити и пропускает `IsSpecialName` property-accessors; проверяются все затронутые объявления классов — generic/non-generic/joined/options. Это снимает неоднозначность с сохранением типизированного состояния и private→internal option-setter'ами. Отдельно проверяется, что общие `ForUpdate` и т.п. доступны без импорта чужих провайдерных namespace.
- **Branching/clone:** от общего origin строятся независимые модифицированные запросы, проверяется отсутствие протечки состояния; повторные модификаторы; propagation projection/join state; невалидные формы и ошибки; вызов на неподдерживаемом диалекте по-прежнему падает так же, как раньше.
- **SQL и кэш** для каждой перемещённой фичи: вывод byte-for-byte там, где исходный SQL стабилен; одинаковая структурная работа план-кэша; различные modifier plans остаются различными; неизменённые запросы по-прежнему cache-hit.
- **Команды:** `dotnet build nextorm.slnx -c Debug`; unit/dialect/extension тесты по конвенциям репозитория (`dotnet test`/MTP); `dotnet docfx docs/docfx.json`.
- **Integration:** suite реально исполняет затронутые ClickHouse/PostgreSQL/SQLServer-провайдеры и покрывает регрессии общих операций. Перед запуском прочитать `.opencode/skills/running-integration-tests/SKILL.md`. Настройка `DOCKER_HOST` и восстановление Podman — по AGENTS; **skipped providers — не passing**, и любой недоступный провайдер фиксируется как blocker явно. Предпочтителен полный существующий cross-provider suite; выдуманные результаты недопустимы.

## 7. Перф-гейт

- Измерений для этого вынесения пока нет; **гарантия нулевой стоимости не заявляется**.
- **Baseline** снимается с **текущего dirty worktree**, включая несвязанные изменения #183; фиксируются `git HEAD`/status, хэш релевантного diff, toolchain, машина и BDN-настройки. Использовать HEAD-only baseline или прежний baseline #183 для смешивания изменений нельзя. Захват baseline для реализации обязан произойти **до** product-правок.
- Сравнение before/after на одной машине, одном рантайме и одной конфигурации, в одинаковых сценариях: zero/single/chained modifier (CH `Final`+`PreWhere`+`Settings`, `ArrayJoin`, joined-вариант; PG `DistinctOn`; SQLServer `Pivot`/`Unpivot`) и существующие общие `Construct_Only`/cache-сценарии. Для микробенчмарков достаточно placeholder-подключений, строящих SQL; сетевого I/O в тайминге конструирования нет.
- **Allocated bytes на логическую операцию** не должны расти при эквивалентных вызовах; не допускается лишний клон/обёртка/state-объект. Фиксируются нормализация логических операций и счётчики GC.
- В отчёт идут **сырые BDN-артефакты и команды**, а не оценки.
- **Timing-правило:** если after-среднее медленнее before при **non-overlapping** BDN confidence intervals — это suspected regression, требующая независимого повторного прогона той же before/after пары. Если подтверждённое значимое замедление повторяется — гейт **fails**, и до accept нужно investigate/revise. Перекрытие интервалов — inconclusive, а не доказательство нулевого overhead; шумные результаты нельзя называть performance parity. Абсолютный процентный порог **не** изобретается.
- Существующие cached-path перф-гейты, где они вызываются, сохраняются; бюджеты/бенчмарки #183 не переписываются.

## 8. Документация

- EN/RU страницы: `docs/guide/07-distinct.md`, `docs/guide/03-grouping-and-aggregates.md`, `docs/guide/provider-specific/postgresql.md`, `docs/guide/provider-specific/sqlserver.md`, `docs/advanced/api-reference.md` и их RU-зеркала — обновляют `using`/импорты и DocFX xref'ы на extension-host-классы.
- MSSQL AdventureWorks sample: компиляция + формулировки в `examples/**`/`readme`/`VERIFICATION`.
- Вводящие в заблуждение комментарии про CH-forwarding в `.csproj`/test-summaries обновляются.
- К историческим #122-документам **дописывается** новый фактический audit-статус, а не переписывается история так, будто прежнего вынесения не было.
- Все затронутые старые xref'ы находятся ignore-aware поиском по докам на этапе реализации; публичные доки нигде не ссылаются на внутренние `docs/specs/**` или на этот design-артефакт.
- Migration/break guidance для удаления PG `DistinctOn` и SQLServer `Pivot`/`Unpivot` (и обновлённых `using`/provider reference) фиксируется в GitHub issue #191 (milestone 1.0.9-rc2) и на уже перечисленных существующих EN/RU-страницах (`docs/guide/07-distinct.md`, `docs/guide/03-grouping-and-aggregates.md`, `docs/guide/provider-specific/sqlserver.md`, `docs/advanced/api-reference.md` + RU-зеркала). Отдельный артефакт release-notes в этой задаче **не** создаётся; если release-notes страница уже существует, она может сослаться на эту миграцию, но это не обязательный новый артефакт scope.

## 9. Рабочее дерево и изоляция

- В репозитории есть dirty cached-path-работа #183 (код/доки/тесты/артефакты). Она **сохраняется**: без `reset`/`cleanup`/`stash`/commit/push/merge.
- Будущие product-правки ограничиваются только scoped-файлами, а baseline diff захватывается заранее.
- Интеграция через worktree/патч может быть выбрана позже; способ исполнения сейчас **не выбирается**.
- Создание этой спеки затрагивает **только** этот новый markdown-файл; тело GitHub issue — внешняя авторизованная запись.

## 10. Acceptance checklist

- [ ] Все перечисленные в §2 провайдер-специфичные **операции и их реализации** отсутствуют в общем core (`EntityBuilder`/`JoinedEntityBuilder`/options).
- [ ] Статический provider API имеет корректные, неизменённые сигнатуры и host-классы.
- [ ] Многопровайдерные общие методы (`ForUpdate` и др.) сохранены и доступны без провайдерных импортов.
- [ ] SQL-generation и семантика сохранены (byte-for-byte, где стабильно).
- [ ] Cache/plan-key поведение сохранено.
- [ ] Реальные integration-прогоны затронутых провайдеров выполнены (skipped ≠ passing).
- [ ] Перф-гейт §7 пройден с сырыми BDN-артефактами.
- [ ] EN/RU доки и xref'ы обновлены; публичные доки не ссылаются на specs.
- [ ] Migration/break guidance для PG `DistinctOn` и SQLServer `Pivot`/`Unpivot` записана в #191 (milestone 1.0.9-rc2) и на существующих EN/RU DistinctOn/SQLServer guide/API-reference страницах; обязательного нового release-notes артефакта нет.
- [ ] Неразрешённых дизайн-вопросов нет.

Одобрение этой спеки и отдельное одобрение письменного плана/способа исполнения остаются впереди; спецификация **не** даёт разрешения на реализацию.

## 11. Указатели доказательств (состояние до рефакторинга)

Это reference-факты (line pointers), а не переименованные символы.

- `src/nextorm.core/Builders/EntityBuilder.cs`: `:1638-1786` — CH-модификаторы; `:1816` — `DistinctOn`; `:2010-2077` — `Pivot`/`Unpivot`; `:2207`/`:2216`/`:2238`/`:2362` — `Clone`/copy; `:2565-2627` — joins; `:3298` — `WithTotals`; `:3661` — non-generic тип.
- `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs`: аритность 2 — `:13`/`:79`/`:115`/`:124`; аритность 8 — `:726`/`:743`.
- `src/nextorm.clickhouse/Extensions/ClickHouseEntityBuilderExtensions.cs` — 58 существующих методов.
- `src/nextorm.core/nextorm.core.csproj:69-72` — IVT-блок (комментарий + `nextorm.clickhouse`).
- `tests/nextorm.clickhouse.extensions.tests` — non-IVT consumer-проект.
- `src/nextorm.postgres/PostgresDialect.cs:365`; `src/nextorm.sqlserver/SqlServerDialect.cs:248` — dialect-ориентиры.
- `docs/specs/design/API-NAMING-REVIEW.md:5446` и `docs/specs/design/code-smells-review.md:8181` — аудиты прежнего вынесения #122.
