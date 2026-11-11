using SkyRoute.Application.External;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Infrastructure.External;

// Every link reports the configured status: Paid by default, so the demo flows without a real payer.
public sealed class FakePaymentGateway(PaymentStatus status = PaymentStatus.Paid) : IPaymentGateway
{
    public Task<PaymentLink> CreatePaymentLinkAsync(Mission mission, decimal amount, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = Guid.NewGuid().ToString("N");
        var link = new PaymentLink(id, amount, new Uri($"https://fake-payments.local/checkout/{id}"), DateTimeOffset.UtcNow.AddMinutes(30));
        return Task.FromResult(link);
    }

    public Task<PaymentStatus> GetPaymentStatusAsync(string paymentLinkId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(status);
    }
}
