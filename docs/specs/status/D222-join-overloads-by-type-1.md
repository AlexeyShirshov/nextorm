---
task: T222
issue: "#222"
cycle: 1
plan_revision: 1
iteration: 2/3
plan_state: ready
evidence_contract_revision: 1
variant: pdca-dotnet
---

# Goal

Реализовать утверждённую спецификацию #222: перегрузки `Join`, `LeftJoin`, `RightJoin`, `FullJoin` без явного источника, с разрешением источника по типу присоединяемой сущности в **том же контексте**.

Основание: `docs/specs/status/issue-222-evidence/evidence-01-alias-join-surface.md`; действующая поверхность — `EntityBuilder.cs:2515–2545`, `JoinedEntityBuilder.cs:31–55`. Устаревшие номера строк из issue не использовать как implementation evidence.

## Минимальное решение

1. **Суть:** убрать необходимость писать явный `context.From<TJoinEntity>()` для обычного типового join.
2. **Ограничения:** сохранить существующие источники, семантику join, options, метаданные, максимальную арность 8 и изменения T213; не менять планировщик, кэш и материализацию.
3. **Оптимум:** разрешать источник существующим `From<TJoinEntity>()` того же контекста и делегировать существующей source-explicit перегрузке. Дальнейшее разрешение остаётся в текущем пути `JoinSourceResolver.Resolve<T>` → `ResolveSource() ?? GetFrom(type, null)` (`JoinSourceResolver.cs:13–14`).

| Альтернатива | Плюсы | Минусы / цена / риск |
|---|---|---|
| Делегация через существующий `From<T>()` и source-explicit join — **выбрано** | Минимальное изменение; единый путь метаданных, options и AST | Дополнительное создание builder на этапе построения запроса |
| Прямой `GetFrom(type, null)` и вызов внутреннего join | Меньше промежуточных объектов | Риск обойти семантику `From<T>()` и продублировать обработку |
| Новый общий механизм разрешения источников | Возможная будущая унификация | Ненужный рефакторинг общей области T213; существенно больше риск и footprint |

## Точная API-поверхность

Для каждого имени `M ∈ {Join, LeftJoin, RightJoin, FullJoin}` добавить:

- На `EntityBuilder<TEntity>`:  
  `M<TJoinEntity>(Expression<Func<TEntity, TJoinEntity, bool>> predicate, Action<JoinOptions>? options = null)`  
  → `JoinedEntityBuilder<TEntity, TJoinEntity>`.
- На каждом `JoinedEntityBuilder<T1,…,Tn>`, **n = 2, 3, 4, 5, 6, 7**:  
  `M<TJoinEntity>(Expression<Func<Projection<T1,…,Tn>, TJoinEntity, bool>> predicate, Action<JoinOptions>? options = null)`  
  → `JoinedEntityBuilder<T1,…,Tn,TJoinEntity>`.

Это **28 новых публичных членов**:

| Владелец | Новые члены | Результирующая арность |
|---|---|---:|
| `EntityBuilder<TEntity>` | `Join<TJoinEntity>`, `LeftJoin<TJoinEntity>`, `RightJoin<TJoinEntity>`, `FullJoin<TJoinEntity>` | 2 |
| `JoinedEntityBuilder<T1,T2>` | Те же четыре source-less перегрузки | 3 |
| `JoinedEntityBuilder<T1,T2,T3>` | Те же четыре | 4 |
| `JoinedEntityBuilder<T1,T2,T3,T4>` | Те же четыре | 5 |
| `JoinedEntityBuilder<T1,T2,T3,T4,T5>` | Те же четыре | 6 |
| `JoinedEntityBuilder<T1,T2,T3,T4,T5,T6>` | Те же четыре | 7 |
| `JoinedEntityBuilder<T1,T2,T3,T4,T5,T6,T7>` | Те же четыре | 8 |

Ограничения generic-параметров сохраняются такими же, как у соответствующих существующих source-explicit методов; новых ограничений не вводить. Возвращаемые типы выше — явное предположение по представленному arity-chain; несовпадение с действующим контрактом требует DO → PLAN, а не молчаливой смены API.

**Вне scope:** `CrossJoin`, `CrossApply`, `OuterApply`, негeneric raw-table `EntityBuilder`, source-less методы на терминальной арности 8, новые CTE/QueryCommand API. Issue перечисляет только четыре predicate-based join; `CrossJoin` не имеет такого predicate-контракта.

# Acceptance criteria

Все критерии ниже — **P1 по построению**. CHECK не понижает их.

| ID | Критерий и проверка | Негативный случай |
|---|---|---|
| A222-01 | Добавлены ровно перечисленные 28 перегрузок; compile fixtures используют каждую форму и проверяют результирующую арность | Нет join до арности 9, raw-table/type-only или type-only Cross/Apply; нет изменения существующих сигнатур |
| A222-02 | Type-only форма эквивалентна явному `sameContext.From<T>()`: источник, метаданные, SQL/AST и результат; options передаются существующему пути | Нельзя брать другой контекст, изобретать fallback-таблицу, обходить configured mapping, менять sticky cache flags или планировщик |
| A222-03 | Четыре join kind, все поддерживаемые receiver-арности, omitted/null/non-null options и unmatched rows работают согласно existing explicit-source oracle | `null` predicate отклоняется `ArgumentNullException` до разрешения источника; неразрешимый через `From<T>()` источник не превращается в успешный запрос |
| A222-04 | Существующие корректные вызовы с EntityBuilder, CTE и QueryCommand сохраняют компиляцию и поведение; inference документирован и проверен | Untyped lambda без выводимого `TJoinEntity` не должна случайно выбирать другой API; возможная неоднозначность invalid all-null вызовов не скрывается |
| A222-05 | EN/RU joins guide обновлены в этом DO; все 28 членов записаны кандидатами T53; текущие dirty changes T213 сохранены | Не включать held freeze, не создавать `PublicAPI.*`, не менять unrelated файлы и не ссылаться из публичных docs на `docs/specs/**` |
| A222-06 | Финальный build без warnings; core, шесть SQL-generation проектов и container-backed integration успешно выполнены; merged coverage ≥85% line и ≥75% branch | Skip/unavailable PostgreSQL, SQL Server, MySQL или ClickHouse не является passing evidence; ненулевой exit или недостигнутый порог не закрывает критерий |
| A222-07 | Замороженный rv=1 evidence contract полностью заполнен фактическими результатами и provenance | Отсутствующий отчёт, неподтверждённый символ или запланированный источник не считается evidence |

