> SUPERSEDED: This artifact is an edited, non-verbatim transcription of the planner return. Preserve it unchanged as historical evidence; the exact complete source is `.pdca/status/issues-65-69-cycle-N3-plan-verbatim.md`.

---8<--- PLAN BEGIN
# План цикла N3 (`N=3, r=1, n=0`) — Кампания `issues-65-69`, Остаток Шага 1 (`#65`)

## 1. Форма и специализированный скилл (`Form and specialized skill`)
- **Форма и оверлей (по ссылке, без переклассификации):** `incremental` + `pdca-coder` + `pdca-dotnet` + `nextorm-pdca` (обоснование формы и оверлея перенесено по ссылке из `.pdca/status/issues-65-69-plan-r1.md` и `.pdca/status/issues-65-69-cycle-N2-plan.md`).
- **Счётчики на входе в PLAN:** `N=3`, `r=1`, `n=0`. Циклы `N1` и `N2` закрыты в ACT со статусом `blocked`/`incomplete` без фазы CHECK и без принятого инкремента; настоящий вызов открывает новый цикл `N3` при неизменном контракте результата `r=1` (не переоткрытие `N2` и не ревизия критериев).
- **Область входа (`Entry scope`):** Остаток Шага 1 (`#65`, кандидат `MappingScope`) — разрешение атрибуции 51 падения полного интеграционного прогона `N2` на чистом изолированном состоянии (`clean-worktree attribution`) и закрытие критериев `#65` до любого перехода к Шагу 2 (`#69`).

---

## 2. Цель и критерии приёмки (`Goal and acceptance criteria`)
- **Исходная задача пользователя (дословно по `.pdca/status/issues-65-69.md` §Original task):**
  > «pdca по задачам 65 и 69 автономно с автокомитом»
  вместе с явным продолжением пользователя на цикл `N3`: доказать атрибуцию падений полного прогона `N2` в чистом временном worktree `/tmp/nextorm-ac65` исключительно на кандидате `#65` (`MappingScope`) без посторонніх неиндексированных/защищённых изменений (`#26` и др.).
- **Запрошенный результат (`Requested result`):** Подтверждённое чистыми доказательствами выполнение остатка Шага 1 (`#65`, `MappingScope`) с точной поимённой атрибуцией падений из `N2` и прохождением канонического полного интеграционного набора и skip-budget на изолированном кандидате `#65` (без артефактов/тестов `#26` и других защищённых изменений), готовое к независимой проверке CHECK и принятию инкремента `#65`.
- **Поставляемый результат (`Delivered result`):** Тот же объект — верифицированный кандидат `#65` (`MappingScope`) с полным комплектом доказательств атрибуции из чистого worktree `/tmp/nextorm-ac65`, обновлённым манифестом/валидаторами (при зелёном прогоне) или точной маршрутизацией по классам/идентификаторам тестов (при сохранении падений).
- **Критерии приёмки (`Acceptance criteria`):**
  Набор критериев `AC-65-1`, `AC-65-2`, `AC-65-3`, `AC-REPO-1` остаётся **неизменным (`r=1`)** и переносится **дословно (verbatim) по точной ссылке** из `.pdca/status/issues-65-69-cycle-N2-plan.md` §3 (и базового плана `.pdca/status/issues-65-69-plan-r1.md`), без добавлений, ослаблений или переформулировок:
  - **`AC-65-1`** — дословный текст критерия в `.pdca/status/issues-65-69-cycle-N2-plan.md` §3.
  - **`AC-65-2`** — дословный текст критерия в `.pdca/status/issues-65-69-cycle-N2-plan.md` §3.
  - **`AC-65-3`** — дословный текст критерия в `.pdca/status/issues-65-69-cycle-N2-plan.md` §3.
  - **`AC-REPO-1`** — дословный текст критерия в `.pdca/status/issues-65-69-cycle-N2-plan.md` §3.

## 3. Метод измерения (`Measurement method`)
- **Метрика и единица измерения:**
  1. Поимённая классификация падений полного прогона `N2` (`8 831 total / 8 583 pass / 51 fail / 197 skip`, `exit 1`) по точным идентификаторам и классам тестов и их распределение по категориям файлов рабочего дерева: `tracked` / `untracked` / `protected` / `mapping-scope`, плюс показатели `skip budget`.
  2. Число упавших тестов (`failed count`) и соблюдение `skip budget` при прогоне канонического полного интеграционного набора (`python3 tools/integration/run-integration-capped.py --full` с обязательным `DOCKER_HOST`) в чистом worktree от текущего `HEAD` (`/tmp/nextorm-ac65`).
