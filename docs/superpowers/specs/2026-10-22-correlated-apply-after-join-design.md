# #324: коррелированный APPLY после join — плоская проекция и контекстная привязка

> **Статус:** scope и архитектура **согласованы** в чате 2026-10-22; **письменный дизайн
> УТВЕРЖДЁН пользователем 2026-10-22** (явный approval). Следующая стадия — **план реализации
> (PLAN)**; план ещё **не написан и не утверждён**. Реализация **НЕ АВТОРИЗОВАНА**.
> Commit/push/merge не авторизованы.
>
> Tracking: GitHub issue **#324** — https://github.com/AlexeyShirshov/nextorm/issues/324
> (проверено: **OPEN**, milestone **`1.0.9-rc4`**; issue остаётся OPEN). Связанный issue
> **#316** — исправление ключа кэша для `JoinExpression.ApplySource`: это **отдельная** работа и
> **явная предпосылка** приёмки cache-критерия и финального завершения #324. Её нельзя
> присваивать, переписывать или дублировать альтернативной реализацией; legacy-записи статусов
> (`.pdca/status/**`, `docs/specs/status/**`) не переписываются.

| Поле | Значение |
|---|---|
| Задача | [#324](https://github.com/AlexeyShirshov/nextorm/issues/324) |
| Milestone | `1.0.9-rc4` (проверен в GitHub) |
| Зависимость | **#316** — явная предпосылка cache-критерия и финального завершения #324 |
| Состояние issue | OPEN |
| Стадия | scope + architecture agreed in chat 2026-10-22; written design **APPROVED** by user 2026-10-22; implementation PLAN not yet written/approved |
| Письменный дизайн | **APPROVED** пользователем 2026-10-22 |
| План реализации | отсутствует (следующий шаг; не создан, не утверждён) |
| Разрешение на реализацию | отсутствует (design approval не авторизует реализацию) |
| Локальный артефакт дизайна | `docs/superpowers/specs/2026-10-22-correlated-apply-after-join-design.md` |
| Git | никаких commit/push/merge |

## 0. Как читать документ

- **«Согласовано»** — решение, явно зафиксированное пользователем в чате 2026-10-22; в рамках
  задачи не переоткрывается.
- **«Architect call»** — архитектурное решение автора документа там, где пользователь явного
  решения не давал; был предметом review и не подаётся как отдельное пользовательское
  согласование.
- **«Architect choice (внутренняя раскладка)»** — выбор внутренней реализации; builder может
  выбрать иную раскладку, не меняя наблюдаемого контракта (имена helper'ов, текст исключений,
  точный набор тестов, планирование регистрации).

## 1. Проблема

Коррелированный `CrossApply`/`OuterApply` не может следовать за join. Как только добавлен
join, левая строка — позиционный маркер `Projection<T1, T2, …>`, и builder бросает
`NotSupportedException` **до** генерации SQL (fail-closed на конструировании):

```
A correlated CROSS/OUTER APPLY source cannot reference a join projection;
apply it to a single-entity source instead.
```

Оба движка, для которых nextorm выражает эту форму нативно (`CROSS/OUTER APPLY` в SQL Server,
`CROSS JOIN LATERAL` в PostgreSQL/MySQL/MariaDB), умеют коррелировать lateral-источник с
**любой** предшествующей таблицей, включая joined. То есть это ограничение
композируемости builder'а, а не базы.

## 2. Цель

Разрешить коррелированный `CrossApply`/`OuterApply` **строго после join** (левая строка — уже
плоская проекция из **≥2 слотов**), сохранив:

- **плоскую** публичную проекцию: APPLY добавляет следующий слот, а не вложенную проекцию;
- согласованность позиционного (`ItemN`) и алиасного (`Alias.X`) API;
- per-row семантику (top-1, агрегат, TVF, коррелированный с колонками **обеих** joined-сторон),
  которую нельзя заменить переписыванием в обычный join + предикаты.

Границы: это задача **только про APPLY после join**. Голый `WithAlias(...)` на одном слоте
(один источник, без join) **не** является дополнительным требованием приёмки и не должен
получать изменений поведения. Существующий коррелированный APPLY на single-entity источнике
остаётся поддерживаемым как есть.

## 3. Решения пользователя (согласовано)

1. **Плоский вывод.** APPLY добавляет **следующий слот**. При двух существующих источниках
   `CrossApply`/`OuterApply` даёт `Projection<T1, T2, T3>`: результат читается как
   `q.Item1.Id` и `q.Item3.String`, **а не** `q.Item1.Item1`. Никаких вложенных проекций.
2. **Ёмкость: 8 источников всего, максимум 7 предшествующих + APPLY.** Пользователь явно
   утвердил этот потолок, а не «8 слева + 9-й APPLY». Уточнение после builder-check: считаются
   **все** слоты проекции/источника, включая безымянные позиционные, а не только именованные
   алиасы.
3. **Оба вида APPLY:** и коррелированный `CrossApply`, и коррелированный `OuterApply`.
4. **Обе формы лямбды:** возвращающие `EntityBuilder<T>` и `QueryCommand<T>`.
5. **После** `Join`/`LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin`; и **позиционный**, и
   **именованный алиасный** API.
6. **Набор поддерживаемых операторов у каждого провайдера — существующий.** #324 **не** добавляет
   нативный `FullJoin` там, где его нет.
7. **Существующие отказы сохраняются провайдер-уместно:** SQLite и ClickHouse без lateral
   (`SupportsApply == false`), in-memory без коррелированного APPLY.
8. **Никакой новой арности 9 и никаких вложенных проекций.**
9. **Обычный value-APPLY и CTE API сохраняются.**

## 4. Отклонённые подходы

- **Снять только guard.** Отклонён: без плоской конструкции и контекстной привязки join'ы
  теряются/привязываются неверно (левая строка перестаёт нести join-источники).
- **Вложенный результат** (APPLY поверх `Projection<…>` как вложенная проекция). Отклонён:
  нарушает согласованный плоский API и добавляет проблемы планировщику и связыванию вложенных
  проекций.
- **Ранняя строковая подстановка алиасов `ItemN => tN`.** Отклонён: смещения scope неверны
  (нумерация `tN` глобальна и может перемежаться рендером вложенных запросов).
- **Выбрано:** плоская коррелированная конструкция **плюс** контекстная привязка на
  существующей инфраструктуре источников/алиасов.

Привязка — это **внутренняя логическая ассоциация owner/slot**, а **не** новый реестр
физических таблиц и **не** сохраняемые строки `tN`; никакой таблицы метаданных проекции
(«projection metadata table») не вводится.

## 5. Конструкция

### 5.1 API-поверхность

Для каждого `JoinedEntityBuilder<T1..Tn>`, `n = 2..7`, добавить **4 коррелированные перегрузки**:

```csharp
JoinedEntityBuilder<T1..Tn, TNext> CrossApply<TNext>(Expression<Func<Projection<T1..Tn>, EntityBuilder<TNext>>> source, Action<JoinOptions>? options = null);
JoinedEntityBuilder<T1..Tn, TNext> OuterApply<TNext>(Expression<Func<Projection<T1..Tn>, EntityBuilder<TNext>>> source, Action<JoinOptions>? options = null);
JoinedEntityBuilder<T1..Tn, TNext> CrossApply<TNext>(Expression<Func<Projection<T1..Tn>, QueryCommand<TNext>>> source, Action<JoinOptions>? options = null);
JoinedEntityBuilder<T1..Tn, TNext> OuterApply<TNext>(Expression<Func<Projection<T1..Tn>, QueryCommand<TNext>>> source, Action<JoinOptions>? options = null);
```

Возврат — **плоская** арность `n+1`. Для `n = 2` это `JoinedEntityBuilder<T1, T2, TNext>`.

Скрытие inherited-перегрузок — обычными `new`-методами на производном `JoinedEntityBuilder` с
плоским возвратом (тот же приём, что уже используется в этих классах). Утверждение, что `new`
не может скрыть унаследованный метод с тем же списком параметров, но другим возвратом, **не
является верным как общее правило C#**: запрет относится к перегрузкам с одинаковой сигнатурой
в одном типе, а не к скрытию inherited-метода. Публичную сигнатуру базового класса **не менять**
и **не добавлять dummy-параметры**. План обязан включить compile-level API-binding тесты,
доказывающие, что унаследованная лямбда скрыта и что статический тип результата — плоский, для
обеих форм лямбды и для граничных арностей.

### 5.2 Общий seam

- Общий `protected`/`internal` seam, который: (a) валидирует и создаёт `JoinExpression` с
  `ApplySource`; (b) конструирует плоский результат и переносит состояние.
- **Не дублировать расходящиеся списки валидаций** между позиционным и алиасным путями:
  одна точка проверки арности, провайдерских ограничений и формы источника.
- Хранить `ApplySource`, **не компилируя и не вычисляя** пользовательскую лямбду на этапе
  конструирования.

### 5.3 Сохранение состояния

- Сохранять исходный физический `FROM`/`SourceFrom`, **упорядоченные** `Joins`, релевантные
  существующие модификаторы builder'а, source overrides и CTE, naming/options/hints/guards.
- Уважать существующие ограничения на неподдерживаемое состояние; **не обещать заново**
  filtered/windowed/eager/`JoinInto`.
- **Точная причина потери join'ов в базовом пути (не «любой конструктор теряет join'ы»):**
  `CreateJoined` создаёт `JoinedEntityBuilder<TEntity, TNext>(dataProvider, join)`, и этот
  конструктор инициализирует `_joins` как `[join]`; `ApplyJoinStateTo` **не** восстанавливает
  `Joins` получателя. `CloneImp` к этому пути отношения не имеет. Существующий плоский
  `JoinCore` в `JoinedEntityBuilder` явно копирует и дополняет `Joins` — новый seam обязан
  делать то же самое.
