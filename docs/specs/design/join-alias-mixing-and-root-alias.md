# Design: свободное смешивание positional/alias джойнов и алиас корня

## Tracking

- GitHub issue: #160 — https://github.com/AlexeyShirshov/nextorm/issues/160
- Milestone: `1.0.9-rc1`
- Связанные: #113 (родительская join-alias фича, закрыта), #159 (CTE alias-API)
- Spec: этот файл; план: будет создан через `writing-plans`
- Status: design approved (in-chat), spec review pending

## 1. Проблема и намерение

#113 добавил именованные join-алиасы (`Alias.<Name>`), но зафиксировал **alias-only** семантику:

- позиционные и алиасные джойны нельзя смешивать (guard `EntityBuilder.cs:3137`; генератор собирает только
  alias→alias; `JoinAliasGenerator.ResolveBuilderSymbol` отвергает `JoinedEntityBuilder<...>`);
- корневая таблица всегда доступна только как `Item1` — дать ей имя нельзя.

Это противоречит исходному намерению владельца: `ItemN` должен быть доступен наравне с алиасом, алиас не
обязателен, а корень должен уметь получать лексическое имя. Сейчас `ItemN` доступен наравне только **на
чтение**; на **объявление** алиас обязателен, если уже начат.

Цель: (1) свободное смешивание позиционных и алиасных джойнов; (2) алиас корня через цепочный
`.WithAlias(Alias.X)` для всех источников.

## 2. Утверждённые решения

- `.WithAlias(Alias.X)` — **только на корне** и именует slot 1; у джойнов имя остаётся трейлинг-аргументом
  `Alias.<Name>`.
- In-memory — **fail-closed**: любой алиас в цепочке (корень или join) → `NotSupportedException`; чисто
  позиционные цепочки в памяти работают как раньше.
- Сгенерированный surface меняется свободно (pre-release, API не заморожен): единый slot-encoded нейминг,
  без legacy-исключений.
- Архитектура **A**: generator-heavy / core-light; все расширения проекции идут через существующий
  `JoinAlias<TNext,TNextEntity,TJoinEntity>` seam; в ядро добавляются `Projection<T1>` и `AliasRoot` seam.
- Фазы: **Фаза 1** — свободное смешивание; **Фаза 2** — алиас корня.
- Приоритет — производительность и удобство; позиционный путь без алиасов сохраняет нулевой оверхед.

## 3. Архитектура и границы

**Два инварианта пути:**

- **Чисто позиционная цепочка** (никаких `Alias.*`) → core-путь без изменений: `JoinedEntityBuilder<T1..Tn>`,
  `Projection<T1..Tn>`, генератор не участвует, in-memory работает как сейчас.
- **Любая цепочка с ≥1 алиасом** (корень `.WithAlias` или join trailing `Alias.<Name>`) → generated-путь:
  генератор эмитит projection+builder+extensions, и **все** шаги — позиционные и алиасные — расширяют одну
  projection-иерархию через `JoinAlias` seam.

**Компоненты и ответственность:**

- `nextorm.core.sourcegenerator` — модель цепочки получает вид шага (`Positional` | `Alias(name)`); корневой
  шаг `Alias` из `.WithAlias`; эмит projection/builder/extension для смешанных схем; диагностики.
- `nextorm.core` — добавляются только: `Projection<T1>` (arity-1, `IExtendableProjection`) и generic
  `AliasRoot<TNext,TNextEntity>` seam (Фаза 2). Guard `CreateJoined:3137` **сохраняется** как предохранитель
  базового (не-generated) пути.
- Резолв алиасов (`AliasFromProjectionVisitor`, `ProjectionAliasCache`) **не меняется**: он уже slot-based
  (`ItemN` → позиция; `JoinSlotAttribute.Position` → позиция → `t1..t8`).
- In-memory — fail-closed на обоих seam'ах (root и join).

**Границы:** генератор владеет «схема → типы»; ядро владеет позиционным движком и generic seam'ами и не знает
про схемы. Alias-члены остаются expression-only.

## 4. Модель проекции и схема

**Схема цепочки** — упорядоченный список шагов; шаг: `Positional` или `Alias(name)`. Схема = (arity, набор
`(slot, name)`). Это ключ эмита типов (`ChainKey`).

**Проекция смешанной цепочки** — производная от `Projection<T1..Tn>`: базовый `Projection` даёт настоящие
`Item1..ItemN`; алиасные слоты добавляют именованные expression-only свойства с `[JoinSlot(slot)]`.
Инвариант: `p.ItemK` и алиас слота K — один и тот же слот.

**`Projection<T1>` (новый публичный, arity-1):** `Item1` + `IExtendableProjection.Extend<T>` →
`Projection<T1,T>`. Корневая alias-проекция: `AliasProjection_A1_Order<T> : Projection<T>`. Это единственное
добавление типов в ядро. Риск: сейчас `Projection` всегда arity ≥ 2 — planner/`DefaultColumnsProvider` могли не
встречать dim=1; нужен отдельный тест плана single-entity через dim-1 проекцию.

