namespace StockService.Dtos;

public sealed class ConsumeStockRequest
{
    public string OperationKey { get; init; } = string.Empty;

    public string? InvoiceNumber { get; init; }

    public IReadOnlyList<ConsumeStockItemRequest> Items { get; init; } = [];

    public static Dictionary<string, string[]> Validate(ConsumeStockRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.OperationKey))
        {
            errors["operationKey"] = ["A chave da operacao e obrigatoria."];
        }

        if (request.Items.Count == 0)
        {
            errors["items"] = ["Informe ao menos um produto para baixar o estoque."];
        }
        else if (request.Items.Any(item => item.ProductId == Guid.Empty || item.Quantity <= 0))
        {
            errors["items"] = ["Todos os itens precisam ter um produto valido e quantidade maior que zero."];
        }

        return errors;
    }
}

public sealed class ConsumeStockItemRequest
{
    public Guid ProductId { get; init; }

    public int Quantity { get; init; }
}

public sealed record ConsumeStockResponse(
    string OperationKey,
    bool Idempotent,
    string Message,
    string? InvoiceNumber);