- Планирование обязано **инвентаризировать состояние против `ApplyJoinStateTo`** и переносить
  ровно то, что он переносит, плюс новые binding-метаданные.
- Базовый single-entity API (`EntityBuilder<T>`) остаётся поддерживаемым без изменений.

### 5.4 Арность 8 (fail-closed)

На точной плоской размерности **8** унаследованные от `EntityBuilder<Projection<T1..T8>>`
методы **не должны молча создавать вложенный девятый источник**. Допустимы два варианта
(architect choice): (a) существующий base guard уже корректно отказывает для точной
flat-размерности 8 — тогда дополнительные cap-перегрузки **не требуются**; (b) если base guard
не покрывает конкретную форму, добавить минимальные `new`-перегрузки, падающие понятным
исключением **до SQL**. Не добавлять лишние cap-overloads «на всякий случай» и не делать
широкий публичный редизайн join-cap. Инвариант один: **никогда** не получить молча девятый
вложенный источник.

### 5.5 Алиасный генератор

- Генератор **активен** (`src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs`, подключён как
  analyzer — см. `OutputItemType="Analyzer"` в csproj), **вопреки устаревшему утверждению в
  AGENTS**.
- Эмитить коррелированные alias-extension overloads для **обеих** форм лямбды и **обоих**
  видов APPLY через существующий seam-фабрику `JoinAlias`.
