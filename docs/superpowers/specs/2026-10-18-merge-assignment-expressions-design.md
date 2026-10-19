# Expression-based присваивания колонок в MERGE (#301): спецификация

> **Статус: письменная спецификация APPROVED пользователем (2026-10-18, ответ «да» на вопрос
> «Одобряешь спеку?» в этой сессии); builder-check завершён без существенных вопросов;
> планирование реализации РАЗРЕШЕНО; реализация НЕ AUTHORIZED.**
>
> Документ — одобренная письменная спецификация объёма и решений. Одобрение разрешает **только
> планирование реализации**; отдельное разрешение на реализацию требуется после плана.
> Git-действия (commit/push/merge) не авторизованы.
>
> Одобрение относится ко **всему документу целиком**, включая решения, добавленные по итогам
> builder-review как architect decisions (§0.1). Эти правки **не выбирались пользователем по
> отдельности** и не выдаются за поимённый user approval; они входят в одобренный текст как его
> часть. Ранее открытое семантическое решение (key-upsert expression INSERT обязан явно связывать
> каждый configured conflict key с соответствующим original source property) было утверждено
> пользователем в обсуждении **до** одобрения всего текста.
>
> Задача: [#301](https://github.com/AlexeyShirshov/nextorm/issues/301),
> **URL проверен**, состояние **OPEN**, milestone **`1.0.9-rc4`**. Issue обновлён этой редакцией;
> прежние формулировки о «partial setters» и о «невозможности anonymous assignment» заменены.

| Поле | Значение |
|---|---|
| Задача | [#301](https://github.com/AlexeyShirshov/nextorm/issues/301) |
| Milestone | `1.0.9-rc4` (без создания нового milestone) |
| Состояние issue | OPEN, enhancement; assignee нет |
| Стадия | written spec APPROVED пользователем (2026-10-18); builder-check завершён без существенных вопросов |
| Ревизия | одобрен весь текст, включая key-upsert key binding и architect decisions §0.1 |
| Разрешение на планирование | да — одобрение спеки разрешает планирование реализации |
| Разрешение на реализацию | отсутствует (реализация НЕ AUTHORIZED) |
| Git | никаких commit/push/merge в этой итерации |

## 0. Как читать документ

- **«Согласовано пользователем»** — решение, зафиксированное в обсуждении до письменного review.
  Отдельно от этого весь письменный текст одобрен пользователем 2026-10-18 (см. статус выше).
- **«Architect call»** — решение, оставленное архитектору/planner в рамках согласованной
  семантики; оно не расширяет scope.
- **«Architect decision»** — решение, принятое в builder-review внутри уже согласованной
  семантики; это **не** отдельный user approval и не расширение scope. Оно входит в одобренный
  пользователем текст (2026-10-18), но не приписывается пользователю как его собственный выбор.
- **«Builder freedom»** — внутренняя свобода реализации, не меняющая наблюдаемый контракт.
- **«Research» vs «tested»** — официальные доки и исходники изучены, но **новых live-execution
  evidence в этой итерации нет**. Всё, что помечено как поведение провайдера, требует тестов при
  реализации; непроверенное не объявляется поддержанным.

### 0.1 Ревизия builder-review: architect decisions (входят в одобренный текст)

Перечисленные ниже правки сделаны по итогам builder-review в рамках уже согласованной
семантики. Это **architect decisions**, а не поимённые user approvals: они не расширяют scope и
не выдаются за отдельный выбор пользователя. Builder-check завершён **без существенных
открытых вопросов**, и письменная спека была одобрена пользователем **целиком**
(2026-10-18), включая эти пункты.

1. Правило конфликтных ключей key-upsert expression INSERT добавлено в parser, capabilities,
   tests, migration и issue (§5.6, §7, §9, §11).
2. Матрица §7 разделяет **native SQL visibility** и **API support**; original source для
   single materialized input обязан поддерживаться через original bound parameters, а не
   native alias.
3. Global filters: expression INSERT при активном релевантном target filter — fail-closed
   `NotSupportedException`, кроме явного `IgnoreFilters` (§7.1).
4. Совместимость: удаляется legacy `ThenUpdate(columns)`; однопараметрический `ThenInsert`
   принимает только initializer-присваивания; bare member невалиден; shim/эвристик нет (§4.4).
5. Уточнение про «independent insert mappings»: это не blanket fail-closed; обычные разные
   значения update/insert валидны; ambiguity рассматривается только для excluded, а full MERGE
   excluded всё равно не поддерживает (§9).
6. Тело lambda: только anonymous initializer или named `MemberInit`, assignable к `TEntity`;
   runtime-конструирования нет (§4.2).

## 1. Контекст

### 1.1 Текущее поведение

В ветках `MERGE` (`ThenUpdate`/`ThenInsert`) значение записываемой колонки всегда берётся из
**одноимённой колонки source**. Селектор — это список колонок, а не присваивания: правая часть не
транслируется, рендер жёстко пишет `target.col = source.col`; insert-ветка — `values (source.col, ...)`.
Внутри `MergeBranch` хранятся только список колонок и условие. Поэтому типовой upsert с аудитом и
производными полями (`UpdatedAt = now()`, `Version = Version + 1`, `trim`, конкатенация,
`SqlFunctions`) не выражается одним стейтментом.

Проблема затрагивает и key-upsert (`WhenMatchedUpdate()`): `ON CONFLICT DO UPDATE`,
`ON DUPLICATE KEY UPDATE` и SQL Server `MERGE ... USING (VALUES)`.

### 1.2 Что заменяется

Первоначальный черновик предлагал семантику «частичных setters»: переопределить подмножество
колонок, остальные автоматически взять из source (linq2db#4357-подобно), и утверждал, что
assignment в анонимном инициализаторе невозможен. **Оба утверждения сняты:**

1. Согласованный scope — **expression-based присваивания для каждой колонки**. UPDATE пишет только
   явно назначенные свойства. INSERT вставляет только явно перечисленные колонки. Пропущенные
   колонки вообще отсутствуют в SQL и получают обычные database defaults / nullability / constraints.
2. C# не разрешает assignment-**выражения** (`Set(...)`) внутри инициализатора, но обычный
   анонимный объектный инициализатор `new { Name = s.Name, UpdatedAt = ... }` полностью легален и
   является согласованной формой API. Именованный `MemberInit` (`new Entity { ... }`) — это
   синтаксическое описание присваиваний, а не вычисляемая сущность и не вызов конструктора.

Прежняя совместимость однопараметрического selector-overload также не требуется — см. §4.4.

### 1.3 Цель

Дать пользователю выразить полный `MERGE` и key-upsert одним стейтментом с per-column значениями:
константы/замыкания, серверные функции и выражения над target/source/excluded, — сохранив
существующие guards и fail-closed поведение для неподдержанных случаев.

## 2. Объём

**В scope:**

- full `MERGE`: SQL Server и PostgreSQL;
- key-upsert: существующие PostgreSQL / SQLite / MySQL / MariaDB / SQL Server;
- interface-модели (`TEntity`-интерфейс) без конструирования интерфейса;
- typed `MemberInit` `new Concrete { ... }` и anonymous `new { ... }`.

**Вне scope:**

- ClickHouse — остаётся без DML (unsupported);
- новый query-source key-upsert;
- новая immutable-материализация сущностей;
- dynamic-dictionary API присваиваний;
- общий multi-statement fallback (второй `UPDATE`, client read-modify-write, per-row цикл);
- произвольный .NET-код в RHS (вычисляется только существующим SQL scalar translator).

## 3. Согласованные решения (user-approved и architect decisions)

Пункты ниже — решения в рамках согласованного scope. Явно помеченные **architect decision**
приняты в builder-review и **не** являются поимённым user approval (см. §0.1); весь текст
спеки, включая их, одобрен пользователем 2026-10-18.

- **UPDATE** пишет только явно назначенные свойства; **INSERT** — только явные колонки. Для
  пропущенных колонок SQL их не содержит: действуют обычные database defaults/nullability/constraints.
- Destination binding — по **CLR-имени свойства mapped `TEntity`**, а не по имени DB-колонки и не
  по имени RHS-переменной source. Это работает и для `TEntity`-интерфейса, не требуя создания
  экземпляра интерфейса.
- Typed initializer — только синтаксис описания; он **не** вычисляет сущность и не вызывает
  конструктор.
- Константы и замыкания параметризуются. RHS использует существующий SQL scalar translator;
  произвольный .NET не поддерживается. Серверно-транслируемые parameter-free функции не должны
  вычисляться как CLR-константы; captured constants и SQL nodes явно различаются.
- Отклоняются: пустое присваивание, unknown/unmapped destination, несовместимые типы, duplicate
  mapped destination, nested member/list initializer, computed/identity assignment, обновление
  ключа. Вставка ключа разрешена.
- Существующие guards сохраняются.
- **Key-upsert expression INSERT — явная привязка ключей (утверждено пользователем до всего текста).** Каждый
  configured conflict key обязан быть явно назначен и напрямую связан с одноимённым original
  source property (`Id = source.Id`); константа/трансформация/другой property — pre-exec error;
  пропущенный или трансформированный ключ — понятная ошибка, **без implicit injection**. Для
  noarg insert сохраняется существующая implicit-привязка original keys. Full MERGE этому
  правилу не подчиняется (§5.6).
- **Global filters (architect decision; входит в одобренный текст).** Старое noarg-поведение не
  меняется; expression INSERT при активном релевантном target filter — fail-closed
  `NotSupportedException`, если planner не докажет filter-контракт для реально вставляемых
  значений в одном стейтменте (source-only prevalidation недостаточна). `IgnoreFilters` —
  явный существующий механизм, автоматического bypass нет (§7.1).
- **Совместимость (architect decision).** Legacy `ThenUpdate(columns)` удаляется;
  однопараметрический `ThenInsert` — только initializer-присваивания; bare member
  (`s => s.Name`) невалиден; никаких shim/эвристик; noarg не меняется (§4.4).

## 4. API-контракт

Новые публичные имена не вводятся. Используются существующие методы с новыми overload-формами.

### 4.1 Сохраняемые формы

Безаргументные `ThenUpdate()`/`ThenInsert()` и key-формы `WhenMatchedUpdate()`/`WhenNotMatchedInsert()`
сохраняют старую семантику (все писабельные колонки / source-колонки).

### 4.2 Новые формы

| Ветка | Форма |
|---|---|
| full update | `ThenUpdate((target, source) => new { ... })` |
| full update | `ThenUpdate((target, source, excluded) => new { ... })` |
| key update | `WhenMatchedUpdate((target, source) => new { ... })` |
| key update | `WhenMatchedUpdate((target, source, excluded) => new { ... })` |
| full insert | `ThenInsert(source => new { ... })` |
| key insert | `WhenNotMatchedInsert(source => new { ... })` |

Каждая форма принимает anonymous initializer и typed `MemberInit`.

Концептуальные сигнатуры:

```csharp
Expression<Func<TEntity, TEntity, TAssignment>>                 // update (target, source)
Expression<Func<TEntity, TEntity, TEntity, TAssignment>>        // update (target, source, excluded)
Expression<Func<TEntity, TAssignment>>                          // insert (source)
```

Допустима эквивалентная реализация, удовлетворяющая этой синтаксической форме. Все lambda-параметры
— mapped `TEntity`.

Тело lambda допустимо **только** в одной из двух форм:

- anonymous object initializer `new { ... }`; либо
- named `MemberInit` `new Concrete { ... }`, где `Concrete` assignable к `TEntity`
  (`TEntity`-интерфейс реализуется concrete-типом).

Произвольный DTO, произвольный `object`/скаляр/`new`-вызов метода, nested member/list
initializer и любой иной shape отклоняются до выполнения. Initializer — синтаксическое описание
присваиваний: объект **не конструируется** во время выполнения и конструктор **не вызывается**.
Anonymous связывается по именам членов; named — по CLR-свойствам `TEntity` (см. §5.3).
Остальные правила §5 сохраняются без изменений.

### 4.3 Insert и роли параметров

Insert-формы принимают **только source**. `target` и `excluded` в insert-ветке нелегальны и
недоступны.

### 4.4 Prerelease-break (architect decision в рамках разрешённой пользователем широкой совместимости)

- **Удаляется** legacy однопараметрический overload `ThenUpdate(columns)` (column selector).
  Замена — двух-/трёхпараметрическая update-форма: `(_, source) => new { source.X }`.
- **Однопараметрический `ThenInsert`** теперь принимает **только initializer-присваивания**
  (anonymous или named, assignable к `TEntity`, §4.2). Bare-member выражение `s => s.Name`
  **невалидно** и отклоняется; оно не интерпретируется как selector и не имеет shim'а.
  `s => new { s.Name }` — валидный anonymous initializer с назначением `Name = s.Name`.
- Никаких old-name selector shim'ов, эвристик по форме или «угадывания» режима.
- Безаргументные `ThenUpdate()`/`ThenInsert()` и key-формы `WhenMatchedUpdate()`/
  `WhenNotMatchedInsert()` не меняются.
- Миграция обязательна в EN+RU доках: старые selector-примеры → initializer; для update —
  `(_, source) => new { ... }`; для insert — явные назначения колонок.

### 4.5 Проверка перегрузок

Точные overload-объявления определяются в плане. Компилятор должен доказать отсутствие
неоднозначностей для: interface vs class, anonymous vs named initializer, closure/null conversions.
Если неоднозначность неустранима без изменения семантики — это вопрос к planner, а не повод
добавлять эвристики.

## 5. Семантика присваиваний

### 5.1 UPDATE

Пишутся только явно назначенные свойства. Остальные колонки не появляются в `SET`; их значения
сохраняются как есть. Никаких автоматических «default from source» записей.

### 5.2 INSERT

Вставляются только явно перечисленные колонки. Пропущенные колонки отсутствуют в списке вставки;
действуют database defaults, nullability и constraints. Ошибка БД при нарушении constraint —
нормальное поведение, а не повод подставлять значения.

### 5.3 Binding

- Destination — CLR-свойство mapped `TEntity`. Имя DB-колонки, remapped через mapping, не является
  ключом binding'а; RHS-имя source-переменной также не является.
- Для `TEntity`-интерфейса binding идёт по свойствам интерфейса; экземпляр интерфейса не создаётся.
- Duplicate mapped destination (два RHS-члена, сериализующиеся в одно свойство/колонку) — ошибка.
- Nested member/list initializer — ошибка.

### 5.4 Значения RHS

- Константы и captured-замыкания параметризуются.
- Выражения используют существующий SQL scalar translator и существующие alias roles.
- Parameter-free серверные функции (например `NOW()`) остаются SQL-узлами и не сворачиваются в
  CLR-константу.
- Произвольный .NET (методы, не поддерживаемые translator'ом) — ошибка, а не CLR-evaluation.

### 5.5 Валидация

До выполнения отклоняются: пустое присваивание; unknown/unmapped destination; несовместимые типы;
duplicate mapped destination; nested member/list initializer; назначение computed/identity колонки;
обновление ключа в matched-ветке. Вставка ключа разрешена. Существующие guards (identity/computed,
key, duplicate columns) сохраняются и распространяются на новый режим.

### 5.6 Key-upsert: привязка конфликтных ключей (утверждено пользователем)

Для **expression-mode insert в key-upsert** (PostgreSQL/SQLite `ON CONFLICT`, MySQL/MariaDB
`ON DUPLICATE KEY`, SQL Server key-upsert через `MERGE`) действует отдельное правило:

- каждый **configured conflict key** (колонка/свойство, по которому определяется conflict,
  включая составной ключ) обязан быть **явно назначен** в initializer'е insert-ветки и
  **напрямую** связан с **одноимённым original source property**: `Id = source.Id`,
  `TenantId = source.TenantId`.
- значение ключа **не может** быть константой, преобразованием, SQL-выражением, `excluded.*`,
  `target.*` или другим свойством. Такая привязка отклоняется до выполнения.
- **пропущенный** или **трансформированный** ключ — понятная pre-execution ошибка с указанием
  ключа. **Implicit injection ключа не выполняется** и значения не подставляются за
  пользователя.
- Для **noarg** insert-ветки (`WhenNotMatchedInsert()` без аргументов) сохраняется существующее
  implicit-поведение: ключи берутся из original source. Смешанная форма «noarg insert +
  expression update» валидна; правило key-binding применяется только к expression insert.
- **Full MERGE не подпадает** под это правило: match и insert независимы, а insert-ключи
  подчиняются существующим key/identity/computed guards и поведению БД. Требование
  `Id = source.Id` — только key-upsert expression insert.
- Привязка уважает mapping/converters: «одноимённый original source property» — CLR-свойство
  mapped `TEntity`, а не имя DB-колонки.

## 6. Роли строк и `excluded`

### 6.1 Роли

- **target** — существующая строка до update.
- **source** — оригинальный входной ряд **до** insert-преобразования.
- **excluded** — provider-native предлагаемая insert-строка, включая неперечисленные/default-колонки
  и trigger-изменения ровно в той мере, в какой это определяет нативный движок. В PostgreSQL
  `BEFORE INSERT`-триггеры отражаются в excluded. `excluded` **никогда** не синтезируется проекцией
  или повторным вычислением insert-выражения и не приравнивается молча к source. Единой
  кросс-движковой гарантии по триггерам нет.

### 6.2 Пример

target `Count = 10`, source `Count = 3`, insert-выражение `Count = source.Count * 2` (вставлено 6).

- update через `source` → `13` (`10 + 3`);
- update через `excluded` → `16` (`10 + 6`).

Это различие должно быть явно зафиксировано в тестах и доках.

### 6.3 Insert

Insert видит только source. `target`/`excluded` недоступны; попытка сослаться на них — ошибка
компиляции/валидации, а не тихая подстановка. Для key-upsert expression INSERT дополнительно
действует правило привязки конфликтных ключей §5.6.

### 6.4 `WhenNotMatchedBySource`

Существующее поведение (`ThenDelete()`/`ThenDoNothing()`) не меняется.

### 6.5 Никаких скрытых записей

В expression-режиме не добавляются default column writes, в том числе implicit dynamic dict
assignments. Dynamic keys не дописываются за спиной пользователя; explicit dynamic destination —
понятная ошибка. Старое no-arg dynamic-поведение не меняется (**architect call**).

## 7. Возможности провайдеров

Таблица разделена на два независимых измерения:

- **Native SQL visibility** — какие роли реально видны в генерируемом SQL-стейтменте
  (алиасы/ключевые слова движка).
- **API support** — какие роли и формы обязуется поддержать nextorm. API может поддержать
  original source **без** native alias: для single materialized input значения берутся из
  original bound parameters, а не из excluded/new-row alias.

Потребность в роли зависит от **фактически упомянутых lambda-ролей/свойств**, а не от арности
lambda. Неиспользуемый параметр не приводит к rejection: source unsupported только если на него
**ссылаются** и значение нельзя сохранить.

| Провайдер / режим | Native SQL: target | Native SQL: original source | Native SQL: excluded | API support / примечание |
|---|---|---|---|---|
| SQL Server, full MERGE | да | да | **нет** | target+source поддержаны; ссылка на excluded → fail до execution |
| PostgreSQL, full MERGE | да | да | **нет** | target+source поддержаны; excluded → fail до execution |
| SQL Server, key-upsert (через `MERGE`) | да | да (original source в `USING`) | **нет** | original source поддержан; excluded → fail до execution |
| PostgreSQL, key-upsert (`ON CONFLICT`) | да | **нет alias** в `DO UPDATE` | да (`excluded`) | API **обязан** поддержать original source для single materialized input через original bound parameters; native excluded поддержан |
| SQLite, key-upsert | да | **нет alias** в `DO UPDATE` | да (`excluded`) | API **обязан** поддержать original source для single materialized input через bound parameters; native excluded поддержан |
| MySQL, key-upsert | да | документированные правила для `INSERT ... SELECT`; **не** `new` alias | да (`VALUES(col)` / row alias) | single input original source — через original bound parameters; **не** использовать `VALUES`/new alias как original source |
| MariaDB, key-upsert | да | не подтверждено; отсутствие подтверждённого original `SELECT` alias **не** блокер для bound single source | да (native `VALUES` документирован) | excluded через native `VALUES` документирован; default/trigger edge semantics provider-defined — тестировать до заявления о поддержке |
| ClickHouse | — | — | — | DML отсутствует, unsupported |
| in-memory | — | — | — | старый key-upsert сохраняется; expression-режим fail-closed; full MERGE unsupported (**architect call**) |

Дополнительные правила:

- **Materialized single input source**: для PG/SQLite/MySQL/MariaDB API обязан поддержать
  original source-ссылки для single input, связывая их с **исходными параметрами запроса**, а не
  с excluded/new-row alias. Это включает source-поля, которые не вставляются; значения обязаны
  уважать converters и mapping. Отсутствие native original alias (PG/SQLite) или его
  неподтверждённость (MariaDB) **не является блокером** для bound single source.
- **MySQL/MariaDB**: `VALUES(col)`/row alias (`new`) указывает на **предлагаемую insert-строку**,
  а не на original source. MySQL-документация допускает original source aliases для
  `INSERT ... SELECT`, но это не blanket guarantee; для single input используем original bound
  parameters.
- **Batch**: target и excluded для key-upsert поддерживаются нативно в соответствии с
  поддерживаемым SQL провайдера. Original source для batch — только при **доказанной точной
  стратегии одного SQL**; иначе детерминированный pre-exec reject. **Silent mapping в excluded
  запрещён.** Непроверенная стратегия не объявляется поддержанной.
- **Multiple rows / query inputs**: нужна доказанная точная стратегия для original
  source/excluded semantics в одном SQL. Если planner не может это доказать —
  `NotSupportedException` **до** выполнения.
- **Key-upsert key equality rule** (§5.6) проверяется parser'ом/capability check до execution;
  full MERGE ей не подчиняется.
- **Full MERGE excluded** не поддерживается ни на SQL Server, ни на PostgreSQL; эмуляция
  проекцией/повторным вычислением insert-выражения запрещена.
- Существующее отклонение query source для key-upsert сохраняется.
- Нет нового global-filter bypass; filter-поведение — §7.1. Нет автопереключения
  `ON CONFLICT` → `MERGE` только чтобы обойти ограничение; существующие routing/guards
  сохраняются.
- `excluded` включает неперечисленные/default-колонки там, где native SQL это позволяет.
  Чтение identity/computed отличается от write-ограничений.
- Доступ к native generated/default edge требует provider capability evidence. MariaDB
  default/trigger edge semantics считаются provider-defined и не объявляются поддержанными без
  тестов; сам native `VALUES` документирован как excluded-механизм.
- SQL Server key-upsert через `MERGE` не может использовать `excluded`.
- **Fail-closed**: никаких multiple statements, client read-modify-write или per-row fallback.

### 7.1 Global filters и expression INSERT (architect decision; входит в одобренный текст)

- Старое noarg-поведение **не меняется**.
- Новый expression-mode INSERT при **любом активном релевантном target-filter** обязан быть
  **fail-closed**: `NotSupportedException` до execution, пока planner не докажет **в одном
  стейтменте**, что реально вставляемые значения удовлетворяют существующему filter-контракту,
  **включая** omissions/defaults/SQL-выражения. Source-only prevalidation недостаточна.
- Базовая линия этой итерации: expression insert + active target filter → явный
  `NotSupportedException`. `IgnoreFilters` — существующий явный механизм, который пользователь
  вызывает сам; автоматического bypass нет.
- Существующие SQL filtered routing guards сохраняются для update/noarg форм.
- Расширенная filter-валидация — отдельный будущий scope; здесь она не обещается.

Это сознательно сужено, чтобы не допустить security bypass при обходе фильтра; пункт помечен
как architect decision и вошёл в одобренный пользователем текст целиком (§0.1).

## 8. Внутренние границы ответственности

- Неизменяемое описание assignments ветки отделяется от legacy no-arg режима.
- Parser разбирает initializer bindings (destination name + RHS), а не извлекает RHS-члены как
  старый `ParseColumns`, и проверяет key-upsert conflict-key binding §5.6 (полнота, прямое
  одноимённое original-source-значение, отсутствие констант/трансформаций/чужих свойств).
- Scoped scalar expressions передаются в planner вместе с alias roles. Dialect rendering и
  capability check выполняются **до** execution; проверка разделяет native SQL visibility и API
  support (§7) и не подменяет original source на excluded.
- Expression INSERT с активным релевантным target filter без явного `IgnoreFilters` отклоняется
  до execution (§7.1); legacy noarg путь не затрагивается.
- Не мутировать shared `QueryCommand.Cache` (в частности, не выставлять sticky `Cache = false`).
  Parameter/plan reuse должен оставаться корректным при меняющихся константах и форме выражений.
- Конкретная модель данных/renderer refactor — **builder freedom**. Global filters и dynamic legacy
  paths сохраняются. Произвольный C# не вычисляется.

## 9. Матрица приёмки

**SQL generation + live DB (каждая поддерживаемая форма):**

- typed `MemberInit` и anonymous;
- interface mapping (`TEntity`-интерфейс);
- remapped DB column доказывает binding по CLR-имени;
- constants/closure/null/compatible conversions и converters;
- target+source arithmetic; SQL-функции не fold'ятся в константы;
- native target qualification в синтаксисе.

**Семантика:**

- UPDATE: пропущенные колонки остаются нетронутыми;
- INSERT: пропущенная колонка получает фактический DB default;
- точные `13` vs `16` на подходящем провайдере;
- negative-тест full-MERGE `excluded`;
- неиспользуемый параметр не даёт rejection;
- native excluded defaults и PostgreSQL trigger test;
- single original source property, отсутствующее в insert;
- **key-upsert expression INSERT**: каждый configured conflict key явно назначен и напрямую
  связан с одноимённым original source property; пропущенный ключ, константа/трансформация,
  `excluded.*`/`target.*`/другое свойство — детерминированная pre-exec ошибка, без implicit
  injection;
- **mixed noarg/expr key-upsert**: noarg insert сохраняет implicit original keys и валиден с
  expression update;
- **full MERGE** не подчиняется key-upsert key equality rule: insert-ключи идут через
  существующие guards и SQL-поведение БД; match и insert независимы;
- batch originals: либо faithful-тест, либо детерминированный rejection до выполнения;
  original source не подменяется молча на excluded;
- **independent insert mappings**: обычные разные значения update/insert валидны (например,
  insert `Version = 1`, update `Version = t.Version + 1`). Blanket fail-closed **не применяется**;
  ambiguity рассматривается только если update ссылается на `excluded` и несколько insert-веток
  делают гипотетическую вставленную строку неоднозначной. Full MERGE `excluded` всё равно
  unsupported, поэтому excluded-эмуляции нет; несколько insert-веток сохраняют исходную
  семантику условий;
- **global filters**: expression INSERT + активный релевантный target filter → детерминированный
  pre-exec `NotSupportedException`; `IgnoreFilters` — явный opt-out; noarg-поведение не меняется.

**Негативы:**

- identity/key/computed;
- unknown destination;
- несовместимые типы;
- пустое присваивание;
- duplicate mapped destination;
- nested member/list initializer;
- обновление ключа;
- explicit dynamic destination;
- key-upsert: пропущенный/константный/трансформированный conflict key или key, привязанный к
  другому свойству;
- bare-member `ThenInsert(s => s.Name)` (удалённый/невалидный selector-режим);
- named initializer, не assignable к `TEntity`, и произвольный DTO;
- expression INSERT при активном target filter без `IgnoreFilters`;
- per-provider unsupported failures, включая ClickHouse и новый in-memory.

**Регрессия и стабильность:**

- старые no-arg формы (включая key form с implicit keys);
- документированная миграция: удалённый `ThenUpdate(columns)`, новый смысл
  `ThenInsert(initializer)`, невалидность bare-member selector;
- `IgnoreFilters` явно разрешает expression INSERT, автоматического bypass нет;
- parameter reuse/plan correctness;
- cancellation/`Returning` без изменений;
- dynamic keys не добавляются скрытно в expression-режиме.

## 10. Размещение тестов и гейты

- SQL-generation тесты — в провайдерных проектах (`tests/nextorm.<provider>.tests`), без БД.
- Parser/unit-контракты и in-memory — в `tests/nextorm.core.tests`.
- Всё исполнение, defaults и triggers — только интеграционные тесты с реальной БД; **никакого
  InMemory в integration**.
- Key-binding и global-filter rejection — parser/capability unit-тесты в core; SQL-generation —
  в провайдерных проектах; live-поведение ключей/фильтров — только интеграционные тесты.
- Assertions — FluentAssertions (кроме `Assert.Skip*`).
- Полный прогон провайдеров — с реальными контейнерами; implementer обязан загрузить инструкции
  `running-integration-tests`. Прогон со `skipped` провайдерами **не** считается зелёным.
- Unit build: .NET 10, warnings-as-errors (`TreatWarningsAsErrors=true`).
- Coverage-гейты не понижаются. Известный красный branch-baseline интеграционного гейта не является
  waiver'ом для приёмки этой фичи.

## 11. Документация

При реализации (не сейчас):

- `docs/guide/21-merge-statement.md` (EN+RU) — новые формы, семантика update/insert, роли
  target/source/excluded, пример 13/16;
- `docs/guide/26-optimistic-concurrency.md` (EN+RU) — старые selector-примеры и миграция;
- EN+RU миграционные заметки: удалённый `ThenUpdate(columns)`, новый смысл
  `ThenInsert(initializer)`, невалидность bare-member selector, key-upsert key binding;
- curated API docs (EN+RU) — новые overload-формы;
- `docs/specs/comparison/linq2db-backlog-gap-analysis.md` — обновить G29 **в реализации**, не
  объявлять сделанным сейчас.

Публичные доки **не** ссылаются на `docs/specs/**` или этот внутренний документ.

## 12. Открыто для planner

В рамках согласованной семантики planner должен решить:

1. Конкретные overload-объявления и внутреннее представление assignments.
2. Точную batch-стратегию для original source (с evidence одного SQL) либо детерминированные
   fail-closed проверки; silent mapping в excluded запрещён.
3. Проверенные provider/version capabilities для native generated/default read.
4. Точную форму/локацию pre-exec ошибок для key-binding и global-filter fail-closed.

Неподдержанные случаи должны быть явно перечислены в доках; широкий «full support» не
рекламируется. Спека одобрена (2026-10-18), что разрешает только планирование реализации;
реализация остаётся НЕ AUTHORIZED до отдельного разрешения после плана.

## 13. Evidence и ссылки

### 13.1 Указатели на исходники (research; line numbers — ориентиры)

- `src/nextorm.core/Builders/MergeBranchBuilders.cs:24-65` — публичные формы веток.
- `src/nextorm.core/Builders/MergeBuilder.Columns.cs:47-126` — `AddBranch`/`ParseColumns`/
  `ValidateExplicitColumns`.
- `src/nextorm.core/Builders/MergeBuilder.cs:436-444,658-677` — routing/upsert.
- `src/nextorm.core/DataContext/SqlMutationBuilder.cs:665-848,995-1083` — key-upsert и MERGE
  rendering (insert-ветка, assignments).
- `src/nextorm.sqlserver/SqlServerDialect.cs:134-159` — SQL Server capability/render.
- `src/nextorm.core/DataContext/QueryPlanner.cs:279-318` — planner/scalar translation.
- `tests/nextorm.integration.tests/Postgres/PostgresSpecificTests.cs:1319-1349` — interface support.
- `docs/guide/21-merge-statement.md:74` — текущее описание selector-семантики (EN+RU).
- `docs/specs/design/API-NAMING-REVIEW.md:4099` — MRG2: существующие публичные имена заморожены
  (`MergeBuilder`, `WhenMatchedUpdate`/`WhenNotMatchedInsert` не переименовываются).
- `MergeBranch` / `MergeCommand` — текущее хранение колонок и условий.

### 13.2 Официальные доки

- PostgreSQL `INSERT` (ON CONFLICT): https://www.postgresql.org/docs/current/sql-insert.html
- PostgreSQL `MERGE`: https://www.postgresql.org/docs/current/sql-merge.html
- SQLite UPSERT: https://sqlite.org/lang_upsert.html
- MySQL `INSERT ... ON DUPLICATE KEY UPDATE`:
  https://dev.mysql.com/doc/refman/8.4/en/insert-on-duplicate.html
- MariaDB `INSERT ... ON DUPLICATE KEY UPDATE`:
  https://mariadb.com/docs/server/reference/sql-statements/data-manipulation/inserting-loading-data/insert-on-duplicate-key-update
- SQL Server `MERGE`:
  https://learn.microsoft.com/en-us/sql/t-sql/statements/merge-transact-sql

### 13.3 Related

- linq2db#4357 — partial setters; семантика **отвергнута** для этого scope:
  https://github.com/linq2db/linq2db/issues/4357
- linq2db#4237 — immutable-модели; materialization не добавляется:
  https://github.com/linq2db/linq2db/issues/4237
- EF Core `ExecuteUpdate(setters => setters.SetProperty(...))` — форма per-column setter:
  https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete#executeupdate

### 13.4 Статус review и approval

- Артефакт: `docs/superpowers/specs/2026-10-18-merge-assignment-expressions-design.md`
  (этот файл), одобрен **целиком**; дата одобрения — **2026-10-18**.
- Одобрение получено явным ответом пользователя «да» на вопрос «Одобряешь спеку?» в текущей
  сессии. Builder-check завершён без существенных открытых вопросов.
- Одобрение разрешает **только планирование реализации**. Реализация **НЕ AUTHORIZED**;
  отдельное разрешение требуется после плана.
- Отслеживание: [#301](https://github.com/AlexeyShirshov/nextorm/issues/301), состояние **OPEN**,
  milestone **`1.0.9-rc4`**, label `enhancement`; scope issue не менялся, обновлён только статус.

---

*Письменная спецификация APPROVED пользователем 2026-10-18 (весь текст, включая architect
decisions §0.1); планирование реализации разрешено, реализация НЕ AUTHORIZED. Новых
live-execution evidence в этой итерации нет: всё поведение провайдеров помечено как research и
подлежит проверке тестами при реализации.*
