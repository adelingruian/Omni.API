namespace Omni.Web.Models
{
    public sealed class ResourceUsage
    {
        public int ResourceUsageId { get; set; }

        public string ResourceType { get; set; } = default!;
        public int ResourceId { get; set; }

        public int FlightId { get; set; }

        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
    }
}