- Сохранять наследование **flattened** generated projection и идентичности **named slots**.
- Сгенерированные позиционные переходы **для after-join получателей** (`n = 2..7`) тоже нужны
  в обеих коррелированных формах и с учётом плоского потолка. **Голый alias-root** (один слот,
  без join) в этой задаче **не меняется** и остаётся вне scope.
- `MaxSlots` считает **все слоты проекции/источника, включая безымянные позиционные**, а не
  только именованные алиасы. Девятый alias-slot сохраняет **существующую** диагностику
  генератора.
- Именованный слот резолвится по `JoinSlotAttribute.Position`; `ItemN` и именованный алиас
  ссылаются на **один и тот же** слот.
- Сохранять существующие диагностики генератора (>8 слотов); не менять несвязанный публичный
  API генератора.

**Публичные сигнатуры пиннятся по существующему API** (схематические шаблоны генератора;
`S`, `Snext`, `Alias.NewSlotMarker`, `AliasJoin_Snext` — условные имена, а не обещанные
литеральные типы). Для каждого `n = 2..7`, `CrossApply`/`OuterApply` и обеих форм лямбды:

Рукописный позиционный получатель:

```csharp
public static AliasJoin_Snext<T1..Tn, TNext> CrossApply<TNext>(
    this JoinedEntityBuilder<T1..Tn> self,
    Expression<Func<Projection<T1..Tn>, EntityBuilder<TNext>>> source,
    Alias.NewSlotMarker marker);

public static AliasJoin_Snext<T1..Tn, TNext> CrossApply<TNext>(
    this JoinedEntityBuilder<T1..Tn> self,
    Expression<Func<Projection<T1..Tn>, QueryCommand<TNext>>> source,
    Alias.NewSlotMarker marker);
// OuterApply — аналогично
```

Получатель, уже несущий alias:

```csharp
public static AliasJoin_Snext<T1..Tn, TNext> CrossApply<TNext>(
    this AliasJoin_S<T1..Tn> self,
    Expression<Func<AliasProjection_S<T1..Tn>, EntityBuilder<TNext>>> source,
    Alias.NewSlotMarker marker);

public static AliasJoin_Snext<T1..Tn, TNext> CrossApply<TNext>(
    this AliasJoin_S<T1..Tn> self,
    Expression<Func<AliasProjection_S<T1..Tn>, QueryCommand<TNext>>> source,
    Alias.NewSlotMarker marker);
// OuterApply — аналогично
```

