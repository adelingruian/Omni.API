using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Omni.Web.Data;
using Omni.Web.Hubs;
using Omni.Web.Models;

namespace Omni.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DisruptionsController : ControllerBase
    {
        private const string GateResourceType = "Gate";
        private const string RunwayResourceType = "Runway";
        private const string DisruptionStatusOpen = "Open";

        private readonly AppDbContext _context;
        private readonly IHubContext<FlightsHub> _hubContext;

        public DisruptionsController(AppDbContext context, IHubContext<FlightsHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public sealed record CreateDisruptionRequest(string ResourceType, int ResourceId);

        [HttpPost]
        public async Task<ActionResult<Disruption>> Create(CreateDisruptionRequest request)
        {
            var todayUtc = DateTimeOffset.UtcNow.Date;

            var disruption = new Disruption
            {
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId,
                StartsAt = todayUtc,
                EndsAt = todayUtc.AddDays(1),
                Status = DisruptionStatusOpen
            };

            _context.Disruptions.Add(disruption);
            await _context.SaveChangesAsync();

            await BroadcastFlightsUpdated();

            return CreatedAtAction(nameof(GetAll), new { id = disruption.DisruptionId }, disruption);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Disruption>>> GetAll()
        {
            var result = await _context.Disruptions
                .AsNoTracking()
                .ToListAsync();

            return result.OrderBy(r => r.StartsAt).ToList();
        }

        [HttpPost("{id:int}/solve")]
        public async Task<IActionResult> Solve(int id)
        {
            var disruption = await _context.Disruptions.FindAsync(id);
            if (disruption == null) return NotFound();

            disruption.Status = "Solved";
            await _context.SaveChangesAsync();

            await BroadcastFlightsUpdated();

            return NoContent();
        }

        private async Task BroadcastFlightsUpdated()
        {
            var flights = await _context.Flights
                .AsNoTracking()
                .OrderBy(f => f.FlightId)
                .ToListAsync();

            var gatesById = await _context.Gates
                .AsNoTracking()
                .ToDictionaryAsync(g => g.GateId, g => g.Name);

            var runwaysById = await _context.Runways
                .AsNoTracking()
                .ToDictionaryAsync(r => r.RunwayId, r => r.Name);

            var disruptedGates = await _context.Disruptions
                .AsNoTracking()
                .Where(d => d.ResourceType == GateResourceType && d.Status == DisruptionStatusOpen)
                .Select(d => d.ResourceId)
                .Distinct()
                .ToListAsync();

            var disruptedRunways = await _context.Disruptions
                .AsNoTracking()
                .Where(d => d.ResourceType == RunwayResourceType && d.Status == DisruptionStatusOpen)
                .Select(d => d.ResourceId)
                .Distinct()
                .ToListAsync();

            var disruptedGateSet = disruptedGates.ToHashSet();
            var disruptedRunwaySet = disruptedRunways.ToHashSet();

            static DateTimeOffset GetDeparture(Flight f) => f.ActualDeparture ?? f.ScheduledDeparture;
            static DateTimeOffset GetArrival(Flight f) => f.ActualArrival ?? f.ScheduledArrival;

            static Dictionary<int, string> BuildConflictMap(IEnumerable<Flight> flights, Func<Flight, int> keySelector)
            {
                var map = new Dictionary<int, string>();

                foreach (var group in flights.GroupBy(keySelector))
                {
                    var list = group
                        .Select(f => new { Flight = f, Start = GetDeparture(f), End = GetArrival(f) })
                        .OrderBy(x => x.Start)
                        .ToList();

                    for (var i = 0; i < list.Count; i++)
                    {
                        for (var j = i + 1; j < list.Count; j++)
                        {
                            if (list[j].Start > list[i].End)
                                break;

                            var a = list[i];
                            var b = list[j];

                            var overlaps = a.Start <= b.End && b.Start <= a.End;
                            if (!overlaps)
                                continue;

                            map.TryAdd(a.Flight.FlightId, b.Flight.FlightNumber);
                            map.TryAdd(b.Flight.FlightId, a.Flight.FlightNumber);
                        }
                    }
                }

                return map;
            }

            var gateConflictMap = BuildConflictMap(flights, f => f.GateId);
            var runwayConflictMap = BuildConflictMap(flights, f => f.RunwayId);

            var payload = flights.Select(f =>
            {
                var gateName = gatesById.TryGetValue(f.GateId, out var gn) ? gn : string.Empty;
                var runwayName = runwaysById.TryGetValue(f.RunwayId, out var rn) ? rn : string.Empty;

                var isGateUnavailable = disruptedGateSet.Contains(f.GateId);
                var hasGateConflict = gateConflictMap.TryGetValue(f.FlightId, out var otherGateFlight);

                var gateStatus = GateStatus.Ok;
                string? gateDescription = null;

                if (isGateUnavailable)
                {
                    gateStatus = GateStatus.Unavailable;
                    gateDescription = "Gate is unavailable";
                }
                else if (hasGateConflict)
                {
                    gateStatus = GateStatus.Conflict;
                    gateDescription = $"Overlaps with {otherGateFlight}";
                }

                var isRunwayUnavailable = disruptedRunwaySet.Contains(f.RunwayId);
                var hasRunwayConflict = runwayConflictMap.TryGetValue(f.FlightId, out var otherRunwayFlight);

                var runwayStatus = RunwayStatus.Ok;
                string? runwayDescription = null;

                if (isRunwayUnavailable)
                {
                    runwayStatus = RunwayStatus.Unavailable;
                    runwayDescription = "Runway is unavailable";
                }
                else if (hasRunwayConflict)
                {
                    runwayStatus = RunwayStatus.Conflict;
                    runwayDescription = $"Overlaps with {otherRunwayFlight}";
                }

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
                    f.PassengerNumber,
                    f.DelayMinutes,
                    f.CrewPilots,
                    f.CrewFlightAttendants,
                    f.BaggageConveyorBelt,
                    f.BaggageTotalChecked);
            }).ToList();

            await _hubContext.Clients.All.SendAsync(FlightsHub.FlightsUpdatedEvent, payload);
        }
    }
}
