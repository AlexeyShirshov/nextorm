-- Поиск «цепочек задержек» авиарейсов
WITH flight_delays AS (
    SELECT 
        flight_id,
        airplane_code,
        actual_departure,
        scheduled_departure,
        EXTRACT(EPOCH FROM (actual_departure - scheduled_departure)) / 60 AS delay_minutes,
        LAG(actual_departure) OVER(
            PARTITION BY airplane_code 
            ORDER BY actual_departure
        ) AS prev_departure,
        LAG(EXTRACT(EPOCH FROM (actual_departure - scheduled_departure)) / 60) OVER(
            PARTITION BY airplane_code 
            ORDER BY actual_departure
        ) AS prev_delay_minutes
    FROM timetable
    WHERE status IN ('Departed', 'Arrived') 
      AND actual_departure IS NOT NULL
),
delay_chains AS (
    SELECT 
        *,
        CASE WHEN delay_minutes > 15 AND prev_delay_minutes > 15 THEN 1 ELSE 0 END AS is_chain_link
    FROM flight_delays
)
SELECT 
    ac.model ->> 'ru' AS "Модель самолета",
    dc.airplane_code AS "Бортовой код",
    dc.scheduled_departure AS "Плановый вылет",
    ROUND(dc.delay_minutes::numeric, 1) AS "Задержка (мин)",
    ROUND(dc.prev_delay_minutes::numeric, 1) AS "Задержка пред. рейса (мин)"
FROM delay_chains dc
JOIN airplanes_data ac ON dc.airplane_code = ac.airplane_code
WHERE dc.delay_minutes > 30 
  AND dc.prev_delay_minutes > 30
ORDER BY dc.airplane_code, dc.scheduled_departure;