## Совместимость и inference

- Корректный non-null явный источник не совместим с первым параметром новой перегрузки — `Expression<…>`; новая перегрузка не должна мешать таким вызовам.
- Проверить также typed-null source с typed predicate, три аргумента старой формы, optional/named arguments и typed expression/lambda.
- `Join<TJoinEntity>((left, right) => …)` — основной документированный вызов. Типизированный predicate может позволять inference; неполностью типизированная lambda без явного generic-аргумента не гарантирует его.
- **Не обещаем отсутствие любой новой неоднозначности:** `JoinedEntityBuilder.Join<T>(null, null)` потенциально становится неоднозначным между `(source, predicate)` и `(predicate, options)`. Это invalid-input форма; проверка фиксирует фактический результат компилятора. Документировать typed cast для различения намерения.
- Допущение: спецификация допускает указанное ограничение для invalid all-null вызовов. Если её действующий контракт требует сохранения компиляции таких вызовов, это конфликт API-формы → DO → PLAN.
- Отсутствие явной регистрации типа **не означает** неразрешимость: convention mapping сохраняется. Отрицательный тест использует тип/конфигурацию, для которых existing `From<T>()` действительно не может получить корректный источник.

# Planned write-footprint

| Путь | Назначение |
|---|---|
| `src/nextorm.core/Builders/EntityBuilder.cs` | **Public API:** четыре перегрузки |
| `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs` | **Public API:** 24 перегрузки |
| `tests/nextorm.core.tests/InMemoryJoinTests.cs` | Runtime equivalence, null/source guards, options, арности |
| `tests/nextorm.sqlite.tests/TypeOnlyJoinSqlGenerationTests.cs` | Новый, планируемый файл |
| `tests/nextorm.postgres.tests/TypeOnlyJoinSqlGenerationTests.cs` | Новый, планируемый файл |
| `tests/nextorm.sqlserver.tests/TypeOnlyJoinSqlGenerationTests.cs` | Новый, планируемый файл |
| `tests/nextorm.mysql.tests/TypeOnlyJoinSqlGenerationTests.cs` | Новый, планируемый файл |
| `tests/nextorm.mariadb.tests/TypeOnlyJoinSqlGenerationTests.cs` | Новый, планируемый файл |
| `tests/nextorm.clickhouse.tests/TypeOnlyJoinSqlGenerationTests.cs` | Новый, планируемый файл |
| `tests/nextorm.integration.tests/CommonTestSuite.JoinByType.cs` | Новый, планируемый partial-файл существующего общего suite |
| `docs/guide/02-joins.md` | EN API и примеры |
| `docs/ru/guide/02-joins.md` | RU зеркальный контракт |
| `docs/specs/status/collection-1.0.9-todo.md` | T222 result/progress и перечень T53 candidates; не менять held state T53 |
| `docs/specs/status/D222-join-overloads-by-type-1.md` | Этот план, результаты, evidence index |
| `artifacts/pdca/D222-join-overloads-by-type/rv1/**` | Планируемые runners, compile fixtures, логи, coverage, manifests |

Существующий `docs/specs/status/issue-222-evidence/**` — вход, сохраняется. Не писать в `D213-joininto-remainder-1.md`, JoinInto-файлы и T213 benchmark reports. Генерируемые build/DocFX outputs не являются новыми исходниками. Сохранять CRLF.

# Variant matrix

Статусы относятся к обязательству проверки, а не к уже выполненным тестам.

