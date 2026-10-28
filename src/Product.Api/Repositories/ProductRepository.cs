using System.Data;
using Npgsql;
using Product.Api.DTOs;
using ProductEntity = Product.Api.Entities.Product;

namespace Product.Api.Repositories;

public sealed class ProductRepository(NpgsqlDataSource source)
{
    private const string Columns = "product_id, product_name, stock_qty, price, is_active, created_at, updated_at";

    public async Task<ProductEntity> CreateAsync(ProductRequest request, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand($"""
            INSERT INTO products (product_name, stock_qty, price) VALUES (@name, @stock, @price)
            RETURNING {Columns}
            """);
        AddValues(cmd, request);
        return (await ReadAsync(cmd, ct))!;
    }

    public async Task<ProductEntity?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand($"SELECT {Columns} FROM products WHERE product_id = @id AND is_active");
        cmd.Parameters.AddWithValue("id", id);
        return await ReadAsync(cmd, ct);
    }

    public async Task<(IReadOnlyList<ProductEntity> Items, long Total)> ListAsync(PageQuery page, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using var count = new NpgsqlCommand("SELECT COUNT(*) FROM products WHERE is_active", connection, transaction);
        var total = (long)(await count.ExecuteScalarAsync(ct))!;
        await using var cmd = new NpgsqlCommand($"""
            SELECT {Columns} FROM products WHERE is_active ORDER BY created_at DESC, product_id DESC
            LIMIT @limit OFFSET @offset
            """, connection, transaction);
        cmd.Parameters.AddWithValue("limit", page.PageSize);
        cmd.Parameters.AddWithValue("offset", ((long)page.PageNumber - 1) * page.PageSize);
        var items = new List<ProductEntity>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) items.Add(Map(reader));
        await transaction.CommitAsync(ct);
        return (items, total);
    }

    public async Task<ProductEntity?> UpdateAsync(Guid id, ProductRequest request, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand($"""
            UPDATE products SET product_name = @name, stock_qty = @stock, price = @price, updated_at = CURRENT_TIMESTAMP
            WHERE product_id = @id AND is_active RETURNING {Columns}
            """);
        cmd.Parameters.AddWithValue("id", id);
        AddValues(cmd, request);
        return await ReadAsync(cmd, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            UPDATE products SET is_active = FALSE, updated_at = CURRENT_TIMESTAMP
            WHERE product_id = @id AND is_active
            """);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<StockResponse?> ReduceStockAsync(Guid id, int quantity, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            UPDATE products SET stock_qty = stock_qty - @quantity, updated_at = CURRENT_TIMESTAMP
            WHERE product_id = @id AND is_active AND stock_qty >= @quantity
            RETURNING product_id, stock_qty
            """);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("quantity", quantity);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetInt32(1)) : null;
    }

    private static void AddValues(NpgsqlCommand cmd, ProductRequest r)
    {
        cmd.Parameters.AddWithValue("name", r.Name.Trim());
        cmd.Parameters.AddWithValue("stock", r.StockQty!.Value);
        cmd.Parameters.AddWithValue("price", r.Price!.Value);
    }

    private static async Task<ProductEntity?> ReadAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    private static ProductEntity Map(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1),
        r.GetInt32(2), r.GetDecimal(3), r.GetBoolean(4), r.GetDateTime(5), r.IsDBNull(6) ? null : r.GetDateTime(6));
}
