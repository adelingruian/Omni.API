using Omni.Web.Data;
using Omni.Web.Models;

namespace Omni.Web.Services
{
    public sealed class ResourceUsageService : IResourceUsageService
    {
        private const string GateResourceType = "Gate";
        private const string RunwayResourceType = "Runway";

        // Tuneable buffers
        private static readonly TimeSpan ArrivalGateBefore = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ArrivalGateAfter = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan ArrivalRunwayAfter = TimeSpan.FromMinutes(20);

        private static readonly TimeSpan DepartureGateBefore = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan DepartureGateAfter = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan DepartureRunwayAfter = TimeSpan.FromMinutes(20);

        private readonly AppDbContext _context;

        public ResourceUsageService(AppDbContext context)
        {
            _context = context;
        }

        public async Task UpsertForFlightAsync(Flight flight, CancellationToken cancellationToken = default)
        {
            // Replace all usages for this flight (simple + safe).
            await DeleteForFlightAsync(flight.FlightId, cancellationToken);

            var usages = BuildUsages(flight);
            if (usages.Count == 0)
                return;

            _context.ResourceUsages.AddRange(usages);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteForFlightAsync(int flightId, CancellationToken cancellationToken = default)
        {
            var existing = await _context.ResourceUsages
                .Where(u => u.FlightId == flightId)
                .ToListAsync(cancellationToken);

            if (existing.Count == 0)
                return;

            _context.ResourceUsages.RemoveRange(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static List<ResourceUsage> BuildUsages(Flight flight)
        {
            var usages = new List<ResourceUsage>();

            // Arrival-based usage
            var hasArrival = flight.ScheduledArrival.HasValue || flight.ActualArrival.HasValue;
            if (hasArrival)
            {
                var arrivalRef = (flight.ActualArrival ?? flight.ScheduledArrival)!.Value;

                // If both are present, use (scheduled + delay) as reference. This keeps the window stable
                // and makes delay derived rather than user-provided.
                if (flight.ScheduledArrival.HasValue && flight.ActualArrival.HasValue)
                {
                    var delay = flight.ActualArrival.Value - flight.ScheduledArrival.Value;
                    if (delay > TimeSpan.Zero)
                        arrivalRef = flight.ScheduledArrival.Value + delay;
                }

                usages.Add(new ResourceUsage
                {
                    ResourceType = GateResourceType,
                    ResourceId = flight.GateId,
                    FlightId = flight.FlightId,
                    StartsAt = arrivalRef - ArrivalGateBefore,
                    EndsAt = arrivalRef + ArrivalGateAfter
                });

                usages.Add(new ResourceUsage
                {
                    ResourceType = RunwayResourceType,
                    ResourceId = flight.RunwayId,
                    FlightId = flight.FlightId,
                    StartsAt = arrivalRef,
                    EndsAt = arrivalRef + ArrivalRunwayAfter
                });
            }

            // Departure-based usage
            var hasDeparture = flight.ScheduledDeparture.HasValue || flight.ActualDeparture.HasValue;
            if (hasDeparture)
            {
                var departureRef = (flight.ActualDeparture ?? flight.ScheduledDeparture)!.Value;

                usages.Add(new ResourceUsage
                {
                    ResourceType = GateResourceType,
                    ResourceId = flight.GateId,
                    FlightId = flight.FlightId,
                    StartsAt = departureRef - DepartureGateBefore,
                    EndsAt = departureRef + DepartureGateAfter
                });

                usages.Add(new ResourceUsage
                {
                    ResourceType = RunwayResourceType,
                    ResourceId = flight.RunwayId,
                    FlightId = flight.FlightId,
                    StartsAt = departureRef,
                    EndsAt = departureRef + DepartureRunwayAfter
                });
            }

            usages.RemoveAll(u => u.EndsAt <= u.StartsAt);
            return usages;
        }
    }
}
