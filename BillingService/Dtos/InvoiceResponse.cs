using BillingService.Models;

namespace BillingService.Dtos;

public sealed record InvoiceItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductDescription,
    int Quantity);

public sealed record InvoiceResponse(
    Guid Id,
    int Number,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    string? LastError,
    IReadOnlyList<InvoiceItemResponse> Items)
{
    public static InvoiceResponse FromModel(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.Number,
            invoice.Status == InvoiceStatus.Open ? "Aberta" : "Fechada",
            AsUtc(invoice.CreatedAtUtc),
            AsUtc(invoice.ClosedAtUtc),
            invoice.LastError,
            invoice.Items
                .OrderBy(item => item.ProductCode)
                .Select(item => new InvoiceItemResponse(
                    item.Id,
                    item.ProductId,
                    item.ProductCode,
                    item.ProductDescription,
                    item.Quantity))
                .ToList());

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? value) =>
        value is null
            ? null
            : AsUtc(value.Value);
}

public sealed record PrintInvoiceResponse(InvoiceResponse Invoice, string Message);
