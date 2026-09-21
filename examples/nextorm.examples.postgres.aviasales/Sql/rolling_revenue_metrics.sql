-- Расчет скользящей средней загрузки и кумулятивного дохода
WITH daily_revenue AS (
    SELECT 
        DATE_TRUNC('day', book_date)::date AS sales_date,
        SUM(total_amount) AS daily_amount
    FROM bookings
    GROUP BY DATE_TRUNC('day', book_date)::date
)
SELECT 
    sales_date AS "Дата",
    daily_amount AS "Выручка за день",
    SUM(daily_amount) OVER (
        ORDER BY sales_date 
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS "Кумулятивная выручка",
    ROUND(AVG(daily_amount) OVER (
        ORDER BY sales_date 
        ROWS BETWEEN 6 PRECEDING AND CURRENT ROW
    ), 2) AS "7-дневная скользящая средняя"
FROM daily_revenue
ORDER BY sales_date DESC;