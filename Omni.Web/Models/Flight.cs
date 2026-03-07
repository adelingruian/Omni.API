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

        // Times (all optional)
        public DateTime? ScheduledDeparture { get; set; }
        public DateTime? ActualDeparture { get; set; }
        public DateTime? ScheduledArrival { get; set; }
        public DateTime? ActualArrival { get; set; }

        // Airport resources (DB ids)
        public int GateId { get; set; }
        public int RunwayId { get; set; }
        public int BaggageConveyorBeltId { get; set; }

        // Passengers
        public int PassengerNumber { get; set; }

        // Baggage
        public int BaggageTotalChecked { get; set; }
    }
}