namespace BillingService.Options;

public sealed class StockServiceOptions
{
    public const string SectionName = "StockService";

    public string BaseUrl { get; init; } = "http://localhost:5001";

    public int TimeoutSeconds { get; init; } = 5;
}
