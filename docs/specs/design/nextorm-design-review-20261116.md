# Design-аудит nextorm — 16.11.2026

> **Этап реализации:** пользователь одобрил D1–D3, включая код и регрессионные тесты. Постоянный план и результаты — [nextorm-design-review-20261116-implementation.md](nextorm-design-review-20261116-implementation.md). Ограничения read-only/build-test запретов ниже относятся к историческим этапам аудита и восстановления; текущий этап разрешает только согласованные изменения D1–D3. Benchmark/DocFX/commit/push/merge по-прежнему не разрешены.

## Результат согласованной реализации D1–D3

**D1–D3 реализованы и прошли scoped build/unit проверки. Общий solution build остаётся BLOCKED; полностью зелёный solution gate не заявляется.** Исторические находки/счётчики ниже сохранены для происхождения решения, а не как список неисполненных рекомендаций.

- **D1 — реализовано:** lazy cache успешной проекции текущей позиции в `src/nextorm.core/DataContext/InMemory/InMemoryEnumerator.cs:116–132,193–200` и `InMemoryEnumeratorAdapter.cs:57–75`. DISTINCT и consumer используют один результат; повторный generic/non-generic Current возвращает его без вызова selector. Не-DISTINCT mapping остаётся lazy, identity path — прямым, selector failure — retryable; Init/новая позиция/завершение/dispose инвалидируют cache, существующие no-op Reset и unsupported Reset сохранены. Current вне валидной позиции по-прежнему не получает нового определённого контракта. **17/17** regression cases, включая действительно приостанавливающийся async-only source.
- **D2 — реализовано:** `src/nextorm.core/Sharding/FanOut/ShardQueryExecution.cs:8–88` — единый internal static источник пяти policies для `ShardQuery<TEntity>` и `ShardQuery<TEntity,TResult>`. Task.Run/Parallel.ForEach, позиции вектора, Ordinal grouping, Math.Clamp, same-owner sequence, gate ownership и cancellation/exception behavior оставлены прежними. Нет нового per-query execution object/интерфейса/public API. **17/17** regression cases, включая suspended async same-owner/overlap/MaxDegree, cancel/exception/gate и оба facade.
- **D3 — реализовано:** `src/nextorm.core/Visitors/Providers/ClickHouseIntervalUnits.cs:8–21` хранит одну приватную OrdinalIgnoreCase-карту восьми единиц. Roslyn refs подтвердил два source call sites: `ClickHouseNativeFunctionTranslator.cs:973`, `src/nextorm.clickhouse/ClickHouseDialect.cs:2613` (provider `ClickHouseScalarFunctions`; `ClickHouseScalars` — test alias). Принимаемые значения, compile-time-literal требование, сообщения ошибок, null/whitespace и 3/4-argument rendering не изменены. Расширенный date/time SQL-generation class — **15/15** focused tests: внутри проверяется полная матрица восьми units × lower/upper/mixed case × обе формы × оба entry paths и invalid/captured/null границы. Это не «15 новых тестов».

### Последние совместные проверки после D3

- `dotnet build src/nextorm.core/nextorm.core.csproj -c Debug` — **exit0, 0 warnings / 0 errors**, обе TFM net10.0/net11.0.
- `dotnet build src/nextorm.clickhouse/nextorm.clickhouse.csproj -c Debug` — **exit0, 0 warnings / 0 errors**, обе TFM.
- `dotnet test tests/nextorm.core.tests -c Debug` — **4328/4328**, failed0/skipped0, exit0.
- `dotnet test tests/nextorm.clickhouse.tests -c Debug` — **1538/1538**, failed0/skipped0, exit0.
- `dotnet build nextorm.slnx -c Debug` — **exit1, 0 warnings / 202 errors** в generated benchmark row-materializer code. Benchmark compiler выбирает NuGet `nextorm/1.0.9` через PostgreSQL example вместо workspace core; generator из workspace ожидает отсутствующие в выбранном assembly `IRowMaterializerProvider`/`RowMaterializerColumn`. Причина и resolved references: [D1-build-diagnosis.md](nextorm-design-review-20261116-evidence/D1-build-diagnosis.md). Первая финальная попытка прервана restart сервера без exit status; приведён результат завершившегося повтора.

Полные команды, red/green историю, первоначальные analyzer failures/timeout, результаты по шагам и журналы см. в [implementation-документе](nextorm-design-review-20261116-implementation.md), [D2-result.md](nextorm-design-review-20261116-evidence/D2-result.md) и [D3-result.md](nextorm-design-review-20261116-evidence/D3-result.md). Все важные итоги находятся в docs, не только в raw `.log`.

