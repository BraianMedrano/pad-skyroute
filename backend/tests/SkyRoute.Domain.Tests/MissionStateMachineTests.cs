using SkyRoute.Domain;
using SkyRoute.Domain.Missions;

namespace SkyRoute.Domain.Tests;

public class MissionStateMachineTests
{
    public enum MissionAction
    {
        Validate,
        MarkPaid,
        Assign,
        Start,
        Complete,
        Fail,
    }

    private static readonly Guid DroneId = Guid.NewGuid();

    // The only allowed transitions: Requested → Validated → Paid → Assigned → InProgress → Completed / Failed.
    private static readonly Dictionary<MissionAction, (MissionStatus From, MissionStatus To)> Allowed = new()
    {
        [MissionAction.Validate] = (MissionStatus.Requested, MissionStatus.Validated),
        [MissionAction.MarkPaid] = (MissionStatus.Validated, MissionStatus.Paid),
        [MissionAction.Assign] = (MissionStatus.Paid, MissionStatus.Assigned),
        [MissionAction.Start] = (MissionStatus.Assigned, MissionStatus.InProgress),
        [MissionAction.Complete] = (MissionStatus.InProgress, MissionStatus.Completed),
        [MissionAction.Fail] = (MissionStatus.InProgress, MissionStatus.Failed),
    };

    public static TheoryData<MissionAction, MissionStatus, MissionStatus> AllowedTransitions()
    {
        var data = new TheoryData<MissionAction, MissionStatus, MissionStatus>();
        foreach (var (action, (from, to)) in Allowed)
        {
            data.Add(action, from, to);
        }
        return data;
    }

    public static TheoryData<MissionAction, MissionStatus> RejectedTransitions()
    {
        var data = new TheoryData<MissionAction, MissionStatus>();
        foreach (var action in Enum.GetValues<MissionAction>())
        {
            foreach (var status in Enum.GetValues<MissionStatus>())
            {
                if (Allowed[action].From != status)
                {
                    data.Add(action, status);
                }
            }
        }
        return data;
    }

    [Fact]
    public void A_requested_delivery_starts_in_Requested_with_no_drone()
    {
        var mission = NewMission();

        Assert.Equal(MissionStatus.Requested, mission.Status);
        Assert.Equal(MissionType.Delivery, mission.Type);
        Assert.Null(mission.AssignedDroneId);
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void Allowed_transition_moves_the_mission_to_the_next_status(MissionAction action, MissionStatus from, MissionStatus to)
    {
        var mission = MissionIn(from);

        Apply(mission, action);

        Assert.Equal(to, mission.Status);
    }

    [Theory]
    [MemberData(nameof(RejectedTransitions))]
    public void Rejected_transition_throws_and_keeps_the_status(MissionAction action, MissionStatus status)
    {
        var mission = MissionIn(status);

        Assert.Throws<InvalidMissionTransitionException>(() => Apply(mission, action));
        Assert.Equal(status, mission.Status);
    }

    [Fact]
    public void Assign_records_the_drone()
    {
        var mission = MissionIn(MissionStatus.Paid);

        mission.Assign(DroneId);

        Assert.Equal(DroneId, mission.AssignedDroneId);
    }

    [Fact]
    public void Validate_records_the_price()
    {
        var mission = NewMission();

        mission.Validate(4500m);

        Assert.Equal(4500m, mission.Price);
    }

    [Fact]
    public void Fail_records_the_reason()
    {
        var mission = MissionIn(MissionStatus.InProgress);

        mission.Fail("Battery depleted");

        Assert.Equal("Battery depleted", mission.FailureReason);
    }

    private const decimal Price = 3500m;

    private static Mission NewMission() =>
        Mission.RequestDelivery(
            new Route(new Coordinates(-34.6037, -58.3816), new Coordinates(-34.5875, -58.3974)),
            packageWeightKg: 1.5,
            recipientDni: "12345678");

    // Walks the allowed path until the mission reaches the requested status.
    private static Mission MissionIn(MissionStatus status)
    {
        var mission = NewMission();
        MissionAction[] path = status == MissionStatus.Failed
            ? [MissionAction.Validate, MissionAction.MarkPaid, MissionAction.Assign, MissionAction.Start, MissionAction.Fail]
            : [MissionAction.Validate, MissionAction.MarkPaid, MissionAction.Assign, MissionAction.Start, MissionAction.Complete];

        foreach (var action in path)
        {
            if (mission.Status == status)
            {
                break;
            }
            Apply(mission, action);
        }
        return mission;
    }

    private static void Apply(Mission mission, MissionAction action)
    {
        switch (action)
        {
            case MissionAction.Validate: mission.Validate(Price); break;
            case MissionAction.MarkPaid: mission.MarkPaid(); break;
            case MissionAction.Assign: mission.Assign(DroneId); break;
            case MissionAction.Start: mission.Start(); break;
            case MissionAction.Complete: mission.Complete(); break;
            case MissionAction.Fail: mission.Fail("Test failure"); break;
        }
    }
}