- Marker — **последний** параметр и называет **добавляемый** слот: он доступен как
  сгенерированное именованное свойство и как `Item(n+1)`; существующие имена и `ItemN`
  сохраняются.
- Возврат — следующая **плоская** generated-схема `AliasJoin` (арность `n+1`).
- **Никакого `Action<JoinOptions>`** в сгенерированном alias-extension API: текущие extensions
  его не имеют; поведение существующего core seam/options не меняется.

**Позиционные переходы на `AliasJoin`** — это **instance** `new`-методы (не extensions),
принимают ту же лямбду получателя `AliasProjection_S<T1..Tn>`, **без marker**, добавляют
**безымянный** `Item(n+1)` и сохраняют ранее именованные слоты; следуют существующей
позиционной сигнатуре options:

```csharp
public new AliasJoin_Snext<T1..Tn, TNext> CrossApply<TNext>(
    Expression<Func<AliasProjection_S<T1..Tn>, EntityBuilder<TNext>>> source,
    Action<JoinOptions> options = null);

public new AliasJoin_Snext<T1..Tn, TNext> CrossApply<TNext>(
    Expression<Func<AliasProjection_S<T1..Tn>, QueryCommand<TNext>>> source,
    Action<JoinOptions> options = null);
// OuterApply — аналогично
```

**Два API-binding примера (схематические имена).**

```csharp
// 1) рукописный JoinedEntityBuilder<T1,T2> + named marker
var q = joined.CrossApply(
    (Projection<T1, T2> p) => ctx.From<TNext>(),
    Alias.NewSlot);                    // Alias.<NewSlot>Marker, последний аргумент
// static: AliasJoin_<nextSuffix><T1, T2, TNext>;
// q.<NewSlot> и q.Item3 — один добавленный слот; q.Item1/q.Item2 не меняются
```

```csharp
// 2) AliasJoin_S<T1,T2> + positional transition (instance new, без marker)
var r = aliasJoined.CrossApply(
    (AliasProjection_S<T1, T2> p) => ctx.From<TNext>(),
    options: null);
// static: AliasJoin_<S>_P3<T1, T2, TNext>;
// r.Item3 — новый безымянный слот; ранее именованные слоты сохранены
```

### 5.6 Разные поверхности получателя

- Рукописный `JoinedEntityBuilder<T1..Tn>` наследует `EntityBuilder<Projection<T1..Tn>>`.
- Сгенерированный `AliasJoin_S<T1..Tn>` наследует `EntityBuilder<AliasProjection_S<T1..Tn>>`,
  **не** `JoinedEntityBuilder`.
- Поэтому hand-written `new` correlated методы **не обслуживают** `AliasJoin`: нужны **оба**
  набора — core-перегрузки на `JoinedEntityBuilder` и generated-перегрузки/transitions на
  `AliasJoin`.
- Compile-level API-binding тесты фиксируют **статический возврат** и доступность членов для
  каждой поверхности; никакой неопределённости достижимости.

## 6. Контракт контекстной привязки (architect call)

### 6.1 Граница привязки

- Новая binding-метаданные вводится **только** для projection outer refs, участвующих в этом
  коррелированном APPLY-пути (включая ancestor refs, если у них уже есть метаданные привязки).
- Если binding **помечен присутствующим**, он **авторитетен**: разрешение идёт по **точному**
  owner/slot; отсутствующая регистрация — исключение. **Никакого** legacy arithmetic/type
  fallback для связанного ref.
- Legacy **unbound** outer refs скалярных подзапросов сохраняют текущий путь. #324 **не**
  мигрирует широкую скалярную корреляцию.
- Существующие scalar-тесты — **регрессионная проверка**, а не «заморозка» ошибочного SQL:
  если новый сценарий вскрывает ошибку в существующем flat `Select`, затронутая **bound**-форма
  направляется через ту же карту; глобальный рерайт не выполняется.
- Обычный `Select` по плоскому выводу переиспользует существующую flat-обработку при условии
  **точной идентичности слота** в self-join и scoped regression-тестах. Общий логический смысл
  слота разделяется, но переписывать каждый projection translator не требуется.

### 6.2 Представление (primary architectural call)

Неизменяемая **per-preparation side table** outer-reference-binding, принадлежащая prepared
`QueryCommand`/root outer registry:

- индексируется **существующим reference index** `OuterRefMarker`;
- парна с исходным `OuterReferences` expression;
- entry: **логический owner occurrence token** + **one-based slot** + ссылка/ключ source
  occurrence слота;
