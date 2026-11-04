namespace SkyRoute.Domain.Missions;

public enum MissionType
{
    Delivery,
}

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

public record Route(Coordinates Origin, Coordinates Destination);

public sealed class Mission
{
    private Mission(Guid id, MissionType type, Route route, double packageWeightKg, string recipientDni)
    {
        Id = id;
        Type = type;
        Route = route;
        PackageWeightKg = packageWeightKg;
        RecipientDni = recipientDni;
        Status = MissionStatus.Requested;
    }

    public Guid Id { get; }
    public MissionType Type { get; }
    public Route Route { get; }
    public double PackageWeightKg { get; }
    public string RecipientDni { get; }
    public MissionStatus Status { get; private set; }
    public Guid? AssignedDroneId { get; private set; }
    public string? FailureReason { get; private set; }

    public static Mission RequestDelivery(Route route, double packageWeightKg, string recipientDni) =>
        new(Guid.NewGuid(), MissionType.Delivery, route, packageWeightKg, recipientDni);

    public void Validate() => MoveTo(MissionStatus.Requested, MissionStatus.Validated);

    public void MarkPaid() => MoveTo(MissionStatus.Validated, MissionStatus.Paid);

    public void Assign(Guid droneId)
    {
        MoveTo(MissionStatus.Paid, MissionStatus.Assigned);
        AssignedDroneId = droneId;
    }

    public void Start() => MoveTo(MissionStatus.Assigned, MissionStatus.InProgress);

    public void Complete() => MoveTo(MissionStatus.InProgress, MissionStatus.Completed);

    public void Fail(string reason)
    {
        MoveTo(MissionStatus.InProgress, MissionStatus.Failed);
        FailureReason = reason;
    }

    private void MoveTo(MissionStatus requiredCurrent, MissionStatus next)
    {
        if (Status != requiredCurrent)
        {
            throw new InvalidMissionTransitionException(Status, next);
        }
        Status = next;
    }
}