**Остатки:** dependency graph benchmark/example требует отдельного согласованного исправления; он не изменён ради зелёного gate. Live integration и benchmark execution не выполнялись; измеренного speedup/байтовой экономии нет. Вне D1–D3 cleanup/public API/package changes не делались. Рабочее дерево менялось параллельно: чужие изменения (включая позднюю правку JoinIntoStitcher) не откатывались и не включались в этот fix.

### Файлы реализации

Продуктовые изменения:

- `src/nextorm.core/DataContext/InMemory/InMemoryEnumerator.cs`
- `src/nextorm.core/DataContext/InMemory/InMemoryEnumeratorAdapter.cs`
- `src/nextorm.core/Sharding/FanOut/ShardQuery.cs`
- `src/nextorm.core/Sharding/FanOut/ShardQueryExecution.cs` (новый internal helper)
- `src/nextorm.core/Visitors/Providers/ClickHouseNativeFunctionTranslator.cs`
- `src/nextorm.core/Visitors/Providers/ClickHouseIntervalUnits.cs` (новый internal helper)
- `src/nextorm.clickhouse/ClickHouseDialect.cs`

Регрессионные тесты:

- `tests/nextorm.core.tests/InMemory/Enumeration/InMemoryEnumeratorProjectionCacheTests.cs` (новый)
- `tests/nextorm.core.tests/Sharding/FanOut/ShardQueryExecutionTests.cs` (новый)
- `tests/nextorm.core.tests/Sharding/FanOut/ShardQueryAsyncExecutionTests.cs` (новый)
- `tests/nextorm.clickhouse.tests/Functions/Sweeps/Scalar/T286bF04DateTimeFunctionSqlGenerationTests.cs`

Документы: этот отчёт, `nextorm-design-review-20261116-implementation.md` и evidence summaries/logs в `nextorm-design-review-20261116-evidence/`. Ведущий просмотрел собственный product/test diff и новые файлы, проверил Roslyn symbols/refs итоговых helpers; CRLF подтверждён для всех 11 C# файлов, tracked diff whitespace check — exit0. Публичные библиотечные сигнатуры и package/project files в этой реализации не менялись.

## Статус восстановления и происхождение данных

**Постоянный документ:** `docs/specs/design/nextorm-design-review-20261116.md`.

Отчёт восстановлен из сохранённого контекста той же сессии: в нём присутствуют текст первоначального итогового отчёта, выводы инструментов, синтаксическая сводка и ответы scout-сессий. Это восстановление документа, **не повторный полный аудит**.

- Изначальное хранение финала и прогресса только в `/tmp/opencode/` было ошибкой: каталог исчез после перезапуска машины. Новый итог и сведения о восстановлении хранятся только в этом внутреннем документе.
- Перед созданием проверено: целевой файл отсутствовал; чужой документ не перезаписан.
- При восстановлении проверены прежние пути: каталог `nextorm-design-review`, `nextorm-design-audit-20261116-7d41a9/audit-report.md`, `nextorm-design-audit-resumed-20261116/metadata.txt` и `nextorm-audit-inmemory-json-csv-evidence.md` под `/tmp/opencode/` отсутствуют. Их имена ниже — **историческое происхождение**, не действующие ссылки на доступные доказательства.
- Сохранены выводы, рекомендации, счётчики рецептов, охват каждого проекта и ограничения. Исторические данные помечены явно; отсутствие исходных файлов не заменено вымышленным новым прогоном.
- HEAD при восстановлении: `95250750e9e6c22aebd01695cd3d438dbe9f9589`. Это другая ревизия, чем исторические снимки аудита; старые числа не объявляются актуальными для неё.
- Узкая перепроверка D1–D3 и записи о JSON boxing завершена и выделена в разделе «Заново проверенные доказательства». Остальные счётчики, позиции и описание охвата относятся к прежнему аудиту, а не к новому прогону.
- Область остаётся прежней: продуктовые проекты `src/**`; без аудита `tests/**` и `examples/**`. Автоматические ссылки Roslyn на исключённые проекты не означают чтение их исходников.
- При восстановлении разрешено менять только этот отчёт. Продуктовый код, реестры, AGENTS и чужие правки не изменяются; build/test/benchmark/DocFX не запускаются, коммитов/push нет.

## Восстановленная прежняя сводка

**Итог прежнего аудита:** 3 подтверждённые находки — 2 🟡 Moderate, 1 ℹ️ Info. Подтверждённых 🔴 в проверенных участках не найдено; это не заключение об отсутствии критических дефектов во всём проекте.

Все **10/10** продуктовых проектов включались в широкий синтаксический проход; ручное/семантическое чтение было выборочным. Исчерпывающая семантическая проверка проекта **не заявлялась и не заявляется**.

Прежний аудит не менял код, конфигурацию или реестры репозитория, не создавал коммитов и не запускал nextorm build/test/BDN/DocFX. Сборка отдельного инструмента `Scanner.csproj`, упомянутая в старом артефакте, не была сборкой nextorm.

### Исторические снимки

