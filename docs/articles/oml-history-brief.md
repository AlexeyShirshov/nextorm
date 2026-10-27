# OML / WORM → Nextorm — исследовательский бриф

> Формат: тезисы, вопросы, ссылки. Не готовая статья. Связь проектов — со слов автора; сходство архитектуры само по себе не доказывает перенос кода.

## Авторское уточнение о преемственности (27.10.2026)

- Nextorm — **переписанный WORM на новой технологической базе**; по мнению автора, базовые концепции и основные приёмы в целом сохранились.
- Замена VB.NET / .NET Framework на C# / .NET Core; отказ от старого legacy-кода.
- Главный пересмотр в области производительности: **от кэширования почти всего (включая результаты) к кэшированию планов запросов**.
- Отдельное отличие, установленное ранее: **change tracking WORM не перенесён в Nextorm**; этим ограничена буквальность формулы «всё остальное сохранил».
- Продолжить исследование **от сходства концепций к конкретным парам реализаций**: запросная модель, построение SQL, маппинг, диалекты, тестовые use cases.
- **Нельзя выводить:** прямое копирование исходников либо полное совпадение API/возможностей без проверки.
- Дополнительные авторские тезисы: [почему WORM был переписан](author-notes-01-reasons-for-nextorm.md).

## Опорная хронология

