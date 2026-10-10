# План релиза 1.0.9-rc2 (milestone 1.0.9-rc2)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.9-rc1.md`, `git show v1.0.9-rc1`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в
> `Directory.Build.props` (одна строка на все 7 пакетов), тег `v1.0.9-rc2` → tag-triggered
> CI-публикация 7 пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка выполнена; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0.9-rc2](https://github.com/AlexeyShirshov/nextorm/milestone/20) — 28 issues, все закрыты (open=0, closed=28): 26 `completed`, 2 `not_planned` (#171, #172 — принятые ограничения).
- **Тег:** `v1.0.9-rc2` (планируется)
- **Ветка релиза:** `1.0.9-rc2` (синхронна с `origin/1.0.9-rc2`)
- **База:** тег `v1.0.9-rc1` (`5412f3ab`); HEAD до подготовки — `a2dd5aca`, 101 коммит от `v1.0.9-rc1`.
- **Версии пакетов:** `1.0.9-rc2` задаётся **одной строкой** в `Directory.Build.props` (`<Version>`);
  CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный бамп нужен
  локальному `dotnet pack` и зависимостям nuspec (`nextorm.mariadb → nextorm.mysql 1.0.9-rc2`).
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC) — без изменений с 1.0.4–1.0.9-rc1.

## 1. Скоуп милстоуна

