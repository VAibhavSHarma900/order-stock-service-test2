CREATE TABLE orders (
    order_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id UUID NOT NULL,
    quantity INT NOT NULL CHECK (quantity > 0),
    order_status VARCHAR(30) NOT NULL DEFAULT 'CREATED',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
ALTER TABLE orders ADD CONSTRAINT chk_order_status
    CHECK (order_status IN ('CREATED', 'PAID', 'CANCELLED'));
CREATE INDEX idx_orders_product_id ON orders(product_id);
