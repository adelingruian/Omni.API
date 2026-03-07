using Microsoft.AspNetCore.SignalR;
using Omni.Web.Data;
using Omni.Web.Hubs;
using Omni.Web.Models;

namespace Omni.Web.Services
{
    public sealed class FlightsBroadcastService : IFlightsBroadcastService
    {
        private const string GateResourceType = "Gate";
        private const string RunwayResourceType = "Runway";
        private const string BeltResourceType = "BaggageConveyorBelt";

        private static readonly TimeSpan BaggageAggregationWindow = TimeSpan.FromHours(1);

        private readonly AppDbContext _context;
        private readonly IHubContext<FlightsHub> _hubContext;
        private readonly IFlightDelayService _delay;

        public FlightsBroadcastService(AppDbContext context, IHubContext<FlightsHub> hubContext, IFlightDelayService delay)
        {
            _context = context;
            _hubContext = hubContext;
            _delay = delay;
        }

        public async Task BroadcastFlightsUpdatedAsync(CancellationToken cancellationToken = default)
        {
            var payload = await BuildFlightsPayloadAsync(cancellationToken);
            await _hubContext.Clients.All.SendAsync(FlightsHub.FlightsUpdatedEvent, payload, cancellationToken);
        }

        public async Task<IReadOnlyList<FlightResponse>> BuildFlightsPayloadAsync(CancellationToken cancellationToken = default)
        {
            var flights = await _context.Flights
                .AsNoTracking()
                .OrderBy(f => f.FlightId)
                .ToListAsync(cancellationToken);

            var possibleDelayByFlightId = await _delay.CalculatePossibleDelaysAsync(cancellationToken);

            var gatesById = await _context.Gates
                .AsNoTracking()
                .ToDictionaryAsync(g => g.GateId, g => g.Name, cancellationToken);

            var runwaysById = await _context.Runways
                .AsNoTracking()
                .ToDictionaryAsync(r => r.RunwayId, r => r.Name, cancellationToken);

            var beltsById = await _context.BaggageConveyorBelts
                .AsNoTracking()
                .ToDictionaryAsync(b => b.BaggageConveyorBeltId, b => b.Name, cancellationToken);

            // Load disruptions as ranges per resource.
            var disruptions = await _context.Disruptions
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var disruptedGateRanges = disruptions
                .Where(d => d.ResourceType == GateResourceType)
                .GroupBy(d => d.ResourceId)
                .ToDictionary(g => g.Key, g => g.Select(d => (d.StartsAt, d.EndsAt)).ToList());

            var disruptedRunwayRanges = disruptions
                .Where(d => d.ResourceType == RunwayResourceType)
                .GroupBy(d => d.ResourceId)
                .ToDictionary(g => g.Key, g => g.Select(d => (d.StartsAt, d.EndsAt)).ToList());

            var disruptedBeltRanges = disruptions
                .Where(d => d.ResourceType == BeltResourceType)
                .GroupBy(d => d.ResourceId)
                .ToDictionary(g => g.Key, g => g.Select(d => (d.StartsAt, d.EndsAt)).ToList());

            var usages = await _context.ResourceUsages
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var usagesByFlightId = usages
                .GroupBy(u => u.FlightId)
                .ToDictionary(g => g.Key, g => g.ToList());

            static bool OverlapsHalfOpen(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
                => aStart < bEnd && bStart < aEnd;

            static DateTime ToEnd(DateTime? end) => end ?? DateTime.MaxValue;

            static bool IsUsageDisrupted(
                ResourceUsage usage,
                Dictionary<int, List<(DateTime StartsAt, DateTime? EndsAt)>> rangesByResourceId)
            {
                if (!rangesByResourceId.TryGetValue(usage.ResourceId, out var ranges))
                    return false;

                foreach (var (start, end) in ranges)
                {
                    // Disruption applies if it overlaps the usage interval.
                    if (OverlapsHalfOpen(usage.StartsAt, usage.EndsAt, start, ToEnd(end)))
                        return true;
                }

                return false;
            }

            static (bool HasOverlap, int? OtherFlightId) FindFirstUsageConflictForFlight(
                int flightId,
                IEnumerable<ResourceUsage> usagesForResourceType,
                Func<ResourceUsage, int> resourceKeySelector)
            {
                foreach (var g in usagesForResourceType.GroupBy(resourceKeySelector))
                {
                    var list = g.OrderBy(u => u.StartsAt).ToList();

                    for (var i = 0; i < list.Count; i++)
                    {
                        for (var j = i + 1; j < list.Count; j++)
                        {
                            if (list[j].StartsAt >= list[i].EndsAt)
                                break;

                            var a = list[i];
                            var b = list[j];

                            if (a.FlightId == b.FlightId)
                                continue;

                            if (!OverlapsHalfOpen(a.StartsAt, a.EndsAt, b.StartsAt, b.EndsAt))
                                continue;

                            if (a.FlightId == flightId) return (true, b.FlightId);
                            if (b.FlightId == flightId) return (true, a.FlightId);
                        }
                    }
                }

                return (false, null);
            }

            static int ComputeTotalDelayMinutes(DateTime? scheduled, DateTime? actual, int possibleDelayMinutes)
            {
                var primaryDelay = 0;
                if (scheduled.HasValue && actual.HasValue)
                {
                    var delta = actual.Value - scheduled.Value;
                    if (delta > TimeSpan.Zero)
                        primaryDelay = (int)Math.Ceiling(delta.TotalMinutes);
                }

                return primaryDelay + possibleDelayMinutes;
            }

            static DateTime? GetRefTimeWithPossibleDelay(DateTime? scheduled, DateTime? actual, int possibleDelayMinutes)
            {
                var reference = actual ?? scheduled;
                if (!reference.HasValue)
                    return null;

                if (possibleDelayMinutes > 0)
                    reference = reference.Value.AddMinutes(possibleDelayMinutes);

                return reference;
            }

            int GetBeltBaggageInWindow(int beltId, DateTime refTime)
            {
                var start = refTime - BaggageAggregationWindow;
                var end = refTime + BaggageAggregationWindow;

                return flights
                    .Where(x => x.BaggageConveyorBeltId == beltId)
                    .Select(x =>
                    {
                        var t = x.ActualArrival ?? x.ScheduledArrival ?? x.ActualDeparture ?? x.ScheduledDeparture;
                        return (Flight: x, Time: t);
                    })
                    .Where(x => x.Time.HasValue && x.Time.Value >= start && x.Time.Value <= end)
                    .Sum(x => x.Flight.BaggageTotalChecked);
            }

            return flights.Select(f =>
            {
                possibleDelayByFlightId.TryGetValue(f.FlightId, out var possibleDelayMinutes);

                var gateName = gatesById.TryGetValue(f.GateId, out var gn) ? gn : string.Empty;
                var runwayName = runwaysById.TryGetValue(f.RunwayId, out var rn) ? rn : string.Empty;
                var beltName = beltsById.TryGetValue(f.BaggageConveyorBeltId, out var bn) ? bn : string.Empty;

                usagesByFlightId.TryGetValue(f.FlightId, out var flightUsages);
                flightUsages ??= [];

                var gateUsagesForFlight = flightUsages.Where(u => u.ResourceType == GateResourceType).ToList();
                var runwayUsagesForFlight = flightUsages.Where(u => u.ResourceType == RunwayResourceType).ToList();
                var beltUsagesForFlight = flightUsages.Where(u => u.ResourceType == BeltResourceType).ToList();

                var isGateUnavailable = gateUsagesForFlight.Any(u => IsUsageDisrupted(u, disruptedGateRanges));
                var isRunwayUnavailable = runwayUsagesForFlight.Any(u => IsUsageDisrupted(u, disruptedRunwayRanges));
                var isBeltUnavailable = beltUsagesForFlight.Any(u => IsUsageDisrupted(u, disruptedBeltRanges));

                var (hasGateConflict, otherGateFlightId) = FindFirstUsageConflictForFlight(
                    f.FlightId,
                    usages.Where(u => u.ResourceType == GateResourceType),
                    u => u.ResourceId);

                var otherGateFlightNumber = otherGateFlightId.HasValue
                    ? flights.FirstOrDefault(x => x.FlightId == otherGateFlightId.Value)?.FlightNumber
                    : null;

                var gateStatus = GateStatus.Ok;
                string? gateDescription = null;

                if (isGateUnavailable)
                {
                    gateStatus = GateStatus.Unavailable;
                    gateDescription = "Gate is unavailable";
                }
                else if (!string.IsNullOrWhiteSpace(otherGateFlightNumber))
                {
                    gateStatus = GateStatus.Conflict;
                    gateDescription = $"Overlaps with {otherGateFlightNumber}";
                }

                var (hasRunwayConflict, otherRunwayFlightId) = FindFirstUsageConflictForFlight(
                    f.FlightId,
                    usages.Where(u => u.ResourceType == RunwayResourceType),
                    u => u.ResourceId);

                var otherRunwayFlightNumber = otherRunwayFlightId.HasValue
                    ? flights.FirstOrDefault(x => x.FlightId == otherRunwayFlightId.Value)?.FlightNumber
                    : null;

                var runwayStatus = RunwayStatus.Ok;
                string? runwayDescription = null;

                if (isRunwayUnavailable)
                {
                    runwayStatus = RunwayStatus.Unavailable;
                    runwayDescription = "Runway is unavailable";
                }
                else if (!string.IsNullOrWhiteSpace(otherRunwayFlightNumber))
                {
                    runwayStatus = RunwayStatus.Conflict;
                    runwayDescription = $"Overlaps with {otherRunwayFlightNumber}";
                }

                var (hasBeltConflict, otherBeltFlightId) = FindFirstUsageConflictForFlight(
                    f.FlightId,
                    usages.Where(u => u.ResourceType == BeltResourceType),
                    u => u.ResourceId);

                var otherBeltFlightNumber = otherBeltFlightId.HasValue
                    ? flights.FirstOrDefault(x => x.FlightId == otherBeltFlightId.Value)?.FlightNumber
                    : null;

                var beltStatus = BeltStatus.Ok;
                string? beltDescription = null;

                if (isBeltUnavailable)
                {
                    beltStatus = BeltStatus.Unavailable;
                    beltDescription = "Belt is unavailable";
                }
                else if (!string.IsNullOrWhiteSpace(otherBeltFlightNumber))
                {
                    beltStatus = BeltStatus.Conflict;
                    beltDescription = $"Overlaps with {otherBeltFlightNumber}";
                }

                var arrivalDelay = ComputeTotalDelayMinutes(f.ScheduledArrival, f.ActualArrival, possibleDelayMinutes);
                var departureDelay = ComputeTotalDelayMinutes(f.ScheduledDeparture, f.ActualDeparture, possibleDelayMinutes);
                var totalDelayMins = Math.Max(arrivalDelay, departureDelay);

                var refTime = GetRefTimeWithPossibleDelay(
                    f.ScheduledArrival ?? f.ScheduledDeparture,
                    f.ActualArrival ?? f.ActualDeparture,
                    possibleDelayMinutes);

                var totalBagsOnBelt = refTime.HasValue
                    ? GetBeltBaggageInWindow(f.BaggageConveyorBeltId, refTime.Value)
                    : f.BaggageTotalChecked;

                var (points, severity) = DisruptionScorecard.Calculate(
                    totalDelayMins,
                    f.PassengerNumber,
                    gateStatus != GateStatus.Ok,
                    runwayStatus != RunwayStatus.Ok,
                    totalBagsOnBelt);

                return new FlightResponse(
                    f.FlightId,
                    f.FlightNumber,
                    f.Aircraft,
                    f.Origin,
                    f.Destination,
                    f.ScheduledDeparture,
                    f.ActualDeparture,
                    f.ScheduledArrival,
                    f.ActualArrival,
                    new GateResponse(f.GateId, gateName, gateStatus, gateDescription),
                    new RunwayResponse(f.RunwayId, runwayName, runwayStatus, runwayDescription),
                    new BeltResponse(f.BaggageConveyorBeltId, beltName, beltStatus, beltDescription),
                    f.PassengerNumber,
                    possibleDelayMinutes,
                    f.BaggageTotalChecked,
                    new DisruptionScore(points, severity));
            }).ToList();
        }
    }
}
