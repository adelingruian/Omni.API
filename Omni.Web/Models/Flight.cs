namespace Omni.Web.Models
{
    public class Flight
    {
        // Primary key
        public int FlightId { get; set; }

        // Identifiers
        public string FlightNumber { get; set; } = default!;
        public int AircraftId { get; set; }
        public int GateId { get; set; }
        public int CrewId { get; set; }

        // Scheduled times (planned)
        public DateTimeOffset ScheduledArrival { get; set; }
        public DateTimeOffset ScheduledDeparture { get; set; }

        // Estimated times (predictions)
        public DateTimeOffset? EstimatedArrival { get; set; }
        public DateTimeOffset? EstimatedDeparture { get; set; }

        // Actual times (observed)
        public DateTimeOffset? ActualArrival { get; set; }
        public DateTimeOffset? ActualDeparture { get; set; }

        // Operational fields
        public TimeSpan? TurnaroundDuration { get; set; }

        // Status and delay information
        public string Status { get; set; } = "Scheduled";
        public int DelayMinutes { get; set; }
    }
}