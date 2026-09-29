# Качество решений #94 — 4-way (a1 / a2 / a3 / upstream)

Слои: **L1** приёмочные тесты (свои + портированные из upstream), **L2** two-tier-судья (scout flash → слепой brains Sol), **L3** two-tier-аудит. Имена API свободны (L1-порт — только переименования).

## L1 — поведение

| вариант | свои core | свои sqlite | порт core | порт sqlite | src тронут | портировано |
|---|---|---|---|---|---|---|
| a1 | ok (574) | ok (613) | — | — | 0 | 0 |
| a2 | ok (575) | ok (611) | FAIL (577) | FAIL (611) | 0 | 8 |
| a3 | ok (582) | ok (624) | FAIL (583) | FAIL (627) | 0 | 8 |
| upstream | ok (584) | ok (607) | — | — | 0 | 0 |

## L2 — рубрика (two-tier: scout flash → brains Sol)

| вариант | R1 | R2 | R3 | R4 | R5 | R6 | correctness | design | api | tests | docs | scope | total |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| a1 | 2 | 2 | 0 | 2 | 1 | 2 | 3 | 3 | 4 | 3 | 4 | 3 | 72 |
| a2 | 2 | 1 | 0 | 0 | 1 | 2 | 2 | 2 | 4 | 3 | 3 | 2 | 51 |
| a3 | 2 | 1 | 0 | 2 | 1 | 2 | 3 | 2 | 4 | 4 | 3 | 3 | 65 |
| upstream | 2 | 2 | 1 | 2 | 1 | 2 | 3 | 3 | 4 | 3 | 4 | 3 | 78 |

## L3 — аудит

| вариант | P0 | P1 | P2 |
|---|---|---|---|
| a1 | 5 | 5 | 3 |
| a2 | 4 | 1 | 2 |
| a3 | 2 | 3 | 9 |
| upstream | 2 | 3 | 3 |

## Механика

| вариант | rc | wall, s | cost | files | tests | overlap | skills |
|---|---|---|---|---|---|---|---|
| a1 | 0 | 980 | 0.197556 | 32 | 2 | 19/32 | нет |
| a2 | 0 | 1770 | 0.214445 | 37 | 7 | 19/37 | implementing-todo-features,running-integration-tests |
| a3 | 0 | 4684 | 0.094795 | 55 | 19 | 16/55 | pdca-dotnet,nextorm-pdca |

## Детали

### a1

- L2 total=72: Свойство-хранилище исключено из обычного маппинга и наполняется несопоставленными полями; SQL добавляет `*` после отображаемых колонок, а маркер участвует в сравнении и хеше плана [evidence §R1, src/nextorm.core/DataContext/Meta/Implementation/EntityMetadata.cs:15-45; evidence §R1, src/nextorm.core/DataContext/Meta/DynamicColumnsStore.cs:285-317; evidence §R2, src/nextorm.core/DataContext/SqlBuilder.cs:456-500; evidence §R4, src/nextorm.core/Query/QueryPlanEqualityComparer.cs:139-144,394-395]. Главный пробел — отсутствие понятной ошибки для join и явной проекции: по чтению кода хранилище в этих случаях молча пропускается; исполняемого теста этих путей нет [evidence §R3, src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:487-490; evidence §S1, evidence-a1-l2.md:277-329]. Запись INSERT/UPDATE/MERGE реализована, но in-memory чтение лишь сохраняет уже имеющийся словарь, а не наполняет его из колонок [evidence §R6, evidence-a1-l2.md:205-211; evidence §R5, evidence-a1-l2.md:199-203].
  - P1: src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:487-490 — join и явная проекция не отклоняются понятной ошибкой: план хранилища не устанавливается, и оно молча пропускается; проверяющих тестов нет [evidence §R3, evidence-a1-l2.md:165-178; evidence §S1, evidence-a1-l2.md:277-329].
  - P1: src/nextorm.core/DataContext/Meta/DynamicColumnsStore.cs:137-159 — ключ плана учитывает маркер и объявленные имена, но не набор ключей словаря во время выполнения; разные наборы дают разный SQL записи, а не разные ключи плана [evidence §R4, evidence-a1-l2.md:180-197; evidence §Scope, evidence-a1-l2.md:233-241].
  - P1: src/nextorm.core/Builders/MergeBuilder.InMemory.cs:59-60 — in-memory merge копирует элементы в словарь, но in-memory чтение возвращает заранее заполненный словарь без наполнения из колонок [evidence §R5, evidence-a1-l2.md:199-203].
