-- Расчет сквозной воронки конверсии (windowFunnel)
SELECT
    level,
    count() AS conversion_count
FROM (
    SELECT
        UserID,
        windowFunnel(1800)(
            EventTime,
            URL LIKE '%/product/%',
            URL LIKE '%/cart%',
            URL LIKE '%/checkout/success%'
        ) AS level
    FROM datasets.hits_v1
    WHERE EventDate = '2014-03-20'
    GROUP BY UserID
)
GROUP BY level
ORDER BY level ASC;