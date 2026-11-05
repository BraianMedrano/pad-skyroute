namespace SkyRoute.Domain.Missions;

public sealed class InvalidMissionTransitionException(MissionStatus from, MissionStatus to)
    : Exception($"A mission cannot move from {from} to {to}.")
{
    public MissionStatus From { get; } = from;
    public MissionStatus To { get; } = to;
}
