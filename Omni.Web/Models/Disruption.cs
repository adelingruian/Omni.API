namespace Omni.Web.Models
{
    public class Disruption
    {
        public int DisruptionId { get; set; }

        public string ResourceType { get; set; } = default!;
        public int ResourceId { get; set; }

        public DateTimeOffset StartsAt { get; set; }
        public DateTimeOffset EndsAt { get; set; }

        public string Status { get; set; } = "Open";
    }
}
