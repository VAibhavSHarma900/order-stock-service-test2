using Microsoft.AspNetCore.Mvc;
using Order.Api.DTOs;
using Order.Api.Services;

namespace Order.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(OrderService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(OrderRequest request, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, HttpContext.TraceIdentifier, ct));

    [HttpGet]
    public async Task<ActionResult<PagedResponse<OrderResponse>>> List([FromQuery] PageQuery page, CancellationToken ct)
        => Ok(await service.ListAsync(page, ct));
}