- Синтаксический Roslyn scan: **520 tracked C# файлов / 172619 строк / 10 src-проектов**, parse errors **0**. Время старого отчёта: **15.11.2026 19:25 UTC**. Это инвентарь кандидатов, а не 520 семантически проверенных файлов.
- Восстановленные ранее metadata показывали HEAD `d931cf1de94052d4e7a543e8f18b14768670d4e0`; позднейшая команда в той же сессии — `d89cd43b9dc2a6aa74f591127f193c5eadd8628f`.
- Позднейшая историческая проверка путей/размеров: **520 tracked .cs / 172628 строк**, untracked src .cs — **0**; generator **3557** строк вместо **3548**, то есть +9 строк в `EntityMetadataGenerator.cs`. Остальные агрегированные размеры совпали, что не доказывает побайтного тождества остальных файлов.
- В старом контексте сохранён manifest SHA256 `94348317c9988d8341360446798250298183c981d821066cf5003af016b90464`. Сам полный manifest утрачен; hash не позволяет восстановить содержимое или доказать тождество текущего дерева.
- Старое дерево не было заморожено: чужие staged/unstaged изменения и параллельные коммиты меняли generator/converters/sharding/docs. Аудит не утверждал, что чужое дерево осталось неизменным.
- Тогда `roslyn structure` успешно загрузил solution, а `members`/`refs` подтверждали отобранные связи. Ограничение раннего syntax-report «daemon unavailable» относилось к раннему проходу, а не ко всему продолжению. Неудачные отдельные запросы generic-символов не считались отсутствием потребителей.

### Методика и skills

В исходном аудите были загружены `dotnet-solid-principles`, `type-design-performance`; вызов `skill analyzing-dotnet-performance` был недоступен, поэтому прочитаны установленный SKILL.md и его семь references: critical, async, memory/strings, collections/LINQ, regex, I/O/serialization, structural. Факт чтения и выводы сохранились в контексте. Эти действия не выдаются за повторные вызовы при восстановлении.

Восстановление следует текущим AGENTS: внутренний документ, CRLF, C# symbols через Roslyn, без изменения исходников и без повторения завершённых проверок. Дополнительно загружены текущие маршрутизаторы `using-dotnet`/`dotnet-advisor` и baseline `dotnet-csharp`; версия SDK сверена по `global.json` — `11.0.100`. Это не новый performance scan.

## Восстановленный scan execution checklist — до классификации

Ниже **точные счётчики из сохранённого вывода прежнего syntax scan**, не количество дефектов и не текущий повторный прогон. Последующие исторические +9 строк generator и любые изменения после аудита в эти числа не включены. Исходный scanner/report-файл утрачен, поэтому повторная воспроизводимость этого старого запуска ограничена сохранённым текстом. Ноль относится только к указанному рецепту, не ко всем возможным формам записи API.

