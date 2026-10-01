# Неявные навигационные запросы: одиночные связи и AsEntityBuilder

**Дата:** 2026-10-01
**Tracking issue:** [#148](https://github.com/AlexeyShirshov/nextorm/issues/148) — верифицировано 2026-10-01 (title/state/milestone/body проверены через gh API; milestone `1.0.9-b` = number 18).
**Milestone:** 1.0.9-b
**Предшественник:** [#105](https://github.com/AlexeyShirshov/nextorm/issues/105) — закрыт; поставлены **только слайсы A+B**, слайс C явно отложен.
**Статус:** разговорные разделы дизайна согласованы; ЭТА письменная спека ожидает одобрения пользователя (user approval); план реализации не составлен, реализация не авторизована.

**Цель.** Эргономичные навигационные запросы на пути к паритету с linq2db / EF Core **БЕЗ** второго полного транслятора `Enumerable`/`Queryable`. Эта ограниченная фича — **НЕ** полный паритет. Существующий публичный API нативного движка и лимиты провайдеров сохраняются.

---

## 1. Принятая область (Accepted scope)

1. **Скалярные цепочки членов ссылочной навигации:** `e.Parent.Name`, `e.Parent.Parent.Name`, self-ссылки, несколько различных связей на один и тот же тип, проверки `e.Parent == null` / `!= null`. Поддерживается везде, где существующий движок поддерживает эквивалентное явное выражение источника/колонки, в частности `Where`, `Select` (скалярные/анонимные проекции), `OrderBy`, подзапросы. Только **явные** метаданные `HasOne`/`HasMany`/`HasOneToOne`/`HasManyThrough`/атрибуты, **без** конвенций именования FK. Только **одноколоночные** ключи связей.

2. **Прямые операции только над ОБЪЯВЛЕННОЙ коллекционной навигацией:** parameterless `Enumerable.Any()`, `Count()`, `LongCount()`, свойство `Count`, когда статически объявленный тип коллекции его предоставляет. **Никаких** перегрузок с предикатом. Результаты: `bool` для `Any()`, `int` для `Count()`, `long` для `LongCount()`, `int` для свойства `Count`. Пустая связанная последовательность даёт `false`/`0`/`0`. `Count()` и свойство `Count` не оборачиваются и не усекаются при превышении `Int32.MaxValue`: выход за диапазон `int` даёт `OverflowException` (согласованно с диапазоном счёта); `LongCount()` использует настоящий 64-битный счёт. Форма-свойство существует только тогда, когда статически объявленный тип коллекции предоставляет `Count`; свойство не добавляется к `IEnumerable` и не меняет CLR-тип навигации. Тот же канонический related source, что и у адаптера.

3. **Новый expression-only API, предложенное/утверждённое имя:** `AsEntityBuilder<T>(this IEnumerable<T> navigation) -> EntityBuilder<T>, T:class`; это **предполагаемая** сигнатура, **не** реализованный API. Фактический CLR-тип навигационного свойства не меняется. Работает на объявленном nav-пути, укоренённом в сущности/алиасе запроса, **только внутри query-выражения**; произвольные захваченные `IEnumerable` и навигация, уже полученная вне выражения, не поддерживаются. Использование вне поддерживаемого query-выражения всегда бросает `NotSupportedException` (**никогда** не default/null-маркер). Контекст выводится из окружающего запроса; контекст, переданный вызывающим кодом, не требуется. Приёмник вызова распознаётся и заменяется **до** CLR-вычисления нативной цепочки builder.

4. **После `AsEntityBuilder`** — полная существующая возможность движка, подходящая для данного провайдера и встраивания запроса, **без** nav-специфичного чёрного списка операторов; обычные API/типы сохраняются (`Offset`/`Limit`, повторный `OrderBy`, `Select->QueryCommand`, обёртка `As` projection и т.д.). Нативные ограничения движка адаптер не чинит и не расширяет. Нативные отсутствующие SQL `GroupJoin`/`SelectMany` остаются отсутствующими; терминальные ограничения scalar/cardinality, глубина корреляции в InMemory, ограничения derived/paging/set composition — те же, что у явного эквивалента. Неподдерживаемое даёт диагностику как у явного эквивалента, а не скрытое клиентское исполнение. Ограничения на прямые вызовы `Enumerable` по навигации (см. §2) на нативные цепочки builder после `AsEntityBuilder` не распространяются.

5. **InMemory включён.** Зарегистрированные наборы данных контекста и метаданные связей **авторитетны** для ОБЪЯВЛЕННЫХ навигационных выражений; предзаполненный граф объектов не требуется; никакой опоры на заполненность enumerable `Children`, никакой lazy loading. Отсутствие/`null` ссылки и количество коллекций согласуются с SQL. Существующие не-навигационные CLR-выражения и обычные лимиты InMemory-движка сохраняются. Отсутствие требуемого зарегистрированного dataset даёт существующую ясную диагностику missing-source, а **не** тихий откат к CLR-графу.

6. **M2M через явный junction**, вложенная навигация/query scopes в пределах существующих возможностей движка; дублирующиеся junction-строки сохраняют вхождения, прямые `Count`/`LongCount` их считают, `Any` проверяет существование. Удаление — только явной нативной distinct-операцией, если она доступна.

## 2. Вне области (Out of scope)

Полная прямая композиция `Enumerable`/`Queryable` на навигации (`Where`/`OrderBy`/`Select`/`Skip`/`Take`/`Distinct`/`GroupBy`/joins/set-операции и т.д.). Дополнительные прямые терминалы `First`/`Single`/`Last`/`Contains` и т.п., не входящие в прямой набор. SQL `SelectMany`, финальная материализация вложенной коллекции/группы, автоматическая загрузка/заполнение графа, составные ключи связей/junction, конвенции именования, сплошное расширение существующих возможностей движка/провайдеров. Никакого подразумеваемого расширения паритета EF-моста. Полная матрица LINQ, исследованная ранее, была **явно отозвана** и заменена дизайном адаптера.

## 3. Null-контракт

- Reference expansion **всегда `LEFT JOIN`** (в метаданных сейчас нет флага обязательности), никогда не тихий `INNER`. Отсутствующая связанная строка распространяет `NULL` по цепочке.
- `e.Parent == null` проверяет существование связанной строки через non-nullable идентифицирующий principal key/присутствие, **НЕ только** через `e.ParentId`; висячий FK считается отсутствующим.
- `p.Name == null` истинно, когда строка отсутствует **ИЛИ** `Name` null; `p.Name != 'Anna'` включает отсутствующие/null строки; `e.IsActive || p.Name == 'Anna'` должен сохранять active-строки без parent.
- Сравнения для nav-derived значений компенсируются в предикатах **И** в value/bool-проекциях; глобальная null-семантика старых запросов при этом не меняется.
- Оператор `!` в C# влияет **только** на аннотации компилятора.
- SQL nullable primitive projections явны: `(int?)e.Parent.Age` или `(int?)... ?? 0`; материализация `NULL` в non-nullable (unlifted int) значение явно завершается ошибкой с контекстом пути и типа результата, без тихого default. Конкретный низкоуровневый тип исключения остаётся существующим контрактом материализации; новая публичная иерархия исключений не вводится. Ссылки материализуются в `null` при отсутствии. Коллекция, достигнутая после отсутствующей ссылки, пуста.
- **ClickHouse:** требуется query-local real-null семантика (например, query-level `join_use_nulls`; точный SQL определяется при планировании с evidence о возможностях провайдера), **без** мутации сессии/глобала и без default `0`/пустой строки, маскирующих отсутствующие строки.

## 4. Архитектура / поток данных

- **A** — `NavigationPathResolver` использует immutable resolved metadata, scope/source identity, точный путь членов связи включая junction identity.
- **B** — стадия до prepare `NavigationExpansion` канонизирует прямые терминалы + адаптер + ref joins **до** компиляции приёмника нативного builder / материализации команды. Имена предварительные, **ВНУТРЕННИЕ** границы, не обязательство по выпущенным публичным типам.
- Один и тот же ref-путь в одном query scope/source переиспользуется в `Where`/`Select`/`OrderBy`. Разные nav/scopes/aliases **не** сливаются; никакого сопоставления с произвольным явным `JOIN` по типу сущности.
- Коллекции — коррелированный `EXISTS`/scalar count, **НИКОГДА** не разворачиваются в основные строки.
- Источник адаптера — обычный related source `EntityBuilder` с FK/principal корреляцией; junction для M2M; те же нативные вложенные лямбды и outer-ref binding.
- **C** — существующий planner/SQL renderer/executor (или соответствующая ветка InMemory) исполняет. Sequence-семантика нативного builder сохраняется; адаптер не вводит альтернативный компилятор/политики композиции.
- InMemory разрешает metadata-based related sources **до** компиляции выражения, null-safe reference-поведение вместо сырого дерева графа.

## 5. Кэш / инварианты

- При подготовке нового плана expansion неизменяем; на каждый прогретый cache hit expansion не повторяется и nav-план не аллоцируется.
- Идентичность плана различает root source/scope, nav member path, relation mapping включая keys/junction и существующую mapping identity/инвалидацию; захваченные значения остаются runtime-параметрами, **никогда** не замораживаются в плане.
- `QueryCommand` для `Any`/`Count` в одном контексте общий; sticky `Cache=false` не мутируется; локальный флаг `storeInCache` применяется только при действительной необходимости (фича не должна отключать кэш).
- Никакой мутации source-команды, никакой утечки между запросами.
- Публичное изменение — аддитивное expression-only расширение с XML-docs в будущей реализации и обновлением docs EN/RU.

## 6. Примеры целевого API (`AsEntityBuilder` ещё не реализован)

```csharp
ctx.From<Parent>()
   .Where(e => e.Parent!.Name == name)
   .Select(e => new { e.Id, ParentName = e.Parent!.Name, ParentAge = (int?)e.Parent!.Age });

ctx.From<Parent>()
   .Select(e => new { e.Id, HasChildren = e.Children.Any(), ChildrenCount = e.Children.Count(), ChildrenCount64 = e.Children.LongCount() });

ctx.From<Parent>()
   .Where(e => e.Children.AsEntityBuilder()
       .Where(c => c.IsActive)
       .OrderByDescending(c => c.CreatedAt)
       .Offset(5).Limit(10)
       .Any());
```

`Offset`/`Limit` и `Select->QueryCommand` — **фактические нативные сигнатуры**; выдумывать fluent `Select`, за которым идут EntityBuilder-операции, нельзя. Будущие async-методы и неопределённый полный LINQ-интерфейс не используются.

## 7. Диагностика

- Незамапленная навигация: диагностика идентифицирует путь и отсутствующий mapping.
- Прямой неподдерживаемый вызов: диагностика указывает на обходной путь `AsEntityBuilder`, когда движок поддерживает эквивалент.
- Использование маркера вне query-выражения всегда бросает `NotSupportedException`.
- Составной ключ, провайдерное или нативное неподдерживаемое — по существующей классификации диагностик, без клиентского исполнения.
- Произвольный сложный scalar/member не трактуется как навигация при отсутствии объявленной связи; поддержанные существующие случаи сохраняются.

## 8. Приёмка / верификация (требования для будущего плана, **НЕ** запускать/не заявлять тесты сегодня)

- Roslyn-discovered symbol impact.
- Сборка Debug/Release без предупреждений и ошибок.
- API/XML docs.
- Тесты: reference absence / dangling / null / OR / bool projection / nonnullable materialization / cast-coalesce; chains / self / dual same type / source scopes; reuse alias; четыре прямые операции (`Any()`, `Count()`, `LongCount()`, свойство `Count`) — empty / nested / M2M duplicates / 64bit count SQL.
- Adapter equivalence к явным нативным запросам по `Where`/`OrderBy`/paging/scalar projection/aggregate; nested correlation / native grouping / set / join features — только там, где явный baseline работает.
- Существующие неподдерживаемые явные случаи остаются неподдерживаемыми, без adapter-specific ban.
- Reject: direct LINQ / captured enumerable / outside marker / composite keys.
- InMemory datasets с невыставленным графом и с несогласованным графом — проверка, что метаданные авторитетны.
- Cache: warmed / captured parameter changes / distinct mappings / no sticky flag; no nav-expansion allocation на прогретом cached path; репрезентативное измерение в будущем плане с baseline, без изобретения абсолютного порога перфоманса.
- Реальные интеграционные прогоны SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse (загрузить skill running-integration-tests, skipped providers — **НЕ** pass, восстановить Podman); MariaDB dialect/shared MySQL behavior — отдельно определить, какие реальные провайдеры запускались, не заявлять протестированный контейнер MariaDB, если его не было.
- Явные ClickHouse null-result assertions нужны; существующий common test suite там **не** работает.
- InMemory parity + сохранение старых тестов.
- Будущие публичные docs EN/RU без ссылок на спеки, валидация DocFX.
- Обязательные правила проекта по перф/покрытию учитываются в плане реализации; результатов пока нет.

## 9. Указатели на evidence

- `src/nextorm.core/Builders/EntityBuilder.cs` — `Select` (249), конструкторы (65–80), `Limit` (2130), `Offset` (2141).
- `src/nextorm.core/Builders/EntityBuilderExtensions.cs` — `Count` (815), `Any` (213).
- `src/nextorm.core/Visitors/CorrelatedQueryExpressionVisitor.cs` — receiver (339–346), compile (261–268).
- Существующая документация связей и `docs/specs/roadmap/todo_navigation_properties.md` (A+B vs C).
- `src/nextorm.core/Visitors/WhereExpressionVisitor.cs` (21–66) / `src/nextorm.core/Visitors/PredicateTranslator.cs` (327–473) — null gap.
- `src/nextorm.core/DataContext/InMemoryConditionFactory.cs` (29–53) — сырой CLR-граф.
- ClickHouse docs: https://clickhouse.com/docs/reference/settings/session-settings/join#join_use_nulls

## 10. Согласования / handoff

Принятые разговорные решения — выше; спека ожидает одобрения пользователя; далее `writing-plans`; никакого кода/плана/коммита сегодня. Любое открытие при реализации, требующее расширения нативных возможностей или изменения утверждённой null/API-семантики, требует **явной ревизии дизайна**, а не тихого изменения объёма.
