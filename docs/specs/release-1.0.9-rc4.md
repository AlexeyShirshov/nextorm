# План релиза 1.0.9-rc4 (milestone 1.0.9-rc4)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.9-rc3.md`, `git show v1.0.9-rc3`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в
> `Directory.Build.props` (одна строка на все 7 пакетов), тег `v1.0.9-rc4` → tag-triggered
> CI-публикация 7 пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка выполняется; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0.9-rc4](https://github.com/AlexeyShirshov/nextorm/milestone/22) — 84 issues, все закрыты (open=0, closed=84): 83 `completed`, 1 `not_planned` (#351 — принятое ограничение, см. §1).
- **Тег:** `v1.0.9-rc4` (планируется)
- **Ветка релиза:** `1.0.9-rc4` (синхронна с `origin/1.0.9-rc4`)
- **База:** тег `v1.0.9-rc3` (`15a4d0bc`); HEAD до подготовки — `67ba9494`, 588 коммитов от `v1.0.9-rc3`.
- **Версии пакетов:** `1.0.9-rc4` задаётся **одной строкой** в `Directory.Build.props` (`<Version>`);
  CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный бамп нужен
  локальному `dotnet pack` и зависимостям nuspec (`nextorm.mariadb → nextorm.mysql 1.0.9-rc4`).
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC) — без изменений с 1.0.4–1.0.9-rc3.

## 1. Скоуп милстоуна

Реестр задач — release notes `docs/index.md` §`### 1.0.9-rc4` (83 issue-ссылки, порядок по номеру).
Крупные кластеры:

| Кластер | Issues |
| --- | --- |
| Каталог built-in функций по всем провайдерам | #248, #260–#264, #276–#286, #290, #332 |
| Новые переносимые возможности SQL | #287 (NULLS FIRST/LAST), #288 (`IS [NOT] DISTINCT FROM`), #289 (savepoints), #266 (DML `OUTPUT`), #301 (MERGE-присваивания), #323 (`SelectGroup`) |
| TVF и связи | #249, #251, #231, #320, #322, #330, #331 |
| Коррелированные запросы и проекции | #279, #295, #300, #303, #304, #307, #312–#315, #324, #343, #350 |
| ClickHouse correctness | #252–#257, #277, #308, #335–#338 |
| Нативный JSON | #325 (MySQL), #326 (SQL Server 2025 / Azure SQL), #345 |
| Prepared command и кэш планов | #333, #316 |
| Тестовая инфраструктура | #328, #329, #339, #341, #342, #347, #348, #349 |

Принятые ограничения (issue закрыт как `not_planned`):

