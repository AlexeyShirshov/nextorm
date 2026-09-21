-- Матрица заполняемости бизнес-класса по дням недели
WITH flight_business_capacity AS (
    SELECT 
        airplane_code,
        COUNT(*) AS total_business_seats
    FROM seats
    WHERE fare_conditions = 'Business'
    GROUP BY airplane_code
),
flight_occupancy AS (
    SELECT 
        f.flight_id,
        f.airplane_code,
        EXTRACT(ISODOW FROM f.scheduled_departure) AS day_of_week,
        COUNT(bp.seat_no) AS occupied_business_seats
    FROM timetable f
    JOIN segments tf ON f.flight_id = tf.flight_id AND tf.fare_conditions = 'Business'
    LEFT JOIN boarding_passes bp ON tf.flight_id = bp.flight_id AND tf.ticket_no = bp.ticket_no
    WHERE f.status IN ('Departed', 'Arrived')
    GROUP BY f.flight_id, f.airplane_code, f.scheduled_departure
)
SELECT 
    ac.model ->> 'ru' AS "Модель самолета",
    ROUND(AVG(CASE WHEN fo.day_of_week = 1 THEN (fo.occupied_business_seats::numeric / NULLIF(cap.total_business_seats, 0)) * 100 END), 1) AS "Пн %",
    ROUND(AVG(CASE WHEN fo.day_of_week = 2 THEN (fo.occupied_business_seats::numeric / NULLIF(cap.total_business_seats, 0)) * 100 END), 1) AS "Вт %",
    ROUND(AVG(CASE WHEN fo.day_of_week = 3 THEN (fo.occupied_business_seats::numeric / NULLIF(cap.total_business_seats, 0)) * 100 END), 1) AS "Ср %",
    ROUND(AVG(CASE WHEN fo.day_of_week = 4 THEN (fo.occupied_business_seats::numeric / NULLIF(cap.total_business_seats, 0)) * 100 END), 1) AS "Чт %",
    ROUND(AVG(CASE WHEN fo.day_of_week = 5 THEN (fo.occupied_business_seats::numeric / NULLIF(cap.total_business_seats, 0)) * 100 END), 1) AS "Пт %",
    ROUND(AVG(CASE WHEN fo.day_of_week = 6 THEN (fo.occupied_business_seats::numeric / NULLIF(cap.total_business_seats, 0)) * 100 END), 1) AS "Сб %",
    ROUND(AVG(CASE WHEN fo.day_of_week = 7 THEN (fo.occupied_business_seats::numeric / NULLIF(cap.total_business_seats, 0)) * 100 END), 1) AS "Вс %"
FROM flight_occupancy fo
JOIN flight_business_capacity cap ON fo.airplane_code = cap.airplane_code
JOIN airplanes_data ac ON fo.airplane_code = ac.airplane_code
GROUP BY ac.model ->> 'ru'
ORDER BY "Модель самолета";
