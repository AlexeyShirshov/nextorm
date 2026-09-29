# 05 — Этапы (прогон №2)

Хронология по локальному времени (WSL), 28–29 сентября 2026.

## 28.09 — санитизация базы

| время | этап | результат |
|---|---|---|
| 21:44 | Проверка контаминации базы `375a841` (= дерево `b6baa0c`) | найдены спойлеры: RFC `todo_dynamic_columns.md` §4/§6 (рецепт upstream + список файлов), `API-NAMING-REVIEW.md:5161` и `linq2db-backlog-gap-analysis.md:387/423/481` («read side shipped / write side deferred») |
| 21:45 | RFC урезан до требований (убран §4-рецепт `IsDynamicColumnsStore`/`RowMaterializerBuilder`/`*Builder` и §6-список файлов), сняты строки-спойлеры | изменены только доки: `src/`+`tests/` идентичны `375a841` |
| 21:45 | Новый base-коммит в clean-room **`8366dd6`** | `env.cleanroom.sh BASE` обновлён |
| 21:45 | Старые ветки сохранены тегами `r1/exp-{a1-bare,a2-skill,a3-pdca}`; старые ворктри удалены | прогон №1 не потерян |
| 21:45 | `setup.sh` пересоздал ворктри `exp-*` от `8366dd6` | три чистых ворктри |

## 28.09 — арбитр и выдача

| шаг | что |
|---|---|
| single-DS арбитр | `eval/profile.jsonc`: `judge`/`auditor` — один primary на `deepseek/deepseek-flash`, тулы `read/grep/glob/list/bash`, `task:false`; сабагенты `nextorm-scout`/`nextorm-brains` (Sol) удалены; vendor-каталог two-tier больше не используется |
| промпты | `eval/prompts/{judge,audit}.md` переписаны как self-contained: судья сам берёт `git diff <BASE>`, читает файлы, выдаёт JSON |
| harness | `eval/run-eval.sh`: убран `BRAINS_MODEL`, добавлен `NO_PORT=1` (пропуск порта эталонных тестов); `eval/report.py`: заголовки single-DS |
| smoke | одноразовый прогон judge на дереве a1 → `OK-SMOKE` (транспорт/модель живы) |
| архив | старая выдача → `exp94/archive-r1/{logs,report,out}`; создана папка отчётов `docs/specs/experiments/94-skill-comparison-2/` с копией доксета и r1-артефактами (`artifacts/run1-*`) |

## 28–29.09 — прогоны рук

| время | рука | итог |
|---|---|---|
| 21:47–22:08 | a1 | rc=0, 20.9 мин, 30 файлов (+428/−25), untracked 7 |
| 22:08–22:50 | a2 | rc=0, 41.9 мин, 33 файла (+722/−107), untracked 10 |
| 22:50–09:27 | a3 | rc=0, активных **78.6 мин**, 37 файлов (+2530/−35); **хост спал 00:05→09:23** (разрыв 557.9 мин), поэтому стенное время 10.6 ч |
| — | `compare.py` | патчи `report/{a1,a2,a3}.patch` + `summary.tsv`; wall a3 исправлен на 4716 с |

## 29.09 — оценка (single-DS)

| время | этап | результат |
|---|---|---|
| 09:32 | первый запуск `run-eval-all.sh` | **сбой**: патчи рук не прикладываются — `error: patch does not apply` на всех файлах; a1–a3 `rc=3`, стартовал только upstream |
| — | диагностика | хэши blob'ов базы совпадают с патчем → дело в line endings; в clean-room **не выставлен `core.autocrlf=true`** (в оригинальном репо он есть), а файлы в рабочем дереве CRLF |
| — | починка | `git -C nextorm-cleanroom config core.autocrlf true`; `git apply --check` для a1/a2/a3 → OK |
| 09:36–10:08 | перезапуск `run-eval-all.sh` (`NO_PORT=1`) | все 4 цели: L2 `rc=0`, L3 `rc=0`, свои L1-тесты зелёные; порт пропущен |
| 10:09 | `report.py` + `collect-reports.sh` | `eval/report.md`, `run2-*` в `artifacts/` |

Итог рубрики: **upstream 89 > a2 80 > a1 75 > a3 70**; L3 P0/P1/P2 — a1 1/2/4, a2 0/2/2, a3 0/2/7,
upstream 0/3/4.

## 29.09 — грабли (для повторения)

1. **`core.autocrlf`** — clean-room-репозиторий создан без него; патчи на CRLF-дерево не ложатся.
   Fix: `git config core.autocrlf true` в репозитории эксперимента (до генерации/применения патчей).
2. **Сон хоста** искажает wall-clock (a3: 38223 с стенных против 4716 с активных) — метрику надо
   считать по событиям, исключая большие разрывы.
3. **`setsid … &` и `sleep` в одном bash-вызове** держат пайп, команда «висит» до таймаута; запуск
   и ожидание лучше разделять.
4. `pkill -f 'run-eval-all'` ловит **собственную** командную строку проверяющего процесса → ложное
   «already running».