- [x] `IndexOf(string literal)` без явного comparison — **0**; `IndexOf(char literal)` — **9**.
- [x] `StartsWith/EndsWith(string literal)` без comparison — **0**; char overload — **1**.
- [x] `Contains(string literal)` без comparison — **0**, с comparison — **3**; char overload — **10**.
- [x] `Substring` — **7**; parameterless `ToLower/ToUpper` — **0**.
- [x] `Replace` — **34**; цепочка ≥3 в statement — **1**. Это не автоматически 34 аллокации: receiver и изменяемость результата требуют проверки.
- [x] `params T[]` — **261**; `params Span/ReadOnlySpan` — **83**.
- [x] Method-name кандидаты `Select` — **115**, `Where` — **42**, ordering family — **73**, `GroupBy` — **1**, `All` — **1**, `Any` — **26**. Включён одноимённый DSL nextorm, не только LINQ.
- [x] `All/Any` с `char.Is*` predicate — **0/0**; отдельные `char.Is*` calls — **26**.
- [x] `ToList` — **47**; `ToArray` — **87**.
- [x] `new Dictionary` — **95**: method-body **55**, field initializer **40**; `new List` — **349**: method-body **339**, field initializer **10**. Учитывались explicit и синтаксически выводимые target-typed `new`, но не все collection expressions.
- [x] `static readonly Dictionary` — **24**; `FrozenDictionary`-typed fields — **3**, `ToFrozenDictionary` — **3**, explicit `new FrozenDictionary` — **0**. Неизменяемость и read-heavy использование всех 24 словарей не доказаны.
- [x] `StringComparer.CurrentCulture` — **0**.
- [x] `RegexOptions.Compiled` — **2** syntax uses; реальная статическая Regex construction — **1**, `[GeneratedRegex]` — **0**. Второй `Compiled` — обработка regex options в SQL-translator, не ещё одна компиляция. Реальная Regex кэшировалась: `src/nextorm.core/Builders/CollectionSlot/CollectionSlotDescriptor.cs:101`.
- [x] `async void` — **0**; `Task.Run` — **2**: оба async fan-out, не CPU-wrapper над sync-методом.
- [x] `.Result` — **4** property candidates: **1** Task-result под `IsCompletedSuccessfully`, **3** не Task. `Wait()` — **0**; `GetAwaiter().GetResult()` — **6**: ClickHouse **1**, in-memory **5**. Blocking expression само по себе не доказывает deadlock.
- [x] `stackalloc` — **5**, внутри lexical loop — **0**; typed/inferable `new byte[]` — **1**, `new char[]` — **2**, direct temporary argument/return byte/char-array candidates — **0**.
- [x] `string.Format` — **1** reference в translator, не доказательство runtime форматирования/boxing в самом ORM. `ContainsKey` — **29** candidates, не 29 двойных lookup.
- [x] `new HttpClient` — **0**; `new JsonSerializerOptions` — **1** в построении shape-options, per-row recreation не установлен; `JsonSerializer.Serialize` simple-name calls — **0**, `Deserialize` — **1**. Ноль не охватывает все generic/alias формы сериализации.
- [x] `FileStream` constructor — **0**; трёхаргументные `ReadAsync/WriteAsync` — **3** candidates: два собственных batch helpers и один TextReader forwarding, не три старых byte[] Stream overload.
- [x] Syntactically public List/Dictionary members — **58**; включены public-члены внутренних типов и намеренные `ToList/ToDictionary`. Это не 58 утечек API.
- [x] **Verify-the-Inverse:** **482 sealed / 788 class types**; **225 static**, **9 abstract**, **72 concrete nonstatic unsealed**. Среди concrete nonstatic: **482/554 = 87.0% sealed**, **72/554 = 13.0% unsealed**. Partial объединялись синтаксически; leaf/наследуемость всех 72 семантически не проверялась. Систематическая sealing-находка не заявлялась.
- [x] Struct shape: **104 readonly / 112 structs**, non-readonly **8**. Mutable accumulator/lease/parser states и публичные Paging/Sorting не превращены автоматически в дефекты; полного defensive-copy анализа не было.
- [x] Прежнее дополнение Roslyn refs, unique src locations: `Enumerable.Cast` — **1** (`src/nextorm.core/DataContext/Parameters/TableParameterValue.cs:190`); `Take` — **3** (`src/nextorm.core/Builders/CollectionSlot/CollectionSlotBinder.cs:409`, `src/nextorm.core/Builders/Entity/EntityBuilder.cs:6121`, `src/nextorm.core/Builders/Joins/JoinIntoStitcher.cs:302`); `Aggregate` без seed — **0**. Другие overload не проверены, единого полного счётчика `Select|Where|Cast|Take|Aggregate` нет.
- [~] Cross-file consistency и compound chains проверялись у отобранных кандидатов. Полный анализ branched Replace, string `+=`, повторных перечислений, ValueTask double-await, false-sharing и regex nested quantifiers на всех 520 файлах **не выполнен**. Pattern единственного Regex construction прочитан; динамические patterns не исследовались как security review.

## 🟡 Moderate — восстановленные рекомендации к согласованию

### D1. [PERF][DRY] Проекция пересчитывается при чтении Current; DISTINCT вычисляет её повторно (2 реализации)

**Эффект:** при ненулевой `_map` DISTINCT вычисляет проекцию для `_seen.Add`, а `ToList/ToListAsync` — снова для выдаваемой строки. Проекция с `new` создаёт второй результат; повторные пользовательские чтения Current добавляют вызовы.

**Прежние места:** `src/nextorm.core/DataContext/InMemory/InMemoryEnumerator.cs:113–117,157–163`, `src/nextorm.core/DataContext/InMemory/InMemoryEnumeratorAdapter.cs:55,92,119`; потребитель `src/nextorm.core/DataContext/InMemory/InMemoryQueryExecutor.cs:61–66,86–91`.

**Рекомендация:** кэшировать проекцию текущей позиции и применять DISTINCT к тому же результату; сохранять lazy mapping на не-DISTINCT путях, если перенос в MoveNext увеличивает работу на Any/Offset.

**Проверка будущего исправления:** counting selector для DISTINCT — один вызов на рассмотренную строку; повторные Current возвращают тот же projection-object. Проверить sync/async, adapter, rejected duplicates, Offset/Limit, repeated Init/dispose. Изменение exception timing требует отдельного решения; числа/регрессии — `nextorm-inmemory-perf-analyst`, число вызовов не равно проценту ускорения.

**Инварианты:** KISS/число концепций, отсутствие неизмеренных perf-утверждений, уважение закрытых решений, warnings-as-errors. Старый R3 (`docs/specs/performance/benchmark-report.md:372–377`) устранил повторное чтение исходного элемента и yield-layer; здесь другая операция — projection delegate. Generic `(TResult)(object)` сам по себе не доказывает boxing.

