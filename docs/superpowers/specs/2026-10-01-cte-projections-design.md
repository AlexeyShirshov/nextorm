# Типизированные проекции обычных и рекурсивных CTE

- **Дата:** 2026-10-01
- **Статус:** conversational design approved; письменная спека ожидает ревью пользователя; реализация не начата
- **Тип документа:** дизайн-спека — записанные решения, не план реализации

## 1. Боль и мотивация

Сейчас чтение из CTE идёт по имени и возвращает `EntityBuilder<TableAlias>`, поэтому обращение к
колонкам — это строковые runtime-API: `t.GetInt64("id")`, `t["id"].AsInt`. Проекция исходного
`Select` при `CteQuery.From` теряется: наружу известны только имена и типы, вытащенные рантаймом.

Хочется: обращаться к колонкам CTE как к свойствам (`row.Id`) с выводом типов; работать с
анонимными типами, DTO/record, скалярами и уже поддерживаемыми формами `Select`; рекурсивно
ссылаться на CTE внутри её же шага; смешивать несколько разнородных CTE и делать типизированные
`Join`.

Ключевое: **CTE — источник проекции, а не mapped-таблица/сущность.** Существующий контракт
результата `Select` сохраняется; отдельный DTO-mapping не вводится.

## 2. Факты текущего worktree (discovery, не тесты)

Наблюдения из scout-проходов этой сессии; worktree грязный, позиции строк волатильны. Это **не**
воспроизведённые дефекты, а риски, требующие тестов.

- Обычный `CteQuery.From(string)` возвращает `EntityBuilder<TableAlias>` и работает по имени:
  `src/nextorm.core/Builders/CteQuery.cs:118`.
- `QueryCommand.ResultType` / `SelectList` и `SelectExpression` описывают проекцию команды;
  `IDataContext.From(QueryCommand<T>)` строит derived table: `src/nextorm.core/DataContext/DataContextExtensions.cs:1386`.
- Внутренний `FromExpression(string, QueryCommand columnShape)` (`src/nextorm.core/Expressions/FromExpression.cs:60`)
  переносит shape колонок; так он используется в `src/nextorm.core/Builders/MutationCteQuery.cs:56-63`.
- Текущий `CteQuery` держит неизменяемый список объявлений.
- `UnionAll<T>` принимает произвольный generic-операнд (`src/nextorm.core/Query/QueryCommand.TResult.cs:973`);
  `SetOperation` не проверяет SQL-форму (`src/nextorm.core/Query/QueryCommand.cs:745-756`).
- `CteHoister` обходит определения, но распространение shape по присоединённому графу
  `QueryCommand` неравномерно — **эмпирически не воспроизведено**.
- Глобальная инъекция фильтров (`src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:866-885`) не имеет
  очевидного исключения по `ColumnShape`; форвардинг конвертеров во внешнюю репроекцию не проверен.
  Это риски, а не доказанные дефекты.

## 3. Альтернативы и выбор

| Вариант | Итог |
|---|---|
| Независимый дескриптор `Cte<TProjection>` | **рекомендован и выбран** |
| `CteQuery<T>` с типом только на «последнем» CTE | не выдерживает разнородные CTE без доп. машинерии |
| `From<Row>(string)` | нет вывода анонимного типа, непроверяемые claims о типах |

Решение: **независимые дескрипторы** — по дескриптору на CTE, каждый несёт собственный `TProjection`.

## 4. Публичный контракт (target API)

Имена generic-параметров формальны. Примеры намеренно без `namespace`.

- `sealed` immutable `Cte<TProjection>`; `Cte<TProjection>.Name` — read-only. Определение и shape —
  внутреннее состояние; глубокой неизменяемости underlying `QueryCommand` контракт не обещает.
- `sealed` immutable `CteReference<TProjection>`: нет публичных конструкторов, принимающих
  произвольные имя/тип; создание только через фабричные методы; не `IDisposable`; мутировать
  объявление нельзя.
- `QueryCommand<T>.AsCte(string name) -> Cte<T>`.
- `QueryCommand<T>.AsRecursiveCte(string name, Func<CteReference<T>, QueryCommand<T>> step) -> Cte<T>`.
- Перегрузка required-maxRecursion: та же сигнатура + `int maxRecursion` последним параметром.
  **Никакого** `int? maxRecursion = null` / optional nullable overload.