**Единый нейминг:** `_` зарезервирован в именах алиасов, поэтому суффикс кодирует каждый шаг:
`A{slot}_{name}` для алиасного, `P{slot}` для позиционного. Пример:
`AliasProjection_A1_Order_P2_A3_Buyer<T1,T2,T3>`, builder `AliasJoin_<тот же суффикс>`. Парсер типа
(`ResolveFromTypeSyntax`) читает токены детерминированно; позицию несёт `A{slot}`, поэтому имена с цифрами на
конце (`Buyer2`) не конфликтуют с позицией. Legacy-имена (`AliasProjection_Buyer_Approver`) **не сохраняются**.

**Arity:** кап 8 как сейчас (`Projection<T1..T8>`); dim-1 добавляется снизу; переполнение — та же диагностика.

## 5. Эмит генератора

Эмит **usage-driven**: генератор идёт от call-site'ов, а не перебирает все схемы, поэтому комбинаторика
ограничена реально написанными цепочками.

**Для каждой использованной смешанной схемы S (≥1 алиас):**

- `AliasProjection_<S><T1..Tn>` : `Projection<T1..Tn>` (+ alias-члены с `[JoinSlot]`; для корня n=1 →
  `Projection<T1>`);
- `AliasJoin_<S><T1..Tn>` : `EntityBuilder<AliasProjection_<S><T1..Tn>>`;
- transitions: на каждый следующий шаг и каждый применимый из 7 операторов — extension.

**Два вида transitions:**

- *алиасный шаг* — как сейчас: `Join<TJoin>(this AliasJoin_<S>…, …, Alias.XMarker)` →
  `self.JoinAlias<AliasJoin_<S'>, AliasProjection_<S'>, TJoin>(…)`;
- *позиционный шаг* — сгенерированный `new` **instance**-метод `Join<TJoin>(EntityBuilder<TJoin>,
  Expression<…>, JoinOptions?)` на `AliasJoin_<S>…` → **тот же** `JoinAlias` seam, но `S'` добавляет
  только `ItemN` (без имени). Extension-метод C# не может затенять применимый instance-метод, поэтому
  переход эмитится именно как `new` instance-метод на сгенерированном приёмнике (тот же приём для
  позиционного шага после корневого `.WithAlias`); guard `3137` базового `EntityBuilder.Join` не
  срабатывает.

**Позиционный префикс:** генератор резолвит core-типы как позиционные шаги — `EntityBuilder<T>` = база;
`JoinedEntityBuilder<T1..Tn>` = база + `n-1` позиционных шагов (снятие текущего запрета
`ResolveBuilderSymbol`). Так покрывается `From<Order>().Join(...).Join(..., Alias.Approver)`.

**Корень `.WithAlias` (Фаза 2):** на каждое корневое имя — extension
`WithAlias<T>(this EntityBuilder<T> self, Alias.RootMarker marker)` → `AliasJoin_A1_Root<T>`. Внутри — core-seam
`AliasRoot<TNext,TNextEntity>(create)` (аналог `CreateAliasJoined` без join'а: state через `ApplyJoinStateTo`,
`_joins` пуст). Root-only гарантируется runtime-guard'ом в seam'е (builder уже с джойнами/alias-state →
`NotSupportedException`) и синтаксической диагностикой генератора (`NORMGEN008` — `.WithAlias` не на
простом корне). На сгенерированном корневом приёмнике генератор дополнительно сеет позиционные
`new` instance-переходы, чтобы `.Join(...)` после `.WithAlias` не уходил в базовый
`EntityBuilder.Join`. Производный корень (`From(builder)` / `From(QueryCommand<T>)`) материализуется
`AliasRoot` в явный derived-table `FromExpression` один раз (с очисткой `_query`), поэтому сохраняется
и получает псевдоним `t1`, как `FromSql`; guard проекции в `ResolveJoinBase` не меняется.

## 6. Рантайм, SQL-алиасы, in-memory, план-кэш

**Резолв алиасов не меняется.** `AliasFromProjectionVisitor.ResolveAliasCore`: сначала `ItemN` → позиция, иначе
`JoinSlotAttribute.Position` → `DefaultAliasProvider.GetAliasName(position)` → `t1..t8`. Slot 1 (корень) и
смешанные схемы работают этим же кодом. Прямое чтение alias-члена вне expression-tree бросает
`NotSupportedException` (expression-only).

**SQL-имена:** корневой алиас → `t1`; join-алиасы → `t2..t8` по позиции слота, независимо от вида шага.
Column-alias в `SELECT` для именованного слота — имя члена (`Buyer`); для позиционных `ItemN` — существующее
поведение (имя source-члена).

