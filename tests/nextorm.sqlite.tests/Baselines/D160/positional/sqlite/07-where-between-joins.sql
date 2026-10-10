select t2.id from (select id from simple_entity
 where (id > 0)) as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id