- `IDataContext.From(Cte<T>) -> EntityBuilder<T>`; `IDataContext.From(CteReference<T>) -> EntityBuilder<T>`.

Совместимость: старые `With` / `WithRecursive` / `From(CteDefinition)` / `From(string)` сохраняют
сигнатуры и поведение; общее поведение `UnionAll` не меняется. Обязательный явный `With` для
дескрипторов не вводится. Вне скоупа: новый `With`-overload для дескриптора, прямой `Join` по
дескриптору, явный DTO-remap, редизайн mutation-CTE API.

## 5. Примеры (target API)

### 5.1 Обычные CTE

```csharp
var recent = baseQuery.Select(x => new { x.Id, x.Total }).AsCte("recent");
var other  = otherQuery.Select(y => new { y.Id, y.Value }).AsCte("other"); // отдельный Cte-тип

var q = context.From(recent).Where(r => r.Total > 0).Select(r => r.Id);

var joined = context.From(recent)
                    .Join(context.From(other), r => r.Id, o => o.Id, (r, o) => new { r.Id, o.Value });
```

Вывод типов всегда равен `TResult` соответствующего `Select`; свойства транслируются в output-алиасы
проекции, а не в физические имена колонок таблицы.

### 5.2 Рекурсия

```csharp
var anchor = baseQuery.Select(x => new { N = x.Id });

var nums = anchor.AsRecursiveCte("nums", self =>
    context.From(self)
           .Where(t => t.N < 5)
           .Select(t => new { N = t.N + 1 }));

var q = context.From(nums).Select(t => t.N);
```

- Колбэк вызывается **один раз синхронно** при построении дескриптора (сбор expression-запроса, без
  DB IO); автоматически формируется `anchor UNION ALL step`. Колбэк обязан вернуть тот же `T`, что и anchor.
- Каждый `AsRecursiveCte` создаёт **уникальный внутренний ownership token** для своей самоссылки;
  идентичность самоссылки — token, а не тип/имя. `From(Cte<T>)` вносит завершённое определение как
  root; `From(CteReference<T>)` вносит только именованный projection source с owning token и **никогда**
  не определение/root.
- Пока готовится завершённый CTE, его step обходится (включая вложенные источники и подзапросы) под
  этим owner token. Совпадающие самоссылки допустимы в этом обходе и в сохранённых источниках шага
  после возврата колбэка; истечение лексической области колбэка **не** объявляется ограничением.
- Ссылка на самоссылку как на независимый исполняемый root или под чужим CTE-owner —
  `InvalidOperationException` до БД. Совпадающее self-edge явно освобождается от обычного обхода
  dependency-циклов; все прочие циклы дают существующие ошибки.
- Anchor не имеет доступа к новой самоссылке и поставляет только shape; самостоятельное использование
  самоссылки не может неявно изобрести/восстановить CTE root. Самоссылка не является полным
  объявлением; цикла `anchor -> step -> definition` в подготовке быть не должно.
- Наружу не выставляется мутабельный неполный дескриптор; взаимной рекурсии нет. Сначала
  поддерживается только автоматический путь `UNION ALL`; ручной `WithRecursive` не меняется.
- `maxRecursion` сохраняет существующую семантику SQL Server; поддержка неподдерживаемых диалектов не изобретается.

## 6. Формы Select и связывание shape

Все текущие SQL-поддерживаемые формы `Select`: скаляр / одна колонка; анонимный тип; constructor
DTO / record; `MemberInit`; whole entity; уже поддерживаемые формы `System.Tuple` / `IProjection`,
включая flatten вложенных и entity-item.

Скалярные параметры и доступ к tuple/member работают по существующей семантике derived table, а не
по новому синтаксису. ValueTuple не получает новой поддержки только потому, что классификатор типов
его распознаёт.