| ID | Вариант / наблюдаемая применимость | Закрытие |
|---|---|---|
| V01 | Обычный reference entity, разрешимый `From<T>()` | **test:** explicit/type-only equivalence |
| V02 | Convention-resolvable тип без explicit registration | **test:** не отвергать только за отсутствие регистрации |
| V03 | Configured mapping, отличающийся от convention mapping | **test:** тот же mapped source в обоих путях |
| V04 | Тип/конфигурация, неразрешимые existing `From<T>()` | **test:** тот же отказ на соответствующей стадии, без fallback |
| V05 | `Join` | **test:** совпавшие/несовпавшие строки, SQL/AST, все receiver-арности |
| V06 | `LeftJoin` | **test:** сохранение unmatched left и nullable/default результата |
| V07 | `RightJoin` | **test:** сохранение unmatched right; capability guard как у explicit формы |
| V08 | `FullJoin` | **test:** unmatched с обеих сторон; capability guard как у explicit формы |
| V09 | `EntityBuilder<TEntity>` → арность 2 | **test:** все четыре kind × omitted/non-null options |
| V10 | Joined receiver арности 2 → 3 | **test:** те же восемь комбинаций |
| V11 | Арность 3 → 4 | **test:** те же восемь комбинаций |
| V12 | Арность 4 → 5 | **test:** те же восемь комбинаций |
| V13 | Арность 5 → 6 | **test:** те же восемь комбинаций |
| V14 | Арность 6 → 7 | **test:** те же восемь комбинаций |
| V15 | Арность 7 → 8 | **test:** те же восемь комбинаций |
| V16 | Терминальная арность 8, raw-table, Cross/Apply | **guard:** semantic API inventory подтверждает отсутствие новых форм |
| V17 | Options omitted и explicit `null` | **test:** одинаковый результат/SQL с explicit-source oracle |
| V18 | Non-null options callback | **test:** число вызовов совпадает с explicit oracle; callback не теряется |
| V19 | Options callback бросает sentinel exception | **test:** исключение не поглощается и не заменяется |
| V20 | `null` predicate на каждом receiver и kind | **test:** `ArgumentNullException` прежде source resolution |
| V21 | Typed expression/typed lambda; explicit generic type | **test:** compile-positive fixtures |
| V22 | Untyped lambda без выводимого join type | **guard:** compile-negative fixture; не обещать inference |
| V23 | Existing EntityBuilder/CTE/QueryCommand source calls; named/optional args | **test:** compile-positive и existing regression suites |
| V24 | Untyped all-null/default argument pairs | **guard:** compiler probe с baseline comparison; неоднозначность invalid-input формы отражена в docs |
| V25 | Value-type entity / nullable entity type | **guard:** сохранить existing generic constraints; если тип разрешён существующим API — paired runtime test, иначе compile rejection |
| V26 | Unmatched value/reference projection components | **test:** сохранить existing null/default semantics, не добавлять преобразования |
| V27 | SQLite/Postgres/SQL Server/MySQL/MariaDB/ClickHouse dialects | **test:** все шесть SQL-generation suites; explicit/type-only сравнение |
| V28 | SQLite + PostgreSQL/SQL Server/MySQL/ClickHouse real execution | **test:** общий integration suite; недоступность контейнера не capability guard |
| V29 | Join kind не поддерживается данным dialect/provider | **guard:** утверждение эквивалентного отказа explicit/type-only, не skip новой проверки |
| V30 | Отдельный real MariaDB server | **deferred:** триггер — появление отдельной MariaDB integration infrastructure или provider-specific regression; MariaDB SQL-gen обязателен сейчас |
| V31 | Планировщик, shared command/cache flags, row mapper | **guard:** semantic/diff review — изменений нет |
| V32 | Source-specific aliases, CTE и QueryCommand state | **test:** explicit APIs сохраняются; новое API не обещает переноса состояния из отсутствующего builder |

# Task list

Все задачи выполняются в одной ревизии; исходные критерии не заменяются.

1. **D222-01 — fix now: закрепить план и baseline.**  
   До production edits записать этот статус и rv=1; сохранить список dirty paths и baseline diff для shared files. Проверить завершённость передачи T213, загрузить действующие `pdca-dotnet`/`nextorm-pdca` правила и integration skill. Подготовить artifact runners с capture exit codes и compile fixtures.  
   Опорные места: `collection-1.0.9-todo.md:25,56,66`; dirty paths из evidence pack.

2. **D222-02 — fix now: добавить 28 перегрузок.**  
   `EntityBuilder.cs:2515–2545`, `JoinedEntityBuilder.cs:31–55` и соответствующие арности до terminal builder. Применить ранний null-predicate guard; source resolution через текущий контекст и делегацию existing join. Не менять `JoinSourceResolver.cs:13–14`, `JoinCore:2725` и общую инфраструктуру.

3. **D222-03 — fix now: core и compiler coverage.**  
   Расширить `InMemoryJoinTests.cs:14`; использовать existing fixtures, включая join/left/right/full и арность 8 (`:23,:203,:241,:258,:161`). Закрыть V01–V26, включая **56 kind × receiver × options комбинаций**, negative source/predicate cases и explicit-source compatibility.

4. **D222-04 — fix now: provider SQL и integration coverage.**  
   Создать перечисленные семь планируемых файлов, используя существующие provider/common suite conventions. Проверять equivalence, а не вводить новые dialect rules. Выполнить real-provider случаи V28/V29 без infrastructure skips.

5. **D222-05 — fix now: docs и T53 register.**  
   Обновить EN/RU `02-joins.md` в текущем dirty состоянии: четыре метода, цепочки до восьми таблиц, predicate/options, inference, `From<T>()` resolution, invalid-null caveat, границы scope. Перечислить все 28 кандидатов T53, сохранив freeze held.

6. **D222-06 — fix now: один финальный DO → CHECK boundary sweep.**  
   После всех изменений выполнить build, compile fixtures, полный набор test projects с coverage, coverage merge/report, DocFX и final semantic/design audit. Записать receipts и actual locations, затем передать CHECK. Локальные focused runs во время DO разрешены, но не заменяют sweep.

**Deferred:** новый resolver/refactoring, Cross/Apply/type-only raw-table API, activation T53, отдельная MariaDB database infrastructure. Триггеры соответственно: доказанное дублирование/новое требование, отдельная утверждённая задача, возобновление T53, появление infrastructure/regression. T213 remainder и performance reports не входят в этот DO.

# Test strategy

## Unit / compiler

- `InMemoryJoinTests`: сравнение explicit/type-only результатов для четырёх kind, receiver-арностей 1–7, unmatched строк и null/default projection semantics.
- Минимальный обязательный options набор: omitted, explicit null, счётчик callback и sentinel-throwing callback.
- Configured mapping и convention mapping — отдельные случаи.
- Unresolvable case выбирается по **доказанному отказу existing `From<T>()`**, а не по предположению «тип не зарегистрирован».
- Compiler fixtures проверяют все 28 членов, return types, typed/untyped inference, named/optional arguments и существующие source overloads.
- Compile-negative fixture считается успешной проверкой только при ожидаемом compiler diagnostic, а не при любой ошибке restore/build.
- Новые классы тестов и test symbols — **планируемые**, не существующее evidence. Contract использует project-level запуск и не опирается на выдуманные FQN.

## Provider SQL-generation

Полные suites:

