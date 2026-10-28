using Order.Api.Infrastructure;
using Order.Api.Repositories;
using Order.Api.Services;
using Npgsql;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((_, logging) => logging
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogEventLevel.Fatal)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}")
    .WriteTo.File(Path.Combine(builder.Environment.ContentRootPath, "logs", "Order-.log"),
        rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 10 * 1024 * 1024,
        rollOnFileSizeLimit: true, retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:O} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}"));

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = 400, Title = "Invalid request", Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    });
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
var connectionString = builder.Configuration.GetConnectionString("Orders");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Set ConnectionStrings__Orders before starting the service.");
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddScoped<OrderRepository>();
builder.Services.AddScoped<OrderService>();
var productUrl = builder.Configuration["ProductService:BaseUrl"] ?? "http://localhost:8082/";
if (!Uri.TryCreate(productUrl, UriKind.Absolute, out var productUri) ||
    (productUri.Scheme != "http" && productUri.Scheme != "https") || !string.IsNullOrEmpty(productUri.UserInfo))
    throw new InvalidOperationException("ProductService__BaseUrl must be an HTTP(S) URL without credentials.");
builder.Services.AddHttpClient<ProductClient>(client =>
{
    client.BaseAddress = productUri;
    client.Timeout = TimeSpan.FromSeconds(5);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

var app = builder.Build();
app.Use(async (context, next) =>
{
    var supplied = context.Request.Headers["X-Correlation-ID"].ToString();
    context.TraceIdentifier = Guid.TryParse(supplied, out var id) ? id.ToString("N") : Guid.NewGuid().ToString("N");
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
        return Task.CompletedTask;
    });
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Requests");
    using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = context.TraceIdentifier });
    await next(context);
    logger.LogInformation("HTTP {Method} {Path} returned {StatusCode}", context.Request.Method, context.Request.Path, context.Response.StatusCode);
});
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapControllers();
app.Run();