Обязательна матрица валидации **каждой** поддерживаемой формы. Неподдерживаемая форма — fail fast,
а не тихий fallback к метаданным. CTE сохраняет связь member/slot-path → output column, алиасы,
конвертер + provider-типы и контракт materialization nullability (нормализованный контракт — §8).
Whole-entity `T` **не** означает table-mapping на внешнем источнике. Дублирующей инъекции глобального entity-фильтра на основной или
присоединённый typed-CTE источник быть не должно; фильтры по-прежнему применяются внутри
определяющего запроса. Одинаковый CLR-тип в двух дескрипторах и один дескриптор в self-join
связываются по instance источника / параметру / slot, а не по типу.

## 7. Модель зависимостей и граф

`From(Cte<T>)` автоматически ассоциирует объявление и достижимые зависимости с запросом;
недостижимые дескрипторы опускаются. Обход: основной `From`; `Join` в обеих формах (builder и
`QueryCommand` overload); derived подзапросы; уже поддерживаемые коррелированные формы; ветки
set-операций; вложенные тела CTE.

Сбор графа детерминирован, dependency-before-consumer. Дубликаты одного instance объявления
дедуплицируются; разные объявления с одинаковым именем — reject по существующей Ordinal-семантике
(автопереименования нет). Рекурсивный CTE обходится под owner token своего `AsRecursiveCte`;
matching self edge явно освобождается от обычного dependency-cycle-обхода (см. §5.2), все прочие
циклы дают существующие ошибки. `From(CteReference<T>)` вносит только projection source с owning
token и не добавляет определение/root; root вносит только `From(Cte<T>)`. Совпадение projection-типа
само по себе не дедуп. Именованный CTE-источник остаётся без лишней derived-SELECT-обёртки.

Переиспользовать существующие `CteDefinition` / `CteMerge` / `CteHoister`, где возможно, а не
строить параллельный CTE-движок. Никаких реализационных предписаний, противоречащих поведенческим
требованиям.

## 8. Валидация shape для нового рекурсивного API

При SQL-подготовке, после того как output-shape anchor и step известны и **до** БД, обе ветки
приводятся к **нормализованному контракту** и сравниваются на равенство. Определение контракта:

- shape flatten-ится в упорядоченный список leaf-колонок по **существующим** правилам проекции;
- на каждую запись фиксируются: member path / slot / index identity; declared CLR materialization
  type (`Nullable<T>` отличается от `T`; nullable-аннотация ссылки — только если существующая
  prepared metadata реально её записывает);
- effective bound provider CLR type = `converter?.ProviderType ?? preparedColumn.ProviderType ??
  column CLR type` в существующем представлении — концептуально, без переизобретения кода;
- member/slot ID соответствуют anchor → step; различающиеся физические source-выражения/имена SQL
  alias допустимы;
- converter должен быть семантически тем же binding, что и в существующей prepared metadata (тот же
  сконфигурированный instance конвертера либо эквивалентность, уже явно признанная в проекте; **без**
  нового generic equivalence-by-converter-class);
- записанная nullability metadata сравнивается как часть контракта колонки; `Nullable` vs
  non-nullable **declared/recorded** контракты не нормализуются молча в равные.

Обе ветки обязаны совпасть по этим type-контрактам: без автоматического unwrap `Nullable`, без
widening. Если равенство установить нельзя — reject mismatched contract, а не молчаливая неверная
materialization. Anchor авторитетен для внешнего имени и shape; идентичных физических
source-выражений/алиасов не требуем (иначе вычисляемый step не пройдёт). Явные cast / projection
unification в основном запросе могут разрешить type mismatch; converter mismatch унифицируется
через сконфигурированный binding.

Примеры: **valid** — разные source-выражения с одинаковыми типами (path/slot совпадают, SQL alias
различается); **invalid** — один и тот же DTO с разным списком member; **invalid** — расхождение
`Nullable<T>` vs `T` либо разных provider-типов / converter binding.

Диагностика: полезный `InvalidOperationException` называет CTE, ветку, позицию и expected-vs-actual
shape. Ошибки компиляции — при несовпадающем `T`; ошибки аргументов — невалидные имена /
`null`-колбэк; существующие диагностики дублирующего имени и циклов сохраняются. Статическая проверка
**не** доказывает nullability SQL-выражения и не покрывает всю SQL/provider-совместимость:
неожиданный `NULL` из БД остаётся предметом существующего runtime-поведения materialization.
Матрицу provider cross-type совместимости не изобретаем — критерий задан нормализованным контрактом
выше; при несовпадении — явные cast / projection-унификация.

