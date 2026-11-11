using SkyRoute.Domain.Missions;

namespace SkyRoute.Application.External;

// A checkout link the payer opens to pay for one mission.
public sealed record PaymentLink(string Id, decimal Amount, Uri CheckoutUrl, DateTimeOffset ExpiresAt);

public enum PaymentStatus
{
    Pending,
    Paid,
    Expired,
}

// Payment is asynchronous: create a link, then check its status until it is Paid or Expired.
public interface IPaymentGateway
{
    Task<PaymentLink> CreatePaymentLinkAsync(Mission mission, decimal amount, CancellationToken cancellationToken = default);

    Task<PaymentStatus> GetPaymentStatusAsync(string paymentLinkId, CancellationToken cancellationToken = default);
}