### D2. [DRY] Bucket fan-out policy продублирована между двумя фасадами (5 пар helpers)

**Эффект:** grouping bucket по shard, sequential same-shard, parallelism clamp, gate release и завершение задач имеют два владельца; изменение правила требует согласованной правки обоих. Расхождения результатов прежний аудит не обнаружил.

**Прежние места:** `src/nextorm.core/Sharding/FanOut/ShardQuery.cs:120–190,274–344`; async-циклы `:137–166,291–320`; потребители `ShardQuery<TEntity>.ToListAsync` `:71–77` и `ShardQuery<TEntity,TResult>.ToListAsync` `:233–239`.

**Рекомендация:** общие execution/grouping helpers без нового интерфейса; типизированную проекцию оставить фасадам. Два конкретных потребителя уже есть. Не объединять extraction с удалением Task.Run или изменением scheduler-семантики.

**Проверка будущего исправления:** обе формы сохраняют bucket-вектор, sequential same-shard, bounded cross-shard parallelism, отмену/исключения и освобождение gate. Прежняя ссылка на согласованный контракт: `docs/specs/roadmap/todo_sharding.md:74–93`.

**Инварианты:** абстракция только при наблюдаемом втором потребителе, KISS, measure-never-assert, закрытые решения и build discipline. Без ближайшей правки fan-out допустимо отложить до изменения concurrency/cancellation policy. Самостоятельного ускорения extraction не обещает.

## ℹ️ Info — восстановленная рекомендация, отложенная с триггером

### D3. [DRY] Whitelist interval units хранится в core translator и ClickHouse dialect (2 карты × 8 записей)

**Эффект:** множество DateTime interval units и SQL spellings приходится менять в двух сборках; расхождения прежний аудит не обнаружил.

**Прежние места:** `src/nextorm.core/Visitors/Providers/ClickHouseNativeFunctionTranslator.cs:521–531,987–993`, `src/nextorm.clickhouse/ClickHouseDialect.cs:2612–2629`.

**Рекомендация:** при расширении единиц использовать один доступный обоим потребителям источник правила/helper, без интерфейса и зависимости core → provider. Два маленьких совпадающих словаря не требуют самостоятельной perf-оптимизации.

**Проверка будущего исправления:** table-driven parity восьми spellings/casing и rejected sub-second/unknown units через оба entry paths; SQL generation неизменен. Whitelist одинаковая, хотя translator дополнительно требует compile-time literal.

**Триггер:** изменение interval unit либо третий потребитель. **Инварианты:** наблюдаемый второй потребитель, KISS, отсутствие неизмеренных perf-обещаний, совместимость public API/docs и warnings-as-errors. Новый public API только ради двух приватных карт не вводить.

## Восстановленные исключения — не считать новыми находками