- L3:
  - P0 src/nextorm.core/Builders/UpdateBuilder.cs:119-132 — ключ хранилища, совпадающий с обычной колонкой, добавляет второй SET той же колонки вместо замены существующего назначения. [evidence §1.11, src/nextorm.core/Builders/UpdateBuilder.cs:85-132]
  - P0 src/nextorm.core/Builders/MergeBuilder.Columns.cs:30-34 — динамические ключи обходят фильтры ключевых, identity- и вычисляемых колонок при записи; аналогичный обход есть в INSERT и UPDATE. [evidence §1.12, src/nextorm.core/Builders/MergeBuilder.Columns.cs:17-34; src/nextorm.core/Builders/InsertBuilder.Values.cs:124-179; src/nextorm.core/Builders/UpdateBuilder.cs:85-117]
  - P0 src/nextorm.core/DataContext/RowMapperFactory.cs:220-225 — общий для процесса кэш raw-мапперов не включает план динамических колонок в ключ: при совпадении остальных полей он возвращает делегат с планом другого вызова. [evidence §11.2, src/nextorm.core/DataContext/MapperCache.cs:23-43; src/nextorm.core/DataContext/RowMapperFactory.cs:220-225,275-298]
  - P0 src/nextorm.core/DataContext/RowMapperFactory.cs:298 — при материализации struct сущность передаётся заполнителю в boxed-копии; созданное для null хранилище устанавливается в копию, а возвращается исходное значение. [evidence §11.3, src/nextorm.core/DataContext/RowMapperFactory.cs:292-300; src/nextorm.core/DataContext/Meta/DynamicColumnsStore.cs:294-306]
  - P0 src/nextorm.core/DataContext/Meta/DynamicColumnsStore.cs:276 — допустимый по проверке конкретный тип хранилища без открытого конструктора без параметров приводит к исключению при обещанном создании хранилища из null. [evidence §3.6, src/nextorm.core/DataContext/Meta/DynamicColumnsStore.cs:118-127,271-277; evidence §4.4, docs/guide/31-dynamic-columns.md:66]
  - P1 src/nextorm.core/DataContext/Meta/EntityPropertyBuilder.cs:240-275 — fluent-объявление хранилища молча отбрасывает ранее заданные параметры маппинга, тогда как атрибутный путь отвергает несовместимые сочетания и документация обещает отказ. [evidence §2.3, src/nextorm.core/DataContext/Meta/EntityPropertyBuilder.cs:240-275; src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:76-80; docs/guide/31-dynamic-columns.md:44-46]
  - P1 src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:487 — явная проекция на сущности с хранилищем молча отключает его заполнение вместо указанного в критерии R3 отказа. [evidence §7.4, src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:487; ref/issue94.md:17]
  - P1 src/nextorm.core/DataContext/Meta/DynamicColumnsStore.cs:22-23 — DynamicColumnMetadata выдаёт свойство словаря как PropertyInfo отдельной динамической колонки, хотя контракт IPropertyMetadata описывает его как CLR-свойство, отображаемое на ColumnName. [evidence §3.4, src/nextorm.core/DataContext/Meta/DynamicColumnsStore.cs:16-23; src/nextorm.core/DataContext/Meta/IPropertyMetadata.cs:24-31]
  - P1 tests/nextorm.sqlite.tests/DynamicColumnsSqlGenerationTests.cs:25-46 — новые тесты чтения используют только заранее созданные словари и не проверяют документированное создание хранилища из null; соответствующие ветви Fill и CopyInto остаются непроверенными. [evidence §4.4, src/nextorm.core/DataContext/Meta/DynamicColumnsStore.cs:251-262,297-307; docs/guide/31-dynamic-columns.md:66]
  - P1 tests/nextorm.sqlite.tests/DynamicColumnsSqlGenerationTests.cs:1 — SQL-форма новой функции проверена только на SQLite: тестов генерации для остальных провайдеров в изменениях нет, а их выполнение не проводилось. [evidence §7.5, tests/nextorm.sqlite.tests/DynamicColumnsSqlGenerationTests.cs; evidence §10, ref/upstream-94.diffstat.txt]
  - P2 src/nextorm.core/Builders/InsertBuilder.Values.cs:165-179 — алгоритм AddDynamicColumns построчно продублирован в MergeBuilder; изменения построения колонок придётся синхронизировать в двух местах. [evidence §1.4, src/nextorm.core/Builders/InsertBuilder.Values.cs:165-179; src/nextorm.core/Builders/MergeBuilder.cs:100-114]
  - P2 src/nextorm.core/Builders/InsertBuilder.Values.cs:171-175 — запись повторяет рефлексивное получение хранилища для каждой пары строка–ключ после уже выполненного обхода хранилищ; та же схема есть в MERGE. [evidence §4.5, src/nextorm.core/Builders/InsertBuilder.Values.cs:171-175; src/nextorm.core/Builders/MergeBuilder.cs:106-110; src/nextorm.core/DataContext/Meta/DynamicColumnsStore.cs:165-166,204-235]

