-- Когортный анализ удержания (Retention) через массивы.
-- Примечание: исходная форма читала UserID во внешнем SELECT, хотя внутренний подзапрос его не
-- проецирует, и делила на размер когорты-недели вместо размера когорты. Ниже исправленная версия:
-- размер когорты берётся из отдельной агрегации first_visits.
WITH
    first_visits AS (
        SELECT UserID, toMonday(min(EventDate)) AS cohort_week
        FROM datasets.hits_v1
        GROUP BY UserID
    ),
    cohort_sizes AS (
        SELECT cohort_week, count() AS cohort_size
        FROM first_visits
        GROUP BY cohort_week
    )
SELECT
    t.cohort_week AS "Неделя когорты",
    toInt64(any(cs.cohort_size)) AS "Размер когорты",
    toString(groupArray((t.week_number, round(t.distinct_users / cs.cohort_size * 100, 2)))) AS "Матрица удержания"
FROM (
    SELECT
        fv.cohort_week AS cohort_week,
        toUInt8((toMonday(h.EventDate) - fv.cohort_week) / 7) AS week_number,
        count(DISTINCT h.UserID) AS distinct_users
    FROM datasets.hits_v1 h
    JOIN first_visits fv ON h.UserID = fv.UserID
    WHERE week_number <= 4
    GROUP BY fv.cohort_week, week_number
) t
JOIN cohort_sizes cs ON t.cohort_week = cs.cohort_week
GROUP BY t.cohort_week
ORDER BY t.cohort_week DESC;
