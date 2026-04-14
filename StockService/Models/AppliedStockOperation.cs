namespace StockService.Models;

public sealed class AppliedStockOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string OperationKey { get; set; } = string.Empty;

    public string? InvoiceNumber { get; set; }

    public DateTime AppliedAtUtc { get; set; }
}
