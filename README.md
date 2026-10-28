# Product and Order Microservices

Two .NET 8 APIs with separate PostgreSQL databases. Product Service owns products and stock; Order Service creates single-product orders by calling Product Service over HTTP. Each service contains Controllers, Services, Repositories, DTOs, and Entities. Persistence uses parameterized SQL through Npgsql.

## Setup

Requirements: .NET 8 SDK and runtime, PostgreSQL 16+, and `psql`. Run commands from the repository root.

Create the databases and tables:

```sh
psql -h localhost -U postgres -d postgres -v ON_ERROR_STOP=1 -f database/setup.sql
```

`product_db` and `order_db` must not already exist. For existing empty databases, apply the corresponding schema script separately. The bootstrap uses psql commands and requires database-creation privileges.

Start Product Service in PowerShell:

```powershell
$env:ConnectionStrings__Products = 'Host=localhost;Port=5432;Database=product_db;Username=postgres;Password=YOUR_PASSWORD'
dotnet run --project src/Product.Api
```

In another terminal, start Order Service:

```powershell
$env:ConnectionStrings__Orders = 'Host=localhost;Port=5432;Database=order_db;Username=postgres;Password=YOUR_PASSWORD'
$env:ProductService__BaseUrl = 'http://localhost:8082/'
dotnet run --project src/Order.Api
```

Use your actual connection details and keep passwords out of Git. For Bash, set these variables with `export` instead. Product Service runs on port **8082** and Order Service on **8083**. The Product API base URL should end with `/`.

Build both services with `dotnet build OrderStock.sln -c Release`.

## Endpoints

| Service | Method | Path | Success |
|---|---|---|---|
| Product | POST | `/api/products` | 201 with created product and Location |
| Product | GET | `/api/products/{id}` | 200 with product |
| Product | GET | `/api/products` | 200 with paginated products |
| Product | PUT | `/api/products/{id}` | 200 with updated product |
| Product | DELETE | `/api/products/{id}` | 204 after soft deletion |
| Product | POST | `/api/products/{id}/reduce-stock` | 200 with remaining stock |
| Order | POST | `/api/orders` | 201 with created order |
| Order | GET | `/api/orders` | 200 with paginated orders |

Product create/update body:

```json
{"name":"Keyboard","price":1499.50,"stockQty":10}
```

Order create body, using a product ID returned by Product Service:

```json
{"productId":"REPLACE_WITH_PRODUCT_UUID","quantity":2}
```

Stock-deduction body:

```json
{"quantity":2}
```

Name is required, nonblank, and limited to 150 characters. Stock must be a nonnegative integer. Price must be between 0 and 99,999,999.99 with at most two meaningful decimal places. Order and deduction quantities must be positive integers. PUT replaces all editable product fields; deleted products are excluded from reads and cannot be ordered. Orders use the supplied `CREATED` status.

Both list endpoints accept `pageNumber` (default 1, minimum 1) and `pageSize` (default 20, maximum 100). Responses contain `items`, `pageNumber`, `pageSize`, and `totalCount`, ordered newest first with ID as a tie-breaker. Pages beyond the end return empty items.

## Order processing

Order Service checks the product through HTTP, inserts a provisional order inside a local transaction, and requests stock deduction. Product Service atomically checks and subtracts stock in one conditional SQL UPDATE. Order Service commits only after deduction succeeds. A confirmed rejection rolls back the provisional order. Order Service never accesses Product database directly.

**Consistency limitation:** the two databases do not share a distributed transaction. A lost deduction response or subsequent order-commit failure can leave an uncertain outcome. These cases are logged for manual reconciliation. Do not blindly retry: there are no idempotency keys, automatic compensation, or deduction retries. Each inter-service HTTP call has a five-second timeout. Product PUT uses last-write-wins absolute stock replacement.

## Errors and logs

Global exception handling returns Problem Details: **400** for invalid input, **404** for missing/inactive products, **409** for insufficient stock, **503** for Product Service dependency failures, and sanitized **500** responses for unexpected errors. Responses and file logs include correlation IDs; Order Service propagates `X-Correlation-ID` to Product Service.

Serilog writes separate files beneath each service's content-root `logs/` directory. Files roll daily and at 10 MB, retaining seven files per service. Credentials and raw request bodies are not logged; logs and build outputs are ignored by Git.

Use Postman to create a product, place an order, and confirm reduced stock. Also check insufficient stock, soft deletion, pagination, and unavailable Product Service. There is no authentication; run this assessment locally. Database timestamps retain the supplied timezone-unspecified type.
