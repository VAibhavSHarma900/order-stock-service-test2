namespace Product.Api.Entities;

public sealed record Product(Guid ProductId, string Name, int StockQty, decimal Price,
    bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);
