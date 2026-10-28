using System.ComponentModel.DataAnnotations;
using ProductEntity = Product.Api.Entities.Product;

namespace Product.Api.DTOs;

public sealed class ProductRequest : IValidatableObject
{
    [Required, StringLength(150)] public string Name { get; init; } = "";
    [Required, Range(0, int.MaxValue)] public int? StockQty { get; init; }
    [Required, Range(typeof(decimal), "0", "99999999.99")] public decimal? Price { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Price is { } price && decimal.Round(price, 2) != price)
            yield return new ValidationResult("Price must have at most two decimal places.", [nameof(Price)]);
    }
}

public sealed class StockRequest
{
    [Required, Range(1, int.MaxValue)] public int? Quantity { get; init; }
}

public sealed record StockResponse(Guid ProductId, int StockQty);
public sealed record ProductResponse(Guid ProductId, string Name, int StockQty, decimal Price,
    bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt)
{
    public static ProductResponse From(ProductEntity p) => new(p.ProductId, p.Name, p.StockQty, p.Price,
        p.IsActive, p.CreatedAt, p.UpdatedAt);
}