- исходное выражение сохраняется для metadata translation и legacy-путей.
- Логический source occurrence = исходный `FROM` или **конкретный упорядоченный `Join`
  occurrence**, **не** `Type` сущности.
- `PrepareJoin` создаёт left-prefix owner binding для `applySource.Parameters[0]` и
  пробрасывает его через visitor outer stack.
- Один и тот же `ParameterExpression`, переиспользованный кэшированным выражением в разных
  command occurrence, **не сливается**: owner occurrence token + parameter identity + scoped
  visitor context.
- **Никаких render frame references** в этой неизменяемой метаданной.
- `CloneForPreparation`/переиспользование плана **обязаны** перестроить/перепривязать метаданные
  command occurrence; **не копировать слепо** runtime binding/alias indices.
- Structural slot topology выводится из структуры запроса; runtime token identity **не** входит
  в persistent key. `#316` equality остаётся структурным.

### 6.3 Инварианты

- **Идентичность `ParameterExpression`** внешней лямбды (не `param.Name` и не `Type`)
  определяет **левую строку конкретного вхождения запроса**.
- Логическая привязка принадлежит этому prepared-вхождению запроса и содержит:
  - параметр левой проекции;
  - упорядоченные видимые левые source-occurrence (слоты `1..N`);
  - типы сущностей по слотам.
- Каждый слот привязан к **точному вхождению** существующего `FROM`/`Join` occurrence;
  **повторяющиеся CLR-типы НЕ сливаются**.
- Хранить/пробрасывать ассоциацию рядом с `ApplySource`/метаданными outer-ссылок **без
  изменения публичного API**.
- Когда `PrepareJoin` делает `applySource.Parameters[0]`, привязывать **owner-specific левый
  префикс** — только `FROM` плюс **предшествующие** join'ы, **не** применённый источник, **не**
  текущий и **не** будущие join'ы.
- Каждый APPLY в цепочке имеет **свой** префикс; итоговая плоская строка может коррелировать
  **следующий** APPLY с предыдущим результатом.
- Параметр, захваченный из **действительно внешнего** scope, сохраняет свою исходную привязку.
- Доступ к свойству сущности по-прежнему использует существующие правила метаданных/конвертеров.
- Сохранять механизм индексов `OuterRefMarker` и root outer registry; **детектор остаётся
  cache-safety**, а не projection binding.

## 7. Рендер-лоуэринг

- **Render state отдельно от immutable metadata.** Расширять существующий render-local columns
  provider/context: `owner occurrence + slot -> exact registration index`. Это **не** отдельный
  реестр физических источников.
- `_paramSources` можно переиспользовать, только если его ключ действительно уникален для
  source occurrence. Слот **нельзя** идентифицировать угадыванием `type + occurrence`, и
  параметр одной проекции сам по себе **не различает N слотов**. Внутренняя owner-slot
  ассоциация авторитетна; type-fallback запрещён.
- Разрешать outer slot в **ТОЧНУЮ** регистрацию даже при одинаковых типах и вложенном source
  scope. Никакой глобальной арифметики `tN` и никакого предположения о непрерывности индексов
  (рендер вложенных запросов может перемежать регистрации).
- Frame stack **scoped** для вложенных команд; metadata owner token резолвится в **активный**
  frame. Привязки принадлежат render context/command occurrence и scoped-стекам, **никогда** —
  статическим разделяемым изменяемым полям.
- Подключаться в обычных точках регистрации источника/выделения алиаса; **порядок и количество
  алиасов не менять**; никаких лишних алиасов ради привязки.
- **Порядок рендера подтверждён builder-check:** `SqlBuilder` регистрирует `FROM`/`JOIN` **до**
  SELECT-списка; правый запрос APPLY транслируется после регистраций левого префикса, `Select`
  — после join'ов. Поэтому спекулятивный select-prepass **не требуется**. План всё равно
  проверяет остальные существующие формы рендерера; универсальный prepass не предписывается.
- Неразрешённая/forward/недоступная привязка — **fail closed** понятным исключением, **никогда**
  неверный алиас.
- Статические метаданные должны включать, **какому owner frame** принадлежит outer ref, а не
  только глобальный root index.
- Одно и то же отображение `owner/slot` **не должно расходиться** между projection `Select` и
  доступом через outer reference; **не** переписывать широко несвязанную скалярную корреляцию.

## 8. Кэш планов и зависимость #316

- **#316 — явная предпосылка приёмки cache-критерия и финального завершения #324.** PLAN #324
  обязан **сначала** проверить фактическое состояние #316. Если его существующий фикс
  `ApplySource` structural equality/hash отсутствует — запланировать/интегрировать его
  **отдельно**, не выдавая за реализованный и не создавая дублирующую альтернативную
  реализацию.
