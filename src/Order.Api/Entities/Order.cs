namespace Order.Api.Entities;

public sealed record Order(Guid OrderId, Guid ProductId, int Quantity, string OrderStatus, DateTime CreatedAt);
