using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SkyRoute.Application.External;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Infrastructure.External;

public sealed class PadiPaymentGateway(HttpClient httpClient, IConfiguration configuration) : IPaymentGateway
{
    private const string Currency = "ARS";
    private const string LinkType = "SingleUse";
    private int TtlMinutes => IntegrationServiceCollectionExtensions.GetTtlMinutes(configuration);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<PaymentLink> CreatePaymentLinkAsync(
        Mission mission,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mission);

        using var response = await httpClient.PostAsJsonAsync(
            "api/v1/integrations/payment-links",
            new CreatePaymentLinkRequest(amount, $"Mission {mission.Id}", Currency, LinkType, TtlMinutes),
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.StatusCode != System.Net.HttpStatusCode.Created)
        {
            throw InvalidResponse("Padi did not confirm payment link creation with HTTP 201.");
        }

        var result = await ReadRequiredJsonAsync<CreatePaymentLinkResponse>(response, cancellationToken);
        var id = RequireText(result.Id, "payment link id");
        if (result.Amount is null || result.Amount.Value != amount)
        {
            throw InvalidResponse("Padi returned a payment link with an unexpected amount.");
        }
        if (!string.Equals(result.Currency, Currency, StringComparison.Ordinal))
        {
            throw InvalidResponse("Padi returned a payment link with an unexpected currency.");
        }
        if (!Uri.TryCreate(result.CheckoutUrl, UriKind.Absolute, out var checkoutUrl) ||
            checkoutUrl.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(checkoutUrl.UserInfo))
        {
            throw InvalidResponse("Padi returned an unusable checkout URL.");
        }
        if (result.ExpiresAt is null)
        {
            throw InvalidResponse("Padi returned a payment link without an expiry time.");
        }