- **Владение:** #324 владеет owner/slot metadata/rebinding и регрессионными тестами; structural
  `ApplySource` equality/hash принадлежит **#316**. #324 можно планировать независимо, но
  **нельзя объявить accepted/final-complete**, пока совместный cache regression не пройдёт.
- Runtime-индексы привязки и идентичности `ParameterExpression` **не являются** входом
  persistent structural cache key.
- **Координировать с #316**: различать apply body, оператор, порядок, **форму slot binding** и
  форму лямбды там, где семантически разные запросы **не могут** делить неверный план.
- Структурно эквивалентные **новые** экземпляры `ParameterExpression` могут переиспользовать
  план; захваченные значения параметров меняют значения исполнения, а не запечённый SQL.
- При переиспользовании кэша восстанавливать **корректные outer-метаданные**.
- **Не мутировать** общий `QueryCommand.Cache` (sticky `_dontCache`); при необходимости —
  `storeInCache: false` в `GetPreparedQueryCommand`.
- **Никаких process-wide кэшей привязок.**

## 9. SQL по провайдерам

- **SQL Server:** `cross apply (…)` / `outer apply (…)`.
- **PostgreSQL/MySQL/MariaDB:** существующие lateral-формы диалекта. **НЕ** закреплять
  сомнительный `CROSS JOIN LATERAL … ON TRUE` из issue: корректный SQL — существующий рендерер
  диалекта/валидная грамматика, то есть `cross join lateral (…) alias` **без** `ON TRUE`, и
  `left join lateral (…) on true` для outer (см. `SqlDialectBase.MakeApply`).
- Использовать **обычные зарегистрированные алиасы**. Ожидаемый простой пример: внешние
  `t1`/`t2`/`t3`, применённый внутренний `t4`; но утверждения тестов должны **доказывать
  идентичность и при ненулевом scope**, а не полагаться на контигуальность.
- **EF Core — адаптер, а не диалект.** `NextORM.EntityFrameworkCore` мостит `DbContext` к
  существующему `IDataContext` через `EntityFrameworkCoreExtensions.CreateNextOrmContext`;
  отдельных EF-диалектов и EF-специфичных negative-требований нет, поведение адаптера не
  меняется.

## 10. Тесты

**Утверждения — только FluentAssertions** (правило репозитория).

### 10.1 SQL-генерация

Матрица исчерпывающа для dialect targets — **SQL Server / PostgreSQL / MySQL / MariaDB**
(EF Core — адаптер, не отдельный диалект; см. §9):

- cross и outer, для **каждой** формы лямбды (`EntityBuilder<T>`, `QueryCommand<T>`);
- позиционный **и** алиасный API;
- после **разрешённых** у данного провайдера join-типов;
- граница плоского вывода **total 3..8**;
- compile-level **API-binding** тесты: унаследованная лямбда скрыта, статический тип результата
  плоский, члены доступны — для обеих форм лямбды, граничных арностей (2, 7, 8) и **обеих
  поверхностей получателя** (§5.6);
- отказ при девятом источнике: позиционно (точная flat-размерность 8) и алиасно
  (существующая диагностика `MaxSlots`); `MaxSlots` считает и безымянные позиционные слоты;
- scalar single-entity и обычный value-APPLY **без изменений** (регрессия, не новое
  требование);
- голый `WithAlias` (один слот, без join) **не меняется** — только регрессия поведения;
- **повторяющиеся типы источников** (например, одинаковый `T` в левых слотах и в применённом);
- **вложенный ненулевой глобальный scope**; многоуровневая корреляция, если существующая
  композируемость это позволяет;
- APPLY→APPLY: предыдущий слот доступен, будущая привязка — нет.

### 10.2 Регрессии кэша и привязки

- Разные цепочки с одинаковым типом проекции — регрессионный тест кэша;
- переиспользование равной структуры с изменёнными capture'ами — под #316;
- self-join с повторяющимся типом и scoped/nested источником — точная идентичность слота;
- legacy unbound scalar-subquery outer refs — существующий путь не меняется.

### 10.3 Интеграционные (живые контейнеры)

- Только через существующие конвенции `ProviderTestSuite`/`CommonTestSuite`, на живых БД.
- Минимум по провайдеру: cross и outer со ссылками на **ОБЕ** левые стороны и **top-1
  ordered per-row**, доказывающие lateral-семантику; для outer — сохранение строки без
  совпадения с null-колонками; кейс повторяющегося типа; последующий APPLY, если провайдер
  поддерживает.
