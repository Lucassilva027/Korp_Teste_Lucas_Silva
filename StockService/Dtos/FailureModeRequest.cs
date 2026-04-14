namespace StockService.Dtos;

public sealed class FailureModeRequest
{
    public bool Enabled { get; init; }

    public string? Message { get; init; }
}

public sealed record FailureModeResponse(bool Enabled, string? Message);