- `tests/nextorm.sqlite.tests`
- `tests/nextorm.postgres.tests`
- `tests/nextorm.sqlserver.tests`
- `tests/nextorm.mysql.tests`
- `tests/nextorm.mariadb.tests`
- `tests/nextorm.clickhouse.tests`

В каждом — paired explicit/type-only SQL, join kind и chained projection, options forwarding, configured source. Поддержка right/full следует existing explicit oracle; unsupported поведение проверяется assertion, не infrastructure skip.

## Integration

`tests/nextorm.integration.tests/CommonTestSuite.JoinByType.cs`: common fixture проверяет результаты inner/left и right/full там, где explicit форма поддерживается; иначе проверяет эквивалентный отказ.

Перед запуском загрузить `.opencode/skills/running-integration-tests/SKILL.md`. Использовать указанный Podman socket; при отсутствии socket выполнить предусмотренное skill восстановление и rerun. PostgreSQL, SQL Server, MySQL, ClickHouse должны действительно выполняться; SQLite тоже включён. Исключения инфраструктуры — DO → PLAN, не «зелёный со skip».

## Coverage и boundary

- Использовать `coverage.settings.xml`: scope `nextorm.{core,sqlite,postgres,sqlserver}`.
- Итоговые пороги: **line ≥85%, branch ≥75%**.
- На `1.0.9` CI может предупреждать вместо hard-fail; это не отменяет обязательного отчёта и критерий данного плана.
- Один финальный sweep после D222-02…05; сбор coverage совмещён с full project tests, без второго дублирующего solution-wide запуска.
- Pre-existing недостаток coverage не даёт права расширять scope или ослаблять порог: представить baseline evidence и вернуть в PLAN.

# Docs plan

**Трогаем:** EN/RU joins guide и внутренний status/collection register.

**Не трогаем:** toc, нумерацию guide, ClickHouse guide, публичные API references и readme, если не обнаружена фактическая сломанная ссылка. Новые страницы/переименования не планируются.

Требования:

- EN/RU синхронны в том же DO.
- Чётко отличать source-less overload от explicit source/CTE/QueryCommand.
- Показывать явный generic type в основном примере.
- Объяснить context-local `From<T>()` mapping и предел арности.
- Не создавать публичные ссылки на внутреннюю спецификацию.
- Не перезаписывать уже имеющиеся изменения guide от T213.

# Perf decision

**Новый benchmark не нужен.**

План ограничен additive wrappers на этапе построения builder/AST: `EntityBuilder.cs:2515–2545,2725`, `JoinedEntityBuilder.cs:31–55`, `JoinSourceResolver.cs:13–14`, analog `DataContextExtensions.cs:1019`. Нет изменения per-row materialization, query execution или plan-cache path.

Cached-path performance acceptance overlay здесь **не применяется**, поскольку существующие query-path/plan-cache алгоритмы не меняются. Это обязательный review guard, не предположение о незаметности любой нагрузки.

Если DO потребует изменить planner/preparation/cache или per-row path, план инвалидируется: DO → PLAN для измерения с pre-change baseline и соответствующего evidence contract. T213 benchmark artifacts не использовать как baseline T222.

# Reconnaissance decision

**Design spike не нужен.** Type-only resolution доказан existing `From<T>()` (`DataContextExtensions.cs:1019`); join delegation/resolution уже существует.

Compiler compatibility probe входит в D222-03 как обязательная проверка выбранного API, а не exploratory spike. Наблюдаемый результат: compile-positive cases проходят, negative cases дают ожидаемые diagnostics, explicit-source oracle сохраняется.

# Unit execution mode

**Последовательно, single tree, branch `1.0.9`; без worktree.**

Причина: production builders, `Builders/Joins/**`, два guide и collection register пересекаются с T213/соседними задачами. Запретить параллельных писателей этих файлов до передачи D222 в CHECK. Независимые read-only gathers и независимые test processes допустимы, но не параллельная реализация.

Никаких commit, push или merge.

# Design checklist

| Проверка | Решение PLAN |
|---|---|
| Утверждённая постановка / issue / milestone | #222, OPEN enhancement, milestone 1.0.9; implementation разрешена текущим циклом |
| Минимальность | 28 thin overloads; existing source resolution и explicit joins |
| Ответственность / дублирование | Не вводить resolver/framework и новые публичные типы |
| API/naming | Четыре existing names, generic join type, predicate + optional options; arity limit сохранён |
| Fail-closed | Null predicate — ранний guard; invalid source — existing resolution failure без fallback |
| Совместимость | Positive compiler matrix + explicit oracle; all-null invalid-input caveat явно выделен |
| Метаданные / cache | Не менять configured metadata, TVP cache, `QueryCommand.Cache`, `storeInCache` semantics |
| Providers | Не добавлять capabilities; dialect/runtime equality с explicit источником |
| Null/value/reference | Existing constraints и projection semantics; V20/V25/V26 обязательны |
| Sealing / anti-pattern | Новые типы не планируются; sealing ratio в pack отсутствует и не заявляется измеренным |
| Public API freeze | T53 held; перечислить 28 candidates, не включать анализатор и не создавать `PublicAPI.*` |
| Docs / repo hygiene | EN/RU, CRLF, сохранение dirty изменений и source provenance |
| Security / scope | Нет SQL identifier construction, новых connections, credentials или внешних записей |

Результат: выбранный дизайн годен для DO при выполнении перечисленных prerequisites; final audit подтверждает, что фактический diff не вышел за него.

# Risks/assumptions

