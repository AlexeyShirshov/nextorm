# D370 — Stream A: вариант A как первый инкремент, границы закрытия

Статус: проект архитектора для письменного одобрения; не разрешение S15. Кампания [#370](https://github.com/AlexeyShirshov/nextorm/issues/370), milestone `1.0.10` (проверено через GitHub). S14: `N=14, r=1, n=1`; CHECK не запускался. Пользователь выбрал A и отсутствие дополнительных пакетов для пользователей, затем согласился считать A первым инкрементом, а не выполнением исходного AC2. Конкретные новые контракты ниже — предложения архитектора, ещё не одобрены. Одобрение этой записи снимает дизайн-гейт для подготовки плана S15, но само по себе не запускает реализацию.

## 1. Актуальная база и влияние #65

HEAD `5560df913e30d35d32c0d710d25a9938f4e8bc1f`: merge `1.0.10` и #65 (`51ddc7de`). Каноническое измерение `dotnet publish tests/nextorm.aot.runner -c Release -r linux-x64`, rc=0, 205 уникальных `(code,path:line)` / 211 сырых диагностик; src193/tests12. S12 на `b1e2db6a`:204/210. Diff42 добавленных/41 удалённая пара:41 сдвиг строк после #65 и новый `IL3050` в `DefaultEntityMetadataProvider.cs:50`. Сдвиги строк не являются исправлениями. Для кодового шага требуется измеренная Δ≤−1 от205 (≤204), без потери покрытия.

Источники: `.pdca/370/rv1/registry-s14-baseline.tsv`, `.pdca/370/rv1/registry-s14-baseline.summary.txt`, `.pdca/370/rv1/registry-contract.md`, `.pdca/370/rv1/attempts/s14-publish.log`, `s14-identity.txt`, `s14-facts-for-architect.md` в том же attempts. Исторические `baseline-registry.tsv` (230 уникальных) и `baseline-rows.tsv` не подменяют S14. Статус: `.pdca/status/T370.md:1772–1786,1961–1964`.

Урок S13: не ссылаться на якобы существующие `EntityDescriptor`/`TypeToken`. Дефект `T370-S13-PLAN-CONTRACT-ASSUMPTION` описан в `docs/specs/design/D370-pdca-process-defects.md`. Все новые имена этой записи явно помечены как предложения. Фактическая поправка исходной фактуры: `EntityMetadata` объявлен **internal class**, не public (`src/nextorm.core/DataContext/Meta/Implementation/EntityMetadata.cs:5`). Публичен `IEntityMetadata`; не добавляем в него обязательных членов.

## 2. Механизм шва и законный владелец инспекции

### 2.1 Не ослаблять AC2

`runtime-typed Type` не означает «Type без DAM». DAM сообщает триммеру требуемые члены и сохраняет их; он не превращает рефлексию в генерацию. Выбран A как ограниченный шаг: конструкторная инспекция переносится к подготовке CLR-метаданных, материализатор получает готовые конструкторы. Исходный AC2 остаётся **not met**, пока остаются runtime-carriers и отсутствуют generated materializers/projection bindings. Эта запись не разрешает CHECK объявить Stream A закрытым после −3.

Typed-путь: `GetPreparedQueryCommand<TResult>` → `GetMapCached<TRow>` → `RowMapperFactory.GetOrBuild<TResult>` → `Build<TResult>` → `RowMaterializerBuilder.Build(typeof(TResult),...)`. Boxed-путь: `GetObjectPreparedQueryCommand` → `QueryCommand.ResultType` → `GetOrBuildBoxed` → `BuildBoxed` → `Build(resultType,...)`. Доказательства: `RowMapperFactory.cs:248–295,390–439,477–495` в `src/nextorm.core/DataContext/Mapping/`; `QueryCommand.cs:160`, `QueryCommand.TResult.cs:24,33,43,52,67` в `src/nextorm.core/Query/Commands/`. Boxed-путь нужен JoinInto, его удаление не допускается.

### 2.2 Целевой A-шов

Предлагается новый **internal** immutable `MaterializationTypeMetadata` в namespace `NextORM.Core` (не существующий тип). Он хранит CLR identity, конструкторные факты и признак value-type; это не entity mapping и не заменитель `IEntityMetadata`. Предлагаемый контракт:

```csharp
internal sealed class MaterializationTypeMetadata
{
    internal Type ClrType { get; }
    internal ConstructorInfo? LongestPublicConstructor { get; }
    internal ConstructorInfo? ParameterlessConstructor { get; }
    internal ConstructorInfo? RangeConstructor { get; }
    internal bool IsValueType { get; }
}
```

Фабрика зарегистрированного типа принимает `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type`; инспекция выполняется там один раз. `ClrType` не обязан быть DAM-аннотирован: после фабрики он используется лишь для identity, типизации expression nodes и сообщений, не для поиска членов. Имена и конкретное размещение вспомогательных методов — детали реализации; контракт выше обязателен.

В internal `EntityMetadata` добавить internal `MaterializationTypeMetadata? Materialization` с инициализацией при создании метаданных; отсутствие записи допустимо для старых/custom metadata и означает обращение к отдельному реестру, не cast внешнего `IEntityMetadata` к internal классу. Источник: `EntityMetadataBuilder<T>.CreateMetadata` (`DataContext/Meta/Entities/EntityMetadataBuilder.cs:92–99`), где T известен. `IEntityMetadata` не изменять. Конструкторы зависят от CLR-типа, а mappings/properties/filters/relationships — от scope/configuration; общий CLR descriptor не должен переносить mappings между scope и не должен заполнять configured metadata cache вместо #65 resolver. `DataContextExtensions.ResolveMetadata<TEntity>:264–320` сохраняет приоритет явно настроенного mapping и повторную проверку под registration gate.

В `ProjectionEntityItem` заменить поле `EntityType` ссылкой на эту запись; источник создания — `QueryCommand.QueryPreparer.cs:2238,2253,2290`, готовые entity metadata доступны `:2248`. Для anonymous/DTO/scalar результата запись создаётся из статического TResult, а не из entity-table metadata. В A `QueryCommand.ResultType` ещё сохраняется для прочих потребителей; добавить internal ссылку на запись при пяти присваиваниях TResult и переносить её в clone/boxed dispatch. Наличие Type внутри записи означает, что это НЕ конечное устранение carriers по AC2.

`RowMaterializerBuilder` больше не вызывает `ConstructorInfoResolver`. Шесть фактических мест: `:188,207,312,334,400,488`. Поправка к исходному описанию: `:400` — parameterless fallback в `BuildRangePairConstructor`, а не GetRangeConstructor; `:488` — Range. Constructor selection, positional/member binding, nullable/default semantics, range flags и исключения должны совпасть с текущими. `:103` использует CLR identity записи; `:221,282,297–299` — display name, value-type/default/null expression. В A expression construction сохраняется; generated direct reader — отдельный последующий шаг.

Range — отдельный контракт: DAM на T владельца НЕ сохраняет автоматически конструкторы типа его свойства. `BuildRangeValue:485–488` получает `PropertyInfo.PropertyType` и снимает Nullable; этот Type нельзя молча объявить гарантированным. Зарегистрированный закрытый Range-тип получает собственную запись; неизвестный тип проходит явно описанную JIT/AOT границу §3. Не обещать исчезновения Range-диагностики без проверенного потока данных.

Кэш (`RowMapperFactory.cs:612,615`) должен сравнивать CLR identity, identity члена, порядок/slot проекции и существующий scope/shape, а не identity экземпляра descriptor. Хеш — только ускоритель: коллизии различаются структурным equality. «Детерминированный» здесь означает одинаковую структуру в одном процессе независимо от регистрации/пересоздания descriptor; стабильные числовые хеши между процессами не требуются. Clear/rebuild не меняет семантику ключа.

### 2.3 DAM и стоимость публичного контракта

Для A используется только `PublicConstructors`, не `All`. Предлагаемые корневые аннотации: T у `EntityMetadataBuilder<T>`, TResult у `QueryCommand<TResult>`, TEntity у `EntityBuilder<TEntity>` и T обоих mapping-overloads `DataContextExtensions.From<T>` (`:1087,1106`). Все partial declarations и generic-пробросы к этим типам должны согласованно передавать тот же контракт. Остальные From-overloads (`:1343,1359,1385,1411,1442,1462,1475,1509,1527`) также входят в транзитивный аудит своих TResult/T. Не утверждаем, что перечисленные пять корней исчерпывают API-каскад: полная замкнутость аннотаций — обязательный проверяемый результат подготовительного измерения S15, а не разрешение подавить оставшиеся IL2091.

DAM-эксперимент `/tmp/opencode/dam-probe`: V1 с ключом статической cache-лямбды оставляет IL2070; V2 с захваченным аннотированным входом чист; V4 с аннотированным свойством чист. Для новой фабрики использовать V2 или прямую инспекцию; не рассчитывать на аннотации ключей ConcurrentDictionary. `/tmp/opencode/dam-cascade`: неаннотированный generic forwarding создаёт IL2091. Это tightening trimming-контракта, даже если CLR-сигнатура не меняется. Конкретные JIT-вызовы не требуют изменений; generic-обёртки пользователей при trim-анализе могут потребовать аннотаций. Обещание «ни один пользователь никогда не меняет код» для A не даётся.

Законный слой инспекции — metadata/registration, которому передана доказанная гарантия членов, либо явно оставленный JIT compatibility fallback. Перенести GetConstructors из одного файла в другой недостаточно: каждый перенесённый сайт в новом реестре получает provenance старого сайта, причину и новый owner. Недоказанный DAM-поток остаётся дефектом, не становится safe по названию слоя.

## 3. Регистрация, discovery, boxed/dynamic и внешние сборки

### 3.1 Предлагаемая публичная добавка

Следующий контракт **новый**, Roslyn не обнаружил существующего `RegisterMaterializationType`. Он предлагается на отдельное одобрение, а не приписывается текущей библиотеке:

```csharp
public static void RegisterMaterializationType<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
    this IDataContext dataContext);
```

Метод принадлежит существующему `NextORM.Core.DataContextExtensions`; не новый пакет. Возвращает void; null context — ArgumentNullException. Регистрирует только CLR-конструкторную запись, не entity mapping, не schema/table, не provider conversion. Повторная регистрация одного T идемпотентна и thread-safe; mapper delegates не регистрирует. Состояние привязано к контексту (не process-global configured mapping cache); для нового контекста требуется новое автоматическое/явное заполнение. CLR-факты допускают общий immutable cache, но не заменяют регистрацию/настройки конкретного контекста.

Планируемая запись `src/nextorm.core/PublicAPI.Unshipped.txt`:

```text
static NextORM.Core.DataContextExtensions.RegisterMaterializationType<T>(this NextORM.Core.IDataContext! dataContext) -> void
```

Снимки API не кодируют DAM generic-параметров: отдельно проверять атрибуты reflection/API-тестом и trim-consumer compile tests. Для internal descriptor и internal свойства EntityMetadata API-записей нет. `EditorBrowsable(Never)` не используется: это документированный публичный контракт и скрытие из IntelliSense не уменьшает compatibility obligations. В этой задаче PublicAPI не редактируется.

### 3.2 Автоматическая регистрация без assembly scanning

Обычные From<T> и QueryCommand<TResult> автоматически создают запись известного закрытого типа. Внешняя сборка не обязана иметь генератор для A: конкретный тип передаётся generic-вызову из потребителя. Нет Assembly.GetTypes/поиска атрибутов по всем сборкам, ModuleInitializer discovery, rooting и дополнительных пакетов. Для закрытого типа, известного только при динамической композиции, AOT-приложение может заранее вызвать предложенный generic registration; после этого runtime Type используется как ключ lookup, а не как разрешение инспекции.

Регистрация верхнего DTO/entity не означает рекурсивной регистрации всех типов его свойств/навигаций. В A зарегистрировать отдельные Range/вложенные формы можно явно; JIT сохраняет автоматический fallback. Поэтому A не обещает бесшовной NativeAOT parity и не закрывает AC2. Последующая генерация должна автоматически собирать reachable закрытые формы и создавать constructor/member bindings в компиляции потребителя. Проверенная граница генератора: `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs`, `AnonymousClassGenerator.cs`; `src/nextorm.core/nextorm.core.csproj:33–36` упаковывает analyzer, не запускает его для core. Новый generator/discovery ABI не вводится этим инкрементом.

Boxed JoinInto получает запись из исходного generic command/registration и сохраняет её при клонировании; не восстанавливает generic-методы через MakeGenericMethod. Для non-generic динамического результата lookup производится до построения reader. Если NativeAOT не имеет записи, выбрасывается `QueryPreparationException` с постоянным префиксом `NEXTORM_AOT_MATERIALIZATION_TYPE_NOT_REGISTERED`, CLR-именем и ролью (result/projection/range), до SQL/чтения строк. Существующий string-конструктор исключения: `DataContext/QueryPreparationException.cs:26`. Это контролируемая диагностика неполноты A, не допустимый конечный результат NativeAOT parity для поддерживаемой формы.

### 3.3 JIT fallback и DefaultEntityMetadataProvider

Обычный JIT сохраняет обнаружение незарегистрированных типов и результат существующих запросов без нового пакета или обязательного вызова регистрации. Выбор безопасной ветки определяется runtime capability, не пользователем и не предположением «зарегистрировано значит AOT». Использование `RuntimeFeature.IsDynamicCodeSupported` как ветки НЕ является доказательством исчезновения предупреждений анализа сборки.

`DefaultEntityMetadataProvider.GetMetadata(Type,in MappingScope)` и `IEntityMetadataProvider` сохраняются (`DataContext/Meta/DefaultEntityMetadataProvider.cs:9–33`, `IEntityMetadataProvider.cs:6–13`). `BuildMetadata(Type):48–55` строит EntityMetadataBuilder<> через MakeGenericType/Activator/Invoke. Для A: JIT fallback остаётся; на AOT miss — детерминированная QueryPreparationException до рефлексивного построения. Конструкторный descriptor НЕ заменяет IEntityMetadata, и наличие только регистрации конструкторов не означает наличия scoped entity mapping. Типизированный From<T>/настроенный provider должен предоставить соответствующий mapping. Не переносить mapping между scope. Новый IL3050:50 остаётся в учёте, пока фактически не устранён; он не списывается как платформа и не считается автоматически снятым runtime guard.

У внешнего IEntityMetadata отсутствуют constructor facts: брать их из отдельного реестра. Не требовать новой реализации интерфейса, не менять provider contract и не заменять настроенный mapping автоматически созданным. Для полного AC2 генерация должна заменить этот runtime metadata factory для поддерживаемых форм; выбранный A это не делает.

### 3.3.1 Уточнения после независимой проверки

Все семь решений §5 пока являются предложениями для письменного одобрения. Уже согласованы лишь выбор A как первого инкремента, сохранение исходного AC2 и отсутствие дополнительных пакетов/обязательных действий для обычного JIT. В частности, согласие с A не означает одобрения нового API регистрации.

**Closed-world граница.** «Динамическая композиция» означает динамическое соединение заранее известных закрытых CLR-форм. Каждый явно регистрируемый T должен присутствовать в статическом generic-вызове; generic API не может зарегистрировать произвольный Type, впервые обнаруженный только во время выполнения. Такие формы в A не поддерживаются при отсутствии dynamic-code capability и завершаются указанной QueryPreparationException. Рефлективный MakeGenericMethod для обхода этого ограничения запрещён. Закрытый Range-тип регистрируется отдельно, его Nullable-обёртка обрабатывается как nullable-семантика той же записи; регистрация владельца не доказывает наличие записи Range. Это ограничение промежуточного A, не сужение окончательного AC2/parity-контракта кампании.

**Lifetime.** Регистрация принадлежит экземпляру IDataContext и действует до завершения его lifetime; внутреннее хранение не должно удерживать умерший контекст глобальной сильной ссылкой. Новый контекст не наследует явных регистраций прежнего. Повторная регистрация идемпотентна. DataContextCache.Clear() очищает воспроизводимые metadata/plan/CLR-fact caches, но не отменяет явно объявленные регистрации живого контекста: это пользовательская конфигурация, не memoization. При восстановлении cache constructor facts разрешено переиспользовать живую immutable запись; mapping восстанавливается по существующим scoped/configured правилам, а не из регистрации конструкторов. Для context-bound registry не добавляется публичный метод очистки/отмены в A.

**Команды без контекста.** QueryCommand<TResult>(IDataContext?, QueryDefinition) уже допускает null (`QueryCommand.TResult.cs:21–24`). Автоматическое создание immutable CLR-записи из TResult не требует контекста и не вызывает публичный extension с null. Unbound command и его clone несут эту запись; при привязке/выполнении она доступна bound context без повторной runtime-инспекции. ArgumentNullException относится только к явному RegisterMaterializationType<T>(null), не к существующему допустимому созданию unbound command. Не менять допустимость null у текущего конструктора.

**Missing mapping не missing constructor.** RegisterMaterializationType<T> гарантирует только constructor facts. Если constructor запись есть, но scoped mapping отсутствует, это не успешная подготовка entity query. Сначала применяются существующие configured/custom provider правила; если единственный оставшийся способ — запрещённый runtime BuildMetadata при отсутствии dynamic-code capability, выбрасывается QueryPreparationException с префиксом `NEXTORM_AOT_ENTITY_METADATA_NOT_AVAILABLE`, CLR-именем и scope до SQL. Не подменять пользовательские ошибки provider этим исключением и не создавать default-scope mapping вместо запрошенного scope.

**Граница fallback.** Решение выполняется по RuntimeFeature.IsDynamicCodeSupported: при true сохраняется compatibility fallback, при false запрещён runtime metadata/construction discovery и применяется fail-closed. Это распространяется также на не-AOT среду с отключённым dynamic code; признак не называется доказательством NativeAOT. Наличие записи позволяет безопасный lookup, но не доказывает безопасность всех остальных операций query pipeline. Runtime branch сам по себе не считается устранением IL3050/IL2070 в publish.

**Gate замера.** Число транзитивных DAM-аннотаций и новых warning sites не известно. Это измеряемая входная величина плана S15; до согласования точного списка затронутых публичных generic-параметров и net-impact продуктовый шаг не разрешён. Измерение в одноразовом worktree — отдельный probe, не скрытая реализация в основной ветке. Если его результат не укладывается в одобренную публичную поверхность/Δ≤−1, требуется пересогласование, а не расширение контракта по усмотрению исполнителя.
### 3.4 Честная арифметика и порядок

A: три текущих IL2070 `ConstructorInfoResolver.cs:18,24,30` — цель удаления, идеальный локальный эффект −3:205→202. Это не измеренный net. Формула нового реестра: `205 − removed + added`, включая новый IL2091/IL2070 у forwarding/legacy inputs и оставшийся DefaultEntityMetadataProvider:50. Если три сайта удалены и два появились при разрешённом переносе, результат204 (−1); при трёх новых —205 (0), шаг числовой гейт не проходит. Число новых сайтов не измерялось; эту запись нельзя цитировать как доказательство net−3 или как утверждённый лимит в два переноса.

B без удаления/выноса/доказанного аннотирования остающегося reflection fallback НЕ даёт −3: analyzer видит сборку, а не только вызовы generated path. Вынос в обязательный пользовательский пакет отвергнут требованием совместимости; B не выбран для S15. C с fail-closed вместо обычного JIT fallback тоже не выбран. Исключения AOT при неизвестной форме не объявлять parity.

Порядок: (1) письменное одобрение этой записи; (2) план S15 и замер замкнутости DAM/публичного forwarding, provenance переноса и net относительно205; (3) ограниченная реализация A только после отдельного разрешения плана; (4) канонический publish и все требуемые сьюты; (5) отдельный дизайн/одобрение compile-time materializers, projection bindings и устранения carriers — обязательная следующая часть Stream A. Не заменять последнюю часть трактовкой DAM=генерация.

## 4. Полная классификация S14

Таблица ниже покрывает все уникальные пары code/site; группировка по (path,code), номера строк перечислены полностью. Один и тот же source site с разными кодами считается несколько раз по registry contract. Disposition описывает обязательную судьбу, НЕ свидетельство уже выполненного исправления и НЕ blanket acceptance. Ни одна из 205 строк этой записью не объявлена безусловно допустимой навсегда.

Классы:
- `A-remove`: три сайта текущего resolver должны исчезнуть; новые входные предупреждения учитываются отдельно.
- `Owner-remediate`: metadata — законный owner инспекции, но имеющиеся предупреждения требуют доказанного member flow/генерации; место владения само по себе не разрешает warning. На A-инкременте остаются открытой задолженностью.
- `JIT-compat/open`: legacy metadata factory сохраняется ради JIT; AOT miss закрыт явно, но IL3050 остаётся открытым до доказанного исправления. Не platform exemption.
- `Platform-replace`: JSON/CSV API-граница; заменить опасную динамическую serialization/formatting форму AOT-safe contract (например, явно переданная/generated JSON metadata вместо runtime inference), сохранить формат/семантику. Не считать warning исключением только из-за System.Text.Json.
- `Runner-remove`: runner обязан перестать вызывать unsafe overloads после появления безопасного продуктового пути, не вырезая сценарии/провайдеры. Stream B execution evidence отдельно.
- `Pipeline-remove`: runtime reflection/generic construction/доступ к членам в query, projection, joins, translation и extensions не является metadata-владением. Требуется typed/generated binding/dispatch с сохранением текущей семантики; A этого не закрывает.

| Путь | Код | Уникальных | Строки | Disposition | Обоснование |
|---|---:|---:|---|---|---|
| `src/nextorm.core/Builders/CollectionSlot/CollectionSlotBinder.cs` | IL2060 | 1 | 240 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/CollectionSlot/CollectionSlotBinder.cs` | IL3050 | 3 | 143, 240, 434 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/CollectionSlot/CollectionSlotRecognizer.cs` | IL2070 | 1 | 496 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/CollectionSlot/CollectionSlotShapes.cs` | IL3050 | 1 | 63 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Entity/EntityBuilder.cs` | IL2026 | 5 | 2706, 2995, 3001, 3144, 5297 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Entity/EntityBuilder.cs` | IL2070 | 2 | 6055, 6057 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Entity/EntityBuilder.cs` | IL3050 | 7 | 2711, 2722, 2729, 2735, 2946, 3011, 3157 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Entity/EntityBuilderEagerLoading.cs` | IL2075 | 1 | 280 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Entity/EntityBuilderEagerLoading.cs` | IL3050 | 3 | 290, 304, 597 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Entity/EntityBuilderExtensions.cs` | IL2060 | 1 | 2585 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Entity/EntityBuilderExtensions.cs` | IL3050 | 2 | 2562, 2585 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Entity/MetadataEagerLoadBinding.cs` | IL2060 | 2 | 1626, 1633 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Entity/MetadataEagerLoadBinding.cs` | IL3050 | 2 | 1626, 1633 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/CompositeJunctionLinkFactory.cs` | IL3050 | 2 | 65, 211 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/JoinIntoManyToManySpec.cs` | IL2026 | 1 | 639 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/JoinIntoManyToManySpec.cs` | IL2075 | 3 | 204, 205, 211 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/JoinIntoManyToManySpec.cs` | IL3050 | 3 | 627, 637, 651 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs` | IL3050 | 3 | 721, 757, 823 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/JoinIntoStitcher.cs` | IL2026 | 1 | 44 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/JoinIntoStitcher.cs` | IL2055 | 1 | 57 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/JoinIntoStitcher.cs` | IL2070 | 2 | 268, 287 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/JoinIntoStitcher.cs` | IL3050 | 1 | 57 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs` | IL2072 | 2 | 165, 232 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs` | IL2075 | 3 | 220, 221, 222 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs` | IL3050 | 6 | 43, 138, 149, 167, 238, 339 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Builders/Returning/JoinedReturningProjection.cs` | IL2070 | 1 | 250 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/DataContextExtensions.cs` | IL3050 | 1 | 377 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Mapping/EntitySelectListBuilder.cs` | IL3050 | 2 | 34, 39 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Meta/ConstructorInfoResolver.cs` | IL2070 | 3 | 18, 24, 30 | A-remove | Инспекция трёх форм конструктора передаётся подготовке CLR-метаданных (§2); учесть новые сайты. |
| `src/nextorm.core/DataContext/Meta/Converters/JsonColumnConverter.cs` | IL2026 | 6 | 102, 106, 120, 122, 124, 137 | Platform-replace | JSON/CSV serialization/binding boundary; нужен статический контракт, не автоматическое исключение платформы. |
| `src/nextorm.core/DataContext/Meta/Converters/JsonColumnConverter.cs` | IL3050 | 7 | 102, 106, 120, 122, 124, 137, 160 | Platform-replace | JSON/CSV serialization/binding boundary; нужен статический контракт, не автоматическое исключение платформы. |
| `src/nextorm.core/DataContext/Meta/Converters/ValueConverter.cs` | IL2075 | 1 | 140 | Owner-remediate | Конвертер владеет provider-type binding; доступ к членам должен иметь доказанный metadata contract. |
| `src/nextorm.core/DataContext/Meta/DefaultEntityMetadataProvider.cs` | IL3050 | 1 | 50 | JIT-compat/open | Runtime MakeGenericType factory §3.3; совместимость JIT, не AOT-доказательство. |
| `src/nextorm.core/DataContext/Meta/Entities/EntityMetadataBuilder.cs` | IL2070 | 4 | 405, 1209, 1297, 1322 | Owner-remediate | Построение mapping и инспекция членов принадлежат metadata; DAM PublicConstructors не исправляет свойства/атрибуты автоматически. |
| `src/nextorm.core/DataContext/Meta/Entities/EntityMetadataBuilder.cs` | IL2072 | 1 | 407 | Owner-remediate | Построение mapping и инспекция членов принадлежат metadata; DAM PublicConstructors не исправляет свойства/атрибуты автоматически. |
| `src/nextorm.core/DataContext/Meta/Entities/EntityMetadataBuilder.cs` | IL2075 | 1 | 413 | Owner-remediate | Построение mapping и инспекция членов принадлежат metadata; DAM PublicConstructors не исправляет свойства/атрибуты автоматически. |
| `src/nextorm.core/DataContext/Meta/Entities/EntityMetadataBuilder.cs` | IL2090 | 5 | 205, 231, 263, 309, 471 | Owner-remediate | Построение mapping и инспекция членов принадлежат metadata; DAM PublicConstructors не исправляет свойства/атрибуты автоматически. |
| `src/nextorm.core/DataContext/Meta/QueryFilters/QueryFilterFunc.cs` | IL2026 | 1 | 182 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Meta/QueryFilters/QueryFilterFunc.cs` | IL2060 | 1 | 35 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Meta/QueryFilters/QueryFilterFunc.cs` | IL3050 | 2 | 35, 87 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/QueryPlanner.cs` | IL2070 | 1 | 1210 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/QueryPlanner.cs` | IL3050 | 1 | 480 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Results/ArrayResultConverter.cs` | IL2060 | 1 | 126 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Results/ArrayResultConverter.cs` | IL3050 | 1 | 126 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Sql/Lowering/ExtremeRowLowering.cs` | IL2060 | 2 | 528, 540 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Sql/Lowering/ExtremeRowLowering.cs` | IL3050 | 9 | 67, 194, 388, 423, 508, 528, 540, 548, 614 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Sql/SqlBuilder.cs` | IL2026 | 1 | 2337 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Sql/SqlBuilder.cs` | IL2075 | 2 | 2216, 2333 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Sql/SqlBuilder.cs` | IL3050 | 1 | 1281 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Sql/SqlSourceRenderer.cs` | IL2070 | 1 | 1337 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/DataContext/Sql/SqlSourceRenderer.cs` | IL2075 | 1 | 1043 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Query/Commands/QueryCommand.QueryPreparer.cs` | IL2026 | 5 | 2046, 2575, 2613, 2743, 2886 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Query/Commands/QueryCommand.QueryPreparer.cs` | IL2070 | 2 | 1820, 2059 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Query/Commands/QueryCommand.QueryPreparer.cs` | IL3050 | 8 | 21, 653, 657, 2589, 2634, 2817, 2834, 3213 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Query/Csv/CsvStreamWriter.cs` | IL2026 | 1 | 299 | Platform-replace | JSON/CSV serialization/binding boundary; нужен статический контракт, не автоматическое исключение платформы. |
| `src/nextorm.core/Query/DictionaryLookup.cs` | IL2070 | 1 | 196 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Query/DictionaryLookup.cs` | IL2075 | 2 | 178, 179 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Query/InValues.cs` | IL2070 | 1 | 501 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs` | IL2111 | 1 | 32 | Platform-replace | JSON/CSV serialization/binding boundary; нужен статический контракт, не автоматическое исключение платформы. |
| `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs` | IL3050 | 1 | 634 | Platform-replace | JSON/CSV serialization/binding boundary; нужен статический контракт, не автоматическое исключение платформы. |
| `src/nextorm.core/Query/Json/JsonShapePlan.cs` | IL2067 | 1 | 469 | Platform-replace | JSON/CSV serialization/binding boundary; нужен статический контракт, не автоматическое исключение платформы. |
| `src/nextorm.core/Query/Json/JsonShapePlan.cs` | IL3050 | 1 | 469 | Platform-replace | JSON/CSV serialization/binding boundary; нужен статический контракт, не автоматическое исключение платформы. |
| `src/nextorm.core/Visitors/Aggregates/AdvancedAggregateTranslator.cs` | IL3050 | 1 | 930 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Visitors/Aggregates/AggregateTerminalRewriter.cs` | IL2060 | 2 | 68, 79 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Visitors/Aggregates/AggregateTerminalRewriter.cs` | IL3050 | 7 | 38, 39, 50, 68, 74, 79, 81 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Visitors/Core/CorrelatedQueryExpressionVisitor.cs` | IL2060 | 1 | 180 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Visitors/Core/CorrelatedQueryExpressionVisitor.cs` | IL3050 | 14 | 180, 217, 255, 813, 851, 930, 987, 989, 1114, 1140, 1330, 1591, 1853, 1868 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Visitors/Navigation/NavigationExpansion.cs` | IL3050 | 3 | 451, 513, 746 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Visitors/Providers/ClickHouseNativeFunctionTranslator.cs` | IL2075 | 1 | 1349 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Visitors/Ranges/PostgresRangeSqlTranslator.cs` | IL2055 | 1 | 90 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Visitors/Ranges/PostgresRangeSqlTranslator.cs` | IL3050 | 1 | 90 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Visitors/Ranges/RangeColumnsSqlTranslator.cs` | IL2070 | 7 | 461, 462, 463, 464, 465, 466, 467 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.core/Visitors/Sql/NormSqlTranslator.cs` | IL3050 | 1 | 352 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.extensions/ExtremeRowExtensions.cs` | IL2060 | 2 | 826, 833 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `src/nextorm.extensions/ExtremeRowExtensions.cs` | IL3050 | 11 | 439, 454, 687, 826, 833, 851, 852, 853, 854, 855, 856 | Pipeline-remove | Типизация/binding/dispatch выполняются в query pipeline, а не при регистрации metadata; требуется устранить runtime inspection/dynamic construction, сохранив семантику. |
| `tests/nextorm.aot.runner/Program.cs` | IL2026 | 9 | 29, 39, 64, 77, 90, 140, 150, 159, 188 | Runner-remove | Безопасные overloads при неизменном покрытии runner; исключение сценария не исправление. |
| `tests/nextorm.aot.runner/Program.cs` | IL3050 | 3 | 64, 77, 90 | Runner-remove | Безопасные overloads при неизменном покрытии runner; исключение сценария не исправление. |
### Сводка арифметики и дубликатов

Итоги рассчитаны по `registry-s14-baseline.tsv`: 211 сырых диагностик, 205 уникальных `(code,path:line)` после удаления шести повторных вхождений; `src` — 193, `tests` — 12. Таблица выше содержит 77 групп `(path,code)`. Counts ниже — уникальные пары, не число сгруппированных строк.

| Код | Уникальных |
|---|---:|
| IL3050 | 109 |
| IL2026 | 30 |
| IL2070 | 26 |
| IL2075 | 15 |
| IL2060 | 13 |
| IL2090 | 5 |
| IL2072 | 3 |
| IL2055 | 2 |
| IL2067 | 1 |
| IL2111 | 1 |

Проверка суммы: 109+30+26+15+13+5+3+2+1+1=205; 211−6 повторных вхождений=205.

| Disposition | Уникальных пар |
|---|---:|
| A-remove | 3 |
| Owner-remediate | 12 |
| JIT-compat/open | 1 |
| Platform-replace | 18 |
| Runner-remove | 12 |
| Pipeline-remove | 159 |

Повторные code/site записи в исходном TSV (multiplicity — число сырых вхождений):

| Код | Путь:строка | Сырых вхождений |
|---|---|---:|
| IL2026 | `src/nextorm.core/Query/Csv/CsvStreamWriter.cs:299` | 2 |
| IL2060 | `src/nextorm.core/DataContext/Sql/Lowering/ExtremeRowLowering.cs:540` | 2 |
| IL2060 | `src/nextorm.extensions/ExtremeRowExtensions.cs:826` | 2 |
| IL2070 | `src/nextorm.core/Query/Commands/QueryCommand.QueryPreparer.cs:2059` | 2 |
| IL3050 | `src/nextorm.core/Query/Json/JsonShapePlan.cs:469` | 2 |
| IL3050 | `tests/nextorm.aot.runner/Program.cs:64` | 2 |

Критерий AC1 после будущих шагов: повторный registry с provenance для каждого moved site, отсутствие непокрытых строк, индивидуально доказанное и одобренное основание каждого реально остающегося warning. Классификация намерения всех205 строк не равна выполнению AC1. На S14 AC1/AC2 не закрываются; Stream B не оценивается здесь.

## 5. Решения для письменного одобрения перед S15

1. **A только первый инкремент.** DAM не переопределяет AC2; цена — Stream A остаётся открытым, потребуется отдельная генерация. Диагностика: цель три сайта, net только измеряется; объём ограниченного шага меньше генератора, но не нулевой публичный каскад.
2. **Internal CLR descriptor + свойство internal EntityMetadata**, отдельное от mapping scope; сохранить boxed JoinInto и custom IEntityMetadata. Публичного interface break нет. Цена: constructor metadata/cache память, новые invariants и parity/cache tests; первоначальный runtime Type не исчезает полностью.
3. **DAM PublicConstructors на перечисленных корнях и транзитивно на forwarding.** Обычным JIT-пользователям не нужны пакеты/регистрация. Generic trim-consumers могут получить IL2091 и потребность в аннотациях. Цена образа — сохранённые public constructors; размер не измерен, не обещать нулевой рост. Число затронутых публичных generic-параметров определяется обязательным замером до принятия кодового плана; бесконтрольное расширение DAM mask не разрешено.
4. **Новая optional public RegisterMaterializationType<T>(IDataContext)** с точной сигнатурой §3.1, не EditorBrowsable-hidden. Цена: одна новая API-запись, долгосрочная поддержка, EN/RU документация, registration/threading/negative tests. AOT dynamic формы могут требовать явной регистрации; это промежуточное ограничение A, не parity. Обычный JIT её не требует.
5. **Сохранить JIT fallback в существующей поставке**, fail-closed только для отсутствующей AOT записи. Цена: остающиеся/перенесённые diagnostics анализатора учитываются полностью, DefaultEntityMetadataProvider:50 не исключается. Не обещать что guard удалит предупреждения; не выносить обязательный пакет.
6. **Принять классификацию §4 как план судьбы, не waiver.** Ни NoWarn, pragma, SuppressMessage, DynamicDependency, rooting, rd.xml, AssemblyMetadata, ни исключения строк из publish не допускаются. Цена — значительная последующая typed/generated работа за пределами S15.
7. **Проверки и документация в кодовом шаге:** конструкторы, nullable/value/anonymous/DTO/scalar/range, boxed JoinInto, неизвестная AOT форма до SQL, внешняя сборка, внешний IEntityMetadata, mapping scope/configured precedence, structural cache collisions и clone, generic forwarding с/без DAM. Канонический publish и обязательные существующие unit/integration suites; container suite только по проектному capped-run контракту, skipped providers не green. Публичные EN/RU docs объясняют DAM и optional registration, без ссылок на внутреннюю спеку. Цена: тестовая матрица и native evidence позже; эта задача ничего из перечисленного не запускала и не меняла.

### Граница handoff

Запрошено одобрение всех семи решений либо конкретных поправок. До него S15 не начинается. Эта запись не меняет `.pdca/status/T370.md`, счётчики, продуктовый код, API snapshots и публичные документы; не содержит результата CHECK. Tracking обновлён: [комментарий к #370](https://github.com/AlexeyShirshov/nextorm/issues/370#issuecomment-6874103856) фиксирует выбранное направление и статус предложений; milestone 1.0.10 проверен. Комментарий не является одобрением дизайна. Коммитов/push нет. Следующий артефакт после одобрения — проверяемый план S15 с точным DAM impact и измерительным гейтом, не немедленная реализация.
