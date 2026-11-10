namespace SkyRoute.Api.Contracts;

public record Coordinates(double Lat, double Lon);

public enum MissionStatus
{
    Requested,
    Validated,
    Paid,
    Assigned,
    InProgress,
    Completed,
    Failed,
}

public record RequestMissionRequest(
    Coordinates Origin,
    Coordinates Destination,
    double PackageWeightKg,
    string RecipientDni,
    // Optional: we POST the mission to this URL when it ends as Completed or Failed (#15).
    Uri? CallbackUrl = null);

public record MissionResponse(
    Guid Id,
    MissionStatus Status,
    Coordinates Origin,
    Coordinates Destination,
    double PackageWeightKg,
    // ARS 2000 base plus ARS 250 per km of the full trip (base → origin → destination → base), rounded to whole pesos.
    decimal Price,
    Guid? AssignedDroneId,
    // Set only when Status is Failed.
    string? FailureReason = null);

// Padi checkout link for a Validated mission. The mission becomes Paid once the payment is confirmed.
public record PaymentResponse(
    Guid MissionId,
    decimal Amount,
    Uri CheckoutUrl,
    DateTimeOffset ExpiresAt);
