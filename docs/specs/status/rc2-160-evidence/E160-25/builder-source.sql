select t2.Id from (select Id, BuyerId, ApproverId from orders) as 't1' join person as 't2' on t1.BuyerId = t2.Id
