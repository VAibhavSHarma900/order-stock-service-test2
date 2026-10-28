-- psql bootstrap for fresh databases; requires permission to create databases.
CREATE DATABASE product_db;
\connect product_db
\ir product-schema.sql
CREATE DATABASE order_db;
\connect order_db
\ir order-schema.sql
