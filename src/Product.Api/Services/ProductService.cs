using Product.Api.DTOs;
using Product.Api.Infrastructure;
using Product.Api.Repositories;

namespace Product.Api.Services;

public sealed class ProductService(ProductRepository repository, ILogger<ProductService> logger)
{
    public async Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken ct)
    {
        var product = await repository.CreateAsync(request, ct);
        logger.LogInformation("Created product {ProductId}", product.ProductId);
        return ProductResponse.From(product);
    }

    public async Task<ProductResponse> GetAsync(Guid id, CancellationToken ct) => ProductResponse.From(
        await repository.GetAsync(id, ct) ?? throw Missing());

    public async Task<PagedResponse<ProductResponse>> ListAsync(PageQuery page, CancellationToken ct)
    {
        var (items, total) = await repository.ListAsync(page, ct);
        return new(items.Select(ProductResponse.From).ToArray(), page.PageNumber, page.PageSize, total);
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, ProductRequest request, CancellationToken ct)
    {
        var product = await repository.UpdateAsync(id, request, ct) ?? throw Missing();
        logger.LogInformation("Updated product {ProductId}", id);
        return ProductResponse.From(product);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        if (!await repository.DeleteAsync(id, ct)) throw Missing();
        logger.LogInformation("Soft-deleted product {ProductId}", id);
    }

    public async Task<StockResponse> ReduceStockAsync(Guid id, StockRequest request, CancellationToken ct)
    {
        var result = await repository.ReduceStockAsync(id, request.Quantity!.Value, ct);
        if (result is null)
        {
            if (await repository.GetAsync(id, ct) is null) throw Missing();
            logger.LogWarning("Insufficient stock for product {ProductId}", id);
            throw new ApiException(409, "The requested quantity exceeds available stock.");
        }
        logger.LogInformation("Deducted {Quantity} units from product {ProductId}; remaining {StockQty}", request.Quantity, id, result.StockQty);
        return result;
    }

    private static ApiException Missing() => new(404, "The active product was not found.");
}