- **SQL-only не считается исполнением.**
- Неподдерживаемые провайдеры — **негативные** тесты; отказ SQLite — по причине **отсутствия
  lateral**, а не проекции.
- Полный интеграционный прогон — по инструкциям `running-integration-tests` (включая
  Podman-remediation); **никаких зелёных заявлений при skip провайдеров**.

### 10.4 Негативные / сохранение существующего

- SQL Server `CrossApply_OnJoinedProjection_ShouldThrowClearNotSupported`
  (`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs`) становится **позитивным**
  SQL-shape тестом.
- SQLite `CorrelatedApply_AfterJoin_ShouldBeRejected`
  (`tests/nextorm.integration.tests/SqliteBuilderGuardCoverageTests.cs`) остаётся **негативным**,
  но по причине **отсутствия lateral**, а не проекции.
- Прочие существующие legitimate unsupported-state guard'ы (filtered/windowed/eager/`JoinInto`,
  provider-уместные отказы) **сохраняются**.
- SQLite/ClickHouse и in-memory сохраняют provider-уместный отказ; существующие guard-тесты
  остаются осмысленными (см. §14).

## 11. Документация

- Три публичные страницы, перечисленные в issue: `docs/advanced/limitations.md`,
  `docs/guide/04-joins.md`, `docs/comparisons/index.md` — плюс их RU-зеркала
  (`docs/ru/advanced/limitations.md`, `docs/ru/guide/04-joins.md`,
  `docs/ru/comparisons/index.md`).
- Пример и лимиты (плоский потолок 8, provider-специфика), **без публичных ссылок на
  `docs/specs/**`**.

## 12. Критерии приёмки

- Коррелированный `CrossApply`/`OuterApply` **после позиционного join** (левая строка ≥2 слотов)
  рендерит SQL с outer-ссылками на **каждый** предшествующий табличный алиас (SQL Server
  `CROSS/OUTER APPLY`, PostgreSQL/MySQL/MariaDB `LATERAL`).
- Плоский вывод: APPLY добавляет следующий слот; при двух существующих — `Projection<T1,T2,T3>`
  (`q.Item1.Id`, `q.Item3.String`).
- Работает для плоской строки **total 3–8** (2–7 предшествующих + APPLY), для **обеих** форм
  лямбды и **обоих** API (позиционного и именованного алиасного); девятый источник **fail-closed
  до SQL** и никогда не даёт вложенный девятый источник.
- `ItemN` и именованный алиас ссылаются на один слот; `MaxSlots` учитывает все слоты, включая
  безымянные позиционные.
- Существующий коррелированный APPLY на single-entity источнике и голый `WithAlias` (один слот,
  без join) **не меняют поведение**; это не дополнительные требования #324.
- SQLite/ClickHouse и in-memory остаются fail-closed с provider-уместным сообщением.
- **Предпосылка #316:** #324 можно планировать независимо, но нельзя объявить accepted/final-
  complete, пока совместный cache regression не пройдёт; structural `ApplySource`
  equality/hash — собственность #316, owner/slot metadata/rebinding и regression-тесты — #324;
  дублирующая альтернативная реализация #316 не создаётся.
- SQL-тесты по провайдерам + живой интеграционный тест; docs EN + RU обновлены.
- **Никаких изменений вне шва #324/#316.**

**Авторизация:** commit/push/merge — нет.

## 13. Вне scope / риски

- Нативный `FullJoin` там, где его нет у провайдера, не добавляется.
- `JoinInto`/filtered/windowed/eager-состояния не получают новых обещаний.
- Голый alias-root (один слот, без join) и широкая скалярная корреляция остаются вне #324.
- **#316** не переписывается и не дублируется: #324 не владеет structural `ApplySource`
  equality/hash.
- **EF Core** не получает нового диалекта или EF-специфичных negative-требований; адаптер
  `EntityFrameworkCoreExtensions.CreateNextOrmContext` не меняется.
- Арность 9 и вложенные проекции не вводятся.
- **Порядок рендера подтверждён builder-check:** `SqlBuilder` регистрирует `FROM`/`JOIN` до
  SELECT-списка, поэтому спекулятивный select-prepass не нужен. Остаётся проверить в плане
  прочие существующие формы рендерера; универсальный prepass не предписывается.

## 14. Указатели на статические улики

Все ссылки — **снимок статических улик, не исполнялся**; номера строк **приблизительны**.
Пути — от `src/nextorm.core/…`, кроме пути генератора.

