using System.Net;
using System.Net.Http.Json;
using BillingService.Models;
using Microsoft.AspNetCore.Mvc;

namespace BillingService.Services;

public sealed class StockServiceClient(HttpClient httpClient)
{
    public async Task<StockConsumeResult> ConsumeStockAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var request = new StockConsumeRequest(
            $"invoice:{invoice.Id}",
            invoice.Number.ToString("D6"),
            invoice.Items
                .Select(item => new StockConsumeItemRequest(item.ProductId, item.Quantity))
                .ToList());

        ProblemDetails? lastProblem = null;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var response = await httpClient.PostAsJsonAsync("/api/stock/consume", request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<StockConsumeResponse>(cancellationToken);
                    return new StockConsumeResult(
                        true,
                        payload?.Idempotent ?? false,
                        payload?.Message ?? "Baixa de estoque concluida.",
                        StatusCodes.Status200OK);
                }

                lastProblem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
                var statusCode = (int)response.StatusCode;
                var isTransientFailure = response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                                         statusCode >= StatusCodes.Status500InternalServerError;

                if (!isTransientFailure || attempt == 3)
                {
                    return new StockConsumeResult(
                        false,
                        false,
                        lastProblem?.Detail ?? lastProblem?.Title ?? "Nao foi possivel atualizar o estoque.",
                        statusCode);
                }
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException)
            {
                lastException = exception;
                if (attempt == 3)
                {
                    break;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
        }

        return new StockConsumeResult(
            false,
            false,
            lastProblem?.Detail ?? lastException?.Message ?? "O servico de estoque nao respondeu apos as tentativas de retry.",
            StatusCodes.Status503ServiceUnavailable);
    }
}

public sealed record StockConsumeRequest(
    string OperationKey,
    string InvoiceNumber,
    IReadOnlyList<StockConsumeItemRequest> Items);

public sealed record StockConsumeItemRequest(Guid ProductId, int Quantity);

public sealed record StockConsumeResponse(
    string OperationKey,
    bool Idempotent,
    string Message,
    string? InvoiceNumber);

public sealed record StockConsumeResult(
    bool Success,
    bool Idempotent,
    string Message,
    int StatusCode);