- **[PERF] JSON array boxing уже зарегистрирован.** Прежнее чтение `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs:748–779` подтвердило Array.GetValue; запись future optimization с прежними измерениями — `docs/specs/status/evidence/rc2-176/r2-n1/perf/perf-notes.md:67–71`. Без нового measurement не переоткрывать. Ранний in-memory/JSON scout эту запись пропустил; итоговый отчёт это исправил.
- **[PERF] JSON flush/runtime numeric dispatch:** уже решения D247, прежние указатели `docs/specs/status/D247-json-streaming-1.md:58`, `D247-json-streaming-2.md:164`. CSV per-row writes/UTF8 passes видны в прежнем обзоре, но отдельная измеренная проблема не установлена; микрооптимизацию только по форме кода не назначать.
- **[PERF] ClickHouse sync-over-async — sync bridge, не доказанный deadlock async API.** Прежние позиции `src/nextorm.clickhouse/Bulk/ClickHouseBulkInsertAdapter.cs:238–245,255–277,317–331`, caller `ClickHouseDataContext.cs:852`. Sync terminal блокирует на async-only driver call; async использует await. Для высокой concurrency — async terminal; пересмотр bridge при факте starvation/deadlock и подтверждённой альтернативе драйвера, не снятие sync API вслепую.
- **[PERF] InMemory blocking expressions не повышены до Critical.** Прежние позиции `src/nextorm.core/DataContext/InMemory/Joins/InMemoryLinqSource.cs:149,155,175,181`, set-op rejection `InMemorySetOperations.cs:49`; `InMemoryQueryBuilder.cs:236–259` оборачивал async source в adapter, предоставляющий IEnumerator, но sync MoveNext отклонял async-only inner (`InMemoryEnumeratorAdapter.cs:103–106`). Реальная достижимость suspended branch из public terminal не доказана. Триггер — новый async route/воспроизведение; сначала controlled async-source probe, затем fail-closed vs async buffering.
- **[SRP][ISP][DIP] F1/F13/F12 не переоткрывались.** Закрытые facade extractions: `docs/specs/design/solid-review.md:11–13`; KISS composite dialect: `:714–754`. Исторические размеры: DataContext **2720**, InMemoryDataContext **579**, QueryExecutor **1528**, ISqlDialect **2414** строк. **97** — исторический размер F12, не текущий пересчёт; старый syntax scan дал **321 declared members**, что не обосновывает split. EntityBuilder **6810**, QueryPreparer file **5310** — navigation signals, не новые god-class findings; прежний extraction `docs/specs/design/code-smells-review.md:406–421`.
- **[TYPE] Нет blanket readonly/sealed cleanup.** Paging/Sorting — оформленные mutable public contracts (`docs/specs/design/API-NAMING-REVIEW.md:2637–2662`, актуализация NullsPosition `:75`). ToList/ToDictionary имеют намеренную материализующую семантику. Public Sorting/Joins/Paging (`src/nextorm.core/Builders/Entity/EntityBuilder.cs:597–601`) раскрывали mutable state, но последствия и допустимая совместимая замена не установлены; триггер — отдельное версионированное public-surface решение. IReadOnlyList сам по себе не делает объект immutable.
- **[DRY] EF member guards / generator syntax pipelines:** сходство не доказывает единую доменную политику для разных boundaries; новый helper/interface не предлагался. Mutable generator collections не объявлены perf-дефектом без анализа incremental invalidation.
- **[LSP] Capability refusal не равен throwing-override defect.** Явные fail-closed гейты SQL/in-memory не классифицировались автоматически как LSP-нарушения; полной контрактной проверки всех overrides не было.
- **Смежная correctness-разведка, не подтверждённый баг:** SQLite registration-marker ставился до окончания Register (`src/nextorm.sqlite/SQLiteFunctions.cs:21–46`), consumer `SqliteDataContext.cs:63–68`; retry failure-сценарий не воспроизведён. Старые in-memory cancellation/single-use claims не представлялись как новые доказанные design-дефекты.

## Восстановленный исторический охват каждого src-проекта

Это описание **выполненного тогда** просмотра, сохранённое в контексте, а не утверждение о повторном чтении всех перечисленных файлов при восстановлении. Числа относятся к позднему историческому metadata snapshot; recipe counts — к более раннему scan. Охват здесь — глубина аудита, **не test coverage**.

- **nextorm.core — 465 файлов / 149284 строки.** Целевой обзор builder/query-preparation member surfaces; полностью перечитаны InMemoryEnumerator, Adapter, InMemoryLinqSource, ShardQuery, CollectionSlotDescriptor. Дополнительные slices InMemoryQueryBuilder `:104–433`, InMemoryQueryExecutor `:45–109`, SetOperations `:1–110`, ResultSetEnumerator `:187–244`, JsonRowWriterFactory `:748–815`, EntityBuilder `:560–639`, ClickHouseNativeFunctionTranslator `:510–539,969–1013`; Roslyn refs подтверждали связи D1/D2/JSON/helpers. Не проверены все 465 файлов, все AST/cache/hash инварианты, visitor ветви и routing/sharding contracts.
- **nextorm.core.sourcegenerator — 5 / 3557 (baseline 3548).** Scout сообщил полное чтение EntityMetadataGenerator, RowMaterializerGenerator, AnonymousClassGenerator, IsExternalInit; JoinAliasGenerator — `:202–215,1000–1179,1378–1602,1635–1774,1861–1946`. Проверялись metadata/row/alias generation boundaries и value-equatable alias models. В прежнем дереве проект уже не был пустым stub: пуст только AnonymousClassGenerator, AGENTS Layout устарел. Metadata менялся параллельно (+9 строк); generated-output compilation/сравнение и все incremental equality paths не проверялись.
- **nextorm.entityframeworkcore — 7 / 1585.** Scout сообщил полное чтение семи файлов: EfCoreFilterBinding `:1–62`, EfQueryFilterTranslator `:1–336`, EntityFrameworkCoreExtensions `:1–169`, NextOrmDbContextExtensions `:1–110`, NextOrmModelMapper `:1–370`, NextOrmOptionsExtension `:1–55`, NextOrmQueryableExtensions `:1–483`. Mapper/filter/bridge boundaries просмотрены; поведенческая эквивалентность member guards и live EF semantics не проверялись.
- **nextorm.extensions — 4 / 2059.** Scout сообщил полное чтение ExtremeRowExtensions `:1–959`, SelectGroupComponent `:1–293`, SelectGroupExpressionAnalyzer `:1–680`, SelectGroupConditionBinder `:1–127`. Prepare/descriptor/build responsibilities и core component caller проверялись; cold-build allocations не названы per-row проблемой. Runtime/SQL parity и весь D362 perf evidence не воспроизводились.
- **nextorm.clickhouse — 7 / 5136.** Целевые bulk slices `Bulk/ClickHouseBulkInsertAdapter.cs:170–349`, context caller `:852`; dialect `:2600–2644`, interval maps и syntax candidates. Не весь dialect/extensions/extreme-row renderer или все driver contracts прочитаны; server perf выводов не было.
- **nextorm.postgres — 13 / 3359.** Целевой native bulk context `:580–629`, PgHalfVector `:75`, frozen range-map inventory. Ошибочная привязка Select/ToArray к bulk снята. Не весь provider/vector/range/EF seam или driver behavior проверен; object[] приходит в bulk loop готовым, upstream allocation не исключается.
- **nextorm.sqlserver — 8 / 4067.** Целевой TVP reuse `SqlServerDataContext.cs:486–521` и builder sorting consumers через Roslyn; syntax inventory по всему проекту. Один TVP SqlDataRecord не доказывает bulk-copy поведение. Остальные bulk/native JSON/vector/pivot ветви и весь dialect не прошли содержательного ручного аудита.
- **nextorm.mysql — 3 / 2013.** Roslyn members контекста, DI `:1–54`, context native bulk/chunk/session boundary `:475–719`, inherited MariaDB seam. MySqlDialect и оставшийся context преимущественно syntax-only; session restore и driver behavior не проверялись.
- **nextorm.mariadb — 3 / 534.** Прочитаны context `:1–77`, dialect `:1–402`, DI `:1–55`. MySQL inheritance и dialect-specific overrides были намеренны; server contracts и все inherited hooks не проверялись.
- **nextorm.sqlite — 5 / 1034.** Scout сообщил чтение SQLiteFunctions `:1–80`, SqliteDialect `:1–559`, SqliteDataContext `:1–280`, SqliteFunctionRenderer `:1–57`, DI `:1–58`. Registration/dialect/context boundaries и mutable variance accumulator просмотрены; runtime retry/concurrency/SQL parity не проверялись.

