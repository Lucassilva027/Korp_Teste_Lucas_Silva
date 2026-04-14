namespace BillingService.Models;

public sealed class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int Number { get; set; }

    public InvoiceStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public string? LastError { get; set; }

    public List<InvoiceItem> Items { get; set; } = [];
}