## 9. Lifecycle и владение

Дескриптор — immutable-ассоциации, а не глубокий snapshot графа `QueryCommand`. Существующие
правила lifecycle/ownership контекста и конкурентности сохраняются. Тело, anchor и step строятся до
захвата; мутация захваченного графа после захвата запрещена. Нет глубокого клонирования, нового
глобального реестра метаданных и мутации общих флагов команд. Подготовка/использование дескриптора
не переопределяет body/type/definitions и не загрязняет entity-метаданные. Валидация — на этапе
подготовки, не новая per-row работа; глобальный plan-cache не отключается; несвязанных
cache/perf-обещаний нет.

## 10. Границы компонентов (концептуально)

- typed-фабрики + descriptor/reference — эргономичное типобезопасное построение;
- связывание shape источника проекции — через существующий механизм `FromExpression` или
  эквивалентный явный projection-source marker;
- сбор/hoisting объявлений графа; валидатор рекурсивного shape;
- существующие dialect emission/materializer'ы потребляют ту же семантику проекции.

Улучшаем только точечные пробелы, нужные для typed API; несвязанного рефакторинга нет.

## 11. Провайдеры

- Обычные CTE: SQLite, PostgreSQL, SQL Server, MySQL, MariaDB (follows MySQL provider), ClickHouse.
- Рекурсивные CTE — только там, где они уже поддержаны (ClickHouse исключён).
- Нового in-memory исполнения CTE нет; core in-memory может строить дескрипторы для metadata-юнит-тестов.
- API data-modifying CTE не меняется; внутренние общие shape-пути можно улучшать при необходимости,
  но с регрессионными тестами. Второй feature surface не появляется.

## 12. Матрица приёмки

| Область | Что проверяем |
|---|---|
| Вывод типов | анонимный тип, DTO/record |
| Формы shape | скаляр, `System.Tuple`, whole entity, flatten nested `IProjection` |
| Проекции | aliased/renamed/computed |
| Конвертеры | converter end-to-end + outer member reprojection |
| Несколько CTE | разнородные, два CTE одного типа, self-join |
| JOIN | builder + query, подзапрос, union graph propagation |
| Зависимости | порядок, дедуп, недостижимые опущены, конфликт имён |
| Рекурсия | валидная: анонимный/скалярный/applicable nested shape |
| Ошибки | одинаковый `T` при разном DTO-shape; несовпадающий `T` (compile contract) |
| Ссылки | unbound/foreign reference |
| maxRecursion | семантика SQL Server |
| Фильтры | только в теле, main + joins |
| Регрессия | старые string/mutation/derived пути |

Локации тестов: `tests/nextorm.core.tests` (descriptor/shape); `tests/nextorm.sqlite.tests` (SQL +
materialization); `tests/nextorm.alias.tests` (alias slots); provider SQL-gen suites;
`tests/nextorm.integration.tests` (`CommonTestSuite.Cte.cs` и другие common suites по всем
провайдерам). Реальная БД обязательна для поддерживаемых провайдеров/форм; зелёного статуса для
пропущенных провайдеров нет; действуют правила скилла `running-integration-tests` и Podman.
Документация EN+RU, XML public docs, API-snapshot где настроен, `dotnet build` без
warnings-as-errors и тестовые доказательства — требования реализации, не этой спеки.

## 13. Out of scope

Явный DTO-remapping; новые формы `Select`; автоматический provider widening/casts; глубокий snapshot
команды; поддержка конкурентного общего контекста; новые SQL-возможности; in-memory CTE-executor;
взаимная рекурсия; рекурсивный UNION-distinct overload; overhaul mutation-CTE surface.

## 14. Handoff

1. Пользователь ревьюит **эту письменную спеку** (conversational approvals не являются approval'ом файла).
2. **Только после** approval — скилл `writing-plans` пишет план.
3. План ревьюится, выбирается метод исполнения; реализации сейчас нет.
4. Коммит — только если отдельно попросят.

Ограничение интеграции: в worktree уже есть обширная незакоммиченная работа identity-returning; она
не должна пересекаться по файлам с будущей реализацией typed-CTE.