- `src/nextorm.core/Builders/EntityBuilder.cs:~3113` — guard в `JoinAliasApply`;
- `src/nextorm.core/Builders/EntityBuilder.cs:~3613` — guard в `JoinApply`;
- `src/nextorm.core/Builders/EntityBuilder.cs:~3668` — `CreateJoined` конструирует
  `JoinedEntityBuilder<TEntity, TNext>(dataProvider, join)`; конструктор
  (`src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs:20-24`) инициализирует `_joins`
  как `[join]`, а `ApplyJoinStateTo` **не** восстанавливает `Joins` получателя. `CloneImp` к
  этому пути отношения не имеет. Это **не** утверждение, что «любой конструктор теряет join'ы».
- `src/nextorm.core/Builders/EntityBuilder.cs:~3171-3173` — `CreateAliasJoined` сохраняет join'ы;
- `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs:261-283` — flat `JoinCore` явно
  копирует `Joins` и дополняет их новым join'ом;
- `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:~2057-2099` — `PrepareJoin`,
  `PushOuter(applySource.Parameters[0])`;
- `src/nextorm.core/ExpressionExtensions.cs:209-219` — outer marker хранит `p.ItemN`;
- `src/nextorm.core/Visitors/MemberTranslator.cs:~961-997` — hardcoded positional outer path;
- `src/nextorm.core/Visitors/AliasFromProjectionVisitor.cs:45-57`;
- `src/nextorm.core/Query/DefaultColumnsProvider.cs:30-65, 94-140` — scope и identity;
- `src/nextorm.core/DataContext/SqlSourceRenderer.cs:~683-693` — регистрация источника; ср. также
  `src/nextorm.core/DataContext/SqlBuilder.cs:~71, ~640` — scope и порядок: `FROM`/`JOIN`
  регистрируются до SELECT-списка (подтверждено builder-check; номера приблизительны);
- `src/nextorm.core/DataContext/ProjectionAliasCache.cs:101-146` — flat occurrence;
- `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs:1155-1176, 1325-1389` — генерируемые
  формы.

Пользовательский выбор **flat** означает: рекурсивное изменение `QueryPlanner.GetFrom` из-за
вложенного результата **не требуется**; менять планировщик только если подтверждён **фактический**
flat-баг.

## 15. Решения после builder-check (author choices, 2026-10-22)

Ниже — **исторический контекст** builder/review: это **author decisions**, а не новые
пользовательские согласования. Он не меняет текущий статус: письменный дизайн **APPROVED
пользователем 2026-10-22**; следующий шаг — **implementation PLAN**, план ещё не написан и не
утверждён; реализация **не авторизована**.

1. **Scope:** строго APPLY после join (`≥2` левых слотов), flat total `3..8` (2–7
   предшествующих + APPLY). Голый `WithAlias` (один слот, без join) не входит в приёмку и не
   меняет поведение. Existing single-entity correlated APPLY остаётся как есть.
2. **Арность/потолок:** `MaxSlots` считает все слоты, включая безымянные позиционные; девятый
   alias-slot сохраняет существующую диагностику; positional arity-8 обязан падать явно и
   никогда не давать вложенный девятый источник. Лишние cap-overloads не требуются, если base
   guard уже покрывает точную flat-размерность 8.
3. **Привязка:** per-preparation side table (indexed by `OuterRefMarker`) — primary
   representation; binding present => authoritative exact owner/slot; unbound legacy scalar
   outer refs не мигрируют. Render mapping — отдельное render-local состояние, не реестр
   физических источников.
4. **`new`-скрытие:** обычные `new`-методы на производном builder'е; базовую сигнатуру не
   менять, dummy-параметры не добавлять; план обязан доказать compile-level API-binding тестами.
5. **#316 — prerequisite:** #324 планируется независимо, но cache-критерий и final completion
   принимаются только после совместного regression; structural `ApplySource` equality/hash —
   #316, owner/slot metadata/rebinding — #324; без takeover/дублирования.
6. **Alias/EF Core:** generated alias API следует существующим сигнатурам (marker последний,
   options не добавляются); `JoinedEntityBuilder` и `AliasJoin_S` — разные поверхности, нужны
   оба набора; EF Core — только существующий адаптер, без отдельного диалекта.

## 16. Открытые вопросы

- Пользовательских открытых вопросов нет. Письменный дизайн **APPROVED** пользователем
  2026-10-22; следующий шаг — **implementation PLAN** (не написан/не утверждён); реализация
  **не авторизована**.
- Имена helper'ов, точный текст исключений и точный набор тестов — **architect choice** builder'а.
- Порядок `FROM`/`JOIN` до SELECT подтверждён builder-check; план проверяет остальные
  существующие формы рендерера. Спекулятивные переписывания источников не допускаются.
