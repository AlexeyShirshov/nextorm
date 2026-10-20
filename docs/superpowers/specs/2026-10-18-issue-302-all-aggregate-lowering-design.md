# #302 — `SelectWhereMax/Min` как SQL-side синтаксический сахар: общий query-tree lowering и агрегатный `All`

> **Статус:** **письменная спецификация утверждена USER 2026-10-18** (явный approval: «апрув»).
> Утверждена revised **SQL-only** редакция: in-memory вне области; native-пути PostgreSQL/ClickHouse
> сохраняются. Design scope согласован; **свежий независимый builder-check этой редакции выполнен**
> (одно уже решённое уточнение про `NULL` внесено без нового user question). Предыдущий builder-check
> относился к предыдущей редакции и не заменяет fresh check.
> **Разграничение гейтов:** design approval ≠ approval плана ≠ разрешение на исполнение. План
> реализации **не создан и не утверждён**; продуктовая реализация **не авторизована**. Следующий
> допустимый артефакт — **план реализации** (требует отдельного USER approval); исполнение плана
> требует отдельного явного разрешения.
> Issue [#302](https://github.com/AlexeyShirshov/nextorm/issues/302) — **URL проверен**, состояние
> **OPEN**, title не меняется, body синхронизирован этой редакцией (SQL-side sugar, общий lowering,
> явные in-memory-исключения, актуальное состояние гейтов). Milestone — **`1.0.9-rc5`** (проверен
> и сохраняется; новый milestone не создаётся). Новый issue не создаётся: это та же согласованная
> задача, расширенная до SQL-only архитектуры.
> Commit/push/merge не авторизованы.
>
> **Спецификация:** `docs/superpowers/specs/2026-10-18-issue-302-all-aggregate-lowering-design.md`.
> **Связанные:** [#144](https://github.com/AlexeyShirshov/nextorm/issues/144) — native `One` для
> PostgreSQL/ClickHouse (closed); в части window-portability `All` **заменяется этой задачей**
> (§1.4); [#115](https://github.com/AlexeyShirshov/nextorm/issues/115) — исходный публичный API
> (closed).

| Поле | Значение |
|---|---|
| Задача | [#302](https://github.com/AlexeyShirshov/nextorm/issues/302) |
| Milestone | `1.0.9-rc5` (проверен в GitHub; сохраняется, новый не создаётся) |
| Состояние issue | OPEN, enhancement; assignee нет |
| Стадия | written spec **approved USER 2026-10-18** («апрув»): revised SQL-only, in-memory out of scope; план не создан |
| Ревизия | architecture scope update: SQL-side sugar, common query-tree lowering, in-memory out of scope |
| План | отсутствует (следующий допустимый артефакт; требует отдельного USER approval) |
| Разрешение на реализацию | отсутствует (design approval ≠ implementation authorization) |
| Approval спеки | USER, 2026-10-18, явное «апрув» (revised SQL-only редакция) |
| Git | никаких commit/push/merge в этой итерации |

## 0. Как читать документ

- **«Согласовано пользователем»** — явно зафиксированное в обсуждении решение; оно не открыто для
  повторного обсуждения в рамках этой задачи. Сюда относятся: прежнее обязательное правило
  агрегатного `All`, расширение архитектурного scope (SQL-side syntactic sugar), явное исключение
  in-memory и требование сохранить native-пути PostgreSQL/ClickHouse.
- **«Architect scope/safety choice»** — выбранная архитектором граница безопасности внутри уже
  согласованного scope (§5.2: сохранение logical command/cache identity, допустимая фаза lowering).
  Это **не** цитата пользовательского решения и **не** отдельный user approval.
- **«Architect guidance»** — предложенная архитектором конструкция внутри согласованной
  семантики. Это не выбранный пользователем синтаксис.
- **«Builder freedom»** — внутренняя свобода реализации, не меняющая наблюдаемый контракт
  (в частности, точное внутреннее представление query-tree).
- **«Evidence»** — измеренный или проверенный факт с указателем на артефакт. Evidence не
  заменяет acceptance gate и не объявляет непроверенное поведение поддержанным.

## 1. Контекст

### 1.1 Текущее поведение

`SelectWhereMax` / `SelectWhereMin` возвращает строку(и) с экстремальным значением селектора —
глобально или per-group. Сегодня у операции две стратегии:

1. **Переносимое понижение** — производная таблица с оконной функцией `row_number()` для
   `ExtremeRowTies.One` и `rank()` для `ExtremeRowTies.All`, фильтр `rn = 1`. Точка входа —
   `MakeExtremeRowSelect` (`src/nextorm.core/DataContext/SqlBuilder.cs:941-975`) →
   `MakePortableExtremeRowSelect` (`:981-1026`); ранжирование —
   `MakeExtremeRowRankExpression` (`:1415-1464`).
2. **Необязательная native-стратегия** — capability `ISqlDialect.ExtremeRowRenderer`.
   Проверенный факт: native-диспатч выполняется **только для `One`**
   (`SqlBuilder.cs:948-972`, комментарий `:948-952`); renderer'ы реализованы у PostgreSQL
   (`src/nextorm.postgres/PostgresExtremeRowRenderer.cs:22`) и ClickHouse
   (`src/nextorm.clickhouse/ClickHouseExtremeRowRenderer.cs:43`). **Нативного `All` сегодня нет
   ни у одного провайдера.** Поэтому `All` на всех SQL-провайдерах идёт через `rank()`-окно.

Флаг `SupportsSelectWhereMinMax` означает поддержку **переносимой** фичи и равен `true` у всех
шести диалектов (SQLite, SQL Server, PostgreSQL, MySQL, MariaDB, ClickHouse). Отсутствие
нативного `All` — это не отсутствие поддержки `SelectWhereMax/Min`; `All` поддержан переносимо.

Структурно сегодняшнее понижение — **отдельный параллельный SELECT-renderer**: команда с
`ExtremeRow` рано уходит из общего пути (`SqlBuilder.cs:46-49`), а `MakeExtremeRowOuter`
(`:1267-1407`) самостоятельно собирает projection, `DISTINCT`, ordering и parameter traversal
вместо переиспользования общих примитивов. Именно этот параллельный конвейер настоящая задача
устраняет (§2, §5).

### 1.2 Решения пользователя

Прежнее обязательное правило (не меняется):

> «в режиме All нужно использовать группировку вместо оконок там, где нет нативного пути».

Это **безусловная стратегия** для поддерживаемых форм. Она не зависит от индексов, оценок
оптимизатора, универсального порога скорости или отдельного performance-approval. Предложение
оставить grouped `All` на оконном понижении из-за неблагоприятных индексных планов
пользователем **отклонено**. К этому вопросу в рамках задачи не возвращаемся.

Согласованное расширение архитектурного scope:

- `SelectWhereMax` / `SelectWhereMin` — это **SQL-side синтаксический сахар**: пользовательская
  форма задаёт логическую семантику, а SQL-стратегия понижения строится внутри обычного
  query-конвейера.
- `ExtremeRow` остаётся **логическим описанием** до момента выбора SQL-стратегии; отдельного
  исполняемого SQL-конвейера вокруг него быть не должно.
- **In-memory явно вне области** этой задачи (§7).
- **Native-пути PostgreSQL/ClickHouse должны быть сохранены** (пользователь подтвердил).

### 1.3 Границы согласованного scope

Задача **не** добавляет native `All` renderer'ы, **не** меняет публичный capability-контракт и
**не** расширяет query shapes. `One` сохраняет наблюдаемый SQL/поведение; допустимы только
внутренние рефакторинги, не меняющие SQL. In-memory не является частью объёма.

### 1.4 Отношение к #144: window-portability `All` заменена

#144 зафиксировал, что `All` остаётся на существующем portable window lowering (и что native `All`
в той задаче не добавляется). В части **требования, что `All` должен оставаться
window-переносимым**, #302 это требование **заменяет (supersedes)**: для `All` оконное понижение
больше не требуется и не используется, актуальна агрегатная стратегия из этой спецификации.
Остальные решения #144 — семантика `One`, правило ties по полному селектору, `NULL`-контракт,
отсутствие native `All` renderer'а в текущем объёме — не пересматриваются. Native `One`
PostgreSQL/ClickHouse сохраняется в неизменном виде.

Исторические артефакты #144 — `docs/specs/design/issue-144-native-extreme-row.md` (включая его
таблицу стратегий и non-goal «нативный `All`»), issue/PR-текст и комментарии прошлого цикла —
остаются **историческими evidence** и **не переписываются**. Ретроактивных правок этих артефактов
нет; актуальным источником для `All` является только эта спецификация. Замена фиксируется здесь,
а не молчаливым изменением истории.

## 2. Центральное требование: SQL-side syntactic sugar

`SelectWhereMax/Min` не является отдельным видом запроса на уровне SQL-рендеринга. Это
синтаксический сахар, который на стадии выбора SQL-стратегии понижается в **обычный
query-tree/derived source**, после чего результат собирается **общей** инфраструктурой
построения SQL.

Обязательные следствия:

1. **Стратегии понижения:**
   - portable `All` — агрегат + join-back/predicate (§5.5);
   - portable `One` — существующая оконная стратегия (без изменений);
   - native `One` PostgreSQL/ClickHouse и внешний `IExtremeRowRenderer` — сохраняются.
2. **Общая инфраструктура.** Результирующий SQL обязан использовать общие примитивы движка для:
   source/`FROM`, projection, filter, alias, `DISTINCT`, sorting и parameter rendering. Отдельный
   полный `MakeExtremeRowOuter`-рендерер, дублирующий эти примитивы, не допускается.
3. **Запрет «переезда» дублирования.** Недостаточно перенести существующую сборку SQL-строк в
   новый `ExtremeRow`-builder-класс: дублирующий outer-SELECT конвейер должен быть **устранён**, а
   не переименован.
4. **Специализированный lowering разрешён и необходим.** Преобразование логической
   semantic-формы (`ExtremeRowTies`, selector, group key) в query-tree — отдельная
   специализированная стадия; это не дублирование рендеринга.
5. **Native-адаптер разрешён.** Публичный `IExtremeRowRenderer.Render` возвращает строку, поэтому
   небольшой адаптер, поставляющий opaque source result в общий outer projection, допустим.
   Публичный контракт не меняется. Если для этого нужен общий внутренний механизм «произвольный
   source result → общая projection/alias/sorting инфраструктура», его точное внутреннее
   представление выбирает план. Документ **не** утверждает, что такой готовый адаптер уже
   существует или проверен.
6. **Один statement, без client-side materialization.** Число запросов, наблюдаемое вызывающей
   стороной, не меняется.

## 3. Область

### 3.1 В области

- Все SQL-провайдеры: SQLite, SQL Server, PostgreSQL, MySQL, MariaDB, ClickHouse.
- `ExtremeRowTies.All`, grouped и global, `SelectWhereMax` и `SelectWhereMin`.
- Скалярный селектор и составной value key.
- Агрегатное понижение: grouped — `GROUP BY` + join-back; global — скалярный агрегат +
  join-back/predicate.
- **Необходимая общая поддержка построения query-tree:** hidden aliases, pass-through columns,
  derived source. Это внутренняя инфраструктура движка, а не новый публичный API.
- Сохранение текущей семантики, eligibility, guard-набора, projection/mapping, outer `DISTINCT`
  и output ordering.
- SQL-generation тесты по всем шести семействам диалектов и live integration там, где для
  провайдера существует рабочий harness.
- Performance evidence как supporting-материал (не acceptance gate).
- Публичная документация EN+RU: `All` описывается как агрегатное понижение через общий
  query-lowering с сохранённым native-hook; `One` остаётся без изменения семантики; без ссылок на
  внутренние спецификации (обязательный deliverable — §12).

**Что НЕ является целью:** удалить каждое упоминание `ExtremeRow`, переписать plan-cache model или
удалить clause из публичного builder'а/внутреннего логического `QueryCommand`. Логическое
состояние, необходимое для unchanged in-memory, может оставаться в общих preparation/cloning/cache
структурах. Цель — устранить **избыточный исполняемый SQL-конвейер** и использовать общие
примитивы движка.

### 3.2 Вне области

- **In-memory провайдер** — полностью вне области (§7).
- `ExtremeRowTies.One`: SQL и выбранная стратегия остаются без изменений; допустимы только
  внутренние рефакторинги, не меняющие SQL и поведение.
- Новые native `All` renderer'ы.
- API tie-break, новые публичные сигнатуры, расширение query shapes.
- Переписывание общего normalization/optimizer (LINQ roadmap) и рефакторинг других
  синтаксических фич (включая `PerParentPaging`, который **не требуется** трогать).
- Создание схемы/индексов, навязывание имён индексов, runtime-инспекция схемы или переключение
  стратегии по плану запроса.
- Гарантия универсального ускорения.

## 4. Семантические инварианты

Инварианты наследуются из `docs/specs/design/issue-144-native-extreme-row.md:22-30` и не
пересматриваются:

- `All` возвращает **все** tied winner-строки, и каждая строка — **полная исходная строка**
  (payload не собирается из произвольных агрегатов).
- Ties определяются **только** полным селектором; пользовательский `OrderBy` влияет только на
  выдачу после выбора победителей, а не на сам выбор. API tie-break нет.
- Дубликаты исходных строк сохраняются в рамках существующей семантики внешнего `DISTINCT`.
- Поддерживаются `Max` и `Min`.
- Строки, у которых **любой** компонент value key равен `NULL`, исключаются — **до** агрегата и
  **до** join-back.
- Nullable-ключи групп поддерживаются; равенство групп при join-back — null-safe **по каждому
  компоненту** group key.
- Null-safe равенство группы выражается **обычным OR** по каждому компоненту:
  `(a = b OR (a IS NULL AND b IS NULL))`. Провайдерно-специфичный синтаксис (например, native
  null-safe оператор) — необязательная оптимизация, а не требование; отсутствие такого оператора
  не является блокером.
- Группа опускается **только** если после исключения строк с `NULL` хотя бы в одном компоненте
  value-selector в ней не осталось допустимых строк. `NULL` в group-selector не исключает ни
  строку, ни группу; в том числе полностью `NULL` составной ключ группы образует допустимую
  группу. Пустой вход опускается; global-форма на пустом входе возвращает ноль строк.
- Пользовательские фильтры применяются к **одной и той же** eligible-популяции и для вычисления
  экстремума, и для payload-строк; фильтр обязан присутствовать на обеих сторонах одинаково.
  Недопустимо, чтобы winner-строка была отфильтрована только с одной стороны.
- Составной value key сравнивается **лексикографически** (поэтапные агрегаты, §5.5.4). Это
  никогда не независимые `MAX(a)`, `MAX(b)` или `MIN(a)`, `MIN(b)`.
- Составные group keys сравниваются null-safe по каждому компоненту.
- Сохраняются projection, mapping, outer `DISTINCT`, output ordering и текущий набор query guards.
- Поддерживаемые query shapes не расширяются; ранее принятые selector-типы/формы не начинают
  отвергаться. Новые ограничения на runtime-типы или селекторы не вводятся.
- Source mapping и converter-семантика провайдеров (MySQL, MariaDB и остальных) не меняются.
- Window-lowering **не** используется как fallback для `All` (§5.5.5).
- Валидация пользовательского запроса выполняется **до** lowering и остаётся отдельной;
  сгенерированные внутренние join/grouping/window-конструкции не легализуют запрещённые
  пользовательские формы и не должны вызывать не относящиеся к ним `ExtremeRow` guards (§5.4).

## 5. Архитектура понижения

### 5.1 Общий query-tree вместо параллельного SELECT-рендерера

Lowering `SelectWhereMax/Min` строит логическое/деревянное описание результата (derived source,
join/aggregate, projection, filters, aliases, ordering, `DISTINCT`), а рендеринг SQL и параметров
выполняет **общий** путь движка. Источник, projection, фильтры, алиасы, `DISTINCT`, сортировка и
parameter traversal не переписываются второй раз внутри `ExtremeRow`-кода.

Допустимая специализация — только semantic-to-query lowering (какой именно query-tree строится для
`All`/`One`, grouped/global, scalar/composite) и адаптер для строкового native-renderer'а. Всё
остальное — общие примитивы.

### 5.2 Фаза понижения и identity — architect scope/safety choice

> Это **architect scope/safety choice**, а не цитата пользовательского решения. Он ограничивает
> способ реализации внутри согласованного SQL-only scope.

- Сохраняются logical command/cache identity и общая clause/preparation/equality/clone
  bookkeeping, если их изменение не является **строго необходимым** для SQL lowering. Нельзя
  удалять shared state, нужное неизменному in-memory.
- Допустимо понижать **detached SQL tree после формирования логического ключа**, при условиях:
  исходная keyed-команда не мутируется, а для SQL- и parameter-traversal'ов и для cache-hit'ов
  используется **идентичная детерминированная** lowered-структура.
- Нормализатор **до** формирования ключа (before-key normalizer) **не обязателен** ради этого
  SQL-only рефакторинга. Если реализация всё же выбирает такую фазу, она обязана обработать
  raw-shape preparation recipes, cache identity, clone state и hash invalidation **до** ключа.
- План обязан выбрать и документировать точную фазу и владельца lowering. Это implementation
  latitude внутри инвариантов, а не неопределённость пользовательского поведения.

### 5.3 Сохранение native-hook

- Публичный интерфейс `IExtremeRowRenderer`, DTO-конструкторы и их ленивость сохраняются.
- `CanRender` остаётся side-effect-free проверкой eligibility и вызывается **до** любых
  SQL/alias/parameter-мутаций. Отрицательный ответ означает fallback на build-strategy time;
  исключение renderer'а пробрасывается и **не** приводит к повторной попытке/late fallback.
- Native-renderer возвращает строку. Нельзя делать вид, что «AST-only» подход устраняет этот
  адаптер. Адаптер может передать opaque source result в общий outer projection, если для этого
  нужен общий внутренний механизм; точное представление — за планом.
- Native `One` PostgreSQL/ClickHouse сохраняется: существующее требование byte-identical SQL для
  `One` остаётся в силе (§5.6).

### 5.4 Валидация и guards

- Guards и compatibility-проверки применяются к **исходному пользовательскому запросу** до
  lowering, в существующем порядке.
- Внутренние join/grouping/window-узлы, созданные lowering'ом, не проходят пользовательские
  `ExtremeRow` guards и не должны отвергаться ими; при этом они **не** делают допустимой ни одну
  пользовательскую форму, которая была запрещена.
- Набор guard-отказов для пользовательских запросов не меняется.

### 5.5 Концептуальная SQL-конструкция

> **Architect guidance, не user-selected syntax.** Ниже — одна корректная агрегатная
> конструкция. Builder может выбрать эквивалентную provider-специфичную SQL без ranking window
> и может оптимизировать scalar и composite раздельно.

#### 5.5.1 Eligible source `S`

Логически фильтрованный источник `S`: исходный `FROM` с пользовательским фильтром и
`IS NOT NULL` по каждому компоненту value key. Одна и та же популяция `S` используется и для
агрегата, и для join-back, чтобы не получить winner, отфильтрованный только с одной стороны.

#### 5.5.2 Global scalar

Для скалярного value key: `E = MAX(value)` или `MIN(value)` по `S`; join-back `S` к `E` по
равенству value. На пустом `S` агрегат даёт `NULL`, и winners не возвращаются. Допустима
эквивалентная форма с предикатом `value = (SELECT MAX(value) ...)`.

#### 5.5.3 Grouped scalar

`E = group key + MAX/MIN(value) GROUP BY group key`; join-back `S` к `E` по null-safe равенству
group key (по каждому компоненту) и по value-сравнению, согласованному с ranking/ties
провайдера. Nullable-группы сохраняются; `NULL` в group-selector, включая полностью `NULL`
составной ключ, группу не исключает. Группа без допустимых value-строк (после исключения строк
с `NULL` хотя бы в одном компоненте value-selector) не попадает в результат, потому что в `S`
для неё нет строк.

#### 5.5.4 Составной value key

Лексикографический порядок реализуется **поэтапным агрегатным ограничением**:

1. агрегировать первый компонент в исходных группах (глобально, если group key нет);
2. сохранить строки, tied по лучшему префиксу;
3. агрегировать следующий компонент внутри выжившей популяции той же группы;
4. повторять до последнего компонента;
5. вернуть только финальные winner-строки источника.

Это гарантирует лексикографический выбор: `(9,2)` побеждает `(8,100)`, а не появляется
несуществующая комбинация `(9,100)`. Чистый `NOT EXISTS`-semijoin не является заменой
требуемому агрегатному понижению; допустима другая **семантически эквивалентная агрегатная**
конструкция.

#### 5.5.5 Факторинг и провайдерные адаптации

- Внутренние derived tables/CTE допустимы в пределах возможностей провайдера. `MATERIALIZED`
  CTE не предписывается; поддержка CTE не становится требованием к публичной форме запроса.
- Никакого создания схемы/индексов, навязывания имён индексов, runtime-инспекции схемы или
  переключения стратегии по оценкам/плану.
- Оконное понижение **не** используется как fallback для `All`. Окно остаётся стратегией `One`
  там, где нет подходящего native-рендера.
- SQL-синтаксис и диалектные адаптации — зона builder/plan. Общая core-семантика не должна
  дублироваться по имени провайдера.
- Существующий capability-based дизайн и порядок guards сохраняются.
- Публичный `IExtremeRowRenderer` не меняется «попутно». Если планирование обнаружит blocker,
  требующий публичного изменения, это отдельный отчёт на approval, а не молчаливое расширение
  scope.

### 5.6 `One` не меняется

SQL и выбранная стратегия `One` сохраняются byte-identical (в пределах существующего
требования). Native `One`-рендеры PostgreSQL/ClickHouse и их cache-инварианты не затрагиваются.
Существующие fragment-тесты `One` недостаточны как исторический golden: перед изменением
снимается baseline полного SQL, после — сравнивается с ним (§11.3).

## 6. Типовой паритет

- Все текущие допустимые key/group типы, converted keys, nullable keys и arities составных
  ключей остаются допустимыми по существующему контракту.
- Сохраняются bound SQL type semantics, collation equality, поведение floating-point
  `NaN`/non-finite там, где оно сейчас поддержано.
- Несовместимости вроде `MIN`/`MAX` над SQL Server `bit`/`bool` решаются
  provider-appropriate агрегатными или order-preserving представлениями, а не сменой контракта.
- Никакого нового публичного API, никакого window-fallback и никакого молчаливого ограничения
  типов/селекторов как workaround.
- Source mapping и converter-семантика MySQL/MariaDB и остальных провайдеров не меняются.
- Точные адаптеры и SQL рассматриваются в плане; parity-тесты обязательны. Невозможность
  обеспечить паритет — **planning blocker**, а не разрешение изменить требование.

## 7. In-memory — явно вне области

In-memory провайдер **не входит** в объём #302. Конкретно запрещается:

- удалять или переписывать `InMemoryExtremeRow`;
- добавлять in-memory поддержку subquery join, window, group-row selection;
- маршрутизировать in-memory через новый SQL query-tree;
- менять source-order winner, null-семантику, guards, async-ограничения или сложность исполнения;
- пытаться исправить обнаруженное поведение in-memory с right-side derived join в рамках #302.

Общие логические объекты (`QueryCommand.ExtremeRow`, clause state, preparation/cloning) обязаны
оставаться совместимыми с in-memory. Существующий in-memory unit suite прогоняется **только как
regression check** из-за общего кода; это не implementation deliverable и не расширяет scope.
Любое обнаруженное in-memory-поведение (включая right-side derived join) фиксируется отдельно и
не чинится здесь.

## 8. Кэш, детерминизм и совместимость

Наследуется из #144:

- Стратегия детерминирована формой запроса и провайдером.
- Нет mutable per-context переключателей и per-instance toggles.
- Отсутствие capability не равно неподдержанной фиче.
- Guards выполняются до диспатча.
- Запрещено мутировать sticky `QueryCommand.Cache`; после decline renderer'а cache остаётся
  включённым.
- Число запросов, наблюдаемое вызывающей стороной, не меняется; нет client-side materialization;
  выполняется один DB statement.
- Существующие cache/plan-cache инварианты сохраняются.

Дополнительно для SQL lowering:

- SQL- и parameter-traversal используют **одну и ту же** детерминированную lowered-структуру;
  cache-hit не пересобирает её иначе.
- Одинаковая логическая форма с разными bound values переиспользует план и подставляет **текущие**
  значения; другая форма `Where` не должна приводить к ошибочному sharing.
- Raw-shape cold/hit и повторные prepared/fresh запросы сохраняют существующее поведение.
- Поведение внешнего renderer'а на cache path сохраняется и **пинуется тестами как существующее
  intended behavior**. Нельзя предполагать, что callbacks никогда не повторяются во время
  parameter extraction; тест должен зафиксировать фактический существующий контракт, а не
  изобретать новый.

## 9. Grounded discovery

Проверенные точки кода (ингредиенты, а не доказательство готового сквозного прототипа):

- `src/nextorm.core/DataContext/SqlBuilder.cs:46-49` — ранний специальный диспатч команды с
  `ExtremeRow` в отдельный путь.
- `SqlBuilder.cs:1267-1407` — `MakeExtremeRowOuter` + `AppendOrderBy`: дублирующие
  projection/`DISTINCT`/ordering/parameter traversal.
- `src/nextorm.core/Expressions/FromExpression.cs:48/106` — derived-table source
  (`FromExpression(QueryCommand)` / `SubQuery`).
- `src/nextorm.core/DataContext/SqlSourceRenderer.cs:559-593` — общая поддержка derived source
  (subquery rendering + `ColumnsProvider` scope).
- `src/nextorm.core/Expressions/SelectExpression.cs:24,26,109,117,126,133,140` — projection
  identity (`PropertyName`/`Expression`) и hidden/pass-through metadata (`PhysicalColumnName`,
  `ProjectionItem`, `OutputName`, `IsProjectionOutputReference`, конструктор).
- `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:1515-1544,1579-1598` —
  `DerivedPassThroughColumn` и `ProjectionOutputColumn`: pass-through/derived-output columns.
- `src/nextorm.core/Query/SqlFunctions.cs:101` — `SqlFunctions.Column<T>` (raw column by name);
  `src/nextorm.core/Visitors/NormSqlTranslator.cs:186-199` — рендеринг как ссылки на output alias.
- `src/nextorm.core/DataContext/QueryPlanner.cs:609-656` — raw-shape recipe до preparation;
  `:692-700` — логический ключ до SQL; `:743-745` — сбор SQL и параметров;
  `:887-909` — извлечение параметров на cache-hit.
- `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs:478/487` — публичные native
  `CanRender` / `Render`.

Ограничения evidence: перечисленное демонстрирует наличие ингредиентов общего query-tree и
точек интеграции, но **не** является успешным end-to-end lowered-прототипом. Документ **не**
утверждает, что построение всего SQL AST уже проверено или протестировано. Общая null-safe
OR-форма уже поддержана; отсутствие отдельного native null-safe оператора **не** является
блокером. Ни одна часть этой спецификации не объявляет непроверенную конструкцию реализованной.

## 10. Performance evidence

> **Supporting, не acceptance gate.** Артефакты — временные и **не** являются durable tracked
> документацией репозитория.

Evidence:

- `/tmp/opencode/issue-302-all-spike/report.md` — SQLite `3.37.2`, raw SQL, 200k строк.
- `/tmp/opencode/issue-302-all-spike-modern/report.md` — SQLite `3.53.4`, .NET 10,
  `Microsoft.Data.Sqlite`, raw SQL, 200k строк.

Наблюдения:

- Scalar grouped без индекса: агрегатная форма примерно **1.6–2x** быстрее rank
  (в отчёте `1.57–2.02`).
- Grouped с подходящим составным индексом: кратный выигрыш (в старом отчёте `7–29x`).
- На modern SQLite при индексе только по value (`ix(v)`) исходный join-back уходит за **20 s**
  guard (abort), тогда как rank — **0.64 s**.
- `MATERIALIZED`-кандидат убирает abort, но в 30 состояниях медленнее rank на **12–81%**
  (ratios `1.12–1.81`). Это одна исследованная SQL-форма и не доказательство, что агрегатное
  понижение в принципе неверно.
- Composite performance **не измерялся**. Composite `NOT EXISTS` fixture проверял корректность
  semijoin, а не реализацию агрегатного понижения.
- Не утверждается, что все провайдеры измерены. Modern-отчёт снят на актуальном .NET runtime.

Пользователь осведомлён об этих результатах и всё равно требует агрегатный `All`. Универсальное
ускорение не гарантируется; порог, при котором нужно оставить окно, не вводится. Регрессии
нельзя маскировать — они должны быть явно показаны в отчётах. Выбор агрегатной стратегии не
зависит от процентов ускорения.

## 11. Приёмка

### 11.1 Архитектура

- Architecture review подтверждает: результат использует существующий общий render
  (source/projection/filter/alias/`DISTINCT`/sorting/parameters); параллельного outer-SELECT
  конвейера нет.
- Дублирование не «переехало» в новый `ExtremeRow`-builder: `MakeExtremeRowOuter`-подобной
  независимой сборки projection/`DISTINCT`/ordering/parameter traversal не остаётся.

### 11.2 SQL-generation и normalized tree

- Все шесть семейств диалектов: SQLite, SQL Server, PostgreSQL, MySQL, MariaDB, ClickHouse.
- `Max` и `Min`; grouped и global; скалярный и составной value key.
- Контракты `NULL`, составных group keys, фильтров (одинаково на обеих сторонах), projection,
  output ordering и dedup.
- Для `All` без native `All` assert'ится агрегатное понижение и **отсутствие**
  `rank()`/`row_number()` lowering.
- Нормализованное дерево/алиасы: hidden alias collision, mapped physical columns,
  DTO/anonymous/record projection, nullable payload, output sorting, `DISTINCT`.
- Guards исходного пользовательского запроса срабатывают **до** внутреннего lowering; внутренние
  узлы не вызывают не относящиеся к ним `ExtremeRow` guards.
- Тесты и SQL для `One` сохраняются без изменений.

### 11.3 `One`, native и внешний renderer

- **Baseline SQL capture до/после** для `One`: полный SQL фиксируется перед изменением и
  сравнивается после (существующие fragment-тесты недостаточны как исторический golden).
- Native `One` PostgreSQL/ClickHouse и forced-portable `One` дают эквивалентный результат.
- Внешний custom renderer: `CanRender` accept/decline/throw; decline даёт fallback на
  build-strategy time; throw пробрасывается без retry; ленивость payload/DTO не нарушена.

### 11.4 Кэш и параметры

- Одинаковая логическая форма с разными bound values переиспользует план с **текущими**
  значениями.
- Другая форма `Where` не приводит к ошибочному sharing.
- Raw-shape cold/hit, повторные prepared/fresh запросы — без регрессий.
- После decline renderer'а cache остаётся включённым; sticky `QueryCommand.Cache` не мутируется.
- Параметры детерминированы и в SQL, и в extraction mode.
- Custom renderer на cache path ведёт себя согласно существующему intended behavior (тест
  пинует фактическое поведение; не предполагается, что callbacks не повторяются при извлечении
  параметров).

### 11.5 Live integration

- Реальный SQLite.
- Container-backed PostgreSQL, SQL Server, MySQL, ClickHouse.
- MariaDB: SQL-generation плюс live в рамках существующей доступности harness.
- Запуск по действующим инструкциям репозитория (`.opencode/skills/running-integration-tests`).
- Unit/SQL-generation тесты **не** заменяют live integration; SQL-generation обязателен по всем
  шести семействам.

### 11.6 Correctness matrix

Независимые ожидаемые winners на нескольких провайдерах:

- несколько ties;
- целостность payload всей строки;
- сохранение дубликатов;
- `NULL` в любом компоненте value key;
- nullable группы (сохраняются; `NULL` в group-selector, включая полностью `NULL` составной
  ключ, группу не исключает), группы без допустимых value-строк после исключения `NULL` хотя бы
  в одном компоненте value-selector (опускаются), пустой вход;
- фильтры, удаляющие исходных winners, — одна и та же популяция с обеих сторон;
- составной лексикографический counterexample `(9,2)` vs `(8,100)`;
- converted keys, включая SQL Server `bool`, и релевантные floating-point/collation случаи в
  пределах существующей поддержки;
- ordering, projection, distinct.

Где полезно, previous window lowering используется как **test-only** независимый baseline; это
не разрешает production window-fallback. Существующие guard-отказы проверяются без изменений.

### 11.7 In-memory — только regression check

- Существующий in-memory unit suite прогоняется как regression check из-за общего кода.
- Новые in-memory тесты/capability не добавляются; поведение не меняется.
- Это не implementation deliverable, а проверка отсутствия поломки shared logical objects.

### 11.8 Performance

- Воспроизводимый raw/ORM-замер в уместном объёме; индексы и планы записываются.
- Как минимум: без индекса, с подходящим индексом, с индексом только по value.
- Отчёт показывает и улучшения, и регрессии; выбранная агрегатная стратегия не гейтится
  процентами, индексами или оценками оптимизатора.
- Никаких schema changes у пользователя.
- Composite-замеры не выдаются за выполненные, если их не было.

### 11.9 Публичная документация

- Обновлены EN и RU страницы из §12; `All` не описывается как оконное понижение.
- Описание отражает SQL-side sugar: общий query-lowering и сохранённый native-hook.
- `One`-описания, примеры и стратегии не изменились по смыслу.
- В публичных страницах нет ссылок на `docs/specs/**` и `docs/superpowers/**`.
- Документация считается частью acceptance: реализация без неё не завершена.

### 11.10 Документационная ревизия

Эта ревизия — **documentation-only**. Для неё не запускались build/tests, и это ожидаемо:
acceptance gates выше относятся к будущей реализации, а не к правке спецификации. Никаких
заявлений о пройденных тестах для этой ревизии не делается.

## 12. Публичная документация — обязательный deliverable

> Это **часть объёма реализации**, а не отдельная задача и не follow-up. Реализация не считается
> завершённой, пока публичные страницы описывают `All` через `rank()`-окно.

Обновляются обе локали в одном изменении:

| EN | RU |
|---|---|
| `docs/guide/09-distinct.md` | `docs/ru/guide/09-distinct.md` |
| `docs/advanced/select-where-extrema-native.md` | `docs/ru/advanced/select-where-extrema-native.md` |
| `docs/advanced/api-reference.md` | `docs/ru/advanced/api-reference.md` |

Требования к содержимому:

- `SelectWhereMax/Min` описывается как **SQL-side syntactic sugar**: логическая форма
  понижается внутри общего query-конвейера.
- Описание `ExtremeRowTies.All` переводится с текущего «`rank()` window lowering» на агрегатное
  понижение: grouped — `GROUP BY` + join-back, global — скалярный агрегат +
  join-back/predicate; composite — лексикографически, по §5.5.4. Явно указано, что ranking window
  для `All` не используется.
- Native-hook сохраняется и описывается точно: native `One` у PostgreSQL/ClickHouse и внешний
  `IExtremeRowRenderer` остаются; native `All` renderer'а нет, поэтому `All` идёт агрегатным
  понижением.
- Примеры SQL и фрагменты, показывающие `rank()`/`RANK() OVER` для `All`, заменяются
  агрегатными эквивалентами. Примеры `One` (`row_number()` и native `One`) остаются.
- `One` описывается без изменения семантики и стратегии: native `One` у PostgreSQL/ClickHouse,
  portable window в остальных случаях. Правки `One` допустимы только там, где формулировка
  иначе становится неверной.
- `api-reference.md`: строки `EntityBuilder.SelectWhereMax/Min` и `ExtremeRowTies` больше не
  утверждают, что **каждая** форма понижается до portable window function; `One` остаётся
  window/native, `All` — агрегатный. Строка `IExtremeRowRenderer` остаётся про `One`.
- Утверждение «`All` никогда не рендерится нативно» переформулируется: нативного `All`
  renderer'а нет, поэтому `All` идёт агрегатным понижением (а не «только окном»).
- **Никаких ссылок на внутренние спецификации** (`docs/specs/**`, `docs/superpowers/**`) из
  публичных страниц или readme: ни гиперссылок, ни относительных `.md`-ссылок, ни GitHub
  `blob`-ссылок. Обоснование остаётся inline.
- EN и RU зеркальны по структуре и терминологии; новых публичных API документация не вводит.

## 13. Non-goals

- Любые изменения in-memory провайдера (§7).
- Переписывание общего normalization/optimizer (LINQ roadmap, general optimizer).
- Редизайн публичного renderer-контракта.
- Удаление clause state `ExtremeRow` из публичного builder'а/логического `QueryCommand` как
  самоцель.
- Рефакторинг других синтаксических фич; `PerParentPaging` — optional, **не требуется**.
- Изменения `One` (кроме внутренних рефакторингов с byte-identical SQL).
- Новые native `All` renderer'ы.
- API tie-break, новый публичный API или изменение публичного контракта.
- Расширение query shapes или новые ограничения runtime-типов/селекторов.
- Стратегия, зависящая от метаданных/индексов/плана.
- Создание схемы/индексов.
- Blanket performance guarantee.

Общие переиспользуемые куски могут пригодиться будущим фичам, но это **не** расширяет #302 на них.

## 14. Свобода builder'а и открытые implementation details

Builder волен выбирать:

- точное внутреннее представление query-tree и адаптера native-source;
- aliasing и SQL-факторинг;
- переиспользование общих lowering-компонентов;
- provider type representations;
- внутренние CTE/derived tables в пределах возможностей провайдера;
- расположение query-тестов и performance harness.

Фаза lowering и владелец identity определяются планом в рамках §5.2. Поэтапная
composite-конструкция из §5.5.4 — architect guidance; её можно заменить семантически
эквивалентной агрегатной конструкцией. Чистый `NOT EXISTS` не заменяет требуемое агрегатное
понижение.

Открытые implementation details не являются неоднозначностями пользовательских требований.
Если для реализации нужен публичный контракт — остановиться и вынести отдельный вопрос на
approval.

## 15. Риски и блокеры

- Composite-агрегат не измерялся; его корректность должна быть доказана тестами, а не выведена
  из scalar-случая.
- Провайдерные различия null-safe equality, collation, `NaN`/non-finite и `bit`/`bool` могут
  вскрыть type blockers. Это planning blocker, а не повод менять требование. Отсутствие native
  null-safe оператора — не блокер: достаточна обычная OR-форма.
- Агрегатная форма может регрессировать на части планов; это должно быть показано честно, без
  маскировки и без возврата к окну.
- MariaDB live-доступность зависит от существующего harness; SQL-generation покрытие обязательно
  в любом случае.
- Если обнаружится необходимость публичного изменения `IExtremeRowRenderer`, работа
  останавливается и вопрос выносится отдельно.
- Точное внутреннее представление «opaque native source → общая projection» не проверено; это
  planning detail, а не заявленная возможность.
- Обнаруженное in-memory поведение right-side derived join **не** исправляется в #302; оно
  фиксируется отдельно как вне-scope находка.

## 16. Следующий гейт

1. ~~Свежий независимый builder-check **этой** редакции~~ — **выполнен**: одно уточнение про
   `NULL` уже решено унаследованной семантикой (внесено в §4, §5.5.3, §11.6), нового user
   question не потребовалось. Предыдущий review относился к предыдущей редакции.
2. ~~**USER review и approval письменной спецификации**~~ — **выполнено 2026-10-18**: USER дал
   явный approval («апрув») revised SQL-only редакции; in-memory вне области. Автоматического
   approval нет и не подразумевается.
3. **Текущий гейт:** план реализации. План **ещё не создан и не утверждён**; он требует
   отдельного USER approval. Реализация (код/тесты/доки продукта) **не авторизована** — нужно
   отдельное явное разрешение после approval плана.
4. Commit/push/merge не авторизованы.

Approval спецификации не означает approval плана и не означает approval реализации; issue остаётся
**OPEN** до завершения будущей реализации.
