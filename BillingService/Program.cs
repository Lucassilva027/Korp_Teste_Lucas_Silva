using System.Text.Json;
using BillingService.Data;
using BillingService.Dtos;
using BillingService.Models;
using BillingService.Options;
using BillingService.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("BillingDb")));
builder.Services.Configure<StockServiceOptions>(builder.Configuration.GetSection(StockServiceOptions.SectionName));
builder.Services.AddHttpClient<StockServiceClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<StockServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
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
                title: "Erro interno no servico de faturamento",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError)
            .ExecuteAsync(context);
    }
});

app.UseCors("frontend");

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => TypedResults.Ok(new { status = "healthy", service = "billing" }));

var api = app.MapGroup("/api");

api.MapGet("/invoices", async Task<Ok<IReadOnlyList<InvoiceResponse>>> (BillingDbContext dbContext, CancellationToken cancellationToken) =>
{
    var invoices = await dbContext.Invoices
        .AsNoTracking()
        .Include(invoice => invoice.Items)
        .OrderByDescending(invoice => invoice.Number)
        .ToListAsync(cancellationToken);

    return TypedResults.Ok<IReadOnlyList<InvoiceResponse>>(invoices.Select(InvoiceResponse.FromModel).ToList());
});

api.MapGet("/invoices/{id:guid}", async Task<Results<Ok<InvoiceResponse>, NotFound>> (
    Guid id,
    BillingDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var invoice = await dbContext.Invoices
        .AsNoTracking()
        .Include(current => current.Items)
        .FirstOrDefaultAsync(current => current.Id == id, cancellationToken);

    return invoice is null
        ? TypedResults.NotFound()
        : TypedResults.Ok(InvoiceResponse.FromModel(invoice));
});

api.MapPost("/invoices", async Task<Results<Created<InvoiceResponse>, ValidationProblem>> (
    CreateInvoiceRequest request,
    BillingDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var validationErrors = CreateInvoiceRequest.Validate(request);
    if (validationErrors.Count > 0)
    {
        return TypedResults.ValidationProblem(validationErrors);
    }

    var normalizedItems = request.Items
        .GroupBy(item => item.ProductId)
        .Select(group =>
        {
            var first = group.First();
            return new InvoiceItem
            {
                ProductId = first.ProductId,
                ProductCode = first.ProductCode.Trim().ToUpperInvariant(),
                ProductDescription = first.ProductDescription.Trim(),
                Quantity = group.Sum(item => item.Quantity)
            };
        })
        .OrderBy(item => item.ProductCode)
        .ToList();

    var nextNumber = (await dbContext.Invoices.MaxAsync(invoice => (int?)invoice.Number, cancellationToken) ?? 0) + 1;

    var invoice = new Invoice
    {
        Number = nextNumber,
        Status = InvoiceStatus.Open,
        CreatedAtUtc = DateTime.UtcNow,
        Items = normalizedItems
    };

    dbContext.Invoices.Add(invoice);
    await dbContext.SaveChangesAsync(cancellationToken);

    return TypedResults.Created($"/api/invoices/{invoice.Id}", InvoiceResponse.FromModel(invoice));
});

api.MapPost("/invoices/{id:guid}/print", async Task<Results<Ok<PrintInvoiceResponse>, ProblemHttpResult, NotFound>> (
    Guid id,
    BillingDbContext dbContext,
    StockServiceClient stockServiceClient,
    CancellationToken cancellationToken) =>
{
    var invoice = await dbContext.Invoices
        .Include(current => current.Items)
        .FirstOrDefaultAsync(current => current.Id == id, cancellationToken);

    if (invoice is null)
    {
        return TypedResults.NotFound();
    }

    if (invoice.Status == InvoiceStatus.Closed)
    {
        return TypedResults.Problem(
            title: "Nota fiscal ja fechada",
            detail: "Somente notas com status Aberta podem ser impressas.",
            statusCode: StatusCodes.Status409Conflict);
    }

    await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken);

    var consumeResult = await stockServiceClient.ConsumeStockAsync(invoice, cancellationToken);
    if (!consumeResult.Success)
    {
        invoice.LastError = consumeResult.Message;
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Problem(
            title: "Falha ao imprimir nota fiscal",
            detail: consumeResult.Message,
            statusCode: consumeResult.StatusCode);
    }

    invoice.Status = InvoiceStatus.Closed;
    invoice.ClosedAtUtc = DateTime.UtcNow;
    invoice.LastError = null;

    await dbContext.SaveChangesAsync(cancellationToken);

    return TypedResults.Ok(new PrintInvoiceResponse(
        InvoiceResponse.FromModel(invoice),
        consumeResult.Idempotent
            ? "Impressao concluida com recuperacao idempotente."
            : "Impressao concluida e estoque atualizado."));
});

app.Run();
