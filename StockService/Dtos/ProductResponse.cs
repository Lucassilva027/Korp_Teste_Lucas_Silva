using StockService.Models;

namespace StockService.Dtos;

public sealed record ProductResponse(
    Guid Id,
    string Code,
    string Description,
    int Balance,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static ProductResponse FromModel(Product product) =>
        new(
            product.Id,
            product.Code,
            product.Description,
            product.Balance,
            AsUtc(product.CreatedAtUtc),
            AsUtc(product.UpdatedAtUtc));

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