- **23.05.2007:** [ранний коммит OML](https://github.com/AlexeyShirshov/oml/commit/2371f4bab9ce746717ae21d0054bc51e7c0ff93b) уже содержит [состояния и регистрацию модификаций](https://github.com/AlexeyShirshov/oml/blob/2371f4bab9ce746717ae21d0054bc51e7c0ff93b/obm/OrmBase.vb#L270-L355), [кэш и механизм инвалидирования](https://github.com/AlexeyShirshov/oml/blob/2371f4bab9ce746717ae21d0054bc51e7c0ff93b/obm/OrmCache.vb#L45-L78).
- **27.07.2009:** [срез OML](https://github.com/AlexeyShirshov/oml/commit/9df4b11d666ec5bfe1319774988c3be543cf89ef) уже содержит [LINQ-провайдер](https://github.com/AlexeyShirshov/oml/blob/9df4b11d666ec5bfe1319774988c3be543cf89ef/net3.5/linqprovider/Provider.vb), [тесты кэша](https://github.com/AlexeyShirshov/oml/blob/9df4b11d666ec5bfe1319774988c3be543cf89ef/TestProject1/Query/Cache.vb).
- **06.12.2015:** [cross joins and save graph dependency](https://github.com/AlexeyShirshov/oml/commit/a8a2a5281d7583dfefe7307ecbe25559ac3952b4): работа с SQL и зависимостями сохранения.
- **15.05.2020:** [Relation refactoring](https://github.com/AlexeyShirshov/oml/commit/886f29cda495ae08b3e66c922d68f6e0a54bb8e3): поздний срез для анализа.
- **20.09.2023:** [ранний README Nextorm](https://github.com/AlexeyShirshov/nextorm/blob/7ee4651280ed2f902297e14bf562d89cea281a30/readme.md): генерация SQL, типизация C#, необязательные сущности.
- **09.12.2023:** [README Nextorm](https://github.com/AlexeyShirshov/nextorm/blob/40c792cb09ffb6713da60d3c19f01384d76b2de2/readme.md): добавлена цель «as fast as Dapper».
- **05.01.2024:** [«Постановка задачи»](https://github.com/AlexeyShirshov/nextorm/blob/5e6e6e8b06bbebc274d7da4970cbe249dfe9e7fc/nextorm.core/docs/ru/motivation.md): три подхода к SQL и маппингу.
- **2026:** [современный pipeline Nextorm](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/ru/engine/index.md): кэш планов, Prepare, материализация; без change tracking.

**Оговорка:** GitHub-репозиторий OML создан в 2017 году, но сохранённая история коммитов уходит в 2007-й. Даты срезов — не обязательно даты первого появления функций.

## Архитектурные линии и свидетельства

### 1. Управление состояниями и сохранение изменений

- **OML:** [`Entity.vb`: ObjectState](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/Entity/Entity.vb#L365-L402), [`CachedEntity.vb`: SaveChanges](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/Entity/CachedEntity.vb#L1240-L1253), [вставка/обновление/удаление](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/Entity/CachedEntity.vb#L1453-L1495).
- **Сценарии:** [`ModificationsTracker`, принятие изменений и batch](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/TestRealSchema/TestTracker.vb#L140-L210).
- **Nextorm:** [отказ от change tracking и явные DML-команды](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/ru/comparisons/index.md).
- **Вопрос:** какие проблемы реального использования привели к упрощению модели?

### 2. Кэширование результатов vs кэширование планов

- **OML:** [`OrmCache.vb`](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/Cache/OrmCache.vb), [`CacheItem.vb`](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/Cache/CacheItem.vb).
- **Тест:** [два чтения с CacheHit=False/True, изменение сущности, затем CacheHit=False](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Query/Cache.vb#L168-L197).
- **Дополнительные кейсы:** [DeferredCache](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Query/Cache.vb), [ImmCache](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Query/ImmCache.vb), [SmartCache](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Query/SmartCache.vb).
- **Nextorm:** [кэш SQL-планов/мапперов и повторная отправка запросов в БД](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/ru/engine/index.md); **не кэш результатов**.
- **Вопросы:** цена инвалидирования? транзакции и согласованность? причины отказа?

### 3. Модель запроса и типизированные выражения

- **OML:** [`QueryCmd`](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/QueryCmd/QueryCmd.vb), [SQL-билдеры](https://github.com/AlexeyShirshov/oml/tree/Worm2010/obm/Query/Builders), [выражения](https://github.com/AlexeyShirshov/oml/tree/Worm2010/obm/Query/Expressions).
- **OML:** [`IQueryProvider` и `QueryVisitor`](https://github.com/AlexeyShirshov/oml/blob/Worm2010/net3.5/linqprovider/Provider.vb#L45-L82); [LINQ-тесты](https://github.com/AlexeyShirshov/oml/blob/Worm2010/net3.5/TestLinq/TestLinq.vb).
- **Nextorm:** [From/EntityBuilder → QueryCommand → QueryPreparer → SQL и маппер](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/ru/engine/index.md).
- **Вопрос:** какие принципы сохранились, а какие были заменены при переходе к C#?

### 4. Маппинг, связи, провайдеры

- **OML:** [MappingEngine](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/Mapping/MappingEngine.vb), [M2M-тесты](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Relations/TestM2M.vb), [наследование](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Advanced/Inheritance.vb).
- **OML:** [SQL-генератор](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/StmtGenerators/StmtGenerator.vb), [MSSQL](https://github.com/AlexeyShirshov/oml/tree/Worm2010/obm/StmtGenerators/db/MSSQL), [MySQL](https://github.com/AlexeyShirshov/oml/tree/Worm2010/MySQL).
- **Nextorm:** [необязательный класс сущности, проекции и SQL-диалекты](https://github.com/AlexeyShirshov/nextorm/blob/main/readme.md).
- **Вопрос:** какие формы маппинга/связей оказались удобными, а какие — слишком связанными с жизненным циклом сущности?

## Масштаб тестового материала (статический подсчёт TestMethod; тесты не запускались)

- [Cache.vb](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Query/Cache.vb) — 17.
- [ImmCache.vb](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Query/ImmCache.vb) — 17.
- [SmartCache.vb](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Query/SmartCache.vb) — 9.
- [TestLinq.vb](https://github.com/AlexeyShirshov/oml/blob/Worm2010/net3.5/TestLinq/TestLinq.vb) — 48.
- [TestTracker.vb](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/TestRealSchema/TestTracker.vb) — 6.
- Только эти пять файлов: **97 атрибутов `TestMethod`**, не общее количество тестов проекта.

## Осторожность в выводах

- **Подтверждено:** функции и тестовые кейсы в OML; отличие кэша результатов OML от кэша планов Nextorm; текущий отказ от tracking.
- **Со слов автора:** OML — отправная точка для Nextorm.
- **Не установлено:** прямая преемственность исходного кода, первенство относительно EF Core, точные причины отдельных архитектурных изменений, внешние production use cases.
- **Для авторского текста:** опыт проблем, цена решений, причины упрощений и последствия компромиссов — дополнить лично.

См. [план первой статьи](01-problem-statement.md) и [общую карту источников](sources.md).
