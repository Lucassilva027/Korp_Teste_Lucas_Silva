namespace BillingService.Dtos;

public sealed class CreateInvoiceRequest
{
    public IReadOnlyList<CreateInvoiceItemRequest> Items { get; init; } = [];

    public static Dictionary<string, string[]> Validate(CreateInvoiceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Items.Count == 0)
        {
            errors["items"] = ["Informe ao menos um produto para a nota fiscal."];
            return errors;
        }

        if (request.Items.Any(item =>
                item.ProductId == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.ProductCode) ||
                string.IsNullOrWhiteSpace(item.ProductDescription) ||
                item.Quantity <= 0))
        {
            errors["items"] = ["Todos os itens da nota precisam ter produto valido e quantidade maior que zero."];
        }

        return errors;
    }
}

public sealed class CreateInvoiceItemRequest
{
    public Guid ProductId { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    public string ProductDescription { get; init; } = string.Empty;

    public int Quantity { get; init; }
}