### a2

- L2 total=51: Атрибут и fluent-настройка выделяют хранилище, а raw-чтение наполняет его несопоставленными колонками; INSERT, UPDATE и MERGE также реализованы [evidence §R1, src/nextorm.core/DataContext/RawMapperFactory.cs:231-270; evidence §R6, src/nextorm.core/Builders/InsertBuilder.Values.cs:152-162]. Однако обычный SELECT не добавляет `*`, join и явная проекция не отклоняют хранилище, а маркер не входит в ключ LINQ-плана [evidence §Addendum 1.2, evidence-a2-l2.md:613-635; evidence §R3, evidence-a2-l2.md:218-231; evidence §R4, evidence-a2-l2.md:259-272]. Тесты с контейнерами прошли для PostgreSQL, SQL Server, MySQL и SQLite, но соответствующий тест ClickHouse отсутствует [evidence §Addendum 1.5, evidence-a2-l2.md:677-693].
  - P1: src/nextorm.core/DataContext/RawMapperFactory.cs:231 — словарь заполняется на raw-пути, но обычный запрос всей сущности выдаёт `select id, name from probe_dynamic_entity` без `*`; заявленный в документации `PrepareFromSql` также не проходит через этот путь [evidence §Addendum 1.2, evidence-a2-l2.md:613-635; evidence §Scope facts, evidence-a2-l2.md:413-422].
  - P1: src/nextorm.core/Builders/MergeBuilder.Columns.cs:16-18 — ошибка предусмотрена для MERGE с query source, но не для join или явной LINQ-проекции с хранилищем [evidence §R3, evidence-a2-l2.md:200-231].
  - P1: src/nextorm.core/Query/QueryPlanEqualityComparer.cs:137 — маркера хранилища в ключе LINQ-плана нет; имена колонок входят лишь в отдельный ключ raw-маппера [evidence §R4, evidence-a2-l2.md:235-272].
  - P1: evidence-a2-l2.md:12-16 — изменение находится в индексе без коммита, хотя постановка требует один коммит [evidence §0, evidence-a2-l2.md:10-16; evidence §Addendum 1.1, evidence-a2-l2.md:488-491].
