using Microsoft.AspNetCore.Mvc;
using Product.Api.DTOs;
using Product.Api.Services;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(ProductRequest request, CancellationToken ct)
    {
        var product = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = product.ProductId }, product);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await service.GetAsync(id, ct));

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> List([FromQuery] PageQuery page, CancellationToken ct)
        => Ok(await service.ListAsync(page, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> Update(Guid id, ProductRequest request, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reduce-stock")]
    public async Task<ActionResult<StockResponse>> ReduceStock(Guid id, StockRequest request, CancellationToken ct)
        => Ok(await service.ReduceStockAsync(id, request, ct));
}
