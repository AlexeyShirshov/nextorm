-- Инкрементальный расчет через комбинаторы агрегатных функций (-Merge)
SELECT
    EventDate AS "Дата",
    uniqMerge(users_state) AS "Точное кол-во уникальных посетителей"
FROM datasets.daily_unique_users_mv
WHERE EventDate >= '2014-03-01' AND EventDate <= '2014-03-31'
GROUP BY EventDate
ORDER BY EventDate DESC;