- L3:
  - P0 src/nextorm.core/Builders/InsertBuilder.Values.cs:152 — Динамический ключ, совпадающий с уже сопоставленной колонкой, добавляется без проверки; INSERT, MERGE и UPDATE сохраняют оба вхождения и выводят их в SQL [evidence §D1.1, InsertBuilder.Values.cs:124-162; evidence §D1.3, SqlMutationBuilder.cs:1043-1059, QueryPlanner.cs:248-254].
  - P0 src/nextorm.core/DataContext/Meta/Implementation/DynamicColumnMetadata.cs:73 — Ключи `Tag` и `tag` считаются разными при сборке записи, поэтому оба имени могут попасть в список колонок SQL; проверки такой коллизии нет [evidence §1.11, DynamicColumnMetadata.cs:73; evidence §D1.2, DynamicColumnMetadata.cs:73; evidence §D1.3, SqlMutationBuilder.cs:1043-1059].
  - P0 src/nextorm.core/Builders/InsertBuilder.Values.cs:152 — Фильтр identity/computed применяется к сопоставленным колонкам, но не к ключам словаря: динамический ключ может снова добавить запрещённую для записи колонку; аналогичные обходы есть в UPDATE и MERGE [evidence §1.8, InsertBuilder.Values.cs:124-161, UpdateBuilder.cs:82-106, MergeBuilder.Columns.cs:22-27, MergeBuilder.cs:87-96].
  - P0 src/nextorm.core/DataContext/RawMapperFactory.cs:231 — Обещанное заполнение словаря через `PrepareFromSql` не реализовано: этот путь использует `RowMapperFactory` без передачи динамических колонок, тогда как их собирает только raw mapper [evidence §5.3, DynamicColumnsAttribute.cs:5-6, QueryPlanner.cs:570-573, RowMapperFactory.cs:150-163; evidence §2.4, ProcedureResult.cs:84,116].
  - P1 src/nextorm.core/DataContext/Meta/EntityPropertyBuilder.cs:253 — `DynamicColumns()` принимает совместно объявленные настройки свойства, включая `Key()` и `Identity()`, но построение метаданных затем исключает свойство и молча теряет эти настройки [evidence §1.9, EntityPropertyBuilder.cs:253-259, EntityMetadataBuilder.cs:41-46].
  - P2 src/nextorm.core/Builders/MergeBuilder.cs:87 — Цикл сбора и добавления динамических колонок продублирован в INSERT и MERGE [evidence §1.4, InsertBuilder.Values.cs:152-161, MergeBuilder.cs:87-96].
  - P2 src/nextorm.core/DataContext/RawMapperFactory.cs:18 — XML-документация утверждает, что несопоставленные колонки игнорируются, хотя при наличии словаря они собираются в него [evidence §5.2, RawMapperFactory.cs:18-21,231-272].

### a3

- L2 total=65: Хранилище размечается, исключается из обычного сопоставления и заполняется несопоставленными колонками при SQL-чтении; маркер участвует в ключе плана, а запись реализована для INSERT/UPDATE/MERGE [evidence §R1, src/nextorm.core/DataContext/RawMapperFactory.cs:146-165; evidence §R4, src/nextorm.core/Query/QueryPlanEqualityComparer.cs:59,348-351; evidence §R6, src/nextorm.core/Builders/InsertBuilder.cs:441-479, src/nextorm.core/Builders/UpdateBuilder.cs:103-116, src/nextorm.core/Builders/MergeBuilder.cs:412-450]. Однако чтение рендерит `select *` вместо требуемого списка сопоставленных колонок с `*`, а join и явная проекция обходят заполнение без ошибки [evidence §R2, src/nextorm.core/DataContext/SqlBuilder.cs:444-454; evidence §R3, src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:301-326]. Контейнерные динамические тесты независимо прошли 12/12 без пропусков, но не проверяют требуемое отклонение join [evidence §Follow-up 1 F1, evidence-a3-l2.md:210-231; evidence §Quality facts, evidence-a3-l2.md:110-116].
  - P1: src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:301-326 — join и явная проекция не отклоняются: хранилище молча остаётся незаполненным [evidence §R3, evidence-a3-l2.md:55-60].
  - P1: src/nextorm.core/DataContext/SqlBuilder.cs:444-454 — вместо `select <mapped>, *` выдаётся только `select *`; на этом пути нет имён колонок для проверки их квотирования [evidence §R2, evidence-a3-l2.md:37-50].
  - P1: src/nextorm.core/DataContext/InMemoryRowMaterializer.cs:32-37 — in-memory сохраняет уже имеющийся словарь, возвращая исходный объект, но отдельного наполнения хранилища нет [evidence §R5, evidence-a3-l2.md:75-79].
