using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Order.Api.DTOs;
using Order.Api.Infrastructure;

namespace Order.Api.Services;

public sealed class ProductClient(HttpClient client)
{
    public async Task<ProductSnapshot> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/products/{id}");
        request.Headers.Add("X-Correlation-ID", correlationId);
        using var response = await SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ApiException(404, "The active product was not found.");
        if (!response.IsSuccessStatusCode) throw Unavailable();
        try
        {
            var product = await response.Content.ReadFromJsonAsync<ProductSnapshot>(cancellationToken: ct);
            if (product is null || product.ProductId != id || product.StockQty < 0) throw Unavailable();
            return product;
        }
        catch (JsonException) { throw Unavailable(); }
    }

    public async Task ReduceAsync(Guid id, int quantity, string correlationId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/products/{id}/reduce-stock")
        { Content = JsonContent.Create(new { quantity }) };
        request.Headers.Add("X-Correlation-ID", correlationId);
        using var response = await SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ApiException(404, "The active product was not found.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new ApiException(409, "The requested quantity exceeds available stock.");
        if (!response.IsSuccessStatusCode) throw Unavailable();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try { return await client.SendAsync(request, ct); }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
    }

    private static ApiException Unavailable() => new(503,
        "Product Service could not complete the request. Do not automatically retry an order with an uncertain outcome.");
}
