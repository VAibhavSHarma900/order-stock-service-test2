using System.Data;
using Npgsql;
using Order.Api.DTOs;
using OrderEntity = Order.Api.Entities.Order;

namespace Order.Api.Repositories;

public sealed class OrderRepository(NpgsqlDataSource source)
{
    private const string Columns = "order_id, product_id, quantity, order_status, created_at";

    public async Task<OrderWrite> BeginOrderAsync(Guid productId, int quantity, CancellationToken ct)
    {
        var connection = await source.OpenConnectionAsync(ct);
        NpgsqlTransaction? transaction = null;
        try
        {
            transaction = await connection.BeginTransactionAsync(ct);
            await using var cmd = new NpgsqlCommand($"""
                INSERT INTO orders (product_id, quantity) VALUES (@product, @quantity) RETURNING {Columns}
                """, connection, transaction);
            cmd.Parameters.AddWithValue("product", productId);
            cmd.Parameters.AddWithValue("quantity", quantity);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            return new OrderWrite(connection, transaction, Map(reader));
        }
        catch
        {
            if (transaction is not null) await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<(IReadOnlyList<OrderEntity> Items, long Total)> ListAsync(PageQuery page, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using var count = new NpgsqlCommand("SELECT COUNT(*) FROM orders", connection, transaction);
        var total = (long)(await count.ExecuteScalarAsync(ct))!;
        await using var cmd = new NpgsqlCommand($"""
            SELECT {Columns} FROM orders ORDER BY created_at DESC, order_id DESC LIMIT @limit OFFSET @offset
            """, connection, transaction);
        cmd.Parameters.AddWithValue("limit", page.PageSize);
        cmd.Parameters.AddWithValue("offset", ((long)page.PageNumber - 1) * page.PageSize);
        var items = new List<OrderEntity>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) items.Add(Map(reader));
        await transaction.CommitAsync(ct);
        return (items, total);
    }

    private static OrderEntity Map(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetDateTime(4));
}

// Owns one local order transaction. Disposal rolls back any uncommitted insert.
public sealed class OrderWrite(NpgsqlConnection connection, NpgsqlTransaction transaction, OrderEntity order) : IAsyncDisposable
{
    public OrderEntity Order { get; } = order;
    public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);

    public async ValueTask DisposeAsync()
    {
        try { await transaction.DisposeAsync(); }
        finally { await connection.DisposeAsync(); }
    }
}
