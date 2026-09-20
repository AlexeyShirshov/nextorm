-- Поиск сессионных аномалий (Оконные функции)
WITH sessions AS (
    SELECT
        UserID,
        EventTime,
        EventTime - lagInFrame(EventTime) OVER (PARTITION BY UserID ORDER BY EventTime) AS time_diff
    FROM datasets.hits_v1
),
session_flags AS (
    SELECT
        UserID,
        EventTime,
        runningAccumulate(if(time_diff IS NULL OR time_diff > 1800, 1, 0)) OVER (PARTITION BY UserID ORDER BY EventTime) AS session_id
    FROM sessions
),
session_counts AS (
    SELECT
        UserID,
        session_id,
        count() AS hits_in_session
    FROM session_flags
    GROUP BY UserID, session_id
)
SELECT 
    UserID,
    session_id AS "ID Сессии",
    hits_in_session AS "Кол-во кликов"
FROM session_counts
WHERE hits_in_session > (SELECT quantile(0.99)(hits_in_session) FROM session_counts)
ORDER BY hits_in_session DESC
LIMIT 100;