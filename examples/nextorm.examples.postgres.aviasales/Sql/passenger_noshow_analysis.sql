-- Поиск «серийных невозвращенцев» и скрытых потерь
WITH passenger_flight_history AS (
    SELECT 
        t.passenger_id,
        t.passenger_name,
        t.ticket_no,
        f.flight_id,
        tf.price,
        CASE WHEN bp.seat_no IS NULL THEN 1 ELSE 0 END AS is_noshow
    FROM tickets t
    JOIN segments tf ON t.ticket_no = tf.ticket_no
    JOIN flights f ON tf.flight_id = f.flight_id
    LEFT JOIN boarding_passes bp ON f.flight_id = bp.flight_id 
                                AND tf.ticket_no = bp.ticket_no
    WHERE f.status IN ('Departed', 'Arrived')
),
passenger_stats AS (
    SELECT 
        passenger_id,
        passenger_name,
        COUNT(flight_id) AS total_booked_flights,
        SUM(is_noshow) AS total_noshows,
        SUM(price) AS wasted_money
    FROM passenger_flight_history
    GROUP BY passenger_id, passenger_name
)
SELECT 
    passenger_id AS "ID Пассажира",
    passenger_name AS "ФИО Пассажира",
    total_booked_flights AS "Забронировано рейсов",
    wasted_money AS "Сумма «сгоревших» билетов (руб.)"
FROM passenger_stats
WHERE total_booked_flights >= 3 
  AND total_booked_flights = total_noshows
ORDER BY wasted_money DESC, passenger_name;
