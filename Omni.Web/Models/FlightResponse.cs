namespace Omni.Web.Models
{
    public sealed record GateResponse(string GateId, string Status);

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
        string Runway,
        int PassengerNumber,
        int DelayMinutes,
        int CrewPilots,
        int CrewFlightAttendants,
        string BaggageConveyorBelt,
        int BaggageTotalChecked);
}