### Восстановленные детали отдельного in-memory/JSON/CSV обзора

Этот список из прежнего evidence включён сюда, чтобы полнота документа больше не зависела от утраченного временного файла.

- Тогда полностью читались `src/nextorm.core/Query/Json/{JsonNativeStream,PooledStreamBufferWriter,JsonStreamWriter,JsonRowWriterCache,JsonRowWriterFactory}.cs`; последний имел 1088 строк.
- Полностью читались `src/nextorm.core/Query/Csv/{CsvStreamWriter,CsvRowBuffer,CsvDialect,CsvValueFormatter}.cs`.
- Полностью читались `src/nextorm.core/DataContext/InMemory/{InMemoryEnumerator,InMemoryEnumeratorAdapter,InMemoryQueryExecutor,InMemoryPreparedQueryCommand,InMemoryRowMaterializer}.cs`.
- Дополнительные прежние диапазоны: InMemoryQueryBuilder `:60–419`, InMemorySetOperations `:1–79`, InMemoryLinqSource `:120–184`, Aggregates/InMemoryAggregates `:40–129`; Query/Commands/QueryCommandExtensions `:420–494`, QueryCommand.TResult `:290–329`; DataContext/QueryExecutor `:1390–1499`, Results/ResultSetEnumerator `:170–224`.
- Точечно читались JsonShapePlan/JsonShapeNode/JsonStreamOptions и InMemoryDataContext `:360–479`.
- Тогда не были прочитаны целиком `DataContext/InMemory/Planning/*.cs`, Ordering, InMemoryGrouping, Joins/{InMemoryJoin,InMemoryConditionFactory,InMemoryNavigationChain}, InMemoryProjectionFactory, Csv/{CsvBinaryFieldWriter,CsvFieldPolicy,CsvPlanCacheKey}, JsonShapePlan body, DataContext/Mapping. Это не заменено утверждением об их полном охвате.
- Последующая узкая проверка в прежнем аудите актуализировала projection/blocking/array paths; из неё появились итоговые D1 и исключения выше. Старая утечка Dispose в InMemoryEnumerator была уже закрыта (`docs/specs/design/code-smells-review.md:243`), не новый finding.

## Заново проверенные доказательства при восстановлении

**Статус: завершено.** Источник новых улик — read-only scout-сессия `ses_e1d6b9f32ffepGk8YSOqNkd5nt`, её ответ сохранён в контексте восстановления и перенесён сюда. Проверены только указанные символы и срезы исходников; никуда во временные каталоги результаты не записывались. Полный scan, build/test/benchmark не повторялись.

