# Решение на границе шага (Step-1 Boundary Decision)

- **Решение (`Decision`):** **ПРОДОЛЖИТЬ В DO ШАГА 1 (`Proceed to Step 1 DO`)** по действующему контракту ревизии `r=1` — без дробления шага (`no split`) и без остановки (`no STOP`).
- **Счётчики (без изменения ревизии плана):** `N=1, r=1, n=1/3` (согласно сверке в статусе; по `references/plan.md` §Revision accounting отбрасывание неподлинных процедурных под-гейтов `B-2`/`B-4` не меняет контракт результата и набор критериев приёмки плана, поэтому `r` не инкрементируется).
- **Форма и оверлей (по ссылке, без переклассификации):** `incremental` + `coder+.NET` — по ссылке на `.pdca/status/issues-65-69-plan-r1.md` и `.pdca/status/issues-65-69-plan-r1-clarification.md` (`references/plan.md` §Entry and first step: *On a resume or replan a still-valid form rationale is carried by reference, not re-authored or re-classified*).

## Обоснование решения на границе

1. **Отсутствие дефекта критериев плана:** После снятия придуманных процедурных/подготовительных гейтов `B-2`/`B-4` (см. `.pdca/status/issues-65-69-plan-r1-clarification.md` и `references/plan.md` §Criteria, §Plan hygiene, §Evidence boundary) в контракте Шага 1 нет дефекта критериев. Единственные критерии приёмки Шага 1 — `AC-65-1`, `AC-65-2`, `AC-65-3` и `AC-REPO-1`; `CHECK` оценивает только критерии плана.
2. **Базовая линия (baseline) установлена до изменений:** Подготовительные свидетельства в `.pdca/issues-65-69/rv1/mapping-scope-default-oracle.log` (24/24 зелёных, `exit 0`), `.pdca/issues-65-69/rv1/baseline-oracle-record.md` и `.pdca/issues-65-69/rv1/validator-exit-status.txt` (`0` / `0`, обязательные строки манифеста `baseline-source`, `default-behavior-test`, `cache-source` выполнены на том же `HEAD 63fbc77a...`) уже фиксируют исходное состояние. По `references/plan.md` §Entry and first step и §Step boundary при установленном baseline первый шаг выполняет само изменение (Issue #65).
3. **Почему шаг не дробится (`no split`):** Baseline уже собран и валидирован как подготовительное свидетельство, а объём Шага 1 ограничен целостным результатом по задаче #65 (`AC-65-1..3` + `AC-REPO-1`). Выделять отдельный под-шаг для baseline или дробить задачу #65 не требуется.
4. **Почему нет `STOP`:** Продуктовые правки и автокоммит по Шагу 1 (#65) ещё не выполнены (`No product edits/commit yet`), поэтому целевой результат ещё не достигнут.

## Ссылки на бриф и критерии для передачи в DO Шага 1 (Plan-to-DO handoff by reference)

- **Исходное задание (verbatim):** «pdca по задачам 65 и 69 автономно с автокомитом»
- **Запрошенный и поставляемый результат (`Requested / Delivered result`):**
  - В рамках всей кампании — закрытые задачи #65 и #69 (milestone `1.0.10`) с автокоммитом принятых шагов;
  - В рамках текущего **Шага 1** — реализованное и проверенное решение задачи **#65** в соответствии с контрактом `.pdca/status/issues-65-69-plan-r1.md` и уточнением `.pdca/status/issues-65-69-plan-r1-clarification.md`.
- **Нормативные ссылки плана для DO и CHECK:**
  - Основной план: `.pdca/status/issues-65-69-plan-r1.md`
  - Уточнение контракта (снятие `B-2`/`B-4`, фиксация рамок критериев): `.pdca/status/issues-65-69-plan-r1-clarification.md`
  - Свидетельства baseline (`rv1`, `HEAD 63fbc77a...`):
    - `.pdca/issues-65-69/rv1/mapping-scope-default-oracle.log`
    - `.pdca/issues-65-69/rv1/baseline-oracle-record.md`
    - `.pdca/issues-65-69/rv1/validator-exit-status.txt`
- **Действующие критерии приёмки Шага 1 (по ссылке на `.pdca/status/issues-65-69-plan-r1.md` и `.pdca/status/issues-65-69-plan-r1-clarification.md`):**
  - `AC-65-1`
  - `AC-65-2`
  - `AC-65-3`
  - `AC-REPO-1`
  *(Никакие внеплановые под-гейты `B-2`/`B-4` не входят в набор приёмки и не могут использоваться `CHECK` для отклонения шага.)*
- **Ограничения, запреты и гигиена исполнения:**
  - Якорь снимка: `HEAD 63fbc77a...`, milestone `1.0.10`.
  - Несвязанные грязные/неотслеживаемые пути рабочего дерева защищены: не трогать, не откатывать и не включать в индекс/коммит.
  - Автокоммит разрешён **только** после независимого `CHECK = PASS` по критериям `AC-65-1..3` и `AC-REPO-1` на принятии инкремента Шага 1; `git push` не выполнять.
  - Декомпозиция на единицы выполнения и выбор конкретных команд проверки остаются за `DO` и `CHECK` соответственно (`references/plan.md` §Plan hygiene, §Evidence boundary).
  - Кандидаты в процессные дефекты `R65-69-PD1` и `R65-69-PD2` учитываются по конвенции репозитория (`docs/specs/design/` и `#### Defect history` в статусе кампании) и не изменяют продуктовые критерии Шага 1.