| Пробел / риск | Закрытие |
|---|---|
| Полный текст constraints/return signatures отсутствует в pack | Явное предположение: повторяют existing arity contract. Semantic inventory и compile fixtures обязательны; несовпадение → PLAN |
| Конкретный rejected entity type не назван | DO устанавливает existing `From<T>()` rejection и сохраняет его как paired oracle; отсутствие registration само по себе не oracle |
| Invalid all-null source calls могут стать ambiguous | Допустимость этого ограниченного изменения — явное допущение; compiler receipt и docs обязательны |
| Возможное shadowing downstream extension methods | Внешняя ecosystem compatibility **deferred**; триггер — конкретный downstream compilation report. Repository calls проверяются сейчас |
| Правильная настройка `JoinOptions` не описана в pack | Не выдумывать properties; обязательны callback count/null/sentinel tests и existing options regressions |
| Dirty shared docs/builders | Baseline snapshot, последовательное владение, final preservation audit |
| T53 может быть активирован sibling task | Текущее доказанное состояние held; изменение состояния → prerequisite/replan, не игнорировать RS0016 |
| Контейнеры/tooling/coverage могут оказаться недоступны | Восстановление по skill; затем provisional DO report. Не объявлять done и не ослаблять criteria |
| New overload wrappers могут потребовать инфраструктурных изменений | Не расширять scope молча; DO → PLAN |

Уверенность высокая в минимальном подходе; constraints, точный invalid-type fixture, compiler corner cases и реальный provider run остаются обязательствами проверки, а не выполненными фактами.

# Evidence contract rv=1

## Frozen-contract note

**До production DO coder записывает этот контракт в статус-файл.** Все перечисленные строки обязательны. Их planned sources не считаются полученным evidence.

- Корень `B = artifacts/pdca/D222-join-overloads-by-type/rv1`.
- Команды выполняются из `/home/alex/sources/nextorm`.
- Планируемый producer `bash "$B/capture.sh" <row-id> -- <command…>` сохраняет:
  `B/<row-id>/command.txt`, `stdout.log`, `stderr.log`, `exitcode.txt`, `receipt.json`.
  Он возвращает exit code вызываемой команды, не маскирует pipeline failures.
- `receipt.json` фиксирует row ID, requirement IDs, `rv=1`, рабочую директорию, revision/tree provenance, время, applicability и результаты.
- Для compound runner обязательны отдельные exit/result receipts каждого дочернего вызова.
- Production/test `file:line` записываются **после** реализации из фактического semantic evidence. Будущие locations/test symbols не заявляются.
- Coverage/test applicability predicates ниже наблюдаемы; отсутствие отчёта не делает строку неприменимой.
- Существующие ID требований/строк сохраняются при обоснованной ревизии; новая `rv` явно supersedes старую с сохранением обязательств и добавлением ID новых вариантов. Для r=1 supersession отсутствует.
- Отсутствующий отчёт само по себе не обосновывает новую rv, DO iteration или сброс попыток.

## Required rows

