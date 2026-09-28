# TODO: динамические колонки (dynamic columns store)

> **Статус (2026-09-28, линия `1.0.9-a`):** стороны **чтения и записи** отгружены и
> верифицированы. Сторона записи реализована для `INSERT`/`UPDATE`/`MERGE` в объёме issue
> [#104](https://github.com/AlexeyShirshov/nextorm/issues/104) (милстоун `1.1`); отложенные
> оптимизации/кандидаты остаются открытыми в том же трекинге. Публичное поведение и оставшиеся
> ограничения опубликованы в `docs/advanced/limitations.md` и `docs/ru/advanced/limitations.md`.
> План остаётся частично реализованным (см. `Deferred`).

> Tracking issue: [#94](https://github.com/AlexeyShirshov/nextorm/issues/94).

## Статус

> **2026-09-28, линия `1.0.9-a`:** сторона **чтения** реализована и перенесена из ветки
> `wip/94-dynamic-columns` (коммит `92fa94f`), сторона **записи** реализована в объёме issue
> [#104](https://github.com/AlexeyShirshov/nextorm/issues/104). Критерии приёмки 1, 2, 3, 5 закрыты
> (см. §4, §7); критерий 4 (набор ключей как ключ плана) для чтения закрыт схемой, а DML пока не
> кэшируется. Публичное поведение и оставшиеся ограничения отражены в `docs/advanced/limitations.md`
> и `docs/ru/advanced/limitations.md`. Отложенные оптимизации и «кандидаты», ждущие воспроизведения,
> собраны в разделе `Deferred` ниже.

> Рабочий план (design RFC). Источник: сравнение инфраструктуры маппинга linq2db — атрибуты
> `DynamicColumnsStoreAttribute` / `DynamicColumnAccessorAttribute` и fluent
> `DynamicColumnsStore()` / `DynamicPropertyAccessors()`. В nextorm маппинг фиксирован набором
> объявленных свойств; неизвестные колонки не читаются и не пишутся.

## 1. Пункт и цель

- **Фича:** сущность может отдать «лишние» колонки строки в словарь (`IDictionary<string, object?>`), ключи
  которого определяются схемой во время выполнения, а не CLR-свойствами; на запись ключи словаря
  рендерятся колонками.
- **Критерий приёмки:**
  1. свойство-хранилище помечается атрибутом/fluent и наполняется при чтении всеми колонками, не
     сопоставленными объявленным членам;
  2. `INSERT`/`UPDATE`/`MERGE` записывают ключи словаря как колонки;
  3. имена колонок квотируются (инъекция исключена, только доверенный источник/allow-list);
  4. набор колонок входит в ключ плана (разные наборы → разные планы);
  5. in-memory — тот же словарь при чтении.

**Реализованный срез:** критерии 1, 2, 3 и 5 — стороны **чтения** и **записи**. Критерий 4 (набор
ключей как ключ плана): для чтения набор определяет схема (не запрос), а DML в этой линии не
кэшируется — при введении кэша отсортированный набор ключей обязан войти в ключ плана. См. §7 и
`docs/advanced/limitations.md`.

## 2. Провайдерная матрица (чтение и запись)

Поведение не зависит от SQL-функций: колонки читаются из `IDataRecord` по имени (`GetName`/`GetValue`),
сопоставленные колонки читаются по порядковому номеру, `*` добавляется после них. Диалектного хука не
нужно. Источники: официальные справочники `SELECT`/`*` каждого движка (PostgreSQL, Microsoft Learn
T-SQL, MySQL/MariaDB, SQLite, ClickHouse) — все поддерживают список колонок вместе с `*`.

Запись также не зависит от SQL-функций: ключи словаря сортируются порядково и рендерятся физическими
колонками (`INSERT`/`UPDATE`/`MERGE`), значения связываются параметрами из рантайм-CLR-значения
(pass-through, без конвертеров/JSON), каждый ключ квотируется `dialect.QuoteIdentifier` независимо от
глобальной опции `UseQuotedIdentifiers`, а соглашение об именовании пропускается. Поведение общее для
всех SQL-провайдеров через один внутренний шов на существующем DML-пути; in-memory сторону записи не
реализует.

| Провайдер | Чтение | Запись | Примечание |
|---|---|---|---|
| SQL Server | **да** | **да (INSERT/UPDATE/MERGE)** | `select <mapped>, *`; запись квотирует динамические ключи диалектом независимо от глобального quoting |
| PostgreSQL | **да** | **да (INSERT/UPDATE/MERGE)** | `select <mapped>, *`; тип значения — из `DbDataReader`/рантайм-CLR |
| MySQL | **да** | **да (INSERT/UPDATE/MERGE)** | `select <mapped>, *` |
| MariaDB | **да** | **да (INSERT/UPDATE/MERGE)** | наследует MySQL |
| SQLite | **да** | **да (INSERT/UPDATE/MERGE)** | `select <mapped>, *`, dynamic typing |
| ClickHouse | **да** | **да (INSERT/UPDATE/MERGE)** | колонки таблицы должны существовать |
| InMemory | **да** | **нет** | провайдер возвращает зарегистрированную строку как есть, поэтому возвращается её собственный словарь; запись не реализована |

**Единообразие провайдеров:** чтение реализовано через общий путь
(`QueryCommand.QueryPreparer.PrepareColumns` → `SqlBuilder.MakeSelect` → `RowMapperFactory` →
`RowMaterializerBuilder`/`DynamicColumns`), поэтому все SQL-провайдеры получают его одновременно;
in-memory — через путь identity-материализации. Запись реализована общим внутренним швом на DML-пути
(`INSERT`/`UPDATE`/`MERGE`) для всех SQL-провайдеров; in-memory остаётся без стороны записи.

## 3. C#-аналог и tier

Аналог linq2db: `DynamicColumnsStoreAttribute` + `DynamicColumnAccessorAttribute`, fluent
`EntityMappingBuilder<T>.DynamicColumnsStore(...)`. Tier **b**: новый атрибут + слот в метаданных +
поддержка в row reader. Имя `DynamicColumnsAttribute` согласовано с `ColumnAttribute` и реестром
`docs/specs/design/API-NAMING-REVIEW.md`.

## 4. Публичный API (реализовано)

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DynamicColumnsAttribute : Attribute;

bool IPropertyMetadata.IsDynamicColumnsStore => false;          // default interface member
IPropertyMetadata? IEntityMetadata.DynamicColumnsStore => null; // default interface member

EntityPropertyBuilder<T> EntityPropertyBuilder<T>.DynamicColumnsStore();
```

- Хранилище исключено из `IEntityMetadata.Properties` (чтобы обычные мапперы/DML его не трогали) и
  доступно через `IEntityMetadata.DynamicColumnsStore`.
- Тип проверяется: `property.PropertyType.IsAssignableFrom(typeof(Dictionary<string, object?>))`
  (`DynamicColumnsTypeFacts.IsStoreType`); обязателен сеттер; нельзя совмещать с `HasColumnName`,
  конвертером, JSON-колонкой или `RangeColumns`.
- Чтение: `RowMaterializerBuilder` при наличии хранилища переходит на member-init с параметрless ctor
  и биндит хранилище к `DynamicColumns.Read(IDataRecord, startIndex, mappedNames)`.
- SQL: `SqlBuilder.MakeSelect` рендерит `*` для маркера `SelectExpression.IsDynamicColumnsStore`.
- План-ключ: маркер участвует в `SelectExpressionPlanEqualityComparer` (Equals/GetHashCode) и,
  значит, в `ColumnsPlanHash`; запрос с хранилищем не делит план с запросом без него.

## 5. План тестов

- Core/in-memory: `tests/nextorm.core.tests/DynamicColumnsInMemoryTests.cs` — возврат словаря
  зарегистрированной строки.
- SQLite (реальная temp-file БД, без контейнеров):
  `tests/nextorm.sqlite.tests/DynamicColumnsTests.cs` — материализация «лишних» колонок (`age`/`city`),
  `NULL`, отсутствие `id`/`name` в словаре + SQL `select id, name, * from dynamic_entity`.
- SQL-gen по провайдерам: `DynamicColumnsStore_ShouldAppendStar` в
  `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/SqlGenerationTests.cs` —
  `select id, * from dynamic_entity`.
- Сторона записи (`INSERT`/`UPDATE`/`MERGE` из ключей словаря) покрыта unit/SQL-gen и
  интеграционными SQLite-тестами write→read — стратегия и имена в
  `docs/specs/status/dynamic-columns-write-104-1.md`.
- Отложено (не реализовано): join → `NotSupportedException`; план по набору ключей (DML не кэшируется;
  при введении кэша отсортированный набор ключей обязан войти в ключ).

## 6. Baseline покрытия

Тесты (полный прогон `dotnet run --project tests/nextorm.<p>.tests -c Debug`, без контейнеров): до —
core 400, sqlite 514, postgres 521, sqlserver 388, mysql 168, mariadb 93, clickhouse 338; после —
core **402**, sqlite **519**, postgres **522**, sqlserver **389**, mysql **169**, mariadb **94**,
clickhouse **339** (все зелёные). Покрытие линии после изменения (dotnet-coverage по core/sqlite/
postgres/sqlserver, `-s coverage.settings.xml`): **line 75.8 %**, branch 69.8 % — выше порога
`MIN_LINE_COVERAGE=75`; baseline «до» в этой сессии не снимался (изменение аддитивное + новые тесты в
`nextorm.core`/`nextorm.sqlite`, покрытие не должно упасть). Публичная поверхность: +1 тип
(`DynamicColumnsAttribute`), +1 метод (`EntityPropertyBuilder<T>.DynamicColumnsStore`), +2
default-члена интерфейсов (`IPropertyMetadata.IsDynamicColumnsStore`,
`IEntityMetadata.DynamicColumnsStore`), +1 init-свойство `PropertyMetadata`, +1 свойство
`EntityMetadata` и 2 internal-свойства `SelectExpression` (в публичную поверхность не входят).

## 7. Статус реализации

**DONE (read-side + write-side #104).** Read-side портирован на релизную линию `1.0.9-a` из
`wip/94-dynamic-columns` (commit `92fa94f`, 2026-09-27). Write-side реализован для
`INSERT`/`UPDATE`/`MERGE` в объёме issue [#104](https://github.com/AlexeyShirshov/nextorm/issues/104);
поведение подтверждено тестами и сборкой.

- **Реализовано (read):** атрибут + fluent + слоты метаданных; исключение хранилища из `Properties`;
  маркер в select-list; `select <mapped>, *`; материализация не сопоставленных колонок в словарь;
  in-memory pass-through; план-ключ; XML-доки.
- **Реализовано (write, #104):** ключи словаря рендерятся физическими колонками, значения — связанными
  параметрами в `INSERT`/`UPDATE`/`MERGE`; порядковая сортировка ключей (детерминированный порядок
  колонок); квотирование каждого ключа диалектом независимо от глобальной опции
  `UseQuotedIdentifiers`, соглашение об именовании пропущено (ключ = имя физической колонки);
  отклонение пустого ключа и ключа с NUL; present-ключ со значением `null` → SQL `NULL`, отсутствующий
  ключ → колонка опущена (`INSERT` — default, `UPDATE`/`MERGE` — без изменений, очистить нельзя);
  равенство набора ключей во всех строках многострочного `INSERT`; в `MERGE` динамические колонки
  только в source/`INSERT`/`SET`, никогда в `ON`; pass-through рантайм-CLR-значения без
  конвертеров/JSON; store-less DML байт-в-байт прежний.
- **Отложено:** пер-ключевой `IPropertyValueConverter`/JSON-колонки; change tracking и «очистка
  опусканием ключа»; сторона записи в in-memory; allow-list ключей; DML-кэш планов (при его введении
  отсортированный набор ключей должен войти в ключ); хранилище при join/проекции/коррелированном
  подзапросе (сознательно `NotSupportedException`/не собирается); `DynamicColumnAccessor`-доступ по
  имени.
- **Решение:** read-side и write-side (`INSERT`/`UPDATE`/`MERGE`) закрыты end-to-end; оставшиеся
  пункты документированы как ограничения в `docs/advanced/limitations.md` (+RU); трекинг — issue #104.

## 8. Открытые вопросы (write-side) — решения

1. **Тип словаря на запись:** реализован вывод из рантайм-CLR-значения (pass-through); пер-ключевой
   `IPropertyValueConverter` отложен до подтверждённого типизированного сценария.
2. **`null` vs отсутствие ключа:** present-ключ со значением `null` пишет SQL `NULL`, отсутствующий
   ключ опускает колонку. Соглашение зеркалит сторону чтения; семантика «очистить колонку отсутствием
   ключа» отложена.
3. **Взаимодействие с value converters и JSON-колонками:** pass-through, без конвертеров/JSON — как и
   материализация стороны чтения.
4. **Квотирование имён-ключей:** каждый ключ квотируется диалектом всегда, плюс валидация имени
   (пустой ключ/NUL отклоняются); allow-list отложен до требования о недоверенном источнике ключей.

## Deferred

Отложенные оптимизации и «кандидаты» (нужное воспроизведение до промоушена). Каждый пункт —
с триггером пересмотра; без него работа не берётся.

### Deferred (in-milestone 1.0.9-a)

Принятые в объём милстоуна `1.0.9-a` тест-матричные follow-up стороны записи #104 (инвариант:
ничего не покидает милстоун — эти пункты не выносятся в отдельную веху).

- **Match condition на настоящей generated-колонке в query-source.** `Merge_QuerySource_OnIdentityKey_ShouldRender`
  (`tests/nextorm.postgres.tests/MergeSqlGenerationTests.cs:371`,
  `tests/nextorm.sqlserver.tests/MergeSqlGenerationTests.cs:252`) использует колонку только с `[Key]`,
  но не настоящую `[DatabaseGenerated(Identity)]`; добавить случай match-condition на реально
  generated-колонке в query-source.
- **Ветви generated-колонки у VALUES-source.** Тест generated-колонки у VALUES-source покрывает только
  `WhenMatched.ThenUpdate`; покрыть также ветви `ThenDelete`/`ThenDoNothing` и ссылку на источник через
  вложенный/методный вызов (`s.Total.ToString()`, `a && s.Total`).

- **Аллокация `Normalize` на строку** — `src/nextorm.core/DataContext/DynamicColumns.cs:65`
  (`IsMapped` вызывает `Normalize(name)` на каждое немаппированное поле). Оптимизация opt-in:
  браться только после измерения пропускной способности/аллокаций на строках с dynamic-columns.
  Триггер: измеримый per-row overhead на бенчмарке динамических строк.
- **Скан select-list на детект хранилища при каждой сборке маппера** —
  `src/nextorm.core/DataContext/RowMaterializerBuilder.cs:52-60`. Триггер: воспроизводимая
  регрессия времени сборки мапперов (build-time), а не единичная строка.
- **Словарь хранилища использует `StringComparer.Ordinal`** (точный регистр имени колонки) —
  `DynamicColumns.cs:43`. Текущее поведение — документированный контракт (ключи как приходят из
  набора результатов). Триггер: если обещаем канонические ключи (регистро-/snake-case-нормализация).
- **Хранилищу нужен parameterless-ctor** — документированное ограничение
  (`docs/advanced/limitations.md`, EN+RU). Триггер: требование support'а сущностей без
  parameterless-ctor со стором.
- **Кандидаты, которым нужно воспроизведение до промоушена:**
  - null-forgiving NRE — `RowMaterializerBuilder.cs:178` (`resultType.GetProperty(...)!`);
  - порядок/границы «стор последним» — `QueryCommand.QueryPreparer.cs:463-472`,
    `RowMaterializerBuilder.cs:169`;
  - рендер «голой» `*` — `SqlBuilder.cs:459-492`;
  - `PhysicalColumnName` для range-пар — `EntitySelectListBuilder.cs:69`.
- **Пре-существующий, не связанный с этим портом:** баг выбора самого длинного ctor в
  `RowMaterializerBuilder.BuildMemberInit` — `RowMaterializerBuilder.cs:199`; воспроизведён на
  `HEAD` до порта, в объём #94 не входит. Триггер: отдельная задача/замер при планировании работ.
