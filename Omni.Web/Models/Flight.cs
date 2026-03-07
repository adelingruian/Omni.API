namespace Omni.Web.Models
{
    public class Flight
    {
        // Primary key
        public int FlightId { get; set; }

        public string FlightNumber { get; set; } = default!;

        // Equipment / routing
        public string Aircraft { get; set; } = default!;
        public string Origin { get; set; } = default!;
        public string Destination { get; set; } = default!;

        // Times
        public DateTimeOffset ScheduledDeparture { get; set; }
        public DateTimeOffset? ActualDeparture { get; set; }
        public DateTimeOffset ScheduledArrival { get; set; }
        public DateTimeOffset? ActualArrival { get; set; }

        // Airport resources (DB ids)
        public int GateId { get; set; }
        public int RunwayId { get; set; }

        // Passengers / delay
        public int PassengerNumber { get; set; }
        public int DelayMinutes { get; set; }

        // Crew info
        public int CrewPilots { get; set; }
        public int CrewFlightAttendants { get; set; }

        // Baggage
        public string BaggageConveyorBelt { get; set; } = default!;
        public int BaggageTotalChecked { get; set; }
    }
}