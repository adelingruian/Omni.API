namespace Omni.Web.Models
{
    public sealed record GateResponse(int GateId, string Name, GateStatus Status, string? Description);
    public sealed record RunwayResponse(int RunwayId, string Name, RunwayStatus Status, string? Description);
    public sealed record BeltResponse(int BaggageConveyorBeltId, string Name, BeltStatus Status, string? Description);

    public sealed record DisruptionScore(double TotalPoints, string Severity);

    public sealed record FlightResponse(
        int FlightId,
        string FlightNumber,
        string Aircraft,
        string Origin,
        string Destination,
        DateTime? ScheduledDeparture,
        DateTime? ActualDeparture,
        DateTime? ScheduledArrival,
        DateTime? ActualArrival,
        GateResponse Gate,
        RunwayResponse Runway,
        BeltResponse Belt,
        int PassengerNumber,
        int PossibleDelayMinutes,
        int BaggageTotalChecked,
        DisruptionScore DisruptionScore);
}
