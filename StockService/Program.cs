using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StockService.Data;
using StockService.Dtos;
using StockService.Models;
using StockService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy
            .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"])
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<StockDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("StockDb")));
builder.Services.AddSingleton<FailureModeState>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception exception)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
                title: "Erro interno no servico de estoque",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError)
            .ExecuteAsync(context);
    }
});

app.UseCors("frontend");

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<StockDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => TypedResults.Ok(new { status = "healthy", service = "stock" }));

var api = app.MapGroup("/api");

api.MapGet("/products", async Task<Ok<IReadOnlyList<ProductResponse>>> (StockDbContext dbContext, CancellationToken cancellationToken) =>
{
    var products = await dbContext.Products
        .AsNoTracking()
        .OrderBy(product => product.Code)
        .Select(product => ProductResponse.FromModel(product))
        .ToListAsync(cancellationToken);

    return TypedResults.Ok<IReadOnlyList<ProductResponse>>(products);
});

api.MapGet("/products/{id:guid}", async Task<Results<Ok<ProductResponse>, NotFound>> (Guid id, StockDbContext dbContext, CancellationToken cancellationToken) =>
{
    var product = await dbContext.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(current => current.Id == id, cancellationToken);

    return product is null
        ? TypedResults.NotFound()
        : TypedResults.Ok(ProductResponse.FromModel(product));
});

api.MapPost("/products", async Task<Results<Created<ProductResponse>, ValidationProblem, ProblemHttpResult>> (
    CreateProductRequest request,
    StockDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var validationErrors = CreateProductRequest.Validate(request);
    if (validationErrors.Count > 0)
    {
        return TypedResults.ValidationProblem(validationErrors);
    }

    var normalizedCode = request.Code.Trim().ToUpperInvariant();

    var alreadyExists = await dbContext.Products
        .AnyAsync(product => product.Code == normalizedCode, cancellationToken);

    if (alreadyExists)
    {
        return TypedResults.Problem(
            title: "Codigo duplicado",
            detail: $"Ja existe um produto cadastrado com o codigo {normalizedCode}.",
            statusCode: StatusCodes.Status409Conflict);
    }

    var utcNow = DateTime.UtcNow;
    var product = new Product
    {
        Code = normalizedCode,
        Description = request.Description.Trim(),
        Balance = request.Balance,
        CreatedAtUtc = utcNow,
        UpdatedAtUtc = utcNow
    };

    dbContext.Products.Add(product);
    await dbContext.SaveChangesAsync(cancellationToken);

    return TypedResults.Created($"/api/products/{product.Id}", ProductResponse.FromModel(product));
});

api.MapGet("/failure-mode", (FailureModeState failureModeState) =>
    TypedResults.Ok(new FailureModeResponse(failureModeState.Enabled, failureModeState.Message)));

api.MapPost("/failure-mode", (FailureModeRequest request, FailureModeState failureModeState) =>
{
    failureModeState.Set(request.Enabled, request.Message);
    return TypedResults.Ok(new FailureModeResponse(failureModeState.Enabled, failureModeState.Message));
});

api.MapPost("/stock/consume", async Task<Results<Ok<ConsumeStockResponse>, ValidationProblem, ProblemHttpResult>> (
    ConsumeStockRequest request,
    StockDbContext dbContext,
    FailureModeState failureModeState,
    CancellationToken cancellationToken) =>
{
    if (failureModeState.Enabled)
    {
        return TypedResults.Problem(
            title: "Servico de estoque indisponivel",
            detail: failureModeState.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var validationErrors = ConsumeStockRequest.Validate(request);
    if (validationErrors.Count > 0)
    {
        return TypedResults.ValidationProblem(validationErrors);
    }

    var operationKey = request.OperationKey.Trim();
    var normalizedItems = request.Items
        .GroupBy(item => item.ProductId)
        .Select(group => new
        {
            ProductId = group.Key,
            Quantity = group.Sum(item => item.Quantity)
        })
        .ToList();

    var alreadyApplied = await dbContext.AppliedStockOperations
        .AsNoTracking()
        .AnyAsync(operation => operation.OperationKey == operationKey, cancellationToken);

    if (alreadyApplied)
    {
        return TypedResults.Ok(new ConsumeStockResponse(
            request.OperationKey,
            true,
            "A operacao ja havia sido aplicada anteriormente.",
            request.InvoiceNumber));
    }

    var productIds = normalizedItems.Select(item => item.ProductId).ToList();
    var products = await dbContext.Products
        .Where(product => productIds.Contains(product.Id))
        .ToListAsync(cancellationToken);

    if (products.Count != productIds.Count)
    {
        return TypedResults.Problem(
            title: "Produto nao encontrado",
            detail: "Um ou mais produtos informados nao existem mais no estoque.",
            statusCode: StatusCodes.Status404NotFound);
    }

    foreach (var item in normalizedItems)
    {
        var product = products.First(current => current.Id == item.ProductId);
        if (product.Balance < item.Quantity)
        {
            return TypedResults.Problem(
                title: "Saldo insuficiente",
                detail: $"O produto {product.Code} possui saldo {product.Balance} e nao suporta a quantidade {item.Quantity}.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

    foreach (var item in normalizedItems)
    {
        var product = products.First(current => current.Id == item.ProductId);
        product.Balance -= item.Quantity;
        product.UpdatedAtUtc = DateTime.UtcNow;
    }

    dbContext.AppliedStockOperations.Add(new AppliedStockOperation
    {
        OperationKey = operationKey,
        InvoiceNumber = request.InvoiceNumber?.Trim(),
        AppliedAtUtc = DateTime.UtcNow
    });

    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return TypedResults.Ok(new ConsumeStockResponse(
        request.OperationKey,
        false,
        "Baixa de estoque concluida com sucesso.",
        request.InvoiceNumber));
});

app.Run();
