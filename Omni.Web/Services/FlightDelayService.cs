using Omni.Web.Data;
using Omni.Web.Models;

namespace Omni.Web.Services
{
    /// <summary>
    /// Calculates "possible delays" based on overlapping ResourceUsages.
    /// Rule: if a flight's resource usage overlaps with another flight usage on the same resource,
    /// the later-starting usage ("second plane") gets delayed by the overlap duration.
    /// 
    /// This service does not mutate the DB; delays are computed at read/broadcast time.
    /// </summary>
    public sealed class FlightDelayService : IFlightDelayService
    {
        private const string GateResourceType = "Gate";
        private const string RunwayResourceType = "Runway";

        private readonly AppDbContext _context;

        public FlightDelayService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<int, int>> CalculatePossibleDelaysAsync(CancellationToken cancellationToken = default)
        {
            var usages = await _context.ResourceUsages
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return Calculate(usages);
        }

        internal static Dictionary<int, int> Calculate(IEnumerable<ResourceUsage> usages)
        {
            var delayByFlightId = new Dictionary<int, int>();

            static int ToMinutesCeiling(TimeSpan delay)
                => (int)Math.Ceiling(delay.TotalMinutes);

            void AddDelay(int flightId, TimeSpan delay)
            {
                if (delay <= TimeSpan.Zero) return;

                var minutes = ToMinutesCeiling(delay);
                if (minutes <= 0) return;

                delayByFlightId.TryGetValue(flightId, out var existing);
                delayByFlightId[flightId] = Math.Max(existing, minutes);
            }

            foreach (var group in usages
                         .Where(u => u.ResourceType is GateResourceType or RunwayResourceType)
                         .GroupBy(u => new { u.ResourceType, u.ResourceId }))
            {
                var list = group.OrderBy(u => u.StartsAt).ToList();

                for (var i = 0; i < list.Count; i++)
                {
                    var a = list[i];

                    for (var j = i + 1; j < list.Count; j++)
                    {
                        var b = list[j];

                        if (b.StartsAt >= a.EndsAt)
                            break;

                        if (a.FlightId == b.FlightId)
                            continue;

                        // Delay the later-starting usage by overlap duration.
                        var overlapEnd = a.EndsAt < b.EndsAt ? a.EndsAt : b.EndsAt;
                        var overlap = overlapEnd - b.StartsAt;

                        AddDelay(b.FlightId, overlap);
                    }
                }
            }

            return delayByFlightId;
        }
    }
}
