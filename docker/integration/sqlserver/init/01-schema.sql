if db_id(N'DataDeveloperIntegration') is null
begin
    create database [DataDeveloperIntegration];
end
go

use [DataDeveloperIntegration];
go

if object_id(N'dbo.open_orders', N'V') is not null drop view dbo.open_orders;
go
if object_id(N'dbo.mark_order_shipped', N'P') is not null drop procedure dbo.mark_order_shipped;
go
if object_id(N'dbo.get_customer_total', N'FN') is not null drop function dbo.get_customer_total;
go
if object_id(N'dbo.orders', N'U') is not null drop table dbo.orders;
go
if object_id(N'dbo.customers', N'U') is not null drop table dbo.customers;
go

create table dbo.customers
(
    customer_id int identity(1,1) primary key,
    name nvarchar(100) not null,
    email nvarchar(150) null,
    created_at datetime2(6) not null constraint df_customers_created_at default sysutcdatetime()
);
go

create table dbo.orders
(
    order_id int identity(1,1) primary key,
    customer_id int not null,
    order_total decimal(10,2) not null,
    status nvarchar(30) not null constraint df_orders_status default N'OPEN',
    created_at datetime2(6) not null constraint df_orders_created_at default sysutcdatetime(),
    constraint fk_orders_customers foreign key (customer_id) references dbo.customers(customer_id)
);
go

insert into dbo.customers (name, email)
values
    (N'Alice Johnson', N'alice@example.com'),
    (N'Bob Smith', N'bob@example.com');
go

insert into dbo.orders (customer_id, order_total, status)
values
    (1, 149.90, N'OPEN'),
    (2, 79.50, N'SHIPPED');
go

create view dbo.open_orders
as
select
    o.order_id,
    c.name as customer_name,
    o.order_total,
    o.status,
    o.created_at
from dbo.orders o
join dbo.customers c on c.customer_id = o.customer_id
where o.status = N'OPEN';
go

create procedure dbo.mark_order_shipped
    @p_order_id int
as
begin
    set nocount on;

    update dbo.orders
    set status = N'SHIPPED'
    where order_id = @p_order_id;
end
go

create function dbo.get_customer_total
(
    @p_customer_id int
)
returns decimal(10,2)
as
begin
    declare @total decimal(10,2);

    select @total = coalesce(sum(order_total), 0)
    from dbo.orders
    where customer_id = @p_customer_id;

    return @total;
end
go
