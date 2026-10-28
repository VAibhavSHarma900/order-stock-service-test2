using Order.Api.DTOs;
using Order.Api.Infrastructure;
using Order.Api.Repositories;

namespace Order.Api.Services;

public sealed class OrderService(OrderRepository repository, ProductClient products, ILogger<OrderService> logger)
{
    public async Task<OrderResponse> CreateAsync(OrderRequest request, string correlationId, CancellationToken ct)
    {
        var productId = request.ProductId!.Value;
        var quantity = request.Quantity!.Value;
        var product = await products.GetAsync(productId, correlationId, ct);
        if (quantity > product.StockQty)
        {
            logger.LogWarning("Order rejected: insufficient stock for product {ProductId}", productId);
            throw new ApiException(409, "The requested quantity exceeds available stock.");
        }

        await using var write = await repository.BeginOrderAsync(productId, quantity, ct);
        var orderId = write.Order.OrderId;
        logger.LogInformation("Starting stock deduction for order {OrderId}, product {ProductId}, quantity {Quantity}", orderId, productId, quantity);
        try
        {
            await products.ReduceAsync(productId, quantity, correlationId, ct);
            // This commits only order_db, not the remote Product Service write.
            await write.CommitAsync(ct);
        }
        catch (ApiException ex) when (ex.StatusCode is 404 or 409)
        {
            logger.LogWarning("Order {OrderId} rejected before commit: product validation failed with {StatusCode}", orderId, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError("UNCERTAIN outcome for order {OrderId}, product {ProductId}, quantity {Quantity}; exception type {ExceptionType}. Stock may be deducted; reconcile before retrying.",
                orderId, productId, quantity, ex.GetType().Name);
            throw;
        }

        logger.LogInformation("Created order {OrderId} for product {ProductId}, quantity {Quantity}", orderId, productId, quantity);
        return OrderResponse.From(write.Order);
    }

    public async Task<PagedResponse<OrderResponse>> ListAsync(PageQuery page, CancellationToken ct)
    {
        var (items, total) = await repository.ListAsync(page, ct);
        return new(items.Select(OrderResponse.From).ToArray(), page.PageNumber, page.PageSize, total);
    }
}