- **Знаменатель и фильтры включения/исключения:**
  - В знаменатель изолированного прогона в `/tmp/nextorm-ac65` включаются `HEAD` и **только** файлы кандидата задачи `#65` (tracked diff по `MappingScope` и явно идентифицированные untracked файлы `MappingScope`).
  - Строго исключаются посторонние тесты/артефакты задачи `#26` (включая два untracked файла struct-проекций, зафиксированных в `.pdca/issues-65-69/rv2/attempts/1/live-integration/canonical-failure-class-counts.log` и `.pdca/issues-65-69/rv2/attempts/1/struct-projection-focused-repro.log`, при чистом `RowMaterializerBuilder.cs`) и любые иные защищённые (`protected`) изменения основного рабочего дерева.
- **Условия сопоставимости и порог:**
  - Прогон сопоставим только при подтверждённом списке путей в `/tmp/nextorm-ac65` (присутствуют исключительно пути кандидата `#65`), активном Podman/Docker сокете (`DOCKER_HOST`) и выполненной проверке `skip budget`.
  - Порог для зелёного исхода кандидата `#65`: `0` падений (`failed = 0`) в каноническом полном интеграционном прогоне и соблюдение `skip budget`, после чего временный worktree `/tmp/nextorm-ac65` удаляется с подтверждением удаления.

## 4. Терминальный исход PLAN (`Terminal outcome`)
- Не применяется: план готов (`ready`), требуется выполнение предписанной пользователем процедуры чистой атрибуции в DO и последующая независимая верификация в CHECK.

## 5. Ограничения и допущения (`Constraints / assumptions`)
- **Обязательные ограничения пользователя (сохраняются без ослабления):**
  1. Перечислить точные идентификаторы и классы упавших тестов из полного прогона `N2` (`.pdca/issues-65-69/rv2/attempts/1/live-integration/canonical/canonical-canonical-full-suite.log`, `canonical-failure-class-counts.log`, `struct-projection-focused-repro.log`), а также сводные счётчики по категориям `tracked` / `untracked` / `protected` / `mapping-scope` и `skip budget`.
  2. Создать временный чистый worktree на HEAD по пути `/tmp/nextorm-ac65`; перенести туда **только** кандидат задачи `#65` (tracked diff по `MappingScope` и явно идентифицированные untracked файлы `MappingScope`); исключить посторонние тесты/артефакты `#26` и все прочие защищённые изменения; перед прогоном верифицировать, что в worktree изменены только пути кандидата; запустить канонический ограниченный полный интеграционный прогон с проверкой skip-budget и требуемым `DOCKER_HOST`; сохранить логи в evidence root основного checkout; удалить worktree и подтвердить отсутствие.
  3. Если прогон зелёный — обновить manifest и validators и передать в CHECK; если сохраняются падения — перечислить точные классы/идентификаторы и маршрутизировать строго по фактам.
- **Запрет на переход к #69:** До независимого принятия #65 в CHECK/ACT работ по Step 2 нет.
- **Защита:** посторонние untracked/protected файлы основного дерева не удалять и не перезаписывать; изоляцию обеспечить временным worktree.
- **Сверка DML1:** статус содержит конфликт: defect history — 1 fix; поздняя progress-запись — 0 fixes. Исполнитель должен сверить первичные записи/артефакты и записать append-only коррекцию, не исправляя наугад.
- **Привязка:** текущий HEAD основного checkout и кандидат MappingScope из статуса/manifest N2; evidence N3 — `.pdca/issues-65-69/rv3/attempts/1/`.

## 6. Пробелы (`Gaps`)
1. Точная поимённая атрибуция 51 N2 failures плюс tracked/untracked/protected/mapping-scope счётчики и skip budget.
2. Полный canonical integration run на чистом worktree, содержащем только HEAD + кандидат #65.
3. При зелёном запуске: актуальный manifest и обе проверки; при повторных падениях — классификация по идентичности и дальнейшее решение по доказательствам.

## 7. Передача PLAN → DO (`Plan-to-DO handoff`)
- Исходная задача — точная ссылка в статусе §Original task; текущий фокус Step 1 (#65).
- Критерии по exact reference: AC-65-1, AC-65-2, AC-65-3, AC-REPO-1 из `.pdca/status/issues-65-69-cycle-N2-plan.md` §3, неизменны.
- DO выполняет только user-authorized clean-worktree attribution route, сохраняет все exclusion paths, собирает полный лог и skip-budget, удаляет worktree после сохранения evidence; если прогон зелёный — fresh manifest + оба validators для CHECK.
- No #69, no criteria changes, no modifications to foreign files. Commit/close only after independent CHECK acceptance.

## 8. Риски и условия остановки (`Risks and stop conditions`)
- Риск: failures повторяются на чистом candidate worktree — зафиксировать exact identities and route with evidence; не обобщать beyond the tested candidate.
- Риск: Podman/Docker unavailable — штатно устранить prerequisite; no provider opt-outs.
- Остановка: Step 1 не закрывается без чистого полного integration evidence + skip budget + independent CHECK.

## 9. Учёт ревизии и уверенность
- `N=3,r=1,n=0`; unchanged acceptance criteria, no criteria revision. N1/N2 ACT records remain closed and unchanged.
---8<--- PLAN END