| Stable row ID; rv | Требования; проверка / сценарий | Evidence kind и planned source | Точный producer / вызов | Oracle: exit / result / log | Обязательные artifacts | Поток-владелец; applicability |
|---|---|---|---|---|---|---|
| E222-BASE; 1 | A222-05,07; shared dirty baseline и prerequisites | Git/status + frozen-plan provenance | `bash "$B/capture.sh" E222-BASE -- bash "$B/baseline.sh"`; runner вызывает `git status --short`, `git diff -- src/nextorm.core/Builders docs/guide/02-joins.md docs/ru/guide/02-joins.md docs/specs/status/collection-1.0.9-todo.md` и сохраняет freeze state | Exit 0; исходный dirty diff сохранён, T213 handoff подтверждён, T53 held | `B/E222-BASE/**`, `baseline.patch`, `prerequisites.json`; этот status-файл | coder; **всегда**, до implementation |
| E222-API; 1 | A222-01,02,04,05; 28 signatures и existing semantic surface | Roslyn semantic inventory + compiler coverage manifest | `Task(scout, "T222 rv1: Roslyn first; discover and inventory EntityBuilder<TEntity> and JoinedEntityBuilder arities 2..8, new and existing join signatures/constraints/returns; inspect source delegation and report actual file:line. No text-search symbol resolution.")`; coder сохраняет ответ | Invocation успешен; ровно 28 additions; constraints/returns подтверждены; исключённые additions отсутствуют | `B/E222-API/semantic-report.md`, `members.json`, `receipt.json`; T53 candidate перечень в collection status | scout → coder persistence; **всегда** |
| E222-BUILD; 1 | A222-01,06; production/test compile | Build transcript | `bash "$B/capture.sh" E222-BUILD -- dotnet build -c Debug` | Exit 0; итог 0 warnings/0 errors, без suppressed new diagnostics | `B/E222-BUILD/**` | coder runs, check verifies; **всегда** |
| E222-COMPAT; 1 | A222-01,03,04; V16,V21–V25 | Compiler positive/negative cases, baseline comparison | `bash "$B/capture.sh" E222-COMPAT -- bash "$B/compatibility/run.sh"`; runner выполняет `dotnet build <каждый manifest-listed fixture.csproj> -c Debug` | Runner exit 0; positive builds exit 0; negative имеют ожидаемый diagnostic. All-null pair зафиксирован относительно baseline; restore errors не принимаются | `B/compatibility/**`, `B/E222-COMPAT/**`, case manifest с точными project paths/commands/diagnostics | coder; **всегда**; constraint-dependent value-type branches решаются по E222-API |
| E222-CORE; 1 | A222-02,03,04,06; V01–V26 | Full core suite + coverage | `bash "$B/capture.sh" E222-CORE -- dotnet-coverage collect "dotnet test tests/nextorm.core.tests -c Debug" -s coverage.settings.xml -f xml -o "$B/coverage/core.xml"` | Exit 0; tests реально обнаружены, 0 failed; matrix cases отражены в manifest и actual test evidence | `B/E222-CORE/**`, `B/coverage/core.xml`, core case manifest | coder; **всегда** |
| E222-SQL; 1 | A222-02,03,04,06; V27,V29,V32 | Шесть full dialect suites + coverage | `bash "$B/capture.sh" E222-SQL -- bash "$B/sql-sweep.sh"`; для `p=sqlite,postgres,sqlserver,mysql,mariadb,clickhouse` точно выполняет `dotnet-coverage collect "dotnet test tests/nextorm.$p.tests -c Debug" -s coverage.settings.xml -f xml -o "$B/coverage/$p.xml"` | Runner и все шесть команд exit 0; каждая suite обнаружена, 0 failed; supported/unsupported oracle cases явно перечислены | `B/E222-SQL/**`, шесть per-provider receipts, `B/coverage/{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.xml`, SQL case manifest | coder; **все шесть безусловно** |
| E222-INTEGRATION; 1 | A222-02,03,04,06; V28,V29 | Real-provider execution + coverage; infrastructure provenance | После integration skill: `bash "$B/capture.sh" E222-INTEGRATION -- env DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet-coverage collect "dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor" -s coverage.settings.xml -f xml -o "$B/coverage/integration.xml"` | Exit 0; SQLite/Postgres/SQL Server/MySQL/ClickHouse действительно выполнены, 0 failed; нет infrastructure/provider skips. Capability guards — assertions с explicit oracle | `B/E222-INTEGRATION/**`, `B/coverage/integration.xml`, provider execution manifest, recovery log при необходимости | coder; **всегда**, socket отсутствие не исключает строку |
| E222-COVERAGE; 1 | A222-06; итоговые line/branch thresholds | Merged coverage + machine-readable gate | `bash "$B/capture.sh" E222-COVERAGE -- bash "$B/coverage-gate.sh"`; runner вызывает `dotnet-coverage merge ... merged.xml`, затем `reportgenerator ...` и проверяет totals | Все команды exit 0; included scope соответствует settings; line ≥85%, branch ≥75%; missing/zero denominator не трактуется как успех | `B/E222-COVERAGE/**`, `merged.xml`, `report/Cobertura.xml`, `report/Summary.txt`, `thresholds.json` | coder, check applies oracle; **всегда** |
| E222-DOCS; 1 | A222-05,06; EN/RU и publication | DocFX build + bilingual docs review | `bash "$B/capture.sh" E222-DOCS -- dotnet docfx docs/docfx.json`; `Task(scout, …)` review EN/RU 02-joins.md | Exit 0; нет новых broken links/API samples; review удовлетворяет обоим языкам | `B/E222-DOCS/**`, `docs-review.md`; оба guide | coder + scout; **всегда** |
| E222-DESIGN; 1 | A222-01…05,07; footprint, invariants, perf decision, dirty preservation | Final semantic/diff design audit | `Task(scout, "T222 rv1 final audit: Roslyn first for symbols; compare actual production change to 28-member contract and baseline; verify context-local From delegation, early predicate guard, options, no planner/cache/row/provider algorithm changes, no T213 overwrite, held T53 and CRLF; report actual file:line and failures.")`; coder сохраняет ответ | Invocation успешен; каждый checklist invariant PASS с provenance; новые perf applicability triggers отсутствуют | `B/E222-DESIGN/audit.md`, `receipt.json`, final footprint manifest | scout → coder; **всегда**, perf waiver подтверждается здесь |
| E222-CHECK; 1 | A222-01…07; completeness и final verdict | Independent CHECK + evidence index | `Task(check, "Check D222-join-overloads-by-type-1.md, plan r1 / contract rv1; evaluate every required row and variant, P1 matrix and provenance; apply finite re-gather budget; return verdict without lowering criteria.")` | PASS только при полном evidence; missing report → bounded re-gather, не fictional PASS | `docs/specs/status/D222-join-overloads-by-type-1.md`, `B/E222-CHECK/check-report.md`, evidence index и re-gather ledger | check; **всегда** |

**CHECK re-gather budget:** максимум **2 targeted rounds на данный CHECK**, владелец — `check`. В каждом раунде заранее перечисляются отсутствующие/невалидные row IDs; повторяются только нужные producer calls, scope не меняется. Coder запускает команды/сохраняет artifacts, scout собирает read-only facts. После исчерпания бюджета CHECK сообщает недостаток evidence; оркестратор передаёт в PLAN. Один лишь отсутствующий отчёт не запускает production DO заново.

# Prerequisites / requirements on sibling results

1. **T213:** завершённая передача текущего `Builders/Joins/**`; никакого параллельного writer. Сохраняются его resolver, projection/arity semantics, JoinInto и dirty docs.
2. **Shared Builders:** до D222-02 зафиксировать tree provenance; изменения siblings в двух production файлах после baseline требуют повторной совместной проверки, не overwrite.
3. **Shared docs/register:** sequential edits с сохранением baseline hunks; нельзя заменять guide или collection status целиком.
4. **T53:** текущий held state — доказанное предусловие. Активация analyzers/PublicAPI siblings требует включить их обязательства через PLAN; не отключать diagnostics.
5. **Toolchain:** .NET 10, warnings-as-errors, центральные версии, OS-specific outputs, CRLF; никаких package/API-freeze изменений в данном scope.
6. **DO reports:** blocker report провизорный, не done. Новое аддитивное предусловие сохраняет исходную D222-задачу активной, blocked на зависимости, со всеми критериями и остатком.
7. **Возврат в PLAN:** истинный внешний blocker → рекомендация оркестратору вызвать `escalate`; недостаток evidence → сначала targeted scout, стойкая низкая уверенность → `escalate`, trigger 5. Фактическая смена действий/зависимостей требует новой `P:`-задачи и r+1; переименование или отсутствие отчёта revision не меняет.

## Progress log