- L3:
  - P0 src/nextorm.core/DataContext/RawMapperFactory.cs:142 — разные наборы имён колонок могут получить один ключ кэша из-за неэкранированного разделителя U+001F; повторно использованный mapper будет соответствовать другой форме результата. [evidence §7 Q3, src/nextorm.core/DataContext/RawMapperFactory.cs:142]
  - P0 src/nextorm.core/DataContext/RawMapperFactory.cs:165 — чтение безусловно записывает данные в уже созданный store; допустимый по проверкам экземпляр словаря с запрещённой записью вызовет исключение. [evidence §7 Q4–Q7, src/nextorm.core/DataContext/RawMapperFactory.cs:158-165]
  - P1 tests/nextorm.core.tests/DynamicColumnsReadTests.cs:66 — нет теста поддержанного пути, где ни одна колонка reader не совпадает с объявленным свойством и все колонки попадают в store. [evidence §7 Q2, src/nextorm.core/DataContext/RawMapperFactory.cs:146-165; evidence §7 Q2, tests/nextorm.core.tests/DynamicColumnsReadTests.cs:66,82,102-103,154,200]
  - P1 tests/nextorm.core.tests/DynamicColumnsReadTests.cs:68 — тесты вызывают внутренний построитель mapper, но не проверяют ленивую обёртку и ветку выбора mapper для ExecuteRaw. [evidence §3.6 C25, src/nextorm.core/DataContext/RawMapperFactory.cs:91-92; evidence §4, tests/nextorm.core.tests/DynamicColumnsReadTests.cs:68]
  - P1 tests/nextorm.core.tests/DynamicColumnsReadTests.cs:53 — поддержанный поиск атрибута store на свойстве интерфейса не покрыт динамическими тестами. [evidence §3.2 C7, src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:78,143-145; evidence §3.2 C7, tests/nextorm.core.tests/DynamicColumnsReadTests.cs:53-60]
  - P2 src/nextorm.core/Builders/MergeBuilder.cs:414 — 37-строчный алгоритм AppendDynamicColumns дословно повторяет реализацию InsertBuilder, создавая две точки сопровождения. [evidence §3.2 C1, src/nextorm.core/Builders/MergeBuilder.cs:414-450; evidence §3.2 C1, src/nextorm.core/Builders/InsertBuilder.cs:443-479]
  - P2 tests/nextorm.integration.tests/Providers/MariaDbTestProvider.cs:87 — скопирован массив из 96 строк seed-данных MySQL-провайдера; изменения данных придётся синхронизировать. [evidence §3.2 C4, tests/nextorm.integration.tests/Providers/MariaDbTestProvider.cs:87-182]
  - P2 tests/nextorm.mariadb.tests/DynamicColumnsSqlGenerationTests.cs:1 — SQL-тесты почти дословно повторяют MySQL-файл, а вспомогательные методы и модель повторяются и в других проектах. [evidence §3.2 C5, tests/nextorm.mariadb.tests/DynamicColumnsSqlGenerationTests.cs:1-103]
  - P2 src/nextorm.core/DataContext/RawMapperFactory.cs:137 — правило построения ключа формы reader продублировано в BuildColumnsKey; два варианта нужно поддерживать согласованными. [evidence §3.2 C8, src/nextorm.core/DataContext/RawMapperFactory.cs:137-142,385-397]
  - P2 src/nextorm.core/DataContext/Meta/DynamicColumnsRules.cs:8 — методы внутреннего класса объявлены public, хотя извне сборки недоступны. [evidence §3.3 C12, src/nextorm.core/DataContext/Meta/DynamicColumnsRules.cs:6-8,52,71,81,107,124,152]
  - P2 src/nextorm.core/DataContext/Meta/DynamicColumnsRules.cs:137 — при каждой записи для каждого динамического ключа повторно вычисляются имена всех объявленных колонок. [evidence §3.4 C18, src/nextorm.core/DataContext/Meta/DynamicColumnsRules.cs:126-142]
  - P2 src/nextorm.core/DataContext/Meta/DynamicColumnsRules.cs:154 — получение store через PropertyInfo.GetValue выполняется для каждой строки пакетной записи. [evidence §3.4 C20, src/nextorm.core/DataContext/Meta/DynamicColumnsRules.cs:152-157; evidence §3.4 C20, src/nextorm.core/Builders/InsertBuilder.cs:452]

### upstream