| # | Release notes |
| --- | --- |
| [#176](https://github.com/AlexeyShirshov/nextorm/issues/176) | JSON streaming Phase 2: вложенные проекции и `Projection<T1,T2>` |
| [#177](https://github.com/AlexeyShirshov/nextorm/issues/177) | JSON streaming Phase 3: DB-side fast-path (`FOR JSON` / `json_agg` / `JSONEachRow`) |
| [#178](https://github.com/AlexeyShirshov/nextorm/issues/178) | JSON streaming: поддержка `enum` (числовая форма и STJ `[JsonConverter]`-строки) |
| [#179](https://github.com/AlexeyShirshov/nextorm/issues/179) | JSON streaming: политика частичного вывода/ошибок (валидный JSON при сбое) |
| [#180](https://github.com/AlexeyShirshov/nextorm/issues/180) | JSON streaming: провайдерные конвертации колонок и тип исключения валидации |
| [#167](https://github.com/AlexeyShirshov/nextorm/issues/167) | CSV streaming: потоковое чтение `byte[]`-колонок чанками (bounded memory) |
| [#160](https://github.com/AlexeyShirshov/nextorm/issues/160) | Join aliases: свободное смешивание positional/alias + алиас корня (`.WithAlias(Alias.X)`) |
| [#159](https://github.com/AlexeyShirshov/nextorm/issues/159) | CTE: прямые перегрузки join-семьи и alias-API для `Cte<T>` |
| [#206](https://github.com/AlexeyShirshov/nextorm/issues/206) | Source generator: CS0111 при двух alias-join с общим `Alias.X` (уже поддержано; регрессионный тест) |
| [#208](https://github.com/AlexeyShirshov/nextorm/issues/208) | SQL Server native JSON: проекция `SqlFunctions.Parameter<T>` снова рендерится с alias (`FOR JSON`) |
| [#190](https://github.com/AlexeyShirshov/nextorm/issues/190) | Fix: выбор всей сущности из JOIN-проекций |
| [#173](https://github.com/AlexeyShirshov/nextorm/issues/173) | Mapping: fold проекций больше не игнорирует `Member` (`SelectExpressionPlanEqualityComparer`/`RowMapperFactory`) |
| [#185](https://github.com/AlexeyShirshov/nextorm/issues/185) | Fix: `BindEntity<T>` регистрирует маппинг сущности (типизированные проекции без `From<T>`) |
| [#174](https://github.com/AlexeyShirshov/nextorm/issues/174) | Fix: `DataContextCache.Clear()` очищает кэш `JoinIntoSpec.IdentitySelectorCache` |
| [#170](https://github.com/AlexeyShirshov/nextorm/issues/170) | Navigation: валидация уникальности FK для OneToOne |
| [#169](https://github.com/AlexeyShirshov/nextorm/issues/169) | Table hints: сохранение `TablesInScopeHints` на multi-table DELETE/UPDATE join-путях |
| [#184](https://github.com/AlexeyShirshov/nextorm/issues/184) | `LoadWith`: режим eager loading вынесен в параметр `EagerLoadMode` (`AsSingleQuery()` удалён) |
| [#191](https://github.com/AlexeyShirshov/nextorm/issues/191) | Provider-specific fluent API вынесен из `EntityBuilder` в extensions провайдерных сборок |
| [#189](https://github.com/AlexeyShirshov/nextorm/issues/189) | `ToDataReader`/`ToDataReaderAsync`: поддержка SQLite для не-LOB проекций |
| [#162](https://github.com/AlexeyShirshov/nextorm/issues/162) | V32: навигация поверх temp-table/TVP-источников отклоняется fail-closed |
| [#188](https://github.com/AlexeyShirshov/nextorm/issues/188) | Comparison benchmarks tier 1: проекции, агрегаты, пагинация, стриминг + cross-library JSON/CSV |
| [#153](https://github.com/AlexeyShirshov/nextorm/issues/153) | PG/CH диалекты `sealed`→`class`: зафиксировано решение о публичной расширяемости |
| [#154](https://github.com/AlexeyShirshov/nextorm/issues/154) | ExtremeRow capability DTO: публичная read-only конструкция вместо internal-конструкторов |
| [#151](https://github.com/AlexeyShirshov/nextorm/issues/151) | Re-run Stryker mutation testing для native extreme-row (фиксация capture) |
| [#157](https://github.com/AlexeyShirshov/nextorm/issues/157) | Согласована запись finding 25 в регистрах |
| [#175](https://github.com/AlexeyShirshov/nextorm/issues/175) | JoinInto test-quality debt: проверки hash-inequality / видимость `EagerLoadSpec.Assign` |

Принятые ограничения (issue закрыты как `not_planned`, ревизия по появлении конкретного потребителя):

| # | Причина |
| --- | --- |
| [#171](https://github.com/AlexeyShirshov/nextorm/issues/171) | `JoinInto` поверх `As`/derived-источника — нет конкретного потребителя и согласованной семантики; существующий guard уже отклоняет вход |
| [#172](https://github.com/AlexeyShirshov/nextorm/issues/172) | Child-collection проекция в anonymous/derived-форму (`NORM.ChildCollection`) — форма не предоставляется; нет тестируемого acceptance-критерия |

Новых страниц документации в этом релизе нет — только правки существующих страниц EN + RU
(`guide/02-joins`, `guide/03`, `guide/04`, `guide/07`, `guide/08-cte`, `guide/12-raw-sql`,
`guide/13-query-hints`, `guide/14-json`, `guide/28-streaming-data`, `guide/provider-specific/*`,
`advanced/relationships`, `advanced/select-where-extrema-native`, `querying/01-projections`,
`comparisons/benchmarks`, `infrastructure/01-query-reuse-and-caching`).

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` (7 пакетов).
Пакет `nextorm.entityframeworkcore` в публикацию **не входит** (не в списке job `publish`).
**Breaking changes (source):** #184 (удалён публичный `AsSingleQuery()`, режим вынесен в параметр
`EagerLoadMode`), #191 (provider-specific fluent API перемещён из core в провайдерные assemblies).

## 2. Предрелизные проверки

Все проверки выполнены на HEAD `a2dd5aca` (ветка `1.0.9-rc2`), после завершения коллекции rc2
и пост-коллекционных коммитов (#160, #206).

- [x] `dotnet build nextorm.slnx -c Debug` — **0 warning / 0 error** (56.8 s).
- [x] `dotnet build nextorm.slnx -c Release` — **0 warning / 0 error** (29.0 s).
- [x] Полный прогон (`dotnet test nextorm.slnx -c Debug --no-build`,
      `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) —
      **total 10095, failed 0, succeeded 9897, skipped 198**; `nextorm.integration.tests` реально
      стартовал контейнеры (1m 25s), все 6 провайдеров исполнены, скипы 198 — **capability-based**
      (напр. «SQL Server exposes row-returning JSON through CROSS APPLY OPENJSON»), ни одного
      skip по недоступности провайдера.
- [x] Краснота CI воспроизведена и устранена (см. §3.5): локальный прогон был зелёным из-за
      наличия gitignored `artifacts/`, а в свежем CI-checkout падали 2 теста #160
      (`PositionalChainSqlInvariantTests`, `JoinAliasMixingSeamTests`) — их baseline-корпус не коммитился.
      После выноса корпуса в tracked fixtures: прогон при **удалённом** `artifacts/…/baseline` зелёный.
- [x] Покрытие (репро CI: `dotnet-coverage collect -s coverage.settings.xml` + `reportgenerator`) —
      **Line 88.4 % / Branch 80.4 %** (порог 85 / 75). Разбивка: `nextorm.core` 88.3 %,
      `nextorm.sqlite` 90.5 %, `nextorm.postgres` 90.2 %, `nextorm.sqlserver` 95 %.
- [x] Docs EN + RU синхронны; `dotnet docfx docs/docfx.json` — **exit 0**, **0 error**
      (2 warning — дубликаты исходников в пустом `nextorm.core.sourcegenerator`, известный шум, не связан с релизом).
- [x] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.9-rc2` (dry-run) — **успешно** (7 `.nupkg`);
      nuspec `nextorm.mariadb` ссылается на `nextorm.mysql 1.0.9-rc2`, core-пакет — `1.0.9-rc2`.
- [x] `nextorm-code-auditor` (release-gate на дельте `v1.0.9-rc1..HEAD`) — **PASS**:
      release-blocking P0/P1 отсутствуют; подавлений в дельте 0/0, ratio 5/5, IDisposable/LINQ/hash-key/
      optional-null — чисто. P2 (не блокируют): `N184-1` (`AsSingleQuery()` удалён — binary-BC при
      будущей заморозке, но стабильного релиза с этим API нет), `NJSON1` (4 новых `WriteJson`-перегрузки —
      additive, инвентарь для заморозки), `NJSON2`/`N184-2` (косметика/нейминг), `IVT-bench`
      (`InternalsVisibleTo nextorm.benchmark`), god-class `EntityBuilder` (pre-existing).

## 3. Изменения в репозитории (поверх HEAD `a2dd5aca`; §3.5 — поверх `75f38b52`, `release prepare`)

### 3.1. Бамб версии

- **`Directory.Build.props`:** `<Version>1.0.9-rc1</Version>` → `1.0.9-rc2`.

### 3.2. `docs/index.md`

- `## Status` — `1.0.9-rc1` → `1.0.9-rc2`.
- `## Releases` — добавлен раздел `### 1.0.9-rc2` (26 issues #151–#208 + строка принятых ограничений #171/#172).

### 3.3. `docs/ru/index.md`

- Без изменений: новых страниц в релизе нет, секции `Status`/`Releases` в русском индексе не ведутся.

### 3.4. Служебное

- **`docs/specs/release-1.0.9-rc2.md`** — этот план.
- **`docs/specs/design/code-smells-review.md`**, **`docs/specs/design/API-NAMING-REVIEW.md`** — записи
  предрелизного аудита (§2; только регистры, продуктовый код не менялся).

### 3.5. Fix красноты CI: baseline-корпус #160 вынесен в tracked fixtures

CI (`build` job, шаг `Test with coverage`) падал на 2 тестах, добавленных #160 (`a60ecb84`):

- `nextorm.sqlite.tests` → `PositionalChainSqlInvariantTests.Positional_chain_sql_is_byte_identical_to_the_frozen_baseline`
  (`artifacts/pdca/D160/rv1/N2/baseline/positional/sqlite/01-inner.sql`);
- `nextorm.core.tests` → `JoinAliasMixingSeamTests.Positional_chain_expression_identity_is_frozen_and_deterministic`
  (`artifacts/pdca/D160/rv1/N2/baseline/positional/01-inner.txt`).

Причина: «замороженный» baseline-корпус лежал под `artifacts/` — каталог в `.gitignore` (снят с учёта
в rc1, см. §3.4 `release-1.0.9-rc1.md`), поэтому в свежем CI-checkout файлов нет и
`File.Exists(baselinePath)` = false. Локально тесты были зелёными, т.к. файлы оставались на диске.

Фикс (только тесты, продуктовый код не менялся):

- baseline-корпус закоммичен: `tests/nextorm.core.tests/Baselines/D160/positional/*.txt` (8 файлов) и
  `tests/nextorm.sqlite.tests/Baselines/D160/positional/sqlite/{*.sql,*.sha256}` (16 файлов);
- `DefaultBaselineRoot` в обоих тестах указывает на tracked-fixtures (env-override `D160_BASELINE_DIR`
  сохранён); `DefaultEvidenceRoot` (revised, запись) остаётся в `artifacts/` — он только генерируется;
- добавлен `.gitattributes` с `tests/nextorm.{core,sqlite}.tests/Baselines/** -text`: корпус сравнивается
  побайтово, и при `core.autocrlf=true` git иначе перезаписал бы его в CRLF на checkout и сломал бы
  сравнение. Проверено `git checkout-index` при `autocrlf=true`: байты на выходе LF и идентичны эталону;
- герметичная проверка: при **удалённом** `artifacts/pdca/D160/rv1/N2/baseline` оба класса зелёные
  (core 3/3, sqlite 3/3) — тест читает именно закоммиченный корпус.

## 4. Публикация (шаги владельца)

- [ ] Закоммитить подготовленный объём (§3) на ветке `1.0.9-rc2` и запушить её.
- [ ] Убедиться, что ветка `1.0.9-rc2` зелёная (coverage hard-fail только на `main`).
- [ ] (Опционально) влить `1.0.9-rc2` в `main` через PR — как для предыдущих релизов.
- [ ] Создать тег `v1.0.9-rc2` → CI job `publish` (OIDC) отправляет 7 пакетов на nuget.org.
- [ ] Создать GitHub Release (prerelease): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#109-rc2> + 7 пакетов + issues #151–#208.
- [ ] Убедиться, что milestone 1.0.9-rc2 закрыт (его issues уже closed).
- [ ] После публикации: бампнуть `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.9-rc2`
      (сейчас там `1.0.8-b`; бамп rc1 также не выполнялся).

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.9-rc2`; `docs/index.md` обновлён (Status + Releases).
- [ ] Тег `v1.0.9-rc2` запушен; CI отправил 7 пакетов на nuget.org.
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#109-rc2` доступен.
- [ ] Milestone 1.0.9-rc2 и issues #151–#208 закрыты.

## 6. Открытые вопросы

- **CI был красным (исправлено, ждёт push).** Пуш `release prepare` (`75f38b52`) и предыдущий пуш
  (`a2dd5aca`) упали на `build` → `Test with coverage` с **2** падениями `PositionalChainSqlInvariantTests`
  (sqlite) и `JoinAliasMixingSeamTests` (core) — baseline-корпус #160 не коммитился (gitignored `artifacts/`).
  Фикс §3.5 (только тесты; продуктовый код не менялся) проверен герметично. **До зелёного CI тег
  `v1.0.9-rc2` создавать нельзя.**
- **Breaking changes (source):** #184 (`AsSingleQuery()` удалён, `LoadWith(..., EagerLoadMode mode = EagerLoadMode.Default)`),
  #191 (provider-specific fluent API перемещён в провайдерные assemblies). Релиз prerelease (`rc`),
  backward compatibility между пререлизами не гарантируется (см. `readme.md` §Status).
- **#171/#172 закрыты как `not_planned`** — принятые ограничения (нет потребителя/семантики),
  ревизия по отдельному issue с тестируемыми acceptance-критериями.
- **`nextorm.entityframeworkcore`** — пакет есть, но в job `publish` (список из 7) его нет; решает владелец.
- **Заморозка публичного API** (`PublicAPI.Shipped/Unshipped.txt`, `PublicApiAnalyzers`, issue #53) —
  P2, вне скоупа; новые публичные элементы rc2 (`EagerLoadMode`, JSON-перегрузки `WriteJson`,
  `WithAlias`) внесены в регистр нейминга для будущей фиксации.
- **Milestone 1.0.9** (номер #4) — 6 открытых issues; следующий этап, в этот релиз не входит.