**In-memory — fail-closed:** любой алиас → `NotSupportedException` (root и join seam'ы). Чисто позиционные
цепочки — без изменений.

**Guard `3137`** остаётся для базового (не-generated) `EntityBuilder.Join`; generated-путь затеняет его.

**Plan cache:** alias-цепочки покрыты `AliasPlanCacheTests`; для смешанных — обязательный аналог (чередование
слотов на одном `DataContext`, повторные cached-выполнения), чтобы исключить смешение слотов.

## 7. Диагностики

Сохраняем `NORMGEN001–006`. Добавляем/уточняем:

- коллизия двух корневых алиасов; коллизия алиаса с `ItemN` (существующая проверка);
- `WithAlias` не на корне (цепочка уже с джойнами/alias-state) — синтаксическая диагностика + runtime-guard;
- неодобренная форма аргумента `Alias.<Name>`; arity > 8;
- неоднозначный slot-encoded суффикс (по построению недостижим, но проверяем при парсинге).

## 8. Производительность

- Позиционный путь (без алиасов) не меняется — нулевой оверхед.
- Alias/смешанный путь идёт тем же `JoinAlias` seam, без новых per-query аллокаций сверх существующих.
- Generated alias-члены — expression-only getters (`=> throw`), без полей/состояния.
- Резолв слота остаётся memoized (`ProjectionAliasCache`, `ConditionalWeakTable` в `AliasFromProjectionVisitor`).
- **Перф-приёмка cached-path** (текущие 7 кейсов + mixed-сценарий) — обязательный гейт.

## 9. Тестирование и документация

**Unit (core/alias):** generated-surface (`Item1..ItemN` + alias-члены с `[JoinSlot]`, единый нейминг,
expression-only); резолв `ItemN`/алиас одного слота → один `tN`, корень → `t1`, `Projection<T1>` (dim-1) в
planner/`DefaultColumnsProvider`; смешивание в обе стороны и чередование, `Where` между шагами, arity cap;
diagnostics; plan cache смешанной цепочки; in-memory fail-closed и неизменность позиционного in-memory.

**Provider SQL-gen:** 7 операторов в смешанных цепочках; корневой алиас для всех корней (`From<T>`,
`From("table")`/`TableAlias`, `FromSql`, `From(Cte<T>)`, temp, `FromTableFunction<T>`, `From(QueryCommand<T>)`,
`CreateQueryBuilder*`); корректные `t1..t8` и column-alias.

**Integration (`CommonTestSuite`, `DOCKER_HOST`):** end-to-end корневой алиас + смешанные джойны на реальных
PostgreSQL / SQL Server / MySQL / MariaDB / SQLite / ClickHouse (где форма применима); провайдерски-специфичное
— в `*SpecificTests.cs`. Skip'и провайдеров не считаются зелёным прогоном.

**Coverage:** ≥ `MIN_LINE_COVERAGE`(85) / `MIN_BRANCH_COVERAGE`(75).

**Docs:** `docs/guide/02-joins.md` + `docs/ru/guide/02-joins.md` (свободное смешивание, `.WithAlias`, единый
нейминг, примеры); `docs/querying/01-projections.md` (+RU) где затрагивается; `docs/advanced/limitations.md`
(+RU) — снять формулировки alias-only / in-memory; внутренние `docs/specs/**` (статус alias-only и variant-matrix).

## 10. Фазы, поставка, гейты

**Фаза 1 — свободное смешивание** (генератор-центрична; ядро не меняется): модель схемы `Positional|Alias`;
резолв позиционных префиксов; единый slot-encoded нейминг; эмит projection/builder + позиционных и алиасных
transitions через `JoinAlias`; diagnostics. Готово, когда смешанные цепочки компилируются и дают верный SQL,
alias-only семантика сохранена.

**Фаза 2 — алиас корня:** ядро — `Projection<T1>` + `AliasRoot` seam + root-only guard + in-memory fail;
генератор — `.WithAlias` на каждое корневое имя; покрытие всех источников. Готово, когда `.WithAlias` работает
на всех корнях и dim-1 проекция корректно планируется.

Каждая фаза независимо зелёная: сборка 0/0, unit + provider SQL-gen + integration (`DOCKER_HOST`) +
перф-приёмка cached-path + coverage ≥85/75.

**Поставка:** без коммитов/merge, интеграция патчами (правило репозитория) — если владелец не скажет иначе.

## 11. Вне scope / исключения

- `JoinInto` — без alias-поверхности (как решено ранее).
- Полная поддержка алиасов в in-memory — не делается (fail-closed).
- Арность ≤ 8 (`Projection<T1..T8>`); поднятие потолка — через `As<T>`, как сейчас.

## 12. Риски

- **dim-1 проекция** в planner/`DefaultColumnsProvider` — потенциально не встречалась; митигация: отдельный
  тест single-entity плана через `Projection<T1>`.
- **Комбинаторика эмита** для смешанных схем — митигация: usage-driven эмит (только реально написанные
  цепочки), единый суффикс-схема.
- **Plan-cache смешение слотов** при чередовании positional/alias — митигация: обязательный mixed plan-cache тест.
- **Overload-разрешение** generated `.WithAlias`/позиционных extension'ов против базовых методов — митигация:
  `new`-затенение + тест компиляции API-surface.