- L2 total=78: TREE побайтово совпадает с эталонным `upstream-94-code.patch`; это подтверждает состав изменений, но не устраняет расхождения с критериями R3 и R5 [evidence §Additional facts (round 2), ref/upstream-94-code.patch:225-230; evidence §Additional facts (round 2), ref/issue94.md:281-284]. Чтение несопоставленных колонок, SQL со `*` и маркер в ключе плана реализованы, тогда как явная проекция не отклоняется, а in-memory возвращает исходный объект с уже заполненным словарём [evidence §R1, src/nextorm.core/DataContext/DynamicColumns.cs:40-54; evidence §R2, src/nextorm.core/DataContext/SqlBuilder.cs:456-464; evidence §R4, src/nextorm.core/Expressions/SelectExpressionPlanEqualityComparer.cs:52-54,88-90; evidence §R3, src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:302-449; evidence §R5, src/nextorm.core/DataContext/InMemoryRowMaterializer.cs:35-36]. Write-side не реализован, но его отсрочка обоснована в ограничениях EN и RU; отдельный эталонный follow-up с указанием задачи #104 в TREE отсутствует [evidence §R6, docs/advanced/limitations.md:62; evidence §R6, docs/ru/advanced/limitations.md:59; evidence §Additional facts (round 2), ref/upstream-94-docs.patch:285].
  - P1: src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:302-449 — join отклоняется, но явная проекция не отклоняет хранилище: документация описывает отсутствие сбора колонок, теста этого пути нет [evidence §R3, src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:302-449; evidence §Additional facts (round 2), docs/guide/32-dynamic-columns.md:265-269].
  - P1: src/nextorm.core/DataContext/InMemoryRowMaterializer.cs:35-36 — для целой сущности возвращается зарегистрированный объект; тест подтверждает сохранение заранее заполненного словаря, но не его наполнение провайдером [evidence §R5, src/nextorm.core/DataContext/InMemoryRowMaterializer.cs:35-36; evidence §R5, tests/nextorm.core.tests/DynamicColumnsInMemoryTests.cs:20-35].
- L3:
  - P0 src/nextorm.core/DataContext/RowMaterializerBuilder.cs:176-180 — метаданные принимают SortedDictionary-хранилище, но материализатор привязывает к его свойству результат типа Dictionary; несовместимость типов должна приводить к ArgumentException при построении выражения [evidence §Gap-fill round 1/Q3.1–Q3.6, src/nextorm.core/DataContext/RowMaterializerBuilder.cs:176-180].
  - P0 src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:73 — при fluent-хранилище поиск атрибутов пропускается: второй [DynamicColumns] на другом свойстве молча игнорируется вместо проверки двух хранилищ [evidence §Facts/C13, src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:73].
  - P1 src/nextorm.core/DataContext/Meta/EntityPropertyBuilder.cs:265-267 — проверка несовместимых настроек хранилища не включает DurationUnit и DurationPrecision; заданная через Duration настройка молча теряется [evidence §Facts/C37, src/nextorm.core/DataContext/Meta/EntityPropertyBuilder.cs:265-267].
  - P1 docs/ru/advanced/api-reference.md — из русской таблицы атрибутов удалены DecimalPrecisionAttribute, ValueConverterAttribute и JsonColumnAttribute, хотя английская таблица их сохраняет [evidence §Facts/C31, docs/ru/advanced/api-reference.md].
  - P1 tests/nextorm.integration.tests — для хранилища нет runtime-проверок с реальными SQL Server, PostgreSQL, MySQL и ClickHouse: новые проверки этих провайдеров сравнивают только SQL-строки, а материализацию проверяет SQLite [evidence §Gap-fill round 1/Q4.2–Q4.5, tests/nextorm.integration.tests].
  - P2 src/nextorm.core/DataContext/DynamicColumns.cs:56-89 — проверка каждого поля линейно обходит имена сопоставленных полей и для несовпавших создаёт нормализованную строку заново на каждой строке результата [evidence §Facts/C24–C25, src/nextorm.core/DataContext/DynamicColumns.cs:56-89].
  - P2 src/nextorm.core/DataContext/InMemoryRowMaterializer.cs:117-118 — добавленный accessor хранилища не достигается описанными in-memory сценариями: целая сущность идёт через identity, а явные проекции не получают store-запись [evidence §Facts/C21, src/nextorm.core/DataContext/InMemoryRowMaterializer.cs:117-118].
  - P2 BenchmarkDotNet.Artifacts/results/* — в коммит фичи попали 15 отслеживаемых машинно-зависимых отчётов бенчмарка, создающих шум в диффе [evidence §Facts/C35, BenchmarkDotNet.Artifacts/results/*].

