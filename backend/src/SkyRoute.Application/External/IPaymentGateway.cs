using SkyRoute.Domain.Missions;

namespace SkyRoute.Application.External;

public sealed record PaymentResult(bool Approved, string? Reason = null);

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeMissionAsync(Mission mission, CancellationToken cancellationToken = default);
}
