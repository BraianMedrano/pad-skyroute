using SkyRoute.Application.External;

namespace SkyRoute.Infrastructure.External;

public sealed class FakeIdentityService(string rejectedDni = "00000000", string rejectionReason = "Recipient DNI rejected") : IIdentityService
{
    public Task<IdentityValidationResult> ValidateRecipientAsync(string dni, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = dni == rejectedDni
            ? new IdentityValidationResult(false, rejectionReason)
            : new IdentityValidationResult(true);
        return Task.FromResult(result);
    }
}
