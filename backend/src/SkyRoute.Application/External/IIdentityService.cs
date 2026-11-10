namespace SkyRoute.Application.External;

public sealed record IdentityValidationResult(bool IsValid, string? Reason = null);

public interface IIdentityService
{
    Task<IdentityValidationResult> ValidateRecipientAsync(string dni, CancellationToken cancellationToken = default);
}
