using SkyRoute.Application.External;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Infrastructure.External;

public sealed class FakePaymentGateway(bool approved = true, string? rejectionReason = "Payment rejected") : IPaymentGateway
{
    public Task<PaymentResult> ChargeMissionAsync(Mission mission, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PaymentResult(approved, approved ? null : rejectionReason));
    }
}