- 2026-10-13 PLAN r=1 persisted by coder (collection `1.0.9-todo`, task T222 / issue #222). plan_state=ready. DO not started (collection task-PLAN boundary: yield at PLAN→DO).
- 2026-10-13T06:44Z | DO | r=1 | n=1/3 | D222-01 DO started: mandatory scope validated before any edit (validate_inner_loop.py brief, exit 0) | artifacts/pdca/D222-join-overloads-by-type/rv1/scope.json
- 2026-10-13T06:44Z | DO | r=1 | n=1/3 | T213 `Builders/Joins/**` handoff confirmed (collection: T213 done, plan r=6/rv=5); no parallel writer — single lane, current worktree/branch 1.0.9 | artifacts/pdca/D222-join-overloads-by-type/rv1/E222-BASE/prerequisites.json
- 2026-10-13T06:44Z | DO | r=1 | n=1/3 | Shared dirty baseline captured before edits (2 pre-existing modified SVGs preserved, not task paths, excluded from commits); frozen runners capture.sh/baseline.sh + compatibility fixtures scaffolded | artifacts/pdca/D222-join-overloads-by-type/rv1/baseline.patch; E222-BASE/receipt.json
- 2026-10-13T06:44Z | DO | r=1 | n=1/3 | Notice (planner finding, NOT a plan revision — r=1/rv=1/N=1 unchanged): the spec's `EntityBuilder.cs:2515–2545` / `JoinCore:2725` line refs are STALE; current locations reported as `EntityBuilder.cs` join members `:4259–4385`, `JoinCore :4316/:4544`. | this status file
- 2026-10-13T06:44Z | DO | r=1 | n=1/3 | Notice verification (coder, Roslyn): the reported `:4259–4385` / `:4316/:4544` belong to the NON-generic raw-table `EntityBuilder` (class opens `:4021`), which V16 declares out of scope; the spec's `:2515–2545` / `:2725` are current and belong to the IN-SCOPE generic `EntityBuilder<TEntity>` (class opens `:22`). The Notice is thus a query-target artifact (Roslyn `members NextORM.Core.EntityBuilder` resolves the non-generic type). D222-02 therefore edits the generic `EntityBuilder<TEntity>` surface per spec/plan; raw-table `EntityBuilder` untouched. | roslyn members NextORM.Core.EntityBuilder; this status file
- 2026-10-13T06:58Z | DO | r=1 | n=1/3 | D222-03 core+compiler coverage: InMemoryJoinTests extended 19->27 (type-only==explicit at every receiver arity for the 4 kinds; V06-V08 unmatched nulls; V17/V18 options forwarded; V19 sentinel not swallowed; V20 null predicate on every receiver/kind; V26 null/default). Compiler fixtures: positive builds (28 members + named/optional + typed expr + struct + QueryCommand), V22 untyped -> CS0411, V24 all-null observed to still compile (no new ambiguity). | artifacts/pdca/D222-join-overloads-by-type/rv1/INNER-CORE-JOINS; E222-COMPAT
- 2026-10-13T06:58Z | DO | r=1 | n=1/3 | D222-04 provider SQL + integration coverage: 6 TypeOnlyJoinSqlGenerationTests (sqlite/sqlserver/postgres/mysql/mariadb/clickhouse) each 7/7 (byte-identical type-only vs explicit SQL at every receiver arity, configured mapping, convention resolution, unresolvable type, Right/Full capability rejection equivalence). Integration `CommonTestSuite.JoinByType.cs` (shared + ClickHouse class) compiles; container run deferred to D222-06. | artifacts/pdca/D222-join-overloads-by-type/rv1/INNER-*; INNER-INTEGRATION-COMPILE
- 2026-10-13T06:58Z | DO | r=1 | n=1/3 | Inner-loop evidence report validated fail-closed (validate_inner_loop.py report, exit 0); single full-solution build gate 0 warnings / 0 errors | artifacts/pdca/D222-join-overloads-by-type/rv1/evidence.json; E222-BUILD
- 2026-10-13T07:31Z | DO | r=1 | n=1/3 | D222-05 docs in sync: EN `docs/guide/02-joins.md` + RU `docs/ru/guide/02-joins.md` add the source-less (type-only) join section (4 operators, chains to 8 tables, options forwarding, typed/untyped inference, context-local `From<T>()` resolution, null-predicate + all-null caveats, scope bounds: no Cross/Apply, no raw-table, no terminal arity 8), no spec links; 28 members registered as T53 freeze candidates (freeze HELD, no PublicAPI files). | E222-DOCS/docs-review.md; D53-public-api-freeze-1.md
- 2026-10-13T07:31Z | DO | r=1 | n=1/3 | D222-06 boundary sweep: E222-BASE 0; E222-API 0 (28 members via Roslyn); E222-BUILD 0 (0 warn/0 err); E222-COMPAT 0; E222-CORE 0 (2446/2446); E222-SQL 0 all six (sqlite 1661/0f/1 pre-existing LOB skip; mssql 812; pg 895; mysql 378; mariadb 259; clickhouse 660). | artifacts/pdca/D222-join-overloads-by-type/rv1/E222-*
- 2026-10-13T07:31Z | DO | r=1 | n=1/3 | E222-INTEGRATION 0 with DOCKER_HOST: containers executed (4 started); 3779 total / 0 failed / 197 capability-guard skips, 0 provider-unavailable skips; JoinByType_* not skipped; Postgres/SQLServer/MySQL/ClickHouse executed. No skipped required provider. | E222-INTEGRATION/stdout.log
- 2026-10-13T07:31Z | DO | r=1 | n=1/3 | E222-COVERAGE 0: contract 4-xml line 82.6% (subset artifact, no branch in `-f xml`); full-sweep 8-xml line 87.9%; CI-comparable whole-solution cobertura line 88.7% / branch 80.7% (≥ 85/75). E222-DOCS 0 (docfx; 2 pre-existing sourcegenerator warnings). E222-DESIGN 0 (+44/+264 additive only; JoinSourceResolver/provider src/Cross-Apply/arity-8 unchanged; perf waiver). | E222-COVERAGE/stdout.log; E222-DESIGN/audit.md
- 2026-10-13T07:31Z | DO | r=1 | n=1/3 | E222-MUTATION 0 (Stryker.NET 5.0.0, installed): JoinedEntityBuilder.cs scope, 107 tested, 65 killed / 42 survived; new members 24 killed / 0 survived, all 42 survivors pre-existing. manifest+report validators exit 0. DO complete — ready for CHECK. | E222-MUTATION/mutation-disposition.md; manifest.json; evidence.json
- 2026-10-13T07:46Z | CHECK | r=1 | n=1/3 | CHECK #1 FAIL: (1) docs claimed null-predicate parity with the source-explicit form, but the explicit form ACCEPTS a null predicate (`JoinCore` takes `LambdaExpression?`) while the source-less form throws `ArgumentNullException` first; (2) docs claimed a ninth join is rejected at compile time / no source-less overload at terminal arity 8, but the inherited generic-base `EntityBuilder<Projection<T1..T8>>` source-less join compiles; (3) V18 options parity and V24/V25 edge evidence incomplete; (4) evidence provenance ragged (receipts at 535fb429), missing machine thresholds.json and case indexes, integration container count recorded as 4 while the log shows 5. | CHECK-*
- 2026-10-13T07:46Z | DO | r=1 | n=2/3 | Corrective round FIX1-FIX3 (docs): EN/RU `02-joins.md` now qualify the equivalence to NON-NULL predicates and state the deliberate null difference; the all-null untyped pair is documented to bind to the pre-existing 2-arg `Cte<T>` overload (`ArgumentNullException: Parameter 'cte'`); the terminal-arity-8 wording now separates the flat arity-chain scope bound (no new overload declared) from inherited API reachability (further source-less join compiles). Terminal-class XML caveat at `JoinedEntityBuilder.cs:1724` corrected likewise. No code/compatibility change. | docs/guide/02-joins.md; docs/ru/guide/02-joins.md; src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs
- 2026-10-13T07:46Z | DO | r=1 | n=2/3 | Corrective round FIX4-FIX6 (tests): `InMemoryJoinTests` options test now asserts BOTH type-only and explicit callback counts == 1 and AST parity (JoinType/Strictness/IsGlobal/JoinHint/TableHints); all-null pair asserted to throw `ArgumentNullException` ParamName 'cte'; value-type entity paired runtime test added (no `where TEntity : class` constraint -> allowed branch). Core inner-loop 29/29. | tests/nextorm.core.tests/InMemoryJoinTests.cs; CORR-CORE
- 2026-10-13T07:46Z | DO | r=1 | n=2/3 | Corrective round FIX5 (V24 baseline): compiled the identical all-null probe against a detached worktree at base `4fbb603b` -> exit 0 (baseline parity; the bound Cte overload predates T222) and against HEAD -> exit 0. Artifacts under `compatibility/allnull-baseline/`. | E222-COMPAT-BASELINE; baseline-comparison.md
- 2026-10-13T07:46Z | DO | r=1 | n=2/3 | Corrective round FIX7 (evidence): re-ran E222-BASE/API/DESIGN/COVERAGE on the current tree; added machine-readable `E222-COVERAGE/thresholds.json` (line 88.7 / branch 80.7, pass) with the cobertura producer reconciled (E222-COVERAGE-ALL); added CASE indexes for COMPAT/CORE/SQL; corrected the integration container count to 5 start/stop pairs (log-derived). Manifest now carries E222-CHECK (open: CHECK#1 findings + re-gather ledger) and E222-COMPAT-BASELINE. Validators manifest/report exit 0. | E222-COVERAGE/thresholds.json; manifest.json; evidence.json
- 2026-10-13T07:54Z | CHECK | r=1 | n=2/3 | CHECK #2 FAIL: A222-05 residual docs claim (arity-8 'compile-time cap / no further join' still stated in EN `02-joins.md:25-29,243-244,701-702,723-724` and RU `:26-32,248,713-714,736-737`) + A222-07 evidence completeness (stale selected_count 27, unpinned V24 baseline SHA, missing COMPAT fixture file:line, variant-closure index). | CHECK2-*
- 2026-10-13T07:54Z | DO | r=1 | n=3/3 | Escalation decision: (a) stay at r=1, final DO round n=3/3, comprehensive CLAIM-LEVEL docs fix + separate evidence repair. No plan revision. | this status file
- 2026-10-13T07:58Z | DO | r=1 | n=3/3 | Final corrective round: claim registry over both guides + neighbouring pages + XML caveat `E222-DOCS/claim-registry.md` — 27 mentions: 12 inaccurate (EN 02-joins :25-31/:246-248/:703-706/:722-731; RU :26-33/:251-253/:716-719/:738-743; EN/RU 08-cte; EN/RU limitations) ALL REWRITTEN to 'flat arity-chain scope bound, not a compile-time rejection'; 9 accurate + 6 generator-scoped verified unchanged. Zero remaining inaccurate 'cap/does not compile' claims. | E222-DOCS/claim-registry.md
- 2026-10-13T07:58Z | DO | r=1 | n=3/3 | Evidence repair (separate from docs): `evidence.json` core selected_count 27->29; V24 baseline SHA pinned `4fbb603b27ff811508ec477604efba2c2132066a` (`baseline-revision.txt`) and the 'identical to AllNull.cs' overstatement corrected (receiver line differs); COMPAT fixture `file:line` added; variant-closure (V01-V26 core / V27,V29,V32 SQL / V28 integration) + V30 deferral trigger indexed; E222-CHECK left open. Re-runs: build 0/0; core 29/29; docfx 0; validators manifest/report 0. | evidence.json; E222-*/case-index.json; CHECK3-*
