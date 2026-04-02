drop view if exists open_orders;
drop procedure if exists mark_order_shipped;
drop function if exists get_customer_total;
drop table if exists orders;
drop table if exists customers;

create table customers
(
    customer_id int auto_increment primary key,
    name varchar(100) not null,
    email varchar(150),
    created_at datetime(6) not null default current_timestamp(6)
);

create table orders
(
    order_id int auto_increment primary key,
    customer_id int not null,
    order_total decimal(10,2) not null,
    status varchar(30) not null default 'OPEN',
    created_at datetime(6) not null default current_timestamp(6),
    constraint fk_orders_customers foreign key (customer_id) references customers(customer_id)
);

insert into customers (name, email)
values
    ('Alice Johnson', 'alice@example.com'),
    ('Bob Smith', 'bob@example.com');

insert into orders (customer_id, order_total, status)
values
    (1, 149.90, 'OPEN'),
    (2, 79.50, 'SHIPPED');

create view open_orders as
select
    o.order_id,
    c.name as customer_name,
    o.order_total,
    o.status,
    o.created_at
from orders o
join customers c on c.customer_id = o.customer_id
where o.status = 'OPEN';

delimiter $$

create procedure mark_order_shipped
(
    in p_order_id int
)
begin
    update orders
    set status = 'SHIPPED'
    where order_id = p_order_id;
end$$

create function get_customer_total
(
    p_customer_id int
)
returns decimal(10,2)
deterministic
begin
    declare v_total decimal(10,2);

    select coalesce(sum(order_total), 0)
    into v_total
    from orders
    where customer_id = p_customer_id;

    return v_total;
end$$

delimiter ;