- **D1 — форма повторного вычисления сохранилась.** `src/nextorm.core/DataContext/InMemory/InMemoryEnumerator.cs:113–119` вызывает `_map(_current!)` из Current, а DISTINCT использует Current для `_seen.Add` (`:157–163`). `src/nextorm.core/DataContext/InMemory/InMemoryEnumeratorAdapter.cs:52–56` вызывает `_map(_inner.Current!)` из Current; DISTINCT читает его в async-пути (`:89–95`) и sync-пути (`:108–122`). В `src/nextorm.core/DataContext/InMemory/InMemoryQueryExecutor.cs:61–70,86–91` циклы ToListAsync/ToList после MoveNext читают `ee.Current` для результата. Это новые подтверждения формы кода, не измерение allocations/throughput.
- **Ограничение семантических связей D1.** Roslyn `refs` для `InMemoryEnumerator.Current` вернул только ссылки из benchmarks, не call sites в src. Caller-граф ToList/ToListAsync не запрашивался. Поэтому текущая цепочка описана по чтению control flow и interface-доступов в исходниках, а не как разрешённый Roslyn полный caller-граф. Автоматически возвращённые benchmark-ссылки не расширяют область аудита.
- **D2 — обе группы пяти helpers сохранились.** `src/nextorm.core/Sharding/FanOut/ShardQuery.cs:120–190,274–344` содержит Scoped, RunEach, RunEachAsync, DegreeOfParallelism, BucketsByShard у двух фасадов; проверенные реализации совпадают с отличием имени generic-параметра TResult/TValue. Остальные call sites и runtime scheduling не перепроверялись.
- **D3 — карты и lookup сохранились.** `src/nextorm.core/Visitors/Providers/ClickHouseNativeFunctionTranslator.cs:521–531` и `src/nextorm.clickhouse/ClickHouseDialect.cs:2612–2622` содержат одинаковые восемь единиц и OrdinalIgnoreCase. Lookup проверены в core `:988–990` и ClickHouse `:2624–2628`. Остальные call sites не перепроверялись.
- **JSON array boxing — постоянная прежняя запись доступна.** Прочитан `docs/specs/status/evidence/rc2-176/r2-n1/perf/perf-notes.md:67–71`: Array.GetValue(int) boxing value-type элементов записан как потенциальная будущая оптимизация, не новая регрессия и не blocker. Находка не переоткрыта; измерения из этого документа заново не выполнялись и не интерпретировались.

**Что эта перепроверка не подтверждает:** актуальные 520 файлов/10 проектов или глобальные recipe counts; прежнюю полноту ручного чтения на новом HEAD; runtime correctness, тесты, производительность и полный caller-граф. Три находки сохранены с прежними приоритетами; новых находок/измерений восстановление не добавило.

## Исторический следующий шаг — до одобрения реализации

- После согласования D1/D2 — одна ось за шаг, build с 0 warnings/0 errors и релевантные unit suites; это будущие gates, **не запуски этого запроса**. При реальной provider boundary — интеграции по `running-integration-tests`, не plain full-suite.
- D1 numbers/regressions — `nextorm-inmemory-perf-analyst`; runtime влияние D2/ClickHouse — `nextorm-db-perf-analyst`. Это предлагаемый маршрут, не выполненное измерение/handoff. Sub-20% deltas на прежней машине не считать доказанным выигрышем.
- Public API по D1/D2 менять не требуется. D3 отложен с триггером. Реестры/issue не обновлялись: это аудит и восстановление документа, не согласованная задача реализации.
- Build/test/benchmark/DocFX при восстановлении запрещены запросом и не выполняются. Постоянный отчёт не требует генерации публичной документации.

## Ограничения восстановления

- Исторические scan counts и глубина чтения сохранены по контексту, а не повторены на новом HEAD. Полные stdout/logs, manifest, scanner binary и отдельные временные evidence-файлы утрачены; их доступность или побайтное восстановление не заявляются.
- Восстановлен полный содержательный итоговый отчёт, включая методику, checklist, находки, исключения, per-project coverage, направления проверки и ограничения. Это не восстановление всех промежуточных raw artifacts.
- Только исходники из раздела «Заново проверенные доказательства» являются текущими доказательствами в объёме перечисленных срезов и ограничений; остальные file:line — исторические указатели, требующие актуализации перед исправлением.
- Старый временный путь больше не является местом хранения финала/прогресса. Единственный новый документ — этот файл в docs; без коммита он сохраняется на постоянном диске, но ещё не является резервной копией в Git.

| Серьёзность | Подтверждённых в прежнем аудите находок | Главная тема |
|---|---:|---|
| 🔴 Critical | 0 | Не обнаружено в проверенных участках |
| 🟡 Moderate | 2 | D1 повторная проекция; D2 двойная fan-out policy |
| ℹ️ Info | 1 | D3 interval-unit whitelist |

> Результат AI-аудита недетерминирован: возможны пропуски и ложные выводы. Это целевой ручной обзор поверх широкого синтаксического scan, не исчерпывающая верификация проекта. Рекомендации требуют человеческого review, регресс-тестов и, для performance-выводов, измерений.
