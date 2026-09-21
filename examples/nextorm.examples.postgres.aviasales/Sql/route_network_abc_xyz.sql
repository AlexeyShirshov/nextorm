-- ABC/XYZ-анализ маршрутной сети авиакомпании
WITH route_monthly_revenue AS (
    SELECT 
        (dep.city ->> 'ru') || ' -> ' || (arr.city ->> 'ru') AS route,
        DATE_TRUNC('month', f.scheduled_departure)::date AS flight_month,
        SUM(tf.price) AS monthly_revenue,
        COUNT(DISTINCT t.ticket_no) AS passenger_count
    FROM timetable f
    JOIN airports_data dep ON f.departure_airport = dep.airport_code
    JOIN airports_data arr ON f.arrival_airport = arr.airport_code
    JOIN segments tf ON f.flight_id = tf.flight_id
    JOIN tickets t ON tf.ticket_no = t.ticket_no
    GROUP BY dep.city ->> 'ru', arr.city ->> 'ru', DATE_TRUNC('month', f.scheduled_departure)::date
),
route_aggregates AS (
    SELECT 
        route,
        SUM(monthly_revenue) AS total_revenue,
        AVG(passenger_count) AS avg_passengers,
        STDDEV(passenger_count) AS stddev_passengers
    FROM route_monthly_revenue
    GROUP BY route
),
abc_analys AS (
    SELECT 
        route,
        total_revenue,
        avg_passengers,
        stddev_passengers,
        SUM(total_revenue) OVER(ORDER BY total_revenue DESC) / SUM(total_revenue) OVER() AS running_percent
    FROM route_aggregates
)
SELECT 
    route AS "Маршрут",
    ROUND(total_revenue / 1000000.0, 2) AS "Общая выручка (млн руб)",
    CASE 
        WHEN running_percent <= 0.80 THEN 'A'
        WHEN running_percent <= 0.95 THEN 'B'
        ELSE 'C'
    END AS "Класс Выручки (ABC)",
    CASE 
        WHEN avg_passengers = 0 OR stddev_passengers IS NULL THEN 'Z'
        WHEN (stddev_passengers / avg_passengers) < 0.10 THEN 'X'
        WHEN (stddev_passengers / avg_passengers) BETWEEN 0.10 AND 0.25 THEN 'Y'
        ELSE 'Z'
    END AS "Класс Стабильности (XYZ)"
FROM abc_analys
ORDER BY total_revenue DESC;
