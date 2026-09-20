-- Анализ структуры трафика по подстрокам (Работа с массивами)
SELECT
    arrayFilter(x -> length(x) > 3, arrayMap(x -> lower(x), splitByChar(' ', SearchPhrase))) AS clean_words,
    count() AS occurrence
FROM datasets.hits_v1
WHERE SearchPhrase != '' 
  AND EventDate = '2014-03-20'
GROUP BY clean_words
ORDER BY occurrence DESC
LIMIT 10;