        return new PaymentLink(id, result.Amount.Value, checkoutUrl, result.ExpiresAt.Value);
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(
        string paymentLinkId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paymentLinkId))
        {
            throw new ArgumentException("A payment link id is required.", nameof(paymentLinkId));
        }

        using var linkResponse = await httpClient.GetAsync(
            $"api/v1/integrations/payment-links/{Uri.EscapeDataString(paymentLinkId)}",
            cancellationToken);
        linkResponse.EnsureSuccessStatusCode();
        var link = await ReadRequiredJsonAsync<PaymentLinkResponse>(linkResponse, cancellationToken);
        var linkStatus = ValidateLink(link, paymentLinkId);

        using var paymentsResponse = await httpClient.GetAsync(
            "api/v1/integrations/received-payments?limit=100",
            cancellationToken);
        paymentsResponse.EnsureSuccessStatusCode();
        var payments = await ReadRequiredJsonAsync<List<ReceivedPaymentResponse>>(paymentsResponse, cancellationToken);
        if (payments is null)
        {
            throw InvalidResponse("Padi returned an invalid received-payments list.");
        }

        foreach (var payment in payments)
        {
            if (payment is null)
            {
                throw InvalidResponse("Padi returned an invalid received payment entry.");
            }
            var listedId = RequireText(payment.Id, "received payment id");
            var listedLinkId = RequireText(payment.PaymentLinkId, "received payment link id");
            var listedStatus = ParseReceivedPaymentStatus(payment.Status);
            if (!string.Equals(listedLinkId, paymentLinkId, StringComparison.Ordinal) ||
                listedStatus != ReceivedPaymentStatus.Succeeded)
            {
                continue;
            }

            using var paymentResponse = await httpClient.GetAsync(
                $"api/v1/integrations/received-payments/{Uri.EscapeDataString(listedId)}",
                cancellationToken);
            paymentResponse.EnsureSuccessStatusCode();
            var verifiedPayment = await ReadRequiredJsonAsync<ReceivedPaymentResponse>(paymentResponse, cancellationToken);
            ValidateSuccessfulPayment(verifiedPayment, listedId, paymentLinkId, link.Amount!.Value);
            return PaymentStatus.Paid;
        }

        if (linkStatus is PaymentLinkStatus.Expired or PaymentLinkStatus.Cancelled ||
            link.ExpiresAt!.Value <= DateTimeOffset.UtcNow)
        {
            return PaymentStatus.Expired;
        }

        return PaymentStatus.Pending;
    }

    private static PaymentLinkStatus ValidateLink(PaymentLinkResponse link, string expectedId)
    {
        if (!string.Equals(RequireText(link.Id, "payment link id"), expectedId, StringComparison.Ordinal))
        {
            throw InvalidResponse("Padi returned a different payment link.");
        }
        if (link.Amount is null || link.Amount < 0)
        {
            throw InvalidResponse("Padi returned an invalid payment link amount.");
        }
        if (!string.Equals(link.Currency, Currency, StringComparison.Ordinal))
        {
            throw InvalidResponse("Padi returned a payment link with an unexpected currency.");
        }
        if (!string.Equals(link.Type, LinkType, StringComparison.Ordinal))
        {
            throw InvalidResponse("Padi returned an unexpected payment link type.");
        }
        if (link.ExpiresAt is null)
        {
            throw InvalidResponse("Padi returned a payment link without an expiry time.");
        }
        return ParsePaymentLinkStatus(link.Status);
    }

    private static void ValidateSuccessfulPayment(
        ReceivedPaymentResponse payment,
        string expectedPaymentId,
        string expectedLinkId,
        decimal expectedAmount)
    {
        if (!string.Equals(RequireText(payment.Id, "received payment id"), expectedPaymentId, StringComparison.Ordinal) ||
            !string.Equals(RequireText(payment.PaymentLinkId, "received payment link id"), expectedLinkId, StringComparison.Ordinal))
        {
            throw InvalidResponse("Padi returned a received payment that does not match the payment link.");
        }
        if (ParseReceivedPaymentStatus(payment.Status) != ReceivedPaymentStatus.Succeeded)
        {
            throw InvalidResponse("Padi's received payment is not successful.");
        }
        if (payment.Amount is null || payment.Amount.Value != expectedAmount ||
            !string.Equals(payment.Currency, Currency, StringComparison.Ordinal))
        {
            throw InvalidResponse("Padi returned a received payment with an unexpected amount or currency.");
        }
    }

    private static PaymentLinkStatus ParsePaymentLinkStatus(string? status) => status switch
    {
        "Active" => PaymentLinkStatus.Active,
        "Completed" => PaymentLinkStatus.Completed,
        "Expired" => PaymentLinkStatus.Expired,
        "Cancelled" => PaymentLinkStatus.Cancelled,
        _ => throw InvalidResponse("Padi returned an unknown payment link status."),
    };

    private static ReceivedPaymentStatus ParseReceivedPaymentStatus(string? status) => status switch
    {
        "Pending" => ReceivedPaymentStatus.Pending,
        "Processing" => ReceivedPaymentStatus.Processing,
        "Succeeded" => ReceivedPaymentStatus.Succeeded,
        "Failed" => ReceivedPaymentStatus.Failed,
        "Cancelled" => ReceivedPaymentStatus.Cancelled,
        _ => throw InvalidResponse("Padi returned an unknown received payment status."),
    };

    private static string RequireText(string? value, string field) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw InvalidResponse($"Padi returned an empty {field}.");

    private static async Task<T> ReadRequiredJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                ?? throw InvalidResponse("Padi returned an empty response.");
        }
        catch (JsonException)
        {
            throw InvalidResponse("Padi returned malformed JSON.");
        }
    }

    private static InvalidOperationException InvalidResponse(string message) => new(message);

    private sealed record CreatePaymentLinkRequest(
        decimal Amount,
        string Title,
        string Currency,
        string Type,
        int TtlMinutes);

    private sealed record CreatePaymentLinkResponse(
        string? Id,
        decimal? Amount,
        string? Currency,
        string? CheckoutUrl,
        DateTimeOffset? ExpiresAt);

    private sealed record PaymentLinkResponse(
        string? Id,
        decimal? Amount,
        string? Currency,
        string? Type,
        string? Status,
        DateTimeOffset? ExpiresAt);

    private sealed record ReceivedPaymentResponse(
        string? Id,
        string? PaymentLinkId,
        decimal? Amount,
        string? Currency,
        string? Status);

    private enum PaymentLinkStatus { Active, Completed, Expired, Cancelled }
    private enum ReceivedPaymentStatus { Pending, Processing, Succeeded, Failed, Cancelled }
}
