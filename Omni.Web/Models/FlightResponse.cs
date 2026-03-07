namespace Omni.Web.Models
{
    public sealed record GateResponse(int GateId, string Name, GateStatus Status, string? Description);
    public sealed record RunwayResponse(int RunwayId, string Name, RunwayStatus Status, string? Description);

    public sealed record FlightResponse(
        int FlightId,
        string FlightNumber,
        string Aircraft,
        string Origin,
        string Destination,
        DateTimeOffset ScheduledDeparture,
        DateTimeOffset? ActualDeparture,
        DateTimeOffset ScheduledArrival,
        DateTimeOffset? ActualArrival,
        GateResponse Gate,
        RunwayResponse Runway,
        int PassengerNumber,
        int DelayMinutes,
        int CrewPilots,
        int CrewFlightAttendants,
        string BaggageConveyorBelt,
        int BaggageTotalChecked);
}
