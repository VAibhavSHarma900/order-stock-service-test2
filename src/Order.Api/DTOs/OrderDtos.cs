using System.ComponentModel.DataAnnotations;
using OrderEntity = Order.Api.Entities.Order;

namespace Order.Api.DTOs;

public sealed class OrderRequest : IValidatableObject
{
    [Required] public Guid? ProductId { get; init; }
    [Required, Range(1, int.MaxValue)] public int? Quantity { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ProductId == Guid.Empty)
            yield return new ValidationResult("ProductId cannot be an empty UUID.", [nameof(ProductId)]);
    }
}

public sealed record OrderResponse(Guid OrderId, Guid ProductId, int Quantity, string OrderStatus, DateTime CreatedAt)
{
    public static OrderResponse From(OrderEntity o) => new(o.OrderId, o.ProductId, o.Quantity, o.OrderStatus, o.CreatedAt);
}

public sealed record ProductSnapshot(Guid ProductId, int StockQty);
