namespace StockService.Dtos;

public sealed class CreateProductRequest
{
    public string Code { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int Balance { get; init; }

    public static Dictionary<string, string[]> Validate(CreateProductRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            errors["code"] = ["O codigo do produto e obrigatorio."];
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors["description"] = ["A descricao do produto e obrigatoria."];
        }

        if (request.Balance < 0)
        {
            errors["balance"] = ["O saldo inicial nao pode ser negativo."];
        }

        return errors;
    }
}