| # | Причина |
| --- | --- |
| [#351](https://github.com/AlexeyShirshov/nextorm/issues/351) | Застой ClickHouse-запроса на полный 120-секундный `HttpClient`-таймаут без обращения к серверу. Две диагностические сессии кампании `integration-branch-coverage-11` дали вердикт `undetermined`: серверная сторона чистая (`system.query_log` — ни одного запроса дольше 200 мс, трафик в окне застоя падает), клиентская половина шкалы недоступна (обход ретранслятора невозможен — прямой адрес контейнера и шлюз отдают `curl exit 28`). Измеримые причины этого класса закрыты #328/#329/#338/#349; дальнейшая диагностика требует изменения маршрутизации/инфраструктуры, а не продукта |

**Breaking changes (consumer-visible).** В отличие от rc3, в rc4 **есть** намеренные изменения
публичной поверхности — пререлиз, совместимость между пререлизами не гарантируется:

- **#291** — `Avg`/`avg_over` над целочисленной колонкой теперь возвращают `double?`/`WindowFunction<double?>` вместо целочисленного типа (тихое усечение среднего устранено);
- **#333** — `Prepare(bool nonStreamUsing, CancellationToken)` удалён в пользу
  `Prepare(PrepareOptions, CancellationToken)` (`PrepareOptions { Streaming, CommandCache }`,
  политика по умолчанию — per-connection); из публичного prepared-API удалены `DbCommand` и `LastRowCount`;
- **#289** — на `ITransactionManager` добавлены `CreateSavepoint`/`RollbackToSavepoint`/`ReleaseSavepoint` (default interface members; для внешних реализаций контракт расширился);
- **#301** — MERGE: expression-based присваивания в `ThenUpdate`/`ThenInsert` вместо прежнего ограничения на константы (авторизованный break пререлиза).

Поведенческие изменения без изменения сигнатур: `nullable == null` рендерится как `IS NULL` (#296),
fail-closed `WithTies()` + ненулевой `OFFSET` на SQL Server (#298), экранирование литералов и
backslash-режимы/NUL (#299, #327), identity плана учитывает `ApplySource` (#316), сырые
`ArgumentException`/`InvalidOperationException` заменены документированными `NotSupportedException`/
контекстными `QueryPreparationException` (#293–#295, #307, #313–#315, #343), `ErrorCode -1` больше не
транспортный сигнал ClickHouse-драйвера (#338).

Новые страницы документации (EN + RU): `docs/comparisons/provider-features.md`,
`docs/guide/13-tvf-result-schemas.md`, `docs/relationships/collection-projection.md`,
`docs/string-format-invariant-provider.md`; остальные правки — существующие страницы.

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` (7 пакетов).
Пакет `nextorm.entityframeworkcore` в публикацию **не входит** (не в списке job `publish`).

## 2. Предрелизные проверки

Проверки выполнены на `67ba9494` (ветка `1.0.9-rc4`) плюс правки §3 (продуктовый код не менялся —
только версия, release notes, план и два xref в API-reference).

- [x] `dotnet build nextorm.slnx -c Debug` — **0 warning / 0 error** (2m00s).
- [x] `dotnet build nextorm.slnx -c Release` — **0 warning / 0 error** (5m50s, прогон release-gate аудита; повторная сборка up-to-date).
- [x] 11 unit/provider-проектов (`dotnet test <project> -c Debug --no-build`, серийно) —
      **total 13721, failed 0, succeeded 13712, skipped 9**.
- [x] `tests/nextorm.integration.tests` канонической командой
      (`DOCKER_HOST=… dotnet run … -- -noColor -notrait "Category=Diagnostic"`) —
      **total 7918, errors 0, failed 1, skipped 197** (956.6 s). Единственный красный —
      `ClickHouseStringLiteralSafetyExecutionTests.ClickHouse_Like_ShouldUseDefaultBackslashEscapingWithoutAnEscapeClause`,
      `TaskCanceledException: The request was canceled due to the configured HttpClient.Timeout of 120 seconds` —
      это ровно подпись принятого ограничения #351 (серверная сторона чиста, улики в `.pdca/integration-branch-coverage-11/rv2/diagnostics/`).
      Изолированный повтор этого же теста — **1/1 passed за 7.8 s**, то есть дефекта продукта нет.
- [x] Skip-budget: `python3 tools/integration/check-integration-skip-budget.py --log …` — **`OK: 197 skips match the pinned budget`** (exit 0).
- [x] Итог по всем прогонам: **total 21639, failed 1** (только #351), **skipped 206**, ни одного провайдера
      не пропущено через `NEXTORM_SKIP_PROVIDERS`.
- [x] Покрытие — CI run `40874052265` на `67ba9494` (**success**, шаги `Test with coverage`,
      `Integration coverage (dedicated gate)`, `Enforce coverage thresholds`, `Coverage summary` — зелёные):
      solution **Line 89 % / Branch 83.3 %** (порог 85 / 75); integration gate **Line 79.8 % / Branch 74.4 %**
      (порог 65 / 75). ⚠️ Integration branch ниже 75 % — hard-fail только на `main`, см. §6.
- [x] Docs EN + RU синхронны; `dotnet docfx docs/docfx.json` — **exit 0, 0 error, 4 warning**:
      2 — известный шум «duplicate source file» пустого `nextorm.core.sourcegenerator`;
      2 — пре-существующий `UidNotFound` на `System.ComponentModel.DataAnnotations.Schema.ColumnAttribute`
      в `docs/guide/14-raw-sql.md` (+RU), файл в этой строке не менялся в rc4.
      Найден и исправлен дефект дельты: после #333 в `docs/advanced/api-reference.md` (+RU) висел
      xref на удалённую перегрузку `Prepare(EntityBuilder<T>, bool, CancellationToken)` → заменён на
      `Prepare(EntityBuilder<T>, PrepareOptions, CancellationToken)`; устаревший `docs/api` (gitignored)
      пересобран с нуля — это убрало ложный `InvalidBookmark` на удалённый `FromTableFunction<T>`.
- [x] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.9-rc4` (dry-run) — **успешно** (7 `.nupkg`);
      nuspec `nextorm.mariadb` ссылается на `nextorm.mysql 1.0.9-rc4`, core-пакет — `1.0.9-rc4`.
- [x] Release-gate аудит дельты `v1.0.9-rc3..HEAD` (`nextorm-code-auditor`) — **NOT BLOCKING**, P0/P1 нет.
- [x] CI на `67ba9494` (run `40874052265`) — **success**, все шаги, включая coverage-гейты и benchmark-таблицы.

## 3. Изменения в репозитории (поверх HEAD `67ba9494`)

### 3.1. Бамб версии

- **`Directory.Build.props`:** `<Version>1.0.9-rc3</Version>` → `1.0.9-rc4`.

### 3.2. `docs/index.md`

- `## Status` — `1.0.9-rc3` → `1.0.9-rc4`.
- `## Releases` — добавлен раздел `### 1.0.9-rc4` (83 issues + строка принятых ограничений #351);
  `### 1.0.9-rc3` и ниже не тронуты.

### 3.3. Служебное

- **`docs/specs/release-1.0.9-rc4.md`** — этот план.
- **Тест-классы кампании покрытия (`cycle 13`)** — `tests/nextorm.integration.tests/**`
  (`C13*`, `CorePreparer*`, `MySqlNullOrdinalOrderingTests`) и файлы `.pdca/status/integration-branch-coverage-{11,12,13}.md`.
- **`docs/specs/design/code-smells-review.md`**, **`docs/specs/design/API-NAMING-REVIEW.md`** — разделы
  «Предрелизный аудит v1.0.9-rc4» (§3.4).

### 3.4. Release-gate аудит дельты `v1.0.9-rc3..HEAD`

`nextorm-code-auditor`, 588 коммитов, `src/**` 204 файла, +48 218/−3 226: **P0/P1 нет**. Проверены
ключ план-кэша (`ApplySource`/`NullsPosition`/`SelectGroup`/`CollectionSlotShape` — в `Equals` **и** хэше),
новый per-connection кэш подготовленной команды (#333: lease/CAS, `Broken`-состояние, порядок
reader→lease; ℹ️(C) rc3 про `CommandReaderOwner` исправлено), политики строковых литералов и
PostgreSQL-NUL на всех швах (#327/#299), MySQL-специализация `CommandText`, eager-load-склейка по
полному ключу (#249/#251), `ORDER BY … NULLS` (#287) и портативный bulk insert (#305).
`dotnet build -c Release` — 0/0, подавления 12/12 оправданных, слоп-паттернов нет.

Открыты только P2/ℹ️: `MapComponents` даёт `IndexOutOfRangeException` вместо именной диагностики при
рукописных `ManyToOne`-метаданных с неверной арностью (`src/nextorm.core/Builders/MetadataEagerLoadBinding.cs:581` —
заведён **#353** в milestone `1.0.9-rc5`), публичные удаления #333 (`LastRowCount`/`DbCommand`/`nonStreamUsing`)
и замена TVF-вида `IQueryable<…>` на `TvfEntityBuilder<…>` (#320) — раскрыты в release notes, инертная
`.editorconfig` (6 `S*` без `SonarAnalyzer`), осиротевший `<summary>` в `ClickHouseNativeFunctionTranslator`.
Записи — `docs/specs/design/code-smells-review.md` и `docs/specs/design/API-NAMING-REVIEW.md`
§«Предрелизный аудит v1.0.9-rc4» (append, CRLF сохранён).

### 3.5. Документация: xref на удалённую перегрузку `Prepare`

#333 удалил `Prepare(EntityBuilder<TEntity>, bool, CancellationToken)`, но в
`docs/advanced/api-reference.md:51` и `docs/ru/advanced/api-reference.md:51` остался xref на него —
DocFX падал в `UidNotFound`. Ссылка заменена на
`Prepare(EntityBuilder<TEntity>, PrepareOptions, CancellationToken)` в обоих языках.

## 4. Публикация (шаги владельца)

- [ ] Закоммитить подготовленный объём (§3) на ветке `1.0.9-rc4` и запушить её.
- [ ] Убедиться, что ветка `1.0.9-rc4` зелёная (coverage hard-fail только на `main`).
- [ ] (Опционально) влить `1.0.9-rc4` в `main` через PR — как для предыдущих релизов.
- [ ] Создать тег `v1.0.9-rc4` → CI job `publish` (OIDC) отправляет 7 пакетов на nuget.org.
- [ ] Создать GitHub Release (prerelease): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#109-rc4> + 7 пакетов + issues #231–#351.
- [ ] Закрыть milestone 1.0.9-rc4 (его issues уже closed, open=0).
- [ ] После публикации: бампнуть `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.9-rc4`
      и обновить дату/строку прогона в `examples/VERIFICATION.md`.

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.9-rc4`; `docs/index.md` обновлён (Status + Releases).
- [ ] Тег `v1.0.9-rc4` запушен; CI отправил 7 пакетов на nuget.org.
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#109-rc4` доступен.
- [ ] Milestone 1.0.9-rc4 закрыт.

## 6. Открытые вопросы

- **#351** закрыт как принятое ограничение; если застой повторится после релиза — новый тикет с
  дампом сокетов/эфемерных портов в момент таймаута (см. §1). В локальном прогоне §2 он воспроизвёлся
  один раз (`ClickHouseStringLiteralSafetyExecutionTests…`, 120 s), тест зелёный в изоляции.
- ⚠️ **Integration branch coverage 74.4 % < 75 %** на `67ba9494` (`main`-only hard-fail). Если вливать
  rc4 в `main` сейчас, gate покраснеет. В релизный коммит входят тест-классы кампании `cycle 13`,
  написанные ровно под этот числитель, — ожидаемый прирост надо подтвердить CI-прогоном на коммите
  релиза; иначе PR в `main` откладывается до результата кампании (её цель — 76 %).
- **#353** (P2 из release-gate аудита: `MapComponents` при рукописных `ManyToOne`-метаданных с
  `FK.Count > PrincipalKey.Count`) — заведён в `1.0.9-rc5`, в rc4 не входит.
- **`nextorm.entityframeworkcore`** — пакет есть, но в job `publish` (список из 7) его нет; решает владелец.
- **Заморозка публичного API** (`PublicAPI.Shipped/Unshipped.txt`, `PublicApiAnalyzers`, issue #53) —
  вне скоупа rc4, остаётся открытой в milestone `1.0.9` вместе с промо-задачей #334.
- **Perf-остатки** (#245–#250, #267–#274, #302, #311, #317–#319 и новый #352 `xxh3`/`xxh32` на MariaDB 11.4) —
  milestone `1.0.9-rc5`, в rc4 не входят.
- **Кампания branch coverage** (`integration-branch-coverage`, цикл 13, цель 76 %) — идёт отдельно от
  релиза; её тесты вошли в релизный коммит как обычные интеграционные тесты, приёмочного измерения
  кампании это не заменяет.
